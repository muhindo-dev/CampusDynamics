using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using MySql.Data.MySqlClient;

public partial class COOPERP_NewScreens_NewStudentRegistration : System.Web.UI.Page
{
    // Resolved at Page_Load; exposed to ASPX via GetDocEntryNo()
    private string _docEntryNo = "";
    // -- Connection strings ------------------------------------------
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

    // -- Programme cache (for JSON output to JS) ---------------------
    private List<ProgrammeItem> _programmeList;

    private class ProgrammeItem
    {
        public string Code;
        public string Name;
        public string FacultyCode;
    }

    // -- Specialisation cache (for JSON output to JS) ---------------
    private List<SpecialisationItem> _specList;

    private class SpecialisationItem
    {
        public int Id;
        public string ProgCode;
        public string Name;
    }

    // -- Edit mode flags ------------------------------------------
    // IsEditMode       = editing an existing STUDENT record by regno (?edit=REGNO)
    // IsApplicantMode  = editing an APPLICATION record by entry no (?eno=ENO)
    private bool IsEditMode
    {
        get { return !string.IsNullOrEmpty(hfEditRegNo.Value); }
    }
    private bool IsApplicantMode
    {
        get { return !string.IsNullOrEmpty(hfEditEno.Value); }
    }

    // ===============================================================
    //  PAGE LOAD
    // ===============================================================
    protected void Page_Load(object sender, EventArgs e)
    {
        // AJAX: document management — handlers write response then return;
        // Response.End() is called here (outside any try-catch) so
        // ThreadAbortException propagates cleanly to ASP.NET.
        string _ajaxAct = Request.QueryString["ajax"] ?? "";
        if (_ajaxAct == "docs_list")        { HandleDocsListAjax();        Response.End(); }
        if (_ajaxAct == "upload_doc")       { HandleUploadDocAjax();       Response.End(); }
        if (_ajaxAct == "del_doc")          { HandleDeleteDocAjax();       Response.End(); }
        if (_ajaxAct == "view_doc")         { HandleViewDocAjax();         Response.End(); }
        if (_ajaxAct == "consolidate_docs") { HandleConsolidateDocsAjax(); Response.End(); }

        // AJAX: delete student
        if (_ajaxAct == "delete")
        {
            HandleDeleteStudent();
            return;
        }

        // ALWAYS reload dropdown items - ViewState is disabled on master page,
        // so dropdown items are lost on every postback. ASP.NET's second
        // ProcessPostData pass will restore user selections from posted form data
        // once the items are available.
        LoadAllDropdownItems();

        if (!IsPostBack)
        {
            SetDefaults();

            // Capture the return URL if provided via querystring
            string returnUrl = Request.QueryString["returnUrl"];
            if (!string.IsNullOrEmpty(returnUrl))
                hfReturnUrl.Value = returnUrl;

            // -- EDIT MODE: populate form with existing student data --
            string editRegNo = Request.QueryString["edit"];
            if (!string.IsNullOrEmpty(editRegNo))
            {
                hfEditRegNo.Value = editRegNo.Trim();
                LoadStudentForEdit(editRegNo.Trim());
            }

            // -- APPLICANT EDIT MODE: populate from application record --
            string editEno = Request.QueryString["eno"];
            if (!string.IsNullOrEmpty(editEno) && string.IsNullOrEmpty(editRegNo))
            {
                hfEditEno.Value = editEno.Trim();
                LoadApplicantForEdit(editEno.Trim());
            }
        }

        // Documents panel: resolve entry number and show panel in edit modes only
        _docEntryNo = ComputeDocEntryNo();
        pnlDocuments.Visible = !string.IsNullOrEmpty(_docEntryNo);
    }

    // ===============================================================
    //  DROPDOWN LOADING
    // ===============================================================
    private void LoadAllDropdownItems()
    {
        LoadFaculties();
        LoadProgrammeList();
        PopulateProgrammeDropdown("");   // ALL programmes so posted value matches
        LoadSpecialisationList();
        PopulateSpecialisationDropdown(""); // ALL specs so posted value matches
        LoadCampuses();
        LoadSessions();
        LoadEntryYears();
        LoadBillingSystems();
        LoadNationalities();
    }

    // -- Faculties ---------------------------------------------------
    private void LoadFaculties()
    {
        ddlFaculty.Items.Clear();
        ddlFaculty.Items.Add(new ListItem("-- All Faculties --", "ALL"));
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT faculty_code, faculty_name FROM acad_faculty WHERE faculty_code != '00' ORDER BY faculty_name", conn))
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        ddlFaculty.Items.Add(new ListItem(
                            rdr["faculty_name"].ToString(),
                            rdr["faculty_code"].ToString()));
            }
        }
        catch { }
    }

    // -- Programme master list (cached for JSON + server dropdown) ---
    private void LoadProgrammeList()
    {
        if (_programmeList != null) return;
        _programmeList = new List<ProgrammeItem>();
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT progcode, progname, IFNULL(faculty_code,'00') AS fc FROM acad_programme WHERE progcode != '-' ORDER BY progname", conn))
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                    {
                        var item = new ProgrammeItem();
                        item.Code = rdr["progcode"].ToString();
                        item.Name = rdr["progname"].ToString();
                        item.FacultyCode = rdr["fc"].ToString();
                        _programmeList.Add(item);
                    }
            }
        }
        catch { }
    }

    private void PopulateProgrammeDropdown(string facultyCode)
    {
        ddlProgramme.Items.Clear();
        ddlProgramme.Items.Add(new ListItem("-- Select Programme --", ""));
        if (_programmeList == null) LoadProgrammeList();
        foreach (var p in _programmeList)
        {
            if (!string.IsNullOrEmpty(facultyCode) && facultyCode != "ALL" && p.FacultyCode != facultyCode)
                continue;
            ddlProgramme.Items.Add(new ListItem(
                p.Code + " - " + p.Name, p.Code));
        }
    }

    // -- Specialisations (Programme → Specialisation cascade) --------
    private void LoadSpecialisationList()
    {
        if (_specList != null) return;
        _specList = new List<SpecialisationItem>();
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT spec_id, prog_id, spec FROM acad_specialisation WHERE spec != '-' AND is_active='Active' ORDER BY spec", conn))
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                    {
                        var item = new SpecialisationItem();
                        item.Id = Convert.ToInt32(rdr["spec_id"]);
                        item.ProgCode = rdr["prog_id"].ToString();
                        item.Name = rdr["spec"].ToString();
                        _specList.Add(item);
                    }
            }
        }
        catch { }
    }

    private void PopulateSpecialisationDropdown(string progCode)
    {
        ddlSpecialisation.Items.Clear();
        ddlSpecialisation.Items.Add(new ListItem("-- None / Not Applicable --", ""));
        if (_specList == null) LoadSpecialisationList();
        foreach (var s in _specList)
        {
            if (!string.IsNullOrEmpty(progCode) && s.ProgCode != progCode)
                continue;
            ddlSpecialisation.Items.Add(new ListItem(
                s.Name, s.Id.ToString()));
        }
    }

    // -- Campuses ----------------------------------------------------
    private void LoadCampuses()
    {
        ddlCampus.Items.Clear();
        ddlCampus.Items.Add(new ListItem("-- Select Campus --", ""));
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT campus_code, campus_name FROM acad_campuses WHERE campus_code != '00' ORDER BY campus_name", conn))
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        ddlCampus.Items.Add(new ListItem(
                            rdr["campus_name"].ToString(),
                            rdr["campus_code"].ToString()));
            }
        }
        catch { }
    }

    // -- Study Sessions ----------------------------------------------
    private void LoadSessions()
    {
        ddlSession.Items.Clear();
        ddlSession.Items.Add(new ListItem("-- Select Session --", ""));
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT Session FROM acad_studysessions ORDER BY Session", conn))
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        ddlSession.Items.Add(new ListItem(
                            rdr["Session"].ToString(),
                            rdr["Session"].ToString()));
            }
        }
        catch { }
    }

    // -- Entry Years -------------------------------------------------
    private void LoadEntryYears()
    {
        ddlEntryYear.Items.Clear();
        ddlEntryYear.Items.Add(new ListItem("-- Select Year --", ""));
        // Newest offered year is the running academic year (or a later one only while
        // admission is open for it) — never next calendar year by default.
        int currentYear = AcademicYearHelper.CurrentAcademicStartYear();
        for (int y = AcademicYearHelper.MaxAdmissionEntryYear(); y >= currentYear - 10; y--)
            ddlEntryYear.Items.Add(new ListItem(y.ToString(), y.ToString()));
    }

    // -- Billing Systems ---------------------------------------------
    private void LoadBillingSystems()
    {
        ddlBilling.Items.Clear();
        ddlBilling.Items.Add(new ListItem("-- Select Billing --", ""));
        try
        {
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT ID, bs_name FROM fin_billing_systems ORDER BY ID", conn))
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        ddlBilling.Items.Add(new ListItem(
                            rdr["bs_name"].ToString(),
                            rdr["ID"].ToString()));
            }
        }
        catch { }
    }

    // -- Nationalities -----------------------------------------------
    private void LoadNationalities()
    {
        ddlNationality.Items.Clear();
        ddlNationality.Items.Add(new ListItem("UGANDAN", "UGANDAN"));
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT nationality_name FROM nationalities WHERE nationality_name != 'UGANDAN' ORDER BY nationality_name", conn))
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        ddlNationality.Items.Add(new ListItem(
                            rdr["nationality_name"].ToString(),
                            rdr["nationality_name"].ToString()));
            }
        }
        catch
        {
            // Fallback - add common nationalities
            string[] common = { "KENYAN", "TANZANIAN", "RWANDAN", "CONGOLESE", "SOUTH SUDANESE", "BURUNDIAN", "SOMALI", "ETHIOPIAN", "NIGERIAN" };
            foreach (string n in common)
                ddlNationality.Items.Add(new ListItem(n, n));
        }
    }

    // -- Set defaults ------------------------------------------------
    private void SetDefaults()
    {
        // Entry year → start of the running academic year (2026 for 2026/2027, including a
        // January intake registered in January 2027 — not the calendar year).
        int currentYear = AcademicYearHelper.CurrentAcademicStartYear();

        if (ddlEntryYear.Items.FindByValue(currentYear.ToString()) != null)
            ddlEntryYear.SelectedValue = currentYear.ToString();

        // Study session → DAY
        if (ddlSession.Items.FindByValue("DAY") != null)
            ddlSession.SelectedValue = "DAY";

        // Intake → AUGUST
        if (ddlIntake.Items.FindByValue("AUGUST") != null)
            ddlIntake.SelectedValue = "AUGUST";

        // Billing → first real item
        if (ddlBilling.Items.Count > 1)
            ddlBilling.SelectedIndex = 1;

        // DOB → 20 years ago from today
        txtDOB.Text = DateTime.Now.AddYears(-20).ToString("yyyy-MM-dd");

        // Nationality → UGANDAN (already first item, but ensure selection)
        if (ddlNationality.Items.FindByValue("UGANDAN") != null)
            ddlNationality.SelectedValue = "UGANDAN";

        // Campus → first real item if only one campus
        if (ddlCampus.Items.Count == 2)
            ddlCampus.SelectedIndex = 1;

        // Entry method → DIRECT
        if (ddlEntryMethod.Items.FindByValue("DIRECT") != null)
            ddlEntryMethod.SelectedValue = "DIRECT";

        // Study year → Year 1
        if (ddlStudyYear.Items.FindByValue("1") != null)
            ddlStudyYear.SelectedValue = "1";

        // District → UGANDA
        txtDistrict.Text = "UGANDA";
        txtResCountry.Text = "UGANDA";
    }

    // ===============================================================
    //  JSON OUTPUT - Programme data for cascading JS
    // ===============================================================
    public string GetProgrammesJson()
    {
        if (_programmeList == null) LoadProgrammeList();
        var sb = new StringBuilder("[");
        for (int i = 0; i < _programmeList.Count; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append("{\"c\":\"");
            sb.Append(EscapeJs(_programmeList[i].Code));
            sb.Append("\",\"n\":\"");
            sb.Append(EscapeJs(_programmeList[i].Name));
            sb.Append("\",\"f\":\"");
            sb.Append(EscapeJs(_programmeList[i].FacultyCode));
            sb.Append("\"}");
        }
        sb.Append("]");
        return sb.ToString();
    }

    // ===============================================================
    //  JSON OUTPUT - Specialisation data for cascading JS
    // ===============================================================
    public string GetSpecialisationsJson()
    {
        if (_specList == null) LoadSpecialisationList();
        var sb = new StringBuilder("[");
        for (int i = 0; i < _specList.Count; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append("{\"id\":");
            sb.Append(_specList[i].Id);
            sb.Append(",\"p\":\"");
            sb.Append(EscapeJs(_specList[i].ProgCode));
            sb.Append("\",\"n\":\"");
            sb.Append(EscapeJs(_specList[i].Name));
            sb.Append("\"}");
        }
        sb.Append("]");
        return sb.ToString();
    }

    private static string EscapeJs(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("'", "\\'").Replace("\n", "").Replace("\r", "");
    }

    // ===============================================================
    //  SPLIT FULL NAME INTO SURNAME / FIRST NAME / OTHER NAMES
    //  Stored format: "SURNAME FIRSTNAME OTHERNAME…" (uppercase, space-separated)
    //  acad_student convention: othername=surname, firstname=given names
    // ===============================================================
    private void SplitFullNameIntoFields(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return;
        var parts = fullName.Trim().ToUpper()
            .Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        txtSurname.Text   = parts.Length > 0 ? parts[0] : "";
        txtFirstName.Text = parts.Length > 1 ? parts[1] : "";
        txtOtherName.Text = parts.Length > 2
            ? string.Join(" ", parts, 2, parts.Length - 2)
            : "";
    }

    // ===============================================================
    //  AJAX: DELETE STUDENT
    // ===============================================================
    private void HandleDeleteStudent()
    {
        Response.Clear();
        Response.ContentType = "application/json";
        string regno = (Request.QueryString["regno"] ?? "").Trim();
        if (string.IsNullOrEmpty(regno))
        {
            Response.Write("{\"ok\":false,\"error\":\"Registration number required.\"}");
            Response.End(); return;
        }
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                // Safety check: block delete if the student has any financial transactions.
                // fin_studentfeestracking lives in campus_dynamics_accounts, so it MUST be
                // schema-qualified — on the campus_dynamics connection an unqualified name
                // resolves to campus_dynamics.fin_studentfeestracking, which does not exist
                // ("Table 'campus_dynamics.fin_studentfeestracking' doesn't exist").
                using (MySqlCommand chk = new MySqlCommand(
                    "SELECT COUNT(*) FROM campus_dynamics_accounts.fin_studentfeestracking WHERE TRIM(regno) = TRIM(@r)", conn))
                {
                    chk.Parameters.AddWithValue("@r", regno);
                    long txCount = Convert.ToInt64(chk.ExecuteScalar());
                    if (txCount > 0)
                        throw new Exception(string.Format(
                            "Cannot delete {0}: {1} fee transaction(s) exist. Reverse all transactions first.", regno, txCount));
                }

                MySqlTransaction tx = conn.BeginTransaction();
                try
                {
                    // Clean up related rows in the current database. Data-driven and safe:
                    // for each candidate table we look up (via information_schema) which
                    // student-key column it ACTUALLY has — regno (acad_registration) or
                    // reg_no (acad_student_cards) — and delete on that column. A table that
                    // doesn't exist, or has neither column, is simply skipped. This avoids the
                    // earlier crashes: a missing table ("acad_student_semester") and a wrong
                    // column ("Unknown column 'regno'" on acad_student_cards, which uses reg_no).
                    // acad_registration cleanup ensures a deleted student leaves no orphaned
                    // registration (the fee guard above already blocks deleting a billed student).
                    foreach (string tbl in new string[] { "acad_registration", "acad_student_cards" })
                    {
                        string keyCol = null;
                        using (MySqlCommand ck = new MySqlCommand(
                            "SELECT COLUMN_NAME FROM information_schema.COLUMNS " +
                            "WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=@t AND COLUMN_NAME IN ('regno','reg_no') " +
                            "ORDER BY FIELD(COLUMN_NAME,'regno','reg_no') LIMIT 1", conn, tx))
                        {
                            ck.Parameters.AddWithValue("@t", tbl);
                            object o = ck.ExecuteScalar();
                            if (o != null && o != DBNull.Value) keyCol = o.ToString();
                        }
                        if (keyCol == null) continue;   // table missing or no student-key column

                        using (MySqlCommand dd = new MySqlCommand("DELETE FROM " + tbl + " WHERE " + keyCol + " = @r", conn, tx))
                        { dd.Parameters.AddWithValue("@r", regno); dd.ExecuteNonQuery(); }
                    }

                    // Delete core student record (mandatory)
                    using (MySqlCommand d3 = new MySqlCommand(
                        "DELETE FROM acad_student WHERE regno = @r", conn, tx))
                    { d3.Parameters.AddWithValue("@r", regno); d3.ExecuteNonQuery(); }

                    tx.Commit();
                }
                catch { tx.Rollback(); throw; }
            }
            Response.Write("{\"ok\":true,\"message\":\"Student " + regno.Replace("\"", "") + " deleted successfully.\"}");
        }
        catch (Exception ex)
        {
            Response.Write("{\"ok\":false,\"error\":\"" + ex.Message.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}");
        }
        Response.End();
    }

    // ===============================================================
    //  LOAD EXISTING STUDENT FOR EDIT MODE
    // ===============================================================
    private void LoadStudentForEdit(string regno)
    {
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                // Detect education columns (may not exist in all schema versions)
                bool seOlYr    = ColExists(conn, "acad_applications", "olevel_year");
                bool seOlYrFb  = !seOlYr  && ColExists(conn, "acad_applications", "stud_pob");
                bool seOlAgg   = ColExists(conn, "acad_applications", "olevel_agg");
                bool seOlAggFb = !seOlAgg && ColExists(conn, "acad_applications", "stud_district");
                bool seAlYr    = ColExists(conn, "acad_applications", "alevel_year");
                bool seAlPts   = ColExists(conn, "acad_applications", "alevel_points");
                bool seAlPtsFb = !seAlPts && ColExists(conn, "acad_applications", "stud_ward");
                bool seOtInst  = ColExists(conn, "acad_applications", "other_institution");
                bool seOtInstFb= !seOtInst && ColExists(conn, "acad_applications", "stud_prevcampus");
                bool seOtQual  = ColExists(conn, "acad_applications", "other_qualification");
                bool seOtQualFb= !seOtQual && ColExists(conn, "acad_applications", "stud_lg");
                bool seOtYr    = ColExists(conn, "acad_applications", "other_year");
                bool seOtYrFb  = !seOtYr  && ColExists(conn, "acad_applications", "stud_village");
                bool seOtGr    = ColExists(conn, "acad_applications", "other_grade");
                bool seOtGrFb  = !seOtGr  && ColExists(conn, "acad_applications", "stud_county");

                string seOlYrX    = seOlYr   ? "COALESCE(a.olevel_year,'')"       : (seOlYrFb   ? "COALESCE(a.stud_pob,'')"       : "''");
                string seOlAggX   = seOlAgg  ? "COALESCE(a.olevel_agg,'')"        : (seOlAggFb  ? "COALESCE(a.stud_district,'')"  : "''");
                string seAlYrX    = seAlYr   ? "COALESCE(a.alevel_year,'')"       : "''";
                string seAlPtsX   = seAlPts  ? "COALESCE(a.alevel_points,'')"     : (seAlPtsFb  ? "COALESCE(a.stud_ward,'')"      : "''");
                string seOtInstX  = seOtInst ? "COALESCE(a.other_institution,'')" : (seOtInstFb ? "COALESCE(a.stud_prevcampus,'')" : "''");
                string seOtQualX  = seOtQual ? "COALESCE(a.other_qualification,'')" : (seOtQualFb? "COALESCE(a.stud_lg,'')"        : "''");
                string seOtYrX    = seOtYr   ? "COALESCE(a.other_year,'')"        : (seOtYrFb   ? "COALESCE(a.stud_village,'')"   : "''");
                string seOtGrX    = seOtGr   ? "COALESCE(a.other_grade,'')"       : (seOtGrFb   ? "COALESCE(a.stud_county,'')"    : "''");

                // Query student data + optional application data
                string sql = @"
                    SELECT s.*,
                           a.stud_sponsor, a.sponsor_contact, a.next_kin, a.kin_contacts,
                           a.kin_relationship, a.stud_phy_address, a.post_box,
                           a.residence_country, a.olevel_school, a.olevel_index,
                           a.alevel_school, a.alevel_index, a.stud_mar_stat,
                           a.title AS app_title, a.physicalDisability AS app_disability,
                           COALESCE(a.referee_name,'') AS referee_name,
                           COALESCE(a.referee_contacts,'') AS referee_contacts,
                           " + seOlYrX   + @" AS edu_olevel_year,
                           " + seOlAggX  + @" AS edu_olevel_agg,
                           " + seAlYrX   + @" AS edu_alevel_year,
                           " + seAlPtsX  + @" AS edu_alevel_points,
                           " + seOtInstX + @" AS edu_other_inst,
                           " + seOtQualX + @" AS edu_other_qual,
                           " + seOtYrX   + @" AS edu_other_year,
                           " + seOtGrX   + @" AS edu_other_grade
                    FROM acad_student s
                    LEFT JOIN acad_applications a ON s.entryno = a.stud_entry_no
                    WHERE s.regno = @r
                    LIMIT 1";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read())
                        {
                            ShowError("Student not found: " + Server.HtmlEncode(regno));
                            return;
                        }

                        // -- Page chrome --
                        litPageTitle.Text = "Edit Student";
                        litPageSubtitle.Text = "Editing record for <strong>" + Server.HtmlEncode(regno) + "</strong>";
                        btnSubmit.Text = "Update Student";

                        // -- Personal Info --
                        // acad_student: othername = surname, firstname = given names
                        string oname = SafeStr(rdr, "othername");  // surname
                        string fname = SafeStr(rdr, "firstname");  // given names
                        // Full name stored as: SURNAME GIVENNAMES
                        txtFullName.Text = (oname + " " + fname).Trim();
                        // Populate the three visible fields
                        txtSurname.Text   = oname;
                        var giveNParts    = fname.Trim().Split(new char[]{' '}, 2, StringSplitOptions.RemoveEmptyEntries);
                        txtFirstName.Text = giveNParts.Length > 0 ? giveNParts[0] : "";
                        txtOtherName.Text = giveNParts.Length > 1 ? giveNParts[1] : "";

                        // Gender: acad_student stores MALE/FEMALE, form uses M/F
                        string genderDb = SafeStr(rdr, "gender").ToUpper();
                        string genderVal = "M";
                        if (genderDb.StartsWith("F")) genderVal = "F";
                        else if (genderDb == "OTHER") genderVal = "OTHER";
                        TrySelect(ddlGender, genderVal);

                        // DOB
                        if (rdr["dob"] != DBNull.Value)
                        {
                            DateTime dob = Convert.ToDateTime(rdr["dob"]);
                            txtDOB.Text = dob.ToString("yyyy-MM-dd");
                        }

                        // Nationality
                        string nat = SafeStr(rdr, "nationality");
                        if (!string.IsNullOrEmpty(nat))
                            TrySelect(ddlNationality, nat);

                        // National ID
                        txtNationalId.Text = SafeStr(rdr, "national_id");

                        // Phone & Email
                        txtPhone.Text = SafeStr(rdr, "studPhone");
                        txtEmail.Text = SafeStr(rdr, "email");

                        // Religion
                        string religion = SafeStr(rdr, "religion");
                        if (!string.IsNullOrEmpty(religion))
                            TrySelect(ddlReligion, religion);

                        // Title - from applications if exists
                        string title = SafeStr(rdr, "app_title");
                        if (!string.IsNullOrEmpty(title))
                            TrySelect(ddlTitle, title);

                        // Marital - from applications
                        string marital = SafeStr(rdr, "stud_mar_stat");
                        if (!string.IsNullOrEmpty(marital))
                            TrySelect(ddlMarital, marital);

                        // Disability - from applications
                        string disability = SafeStr(rdr, "app_disability");
                        if (!string.IsNullOrEmpty(disability) && disability != "-")
                            txtDisability.Text = disability;

                        // -- Academic Details --
                        string progId = SafeStr(rdr, "progid");
                        if (!string.IsNullOrEmpty(progId))
                        {
                            TrySelect(ddlProgramme, progId);
                            hfProgramme.Value = progId;

                            // Set faculty from programme cache to trigger JS cascade
                            if (_programmeList != null)
                            {
                                foreach (var p in _programmeList)
                                {
                                    if (p.Code == progId)
                                    {
                                        TrySelect(ddlFaculty, p.FacultyCode);
                                        hfFaculty.Value = p.FacultyCode;
                                        break;
                                    }
                                }
                            }
                        }

                        // Specialisation - reverse lookup
                        // DB may store spec_id as string ("22") or plain text name
                        string specVal = SafeStr(rdr, "specialisation");
                        if (!string.IsNullOrEmpty(specVal) && specVal != "-")
                        {
                            if (_specList != null)
                            {
                                bool found = false;

                                // Try 1: match by spec_id (existing data stores IDs as strings)
                                int specIdParsed;
                                if (int.TryParse(specVal, out specIdParsed) && specIdParsed > 0)
                                {
                                    foreach (var sp in _specList)
                                    {
                                        if (sp.Id == specIdParsed)
                                        {
                                            TrySelect(ddlSpecialisation, sp.Id.ToString());
                                            hfSpecialisation.Value = sp.Id.ToString();
                                            found = true;
                                            break;
                                        }
                                    }
                                }

                                // Try 2: match by name (plain-text data)
                                if (!found)
                                {
                                    foreach (var sp in _specList)
                                    {
                                        if (sp.Name == specVal && (string.IsNullOrEmpty(progId) || sp.ProgCode == progId))
                                        {
                                            TrySelect(ddlSpecialisation, sp.Id.ToString());
                                            hfSpecialisation.Value = sp.Id.ToString();
                                            found = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        // Session
                        string session = SafeStr(rdr, "studsesion");
                        if (!string.IsNullOrEmpty(session))
                        {
                            TrySelect(ddlSession, session);
                            hfSession.Value = session;
                        }

                        // Campus - studCampus stores int (1,2) but dropdown
                        // values are zero-padded campus_code strings ("01","02")
                        int campusCode = SafeInt(rdr["studCampus"], 0);
                        if (campusCode > 0)
                        {
                            string campusStr = campusCode.ToString().PadLeft(2, '0');
                            TrySelect(ddlCampus, campusStr);
                            hfCampus.Value = campusStr;
                        }

                        // Entry year
                        int entryYear = SafeInt(rdr["entryyear"], 0);
                        if (entryYear > 0)
                        {
                            // Ensure the year exists in the dropdown
                            if (ddlEntryYear.Items.FindByValue(entryYear.ToString()) == null)
                                ddlEntryYear.Items.Insert(0, new ListItem(entryYear.ToString(), entryYear.ToString()));
                            TrySelect(ddlEntryYear, entryYear.ToString());
                            hfEntryYear.Value = entryYear.ToString();
                        }

                        // Entry method
                        string entMethod = SafeStr(rdr, "entrymethod");
                        if (!string.IsNullOrEmpty(entMethod))
                            TrySelect(ddlEntryMethod, entMethod);

                        // Intake
                        string intake = SafeStr(rdr, "intake");
                        if (!string.IsNullOrEmpty(intake))
                            TrySelect(ddlIntake, intake);

                        // Billing
                        int billingId = SafeInt(rdr["billingID"], 0);
                        if (billingId > 0)
                        {
                            TrySelect(ddlBilling, billingId.ToString());
                            hfBilling.Value = billingId.ToString();
                        }

                        // -- Address & Contact (from applications) --
                        string addr = SafeStr(rdr, "stud_phy_address");
                        if (!string.IsNullOrEmpty(addr)) txtAddress.Text = addr;

                        string pb = SafeStr(rdr, "post_box");
                        if (!string.IsNullOrEmpty(pb)) txtPostBox.Text = pb;

                        string dist = SafeStr(rdr, "home_dist");
                        if (!string.IsNullOrEmpty(dist)) txtDistrict.Text = dist;

                        string resCntry = SafeStr(rdr, "residence_country");
                        if (!string.IsNullOrEmpty(resCntry)) txtResCountry.Text = resCntry;

                        // -- Sponsor & Kin (from applications) --
                        string sponsor = SafeStr(rdr, "stud_sponsor");
                        if (!string.IsNullOrEmpty(sponsor)) txtSponsor.Text = sponsor;

                        string spContact = SafeStr(rdr, "sponsor_contact");
                        if (!string.IsNullOrEmpty(spContact)) txtSponsorContact.Text = spContact;

                        string kin = SafeStr(rdr, "next_kin");
                        if (!string.IsNullOrEmpty(kin)) txtKinName.Text = kin;

                        string kinRel = SafeStr(rdr, "kin_relationship");
                        if (!string.IsNullOrEmpty(kinRel)) TrySelect(ddlKinRelation, kinRel);

                        string kinCon = SafeStr(rdr, "kin_contacts");
                        if (!string.IsNullOrEmpty(kinCon)) txtKinContact.Text = kinCon;

                        string refNm = SafeStr(rdr, "referee_name");
                        if (!string.IsNullOrEmpty(refNm)) txtRefereeName.Text = refNm;
                        string refCt = SafeStr(rdr, "referee_contacts");
                        if (!string.IsNullOrEmpty(refCt)) txtRefereeContact.Text = refCt;

                        // -- Education Background (from applications) --
                        string olSchool = SafeStr(rdr, "olevel_school");
                        if (!string.IsNullOrEmpty(olSchool)) txtOLevelSchool.Text = olSchool;

                        string olIdx = SafeStr(rdr, "olevel_index");
                        if (!string.IsNullOrEmpty(olIdx)) txtOLevelIndex.Text = olIdx;

                        string alSchool = SafeStr(rdr, "alevel_school");
                        if (!string.IsNullOrEmpty(alSchool)) txtALevelSchool.Text = alSchool;

                        string alIdx = SafeStr(rdr, "alevel_index");
                        if (!string.IsNullOrEmpty(alIdx)) txtALevelIndex.Text = alIdx;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ShowError("Error loading student for edit: " + ex.Message);
        }
    }

    // ===============================================================
    //  LOAD APPLICANT FOR EDIT (by stud_entry_no, no acad_student row required)
    // ===============================================================
    private void LoadApplicantForEdit(string eno)
    {
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                // Detect optional columns
                bool hasNatIdNew    = ColExists(conn, "acad_applications", "stud_id_number");
                bool hasNatIdLeg    = !hasNatIdNew && ColExists(conn, "acad_applications", "national_id");
                bool hasSponsorC    = ColExists(conn, "acad_applications", "sponsor_contact");
                bool hasDistrict    = ColExists(conn, "acad_applications", "home_district");
                bool hasPoBox       = ColExists(conn, "acad_applications", "post_box");
                bool hasCountry     = ColExists(conn, "acad_applications", "residence_country");
                bool hasTitle       = ColExists(conn, "acad_applications", "title");
                bool hasDisability  = ColExists(conn, "acad_applications", "physicalDisability");
                bool hasMarital     = ColExists(conn, "acad_applications", "stud_mar_stat");
                bool hasOlSchool    = ColExists(conn, "acad_applications", "olevel_school");
                bool hasOlIndex     = ColExists(conn, "acad_applications", "olevel_index");
                bool hasAlSchool    = ColExists(conn, "acad_applications", "alevel_school");
                bool hasAlIndex     = ColExists(conn, "acad_applications", "alevel_index");
                bool hasMethod      = ColExists(conn, "acad_applications", "stud_entry_method");
                bool hasBilling     = ColExists(conn, "acad_applications", "billingID");
                bool hasKinRel      = ColExists(conn, "acad_applications", "kin_relationship");
                bool hasKinContact  = ColExists(conn, "acad_applications", "kin_contacts");
                // Education — year / aggregate / points / other qualification columns
                bool hasOlYr        = ColExists(conn, "acad_applications", "olevel_year");
                bool hasOlYrFb      = !hasOlYr  && ColExists(conn, "acad_applications", "stud_pob");
                bool hasOlAgg       = ColExists(conn, "acad_applications", "olevel_agg");
                bool hasOlAggFb     = !hasOlAgg && ColExists(conn, "acad_applications", "stud_district");
                bool hasAlYr        = ColExists(conn, "acad_applications", "alevel_year");
                bool hasAlPts       = ColExists(conn, "acad_applications", "alevel_points");
                bool hasAlPtsFb     = !hasAlPts && ColExists(conn, "acad_applications", "stud_ward");
                bool hasOtInst      = ColExists(conn, "acad_applications", "other_institution");
                bool hasOtInstFb    = !hasOtInst && ColExists(conn, "acad_applications", "stud_prevcampus");
                bool hasOtQual      = ColExists(conn, "acad_applications", "other_qualification");
                bool hasOtQualFb    = !hasOtQual && ColExists(conn, "acad_applications", "stud_lg");
                bool hasOtYr        = ColExists(conn, "acad_applications", "other_year");
                bool hasOtYrFb      = !hasOtYr && ColExists(conn, "acad_applications", "stud_village");
                bool hasOtGr        = ColExists(conn, "acad_applications", "other_grade");
                bool hasOtGrFb      = !hasOtGr && ColExists(conn, "acad_applications", "stud_county");

                string natIdExpr   = hasNatIdNew  ? "COALESCE(a.stud_id_number,'')"   : (hasNatIdLeg ? "COALESCE(a.national_id,'')" : "''");
                string sponsorCExpr= hasSponsorC  ? "COALESCE(a.sponsor_contact,'')"  : "''";
                string districtExpr= hasDistrict  ? "COALESCE(a.home_district,'')"    : "''";
                string poBoxExpr   = hasPoBox     ? "COALESCE(a.post_box,'')"         : "''";
                string countryExpr = hasCountry   ? "COALESCE(a.residence_country,'')"  : "''";
                string titleExpr   = hasTitle     ? "COALESCE(a.title,'')"            : "''";
                string disaExpr    = hasDisability? "COALESCE(a.physicalDisability,'')"  : "''";
                string maritalExpr = hasMarital   ? "COALESCE(a.stud_mar_stat,'')"    : "''";
                string olsExpr     = hasOlSchool  ? "COALESCE(a.olevel_school,'')"    : "''";
                string oliExpr     = hasOlIndex   ? "COALESCE(a.olevel_index,'')"     : "''";
                string alsExpr     = hasAlSchool  ? "COALESCE(a.alevel_school,'')"    : "''";
                string aliExpr     = hasAlIndex   ? "COALESCE(a.alevel_index,'')"     : "''";
                string methExpr    = hasMethod    ? "COALESCE(a.stud_entry_method,'')"  : "''";
                string billExpr    = hasBilling   ? "COALESCE(a.billingID,0)"         : "0";
                string kinRelExpr  = hasKinRel    ? "COALESCE(a.kin_relationship,'')" : "''";
                string kinConExpr  = hasKinContact? "COALESCE(a.kin_contacts,'')"     : "''";
                // Build expressions for education fields with fallback to legacy columns
                string olYrExpr   = hasOlYr   ? "COALESCE(a.olevel_year,'')"          : (hasOlYrFb   ? "COALESCE(a.stud_pob,'')"          : "''");
                string olAggExpr  = hasOlAgg  ? "COALESCE(a.olevel_agg,'')"           : (hasOlAggFb  ? "COALESCE(a.stud_district,'')"       : "''");
                string alYrExpr   = hasAlYr   ? "COALESCE(a.alevel_year,'')"          : "''";
                string alPtsExpr  = hasAlPts  ? "COALESCE(a.alevel_points,'')"        : (hasAlPtsFb  ? "COALESCE(a.stud_ward,'')"           : "''");
                string otInstExpr = hasOtInst ? "COALESCE(a.other_institution,'')"    : (hasOtInstFb ? "COALESCE(a.stud_prevcampus,'')"     : "''");
                string otQualExpr = hasOtQual ? "COALESCE(a.other_qualification,'')"  : (hasOtQualFb ? "COALESCE(a.stud_lg,'')"             : "''");
                string otYrExpr   = hasOtYr   ? "COALESCE(a.other_year,'')"           : (hasOtYrFb   ? "COALESCE(a.stud_village,'')"        : "''");
                string otGrExpr   = hasOtGr   ? "COALESCE(a.other_grade,'')"          : (hasOtGrFb   ? "COALESCE(a.stud_county,'')"         : "''");

                string sql = @"
                    SELECT
                        COALESCE(a.stud_name,'')         AS stud_name,
                        COALESCE(a.stud_sex,'M')         AS stud_sex,
                        a.stud_birthdate,
                        COALESCE(a.stud_nationality,'UGANDAN') AS nationality,
                        COALESCE(a.stud_religion,'')     AS religion,
                        COALESCE(a.stud_phone,'')        AS phone,
                        COALESCE(a.stud_email,'')        AS email,
                        COALESCE(a.stud_campus,'')       AS campus,
                        COALESCE(a.stud_intake,'')       AS intake,
                        COALESCE(a.stud_entry_year,'')   AS entry_year,
                        COALESCE(a.stud_sponsor,'')      AS sponsor,
                        COALESCE(a.next_kin,'')          AS kin_name,
                        COALESCE(a.stud_reg_no,'')       AS stud_reg_no,
                        COALESCE(a.stud_phy_address,'')  AS address,
                        COALESCE(c.prog_id,'')           AS prog_id,
                        COALESCE(c.adm_session,'')       AS session,
                        COALESCE(CAST(c.sub_comb AS CHAR),'') AS sub_comb,
                        " + natIdExpr   + @" AS national_id,
                        " + sponsorCExpr+ @" AS sponsor_contact,
                        " + districtExpr+ @" AS district,
                        " + poBoxExpr   + @" AS pobox,
                        " + countryExpr + @" AS country,
                        " + titleExpr   + @" AS title,
                        " + disaExpr    + @" AS disability,
                        " + maritalExpr + @" AS marital,
                        " + olsExpr     + @" AS olevel_school,
                        " + oliExpr     + @" AS olevel_index,
                        " + alsExpr     + @" AS alevel_school,
                        " + aliExpr     + @" AS alevel_index,
                        " + methExpr    + @" AS entry_method,
                        " + billExpr    + @" AS billing_id,
                        " + kinRelExpr  + @" AS kin_relationship,
                        " + kinConExpr  + @" AS kin_contacts,
                        COALESCE(a.referee_name,'')     AS referee_name,
                        COALESCE(a.referee_contacts,'') AS referee_contacts,
                        " + olYrExpr    + @" AS edu_olevel_year,
                        " + olAggExpr   + @" AS edu_olevel_agg,
                        " + alYrExpr    + @" AS edu_alevel_year,
                        " + alPtsExpr   + @" AS edu_alevel_points,
                        " + otInstExpr  + @" AS edu_other_inst,
                        " + otQualExpr  + @" AS edu_other_qual,
                        " + otYrExpr    + @" AS edu_other_year,
                        " + otGrExpr    + @" AS edu_other_grade
                    FROM acad_applications a
                    LEFT JOIN acad_applicant_choices c ON c.stud_entry_no = a.stud_entry_no AND c.Choice = 1
                    WHERE a.stud_entry_no = @eno
                    LIMIT 1";

                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@eno", eno);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read())
                        {
                            ShowError("Application not found: " + Server.HtmlEncode(eno));
                            return;
                        }

                        // Page chrome
                        litPageTitle.Text         = "Edit Application";
                        litPageSubtitle.Text      = "Editing application <strong>" + Server.HtmlEncode(eno) + "</strong>";
                        btnSubmit.Text = "Save Application";

                        // Name — populate hidden merged field and the three visible fields
                        string fullNameVal = SafeStr(rdr, "stud_name");
                        txtFullName.Text = fullNameVal;
                        SplitFullNameIntoFields(fullNameVal);

                        // Gender: acad_applications stores M / F / O (1-char column)
                        string rawSex = SafeStr(rdr, "stud_sex");
                        TrySelect(ddlGender, rawSex == "O" ? "OTHER" : rawSex);

                        // DOB
                        if (rdr["stud_birthdate"] != DBNull.Value)
                            txtDOB.Text = Convert.ToDateTime(rdr["stud_birthdate"]).ToString("yyyy-MM-dd");

                        // Personal
                        TrySelect(ddlNationality, SafeStr(rdr, "nationality"));
                        TrySelect(ddlReligion,    SafeStr(rdr, "religion"));
                        TrySelect(ddlMarital,     SafeStr(rdr, "marital"));
                        TrySelect(ddlTitle,       SafeStr(rdr, "title"));
                        string dis = SafeStr(rdr, "disability");
                        if (!string.IsNullOrEmpty(dis) && dis != "-") txtDisability.Text = dis;

                        // National ID
                        txtNationalId.Text = SafeStr(rdr, "national_id");

                        // Contact
                        txtPhone.Text   = SafeStr(rdr, "phone");
                        txtEmail.Text   = SafeStr(rdr, "email");
                        txtAddress.Text = SafeStr(rdr, "address");
                        string pb = SafeStr(rdr, "pobox");   if (!string.IsNullOrEmpty(pb))  txtPostBox.Text   = pb;
                        string dt = SafeStr(rdr, "district"); if (!string.IsNullOrEmpty(dt)) txtDistrict.Text  = dt;
                        string rc = SafeStr(rdr, "country");  if (!string.IsNullOrEmpty(rc)) txtResCountry.Text = rc;

                        // Sponsor & Kin
                        txtSponsor.Text        = SafeStr(rdr, "sponsor");
                        txtSponsorContact.Text = SafeStr(rdr, "sponsor_contact");
                        txtKinName.Text        = SafeStr(rdr, "kin_name");
                        TrySelect(ddlKinRelation, SafeStr(rdr, "kin_relationship"));
                        txtKinContact.Text = SafeStr(rdr, "kin_contacts");

                        string refNm2 = SafeStr(rdr, "referee_name");
                        if (!string.IsNullOrEmpty(refNm2)) txtRefereeName.Text = refNm2;
                        string refCt2 = SafeStr(rdr, "referee_contacts");
                        if (!string.IsNullOrEmpty(refCt2)) txtRefereeContact.Text = refCt2;

                        // Education — existing fields
                        string ols = SafeStr(rdr, "olevel_school"); if (!string.IsNullOrEmpty(ols)) txtOLevelSchool.Text = ols;
                        string oli = SafeStr(rdr, "olevel_index");  if (!string.IsNullOrEmpty(oli)) txtOLevelIndex.Text  = oli;
                        string als = SafeStr(rdr, "alevel_school"); if (!string.IsNullOrEmpty(als)) txtALevelSchool.Text = als;
                        string ali = SafeStr(rdr, "alevel_index");  if (!string.IsNullOrEmpty(ali)) txtALevelIndex.Text  = ali;

                        // Education — year / aggregate / points / other qualification
                        // (aliased with edu_ prefix since SELECT s.* may already have same-named columns)
                        string olYr  = SafeStr(rdr, "edu_olevel_year");    if (!string.IsNullOrEmpty(olYr) && olYr != "0")  txtOLevelYear.Text = olYr;
                        string olAgg = SafeStr(rdr, "edu_olevel_agg");     if (!string.IsNullOrEmpty(olAgg) && olAgg != "0") txtOLevelAgg.Text = olAgg;
                        string alYr  = SafeStr(rdr, "edu_alevel_year");    if (!string.IsNullOrEmpty(alYr) && alYr != "0")  txtALevelYear.Text = alYr;
                        string alPts = SafeStr(rdr, "edu_alevel_points");  if (!string.IsNullOrEmpty(alPts) && alPts != "0") txtALevelPoints.Text = alPts;
                        string otIn  = SafeStr(rdr, "edu_other_inst");     if (!string.IsNullOrEmpty(otIn))  txtOtherInst.Text  = otIn;
                        string otQu  = SafeStr(rdr, "edu_other_qual");     if (!string.IsNullOrEmpty(otQu))  txtOtherQual.Text  = otQu;
                        string otYr  = SafeStr(rdr, "edu_other_year");  if (!string.IsNullOrEmpty(otYr) && otYr != "0")  txtOtherYear.Text = otYr;
                        string otGr  = SafeStr(rdr, "edu_other_grade"); if (!string.IsNullOrEmpty(otGr))  txtOtherGrade.Text = otGr;

                        // Academic (from applicant_choices)
                        string progId = SafeStr(rdr, "prog_id");
                        if (!string.IsNullOrEmpty(progId))
                        {
                            TrySelect(ddlProgramme, progId);
                            hfProgramme.Value = progId;
                            if (_programmeList != null)
                                foreach (var p in _programmeList)
                                    if (p.Code == progId) { TrySelect(ddlFaculty, p.FacultyCode); hfFaculty.Value = p.FacultyCode; break; }
                        }

                        string sess = SafeStr(rdr, "session");
                        if (!string.IsNullOrEmpty(sess)) { TrySelect(ddlSession, sess); hfSession.Value = sess; }

                        // Campus: acad_applications stores campus_code string or int
                        string camp = SafeStr(rdr, "campus");
                        if (!string.IsNullOrEmpty(camp))
                        {
                            // Try direct match, then zero-padded 2-digit match
                            if (ddlCampus.Items.FindByValue(camp) != null)
                            { TrySelect(ddlCampus, camp); hfCampus.Value = camp; }
                            else
                            {
                                int campInt;
                                if (int.TryParse(camp, out campInt))
                                {
                                    string campPad = campInt.ToString().PadLeft(2, '0');
                                    TrySelect(ddlCampus, campPad); hfCampus.Value = campPad;
                                }
                            }
                        }

                        string intake = SafeStr(rdr, "intake");
                        if (!string.IsNullOrEmpty(intake)) TrySelect(ddlIntake, intake);

                        string entYr = SafeStr(rdr, "entry_year");
                        if (!string.IsNullOrEmpty(entYr))
                        {
                            if (ddlEntryYear.Items.FindByValue(entYr) == null)
                                ddlEntryYear.Items.Insert(0, new ListItem(entYr, entYr));
                            TrySelect(ddlEntryYear, entYr); hfEntryYear.Value = entYr;
                        }

                        string meth = SafeStr(rdr, "entry_method");
                        if (!string.IsNullOrEmpty(meth)) TrySelect(ddlEntryMethod, meth);

                        int billingId = SafeInt(rdr["billing_id"], 0);
                        if (billingId > 0) { TrySelect(ddlBilling, billingId.ToString()); hfBilling.Value = billingId.ToString(); }

                        // Specialisation
                        string subComb = SafeStr(rdr, "sub_comb");
                        if (!string.IsNullOrEmpty(subComb) && subComb != "0" && subComb != "-")
                        {
                            if (_specList != null)
                            {
                                int scId;
                                bool specFound = false;
                                if (int.TryParse(subComb, out scId) && scId > 0)
                                    foreach (var sp in _specList)
                                        if (sp.Id == scId) { TrySelect(ddlSpecialisation, sp.Id.ToString()); hfSpecialisation.Value = sp.Id.ToString(); specFound = true; break; }
                                if (!specFound)
                                    foreach (var sp in _specList)
                                        if (sp.Name == subComb && (string.IsNullOrEmpty(progId) || sp.ProgCode == progId))
                                        { TrySelect(ddlSpecialisation, sp.Id.ToString()); hfSpecialisation.Value = sp.Id.ToString(); break; }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ShowError("Error loading application for edit: " + ex.Message);
        }
    }

    private bool ColExists(MySqlConnection conn, string table, string column)
    {
        using (var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=@t AND COLUMN_NAME=@c", conn))
        {
            cmd.Parameters.AddWithValue("@t", table);
            cmd.Parameters.AddWithValue("@c", column);
            object v = cmd.ExecuteScalar();
            return v != null && v != DBNull.Value && Convert.ToInt32(v) > 0;
        }
    }

    // ===============================================================
    //  UPDATE APPLICATION FROM FORM (applicant edit mode)
    //  Never touches adm_status or stud_reg_no.
    // ===============================================================
    private void UpdateApplicantFromForm(string eno)
    {
        if (string.IsNullOrEmpty(eno)) { ShowError("Entry number required."); return; }

        // Gather form values
        // Build full name from the three visible fields (server-side, no JS dependency)
        string _sur  = (txtSurname.Text   ?? "").Trim().ToUpper();
        string _fst  = (txtFirstName.Text ?? "").Trim().ToUpper();
        string _oth  = (txtOtherName.Text ?? "").Trim().ToUpper();
        string fullName = string.Join(" ", new[] { _sur, _fst, _oth }
            .Where(s => !string.IsNullOrEmpty(s))).Trim();
        // Keep the hidden merged field in sync for any code that reads it later
        if (!string.IsNullOrEmpty(fullName)) txtFullName.Text = fullName;
        string gender         = ddlGender.SelectedValue;
        string dobStr         = (txtDOB.Text             ?? "").Trim();
        string phone          = (txtPhone.Text           ?? "").Trim();
        string email          = (txtEmail.Text           ?? "").Trim();
        string nationality    = DdlOrHidden(ddlNationality, hfNationality);
        string religion       = ddlReligion.SelectedValue;
        string marital        = ddlMarital.SelectedValue;
        string disability     = (txtDisability.Text      ?? "").Trim();
        string nationalId     = (txtNationalId.Text      ?? "").Trim().ToUpper();
        string title          = ddlTitle.SelectedValue;
        string programme      = DdlOrHidden(ddlProgramme, hfProgramme);
        string session        = DdlOrHidden(ddlSession,   hfSession);
        string campus         = DdlOrHidden(ddlCampus,    hfCampus);
        string entryMethod    = ddlEntryMethod.SelectedValue;
        string entryYear      = DdlOrHidden(ddlEntryYear, hfEntryYear);
        string intake         = ddlIntake.SelectedValue;
        string billing        = DdlOrHidden(ddlBilling,   hfBilling);
        string specIdStr      = DdlOrHidden(ddlSpecialisation, hfSpecialisation);
        string address        = (txtAddress.Text         ?? "").Trim();
        string postBox        = (txtPostBox.Text         ?? "").Trim();
        string district       = (txtDistrict.Text        ?? "").Trim();
        string resCountry     = (txtResCountry.Text      ?? "").Trim();
        string sponsor        = (txtSponsor.Text         ?? "").Trim();
        string sponsorContact = (txtSponsorContact.Text  ?? "").Trim();
        string kinName        = (txtKinName.Text         ?? "").Trim();
        string kinRelation    = ddlKinRelation.SelectedValue;
        string kinContact     = (txtKinContact.Text      ?? "").Trim();
        string refereeName    = (txtRefereeName.Text     ?? "").Trim();
        string refereeContact = (txtRefereeContact.Text  ?? "").Trim();
        string oLevelSchool   = (txtOLevelSchool.Text    ?? "").Trim().ToUpper();
        string oLevelIndex    = (txtOLevelIndex.Text     ?? "").Trim().ToUpper();
        string oLevelYear     = (txtOLevelYear.Text       ?? "").Trim();
        string oLevelAgg      = (txtOLevelAgg.Text       ?? "").Trim();
        string aLevelSchool   = (txtALevelSchool.Text    ?? "").Trim().ToUpper();
        string aLevelIndex    = (txtALevelIndex.Text     ?? "").Trim().ToUpper();
        string aLevelYear     = (txtALevelYear.Text      ?? "").Trim();
        string aLevelPoints   = (txtALevelPoints.Text    ?? "").Trim();
        string otherInst      = (txtOtherInst.Text       ?? "").Trim().ToUpper();
        string otherQual      = (txtOtherQual.Text       ?? "").Trim().ToUpper();
        string otherYear      = (txtOtherYear.Text       ?? "").Trim();
        string otherGrade     = (txtOtherGrade.Text      ?? "").Trim().ToUpper();

        if (string.IsNullOrEmpty(fullName))   { ShowError("Please enter the full name."); return; }
        if (string.IsNullOrEmpty(programme))  { ShowError("Please select a programme."); return; }

        DateTime birthDate = new DateTime(1980, 1, 1);
        if (!string.IsNullOrEmpty(dobStr)) { DateTime p; if (DateTime.TryParse(dobStr, out p)) birthDate = p; }
        if (string.IsNullOrEmpty(nationality)) nationality = "UGANDAN";

        int specId = SafeInt(specIdStr, 0);

        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                bool hasNatIdNew   = ColExists(conn, "acad_applications", "stud_id_number");
                bool hasNatIdLeg   = !hasNatIdNew && ColExists(conn, "acad_applications", "national_id");
                bool hasSponsorC   = ColExists(conn, "acad_applications", "sponsor_contact");
                bool hasDistrict   = ColExists(conn, "acad_applications", "home_district");
                bool hasPoBox      = ColExists(conn, "acad_applications", "post_box");
                bool hasCountry    = ColExists(conn, "acad_applications", "residence_country");
                bool hasTitle      = ColExists(conn, "acad_applications", "title");
                bool hasDisab      = ColExists(conn, "acad_applications", "physicalDisability");
                bool hasMarital    = ColExists(conn, "acad_applications", "stud_mar_stat");
                bool hasOlSchool   = ColExists(conn, "acad_applications", "olevel_school");
                bool hasOlIndex    = ColExists(conn, "acad_applications", "olevel_index");
                bool hasOlYr       = ColExists(conn, "acad_applications", "olevel_year");
                bool hasOlYrFb     = !hasOlYr  && ColExists(conn, "acad_applications", "stud_pob");      // legacy fallback
                bool hasOlAgg      = ColExists(conn, "acad_applications", "olevel_agg");
                bool hasOlAggFb    = !hasOlAgg && ColExists(conn, "acad_applications", "stud_district");  // legacy fallback
                bool hasAlSchool   = ColExists(conn, "acad_applications", "alevel_school");
                bool hasAlIndex    = ColExists(conn, "acad_applications", "alevel_index");
                bool hasAlYr       = ColExists(conn, "acad_applications", "alevel_year");
                bool hasAlPts      = ColExists(conn, "acad_applications", "alevel_points");
                bool hasAlPtsFb    = !hasAlPts && ColExists(conn, "acad_applications", "stud_ward");
                bool hasOtInst     = ColExists(conn, "acad_applications", "other_institution");
                bool hasOtInstFb   = !hasOtInst && ColExists(conn, "acad_applications", "stud_prevcampus");
                bool hasOtQual     = ColExists(conn, "acad_applications", "other_qualification");
                bool hasOtQualFb   = !hasOtQual && ColExists(conn, "acad_applications", "stud_lg");
                bool hasOtYr       = ColExists(conn, "acad_applications", "other_year");
                bool hasOtYrFb     = !hasOtYr && ColExists(conn, "acad_applications", "stud_village");
                bool hasOtGr       = ColExists(conn, "acad_applications", "other_grade");
                bool hasOtGrFb     = !hasOtGr && ColExists(conn, "acad_applications", "stud_county");
                bool hasMethod     = ColExists(conn, "acad_applications", "stud_entry_method");
                bool hasBilling    = ColExists(conn, "acad_applications", "billingID");
                bool hasKinRel     = ColExists(conn, "acad_applications", "kin_relationship");
                bool hasKinCon     = ColExists(conn, "acad_applications", "kin_contacts");
                bool hasUpdatedAt  = ColExists(conn, "acad_applications", "app_last_updated_at");

                // Build dynamic SET clause — never include adm_status or stud_reg_no
                var sets  = new System.Collections.Generic.List<string>();
                var parms = new System.Collections.Generic.Dictionary<string, object>();

                sets.Add("stud_name=@nm");        parms["@nm"]   = fullName;
                sets.Add("stud_sex=@sx");         parms["@sx"]   = gender == "OTHER" ? "O" : gender;
                sets.Add("stud_birthdate=@dob");  parms["@dob"]  = birthDate;
                sets.Add("stud_nationality=@nat");parms["@nat"]  = nationality;
                sets.Add("stud_religion=@rel");   parms["@rel"]  = religion;
                sets.Add("stud_phone=@phn");      parms["@phn"]  = phone;
                sets.Add("stud_email=@eml");      parms["@eml"]  = email;
                sets.Add("stud_campus=@cp");      parms["@cp"]   = campus;
                sets.Add("stud_intake=@itk");     parms["@itk"]  = intake;
                sets.Add("next_kin=@kin");         parms["@kin"]  = kinName;
                sets.Add("stud_phy_address=@adr"); parms["@adr"] = address;
                sets.Add("stud_sponsor=@spn");    parms["@spn"]  = sponsor;
                if (!string.IsNullOrEmpty(entryYear)) { sets.Add("stud_entry_year=@yr"); parms["@yr"] = entryYear; }
                if (hasNatIdNew)  { sets.Add("stud_id_number=@nid");  parms["@nid"] = nationalId; }
                else if (hasNatIdLeg) { sets.Add("national_id=@nid"); parms["@nid"] = nationalId; }
                if (hasSponsorC) { sets.Add("sponsor_contact=@sc");   parms["@sc"]  = sponsorContact; }
                if (hasDistrict) { sets.Add("home_district=@dst");    parms["@dst"] = district; }
                if (hasPoBox)    { sets.Add("post_box=@pb");          parms["@pb"]  = postBox; }
                if (hasCountry)  { sets.Add("residence_country=@cn"); parms["@cn"]  = resCountry; }
                if (hasTitle)    { sets.Add("title=@ttl");            parms["@ttl"] = title; }
                if (hasDisab)    { sets.Add("physicalDisability=@di");parms["@di"]  = disability; }
                if (hasMarital)  { sets.Add("stud_mar_stat=@mar");    parms["@mar"] = marital; }
                // Education — varchar fields
                if (hasOlSchool) { sets.Add("olevel_school=@ols");       parms["@ols"] = StrOrNull(oLevelSchool); }
                if (hasOlIndex)  { sets.Add("olevel_index=@oli");        parms["@oli"] = StrOrNull(oLevelIndex); }
                // olevel_year: INT column; fallback to stud_pob (varchar) if canonical column absent
                if (hasOlYr)          { sets.Add("olevel_year=@oly");    parms["@oly"] = IntOrNull(oLevelYear); }
                else if (hasOlYrFb)   { sets.Add("stud_pob=@oly");      parms["@oly"] = StrOrNull(oLevelYear); }
                // olevel_agg: may be INT or VARCHAR — use StrOrNull so "DISTINCTION" isn't lost;
                // fallback to stud_district (varchar) when column absent
                if (hasOlAgg)         { sets.Add("olevel_agg=@ola");    parms["@ola"] = StrOrNull(oLevelAgg); }
                else if (hasOlAggFb)  { sets.Add("stud_district=@ola"); parms["@ola"] = StrOrNull(oLevelAgg); }
                if (hasAlSchool) { sets.Add("alevel_school=@als");       parms["@als"] = StrOrNull(aLevelSchool); }
                if (hasAlIndex)  { sets.Add("alevel_index=@ali");        parms["@ali"] = StrOrNull(aLevelIndex); }
                // alevel_year: INT column; no known legacy fallback
                if (hasAlYr)     { sets.Add("alevel_year=@aly");         parms["@aly"] = IntOrNull(aLevelYear); }
                // alevel_points: can hold "PPP" — use StrOrNull for both canonical and fallback
                if (hasAlPts)         { sets.Add("alevel_points=@alp");  parms["@alp"] = StrOrNull(aLevelPoints); }
                else if (hasAlPtsFb)  { sets.Add("stud_ward=@alp");     parms["@alp"] = StrOrNull(aLevelPoints); }
                if (hasOtInst)        { sets.Add("other_institution=@oi");  parms["@oi"] = StrOrNull(otherInst); }
                else if (hasOtInstFb) { sets.Add("stud_prevcampus=@oi");   parms["@oi"] = StrOrNull(otherInst); }
                if (hasOtQual)        { sets.Add("other_qualification=@oq"); parms["@oq"] = StrOrNull(otherQual); }
                else if (hasOtQualFb) { sets.Add("stud_lg=@oq");           parms["@oq"] = StrOrNull(otherQual); }
                if (hasOtYr)          { sets.Add("other_year=@oyr");        parms["@oyr"] = IntOrNull(otherYear); }
                else if (hasOtYrFb)   { sets.Add("stud_village=@oyr");     parms["@oyr"] = StrOrNull(otherYear); }
                if (hasOtGr)          { sets.Add("other_grade=@ogr");       parms["@ogr"] = StrOrNull(otherGrade); }
                else if (hasOtGrFb)   { sets.Add("stud_county=@ogr");      parms["@ogr"] = StrOrNull(otherGrade); }
                if (hasMethod)   { sets.Add("stud_entry_method=@mth");  parms["@mth"] = entryMethod; }
                if (hasBilling && !string.IsNullOrEmpty(billing)) { sets.Add("billingID=@bil"); parms["@bil"] = SafeInt(billing, 1); }
                if (hasKinRel)   { sets.Add("kin_relationship=@kr");  parms["@kr"]  = kinRelation; }
                if (hasKinCon)   { sets.Add("kin_contacts=@kc");      parms["@kc"]  = kinContact; }
                sets.Add("referee_name=@rfn");     parms["@rfn"] = StrOrNull(refereeName);
                sets.Add("referee_contacts=@rfc"); parms["@rfc"] = StrOrNull(refereeContact);
                if (hasUpdatedAt){ sets.Add("app_last_updated_at=@upd"); parms["@upd"] = DateTime.UtcNow; }

                parms["@eno"] = eno;
                int appRows;
                using (var cmd = new MySqlCommand(
                    "UPDATE acad_applications SET " + string.Join(",", sets.ToArray()) + " WHERE stud_entry_no=@eno", conn))
                {
                    foreach (var kv in parms) cmd.Parameters.AddWithValue(kv.Key, kv.Value);
                    appRows = cmd.ExecuteNonQuery();
                }
                if (appRows == 0)
                    throw new Exception("No application record found for entry number " + eno + ". The record may have been deleted or the entry number is incorrect.");

                // Upsert acad_applicant_choices: UPDATE if row exists, INSERT if not.
                // This handles walk-in applicants who may have no Choice=1 row yet.
                using (var cmd = new MySqlCommand(@"
                    INSERT INTO acad_applicant_choices (stud_entry_no, Choice, prog_id, adm_status, adm_session, sub_comb)
                    VALUES (@eno, 1, @pg, 0, @ss, @sp)
                    ON DUPLICATE KEY UPDATE
                        prog_id     = VALUES(prog_id),
                        adm_session = VALUES(adm_session),
                        sub_comb    = VALUES(sub_comb)", conn))
                {
                    cmd.Parameters.AddWithValue("@eno", eno);
                    cmd.Parameters.AddWithValue("@pg",  programme ?? "");
                    cmd.Parameters.AddWithValue("@ss",  session   ?? "");
                    cmd.Parameters.AddWithValue("@sp",  specId > 0 ? specId : 0);
                    cmd.ExecuteNonQuery();
                }

                // If a student record exists, also update acad_student for consistency
                string regno = "";
                using (var cmd = new MySqlCommand(
                    "SELECT COALESCE(stud_reg_no,'') FROM acad_applications WHERE stud_entry_no=@eno LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@eno", eno);
                    object rv = cmd.ExecuteScalar();
                    if (rv != null && rv != DBNull.Value) regno = rv.ToString().Trim();
                }

                bool studentExists = false;
                if (!string.IsNullOrEmpty(regno) && regno != "-")
                {
                    using (var cmd = new MySqlCommand(
                        "SELECT COUNT(*) FROM acad_student WHERE regno=@r OR entryno=@eno", conn))
                    {
                        cmd.Parameters.AddWithValue("@r",   regno);
                        cmd.Parameters.AddWithValue("@eno", eno);
                        studentExists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }

                if (studentExists)
                {
                    // Normalise names: first token = othername (surname), rest = firstname
                    string fName = fullName, oName = "";
                    int si = fullName.IndexOf(' ');
                    if (si > 0) { oName = fullName.Substring(0, si).Trim(); fName = fullName.Substring(si + 1).Trim(); }
                    string genderDb = gender == "F" ? "FEMALE" : (gender == "OTHER" ? "OTHER" : "MALE");

                    using (var cmd = new MySqlCommand(@"
                        UPDATE acad_student SET
                            firstname=@fn, othername=@on, gender=@gd, dob=@dob,
                            nationality=@nat, national_id=@nid, religion=@rel,
                            studPhone=@phn, email=@eml,
                            progid=@prog, studsesion=@sess, specialisation=@spec,
                            intake=@itk, billingID=@bil
                        WHERE regno=@r OR entryno=@eno", conn))
                    {
                        cmd.Parameters.AddWithValue("@fn",   fName);
                        cmd.Parameters.AddWithValue("@on",   oName);
                        cmd.Parameters.AddWithValue("@gd",   genderDb);
                        cmd.Parameters.AddWithValue("@dob",  birthDate);
                        cmd.Parameters.AddWithValue("@nat",  nationality);
                        cmd.Parameters.AddWithValue("@nid",  nationalId);
                        cmd.Parameters.AddWithValue("@rel",  religion);
                        cmd.Parameters.AddWithValue("@phn",  phone);
                        cmd.Parameters.AddWithValue("@eml",  email);
                        cmd.Parameters.AddWithValue("@prog", programme);
                        cmd.Parameters.AddWithValue("@sess", session);
                        cmd.Parameters.AddWithValue("@spec", specId > 0 ? specIdStr : "");
                        cmd.Parameters.AddWithValue("@itk",  intake);
                        cmd.Parameters.AddWithValue("@bil",  SafeInt(billing, 1));
                        cmd.Parameters.AddWithValue("@r",    regno);
                        cmd.Parameters.AddWithValue("@eno",  eno);
                        cmd.ExecuteNonQuery();
                    }
                }

                // Activity log
                using (var cmd = new MySqlCommand(@"
                    INSERT INTO acad_activity_log(user_id,page_function,par,comments,access_date)
                    VALUES(@usr,'Edit Application',@par,'Application data updated',NOW())", conn))
                {
                    cmd.Parameters.AddWithValue("@usr", GetCurrentUser());
                    cmd.Parameters.AddWithValue("@par", string.Format("ENO:{0} prog:{1}", eno, programme));
                    cmd.ExecuteNonQuery();
                }

                // Show success + redirect back
                string returnUrl = hfReturnUrl.Value;
                if (string.IsNullOrEmpty(returnUrl))
                    returnUrl = ResolveUrl("~/COOPERP/NewScreens/AdmissionsController.aspx");

                ShowSuccess(string.Format(
                    "Saved — Entry: {0} &nbsp;|&nbsp; Name: {1} &nbsp;|&nbsp; Phone: {2} &nbsp;|&nbsp; Programme: {3}",
                    Server.HtmlEncode(eno),
                    Server.HtmlEncode(fullName),
                    Server.HtmlEncode(phone),
                    Server.HtmlEncode(programme)));
            }
        }
        catch (Exception ex)
        {
            ShowError("Failed to update application: " + ex.Message);
        }
    }

    /// <summary>Safe string reader - returns "" for null/DBNull columns.</summary>
    private static string SafeStr(MySqlDataReader rdr, string col)
    {
        try
        {
            int ord = rdr.GetOrdinal(col);
            if (rdr.IsDBNull(ord)) return "";
            return rdr.GetString(ord).Trim();
        }
        catch { return ""; }
    }

    // ===============================================================
    //  REGISTRATION HANDLER - 8-step process
    // ===============================================================
    protected void btnSubmitRegistration_Click(object sender, EventArgs e)
    {
        // -- APPLICANT EDIT MODE: route to application update --------
        if (IsApplicantMode)
        {
            UpdateApplicantFromForm(hfEditEno.Value);
            return;
        }

        // -- STUDENT EDIT MODE: route to student update --------------
        if (IsEditMode)
        {
            UpdateExistingStudent();
            return;
        }

        // -- Gather form values --------------------------------------
        // Use hidden-field fallback for dropdowns (ViewState is off,
        // and programme list may have been filtered client-side by JS)
        string title        = ddlTitle.SelectedValue;
        string fullName     = (txtFullName.Text ?? "").Trim().ToUpper();
        string gender       = ddlGender.SelectedValue;
        string dobStr       = (txtDOB.Text ?? "").Trim();
        string phone        = (txtPhone.Text ?? "").Trim();
        string email        = (txtEmail.Text ?? "").Trim();
        string nationality  = DdlOrHidden(ddlNationality, hfNationality);
        string religion     = ddlReligion.SelectedValue;
        string marital      = ddlMarital.SelectedValue;
        string disability   = (txtDisability.Text ?? "").Trim();
        string nationalId   = (txtNationalId.Text ?? "").Trim().ToUpper();

        string programme    = DdlOrHidden(ddlProgramme, hfProgramme);
        string session      = DdlOrHidden(ddlSession, hfSession);
        string campus       = DdlOrHidden(ddlCampus, hfCampus);
        string entryMethod  = ddlEntryMethod.SelectedValue;
        string entryYear    = DdlOrHidden(ddlEntryYear, hfEntryYear);
        string intake       = ddlIntake.SelectedValue;
        string billing      = DdlOrHidden(ddlBilling, hfBilling);
        string specIdStr    = DdlOrHidden(ddlSpecialisation, hfSpecialisation);

        string address      = (txtAddress.Text ?? "").Trim();
        string postBox      = (txtPostBox.Text ?? "").Trim();
        string district     = (txtDistrict.Text ?? "").Trim();
        string resCountry   = (txtResCountry.Text ?? "").Trim();

        string sponsor        = (txtSponsor.Text ?? "").Trim();
        string sponsorContact = (txtSponsorContact.Text ?? "").Trim();
        string kinName        = (txtKinName.Text ?? "").Trim();
        string kinRelation    = ddlKinRelation.SelectedValue;
        string kinContact     = (txtKinContact.Text ?? "").Trim();
        string refereeName    = (txtRefereeName.Text    ?? "").Trim();
        string refereeContact = (txtRefereeContact.Text ?? "").Trim();

        string oLevelSchool = (txtOLevelSchool.Text ?? "").Trim();
        string oLevelIndex  = (txtOLevelIndex.Text ?? "").Trim();
        string aLevelSchool = (txtALevelSchool.Text ?? "").Trim();
        string aLevelIndex  = (txtALevelIndex.Text ?? "").Trim();

        // -- Server-side validation ----------------------------------
        if (string.IsNullOrEmpty(fullName))
        { ShowError("Please enter the student's full name."); return; }
        if (string.IsNullOrEmpty(programme))
        { ShowError("Please select a programme."); return; }
        if (string.IsNullOrEmpty(phone))
        { ShowError("Please enter the student's phone number."); return; }
        if (string.IsNullOrEmpty(session))
        { ShowError("Please select a study session."); return; }
        if (string.IsNullOrEmpty(campus))
        { ShowError("Please select a campus."); return; }
        if (string.IsNullOrEmpty(entryYear))
        { ShowError("Please select an entry year."); return; }
        if (SafeInt(entryYear, 0) > AcademicYearHelper.MaxAdmissionEntryYear())
        { ShowError("Entry year " + entryYear + " belongs to academic year " + entryYear + "/" + (SafeInt(entryYear, 0) + 1) +
                    ", which has not started and is not open for admission. A January intake joins the current academic year (" +
                    AcademicYearHelper.GetCurrentAcademicYear() + ")."); return; }
        if (string.IsNullOrEmpty(billing))
        { ShowError("Please select a billing system."); return; }

        // Parse DOB
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
                    ShowError("Failed to generate an entry number. Please try again.");
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
                        stud_reg_no, billingID, national_id
                    ) VALUES (
                        @entryNo, @name, @sex, @nationality, @religion,
                        @entryMethod, @sponsor, @sponsorContact, @entryYear,
                        @dob, @phone, @kinName, @address, @email,
                        @marital, @campus, 0, '',
                        @resCountry, @oLevelSchool, @aLevelSchool, @refereeName,
                        @refereeContact, '', '', '',
                        @oLevelIndex, @aLevelIndex, '', @postBox, @district,
                        '', @kinContact, @kinRelation, 0, '',
                        @intake, '', @title, @disability,
                        '-', @billingID, @nationalId
                    )";
                using (var cmd = new MySqlCommand(insertAppSql, conn))
                {
                    cmd.Parameters.AddWithValue("@entryNo",        entryNo);
                    cmd.Parameters.AddWithValue("@name",           fullName);
                    cmd.Parameters.AddWithValue("@sex",            gender == "OTHER" ? "O" : gender);
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
                    cmd.Parameters.AddWithValue("@refereeName",    refereeName);
                    cmd.Parameters.AddWithValue("@refereeContact", refereeContact);
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
                    cmd.Parameters.AddWithValue("@nationalId",    nationalId);
                    cmd.ExecuteNonQuery();
                }

                // -- Step 3: Insert applicant choice (admitted) -----
                const string insertChoiceSql = @"
                    INSERT INTO acad_applicant_choices
                        (stud_entry_no, Choice, prog_id, adm_status, adm_session, sub_comb)
                    VALUES
                        (@eno, 1, @prog, 1, @session, @specId)";
                using (var cmd = new MySqlCommand(insertChoiceSql, conn))
                {
                    cmd.Parameters.AddWithValue("@eno",     entryNo);
                    cmd.Parameters.AddWithValue("@prog",    programme);
                    cmd.Parameters.AddWithValue("@session", session);
                    cmd.Parameters.AddWithValue("@specId",  SafeInt(specIdStr, 0));
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

                // -- Step 6: Create the student account only (acad_student).
                // Inline equivalent of acad_RegisterApplicant SP — bypasses any SP-level
                // academic-year restriction so registration works for any entry year.
                // Semester registration is NOT created here — students self-register.

                // 6a: Look up prog/session/spec from applicant choices
                string regProg = "", regSess = "", regSpec = "", regNewNo = "";
                using (var cmd = new MySqlCommand(
                    "SELECT prog_id, adm_session, sub_comb FROM acad_applicant_choices WHERE stud_entry_no=@eno AND choice=1 LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@eno", entryNo);
                    using (var rdr = cmd.ExecuteReader())
                        if (rdr.Read())
                        {
                            regProg = rdr["prog_id"]    != DBNull.Value ? rdr["prog_id"].ToString()    : "";
                            regSess = rdr["adm_session"] != DBNull.Value ? rdr["adm_session"].ToString() : "";
                            regSpec = rdr["sub_comb"]   != DBNull.Value ? rdr["sub_comb"].ToString()   : "";
                        }
                }
                using (var cmd = new MySqlCommand(
                    "SELECT stud_reg_no FROM acad_applications WHERE stud_entry_no=@eno LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@eno", entryNo);
                    object v = cmd.ExecuteScalar();
                    if (v != null && v != DBNull.Value) regNewNo = v.ToString();
                }

                // 6b: Insert into acad_student
                using (var cmd = new MySqlCommand(@"
                    INSERT IGNORE INTO acad_student(
                        entryno, regno, firstname, dob, gender, nationality, religion,
                        entrymethod, progid, studPhone, email, entryyear, studsesion,
                        home_dist, intake, gradSystemID, othername, duration, specialisation, studcampus
                    )
                    SELECT stud_reg_no, stud_entry_no,
                        REPLACE(SUBSTRING_INDEX(stud_name,' ',3), SUBSTRING_INDEX(stud_name,' ',1), ''),
                        stud_birthdate,
                        CASE stud_sex WHEN 'M' THEN 'MALE' WHEN 'F' THEN 'FEMALE' WHEN 'O' THEN 'OTHER' ELSE stud_sex END,
                        stud_nationality, stud_religion, stud_entry_method,
                        @prog, stud_phone, stud_email, stud_entry_year, @sess, home_district,
                        stud_intake, 1,
                        UPPER(SUBSTRING_INDEX(stud_name,' ',1)),
                        Acad_GetApplicantDetails(6, @prog),
                        @spec, stud_campus
                    FROM acad_applications WHERE stud_entry_no=@eno", conn))
                {
                    cmd.Parameters.AddWithValue("@prog", regProg);
                    cmd.Parameters.AddWithValue("@sess", regSess);
                    cmd.Parameters.AddWithValue("@spec", regSpec);
                    cmd.Parameters.AddWithValue("@eno",  entryNo);
                    cmd.ExecuteNonQuery();
                }

                // 6c: Semester registration is NOT auto-created. Students self-register per
                // semester via the eportal wizard (which sets regstatus='REGISTERED' and bills).
                // Creating an 'UNREGISTERED' placeholder here previously blocked self-registration.

                // 6d: Activity log — best-effort only. Audit logging must NEVER abort (or partially
                // commit) a registration. user_id is capped defensively to the column width.
                try
                {
                    using (var cmd = new MySqlCommand(@"
                        INSERT INTO acad_activity_log(user_id, page_function, par, comments, access_date)
                        VALUES(@usr, 'Applicant Registration', @par, 'Registered Applicant', NOW())", conn))
                    {
                        string usr = GetCurrentUser();
                        if (usr != null && usr.Length > 100) usr = usr.Substring(0, 100);
                        cmd.Parameters.AddWithValue("@usr", usr);
                        cmd.Parameters.AddWithValue("@par", string.Format("prog: {0} Reg No: {1}", regProg, regNewNo));
                        cmd.ExecuteNonQuery();
                    }
                }
                catch { /* audit log is non-critical — never fail the registration on a log error */ }

                // -- Step 7: Update billing system + specialisation on acad_student
                // Store the spec_id (not the name) so the JOIN in listing pages works
                int specId = SafeInt(specIdStr, 0);
                using (var cmd = new MySqlCommand(
                    "UPDATE acad_student SET billingID=@bid, specialisation=@spec, national_id=@nid WHERE regno=@rno", conn))
                {
                    cmd.Parameters.AddWithValue("@bid", SafeInt(billing, 1));
                    cmd.Parameters.AddWithValue("@spec", specId > 0 ? specIdStr : "");
                    cmd.Parameters.AddWithValue("@nid", nationalId);
                    cmd.Parameters.AddWithValue("@rno", regNo);
                    cmd.ExecuteNonQuery();
                }

                // -- Step 8: Immediate registration disabled — students self-register via the portal --

                // -- Success: show result card via startup script ------
                ClientScript.RegisterStartupScript(GetType(), "regSuccess",
                    string.Format("showSuccess('{0}','{1}',false);",
                        EscapeJs(entryNo), EscapeJs(regNo)), true);
            }
        }
        catch (Exception ex)
        {
            ShowError("Failed to register student: " + ex.Message);
        }
    }

    // ===============================================================
    //  AUTO-BILLING
    // ===============================================================
    private void AutoBillStudent(int regId)
    {
        try
        {
            string regno = "", acadYear = "";
            int semester = 0;
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT regno, acad_year, semester FROM acad_registration WHERE ID=@id LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@id", regId);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read()) return;
                        regno    = rdr["regno"].ToString();
                        acadYear = rdr["acad_year"].ToString();
                        semester = Convert.ToInt32(rdr["semester"]);
                    }
                }
            }
            if (string.IsNullOrEmpty(regno)) return;

            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("fin_AutoBillOnRegistration", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 120;
                    cmd.Parameters.AddWithValue("@p_regno",    regno);
                    cmd.Parameters.AddWithValue("@p_acadyear", acadYear);
                    cmd.Parameters.AddWithValue("@p_semester",  semester);
                    cmd.Parameters.AddWithValue("@p_user",     GetCurrentUser());
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.NextResult()) { }
                    }
                }
            }
        }
        catch { /* billing failure should not block registration */ }
    }

    // ===============================================================
    //  UPDATE EXISTING STUDENT (edit mode)
    // ===============================================================
    private void UpdateExistingStudent()
    {
        string regno = hfEditRegNo.Value;
        if (string.IsNullOrEmpty(regno))
        {
            ShowError("No student registration number found for update.");
            return;
        }

        // -- Gather form values ----------------------------------------
        string title        = ddlTitle.SelectedValue;
        // Build full name from visible fields (no JS dependency)
        string _s2 = (txtSurname.Text   ?? "").Trim().ToUpper();
        string _f2 = (txtFirstName.Text ?? "").Trim().ToUpper();
        string _o2 = (txtOtherName.Text ?? "").Trim().ToUpper();
        string fullName = string.Join(" ", new[] { _s2, _f2, _o2 }
            .Where(s => !string.IsNullOrEmpty(s))).Trim();
        if (!string.IsNullOrEmpty(fullName)) txtFullName.Text = fullName;
        string gender       = ddlGender.SelectedValue;
        string dobStr       = (txtDOB.Text ?? "").Trim();
        string phone        = (txtPhone.Text ?? "").Trim();
        string email        = (txtEmail.Text ?? "").Trim();
        string nationality  = DdlOrHidden(ddlNationality, hfNationality);
        string religion     = ddlReligion.SelectedValue;
        string marital      = ddlMarital.SelectedValue;
        string disability   = (txtDisability.Text ?? "").Trim();
        string nationalId   = (txtNationalId.Text ?? "").Trim().ToUpper();

        string programme    = DdlOrHidden(ddlProgramme, hfProgramme);
        string session      = DdlOrHidden(ddlSession, hfSession);
        string campus       = DdlOrHidden(ddlCampus, hfCampus);
        string entryMethod  = ddlEntryMethod.SelectedValue;
        string entryYear    = DdlOrHidden(ddlEntryYear, hfEntryYear);
        string intake       = ddlIntake.SelectedValue;
        string billing      = DdlOrHidden(ddlBilling, hfBilling);
        string specIdStr    = DdlOrHidden(ddlSpecialisation, hfSpecialisation);

        string address      = (txtAddress.Text ?? "").Trim();
        string postBox      = (txtPostBox.Text ?? "").Trim();
        string district     = (txtDistrict.Text ?? "").Trim();
        string resCountry   = (txtResCountry.Text ?? "").Trim();

        string sponsor        = (txtSponsor.Text ?? "").Trim();
        string sponsorContact = (txtSponsorContact.Text ?? "").Trim();
        string kinName        = (txtKinName.Text ?? "").Trim();
        string kinRelation    = ddlKinRelation.SelectedValue;
        string kinContact     = (txtKinContact.Text ?? "").Trim();
        string refereeName2   = (txtRefereeName.Text    ?? "").Trim();
        string refereeContact2= (txtRefereeContact.Text ?? "").Trim();

        string oLevelSchool  = (txtOLevelSchool.Text  ?? "").Trim();
        string oLevelIndex   = (txtOLevelIndex.Text   ?? "").Trim();
        string oLevelYear2   = (txtOLevelYear.Text    ?? "").Trim();
        string oLevelAgg2    = (txtOLevelAgg.Text     ?? "").Trim();
        string aLevelSchool  = (txtALevelSchool.Text  ?? "").Trim();
        string aLevelIndex   = (txtALevelIndex.Text   ?? "").Trim();
        string aLevelYear2   = (txtALevelYear.Text    ?? "").Trim();
        string aLevelPoints2 = (txtALevelPoints.Text  ?? "").Trim();
        string otherInst2    = (txtOtherInst.Text     ?? "").Trim().ToUpper();
        string otherQual2    = (txtOtherQual.Text     ?? "").Trim().ToUpper();
        string otherYear2    = (txtOtherYear.Text     ?? "").Trim();
        string otherGrade2   = (txtOtherGrade.Text    ?? "").Trim().ToUpper();

        // -- Validation ---------------------------------------------
        if (string.IsNullOrEmpty(fullName))
        { ShowError("Please enter the student's full name."); return; }
        if (string.IsNullOrEmpty(programme))
        { ShowError("Please select a programme."); return; }
        if (string.IsNullOrEmpty(phone))
        { ShowError("Please enter the student's phone number."); return; }
        if (string.IsNullOrEmpty(session))
        { ShowError("Please select a study session."); return; }
        if (string.IsNullOrEmpty(campus))
        { ShowError("Please select a campus."); return; }

        // Parse DOB
        DateTime birthDate = new DateTime(1980, 1, 1);
        if (!string.IsNullOrEmpty(dobStr))
        {
            DateTime parsed;
            if (DateTime.TryParse(dobStr, out parsed))
                birthDate = parsed;
        }

        if (string.IsNullOrEmpty(nationality)) nationality = "UGANDAN";
        if (string.IsNullOrEmpty(district))    district = "UGANDA";

        // Split full name into firstname + othername
        // Convention: first token = firstname, remainder = othername
        string firstName = fullName;
        string otherName = "";
        int spaceIdx = fullName.IndexOf(' ');
        if (spaceIdx > 0)
        {
            firstName = fullName.Substring(0, spaceIdx).Trim();
            otherName = fullName.Substring(spaceIdx + 1).Trim();
        }

        // Gender mapping: form sends M/F, DB stores MALE/FEMALE
        string genderDb = "MALE";
        if (gender == "F") genderDb = "FEMALE";
        else if (gender == "OTHER") genderDb = "OTHER";

        // Store the spec_id (not the name) so the JOIN in listing pages works
        int specId = SafeInt(specIdStr, 0);
        string specName = specId > 0 ? specIdStr : "";

        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                // -- Update acad_student ---------------------------
                const string updateStudentSql = @"
                    UPDATE acad_student SET
                        firstname       = @firstName,
                        othername       = @otherName,
                        gender          = @gender,
                        dob             = @dob,
                        nationality     = @nationality,
                        national_id     = @nationalId,
                        religion        = @religion,
                        entrymethod     = @entryMethod,
                        progid          = @programme,
                        studPhone       = @phone,
                        email           = @email,
                        entryyear       = @entryYear,
                        studsesion      = @session,
                        home_dist       = @district,
                        intake          = @intake,
                        specialisation  = @specName,
                        studCampus      = @campus,
                        billingID       = @billingId
                    WHERE regno = @regno";

                using (var cmd = new MySqlCommand(updateStudentSql, conn))
                {
                    cmd.Parameters.AddWithValue("@firstName",   firstName);
                    cmd.Parameters.AddWithValue("@otherName",   otherName);
                    cmd.Parameters.AddWithValue("@gender",      genderDb);
                    cmd.Parameters.AddWithValue("@dob",         birthDate);
                    cmd.Parameters.AddWithValue("@nationality", nationality);
                    cmd.Parameters.AddWithValue("@nationalId",  nationalId);
                    cmd.Parameters.AddWithValue("@religion",    religion);
                    cmd.Parameters.AddWithValue("@entryMethod", entryMethod);
                    cmd.Parameters.AddWithValue("@programme",   programme);
                    cmd.Parameters.AddWithValue("@phone",       phone);
                    cmd.Parameters.AddWithValue("@email",       email);
                    cmd.Parameters.AddWithValue("@entryYear",   SafeInt(entryYear, DateTime.Now.Year));
                    cmd.Parameters.AddWithValue("@session",     session);
                    cmd.Parameters.AddWithValue("@district",    district);
                    cmd.Parameters.AddWithValue("@intake",      intake);
                    cmd.Parameters.AddWithValue("@specName",    specName);
                    cmd.Parameters.AddWithValue("@campus",      SafeInt(campus, 1));
                    cmd.Parameters.AddWithValue("@billingId",   SafeInt(billing, 1));
                    cmd.Parameters.AddWithValue("@regno",       regno);
                    cmd.ExecuteNonQuery();
                }

                // -- Get the student's entry number ---------------
                string entryNo = "";
                using (var cmd = new MySqlCommand(
                    "SELECT entryno FROM acad_student WHERE regno=@r LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    object val = cmd.ExecuteScalar();
                    if (val != null && val != DBNull.Value)
                        entryNo = val.ToString();
                }

                // -- Update acad_applications (if record exists) --
                if (!string.IsNullOrEmpty(entryNo) && entryNo != "-")
                {
                    // Check if application record exists
                    bool appExists = false;
                    using (var cmd = new MySqlCommand(
                        "SELECT COUNT(*) FROM acad_applications WHERE stud_entry_no=@eno", conn))
                    {
                        cmd.Parameters.AddWithValue("@eno", entryNo);
                        appExists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }

                    if (appExists)
                    {
                        const string updateAppSql = @"
                            UPDATE acad_applications SET
                                stud_name          = @name,
                                stud_sex           = @sex,
                                stud_nationality   = @nationality,
                                stud_religion      = @religion,
                                stud_entry_method  = @entryMethod,
                                stud_sponsor       = @sponsor,
                                sponsor_contact    = @sponsorContact,
                                stud_birthdate     = @dob,
                                stud_phone         = @phone,
                                next_kin           = @kinName,
                                stud_phy_address   = @address,
                                stud_email         = @email,
                                stud_mar_stat      = @marital,
                                stud_campus        = @campus,
                                residence_country  = @resCountry,
                                olevel_school      = @oLevelSchool,
                                olevel_index       = @oLevelIndex,
                                olevel_year        = @oLevelYear,
                                olevel_agg         = @oLevelAgg,
                                alevel_school      = @aLevelSchool,
                                alevel_index       = @aLevelIndex,
                                alevel_year        = @aLevelYear,
                                alevel_points      = @aLevelPoints,
                                other_institution  = @otherInst,
                                other_qualification= @otherQual,
                                other_year         = @otherYear,
                                other_grade        = @otherGrade,
                                post_box           = @postBox,
                                home_district      = @district,
                                kin_contacts       = @kinContact,
                                kin_relationship   = @kinRelation,
                                referee_name       = @rfn,
                                referee_contacts   = @rfc,
                                stud_intake        = @intake,
                                title              = @title,
                                physicalDisability = @disability,
                                billingID          = @billingId,
                                national_id        = @nationalId
                            WHERE stud_entry_no = @eno";
                        using (var cmd = new MySqlCommand(updateAppSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@name",           fullName);
                            cmd.Parameters.AddWithValue("@sex",            gender == "OTHER" ? "O" : gender);
                            cmd.Parameters.AddWithValue("@nationality",    nationality);
                            cmd.Parameters.AddWithValue("@religion",       religion);
                            cmd.Parameters.AddWithValue("@entryMethod",    entryMethod);
                            cmd.Parameters.AddWithValue("@sponsor",        sponsor);
                            cmd.Parameters.AddWithValue("@sponsorContact", sponsorContact);
                            cmd.Parameters.AddWithValue("@dob",            birthDate);
                            cmd.Parameters.AddWithValue("@phone",          phone);
                            cmd.Parameters.AddWithValue("@kinName",        kinName);
                            cmd.Parameters.AddWithValue("@address",        address);
                            cmd.Parameters.AddWithValue("@email",          email);
                            cmd.Parameters.AddWithValue("@marital",        marital);
                            cmd.Parameters.AddWithValue("@campus",         campus);
                            cmd.Parameters.AddWithValue("@resCountry",     resCountry);
                            cmd.Parameters.AddWithValue("@oLevelSchool",   oLevelSchool);
                            cmd.Parameters.AddWithValue("@oLevelIndex",    oLevelIndex);
                            cmd.Parameters.AddWithValue("@oLevelYear",     IntOrNull(oLevelYear2));
                            cmd.Parameters.AddWithValue("@oLevelAgg",      StrOrNull(oLevelAgg2));
                            cmd.Parameters.AddWithValue("@aLevelSchool",   aLevelSchool);
                            cmd.Parameters.AddWithValue("@aLevelIndex",    aLevelIndex);
                            cmd.Parameters.AddWithValue("@aLevelYear",     IntOrNull(aLevelYear2));
                            cmd.Parameters.AddWithValue("@aLevelPoints",   StrOrNull(aLevelPoints2));
                            cmd.Parameters.AddWithValue("@otherInst",      StrOrNull(otherInst2));
                            cmd.Parameters.AddWithValue("@otherQual",      StrOrNull(otherQual2));
                            cmd.Parameters.AddWithValue("@otherYear",      IntOrNull(otherYear2));
                            cmd.Parameters.AddWithValue("@otherGrade",     StrOrNull(otherGrade2));
                            cmd.Parameters.AddWithValue("@postBox",        postBox);
                            cmd.Parameters.AddWithValue("@district",       district);
                            cmd.Parameters.AddWithValue("@kinContact",     kinContact);
                            cmd.Parameters.AddWithValue("@kinRelation",    kinRelation);
                            cmd.Parameters.AddWithValue("@rfn",            StrOrNull(refereeName2));
                            cmd.Parameters.AddWithValue("@rfc",            StrOrNull(refereeContact2));
                            cmd.Parameters.AddWithValue("@intake",         intake);
                            cmd.Parameters.AddWithValue("@title",          title);
                            cmd.Parameters.AddWithValue("@disability",     disability);
                            cmd.Parameters.AddWithValue("@billingId",      SafeInt(billing, 1));
                            cmd.Parameters.AddWithValue("@nationalId",     nationalId);
                            cmd.Parameters.AddWithValue("@eno",            entryNo);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // -- Update acad_applicant_choices specialisation --
                    if (specId > 0)
                    {
                        using (var cmd = new MySqlCommand(
                            "UPDATE acad_applicant_choices SET sub_comb=@sid, prog_id=@prog WHERE stud_entry_no=@eno LIMIT 1", conn))
                        {
                            cmd.Parameters.AddWithValue("@sid",  specId);
                            cmd.Parameters.AddWithValue("@prog", programme);
                            cmd.Parameters.AddWithValue("@eno",  entryNo);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                // -- Success - show result via JS -------------------
                ShowSuccess("Student record updated — Reg No: " + Server.HtmlEncode(regno));
            }
        }
        catch (Exception ex)
        {
            ShowError("Failed to update student: " + ex.Message);
        }
    }

    // ===============================================================
    //  HELPERS
    // ===============================================================

    /// <summary>
    /// Read selected value from dropdown, falling back to hidden field
    /// if dropdown has no selection (ViewState disabled + client-side filtering).
    /// </summary>
    private string DdlOrHidden(DropDownList ddl, HiddenField hf)
    {
        string val = ddl.SelectedValue;
        if (!string.IsNullOrEmpty(val)) return val;
        // Fallback 1: JS mirrors the DDL value into a hidden field before postback
        if (hf != null && !string.IsNullOrEmpty(hf.Value))
            return hf.Value;
        // Fallback 2: read raw posted value; ViewState-off + empty items on first
        // ProcessPostData pass can cause ASP.NET to lose the selection entirely.
        if (ddl.UniqueID != null)
        {
            string raw = Request.Form[ddl.UniqueID];
            if (!string.IsNullOrEmpty(raw)) return raw;
        }
        return "";
    }

    // Safely maps a year string (or any integer field) to int or DBNull.
    // Empty string / non-numeric / zero all become NULL — avoids MySQL
    // "Incorrect integer value: ''" errors on INT columns.
    private static object IntOrNull(string val)
    {
        int n;
        return int.TryParse((val ?? "").Trim(), out n) && n > 0
            ? (object)n
            : (object)DBNull.Value;
    }

    // For optional VARCHAR fields: empty string becomes NULL so the DB
    // can distinguish "not provided" from "cleared to empty".
    private static object StrOrNull(string val)
    {
        var s = (val ?? "").Trim();
        return s.Length > 0 ? (object)s : (object)DBNull.Value;
    }

    private void ShowError(string msg)
    {
        alertBox.Visible = true;
        alertBox.Attributes["class"] = "nsr-alert nsr-alert--err";
        litAlert.Text = Server.HtmlEncode(msg);
        RestoreDropdownSelections();
    }

    private void ShowSuccess(string msg)
    {
        alertBox.Visible = true;
        alertBox.Attributes["class"] = "nsr-alert nsr-alert--ok";
        litAlert.Text = msg;
    }

    /// <summary>
    /// After postback with ViewState off, re-select the values the user
    /// chose (captured in hidden fields by JS before the postback).
    /// </summary>
    private void RestoreDropdownSelections()
    {
        TrySelect(ddlProgramme,   hfProgramme.Value);
        TrySelect(ddlSession,     hfSession.Value);
        TrySelect(ddlCampus,      hfCampus.Value);
        TrySelect(ddlEntryYear,   hfEntryYear.Value);
        TrySelect(ddlBilling,     hfBilling.Value);
        TrySelect(ddlFaculty,     hfFaculty.Value);
        TrySelect(ddlNationality, hfNationality.Value);
        TrySelect(ddlSpecialisation, hfSpecialisation.Value);
    }

    private static void TrySelect(DropDownList ddl, string val)
    {
        if (string.IsNullOrEmpty(val)) return;
        if (ddl.Items.FindByValue(val) != null)
            ddl.SelectedValue = val;
    }

    private int SafeInt(string val, int def)
    {
        int r;
        return int.TryParse(val, out r) ? r : def;
    }

    private int SafeInt(object val, int def)
    {
        if (val == null || val == DBNull.Value) return def;
        int r;
        return int.TryParse(val.ToString(), out r) ? r : def;
    }

    private string GetCurrentUser()
    {
        if (Session["username"] != null && Session["username"].ToString().Trim() != "")
            return Session["username"].ToString().Trim();
        if (HttpContext.Current.User.Identity.IsAuthenticated)
            return HttpContext.Current.User.Identity.Name;
        return "system";
    }

    // ===============================================================
    //  DOCUMENT MANAGEMENT — shared helpers
    // ===============================================================

    // Exposed to ASPX rendering to seed the JS variable
    public string GetDocEntryNo() { return _docEntryNo; }

    // Resolves the applicant entry number for the current edit session.
    // Applicant mode (?eno=…) → directly from QS.
    // Student mode (?edit=REGNO) → look up acad_student.entryno.
    private string ComputeDocEntryNo()
    {
        string eno = (Request.QueryString["eno"] ?? "").Trim();
        if (!string.IsNullOrEmpty(eno)) return eno;

        string regno = (Request.QueryString["edit"] ?? "").Trim();
        if (string.IsNullOrEmpty(regno)) return "";

        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT COALESCE(entryno,'') FROM acad_student WHERE regno=@r LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    object v = cmd.ExecuteScalar();
                    return (v != null && v != DBNull.Value) ? v.ToString().Trim() : "";
                }
            }
        }
        catch { return ""; }
    }

    // Same logic as ComputeDocEntryNo but callable from AJAX handlers
    // (runs outside the normal page lifecycle).
    private string GetEditEntryNo()
    {
        string eno = (Request.QueryString["eno"] ?? "").Trim();
        if (!string.IsNullOrEmpty(eno)) return eno;

        string regno = (Request.QueryString["edit"] ?? "").Trim();
        if (string.IsNullOrEmpty(regno)) return "";

        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT COALESCE(entryno,'') FROM acad_student WHERE regno=@r LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    object v = cmd.ExecuteScalar();
                    return (v != null && v != DBNull.Value) ? v.ToString().Trim() : "";
                }
            }
        }
        catch { return ""; }
    }

    private bool IsStaffAuthenticated()
    {
        return Session["username"] != null &&
               !string.IsNullOrEmpty(Session["username"].ToString().Trim());
    }

    // Returns the same upload root as the portal (ApplyUploadPath or ~/apply-uploads).
    private string GetAdminUploadDir()
    {
        string cfg = ConfigurationManager.AppSettings["ApplyUploadPath"] ?? "";
        if (!string.IsNullOrEmpty(cfg) && Directory.Exists(cfg)) return cfg;
        string mapped = Server.MapPath("~/apply-uploads");
        if (!Directory.Exists(mapped)) Directory.CreateDirectory(mapped);
        return mapped;
    }

    private static readonly string[] DocValidTypes = {
        "PHOTO", "OLEVEL", "ALEVEL", "NATID",
        "PASSPORT", "TRANSCRIPT", "BIRTH_CERTIFICATE",
        "RECOMMENDATION", "MEDICAL", "OTHER"
    };

    private static string NormaliseDocType(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        string t = raw.Trim().ToUpper();
        var aliases = new Dictionary<string, string>
        {
            { "PASSPORT_PHOTO", "PHOTO" }, { "PROFILE_PHOTO", "PHOTO" },
            { "PROFILE_PIC",    "PHOTO" }, { "PICTURE",        "PHOTO" },
            { "O_LEVEL",        "OLEVEL"}, { "O-LEVEL",         "OLEVEL" },
            { "OLEVELS",        "OLEVEL"}, { "A_LEVEL",         "ALEVEL" },
            { "A-LEVEL",        "ALEVEL"}, { "ALEVELS",         "ALEVEL" },
            { "NATIONAL_ID",    "NATID" }, { "NATIONAL_IDCARD", "NATID"  },
            { "NIN",            "NATID" }, { "ID_CARD",         "NATID"  }
        };
        string mapped;
        if (aliases.TryGetValue(t, out mapped)) t = mapped;
        return t;
    }

    // Writes JSON to the response. Caller (Page_Load) calls Response.End()
    // outside any try-catch so ThreadAbortException propagates cleanly.
    private void WriteJson(string json)
    {
        Response.Clear();
        Response.ContentType = "application/json";
        Response.Write(json);
    }

    // ---------------------------------------------------------------
    //  AJAX: docs_list — returns JSON array of documents for this eno
    // ---------------------------------------------------------------
    private void HandleDocsListAjax()
    {
        if (!IsStaffAuthenticated()) { WriteJson("{\"ok\":false,\"error\":\"Not authenticated\"}"); return; }

        string eno = GetEditEntryNo();
        if (string.IsNullOrEmpty(eno)) { WriteJson("{\"ok\":false,\"error\":\"No entry number\"}"); return; }

        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT id, doc_type, original_filename, file_size_bytes, " +
                    "DATE_FORMAT(uploaded_at,'%Y-%m-%d %H:%i') AS uploaded_at " +
                    "FROM apply_documents WHERE stud_entry_no=@eno ORDER BY uploaded_at", conn))
                {
                    cmd.Parameters.AddWithValue("@eno", eno);
                    var sb = new StringBuilder("[");
                    int n = 0;
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            if (n++ > 0) sb.Append(",");
                            sb.AppendFormat(
                                "{{\"id\":{0},\"doc_type\":\"{1}\",\"filename\":\"{2}\",\"size\":{3},\"uploaded_at\":\"{4}\"}}",
                                rdr["id"],
                                EscapeJs(rdr["doc_type"].ToString()),
                                EscapeJs(rdr["original_filename"].ToString()),
                                rdr["file_size_bytes"],
                                EscapeJs(rdr["uploaded_at"].ToString()));
                        }
                    }
                    sb.Append("]");
                    WriteJson("{\"ok\":true,\"docs\":" + sb + "}");
                }
            }
        }
        catch (Exception ex)
        {
            WriteJson("{\"ok\":false,\"error\":\"" + EscapeJs(ex.Message) + "\"}");
        }
    }

    // ---------------------------------------------------------------
    //  AJAX: upload_doc — multipart POST, mirrors portal's logic exactly
    // ---------------------------------------------------------------
    private void HandleUploadDocAjax()
    {
        if (!IsStaffAuthenticated()) { WriteJson("{\"ok\":false,\"error\":\"Not authenticated\"}"); return; }

        string eno = GetEditEntryNo();
        if (string.IsNullOrEmpty(eno)) { WriteJson("{\"ok\":false,\"error\":\"No entry number\"}"); return; }

        string docType = NormaliseDocType(Request.Form["doc_type"] ?? "");
        bool validType = false;
        foreach (string vt in DocValidTypes) if (vt == docType) { validType = true; break; }
        if (!validType) { WriteJson("{\"ok\":false,\"error\":\"Invalid document type.\"}"); return; }

        if (Request.Files.Count == 0) { WriteJson("{\"ok\":false,\"error\":\"No file received.\"}"); return; }
        HttpPostedFile file = Request.Files[0];
        if (file == null || file.ContentLength == 0) { WriteJson("{\"ok\":false,\"error\":\"File is empty.\"}"); return; }
        if (file.ContentLength > 50 * 1024 * 1024)   { WriteJson("{\"ok\":false,\"error\":\"File must not exceed 50 MB.\"}"); return; }

        string ext = Path.GetExtension(file.FileName ?? "").ToLower();
        string[] allowed = { ".jpg", ".jpeg", ".png", ".pdf" };
        bool extOk = false;
        foreach (string a in allowed) if (a == ext) { extOk = true; break; }
        if (!extOk) { WriteJson("{\"ok\":false,\"error\":\"Only JPG, PNG, and PDF files are accepted.\"}"); return; }

        // Everything from here on (disk save + DB) is in one try-catch so
        // any failure (permissions, missing table, etc.) returns JSON, not HTML.
        string storedPath = "";
        try
        {
            // Save to disk — same layout as portal: {uploadDir}/{eno}/{DOCTYPE_yyyyMMddHHmmss.ext}
            string uploadDir = GetAdminUploadDir();
            string subDir    = Path.Combine(uploadDir, eno);
            if (!Directory.Exists(subDir)) Directory.CreateDirectory(subDir);
            string safeName = docType + "_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ext;
            storedPath = Path.Combine(subDir, safeName);
            file.SaveAs(storedPath);

            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                // Non-OTHER types: one slot each — replace old file + record (same as portal)
                if (docType != "OTHER")
                {
                    using (var cmd = new MySqlCommand(
                        "SELECT stored_path FROM apply_documents WHERE stud_entry_no=@eno AND doc_type=@dt", conn))
                    {
                        cmd.Parameters.AddWithValue("@eno", eno);
                        cmd.Parameters.AddWithValue("@dt",  docType);
                        using (var rdr = cmd.ExecuteReader())
                            while (rdr.Read())
                                try { string p = rdr["stored_path"].ToString(); if (File.Exists(p)) File.Delete(p); } catch { }
                    }
                    using (var cmd = new MySqlCommand(
                        "DELETE FROM apply_documents WHERE stud_entry_no=@eno AND doc_type=@dt", conn))
                    {
                        cmd.Parameters.AddWithValue("@eno", eno);
                        cmd.Parameters.AddWithValue("@dt",  docType);
                        cmd.ExecuteNonQuery();
                    }
                }

                long newId;
                using (var cmd = new MySqlCommand(
                    "INSERT INTO apply_documents (stud_entry_no, doc_type, original_filename, stored_path, file_size_bytes, uploaded_at) " +
                    "VALUES (@eno,@dt,@fn,@sp,@sz,@now); SELECT LAST_INSERT_ID();", conn))
                {
                    cmd.Parameters.AddWithValue("@eno", eno);
                    cmd.Parameters.AddWithValue("@dt",  docType);
                    cmd.Parameters.AddWithValue("@fn",  Path.GetFileName(file.FileName));
                    cmd.Parameters.AddWithValue("@sp",  storedPath);
                    cmd.Parameters.AddWithValue("@sz",  file.ContentLength);
                    cmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
                    newId = Convert.ToInt64(cmd.ExecuteScalar());
                }

                WriteJson(string.Format(
                    "{{\"ok\":true,\"doc\":{{\"id\":{0},\"doc_type\":\"{1}\",\"filename\":\"{2}\",\"size\":{3},\"uploaded_at\":\"{4}\"}}}}",
                    newId, EscapeJs(docType), EscapeJs(Path.GetFileName(file.FileName)),
                    file.ContentLength, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm")));
            }
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrEmpty(storedPath))
                try { if (File.Exists(storedPath)) File.Delete(storedPath); } catch { }
            WriteJson("{\"ok\":false,\"error\":\"" + EscapeJs(ex.Message) + "\"}");
        }
    }

    // ---------------------------------------------------------------
    //  AJAX: del_doc — delete one document by id (verifies ownership)
    // ---------------------------------------------------------------
    private void HandleDeleteDocAjax()
    {
        if (!IsStaffAuthenticated()) { WriteJson("{\"ok\":false,\"error\":\"Not authenticated\"}"); return; }

        int docId = SafeInt(Request.QueryString["id"] ?? "", 0);
        string eno = GetEditEntryNo();
        if (docId <= 0 || string.IsNullOrEmpty(eno))
        { WriteJson("{\"ok\":false,\"error\":\"Missing id or entry number.\"}"); return; }

        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string storedPath = "", origName = "";
                using (var cmd = new MySqlCommand(
                    "SELECT stored_path, original_filename FROM apply_documents WHERE id=@id AND stud_entry_no=@eno LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@id",  docId);
                    cmd.Parameters.AddWithValue("@eno", eno);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read())
                        { WriteJson("{\"ok\":false,\"error\":\"Document not found.\"}"); return; }
                        storedPath = rdr["stored_path"].ToString();
                        origName   = rdr["original_filename"].ToString();
                    }
                }
                // Resolve path using multi-root strategy, then delete
                string resolved = ResolveDocPath(storedPath, eno, origName);
                try { if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved)) File.Delete(resolved); } catch { }
                using (var cmd = new MySqlCommand("DELETE FROM apply_documents WHERE id=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", docId);
                    cmd.ExecuteNonQuery();
                }
            }
            WriteJson("{\"ok\":true}");
        }
        catch (Exception ex)
        {
            WriteJson("{\"ok\":false,\"error\":\"" + EscapeJs(ex.Message) + "\"}");
        }
    }

    // ---------------------------------------------------------------
    //  AJAX: view_doc — serve the file inline (staff session required)
    // ---------------------------------------------------------------
    private void HandleViewDocAjax()
    {
        // Always bypass IIS custom error pages so our message reaches the browser.
        Response.TrySkipIisCustomErrors = true;

        if (!IsStaffAuthenticated()) { Response.StatusCode = 403; return; }

        int docId = SafeInt(Request.QueryString["id"] ?? "", 0);
        string eno = GetEditEntryNo();
        if (docId <= 0 || string.IsNullOrEmpty(eno))
        {
            Response.StatusCode = 400;
            Response.ContentType = "text/plain";
            Response.Write("Bad request: missing id or entry number.");
            return;
        }

        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                string storedPath = "", fileName = "";

                // Look up the document — ownership check (id + eno) included.
                // Portal may store stud_entry_no in any of the standard formats so
                // we also try a case-insensitive fallback without the ownership check
                // in case the eno casing differs between portal and admin data.
                using (var cmd = new MySqlCommand(
                    "SELECT stored_path, original_filename, stud_entry_no " +
                    "FROM apply_documents WHERE id=@id LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@id", docId);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read())
                        {
                            Response.StatusCode = 404;
                            Response.ContentType = "text/plain";
                            Response.Write("Document record not found (id=" + docId + ").");
                            return;
                        }
                        // Verify ownership (case-insensitive)
                        string dbEno = rdr["stud_entry_no"].ToString().Trim();
                        if (!string.Equals(dbEno, eno, StringComparison.OrdinalIgnoreCase))
                        {
                            Response.StatusCode = 403;
                            Response.ContentType = "text/plain";
                            Response.Write("Access denied: document does not belong to this application.");
                            return;
                        }
                        storedPath = rdr["stored_path"].ToString().Trim();
                        fileName   = rdr["original_filename"].ToString().Trim();
                    }
                }

                // Resolve the physical file across all known upload roots.
                string resolvedPath = ResolveDocPath(storedPath, eno, fileName);

                // If the file can't be found on disk, proxy it via the portal's HTTP URL.
                // This covers two cases:
                //   • Relative stored path (legacy upload before ApplyUploadPath was set)
                //   • Absolute path under PortalUploadRoot that the admin app pool
                //     cannot read due to IIS directory ACL restrictions
                // IIS static-file module serves the request under the portal's own app-pool
                // identity, so admin file-system permissions are irrelevant.
                byte[] proxyBytes = null;
                if (string.IsNullOrEmpty(resolvedPath))
                    proxyBytes = TryFetchDocViaPortalHttp(storedPath, conn, docId, eno);

                if (string.IsNullOrEmpty(resolvedPath) && proxyBytes == null)
                {
                    Response.StatusCode = 404;
                    Response.ContentType = "text/plain";
                    Response.Write(string.Format(
                        "Document file not found on disk.\r\n" +
                        "Stored path : {0}\r\n" +
                        "Upload root : {1}\r\n" +
                        "Entry no    : {2}\r\n" +
                        "File name   : {3}\r\n\r\n" +
                        "Fix options:\r\n" +
                        "  A) Grant the admin IIS application pool READ permission on\r\n" +
                        "     the portal upload directory ({4}\\uploads) then retry.\r\n" +
                        "  B) Set PORTAL_BASE_URL in admin web.config appSettings so the\r\n" +
                        "     admin can proxy legacy documents via the portal's HTTP URL.",
                        storedPath,
                        ConfigurationManager.AppSettings["ApplyUploadPath"] ?? "(not set)",
                        eno, fileName,
                        ConfigurationManager.AppSettings["PortalUploadRoot"] ?? "(PortalUploadRoot not set)"));
                    return;
                }

                // Self-heal: update DB if we found the file at a different path
                if (!string.IsNullOrEmpty(resolvedPath)
                    && !string.Equals(resolvedPath, storedPath, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using (var upd = new MySqlCommand(
                            "UPDATE apply_documents SET stored_path=@p WHERE id=@id", conn))
                        {
                            upd.Parameters.AddWithValue("@p",  resolvedPath);
                            upd.Parameters.AddWithValue("@id", docId);
                            upd.ExecuteNonQuery();
                        }
                    }
                    catch { /* non-fatal — file still served */ }
                }

                // Determine bytes and extension for the response
                byte[] bytes;
                string extForCt;
                if (proxyBytes != null)
                {
                    bytes    = proxyBytes;
                    extForCt = Path.GetExtension(storedPath).ToLowerInvariant();
                }
                else
                {
                    bytes    = File.ReadAllBytes(resolvedPath);
                    extForCt = Path.GetExtension(resolvedPath).ToLowerInvariant();
                }

                string fext = extForCt;
                string ct   = fext == ".pdf" ? "application/pdf"
                            : fext == ".png" ? "image/png"
                            : "image/jpeg";

                Response.Clear();
                Response.ContentType = ct;
                Response.AddHeader("Content-Disposition",
                    "inline; filename=\"" + fileName.Replace("\"", "'") + "\"");
                Response.Cache.SetCacheability(System.Web.HttpCacheability.Private);
                Response.Cache.SetMaxAge(TimeSpan.FromMinutes(30));
                Response.BinaryWrite(bytes);
            }
        }
        catch (UnauthorizedAccessException uae)
        {
            Response.Clear();
            Response.StatusCode  = 403;
            Response.ContentType = "text/plain";
            Response.Write("Permission denied reading document file.\r\n" +
                "Grant the admin IIS application pool READ permission on the apply-uploads folder.\r\n" +
                "Detail: " + uae.Message);
        }
        catch (Exception ex)
        {
            Response.Clear();
            Response.StatusCode  = 500;
            Response.ContentType = "text/plain";
            Response.Write("Error serving document: " + ex.Message);
        }
    }

    // ---------------------------------------------------------------
    //  AJAX: consolidate_docs
    //  Moves every apply_documents file into the single standard folder
    //  ({ApplyUploadPath}/{eno}/{filename}) and updates stored_path in DB.
    //  Safe: copies first, deletes old only after DB update succeeds.
    // ---------------------------------------------------------------
    private void HandleConsolidateDocsAjax()
    {
        Response.Clear();
        Response.ContentType = "application/json";

        if (!IsStaffAuthenticated())
        { Response.Write("{\"ok\":false,\"error\":\"Not authenticated.\"}"); return; }

        string standardRoot = ConfigurationManager.AppSettings["ApplyUploadPath"] ?? "";
        if (string.IsNullOrEmpty(standardRoot))
        { Response.Write("{\"ok\":false,\"error\":\"ApplyUploadPath is not configured in web.config.\"}"); return; }

        int moved = 0, alreadyOk = 0, notFound = 0, errCount = 0;
        var errDetails = new System.Text.StringBuilder();

        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                // Load all document records
                var docs = new List<long[]>(); // id placeholder; use strings below
                var docList = new List<Tuple<long, string, string, string>>();
                using (var cmd = new MySqlCommand(
                    "SELECT id, stud_entry_no, stored_path, original_filename " +
                    "FROM apply_documents ORDER BY id", conn))
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                        docList.Add(Tuple.Create(
                            Convert.ToInt64(rdr["id"]),
                            rdr["stud_entry_no"].ToString().Trim(),
                            rdr["stored_path"].ToString().Trim(),
                            rdr["original_filename"].ToString().Trim()));
                }

                foreach (var doc in docList)
                {
                    long   id         = doc.Item1;
                    string eno        = doc.Item2;
                    string storedPath = doc.Item3;
                    string origName   = doc.Item4;

                    // Compute target standard path
                    string fn = !string.IsNullOrEmpty(storedPath)
                        ? Path.GetFileName(storedPath) : "";
                    if (string.IsNullOrEmpty(fn)) fn = Path.GetFileName(origName ?? "");
                    if (string.IsNullOrEmpty(fn)) { errCount++; continue; }

                    string standardPath = Path.Combine(standardRoot, eno, fn);

                    // Already correct — just ensure DB matches
                    if (string.Equals(storedPath, standardPath, StringComparison.OrdinalIgnoreCase)
                        && File.Exists(standardPath))
                    { alreadyOk++; continue; }

                    // Find the actual file wherever it lives
                    string resolvedPath = ResolveDocPath(storedPath, eno, origName);
                    if (string.IsNullOrEmpty(resolvedPath))
                    { notFound++; continue; }

                    // Already at standard location (different casing in DB)
                    if (string.Equals(resolvedPath, standardPath, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            using (var u = new MySqlCommand(
                                "UPDATE apply_documents SET stored_path=@p WHERE id=@id", conn))
                            {
                                u.Parameters.AddWithValue("@p",  standardPath);
                                u.Parameters.AddWithValue("@id", id);
                                u.ExecuteNonQuery();
                            }
                            alreadyOk++;
                        }
                        catch { errCount++; }
                        continue;
                    }

                    // Move to standard location
                    try
                    {
                        string destDir = Path.Combine(standardRoot, eno);
                        if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

                        // If a file already exists at the target with a different name, add suffix
                        string targetPath = standardPath;
                        if (File.Exists(targetPath) &&
                            !string.Equals(resolvedPath, targetPath, StringComparison.OrdinalIgnoreCase))
                        {
                            string nameNoExt = Path.GetFileNameWithoutExtension(fn);
                            string ext2      = Path.GetExtension(fn);
                            targetPath = Path.Combine(destDir,
                                nameNoExt + "_" + DateTime.UtcNow.ToString("HHmmss") + ext2);
                        }

                        File.Copy(resolvedPath, targetPath, false); // don't overwrite

                        using (var u = new MySqlCommand(
                            "UPDATE apply_documents SET stored_path=@p WHERE id=@id", conn))
                        {
                            u.Parameters.AddWithValue("@p",  targetPath);
                            u.Parameters.AddWithValue("@id", id);
                            u.ExecuteNonQuery();
                        }

                        // Delete old only after DB update succeeds
                        try
                        {
                            if (!string.Equals(resolvedPath, targetPath,
                                    StringComparison.OrdinalIgnoreCase))
                                File.Delete(resolvedPath);
                        }
                        catch { /* non-fatal — file is safe in new location */ }

                        moved++;
                    }
                    catch (Exception ex)
                    {
                        errCount++;
                        if (errDetails.Length < 500)
                            errDetails.AppendFormat("id={0}: {1}; ", id, ex.Message.Replace("\"","'"));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Response.Write("{\"ok\":false,\"error\":\"" + EscapeJs(ex.Message) + "\"}");
            return;
        }

        Response.Write(string.Format(
            "{{\"ok\":true,\"moved\":{0},\"already_ok\":{1},\"not_found\":{2},\"errors\":{3}{4}}}",
            moved, alreadyOk, notFound, errCount,
            errDetails.Length > 0
                ? ",\"error_detail\":" + "\"" + EscapeJs(errDetails.ToString()) + "\""
                : ""));
    }

    // ---------------------------------------------------------------
    //  Resolve a document's physical path across all known upload roots.
    //
    //  Uses CanReadFile() instead of File.Exists() throughout:
    //  on Windows IIS, File.Exists() returns false silently when the app
    //  pool lacks directory-listing rights, even if the file is readable.
    //  File.OpenRead() is the correct gate — it only needs READ on the
    //  specific file, not enumerate-directory on the parent.
    //
    //  Handles:
    //    - Absolute paths (from admin uploads and portal API)
    //    - Path-root mismatches (different drive letter / wwwroot location
    //      between the portal server and this server — rebuild using the
    //      configured ApplyUploadPath root)
    //    - Relative paths like "uploads/applicants/{eno}/file.jpg"
    //      (stored by the portal wizard before ApplyUploadPath was set)
    // ---------------------------------------------------------------
    private string ResolveDocPath(string storedPath, string eno, string filename)
    {
        // 1. Try the stored_path exactly as recorded in the DB.
        if (!string.IsNullOrEmpty(storedPath) && Path.IsPathRooted(storedPath)
            && CanReadFile(storedPath))
            return storedPath;

        string fn = !string.IsNullOrEmpty(storedPath) ? Path.GetFileName(storedPath) : "";
        if (string.IsNullOrEmpty(fn)) fn = (filename ?? "").Trim();

        // 2. For absolute stored paths whose root differs from the configured
        //    ApplyUploadPath (e.g. path was stored on a different server / drive),
        //    rebuild by swapping the root. Example:
        //      stored:    C:\inetpub\wwwroot\apply-uploads\MRU…\file.jpg
        //      configured: D:\uploads\apply-uploads
        //    → try D:\uploads\apply-uploads\MRU…\file.jpg
        if (!string.IsNullOrEmpty(storedPath) && Path.IsPathRooted(storedPath)
            && !string.IsNullOrEmpty(fn))
        {
            string cfgRoot = ConfigurationManager.AppSettings["ApplyUploadPath"] ?? "";
            if (!string.IsNullOrEmpty(cfgRoot))
            {
                // Rebuild: configuredRoot\{eno}\filename
                string rebulit = Path.Combine(cfgRoot, eno, fn);
                if (CanReadFile(rebulit)) return rebulit;

                // Also try without the extra eno sub-folder (some older portal
                // versions stored directly under the root)
                string rebulitFlat = Path.Combine(cfgRoot, fn);
                if (CanReadFile(rebulitFlat)) return rebulitFlat;
            }

            // 2b. Admin's own MapPath-based upload dir (for admin-uploaded docs)
            try
            {
                string adminDir = Path.Combine(Server.MapPath("~/apply-uploads"), eno, fn);
                if (CanReadFile(adminDir)) return adminDir;
            }
            catch { }
        }

        // 3. Relative path (portal wizard stored "uploads/applicants/{eno}/file.jpg"
        //    before ApplyUploadPath was configured)
        if (!string.IsNullOrEmpty(storedPath) && !Path.IsPathRooted(storedPath))
        {
            string relNorm = storedPath.Replace('/', Path.DirectorySeparatorChar)
                                       .TrimStart(Path.DirectorySeparatorChar);

            // 3a. Against PortalUploadRoot config
            string portalRoot = ConfigurationManager.AppSettings["PortalUploadRoot"] ?? "";
            if (!string.IsNullOrEmpty(portalRoot))
            {
                string c = Path.Combine(portalRoot, relNorm);
                if (CanReadFile(c)) return c;
            }

            // 3b. Auto-discover: scan all sibling directories of ApplyUploadPath
            string sharedDir = ConfigurationManager.AppSettings["ApplyUploadPath"] ?? "";
            if (!string.IsNullOrEmpty(sharedDir))
            {
                string parent = Path.GetDirectoryName(sharedDir) ?? "";
                if (!string.IsNullOrEmpty(parent))
                {
                    try
                    {
                        foreach (string dir in Directory.GetDirectories(parent))
                        {
                            string c = Path.Combine(dir, relNorm);
                            if (CanReadFile(c)) return c;
                        }
                    }
                    catch { }
                }
            }
        }

        // 4. Last-resort: try all known roots with just the filename
        if (!string.IsNullOrEmpty(fn))
        {
            // 4a. ApplyUploadPath\{eno}\filename
            string sharedDir2 = ConfigurationManager.AppSettings["ApplyUploadPath"] ?? "";
            if (!string.IsNullOrEmpty(sharedDir2))
            {
                string c = Path.Combine(sharedDir2, eno, fn);
                if (CanReadFile(c)) return c;
            }

            // 4b. PortalUploadRoot + portal-style subdirectory
            string pRoot = ConfigurationManager.AppSettings["PortalUploadRoot"] ?? "";
            if (!string.IsNullOrEmpty(pRoot))
            {
                string c1 = Path.Combine(pRoot, "uploads", "applicants", eno, fn);
                if (CanReadFile(c1)) return c1;
                string c2 = Path.Combine(pRoot, "apply-uploads", eno, fn);
                if (CanReadFile(c2)) return c2;
            }

            // 4c. Admin MapPath fallback
            try
            {
                string c = Path.Combine(Server.MapPath("~/apply-uploads"), eno, fn);
                if (CanReadFile(c)) return c;
            }
            catch { }
        }

        return null; // Not found in any known location
    }

    // ---------------------------------------------------------------
    //  Proxy a portal-uploaded document via HTTP.
    //
    //  When a document was uploaded before ApplyUploadPath was set on the
    //  portal, stored_path is a relative path like
    //  "uploads/applicants/{eno}/filename.jpg".  The file lives inside the
    //  portal's own IIS site root, which the admin app pool cannot read
    //  directly (different IIS application pool identity / directory ACL).
    //
    //  Fetching via the portal's public URL works because IIS's own static-
    //  file module services the request under the portal app pool's identity,
    //  not the admin's.  Both HTTP and HTTPS variants are tried.
    //
    //  On success the method also self-heals the DB stored_path to the
    //  physical path (PortalUploadRoot + relative), so the NEXT view goes
    //  straight to disk without needing the HTTP round-trip.
    // ---------------------------------------------------------------
    private byte[] TryFetchDocViaPortalHttp(string storedPath, MySqlConnection conn, int docId, string eno)
    {
        if (string.IsNullOrEmpty(storedPath)) return null;

        // Derive the URL-relative portion from the stored path:
        //   • Relative stored path  → use as-is
        //   • Absolute path under PortalUploadRoot → strip the root prefix to get the relative part
        //     e.g. C:\inetpub\wwwroot\CampusDynamics_Portal\uploads\applicants\...\file.jpg
        //          → uploads/applicants/.../file.jpg
        string relUrl;
        if (!Path.IsPathRooted(storedPath))
        {
            relUrl = storedPath.Replace('\\', '/').TrimStart('/');
        }
        else
        {
            string pRoot = (ConfigurationManager.AppSettings["PortalUploadRoot"] ?? "")
                           .TrimEnd('\\', '/');
            if (string.IsNullOrEmpty(pRoot)
                || !storedPath.StartsWith(pRoot, StringComparison.OrdinalIgnoreCase))
                return null;   // absolute but not under portal root — don't guess
            relUrl = storedPath.Substring(pRoot.Length)
                               .TrimStart('\\', '/')
                               .Replace('\\', '/');
        }

        if (string.IsNullOrEmpty(relUrl)) return null;

        // Build candidate URLs: HTTPS first, HTTP fallback
        string portalBase = (ConfigurationManager.AppSettings["PORTAL_BASE_URL"]
                          ?? ConfigurationManager.AppSettings["PortalBaseUrl"]
                          ?? "https://eportal.mru.ac.ug").TrimEnd('/');

        string[] candidates = {
            portalBase + "/" + relUrl,
            portalBase.Replace("https://", "http://") + "/" + relUrl
        };

        // Accept any SSL certificate — this is an internal server-to-server request
        var prevCb = System.Net.ServicePointManager.ServerCertificateValidationCallback;
        System.Net.ServicePointManager.ServerCertificateValidationCallback = (s, c, ch, e) => true;
        try
        {
            foreach (string url in candidates)
            {
                try
                {
                    var req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                    req.Timeout = 10000;
                    req.AllowAutoRedirect = true;
                    byte[] data;
                    using (var resp = (System.Net.HttpWebResponse)req.GetResponse())
                    {
                        if ((int)resp.StatusCode != 200) continue;
                        using (var ms = new System.IO.MemoryStream())
                        {
                            resp.GetResponseStream().CopyTo(ms);
                            data = ms.ToArray();
                        }
                    }
                    if (data == null || data.Length == 0) continue;

                    // No self-heal here: the admin app pool cannot read the portal
                    // directory, so storing the absolute portal path in the DB would just
                    // cause ResolveDocPath to fail on the next request and fall back here
                    // again anyway.  Leave the stored_path unchanged.

                    return data;
                }
                catch { /* try next URL */ }
            }
        }
        finally
        {
            System.Net.ServicePointManager.ServerCertificateValidationCallback = prevCb;
        }
        return null;
    }

    // Returns true if the file exists AND the current IIS process can open it.
    // Unlike File.Exists(), this correctly distinguishes "file not found" from
    // "directory listing permission denied" — the latter returns false from
    // File.Exists even when the file is readable.
    private static bool CanReadFile(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        try
        {
            using (var fs = File.OpenRead(path)) { }
            return true;
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
        catch { return false; }
    }
}