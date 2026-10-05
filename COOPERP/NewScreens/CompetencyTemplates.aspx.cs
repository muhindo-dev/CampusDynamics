using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web;
using System.Web.UI;
using MySql.Data.MySqlClient;

public partial class COOPERP_NewScreens_CompetencyTemplates : System.Web.UI.Page
{
    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    // ─── Query-string helpers ──────────────────────────────────────────
    private string QsCategory
    {
        get { return (Request.QueryString["cat"] ?? "").Trim().ToUpper(); }
    }

    private string QsSearch
    {
        get { return (Request.QueryString["q"] ?? "").Trim(); }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PAGE LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════
    protected void Page_Load(object sender, EventArgs e)
    {
        // ── AJAX handler ──
        string ajax = (Request.QueryString["ajax"] ?? "").Trim().ToLower();
        if (!string.IsNullOrEmpty(ajax))
        {
            HandleAjax(ajax);
            return;
        }

        if (!IsPostBack)
        {
            LoadPage();
        }
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
            // ?ajax= runs BEFORE SidebarMaster's login check: gate it here.
            // (save/delete were reachable anonymously before this.)
            if (!IsCallerAuthenticated())
            {
                Response.Write("{\"ok\":false,\"msg\":\"Your session has expired. Please sign in again, then retry.\"}");
                try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
                return;
            }
            if (action == "save" || action == "delete" || action == "reorder")
            {
                if (!string.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                    || !MarksAntiForgeryService.ValidateRequest())
                {
                    Response.Write("{\"ok\":false,\"msg\":\"Security validation failed. Please refresh and try again.\"}");
                    try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
                    return;
                }
                if (!HasHrAppraisalAccess())
                {
                    Response.Write("{\"ok\":false,\"msg\":\"Access denied. HR or administrator access is required.\"}");
                    try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
                    return;
                }
            }

            switch (action)
            {
                case "save":       AjaxSave();       break;
                case "delete":     AjaxDelete();     break;
                case "reorder":    AjaxReorder();    break;
                case "get":        AjaxGet();        break;
                default:
                    Response.Write("{\"ok\":false,\"msg\":\"Unknown action\"}");
                    break;
            }
        }
        catch (System.Threading.ThreadAbortException) { /* Response.End() - re-thrown by the runtime */ }
        catch (Exception ex)
        {
            Response.Write("{\"ok\":false,\"msg\":\"" + JsEscape(ex.Message) + "\"}");
        }
        try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
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

    /// <summary>
    /// HR appraisal administration: RBAC admin wildcard, an active sys role 'admin' or
    /// 'hr_manager', or legacy my_aspnet role Administrator / System Admin / Human Resource /
    /// Human Resource Manager. (Same rule as AppraisalView / AppraisalSessions.)
    /// </summary>
    private bool HasHrAppraisalAccess()
    {
        try
        {
            if (!IsCallerAuthenticated()) return false;
            if (RoleAccessService.IsAdmin()) return true;
            string u = Session != null && Session["username"] != null ? Session["username"].ToString().Trim()
                     : (User != null && User.Identity != null ? User.Identity.Name : "");
            if (string.IsNullOrEmpty(u)) return false;
            DataTable dt = ExecuteQuery(
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
            return dt.Rows.Count > 0 && SafeInt(dt.Rows[0]["n"]) > 0;
        }
        catch { return false; }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AJAX: SAVE (create or update)
    // ═══════════════════════════════════════════════════════════════════
    private void AjaxSave()
    {
        int templateId = SafeInt(Request.Form["template_id"]);
        string staffCategory = (Request.Form["staff_category"] ?? "").Trim().ToUpper();
        string competencyCode = (Request.Form["competency_code"] ?? "").Trim();
        string categoryName = (Request.Form["category_name"] ?? "").Trim();
        string competencyName = (Request.Form["competency_name"] ?? "").Trim();
        string description = (Request.Form["description"] ?? "").Trim();
        int sortOrder = SafeInt(Request.Form["sort_order"]);

        // Validation
        if (string.IsNullOrEmpty(staffCategory))
        {
            Response.Write("{\"ok\":false,\"msg\":\"Staff category is required\"}");
            return;
        }
        if (string.IsNullOrEmpty(competencyCode))
        {
            Response.Write("{\"ok\":false,\"msg\":\"Competency code is required\"}");
            return;
        }
        if (string.IsNullOrEmpty(categoryName))
        {
            Response.Write("{\"ok\":false,\"msg\":\"Category name is required\"}");
            return;
        }
        if (string.IsNullOrEmpty(competencyName))
        {
            Response.Write("{\"ok\":false,\"msg\":\"Competency name is required\"}");
            return;
        }

        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();

            if (templateId > 0)
            {
                // UPDATE
                using (MySqlCommand cmd = new MySqlCommand(
                    @"UPDATE appraisal_competency_templates
                      SET staff_category  = @cat,
                          competency_code = @code,
                          category_name   = @catName,
                          competency_name = @compName,
                          description     = @desc,
                          sort_order      = @sort
                      WHERE template_id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@cat", staffCategory);
                    cmd.Parameters.AddWithValue("@code", competencyCode);
                    cmd.Parameters.AddWithValue("@catName", categoryName);
                    cmd.Parameters.AddWithValue("@compName", competencyName);
                    cmd.Parameters.AddWithValue("@desc", description);
                    cmd.Parameters.AddWithValue("@sort", sortOrder);
                    cmd.Parameters.AddWithValue("@id", templateId);
                    cmd.ExecuteNonQuery();
                }
                Response.Write("{\"ok\":true,\"msg\":\"Competency updated successfully\"}");
            }
            else
            {
                // If sort_order = 0, auto-assign next
                if (sortOrder == 0)
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "SELECT IFNULL(MAX(sort_order),0)+1 FROM appraisal_competency_templates WHERE staff_category = @cat", conn))
                    {
                        cmd.Parameters.AddWithValue("@cat", staffCategory);
                        sortOrder = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }

                // Check for duplicate code within category
                using (MySqlCommand chk = new MySqlCommand(
                    "SELECT COUNT(*) FROM appraisal_competency_templates WHERE staff_category = @cat AND competency_code = @code", conn))
                {
                    chk.Parameters.AddWithValue("@cat", staffCategory);
                    chk.Parameters.AddWithValue("@code", competencyCode);
                    if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                    {
                        Response.Write("{\"ok\":false,\"msg\":\"Competency code '" + JsEscape(competencyCode) + "' already exists for this category\"}");
                        return;
                    }
                }

                using (MySqlCommand cmd = new MySqlCommand(
                    @"INSERT INTO appraisal_competency_templates
                        (staff_category, competency_code, category_name, competency_name, description, sort_order)
                      VALUES (@cat, @code, @catName, @compName, @desc, @sort)", conn))
                {
                    cmd.Parameters.AddWithValue("@cat", staffCategory);
                    cmd.Parameters.AddWithValue("@code", competencyCode);
                    cmd.Parameters.AddWithValue("@catName", categoryName);
                    cmd.Parameters.AddWithValue("@compName", competencyName);
                    cmd.Parameters.AddWithValue("@desc", description);
                    cmd.Parameters.AddWithValue("@sort", sortOrder);
                    cmd.ExecuteNonQuery();
                }
                Response.Write("{\"ok\":true,\"msg\":\"Competency created successfully\"}");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AJAX: DELETE
    // ═══════════════════════════════════════════════════════════════════
    private void AjaxDelete()
    {
        int templateId = SafeInt(Request.Form["template_id"]);
        if (templateId <= 0)
        {
            Response.Write("{\"ok\":false,\"msg\":\"Invalid template ID\"}");
            return;
        }

        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(
                "DELETE FROM appraisal_competency_templates WHERE template_id = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", templateId);
                int affected = cmd.ExecuteNonQuery();
                if (affected > 0)
                    Response.Write("{\"ok\":true,\"msg\":\"Competency deleted\"}");
                else
                    Response.Write("{\"ok\":false,\"msg\":\"Record not found\"}");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AJAX: REORDER (batch update sort_order)
    // ═══════════════════════════════════════════════════════════════════
    private void AjaxReorder()
    {
        string ids = (Request.Form["ids"] ?? "").Trim();
        if (string.IsNullOrEmpty(ids))
        {
            Response.Write("{\"ok\":false,\"msg\":\"No IDs provided\"}");
            return;
        }

        string[] parts = ids.Split(',');
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            int order = 0;
            foreach (string part in parts)
            {
                int id;
                if (!int.TryParse(part.Trim(), out id) || id <= 0) continue;
                order++;
                using (MySqlCommand cmd = new MySqlCommand(
                    "UPDATE appraisal_competency_templates SET sort_order = @s WHERE template_id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@s", order);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }
        Response.Write("{\"ok\":true,\"msg\":\"Order saved\"}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AJAX: GET (single record for edit)
    // ═══════════════════════════════════════════════════════════════════
    private void AjaxGet()
    {
        int templateId = SafeInt(Request.QueryString["id"]);
        if (templateId <= 0)
        {
            Response.Write("{\"ok\":false,\"msg\":\"Invalid ID\"}");
            return;
        }

        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT * FROM appraisal_competency_templates WHERE template_id = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", templateId);
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.Append("{\"ok\":true,\"data\":{");
                        sb.AppendFormat("\"template_id\":{0}", rdr.GetInt32(rdr.GetOrdinal("template_id")));
                        sb.AppendFormat(",\"staff_category\":\"{0}\"", JsEscape(rdr.GetString(rdr.GetOrdinal("staff_category"))));
                        sb.AppendFormat(",\"competency_code\":\"{0}\"", JsEscape(rdr.GetString(rdr.GetOrdinal("competency_code"))));
                        sb.AppendFormat(",\"category_name\":\"{0}\"", JsEscape(rdr.GetString(rdr.GetOrdinal("category_name"))));
                        sb.AppendFormat(",\"competency_name\":\"{0}\"", JsEscape(rdr.GetString(rdr.GetOrdinal("competency_name"))));

                        int descOrd = rdr.GetOrdinal("description");
                        string desc = rdr.IsDBNull(descOrd) ? "" : rdr.GetString(descOrd);
                        sb.AppendFormat(",\"description\":\"{0}\"", JsEscape(desc));

                        sb.AppendFormat(",\"sort_order\":{0}", rdr.GetInt32(rdr.GetOrdinal("sort_order")));
                        sb.Append("}}");
                        Response.Write(sb.ToString());
                    }
                    else
                    {
                        Response.Write("{\"ok\":false,\"msg\":\"Not found\"}");
                    }
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PAGE LOAD
    // ═══════════════════════════════════════════════════════════════════
    private void LoadPage()
    {
        try
        {
            LoadCategoryFilter();
            LoadSummaryStats();
            LoadTemplatesList();
        }
        catch (Exception ex)
        {
            litError.Text = "<div class='pa-alert pa-alert--error'>Error: " +
                HttpUtility.HtmlEncode(ex.Message) + "</div>";
        }
    }

    private void LoadCategoryFilter()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("<option value=''>All Categories</option>");
        string[] cats = new string[] { "ACADEMIC", "ADMINISTRATIVE", "SUPPORT" };
        foreach (string c in cats)
        {
            sb.AppendFormat("<option value='{0}'{1}>{2}</option>",
                c,
                c == QsCategory ? " selected" : "",
                c.Substring(0, 1) + c.Substring(1).ToLower());
        }
        litCatOptions.Text = sb.ToString();
    }

    private void LoadSummaryStats()
    {
        DataTable dt = ExecuteQuery(
            @"SELECT
                staff_category,
                COUNT(*) AS cnt,
                COUNT(DISTINCT category_name) AS groups
              FROM appraisal_competency_templates
              GROUP BY staff_category
              ORDER BY FIELD(staff_category,'ACADEMIC','ADMINISTRATIVE','SUPPORT')");

        int totalAll = 0;
        int groupsAll = 0;

        StringBuilder sb = new StringBuilder();
        foreach (DataRow r in dt.Rows)
        {
            string cat = SafeStr(r["staff_category"]);
            int cnt = SafeInt(r["cnt"]);
            int groups = SafeInt(r["groups"]);
            totalAll += cnt;
            groupsAll += groups;

            string color = cat == "ACADEMIC" ? "blue" : cat == "ADMINISTRATIVE" ? "purple" : "teal";
            string label = cat.Substring(0, 1) + cat.Substring(1).ToLower();

            sb.AppendFormat("<div class='pa-kpi pa-kpi--{0}'>", color);
            sb.Append("<div class='pa-kpi__body'>");
            sb.AppendFormat("<div class='pa-kpi__val'>{0}</div>", cnt);
            sb.AppendFormat("<div class='pa-kpi__label'>{0} ({1} groups)</div>", label, groups);
            sb.Append("</div></div>");
        }

        litKpiTotal.Text = totalAll.ToString("N0");
        litKpiGroups.Text = groupsAll.ToString("N0");
        litCatStats.Text = sb.ToString();
    }

    private void LoadTemplatesList()
    {
        string where = " WHERE 1=1";
        List<MySqlParameter> parms = new List<MySqlParameter>();

        if (!string.IsNullOrEmpty(QsCategory))
        {
            where += " AND staff_category = @cat";
            parms.Add(new MySqlParameter("@cat", QsCategory));
        }
        if (!string.IsNullOrEmpty(QsSearch))
        {
            where += " AND (competency_code LIKE @q OR competency_name LIKE @q OR category_name LIKE @q)";
            parms.Add(new MySqlParameter("@q", "%" + QsSearch + "%"));
        }

        string sql = string.Format(
            @"SELECT template_id, staff_category, competency_code, category_name,
                     competency_name, description, sort_order
              FROM appraisal_competency_templates
              {0}
              ORDER BY FIELD(staff_category,'ACADEMIC','ADMINISTRATIVE','SUPPORT'), sort_order, competency_code",
            where);

        DataTable dt = ExecuteQuery(sql, parms.ToArray());

        litRecordCount.Text = dt.Rows.Count.ToString("N0");

        StringBuilder sb = new StringBuilder();
        if (dt.Rows.Count == 0)
        {
            sb.Append("<tr><td colspan='7' style='text-align:center;color:#999;padding:24px;'>No competency templates found</td></tr>");
        }
        else
        {
            string lastGroup = "";
            int rowNum = 0;
            foreach (DataRow r in dt.Rows)
            {
                rowNum++;
                string cat = SafeStr(r["staff_category"]);
                string catName = SafeStr(r["category_name"]);
                string groupKey = cat + "|" + catName;

                // Group header row
                if (groupKey != lastGroup)
                {
                    string catLabel = cat.Substring(0, 1) + cat.Substring(1).ToLower();
                    string catMod = cat.ToLower();
                    sb.AppendFormat("<tr class='ct-group-row'><td colspan='7'>" +
                        "<span class='pa-cat-badge pa-cat-badge--{0}'>{1}</span> " +
                        "<strong>{2}</strong></td></tr>",
                        catMod, catLabel, HttpUtility.HtmlEncode(catName));
                    lastGroup = groupKey;
                }

                sb.AppendFormat("<tr data-id='{0}'>", SafeInt(r["template_id"]));
                sb.AppendFormat("<td class='pa-num' style='color:#aaa;'>{0}</td>", rowNum);
                sb.AppendFormat("<td><code style='background:#f0f2f4;padding:2px 6px;border-radius:3px;font-size:11px;'>{0}</code></td>",
                    HttpUtility.HtmlEncode(SafeStr(r["competency_code"])));
                sb.AppendFormat("<td>{0}</td>",
                    HttpUtility.HtmlEncode(SafeStr(r["competency_name"])));
                sb.AppendFormat("<td style='font-size:11px;color:#666;'>{0}</td>",
                    HttpUtility.HtmlEncode(SafeStr(r["category_name"])));
                sb.AppendFormat("<td class='pa-num'>{0}</td>", SafeInt(r["sort_order"]));

                string desc = SafeStr(r["description"]);
                string descTrunc = desc.Length > 50
                    ? HttpUtility.HtmlEncode(desc.Substring(0, 50)) + "&hellip;"
                    : HttpUtility.HtmlEncode(desc);
                if (string.IsNullOrEmpty(desc)) descTrunc = "<span style='color:#ccc;'>\u2014</span>";
                sb.AppendFormat("<td style='font-size:11px;'>{0}</td>", descTrunc);

                sb.Append("<td style='white-space:nowrap;'>");
                sb.AppendFormat("<button type='button' class='ct-btn ct-btn--edit' onclick='editRow({0})' title='Edit'>",
                    SafeInt(r["template_id"]));
                sb.Append("<svg xmlns='http://www.w3.org/2000/svg' width='13' height='13' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><path d='M12 20h9'/><path d='M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z'/></svg>");
                sb.Append("</button> ");
                sb.AppendFormat("<button type='button' class='ct-btn ct-btn--delete' onclick='deleteRow({0})' title='Delete'>",
                    SafeInt(r["template_id"]));
                sb.Append("<svg xmlns='http://www.w3.org/2000/svg' width='13' height='13' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><polyline points='3 6 5 6 21 6'/><path d='M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2'/></svg>");
                sb.Append("</button>");
                sb.Append("</td>");
                sb.Append("</tr>");
            }
        }
        litRows.Text = sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════

    private static string JsEscape(string val)
    {
        if (string.IsNullOrEmpty(val)) return "";
        return val.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
    }

    private int SafeInt(object val)
    {
        if (val == null || val == DBNull.Value) return 0;
        int result;
        return int.TryParse(val.ToString(), out result) ? result : 0;
    }

    private string SafeStr(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        return val.ToString();
    }

    private DataTable ExecuteQuery(string sql, params MySqlParameter[] parms)
    {
        DataTable dt = new DataTable();
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null)
                    foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) { da.Fill(dt); }
            }
        }
        return dt;
    }
}
