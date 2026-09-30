using System;
using System.Web;
using System.Web.Services;
using System.Web.UI;
using MySql.Data.MySqlClient;   // Force ID collection reads/writes idcard_force_collection

/// <summary>
/// eadmin ID Card operations console. Thin WebMethods over IDCardService; the
/// engine owns all logic + the state-machine funnel. Actor resolved from session.
/// </summary>
public partial class COOPERP_NewScreens_IDCardController : System.Web.UI.Page
{    /// <summary>
    /// The status filter's options, labelled from the one shared table so the dropdown
    /// cannot disagree with the tiles above it or the chips below it. The VALUE stays the
    /// raw status, because that is what the server filters on and what the URL carries.
    /// </summary>
    protected string IDCardOptions()
    {
        var sb = new System.Text.StringBuilder();
        foreach (string st in IDCardService.AllStatuses)
            sb.Append("<option value=\"").Append(st).Append("\">")
              .Append(Server.HtmlEncode(IDCardService.StatusLabel(st))).Append("</option>");
        return sb.ToString();
    }
    protected void Page_Load(object sender, EventArgs e) { }

    private static string Actor()
    {
        var s = HttpContext.Current != null ? HttpContext.Current.Session : null;
        string u = s != null ? (s["username"] as string) : null;
        return string.IsNullOrEmpty(u) ? "admin" : u;
    }

    /// <summary>
    /// True when the request carries a signed-in staff session.
    ///
    /// These PageMethods were reachable by anyone: a POST with no cookies to
    /// IDCardController.aspx/Action ran the state machine, and Actor() then signed the
    /// audit trail "admin". Verified against production before this was added — Stats
    /// returned the live counts and Action reached the engine. Every method below now
    /// refuses an unauthenticated caller.
    ///
    /// Accepts either signal the eadmin screens use, because different entry points
    /// establish one or the other and requiring both would lock out real staff.
    /// </summary>
    private static bool Authed()
    {
        var ctx = HttpContext.Current;
        if (ctx == null) return false;
        try
        {
            if (ctx.User != null && ctx.User.Identity != null && ctx.User.Identity.IsAuthenticated
                && !string.IsNullOrEmpty(ctx.User.Identity.Name)) return true;
        }
        catch { }
        try
        {
            if (ctx.Session != null)
            {
                object u = ctx.Session["username"];
                if (u != null && !string.IsNullOrEmpty(u.ToString().Trim())) return true;
            }
        }
        catch { }
        return false;
    }

    /// <summary>
    /// The refusal. A PageMethod cannot usefully return 401 — FormsAuthenticationModule
    /// turns it into a 302 to the login page and the caller receives HTML where it
    /// expected JSON, reporting "request failed" instead of "your session expired".
    /// </summary>
    private const string Denied = "{\"success\":false,\"message\":\"Your session has expired. Please sign in again, then retry.\"}";

    [WebMethod(EnableSession = true)]
    public static string Stats() { return Authed() ? IDCardService.StatsJson() : Denied; }

    /// <summary>Statuses and the legal-transition map, so the console's status picker
    /// can mark which moves are ordinary and which need an override.</summary>
    [WebMethod(EnableSession = true)]
    public static string Meta() { return Authed() ? IDCardService.MetaJson() : Denied; }

    /// <summary>
    /// Move a request to any status. A legal move behaves exactly like the ordinary
    /// action buttons; anything else needs override=true and a reason, and is written
    /// into the request's history as an override. See IDCardService.SetStatusJson.
    /// </summary>
    [WebMethod(EnableSession = true)]
    public static string SetStatus(string requestNo, string toStatus, string reason, bool allowOverride)
    {
        if (!Authed()) return Denied;
        return IDCardService.SetStatusJson(requestNo, toStatus, reason, Actor(), "admin", "eadmin", allowOverride);
    }

    [WebMethod(EnableSession = true)]
    public static string List(string status, string type, string cardType, string q, int page, int size)
    { return Authed() ? IDCardService.ListJson(status, type, cardType, q, page, size <= 0 ? 50 : size) : Denied; }

    [WebMethod(EnableSession = true)]
    public static string Detail(string requestNo) { return Authed() ? IDCardService.DetailJson(requestNo) : Denied; }

    [WebMethod(EnableSession = true)]
    public static string Action(string requestNo, string action, string reason, string collectionPoint)
    { return Authed() ? IDCardService.ActionJson(requestNo, action, reason, collectionPoint, Actor(), "admin", "eadmin") : Denied; }

    /// <summary>
    /// Apply one action to many requests. Each is run through the same state-machine
    /// funnel as Action(); requests in an incompatible state simply fail (reported
    /// per-item) without affecting the others.
    /// </summary>
    [WebMethod(EnableSession = true)]
    public static string BatchAction(string requestNos, string action, string reason, string collectionPoint)
    { return Authed() ? IDCardService.BatchActionJson(requestNos, action, reason, collectionPoint, Actor(), "admin", "eadmin", 500) : Denied; }

    // ════════════════════════════════════════════════════════════════════════════
    //  FORCE ID COLLECTION
    //
    //  Puts a student under an obligation to collect their printed card from a named
    //  office. Once the enforce-from date arrives, the portal stops them until the
    //  counter marks the card collected. Reads/writes idcard_force_collection; the
    //  shared IDCardService owns the schema and the "is it in force" rule so the
    //  portal gate and this console can never disagree.
    // ════════════════════════════════════════════════════════════════════════════

    private static string ConnStr { get { return IDCardService.ConnStr; } }

    /// <summary>
    /// The numbers this typed one could plausibly mean, best guess first.
    ///
    /// Numbers are issued as MRU + year + six digits (MRU2026000001), but the leading
    /// zeros are exactly what people get wrong — they type MRU2026004301 as
    /// MRU20260004301 or MRU202604301, and then have to sit there adding and deleting
    /// zeros. So the significant part is taken (everything after the year, with leading
    /// zeros stripped) and re-padded to every width it could have been. All candidates
    /// are exact strings, so each is a primary-key hit rather than a scan.
    /// </summary>
    private static System.Collections.Generic.List<string> RegnoCandidates(string typed)
    {
        var outp = new System.Collections.Generic.List<string>();
        string t = (typed ?? "").Trim().ToUpperInvariant();
        if (t == "") return outp;
        outp.Add(t);                                    // what they typed wins if it is real

        var m = System.Text.RegularExpressions.Regex.Match(t, "^MRU([0-9]{4})([0-9]+)$");
        if (!m.Success) return outp;

        string year = m.Groups[1].Value;
        string core = m.Groups[2].Value.TrimStart('0');
        if (core == "") core = "0";

        // 6 first: that is the issued width for 99.9% of the roll.
        int[] widths = { 6, 5, 4, 3, 7 };
        foreach (int w in widths)
        {
            if (core.Length > w) continue;
            string cand = "MRU" + year + core.PadLeft(w, '0');
            if (!outp.Contains(cand)) outp.Add(cand);
        }
        return outp;
    }

    /// <summary>
    /// When this account was last seen in the portal, and how long ago in words.
    ///
    /// Read from the membership tables, which are the only place the portal records
    /// it: LastLoginDate is the last successful sign-in and the two LastActivityDate
    /// columns are touched as the session is used, so the newest of the three is the
    /// truthful "last seen". All 16,913 student accounts carry all three.
    ///
    /// Keyed on my_aspnet_users.name, which is the registration number and carries a
    /// UNIQUE index in the same utf8_general_ci collation — EXPLAIN reports const on
    /// both tables (a unique-index seek, one row each) and it measures 0.55 ms, so it
    /// costs the lookup nothing. Deliberately a separate query rather than a join onto
    /// the candidate search: it runs once, on the ALREADY-RESOLVED number, so it stays
    /// an exact index seek instead of widening that query across databases.
    ///
    /// Never throws — a missing or unreadable account just means "not known", which
    /// must not stop an operator applying a collection block.
    /// </summary>
    private static void LastSeen(MySqlConnection c, string regno, out string when, out string ago)
    {
        when = ""; ago = "";
        try
        {
            DateTime? seen = null;
            using (var cmd = new MySqlCommand(
                "SELECT GREATEST(IFNULL(u.lastActivityDate,'1000-01-01')," +
                "                IFNULL(m.LastLoginDate,'1000-01-01')," +
                "                IFNULL(m.LastActivityDate,'1000-01-01')) AS seen " +
                "FROM campus_dynamics_portal.my_aspnet_users u " +
                "LEFT JOIN campus_dynamics_portal.my_aspnet_membership m ON m.userId = u.id " +
                "WHERE u.name = @r LIMIT 1", c))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                object v = cmd.ExecuteScalar();
                if (v != null && v != DBNull.Value)
                {
                    DateTime d = Convert.ToDateTime(v);
                    if (d.Year > 1900) seen = d;
                }
            }
            if (!seen.HasValue) return;

            when = seen.Value.ToString("d MMM yyyy, HH:mm");
            TimeSpan gap = DateTime.Now - seen.Value;
            if (gap.TotalSeconds < 0) ago = "just now";
            else if (gap.TotalMinutes < 2) ago = "just now";
            else if (gap.TotalMinutes < 60) ago = ((int)gap.TotalMinutes) + " minutes ago";
            else if (gap.TotalHours < 24) ago = ((int)gap.TotalHours) + (((int)gap.TotalHours) == 1 ? " hour ago" : " hours ago");
            else if (gap.TotalDays < 31) ago = ((int)gap.TotalDays) + (((int)gap.TotalDays) == 1 ? " day ago" : " days ago");
            else if (gap.TotalDays < 365) ago = ((int)(gap.TotalDays / 30)) + " months ago";
            else ago = ((int)(gap.TotalDays / 365)) + (((int)(gap.TotalDays / 365)) == 1 ? " year ago" : " years ago");
        }
        catch { when = ""; ago = ""; }
    }

    /// <summary>
    /// Who this registration number belongs to, plus any obligation already on them.
    /// Drives the modal: the operator types an ID and the form fills itself in, so a
    /// block is never applied to a number that was mistyped. Resolves leading-zero
    /// variants itself and returns the REAL number, which the modal writes back into
    /// the box — so the operator never corrects zeros by hand.
    /// </summary>
    [WebMethod(EnableSession = true)]
    public static string ForceLookup(string regno)
    {
        if (!Authed()) return Denied;
        regno = (regno ?? "").Trim();
        if (regno.Length < 6) return "{\"success\":false,\"message\":\"Enter a full registration number.\"}";
        try
        {
            using (var c = new MySqlConnection(ConnStr))
            {
                c.Open();
                IDCardService.EnsureForceCollectionSchema(c);

                var cands = RegnoCandidates(regno);
                if (cands.Count == 0)
                    return "{\"success\":false,\"message\":\"Enter a full registration number.\"}";

                var names = new System.Text.StringBuilder();
                for (int i = 0; i < cands.Count; i++) { if (i > 0) names.Append(","); names.Append("@c").Append(i); }

                string name = "", prog = "", campus = "", status = "", entry = "";
                using (var cmd = new MySqlCommand(
                    "SELECT TRIM(s.regno) AS regno," +
                    // Surname first, matching the exam permit (acad_ExamPass builds its
                    // full_name the same way round). Each part is trimmed on its own, not
                    // just the joined string: firstname is stored with a leading space
                    // (" DEVIS"), which would otherwise show as a double gap once reversed.
                    "       TRIM(CONCAT(TRIM(IFNULL(s.othername,'')),' ',TRIM(IFNULL(s.firstname,'')))) AS nm," +
                    "       IFNULL(s.progid,'') AS prog, IFNULL(s.new_status,'') AS st," +
                    "       IFNULL(s.entryyear,'') AS ey," +
                    "       IFNULL(cp.campus_name, CONCAT('Campus ', IFNULL(s.studCampus,0))) AS campus " +
                    "FROM acad_student s LEFT JOIN acad_campuses cp ON cp.ID = s.studCampus " +
                    "WHERE TRIM(s.regno) IN (" + names + ") " +
                    "ORDER BY FIELD(TRIM(s.regno)," + names + ") LIMIT 1", c))
                {
                    for (int i = 0; i < cands.Count; i++) cmd.Parameters.AddWithValue("@c" + i, cands[i]);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                            return "{\"success\":false,\"message\":\"No student found with that registration number.\"}";
                        regno = Convert.ToString(r["regno"]).Trim();   // the real one, zeros and all
                        name = Convert.ToString(r["nm"]).Trim();
                        prog = Convert.ToString(r["prog"]);
                        status = Convert.ToString(r["st"]);
                        entry = Convert.ToString(r["ey"]);
                        campus = Convert.ToString(r["campus"]);
                    }
                }

                // The card's own state, so the operator can see whether there is even a
                // card to collect before ordering somebody across town for it.
                string cardStatus = "";
                using (var cmd = new MySqlCommand(
                    "SELECT status FROM idcard_requests WHERE TRIM(regno)=TRIM(@r) " +
                    "ORDER BY FIELD(status,'READY','PRINTED','APPROVED','SUBMITTED') , id DESC LIMIT 1", c))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    object v = cmd.ExecuteScalar();
                    cardStatus = (v == null || v == DBNull.Value) ? "" : Convert.ToString(v);
                }

                var fc = IDCardService.ForceCollectionFor(regno);

                string seenWhen, seenAgo;
                LastSeen(c, regno, out seenWhen, out seenAgo);

                var sb = new System.Text.StringBuilder();
                sb.Append("{\"success\":true,\"regno\":\"").Append(J(regno))
                  .Append("\",\"name\":\"").Append(J(name))
                  .Append("\",\"programme\":\"").Append(J(prog))
                  .Append("\",\"studentStatus\":\"").Append(J(status))
                  .Append("\",\"entryYear\":\"").Append(J(entry))
                  .Append("\",\"campus\":\"").Append(J(campus))
                  .Append("\",\"cardStatus\":\"").Append(J(cardStatus))
                  .Append("\",\"cardStatusLabel\":\"").Append(J(IDCardService.StatusLabel(cardStatus)))
                  .Append("\",\"lastSeen\":\"").Append(J(seenWhen))
                  .Append("\",\"lastSeenAgo\":\"").Append(J(seenAgo))
                  .Append("\",\"hasExisting\":").Append(fc.Exists ? "true" : "false")
                  .Append(",\"existingActive\":").Append(fc.Active ? "true" : "false")
                  .Append(",\"existingCollected\":").Append(fc.Collected ? "true" : "false")
                  .Append(",\"group\":\"").Append(J(fc.GroupName)).Append("\"")
                  .Append(",\"point\":\"").Append(J(fc.Exists ? fc.CollectionPoint : IDCardService.POINT_AR))
                  .Append("\",\"enforceFrom\":\"")
                  .Append(fc.EnforceFrom.HasValue ? fc.EnforceFrom.Value.ToString("yyyy-MM-dd") : "")
                  .Append("\",\"note\":\"").Append(J(fc.Note)).Append("\"}");
                return sb.ToString();
            }
        }
        catch (Exception ex) { return "{\"success\":false,\"message\":\"" + J(ex.Message) + "\"}"; }
    }

    /// <summary>Applies (or updates) the obligation for one or many students.</summary>
    [WebMethod(EnableSession = true)]
    public static string ForceSave(string regnos, string point, string enforceFrom, string note, string groupName)
    {
        if (!Authed()) return Denied;
        point = string.Equals((point ?? "").Trim(), IDCardService.POINT_IT, StringComparison.OrdinalIgnoreCase)
            ? IDCardService.POINT_IT : IDCardService.POINT_AR;

        DateTime from;
        if (!DateTime.TryParseExact((enforceFrom ?? "").Trim(), "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out from))
            from = DateTime.Today;

        // Only the batch letters the console offers. Anything else becomes no group at
        // all rather than being stored as typed — this column is grouped and filtered on.
        string grp = (groupName ?? "").Trim().ToUpperInvariant();
        if (grp.Length != 1 || "ABCEFGHIJK".IndexOf(grp) < 0) grp = "";

        var list = new System.Collections.Generic.List<string>();
        foreach (string raw in (regnos ?? "").Split(','))
        {
            string v = raw.Trim();
            if (v != "" && !list.Contains(v)) list.Add(v);
        }
        if (list.Count == 0) return "{\"success\":false,\"message\":\"No student selected.\"}";
        if (list.Count > 500) return "{\"success\":false,\"message\":\"Too many at once — send 500 or fewer.\"}";

        string actor = Actor();
        int applied = 0, unknown = 0;
        var missing = new System.Collections.Generic.List<string>();
        try
        {
            using (var c = new MySqlConnection(ConnStr))
            {
                c.Open();
                IDCardService.EnsureForceCollectionSchema(c);
                foreach (string reg in list)
                {
                    // Never place a block on a number that is not a student.
                    string campus = null;
                    using (var chk = new MySqlCommand(
                        "SELECT IFNULL(cp.campus_name, CONCAT('Campus ', IFNULL(s.studCampus,0))) " +
                        "FROM acad_student s LEFT JOIN acad_campuses cp ON cp.ID = s.studCampus " +
                        "WHERE TRIM(s.regno)=TRIM(@r) LIMIT 1", c))
                    {
                        chk.Parameters.AddWithValue("@r", reg);
                        object v = chk.ExecuteScalar();
                        if (v == null || v == DBNull.Value) { unknown++; if (missing.Count < 10) missing.Add(reg); continue; }
                        campus = Convert.ToString(v);
                    }

                    // Re-applying resets the obligation: it becomes active and uncollected
                    // again, which is what "force them again" has to mean.
                    using (var cmd = new MySqlCommand(
                        "INSERT INTO idcard_force_collection " +
                        " (regno, is_active, collection_point, campus, group_name, enforce_from, note, collected, created_by, created_at) " +
                        "VALUES (@r, 1, @p, @cam, @g, @f, @n, 0, @by, NOW()) " +
                        "ON DUPLICATE KEY UPDATE is_active=1, collection_point=@p, campus=@cam, group_name=@g, " +
                        " enforce_from=@f, note=@n, collected=0, collected_at=NULL, collected_by=NULL, " +
                        " updated_by=@by, updated_at=NOW()", c))
                    {
                        cmd.Parameters.AddWithValue("@r", reg);
                        cmd.Parameters.AddWithValue("@p", point);
                        cmd.Parameters.AddWithValue("@cam", (object)(campus ?? ""));
                        cmd.Parameters.AddWithValue("@g", grp == "" ? (object)DBNull.Value : grp);
                        cmd.Parameters.AddWithValue("@f", from.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@n", (object)((note ?? "").Trim()));
                        cmd.Parameters.AddWithValue("@by", actor);
                        cmd.ExecuteNonQuery();
                        applied++;
                    }
                }
            }
        }
        catch (Exception ex) { return "{\"success\":false,\"message\":\"" + J(ex.Message) + "\"}"; }

        return "{\"success\":true,\"applied\":" + applied + ",\"unknown\":" + unknown
             + ",\"missing\":\"" + J(string.Join(", ", missing.ToArray()))
             + "\",\"point\":\"" + J(IDCardService.CollectionPointLabel(point))
             + "\",\"group\":\"" + J(grp)
             + "\",\"enforceFrom\":\"" + from.ToString("yyyy-MM-dd") + "\"}";
    }

    /// <summary>
    /// Clears the obligation. "collected" records that the card was handed over (the
    /// normal way out); "cancel" lifts a block that should not have been applied.
    /// </summary>
    [WebMethod(EnableSession = true)]
    public static string ForceClear(string regnos, string mode)
    {
        if (!Authed()) return Denied;
        bool collected = !string.Equals((mode ?? "").Trim(), "cancel", StringComparison.OrdinalIgnoreCase);

        var list = new System.Collections.Generic.List<string>();
        foreach (string raw in (regnos ?? "").Split(','))
        { string v = raw.Trim(); if (v != "" && !list.Contains(v)) list.Add(v); }
        if (list.Count == 0) return "{\"success\":false,\"message\":\"No student selected.\"}";

        string actor = Actor();
        int n = 0;
        try
        {
            using (var c = new MySqlConnection(ConnStr))
            {
                c.Open();
                IDCardService.EnsureForceCollectionSchema(c);
                foreach (string reg in list)
                {
                    string sql = collected
                        ? "UPDATE idcard_force_collection SET collected=1, collected_at=NOW(), collected_by=@by," +
                          " updated_by=@by, updated_at=NOW() WHERE TRIM(regno)=TRIM(@r)"
                        : "UPDATE idcard_force_collection SET is_active=0, updated_by=@by, updated_at=NOW()" +
                          " WHERE TRIM(regno)=TRIM(@r)";
                    using (var cmd = new MySqlCommand(sql, c))
                    {
                        cmd.Parameters.AddWithValue("@r", reg);
                        cmd.Parameters.AddWithValue("@by", actor);
                        n += cmd.ExecuteNonQuery();
                    }
                }
            }
        }
        catch (Exception ex) { return "{\"success\":false,\"message\":\"" + J(ex.Message) + "\"}"; }
        return "{\"success\":true,\"cleared\":" + n + ",\"mode\":\"" + (collected ? "collected" : "cancel") + "\"}";
    }

    /// <summary>
    /// Pushes every obligation still in force back by a number of days.
    ///
    /// This exists because the blockade is only as good as the card supply behind it. When
    /// printing stops, a student who is told to collect a card that nobody can hand them is
    /// locked out of their portal for something they cannot do — so the obligation has to
    /// move, in bulk, at the moment the supply pauses. Before this it was a database job.
    ///
    /// Three things it deliberately does NOT do:
    ///
    ///   it never pulls a date FORWARD.  GREATEST() means a student already scheduled for
    ///                                   later than the new date keeps their later date. A
    ///                                   postponement that quietly brought somebody's block
    ///                                   closer would be the opposite of what it says.
    ///
    ///   it leaves collected rows alone. Somebody who has their card is finished with; moving
    ///                                   their date would resurrect a settled obligation.
    ///
    ///   it leaves cancelled rows alone. is_active=0 was a decision, not an oversight.
    ///
    /// The reason is written into the note so that six weeks later the record still says why
    /// a date moved, and updated_by records who moved it.
    /// </summary>
    [WebMethod(EnableSession = true)]
    public static string ForcePostpone(int days, string why)
    {
        if (!Authed()) return Denied;
        if (days < 1 || days > 90)
            return "{\"success\":false,\"message\":\"Choose between 1 and 90 days.\"}";

        string reason = (why ?? "").Trim();
        if (reason.Length > 120) reason = reason.Substring(0, 120);
        if (reason == "") reason = "card supply paused";

        string actor = Actor();
        int n = 0;
        string newDate = "";
        try
        {
            using (var c = new MySqlConnection(ConnStr))
            {
                c.Open();
                IDCardService.EnsureForceCollectionSchema(c);

                using (var cmd = new MySqlCommand(
                    "SELECT DATE_FORMAT(DATE_ADD(CURDATE(), INTERVAL @d DAY), '%Y-%m-%d')", c))
                {
                    cmd.Parameters.AddWithValue("@d", days);
                    object v = cmd.ExecuteScalar();
                    newDate = v == null ? "" : v.ToString();
                }

                using (var cmd = new MySqlCommand(
                    "UPDATE idcard_force_collection " +
                    "   SET enforce_from = GREATEST(enforce_from, DATE_ADD(CURDATE(), INTERVAL @d DAY))," +
                    "       note = TRIM(CONCAT(IFNULL(note,'')," +
                    "              CASE WHEN IFNULL(note,'')='' THEN '' ELSE ' | ' END," +
                    "              'Held to ', DATE_FORMAT(DATE_ADD(CURDATE(), INTERVAL @d DAY), '%e %b %Y')," +
                    "              ' - ', @why, ' ', DATE_FORMAT(CURDATE(), '%e %b %Y'), '.'))," +
                    "       updated_by = @by," +
                    "       updated_at = NOW() " +
                    " WHERE is_active = 1 AND collected = 0 " +
                    "   AND enforce_from < DATE_ADD(CURDATE(), INTERVAL @d DAY)", c))
                {
                    cmd.Parameters.AddWithValue("@d", days);
                    cmd.Parameters.AddWithValue("@why", reason);
                    cmd.Parameters.AddWithValue("@by", actor);
                    n = cmd.ExecuteNonQuery();
                }
            }
        }
        catch (Exception ex) { return "{\"success\":false,\"message\":\"" + J(ex.Message) + "\"}"; }

        return "{\"success\":true,\"moved\":" + n + ",\"until\":\"" + J(newDate) + "\"}";
    }

    /// <summary>The obligation list, for the console's own tab.</summary>
    [WebMethod(EnableSession = true)]
    public static string ForceList(string state, string q)
    {
        if (!Authed()) return Denied;
        state = (state ?? "").Trim().ToLowerInvariant();
        q = (q ?? "").Trim();
        var sb = new System.Text.StringBuilder();
        int active = 0, waiting = 0, done = 0;
        try
        {
            using (var c = new MySqlConnection(ConnStr))
            {
                c.Open();
                IDCardService.EnsureForceCollectionSchema(c);

                using (var cmd = new MySqlCommand(
                    "SELECT SUM(is_active=1 AND collected=0 AND enforce_from<=CURDATE()) a," +
                    "       SUM(is_active=1 AND collected=0 AND enforce_from> CURDATE()) w," +
                    "       SUM(collected=1) d FROM idcard_force_collection", c))
                using (var r = cmd.ExecuteReader())
                    if (r.Read())
                    {
                        active = r[0] == DBNull.Value ? 0 : Convert.ToInt32(r[0]);
                        waiting = r[1] == DBNull.Value ? 0 : Convert.ToInt32(r[1]);
                        done = r[2] == DBNull.Value ? 0 : Convert.ToInt32(r[2]);
                    }

                string where = " WHERE 1=1 ";
                if (state == "active") where += " AND f.is_active=1 AND f.collected=0 AND f.enforce_from<=CURDATE() ";
                else if (state == "scheduled") where += " AND f.is_active=1 AND f.collected=0 AND f.enforce_from>CURDATE() ";
                else if (state == "collected") where += " AND f.collected=1 ";
                if (q != "") where += " AND (f.regno LIKE @q OR CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,'')) LIKE @q) ";

                using (var cmd = new MySqlCommand(
                    "SELECT f.regno, f.collection_point, IFNULL(f.campus,'') campus," +
                    "       IFNULL(f.group_name,'') group_name, f.enforce_from," +
                    "       f.is_active, f.collected, IFNULL(f.note,'') note," +
                    "       IFNULL(f.created_by,'') created_by, f.created_at," +
                    // Same order as the modal, so one student never appears two ways.
                    "       TRIM(CONCAT(TRIM(IFNULL(s.othername,'')),' ',TRIM(IFNULL(s.firstname,'')))) nm," +
                    "       (f.is_active=1 AND f.collected=0 AND f.enforce_from<=CURDATE()) in_force " +
                    "FROM idcard_force_collection f " +
                    "LEFT JOIN acad_student s ON TRIM(s.regno)=TRIM(f.regno) " + where +
                    // Clustered by campus then batch so the colour-coded numbers group on screen —
                    // that is what makes the list sortable by eye.
                    "ORDER BY f.collected, f.campus, IFNULL(f.group_name,'~'), f.enforce_from DESC, f.regno LIMIT 400", c))
                {
                    if (q != "") cmd.Parameters.AddWithValue("@q", "%" + q + "%");
                    using (var r = cmd.ExecuteReader())
                    {
                        sb.Append("[");
                        bool first = true;
                        while (r.Read())
                        {
                            if (!first) sb.Append(",");
                            first = false;
                            sb.Append("{\"regno\":\"").Append(J(Convert.ToString(r["regno"])))
                              .Append("\",\"name\":\"").Append(J(Convert.ToString(r["nm"])))
                              .Append("\",\"point\":\"").Append(J(IDCardService.CollectionPointLabel(Convert.ToString(r["collection_point"]))))
                              .Append("\",\"campus\":\"").Append(J(Convert.ToString(r["campus"])))
                              .Append("\",\"group\":\"").Append(J(Convert.ToString(r["group_name"])))
                              .Append("\",\"from\":\"").Append(r["enforce_from"] == DBNull.Value ? "" : Convert.ToDateTime(r["enforce_from"]).ToString("yyyy-MM-dd"))
                              .Append("\",\"note\":\"").Append(J(Convert.ToString(r["note"])))
                              .Append("\",\"by\":\"").Append(J(Convert.ToString(r["created_by"])))
                              .Append("\",\"collected\":").Append(Convert.ToInt32(r["collected"]) == 1 ? "true" : "false")
                              .Append(",\"active\":").Append(Convert.ToInt32(r["is_active"]) == 1 ? "true" : "false")
                              .Append(",\"inForce\":").Append(Convert.ToInt32(r["in_force"]) == 1 ? "true" : "false")
                              .Append("}");
                        }
                        sb.Append("]");
                    }
                }
            }
        }
        catch (Exception ex) { return "{\"success\":false,\"message\":\"" + J(ex.Message) + "\"}"; }

        return "{\"success\":true,\"active\":" + active + ",\"scheduled\":" + waiting
             + ",\"collected\":" + done + ",\"rows\":" + sb.ToString() + "}";
    }

    /// <summary>Minimal JSON string escaping for the hand-built payloads above.</summary>
    private static string J(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new System.Text.StringBuilder(s.Length + 8);
        foreach (char ch in s)
        {
            if (ch == '"' || ch == '\\') sb.Append('\\').Append(ch);
            else if (ch == '\n') sb.Append("\\n");
            else if (ch == '\r') sb.Append("\\r");
            else if (ch == '\t') sb.Append("\\t");
            else if (ch < ' ') sb.Append("\\u").Append(((int)ch).ToString("x4"));
            else sb.Append(ch);
        }
        return sb.ToString();
    }

    [WebMethod(EnableSession = true)]
    public static string Windows() { return Authed() ? IDCardService.WindowsJson() : Denied; }

    [WebMethod(EnableSession = true)]
    public static string CreateWindow(string title, string scope, string opensAt, string closesAt, string notes)
    { return Authed() ? IDCardService.CreateWindowJson(title, scope, opensAt, closesAt, notes, Actor()) : Denied; }

    [WebMethod(EnableSession = true)]
    public static string SetWindow(int id, bool active) { return Authed() ? IDCardService.SetWindowActiveJson(id, active) : Denied; }
}
