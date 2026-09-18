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

public partial class COOPERP_NewScreens_AcademicResults : System.Web.UI.Page
{
    private string ConnectionString
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }
    
    protected void Page_Init(object sender, EventArgs e)
    {
        // On postback, bind real data so DevExpress can restore grid state
        // (pagination, selections) before event handlers fire
        if (IsPostBack)
        {
            BindGrid();
        }
    }
    
    protected void Page_Load(object sender, EventArgs e)
    {
        if (!IsPostBack)
        {
            LoadProgrammes();
            LoadAcademicYears();
            LoadCourses();
            LoadStats();
            BindGrid();
        }
    }
    
    #region Data Loading
    
    private void LoadProgrammes()
    {
        ddlProgramme.Items.Clear();
        ddlProgramme.Items.Add(new ListItem("-- All Programmes --", ""));
        
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string username = HttpContext.Current.User.Identity.Name;
                
                // Try to get user's assigned programmes
                try
                {
                    using (MySqlCommand cmd = new MySqlCommand("myaspnet_GetMyProgrammes", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
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
                }
                catch { /* stored procedure may not exist - fall through to load all */ }
                
                // If no programmes found via permissions, load all
                if (ddlProgramme.Items.Count == 1)
                {
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
        }
        catch { /* Don't let programme loading failure prevent page from loading */ }
    }
    
    private void LoadAcademicYears()
    {
        ddlAcadYear.Items.Clear();
        ddlAcadYear.Items.Add(new ListItem("-- All --", ""));
        
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                // Source years from the small canonical years table (26 rows) instead of a
                // DISTINCT scan of acad_results (627k rows) on every page load (~2s -> instant).
                string sql = "SELECT acadyear AS acad FROM acad_acadyears WHERE IFNULL(acadyear,'') <> '' ORDER BY acadyear DESC";
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string yr = reader["acad"].ToString();
                            if (!string.IsNullOrEmpty(yr))
                                ddlAcadYear.Items.Add(new ListItem(yr, yr));
                        }
                    }
                }
            }
        }
        catch { }
        
        // Add generated years as fallback
        int currentYear = DateTime.Now.Year;
        for (int i = currentYear + 1; i >= currentYear - 5; i--)
        {
            string acadYear = string.Format("{0}/{1}", i, i + 1);
            if (ddlAcadYear.Items.FindByValue(acadYear) == null)
                ddlAcadYear.Items.Add(new ListItem(acadYear, acadYear));
        }
    }
    
    private void LoadCourses()
    {
        ddlCourse.Items.Clear();
        ddlCourse.Items.Add(new ListItem("-- All Courses --", ""));
        
        if (string.IsNullOrEmpty(ddlProgramme.SelectedValue))
            return;
        
        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            
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
    
    private void LoadStats()
    {
        litTotalCount.Text = "0";
        litStudentCount.Text = "0";
        litPassCount.Text = "0";
        litFailCount.Text = "0";
        litRetakeCount.Text = "0";
        litAvgScore.Text = "0.0";
        
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                
                string baseWhere = BuildWhereClause("r");
                
                // Total records
                using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*) FROM acad_results r LEFT JOIN acad_student s ON r.regno = s.regno " + baseWhere, conn))
                {
                    AddFilterParameters(cmd);
                    litTotalCount.Text = Convert.ToInt32(cmd.ExecuteScalar()).ToString();
                }
                
                // Distinct students
                using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(DISTINCT r.regno) FROM acad_results r LEFT JOIN acad_student s ON r.regno = s.regno " + baseWhere, conn))
                {
                    AddFilterParameters(cmd);
                    litStudentCount.Text = Convert.ToInt32(cmd.ExecuteScalar()).ToString();
                }
                
                // Pass (score >= 50)
                string passWhere = baseWhere + (baseWhere.Length > 0 ? " AND " : "WHERE ") + "r.score >= 50";
                using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*) FROM acad_results r LEFT JOIN acad_student s ON r.regno = s.regno " + passWhere, conn))
                {
                    AddFilterParameters(cmd);
                    litPassCount.Text = Convert.ToInt32(cmd.ExecuteScalar()).ToString();
                }
                
                // Fail (score < 50 AND score > 0)
                string failWhere = baseWhere + (baseWhere.Length > 0 ? " AND " : "WHERE ") + "r.score < 50 AND r.score > 0";
                using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*) FROM acad_results r LEFT JOIN acad_student s ON r.regno = s.regno " + failWhere, conn))
                {
                    AddFilterParameters(cmd);
                    litFailCount.Text = Convert.ToInt32(cmd.ExecuteScalar()).ToString();
                }
                
                // Retake comment
                string retakeWhere = baseWhere + (baseWhere.Length > 0 ? " AND " : "WHERE ") + "r.result_comment = 'RT'";
                using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*) FROM acad_results r LEFT JOIN acad_student s ON r.regno = s.regno " + retakeWhere, conn))
                {
                    AddFilterParameters(cmd);
                    litRetakeCount.Text = Convert.ToInt32(cmd.ExecuteScalar()).ToString();
                }
                
                // Avg score
                string avgWhere = baseWhere + (baseWhere.Length > 0 ? " AND " : "WHERE ") + "r.score > 0";
                using (MySqlCommand cmd = new MySqlCommand("SELECT ROUND(AVG(r.score), 1) FROM acad_results r LEFT JOIN acad_student s ON r.regno = s.regno " + avgWhere, conn))
                {
                    AddFilterParameters(cmd);
                    object result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                        litAvgScore.Text = result.ToString();
                }
            }
        }
        catch { }
    }
    
    #endregion
    
    #region Grid Binding
    
    private DataTable CreateEmptyResultsTable()
    {
        DataTable dt = new DataTable();
        dt.Columns.Add("ID", typeof(int));
        dt.Columns.Add("regno", typeof(string));
        dt.Columns.Add("stud_name", typeof(string));
        dt.Columns.Add("courseid", typeof(string));
        dt.Columns.Add("coursename", typeof(string));
        dt.Columns.Add("acad", typeof(string));
        dt.Columns.Add("studyyear", typeof(int));
        dt.Columns.Add("semester", typeof(int));
        dt.Columns.Add("CreditUnits", typeof(int));
        dt.Columns.Add("score", typeof(int));
        dt.Columns.Add("grade", typeof(string));
        dt.Columns.Add("gradept", typeof(double));
        dt.Columns.Add("gpa", typeof(double));
        dt.Columns.Add("result_comment", typeof(string));
        dt.Columns.Add("progcode", typeof(string));
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
                
                string whereClause = BuildWhereClause("r");
                
                string sql = @"SELECT 
                    r.ID, r.regno, r.courseid,
                    COALESCE(c.coursename, r.courseid) AS coursename,
                    r.acad, r.studyyear, r.semester,
                    r.CreditUnits, r.score, r.grade, r.gradept, r.gpa,
                    r.result_comment,
                    CONCAT(COALESCE(s.firstname, ''), ' ', COALESCE(s.othername, '')) as stud_name,
                    COALESCE(s.progid, '') as progcode
                    FROM acad_results r
                    LEFT JOIN acad_student s ON r.regno = s.regno
                    LEFT JOIN acad_course c ON r.courseid = c.courseID
                    " + whereClause + @"
                    ORDER BY r.ID DESC
                    LIMIT 5000";
                
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.CommandTimeout = 30;
                    AddFilterParameters(cmd);
                    
                    DataTable resultDt = new DataTable();
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        adapter.Fill(resultDt);
                    }
                    
                    if (resultDt.Columns.Contains("coursename"))
                        dt = resultDt;
                    
                    string searchTerm = txtSearch.Text.Trim();
                    if (dt.Rows.Count == 0)
                    {
                        if (!string.IsNullOrEmpty(searchTerm))
                            lblMessage.Text = "No results found for '" + searchTerm + "'.";
                        else
                            lblMessage.Text = "No academic results found for the selected filters.";
                        lblMessage.ForeColor = System.Drawing.Color.Orange;
                    }
                    else
                    {
                        string msg = string.Format("Showing {0} records", dt.Rows.Count);
                        if (!string.IsNullOrEmpty(searchTerm))
                            msg += " for '" + searchTerm + "'";
                        if (dt.Rows.Count >= 5000)
                            msg += " (limit reached - use filters to narrow down)";
                        lblMessage.Text = msg;
                        lblMessage.ForeColor = System.Drawing.Color.Green;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            lblMessage.Text = "Error loading data: " + ex.Message;
            lblMessage.ForeColor = System.Drawing.Color.Red;
        }
        
        gvResults.DataSource = dt;
        gvResults.DataBind();
    }
    
    private string BuildWhereClause(string alias)
    {
        List<string> conditions = new List<string>();
        
        string searchTerm = txtSearch.Text.Trim();
        string progValue = ddlProgramme.SelectedValue;
        string acadValue = ddlAcadYear.SelectedValue;
        string yearValue = ddlStudyYear.SelectedValue;
        string semValue = ddlSemester.SelectedValue;
        string courseValue = ddlCourse.SelectedValue;
        string gradeValue = ddlGrade.SelectedValue;
        string commentValue = ddlComment.SelectedValue;
        
        if (!string.IsNullOrEmpty(searchTerm))
            conditions.Add("(" + alias + ".regno LIKE @search OR s.firstname LIKE @search OR s.othername LIKE @search OR CONCAT(s.firstname, ' ', COALESCE(s.othername, '')) LIKE @search)");
        if (!string.IsNullOrEmpty(progValue))
            conditions.Add("s.progid = @prog");
        if (!string.IsNullOrEmpty(acadValue))
            conditions.Add(alias + ".acad = @acad");
        if (!string.IsNullOrEmpty(yearValue))
            conditions.Add(alias + ".studyyear = @yr");
        if (!string.IsNullOrEmpty(semValue))
            conditions.Add(alias + ".semester = @sem");
        if (!string.IsNullOrEmpty(courseValue))
            conditions.Add(alias + ".courseid = @course");
        if (!string.IsNullOrEmpty(gradeValue))
            conditions.Add(alias + ".grade = @grade");
        if (!string.IsNullOrEmpty(commentValue))
            conditions.Add(alias + ".result_comment = @comment");
        
        return conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
    }
    
    private void AddFilterParameters(MySqlCommand cmd)
    {
        string searchTerm = txtSearch.Text.Trim();
        
        if (!string.IsNullOrEmpty(searchTerm))
            cmd.Parameters.AddWithValue("@search", "%" + searchTerm + "%");
        if (!string.IsNullOrEmpty(ddlProgramme.SelectedValue))
            cmd.Parameters.AddWithValue("@prog", ddlProgramme.SelectedValue);
        if (!string.IsNullOrEmpty(ddlAcadYear.SelectedValue))
            cmd.Parameters.AddWithValue("@acad", ddlAcadYear.SelectedValue);
        if (!string.IsNullOrEmpty(ddlStudyYear.SelectedValue))
            cmd.Parameters.AddWithValue("@yr", int.Parse(ddlStudyYear.SelectedValue));
        if (!string.IsNullOrEmpty(ddlSemester.SelectedValue))
            cmd.Parameters.AddWithValue("@sem", int.Parse(ddlSemester.SelectedValue));
        if (!string.IsNullOrEmpty(ddlCourse.SelectedValue))
            cmd.Parameters.AddWithValue("@course", ddlCourse.SelectedValue);
        if (!string.IsNullOrEmpty(ddlGrade.SelectedValue))
            cmd.Parameters.AddWithValue("@grade", ddlGrade.SelectedValue);
        if (!string.IsNullOrEmpty(ddlComment.SelectedValue))
            cmd.Parameters.AddWithValue("@comment", ddlComment.SelectedValue);
    }
    
    #endregion
    
    #region Display Helpers
    
    protected string GetGradeBadge(object gradeObj)
    {
        string grade = (gradeObj != null && gradeObj != DBNull.Value) ? gradeObj.ToString() : "";
        if (string.IsNullOrEmpty(grade)) return "";
        
        string cssClass = "ar-grade--e";
        string g = grade.ToUpper().TrimEnd('+', '-');
        
        switch (g)
        {
            case "A": cssClass = "ar-grade--a"; break;
            case "B": cssClass = "ar-grade--b"; break;
            case "C": cssClass = "ar-grade--c"; break;
            case "D": cssClass = "ar-grade--d"; break;
            default: cssClass = "ar-grade--e"; break;
        }
        
        return string.Format("<span class=\"ar-grade {0}\">{1}</span>", cssClass, grade);
    }
    
    #endregion
    
    #region Grade Calculation
    
    private string CalculateGrade(int score)
    {
        // MRU / NCHE scale: 80 A · 75 B+ · 70 B · 65 C+ · 60 C · 55 D+ · 50 D · <50 F.
        if (score >= 80) return "A";
        if (score >= 75) return "B+";
        if (score >= 70) return "B";
        if (score >= 65) return "C+";
        if (score >= 60) return "C";
        if (score >= 55) return "D+";
        if (score >= 50) return "D";
        return "F";
    }

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
    
    private string CalculateResultComment(int score)
    {
        if (score >= 50) return "NP";  // Normal Pass
        if (score >= 45) return "PP";  // Probationary Pass
        return "RT";                    // Retake
    }
    
    #endregion
    
    #region Event Handlers
    
    protected void btnSearch_Click(object sender, EventArgs e)
    {
        LoadStats();
        BindGrid();
    }
    
    protected void btnClearSearch_Click(object sender, EventArgs e)
    {
        txtSearch.Text = "";
        LoadStats();
        BindGrid();
    }
    
    protected void ddlFilter_Changed(object sender, EventArgs e)
    {
        // Reload courses if programme/year/semester changed
        DropDownList ddl = (DropDownList)sender;
        if (ddl.ID == "ddlProgramme" || ddl.ID == "ddlStudyYear" || ddl.ID == "ddlSemester")
        {
            LoadCourses();
        }
        
        LoadStats();
        BindGrid();
    }
    
    protected void btnRefresh_Click(object sender, EventArgs e)
    {
        LoadStats();
        BindGrid();
    }
    
    protected void btnExportExcel_Click(object sender, EventArgs e)
    {
        string fileName = string.Format("AcademicResults_{0}_{1}",
            ddlProgramme.SelectedValue, ddlAcadYear.SelectedValue.Replace("/", "-"));
        gvExporter.WriteXlsxToResponse(fileName, new XlsxExportOptionsEx { ExportType = DevExpress.Export.ExportType.WYSIWYG });
    }
    
    protected void gvResults_RowUpdating(object sender, DevExpress.Web.Data.ASPxDataUpdatingEventArgs e)
    {
        int id = Convert.ToInt32(e.Keys["ID"]);
        int score = Convert.ToInt32(e.NewValues["score"] ?? e.OldValues["score"] ?? 0);
        int creditUnits = Convert.ToInt32(e.NewValues["CreditUnits"] ?? e.OldValues["CreditUnits"] ?? 3);
        
        // Recalculate grade, grade point, and comment from score
        string grade = CalculateGrade(score);
        double gradePt = CalculateGradePoint(grade);
        string comment = CalculateResultComment(score);
        
        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            string sql = @"UPDATE acad_results 
                          SET score = @score, grade = @grade, gradept = @gradept, 
                              CreditUnits = @cu, result_comment = @comment
                          WHERE ID = @id";
            
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@score", score);
                cmd.Parameters.AddWithValue("@grade", grade);
                cmd.Parameters.AddWithValue("@gradept", gradePt);
                cmd.Parameters.AddWithValue("@cu", creditUnits);
                cmd.Parameters.AddWithValue("@comment", comment);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }
        
        e.Cancel = true;
        BindGrid();
    }
    
    protected void gvResults_BatchUpdate(object sender, DevExpress.Web.Data.ASPxDataBatchUpdateEventArgs e)
    {
        // Handle batch updates
        foreach (var args in e.UpdateValues)
        {
            int id = Convert.ToInt32(args.Keys["ID"]);
            int score = Convert.ToInt32(args.NewValues["score"] ?? 0);
            int creditUnits = Convert.ToInt32(args.NewValues["CreditUnits"] ?? 3);
            
            string grade = CalculateGrade(score);
            double gradePt = CalculateGradePoint(grade);
            string comment = CalculateResultComment(score);
            
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string sql = @"UPDATE acad_results 
                              SET score = @score, grade = @grade, gradept = @gradept,
                                  CreditUnits = @cu, result_comment = @comment
                              WHERE ID = @id";
                
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@score", score);
                    cmd.Parameters.AddWithValue("@grade", grade);
                    cmd.Parameters.AddWithValue("@gradept", gradePt);
                    cmd.Parameters.AddWithValue("@cu", creditUnits);
                    cmd.Parameters.AddWithValue("@comment", comment);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }
        
        // Handle batch deletes
        int deleteCount = 0;
        foreach (var args in e.DeleteValues)
        {
            int id = Convert.ToInt32(args.Keys["ID"]);
            deleteCount += DeleteResultById(id);
        }
        
        if (deleteCount > 0)
            ShowMessage(deleteCount + " result(s) deleted.", "success");
        
        LoadStats();
        BindGrid();
    }
    
    protected void gvResults_RowDeleting(object sender, DevExpress.Web.Data.ASPxDataDeletingEventArgs e)
    {
        int id = Convert.ToInt32(e.Keys["ID"]);
        int affected = DeleteResultById(id);
        
        if (affected > 0)
            ShowMessage("Result deleted successfully.", "success");
        else
            ShowMessage("Record not found or already deleted.", "warning");
        
        e.Cancel = true;
        LoadStats();
        BindGrid();
    }
    
    private int DeleteResultById(int id)
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                // Name the person before the row goes. The audit trigger on acad_results reads
                // mark_audit_context; without this the deletion of a student's published mark
                // is recorded as "system", which is the same as not recording it.
                MarkAuditContext.Set(conn, null, "AcademicResults:delete-result",
                    "Published result deleted from the Academic Results screen");

                string sql = "DELETE FROM acad_results WHERE ID = @id";
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    return cmd.ExecuteNonQuery();
                }
            }
        }
        catch (Exception ex)
        {
            ShowMessage("Error deleting: " + ex.Message, "error");
            return 0;
        }
    }
    
    protected void btnDeleteSelected_Click(object sender, EventArgs e)
    {
        List<object> selectedIds = gvResults.GetSelectedFieldValues("ID");
        
        if (selectedIds.Count == 0)
        {
            ShowMessage("No records selected. Use the checkboxes to select records to delete.", "warning");
            return;
        }
        
        int count = 0;
        foreach (object id in selectedIds)
        {
            count += DeleteResultById(Convert.ToInt32(id));
        }
        
        if (count > 0)
            ShowMessage(count + " result(s) deleted successfully.", "success");
        else
            ShowMessage("No records were deleted.", "warning");
        
        gvResults.Selection.UnselectAll();
        LoadStats();
        BindGrid();
    }
    
    #endregion
    
    private void ShowMessage(string message, string type)
    {
        pnlMessage.Visible = true;
        pnlMessage.CssClass = "ar-message show ar-message--" + type;
        litMessage.Text = message;
    }
}
