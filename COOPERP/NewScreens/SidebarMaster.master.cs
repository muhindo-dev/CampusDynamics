using System;
using System.Text;
using System.Web.UI;
using System.Data;
using MySql.Data.MySqlClient;
using System.Configuration;

public partial class COOPERP_NewScreens_SidebarMaster : System.Web.UI.MasterPage
{
    private string ConnectionString = ConfigurationManager.ConnectionStrings["vacConnectionString"] != null
        ? ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString
        : "Server=localhost;Database=campus_dynamics;Uid=root;Pwd=24thdecember1977;";

    protected void Page_Load(object sender, EventArgs e)
    {
        if (Session["username"] == null)
        {
            Response.Redirect("~/Default.aspx");
            return;
        }

        // Set footer text
        lbl_footer.Text = "© " + DateTime.Now.Year + " Mutesa I Royal University - Powered by Campus Dynamics";
        
        // Set brand link
        linkBrand.HRef = ResolveUrl("~/COOPERP/NewScreens/NewDashboard.aspx");
        
        // Set dynamic page title based on current page
        SetPageTitle();
        
        // Load dropdowns
        if (!IsPostBack)
        {
            LoadAcademicYears();
            LoadSemesters();
            LoadCampuses();
        }

        // RBAC: inject the slug data the sidebar JS uses to hide menu items the
        // user has no access to. ON by default; fail-safe (admins and users with
        // no role see the full menu). Disable with appSetting RbacSlugMenu="false".
        RegisterRbacMenuScript();
    }

    private void RegisterRbacMenuScript()
    {
        try
        {
            // Menu access filtering is ON by default. Disable only with the
            // appSetting RbacSlugMenu = "false" (or "off"). Even when on it is
            // fail-safe: admins and users with no role always see the full menu.
            string mf = (ConfigurationManager.AppSettings["RbacSlugMenu"] ?? "").Trim().ToLower();
            bool menuFilter = mf != "false" && mf != "off";

            // Ensure the user's access set is loaded AND fresh. We reload when it is
            // null (never loaded) OR empty (loaded as "no access") so that roles
            // granted/changed after the user logged in take effect on the next page
            // load — no re-login needed. A genuinely role-less user simply reloads to
            // empty again. Admins ("*") and users with slugs are left as-is.
            object slugObj = Session[RoleAccessService.SESSION_SLUGS];
            string username = Session["username"] != null ? Session["username"].ToString() : "";
            if (!string.IsNullOrEmpty(username))
            {
                if (slugObj == null || string.IsNullOrEmpty(slugObj as string))
                    RoleAccessService.LoadUserAccess(username);     // never loaded / no access → load now
                else
                    RoleAccessService.MaybeRefresh(username);       // already loaded → refresh if stale, so role/permission changes propagate without re-login
                slugObj = Session[RoleAccessService.SESSION_SLUGS];
            }

            // Allowed slugs for the current user:
            //   "'*'"  → admin (full access)
            //   "[..]" → an array of granted slugs ([] = loaded, NO role → menu hidden)
            //   "null" → genuinely not loaded (not logged in) → fail-safe show all
            string raw = slugObj as string ?? "";
            string allowedJs;
            if (raw.Contains(RoleAccessService.ADMIN_WILDCARD))
            {
                allowedJs = "'*'";
            }
            else if (slugObj == null)
            {
                allowedJs = "null";
            }
            else
            {
                var parts = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                var arr = new StringBuilder("[");
                for (int i = 0; i < parts.Length; i++)
                {
                    if (i > 0) arr.Append(",");
                    arr.Append("'").Append(parts[i].Trim().Replace("'", "")).Append("'");
                }
                arr.Append("]");
                allowedJs = arr.ToString();   // [] when the user has no role
            }

            // For a user whose page access is actually enforced, the menu is filtered against
            // the very set the gate uses, not against the slug map. Two reasons. The sets
            // cannot drift into disagreeing about the same page. And a link the slug map
            // cannot resolve is hidden rather than shown: for a gated user an unmapped link is
            // guaranteed to answer 403, so offering it is worse than omitting it.
            string gatedJs = "false", pagesJs = "null";
            try
            {
                if (ReadOnlyGate.IsGated(Context))
                {
                    gatedJs = "true";
                    var sb2 = new StringBuilder("[");
                    bool f2 = true;
                    foreach (string pg in ReadOnlyGate.AllowedPageSet(Context))
                    {
                        if (!f2) sb2.Append(",");
                        f2 = false;
                        sb2.Append("'").Append(pg.Replace("'", "").ToLowerInvariant()).Append("'");
                    }
                    pagesJs = sb2.Append("]").ToString();
                }
            }
            catch { gatedJs = "false"; pagesJs = "null"; }

            string script = "window.cdMenuFilter=" + (menuFilter ? "true" : "false") +
                            ";window.cdAllowedSlugs=" + allowedJs +
                            ";window.cdPageGated=" + gatedJs +
                            ";window.cdAllowedPages=" + pagesJs +
                            ";window.cdUrlSlugMap=" + GetUrlSlugMapJs() + ";";

            Page.ClientScript.RegisterStartupScript(GetType(), "rbacMenuData", script, true);
        }
        catch
        {
            // Never let RBAC wiring break the master page — fail open (show all).
            Page.ClientScript.RegisterStartupScript(GetType(), "rbacMenuData",
                "window.cdMenuFilter=false;window.cdAllowedSlugs=null;window.cdUrlSlugMap={};", true);
        }
    }

    // Builds the url-filename -> slug map the sidebar filter needs. Cached for
    // 10 minutes (shared across all users) so it is one query, not per-request.
    private string GetUrlSlugMapJs()
    {
        var cache = System.Web.HttpRuntime.Cache;
        var cached = cache["rbac_menu_map_js"] as string;
        if (cached != null) return cached;

        var map = new StringBuilder("{");
        bool first = true;
        using (var conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            using (var cmd = new MySqlCommand(
                "SELECT url, menu_slug FROM sys_menu_items WHERE is_active=1 AND IFNULL(url,'')<>''", conn))
            using (var dr = cmd.ExecuteReader())
            {
                while (dr.Read())
                {
                    string url  = dr["url"].ToString();
                    string slug = dr["menu_slug"].ToString();
                    string file = url.Split('?')[0].TrimEnd('/');
                    int slash = file.LastIndexOf('/');
                    if (slash >= 0) file = file.Substring(slash + 1);
                    file = file.ToLowerInvariant();
                    if (string.IsNullOrEmpty(file)) continue;
                    if (!first) map.Append(",");
                    first = false;
                    map.Append("'").Append(file.Replace("'", "")).Append("':'")
                       .Append(slug.Replace("'", "")).Append("'");
                }
            }
        }
        map.Append("}");
        string js = map.ToString();
        cache.Insert("rbac_menu_map_js", js, null,
            DateTime.UtcNow.AddMinutes(10), System.Web.Caching.Cache.NoSlidingExpiration);
        return js;
    }
    
    private void SetPageTitle()
    {
        string pageName = System.IO.Path.GetFileNameWithoutExtension(Request.Url.AbsolutePath);
        string title = "Dashboard";
        
        switch (pageName.ToLower())
        {
            case "newdashboard":
                title = "Dashboard";
                break;
            case "programmesdashboard":
                title = "Programmes Dashboard";
                break;
            case "newfaculties":
                title = "Faculties";
                break;
            case "newfacultyprogrammes":
                title = "Programmes";
                break;
            case "newspecialisations":
                title = "Specialisations";
                break;
            case "newprogrammecourses":
                title = "Programme Courses";
                break;
            case "programmelecturers":
                title = "Lecturers";
                break;
            case "programmelecturerloads":
                title = "Lecturer Loads";
                break;
            case "programmeloadrequests":
                title = "Load Requests";
                break;
            case "newcourses":
                title = "Course Bank";
                break;
            case "newstudentinfo":
                title = "Student Records";
                break;
            // Student Lists
            case "activestudents":
                title = "Active Students";
                break;
            case "admittedstudents":
                title = "Admitted Students";
                break;
            case "allstudents":
                title = "All Students";
                break;
            case "alumnistudents":
                title = "Alumni";
                break;
            // Student Registration
            case "newstudentregistration":
                title = "Register New Student";
                break;
            case "studentsregistration":
                title = "Semester Registration";
                break;
            case "studentsspecialisation":
                title = "Specialisations";
                break;
            case "studentspromotion":
                title = "Year Promotions";
                break;
            case "enrollmentanalysis":
                title = "Enrolment Analysis";
                break;
            // Student Services
            case "studentsidcards":
                title = "ID Cards";
                break;
            case "idcardstatus":
                title = "ID Card Status";
                break;
            case "studentdocuments":
                title = "Student Documents";
                break;
            case "residenceallocation":
                title = "Residence Allocation";
                break;
            case "portalonboarding":
                title = "Portal Onboarding";
                break;
            case "ncheexporter":
                title = "NCHE Data Export";
                break;
            case "nchestudentexporter":
                title = "NCHE Student Exporter";
                break;
            // Course Registration & Exam Administration
            case "courseregistration":
                title = "Course Registration";
                break;
            case "examresultsinfo":
                title = "Exam Results Info";
                break;
            case "examapproval":
                title = "Exam Approval & Printing";
                break;
            case "generalmarksheets":
                title = "General Marksheets";
                break;
            case "researchmarksheets":
                title = "Research Marksheets";
                break;
            // Results Processing
            case "academicresults":
                title = "Academic Results";
                break;
            case "resultsrelease":
                title = "Results Release";
                break;
            case "resultsupdates":
                title = "Change Marks Request";
                break;
            case "resultsholdlist":
                title = "Hold List";
                break;
            case "resultsauditlog":
                title = "Results Audit Log";
                break;
            case "marksaudittrail":
                title = "Marks Audit Trail";
                break;
            // Performance & Analytics
            case "resultsanalytics":
                title = "Analytics Dashboard";
                break;
            case "studentresultsview":
                title = "Student Results View";
                break;
            case "graduationcentre":
                title = "Graduation Centre";
                break;
            case "graduationanalysis":
                title = "Graduation Analysis";
                break;
            case "documentcentre":
                title = "Document Centre";
                break;
            // Fees Module
            case "feesmanagement":
                title = "Fees Dashboard";
                break;
            case "feesstructure":
                title = "Fee Structure & Settings";
                break;
            case "feesregistration":
                title = "Fee Registration";
                break;
            case "feesaudittrail":
                title = "Fees Audit Trail";
                break;
            case "otherfeesbilling":
                title = "Other Fees Billing";
                break;
            case "billwaivers":
                title = "Bill Waivers";
                break;
            case "feestransactions":
                title = "Transactions";
                break;
            case "studentledgers":
                title = "Student Ledgers";
                break;
            case "bursarydashboard":
                title = "Bursary Dashboard";
                break;
            case "bursaryschemes":
                title = "Bursary Schemes";
                break;
            case "bursarybeneficiaries":
                title = "Bursary Beneficiaries";
                break;
            case "systemvalidationstats":
                title = "System Validation Stats";
                break;
            // Finance Module
            case "financedashboard":
                title = "Finance Dashboard";
                break;
            case "chartofaccounts":
                title = "Chart of Accounts";
                break;
            case "mainaccountscontroller":
                title = "Main Accounts Controller";
                break;
            case "subaccountscontroller":
                title = "Sub Accounts Controller";
                break;
            case "generalledger":
                title = "General Ledger";
                break;
            case "journalentries":
                title = "Journal Entries";
                break;
            case "paymentvouchers":
                title = "Payment Vouchers";
                break;
            case "studentreceipts":
                title = "Student Receipts";
                break;
            case "contravouchers":
                title = "Contra Vouchers";
                break;
            case "trialbalance":
                title = "Trial Balance";
                break;
            case "incomestatement":
                title = "Income Statement";
                break;
            case "balancesheet":
                title = "Balance Sheet";
                break;
            case "financialperiods":
                title = "Financial Periods";
                break;
            case "ledgercategories":
                title = "Ledger Categories";
                break;
            case "financeaudittrail":
                title = "Finance Audit Trail";
                break;
            case "suppliermanagement":
                title = "Supplier Management";
                break;
            // HR & Payroll Module
            case "hrdashboard":
                title = "Human Resources";
                break;
            case "hremployees":
                title = "Employee Directory";
                break;
            case "hrcontracts":
                title = "Contracts";
                break;
            case "contractrenewals":
            case "contractrenewalview":
                title = "Contract Renewals";
                break;
            case "hrpayroll":
                title = "Payroll Runs";
                break;
            case "hrpayslips":
                title = "Payslips";
                break;
            case "hrallowances":
                title = "Allowance Records";
                break;
            case "hrdeductions":
                title = "Deduction Records";
                break;
            case "hrleavemanagement":
                title = "Leave Balances";
                break;
            case "hrsettings":
                title = "HR Settings";
                break;
            case "hrconfig":
                title = "Payroll and Tax Settings";
                break;
            case "leaveapplications":
            case "leaveapplicationform":
                title = "Leave Applications";
                break;
            case "profilechangerequests":
                title = "Profile Requests";
                break;
            case "assetsdashboard":
                title = "Assets Dashboard";
                break;
            case "assets":
                title = "Assets";
                break;
            case "assetrecords":
                title = "Asset Records";
                break;
            case "assetcategories":
                title = "Asset Categories";
                break;
            case "assetreports":
                title = "Asset Reports";
                break;
            case "assetimport":
                title = "Import Assets";
                break;
            case "disciplinarydashboard":
                title = "Disciplinary Dashboard";
                break;
            case "disciplinaryrecords":
                title = "Disciplinary Records";
                break;
            case "disciplinarycase":
                title = "Disciplinary Case";
                break;
            case "disciplinaryupdates":
                title = "Record Updates";
                break;
            case "disciplinarysettings":
                title = "Case Types and Sanctions";
                break;
            case "disciplinaryreports":
                title = "Disciplinary Reports";
                break;
            case "appraisaldashboard":
                title = "Appraisal Overview";
                break;
            case "appraisalsessions":
                title = "Appraisal Sessions";
                break;
            case "appraisalview":
                title = "Appraisals";
                break;
            case "appraisalreports":
                title = "Appraisal Reports";
                break;
            case "competencytemplates":
                title = "Competencies";
                break;
            case "expectedstandards":
                title = "Expected Standards";
                break;
            // System Configuration
            case "academicyears":
                title = "Academic Years";
                break;
            // Marks Module (Batch 13)
            case "teacherdashboard":
                title = "My Marks Dashboard";
                break;
            case "markentry":
                title = "Mark Entry";
                break;
            case "assignmentmanager":
                title = "Teaching Assignments";
                break;
            case "deadlinemanager":
                title = "Deadline Manager";
                break;
            case "deanapproval":
                title = "Dean Approval";
                break;
            case "auditcentre":
                title = "Audit Centre";
                break;
            case "marksalertdashboard":
                title = "Operational Alerts";
                break;
            // Elections Module
            case "electionsdashboard":
                title = "Elections Dashboard";
                break;
            case "electionposts":
                title = "Election Posts";
                break;
            case "electioncandidates":
                title = "Election Candidates";
                break;
            case "electionvoters":
                title = "Election Voters";
                break;
            case "electionresults":
                title = "Election Results";
                break;
            // Load Allocation Module
            case "loadallocationdashboard":
                title = "Allocation Dashboard";
                break;
            case "loadallocations":
                title = "Teaching Allocations";
                break;
            case "timetableview":
                title = "Timetable View";
                break;
            case "lecturerooms":
                title = "Lecture Rooms";
                break;
            case "workloadanalysis":
                title = "Workload Analysis";
                break;
            case "communications":
                title = "Communications";
                break;
            case "communicationanalytics":
                title = "Communication Analytics";
                break;
            case "knowledgebasemanagement":
                title = "Knowledgebase Management";
                break;
            case "knowledgebase":
                title = "Knowledge Base";
                break;
            default:
                title = pageName.Replace("New", "").Replace("_", " ");
                break;
        }
        
        lblPageTitle.Text = title;
    }
    
    /// <summary>
    /// Gets the current academic year - now centralised in AcademicYearHelper.
    /// </summary>
    
    private void LoadAcademicYears()
    {
        ddlAcademicYear.Items.Clear();
        
        // Add empty option first
        ddlAcademicYear.Items.Add(new System.Web.UI.WebControls.ListItem("-", ""));
        
        string currentAcadYear = AcademicYearHelper.GetCurrentAcademicYear();
        
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                // Get active years from acad_acadyears, filter to not show future academic years
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT acadyear FROM acad_acadyears WHERE status = 'Active' AND acadyear <= @currentYear ORDER BY acadyear DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@currentYear", currentAcadYear);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string year = reader["acadyear"].ToString();
                            ddlAcademicYear.Items.Add(new System.Web.UI.WebControls.ListItem(year, year));
                        }
                    }
                }
            }
        }
        catch
        {
            // Fallback - add current and past 5 years if database fails
            int year = DateTime.Now.Year;
            int month = DateTime.Now.Month;
            int startYear = (month >= 8) ? year : year - 1;
            
            for (int i = 0; i < 6; i++)
            {
                string acadYear = (startYear - i) + "/" + (startYear - i + 1);
                ddlAcademicYear.Items.Add(new System.Web.UI.WebControls.ListItem(acadYear, acadYear));
            }
        }
        
        // Set selected academic year from session, or default to current year
        if (Session["SelectedAcademicYear"] != null && !string.IsNullOrEmpty(Session["SelectedAcademicYear"].ToString()))
        {
            string savedYear = Session["SelectedAcademicYear"].ToString();
            if (ddlAcademicYear.Items.FindByValue(savedYear) != null)
            {
                ddlAcademicYear.SelectedValue = savedYear;
            }
        }
        else
        {
            // Default to current academic year
            if (ddlAcademicYear.Items.FindByValue(currentAcadYear) != null)
            {
                ddlAcademicYear.SelectedValue = currentAcadYear;
                Session["SelectedAcademicYear"] = currentAcadYear;
            }
        }
    }
    
    private void LoadSemesters()
    {
        ddlSemester.Items.Clear();
        
        // Add empty option first
        ddlSemester.Items.Add(new System.Web.UI.WebControls.ListItem("-", ""));
        
        // Add semesters 1-3
        ddlSemester.Items.Add(new System.Web.UI.WebControls.ListItem("Sem 1", "1"));
        ddlSemester.Items.Add(new System.Web.UI.WebControls.ListItem("Sem 2", "2"));
        ddlSemester.Items.Add(new System.Web.UI.WebControls.ListItem("Sem 3", "3"));
        
        // Set selected semester from session if available
        if (Session["SelectedSemester"] != null && !string.IsNullOrEmpty(Session["SelectedSemester"].ToString()))
        {
            string savedSem = Session["SelectedSemester"].ToString();
            if (ddlSemester.Items.FindByValue(savedSem) != null)
            {
                ddlSemester.SelectedValue = savedSem;
            }
        }
    }
    
    protected void ddlAcademicYear_SelectedIndexChanged(object sender, EventArgs e)
    {
        // Save selected academic year to session
        Session["SelectedAcademicYear"] = ddlAcademicYear.SelectedValue;
    }
    
    protected void ddlSemester_SelectedIndexChanged(object sender, EventArgs e)
    {
        // Save selected semester to session
        Session["SelectedSemester"] = ddlSemester.SelectedValue;
    }
    
    private void LoadCampuses()
    {
        ddlCampus.Items.Clear();
        
        // Add "No Campus" option first (default - no campus selected)
        ddlCampus.Items.Add(new System.Web.UI.WebControls.ListItem("- No Campus -", ""));
        
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                // Get all campuses excluding ID=0 (usually system/placeholder records)
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT ID, campus_name, campus_short_name FROM acad_campuses WHERE ID != 0 ORDER BY campus_name", conn))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string id = reader["ID"].ToString();
                            string name = reader["campus_name"].ToString();
                            string shortName = reader["campus_short_name"] != DBNull.Value 
                                ? reader["campus_short_name"].ToString() 
                                : "";
                            
                            // Display short name if available, otherwise full name
                            string displayText = !string.IsNullOrEmpty(shortName) ? shortName : name;
                            
                            ddlCampus.Items.Add(new System.Web.UI.WebControls.ListItem(displayText, id));
                        }
                    }
                }
            }
        }
        catch
        {
            // If database fails, just keep the "No Campus" option
        }
        
        // Set selected campus from session if available
        if (Session["SelectedCampus"] != null && !string.IsNullOrEmpty(Session["SelectedCampus"].ToString()))
        {
            string savedCampus = Session["SelectedCampus"].ToString();
            if (ddlCampus.Items.FindByValue(savedCampus) != null)
            {
                ddlCampus.SelectedValue = savedCampus;
            }
        }
        // Otherwise, leave default "No Campus" selected (which has empty value)
    }
    
    protected void ddlCampus_SelectedIndexChanged(object sender, EventArgs e)
    {
        // Save selected campus to session
        Session["SelectedCampus"] = ddlCampus.SelectedValue;
        
        // Also set campusno session variable for compatibility with existing system
        if (!string.IsNullOrEmpty(ddlCampus.SelectedValue))
        {
            Session["campusno"] = ddlCampus.SelectedValue;
        }
        else
        {
            Session["campusno"] = null;
        }
    }
}
