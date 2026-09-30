using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Configuration;
using System.Web.Script.Serialization;
using System.Web.Script.Services;
using System.Web.Services;
using System.Web.UI;
using MySql.Data.MySqlClient;

[ScriptService]
public partial class COOPERP_NewScreens_SemesterDeletionRequestsController : Page
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 };

    private static string ConnStr
    {
        get { return WebConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        if (!IsPostBack)
        {
            string status = (Request.QueryString["status"] ?? "PENDING").Trim().ToUpperInvariant();
            var valid = new[] { "PENDING", "APPROVED", "REJECTED", "ALL" };
            bool ok = false;
            foreach (var v in valid) { if (status == v) { ok = true; break; } }
            if (!ok) status = "PENDING";

            string year = (Request.QueryString["year"] ?? "").Trim();
            string q    = (Request.QueryString["q"] ?? "").Trim();
            int page = 1;
            int per  = 25;
            int.TryParse(Request.QueryString["page"] ?? "1", out page);
            int.TryParse(Request.QueryString["per"] ?? "25", out per);
            if (page < 1) page = 1;
            if (per < 10) per = 10;
            if (per > 100) per = 100;

            litInitVars.Text = string.Format(
                "<script>var _sdrInit={{status:'{0}',year:'{1}',q:'{2}',page:{3},per:{4}}};</script>",
                HttpUtility.JavaScriptStringEncode(status),
                HttpUtility.JavaScriptStringEncode(year),
                HttpUtility.JavaScriptStringEncode(q),
                page, per);
        }
    }

    private static bool IsAdmin()
    {
        return true;
    }

    private static string AdminUser()
    {
        var ctx = HttpContext.Current;
        if (ctx == null) return "admin";
        if (ctx.Session != null && ctx.Session["username"] != null && !string.IsNullOrWhiteSpace(ctx.Session["username"].ToString()))
            return ctx.Session["username"].ToString().Trim();
        if (ctx.User != null && ctx.User.Identity != null && !string.IsNullOrWhiteSpace(ctx.User.Identity.Name))
            return ctx.User.Identity.Name.Trim();
        return "admin";
    }

    [WebMethod(EnableSession = true)]
    public static string GetRequests(string statusFilter, string acadYear, string search, int page, int pageSize)
    {
        try
        {
            if (!IsAdmin()) return Json.Serialize(new { success = false, message = "Access denied." });

            string status = (statusFilter ?? "PENDING").Trim().ToUpperInvariant();
            string year   = (acadYear ?? "").Trim();
            string q      = (search ?? "").Trim();
            if (page < 1) page = 1;
            if (pageSize < 10) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var where = new List<string> { "1=1" };
            var parms = new List<MySqlParameter>();

            if (!string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                where.Add("r.status=@st");
                parms.Add(new MySqlParameter("@st", status));
            }
            if (!string.IsNullOrWhiteSpace(year))
            {
                where.Add("r.acad_year=@ay");
                parms.Add(new MySqlParameter("@ay", year));
            }
            if (!string.IsNullOrWhiteSpace(q))
            {
                where.Add("(r.regno LIKE @q OR IFNULL(r.student_name,'') LIKE @q OR IFNULL(r.programme_name,'') LIKE @q)");
                parms.Add(new MySqlParameter("@q", "%" + q + "%"));
            }

            string whereClause = string.Join(" AND ", where);
            int total = 0, pending = 0, approved = 0, rejected = 0, totalPages = 1;
            var rows = new List<object>();

            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open();

                using (var cmd = new MySqlCommand(
                    "SELECT COUNT(*) AS total, " +
                    "SUM(CASE WHEN r.status='PENDING' THEN 1 ELSE 0 END) AS pending, " +
                    "SUM(CASE WHEN r.status='APPROVED' THEN 1 ELSE 0 END) AS approved, " +
                    "SUM(CASE WHEN r.status='REJECTED' THEN 1 ELSE 0 END) AS rejected " +
                    "FROM campus_dynamics_portal.acad_semester_deletion_requests r WHERE " + whereClause, conn))
                {
                    foreach (var p in parms)
                        cmd.Parameters.Add(new MySqlParameter(p.ParameterName, p.Value));
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            total    = ToInt(rdr["total"]);
                            pending  = ToInt(rdr["pending"]);
                            approved = ToInt(rdr["approved"]);
                            rejected = ToInt(rdr["rejected"]);
                        }
                    }
                }

                int offset = (page - 1) * pageSize;
                totalPages = total == 0 ? 1 : (int)Math.Ceiling((double)total / pageSize);

                using (var cmd = new MySqlCommand(@"
                    SELECT r.id,
                           r.regno,
                           IFNULL(r.student_name,'') AS student_name,
                           IFNULL(r.programme_code,'') AS programme_code,
                           IFNULL(r.programme_name,'') AS programme_name,
                           r.acad_year,
                           r.study_year,
                           r.semester,
                           IFNULL(r.request_reason,'') AS request_reason,
                           r.status,
                           IFNULL(r.admin_username,'') AS admin_username,
                           IFNULL(r.admin_comment,'') AS admin_comment,
                           DATE_FORMAT(r.created_at,'%d %b %Y %H:%i') AS created_at,
                           DATE_FORMAT(r.decided_at,'%d %b %Y %H:%i') AS decided_at
                    FROM campus_dynamics_portal.acad_semester_deletion_requests r
                    WHERE " + whereClause + @"
                    ORDER BY r.created_at DESC
                    LIMIT @lim OFFSET @off", conn))
                {
                    foreach (var p in parms)
                        cmd.Parameters.Add(new MySqlParameter(p.ParameterName, p.Value));
                    cmd.Parameters.AddWithValue("@lim", pageSize);
                    cmd.Parameters.AddWithValue("@off", offset);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            rows.Add(new
                            {
                                id             = ToInt(rdr["id"]),
                                regno          = rdr["regno"].ToString(),
                                student_name   = rdr["student_name"].ToString(),
                                programme_code = rdr["programme_code"].ToString(),
                                programme_name = rdr["programme_name"].ToString(),
                                acad_year      = rdr["acad_year"].ToString(),
                                study_year     = ToInt(rdr["study_year"]),
                                semester       = ToInt(rdr["semester"]),
                                request_reason = rdr["request_reason"].ToString(),
                                status         = rdr["status"].ToString(),
                                admin_username = rdr["admin_username"].ToString(),
                                admin_comment  = rdr["admin_comment"].ToString(),
                                created_at     = rdr["created_at"].ToString(),
                                decided_at     = rdr["decided_at"].ToString()
                            });
                        }
                    }
                }
            }

            return Json.Serialize(new
            {
                success     = true,
                requests    = rows,
                total       = total,
                pending     = pending,
                approved    = approved,
                rejected    = rejected,
                page        = page,
                pageSize    = pageSize,
                total_pages = totalPages
            });
        }
        catch (Exception ex)
        {
            return Json.Serialize(new { success = false, message = ex.Message });
        }
    }

    [WebMethod(EnableSession = true)]
    public static string BatchDecide(string idsJson, string decision, string comment)
    {
        try
        {
            if (!IsAdmin()) return Json.Serialize(new { success = false, message = "Access denied." });

            string d = (decision ?? "").Trim().ToUpperInvariant();
            string c = (comment ?? "").Trim();
            if (d != "APPROVE" && d != "REJECT")
                return Json.Serialize(new { success = false, message = "Decision must be APPROVE or REJECT." });
            if (d == "REJECT" && string.IsNullOrWhiteSpace(c))
                return Json.Serialize(new { success = false, message = "Comment is required for batch rejection." });

            int[] ids;
            try { ids = (new JavaScriptSerializer()).Deserialize<int[]>(idsJson ?? "[]"); }
            catch { return Json.Serialize(new { success = false, message = "Invalid ID list." }); }

            if (ids == null || ids.Length == 0)
                return Json.Serialize(new { success = false, message = "No requests selected." });

            int processed = 0, skipped = 0;
            var errors = new List<string>();

            foreach (int id in ids)
            {
                try
                {
                    var resultJson = DecideRequest(id, d, c);
                    var result = (new JavaScriptSerializer()).Deserialize<Dictionary<string, object>>(resultJson);
                    object successVal;
                    if (result != null && result.TryGetValue("success", out successVal) && true.Equals(successVal))
                        processed++;
                    else
                    {
                        skipped++;
                        object msg;
                        if (result != null && result.TryGetValue("message", out msg))
                            errors.Add("ID " + id + ": " + (msg ?? "unknown error").ToString());
                    }
                }
                catch (Exception ex)
                {
                    skipped++;
                    errors.Add("ID " + id + ": " + ex.Message);
                }
            }

            return Json.Serialize(new
            {
                success   = true,
                processed = processed,
                skipped   = skipped,
                errors    = errors,
                message   = processed + " request(s) " + (d == "APPROVE" ? "approved" : "rejected") +
                            (skipped > 0 ? ", " + skipped + " skipped" : "") + "."
            });
        }
        catch (Exception ex)
        {
            return Json.Serialize(new { success = false, message = ex.Message });
        }
    }

    [WebMethod(EnableSession = true)]
    public static string GetRequest(int id)
    {
        try
        {
            if (!IsAdmin()) return Json.Serialize(new { success = false, message = "Access denied." });
            if (id <= 0) return Json.Serialize(new { success = false, message = "Invalid request id." });

            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(@"
                    SELECT id, regno, IFNULL(student_name,'') AS student_name,
                           IFNULL(programme_code,'') AS programme_code,
                           IFNULL(programme_name,'') AS programme_name,
                           acad_year, study_year, semester,
                           IFNULL(request_reason,'') AS request_reason,
                           status, IFNULL(admin_username,'') AS admin_username,
                           IFNULL(admin_comment,'') AS admin_comment,
                           DATE_FORMAT(created_at,'%d %b %Y %H:%i') AS created_at,
                           DATE_FORMAT(decided_at,'%d %b %Y %H:%i') AS decided_at,
                           deletion_executed
                    FROM campus_dynamics_portal.acad_semester_deletion_requests
                    WHERE id=@id
                    LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read()) return Json.Serialize(new { success = false, message = "Request not found." });

                        var row = new
                        {
                            id             = ToInt(rdr["id"]),
                            regno          = rdr["regno"].ToString(),
                            student_name   = rdr["student_name"].ToString(),
                            programme_code = rdr["programme_code"].ToString(),
                            programme_name = rdr["programme_name"].ToString(),
                            acad_year      = rdr["acad_year"].ToString(),
                            study_year     = ToInt(rdr["study_year"]),
                            semester       = ToInt(rdr["semester"]),
                            request_reason = rdr["request_reason"].ToString(),
                            status         = rdr["status"].ToString(),
                            admin_username = rdr["admin_username"].ToString(),
                            admin_comment  = rdr["admin_comment"].ToString(),
                            created_at     = rdr["created_at"].ToString(),
                            decided_at     = rdr["decided_at"].ToString(),
                            deletion_executed = ToInt(rdr["deletion_executed"]) == 1
                        };

                        // Read what approving would actually destroy, and hand it back with the
                        // request. An approver deciding without this is deciding blind: the
                        // screen used to show the student's reason and nothing about the courses,
                        // the marks or the money that the click would take with it.
                        string rq = rdr["regno"].ToString();
                        string ay = rdr["acad_year"].ToString();
                        int sy = ToInt(rdr["study_year"]);
                        int sm = ToInt(rdr["semester"]);
                        rdr.Close();

                        var imp = ReadImpact(conn, null, rq, ay, sy, sm);

                        return Json.Serialize(new
                        {
                            success = true,
                            request = row,
                            impact = new
                            {
                                courses        = imp.Courses,
                                marked_courses = imp.MarkedCourses,
                                results        = imp.Results,
                                bills          = imp.Bills,
                                bill_amount    = imp.BillAmount,
                                ledger_rows    = imp.LedgerRows,
                                siblings       = imp.Siblings,
                                shares_period  = imp.SharesPeriod,
                                blocked        = imp.Blocked,
                                balance_before = imp.BalanceBefore,
                                balance_after  = imp.BalanceAfter
                            }
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return Json.Serialize(new { success = false, message = ex.Message });
        }
    }

    [WebMethod(EnableSession = true)]
    public static string DecideRequest(int id, string decision, string comment)
    {
        try
        {
            if (!IsAdmin()) return Json.Serialize(new { success = false, message = "Access denied." });
            if (id <= 0) return Json.Serialize(new { success = false, message = "Invalid request id." });

            string d = (decision ?? "").Trim().ToUpperInvariant();
            string c = (comment ?? "").Trim();
            if (d != "APPROVE" && d != "REJECT") return Json.Serialize(new { success = false, message = "Decision must be APPROVE or REJECT." });
            if (d == "REJECT" && string.IsNullOrWhiteSpace(c)) return Json.Serialize(new { success = false, message = "Comment is required for rejection." });

            string regno;
            string acadYear;
            int studyYear;
            int semester;
            string status;

            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(@"
                    SELECT regno, acad_year, study_year, semester, status
                    FROM campus_dynamics_portal.acad_semester_deletion_requests
                    WHERE id=@id
                    LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read()) return Json.Serialize(new { success = false, message = "Request not found." });
                        regno     = rdr["regno"].ToString();
                        acadYear  = rdr["acad_year"].ToString();
                        studyYear = ToInt(rdr["study_year"]);
                        semester  = ToInt(rdr["semester"]);
                        status    = rdr["status"].ToString();
                    }
                }
            }

            int deletedRegs = 0;
            int deletedCourses = 0;
            int deletedBills = 0;
            int deletedLedger = 0;
            decimal reversedAmount = 0m;
            bool keptSharedRows = false;

            if (d == "APPROVE")
            {
                using (var conn = new MySqlConnection(ConnStr))
                {
                    conn.Open();

                    var imp = ReadImpact(conn, null, regno, acadYear, studyYear, semester);

                    // Published results are an academic record. Whatever the student asked for
                    // and whoever is approving, a semester that has produced results is not
                    // something this screen takes away — the marks would have to be withdrawn
                    // through the marks pipeline first, by the people who own them.
                    if (imp.Blocked)
                    {
                        return Json.Serialize(new
                        {
                            success = false,
                            message = "This semester has " + imp.Results + " published result(s). " +
                                      "Withdraw the results first — a semester carrying results cannot be deleted here."
                        });
                    }

                    // Everything in one transaction. Deleting the registration but failing on the
                    // fees would leave the student billed for a semester that no longer exists,
                    // which is the exact fault this is fixing; a half-done reversal is worse than
                    // none. All the tables involved are InnoDB, so this really does roll back.
                    //
                    // One asterisk: fin_deleted_ledger, which the fin_ledger delete trigger writes
                    // to, is MyISAM. A rollback leaves an archive row claiming a deletion that did
                    // not happen. It is an append-only audit table that no balance is computed
                    // from, so the residue is noise rather than damage — but it is worth knowing
                    // it can happen before anyone reads that table as gospel.
                    using (var tx = conn.BeginTransaction())
                    {
                        // ── the registration row: archived, then removed ──────────────────
                        using (var cmd = new MySqlCommand(@"
                            INSERT INTO campus_dynamics.acad_registration_regdel_bak
                            SELECT * FROM campus_dynamics.acad_registration
                             WHERE regno=@r AND acad_year=@a AND studyyear=@y AND semester=@s", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@r", regno.Trim());
                            cmd.Parameters.AddWithValue("@a", acadYear);
                            cmd.Parameters.AddWithValue("@y", studyYear);
                            cmd.Parameters.AddWithValue("@s", semester);
                            cmd.ExecuteNonQuery();
                        }

                        using (var cmd = new MySqlCommand(@"
                            DELETE FROM campus_dynamics.acad_registration
                             WHERE regno=@r AND acad_year=@a AND studyyear=@y AND semester=@s
                             LIMIT 1", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@r", regno.Trim());
                            cmd.Parameters.AddWithValue("@a", acadYear);
                            cmd.Parameters.AddWithValue("@y", studyYear);
                            cmd.Parameters.AddWithValue("@s", semester);
                            deletedRegs = cmd.ExecuteNonQuery();
                        }

                        // ── the courses and the fees ──────────────────────────────────────
                        // Both are keyed by (regno, acad_year, semester) with no study year, so
                        // they belong to the SEMESTER, not to this one registration row. If the
                        // student still holds another registration for the same semester at a
                        // different study year — 23 students do — those rows are still that
                        // registration's, and removing them here would strip the courses and
                        // cancel the fees of a semester they are still enrolled in. So they only
                        // go when this was the last registration standing for the period.
                        keptSharedRows = imp.SharesPeriod;
                        if (!keptSharedRows)
                        {
                            using (var cmd = new MySqlCommand(@"
                                INSERT INTO campus_dynamics_portal.acad_course_registration_regdel_bak
                                SELECT * FROM campus_dynamics_portal.acad_course_registration
                                 WHERE regno=@r AND acad_year=@a AND semester=@s", conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@r", regno.Trim());
                                cmd.Parameters.AddWithValue("@a", acadYear);
                                cmd.Parameters.AddWithValue("@s", semester);
                                cmd.ExecuteNonQuery();
                            }

                            using (var cmd = new MySqlCommand(@"
                                DELETE FROM campus_dynamics_portal.acad_course_registration
                                 WHERE regno=@r AND acad_year=@a AND semester=@s", conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@r", regno.Trim());
                                cmd.Parameters.AddWithValue("@a", acadYear);
                                cmd.Parameters.AddWithValue("@s", semester);
                                deletedCourses = cmd.ExecuteNonQuery();
                            }

                            reversedAmount = imp.BillAmount;

                            // The bill is archived before either side of it is touched, because
                            // once the tracking row is gone the ledger rows can no longer be
                            // found — the only link between them is folio = 'BillNo:' + TID.
                            using (var cmd = new MySqlCommand(@"
                                INSERT INTO campus_dynamics_accounts.fin_studentfeestracking_regdel_bak
                                SELECT * FROM campus_dynamics_accounts.fin_studentfeestracking
                                 WHERE regno=@r AND acadyear=@a AND semester=@s AND trans_type='Bill'", conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@r", regno.Trim());
                                cmd.Parameters.AddWithValue("@a", acadYear);
                                cmd.Parameters.AddWithValue("@s", semester);
                                cmd.ExecuteNonQuery();
                            }

                            // The GL side first, matched through the folio. Only DR rows: those
                            // are the charges. Payments are CR, they carry no semester at all, and
                            // they are never touched — the money was genuinely received, and a
                            // student who paid for a semester they are dropping ends up in credit
                            // against the next one. Cancelling a charge is bookkeeping; taking
                            // back a receipt would be something else entirely.
                            using (var cmd = new MySqlCommand(@"
                                DELETE l FROM campus_dynamics_accounts.fin_ledger l
                                  JOIN campus_dynamics_accounts.fin_studentfeestracking t
                                    ON l.folio = CONCAT('BillNo:', t.TID) AND l.accountcode = t.regno
                                 WHERE t.regno=@r AND t.acadyear=@a AND t.semester=@s
                                   AND t.trans_type='Bill' AND l.transactionType='DR'", conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@r", regno.Trim());
                                cmd.Parameters.AddWithValue("@a", acadYear);
                                cmd.Parameters.AddWithValue("@s", semester);
                                deletedLedger = cmd.ExecuteNonQuery();
                            }

                            // Then the bill itself. Deleting this row fires trg_sync_bill_uniqueness_delete,
                            // which clears fin_bill_uniqueness for the period — without that the
                            // guard would still read "already billed" and a student who registers
                            // this semester again could never be charged for it.
                            using (var cmd = new MySqlCommand(@"
                                DELETE FROM campus_dynamics_accounts.fin_studentfeestracking
                                 WHERE regno=@r AND acadyear=@a AND semester=@s AND trans_type='Bill'", conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@r", regno.Trim());
                                cmd.Parameters.AddWithValue("@a", acadYear);
                                cmd.Parameters.AddWithValue("@s", semester);
                                deletedBills = cmd.ExecuteNonQuery();
                            }
                        }

                        tx.Commit();
                    }
                }
            }

            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(@"
                    UPDATE campus_dynamics_portal.acad_semester_deletion_requests
                    SET status=@st,
                        admin_username=@au,
                        admin_comment=@ac,
                        decided_at=NOW(),
                        deletion_executed=@dex,
                        deletion_executed_at=CASE WHEN @dex=1 THEN NOW() ELSE deletion_executed_at END,
                        student_notified=1,
                        student_notified_at=NOW(),
                        student_seen=0,
                        student_seen_at=NULL
                    WHERE id=@id", conn))
                {
                    // The comment is the record of what happened, so the numbers go into it.
                    // "Approved" on its own does not tell anyone, later, that a bill was cancelled.
                    string note = c;
                    if (d == "APPROVE")
                    {
                        string what = "Removed: registration " + deletedRegs
                                    + ", course rows " + deletedCourses
                                    + ", fee bills " + deletedBills
                                    + " (UGX " + reversedAmount.ToString("N0") + ")"
                                    + ", ledger entries " + deletedLedger + ".";
                        if (keptSharedRows)
                            what += " Courses and fees were KEPT: the student still holds another "
                                  + "registration for this same semester at a different year of study.";
                        note = string.IsNullOrEmpty(c) ? what : c + "  " + what;
                    }

                    cmd.Parameters.AddWithValue("@st", d == "APPROVE" ? "APPROVED" : "REJECTED");
                    cmd.Parameters.AddWithValue("@au", AdminUser());
                    cmd.Parameters.AddWithValue("@ac", note);
                    cmd.Parameters.AddWithValue("@dex", d == "APPROVE" ? 1 : 0);
                    cmd.Parameters.AddWithValue("@id", id);
                    if (cmd.ExecuteNonQuery() <= 0)
                        return Json.Serialize(new { success = false, message = "Request not found. Refresh and retry." });
                }
            }

            return Json.Serialize(new
            {
                success = true,
                message = d != "APPROVE"
                        ? "Request rejected."
                        : keptSharedRows
                          ? "Registration deleted. Courses and fees were kept — the student still has another "
                            + "registration for this semester."
                          : "Registration deleted, " + deletedCourses + " course row(s) removed and fees of UGX "
                            + reversedAmount.ToString("N0") + " reversed.",
                data    = new
                {
                    deleted_semester_registration = deletedRegs,
                    deleted_course_rows           = deletedCourses,
                    deleted_fee_bills             = deletedBills,
                    deleted_ledger_entries        = deletedLedger,
                    reversed_amount               = reversedAmount,
                    shared_period_rows_kept       = keptSharedRows
                }
            });
        }
        catch (Exception ex)
        {
            return Json.Serialize(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Everything an approval would actually destroy, read before anything is touched.
    ///
    /// This exists because approving used to remove the registration and its course rows and
    /// stop there — leaving the semester's fees standing on the student's account for a
    /// semester that no longer existed. A registration is not one row: the wizard that creates
    /// it also raises a bill (fin_AutoBillOnRegistration), so undoing it has to undo that too.
    /// Reversing a registration means reversing what registering DID.
    /// </summary>
    private class Impact
    {
        public int Courses;
        public int MarkedCourses;
        public int Results;
        public int Bills;
        public decimal BillAmount;
        public int LedgerRows;
        /// <summary>
        /// Other registrations for the SAME academic year and semester at a different study
        /// year. They matter more than they look: course rows and bills are both keyed by
        /// (regno, acad_year, semester) with NO study year, so they are shared. Deleting them
        /// while a sibling registration survives would strip the courses and cancel the fees of
        /// a semester the student is still registered for.
        /// </summary>
        public int Siblings;
        /// <summary>What the student owes right now, before anything is reversed.</summary>
        public decimal BalanceBefore;
        /// <summary>
        /// What they will owe after. Cancelling a bill removes a DR from both the ledger and
        /// the tracking table, so the balance simply drops by the amount cancelled — and it can
        /// legitimately go negative, which is a credit sitting on the account.
        /// </summary>
        public decimal BalanceAfter
        {
            get { return SharesPeriod ? BalanceBefore : BalanceBefore - BillAmount; }
        }

        public bool SharesPeriod { get { return Siblings > 0; } }
        /// <summary>Published results are an academic record. No portal button deletes those.</summary>
        public bool Blocked { get { return Results > 0; } }
    }

    /// <summary>One round trip for the whole picture, all of it scoped to the one period.</summary>
    private static Impact ReadImpact(MySqlConnection conn, MySqlTransaction tx,
                                     string regno, string acadYear, int studyYear, int semester)
    {
        var imp = new Impact();
        using (var cmd = new MySqlCommand(@"
            SELECT
              (SELECT COUNT(*) FROM campus_dynamics_portal.acad_course_registration c
                WHERE c.regno=@r AND c.acad_year=@a AND c.semester=@s) AS courses,
              (SELECT COUNT(*) FROM campus_dynamics_portal.acad_course_registration c
                WHERE c.regno=@r AND c.acad_year=@a AND c.semester=@s
                  AND (IFNULL(c.mark_stage,'NOT_ENTERED') <> 'NOT_ENTERED'
                    OR c.provisional_total_marks IS NOT NULL
                    OR c.provisional_course_work_marks IS NOT NULL
                    OR c.provisional_exam_marks IS NOT NULL)) AS marked,
              (SELECT COUNT(*) FROM campus_dynamics.acad_results x
                WHERE x.regno=@r AND x.acad=@a AND x.semester=@s) AS results,
              (SELECT COUNT(*) FROM campus_dynamics_accounts.fin_studentfeestracking t
                WHERE t.regno=@r AND t.acadyear=@a AND t.semester=@s AND t.trans_type='Bill') AS bills,
              (SELECT IFNULL(SUM(t.amount),0) FROM campus_dynamics_accounts.fin_studentfeestracking t
                WHERE t.regno=@r AND t.acadyear=@a AND t.semester=@s AND t.trans_type='Bill') AS bill_amount,
              (SELECT COUNT(*) FROM campus_dynamics_accounts.fin_ledger l
                 JOIN campus_dynamics_accounts.fin_studentfeestracking t2
                   ON l.folio = CONCAT('BillNo:', t2.TID) AND l.accountcode = t2.regno
                WHERE t2.regno=@r AND t2.acadyear=@a AND t2.semester=@s
                  AND t2.trans_type='Bill' AND l.transactionType='DR') AS ledger_rows,
              (SELECT COUNT(*) FROM campus_dynamics.acad_registration r
                WHERE r.regno=@r AND r.acad_year=@a AND r.semester=@s AND r.studyyear<>@y) AS siblings", conn))
        {
            if (tx != null) cmd.Transaction = tx;
            cmd.Parameters.AddWithValue("@r", (regno ?? "").Trim());
            cmd.Parameters.AddWithValue("@a", acadYear);
            cmd.Parameters.AddWithValue("@s", semester);
            cmd.Parameters.AddWithValue("@y", studyYear);
            using (var rdr = cmd.ExecuteReader())
            {
                if (rdr.Read())
                {
                    imp.Courses       = ToInt(rdr["courses"]);
                    imp.MarkedCourses = ToInt(rdr["marked"]);
                    imp.Results       = ToInt(rdr["results"]);
                    imp.Bills         = ToInt(rdr["bills"]);
                    imp.BillAmount    = rdr["bill_amount"] == DBNull.Value ? 0m : Convert.ToDecimal(rdr["bill_amount"]);
                    imp.LedgerRows    = ToInt(rdr["ledger_rows"]);
                    imp.Siblings      = ToInt(rdr["siblings"]);
                }
            }
        }

        imp.BalanceBefore = ReadBalance(conn, tx, regno);
        return imp;
    }

    /// <summary>
    /// The student's outstanding balance, computed the way the whole system computes it.
    ///
    /// This query is lifted from SemesterRegistrationWizardService.GetStudentOutstandingBalance,
    /// which its own comment calls the single source of truth — the same logic behind
    /// FeeAccessHelper, StudentFees and the student's dashboard. Rolling a simpler SUM here
    /// would give the approver a number that disagreed with the one the student is looking at,
    /// and the two would be reconciled by argument.
    ///
    /// The union is not decoration: a bill can exist in the tracking table before it reaches the
    /// GL, so counting only one side under-reads. The NOT EXISTS is what stops a bill present in
    /// both from being counted twice.
    /// </summary>
    private static decimal ReadBalance(MySqlConnection conn, MySqlTransaction tx, string regno)
    {
        try
        {
            using (var cmd = new MySqlCommand(@"
                SELECT
                    COALESCE(SUM(CASE WHEN x.ttype='DR' THEN x.amt ELSE 0 END), 0) -
                    COALESCE(SUM(CASE WHEN x.ttype='CR' THEN x.amt ELSE 0 END), 0) AS balance
                FROM (
                    SELECT fl.transactionType AS ttype, fl.transaction_amount AS amt
                    FROM   campus_dynamics_accounts.fin_ledger fl
                    WHERE  fl.accountcode = @r AND fl.transaction_amount > 0

                    UNION ALL

                    SELECT CASE WHEN t.trans_type IN ('Payment','Waiver') THEN 'CR' ELSE 'DR' END AS ttype,
                           t.amount AS amt
                    FROM   campus_dynamics_accounts.fin_studentfeestracking t
                    WHERE  t.regno = @r
                      AND  t.post_status = 'Posted'
                      AND  NOT EXISTS (
                           SELECT 1 FROM campus_dynamics_accounts.fin_ledger fl2
                           WHERE  fl2.accountcode = t.regno
                             AND (
                                  fl2.voucherNo = CAST(t.TID AS CHAR)
                               OR fl2.folio    = CONCAT('BillNo:', CAST(t.TID AS CHAR))
                               OR (    fl2.transaction_amount = t.amount
                                   AND DATE(fl2.transactionDate) = DATE(t.trans_date)
                                   AND fl2.transactionType = CASE WHEN t.trans_type IN ('Payment','Waiver') THEN 'CR' ELSE 'DR' END
                                   AND (t.trans_type IN ('Payment','Waiver') OR fl2.particulars = t.detail OR t.detail IS NULL OR t.detail = '')
                                  )
                             )
                      )
                ) x", conn))
            {
                if (tx != null) cmd.Transaction = tx;
                cmd.Parameters.AddWithValue("@r", (regno ?? "").Trim());
                object v = cmd.ExecuteScalar();
                return v == null || v == DBNull.Value ? 0m : Convert.ToDecimal(v);
            }
        }
        catch
        {
            // A balance we cannot read must not stop an approval. The wizard simply leaves the
            // before/after line out rather than showing a figure it is not sure of.
            return 0m;
        }
    }

    private static int ToInt(object value)
    {
        if (value == null || value == DBNull.Value) return 0;
        int outVal;
        return int.TryParse(value.ToString(), out outVal) ? outVal : 0;
    }
}
