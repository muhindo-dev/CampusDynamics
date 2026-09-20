using System;
using System.Collections.Generic;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Globalization;
using MySql.Data.MySqlClient;
using System.Configuration;

public partial class COOPERP_NewScreens_AcademicYears : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        // Handle custom edit postback
        string target = Request["__EVENTTARGET"];
        string arg = Request["__EVENTARGUMENT"];
        if (target == "EditYear" && !string.IsNullOrEmpty(arg))
        {
            int editId;
            if (int.TryParse(arg, out editId))
            {
                LoadYearForEdit(editId);
                // Open the modal via script
                ScriptManager.RegisterStartupScript(this, GetType(), "openEdit",
                    "document.getElementById('modalOverlay').classList.add('open');", true);
            }
        }

        if (!IsPostBack)
        {
            BindGrid();
            LoadStats();
        }
    }


    // ---------------------------------------------------
    //  BIND GRID
    // ---------------------------------------------------

    private void BindGrid()
    {
        string connStr = ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString;
        string statusFilter = ddlStatusFilter.SelectedValue;

        string sql = "SELECT * FROM acad_acadyears";
        if (!string.IsNullOrEmpty(statusFilter))
            sql += " WHERE status = @st";
        sql += " ORDER BY acadyear DESC";

        DataTable dt = new DataTable();
        try
        {
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    if (!string.IsNullOrEmpty(statusFilter))
                        cmd.Parameters.AddWithValue("@st", statusFilter);
                    using (MySqlDataAdapter da = new MySqlDataAdapter(cmd))
                        da.Fill(dt);
                }
            }
        }
        catch { }

        // Ensure semester active columns exist in the DataTable even if the
        // migration hasn't been run yet — Eval() will throw if the column is absent.
        string[] semActiveCols = { "semester_1_is_active", "semester_2_is_active", "semester_3_is_active" };
        foreach (string col in semActiveCols)
        {
            if (!dt.Columns.Contains(col))
            {
                dt.Columns.Add(col, typeof(string));
                foreach (DataRow r in dt.Rows) r[col] = "No";
            }
        }

        gridYears.DataSource = dt;
        gridYears.DataBind();

        litShowing.Text = dt.Rows.Count.ToString();
    }

    // ---------------------------------------------------
    //  LOAD STATS
    // ---------------------------------------------------

    private void LoadStats()
    {
        string connStr = ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString;
        try
        {
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(@"
                    SELECT
                        COUNT(*) AS total,
                        SUM(CASE WHEN status='Active' THEN 1 ELSE 0 END) AS active_count,
                        MAX(CASE WHEN is_current_year='Yes' THEN acadyear ELSE NULL END) AS current_acad,
                        MAX(CASE WHEN is_current_financial_year='Yes' THEN acadyear ELSE NULL END) AS current_fin
                    FROM acad_acadyears", conn))
                {
                    using (MySqlDataReader rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            litTotal.Text = rdr["total"] != DBNull.Value ? rdr["total"].ToString() : "0";
                            litActive.Text = rdr["active_count"] != DBNull.Value ? rdr["active_count"].ToString() : "0";
                            litCurrentAcad.Text = rdr["current_acad"] != DBNull.Value ? rdr["current_acad"].ToString() : "-";
                            litCurrentFin.Text = rdr["current_fin"] != DBNull.Value ? rdr["current_fin"].ToString() : "-";
                        }
                    }
                }
            }
        }
        catch { }

        // Active semester display for current academic year
        try { litActiveSems.Text = AcademicYearHelper.GetActiveSemestersDisplay(); }
        catch { litActiveSems.Text = "-"; }
    }

    // ---------------------------------------------------
    //  FILTER
    // ---------------------------------------------------

    protected void ddlStatusFilter_SelectedIndexChanged(object sender, EventArgs e)
    {
        BindGrid();
        LoadStats();
    }

    // ---------------------------------------------------
    //  GRID CALLBACK (unused placeholder)
    // ---------------------------------------------------

    protected void gridYears_CustomButtonCallback(object sender, DevExpress.Web.ASPxGridViewCustomButtonCallbackEventArgs e)
    {
    }

    // ---------------------------------------------------
    //  SAVE  (Add / Edit)
    // ---------------------------------------------------

    protected void btnSave_Click(object sender, EventArgs e)
    {
        string startYearStr = txtStartYear.Text.Trim();
        int startYear;
        if (!int.TryParse(startYearStr, out startYear))
        {
            ShowAlert("Please enter a valid 4-digit start year.", true);
            return;
        }

        string acadyear = string.Format("{0}/{1}", startYear, startYear + 1);

        DateTime startDate;
        DateTime endDate;
        if (!DateTime.TryParse(txtStartDate.Text, out startDate))
        {
            ShowAlert("Please enter a valid start date.", true);
            return;
        }
        if (!DateTime.TryParse(txtEndDate.Text, out endDate))
        {
            ShowAlert("Please enter a valid end date.", true);
            return;
        }

        int semCount;
        if (!int.TryParse(ddlSemesterCount.SelectedValue, out semCount))
            semCount = 2;

        string desc = txtDescription.Text.Trim();
        string status = ddlStatus.SelectedValue;
        string user = Page.User.Identity.IsAuthenticated ? Page.User.Identity.Name : "system";

        // Semester active status
        string sem1 = ddlSem1Active.SelectedValue == "Yes" ? "Yes" : "No";
        string sem2 = ddlSem2Active.SelectedValue == "Yes" ? "Yes" : "No";
        string sem3 = ddlSem3Active.SelectedValue == "Yes" ? "Yes" : "No";

        string editIdStr = hfEditId.Value;
        int editId;
        bool isEdit = int.TryParse(editIdStr, out editId) && editId > 0;

        string result;
        if (isEdit)
        {
            result = AcademicYearHelper.UpdateAcademicYear(editId, acadyear, startDate, endDate, semCount, desc, status, user, sem1, sem2, sem3);
        }
        else
        {
            result = AcademicYearHelper.AddAcademicYear(acadyear, startDate, endDate, semCount, desc, status, user, sem1, sem2, sem3);
        }

        if (!string.IsNullOrEmpty(result))
        {
            ShowAlert(result, true);
            return;
        }

        // Admission travels with the year it belongs to, saved from the same form.
        string admErr = SaveAdmissionFields(acadyear);
        if (!string.IsNullOrEmpty(admErr))
        {
            // The year itself saved; only the admission window was rejected, so say exactly
            // that rather than implying nothing was written.
            ShowAlert("The academic year was saved, but the admission settings were not: " + admErr, true);
            BindGrid();
            LoadStats();
            return;
        }

        // Set as current if checked
        if (chkSetCurrentAcad.Checked)
            AcademicYearHelper.SetCurrentAcademicYear(acadyear, user);
        if (chkSetCurrentFin.Checked)
            AcademicYearHelper.SetCurrentFinancialYear(acadyear, user);

        AcademicYearHelper.AdmissionState admNow = AcademicYearHelper.GetAdmission(acadyear);
        string admNote = admNow.IsAcceptingNow
            ? " Applications are open for " + acadyear + "."
            : (chkAdmissionOpen.Checked
                ? " Admission is ticked but outside its window, so applicants cannot see " + acadyear + " yet."
                : " Applications are closed for " + acadyear + ".");
        ShowAlert((isEdit ? "Academic year updated successfully." : "Academic year added successfully.") + admNote, false);
        BindGrid();
        LoadStats();

        // Close modal and scroll to the alert so the user sees the result
        ScriptManager.RegisterStartupScript(this, GetType(), "closeModal",
            "closeModal(); window.scrollTo({top:0,behavior:'smooth'});", true);
    }

    // ---------------------------------------------------
    //  SET CURRENT  (from Set Current modal)
    // ---------------------------------------------------

    // ---------------------------------------------------
    //  ADMISSION  (apply_intakes, via AcademicYearHelper)
    // ---------------------------------------------------
    //
    //  Opening a year for admission writes the row the application path was
    //  already reading and nobody could write:
    //
    //      eportal  apply/apply-step3.aspx.cs  → builds the Intake / Entry Year list
    //      API      API/v2/apply.aspx.cs       → HandleIntakes
    //
    //  With the table empty both fell back to "this calendar year and the next
    //  two", which is where the bare-year values in stud_intake came from.

    /// <summary>Renders the admission state for one year in the grid.</summary>
    protected string GetAdmissionHtml(object acadyearObj)
    {
        string acadyear = acadyearObj == null ? "" : acadyearObj.ToString();
        AcademicYearHelper.AdmissionState st = AdmissionFor(acadyear);

        string bg, fg, text = st.Describe();
        if (!st.HasRow) { bg = "#f1f5f9"; fg = "#94a3b8"; }
        else if (st.IsAcceptingNow) { bg = "#e8f3ec"; fg = "#1e6b3a"; }
        else if (st.IsOpen) { bg = "#fff8e1"; fg = "#92400e"; }   // open, but outside its window
        else { bg = "#fdecea"; fg = "#b3261e"; }

        return "<span style=\"display:inline-block;font-size:10px;font-weight:700;padding:3px 8px;" +
               "background:" + bg + ";color:" + fg + ";white-space:nowrap;\">" +
               Server.HtmlEncode(text) + "</span>";
    }

    // One read per render rather than one per row.
    private Dictionary<string, AcademicYearHelper.AdmissionState> _admCache;
    private AcademicYearHelper.AdmissionState AdmissionFor(string acadyear)
    {
        if (_admCache == null) _admCache = AcademicYearHelper.GetAdmissionMap();
        AcademicYearHelper.AdmissionState st;
        if (_admCache.TryGetValue((acadyear ?? "").Trim(), out st)) return st;
        return new AcademicYearHelper.AdmissionState();
    }

    /// <summary>Fills the Admission panel for the year being edited.</summary>
    private void LoadAdmissionFields(string acadyear)
    {
        AcademicYearHelper.AdmissionState st = AcademicYearHelper.GetAdmission(acadyear);
        chkAdmissionOpen.Checked = st.IsOpen;
        txtAdmFrom.Text = st.OpenFrom.HasValue ? st.OpenFrom.Value.ToString("yyyy-MM-dd") : "";
        txtAdmTo.Text = st.OpenTo.HasValue ? st.OpenTo.Value.ToString("yyyy-MM-dd") : "";

        // Say what applicants can actually see, and where else admission is open, so a second
        // year is not opened by accident. A year can be ticked and still invisible because its
        // window has not started — worth stating rather than leaving to be deduced.
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("<div style=\"margin-top:12px;padding:9px 12px;background:#f8fafc;border:1px solid #e0e5ed;font-size:11.5px;line-height:1.6;color:#64748b;\">");
        sb.Append("<b style=\"color:#1a1a2e;\">Right now:</b> ");
        sb.Append(st.IsAcceptingNow
            ? "applicants <b style=\"color:#1e6b3a;\">can</b> apply for " + Server.HtmlEncode(acadyear) + "."
            : "applicants <b style=\"color:#b3261e;\">cannot</b> apply for " + Server.HtmlEncode(acadyear) + ".");

        List<string> open = AcademicYearHelper.GetOpenAdmissionYears();
        open.Remove(acadyear);
        sb.Append("<br />");
        if (open.Count == 0)
            sb.Append("No other academic year is accepting applications.");
        else
            sb.Append("Also accepting applications: <b>" + Server.HtmlEncode(string.Join(", ", open.ToArray())) + "</b>.");
        sb.Append("</div>");
        litAdmCurrent.Text = sb.ToString();
    }

    /// <summary>Saves the Admission panel as part of saving the year.</summary>
    private string SaveAdmissionFields(string acadyear)
    {
        DateTime parsed;
        DateTime? from = DateTime.TryParse(txtAdmFrom.Text, out parsed) ? (DateTime?)parsed.Date : null;
        // Inclusive close date: "close on the 31st" has to mean the end of the 31st, or the
        // year disappears from the portal a day early.
        DateTime? to = DateTime.TryParse(txtAdmTo.Text, out parsed)
                       ? (DateTime?)parsed.Date.AddDays(1).AddSeconds(-1) : null;

        string user = Page.User.Identity.IsAuthenticated ? Page.User.Identity.Name : "system";
        return AcademicYearHelper.SetAdmission(acadyear, chkAdmissionOpen.Checked, from, to, user);
    }

    protected void btnSetCurrent_Click(object sender, EventArgs e)
    {
        string acadyear = hfSetCurrentYear.Value;
        if (string.IsNullOrEmpty(acadyear))
        {
            ShowAlert("No academic year selected.", true);
            return;
        }

        string user = Page.User.Identity.IsAuthenticated ? Page.User.Identity.Name : "system";

        if (chkSetAcad.Checked)
        {
            string res = AcademicYearHelper.SetCurrentAcademicYear(acadyear, user);
            if (!string.IsNullOrEmpty(res)) { ShowAlert(res, true); return; }
        }
        if (chkSetFin.Checked)
        {
            string res = AcademicYearHelper.SetCurrentFinancialYear(acadyear, user);
            if (!string.IsNullOrEmpty(res)) { ShowAlert(res, true); return; }
        }

        if (!chkSetAcad.Checked && !chkSetFin.Checked)
        {
            ShowAlert("Please select at least one option (Academic Year or Financial Year).", true);
            return;
        }

        ShowAlert(string.Format("{0} has been set as current.", acadyear), false);
        BindGrid();
        LoadStats();

        ScriptManager.RegisterStartupScript(this, GetType(), "closeSetCurrent", "closeSetCurrentModal();", true);
    }

    // ---------------------------------------------------
    //  LOAD FOR EDIT
    // ---------------------------------------------------

    private void LoadYearForEdit(int id)
    {
        DataRow row = AcademicYearHelper.GetAcademicYearById(id);
        if (row == null) return;

        hfEditId.Value = id.ToString();

        string acadyear = row["acadyear"].ToString();
        LoadAdmissionFields(acadyear);
        string[] parts = acadyear.Split('/');
        if (parts.Length == 2)
        {
            txtStartYear.Text = parts[0];
            txtEndYear.Text = parts[1];
        }

        if (row["start_date"] != DBNull.Value)
            txtStartDate.Text = ((DateTime)row["start_date"]).ToString("yyyy-MM-dd");
        if (row["end_date"] != DBNull.Value)
            txtEndDate.Text = ((DateTime)row["end_date"]).ToString("yyyy-MM-dd");

        if (row["semester_count"] != DBNull.Value)
        {
            string sc = row["semester_count"].ToString();
            if (ddlSemesterCount.Items.FindByValue(sc) != null)
                ddlSemesterCount.SelectedValue = sc;
        }

        if (row["status"] != DBNull.Value)
        {
            string st = row["status"].ToString();
            if (ddlStatus.Items.FindByValue(st) != null)
                ddlStatus.SelectedValue = st;
        }

        // Semester active status
        string[] semCols = { "semester_1_is_active", "semester_2_is_active", "semester_3_is_active" };
        DropDownList[] semDdls = { ddlSem1Active, ddlSem2Active, ddlSem3Active };
        for (int i = 0; i < 3; i++)
        {
            try
            {
                string val = row[semCols[i]] != DBNull.Value ? row[semCols[i]].ToString() : "No";
                if (semDdls[i].Items.FindByValue(val) != null)
                    semDdls[i].SelectedValue = val;
            }
            catch { /* column may not exist yet until migration is run */ }
        }

        txtDescription.Text = row["description"] != DBNull.Value ? row["description"].ToString() : "";

        chkSetCurrentAcad.Checked = row["is_current_year"].ToString() == "Yes";
        chkSetCurrentFin.Checked  = row["is_current_financial_year"].ToString() == "Yes";

        // Set modal title via script
        ScriptManager.RegisterStartupScript(this, GetType(), "setEditTitle",
            "document.getElementById('modalTitle').textContent='Edit Academic Year';" +
            "document.getElementById('yearPreview').textContent='" + acadyear + "';", true);
    }

    // ---------------------------------------------------
    //  GRID TEMPLATE HELPER
    // ---------------------------------------------------

    protected string GetSemesterStatusHtml(object s1, object s2, object s3, object semCount)
    {
        int count = 2;
        int.TryParse(semCount != null ? semCount.ToString() : "2", out count);

        bool a1 = s1 != null && s1.ToString() == "Yes";
        bool a2 = s2 != null && s2.ToString() == "Yes";
        bool a3 = s3 != null && s3.ToString() == "Yes";

        var sb = new System.Text.StringBuilder();
        sb.Append("<div style='display:flex;gap:4px;flex-wrap:wrap;justify-content:center;'>");

        string activeStyle   = "display:inline-block;padding:2px 7px;border-radius:0;font-size:10px;font-weight:700;background:#e8f5e9;color:#2e7d32;border:1px solid #a5d6a7;";
        string inactiveStyle = "display:inline-block;padding:2px 7px;border-radius:0;font-size:10px;font-weight:600;background:#f5f5f5;color:#999;border:1px solid #e0e0e0;";

        sb.AppendFormat("<span style='{0}' title='Semester 1 is {1}'>S1:{2}</span>",
            a1 ? activeStyle : inactiveStyle, a1 ? "Active" : "Closed", a1 ? "&#9679;" : "&#9675;");

        if (count >= 2)
            sb.AppendFormat("<span style='{0}' title='Semester 2 is {1}'>S2:{2}</span>",
                a2 ? activeStyle : inactiveStyle, a2 ? "Active" : "Closed", a2 ? "&#9679;" : "&#9675;");

        if (count >= 3)
            sb.AppendFormat("<span style='{0}' title='Semester 3 is {1}'>S3:{2}</span>",
                a3 ? activeStyle : inactiveStyle, a3 ? "Active" : "Closed", a3 ? "&#9679;" : "&#9675;");

        sb.Append("</div>");
        return sb.ToString();
    }

    // ---------------------------------------------------
    //  ALERT HELPER
    // ---------------------------------------------------

    private void ShowAlert(string message, bool isError)
    {
        pnlAlert.Visible = true;
        pnlAlert.CssClass = isError ? "ay-alert ay-alert--error" : "ay-alert ay-alert--success";
        pnlAlert.Controls.Clear();
        pnlAlert.Controls.Add(new System.Web.UI.LiteralControl(message));
        // Scroll to alert so user always sees feedback
        ScriptManager.RegisterStartupScript(this, GetType(), "scrollAlert",
            "window.scrollTo({top:0,behavior:'smooth'});", true);
    }
}