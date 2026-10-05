using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

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

    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, "hr.leave_applications");

        if (!IsPostBack)
            LoadPage();
    }

    // ── Page render ────────────────────────────────────────────────────────────

    private void LoadPage()
    {
        string roleCode = RoleAccessService.GetRoleCode();
        string username = Session["username"] as string ?? "";
        bool   isAdmin  = RoleAccessService.IsAdmin();
        bool   isHr     = HrRoles.Contains(roleCode);
        bool   isVc     = VcRoles.Contains(roleCode);
        bool   isHod    = HodRoles.Contains(roleCode);

        // ── Subtitle ─────────────────────────────────────────────────────────
        string subtitle;
        if (isAdmin)       subtitle = "All leave applications across the institution";
        else if (isHr)     subtitle = "All applications — HR management view";
        else if (isVc)     subtitle = "Applications pending Vice Chancellor decision";
        else if (isHod)    subtitle = "Applications from your department pending your approval";
        else               subtitle = "Your leave application history";
        litSubtitle.Text = HttpUtility.HtmlEncode(subtitle);

        // ── New Application button (employees, HR admin on behalf) ────────────
        if (!isVc || isAdmin)
        {
            litNewBtn.Text =
                "<a href=\"LeaveApplicationForm.aspx\" class=\"lv-btn lv-btn--primary\">" +
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"12\" height=\"12\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><line x1=\"12\" y1=\"5\" x2=\"12\" y2=\"19\"/><line x1=\"5\" y1=\"12\" x2=\"19\" y2=\"12\"/></svg>" +
                "New Application</a>";
        }

        int page = 1;
        int.TryParse(Request.QueryString["page"] ?? "1", out page);
        if (page < 1) page = 1;

        var rowsSb  = new StringBuilder();
        var statsSb = new StringBuilder();
        var pagerSb = new StringBuilder();

        // Stat counters
        int cAll = 0, cPending = 0, cHod = 0, cHr = 0, cVc = 0, cGranted = 0, cDeclined = 0;

        try
        {
            using (var conn = new MySqlConnection(ConnStr()))
            {
                conn.Open();

                // ── Counts by status (scope to what this role can see) ────────
                string countWhere = BuildRoleWhere(roleCode, username, isAdmin, isHr, isVc, isHod);

                const string countSql = @"
                    SELECT status, COUNT(*) AS cnt
                    FROM hrm_leave_applications
                    WHERE is_active=1 {0}
                    GROUP BY status";

                using (var cmd = new MySqlCommand(string.Format(countSql, countWhere), conn))
                {
                    AddRoleParams(cmd, roleCode, username, isAdmin, isHr, isVc, isHod);
                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            int cnt = Convert.ToInt32(dr["cnt"]);
                            string s = dr["status"].ToString();
                            cAll += cnt;
                            switch (s)
                            {
                                case "SUBMITTED":     cPending += cnt; break;
                                case "HOD_APPROVED":  cHod     += cnt; break;
                                case "HR_APPROVED":   cHr      += cnt; break;
                                case "VC_GRANTED":    cGranted += cnt; break;
                                case "HOD_DECLINED":
                                case "HR_DECLINED":
                                case "VC_NOT_GRANTED":
                                case "CANCELLED":     cDeclined += cnt; break;
                            }
                            if (s == "HR_APPROVED") cVc += cnt;
                        }
                    }
                }

                // ── Stat chips ────────────────────────────────────────────────
                statsSb.Append("<div class=\"pa-list-stats\">");
                statsSb.AppendFormat("<span class=\"pa-list-stat ps--all\"><b>{0}</b>&nbsp;<span class=\"pa-list-stat__lbl\">Total</span></span>", cAll);
                statsSb.AppendFormat("<span class=\"pa-list-stat ps--pending\"><b>{0}</b>&nbsp;<span class=\"pa-list-stat__lbl\">Pending HOD</span></span>", cPending);
                statsSb.AppendFormat("<span class=\"pa-list-stat ps--hod\"><b>{0}</b>&nbsp;<span class=\"pa-list-stat__lbl\">Pending HR</span></span>", cHod);
                statsSb.AppendFormat("<span class=\"pa-list-stat ps--vc\"><b>{0}</b>&nbsp;<span class=\"pa-list-stat__lbl\">Pending VC</span></span>", cVc);
                statsSb.AppendFormat("<span class=\"pa-list-stat ps--ok\"><b>{0}</b>&nbsp;<span class=\"pa-list-stat__lbl\">Granted</span></span>", cGranted);
                statsSb.AppendFormat("<span class=\"pa-list-stat ps--no\"><b>{0}</b>&nbsp;<span class=\"pa-list-stat__lbl\">Declined</span></span>", cDeclined);
                statsSb.Append("</div>");

                // ── Total for pagination ──────────────────────────────────────
                int total = 0;
                using (var cmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM hrm_leave_applications WHERE is_active=1 " + countWhere, conn))
                {
                    AddRoleParams(cmd, roleCode, username, isAdmin, isHr, isVc, isHod);
                    total = Convert.ToInt32(cmd.ExecuteScalar());
                }

                int totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
                if (page > totalPages) page = totalPages;
                int offset = (page - 1) * PageSize;

                // ── Data rows ─────────────────────────────────────────────────
                string dataSql = @"
                    SELECT id, emp_name, emp_code, faculty_dept, leave_type,
                           leave_from, leave_to, num_days, status,
                           created_at, employee_submitted_at,
                           hod_actor_name, hr_actor_name, vc_actor_name, vc_decision
                    FROM hrm_leave_applications
                    WHERE is_active=1 " + countWhere + @"
                    ORDER BY
                        CASE status
                            WHEN 'SUBMITTED'    THEN 1
                            WHEN 'HOD_APPROVED' THEN 2
                            WHEN 'HR_APPROVED'  THEN 3
                            ELSE 4
                        END,
                        COALESCE(employee_submitted_at, created_at) DESC
                    LIMIT @lim OFFSET @off";

                using (var cmd = new MySqlCommand(dataSql, conn))
                {
                    AddRoleParams(cmd, roleCode, username, isAdmin, isHr, isVc, isHod);
                    cmd.Parameters.AddWithValue("@lim", PageSize);
                    cmd.Parameters.AddWithValue("@off", offset);

                    using (var dr = cmd.ExecuteReader())
                    {
                        bool any = false;
                        int  rowNum = offset;
                        while (dr.Read())
                        {
                            any = true;
                            rowNum++;
                            int    id         = Convert.ToInt32(dr["id"]);
                            string empName    = dr["emp_name"].ToString();
                            string empCode    = dr["emp_code"].ToString();
                            string dept       = dr["faculty_dept"].ToString();
                            string lvType     = dr["leave_type"].ToString();
                            string status     = dr["status"].ToString();
                            string fromDt     = Convert.ToDateTime(dr["leave_from"]).ToString("dd MMM yyyy");
                            string toDt       = Convert.ToDateTime(dr["leave_to"]).ToString("dd MMM yyyy");
                            int    days       = dr["num_days"] == DBNull.Value ? 0 : Convert.ToInt32(dr["num_days"]);
                            DateTime created  = Convert.ToDateTime(dr["created_at"]);
                            string appliedOn  = created.ToString("dd MMM yy");

                            string searchVal  = (empName + " " + empCode + " " + dept + " " + lvType).ToLower();

                            // Avatar
                            string initials = GetInitials(empName);
                            string avatarClr= GetAvatarColor(empName);

                            rowsSb.Append("<tr class=\"lv-row\"");
                            rowsSb.AppendFormat(" data-search=\"{0}\"", HttpUtility.HtmlAttributeEncode(searchVal));
                            rowsSb.AppendFormat(" data-status=\"{0}\"", HttpUtility.HtmlAttributeEncode(status));
                            rowsSb.AppendFormat(" data-type=\"{0}\"",   HttpUtility.HtmlAttributeEncode(lvType));
                            rowsSb.Append(">");

                            // #
                            rowsSb.AppendFormat("<td style=\"color:var(--muted);font-size:10px;\">{0}</td>", rowNum);

                            // Employee
                            rowsSb.Append("<td>");
                            rowsSb.Append("<div class=\"lv-emp\">");
                            rowsSb.AppendFormat("<div class=\"lv-avatar\" style=\"background:{0}\">{1}</div>", avatarClr, initials);
                            rowsSb.Append("<div>");
                            rowsSb.AppendFormat("<div class=\"lv-emp__name\">{0}</div>", HttpUtility.HtmlEncode(empName));
                            if (!string.IsNullOrEmpty(dept))
                                rowsSb.AppendFormat("<div class=\"lv-emp__dept\">{0}</div>", HttpUtility.HtmlEncode(dept));
                            rowsSb.Append("</div></div></td>");

                            // Leave type
                            rowsSb.AppendFormat("<td>{0}</td>", LeaveTypeBadge(lvType));

                            // From / To
                            rowsSb.AppendFormat("<td style=\"white-space:nowrap;font-size:11px;\">{0}</td>", HttpUtility.HtmlEncode(fromDt));
                            rowsSb.AppendFormat("<td style=\"white-space:nowrap;font-size:11px;\">{0}</td>", HttpUtility.HtmlEncode(toDt));

                            // Days
                            rowsSb.AppendFormat("<td style=\"text-align:center;font-weight:600;color:var(--txt);\">{0}</td>", days > 0 ? days.ToString() : "—");

                            // Status
                            rowsSb.AppendFormat("<td>{0}</td>", StatusBadge(status));

                            // Applied
                            rowsSb.AppendFormat("<td style=\"font-size:10px;color:var(--muted);white-space:nowrap;\">{0}</td>", HttpUtility.HtmlEncode(appliedOn));

                            // Actions
                            string idStr = id.ToString();
                            rowsSb.Append("<td>");
                            rowsSb.Append("<div class=\"pa-act-menu\">");
                            rowsSb.Append("<button type=\"button\" class=\"pa-act-btn\">Actions " +
                                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><polyline points=\"6 9 12 15 18 9\"/></svg></button>");
                            rowsSb.Append("<div class=\"pa-act-drop\">");
                            rowsSb.AppendFormat(
                                "<a class=\"pa-act-item\" href=\"LeaveApplicationForm.aspx?id={0}\">" +
                                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"12\" height=\"12\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z\"/><circle cx=\"12\" cy=\"12\" r=\"3\"/></svg>" +
                                "View / Approve</a>",
                                id);
                            rowsSb.AppendFormat(
                                "<a class=\"pa-act-item\" href=\"LeaveApplicationForm.aspx?id={0}&print=1\" target=\"_blank\">" +
                                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"12\" height=\"12\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><polyline points=\"6 9 6 2 18 2 18 9\"/><path d=\"M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2\"/><rect x=\"6\" y=\"14\" width=\"12\" height=\"8\"/></svg>" +
                                "Print Form</a>",
                                id);

                            if (isAdmin || isHr)
                            {
                                rowsSb.Append("<div class=\"pa-act-sep\"></div>");
                                rowsSb.AppendFormat(
                                    "<button type=\"button\" class=\"pa-act-item pa-act-item--danger\" onclick=\"cancelApp({0})\">" +
                                    "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"12\" height=\"12\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><circle cx=\"12\" cy=\"12\" r=\"10\"/><line x1=\"15\" y1=\"9\" x2=\"9\" y2=\"15\"/><line x1=\"9\" y1=\"9\" x2=\"15\" y2=\"15\"/></svg>" +
                                    "Cancel Application</button>",
                                    id);
                            }

                            rowsSb.Append("</div></div></td>");
                            rowsSb.Append("</tr>");
                        }

                        if (!any)
                            rowsSb.Append("<tr><td colspan=\"9\" class=\"lv-empty\">No leave applications found.</td></tr>");
                    }
                }

                // ── Pager ─────────────────────────────────────────────────────
                if (totalPages > 1)
                {
                    string baseUrl = "LeaveApplications.aspx?x=1";
                    pagerSb.AppendFormat("<span style=\"margin-right:8px;\">Page {0} of {1} ({2} records)</span>", page, totalPages, total);
                    if (page > 1)
                        pagerSb.AppendFormat("<a href=\"{0}&amp;page={1}\">&laquo;</a>", baseUrl, page - 1);
                    for (int i = 1; i <= totalPages; i++)
                    {
                        if (totalPages > 10 && i > 3 && i < totalPages - 2 && Math.Abs(i - page) > 2)
                        {
                            if (i == 4) pagerSb.Append("<span style=\"padding:0 4px;\">…</span>");
                            continue;
                        }
                        pagerSb.AppendFormat("<a href=\"{0}&amp;page={1}\"{2}>{1}</a>",
                            baseUrl, i, i == page ? " class=\"active\"" : "");
                    }
                    if (page < totalPages)
                        pagerSb.AppendFormat("<a href=\"{0}&amp;page={1}\">&raquo;</a>", baseUrl, page + 1);
                }
            }
        }
        catch (Exception ex)
        {
            rowsSb.Append("<tr><td colspan=\"9\" style=\"color:#dc2626;padding:20px\">" +
                HttpUtility.HtmlEncode("Error: " + ex.Message) + "</td></tr>");
        }

        litStats.Text = statsSb.ToString();
        litRows.Text  = rowsSb.ToString();
        litPager.Text = pagerSb.ToString();
    }

    // ── Role-based WHERE builder ────────────────────────────────────────────────

    private static string BuildRoleWhere(string roleCode, string username,
        bool isAdmin, bool isHr, bool isVc, bool isHod)
    {
        if (isAdmin) return "";  // Admin sees all

        if (isVc && !isHr)
            return "AND status IN ('HR_APPROVED','VC_GRANTED','VC_NOT_GRANTED','VC_POSTPONED')";

        if (isHr)
            return ""; // HR sees all

        if (isHod)
            // HOD sees: their own + submissions from their supervised users
            return "AND (created_by = @uname OR supervisor_username = @uname)";

        // Regular employee sees their own, plus any application they were chosen to
        // approve as Supervisor / HOD (e.g. users whose role is 'hod').
        return "AND (created_by = @uname OR supervisor_username = @uname)";
    }

    private static void AddRoleParams(MySqlCommand cmd, string roleCode, string username,
        bool isAdmin, bool isHr, bool isVc, bool isHod)
    {
        if (!isAdmin && !isVc && (isHod || (!isHr)))
            cmd.Parameters.AddWithValue("@uname", username);
    }

    // ── Badge helpers ──────────────────────────────────────────────────────────

    private static string StatusBadge(string status)
    {
        string cls, label;
        switch (status)
        {
            case "DRAFT":          cls = "draft";           label = "Draft";           break;
            case "SUBMITTED":      cls = "submitted";       label = "Pending HOD";     break;
            case "HOD_APPROVED":   cls = "hod_approved";   label = "Pending HR";      break;
            case "HOD_DECLINED":   cls = "hod_declined";   label = "HOD Declined";    break;
            case "HR_APPROVED":    cls = "hr_approved";    label = "Pending VC";      break;
            case "HR_DECLINED":    cls = "hr_declined";    label = "HR Declined";     break;
            case "VC_GRANTED":     cls = "vc_granted";     label = "Granted";         break;
            case "VC_NOT_GRANTED": cls = "vc_not_granted"; label = "Not Granted";     break;
            case "VC_POSTPONED":   cls = "vc_postponed";   label = "Postponed";       break;
            case "CANCELLED":      cls = "cancelled";      label = "Cancelled";       break;
            default:               cls = "draft";           label = status;            break;
        }
        return string.Format("<span class=\"lv-status lv-status--{0}\">{1}</span>", cls, label);
    }

    private static string LeaveTypeBadge(string type)
    {
        string cls, label;
        switch (type)
        {
            case "annual":       cls = "annual";      label = "Annual";       break;
            case "study":        cls = "study";       label = "Study";        break;
            case "sick":         cls = "sick";        label = "Sick";         break;
            case "maternity":    cls = "maternity";   label = "Maternity";    break;
            case "bereavement":  cls = "bereavement"; label = "Bereavement";  break;
            default:             cls = "annual";      label = type;           break;
        }
        return string.Format("<span class=\"lv-type lv-type--{0}\">{1}</span>", cls, label);
    }

    private static readonly string[] AvatarColors = {
        "#174DA4","#2e7d32","#6a1b9a","#00838f","#c62828","#4527a0","#00695c","#ad1457","#e65100","#37474f"
    };
    private static string GetAvatarColor(string name)
    {
        if (string.IsNullOrEmpty(name)) return AvatarColors[0];
        int h = 0; foreach (char c in name) h = (h * 31 + (int)c) & 0x7fffffff;
        return AvatarColors[h % AvatarColors.Length];
    }
    private static string GetInitials(string name)
    {
        var parts = (name ?? "?").Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2) return ("" + parts[0][0] + parts[parts.Length - 1][0]).ToUpper();
        return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : (name.Length == 1 ? name.ToUpper() : "?");
    }

    // ── Utilities ──────────────────────────────────────────────────────────────

    private static string ConnStr()
    {
        var cs = ConfigurationManager.ConnectionStrings["vacConnectionString"];
        if (cs != null && !string.IsNullOrEmpty(cs.ConnectionString)) return cs.ConnectionString;
        cs = ConfigurationManager.ConnectionStrings["DefaultConnection"];
        if (cs != null) return cs.ConnectionString;
        throw new InvalidOperationException("No valid connection string.");
    }
}
