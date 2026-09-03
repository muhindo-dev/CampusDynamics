using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using MySql.Data.MySqlClient;

public partial class COOPERP_NewScreens_StudentsPromotion : System.Web.UI.Page
{
    private string ConnectionString
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }
    
    protected void Page_Load(object sender, EventArgs e)
    {
        if (!IsPostBack)
        {
            LoadAcademicYears();
            LoadProgrammes();
            
            // Set default academic year and semester
            string defaultYear = AcademicYearHelper.GetCurrentAcademicYear();
            if (ddlAcadYear.Items.FindByValue(defaultYear) != null)
                ddlAcadYear.SelectedValue = defaultYear;
            
            // Default to semester 1
            ddlSemester.SelectedValue = "1";
            
            // Set default new academic year (same or next)
            SetDefaultNewAcadYear();
            
            UpdateDisplayLabels();
            LoadStats();
            BindGrid();
        }
    }
    
    // Academic year logic centralised in AcademicYearHelper
    
    private void LoadAcademicYears()
    {
        AcademicYearHelper.PopulateDropDown(ddlAcadYear, false, false);
        // Also populate the new academic year dropdown (includes +2 years for promotion)
        ddlNewAcadYear.Items.Clear();
        int currentYear = DateTime.Now.Year;
        for (int i = currentYear + 2; i >= currentYear - 10; i--)
        {
            string acadYear = string.Format("{0}/{1}", i, i + 1);
            ddlNewAcadYear.Items.Add(new ListItem(acadYear, acadYear));
        }
    }
    
    private void SetDefaultNewAcadYear()
    {
        // By default, new academic year should be same as current or next based on semester
        string currentAcadYear = ddlAcadYear.SelectedValue;
        int currentSem = int.Parse(ddlSemester.SelectedValue);
        
        if (currentSem == 2)
        {
            // If promoting from sem 2, go to next academic year sem 1
            int startYear = int.Parse(currentAcadYear.Split('/')[0]);
            string nextAcadYear = string.Format("{0}/{1}", startYear + 1, startYear + 2);
            if (ddlNewAcadYear.Items.FindByValue(nextAcadYear) != null)
                ddlNewAcadYear.SelectedValue = nextAcadYear;
            ddlNewSemester.SelectedValue = "1";
            chkIncrementYear.Checked = true;
        }
        else
        {
            // If promoting from sem 1, go to sem 2 same year
            ddlNewAcadYear.SelectedValue = currentAcadYear;
            ddlNewSemester.SelectedValue = "2";
            chkIncrementYear.Checked = false;
        }
    }
    
    private void LoadProgrammes()
    {
        ddlProgramme.Items.Clear();
        ddlProgramme.Items.Add(new ListItem("-- All Programmes --", ""));
        
        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            string sql = "SELECT progcode, progname FROM acad_programme ORDER BY progname";
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string code = reader["progcode"].ToString();
                        string name = reader["progname"].ToString();
                        ddlProgramme.Items.Add(new ListItem(code + " - " + name, code));
                    }
                }
            }
        }
    }
    
    private void UpdateDisplayLabels()
    {
        litAcadYearDisplay.Text = ddlAcadYear.SelectedValue;
        litSemesterDisplay.Text = ddlSemester.SelectedValue;
    }
    
    private void LoadStats()
    {
        string acadYear = ddlAcadYear.SelectedValue;
        int semester = int.Parse(ddlSemester.SelectedValue);
        
        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            
            // Get programme durations for completion status calculation
            string sql = @"SELECT 
                           SUM(CASE WHEN r.studyyear < COALESCE(p.couselength, 3) THEN 1 ELSE 0 END) as continuing,
                           SUM(CASE WHEN r.studyyear >= COALESCE(p.couselength, 3) THEN 1 ELSE 0 END) as completing,
                           COUNT(*) as total
                           FROM acad_registration r
                           LEFT JOIN acad_student s ON r.regno = s.regno
                           LEFT JOIN acad_programme p ON s.progid = p.progcode
                           WHERE r.acad_year = @acadYear AND r.semester = @semester";
            
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@acadYear", acadYear);
                cmd.Parameters.AddWithValue("@semester", semester);
                
                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        litContinuing.Text = (reader["continuing"] != DBNull.Value ? reader["continuing"].ToString() : "0");
                        litCompleting.Text = (reader["completing"] != DBNull.Value ? reader["completing"].ToString() : "0");
                        litTotal.Text = (reader["total"] != DBNull.Value ? reader["total"].ToString() : "0");
                        litOther.Text = "0"; // Could add other statuses like HALTED, DISCONTINUED
                    }
                }
            }
        }
    }
    
    private void BindGrid()
    {
        string acadYear = ddlAcadYear.SelectedValue;
        int semester = int.Parse(ddlSemester.SelectedValue);
        
        string sql = @"SELECT r.ID, r.regno, r.acad_year, r.semester, r.regstatus, r.studyyear,
                       r.residence_status, r.examClearance,
                       CONCAT(s.firstname, ' ', COALESCE(s.othername, '')) as student_name,
                       s.progid as progcode, s.intake,
                       COALESCE(p.couselength, 3) as prog_duration,
                       CASE 
                           WHEN r.studyyear >= COALESCE(p.couselength, 3) THEN 'COMPLETING'
                           ELSE 'CONTINUING'
                       END as completion_status
                       FROM acad_registration r
                       LEFT JOIN acad_student s ON r.regno = s.regno
                       LEFT JOIN acad_programme p ON s.progid = p.progcode
                       WHERE r.acad_year = @acadYear AND r.semester = @semester";
        
        // Apply filters
        if (!string.IsNullOrEmpty(ddlStudyYear.SelectedValue))
            sql += " AND r.studyyear = @studyYear";
        
        if (!string.IsNullOrEmpty(ddlProgramme.SelectedValue))
            sql += " AND s.progid = @programme";
        
        if (!string.IsNullOrEmpty(ddlIntake.SelectedValue))
            sql += " AND s.intake = @intake";
        
        if (!string.IsNullOrEmpty(ddlCompletionStatus.SelectedValue))
        {
            if (ddlCompletionStatus.SelectedValue == "CONTINUING")
                sql += " AND r.studyyear < COALESCE(p.couselength, 3)";
            else if (ddlCompletionStatus.SelectedValue == "COMPLETING")
                sql += " AND r.studyyear >= COALESCE(p.couselength, 3)";
        }
        
        sql += " ORDER BY s.firstname, s.othername";
        
        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@acadYear", acadYear);
                cmd.Parameters.AddWithValue("@semester", semester);
                
                if (!string.IsNullOrEmpty(ddlStudyYear.SelectedValue))
                    cmd.Parameters.AddWithValue("@studyYear", int.Parse(ddlStudyYear.SelectedValue));
                
                if (!string.IsNullOrEmpty(ddlProgramme.SelectedValue))
                    cmd.Parameters.AddWithValue("@programme", ddlProgramme.SelectedValue);
                
                if (!string.IsNullOrEmpty(ddlIntake.SelectedValue))
                    cmd.Parameters.AddWithValue("@intake", ddlIntake.SelectedValue);
                
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    da.Fill(dt);
                    gvPromotion.DataSource = dt;
                    gvPromotion.DataBind();
                }
            }
        }
    }
    
    protected string GetStatusClass(string status)
    {
        switch (status.ToUpper())
        {
            case "UNREGISTERED": return "unregistered";
            case "REGISTERED": return "registered";
            case "CLEARED": return "cleared";
            case "DISCONTINUED": return "discontinued";
            case "LATE REGISTERED": return "late";
            case "HALTED": return "halted";
            case "DEAD YEAR": return "deadyear";
            default: return "";
        }
    }
    
    protected string GetCompletionClass(string status)
    {
        switch (status.ToUpper())
        {
            case "CONTINUING": return "continuing";
            case "COMPLETING": return "completing";
            case "COMPLETED": return "completed";
            default: return "";
        }
    }
    
    protected void gvPromotion_HtmlDataCellPrepared(object sender, DevExpress.Web.ASPxGridViewTableDataCellEventArgs e)
    {
        // Ensure action cell allows overflow
        if (e.Cell.CssClass != null && e.Cell.CssClass.Contains("cd-action-cell"))
        {
            e.Cell.Style["overflow"] = "visible";
            e.Cell.Style["position"] = "relative";
        }
    }
    
    #region Filter Events
    
    protected void ddlAcadYear_SelectedIndexChanged(object sender, EventArgs e)
    {
        SetDefaultNewAcadYear();
        UpdateDisplayLabels();
        LoadStats();
        BindGrid();
    }
    
    protected void ddlSemester_SelectedIndexChanged(object sender, EventArgs e)
    {
        SetDefaultNewAcadYear();
        UpdateDisplayLabels();
        LoadStats();
        BindGrid();
    }
    
    protected void ddlStudyYear_SelectedIndexChanged(object sender, EventArgs e)
    {
        LoadStats();
        BindGrid();
    }
    
    protected void ddlProgramme_SelectedIndexChanged(object sender, EventArgs e)
    {
        LoadStats();
        BindGrid();
    }
    
    protected void ddlIntake_SelectedIndexChanged(object sender, EventArgs e)
    {
        LoadStats();
        BindGrid();
    }
    
    protected void ddlCompletionStatus_SelectedIndexChanged(object sender, EventArgs e)
    {
        LoadStats();
        BindGrid();
    }
    
    #endregion
    
    #region Individual Actions
    
    protected void btnPromote_Click(object sender, EventArgs e)
    {
        try
        {
            LinkButton btn = (LinkButton)sender;
            int regId = int.Parse(btn.CommandArgument);
            
            PromoteStudent(regId);
            
            ShowMessage("Student promoted successfully.");
            LoadStats();
            BindGrid();
        }
        catch (Exception ex)
        {
            ShowMessage("Error: " + ex.Message);
        }
    }
    
    protected void btnDelete_Click(object sender, EventArgs e)
    {
        try
        {
            LinkButton btn = (LinkButton)sender;
            int regId = int.Parse(btn.CommandArgument);
            
            DeleteRegistration(regId);
            
            ShowMessage("Registration record deleted.");
            LoadStats();
            BindGrid();
        }
        catch (Exception ex)
        {
            ShowMessage("Error: " + ex.Message);
        }
    }
    
    private void PromoteStudent(int regId)
    {
        // ── POLICY: bulk promotion may no longer create semester registrations ──────
        // This method used to INSERT INTO acad_registration with registeredBy='-', i.e.
        // with no attribution to any person. Run over a multi-select grid
        // (btnPromoteSelected_Click), it manufactured registrations that no student ever
        // asked for. That is how 2026/2027 acquired ~1,430 unrequested registrations,
        // which were then billed.
        //
        // A semester registration must be created ONLY by the student themselves in the
        // eportal wizard, or individually by a named staff member on the Students
        // Registration / Fees Registration screens (which attribute the row to that user).
        // The database enforces this too — see
        // sql/guardrails/acad_registration_require_attribution.sql, which rejects any
        // insert whose registeredBy is blank, '-', or an AUTO/RECON/SYSTEM marker.
        throw new Exception(
            "Bulk promotion can no longer create semester registrations. " +
            "Students must register themselves through the eportal, or a named staff member " +
            "must add the registration individually on the Students Registration screen.");

#pragma warning disable 162 // unreachable: retained for reference / future re-enable
        string newAcadYear = ddlNewAcadYear.SelectedValue;
        int newSemester = int.Parse(ddlNewSemester.SelectedValue);
        bool incrementYear = chkIncrementYear.Checked;
        
        // Enforce current academic year only
        if (!AcademicYearHelper.IsCurrentAcademicYear(newAcadYear))
        {
            throw new Exception(string.Format(
                "Promotion is only allowed into the current academic year ({0}). You selected {1}.",
                AcademicYearHelper.GetCurrentYearDisplay(), newAcadYear));
        }
        
        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            
            // Get current registration details
            string selectSql = @"SELECT regno, studyyear, residence_status FROM acad_registration WHERE ID = @id";
            
            string regno = "";
            int studyYear = 1;
            string residence = "NON RESIDENT";
            
            using (MySqlCommand cmd = new MySqlCommand(selectSql, conn))
            {
                cmd.Parameters.AddWithValue("@id", regId);
                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        regno = reader["regno"].ToString();
                        studyYear = Convert.ToInt32(reader["studyyear"]);
                        residence = reader["residence_status"] != DBNull.Value ? reader["residence_status"].ToString() : "NON RESIDENT";
                    }
                }
            }
            
            if (string.IsNullOrEmpty(regno))
            {
                throw new Exception("Registration record not found.");
            }
            
            // Calculate new study year
            int newStudyYear = incrementYear ? studyYear + 1 : studyYear;
            
            // Check if record already exists for this student in new semester
            string checkSql = @"SELECT COUNT(*) FROM acad_registration 
                               WHERE regno = @regno AND acad_year = @acadYear AND semester = @semester";
            
            using (MySqlCommand cmd = new MySqlCommand(checkSql, conn))
            {
                cmd.Parameters.AddWithValue("@regno", regno);
                cmd.Parameters.AddWithValue("@acadYear", newAcadYear);
                cmd.Parameters.AddWithValue("@semester", newSemester);
                
                int count = Convert.ToInt32(cmd.ExecuteScalar());
                if (count > 0)
                {
                    throw new Exception("Student already has a registration record for the new semester.");
                }
            }
            
            // Insert new registration
            string insertSql = @"INSERT INTO acad_registration 
                                (regno, acad_year, semester, regstatus, studyyear, id_cardStatus, residence_status, 
                                 reg_CardStatus, examClearance, clearedBy, registeredBy)
                                VALUES (@regno, @acadYear, @semester, 'UNREGISTERED', @studyYear, 'UNPRINTED', 
                                        @residence, 'UNPRINTED', 'UNCLEARED', '-', '-')";
            
            using (MySqlCommand cmd = new MySqlCommand(insertSql, conn))
            {
                cmd.Parameters.AddWithValue("@regno", regno);
                cmd.Parameters.AddWithValue("@acadYear", newAcadYear);
                cmd.Parameters.AddWithValue("@semester", newSemester);
                cmd.Parameters.AddWithValue("@studyYear", newStudyYear);
                cmd.Parameters.AddWithValue("@residence", residence);
                
                cmd.ExecuteNonQuery();
            }
        }
#pragma warning restore 162
    }
    
    private void DeleteRegistration(int regId)
    {
        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            string sql = "DELETE FROM acad_registration WHERE ID = @id";
            
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", regId);
                cmd.ExecuteNonQuery();
            }
        }
    }
    
    #endregion
    
    #region Batch Actions
    
    protected void btnPromoteSelected_Click(object sender, EventArgs e)
    {
        try
        {
            List<object> selectedKeys = gvPromotion.GetSelectedFieldValues("ID");
            
            if (selectedKeys.Count == 0)
            {
                ShowMessage("Please select at least one student.");
                return;
            }
            
            int successCount = 0;
            int skipCount = 0;
            List<string> errors = new List<string>();
            
            foreach (object key in selectedKeys)
            {
                int regId = Convert.ToInt32(key);
                
                try
                {
                    PromoteStudent(regId);
                    successCount++;
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("already has a registration"))
                        skipCount++;
                    else
                        errors.Add(ex.Message);
                }
            }
            
            gvPromotion.Selection.UnselectAll();
            
            string message = string.Format("{0} student(s) promoted successfully.", successCount);
            if (skipCount > 0)
                message += string.Format(" {0} skipped (already exist).", skipCount);
            if (errors.Count > 0)
                message += string.Format(" {0} error(s).", errors.Count);
            
            ShowMessage(message);
            LoadStats();
            BindGrid();
        }
        catch (Exception ex)
        {
            ShowMessage("Error: " + ex.Message);
        }
    }
    
    protected void btnBatchDelete_Click(object sender, EventArgs e)
    {
        try
        {
            List<object> selectedKeys = gvPromotion.GetSelectedFieldValues("ID");
            
            if (selectedKeys.Count == 0)
            {
                ShowMessage("Please select at least one student.");
                return;
            }
            
            int successCount = 0;
            
            foreach (object key in selectedKeys)
            {
                int regId = Convert.ToInt32(key);
                
                try
                {
                    DeleteRegistration(regId);
                    successCount++;
                }
                catch { }
            }
            
            gvPromotion.Selection.UnselectAll();
            ShowMessage(string.Format("{0} registration record(s) deleted.", successCount));
            LoadStats();
            BindGrid();
        }
        catch (Exception ex)
        {
            ShowMessage("Error: " + ex.Message);
        }
    }
    
    protected void btnRefresh_Click(object sender, EventArgs e)
    {
        LoadStats();
        BindGrid();
    }
    
    #endregion
    
    private void ShowMessage(string message)
    {
        litMessage.Text = message;
        popMessage.ShowOnPageLoad = true;
    }
}
