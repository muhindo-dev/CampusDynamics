using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web;
using System.Web.UI;
using MySql.Data.MySqlClient;

/// <summary>
/// Competencies (appraisal Section C criteria) per staff category, with a printable catalogue
/// (?action=print, HrDocument). Signed-in staff may view; changes and every ?ajax= call need HR access.
/// </summary>
public partial class COOPERP_NewScreens_CompetencyTemplates : System.Web.UI.Page
{
    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    private string QsCategory { get { return (Request.QueryString["cat"] ?? "").Trim().ToUpper(); } }
    private string QsSearch { get { return (Request.QueryString["q"] ?? "").Trim(); } }

    private static readonly string[] Categories = new string[] { "ACADEMIC", "ADMINISTRATIVE", "SUPPORT" };

    protected string PrintLink
    {
        get { return "CompetencyTemplates.aspx?action=print" + (QsCategory != "" ? "&amp;cat=" + HttpUtility.UrlEncode(QsCategory) : ""); }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        string ajax = (Request.QueryString["ajax"] ?? "").Trim().ToLower();
        if (!string.IsNullOrEmpty(ajax))
        {
            if (!HrAccess.RequireHr(true)) return;
            HandleAjax(ajax);
            return;
        }

        // Signed-in staff may view the catalogue; changes need HR access.
        if (!HrAccess.IsSignedIn()) { HrAccess.RequireHr(false); return; }

        if ((Request.QueryString["action"] ?? "").ToLower() == "print")
        {
            PrintCatalogue();
            return;
        }

        if (!IsPostBack) LoadPage();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AJAX (caller already passed HrAccess.RequireHr)
    // ═══════════════════════════════════════════════════════════════════
    private void HandleAjax(string action)
    {
        Response.Clear();
        Response.ContentType = "application/json";
        try
        {
            if (action == "save" || action == "delete" || action == "reorder")
            {
                if (!string.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                    || !MarksAntiForgeryService.ValidateRequest())
                {
                    Response.Write("{\"ok\":false,\"msg\":\"The page has expired. Refresh it and try again.\"}");
                    try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
                    return;
                }
            }
            switch (action)
            {
                case "save":    AjaxSave(); break;
                case "delete":  AjaxDelete(); break;
                case "reorder": AjaxReorder(); break;
                case "get":     AjaxGet(); break;
                default:        Response.Write("{\"ok\":false,\"msg\":\"Unknown action.\"}"); break;
            }
        }
        catch (System.Threading.ThreadAbortException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("CompetencyTemplates " + action + ": " + ex);
            Response.Write("{\"ok\":false,\"msg\":\"The change could not be saved. Try again, or contact MIS if it keeps failing.\"}");
        }
        try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
    }

    private void AjaxSave()
    {
        int templateId = SafeInt(Request.Form["template_id"]);
        string staffCategory = (Request.Form["staff_category"] ?? "").Trim().ToUpper();
        string competencyCode = (Request.Form["competency_code"] ?? "").Trim();
        string categoryName = (Request.Form["category_name"] ?? "").Trim();
        string competencyName = (Request.Form["competency_name"] ?? "").Trim();
        string description = (Request.Form["description"] ?? "").Trim();
        int sortOrder = SafeInt(Request.Form["sort_order"]);

        if (Array.IndexOf(Categories, staffCategory) < 0) { Msg(false, "Choose a staff category."); return; }
        if (competencyCode == "") { Msg(false, "Enter a code."); return; }
        if (categoryName == "") { Msg(false, "Enter a group."); return; }
        if (competencyName == "") { Msg(false, "Enter the competency."); return; }

        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            if (templateId > 0)
            {
                using (MySqlCommand cmd = new MySqlCommand(
                    @"UPDATE appraisal_competency_templates
                      SET staff_category = @cat, competency_code = @code, category_name = @catName,
                          competency_name = @compName, description = @desc, sort_order = @sort
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
                Msg(true, "Competency saved.");
            }
            else
            {
                if (sortOrder == 0)
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "SELECT IFNULL(MAX(sort_order),0)+1 FROM appraisal_competency_templates WHERE staff_category = @cat", conn))
                    {
                        cmd.Parameters.AddWithValue("@cat", staffCategory);
                        sortOrder = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
                using (MySqlCommand chk = new MySqlCommand(
                    "SELECT COUNT(*) FROM appraisal_competency_templates WHERE staff_category = @cat AND competency_code = @code", conn))
                {
                    chk.Parameters.AddWithValue("@cat", staffCategory);
                    chk.Parameters.AddWithValue("@code", competencyCode);
                    if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                    {
                        Msg(false, "Code " + competencyCode + " is already used in this staff category.");
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
                Msg(true, "Competency added.");
            }
        }
    }

    private void AjaxDelete()
    {
        int templateId = SafeInt(Request.Form["template_id"]);
        if (templateId <= 0) { Msg(false, "Invalid competency."); return; }
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand("DELETE FROM appraisal_competency_templates WHERE template_id = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", templateId);
                if (cmd.ExecuteNonQuery() > 0) Msg(true, "Competency deleted.");
                else Msg(false, "The competency was not found.");
            }
        }
    }

    private void AjaxReorder()
    {
        string ids = (Request.Form["ids"] ?? "").Trim();
        if (ids == "") { Msg(false, "Nothing to reorder."); return; }
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            int order = 0;
            foreach (string part in ids.Split(','))
            {
                int id;
                if (!int.TryParse(part.Trim(), out id) || id <= 0) continue;
                order++;
                using (MySqlCommand cmd = new MySqlCommand("UPDATE appraisal_competency_templates SET sort_order = @s WHERE template_id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@s", order);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }
        Msg(true, "Order saved.");
    }

    private void AjaxGet()
    {
        int templateId = SafeInt(Request.QueryString["id"]);
        DataTable dt = ExecuteQuery("SELECT * FROM appraisal_competency_templates WHERE template_id = @id", new MySqlParameter("@id", templateId));
        if (dt.Rows.Count == 0) { Msg(false, "The competency was not found."); return; }
        DataRow r = dt.Rows[0];
        Response.Write(string.Format(
            "{{\"ok\":true,\"data\":{{\"template_id\":{0},\"staff_category\":\"{1}\",\"competency_code\":\"{2}\",\"category_name\":\"{3}\",\"competency_name\":\"{4}\",\"description\":\"{5}\",\"sort_order\":{6}}}}}",
            SafeInt(r["template_id"]), JsEscape(SafeStr(r["staff_category"])), JsEscape(SafeStr(r["competency_code"])),
            JsEscape(SafeStr(r["category_name"])), JsEscape(SafeStr(r["competency_name"])), JsEscape(SafeStr(r["description"])),
            SafeInt(r["sort_order"])));
    }

    private void Msg(bool ok, string msg)
    {
        Response.Write("{\"ok\":" + (ok ? "true" : "false") + ",\"msg\":\"" + JsEscape(msg) + "\"}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PAGE
    // ═══════════════════════════════════════════════════════════════════
    private void LoadPage()
    {
        bool canEdit = HrAccess.IsHr();
        if (canEdit)
            litAddButton.Text = "<button type='button' class='hr-btn hr-btn--inverse' onclick='openCreateModal()'>" +
                "<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><line x1='12' y1='5' x2='12' y2='19'/><line x1='5' y1='12' x2='19' y2='12'/></svg>" +
                "Add competency</button>";
        try
        {
            LoadCategoryFilter();
            LoadSummaryStats();
            LoadTemplatesList(canEdit);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("CompetencyTemplates: " + ex);
            litError.Text = "<div class='hr-notice hr-notice--bad'>The competencies could not be loaded. Refresh the page or try again later.</div>";
        }
    }

    private void LoadCategoryFilter()
    {
        StringBuilder sb = new StringBuilder("<option value=''>All categories</option>");
        foreach (string c in Categories)
            sb.AppendFormat("<option value='{0}'{1}>{2}</option>", c, c == QsCategory ? " selected" : "", CategoryWord(c));
        litCatOptions.Text = sb.ToString();
    }

    private void LoadSummaryStats()
    {
        DataTable dt = ExecuteQuery(
            @"SELECT staff_category, COUNT(*) AS cnt, COUNT(DISTINCT category_name) AS grp
              FROM appraisal_competency_templates
              GROUP BY staff_category
              ORDER BY FIELD(staff_category,'ACADEMIC','ADMINISTRATIVE','SUPPORT')");
        int totalAll = 0, groupsAll = 0;
        StringBuilder sb = new StringBuilder();
        foreach (DataRow r in dt.Rows)
        {
            string cat = SafeStr(r["staff_category"]);
            int cnt = SafeInt(r["cnt"]), grp = SafeInt(r["grp"]);
            totalAll += cnt; groupsAll += grp;
            sb.AppendFormat("<a class='hr-kpi' href='CompetencyTemplates.aspx?cat={0}'><div class='hr-kpi__label'>{1}</div><div class='hr-kpi__value'>{2}</div><div class='hr-kpi__sub'>{3}</div></a>",
                HttpUtility.UrlEncode(cat), CategoryWord(cat), cnt, grp == 1 ? "1 group" : grp + " groups");
        }
        litKpiTotal.Text = totalAll.ToString("N0");
        litKpiGroups.Text = groupsAll == 1 ? "1 group" : groupsAll.ToString("N0") + " groups";
        litCatStats.Text = sb.ToString();
    }

    private DataTable LoadList()
    {
        string where = " WHERE 1=1";
        List<MySqlParameter> parms = new List<MySqlParameter>();
        if (QsCategory != "") { where += " AND staff_category = @cat"; parms.Add(new MySqlParameter("@cat", QsCategory)); }
        if (QsSearch != "")
        {
            where += " AND (competency_code LIKE @q OR competency_name LIKE @q OR category_name LIKE @q)";
            parms.Add(new MySqlParameter("@q", "%" + QsSearch + "%"));
        }
        return ExecuteQuery(
            @"SELECT template_id, staff_category, competency_code, category_name, competency_name, description, sort_order
              FROM appraisal_competency_templates" + where + @"
              ORDER BY FIELD(staff_category,'ACADEMIC','ADMINISTRATIVE','SUPPORT'), sort_order, competency_code",
            parms.ToArray());
    }

    private void LoadTemplatesList(bool canEdit)
    {
        DataTable dt = LoadList();
        litRecordCount.Text = dt.Rows.Count.ToString("N0");

        StringBuilder sb = new StringBuilder();
        if (dt.Rows.Count == 0)
        {
            sb.Append("<tr><td colspan='5' class='hr-empty'>No competencies match the filters.</td></tr>");
        }
        else
        {
            string lastGroup = "";
            foreach (DataRow r in dt.Rows)
            {
                string cat = SafeStr(r["staff_category"]);
                string groupName = SafeStr(r["category_name"]);
                string key = cat + "|" + groupName;
                if (key != lastGroup)
                {
                    sb.AppendFormat("<tr class='ct-group'><td colspan='5'>{0}: {1}</td></tr>", CategoryWord(cat), Enc(groupName));
                    lastGroup = key;
                }
                int id = SafeInt(r["template_id"]);
                string code = SafeStr(r["competency_code"]);
                sb.Append("<tr>");
                sb.AppendFormat("<td><span class='hr-code'>{0}</span></td>", Enc(code));
                sb.AppendFormat("<td>{0}</td>", Enc(SafeStr(r["competency_name"])));
                sb.AppendFormat("<td class='hr-num'>{0}</td>", SafeInt(r["sort_order"]));
                sb.AppendFormat("<td class='ct-desc'>{0}</td>", Enc(SafeStr(r["description"])));
                sb.Append("<td class='hr-right' style='white-space:nowrap;'>");
                if (canEdit)
                {
                    sb.AppendFormat("<button type='button' class='hr-btn hr-btn--secondary hr-btn--sm' onclick='editRow({0})'>Edit</button> ", id);
                    sb.AppendFormat("<button type='button' class='hr-btn hr-btn--danger hr-btn--sm' onclick=\"deleteRow({0}, '{1}')\">Delete</button>",
                        id, HttpUtility.HtmlAttributeEncode(HttpUtility.JavaScriptStringEncode(code)));
                }
                sb.Append("</td></tr>");
            }
        }
        litRows.Text = sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PRINTABLE CATALOGUE (HrDocument)
    // ═══════════════════════════════════════════════════════════════════
    private void PrintCatalogue()
    {
        HrDocument.Options o = new HrDocument.Options();
        o.Confidential = false;
        o.BackUrl = "CompetencyTemplates.aspx" + (QsCategory != "" ? "?cat=" + HttpUtility.UrlEncode(QsCategory) : "");
        o.Reference = "Section C of the staff performance appraisal" + (QsCategory != "" ? "   |   " + CategoryWord(QsCategory) + " staff" : "");

        StringBuilder h = new StringBuilder();
        DataTable dt;
        try { dt = LoadList(); }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("CompetencyTemplates print: " + ex);
            Response.Write(HrDocument.Page("Competency catalogue", HrDocument.Paragraph("The catalogue could not be loaded."), o));
            Response.End();
            return;
        }
        foreach (string cat in Categories)
        {
            List<DataRow> rows = new List<DataRow>();
            foreach (DataRow r in dt.Rows) if (SafeStr(r["staff_category"]).ToUpper() == cat) rows.Add(r);
            if (rows.Count == 0) continue;
            h.Append(HrDocument.Heading(CategoryWord(cat) + " staff"));
            h.Append("<table class=\"grid\"><thead><tr><th style=\"width:12%\">Code</th><th>Competency</th><th style=\"width:30%\">Description</th></tr></thead><tbody>");
            string last = null;
            foreach (DataRow r in rows)
            {
                string g = SafeStr(r["category_name"]);
                if (g != last)
                {
                    h.AppendFormat("<tr><td colspan=\"3\" style=\"font-weight:700;color:#05275C;background:#f5f7fa;\">{0}</td></tr>", HrDocument.E(g));
                    last = g;
                }
                h.AppendFormat("<tr><td>{0}</td><td>{1}</td><td>{2}</td></tr>",
                    HrDocument.E(SafeStr(r["competency_code"])), HrDocument.E(SafeStr(r["competency_name"])), HrDocument.Multiline(SafeStr(r["description"])));
            }
            h.Append("</tbody></table>");
        }
        if (h.Length == 0) h.Append(HrDocument.Paragraph("No competencies are recorded."));

        Response.Clear();
        Response.ContentType = "text/html";
        Response.Write(HrDocument.Page("Competency catalogue", h.ToString(), o));
        try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════
    private static string CategoryWord(string c)
    {
        switch ((c ?? "").ToUpperInvariant())
        {
            case "ACADEMIC": return "Academic";
            case "ADMINISTRATIVE": return "Administrative";
            case "SUPPORT": return "Support";
            default: return HttpUtility.HtmlEncode(c ?? "");
        }
    }

    private static string Enc(string s) { return HttpUtility.HtmlEncode(HrExport.Clean(s ?? "")); }

    private static string JsEscape(string val)
    {
        if (string.IsNullOrEmpty(val)) return "";
        return val.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n").Replace("<", "\\u003c");
    }

    private static int SafeInt(object val)
    {
        if (val == null || val == DBNull.Value) return 0;
        if (val is bool) return (bool)val ? 1 : 0;
        int result;
        return int.TryParse(val.ToString(), out result) ? result : 0;
    }

    private static string SafeStr(object val) { return val == null || val == DBNull.Value ? "" : val.ToString(); }

    private DataTable ExecuteQuery(string sql, params MySqlParameter[] parms)
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
}
