using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

// =====================================================================
//  SEMS — Student Email Management System (eAdmin side).
//  Eligibility, idempotent generation, email creation, lifecycle, stats,
//  list/search and complaint handling. Tables live in campus_dynamics_portal
//  (sems_*); reached cross-DB from the campus_dynamics (vac) connection.
// =====================================================================
public static class SemsAdmin
{
    private const decimal MinPaid = 100000m;

    private static string Conn
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    // =================================================================
    //  WHO QUALIFIES FOR A UNIVERSITY EMAIL
    //
    //  Three rules, and a student must satisfy all three:
    //
    //    1. admitted in 2026 or later   — the intake SEMS was built for
    //    2. paid UGX 100,000 or more    — a real student, not a name on a form
    //    3. has signed in to the portal — the account exists and works
    //
    //  Plus two mechanics that are not policy and never were: skip anyone already
    //  in the pipeline, so Generate can be pressed twice safely; and skip anyone
    //  who already holds an @mru.ac.ug address, because issuing a second one is
    //  not a favour.
    //
    //  Note what is NOT a rule any more. The old predicate required
    //  acad_student.email to be EMPTY, which quietly disqualified every student
    //  who had given a personal Gmail address — 200 of the 2026 intake, and the
    //  reason Generate had stopped finding anyone. Having a personal address is
    //  the normal case; it says nothing about whether the university owes them
    //  an official one.
    //
    //  Written once and shared, because this used to be four separate copies of
    //  the same WHERE clause. They had already drifted, so the count on the
    //  dashboard, the list on the Candidates tab and the students Generate
    //  actually created were each answering a slightly different question.
    // =================================================================

    private const int MinEntryYear = 2026;

    /// <summary>Rule 2. Pre-aggregated on purpose — the per-row correlated version timed out.</summary>
    private const string PaidJoin =
        "JOIN (SELECT TRIM(regno) rg, SUM(amount) paid FROM campus_dynamics_accounts.fin_studentfeestracking " +
        "      WHERE trans_type='Payment' GROUP BY TRIM(regno)) pay ON pay.rg = TRIM(s.regno) ";

    /// <summary>
    /// Rule 3. The membership provider stamps LastLoginDate = CreationDate when it creates an
    /// account, so a row where the two are equal has never actually been signed in to; "has
    /// logged in" is strictly greater. Both columns are NOT NULL in practice (0 nulls of 101k).
    /// Same collation on both sides of the name join, so the UNIQUE index on it is used.
    /// </summary>
    private const string LoginJoin =
        "JOIN campus_dynamics_portal.my_aspnet_users mu ON mu.name = TRIM(s.regno) " +
        "JOIN campus_dynamics_portal.my_aspnet_membership mm ON mm.userId = mu.id ";
    private const string HasLoggedIn = "mm.LastLoginDate > mm.CreationDate";

    /// <summary>Already holds an official address — there is nothing to issue.</summary>
    private const string NoUniversityAddress =
        "LOWER(TRIM(IFNULL(s.email,''))) NOT LIKE '%@" + UniversityDomain + "'";
    private const string UniversityDomain = "mru.ac.ug";

    private const string NotInPipeline =
        "NOT EXISTS (SELECT 1 FROM campus_dynamics_portal.sems_email_creations e WHERE e.regno=TRIM(s.regno))";

    /// <summary>The full rule, as a FROM + WHERE that any query can build on.</summary>
    private static readonly string EligibleFrom =
        "FROM campus_dynamics.acad_student s " + PaidJoin + LoginJoin +
        "WHERE s.entryyear >= " + MinEntryYear + " " +
        "  AND pay.paid >= " + MinPaid.ToString("0") + " " +
        "  AND " + HasLoggedIn + " " +
        "  AND " + NoUniversityAddress + " " +
        "  AND " + NotInPipeline + " ";

    /// <summary>
    /// Everyone the pipeline could ever be asked about: the intake, minus those who already
    /// have an address or a record. Payment and sign-in are left OUT here on purpose — the
    /// Candidates tab exists precisely to show the students the automatic rule skips, and it
    /// cannot do that if it applies the same filter.
    /// </summary>
    private static readonly string CandidateFrom =
        "FROM campus_dynamics.acad_student s " + PaySub +
        "LEFT JOIN campus_dynamics_portal.my_aspnet_users mu ON mu.name = TRIM(s.regno) " +
        "LEFT JOIN campus_dynamics_portal.my_aspnet_membership mm ON mm.userId = mu.id " +
        "WHERE s.entryyear >= " + MinEntryYear + " " +
        "  AND " + NoUniversityAddress + " " +
        "  AND " + NotInPipeline + " ";

    // =================================================================
    //  THE STAGE VOCABULARY
    //
    //  Three stages, and only three. They are listed here once and every screen
    //  reads them from here, because they had drifted into four different lists
    //  that did not agree:
    //
    //    * the filter offered "Email created", a stage no record has ever held;
    //    * the change-stage prompt offered "EMAIL_CREATED" and "SUSPENDED",
    //      neither of which exists — typing one wrote a value the badge could
    //      not render and the filter could never find again;
    //    * the badge map and the server's own whitelist each knew a different
    //      set.
    //
    //  current_status is not a second thing to choose. It is a fixed companion
    //  of the stage, so it is derived here rather than set by hand.
    // =================================================================
    public class Stage
    {
        public string key { get; set; }
        public string label { get; set; }
        /// <summary>What an admin is doing by choosing it, in the imperative.</summary>
        public string action { get; set; }
        public string status { get; set; }
        public string hint { get; set; }
    }

    private static readonly Stage[] Stages = new[]
    {
        new Stage { key = "PENDING_CREATION",     label = "Pending creation",     action = "Send back to Pending creation",
                    status = "PENDING",
                    hint = "No address issued yet. The student is waiting for one and the portal does not hold them." },
        new Stage { key = "READY_FOR_COLLECTION", label = "Ready for collection", action = "Mark Ready for collection",
                    status = "READY",
                    hint = "An address exists and is waiting to be collected. The portal holds the student at the email journey until they do." },
        new Stage { key = "COMPLETED",            label = "Completed",            action = "Mark Completed",
                    status = "COMPLETED",
                    hint = "The student has seen their address and password. Nothing further is asked of them." }
    };

    private static Stage StageOf(string key)
    {
        key = (key ?? "").Trim().ToUpperInvariant();
        foreach (var st in Stages) if (st.key == key) return st;
        return null;
    }

    /// <summary>The stage list, for the filter, the badges and the change-stage dropdown.</summary>
    public static string StageList()
    {
        return new JavaScriptSerializer().Serialize(new { success = true, stages = Stages });
    }

    private static string Actor()
    {
        try { var u = HttpContext.Current.Session["username"]; return u == null ? "admin" : u.ToString(); }
        catch { return "admin"; }
    }

    private static void Log(MySqlConnection c, MySqlTransaction tx, int creationId, string regno, string action,
                            string from, string to, string detail)
    {
        try
        {
            using (var cmd = new MySqlCommand(
                "INSERT INTO campus_dynamics_portal.sems_activity_log (creation_id,regno,action,stage_from,stage_to,actor,actor_role,detail,created_at) " +
                "VALUES (@id,@r,@a,@f,@t,@who,'ADMIN',@d,NOW())", c, tx))
            {
                cmd.Parameters.AddWithValue("@id", creationId <= 0 ? (object)DBNull.Value : creationId);
                cmd.Parameters.AddWithValue("@r", regno ?? "");
                cmd.Parameters.AddWithValue("@a", action);
                cmd.Parameters.AddWithValue("@f", (object)from ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@t", (object)to ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@who", Actor());
                cmd.Parameters.AddWithValue("@d", (object)detail ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }
        catch { /* logging must never break the action */ }
    }

    private static void Notify(MySqlConnection c, MySqlTransaction tx, string regno, string title, string msg, string icon)
    {
        try
        {
            using (var cmd = new MySqlCommand(
                "INSERT INTO campus_dynamics_portal.sems_notifications (regno,title,message,icon,is_read,created_at) VALUES (@r,@t,@m,@i,'No',NOW())", c, tx))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                cmd.Parameters.AddWithValue("@t", title);
                cmd.Parameters.AddWithValue("@m", (object)msg ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@i", (object)icon ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }
        catch { }
    }

    // ── Dashboard KPIs ───────────────────────────────────────────────
    public static string Stats()
    {
        var js = new JavaScriptSerializer();
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                // "Eligible" and "paid < 100k" differ only by the payment threshold, but each used
                // to run its own copy of the fees aggregate — the single most expensive thing on
                // this page at ~173ms a piece. One pass now yields both counts, halving page-load
                // cost, and the two figures are guaranteed to come from the same instant.
                int eligible = 0, partial = 0;
                // "Eligible" is exactly what Generate would create — all three rules — so the
                // number on the button and the number of rows it makes are the same number.
                // "Partial" is the near-miss it is worth an admin knowing about: the money is
                // short, everything else is in order.
                using (var cmd = new MySqlCommand(
                    "SELECT SUM(pay.paid >= " + MinPaid.ToString("0") + " AND " + HasLoggedIn + ") AS eligible," +
                    "       SUM(pay.paid > 0 AND pay.paid < " + MinPaid.ToString("0") + ") AS partial " +
                    "FROM campus_dynamics.acad_student s " + PaySub +
                    "LEFT JOIN campus_dynamics_portal.my_aspnet_users mu ON mu.name = TRIM(s.regno) " +
                    "LEFT JOIN campus_dynamics_portal.my_aspnet_membership mm ON mm.userId = mu.id " +
                    "WHERE s.entryyear >= " + MinEntryYear + " " +
                    "  AND " + NoUniversityAddress + " AND " + NotInPipeline, c))
                {
                    cmd.CommandTimeout = 60;
                    using (var rd = cmd.ExecuteReader())
                        if (rd.Read())
                        {
                            eligible = rd["eligible"] == DBNull.Value ? 0 : Convert.ToInt32(rd["eligible"]);
                            partial  = rd["partial"]  == DBNull.Value ? 0 : Convert.ToInt32(rd["partial"]);
                        }
                }
                Func<string, int> cnt = w => Scalar(c, "SELECT COUNT(*) FROM campus_dynamics_portal.sems_email_creations " + w);
                int total     = cnt("");
                int pending   = cnt("WHERE current_stage='PENDING_CREATION'");
                int ready     = cnt("WHERE current_stage = 'READY_FOR_COLLECTION'");
                int learning  = cnt("WHERE gmail_guide_done_at IS NOT NULL");
                int quiz      = cnt("WHERE quiz_passed_at IS NOT NULL");
                int activated = cnt("WHERE verification_status='VERIFIED'");
                int completed = cnt("WHERE current_stage='COMPLETED'");
                int pwchanged = cnt("WHERE password_changed='Yes'");
                int complaints= Scalar(c, "SELECT COUNT(*) FROM campus_dynamics_portal.sems_complaints WHERE status NOT IN ('RESOLVED','CLOSED')");
                int forgot    = Scalar(c, "SELECT COUNT(*) FROM campus_dynamics_portal.sems_complaints WHERE category='Forgot Password' AND status NOT IN ('RESOLVED','CLOSED')");
                double rate = total > 0 ? Math.Round(completed * 100.0 / total, 1) : 0;

                // Per-campus breakdown. One grouped pass rather than a query per campus, so
                // adding a third campus later costs nothing.
                var campuses = new List<object>();
                using (var cmd = new MySqlCommand(
                    "SELECT IFNULL(campus,'') campus, COUNT(*) total," +
                    " SUM(current_stage='PENDING_CREATION') pending," +
                    " SUM(current_stage = 'READY_FOR_COLLECTION') ready," +
                    " SUM(current_stage='COMPLETED') completed," +
                    " SUM(verification_status='VERIFIED') verified" +
                    " FROM campus_dynamics_portal.sems_email_creations" +
                    " GROUP BY campus ORDER BY total DESC", c))
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read())
                    {
                        int cTotal = Convert.ToInt32(rd["total"]);
                        int cDone  = Convert.ToInt32(rd["completed"]);
                        campuses.Add(new
                        {
                            code = rd["campus"].ToString(),
                            name = CampusName(rd["campus"].ToString()),
                            total = cTotal,
                            pending = Convert.ToInt32(rd["pending"]),
                            ready = Convert.ToInt32(rd["ready"]),
                            completed = cDone,
                            verified = Convert.ToInt32(rd["verified"]),
                            successRate = cTotal > 0 ? Math.Round(cDone * 100.0 / cTotal, 1) : 0
                        });
                    }

                return js.Serialize(new { success = true, eligible, partial, total, pending, ready, learning, quiz, activated, completed, pwchanged, complaints, forgot, successRate = rate, campuses });
            }
        }
        catch (Exception ex) { return js.Serialize(new { success = false, message = ex.Message }); }
    }

    // ── Generate eligible (idempotent) ───────────────────────────────
    public static string GenerateEligible()
    {
        var js = new JavaScriptSerializer();
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                int created;
                using (var cmd = new MySqlCommand(
                    "INSERT INTO campus_dynamics_portal.sems_email_creations " +
                    " (regno, entryno, admission_year, campus, programme, student_name, current_stage, current_status, creation_date, created_by, paid_amount_snapshot) " +
                    "SELECT TRIM(s.regno), s.entryno, s.entryyear, s.studCampus, s.progid, " +
                    "  NULLIF(TRIM(CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,''))),''), " +
                    "  'PENDING_CREATION','PENDING', NOW(), @who, pay.paid " +
                    EligibleFrom, c))
                {
                    cmd.CommandTimeout = 120;
                    cmd.Parameters.AddWithValue("@who", Actor());
                    created = cmd.ExecuteNonQuery();
                }
                Log(c, null, 0, null, "generate_eligible", null, "PENDING_CREATION", created + " record(s) created");
                return js.Serialize(new { success = true, created, message = created + " new eligible student(s) added to the pipeline." });
            }
        }
        catch (Exception ex) { return js.Serialize(new { success = false, message = ex.Message }); }
    }

    // ── Create the email for one student ─────────────────────────────
    public static string CreateEmail(string regno, string email, string tempPw, string notes)
    {
        var js = new JavaScriptSerializer();
        regno = (regno ?? "").Trim();
        // Normalised the way the directory and the UNIQUE index store it, so "J.Doe26@MRU.ac.ug "
        // and "jdoe26@mru.ac.ug" can never end up as two records for one mailbox.
        email = (email ?? "").Trim().ToLowerInvariant();
        tempPw = (tempPw ?? "").Trim();
        if (regno == "" || email == "" || tempPw == "")
            return js.Serialize(new { success = false, message = "Email address and temporary password are required." });
        if (!SemsBatch.IsValidEmail(email))
            return js.Serialize(new { success = false, message = "\"" + email + "\" is not a valid address. Use letters, digits and dots only, starting with a letter." });
        if (!SemsBatch.IsUsablePassword(tempPw, email))
            return js.Serialize(new { success = false, message = "The temporary password must be at least " + SemsBatch.MinPasswordLength +
                                                                " characters and cannot be the address itself — Google rejects both." });
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                using (var tx = c.BeginTransaction())
                {
                    int id = 0; string stage = "";
                    using (var q = new MySqlCommand("SELECT id, current_stage FROM campus_dynamics_portal.sems_email_creations WHERE regno=@r LIMIT 1", c, tx))
                    { q.Parameters.AddWithValue("@r", regno); using (var rd = q.ExecuteReader()) if (rd.Read()) { id = Convert.ToInt32(rd[0]); stage = rd[1].ToString(); } }
                    if (id == 0) { tx.Rollback(); return js.Serialize(new { success = false, message = "No pipeline record for that student." }); }

                    using (var up = new MySqlCommand(
                        "UPDATE campus_dynamics_portal.sems_email_creations SET email_address=@e, temp_password=@p, " +
                        "current_stage='READY_FOR_COLLECTION', current_status='READY', email_created_at=NOW(), " +
                        "notes=CONCAT(COALESCE(NULLIF(notes,''),''), CASE WHEN @n<>'' THEN CONCAT(' | ', @n) ELSE '' END), " +
                        "last_updated_by=@who, last_updated_at=NOW() WHERE id=@id", c, tx))
                    {
                        up.Parameters.AddWithValue("@e", email);
                        up.Parameters.AddWithValue("@p", tempPw);
                        up.Parameters.AddWithValue("@n", notes ?? "");
                        up.Parameters.AddWithValue("@who", Actor());
                        up.Parameters.AddWithValue("@id", id);
                        up.ExecuteNonQuery();
                    }
                    Log(c, tx, id, regno, "create_email", stage, "READY_FOR_COLLECTION", email);
                    Notify(c, tx, regno, "Your university email address is ready", "Open the portal to collect your @mru.ac.ug address and its password. It takes about five minutes.", "mail");
                    tx.Commit();
                    return js.Serialize(new { success = true, message = "Email created. Student can now start onboarding." });
                }
            }
        }
        catch (MySqlException mex)
        {
            if (mex.Number == 1062)
                return js.Serialize(new { success = false, message = "That email address is already assigned to another student." });
            return js.Serialize(new { success = false, message = mex.Message });
        }
        catch (Exception ex) { return js.Serialize(new { success = false, message = ex.Message }); }
    }

    // ── List (GET-filter driven, paginated) ──────────────────────────
    public static string Search(string q, string stage, string campus, string programme, string year, string verification, int page, int pageSize)
    {
        var js = new JavaScriptSerializer();
        if (pageSize < 1 || pageSize > 200) pageSize = 25;
        if (page < 1) page = 1;
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                var wb = new StringBuilder("WHERE 1=1");
                var ps = new List<MySqlParameter>();

                // Free-text search, tokenised on whitespace: EVERY token must appear in at least
                // one of the searchable columns, in any order.
                //
                // Three real failures this fixes, each verified against live data:
                //   * entryno was not searched at all, even though the box promises "student
                //     number" — searching 26/U/DME/0001/M/DAY returned nothing.
                //   * student_name is stored "FIRSTNAME SURNAME", but class lists (and most
                //     people) write surname first, so "SSENTONGO WILBERFORCE" matched nothing.
                //   * a stray double space broke the match outright.
                // Matching per-token rather than on the raw string makes word order and
                // whitespace irrelevant. Capped so a pathological paste can't build a huge WHERE.
                if (!string.IsNullOrWhiteSpace(q))
                {
                    string[] tokens = q.Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    int used = 0;
                    for (int i = 0; i < tokens.Length && used < 6; i++)
                    {
                        string tok = tokens[i].Trim();
                        if (tok.Length == 0) continue;
                        string pn = "@q" + used;
                        wb.Append(" AND (regno LIKE ").Append(pn)
                          .Append(" OR entryno LIKE ").Append(pn)
                          .Append(" OR student_name LIKE ").Append(pn)
                          .Append(" OR email_address LIKE ").Append(pn).Append(")");
                        ps.Add(new MySqlParameter(pn, "%" + tok + "%"));
                        used++;
                    }
                }
                if (!string.IsNullOrWhiteSpace(stage)) { wb.Append(" AND current_stage=@st"); ps.Add(new MySqlParameter("@st", stage)); }
                if (!string.IsNullOrWhiteSpace(campus)) { wb.Append(" AND campus=@cm"); ps.Add(new MySqlParameter("@cm", campus)); }
                if (!string.IsNullOrWhiteSpace(programme)) { wb.Append(" AND programme=@pr"); ps.Add(new MySqlParameter("@pr", programme)); }
                if (!string.IsNullOrWhiteSpace(year)) { wb.Append(" AND admission_year=@yr"); ps.Add(new MySqlParameter("@yr", year)); }
                if (!string.IsNullOrWhiteSpace(verification)) { wb.Append(" AND verification_status=@vs"); ps.Add(new MySqlParameter("@vs", verification)); }
                string where = wb.ToString();

                int total;
                using (var cc = new MySqlCommand("SELECT COUNT(*) FROM campus_dynamics_portal.sems_email_creations " + where, c))
                { foreach (var p in ps) cc.Parameters.Add(Clone(p)); total = Convert.ToInt32(cc.ExecuteScalar()); }
                int pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
                if (page > pageCount) page = pageCount;

                var rows = new List<object>();
                using (var cmd = new MySqlCommand(
                    "SELECT id, regno, student_name, programme, campus, admission_year, IFNULL(email_address,'') email, " +
                    "current_stage, current_status, verification_status, complaint_status, IFNULL(paid_amount_snapshot,0) paid, DATE_FORMAT(creation_date,'%Y-%m-%d') cdate, " +
                    "DATE_FORMAT(last_updated_at,'%Y-%m-%d %H:%i') udate " +
                    "FROM campus_dynamics_portal.sems_email_creations " + where + " ORDER BY id DESC LIMIT @off,@ps", c))
                {
                    foreach (var p in ps) cmd.Parameters.Add(Clone(p));
                    cmd.Parameters.AddWithValue("@off", (page - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@ps", pageSize);
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read())
                            rows.Add(new
                            {
                                id = Convert.ToInt32(rd["id"]),
                                regno = rd["regno"].ToString(),
                                name = rd["student_name"].ToString(),
                                programme = rd["programme"].ToString(),
                                campus = CampusName(rd["campus"].ToString()),
                                year = rd["admission_year"].ToString(),
                                email = rd["email"].ToString(),
                                stage = rd["current_stage"].ToString(),
                                status = rd["current_status"].ToString(),
                                verification = rd["verification_status"].ToString(),
                                complaint = rd["complaint_status"].ToString(),
                                paid = rd["paid"] == DBNull.Value ? 0 : Convert.ToDecimal(rd["paid"]),
                                created = rd["cdate"].ToString(),
                                updated = rd["udate"] == DBNull.Value ? "" : rd["udate"].ToString()
                            });
                }
                return js.Serialize(new { success = true, total, page, pageCount, pageSize, rows });
            }
        }
        catch (Exception ex) { return js.Serialize(new { success = false, message = ex.Message }); }
    }

    // ── Filter option lists (campuses / programmes present in the pipeline) ──
    public static string Filters()
    {
        var js = new JavaScriptSerializer();
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                var progs = new List<object>();
                using (var cmd = new MySqlCommand(
                    "SELECT DISTINCT e.programme, IFNULL(p.progname,e.programme) nm FROM campus_dynamics_portal.sems_email_creations e " +
                    "LEFT JOIN campus_dynamics.acad_programme p ON p.progcode=e.programme WHERE IFNULL(e.programme,'')<>'' ORDER BY nm", c))
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read()) progs.Add(new { code = rd[0].ToString(), name = rd[1].ToString() });

                // Campuses actually present in the pipeline, with counts, so the filter only
                // ever offers options that return something.
                var camps = new List<object>();
                using (var cmd = new MySqlCommand(
                    "SELECT IFNULL(campus,'') campus, COUNT(*) n FROM campus_dynamics_portal.sems_email_creations " +
                    "WHERE IFNULL(campus,'')<>'' GROUP BY campus ORDER BY n DESC", c))
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read())
                        camps.Add(new { code = rd[0].ToString(), name = CampusName(rd[0].ToString()), count = Convert.ToInt32(rd[1]) });

                return js.Serialize(new { success = true, programmes = progs, campuses = camps });
            }
        }
        catch (Exception ex) { return js.Serialize(new { success = false, message = ex.Message }); }
    }

    // ── Complaint queue (admin) ──────────────────────────────────────
    public static string Complaints(string status)
    {
        var js = new JavaScriptSerializer();
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                string w = string.IsNullOrWhiteSpace(status) ? "WHERE status NOT IN ('RESOLVED','CLOSED')" : "WHERE status=@s";
                var list = new List<object>();
                // Joined to the pipeline record so the list says WHO is complaining and what
                // address they hold. Without it every row was a student number, and an
                // administrator had to open each one to find out whether it was even actionable.
                using (var cmd = new MySqlCommand(
                    "SELECT k.id, k.regno, k.category, k.description, k.status, k.priority, " +
                    "IFNULL(k.admin_response,'') resp, DATE_FORMAT(k.created_at,'%Y-%m-%d %H:%i') cat, " +
                    "DATE_FORMAT(k.updated_at,'%Y-%m-%d %H:%i') uat, IFNULL(k.handled_by,'') handled, " +
                    "IFNULL(e.student_name,'') sname, IFNULL(e.email_address,'') email, " +
                    "IFNULL(e.current_stage,'') stage, IFNULL(e.programme,'') programme " +
                    "FROM campus_dynamics_portal.sems_complaints k " +
                    "LEFT JOIN campus_dynamics_portal.sems_email_creations e ON e.regno = k.regno " +
                    w.Replace("WHERE status", "WHERE k.status") +
                    " ORDER BY (k.status='SUBMITTED') DESC, k.id DESC LIMIT 200", c))
                {
                    if (!string.IsNullOrWhiteSpace(status)) cmd.Parameters.AddWithValue("@s", status);
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read())
                            list.Add(new { id = Convert.ToInt64(rd["id"]), regno = rd["regno"].ToString(), category = rd["category"].ToString(),
                                description = rd["description"].ToString(), status = rd["status"].ToString(), priority = rd["priority"].ToString(),
                                response = rd["resp"].ToString(), created = rd["cat"].ToString(), updated = rd["uat"].ToString(),
                                handledBy = rd["handled"].ToString(), name = rd["sname"].ToString(), email = rd["email"].ToString(),
                                stage = rd["stage"].ToString(), programme = rd["programme"].ToString(),
                                inPipeline = rd["sname"].ToString() != "" || rd["email"].ToString() != "" });
                }
                return js.Serialize(new { success = true, complaints = list });
            }
        }
        catch (Exception ex) { return js.Serialize(new { success = false, message = ex.Message }); }
    }

    public static string RespondComplaint(long id, string status, string response)
    {
        var js = new JavaScriptSerializer();
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                string regno = "";
                using (var q = new MySqlCommand("SELECT regno FROM campus_dynamics_portal.sems_complaints WHERE id=@id", c))
                { q.Parameters.AddWithValue("@id", id); var o = q.ExecuteScalar(); regno = o == null ? "" : o.ToString(); }
                using (var up = new MySqlCommand(
                    "UPDATE campus_dynamics_portal.sems_complaints SET status=@s, admin_response=@r, handled_by=@who, updated_at=NOW() WHERE id=@id", c))
                {
                    up.Parameters.AddWithValue("@s", string.IsNullOrWhiteSpace(status) ? "RESPONDED" : status);
                    up.Parameters.AddWithValue("@r", (object)response ?? DBNull.Value);
                    up.Parameters.AddWithValue("@who", Actor());
                    up.Parameters.AddWithValue("@id", id);
                    up.ExecuteNonQuery();
                }
                if (regno != "") Notify(c, null, regno, "Update on your email complaint", string.IsNullOrWhiteSpace(response) ? "Status: " + status : response, "help");
                return js.Serialize(new { success = true, message = "Complaint updated." });
            }
        }
        catch (Exception ex) { return js.Serialize(new { success = false, message = ex.Message }); }
    }

    // Pre-aggregated payments (tracking 'Payment' rows) joined once — the same source the
    // eligibility uses, so amounts on screen match the eligibility decision.
    private const string PaySub =
        "LEFT JOIN (SELECT TRIM(regno) rg, SUM(amount) paid FROM campus_dynamics_accounts.fin_studentfeestracking " +
        "           WHERE trans_type='Payment' GROUP BY TRIM(regno)) pay ON pay.rg = TRIM(s.regno) ";

    // ── Candidates: 2026+ students NOT yet in the pipeline, WITH their payment amount ──
    // This is what surfaces students who "paid but don't appear": the partial payers
    // (1..99,999) that the >=100k auto-generate skips. payFilter: eligible | partial | none | (all).
    public static string Candidates(string q, string payFilter, int page, int pageSize)
    {
        var js = new JavaScriptSerializer();
        if (pageSize < 1 || pageSize > 200) pageSize = 25;
        if (page < 1) page = 1;
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                var wb = new StringBuilder(CandidateFrom);
                var ps = new List<MySqlParameter>();
                payFilter = (payFilter ?? "").Trim().ToLowerInvariant();
                // "eligible" here means exactly what Generate means by it, sign-in included, so
                // the tab and the button cannot disagree about who is ready.
                if (payFilter == "eligible") wb.Append("AND pay.paid >= " + MinPaid.ToString("0") + " AND " + HasLoggedIn + " ");
                else if (payFilter == "partial") wb.Append("AND pay.paid > 0 AND pay.paid < " + MinPaid.ToString("0") + " ");
                else if (payFilter == "none") wb.Append("AND (pay.paid IS NULL OR pay.paid = 0) ");
                else if (payFilter == "paid") wb.Append("AND pay.paid > 0 ");
                else if (payFilter == "neverloggedin") wb.Append("AND NOT (" + HasLoggedIn + ") ");
                if (!string.IsNullOrWhiteSpace(q)) { wb.Append("AND (TRIM(s.regno) LIKE @q OR CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,'')) LIKE @q) "); ps.Add(new MySqlParameter("@q", "%" + q.Trim() + "%")); }
                string from = wb.ToString();

                int total;
                using (var cc = new MySqlCommand("SELECT COUNT(*) " + from, c)) { cc.CommandTimeout = 120; foreach (var p in ps) cc.Parameters.Add(Clone(p)); total = Convert.ToInt32(cc.ExecuteScalar()); }
                int pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)); if (page > pageCount) page = pageCount;

                var rows = new List<object>();
                using (var cmd = new MySqlCommand(
                    "SELECT TRIM(s.regno) regno, TRIM(CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,''))) nm, " +
                    "IFNULL(s.progid,'') prog, s.studCampus, s.entryyear, IFNULL(pay.paid,0) paid, " +
                    "(" + HasLoggedIn + ") loggedIn, IFNULL(TRIM(s.email),'') personal " + from +
                    "ORDER BY IFNULL(pay.paid,0) DESC, s.regno LIMIT @off,@ps", c))
                {
                    cmd.CommandTimeout = 120; foreach (var p in ps) cmd.Parameters.Add(Clone(p));
                    cmd.Parameters.AddWithValue("@off", (page - 1) * pageSize); cmd.Parameters.AddWithValue("@ps", pageSize);
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read())
                        {
                            decimal paid = rd["paid"] == DBNull.Value ? 0 : Convert.ToDecimal(rd["paid"]);
                            bool loggedIn = rd["loggedIn"] != DBNull.Value && Convert.ToInt32(rd["loggedIn"]) == 1;
                            // Both halves of the reason, so the tab answers "why was this student
                            // not generated?" without an admin having to work it out.
                            rows.Add(new { regno = rd["regno"].ToString(), name = rd["nm"].ToString(), programme = rd["prog"].ToString(),
                                campus = CampusName(rd["studCampus"].ToString()), year = rd["entryyear"].ToString(), paid,
                                loggedIn, personal = rd["personal"].ToString(),
                                bucket = (paid >= MinPaid && loggedIn) ? "eligible" : (paid > 0 ? "partial" : "none") });
                        }
                }
                return js.Serialize(new { success = true, total, page, pageCount, rows });
            }
        }
        catch (Exception ex) { return js.Serialize(new { success = false, message = ex.Message }); }
    }

    /// <summary>
    /// Looks up one student by number so the admin can see who they are BEFORE creating a
    /// record for them. Answers for any student in acad_student, whatever their year or
    /// payment, and says what would stop a record being made.
    /// </summary>
    public static string LookupStudent(string regno)
    {
        var js = new JavaScriptSerializer();
        regno = (regno ?? "").Trim();
        if (regno == "") return js.Serialize(new { success = false, message = "Type a student number." });
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT TRIM(s.regno) regno, TRIM(CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,''))) nm, " +
                    "  IFNULL(s.progid,'') prog, IFNULL(pr.progname,'') progname, IFNULL(s.studCampus,'') campus, " +
                    "  IFNULL(s.entryyear,'') yr, IFNULL(TRIM(s.email),'') email, IFNULL(pay.paid,0) paid, " +
                    "  (mm.LastLoginDate > mm.CreationDate) loggedIn, " +
                    "  EXISTS(SELECT 1 FROM campus_dynamics_portal.sems_email_creations e WHERE e.regno=TRIM(s.regno)) inPipeline " +
                    "FROM campus_dynamics.acad_student s " + PaySub +
                    "LEFT JOIN campus_dynamics.acad_programme pr ON pr.progcode = s.progid " +
                    "LEFT JOIN campus_dynamics_portal.my_aspnet_users mu ON mu.name = TRIM(s.regno) " +
                    "LEFT JOIN campus_dynamics_portal.my_aspnet_membership mm ON mm.userId = mu.id " +
                    "WHERE TRIM(s.regno)=@r LIMIT 1", c))
                {
                    cmd.CommandTimeout = 60;
                    cmd.Parameters.AddWithValue("@r", regno);
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read())
                            return js.Serialize(new { success = false, message = "No student with the number " + regno + "." });

                        string email = rd["email"].ToString();
                        bool inPipeline = Convert.ToInt32(rd["inPipeline"]) == 1;
                        bool hasUni = email.ToLowerInvariant().EndsWith("@" + UniversityDomain);
                        decimal paid = rd["paid"] == DBNull.Value ? 0 : Convert.ToDecimal(rd["paid"]);
                        bool loggedIn = rd["loggedIn"] != DBNull.Value && Convert.ToInt32(rd["loggedIn"]) == 1;

                        // A blocker stops the record being made; a caution is something the
                        // automatic rule would have refused on, which an admin may override.
                        string blocker = inPipeline ? "This student already has a record in the pipeline."
                                       : hasUni ? "This student already holds " + email + "." : "";
                        var cautions = new List<string>();
                        int yr; int.TryParse(rd["yr"].ToString(), out yr);
                        if (yr > 0 && yr < MinEntryYear) cautions.Add("Admitted in " + yr + ", before the " + MinEntryYear + " intake.");
                        if (paid < MinPaid) cautions.Add("Has paid " + paid.ToString("N0") + ", under the " + MinPaid.ToString("N0") + " threshold.");
                        if (!loggedIn) cautions.Add("Has never signed in to the portal.");

                        return js.Serialize(new
                        {
                            success = true,
                            regno = rd["regno"].ToString(),
                            name = rd["nm"].ToString(),
                            programme = rd["progname"].ToString() == "" ? rd["prog"].ToString() : rd["progname"].ToString(),
                            campus = CampusName(rd["campus"].ToString()),
                            year = rd["yr"].ToString(),
                            personal = email,
                            paid,
                            loggedIn,
                            inPipeline,
                            blocker,
                            cautions
                        });
                    }
                }
            }
        }
        catch (Exception ex) { return js.Serialize(new { success = false, message = ex.Message }); }
    }

    /// <summary>
    /// Creates a pipeline record for one student from scratch — any year, any payment, signed
    /// in or not. This is the deliberate override for the cases the automatic rule cannot
    /// cover: a continuing student who asks for an address, a late admission, a record that
    /// was removed by mistake.
    ///
    /// The two guards that remain are not policy: a second record for the same student, and a
    /// student who already holds an address on the domain. Neither is an override anyone
    /// wants — they produce duplicates, not favours.
    /// </summary>
    public static string CreateRecord(string regno, string email, string tempPw, string stage, string note)
    {
        var js = new JavaScriptSerializer();
        regno = (regno ?? "").Trim();
        email = (email ?? "").Trim().ToLowerInvariant();
        tempPw = (tempPw ?? "").Trim();
        if (regno == "") return js.Serialize(new { success = false, message = "Student number is required." });

        // An address is optional. With one, the record is created and issued in a single step;
        // without one, it is parked at Pending creation for a batch to pick up later.
        bool issuing = email != "";
        Stage target = StageOf(stage) ?? StageOf(issuing ? "READY_FOR_COLLECTION" : "PENDING_CREATION");

        if (issuing)
        {
            if (!SemsBatch.IsValidEmail(email))
                return js.Serialize(new { success = false, message = "\"" + email + "\" is not a valid address. Letters, digits and dots only, starting with a letter." });
            if (!email.EndsWith("@" + UniversityDomain, StringComparison.OrdinalIgnoreCase))
                return js.Serialize(new { success = false, message = "The address must be on @" + UniversityDomain + "." });
            if (tempPw == "") tempPw = SemsBatch.DefaultPassword;
            if (!SemsBatch.IsUsablePassword(tempPw, email))
                return js.Serialize(new { success = false, message = "The temporary password must be at least " +
                    SemsBatch.MinPasswordLength + " characters and cannot be the address itself — Google rejects both." });
        }
        else if (target.key != "PENDING_CREATION")
        {
            return js.Serialize(new { success = false, message = "Give the student an address before setting the stage to " + target.label + "." });
        }

        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();

                // Asked before the insert so the refusal can name WHO holds the address, which a
                // duplicate-key error cannot.
                if (issuing)
                {
                    using (var q = new MySqlCommand(
                        "SELECT IFNULL(owner_ref,''), source FROM campus_dynamics_portal.sems_email_directory " +
                        "WHERE email=@e AND status<>'RELEASED' LIMIT 1", c))
                    {
                        q.Parameters.AddWithValue("@e", email);
                        using (var rd = q.ExecuteReader())
                            if (rd.Read() && !rd[0].ToString().Equals(regno, StringComparison.OrdinalIgnoreCase))
                                return js.Serialize(new { success = false, message = email + " is already held by " +
                                    rd[1].ToString().ToLowerInvariant() + " record " +
                                    (rd[0].ToString() == "" ? "(reserved)" : rd[0].ToString()) + "." });
                    }
                }

                using (var tx = c.BeginTransaction())
                {
                    int n;
                    using (var cmd = new MySqlCommand(
                        "INSERT INTO campus_dynamics_portal.sems_email_creations " +
                        " (regno, entryno, admission_year, campus, programme, student_name, current_stage, current_status, " +
                        "  creation_date, created_by, paid_amount_snapshot, email_address, temp_password, email_created_at, notes) " +
                        "SELECT TRIM(s.regno), s.entryno, s.entryyear, s.studCampus, s.progid, " +
                        "  NULLIF(TRIM(CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,''))),''), " +
                        "  @stg, @sts, NOW(), @who, IFNULL(pay.paid,0), NULLIF(@em,''), NULLIF(@pw,''), " +
                        "  CASE WHEN @em<>'' THEN NOW() ELSE NULL END, " +
                        "  NULLIF(TRIM(CONCAT('Added by hand by ', @who, CASE WHEN @note<>'' THEN CONCAT(' — ', @note) ELSE '' END)),'') " +
                        "FROM campus_dynamics.acad_student s " + PaySub +
                        "WHERE TRIM(s.regno)=@r " +
                        "  AND " + NoUniversityAddress + " " +
                        "  AND " + NotInPipeline + " LIMIT 1", c, tx))
                    {
                        cmd.CommandTimeout = 60;
                        cmd.Parameters.AddWithValue("@stg", target.key);
                        cmd.Parameters.AddWithValue("@sts", target.status);
                        cmd.Parameters.AddWithValue("@who", Actor());
                        cmd.Parameters.AddWithValue("@r", regno);
                        cmd.Parameters.AddWithValue("@em", issuing ? email : "");
                        cmd.Parameters.AddWithValue("@pw", issuing ? tempPw : "");
                        cmd.Parameters.AddWithValue("@note", note ?? "");
                        n = cmd.ExecuteNonQuery();
                    }
                    if (n == 0)
                    {
                        tx.Rollback();
                        return js.Serialize(new { success = false, message =
                            "Nothing created. Either " + regno + " is not a student, already has a record, or already holds an @" +
                            UniversityDomain + " address." });
                    }
                    int id = (int)0;
                    using (var q = new MySqlCommand("SELECT id FROM campus_dynamics_portal.sems_email_creations WHERE regno=@r LIMIT 1", c, tx))
                    { q.Parameters.AddWithValue("@r", regno); var v = q.ExecuteScalar(); if (v != null && v != DBNull.Value) id = Convert.ToInt32(v); }

                    if (issuing)
                    {
                        // The address is registered the same way a batch registers one, or the
                        // allocator could hand the same name to somebody else tomorrow.
                        SemsBatch.UpsertDirectory(c, tx, email, "PIPELINE", "STUDENT", regno, "", "ACTIVE",
                                                  "issued by hand by " + Actor());
                    }

                    Log(c, tx, id, regno, "create_record", null, target.key,
                        (issuing ? email + " issued by hand" : "record created by hand") +
                        (string.IsNullOrWhiteSpace(note) ? "" : " — " + note.Trim()));

                    if (issuing && target.key == "READY_FOR_COLLECTION")
                        Notify(c, tx, regno, "Your university email address is ready",
                               "Open the portal to collect your @mru.ac.ug address and its password. It takes about five minutes.", "mail");

                    tx.Commit();
                    return js.Serialize(new { success = true, regno, message =
                        issuing ? (regno + " created with " + email + ", at " + target.label + ".")
                                : (regno + " added to the pipeline at " + target.label + ".") });
                }
            }
        }
        catch (MySqlException mex)
        {
            if (mex.Number == 1062) return js.Serialize(new { success = false, message = "That address is already assigned to another student." });
            return js.Serialize(new { success = false, message = mex.Message });
        }
        catch (Exception ex) { return js.Serialize(new { success = false, message = ex.Message }); }
    }

    // Force-add ONE 2026+ student to the pipeline regardless of how much they've paid.
    public static string AddToPipeline(string regno, string note)
    {
        var js = new JavaScriptSerializer(); regno = (regno ?? "").Trim();
        if (regno == "") return js.Serialize(new { success = false, message = "Student number is required." });
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                int n;
                using (var cmd = new MySqlCommand(
                    "INSERT INTO campus_dynamics_portal.sems_email_creations " +
                    " (regno, entryno, admission_year, campus, programme, student_name, current_stage, current_status, creation_date, created_by, paid_amount_snapshot, notes) " +
                    "SELECT TRIM(s.regno), s.entryno, s.entryyear, s.studCampus, s.progid, " +
                    "  NULLIF(TRIM(CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,''))),''), " +
                    "  'PENDING_CREATION','PENDING', NOW(), @who, IFNULL(pay.paid,0), NULLIF(@note,'') " +
                    "FROM campus_dynamics.acad_student s " + PaySub +
                    "WHERE TRIM(s.regno)=@r AND s.entryyear >= " + MinEntryYear + " " +
                    "  AND " + NoUniversityAddress + " " +
                    "  AND " + NotInPipeline + " LIMIT 1", c))
                {
                    cmd.Parameters.AddWithValue("@who", Actor());
                    cmd.Parameters.AddWithValue("@r", regno);
                    cmd.Parameters.AddWithValue("@note", note ?? "");
                    n = cmd.ExecuteNonQuery();
                }
                if (n == 0) return js.Serialize(new { success = false, message =
                    "Not added: the student is already in the pipeline, already holds an @" + UniversityDomain +
                    " address, or was admitted before " + MinEntryYear + "." });
                Log(c, null, 0, regno, "add_to_pipeline", null, "PENDING_CREATION", "manually added by admin");
                return js.Serialize(new { success = true, message = "Student added to the pipeline." });
            }
        }
        catch (Exception ex) { return js.Serialize(new { success = false, message = ex.Message }); }
    }

    // 360: manually move a student to any lifecycle stage.
    public static string SetStatus(string regno, string stage, string note)
    {
        var js = new JavaScriptSerializer();
        regno = (regno ?? "").Trim();
        Stage target = StageOf(stage);
        if (regno == "") return js.Serialize(new { success = false, message = "Student number is required." });
        if (target == null) return js.Serialize(new { success = false, message = "\"" + stage + "\" is not a stage." });
        string status = target.status;
        stage = target.key;
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                int id = 0; string from = "";
                using (var q = new MySqlCommand("SELECT id, current_stage FROM campus_dynamics_portal.sems_email_creations WHERE regno=@r LIMIT 1", c))
                { q.Parameters.AddWithValue("@r", regno); using (var rd = q.ExecuteReader()) if (rd.Read()) { id = Convert.ToInt32(rd[0]); from = rd[1].ToString(); } }
                if (id == 0) return js.Serialize(new { success = false, message = "No pipeline record for that student." });
                using (var up = new MySqlCommand(
                    "UPDATE campus_dynamics_portal.sems_email_creations SET current_stage=@st, current_status=@ss, " +
                    "notes=CONCAT(COALESCE(NULLIF(notes,''),''), CASE WHEN @n<>'' THEN CONCAT(' | ', @n) ELSE '' END), " +
                    "last_updated_by=@who, last_updated_at=NOW() WHERE id=@id", c))
                {
                    up.Parameters.AddWithValue("@st", stage); up.Parameters.AddWithValue("@ss", status);
                    up.Parameters.AddWithValue("@n", note ?? ""); up.Parameters.AddWithValue("@who", Actor()); up.Parameters.AddWithValue("@id", id);
                    up.ExecuteNonQuery();
                }
                Log(c, null, id, regno, "set_status", from, stage, note);
                return js.Serialize(new { success = true, message = "Stage changed to " + target.label + "." });
            }
        }
        catch (Exception ex) { return js.Serialize(new { success = false, message = ex.Message }); }
    }

    // 360: reset the temporary password (and notify the student).
    public static string SetPassword(string regno, string tempPw)
    {
        var js = new JavaScriptSerializer(); regno = (regno ?? "").Trim();
        tempPw = (tempPw ?? "").Trim();
        if (regno == "" || tempPw == "") return js.Serialize(new { success = false, message = "Student and a new temporary password are required." });
        if (!SemsBatch.IsUsablePassword(tempPw, null))
            return js.Serialize(new { success = false, message = "A temporary password must be at least " + SemsBatch.MinPasswordLength +
                                                                " characters — Google rejects anything shorter." });
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                int n;
                using (var up = new MySqlCommand("UPDATE campus_dynamics_portal.sems_email_creations SET temp_password=@p, password_changed='No', last_updated_by=@who, last_updated_at=NOW() WHERE regno=@r", c))
                { up.Parameters.AddWithValue("@p", tempPw.Trim()); up.Parameters.AddWithValue("@who", Actor()); up.Parameters.AddWithValue("@r", regno); n = up.ExecuteNonQuery(); }
                if (n == 0) return js.Serialize(new { success = false, message = "No pipeline record for that student." });
                Log(c, null, 0, regno, "reset_password", null, null, "temporary password reset by admin");
                Notify(c, null, regno, "Your email password was reset", "Please open the portal, view your new temporary password and set your own.", "key");
                return js.Serialize(new { success = true, message = "Temporary password reset." });
            }
        }
        catch (Exception ex) { return js.Serialize(new { success = false, message = ex.Message }); }
    }

    /// <summary>
    /// Changes the address on a record that already has one.
    ///
    /// CreateEmail issues a first address; this replaces a live one, which is a different job
    /// because something is already holding the old name. Two tables have to agree:
    /// sems_email_creations carries the address under a UNIQUE index, and sems_email_directory
    /// is the oracle every other part of SEMS asks before handing a name out. Changing one and
    /// not the other either leaks a name that is still in use or strands one that nobody holds
    /// — so both move inside a single transaction, or neither does.
    ///
    /// The old address is released rather than deleted-and-forgotten: it goes back to the pool
    /// and can be issued again, which is what an administrator correcting a typo expects.
    /// </summary>
    public static string SetEmail(string regno, string email, string note)
    {
        var js = new JavaScriptSerializer();
        regno = (regno ?? "").Trim();
        // Normalised exactly as CreateEmail and the directory store it, so case and stray
        // spaces can never produce two records for one mailbox.
        email = (email ?? "").Trim().ToLowerInvariant();
        note = (note ?? "").Trim();

        if (regno == "" || email == "")
            return js.Serialize(new { success = false, message = "Student and the new address are both required." });
        if (!SemsBatch.IsValidEmail(email))
            return js.Serialize(new { success = false, message = "\"" + email + "\" is not a valid address. Use letters, digits and dots only, starting with a letter." });

        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();

                int id = 0; string oldEmail = "", stage = "", name = "";
                using (var q = new MySqlCommand(
                    "SELECT id, IFNULL(email_address,''), current_stage, IFNULL(student_name,'') " +
                    "FROM campus_dynamics_portal.sems_email_creations WHERE regno=@r LIMIT 1", c))
                {
                    q.Parameters.AddWithValue("@r", regno);
                    using (var rd = q.ExecuteReader())
                        if (rd.Read()) { id = Convert.ToInt32(rd[0]); oldEmail = rd[1].ToString(); stage = rd[2].ToString(); name = rd[3].ToString(); }
                }
                if (id == 0) return js.Serialize(new { success = false, message = "No pipeline record for that student." });
                if (string.Equals(oldEmail, email, StringComparison.OrdinalIgnoreCase))
                    return js.Serialize(new { success = false, message = "That is already this student\u2019s address \u2014 nothing to change." });

                string heldElsewhere = "";   // set when the OLD name is a real Workspace mailbox

                // Asked before the write so the refusal can name WHO holds it, which a
                // duplicate-key error cannot.
                using (var q = new MySqlCommand(
                    "SELECT IFNULL(owner_ref,''), source FROM campus_dynamics_portal.sems_email_directory " +
                    "WHERE email=@e AND status<>'RELEASED' LIMIT 1", c))
                {
                    q.Parameters.AddWithValue("@e", email);
                    using (var rd = q.ExecuteReader())
                        if (rd.Read() && !rd[0].ToString().Equals(regno, StringComparison.OrdinalIgnoreCase))
                            return js.Serialize(new { success = false, message = email + " is already held by " +
                                rd[1].ToString().ToLowerInvariant() + " record " +
                                (rd[0].ToString() == "" ? "(reserved)" : rd[0].ToString()) + "." });
                }

                using (var tx = c.BeginTransaction())
                {
                    try
                    {
                        using (var up = new MySqlCommand(
                            "UPDATE campus_dynamics_portal.sems_email_creations SET email_address=@e, " +
                            "last_updated_by=@who, last_updated_at=NOW() WHERE id=@id", c, tx))
                        {
                            up.Parameters.AddWithValue("@e", email);
                            up.Parameters.AddWithValue("@who", Actor());
                            up.Parameters.AddWithValue("@id", id);
                            up.ExecuteNonQuery();
                        }

                        // Releasing the old name is only safe for names this system reserved.
                        // A GOOGLE-sourced row means the mailbox really exists in Workspace, and
                        // deleting the row would tell SEMS the name is free while Google still
                        // holds it - 831 of the 1,150 live addresses are in exactly that state.
                        // Those rows stay, and the caller is told the rename must be done in
                        // Google too.
                        if (oldEmail != "")
                        {
                            using (var d = new MySqlCommand(
                                "DELETE FROM campus_dynamics_portal.sems_email_directory " +
                                "WHERE email=@e AND owner_ref=@r AND source IN ('PIPELINE','RESERVED')", c, tx))
                            { d.Parameters.AddWithValue("@e", oldEmail.ToLowerInvariant()); d.Parameters.AddWithValue("@r", regno); d.ExecuteNonQuery(); }

                            using (var q2 = new MySqlCommand(
                                "SELECT source FROM campus_dynamics_portal.sems_email_directory WHERE email=@e LIMIT 1", c, tx))
                            {
                                q2.Parameters.AddWithValue("@e", oldEmail.ToLowerInvariant());
                                var o2 = q2.ExecuteScalar();
                                if (o2 != null && o2 != DBNull.Value) heldElsewhere = o2.ToString();
                            }
                        }

                        // The new one is claimed, in the same shape the batch reserver uses.
                        int at = email.IndexOf('@');
                        string local = at > 0 ? email.Substring(0, at) : email;
                        string domain = at > 0 ? email.Substring(at + 1) : "";
                        using (var ins = new MySqlCommand(
                            "INSERT INTO campus_dynamics_portal.sems_email_directory " +
                            "(email,local_part,domain,source,owner_type,owner_ref,display_name,status,first_seen_at,last_seen_at,notes) " +
                            "VALUES (@e,@l,@d,'PIPELINE','STUDENT',@o,@n,'ACTIVE',NOW(),NOW(),@nt) " +
                            "ON DUPLICATE KEY UPDATE owner_ref=@o, owner_type='STUDENT', " +
                            "status='ACTIVE', display_name=@n, last_seen_at=NOW(), notes=@nt", c, tx))
                        {
                            ins.Parameters.AddWithValue("@e", email);
                            ins.Parameters.AddWithValue("@l", local);
                            ins.Parameters.AddWithValue("@d", domain);
                            ins.Parameters.AddWithValue("@o", regno);
                            ins.Parameters.AddWithValue("@n", name.Length > 150 ? name.Substring(0, 150) : name);
                            ins.Parameters.AddWithValue("@nt", "changed by admin" + (note == "" ? "" : ": " + (note.Length > 180 ? note.Substring(0, 180) : note)));
                            ins.ExecuteNonQuery();
                        }

                        Log(c, tx, id, regno, "change_email", stage, stage,
                            (oldEmail == "" ? "(none)" : oldEmail) + " \u2192 " + email + (note == "" ? "" : " | " + note));
                        Notify(c, tx, regno, "Your university email address has changed",
                               "Your address is now " + email + ". Your password is unchanged. Open the portal if you need to see your details again.", "mail");
                        tx.Commit();
                    }
                    catch { try { tx.Rollback(); } catch { } throw; }
                }

                string tail;
                if (oldEmail == "") tail = ".";
                else if (heldElsewhere != "")
                    tail = ". " + oldEmail + " is still a real " + heldElsewhere.ToLowerInvariant() +
                           " mailbox - rename or delete it in Google Workspace too, or the student keeps two.";
                else tail = ". " + oldEmail + " is free again.";
                return js.Serialize(new { success = true, message = "Address changed to " + email + tail,
                                          warnGoogle = heldElsewhere != "" });
            }
        }
        catch (MySqlException mex)
        {
            if (mex.Number == 1062)
                return js.Serialize(new { success = false, message = "That address is already assigned to another student." });
            return js.Serialize(new { success = false, message = mex.Message });
        }
        catch (Exception ex) { return js.Serialize(new { success = false, message = ex.Message }); }
    }

    /// <summary>Replaces the free-text notes on a record. Empty clears them.</summary>
    public static string SetNotes(string regno, string notes)
    {
        var js = new JavaScriptSerializer(); regno = (regno ?? "").Trim();
        notes = (notes ?? "").Trim();
        if (regno == "") return js.Serialize(new { success = false, message = "Student is required." });
        if (notes.Length > 1000) notes = notes.Substring(0, 1000);
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                int n;
                using (var up = new MySqlCommand(
                    "UPDATE campus_dynamics_portal.sems_email_creations SET notes=@n, last_updated_by=@who, " +
                    "last_updated_at=NOW() WHERE regno=@r", c))
                {
                    up.Parameters.AddWithValue("@n", notes);
                    up.Parameters.AddWithValue("@who", Actor());
                    up.Parameters.AddWithValue("@r", regno);
                    n = up.ExecuteNonQuery();
                }
                if (n == 0) return js.Serialize(new { success = false, message = "No pipeline record for that student." });
                Log(c, null, 0, regno, "edit_notes", null, null, notes == "" ? "notes cleared" : notes);
                return js.Serialize(new { success = true, message = "Notes saved." });
            }
        }
        catch (Exception ex) { return js.Serialize(new { success = false, message = ex.Message }); }
    }

    // 360: remove a record from the pipeline entirely.
    public static string DeleteRecord(string regno, string note)
    {
        var js = new JavaScriptSerializer(); regno = (regno ?? "").Trim();
        if (regno == "") return js.Serialize(new { success = false, message = "Student number is required." });
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();

                // Read the address before the row goes, so it can be handed back and so the log
                // still says what was surrendered.
                string email = "";
                using (var q = new MySqlCommand(
                    "SELECT IFNULL(email_address,'') FROM campus_dynamics_portal.sems_email_creations WHERE regno=@r LIMIT 1", c))
                { q.Parameters.AddWithValue("@r", regno); var v = q.ExecuteScalar(); email = v == null || v == DBNull.Value ? "" : v.ToString().Trim(); }

                Log(c, null, 0, regno, "delete_record", null, null,
                    (email == "" ? "" : email + " released — ") + (note ?? ""));   // log BEFORE the row is gone

                int n;
                using (var d = new MySqlCommand("DELETE FROM campus_dynamics_portal.sems_email_creations WHERE regno=@r", c))
                { d.Parameters.AddWithValue("@r", regno); n = d.ExecuteNonQuery(); }
                if (n == 0) return js.Serialize(new { success = false, message = "No pipeline record for that student." });

                // The address goes back to the pool. Without this, removing a record left its
                // name reserved to a student who no longer has a record — unissuable to anyone,
                // including the same student if they were added again.
                if (email != "")
                {
                    using (var d = new MySqlCommand(
                        "DELETE FROM campus_dynamics_portal.sems_email_directory " +
                        "WHERE email=@e AND owner_ref=@r AND source='PIPELINE'", c))
                    { d.Parameters.AddWithValue("@e", email.ToLowerInvariant()); d.Parameters.AddWithValue("@r", regno); d.ExecuteNonQuery(); }
                }

                return js.Serialize(new { success = true, message =
                    "Removed from the pipeline." + (email == "" ? "" : " " + email + " is free again.") });
            }
        }
        catch (Exception ex) { return js.Serialize(new { success = false, message = ex.Message }); }
    }

    // 360: full detail for one student — record, per-stage timestamps, activity log, complaints.
    public static string Detail(string regno)
    {
        var js = new JavaScriptSerializer(); regno = (regno ?? "").Trim();
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                object rec = null;
                using (var cmd = new MySqlCommand(
                    "SELECT id, regno, student_name, programme, campus, admission_year, IFNULL(entryno,'') entryno, " +
                    "IFNULL(email_address,'') email, IFNULL(temp_password,'') pw, current_stage, current_status, verification_status, " +
                    "IFNULL(password_changed,'No') pwc, IFNULL(paid_amount_snapshot,0) paid, IFNULL(notes,'') notes, " +
                    "DATE_FORMAT(creation_date,'%Y-%m-%d %H:%i') t_created, DATE_FORMAT(email_created_at,'%Y-%m-%d %H:%i') t_email, " +
                    "DATE_FORMAT(education_done_at,'%Y-%m-%d %H:%i') t_edu, DATE_FORMAT(gmail_guide_done_at,'%Y-%m-%d %H:%i') t_gmail, " +
                    "DATE_FORMAT(quiz_passed_at,'%Y-%m-%d %H:%i') t_quiz, DATE_FORMAT(credentials_viewed_at,'%Y-%m-%d %H:%i') t_viewed, " +
                    "DATE_FORMAT(verified_at,'%Y-%m-%d %H:%i') t_verified, DATE_FORMAT(completed_at,'%Y-%m-%d %H:%i') t_completed " +
                    "FROM campus_dynamics_portal.sems_email_creations WHERE regno=@r LIMIT 1", c))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    using (var rd = cmd.ExecuteReader())
                        if (rd.Read())
                            rec = new
                            {
                                id = Convert.ToInt32(rd["id"]), regno = rd["regno"].ToString(), name = rd["student_name"].ToString(),
                                programme = rd["programme"].ToString(), campus = CampusName(rd["campus"].ToString()), year = rd["admission_year"].ToString(),
                                entryno = rd["entryno"].ToString(), email = rd["email"].ToString(), pw = rd["pw"].ToString(),
                                stage = rd["current_stage"].ToString(), status = rd["current_status"].ToString(), verification = rd["verification_status"].ToString(),
                                pwChanged = rd["pwc"].ToString(), paid = Convert.ToDecimal(rd["paid"]), notes = rd["notes"].ToString(),
                                t_created = S(rd["t_created"]), t_email = S(rd["t_email"]), t_edu = S(rd["t_edu"]), t_gmail = S(rd["t_gmail"]),
                                t_quiz = S(rd["t_quiz"]), t_viewed = S(rd["t_viewed"]), t_verified = S(rd["t_verified"]), t_completed = S(rd["t_completed"])
                            };
                }
                if (rec == null) return js.Serialize(new { success = false, message = "No pipeline record for that student." });

                var acts = new List<object>();
                using (var cmd = new MySqlCommand("SELECT action, IFNULL(stage_from,'') sf, IFNULL(stage_to,'') st, IFNULL(actor,'') ac, IFNULL(detail,'') dt, DATE_FORMAT(created_at,'%Y-%m-%d %H:%i') ct FROM campus_dynamics_portal.sems_activity_log WHERE regno=@r ORDER BY id DESC LIMIT 40", c))
                { cmd.Parameters.AddWithValue("@r", regno); using (var rd = cmd.ExecuteReader()) while (rd.Read()) acts.Add(new { action = rd["action"].ToString(), from = rd["sf"].ToString(), to = rd["st"].ToString(), actor = rd["ac"].ToString(), detail = rd["dt"].ToString(), at = rd["ct"].ToString() }); }

                var comps = new List<object>();
                using (var cmd = new MySqlCommand("SELECT category, status, IFNULL(admin_response,'') resp, DATE_FORMAT(created_at,'%Y-%m-%d %H:%i') ct FROM campus_dynamics_portal.sems_complaints WHERE regno=@r ORDER BY id DESC LIMIT 10", c))
                { cmd.Parameters.AddWithValue("@r", regno); using (var rd = cmd.ExecuteReader()) while (rd.Read()) comps.Add(new { category = rd["category"].ToString(), status = rd["status"].ToString(), response = rd["resp"].ToString(), at = rd["ct"].ToString() }); }

                return js.Serialize(new { success = true, record = rec, activity = acts, complaints = comps });
            }
        }
        catch (Exception ex) { return js.Serialize(new { success = false, message = ex.Message }); }
    }

    // ── helpers ──────────────────────────────────────────────────────
    private static string S(object v) { return v == null || v == DBNull.Value ? "" : v.ToString(); }
    private static int Scalar(MySqlConnection c, string sql)
    { using (var cmd = new MySqlCommand(sql, c)) { cmd.CommandTimeout = 60; var v = cmd.ExecuteScalar(); return v == null || v == DBNull.Value ? 0 : Convert.ToInt32(v); } }
    private static MySqlParameter Clone(MySqlParameter p) { return new MySqlParameter(p.ParameterName, p.Value); }
    private static string CampusName(string c)
    { c = (c ?? "").Trim(); return c == "1" ? "Kakeeka" : c == "2" ? "Kirumba" : (c == "" ? "-" : c); }
}
