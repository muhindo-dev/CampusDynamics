using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Web.Configuration;
using System.Web.Script.Serialization;
using System.Web.Script.Services;
using System.Web.Services;
using System.Web.UI;
using MySql.Data.MySqlClient;

/// <summary>
/// Admin 360° Mark Requests Controller — GET-based list/filters/paging, AJAX for actions only.
/// </summary>
[ScriptService]
public partial class COOPERP_NewScreens_MarkRequestsAdmin : Page
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 };

    // ── Filter state (set in Page_Load, used by ASPX <%= %> expressions) ──
    protected string FilterYear   { get; private set; }
    protected string FilterSem    { get; private set; }
    protected string FilterType   { get; private set; }
    protected string FilterStatus { get; private set; }
    protected string FilterQ      { get; private set; }
    protected int    FilterPage   { get; private set; }
    protected int    FilterSize   { get; private set; }
    protected int    TotalCount   { get; private set; }

    private static string ConnStr
    {
        get { return WebConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    // ── Student name column — detected once per AppDomain, never re-queried ──
    // null = not yet resolved; "" = neither column exists; otherwise the column name
    private static volatile string _nameColResolved;
    private static readonly object _nameColLock = new object();

    private const string CacheKeyYears = "mra_acad_year_list_v1";

    private static void EnsureNameCol(MySqlConnection conn)
    {
        if (_nameColResolved != null) return;
        lock (_nameColLock)
        {
            if (_nameColResolved != null) return;
            foreach (string col in new[] { "StudentName", "stud_name" })
            {
                try
                {
                    using (var c = new MySqlCommand("SELECT `" + col + "` FROM acad_student LIMIT 0", conn))
                    using (var r = c.ExecuteReader()) { r.Close(); }
                    _nameColResolved = col;
                    return;
                }
                catch { }
            }
            _nameColResolved = "";
        }
    }

    private static string NameExpr
    {
        get
        {
            string col = _nameColResolved ?? "";
            return col.Length > 0 ? "IFNULL(s." + col + ",r.regno)" : "r.regno";
        }
    }

    private static bool NeedStudentJoin
    {
        get { return (_nameColResolved ?? "").Length > 0; }
    }

    protected string HE(string s)
    {
        return HttpUtility.HtmlEncode(s ?? "");
    }

    protected string Sel(string val, string opt)
    {
        return string.Equals((val ?? "").Trim(), (opt ?? "").Trim(), StringComparison.OrdinalIgnoreCase)
            ? " selected" : "";
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  PAGE LOAD — parse QS, single connection for all three loads
    // ════════════════════════════════════════════════════════════════════════════
    protected void Page_Load(object sender, EventArgs e)
    {
        FilterYear   = (Request.QueryString["year"]   ?? "").Trim();
        FilterSem    = (Request.QueryString["sem"]    ?? "").Trim();
        FilterType   = (Request.QueryString["type"]   ?? "").Trim().ToUpperInvariant();
        FilterQ      = (Request.QueryString["q"]      ?? "").Trim();

        FilterStatus = (Request.QueryString["status"] ?? "").Trim().ToUpperInvariant();
        // "ALL" sentinel from the dropdown = no status filter
        if (string.Equals(FilterStatus, "ALL", StringComparison.OrdinalIgnoreCase))
            FilterStatus = "";

        int pg, sz;
        int.TryParse(Request.QueryString["page"] ?? "1",  out pg);
        int.TryParse(Request.QueryString["size"] ?? "50", out sz);
        FilterPage = Math.Max(1, pg);
        bool validSz = sz == 25 || sz == 50 || sz == 100 || sz == 200;
        FilterSize = validSz ? sz : 50;

        using (var conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            EnsureNameCol(conn);
            LoadYearOptions(conn);
            LoadStats(conn);
            LoadRows(conn);
        }
    }

    // ── URL builder for pager links ──────────────────────────────────────────
    private string PageUrl(int page)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(FilterYear))   parts.Add("year="   + Uri.EscapeDataString(FilterYear));
        if (!string.IsNullOrEmpty(FilterSem))    parts.Add("sem="    + Uri.EscapeDataString(FilterSem));
        if (!string.IsNullOrEmpty(FilterType))   parts.Add("type="   + Uri.EscapeDataString(FilterType));
        if (!string.IsNullOrEmpty(FilterStatus)) parts.Add("status=" + Uri.EscapeDataString(FilterStatus));
        if (!string.IsNullOrEmpty(FilterQ))      parts.Add("q="      + Uri.EscapeDataString(FilterQ));
        if (FilterSize != 50)                    parts.Add("size="   + FilterSize);
        if (page > 1)                            parts.Add("page="   + page);
        string qs = string.Join("&", parts.ToArray());
        return Request.Url.AbsolutePath + (qs.Length > 0 ? "?" + qs : "");
    }

    // ── Year dropdown — values cached for 90 s (rarely changes) ─────────────
    private void LoadYearOptions(MySqlConnection conn)
    {
        List<string> years = HttpRuntime.Cache[CacheKeyYears] as List<string>;
        if (years == null)
        {
            years = new List<string>();
            try
            {
                using (var cmd = new MySqlCommand(@"
                    SELECT DISTINCT acad_year
                    FROM campus_dynamics_portal.acad_marks_requests
                    WHERE acad_year IS NOT NULL AND acad_year <> ''
                    ORDER BY acad_year DESC LIMIT 20", conn))
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        string v = rdr.IsDBNull(0) ? "" : rdr.GetString(0);
                        if (!string.IsNullOrEmpty(v)) years.Add(v);
                    }
                }
            }
            catch { }
            HttpRuntime.Cache.Insert(CacheKeyYears, years, null,
                DateTime.UtcNow.AddSeconds(90), System.Web.Caching.Cache.NoSlidingExpiration);
        }

        var sb = new StringBuilder();
        foreach (string v in years)
        {
            string sel = string.Equals(FilterYear, v, StringComparison.OrdinalIgnoreCase) ? " selected" : "";
            sb.AppendFormat("<option value=\"{0}\"{1}>{0}</option>", HE(v), sel);
        }
        litYearOpts.Text = sb.ToString();
    }

    // ── Role-based data scope (admin=all, dean=faculty, HOD=department) ──
    // A mark request has no prog_id of its own, so its programme is derived from its
    // course registration (course_reg_id → acad_course_registration.prog_id), falling
    // back to the student's programme. Aliased as `r` on the requests table.
    private const string ProgExpr =
        "COALESCE((SELECT cr2.prog_id FROM campus_dynamics_portal.acad_course_registration cr2 WHERE cr2.id=r.course_reg_id LIMIT 1)," +
        "(SELECT s2.progid FROM acad_student s2 WHERE s2.regno=r.regno LIMIT 1))";

    private const string SCOPE_DENIED = "{\"success\":false,\"message\":\"You can only act on requests within your faculty/department.\"}";

    private static MarksScope CurrentScope()
    {
        HttpContext ctx = HttpContext.Current;
        if (ctx != null && ctx.Items["__mraScope"] is MarksScope) return (MarksScope)ctx.Items["__mraScope"];
        MarksScope s = MarksScopeResolver.Resolve();
        if (ctx != null) ctx.Items["__mraScope"] = s;
        return s;
    }

    // Who may MANAGE (approve/reject/etc.) mark requests: super admin, a Dean (faculty),
    // or a Head of Department. General departmental staff get read-only (scoped) access.
    private static bool CanManageRequests()
    {
        MarksScope s = CurrentScope();
        if (s.IsAdmin) return true;
        if (s.Mode == "faculty") return true;                                          // Dean
        if (s.Mode == "department" && s.RoleNote == "Head of Department") return true;  // HOD
        return false;
    }

    // True if a mark request (by id) belongs to a programme within the user's scope.
    private static bool RequestInScope(MySqlConnection conn, int id)
    {
        MarksScope s = CurrentScope();
        if (s.IsAdmin || s.AllowedProgCodes == null) return true;
        string prog = "";
        using (var cmd = new MySqlCommand(
            "SELECT " + ProgExpr + " FROM campus_dynamics_portal.acad_marks_requests r WHERE r.id=@id LIMIT 1", conn))
        {
            cmd.Parameters.AddWithValue("@id", id);
            object v = cmd.ExecuteScalar();
            if (v != null && v != DBNull.Value) prog = v.ToString().Trim();
        }
        return s.AllowsProg(prog);
    }

    // ── Stats bar (scoped to year/sem/type only — NOT filtered by status/search) ──
    private void LoadStats(MySqlConnection conn)
    {
        var where = new StringBuilder("WHERE 1=1");
        var parms = new List<MySqlParameter>();
        if (!string.IsNullOrEmpty(FilterYear)) { where.Append(" AND r.acad_year=@ay");    parms.Add(new MySqlParameter("@ay",  FilterYear)); }
        if (!string.IsNullOrEmpty(FilterSem))  { where.Append(" AND r.semester=@sem");   parms.Add(new MySqlParameter("@sem", FilterSem));  }
        if (!string.IsNullOrEmpty(FilterType)) { where.Append(" AND r.request_type=@rt"); parms.Add(new MySqlParameter("@rt", FilterType)); }
        where.Append(CurrentScope().ProgFilterExpr(ProgExpr)); // role-based scope

        int tot = 0, pl = 0, ps = 0, pa = 0, ap = 0, rj = 0, cn = 0;
        try
        {
            using (var cmd = new MySqlCommand(@"
                SELECT
                    COUNT(*) AS tot,
                    SUM(CASE WHEN status='PENDING_LECTURER'   THEN 1 ELSE 0 END) AS pl,
                    SUM(CASE WHEN status='PENDING_SUPERVISOR' THEN 1 ELSE 0 END) AS ps,
                    SUM(CASE WHEN status='PENDING_ADMIN'      THEN 1 ELSE 0 END) AS pa,
                    SUM(CASE WHEN status='APPROVED'           THEN 1 ELSE 0 END) AS ap,
                    SUM(CASE WHEN status='REJECTED'           THEN 1 ELSE 0 END) AS rj,
                    SUM(CASE WHEN status='CANCELLED'          THEN 1 ELSE 0 END) AS cn
                FROM campus_dynamics_portal.acad_marks_requests r " + where, conn))
            {
                AddParams(cmd, parms);
                using (var rdr = cmd.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        tot = N(rdr, "tot"); pl = N(rdr, "pl"); ps = N(rdr, "ps");
                        pa  = N(rdr, "pa");  ap = N(rdr, "ap"); rj = N(rdr, "rj"); cn = N(rdr, "cn");
                    }
                }
            }
        }
        catch { /* show zeros on error */ }

        litStats.Text = string.Format(@"
<div class=""mra-grid"">
  <div class=""mra-stat""><div class=""mra-stat__lbl"">Total</div><div class=""mra-stat__val"">{0}</div><div class=""mra-stat__sub"">All requests in scope</div></div>
  <div class=""mra-stat""><div class=""mra-stat__lbl"">Pending Admin</div><div class=""mra-stat__val"" style=""color:#5b21b6"">{1}</div><div class=""mra-stat__sub"">Requires immediate action</div></div>
  <div class=""mra-stat""><div class=""mra-stat__lbl"">Pending Supervisor/Lecturer</div><div class=""mra-stat__val"">{2}</div><div class=""mra-stat__sub"">Stuck before admin stage</div></div>
  <div class=""mra-stat""><div class=""mra-stat__lbl"">Approved</div><div class=""mra-stat__val"" style=""color:#2e7d32"">{3}</div><div class=""mra-stat__sub"">Resolved as approved</div></div>
  <div class=""mra-stat""><div class=""mra-stat__lbl"">Rejected + Cancelled</div><div class=""mra-stat__val"" style=""color:#b42318"">{4}</div><div class=""mra-stat__sub"">Closed without approval</div></div>
</div>", tot, pa, pl + ps, ap, rj + cn);
    }

    // ── Main list (paginated, fully filtered) ────────────────────────────────
    private void LoadRows(MySqlConnection conn)
    {
        string nameExpr      = NameExpr;
        bool   needNameJoin  = NeedStudentJoin;

        var where = new StringBuilder("WHERE 1=1");
        var parms = new List<MySqlParameter>();
        if (!string.IsNullOrEmpty(FilterYear))   { where.Append(" AND r.acad_year=@ay");    parms.Add(new MySqlParameter("@ay",  FilterYear));   }
        if (!string.IsNullOrEmpty(FilterSem))    { where.Append(" AND r.semester=@sem");    parms.Add(new MySqlParameter("@sem", FilterSem));    }
        if (!string.IsNullOrEmpty(FilterType))   { where.Append(" AND r.request_type=@rt"); parms.Add(new MySqlParameter("@rt",  FilterType));   }
        if (!string.IsNullOrEmpty(FilterStatus)) { where.Append(" AND r.status=@sf");       parms.Add(new MySqlParameter("@sf",  FilterStatus)); }
        where.Append(CurrentScope().ProgFilterExpr(ProgExpr)); // role-based scope (admin=all, dean=faculty, HOD=dept)
        bool hasQ = !string.IsNullOrEmpty(FilterQ);
        if (hasQ) parms.Add(new MySqlParameter("@q", "%" + FilterQ + "%"));

        string searchExtra = hasQ
            ? " AND (r.regno LIKE @q OR r.course_id LIKE @q" + (needNameJoin ? " OR " + nameExpr + " LIKE @q" : "") + ")"
            : "";
        string fullWhere = where + searchExtra;

        // ── Lean COUNT — skip all joins except student (only when name-searching) ──
        int totalCount = 0;
        string countJoin = (hasQ && needNameJoin) ? " LEFT JOIN acad_student s ON s.regno = r.regno" : "";
        using (var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM campus_dynamics_portal.acad_marks_requests r" + countJoin + " " + fullWhere, conn))
        {
            cmd.CommandTimeout = 30;
            AddParams(cmd, parms);
            object o = cmd.ExecuteScalar();
            int.TryParse(o != null ? o.ToString() : "0", out totalCount);
        }

        // ── Paginated data query ─────────────────────────────────────────────
        int offset = (FilterPage - 1) * FilterSize;
        var sbRows = new StringBuilder();

        string studentJoin = needNameJoin ? " LEFT JOIN acad_student s ON s.regno = r.regno" : "";

        string sql = @"
            SELECT r.id, r.regno,
                " + nameExpr + @" AS student_name,
                r.course_id,
                IFNULL(c.courseName, r.course_id) AS course_name,
                r.acad_year, r.semester,
                r.request_type, r.status,
                IFNULL(r.student_reason,'')       AS student_reason,
                IFNULL(r.lecturer_response,'')    AS lecturer_response,
                IFNULL(r.supervisor_response,'')  AS supervisor_response,
                IFNULL(r.admin_response,'')       AS admin_response,
                r.proposed_cw, r.proposed_exam, r.proposed_total,
                cr.provisional_course_work_marks  AS orig_cw,
                cr.provisional_exam_marks         AS orig_exam,
                IFNULL(cr.provisional_total_marks, r.proposed_total) AS orig_total,
                IFNULL(le.emp_name,'')            AS lecturer_name,
                IFNULL(r.assigned_lecturer_id,0)  AS assigned_lid,
                IFNULL(r.lecturer_id,0)           AS default_lid,
                IFNULL(se.emp_name,'')            AS supervisor_name,
                DATE_FORMAT(r.created_at,'%d %b %Y %H:%i') AS created_at,
                DATE_FORMAT(r.updated_at,'%d %b %Y %H:%i') AS updated_at,
                IFNULL(r.admin_username,'')       AS admin_username
            FROM campus_dynamics_portal.acad_marks_requests r"
            + studentJoin + @"
            LEFT JOIN acad_course c   ON c.courseID  = r.course_id
            LEFT JOIN campus_dynamics_portal.acad_course_registration cr ON cr.id = r.course_reg_id
            LEFT JOIN hrm_employee le ON le.empID = COALESCE(r.assigned_lecturer_id, r.lecturer_id)
            LEFT JOIN hrm_employee se ON se.empID = r.supervisor_id
            " + fullWhere + @"
            ORDER BY
                CASE r.status
                    WHEN 'PENDING_ADMIN'      THEN 0
                    WHEN 'PENDING_SUPERVISOR' THEN 1
                    WHEN 'PENDING_LECTURER'   THEN 2
                    WHEN 'APPROVED'           THEN 3
                    WHEN 'REJECTED'           THEN 4
                    ELSE 5
                END,
                r.created_at DESC
            LIMIT @sz OFFSET @off";

        using (var cmd = new MySqlCommand(sql, conn))
        {
            cmd.CommandTimeout = 30;
            AddParams(cmd, parms);
            cmd.Parameters.AddWithValue("@sz",  FilterSize);
            cmd.Parameters.AddWithValue("@off", offset);
            using (var rdr = cmd.ExecuteReader())
            {
                int rowNum = offset + 1;
                while (rdr.Read()) sbRows.Append(RenderRow(rdr, rowNum++));
            }
        }

        TotalCount = totalCount;
        litCount.Text = totalCount.ToString("N0");

        litRows.Text = sbRows.Length > 0
            ? sbRows.ToString()
            : "<tr><td colspan=\"9\" class=\"mra-note\" style=\"padding:24px;text-align:center;\">No mark requests found for the selected filters.</td></tr>";

        int totalPages = totalCount > 0 ? (int)Math.Ceiling((double)totalCount / FilterSize) : 1;
        litPager.Text = RenderPager(FilterPage, totalPages);
    }

    // ── Reuse a parameter list across multiple commands ───────────────────────
    private static void AddParams(MySqlCommand cmd, List<MySqlParameter> parms)
    {
        foreach (var p in parms)
            cmd.Parameters.AddWithValue(p.ParameterName, p.Value);
    }

    private string RenderRow(System.Data.IDataReader rdr, int rowNum)
    {
        string id       = S(rdr, "id");
        string status   = S(rdr, "status").ToUpperInvariant();
        string name     = S(rdr, "student_name");
        string regno    = S(rdr, "regno");
        string courseN  = S(rdr, "course_name");
        string courseI  = S(rdr, "course_id");
        string year     = S(rdr, "acad_year");
        string sem      = S(rdr, "semester");
        string rtype    = S(rdr, "request_type");
        string created  = S(rdr, "created_at");
        string studRsn  = S(rdr, "student_reason");
        string lecResp  = S(rdr, "lecturer_response");
        string supResp  = S(rdr, "supervisor_response");
        string admResp  = S(rdr, "admin_response");
        string lecName  = S(rdr, "lecturer_name");
        string supName  = S(rdr, "supervisor_name");
        string admName  = S(rdr, "admin_username");
        string assignedLid = S(rdr, "assigned_lid");
        string defaultLid  = S(rdr, "default_lid");
        bool   isAssigned  = assignedLid.Length > 0 && assignedLid != "0";
        string effLid      = isAssigned ? assignedLid : defaultLid;
        string origCw   = S(rdr, "orig_cw");
        string origEx   = S(rdr, "orig_exam");
        string origTot  = S(rdr, "orig_total");
        int origTotN; string origGr = int.TryParse(origTot, out origTotN) && origTotN > 0 ? CalcGrade(origTotN) : "";
        string propCw   = S(rdr, "proposed_cw");
        string propEx   = S(rdr, "proposed_exam");
        string propTot  = S(rdr, "proposed_total");

        bool canAct    = status == "PENDING_LECTURER" || status == "PENDING_SUPERVISOR" || status == "PENDING_ADMIN";
        bool canReopen = status == "APPROVED" || status == "REJECTED" || status == "CANCELLED";

        string pillCls  = "mra-pill mra-pill--" + status;
        string stDisp   = (status ?? "").Replace("_", " ");
        string chipCls  = (rtype == "MISSING_MARK") ? "mra-chip mra-chip--missing" : "mra-chip mra-chip--change";
        string typeDisp = (rtype == "MISSING_MARK") ? "Missing" : "Change";

        // data-search: name + regno + courseId + courseName (lowercased, space-joined)
        string searchVal = (name + " " + regno + " " + courseI + " " + courseN).ToLowerInvariant();

        var sb = new StringBuilder();
        sb.AppendFormat(
            "<tr data-id=\"{0}\" data-status=\"{1}\" data-name=\"{2}\" data-regno=\"{3}\" data-course=\"{4}\" data-search=\"{5}\" data-pcw=\"{6}\" data-pex=\"{7}\" data-ocw=\"{8}\" data-oex=\"{9}\" data-courseid=\"{10}\" data-year=\"{11}\" data-sem=\"{12}\" data-lecturerid=\"{13}\">",
            HE(id), HE(status), HE(name.Length > 0 ? name : regno), HE(regno),
            HE(courseN.Length > 0 ? courseN : courseI),
            HE(searchVal), HE(propCw), HE(propEx), HE(origCw), HE(origEx),
            HE(courseI), HE(year), HE(sem), HE(effLid));

        // col 1: checkbox
        sb.AppendFormat("<td style=\"text-align:center;\"><input type=\"checkbox\" class=\"mra-row-chk mra-chk\" value=\"{0}\"/></td>", HE(id));

        // col 2: ID
        sb.AppendFormat("<td><span style=\"font-size:10px;font-weight:800;color:#64748b;\">#{0}</span></td>", HE(id));

        // col 3: Student — name bold + regno code style
        sb.AppendFormat(
            "<td><div class=\"mra-strong\" style=\"margin-bottom:2px;\">{0}</div><div><code class=\"mra-code\">{1}</code></div></td>",
            HE(name.Length > 0 ? name : regno), HE(regno));

        // col 4: Course — name + code + Yr/Sem
        sb.AppendFormat(
            "<td><div style=\"font-size:10px;font-weight:700;color:#1f2937;line-height:1.35;margin-bottom:2px;\">{0}</div>" +
            "<div class=\"mra-note\">{1}</div>" +
            "<div class=\"mra-note\">Yr {2} / Sem {3}</div></td>",
            HE(courseN.Length > 0 ? courseN : courseI), HE(courseI), HE(year), HE(sem));

        // col 5: Lecturer — the effective (assigned overrides the course default) lecturer
        string lecCell  = lecName.Length > 0 ? HE(lecName) : "&mdash;";
        string lecBadge = isAssigned
            ? "<span class=\"mra-lec-badge mra-lec-badge--assigned\">assigned</span>"
            : (lecName.Length > 0 ? "<span class=\"mra-lec-badge\">course default</span>" : "");
        sb.AppendFormat(
            "<td><div class=\"mra-strong\" style=\"font-size:11px;line-height:1.3;\">{0}</div><div style=\"margin-top:3px;\">{1}</div></td>",
            lecCell, lecBadge);

        // col 6: Type chip + status pill
        sb.AppendFormat(
            "<td><div style=\"margin-bottom:3px;\"><span class=\"{0}\">{1}</span></div>" +
            "<div><span class=\"{2}\">{3}</span></div>" +
            "<div class=\"mra-note\" style=\"margin-top:3px;\">Created:<br/>{4}</div></td>",
            HE(chipCls), HE(typeDisp), HE(pillCls), HE(stDisp), HE(created));

        // col 6: Marks — Original vs Proposed mini boxes side by side
        sb.Append("<td><div class=\"mra-marks-compare\">");
        sb.Append("<div class=\"mra-mark-box\">");
        sb.Append("<div class=\"mra-mark-box__lbl\">Original</div>");
        sb.AppendFormat("<div class=\"mra-mark-box__row\">CW&nbsp;<span>{0}</span></div>", Dash(origCw));
        sb.AppendFormat("<div class=\"mra-mark-box__row\">Ex&nbsp;<span>{0}</span></div>", Dash(origEx));
        sb.AppendFormat("<div class=\"mra-mark-box__row\">Tot&nbsp;<span>{0}</span></div>", Dash(origTot));
        sb.AppendFormat("<div class=\"mra-mark-box__row\">Gr&nbsp;<span>{0}</span></div>", Dash(origGr));
        sb.Append("</div>");
        sb.Append("<div class=\"mra-mark-box\">");
        sb.Append("<div class=\"mra-mark-box__lbl\">Proposed</div>");
        sb.AppendFormat("<div class=\"mra-mark-box__row\">CW&nbsp;<span>{0}</span></div>", Dash(propCw));
        sb.AppendFormat("<div class=\"mra-mark-box__row\">Ex&nbsp;<span>{0}</span></div>", Dash(propEx));
        sb.AppendFormat("<div class=\"mra-mark-box__row\">Tot&nbsp;<span>{0}</span></div>", Dash(propTot));
        sb.Append("</div>");
        sb.Append("</div></td>");

        // col 7: Trail
        sb.Append("<td>");
        if (studRsn.Length > 0)
        {
            string preview = studRsn.Length > 120 ? studRsn.Substring(0, 117) + "..." : studRsn;
            sb.AppendFormat("<div class=\"mra-trail-quote\">{0}</div>", HE(preview));
        }
        if (lecResp.Length > 0)
            sb.AppendFormat("<div class=\"mra-trail-resp\"><b>Lec:</b> {0}</div>", HE(lecResp));
        if (supResp.Length > 0)
            sb.AppendFormat("<div class=\"mra-trail-resp\"><b>Sup:</b> {0}</div>", HE(supResp));
        if (admResp.Length > 0)
            sb.AppendFormat("<div class=\"mra-trail-resp\"><b>Admin:</b> {0}</div>", HE(admResp));
        if (studRsn.Length == 0 && lecResp.Length == 0 && supResp.Length == 0 && admResp.Length == 0)
            sb.Append("<div class=\"mra-note\">-</div>");
        // actors line
        var actors = new System.Collections.Generic.List<string>();
        if (supName.Length > 0) actors.Add("Sup: " + supName);
        if (admName.Length > 0) actors.Add("Admin: " + admName);
        if (actors.Count > 0)
            sb.AppendFormat("<div class=\"mra-trail-actors\">{0}</div>", HE(string.Join(" | ", actors.ToArray())));
        sb.Append("</td>");

        // col 8: Actions dropdown
        sb.AppendFormat("<td style=\"text-align:right;\"><div class=\"mra-menu-wrap\" id=\"mw-{0}\">", HE(id));
        sb.AppendFormat("<button type=\"button\" class=\"mra-btn mra-btn--sm\" onclick=\"toggleMenu('mw-{0}')\">Actions &#9660;</button>", HE(id));
        sb.Append("<div class=\"mra-menu\">");
        sb.AppendFormat("<button type=\"button\" class=\"mra-menu-item\" onclick=\"openDetail('{0}')\">&#128065; View Details</button>", HE(id));
        if (canAct)
        {
            sb.AppendFormat("<button type=\"button\" class=\"mra-menu-item\" onclick=\"openModal('approve','{0}')\">&#10003; Approve</button>", HE(id));
            sb.AppendFormat("<button type=\"button\" class=\"mra-menu-item\" onclick=\"openModal('reject','{0}')\">&#10007; Reject</button>", HE(id));
        }
        sb.AppendFormat("<button type=\"button\" class=\"mra-menu-item mra-menu-item--warn\" onclick=\"openModal('marks','{0}')\">&#9998; Update Marks</button>", HE(id));
        sb.AppendFormat("<button type=\"button\" class=\"mra-menu-item\" onclick=\"openLecturerModal('{0}')\">&#128100; Change Lecturer</button>", HE(id));
        sb.AppendFormat("<button type=\"button\" class=\"mra-menu-item\" onclick=\"openModal('status','{0}')\">&#8646; Change Status</button>", HE(id));
        if (canReopen)
        {
            sb.AppendFormat("<button type=\"button\" class=\"mra-menu-item\" onclick=\"openModal('reopen','{0}')\">&#8635; Reopen</button>", HE(id));
        }
        sb.AppendFormat("<button type=\"button\" class=\"mra-menu-item mra-menu-item--danger\" onclick=\"openModal('force','{0}')\">&#8855; Force Close</button>", HE(id));
        sb.Append("</div></div></td>");
        sb.Append("</tr>");
        return sb.ToString();
    }

    private string RenderPager(int page, int totalPages)
    {
        if (totalPages <= 1) return "";
        var sb = new StringBuilder();
        sb.Append("<div class=\"mra-pager\">");
        sb.AppendFormat("<span>Page {0} of {1} &nbsp;&mdash;&nbsp; {2} records</span>", page, totalPages, TotalCount.ToString("N0"));
        sb.Append("<div class=\"mra-pager__nav\">");
        if (page > 1)
            sb.AppendFormat("<a href=\"{0}\" class=\"mra-btn\">&lsaquo; Prev</a>", HE(PageUrl(page - 1)));

        int from = Math.Max(1, page - 2);
        int to   = Math.Min(totalPages, from + 4);
        from = Math.Max(1, to - 4);
        for (int p = from; p <= to; p++)
        {
            if (p == page)
                sb.AppendFormat("<span class=\"mra-btn mra-btn--cur\">{0}</span>", p);
            else
                sb.AppendFormat("<a href=\"{0}\" class=\"mra-btn\">{1}</a>", HE(PageUrl(p)), p);
        }
        if (page < totalPages)
            sb.AppendFormat("<a href=\"{0}\" class=\"mra-btn\">Next &rsaquo;</a>", HE(PageUrl(page + 1)));

        sb.Append("</div></div>");
        return sb.ToString();
    }

    // ── Helper: display dash for empty values ────────────────────────────────
    private string Dash(string v)
    {
        return string.IsNullOrEmpty(v) ? "-" : HE(v);
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  AUDIT HELPERS
    // ════════════════════════════════════════════════════════════════════════════

    private static string GetClientIp()
    {
        try
        {
            var ctx = HttpContext.Current;
            if (ctx == null) return "unknown";
            string ip = ctx.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            if (!string.IsNullOrEmpty(ip)) { int c = ip.IndexOf(','); if (c >= 0) ip = ip.Substring(0, c); ip = ip.Trim(); }
            if (string.IsNullOrEmpty(ip) || ip == "::1") ip = ctx.Request.UserHostAddress ?? "unknown";
            return ip.Trim();
        }
        catch { return "unknown"; }
    }

    // Records who/where/why for the acad_results audit trigger, keyed by CONNECTION_ID().
    // Connector-safe (normal params, no user vars). Best-effort — never breaks the write.
    private static void SetMarkAuditContext(MySqlConnection conn, MySqlTransaction tx, string actor, string source, string reason)
    {
        try
        {
            using (var cmd = new MySqlCommand(
                "REPLACE INTO campus_dynamics.mark_audit_context (conn_id, actor, source, reason, ip, set_at) " +
                "VALUES (CONNECTION_ID(), @a, @s, @r, @ip, NOW())", conn, tx))
            {
                cmd.Parameters.AddWithValue("@a", (actor ?? "").Length > 90 ? actor.Substring(0, 90) : (actor ?? ""));
                cmd.Parameters.AddWithValue("@s", (source ?? "").Length > 100 ? source.Substring(0, 100) : (source ?? ""));
                cmd.Parameters.AddWithValue("@r", (reason ?? "").Length > 200 ? reason.Substring(0, 200) : (reason ?? ""));
                cmd.Parameters.AddWithValue("@ip", GetClientIp());
                cmd.ExecuteNonQuery();
            }
        }
        catch { /* attribution is best-effort; must never break the mark write */ }
    }

    // Best-effort write to acad_activity_log — uses a separate connection so it
    // never interferes with the caller's transaction and never throws to the caller.
    private static void WriteAuditLog(string userId, string pageFunction, string par, string comments = "")
    {
        try
        {
            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "INSERT INTO acad_activity_log (user_id, page_function, par, comments, access_date) " +
                    "VALUES (@u,@pf,@par,@cm,NOW())", conn))
                {
                    cmd.Parameters.AddWithValue("@u",   (userId ?? "admin").Trim());
                    cmd.Parameters.AddWithValue("@pf",  pageFunction);
                    cmd.Parameters.AddWithValue("@par", par);
                    cmd.Parameters.AddWithValue("@cm",  comments);
                    cmd.ExecuteNonQuery();
                }
            }
        }
        catch { /* audit logging is best-effort and must never fail the caller */ }
    }

    // Build a par string in the format that both audit trail parsers expect.
    // Fields are space-separated with clear keys so ExtractBetween() can parse them.
    private static string BuildAuditPar(string regno, string courseId, string acadYear, int semester,
        int? oldCw, int? newCw, int? oldExam, int? newExam,
        int? oldScore, int? newScore, string oldGrade, string newGrade,
        int requestId, string action, string note)
    {
        return string.Format(
            "Student: {0} Course: {1} Academic Year: {2} Semester: {3} " +
            "Old CourseWork Mark: {4} New CourseWork: {5} " +
            "Old Exam Mark: {6} New Exam Mark: {7} " +
            "Old Score: {8} New Score: {9} Old Grade: {10} New Grade: {11} " +
            "Request#: {12} Action: {13} Note: {14} IP Address: {15}",
            regno, courseId, acadYear, semester,
            NullableStr(oldCw), NullableStr(newCw), NullableStr(oldExam), NullableStr(newExam),
            NullableStr(oldScore), NullableStr(newScore),
            string.IsNullOrEmpty(oldGrade) ? "-" : oldGrade,
            string.IsNullOrEmpty(newGrade) ? "-" : newGrade,
            requestId, action,
            string.IsNullOrEmpty(note) ? "-" : note,
            GetClientIp());
    }

    private static string NullableStr(int? v) { return v.HasValue ? v.Value.ToString() : "-"; }

    // Lightweight context fetch for audit — used by Reject/ForceClose/Reopen
    // which don't otherwise read the full row.
    private static void FetchRequestContext(MySqlConnection conn, int requestId,
        out string regno, out string courseId, out string acadYear, out int semester)
    {
        regno = ""; courseId = ""; acadYear = ""; semester = 0;
        try
        {
            using (var cmd = new MySqlCommand(
                "SELECT regno, course_id, acad_year, semester " +
                "FROM campus_dynamics_portal.acad_marks_requests WHERE id=@id LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@id", requestId);
                using (var rdr = cmd.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        regno    = S(rdr, "regno");
                        courseId = S(rdr, "course_id");
                        acadYear = S(rdr, "acad_year");
                        int.TryParse(S(rdr, "semester"), out semester);
                    }
                }
            }
        }
        catch { }
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  AUTH
    // ════════════════════════════════════════════════════════════════════════════

    private static bool IsAdmin()
    {
        return true;
    }

    private static string AdminUser()
    {
        var ctx = HttpContext.Current;
        if (ctx == null) return "admin";
        string userName = "";
        if (ctx.User != null && ctx.User.Identity != null && !string.IsNullOrEmpty(ctx.User.Identity.Name))
            userName = ctx.User.Identity.Name;
        else if (ctx.Session != null && ctx.Session["regno"] != null)
            userName = ctx.Session["regno"].ToString();
        if (string.IsNullOrEmpty(userName)) userName = "admin";
        return userName.Trim();
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  VIEW DETAILS — full record for the detail modal
    // ════════════════════════════════════════════════════════════════════════════
    [WebMethod(EnableSession = true)]
    public static string AdminGetRequestDetail(int requestId)
    {
        try
        {
            if (!CanManageRequests()) return Json.Serialize(new { success = false, message = "Access denied — outside your faculty/department." });
            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                if (!RequestInScope(conn, requestId)) return Json.Serialize(new { success = false, message = "This request is outside your faculty/department." });
                EnsureNameCol(conn);
                string sJoin = NeedStudentJoin ? " LEFT JOIN acad_student s ON s.regno = r.regno" : "";
                using (var cmd = new MySqlCommand(
                    "SELECT r.id, r.regno, " + NameExpr + " AS student_name, r.course_id, " +
                    "IFNULL(c.courseName, r.course_id) AS course_name, r.acad_year, r.semester, r.request_type, r.status, " +
                    "IFNULL(r.student_reason,'') student_reason, IFNULL(r.lecturer_response,'') lecturer_response, " +
                    "IFNULL(r.supervisor_response,'') supervisor_response, IFNULL(r.admin_response,'') admin_response, " +
                    "r.proposed_cw, r.proposed_exam, r.proposed_total, " +
                    "cr.provisional_course_work_marks orig_cw, cr.provisional_exam_marks orig_exam, " +
                    "IFNULL(cr.provisional_total_marks, r.proposed_total) orig_total, " +
                    "IFNULL(r.assigned_lecturer_id,0) assigned_lid, " +
                    "IFNULL(le.emp_name,'') lecturer_name, IFNULL(le.emp_email,'') lecturer_email, " +
                    "IFNULL(dl.emp_name,'') default_name, IFNULL(se.emp_name,'') supervisor_name, IFNULL(r.admin_username,'') admin_username, " +
                    "DATE_FORMAT(r.created_at,'%d %b %Y %H:%i') created_at, DATE_FORMAT(r.updated_at,'%d %b %Y %H:%i') updated_at, " +
                    "DATE_FORMAT(r.lecturer_responded_at,'%d %b %Y %H:%i') lec_at, " +
                    "DATE_FORMAT(r.supervisor_responded_at,'%d %b %Y %H:%i') sup_at, " +
                    "DATE_FORMAT(r.admin_responded_at,'%d %b %Y %H:%i') adm_at " +
                    "FROM campus_dynamics_portal.acad_marks_requests r" + sJoin + " " +
                    "LEFT JOIN acad_course c ON c.courseID = r.course_id " +
                    "LEFT JOIN campus_dynamics_portal.acad_course_registration cr ON cr.id = r.course_reg_id " +
                    "LEFT JOIN hrm_employee le ON le.empID = COALESCE(r.assigned_lecturer_id, r.lecturer_id) " +
                    "LEFT JOIN hrm_employee dl ON dl.empID = r.lecturer_id " +
                    "LEFT JOIN hrm_employee se ON se.empID = r.supervisor_id " +
                    "WHERE r.id=@id LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@id", requestId);
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read()) return Json.Serialize(new { success = false, message = "Request not found." });
                        string aLid = S(rd, "assigned_lid");
                        bool isAssigned = aLid.Length > 0 && aLid != "0";
                        int pt; string propTot = S(rd, "proposed_total"); string propGr = int.TryParse(propTot, out pt) && pt > 0 ? CalcGrade(pt) : "";
                        int ot; string origTot = S(rd, "orig_total"); string origGr = int.TryParse(origTot, out ot) && ot > 0 ? CalcGrade(ot) : "";
                        return Json.Serialize(new
                        {
                            success = true,
                            id = S(rd, "id"), regno = S(rd, "regno"), student = S(rd, "student_name"),
                            course = S(rd, "course_id"), course_name = S(rd, "course_name"),
                            year = S(rd, "acad_year"), sem = S(rd, "semester"),
                            type = S(rd, "request_type"), status = S(rd, "status"),
                            student_reason = S(rd, "student_reason"), lecturer_response = S(rd, "lecturer_response"),
                            supervisor_response = S(rd, "supervisor_response"), admin_response = S(rd, "admin_response"),
                            orig_cw = S(rd, "orig_cw"), orig_exam = S(rd, "orig_exam"), orig_total = origTot, orig_grade = origGr,
                            prop_cw = S(rd, "proposed_cw"), prop_exam = S(rd, "proposed_exam"), prop_total = propTot, prop_grade = propGr,
                            lecturer_name = S(rd, "lecturer_name"), lecturer_email = S(rd, "lecturer_email"),
                            default_name = S(rd, "default_name"), is_assigned = isAssigned,
                            supervisor_name = S(rd, "supervisor_name"), admin_username = S(rd, "admin_username"),
                            created_at = S(rd, "created_at"), updated_at = S(rd, "updated_at"),
                            lec_at = S(rd, "lec_at"), sup_at = S(rd, "sup_at"), adm_at = S(rd, "adm_at")
                        });
                    }
                }
            }
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  LECTURER DIRECTORY — assigned + course-taught flagged, then all academic staff
    // ════════════════════════════════════════════════════════════════════════════
    [WebMethod(EnableSession = true)]
    public static string AdminGetLecturers(string courseId, string acadYear, int semester)
    {
        try
        {
            if (!CanManageRequests()) return Json.Serialize(new { success = false, message = "Access denied." });
            courseId = (courseId ?? "").Trim();
            var list = new System.Collections.Generic.List<object>();
            var seen = new System.Collections.Generic.HashSet<int>();
            int defaultId = 0;
            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                if (courseId.Length > 0)
                {
                    using (var cmd = new MySqlCommand(
                        "SELECT IFNULL(pc.lecturer_id,0) lid FROM acad_programmecourses pc " +
                        "WHERE pc.course_code=@c AND UPPER(IFNULL(pc.is_lecturere_assigned,'NO'))='YES' AND IFNULL(pc.lecturer_id,0)>0 LIMIT 1", conn))
                    {
                        cmd.Parameters.AddWithValue("@c", courseId);
                        using (var rd = cmd.ExecuteReader()) if (rd.Read()) int.TryParse(S(rd, "lid"), out defaultId);
                    }
                    using (var cmd = new MySqlCommand(
                        "SELECT ta.staffCode lid, IFNULL(e.emp_name, CONCAT('Staff #', ta.staffCode)) nm, " +
                        "MAX(CASE WHEN ta.acad_year=@ay AND ta.semester=@sem THEN 1 ELSE 0 END) st " +
                        "FROM acad_teaching_allocation ta LEFT JOIN hrm_employee e ON e.empID=ta.staffCode " +
                        "WHERE ta.courseID=@c AND IFNULL(ta.staffCode,'')<>'' GROUP BY ta.staffCode, nm ORDER BY st DESC, nm ASC LIMIT 100", conn))
                    {
                        cmd.Parameters.AddWithValue("@c", courseId);
                        cmd.Parameters.AddWithValue("@ay", acadYear ?? "");
                        cmd.Parameters.AddWithValue("@sem", semester);
                        using (var rd = cmd.ExecuteReader())
                            while (rd.Read())
                            {
                                int lid; if (!int.TryParse(S(rd, "lid"), out lid) || lid <= 0 || !seen.Add(lid)) continue;
                                list.Add(new { id = lid, name = S(rd, "nm"), is_default = (lid == defaultId), is_taught = true });
                            }
                    }
                }
                using (var cmd = new MySqlCommand(
                    "SELECT empID lid, IFNULL(emp_name, CONCAT('Staff #', empID)) nm FROM hrm_employee " +
                    "WHERE IFNULL(emp_name,'')<>'' ORDER BY emp_name ASC LIMIT 2000", conn))
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read())
                    {
                        int lid; if (!int.TryParse(S(rd, "lid"), out lid) || lid <= 0 || !seen.Add(lid)) continue;
                        list.Add(new { id = lid, name = S(rd, "nm"), is_default = (lid == defaultId), is_taught = false });
                    }
            }
            return Json.Serialize(new { success = true, lecturers = list });
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  SET / CHANGE the assigned lecturer (admin override → assigned_lecturer_id)
    // ════════════════════════════════════════════════════════════════════════════
    [WebMethod(EnableSession = true)]
    public static string AdminSetLecturer(int requestId, int lecturerId)
    {
        try
        {
            if (!CanManageRequests()) return Json.Serialize(new { success = false, message = "Access denied — outside your faculty/department." });
            if (lecturerId <= 0) return Json.Serialize(new { success = false, message = "Please pick a lecturer." });
            string adminUser = AdminUser();
            string lecName = "";
            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                if (!RequestInScope(conn, requestId)) return Json.Serialize(new { success = false, message = "This request is outside your faculty/department." });
                using (var cmd = new MySqlCommand("SELECT IFNULL(emp_name,'') n, COUNT(*) c FROM hrm_employee WHERE empID=@e", conn))
                {
                    cmd.Parameters.AddWithValue("@e", lecturerId);
                    using (var rd = cmd.ExecuteReader())
                        if (rd.Read()) { if (Convert.ToInt64(S(rd, "c")) == 0) return Json.Serialize(new { success = false, message = "That lecturer was not found." }); lecName = S(rd, "n"); }
                }
                int n;
                using (var cmd = new MySqlCommand(
                    "UPDATE campus_dynamics_portal.acad_marks_requests SET assigned_lecturer_id=@lid, admin_username=@au, updated_at=NOW() WHERE id=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@lid", lecturerId);
                    cmd.Parameters.AddWithValue("@au", adminUser);
                    cmd.Parameters.AddWithValue("@id", requestId);
                    n = cmd.ExecuteNonQuery();
                }
                if (n == 0) return Json.Serialize(new { success = false, message = "Request not found." });
            }
            WriteAuditLog(adminUser, "Mark Request Set Lecturer",
                "Request#: " + requestId + " Lecturer: " + lecturerId + " (" + lecName + ")",
                "Assigned lecturer set to " + (lecName.Length > 0 ? lecName : ("#" + lecturerId)) + " by " + adminUser + ". IP: " + GetClientIp());
            return Json.Serialize(new { success = true, lecturer_id = lecturerId, lecturer_name = lecName, message = "Lecturer set to " + (lecName.Length > 0 ? lecName : ("#" + lecturerId)) + " — the request now routes to them." });
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  ADMIN APPROVE
    // ════════════════════════════════════════════════════════════════════════════

    [WebMethod(EnableSession = true)]
    public static string AdminApprove(int requestId, string note, int? adminProposedCw, int? adminProposedExam)
    {
        try
        {
            if (!CanManageRequests()) return Json.Serialize(new { success = false, message = "Access denied — you can only manage requests within your faculty/department." });

            note = (note ?? "").Trim();
            string adminUser = AdminUser();

            string reqRegno = "", courseId = "", acadYear = "", requestType = "";
            int semester = 0, courseRegId = 0;
            int? proposedCw = null, proposedExam = null, proposedTotal = null;
            bool marksPublished = false;
            bool enteredJourney = false;
            int publishedTotal = 0;
            decimal semGpa = 0m, cgpa = 0m;

            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                if (!RequestInScope(conn, requestId)) return SCOPE_DENIED;
                using (var tx = conn.BeginTransaction())
                {
                    using (var cmd = new MySqlCommand(@"
                        SELECT regno, course_id, acad_year, semester, course_reg_id,
                               request_type, proposed_cw, proposed_exam, proposed_total
                        FROM campus_dynamics_portal.acad_marks_requests
                        WHERE id = @id
                          AND 1=1
                        LIMIT 1 FOR UPDATE", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@id", requestId);
                        using (var rdr = cmd.ExecuteReader())
                        {
                            if (!rdr.Read())
                            {
                                tx.Rollback();
                                return Json.Serialize(new { success = false, message = "Request not found or already resolved." });
                            }
                            reqRegno      = S(rdr, "regno");
                            courseId      = S(rdr, "course_id");
                            acadYear      = S(rdr, "acad_year");
                            int.TryParse(S(rdr, "semester"),     out semester);
                            int.TryParse(S(rdr, "course_reg_id"), out courseRegId);
                            requestType   = S(rdr, "request_type");
                            proposedCw    = NI(rdr, "proposed_cw");
                            proposedExam  = NI(rdr, "proposed_exam");
                            proposedTotal = NI(rdr, "proposed_total");
                        }
                    }

                    bool hasAdminOverrides = adminProposedCw.HasValue || adminProposedExam.HasValue;
                    if (hasAdminOverrides)
                    {
                        if (!adminProposedCw.HasValue || !adminProposedExam.HasValue)
                        {
                            tx.Rollback();
                            return Json.Serialize(new { success = false, message = "Please provide both CW and Exam marks." });
                        }
                        if (adminProposedCw.Value < 0 || adminProposedCw.Value > 100 ||
                            adminProposedExam.Value < 0 || adminProposedExam.Value > 100)
                        {
                            tx.Rollback();
                            return Json.Serialize(new { success = false, message = "CW and Exam marks must be between 0 and 100." });
                        }
                        proposedCw    = adminProposedCw;
                        proposedExam  = adminProposedExam;
                        proposedTotal = adminProposedCw.Value + adminProposedExam.Value;
                    }

                    bool canPublish = !string.IsNullOrEmpty(reqRegno)
                        && !string.IsNullOrEmpty(courseId)
                        && (proposedTotal.HasValue || (proposedCw.HasValue && proposedExam.HasValue));

                    // A MARK_CHANGE re-enters the marks journey (Capture → Approve → Publish)
                    // instead of being force-published here — same rule as the portal. Only
                    // MISSING_MARK (a mark that never existed) is published on approval.
                    if (string.Equals(requestType, "MARK_CHANGE", StringComparison.OrdinalIgnoreCase))
                    {
                        EnterChangeIntoJourney(conn, tx, requestId, courseRegId,
                            proposedCw, proposedExam, proposedTotal, "Admin: " + adminUser, note);
                        enteredJourney = true;
                    }
                    else if (canPublish)
                    {
                        PublishToResults(conn, tx, requestId, reqRegno, courseId, acadYear,
                            semester, courseRegId, proposedCw, proposedExam, proposedTotal,
                            "Admin: " + adminUser, note,
                            out publishedTotal, out semGpa, out cgpa);
                        marksPublished = true;
                    }

                    using (var cmd = new MySqlCommand(@"
                        UPDATE campus_dynamics_portal.acad_marks_requests
                        SET status             = 'APPROVED',
                            admin_username     = @au,
                            admin_response     = @note,
                            proposed_cw        = COALESCE(@pcw, proposed_cw),
                            proposed_exam      = COALESCE(@pex, proposed_exam),
                            proposed_total     = COALESCE(@ptot, proposed_total),
                            admin_responded_at = NOW(),
                            updated_at         = NOW()
                        WHERE id = @id", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@au",   adminUser);
                        cmd.Parameters.AddWithValue("@note", note);
                        cmd.Parameters.AddWithValue("@pcw",  proposedCw.HasValue   ? (object)proposedCw.Value   : DBNull.Value);
                        cmd.Parameters.AddWithValue("@pex",  proposedExam.HasValue  ? (object)proposedExam.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@ptot", proposedTotal.HasValue ? (object)proposedTotal.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@id",   requestId);
                        cmd.ExecuteNonQuery();
                    }

                    tx.Commit();
                }
            }

            WriteAuditLog(adminUser, "Mark Request Approve",
                BuildAuditPar(reqRegno, courseId, acadYear, semester,
                    proposedCw, proposedCw, proposedExam, proposedExam,
                    null, marksPublished ? (int?)publishedTotal : null,
                    null, null, requestId, "APPROVED", note),
                string.Format("Request #{0} approved. Marks published: {1}. SemGPA: {2:F2}. IP: {3}",
                    requestId, marksPublished, semGpa, GetClientIp()));

            return Json.Serialize(new
            {
                success          = true,
                marks_published  = marksPublished,
                entered_journey  = enteredJourney,
                published_total  = publishedTotal,
                semester_gpa     = semGpa,
                cgpa             = cgpa,
                message          = marksPublished
                    ? "Approved. Marks published. GPA/CGPA recalculated."
                    : (enteredJourney
                        ? "Approved. The new mark has been entered and now goes through the normal journey: Capture (HOD) → Approve (Dean) → Publish (Senate)."
                        : "Request approved. (No marks to publish — proposed marks missing.)")
            });
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  ADMIN REJECT
    // ════════════════════════════════════════════════════════════════════════════

    [WebMethod(EnableSession = true)]
    public static string AdminReject(int requestId, string reason)
    {
        try
        {
            if (!CanManageRequests()) return Json.Serialize(new { success = false, message = "Access denied — you can only manage requests within your faculty/department." });

            reason = (reason ?? "").Trim();
            if (reason.Length < 5) return Json.Serialize(new { success = false, message = "Please provide a reason (min 5 characters)." });

            string adminUser = AdminUser();
            string logRegno = "", logCourse = "", logYear = ""; int logSem = 0;
            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                if (!RequestInScope(conn, requestId)) return SCOPE_DENIED;
                FetchRequestContext(conn, requestId, out logRegno, out logCourse, out logYear, out logSem);
                using (var cmd = new MySqlCommand(@"
                    UPDATE campus_dynamics_portal.acad_marks_requests
                    SET status             = 'REJECTED',
                        admin_username     = @au,
                        admin_response     = @reason,
                        admin_responded_at = NOW(),
                        updated_at         = NOW()
                    WHERE id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@au",     adminUser);
                    cmd.Parameters.AddWithValue("@reason", reason);
                    cmd.Parameters.AddWithValue("@id",     requestId);
                    int n = cmd.ExecuteNonQuery();
                    if (n == 0) return Json.Serialize(new { success = false, message = "Request not found or already resolved." });
                }
            }
            WriteAuditLog(adminUser, "Mark Request Reject",
                BuildAuditPar(logRegno, logCourse, logYear, logSem,
                    null, null, null, null, null, null, null, null,
                    requestId, "REJECTED", reason),
                string.Format("Request #{0} rejected by {1}. Reason: {2}. IP: {3}",
                    requestId, adminUser, reason, GetClientIp()));
            return Json.Serialize(new { success = true, message = "Request rejected." });
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  ADMIN FORCE CLOSE
    // ════════════════════════════════════════════════════════════════════════════

    [WebMethod(EnableSession = true)]
    public static string AdminForceClose(int requestId, string reason)
    {
        try
        {
            if (!CanManageRequests()) return Json.Serialize(new { success = false, message = "Access denied — you can only manage requests within your faculty/department." });

            reason = (reason ?? "").Trim();
            if (reason.Length < 5) return Json.Serialize(new { success = false, message = "Please provide a reason (min 5 characters)." });

            string adminUser = AdminUser();
            string fcRegno = "", fcCourse = "", fcYear = ""; int fcSem = 0;
            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                if (!RequestInScope(conn, requestId)) return SCOPE_DENIED;
                FetchRequestContext(conn, requestId, out fcRegno, out fcCourse, out fcYear, out fcSem);
                using (var cmd = new MySqlCommand(@"
                    UPDATE campus_dynamics_portal.acad_marks_requests
                    SET status             = 'CANCELLED',
                        admin_username     = @au,
                        admin_response     = @reason,
                        admin_responded_at = NOW(),
                        updated_at         = NOW()
                    WHERE id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@au",     adminUser);
                    cmd.Parameters.AddWithValue("@reason", "FORCE CLOSED BY ADMIN: " + reason);
                    cmd.Parameters.AddWithValue("@id",     requestId);
                    int n = cmd.ExecuteNonQuery();
                    if (n == 0) return Json.Serialize(new { success = false, message = "Request not found." });
                }
            }
            WriteAuditLog(adminUser, "Mark Request Force Close",
                BuildAuditPar(fcRegno, fcCourse, fcYear, fcSem,
                    null, null, null, null, null, null, null, null,
                    requestId, "FORCE_CLOSED", reason),
                string.Format("Request #{0} force-closed by {1}. Reason: {2}. IP: {3}",
                    requestId, adminUser, reason, GetClientIp()));
            return Json.Serialize(new { success = true, message = "Request force-closed." });
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  ADMIN REOPEN
    // ════════════════════════════════════════════════════════════════════════════

    [WebMethod(EnableSession = true)]
    public static string AdminReopen(int requestId, string note)
    {
        try
        {
            if (!CanManageRequests()) return Json.Serialize(new { success = false, message = "Access denied — you can only manage requests within your faculty/department." });
            note = (note ?? "").Trim();
            string adminUser = AdminUser();
            string roRegno = "", roCourse = "", roYear = "", curStatus = ""; int roSem = 0;
            bool reverted = false; string sideMsg = ""; decimal semGpa = 0m, cgpa = 0m;
            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                if (!RequestInScope(conn, requestId)) return SCOPE_DENIED;
                using (var tx = conn.BeginTransaction())
                {
                    using (var cmd = new MySqlCommand(
                        "SELECT status, regno, course_id, acad_year, semester " +
                        "FROM campus_dynamics_portal.acad_marks_requests WHERE id=@id LIMIT 1 FOR UPDATE", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@id", requestId);
                        using (var rdr = cmd.ExecuteReader())
                        {
                            if (!rdr.Read()) { tx.Rollback(); return Json.Serialize(new { success = false, message = "Request not found." }); }
                            curStatus = S(rdr, "status").ToUpperInvariant();
                            roRegno = S(rdr, "regno"); roCourse = S(rdr, "course_id"); roYear = S(rdr, "acad_year");
                            int.TryParse(S(rdr, "semester"), out roSem);
                        }
                    }

                    // Reopening an APPROVED request must undo its published mark, or the
                    // transcript/GPA silently drifts. Auto-revert first (comment-based, safe).
                    if (curStatus == "APPROVED")
                        RevertPublishedResult(conn, tx, requestId, roRegno, roCourse, roYear, roSem,
                            "Admin: " + adminUser, out reverted, out sideMsg, out semGpa, out cgpa);

                    using (var cmd = new MySqlCommand(@"
                        UPDATE campus_dynamics_portal.acad_marks_requests
                        SET status='PENDING_ADMIN', admin_username=@au, admin_response=@note,
                            admin_responded_at=NOW(), updated_at=NOW()
                        WHERE id=@id", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@au", adminUser);
                        cmd.Parameters.AddWithValue("@note", note.Length > 0 ? note : "Reopened by admin.");
                        cmd.Parameters.AddWithValue("@id", requestId);
                        cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
            }
            WriteAuditLog(adminUser, "Mark Request Reopen",
                BuildAuditPar(roRegno, roCourse, roYear, roSem,
                    null, null, null, null, null, null, null, null,
                    requestId, "REOPENED", note),
                string.Format("Request #{0} reopened by {1}. Reverted:{2}. Note: {3}. IP: {4}",
                    requestId, adminUser, reverted, note, GetClientIp()));
            string rmsg = "Request reopened as Pending Admin.";
            if (curStatus == "APPROVED")
                rmsg += reverted ? " Prior mark auto-reverted; GPA/CGPA recalculated."
                                 : (" NOTE: " + (sideMsg.Length > 0 ? sideMsg : "published mark was not auto-reverted — verify manually."));
            return Json.Serialize(new { success = true, marks_reverted = reverted, semester_gpa = semGpa, cgpa = cgpa, message = rmsg });
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  ADMIN CHANGE STATUS — full manual override (any of the 6 states), robust:
    //    • → APPROVED  publishes proposed marks (if available) + recomputes GPA/CGPA
    //    • leaving APPROVED  auto-reverts the previously published mark + recomputes
    //    • every transition is audited
    // ════════════════════════════════════════════════════════════════════════════

    [WebMethod(EnableSession = true)]
    public static string AdminChangeStatus(int requestId, string newStatus, string note)
    {
        try
        {
            if (!CanManageRequests()) return Json.Serialize(new { success = false, message = "Access denied — you can only manage requests within your faculty/department." });

            newStatus = (newStatus ?? "").Trim().ToUpperInvariant();
            string[] valid = { "PENDING_LECTURER", "PENDING_SUPERVISOR", "PENDING_ADMIN", "APPROVED", "REJECTED", "CANCELLED" };
            if (Array.IndexOf(valid, newStatus) < 0)
                return Json.Serialize(new { success = false, message = "Invalid target status." });

            note = (note ?? "").Trim();
            if (note.Length < 3) return Json.Serialize(new { success = false, message = "Please provide a short reason/note (min 3 characters)." });

            string adminUser = AdminUser();
            string curStatus = "", regno = "", courseId = "", acadYear = "", reqType = "";
            int semester = 0, courseRegId = 0;
            int? pcw = null, pex = null, ptot = null;
            bool published = false, reverted = false, enteredJourney = false;
            int publishedTotal = 0;
            decimal semGpa = 0m, cgpa = 0m;
            string sideMsg = "";

            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                if (!RequestInScope(conn, requestId)) return SCOPE_DENIED;
                using (var tx = conn.BeginTransaction())
                {
                    using (var cmd = new MySqlCommand(@"
                        SELECT status, regno, course_id, acad_year, semester, course_reg_id,
                               request_type, proposed_cw, proposed_exam, proposed_total
                        FROM campus_dynamics_portal.acad_marks_requests WHERE id=@id LIMIT 1 FOR UPDATE", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@id", requestId);
                        using (var rdr = cmd.ExecuteReader())
                        {
                            if (!rdr.Read()) { tx.Rollback(); return Json.Serialize(new { success = false, message = "Request not found." }); }
                            curStatus = S(rdr, "status").ToUpperInvariant();
                            regno = S(rdr, "regno"); courseId = S(rdr, "course_id"); acadYear = S(rdr, "acad_year");
                            reqType = S(rdr, "request_type");
                            int.TryParse(S(rdr, "semester"), out semester);
                            int.TryParse(S(rdr, "course_reg_id"), out courseRegId);
                            pcw = NI(rdr, "proposed_cw"); pex = NI(rdr, "proposed_exam"); ptot = NI(rdr, "proposed_total");
                        }
                    }

                    if (curStatus == newStatus) { tx.Rollback(); return Json.Serialize(new { success = false, message = "Request is already " + Pretty(newStatus) + "." }); }

                    if (newStatus == "APPROVED")
                    {
                        bool canPublish = regno != "" && courseId != "" && (ptot.HasValue || (pcw.HasValue && pex.HasValue));
                        // Same rule as AdminApprove: a MARK_CHANGE re-enters the journey; only
                        // MISSING_MARK publishes on approval.
                        if (string.Equals(reqType, "MARK_CHANGE", StringComparison.OrdinalIgnoreCase))
                        {
                            EnterChangeIntoJourney(conn, tx, requestId, courseRegId, pcw, pex, ptot, "Admin: " + adminUser, note);
                            enteredJourney = true;
                        }
                        else if (canPublish)
                        {
                            PublishToResults(conn, tx, requestId, regno, courseId, acadYear, semester, courseRegId,
                                pcw, pex, ptot, "Admin: " + adminUser, note, out publishedTotal, out semGpa, out cgpa);
                            published = true;
                        }
                    }
                    else if (curStatus == "APPROVED")
                    {
                        RevertPublishedResult(conn, tx, requestId, regno, courseId, acadYear, semester,
                            "Admin: " + adminUser, out reverted, out sideMsg, out semGpa, out cgpa);
                    }

                    using (var cmd = new MySqlCommand(@"
                        UPDATE campus_dynamics_portal.acad_marks_requests
                        SET status=@st, admin_username=@au, admin_response=@note,
                            admin_responded_at=NOW(), updated_at=NOW()
                        WHERE id=@id", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@st", newStatus);
                        cmd.Parameters.AddWithValue("@au", adminUser);
                        cmd.Parameters.AddWithValue("@note", note);
                        cmd.Parameters.AddWithValue("@id", requestId);
                        cmd.ExecuteNonQuery();
                    }

                    tx.Commit();
                }
            }

            WriteAuditLog(adminUser, "Mark Request Status Change",
                BuildAuditPar(regno, courseId, acadYear, semester, null, null, null, null,
                    null, published ? (int?)publishedTotal : null, null, null,
                    requestId, "STATUS " + curStatus + "->" + newStatus, note),
                string.Format("Request #{0} status {1} -> {2} by {3}. Published:{4} Reverted:{5}. IP: {6}",
                    requestId, curStatus, newStatus, adminUser, published, reverted, GetClientIp()));

            string msg = "Status changed: " + Pretty(curStatus) + " → " + Pretty(newStatus) + ".";
            if (published) msg += " Proposed marks published; GPA/CGPA recalculated.";
            if (enteredJourney) msg += " The new mark was entered into the journey: Capture (HOD) → Approve (Dean) → Publish (Senate).";
            if (curStatus == "APPROVED" && newStatus != "APPROVED")
                msg += reverted ? " Prior mark auto-reverted; GPA/CGPA recalculated."
                                : (" NOTE: " + (sideMsg.Length > 0 ? sideMsg : "published mark was not auto-reverted — verify manually."));

            return Json.Serialize(new
            {
                success = true, from = curStatus, to = newStatus,
                marks_published = published, marks_reverted = reverted, entered_journey = enteredJourney,
                semester_gpa = semGpa, cgpa = cgpa, message = msg
            });
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  ADMIN PUSH TO ADMIN
    // ════════════════════════════════════════════════════════════════════════════

    [WebMethod(EnableSession = true)]
    public static string AdminPushToAdmin(int requestId, string note)
    {
        try
        {
            if (!CanManageRequests()) return Json.Serialize(new { success = false, message = "Access denied — you can only manage requests within your faculty/department." });
            note = (note ?? "").Trim();
            string adminUser = AdminUser();
            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                if (!RequestInScope(conn, requestId)) return SCOPE_DENIED;
                using (var cmd = new MySqlCommand(@"
                    UPDATE campus_dynamics_portal.acad_marks_requests
                    SET status             = 'PENDING_ADMIN',
                        admin_username     = @au,
                        admin_response     = @note,
                        admin_responded_at = NOW(),
                        updated_at         = NOW()
                    WHERE id = @id
                      AND status IN ('PENDING_LECTURER','PENDING_SUPERVISOR')", conn))
                {
                    cmd.Parameters.AddWithValue("@au",   adminUser);
                    cmd.Parameters.AddWithValue("@note", note.Length > 0 ? note : "Escalated to admin by admin override.");
                    cmd.Parameters.AddWithValue("@id",   requestId);
                    int n = cmd.ExecuteNonQuery();
                    if (n == 0) return Json.Serialize(new { success = false, message = "Request not found or not in a lecturer/supervisor pending state." });
                }
            }
            return Json.Serialize(new { success = true, message = "Request pushed to Admin queue." });
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  ADMIN UPDATE MARKS
    // ════════════════════════════════════════════════════════════════════════════

    [WebMethod(EnableSession = true)]
    public static string AdminUpdateMarks(int requestId, int? cw, int? exam, string note)
    {
        try
        {
            if (!CanManageRequests()) return Json.Serialize(new { success = false, message = "Access denied — you can only manage requests within your faculty/department." });
            note = (note ?? "").Trim();
            string adminUser = AdminUser();

            if (cw.HasValue && (cw.Value < 0 || cw.Value > 40))
                return Json.Serialize(new { success = false, message = "CW mark must be 0–40." });
            if (exam.HasValue && (exam.Value < 0 || exam.Value > 60))
                return Json.Serialize(new { success = false, message = "Exam mark must be 0–60." });
            if (cw.HasValue != exam.HasValue)
                return Json.Serialize(new { success = false, message = "Provide both CW and Exam, or leave both blank." });

            string reqRegno = "", courseId = "", acadYear = "";
            int semester = 0, courseRegId = 0;
            int? existingCw = null, existingExam = null, existingTotal = null;
            int? finalCw = null, finalExam = null, finalTotal = null;
            bool marksPublished = false;
            int publishedTotal = 0;
            decimal semGpa = 0m, cgpa = 0m;

            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                if (!RequestInScope(conn, requestId)) return SCOPE_DENIED;
                // Lock order: acad_marks_requests first (FOR UPDATE), then acad_results
                // — same order as AdminApprove, preventing circular deadlock.
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        using (var cmd = new MySqlCommand(@"
                            SELECT regno, course_id, acad_year, semester, course_reg_id,
                                   proposed_cw, proposed_exam, proposed_total
                            FROM campus_dynamics_portal.acad_marks_requests
                            WHERE id = @id
                            LIMIT 1 FOR UPDATE", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@id", requestId);
                            using (var rdr = cmd.ExecuteReader())
                            {
                                if (!rdr.Read())
                                {
                                    tx.Rollback();
                                    return Json.Serialize(new { success = false, message = "Request not found." });
                                }
                                reqRegno      = S(rdr, "regno");
                                courseId      = S(rdr, "course_id");
                                acadYear      = S(rdr, "acad_year");
                                int.TryParse(S(rdr, "semester"),      out semester);
                                int.TryParse(S(rdr, "course_reg_id"), out courseRegId);
                                existingCw    = NI(rdr, "proposed_cw");
                                existingExam  = NI(rdr, "proposed_exam");
                                existingTotal = NI(rdr, "proposed_total");
                            }
                        }

                        // Resolve final marks: admin-supplied takes priority, else keep existing
                        finalCw    = cw.HasValue   ? cw   : existingCw;
                        finalExam  = exam.HasValue  ? exam : existingExam;
                        finalTotal = (finalCw.HasValue && finalExam.HasValue)
                            ? (int?)(finalCw.Value + finalExam.Value)
                            : existingTotal;

                        bool canPublish = !string.IsNullOrEmpty(reqRegno)
                            && !string.IsNullOrEmpty(courseId)
                            && (finalTotal.HasValue || (finalCw.HasValue && finalExam.HasValue));

                        if (canPublish)
                        {
                            PublishToResults(conn, tx, requestId, reqRegno, courseId, acadYear,
                                semester, courseRegId, finalCw, finalExam, finalTotal,
                                "Admin (marks update): " + adminUser, note,
                                out publishedTotal, out semGpa, out cgpa);
                            marksPublished = true;
                        }

                        // Update proposed marks on the request; status is intentionally NOT changed
                        using (var cmd = new MySqlCommand(@"
                            UPDATE campus_dynamics_portal.acad_marks_requests
                            SET proposed_cw        = COALESCE(@pcw,  proposed_cw),
                                proposed_exam      = COALESCE(@pex,  proposed_exam),
                                proposed_total     = COALESCE(@ptot, proposed_total),
                                admin_username     = @au,
                                admin_response     = @note,
                                updated_at         = NOW()
                            WHERE id = @id", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@pcw",  finalCw.HasValue    ? (object)finalCw.Value    : DBNull.Value);
                            cmd.Parameters.AddWithValue("@pex",  finalExam.HasValue  ? (object)finalExam.Value  : DBNull.Value);
                            cmd.Parameters.AddWithValue("@ptot", finalTotal.HasValue ? (object)finalTotal.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@au",   adminUser);
                            cmd.Parameters.AddWithValue("@note", note.Length > 0 ? note : "Marks updated by admin.");
                            cmd.Parameters.AddWithValue("@id",   requestId);
                            cmd.ExecuteNonQuery();
                        }

                        tx.Commit();
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { }
                        throw;
                    }
                }
            }

            WriteAuditLog(adminUser, "Mark Request Marks Update",
                BuildAuditPar(reqRegno, courseId, acadYear, semester,
                    existingCw, finalCw, existingExam, finalExam,
                    existingTotal, marksPublished ? (int?)publishedTotal : finalTotal,
                    null, null, requestId, "MARKS_UPDATED", note),
                string.Format("Request #{0} marks updated by {1}. CW: {2}→{3} Exam: {4}→{5}. Published: {6}. IP: {7}",
                    requestId, adminUser,
                    existingCw.HasValue ? existingCw.Value.ToString() : "-", finalCw.HasValue ? finalCw.Value.ToString() : "-",
                    existingExam.HasValue ? existingExam.Value.ToString() : "-", finalExam.HasValue ? finalExam.Value.ToString() : "-",
                    marksPublished, GetClientIp()));

            return Json.Serialize(new
            {
                success         = true,
                marks_published = marksPublished,
                published_total = publishedTotal,
                semester_gpa    = semGpa,
                cgpa            = cgpa,
                message         = marksPublished
                    ? "Marks updated and published to student results. GPA recalculated."
                    : "Proposed marks updated. (Insufficient data to publish to results — check regno and course ID.)"
            });
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  ADMIN BATCH  (approve | reject | force)
    // ════════════════════════════════════════════════════════════════════════════

    [WebMethod(EnableSession = true)]
    public static string AdminBatch(string ids, string action, string note)
    {
        try
        {
            if (!CanManageRequests()) return Json.Serialize(new { success = false, message = "Access denied — you can only manage requests within your faculty/department." });

            action = (action ?? "").Trim().ToLowerInvariant();
            note   = (note   ?? "").Trim();

            if (action != "approve" && action != "reject" && action != "force")
                return Json.Serialize(new { success = false, message = "Invalid batch action." });

            if ((action == "reject" || action == "force") && note.Length < 5)
                return Json.Serialize(new { success = false, message = "Please provide a reason (min 5 characters)." });

            // Parse and validate IDs
            var rawIds = (ids ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var intIds = new System.Collections.Generic.List<int>();
            foreach (string raw in rawIds)
            {
                int v; if (int.TryParse(raw.Trim(), out v) && v > 0) intIds.Add(v);
            }
            if (intIds.Count == 0) return Json.Serialize(new { success = false, message = "No valid request IDs provided." });

            string adminUser = AdminUser();
            int processed = 0;

            using (var conn = new MySqlConnection(ConnStr))
            {
                conn.Open();

                // Role-based scope: drop any ids outside the user's faculty/department.
                if (!CurrentScope().IsAdmin)
                {
                    var scoped = new System.Collections.Generic.List<int>();
                    foreach (int rid in intIds) if (RequestInScope(conn, rid)) scoped.Add(rid);
                    intIds = scoped;
                    if (intIds.Count == 0)
                        return Json.Serialize(new { success = false, message = "None of the selected requests are within your faculty/department." });
                }

                if (action == "approve")
                {
                    // Process each ID individually to use existing publish logic
                    foreach (int rid in intIds)
                    {
                        try
                        {
                            string reqRegno = "", courseId = "", acadYear = "", requestType = "";
                            int semester = 0, courseRegId = 0;
                            int? proposedCw = null, proposedExam = null, proposedTotal = null;
                            bool found = false;

                            using (var tx = conn.BeginTransaction())
                            {
                                using (var cmd = new MySqlCommand(@"
                                    SELECT regno, course_id, acad_year, semester, course_reg_id,
                                           request_type, proposed_cw, proposed_exam, proposed_total
                                    FROM campus_dynamics_portal.acad_marks_requests
                                    WHERE id = @id
                                    LIMIT 1 FOR UPDATE", conn, tx))
                                {
                                    cmd.Parameters.AddWithValue("@id", rid);
                                    using (var rdr = cmd.ExecuteReader())
                                    {
                                        if (rdr.Read())
                                        {
                                            found       = true;
                                            reqRegno    = S(rdr, "regno");
                                            courseId    = S(rdr, "course_id");
                                            acadYear    = S(rdr, "acad_year");
                                            int.TryParse(S(rdr, "semester"),      out semester);
                                            int.TryParse(S(rdr, "course_reg_id"), out courseRegId);
                                            requestType = S(rdr, "request_type");
                                            proposedCw  = NI(rdr, "proposed_cw");
                                            proposedExam= NI(rdr, "proposed_exam");
                                            proposedTotal= NI(rdr, "proposed_total");
                                        }
                                    }
                                }

                                if (!found) { tx.Rollback(); continue; }

                                bool canPublish = !string.IsNullOrEmpty(reqRegno)
                                    && !string.IsNullOrEmpty(courseId)
                                    && (proposedTotal.HasValue || (proposedCw.HasValue && proposedExam.HasValue));

                                // Same rule as AdminApprove: a MARK_CHANGE re-enters the journey
                                // (Capture → Approve → Publish); only MISSING_MARK publishes now.
                                if (string.Equals(requestType, "MARK_CHANGE", StringComparison.OrdinalIgnoreCase))
                                {
                                    EnterChangeIntoJourney(conn, tx, rid, courseRegId,
                                        proposedCw, proposedExam, proposedTotal, "Admin (batch): " + adminUser, note);
                                }
                                else if (canPublish)
                                {
                                    int pt; decimal sg, cg;
                                    PublishToResults(conn, tx, rid, reqRegno, courseId, acadYear,
                                        semester, courseRegId, proposedCw, proposedExam, proposedTotal,
                                        "Admin (batch): " + adminUser, note, out pt, out sg, out cg);
                                }

                                using (var cmd = new MySqlCommand(@"
                                    UPDATE campus_dynamics_portal.acad_marks_requests
                                    SET status             = 'APPROVED',
                                        admin_username     = @au,
                                        admin_response     = @note,
                                        admin_responded_at = NOW(),
                                        updated_at         = NOW()
                                    WHERE id = @id", conn, tx))
                                {
                                    cmd.Parameters.AddWithValue("@au",   adminUser);
                                    cmd.Parameters.AddWithValue("@note", note);
                                    cmd.Parameters.AddWithValue("@id",   rid);
                                    cmd.ExecuteNonQuery();
                                }
                                tx.Commit();
                                processed++;
                                WriteAuditLog(adminUser, "Mark Request Batch",
                                    BuildAuditPar(reqRegno, courseId, acadYear, semester,
                                        proposedCw, proposedCw, proposedExam, proposedExam,
                                        null, canPublish ? (int?)proposedTotal : null,
                                        null, null, rid, "BATCH_APPROVED", note),
                                    string.Format("Batch APPROVE Request #{0} by {1}. IP: {2}", rid, adminUser, GetClientIp()));
                            }
                        }
                        catch { /* skip individual failures in batch */ }
                    }
                }
                else
                {
                    // reject / force — single UPDATE with IN (...)
                    string newStatus = action == "reject" ? "REJECTED" : "CANCELLED";
                    string noteVal   = action == "force" ? "FORCE CLOSED BY ADMIN (batch): " + note : note;
                    string batchAction = action == "reject" ? "BATCH_REJECTED" : "BATCH_FORCE_CLOSED";

                    // Build parameterised IN list
                    var inParams = new System.Collections.Generic.List<string>();
                    using (var cmd = new MySqlCommand())
                    {
                        cmd.Connection = conn;
                        for (int i = 0; i < intIds.Count; i++)
                        {
                            string pname = "@id" + i;
                            inParams.Add(pname);
                            cmd.Parameters.AddWithValue(pname, intIds[i]);
                        }
                        cmd.CommandText = string.Format(
                            @"UPDATE campus_dynamics_portal.acad_marks_requests
                              SET status             = @st,
                                  admin_username     = @au,
                                  admin_response     = @note,
                                  admin_responded_at = NOW(),
                                  updated_at         = NOW()
                              WHERE id IN ({0})", string.Join(",", inParams.ToArray()));
                        cmd.Parameters.AddWithValue("@st",   newStatus);
                        cmd.Parameters.AddWithValue("@au",   adminUser);
                        cmd.Parameters.AddWithValue("@note", noteVal);
                        processed = cmd.ExecuteNonQuery();
                    }
                    // Audit one entry per ID in the batch
                    foreach (int rid in intIds)
                    {
                        WriteAuditLog(adminUser, "Mark Request Batch",
                            string.Format("Student: - Course: - Academic Year: - Semester: 0 " +
                                "Old CourseWork Mark: - New CourseWork: - Old Exam Mark: - New Exam Mark: - " +
                                "Old Score: - New Score: - Old Grade: - New Grade: - " +
                                "Request#: {0} Action: {1} Note: {2} IP Address: {3}",
                                rid, batchAction, note, GetClientIp()),
                            string.Format("Batch {0} Request #{1} by {2}. IP: {3}", action.ToUpper(), rid, adminUser, GetClientIp()));
                    }
                }
            }

            return Json.Serialize(new { success = true, message = processed + " request(s) processed.", count = processed });
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  DATA READER HELPERS
    // ════════════════════════════════════════════════════════════════════════════

    private static string S(System.Data.IDataReader rdr, string col)
    {
        try { int i = rdr.GetOrdinal(col); return rdr.IsDBNull(i) ? "" : rdr.GetValue(i).ToString().Trim(); }
        catch { return ""; }
    }

    private static int? NI(System.Data.IDataReader rdr, string col)
    {
        try
        {
            int i = rdr.GetOrdinal(col);
            if (rdr.IsDBNull(i)) return null;
            int v; return int.TryParse(rdr.GetValue(i).ToString(), out v) ? (int?)v : null;
        }
        catch { return null; }
    }

    private static int N(System.Data.IDataReader rdr, string col)
    {
        try
        {
            int i = rdr.GetOrdinal(col);
            if (rdr.IsDBNull(i)) return 0;
            int v; return int.TryParse(rdr.GetValue(i).ToString(), out v) ? v : 0;
        }
        catch { return 0; }
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  GRADE / GPA HELPERS
    // ════════════════════════════════════════════════════════════════════════════

    // MRU / NCHE scale: 80 A · 75 B+ · 70 B · 65 C+ · 60 C · 55 D+ · 50 D · <50 F.
    private static string CalcGrade(int score)
    {
        if (score >= 80) return "A";
        if (score >= 75) return "B+";
        if (score >= 70) return "B";
        if (score >= 65) return "C+";
        if (score >= 60) return "C";
        if (score >= 55) return "D+";
        if (score >= 50) return "D";
        return "F";
    }

    private static decimal GradeToPoint(string grade)
    {
        switch ((grade ?? "").Trim().ToUpper())
        {
            case "A":  return 5.0m;
            case "B+": return 4.5m;
            case "B":  return 4.0m;
            case "C+": return 3.5m;
            case "C":  return 3.0m;
            case "D+": return 2.5m;
            case "D":  return 2.0m;
            default:   return 0.0m;
        }
    }

    private static decimal CalcGpa(MySqlConnection conn, MySqlTransaction tx, string regno, string acadYear, int semester)
    {
        using (var cmd = new MySqlCommand(@"
            SELECT
                COALESCE(SUM(COALESCE(gradept, CASE UPPER(TRIM(COALESCE(grade,'')))
                    WHEN 'A' THEN 5.0 WHEN 'B+' THEN 4.5 WHEN 'B' THEN 4.0
                    WHEN 'C+' THEN 3.5 WHEN 'C' THEN 3.0 WHEN 'D+' THEN 2.5
                    WHEN 'D' THEN 2.0 WHEN 'F' THEN 0.0 ELSE 0.0 END
                ) * COALESCE(CreditUnits,3)), 0) AS wp,
                COALESCE(SUM(COALESCE(CreditUnits,3)), 0) AS tc
            FROM acad_results
            WHERE regno=@r AND acad=@ay AND semester=@sem", conn, tx))
        {
            cmd.Parameters.AddWithValue("@r",  regno);
            cmd.Parameters.AddWithValue("@ay", acadYear);
            cmd.Parameters.AddWithValue("@sem", semester);
            using (var rdr = cmd.ExecuteReader())
            {
                if (!rdr.Read()) return 0m;
                decimal wp = rdr.IsDBNull(0) ? 0m : Convert.ToDecimal(rdr.GetValue(0));
                decimal tc = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                return tc > 0 ? Math.Round(wp / tc, 2) : 0m;
            }
        }
    }

    private static decimal CalcCgpa(MySqlConnection conn, MySqlTransaction tx, string regno)
    {
        using (var cmd = new MySqlCommand(@"
            SELECT
                COALESCE(SUM(COALESCE(gradept, CASE UPPER(TRIM(COALESCE(grade,'')))
                    WHEN 'A' THEN 5.0 WHEN 'B+' THEN 4.5 WHEN 'B' THEN 4.0
                    WHEN 'C+' THEN 3.5 WHEN 'C' THEN 3.0 WHEN 'D+' THEN 2.5
                    WHEN 'D' THEN 2.0 WHEN 'F' THEN 0.0 ELSE 0.0 END
                ) * COALESCE(CreditUnits,3)), 0) AS wp,
                COALESCE(SUM(COALESCE(CreditUnits,3)), 0) AS tc
            FROM acad_results WHERE regno=@r", conn, tx))
        {
            cmd.Parameters.AddWithValue("@r", regno);
            using (var rdr = cmd.ExecuteReader())
            {
                if (!rdr.Read()) return 0m;
                decimal wp = rdr.IsDBNull(0) ? 0m : Convert.ToDecimal(rdr.GetValue(0));
                decimal tc = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                return tc > 0 ? Math.Round(wp / tc, 2) : 0m;
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  PUBLISH MARKS TO acad_results
    // ════════════════════════════════════════════════════════════════════════════

    // ════════════════════════════════════════════════════════════════════════════
    //  Route an approved MARK_CHANGE into the normal marks journey instead of
    //  force-publishing it.
    //
    //  Same rule as the portal (MarkRequestsController.EnterApprovedChangeIntoJourney):
    //  a mark change that clears its request-approval does NOT go straight to
    //  acad_results. The new mark is set on the registration and the row drops to
    //  ENTERED (lecturer entry), then travels Capture (HOD) → Approve (Dean) →
    //  Publish (Senate) like any other mark; only the Senate publish writes results.
    //  Stale capture/approve/publish links are cleared. acad_results is deliberately
    //  left untouched — the previously-published mark (if any) stays of record until
    //  the new one completes the journey, and a fresh change publishes nothing yet.
    // ════════════════════════════════════════════════════════════════════════════
    private static void EnterChangeIntoJourney(
        MySqlConnection conn, MySqlTransaction tx,
        int requestId, int courseRegId,
        int? proposedCw, int? proposedExam, int? proposedTotal,
        string actorLabel, string note)
    {
        if (courseRegId <= 0) return;

        int total = proposedTotal.HasValue ? proposedTotal.Value : ((proposedCw ?? 0) + (proposedExam ?? 0));
        string comment = "Mark-change request #" + requestId + " approved by " + (actorLabel ?? "system")
            + "; new mark entered into the approval journey (Capture -> Approve -> Publish). CW="
            + (proposedCw.HasValue ? proposedCw.Value.ToString() : "-") + ", Exam="
            + (proposedExam.HasValue ? proposedExam.Value.ToString() : "-") + ", Total=" + total
            + (string.IsNullOrEmpty(note) ? "" : ("; " + note)) + ".";

        using (var cmd = new MySqlCommand(@"
            UPDATE campus_dynamics_portal.acad_course_registration
            SET provisional_course_work_marks    = COALESCE(@cw, provisional_course_work_marks),
                provisional_exam_marks            = COALESCE(@ex, provisional_exam_marks),
                provisional_total_marks           = @total,
                provisional_marks_status          = 'pending',
                mark_stage                        = 'ENTERED',
                capture_record_id                 = NULL,
                approve_record_id                 = NULL,
                publish_record_id                 = NULL,
                mark_stage_changed_at             = NOW(),
                mark_stage_changed_by             = @actor,
                provisional_marks_review_comments = @cm,
                provisional_marks_reviewed_by     = @actor,
                provisional_marks_review_date     = NOW()
            WHERE id = @id", conn, tx))
        {
            cmd.Parameters.AddWithValue("@cw",    proposedCw.HasValue   ? (object)proposedCw.Value   : DBNull.Value);
            cmd.Parameters.AddWithValue("@ex",    proposedExam.HasValue  ? (object)proposedExam.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@total", total);
            cmd.Parameters.AddWithValue("@actor", actorLabel ?? "");
            cmd.Parameters.AddWithValue("@cm",    comment);
            cmd.Parameters.AddWithValue("@id",    courseRegId);
            cmd.ExecuteNonQuery();
        }
    }

    private static void PublishToResults(
        MySqlConnection conn, MySqlTransaction tx,
        int requestId, string regno, string courseId, string acadYear, int semester,
        int courseRegId, int? proposedCw, int? proposedExam, int? proposedTotal,
        string actorLabel, string adminNote,
        out int publishedTotal, out decimal semGpa, out decimal cgpa)
    {
        // Attribute this final-result change for the acad_results audit trigger (who changed what).
        SetMarkAuditContext(conn, tx, actorLabel, "MarkRequestsAdmin",
            "Request #" + requestId + (string.IsNullOrEmpty(adminNote) ? "" : (": " + adminNote)));
        int? finalCw = proposedCw, finalExam = proposedExam;
        if ((!finalCw.HasValue || !finalExam.HasValue) && courseRegId > 0)
        {
            using (var cmd = new MySqlCommand(@"
                SELECT provisional_course_work_marks, provisional_exam_marks
                FROM campus_dynamics_portal.acad_course_registration WHERE id=@id LIMIT 1", conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", courseRegId);
                using (var rdr = cmd.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        if (!finalCw.HasValue)   finalCw   = rdr.IsDBNull(0) ? (int?)null : Convert.ToInt32(rdr.GetValue(0));
                        if (!finalExam.HasValue) finalExam = rdr.IsDBNull(1) ? (int?)null : Convert.ToInt32(rdr.GetValue(1));
                    }
                }
            }
        }

        if (finalCw.HasValue && finalExam.HasValue)
            publishedTotal = finalCw.Value + finalExam.Value;
        else if (proposedTotal.HasValue)
            publishedTotal = proposedTotal.Value;
        else
            throw new InvalidOperationException("Cannot publish — proposed marks are incomplete.");

        string grade    = CalcGrade(publishedTotal);
        decimal gradePt = GradeToPoint(grade);

        int studyYear = 1;
        using (var cmd = new MySqlCommand(@"
            SELECT IFNULL(studyyear,1) FROM acad_registration
            WHERE regno=@r AND acad_year=@ay ORDER BY semester DESC LIMIT 1", conn, tx))
        {
            cmd.Parameters.AddWithValue("@r",  regno);
            cmd.Parameters.AddWithValue("@ay", acadYear);
            var o = cmd.ExecuteScalar();
            if (o != null && o != DBNull.Value) int.TryParse(o.ToString(), out studyYear);
        }

        int creditUnits = 3;
        string[] cuCols = new[] { "CreditUnit", "creditunit", "CreditUnits", "creditunits" };
        foreach (string cuCol in cuCols)
        {
            try
            {
                using (var cmd = new MySqlCommand(
                    "SELECT COALESCE(NULLIF(" + cuCol + ",0),3) FROM acad_course WHERE courseID=@cid LIMIT 1", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@cid", courseId);
                    var o = cmd.ExecuteScalar();
                    if (o != null && o != DBNull.Value)
                    {
                        int p; if (int.TryParse(o.ToString(), out p) && p > 0) { creditUnits = p; break; }
                    }
                }
            }
            catch { }
        }

        string oldGrade = ""; int? oldScore = null;
        using (var cmd = new MySqlCommand(@"
            SELECT score, IFNULL(grade,'') AS grade FROM acad_results
            WHERE regno=@r AND courseid=@cid AND acad=@ay AND semester=@sem ORDER BY id DESC LIMIT 1", conn, tx))
        {
            cmd.Parameters.AddWithValue("@r",   regno); cmd.Parameters.AddWithValue("@cid", courseId);
            cmd.Parameters.AddWithValue("@ay",  acadYear); cmd.Parameters.AddWithValue("@sem", semester);
            using (var rdr = cmd.ExecuteReader())
            {
                if (rdr.Read())
                {
                    oldScore = rdr.IsDBNull(0) ? (int?)null : Convert.ToInt32(rdr.GetValue(0));
                    if (!rdr.IsDBNull(1)) oldGrade = rdr.GetValue(1).ToString();
                }
            }
        }

        string comment = "Request #" + requestId + " approved by " + actorLabel
            + "; Score " + (oldScore.HasValue ? oldScore.Value.ToString() : "-") + "→" + publishedTotal
            + ", Grade " + (string.IsNullOrEmpty(oldGrade) ? "-" : oldGrade) + "→" + grade
            + "; CW " + (finalCw.HasValue ? finalCw.Value.ToString() : "-")
            + ", Exam " + (finalExam.HasValue ? finalExam.Value.ToString() : "-")
            + (string.IsNullOrEmpty(adminNote) ? "" : "; Note: " + adminNote);

        string[] resCuCols = new[] { "CreditUnits", "creditunits" };
        string resCuCol = "CreditUnits";
        foreach (string c in resCuCols)
        {
            try
            {
                using (var t = new MySqlCommand("SELECT " + c + " FROM acad_results LIMIT 0", conn, tx))
                { t.ExecuteReader().Close(); resCuCol = c; break; }
            }
            catch { }
        }

        int updated = 0;
        using (var cmd = new MySqlCommand(@"
            UPDATE acad_results SET score=@sc, grade=@gr, gradept=@gp, " + resCuCol + @"=@cu,
                studyyear=@sy, result_comment=@cm
            WHERE regno=@r AND courseid=@cid AND acad=@ay AND semester=@sem", conn, tx))
        {
            cmd.Parameters.AddWithValue("@sc", publishedTotal); cmd.Parameters.AddWithValue("@gr", grade);
            cmd.Parameters.AddWithValue("@gp", gradePt);        cmd.Parameters.AddWithValue("@cu", creditUnits);
            cmd.Parameters.AddWithValue("@sy", studyYear);      cmd.Parameters.AddWithValue("@cm", comment);
            cmd.Parameters.AddWithValue("@r",  regno);          cmd.Parameters.AddWithValue("@cid", courseId);
            cmd.Parameters.AddWithValue("@ay", acadYear);       cmd.Parameters.AddWithValue("@sem", semester);
            updated = cmd.ExecuteNonQuery();
        }

        if (updated == 0)
        {
            using (var cmd = new MySqlCommand(@"
                INSERT INTO acad_results (regno,courseid,acad,semester,studyyear,score,grade,gradept," + resCuCol + @",result_comment)
                VALUES (@r,@cid,@ay,@sem,@sy,@sc,@gr,@gp,@cu,@cm)", conn, tx))
            {
                cmd.Parameters.AddWithValue("@r",  regno);         cmd.Parameters.AddWithValue("@cid", courseId);
                cmd.Parameters.AddWithValue("@ay", acadYear);      cmd.Parameters.AddWithValue("@sem", semester);
                cmd.Parameters.AddWithValue("@sy", studyYear);     cmd.Parameters.AddWithValue("@sc", publishedTotal);
                cmd.Parameters.AddWithValue("@gr", grade);         cmd.Parameters.AddWithValue("@gp", gradePt);
                cmd.Parameters.AddWithValue("@cu", creditUnits);   cmd.Parameters.AddWithValue("@cm", comment);
                cmd.ExecuteNonQuery();
            }
        }

        semGpa = CalcGpa(conn, tx, regno, acadYear, semester);
        cgpa   = CalcCgpa(conn, tx, regno);

        using (var cmd = new MySqlCommand(
            "UPDATE acad_results SET gpa=@gpa WHERE regno=@r AND acad=@ay AND semester=@sem", conn, tx))
        {
            cmd.Parameters.AddWithValue("@gpa", semGpa);
            cmd.Parameters.AddWithValue("@r",   regno);
            cmd.Parameters.AddWithValue("@ay",  acadYear);
            cmd.Parameters.AddWithValue("@sem", semester);
            cmd.ExecuteNonQuery();
        }

        if (courseRegId > 0)
        {
            try
            {
                using (var cmd = new MySqlCommand(@"
                    UPDATE campus_dynamics_portal.acad_course_registration
                    SET provisional_course_work_marks = COALESCE(@cw, provisional_course_work_marks),
                        provisional_exam_marks  = COALESCE(@ex, provisional_exam_marks),
                        provisional_total_marks = @tot,
                        provisional_marks_status = 'published',
                        provisional_marks_review_comments = @cm,
                        provisional_marks_reviewed_by = @actor,
                        provisional_marks_review_date = NOW(),
                        provisional_published_by = @actor,
                        provisional_published_date = NOW()
                    WHERE id = @id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@cw",    finalCw.HasValue   ? (object)finalCw.Value   : DBNull.Value);
                    cmd.Parameters.AddWithValue("@ex",    finalExam.HasValue  ? (object)finalExam.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@tot",   publishedTotal);
                    cmd.Parameters.AddWithValue("@cm",    comment);
                    cmd.Parameters.AddWithValue("@actor", actorLabel);
                    cmd.Parameters.AddWithValue("@id",    courseRegId);
                    cmd.ExecuteNonQuery();
                }
            }
            catch { /* non-fatal */ }
        }

        // ── Audit: marks written to acad_results ──────────────────────────────
        // Uses a separate connection (best-effort) so it can never deadlock with
        // or roll back the caller's transaction.
        WriteAuditLog(
            actorLabel,
            "Marks Published to Results",
            BuildAuditPar(regno, courseId, acadYear, semester,
                null, finalCw, null, finalExam,
                oldScore, (int?)publishedTotal, oldGrade, grade,
                requestId, "PUBLISHED", adminNote),
            string.Format("Marks written to acad_results for {0}/{1} by {2}. Score: {3}→{4} Grade: {5}→{6}. IP: {7}",
                regno, courseId, actorLabel,
                oldScore.HasValue ? oldScore.Value.ToString() : "-", publishedTotal,
                string.IsNullOrEmpty(oldGrade) ? "-" : oldGrade, grade,
                GetClientIp()));
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  REVERSE a mark this request previously published to acad_results
    //
    //  Schema-free + robust: the approval stamped result_comment as
    //    "Request #<id> approved by <actor>; Score <old>→<new>, Grade <old>→<new>; ..."
    //  so we restore <old>. If <old> is "-" the approval INSERTED the row → we delete
    //  it. If the current row's comment references a DIFFERENT request (i.e. it was
    //  superseded by a later approval) we refuse and tell the caller — never guess.
    // ════════════════════════════════════════════════════════════════════════════
    private static void RevertPublishedResult(
        MySqlConnection conn, MySqlTransaction tx, int requestId,
        string regno, string courseId, string acadYear, int semester, string actorLabel,
        out bool reverted, out string message, out decimal semGpa, out decimal cgpa)
    {
        reverted = false; message = ""; semGpa = 0m; cgpa = 0m;

        // Attribute this reversal for the acad_results audit trigger.
        SetMarkAuditContext(conn, tx, actorLabel, "MarkRequestsAdmin:revert", "Revert request #" + requestId);

        long rowId = -1; string comment = "";
        using (var cmd = new MySqlCommand(@"
            SELECT id, IFNULL(result_comment,'') AS rc FROM acad_results
            WHERE regno=@r AND courseid=@cid AND acad=@ay AND semester=@sem
            ORDER BY id DESC LIMIT 1", conn, tx))
        {
            cmd.Parameters.AddWithValue("@r", regno); cmd.Parameters.AddWithValue("@cid", courseId);
            cmd.Parameters.AddWithValue("@ay", acadYear); cmd.Parameters.AddWithValue("@sem", semester);
            using (var rdr = cmd.ExecuteReader())
                if (rdr.Read()) { rowId = Convert.ToInt64(rdr.GetValue(0)); comment = rdr.GetValue(1).ToString(); }
        }

        if (rowId < 0) { message = "No published result found for this course — nothing to revert."; return; }

        if (comment.IndexOf("Request #" + requestId + " approved", StringComparison.OrdinalIgnoreCase) < 0)
        {
            message = "The published result was changed by a later action — not auto-reverted. Please verify the mark manually.";
            return;
        }

        string oldScoreTok = ExtractArrowOld(comment, "Score ");
        string oldGradeTok = ExtractArrowOld(comment, "Grade ");
        if (oldScoreTok == null)
        {
            message = "Couldn't read the prior mark from the record — not auto-reverted. Please verify manually.";
            return;
        }

        if (oldScoreTok == "-")
        {
            using (var cmd = new MySqlCommand("DELETE FROM acad_results WHERE id=@id", conn, tx))
            { cmd.Parameters.AddWithValue("@id", rowId); cmd.ExecuteNonQuery(); }
        }
        else
        {
            int oldScore; int.TryParse(oldScoreTok, out oldScore);
            string oldGrade = (!string.IsNullOrEmpty(oldGradeTok) && oldGradeTok != "-") ? oldGradeTok : CalcGrade(oldScore);
            decimal oldPt = GradeToPoint(oldGrade);
            string rc = "Reverted request #" + requestId + " by " + actorLabel + "; restored Score " + oldScore + ", Grade " + oldGrade;
            using (var cmd = new MySqlCommand(
                "UPDATE acad_results SET score=@sc, grade=@gr, gradept=@gp, result_comment=@cm WHERE id=@id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@sc", oldScore); cmd.Parameters.AddWithValue("@gr", oldGrade);
                cmd.Parameters.AddWithValue("@gp", oldPt); cmd.Parameters.AddWithValue("@cm", rc);
                cmd.Parameters.AddWithValue("@id", rowId); cmd.ExecuteNonQuery();
            }
        }

        semGpa = CalcGpa(conn, tx, regno, acadYear, semester);
        cgpa   = CalcCgpa(conn, tx, regno);
        using (var cmd = new MySqlCommand(
            "UPDATE acad_results SET gpa=@gpa WHERE regno=@r AND acad=@ay AND semester=@sem", conn, tx))
        {
            cmd.Parameters.AddWithValue("@gpa", semGpa); cmd.Parameters.AddWithValue("@r", regno);
            cmd.Parameters.AddWithValue("@ay", acadYear); cmd.Parameters.AddWithValue("@sem", semester);
            cmd.ExecuteNonQuery();
        }

        // best-effort: flip provisional back so it can be re-published cleanly later
        try
        {
            using (var cmd = new MySqlCommand(@"
                UPDATE campus_dynamics_portal.acad_course_registration cr
                JOIN campus_dynamics_portal.acad_marks_requests r ON r.course_reg_id = cr.id
                SET cr.provisional_marks_status = 'submitted',
                    cr.provisional_marks_review_comments = CONCAT('Reverted by ', @actor, ' on ', NOW())
                WHERE r.id=@rid", conn, tx))
            { cmd.Parameters.AddWithValue("@actor", actorLabel); cmd.Parameters.AddWithValue("@rid", requestId); cmd.ExecuteNonQuery(); }
        }
        catch { /* non-fatal */ }

        reverted = true;
        message = "Prior mark restored and GPA/CGPA recalculated.";

        WriteAuditLog(actorLabel, "Marks Reverted from Results",
            BuildAuditPar(regno, courseId, acadYear, semester, null, null, null, null,
                null, null, null, null, requestId, "REVERTED", message),
            string.Format("Result reverted for {0}/{1} by {2}. {3} IP: {4}",
                regno, courseId, actorLabel, message, GetClientIp()));
    }

    // From "...Score 65→85, ..." with prefix "Score " returns "65" (part before → or ->).
    private static string ExtractArrowOld(string text, string prefix)
    {
        if (string.IsNullOrEmpty(text)) return null;
        int i = text.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        i += prefix.Length;
        int a1 = text.IndexOf('→', i);              // '→'
        int a2 = text.IndexOf("->", i, StringComparison.Ordinal);
        int end;
        if (a1 >= 0 && (a2 < 0 || a1 < a2)) end = a1;
        else if (a2 >= 0) end = a2;
        else return null;
        return text.Substring(i, end - i).Trim().TrimEnd(',', ';', ' ');
    }

    private static string Pretty(string status)
    {
        switch ((status ?? "").ToUpperInvariant())
        {
            case "PENDING_LECTURER":   return "Pending Lecturer";
            case "PENDING_SUPERVISOR": return "Pending Supervisor";
            case "PENDING_ADMIN":      return "Pending Admin";
            case "APPROVED":           return "Approved";
            case "REJECTED":           return "Rejected";
            case "CANCELLED":          return "Cancelled";
            default:                   return status;
        }
    }
}
