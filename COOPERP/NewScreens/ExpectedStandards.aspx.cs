using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web;
using System.Web.UI;
using MySql.Data.MySqlClient;

/// <summary>
/// Expected Standards catalogue (Performance Appraisal, Section B).
///
/// appraisal_standard_groups  - one group per office / staff type (Lecturers, Finance, ICT ...)
/// appraisal_standards        - the Responsibility / KPA + Expected Standard rows of a group
/// hrm_departments.standards_group_id - which group a department's staff are given
///
/// The resolver appraisal_resolve_group(empID, category) reads these tables when a record
/// is generated, and the staff portal pre-fills Section B from the group's active standards.
/// Anything already referenced by appraisal_section_b.standard_id (or, for a group, by a
/// record / department / standard) is deactivated, never hard-deleted.
/// </summary>
public partial class COOPERP_NewScreens_ExpectedStandards : System.Web.UI.Page
{
    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    private int QsGroup
    {
        get { int v; return int.TryParse(Request.QueryString["gid"] ?? "0", out v) && v > 0 ? v : 0; }
    }

    private static readonly string[] AppliesTo = new string[] { "ACADEMIC", "ADMINISTRATIVE", "ANY" };

    // ═══════════════════════════════════════════════════════════════════
    //  PAGE LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════
    protected void Page_Load(object sender, EventArgs e)
    {
        string ajax = (Request.QueryString["ajax"] ?? "").Trim().ToLower();
        if (!string.IsNullOrEmpty(ajax))
        {
            HandleAjax(ajax);
            return;
        }

        if (!IsPostBack)
        {
            try { LoadPage(); }
            catch (Exception ex)
            {
                litError.Text = "<div class='es-alert es-alert--error'>Error loading catalogue: " +
                    HttpUtility.HtmlEncode(ex.Message) + "</div>";
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AUTH
    // ═══════════════════════════════════════════════════════════════════
    private bool IsCallerAuthenticated()
    {
        try
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated
                && !string.IsNullOrEmpty(User.Identity.Name)) return true;
        }
        catch { }
        try
        {
            if (Session != null)
            {
                object u = Session["username"];
                if (u != null && !string.IsNullOrEmpty(u.ToString().Trim())) return true;
            }
        }
        catch { }
        return false;
    }

    private bool? _hrAccess;

    /// <summary>
    /// HR appraisal administration: RBAC admin wildcard, an active sys role 'admin' or
    /// 'hr_manager', or legacy my_aspnet role Administrator / System Admin / Human Resource /
    /// Human Resource Manager. (Same rule as AppraisalView / AppraisalSessions.)
    /// </summary>
    private bool HasHrAppraisalAccess()
    {
        if (_hrAccess.HasValue) return _hrAccess.Value;
        bool ok = false;
        try
        {
            if (IsCallerAuthenticated())
            {
                if (RoleAccessService.IsAdmin()) ok = true;
                string u = Session != null && Session["username"] != null ? Session["username"].ToString().Trim()
                         : (User != null && User.Identity != null ? User.Identity.Name : "");
                if (!ok && !string.IsNullOrEmpty(u))
                {
                    DataTable dt = Q(
                        @"SELECT
                            (SELECT COUNT(*) FROM sys_user_roles ur
                               JOIN sys_roles r ON r.id = ur.role_id
                              WHERE ur.username = @u AND ur.is_active = 1 AND r.is_active = 1
                                AND (ur.expires_at IS NULL OR ur.expires_at > NOW())
                                AND r.role_code IN ('admin','hr_manager'))
                          + (SELECT COUNT(*) FROM my_aspnet_users mu
                               JOIN my_aspnet_usersinroles mur ON mur.userId = mu.id
                               JOIN my_aspnet_roles mr ON mr.id = mur.roleId
                              WHERE mu.name = @u
                                AND mr.name IN ('Administrator','System Admin','Human Resource','Human Resource Manager')) AS n",
                        new MySqlParameter("@u", u));
                    ok = dt.Rows.Count > 0 && SafeInt(dt.Rows[0]["n"]) > 0;
                }
            }
        }
        catch { ok = false; }
        _hrAccess = ok;
        return ok;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AJAX ROUTER
    // ═══════════════════════════════════════════════════════════════════
    private void HandleAjax(string action)
    {
        Response.Clear();
        Response.ContentType = "application/json";
        try
        {
            // ?ajax= runs BEFORE SidebarMaster's login check, so it gates itself.
            if (!IsCallerAuthenticated())
            {
                Response.Write("{\"ok\":false,\"msg\":\"Your session has expired. Please sign in again, then retry.\"}");
            }
            else if (action == "get_group" || action == "get_standard")
            {
                if (action == "get_group") AjaxGetGroup(); else AjaxGetStandard();
            }
            else if (!string.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                     || !MarksAntiForgeryService.ValidateRequest())
            {
                Response.Write("{\"ok\":false,\"msg\":\"Security validation failed. Please refresh and try again.\"}");
            }
            else if (!HasHrAppraisalAccess())
            {
                Response.Write("{\"ok\":false,\"msg\":\"Access denied. HR or administrator access is required.\"}");
            }
            else
            {
                switch (action)
                {
                    case "save_group":      AjaxSaveGroup(); break;
                    case "toggle_group":    AjaxToggle("appraisal_standard_groups", "group_id", "group"); break;
                    case "delete_group":    AjaxDeleteGroup(); break;
                    case "save_standard":   AjaxSaveStandard(); break;
                    case "toggle_standard": AjaxToggle("appraisal_standards", "standard_id", "standard"); break;
                    case "delete_standard": AjaxDeleteStandard(); break;
                    case "reorder":         AjaxReorder(); break;
                    case "map_department":  AjaxMapDepartment(); break;
                    default: Response.Write("{\"ok\":false,\"msg\":\"Unknown action\"}"); break;
                }
            }
        }
        catch (System.Threading.ThreadAbortException) { }
        catch (Exception ex)
        {
            Response.Write("{\"ok\":false,\"msg\":\"" + Js(ex.Message) + "\"}");
        }
        try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
    }

    private string F(string key) { return (Request.Form[key] ?? "").Trim(); }

    // ── groups ──────────────────────────────────────────────────────────
    private void AjaxGetGroup()
    {
        int id = SafeInt(Request.QueryString["id"]);
        DataTable dt = Q("SELECT group_id, group_code, group_name, applies_to, sort_order, is_active FROM appraisal_standard_groups WHERE group_id = @id",
            new MySqlParameter("@id", id));
        if (dt.Rows.Count == 0) { Response.Write("{\"ok\":false,\"msg\":\"Group not found\"}"); return; }
        DataRow r = dt.Rows[0];
        Response.Write(string.Format(
            "{{\"ok\":true,\"data\":{{\"group_id\":{0},\"group_code\":\"{1}\",\"group_name\":\"{2}\",\"applies_to\":\"{3}\",\"sort_order\":{4},\"is_active\":{5}}}}}",
            SafeInt(r["group_id"]), Js(SafeStr(r["group_code"])), Js(SafeStr(r["group_name"])),
            Js(SafeStr(r["applies_to"])), SafeInt(r["sort_order"]), SafeInt(r["is_active"])));
    }

    private void AjaxSaveGroup()
    {
        int id = SafeInt(F("group_id"));
        string code = F("group_code").ToUpper().Replace(" ", "_");
        string name = F("group_name");
        string applies = F("applies_to").ToUpper();
        int sort = SafeInt(F("sort_order"));

        if (code == "" || code.Length > 30) { Response.Write("{\"ok\":false,\"msg\":\"Group code is required (max 30 characters)\"}"); return; }
        foreach (char c in code)
            if (!(char.IsLetterOrDigit(c) || c == '_')) { Response.Write("{\"ok\":false,\"msg\":\"Group code may contain only letters, digits and _\"}"); return; }
        if (name == "" || name.Length > 150) { Response.Write("{\"ok\":false,\"msg\":\"Group name is required (max 150 characters)\"}"); return; }
        if (Array.IndexOf(AppliesTo, applies) < 0) { Response.Write("{\"ok\":false,\"msg\":\"Choose who the group applies to\"}"); return; }

        DataTable dup = Q("SELECT COUNT(*) AS n FROM appraisal_standard_groups WHERE group_code = @c AND group_id <> @id",
            new MySqlParameter("@c", code), new MySqlParameter("@id", id));
        if (SafeInt(dup.Rows[0]["n"]) > 0) { Response.Write("{\"ok\":false,\"msg\":\"Group code '" + Js(code) + "' is already used\"}"); return; }

        if (id > 0)
        {
            X(@"UPDATE appraisal_standard_groups
                   SET group_code = @c, group_name = @n, applies_to = @a, sort_order = @s, updated_at = NOW()
                 WHERE group_id = @id",
              new MySqlParameter("@c", code), new MySqlParameter("@n", name), new MySqlParameter("@a", applies),
              new MySqlParameter("@s", sort), new MySqlParameter("@id", id));
            Response.Write("{\"ok\":true,\"msg\":\"Group updated\",\"id\":" + id + "}");
        }
        else
        {
            if (sort == 0)
                sort = SafeInt(Q("SELECT IFNULL(MAX(sort_order),0) + 10 AS n FROM appraisal_standard_groups").Rows[0]["n"]);
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    @"INSERT INTO appraisal_standard_groups (group_code, group_name, applies_to, sort_order, is_active, created_at, updated_at)
                      VALUES (@c, @n, @a, @s, 1, NOW(), NOW()); SELECT LAST_INSERT_ID();", conn))
                {
                    cmd.Parameters.AddWithValue("@c", code);
                    cmd.Parameters.AddWithValue("@n", name);
                    cmd.Parameters.AddWithValue("@a", applies);
                    cmd.Parameters.AddWithValue("@s", sort);
                    int newId = Convert.ToInt32(cmd.ExecuteScalar());
                    Response.Write("{\"ok\":true,\"msg\":\"Group created\",\"id\":" + newId + "}");
                }
            }
        }
    }

    private int GroupReferences(int id)
    {
        DataTable dt = Q(
            @"SELECT (SELECT COUNT(*) FROM appraisal_standards WHERE group_id = @id)
                   + (SELECT COUNT(*) FROM hrm_departments WHERE standards_group_id = @id)
                   + (SELECT COUNT(*) FROM appraisal_records WHERE standards_group_id = @id) AS n",
            new MySqlParameter("@id", id));
        return SafeInt(dt.Rows[0]["n"]);
    }

    private void AjaxDeleteGroup()
    {
        int id = SafeInt(F("id"));
        if (id <= 0) { Response.Write("{\"ok\":false,\"msg\":\"Invalid group\"}"); return; }
        if (GroupReferences(id) > 0)
        {
            Response.Write("{\"ok\":false,\"msg\":\"This group has standards, departments or appraisal records attached - deactivate it instead.\"}");
            return;
        }
        int n = X("DELETE FROM appraisal_standard_groups WHERE group_id = @id", new MySqlParameter("@id", id));
        Response.Write(n > 0 ? "{\"ok\":true,\"msg\":\"Group deleted\"}" : "{\"ok\":false,\"msg\":\"Group not found\"}");
    }

    private void AjaxToggle(string table, string key, string label)
    {
        int id = SafeInt(F("id"));
        int active = F("active") == "1" ? 1 : 0;
        int n = X(string.Format("UPDATE {0} SET is_active = @a, updated_at = NOW() WHERE {1} = @id", table, key),
            new MySqlParameter("@a", active), new MySqlParameter("@id", id));
        if (n == 0) { Response.Write("{\"ok\":false,\"msg\":\"Not found\"}"); return; }
        Response.Write("{\"ok\":true,\"msg\":\"" + (label == "group" ? "Group " : "Standard ") + (active == 1 ? "activated" : "deactivated") + "\"}");
    }

    // ── standards ───────────────────────────────────────────────────────
    private void AjaxGetStandard()
    {
        int id = SafeInt(Request.QueryString["id"]);
        DataTable dt = Q("SELECT standard_id, group_id, kpa_title, expected_standard, sort_order, is_active FROM appraisal_standards WHERE standard_id = @id",
            new MySqlParameter("@id", id));
        if (dt.Rows.Count == 0) { Response.Write("{\"ok\":false,\"msg\":\"Standard not found\"}"); return; }
        DataRow r = dt.Rows[0];
        Response.Write(string.Format(
            "{{\"ok\":true,\"data\":{{\"standard_id\":{0},\"group_id\":{1},\"kpa_title\":\"{2}\",\"expected_standard\":\"{3}\",\"sort_order\":{4},\"is_active\":{5}}}}}",
            SafeInt(r["standard_id"]), SafeInt(r["group_id"]), Js(SafeStr(r["kpa_title"])),
            Js(SafeStr(r["expected_standard"])), SafeInt(r["sort_order"]), SafeInt(r["is_active"])));
    }

    private void AjaxSaveStandard()
    {
        int id = SafeInt(F("standard_id"));
        int gid = SafeInt(F("group_id"));
        string kpa = F("kpa_title");
        string std = F("expected_standard");

        if (gid <= 0 || Q("SELECT 1 FROM appraisal_standard_groups WHERE group_id = @g", new MySqlParameter("@g", gid)).Rows.Count == 0)
        { Response.Write("{\"ok\":false,\"msg\":\"Choose a valid group\"}"); return; }
        if (kpa == "" || kpa.Length > 255) { Response.Write("{\"ok\":false,\"msg\":\"Responsibility / KPA is required (max 255 characters)\"}"); return; }
        if (std == "") { Response.Write("{\"ok\":false,\"msg\":\"Expected standard is required\"}"); return; }

        if (id > 0)
        {
            int n = X(@"UPDATE appraisal_standards
                           SET kpa_title = @k, expected_standard = @s, updated_at = NOW()
                         WHERE standard_id = @id AND group_id = @g",
                new MySqlParameter("@k", kpa), new MySqlParameter("@s", std),
                new MySqlParameter("@id", id), new MySqlParameter("@g", gid));
            Response.Write(n > 0 ? "{\"ok\":true,\"msg\":\"Standard updated. Appraisals already started keep the wording they were given.\"}"
                                 : "{\"ok\":false,\"msg\":\"Standard not found\"}");
        }
        else
        {
            int sort = SafeInt(Q("SELECT IFNULL(MAX(sort_order),0) + 1 AS n FROM appraisal_standards WHERE group_id = @g",
                new MySqlParameter("@g", gid)).Rows[0]["n"]);
            X(@"INSERT INTO appraisal_standards (group_id, kpa_title, expected_standard, sort_order, is_active, created_at, updated_at)
                VALUES (@g, @k, @s, @o, 1, NOW(), NOW())",
              new MySqlParameter("@g", gid), new MySqlParameter("@k", kpa),
              new MySqlParameter("@s", std), new MySqlParameter("@o", sort));
            Response.Write("{\"ok\":true,\"msg\":\"Standard added\"}");
        }
    }

    private void AjaxDeleteStandard()
    {
        int id = SafeInt(F("id"));
        if (id <= 0) { Response.Write("{\"ok\":false,\"msg\":\"Invalid standard\"}"); return; }
        int refs = SafeInt(Q("SELECT COUNT(*) AS n FROM appraisal_section_b WHERE standard_id = @id",
            new MySqlParameter("@id", id)).Rows[0]["n"]);
        if (refs > 0)
        {
            Response.Write("{\"ok\":false,\"msg\":\"Used on " + refs + " appraisal row(s) - deactivate it instead.\"}");
            return;
        }
        int n = X("DELETE FROM appraisal_standards WHERE standard_id = @id", new MySqlParameter("@id", id));
        Response.Write(n > 0 ? "{\"ok\":true,\"msg\":\"Standard deleted\"}" : "{\"ok\":false,\"msg\":\"Standard not found\"}");
    }

    private void AjaxReorder()
    {
        int gid = SafeInt(F("group_id"));
        string ids = F("ids");
        if (gid <= 0 || ids == "") { Response.Write("{\"ok\":false,\"msg\":\"Nothing to reorder\"}"); return; }
        int order = 0;
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlTransaction tx = conn.BeginTransaction())
            {
                foreach (string part in ids.Split(','))
                {
                    int sid;
                    if (!int.TryParse(part.Trim(), out sid) || sid <= 0) continue;
                    order++;
                    using (MySqlCommand cmd = new MySqlCommand(
                        "UPDATE appraisal_standards SET sort_order = @o, updated_at = NOW() WHERE standard_id = @id AND group_id = @g", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@o", order);
                        cmd.Parameters.AddWithValue("@id", sid);
                        cmd.Parameters.AddWithValue("@g", gid);
                        cmd.ExecuteNonQuery();
                    }
                }
                tx.Commit();
            }
        }
        Response.Write("{\"ok\":true,\"msg\":\"Order saved\"}");
    }

    private void AjaxMapDepartment()
    {
        int deptId = SafeInt(F("dept_id"));
        int gid = SafeInt(F("group_id"));
        if (deptId <= 0) { Response.Write("{\"ok\":false,\"msg\":\"Invalid department\"}"); return; }
        if (gid > 0 && Q("SELECT 1 FROM appraisal_standard_groups WHERE group_id = @g", new MySqlParameter("@g", gid)).Rows.Count == 0)
        { Response.Write("{\"ok\":false,\"msg\":\"Unknown group\"}"); return; }
        int n = X("UPDATE hrm_departments SET standards_group_id = @g WHERE ID = @d",
            new MySqlParameter("@g", gid > 0 ? (object)gid : DBNull.Value), new MySqlParameter("@d", deptId));
        Response.Write(n > 0 ? "{\"ok\":true,\"msg\":\"Department mapping saved. It applies to records generated from now on.\"}"
                              : "{\"ok\":false,\"msg\":\"Department not found\"}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PAGE RENDER
    // ═══════════════════════════════════════════════════════════════════
    private void LoadPage()
    {
        bool canEdit = HasHrAppraisalAccess();
        litReadOnly.Text = canEdit ? "" :
            "<div class='es-alert es-alert--info'>Read-only: HR or administrator access is required to change the catalogue.</div>" +
            "<script type='text/javascript'>window.ES_READ_ONLY=true;</script>";

        DataTable groups = Q(
            @"SELECT g.group_id, g.group_code, g.group_name, g.applies_to, g.sort_order, g.is_active,
                     (SELECT COUNT(*) FROM appraisal_standards s WHERE s.group_id = g.group_id AND s.is_active = 1) AS n_active,
                     (SELECT COUNT(*) FROM appraisal_standards s WHERE s.group_id = g.group_id) AS n_all,
                     (SELECT COUNT(*) FROM hrm_departments d WHERE d.standards_group_id = g.group_id) AS n_depts,
                     (SELECT COUNT(*) FROM appraisal_records r WHERE r.standards_group_id = g.group_id) AS n_records
              FROM appraisal_standard_groups g
              ORDER BY g.is_active DESC, g.sort_order, g.group_name");

        int selected = QsGroup;
        if (selected == 0 && groups.Rows.Count > 0) selected = SafeInt(groups.Rows[0]["group_id"]);

        // KPIs
        int gActive = 0, sActive = 0;
        foreach (DataRow g in groups.Rows) { if (SafeInt(g["is_active"]) == 1) gActive++; sActive += SafeInt(g["n_active"]); }
        litKpiGroups.Text = gActive.ToString("N0");
        litKpiStandards.Text = sActive.ToString("N0");
        litKpiMapped.Text = SafeInt(Q("SELECT COUNT(*) AS n FROM hrm_departments WHERE standards_group_id IS NOT NULL").Rows[0]["n"]).ToString("N0");
        litKpiUsed.Text = SafeInt(Q("SELECT COUNT(*) AS n FROM appraisal_section_b WHERE standard_id IS NOT NULL").Rows[0]["n"]).ToString("N0");

        // Group list
        StringBuilder sb = new StringBuilder();
        DataRow sel = null;
        foreach (DataRow g in groups.Rows)
        {
            int gid = SafeInt(g["group_id"]);
            bool active = SafeInt(g["is_active"]) == 1;
            if (gid == selected) sel = g;
            sb.AppendFormat("<a href='ExpectedStandards.aspx?gid={0}' class='es-group{1}{2}'>", gid,
                gid == selected ? " is-selected" : "", active ? "" : " is-inactive");
            sb.AppendFormat("<span class='es-group__name'>{0}</span>", Enc(SafeStr(g["group_name"])));
            sb.Append("<span class='es-group__meta'>");
            sb.AppendFormat("<span class='es-code'>{0}</span>", Enc(SafeStr(g["group_code"])));
            sb.AppendFormat("<span class='es-badge es-badge--{0}'>{1}</span>", SafeStr(g["applies_to"]).ToLower(), AppliesLabel(SafeStr(g["applies_to"])));
            if (!active) sb.Append("<span class='es-badge es-badge--off'>Inactive</span>");
            sb.AppendFormat("<span class='es-group__count'>{0} std &middot; {1} dept</span>", SafeInt(g["n_active"]), SafeInt(g["n_depts"]));
            sb.Append("</span></a>");
        }
        if (groups.Rows.Count == 0) sb.Append("<div class='es-empty'>No groups yet. Add the first one.</div>");
        litGroups.Text = sb.ToString();

        // Selected group panel
        if (sel == null)
        {
            litGroupHead.Text = "<div class='es-empty'>Select or create a group.</div>";
            litStandards.Text = "";
            hfGroupId.Value = "0";
        }
        else
        {
            int gid = SafeInt(sel["group_id"]);
            bool active = SafeInt(sel["is_active"]) == 1;
            hfGroupId.Value = gid.ToString();
            int refs = SafeInt(sel["n_all"]) + SafeInt(sel["n_depts"]) + SafeInt(sel["n_records"]);

            StringBuilder h = new StringBuilder();
            h.Append("<div class='es-panel-head'>");
            h.Append("<div class='es-panel-head__text'>");
            h.AppendFormat("<div class='es-panel-head__title'>{0}</div>", Enc(SafeStr(sel["group_name"])));
            h.AppendFormat("<div class='es-panel-head__sub'><span class='es-code'>{0}</span> Applies to: <strong>{1}</strong> &middot; {2} active standard(s) &middot; {3} department(s) mapped &middot; used by {4} appraisal record(s){5}</div>",
                Enc(SafeStr(sel["group_code"])), AppliesLabel(SafeStr(sel["applies_to"])),
                SafeInt(sel["n_active"]), SafeInt(sel["n_depts"]), SafeInt(sel["n_records"]),
                active ? "" : " &middot; <span class='es-badge es-badge--off'>Inactive</span>");
            h.Append("</div>");
            if (canEdit)
            {
                h.Append("<div class='es-panel-head__actions'>");
                h.AppendFormat("<button type='button' class='es-btn es-btn--outline' onclick='editGroup({0})'>{1} Edit Group</button>", gid, IconEdit());
                h.AppendFormat("<button type='button' class='es-btn es-btn--outline' onclick='toggleGroup({0},{1})'>{2}</button>",
                    gid, active ? 0 : 1, active ? "Deactivate" : "Activate");
                if (refs == 0)
                    h.AppendFormat("<button type='button' class='es-btn es-btn--danger-outline' onclick='deleteGroup({0})'>Delete</button>", gid);
                h.AppendFormat("<button type='button' class='es-btn es-btn--primary' onclick='addStandard()'>{0} Add Standard</button>", IconPlus());
                h.Append("</div>");
            }
            h.Append("</div>");
            litGroupHead.Text = h.ToString();

            DataTable stds = Q(
                @"SELECT s.standard_id, s.kpa_title, s.expected_standard, s.sort_order, s.is_active,
                         (SELECT COUNT(*) FROM appraisal_section_b b WHERE b.standard_id = s.standard_id) AS n_used
                  FROM appraisal_standards s
                  WHERE s.group_id = @g
                  ORDER BY s.is_active DESC, s.sort_order, s.standard_id",
                new MySqlParameter("@g", gid));

            StringBuilder t = new StringBuilder();
            if (stds.Rows.Count == 0)
            {
                t.Append("<tr><td colspan='6' class='es-empty'>No standards in this group yet.</td></tr>");
            }
            else
            {
                int i = 0;
                foreach (DataRow s in stds.Rows)
                {
                    i++;
                    int sid = SafeInt(s["standard_id"]);
                    bool sActiveRow = SafeInt(s["is_active"]) == 1;
                    int used = SafeInt(s["n_used"]);
                    t.AppendFormat("<tr data-id='{0}' data-active='{1}'{2}>", sid, sActiveRow ? 1 : 0, sActiveRow ? "" : " class='is-inactive'");
                    t.AppendFormat("<td class='es-num'>{0}</td>", i);
                    t.AppendFormat("<td class='es-kpa'>{0}{1}</td>", Enc(SafeStr(s["kpa_title"])),
                        sActiveRow ? "" : " <span class='es-badge es-badge--off'>Inactive</span>");
                    t.AppendFormat("<td class='es-std'>{0}</td>", Enc(SafeStr(s["expected_standard"])));
                    t.AppendFormat("<td class='es-num'>{0}</td>", used > 0 ? used.ToString() : "<span style='color:#bbb;'>0</span>");
                    t.Append("<td class='es-actions'>");
                    if (canEdit)
                    {
                        if (sActiveRow)
                        {
                            t.Append("<button type='button' class='es-icon-btn' title='Move up' onclick='moveRow(this,-1)'><svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><polyline points='18 15 12 9 6 15'/></svg></button>");
                            t.Append("<button type='button' class='es-icon-btn' title='Move down' onclick='moveRow(this,1)'><svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><polyline points='6 9 12 15 18 9'/></svg></button>");
                        }
                        t.AppendFormat("<button type='button' class='es-icon-btn' title='Edit' onclick='editStandard({0})'>{1}</button>", sid, IconEdit());
                        t.AppendFormat("<button type='button' class='es-icon-btn' title='{0}' onclick='toggleStandard({1},{2})'>{3}</button>",
                            sActiveRow ? "Deactivate" : "Activate", sid, sActiveRow ? 0 : 1,
                            sActiveRow
                                ? "<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><circle cx='12' cy='12' r='10'/><line x1='4.93' y1='4.93' x2='19.07' y2='19.07'/></svg>"
                                : "<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><polyline points='20 6 9 17 4 12'/></svg>");
                        if (used == 0)
                            t.AppendFormat("<button type='button' class='es-icon-btn es-icon-btn--danger' title='Delete (never used)' onclick='deleteStandard({0})'><svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><polyline points='3 6 5 6 21 6'/><path d='M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2'/></svg></button>", sid);
                    }
                    t.Append("</td></tr>");
                }
            }
            litStandards.Text = t.ToString();
        }

        // Department mapping
        StringBuilder opts = new StringBuilder();
        foreach (DataRow g in groups.Rows)
            opts.AppendFormat("<option value='{0}'>{1}{2}</option>", SafeInt(g["group_id"]), Enc(SafeStr(g["group_name"])),
                SafeInt(g["is_active"]) == 1 ? "" : " (inactive)");
        string optHtml = opts.ToString();

        DataTable depts = Q(
            @"SELECT d.ID, d.dept_name, IFNULL(d.faculty_code,'') AS faculty_code, d.standards_group_id,
                     (SELECT COUNT(*) FROM hrm_employee e WHERE e.dept_id = d.ID) AS n_staff
              FROM hrm_departments d
              ORDER BY (d.standards_group_id IS NULL), d.dept_name");
        StringBuilder dsb = new StringBuilder();
        foreach (DataRow d in depts.Rows)
        {
            int did = SafeInt(d["ID"]);
            int cur = SafeInt(d["standards_group_id"]);
            dsb.AppendFormat("<tr><td>{0}</td><td class='es-muted'>{1}</td><td class='es-num'>{2}</td><td>",
                Enc(SafeStr(d["dept_name"])), Enc(SafeStr(d["faculty_code"])), SafeInt(d["n_staff"]));
            dsb.AppendFormat("<select class='es-select es-dept-sel' data-dept='{0}' data-cur='{1}'{2} onchange='mapDept(this)'>", did, cur, canEdit ? "" : " disabled");
            dsb.Append("<option value='0'>Default (by staff category)</option>");
            dsb.Append(cur > 0 ? optHtml.Replace("value='" + cur + "'", "value='" + cur + "' selected") : optHtml);
            dsb.Append("</select></td></tr>");
        }
        if (depts.Rows.Count == 0) dsb.Append("<tr><td colspan='4' class='es-empty'>No departments found.</td></tr>");
        litDepts.Text = dsb.ToString();
    }

    private static string AppliesLabel(string a)
    {
        switch ((a ?? "").ToUpper())
        {
            case "ACADEMIC":       return "Academic";
            case "ADMINISTRATIVE": return "Administrative";
            case "ANY":            return "Any staff";
            default:               return a;
        }
    }

    private static string IconEdit()
    {
        return "<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><path d='M12 20h9'/><path d='M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z'/></svg>";
    }

    private static string IconPlus()
    {
        return "<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><line x1='12' y1='5' x2='12' y2='19'/><line x1='5' y1='12' x2='19' y2='12'/></svg>";
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════
    private static string Js(string val)
    {
        if (string.IsNullOrEmpty(val)) return "";
        StringBuilder sb = new StringBuilder(val.Length + 8);
        foreach (char c in val)
        {
            switch (c)
            {
                case '"':  sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n");  break;
                case '\r': break;
                case '\t': sb.Append("\\t");  break;
                case '<':  sb.Append("\\u003c"); break;
                default:
                    if (c < 0x20) sb.AppendFormat("\\u{0:X4}", (int)c); else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    private static string Enc(string s) { return HttpUtility.HtmlEncode(s ?? ""); }

    private static int SafeInt(object val)
    {
        if (val == null || val == DBNull.Value) return 0;
        // TINYINT(1) columns (is_na, is_active, is_catalogue) arrive as bool with this connection string.
        if (val is bool) return (bool)val ? 1 : 0;
        int result;
        return int.TryParse(val.ToString(), out result) ? result : 0;
    }

    private static string SafeStr(object val)
    {
        return (val == null || val == DBNull.Value) ? "" : val.ToString();
    }

    private DataTable Q(string sql, params MySqlParameter[] parms)
    {
        DataTable dt = new DataTable();
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null) foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) { da.Fill(dt); }
            }
        }
        return dt;
    }

    private int X(string sql, params MySqlParameter[] parms)
    {
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null) foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                return cmd.ExecuteNonQuery();
            }
        }
    }
}
