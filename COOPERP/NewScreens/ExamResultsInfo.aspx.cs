using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using MySql.Data.MySqlClient;
using DevExpress.XtraPrinting;
using DevExpress.Web;

public partial class COOPERP_NewScreens_ExamResultsInfo : System.Web.UI.Page
{
    private string ConnectionString
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }
    
    private bool IsResultsLocked()
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM acad_results_lock WHERE lock_type = 'RESULTS_DEADLINE' AND is_active = 1 AND CURDATE() > deadline_date", conn))
                {
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
        }
        catch { return false; }
    }
    
    protected void Page_Init(object sender, EventArgs e)
    {
        // On postback, bind real data so DevExpress can restore grid state
        // (pagination, selections, etc.) before event handlers fire
        if (IsPostBack)
        {
            BindGrid();
        }
    }
    
    protected void Page_Load(object sender, EventArgs e)
    {
        if (!IsPostBack)
        {
            LoadCampuses();
            LoadAcademicYears();
            LoadProgrammes();
            LoadEntryYears();
            
            UpdateDisplayLabels();
            LoadCourses();
            LoadStats();
            BindGrid();
        }
        
        CheckUserPermissions();
    }
    
    private void CheckUserPermissions()
    {
        // Enable approve/cancel buttons only for Dean or Administrator
        bool canApprove = RoleAccessService.IsInRoleCompat("Dean") || RoleAccessService.IsInRoleCompat("Administrator");
        btnApprove.Enabled = canApprove;
        btnCancelApproval.Enabled = canApprove;
    }
    
    // Academic year logic centralised in AcademicYearHelper
    
    private void LoadCampuses()
    {
        ddlCampus.Items.Clear();
        
        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            string sql = "SELECT ID, campus_name FROM acad_campuses ORDER BY campus_name";
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ddlCampus.Items.Add(new ListItem(reader["campus_name"].ToString(), reader["ID"].ToString()));
                    }
                }
            }
        }
        
        if (ddlCampus.Items.Count > 0)
            ddlCampus.SelectedIndex = 0;
    }
    
    private void LoadAcademicYears()
    {
        ddlAcadYear.Items.Clear();
        ddlAcadYear.Items.Add(new ListItem("-- All --", ""));
        
        bool hasDataYears = false;
        
        // First try to load academic years that actually have data in the database
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string sql = "SELECT DISTINCT acadyear FROM acad_examresults_faculty WHERE acadyear IS NOT NULL AND acadyear != '' ORDER BY acadyear DESC";
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string yr = reader["acadyear"].ToString();
                            if (!string.IsNullOrEmpty(yr))
                            {
                                ddlAcadYear.Items.Add(new ListItem(yr, yr));
                                hasDataYears = true;
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore and fall through to generated years
        }
        
        // Always add generated years as fallback options
        int currentYear = DateTime.Now.Year;
        for (int i = currentYear + 1; i >= currentYear - 5; i--)
        {
            string acadYear = string.Format("{0}/{1}", i, i + 1);
            // Only add if not already in the list
            if (ddlAcadYear.Items.FindByValue(acadYear) == null)
                ddlAcadYear.Items.Add(new ListItem(acadYear, acadYear));
        }
    }
    
    private void LoadEntryYears()
    {
        ddlEntryYear.Items.Clear();
        ddlEntryYear.Items.Add(new ListItem("-- All --", ""));
        int currentYear = DateTime.Now.Year;
        for (int i = currentYear + 1; i >= currentYear - 10; i--)
        {
            ddlEntryYear.Items.Add(new ListItem(i.ToString(), i.ToString()));
        }
    }
    
    private void LoadProgrammes()
    {
        ddlProgramme.Items.Clear();
        ddlProgramme.Items.Add(new ListItem("-- Select Programme --", ""));
        
        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            string username = HttpContext.Current.User.Identity.Name;
            
            // Get user's assigned programmes using the stored procedure
            using (MySqlCommand cmd = new MySqlCommand("myaspnet_GetMyProgrammes", conn))
            {
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@usr", username);
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
            
            // If no programmes found via permissions, load all
            if (ddlProgramme.Items.Count == 1)
            {
                string sqlAll = "SELECT progcode, progname FROM acad_programme ORDER BY progname";
                using (MySqlCommand cmd = new MySqlCommand(sqlAll, conn))
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
    }
    
    private void LoadCourses()
    {
        ddlCourse.Items.Clear();
        ddlCourse.Items.Add(new ListItem("-- Select Course --", ""));
        
        if (string.IsNullOrEmpty(ddlProgramme.SelectedValue))
            return;
        
        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            
            // Build dynamic query based on which filters are selected
            List<string> conditions = new List<string>();
            conditions.Add("pc.progcode = @prog");
            
            if (!string.IsNullOrEmpty(ddlStudyYear.SelectedValue))
                conditions.Add("pc.study_year = @yr");
            if (!string.IsNullOrEmpty(ddlSemester.SelectedValue))
                conditions.Add("pc.semester = @sem");
            
            string sql = @"SELECT DISTINCT pc.course_code, c.courseName as course_name
                          FROM acad_programmecourses pc
                          INNER JOIN acad_course c ON pc.course_code = c.courseID
                          WHERE " + string.Join(" AND ", conditions) + @"
                          ORDER BY c.courseName";
            
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@prog", ddlProgramme.SelectedValue);
                if (!string.IsNullOrEmpty(ddlStudyYear.SelectedValue))
                    cmd.Parameters.AddWithValue("@yr", int.Parse(ddlStudyYear.SelectedValue));
                if (!string.IsNullOrEmpty(ddlSemester.SelectedValue))
                    cmd.Parameters.AddWithValue("@sem", int.Parse(ddlSemester.SelectedValue));
                
                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string code = reader["course_code"].ToString();
                        string name = reader["course_name"].ToString();
                        ddlCourse.Items.Add(new ListItem(code + " - " + name, code));
                    }
                }
            }
        }
    }
    
    private void LoadMarkRatios()
    {
        pnlRatios.Visible = false;
        
        // Require course, programme, semester, and study year to load ratios
        if (string.IsNullOrEmpty(ddlCourse.SelectedValue) || 
            string.IsNullOrEmpty(ddlProgramme.SelectedValue) ||
            string.IsNullOrEmpty(ddlSemester.SelectedValue) ||
            string.IsNullOrEmpty(ddlStudyYear.SelectedValue))
            return;
        
        // The ratio panel used to be filled from acad_examresults_faculty_settings by a query
        // that could never run (acad_year / prog_id / study_year are not columns on that table).
        // Marks are not scaled by anything, so the panel would only mislead; it states the scale
        // the components are actually entered on. No database round-trip is needed for that.
        litCWRatio.Text = "40";
        litTestRatio.Text = "0";
        litExamRatio.Text = "60";
        pnlRatios.Visible = true;
    }
    
    private void UpdateDisplayLabels()
    {
        litAcadYearDisplay.Text = ddlAcadYear.SelectedValue;
        litSemesterDisplay.Text = ddlSemester.SelectedValue;
    }
    
    private void LoadStats()
    {
        litTotalCount.Text = "0";
        litPendingCount.Text = "0";
        litApprovedCount.Text = "0";
        litPassCount.Text = "0";
        litFailCount.Text = "0";
        
        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            
            // Build dynamic WHERE clause based on selected filters
            List<string> conditions = new List<string>();
            
            // Add filters only if selected (all filters are now optional)
            string acadValue = ddlAcadYear.SelectedValue;
            string semValue = ddlSemester.SelectedValue;
            string progValue = ddlProgramme.SelectedValue;
            string courseValue = ddlCourse.SelectedValue;
            string sessionValue = ddlSession.SelectedValue;
            string statusValue = ddlExamStatus.SelectedValue;
            string studyYearValue = ddlStudyYear.SelectedValue;
            
            if (!string.IsNullOrEmpty(acadValue))
                conditions.Add("acadyear = @acad");
            if (!string.IsNullOrEmpty(semValue))
                conditions.Add("semester = @sem");
            if (!string.IsNullOrEmpty(progValue))
                conditions.Add("progid = @prog");
            if (!string.IsNullOrEmpty(courseValue))
                conditions.Add("course_id = @course");
            if (!string.IsNullOrEmpty(sessionValue))
                conditions.Add("stud_session = @session");
            if (!string.IsNullOrEmpty(statusValue))
                conditions.Add("exam_status = @status");
            if (!string.IsNullOrEmpty(studyYearValue))
                conditions.Add("cyear = @yr");
            
            string baseWhere = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
            
            // Total
            string sqlTotal = "SELECT COUNT(*) FROM acad_examresults_faculty " + baseWhere;
            using (MySqlCommand cmd = new MySqlCommand(sqlTotal, conn))
            {
                AddStatsParameters(cmd);
                litTotalCount.Text = Convert.ToInt32(cmd.ExecuteScalar()).ToString();
            }
            
            // Pending (not approved)
            string pendingWhere = baseWhere + (baseWhere.Length > 0 ? " AND " : "WHERE ") + "(approved_by = '-' OR approved_by IS NULL)";
            using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*) FROM acad_examresults_faculty " + pendingWhere, conn))
            {
                AddStatsParameters(cmd);
                litPendingCount.Text = Convert.ToInt32(cmd.ExecuteScalar()).ToString();
            }
            
            // Approved
            string approvedWhere = baseWhere + (baseWhere.Length > 0 ? " AND " : "WHERE ") + "approved_by != '-' AND approved_by IS NOT NULL";
            using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*) FROM acad_examresults_faculty " + approvedWhere, conn))
            {
                AddStatsParameters(cmd);
                litApprovedCount.Text = Convert.ToInt32(cmd.ExecuteScalar()).ToString();
            }
            
            // Pass (total >= 50)
            string passWhere = baseWhere + (baseWhere.Length > 0 ? " AND " : "WHERE ") + "total_mark >= 50";
            using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*) FROM acad_examresults_faculty " + passWhere, conn))
            {
                AddStatsParameters(cmd);
                litPassCount.Text = Convert.ToInt32(cmd.ExecuteScalar()).ToString();
            }
            
            // Fail (total < 50)
            string failWhere = baseWhere + (baseWhere.Length > 0 ? " AND " : "WHERE ") + "total_mark < 50 AND total_mark > 0";
            using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*) FROM acad_examresults_faculty " + failWhere, conn))
            {
                AddStatsParameters(cmd);
                litFailCount.Text = Convert.ToInt32(cmd.ExecuteScalar()).ToString();
            }
        }
    }
    
    private void AddStatsParameters(MySqlCommand cmd)
    {
        if (!string.IsNullOrEmpty(ddlAcadYear.SelectedValue))
            cmd.Parameters.AddWithValue("@acad", ddlAcadYear.SelectedValue);
        if (!string.IsNullOrEmpty(ddlSemester.SelectedValue))
            cmd.Parameters.AddWithValue("@sem", int.Parse(ddlSemester.SelectedValue));
        if (!string.IsNullOrEmpty(ddlProgramme.SelectedValue))
            cmd.Parameters.AddWithValue("@prog", ddlProgramme.SelectedValue);
        if (!string.IsNullOrEmpty(ddlCourse.SelectedValue))
            cmd.Parameters.AddWithValue("@course", ddlCourse.SelectedValue);
        if (!string.IsNullOrEmpty(ddlSession.SelectedValue))
            cmd.Parameters.AddWithValue("@session", ddlSession.SelectedValue);
        if (!string.IsNullOrEmpty(ddlExamStatus.SelectedValue))
            cmd.Parameters.AddWithValue("@status", ddlExamStatus.SelectedValue);
        if (!string.IsNullOrEmpty(ddlStudyYear.SelectedValue))
            cmd.Parameters.AddWithValue("@yr", int.Parse(ddlStudyYear.SelectedValue));
    }
    
    private DataTable CreateEmptyResultsTable()
    {
        // Create a DataTable with expected columns to prevent binding errors when no data
        DataTable dt = new DataTable();
        dt.Columns.Add("ID", typeof(int));
        dt.Columns.Add("regno", typeof(string));
        dt.Columns.Add("course_id", typeof(string));
        dt.Columns.Add("acadyear", typeof(string));
        dt.Columns.Add("semester", typeof(int));
        dt.Columns.Add("cw_mark_entered", typeof(int));
        dt.Columns.Add("exam_mark_entered", typeof(int));
        dt.Columns.Add("total_mark", typeof(int));
        dt.Columns.Add("grade", typeof(string));
        dt.Columns.Add("gradept", typeof(double));
        dt.Columns.Add("exam_status", typeof(string));
        dt.Columns.Add("approved_by", typeof(string));
        dt.Columns.Add("progid", typeof(string));
        dt.Columns.Add("stud_session", typeof(string));
        dt.Columns.Add("cyear", typeof(int));
        dt.Columns.Add("stud_name", typeof(string));
        dt.Columns.Add("course_name", typeof(string));
        dt.Columns.Add("EntryNo", typeof(int));
        dt.Columns.Add("cw_mark", typeof(int));
        dt.Columns.Add("ex_mark", typeof(int));
        dt.Columns.Add("test_mark_entered", typeof(int));
        dt.Columns.Add("test_mark", typeof(int));
        return dt;
    }
    
    private void BindGrid()
    {
        DataTable dt = CreateEmptyResultsTable();
        
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                
                // Build dynamic WHERE clause based on selected filters
                List<string> conditions = new List<string>();
                
                // Add filters only if selected (all are now optional)
                string acadValue = ddlAcadYear.SelectedValue;
                string semValue = ddlSemester.SelectedValue;
                string progValue = ddlProgramme.SelectedValue;
                string courseValue = ddlCourse.SelectedValue;
                string sessionValue = ddlSession.SelectedValue;
                string statusValue = ddlExamStatus.SelectedValue;
                string studyYearValue = ddlStudyYear.SelectedValue;
                string searchTerm = txtSearch.Text.Trim();
                
                if (!string.IsNullOrEmpty(acadValue))
                    conditions.Add("e.acadyear = @acad");
                if (!string.IsNullOrEmpty(semValue))
                    conditions.Add("e.semester = @sems");
                if (!string.IsNullOrEmpty(progValue))
                    conditions.Add("e.progid = @prog");
                if (!string.IsNullOrEmpty(courseValue))
                    conditions.Add("e.course_id = @csid");
                if (!string.IsNullOrEmpty(sessionValue))
                    conditions.Add("e.stud_session = @sess");
                if (!string.IsNullOrEmpty(statusValue))
                    conditions.Add("e.exam_status = @stat");
                if (!string.IsNullOrEmpty(studyYearValue))
                    conditions.Add("e.cyear = @yr");
                
                // Search by student name or registration number
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    conditions.Add("(e.regno LIKE @search OR s.firstname LIKE @search OR s.othername LIKE @search OR CONCAT(s.firstname, ' ', COALESCE(s.othername, '')) LIKE @search)");
                }
                
                string whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
                
                string sql = @"SELECT 
                    e.ID, e.regno, e.course_id, e.acadyear, e.semester,
                    e.cw_mark_entered, e.exam_mark_entered, e.total_mark,
                    e.grade, e.gradept, e.exam_status, e.approved_by,
                    e.progid, e.stud_session, e.cyear,
                    CONCAT(s.firstname, ' ', COALESCE(s.othername, '')) as stud_name,
                    c.courseName as course_name,
                    e.cw_mark, e.ex_mark, e.test_mark_entered, e.test_mark
                    FROM acad_examresults_faculty e
                    LEFT JOIN acad_student s ON e.regno = s.regno
                    LEFT JOIN acad_course c ON e.course_id = c.courseID
                    " + whereClause + @"
                    ORDER BY s.firstname, s.othername";
                
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    if (!string.IsNullOrEmpty(acadValue))
                        cmd.Parameters.AddWithValue("@acad", acadValue);
                    if (!string.IsNullOrEmpty(semValue))
                        cmd.Parameters.AddWithValue("@sems", int.Parse(semValue));
                    if (!string.IsNullOrEmpty(progValue))
                        cmd.Parameters.AddWithValue("@prog", progValue);
                    if (!string.IsNullOrEmpty(courseValue))
                        cmd.Parameters.AddWithValue("@csid", courseValue);
                    if (!string.IsNullOrEmpty(sessionValue))
                        cmd.Parameters.AddWithValue("@sess", sessionValue);
                    if (!string.IsNullOrEmpty(statusValue))
                        cmd.Parameters.AddWithValue("@stat", statusValue);
                    if (!string.IsNullOrEmpty(studyYearValue))
                        cmd.Parameters.AddWithValue("@yr", int.Parse(studyYearValue));
                    if (!string.IsNullOrEmpty(searchTerm))
                        cmd.Parameters.AddWithValue("@search", "%" + searchTerm + "%");
                    
                    DataTable resultDt = new DataTable();
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        adapter.Fill(resultDt);
                    }
                    
                    // Use result table if it has data or has the required columns
                    if (resultDt.Columns.Contains("course_name"))
                        dt = resultDt;
                    
                    // Show message if no data found
                    if (dt.Rows.Count == 0)
                    {
                        if (!string.IsNullOrEmpty(searchTerm))
                            lblMessage.Text = "No results found for '" + searchTerm + "'. Try a different name or registration number.";
                        else
                            lblMessage.Text = "No exam results found for the selected filters. Records in database: " + GetTotalRecordCount();
                        lblMessage.ForeColor = System.Drawing.Color.Orange;
                    }
                    else
                    {
                        string msg = string.Format("Showing {0} records", dt.Rows.Count);
                        if (!string.IsNullOrEmpty(searchTerm))
                            msg += " for '" + searchTerm + "'";
                        lblMessage.Text = msg;
                        lblMessage.ForeColor = System.Drawing.Color.Green;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Show error message to help debug
            lblMessage.Text = "Error loading data: " + ex.Message;
            lblMessage.ForeColor = System.Drawing.Color.Red;
        }
        
        gvResults.DataSource = dt;
        gvResults.DataBind();
        
        // Only load mark ratios if course is selected
        if (!string.IsNullOrEmpty(ddlCourse.SelectedValue))
            LoadMarkRatios();
    }
    
    private int GetTotalRecordCount()
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*) FROM acad_examresults_faculty", conn))
                {
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }
        catch
        {
            return -1;
        }
    }
    
    protected string GetMarkCellHtml(object mark)
    {
        int totalMark = mark != null && mark != DBNull.Value ? Convert.ToInt32(mark) : 0;
        string cssClass = totalMark >= 50 ? "er-mark-cell--pass" : "er-mark-cell--fail";
        return string.Format("<span class=\"er-mark-cell {0}\">{1}</span>", cssClass, totalMark);
    }
    
    protected string GetExamStatusBadge(object status)
    {
        string statusStr = (status != null) ? status.ToString() : "REGULAR";
        string cssClass = "er-status-badge--regular";
        
        switch (statusStr.ToUpper())
        {
            case "REGULAR":
                cssClass = "er-status-badge--regular";
                break;
            case "RETAKE":
                cssClass = "er-status-badge--retake";
                break;
            case "SUPPLEMENTARY":
                cssClass = "er-status-badge--supplementary";
                break;
            case "SPECIAL":
                cssClass = "er-status-badge--special";
                break;
        }
        
        return string.Format("<span class=\"er-status-badge {0}\">{1}</span>", cssClass, statusStr);
    }
    
    protected string GetApprovalStatusBadge(object approvedBy)
    {
        string approver = (approvedBy != null && approvedBy != DBNull.Value) ? approvedBy.ToString() : "-";
        
        if (approver == "-" || string.IsNullOrEmpty(approver))
        {
            return "<span class=\"er-status-badge er-status-badge--pending\">PENDING</span>";
        }
        else
        {
            return string.Format("<span class=\"er-status-badge er-status-badge--approved\">{0}</span>", approver);
        }
    }
    
    #region Event Handlers
    
    protected void btnSearch_Click(object sender, EventArgs e)
    {
        // Search queries the database - BindGrid reads txtSearch.Text
        LoadStats();
        BindGrid();
    }
    
    protected void btnClearSearch_Click(object sender, EventArgs e)
    {
        txtSearch.Text = "";
        LoadStats();
        BindGrid();
    }
    
    protected void ddlCampus_SelectedIndexChanged(object sender, EventArgs e)
    {
        LoadStats();
        BindGrid();
    }
    
    protected void ddlAcadYear_SelectedIndexChanged(object sender, EventArgs e)
    {
        UpdateDisplayLabels();
        LoadStats();
        BindGrid();
    }
    
    protected void ddlProgramme_SelectedIndexChanged(object sender, EventArgs e)
    {
        LoadCourses();
        LoadStats();
        BindGrid();
    }
    
    protected void ddlStudyYear_SelectedIndexChanged(object sender, EventArgs e)
    {
        LoadCourses();
        LoadStats();
        BindGrid();
    }
    
    protected void ddlSemester_SelectedIndexChanged(object sender, EventArgs e)
    {
        UpdateDisplayLabels();
        LoadCourses();
        LoadStats();
        BindGrid();
    }
    
    protected void ddlEntryYear_SelectedIndexChanged(object sender, EventArgs e)
    {
        LoadStats();
        BindGrid();
    }
    
    protected void ddlIntake_SelectedIndexChanged(object sender, EventArgs e)
    {
        LoadStats();
        BindGrid();
    }
    
    protected void ddlSession_SelectedIndexChanged(object sender, EventArgs e)
    {
        LoadStats();
        BindGrid();
    }
    
    protected void ddlCourse_SelectedIndexChanged(object sender, EventArgs e)
    {
        LoadStats();
        BindGrid();
    }
    
    protected void ddlExamStatus_SelectedIndexChanged(object sender, EventArgs e)
    {
        LoadStats();
        BindGrid();
    }
    
    protected void btnRefresh_Click(object sender, EventArgs e)
    {
        LoadStats();
        BindGrid();
    }
    
    protected void btnApprove_Click(object sender, EventArgs e)
    {
        if (IsResultsLocked())
        {
            ShowMessage("Results are LOCKED. The submission deadline has passed. No changes allowed.", "error");
            return;
        }
        if (!RoleAccessService.IsInRoleCompat("Dean") && !RoleAccessService.IsInRoleCompat("Administrator"))
        {
            ShowMessage("Results approvals can only be done by the Dean.", "error");
            return;
        }
        
        int count = 0;
        string username = HttpContext.Current.User.Identity.Name;
        string batchId = MarksAuditLogger.NewBatchId();
        
        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            
            List<object> selectedIds = gvResults.GetSelectedFieldValues("ID");
            
            foreach (object id in selectedIds)
            {
                int intId = Convert.ToInt32(id);
                
                // Snapshot before change (also gives us approval status + student info)
                DataRow snap = MarksAuditLogger.SnapshotFaculty(conn, intId);
                if (snap == null) continue;
                
                string currentApprover = snap["approved_by"].ToString();
                if (currentApprover != "-" && !string.IsNullOrEmpty(currentApprover))
                    continue;
                
                string updateSql = "UPDATE acad_examresults_faculty SET approved_by = @user WHERE ID = @id";
                using (MySqlCommand updateCmd = new MySqlCommand(updateSql, conn))
                {
                    updateCmd.Parameters.AddWithValue("@user", username);
                    updateCmd.Parameters.AddWithValue("@id", intId);
                    int affected = updateCmd.ExecuteNonQuery();
                    if (affected > 0)
                    {
                        count++;
                        MarksAuditLogger.LogFacultyApproval("APPROVE",
                            intId, snap["regno"].ToString(), snap["course_id"].ToString(),
                            snap["acadyear"].ToString(), Convert.ToInt32(snap["semester"]),
                            snap["progid"].ToString(), currentApprover, username,
                            "ExamResultsInfo.aspx", batchId);
                    }
                }
            }
        }
        
        if (count > 0)
            ShowMessage(count + " result(s) approved successfully.", "success");
        else
            ShowMessage("No results were approved. They may already be approved.", "info");
        
        gvResults.Selection.UnselectAll();
        LoadStats();
        BindGrid();
    }
    
    protected void btnCancelApproval_Click(object sender, EventArgs e)
    {
        if (IsResultsLocked())
        {
            ShowMessage("Results are LOCKED. The submission deadline has passed. No changes allowed.", "error");
            return;
        }
        if (!RoleAccessService.IsInRoleCompat("Dean") && !RoleAccessService.IsInRoleCompat("Administrator"))
        {
            ShowMessage("Only the Dean can cancel approvals.", "error");
            return;
        }
        
        int count = 0;
        string batchId = MarksAuditLogger.NewBatchId();
        
        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            
            List<object> selectedIds = gvResults.GetSelectedFieldValues("ID");
            
            foreach (object id in selectedIds)
            {
                int intId = Convert.ToInt32(id);
                
                // Snapshot before change
                DataRow snap = MarksAuditLogger.SnapshotFaculty(conn, intId);
                string oldApprover = snap != null ? snap["approved_by"].ToString() : "";
                
                string updateSql = "UPDATE acad_examresults_faculty SET approved_by = '-' WHERE ID = @id AND approved_by != '-'";
                using (MySqlCommand updateCmd = new MySqlCommand(updateSql, conn))
                {
                    updateCmd.Parameters.AddWithValue("@id", intId);
                    int affected = updateCmd.ExecuteNonQuery();
                    if (affected > 0 && snap != null)
                    {
                        count++;
                        MarksAuditLogger.LogFacultyApproval("UNAPPROVE",
                            intId, snap["regno"].ToString(), snap["course_id"].ToString(),
                            snap["acadyear"].ToString(), Convert.ToInt32(snap["semester"]),
                            snap["progid"].ToString(), oldApprover, "-",
                            "ExamResultsInfo.aspx", batchId);
                    }
                }
            }
        }
        
        if (count > 0)
            ShowMessage(count + " approval(s) cancelled successfully.", "success");
        else
            ShowMessage("No approvals were cancelled.", "info");
        
        gvResults.Selection.UnselectAll();
        LoadStats();
        BindGrid();
    }
    
    protected void btnPrintSheet_Click(object sender, EventArgs e)
    {
        // Store session variables for report
        Session["prog"] = ddlProgramme.SelectedValue;
        Session["acad"] = ddlAcadYear.SelectedValue;
        Session["sems"] = ddlSemester.SelectedValue;
        Session["csid"] = ddlCourse.SelectedValue;
        Session["sess"] = ddlSession.SelectedValue;
        Session["yr"] = ddlStudyYear.SelectedValue;
        Session["stat"] = ddlExamStatus.SelectedValue;
        Session["intak"] = ddlIntake.SelectedValue;
        Session["entyr"] = ddlEntryYear.SelectedValue;
        Session["camp"] = ddlCampus.SelectedValue;
        Session["Report"] = "Faculty Marksheet";
        
        // Redirect to report viewer
        Response.Redirect("~/COOPERP/XtraReports/Default.aspx");
    }
    
    protected void btnExportExcel_Click(object sender, EventArgs e)
    {
        string fileName = string.Format("ExamResults_{0}_{1}_{2}_Sem{3}", 
            ddlProgramme.SelectedValue, ddlCourse.SelectedValue, 
            ddlAcadYear.SelectedValue.Replace("/", "-"), ddlSemester.SelectedValue);
        gvExporter.WriteXlsxToResponse(fileName, new XlsxExportOptionsEx { ExportType = DevExpress.Export.ExportType.WYSIWYG });
    }
    
    protected void gvResults_RowDeleting(object sender, DevExpress.Web.Data.ASPxDataDeletingEventArgs e)
    {
        if (IsResultsLocked())
        {
            ShowMessage("Results are LOCKED. The submission deadline has passed. No deletions allowed.", "error");
            e.Cancel = true;
            return;
        }
        int id = Convert.ToInt32(e.Keys["ID"]);
        int affected = DeleteResultById(id);
        
        if (affected > 0)
            ShowMessage("Result deleted successfully.", "success");
        else if (affected == 0)
            ShowMessage("Record not found or already deleted.", "warning");
        // affected == -1 means it was approved and blocked
        
        e.Cancel = true;
        LoadStats();
        BindGrid();
    }
    
    /// <summary>
    /// Deletes a single result by ID. Returns rows affected, 0 if not found, -1 if blocked (approved).
    /// </summary>
    private int DeleteResultById(int id, string batchId = null)
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                
                // Snapshot BEFORE delete — captures everything about to be destroyed
                DataRow snap = MarksAuditLogger.SnapshotFaculty(conn, id);
                
                if (snap == null)
                    return 0;
                
                string approvedBy = snap["approved_by"].ToString();
                string regno = snap["regno"].ToString();
                string courseId = snap["course_id"].ToString();
                
                if (approvedBy != "-" && !string.IsNullOrEmpty(approvedBy))
                {
                    ShowMessage("Cannot delete approved result for " + regno + " (" + courseId + "). Cancel approval first.", "error");
                    return -1;
                }
                
                // Delete the record
                string deleteSql = "DELETE FROM acad_examresults_faculty WHERE ID = @id";
                using (MySqlCommand deleteCmd = new MySqlCommand(deleteSql, conn))
                {
                    deleteCmd.Parameters.AddWithValue("@id", id);
                    int affected = deleteCmd.ExecuteNonQuery();
                    
                    if (affected > 0)
                        MarksAuditLogger.LogFacultyDelete(snap, "ExamResultsInfo.aspx", null, batchId);
                    
                    return affected;
                }
            }
        }
        catch (Exception ex)
        {
            ShowMessage("Error deleting result: " + ex.Message, "error");
            return 0;
        }
    }
    
    protected void btnDeleteSelected_Click(object sender, EventArgs e)
    {
        if (IsResultsLocked())
        {
            ShowMessage("Results are LOCKED. The submission deadline has passed. No deletions allowed.", "error");
            return;
        }
        int count = 0;
        int blocked = 0;
        string batchId = MarksAuditLogger.NewBatchId();
        
        List<object> selectedIds = gvResults.GetSelectedFieldValues("ID");
        
        if (selectedIds.Count == 0)
        {
            ShowMessage("No records selected. Use the checkboxes to select records to delete.", "warning");
            return;
        }
        
        foreach (object id in selectedIds)
        {
            int result = DeleteResultById(Convert.ToInt32(id), batchId);
            if (result > 0) count++;
            else if (result == -1) blocked++;
        }
        
        string msg = "";
        if (count > 0) msg += count + " result(s) deleted. ";
        if (blocked > 0) msg += blocked + " approved result(s) skipped (cancel approval first).";
        if (count == 0 && blocked == 0) msg = "No records were deleted.";
        
        ShowMessage(msg, count > 0 ? "success" : "warning");
        
        gvResults.Selection.UnselectAll();
        LoadStats();
        BindGrid();
    }
    
    protected void gvResults_BatchUpdate(object sender, DevExpress.Web.Data.ASPxDataBatchUpdateEventArgs e)
    {
        if (IsResultsLocked())
        {
            ShowMessage("Results are LOCKED. The submission deadline has passed. No changes allowed.", "error");
            return;
        }
        string batchId = MarksAuditLogger.NewBatchId();
        
        // Handle batch updates (mark edits)
        foreach (var args in e.UpdateValues)
        {
            UpdateResultRow(args, batchId);
        }
        
        // Handle batch deletes (the delete button in Batch mode only fires here, not RowDeleting)
        int deleteCount = 0;
        foreach (var args in e.DeleteValues)
        {
            int id = Convert.ToInt32(args.Keys["ID"]);
            deleteCount += DeleteResultById(id, batchId);
        }
        
        if (deleteCount > 0)
            ShowMessage(deleteCount + " result(s) deleted successfully.", "success");
        
        LoadStats();
        BindGrid();
    }
    
    protected void gvResults_RowUpdating(object sender, DevExpress.Web.Data.ASPxDataUpdatingEventArgs e)
    {
        if (IsResultsLocked())
        {
            e.Cancel = true;
            throw new Exception("Results are LOCKED. The submission deadline has passed. No changes allowed.");
        }
        // Check if approved - don't allow editing
        object approvedByVal = e.OldValues["approved_by"];
        string approvedBy = (approvedByVal != null && approvedByVal != DBNull.Value) ? approvedByVal.ToString() : "-";
        if (approvedBy != "-" && !string.IsNullOrEmpty(approvedBy))
        {
            e.Cancel = true;
            throw new Exception("Approved results cannot be edited. Student: " + e.OldValues["regno"]);
        }
        
        // Calculate marks based on ratios
        int cwEntered = Convert.ToInt32(e.NewValues["cw_mark_entered"] ?? 0);
        int examEntered = Convert.ToInt32(e.NewValues["exam_mark_entered"] ?? 0);
        
        // MRU enters marks already on their final scale: coursework out of 40, exam out of 60,
        // and the total is their plain sum. 36,660 rows in acad_examresults_faculty have
        // cw_mark = cw_mark_entered against 4 that are scaled.
        //
        // A ratio lookup used to sit here. It filtered on acad_year, prog_id and study_year while
        // the table has acadyear, progid and cyear, so it threw ERROR 1054 on every edit - and
        // with no try/catch around it, saving a mark on this screen simply failed. It is removed
        // rather than repaired: its fallback was 30/70 (wrong for MRU), and of the settings rows
        // for current years 43 read 0/100 and 18 read 0/0, so a repaired lookup would zero the
        // coursework, or everything, for those courses.
        int cwMark = cwEntered;
        int exMark = examEntered;
        int totalMark = cwMark + exMark;

        // Components are entered on their own scales, so check them on their own scales. The four
        // rows in 2025/2026 carrying invented splits (cw 30 / exam 44, from a 74 typed into BOTH
        // boxes) are exactly what this refuses.
        if (cwEntered > 40)
        {
            e.Cancel = true;
            throw new Exception("Coursework is out of 40 - " + cwEntered + " was entered for student: " + e.OldValues["regno"]);
        }
        if (examEntered > 60)
        {
            e.Cancel = true;
            throw new Exception("The exam mark is out of 60 - " + examEntered + " was entered for student: " + e.OldValues["regno"]);
        }
        if (totalMark > 100)
        {
            e.Cancel = true;
            throw new Exception("Total mark exceeds 100 for student: " + e.OldValues["regno"]);
        }
        
        // Recalculate grade and grade point from total mark
        string grade = CalculateGrade(totalMark);
        double gradePt = CalculateGradePoint(grade);
        
        // Update the record directly
        int id = Convert.ToInt32(e.Keys["ID"]);
        
        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            
            // Snapshot before edit
            DataRow snap = MarksAuditLogger.SnapshotFaculty(conn, id);
            
            string updateSql = @"UPDATE acad_examresults_faculty 
                                SET cw_mark_entered = @cw_entered, cw_mark = @cw_mark,
                                    exam_mark_entered = @exam_entered, ex_mark = @ex_mark,
                                    total_mark = @total, grade = @grade, gradept = @gradept
                                WHERE ID = @id";
            
            using (MySqlCommand cmd = new MySqlCommand(updateSql, conn))
            {
                cmd.Parameters.AddWithValue("@cw_entered", cwEntered);
                cmd.Parameters.AddWithValue("@cw_mark", cwMark);
                cmd.Parameters.AddWithValue("@exam_entered", examEntered);
                cmd.Parameters.AddWithValue("@ex_mark", exMark);
                cmd.Parameters.AddWithValue("@total", totalMark);
                cmd.Parameters.AddWithValue("@grade", grade);
                cmd.Parameters.AddWithValue("@gradept", gradePt);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            
            // Audit log: old→new
            MarksAuditLogger.LogFacultyUpdate(snap,
                cwEntered, 0, examEntered,
                cwMark, 0, exMark,
                totalMark, grade, "ExamResultsInfo.aspx");
        }
        
        e.Cancel = true; // Cancel the default update since we handled it manually
        BindGrid();
    }
    
    private void UpdateResultRow(DevExpress.Web.Data.ASPxDataUpdateValues args, string batchId = null)
    {
        int id = Convert.ToInt32(args.Keys["ID"]);
        int cwEntered = Convert.ToInt32(args.NewValues["cw_mark_entered"] ?? 0);
        int examEntered = Convert.ToInt32(args.NewValues["exam_mark_entered"] ?? 0);
        
        // Not scaled - see gvResults_RowUpdating. Components are entered on their final
        // scales and the total is their plain sum.
        
        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            
            // Snapshot before edit
            DataRow snap = MarksAuditLogger.SnapshotFaculty(conn, id);
            
            int cwMark = cwEntered;
            int exMark = examEntered;
            int totalMark = cwMark + exMark;

            if (cwEntered > 40)
                throw new Exception("Coursework is out of 40 - " + cwEntered + " was entered (row " + id + ").");
            if (examEntered > 60)
                throw new Exception("The exam mark is out of 60 - " + examEntered + " was entered (row " + id + ").");
            if (totalMark > 100)
                throw new Exception("Total mark exceeds 100 (row " + id + ").");
            
            // Recalculate grade and grade point from total mark
            string grade = CalculateGrade(totalMark);
            double gradePt = CalculateGradePoint(grade);
            
            string updateSql = @"UPDATE acad_examresults_faculty 
                                SET cw_mark_entered = @cw_entered, cw_mark = @cw_mark,
                                    exam_mark_entered = @exam_entered, ex_mark = @ex_mark,
                                    total_mark = @total, grade = @grade, gradept = @gradept
                                WHERE ID = @id";
            
            using (MySqlCommand cmd = new MySqlCommand(updateSql, conn))
            {
                cmd.Parameters.AddWithValue("@cw_entered", cwEntered);
                cmd.Parameters.AddWithValue("@cw_mark", cwMark);
                cmd.Parameters.AddWithValue("@exam_entered", examEntered);
                cmd.Parameters.AddWithValue("@ex_mark", exMark);
                cmd.Parameters.AddWithValue("@total", totalMark);
                cmd.Parameters.AddWithValue("@grade", grade);
                cmd.Parameters.AddWithValue("@gradept", gradePt);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            
            // Audit log: old→new
            MarksAuditLogger.LogFacultyUpdate(snap,
                cwEntered, 0, examEntered,
                cwMark, 0, exMark,
                totalMark, grade, "ExamResultsInfo.aspx",
                null, batchId);
        }
    }
    
    #endregion
    
    #region Grade Calculation Helpers
    
    /// <summary>
    /// Calculate letter grade from total mark (0-100 scale).
    /// Uses the same grading scale as ResultsUpdates.aspx.cs.
    /// </summary>
    private string CalculateGrade(int totalMark)
    {
        // MRU / NCHE scale: 80 A · 75 B+ · 70 B · 65 C+ · 60 C · 55 D+ · 50 D · <50 F.
        if (totalMark >= 80) return "A";
        if (totalMark >= 75) return "B+";
        if (totalMark >= 70) return "B";
        if (totalMark >= 65) return "C+";
        if (totalMark >= 60) return "C";
        if (totalMark >= 55) return "D+";
        if (totalMark >= 50) return "D";
        return "F";
    }
    
    /// <summary>
    /// Convert letter grade to grade point on 5.0 scale.
    /// Uses the same mapping as ResultsUpdates.aspx.cs.
    /// </summary>
    private double CalculateGradePoint(string grade)
    {
        switch (grade)
        {
            case "A":  return 5.0;
            case "B+": return 4.5;
            case "B":  return 4.0;
            case "C+": return 3.5;
            case "C":  return 3.0;
            case "D+": return 2.5;
            case "D":  return 2.0;
            default:   return 0.0;   // F
        }
    }
    
    #endregion
    
    private void ShowMessage(string message, string type)
    {
        pnlMessage.Visible = true;
        pnlMessage.CssClass = "er-message show er-message--" + type;
        litMessage.Text = message;
    }
}
