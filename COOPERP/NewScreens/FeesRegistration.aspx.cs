using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using MySql.Data.MySqlClient;

public partial class COOPERP_NewScreens_FeesRegistration : System.Web.UI.Page
{
    private string ConnectionString
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    private string AcctConnStr
    {
        get
        {
            var cs = ConfigurationManager.ConnectionStrings["accountsConnectionString"];
            return cs != null ? cs.ConnectionString
                : "server=localhost;User Id=root;password=24thdecember1977;database=campus_dynamics_accounts;DefaultCommandTimeout=600;Convert Zero Datetime=True;charset=utf8";
        }
    }

    // ===================================================================
    // PAGE LIFECYCLE
    // ===================================================================

    protected override void OnInit(EventArgs e)
    {
        base.OnInit(e);
        // ViewState is disabled on the master page. Dropdown items must
        // be repopulated in Init (BEFORE LoadPostData restores selections).
        LoadAcademicYears();
        LoadProgrammes();
        LoadNewStudentDropdowns();
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        // Set HTML5 date type (works even with ViewState disabled)
        txtNewStudDOB.Attributes["type"] = "date";

        if (!IsPostBack)
        {
            // Default to current academic year
            string curYear = AcademicYearHelper.GetCurrentAcademicYear();
            if (ddlAcadYear.Items.FindByValue(curYear) != null)
                ddlAcadYear.SelectedValue = curYear;
            ddlSemester.SelectedValue = "";
            ddlAddSemester.SelectedValue = AcademicYearHelper.GetCurrentSemester().ToString();
            UpdateDisplayLabels();
            LoadStats();
            BindGrid();
        }
    }

    // ===================================================================
    // HELPERS - Academic Year / Semester (centralised in AcademicYearHelper)
    // ===================================================================

    private void LoadAcademicYears()
    {
        // Main filter dropdown (with "All" option)
        AcademicYearHelper.PopulateDropDown(ddlAcadYear, true, false);

        // Form dropdown (no "All" option, default to current year)
        AcademicYearHelper.PopulateDropDown(ddlAddAcadYear, false, true);
    }

    private void LoadProgrammes()
    {
        ddlProgramme.Items.Clear();
        ddlProgramme.Items.Add(new ListItem("All Programmes", ""));
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT progcode, progname FROM acad_programme ORDER BY progname", conn))
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        ddlProgramme.Items.Add(new ListItem(
                            rdr["progcode"] + " - " + rdr["progname"], rdr["progcode"].ToString()));
            }
        }
        catch { /* non-fatal */ }
    }

    private void LoadNewStudentDropdowns()
    {
        // Programmes (for new student form - separate from filter)
        ddlNewStudProgramme.Items.Clear();
        ddlNewStudProgramme.Items.Add(new ListItem("- Select Programme -", ""));
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT progcode, progname FROM acad_programme WHERE progcode != '-' ORDER BY progname", conn))
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        ddlNewStudProgramme.Items.Add(new ListItem(
                            rdr["progcode"] + " - " + rdr["progname"],
                            rdr["progcode"].ToString()));
            }
        }
        catch { }

        // Campuses
        ddlNewStudCampus.Items.Clear();
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT campus_code, campus_name FROM acad_campuses WHERE campus_code != '00' ORDER BY campus_name", conn))
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        ddlNewStudCampus.Items.Add(new ListItem(
                            rdr["campus_name"].ToString(),
                            rdr["campus_code"].ToString()));
            }
        }
        catch { }

        // Study Sessions
        ddlNewStudSession.Items.Clear();
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT Session FROM acad_studysessions ORDER BY Session", conn))
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        ddlNewStudSession.Items.Add(new ListItem(
                            rdr["Session"].ToString(),
                            rdr["Session"].ToString()));
            }
        }
        catch { }
        if (ddlNewStudSession.Items.FindByValue("DAY") != null)
            ddlNewStudSession.SelectedValue = "DAY";

        // Entry Years
        ddlNewStudEntryYear.Items.Clear();
        int currentYear = DateTime.Now.Year;
        for (int y = currentYear + 1; y >= currentYear - 5; y--)
            ddlNewStudEntryYear.Items.Add(new ListItem(y.ToString(), y.ToString()));
        if (ddlNewStudEntryYear.Items.FindByValue(currentYear.ToString()) != null)
            ddlNewStudEntryYear.SelectedValue = currentYear.ToString();

        // Billing Systems
        ddlNewStudBilling.Items.Clear();
        try
        {
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT ID, bs_name FROM fin_billing_systems ORDER BY ID", conn))
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        ddlNewStudBilling.Items.Add(new ListItem(
                            rdr["bs_name"].ToString(),
                            rdr["ID"].ToString()));
            }
        }
        catch { }
        if (ddlNewStudBilling.Items.Count > 0)
            ddlNewStudBilling.SelectedIndex = 0;

        // Default intake to AUGUST
        if (ddlNewStudIntake.Items.FindByValue("AUGUST") != null)
            ddlNewStudIntake.SelectedValue = "AUGUST";
    }

    private void UpdateDisplayLabels()
    {
        string yr  = string.IsNullOrEmpty(ddlAcadYear.SelectedValue) ? "All Years"    : ddlAcadYear.SelectedValue;
        string sem = string.IsNullOrEmpty(ddlSemester.SelectedValue) ? "All Semesters" : "Sem " + ddlSemester.SelectedValue;
        litAcadYearDisplay.Text = yr;
        litSemesterDisplay.Text = sem;
        litAcadContext.Text = string.Format(
            "<span style='font-size:11px;color:#174DA4;background:rgba(23,77,164,.08);padding:4px 10px;border-radius:10px;font-weight:600;'>{0} &nbsp;·&nbsp; {1}</span>",
            Server.HtmlEncode(yr), Server.HtmlEncode(sem));
    }

    private string GetCurrentUser()
    {
        if (Session["username"] != null && Session["username"].ToString().Trim() != "")
            return Session["username"].ToString().Trim();
        if (HttpContext.Current.User.Identity.IsAuthenticated)
            return HttpContext.Current.User.Identity.Name;
        return "system";
    }

    // ===================================================================
    // STATS
    // ===================================================================

    private void LoadStats()
    {
        string acadYear = ddlAcadYear.SelectedValue;
        string semester = ddlSemester.SelectedValue;

        var sb = new StringBuilder(@"
            SELECT
                COUNT(*)                                                          AS total,
                SUM(CASE WHEN r.regstatus = 'UNREGISTERED'    THEN 1 ELSE 0 END)  AS unregistered,
                SUM(CASE WHEN r.regstatus = 'REGISTERED'      THEN 1 ELSE 0 END)  AS registered,
                SUM(CASE WHEN r.regstatus = 'LATE REGISTERED' THEN 1 ELSE 0 END)  AS late_reg,
                SUM(CASE WHEN r.regstatus = 'CLEARED'         THEN 1 ELSE 0 END)  AS cleared,
                SUM(CASE WHEN r.regstatus = 'DISCONTINUED'    THEN 1 ELSE 0 END)  AS discontinued,
                SUM(CASE WHEN r.regstatus = 'HALTED'          THEN 1 ELSE 0 END)  AS halted,
                SUM(CASE WHEN r.regstatus = 'DEAD YEAR'       THEN 1 ELSE 0 END)  AS dead_year,
                SUM(CASE WHEN b.bill_count > 0 THEN 1 ELSE 0 END)                 AS billed,
                SUM(CASE WHEN b.bill_count IS NULL OR b.bill_count = 0 THEN 1 ELSE 0 END) AS not_billed
            FROM acad_registration r
            LEFT JOIN (
                SELECT regno, acadyear, semester, COUNT(*) AS bill_count
                FROM campus_dynamics_accounts.fin_studentfeestracking
                WHERE trans_type = 'Bill'
                GROUP BY regno, acadyear, semester
            ) b ON b.regno = r.regno AND b.acadyear = r.acad_year AND b.semester = r.semester
            WHERE 1=1");

        var parms = new List<MySqlParameter>();
        if (!string.IsNullOrEmpty(acadYear)) { sb.Append(" AND r.acad_year = @ay");  parms.Add(new MySqlParameter("@ay", acadYear)); }
        if (!string.IsNullOrEmpty(semester)) { sb.Append(" AND r.semester  = @sem"); parms.Add(new MySqlParameter("@sem", SafeInt(semester, 1))); }

        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(sb.ToString(), conn))
                {
                    foreach (var p in parms) cmd.Parameters.Add(p);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            litTotal.Text          = SafeStr(rdr["total"]);
                            litUnregistered.Text   = SafeStr(rdr["unregistered"]);
                            litRegistered.Text     = SafeStr(rdr["registered"]);
                            litLateRegistered.Text = SafeStr(rdr["late_reg"]);
                            litCleared.Text        = SafeStr(rdr["cleared"]);
                            litDiscontinued.Text   = SafeStr(rdr["discontinued"]);
                            litHalted.Text         = SafeStr(rdr["halted"]);
                            litDeadYear.Text       = SafeStr(rdr["dead_year"]);
                            litBilled.Text         = SafeStr(rdr["billed"]);
                            litNotBilled.Text      = SafeStr(rdr["not_billed"]);
                        }
                    }
                }
            }
        }
        catch { /* non-fatal */ }
    }

    // ===================================================================
    // BIND GRID
    // ===================================================================

    private void BindGrid()
    {
        string search = (txtSearch.Text ?? "").Trim();

        int pageSize = SafeInt(ddlPageSize.SelectedValue, 50);
        if (pageSize > 0) gvRegistration.SettingsPager.PageSize = pageSize;

        var sb = new StringBuilder(@"
            SELECT
                r.ID,
                r.regno,
                r.acad_year,
                r.semester,
                r.regstatus,
                r.studyyear,
                CASE WHEN r.id_cardStatus = 'ISSUED' THEN 'ISSUED' ELSE 'NOT ISSUED' END AS id_cardStatus,
                CASE WHEN r.residence_status = 'RESIDENT' THEN 'RESIDENT' ELSE 'NON-RESIDENT' END AS residence_status,
                COALESCE(r.examClearance, 'UNCLEARED')       AS examClearance,
                CASE WHEN r.examClearanceDate IS NULL OR r.examClearanceDate = '0000-00-00'
                     THEN '' ELSE DATE_FORMAT(r.examClearanceDate,'%d %b %Y') END AS examClearanceDate,
                COALESCE(r.registeredBy, '') AS registeredBy,
                COALESCE(r.clearedBy, '')    AS clearedBy,
                TRIM(CONCAT(COALESCE(s.firstname,''), ' ', COALESCE(s.othername,''))) AS student_name,
                COALESCE(s.progid, '')       AS progcode,
                COALESCE(p.progname, '')     AS progname,
                CASE WHEN b.bill_count > 0 THEN 'BILLED' ELSE 'NOT BILLED' END AS billing_status,
                COALESCE(b.total_billed, 0)  AS total_billed,
                COALESCE(b.total_paid, 0)    AS total_paid
            FROM acad_registration r
            LEFT JOIN acad_student s   ON r.regno  = s.regno
            LEFT JOIN acad_programme p ON s.progid = p.progcode
            LEFT JOIN (
                SELECT regno, acadyear, semester,
                    COUNT(CASE WHEN trans_type = 'Bill' THEN 1 END) AS bill_count,
                    SUM(CASE WHEN trans_type = 'Bill' THEN amount ELSE 0 END) AS total_billed,
                    SUM(CASE WHEN trans_type = 'Payment' THEN amount ELSE 0 END) AS total_paid
                FROM campus_dynamics_accounts.fin_studentfeestracking
                GROUP BY regno, acadyear, semester
            ) b ON b.regno = r.regno AND b.acadyear = r.acad_year AND b.semester = r.semester
            WHERE 1=1");

        var parameters = new List<MySqlParameter>();

        string acadYear = ddlAcadYear.SelectedValue;
        string semStr   = ddlSemester.SelectedValue;
        if (!string.IsNullOrEmpty(acadYear)) { sb.Append(" AND r.acad_year = @acadYear"); parameters.Add(new MySqlParameter("@acadYear", acadYear)); }
        if (!string.IsNullOrEmpty(semStr))   { sb.Append(" AND r.semester  = @semester"); parameters.Add(new MySqlParameter("@semester", SafeInt(semStr, 1))); }

        if (!string.IsNullOrEmpty(ddlStudyYear.SelectedValue))
        {
            sb.Append(" AND r.studyyear = @studyYear");
            parameters.Add(new MySqlParameter("@studyYear", SafeInt(ddlStudyYear.SelectedValue, 0)));
        }
        if (!string.IsNullOrEmpty(ddlRegStatus.SelectedValue))
        {
            sb.Append(" AND r.regstatus = @regStatus");
            parameters.Add(new MySqlParameter("@regStatus", ddlRegStatus.SelectedValue));
        }
        if (!string.IsNullOrEmpty(ddlProgramme.SelectedValue))
        {
            sb.Append(" AND s.progid = @programme");
            parameters.Add(new MySqlParameter("@programme", ddlProgramme.SelectedValue));
        }
        if (!string.IsNullOrEmpty(ddlExamClearance.SelectedValue))
        {
            sb.Append(" AND r.examClearance = @examClear");
            parameters.Add(new MySqlParameter("@examClear", ddlExamClearance.SelectedValue));
        }
        if (!string.IsNullOrEmpty(ddlIDCard.SelectedValue))
        {
            if (ddlIDCard.SelectedValue == "ISSUED")
                sb.Append(" AND r.id_cardStatus = 'ISSUED'");
            else
                sb.Append(" AND (r.id_cardStatus IS NULL OR r.id_cardStatus != 'ISSUED')");
        }
        if (!string.IsNullOrEmpty(ddlResidence.SelectedValue))
        {
            if (ddlResidence.SelectedValue == "RESIDENT")
                sb.Append(" AND r.residence_status = 'RESIDENT'");
            else
                sb.Append(" AND r.residence_status != 'RESIDENT'");
        }
        if (!string.IsNullOrEmpty(ddlBilling.SelectedValue))
        {
            if (ddlBilling.SelectedValue == "BILLED")
                sb.Append(" AND EXISTS(SELECT 1 FROM campus_dynamics_accounts.fin_studentfeestracking ft WHERE ft.regno=r.regno AND ft.acadyear=r.acad_year AND ft.semester=r.semester AND ft.trans_type='Bill')");
            else
                sb.Append(" AND NOT EXISTS(SELECT 1 FROM campus_dynamics_accounts.fin_studentfeestracking ft WHERE ft.regno=r.regno AND ft.acadyear=r.acad_year AND ft.semester=r.semester AND ft.trans_type='Bill')");
        }
        if (!string.IsNullOrEmpty(search))
        {
            sb.Append(" AND (r.regno LIKE @search OR TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) LIKE @search OR s.progid LIKE @search)");
            parameters.Add(new MySqlParameter("@search", "%" + search + "%"));
        }

        sb.Append(" ORDER BY s.firstname, s.othername, r.regno");

        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(sb.ToString(), conn))
                {
                    foreach (var p in parameters) cmd.Parameters.Add(p);
                    using (var da = new MySqlDataAdapter(cmd))
                    {
                        var dt = new DataTable();
                        da.Fill(dt);
                        int count = dt.Rows.Count;
                        lblRecordCount.Text  = count.ToString("N0") + " record" + (count != 1 ? "s" : "");
                        litFooterCount.Text  = count.ToString("N0") + " record" + (count != 1 ? "s" : "");
                        gvRegistration.DataSource = dt;
                        gvRegistration.DataBind();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ShowToast(false, "Error loading data: " + ex.Message);
        }
    }

    // ===================================================================
    // TEMPLATE HELPERS
    // ===================================================================

    protected string ShowIf(object val, string match)
    {
        return val != null && val.ToString().Equals(match, StringComparison.OrdinalIgnoreCase)
            ? "" : "display:none;";
    }
    protected string ShowIfIn(object val, string options)
    {
        if (val == null) return "display:none;";
        foreach (var opt in options.Split('|'))
            if (opt.Trim().Equals(val.ToString().Trim(), StringComparison.OrdinalIgnoreCase))
                return "";
        return "display:none;";
    }
    protected string ShowIfNot(object val, string match)
    {
        return val == null || !val.ToString().Equals(match, StringComparison.OrdinalIgnoreCase)
            ? "" : "display:none;";
    }
    protected string ShowIfNotIn(object val, string options)
    {
        if (val == null) return "";
        foreach (var opt in options.Split('|'))
            if (opt.Trim().Equals(val.ToString().Trim(), StringComparison.OrdinalIgnoreCase))
                return "display:none;";
        return "";
    }

    protected string GetStatusClass(string s)
    {
        switch ((s ?? "").ToUpper().Trim())
        {
            case "UNREGISTERED":    return "unreg";
            case "REGISTERED":      return "reg";
            case "LATE REGISTERED": return "late";
            case "CLEARED":         return "cleared";
            case "DISCONTINUED":    return "discont";
            case "HALTED":          return "halted";
            case "DEAD YEAR":       return "dead";
            default:                return "unreg";
        }
    }
    protected string GetClearanceClass(string s)
    {
        switch ((s ?? "").ToUpper().Trim())
        {
            case "CLEARED":   return "cleared";
            case "PRINTED":   return "printed";
            default:          return "uncleared";
        }
    }
    protected string GetIDCardClass(string s)
    {
        switch ((s ?? "").ToUpper().Trim())
        {
            case "ISSUED":
            case "PRINTED": return "issued";
            default:        return "notissued";
        }
    }
    protected string GetBillingClass(string s)
    {
        return (s ?? "").ToUpper().Trim() == "BILLED" ? "billed" : "notbilled";
    }

    // ===================================================================
    // FILTER CHANGE HANDLERS
    // ===================================================================

    protected void btnSearch_Click(object sender, EventArgs e)                   { LoadStats(); BindGrid(); }
    protected void ddlAcadYear_SelectedIndexChanged(object sender, EventArgs e)   { UpdateDisplayLabels(); LoadStats(); BindGrid(); }
    protected void ddlSemester_SelectedIndexChanged(object sender, EventArgs e)   { UpdateDisplayLabels(); LoadStats(); BindGrid(); }
    protected void ddlStudyYear_SelectedIndexChanged(object sender, EventArgs e)  { LoadStats(); BindGrid(); }
    protected void ddlRegStatus_SelectedIndexChanged(object sender, EventArgs e)  { LoadStats(); BindGrid(); }
    protected void ddlProgramme_SelectedIndexChanged(object sender, EventArgs e)  { LoadStats(); BindGrid(); }
    protected void ddlExamClearance_SelectedIndexChanged(object sender, EventArgs e) { LoadStats(); BindGrid(); }
    protected void ddlIDCard_SelectedIndexChanged(object sender, EventArgs e)     { LoadStats(); BindGrid(); }
    protected void ddlResidence_SelectedIndexChanged(object sender, EventArgs e)  { LoadStats(); BindGrid(); }
    protected void ddlBilling_SelectedIndexChanged(object sender, EventArgs e)    { LoadStats(); BindGrid(); }
    protected void txtSearch_TextChanged(object sender, EventArgs e)              { }
    protected void ddlPageSize_Changed(object sender, EventArgs e)               { LoadStats(); BindGrid(); }

    protected void btnReset_Click(object sender, EventArgs e)
    {
        txtSearch.Text                 = "";
        ddlAcadYear.SelectedValue      = "";
        ddlSemester.SelectedValue      = "";
        ddlStudyYear.SelectedValue     = "";
        ddlRegStatus.SelectedValue     = "";
        ddlProgramme.SelectedValue     = "";
        ddlExamClearance.SelectedValue = "";
        ddlIDCard.SelectedValue        = "";
        ddlResidence.SelectedValue     = "";
        ddlBilling.SelectedValue       = "";
        UpdateDisplayLabels();
        LoadStats();
        BindGrid();
    }
    protected void btnRefresh_Click(object sender, EventArgs e) { LoadStats(); BindGrid(); }

    // ===================================================================
    // INDIVIDUAL ACTIONS
    // ===================================================================

    protected void btnRegister_Click(object sender, EventArgs e)
    {
        ShowToast(false, "Semester registration is now student self-service. Students must register through the student portal.");
        LoadStats(); BindGrid();
    }

    protected void btnLateRegister_Click(object sender, EventArgs e)
    {
        ShowToast(false, "Semester registration is now student self-service. Students must register through the student portal.");
        LoadStats(); BindGrid();
    }

    protected void btnClear_Click(object sender, EventArgs e)
    {
        int id = GetLinkButtonID(sender);
        if (id <= 0) return;
        if (ClearStudent(id))
            ShowToast(true, "Student cleared for exams.");
        else
            ShowToast(false, dcClearRefusal ?? "Could not clear student. They must be Registered or Late Registered first.");
        LoadStats(); BindGrid();
    }

    protected void btnUndoClear_Click(object sender, EventArgs e)
    {
        int id = GetLinkButtonID(sender);
        if (id <= 0) return;
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                const string sql = @"UPDATE acad_registration
                    SET regstatus='REGISTERED', examClearance='UNCLEARED',
                        examClearanceDate=NULL, clearedBy=NULL
                    WHERE ID=@id AND examClearance='CLEARED'";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    bool ok = cmd.ExecuteNonQuery() > 0;
                    ShowToast(ok, ok ? "Exam clearance undone. Student reverted to Registered."
                                    : "Could not undo clearance.");
                }
            }
        }
        catch (Exception ex) { ShowToast(false, "Error: " + ex.Message); }
        LoadStats(); BindGrid();
    }

    protected void btnUnregister_Click(object sender, EventArgs e)
    {
        int id = GetLinkButtonID(sender);
        if (id <= 0) return;
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                const string sql = @"UPDATE acad_registration
                    SET regstatus='UNREGISTERED', registeredBy=NULL
                    WHERE ID=@id AND regstatus IN ('REGISTERED','LATE REGISTERED')";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    bool ok = cmd.ExecuteNonQuery() > 0;
                    ShowToast(ok, ok ? "Registration undone." : "Could not undo registration.");
                }
            }
        }
        catch (Exception ex) { ShowToast(false, "Error: " + ex.Message); }
        LoadStats(); BindGrid();
    }

    protected void btnDiscontinue_Click(object sender, EventArgs e)
    {
        int id = GetLinkButtonID(sender);
        if (id <= 0) return;
        if (ForceStatus(id, "DISCONTINUED"))
            ShowToast(true, "Student marked as Discontinued.");
        else
            ShowToast(false, "Could not update status.");
        LoadStats(); BindGrid();
    }

    protected void btnHalt_Click(object sender, EventArgs e)
    {
        int id = GetLinkButtonID(sender);
        if (id <= 0) return;
        if (ForceStatus(id, "HALTED"))
            ShowToast(true, "Student registration halted.");
        else
            ShowToast(false, "Could not halt registration.");
        LoadStats(); BindGrid();
    }

    protected void btnDeadYear_Click(object sender, EventArgs e)
    {
        int id = GetLinkButtonID(sender);
        if (id <= 0) return;
        if (ForceStatus(id, "DEAD YEAR"))
            ShowToast(true, "Marked as Dead Year.");
        else
            ShowToast(false, "Could not update status.");
        LoadStats(); BindGrid();
    }

    protected void btnReactivate_Click(object sender, EventArgs e)
    {
        int id = GetLinkButtonID(sender);
        if (id <= 0) return;
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                const string sql = @"UPDATE acad_registration
                    SET regstatus='UNREGISTERED', registeredBy=NULL, examClearance='UNCLEARED'
                    WHERE ID=@id AND regstatus IN ('DISCONTINUED','HALTED','DEAD YEAR')";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    bool ok = cmd.ExecuteNonQuery() > 0;
                    ShowToast(ok, ok ? "Student reactivated to Unregistered." : "Could not reactivate student.");
                }
            }
        }
        catch (Exception ex) { ShowToast(false, "Error: " + ex.Message); }
        LoadStats(); BindGrid();
    }

    protected void btnIssueIDCard_Click(object sender, EventArgs e)
    {
        int id = GetLinkButtonID(sender);
        if (id <= 0) return;
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                const string sql = "UPDATE acad_registration SET id_cardStatus='ISSUED' WHERE ID=@id";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    bool ok = cmd.ExecuteNonQuery() > 0;
                    ShowToast(ok, ok ? "ID card marked as Issued." : "Could not update ID card status.");
                }
            }
        }
        catch (Exception ex) { ShowToast(false, "Error: " + ex.Message); }
        BindGrid();
    }

    protected void btnRevokeIDCard_Click(object sender, EventArgs e)
    {
        int id = GetLinkButtonID(sender);
        if (id <= 0) return;
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                const string sql = "UPDATE acad_registration SET id_cardStatus='NOT ISSUED' WHERE ID=@id";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    bool ok = cmd.ExecuteNonQuery() > 0;
                    ShowToast(ok, ok ? "ID card revoked." : "Could not revoke ID card.");
                }
            }
        }
        catch (Exception ex) { ShowToast(false, "Error: " + ex.Message); }
        BindGrid();
    }

    // ===================================================================
    // BATCH ACTIONS
    // ===================================================================

    protected void btnBatchRegister_Click(object sender, EventArgs e)          { RunBatch("register");     }
    protected void btnBatchLateRegister_Click(object sender, EventArgs e)      { RunBatch("late");         }
    protected void btnBatchClear_Click(object sender, EventArgs e)             { RunBatch("clear");        }
    protected void btnBatchUndoReg_Click(object sender, EventArgs e)           { RunBatch("undoreg");      }
    protected void btnBatchUndoClear_Click(object sender, EventArgs e)         { RunBatch("undoclear");    }
    protected void btnBatchDiscontinue_Click(object sender, EventArgs e)       { RunBatch("discontinue"); }
    protected void btnBatchHalt_Click(object sender, EventArgs e)              { RunBatch("halt");         }
    protected void btnBatchDeadYear_Click(object sender, EventArgs e)          { RunBatch("deadyear");     }
    protected void btnBatchReactivate_Click(object sender, EventArgs e)        { RunBatch("reactivate");   }
    protected void btnBatchBill_Click(object sender, EventArgs e)              { RunBatch("bill");         }

    private void RunBatch(string action)
    {
        if (action == "register" || action == "late")
        {
            ShowToast(false, "Semester registration is now student self-service. Students must register through the student portal.");
            return;
        }
        var keys = gvRegistration.GetSelectedFieldValues("ID");
        if (keys == null || keys.Count == 0)
        {
            ShowToast(false, "No students selected.");
            return;
        }
        int processed = 0, skipped = 0;
        string lastBillError = "";
        foreach (object key in keys)
        {
            int id = Convert.ToInt32(key);
            bool ok = false;
            switch (action)
            {
                case "register":    break;
                case "late":        break;
                case "clear":       ok = ClearStudent(id);  break;
                case "undoreg":     ok = BatchUndoReg(id);  break;
                case "undoclear":   ok = BatchUndoClear(id); break;
                case "discontinue": ok = ForceStatus(id, "DISCONTINUED"); break;
                case "halt":        ok = ForceStatus(id, "HALTED");       break;
                case "deadyear":    ok = ForceStatus(id, "DEAD YEAR");    break;
                case "reactivate":  ok = BatchReactivate(id);             break;
                case "bill":
                    string billErr = BillStudentExplicit(id);
                    ok = string.IsNullOrEmpty(billErr);
                    if (!ok) lastBillError = billErr;
                    break;
            }
            if (ok) processed++; else skipped++;
        }
        gvRegistration.Selection.UnselectAll();
        string msg;
        if (action == "bill")
        {
            msg = processed + " student(s) billed successfully.";
            if (skipped > 0) msg += " " + skipped + " failed. " + lastBillError;
        }
        else
        {
            msg = processed + " student(s) updated.";
            if (skipped > 0) msg += " " + skipped + " skipped (status ineligible).";
        }
        ShowToast(processed > 0, msg);
        LoadStats();
        BindGrid();
    }

    // ===================================================================
    // ADD REGISTRATION MODAL
    // ===================================================================

    protected void btnDoAddReg_Click(object sender, EventArgs e)
    {
        string regno     = (txtAddRegNo.Text ?? "").Trim();
        string acadYear  = ddlAddAcadYear.SelectedValue;
        int    semester  = SafeInt(ddlAddSemester.SelectedValue, 1);
        int    studyYear = SafeInt(ddlAddStudyYear.SelectedValue, 1);
        string status    = ddlAddStatus.SelectedValue;
        string residence = ddlAddResidence.SelectedValue;

        if (string.IsNullOrEmpty(regno))
        {
            ShowAddRegError("Please enter a registration number.");
            return;
        }
        if (string.IsNullOrEmpty(acadYear))
        {
            ShowAddRegError("Please select an academic year.");
            return;
        }
        if (semester < 1 || semester > 3)
        {
            ShowAddRegError("Please select a valid semester.");
            return;
        }

        if (status == "REGISTERED" || status == "LATE REGISTERED")
        {
            ShowAddRegError("Semester registration is now student self-service. Students must register through the student portal.");
            return;
        }

        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                long sc = 0;
                using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM acad_student WHERE regno=@r", conn))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    sc = (long)cmd.ExecuteScalar();
                }
                if (sc == 0)
                {
                    ShowAddRegError("Student with registration number \"" + Server.HtmlEncode(regno) + "\" was not found in student records.");
                    return;
                }

                long dc = 0;
                using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM acad_registration WHERE regno=@r AND acad_year=@y AND semester=@s", conn))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    cmd.Parameters.AddWithValue("@y", acadYear);
                    cmd.Parameters.AddWithValue("@s", semester);
                    dc = (long)cmd.ExecuteScalar();
                }
                if (dc > 0)
                {
                    ShowAddRegError(string.Format(
                        "Student {0} already has a registration record for {1}, Semester {2}. Duplicate entries are not allowed.",
                        Server.HtmlEncode(regno), acadYear, semester));
                    return;
                }

                const string insertSql = @"
                    INSERT INTO acad_registration
                        (regno, acad_year, semester, studyyear, regstatus,
                         id_cardStatus, residence_status, examClearance, registeredBy)
                    VALUES
                        (@regno, @year, @sem, @studyYear, @status,
                         'UNPRINTED', @res, 'UNCLEARED', @user)";
                using (var cmd = new MySqlCommand(insertSql, conn))
                {
                    cmd.Parameters.AddWithValue("@regno",     regno);
                    cmd.Parameters.AddWithValue("@year",      acadYear);
                    cmd.Parameters.AddWithValue("@sem",       semester);
                    cmd.Parameters.AddWithValue("@studyYear", studyYear);
                    cmd.Parameters.AddWithValue("@status",    status);
                    cmd.Parameters.AddWithValue("@res",       residence);
                    cmd.Parameters.AddWithValue("@user",      GetCurrentUser());
                    cmd.ExecuteNonQuery();
                }

                // Auto-bill if status is REGISTERED or LATE REGISTERED
                if (status == "REGISTERED" || status == "LATE REGISTERED")
                {
                    // Get the newly inserted ID
                    long newId = 0;
                    using (var cmd2 = new MySqlCommand(
                        "SELECT ID FROM acad_registration WHERE regno=@r AND acad_year=@y AND semester=@s LIMIT 1", conn))
                    {
                        cmd2.Parameters.AddWithValue("@r", regno);
                        cmd2.Parameters.AddWithValue("@y", acadYear);
                        cmd2.Parameters.AddWithValue("@s", semester);
                        object val = cmd2.ExecuteScalar();
                        if (val != null) newId = Convert.ToInt64(val);
                    }
                    if (newId > 0)
                    {
                        string billWarn = AutoBillStudent((int)newId);
                        if (!string.IsNullOrEmpty(billWarn))
                            ScriptManager.RegisterStartupScript(this, GetType(), "billWarn",
                                "setTimeout(function(){showToast(false,'" + billWarn.Replace("'", "\\'") + "');},600);", true);
                    }
                }
            }

            ScriptManager.RegisterStartupScript(this, GetType(), "closeAdd", "closeModal('addRegModal');", true);
            txtAddRegNo.Text = "";
            ShowToast(true, "Registration record added for " + regno + ".");
            LoadStats();
            BindGrid();
        }
        catch (Exception ex)
        {
            ShowAddRegError("Unexpected error: " + ex.Message);
        }
    }

    private void ShowAddRegError(string msg)
    {
        addRegResult.Visible    = true;
        addRegResult.Attributes["class"] = "hr-result hr-result--err";
        litAddRegResult.Text    = Server.HtmlEncode(msg);
        ScriptManager.RegisterStartupScript(this, GetType(), "reopenAdd", "openAddRegModal();", true);
    }

    // ===================================================================
    // REGISTER NEW STUDENT MODAL
    // ===================================================================

    // ===================================================================
    // REGISTER NEW STUDENT MODAL
    // ===================================================================

    protected void btnDoNewStudent_Click(object sender, EventArgs e)
    {
        // -- Gather all form values --
        string title        = ddlNewStudTitle.SelectedValue;
        string fullName     = (txtNewStudName.Text ?? "").Trim().ToUpper();
        string gender       = ddlNewStudGender.SelectedValue;
        string dobStr       = (txtNewStudDOB.Text ?? "").Trim();
        string phone        = (txtNewStudPhone.Text ?? "").Trim();
        string email        = (txtNewStudEmail.Text ?? "").Trim();
        string nationality  = (txtNewStudNationality.Text ?? "").Trim();
        string religion     = ddlNewStudReligion.SelectedValue;
        string marital      = ddlNewStudMarital.SelectedValue;
        string disability   = (txtNewStudDisability.Text ?? "").Trim();

        string programme    = ddlNewStudProgramme.SelectedValue;
        string session      = ddlNewStudSession.SelectedValue;
        string campus       = ddlNewStudCampus.SelectedValue;
        string entryMethod  = ddlNewStudEntryMethod.SelectedValue;
        string entryYear    = ddlNewStudEntryYear.SelectedValue;
        string intake       = ddlNewStudIntake.SelectedValue;
        string billing      = ddlNewStudBilling.SelectedValue;

        string address      = (txtNewStudAddress.Text ?? "").Trim();
        string postBox      = (txtNewStudPostBox.Text ?? "").Trim();
        string district     = (txtNewStudDistrict.Text ?? "").Trim();
        string resCountry   = (txtNewStudResCountry.Text ?? "").Trim();

        string sponsor        = (txtNewStudSponsor.Text ?? "").Trim();
        string sponsorContact = (txtNewStudSponsorContact.Text ?? "").Trim();
        string kinName        = (txtNewStudKinName.Text ?? "").Trim();
        string kinRelation    = (txtNewStudKinRelation.Text ?? "").Trim();
        string kinContact     = (txtNewStudKinContact.Text ?? "").Trim();

        string oLevelSchool = (txtNewStudOLevelSchool.Text ?? "").Trim();
        string oLevelIndex  = (txtNewStudOLevelIndex.Text ?? "").Trim();
        string aLevelSchool = (txtNewStudALevelSchool.Text ?? "").Trim();
        string aLevelIndex  = (txtNewStudALevelIndex.Text ?? "").Trim();

        // -- Validation --
        if (string.IsNullOrEmpty(fullName))
        { ShowNewStudentError("Please enter the student's full name."); return; }
        if (string.IsNullOrEmpty(programme))
        { ShowNewStudentError("Please select a programme."); return; }
        if (string.IsNullOrEmpty(phone))
        { ShowNewStudentError("Please enter the student's phone number."); return; }
        if (string.IsNullOrEmpty(session))
        { ShowNewStudentError("Please select a study session."); return; }
        if (string.IsNullOrEmpty(campus))
        { ShowNewStudentError("Please select a campus."); return; }
        if (string.IsNullOrEmpty(entryYear))
        { ShowNewStudentError("Please select an entry year."); return; }
        if (string.IsNullOrEmpty(billing))
        { ShowNewStudentError("Please select a billing system."); return; }

        // Parse date of birth (default 1980-01-01 if not provided)
        DateTime birthDate = new DateTime(1980, 1, 1);
        if (!string.IsNullOrEmpty(dobStr))
        {
            DateTime parsed;
            if (DateTime.TryParse(dobStr, out parsed))
                birthDate = parsed;
        }

        if (string.IsNullOrEmpty(nationality)) nationality = "UGANDAN";
        if (string.IsNullOrEmpty(district))    district = "UGANDA";

        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                // -- Step 1: Generate entry number ------------------
                string entryNo = "";
                using (var cmd = new MySqlCommand(
                    "SELECT acad_ApplicNoGenerator(@yr) AS eno", conn))
                {
                    cmd.Parameters.AddWithValue("@yr", SafeInt(entryYear, DateTime.Now.Year));
                    object result = cmd.ExecuteScalar();
                    if (result != null) entryNo = result.ToString();
                }
                if (string.IsNullOrEmpty(entryNo) || entryNo == "-")
                {
                    ShowNewStudentError("Failed to generate an entry number. Please try again.");
                    return;
                }

                // -- Step 2: Insert into acad_applications ----------
                const string insertAppSql = @"
                    INSERT INTO acad_applications (
                        stud_entry_no, stud_name, stud_sex, stud_nationality, stud_religion,
                        stud_entry_method, stud_sponsor, sponsor_contact, stud_entry_year,
                        stud_birthdate, stud_phone, next_kin, stud_phy_address, stud_email,
                        stud_mar_stat, stud_campus, IsAccountTransfered, spouse_name,
                        residence_country, olevel_school, alevel_school, referee_name,
                        referee_contacts, referee_comments, letter_campus, health_comments,
                        olevel_index, alevel_index, spouse_contacts, post_box, home_district,
                        stud_occupation, kin_contacts, kin_relationship, alevel_year, hall,
                        stud_intake, spouseOccupation, title, physicalDisability,
                        stud_reg_no, billingID
                    ) VALUES (
                        @entryNo, @name, @sex, @nationality, @religion,
                        @entryMethod, @sponsor, @sponsorContact, @entryYear,
                        @dob, @phone, @kinName, @address, @email,
                        @marital, @campus, 0, '',
                        @resCountry, @oLevelSchool, @aLevelSchool, '',
                        '', '', '', '',
                        @oLevelIndex, @aLevelIndex, '', @postBox, @district,
                        '', @kinContact, @kinRelation, 0, '',
                        @intake, '', @title, @disability,
                        '-', @billingID
                    )";
                using (var cmd = new MySqlCommand(insertAppSql, conn))
                {
                    cmd.Parameters.AddWithValue("@entryNo",        entryNo);
                    cmd.Parameters.AddWithValue("@name",           fullName);
                    cmd.Parameters.AddWithValue("@sex",            gender);
                    cmd.Parameters.AddWithValue("@nationality",    nationality);
                    cmd.Parameters.AddWithValue("@religion",       religion);
                    cmd.Parameters.AddWithValue("@entryMethod",    entryMethod);
                    cmd.Parameters.AddWithValue("@sponsor",        sponsor);
                    cmd.Parameters.AddWithValue("@sponsorContact", sponsorContact);
                    cmd.Parameters.AddWithValue("@entryYear",      entryYear);
                    cmd.Parameters.AddWithValue("@dob",            birthDate);
                    cmd.Parameters.AddWithValue("@phone",          phone);
                    cmd.Parameters.AddWithValue("@kinName",        kinName);
                    cmd.Parameters.AddWithValue("@address",        address);
                    cmd.Parameters.AddWithValue("@email",          email);
                    cmd.Parameters.AddWithValue("@marital",        marital);
                    cmd.Parameters.AddWithValue("@campus",         campus);
                    cmd.Parameters.AddWithValue("@resCountry",     resCountry);
                    cmd.Parameters.AddWithValue("@oLevelSchool",   oLevelSchool);
                    cmd.Parameters.AddWithValue("@aLevelSchool",   aLevelSchool);
                    cmd.Parameters.AddWithValue("@oLevelIndex",    oLevelIndex);
                    cmd.Parameters.AddWithValue("@aLevelIndex",    aLevelIndex);
                    cmd.Parameters.AddWithValue("@postBox",        postBox);
                    cmd.Parameters.AddWithValue("@district",       district);
                    cmd.Parameters.AddWithValue("@kinContact",     kinContact);
                    cmd.Parameters.AddWithValue("@kinRelation",    kinRelation);
                    cmd.Parameters.AddWithValue("@intake",         intake);
                    cmd.Parameters.AddWithValue("@title",          title);
                    cmd.Parameters.AddWithValue("@disability",     disability);
                    cmd.Parameters.AddWithValue("@billingID",      SafeInt(billing, 1));
                    cmd.ExecuteNonQuery();
                }

                // -- Step 3: Insert applicant choice (admitted) -----
                const string insertChoiceSql = @"
                    INSERT INTO acad_applicant_choices
                        (stud_entry_no, Choice, prog_id, adm_status, adm_session, sub_comb)
                    VALUES
                        (@eno, 1, @prog, 1, @session, '-')";
                using (var cmd = new MySqlCommand(insertChoiceSql, conn))
                {
                    cmd.Parameters.AddWithValue("@eno",     entryNo);
                    cmd.Parameters.AddWithValue("@prog",    programme);
                    cmd.Parameters.AddWithValue("@session", session);
                    cmd.ExecuteNonQuery();
                }

                // -- Step 4: Generate registration number -----------
                string regNo = "-";
                using (var cmd = new MySqlCommand(
                    "SELECT acad_RegNoCreator(@eno) AS regno", conn))
                {
                    cmd.Parameters.AddWithValue("@eno", entryNo);
                    object result = cmd.ExecuteScalar();
                    if (result != null) regNo = result.ToString();
                }

                // -- Step 5: Update stud_reg_no in applications -----
                if (regNo != "-" && !string.IsNullOrEmpty(regNo))
                {
                    using (var cmd = new MySqlCommand(
                        "UPDATE acad_applications SET stud_reg_no=@rn WHERE stud_entry_no=@eno AND (stud_reg_no='-' OR stud_reg_no='')",
                        conn))
                    {
                        cmd.Parameters.AddWithValue("@rn",  regNo);
                        cmd.Parameters.AddWithValue("@eno", entryNo);
                        cmd.ExecuteNonQuery();
                    }
                }

                // -- Step 6: Register applicant (acad_student + acad_registration) --
                using (var cmd = new MySqlCommand("acad_RegisterApplicant", conn))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@eyr", SafeInt(entryYear, DateTime.Now.Year));
                    cmd.Parameters.AddWithValue("@eno", entryNo);
                    cmd.Parameters.AddWithValue("@usr", GetCurrentUser());
                    cmd.ExecuteNonQuery();
                }

                // -- Step 7: Update billing system on acad_student --
                using (var cmd = new MySqlCommand(
                    "UPDATE acad_student SET billingID=@bid WHERE regno=@eno", conn))
                {
                    cmd.Parameters.AddWithValue("@bid", SafeInt(billing, 1));
                    cmd.Parameters.AddWithValue("@eno", entryNo);
                    cmd.ExecuteNonQuery();
                }

                // -- Step 8: Immediate registration disabled — students self-register via the portal --

                // -- Success ----------------------------------------
                ScriptManager.RegisterStartupScript(this, GetType(), "closeNewStud",
                    "closeModal('newStudentModal');", true);

                string successMsg = string.Format(
                    "Student created! Entry No: {0}, Reg No: {1}",
                    entryNo, regNo);
                ShowToast(true, successMsg);

                // Clear form fields
                txtNewStudName.Text = "";
                txtNewStudPhone.Text = "";
                txtNewStudEmail.Text = "";
                txtNewStudDOB.Text = "";
                txtNewStudNationality.Text = "UGANDAN";
                txtNewStudAddress.Text = "";
                txtNewStudPostBox.Text = "";
                txtNewStudDistrict.Text = "UGANDA";
                txtNewStudResCountry.Text = "UGANDA";
                txtNewStudSponsor.Text = "";
                txtNewStudSponsorContact.Text = "";
                txtNewStudKinName.Text = "";
                txtNewStudKinRelation.Text = "";
                txtNewStudKinContact.Text = "";
                txtNewStudOLevelSchool.Text = "";
                txtNewStudOLevelIndex.Text = "";
                txtNewStudALevelSchool.Text = "";
                txtNewStudALevelIndex.Text = "";
                txtNewStudDisability.Text = "";

                LoadStats();
                BindGrid();
            }
        }
        catch (Exception ex)
        {
            ShowNewStudentError("Failed to register student: " + ex.Message);
        }
    }

    private void ShowNewStudentError(string msg)
    {
        newStudResult.Visible = true;
        newStudResult.Attributes["class"] = "hr-result hr-result--err";
        litNewStudResult.Text = Server.HtmlEncode(msg);
        ScriptManager.RegisterStartupScript(this, GetType(), "reopenNewStud",
            "openNewStudentModal();", true);
    }

    // ===================================================================
    // CHANGE STATUS MODAL
    // ===================================================================

    protected void btnDoChangeStatus_Click(object sender, EventArgs e)
    {
        int id = SafeInt(hdnCSID.Value, 0);
        if (id <= 0) { ShowToast(false, "Invalid record ID."); return; }

        string newStatus = ddlNewStatus.SelectedValue;
        if (string.IsNullOrEmpty(newStatus)) { ShowToast(false, "Please select a status."); return; }

        if (newStatus == "REGISTERED" || newStatus == "LATE REGISTERED")
        {
            ShowToast(false, "Semester registration is now student self-service. Students must register through the student portal.");
            return;
        }

        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string sql = "UPDATE acad_registration SET regstatus=@s WHERE ID=@id";
                if (newStatus == "CLEARED")
                    sql = "UPDATE acad_registration SET regstatus=@s, examClearance='CLEARED', examClearanceDate=NOW(), clearedBy=@user WHERE ID=@id";
                else if (newStatus == "REGISTERED" || newStatus == "LATE REGISTERED")
                    sql = "UPDATE acad_registration SET regstatus=@s, registeredBy=@user WHERE ID=@id";
                else if (newStatus == "UNREGISTERED")
                    sql = "UPDATE acad_registration SET regstatus=@s, registeredBy=NULL, examClearance='UNCLEARED' WHERE ID=@id";

                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@s",    newStatus);
                    cmd.Parameters.AddWithValue("@id",   id);
                    cmd.Parameters.AddWithValue("@user", GetCurrentUser());
                    bool ok = cmd.ExecuteNonQuery() > 0;
                    string billWarn2 = null;
                    if (ok && (newStatus == "REGISTERED" || newStatus == "LATE REGISTERED"))
                        billWarn2 = AutoBillStudent(id);
                    ScriptManager.RegisterStartupScript(this, GetType(), "closecs", "closeModal('changeStatusModal');", true);
                    string statusMsg = ok ? "Status changed to " + newStatus + "." : "No changes made.";
                    if (!string.IsNullOrEmpty(billWarn2)) statusMsg += " WARNING: " + billWarn2;
                    ShowToast(ok, statusMsg);
                }
            }
        }
        catch (Exception ex) { ShowToast(false, "Error: " + ex.Message); }
        LoadStats();
        BindGrid();
    }

    // ===================================================================
    // EXPORT CSV
    // ===================================================================

    protected void btnExportCsv_Click(object sender, EventArgs e)
    {
        string acadYear = ddlAcadYear.SelectedValue;
        string semStr   = ddlSemester.SelectedValue;
        string search   = (txtSearch.Text ?? "").Trim();

        var sb = new StringBuilder(@"
            SELECT r.regno,
                TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) AS student_name,
                COALESCE(s.progid,'') AS progcode,
                COALESCE(p.progname,'') AS progname,
                r.studyyear, r.regstatus,
                COALESCE(r.examClearance,'UNCLEARED') AS examClearance,
                CASE WHEN r.id_cardStatus = 'ISSUED' THEN 'ISSUED' ELSE 'NOT ISSUED' END AS id_cardStatus,
                CASE WHEN r.residence_status = 'RESIDENT' THEN 'RESIDENT' ELSE 'NON-RESIDENT' END AS residence_status,
                COALESCE(r.registeredBy,'') AS registeredBy,
                COALESCE(r.clearedBy,'') AS clearedBy,
                CASE WHEN r.examClearanceDate IS NULL OR r.examClearanceDate='0000-00-00'
                     THEN '' ELSE DATE_FORMAT(r.examClearanceDate,'%Y-%m-%d') END AS examClearanceDate,
                r.acad_year, r.semester,
                CASE WHEN b.bill_count > 0 THEN 'BILLED' ELSE 'NOT BILLED' END AS billing_status,
                COALESCE(b.total_billed, 0) AS total_billed,
                COALESCE(b.total_paid, 0)   AS total_paid
            FROM acad_registration r
            LEFT JOIN acad_student s ON r.regno=s.regno
            LEFT JOIN acad_programme p ON s.progid=p.progcode
            LEFT JOIN (
                SELECT regno, acadyear, semester,
                    COUNT(CASE WHEN trans_type='Bill' THEN 1 END) AS bill_count,
                    SUM(CASE WHEN trans_type='Bill' THEN amount ELSE 0 END) AS total_billed,
                    SUM(CASE WHEN trans_type='Payment' THEN amount ELSE 0 END) AS total_paid
                FROM campus_dynamics_accounts.fin_studentfeestracking
                GROUP BY regno, acadyear, semester
            ) b ON b.regno=r.regno AND b.acadyear=r.acad_year AND b.semester=r.semester
            WHERE 1=1");

        var parameters = new List<MySqlParameter>();
        if (!string.IsNullOrEmpty(acadYear)) { sb.Append(" AND r.acad_year=@acadYear"); parameters.Add(new MySqlParameter("@acadYear", acadYear)); }
        if (!string.IsNullOrEmpty(semStr))   { sb.Append(" AND r.semester=@semester");  parameters.Add(new MySqlParameter("@semester", SafeInt(semStr, 1))); }
        if (!string.IsNullOrEmpty(ddlStudyYear.SelectedValue))     { sb.Append(" AND r.studyyear=@sy"); parameters.Add(new MySqlParameter("@sy", SafeInt(ddlStudyYear.SelectedValue, 0))); }
        if (!string.IsNullOrEmpty(ddlRegStatus.SelectedValue))     { sb.Append(" AND r.regstatus=@rs"); parameters.Add(new MySqlParameter("@rs", ddlRegStatus.SelectedValue)); }
        if (!string.IsNullOrEmpty(ddlProgramme.SelectedValue))     { sb.Append(" AND s.progid=@prog"); parameters.Add(new MySqlParameter("@prog", ddlProgramme.SelectedValue)); }
        if (!string.IsNullOrEmpty(ddlExamClearance.SelectedValue)) { sb.Append(" AND r.examClearance=@ec"); parameters.Add(new MySqlParameter("@ec", ddlExamClearance.SelectedValue)); }
        if (!string.IsNullOrEmpty(ddlIDCard.SelectedValue))        { if (ddlIDCard.SelectedValue == "ISSUED") sb.Append(" AND r.id_cardStatus='ISSUED'"); else sb.Append(" AND (r.id_cardStatus IS NULL OR r.id_cardStatus!='ISSUED')"); }
        if (!string.IsNullOrEmpty(ddlResidence.SelectedValue))     { if (ddlResidence.SelectedValue == "RESIDENT") sb.Append(" AND r.residence_status='RESIDENT'"); else sb.Append(" AND r.residence_status!='RESIDENT'"); }
        if (!string.IsNullOrEmpty(ddlBilling.SelectedValue))        { if (ddlBilling.SelectedValue == "BILLED") sb.Append(" AND EXISTS(SELECT 1 FROM campus_dynamics_accounts.fin_studentfeestracking ft WHERE ft.regno=r.regno AND ft.acadyear=r.acad_year AND ft.semester=r.semester AND ft.trans_type='Bill')"); else sb.Append(" AND NOT EXISTS(SELECT 1 FROM campus_dynamics_accounts.fin_studentfeestracking ft WHERE ft.regno=r.regno AND ft.acadyear=r.acad_year AND ft.semester=r.semester AND ft.trans_type='Bill')"); }
        if (!string.IsNullOrEmpty(search))                         { sb.Append(" AND (r.regno LIKE @s OR TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) LIKE @s)"); parameters.Add(new MySqlParameter("@s", "%" + search + "%")); }
        sb.Append(" ORDER BY s.firstname, s.othername, r.regno");

        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(sb.ToString(), conn))
                {
                    foreach (var p in parameters) cmd.Parameters.Add(p);
                    using (var da = new MySqlDataAdapter(cmd))
                    {
                        var dt = new DataTable();
                        da.Fill(dt);

                        var csv = new StringBuilder();
                        csv.AppendLine("Reg No,Student Name,Programme Code,Programme Name,Study Year,Reg Status,Exam Clearance,ID Card,Residence,Billing Status,Total Billed,Total Paid,Registered By,Cleared By,Clearance Date,Academic Year,Semester");
                        foreach (DataRow row in dt.Rows)
                        {
                            csv.AppendLine(string.Join(",",
                                CsvEsc(row["regno"]), CsvEsc(row["student_name"]),
                                CsvEsc(row["progcode"]), CsvEsc(row["progname"]),
                                CsvEsc(row["studyyear"]), CsvEsc(row["regstatus"]),
                                CsvEsc(row["examClearance"]), CsvEsc(row["id_cardStatus"]),
                                CsvEsc(row["residence_status"]),
                                CsvEsc(row["billing_status"]),
                                CsvEsc(row["total_billed"]), CsvEsc(row["total_paid"]),
                                CsvEsc(row["registeredBy"]),
                                CsvEsc(row["clearedBy"]), CsvEsc(row["examClearanceDate"]),
                                CsvEsc(row["acad_year"]), CsvEsc(row["semester"])));
                        }

                        string yearPart = string.IsNullOrEmpty(acadYear) ? "all" : acadYear.Replace("/", "_");
                        string semPart  = string.IsNullOrEmpty(semStr)   ? "all" : "sem" + semStr;
                        string filename = string.Format("fees_registration_{0}_{1}.csv", yearPart, semPart);
                        Response.Clear();
                        Response.ContentType = "text/csv";
                        Response.AddHeader("Content-Disposition", "attachment; filename=" + filename);
                        Response.Write(csv.ToString());
                        Response.End();
                    }
                }
            }
        }
        catch (Exception ex) { ShowToast(false, "Export failed: " + ex.Message); }
    }

    private string CsvEsc(object val)
    {
        string s = (val == null || val == DBNull.Value) ? "" : val.ToString();
        if (s.Contains(",") || s.Contains("\"") || s.Contains("\n"))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    // ===================================================================
    // GRID EVENTS
    // ===================================================================

    protected void gvRegistration_HtmlDataCellPrepared(object sender, DevExpress.Web.ASPxGridViewTableDataCellEventArgs e)
    {
        e.Cell.Style["vertical-align"] = "middle";
        if (e.DataColumn.FieldName == "progcode")
        {
            object progname = e.GetValue("progname");
            if (progname != null && progname.ToString().Trim() != "")
                e.Cell.Attributes["title"] = progname.ToString();
        }
    }

    protected void gvRegistration_HtmlRowPrepared(object sender, DevExpress.Web.ASPxGridViewTableRowEventArgs e)
    {
        if (e.RowType != DevExpress.Web.GridViewRowType.Data) return;
        string status = (e.GetValue("regstatus") ?? "").ToString();
        switch (status.ToUpper())
        {
            case "LATE REGISTERED": e.Row.CssClass = "rg-row-late";    break;
            case "CLEARED":         e.Row.CssClass = "rg-row-cleared"; break;
            case "DISCONTINUED":    e.Row.CssClass = "rg-row-discont"; break;
            case "HALTED":          e.Row.CssClass = "rg-row-halted";  break;
            case "DEAD YEAR":       e.Row.CssClass = "rg-row-dead";    break;
        }
    }

    // ===================================================================
    // DATABASE OPERATION PRIMITIVES
    // ===================================================================

    private bool DoSetStatus(int id, string newStatus, string auditCol, string condCol, string condVal)
    {
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string sql = string.Format(
                    "UPDATE acad_registration SET regstatus=@s, {0}=@user WHERE ID=@id AND {1}=@cond",
                    auditCol, condCol);
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@s",    newStatus);
                    cmd.Parameters.AddWithValue("@user", GetCurrentUser());
                    cmd.Parameters.AddWithValue("@id",   id);
                    cmd.Parameters.AddWithValue("@cond", condVal);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }
        catch { return false; }
    }

    /// <summary>
    /// Auto-bill a student after registration. Safe to call multiple times —
    /// fin_TermlyItemBillingFN has a pre-check that skips already-billed items.
    /// Returns null on success (or already billed). Returns a warning string
    /// when billing could not be confirmed — registration is never blocked.
    /// Failures are also logged to fin_billing_errors for admin review.
    /// </summary>
    private string AutoBillStudent(int regId)
    {
        string regno = "", acadYear = "";
        int semester = 0;
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT regno, acad_year, semester FROM acad_registration WHERE ID=@id LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@id", regId);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read()) return null;
                        regno    = rdr["regno"].ToString();
                        acadYear = rdr["acad_year"].ToString();
                        semester = Convert.ToInt32(rdr["semester"]);
                    }
                }
            }
            if (string.IsNullOrEmpty(regno)) return null;

            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("fin_AutoBillOnRegistration", conn))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.CommandTimeout = 120;
                    cmd.Parameters.AddWithValue("@p_regno",    regno);
                    cmd.Parameters.AddWithValue("@p_acadyear", acadYear);
                    cmd.Parameters.AddWithValue("@p_semester",  semester);
                    cmd.Parameters.AddWithValue("@p_user",     GetCurrentUser());
                    using (var rdr = cmd.ExecuteReader()) { while (rdr.NextResult()) { } }
                }

                // Post-billing verification: confirm at least one bill row was created
                using (var chk = new MySqlCommand(
                    "SELECT COUNT(*) FROM fin_studentfeestracking WHERE TRIM(regno)=TRIM(@r) AND acadyear=@a AND semester=@s AND trans_type='Bill'", conn))
                {
                    chk.Parameters.AddWithValue("@r", regno);
                    chk.Parameters.AddWithValue("@a", acadYear);
                    chk.Parameters.AddWithValue("@s", semester);
                    if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                        return null; // billed successfully
                }
            }

            // No bills found after SP ran — log and warn
            string warn = "Billing could not be confirmed for " + regno + " (" + acadYear + " Sem " + semester + "). Please use Fix Billing or Bill Student to complete.";
            LogBillingError(regno, acadYear, semester, warn, "AutoBill-NoRows");
            return warn;
        }
        catch (Exception ex)
        {
            string warn = "Auto-billing failed for " + regno + " (" + acadYear + " Sem " + semester + "): " + ex.Message;
            try { LogBillingError(regno, acadYear, semester, warn, "AutoBill-Exception"); } catch { }
            return warn; // warn caller; registration is not rolled back
        }
    }

    private void LogBillingError(string regno, string acadYear, int semester, string message, string source)
    {
        try
        {
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    @"INSERT IGNORE INTO fin_billing_errors (regno, acad_year, semester, triggered_by, trigger_source, error_message)
                      VALUES (@r, @a, @s, @u, @src, @msg)", conn))
                {
                    cmd.Parameters.AddWithValue("@r",   regno ?? "");
                    cmd.Parameters.AddWithValue("@a",   acadYear ?? "");
                    cmd.Parameters.AddWithValue("@s",   semester);
                    cmd.Parameters.AddWithValue("@u",   GetCurrentUser() ?? "SYSTEM");
                    cmd.Parameters.AddWithValue("@src", source ?? "AutoBill");
                    cmd.Parameters.AddWithValue("@msg", message ?? "");
                    cmd.ExecuteNonQuery();
                }
            }
        }
        catch { /* logging must never fail the caller */ }
    }

    /// <summary>
    /// Explicit billing action - calls fin_Autobilling directly (not the
    /// wrapper SP that blocks on registration status).  Returns a result
    /// string:  "" = success,  otherwise a human-readable error message.
    /// Safe to call multiple times - fin_TermlyItemBillingFN pre-checks.
    /// </summary>
    private string BillStudentExplicit(int regId)
    {
        try
        {
            // -- 1. Read registration row -----------------------------
            string regno = "", acadYear = "", regStatus = "", resStatus = "";
            int semester = 0;
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT regno, acad_year, semester, regstatus, residence_status FROM acad_registration WHERE ID=@id LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@id", regId);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read()) return "Registration record not found.";
                        regno     = rdr["regno"].ToString();
                        acadYear  = rdr["acad_year"].ToString();
                        semester  = Convert.ToInt32(rdr["semester"]);
                        regStatus = (rdr["regstatus"] ?? "").ToString().Trim();
                        resStatus = (rdr["residence_status"] ?? "").ToString().Trim();
                    }
                }
            }
            if (string.IsNullOrEmpty(regno)) return "Registration record has no reg number.";

            // -- 2. Read student profile ------------------------------
            string progid = "", session = "";
            int billingID = 0, entryYear = 0;
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT progid, studsesion, billingID, entryyear FROM acad_student WHERE regno=@r LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read()) return "Student profile (" + regno + ") not found in acad_student.";
                        progid    = (rdr["progid"] ?? "").ToString().Trim();
                        session   = (rdr["studsesion"] ?? "").ToString().Trim();
                        billingID = Convert.ToInt32(rdr["billingID"]);
                        entryYear = Convert.ToInt32(rdr["entryyear"]);
                    }
                }
            }

            // -- 3. Check that a fee schedule actually exists ---------
            int schedCount = 0;
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    @"SELECT COUNT(*) FROM fin_fees_pay_schedule
                      WHERE progid=@p AND billingID=@b AND stud_session=@s AND curr_year=@y", conn))
                {
                    cmd.Parameters.AddWithValue("@p", progid);
                    cmd.Parameters.AddWithValue("@b", billingID);
                    cmd.Parameters.AddWithValue("@s", session);
                    cmd.Parameters.AddWithValue("@y", entryYear);
                    schedCount = Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
            if (schedCount == 0)
            {
                // Also check fin_programme_fees (new fee structure used by fin_BillProgrammeFees)
                using (var conn = new MySqlConnection(AcctConnStr))
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand(
                        "SELECT COUNT(*) FROM fin_programme_fees WHERE progcode=@p AND is_active='Yes'", conn))
                    {
                        cmd.Parameters.AddWithValue("@p", progid);
                        schedCount = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            if (schedCount == 0)
                return "No fee schedule for " + progid + " / " + session
                     + " / BillingID " + billingID + " / EntryYear " + entryYear
                     + ".  Add rows in Fee Pay Schedule or Programme Fees first.";

            // -- 4. Count existing bills before calling SP ------------
            int billsBefore = 0;
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM fin_studentfeestracking WHERE regno=@r AND acadyear=@a AND semester=@s AND trans_type='Bill'", conn))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    cmd.Parameters.AddWithValue("@a", acadYear);
                    cmd.Parameters.AddWithValue("@s", semester);
                    billsBefore = Convert.ToInt32(cmd.ExecuteScalar());
                }
            }

            // -- 5. Call fin_Autobilling directly for REG fees --------
            //    (bypasses the registration-status gate in the wrapper SP
            //     so staff can explicitly bill any student in the grid)
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("fin_Autobilling", conn))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.CommandTimeout = 120;
                    cmd.Parameters.AddWithValue("@reg",  regno);
                    cmd.Parameters.AddWithValue("@acad", acadYear);
                    cmd.Parameters.AddWithValue("@sems", semester);
                    cmd.Parameters.AddWithValue("@typ",  "REG");
                    cmd.Parameters.AddWithValue("@usr",  GetCurrentUser());
                    cmd.Parameters.AddWithValue("@csid", "MANUAL");
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.NextResult()) { }
                    }
                }
            }

            // -- 5b. ACCOMO billing if resident -----------------------
            if (resStatus == "RESIDENT")
            {
                using (var conn = new MySqlConnection(AcctConnStr))
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand("fin_Autobilling", conn))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.CommandTimeout = 120;
                        cmd.Parameters.AddWithValue("@reg",  regno);
                        cmd.Parameters.AddWithValue("@acad", acadYear);
                        cmd.Parameters.AddWithValue("@sems", semester);
                        cmd.Parameters.AddWithValue("@typ",  "ACCOMO");
                        cmd.Parameters.AddWithValue("@usr",  GetCurrentUser());
                        cmd.Parameters.AddWithValue("@csid", "MANUAL");
                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.NextResult()) { }
                        }
                    }
                }
            }

            // -- 6. Verify billing actually happened ------------------
            int billsAfter = 0;
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM fin_studentfeestracking WHERE regno=@r AND acadyear=@a AND semester=@s AND trans_type='Bill'", conn))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    cmd.Parameters.AddWithValue("@a", acadYear);
                    cmd.Parameters.AddWithValue("@s", semester);
                    billsAfter = Convert.ToInt32(cmd.ExecuteScalar());
                }
            }

            if (billsAfter > billsBefore)
                return "";  // success - new bills created
            if (billsAfter > 0)
                return "";  // already billed - that's fine
            return "SP executed but no bills created - check fee schedule match.";
        }
        catch (MySqlException mex)
        {
            // MySQL error 1062 = duplicate key — DB-level uniqueness constraint
            // prevented a duplicate bill. This is safe — student is already billed.
            if (mex.Number == 1062) return "";
            return "Error: " + mex.Message;
        }
        catch (Exception ex) { return "Error: " + ex.Message; }
    }

    /// <summary>
    /// Single-student "Bill Student" action from the row action popover.
    /// </summary>
    protected void btnBillStudent_Click(object sender, EventArgs e)
    {
        try
        {
            LinkButton btn = (LinkButton)sender;
            int regId = Convert.ToInt32(btn.CommandArgument);
            string result = BillStudentExplicit(regId);
            if (string.IsNullOrEmpty(result))
                ShowToast(true, "Student billed successfully.");
            else
                ShowToast(false, result);
        }
        catch (Exception ex)
        {
            ShowToast(false, "Billing error: " + ex.Message);
        }
        LoadStats();
        BindGrid();
    }

    // Why the last ClearStudent call refused, when the refusal was a disciplinary one.
    private string dcClearRefusal;

    private bool ClearStudent(int id)
    {
        dcClearRefusal = null;
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                // Student Discipline: a suspended or expelled student is not cleared for examinations.
                object dcReg = new MySqlCommand("SELECT regno FROM acad_registration WHERE ID=" + id, conn).ExecuteScalar();
                if (dcReg != null)
                {
                    DcClearanceResult dcr = DcClearance.Check(conn, null, Convert.ToString(dcReg));
                    if (dcr.BlocksExamClearance)
                    {
                        dcClearRefusal = "Not cleared: " + dcr.Summary + ". See Student Discipline.";
                        return false;
                    }
                }
                const string sql = @"UPDATE acad_registration
                    SET regstatus='CLEARED', examClearance='CLEARED',
                        examClearanceDate=NOW(), clearedBy=@user
                    WHERE ID=@id AND regstatus IN ('REGISTERED','LATE REGISTERED')";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id",   id);
                    cmd.Parameters.AddWithValue("@user", GetCurrentUser());
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }
        catch { return false; }
    }

    private bool ForceStatus(int id, string newStatus)
    {
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("UPDATE acad_registration SET regstatus=@s WHERE ID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@s",  newStatus);
                    cmd.Parameters.AddWithValue("@id", id);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }
        catch { return false; }
    }

    private bool BatchUndoReg(int id)
    {
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "UPDATE acad_registration SET regstatus='UNREGISTERED', registeredBy=NULL WHERE ID=@id AND regstatus IN ('REGISTERED','LATE REGISTERED')", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }
        catch { return false; }
    }

    private bool BatchUndoClear(int id)
    {
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "UPDATE acad_registration SET regstatus='REGISTERED', examClearance='UNCLEARED', examClearanceDate=NULL, clearedBy=NULL WHERE ID=@id AND examClearance='CLEARED'", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }
        catch { return false; }
    }

    private bool BatchReactivate(int id)
    {
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "UPDATE acad_registration SET regstatus='UNREGISTERED', registeredBy=NULL, examClearance='UNCLEARED' WHERE ID=@id AND regstatus IN ('DISCONTINUED','HALTED','DEAD YEAR')", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }
        catch { return false; }
    }

    // ===================================================================
    // UTILITIES
    // ===================================================================

    private void ShowToast(bool success, string message)
    {
        string js = string.Format("showToast({0},'{1}');",
            success ? "true" : "false",
            message.Replace("'", "\\'").Replace("\r", "").Replace("\n", ""));
        ScriptManager.RegisterStartupScript(this, GetType(), "toast_" + Guid.NewGuid().ToString("N"), js, true);
    }

    private int GetLinkButtonID(object sender)
    {
        var btn = sender as LinkButton;
        if (btn == null) return 0;
        int id;
        return int.TryParse(btn.CommandArgument, out id) ? id : 0;
    }

    private int SafeInt(string val, int def = 0)
    {
        int r; return int.TryParse(val, out r) ? r : def;
    }
    private int SafeInt(object val, int def = 0)
    {
        if (val == null || val == DBNull.Value) return def;
        int r; return int.TryParse(val.ToString(), out r) ? r : def;
    }
    private string SafeStr(object val)
    {
        if (val == null || val == DBNull.Value) return "0";
        string s = val.ToString().Trim();
        if (string.IsNullOrEmpty(s)) return "0";
        long n;
        if (long.TryParse(s, out n)) return n.ToString("N0");
        return s;
    }

    protected string JsEncode(object val)
    {
        string s = (val == null || val == DBNull.Value) ? "" : val.ToString();
        return s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "");
    }
}