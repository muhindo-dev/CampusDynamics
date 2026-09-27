using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using MySql.Data.MySqlClient;
using System.Configuration;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.IO;
using System.Text;
using System.Linq;
using System.Security.Cryptography;
using System.Web;

public partial class COOPERP_NewScreens_NewStudentInfo : System.Web.UI.Page
{
    private string ConnectionString = ConfigurationManager.ConnectionStrings["vacConnectionString"] != null
        ? ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString
        : "Server=localhost;Database=campus_dynamics;Uid=root;Pwd=24thdecember1977;";

    // Shared specIdToUse between Curriculum and Validation tabs
    private int _sharedSpecIdToUse = 0;
    
    // Flag to ensure DB columns are created only once per app start
    private static bool _dbColumnsChecked = false;
    private string _setPasswordLastDebug = "";

    // Status filter - can be set by query parameter or dropdown
    protected string StatusFilter
    {
        get
        {
            // First check ViewState (for postbacks)
            if (ViewState["StatusFilter"] != null)
                return ViewState["StatusFilter"].ToString();
            // Then check query string
            if (!string.IsNullOrEmpty(Request.QueryString["status"]))
                return Request.QueryString["status"].ToUpper();
            return "";
        }
        set { ViewState["StatusFilter"] = value; }
    }

    // Page title based on status
    protected string PageTitle
    {
        get
        {
            switch (StatusFilter)
            {
                case "ACTIVE": return "Active Students";
                case "ADMITTED": return "Admitted Students";
                case "ALUMNI": return "Alumni Students";
                case "ALL": return "All Students";
                default: return "Student Records";
            }
        }
    }

    protected void Page_Init(object sender, EventArgs e)
    {
        // CRITICAL: ViewState is disabled at the form level (SidebarMaster: <form EnableViewState="false">).
        // With ViewState off, the ASPxGridView is empty during LoadPostData (which runs BEFORE Page_Load),
        // so it cannot capture the edit-form editor values a user typed — binding it in Page_Load is too
        // late and makes inline edits (e.g. a name change) silently fail to persist. DevExpress requires
        // the grid to be bound in Page_Init in this scenario. Filters are read from the query string
        // (the page is GET-state driven) since the filter controls aren't populated this early.
        // AJAX/JSON/PDF endpoints (?action=...) handle their own response in Page_Load — skip binding.
        if (string.IsNullOrEmpty(Request.QueryString["action"]))
        {
            EnsureValidationColumnsExist(); // guarded (once/app) — must run before the grid query uses these columns
            BindStudentsGrid();
        }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        // Ensure validation columns exist in database (once per app start)
        EnsureValidationColumnsExist();
        
        // Handle AJAX requests for batch operations
        string action = Request.QueryString["action"];
        if (!string.IsNullOrEmpty(action))
        {
            // ── Authentication gate ──────────────────────────────────────────
            // These endpoints were reachable ANONYMOUSLY. Whatever sends an
            // unauthenticated visitor to the login page runs after this point, so every
            // ?action= handler executed first and WriteJsonAndComplete ended the response
            // before the redirect could happen. A plain POST from anywhere on the internet
            // could therefore reset a student's password, change batch statuses, replace a
            // photograph or generate documents.
            //
            // Verified against production before fixing: an anonymous POST to
            // ?action=ResetPasswordToDefault returned {"success":true} and genuinely
            // rewrote the password hash and salt of a real account. That account was
            // restored from a backup taken beforehand.
            //
            // The gate covers EVERY action rather than only the new one, because they all
            // shared the same hole.
            if (!IsCallerAuthenticated())
            {
                // 200 with success:false, NOT 401. FormsAuthenticationModule rewrites a 401 into a
                // 302 to the login page, so the browser receives an HTML login form where the
                // caller expected JSON — the screen then reports "could not reach the server"
                // instead of "your session expired". The status code buys nothing here because the
                // request is already refused; what matters is that the caller can read why.
                Response.Clear();
                Response.ContentType = "application/json";
                Response.Write("{\"success\":false,\"message\":\"Your session has expired. Please sign in again, then retry.\"}");
                try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
                return;
            }

            bool handledAction = false;

            if (action == "PreviewBatchStatus")
            {
                HandlePreviewBatchStatus();
                handledAction = true;
            }
            else if (action == "ApplyBatchStatus")
            {
                HandleApplyBatchStatus();
                handledAction = true;
            }
            else if (action == "PreviewBatchValidation")
            {
                HandlePreviewBatchValidation();
                handledAction = true;
            }
            else if (action == "ApplyBatchValidation")
            {
                HandleApplyBatchValidation();
                handledAction = true;
            }
            else if (action == "PreviewSummaryReport")
            {
                HandlePreviewSummaryReport();
                handledAction = true;
            }
            else if (action == "SummaryReportCascade")
            {
                HandleSummaryReportCascade();
                handledAction = true;
            }
            else if (action == "ExportSummaryReport")
            {
                HandleExportSummaryReport();
                handledAction = true;
            }
            else if (action == "ExportPerformanceReport")
            {
                HandleExportPerformanceReport();
                handledAction = true;
            }
            else if (action == "SetPassword")
            {
                HandleSetPassword();
                handledAction = true;
            }
            else if (action == "ResetPasswordToDefault")
            {
                HandleResetPasswordToDefault();
                handledAction = true;
            }
            else if (action == "SetPhoto")
            {
                HandleSetPhoto();
                handledAction = true;
            }
            else if (action == "GenerateAcademicDocument")
            {
                HandleGenerateAcademicDocument();
                handledAction = true;
            }
            else if (action == "ListStudents")
            {
                HandleListStudents();
                handledAction = true;
            }
            else if (action == "HealthSetPassword")
            {
                HandleHealthSetPassword();
                handledAction = true;
            }
            else if (action == "CheckStudentForSetPassword")
            {
                HandleCheckStudentForSetPassword();
                handledAction = true;
            }
            else if (action == "ChangeProgInit")   { HandleChangeProgInit();   handledAction = true; }
            else if (action == "SpecList")          { HandleSpecList();          handledAction = true; }
            else if (action == "PreviewProgRegno") { HandlePreviewProgRegno();  handledAction = true; }
            else if (action == "ChangeProgramme")   { HandleChangeProgramme();   handledAction = true; }
            else if (action == "ChangeEntryYear")   { HandleChangeEntryYear();   handledAction = true; }
            else if (action == "QuickEditLoad")      { HandleQuickEditLoad();      handledAction = true; }
            else if (action == "PreviewEntrynoForSession") { HandlePreviewEntrynoForSession(); handledAction = true; }
            else if (action == "DiagnoseTranscript")  { HandleDiagnoseTranscript();  handledAction = true; }
            else if (action == "FixTranscript")       { HandleFixTranscript();       handledAction = true; }
            else if (action == "QuickEditSave")      { HandleQuickEditSave();      handledAction = true; }
            else if (action == "ExportStudentsList"){ HandleExportStudentsList();handledAction = true; }

            // For action endpoints (JSON/PDF/etc), stop normal page rendering lifecycle.
            // Returning from Page_Load alone is not enough in WebForms; the page can still render.
            if (handledAction)
            {
                Response.SuppressContent = true;
                HttpContext.Current.ApplicationInstance.CompleteRequest();
                return;
            }
        }
        
        // Always reload programmes - ViewState is disabled so dropdowns lose items on postback
        LoadBatchProgrammes();

        // ViewState is disabled, so the filter bar (dropdown items + current selections) must be
        // rebuilt on EVERY request — otherwise a save postback (inline edit) would leave the filter
        // dropdowns empty and unselected. These are query-string driven and idempotent.
        LoadFilters();
        ApplyFiltersFromQueryString();

        // Set default photo URL for JavaScript use (hidden field also loses its value with ViewState off)
        hdnDefaultPhotoUrl.Value = ResolveUrl("~/COOPERP/StudentInfo/photos/default.png");

        // Update page header based on status
        if (litPageTitle != null)
            litPageTitle.Text = PageTitle;
        // NOTE: the students grid is bound in Page_Init (required because ViewState is disabled —
        // see the comment there). Do NOT bind it here in Page_Load or inline edits stop persisting.
    }

    protected string BuildViewProfileOnclick(object regnoObj)
    {
        string regno = HttpUtility.JavaScriptStringEncode(Convert.ToString(regnoObj) ?? string.Empty);
        return string.Format("openStudentProfile(\"{0}\"); closeAllActionPopovers(); return false;", regno);
    }

    protected string BuildEditOnclick(object keyValueObj)
    {
        string key = HttpUtility.JavaScriptStringEncode(Convert.ToString(keyValueObj) ?? string.Empty);
        return string.Format("gridEditRow(\"gvStudents\", \"{0}\"); return false;", key);
    }

    protected string BuildSetPasswordOnclick(object regnoObj, object firstNameObj, object otherNameObj)
    {
        string regno = HttpUtility.JavaScriptStringEncode(Convert.ToString(regnoObj) ?? string.Empty);
        string studentName = ((Convert.ToString(firstNameObj) ?? string.Empty).Trim() + " " + (Convert.ToString(otherNameObj) ?? string.Empty).Trim()).Trim();
        studentName = HttpUtility.JavaScriptStringEncode(studentName);
        return string.Format("openSetPasswordModal(\"{0}\", \"{1}\"); closeAllActionPopovers(); return false;", regno, studentName);
    }
    
    /// <summary>
    /// Ensures the validation-related columns exist in acad_student table.
    /// Columns: has_passed, is_curriculum_fully_set, fail_reason
    /// </summary>
    private void EnsureValidationColumnsExist()
    {
        if (_dbColumnsChecked) return;
        _dbColumnsChecked = true;
        
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                
                // Check and add columns if they don't exist
                // completion_date: admin-entered academic completion date; overrides the transcript's
                // auto-computed completion date (see AcademicDocumentPdfService + TranscriptPrint).
                string[] columns = new string[] { "has_passed", "is_curriculum_fully_set", "fail_reason", "completion_date" };
                string[] columnDefs = new string[] {
                    "VARCHAR(5) DEFAULT 'No'",
                    "VARCHAR(5) DEFAULT 'No'",
                    "TEXT DEFAULT NULL",
                    "DATE NULL"
                };
                
                for (int i = 0; i < columns.Length; i++)
                {
                    string checkSql = @"SELECT COUNT(*) FROM information_schema.COLUMNS 
                                        WHERE TABLE_SCHEMA = DATABASE() 
                                        AND TABLE_NAME = 'acad_student' 
                                        AND COLUMN_NAME = @colName";
                    
                    using (MySqlCommand cmd = new MySqlCommand(checkSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@colName", columns[i]);
                        int exists = Convert.ToInt32(cmd.ExecuteScalar());
                        
                        if (exists == 0)
                        {
                            string alterSql = "ALTER TABLE acad_student ADD COLUMN " + columns[i] + " " + columnDefs[i];
                            using (MySqlCommand alterCmd = new MySqlCommand(alterSql, conn))
                            {
                                alterCmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Error ensuring validation columns: " + ex.Message);
        }
    }
    
    #region Batch Operations
    
    private void LoadBatchProgrammes()
    {
        try
        {
            ddlBatchProgramme.Items.Clear();
            ddlBatchProgramme.Items.Add(new ListItem("-- Select Programme --", ""));
            
            ddlValidationProgramme.Items.Clear();
            ddlValidationProgramme.Items.Add(new ListItem("-- All Programmes --", ""));
            
            ddlReportProgramme.Items.Clear();
            ddlReportProgramme.Items.Add(new ListItem("-- Select Programme --", ""));
            
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT progcode, progname FROM acad_programme ORDER BY progname", conn))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string progName = reader["progname"].ToString();
                            string progCode = reader["progcode"].ToString();
                            
                            ddlBatchProgramme.Items.Add(new ListItem(progName, progCode));
                            ddlValidationProgramme.Items.Add(new ListItem(progName, progCode));
                            // Include code in display for report dropdown: "CODE - Programme Name"
                            ddlReportProgramme.Items.Add(new ListItem(progCode + " - " + progName, progCode));
                        }
                    }
                }
            }
        }
        catch { }
    }
    
    private void HandlePreviewBatchStatus()
    {
        Response.ContentType = "application/json";
        
        try
        {
            // Read JSON body
            string json;
            using (StreamReader reader = new StreamReader(Request.InputStream))
            {
                json = reader.ReadToEnd();
            }
            
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> data = serializer.Deserialize<Dictionary<string, object>>(json);
            
            string conditionType = data["conditionType"].ToString();
            string targetStatus = data["targetStatus"].ToString();
            bool negate = data.ContainsKey("negate") && Convert.ToBoolean(data["negate"]);
            
            int count = GetBatchAffectedCount(conditionType, negate, data);
            
            Response.Write(serializer.Serialize(new { count = count }));
        }
        catch (Exception ex)
        {
            Response.Write("{\"count\": 0, \"error\": \"" + ex.Message.Replace("\"", "'") + "\"}");
        }
        Response.End();
    }
    
    private void HandleApplyBatchStatus()
    {
        Response.ContentType = "application/json";
        
        try
        {
            // Read JSON body
            string json;
            using (StreamReader reader = new StreamReader(Request.InputStream))
            {
                json = reader.ReadToEnd();
            }
            
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> data = serializer.Deserialize<Dictionary<string, object>>(json);
            
            string conditionType = data["conditionType"].ToString();
            string targetStatus = data["targetStatus"].ToString();
            bool negate = data.ContainsKey("negate") && Convert.ToBoolean(data["negate"]);
            
            int updated = ApplyBatchStatusChange(conditionType, targetStatus, negate, data);
            
            Response.Write(serializer.Serialize(new { success = true, updated = updated }));
        }
        catch (Exception ex)
        {
            Response.Write("{\"success\": false, \"message\": \"" + ex.Message.Replace("\"", "'") + "\"}");
        }
        Response.End();
    }
    
    private int GetBatchAffectedCount(string conditionType, bool negate, Dictionary<string, object> data)
    {
        // For payment condition, we need a special query that joins with accounts DB
        if (conditionType == "payment")
        {
            return GetPaymentAffectedCount(negate, data);
        }
        
        string whereClause = BuildBatchWhereClause(conditionType, negate, data);
        
        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            string sql = "SELECT COUNT(*) FROM acad_student WHERE " + whereClause;
            
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                AddBatchParameters(cmd, conditionType, data);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }
    }
    
    private int GetPaymentAffectedCount(bool negate, Dictionary<string, object> data)
    {
        int days = Convert.ToInt32(data["paymentDays"]);
        
        // Query the accounts database for students who made payments (CR transactions)
        // The fin_ledger table has accountcode which stores the student's regno
        // transactionType 'CR' = credit = payment received
        string accountsConnStr = ConfigurationManager.ConnectionStrings["accountsConnectionString"] != null
            ? ConfigurationManager.ConnectionStrings["accountsConnectionString"].ConnectionString
            : "Server=localhost;Database=campus_dynamics_accounts;Uid=root;Pwd=24thdecember1977;";
        
        using (MySqlConnection conn = new MySqlConnection(accountsConnStr))
        {
            conn.Open();
            string sql;
            
            if (negate)
            {
                // Students who DID NOT pay in the period
                sql = @"SELECT COUNT(*) 
                        FROM campus_dynamics.acad_student s
                        WHERE s.regno NOT IN (
                            SELECT DISTINCT l.accountcode 
                            FROM fin_ledger l 
                            WHERE l.transactionType = 'CR' 
                            AND l.transactionDate >= DATE_SUB(CURDATE(), INTERVAL @days DAY)
                        )";
            }
            else
            {
                // Students who DID pay in the period
                sql = @"SELECT COUNT(DISTINCT s.regno) 
                        FROM campus_dynamics.acad_student s
                        INNER JOIN fin_ledger l ON l.accountcode = s.regno
                        WHERE l.transactionType = 'CR' 
                        AND l.transactionDate >= DATE_SUB(CURDATE(), INTERVAL @days DAY)";
            }
            
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@days", days);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }
    }
    
    private int ApplyBatchStatusChange(string conditionType, string targetStatus, bool negate, Dictionary<string, object> data)
    {
        // For payment condition, we need a special approach
        if (conditionType == "payment")
        {
            return ApplyPaymentStatusChange(targetStatus, negate, data);
        }
        
        string whereClause = BuildBatchWhereClause(conditionType, negate, data);
        
        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            string sql = "UPDATE acad_student SET new_status = @targetStatus WHERE " + whereClause;
            
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@targetStatus", targetStatus);
                AddBatchParameters(cmd, conditionType, data);
                return cmd.ExecuteNonQuery();
            }
        }
    }
    
    private int ApplyPaymentStatusChange(string targetStatus, bool negate, Dictionary<string, object> data)
    {
        int days = Convert.ToInt32(data["paymentDays"]);
        
        string accountsConnStr = ConfigurationManager.ConnectionStrings["accountsConnectionString"] != null
            ? ConfigurationManager.ConnectionStrings["accountsConnectionString"].ConnectionString
            : "Server=localhost;Database=campus_dynamics_accounts;Uid=root;Pwd=24thdecember1977;";
        
        using (MySqlConnection conn = new MySqlConnection(accountsConnStr))
        {
            conn.Open();
            string sql;
            
            if (negate)
            {
                // Update students who DID NOT pay in the specified period
                sql = @"UPDATE campus_dynamics.acad_student s
                        SET s.new_status = @targetStatus
                        WHERE s.regno NOT IN (
                            SELECT DISTINCT l.accountcode 
                            FROM fin_ledger l 
                            WHERE l.transactionType = 'CR' 
                            AND l.transactionDate >= DATE_SUB(CURDATE(), INTERVAL @days DAY)
                        )";
            }
            else
            {
                // Update students who DID pay in the specified period
                sql = @"UPDATE campus_dynamics.acad_student s
                        SET s.new_status = @targetStatus
                        WHERE s.regno IN (
                            SELECT DISTINCT l.accountcode 
                            FROM fin_ledger l 
                            WHERE l.transactionType = 'CR' 
                            AND l.transactionDate >= DATE_SUB(CURDATE(), INTERVAL @days DAY)
                        )";
            }
            
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@targetStatus", targetStatus);
                cmd.Parameters.AddWithValue("@days", days);
                return cmd.ExecuteNonQuery();
            }
        }
    }
    
    private string BuildBatchWhereClause(string conditionType, bool negate, Dictionary<string, object> data)
    {
        string op = negate ? "!=" : "=";
        
        switch (conditionType)
        {
            case "entry_year":
                return "entryyear " + op + " @entryYear";
                
            case "programme":
                return "progid " + op + " @programme";
                
            case "current_status":
                return "new_status " + op + " @currentStatus";
                
            default:
                return "1=0"; // Safety - no records if unknown condition
        }
    }
    
    private void AddBatchParameters(MySqlCommand cmd, string conditionType, Dictionary<string, object> data)
    {
        switch (conditionType)
        {
            case "entry_year":
                cmd.Parameters.AddWithValue("@entryYear", data["entryYear"].ToString());
                break;
                
            case "programme":
                cmd.Parameters.AddWithValue("@programme", data["programme"].ToString());
                break;
                
            case "current_status":
                cmd.Parameters.AddWithValue("@currentStatus", data["currentStatus"].ToString());
                break;
        }
    }
    
    /// <summary>
    /// Handles AJAX request to preview how many students will be affected by batch validation.
    /// Filters by programme and entry year if specified.
    /// </summary>
    private void HandlePreviewBatchValidation()
    {
        Response.ContentType = "application/json";
        
        try
        {
            string programme = Request.QueryString["programme"] ?? "";
            string entryYear = Request.QueryString["entryYear"] ?? "";
            string entryNumbers = Request.QueryString["entryNumbers"] ?? "";
            
            int count = GetBatchValidationCount(programme, entryYear, entryNumbers);
            
            Response.Write("{\"count\": " + count + "}");
        }
        catch (Exception ex)
        {
            Response.Write("{\"count\": 0, \"error\": \"" + ex.Message.Replace("\"", "'") + "\"}");
        }
        Response.End();
    }
    
    /// <summary>
    /// Handles AJAX request to apply batch validation to students.
    /// Validates each student's results against their curriculum and updates has_passed, is_curriculum_fully_set, fail_reason.
    /// </summary>
    private void HandleApplyBatchValidation()
    {
        Response.ContentType = "application/json";
        
        try
        {
            // Read JSON body
            string json;
            using (StreamReader reader = new StreamReader(Request.InputStream))
            {
                json = reader.ReadToEnd();
            }
            
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> data = serializer.Deserialize<Dictionary<string, object>>(json);
            
            string programme = data.ContainsKey("programme") ? data["programme"].ToString() : "";
            string entryYear = data.ContainsKey("entryYear") ? data["entryYear"].ToString() : "";
            string entryNumbers = data.ContainsKey("entryNumbers") ? data["entryNumbers"].ToString() : "";
            
            int validated = ApplyBatchValidation(programme, entryYear, entryNumbers);
            
            Response.Write(serializer.Serialize(new { success = true, validated = validated }));
        }
        catch (Exception ex)
        {
            Response.Write("{\"success\": false, \"message\": \"" + ex.Message.Replace("\"", "'") + "\"}");
        }
        Response.End();
    }
    
    /// <summary>
    /// Gets count of students that would be affected by batch validation.
    /// Supports filtering by programme, entry year, or specific entry numbers (comma-separated).
    /// </summary>
    private int GetBatchValidationCount(string programme, string entryYear, string entryNumbers = "")
    {
        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            
            string sql = "SELECT COUNT(*) FROM acad_student WHERE 1=1";
            
            // If entry numbers specified, use them (ignore other filters)
            if (!string.IsNullOrEmpty(entryNumbers))
            {
                string[] entries = entryNumbers.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (entries.Length > 0)
                {
                    List<string> paramNames = new List<string>();
                    for (int i = 0; i < entries.Length; i++)
                        paramNames.Add("@entry" + i);
                    sql += " AND entryno IN (" + string.Join(",", paramNames) + ")";
                    
                    using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                    {
                        for (int i = 0; i < entries.Length; i++)
                            cmd.Parameters.AddWithValue("@entry" + i, entries[i].Trim());
                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            
            if (!string.IsNullOrEmpty(programme))
                sql += " AND progid = @programme";
            if (!string.IsNullOrEmpty(entryYear))
                sql += " AND entryyear = @entryYear";
            
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (!string.IsNullOrEmpty(programme))
                    cmd.Parameters.AddWithValue("@programme", programme);
                if (!string.IsNullOrEmpty(entryYear))
                    cmd.Parameters.AddWithValue("@entryYear", entryYear);
                
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }
    }
    
    /// <summary>
    /// Applies batch validation to all matching students.
    /// For each student:
    /// 1. Gets their specialisation (or default for their programme)
    /// 2. Checks if curriculum is fully set (is_fully_set field)
    /// 3. Compares curriculum course count per semester vs actual results
    /// 4. Updates has_passed, is_curriculum_fully_set, fail_reason fields
    /// Supports filtering by programme, entry year, or specific entry numbers (comma-separated).
    /// </summary>
    private int ApplyBatchValidation(string programme, string entryYear, string entryNumbers = "")
    {
        int validatedCount = 0;
        
        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            
            // Get all students matching the filter
            string selectSql = @"SELECT s.regno, s.progid, s.specialisation 
                                 FROM acad_student s 
                                 WHERE 1=1";
            
            List<string> entryParams = new List<string>();
            string[] entries = null;
            
            // If entry numbers specified, use them (ignore other filters)
            if (!string.IsNullOrEmpty(entryNumbers))
            {
                entries = entryNumbers.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (entries.Length > 0)
                {
                    for (int i = 0; i < entries.Length; i++)
                        entryParams.Add("@entry" + i);
                    selectSql += " AND s.entryno IN (" + string.Join(",", entryParams) + ")";
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(programme))
                    selectSql += " AND s.progid = @programme";
                if (!string.IsNullOrEmpty(entryYear))
                    selectSql += " AND s.entryyear = @entryYear";
            }
            
            DataTable students = new DataTable();
            using (MySqlCommand cmd = new MySqlCommand(selectSql, conn))
            {
                // Add parameters based on filter type
                if (!string.IsNullOrEmpty(entryNumbers) && entries != null && entries.Length > 0)
                {
                    for (int i = 0; i < entries.Length; i++)
                        cmd.Parameters.AddWithValue("@entry" + i, entries[i].Trim());
                }
                else
                {
                    if (!string.IsNullOrEmpty(programme))
                        cmd.Parameters.AddWithValue("@programme", programme);
                    if (!string.IsNullOrEmpty(entryYear))
                        cmd.Parameters.AddWithValue("@entryYear", entryYear);
                }
                
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd))
                {
                    da.Fill(students);
                }
            }
            
            // Process each student
            foreach (DataRow student in students.Rows)
            {
                string regno = student["regno"].ToString();
                string progid = student["progid"].ToString();
                string specId = student["specialisation"] != DBNull.Value ? student["specialisation"].ToString() : "";
                
                // Validate this student
                ValidateSingleStudent(conn, regno, progid, specId);
                validatedCount++;
            }
        }
        
        return validatedCount;
    }
    
    /// <summary>
    /// Validates a single student's results against their curriculum.
    /// Updates has_passed, is_curriculum_fully_set, and fail_reason fields.
    /// 
    /// Relaxed validation rules:
    /// 1. Only CORE courses count towards the required curriculum total (electives excluded).
    /// 2. Semester 3 is always auto-passed (supplementary / short semester).
    /// 3. Future year+semester slots where the student has ZERO results are skipped
    ///    (student hasn't reached that point yet).
    /// 4. If a student has 6 or more total results in a given year+semester,
    ///    that slot is auto-passed regardless of the curriculum requirement.
    /// 5. Curriculum is now compared per study_year + semester (not semester alone).
    /// </summary>
    private void ValidateSingleStudent(MySqlConnection conn, string regno, string progid, string specId)
    {
        string hasPassed = "No";
        string isCurriculumFullySet = "No";
        List<string> failReasons = new List<string>();
        
        try
        {
            // Normalise specId: treat "-", "0", whitespace-only as empty (unassigned)
            specId = (specId ?? "").Trim();
            bool needDefault = string.IsNullOrEmpty(specId) || specId == "0" || specId == "-";
            
            // Step 1: Get specialisation ID (student's own or default for programme)
            if (needDefault)
            {
                specId = "";
                // Strategy 1: Find specialisation marked as default
                string defaultSpecSql = @"SELECT spec_id FROM acad_specialisation 
                                          WHERE prog_id = @progid AND is_default = 'Yes' 
                                          LIMIT 1";
                using (MySqlCommand cmd = new MySqlCommand(defaultSpecSql, conn))
                {
                    cmd.Parameters.AddWithValue("@progid", progid);
                    object result = cmd.ExecuteScalar();
                    specId = result != null ? result.ToString().Trim() : "";
                }
                
                // Strategy 2: Find specialisation named 'Default'
                if (string.IsNullOrEmpty(specId))
                {
                    string namedDefaultSql = @"SELECT spec_id FROM acad_specialisation 
                                              WHERE prog_id = @progid AND (spec = 'Default' OR spec LIKE '%Default%')
                                              ORDER BY spec_id LIMIT 1";
                    using (MySqlCommand cmd = new MySqlCommand(namedDefaultSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@progid", progid);
                        object result = cmd.ExecuteScalar();
                        specId = result != null ? result.ToString().Trim() : "";
                    }
                }
                
                // Strategy 3: Fall back to the first specialisation for this programme
                if (string.IsNullOrEmpty(specId))
                {
                    string firstSpecSql = @"SELECT spec_id FROM acad_specialisation 
                                            WHERE prog_id = @progid 
                                            ORDER BY spec_id LIMIT 1";
                    using (MySqlCommand cmd = new MySqlCommand(firstSpecSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@progid", progid);
                        object result = cmd.ExecuteScalar();
                        specId = result != null ? result.ToString().Trim() : "";
                    }
                }
            }
            
            // Step 2: Check if specialisation exists and is fully set
            if (!string.IsNullOrEmpty(specId))
            {
                string checkSpecSql = @"SELECT COALESCE(is_fully_set, 'No') AS is_fully_set FROM acad_specialisation WHERE spec_id = @specId";
                using (MySqlCommand cmd = new MySqlCommand(checkSpecSql, conn))
                {
                    cmd.Parameters.AddWithValue("@specId", specId);
                    object result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        isCurriculumFullySet = result.ToString().Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase) ? "Yes" : "No";
                    }
                }
            }
            
            if (string.IsNullOrEmpty(specId))
            {
                failReasons.Add("No curriculum defined for programme");
            }
            else if (isCurriculumFullySet != "Yes")
            {
                failReasons.Add("Curriculum not fully set");
            }
            else
            {
                // ============================================================
                // Step 3: Get CORE curriculum courses grouped by study_year + semester
                // Only CORE courses count towards the required total.
                // ============================================================
                string curriculumSql = @"SELECT study_year, semester, COUNT(*) as course_count 
                                         FROM acad_programmecourses 
                                         WHERE specialisation_id = @specId 
                                           AND UPPER(TRIM(course_type)) = 'CORE'
                                         GROUP BY study_year, semester 
                                         ORDER BY study_year, semester";
                
                // Key = "Y{study_year}S{semester}", Value = required CORE course count
                Dictionary<string, int> curriculumByYearSem = new Dictionary<string, int>();
                // Also track the raw year and semester for each key
                Dictionary<string, int[]> yearSemLookup = new Dictionary<string, int[]>();
                
                using (MySqlCommand cmd = new MySqlCommand(curriculumSql, conn))
                {
                    cmd.Parameters.AddWithValue("@specId", specId);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int yr = Convert.ToInt32(reader["study_year"]);
                            int sem = Convert.ToInt32(reader["semester"]);
                            string key = "Y" + yr + "S" + sem;
                            int count = Convert.ToInt32(reader["course_count"]);
                            curriculumByYearSem[key] = count;
                            yearSemLookup[key] = new int[] { yr, sem };
                        }
                    }
                }
                
                // ============================================================
                // Step 4: Get ALL student results grouped by studyyear + semester
                //         (total count - includes F grades - for the 6+ auto-pass rule)
                // ============================================================
                string allResultsSql = @"SELECT studyyear, semester, COUNT(*) as total_count 
                                         FROM acad_results 
                                         WHERE TRIM(regno) = @regno 
                                           AND grade IS NOT NULL
                                         GROUP BY studyyear, semester";
                
                Dictionary<string, int> totalResultsByYearSem = new Dictionary<string, int>();
                using (MySqlCommand cmd = new MySqlCommand(allResultsSql, conn))
                {
                    cmd.Parameters.AddWithValue("@regno", regno);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int yr = Convert.ToInt32(reader["studyyear"]);
                            int sem = Convert.ToInt32(reader["semester"]);
                            string key = "Y" + yr + "S" + sem;
                            totalResultsByYearSem[key] = Convert.ToInt32(reader["total_count"]);
                        }
                    }
                }
                
                // ============================================================
                // Step 5: Get PASSED results grouped by studyyear + semester
                //         (grade IS NOT NULL and grade != 'F')
                // ============================================================
                string passedResultsSql = @"SELECT studyyear, semester, COUNT(*) as result_count 
                                            FROM acad_results 
                                            WHERE TRIM(regno) = @regno 
                                              AND grade IS NOT NULL 
                                              AND UPPER(TRIM(grade)) != 'F'
                                            GROUP BY studyyear, semester";
                
                Dictionary<string, int> passedResultsByYearSem = new Dictionary<string, int>();
                using (MySqlCommand cmd = new MySqlCommand(passedResultsSql, conn))
                {
                    cmd.Parameters.AddWithValue("@regno", regno);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int yr = Convert.ToInt32(reader["studyyear"]);
                            int sem = Convert.ToInt32(reader["semester"]);
                            string key = "Y" + yr + "S" + sem;
                            passedResultsByYearSem[key] = Convert.ToInt32(reader["result_count"]);
                        }
                    }
                }
                
                // ============================================================
                // Step 6: Compare curriculum vs results per year+semester
                //         with relaxed rules
                // ============================================================
                bool allSemestersPassed = true;
                int validatedSlots = 0; // track how many slots were actually evaluated
                
                foreach (var kvp in curriculumByYearSem)
                {
                    string key = kvp.Key;
                    int required = kvp.Value;
                    int[] yrSem = yearSemLookup[key];
                    int year = yrSem[0];
                    int semester = yrSem[1];
                    
                    int totalResults = totalResultsByYearSem.ContainsKey(key) ? totalResultsByYearSem[key] : 0;
                    int passedResults = passedResultsByYearSem.ContainsKey(key) ? passedResultsByYearSem[key] : 0;
                    
                    // Rule 1: Semester 3 is always auto-passed (skip validation entirely)
                    if (semester == 3)
                    {
                        validatedSlots++;
                        continue; // auto-pass
                    }
                    
                    // Rule 2: Skip future slots where student has NO results at all
                    //         (student hasn't reached this year+semester yet)
                    if (totalResults == 0)
                    {
                        continue; // don't count, don't fail - future semester
                    }
                    
                    validatedSlots++;
                    
                    // Rule 3: If student has 6 or more total results in this slot,
                    //         auto-pass regardless of curriculum requirement
                    if (totalResults >= 6)
                    {
                        continue; // auto-pass - heavy course load
                    }
                    
                    // Rule 4: Normal comparison - passed results vs required CORE count
                    if (passedResults < required)
                    {
                        allSemestersPassed = false;
                        failReasons.Add("Y" + year + " Sem" + semester + ": " + passedResults + "/" + required + " core courses passed");
                    }
                }
                
                if (allSemestersPassed && validatedSlots > 0)
                {
                    hasPassed = "Yes";
                }
                else if (curriculumByYearSem.Count == 0)
                {
                    failReasons.Add("Curriculum has no CORE courses defined");
                }
                else if (validatedSlots == 0)
                {
                    // Student has no results in any curriculum slot yet
                    failReasons.Add("No results found for any curriculum semester");
                }
            }
        }
        catch (Exception ex)
        {
            failReasons.Add("Validation error: " + ex.Message);
        }
        
        // Step 7: Update the student record
        string failReason = failReasons.Count > 0 ? string.Join("; ", failReasons) : null;
        
        string updateSql = @"UPDATE acad_student 
                             SET has_passed = @hasPassed, 
                                 is_curriculum_fully_set = @isCurriculumFullySet, 
                                 fail_reason = @failReason 
                             WHERE regno = @regno";
        
        using (MySqlCommand cmd = new MySqlCommand(updateSql, conn))
        {
            cmd.Parameters.AddWithValue("@hasPassed", hasPassed);
            cmd.Parameters.AddWithValue("@isCurriculumFullySet", isCurriculumFullySet);
            cmd.Parameters.AddWithValue("@failReason", failReason != null ? (object)failReason : DBNull.Value);
            cmd.Parameters.AddWithValue("@regno", regno);
            cmd.ExecuteNonQuery();
        }
    }
    
    #endregion
    
    #region Summary Report Export Handlers
    
    /// <summary>
    /// Handles AJAX request to preview the count of students for summary report.
    /// </summary>
    // ════════════════════════════════════════════════════════════════════════
    //  Source-aware results layer for the Summary Report (aligned with the staged
    //  marks workflow + ResultsExporter). "published" reads acad_results; the
    //  staged stages (approved/captured/entered) read campus_dynamics_portal.
    //  acad_course_registration at that mark_stage and derive grade / grade-point /
    //  credit-units on the fly (NCHE 2015) — same column names either way.
    // ════════════════════════════════════════════════════════════════════════
    private static string SR_StageFor(string source)
    {
        switch ((source ?? "").Trim().ToLowerInvariant())
        {
            case "entered":  return "ENTERED";
            case "captured": return "CAPTURED";
            case "approved": return "APPROVED";
            default:         return null;   // published / all
        }
    }
    // "all" = every result that is published OR fully marked (both CW & Exam). Widest coverage.
    private static bool SR_IsAll(string source) { return (source ?? "").Trim().ToLowerInvariant() == "all"; }
    private static bool SR_IsPublished(string source) { return !SR_IsAll(source) && SR_StageFor(source) == null; }
    private static string SR_SourceLabel(string source)
    {
        switch ((source ?? "").Trim().ToLowerInvariant())
        {
            case "all":      return "All results (published block figures + fully-marked provisional)";
            case "entered":  return "Entered (Lecturer) — provisional, pending capture";
            case "captured": return "Captured (HOD) — provisional, pending approval";
            case "approved": return "Approved (Dean) — provisional, pending Senate approval";
            default:         return "Published results";
        }
    }
    private static string SR_GradeCase(string s)
    {
        return "CASE WHEN " + s + ">=80 THEN 'A' WHEN " + s + ">=75 THEN 'B+' WHEN " + s + ">=70 THEN 'B' " +
               "WHEN " + s + ">=65 THEN 'C+' WHEN " + s + ">=60 THEN 'C' WHEN " + s + ">=55 THEN 'D+' " +
               "WHEN " + s + ">=50 THEN 'D' ELSE 'F' END";
    }
    private static string SR_GptCase(string s)
    {
        return "CASE WHEN " + s + ">=80 THEN 5.0 WHEN " + s + ">=75 THEN 4.5 WHEN " + s + ">=70 THEN 4.0 " +
               "WHEN " + s + ">=65 THEN 3.5 WHEN " + s + ">=60 THEN 3.0 WHEN " + s + ">=55 THEN 2.5 " +
               "WHEN " + s + ">=50 THEN 2.0 ELSE 0.0 END";
    }
    // FROM clause whose row alias 'r' exposes the same columns as acad_results
    // (regno, courseid, progid, acad, semester, studyyear, score, grade, gradept, gpa,
    //  CreditUnits, is_retake) plus cw_marks/exam_marks/prov_total/sub_status for the
    //  marksheet, and the standard student/programme/faculty/course joins.
    private static string SR_ResultsFrom(string source)
    {
        string joins =
            " LEFT JOIN acad_student s ON s.regno=r.regno " +
            " LEFT JOIN acad_programme p ON p.progcode=r.progid " +
            " LEFT JOIN acad_faculty f ON f.faculty_code=p.faculty_code " +
            " LEFT JOIN acad_course c ON c.courseID=r.courseid ";
        const string SC = "cr.provisional_total_marks";

        if (SR_IsAll(source))
        {
            // ALL results: every acad_course_registration row with a total mark that is either
            // fully marked (BOTH course-work & exam) OR published. After the classic-marks
            // migration this table holds the published block figures AND portal marks, so this is
            // the widest coverage. (Heavy on MySQL 5.6 — used only for the on-demand PDF export;
            // the interactive count/cascade use lean cohort-driven queries.)
            return
                " FROM ( SELECT cr.regno, cr.courseID courseid, cr.prog_id progid, cr.acad_year acad, cr.semester, " +
                "   (SELECT MIN(pc.study_year) FROM acad_programmecourses pc WHERE pc.progcode=cr.prog_id AND pc.course_code=cr.courseID) studyyear, " +
                "   " + SC + " score, " + SR_GradeCase(SC) + " grade, " + SR_GptCase(SC) + " gradept, " + SR_GptCase(SC) + " gpa, " +
                "   IFNULL(ac.CreditUnit,0) CreditUnits, " +
                "   IF(cr.registration_type='RETAKE' OR cr.retake_registration_id IS NOT NULL,1,0) is_retake, " +
                "   cr.provisional_course_work_marks cw_marks, cr.provisional_exam_marks exam_marks, " +
                "   cr.provisional_total_marks prov_total, cr.mark_stage sub_status " +
                "  FROM campus_dynamics_portal.acad_course_registration cr " +
                "  LEFT JOIN acad_course ac ON ac.courseID=cr.courseID " +
                "  WHERE " + SC + " IS NOT NULL AND ( (cr.provisional_course_work_marks IS NOT NULL AND cr.provisional_exam_marks IS NOT NULL) " +
                "        OR UPPER(IFNULL(cr.mark_stage,''))='PUBLISHED' OR UPPER(IFNULL(cr.provisional_marks_status,''))='PUBLISHED' ) ) r " +
                joins;
        }

        string stage = SR_StageFor(source);
        if (stage == null)
            return " FROM acad_results r " + joins;   // published

        return
            " FROM ( SELECT cr.regno, cr.courseID courseid, cr.prog_id progid, cr.acad_year acad, cr.semester, " +
            "   (SELECT MIN(pc.study_year) FROM acad_programmecourses pc WHERE pc.progcode=cr.prog_id AND pc.course_code=cr.courseID) studyyear, " +
            "   " + SC + " score, " + SR_GradeCase(SC) + " grade, " + SR_GptCase(SC) + " gradept, " + SR_GptCase(SC) + " gpa, " +
            "   IFNULL(ac.CreditUnit,0) CreditUnits, " +
            "   IF(cr.registration_type='RETAKE' OR cr.retake_registration_id IS NOT NULL,1,0) is_retake, " +
            "   cr.provisional_course_work_marks cw_marks, cr.provisional_exam_marks exam_marks, " +
            "   cr.provisional_total_marks prov_total, cr.mark_stage sub_status " +
            "  FROM campus_dynamics_portal.acad_course_registration cr " +
            "  LEFT JOIN acad_course ac ON ac.courseID=cr.courseID " +
            "  WHERE cr.mark_stage='" + stage + "' AND cr.provisional_total_marks IS NOT NULL ) r " +
            joins;
    }

    // The "all" mark predicate on an acad_course_registration alias: has a total, and is either
    // fully marked (both CW & Exam) or published. Used by the lean cohort-driven count/cascade so
    // the interactive parts stay fast (the derived-table SR_ResultsFrom("all") is only for export).
    private static string SR_AllMarkPredicate(string cr)
    {
        return " " + cr + ".provisional_total_marks IS NOT NULL AND ( (" + cr + ".provisional_course_work_marks IS NOT NULL AND " + cr + ".provisional_exam_marks IS NOT NULL) " +
               "OR UPPER(IFNULL(" + cr + ".mark_stage,''))='PUBLISHED' OR UPPER(IFNULL(" + cr + ".provisional_marks_status,''))='PUBLISHED' ) ";
    }
    // The course→study-year expression for a cr alias (matches the derived-table studyyear column).
    private static string SR_StudyYearExpr(string cr)
    {
        return "(SELECT MIN(pc.study_year) FROM acad_programmecourses pc WHERE pc.progcode=" + cr + ".prog_id AND pc.course_code=" + cr + ".courseID)";
    }

    // ----- Academic-year (sitting) scoping ------------------------------------------------------
    // A results summary is a SITTING = (academic year, year-of-study, semester). The academic-year
    // filter is what pins it to one sitting instead of pooling every year a course was ever taught.
    // acadCol = the academic-year column on the marks alias ("acad" for acad_results / the derived
    // SR_ResultsFrom alias r; "acad_year" for the raw portal cr). Caller binds @acadYear (via
    // SR_BindSitting) when acadYear is non-empty, and @semester when semester is non-empty.
    //
    // excludePromoted (optional, default off) drops students whose LATEST active semester
    // registration is AFTER the requested sitting — i.e. keep only those still at this level. It is
    // OFF by default on purpose: inclusion is decided by the marks' own academic year, never by the
    // student's current standing, so a student who has since moved on still appears on the report for
    // the semester they actually sat (no completed-semester marks are ever dropped).
    private static string SR_SittingFilter(string acadCol, string acadYear, string semester, bool excludePromoted)
    {
        if (string.IsNullOrWhiteSpace(acadYear)) return "";
        string w = " AND " + acadCol + " = @acadYear";
        if (excludePromoted && !string.IsNullOrWhiteSpace(semester))
            w += " AND NOT EXISTS (SELECT 1 FROM acad_registration g WHERE g.regno = s.regno " +
                 "AND g.regstatus IN ('REGISTERED','CLEARED','LATE REGISTERED') " +
                 "AND (g.acad_year > @acadYear OR (g.acad_year = @acadYear AND g.semester > @semester)))";
        return w;
    }
    private static void SR_BindSitting(MySqlCommand cmd, string acadYear)
    {
        if (!string.IsNullOrWhiteSpace(acadYear)) cmd.Parameters.AddWithValue("@acadYear", acadYear);
    }

    // ----- Specialisation (optional) scoping -----------------------------------------------------
    // A student's specialisation lives in acad_student.specialisation, which holds the spec_id of an
    // acad_specialisation row (NOT its name) — spec_id 13 is the institution's "-" placeholder that
    // 30k students carry to mean "no specialisation". The filter is therefore an id comparison, and
    // it is only ever applied when the caller asked for one.
    //
    // SR_SpecId is the single gate: anything that is not a positive integer becomes 0 = no filter.
    // A bad or absent value can only ever WIDEN to "all specialisations", never silently narrow to
    // an empty set, and never reaches SQL as text.
    private static int SR_SpecId(string spec)
    {
        int id;
        if (string.IsNullOrWhiteSpace(spec)) return 0;
        if (!int.TryParse(spec.Trim(), out id)) return 0;
        return id > 0 ? id : 0;
    }
    private static string SR_SpecFilter(string spec)
    {
        return SR_SpecId(spec) > 0 ? " AND s.specialisation = @specId" : "";
    }
    private static void SR_BindSpec(MySqlCommand cmd, string spec)
    {
        int id = SR_SpecId(spec);
        if (id > 0) cmd.Parameters.AddWithValue("@specId", id);
    }

    /// <summary>
    /// Display name of a specialisation, for report headings. Empty when none is selected or the
    /// id no longer resolves, so a caller can append it unconditionally.
    /// </summary>
    private string SR_SpecName(string spec)
    {
        int id = SR_SpecId(spec);
        if (id <= 0) return "";
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT TRIM(IFNULL(spec,'')) FROM acad_specialisation WHERE spec_id=@id LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    object o = cmd.ExecuteScalar();
                    string nm = o == null || o == DBNull.Value ? "" : Convert.ToString(o).Trim();
                    return nm == "-" ? "" : nm;
                }
            }
        }
        catch { return ""; }
    }

    /// <summary>
    /// Cascade helper for the Summary Report modal: given a source + programme, returns every
    /// (entryYear, studyYear, semester) combination that actually has gradeable data, with a
    /// student count. The client uses this to populate — and auto-select — the dependent
    /// dropdowns, so the user can never pick an empty combination. Leaner than SR_ResultsFrom
    /// (driven from acad_student for speed) but the same logical result set.
    /// </summary>
    private void HandleSummaryReportCascade()
    {
        Response.Clear();
        Response.ContentType = "application/json";
        try
        {
            string programme = Request.QueryString["programme"] ?? "";
            string source = Request.QueryString["source"] ?? "approved";
            var combos = new List<object>();
            var years = new List<string>();   // ALL entry years the programme has (not limited to ones with marks)
            var specs = new List<object>();   // specialisations actually held by this programme's students

            if (!string.IsNullOrWhiteSpace(programme))
            {
                string stage = SR_StageFor(source);   // null => published
                string sql;
                if (SR_IsAll(source))
                {
                    // ALL: published OR fully-marked provisional, cohort-driven. Study-year comes from a
                    // pre-grouped acad_programmecourses join (≈4× faster than a per-row subquery here,
                    // since this scans every one of the programme's marks across all years). Academic
                    // year (cr.acad_year) is now part of the combo so the client can cascade on the sitting.
                    sql = "SELECT s.entryyear y, cr.acad_year ay, pcs.sy sy, cr.semester s, COUNT(DISTINCT s.regno) n " +
                          "FROM acad_student s JOIN campus_dynamics_portal.acad_course_registration cr ON cr.regno = s.regno " +
                          "LEFT JOIN (SELECT progcode, course_code, MIN(study_year) sy FROM acad_programmecourses GROUP BY progcode, course_code) pcs " +
                          "  ON pcs.progcode = cr.prog_id AND pcs.course_code = cr.courseID " +
                          "WHERE s.progid = @programme AND TRIM(IFNULL(s.entryyear,'')) <> '' AND" + SR_AllMarkPredicate("cr") +
                          "  AND cr.semester IS NOT NULL " +
                          "GROUP BY s.entryyear, cr.acad_year, pcs.sy, cr.semester HAVING n > 0 AND pcs.sy IS NOT NULL " +
                          "ORDER BY ay DESC, y DESC, sy, s";
                }
                else if (stage == null)
                {
                    sql = "SELECT s.entryyear y, r.acad ay, r.studyyear sy, r.semester s, COUNT(DISTINCT s.regno) n " +
                          "FROM acad_student s JOIN acad_results r ON r.regno = s.regno " +
                          "WHERE s.progid = @programme AND TRIM(IFNULL(s.entryyear,'')) <> '' " +
                          "  AND r.studyyear IS NOT NULL AND r.semester IS NOT NULL " +
                          "GROUP BY s.entryyear, r.acad, r.studyyear, r.semester HAVING n > 0 " +
                          "ORDER BY ay DESC, y DESC, sy, s";
                }
                else
                {
                    sql = "SELECT s.entryyear y, sub.ay ay, sub.sy sy, sub.s s, COUNT(DISTINCT s.regno) n " +
                          "FROM acad_student s JOIN ( " +
                          "   SELECT cr.regno, cr.acad_year ay, cr.semester s, " +
                          "     (SELECT MIN(pc.study_year) FROM acad_programmecourses pc " +
                          "        WHERE pc.progcode = cr.prog_id AND pc.course_code = cr.courseID) sy " +
                          "   FROM campus_dynamics_portal.acad_course_registration cr " +
                          "   WHERE cr.mark_stage = @stage AND cr.provisional_total_marks IS NOT NULL ) sub " +
                          "  ON sub.regno = s.regno " +
                          "WHERE s.progid = @programme AND TRIM(IFNULL(s.entryyear,'')) <> '' AND sub.sy IS NOT NULL " +
                          "GROUP BY s.entryyear, sub.ay, sub.sy, sub.s HAVING n > 0 " +
                          "ORDER BY ay DESC, y DESC, sy, s";
                }

                using (MySqlConnection conn = new MySqlConnection(ConnectionString))
                {
                    conn.Open();
                    using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@programme", programme);
                        if (stage != null) cmd.Parameters.AddWithValue("@stage", stage);
                        using (MySqlDataReader rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                combos.Add(new
                                {
                                    y = rdr["y"] == DBNull.Value ? "" : Convert.ToString(rdr["y"]).Trim(),
                                    ay = rdr["ay"] == DBNull.Value ? "" : Convert.ToString(rdr["ay"]).Trim(),
                                    sy = rdr["sy"] == DBNull.Value ? "" : Convert.ToString(rdr["sy"]).Trim(),
                                    s = rdr["s"] == DBNull.Value ? "" : Convert.ToString(rdr["s"]).Trim(),
                                    n = rdr["n"] == DBNull.Value ? 0 : Convert.ToInt32(rdr["n"])
                                });
                            }
                        }
                    }

                    // Every entry year the programme has (so the user can pick any, not just ones with marks).
                    using (MySqlCommand cmdY = new MySqlCommand(
                        "SELECT DISTINCT entryyear FROM acad_student WHERE progid=@programme AND TRIM(IFNULL(entryyear,''))<>'' ORDER BY entryyear DESC", conn))
                    {
                        cmdY.Parameters.AddWithValue("@programme", programme);
                        using (MySqlDataReader ry = cmdY.ExecuteReader())
                            while (ry.Read()) years.Add(Convert.ToString(ry["entryyear"]).Trim());
                    }

                    // Specialisations the programme's students ACTUALLY hold, counted, newest-heaviest
                    // first. Driven from acad_student rather than from acad_specialisation.prog_id so
                    // the list can never offer an option that matches nobody — a specialisation
                    // declared against the programme but carried by no student would return an empty
                    // report, and a specialisation carried by students but declared against another
                    // programme (there is drift in this data) would be missing. Placeholder rows
                    // (spec_id 13 / a "-" name / blank) are what "no specialisation" is stored as,
                    // so they are not offered as a choice.
                    using (MySqlCommand cmdS = new MySqlCommand(
                        "SELECT sp.spec_id, TRIM(sp.spec) spec, TRIM(IFNULL(sp.abbrev,'')) abbrev, COUNT(*) n " +
                        "FROM acad_student s " +
                        "JOIN acad_specialisation sp ON sp.spec_id = s.specialisation " +
                        "WHERE s.progid = @programme " +
                        "  AND TRIM(IFNULL(sp.spec,'')) NOT IN ('','-') " +
                        "GROUP BY sp.spec_id, sp.spec, sp.abbrev " +
                        "ORDER BY n DESC, sp.spec", conn))
                    {
                        cmdS.Parameters.AddWithValue("@programme", programme);
                        using (MySqlDataReader rs = cmdS.ExecuteReader())
                            while (rs.Read())
                                specs.Add(new
                                {
                                    id = Convert.ToString(rs["spec_id"]),
                                    name = Convert.ToString(rs["spec"]).Trim(),
                                    abbrev = Convert.ToString(rs["abbrev"]).Trim(),
                                    n = rs["n"] == DBNull.Value ? 0 : Convert.ToInt32(rs["n"])
                                });
                    }
                }
            }

            Response.Write(new JavaScriptSerializer().Serialize(new { combos = combos, years = years, specs = specs }));
        }
        catch (Exception ex)
        {
            Response.Write("{\"combos\": [], \"error\": \"" + ex.Message.Replace("\"", "'").Replace("\r", " ").Replace("\n", " ") + "\"}");
        }
        try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
    }

    private void HandlePreviewSummaryReport()
    {
        Response.Clear();
        Response.ContentType = "application/json";
        
        try
        {
            string programme = Request.QueryString["programme"] ?? "";
            string entryYear = Request.QueryString["entryYear"] ?? "";
            string studyYear = Request.QueryString["studyYear"] ?? "";
            string semester = Request.QueryString["semester"] ?? "";
            string entryNumbers = Request.QueryString["entryNumbers"] ?? "";
            string source = Request.QueryString["source"] ?? "approved";
            string acadYear = Request.QueryString["acadYear"] ?? "";
            bool excludePromoted = (Request.QueryString["excludePromoted"] ?? "") == "1";
            string specialisation = Request.QueryString["specialisation"] ?? "";

            // Guard: never count the whole database. Require a programme (or explicit reg numbers).
            int count = (string.IsNullOrWhiteSpace(programme) && string.IsNullOrWhiteSpace(entryNumbers))
                ? 0
                : GetSummaryReportStudentCount(programme, entryYear, studyYear, semester, entryNumbers, source, acadYear, excludePromoted, specialisation);

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Response.Write(serializer.Serialize(new { count = count }));
        }
        catch (Exception ex)
        {
            Response.Write("{\"count\": 0, \"error\": \"" + ex.Message.Replace("\"", "'").Replace("\r", " ").Replace("\n", " ") + "\"}");
        }
        
        try
        {
            Response.End();
        }
        catch (System.Threading.ThreadAbortException)
        {
            // Expected - Response.End() always throws this
        }
    }
    
    /// <summary>
    /// Gets count of students who have results matching the filters.
    /// </summary>
    private int GetSummaryReportStudentCount(string programme, string entryYear, string studyYear, string semester, string entryNumbers, string source)
    {
        return GetSummaryReportStudentCount(programme, entryYear, studyYear, semester, entryNumbers, source, "", false);
    }

    private int GetSummaryReportStudentCount(string programme, string entryYear, string studyYear, string semester, string entryNumbers, string source, string acadYear, bool excludePromoted)
    {
        return GetSummaryReportStudentCount(programme, entryYear, studyYear, semester, entryNumbers, source, acadYear, excludePromoted, "");
    }

    private int GetSummaryReportStudentCount(string programme, string entryYear, string studyYear, string semester, string entryNumbers, string source, string acadYear, bool excludePromoted, string specialisation)
    {
        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();

            // Source-aware. For "all" use a lean cohort-driven direct join (fast on MySQL 5.6);
            // otherwise the normalized SR_ResultsFrom (published = acad_results; staged = ACR@stage).
            bool all = SR_IsAll(source);
            string sql = all
                ? "SELECT COUNT(DISTINCT s.regno) FROM acad_student s JOIN campus_dynamics_portal.acad_course_registration cr ON cr.regno=s.regno WHERE" + SR_AllMarkPredicate("cr")
                : "SELECT COUNT(DISTINCT s.regno) " + SR_ResultsFrom(source) + " WHERE 1=1";

            List<string> entryParams = new List<string>();
            string[] entries = null;

            // Specific students: match canonical regno OR legacy entryno.
            if (!string.IsNullOrEmpty(entryNumbers))
            {
                entries = entryNumbers.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (entries.Length > 0)
                {
                    for (int i = 0; i < entries.Length; i++)
                        entryParams.Add("@entry" + i);
                    string inList = string.Join(",", entryParams);
                    sql += " AND (s.regno IN (" + inList + ") OR s.entryno IN (" + inList + "))";
                }
            }

            // Always filter by programme, entry year, study year, and semester
            if (!string.IsNullOrEmpty(programme))
                sql += " AND s.progid = @programme";
            if (!string.IsNullOrEmpty(entryYear))
                sql += " AND s.entryyear = @entryYear";
            if (!string.IsNullOrEmpty(studyYear))
                sql += all ? " AND @studyYear = " + SR_StudyYearExpr("cr") : " AND r.studyyear = @studyYear";
            if (!string.IsNullOrEmpty(semester))
                sql += all ? " AND cr.semester = @semester" : " AND r.semester = @semester";
            // Academic-year sitting filter: the "all" branch scans the raw portal cr (acad_year);
            // every other source uses the derived/results alias r (acad).
            sql += SR_SittingFilter(all ? "cr.acad_year" : "r.acad", acadYear, semester, excludePromoted);
            sql += SR_SpecFilter(specialisation);

            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (!string.IsNullOrEmpty(entryNumbers) && entries != null && entries.Length > 0)
                {
                    for (int i = 0; i < entries.Length; i++)
                        cmd.Parameters.AddWithValue("@entry" + i, entries[i].Trim());
                }

                if (!string.IsNullOrEmpty(programme))
                    cmd.Parameters.AddWithValue("@programme", programme);
                if (!string.IsNullOrEmpty(entryYear))
                    cmd.Parameters.AddWithValue("@entryYear", entryYear);
                if (!string.IsNullOrEmpty(studyYear))
                    cmd.Parameters.AddWithValue("@studyYear", studyYear);
                if (!string.IsNullOrEmpty(semester))
                    cmd.Parameters.AddWithValue("@semester", semester);
                SR_BindSitting(cmd, acadYear);
                SR_BindSpec(cmd, specialisation);

                object result = cmd.ExecuteScalar();
                return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
            }
        }
    }
    
    /// <summary>
    /// Handles the export of summary report as PDF.
    /// Generates a professional PDF with student details and their results.
    /// </summary>
    private void HandleExportSummaryReport()
    {
        try
        {
            string programme = Request.QueryString["programme"] ?? "";
            string entryYear = Request.QueryString["entryYear"] ?? "";
            string studyYear = Request.QueryString["studyYear"] ?? "";
            string semester = Request.QueryString["semester"] ?? "";
            string entryNumbers = Request.QueryString["entryNumbers"] ?? "";
            string source = Request.QueryString["source"] ?? "approved";
            int minCourses = ParseMinCourses(Request.QueryString["minCourses"]);   // provided figure, else 4
            string acadYear = Request.QueryString["acadYear"] ?? "";               // sitting anchor (blank = all years)
            bool excludePromoted = (Request.QueryString["excludePromoted"] ?? "") == "1";
            string specialisation = Request.QueryString["specialisation"] ?? "";   // optional: one specialisation only

            // Guard: refuse an unscoped export (would pull the whole database).
            if (string.IsNullOrWhiteSpace(programme) && string.IsNullOrWhiteSpace(entryNumbers))
            {
                Response.Clear();
                Response.ContentType = "text/html";
                Response.Write("<html><body style='font-family:sans-serif;padding:24px;color:#333'><h3>Please select a Programme (or enter registration numbers) before generating the report.</h3></body></html>");
                try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
                return;
            }

            // Get students with their results (source-aware)
            DataTable reportData = GetSummaryReportData(programme, entryYear, studyYear, semester, entryNumbers, source, acadYear, excludePromoted, specialisation);

            if (reportData.Rows.Count == 0)
            {
                Response.Clear();
                Response.ContentType = "text/html";
                Response.Write("<html><body><h3>No data found for the selected filters.</h3></body></html>");
                try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
                return;
            }

            // Generate PDF using DevExpress XtraPrinting
            GenerateSummaryReportPdf(reportData, studyYear, semester, entryYear, minCourses, acadYear);
        }
        catch (System.Threading.ThreadAbortException)
        {
            // Expected when Response.End() is called - ignore
        }
        catch (Exception ex)
        {
            Response.Clear();
            Response.ContentType = "text/html";
            Response.Write("<html><body><h3>Error generating report:</h3><p>" + Server.HtmlEncode(ex.Message) + "</p></body></html>");
            try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
        }
    }

    /// <summary>
    /// Handles the export of Performance Summary Report (CGPA-based categorization).
    /// Shows students categorized by: VC's List, Dean's List, Second Class Lower, Pass.
    /// </summary>
    private void HandleExportPerformanceReport()
    {
        try
        {
            string programme = Request.QueryString["programme"] ?? "";
            string entryYear = Request.QueryString["entryYear"] ?? "";
            string studyYear = Request.QueryString["studyYear"] ?? "";
            string semester = Request.QueryString["semester"] ?? "";
            string entryNumbers = Request.QueryString["entryNumbers"] ?? "";
            string source = Request.QueryString["source"] ?? "approved";
            int minCourses = ParseMinCourses(Request.QueryString["minCourses"]);   // provided figure, else 4
            string acadYear = Request.QueryString["acadYear"] ?? "";               // sitting anchor (blank = all years)
            bool excludePromoted = (Request.QueryString["excludePromoted"] ?? "") == "1";
            string specialisation = Request.QueryString["specialisation"] ?? "";   // optional: one specialisation only

            // Guard: refuse an unscoped export (would pull the whole database).
            if (string.IsNullOrWhiteSpace(programme) && string.IsNullOrWhiteSpace(entryNumbers))
            {
                Response.Clear();
                Response.ContentType = "text/html";
                Response.Write("<html><body style='font-family:sans-serif;padding:24px;color:#333'><h3>Please select a Programme (or enter registration numbers) before generating the report.</h3></body></html>");
                try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
                return;
            }

            // Get students with CGPA data (source-aware: published or a staged stage)
            DataTable studentData = GetPerformanceReportData(programme, entryYear, studyYear, semester, entryNumbers, source, acadYear, excludePromoted, specialisation);

            if (studentData.Rows.Count == 0)
            {
                Response.Clear();
                Response.ContentType = "text/html";
                Response.Write("<html><body><h3>No data found for the selected filters.</h3></body></html>");
                try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
                return;
            }

            // Get programme name. When one specialisation was asked for, it goes into the report's
            // title line — the Performance Report has no per-specialisation section headings (unlike
            // the Summary Report), so without this two exports of the same sitting would be
            // indistinguishable once printed.
            string programmeName = GetProgrammeName(programme);
            string specName = SR_SpecName(specialisation);
            if (specName != "") programmeName = programmeName + " - " + specName;

            // Generate Performance Report PDF
            GeneratePerformanceReportPdf(studentData, programmeName, entryYear, studyYear, semester, source, minCourses, acadYear);
        }
        catch (System.Threading.ThreadAbortException)
        {
            // Expected when Response.End() is called - ignore
        }
        catch (Exception ex)
        {
            Response.Clear();
            Response.ContentType = "text/html";
            Response.Write("<html><body><h3>Error generating report:</h3><p>" + Server.HtmlEncode(ex.Message) + "</p></body></html>");
            try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
        }
    }
    
    private string GetProgrammeName(string progCode)
    {
        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand("SELECT progname FROM acad_programme WHERE progcode = @code", conn))
            {
                cmd.Parameters.AddWithValue("@code", progCode);
                object result = cmd.ExecuteScalar();
                return result != null ? result.ToString() : progCode;
            }
        }
    }
    
    private DataTable GetPerformanceReportData(string programme, string entryYear, string studyYear, string semester, string entryNumbers)
    {
        return GetPerformanceReportData(programme, entryYear, studyYear, semester, entryNumbers, "approved", "", false);
    }

    private DataTable GetPerformanceReportData(string programme, string entryYear, string studyYear, string semester, string entryNumbers, string source)
    {
        return GetPerformanceReportData(programme, entryYear, studyYear, semester, entryNumbers, source, "", false);
    }

    private DataTable GetPerformanceReportData(string programme, string entryYear, string studyYear, string semester, string entryNumbers, string source, string acadYear, bool excludePromoted)
    {
        return GetPerformanceReportData(programme, entryYear, studyYear, semester, entryNumbers, source, acadYear, excludePromoted, "");
    }

    private DataTable GetPerformanceReportData(string programme, string entryYear, string studyYear, string semester, string entryNumbers, string source, string acadYear, bool excludePromoted, string specialisation)
    {
        DataTable dt = new DataTable();

        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();

            // Two GPA figures per student:
            //   sem_gpa  = credit-weighted GPA over the selected study-year + semester (from the chosen source)
            //   cgpa     = authoritative cumulative CGPA (acad_CGPAFinder, published & retake-aware);
            //              0 for a fresh cohort with no published history — the caller then falls back to sem_gpa.
            // Also surfaced: fails (score<50) and any_retake, so the report can show Fail / Retake / Incomplete.
            // NOTE: no HAVING filter — every student appears so the board sees fails/incompletes too.
            string sql = @"SELECT
                s.regno,
                s.entryno,
                CONCAT(COALESCE(s.firstname, ''), ' ', COALESCE(s.othername, '')) AS student_name,
                s.gender,
                p.progname,
                IFNULL(p.levelCode, 3) AS level_code,
                s.has_passed,
                s.fail_reason,
                ROUND(SUM(r.gradept * r.CreditUnits) / NULLIF(SUM(r.CreditUnits), 0), 2) AS sem_gpa,
                SUM(r.score < 50) AS fails,
                MAX(r.is_retake) AS any_retake,
                COUNT(*) AS courses"
                + SR_ResultsFrom(source) +
                @" WHERE s.progid = @programme
                   AND r.studyyear = @studyYear
                   AND r.semester = @semester";

            // Entry year is now an OPTIONAL cohort narrowing (the academic-year sitting is the anchor).
            if (!string.IsNullOrWhiteSpace(entryYear))
                sql += " AND s.entryyear = @entryYear";
            // Academic-year sitting filter (+ optional exclude-promoted). r exposes .acad in every source.
            sql += SR_SittingFilter("r.acad", acadYear, semester, excludePromoted);
            sql += SR_SpecFilter(specialisation);

            List<string> entryParams = new List<string>();
            string[] entries = null;

            if (!string.IsNullOrEmpty(entryNumbers))
            {
                entries = entryNumbers.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (entries.Length > 0)
                {
                    for (int i = 0; i < entries.Length; i++)
                        entryParams.Add("@entry" + i);
                    string inList = string.Join(",", entryParams);
                    sql += " AND (s.regno IN (" + inList + ") OR s.entryno IN (" + inList + "))";
                }
            }

            sql += @" GROUP BY s.regno, s.entryno, s.firstname, s.othername, s.gender, p.progname, p.levelCode, s.has_passed, s.fail_reason
                      ORDER BY sem_gpa DESC";

            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@programme", programme);
                if (!string.IsNullOrWhiteSpace(entryYear))
                    cmd.Parameters.AddWithValue("@entryYear", entryYear);
                cmd.Parameters.AddWithValue("@studyYear", studyYear);
                cmd.Parameters.AddWithValue("@semester", semester);
                SR_BindSitting(cmd, acadYear);
                SR_BindSpec(cmd, specialisation);

                if (!string.IsNullOrEmpty(entryNumbers) && entries != null && entries.Length > 0)
                {
                    for (int i = 0; i < entries.Length; i++)
                        cmd.Parameters.AddWithValue("@entry" + i, entries[i].Trim());
                }

                using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                {
                    adapter.Fill(dt);
                }
            }
        }

        return dt;
    }
    
    private void GeneratePerformanceReportPdf(DataTable data, string programmeName, string entryYear, string studyYear, string semester, string source, int minCourses)
    {
        GeneratePerformanceReportPdf(data, programmeName, entryYear, studyYear, semester, source, minCourses, "");
    }

    private void GeneratePerformanceReportPdf(DataTable data, string programmeName, string entryYear, string studyYear, string semester, string source, int minCourses, string acadYear)
    {
        // Create DevExpress PrintingSystem
        DevExpress.XtraPrinting.PrintingSystem ps = new DevExpress.XtraPrinting.PrintingSystem();

        // Create custom link for PDF content
        DevExpress.XtraPrinting.Link pdfLink = new DevExpress.XtraPrinting.Link(ps);
        pdfLink.CreateDetailArea += (s, args) => {
            GeneratePerformanceReportContent(args.Graph, data, programmeName, entryYear, studyYear, semester, source, minCourses, acadYear);
        };
        
        // Set page settings for A4
        // A4 = 210mm x 297mm = 8.27" x 11.69"
        // Margins in hundredths of an inch: 50 = 0.5 inch
        ps.PageSettings.PaperKind = System.Drawing.Printing.PaperKind.A4;
        ps.PageSettings.Landscape = false;
        ps.PageSettings.LeftMargin = 50;
        ps.PageSettings.RightMargin = 50;
        ps.PageSettings.TopMargin = 50;
        ps.PageSettings.BottomMargin = 50;
        
        pdfLink.CreateDocument();
        
        // Export to PDF
        string fileName = string.Format("PerformanceSummary_{0}_{1}.pdf", entryYear.Replace("/", "-"), DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        
        // Clear all response content and set proper headers for PDF
        Response.Clear();
        Response.Buffer = true;
        Response.ClearHeaders();
        Response.ClearContent();
        Response.ContentType = "application/pdf";
        Response.AddHeader("Content-Disposition", "inline; filename=\"" + fileName + "\"");
        Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
        
        using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
        {
            ps.ExportToPdf(stream);
            byte[] pdfBytes = stream.ToArray();
            Response.AddHeader("Content-Length", pdfBytes.Length.ToString());
            Response.BinaryWrite(pdfBytes);
            Response.Flush();
        }
        
        try
        {
            Response.End();
        }
        catch (System.Threading.ThreadAbortException)
        {
            // Expected exception from Response.End() - ignore
        }
    }
    
    private void GeneratePerformanceReportContent(DevExpress.XtraPrinting.BrickGraphics gr, DataTable data,
        string programmeName, string entryYear, string studyYear, string semester, string source, int minCourses)
    {
        GeneratePerformanceReportContent(gr, data, programmeName, entryYear, studyYear, semester, source, minCourses, "");
    }

    private void GeneratePerformanceReportContent(DevExpress.XtraPrinting.BrickGraphics gr, DataTable data,
        string programmeName, string entryYear, string studyYear, string semester, string source, int minCourses, string acadYear)
    {
        // Get university name and logo
        string universityName = GetUniversityName();
        string logoPath = Server.MapPath("~/COOPERP/images/welcomelogo.png");

        // Academic year of the SITTING (as requested); fall back to the current year for legacy links.
        string academicYear = !string.IsNullOrWhiteSpace(acadYear) ? acadYear : AcademicYearHelper.GetCurrentAcademicYear();
        
        // Colors
        System.Drawing.Color brandColor = System.Drawing.Color.FromArgb(23, 77, 164);
        System.Drawing.Color darkGray = System.Drawing.Color.FromArgb(51, 51, 51);
        System.Drawing.Color lightGray = System.Drawing.Color.FromArgb(102, 102, 102);
        System.Drawing.Color headerBg = System.Drawing.Color.FromArgb(52, 73, 94);
        System.Drawing.Color altRowColor = System.Drawing.Color.FromArgb(248, 248, 248);
        System.Drawing.Color borderColor = System.Drawing.Color.FromArgb(200, 200, 200);
        
        // Category colors
        System.Drawing.Color vcListColor = System.Drawing.Color.FromArgb(26, 84, 144);
        System.Drawing.Color deansListColor = System.Drawing.Color.FromArgb(46, 125, 50);
        System.Drawing.Color secondLowerColor = System.Drawing.Color.FromArgb(245, 124, 0);
        System.Drawing.Color passColor = System.Drawing.Color.FromArgb(198, 40, 40);
        
        // Fonts
        System.Drawing.Font uniNameFont = new System.Drawing.Font("Tahoma", 14, System.Drawing.FontStyle.Bold);
        System.Drawing.Font addressFont = new System.Drawing.Font("Tahoma", 8, System.Drawing.FontStyle.Regular);
        System.Drawing.Font titleFont = new System.Drawing.Font("Tahoma", 10, System.Drawing.FontStyle.Bold);
        System.Drawing.Font normalFont = new System.Drawing.Font("Tahoma", 8, System.Drawing.FontStyle.Regular);
        System.Drawing.Font boldNormalFont = new System.Drawing.Font("Tahoma", 8, System.Drawing.FontStyle.Bold);
        System.Drawing.Font categoryHeaderFont = new System.Drawing.Font("Tahoma", 9, System.Drawing.FontStyle.Bold);
        System.Drawing.Font tableHeaderFont = new System.Drawing.Font("Tahoma", 7, System.Drawing.FontStyle.Bold);
        System.Drawing.Font cellFont = new System.Drawing.Font("Tahoma", 7, System.Drawing.FontStyle.Regular);
        System.Drawing.Font smallFont = new System.Drawing.Font("Tahoma", 7, System.Drawing.FontStyle.Regular);
        System.Drawing.Font italicFont = new System.Drawing.Font("Tahoma", 7, System.Drawing.FontStyle.Italic);
        System.Drawing.Font summaryFont = new System.Drawing.Font("Tahoma", 8, System.Drawing.FontStyle.Bold);
        
        // Actual printable width (page minus margins) so the table fills the whole page
        // instead of the old fixed 555. Columns below are scaled by 'sc' to fill it.
        float pageWidth = gr.ClientPageSize.Width;
        if (pageWidth < 50) pageWidth = 555; // safety fallback
        float pageHeight = gr.ClientPageSize.Height;
        if (pageHeight < 50) pageHeight = 780; // safety fallback (A4 portrait)
        float sc = pageWidth / 555f;
        float y = 0;
        
        // Categorize students. Basis = authoritative cumulative CGPA when available,
        // else the current semester GPA (fresh cohorts with no published history).
        // Students with any failed course / retake are routed to Retake/Referred so a
        // board sees them separately from the honour/class lists.
        List<DataRow> vcListStudents = new List<DataRow>();
        List<DataRow> deansListStudents = new List<DataRow>();
        List<DataRow> secondLowerStudents = new List<DataRow>();
        List<DataRow> passStudents = new List<DataRow>();
        List<DataRow> retakeStudents = new List<DataRow>();
        List<DataRow> failStudents = new List<DataRow>();

        // Minimum courses to pass — supplied on the export form (defaults to 4 server-side). Fewer
        // results than this is a fail reason (incomplete sitting), independent of any curriculum.
        long MIN_PAPERS = minCourses;
        bool hasCourses = data.Columns.Contains("courses");
        bool hasReason = data.Columns.Contains("fail_reason");

        foreach (DataRow row in data.Rows)
        {
            decimal semGpa = data.Columns.Contains("sem_gpa") && row["sem_gpa"] != DBNull.Value ? Convert.ToDecimal(row["sem_gpa"]) : 0;
            decimal basis = semGpa;   // single, per-semester GPA (same value the GPA column & marksheet show)
            long fails = data.Columns.Contains("fails") && row["fails"] != DBNull.Value ? Convert.ToInt64(row["fails"]) : 0;
            long courses = hasCourses && row["courses"] != DBNull.Value ? Convert.ToInt64(row["courses"]) : 0;

            // SIMPLE fail rule (no curriculum): a student fails the semester if they have any F
            // (score < 50) OR sat fewer than MIN_PAPERS papers (level minimum) in the semester.
            bool isFail = fails > 0 || courses < MIN_PAPERS;

            if (isFail)
            {
                // Build a plain, curriculum-free reason for the Fail table.
                var parts = new List<string>();
                if (fails > 0) parts.Add(fails + " failed paper" + (fails == 1 ? "" : "s"));
                if (courses < MIN_PAPERS) parts.Add("only " + courses + " paper" + (courses == 1 ? "" : "s") + " (min " + MIN_PAPERS + ")");
                if (hasReason) row["fail_reason"] = string.Join("; ", parts);
                failStudents.Add(row);
            }
            else
            {
                if (hasReason) row["fail_reason"] = "";   // clear any stale curriculum reason
                bool retake = data.Columns.Contains("any_retake") && row["any_retake"] != DBNull.Value && Convert.ToInt32(row["any_retake"]) == 1;
                if (retake) retakeStudents.Add(row);      // passed but retaking a course → Retake/Referred
                else if (basis >= 4.40m) vcListStudents.Add(row);
                else if (basis >= 3.60m) deansListStudents.Add(row);
                else if (basis >= 2.80m) secondLowerStudents.Add(row);
                else if (basis >= 2.00m) passStudents.Add(row);
                else passStudents.Add(row);   // no F and enough papers → at worst a Pass
            }
        }
        
        // ========== HEADER SECTION - CLEAN LETTERHEAD STYLE ==========

        // University logo (top-left), balanced against the centered letterhead text.
        if (System.IO.File.Exists(logoPath))
        {
            try
            {
                System.Drawing.Image logoImg = System.Drawing.Image.FromFile(logoPath);
                DevExpress.XtraPrinting.ImageBrick logoBrick = new DevExpress.XtraPrinting.ImageBrick();
                logoBrick.Image = logoImg;
                logoBrick.SizeMode = DevExpress.XtraPrinting.ImageSizeMode.ZoomImage;
                logoBrick.Sides = DevExpress.XtraPrinting.BorderSide.None;
                gr.DrawBrick(logoBrick, new System.Drawing.RectangleF(0, y, 92 * sc, 46));
            }
            catch { }
        }

        // University Name - Large, Centered, Bold
        DrawTextLine(gr, universityName, 0, y, pageWidth, 22, uniNameFont, brandColor, System.Drawing.StringAlignment.Center);
        y += 20;
        
        // Address line
        DrawTextLine(gr, "Mengo, Kampala, Uganda | Email: info@mru.ac.ug", 0, y, pageWidth, 12, addressFont, darkGray, System.Drawing.StringAlignment.Center);
        y += 14;
        
        // Programme Year Semester Academic Year - Bold Title Line
        string titleLine = string.Format("{0} YEAR {1} SEMESTER {2} {3}",
            programmeName.ToUpper(),
            !string.IsNullOrEmpty(studyYear) ? studyYear : "ALL",
            !string.IsNullOrEmpty(semester) ? semester : "ALL",
            academicYear);
        DrawTextLine(gr, titleLine, 0, y, pageWidth, 14, titleFont, darkGray, System.Drawing.StringAlignment.Center);
        y += 16;
        
        // Generated timestamp
        DrawTextLine(gr, "Generated: " + DateTime.Now.ToString("dddd, MMMM dd, yyyy - hh:mm tt"),
            0, y, pageWidth, 11, smallFont, lightGray, System.Drawing.StringAlignment.Center);
        y += 14;

        // Horizontal line separator
        DevExpress.XtraPrinting.LineBrick lineBrick1 = new DevExpress.XtraPrinting.LineBrick();
        lineBrick1.ForeColor = brandColor;
        lineBrick1.LineStyle = System.Drawing.Drawing2D.DashStyle.Solid;
        lineBrick1.LineWidth = 2;
        lineBrick1.Sides = DevExpress.XtraPrinting.BorderSide.None;
        gr.DrawBrick(lineBrick1, new System.Drawing.RectangleF(0, y, pageWidth, 2));
        y += 8;
        
        // Introductory paragraph
        string introText = "On recommendation of the Faculty Board, the following results are hereby presented to the Senate Examinations Board for further consideration and onward presentation to the Senate for final approval in the categories indicated below:";
        y = DrawWrappedText(gr, introText, 0, y, pageWidth, normalFont, darkGray);
        y += 8;
        
        // ========== COLUMN WIDTHS - FULL PAGE WIDTH (555) ==========
        // Columns: #, REG NO, STUDENT NAME, GENDER, GPA, STATUS, REASON
        float numWidth = 22 * sc;
        float regNoWidth = 125 * sc;
        float nameWidth = 140 * sc;
        float genderWidth = 42 * sc;
        float cgpaWidth = 66 * sc;   // single per-semester GPA
        float statusWidth = 48 * sc;
        float reasonWidth = pageWidth - numWidth - regNoWidth - nameWidth - genderWidth - cgpaWidth - statusWidth;
        float rowHeight = 20;
        
        // ========== DRAW EACH CATEGORY ==========
        
        // 1. VC's List (First Class)
        y = DrawPerformanceCategory(gr, vcListStudents, "1. VC'S LIST (FIRST CLASS)", vcListStudents.Count,
            "The following students obtained a GPA between 4.40 and 5.00.",
            "No students meet the First Class criteria (GPA 4.40 - 5.00)",
            vcListColor, y, pageWidth, pageHeight, rowHeight, categoryHeaderFont, tableHeaderFont, cellFont, normalFont, italicFont,
            numWidth, regNoWidth, nameWidth, genderWidth, cgpaWidth, statusWidth, reasonWidth,
            headerBg, altRowColor, borderColor);
        y += 10;
        
        // 2. Dean's List (Second Class Upper)
        y = DrawPerformanceCategory(gr, deansListStudents, "2. DEAN'S LIST (SECOND CLASS UPPER DIVISION)", deansListStudents.Count,
            "The following students obtained a GPA between 3.60 and 4.39.",
            "No students meet the Second Class Upper criteria (GPA 3.60 - 4.39)",
            deansListColor, y, pageWidth, pageHeight, rowHeight, categoryHeaderFont, tableHeaderFont, cellFont, normalFont, italicFont,
            numWidth, regNoWidth, nameWidth, genderWidth, cgpaWidth, statusWidth, reasonWidth,
            headerBg, altRowColor, borderColor);
        y += 10;
        
        // 3. Second Class Lower
        y = DrawPerformanceCategory(gr, secondLowerStudents, "3. SECOND CLASS LOWER DIVISION", secondLowerStudents.Count,
            "The following students obtained a GPA between 2.80 and 3.59.",
            "No students in this category",
            secondLowerColor, y, pageWidth, pageHeight, rowHeight, categoryHeaderFont, tableHeaderFont, cellFont, normalFont, italicFont,
            numWidth, regNoWidth, nameWidth, genderWidth, cgpaWidth, statusWidth, reasonWidth,
            headerBg, altRowColor, borderColor);
        y += 10;
        
        // 4. Pass
        y = DrawPerformanceCategory(gr, passStudents, "4. PASS", passStudents.Count,
            "The following students obtained a GPA between 2.00 and 2.79.",
            "No students in this category",
            passColor, y, pageWidth, pageHeight, rowHeight, categoryHeaderFont, tableHeaderFont, cellFont, normalFont, italicFont,
            numWidth, regNoWidth, nameWidth, genderWidth, cgpaWidth, statusWidth, reasonWidth,
            headerBg, altRowColor, borderColor);
        y += 10;

        // 5. Retake / Referred — one or more failed courses or retakes
        y = DrawPerformanceCategory(gr, retakeStudents, "5. RETAKE / REFERRED", retakeStudents.Count,
            "The following students have one or more failed course(s) or retakes and must resit/clear before progressing.",
            "No students with retakes/referrals in this category",
            secondLowerColor, y, pageWidth, pageHeight, rowHeight, categoryHeaderFont, tableHeaderFont, cellFont, normalFont, italicFont,
            numWidth, regNoWidth, nameWidth, genderWidth, cgpaWidth, statusWidth, reasonWidth,
            headerBg, altRowColor, borderColor);
        y += 10;

        // 6. Fail — has one or more F, or sat fewer than 4 papers (curriculum-free rule)
        y = DrawPerformanceCategory(gr, failStudents, "6. FAIL", failStudents.Count,
            "The following students failed the semester: one or more failed paper(s) (score below 50) and/or fewer than 4 papers sat.",
            "No students in this category",
            passColor, y, pageWidth, pageHeight, rowHeight, categoryHeaderFont, tableHeaderFont, cellFont, normalFont, italicFont,
            numWidth, regNoWidth, nameWidth, genderWidth, cgpaWidth, statusWidth, reasonWidth,
            headerBg, altRowColor, borderColor);
        y += 15;

        // ========== OVERALL SUMMARY ==========
        DevExpress.XtraPrinting.LineBrick summaryLine = new DevExpress.XtraPrinting.LineBrick();
        summaryLine.ForeColor = brandColor;
        summaryLine.LineStyle = System.Drawing.Drawing2D.DashStyle.Solid;
        summaryLine.LineWidth = 1;
        summaryLine.Sides = DevExpress.XtraPrinting.BorderSide.None;
        gr.DrawBrick(summaryLine, new System.Drawing.RectangleF(0, y, pageWidth, 1));
        y += 8;
        
        string overallSummary = string.Format("OVERALL: First {0} | 2nd Upper {1} | 2nd Lower {2} | Pass {3} | Retake/Referred {4} | Fail {5} | TOTAL {6}",
            vcListStudents.Count, deansListStudents.Count, secondLowerStudents.Count, passStudents.Count, retakeStudents.Count, failStudents.Count, data.Rows.Count);
        
        DevExpress.XtraPrinting.TextBrick summaryBrick = new DevExpress.XtraPrinting.TextBrick();
        summaryBrick.Text = overallSummary;
        summaryBrick.Font = summaryFont;
        summaryBrick.ForeColor = System.Drawing.Color.White;
        summaryBrick.BackColor = brandColor;
        summaryBrick.Sides = DevExpress.XtraPrinting.BorderSide.None;
        summaryBrick.Padding = new DevExpress.XtraPrinting.PaddingInfo(5, 5, 4, 4);
        summaryBrick.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(System.Drawing.StringAlignment.Center);
        gr.DrawBrick(summaryBrick, new System.Drawing.RectangleF(0, y, pageWidth, 22));
        y += 30;

        // ========== SIGNATURES ==========
        // Four approval signatories, side by side: a signing line on top, full designation below.
        y += 40; // leave room to sign above the lines
        if ((y % pageHeight) + 34 > pageHeight)   // keep the signature row together on one page
            y = ((float)Math.Floor(y / pageHeight) + 1) * pageHeight + 24;

        string[] sigLabels = new string[] { "Head of Department", "Dean", "Academic Registrar", "Deputy Vice Chancellor" };
        float sigColW = pageWidth / 4f;
        float sigGap = 20f; // inset so a line doesn't run into its neighbour
        for (int sgi = 0; sgi < sigLabels.Length; sgi++)
        {
            float colX = sgi * sigColW;

            // Signing line (on top)
            DevExpress.XtraPrinting.LineBrick sigLine = new DevExpress.XtraPrinting.LineBrick();
            sigLine.ForeColor = darkGray;
            sigLine.LineStyle = System.Drawing.Drawing2D.DashStyle.Solid;
            sigLine.LineWidth = 1;
            sigLine.Sides = DevExpress.XtraPrinting.BorderSide.None;
            gr.DrawBrick(sigLine, new System.Drawing.RectangleF(colX + sigGap / 2f, y, sigColW - sigGap, 1));

            // Designation (below the line), centered
            DrawTextLine(gr, sigLabels[sgi], colX, y + 4, sigColW, 14, boldNormalFont, darkGray, System.Drawing.StringAlignment.Center);
        }
        y += 26;

        // ========== FOOTER ==========
        DevExpress.XtraPrinting.LineBrick footerLine = new DevExpress.XtraPrinting.LineBrick();
        footerLine.ForeColor = brandColor;
        footerLine.LineStyle = System.Drawing.Drawing2D.DashStyle.Solid;
        footerLine.LineWidth = 1;
        footerLine.Sides = DevExpress.XtraPrinting.BorderSide.None;
        gr.DrawBrick(footerLine, new System.Drawing.RectangleF(0, y, pageWidth, 1));
        y += 8;
        
        // University name centered
        DrawTextLine(gr, universityName, 0, y, pageWidth, 14, titleFont, brandColor, System.Drawing.StringAlignment.Center);
        y += 16;
        
        // Computer generated notice
        DrawTextLine(gr, "Computer-generated report — valid once signed by the officers above.", 0, y, pageWidth, 12, italicFont, lightGray, System.Drawing.StringAlignment.Center);
        y += 14;
        
        // Copyright
        string copyright = "© " + DateTime.Now.Year + " " + universityName + ". All Rights Reserved.";
        DrawTextLine(gr, copyright, 0, y, pageWidth, 12, smallFont, lightGray, System.Drawing.StringAlignment.Center);
    }
    
    private void DrawTextLine(DevExpress.XtraPrinting.BrickGraphics gr, string text, float x, float y, 
        float width, float height, System.Drawing.Font font, System.Drawing.Color color, System.Drawing.StringAlignment align)
    {
        DevExpress.XtraPrinting.TextBrick brick = new DevExpress.XtraPrinting.TextBrick();
        brick.Text = text;
        brick.Font = font;
        brick.ForeColor = color;
        brick.BackColor = System.Drawing.Color.Transparent;
        brick.Sides = DevExpress.XtraPrinting.BorderSide.None;
        brick.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(align);
        gr.DrawBrick(brick, new System.Drawing.RectangleF(x, y, width, height));
    }
    
    private float DrawWrappedText(DevExpress.XtraPrinting.BrickGraphics gr, string text, float x, float y, 
        float width, System.Drawing.Font font, System.Drawing.Color color)
    {
        DevExpress.XtraPrinting.TextBrick brick = new DevExpress.XtraPrinting.TextBrick();
        brick.Text = text;
        brick.Font = font;
        brick.ForeColor = color;
        brick.BackColor = System.Drawing.Color.Transparent;
        brick.Sides = DevExpress.XtraPrinting.BorderSide.None;
        brick.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(System.Drawing.StringAlignment.Near);
        
        // Estimate height needed for wrapped text (approximately 12 pixels per line, ~80 chars per line)
        int estimatedLines = (int)Math.Ceiling(text.Length / 90.0);
        float height = estimatedLines * 14;
        
        gr.DrawBrick(brick, new System.Drawing.RectangleF(x, y, width, height));
        return y + height;
    }
    
    private float DrawPerformanceCategory(DevExpress.XtraPrinting.BrickGraphics gr, List<DataRow> students,
        string categoryTitle, int studentCount, string description, string emptyMessage,
        System.Drawing.Color categoryColor, float y, float pageWidth, float pageHeight,
        float rowHeight, System.Drawing.Font categoryFont, System.Drawing.Font headerFont, System.Drawing.Font cellFont,
        System.Drawing.Font normalFont, System.Drawing.Font italicFont,
        float numWidth, float regNoWidth, float nameWidth, float genderWidth, float cgpaWidth, float statusWidth, float reasonWidth,
        System.Drawing.Color headerBg, System.Drawing.Color altRowColor, System.Drawing.Color borderColor)
    {
        // Keep a non-empty category's heading with its first rows: start a new page if there isn't
        // room for the category bar + description + table header + a couple of rows.
        if (students.Count > 0 && (y % pageHeight) + 90 > pageHeight)
            y = ((float)Math.Floor(y / pageHeight) + 1) * pageHeight;

        // Category header: "1. VC'S LIST (FIRST CLASS) 0 STUDENTS"
        string headerText = string.Format("{0}   {1} STUDENTS", categoryTitle, studentCount);
        DevExpress.XtraPrinting.TextBrick categoryBrick = new DevExpress.XtraPrinting.TextBrick();
        categoryBrick.Text = headerText;
        categoryBrick.Font = categoryFont;
        categoryBrick.ForeColor = System.Drawing.Color.White;
        categoryBrick.BackColor = categoryColor;
        categoryBrick.Sides = DevExpress.XtraPrinting.BorderSide.None;
        categoryBrick.Padding = new DevExpress.XtraPrinting.PaddingInfo(5, 5, 3, 3);
        categoryBrick.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(System.Drawing.StringAlignment.Near);
        gr.DrawBrick(categoryBrick, new System.Drawing.RectangleF(0, y, pageWidth, 20));
        y += 22;
        
        // Description line
        DrawTextLine(gr, description, 0, y, pageWidth, 14, normalFont, System.Drawing.Color.FromArgb(51, 51, 51), System.Drawing.StringAlignment.Near);
        y += 16;
        
        if (students.Count == 0)
        {
            // Empty message in italics
            DrawTextLine(gr, emptyMessage, 0, y, pageWidth, 14, italicFont, System.Drawing.Color.Gray, System.Drawing.StringAlignment.Near);
            y += 16;
        }
        else
        {
            // Table header — reusable so it REPEATS at the top of each page the table spills onto.
            float x = 0;
            Action drawHdr = () =>
            {
                x = 0;
                DrawTableHeaderCell(gr, "#", x, y, numWidth, rowHeight, headerFont, headerBg, borderColor); x += numWidth;
                DrawTableHeaderCell(gr, "REG NO", x, y, regNoWidth, rowHeight, headerFont, headerBg, borderColor); x += regNoWidth;
                DrawTableHeaderCell(gr, "STUDENT NAME", x, y, nameWidth, rowHeight, headerFont, headerBg, borderColor); x += nameWidth;
                DrawTableHeaderCell(gr, "GENDER", x, y, genderWidth, rowHeight, headerFont, headerBg, borderColor); x += genderWidth;
                DrawTableHeaderCell(gr, "GPA", x, y, cgpaWidth, rowHeight, headerFont, headerBg, borderColor); x += cgpaWidth;
                DrawTableHeaderCell(gr, "STATUS", x, y, statusWidth, rowHeight, headerFont, headerBg, borderColor); x += statusWidth;
                DrawTableHeaderCell(gr, "REASON", x, y, reasonWidth, rowHeight, headerFont, headerBg, borderColor);
                y += rowHeight;
            };
            drawHdr();

            // Data rows
            int rowNum = 1;
            foreach (DataRow row in students)
            {
                // Repeat the header on a new page when the row would overflow the current one.
                if ((y % pageHeight) + rowHeight > pageHeight - 4)
                {
                    y = ((float)Math.Floor(y / pageHeight) + 1) * pageHeight;
                    drawHdr();
                }
                x = 0;
                System.Drawing.Color rowBg = rowNum % 2 == 0 ? altRowColor : System.Drawing.Color.White;
                
                DrawTableDataCell(gr, rowNum.ToString(), x, y, numWidth, rowHeight, cellFont, rowBg, borderColor, System.Drawing.StringAlignment.Center); x += numWidth;
                string regNoText = row.Table.Columns.Contains("regno") && row["regno"] != DBNull.Value && row["regno"].ToString().Trim() != ""
                    ? row["regno"].ToString() : row["entryno"].ToString();
                DrawTableDataCell(gr, regNoText, x, y, regNoWidth, rowHeight, cellFont, rowBg, borderColor, System.Drawing.StringAlignment.Near); x += regNoWidth;
                DrawTableDataCell(gr, row["student_name"].ToString(), x, y, nameWidth, rowHeight, cellFont, rowBg, borderColor, System.Drawing.StringAlignment.Near); x += nameWidth;
                DrawTableDataCell(gr, row["gender"].ToString(), x, y, genderWidth, rowHeight, cellFont, rowBg, borderColor, System.Drawing.StringAlignment.Center); x += genderWidth;

                // GPA cell: the single per-semester GPA (no cumulative).
                decimal semGpaVal = row.Table.Columns.Contains("sem_gpa") && row["sem_gpa"] != DBNull.Value ? Convert.ToDecimal(row["sem_gpa"]) : 0;
                string gpaText = semGpaVal.ToString("F2");
                DrawTableDataCell(gr, gpaText, x, y, cgpaWidth, rowHeight, cellFont, rowBg, borderColor, System.Drawing.StringAlignment.Center); x += cgpaWidth;

                // Status column — derived from the SAME curriculum-free rule as the classification:
                // a row is FAIL only when it carries a fail reason (set for the Fail category), else PASS.
                string failReasonVal = row.Table.Columns.Contains("fail_reason") && row["fail_reason"] != DBNull.Value ? row["fail_reason"].ToString().Trim() : "";
                bool hasPassed = failReasonVal == "";
                string statusText = hasPassed ? "PASS" : "FAIL";
                System.Drawing.Color statusBgColor = hasPassed ? System.Drawing.Color.FromArgb(200, 230, 200) : System.Drawing.Color.FromArgb(255, 200, 200);
                System.Drawing.Color statusTextColor = hasPassed ? System.Drawing.Color.FromArgb(34, 139, 34) : System.Drawing.Color.FromArgb(178, 34, 34);
                DrawStatusCell(gr, statusText, x, y, statusWidth, rowHeight, cellFont, statusBgColor, statusTextColor, borderColor); x += statusWidth;
                
                // Reason column
                string failReason = row["fail_reason"] != DBNull.Value ? row["fail_reason"].ToString() : "";
                DrawTableDataCell(gr, failReason, x, y, reasonWidth, rowHeight, cellFont, rowBg, borderColor, System.Drawing.StringAlignment.Near);
                
                y += rowHeight;
                rowNum++;
            }
        }
        
        return y;
    }
    
    private void DrawStatusCell(DevExpress.XtraPrinting.BrickGraphics gr, string text, float x, float y, 
        float width, float height, System.Drawing.Font font, System.Drawing.Color bgColor, System.Drawing.Color textColor, System.Drawing.Color borderColor)
    {
        DevExpress.XtraPrinting.TextBrick brick = new DevExpress.XtraPrinting.TextBrick();
        brick.Text = text;
        brick.Font = font;
        brick.ForeColor = textColor;
        brick.BackColor = bgColor;
        brick.Sides = DevExpress.XtraPrinting.BorderSide.All;
        brick.BorderColor = borderColor;
        brick.BorderWidth = 1;
        brick.Padding = new DevExpress.XtraPrinting.PaddingInfo(3, 3, 4, 4);
        brick.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(System.Drawing.StringAlignment.Center);
        gr.DrawBrick(brick, new System.Drawing.RectangleF(x, y, width, height));
    }
    
    private void DrawTableHeaderCell(DevExpress.XtraPrinting.BrickGraphics gr, string text, float x, float y, 
        float width, float height, System.Drawing.Font font, System.Drawing.Color bgColor, System.Drawing.Color borderColor)
    {
        DevExpress.XtraPrinting.TextBrick brick = new DevExpress.XtraPrinting.TextBrick();
        brick.Text = text;
        brick.Font = font;
        brick.ForeColor = System.Drawing.Color.White;
        brick.BackColor = bgColor;
        brick.Sides = DevExpress.XtraPrinting.BorderSide.All;
        brick.BorderColor = System.Drawing.Color.FromArgb(70, 90, 110);
        brick.BorderWidth = 1;
        brick.Padding = new DevExpress.XtraPrinting.PaddingInfo(3, 3, 5, 5);
        brick.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(System.Drawing.StringAlignment.Center);
        gr.DrawBrick(brick, new System.Drawing.RectangleF(x, y, width, height));
    }
    
    private void DrawTableDataCell(DevExpress.XtraPrinting.BrickGraphics gr, string text, float x, float y, 
        float width, float height, System.Drawing.Font font, System.Drawing.Color bgColor, System.Drawing.Color borderColor, System.Drawing.StringAlignment align)
    {
        DevExpress.XtraPrinting.TextBrick brick = new DevExpress.XtraPrinting.TextBrick();
        brick.Text = text;
        brick.Font = font;
        brick.ForeColor = System.Drawing.Color.FromArgb(51, 51, 51);
        brick.BackColor = bgColor;
        brick.Sides = DevExpress.XtraPrinting.BorderSide.All;
        brick.BorderColor = borderColor;
        brick.BorderWidth = 1;
        brick.Padding = new DevExpress.XtraPrinting.PaddingInfo(3, 3, 4, 4);
        brick.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(align);
        gr.DrawBrick(brick, new System.Drawing.RectangleF(x, y, width, height));
    }

    /// <summary>
    /// Gets detailed student and results data for the summary report.
    /// </summary>
    private DataTable GetSummaryReportData(string programme, string entryYear, string studyYear, string semester, string entryNumbers)
    {
        return GetSummaryReportData(programme, entryYear, studyYear, semester, entryNumbers, "published", "", false);
    }

    private DataTable GetSummaryReportData(string programme, string entryYear, string studyYear, string semester, string entryNumbers, string source)
    {
        return GetSummaryReportData(programme, entryYear, studyYear, semester, entryNumbers, source, "", false);
    }

    private DataTable GetSummaryReportData(string programme, string entryYear, string studyYear, string semester, string entryNumbers, string source, string acadYear, bool excludePromoted)
    {
        return GetSummaryReportData(programme, entryYear, studyYear, semester, entryNumbers, source, acadYear, excludePromoted, "");
    }

    private DataTable GetSummaryReportData(string programme, string entryYear, string studyYear, string semester, string entryNumbers, string source, string acadYear, bool excludePromoted, string specialisation)
    {
        // Published: sourced from acad_results, ENRICHED with CW/Exam/Total + status from the
        // portal provisional ledger (with a transparent fallback if that cross-DB join is
        // unavailable). Staged (approved/captured/entered): sourced directly from the ledger at
        // that mark_stage, with grade/GP/CU derived on the fly (NCHE 2015).
        Exception lastEx = null;
        foreach (bool includeProvisional in new[] { true, false })
        {
            try { return BuildSummaryReportTable(programme, entryYear, studyYear, semester, entryNumbers, includeProvisional, source, acadYear, excludePromoted, specialisation); }
            catch (Exception ex) { lastEx = ex; }
        }
        throw lastEx;
    }

    private DataTable BuildSummaryReportTable(string programme, string entryYear, string studyYear, string semester, string entryNumbers, bool includeProvisional, string source)
    {
        return BuildSummaryReportTable(programme, entryYear, studyYear, semester, entryNumbers, includeProvisional, source, "", false);
    }

    private DataTable BuildSummaryReportTable(string programme, string entryYear, string studyYear, string semester, string entryNumbers, bool includeProvisional, string source, string acadYear, bool excludePromoted)
    {
        return BuildSummaryReportTable(programme, entryYear, studyYear, semester, entryNumbers, includeProvisional, source, acadYear, excludePromoted, "");
    }

    private DataTable BuildSummaryReportTable(string programme, string entryYear, string studyYear, string semester, string entryNumbers, bool includeProvisional, string source, string acadYear, bool excludePromoted, string specialisation)
    {
        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();

            bool unpub = !SR_IsPublished(source);

            // CW/Exam/Total + status: for published these come from the portal ledger via a
            // LEFT JOIN (cr2); for a staged source they ARE the source, exposed by SR_ResultsFrom.
            string provSelect;
            string fromClause;
            if (unpub)
            {
                provSelect = @", r.cw_marks AS cw_marks, r.exam_marks AS exam_marks,
                                 r.prov_total AS prov_total, r.sub_status AS sub_status ";
                fromClause = SR_ResultsFrom(source) +
                             " LEFT JOIN acad_specialisation sp ON s.specialisation = sp.spec_id ";
            }
            else
            {
                provSelect = includeProvisional
                    ? @", cr2.provisional_course_work_marks AS cw_marks,
                          cr2.provisional_exam_marks        AS exam_marks,
                          cr2.provisional_total_marks       AS prov_total,
                          cr2.provisional_marks_status      AS sub_status "
                    : "";
                string provJoin = includeProvisional
                    ? @" LEFT JOIN campus_dynamics_portal.acad_course_registration cr2
                           ON cr2.regno = r.regno AND cr2.courseID = r.courseid
                          AND cr2.acad_year = r.acad AND cr2.semester = r.semester "
                    : "";
                fromClause =
                    @" FROM acad_student s
                       INNER JOIN acad_results r ON s.regno = r.regno
                       LEFT JOIN acad_programme p ON s.progid = p.progcode
                       LEFT JOIN acad_course c ON r.courseid = c.courseID
                       LEFT JOIN acad_specialisation sp ON s.specialisation = sp.spec_id" + provJoin;
            }

            // Query to get student details and their results with specialization
            string sql = @"SELECT
                            s.regno,
                            s.entryno,
                            CONCAT(COALESCE(s.firstname, ''), ' ', COALESCE(s.othername, '')) AS student_name,
                            s.firstname,
                            s.othername,
                            s.entryyear,
                            s.progid,
                            p.progname,
                            IFNULL(p.levelCode, 3) AS level_code,
                            ROUND(acad_CGPAFinder(s.regno), 2) AS cgpa,
                            s.specialisation AS spec_id,
                            CASE WHEN sp.spec IS NULL OR TRIM(sp.spec) = '' OR TRIM(sp.spec) = '-' THEN 'General' ELSE TRIM(sp.spec) END AS specialization,
                            r.courseid,
                            c.courseName AS course_title,
                            r.semester AS result_semester,
                            r.acad AS academic_year,
                            r.studyyear,
                            r.score,
                            r.grade,
                            r.gradept,
                            r.CreditUnits,
                            r.gpa" + provSelect + fromClause + @"
                           WHERE 1=1";

            List<string> entryParams = new List<string>();
            string[] entries = null;

            // If entry numbers specified, use them
            if (!string.IsNullOrEmpty(entryNumbers))
            {
                entries = entryNumbers.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (entries.Length > 0)
                {
                    for (int i = 0; i < entries.Length; i++)
                        entryParams.Add("@entry" + i);
                    string inList = string.Join(",", entryParams);
                    sql += " AND (s.regno IN (" + inList + ") OR s.entryno IN (" + inList + "))";
                }
            }

            // Programme + year-of-study + semester scope the sitting; entry year is optional; the
            // academic-year filter pins it to one sitting (was missing — the cause of cross-year pooling).
            if (!string.IsNullOrEmpty(programme))
                sql += " AND s.progid = @programme";
            if (!string.IsNullOrEmpty(entryYear))
                sql += " AND s.entryyear = @entryYear";
            if (!string.IsNullOrEmpty(studyYear))
                sql += " AND r.studyyear = @studyYear";
            if (!string.IsNullOrEmpty(semester))
                sql += " AND r.semester = @semester";
            sql += SR_SittingFilter("r.acad", acadYear, semester, excludePromoted);
            sql += SR_SpecFilter(specialisation);

            sql += " ORDER BY s.entryno, r.semester, r.courseid";

            DataTable dt = new DataTable();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (!string.IsNullOrEmpty(entryNumbers) && entries != null && entries.Length > 0)
                {
                    for (int i = 0; i < entries.Length; i++)
                        cmd.Parameters.AddWithValue("@entry" + i, entries[i].Trim());
                }

                if (!string.IsNullOrEmpty(programme))
                    cmd.Parameters.AddWithValue("@programme", programme);
                if (!string.IsNullOrEmpty(entryYear))
                    cmd.Parameters.AddWithValue("@entryYear", entryYear);
                if (!string.IsNullOrEmpty(studyYear))
                    cmd.Parameters.AddWithValue("@studyYear", studyYear);
                if (!string.IsNullOrEmpty(semester))
                    cmd.Parameters.AddWithValue("@semester", semester);
                SR_BindSitting(cmd, acadYear);
                SR_BindSpec(cmd, specialisation);

                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd))
                {
                    da.Fill(dt);
                }
            }

            return dt;
        }
    }

    /// <summary>
    /// Maps a new-system submission status to a soft cell-tint colour for the results grid.
    /// Returns the supplied fallback when there is no provisional record.
    /// </summary>
    private System.Drawing.Color GetSubmissionTint(string status, System.Drawing.Color fallback)
    {
        switch ((status ?? "").Trim().ToLowerInvariant())
        {
            case "published": return System.Drawing.Color.FromArgb(223, 246, 230); // soft green
            case "approved":  return System.Drawing.Color.FromArgb(224, 239, 255); // soft blue
            case "pending":   return System.Drawing.Color.FromArgb(255, 247, 214); // soft amber
            case "rejected":  return System.Drawing.Color.FromArgb(252, 226, 226); // soft red
            default:          return fallback;
        }
    }
    
    /// <summary>
    /// Gets the university name from acad_university table.
    /// </summary>
    private string GetUniversityName()
    {
        string universityName = "UNIVERSITY"; // Default fallback
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand("SELECT university_name FROM acad_university LIMIT 1", conn))
                {
                    object result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        universityName = result.ToString().ToUpper();
                    }
                }
            }
        }
        catch { }
        return universityName;
    }
    
    /// <summary>
    /// Gets the current academic year - centralised in AcademicYearHelper.
    /// </summary>
    
    /// <summary>
    /// Normalizes a specialization name so that equivalent text variants group together.
    /// Handles: case, separators (& → AND), whitespace, and parenthetical spacing.
    /// IMPORTANT: Subject order is preserved - "GEOGRAPHY AND HISTORY" is a different
    /// specialization from "HISTORY AND GEOGRAPHY" (first subject = primary/major).
    /// </summary>
    private string NormalizeSpecializationName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Trim().Length == 0)
            return "GENERAL";
        
        string n = name.Trim().ToUpper();
        
        // Treat dash, "0", "GENERAL", or any single non-letter character as no-specialization
        if (n == "GENERAL" || n == "-" || n == "0" || n == "N/A" || n == "NONE")
            return "GENERAL";
        
        // Replace & with AND (the canonical separator)
        n = n.Replace("&", " AND ");
        
        // Normalize spacing before parentheses: "RELIGION(" → "RELIGION ("
        n = n.Replace("(", " (");
        
        // Collapse multiple spaces to single space
        while (n.Contains("  "))
            n = n.Replace("  ", " ");
        
        n = n.Trim();
        
        // Trim individual parts but do NOT reorder - subject order is significant
        string[] parts = n.Split(new string[] { " AND " }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
            parts[i] = parts[i].Trim();
        
        return string.Join(" AND ", parts);
    }
    
    /// <summary>
    /// Gets curriculum validation info for a specialization, study year, and semester.
    /// Returns: spec_id, curriculum_course_count, is_fully_set, programme default spec_id if applicable
    /// </summary>
    private class CurriculumInfo
    {
        public string SpecId { get; set; }
        public int CurriculumCourseCount { get; set; }
        public bool IsFullySet { get; set; }
        public bool IsDefault { get; set; }
        public string SpecName { get; set; }
    }
    
    private CurriculumInfo GetCurriculumInfo(string specId, string progId, string studyYear, string semester)
    {
        CurriculumInfo info = new CurriculumInfo
        {
            SpecId = specId,
            CurriculumCourseCount = 0,
            IsFullySet = false,
            IsDefault = false,
            SpecName = ""
        };
        
        try
        {
            // Normalise specId: treat "-", "0", whitespace-only as empty (unassigned)
            string cleanSpecId = (specId ?? "").Trim();
            bool needDefault = string.IsNullOrEmpty(cleanSpecId) || cleanSpecId == "0" || cleanSpecId == "-";
            
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                
                if (needDefault)
                {
                    // Strategy 1: Find specialisation marked as default
                    string defaultSpecSql = @"SELECT spec_id, spec, COALESCE(is_fully_set, 'No') AS is_fully_set 
                                              FROM acad_specialisation 
                                              WHERE prog_id = @progId AND is_default = 'Yes' 
                                              LIMIT 1";
                    using (MySqlCommand cmd = new MySqlCommand(defaultSpecSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@progId", progId);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                info.SpecId = reader["spec_id"].ToString().Trim();
                                info.SpecName = reader["spec"] != DBNull.Value ? reader["spec"].ToString().Trim() : "";
                                info.IsFullySet = reader["is_fully_set"] != DBNull.Value 
                                    && reader["is_fully_set"].ToString().Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase);
                                info.IsDefault = true;
                            }
                        }
                    }
                    
                    // Strategy 2: Find specialisation named 'Default'
                    if (string.IsNullOrEmpty(info.SpecId) || info.SpecId == "0")
                    {
                        string namedDefaultSql = @"SELECT spec_id, spec, COALESCE(is_fully_set, 'No') AS is_fully_set 
                                                   FROM acad_specialisation 
                                                   WHERE prog_id = @progId AND (spec = 'Default' OR spec LIKE '%Default%')
                                                   ORDER BY spec_id LIMIT 1";
                        using (MySqlCommand cmd = new MySqlCommand(namedDefaultSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@progId", progId);
                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    info.SpecId = reader["spec_id"].ToString().Trim();
                                    info.SpecName = reader["spec"] != DBNull.Value ? reader["spec"].ToString().Trim() : "";
                                    info.IsFullySet = reader["is_fully_set"] != DBNull.Value 
                                        && reader["is_fully_set"].ToString().Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase);
                                    info.IsDefault = true;
                                }
                            }
                        }
                    }
                    
                    // Strategy 3: Fall back to the first specialisation for this programme
                    if (string.IsNullOrEmpty(info.SpecId) || info.SpecId == "0")
                    {
                        string firstSpecSql = @"SELECT spec_id, spec, COALESCE(is_fully_set, 'No') AS is_fully_set 
                                                FROM acad_specialisation 
                                                WHERE prog_id = @progId 
                                                ORDER BY spec_id LIMIT 1";
                        using (MySqlCommand cmd = new MySqlCommand(firstSpecSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@progId", progId);
                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    info.SpecId = reader["spec_id"].ToString().Trim();
                                    info.SpecName = reader["spec"] != DBNull.Value ? reader["spec"].ToString().Trim() : "";
                                    info.IsFullySet = reader["is_fully_set"] != DBNull.Value 
                                        && reader["is_fully_set"].ToString().Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase);
                                    info.IsDefault = true;
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Student has a valid spec_id - look it up directly
                    string specSql = @"SELECT spec, COALESCE(is_fully_set, 'No') AS is_fully_set 
                                       FROM acad_specialisation WHERE spec_id = @specId";
                    using (MySqlCommand cmd = new MySqlCommand(specSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@specId", cleanSpecId);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                info.SpecName = reader["spec"] != DBNull.Value ? reader["spec"].ToString().Trim() : "";
                                info.IsFullySet = reader["is_fully_set"] != DBNull.Value 
                                    && reader["is_fully_set"].ToString().Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase);
                            }
                        }
                    }
                    info.SpecId = cleanSpecId;
                }
                
                // Get curriculum course count for this spec + study_year + semester
                if (!string.IsNullOrEmpty(info.SpecId) && info.SpecId != "0")
                {
                    string countSql = @"SELECT COUNT(*) FROM acad_programmecourses 
                                        WHERE specialisation_id = @specId 
                                        AND study_year = @studyYear 
                                        AND semester = @semester";
                    using (MySqlCommand cmd = new MySqlCommand(countSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@specId", info.SpecId);
                        cmd.Parameters.AddWithValue("@studyYear", studyYear);
                        cmd.Parameters.AddWithValue("@semester", semester);
                        object result = cmd.ExecuteScalar();
                        info.CurriculumCourseCount = result != null ? Convert.ToInt32(result) : 0;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("GetCurriculumInfo error: " + ex.Message);
        }
        
        return info;
    }
    
    /// <summary>
    /// Generates a PDF summary report grouped by specialization with courses as columns.
    /// </summary>
    // The "minimum courses to pass" figure the operator supplies on the export form. When absent or
    // invalid the exports default to 4. (The modal pre-fills 5.)
    private static int ParseMinCourses(string raw)
    {
        int n;
        if (int.TryParse((raw ?? "").Trim(), out n) && n >= 1 && n <= 30) return n;
        return 4;
    }

    /// <summary>
    /// Checks every course code the report is about to print against the approved curriculum for
    /// this programme, year and semester, and returns one line per problem found. The report is
    /// still produced — refusing to print would help nobody at results time — but it now carries
    /// the warnings on its face, so nothing is published on the quiet assumption that the codes
    /// are sound.
    ///
    /// A course code is a key. These checks exist because the same subject was being carried under
    /// two codes, a Semester 1 code was appearing in a Semester 2 report, and codes differing only
    /// by a space were splitting one cohort in two.
    /// </summary>
    private List<string> BuildCourseValidationIssues(DataTable data, string studyYear, string semester)
    {
        var issues = new List<string>();
        try
        {
            string prog = "";
            var codes = new List<string>();
            // Case-insensitive, because the course-code columns are: a course written two ways is
            // one course, and listing it twice would report the same issue against it twice.
            var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var namesByCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in data.Rows)
            {
                if (prog == "" && data.Columns.Contains("progid") && r["progid"] != DBNull.Value)
                    prog = Convert.ToString(r["progid"]).Trim();
                if (r["courseid"] == DBNull.Value) continue;
                string code = Convert.ToString(r["courseid"]).Trim();
                if (code == "" || !seenCodes.Add(code)) continue;
                codes.Add(code);
                namesByCode[code] = data.Columns.Contains("course_title") && r["course_title"] != DBNull.Value
                    ? Convert.ToString(r["course_title"]).Trim() : "";
            }
            if (codes.Count == 0) return issues;

            int yr, sem;
            int.TryParse((studyYear ?? "").Trim(), out yr);
            int.TryParse((semester ?? "").Trim(), out sem);

            var approved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                if (prog != "" && yr > 0 && sem > 0)
                    using (MySqlCommand cmd = new MySqlCommand(
                        "SELECT DISTINCT course_code FROM acad_programmecourses WHERE progcode=@p AND study_year=@y AND semester=@s", conn))
                    {
                        cmd.Parameters.AddWithValue("@p", prog);
                        cmd.Parameters.AddWithValue("@y", yr);
                        cmd.Parameters.AddWithValue("@s", sem);
                        using (MySqlDataReader rd = cmd.ExecuteReader())
                            while (rd.Read()) approved.Add(Convert.ToString(rd[0]).Trim());
                    }
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT courseID, IFNULL(course_state,'ACTIVE'), IFNULL(merged_into,'') FROM acad_course", conn))
                using (MySqlDataReader rd = cmd.ExecuteReader())
                    while (rd.Read())
                    {
                        string cid = Convert.ToString(rd[0]).Trim();
                        known.Add(cid);
                        string st = Convert.ToString(rd[1]).Trim();
                        if (st != "ACTIVE") merged[cid] = st + (Convert.ToString(rd[2]).Trim() != "" ? " into " + Convert.ToString(rd[2]).Trim() : "");
                    }
            }

            // Expected level digits for this sitting, e.g. Year 2 Semester 2 -> "22".
            string expect = (yr > 0 && sem > 0) ? yr.ToString() + sem.ToString() : "";
            foreach (string code in codes)
            {
                if (code.IndexOf(' ') >= 0)
                    issues.Add(code + " — code contains a space. The same course also exists without it, which splits one cohort across two codes.");
                if (!known.Contains(code))
                    issues.Add(code + " — no record in the course master, so its name and credit unit cannot be resolved.");
                string state;
                if (merged.TryGetValue(code, out state))
                    issues.Add(code + " — the course master marks this code " + state + "; results should not still be recorded against it.");
                if (expect != "")
                {
                    var m = System.Text.RegularExpressions.Regex.Match(code, @"^[A-Za-z]+(\d{2})");
                    if (m.Success && m.Groups[1].Value != expect)
                        issues.Add(code + " — code says year/semester " + m.Groups[1].Value[0] + "/" + m.Groups[1].Value[1] +
                                   ", but this is a Year " + yr + " Semester " + sem + " report.");
                }
                if (approved.Count > 0 && !approved.Contains(code))
                    issues.Add(code + " — not in the approved curriculum for this programme, year and semester.");
            }

            // Two codes, one subject: the split this whole exercise is about.
            var byName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in namesByCode)
            {
                string key = System.Text.RegularExpressions.Regex.Replace((kv.Value ?? "").ToUpperInvariant(), @"\s+", "");
                if (key == "") continue;
                if (!byName.ContainsKey(key)) byName[key] = new List<string>();
                byName[key].Add(kv.Key);
            }
            foreach (var kv in byName)
                if (kv.Value.Count > 1)
                    issues.Add(string.Join(" + ", kv.Value.ToArray()) + " — same course name under more than one code. Verify whether these are genuinely different papers.");

            issues.Sort(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) { issues.Add("Course validation could not run: " + ex.Message); }
        return issues;
    }

    private void GenerateSummaryReportPdf(DataTable data, string studyYear, string semester, string entryYear, int minCourses)
    {
        GenerateSummaryReportPdf(data, studyYear, semester, entryYear, minCourses, "");
    }

    private void GenerateSummaryReportPdf(DataTable data, string studyYear, string semester, string entryYear, int minCourses, string acadYear)
    {
        string universityName = GetUniversityName();
        string progName = data.Rows.Count > 0 && data.Rows[0]["progname"] != DBNull.Value 
            ? data.Rows[0]["progname"].ToString() : "";
        
        // Get logo path
        string logoPath = Server.MapPath("~/COOPERP/images/welcomelogo.png");
        
        // Group by spec_id (the database primary key) to ensure each specialization
        // is displayed EXACTLY as it exists in acad_specialisation - no fabrication, no merging.
        // Students with no specialization (null/0/-/empty) group together under key "0".
        var specializationGroups = data.AsEnumerable()
            .GroupBy(r => {
                string sid = r["spec_id"] != DBNull.Value ? r["spec_id"].ToString().Trim() : "0";
                if (string.IsNullOrEmpty(sid) || sid == "-") sid = "0";
                return sid;
            })
            .OrderBy(g => g.Key == "0" ? 1 : 0)  // No-specialization group goes LAST
            .ThenBy(g => {
                // Sort named groups by their display name from the DB
                string raw = g.First()["specialization"] != DBNull.Value ? g.First()["specialization"].ToString().Trim() : "";
                return raw;
            })
            .ToList();

        // Whether the new marks-submission fields (CW/Exam/status) were successfully joined in.
        bool hasProvisional = data.Columns.Contains("sub_status");

        using (DevExpress.XtraPrinting.PrintingSystem ps = new DevExpress.XtraPrinting.PrintingSystem())
        {
            DevExpress.XtraPrinting.Link link = new DevExpress.XtraPrinting.Link(ps);
            
            // Set paper to A4 Landscape with minimal margins
            link.PaperKind = System.Drawing.Printing.PaperKind.A4;
            link.Landscape = true;
            link.Margins = new System.Drawing.Printing.Margins(10, 10, 10, 10);
            
            link.CreateDetailArea += (sender, e) => {
                DevExpress.XtraPrinting.BrickGraphics gr = e.Graph;
                
                // Fonts - reduced sizes
                System.Drawing.Font uniNameFont = new System.Drawing.Font("Tahoma", 12, System.Drawing.FontStyle.Bold);
                System.Drawing.Font reportTitleFont = new System.Drawing.Font("Tahoma", 10, System.Drawing.FontStyle.Bold);
                System.Drawing.Font progNameFont = new System.Drawing.Font("Tahoma", 9, System.Drawing.FontStyle.Bold);
                System.Drawing.Font filterLabelFont = new System.Drawing.Font("Tahoma", 7, System.Drawing.FontStyle.Bold);
                System.Drawing.Font filterValueFont = new System.Drawing.Font("Tahoma", 7, System.Drawing.FontStyle.Regular);
                System.Drawing.Font subtitleFont = new System.Drawing.Font("Tahoma", 8, System.Drawing.FontStyle.Bold);
                System.Drawing.Font headerFont = new System.Drawing.Font("Tahoma", 6, System.Drawing.FontStyle.Bold);
                System.Drawing.Font cellFont = new System.Drawing.Font("Tahoma", 6, System.Drawing.FontStyle.Regular);
                System.Drawing.Font smallFont = new System.Drawing.Font("Tahoma", 7, System.Drawing.FontStyle.Regular);
                System.Drawing.Font dateFont = new System.Drawing.Font("Tahoma", 7, System.Drawing.FontStyle.Regular);
                
                // Colors
                System.Drawing.Color brandColor = System.Drawing.Color.FromArgb(23, 77, 164); // University blue
                System.Drawing.Color darkGray = System.Drawing.Color.FromArgb(51, 51, 51);
                System.Drawing.Color lightGray = System.Drawing.Color.FromArgb(102, 102, 102);
                System.Drawing.Color filterBgColor = System.Drawing.Color.FromArgb(248, 249, 250);
                System.Drawing.Color filterBorderColor = System.Drawing.Color.FromArgb(222, 226, 230);
                System.Drawing.Color headerBg = System.Drawing.Color.FromArgb(52, 73, 94);
                System.Drawing.Color specHeaderBg = System.Drawing.Color.FromArgb(44, 62, 80);
                System.Drawing.Color altRowColor = System.Drawing.Color.FromArgb(245, 245, 245);
                System.Drawing.Color borderColor = System.Drawing.Color.FromArgb(150, 150, 150);
                System.Drawing.Color lineColor = System.Drawing.Color.FromArgb(23, 77, 164);

                // Minimum courses to pass — supplied on the export form (defaults to 4 server-side).
                // 'minCourses' is a method parameter; used directly by the STATUS rule below.

                float y = 0;
                // Use the ACTUAL printable width (page minus margins, in DevExpress document
                // units) so the content fills the whole landscape page instead of the old
                // hardcoded 822 (which assumed point units and left ~30% blank on the right).
                float pageWidth = gr.ClientPageSize.Width;
                if (pageWidth < 50) pageWidth = 822; // safety fallback
                // Printable page height — used to repeat table headers when a table spills to a new page.
                float pageHeight = gr.ClientPageSize.Height;
                if (pageHeight < 50) pageHeight = 560; // safety fallback (A4 landscape)
                float logoWidth = 120;
                float logoHeight = 40;
                
                // ========== PROFESSIONAL REPORT HEADER ==========
                
                // Logo on the left
                if (System.IO.File.Exists(logoPath))
                {
                    try
                    {
                        System.Drawing.Image logoImg = System.Drawing.Image.FromFile(logoPath);
                        DevExpress.XtraPrinting.ImageBrick logoBrick = new DevExpress.XtraPrinting.ImageBrick();
                        logoBrick.Image = logoImg;
                        logoBrick.SizeMode = DevExpress.XtraPrinting.ImageSizeMode.ZoomImage;
                        logoBrick.Sides = DevExpress.XtraPrinting.BorderSide.None;
                        gr.DrawBrick(logoBrick, new System.Drawing.RectangleF(5, y + 5, logoWidth, logoHeight));
                    }
                    catch { }
                }
                
                // University Name - centered
                DevExpress.XtraPrinting.TextBrick uniNameBrick = new DevExpress.XtraPrinting.TextBrick();
                uniNameBrick.Text = universityName;
                uniNameBrick.Font = uniNameFont;
                uniNameBrick.ForeColor = brandColor;
                uniNameBrick.Sides = DevExpress.XtraPrinting.BorderSide.None;
                uniNameBrick.BackColor = System.Drawing.Color.Transparent;
                uniNameBrick.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(System.Drawing.StringAlignment.Center);
                gr.DrawBrick(uniNameBrick, new System.Drawing.RectangleF(0, y + 5, pageWidth, 18));
                
                // Report Title - centered
                DevExpress.XtraPrinting.TextBrick reportTitleBrick = new DevExpress.XtraPrinting.TextBrick();
                reportTitleBrick.Text = "STUDENT RESULTS & MARKS SUBMISSION SUMMARY";
                reportTitleBrick.Font = reportTitleFont;
                reportTitleBrick.ForeColor = darkGray;
                reportTitleBrick.Sides = DevExpress.XtraPrinting.BorderSide.None;
                reportTitleBrick.BackColor = System.Drawing.Color.Transparent;
                reportTitleBrick.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(System.Drawing.StringAlignment.Center);
                gr.DrawBrick(reportTitleBrick, new System.Drawing.RectangleF(0, y + 26, pageWidth, 16));
                
                y += 48;
                
                // Horizontal line separator
                DevExpress.XtraPrinting.LineBrick lineBrick1 = new DevExpress.XtraPrinting.LineBrick();
                lineBrick1.ForeColor = lineColor;
                lineBrick1.LineStyle = System.Drawing.Drawing2D.DashStyle.Solid;
                lineBrick1.LineWidth = 2;
                lineBrick1.Sides = DevExpress.XtraPrinting.BorderSide.None;
                gr.DrawBrick(lineBrick1, new System.Drawing.RectangleF(0, y, pageWidth, 2));
                y += 6;
                
                // ========== FILTER INFORMATION BOX ==========
                float filterBoxHeight = 38;
                float filterBoxY = y;
                
                // Filter background box
                DevExpress.XtraPrinting.TextBrick filterBgBrick = new DevExpress.XtraPrinting.TextBrick();
                filterBgBrick.Text = "";
                filterBgBrick.BackColor = filterBgColor;
                filterBgBrick.Sides = DevExpress.XtraPrinting.BorderSide.All;
                filterBgBrick.BorderColor = filterBorderColor;
                filterBgBrick.BorderWidth = 1;
                gr.DrawBrick(filterBgBrick, new System.Drawing.RectangleF(0, filterBoxY, pageWidth, filterBoxHeight));
                
                // Programme row
                float labelWidth = 80;
                float valueWidth = 320;
                float col1X = 10;
                float col2X = pageWidth / 2 + 10;
                float row1Y = filterBoxY + 5;
                float row2Y = filterBoxY + 20;
                
                // Programme Label
                DevExpress.XtraPrinting.TextBrick progLabelBrick = new DevExpress.XtraPrinting.TextBrick();
                progLabelBrick.Text = "Programme:";
                progLabelBrick.Font = filterLabelFont;
                progLabelBrick.ForeColor = darkGray;
                progLabelBrick.Sides = DevExpress.XtraPrinting.BorderSide.None;
                progLabelBrick.BackColor = System.Drawing.Color.Transparent;
                gr.DrawBrick(progLabelBrick, new System.Drawing.RectangleF(col1X, row1Y, labelWidth, 14));
                
                // Programme Value
                DevExpress.XtraPrinting.TextBrick progValueBrick = new DevExpress.XtraPrinting.TextBrick();
                progValueBrick.Text = progName;
                progValueBrick.Font = filterValueFont;
                progValueBrick.ForeColor = brandColor;
                progValueBrick.Sides = DevExpress.XtraPrinting.BorderSide.None;
                progValueBrick.BackColor = System.Drawing.Color.Transparent;
                gr.DrawBrick(progValueBrick, new System.Drawing.RectangleF(col1X + labelWidth, row1Y, valueWidth, 14));
                
                // Entry Year Label
                DevExpress.XtraPrinting.TextBrick entryLabelBrick = new DevExpress.XtraPrinting.TextBrick();
                entryLabelBrick.Text = "Entry Year:";
                entryLabelBrick.Font = filterLabelFont;
                entryLabelBrick.ForeColor = darkGray;
                entryLabelBrick.Sides = DevExpress.XtraPrinting.BorderSide.None;
                entryLabelBrick.BackColor = System.Drawing.Color.Transparent;
                gr.DrawBrick(entryLabelBrick, new System.Drawing.RectangleF(col2X, row1Y, labelWidth, 14));
                
                // Entry Year Value
                DevExpress.XtraPrinting.TextBrick entryValueBrick = new DevExpress.XtraPrinting.TextBrick();
                entryValueBrick.Text = !string.IsNullOrEmpty(entryYear) ? entryYear : "All";
                entryValueBrick.Font = filterValueFont;
                entryValueBrick.ForeColor = brandColor;
                entryValueBrick.Sides = DevExpress.XtraPrinting.BorderSide.None;
                entryValueBrick.BackColor = System.Drawing.Color.Transparent;
                gr.DrawBrick(entryValueBrick, new System.Drawing.RectangleF(col2X + labelWidth, row1Y, 100, 14));
                
                // Year of Study Label
                DevExpress.XtraPrinting.TextBrick studyLabelBrick = new DevExpress.XtraPrinting.TextBrick();
                studyLabelBrick.Text = "Year of Study:";
                studyLabelBrick.Font = filterLabelFont;
                studyLabelBrick.ForeColor = darkGray;
                studyLabelBrick.Sides = DevExpress.XtraPrinting.BorderSide.None;
                studyLabelBrick.BackColor = System.Drawing.Color.Transparent;
                gr.DrawBrick(studyLabelBrick, new System.Drawing.RectangleF(col1X, row2Y, labelWidth, 14));
                
                // Year of Study Value
                DevExpress.XtraPrinting.TextBrick studyValueBrick = new DevExpress.XtraPrinting.TextBrick();
                studyValueBrick.Text = !string.IsNullOrEmpty(studyYear) ? "Year " + studyYear : "All";
                studyValueBrick.Font = filterValueFont;
                studyValueBrick.ForeColor = brandColor;
                studyValueBrick.Sides = DevExpress.XtraPrinting.BorderSide.None;
                studyValueBrick.BackColor = System.Drawing.Color.Transparent;
                gr.DrawBrick(studyValueBrick, new System.Drawing.RectangleF(col1X + labelWidth, row2Y, 100, 14));
                
                // Semester Label
                DevExpress.XtraPrinting.TextBrick semLabelBrick = new DevExpress.XtraPrinting.TextBrick();
                semLabelBrick.Text = "Semester:";
                semLabelBrick.Font = filterLabelFont;
                semLabelBrick.ForeColor = darkGray;
                semLabelBrick.Sides = DevExpress.XtraPrinting.BorderSide.None;
                semLabelBrick.BackColor = System.Drawing.Color.Transparent;
                gr.DrawBrick(semLabelBrick, new System.Drawing.RectangleF(col2X, row2Y, labelWidth, 14));
                
                // Semester Value (append the academic year of the sitting when supplied)
                DevExpress.XtraPrinting.TextBrick semValueBrick = new DevExpress.XtraPrinting.TextBrick();
                semValueBrick.Text = (!string.IsNullOrEmpty(semester) ? "Semester " + semester : "All")
                    + (!string.IsNullOrWhiteSpace(acadYear) ? "  ·  " + acadYear : "");
                semValueBrick.Font = filterValueFont;
                semValueBrick.ForeColor = brandColor;
                semValueBrick.Sides = DevExpress.XtraPrinting.BorderSide.None;
                semValueBrick.BackColor = System.Drawing.Color.Transparent;
                gr.DrawBrick(semValueBrick, new System.Drawing.RectangleF(col2X + labelWidth, row2Y, 100, 14));
                
                // Generated Date on the right side of filter box
                DevExpress.XtraPrinting.TextBrick dateLabelBrick = new DevExpress.XtraPrinting.TextBrick();
                dateLabelBrick.Text = "Generated: " + DateTime.Now.ToString("dd MMM yyyy, HH:mm");
                dateLabelBrick.Font = dateFont;
                dateLabelBrick.ForeColor = lightGray;
                dateLabelBrick.Sides = DevExpress.XtraPrinting.BorderSide.None;
                dateLabelBrick.BackColor = System.Drawing.Color.Transparent;
                dateLabelBrick.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(System.Drawing.StringAlignment.Far);
                gr.DrawBrick(dateLabelBrick, new System.Drawing.RectangleF(pageWidth - 200, row2Y, 190, 14));
                
                y = filterBoxY + filterBoxHeight + 8;
                
                // ========== COURSE VALIDATION WARNING ==========
                // Printed on the face of the report when a code does not agree with the curriculum,
                // so results are never published on the silent assumption that the codes are sound.
                {
                    List<string> codeIssues = BuildCourseValidationIssues(data, studyYear, semester);
                    if (codeIssues.Count > 0)
                    {
                        DevExpress.XtraPrinting.TextBrick warnTitle = new DevExpress.XtraPrinting.TextBrick();
                        warnTitle.Text = "  COURSE VALIDATION WARNING — " + codeIssues.Count + " issue" + (codeIssues.Count == 1 ? "" : "s") +
                                         " found. Verify the official curriculum / course master before publishing these results.";
                        warnTitle.Font = new System.Drawing.Font("Tahoma", 8, System.Drawing.FontStyle.Bold);
                        warnTitle.ForeColor = System.Drawing.Color.FromArgb(133, 100, 4);
                        warnTitle.BackColor = System.Drawing.Color.FromArgb(255, 243, 205);
                        warnTitle.BorderColor = System.Drawing.Color.FromArgb(255, 224, 130);
                        warnTitle.Sides = DevExpress.XtraPrinting.BorderSide.All;
                        warnTitle.Padding = new DevExpress.XtraPrinting.PaddingInfo(6, 6, 5, 5);
                        gr.DrawBrick(warnTitle, new System.Drawing.RectangleF(0, y, pageWidth, 18));
                        y += 18;

                        int shown = Math.Min(codeIssues.Count, 12);
                        for (int i = 0; i < shown; i++)
                        {
                            DevExpress.XtraPrinting.TextBrick line = new DevExpress.XtraPrinting.TextBrick();
                            line.Text = "     • " + codeIssues[i];
                            line.Font = new System.Drawing.Font("Tahoma", 6.5f, System.Drawing.FontStyle.Regular);
                            line.ForeColor = System.Drawing.Color.FromArgb(90, 68, 3);
                            line.BackColor = System.Drawing.Color.FromArgb(255, 250, 230);
                            line.BorderColor = System.Drawing.Color.FromArgb(255, 224, 130);
                            line.Sides = DevExpress.XtraPrinting.BorderSide.Left | DevExpress.XtraPrinting.BorderSide.Right;
                            line.Padding = new DevExpress.XtraPrinting.PaddingInfo(6, 6, 2, 2);
                            gr.DrawBrick(line, new System.Drawing.RectangleF(0, y, pageWidth, 12));
                            y += 12;
                        }
                        if (codeIssues.Count > shown)
                        {
                            DevExpress.XtraPrinting.TextBrick more = new DevExpress.XtraPrinting.TextBrick();
                            more.Text = "     … and " + (codeIssues.Count - shown) + " more.";
                            more.Font = new System.Drawing.Font("Tahoma", 6.5f, System.Drawing.FontStyle.Italic);
                            more.ForeColor = System.Drawing.Color.FromArgb(90, 68, 3);
                            more.BackColor = System.Drawing.Color.FromArgb(255, 250, 230);
                            more.BorderColor = System.Drawing.Color.FromArgb(255, 224, 130);
                            more.Sides = DevExpress.XtraPrinting.BorderSide.Left | DevExpress.XtraPrinting.BorderSide.Right | DevExpress.XtraPrinting.BorderSide.Bottom;
                            more.Padding = new DevExpress.XtraPrinting.PaddingInfo(6, 6, 2, 2);
                            gr.DrawBrick(more, new System.Drawing.RectangleF(0, y, pageWidth, 12));
                            y += 12;
                        }
                        y += 8;
                    }
                }

                // ========== COURSE PERFORMANCE ANALYSIS TABLE ==========
                {
                    // Aggregate stats per course across ALL specializations.
                    //
                    // Grouped case-insensitively and on the trimmed code. The course-code columns
                    // use utf8_general_ci, so mgt1201b and MGT1201B are ONE course to the database
                    // — acad_course is keyed on the code and physically cannot hold both. But the
                    // registration table has drifted, and 14 codes are written in more than one
                    // casing across 11,294 rows. LINQ's default string comparer is case-sensitive,
                    // so the report was splitting a single course into two lines with the marks
                    // divided between them — MGT1201B appearing once with 26 students and again,
                    // lower-case, with 3.
                    var courseStats = data.AsEnumerable()
                        .Where(r => r["courseid"] != DBNull.Value && !string.IsNullOrEmpty(r["courseid"].ToString())
                                 && r["grade"] != DBNull.Value && !string.IsNullOrEmpty(r["grade"].ToString()))
                        .GroupBy(r => r["courseid"].ToString().Trim(), StringComparer.OrdinalIgnoreCase)
                        .Select(g => {
                            var scores = g.Where(r => r["score"] != DBNull.Value)
                                          .Select(r => { 
                                              decimal val; 
                                              return decimal.TryParse(r["score"].ToString(), out val) ? val : -1m; 
                                          })
                                          .Where(s => s >= 0)
                                          .ToList();
                            
                            int totalResults = g.Count();
                            int passCount = g.Count(r => r["grade"].ToString() != "F");
                            int failCount = totalResults - passCount;
                            
                            string courseTitle = g.First()["course_title"] != DBNull.Value 
                                ? g.First()["course_title"].ToString() : "";
                            
                            decimal creditUnits = 0;
                            var cuRow = g.FirstOrDefault(r => r["CreditUnits"] != DBNull.Value);
                            if (cuRow != null) decimal.TryParse(cuRow["CreditUnits"].ToString(), out creditUnits);
                            
                            return new {
                                // Shown in the catalogue's own spelling. Every one of the 7,200
                                // codes in acad_course is upper-case, so upper-casing here is the
                                // canonical form rather than a cosmetic choice — and it stops the
                                // line being labelled with whichever mis-cased row happened to
                                // arrive first.
                                Code = (g.Key ?? "").ToUpperInvariant(),
                                Title = courseTitle,
                                CreditUnits = creditUnits,
                                TotalResults = totalResults,
                                PassCount = passCount,
                                FailCount = failCount,
                                PassRate = totalResults > 0 ? Math.Round((decimal)passCount / totalResults * 100, 1) : 0m,
                                AvgScore = scores.Count > 0 ? Math.Round(scores.Average(), 1) : 0m,
                                HighScore = scores.Count > 0 ? scores.Max() : 0m,
                                LowScore = scores.Count > 0 ? scores.Min() : 0m
                            };
                        })
                        .OrderBy(c => c.Code)
                        .ToList();
                    
                    if (courseStats.Count > 0)
                    {
                        // Section title
                        DevExpress.XtraPrinting.TextBrick cpaTitle = new DevExpress.XtraPrinting.TextBrick();
                        cpaTitle.Text = "  COURSE PERFORMANCE ANALYSIS";
                        cpaTitle.Font = new System.Drawing.Font("Tahoma", 8, System.Drawing.FontStyle.Bold);
                        cpaTitle.ForeColor = System.Drawing.Color.White;
                        cpaTitle.BackColor = brandColor;
                        cpaTitle.Sides = DevExpress.XtraPrinting.BorderSide.None;
                        cpaTitle.Padding = new DevExpress.XtraPrinting.PaddingInfo(5, 5, 5, 5);
                        gr.DrawBrick(cpaTitle, new System.Drawing.RectangleF(0, y, pageWidth, 20));
                        y += 22;
                        
                        // Column widths
                        float cpaSn = 22;
                        float cpaCode = 70;
                        float cpaName = 215;
                        float cpaCU = 28;
                        float cpaStudents = 52;
                        float cpaAvg = 50;
                        float cpaHigh = 50;
                        float cpaLow = 50;
                        float cpaPass = 42;
                        float cpaFail = 42;
                        float cpaRate = 55;
                        float cpaBar = pageWidth - cpaSn - cpaCode - cpaName - cpaCU - cpaStudents 
                                     - cpaAvg - cpaHigh - cpaLow - cpaPass - cpaFail - cpaRate;
                        
                        // Table header
                        float cpaHeaderH = 20;
                        System.Drawing.Font cpaHeaderFont = new System.Drawing.Font("Tahoma", 6, System.Drawing.FontStyle.Bold);
                        System.Drawing.Font cpaDataFont = new System.Drawing.Font("Tahoma", 6, System.Drawing.FontStyle.Regular);
                        System.Drawing.Color cpaHeaderBg = System.Drawing.Color.FromArgb(52, 73, 94);
                        
                        float cx = 0;
                        string[] cpaHeaders = new string[] { "#", "COURSE CODE", "COURSE NAME", "CU", "STUDENTS", "AVG", "HIGHEST", "LOWEST", "PASS", "FAIL", "PASS %", "" };
                        float[] cpaWidths = new float[] { cpaSn, cpaCode, cpaName, cpaCU, cpaStudents, cpaAvg, cpaHigh, cpaLow, cpaPass, cpaFail, cpaRate, cpaBar };
                        System.Drawing.StringAlignment[] cpaAligns = new System.Drawing.StringAlignment[] {
                            System.Drawing.StringAlignment.Center, System.Drawing.StringAlignment.Near, System.Drawing.StringAlignment.Near,
                            System.Drawing.StringAlignment.Center, System.Drawing.StringAlignment.Center, System.Drawing.StringAlignment.Center,
                            System.Drawing.StringAlignment.Center, System.Drawing.StringAlignment.Center, System.Drawing.StringAlignment.Center,
                            System.Drawing.StringAlignment.Center, System.Drawing.StringAlignment.Center, System.Drawing.StringAlignment.Near
                        };
                        
                        for (int h = 0; h < cpaHeaders.Length; h++)
                        {
                            DevExpress.XtraPrinting.TextBrick hdr = new DevExpress.XtraPrinting.TextBrick();
                            hdr.Text = cpaHeaders[h];
                            hdr.Font = cpaHeaderFont;
                            hdr.ForeColor = System.Drawing.Color.White;
                            hdr.BackColor = cpaHeaderBg;
                            hdr.BorderColor = borderColor;
                            hdr.Sides = DevExpress.XtraPrinting.BorderSide.All;
                            hdr.Padding = new DevExpress.XtraPrinting.PaddingInfo(3, 3, 4, 4);
                            hdr.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(cpaAligns[h]);
                            gr.DrawBrick(hdr, new System.Drawing.RectangleF(cx, y, cpaWidths[h], cpaHeaderH));
                            cx += cpaWidths[h];
                        }
                        y += cpaHeaderH;
                        
                        // Data rows
                        float cpaRowH = 18;
                        int cpaRowNum = 0;
                        
                        // Compute overall totals for the footer row
                        int grandTotalResults = 0;
                        int grandTotalPass = 0;
                        int grandTotalFail = 0;
                        decimal grandScoreSum = 0;
                        int grandScoreCount = 0;
                        
                        foreach (var cs in courseStats)
                        {
                            cpaRowNum++;
                            cx = 0;
                            System.Drawing.Color cpaRowBg = (cpaRowNum % 2 == 0) ? altRowColor : System.Drawing.Color.White;
                            
                            // Determine pass rate color
                            System.Drawing.Color rateColor;
                            if (cs.PassRate >= 80) rateColor = System.Drawing.Color.FromArgb(39, 174, 96);        // Green
                            else if (cs.PassRate >= 60) rateColor = System.Drawing.Color.FromArgb(41, 128, 185);   // Blue
                            else if (cs.PassRate >= 40) rateColor = System.Drawing.Color.FromArgb(243, 156, 18);   // Orange
                            else rateColor = System.Drawing.Color.FromArgb(192, 57, 43);                           // Red
                            
                            // Truncate long course names
                            string cpaDispName = cs.Title.Length > 38 ? cs.Title.Substring(0, 38) + ".." : cs.Title;
                            
                            string[] cpaValues = new string[] {
                                cpaRowNum.ToString(),
                                cs.Code,
                                cpaDispName,
                                cs.CreditUnits > 0 ? cs.CreditUnits.ToString("0") : "-",
                                cs.TotalResults.ToString(),
                                cs.AvgScore > 0 ? cs.AvgScore.ToString("0.0") : "-",
                                cs.HighScore > 0 ? cs.HighScore.ToString("0") : "-",
                                cs.LowScore > 0 ? cs.LowScore.ToString("0") : "-",
                                cs.PassCount.ToString(),
                                cs.FailCount.ToString(),
                                cs.PassRate.ToString("0.0") + "%",
                                ""
                            };
                            
                            for (int d = 0; d < cpaValues.Length; d++)
                            {
                                DevExpress.XtraPrinting.TextBrick cell = new DevExpress.XtraPrinting.TextBrick();
                                cell.Text = cpaValues[d];
                                cell.Font = cpaDataFont;
                                cell.BackColor = cpaRowBg;
                                cell.BorderColor = borderColor;
                                cell.Sides = DevExpress.XtraPrinting.BorderSide.All;
                                cell.Padding = new DevExpress.XtraPrinting.PaddingInfo(3, 3, 3, 3);
                                cell.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(cpaAligns[d]);
                                
                                // Color-code specific columns
                                if (d == 10) cell.ForeColor = rateColor; // Pass %
                                else if (d == 9 && cs.FailCount > 0) cell.ForeColor = System.Drawing.Color.FromArgb(192, 57, 43); // Fail count in red
                                else cell.ForeColor = darkGray;
                                
                                // Bold the pass % column
                                if (d == 10) cell.Font = new System.Drawing.Font("Tahoma", 6, System.Drawing.FontStyle.Bold);
                                
                                gr.DrawBrick(cell, new System.Drawing.RectangleF(cx, y, cpaWidths[d], cpaRowH));
                                cx += cpaWidths[d];
                            }
                            
                            // Visual pass-rate bar in the last column
                            if (cpaBar > 4)
                            {
                                float barMaxWidth = cpaBar - 6;
                                float barWidth = barMaxWidth * (float)(cs.PassRate / 100m);
                                if (barWidth < 1 && cs.PassRate > 0) barWidth = 1;
                                
                                DevExpress.XtraPrinting.TextBrick barBrick = new DevExpress.XtraPrinting.TextBrick();
                                barBrick.Text = "";
                                barBrick.BackColor = rateColor;
                                barBrick.Sides = DevExpress.XtraPrinting.BorderSide.None;
                                gr.DrawBrick(barBrick, new System.Drawing.RectangleF(
                                    cx - cpaBar + 3, y + 4, barWidth, cpaRowH - 8));
                            }
                            
                            // Accumulate grand totals
                            grandTotalResults += cs.TotalResults;
                            grandTotalPass += cs.PassCount;
                            grandTotalFail += cs.FailCount;
                            if (cs.AvgScore > 0)
                            {
                                grandScoreSum += cs.AvgScore * cs.TotalResults;
                                grandScoreCount += cs.TotalResults;
                            }
                            
                            y += cpaRowH;
                        }
                        
                        // ========== TOTALS / OVERALL ROW ==========
                        cx = 0;
                        System.Drawing.Color totalsBg = System.Drawing.Color.FromArgb(44, 62, 80);
                        System.Drawing.Font totalsFont = new System.Drawing.Font("Tahoma", 6, System.Drawing.FontStyle.Bold);
                        decimal grandAvg = grandScoreCount > 0 ? Math.Round(grandScoreSum / grandScoreCount, 1) : 0m;
                        decimal grandPassRate = grandTotalResults > 0 ? Math.Round((decimal)grandTotalPass / grandTotalResults * 100, 1) : 0m;
                        
                        string[] totalValues = new string[] {
                            "", "OVERALL", courseStats.Count + " courses", "",
                            grandTotalResults.ToString(),
                            grandAvg > 0 ? grandAvg.ToString("0.0") : "-",
                            "", "",
                            grandTotalPass.ToString(),
                            grandTotalFail.ToString(),
                            grandPassRate.ToString("0.0") + "%",
                            ""
                        };
                        
                        for (int t = 0; t < totalValues.Length; t++)
                        {
                            DevExpress.XtraPrinting.TextBrick tCell = new DevExpress.XtraPrinting.TextBrick();
                            tCell.Text = totalValues[t];
                            tCell.Font = totalsFont;
                            tCell.ForeColor = System.Drawing.Color.White;
                            tCell.BackColor = totalsBg;
                            tCell.BorderColor = borderColor;
                            tCell.Sides = DevExpress.XtraPrinting.BorderSide.All;
                            tCell.Padding = new DevExpress.XtraPrinting.PaddingInfo(3, 3, 3, 3);
                            tCell.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(cpaAligns[t]);
                            
                            // Highlight fail count in totals row
                            if (t == 9 && grandTotalFail > 0) 
                                tCell.ForeColor = System.Drawing.Color.FromArgb(255, 180, 180);
                            
                            gr.DrawBrick(tCell, new System.Drawing.RectangleF(cx, y, cpaWidths[t], cpaRowH + 2));
                            cx += cpaWidths[t];
                        }
                        
                        y += cpaRowH + 2;
                        y += 12;
                    }
                }

                // ========== SPECIALIZATION SECTIONS ==========
                foreach (var specGroup in specializationGroups)
                {
                    // Use the EXACT spec name from acad_specialisation - no fabrication
                    string specName;
                    if (specGroup.Key == "0")
                    {
                        specName = "NOT ASSIGNED";
                    }
                    else
                    {
                        // Get the spec name directly from the DB row (already TRIM'd in SQL)
                        specName = specGroup.First()["specialization"] != DBNull.Value 
                            ? specGroup.First()["specialization"].ToString().Trim() : "NOT ASSIGNED";
                        if (string.IsNullOrEmpty(specName) || specName == "General" || specName == "-")
                            specName = "NOT ASSIGNED";
                    }
                    
                    // Get programme and spec_id from the group key
                    string progId = specGroup.First()["progid"] != DBNull.Value ? specGroup.First()["progid"].ToString() : "";
                    string specId = specGroup.Key; // This is the actual spec_id from the database
                    
                    // Get unique courses for this specialization
                    // Distinct() on the raw code is case-sensitive, so a course written two ways
                    // listed twice. Collapsed on the canonical (upper-case) code instead — see
                    // the note on the course-performance table.
                    var courses = specGroup
                        .Where(r => r["courseid"] != DBNull.Value && r["courseid"].ToString().Trim() != "")
                        .GroupBy(r => r["courseid"].ToString().Trim().ToUpperInvariant())
                        .Select(g => new {
                            Code = g.Key,
                            Title = g.Select(r => r["course_title"] != DBNull.Value ? r["course_title"].ToString() : "")
                                     .FirstOrDefault(t => !string.IsNullOrEmpty(t)) ?? ""
                        })
                        .OrderBy(c => c.Code)
                        .ToList();
                    
                    if (courses.Count == 0) continue;
                    
                    // Get unique students in this specialization
                    var students = specGroup
                        .GroupBy(r => r["entryno"].ToString())
                        .Select(g => new {
                            EntryNo = g.Key,
                            RegNo = g.First()["entryno"] != DBNull.Value ? g.First()["entryno"].ToString() : "",
                            Name = g.First()["student_name"] != DBNull.Value ? g.First()["student_name"].ToString() : "",
                            Cgpa = (g.First().Table.Columns.Contains("cgpa") && g.First()["cgpa"] != DBNull.Value && Convert.ToDecimal(g.First()["cgpa"]) > 0)
                                     ? Convert.ToDecimal(g.First()["cgpa"]).ToString("0.00") : "-",
                            Results = g.ToList()
                        })
                        .OrderBy(s => s.Name)
                        .ToList();

                    if (students.Count == 0) continue;

                    // Calculate column widths - fixed cols: SN, Name, Reg, CGPA, Status.
                    // '#' is wide enough for three digits: at 22pt it clipped every serial from 10
                    // upwards to its first character, so a 41-student group appeared to restart its
                    // numbering on each page.
                    float snWidth = 30;
                    float nameWidth = 132;
                    float regWidth = 112;
                    float cgpaWidth = 40;   // cumulative GPA column
                    float statusWidth = 66; // Status column - fits "INCOMPLETE 1/5"
                    float fixedWidth = snWidth + nameWidth + regWidth + cgpaWidth + statusWidth;
                    float availableWidth = pageWidth - fixedWidth;

                    // A course column is sized from the LONGEST code in this table, never the other
                    // way round: a code is a key, and a shortened key is a different key. Five
                    // different Mathematics papers used to print as an identical "BME220".
                    // When the codes will not all fit, the table is split into column groups and
                    // repeated - so every course is shown in full, and none is dropped.
                    float widestCode = 0f;
                    for (int i = 0; i < courses.Count; i++)
                        widestCode = Math.Max(widestCode, courses[i].Code.Trim().Length * 5.4f);
                    float minCourseColWidth = Math.Max(50f, widestCode + 11f);
                    int coursesPerGroup = Math.Max(1, (int)(availableWidth / minCourseColWidth));
                    int courseGroupCount = Math.Max(1, (int)Math.Ceiling(courses.Count / (double)coursesPerGroup));

                    // Keep a specialization's header with at least its first row: if there isn't room
                    // for spec header (26) + table header (24) + one row (22), start on a new page.
                    if ((y % pageHeight) + 72 > pageHeight)
                        y = ((float)Math.Floor(y / pageHeight) + 1) * pageHeight;

                    // ========== SPECIALIZATION HEADER ==========
                    // Specialization header (full width) — name + student count. No curriculum info shown.
                    string specHeaderText = "  " + specName + " (" + students.Count + " students)";
                    DevExpress.XtraPrinting.TextBrick specHeader = new DevExpress.XtraPrinting.TextBrick();
                    specHeader.Text = specHeaderText;
                    specHeader.Font = subtitleFont;
                    specHeader.ForeColor = System.Drawing.Color.White;
                    specHeader.BackColor = specHeaderBg;
                    specHeader.Sides = DevExpress.XtraPrinting.BorderSide.None;
                    specHeader.Padding = new DevExpress.XtraPrinting.PaddingInfo(5, 5, 6, 6);
                    gr.DrawBrick(specHeader, new System.Drawing.RectangleF(0, y, pageWidth, 24));
                    
                    // Right filler to complete the header bar
                    DevExpress.XtraPrinting.TextBrick rightFiller = new DevExpress.XtraPrinting.TextBrick();
                    rightFiller.Text = "";
                    rightFiller.BackColor = specHeaderBg;
                    rightFiller.Sides = DevExpress.XtraPrinting.BorderSide.None;
                    gr.DrawBrick(rightFiller, new System.Drawing.RectangleF(pageWidth * 0.9f, y, pageWidth * 0.1f, 24));
                    
                    y += 26;
                    
                    // One pass per column group. Group 0 carries the specialization header; a later
                    // group repeats the student columns under a "continued" bar so the reader can
                    // still tell whose marks they are looking at.
                    for (int courseGroup = 0; courseGroup < courseGroupCount; courseGroup++)
                    {
                    int firstCourse = courseGroup * coursesPerGroup;
                    int groupCourseCount = Math.Min(coursesPerGroup, courses.Count - firstCourse);
                    if (groupCourseCount <= 0) break;
                    bool lastCourseGroup = (courseGroup == courseGroupCount - 1);
                    float courseColWidth = availableWidth / groupCourseCount;

                    if (courseGroup > 0)
                    {
                        // Continuation bar for this specialization's remaining courses.
                        if ((y % pageHeight) + 72 > pageHeight)
                            y = ((float)Math.Floor(y / pageHeight) + 1) * pageHeight;
                        DevExpress.XtraPrinting.TextBrick contBar = new DevExpress.XtraPrinting.TextBrick();
                        contBar.Text = "  " + specName + " (continued — courses " + (firstCourse + 1) + "–" + (firstCourse + groupCourseCount) + " of " + courses.Count + ")";
                        contBar.Font = subtitleFont;
                        contBar.ForeColor = System.Drawing.Color.White;
                        contBar.BackColor = specHeaderBg;
                        contBar.Sides = DevExpress.XtraPrinting.BorderSide.None;
                        contBar.Padding = new DevExpress.XtraPrinting.PaddingInfo(5, 5, 6, 6);
                        gr.DrawBrick(contBar, new System.Drawing.RectangleF(0, y, pageWidth, 24));
                        y += 26;
                    }

                    // Table header — defined as a reusable action so it can be REPEATED at the top of
                    // each page this specialization's table spills onto (manual BrickGraphics paging).
                    float x = 0;
                    float headerHeight = 24;
                    Action drawTableHeader = () =>
                    {
                        x = 0;
                        DevExpress.XtraPrinting.TextBrick snHeader = new DevExpress.XtraPrinting.TextBrick();
                        snHeader.Text = "#";
                        snHeader.Font = headerFont;
                        snHeader.ForeColor = System.Drawing.Color.White;
                        snHeader.BackColor = headerBg;
                        snHeader.BorderColor = borderColor;
                        snHeader.Sides = DevExpress.XtraPrinting.BorderSide.All;
                        snHeader.Padding = new DevExpress.XtraPrinting.PaddingInfo(5, 5, 6, 6);
                        snHeader.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(System.Drawing.StringAlignment.Center);
                        gr.DrawBrick(snHeader, new System.Drawing.RectangleF(x, y, snWidth, headerHeight));
                        x += snWidth;

                        DevExpress.XtraPrinting.TextBrick nameHeader = new DevExpress.XtraPrinting.TextBrick();
                        nameHeader.Text = "Student Name";
                        nameHeader.Font = headerFont;
                        nameHeader.ForeColor = System.Drawing.Color.White;
                        nameHeader.BackColor = headerBg;
                        nameHeader.BorderColor = borderColor;
                        nameHeader.Sides = DevExpress.XtraPrinting.BorderSide.All;
                        nameHeader.Padding = new DevExpress.XtraPrinting.PaddingInfo(5, 5, 6, 6);
                        gr.DrawBrick(nameHeader, new System.Drawing.RectangleF(x, y, nameWidth, headerHeight));
                        x += nameWidth;

                        DevExpress.XtraPrinting.TextBrick regHeader = new DevExpress.XtraPrinting.TextBrick();
                        regHeader.Text = "Reg No";
                        regHeader.Font = headerFont;
                        regHeader.ForeColor = System.Drawing.Color.White;
                        regHeader.BackColor = headerBg;
                        regHeader.BorderColor = borderColor;
                        regHeader.Sides = DevExpress.XtraPrinting.BorderSide.All;
                        regHeader.Padding = new DevExpress.XtraPrinting.PaddingInfo(5, 5, 6, 6);
                        gr.DrawBrick(regHeader, new System.Drawing.RectangleF(x, y, regWidth, headerHeight));
                        x += regWidth;

                        for (int i = firstCourse; i < firstCourse + groupCourseCount; i++)
                        {
                            DevExpress.XtraPrinting.TextBrick courseHeader = new DevExpress.XtraPrinting.TextBrick();
                            // The official code from the course master, in full, always.
                            courseHeader.Text = courses[i].Code.Trim();
                            courseHeader.Font = headerFont;
                            courseHeader.ForeColor = System.Drawing.Color.White;
                            courseHeader.BackColor = headerBg;
                            courseHeader.BorderColor = borderColor;
                            courseHeader.Sides = DevExpress.XtraPrinting.BorderSide.All;
                            courseHeader.Padding = new DevExpress.XtraPrinting.PaddingInfo(4, 4, 6, 6);
                            courseHeader.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(System.Drawing.StringAlignment.Center);
                            gr.DrawBrick(courseHeader, new System.Drawing.RectangleF(x, y, courseColWidth, headerHeight));
                            x += courseColWidth;
                        }

                        DevExpress.XtraPrinting.TextBrick cgpaHeader = new DevExpress.XtraPrinting.TextBrick();
                        cgpaHeader.Text = "CGPA";
                        cgpaHeader.Font = headerFont;
                        cgpaHeader.ForeColor = System.Drawing.Color.White;
                        cgpaHeader.BackColor = headerBg;
                        cgpaHeader.BorderColor = borderColor;
                        cgpaHeader.Sides = DevExpress.XtraPrinting.BorderSide.All;
                        cgpaHeader.Padding = new DevExpress.XtraPrinting.PaddingInfo(4, 4, 6, 6);
                        cgpaHeader.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(System.Drawing.StringAlignment.Center);
                        gr.DrawBrick(cgpaHeader, new System.Drawing.RectangleF(x, y, cgpaWidth, headerHeight));
                        x += cgpaWidth;

                        DevExpress.XtraPrinting.TextBrick statusHeader = new DevExpress.XtraPrinting.TextBrick();
                        statusHeader.Text = "STATUS";
                        statusHeader.Font = headerFont;
                        statusHeader.ForeColor = System.Drawing.Color.White;
                        statusHeader.BackColor = headerBg;
                        statusHeader.BorderColor = borderColor;
                        statusHeader.Sides = DevExpress.XtraPrinting.BorderSide.All;
                        statusHeader.Padding = new DevExpress.XtraPrinting.PaddingInfo(4, 4, 6, 6);
                        statusHeader.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(System.Drawing.StringAlignment.Center);
                        gr.DrawBrick(statusHeader, new System.Drawing.RectangleF(x, y, statusWidth, headerHeight));

                        y += headerHeight;
                    };
                    drawTableHeader();
                    
                    // Student Data Rows - taller to fit Grade(Score) on line 1 and CW·Exam on line 2
                    float rowHeight = 22;   // single-line cells (grade + score only)
                    int sn = 0;
                    foreach (var student in students)
                    {
                        // If this row would spill past the page bottom, move to the next page and
                        // REPEAT the table header there so the continuation stays readable.
                        if ((y % pageHeight) + rowHeight > pageHeight - 4)
                        {
                            y = ((float)Math.Floor(y / pageHeight) + 1) * pageHeight;
                            drawTableHeader();
                        }
                        sn++;
                        x = 0;
                        System.Drawing.Color rowBg = (sn % 2 == 0) ? altRowColor : System.Drawing.Color.White;
                        
                        // SN Cell
                        DevExpress.XtraPrinting.TextBrick snCell = new DevExpress.XtraPrinting.TextBrick();
                        snCell.Text = sn.ToString();
                        snCell.Font = cellFont;
                        snCell.BackColor = rowBg;
                        snCell.BorderColor = borderColor;
                        snCell.Sides = DevExpress.XtraPrinting.BorderSide.All;
                        snCell.Padding = new DevExpress.XtraPrinting.PaddingInfo(5, 5, 6, 6);
                        snCell.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(System.Drawing.StringAlignment.Center);
                        gr.DrawBrick(snCell, new System.Drawing.RectangleF(x, y, snWidth, rowHeight));
                        x += snWidth;
                        
                        // Name Cell
                        string displayName = student.Name.Length > 32 ? student.Name.Substring(0, 32) + ".." : student.Name;
                        DevExpress.XtraPrinting.TextBrick nameCell = new DevExpress.XtraPrinting.TextBrick();
                        nameCell.Text = displayName;
                        nameCell.Font = cellFont;
                        nameCell.BackColor = rowBg;
                        nameCell.BorderColor = borderColor;
                        nameCell.Sides = DevExpress.XtraPrinting.BorderSide.All;
                        nameCell.Padding = new DevExpress.XtraPrinting.PaddingInfo(5, 5, 6, 6);
                        gr.DrawBrick(nameCell, new System.Drawing.RectangleF(x, y, nameWidth, rowHeight));
                        x += nameWidth;
                        
                        // RegNo Cell
                        DevExpress.XtraPrinting.TextBrick regCell = new DevExpress.XtraPrinting.TextBrick();
                        regCell.Text = student.RegNo;
                        regCell.Font = cellFont;
                        regCell.BackColor = rowBg;
                        regCell.BorderColor = borderColor;
                        regCell.Sides = DevExpress.XtraPrinting.BorderSide.All;
                        regCell.Padding = new DevExpress.XtraPrinting.PaddingInfo(5, 5, 6, 6);
                        gr.DrawBrick(regCell, new System.Drawing.RectangleF(x, y, regWidth, rowHeight));
                        x += regWidth;
                        
                        // Papers sat and papers passed are counted over the student's WHOLE result
                        // set, not the columns that happen to be on screen. Counting the visible
                        // subset meant a course pushed off the page silently lowered the student's
                        // paper count and could turn a PASS into a FAIL.
                        int studentResultCount = 0;
                        int studentPassedCount = 0;
                        foreach (DataRow rr in student.Results)
                        {
                            if (rr["grade"] == DBNull.Value) continue;
                            studentResultCount++;
                            if (rr["grade"].ToString() != "F") studentPassedCount++;
                        }

                        // Course Grade + Score Cells (this column group only)
                        for (int i = firstCourse; i < firstCourse + groupCourseCount; i++)
                        {
                            string courseCode = courses[i].Code;
                            var result = student.Results.FirstOrDefault(r => 
                                r["courseid"] != DBNull.Value && r["courseid"].ToString() == courseCode);
                            
                            string displayText = "-";
                            System.Drawing.Color gradeColor = System.Drawing.Color.LightGray;
                            System.Drawing.Color cellBg = rowBg;

                            if (result != null && result["grade"] != DBNull.Value)
                            {
                                string grade = result["grade"].ToString();
                                string score = result["score"] != DBNull.Value ? result["score"].ToString() : "";

                                // Cell shows ONLY the grade and the score, e.g. "C (60)".
                                if (!string.IsNullOrEmpty(score))
                                    displayText = grade + " (" + score + ")";
                                else
                                    displayText = grade;

                                // Tint the cell background by marks-submission status (no CW/Exam text).
                                if (hasProvisional)
                                {
                                    string subStatus = result["sub_status"] != DBNull.Value ? result["sub_status"].ToString() : "";
                                    cellBg = GetSubmissionTint(subStatus, rowBg);
                                }

                                gradeColor = (grade == "F") ? System.Drawing.Color.FromArgb(192, 57, 43) : System.Drawing.Color.Black;
                            }

                            DevExpress.XtraPrinting.TextBrick gradeCell = new DevExpress.XtraPrinting.TextBrick();
                            gradeCell.Text = displayText;
                            gradeCell.Font = cellFont;
                            gradeCell.ForeColor = gradeColor;
                            gradeCell.BackColor = cellBg;
                            gradeCell.BorderColor = borderColor;
                            gradeCell.Sides = DevExpress.XtraPrinting.BorderSide.All;
                            gradeCell.Padding = new DevExpress.XtraPrinting.PaddingInfo(4, 4, 4, 4);
                            gradeCell.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(System.Drawing.StringAlignment.Center);
                            gr.DrawBrick(gradeCell, new System.Drawing.RectangleF(x, y, courseColWidth, rowHeight));
                            x += courseColWidth;
                        }

                        // CGPA cell (cumulative, authoritative)
                        DevExpress.XtraPrinting.TextBrick cgpaCell = new DevExpress.XtraPrinting.TextBrick();
                        cgpaCell.Text = student.Cgpa;
                        cgpaCell.Font = cellFont;
                        cgpaCell.ForeColor = System.Drawing.Color.FromArgb(23, 77, 164);
                        cgpaCell.BackColor = rowBg;
                        cgpaCell.BorderColor = borderColor;
                        cgpaCell.Sides = DevExpress.XtraPrinting.BorderSide.All;
                        cgpaCell.Padding = new DevExpress.XtraPrinting.PaddingInfo(4, 4, 4, 4);
                        cgpaCell.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(System.Drawing.StringAlignment.Center);
                        gr.DrawBrick(cgpaCell, new System.Drawing.RectangleF(x, y, cgpaWidth, rowHeight));
                        x += cgpaWidth;

                        // Status Cell — curriculum-free rule. A student FAILS the semester if they have
                        // any F (score < 50) OR sat fewer than the level minimum (Diploma 5 / Bachelor 6)
                        // courses; otherwise PASS. studentPassedCount = non-F results.
                        // Three distinct outcomes, because two of them used to read the same.
                        //   FAIL        - the student actually failed a paper (an F exists)
                        //   INCOMPLETE  - no F, but fewer papers than the sitting requires; usually a
                        //                 student from another year carrying one paper. "FAIL (1/5)"
                        //                 made a B+ in that single paper look like a failure.
                        //   PASS        - no F and enough papers
                        int studentFailCount = studentResultCount - studentPassedCount;
                        bool hasF = studentFailCount > 0;
                        bool tooFew = studentResultCount < minCourses;
                        string statusText = hasF
                            ? "FAIL - " + studentFailCount + "F"
                            : (tooFew ? "INCOMPLETE " + studentResultCount + "/" + minCourses : "PASS");
                        System.Drawing.Color statusBgColor = hasF
                            ? System.Drawing.Color.FromArgb(248, 215, 218)
                            : (tooFew ? System.Drawing.Color.FromArgb(255, 243, 205) : System.Drawing.Color.FromArgb(212, 237, 218));
                        System.Drawing.Color statusTextColor = hasF
                            ? System.Drawing.Color.FromArgb(114, 28, 36)
                            : (tooFew ? System.Drawing.Color.FromArgb(133, 100, 4) : System.Drawing.Color.FromArgb(21, 87, 36));

                        DevExpress.XtraPrinting.TextBrick statusCell = new DevExpress.XtraPrinting.TextBrick();
                        statusCell.Text = statusText;
                        statusCell.Font = new System.Drawing.Font("Tahoma", 5, System.Drawing.FontStyle.Bold);
                        statusCell.ForeColor = statusTextColor;
                        statusCell.BackColor = statusBgColor;
                        statusCell.BorderColor = borderColor;
                        statusCell.Sides = DevExpress.XtraPrinting.BorderSide.All;
                        statusCell.Padding = new DevExpress.XtraPrinting.PaddingInfo(3, 3, 6, 6);
                        statusCell.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(System.Drawing.StringAlignment.Center);
                        gr.DrawBrick(statusCell, new System.Drawing.RectangleF(x, y, statusWidth, rowHeight));

                        y += rowHeight;
                    }

                    y += 12; // Space between column groups / sections
                    }        // end column-group loop
                }
                
                // ========== SUMMARY STATISTICS SECTION ==========
                y += 10;
                
                // Calculate summary statistics
                int totalStudents = specializationGroups.Sum(g => g.Select(r => r["entryno"].ToString()).Distinct().Count());
                int totalSpecializations = specializationGroups.Count;
                
                // Summary box background
                float summaryBoxHeight = hasProvisional ? 108 : 85;
                DevExpress.XtraPrinting.TextBrick summaryBgBrick = new DevExpress.XtraPrinting.TextBrick();
                summaryBgBrick.Text = "";
                summaryBgBrick.BackColor = System.Drawing.Color.FromArgb(248, 249, 250);
                summaryBgBrick.Sides = DevExpress.XtraPrinting.BorderSide.All;
                summaryBgBrick.BorderColor = System.Drawing.Color.FromArgb(222, 226, 230);
                summaryBgBrick.BorderWidth = 1;
                gr.DrawBrick(summaryBgBrick, new System.Drawing.RectangleF(0, y, pageWidth, summaryBoxHeight));
                
                // Summary Title
                DevExpress.XtraPrinting.TextBrick summaryTitleBrick = new DevExpress.XtraPrinting.TextBrick();
                summaryTitleBrick.Text = "REPORT SUMMARY";
                summaryTitleBrick.Font = new System.Drawing.Font("Tahoma", 8, System.Drawing.FontStyle.Bold);
                summaryTitleBrick.ForeColor = brandColor;
                summaryTitleBrick.Sides = DevExpress.XtraPrinting.BorderSide.None;
                summaryTitleBrick.BackColor = System.Drawing.Color.Transparent;
                gr.DrawBrick(summaryTitleBrick, new System.Drawing.RectangleF(10, y + 6, 150, 14));
                
                // Summary content - Row 1
                float summaryY = y + 22;
                float sumCol1X = 10;
                float sumCol2X = pageWidth / 3;
                float sumCol3X = (pageWidth / 3) * 2;
                
                // Total Students
                DevExpress.XtraPrinting.TextBrick statLabel1 = new DevExpress.XtraPrinting.TextBrick();
                statLabel1.Text = "Total Students:";
                statLabel1.Font = filterLabelFont;
                statLabel1.ForeColor = darkGray;
                statLabel1.Sides = DevExpress.XtraPrinting.BorderSide.None;
                statLabel1.BackColor = System.Drawing.Color.Transparent;
                gr.DrawBrick(statLabel1, new System.Drawing.RectangleF(sumCol1X, summaryY, 80, 12));
                
                DevExpress.XtraPrinting.TextBrick statValue1 = new DevExpress.XtraPrinting.TextBrick();
                statValue1.Text = totalStudents.ToString();
                statValue1.Font = new System.Drawing.Font("Tahoma", 9, System.Drawing.FontStyle.Bold);
                statValue1.ForeColor = brandColor;
                statValue1.Sides = DevExpress.XtraPrinting.BorderSide.None;
                statValue1.BackColor = System.Drawing.Color.Transparent;
                gr.DrawBrick(statValue1, new System.Drawing.RectangleF(sumCol1X + 80, summaryY, 60, 12));
                
                // Total Specializations
                DevExpress.XtraPrinting.TextBrick statLabel2 = new DevExpress.XtraPrinting.TextBrick();
                statLabel2.Text = "Specializations:";
                statLabel2.Font = filterLabelFont;
                statLabel2.ForeColor = darkGray;
                statLabel2.Sides = DevExpress.XtraPrinting.BorderSide.None;
                statLabel2.BackColor = System.Drawing.Color.Transparent;
                gr.DrawBrick(statLabel2, new System.Drawing.RectangleF(sumCol2X, summaryY, 80, 12));
                
                DevExpress.XtraPrinting.TextBrick statValue2 = new DevExpress.XtraPrinting.TextBrick();
                statValue2.Text = totalSpecializations.ToString();
                statValue2.Font = new System.Drawing.Font("Tahoma", 9, System.Drawing.FontStyle.Bold);
                statValue2.ForeColor = brandColor;
                statValue2.Sides = DevExpress.XtraPrinting.BorderSide.None;
                statValue2.BackColor = System.Drawing.Color.Transparent;
                gr.DrawBrick(statValue2, new System.Drawing.RectangleF(sumCol2X + 80, summaryY, 60, 12));
                
                // Report Period
                DevExpress.XtraPrinting.TextBrick statLabel3 = new DevExpress.XtraPrinting.TextBrick();
                statLabel3.Text = "Report Period:";
                statLabel3.Font = filterLabelFont;
                statLabel3.ForeColor = darkGray;
                statLabel3.Sides = DevExpress.XtraPrinting.BorderSide.None;
                statLabel3.BackColor = System.Drawing.Color.Transparent;
                gr.DrawBrick(statLabel3, new System.Drawing.RectangleF(sumCol3X, summaryY, 80, 12));
                
                string periodText = "Year " + studyYear + ", Sem " + semester;
                DevExpress.XtraPrinting.TextBrick statValue3 = new DevExpress.XtraPrinting.TextBrick();
                statValue3.Text = periodText;
                statValue3.Font = new System.Drawing.Font("Tahoma", 9, System.Drawing.FontStyle.Bold);
                statValue3.ForeColor = brandColor;
                statValue3.Sides = DevExpress.XtraPrinting.BorderSide.None;
                statValue3.BackColor = System.Drawing.Color.Transparent;
                gr.DrawBrick(statValue3, new System.Drawing.RectangleF(sumCol3X + 80, summaryY, 100, 12));
                

                // Status legend — curriculum-free rule
                summaryY += 18;
                DevExpress.XtraPrinting.TextBrick legendTitle = new DevExpress.XtraPrinting.TextBrick();
                legendTitle.Text = "Status:";
                legendTitle.Font = filterLabelFont;
                legendTitle.ForeColor = darkGray;
                legendTitle.Sides = DevExpress.XtraPrinting.BorderSide.None;
                legendTitle.BackColor = System.Drawing.Color.Transparent;
                gr.DrawBrick(legendTitle, new System.Drawing.RectangleF(sumCol1X, summaryY, 50, 12));

                DevExpress.XtraPrinting.TextBrick legendText = new DevExpress.XtraPrinting.TextBrick();
                // Reads the figure the report was actually run with. It used to say "4" whatever the
                // operator chose, so a report built on 5 contradicted its own legend.
                legendText.Text = "PASS = no F and at least " + minCourses + " papers"
                                + "      FAIL = one or more failed paper (F)"
                                + "      INCOMPLETE = no F but fewer than " + minCourses + " papers sat this sitting";
                legendText.Font = smallFont;
                legendText.ForeColor = lightGray;
                legendText.Sides = DevExpress.XtraPrinting.BorderSide.None;
                legendText.BackColor = System.Drawing.Color.Transparent;
                gr.DrawBrick(legendText, new System.Drawing.RectangleF(sumCol1X + 52, summaryY, pageWidth - sumCol1X - 60, 12));

                // ===== Marks-submission legend (only when enriched from the new system) =====
                if (hasProvisional)
                {
                    summaryY += 18;

                    DevExpress.XtraPrinting.TextBrick subLegendTitle = new DevExpress.XtraPrinting.TextBrick();
                    subLegendTitle.Text = "Marks Submission:";
                    subLegendTitle.Font = filterLabelFont;
                    subLegendTitle.ForeColor = darkGray;
                    subLegendTitle.Sides = DevExpress.XtraPrinting.BorderSide.None;
                    subLegendTitle.BackColor = System.Drawing.Color.Transparent;
                    gr.DrawBrick(subLegendTitle, new System.Drawing.RectangleF(sumCol1X, summaryY, 90, 12));

                    DevExpress.XtraPrinting.TextBrick lineNote = new DevExpress.XtraPrinting.TextBrick();
                    lineNote.Text = "Cell tint = submission status";
                    lineNote.Font = smallFont;
                    lineNote.ForeColor = lightGray;
                    lineNote.Sides = DevExpress.XtraPrinting.BorderSide.None;
                    lineNote.BackColor = System.Drawing.Color.Transparent;
                    gr.DrawBrick(lineNote, new System.Drawing.RectangleF(sumCol1X + 85, summaryY, 150, 12));

                    string[] subLabels = { "Published", "Approved", "Pending", "Rejected" };
                    float sx = sumCol2X + 30;
                    foreach (string lbl in subLabels)
                    {
                        DevExpress.XtraPrinting.TextBrick swatch = new DevExpress.XtraPrinting.TextBrick();
                        swatch.Text = "";
                        swatch.BackColor = GetSubmissionTint(lbl, System.Drawing.Color.White);
                        swatch.BorderColor = borderColor;
                        swatch.Sides = DevExpress.XtraPrinting.BorderSide.All;
                        swatch.BorderWidth = 1;
                        gr.DrawBrick(swatch, new System.Drawing.RectangleF(sx, summaryY, 12, 11));

                        DevExpress.XtraPrinting.TextBrick swatchLbl = new DevExpress.XtraPrinting.TextBrick();
                        swatchLbl.Text = lbl;
                        swatchLbl.Font = smallFont;
                        swatchLbl.ForeColor = lightGray;
                        swatchLbl.Sides = DevExpress.XtraPrinting.BorderSide.None;
                        swatchLbl.BackColor = System.Drawing.Color.Transparent;
                        gr.DrawBrick(swatchLbl, new System.Drawing.RectangleF(sx + 15, summaryY, 55, 12));

                        sx += 78;
                    }
                }

                y += summaryBoxHeight + 8;
                
                
                // ========== SIGNATURES ==========
                // Four approval signatories, side by side: a signing line on top, full designation below.
                y += 40; // leave room to sign above the lines
                // keep the whole signature row together on one page
                if ((y % pageHeight) + 34 > pageHeight)
                    y = ((float)Math.Floor(y / pageHeight) + 1) * pageHeight + 24;

                string[] sigLabels = new string[] { "Head of Department", "Dean", "Academic Registrar", "Deputy Vice Chancellor" };
                float sigColW = pageWidth / 4f;
                float sigGap = 20f; // inset so a line doesn't run into its neighbour
                System.Drawing.Font sigFont = new System.Drawing.Font("Tahoma", 7, System.Drawing.FontStyle.Bold);
                for (int sgi = 0; sgi < sigLabels.Length; sgi++)
                {
                    float colX = sgi * sigColW;

                    // Signing line (on top)
                    DevExpress.XtraPrinting.LineBrick sigLine = new DevExpress.XtraPrinting.LineBrick();
                    sigLine.ForeColor = darkGray;
                    sigLine.LineStyle = System.Drawing.Drawing2D.DashStyle.Solid;
                    sigLine.LineWidth = 1;
                    sigLine.Sides = DevExpress.XtraPrinting.BorderSide.None;
                    gr.DrawBrick(sigLine, new System.Drawing.RectangleF(colX + sigGap / 2f, y, sigColW - sigGap, 1));

                    // Designation (below the line), centered under the line
                    DevExpress.XtraPrinting.TextBrick sigText = new DevExpress.XtraPrinting.TextBrick();
                    sigText.Text = sigLabels[sgi];
                    sigText.Font = sigFont;
                    sigText.ForeColor = darkGray;
                    sigText.Sides = DevExpress.XtraPrinting.BorderSide.None;
                    sigText.BackColor = System.Drawing.Color.Transparent;
                    sigText.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(System.Drawing.StringAlignment.Center);
                    gr.DrawBrick(sigText, new System.Drawing.RectangleF(colX, y + 4, sigColW, 14));
                }
                y += 26;

                // ========== PROFESSIONAL FOOTER ==========

                // Thin line above footer
                DevExpress.XtraPrinting.LineBrick footerLine = new DevExpress.XtraPrinting.LineBrick();
                footerLine.ForeColor = lineColor;
                footerLine.LineStyle = System.Drawing.Drawing2D.DashStyle.Solid;
                footerLine.LineWidth = 1;
                footerLine.Sides = DevExpress.XtraPrinting.BorderSide.None;
                gr.DrawBrick(footerLine, new System.Drawing.RectangleF(0, y, pageWidth, 1));
                y += 6;
                
                // Footer text
                string footerText = universityName + "  |  Student Results Summary Report  |  Generated: " + DateTime.Now.ToString("dd MMM yyyy, HH:mm");
                DevExpress.XtraPrinting.TextBrick footerBrick = new DevExpress.XtraPrinting.TextBrick();
                footerBrick.Text = footerText;
                footerBrick.Font = smallFont;
                footerBrick.ForeColor = System.Drawing.Color.Gray;
                footerBrick.Sides = DevExpress.XtraPrinting.BorderSide.None;
                footerBrick.BackColor = System.Drawing.Color.Transparent;
                footerBrick.StringFormat = new DevExpress.XtraPrinting.BrickStringFormat(System.Drawing.StringAlignment.Center);
                gr.DrawBrick(footerBrick, new System.Drawing.RectangleF(0, y, pageWidth, 18));
            };
            
            link.CreateDocument();
            
            string fileName = string.Format("SummaryReport_{0}.pdf", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            
            // Clear all response content and set proper headers for PDF
            Response.Clear();
            Response.Buffer = true;
            Response.ClearHeaders();
            Response.ClearContent();
            Response.ContentType = "application/pdf";
            Response.AddHeader("Content-Disposition", "inline; filename=\"" + fileName + "\"");
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            
            using (MemoryStream ms = new MemoryStream())
            {
                ps.ExportToPdf(ms);
                byte[] pdfBytes = ms.ToArray();
                Response.AddHeader("Content-Length", pdfBytes.Length.ToString());
                Response.BinaryWrite(pdfBytes);
                Response.Flush();
            }
            
            try
            {
                Response.End();
            }
            catch (System.Threading.ThreadAbortException)
            {
                // Expected exception from Response.End() - ignore
            }
        }
    }
    
    #endregion
    
    private void BindStudentsGrid()
    {
        int pageSize = ParseQueryInt("size", 20, 10, 200);
        int page = ParseQueryInt("page", 1, 1, 1000000);
        int offset = (page - 1) * pageSize;

        // Fetch only the requested page from SQL (LIMIT/OFFSET) + a COUNT for the pager,
        // instead of loading all matching students and slicing in memory.
        int totalRows;
        DataTable pagedRows = GetStudentsData(offset, pageSize, out totalRows);

        int totalPages = totalRows > 0 ? (int)Math.Ceiling(totalRows / (double)pageSize) : 1;
        if (page > totalPages)
        {
            // Requested page is past the end — refetch the last valid page.
            page = totalPages;
            offset = (page - 1) * pageSize;
            pagedRows = GetStudentsData(offset, pageSize, out totalRows);
        }

        gvStudents.DataSource = pagedRows;
        gvStudents.DataBind();

        litPageInfo.Text = "Page " + page + " of " + totalPages + " | Total: " + totalRows.ToString("N0");
        litPager.Text = BuildPagerHtml(page, totalPages, pageSize);
    }

    // Returns the query-string value for the given key (trimmed) when present,
    // otherwise the supplied control value (trimmed). Used so grid filtering works
    // when the grid is bound in Page_Init, before postback controls are populated.
    private string QsOrCtl(string qsKey, string ctlValue)
    {
        string qs = Request.QueryString[qsKey];
        if (!string.IsNullOrEmpty(qs)) return qs.Trim();
        return (ctlValue ?? "").Trim();
    }

    private int ParseQueryInt(string key, int fallback, int min, int max)
    {
        int parsed;
        if (!int.TryParse(Request.QueryString[key], out parsed)) return fallback;
        if (parsed < min) return min;
        if (parsed > max) return max;
        return parsed;
    }

    private void ApplyFiltersFromQueryString()
    {
        string status = (Request.QueryString["status"] ?? "").Trim().ToUpper();
        string q = (Request.QueryString["q"] ?? "").Trim();
        string faculty = (Request.QueryString["faculty"] ?? "").Trim();
        string programme = (Request.QueryString["prog"] ?? "").Trim();
        string entryYear = (Request.QueryString["entryyear"] ?? "").Trim();
        string session = (Request.QueryString["session"] ?? "").Trim();
        string campus = (Request.QueryString["campus"] ?? "").Trim();

        txtSearch.Text = q;

        if (!string.IsNullOrEmpty(status) && ddlFilterStatus.Items.FindByValue(status) != null)
            ddlFilterStatus.SelectedValue = status;
        StatusFilter = status;

        if (!string.IsNullOrEmpty(faculty) && ddlFilterFaculty.Items.FindByValue(faculty) != null)
            ddlFilterFaculty.SelectedValue = faculty;

        LoadProgrammes(faculty);
        if (!string.IsNullOrEmpty(programme) && ddlFilterProgramme.Items.FindByValue(programme) != null)
            ddlFilterProgramme.SelectedValue = programme;

        if (!string.IsNullOrEmpty(entryYear) && ddlFilterEntryYear.Items.FindByValue(entryYear) != null)
            ddlFilterEntryYear.SelectedValue = entryYear;

        if (!string.IsNullOrEmpty(session) && ddlFilterSession.Items.FindByValue(session) != null)
            ddlFilterSession.SelectedValue = session;

        if (!string.IsNullOrEmpty(campus) && ddlFilterCampus.Items.FindByValue(campus) != null)
            ddlFilterCampus.SelectedValue = campus;

        if (litPageTitle != null)
            litPageTitle.Text = PageTitle;
    }

    private string BuildPagerHtml(int page, int totalPages, int pageSize)
    {
        if (totalPages <= 1) return string.Empty;

        StringBuilder sb = new StringBuilder();
        if (page > 1)
            sb.Append("<a href='" + BuildListUrl(page - 1, pageSize) + "'>&laquo; Prev</a>");

        int start = Math.Max(1, page - 2);
        int end = Math.Min(totalPages, start + 4);
        start = Math.Max(1, end - 4);

        for (int i = start; i <= end; i++)
        {
            if (i == page)
                sb.Append("<span class='active'>" + i + "</span>");
            else
                sb.Append("<a href='" + BuildListUrl(i, pageSize) + "'>" + i + "</a>");
        }

        if (page < totalPages)
            sb.Append("<a href='" + BuildListUrl(page + 1, pageSize) + "'>Next &raquo;</a>");

        return sb.ToString();
    }

    private string BuildListUrl(int page, int pageSize)
    {
        var qs = HttpUtility.ParseQueryString(string.Empty);

        string q = txtSearch != null ? (txtSearch.Text ?? "").Trim() : "";
        if (!string.IsNullOrEmpty(q)) qs["q"] = q;

        if (ddlFilterStatus != null && !string.IsNullOrEmpty(ddlFilterStatus.SelectedValue)) qs["status"] = ddlFilterStatus.SelectedValue;
        if (ddlFilterFaculty != null && !string.IsNullOrEmpty(ddlFilterFaculty.SelectedValue)) qs["faculty"] = ddlFilterFaculty.SelectedValue;
        if (ddlFilterProgramme != null && !string.IsNullOrEmpty(ddlFilterProgramme.SelectedValue)) qs["prog"] = ddlFilterProgramme.SelectedValue;
        if (ddlFilterEntryYear != null && !string.IsNullOrEmpty(ddlFilterEntryYear.SelectedValue)) qs["entryyear"] = ddlFilterEntryYear.SelectedValue;
        if (ddlFilterSession != null && !string.IsNullOrEmpty(ddlFilterSession.SelectedValue)) qs["session"] = ddlFilterSession.SelectedValue;
        if (ddlFilterCampus != null && !string.IsNullOrEmpty(ddlFilterCampus.SelectedValue)) qs["campus"] = ddlFilterCampus.SelectedValue;

        qs["page"] = page.ToString();
        qs["size"] = pageSize.ToString();

        return "NewStudentInfo.aspx?" + qs.ToString();
    }
    
    #region Filters Loading
    
    private void LoadFilters()
    {
        LoadFaculties();
        LoadProgrammes("");
        LoadEntryYears();
        LoadSessions();
        LoadCampuses();
    }
    
    private void LoadFaculties()
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT TRIM(faculty_code) AS faculty_code, TRIM(faculty_name) AS faculty_name FROM acad_faculty WHERE TRIM(faculty_name) <> '' ORDER BY faculty_name", conn))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string name = reader["faculty_name"].ToString().Trim();
                            string code = reader["faculty_code"].ToString().Trim();
                            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(code))
                                ddlFilterFaculty.Items.Add(new ListItem(name, code));
                        }
                    }
                }
            }
        }
        catch { }
    }
    
    private void LoadProgrammes(string facultyCode)
    {
        ddlFilterProgramme.Items.Clear();
        ddlFilterProgramme.Items.Add(new ListItem("All", ""));
        
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string sql;
                MySqlCommand cmd;
                
                if (string.IsNullOrEmpty(facultyCode))
                {
                    sql = "SELECT TRIM(progcode) AS progcode, CONCAT(TRIM(progcode), ' - ', TRIM(progname)) AS progname FROM acad_programme WHERE TRIM(progcode) <> '' ORDER BY progname";
                    cmd = new MySqlCommand(sql, conn);
                }
                else
                {
                    sql = "SELECT TRIM(progcode) AS progcode, CONCAT(TRIM(progcode), ' - ', TRIM(progname)) AS progname FROM acad_programme WHERE TRIM(faculty_code) = @faculty ORDER BY progname";
                    cmd = new MySqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@faculty", facultyCode.Trim());
                }
                
                using (cmd)
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ddlFilterProgramme.Items.Add(new ListItem(
                                reader["progname"].ToString(),
                                reader["progcode"].ToString().Trim()
                            ));
                        }
                    }
                }
            }
        }
        catch { }
    }
    
    private void LoadEntryYears()
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT DISTINCT TRIM(entryyear) AS entryyear FROM acad_student WHERE entryyear IS NOT NULL AND TRIM(entryyear) <> '' ORDER BY entryyear DESC", conn))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string year = reader["entryyear"].ToString().Trim();
                            if (!string.IsNullOrEmpty(year))
                                ddlFilterEntryYear.Items.Add(new ListItem(year, year));
                        }
                    }
                }
            }
        }
        catch { }
    }
    
    private void LoadSessions()
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT TRIM(Session) AS Session FROM acad_studysessions WHERE TRIM(Session) <> '' ORDER BY Session", conn))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string session = reader["Session"].ToString().Trim();
                            if (!string.IsNullOrEmpty(session))
                                ddlFilterSession.Items.Add(new ListItem(session, session));
                        }
                    }
                }
            }
        }
        catch { }
    }
    
    private void LoadCampuses()
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT TRIM(campus_code) AS campus_code, TRIM(campus_name) AS campus_name FROM acad_campuses WHERE TRIM(campus_name) <> '' ORDER BY campus_name", conn))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string name = reader["campus_name"].ToString().Trim();
                            string code = reader["campus_code"].ToString().Trim();
                            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(code))
                                ddlFilterCampus.Items.Add(new ListItem(name, code));
                        }
                    }
                }
            }
        }
        catch { }
    }
    
    #endregion
    
    #region Data Loading
    
    private DataTable GetStudentsData(int offset, int pageSize, out int totalRows)
    {
        totalRows = 0;
        DataTable dt = new DataTable();
        try
        {
            // Resolve current academic year once for the registration check
            string currentAcadYear = AcademicYearHelper.GetCurrentAcademicYear();

            // ---- Build the shared WHERE once, plus a param-adder reused by COUNT + data queries ----
            string where = " WHERE 1=1 ";

            // Filters are resolved from the QUERY STRING first (this page is GET-state driven,
            // and the grid is bound in Page_Init — before the filter controls are populated on a
            // postback), falling back to the control selection when a query param is absent.
            string searchTerm       = QsOrCtl("q",         txtSearch != null ? txtSearch.Text : "");
            string effectiveStatus  = QsOrCtl("status",    ddlFilterStatus != null ? ddlFilterStatus.SelectedValue : "");
            if (string.IsNullOrEmpty(effectiveStatus)) effectiveStatus = StatusFilter;
            string selectedFaculty   = QsOrCtl("faculty",  ddlFilterFaculty != null ? ddlFilterFaculty.SelectedValue : "");
            string selectedProgramme = QsOrCtl("prog",     ddlFilterProgramme != null ? ddlFilterProgramme.SelectedValue : "");
            string selectedEntryYear = QsOrCtl("entryyear",ddlFilterEntryYear != null ? ddlFilterEntryYear.SelectedValue : "");
            string selectedSession   = QsOrCtl("session",  ddlFilterSession != null ? ddlFilterSession.SelectedValue : "");
            string selectedCampus    = QsOrCtl("campus",   ddlFilterCampus != null ? ddlFilterCampus.SelectedValue : "");

            if (!string.IsNullOrEmpty(searchTerm))
            {
                where += @" AND (TRIM(s.firstname) LIKE @search
                           OR TRIM(s.othername) LIKE @search
                           OR TRIM(s.regno) LIKE @search
                           OR TRIM(s.entryno) LIKE @search
                           OR TRIM(s.studPhone) LIKE @search
                           OR TRIM(s.email) LIKE @search
                           OR CONCAT(COALESCE(TRIM(s.firstname),''), ' ', COALESCE(TRIM(s.othername),'')) LIKE @search) ";
            }

            if (!string.IsNullOrEmpty(effectiveStatus) && effectiveStatus != "ALL")
                where += " AND TRIM(s.new_status) = @status ";

            if (!string.IsNullOrEmpty(selectedProgramme))
                where += " AND TRIM(s.progid) = @progid ";
            else if (!string.IsNullOrEmpty(selectedFaculty))
                where += " AND TRIM(p.faculty_code) = @faculty ";

            if (!string.IsNullOrEmpty(selectedEntryYear))
                where += " AND TRIM(s.entryyear) = @entryyear ";
            if (!string.IsNullOrEmpty(selectedSession))
                where += " AND TRIM(s.studsesion) = @session ";

            if (!string.IsNullOrEmpty(selectedCampus))
                where += " AND TRIM(s.studCampus) = @campus ";

            // Adds exactly the params referenced in `where` (order-independent).
            Action<MySqlCommand> addWhereParams = delegate(MySqlCommand c)
            {
                if (!string.IsNullOrEmpty(searchTerm))
                    c.Parameters.AddWithValue("@search", "%" + searchTerm + "%");
                if (!string.IsNullOrEmpty(effectiveStatus) && effectiveStatus != "ALL")
                    c.Parameters.AddWithValue("@status", effectiveStatus.Trim());
                if (!string.IsNullOrEmpty(selectedProgramme))
                    c.Parameters.AddWithValue("@progid", selectedProgramme.Trim());
                else if (!string.IsNullOrEmpty(selectedFaculty))
                    c.Parameters.AddWithValue("@faculty", selectedFaculty.Trim());
                if (!string.IsNullOrEmpty(selectedEntryYear))
                    c.Parameters.AddWithValue("@entryyear", selectedEntryYear.Trim());
                if (!string.IsNullOrEmpty(selectedSession))
                    c.Parameters.AddWithValue("@session", selectedSession.Trim());
                if (!string.IsNullOrEmpty(selectedCampus))
                    c.Parameters.AddWithValue("@campus", selectedCampus.Trim());
            };

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                // ---- COUNT (total rows for the pager) — only needs acad_student + acad_programme (faculty filter) ----
                string countSql = "SELECT COUNT(*) FROM acad_student s LEFT JOIN acad_programme p ON s.progid = p.progcode" + where;
                using (MySqlCommand cc = new MySqlCommand(countSql, conn))
                {
                    cc.CommandTimeout = 60;
                    addWhereParams(cc);
                    object o = cc.ExecuteScalar();
                    totalRows = (o == null || o == DBNull.Value) ? 0 : Convert.ToInt32(o);
                }

                // ---- DATA (just the requested page) ----
                string dataSql = @"SELECT s.regno, s.entryno, s.firstname, s.othername, s.gender, s.dob,
                           s.nationality, s.religion, s.studPhone, s.email, s.home_dist,
                           s.national_id, s.progid, p.progcode, p.progname, s.specialisation,
                           COALESCE(sp.spec, NULLIF(TRIM(s.specialisation), ''), '-') AS spec_name,
                           s.entryyear, s.intake,
                           s.studsesion, s.studCampus, s.gradSystemID, s.completion_date, s.photofile, s.stud_status, s.new_status,
                           COALESCE(c.campus_name, s.studCampus, '-') AS campus_name,
                           COALESCE(s.has_passed, 'No') AS has_passed,
                           COALESCE(s.is_curriculum_fully_set, 'No') AS is_curriculum_fully_set,
                           s.fail_reason,
                           IF(r_curr.regno IS NOT NULL, 'Yes', 'No') AS is_registered
                           FROM acad_student s
                           LEFT JOIN acad_programme p ON s.progid = p.progcode
                           LEFT JOIN acad_specialisation sp ON s.specialisation = sp.spec_id
                           LEFT JOIN acad_campuses c ON s.studCampus = c.campus_code
                           LEFT JOIN (SELECT DISTINCT regno FROM acad_registration WHERE acad_year = @currentAcadYear) r_curr ON s.regno = r_curr.regno"
                           + where
                           + " ORDER BY s.entryyear DESC, s.firstname, s.othername LIMIT @off, @ps";

                using (MySqlCommand cmd = new MySqlCommand(dataSql, conn))
                {
                    cmd.CommandTimeout = 60;
                    cmd.Parameters.AddWithValue("@currentAcadYear", !string.IsNullOrEmpty(currentAcadYear) ? currentAcadYear : "0000/0000");
                    addWhereParams(cmd);
                    cmd.Parameters.AddWithValue("@off", offset < 0 ? 0 : offset);
                    cmd.Parameters.AddWithValue("@ps", pageSize < 1 ? 20 : pageSize);
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                        adapter.Fill(dt);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Error loading students: " + ex.Message);
        }

        // Update student count indicator with the TOTAL (not just this page)
        UpdateStudentCount(totalRows);

        return dt;
    }
    
    private void UpdateStudentCount(int count)
    {
        if (litStudentCount != null)
        {
            if (count == 0)
                litStudentCount.Text = "<span class='cd-filters__count'>No students found</span>";
            else
                litStudentCount.Text = string.Format("<span class='cd-filters__count'>{0} student{1}</span>", count, count == 1 ? "" : "s");
        }
    }
    
    #endregion
    
    #region Filter Events
    
    protected void btnSearch_Click(object sender, EventArgs e)
    {
        // Grid will rebind automatically via Page_Load with the search term
    }
    
    protected void btnResetFilters_Click(object sender, EventArgs e)
    {
        // Clear search
        txtSearch.Text = "";
        
        // Reset all dropdowns
        ddlFilterStatus.SelectedIndex = 0;
        ddlFilterFaculty.SelectedIndex = 0;
        ddlFilterProgramme.SelectedIndex = 0;
        ddlFilterEntryYear.SelectedIndex = 0;
        ddlFilterSession.SelectedIndex = 0;
        ddlFilterCampus.SelectedIndex = 0;
        
        // Reset status filter
        StatusFilter = "";
        
        // Reload programmes (in case faculty had filtered them)
        LoadProgrammes("");
        
        // Update page title
        if (litPageTitle != null)
            litPageTitle.Text = "Student Records";
    }
    
    protected void ddlFilterFaculty_SelectedIndexChanged(object sender, EventArgs e)
    {
        LoadProgrammes(ddlFilterFaculty.SelectedValue);
    }
    
    protected void ddlFilterProgramme_SelectedIndexChanged(object sender, EventArgs e)
    {
        // Grid will rebind automatically via Page_Load
    }
    
    protected void ddlFilterEntryYear_SelectedIndexChanged(object sender, EventArgs e)
    {
        // Grid will rebind automatically via Page_Load
    }
    
    protected void ddlFilterSession_SelectedIndexChanged(object sender, EventArgs e)
    {
        // Grid will rebind automatically via Page_Load
    }
    
    protected void ddlFilterCampus_SelectedIndexChanged(object sender, EventArgs e)
    {
        // Grid will rebind automatically via Page_Load
    }
    
    protected void ddlFilterStatus_SelectedIndexChanged(object sender, EventArgs e)
    {
        // Store the selected status in ViewState
        StatusFilter = ddlFilterStatus.SelectedValue;
        
        // Update page title
        if (litPageTitle != null)
            litPageTitle.Text = PageTitle;
    }
    
    #endregion
    
    #region Grid Events
    
    protected void gvStudents_RowUpdating(object sender, DevExpress.Web.Data.ASPxDataUpdatingEventArgs e)
    {
        try
        {
            // Validate required key
            object regnoKey = e.Keys["regno"];
            if (regnoKey == null || string.IsNullOrEmpty(regnoKey.ToString().Trim()))
                throw new Exception("Student regno is missing.");
            
            string regno = regnoKey.ToString().Trim();
            
            // Trim helper – safely gets a trimmed string from the new values
            // (Programme & Specialisation are intentionally excluded – use Full Edit)
            string firstname   = (e.NewValues["firstname"] ?? "").ToString().Trim();
            string othername   = (e.NewValues["othername"] ?? "").ToString().Trim();
            string gender      = (e.NewValues["gender"] ?? "").ToString().Trim();
            object dob         = e.NewValues["dob"] ?? DBNull.Value;
            string nationality = (e.NewValues["nationality"] ?? "").ToString().Trim();
            string religion    = (e.NewValues["religion"] ?? "").ToString().Trim();
            string phone       = (e.NewValues["studPhone"] ?? "").ToString().Trim();
            string email       = (e.NewValues["email"] ?? "").ToString().Trim();
            string district    = (e.NewValues["home_dist"] ?? "").ToString().Trim();
            string nin         = (e.NewValues["national_id"] ?? "").ToString().Trim();
            string entryyear   = (e.NewValues["entryyear"] ?? "").ToString().Trim();
            string intake      = (e.NewValues["intake"] ?? "").ToString().Trim();
            string session     = (e.NewValues["studsesion"] ?? "").ToString().Trim();
            string campus      = (e.NewValues["studCampus"] ?? "").ToString().Trim();
            string gradsystem  = (e.NewValues["gradSystemID"] ?? "").ToString().Trim();
            object compDate    = e.NewValues["completion_date"] ?? DBNull.Value;
            string newstatus   = (e.NewValues["new_status"] ?? "ADMITTED").ToString().Trim();
            
            // Basic validation
            if (string.IsNullOrEmpty(firstname))
                throw new Exception("First name is required.");
            
            // Validate email format if provided
            if (!string.IsNullOrEmpty(email) && !email.Contains("@"))
                throw new Exception("Invalid email format.");
            
            // ── Root-cause fix ─────────────────────────────────────────────
            // (1) STRICT SQL mode rejects '' for INT/DATE columns ("Incorrect
            //     integer/date value: ''"). This happened whenever a combo (campus,
            //     grading system) rendered blank because the stored value was not in
            //     its list, or dob/entryyear were cleared — the whole UPDATE failed.
            //     Fix: parse numerics/date; bind NULL for nullable columns; for the
            //     NOT NULL studCampus, omit it (keep the existing value) when blank.
            // (2) MySQL returns rows *changed*, so re-saving unchanged data gives
            //     rows==0 — the old code threw a false "no student" error. Fix:
            //     only error if the regno genuinely doesn't exist.
            int _iv;
            object pEntry = int.TryParse(entryyear, out _iv) ? (object)_iv : DBNull.Value;
            object pGrad  = int.TryParse(gradsystem, out _iv) ? (object)_iv : DBNull.Value;
            bool campusOk = int.TryParse(campus, out _iv); int campusVal = campusOk ? _iv : 0;
            object pDob = DBNull.Value; DateTime _dv;
            if (dob != null && dob != DBNull.Value && DateTime.TryParse(dob.ToString(), out _dv) && _dv.Year > 1900)
                pDob = _dv;
            // completion_date: blank/cleared -> NULL (transcript falls back to the auto June default);
            // a valid date -> stored and OVERRIDES the computed transcript completion date.
            object pComp = DBNull.Value; DateTime _cv;
            if (compDate != null && compDate != DBNull.Value && DateTime.TryParse(compDate.ToString(), out _cv) && _cv.Year > 1900)
                pComp = _cv.Date;

            System.Collections.Generic.List<string> sets = new System.Collections.Generic.List<string>();
            sets.Add("firstname=@firstname"); sets.Add("othername=@othername"); sets.Add("gender=@gender");
            sets.Add("dob=@dob"); sets.Add("nationality=@nationality"); sets.Add("religion=@religion");
            sets.Add("studPhone=@phone"); sets.Add("email=@email"); sets.Add("home_dist=@district");
            sets.Add("national_id=@nin"); sets.Add("entryyear=@entryyear"); sets.Add("intake=@intake");
            if (!string.IsNullOrEmpty(session)) sets.Add("studsesion=@session");
            if (campusOk) sets.Add("studCampus=@campus");
            sets.Add("gradSystemID=@gradsystem"); sets.Add("completion_date=@completion_date"); sets.Add("new_status=@newstatus");
            string sql = "UPDATE acad_student SET " + string.Join(", ", sets.ToArray()) + " WHERE regno=@regno";

            bool changed = false;
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.CommandTimeout = 30;
                    cmd.Parameters.AddWithValue("@regno", regno);
                    cmd.Parameters.AddWithValue("@firstname", firstname);
                    cmd.Parameters.AddWithValue("@othername", othername);
                    cmd.Parameters.AddWithValue("@gender", gender);
                    cmd.Parameters.AddWithValue("@dob", pDob);
                    cmd.Parameters.AddWithValue("@nationality", nationality);
                    cmd.Parameters.AddWithValue("@religion", religion);
                    cmd.Parameters.AddWithValue("@phone", phone);
                    cmd.Parameters.AddWithValue("@email", email);
                    cmd.Parameters.AddWithValue("@district", district);
                    cmd.Parameters.AddWithValue("@nin", nin);
                    cmd.Parameters.AddWithValue("@entryyear", pEntry);
                    cmd.Parameters.AddWithValue("@intake", intake);
                    if (!string.IsNullOrEmpty(session)) cmd.Parameters.AddWithValue("@session", session);
                    if (campusOk) cmd.Parameters.AddWithValue("@campus", campusVal);
                    cmd.Parameters.AddWithValue("@gradsystem", pGrad);
                    cmd.Parameters.AddWithValue("@completion_date", pComp);
                    cmd.Parameters.AddWithValue("@newstatus", newstatus);

                    int rows = cmd.ExecuteNonQuery();
                    changed = rows > 0;
                    if (rows == 0)
                    {
                        // 0 rows CHANGED — either identical values (fine) or a missing regno.
                        bool exists;
                        using (MySqlCommand chk = new MySqlCommand("SELECT COUNT(*) FROM acad_student WHERE regno=@r", conn))
                        { chk.Parameters.AddWithValue("@r", regno); exists = Convert.ToInt64(chk.ExecuteScalar()) > 0; }
                        if (!exists)
                            throw new Exception("No student found with regno " + regno + ".");
                        // exists but nothing changed -> treat as a successful save.
                    }
                }
            }

            e.Cancel = true;          // we ran our own UPDATE above
            gvStudents.CancelEdit();  // close the popup edit form
            BindStudentsGrid();       // re-render with the saved values
            ShowSaveToast(true, changed
                ? ("Student " + regno + " updated successfully.")
                : ("Student " + regno + " saved — no changes were needed."));
        }
        catch (Exception ex)
        {
            // Keep the edit form OPEN so the user can fix the input and retry; surface the reason
            // instead of throwing an unhandled exception (which showed a blank/error page).
            e.Cancel = true;
            ShowSaveToast(false, "Could not update student: " + ex.Message);
        }
    }

    /// <summary>
    /// Shows a floating success/error banner after an inline-edit postback. Injected via a startup
    /// script because this page runs full postbacks (grid callbacks are disabled) and ViewState is off.
    /// </summary>
    private void ShowSaveToast(bool ok, string message)
    {
        string safe = (message ?? "").Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", " ").Replace("\n", " ");
        string js = "if(window.cdToast){cdToast(" + (ok ? "true" : "false") + ",'" + safe + "');}";
        ScriptManager.RegisterStartupScript(this, GetType(), "cdSaveToast", js, true);
    }
    
    protected void gvStudents_RowDeleting(object sender, DevExpress.Web.Data.ASPxDataDeletingEventArgs e)
    {
        // Delete not allowed
        e.Cancel = true;
    }
    
    #endregion
    
    #region Student Profile Loading
    
    protected void btnLoadProfile_Click(object sender, EventArgs e)
    {
        string regno = hdnSelectedRegno.Value;
        if (string.IsNullOrEmpty(regno))
            return;
        
        try
        {
            LoadStudentCurriculum(regno);
            LoadStudentValidation(regno);
            LoadStudentBioData(regno);
            LoadStudentResults(regno);
            LoadAllResultsDirect(regno);  // Load direct results from acad_examresults_faculty
            LoadFacultyRegistrations(regno);
            LoadCourseRegistrations(regno);
            LoadFeesLedger(regno);
            LoadThesisTab(regno);
            
            // Show the popup - set ShowOnPageLoad for postback scenario
            popStudentProfile.ShowOnPageLoad = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Error in btnLoadProfile_Click: " + ex.Message);
        }
    }
    
    private void LoadStudentBioData(string regno)
    {
        try
        {
            // Simple query - just get student data with programme and campus
            string sql = @"SELECT s.*, 
                           COALESCE(p.progname, '') AS progname, 
                           COALESCE(p.progcode, '') AS progcode,
                           COALESCE(c.campus_name, s.studCampus, '-') AS campus_name
                           FROM acad_student s
                           LEFT JOIN acad_programme p ON s.progid = p.progcode
                           LEFT JOIN acad_campuses c ON s.studCampus = c.campus_code
                           WHERE s.regno = @regno
                           LIMIT 1";
            
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@regno", regno);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // Profile Header
                            string firstName = GetSafeString(reader, "firstname");
                            string otherName = GetSafeString(reader, "othername");
                            string fullName = (firstName + " " + otherName).Trim();
                            
                            litStudentName.Text = !string.IsNullOrEmpty(fullName) ? fullName : "-";
                            litRegNo.Text = GetSafeString(reader, "entryno", regno);
                            litProgramme.Text = GetSafeString(reader, "progname", "-");
                            litSpecialisation.Text = GetSafeString(reader, "specialisation", "-");
                            litEntryYear.Text = GetSafeString(reader, "entryyear", "-");
                            litSession.Text = GetSafeString(reader, "studsesion", "-");
                            litCampus.Text = GetSafeString(reader, "campus_name", "-");
                            litIntake.Text = GetSafeString(reader, "intake", "-");
                            
                            // Full Edit link
                            lnkFullEdit.HRef = "NewStudentRegistration.aspx?edit=" + Server.UrlEncode(regno)
                                + "&returnUrl=" + Server.UrlEncode("NewStudentInfo.aspx?status=ALL&search=" + Server.UrlEncode(regno));
                            
                            // Profile Photo
                            string photoFile = GetSafeString(reader, "photofile");
                            if (!string.IsNullOrEmpty(photoFile))
                                imgProfilePhoto.ImageUrl = ResolveUrl("~/COOPERP/StudentInfo/photos/" + photoFile);
                            else
                                imgProfilePhoto.ImageUrl = ResolveUrl("~/COOPERP/StudentInfo/photos/default.png");
                            
                            // Bio Data - Personal
                            litBioRegNo.Text = GetSafeString(reader, "entryno", "-");
                            litBioEntryNo.Text = GetSafeString(reader, "regno", "-");
                            litBioFullName.Text = !string.IsNullOrEmpty(fullName) ? fullName : "-";
                            litBioGender.Text = GetSafeString(reader, "gender", "-");
                            
                            if (reader["dob"] != DBNull.Value)
                            {
                                try
                                {
                                    DateTime dob = Convert.ToDateTime(reader["dob"]);
                                    litBioDOB.Text = dob.ToString("dd MMMM yyyy");
                                }
                                catch
                                {
                                    litBioDOB.Text = "-";
                                }
                            }
                            else
                            {
                                litBioDOB.Text = "-";
                            }
                            
                            litBioNationality.Text = GetSafeString(reader, "nationality", "-");
                            litBioReligion.Text = GetSafeString(reader, "religion", "-");
                            litBioDistrict.Text = GetSafeString(reader, "home_dist", "-");
                            litBioEntryMethod.Text = GetSafeString(reader, "entryMethod", "-");
                            
                            // Bio Data - Contact
                            litBioPhone.Text = GetSafeString(reader, "studPhone", "-");
                            litBioEmail.Text = GetSafeString(reader, "email", "-");
                            
                            // Bio Data - Academic
                            string progCode = GetSafeString(reader, "progcode");
                            string progName = GetSafeString(reader, "progname");
                            litBioProgramme.Text = !string.IsNullOrEmpty(progCode) ? progCode + " - " + progName : "-";
                            litBioSpecialisation.Text = GetSafeString(reader, "specialisation", "-");
                            litBioEntryYear.Text = GetSafeString(reader, "entryyear", "-");
                            litBioIntake.Text = GetSafeString(reader, "intake", "-");
                            litBioSession.Text = GetSafeString(reader, "studsesion", "-");
                            litBioCampus.Text = GetSafeString(reader, "campus_name", "-");
                            litBioGradingSystem.Text = GetSafeString(reader, "gradSystemID", "-");
                            
                            litBioDuration.Text = "-"; // Duration not available in current schema
                            
                            litBioHall.Text = GetSafeString(reader, "studentHall", "-");
                        }
                        else
                        {
                            // No student found - set defaults
                            litStudentName.Text = "Student Not Found";
                            litRegNo.Text = regno;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Error loading bio data: " + ex.Message);
            litStudentName.Text = "Error Loading Data";
            litRegNo.Text = ex.Message;
        }
    }
    
    private string GetSafeString(MySqlDataReader reader, string fieldName, string defaultValue = "")
    {
        try
        {
            int ordinal = reader.GetOrdinal(fieldName);
            if (!reader.IsDBNull(ordinal))
            {
                return reader.GetValue(ordinal).ToString();
            }
        }
        catch { }
        return defaultValue;
    }

        private List<string> BuildRegnoAliases(string regno)
        {
            var aliases = new List<string>();
            string value = (regno ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(value)) return aliases;

            aliases.Add(value);

            string[] parts = value.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 4)
            {
                for (int len = parts.Length - 1; len >= 4; len--)
                {
                    string candidate = string.Join("/", parts.Take(len).ToArray());
                    if (!aliases.Exists(a => string.Equals(a, candidate, StringComparison.OrdinalIgnoreCase)))
                        aliases.Add(candidate);
                }
            }

            return aliases;
        }

        private string GetAwardClassFromCgpa(decimal cgpa)
        {
            if (cgpa >= 4.40m) return "FIRST CLASS";
            if (cgpa >= 3.60m) return "SECOND CLASS UPPER";
            if (cgpa >= 2.80m) return "SECOND CLASS LOWER";
            if (cgpa >= 2.00m) return "PASS";
            return "RETAKE";
        }

        private void LoadStudentResultsFallback(DataTable dtAllResults, string regno)
        {
            if (dtAllResults == null || string.IsNullOrWhiteSpace(regno)) return;

            List<string> aliases = BuildRegnoAliases(regno);
            if (aliases.Count == 0) return;

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                StringBuilder sql = new StringBuilder();
                sql.Append(@"SELECT
                                COALESCE(r.studyyear, 0) AS studyyear,
                                COALESCE(r.semester, 0) AS semester,
                                COALESCE(r.courseid, '') AS courseid,
                                COALESCE(c.coursename, r.courseid, '') AS coursename,
                                COALESCE(NULLIF(r.CreditUnits, 0), NULLIF(c.credit_hrs, 0), 3) AS CreditUnits,
                                COALESCE(r.score, 0) AS score,
                                COALESCE(r.grade, '') AS grade,
                                COALESCE(r.gradept, 0) AS gradept,
                                COALESCE(r.gpa, 0) AS gpa,
                                COALESCE(r.acad, '') AS acad
                             FROM acad_results r
                             LEFT JOIN acad_course c ON c.courseid = r.courseid
                             WHERE ");

                var clauses = new List<string>();
                for (int i = 0; i < aliases.Count; i++)
                {
                    clauses.Add("TRIM(r.regno) = @reg" + i);
                    clauses.Add("TRIM(r.regno) LIKE @regp" + i);
                }

                sql.Append("(");
                sql.Append(string.Join(" OR ", clauses));
                sql.Append(") ORDER BY COALESCE(r.studyyear,0) ASC, COALESCE(r.semester,0) ASC, r.courseid ASC");

                using (MySqlCommand cmd = new MySqlCommand(sql.ToString(), conn))
                {
                    for (int i = 0; i < aliases.Count; i++)
                    {
                        cmd.Parameters.AddWithValue("@reg" + i, aliases[i]);
                        cmd.Parameters.AddWithValue("@regp" + i, aliases[i] + "/%");
                    }

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            DataRow row = dtAllResults.NewRow();
                            row["studyyear"] = reader["studyyear"] != DBNull.Value ? Convert.ToInt32(reader["studyyear"]) : 0;
                            row["semester"] = reader["semester"] != DBNull.Value ? Convert.ToInt32(reader["semester"]) : 0;
                            row["courseid"] = reader["courseid"] != DBNull.Value ? reader["courseid"].ToString() : "";
                            row["coursename"] = reader["coursename"] != DBNull.Value ? reader["coursename"].ToString() : "";
                            row["CreditUnits"] = reader["CreditUnits"] != DBNull.Value ? Convert.ToDecimal(reader["CreditUnits"]) : 0m;
                            row["score"] = reader["score"] != DBNull.Value ? Convert.ToDecimal(reader["score"]) : 0m;
                            row["grade"] = reader["grade"] != DBNull.Value ? reader["grade"].ToString() : "";
                            row["gradept"] = reader["gradept"] != DBNull.Value ? Convert.ToDecimal(reader["gradept"]) : 0m;
                            row["gpa"] = reader["gpa"] != DBNull.Value ? Convert.ToDecimal(reader["gpa"]) : 0m;
                            row["acad"] = reader["acad"] != DBNull.Value ? reader["acad"].ToString() : "";
                            dtAllResults.Rows.Add(row);
                        }
                    }
                }
            }
        }
    
    private void LoadStudentResults(string regno)
    {
        try
        {
            // Get all results using stored procedure - loop through all year/semester combinations
            DataTable dtAllResults = new DataTable();
            dtAllResults.Columns.Add("studyyear", typeof(int));
            dtAllResults.Columns.Add("semester", typeof(int));
            dtAllResults.Columns.Add("courseid", typeof(string));
            dtAllResults.Columns.Add("coursename", typeof(string));
            dtAllResults.Columns.Add("CreditUnits", typeof(decimal));
            dtAllResults.Columns.Add("score", typeof(decimal));
            dtAllResults.Columns.Add("grade", typeof(string));
            dtAllResults.Columns.Add("gradept", typeof(decimal));
            dtAllResults.Columns.Add("gpa", typeof(decimal));
            dtAllResults.Columns.Add("acad", typeof(string));
            
            int passed = 0;
            int failed = 0;
            decimal totalCredits = 0;
            decimal lastGPA = 0;
            decimal lastCGPA = 0;
            string lastAwardClass = "-";
            
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                
                // Loop through years 1-6 and semesters 1-4 to get all results
                for (int yr = 1; yr <= 6; yr++)
                {
                    for (int sem = 1; sem <= 4; sem++)
                    {
                        // Get semester results
                        using (MySqlCommand cmd = new MySqlCommand("acad_GetSemesterResults", conn))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.AddWithValue("@reg", regno);
                            cmd.Parameters.AddWithValue("@yr", yr);
                            cmd.Parameters.AddWithValue("@sem", sem);
                            
                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    DataRow row = dtAllResults.NewRow();
                                    row["studyyear"] = yr;
                                    row["semester"] = sem;
                                    row["courseid"] = reader["courseid"] != DBNull.Value ? reader["courseid"].ToString() : "";
                                    row["coursename"] = reader["coursename"] != DBNull.Value ? reader["coursename"].ToString() : "";
                                    row["CreditUnits"] = reader["CreditUnits"] != DBNull.Value ? Convert.ToDecimal(reader["CreditUnits"]) : 0;
                                    row["score"] = reader["score"] != DBNull.Value ? Convert.ToDecimal(reader["score"]) : 0;
                                    row["grade"] = reader["grade"] != DBNull.Value ? reader["grade"].ToString() : "";
                                    row["gradept"] = reader["gradept"] != DBNull.Value ? Convert.ToDecimal(reader["gradept"]) : 0;
                                    row["gpa"] = reader["gpa"] != DBNull.Value ? Convert.ToDecimal(reader["gpa"]) : 0;
                                    row["acad"] = reader["acad"] != DBNull.Value ? reader["acad"].ToString() : "";
                                    dtAllResults.Rows.Add(row);
                                    
                                    // Count passed/failed
                                    string grade = row["grade"].ToString();
                                    decimal credits = Convert.ToDecimal(row["CreditUnits"]);
                                    if (!string.IsNullOrEmpty(grade) && grade != "F" && grade != "X" && grade != "NE" && grade != "I")
                                    {
                                        passed++;
                                        totalCredits += credits;
                                    }
                                    else if (grade == "F" || grade == "X")
                                    {
                                        failed++;
                                    }
                                }
                            }
                        }
                        
                        // Get semester summary to get GPA, CGPA and Award Class
                        if (dtAllResults.Select(string.Format("studyyear = {0} AND semester = {1}", yr, sem)).Length > 0)
                        {
                            using (MySqlCommand cmdSum = new MySqlCommand("acad_SemesterSummary", conn))
                            {
                                cmdSum.CommandType = CommandType.StoredProcedure;
                                cmdSum.Parameters.AddWithValue("@reg", regno);
                                cmdSum.Parameters.AddWithValue("@yr", yr);
                                cmdSum.Parameters.AddWithValue("@sem", sem);
                                
                                using (MySqlDataReader reader = cmdSum.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        lastGPA = reader["gpa"] != DBNull.Value ? Convert.ToDecimal(reader["gpa"]) : 0;
                                        lastCGPA = reader["cgpa"] != DBNull.Value ? Convert.ToDecimal(reader["cgpa"]) : 0;
                                        lastAwardClass = reader["awardClass"] != DBNull.Value ? reader["awardClass"].ToString() : "-";
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Fallback: if stored procedures returned no rows, query acad_results directly
            // using regno aliases (supports shortened/full regno variants).
            if (dtAllResults.Rows.Count == 0)
            {
                LoadStudentResultsFallback(dtAllResults, regno);
            }

            // Recalculate summary metrics from the final dataset to keep values consistent
            passed = 0;
            failed = 0;
            totalCredits = 0;
            decimal weightedGpSum = 0;
            decimal weightedCreditSum = 0;

            foreach (DataRow row in dtAllResults.Rows)
            {
                string grade = row["grade"] != DBNull.Value ? row["grade"].ToString() : "";
                decimal credits = row["CreditUnits"] != DBNull.Value ? Convert.ToDecimal(row["CreditUnits"]) : 0m;
                decimal gp = row["gradept"] != DBNull.Value ? Convert.ToDecimal(row["gradept"]) : 0m;

                if (!string.IsNullOrEmpty(grade) && grade != "F" && grade != "X" && grade != "NE" && grade != "I")
                {
                    passed++;
                    totalCredits += credits;
                }
                else if (grade == "F" || grade == "X")
                {
                    failed++;
                }

                weightedGpSum += gp * credits;
                weightedCreditSum += credits;
            }

            if (dtAllResults.Rows.Count > 0)
            {
                // Latest semester GPA from available rows
                DataView semView = new DataView(dtAllResults);
                DataTable semesters = semView.ToTable(true, "studyyear", "semester");
                semesters.DefaultView.Sort = "studyyear DESC, semester DESC";
                if (semesters.DefaultView.Count > 0)
                {
                    int y = Convert.ToInt32(semesters.DefaultView[0]["studyyear"]);
                    int s = Convert.ToInt32(semesters.DefaultView[0]["semester"]);
                    DataRow[] latestRows = dtAllResults.Select(string.Format("studyyear = {0} AND semester = {1}", y, s));
                    decimal semGpSum = 0m;
                    decimal semCuSum = 0m;
                    foreach (DataRow r in latestRows)
                    {
                        decimal cu = r["CreditUnits"] != DBNull.Value ? Convert.ToDecimal(r["CreditUnits"]) : 0m;
                        decimal gpt = r["gradept"] != DBNull.Value ? Convert.ToDecimal(r["gradept"]) : 0m;
                        semGpSum += gpt * cu;
                        semCuSum += cu;
                    }
                    lastGPA = semCuSum > 0 ? semGpSum / semCuSum : 0m;
                }

                lastCGPA = weightedCreditSum > 0 ? weightedGpSum / weightedCreditSum : 0m;
                lastAwardClass = GetAwardClassFromCgpa(lastCGPA);
            }
            
            // Set summary stats
            litGPA.Text = lastGPA.ToString("F2");
            litCoursesPassed.Text = passed.ToString();
            litCoursesFailed.Text = failed.ToString();
            litTotalCredits.Text = totalCredits.ToString("0");
            
            if (dtAllResults.Rows.Count == 0)
            {
                pnlNoResults.Visible = true;
                rptResultsSemesters.Visible = false;
                litCGPA.Text = "0.00";
                litAwardClass.Text = "-";
            }
            else
            {
                pnlNoResults.Visible = false;
                rptResultsSemesters.Visible = true;
                
                // Group results by year/semester
                var semesterGroups = new List<object>();
                DataView view = new DataView(dtAllResults);
                DataTable distinctSemesters = view.ToTable(true, "studyyear", "semester");
                distinctSemesters.DefaultView.Sort = "studyyear ASC, semester ASC";
                
                foreach (DataRowView semRowView in distinctSemesters.DefaultView)
                {
                    DataRow semRow = semRowView.Row;
                    int year = Convert.ToInt32(semRow["studyyear"]);
                    int semester = Convert.ToInt32(semRow["semester"]);
                    
                    DataRow[] courseRows = dtAllResults.Select(string.Format("studyyear = {0} AND semester = {1}", year, semester));
                    if (courseRows.Length == 0) continue;
                    
                    var courses = new List<object>();
                    decimal semGP = 0;
                    decimal semCredits = 0;
                    
                    foreach (DataRow courseRow in courseRows)
                    {
                        decimal credits = Convert.ToDecimal(courseRow["CreditUnits"]);
                        decimal gp = Convert.ToDecimal(courseRow["gradept"]);
                        
                        semGP += gp * credits;
                        semCredits += credits;
                        
                        courses.Add(new {
                            course_code = courseRow["courseid"],
                            course_title = courseRow["coursename"],
                            credits = credits,
                            mark = courseRow["score"],
                            grade = courseRow["grade"],
                            gp = gp
                        });
                    }
                    
                    decimal semGPA = semCredits > 0 ? semGP / semCredits : 0;
                    
                    semesterGroups.Add(new {
                        year = year,
                        semester = semester,
                        gpa = semGPA,
                        courses = courses
                    });
                }
                
                litCGPA.Text = lastCGPA.ToString("F2");
                litAwardClass.Text = !string.IsNullOrEmpty(lastAwardClass) ? lastAwardClass : "-";
                
                rptResultsSemesters.DataSource = semesterGroups;
                rptResultsSemesters.DataBind();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Error loading results: " + ex.Message);
            pnlNoResults.Visible = true;
            rptResultsSemesters.Visible = false;
        }
    }
    
    /// <summary>
    /// Load all results directly from acad_examresults_faculty table (no filtering)
    /// This bypasses the stored procedure and shows all results regardless of approval status
    /// </summary>
    private void LoadAllResultsDirect(string regno)
    {
        try
        {
            DataTable dtAllResults = new DataTable();
            int passed = 0, failed = 0, pending = 0;
            decimal totalMarks = 0;
            decimal totalGP = 0;
            decimal totalCredits = 0;
            
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                
                // Query directly from acad_examresults_faculty - no approval filter
                string sql = @"SELECT 
                    er.ID,
                    er.course_id,
                    COALESCE(c.coursename, er.course_id) AS course_name,
                    COALESCE(c.credit_hrs, 3) AS credit_hours,
                    er.ca_mark,
                    er.exam_mark,
                    er.finalmark,
                    er.grade,
                    er.gradept,
                    er.acad_year,
                    er.semester,
                    er.approved_by,
                    CASE
                        WHEN er.finalmark >= 80 THEN 'A'
                        WHEN er.finalmark >= 75 THEN 'B+'
                        WHEN er.finalmark >= 70 THEN 'B'
                        WHEN er.finalmark >= 65 THEN 'C+'
                        WHEN er.finalmark >= 60 THEN 'C'
                        WHEN er.finalmark >= 55 THEN 'D+'
                        WHEN er.finalmark >= 50 THEN 'D'
                        ELSE 'F'
                    END AS computed_grade,
                    CASE
                        WHEN er.finalmark >= 80 THEN 5.0
                        WHEN er.finalmark >= 75 THEN 4.5
                        WHEN er.finalmark >= 70 THEN 4.0
                        WHEN er.finalmark >= 65 THEN 3.5
                        WHEN er.finalmark >= 60 THEN 3.0
                        WHEN er.finalmark >= 55 THEN 2.5
                        WHEN er.finalmark >= 50 THEN 2.0
                        ELSE 0.0
                    END AS computed_gp
                FROM acad_examresults_faculty er
                LEFT JOIN acad_course c ON c.courseid = er.course_id
                WHERE er.reg_no = @RegNo
                ORDER BY er.acad_year DESC, er.semester ASC, c.coursename";
                
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@RegNo", regno);
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        adapter.Fill(dtAllResults);
                    }
                }
            }
            
            if (dtAllResults.Rows.Count == 0)
            {
                pnlNoDirectResults.Visible = true;
                rptDirectResultsSemesters.Visible = false;
                litDirectTotalCourses.Text = "0";
                litDirectAvgMark.Text = "0.0";
                litDirectGPA.Text = "0.00";
                litDirectPassed.Text = "0";
                litDirectFailed.Text = "0";
                litDirectPending.Text = "0";
            }
            else
            {
                pnlNoDirectResults.Visible = false;
                rptDirectResultsSemesters.Visible = true;
                
                // Calculate statistics
                foreach (DataRow row in dtAllResults.Rows)
                {
                    decimal finalMark = row["finalmark"] != DBNull.Value ? Convert.ToDecimal(row["finalmark"]) : 0;
                    decimal credits = row["credit_hours"] != DBNull.Value ? Convert.ToDecimal(row["credit_hours"]) : 3;
                    decimal gp = row["computed_gp"] != DBNull.Value ? Convert.ToDecimal(row["computed_gp"]) : 0;
                    string approvedBy = row["approved_by"] != null && row["approved_by"] != DBNull.Value ? row["approved_by"].ToString() : "-";
                    
                    totalMarks += finalMark;
                    totalGP += gp * credits;
                    totalCredits += credits;
                    
                    if (approvedBy == "-" || string.IsNullOrEmpty(approvedBy))
                    {
                        pending++;
                    }
                    else if (finalMark >= 50)
                    {
                        passed++;
                    }
                    else
                    {
                        failed++;
                    }
                }
                
                // Set summary statistics
                litDirectTotalCourses.Text = dtAllResults.Rows.Count.ToString();
                litDirectAvgMark.Text = (dtAllResults.Rows.Count > 0 ? totalMarks / dtAllResults.Rows.Count : 0).ToString("F1");
                litDirectGPA.Text = (totalCredits > 0 ? totalGP / totalCredits : 0).ToString("F2");
                litDirectPassed.Text = passed.ToString();
                litDirectFailed.Text = failed.ToString();
                litDirectPending.Text = pending.ToString();
                
                // Group results by academic year and semester
                var semesterGroups = new List<object>();
                DataView view = new DataView(dtAllResults);
                DataTable distinctSemesters = view.ToTable(true, "acad_year", "semester");
                distinctSemesters.DefaultView.Sort = "acad_year DESC, semester ASC";
                
                foreach (DataRowView semRowView in distinctSemesters.DefaultView)
                {
                    string acadYear = semRowView["acad_year"] != null && semRowView["acad_year"] != DBNull.Value ? semRowView["acad_year"].ToString() : "";
                    int semester = semRowView["semester"] != DBNull.Value ? Convert.ToInt32(semRowView["semester"]) : 0;
                    
                    DataRow[] courseRows = dtAllResults.Select(
                        string.Format("acad_year = '{0}' AND semester = {1}", acadYear.Replace("'", "''"), semester));
                    
                    if (courseRows.Length == 0) continue;
                    
                    var courses = new List<object>();
                    decimal semGP = 0;
                    decimal semCredits = 0;
                    
                    foreach (DataRow courseRow in courseRows)
                    {
                        decimal credits = courseRow["credit_hours"] != DBNull.Value ? Convert.ToDecimal(courseRow["credit_hours"]) : 3;
                        decimal gp = courseRow["computed_gp"] != DBNull.Value ? Convert.ToDecimal(courseRow["computed_gp"]) : 0;
                        
                        semGP += gp * credits;
                        semCredits += credits;
                        
                        courses.Add(new {
                            course_code = courseRow["course_id"],
                            course_title = courseRow["course_name"],
                            credits = credits,
                            ca_mark = courseRow["ca_mark"] != DBNull.Value ? Convert.ToDecimal(courseRow["ca_mark"]).ToString("F0") : "-",
                            exam_mark = courseRow["exam_mark"] != DBNull.Value ? Convert.ToDecimal(courseRow["exam_mark"]).ToString("F0") : "-",
                            final_mark = courseRow["finalmark"] != DBNull.Value ? Convert.ToDecimal(courseRow["finalmark"]).ToString("F0") : "-",
                            grade = courseRow["computed_grade"] != null && courseRow["computed_grade"] != DBNull.Value ? courseRow["computed_grade"].ToString() : "-",
                            gp = gp,
                            approved_by = courseRow["approved_by"] != null && courseRow["approved_by"] != DBNull.Value ? courseRow["approved_by"].ToString() : "-"
                        });
                    }
                    
                    decimal semGPA = semCredits > 0 ? semGP / semCredits : 0;
                    
                    semesterGroups.Add(new {
                        acad_year = acadYear,
                        semester = semester,
                        course_count = courses.Count,
                        gpa = semGPA,
                        courses = courses
                    });
                }
                
                rptDirectResultsSemesters.DataSource = semesterGroups;
                rptDirectResultsSemesters.DataBind();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Error loading direct results: " + ex.Message);
            pnlNoDirectResults.Visible = true;
            rptDirectResultsSemesters.Visible = false;
        }
    }
    
    /// <summary>
    /// Get status badge HTML for direct results view
    /// </summary>
    protected string GetDirectStatusBadge(object approvedBy)
    {
        string status = approvedBy != null && approvedBy != DBNull.Value ? approvedBy.ToString() : "-";
        
        if (string.IsNullOrEmpty(status) || status == "-")
        {
            return "<span style=\"background:#ffc107;color:#000;padding:2px 6px;border-radius:3px;font-size:9px;font-weight:500;\">PENDING</span>";
        }
        else if (status == "HELD")
        {
            return "<span style=\"background:#dc3545;color:#fff;padding:2px 6px;border-radius:3px;font-size:9px;font-weight:500;\">HELD</span>";
        }
        else
        {
            return "<span style=\"background:#28a745;color:#fff;padding:2px 6px;border-radius:3px;font-size:9px;font-weight:500;\">APPROVED</span>";
        }
    }
    
    /// <summary>
    /// Print Provisional Results button click handler
    /// </summary>
    protected void btnPrintProvisional_Click(object sender, EventArgs e)
    {
        string regno = hdnSelectedRegno.Value;
        if (string.IsNullOrEmpty(regno))
            return;
            
        // Set session variables for the report
        Session["regno"] = regno;
        Session["Report"] = "ResultStatement";
        
        // Open report in new window using JavaScript
        string script = "window.open('../XtraReports/Default.aspx', 'ProvisionalResults', 'width=900,height=700,scrollbars=yes,resizable=yes');";
        ScriptManager.RegisterStartupScript(this, GetType(), "PrintProvisional", script, true);
    }
    
    /// <summary>
    /// Print Transcript button click handler
    /// </summary>
    protected void btnPrintTranscript_Click(object sender, EventArgs e)
    {
        string regno = hdnSelectedRegno.Value;
        if (string.IsNullOrEmpty(regno))
            return;
            
        // Set session variables for the transcript report
        Session["reg"] = regno;
        Session["Report"] = "Single Transcript";
        
        // Open report in new window using JavaScript
        string script = "window.open('../XtraReports/Default.aspx', 'Transcript', 'width=900,height=700,scrollbars=yes,resizable=yes');";
        ScriptManager.RegisterStartupScript(this, GetType(), "PrintTranscript", script, true);
    }
    
    private void LoadFacultyRegistrations(string regno)
    {
        try
        {
            // acad_registration table columns: ID, regno, acad_year, semester, regstatus, studyyear, 
            // id_cardStatus, residence_status, reg_CardStatus, examClearance, examClearanceDate, clearedBy, registeredBy
            string sql = @"SELECT acad_year as academic_year, semester, studyyear as study_year, 
                           regstatus as remarks, examClearanceDate as reg_date, 
                           CASE WHEN examClearance = 'YES' THEN 1 ELSE 0 END as exam_clearance, 
                           CASE WHEN reg_CardStatus = 'YES' THEN 1 ELSE 0 END as reg_clearance
                           FROM acad_registration
                           WHERE regno = @regno
                           ORDER BY acad_year DESC, semester DESC";
            
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@regno", regno);
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        adapter.Fill(dt);
                    }
                }
            }
            
            if (dt.Rows.Count == 0)
            {
                pnlNoRegistrations.Visible = true;
                rptRegistrations.Visible = false;
            }
            else
            {
                pnlNoRegistrations.Visible = false;
                rptRegistrations.Visible = true;
                rptRegistrations.DataSource = dt;
                rptRegistrations.DataBind();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Error loading registrations: " + ex.Message);
            pnlNoRegistrations.Visible = true;
            rptRegistrations.Visible = false;
        }
    }
    
    private void LoadCourseRegistrations(string regno)
    {
        try
        {
            // acad_course_registration columns: ID, regno, courseID, acad_year, semester, course_status, prog_id, stud_session
            string sql = @"SELECT cr.acad_year as academic_year, cr.semester, cr.courseID as course_code, 
                           c.CourseTitle as course_title, COALESCE(c.CreditUnits, 0) as credits,
                           COALESCE(cr.course_status, 'REGISTERED') as course_type, NULL as reg_date
                           FROM acad_course_registration cr
                           LEFT JOIN acad_courses c ON cr.courseID = c.CourseCode
                           WHERE cr.regno = @regno
                           ORDER BY cr.acad_year DESC, cr.semester DESC, cr.courseID";
            
            DataTable dtCourseReg = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@regno", regno);
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        adapter.Fill(dtCourseReg);
                    }
                }
            }
            
            if (dtCourseReg.Rows.Count == 0)
            {
                pnlNoCourseReg.Visible = true;
                rptCourseRegSemesters.Visible = false;
            }
            else
            {
                pnlNoCourseReg.Visible = false;
                rptCourseRegSemesters.Visible = true;
                
                // Group by academic year/semester
                var semesterGroups = new List<object>();
                DataView view = new DataView(dtCourseReg);
                DataTable distinctSemesters = view.ToTable(true, "academic_year", "semester");
                
                foreach (DataRow semRow in distinctSemesters.Rows)
                {
                    string acadYear = semRow["academic_year"] != DBNull.Value ? semRow["academic_year"].ToString() : "";
                    int semester = semRow["semester"] != DBNull.Value ? Convert.ToInt32(semRow["semester"]) : 0;
                    
                    DataRow[] courseRows = dtCourseReg.Select(string.Format("academic_year = '{0}' AND semester = {1}", acadYear, semester));
                    
                    var courses = new List<object>();
                    foreach (DataRow courseRow in courseRows)
                    {
                        courses.Add(new {
                            course_code = courseRow["course_code"],
                            course_title = courseRow["course_title"],
                            credits = courseRow["credits"],
                            course_type = courseRow["course_type"],
                            reg_date = courseRow["reg_date"]
                        });
                    }
                    
                    semesterGroups.Add(new {
                        academic_year = acadYear,
                        semester = semester,
                        course_count = courses.Count,
                        courses = courses
                    });
                }
                
                rptCourseRegSemesters.DataSource = semesterGroups;
                rptCourseRegSemesters.DataBind();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Error loading course registrations: " + ex.Message);
            pnlNoCourseReg.Visible = true;
            rptCourseRegSemesters.Visible = false;
        }
    }
    
    // Helper methods for formatting in repeater
    protected string FormatAmount(object amount)
    {
        if (amount == null || amount == DBNull.Value)
            return "-";
        
        try
        {
            decimal val = Convert.ToDecimal(amount);
            return val > 0 ? val.ToString("N0") : "-";
        }
        catch
        {
            return "-";
        }
    }
    
    protected string FormatBalance(object balance)
    {
        if (balance == null || balance == DBNull.Value)
            return "0";
        
        try
        {
            decimal val = Convert.ToDecimal(balance);
            return val.ToString("N0");
        }
        catch
        {
            return "0";
        }
    }
    
    // Connection string for accounts database (different from main database)
    private string AccountsConnectionString = ConfigurationManager.ConnectionStrings["accountsConnectionString"] != null
        ? ConfigurationManager.ConnectionStrings["accountsConnectionString"].ConnectionString
        : "Server=localhost;Database=campus_dynamics_accounts;Uid=root;Pwd=24thdecember1977;";
    
    private void LoadFeesLedger(string regno)
    {
        try
        {
            // Use fin_GetStudentLedger stored procedure — reads from fin_ledger (GL),
            // which is the canonical source of ALL transactions including manual
            // bank deposits and journal adjustments that fin_studentfeestracking misses.
            DataTable dtFees = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(AccountsConnectionString))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand("fin_GetStudentLedger", conn))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.CommandTimeout = 30;
                    cmd.Parameters.AddWithValue("@reg", regno);
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        adapter.Fill(dtFees);
                    }
                }
            }
            
            if (dtFees.Rows.Count == 0)
            {
                pnlNoFees.Visible = true;
                rptFeesLedger.Visible = false;
                litTotalInvoiced.Text = "0";
                litTotalPaid.Text = "0";
                litBalance.Text = "0";
            }
            else
            {
                pnlNoFees.Visible = false;
                rptFeesLedger.Visible = true;
                
                // Calculate totals from the stored procedure results
                decimal totalDebit = 0;
                decimal totalCredit = 0;
                decimal lastBalance = 0;
                
                // Create a new DataTable with proper column names for display
                DataTable displayTable = new DataTable();
                displayTable.Columns.Add("trans_date", typeof(string));
                displayTable.Columns.Add("reference", typeof(string));
                displayTable.Columns.Add("description", typeof(string));
                displayTable.Columns.Add("debit", typeof(decimal));
                displayTable.Columns.Add("credit", typeof(decimal));
                displayTable.Columns.Add("running_balance", typeof(decimal));
                
                foreach (DataRow srcRow in dtFees.Rows)
                {
                    DataRow newRow = displayTable.NewRow();
                    
                    // Map stored procedure columns to display columns
                    newRow["trans_date"] = srcRow.Table.Columns.Contains("formated_date") && srcRow["formated_date"] != DBNull.Value 
                        ? srcRow["formated_date"].ToString() : "";
                    newRow["reference"] = srcRow.Table.Columns.Contains("voucherNo") && srcRow["voucherNo"] != DBNull.Value 
                        ? srcRow["voucherNo"].ToString() : "";
                    newRow["description"] = srcRow.Table.Columns.Contains("particulars") && srcRow["particulars"] != DBNull.Value 
                        ? srcRow["particulars"].ToString() : "";
                    
                    decimal debit = 0;
                    decimal credit = 0;
                    
                    if (srcRow.Table.Columns.Contains("dr_amount") && srcRow["dr_amount"] != DBNull.Value)
                    {
                        decimal.TryParse(srcRow["dr_amount"].ToString(), out debit);
                    }
                    if (srcRow.Table.Columns.Contains("cr_amount") && srcRow["cr_amount"] != DBNull.Value)
                    {
                        decimal.TryParse(srcRow["cr_amount"].ToString(), out credit);
                    }
                    
                    newRow["debit"] = debit;
                    newRow["credit"] = credit;
                    
                    // Compute running balance in code — do NOT trust curr_balance
                    // from the SP which is unreliable (can be NULL or always 0).
                    // Running balance = cumulative (debit - credit) per accounting convention:
                    // debits (invoices) increase the balance owed; credits (payments) reduce it.
                    totalDebit += debit;
                    totalCredit += credit;
                    lastBalance = totalDebit - totalCredit;
                    newRow["running_balance"] = lastBalance;
                    
                    displayTable.Rows.Add(newRow);
                }
                
                litTotalInvoiced.Text = totalDebit.ToString("N0");
                litTotalPaid.Text = totalCredit.ToString("N0");
                litBalance.Text = (totalDebit - totalCredit).ToString("N0");
                
                rptFeesLedger.DataSource = displayTable;
                rptFeesLedger.DataBind();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Error loading fees ledger: " + ex.Message);
            pnlNoFees.Visible = true;
            rptFeesLedger.Visible = false;
        }
    }
    
    #region Thesis & Supervisor Tab
    
    private void LoadThesisTab(string regno)
    {
        try
        {
            // Get thesis info for this student
            DataRow thesisRow = GraduateHelper.GetThesisInfo(regno);
            
            if (thesisRow != null)
            {
                string thesisTitle = thesisRow["thesis_title"] != DBNull.Value 
                    ? thesisRow["thesis_title"].ToString().Trim() : "";
                string supervisorName = thesisRow["supervisor_name"] != DBNull.Value 
                    ? thesisRow["supervisor_name"].ToString().Trim() : "";
                string resStatus = thesisRow["res_status"] != DBNull.Value 
                    ? thesisRow["res_status"].ToString().Trim() : "";
                
                // Status badge
                string badgeCss = "sp-thesis-status-badge sp-thesis-status-badge--none";
                string statusText = "No Status";
                if (!string.IsNullOrEmpty(resStatus))
                {
                    statusText = resStatus;
                    if (resStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase) 
                        || resStatus.Equals("Defended", StringComparison.OrdinalIgnoreCase))
                        badgeCss = "sp-thesis-status-badge sp-thesis-status-badge--completed";
                    else
                        badgeCss = "sp-thesis-status-badge sp-thesis-status-badge--progress";
                }
                litThesisStatusBadge.Text = String.Format("<span class=\"{0}\">{1}</span>", badgeCss, statusText);
                
                // Current supervisor name
                litCurrentSupervisorName.Text = string.IsNullOrEmpty(supervisorName) ? "Not Assigned" : supervisorName;
                
                // Current thesis title display
                if (!string.IsNullOrEmpty(thesisTitle))
                {
                    litCurrentThesisTitle.Text = String.Format(
                        "<span class=\"sp-thesis-current__value\">{0}</span>",
                        System.Web.HttpUtility.HtmlEncode(thesisTitle));
                }
                else
                {
                    litCurrentThesisTitle.Text = 
                        "<span class=\"sp-thesis-current__value sp-thesis-current__value--empty\">No thesis title set</span>";
                }
                
                // Populate edit fields
                txtThesisTitleEdit.Text = thesisTitle;
                txtSupervisorEdit.Text = supervisorName;
                    
                // Set research status dropdown
                ListItem statusItem = ddlResearchStatus.Items.FindByValue(resStatus);
                if (statusItem != null)
                    ddlResearchStatus.SelectedValue = resStatus;
                else
                    ddlResearchStatus.SelectedIndex = 0;
                
                pnlNoThesis.Visible = false;
            }
            else
            {
                // No record — show empty state, but form is still available
                litThesisStatusBadge.Text = "<span class=\"sp-thesis-status-badge sp-thesis-status-badge--none\">No Record</span>";
                litCurrentSupervisorName.Text = "Not Assigned";
                litCurrentThesisTitle.Text = 
                    "<span class=\"sp-thesis-current__value sp-thesis-current__value--empty\">No thesis title set</span>";
                txtThesisTitleEdit.Text = "";
                txtSupervisorEdit.Text = "";
                ddlResearchStatus.SelectedIndex = 0;
                pnlNoThesis.Visible = true;
            }
            
            pnlThesisMessage.Visible = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Error loading thesis tab: " + ex.Message);
            litThesisStatusBadge.Text = "<span class=\"sp-thesis-status-badge sp-thesis-status-badge--none\">Error</span>";
            litCurrentSupervisorName.Text = "-";
            litCurrentThesisTitle.Text = 
                "<span class=\"sp-thesis-current__value sp-thesis-current__value--empty\">Error loading thesis data</span>";
        }
    }
    
    protected void btnSaveThesis_Click(object sender, EventArgs e)
    {
        string regno = hdnSelectedRegno.Value;
        if (string.IsNullOrEmpty(regno))
            return;
        
        try
        {
            string thesisTitle = txtThesisTitleEdit.Text.Trim();
            string supervisorName = txtSupervisorEdit.Text.Trim();
            string resStatus = ddlResearchStatus.SelectedValue;
            
            // Save thesis info using GraduateHelper (free-text supervisor)
            GraduateHelper.SaveThesisInfoText(regno, thesisTitle, supervisorName);
            
            // Also update research status if changed
            if (!string.IsNullOrEmpty(resStatus))
            {
                using (MySqlConnection conn = new MySqlConnection(ConnectionString))
                {
                    conn.Open();
                    using (MySqlCommand cmd = new MySqlCommand(
                        "UPDATE acad_graduate_research SET res_status = @status WHERE TRIM(regno) = @reg", conn))
                    {
                        cmd.Parameters.AddWithValue("@status", resStatus);
                        cmd.Parameters.AddWithValue("@reg", regno.Trim());
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            
            // Show success message
            pnlThesisMessage.Visible = true;
            litThesisMessage.Text = "<div class=\"sp-thesis-msg sp-thesis-msg--success\">Thesis information saved successfully.</div>";
            
            // Reload the tab to reflect changes
            LoadThesisTab(regno);
            
            // Keep popup open
            popStudentProfile.ShowOnPageLoad = true;
        }
        catch (Exception ex)
        {
            pnlThesisMessage.Visible = true;
            litThesisMessage.Text = String.Format(
                "<div class=\"sp-thesis-msg sp-thesis-msg--error\">Error saving thesis info: {0}</div>",
                System.Web.HttpUtility.HtmlEncode(ex.Message));
            popStudentProfile.ShowOnPageLoad = true;
        }
    }
    
    #endregion
    
    private void LoadStudentCurriculum(string regno)
    {
        try
        {
            string progCode = "";
            string progName = "";
            string studentSpecId = "";
            string studentSpecName = "";
            int defaultSpecId = 0;
            bool isDefaultSpec = false;
            
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                
                // Step 1: Get student's programme and specialisation
                string sql = @"SELECT s.progid, s.specialisation, 
                               COALESCE(p.progname, '') AS progname,
                               COALESCE(sp.spec, '') AS spec_name,
                               COALESCE(sp.spec_id, 0) AS spec_id
                               FROM acad_student s
                               LEFT JOIN acad_programme p ON s.progid = p.progcode
                               LEFT JOIN acad_specialisation sp ON s.specialisation = sp.spec_id
                               WHERE s.regno = @regno
                               LIMIT 1";
                
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@regno", regno);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            progCode = reader["progid"] != DBNull.Value ? reader["progid"].ToString() : "";
                            progName = reader["progname"] != DBNull.Value ? reader["progname"].ToString() : "";
                            studentSpecName = reader["spec_name"] != DBNull.Value ? reader["spec_name"].ToString() : "";
                            studentSpecId = reader["spec_id"] != DBNull.Value ? reader["spec_id"].ToString() : "0";
                        }
                    }
                }
                
                // Step 2: If student has no valid specialisation, get the default one for the programme
                int specIdToUse = 0;
                if (string.IsNullOrEmpty(studentSpecId) || studentSpecId == "0" || string.IsNullOrEmpty(studentSpecName) || studentSpecName == "-")
                {
                    // Get default specialisation for the programme
                    string defaultSql = @"SELECT spec_id, spec FROM acad_specialisation 
                                          WHERE prog_id = @progCode AND (spec = 'Default' OR spec LIKE '%Default%')
                                          ORDER BY spec_id LIMIT 1";
                    
                    using (MySqlCommand cmd = new MySqlCommand(defaultSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@progCode", progCode);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                defaultSpecId = Convert.ToInt32(reader["spec_id"]);
                                studentSpecName = reader["spec"] != DBNull.Value ? reader["spec"].ToString() : "Default";
                                specIdToUse = defaultSpecId;
                                isDefaultSpec = true;
                            }
                        }
                    }
                    
                    // If no default found, try to get the first specialisation for this programme
                    if (specIdToUse == 0)
                    {
                        string firstSql = @"SELECT spec_id, spec FROM acad_specialisation 
                                            WHERE prog_id = @progCode 
                                            ORDER BY spec_id LIMIT 1";
                        
                        using (MySqlCommand cmd = new MySqlCommand(firstSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@progCode", progCode);
                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    defaultSpecId = Convert.ToInt32(reader["spec_id"]);
                                    studentSpecName = reader["spec"] != DBNull.Value ? reader["spec"].ToString() : "Default";
                                    specIdToUse = defaultSpecId;
                                    isDefaultSpec = true;
                                }
                            }
                        }
                    }
                }
                else
                {
                    specIdToUse = Convert.ToInt32(studentSpecId);
                    isDefaultSpec = false;
                }
                
                // Update header info
                litCurrProgramme.Text = !string.IsNullOrEmpty(progName) ? progName : "-";
                litCurrProgCode.Text = progCode;
                litCurrSpecialisation.Text = !string.IsNullOrEmpty(studentSpecName) ? studentSpecName : "Not Assigned";
                
                if (isDefaultSpec)
                {
                    litCurrSpecBadge.Text = " <span class='sp-curriculum-badge sp-curriculum-badge--default'>Default</span>";
                    litCurrSpecNote.Text = "Student has no assigned specialisation - showing default curriculum";
                }
                else
                {
                    litCurrSpecBadge.Text = " <span class='sp-curriculum-badge sp-curriculum-badge--assigned'>Assigned</span>";
                    litCurrSpecNote.Text = "";
                }
                
                // Step 3: Load curriculum courses for this specialisation
                _sharedSpecIdToUse = specIdToUse; // Save for Validation tab to use
                if (specIdToUse > 0)
                {
                    LoadCurriculumCourses(conn, specIdToUse);
                }
                else
                {
                    // No curriculum available
                    litCurrTotalCourses.Text = "0";
                    litCurrTotalCredits.Text = "0";
                    litCurrCoreCourses.Text = "0";
                    litCurrElectives.Text = "0";
                    litCurriculumContent.Text = "";
                    pnlNoCurriculum.Visible = true;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Error loading curriculum: " + ex.Message);
            litCurrProgramme.Text = "-";
            litCurrSpecialisation.Text = "-";
            litCurriculumContent.Text = "";
            pnlNoCurriculum.Visible = true;
        }
    }
    
    private void LoadCurriculumCourses(MySqlConnection conn, int specId)
    {
        try
        {
            // Get all courses for this specialisation
            string sql = @"SELECT pc.study_year, pc.semester, pc.course_code, pc.course_type,
                           COALESCE(c.courseName, '') AS courseName, 
                           COALESCE(c.CreditUnit, 3) AS CreditUnit
                           FROM acad_programmecourses pc
                           LEFT JOIN acad_course c ON pc.course_code = c.courseID
                           WHERE pc.specialisation_id = @specId
                           ORDER BY pc.study_year, pc.semester, pc.course_code";
            
            DataTable dtCourses = new DataTable();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@specId", specId);
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd))
                {
                    da.Fill(dtCourses);
                }
            }
            
            if (dtCourses.Rows.Count == 0)
            {
                litCurrTotalCourses.Text = "0";
                litCurrTotalCredits.Text = "0";
                litCurrCoreCourses.Text = "0";
                litCurrElectives.Text = "0";
                litCurriculumContent.Text = "";
                pnlNoCurriculum.Visible = true;
                return;
            }
            
            pnlNoCurriculum.Visible = false;
            
            // Calculate totals
            int totalCourses = dtCourses.Rows.Count;
            int totalCredits = 0;
            int coreCourses = 0;
            int electives = 0;
            
            foreach (DataRow row in dtCourses.Rows)
            {
                int credits = row["CreditUnit"] != DBNull.Value ? Convert.ToInt32(row["CreditUnit"]) : 0;
                totalCredits += credits;
                
                string courseType = row["course_type"] != DBNull.Value ? row["course_type"].ToString().ToUpper() : "CORE";
                if (courseType == "ELECTIVE")
                    electives++;
                else
                    coreCourses++;
            }
            
            litCurrTotalCourses.Text = totalCourses.ToString();
            litCurrTotalCredits.Text = totalCredits.ToString();
            litCurrCoreCourses.Text = coreCourses.ToString();
            litCurrElectives.Text = electives.ToString();
            
            // Group by year and semester and build HTML
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            
            // Find max year
            int maxYear = 1;
            foreach (DataRow row in dtCourses.Rows)
            {
                int year = row["study_year"] != DBNull.Value ? Convert.ToInt32(row["study_year"]) : 1;
                if (year > maxYear) maxYear = year;
            }
            
            // Build curriculum grid by year
            for (int year = 1; year <= maxYear; year++)
            {
                sb.Append("<div class='sp-curriculum-year'>");
                sb.Append("<div class='sp-curriculum-year__header'>Year " + year + "</div>");
                
                // Loop through semesters 1-2
                for (int sem = 1; sem <= 2; sem++)
                {
                    // Get courses for this year/semester
                    DataRow[] semesterCourses = dtCourses.Select("study_year = " + year + " AND semester = " + sem);
                    
                    int semCredits = 0;
                    foreach (DataRow row in semesterCourses)
                    {
                        semCredits += row["CreditUnit"] != DBNull.Value ? Convert.ToInt32(row["CreditUnit"]) : 0;
                    }
                    
                    sb.Append("<div class='sp-curriculum-semester'>");
                    sb.Append("<div class='sp-curriculum-semester__header'>");
                    sb.Append("<span>Semester " + sem + "</span>");
                    sb.Append("<span class='sp-curriculum-semester__credits'>" + semCredits + " CU</span>");
                    sb.Append("</div>");
                    
                    if (semesterCourses.Length > 0)
                    {
                        foreach (DataRow row in semesterCourses)
                        {
                            string courseCode = row["course_code"].ToString();
                            string courseName = row["courseName"] != DBNull.Value ? row["courseName"].ToString() : "";
                            int credits = row["CreditUnit"] != DBNull.Value ? Convert.ToInt32(row["CreditUnit"]) : 0;
                            string courseType = row["course_type"] != DBNull.Value ? row["course_type"].ToString().ToUpper() : "CORE";
                            
                            string typeClass = courseType == "ELECTIVE" ? "sp-curriculum-course__type--elective" : "sp-curriculum-course__type--core";
                            
                            sb.Append("<div class='sp-curriculum-course'>");
                            sb.Append("<span class='sp-curriculum-course__code'>" + Server.HtmlEncode(courseCode) + "</span>");
                            sb.Append("<span class='sp-curriculum-course__name'>" + Server.HtmlEncode(courseName) + "</span>");
                            sb.Append("<span class='sp-curriculum-course__credits'>" + credits + " CU</span>");
                            sb.Append("<span class='sp-curriculum-course__type " + typeClass + "'>" + courseType + "</span>");
                            sb.Append("</div>");
                        }
                    }
                    else
                    {
                        sb.Append("<div class='sp-curriculum-empty'>No courses assigned</div>");
                    }
                    
                    sb.Append("</div>"); // End semester
                }
                
                sb.Append("</div>"); // End year
            }
            
            litCurriculumContent.Text = sb.ToString();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Error loading curriculum courses: " + ex.Message);
            litCurriculumContent.Text = "";
            pnlNoCurriculum.Visible = true;
        }
    }
    
    /// <summary>
    /// VALIDATION TAB - Results Validation Against Curriculum
    /// =========================================================
    /// This method validates a student's academic results against their programme curriculum.
    /// 
    /// HOW IT WORKS:
    /// 1. Gets the student's programme and specialisation from acad_student table
    /// 2. If student has no assigned specialisation, uses the "Default" specialisation for the programme
    /// 3. If no Default exists, uses the first available specialisation for the programme
    /// 4. Loads curriculum courses from acad_programmecourses for the determined specialisation
    /// 5. Counts student's results from acad_results for each year/semester
    /// 6. Compares: If student results count >= curriculum courses count = PASS, else FAIL
    /// 
    /// PASS CRITERIA:
    /// - Each semester: Student must have results for AT LEAST as many courses as defined in curriculum
    /// - Overall: ALL semesters must pass for overall PASS status
    /// 
    /// TABLES USED:
    /// - acad_student: Student's programme (progid) and specialisation
    /// - acad_specialisation: Specialisation definitions (spec_id, prog_id, spec name)
    /// - acad_programmecourses: Curriculum courses per specialisation (study_year, semester, course_code)
    /// - acad_results: Student's actual results (regno, studyyear, semester, grade)
    /// 
    /// Created: February 2026
    /// </summary>
    private void LoadStudentValidation(string regno)
    {
        try
        {
            string progCode = "";
            string progName = "";
            string studentSpecId = "";
            string studentSpecName = "";
            string isFullySet = "No"; // Track if curriculum is fully set
            int defaultSpecId = 0;
            
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                
                // Step 1: Get student's programme and specialisation (including is_fully_set)
                string sql = @"SELECT s.progid, s.specialisation, 
                               COALESCE(p.progname, '') AS progname,
                               COALESCE(sp.spec, '') AS spec_name,
                               COALESCE(sp.spec_id, 0) AS spec_id,
                               COALESCE(sp.is_fully_set, 'No') AS is_fully_set
                               FROM acad_student s
                               LEFT JOIN acad_programme p ON s.progid = p.progcode
                               LEFT JOIN acad_specialisation sp ON s.specialisation = sp.spec_id
                               WHERE s.regno = @regno
                               LIMIT 1";
                
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@regno", regno);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            progCode = reader["progid"] != DBNull.Value ? reader["progid"].ToString() : "";
                            progName = reader["progname"] != DBNull.Value ? reader["progname"].ToString() : "";
                            studentSpecName = reader["spec_name"] != DBNull.Value ? reader["spec_name"].ToString() : "";
                            studentSpecId = reader["spec_id"] != DBNull.Value ? reader["spec_id"].ToString() : "0";
                            isFullySet = reader["is_fully_set"] != DBNull.Value ? reader["is_fully_set"].ToString() : "No";
                        }
                    }
                }
                
                // Step 2: If student has no valid specialisation, get the default one for the programme
                int specIdToUse = 0;
                if (string.IsNullOrEmpty(studentSpecId) || studentSpecId == "0" || string.IsNullOrEmpty(studentSpecName) || studentSpecName == "-")
                {
                    // Get default specialisation for the programme (including is_fully_set)
                    string defaultSql = @"SELECT spec_id, spec, COALESCE(is_fully_set, 'No') AS is_fully_set FROM acad_specialisation 
                                          WHERE prog_id = @progCode AND (spec = 'Default' OR spec LIKE '%Default%')
                                          ORDER BY spec_id LIMIT 1";
                    
                    using (MySqlCommand cmd = new MySqlCommand(defaultSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@progCode", progCode);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                defaultSpecId = Convert.ToInt32(reader["spec_id"]);
                                studentSpecName = reader["spec"] != DBNull.Value ? reader["spec"].ToString() : "Default";
                                isFullySet = reader["is_fully_set"] != DBNull.Value ? reader["is_fully_set"].ToString() : "No";
                                specIdToUse = defaultSpecId;
                            }
                        }
                    }
                    
                    // If no default found, try to get the first specialisation for this programme
                    if (specIdToUse == 0)
                    {
                        string firstSql = @"SELECT spec_id, spec, COALESCE(is_fully_set, 'No') AS is_fully_set FROM acad_specialisation 
                                            WHERE prog_id = @progCode 
                                            ORDER BY spec_id LIMIT 1";
                        
                        using (MySqlCommand cmd = new MySqlCommand(firstSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@progCode", progCode);
                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    defaultSpecId = Convert.ToInt32(reader["spec_id"]);
                                    studentSpecName = reader["spec"] != DBNull.Value ? reader["spec"].ToString() : "Default";
                                    isFullySet = reader["is_fully_set"] != DBNull.Value ? reader["is_fully_set"].ToString() : "No";
                                    specIdToUse = defaultSpecId;
                                }
                            }
                        }
                    }
                }
                else
                {
                    specIdToUse = Convert.ToInt32(studentSpecId);
                }
                
                // Update header info
                litChkProgramme.Text = !string.IsNullOrEmpty(progName) ? progName : "-";
                litChkProgCode.Text = progCode;
                litChkSpecialisation.Text = !string.IsNullOrEmpty(studentSpecName) ? studentSpecName : "Not Assigned";
                
                // Show curriculum fully set badge
                if (isFullySet == "Yes")
                {
                    litChkSpecBadge.Text = " <span style='display:inline-block;background:#28a745;color:white;font-size:9px;padding:2px 6px;border-radius:3px;font-weight:600;vertical-align:middle;'>CURRICULUM SET</span>";
                }
                else
                {
                    litChkSpecBadge.Text = " <span style='display:inline-block;background:#dc3545;color:white;font-size:9px;padding:2px 6px;border-radius:3px;font-weight:600;vertical-align:middle;'>CURRICULUM NOT SET</span>";
                }
                litChkSpecNote.Text = "";
                
                // Step 3: Load and validate curriculum courses
                if (specIdToUse > 0)
                {
                    LoadValidationCourses(conn, specIdToUse, regno);
                }
                else
                {
                    // No curriculum available
                    litChkTotalCourses.Text = "0";
                    litChkTotalCredits.Text = "0";
                    litChkCoreCourses.Text = "0";
                    litChkElectives.Text = "0";
                    litCheckerContent.Text = "";
                    pnlNoChecker.Visible = true;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Error loading checker: " + ex.Message);
            litChkProgramme.Text = "-";
            litChkSpecialisation.Text = "-";
            litCheckerContent.Text = "";
            pnlNoChecker.Visible = true;
        }
    }
    
    /// <summary>
    /// Loads curriculum courses and compares against student results for validation.
    /// For each semester: counts curriculum courses vs student results to determine pass/fail.
    /// </summary>
    private void LoadValidationCourses(MySqlConnection conn, int specId, string regno)
    {
        try
        {
            // Get all curriculum courses for this specialisation
            string sql = @"SELECT pc.study_year, pc.semester, pc.course_code, pc.course_type,
                           COALESCE(c.courseName, '') AS courseName, 
                           COALESCE(c.CreditUnit, 3) AS CreditUnit
                           FROM acad_programmecourses pc
                           LEFT JOIN acad_course c ON pc.course_code = c.courseID
                           WHERE pc.specialisation_id = @specId
                           ORDER BY pc.study_year, pc.semester, pc.course_code";
            
            DataTable dtCourses = new DataTable();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@specId", specId);
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd))
                {
                    da.Fill(dtCourses);
                }
            }
            
            if (dtCourses.Rows.Count == 0)
            {
                litChkTotalCourses.Text = "0";
                litChkTotalCredits.Text = "0";
                litChkCoreCourses.Text = "0";
                litChkElectives.Text = "0";
                litCheckerContent.Text = "";
                pnlNoChecker.Visible = true;
                return;
            }
            
            pnlNoChecker.Visible = false;
            
            // Get student's results count per semester
            string resultsSql = @"SELECT studyyear, semester, COUNT(*) AS results_count
                                  FROM acad_results
                                  WHERE regno = @regno AND grade IS NOT NULL AND grade != ''
                                  GROUP BY studyyear, semester";
            
            DataTable dtResults = new DataTable();
            using (MySqlCommand cmd = new MySqlCommand(resultsSql, conn))
            {
                cmd.Parameters.AddWithValue("@regno", regno);
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd))
                {
                    da.Fill(dtResults);
                }
            }
            
            // Calculate totals
            int totalCourses = dtCourses.Rows.Count;
            int totalCredits = 0;
            int coreCourses = 0;
            int electives = 0;
            
            foreach (DataRow row in dtCourses.Rows)
            {
                int credits = row["CreditUnit"] != DBNull.Value ? Convert.ToInt32(row["CreditUnit"]) : 0;
                totalCredits += credits;
                
                string courseType = row["course_type"] != DBNull.Value ? row["course_type"].ToString().ToUpper() : "CORE";
                if (courseType == "ELECTIVE")
                    electives++;
                else
                    coreCourses++;
            }
            
            litChkTotalCourses.Text = totalCourses.ToString();
            litChkTotalCredits.Text = totalCredits.ToString();
            litChkCoreCourses.Text = coreCourses.ToString();
            litChkElectives.Text = electives.ToString();
            
            // Group by year and semester and build HTML
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            
            // Find max year
            int maxYear = 1;
            foreach (DataRow row in dtCourses.Rows)
            {
                int year = row["study_year"] != DBNull.Value ? Convert.ToInt32(row["study_year"]) : 1;
                if (year > maxYear) maxYear = year;
            }
            
            // Track overall pass/fail
            int semestersPassed = 0;
            int semestersFailed = 0;
            int semestersTotal = 0;
            
            // Build curriculum grid by year
            for (int year = 1; year <= maxYear; year++)
            {
                sb.Append("<div class='sp-curriculum-year'>");
                sb.Append("<div class='sp-curriculum-year__header'>Year " + year + "</div>");
                
                // Loop through semesters 1-2
                for (int sem = 1; sem <= 2; sem++)
                {
                    // Get courses for this year/semester
                    DataRow[] semesterCourses = dtCourses.Select("study_year = " + year + " AND semester = " + sem);
                    
                    if (semesterCourses.Length == 0) continue; // Skip if no curriculum courses
                    
                    semestersTotal++;
                    
                    int semCredits = 0;
                    foreach (DataRow row in semesterCourses)
                    {
                        semCredits += row["CreditUnit"] != DBNull.Value ? Convert.ToInt32(row["CreditUnit"]) : 0;
                    }
                    
                    // Get student results for this semester
                    DataRow[] resultRows = dtResults.Select("studyyear = " + year + " AND semester = " + sem);
                    int studentResults = resultRows.Length > 0 ? Convert.ToInt32(resultRows[0]["results_count"]) : 0;
                    
                    // Determine pass/fail
                    bool isPassed = studentResults >= semesterCourses.Length;
                    if (isPassed) semestersPassed++; else semestersFailed++;
                    
                    sb.Append("<div class='sp-curriculum-semester'>");
                    sb.Append("<div class='sp-curriculum-semester__header'>");
                    sb.Append("<span>Semester " + sem + "</span>");
                    sb.Append("<span class='sp-curriculum-semester__credits'>" + semCredits + " CU</span>");
                    sb.Append("</div>");
                    
                    // Compact single row with curriculum, results, and status
                    string statusBg = isPassed ? "#d4edda" : "#f8d7da";
                    string statusColor = isPassed ? "#28a745" : "#dc3545";
                    string statusIcon = isPassed ? "&#10004;" : "&#10008;";
                    string statusText = isPassed ? "PASS" : "FAIL";
                    
                    sb.Append("<div style='display:flex;align-items:center;justify-content:space-between;padding:6px 10px;font-size:11px;background:" + statusBg + ";'>");
                    sb.Append("<span><b style='color:#174DA4;'>Curriculum:</b> " + semesterCourses.Length + "</span>");
                    sb.Append("<span><b style='color:#666;'>Results:</b> " + studentResults + "</span>");
                    sb.Append("<span style='font-weight:700;color:" + statusColor + ";'>" + statusIcon + " " + statusText + "</span>");
                    sb.Append("</div>");
                    
                    sb.Append("</div>"); // End semester
                }
                
                sb.Append("</div>"); // End year
            }
            
            // Build overall status header - compact design
            string overallHtml = "";
            if (semestersTotal > 0)
            {
                if (semestersFailed == 0)
                {
                    overallHtml = "<div style='display:flex;align-items:center;justify-content:center;gap:10px;background:#d4edda;border:1px solid #28a745;padding:8px 16px;margin:0 0 12px 0;border-radius:4px;'><span style='color:#28a745;font-weight:700;font-size:13px;'>&#10004; OVERALL: PASS</span><span style='color:#155724;font-size:11px;'>(" + semestersPassed + "/" + semestersTotal + " semesters)</span></div>";
                }
                else
                {
                    overallHtml = "<div style='display:flex;align-items:center;justify-content:center;gap:10px;background:#f8d7da;border:1px solid #dc3545;padding:8px 16px;margin:0 0 12px 0;border-radius:4px;'><span style='color:#dc3545;font-weight:700;font-size:13px;'>&#10008; OVERALL: FAILED</span><span style='color:#721c24;font-size:11px;'>(" + semestersFailed + "/" + semestersTotal + " semesters need attention)</span></div>";
                }
            }
            
            litCheckerContent.Text = overallHtml + sb.ToString();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Error loading checker courses: " + ex.Message);
            litCheckerContent.Text = "";
            pnlNoChecker.Visible = true;
        }
    }
    
    #endregion
    
    #region Set Password

    private void HandleSetPhoto()
    {
        JavaScriptSerializer serializer = new JavaScriptSerializer();

        try
        {
            string regno = (Request.Form["regno"] ?? string.Empty).Trim();
            HttpPostedFile postedFile = Request.Files["photoFile"];

            if (string.IsNullOrEmpty(regno))
            {
                WriteJsonAndComplete(serializer, new { success = false, message = "Registration number is required." });
                return;
            }

            if (postedFile == null || postedFile.ContentLength <= 0)
            {
                WriteJsonAndComplete(serializer, new { success = false, message = "Please select a photo to upload." });
                return;
            }

            string ext = Path.GetExtension(postedFile.FileName ?? string.Empty).ToLowerInvariant();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".bmp" && ext != ".gif")
            {
                WriteJsonAndComplete(serializer, new { success = false, message = "Only image files are allowed (jpg, jpeg, png, bmp, gif)." });
                return;
            }

            byte[] originalBytes;
            using (Stream input = postedFile.InputStream)
            {
                int length = Convert.ToInt32(input.Length);
                originalBytes = new byte[length];
                input.Read(originalBytes, 0, length);
            }

            imageManager im = new imageManager();
            byte[] thumbBytes = im.MakeThumb(originalBytes);

            string fileName = Guid.NewGuid().ToString("N") + ".jpg";
            string photosFolder = Server.MapPath("~/COOPERP/StudentInfo/photos/");
            if (!Directory.Exists(photosFolder))
            {
                Directory.CreateDirectory(photosFolder);
            }

            string savePath = Path.Combine(photosFolder, fileName);
            File.WriteAllBytes(savePath, thumbBytes);

            int rows;
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand("UPDATE acad_student SET photofile = @photofile WHERE regno = @regno", conn))
                {
                    cmd.Parameters.AddWithValue("@photofile", fileName);
                    cmd.Parameters.AddWithValue("@regno", regno);
                    rows = cmd.ExecuteNonQuery();
                }
            }

            if (rows <= 0)
            {
                WriteJsonAndComplete(serializer, new { success = false, message = "Student not found. Photo was not updated." });
                return;
            }

            WriteJsonAndComplete(serializer, new
            {
                success = true,
                message = "Photo updated successfully.",
                photoUrl = ResolveUrl("~/COOPERP/StudentInfo/photos/" + fileName)
            });
            return;
        }
        catch (System.Threading.ThreadAbortException)
        {
            return;
        }
        catch (Exception ex)
        {
            WriteJsonAndComplete(serializer, new { success = false, message = "Error updating photo: " + ex.Message });
            return;
        }
    }

    // ===================================================================
    // Transcript repair
    // ===================================================================
    //
    // WHY A TRANSCRIPT COMES OUT EMPTY FOR A STUDENT WHO HAS MARKS.
    //
    // Every transcript stored procedure — acad_GetSingleStudentTranscript_All, _Col1,
    // _Col2 and the batch equivalents — ends with the same join:
    //
    //     FROM acad_transcript_results r JOIN acad_graduands g ON g.regno = r.regno
    //     WHERE g.regno = reg AND trans_status = 'Ready'
    //
    // and acad_UpdateDocStatus walks that status ONE WAY as a transcript is issued:
    // Ready -> Printed -> Picked. So the act of printing a transcript is also what
    // stops the next one from ever containing anything. The student keeps every mark;
    // the document simply renders blank, with no error anywhere to explain it.
    //
    // Measured on the live register: 556 students are sitting past 'Ready' (548
    // Printed, 8 Picked) and every one of them has both marks and transcript rows —
    // they are complete records being filtered out by a workflow flag.
    //
    // acad_CreateTranscript, which the document service calls before rendering, only
    // ever rebuilds acad_transcript_results. It never looks at acad_graduands, so it
    // cannot clear this by itself.
    //
    // A SECOND, DIFFERENT FAULT: 19,217 students with marks have no acad_graduands row
    // at all (16,886 of them ALUMNI). That one is NOT repaired automatically here.
    // Inserting into acad_graduands declares somebody a graduand, with a class of award
    // and a graduation date, and it feeds the graduation lists — that is a Registrar's
    // decision, not a side effect of a button labelled "fix". It is diagnosed, named,
    // and left alone.

    /// <summary>
    /// action=DiagnoseTranscript — read-only. Says exactly why this student's transcript
    /// would come out empty, so the operator sees the reason before anything is changed.
    /// </summary>
    private void HandleDiagnoseTranscript()
    {
        var js = new JavaScriptSerializer();
        try
        {
            string regno = (Request["regno"] ?? "").Trim();
            if (regno == "") { WriteJsonAndComplete(js, new { success = false, message = "Registration number is required." }); return; }

            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                TranscriptDiagnosis d = DiagnoseTranscript(conn, regno);
                WriteJsonAndComplete(js, new
                {
                    success = true,
                    regno = regno,
                    name = d.Name,
                    results = d.Results,
                    transcriptRows = d.TranscriptRows,
                    hasGraduandRow = d.HasGraduandRow,
                    transStatus = d.TransStatus,
                    printedBy = d.PrintedBy,
                    printedOn = d.PrintedOn,
                    willPrint = d.WillPrint,
                    canRepair = d.CanRepair,
                    fault = d.Fault,
                    detail = d.Detail
                });
            }
        }
        catch (Exception ex) { WriteJsonAndComplete(js, new { success = false, message = ex.Message }); }
    }

    private class TranscriptDiagnosis
    {
        public string Name = "";
        public int Results, TranscriptRows;
        public bool HasGraduandRow, WillPrint, CanRepair;
        public string TransStatus = "", PrintedBy = "", PrintedOn = "";
        public string Fault = "", Detail = "";
    }

    private TranscriptDiagnosis DiagnoseTranscript(MySqlConnection conn, string regno)
    {
        var d = new TranscriptDiagnosis();

        using (var cmd = new MySqlCommand(
            "SELECT TRIM(CONCAT(COALESCE(firstname,''),' ',COALESCE(othername,''))) FROM acad_student WHERE regno=@r LIMIT 1", conn))
        { cmd.Parameters.AddWithValue("@r", regno); object o = cmd.ExecuteScalar(); d.Name = o == null || o == DBNull.Value ? "" : o.ToString().Trim(); }

        using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM acad_results WHERE regno=@r", conn))
        { cmd.Parameters.AddWithValue("@r", regno); d.Results = Convert.ToInt32(cmd.ExecuteScalar()); }

        using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM acad_transcript_results WHERE regno=@r", conn))
        { cmd.Parameters.AddWithValue("@r", regno); d.TranscriptRows = Convert.ToInt32(cmd.ExecuteScalar()); }

        using (var cmd = new MySqlCommand(
            "SELECT COALESCE(trans_status,''), COALESCE(trans_printer,''), COALESCE(DATE_FORMAT(trans_date,'%e %b %Y'),'') " +
            "FROM acad_graduands WHERE regno=@r LIMIT 1", conn))
        {
            cmd.Parameters.AddWithValue("@r", regno);
            using (var r = cmd.ExecuteReader())
                if (r.Read())
                {
                    d.HasGraduandRow = true;
                    d.TransStatus = r.GetString(0).Trim();
                    d.PrintedBy = r.GetString(1).Trim();
                    d.PrintedOn = r.GetString(2).Trim();
                }
        }

        d.WillPrint = d.HasGraduandRow
                   && string.Equals(d.TransStatus, "Ready", StringComparison.OrdinalIgnoreCase)
                   && d.TranscriptRows > 0;

        if (d.Results == 0)
        {
            d.Fault = "No marks";
            d.Detail = "This student has no results on file at all, so there is nothing to put on a transcript. "
                     + "Nothing here can fix that — the marks have to be entered or published first.";
            d.CanRepair = false;
        }
        else if (!d.HasGraduandRow)
        {
            d.Fault = "Not on the graduands list";
            d.Detail = "This student has " + d.Results + " marks, but no record on the graduands list — and the transcript "
                     + "is built by joining to that list. Adding someone to it sets a class of award and a graduation date "
                     + "and puts them on the graduation lists, so it is a Registrar's decision rather than something this "
                     + "button should do. Use the printable (HTML) transcript for a student who has not graduated.";
            d.CanRepair = false;
        }
        else if (!string.Equals(d.TransStatus, "Ready", StringComparison.OrdinalIgnoreCase))
        {
            d.Fault = "Transcript already issued";
            d.Detail = "The transcript was already printed" + (d.PrintedBy == "" ? "" : " by " + d.PrintedBy)
                     + (d.PrintedOn == "" ? "" : " on " + d.PrintedOn) + ", which moved this student to \"" + d.TransStatus
                     + "\". Every transcript is built with a filter for \"Ready\", so from that moment on the document "
                     + "renders empty even though all " + d.Results + " marks are still there. Fixing this puts the status "
                     + "back to Ready and rebuilds the transcript rows.";
            d.CanRepair = true;
        }
        else if (d.TranscriptRows == 0)
        {
            d.Fault = "Transcript rows missing";
            d.Detail = "This student has " + d.Results + " marks and is marked Ready, but the transcript working table is "
                     + "empty, so the document would print with no courses. Fixing this rebuilds it from the marks.";
            d.CanRepair = true;
        }
        else
        {
            d.Fault = "";
            d.Detail = "Nothing is wrong: " + d.Results + " marks, " + d.TranscriptRows + " transcript rows, status Ready. "
                     + "This transcript should print normally. Fixing it anyway simply rebuilds the rows from the marks.";
            d.CanRepair = true;
        }
        return d;
    }

    /// <summary>
    /// action=FixTranscript — repairs the two faults this button is allowed to repair:
    /// a status that has walked past 'Ready', and a stale or empty transcript working
    /// table. Both are re-derived from the marks, so it is safe to run on a student who
    /// is already fine, and safe to run twice.
    ///
    /// It will NOT create a graduands row. See the note above HandleDiagnoseTranscript.
    /// </summary>
    private void HandleFixTranscript()
    {
        var js = new JavaScriptSerializer();
        try
        {
            string regno = (Request["regno"] ?? "").Trim();
            if (regno == "") { WriteJsonAndComplete(js, new { success = false, message = "Registration number is required." }); return; }

            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                TranscriptDiagnosis before = DiagnoseTranscript(conn, regno);

                if (before.Results == 0)
                { WriteJsonAndComplete(js, new { success = false, message = before.Detail }); return; }
                if (!before.HasGraduandRow)
                { WriteJsonAndComplete(js, new { success = false, message = before.Detail }); return; }

                var did = new System.Collections.Generic.List<string>();

                // 1. Put the status back to Ready. Kept as a plain UPDATE rather than a
                //    call to acad_ReverseDocStatus because that procedure steps back only
                //    ONE stage — a student on 'Picked' would land on 'Printed' and still
                //    render an empty transcript. The printer and date are left untouched:
                //    they are the record of who issued the earlier copy.
                if (!string.Equals(before.TransStatus, "Ready", StringComparison.OrdinalIgnoreCase))
                {
                    using (var cmd = new MySqlCommand("UPDATE acad_graduands SET trans_status='Ready' WHERE regno=@r", conn))
                    { cmd.Parameters.AddWithValue("@r", regno); cmd.ExecuteNonQuery(); }
                    did.Add("status put back to Ready (was " + before.TransStatus + ")");
                }

                // 2. Rebuild the transcript working table from the marks. This is exactly
                //    what the document service calls before every render, so it can only
                //    bring the rows in line with the results.
                using (var cmd = new MySqlCommand("acad_CreateTranscript", conn))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.CommandTimeout = 120;
                    cmd.Parameters.AddWithValue("reg", regno);
                    cmd.Parameters.AddWithValue("typ", "Normal");
                    cmd.Parameters.AddWithValue("fid", 0);
                    cmd.ExecuteNonQuery();
                }

                TranscriptDiagnosis after = DiagnoseTranscript(conn, regno);
                did.Add("transcript rebuilt from marks (" + after.TranscriptRows + " course rows)");

                LogStudentAction(conn, regno, "FixTranscript",
                    "Transcript repair: " + string.Join("; ", did.ToArray())
                    + ". Before: status=" + (before.TransStatus == "" ? "(none)" : before.TransStatus)
                    + ", rows=" + before.TranscriptRows + ", marks=" + before.Results + ".");

                string msg = after.WillPrint
                    ? "Fixed. " + string.Join("; ", did.ToArray()) + ". This transcript will now print."
                    : "Ran the repair (" + string.Join("; ", did.ToArray()) + "), but the transcript still will not print: " + after.Detail;

                WriteJsonAndComplete(js, new
                {
                    success = after.WillPrint,
                    message = msg,
                    willPrint = after.WillPrint,
                    transcriptRows = after.TranscriptRows,
                    transStatus = after.TransStatus
                });
            }
        }
        catch (Exception ex) { WriteJsonAndComplete(js, new { success = false, message = "Repair failed: " + ex.Message }); }
    }

    /// <summary>
    /// The template used when a caller asks for an academic document without naming one.
    /// Must match the option marked selected on ddlAcademicDocumentType — if the two
    /// disagree, the screen and a hand-built URL quietly produce different documents.
    /// </summary>
    private const string DefaultAcademicDocumentType = "TranscriptCompact";

    private void HandleGenerateAcademicDocument()
    {
        try
        {
            string documentType = (Request["documentType"] ?? Request["type"] ?? Request["doc"] ?? string.Empty).Trim();
            string regnosRaw = (Request["regnos"] ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(regnosRaw))
            {
                string singleRegno = (Request["regno"] ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(singleRegno))
                    regnosRaw = singleRegno;
            }

            if (string.IsNullOrEmpty(documentType) && string.IsNullOrEmpty(regnosRaw))
            {
                WriteAcademicDocumentHelpAndComplete();
                return;
            }

            // Students were named but no template was: fall through to the default rather
            // than refusing. Template 3 is the default everywhere — the dialog opens on
            // it, and a hand-built URL that omits documentType now gets the same thing
            // the screen would have sent.
            if (string.IsNullOrEmpty(documentType))
                documentType = DefaultAcademicDocumentType;

            List<string> regnos = ParseRegnos(regnosRaw);
            if (regnos.Count == 0)
            {
                WriteHtmlErrorAndComplete("Select at least one student before generating academic documents. You can pass regno=... for a single student or regnos=REG1,REG2 for batch generation.");
                return;
            }

            // ── The graduation gate ──────────────────────────────────────────────────
            // Both routes out of this method produce a graduation document, so the check sits
            // above the fork rather than being written twice and drifting. A batch where some
            // students are approved and some are not prints the approved ones and reports the
            // rest by name: refusing the whole selection because of one unapproved student
            // would make the Registry re-select by hand to find out which.
            string printActor = ResolveDocumentActor();
            GraduationPrintGate.Outcome gate = GraduationPrintGate.Check(regnos, documentType);

            if (gate.AnyBlocked)
            {
                // The refusal is recorded whether or not the operator goes on with the rest,
                // because the fact worth keeping is that a document was sought for somebody who
                // has not been approved.
                GraduationPrintGate.Record(gate, documentType, null, printActor, RoleAccessService.GetRoleName());
                WriteGraduationBlockAndComplete(gate, documentType);
                return;
            }

            // Print-optimized HTML transcript (adaptive layout engine) instead of the fixed PDF.
            if (documentType.Equals("TranscriptHTML", StringComparison.OrdinalIgnoreCase)
                || documentType.Equals("HTML", StringComparison.OrdinalIgnoreCase)
                || documentType.Equals("PrintTranscript", StringComparison.OrdinalIgnoreCase))
            {
                GraduationPrintGate.Record(gate, documentType, "TranscriptPrint.aspx", printActor,
                                           RoleAccessService.GetRoleName());
                string url = ResolveUrl("~/COOPERP/NewScreens/TranscriptPrint.aspx?reg="
                    + Server.UrlEncode(regnos[0]) + "&autoprint=1");
                Response.Redirect(url, false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            AcademicDocumentPdfService service = new AcademicDocumentPdfService();
            AcademicDocumentPdfService.ExportResult result = service.GeneratePdf(documentType, regnos);

            if (result == null || !result.Success || result.Content == null || result.Content.Length == 0)
            {
                WriteHtmlErrorAndComplete(result != null ? result.Message : "The academic document could not be generated.");
                return;
            }

            // Recorded only once the document exists. Writing it earlier would log prints that
            // never happened, and the graduation list would show a transcript as issued when
            // the generator had in fact failed.
            GraduationPrintGate.Record(gate, documentType, result.FileName, printActor,
                                       RoleAccessService.GetRoleName());

            WriteBinaryFileAndComplete(result.Content, result.ContentType, result.FileName);
        }
        catch (System.Threading.ThreadAbortException)
        {
            return;
        }
        catch (Exception ex)
        {
            WriteHtmlErrorAndComplete("Error generating academic document: " + ex.Message);
        }
    }

    private List<string> ParseRegnos(string raw)
    {
        List<string> values = new List<string>();
        if (string.IsNullOrWhiteSpace(raw))
            return values;

        string[] parts = raw.Split(new char[] { ',', ';', '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            string value = (part ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(value))
                continue;

            bool exists = false;
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
                values.Add(value);
        }

        return values;
    }
    
    /// <summary>
    /// True when the request carries a signed-in staff session.
    ///
    /// Accepts either signal the eadmin screens use — a forms-authentication identity or the
    /// Session["username"] the older pages set — because different entry points establish one or
    /// the other, and requiring both would lock out legitimate administrators.
    /// </summary>
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
    /// The factory password a student account is reset to. Must match DefaultPortalPassword in
    /// the portal's lg_modern.ascx.cs — that login path watches for this exact value and forces
    /// the student through ForcePasswordChange.aspx before they can use the portal. Reset to
    /// anything else and the student would keep a password an administrator knows.
    /// </summary>
    private const string FactoryPortalPassword = "123";

    /// <summary>
    /// action=ResetPasswordToDefault — one-click reset of a student's portal password back to the
    /// factory value.
    ///
    /// The password is fixed SERVER-SIDE rather than taken from the request. The row button cannot
    /// then be repurposed to set an arbitrary password on any student by editing the POST, and the
    /// audit line can state what the password was set to without having to trust the caller.
    ///
    /// Everything else reuses the Set Password machinery: the same account resolution (which
    /// creates a portal login if the student never had one), the same salt-and-hash, the same
    /// unlock of a locked-out account, and the same self-heal for a missing membership row.
    /// </summary>
    private void HandleResetPasswordToDefault()
    {
        JavaScriptSerializer serializer = new JavaScriptSerializer();
        try
        {
            string regno = (Request.Form["regno"] ?? "").Trim();
            if (string.IsNullOrEmpty(regno))
            {
                WriteJsonAndComplete(serializer, new { success = false, message = "No student was supplied." });
                return;
            }

            string portalConnStr = ConfigurationManager.ConnectionStrings["campus_dynamics_portalConnectionString"] != null
                ? ConfigurationManager.ConnectionStrings["campus_dynamics_portalConnectionString"].ConnectionString
                : ConnectionString;

            using (MySqlConnection conn = new MySqlConnection(portalConnStr))
            {
                conn.Open();

                string userId = ResolvePortalUserIdForPasswordReset(conn, regno);
                if (string.IsNullOrEmpty(userId))
                {
                    WriteJsonAndComplete(serializer, new {
                        success = false,
                        message = "No portal account for " + regno + ", and one could not be created. Check the registration number."
                    });
                    return;
                }

                byte[] saltBytes = new byte[16];
                using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider()) rng.GetBytes(saltBytes);
                string salt = Convert.ToBase64String(saltBytes);
                string hashed = HashPasswordWithSalt(FactoryPortalPassword, salt);

                using (MySqlCommand cmd = new MySqlCommand(
                    @"UPDATE campus_dynamics_portal.my_aspnet_membership
                         SET password = @password,
                             passwordKey = @salt,
                             IsLockedOut = 0,
                             FailedPasswordAttemptCount = 0,
                             LastPasswordChangedDate = @now
                       WHERE userId = @userId", conn))
                {
                    cmd.Parameters.AddWithValue("@password", hashed);
                    cmd.Parameters.AddWithValue("@salt", salt);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@now", DateTime.UtcNow);

                    int rows = cmd.ExecuteNonQuery();
                    if (rows == 0 && EnsurePortalMembershipRecord(conn, userId))
                        rows = cmd.ExecuteNonQuery();

                    if (rows == 0)
                    {
                        WriteJsonAndComplete(serializer, new {
                            success = false,
                            message = "Could not update the login record for " + regno + ". Try Set Password instead."
                        });
                        return;
                    }
                }

                LogStudentAction(conn, regno, "ResetPasswordToDefault",
                    "Portal password reset to the factory default; account unlocked.");

                WriteJsonAndComplete(serializer, new {
                    success = true,
                    password = FactoryPortalPassword,
                    message = "Password reset to " + FactoryPortalPassword + ". " +
                              "The student will be asked to choose a new one the moment they sign in."
                });
            }
        }
        catch (Exception ex)
        {
            WriteJsonAndComplete(serializer, new { success = false, message = "Reset failed: " + ex.Message });
        }
    }

    private void HandleSetPassword()
    {
        JavaScriptSerializer serializer = new JavaScriptSerializer();
        
        try
        {
            // Read POST data
            string regno = Request.Form["regno"];
            string newPassword = Request.Form["newPassword"];
            
            if (string.IsNullOrEmpty(regno) || string.IsNullOrEmpty(newPassword))
            {
                WriteJsonAndComplete(serializer, new { success = false, message = "Registration number and password are required." });
                return;
            }
            
            regno = regno.Trim();
            newPassword = newPassword.Trim();
            
            // Use the portal connection string for membership tables
            string portalConnStr = ConfigurationManager.ConnectionStrings["campus_dynamics_portalConnectionString"] != null
                ? ConfigurationManager.ConnectionStrings["campus_dynamics_portalConnectionString"].ConnectionString
                : "";
            
            if (string.IsNullOrEmpty(portalConnStr))
            {
                // Fallback: use main connection but prefix tables with campus_dynamics_portal
                portalConnStr = ConnectionString;
            }
            
            using (MySqlConnection conn = new MySqlConnection(portalConnStr))
            {
                conn.Open();
                
                // Step 1: Resolve portal user id from regno.
                // Supports both legacy usernames (regno) and migrated usernames (verified university email).
                // Will automatically create a portal account if the student exists but doesn't have one yet.
                string userId = ResolvePortalUserIdForPasswordReset(conn, regno);
                
                if (string.IsNullOrEmpty(userId))
                {
                    string debugSuffix = string.IsNullOrEmpty(_setPasswordLastDebug) ? "" : (" Diagnostic: " + _setPasswordLastDebug);
                    WriteJsonAndComplete(serializer, new {
                        success = false, 
                        message = "[SETPASSWORD_V2] Student " + regno + " not found in the system or portal account could not be created. Please verify the registration number." + debugSuffix
                    });
                    return;
                }
                
                // Step 2: Generate a new salt and hash the password
                byte[] saltBytes = new byte[16];
                using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                {
                    rng.GetBytes(saltBytes);
                }
                string salt = Convert.ToBase64String(saltBytes);
                string hashedPassword = HashPasswordWithSalt(newPassword, salt);
                
                // Step 3: Update the membership record with new password, salt, unlock account
                using (MySqlCommand cmd = new MySqlCommand(
                    @"UPDATE campus_dynamics_portal.my_aspnet_membership 
                      SET password = @password, 
                          passwordKey = @salt, 
                          IsLockedOut = 0, 
                          FailedPasswordAttemptCount = 0,
                          LastPasswordChangedDate = @now
                      WHERE userId = @userId", conn))
                {
                    cmd.Parameters.AddWithValue("@password", hashedPassword);
                    cmd.Parameters.AddWithValue("@salt", salt);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
                    
                    int rows = cmd.ExecuteNonQuery();
                    if (rows > 0)
                    {
                        WriteJsonAndComplete(serializer, new {
                            success = true, 
                            message = "[SETPASSWORD_V2] Password set successfully for " + regno + ". The student can now log in with the new password." 
                        });
                    }
                    else
                    {
                        bool healed = EnsurePortalMembershipRecord(conn, userId);
                        if (healed)
                        {
                            int retryRows = cmd.ExecuteNonQuery();
                            if (retryRows > 0)
                            {
                                WriteJsonAndComplete(serializer, new {
                                    success = true,
                                    message = "[SETPASSWORD_V2] Password set successfully for " + regno + ". The account membership record was repaired automatically." 
                                });
                            }
                            else
                            {
                                WriteJsonAndComplete(serializer, new {
                                    success = false,
                                    message = "[SETPASSWORD_V2] Membership repair attempted but password update still failed for this user." 
                                });
                            }
                        }
                        else
                        {
                            string debugSuffix = string.IsNullOrEmpty(_setPasswordLastDebug) ? "" : (" Diagnostic: " + _setPasswordLastDebug);
                            WriteJsonAndComplete(serializer, new {
                                success = false,
                                message = "[SETPASSWORD_V2] No membership record found for this user. The account may be incomplete." + debugSuffix
                            });
                        }
                    }
                }
            }
        }
        catch (System.Threading.ThreadAbortException)
        {
            return;
        }
        catch (Exception ex)
        {
            WriteJsonAndComplete(serializer, new {
                success = false, 
                message = "[SETPASSWORD_V2] Error: " + ex.Message 
            });
        }

        return;
    }

    private void HandleListStudents()
    {
        JavaScriptSerializer serializer = new JavaScriptSerializer();
        
        try
        {
            List<object> students = new List<object>();
            
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                
                // Get first 20 students with email
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT regno, email FROM acad_student WHERE email IS NOT NULL AND email != '' LIMIT 20", conn))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            students.Add(new {
                                regno = reader["regno"].ToString(),
                                email = reader["email"].ToString()
                            });
                        }
                    }
                }
            }
            
            WriteJsonAndComplete(serializer, new {
                success = true,
                count = students.Count,
                students = students
            });
        }
        catch (Exception ex)
        {
            WriteJsonAndComplete(serializer, new {
                success = false,
                message = "Error: " + ex.Message
            });
        }
    }

    private void HandleHealthSetPassword()
    {
        JavaScriptSerializer serializer = new JavaScriptSerializer();

        try
        {
            bool hasRegnoColumn = false;
            bool hasEmailColumn = false;

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                using (MySqlCommand cmd = new MySqlCommand(
                    @"SELECT COLUMN_NAME 
                      FROM information_schema.COLUMNS 
                      WHERE TABLE_SCHEMA = DATABASE() 
                        AND TABLE_NAME = 'acad_student'
                        AND COLUMN_NAME IN ('regno','email')", conn))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string col = reader["COLUMN_NAME"].ToString().ToLower();
                            if (col == "regno") hasRegnoColumn = true;
                            if (col == "email") hasEmailColumn = true;
                        }
                    }
                }
            }

            WriteJsonAndComplete(serializer, new
            {
                success = true,
                version = "SETPASSWORD_V2",
                serverUtc = DateTime.UtcNow.ToString("o"),
                checks = new
                {
                    hasVacConnectionString = !string.IsNullOrEmpty(ConnectionString),
                    acadStudentHasRegno = hasRegnoColumn,
                    acadStudentHasEmail = hasEmailColumn
                }
            });
        }
        catch (System.Threading.ThreadAbortException)
        {
            return;
        }
        catch (Exception ex)
        {
            WriteJsonAndComplete(serializer, new
            {
                success = false,
                version = "SETPASSWORD_V2",
                message = "Health check error: " + ex.Message
            });
        }
    }

    private void HandleCheckStudentForSetPassword()
    {
        JavaScriptSerializer serializer = new JavaScriptSerializer();

        try
        {
            string regno = (Request["regno"] ?? "").Trim();
            if (string.IsNullOrEmpty(regno))
            {
                WriteJsonAndComplete(serializer, new
                {
                    success = false,
                    version = "SETPASSWORD_V2",
                    message = "regno is required"
                });
                return;
            }

            bool studentFound = false;
            string matchedRegno = "";
            string studentEmail = "";
            List<string> similarRegnos = new List<string>();

            using (MySqlConnection acadConn = new MySqlConnection(ConnectionString))
            {
                acadConn.Open();

                using (MySqlCommand findStudentCmd = new MySqlCommand(
                    "SELECT regno, email FROM acad_student WHERE LOWER(TRIM(regno)) = LOWER(@regno) LIMIT 1", acadConn))
                {
                    findStudentCmd.Parameters.AddWithValue("@regno", regno);
                    using (MySqlDataReader reader = findStudentCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            studentFound = true;
                            matchedRegno = reader["regno"] == DBNull.Value ? "" : reader["regno"].ToString();
                            studentEmail = reader["email"] == DBNull.Value ? "" : reader["email"].ToString();
                        }
                    }
                }

                if (!studentFound)
                {
                    string regPrefix = regno.Length >= 7 ? regno.Substring(0, 7) : regno;
                    using (MySqlCommand similarCmd = new MySqlCommand(
                        "SELECT regno FROM acad_student WHERE regno LIKE @prefix LIMIT 10", acadConn))
                    {
                        similarCmd.Parameters.AddWithValue("@prefix", regPrefix + "%");
                        using (MySqlDataReader reader = similarCmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                similarRegnos.Add(reader["regno"].ToString());
                            }
                        }
                    }
                }
            }

            bool portalUserByRegno = false;
            bool portalUserByEmail = false;
            List<string> usersColumns = new List<string>();
            List<string> membershipColumns = new List<string>();
            string portalConnStr = ConfigurationManager.ConnectionStrings["campus_dynamics_portalConnectionString"] != null
                ? ConfigurationManager.ConnectionStrings["campus_dynamics_portalConnectionString"].ConnectionString
                : "";

            if (!string.IsNullOrEmpty(portalConnStr))
            {
                using (MySqlConnection portalConn = new MySqlConnection(portalConnStr))
                {
                    portalConn.Open();

                    using (MySqlCommand byRegCmd = new MySqlCommand(
                        "SELECT COUNT(*) FROM campus_dynamics_portal.my_aspnet_users WHERE LOWER(TRIM(name)) = LOWER(@regno)", portalConn))
                    {
                        byRegCmd.Parameters.AddWithValue("@regno", regno);
                        portalUserByRegno = Convert.ToInt32(byRegCmd.ExecuteScalar()) > 0;
                    }

                    if (!string.IsNullOrEmpty(studentEmail))
                    {
                        using (MySqlCommand byEmailCmd = new MySqlCommand(
                            "SELECT COUNT(*) FROM campus_dynamics_portal.my_aspnet_users WHERE LOWER(TRIM(name)) = LOWER(@email) OR LOWER(IFNULL(verified_email,'')) = LOWER(@email)", portalConn))
                        {
                            byEmailCmd.Parameters.AddWithValue("@email", studentEmail);
                            portalUserByEmail = Convert.ToInt32(byEmailCmd.ExecuteScalar()) > 0;
                        }
                    }

                    using (MySqlCommand usersColsCmd = new MySqlCommand(
                        @"SELECT COLUMN_NAME
                          FROM information_schema.COLUMNS
                          WHERE TABLE_SCHEMA = 'campus_dynamics_portal'
                            AND TABLE_NAME = 'my_aspnet_users'
                          ORDER BY ORDINAL_POSITION", portalConn))
                    {
                        using (MySqlDataReader reader = usersColsCmd.ExecuteReader())
                        {
                            while (reader.Read())
                                usersColumns.Add(reader["COLUMN_NAME"].ToString());
                        }
                    }

                    using (MySqlCommand memberColsCmd = new MySqlCommand(
                        @"SELECT COLUMN_NAME
                          FROM information_schema.COLUMNS
                          WHERE TABLE_SCHEMA = 'campus_dynamics_portal'
                            AND TABLE_NAME = 'my_aspnet_membership'
                          ORDER BY ORDINAL_POSITION", portalConn))
                    {
                        using (MySqlDataReader reader = memberColsCmd.ExecuteReader())
                        {
                            while (reader.Read())
                                membershipColumns.Add(reader["COLUMN_NAME"].ToString());
                        }
                    }
                }
            }

            WriteJsonAndComplete(serializer, new
            {
                success = true,
                version = "SETPASSWORD_V2",
                inputRegno = regno,
                studentFound = studentFound,
                matchedRegno = matchedRegno,
                studentEmail = studentEmail,
                portalUserByRegno = portalUserByRegno,
                portalUserByEmail = portalUserByEmail,
                similarRegnos = similarRegnos,
                usersColumns = usersColumns,
                membershipColumns = membershipColumns
            });
        }
        catch (System.Threading.ThreadAbortException)
        {
            return;
        }
        catch (Exception ex)
        {
            WriteJsonAndComplete(serializer, new
            {
                success = false,
                version = "SETPASSWORD_V2",
                message = "CheckStudentForSetPassword error: " + ex.Message
            });
        }
    }

    private string ResolvePortalUserIdForPasswordReset(MySqlConnection conn, string regno)
    {
        if (conn == null || string.IsNullOrWhiteSpace(regno)) return null;
        _setPasswordLastDebug = "";

        regno = regno.Trim();

        // Step 1: Validate the student exists in acad_student using academic database connection
        bool studentExists = false;
        string studentEmail = null;
        string debugError = "";
        // Create a separate connection to the academic database for this query
        using (MySqlConnection acadConn = new MySqlConnection(ConnectionString))
        {
            try
            {
                acadConn.Open();
                using (MySqlCommand validateCmd = new MySqlCommand(
                    "SELECT regno, email FROM acad_student WHERE LOWER(TRIM(regno)) = LOWER(@regno) LIMIT 1", acadConn))
                {
                    validateCmd.Parameters.AddWithValue("@regno", regno);
                    using (MySqlDataReader reader = validateCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            studentExists = true;
                            if (reader["email"] != DBNull.Value)
                                studentEmail = reader["email"].ToString();
                        }
                        else
                        {
                            debugError = "Query executed but no student found with regno=" + regno;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                debugError = "Academic DB Error: " + ex.Message;
                System.Diagnostics.Debug.WriteLine(debugError);
            }
        }

        // If student doesn't exist in acad_student, we can't proceed
        if (!studentExists)
        {
            // Student not found in main database
            if (!string.IsNullOrEmpty(debugError))
            {
                _setPasswordLastDebug = debugError;
                System.Diagnostics.Debug.WriteLine("ResolvePortalUserIdForPasswordReset failure: " + debugError);
            }
            return null;
        }

        // Step 2: Try direct match on registration number (legacy usernames stored as regno)
        using (MySqlCommand directCmd = new MySqlCommand(
            "SELECT id FROM campus_dynamics_portal.my_aspnet_users WHERE LOWER(TRIM(name)) = LOWER(@regno) LIMIT 1", conn))
        {
            directCmd.Parameters.AddWithValue("@regno", regno);
            object direct = directCmd.ExecuteScalar();
            if (direct != null && direct != DBNull.Value)
                return direct.ToString();
        }

        // Step 3: Try match by email (for migrated accounts)
        if (!string.IsNullOrEmpty(studentEmail))
        {
            using (MySqlCommand emailCmd = new MySqlCommand(
                "SELECT u.id FROM campus_dynamics_portal.my_aspnet_users u " +
                "WHERE LOWER(TRIM(u.name)) = LOWER(@email) OR LOWER(IFNULL(u.verified_email,'')) = LOWER(@email) " +
                "LIMIT 1", conn))
            {
                emailCmd.Parameters.AddWithValue("@email", studentEmail);
                object emailResult = emailCmd.ExecuteScalar();
                if (emailResult != null && emailResult != DBNull.Value)
                    return emailResult.ToString();
            }
        }

        // Step 4: Student exists but no portal user found - CREATE ONE
        // Generate a portal user for this student so they can set/use password
        try
        {
            string portalUserId = CreatePortalUserForStudent(conn, regno, studentEmail);
            if (!string.IsNullOrEmpty(portalUserId))
                return portalUserId;
            if (string.IsNullOrEmpty(_setPasswordLastDebug))
                _setPasswordLastDebug = "Portal user creation returned no user id.";
        }
        catch (Exception ex)
        {
            _setPasswordLastDebug = "Error creating portal user: " + ex.Message;
            System.Diagnostics.Debug.WriteLine("Error creating portal user: " + ex.Message);
        }

        // If we get here, student exists but we couldn't create or find a portal user
        return null;
    }

    /// <summary>
    /// Creates a new portal user account for a student who exists in acad_student but doesn't have a portal account yet.
    /// Returns the new user ID on success, null on failure.
    /// </summary>
    private string CreatePortalUserForStudent(MySqlConnection conn, string regno, string email)
    {
        try
        {
            string newUserId = "";
            string normalizedEmail = string.IsNullOrWhiteSpace(email) ? "" : email.Trim();
            DateTime now = DateTime.UtcNow;
            bool userCreated = false;
            string applicationId = GetPortalApplicationId(conn);
            string usersIdDataType = GetUsersIdDataType(conn).ToLower();
            int usersIdMaxLen = GetUsersIdMaxLength(conn);
            bool usersIdIsNumeric = usersIdDataType.Contains("int") || usersIdDataType.Contains("decimal") || usersIdDataType.Contains("numeric");

            if (!usersIdIsNumeric)
            {
                newUserId = Guid.NewGuid().ToString("N");
                if (usersIdMaxLen > 0 && newUserId.Length > usersIdMaxLen)
                    newUserId = newUserId.Substring(0, usersIdMaxLen);
            }

            if (string.IsNullOrEmpty(applicationId))
            {
                _setPasswordLastDebug = "No applicationId found in campus_dynamics_portal.my_aspnet_applications";
                return null;
            }

            try
            {
                if (usersIdIsNumeric)
                {
                    using (MySqlCommand createCmdNumeric = new MySqlCommand(
                        @"INSERT INTO campus_dynamics_portal.my_aspnet_users 
                          (applicationId, name, isAnonymous, lastActivityDate, verified_email, user_type, IsApproved, user_verification_status, DateCreated)
                          VALUES (@applicationId, @name, 0, @now, @email, @type, 1, 'AUTO_CREATED', @now)", conn))
                    {
                        createCmdNumeric.Parameters.AddWithValue("@applicationId", applicationId);
                        createCmdNumeric.Parameters.AddWithValue("@name", regno);
                        createCmdNumeric.Parameters.AddWithValue("@email", normalizedEmail);
                        createCmdNumeric.Parameters.AddWithValue("@type", "STUDENT");
                        createCmdNumeric.Parameters.AddWithValue("@now", now);
                        userCreated = createCmdNumeric.ExecuteNonQuery() > 0;
                    }

                    if (userCreated)
                    {
                        using (MySqlCommand idCmd = new MySqlCommand("SELECT LAST_INSERT_ID()", conn))
                        {
                            object newIdObj = idCmd.ExecuteScalar();
                            if (newIdObj != null && newIdObj != DBNull.Value)
                                newUserId = newIdObj.ToString();
                        }
                    }
                }
                else
                {
                    using (MySqlCommand createCmd = new MySqlCommand(
                        @"INSERT INTO campus_dynamics_portal.my_aspnet_users 
                          (id, applicationId, name, isAnonymous, lastActivityDate, verified_email, user_type, IsApproved, user_verification_status, DateCreated)
                          VALUES (@id, @applicationId, @name, 0, @now, @email, @type, 1, 'AUTO_CREATED', @now)", conn))
                    {
                        createCmd.Parameters.AddWithValue("@id", newUserId);
                        createCmd.Parameters.AddWithValue("@applicationId", applicationId);
                        createCmd.Parameters.AddWithValue("@name", regno);
                        createCmd.Parameters.AddWithValue("@email", normalizedEmail);
                        createCmd.Parameters.AddWithValue("@type", "STUDENT");
                        createCmd.Parameters.AddWithValue("@now", now);

                        userCreated = createCmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception exCreatePrimary)
            {
                if (usersIdIsNumeric)
                {
                    try
                    {
                        using (MySqlCommand fallbackUserCmdNumeric = new MySqlCommand(
                            @"INSERT INTO campus_dynamics_portal.my_aspnet_users 
                              (applicationId, name, isAnonymous, lastActivityDate)
                              VALUES (@applicationId, @name, 0, @now)", conn))
                        {
                            fallbackUserCmdNumeric.Parameters.AddWithValue("@applicationId", applicationId);
                            fallbackUserCmdNumeric.Parameters.AddWithValue("@name", regno);
                            fallbackUserCmdNumeric.Parameters.AddWithValue("@now", now);
                            userCreated = fallbackUserCmdNumeric.ExecuteNonQuery() > 0;
                        }

                        if (userCreated)
                        {
                            using (MySqlCommand idCmd = new MySqlCommand("SELECT LAST_INSERT_ID()", conn))
                            {
                                object newIdObj = idCmd.ExecuteScalar();
                                if (newIdObj != null && newIdObj != DBNull.Value)
                                    newUserId = newIdObj.ToString();
                            }
                        }
                    }
                    catch (Exception exCreateFallback)
                    {
                        _setPasswordLastDebug = "Users insert failed. Primary: " + exCreatePrimary.Message + " | Fallback: " + exCreateFallback.Message;
                    }
                }
                else
                {
                    try
                    {
                        using (MySqlCommand fallbackUserCmd = new MySqlCommand(
                            @"INSERT INTO campus_dynamics_portal.my_aspnet_users 
                              (id, applicationId, name, isAnonymous, lastActivityDate)
                              VALUES (@id, @applicationId, @name, 0, @now)", conn))
                        {
                            fallbackUserCmd.Parameters.AddWithValue("@id", newUserId);
                            fallbackUserCmd.Parameters.AddWithValue("@applicationId", applicationId);
                            fallbackUserCmd.Parameters.AddWithValue("@name", regno);
                            fallbackUserCmd.Parameters.AddWithValue("@now", now);
                            userCreated = fallbackUserCmd.ExecuteNonQuery() > 0;
                        }
                    }
                    catch (Exception exCreateFallback)
                    {
                        _setPasswordLastDebug = "Users insert failed. Primary: " + exCreatePrimary.Message + " | Fallback: " + exCreateFallback.Message;
                    }
                }
            }

            if (!userCreated)
                return null;

            // Ensure membership row exists and has required password/salt fields
            byte[] bootstrapSaltBytes = new byte[16];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(bootstrapSaltBytes);
            }
            string bootstrapSalt = Convert.ToBase64String(bootstrapSaltBytes);
            string bootstrapHash = HashPasswordWithSalt(Guid.NewGuid().ToString("N"), bootstrapSalt);

            bool membershipCreated = false;
            try
            {
                using (MySqlCommand memberCmd = new MySqlCommand(
                    @"INSERT INTO campus_dynamics_portal.my_aspnet_membership
                      (userId, Email, Comment, Password, PasswordKey, PasswordFormat, PasswordQuestion, PasswordAnswer, IsApproved, LastActivityDate, LastLoginDate, LastPasswordChangedDate, CreationDate, IsLockedOut, LastLockedOutDate, FailedPasswordAttemptCount, FailedPasswordAttemptWindowStart, FailedPasswordAnswerAttemptCount, FailedPasswordAnswerAttemptWindowStart)
                      VALUES (@userId, @email, '', @password, @salt, 1, '', '', 1, @now, @now, @now, @now, 0, @now, 0, @now, 0, @now)", conn))
                {
                    memberCmd.Parameters.AddWithValue("@userId", newUserId);
                    memberCmd.Parameters.AddWithValue("@email", normalizedEmail);
                    memberCmd.Parameters.AddWithValue("@password", bootstrapHash);
                    memberCmd.Parameters.AddWithValue("@salt", bootstrapSalt);
                    memberCmd.Parameters.AddWithValue("@now", now);
                    membershipCreated = memberCmd.ExecuteNonQuery() > 0;
                }
            }
            catch
            {
                // Some deployments use userid instead of userId column naming
                try
                {
                    using (MySqlCommand memberCmdAlt = new MySqlCommand(
                        @"INSERT INTO campus_dynamics_portal.my_aspnet_membership
                          (userId, Email, Comment, Password, PasswordKey, PasswordFormat, PasswordQuestion, PasswordAnswer, IsApproved, LastActivityDate, LastLoginDate, LastPasswordChangedDate, CreationDate, IsLockedOut, LastLockedOutDate, FailedPasswordAttemptCount, FailedPasswordAttemptWindowStart, FailedPasswordAnswerAttemptCount, FailedPasswordAnswerAttemptWindowStart)
                          VALUES (@userId, @email, '', @password, @salt, 1, '', '', 1, @now, @now, @now, @now, 0, @now, 0, @now, 0, @now)", conn))
                    {
                        memberCmdAlt.Parameters.AddWithValue("@userId", newUserId);
                        memberCmdAlt.Parameters.AddWithValue("@email", normalizedEmail);
                        memberCmdAlt.Parameters.AddWithValue("@password", bootstrapHash);
                        memberCmdAlt.Parameters.AddWithValue("@salt", bootstrapSalt);
                        memberCmdAlt.Parameters.AddWithValue("@now", now);
                        membershipCreated = memberCmdAlt.ExecuteNonQuery() > 0;
                    }
                }
                catch
                {
                    using (MySqlCommand checkMemberCmd = new MySqlCommand(
                        "SELECT COUNT(*) FROM campus_dynamics_portal.my_aspnet_membership WHERE userid = @userId OR userId = @userId", conn))
                    {
                        checkMemberCmd.Parameters.AddWithValue("@userId", newUserId);
                        membershipCreated = Convert.ToInt32(checkMemberCmd.ExecuteScalar()) > 0;
                    }
                }
            }

            if (membershipCreated)
            {
                return newUserId;
            }
        }
        catch (Exception ex)
        {
            _setPasswordLastDebug = "CreatePortalUserForStudent error: " + ex.Message;
            System.Diagnostics.Debug.WriteLine("CreatePortalUserForStudent error: " + ex.Message);
        }

        return null;
    }

    private string GetPortalApplicationId(MySqlConnection conn)
    {
        using (MySqlCommand appCmd = new MySqlCommand(
            "SELECT id FROM campus_dynamics_portal.my_aspnet_applications ORDER BY id LIMIT 1", conn))
        {
            object appId = appCmd.ExecuteScalar();
            if (appId != null && appId != DBNull.Value)
                return appId.ToString();
        }

        return null;
    }

    private string GetUsersIdDataType(MySqlConnection conn)
    {
        using (MySqlCommand cmd = new MySqlCommand(
            @"SELECT DATA_TYPE
              FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = 'campus_dynamics_portal'
                AND TABLE_NAME = 'my_aspnet_users'
                AND COLUMN_NAME = 'id'
              LIMIT 1", conn))
        {
            object result = cmd.ExecuteScalar();
            return result == null || result == DBNull.Value ? "" : result.ToString();
        }
    }

    private int GetUsersIdMaxLength(MySqlConnection conn)
    {
        using (MySqlCommand cmd = new MySqlCommand(
            @"SELECT IFNULL(CHARACTER_MAXIMUM_LENGTH, 0)
              FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = 'campus_dynamics_portal'
                AND TABLE_NAME = 'my_aspnet_users'
                AND COLUMN_NAME = 'id'
              LIMIT 1", conn))
        {
            object result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value) return 0;
            int maxLen;
            return int.TryParse(result.ToString(), out maxLen) ? maxLen : 0;
        }
    }

    private bool EnsurePortalMembershipRecord(MySqlConnection conn, string userId)
    {
        if (conn == null || string.IsNullOrWhiteSpace(userId)) return false;
        _setPasswordLastDebug = "";

        string email = "";
        using (MySqlCommand emailCmd = new MySqlCommand(
            "SELECT IFNULL(verified_email, '') FROM campus_dynamics_portal.my_aspnet_users WHERE id = @userId LIMIT 1", conn))
        {
            emailCmd.Parameters.AddWithValue("@userId", userId);
            object emailObj = emailCmd.ExecuteScalar();
            if (emailObj != null && emailObj != DBNull.Value)
                email = emailObj.ToString();
        }

        byte[] bootstrapSaltBytes = new byte[16];
        using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
        {
            rng.GetBytes(bootstrapSaltBytes);
        }
        string bootstrapSalt = Convert.ToBase64String(bootstrapSaltBytes);
        string bootstrapHash = HashPasswordWithSalt(Guid.NewGuid().ToString("N"), bootstrapSalt);
        DateTime now = DateTime.UtcNow;

        try
        {
            using (MySqlCommand memberCmd = new MySqlCommand(
                @"INSERT INTO campus_dynamics_portal.my_aspnet_membership
                  (userId, Email, Comment, Password, PasswordKey, PasswordFormat, PasswordQuestion, PasswordAnswer, IsApproved, LastActivityDate, LastLoginDate, LastPasswordChangedDate, CreationDate, IsLockedOut, LastLockedOutDate, FailedPasswordAttemptCount, FailedPasswordAttemptWindowStart, FailedPasswordAnswerAttemptCount, FailedPasswordAnswerAttemptWindowStart)
                  VALUES (@userId, @email, '', @password, @salt, 1, '', '', 1, @now, @now, @now, @now, 0, @now, 0, @now, 0, @now)", conn))
            {
                memberCmd.Parameters.AddWithValue("@userId", userId);
                memberCmd.Parameters.AddWithValue("@email", email);
                memberCmd.Parameters.AddWithValue("@password", bootstrapHash);
                memberCmd.Parameters.AddWithValue("@salt", bootstrapSalt);
                memberCmd.Parameters.AddWithValue("@now", now);
                return memberCmd.ExecuteNonQuery() > 0;
            }
        }
        catch (Exception exPrimary)
        {
            _setPasswordLastDebug = "Membership insert failed (primary): " + exPrimary.Message;
            try
            {
                using (MySqlCommand memberCmdAlt = new MySqlCommand(
                    @"INSERT INTO campus_dynamics_portal.my_aspnet_membership
                      (userId, Email, Comment, Password, PasswordKey, PasswordFormat, PasswordQuestion, PasswordAnswer, IsApproved, LastActivityDate, LastLoginDate, LastPasswordChangedDate, CreationDate, IsLockedOut, LastLockedOutDate, FailedPasswordAttemptCount, FailedPasswordAttemptWindowStart, FailedPasswordAnswerAttemptCount, FailedPasswordAnswerAttemptWindowStart)
                      VALUES (@userId, @email, '', @password, @salt, 1, '', '', 1, @now, @now, @now, @now, 0, @now, 0, @now, 0, @now)", conn))
                {
                    memberCmdAlt.Parameters.AddWithValue("@userId", userId);
                    memberCmdAlt.Parameters.AddWithValue("@email", email);
                    memberCmdAlt.Parameters.AddWithValue("@password", bootstrapHash);
                    memberCmdAlt.Parameters.AddWithValue("@salt", bootstrapSalt);
                    memberCmdAlt.Parameters.AddWithValue("@now", now);
                    return memberCmdAlt.ExecuteNonQuery() > 0;
                }
            }
            catch (Exception exFallback)
            {
                _setPasswordLastDebug = "Membership insert failed (fallback): " + exFallback.Message;
                return false;
            }
        }
    }

    // ===============================================================
    //  ADMIN ROW ACTIONS  (change programme / entry year) + LIST EXPORT
    //  All AJAX (JSON), following the SetPassword pattern. Conn = campus_dynamics.
    // ===============================================================

    /// <summary>Bundle for the Change-Programme modal: the student's current programme/spec + the
    /// full active programme list, in one round-trip.</summary>
    private void HandleChangeProgInit()
    {
        var js = new JavaScriptSerializer();
        try
        {
            string regno = (Request.Form["regno"] ?? Request.QueryString["regno"] ?? "").Trim();
            if (regno == "") { WriteJsonAndComplete(js, new { success = false, message = "Missing registration number." }); return; }

            object current = null;
            var programmes = new List<object>();
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    @"SELECT s.regno, TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) nm,
                             s.progid, p.progname, s.specialisation,
                             COALESCE(sp.spec, NULLIF(TRIM(s.specialisation),''),'') specname, s.entryyear
                        FROM acad_student s
                        LEFT JOIN acad_programme p ON p.progcode = s.progid
                        LEFT JOIN acad_specialisation sp ON sp.spec_id = s.specialisation
                       WHERE s.regno=@r LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read()) { WriteJsonAndComplete(js, new { success = false, message = "Student not found: " + regno }); return; }
                        current = new
                        {
                            regno = SafeStr(rdr, "regno"), name = SafeStr(rdr, "nm"),
                            prog = SafeStr(rdr, "progid"), progname = SafeStr(rdr, "progname"),
                            spec = SafeStr(rdr, "specialisation"), specname = SafeStr(rdr, "specname"),
                            entryyear = SafeStr(rdr, "entryyear")
                        };
                    }
                }
                using (var cmd = new MySqlCommand(
                    "SELECT progcode, progname FROM acad_programme WHERE TRIM(IFNULL(progcode,''))<>'' ORDER BY progname", conn))
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        programmes.Add(new { code = SafeStr(rdr, "progcode"), name = SafeStr(rdr, "progname") });
            }
            WriteJsonAndComplete(js, new { success = true, current = current, programmes = programmes });
        }
        catch (Exception ex) { WriteJsonAndComplete(js, new { success = false, message = ex.Message }); }
    }

    /// <summary>Specialisations for a programme (cascade). Empty list = the programme has none.</summary>
    /// <summary>
    /// Specialisations offerable for a programme, for the Change Programme / Specialisation dialog.
    ///
    /// This list used to be restricted to is_active='Active', which hid 214 of the 273
    /// specialisations on record — 78% of them. The consequence was worse than a short list:
    /// 703 students hold a specialisation that would not appear, so the dropdown fell back to
    /// "-- None --" and saving any unrelated edit silently erased it. "Inactive" here means
    /// "not offered to new intakes", not "never existed"; students already on one still have it.
    ///
    /// So everything for the programme is returned, flagged rather than filtered, and the
    /// student's own current specialisation is ALWAYS included even when it belongs to another
    /// programme (49 students are in that position). The caller can then never lose a value
    /// simply because this list declined to mention it.
    /// </summary>
    private void HandleSpecList()
    {
        var js = new JavaScriptSerializer();
        try
        {
            string prog  = (Request.Form["prog"]  ?? Request.QueryString["prog"]  ?? "").Trim();
            string regno = (Request.Form["regno"] ?? Request.QueryString["regno"] ?? "").Trim();

            var items = new List<object>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string currentSpec = "", currentSpecName = "";
            bool currentIsForeign = false;

            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                // What the student holds now, so it can never be dropped from the list.
                if (regno != "")
                    using (var cmd = new MySqlCommand(
                        @"SELECT s.specialisation, TRIM(IFNULL(sp.spec,'')) spec, TRIM(IFNULL(sp.prog_id,'')) sprog
                          FROM acad_student s
                          LEFT JOIN acad_specialisation sp ON sp.spec_id = s.specialisation
                          WHERE s.regno=@r LIMIT 1", conn))
                    {
                        cmd.Parameters.AddWithValue("@r", regno);
                        using (var rdr = cmd.ExecuteReader())
                            if (rdr.Read())
                            {
                                currentSpec = SafeStr(rdr, "specialisation");
                                currentSpecName = SafeStr(rdr, "spec");
                                string sprog = SafeStr(rdr, "sprog");
                                currentIsForeign = sprog != "" && prog != "" &&
                                                   !string.Equals(sprog, prog, StringComparison.OrdinalIgnoreCase);
                            }
                    }

                if (prog != "")
                    using (var cmd = new MySqlCommand(
                        @"SELECT sp.spec_id, TRIM(sp.spec) spec, TRIM(IFNULL(sp.abbrev,'')) abbrev,
                                 UPPER(TRIM(IFNULL(sp.is_active,'Active')))='ACTIVE' AS is_on,
                                 (SELECT COUNT(*) FROM acad_student st WHERE st.specialisation = sp.spec_id) students
                          FROM acad_specialisation sp
                          WHERE sp.prog_id=@p
                            AND TRIM(IFNULL(sp.spec,''))<>'' AND TRIM(sp.spec)<>'-'
                          ORDER BY is_on DESC, students DESC, sp.spec", conn))
                    {
                        cmd.Parameters.AddWithValue("@p", prog);
                        using (var rdr = cmd.ExecuteReader())
                            while (rdr.Read())
                            {
                                string id = SafeStr(rdr, "spec_id");
                                if (id == "" || !seen.Add(id)) continue;
                                items.Add(new
                                {
                                    id = id,
                                    name = SafeStr(rdr, "spec"),
                                    abbrev = SafeStr(rdr, "abbrev"),
                                    active = SafeStr(rdr, "is_on") == "1",
                                    students = SafeStr(rdr, "students"),
                                    foreign_ = false
                                });
                            }
                    }

                // The student's own specialisation, if the programme list did not already carry it.
                if (currentSpec != "" && currentSpecName != "" && currentSpecName != "-" && !seen.Contains(currentSpec))
                {
                    items.Insert(0, new
                    {
                        id = currentSpec,
                        name = currentSpecName,
                        abbrev = "",
                        active = true,
                        students = "",
                        foreign_ = currentIsForeign
                    });
                    seen.Add(currentSpec);
                }
            }

            WriteJsonAndComplete(js, new
            {
                success = true,
                items = items,
                hasSpecs = items.Count > 0,
                current = currentSpec,
                currentName = currentSpecName
            });
        }
        catch (Exception ex) { WriteJsonAndComplete(js, new { success = false, message = ex.Message }); }
    }

    /// <summary>Moves a student to a different programme (and optional specialisation). Validates the
    /// programme exists and the specialisation belongs to it; logged to acad_activity_log.</summary>
    private void HandleChangeProgramme()
    {
        var js = new JavaScriptSerializer();
        try
        {
            string regno = (Request.Form["regno"] ?? "").Trim();
            string prog  = (Request.Form["prog"] ?? "").Trim();
            string spec  = (Request.Form["spec"] ?? "").Trim();   // spec_id, or "" for none
            bool changeProg  = (Request.Form["changeProg"]  ?? "0") == "1";  // caller explicitly wants to move programme
            bool updateRegNo = (Request.Form["updateRegNo"] ?? "0") == "1";  // caller explicitly wants the reg/entry no regenerated
            if (regno == "") { WriteJsonAndComplete(js, new { success = false, message = "Registration number is required." }); return; }

            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                string oldProg = "", oldEntryno = "", studCampus = "";
                using (var cmd = new MySqlCommand("SELECT progid, entryno, studCampus FROM acad_student WHERE regno=@r LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read()) { WriteJsonAndComplete(js, new { success = false, message = "Student not found." }); return; }
                        oldProg = SafeStr(rdr, "progid"); oldEntryno = SafeStr(rdr, "entryno"); studCampus = SafeStr(rdr, "studCampus");
                    }
                }

                // Campus letter for the rebuilt number. The operator's explicit choice wins;
                // otherwise it comes from the campus on the student's record, not from the old
                // number — 1,710 students carry an "M" while their record says Kakeeka, and
                // carrying that letter forward would keep propagating the wrong one.
                string campusLetter = (Request.Form["campusLetter"] ?? "").Trim().ToUpperInvariant();
                if (campusLetter != "K" && campusLetter != "M") campusLetter = CampusLetterFor(studCampus);

                // Backend enforcement (independent of the UI checkboxes — defence in depth):
                //  • the programme changes ONLY when the caller asked to change it AND picked a *different* one;
                //  • the reg/entry number is regenerated ONLY on top of that, and only when the caller opted in.
                // Without those flags the reg/entry number is never touched — the common case is a spec-only edit.
                bool progChanged  = changeProg && prog != "" && !string.Equals(prog, oldProg, StringComparison.OrdinalIgnoreCase);
                string effProg    = progChanged ? prog : oldProg;   // spec is validated against the effective programme
                bool regenEntryno = progChanged && updateRegNo;

                using (var cmd = new MySqlCommand("SELECT progname FROM acad_programme WHERE progcode=@p LIMIT 1", conn))
                { cmd.Parameters.AddWithValue("@p", effProg); if (cmd.ExecuteScalar() == null) { WriteJsonAndComplete(js, new { success = false, message = "Programme '" + effProg + "' does not exist." }); return; } }

                // The specialisation must belong to the effective programme — EXCEPT when it is
                // simply the one the student already has. 49 students carry a specialisation
                // declared against another programme; refusing it here would block every
                // unrelated edit on those records, and re-saving an unchanged value should never
                // be treated as an attempted move. Anything genuinely new is still checked.
                if (spec != "")
                {
                    string existingSpec = "";
                    using (var cmd = new MySqlCommand("SELECT IFNULL(specialisation,'') FROM acad_student WHERE regno=@r LIMIT 1", conn))
                    { cmd.Parameters.AddWithValue("@r", regno); object o = cmd.ExecuteScalar(); existingSpec = o == null ? "" : Convert.ToString(o).Trim(); }

                    if (!string.Equals(spec, existingSpec, StringComparison.OrdinalIgnoreCase))
                        using (var cmd = new MySqlCommand("SELECT 1 FROM acad_specialisation WHERE spec_id=@s AND prog_id=@p LIMIT 1", conn))
                        {
                            cmd.Parameters.AddWithValue("@s", spec); cmd.Parameters.AddWithValue("@p", effProg);
                            if (cmd.ExecuteScalar() == null)
                            { WriteJsonAndComplete(js, new { success = false, message = "That specialisation does not belong to the selected programme." }); return; }
                        }
                }

                // The formatted registration number (entryno) embeds the programme code + the per-programme
                // student number. It is regenerated ONLY when the programme actually moves AND the caller
                // opted in. The canonical acad_student.regno (PRIMARY KEY / system-wide FK) is never changed.
                string newEntryno = regenEntryno ? ComputeNewEntryno(conn, oldEntryno, prog, campusLetter) : "";

                // If the operator settles a campus conflict, the campus FIELD moves with the letter.
                // Writing "M" into the number while the record still says Kakeeka would just recreate
                // the disagreement this dialog exists to resolve.
                string newCampusCode = campusLetter == "M" ? "2" : (campusLetter == "K" ? "1" : "");
                bool campusChanged = regenEntryno && newCampusCode != "" &&
                                     newCampusCode != (studCampus ?? "").Trim().TrimStart('0');

                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        var sets = new System.Collections.Generic.List<string> { "specialisation=@s" };
                        if (progChanged)      sets.Add("progid=@p");
                        if (newEntryno != "") sets.Add("entryno=@e");
                        if (campusChanged)    sets.Add("studCampus=@cm");
                        using (var cmd = new MySqlCommand("UPDATE acad_student SET " + string.Join(", ", sets) + " WHERE regno=@r", conn, tx))
                        {
                            // acad_student.specialisation is NOT NULL (default '-'). For programmes/faculties
                            // that have NO subject combinations, the caller sends an empty spec — store the
                            // "none" sentinel '-' rather than NULL, which previously failed with
                            // "Column 'specialisation' cannot be null" and blocked the whole course change.
                            cmd.Parameters.AddWithValue("@s", spec == "" ? (object)"-" : spec);
                            if (progChanged)      cmd.Parameters.AddWithValue("@p", prog);
                            if (newEntryno != "") cmd.Parameters.AddWithValue("@e", newEntryno);
                            if (campusChanged)    cmd.Parameters.AddWithValue("@cm", newCampusCode);
                            cmd.Parameters.AddWithValue("@r", regno);
                            cmd.ExecuteNonQuery();
                        }
                        // Keep the only other table that references entryno in step (only if it actually changed).
                        if (newEntryno != "" && oldEntryno != "" && !string.Equals(newEntryno, oldEntryno))
                            using (var cmd = new MySqlCommand("UPDATE acad_haltcases SET entryno=@n WHERE entryno=@o", conn, tx))
                            { cmd.Parameters.AddWithValue("@n", newEntryno); cmd.Parameters.AddWithValue("@o", oldEntryno); cmd.ExecuteNonQuery(); }
                        tx.Commit();
                    }
                    catch { tx.Rollback(); throw; }
                }

                string campusNote = campusChanged
                    ? ("; Campus " + studCampus + " -> " + newCampusCode + " (" + CampusNameForLetter(campusLetter) + ")") : "";
                string logMsg = progChanged
                    ? ("Programme " + oldProg + " -> " + prog + (spec != "" ? " (spec " + spec + ")" : "")
                        + (newEntryno != "" ? "; Reg No " + oldEntryno + " -> " + newEntryno : "; Reg No kept " + oldEntryno)
                        + campusNote)
                    : ("Specialisation updated" + (spec != "" ? " (spec " + spec + ")" : " (cleared)") + "; programme & reg no unchanged");
                LogStudentAction(conn, regno, "ChangeProgramme", logMsg);

                string userMsg = progChanged
                    ? ("Programme updated to " + prog + "." + (newEntryno != "" ? " New Reg No: " + newEntryno : " Registration number kept unchanged.")
                        + (campusChanged ? " Campus set to " + CampusNameForLetter(campusLetter) + "." : ""))
                    : "Specialisation updated. Programme and registration number were left unchanged.";
                WriteJsonAndComplete(js, new { success = true, newEntryno = newEntryno, message = userMsg });
            }
        }
        catch (Exception ex) { WriteJsonAndComplete(js, new { success = false, message = ex.Message }); }
    }

    /// <summary>
    /// Campus letter used in segment 5 of an entry number: <b>K</b> = Kakeeka (campus 1),
    /// <b>M</b> = Kirumba / Masaka (campus 2). "" when the campus is unknown, in which case the
    /// existing letter is left alone rather than guessed at.
    /// </summary>
    private static string CampusLetterFor(string studCampus)
    {
        string c = (studCampus ?? "").Trim().TrimStart('0');
        return c == "2" ? "M" : (c == "1" ? "K" : "");
    }

    private static string CampusNameForLetter(string letter)
    {
        letter = (letter ?? "").Trim().ToUpperInvariant();
        if (letter.StartsWith("M")) return "Kirumba (Masaka)";
        if (letter.StartsWith("K")) return "Kakeeka";
        return "";
    }

    /// <summary>Segment 5 of an entry number — the campus letter — or "" when the number is too short.</summary>
    private static string CampusSegmentOf(string entryno)
    {
        if (string.IsNullOrWhiteSpace(entryno)) return "";
        string[] seg = entryno.Split('/');
        return seg.Length >= 5 ? (seg[4] ?? "").Trim() : "";
    }

    /// <summary>Rebuilds the formatted registration number (entryno) for a new programme: keeps the year
    /// prefix / level / session segments, sets segment 3 = new programme code, segment 4 =
    /// the next free student-number for that (year, programme), and segment 5 = the campus letter when the
    /// caller supplies one. Returns "" when the entryno isn't in the
    /// standard slash format (then only progid/specialisation change). Guards against a duplicate entryno.</summary>
    private string ComputeNewEntryno(MySqlConnection conn, string oldEntryno, string newProg, string campusLetter)
    {
        if (string.IsNullOrWhiteSpace(oldEntryno)) return "";
        string[] seg = oldEntryno.Split('/');
        if (seg.Length < 4) return "";                 // not the YY/U/PROG/SEQ/... form → leave identifiers alone
        string yearPrefix = seg[0];
        string oldSeq = (seg[3] ?? "").Trim();

        // Campus letter. Only the FIRST character is replaced: legacy numbers carry two-letter
        // codes there (KD, MJ, MM) whose second character means something we do not model, and
        // rewriting the whole segment would throw that away.
        campusLetter = (campusLetter ?? "").Trim().ToUpperInvariant();
        if (seg.Length >= 5 && campusLetter.Length == 1)
        {
            string cur = (seg[4] ?? "").Trim();
            seg[4] = cur.Length > 1 ? campusLetter + cur.Substring(1) : campusLetter;
        }

        long next = 1;
        using (var cmd = new MySqlCommand(
            @"SELECT MAX(CAST(SUBSTRING_INDEX(SUBSTRING_INDEX(entryno,'/',4),'/',-1) AS UNSIGNED))
                FROM acad_student
               WHERE SUBSTRING_INDEX(entryno,'/',1)=@yr
                 AND SUBSTRING_INDEX(SUBSTRING_INDEX(entryno,'/',3),'/',-1)=@prog", conn))
        {
            cmd.Parameters.AddWithValue("@yr", yearPrefix);
            cmd.Parameters.AddWithValue("@prog", newProg);
            var o = cmd.ExecuteScalar();
            long m; if (o != null && o != DBNull.Value && long.TryParse(o.ToString(), out m)) next = m + 1;
        }

        int ov, width = 4;
        if (int.TryParse(oldSeq, out ov)) width = Math.Max(4, oldSeq.Length);

        // Build, then bump the number until the entryno is unique (defends against a rare race).
        for (int guard = 0; guard < 1000; guard++)
        {
            seg[2] = newProg;
            seg[3] = next.ToString().PadLeft(width, '0');
            string candidate = string.Join("/", seg);
            using (var cmd = new MySqlCommand("SELECT 1 FROM acad_student WHERE entryno=@e LIMIT 1", conn))
            { cmd.Parameters.AddWithValue("@e", candidate); if (cmd.ExecuteScalar() == null) return candidate; }
            next++;
        }
        return "";   // extremely unlikely — fall back to leaving entryno unchanged
    }

    /// <summary>Preview the new Reg No / student number a programme change would produce (no write).</summary>
    private void HandlePreviewProgRegno()
    {
        var js = new JavaScriptSerializer();
        try
        {
            string regno = (Request.Form["regno"] ?? Request.QueryString["regno"] ?? "").Trim();
            string prog  = (Request.Form["prog"]  ?? Request.QueryString["prog"]  ?? "").Trim();
            if (regno == "" || prog == "") { WriteJsonAndComplete(js, new { success = false, message = "Missing parameters." }); return; }
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string oldEntryno = "", studCampus = "", campusName = "";
                using (var cmd = new MySqlCommand(
                    "SELECT s.entryno, IFNULL(s.studCampus,'') sc, IFNULL(c.campus_name,'') cn FROM acad_student s " +
                    "LEFT JOIN acad_campuses c ON TRIM(LEADING '0' FROM c.campus_code) = TRIM(LEADING '0' FROM s.studCampus) " +
                    "WHERE s.regno=@r LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    using (var rdr = cmd.ExecuteReader())
                        if (rdr.Read())
                        { oldEntryno = SafeStr(rdr, "entryno"); studCampus = SafeStr(rdr, "sc"); campusName = SafeStr(rdr, "cn"); }
                }

                string letterFromRecord = CampusLetterFor(studCampus);
                string letterInOldNumber = CampusSegmentOf(oldEntryno);
                string chosen = (Request.Form["campusLetter"] ?? Request.QueryString["campusLetter"] ?? "").Trim().ToUpperInvariant();
                if (chosen != "K" && chosen != "M") chosen = letterFromRecord;

                string preview = ComputeNewEntryno(conn, oldEntryno, prog, chosen);

                // A conflict is reported, never resolved silently: only a human knows whether the
                // student sits in Masaka or Kakeeka when the record and the number disagree.
                bool conflict = letterFromRecord != "" && letterInOldNumber != "" &&
                                !letterInOldNumber.StartsWith(letterFromRecord, StringComparison.OrdinalIgnoreCase);

                WriteJsonAndComplete(js, new
                {
                    success = true,
                    oldEntryno = oldEntryno,
                    newEntryno = preview,
                    campusCode = studCampus,
                    campusName = campusName,
                    letterOnRecord = letterFromRecord,
                    letterInNumber = letterInOldNumber,
                    letterUsed = chosen,
                    campusOnRecordName = CampusNameForLetter(letterFromRecord),
                    campusInNumberName = CampusNameForLetter(letterInOldNumber),
                    campusConflict = conflict
                });
            }
        }
        catch (Exception ex) { WriteJsonAndComplete(js, new { success = false, message = ex.Message }); }
    }

    /// <summary>Changes a student's entry year (validated 1990..currentYear+1); logged.</summary>
    // ── Quick Edit: load a single student's editable fields + dropdown option sources ──
    private void HandleQuickEditLoad()
    {
        var js = new JavaScriptSerializer();
        try
        {
            string regno = (Request.Form["regno"] ?? Request.QueryString["regno"] ?? "").Trim();
            if (regno == "") { WriteJsonAndComplete(js, new { success = false, message = "Registration number is required." }); return; }
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                System.Collections.Generic.Dictionary<string, object> stud = null;
                using (var cmd = new MySqlCommand(
                    "SELECT s.regno, IFNULL(s.entryno,'') entryno, IFNULL(s.firstname,'') firstname, IFNULL(s.othername,'') othername," +
                    " IFNULL(s.gender,'') gender, CASE WHEN s.dob IS NULL OR s.dob='0000-00-00' THEN '' ELSE DATE_FORMAT(s.dob,'%Y-%m-%d') END dob," +
                    " IFNULL(s.nationality,'') nationality, IFNULL(s.religion,'') religion, IFNULL(s.studPhone,'') studPhone, IFNULL(s.email,'') email," +
                    " IFNULL(s.home_dist,'') home_dist, IFNULL(s.national_id,'') national_id, IFNULL(s.entryyear,'') entryyear, IFNULL(s.intake,'') intake," +
                    " IFNULL(s.studsesion,'') studsesion, IFNULL(s.studCampus,'') studCampus, IFNULL(s.gradSystemID,'') gradSystemID," +
                    " IFNULL(s.entrymethod,'') entrymethod, IFNULL(s.new_status,'') new_status," +
                    " CASE WHEN s.completion_date IS NULL OR s.completion_date='0000-00-00' THEN '' ELSE DATE_FORMAT(s.completion_date,'%Y-%m-%d') END completion_date," +
                    " IFNULL(p.progname,'') progname, IFNULL(s.progid,'') progid" +
                    " FROM acad_student s LEFT JOIN acad_programme p ON p.progcode=s.progid WHERE s.regno=@r LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read()) { WriteJsonAndComplete(js, new { success = false, message = "Student not found." }); return; }
                        stud = new System.Collections.Generic.Dictionary<string, object>();
                        for (int i = 0; i < r.FieldCount; i++) stud[r.GetName(i)] = r.IsDBNull(i) ? "" : r.GetValue(i).ToString();
                    }
                }
                var campuses = new System.Collections.Generic.List<object>();
                using (var cmd = new MySqlCommand("SELECT ID, campus_name FROM acad_campuses WHERE ID>0 ORDER BY ID", conn))
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) campuses.Add(new { id = r.GetValue(0).ToString(), name = r.GetValue(1).ToString() });
                var grading = new System.Collections.Generic.List<object>();
                using (var cmd = new MySqlCommand("SELECT ID, gs_name FROM acad_gradingsystem ORDER BY ID", conn))
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) grading.Add(new { id = r.GetValue(0).ToString(), name = r.GetValue(1).ToString() });
                WriteJsonAndComplete(js, new { success = true, student = stud, campuses = campuses, grading = grading });
            }
        }
        catch (Exception ex) { WriteJsonAndComplete(js, new { success = false, message = ex.Message }); }
    }

    // ── Quick Edit: save the edited fields (strict-SQL-mode safe; identity fields untouched) ──
    // ===================================================================
    // Entry number ↔ study session
    // ===================================================================
    //
    // The formatted entry number is YY/U/PROG/SEQ/CAMPUS/SESSION — the number printed
    // on the transcript as "REG NO." (21/U/BED(P)/1238/KA/INS). Its last segment encodes
    // the study session, and changing acad_student.studsesion has never touched it, so a
    // student moved from Day to Weekend kept an entry number that still said DAY.
    //
    // WHAT MAKES THIS DELICATE, measured before writing any of it: the format is not
    // uniform. Of the 32,689 slash-formatted entry numbers, 21,403 have six segments,
    // 8,213 have five, 1,984 have four and 1,088 have seven. The last segment is a
    // session code only in the six-segment form; elsewhere it is a campus code (K, M),
    // a bare number (007, 104), or empty. Rewriting "the last segment" blindly would
    // corrupt roughly a third of the student body's entry numbers.
    //
    // So the rule is the opposite of blind: the segment is replaced ONLY when it is
    // already a recognisable session code. Anything else is left exactly alone and the
    // offer is never made.
    //
    // acad_student.regno — the primary key, referenced across the whole system — is
    // NEVER touched here, the same rule the programme-change action follows.

    /// <summary>
    /// The code written into the entry number for a given session. Reflects what the
    /// register actually uses for 2022 and later: DAY 5,230, WKD 1,377, INSRV 977
    /// (against INS 396 for the same session), EVE for evening.
    /// </summary>
    private static string SessionSegmentCode(string session)
    {
        string s = (session ?? "").Trim().ToUpperInvariant();
        if (s == "DAY") return "DAY";
        if (s == "WEEKEND") return "WKD";
        if (s == "EVENING") return "EVE";
        if (s == "INSERVICE" || s == "IN-SERVICE" || s == "IN SERVICE") return "INSRV";
        return "";
    }

    /// <summary>
    /// Every spelling of a session code seen in the register. Used to decide whether the
    /// last segment of an entry number is a session at all — which is the whole safety
    /// of this feature. "K" and "M" are campuses and must never match.
    /// </summary>
    private static bool IsSessionSegment(string segment)
    {
        string s = (segment ?? "").Trim().ToUpperInvariant();
        switch (s)
        {
            case "DAY": case "D":
            case "WKD": case "WKND": case "WEEKEND":
            case "EVE": case "EVENING":
            case "INS": case "INSR": case "INSRV": case "INSERVICE":
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// The entry number this student would have if its session segment matched
    /// <paramref name="newSession"/>. Returns "" when the change does not apply — no
    /// entry number, no slashes, an unrecognised last segment, or nothing to change.
    /// <paramref name="reason"/> explains which, for the screen to show.
    /// </summary>
    private static string ProjectEntrynoForSession(string entryno, string newSession, string overrideCode, out string reason)
    {
        reason = "";
        string e = (entryno ?? "").Trim();
        if (e == "") { reason = "This student has no entry number on file."; return ""; }
        if (e.IndexOf('/') < 0) { reason = "This entry number is not in the slash format, so it carries no session."; return ""; }

        string[] parts = e.Split('/');
        string last = parts[parts.Length - 1].Trim();

        if (!IsSessionSegment(last))
        {
            reason = "The last part of this entry number (\"" + last + "\") is not a session code, "
                   + "so it is left alone. Only entry numbers ending in DAY, EVE, WKD or INSRV are changed here.";
            return "";
        }

        string code = (overrideCode ?? "").Trim().ToUpperInvariant();
        if (code == "") code = SessionSegmentCode(newSession);
        if (code == "") { reason = "There is no entry-number code for the session \"" + newSession + "\"."; return ""; }

        if (string.Equals(last, code, StringComparison.OrdinalIgnoreCase))
        { reason = "The entry number already ends in " + code + "."; return ""; }

        parts[parts.Length - 1] = code;
        return string.Join("/", parts);
    }

    /// <summary>
    /// action=PreviewEntrynoForSession — what the entry number would become, shown live
    /// in the Quick Edit dialog before anything is saved. Read-only.
    /// </summary>
    private void HandlePreviewEntrynoForSession()
    {
        var js = new JavaScriptSerializer();
        try
        {
            string regno = (Request["regno"] ?? "").Trim();
            string session = (Request["studsesion"] ?? "").Trim();
            string code = (Request["code"] ?? "").Trim();
            if (regno == "") { WriteJsonAndComplete(js, new { success = false, message = "Registration number is required." }); return; }

            string entryno = "", currentSession = "";
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT IFNULL(entryno,''), IFNULL(studsesion,'') FROM acad_student WHERE regno=@r LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read()) { WriteJsonAndComplete(js, new { success = false, message = "Student not found." }); return; }
                        entryno = r.GetString(0).Trim();
                        currentSession = r.GetString(1).Trim();
                    }
                }
            }

            string reason;
            string projected = ProjectEntrynoForSession(entryno, session, code, out reason);
            WriteJsonAndComplete(js, new
            {
                success = true,
                entryno = entryno,
                currentSession = currentSession,
                sessionChanged = !string.Equals(currentSession, session, StringComparison.OrdinalIgnoreCase),
                canUpdate = projected != "",
                projected = projected,
                suggestedCode = SessionSegmentCode(session),
                reason = reason
            });
        }
        catch (Exception ex) { WriteJsonAndComplete(js, new { success = false, message = ex.Message }); }
    }

    /// <summary>
    /// Rewrites the session segment of a student's entry number, after re-checking every
    /// condition the dialog checked. The browser's tick is treated as a request, not as
    /// permission: an admin who opened the dialog, changed the session, ticked the box
    /// and then changed the session back must not end up with a rewritten number.
    ///
    /// Returns a sentence to append to the success message, or "" when nothing was done.
    /// Never throws: the student's own details are already saved by this point, and a
    /// problem with the entry number must not report that save as a failure.
    /// </summary>
    private string ApplyEntrynoSessionChange(MySqlConnection conn, string regno,
        string priorEntryno, string priorSession, string newSession, string overrideCode)
    {
        try
        {
            if (string.Equals(priorSession, newSession, StringComparison.OrdinalIgnoreCase))
                return "  The entry number was left unchanged because the session did not change.";

            string reason;
            string projected = ProjectEntrynoForSession(priorEntryno, newSession, overrideCode, out reason);
            if (projected == "")
                return "  The entry number was left unchanged. " + reason;

            // The code came from a text box, so it is bounded here rather than trusted:
            // letters only, and short. A slash would silently add a segment; anything
            // longer than this is not a session code.
            string tail = projected.Substring(projected.LastIndexOf('/') + 1);
            if (tail.Length == 0 || tail.Length > 8)
                return "  The entry number was left unchanged: \"" + tail + "\" is not a valid session code.";
            foreach (char c in tail)
                if (!char.IsLetter(c))
                    return "  The entry number was left unchanged: a session code may only contain letters.";

            // entryno has no unique index and 135 duplicate values already exist, so the
            // database will not catch a collision. Landing on ANOTHER student's number is
            // a different matter from the historical duplicates, and is worth stopping for.
            using (var dup = new MySqlCommand(
                "SELECT regno FROM acad_student WHERE TRIM(entryno)=TRIM(@e) AND regno<>@r LIMIT 1", conn))
            {
                dup.Parameters.AddWithValue("@e", projected);
                dup.Parameters.AddWithValue("@r", regno);
                object other = dup.ExecuteScalar();
                if (other != null && other != DBNull.Value)
                    return "  The entry number was NOT changed: " + projected + " already belongs to "
                         + other.ToString().Trim() + ". Please resolve that first.";
            }

            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    using (var up = new MySqlCommand("UPDATE acad_student SET entryno=@e WHERE regno=@r", conn, tx))
                    { up.Parameters.AddWithValue("@e", projected); up.Parameters.AddWithValue("@r", regno); up.ExecuteNonQuery(); }

                    // acad_haltcases is the only other table carrying an entry number
                    // (confirmed against information_schema), so it moves with it or the
                    // two records stop referring to the same student.
                    using (var up = new MySqlCommand("UPDATE acad_haltcases SET entryno=@e WHERE TRIM(entryno)=TRIM(@old)", conn, tx))
                    { up.Parameters.AddWithValue("@e", projected); up.Parameters.AddWithValue("@old", priorEntryno); up.ExecuteNonQuery(); }

                    tx.Commit();
                }
                catch { tx.Rollback(); throw; }
            }

            LogStudentAction(conn, regno, "QuickEditEntrynoSession",
                "Session " + priorSession + " -> " + newSession + "; entry number " + priorEntryno + " -> " + projected);

            return "  Entry number updated: " + priorEntryno + "  ->  " + projected;
        }
        catch (Exception ex)
        {
            return "  The student's details were saved, but the entry number could not be updated: " + ex.Message;
        }
    }

    private void HandleQuickEditSave()
    {
        var js = new JavaScriptSerializer();
        try
        {
            string regno = (Request.Form["regno"] ?? "").Trim();
            if (regno == "") { WriteJsonAndComplete(js, new { success = false, message = "Registration number is required." }); return; }
            System.Func<string, string> F = k => (Request.Form[k] ?? "").ToString().Trim();

            string firstname = F("firstname");
            if (firstname == "") { WriteJsonAndComplete(js, new { success = false, message = "First name is required." }); return; }
            string othername = F("othername"), gender = F("gender"), nationality = F("nationality"), religion = F("religion"),
                   phone = F("studPhone"), email = F("email"), district = F("home_dist"), nin = F("national_id"),
                   entryyear = F("entryyear"), intake = F("intake"), session = F("studsesion"), campus = F("studCampus"),
                   gradsystem = F("gradSystemID"), entrymethod = F("entrymethod"), newstatus = F("new_status"),
                   dobS = F("dob"), compS = F("completion_date");
            if (email != "" && !email.Contains("@")) { WriteJsonAndComplete(js, new { success = false, message = "Invalid email format." }); return; }
            if (newstatus == "") newstatus = "ADMITTED";

            // Strict-SQL-mode safe: numerics/dates parse or bind NULL; NOT NULL studCampus omitted when blank.
            int _iv;
            object pEntry = int.TryParse(entryyear, out _iv) ? (object)_iv : DBNull.Value;
            object pGrad  = int.TryParse(gradsystem, out _iv) ? (object)_iv : DBNull.Value;
            bool campusOk = int.TryParse(campus, out _iv); int campusVal = campusOk ? _iv : 0;
            object pDob = DBNull.Value; DateTime _dv;
            if (dobS != "" && DateTime.TryParse(dobS, out _dv) && _dv.Year > 1900) pDob = _dv.Date;
            object pComp = DBNull.Value; DateTime _cv;
            if (compS != "" && DateTime.TryParse(compS, out _cv) && _cv.Year > 1900) pComp = _cv.Date;

            var sets = new System.Collections.Generic.List<string>();
            sets.Add("firstname=@firstname"); sets.Add("othername=@othername"); sets.Add("gender=@gender");
            sets.Add("dob=@dob"); sets.Add("nationality=@nationality"); sets.Add("religion=@religion");
            sets.Add("studPhone=@phone"); sets.Add("email=@email"); sets.Add("home_dist=@district");
            sets.Add("national_id=@nin"); sets.Add("entryyear=@entryyear"); sets.Add("intake=@intake");
            sets.Add("entrymethod=@entrymethod");
            if (session != "") sets.Add("studsesion=@session");
            if (campusOk) sets.Add("studCampus=@campus");
            sets.Add("gradSystemID=@gradsystem"); sets.Add("completion_date=@completion_date"); sets.Add("new_status=@newstatus");
            string sql = "UPDATE acad_student SET " + string.Join(", ", sets.ToArray()) + " WHERE regno=@regno";

            // Opt-in, from the "also update the entry number" tick in the dialog. Enforced
            // again below regardless of what the browser sent.
            bool updateEntryno = F("updateEntryno") == "1";
            string entrynoNote = "";
            string priorEntryno = "", priorSession = "";

            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                // Read the session and entry number BEFORE the update: the update
                // overwrites studsesion, and "did the session actually change" cannot be
                // answered afterwards.
                if (updateEntryno)
                {
                    using (var pre = new MySqlCommand("SELECT IFNULL(entryno,''), IFNULL(studsesion,'') FROM acad_student WHERE regno=@r LIMIT 1", conn))
                    {
                        pre.Parameters.AddWithValue("@r", regno);
                        using (var pr = pre.ExecuteReader())
                            if (pr.Read()) { priorEntryno = pr.GetString(0).Trim(); priorSession = pr.GetString(1).Trim(); }
                    }
                }

                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.CommandTimeout = 30;
                    cmd.Parameters.AddWithValue("@regno", regno);
                    cmd.Parameters.AddWithValue("@firstname", firstname);
                    cmd.Parameters.AddWithValue("@othername", othername);
                    cmd.Parameters.AddWithValue("@gender", gender);
                    cmd.Parameters.AddWithValue("@dob", pDob);
                    cmd.Parameters.AddWithValue("@nationality", nationality);
                    cmd.Parameters.AddWithValue("@religion", religion);
                    cmd.Parameters.AddWithValue("@phone", phone);
                    cmd.Parameters.AddWithValue("@email", email);
                    cmd.Parameters.AddWithValue("@district", district);
                    cmd.Parameters.AddWithValue("@nin", nin);
                    cmd.Parameters.AddWithValue("@entryyear", pEntry);
                    cmd.Parameters.AddWithValue("@intake", intake);
                    cmd.Parameters.AddWithValue("@entrymethod", entrymethod);
                    if (session != "") cmd.Parameters.AddWithValue("@session", session);
                    if (campusOk) cmd.Parameters.AddWithValue("@campus", campusVal);
                    cmd.Parameters.AddWithValue("@gradsystem", pGrad);
                    cmd.Parameters.AddWithValue("@completion_date", pComp);
                    cmd.Parameters.AddWithValue("@newstatus", newstatus);
                    int rows = cmd.ExecuteNonQuery();
                    if (rows == 0)
                    {
                        bool exists;
                        using (var chk = new MySqlCommand("SELECT COUNT(*) FROM acad_student WHERE regno=@r", conn))
                        { chk.Parameters.AddWithValue("@r", regno); exists = Convert.ToInt64(chk.ExecuteScalar()) > 0; }
                        if (!exists) { WriteJsonAndComplete(js, new { success = false, message = "No student found with regno " + regno + "." }); return; }
                    }
                }

                if (updateEntryno)
                    entrynoNote = ApplyEntrynoSessionChange(conn, regno, priorEntryno, priorSession, session, F("entrynoCode"));
            }
            WriteJsonAndComplete(js, new { success = true, message = "Student " + regno + " updated successfully." + entrynoNote });
        }
        catch (Exception ex) { WriteJsonAndComplete(js, new { success = false, message = ex.Message }); }
    }

    private void HandleChangeEntryYear()
    {
        var js = new JavaScriptSerializer();
        try
        {
            string regno = (Request.Form["regno"] ?? "").Trim();
            string yearS = (Request.Form["year"] ?? "").Trim();
            int year;
            if (regno == "" || !int.TryParse(yearS, out year))
            { WriteJsonAndComplete(js, new { success = false, message = "Registration number and a valid year are required." }); return; }
            if (year < 1990 || year > DateTime.Now.Year + 1)
            { WriteJsonAndComplete(js, new { success = false, message = "Entry year must be between 1990 and " + (DateTime.Now.Year + 1) + "." }); return; }

            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string oldY = "";
                using (var cmd = new MySqlCommand("SELECT entryyear FROM acad_student WHERE regno=@r LIMIT 1", conn))
                { cmd.Parameters.AddWithValue("@r", regno); var o = cmd.ExecuteScalar(); if (o == null) { WriteJsonAndComplete(js, new { success = false, message = "Student not found." }); return; } oldY = o == DBNull.Value ? "" : o.ToString(); }

                using (var cmd = new MySqlCommand("UPDATE acad_student SET entryyear=@y WHERE regno=@r", conn))
                { cmd.Parameters.AddWithValue("@y", year); cmd.Parameters.AddWithValue("@r", regno); cmd.ExecuteNonQuery(); }
                LogStudentAction(conn, regno, "ChangeEntryYear", "Entry year " + oldY + " -> " + year);
                WriteJsonAndComplete(js, new { success = true, message = "Entry year updated to " + year + "." });
            }
        }
        catch (Exception ex) { WriteJsonAndComplete(js, new { success = false, message = ex.Message }); }
    }

    /// <summary>Streams the CURRENT filtered student list (all rows, not just the page) with the
    /// user-selected columns, as CSV or Excel-openable HTML. Reuses GetStudentsData so filters + data
    /// exactly match the on-screen list.</summary>
    private void HandleExportStudentsList()
    {
        try
        {
            string fmt  = (Request.QueryString["fmt"] ?? "csv").Trim().ToLowerInvariant();
            string cols = (Request.QueryString["cols"] ?? "").Trim();

            // key -> (header, DataTable field). Order here is the fallback order.
            var map = new System.Collections.Generic.List<string[]> {
                new[]{"entryno","Reg No","entryno"}, new[]{"regno","Entry No","regno"},
                new[]{"name","Student Name","__name"}, new[]{"gender","Gender","gender"},
                new[]{"phone","Phone","studPhone"}, new[]{"email","Email","email"},
                new[]{"entryyear","Entry Year","entryyear"}, new[]{"intake","Intake","intake"},
                new[]{"prog","Programme Code","progcode"}, new[]{"progname","Programme","progname"},
                new[]{"spec","Specialisation","spec_name"}, new[]{"session","Session","studsesion"},
                new[]{"campus","Campus","campus_name"}, new[]{"status","Status","new_status"},
                new[]{"nationality","Nationality","nationality"}, new[]{"nin","NIN","national_id"},
                new[]{"district","Home District","home_dist"}, new[]{"registered","Registered","is_registered"}
            };
            var wanted = new System.Collections.Generic.HashSet<string>(
                (cols == "" ? "entryno,regno,name,gender,phone,email,entryyear,prog,progname,spec,session,campus,status" : cols)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            var chosen = map.FindAll(m => wanted.Contains(m[0]));
            if (chosen.Count == 0) chosen = map.GetRange(0, 13);

            int total;
            DataTable dt = GetStudentsData(0, int.MaxValue, out total);   // ALL filtered rows

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
            if (fmt == "excel" || fmt == "xls")
            {
                Response.Clear();
                Response.ContentType = "application/vnd.ms-excel";
                Response.AddHeader("Content-Disposition", "attachment; filename=\"students_" + stamp + ".xls\"");
                var sb = new StringBuilder();
                sb.Append("<table border='1'><thead><tr>");
                foreach (var c in chosen) sb.Append("<th style='background:#05275C;color:#fff'>").Append(HttpUtility.HtmlEncode(c[1])).Append("</th>");
                sb.Append("</tr></thead><tbody>");
                foreach (DataRow r in dt.Rows)
                {
                    sb.Append("<tr>");
                    foreach (var c in chosen) sb.Append("<td>").Append(HttpUtility.HtmlEncode(ExportCell(r, c[2]))).Append("</td>");
                    sb.Append("</tr>");
                }
                sb.Append("</tbody></table>");
                Response.Write(sb.ToString());
            }
            else
            {
                Response.Clear();
                Response.ContentType = "text/csv";
                Response.AddHeader("Content-Disposition", "attachment; filename=\"students_" + stamp + ".csv\"");
                var sb = new StringBuilder();
                sb.Append(string.Join(",", chosen.ConvertAll(c => CsvCell(c[1])).ToArray())).Append("\r\n");
                foreach (DataRow r in dt.Rows)
                    sb.Append(string.Join(",", chosen.ConvertAll(c => CsvCell(ExportCell(r, c[2]))).ToArray())).Append("\r\n");
                Response.Write(sb.ToString());
            }
            Response.End();
        }
        // Response.End() raises ThreadAbortException — it auto-re-raises out of any catch, so it MUST be
        // caught here (before the general catch) or it lands in "catch (Exception)" as a false failure.
        catch (System.Threading.ThreadAbortException) { /* expected — the framework finishes the response */ }
        catch (Exception ex)
        {
            try { Response.Clear(); Response.ContentType = "text/plain"; Response.Write("Export failed: " + ex.Message); Response.End(); }
            catch (System.Threading.ThreadAbortException) { }
        }
    }

    private static string ExportCell(DataRow r, string field)
    {
        if (field == "__name")
            return ((SafeCol(r, "firstname") + " " + SafeCol(r, "othername")).Trim());
        return SafeCol(r, field);
    }
    private static string SafeCol(DataRow r, string col)
    {
        if (!r.Table.Columns.Contains(col)) return "";
        object o = r[col]; return o == null || o == DBNull.Value ? "" : o.ToString().Trim();
    }
    private static string CsvCell(string s)
    {
        s = s ?? "";
        if (s.IndexOf('"') >= 0 || s.IndexOf(',') >= 0 || s.IndexOf('\n') >= 0 || s.IndexOf('\r') >= 0)
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
    private static string SafeStr(MySqlDataReader r, string col)
    {
        try { object o = r[col]; return o == null || o == DBNull.Value ? "" : o.ToString(); } catch { return ""; }
    }
    private void LogStudentAction(MySqlConnection conn, string regno, string fn, string comment)
    {
        try
        {
            string actor = ""; try { actor = HttpContext.Current.User.Identity.Name; } catch { }
            using (var cmd = new MySqlCommand(
                @"INSERT INTO acad_activity_log (user_id, page_function, par, comments, access_date)
                  VALUES (@u, @f, @p, @c, NOW())", conn))
            {
                cmd.Parameters.AddWithValue("@u", (string.IsNullOrEmpty(actor) ? "system" : actor).Length > 100 ? actor.Substring(0, 100) : (string.IsNullOrEmpty(actor) ? "system" : actor));
                cmd.Parameters.AddWithValue("@f", ("NewStudentInfo:" + fn).Length > 45 ? ("NewStudentInfo:" + fn).Substring(0, 45) : "NewStudentInfo:" + fn);
                cmd.Parameters.AddWithValue("@p", regno.Length > 300 ? regno.Substring(0, 300) : regno);
                cmd.Parameters.AddWithValue("@c", comment.Length > 200 ? comment.Substring(0, 200) : comment);
                cmd.ExecuteNonQuery();
            }
        }
        catch { /* logging must never block the action */ }
    }

    private void WriteJsonAndComplete(JavaScriptSerializer serializer, object payload)
    {
        Response.Clear();
        Response.Buffer = true;
        Response.ContentType = "application/json";
        Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
        Response.Cache.SetNoStore();
        Response.Cache.SetRevalidation(System.Web.HttpCacheRevalidation.AllCaches);
        Response.Expires = -1;
        Response.AppendHeader("Pragma", "no-cache");
        Response.AppendHeader("X-SetPassword-Version", "SETPASSWORD_V2");
        Response.Write(serializer.Serialize(payload));
        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    /// <summary>
    /// A file name that cannot break the Content-Disposition header.
    ///
    /// The quoted filename parameter has no escaping, so a double quote, a semicolon, a
    /// backslash, a newline or any non-ASCII byte inside it truncates or corrupts the
    /// header — and a browser that cannot parse the header falls back to naming the file
    /// after the URL, which for this endpoint is a .aspx page. Those characters are
    /// replaced rather than dropped so the name stays the same length and stays readable.
    /// </summary>
    private static string HeaderSafeFileName(string fileName, string fallback)
    {
        if (string.IsNullOrEmpty(fileName)) return fallback;
        System.Text.StringBuilder sb = new System.Text.StringBuilder(fileName.Length);
        foreach (char c in fileName)
        {
            if (c < 32 || c > 126) { sb.Append('_'); continue; }          // control + non-ASCII
            if (c == '"' || c == '\\' || c == ';' || c == ',') { sb.Append('_'); continue; }
            sb.Append(c);
        }
        string safe = sb.ToString().Trim();
        return safe.Length == 0 ? fallback : safe;
    }

    private void WriteBinaryFileAndComplete(byte[] content, string contentType, string fileName)
    {
        Response.Clear();
        Response.Buffer = true;
        Response.ContentType = string.IsNullOrEmpty(contentType) ? "application/octet-stream" : contentType;
        Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
        Response.Cache.SetNoStore();
        Response.Cache.SetRevalidation(System.Web.HttpCacheRevalidation.AllCaches);
        Response.Expires = -1;
        Response.AppendHeader("Pragma", "no-cache");

        /* A PDF opens in the browser; anything else downloads.
         *
         * These are documents an operator wants to LOOK at — check the marks, check the
         * class of award — before deciding to keep or print them, so forcing a download
         * puts a file-manager round trip in front of every glance.
         *
         * The reason this was briefly "attachment" is worth keeping written down. The
         * request arrives as a form POST targeted at a new tab, so that tab's URL used
         * to be the page itself, .../NewStudentInfo.aspx?action=GenerateAcademicDocument.
         * An inline response lets the browser name a saved file after the URL rather
         * than after this header, which is how a transcript once came down as
         * "NewStudentInfo.asp". The fix for that is NOT to stop previewing — it is to
         * stop the URL being a lie: the form now posts to
         * .../NewStudentInfo.aspx/<name>.pdf?action=..., which ASP.NET routes through
         * PathInfo (verified against production) and which leaves the browser with a
         * .pdf to fall back on. Preview and a correct filename, rather than one or the
         * other.
         *
         * Both filename forms are still sent. The plain quoted one is ASCII-only for
         * older clients; filename* (RFC 5987) carries the exact name and is what every
         * current browser prefers. A registration number like 21/U/BED(P)/1238/KA/INS is
         * why the header has to survive whatever arrives. */
        string safeName = HeaderSafeFileName(fileName, "AcademicDocument.pdf");
        bool isPdf = !string.IsNullOrEmpty(Response.ContentType)
            && Response.ContentType.IndexOf("application/pdf", StringComparison.OrdinalIgnoreCase) >= 0;
        Response.AppendHeader("Content-Disposition",
            (isPdf ? "inline" : "attachment")
            + "; filename=\"" + safeName + "\"; filename*=UTF-8''" + Uri.EscapeDataString(fileName ?? safeName));
        Response.AppendHeader("Content-Length", content != null ? content.Length.ToString() : "0");
        Response.BinaryWrite(content);
        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    /// <summary>
    /// A refusal that tells the operator what to do next rather than only what went wrong.
    ///
    /// Three ways forward, in the order they are usually wanted: approve the named students in
    /// the Graduation Centre, carry on with the ones who are approved, or go back. The middle
    /// one matters most in practice: a batch of forty with two unapproved should not send
    /// anybody back to re-tick thirty-eight boxes, so the link carries the approved list.
    /// </summary>
    private void WriteGraduationBlockAndComplete(GraduationPrintGate.Outcome gate, string documentType)
    {
        Response.Clear();
        Response.Buffer = true;
        Response.ContentType = "text/html";
        Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
        Response.Cache.SetNoStore();

        bool some = gate.Allowed.Count > 0;
        string kind = GraduationPrintGate.KindOf(documentType) == GraduationPrintGate.CERTIFICATE
                    ? "certificate" : "transcript";

        StringBuilder rows = new StringBuilder();
        foreach (GraduationPrintGate.Verdict v in gate.Blocked)
        {
            rows.Append("<li><b>")
                .Append(HttpUtility.HtmlEncode(v.Name == "" ? v.RegNo : v.Name))
                .Append("</b> <span class='r'>").Append(HttpUtility.HtmlEncode(v.RegNo)).Append("</span><br>")
                .Append("<span class='w'>").Append(HttpUtility.HtmlEncode(v.Reason)).Append("</span></li>");
        }

        string carryOn = "";
        if (some)
        {
            string url = ResolveUrl("~/COOPERP/NewScreens/NewStudentInfo.aspx?action=GenerateAcademicDocument")
                       + "&documentType=" + Server.UrlEncode(documentType)
                       + "&regnos=" + Server.UrlEncode(string.Join(",", gate.Allowed.ToArray()));
            carryOn = "<a class='btn btn--p' href='" + HttpUtility.HtmlEncode(url) + "'>Carry on with the other "
                    + gate.Allowed.Count + "</a>";
        }

        string centre = ResolveUrl("~/COOPERP/NewScreens/GraduationCandidates.aspx");

        Response.Write(
            "<!DOCTYPE html><html><head><title>Not approved for graduation</title><style>" +
            "body{font-family:Segoe UI,Arial,sans-serif;background:#f5f7fa;padding:24px;color:#1a1a2e;margin:0}" +
            ".card{max-width:680px;margin:40px auto;background:#fff;border:1px solid #e0e5ed;border-radius:4px;padding:22px 24px}" +
            "h2{margin:0 0 6px;font-size:18px;color:#05275C}" +
            ".lead{font-size:13px;line-height:1.6;color:#475467;margin:0 0 14px}" +
            "ul{list-style:none;padding:0;margin:0 0 16px}" +
            "li{border:1px solid #f3c9c9;background:#fef5f5;padding:9px 11px;margin-bottom:7px;font-size:12px;line-height:1.55}" +
            ".r{color:#8a94a6;font-size:11px}.w{color:#7f1d1d}" +
            ".btns{display:flex;gap:8px;flex-wrap:wrap;margin-top:4px}" +
            ".btn{display:inline-block;padding:7px 13px;border:1px solid #e0e5ed;background:#fff;color:#05275C;" +
            "text-decoration:none;font-size:12px}.btn--p{background:#174DA4;border-color:#174DA4;color:#fff}" +
            "</style></head><body><div class='card'>" +
            "<h2>" + (gate.Blocked.Count == 1 ? "This student is not on the graduation list"
                                              : gate.Blocked.Count + " of these students are not on the graduation list") + "</h2>" +
            "<p class='lead'>From 2026/2027 a " + kind + " is only issued to a student the Academic " +
            "Registry has approved for graduation. Students who are still studying are not affected, " +
            "and neither is anything from an earlier year.</p>" +
            "<ul>" + rows + "</ul><div class='btns'>" + carryOn +
            "<a class='btn' href='" + HttpUtility.HtmlEncode(centre) + "'>Open the Graduation Centre</a>" +
            "<a class='btn' href='javascript:history.back()'>Go back</a>" +
            "</div></div></body></html>");
        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    /// <summary>Who is printing, for the document record. Same two signals the auth check uses.</summary>
    private string ResolveDocumentActor()
    {
        try
        {
            if (Session != null)
            {
                string s = Convert.ToString(Session["username"] ?? Session["usernm"] ?? "").Trim();
                if (s != "") return s;
            }
        }
        catch { }
        try
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
                return (User.Identity.Name ?? "").Trim();
        }
        catch { }
        return "unknown";
    }

    private void WriteHtmlErrorAndComplete(string message)
    {
        Response.Clear();
        Response.Buffer = true;
        Response.ContentType = "text/html";
        Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
        Response.Cache.SetNoStore();

        string safeMessage = HttpUtility.HtmlEncode(message ?? "An error occurred.");
        Response.Write("<!DOCTYPE html><html><head><title>Academic Documents</title><style>body{font-family:Segoe UI,Arial,sans-serif;background:#f8fafc;padding:24px;color:#1f2937}.card{max-width:640px;margin:40px auto;background:#fff;border:1px solid #e5e7eb;border-radius:10px;padding:24px;box-shadow:0 10px 30px rgba(0,0,0,.06)}h2{margin:0 0 10px;font-size:20px;color:#174DA4}p{margin:0;font-size:14px;line-height:1.6}</style></head><body><div class='card'><h2>Academic Document Generation</h2><p>" + safeMessage + "</p></div></body></html>");
        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    private void WriteAcademicDocumentHelpAndComplete()
    {
        Response.Clear();
        Response.Buffer = true;
        Response.ContentType = "text/html";
        Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
        Response.Cache.SetNoStore();

        string endpoint = ResolveUrl("~/COOPERP/NewScreens/NewStudentInfo.aspx?action=GenerateAcademicDocument");
        string transcriptExample = endpoint + "&documentType=Transcript&regno=STUDENT_REGNO";
        string certificateExample = endpoint + "&documentType=Certificate&regno=STUDENT_REGNO";
        string batchExample = endpoint + "&documentType=Transcript&regnos=REG001,REG002,REG003";

        Response.Write(
            "<!DOCTYPE html><html><head><title>Academic Documents</title>" +
            "<style>body{font-family:Segoe UI,Arial,sans-serif;background:#f8fafc;padding:24px;color:#1f2937}.card{max-width:760px;margin:40px auto;background:#fff;border:1px solid #e5e7eb;border-radius:10px;padding:24px;box-shadow:0 10px 30px rgba(0,0,0,.06)}h2{margin:0 0 10px;font-size:20px;color:#174DA4}p,li{font-size:14px;line-height:1.7}code{display:block;background:#f8fafc;border:1px solid #e5e7eb;border-radius:6px;padding:10px 12px;margin:8px 0;color:#0f172a;word-break:break-all}ul{padding-left:20px}</style></head><body>" +
            "<div class='card'>" +
            "<h2>Academic Document Generation</h2>" +
            "<p>This endpoint generates the same transcript and certificate PDFs used by the classic system.</p>" +
            "<ul>" +
            "<li>Supported document types: <strong>Transcript</strong>, <strong>Certificate</strong></li>" +
            "<li>Single student: pass <strong>regno</strong></li>" +
            "<li>Batch generation: pass <strong>regnos</strong> as a comma-separated list</li>" +
            "</ul>" +
            "<p><strong>Examples</strong></p>" +
            "<code>" + HttpUtility.HtmlEncode(transcriptExample) + "</code>" +
            "<code>" + HttpUtility.HtmlEncode(certificateExample) + "</code>" +
            "<code>" + HttpUtility.HtmlEncode(batchExample) + "</code>" +
            "<p>From the New Student Info screen, the recommended path remains the row action or batch menu because it fills these parameters automatically.</p>" +
            "</div></body></html>");
        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }
    
    /// <summary>
    /// Hashes a password matching the ASP.NET MySQLMembershipProvider with passwordFormat="Hashed".
    /// In .NET 4.0+ with no explicit machineKey, Membership.HashAlgorithmType defaults to "HMACSHA256".
    /// HMACSHA256 is a KeyedHashAlgorithm, so the salt is used as the HMAC key.
    /// Algorithm: HMACSHA256(key=salt_bytes, data=salt_bytes + Unicode_bytes(password)) → Base64
    /// Matches HashPasswordBytes() / EncodePassword() in MySql.Web.Security.MySQLMembershipProvider.
    /// </summary>
    private string HashPasswordWithSalt(string password, string base64Salt)
    {
        byte[] saltBytes = Convert.FromBase64String(base64Salt);
        byte[] passwordBytes = Encoding.Unicode.GetBytes(password);
        
        // Combine salt + password bytes (salt first, then password)
        byte[] combined = new byte[saltBytes.Length + passwordBytes.Length];
        System.Buffer.BlockCopy(saltBytes, 0, combined, 0, saltBytes.Length);
        System.Buffer.BlockCopy(passwordBytes, 0, combined, saltBytes.Length, passwordBytes.Length);
        
        // Hash with HMACSHA256 keyed by the salt (matches .NET 4.0+ default Membership hash)
        using (HMACSHA256 hmac = new HMACSHA256(saltBytes))
        {
            byte[] hashBytes = hmac.ComputeHash(combined);
            return Convert.ToBase64String(hashBytes);
        }
    }
    
    #endregion
}
