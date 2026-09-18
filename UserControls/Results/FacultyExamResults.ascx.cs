using DevExpress.XtraPrinting;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using MySql.Data.MySqlClient;
using ResultsDataTableAdapters;
using SecurityTableAdapters;

public partial class UserControls_Results_FacultyExamResults : System.Web.UI.UserControl
{
    acad_activity_logTableAdapter sec_log = new acad_activity_logTableAdapter();
    acad_examresults_faculty_settingsTableAdapter setting = new acad_examresults_faculty_settingsTableAdapter();

    private bool IsResultsLocked()
    {
        try
        {
            string connStr = ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString;
            using (MySqlConnection conn = new MySqlConnection(connStr))
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

    protected void Page_Load(object sender, EventArgs e)
    {
        if (!IsPostBack)
        {
            txtAcadYear.DataSource = SettingsFile.ReturnAcademicYrs();
            txtAcadYear.DataBind();
            txtAcadYear.Text = SettingsFile.ReturnDefaultAcademicYr();
            pop_messagebox.HeaderText = SettingsFile.AppName;
            txt_entry_year.DataSource = SettingsFile.ReturnYears();
            txt_entry_year.DataBind();


           // txtCourse.Value = "ACC";
            txtCampus.Value = 1;
            txt_entry_year.Text = DateTime.Now.Year.ToString();
        }
        if (HttpContext.Current.User.IsInRole("Dean") || HttpContext.Current.User.IsInRole("Administrator"))
        {
            cmdApprove.Enabled = true;
            cmdCancelApprove.Enabled = true;
        }

        //if (string.IsNullOrEmpty(txtCourse.Value.ToString()))
           
       // if (string.IsNullOrEmpty(txtCampus.Value.ToString()))
            
        //if (string.IsNullOrEmpty(txt_entry_year.Text))


      

    }
    protected void cmdApprove_Click(object sender, EventArgs e)
    {
        if (IsResultsLocked())
        {
            lbl_msg.Text = "Results are LOCKED. The submission deadline has passed. No approvals allowed.";
            pop_msgBox.ShowOnPageLoad = true;
            return;
        }
        String comm = "No Pending Results Selected";
        int noRows = gvMarksheet.VisibleRowCount, counter = 0;
        ResultsDataTableAdapters.acad_examresults_facultyTableAdapter EXM = new ResultsDataTableAdapters.acad_examresults_facultyTableAdapter();
        string batchId = MarksAuditLogger.NewBatchId();

        if (HttpContext.Current.User.IsInRole("Dean"))
        {
            for (int i = 0; i < noRows; i++)
            {
                if (gvMarksheet.Selection.IsRowSelected(i) && gvMarksheet.GetRowValues(i, "approved_by").ToString() == "-")
                {
                    try
                    {
                        int rowId = int.Parse(gvMarksheet.GetRowValues(i, "ID").ToString());
                        string rowRegno = gvMarksheet.GetRowValues(i, "regno").ToString();

                        EXM.acad_CaptureFacultyResults(HttpContext.Current.User.Identity.Name.ToString(),
                        rowRegno, txtCourse.Value.ToString(), txtAcadYear.Text, txtSemester.Text,
                        int.Parse(gvMarksheet.GetRowValues(i, "total_mark").ToString()), int.Parse(txtStudyYear.Text),
                        rowId, txtExamStatus.Text);
                        counter++;
                        comm = counter + " Result(s) Approved";

                        // Structured audit log
                        MarksAuditLogger.LogFacultyApproval("APPROVE", rowId,
                            rowRegno, txtCourse.Value.ToString(), txtAcadYear.Text,
                            int.Parse(txtSemester.Text), txtProg.Value.ToString(),
                            "-", HttpContext.Current.User.Identity.Name,
                            "FacultyExamResults.ascx", batchId);
                    }
                    catch (Exception ex)
                    {
                        comm = "Error!" + ex.Message;

                    }
                }

            }
        }
        else
        {
            comm = "Results Approvals can only be done by the Dean.";
        }
        gvMarksheet.DataBind();
        lbl_msg.Text = comm;
        pop_msgBox.ShowOnPageLoad = true;

    }
    protected void cmdDetails_Click(object sender, ImageClickEventArgs e)
    {
        

        pop_details.ContentUrl = "~/COOPERP/Results/MarkSheetDetails.aspx";
        pop_details.ShowOnPageLoad = true;
    }
    protected void cmdExportExcel_Click(object sender, EventArgs e)
    {
        GVE_Marksheets.WriteXlsToResponse(string.Format("{2} MarksheetList {0}_{1}", txtAcadYear.Text, txtSemester.Text, txtExamStatus.Text), 
            new XlsExportOptions { ExportMode = XlsExportMode.SingleFile });
    }
    protected void cmdStatusChange_Click(object sender, EventArgs e)
    {
        ResultsDataTableAdapters.acad_examsettingsTableAdapter EX = new ResultsDataTableAdapters.acad_examsettingsTableAdapter();
        lbl_comment.Text = "Status Updated";
    }

    void StatusUpdater()
    {
       

    }
    
    protected void gvMarksheet_RowUpdating(object sender, DevExpress.Web.Data.ASPxDataUpdatingEventArgs e)
    {
        if (IsResultsLocked())
        {
            e.Cancel = true;
            throw new Exception("Results are LOCKED. The submission deadline has passed. No changes allowed.");
        }
        if (e.OldValues["approved_by"].ToString() != "-")
        {
            e.NewValues["cw_mark_entered"] = e.OldValues["cw_mark_entered"];
            e.NewValues["test_mark_entered"] = e.OldValues["test_mark_entered"];
            // The grid binds this column as exam_mark_entered (see the .ascx); the old key
            // did not exist in the values dictionary.
            e.NewValues["exam_mark_entered"] = e.OldValues["exam_mark_entered"];
            throw new Exception("Approved Results Can not be Edited. \nCheck Student: [" + e.OldValues["regno"] + "]");
        }
        else
        {
            string courseworkratio = setting.GetCourseWorkRatio(txtCourse.Value.ToString(), txtAcadYear.Text, int.Parse(txtSemester.Text), txtProg.Value.ToString(), txtSession.Text, int.Parse(txtStudyYear.Text),
                            txtintake.Text, int.Parse(txt_entry_year.Text), int.Parse(txtCampus.Value.ToString())).ToString();
            string testratio = setting.GetTestRatio(txtCourse.Value.ToString(), txtAcadYear.Text, int.Parse(txtSemester.Text), txtProg.Value.ToString(), txtSession.Text, int.Parse(txtStudyYear.Text),
                                 txtintake.Text, int.Parse(txt_entry_year.Text), int.Parse(txtCampus.Value.ToString())).ToString();
            string examratio = setting.GetExamRatio(txtCourse.Value.ToString(), txtAcadYear.Text, int.Parse(txtSemester.Text), txtProg.Value.ToString(), txtSession.Text, int.Parse(txtStudyYear.Text),
                                 txtintake.Text, int.Parse(txt_entry_year.Text), int.Parse(txtCampus.Value.ToString())).ToString();
            if (!string.IsNullOrEmpty(courseworkratio))
                e.NewValues["cw_mark"] = Math.Round(decimal.Parse(e.NewValues["cw_mark_entered"].ToString()) * decimal.Parse(courseworkratio) / 100, 0);
            else
                throw new Exception("Mark Ratios have not been Set.");

            if (!string.IsNullOrEmpty(testratio))
                e.NewValues["test_mark"] = Math.Round(decimal.Parse(e.NewValues["test_mark_entered"].ToString()) * decimal.Parse(testratio) / 100, 0);

            if (!string.IsNullOrEmpty(examratio))
                e.NewValues["ex_mark"] = Math.Round(decimal.Parse(e.NewValues["exam_mark_entered"].ToString()) * decimal.Parse(examratio) / 100,0);
            else
                throw new Exception("Mark Ratios have not been Set.");

            e.NewValues["total_mark"] = int.Parse(e.NewValues["cw_mark"].ToString()) + int.Parse(e.NewValues["test_mark"].ToString()) + int.Parse(e.NewValues["ex_mark"].ToString());

            if(int.Parse(e.NewValues["total_mark"].ToString())>100)
                throw new Exception("Total Mark is incorrect for student: [ " + e.OldValues["regno"] +" ]");

            /*if (int.Parse(e.NewValues["total_mark"].ToString()) >= 45 && int.Parse(e.NewValues["total_mark"].ToString()) <= 50)
                e.NewValues["total_mark"] = 50;*/

        }
    }
    protected void cmdCancelApprove_Click(object sender, EventArgs e)
    {
        if (IsResultsLocked())
        {
            lbl_msg.Text = "Results are LOCKED. The submission deadline has passed. No changes allowed.";
            pop_msgBox.ShowOnPageLoad = true;
            return;
        }
        int noRows = gvMarksheet.VisibleRowCount,counter=0;
        ResultsDataTableAdapters.acad_examresults_facultyTableAdapter EXM = new ResultsDataTableAdapters.acad_examresults_facultyTableAdapter();
        string batchId = MarksAuditLogger.NewBatchId();
        
        for (int i = 0; i < noRows; i++)
        {
            if (gvMarksheet.Selection.IsRowSelected(i) && gvMarksheet.GetRowValues(i, "approved_by").ToString()!="-")
            {
                //if (gvMarksheet.GetRowValues(i, "approved_by").ToString() != HttpContext.Current.User.Identity.Name.ToString() && gvMarksheet.GetRowValues(i, "approved_by").ToString()!="-")
                //{
                //    lbl_msg.Text = "Cancelation Denied. You can not cancel another user's Approvals.";
                //    break;
                //}
                //else 
                if (!HttpContext.Current.User.IsInRole("Dean"))
                {
                    lbl_msg.Text = "Cancelation Denied. Only the Dean can cancel approvals.";
                    break;
                }
                else
                {
                    int rowId = int.Parse(gvMarksheet.GetRowValues(i, "ID").ToString());
                    string oldApprover = gvMarksheet.GetRowValues(i, "approved_by").ToString();
                    string rowRegno = gvMarksheet.GetRowValues(i, "regno").ToString();
                    
                    counter++;
                    EXM.UpdateApprovalStatus(HttpContext.Current.User.Identity.Name,"-", rowId);
                    lbl_msg.Text = counter + " Results Approvals Cancelled";
                    
                    // Structured audit log
                    MarksAuditLogger.LogFacultyApproval("UNAPPROVE", rowId,
                        rowRegno, txtCourse.Value.ToString(), txtAcadYear.Text,
                        int.Parse(txtSemester.Text), txtProg.Value.ToString(),
                        oldApprover, "-",
                        "FacultyExamResults.ascx", batchId);
                }
            }
        }
        gvMarksheet.DataBind();
        pop_msgBox.ShowOnPageLoad = true;
    }



    protected void cmdPrintSheet_Click(object sender, EventArgs e)
    {
        pop_details.Width = 1000;
        pop_details.Height = 600;
        pop_details.ContentUrl = "~/COOPERP/XtraReports/Default.aspx";
        Session["prog"] = txtProg.Value;
        Session["acad"] = txtAcadYear.Text;
        Session["sems"] = txtSemester.Text;
        Session["Report"] = "Faculty Marksheet";

        Session["csid"]=txtCourse.Value;
        Session["sess"]=txtSession.Text;
        Session["yr"]=txtStudyYear.Text;
         Session["stat"]=txtExamStatus.Text;
         Session["intak"] = txtintake.Text;
         Session["entyr"] = txt_entry_year.Text;
         Session["campus"] = txtCampus.Value;
        pop_details.ShowOnPageLoad = true;

    }



    protected void cmdCreateSheet_Click(object sender, EventArgs e)
    {
        if (IsResultsLocked())
        {
            lbl_msg.Text = "Results are LOCKED. The submission deadline has passed. Sheet creation is not allowed.";
            pop_msgBox.ShowOnPageLoad = true;
            return;
        }
        try
        {
            ResultsDataTableAdapters.acad_examresults_facultyTableAdapter EXM = new ResultsDataTableAdapters.acad_examresults_facultyTableAdapter();
            EXM.acad_CreateFacultyExamSheet(txtProg.Value.ToString(), int.Parse(txtSemester.Text), int.Parse(txtStudyYear.Text), txtSession.Text, txtAcadYear.Text,
                txtCourse.Value.ToString(), txtExamStatus.Text,txtintake.Text,txt_entry_year.Text,txtCampus.Value.ToString(),txt_courseworkratio.Text,txt_examratio.Text,txt_testratio.Text);
           
            gvMarksheet.DataBind();

            if(gvMarksheet.VisibleRowCount == 0)
                lbl_msg.Text = "Resultsheet Empty. No Registered Students.";
            else
                lbl_msg.Text = "Resultsheet Refresh Completed";

            pop_messagebox.ShowOnPageLoad = false;
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Object reference not set to an instance of an object."))
                lbl_msg.Text = "Resultsheet Refresh Error! Try Again [ Make Sure all fields are Selected ]";
            else
                if (ex.Message.Contains("Exception has been thrown by the target of an invocation."))
                    lbl_msg.Text = "Resultsheet Refresh Error! Try Again [" + ex.InnerException.Message + "]";
                else

                   lbl_msg.Text = "Resultsheet Refresh Error! Try Again [" + ex.Message + "]";
        }
        pop_msgBox.ShowOnPageLoad = true;
    }
    
    protected void cmddisplayratios_Click(object sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(txtCourse.Value.ToString()))
                txtCourse.Value = "-";
            string courseworkratio = setting.GetCourseWorkRatio(txtCourse.Value.ToString(), txtAcadYear.Text, int.Parse(txtSemester.Text), txtProg.Value.ToString(), txtSession.Text, int.Parse(txtStudyYear.Text),
                                txtintake.Text, int.Parse(txt_entry_year.Text), int.Parse(txtCampus.Value.ToString())).ToString();

            string testratio = setting.GetTestRatio(txtCourse.Value.ToString(), txtAcadYear.Text, int.Parse(txtSemester.Text), txtProg.Value.ToString(), txtSession.Text, int.Parse(txtStudyYear.Text),
                                txtintake.Text, int.Parse(txt_entry_year.Text), int.Parse(txtCampus.Value.ToString())).ToString();

            string examratio = setting.GetExamRatio(txtCourse.Value.ToString(), txtAcadYear.Text, int.Parse(txtSemester.Text), txtProg.Value.ToString(), txtSession.Text, int.Parse(txtStudyYear.Text),
                                 txtintake.Text, int.Parse(txt_entry_year.Text), int.Parse(txtCampus.Value.ToString())).ToString();
            if (!string.IsNullOrEmpty(courseworkratio))
                txt_courseworkratio.Text = courseworkratio;
            else
                txt_courseworkratio.Text = "0";

            if (!string.IsNullOrEmpty(testratio))
                txt_testratio.Text = testratio;
            else
                txt_testratio.Text = "0";

            if (!string.IsNullOrEmpty(examratio))
                txt_examratio.Text = examratio;
            else
                txt_examratio.Text = "0";
        }
        catch (Exception ex)
        {
            lbl_msg.Text = "Error: Make sure the Course is Selected. " + ex.Message;  
        }
    }
    
    protected void gvMarksheet_CustomErrorText(object sender, DevExpress.Web.ASPxGridViewCustomErrorTextEventArgs e)
    {
        if (e.ErrorText.Contains("Exception has been thrown by the target of an invocation."))
            e.ErrorText = e.Exception.InnerException.Message;
    }
    protected void cmdPrintBlankCourseWorkSheet_Click(object sender, EventArgs e)
    {
        pop_details.Width = 1000;
        pop_details.Height = 600;
        pop_details.ContentUrl = "~/COOPERP/XtraReports/Default.aspx";
        //Session["prog"] = txtProg.Value;
        Session["acad"] = txtAcadYear.Text;
        Session["sems"] = txtSemester.Text;
        Session["Report"] = "Blank CourseWork Marksheet";

        //Session["csid"] = txtCourse.Value;
        //Session["sess"] = txtSession.Text;
        Session["yr"] = txtStudyYear.Text;
        //Session["stat"] = txtExamStatus.Text;
       Session["intak"] = txtintake.Text;
       Session["entyr"] = txt_entry_year.Text;
        Session["campus"] = txtCampus.Value;
        pop_details.ShowOnPageLoad = true;
    }
    protected void cmdPrintBlankExamSheet_Click(object sender, EventArgs e)
    {
        pop_details.Width = 1000;
        pop_details.Height = 600;
        pop_details.ContentUrl = "~/COOPERP/XtraReports/Default.aspx";
        //Session["prog"] = txtProg.Value;
        Session["acad"] = txtAcadYear.Text;
        Session["sems"] = txtSemester.Text;
        Session["Report"] = "Blank Exam Marksheet";

       // Session["csid"] = txtCourse.Value;
        //Session["sess"] = txtSession.Text;
        Session["yr"] = txtStudyYear.Text;
        //Session["stat"] = txtExamStatus.Text;
        Session["intak"] = txtintake.Text;
        Session["entyr"] = txt_entry_year.Text;
        Session["campus"] = txtCampus.Value;
        pop_details.ShowOnPageLoad = true;
    }
    protected void cmdPrintInternshipSheet_Click(object sender, EventArgs e)
    {
        pop_details.Width = 1000;
        pop_details.Height = 600;
        pop_details.ContentUrl = "~/COOPERP/XtraReports/Default.aspx";
        //Session["prog"] = txtProg.Value;
        Session["acad"] = txtAcadYear.Text;
        Session["sems"] = txtSemester.Text;
        Session["Report"] = "Blank Internship Marksheet";

        //Session["csid"] = txtCourse.Value;
        //Session["sess"] = txtSession.Text;
        Session["yr"] = txtStudyYear.Text;
        //Session["stat"] = txtExamStatus.Text;
        Session["intak"] = txtintake.Text;
        Session["entyr"] = txt_entry_year.Text;
        Session["campus"] = txtCampus.Value;
        pop_details.ShowOnPageLoad = true;
    }
    protected void cmdPrintBlankResearchSheet_Click(object sender, EventArgs e)
    {
        pop_details.Width = 1000;
        pop_details.Height = 600;
        pop_details.ContentUrl = "~/COOPERP/XtraReports/Default.aspx";
        //Session["prog"] = txtProg.Value;
        Session["acad"] = txtAcadYear.Text;
        Session["sems"] = txtSemester.Text;
        Session["Report"] = "Blank Research Marksheet";

        //Session["csid"] = txtCourse.Value;
        //Session["sess"] = txtSession.Text;
        Session["yr"] = txtStudyYear.Text;
        //Session["stat"] = txtExamStatus.Text;
        Session["intak"] = txtintake.Text;
        Session["entyr"] = txt_entry_year.Text;
        Session["campus"] = txtCampus.Value;
        pop_details.ShowOnPageLoad = true;
    }
    protected void txtProg_SelectedIndexChanged1(object sender, EventArgs e)
    {
        txtCourse.DataBind();
    }
    protected void gvMarksheet_RowDeleting(object sender, DevExpress.Web.Data.ASPxDataDeletingEventArgs e)
    {
        if (e.Values["approved_by"].ToString() != "-")
            throw new Exception("Results Already Approved. Deletion Denied");
        
        // Snapshot before DevExpress performs the delete
        try
        {
            int rowId = Convert.ToInt32(e.Keys["ID"]);
            using (var conn = new MySqlConnection(ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString))
            {
                conn.Open();
                DataRow snap = MarksAuditLogger.SnapshotFaculty(conn, rowId);
                MarksAuditLogger.LogFacultyDelete(snap, "FacultyExamResults.ascx");
            }
        }
        catch { /* audit must never block deletion */ }
    }

    protected void gvMarksheet_RowUpdated(object sender, DevExpress.Web.Data.ASPxDataUpdatedEventArgs e)
    {
        try
        {
            string ipaddress = GetClientIPAddress();

            // Safe value retrieval with conditional operator
            string student = e.OldValues["regno"] != null ? e.OldValues["regno"].ToString() : "Unknown";

            string Course = txtCourse.Value != null ? txtCourse.Value.ToString() : "Unknown";
            string AcademicYear = !string.IsNullOrEmpty(txtAcadYear.Text) ? txtAcadYear.Text : "Unknown";
            string Semester = !string.IsNullOrEmpty(txtSemester.Text) ? txtSemester.Text : "Unknown";

            string OldCourseWork = e.OldValues["cw_mark_entered"] != null ? e.OldValues["cw_mark_entered"].ToString() : "";
            string NewCourseWork = e.NewValues["cw_mark_entered"] != null ? e.NewValues["cw_mark_entered"].ToString() : "";
            string OldTestMark = e.OldValues["test_mark_entered"] != null ? e.OldValues["test_mark_entered"].ToString() : "";
            string NewTestMark = e.NewValues["test_mark_entered"] != null ? e.NewValues["test_mark_entered"].ToString() : "";
            string OldExamMark = e.OldValues["exam_mark_entered"] != null ? e.OldValues["exam_mark_entered"].ToString() : "";
            string NewExamMark = e.NewValues["exam_mark_entered"] != null ? e.NewValues["exam_mark_entered"].ToString() : "";

            // Existing generic activity log (keep as-is)
            sec_log.Insert(
                HttpContext.Current.User.Identity.Name,
                "Faculty Exam Results Editor",
                "Student: " + student + " Course: " + Course +
                " Academic Year: " + AcademicYear + " Semester: " + Semester + " Old CourseWork Mark: " + OldCourseWork +
                " New CourseWork: " + NewCourseWork + " Old Test Mark: " + OldTestMark + " New Test Mark: " + NewTestMark +
                " Old Exam Mark: " + OldExamMark + " New Exam Mark: " + NewExamMark + " IP Address: " + ipaddress,
                "Changed Student Marks",
                DateTime.Now
            );
            
            // Structured audit log (new — captures before/after with structured fields)
            try
            {
                int rowId = 0;
                try { rowId = Convert.ToInt32(gvMarksheet.GetRowValues(gvMarksheet.EditingRowVisibleIndex, "ID")); } catch { }
                
                int? oldCw = SafeToInt(e.OldValues["cw_mark"]);
                int? newCw = SafeToInt(e.NewValues["cw_mark"]);
                int? oldTest = SafeToInt(e.OldValues["test_mark"]);
                int? newTest = SafeToInt(e.NewValues["test_mark"]);
                int? oldExam = SafeToInt(e.OldValues["ex_mark"]);
                int? newExam = SafeToInt(e.NewValues["ex_mark"]);
                int? oldTotal = SafeToInt(e.OldValues["total_mark"]);
                int? newTotal = SafeToInt(e.NewValues["total_mark"]);
                string oldGrade = e.OldValues["grade"] != null ? e.OldValues["grade"].ToString() : null;
                
                string summary = "CW:" + OldCourseWork + "→" + NewCourseWork +
                                 ", Test:" + OldTestMark + "→" + NewTestMark +
                                 ", Exam:" + OldExamMark + "→" + NewExamMark;
                
                int? sem = null;
                try { sem = int.Parse(Semester); } catch { }
                
                MarksAuditLogger.Log("UPDATE", "acad_examresults_faculty", rowId,
                    student, Course, AcademicYear, sem,
                    txtProg.Value != null ? txtProg.Value.ToString() : null,
                    "ALL", summary, null,
                    oldCw, newCw, oldTest, newTest, oldExam, newExam, oldTotal, newTotal,
                    oldGrade, null, null, null,
                    null, "FacultyExamResults.ascx");
            }
            catch { /* structured audit must never break existing logging */ }
        }
        catch (Exception ex)
        {
            // Log the error if something still goes wrong
            sec_log.Insert(
                "SYSTEM",
                "Error",
                "Error in gvMarksheet_RowUpdated: " + ex.Message,
                "Error",
                DateTime.Now
            );
        }
    }
    
    /// <summary>
    /// Safe nullable int conversion for audit logging.
    /// </summary>
    private static int? SafeToInt(object val)
    {
        if (val == null || val == DBNull.Value) return null;
        int result;
        return int.TryParse(val.ToString(), out result) ? (int?)result : null;
    }

    protected string GetClientIPAddress()
    {
        // Look for a proxy address first
        string ip = HttpContext.Current.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];

        // If no proxy, get the standard remote address
        if (string.IsNullOrEmpty(ip))
        {
            ip = HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"];
        }
        else
        {
            // Extract first IP if multiple addresses are listed
            ip = ip.Split(',')[0].Trim();
        }

        // Handle localhost case
        if (ip == "::1")
        {
            ip = "127.0.0.1";
        }

        return ip;
    }
}