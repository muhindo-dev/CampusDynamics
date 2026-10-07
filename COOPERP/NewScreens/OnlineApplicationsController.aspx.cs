using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

public partial class COOPERP_NewScreens_OnlineApplicationsController : System.Web.UI.Page
{
    // ── Filter state (set in Page_Load, used by ASPX <%= %> expressions) ────
    protected string FilterQ       { get; private set; }
    protected string FilterStatus  { get; private set; }
    protected string FilterProg    { get; private set; }
    protected string FilterYear    { get; private set; }
    protected string FilterSession { get; private set; }
    protected int    FilterPage    { get; private set; }
    protected int    FilterSize    { get; private set; }

    // ColumnExists cache — avoids repeated information_schema queries per request
    private readonly Dictionary<string, bool> _colCache =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    // Always connects to campus_dynamics_portal — portal application data lives there.
    // Connection string names differ between environments, so we scan by actual database
    // name rather than relying on string name conventions.
    private string ConnStr
    {
        get
        {
            // acad_applications is written by the portal into campus_dynamics (the main EMIS DB).
            // In production the portal uses campus_dynamics_portalConnectionString → campus_dynamics,
            // so we scan by actual database name to stay environment-agnostic.

            // Pass 1: find any MySQL connection string that already targets campus_dynamics
            foreach (System.Configuration.ConnectionStringSettings cs in ConfigurationManager.ConnectionStrings)
            {
                if (string.IsNullOrEmpty(cs.ConnectionString)) continue;
                if (cs.ConnectionString.IndexOf("XpoProvider", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                try
                {
                    var b = new MySqlConnectionStringBuilder(cs.ConnectionString);
                    if (string.Equals(b.Database, "campus_dynamics", StringComparison.OrdinalIgnoreCase))
                        return cs.ConnectionString;
                }
                catch { }
            }

            // Pass 2: borrow credentials from any known string and override the database
            string[] candidates = { "campus_dynamics_portalConnectionString", "vacConnectionString",
                                     "schoolMISConnectionString", "LocalMySqlServer" };
            foreach (string name in candidates)
            {
                var cs = ConfigurationManager.ConnectionStrings[name];
                if (cs == null || string.IsNullOrEmpty(cs.ConnectionString)) continue;
                if (cs.ConnectionString.IndexOf("XpoProvider", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                try
                {
                    var b = new MySqlConnectionStringBuilder(cs.ConnectionString);
                    b.Database = "campus_dynamics";
                    return b.ConnectionString;
                }
                catch { }
            }

            return string.Empty;
        }
    }

    // True when user is authenticated via Forms Auth cookie OR has a live session variable
    private bool IsAuthenticatedStaff()
    {
        if (Context.User != null && Context.User.Identity != null && Context.User.Identity.IsAuthenticated)
            return true;
        return (Session["username"] != null && !string.IsNullOrWhiteSpace(Session["username"].ToString()))
            || (Session["usernm"]   != null && !string.IsNullOrWhiteSpace(Session["usernm"].ToString()));
    }

    // ════════════════════════════════════════════════════════════════════════
    // PAGE LOAD
    // ════════════════════════════════════════════════════════════════════════
    protected void Page_Load(object sender, EventArgs e)
    {
        string ajax = Request.QueryString["ajax"] ?? "";

        // Binary branch — stream the payment receipt file (not JSON)
        if (string.Equals(ajax, "view_receipt", StringComparison.OrdinalIgnoreCase))
        {
            HandleViewReceipt();
            return;
        }

        if (!string.IsNullOrEmpty(ajax))
        {
            // AJAX branch — detail view and actions only
            Response.Clear();
            Response.ContentType = "application/json";
            Response.TrySkipIisCustomErrors = true; // ensure JSON reaches the client, not IIS HTML error page

            if (!IsAuthenticatedStaff())
            {
                Response.StatusCode = 403;
                Response.Write("{\"ok\":false,\"error\":\"session_expired\"}");
                Response.End();
                return;
            }

            try
            {
                switch (ajax)
                {
                    case "detail": HandleDetail(); break;
                    case "edit":   HandleEdit();   break;
                    case "review": HandleReview(); break;
                    case "admit":  HandleAdmit();  break;
                    case "reject": HandleReject(); break;
                    case "note":   HandleNote();   break;
                    case "pay_verify": HandlePayVerify(); break;
                    case "pay_reject": HandlePayReject(); break;
                    default:
                        Response.Write("{\"ok\":false,\"error\":\"Unknown action.\"}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Response.Write("{\"ok\":false,\"error\":" + JsonStr(ex.Message) + "}");
            }
            Response.End();
            return;
        }

        // ── Normal server-side page render ───────────────────────────────
        if (!IsAuthenticatedStaff())
        {
            Response.Redirect(ResolveUrl("~/Default.aspx"));
            return;
        }

        FilterQ       = (Request.QueryString["q"]       ?? "").Trim();
        FilterStatus  = (Request.QueryString["status"]  ?? "").Trim().ToUpperInvariant();
        FilterProg    = (Request.QueryString["prog"]    ?? "").Trim();
        FilterYear    = (Request.QueryString["year"]    ?? "").Trim();
        FilterSession = (Request.QueryString["session"] ?? "").Trim().ToUpperInvariant();
        FilterPage    = Math.Max(1, SafeInt(Request.QueryString["page"], 1));
        FilterSize    = Math.Min(500, Math.Max(10, SafeInt(Request.QueryString["size"], 50)));

        LoadStats();
        LoadProgrammeOptions();
        LoadYearOptions();
        LoadList();
    }

    // ════════════════════════════════════════════════════════════════════════
    // SERVER-SIDE RENDER — STATS
    // ════════════════════════════════════════════════════════════════════════
    private void LoadStats()
    {
        long total = 0, draft = 0, submitted = 0, review = 0, admitted = 0, rejected = 0, withDocs = 0;
        try
        {
            using (var conn = Open())
            {
                if (ColumnExists(conn, "acad_applications", "app_status"))
                {
                    bool hasUserId = ColumnExists(conn, "acad_applications", "applicant_user_id");
                    string onlineFilter = hasUserId
                        ? "WHERE a.applicant_user_id IS NOT NULL"
                        : "WHERE a.app_status IS NOT NULL";
                    string sql = @"
                        SELECT
                            COUNT(*) AS total,
                            SUM(CASE WHEN IFNULL(a.app_status,'DRAFT')='DRAFT'      THEN 1 ELSE 0 END) AS draft,
                            SUM(CASE WHEN a.app_status='SUBMITTED'                   THEN 1 ELSE 0 END) AS submitted,
                            SUM(CASE WHEN a.app_status='UNDER_REVIEW'                THEN 1 ELSE 0 END) AS review,
                            SUM(CASE WHEN a.app_status='ADMITTED'                    THEN 1 ELSE 0 END) AS admitted,
                            SUM(CASE WHEN a.app_status IN('REJECTED','WITHDRAWN')    THEN 1 ELSE 0 END) AS rejected,
                            SUM(CASE WHEN dc.cnt > 0 THEN 1 ELSE 0 END)             AS with_docs
                        FROM acad_applications a
                        LEFT JOIN (SELECT stud_entry_no, COUNT(*) AS cnt FROM apply_documents GROUP BY stud_entry_no) dc
                                  ON dc.stud_entry_no = a.stud_entry_no
                        " + onlineFilter;
                    using (var cmd = new MySqlCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            total     = SafeLong(r["total"]);
                            draft     = SafeLong(r["draft"]);
                            submitted = SafeLong(r["submitted"]);
                            review    = SafeLong(r["review"]);
                            admitted  = SafeLong(r["admitted"]);
                            rejected  = SafeLong(r["rejected"]);
                            withDocs  = SafeLong(r["with_docs"]);
                        }
                    }
                }
            }
        }
        catch { /* show zeros on DB error */ }

        var sb = new StringBuilder();
        sb.Append("<div class=\"oa-stats\">");
        sb.AppendFormat("<div class=\"oa-stat oa-stat--total\"><div class=\"oa-stat__label\">Total</div><div class=\"oa-stat__value\">{0:N0}</div></div>", total);
        sb.AppendFormat("<div class=\"oa-stat oa-stat--draft\"><div class=\"oa-stat__label\">Draft</div><div class=\"oa-stat__value\">{0:N0}</div></div>", draft);
        sb.AppendFormat("<div class=\"oa-stat oa-stat--submitted\"><div class=\"oa-stat__label\">Submitted</div><div class=\"oa-stat__value\">{0:N0}</div></div>", submitted);
        sb.AppendFormat("<div class=\"oa-stat oa-stat--review\"><div class=\"oa-stat__label\">Under Review</div><div class=\"oa-stat__value\">{0:N0}</div></div>", review);
        sb.AppendFormat("<div class=\"oa-stat oa-stat--admitted\"><div class=\"oa-stat__label\">Admitted</div><div class=\"oa-stat__value\">{0:N0}</div></div>", admitted);
        sb.AppendFormat("<div class=\"oa-stat oa-stat--rejected\"><div class=\"oa-stat__label\">Rejected / Withdrawn</div><div class=\"oa-stat__value\">{0:N0}</div></div>", rejected);
        sb.AppendFormat("<div class=\"oa-stat oa-stat--docs\"><div class=\"oa-stat__label\">With Documents</div><div class=\"oa-stat__value\">{0:N0}</div></div>", withDocs);
        sb.Append("</div>");
        litStats.Text = sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════════
    // SERVER-SIDE RENDER — PROGRAMME DROPDOWN OPTIONS
    // ════════════════════════════════════════════════════════════════════════
    private void LoadProgrammeOptions()
    {
        var sb = new StringBuilder();
        try
        {
            using (var conn = Open())
            using (var cmd = new MySqlCommand(
                "SELECT progcode, progname FROM acad_programme ORDER BY progname", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    string code = r["progcode"].ToString();
                    string name = r["progname"].ToString();
                    string sel  = string.Equals(FilterProg, code, StringComparison.OrdinalIgnoreCase)
                                  ? " selected=\"selected\"" : "";
                    sb.AppendFormat("<option value=\"{0}\"{1}>{2}</option>",
                        HtmlAE(code), sel, HE(name));
                }
            }
        }
        catch { }
        litProgOptions.Text = sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════════
    // SERVER-SIDE RENDER — YEAR DROPDOWN OPTIONS
    // ════════════════════════════════════════════════════════════════════════
    private void LoadYearOptions()
    {
        var sb  = new StringBuilder();
        int now = DateTime.Now.Year;
        for (int y = now; y >= now - 10; y--)
        {
            string sel = (FilterYear == y.ToString()) ? " selected=\"selected\"" : "";
            sb.AppendFormat("<option value=\"{0}\"{1}>{0}</option>", y, sel);
        }
        litYearOptions.Text = sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════════
    // SERVER-SIDE RENDER — APPLICATION LIST + PAGER
    // ════════════════════════════════════════════════════════════════════════
    private void LoadList()
    {
        var sbRows  = new StringBuilder();
        long total  = 0;
        int  offset = (FilterPage - 1) * FilterSize;

        // Build WHERE clause from filters
        var where = new List<string>();
        var parms = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(FilterQ))
        {
            where.Add("(a.stud_name LIKE @q OR a.stud_entry_no LIKE @q OR a.stud_email LIKE @q)");
            parms["@q"] = "%" + FilterQ + "%";
        }
        if (!string.IsNullOrEmpty(FilterProg))
        {
            where.Add("c.prog_id = @prog");
            parms["@prog"] = FilterProg;
        }
        if (!string.IsNullOrEmpty(FilterYear))
        {
            where.Add("a.stud_entry_year = @year");
            parms["@year"] = FilterYear;
        }
        if (!string.IsNullOrEmpty(FilterSession))
        {
            where.Add("c.adm_session = @session");
            parms["@session"] = FilterSession;
        }
        switch (FilterStatus)
        {
            case "DRAFT":        where.Add("IFNULL(a.app_status,'DRAFT')='DRAFT'"); break;
            case "SUBMITTED":    where.Add("a.app_status='SUBMITTED'");             break;
            case "UNDER_REVIEW": where.Add("a.app_status='UNDER_REVIEW'");          break;
            case "ADMITTED":     where.Add("a.app_status='ADMITTED'");              break;
            case "REJECTED":     where.Add("a.app_status='REJECTED'");              break;
            case "WITHDRAWN":    where.Add("a.app_status='WITHDRAWN'");             break;
        }
        try
        {
            using (var conn = Open())
            {
                bool hasAppStatus   = ColumnExists(conn, "acad_applications", "app_status");
                bool hasSubmittedAt = ColumnExists(conn, "acad_applications", "app_submitted_at");
                bool hasUserId      = ColumnExists(conn, "acad_applications", "applicant_user_id");

                if (!hasAppStatus)
                {
                    sbRows.Append("<tr><td colspan=\"11\" class=\"oa-empty\">The portal application schema is not yet initialised. Applications will appear here once students begin applying online.</td></tr>");
                    litTableRows.Text = sbRows.ToString();
                    litCount.Text     = "0 records";
                    litPager.Text     = "";
                    return;
                }

                // Always restrict to online portal applications only
                if (hasUserId)
                    where.Insert(0, "a.applicant_user_id IS NOT NULL");
                else
                    where.Insert(0, "a.app_status IS NOT NULL");

                string wClause2 = "WHERE " + string.Join(" AND ", where.ToArray());

                string subCol = hasSubmittedAt ? "a.app_submitted_at" : "NULL";
                bool hasPhone  = ColumnExists(conn, "acad_applications", "stud_phone");
                bool hasIntake = ColumnExists(conn, "acad_applications", "stud_intake");
                bool hasPayments = TableExists(conn, "apply_payments");
                string phoneX  = hasPhone  ? "COALESCE(a.stud_phone,'')"  : "''";
                string intakeX = hasIntake ? "COALESCE(a.stud_intake,'')" : "''";
                string payJoin = hasPayments
                    ? "LEFT JOIN (SELECT stud_entry_no, status FROM apply_payments) pay ON pay.stud_entry_no = a.stud_entry_no"
                    : "";
                string payCol  = hasPayments ? "COALESCE(pay.status,'')" : "''";

                string countSql = @"
                    SELECT COUNT(*)
                    FROM acad_applications a
                    LEFT JOIN acad_applicant_choices c ON c.stud_entry_no = a.stud_entry_no AND c.Choice = 1
                    LEFT JOIN acad_programme p ON p.progcode = c.prog_id
                    " + wClause2;

                string listSql = @"
                    SELECT
                        a.stud_entry_no AS eno,
                        TRIM(COALESCE(a.stud_name,
                             CONCAT(COALESCE(a.stud_surname,''),' ',COALESCE(a.stud_other_names,'')))) AS name,
                        " + phoneX  + @" AS phone,
                        COALESCE(p.progname, c.prog_id, '')                     AS programme,
                        COALESCE(c.adm_session, '')                             AS session,
                        " + intakeX + @"                                        AS intake,
                        COALESCE(a.stud_entry_year, '')                         AS year,
                        IFNULL(a.app_status,'DRAFT')                            AS status,
                        COALESCE(dc.cnt, 0)                                     AS doc_count,
                        " + payCol + @"                                         AS pay_status,
                        " + subCol + @"                                         AS submitted_at
                    FROM acad_applications a
                    LEFT JOIN acad_applicant_choices c ON c.stud_entry_no = a.stud_entry_no AND c.Choice = 1
                    LEFT JOIN acad_programme p ON p.progcode = c.prog_id
                    LEFT JOIN (SELECT stud_entry_no, COUNT(*) AS cnt FROM apply_documents GROUP BY stud_entry_no) dc
                              ON dc.stud_entry_no = a.stud_entry_no
                    " + payJoin + @"
                    " + wClause2 + @"
                    ORDER BY a.stud_entry_no DESC
                    LIMIT @offset, @size";

                using (var countCmd = new MySqlCommand(countSql, conn))
                {
                    foreach (var kv in parms) countCmd.Parameters.AddWithValue(kv.Key, kv.Value);
                    object cv = countCmd.ExecuteScalar();
                    if (cv != null && cv != DBNull.Value) total = Convert.ToInt64(cv);
                }

                using (var cmd = new MySqlCommand(listSql, conn))
                {
                    cmd.CommandTimeout = 30;
                    cmd.Parameters.AddWithValue("@offset", offset);
                    cmd.Parameters.AddWithValue("@size",   FilterSize);
                    foreach (var kv in parms) cmd.Parameters.AddWithValue(kv.Key, kv.Value);

                    bool hasRows = false;
                    int  rowNum  = offset + 1;

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            hasRows = true;
                            string eno      = r["eno"].ToString();
                            string name     = r["name"].ToString().Trim();
                            string phone    = r["phone"].ToString().Trim();
                            string prog     = r["programme"].ToString();
                            string sess     = r["session"].ToString();
                            string intake   = r["intake"].ToString().Trim();
                            string yr       = r["year"].ToString();
                            string status   = r["status"].ToString();
                            int    docCount = SafeInt(r["doc_count"].ToString(), 0);
                            string payStat  = r["pay_status"].ToString();

                            string submAt = "";
                            object saVal  = r["submitted_at"];
                            DateTime saDate;
                            if (saVal != null && saVal != DBNull.Value &&
                                DateTime.TryParse(saVal.ToString(), out saDate))
                                submAt = saDate.ToString("dd MMM yyyy");

                            // Clicking anywhere on the row (except the actions cell) opens detail
                            sbRows.AppendFormat("<tr class=\"oa-clickrow\" onclick=\"openDetail('{0}')\" title=\"Click to view full details\">", JsStr(eno));
                            sbRows.AppendFormat("<td style=\"color:#bbb;font-size:10px;text-align:center;\">{0}</td>", rowNum);
                            sbRows.AppendFormat("<td><span class=\"oa-eno\">{0}</span></td>", HE(eno));
                            // Name + phone stacked
                            sbRows.AppendFormat(
                                "<td><span class=\"oa-name\">{0}</span>{1}</td>",
                                HE(name),
                                phone.Length > 0
                                    ? string.Format("<br/><span class=\"oa-secondary\">{0}</span>", HE(phone))
                                    : "");
                            sbRows.AppendFormat("<td class=\"oa-prog-cell\">{0}</td>", HE(prog));
                            // Session + Intake stacked
                            sbRows.AppendFormat(
                                "<td>{0}{1}</td>",
                                HE(sess),
                                intake.Length > 0
                                    ? string.Format("<br/><span class=\"oa-secondary\">{0}</span>", HE(intake))
                                    : "");
                            sbRows.AppendFormat("<td>{0}</td>", HE(yr));
                            sbRows.AppendFormat("<td>{0}</td>", StatusBadgeHtml(status));
                            sbRows.AppendFormat("<td style=\"text-align:center;\">{0}</td>", DocCountBadge(docCount));
                            sbRows.AppendFormat("<td style=\"text-align:center;\">{0}</td>", PaymentBadgeHtml(status, payStat));
                            sbRows.AppendFormat("<td class=\"oa-date-cell\">{0}</td>",
                                submAt.Length > 0 ? HE(submAt) : "&#8212;");

                            // ── Row ⋮ menu (stopPropagation so row-click doesn't fire) ──
                            sbRows.Append("<td onclick=\"event.stopPropagation()\"><div class=\"oa-actions-cell\">");
                            sbRows.Append("<div class=\"oa-row-menu-wrap\">");
                            sbRows.Append("<button type=\"button\" class=\"oa-row-trigger\" onclick=\"oaToggleRowMenu(this)\" title=\"Actions\">&#8942;</button>");
                            sbRows.Append("<div class=\"oa-row-menu\">");

                            sbRows.AppendFormat(
                                "<button type=\"button\" class=\"oa-row-menu__item\" onclick=\"oaCloseRowMenus();openDetail('{0}')\">" +
                                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"13\" height=\"13\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\"><path d=\"M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z\"/><circle cx=\"12\" cy=\"12\" r=\"3\"/></svg>View Details</button>",
                                JsStr(eno));
                            sbRows.AppendFormat(
                                "<a href=\"NewStudentRegistration.aspx?eno={0}&returnUrl={1}\" class=\"oa-row-menu__item oa-row-menu__item--edit\">" +
                                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"13\" height=\"13\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\"><path d=\"M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7\"/><path d=\"M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z\"/></svg>Edit Application</a>",
                                Uri.EscapeDataString(eno),
                                Uri.EscapeDataString("OnlineApplicationsController.aspx"));

                            if (status == "SUBMITTED" || status == "UNDER_REVIEW")
                            {
                                sbRows.Append("<div class=\"oa-row-menu__sep\"></div>");
                                if (status == "SUBMITTED")
                                    sbRows.AppendFormat(
                                        "<button type=\"button\" class=\"oa-row-menu__item\" onclick=\"oaCloseRowMenus();moveToReview('{0}','{1}')\">" +
                                        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"13\" height=\"13\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\"><path d=\"M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z\"/><circle cx=\"12\" cy=\"12\" r=\"3\"/></svg>Move to Review</button>",
                                        JsStr(eno), JsStr(name));
                                sbRows.AppendFormat(
                                    "<button type=\"button\" class=\"oa-row-menu__item oa-row-menu__item--success\" onclick=\"oaCloseRowMenus();admitOne('{0}','{1}')\">" +
                                    "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"13\" height=\"13\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.5\"><polyline points=\"20 6 9 17 4 12\"/></svg>Admit</button>",
                                    JsStr(eno), JsStr(name));
                                sbRows.AppendFormat(
                                    "<button type=\"button\" class=\"oa-row-menu__item oa-row-menu__item--danger\" onclick=\"oaCloseRowMenus();rejectOne('{0}','{1}')\">" +
                                    "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"13\" height=\"13\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.5\"><line x1=\"18\" y1=\"6\" x2=\"6\" y2=\"18\"/><line x1=\"6\" y1=\"6\" x2=\"18\" y2=\"18\"/></svg>Reject</button>",
                                    JsStr(eno), JsStr(name));
                            }

                            sbRows.Append("</div></div></div></td></tr>");
                            rowNum++;
                        }
                    }

                    if (!hasRows)
                        sbRows.Append("<tr><td colspan=\"11\" class=\"oa-empty\">No applications match the selected filters.</td></tr>");
                }
            }
        }
        catch (Exception ex)
        {
            sbRows.Append("<tr><td colspan=\"11\" class=\"oa-empty\" style=\"color:#c62828;\">Error loading data: " + HE(ex.Message) + "</td></tr>");
        }

        litTableRows.Text = sbRows.ToString();
        litCount.Text = string.Format("{0:N0} record{1}", total, total == 1 ? "" : "s");

        // ── Pager ────────────────────────────────────────────────────────
        int  pages = Math.Max(1, (int)Math.Ceiling((double)total / FilterSize));
        long from  = total > 0 ? (long)offset + 1 : 0;
        long to    = Math.Min(total, (long)offset + FilterSize);

        var sbPager = new StringBuilder();
        sbPager.Append("<div class=\"oa-pager\">");
        if (total > 0)
            sbPager.AppendFormat("<span>Showing {0:N0}&#8211;{1:N0} of {2:N0} record{3}</span>",
                from, to, total, total == 1 ? "" : "s");
        else
            sbPager.Append("<span>0 records</span>");

        sbPager.Append("<div class=\"oa-pager__nav\">");

        if (FilterPage > 1)
            sbPager.AppendFormat("<a href=\"{0}\" class=\"oa-pager__btn\">&larr; Prev</a>",
                HtmlAE(PageUrl(FilterPage - 1)));
        else
            sbPager.Append("<span class=\"oa-pager__btn\" style=\"opacity:.4;cursor:default;\">&larr; Prev</span>");

        sbPager.AppendFormat(
            "<span style=\"padding:4px 10px;font-size:11px;color:#555;\">Page {0} of {1}</span>",
            FilterPage, pages);

        if (FilterPage < pages)
            sbPager.AppendFormat("<a href=\"{0}\" class=\"oa-pager__btn\">Next &rarr;</a>",
                HtmlAE(PageUrl(FilterPage + 1)));
        else
            sbPager.Append("<span class=\"oa-pager__btn\" style=\"opacity:.4;cursor:default;\">Next &rarr;</span>");

        sbPager.Append("</div></div>");
        litPager.Text = sbPager.ToString();
    }

    // Builds a page URL preserving all active filters
    private string PageUrl(int page)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(FilterQ))       parts.Add("q="       + Uri.EscapeDataString(FilterQ));
        if (!string.IsNullOrEmpty(FilterStatus))  parts.Add("status="  + Uri.EscapeDataString(FilterStatus));
        if (!string.IsNullOrEmpty(FilterProg))    parts.Add("prog="    + Uri.EscapeDataString(FilterProg));
        if (!string.IsNullOrEmpty(FilterYear))    parts.Add("year="    + Uri.EscapeDataString(FilterYear));
        if (!string.IsNullOrEmpty(FilterSession)) parts.Add("session=" + Uri.EscapeDataString(FilterSession));
        if (FilterSize != 50) parts.Add("size=" + FilterSize);
        if (page > 1) parts.Add("page=" + page);
        return "OnlineApplicationsController.aspx" +
               (parts.Count > 0 ? "?" + string.Join("&", parts.ToArray()) : "");
    }

    private static string StatusBadgeHtml(string s)
    {
        string cls, label;
        switch (s)
        {
            case "SUBMITTED":    cls = "oa-badge--submitted";    label = "Submitted";    break;
            case "UNDER_REVIEW": cls = "oa-badge--under_review"; label = "Under Review"; break;
            case "ADMITTED":     cls = "oa-badge--admitted";     label = "Admitted";     break;
            case "REJECTED":     cls = "oa-badge--rejected";     label = "Rejected";     break;
            case "WITHDRAWN":    cls = "oa-badge--withdrawn";    label = "Withdrawn";    break;
            default:             cls = "oa-badge--draft";        label = "Draft";        break;
        }
        return "<span class=\"oa-badge " + cls + "\">" + HE(label) + "</span>";
    }

    private static string DocCountBadge(int n)
    {
        if (n == 0)
            return "<span class=\"oa-doc-badge oa-doc-badge--none\">0 docs</span>";
        if (n < 3)
            return string.Format("<span class=\"oa-doc-badge oa-doc-badge--few\">{0} doc{1}</span>", n, n == 1 ? "" : "s");
        return string.Format("<span class=\"oa-doc-badge oa-doc-badge--ok\">{0} docs</span>", n);
    }

    // Payment-proof status badge for the list. Shows a muted dash for pre-submission
    // states with no proof yet; an "Unpaid" flag once the application is submitted.
    private static string PaymentBadgeHtml(string appStatus, string payStatus)
    {
        switch ((payStatus ?? "").Trim().ToUpperInvariant())
        {
            case "VERIFIED": return "<span class=\"oa-pay-badge oa-pay-badge--verified\">Verified</span>";
            case "REJECTED": return "<span class=\"oa-pay-badge oa-pay-badge--rejected\">Rejected</span>";
            case "PENDING":  return "<span class=\"oa-pay-badge oa-pay-badge--pending\">Pending</span>";
        }
        string a = (appStatus ?? "").Trim().ToUpperInvariant();
        if (a == "SUBMITTED" || a == "UNDER_REVIEW" || a == "ADMITTED")
            return "<span class=\"oa-pay-badge oa-pay-badge--none\">Unpaid</span>";
        return "<span style=\"color:#ccc;\">&#8212;</span>";
    }

    // ════════════════════════════════════════════════════════════════════════
    // AJAX — DETAIL
    // ════════════════════════════════════════════════════════════════════════
    private void HandleDetail()
    {
        string eno = (Request.QueryString["eno"] ?? "").Trim();
        if (string.IsNullOrEmpty(eno)) throw new Exception("Entry number required.");

        using (var conn = Open())
        {
            bool hasSubmittedAt   = ColumnExists(conn, "acad_applications", "app_submitted_at");
            bool hasUpdatedAt     = ColumnExists(conn, "acad_applications", "app_last_updated_at");
            bool hasAppStatus     = ColumnExists(conn, "acad_applications", "app_status");
            bool hasReviewerNotes = ColumnExists(conn, "acad_applications", "app_reviewer_notes");
            bool hasSurname       = ColumnExists(conn, "acad_applications", "stud_surname");
            bool hasNatId         = ColumnExists(conn, "acad_applications", "stud_id_number");
            bool hasNatIdLeg      = !hasNatId && ColumnExists(conn, "acad_applications", "national_id");
            bool hasEmerg         = ColumnExists(conn, "acad_applications", "emergency_contact_name");
            bool hasSponsorC      = ColumnExists(conn, "acad_applications", "sponsor_contact");
            bool hasDistrict      = ColumnExists(conn, "acad_applications", "home_district");
            bool hasPoBox         = ColumnExists(conn, "acad_applications", "post_box");
            bool hasCountry       = ColumnExists(conn, "acad_applications", "residence_country");
            bool hasTitle         = ColumnExists(conn, "acad_applications", "title");
            bool hasMethod        = ColumnExists(conn, "acad_applications", "stud_entry_method");
            bool hasMarital       = ColumnExists(conn, "acad_applications", "stud_mar_stat");
            bool hasRefName       = ColumnExists(conn, "acad_applications", "referee_name");
            bool hasRefCon        = ColumnExists(conn, "acad_applications", "referee_contacts");
            bool hasKinRel        = ColumnExists(conn, "acad_applications", "kin_relationship");
            bool hasKinCon        = ColumnExists(conn, "acad_applications", "kin_contacts");

            // Education columns — all optional; portal stores year in stud_pob, agg in stud_district, etc.
            bool hasOlSchool  = ColumnExists(conn, "acad_applications", "olevel_school");
            bool hasOlIndex   = ColumnExists(conn, "acad_applications", "olevel_index");
            bool hasOlYear    = ColumnExists(conn, "acad_applications", "olevel_year");
            bool hasOlYearFb  = !hasOlYear  && ColumnExists(conn, "acad_applications", "stud_pob");
            bool hasOlAgg     = ColumnExists(conn, "acad_applications", "olevel_agg");
            bool hasOlAggFb   = !hasOlAgg   && ColumnExists(conn, "acad_applications", "stud_district");
            bool hasAlSchool  = ColumnExists(conn, "acad_applications", "alevel_school");
            bool hasAlIndex   = ColumnExists(conn, "acad_applications", "alevel_index");
            bool hasAlYear    = ColumnExists(conn, "acad_applications", "alevel_year");
            bool hasAlPts     = ColumnExists(conn, "acad_applications", "alevel_points");
            bool hasAlPtsFb   = !hasAlPts   && ColumnExists(conn, "acad_applications", "stud_ward");
            bool hasOtherInst = ColumnExists(conn, "acad_applications", "other_institution");
            bool hasOtherInstFb = !hasOtherInst && ColumnExists(conn, "acad_applications", "stud_prevcampus");
            bool hasOtherQual = ColumnExists(conn, "acad_applications", "other_qualification");
            bool hasOtherQualFb = !hasOtherQual && ColumnExists(conn, "acad_applications", "stud_lg");
            bool hasOtherYear = ColumnExists(conn, "acad_applications", "other_year");
            bool hasOtherYearFb = !hasOtherYear && ColumnExists(conn, "acad_applications", "stud_village");
            bool hasOtherGrade = ColumnExists(conn, "acad_applications", "other_grade");
            bool hasOtherGradeFb = !hasOtherGrade && ColumnExists(conn, "acad_applications", "stud_county");

            string olSchoolX = hasOlSchool  ? "COALESCE(a.olevel_school,'')" : "''";
            string olIndexX  = hasOlIndex   ? "COALESCE(a.olevel_index,'')"  : "''";
            string olYearX   = hasOlYear    ? "COALESCE(a.olevel_year,'')"   : (hasOlYearFb  ? "COALESCE(a.stud_pob,'')"       : "''");
            string olAggX    = hasOlAgg     ? "COALESCE(a.olevel_agg,'')"    : (hasOlAggFb   ? "COALESCE(a.stud_district,'')"  : "''");
            string alSchoolX = hasAlSchool  ? "COALESCE(a.alevel_school,'')" : "''";
            string alIndexX  = hasAlIndex   ? "COALESCE(a.alevel_index,'')"  : "''";
            string alYearX   = hasAlYear    ? "COALESCE(a.alevel_year,'')"   : "''";
            string alPtsX    = hasAlPts     ? "COALESCE(a.alevel_points,'')" : (hasAlPtsFb   ? "COALESCE(a.stud_ward,'')"      : "''");
            string otInstX   = hasOtherInst ? "COALESCE(a.other_institution,'')" : (hasOtherInstFb  ? "COALESCE(a.stud_prevcampus,'')" : "''");
            string otQualX   = hasOtherQual ? "COALESCE(a.other_qualification,'')" : (hasOtherQualFb ? "COALESCE(a.stud_lg,'')"         : "''");
            string otYearX   = hasOtherYear ? "COALESCE(a.other_year,'')"    : (hasOtherYearFb  ? "COALESCE(a.stud_village,'')"  : "''");
            string otGradeX  = hasOtherGrade ? "COALESCE(a.other_grade,'')"  : (hasOtherGradeFb ? "COALESCE(a.stud_county,'')"   : "''");


            string natIdX    = hasNatId    ? "COALESCE(a.stud_id_number,'')" : (hasNatIdLeg ? "COALESCE(a.national_id,'')" : "''");
            string sponsorCX = hasSponsorC ? "COALESCE(a.sponsor_contact,'')" : "''";
            string distX     = hasDistrict ? "COALESCE(a.home_district,'')"   : "''";
            string pbX       = hasPoBox    ? "COALESCE(a.post_box,'')"        : "''";
            string cntX      = hasCountry  ? "COALESCE(a.residence_country,'')" : "''";
            string titleX    = hasTitle    ? "COALESCE(a.title,'')"           : "''";
            string methX     = hasMethod   ? "COALESCE(a.stud_entry_method,'')" : "''";
            string maritalX  = hasMarital  ? "COALESCE(a.stud_mar_stat,'')"  : "''";
            string refNmX    = hasRefName  ? "COALESCE(a.referee_name,'')"   : "''";
            string refCnX    = hasRefCon   ? "COALESCE(a.referee_contacts,'')" : "''";
            string kinRelX   = hasKinRel   ? "COALESCE(a.kin_relationship,'')" : "''";
            string kinConX   = hasKinCon   ? "COALESCE(a.kin_contacts,'')"   : "''";

            string nameExpr = hasSurname
                ? "TRIM(CONCAT(COALESCE(a.stud_surname,''),' ',COALESCE(a.stud_other_names,'')))"
                : "COALESCE(a.stud_name,'')";

            string sql = @"
                SELECT
                    a.stud_entry_no AS eno,
                    " + nameExpr + @" AS full_name,
                    COALESCE(a.stud_email,'')  AS email,
                    COALESCE(a.stud_phone,'')  AS phone,
                    COALESCE(DATE_FORMAT(a.stud_birthdate,'%d/%m/%Y'),'') AS dob,
                    COALESCE(a.stud_sex,'')         AS gender,
                    COALESCE(a.stud_nationality,'') AS nationality,
                    COALESCE(a.stud_religion,'')    AS religion,
                    " + maritalX + @"               AS marital,
                    COALESCE(a.physicalDisability,'') AS disability,
                    " + titleX + @"                 AS title,
                    COALESCE(a.stud_phy_address,'') AS address,
                    " + distX + @"                  AS district,
                    " + pbX   + @"                  AS pobox,
                    " + cntX  + @"                  AS country,
                    " + olSchoolX + @"               AS olevel_school,
                    " + olIndexX  + @"               AS olevel_index,
                    " + olYearX   + @"               AS olevel_year,
                    " + olAggX    + @"               AS olevel_agg,
                    " + alSchoolX + @"               AS alevel_school,
                    " + alIndexX  + @"               AS alevel_index,
                    " + alYearX   + @"               AS alevel_year,
                    " + alPtsX    + @"               AS alevel_points,
                    " + otInstX   + @"               AS other_inst,
                    " + otQualX   + @"               AS other_qual,
                    " + otYearX   + @"               AS other_year,
                    " + otGradeX  + @"               AS other_grade,
                    COALESCE(a.stud_sponsor,'')     AS sponsor,
                    " + sponsorCX + @"              AS sponsor_contact,
                    COALESCE(a.next_kin,'')         AS kin_name,
                    " + kinRelX + @"                AS kin_relationship,
                    " + kinConX + @"                AS kin_contacts,
                    " + refNmX  + @"                AS referee_name,
                    " + refCnX  + @"                AS referee_contacts,
                    " + natIdX  + @"                AS natid,
                    COALESCE(a.stud_campus,'')      AS campus,
                    COALESCE(a.stud_intake,'')      AS intake,
                    COALESCE(a.stud_entry_year,'')  AS entry_year,
                    " + methX   + @"                AS entry_method,
                    COALESCE(c.sub_comb,'')         AS specialisation,
                    COALESCE(p.progname, c.prog_id,'') AS programme,
                    COALESCE(c.prog_id,'')           AS prog_id,
                    COALESCE(c.adm_session,'')      AS session,
                    " + (hasAppStatus     ? "IFNULL(a.app_status,'DRAFT')"         : "'DRAFT'") + @" AS app_status,
                    " + (hasSubmittedAt   ? "a.app_submitted_at"                    : "NULL")    + @" AS submitted_at,
                    " + (hasUpdatedAt     ? "a.app_last_updated_at"                 : "NULL")    + @" AS updated_at,
                    " + (hasReviewerNotes ? "COALESCE(a.app_reviewer_notes,'')"     : "''")      + @" AS reviewer_notes,
                    " + (hasEmerg ? "COALESCE(a.emergency_contact_name,'')"  : "''") + @" AS emerg_name,
                    " + (hasEmerg ? "COALESCE(a.emergency_contact_rel,'')"   : "''") + @" AS emerg_rel,
                    " + (hasEmerg ? "COALESCE(a.emergency_contact_phone,'')" : "''") + @" AS emerg_phone
                FROM acad_applications a
                LEFT JOIN acad_applicant_choices c ON c.stud_entry_no = a.stud_entry_no AND c.Choice = 1
                LEFT JOIN acad_programme p ON p.progcode = c.prog_id
                WHERE a.stud_entry_no = @eno
                LIMIT 1";

            // Billing (cross-DB, best-effort)
            string billing = "";
            try {
                using (var bc = new MySqlCommand(
                    "SELECT COALESCE(f.bs_name,'') FROM acad_applications a " +
                    "JOIN campus_dynamics_accounts.fin_billing_systems f ON f.ID=a.billingID " +
                    "WHERE a.stud_entry_no=@eno LIMIT 1", conn))
                { bc.Parameters.AddWithValue("@eno", eno); object bv = bc.ExecuteScalar(); if (bv != null && bv != DBNull.Value) billing = bv.ToString(); }
            } catch { }

            // Build the JSON body from the main reader first, then close it.
            // GetDocsJson must run AFTER the reader is disposed — MySQL does not support
            // two simultaneous active readers on the same connection (no MARS).
            var sb = new StringBuilder();
            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@eno", eno);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) throw new Exception("Application not found: " + eno);

                    sb.Append("{\"ok\":true,");
                    sb.Append("\"eno\":"              + JsonStr(r["eno"].ToString())              + ",");
                    sb.Append("\"name\":"             + JsonStr(r["full_name"].ToString().Trim()) + ",");
                    sb.Append("\"email\":"            + JsonStr(r["email"].ToString())            + ",");
                    sb.Append("\"phone\":"            + JsonStr(r["phone"].ToString())            + ",");
                    sb.Append("\"dob\":"              + JsonStr(r["dob"].ToString())              + ",");
                    sb.Append("\"gender\":"           + JsonStr(r["gender"].ToString())           + ",");
                    sb.Append("\"nationality\":"      + JsonStr(r["nationality"].ToString())      + ",");
                    sb.Append("\"natid\":"            + JsonStr(r["natid"].ToString())            + ",");
                    sb.Append("\"title\":"            + JsonStr(r["title"].ToString())            + ",");
                    sb.Append("\"religion\":"         + JsonStr(r["religion"].ToString())         + ",");
                    sb.Append("\"marital\":"          + JsonStr(r["marital"].ToString())          + ",");
                    sb.Append("\"disability\":"       + JsonStr(r["disability"].ToString())       + ",");
                    sb.Append("\"address\":"          + JsonStr(r["address"].ToString())          + ",");
                    sb.Append("\"district\":"         + JsonStr(r["district"].ToString())         + ",");
                    sb.Append("\"pobox\":"            + JsonStr(r["pobox"].ToString())            + ",");
                    sb.Append("\"country\":"          + JsonStr(r["country"].ToString())          + ",");
                    sb.Append("\"olevel_school\":"    + JsonStr(r["olevel_school"].ToString())    + ",");
                    sb.Append("\"olevel_index\":"     + JsonStr(r["olevel_index"].ToString())     + ",");
                    sb.Append("\"olevel_year\":"      + JsonStr(r["olevel_year"].ToString())      + ",");
                    sb.Append("\"olevel_agg\":"       + JsonStr(r["olevel_agg"].ToString())       + ",");
                    sb.Append("\"alevel_school\":"    + JsonStr(r["alevel_school"].ToString())    + ",");
                    sb.Append("\"alevel_index\":"     + JsonStr(r["alevel_index"].ToString())     + ",");
                    sb.Append("\"alevel_year\":"      + JsonStr(r["alevel_year"].ToString())      + ",");
                    sb.Append("\"alevel_points\":"    + JsonStr(r["alevel_points"].ToString())    + ",");
                    sb.Append("\"other_inst\":"       + JsonStr(r["other_inst"].ToString())       + ",");
                    sb.Append("\"other_qual\":"       + JsonStr(r["other_qual"].ToString())       + ",");
                    sb.Append("\"other_year\":"       + JsonStr(r["other_year"].ToString())       + ",");
                    sb.Append("\"other_grade\":"      + JsonStr(r["other_grade"].ToString())      + ",");
                    sb.Append("\"programme\":"        + JsonStr(r["programme"].ToString())        + ",");
                    sb.Append("\"prog_id\":"          + JsonStr(r["prog_id"].ToString())          + ",");
                    sb.Append("\"session\":"          + JsonStr(r["session"].ToString())          + ",");
                    sb.Append("\"campus\":"           + JsonStr(r["campus"].ToString())           + ",");
                    sb.Append("\"intake\":"           + JsonStr(r["intake"].ToString())           + ",");
                    sb.Append("\"entry_method\":"     + JsonStr(r["entry_method"].ToString())     + ",");
                    sb.Append("\"specialisation\":"   + JsonStr(r["specialisation"].ToString())   + ",");
                    sb.Append("\"billing\":"          + JsonStr(billing)                          + ",");
                    sb.Append("\"sponsor\":"          + JsonStr(r["sponsor"].ToString())          + ",");
                    sb.Append("\"sponsor_contact\":"  + JsonStr(r["sponsor_contact"].ToString())  + ",");
                    sb.Append("\"kin_name\":"         + JsonStr(r["kin_name"].ToString())         + ",");
                    sb.Append("\"kin_relationship\":"  + JsonStr(r["kin_relationship"].ToString()) + ",");
                    sb.Append("\"kin_contacts\":"     + JsonStr(r["kin_contacts"].ToString())     + ",");
                    sb.Append("\"referee_name\":"     + JsonStr(r["referee_name"].ToString())     + ",");
                    sb.Append("\"referee_contacts\":"  + JsonStr(r["referee_contacts"].ToString()) + ",");
                    sb.Append("\"entry_year\":"       + JsonStr(r["entry_year"].ToString())       + ",");
                    sb.Append("\"emerg_name\":"       + JsonStr(r["emerg_name"].ToString())       + ",");
                    sb.Append("\"emerg_rel\":"        + JsonStr(r["emerg_rel"].ToString())        + ",");
                    sb.Append("\"emerg_phone\":"      + JsonStr(r["emerg_phone"].ToString())      + ",");
                    sb.Append("\"status\":"           + JsonStr(r["app_status"].ToString())       + ",");
                    sb.Append("\"reviewer_notes\":"   + JsonStr(r["reviewer_notes"].ToString())   + ",");
                    sb.Append("\"submitted_at\":"     + JsonStr(FormatDate(r["submitted_at"]))    + ",");
                    sb.Append("\"updated_at\":"       + JsonStr(FormatDate(r["updated_at"])));
                    // docs appended AFTER reader closes (below)
                }
            } // ← reader and command disposed here — connection is now free

            // Fetch docs on the now-free connection
            sb.Append(",\"docs\":");
            sb.Append(GetDocsJson(conn, eno));
            // Proof-of-payment record (or null)
            sb.Append(",\"payment\":");
            sb.Append(GetPaymentJson(conn, eno));
            sb.Append("}");
            Response.Write(sb.ToString());
        }
    }

    private string GetDocsJson(MySqlConnection conn, string eno)
    {
        var sb = new StringBuilder("[");
        try
        {
            if (!TableExists(conn, "apply_documents")) return "[]";
            using (var cmd = new MySqlCommand(
                "SELECT id, doc_type, original_filename, file_size_bytes, " +
                "DATE_FORMAT(uploaded_at,'%d/%m/%Y %H:%i') AS uploaded_at " +
                "FROM apply_documents WHERE stud_entry_no=@eno ORDER BY uploaded_at",
                conn))
            {
                cmd.Parameters.AddWithValue("@eno", eno);
                int n = 0;
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        if (n++ > 0) sb.Append(",");
                        sb.AppendFormat("{{\"id\":{0},\"type\":{1},\"filename\":{2},\"size\":{3},\"date\":{4}}}",
                            r["id"],
                            JsonStr(r["doc_type"].ToString()),
                            JsonStr(r["original_filename"].ToString()),
                            r["file_size_bytes"],
                            JsonStr(r["uploaded_at"].ToString()));
                    }
                }
            }
        }
        catch { }
        sb.Append("]");
        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════════
    // PROOF OF PAYMENT — read JSON, verify / reject, stream receipt
    // ════════════════════════════════════════════════════════════════════════
    private string GetPaymentJson(MySqlConnection conn, string eno)
    {
        try
        {
            if (!TableExists(conn, "apply_payments")) return "null";
            using (var cmd = new MySqlCommand(
                "SELECT id, IFNULL(amount,'') AS amount, IFNULL(currency,'') AS currency, " +
                "IFNULL(payment_reference,'') AS reference, " +
                "COALESCE(DATE_FORMAT(payment_date,'%d/%m/%Y'),'') AS pdate, " +
                "IFNULL(payment_method,'') AS method, IFNULL(notes,'') AS notes, " +
                "IFNULL(receipt_filename,'') AS rfile, IFNULL(receipt_path,'') AS rpath, " +
                "IFNULL(status,'PENDING') AS status, IFNULL(admin_notes,'') AS admin_notes, " +
                "COALESCE(DATE_FORMAT(created_at,'%d/%m/%Y %H:%i'),'') AS created " +
                "FROM apply_payments WHERE stud_entry_no=@eno LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@eno", eno);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return "null";
                    var sb = new StringBuilder("{");
                    sb.Append("\"id\":"        + SafeInt(r["id"], 0) + ",");
                    sb.Append("\"amount\":"    + JsonStr(r["amount"].ToString())     + ",");
                    sb.Append("\"currency\":"  + JsonStr(r["currency"].ToString())   + ",");
                    sb.Append("\"reference\":" + JsonStr(r["reference"].ToString())  + ",");
                    sb.Append("\"date\":"      + JsonStr(r["pdate"].ToString())      + ",");
                    sb.Append("\"method\":"    + JsonStr(r["method"].ToString())     + ",");
                    sb.Append("\"notes\":"     + JsonStr(r["notes"].ToString())      + ",");
                    sb.Append("\"receipt_filename\":" + JsonStr(r["rfile"].ToString()) + ",");
                    sb.Append("\"has_receipt\":" + (r["rpath"].ToString().Trim().Length > 0 ? "true" : "false") + ",");
                    sb.Append("\"status\":"    + JsonStr(r["status"].ToString())     + ",");
                    sb.Append("\"admin_notes\":" + JsonStr(r["admin_notes"].ToString()) + ",");
                    sb.Append("\"created\":"   + JsonStr(r["created"].ToString()));
                    sb.Append("}");
                    return sb.ToString();
                }
            }
        }
        catch { }
        return "null";
    }

    private void HandlePayVerify()
    {
        var data = ReadPostJson();
        string eno = GetStr(data, "eno");
        if (string.IsNullOrEmpty(eno)) throw new Exception("Entry number required.");
        using (var conn = Open())
        {
            if (!TableExists(conn, "apply_payments")) throw new Exception("No payment records exist yet.");
            int n = UpdatePaymentStatus(conn, eno, "VERIFIED", null);
            if (n == 0) throw new Exception("No proof of payment found for this application.");
            WriteAuditLog(conn, eno, "PAYMENT_VERIFIED", "Application fee verified by " + GetCurrentUser());
        }
        Response.Write("{\"ok\":true}");
    }

    private void HandlePayReject()
    {
        var data   = ReadPostJson();
        string eno    = GetStr(data, "eno");
        string reason = GetStr(data, "reason");
        if (string.IsNullOrEmpty(eno))    throw new Exception("Entry number required.");
        if (string.IsNullOrEmpty(reason)) throw new Exception("A reason is required.");
        using (var conn = Open())
        {
            if (!TableExists(conn, "apply_payments")) throw new Exception("No payment records exist yet.");
            int n = UpdatePaymentStatus(conn, eno, "REJECTED", reason);
            if (n == 0) throw new Exception("No proof of payment found for this application.");
            WriteAuditLog(conn, eno, "PAYMENT_REJECTED", "Application fee rejected by " + GetCurrentUser() + ". Reason: " + reason);
        }
        Response.Write("{\"ok\":true}");
    }

    private int UpdatePaymentStatus(MySqlConnection conn, string eno, string status, string adminNotes)
    {
        var sets = new List<string> { "status=@s" };
        bool hasReviewedAt = ColumnExists(conn, "apply_payments", "reviewed_at");
        bool hasAdminNotes = ColumnExists(conn, "apply_payments", "admin_notes");
        if (hasReviewedAt) sets.Add("reviewed_at=@now");
        if (adminNotes != null && hasAdminNotes) sets.Add("admin_notes=@an");

        using (var cmd = new MySqlCommand(
            "UPDATE apply_payments SET " + string.Join(",", sets.ToArray()) + " WHERE stud_entry_no=@eno", conn))
        {
            cmd.Parameters.AddWithValue("@s", status);
            if (hasReviewedAt) cmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
            if (adminNotes != null && hasAdminNotes) cmd.Parameters.AddWithValue("@an", adminNotes);
            cmd.Parameters.AddWithValue("@eno", eno);
            return cmd.ExecuteNonQuery();
        }
    }

    // Streams the applicant's uploaded receipt file (shared upload folder).
    private void HandleViewReceipt()
    {
        if (!IsAuthenticatedStaff()) { Response.StatusCode = 403; Response.End(); return; }
        string eno = (Request.QueryString["eno"] ?? "").Trim();
        if (string.IsNullOrEmpty(eno)) { Response.StatusCode = 400; Response.End(); return; }

        string path = null, fname = null;
        try
        {
            using (var conn = Open())
            {
                if (!TableExists(conn, "apply_payments")) { Response.StatusCode = 404; Response.End(); return; }
                using (var cmd = new MySqlCommand(
                    "SELECT IFNULL(receipt_path,''), IFNULL(receipt_filename,'') FROM apply_payments WHERE stud_entry_no=@eno LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@eno", eno);
                    using (var r = cmd.ExecuteReader())
                        if (r.Read()) { path = r.GetString(0); fname = r.GetString(1); }
                }
            }
        }
        catch { Response.StatusCode = 500; Response.End(); return; }

        if (string.IsNullOrWhiteSpace(path)) { Response.StatusCode = 404; Response.End(); return; }

        string full = System.IO.Path.IsPathRooted(path)
            ? path
            : Server.MapPath("~/" + path.Replace("/", "\\").TrimStart('\\'));
        if (!System.IO.File.Exists(full)) { Response.StatusCode = 404; Response.End(); return; }

        string ext = System.IO.Path.GetExtension(full).ToLowerInvariant();
        string ct = ext == ".pdf" ? "application/pdf" : (ext == ".png" ? "image/png" : "image/jpeg");
        string dl = string.IsNullOrWhiteSpace(fname) ? ("receipt" + ext) : fname.Replace("\"", "'");

        Response.Clear();
        Response.ContentType = ct;
        Response.AddHeader("Content-Disposition", "inline; filename=\"" + dl + "\"");
        Response.TransmitFile(full);
        Response.End();
    }

    // ════════════════════════════════════════════════════════════════════════
    // AJAX — EDIT APPLICATION DATA (never touches app_status / stud_reg_no)
    // ════════════════════════════════════════════════════════════════════════
    private void HandleEdit()
    {
        var data = ReadPostJson();
        string eno = GetStr(data, "eno");
        if (string.IsNullOrEmpty(eno)) throw new Exception("Entry number required.");

        using (var conn = Open())
        {
            var sets  = new List<string>();
            var parms = new Dictionary<string, object>();

            sets.Add("stud_name=@nm");         parms["@nm"]  = GetStr(data, "name");
            sets.Add("stud_sex=@sx");          parms["@sx"]  = GetStr(data, "sex");
            sets.Add("stud_nationality=@nat"); parms["@nat"] = GetStr(data, "nationality");
            sets.Add("stud_religion=@rel");    parms["@rel"] = GetStr(data, "religion");
            sets.Add("stud_mar_stat=@mar");    parms["@mar"] = GetStr(data, "marital");
            sets.Add("stud_phone=@phn");       parms["@phn"] = GetStr(data, "phone");
            sets.Add("stud_email=@eml");       parms["@eml"] = GetStr(data, "email");
            sets.Add("stud_phy_address=@adr"); parms["@adr"] = GetStr(data, "address");
            sets.Add("stud_sponsor=@spn");     parms["@spn"] = GetStr(data, "sponsor");
            sets.Add("stud_campus=@cp");       parms["@cp"]  = GetStr(data, "campus");
            sets.Add("stud_intake=@itk");      parms["@itk"] = GetStr(data, "intake");
            sets.Add("physicalDisability=@di");parms["@di"]  = GetStr(data, "disability");
            sets.Add("next_kin=@kin");         parms["@kin"] = GetStr(data, "kin_name");
            sets.Add("kin_relationship=@kr");  parms["@kr"]  = GetStr(data, "kin_relationship");
            sets.Add("kin_contacts=@kc");      parms["@kc"]  = GetStr(data, "kin_contacts");

            string entryYr = GetStr(data, "entry_year");
            if (!string.IsNullOrEmpty(entryYr)) { sets.Add("stud_entry_year=@yr"); parms["@yr"] = entryYr; }

            string dobRaw = GetStr(data, "dob");
            if (!string.IsNullOrEmpty(dobRaw))
            {
                System.DateTime dob;
                string[] fmts = { "dd/MM/yyyy", "yyyy-MM-dd", "d/M/yyyy", "dd-MM-yyyy" };
                if (System.DateTime.TryParseExact(dobRaw, fmts,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out dob))
                { sets.Add("stud_birthdate=@dob"); parms["@dob"] = dob.ToString("yyyy-MM-dd"); }
            }

            if (ColumnExists(conn, "acad_applications", "stud_id_number"))
            { sets.Add("stud_id_number=@nid"); parms["@nid"] = GetStr(data, "natid"); }
            else if (ColumnExists(conn, "acad_applications", "national_id"))
            { sets.Add("national_id=@nid");    parms["@nid"] = GetStr(data, "natid"); }

            if (ColumnExists(conn, "acad_applications", "olevel_school"))  { sets.Add("olevel_school=@ols");  parms["@ols"] = GetStr(data, "olevel_school"); }
            if (ColumnExists(conn, "acad_applications", "olevel_index"))   { sets.Add("olevel_index=@oli");   parms["@oli"] = GetStr(data, "olevel_index"); }
            if (ColumnExists(conn, "acad_applications", "olevel_year"))    { sets.Add("olevel_year=@oly");    parms["@oly"] = GetStr(data, "olevel_year"); }
            else if (ColumnExists(conn, "acad_applications", "stud_pob"))  { sets.Add("stud_pob=@oly");       parms["@oly"] = GetStr(data, "olevel_year"); }
            if (ColumnExists(conn, "acad_applications", "olevel_agg"))     { sets.Add("olevel_agg=@ola");     parms["@ola"] = GetStr(data, "olevel_agg"); }
            else if (ColumnExists(conn, "acad_applications", "stud_district")) { sets.Add("stud_district=@ola"); parms["@ola"] = GetStr(data, "olevel_agg"); }
            if (ColumnExists(conn, "acad_applications", "alevel_school"))  { sets.Add("alevel_school=@als");  parms["@als"] = GetStr(data, "alevel_school"); }
            if (ColumnExists(conn, "acad_applications", "alevel_index"))   { sets.Add("alevel_index=@ali");   parms["@ali"] = GetStr(data, "alevel_index"); }
            if (ColumnExists(conn, "acad_applications", "alevel_year"))    { sets.Add("alevel_year=@aly");    parms["@aly"] = GetStr(data, "alevel_year"); }
            if (ColumnExists(conn, "acad_applications", "alevel_points"))  { sets.Add("alevel_points=@alp");  parms["@alp"] = GetStr(data, "alevel_points"); }
            else if (ColumnExists(conn, "acad_applications", "stud_ward")) { sets.Add("stud_ward=@alp");      parms["@alp"] = GetStr(data, "alevel_points"); }
            if (ColumnExists(conn, "acad_applications", "other_institution"))     { sets.Add("other_institution=@oi");    parms["@oi"]  = GetStr(data, "other_inst"); }
            else if (ColumnExists(conn, "acad_applications", "stud_prevcampus")) { sets.Add("stud_prevcampus=@oi");      parms["@oi"]  = GetStr(data, "other_inst"); }
            if (ColumnExists(conn, "acad_applications", "other_qualification"))   { sets.Add("other_qualification=@oq"); parms["@oq"]  = GetStr(data, "other_qual"); }
            else if (ColumnExists(conn, "acad_applications", "stud_lg"))         { sets.Add("stud_lg=@oq");             parms["@oq"]  = GetStr(data, "other_qual"); }
            if (ColumnExists(conn, "acad_applications", "other_year"))            { sets.Add("other_year=@oyr");         parms["@oyr"] = GetStr(data, "other_year"); }
            else if (ColumnExists(conn, "acad_applications", "stud_village"))    { sets.Add("stud_village=@oyr");       parms["@oyr"] = GetStr(data, "other_year"); }
            if (ColumnExists(conn, "acad_applications", "other_grade"))           { sets.Add("other_grade=@ogr");        parms["@ogr"] = GetStr(data, "other_grade"); }
            else if (ColumnExists(conn, "acad_applications", "stud_county"))     { sets.Add("stud_county=@ogr");        parms["@ogr"] = GetStr(data, "other_grade"); }

            if (ColumnExists(conn, "acad_applications", "emergency_contact_name"))
            {
                sets.Add("emergency_contact_name=@en");  parms["@en"]  = GetStr(data, "emerg_name");
                sets.Add("emergency_contact_rel=@er");   parms["@er"]  = GetStr(data, "emerg_rel");
                sets.Add("emergency_contact_phone=@ep"); parms["@ep"]  = GetStr(data, "emerg_phone");
            }
            else
            {
                sets.Add("next_kin=@kin2");       parms["@kin2"]  = GetStr(data, "emerg_name");
                sets.Add("kin_relationship=@kr2");parms["@kr2"]   = GetStr(data, "emerg_rel");
                sets.Add("kin_contacts=@kc2");    parms["@kc2"]   = GetStr(data, "emerg_phone");
            }

            if (ColumnExists(conn, "acad_applications", "app_last_updated_at"))
            { sets.Add("app_last_updated_at=@upd"); parms["@upd"] = System.DateTime.UtcNow; }

            parms["@eno"] = eno;
            using (var cmd = new MySqlCommand(
                "UPDATE acad_applications SET " + string.Join(",", sets.ToArray()) +
                " WHERE stud_entry_no=@eno", conn))
            {
                foreach (var kv in parms) cmd.Parameters.AddWithValue(kv.Key, kv.Value);
                cmd.ExecuteNonQuery();
            }

            var cSets  = new List<string>();
            var cParms = new Dictionary<string, object>();
            string progId  = GetStr(data, "prog_id");
            string session = GetStr(data, "session");
            if (!string.IsNullOrEmpty(progId))  { cSets.Add("prog_id=@pg");    cParms["@pg"] = progId; }
            if (!string.IsNullOrEmpty(session)) { cSets.Add("adm_session=@ss");cParms["@ss"] = session; }
            if (cSets.Count > 0)
            {
                cParms["@eno"] = eno;
                using (var cmd = new MySqlCommand(
                    "UPDATE acad_applicant_choices SET " + string.Join(",", cSets.ToArray()) +
                    " WHERE stud_entry_no=@eno AND Choice=1", conn))
                {
                    foreach (var kv in cParms) cmd.Parameters.AddWithValue(kv.Key, kv.Value);
                    cmd.ExecuteNonQuery();
                }
            }

            WriteAuditLog(conn, eno, "EDITED", "Application data edited by " + GetCurrentUser());
        }
        Response.Write("{\"ok\":true,\"message\":\"Application updated successfully.\"}");
    }

    // ════════════════════════════════════════════════════════════════════════
    // AJAX — REVIEW / ADMIT / REJECT / NOTE
    // ════════════════════════════════════════════════════════════════════════
    private void HandleReview()
    {
        var data = ReadPostJson();
        string eno = GetStr(data, "eno");
        if (string.IsNullOrEmpty(eno)) throw new Exception("Entry number required.");
        using (var conn = Open())
        {
            if (!ColumnExists(conn, "acad_applications", "app_status"))
                throw new Exception("app_status column not found.");
            string current = GetAppStatus(conn, eno);
            if (current != "SUBMITTED")
                throw new Exception("Application must be SUBMITTED to move to review. Current: " + current);
            UpdateStatus(conn, eno, "UNDER_REVIEW");
            WriteAuditLog(conn, eno, "UNDER_REVIEW", "Moved to under review by " + GetCurrentUser());
        }
        Response.Write("{\"ok\":true}");
    }

    private void HandleAdmit()
    {
        var data = ReadPostJson();
        string eno = GetStr(data, "eno");
        if (string.IsNullOrEmpty(eno)) throw new Exception("Entry number required.");
        using (var conn = Open())
        {
            if (!ColumnExists(conn, "acad_applications", "app_status"))
                throw new Exception("app_status column not found.");
            string current = GetAppStatus(conn, eno);
            if (current != "SUBMITTED" && current != "UNDER_REVIEW")
                throw new Exception("Application must be SUBMITTED or UNDER_REVIEW to admit. Current: " + current);
            UpdateStatus(conn, eno, "ADMITTED");
            using (var cmd = new MySqlCommand(
                "UPDATE acad_applicant_choices SET adm_status=1 WHERE stud_entry_no=@eno AND Choice=1 AND adm_status=0",
                conn))
            {
                cmd.Parameters.AddWithValue("@eno", eno);
                cmd.ExecuteNonQuery();
            }

            // Create + ACTIVATE the actual student account. Previously admitting only flipped
            // the application status, so no acad_student record was ever created and the
            // student was never active. Now we generate the reg-no, create the student record
            // (acad_RegisterApplicant) and set new_status = ACTIVE.
            ProvisionResult provision = CreateAndActivateStudent(conn, eno);

            WriteAuditLog(conn, eno, "ADMITTED", "Admitted via Online Applications by " + GetCurrentUser() + provision.Audit);
            Response.Write("{\"ok\":true,\"message\":" + JsStr(provision.Message) + "}");
        }
    }

    private struct ProvisionResult { public string Message; public string Audit; }

    /// <summary>
    /// Creates the acad_student account for an admitted applicant (idempotent) and marks it
    /// ACTIVE. Best-effort: a failure here is reported but never blocks the admit decision.
    /// </summary>
    private ProvisionResult CreateAndActivateStudent(MySqlConnection conn, string eno)
    {
        var res = new ProvisionResult();
        try
        {
            // The register SP sets acad_student.regno = the application entry-no (eno) and
            // acad_student.entryno = the generated reg-number — so we key on regno = eno.
            bool exists;
            using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM acad_student WHERE TRIM(regno)=TRIM(@eno)", conn))
            {
                cmd.Parameters.AddWithValue("@eno", eno);
                exists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }

            if (!exists)
            {
                // 1) Generate the applicant's reg-number (stored on acad_student.entryno).
                string genNo = null;
                try
                {
                    using (var cmd = new MySqlCommand("SELECT acad_RegNoCreator(@eno)", conn))
                    {
                        cmd.Parameters.AddWithValue("@eno", eno);
                        object o = cmd.ExecuteScalar();
                        if (o != null && o != DBNull.Value) genNo = o.ToString().Trim();
                    }
                }
                catch { }
                if (string.IsNullOrEmpty(genNo) || genNo == "-")
                {
                    res.Message = "Admitted, but reg-no generation failed — student account NOT created. Use Admissions to register.";
                    res.Audit = " | WARNING: reg-no generation failed; acad_student not created.";
                    return res;
                }

                // 2) The register SP reads stud_reg_no from the application — persist it first.
                using (var cmd = new MySqlCommand(
                    "UPDATE acad_applications SET stud_reg_no=@r WHERE stud_entry_no=@eno AND IFNULL(TRIM(stud_reg_no),'') IN ('','-')", conn))
                {
                    cmd.Parameters.AddWithValue("@r", genNo);
                    cmd.Parameters.AddWithValue("@eno", eno);
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new MySqlCommand(
                    "UPDATE acad_applicant_choices SET choice_reg_no=@r WHERE stud_entry_no=@eno AND Choice=1", conn))
                {
                    cmd.Parameters.AddWithValue("@r", genNo);
                    cmd.Parameters.AddWithValue("@eno", eno);
                    cmd.ExecuteNonQuery();
                }

                // 3) Create the acad_student record via the shared, proven SP.
                //    The SP only accepts the current academic year, so pass that year's start
                //    (2026 for 2026/2027), never the calendar year: admitting the January intake
                //    in January 2027 would otherwise ask for 2027/2028 and be refused.
                int admitYear = AcademicYearHelper.IntakeYearOf(AcademicYearHelper.GetCurrentAcademicYear());
                if (admitYear <= 0) admitYear = DateTime.Now.Year;
                using (var cmd = new MySqlCommand("CALL acad_RegisterApplicant(@yr, @eno, @usr)", conn))
                {
                    cmd.Parameters.AddWithValue("@yr", admitYear);
                    cmd.Parameters.AddWithValue("@eno", eno);
                    cmd.Parameters.AddWithValue("@usr", GetCurrentUser());
                    cmd.ExecuteNonQuery();
                }

                // 3b) Admission must NOT register the student for a semester. The SP also inserts an
                //     UNREGISTERED acad_registration placeholder, which the eportal self-registration
                //     wizard treats as "already registered" and so blocks the student from registering
                //     themselves. Remove that placeholder — the student self-registers via eportal,
                //     which creates the REGISTERED row and bills them. Only ever delete the freshly
                //     auto-created UNREGISTERED row; a genuine REGISTERED row is left untouched.
                using (var cmd = new MySqlCommand(
                    "DELETE FROM acad_registration WHERE TRIM(regno)=TRIM(@eno) " +
                    "AND UPPER(TRIM(regstatus))='UNREGISTERED' AND semester=1 AND studyyear=1", conn))
                {
                    cmd.Parameters.AddWithValue("@eno", eno);
                    cmd.ExecuteNonQuery();
                }
            }

            // 4) Activate the student (default new_status is 'ADMITTED' — make it ACTIVE).
            int activated;
            using (var cmd = new MySqlCommand(
                "UPDATE acad_student SET new_status='ACTIVE', stud_status='ACTIVE' WHERE TRIM(regno)=TRIM(@eno)", conn))
            {
                cmd.Parameters.AddWithValue("@eno", eno);
                activated = cmd.ExecuteNonQuery();
            }

            if (activated <= 0)
            {
                res.Message = "Admitted, but the student account could not be confirmed. Please verify in Students.";
                res.Audit = " | WARNING: acad_student not found after register step.";
                return res;
            }
            res.Message = "Admitted. Student account created and activated (Reg No: " + eno + "). The student now registers for the semester themselves via the eportal.";
            res.Audit = " | Student account created & activated (not semester-registered), regno=" + eno;
            return res;
        }
        catch (Exception ex)
        {
            res.Message = "Admitted, but creating the student account failed: " + ex.Message;
            res.Audit = " | WARNING: student provisioning failed: " + ex.Message;
            return res;
        }
    }

    private void HandleReject()
    {
        var data   = ReadPostJson();
        string eno    = GetStr(data, "eno");
        string reason = GetStr(data, "reason");
        if (string.IsNullOrEmpty(eno))    throw new Exception("Entry number required.");
        if (string.IsNullOrEmpty(reason)) throw new Exception("Rejection reason is required.");
        using (var conn = Open())
        {
            if (!ColumnExists(conn, "acad_applications", "app_status"))
                throw new Exception("app_status column not found.");
            string current = GetAppStatus(conn, eno);
            if (current == "ADMITTED" || current == "WITHDRAWN" || current == "REJECTED")
                throw new Exception("Cannot reject application in status: " + current);
            UpdateStatus(conn, eno, "REJECTED");
            SaveReviewerNotes(conn, eno, "[REJECTED] " + reason);
            using (var cmd = new MySqlCommand(
                "UPDATE acad_applicant_choices SET adm_status=2 WHERE stud_entry_no=@eno AND Choice=1 AND adm_status IN(0,1)",
                conn))
            {
                cmd.Parameters.AddWithValue("@eno", eno);
                cmd.ExecuteNonQuery();
            }
            WriteAuditLog(conn, eno, "REJECTED", "Rejected by " + GetCurrentUser() + ". Reason: " + reason);
        }
        Response.Write("{\"ok\":true}");
    }

    private void HandleNote()
    {
        var data  = ReadPostJson();
        string eno   = GetStr(data, "eno");
        string notes = GetStr(data, "notes");
        if (string.IsNullOrEmpty(eno)) throw new Exception("Entry number required.");
        using (var conn = Open())
            SaveReviewerNotes(conn, eno, notes);
        Response.Write("{\"ok\":true}");
    }

    // ════════════════════════════════════════════════════════════════════════
    // DB HELPERS
    // ════════════════════════════════════════════════════════════════════════
    private MySqlConnection Open()
    {
        var conn = new MySqlConnection(ConnStr);
        conn.Open();
        return conn;
    }

    private string GetAppStatus(MySqlConnection conn, string eno)
    {
        using (var cmd = new MySqlCommand(
            "SELECT IFNULL(app_status,'DRAFT') FROM acad_applications WHERE stud_entry_no=@eno LIMIT 1", conn))
        {
            cmd.Parameters.AddWithValue("@eno", eno);
            object v = cmd.ExecuteScalar();
            if (v == null || v == DBNull.Value) throw new Exception("Application not found: " + eno);
            return v.ToString().Trim().ToUpperInvariant();
        }
    }

    private void UpdateStatus(MySqlConnection conn, string eno, string status)
    {
        bool hasUpdatedAt = ColumnExists(conn, "acad_applications", "app_last_updated_at");
        string sql = hasUpdatedAt
            ? "UPDATE acad_applications SET app_status=@s, app_last_updated_at=@now WHERE stud_entry_no=@eno"
            : "UPDATE acad_applications SET app_status=@s WHERE stud_entry_no=@eno";
        using (var cmd = new MySqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@s",   status);
            cmd.Parameters.AddWithValue("@eno", eno);
            if (hasUpdatedAt) cmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
            cmd.ExecuteNonQuery();
        }
    }

    private void SaveReviewerNotes(MySqlConnection conn, string eno, string notes)
    {
        if (!ColumnExists(conn, "acad_applications", "app_reviewer_notes")) return;
        bool hasUpdatedAt = ColumnExists(conn, "acad_applications", "app_last_updated_at");
        string sql = "UPDATE acad_applications SET app_reviewer_notes=@n";
        if (hasUpdatedAt) sql += ", app_last_updated_at=@now";
        sql += " WHERE stud_entry_no=@eno";
        using (var cmd = new MySqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@n",   notes ?? "");
            if (hasUpdatedAt) cmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@eno", eno);
            cmd.ExecuteNonQuery();
        }
    }

    private void WriteAuditLog(MySqlConnection conn, string eno, string action, string detail)
    {
        try
        {
            using (var cmd = new MySqlCommand(@"
                INSERT IGNORE INTO apply_audit_log (stud_entry_no, actor, action, detail, created_at)
                VALUES (@eno, @actor, @action, @detail, @now)", conn))
            {
                cmd.Parameters.AddWithValue("@eno",    eno);
                cmd.Parameters.AddWithValue("@actor",  GetCurrentUser());
                cmd.Parameters.AddWithValue("@action", action);
                cmd.Parameters.AddWithValue("@detail", detail ?? "");
                cmd.Parameters.AddWithValue("@now",    DateTime.UtcNow);
                cmd.ExecuteNonQuery();
            }
        }
        catch { }
    }

    private string GetCurrentUser()
    {
        if (Session["LoggedInUser"] != null) return Session["LoggedInUser"].ToString();
        if (Session["username"]     != null) return Session["username"].ToString();
        return "system";
    }

    private bool ColumnExists(MySqlConnection conn, string table, string column)
    {
        string key = table + "." + column;
        if (_colCache.ContainsKey(key)) return _colCache[key];
        using (var cmd = new MySqlCommand(@"
            SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=@t AND COLUMN_NAME=@c", conn))
        {
            cmd.Parameters.AddWithValue("@t", table);
            cmd.Parameters.AddWithValue("@c", column);
            object v = cmd.ExecuteScalar();
            bool result = v != null && v != DBNull.Value && Convert.ToInt32(v) > 0;
            _colCache[key] = result;
            return result;
        }
    }

    private bool TableExists(MySqlConnection conn, string table)
    {
        using (var cmd = new MySqlCommand(@"
            SELECT COUNT(*) FROM information_schema.TABLES
            WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=@t", conn))
        {
            cmd.Parameters.AddWithValue("@t", table);
            object v = cmd.ExecuteScalar();
            return v != null && v != DBNull.Value && Convert.ToInt32(v) > 0;
        }
    }

    private Dictionary<string, object> ReadPostJson()
    {
        string body = "";
        using (var sr = new System.IO.StreamReader(Request.InputStream))
            body = sr.ReadToEnd();
        if (string.IsNullOrWhiteSpace(body)) return new Dictionary<string, object>();
        return new JavaScriptSerializer()
            .Deserialize<Dictionary<string, object>>(body) ?? new Dictionary<string, object>();
    }

    // ════════════════════════════════════════════════════════════════════════
    // STRING HELPERS (protected so ASPX <%= %> expressions can call them)
    // ════════════════════════════════════════════════════════════════════════
    protected static string HE(string s)
    {
        return HttpUtility.HtmlEncode(s ?? "");
    }

    private static string HtmlAE(string s)
    {
        return HttpUtility.HtmlAttributeEncode(s ?? "");
    }

    // Returns selected="selected" when current matches option (used in ASPX filter selects)
    protected string Sel(string current, string option)
    {
        return string.Equals(current ?? "", option ?? "", StringComparison.OrdinalIgnoreCase)
            ? " selected=\"selected\"" : "";
    }

    private static string JsStr(string s)
    {
        return (s ?? "").Replace("\\", "\\\\").Replace("'", "\\'")
                        .Replace("\r", "").Replace("\n", "");
    }

    private static string GetStr(Dictionary<string, object> d, string key)
    {
        return d.ContainsKey(key) ? Convert.ToString(d[key] ?? "").Trim() : "";
    }

    private static int SafeInt(object v, int def = 0)
    {
        int n;
        return int.TryParse(Convert.ToString(v ?? ""), out n) ? n : def;
    }

    private static long SafeLong(object v, long def = 0)
    {
        long n;
        return long.TryParse(Convert.ToString(v ?? ""), out n) ? n : def;
    }

    private static string FormatDate(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        DateTime dt;
        return DateTime.TryParse(val.ToString(), out dt)
            ? dt.ToString("dd MMM yyyy, HH:mm") : "";
    }

    private static string JsonStr(string s)
    {
        if (s == null) return "null";
        return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                        .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
    }
}
