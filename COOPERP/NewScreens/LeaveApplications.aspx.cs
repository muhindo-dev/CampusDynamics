using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

/// <summary>
/// Leave applications list. Used by every employee (own applications and those they approve),
/// HODs, HR and the Vice Chancellor; the role rules below decide what each one sees.
/// Search, status and type filters are applied in SQL and kept by the pager and the export.
/// </summary>
public partial class COOPERP_NewScreens_LeaveApplications : System.Web.UI.Page
{
    private const int PageSize = 40;

    // Role codes that can do HR-level actions
    private static readonly HashSet<string> HrRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "hr_manager", "admin" };

    // Role codes that can do VC-level actions
    private static readonly HashSet<string> VcRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "vc", "admin" };

    // Role codes that act as HOD / supervisor
    private static readonly HashSet<string> HodRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "dean", "registrar", "hr_manager", "admin" };

    private static readonly string[] Pending = { "SUBMITTED", "HOD_APPROVED", "HR_APPROVED" };
    private static readonly string[] Declined = { "HOD_DECLINED", "HR_DECLINED", "VC_NOT_GRANTED", "CANCELLED" };
    private static readonly string[] AllStatuses =
        { "DRAFT", "SUBMITTED", "HOD_APPROVED", "HOD_DECLINED", "HR_APPROVED", "HR_DECLINED", "VC_GRANTED", "VC_NOT_GRANTED", "VC_POSTPONED", "CANCELLED" };
    private static readonly string[] LeaveTypes = { "annual", "study", "sick", "maternity", "bereavement" };

    // ── filters from the query string ─────────────────────────────────────
    private string QsSearch { get { return (Request.QueryString["q"] ?? "").Trim(); } }
    private string QsStatus
    {
        get
        {
            string s = (Request.QueryString["status"] ?? "").Trim().ToUpperInvariant();
            if (s == "PENDING" || s == "DECLINED" || Array.IndexOf(AllStatuses, s) >= 0) return s;
            return "";
        }
    }
    private string QsType
    {
        get
        {
            string t = (Request.QueryString["type"] ?? "").Trim().ToLowerInvariant();
            return Array.IndexOf(LeaveTypes, t) >= 0 ? t : "";
        }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        if (!HrAccess.IsSignedIn()) { Response.Redirect("~/Default.aspx", true); return; }
        RoleAccessService.RequireSlug(this, "hr.leave_applications");

        string export = (Request.QueryString["export"] ?? "").Trim().ToLowerInvariant();
        if (export == "xlsx" || export == "csv")
        {
            SendExport(export);
            return;
        }

        if (!IsPostBack)
            LoadPage();
    }

    // ── role scope ────────────────────────────────────────────────────────

    private sealed class Scope
    {
        public string RoleCode, Username;
        public bool IsAdmin, IsHr, IsVc, IsHod;
    }

    private Scope CurrentScope()
    {
        Scope s = new Scope();
        s.RoleCode = RoleAccessService.GetRoleCode();
        s.Username = Session["username"] as string ?? "";
        s.IsAdmin  = RoleAccessService.IsAdmin();
        s.IsHr     = HrRoles.Contains(s.RoleCode);
        s.IsVc     = VcRoles.Contains(s.RoleCode);
        s.IsHod    = HodRoles.Contains(s.RoleCode);
        return s;
    }

    private static string BuildRoleWhere(Scope s)
    {
        if (s.IsAdmin) return "";  // Admin sees all
        if (s.IsVc && !s.IsHr)
            return " AND status IN ('HR_APPROVED','VC_GRANTED','VC_NOT_GRANTED','VC_POSTPONED')";
        if (s.IsHr) return "";     // HR sees all
        // HOD and regular employees: their own, plus any application they were chosen to approve.
        return " AND (created_by = @uname OR supervisor_username = @uname)";
    }

    private static void AddRoleParams(MySqlCommand cmd, Scope s)
    {
        if (!s.IsAdmin && !s.IsVc && (s.IsHod || !s.IsHr))
            cmd.Parameters.AddWithValue("@uname", s.Username);
    }

    /// <summary>Search, status and type filters as SQL (applied after the role scope).</summary>
    private string BuildFilterWhere(bool withStatus)
    {
        StringBuilder w = new StringBuilder();
        if (QsSearch != "")
            w.Append(" AND (emp_name LIKE @q OR emp_code LIKE @q OR faculty_dept LIKE @q OR position_held LIKE @q)");
        if (QsType != "") w.Append(" AND leave_type = @type");
        if (withStatus)
        {
            string st = QsStatus;
            if (st == "PENDING") w.Append(" AND status IN ('SUBMITTED','HOD_APPROVED','HR_APPROVED')");
            else if (st == "DECLINED") w.Append(" AND status IN ('HOD_DECLINED','HR_DECLINED','VC_NOT_GRANTED','CANCELLED')");
            else if (st != "") w.Append(" AND status = @status");
        }
        return w.ToString();
    }

    private void AddFilterParams(MySqlCommand cmd, bool withStatus)
    {
        if (QsSearch != "") cmd.Parameters.AddWithValue("@q", "%" + QsSearch + "%");
        if (QsType != "") cmd.Parameters.AddWithValue("@type", QsType);
        if (withStatus && QsStatus != "" && QsStatus != "PENDING" && QsStatus != "DECLINED")
            cmd.Parameters.AddWithValue("@status", QsStatus);
    }

    // ── page ──────────────────────────────────────────────────────────────

    private void LoadPage()
    {
        Scope s = CurrentScope();

        string subtitle;
        if (s.IsAdmin || s.IsHr) subtitle = "All applications";
        else if (s.IsVc)         subtitle = "Applications for the Vice Chancellor";
        else                     subtitle = "Your applications and those you approve";
        litSubtitle.Text = HttpUtility.HtmlEncode(subtitle);

        if (!s.IsVc || s.IsAdmin)
        {
            litNewBtn.Text =
                "<a href=\"LeaveApplicationForm.aspx\" class=\"hr-btn hr-btn--inverse\">" +
                "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><line x1=\"12\" y1=\"5\" x2=\"12\" y2=\"19\"/><line x1=\"5\" y1=\"12\" x2=\"19\" y2=\"12\"/></svg>" +
                "New application</a>";
        }

        txtSearchValue = QsSearch;
        litStatusOptions.Text = StatusOptions(QsStatus);
        litTypeOptions.Text = TypeOptions(QsType);

        int page = 1;
        int.TryParse(Request.QueryString["page"] ?? "1", out page);
        if (page < 1) page = 1;

        StringBuilder rowsSb = new StringBuilder();
        string roleWhere = BuildRoleWhere(s);

        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr()))
            {
                conn.Open();

                // Tile counts: role scope + search + type, every status.
                Dictionary<string, int> counts = new Dictionary<string, int>();
                int cAll = 0;
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT status, COUNT(*) AS cnt FROM hrm_leave_applications WHERE is_active=1" +
                    roleWhere + BuildFilterWhere(false) + " GROUP BY status", conn))
                {
                    AddRoleParams(cmd, s);
                    AddFilterParams(cmd, false);
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                        while (dr.Read())
                        {
                            int n = Convert.ToInt32(dr["cnt"]);
                            counts[dr["status"].ToString()] = n;
                            cAll += n;
                        }
                }
                litStats.Text = BuildTiles(counts, cAll);

                // Total for the pager (all filters).
                int total;
                string where = " WHERE is_active=1" + roleWhere + BuildFilterWhere(true);
                using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*) FROM hrm_leave_applications" + where, conn))
                {
                    AddRoleParams(cmd, s);
                    AddFilterParams(cmd, true);
                    total = Convert.ToInt32(cmd.ExecuteScalar());
                }

                int totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
                if (page > totalPages) page = totalPages;
                int offset = (page - 1) * PageSize;

                string dataSql =
                    @"SELECT id, emp_name, emp_code, faculty_dept, position_held, leave_type,
                             leave_from, leave_to, num_days, status, created_at, employee_submitted_at
                      FROM hrm_leave_applications" + where + @"
                      ORDER BY CASE status WHEN 'SUBMITTED' THEN 1 WHEN 'HOD_APPROVED' THEN 2 WHEN 'HR_APPROVED' THEN 3 ELSE 4 END,
                               COALESCE(employee_submitted_at, created_at) DESC
                      LIMIT @lim OFFSET @off";

                using (MySqlCommand cmd = new MySqlCommand(dataSql, conn))
                {
                    AddRoleParams(cmd, s);
                    AddFilterParams(cmd, true);
                    cmd.Parameters.AddWithValue("@lim", PageSize);
                    cmd.Parameters.AddWithValue("@off", offset);
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                            rowsSb.Append(RowHtml(dr, s));
                    }
                }
                if (rowsSb.Length == 0)
                    rowsSb.Append("<tr><td colspan=\"8\" class=\"hr-empty\">No leave applications match these filters.</td></tr>");

                litCount.Text = total == 1 ? "1 application" : total.ToString("N0") + " applications";
                litPager.Text = BuildPager(page, totalPages, total);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("LeaveApplications: " + ex);
            rowsSb.Length = 0;
            rowsSb.Append("<tr><td colspan=\"8\" class=\"hr-empty\">The applications could not be loaded. Refresh the page, and contact MIS if it keeps happening.</td></tr>");
        }

        litRows.Text = rowsSb.ToString();
        litExportLinks.Text =
            "<a class=\"hr-btn hr-btn--secondary hr-btn--sm\" href=\"" + HttpUtility.HtmlAttributeEncode(FilterUrl("export=xlsx", false)) + "\">" + IconDownload + "Excel</a>" +
            "<a class=\"hr-btn hr-btn--secondary hr-btn--sm\" href=\"" + HttpUtility.HtmlAttributeEncode(FilterUrl("export=csv", false)) + "\">CSV</a>";
    }

    protected string txtSearchValue = "";

    private string RowHtml(MySqlDataReader dr, Scope s)
    {
        int id = Convert.ToInt32(dr["id"]);
        string empName = dr["emp_name"].ToString();
        string status  = dr["status"].ToString();
        int days = dr["num_days"] == DBNull.Value ? 0 : Convert.ToInt32(dr["num_days"]);
        object submitted = dr["employee_submitted_at"] != DBNull.Value ? dr["employee_submitted_at"] : dr["created_at"];

        StringBuilder sb = new StringBuilder();
        sb.Append("<tr>");
        sb.Append("<td><div class=\"lv-emp\"><span class=\"lv-ini\">").Append(HttpUtility.HtmlEncode(Initials(empName))).Append("</span><div>")
          .Append("<div class=\"lv-name\">").Append(Enc(empName)).Append("</div>")
          .Append("<span class=\"hr-sub\">").Append(Enc(JoinNonEmpty(dr["emp_code"], dr["faculty_dept"]))).Append("</span>")
          .Append("</div></div></td>");
        sb.Append("<td>").Append(HttpUtility.HtmlEncode(LeaveTypeLabel(dr["leave_type"].ToString()))).Append("</td>");
        sb.Append("<td style=\"white-space:nowrap\">").Append(D(dr["leave_from"])).Append("</td>");
        sb.Append("<td style=\"white-space:nowrap\">").Append(D(dr["leave_to"])).Append("</td>");
        sb.Append("<td class=\"hr-num\">").Append(days > 0 ? days.ToString() : "").Append("</td>");
        sb.Append("<td>").Append(StatusBadge(status)).Append("</td>");
        sb.Append("<td style=\"white-space:nowrap\">").Append(D(submitted)).Append("</td>");
        sb.Append("<td class=\"hr-right\" style=\"white-space:nowrap\">");
        sb.AppendFormat("<a class=\"hr-btn hr-btn--secondary hr-btn--sm\" href=\"LeaveApplicationForm.aspx?id={0}\">Open</a> ", id);
        sb.AppendFormat("<a class=\"hr-btn hr-btn--secondary hr-btn--sm\" href=\"LeaveApplicationForm.aspx?id={0}&amp;print=1\" target=\"_blank\" title=\"Print form\">Print</a>", id);
        if ((s.IsAdmin || s.IsHr) && status != "CANCELLED" && status != "VC_GRANTED")
            sb.AppendFormat(" <button type=\"button\" class=\"hr-btn hr-btn--danger hr-btn--sm\" onclick=\"cancelApp({0})\">Cancel</button>", id);
        sb.Append("</td></tr>");
        return sb.ToString();
    }

    private string BuildTiles(Dictionary<string, int> c, int all)
    {
        int awaitingHod = Get(c, "SUBMITTED"), awaitingHr = Get(c, "HOD_APPROVED"), awaitingVc = Get(c, "HR_APPROVED");
        int granted = Get(c, "VC_GRANTED");
        int declined = 0;
        foreach (string st in Declined) declined += Get(c, st);

        StringBuilder sb = new StringBuilder("<div class=\"hr-kpis\">");
        Tile(sb, "All applications", all, "");
        Tile(sb, "Awaiting HOD", awaitingHod, "SUBMITTED");
        Tile(sb, "Awaiting HR", awaitingHr, "HOD_APPROVED");
        Tile(sb, "Awaiting Vice Chancellor", awaitingVc, "HR_APPROVED");
        Tile(sb, "Granted", granted, "VC_GRANTED");
        Tile(sb, "Declined or cancelled", declined, "DECLINED");
        sb.Append("</div>");
        return sb.ToString();
    }

    private void Tile(StringBuilder sb, string label, int value, string status)
    {
        bool active = QsStatus == status;
        sb.Append("<a class=\"hr-kpi").Append(active ? " lv-kpi--on" : "").Append("\" href=\"")
          .Append(HttpUtility.HtmlAttributeEncode(FilterUrlWithStatus(status))).Append("\">")
          .Append("<div class=\"hr-kpi__label\">").Append(HttpUtility.HtmlEncode(label)).Append("</div>")
          .Append("<div class=\"hr-kpi__value\">").Append(value.ToString("N0")).Append("</div></a>");
    }

    private static int Get(Dictionary<string, int> d, string k) { int v; return d.TryGetValue(k, out v) ? v : 0; }

    // ── URLs that keep the filters ────────────────────────────────────────

    private string FilterUrl(string extra, bool keepPage)
    {
        List<string> p = new List<string>();
        if (QsSearch != "") p.Add("q=" + HttpUtility.UrlEncode(QsSearch));
        if (QsStatus != "") p.Add("status=" + HttpUtility.UrlEncode(QsStatus));
        if (QsType != "") p.Add("type=" + HttpUtility.UrlEncode(QsType));
        if (keepPage && Request.QueryString["page"] != null) p.Add("page=" + HttpUtility.UrlEncode(Request.QueryString["page"]));
        if (!string.IsNullOrEmpty(extra)) p.Add(extra);
        return "LeaveApplications.aspx" + (p.Count > 0 ? "?" + string.Join("&", p.ToArray()) : "");
    }

    private string FilterUrlWithStatus(string status)
    {
        List<string> p = new List<string>();
        if (QsSearch != "") p.Add("q=" + HttpUtility.UrlEncode(QsSearch));
        if (status != "") p.Add("status=" + HttpUtility.UrlEncode(status));
        if (QsType != "") p.Add("type=" + HttpUtility.UrlEncode(QsType));
        return "LeaveApplications.aspx" + (p.Count > 0 ? "?" + string.Join("&", p.ToArray()) : "");
    }

    private string BuildPager(int page, int totalPages, int total)
    {
        if (totalPages <= 1) return "";
        StringBuilder sb = new StringBuilder("<div class=\"hr-pager\">");
        sb.AppendFormat("<span>Page {0} of {1}</span>", page, totalPages);
        sb.Append(PagerLink("Previous", page - 1, page > 1));
        sb.Append(PagerLink("Next", page + 1, page < totalPages));
        sb.Append("</div>");
        return sb.ToString();
    }

    private string PagerLink(string label, int target, bool enabled)
    {
        if (!enabled) return "<button type=\"button\" disabled=\"disabled\">" + label + "</button>";
        return "<button type=\"button\" onclick=\"location.href='" +
               HttpUtility.JavaScriptStringEncode(FilterUrl("page=" + target, false)) + "'\">" + label + "</button>";
    }

    // ── export ────────────────────────────────────────────────────────────

    private void SendExport(string fmt)
    {
        Scope s = CurrentScope();
        HrExport.Report r = new HrExport.Report("Leave applications", "leave-applications");
        r.PreparedBy = HrAccess.Username();
        if (!(s.IsAdmin || s.IsHr))
            r.AddScope("View", s.IsVc ? "Applications for the Vice Chancellor" : "Own applications and those approved by " + s.Username);
        r.AddScope("Status", StatusFilterLabel(QsStatus));
        r.AddScope("Leave type", QsType == "" ? "" : LeaveTypeLabel(QsType));
        r.AddScope("Search", QsSearch);

        HrExport.Sheet sh = r.NewSheet("Applications");
        sh.Add("Reference").Add("Staff No").Add("Name").Add("Department").Add("Position").Add("Leave type")
          .Add("First day", HrExport.Kind.Date).Add("Last day", HrExport.Kind.Date).Add("Days", HrExport.Kind.Number, true)
          .Add("Status").Add("Submitted", HrExport.Kind.Date).Add("Head of Department")
          .Add("HOD decision", HrExport.Kind.Date).Add("HR decision", HrExport.Kind.Date)
          .Add("Vice Chancellor decision", HrExport.Kind.Date).Add("Days granted", HrExport.Kind.Number);

        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr()))
            {
                conn.Open();
                string sql =
                    @"SELECT id, emp_code, emp_name, faculty_dept, position_held, leave_type, leave_from, leave_to,
                             num_days, status, employee_submitted_at, supervisor_name, supervisor_username,
                             hod_action_at, hr_action_at, vc_action_at, vc_days_taken
                      FROM hrm_leave_applications WHERE is_active=1" + BuildRoleWhere(s) + BuildFilterWhere(true) + @"
                      ORDER BY leave_from DESC, emp_name";
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    AddRoleParams(cmd, s);
                    AddFilterParams(cmd, true);
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                        while (dr.Read())
                        {
                            string sup = Str(dr["supervisor_name"]);
                            if (sup == "") sup = Str(dr["supervisor_username"]);
                            sh.Row("LV-" + Convert.ToInt32(dr["id"]).ToString("0000"), Str(dr["emp_code"]), Str(dr["emp_name"]),
                                Str(dr["faculty_dept"]), Str(dr["position_held"]), LeaveTypeLabel(Str(dr["leave_type"])),
                                dr["leave_from"], dr["leave_to"], dr["num_days"], StatusLabel(Str(dr["status"])),
                                dr["employee_submitted_at"], sup, dr["hod_action_at"], dr["hr_action_at"],
                                dr["vc_action_at"], dr["vc_days_taken"]);
                        }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("LeaveApplications export: " + ex);
            Response.Clear();
            Response.ContentType = "text/plain";
            Response.Write("The export could not be prepared. Go back and try again.");
            Response.End();
            return;
        }

        if (fmt == "csv") HrExport.SendCsv(Response, r, 0);
        else HrExport.SendXlsx(Response, r);
        Response.End();
    }

    // ── labels and badges ─────────────────────────────────────────────────

    internal static string StatusLabel(string status)
    {
        switch (status)
        {
            case "DRAFT":          return "Draft";
            case "SUBMITTED":      return "Awaiting HOD";
            case "HOD_APPROVED":   return "Awaiting HR";
            case "HOD_DECLINED":   return "Declined by HOD";
            case "HR_APPROVED":    return "Awaiting Vice Chancellor";
            case "HR_DECLINED":    return "Declined by HR";
            case "VC_GRANTED":     return "Granted";
            case "VC_NOT_GRANTED": return "Not granted";
            case "VC_POSTPONED":   return "Postponed";
            case "CANCELLED":      return "Cancelled";
            default:               return status;
        }
    }

    private static string StatusFilterLabel(string st)
    {
        if (st == "") return "";
        if (st == "PENDING") return "Awaiting action";
        if (st == "DECLINED") return "Declined or cancelled";
        return StatusLabel(st);
    }

    private static string StatusBadge(string status)
    {
        string kind;
        switch (status)
        {
            case "SUBMITTED": case "HOD_APPROVED": case "HR_APPROVED": kind = "info"; break;
            case "VC_GRANTED": kind = "ok"; break;
            case "VC_POSTPONED": kind = "warn"; break;
            case "HOD_DECLINED": case "HR_DECLINED": case "VC_NOT_GRANTED": kind = "bad"; break;
            default: kind = "neutral"; break;
        }
        return "<span class=\"hr-badge hr-badge--" + kind + "\">" + HttpUtility.HtmlEncode(StatusLabel(status)) + "</span>";
    }

    private static string LeaveTypeLabel(string type)
    {
        switch (type)
        {
            case "annual":      return "Annual leave";
            case "study":       return "Study leave";
            case "sick":        return "Sick leave";
            case "maternity":   return "Maternity leave";
            case "bereavement": return "Family bereavement";
            default:            return type;
        }
    }

    private static string StatusOptions(string sel)
    {
        string[][] opts =
        {
            new[] { "", "All statuses" },
            new[] { "PENDING", "Awaiting action" },
            new[] { "SUBMITTED", "Awaiting HOD" },
            new[] { "HOD_APPROVED", "Awaiting HR" },
            new[] { "HR_APPROVED", "Awaiting Vice Chancellor" },
            new[] { "VC_GRANTED", "Granted" },
            new[] { "VC_POSTPONED", "Postponed" },
            new[] { "DECLINED", "Declined or cancelled" },
            new[] { "HOD_DECLINED", "Declined by HOD" },
            new[] { "HR_DECLINED", "Declined by HR" },
            new[] { "VC_NOT_GRANTED", "Not granted" },
            new[] { "CANCELLED", "Cancelled" },
            new[] { "DRAFT", "Draft" }
        };
        StringBuilder sb = new StringBuilder();
        foreach (string[] o in opts)
            sb.AppendFormat("<option value=\"{0}\"{1}>{2}</option>", o[0], o[0] == sel ? " selected=\"selected\"" : "", o[1]);
        return sb.ToString();
    }

    private static string TypeOptions(string sel)
    {
        StringBuilder sb = new StringBuilder("<option value=\"\">All leave types</option>");
        foreach (string t in LeaveTypes)
            sb.AppendFormat("<option value=\"{0}\"{1}>{2}</option>", t, t == sel ? " selected=\"selected\"" : "", LeaveTypeLabel(t));
        return sb.ToString();
    }

    private const string IconDownload =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4\"/><polyline points=\"7 10 12 15 17 10\"/><line x1=\"12\" y1=\"15\" x2=\"12\" y2=\"3\"/></svg>";

    // ── utilities ─────────────────────────────────────────────────────────

    private static string Initials(string name)
    {
        string[] parts = (name ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2) return ("" + parts[0][0] + parts[parts.Length - 1][0]).ToUpperInvariant();
        if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
        return "";
    }

    private static string JoinNonEmpty(object a, object b)
    {
        string x = Str(a), y = Str(b);
        if (x != "" && y != "") return x + ", " + y;
        return x + y;
    }

    private static string D(object v)
    {
        if (v == null || v == DBNull.Value) return "";
        DateTime d;
        if (v is DateTime) d = (DateTime)v;
        else if (!DateTime.TryParse(v.ToString(), out d)) return "";
        return d.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
    }

    private static string Str(object v) { return v == null || v == DBNull.Value ? "" : v.ToString().Trim(); }
    private static string Enc(string s) { return HttpUtility.HtmlEncode(HrExport.Clean(s ?? "")); }

    private static string ConnStr()
    {
        ConnectionStringSettings cs = ConfigurationManager.ConnectionStrings["vacConnectionString"];
        if (cs != null && !string.IsNullOrEmpty(cs.ConnectionString)) return cs.ConnectionString;
        cs = ConfigurationManager.ConnectionStrings["DefaultConnection"];
        if (cs != null) return cs.ConnectionString;
        throw new InvalidOperationException("No valid connection string.");
    }
}
