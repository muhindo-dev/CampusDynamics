using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Web;
using System.Web.Configuration;
using System.Web.UI;
using System.Web.UI.WebControls;
using MySql.Data.MySqlClient;

public partial class COOPERP_NewScreens_BursaryBeneficiaries : System.Web.UI.Page
{
    private string AcctConnStr
    {
        get { return WebConfigurationManager.ConnectionStrings["accountsConnectionString"].ConnectionString; }
    }
    private string MainConnStr
    {
        get { return WebConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    private static bool _indexChecked;  // one-time migration flag

    protected void Page_Load(object sender, EventArgs e)
    {
        // AJAX endpoints
        string ajax = Request.QueryString["ajax"];
        if (ajax == "search") { HandleStudentSearch(); return; }
        if (ajax == "getreg") { HandleGetRegistration(); return; }
        if (ajax == "getschemes") { HandleGetSchemes(); return; }
        if (ajax == "calcfees") { HandleCalcFees(); return; }

        EnsureFeeTrackingIndex();

        txtEditAmount.Attributes["type"] = "number";
        txtEditAmount.Attributes["min"] = "0";
        txtEditAmount.Attributes["step"] = "1";
        txtEditTxRef.Attributes["type"] = "number";
        txtEditTxRef.Attributes["min"] = "0";

        LoadLookups();

        // Restore filter dropdowns (ViewState disabled on master page)
        RestorePostedValue(ddlScheme);
        RestorePostedValue(ddlAcadYear);
        RestorePostedValue(ddlSemester);
        RestorePostedValue(ddlPageSize);

        // Restore modal dropdowns
        RestorePostedValue(ddlAddScheme);
        RestorePostedValue(ddlAddAcadYear);
        RestorePostedValue(ddlAddSemester);
        RestorePostedValue(ddlEditScheme);
        RestorePostedValue(ddlEditAcadYear);
        RestorePostedValue(ddlEditSemester);

        // Check query string for scheme pre-filter
        if (!IsPostBack)
        {
            string schemeQs = Request.QueryString["scheme"];
            if (!string.IsNullOrEmpty(schemeQs))
            {
                ListItem li = ddlScheme.Items.FindByValue(schemeQs);
                if (li != null) { ddlScheme.ClearSelection(); li.Selected = true; }
            }
        }

        LoadBeneficiaries();
    }

    // ====================================================================
    // Auto-migration: fix broken Index_UNQ on fin_studentfeestracking
    // The original Index_UNQ was on (regno, semester, acadyear) only —
    // far too restrictive. Bursaries, waivers, and multiple bill items
    // all share the same student+semester+year. Drop the UNIQUE and
    // replace with a non-unique performance index.
    // ====================================================================
    private void EnsureFeeTrackingIndex()
    {
        if (_indexChecked) return;
        _indexChecked = true;
        try
        {
            using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                // Check if Index_UNQ exists
                bool hasUNQ = false;
                using (MySqlCommand cmd = new MySqlCommand(
                    "SHOW INDEX FROM fin_studentfeestracking WHERE Key_name = 'Index_UNQ'", conn))
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    hasUNQ = rdr.HasRows;
                }
                if (hasUNQ)
                {
                    // Drop the broken unique index
                    using (MySqlCommand cmd = new MySqlCommand(
                        "ALTER TABLE fin_studentfeestracking DROP INDEX Index_UNQ", conn))
                    { cmd.ExecuteNonQuery(); }
                    // Add a non-unique performance index in its place
                    using (MySqlCommand cmd = new MySqlCommand(
                        @"ALTER TABLE fin_studentfeestracking
                          ADD INDEX idx_student_sem_year (regno, acadyear, semester, item_code, trans_type)", conn))
                    { cmd.ExecuteNonQuery(); }
                }
            }
        }
        catch { /* non-critical migration */ }
    }

    // ====================================================================
    // AJAX Student Search
    // ====================================================================
    private void HandleStudentSearch()
    {
        Response.ContentType = "application/json";
        string q = (Request.QueryString["q"] ?? "").Trim();
        if (q.Length < 2) { Response.Write("[]"); Response.End(); return; }

        var sb = new StringBuilder("[");
        using (MySqlConnection conn = new MySqlConnection(MainConnStr))
        {
            conn.Open();
            string sql = @"SELECT s.regno, CONCAT(s.firstname,' ',s.othername) AS student_name, 
                                  IFNULL(p.progname,'') AS programme
                           FROM acad_student s
                           LEFT JOIN acad_programme p ON s.progid = p.progcode
                           WHERE s.regno LIKE @q OR CONCAT(s.firstname,' ',s.othername) LIKE @q
                           LIMIT 15";
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@q", "%" + q + "%");
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    bool first = true;
                    while (rdr.Read())
                    {
                        if (!first) sb.Append(",");
                        first = false;
                        sb.AppendFormat("{{\"regno\":\"{0}\",\"name\":\"{1}\",\"programme\":\"{2}\"}}",
                            JsEsc(rdr["regno"].ToString()),
                            JsEsc(rdr["student_name"].ToString()),
                            JsEsc(rdr["programme"].ToString()));
                    }
                }
            }
        }
        sb.Append("]");
        Response.Write(sb.ToString());
        Response.End();
    }

    // ====================================================================
    // AJAX Get Registration
    // ====================================================================
    private void HandleGetRegistration()
    {
        Response.ContentType = "application/json";
        string reg = (Request.QueryString["r"] ?? "").Trim();
        if (string.IsNullOrEmpty(reg)) { Response.Write("{\"found\":false,\"regs\":[]}"); Response.End(); return; }

        using (MySqlConnection conn = new MySqlConnection(MainConnStr))
        {
            conn.Open();
            // Return ALL registrations so admin can pick one
            string sql = @"SELECT r.acad_year, r.semester, r.studyyear, r.regstatus,
                                  CONCAT(s.firstname,' ',s.othername) AS student_name,
                                  IFNULL(p.progname,'') AS programme,
                                  IFNULL(s.progid,'') AS progcode
                           FROM acad_registration r
                           JOIN acad_student s ON s.regno = r.regno
                           LEFT JOIN acad_programme p ON s.progid = p.progcode
                           WHERE LOWER(r.regno) = LOWER(@r)
                           ORDER BY r.acad_year DESC, r.semester DESC
                           LIMIT 20";
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@r", reg);
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    var sb = new StringBuilder();
                    sb.Append("{\"found\":true,\"regs\":[");
                    bool any = false;
                    bool first = true;
                    string name = "";
                    string programme = "";
                    while (rdr.Read())
                    {
                        any = true;
                        if (!first) sb.Append(",");
                        first = false;
                        name = rdr["student_name"].ToString();
                        programme = rdr["programme"].ToString();
                        sb.AppendFormat(
                            "{{\"acad_year\":\"{0}\",\"semester\":{1},\"study_year\":{2},\"regstatus\":\"{3}\"}}",
                            JsEsc(rdr["acad_year"].ToString()),
                            rdr["semester"],
                            rdr["studyyear"],
                            JsEsc(rdr["regstatus"].ToString()));
                    }
                    sb.Append("]");
                    if (any)
                    {
                        sb.AppendFormat(",\"name\":\"{0}\",\"programme\":\"{1}\"",
                            JsEsc(name), JsEsc(programme));
                    }
                    else
                    {
                        sb.Clear();
                        sb.Append("{\"found\":false,\"regs\":[]");
                    }
                    sb.Append("}");
                    Response.Write(sb.ToString());
                }
            }
        }
        Response.End();
    }

    // ====================================================================
    // AJAX Get Schemes — returns all active schemes with type info
    // ====================================================================
    private void HandleGetSchemes()
    {
        Response.ContentType = "application/json";
        var sb = new StringBuilder("[");
        using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();
            string sql = @"SELECT scholarshipID, scholarshipName, bursary_amount,
                                  IFNULL(scheme_type,'FIXED') AS scheme_type,
                                  IFNULL(scheme_value,0) AS scheme_value
                           FROM scholarships WHERE status='Active'
                           ORDER BY scholarshipName";
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            using (MySqlDataReader rdr = cmd.ExecuteReader())
            {
                bool first = true;
                while (rdr.Read())
                {
                    if (!first) sb.Append(",");
                    first = false;
                    double amt = rdr["bursary_amount"] != DBNull.Value ? Convert.ToDouble(rdr["bursary_amount"]) : 0;
                    double val = rdr["scheme_value"] != DBNull.Value ? Convert.ToDouble(rdr["scheme_value"]) : 0;
                    sb.AppendFormat("{{\"id\":{0},\"name\":\"{1}\",\"amount\":{2},\"type\":\"{3}\",\"value\":{4}}}",
                        rdr["scholarshipID"],
                        JsEsc(rdr["scholarshipName"].ToString()),
                        amt,
                        JsEsc(rdr["scheme_type"].ToString()),
                        val);
                }
            }
        }
        sb.Append("]");
        Response.Write(sb.ToString());
        Response.End();
    }

    // ====================================================================
    // AJAX Calculate Fees — returns tuition+functional for student's programme
    // Expects: r (regno), y (acad_year), s (semester)
    // ====================================================================
    private void HandleCalcFees()
    {
        Response.ContentType = "application/json";
        string regNo = (Request.QueryString["r"] ?? "").Trim();
        string acadYear = (Request.QueryString["y"] ?? "").Trim();
        string semStr = (Request.QueryString["s"] ?? "").Trim();

        if (string.IsNullOrEmpty(regNo) || string.IsNullOrEmpty(acadYear) || string.IsNullOrEmpty(semStr))
        {
            Response.Write("{\"ok\":false,\"msg\":\"Missing parameters\"}");
            Response.End();
            return;
        }

        int semester;
        if (!int.TryParse(semStr, out semester) || semester < 1 || semester > 3)
        {
            Response.Write("{\"ok\":false,\"msg\":\"Invalid semester\"}");
            Response.End();
            return;
        }

        try
        {
            // 1. Get student's progid and determine study year
            string progCode = "";
            int studyYear = 1;
            using (MySqlConnection conn = new MySqlConnection(MainConnStr))
            {
                conn.Open();
                // Get programme
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT progid FROM acad_student WHERE regno=@r LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@r", regNo);
                    object val = cmd.ExecuteScalar();
                    if (val == null || val == DBNull.Value)
                    {
                        Response.Write("{\"ok\":false,\"msg\":\"Student not found\"}");
                        Response.End();
                        return;
                    }
                    progCode = val.ToString();
                }
                // Get study year from registration
                using (MySqlCommand cmd = new MySqlCommand(
                    @"SELECT studyyear FROM acad_registration
                      WHERE LOWER(regno)=LOWER(@r) AND acad_year=@y AND semester=@s
                      LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@r", regNo);
                    cmd.Parameters.AddWithValue("@y", acadYear);
                    cmd.Parameters.AddWithValue("@s", semester);
                    object val = cmd.ExecuteScalar();
                    if (val != null && val != DBNull.Value)
                    {
                        int.TryParse(val.ToString(), out studyYear);
                        if (studyYear < 1) studyYear = 1;
                        if (studyYear > 3) studyYear = 3;
                    }
                }
            }

            // 2. Query fin_programme_fees for the programme
            if (string.IsNullOrEmpty(progCode))
            {
                Response.Write("{\"ok\":false,\"msg\":\"Student has no programme assigned\"}");
                Response.End();
                return;
            }

            // Build dynamic column names: y{studyYear}_s{semester}_tuition, y{studyYear}_s{semester}_functional
            string colTuition = string.Format("y{0}_s{1}_tuition", studyYear, semester);
            string colFunctional = string.Format("y{0}_s{1}_functional", studyYear, semester);

            using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                // Resolved by the student's own study session (fin_ProgFeeRow). A programme may
                // hold both a MAIN and an INSERVICE structure, and "WHERE progcode=X LIMIT 1"
                // would pick an arbitrary one — sizing a bursary against the wrong fee.
                string sql = string.Format(
                    "SELECT `{0}`, `{1}` FROM fin_programme_fees " +
                    "WHERE ID = fin_ProgFeeRow(@prog, (SELECT s.studsesion " +
                    "                                  FROM campus_dynamics.acad_student s " +
                    "                                  WHERE s.regno=@feeReg LIMIT 1))",
                    colTuition, colFunctional);
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@prog", progCode);
                    cmd.Parameters.AddWithValue("@feeReg", regNo);
                    using (MySqlDataReader rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            double tuition = rdr[colTuition] != DBNull.Value ? Convert.ToDouble(rdr[colTuition]) : 0;
                            double functional = rdr[colFunctional] != DBNull.Value ? Convert.ToDouble(rdr[colFunctional]) : 0;
                            double total = tuition + functional;
                            Response.Write(string.Format(
                                "{{\"ok\":true,\"tuition\":{0},\"functional\":{1},\"total\":{2},\"study_year\":{3},\"programme\":\"{4}\"}}",
                                tuition, functional, total, studyYear, JsEsc(progCode)));
                        }
                        else
                        {
                            Response.Write(string.Format(
                                "{{\"ok\":false,\"msg\":\"No fee structure found for programme {0} (Year {1}, Sem {2})\"}}", 
                                JsEsc(progCode), studyYear, semester));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Response.Write("{\"ok\":false,\"msg\":\"" + JsEsc(ex.Message) + "\"}");
        }
        Response.End();
    }

    // ====================================================================
    // Load Lookups
    // ====================================================================
    private void LoadLookups()
    {
        // Schemes dropdown (for filter and modals)
        using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();

            DataTable dtSchemes = new DataTable();
            using (MySqlCommand cmd = new MySqlCommand(
                @"SELECT scholarshipID, scholarshipName, bursary_amount,
                         IFNULL(scheme_type,'FIXED') AS scheme_type,
                         IFNULL(scheme_value,0) AS scheme_value
                  FROM scholarships ORDER BY scholarshipName", conn))
            using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) { da.Fill(dtSchemes); }

            // Filter dropdown
            ddlScheme.Items.Clear();
            ddlScheme.Items.Add(new ListItem("All Schemes", ""));
            foreach (DataRow r in dtSchemes.Rows)
                ddlScheme.Items.Add(new ListItem(r["scholarshipName"].ToString(), r["scholarshipID"].ToString()));

            // Add modal dropdown
            ddlAddScheme.Items.Clear();
            ddlAddScheme.Items.Add(new ListItem("-- Select Scheme --", ""));
            foreach (DataRow r in dtSchemes.Rows)
            {
                string stype = r["scheme_type"].ToString();
                double schAmt = r["bursary_amount"] != DBNull.Value ? Convert.ToDouble(r["bursary_amount"]) : 0;
                double schVal = r["scheme_value"] != DBNull.Value ? Convert.ToDouble(r["scheme_value"]) : 0;
                string suffix = "";
                if (stype == "PERCENTAGE") suffix = " — " + schVal.ToString("0.##") + "% of fees";
                else if (stype == "CUSTOM") suffix = " — Custom amount";
                else suffix = " — UGX " + schAmt.ToString("N0");
                ddlAddScheme.Items.Add(new ListItem(
                    r["scholarshipName"].ToString() + suffix,
                    r["scholarshipID"].ToString()));
            }

            // Edit modal dropdown
            ddlEditScheme.Items.Clear();
            foreach (DataRow r in dtSchemes.Rows)
            {
                double schAmt = r["bursary_amount"] != DBNull.Value ? Convert.ToDouble(r["bursary_amount"]) : 0;
                ddlEditScheme.Items.Add(new ListItem(
                    r["scholarshipName"].ToString() + " — UGX " + schAmt.ToString("N0"),
                    r["scholarshipID"].ToString()));
            }

            // Store scheme data as JSON for JS (includes type + value)
            var sbData = new StringBuilder("{");
            bool firstScheme = true;
            foreach (DataRow r in dtSchemes.Rows)
            {
                if (!firstScheme) sbData.Append(",");
                firstScheme = false;
                double schAmt = r["bursary_amount"] != DBNull.Value ? Convert.ToDouble(r["bursary_amount"]) : 0;
                double schVal = r["scheme_value"] != DBNull.Value ? Convert.ToDouble(r["scheme_value"]) : 0;
                string stype = r["scheme_type"].ToString();
                sbData.AppendFormat("\"{0}\":{{\"amount\":{1},\"type\":\"{2}\",\"value\":{3},\"name\":\"{4}\"}}",
                    r["scholarshipID"], schAmt, JsEsc(stype), schVal, JsEsc(r["scholarshipName"].ToString()));
            }
            sbData.Append("}");
            ScriptManager.RegisterStartupScript(this, GetType(), "schemeData",
                "var _schemeData = " + sbData.ToString() + ";" +
                "var _schemeAmounts = (function(d){var a={};for(var k in d)a[k]=d[k].amount;return a;})(_schemeData);", true);

            // Academic years
            DataTable dtYears = new DataTable();
            using (MySqlCommand cmd = new MySqlCommand(
                @"SELECT DISTINCT acadyear FROM fin_studentfeestracking WHERE acadyear<>'' 
                  UNION SELECT DISTINCT scholarhipYear FROM scholarshipstudents WHERE scholarhipYear<>''
                  ORDER BY acadyear DESC", conn))
            using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) { da.Fill(dtYears); }

            ddlAcadYear.Items.Clear();
            ddlAcadYear.Items.Add(new ListItem("All Years", ""));
            ddlAddAcadYear.Items.Clear();
            ddlEditAcadYear.Items.Clear();
            foreach (DataRow r in dtYears.Rows)
            {
                string yr = r[0].ToString();
                ddlAcadYear.Items.Add(new ListItem(yr, yr));
                ddlAddAcadYear.Items.Add(new ListItem(yr, yr));
                ddlEditAcadYear.Items.Add(new ListItem(yr, yr));
            }
        }
    }

    private void RestorePostedValue(DropDownList ddl)
    {
        string posted = Request.Form[ddl.UniqueID];
        if (!string.IsNullOrEmpty(posted))
        {
            ListItem li = ddl.Items.FindByValue(posted);
            if (li != null) { ddl.ClearSelection(); li.Selected = true; }
        }
    }

    // ====================================================================
    // Load Beneficiaries
    // ====================================================================
    private void LoadBeneficiaries()
    {
        string search = txtSearch.Text.Trim();
        string schemeFilter = ddlScheme.SelectedValue;
        string yearFilter = ddlAcadYear.SelectedValue;
        string semFilter = ddlSemester.SelectedValue;

        int pageSize = 50;
        int.TryParse(ddlPageSize.SelectedValue, out pageSize);
        if (pageSize < 1) pageSize = 50;

        using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();

            // Stats query (unfiltered)
            using (MySqlCommand cmdStats = new MySqlCommand(
                @"SELECT COUNT(*) AS total,
                         IFNULL(SUM(ss.amount_offered),0) AS total_amount
                  FROM scholarshipstudents ss", conn))
            {
                using (MySqlDataReader rdr = cmdStats.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        litTotal.Text = rdr["total"].ToString();
                        double totalAmt = Convert.ToDouble(rdr["total_amount"]);
                        litTotalAmount.Text = "UGX " + totalAmt.ToString("N0");
                    }
                }
            }

            // Filtered data query
            var where = new List<string>();
            var parms = new List<MySqlParameter>();

            if (!string.IsNullOrEmpty(schemeFilter))
            {
                where.Add("ss.scholarshipID = @scheme");
                parms.Add(new MySqlParameter("@scheme", int.Parse(schemeFilter)));
            }
            if (!string.IsNullOrEmpty(yearFilter))
            {
                where.Add("ss.scholarhipYear = @year");
                parms.Add(new MySqlParameter("@year", yearFilter));
            }
            if (!string.IsNullOrEmpty(semFilter))
            {
                where.Add("ss.scholarhipTerm = @sem");
                parms.Add(new MySqlParameter("@sem", int.Parse(semFilter)));
            }
            if (!string.IsNullOrEmpty(search))
            {
                where.Add("(ss.adm_no LIKE @search OR s.scholarshipName LIKE @search OR CONCAT(st.firstname,' ',st.othername) LIKE @search)");
                parms.Add(new MySqlParameter("@search", "%" + search + "%"));
            }

            string whereClause = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "";

            string sql = @"SELECT ss.stid, ss.adm_no, ss.scholarshipID, ss.scholarhipTerm, ss.scholarhipYear,
                                  ss.amountDue, ss.amount_offered, ss.transaction_ref, ss.status, ss.date_added, ss.notes,
                                  s.scholarshipName,
                                  IFNULL(CONCAT(st.firstname,' ',st.othername),'—') AS student_name,
                                  GREATEST(1, CAST(SUBSTRING(ss.scholarhipYear,1,4) AS SIGNED) - CAST(SUBSTRING(ss.adm_no,4,4) AS SIGNED) + 1) AS study_year
                           FROM scholarshipstudents ss
                           LEFT JOIN scholarships s ON s.scholarshipID = ss.scholarshipID
                           LEFT JOIN campus_dynamics.acad_student st ON st.regno = ss.adm_no"
                         + whereClause + " ORDER BY ss.date_added DESC LIMIT @limit";

            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                foreach (var p in parms) cmd.Parameters.Add(p);
                cmd.Parameters.AddWithValue("@limit", pageSize);

                DataTable dt = new DataTable();
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) { da.Fill(dt); }

                gvBeneficiaries.DataSource = dt;
                gvBeneficiaries.SettingsPager.PageSize = pageSize;
                gvBeneficiaries.DataBind();

                lblRecordCount.Text = dt.Rows.Count + " record" + (dt.Rows.Count != 1 ? "s" : "");
                litFooter.Text = string.Format("Showing <strong>{0}</strong> beneficiar{1}",
                    dt.Rows.Count, dt.Rows.Count != 1 ? "ies" : "y");

                // Filter info label
                var filters = new List<string>();
                if (!string.IsNullOrEmpty(schemeFilter)) filters.Add("Scheme: " + (ddlScheme.SelectedItem != null ? ddlScheme.SelectedItem.Text : schemeFilter));
                if (!string.IsNullOrEmpty(yearFilter)) filters.Add(yearFilter);
                if (!string.IsNullOrEmpty(semFilter)) filters.Add("Sem " + semFilter);
                lblFilterInfo.Text = filters.Count > 0 ? string.Join(" | ", filters) : "";
            }
        }
    }

    // ====================================================================
    // Add Beneficiary
    // ====================================================================
    protected void btnSaveBeneficiary_Click(object sender, EventArgs e)
    {
        RestorePostedValue(ddlAddScheme);
        RestorePostedValue(ddlAddAcadYear);
        RestorePostedValue(ddlAddSemester);

        string regNo = hfAddRegNo.Value.Trim();
        if (string.IsNullOrEmpty(regNo)) regNo = txtAddRegNo.Text.Trim();
        string schemeVal = ddlAddScheme.SelectedValue;
        string yearVal   = ddlAddAcadYear.SelectedValue;
        string semVal    = ddlAddSemester.SelectedValue;
        string notes     = txtAddNotes.Text.Trim();

        // Validation
        if (string.IsNullOrEmpty(regNo))
        { ShowToast("Student Reg No is required.", false); OpenModalAfterPostback("modal-add-beneficiary"); return; }
        if (string.IsNullOrEmpty(schemeVal))
        { ShowToast("Bursary Scheme is required.", false); OpenModalAfterPostback("modal-add-beneficiary"); return; }
        if (string.IsNullOrEmpty(yearVal))
        { ShowToast("Academic Year is required.", false); OpenModalAfterPostback("modal-add-beneficiary"); return; }
        if (string.IsNullOrEmpty(semVal))
        { ShowToast("Semester is required.", false); OpenModalAfterPostback("modal-add-beneficiary"); return; }

        // Validate that student is registered for this academic year and semester
        try
        {
            using (MySqlConnection connReg = new MySqlConnection(MainConnStr))
            {
                connReg.Open();
                using (MySqlCommand chkReg = new MySqlCommand(
                    @"SELECT regstatus FROM acad_registration 
                      WHERE LOWER(regno) = LOWER(@r) AND acad_year = @y AND semester = @s
                      LIMIT 1", connReg))
                {
                    chkReg.Parameters.AddWithValue("@r", regNo);
                    chkReg.Parameters.AddWithValue("@y", yearVal);
                    chkReg.Parameters.AddWithValue("@s", semVal);
                    object regResult = chkReg.ExecuteScalar();
                    if (regResult == null || regResult == DBNull.Value)
                    {
                        ShowToast("Student '" + regNo + "' has no registration record for " + yearVal + " Semester " + semVal + ". Please verify.", false);
                        OpenModalAfterPostback("modal-add-beneficiary");
                        return;
                    }
                    string regStatus = regResult.ToString().Trim().ToUpper();
                    // Accept: REGISTERED, CLEARED, LATE REGISTERED, LATE-REGISTERED
                    bool validEnrolment = regStatus == "REGISTERED" || regStatus == "CLEARED"
                                      || regStatus == "LATE REGISTERED" || regStatus == "LATE-REGISTERED";
                    if (!validEnrolment)
                    {
                        ShowToast("Student '" + regNo + "' cannot receive a bursary — registration status is '" + regResult.ToString() + "' for " + yearVal + " Semester " + semVal + ".", false);
                        OpenModalAfterPostback("modal-add-beneficiary");
                        return;
                    }
                }
            }
        }
        catch (Exception exReg)
        {
            ShowToast("Registration check failed: " + Server.HtmlEncode(exReg.Message), false);
            OpenModalAfterPostback("modal-add-beneficiary");
            return;
        }

        int schemeId;
        if (!int.TryParse(schemeVal, out schemeId))
        { ShowToast("Invalid bursary scheme.", false); OpenModalAfterPostback("modal-add-beneficiary"); return; }

        int semester;
        if (!int.TryParse(semVal, out semester) || semester < 1 || semester > 3)
        { ShowToast("Invalid semester.", false); OpenModalAfterPostback("modal-add-beneficiary"); return; }

        if (notes.Length > 500) notes = notes.Substring(0, 500);

        try
        {
            // Verify student exists
            using (MySqlConnection connMain = new MySqlConnection(MainConnStr))
            {
                connMain.Open();
                using (MySqlCommand chk = new MySqlCommand("SELECT COUNT(*) FROM acad_student WHERE regno=@r", connMain))
                {
                    chk.Parameters.AddWithValue("@r", regNo);
                    if (Convert.ToInt64(chk.ExecuteScalar()) == 0)
                    { ShowToast("Student '" + regNo + "' not found in the system.", false); OpenModalAfterPostback("modal-add-beneficiary"); return; }
                }
            }

            double schemeAmount = 0;
            string schemeName = "";
            string schemeType = "FIXED";
            double schemeValue = 0;
            long newTID = 0;

            using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();

                // Look up the scheme's details including type
                using (MySqlCommand cmdScheme = new MySqlCommand(
                    @"SELECT scholarshipName, bursary_amount,
                             IFNULL(scheme_type,'FIXED') AS scheme_type,
                             IFNULL(scheme_value,0) AS scheme_value
                      FROM scholarships WHERE scholarshipID=@id AND status='Active'", conn))
                {
                    cmdScheme.Parameters.AddWithValue("@id", schemeId);
                    using (MySqlDataReader rdr = cmdScheme.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            schemeName = rdr["scholarshipName"].ToString();
                            schemeAmount = rdr["bursary_amount"] != DBNull.Value ? Convert.ToDouble(rdr["bursary_amount"]) : 0;
                            schemeType = rdr["scheme_type"].ToString();
                            schemeValue = rdr["scheme_value"] != DBNull.Value ? Convert.ToDouble(rdr["scheme_value"]) : 0;
                        }
                        else
                        {
                            ShowToast("Selected scheme is not active or does not exist.", false);
                            OpenModalAfterPostback("modal-add-beneficiary");
                            return;
                        }
                    }
                }

                // Determine the actual bursary amount based on scheme type
                double actualAmount = 0;

                if (schemeType == "FIXED")
                {
                    actualAmount = schemeAmount;
                    if (actualAmount <= 0)
                    {
                        ShowToast("This FIXED scheme has no bursary amount configured. Please set the amount on the Bursary Schemes page first.", false);
                        OpenModalAfterPostback("modal-add-beneficiary");
                        return;
                    }
                }
                else if (schemeType == "PERCENTAGE")
                {
                    // Calculate from fin_programme_fees
                    if (schemeValue <= 0 || schemeValue > 100)
                    {
                        ShowToast("This PERCENTAGE scheme has an invalid percentage value (" + schemeValue + "%). Please correct it on the Bursary Schemes page.", false);
                        OpenModalAfterPostback("modal-add-beneficiary");
                        return;
                    }

                    double totalFees = 0;
                    try
                    {
                        // Get student's programme and study year
                        string progCode = "";
                        int studyYear = 1;
                        using (MySqlConnection connMain = new MySqlConnection(MainConnStr))
                        {
                            connMain.Open();
                            using (MySqlCommand cmd = new MySqlCommand("SELECT progid FROM acad_student WHERE regno=@r LIMIT 1", connMain))
                            {
                                cmd.Parameters.AddWithValue("@r", regNo);
                                object pv = cmd.ExecuteScalar();
                                if (pv != null && pv != DBNull.Value) progCode = pv.ToString();
                            }
                            using (MySqlCommand cmd = new MySqlCommand(
                                @"SELECT studyyear FROM acad_registration 
                                  WHERE LOWER(regno)=LOWER(@r) AND acad_year=@y AND semester=@s LIMIT 1", connMain))
                            {
                                cmd.Parameters.AddWithValue("@r", regNo);
                                cmd.Parameters.AddWithValue("@y", yearVal);
                                cmd.Parameters.AddWithValue("@s", semester);
                                object sv = cmd.ExecuteScalar();
                                if (sv != null && sv != DBNull.Value) int.TryParse(sv.ToString(), out studyYear);
                                if (studyYear < 1) studyYear = 1;
                                if (studyYear > 3) studyYear = 3;
                            }
                        }

                        if (string.IsNullOrEmpty(progCode))
                        {
                            ShowToast("Cannot calculate percentage — student has no programme assigned.", false);
                            OpenModalAfterPostback("modal-add-beneficiary");
                            return;
                        }

                        string colT = string.Format("y{0}_s{1}_tuition", studyYear, semester);
                        string colF = string.Format("y{0}_s{1}_functional", studyYear, semester);
                        // Resolved by the student's own study session — see fin_ProgFeeRow.
                        string feesSql = string.Format(
                            "SELECT `{0}`, `{1}` FROM fin_programme_fees " +
                            "WHERE ID = fin_ProgFeeRow(@prog, (SELECT s.studsesion " +
                            "                                  FROM campus_dynamics.acad_student s " +
                            "                                  WHERE s.regno=@feeReg LIMIT 1))",
                            colT, colF);

                        double tuit = 0;
                        using (MySqlCommand cmd = new MySqlCommand(feesSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@prog", progCode);
                            cmd.Parameters.AddWithValue("@feeReg", regNo);
                            using (MySqlDataReader rdr = cmd.ExecuteReader())
                            {
                                if (rdr.Read())
                                {
                                    tuit = rdr[colT] != DBNull.Value ? Convert.ToDouble(rdr[colT]) : 0;
                                    double func = rdr[colF] != DBNull.Value ? Convert.ToDouble(rdr[colF]) : 0;
                                    totalFees = tuit + func;
                                }
                            }
                        }

                        if (totalFees <= 0)
                        {
                            ShowToast(string.Format("No fee structure found for programme {0} Year {1} Sem {2}. Cannot calculate percentage bursary.", progCode, studyYear, semester), false);
                            OpenModalAfterPostback("modal-add-beneficiary");
                            return;
                        }

                        actualAmount = Math.Round(tuit * schemeValue / 100.0, 0);
                    }
                    catch (Exception exFees)
                    {
                        ShowToast("Fee calculation error: " + Server.HtmlEncode(exFees.Message), false);
                        OpenModalAfterPostback("modal-add-beneficiary");
                        return;
                    }
                }
                else if (schemeType == "CUSTOM")
                {
                    // Read custom amount from hidden field
                    string customAmtStr = hfAddCustomAmount.Value.Trim();
                    if (string.IsNullOrEmpty(customAmtStr) || !double.TryParse(customAmtStr, out actualAmount) || actualAmount <= 0)
                    {
                        ShowToast("Please enter a valid custom bursary amount.", false);
                        OpenModalAfterPostback("modal-add-beneficiary");
                        return;
                    }
                }
                else
                {
                    // Unknown type — fallback to scheme amount
                    actualAmount = schemeAmount;
                }

                // Duplicate check removed — students may receive multiple bursaries per semester

                // Delegate to BursaryManager (atomic — all 3 tables in one transaction)
                int itemCode = BursaryManager.GetOrCreateBillingItem(conn);
                var result   = BursaryManager.Create(conn, regNo, schemeId, schemeName, actualAmount, yearVal, semester, itemCode, notes);

                if (result.Success)
                {
                    hfAddRegNo.Value = ""; txtAddRegNo.Text = ""; txtAddNotes.Text = "";
                    hfAddCustomAmount.Value = "";
                    string displayName = ddlAddScheme.SelectedItem != null ? ddlAddScheme.SelectedItem.Text : schemeVal;
                    string typeNote = "";
                    if (schemeType == "PERCENTAGE")
                        typeNote = string.Format(" ({0}% of tuition)", schemeValue.ToString("0.##"));
                    else if (schemeType == "CUSTOM")
                        typeNote = " (custom amount)";
                    ShowToast(string.Format("Beneficiary {0} added to {1} ({2} Sem {3}) — UGX {4}{5}. Fee transaction #{6} created.",
                        regNo, schemeName, yearVal, semester, actualAmount.ToString("N0"), typeNote, result.TID), true);
                    LoadBeneficiaries();
                }
                else
                {
                    ShowToast("Error: " + result.Message, false);
                    OpenModalAfterPostback("modal-add-beneficiary");
                }
            }
        }
        catch (Exception ex)
        {
            ShowToast("Error: " + Server.HtmlEncode(ex.Message), false);
            OpenModalAfterPostback("modal-add-beneficiary");
        }
    }

    // ====================================================================
    // Edit Beneficiary
    // ====================================================================
    protected void btnEditBeneficiary_Click(object sender, EventArgs e)
    {
        RestorePostedValue(ddlEditScheme);
        RestorePostedValue(ddlEditAcadYear);
        RestorePostedValue(ddlEditSemester);

        string idStr     = hfEditID.Value.Trim();
        string schemeVal = ddlEditScheme.SelectedValue;
        string yearVal   = ddlEditAcadYear.SelectedValue;
        string semVal    = ddlEditSemester.SelectedValue;
        string amountStr = txtEditAmount.Text.Trim();
        string notes     = txtEditNotes.Text.Trim();

        int id;
        if (!int.TryParse(idStr, out id) || id <= 0)
        { ShowToast("Invalid beneficiary ID.", false); return; }
        if (string.IsNullOrEmpty(schemeVal))
        { ShowToast("Bursary Scheme is required.", false); OpenModalAfterPostback("modal-edit-beneficiary"); return; }
        if (string.IsNullOrEmpty(yearVal))
        { ShowToast("Academic Year is required.", false); OpenModalAfterPostback("modal-edit-beneficiary"); return; }
        if (string.IsNullOrEmpty(semVal))
        { ShowToast("Semester is required.", false); OpenModalAfterPostback("modal-edit-beneficiary"); return; }
        if (string.IsNullOrEmpty(amountStr))
        { ShowToast("Amount is required.", false); OpenModalAfterPostback("modal-edit-beneficiary"); return; }

        int schemeId;
        if (!int.TryParse(schemeVal, out schemeId))
        { ShowToast("Invalid scheme.", false); OpenModalAfterPostback("modal-edit-beneficiary"); return; }
        int semester;
        if (!int.TryParse(semVal, out semester) || semester < 1 || semester > 3)
        { ShowToast("Invalid semester.", false); OpenModalAfterPostback("modal-edit-beneficiary"); return; }
        double amount;
        if (!double.TryParse(amountStr, out amount) || amount < 0)
        { ShowToast("Amount must be a positive number.", false); OpenModalAfterPostback("modal-edit-beneficiary"); return; }
        if (notes.Length > 500) notes = notes.Substring(0, 500);

        try
        {
            using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                int itemCode = BursaryManager.GetOrCreateBillingItem(conn);
                var result   = BursaryManager.Update(conn, id, schemeId, yearVal, semester, amount, notes, itemCode);
                ShowToast(result.Message, result.Success);
                if (!result.Success) OpenModalAfterPostback("modal-edit-beneficiary");
                else LoadBeneficiaries();
            }
        }
        catch (Exception ex)
        {
            ShowToast("Error: " + Server.HtmlEncode(ex.Message), false);
            OpenModalAfterPostback("modal-edit-beneficiary");
        }
    }

    // ====================================================================
    // Delete Beneficiary
    // ====================================================================
    protected void btnDeleteBeneficiary_Click(object sender, EventArgs e)
    {
        string idStr = hfDeleteID.Value.Trim();
        int id;
        if (!int.TryParse(idStr, out id) || id <= 0)
        { ShowToast("Invalid beneficiary ID.", false); return; }

        try
        {
            using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                var result = BursaryManager.Delete(conn, id);
                ShowToast(result.Message, result.Success);
            }
            LoadBeneficiaries();
        }
        catch (Exception ex)
        {
            ShowToast("Error: " + Server.HtmlEncode(ex.Message), false);
        }
    }

    // ====================================================================
    // Export CSV
    // ====================================================================
    protected void btnExportCsv_Click(object sender, EventArgs e)
    {
        RestorePostedValue(ddlScheme);
        RestorePostedValue(ddlAcadYear);
        RestorePostedValue(ddlSemester);

        using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();

            var where = new List<string>();
            var parms = new List<MySqlParameter>();

            if (!string.IsNullOrEmpty(ddlScheme.SelectedValue)) { where.Add("ss.scholarshipID=@scheme"); parms.Add(new MySqlParameter("@scheme", int.Parse(ddlScheme.SelectedValue))); }
            if (!string.IsNullOrEmpty(ddlAcadYear.SelectedValue)) { where.Add("ss.scholarhipYear=@year"); parms.Add(new MySqlParameter("@year", ddlAcadYear.SelectedValue)); }
            if (!string.IsNullOrEmpty(ddlSemester.SelectedValue)) { where.Add("ss.scholarhipTerm=@sem"); parms.Add(new MySqlParameter("@sem", int.Parse(ddlSemester.SelectedValue))); }
            string search = txtSearch.Text.Trim();
            if (!string.IsNullOrEmpty(search)) { where.Add("(ss.adm_no LIKE @search OR s.scholarshipName LIKE @search OR CONCAT(st.firstname,' ',st.othername) LIKE @search)"); parms.Add(new MySqlParameter("@search", "%" + search + "%")); }

            string whereClause = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "";

            string sql = @"SELECT ss.stid, ss.adm_no, IFNULL(CONCAT(st.firstname,' ',st.othername),'') AS student_name,
                                  s.scholarshipName, ss.scholarhipYear, ss.scholarhipTerm, ss.amount_offered,
                                  ss.status, ss.transaction_ref, ss.date_added, ss.notes
                           FROM scholarshipstudents ss
                           LEFT JOIN scholarships s ON s.scholarshipID = ss.scholarshipID
                           LEFT JOIN campus_dynamics.acad_student st ON st.regno = ss.adm_no"
                         + whereClause + " ORDER BY ss.date_added DESC";

            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                foreach (var p in parms) cmd.Parameters.Add(p);
                DataTable dt = new DataTable();
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) { da.Fill(dt); }

                Response.Clear();
                Response.ContentType = "text/csv";
                Response.AddHeader("Content-Disposition", "attachment;filename=BursaryBeneficiaries_" + DateTime.Now.ToString("yyyyMMdd") + ".csv");
                Response.ContentEncoding = Encoding.UTF8;

                // Header
                Response.Write("ID,Reg No,Student Name,Bursary Scheme,Academic Year,Semester,Amount Offered,Status,TX Ref,Date Added,Notes\n");

                foreach (DataRow r in dt.Rows)
                {
                    Response.Write(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}\n",
                        r["stid"],
                        CsvSafe(r["adm_no"]),
                        CsvSafe(r["student_name"]),
                        CsvSafe(r["scholarshipName"]),
                        CsvSafe(r["scholarhipYear"]),
                        r["scholarhipTerm"],
                        r["amount_offered"],
                        CsvSafe(r["status"]),
                        r["transaction_ref"] == DBNull.Value ? "" : r["transaction_ref"].ToString(),
                        r["date_added"] == DBNull.Value ? "" : Convert.ToDateTime(r["date_added"]).ToString("yyyy-MM-dd"),
                        CsvSafe(r["notes"])));
                }

                Response.End();
            }
        }
    }

    // ====================================================================
    // Filter Events
    // ====================================================================
    protected void btnSearch_Click(object sender, EventArgs e) { }
    protected void btnReset_Click(object sender, EventArgs e)
    {
        txtSearch.Text = "";
        ddlScheme.ClearSelection();
        ddlAcadYear.ClearSelection();
        ddlSemester.ClearSelection();
        LoadBeneficiaries();
    }
    protected void ddlScheme_SelectedIndexChanged(object sender, EventArgs e) { }
    protected void ddlAcadYear_SelectedIndexChanged(object sender, EventArgs e) { }
    protected void ddlSemester_SelectedIndexChanged(object sender, EventArgs e) { }
    protected void ddlPageSize_Changed(object sender, EventArgs e) { }

    // ====================================================================
    // Template Helpers
    // ====================================================================

    protected string FormatAmt(object val)
    {
        if (val == null || val == DBNull.Value) return "0";
        try
        {
            double d = Convert.ToDouble(val);
            return "UGX " + d.ToString("N0");
        }
        catch { return val.ToString(); }
    }

    protected string FormatTxRef(object val)
    {
        if (val == null || val == DBNull.Value) return "<span style='color:#ccc;'>—</span>";
        string tid = val.ToString();
        return "<a href='FeesTransactions.aspx?tid=" + tid + "' target='_blank' style='color:#174DA4;font-weight:700;text-decoration:none;' title='Open transaction #" + tid + "'>#" + tid + "</a>";
    }

    protected string FormatDate(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        try { return Convert.ToDateTime(val).ToString("dd MMM yyyy"); }
        catch { return val.ToString(); }
    }

    protected string SafeStr(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        return val.ToString();
    }

    private string CsvSafe(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        string s = val.ToString();
        if (s.Contains(",") || s.Contains("\"") || s.Contains("\n"))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    // ====================================================================
    // Plumbing
    // ====================================================================
    private void ShowToast(string message, bool success)
    {
        pnlToast.Visible = true;
        divToast.Attributes["class"] = success ? "fs-toast fs-toast--success" : "fs-toast fs-toast--error";
        divToast.InnerHtml = Server.HtmlEncode(message);
    }

    private void OpenModalAfterPostback(string modalId)
    {
        ScriptManager.RegisterStartupScript(this, GetType(), "reopenModal",
            "setTimeout(function(){ openModal('" + modalId + "'); },100);", true);
    }

    private static string JsEsc(string val)
    {
        if (string.IsNullOrEmpty(val)) return "";
        return val.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
    }

    // ====================================================================
    // BursaryManager — centralised, transactional Create / Update / Delete
    // Keeps scholarshipstudents + fin_studentfeestracking + fin_ledger in sync.
    // Every public method wraps its work in a MySqlTransaction so all three
    // tables are always written (or rolled back) together — no orphans.
    // ====================================================================
    private static class BursaryManager
    {
        public class Result
        {
            public bool   Success { get; set; }
            public string Message { get; set; }
            public long   TID     { get; set; }
        }

        // ----------------------------------------------------------------
        // Create — inserts beneficiary + payment transaction + ledger DR/CR
        // ----------------------------------------------------------------
        public static Result Create(MySqlConnection conn, string regNo, int schemeId,
            string schemeName, double amount, string acadYear, int semester, int itemCode, string notes)
        {
            var result = new Result();

            // GUARD: a student with active retake(s) is not eligible for a bursary while a
            // retake is in progress. Checked before any write so all-or-nothing holds.
            try
            {
                using (MySqlCommand g = new MySqlCommand(
                    @"SELECT COUNT(*) FROM campus_dynamics_portal.acad_retake_registrations
                      WHERE TRIM(regno)=TRIM(@r) AND status NOT IN ('COMPLETED','CANCELLED')", conn))
                {
                    g.Parameters.AddWithValue("@r", regNo);
                    long activeRetakes = Convert.ToInt64(g.ExecuteScalar());
                    if (activeRetakes > 0)
                    {
                        result.Success = false;
                        result.Message = "Student " + regNo + " has " + activeRetakes +
                            " active retake(s) and is not eligible for a bursary while a retake is in progress.";
                        return result;
                    }
                }
            }
            catch { /* retake table unavailable — do not block bursary on a transient error */ }

            MySqlTransaction tx = conn.BeginTransaction();
            try
            {
                // 1. fin_studentfeestracking payment row
                long tid;
                using (MySqlCommand cmd = new MySqlCommand(
                    @"INSERT INTO fin_studentfeestracking
                        (regno, semester, acadyear, amount, item_code, trans_type, detail, trans_date, post_status)
                      VALUES (@r, @s, @y, @a, @ic, 'Payment', @d, @dt, 'Posted')", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@r",  regNo);
                    cmd.Parameters.AddWithValue("@s",  semester);
                    cmd.Parameters.AddWithValue("@y",  acadYear);
                    cmd.Parameters.AddWithValue("@a",  amount);
                    cmd.Parameters.AddWithValue("@ic", itemCode);
                    cmd.Parameters.AddWithValue("@d",  "Bursary: " + schemeName);
                    cmd.Parameters.AddWithValue("@dt", DateTime.Now.ToString("yyyy-MM-dd"));
                    cmd.ExecuteNonQuery();
                    tid = cmd.LastInsertedId;
                }

                // 2. Ledger double-entry (DR asset + CR student folio)
                PostLedgerEntries(conn, tx, tid, regNo, amount, schemeName);

                // 3. scholarshipstudents beneficiary record
                using (MySqlCommand cmd = new MySqlCommand(
                    @"INSERT INTO scholarshipstudents
                        (adm_no, scholarshipID, scholarhipTerm, scholarhipYear,
                         amountDue, amount_offered, transaction_ref, status, notes)
                      VALUES (@r, @s, @t, @y, @amt, @amt, @tid, 'Approved', @notes)", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@r",     regNo);
                    cmd.Parameters.AddWithValue("@s",     schemeId);
                    cmd.Parameters.AddWithValue("@t",     semester);
                    cmd.Parameters.AddWithValue("@y",     acadYear);
                    cmd.Parameters.AddWithValue("@amt",   amount);
                    cmd.Parameters.AddWithValue("@tid",   tid);
                    cmd.Parameters.AddWithValue("@notes", string.IsNullOrEmpty(notes) ? (object)DBNull.Value : notes);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                result.Success = true;
                result.TID     = tid;
                result.Message = "Beneficiary added. TID #" + tid;
            }
            catch (Exception ex)
            {
                tx.Rollback();
                result.Success = false;
                result.Message = ex.Message;
            }
            return result;
        }

        // ----------------------------------------------------------------
        // Update — edits beneficiary; syncs transaction + ledger if amount changed
        // ----------------------------------------------------------------
        public static Result Update(MySqlConnection conn, int stid, int schemeId,
            string acadYear, int semester, double newAmount, string notes, int itemCode)
        {
            var result = new Result();
            MySqlTransaction tx = conn.BeginTransaction();
            try
            {
                // Load current state
                long? prevTID    = null;
                double prevAmount = 0;
                string prevAdmNo = "";
                string prevSchemeName = "";
                using (MySqlCommand cmd = new MySqlCommand(
                    @"SELECT ss.transaction_ref, ss.amount_offered, ss.adm_no,
                             IFNULL(s.scholarshipName,'') AS sname
                      FROM scholarshipstudents ss
                      LEFT JOIN scholarships s ON s.scholarshipID = ss.scholarshipID
                      WHERE ss.stid = @id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id", stid);
                    using (MySqlDataReader rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read())
                        {
                            tx.Rollback();
                            result.Success = false;
                            result.Message = "Beneficiary #" + stid + " not found.";
                            return result;
                        }
                        prevTID        = rdr["transaction_ref"] != DBNull.Value ? (long?)Convert.ToInt64(rdr["transaction_ref"]) : null;
                        prevAmount     = rdr["amount_offered"]  != DBNull.Value ? Convert.ToDouble(rdr["amount_offered"])        : 0;
                        prevAdmNo      = rdr["adm_no"].ToString();
                        prevSchemeName = rdr["sname"].ToString();
                    }
                }

                // Resolve new scheme name
                string newSchemeName = prevSchemeName;
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT IFNULL(scholarshipName,'') FROM scholarships WHERE scholarshipID=@s", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@s", schemeId);
                    object val = cmd.ExecuteScalar();
                    if (val != null && val != DBNull.Value) newSchemeName = val.ToString();
                }

                bool amountChanged = Math.Abs(newAmount - prevAmount) > 0.001;

                if (prevTID.HasValue && amountChanged)
                {
                    // Update payment row amount
                    using (MySqlCommand cmd = new MySqlCommand(
                        "UPDATE fin_studentfeestracking SET amount=@a, detail=@d WHERE TID=@tid AND trans_type='Payment'", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@a",   newAmount);
                        cmd.Parameters.AddWithValue("@d",   "Bursary: " + newSchemeName);
                        cmd.Parameters.AddWithValue("@tid", prevTID.Value);
                        cmd.ExecuteNonQuery();
                    }
                    // Replace ledger entries at new amount
                    DeleteLedgerEntries(conn, tx, prevTID.Value);
                    PostLedgerEntries(conn, tx, prevTID.Value, prevAdmNo, newAmount, newSchemeName);
                }
                else if (!prevTID.HasValue)
                {
                    // No transaction yet — create one now
                    using (MySqlCommand cmd = new MySqlCommand(
                        @"INSERT INTO fin_studentfeestracking
                            (regno, semester, acadyear, amount, item_code, trans_type, detail, trans_date, post_status)
                          VALUES (@r, @s, @y, @a, @ic, 'Payment', @d, @dt, 'Posted')", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@r",  prevAdmNo);
                        cmd.Parameters.AddWithValue("@s",  semester);
                        cmd.Parameters.AddWithValue("@y",  acadYear);
                        cmd.Parameters.AddWithValue("@a",  newAmount);
                        cmd.Parameters.AddWithValue("@ic", itemCode);
                        cmd.Parameters.AddWithValue("@d",  "Bursary: " + newSchemeName);
                        cmd.Parameters.AddWithValue("@dt", DateTime.Now.ToString("yyyy-MM-dd"));
                        cmd.ExecuteNonQuery();
                        prevTID = cmd.LastInsertedId;
                    }
                    PostLedgerEntries(conn, tx, prevTID.Value, prevAdmNo, newAmount, newSchemeName);
                }

                // Update the beneficiary record
                using (MySqlCommand cmd = new MySqlCommand(
                    @"UPDATE scholarshipstudents SET
                        scholarshipID=@s, scholarhipTerm=@t, scholarhipYear=@y,
                        amountDue=@amt, amount_offered=@amt,
                        transaction_ref=@tid, notes=@notes
                      WHERE stid=@id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@s",     schemeId);
                    cmd.Parameters.AddWithValue("@t",     semester);
                    cmd.Parameters.AddWithValue("@y",     acadYear);
                    cmd.Parameters.AddWithValue("@amt",   newAmount);
                    cmd.Parameters.AddWithValue("@tid",   prevTID.HasValue ? (object)prevTID.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@notes", string.IsNullOrEmpty(notes) ? (object)DBNull.Value : notes);
                    cmd.Parameters.AddWithValue("@id",    stid);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                result.Success = true;
                result.TID     = prevTID.HasValue ? prevTID.Value : 0;
                result.Message = "Beneficiary #" + stid + " updated."
                               + (amountChanged ? " Amount & ledger updated to UGX " + newAmount.ToString("N0") + "." : "");
            }
            catch (Exception ex)
            {
                tx.Rollback();
                result.Success = false;
                result.Message = ex.Message;
            }
            return result;
        }

        // ----------------------------------------------------------------
        // Delete — removes beneficiary + transaction + ledger atomically
        // ----------------------------------------------------------------
        public static Result Delete(MySqlConnection conn, int stid)
        {
            var result = new Result();
            MySqlTransaction tx = conn.BeginTransaction();
            try
            {
                // Read linked TID first
                long? tid = null;
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT transaction_ref FROM scholarshipstudents WHERE stid=@id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id", stid);
                    object val = cmd.ExecuteScalar();
                    if (val != null && val != DBNull.Value) tid = Convert.ToInt64(val);
                }

                // Delete the beneficiary
                int deleted;
                using (MySqlCommand cmd = new MySqlCommand(
                    "DELETE FROM scholarshipstudents WHERE stid=@id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id", stid);
                    deleted = cmd.ExecuteNonQuery();
                }

                if (deleted == 0)
                {
                    tx.Rollback();
                    result.Success = false;
                    result.Message = "Beneficiary not found.";
                    return result;
                }

                // Delete the payment transaction + ledger entries
                if (tid.HasValue)
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "DELETE FROM fin_studentfeestracking WHERE TID=@tid AND trans_type='Payment'", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@tid", tid.Value);
                        cmd.ExecuteNonQuery();
                    }
                    DeleteLedgerEntries(conn, tx, tid.Value);
                    result.TID = tid.Value;
                }

                tx.Commit();
                result.Success = true;
                result.Message = "Beneficiary removed."
                               + (tid.HasValue ? " Fee transaction #" + tid.Value + " reversed." : "");
            }
            catch (Exception ex)
            {
                tx.Rollback();
                result.Success = false;
                result.Message = ex.Message;
            }
            return result;
        }

        // ----------------------------------------------------------------
        // GetOrCreateBillingItem — finds or creates 'Bursary/Scholarship' item
        // ----------------------------------------------------------------
        public static int GetOrCreateBillingItem(MySqlConnection conn)
        {
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT ItemCode FROM academicbillingitems WHERE ItemName='Bursary/Scholarship' LIMIT 1", conn))
            {
                object r = cmd.ExecuteScalar();
                if (r != null && r != DBNull.Value) return Convert.ToInt32(r);
            }
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT ItemCode FROM academicbillingitems WHERE ItemName LIKE '%Bursary%' OR ItemName LIKE '%Scholarship%' LIMIT 1", conn))
            {
                object r = cmd.ExecuteScalar();
                if (r != null && r != DBNull.Value) return Convert.ToInt32(r);
            }
            using (MySqlCommand cmd = new MySqlCommand(
                "INSERT INTO academicbillingitems (ItemName, AccountCode, PriorityCode) VALUES ('Bursary/Scholarship','AC6099',1)", conn))
            {
                cmd.ExecuteNonQuery();
                return (int)cmd.LastInsertedId;
            }
        }

        // ----------------------------------------------------------------
        // PostLedgerEntries — DR on AC1302 (asset) + CR on student folio
        // ----------------------------------------------------------------
        private static void PostLedgerEntries(MySqlConnection conn, MySqlTransaction tx,
            long tid, string regNo, double amount, string schemeName)
        {
            const string sql = @"
                INSERT INTO fin_ledger
                    (accountcode, account_type, transactionType, transaction_amount, particulars,
                     voucherNo, transactionDate, teller, timeLog, folio,
                     journal_no, trans_currency, actual_amount, curr_balance, forex_rate, ugx_amount)
                VALUES (@ac, @at, @tt, @amt, @part, @vno, @td, 'System', @tl, @fo, '-', 'UGX', @amt, 0, 1, @amt)";

            string today = DateTime.Now.ToString("yyyy-MM-dd");

            // DR — bursary disbursed (asset side)
            using (MySqlCommand cmd = new MySqlCommand(sql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@ac",   "AC1302");
                cmd.Parameters.AddWithValue("@at",   "Chart Account");
                cmd.Parameters.AddWithValue("@tt",   "DR");
                cmd.Parameters.AddWithValue("@amt",  amount);
                cmd.Parameters.AddWithValue("@part", "Bursary disbursed: " + schemeName + " [" + regNo + "]");
                cmd.Parameters.AddWithValue("@vno",  tid);
                cmd.Parameters.AddWithValue("@td",   today);
                cmd.Parameters.AddWithValue("@tl",   DateTime.Now);
                cmd.Parameters.AddWithValue("@fo",   regNo);
                cmd.ExecuteNonQuery();
            }

            // CR — student balance reduced
            using (MySqlCommand cmd = new MySqlCommand(sql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@ac",   regNo);
                cmd.Parameters.AddWithValue("@at",   "BursaryFees");
                cmd.Parameters.AddWithValue("@tt",   "CR");
                cmd.Parameters.AddWithValue("@amt",  amount);
                cmd.Parameters.AddWithValue("@part", "Bursary: " + schemeName);
                cmd.Parameters.AddWithValue("@vno",  tid);
                cmd.Parameters.AddWithValue("@td",   today);
                cmd.Parameters.AddWithValue("@tl",   DateTime.Now);
                cmd.Parameters.AddWithValue("@fo",   regNo);
                cmd.ExecuteNonQuery();
            }
        }

        // ----------------------------------------------------------------
        // DeleteLedgerEntries — removes both DR and CR rows for a voucherNo
        // ----------------------------------------------------------------
        private static void DeleteLedgerEntries(MySqlConnection conn, MySqlTransaction tx, long tid)
        {
            using (MySqlCommand cmd = new MySqlCommand(
                "DELETE FROM fin_ledger WHERE voucherNo=@vno", conn, tx))
            {
                cmd.Parameters.AddWithValue("@vno", tid);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
