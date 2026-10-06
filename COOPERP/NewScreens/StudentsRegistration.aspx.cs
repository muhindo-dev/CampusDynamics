using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

public partial class COOPERP_NewScreens_StudentsRegistration : System.Web.UI.Page
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

    protected void Page_Load(object sender, EventArgs e)
    {
        // AJAX endpoints for the rich delete flow (summary + transactional delete). These respond
        // with JSON and end the request before any page rendering.
        string ajax = Request.QueryString["ajax"];
        if (ajax == "delreg_summary") { HandleDeleteRegSummary(); return; }
        if (ajax == "delreg_execute") { HandleDeleteRegExecute(); return; }

        // ALWAYS reload dropdown items — ViewState is disabled on master page,
        // so dropdown items are lost on every postback. ASP.NET's second
        // ProcessPostData pass will restore user selections from posted form
        // data once the items are available.
        LoadAcademicYears();
        LoadProgrammes();

        if (!IsPostBack)
        {
            // Filter default = ALL (year + semester). The GET query string is the
            // single source of truth for the active filters; nothing is pre-selected
            // so picking "All" sticks instead of snapping back to the current period.
            ApplyFiltersFromQueryString();

            // Add-modal defaults to the current period for data-entry convenience.
            int curSem = AcademicYearHelper.GetCurrentSemester();
            if (curSem >= 1 && curSem <= 3)
                ddlAddSemester.SelectedValue = curSem.ToString();

            UpdateDisplayLabels();
            LoadStats();
            BindGrid();
        }
        else
        {
            // ViewState is disabled on the master page, and the Add/Edit *year* dropdowns are
            // dynamically (re)populated on every load with a current-year default (via
            // AcademicYearHelper.PopulateDropDown(..., selectCurrent:true)). On a form submit that
            // default would overwrite the year the user actually picked in the modal — so restore
            // each year dropdown from its posted value here, BEFORE the Add/Edit save handlers read
            // it. This guarantees create/edit uses exactly the year set in the form, not "current".
            // (The other modal fields — semester, study year, status, residence — are static markup
            // items, so ASP.NET already restores their posted selection.)
            SetSelectedIfExists(ddlAddAcadYear, Request.Form[ddlAddAcadYear.UniqueID]);
            SetSelectedIfExists(ddlEditAcadYear, Request.Form[ddlEditAcadYear.UniqueID]);
        }
    }

    // ===================================================================
    // HELPERS - Academic Year / Semester (centralised in AcademicYearHelper)
    // ===================================================================

    private void LoadAcademicYears()
    {
        // Main filter dropdown: "All" option, default to ALL (no pre-selected year).
        // GET query-string fully controls the selection; an empty/absent value = All.
        AcademicYearHelper.PopulateDropDown(ddlAcadYear, true, false);

        // Form dropdowns (no "All" option, default to current year)
        AcademicYearHelper.PopulateDropDown(ddlAddAcadYear, false, true);
        AcademicYearHelper.PopulateDropDown(ddlEditAcadYear, false, true);
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

    private void ApplyFiltersFromQueryString()
    {
        string q = (Request.QueryString["q"] ?? "").Trim();
        if (!string.IsNullOrEmpty(q)) txtSearch.Text = q;

        SetSelectedIfExists(ddlAcadYear, Request.QueryString["ay"] ?? "");
        SetSelectedIfExists(ddlSemester, Request.QueryString["sem"] ?? "");
        SetSelectedIfExists(ddlStudyYear, Request.QueryString["sy"] ?? "");
        SetSelectedIfExists(ddlRegStatus, Request.QueryString["rs"] ?? "");
        SetSelectedIfExists(ddlProgramme, Request.QueryString["prog"] ?? "");
        SetSelectedIfExists(ddlExamClearance, Request.QueryString["ec"] ?? "");
        SetSelectedIfExists(ddlIDCard, Request.QueryString["idc"] ?? "");
        SetSelectedIfExists(ddlResidence, Request.QueryString["res"] ?? "");
        SetSelectedIfExists(ddlBilling, Request.QueryString["bill"] ?? "");
        SetSelectedIfExists(ddlPageSize, Request.QueryString["per"] ?? "");
    }

    private void SetSelectedIfExists(ListControl control, string value)
    {
        if (control == null) return;
        if (string.IsNullOrEmpty(value)) return;
        var item = control.Items.FindByValue(value);
        if (item != null)
        {
            control.ClearSelection();
            item.Selected = true;
        }
    }

    private int ParseQueryInt(string key, int defaultValue, int minValue, int maxValue)
    {
        int value;
        if (!int.TryParse(Request.QueryString[key], out value)) value = defaultValue;
        if (value < minValue) value = minValue;
        if (value > maxValue) value = maxValue;
        return value;
    }

    private string BuildListUrl(int page)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);

        if (!string.IsNullOrEmpty(txtSearch.Text.Trim())) query["q"] = txtSearch.Text.Trim();
        if (!string.IsNullOrEmpty(ddlAcadYear.SelectedValue)) query["ay"] = ddlAcadYear.SelectedValue;
        if (!string.IsNullOrEmpty(ddlSemester.SelectedValue)) query["sem"] = ddlSemester.SelectedValue;
        if (!string.IsNullOrEmpty(ddlStudyYear.SelectedValue)) query["sy"] = ddlStudyYear.SelectedValue;
        if (!string.IsNullOrEmpty(ddlRegStatus.SelectedValue)) query["rs"] = ddlRegStatus.SelectedValue;
        if (!string.IsNullOrEmpty(ddlProgramme.SelectedValue)) query["prog"] = ddlProgramme.SelectedValue;
        if (!string.IsNullOrEmpty(ddlExamClearance.SelectedValue)) query["ec"] = ddlExamClearance.SelectedValue;
        if (!string.IsNullOrEmpty(ddlIDCard.SelectedValue)) query["idc"] = ddlIDCard.SelectedValue;
        if (!string.IsNullOrEmpty(ddlResidence.SelectedValue)) query["res"] = ddlResidence.SelectedValue;
        if (!string.IsNullOrEmpty(ddlBilling.SelectedValue)) query["bill"] = ddlBilling.SelectedValue;
        if (!string.IsNullOrEmpty(ddlPageSize.SelectedValue)) query["per"] = ddlPageSize.SelectedValue;

        query["page"] = (page < 1 ? 1 : page).ToString();

        string q = query.ToString();
        return ResolveUrl("~/COOPERP/NewScreens/StudentsRegistration.aspx") + (string.IsNullOrEmpty(q) ? "" : "?" + q);
    }

    private string BuildPagerHtml(int page, int totalPages)
    {
        if (totalPages <= 1) return "";

        var html = new StringBuilder();

        if (page > 1) html.AppendFormat("<a href='{0}'>Prev</a>", BuildListUrl(page - 1));
        else html.Append("<span class='is-disabled'>Prev</span>");

        int start = Math.Max(1, page - 3);
        int end = Math.Min(totalPages, page + 3);

        if (start > 1)
        {
            html.AppendFormat("<a href='{0}'>1</a>", BuildListUrl(1));
            if (start > 2) html.Append("<span class='is-disabled'>…</span>");
        }

        for (int p = start; p <= end; p++)
        {
            if (p == page) html.AppendFormat("<span class='is-active'>{0}</span>", p);
            else html.AppendFormat("<a href='{0}'>{1}</a>", BuildListUrl(p), p);
        }

        if (end < totalPages)
        {
            if (end < totalPages - 1) html.Append("<span class='is-disabled'>…</span>");
            html.AppendFormat("<a href='{0}'>{1}</a>", BuildListUrl(totalPages), totalPages);
        }

        if (page < totalPages) html.AppendFormat("<a href='{0}'>Next</a>", BuildListUrl(page + 1));
        else html.Append("<span class='is-disabled'>Next</span>");

        return html.ToString();
    }

    private void RedirectToList(int page)
    {
        Response.Redirect(BuildListUrl(page), false);
        Context.ApplicationInstance.CompleteRequest();
    }

    private void RedirectCleanList()
    {
        Response.Redirect(ResolveUrl("~/COOPERP/NewScreens/StudentsRegistration.aspx"), false);
        Context.ApplicationInstance.CompleteRequest();
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
                -- Billed / Not-billed measured among ENROLLED rows only (REGISTERED /
                -- LATE REGISTERED / CLEARED). Do NOT gate on acad_student.new_status -- it
                -- is corrupted (28k+ active students carry a stale ALUMNI value), which
                -- would undercount and make the cards disagree with the grid filter.
                SUM(CASE WHEN r.regstatus IN ('REGISTERED','LATE REGISTERED','CLEARED')
                    AND EXISTS(
                        SELECT 1
                        FROM campus_dynamics_accounts.fin_studentfeestracking ft
                        WHERE ft.regno = COALESCE(NULLIF(TRIM(sreg.regno), ''), TRIM(r.regno))
                          AND ft.acadyear = r.acad_year
                          AND ft.semester = r.semester
                          AND ft.trans_type = 'Bill'
                    )
                THEN 1 ELSE 0 END) AS billed,
                SUM(CASE WHEN r.regstatus IN ('REGISTERED','LATE REGISTERED','CLEARED')
                    AND NOT EXISTS(
                        SELECT 1
                        FROM campus_dynamics_accounts.fin_studentfeestracking ft
                        WHERE ft.regno = COALESCE(NULLIF(TRIM(sreg.regno), ''), TRIM(r.regno))
                          AND ft.acadyear = r.acad_year
                          AND ft.semester = r.semester
                          AND ft.trans_type = 'Bill'
                    )
                THEN 1 ELSE 0 END) AS not_billed
            FROM acad_registration r
            LEFT JOIN acad_student sreg ON sreg.regno = r.regno
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
                    cmd.CommandTimeout = 30;
                    foreach (var p in parms) cmd.Parameters.Add(p);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            litTotal.Text          = FormatCount(rdr["total"]);
                            litUnregistered.Text   = FormatCount(rdr["unregistered"]);
                            litRegistered.Text     = FormatCount(rdr["registered"]);
                            litLateRegistered.Text = FormatCount(rdr["late_reg"]);
                            litCleared.Text        = FormatCount(rdr["cleared"]);
                            litDiscontinued.Text   = FormatCount(rdr["discontinued"]);
                            litHalted.Text         = FormatCount(rdr["halted"]);
                            litDeadYear.Text       = FormatCount(rdr["dead_year"]);
                            litBilled.Text         = FormatCount(rdr["billed"]);
                            litNotBilled.Text      = FormatCount(rdr["not_billed"]);
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
        if (pageSize < 25) pageSize = 25;
        if (pageSize > 500) pageSize = 500;

        int page = ParseQueryInt("page", 1, 1, int.MaxValue);

        var fromWhere = new StringBuilder(@"
            FROM acad_registration r
            LEFT JOIN acad_student sreg ON sreg.regno = r.regno
            LEFT JOIN acad_programme preg ON sreg.progid = preg.progcode
            WHERE 1=1");

        var parameters = new List<MySqlParameter>();

        // Year / semester - only filter when not "All"
        string acadYear = ddlAcadYear.SelectedValue;
        string semStr   = ddlSemester.SelectedValue;
        if (!string.IsNullOrEmpty(acadYear)) { fromWhere.Append(" AND r.acad_year = @acadYear"); parameters.Add(new MySqlParameter("@acadYear", acadYear)); }
        if (!string.IsNullOrEmpty(semStr))   { fromWhere.Append(" AND r.semester  = @semester"); parameters.Add(new MySqlParameter("@semester", SafeInt(semStr, 1))); }

        // Other filters
        if (!string.IsNullOrEmpty(ddlStudyYear.SelectedValue))
        {
            fromWhere.Append(" AND r.studyyear = @studyYear");
            parameters.Add(new MySqlParameter("@studyYear", SafeInt(ddlStudyYear.SelectedValue, 0)));
        }
        if (!string.IsNullOrEmpty(ddlRegStatus.SelectedValue))
        {
            fromWhere.Append(" AND r.regstatus = @regStatus");
            parameters.Add(new MySqlParameter("@regStatus", ddlRegStatus.SelectedValue));
        }
        if (!string.IsNullOrEmpty(ddlProgramme.SelectedValue))
        {
            fromWhere.Append(" AND COALESCE(NULLIF(sreg.progid, ''), '') = @programme");
            parameters.Add(new MySqlParameter("@programme", ddlProgramme.SelectedValue));
        }
        if (!string.IsNullOrEmpty(ddlExamClearance.SelectedValue))
        {
            fromWhere.Append(" AND r.examClearance = @examClear");
            parameters.Add(new MySqlParameter("@examClear", ddlExamClearance.SelectedValue));
        }
        if (!string.IsNullOrEmpty(ddlIDCard.SelectedValue))
        {
            if (ddlIDCard.SelectedValue == "ISSUED")
                fromWhere.Append(" AND r.id_cardStatus = 'ISSUED'");
            else
                fromWhere.Append(" AND (r.id_cardStatus IS NULL OR r.id_cardStatus != 'ISSUED')");
        }
        if (!string.IsNullOrEmpty(ddlResidence.SelectedValue))
        {
            if (ddlResidence.SelectedValue == "RESIDENT")
                fromWhere.Append(" AND r.residence_status = 'RESIDENT'");
            else
                fromWhere.Append(" AND r.residence_status != 'RESIDENT'");
        }
        if (!string.IsNullOrEmpty(ddlBilling.SelectedValue))
        {
            // Billed / Not-billed apply to ENROLLED registrations only (REGISTERED /
            // LATE REGISTERED / CLEARED), so the grid matches the Billed/Not-Billed
            // stat-card counts exactly. An UNREGISTERED row is never "not billed" here
            // (it's expected to have no bill) — it belongs under the Unregistered filter.
            if (ddlBilling.SelectedValue == "BILLED")
                fromWhere.Append(" AND r.regstatus IN ('REGISTERED','LATE REGISTERED','CLEARED') AND EXISTS(SELECT 1 FROM campus_dynamics_accounts.fin_studentfeestracking ft WHERE ft.regno=COALESCE(NULLIF(TRIM(sreg.regno), ''), TRIM(r.regno)) AND ft.acadyear=r.acad_year AND ft.semester=r.semester AND ft.trans_type='Bill')");
            else
                fromWhere.Append(" AND r.regstatus IN ('REGISTERED','LATE REGISTERED','CLEARED') AND NOT EXISTS(SELECT 1 FROM campus_dynamics_accounts.fin_studentfeestracking ft WHERE ft.regno=COALESCE(NULLIF(TRIM(sreg.regno), ''), TRIM(r.regno)) AND ft.acadyear=r.acad_year AND ft.semester=r.semester AND ft.trans_type='Bill')");
        }
        if (!string.IsNullOrEmpty(search))
        {
            fromWhere.Append(" AND (COALESCE(NULLIF(TRIM(sreg.regno), ''), TRIM(r.regno)) LIKE @search OR TRIM(CONCAT_WS(' ', NULLIF(sreg.firstname, ''), NULLIF(sreg.othername, ''))) LIKE @search OR COALESCE(NULLIF(sreg.progid, ''), '') LIKE @search)");
            parameters.Add(new MySqlParameter("@search", "%" + search + "%"));
        }

        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                int totalCount;
                using (var countCmd = new MySqlCommand("SELECT COUNT(*) " + fromWhere, conn))
                {
                    countCmd.CommandTimeout = 45;
                    foreach (var p in parameters)
                        countCmd.Parameters.AddWithValue(p.ParameterName, p.Value);
                    totalCount = SafeInt(countCmd.ExecuteScalar(), 0);
                }

                int totalPages = totalCount <= 0 ? 1 : (int)Math.Ceiling((double)totalCount / pageSize);
                if (page > totalPages) page = totalPages;
                int offset = (page - 1) * pageSize;

                var selectSql = new StringBuilder(@"
                    SELECT
                        r.ID,
                        COALESCE(NULLIF(TRIM(sreg.regno), ''), TRIM(r.regno)) AS regno,
                        r.acad_year,
                        r.semester,
                        r.regstatus,
                        r.studyyear,
                        CASE WHEN r.id_cardStatus = 'ISSUED' THEN 'ISSUED' ELSE 'NOT ISSUED' END AS id_cardStatus,
                        CASE WHEN r.residence_status = 'RESIDENT' THEN 'RESIDENT' ELSE 'NON-RESIDENT' END AS residence_status,
                        COALESCE(r.examClearance, 'UNCLEARED')       AS examClearance,
                        CASE WHEN r.examClearanceDate IS NULL OR r.examClearanceDate = '0000-00-00'
                             THEN '' ELSE DATE_FORMAT(r.examClearanceDate,'%d %b %Y') END AS examClearanceDate,
                        CASE WHEN r.examClearanceDate IS NULL OR r.examClearanceDate = '0000-00-00'
                             THEN '' ELSE DATE_FORMAT(r.examClearanceDate,'%Y-%m-%d') END AS examClearanceDateRaw,
                        COALESCE(r.registeredBy, '') AS registeredBy,
                        COALESCE(r.clearedBy, '')    AS clearedBy,
                        NULLIF(TRIM(CONCAT_WS(' ', NULLIF(sreg.firstname, ''), NULLIF(sreg.othername, ''))), '') AS student_name,
                        COALESCE(NULLIF(sreg.progid, ''), '') AS progcode,
                        COALESCE(NULLIF(preg.progname, ''), '') AS progname,
                        CASE WHEN EXISTS(
                            SELECT 1
                            FROM campus_dynamics_accounts.fin_studentfeestracking ft
                            WHERE ft.regno = COALESCE(NULLIF(TRIM(sreg.regno), ''), TRIM(r.regno))
                              AND ft.acadyear = r.acad_year
                              AND ft.semester = r.semester
                              AND ft.trans_type = 'Bill'
                        ) THEN 'BILLED' ELSE 'NOT BILLED' END AS billing_status,
                        COALESCE((
                            SELECT SUM(ftb.amount)
                            FROM campus_dynamics_accounts.fin_studentfeestracking ftb
                            WHERE ftb.regno = COALESCE(NULLIF(TRIM(sreg.regno), ''), TRIM(r.regno))
                              AND ftb.acadyear = r.acad_year
                              AND ftb.semester = r.semester
                              AND ftb.trans_type = 'Bill'
                        ), 0) AS total_billed,
                        COALESCE((
                            SELECT SUM(ftp.amount)
                            FROM campus_dynamics_accounts.fin_studentfeestracking ftp
                            WHERE ftp.regno = COALESCE(NULLIF(TRIM(sreg.regno), ''), TRIM(r.regno))
                              AND ftp.acadyear = r.acad_year
                              AND ftp.semester = r.semester
                              AND ftp.trans_type = 'Payment'
                        ), 0) AS total_paid
                ");
                selectSql.Append(fromWhere.ToString());
                selectSql.Append(" ORDER BY sreg.firstname, sreg.othername, COALESCE(NULLIF(TRIM(sreg.regno), ''), TRIM(r.regno))");
                selectSql.Append(" LIMIT @offset, @pageSize");

                using (var cmd = new MySqlCommand(selectSql.ToString(), conn))
                {
                    cmd.CommandTimeout = 60;
                    foreach (var p in parameters)
                        cmd.Parameters.AddWithValue(p.ParameterName, p.Value);
                    cmd.Parameters.AddWithValue("@offset", offset);
                    cmd.Parameters.AddWithValue("@pageSize", pageSize);

                    using (var da = new MySqlDataAdapter(cmd))
                    {
                        var dt = new DataTable();
                        da.Fill(dt);

                        HydrateMissingStudentDetails(dt, conn);

                        lblRecordCount.Text  = totalCount.ToString("N0") + " item" + (totalCount != 1 ? "s" : "");
                        litFooterCount.Text  = string.Format("Page {0} of {1} ({2:N0} items)", page, totalPages, totalCount);
                        litPager.Text        = BuildPagerHtml(page, totalPages);
                        rptRegistrations.DataSource = dt;
                        rptRegistrations.DataBind();
                        pnlNoData.Visible = (dt.Rows.Count == 0);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ShowToast(false, "Error loading data: " + ex.Message);
        }
    }

    private sealed class StudentFallbackInfo
    {
        public string RegNo;
        public string StudentName;
        public string ProgCode;
        public string ProgName;
    }

    private void HydrateMissingStudentDetails(DataTable table, MySqlConnection conn)
    {
        if (table == null || table.Rows.Count == 0 || conn == null) return;

        var missingEmails = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (DataRow row in table.Rows)
        {
            string studentName = (row["student_name"] == DBNull.Value ? "" : row["student_name"].ToString()).Trim();
            if (!string.IsNullOrEmpty(studentName)) continue;

            string regOrEmail = (row["regno"] == DBNull.Value ? "" : row["regno"].ToString()).Trim();
            if (string.IsNullOrEmpty(regOrEmail) || regOrEmail.IndexOf('@') < 0) continue;

            string key = regOrEmail.ToLowerInvariant();
            if (seen.Add(key)) missingEmails.Add(key);
        }

        if (missingEmails.Count == 0) return;

        var sql = new StringBuilder(@"
            SELECT
                LOWER(TRIM(s.email)) AS email_key,
                TRIM(IFNULL(s.regno, '')) AS regno,
                NULLIF(TRIM(CONCAT_WS(' ', NULLIF(s.firstname, ''), NULLIF(s.othername, ''))), '') AS student_name,
                IFNULL(s.progid, '') AS progcode,
                IFNULL(p.progname, '') AS progname
            FROM acad_student s
            LEFT JOIN acad_programme p ON p.progcode = s.progid
            WHERE IFNULL(s.email, '') <> ''
              AND LOWER(TRIM(s.email)) IN (");

        for (int i = 0; i < missingEmails.Count; i++)
        {
            if (i > 0) sql.Append(",");
            sql.Append("@e").Append(i);
        }
        sql.Append(") ORDER BY s.regno");

        var byEmail = new Dictionary<string, StudentFallbackInfo>(StringComparer.OrdinalIgnoreCase);

        using (var cmd = new MySqlCommand(sql.ToString(), conn))
        {
            cmd.CommandTimeout = 30;
            for (int i = 0; i < missingEmails.Count; i++)
                cmd.Parameters.AddWithValue("@e" + i, missingEmails[i]);

            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    string emailKey = rdr["email_key"].ToString();
                    if (string.IsNullOrEmpty(emailKey) || byEmail.ContainsKey(emailKey)) continue;

                    byEmail[emailKey] = new StudentFallbackInfo
                    {
                        RegNo = rdr["regno"].ToString().Trim(),
                        StudentName = (rdr["student_name"] == DBNull.Value ? "" : rdr["student_name"].ToString().Trim()),
                        ProgCode = rdr["progcode"].ToString().Trim(),
                        ProgName = rdr["progname"].ToString().Trim()
                    };
                }
            }
        }

        if (byEmail.Count == 0) return;

        foreach (DataRow row in table.Rows)
        {
            string studentName = (row["student_name"] == DBNull.Value ? "" : row["student_name"].ToString()).Trim();
            if (!string.IsNullOrEmpty(studentName)) continue;

            string regOrEmail = (row["regno"] == DBNull.Value ? "" : row["regno"].ToString()).Trim();
            if (string.IsNullOrEmpty(regOrEmail)) continue;

            StudentFallbackInfo info;
            if (!byEmail.TryGetValue(regOrEmail.ToLowerInvariant(), out info)) continue;

            if (!string.IsNullOrEmpty(info.StudentName)) row["student_name"] = info.StudentName;
            if (!string.IsNullOrEmpty(info.ProgCode)) row["progcode"] = info.ProgCode;
            if (!string.IsNullOrEmpty(info.ProgName)) row["progname"] = info.ProgName;
            if (!string.IsNullOrEmpty(info.RegNo)) row["regno"] = info.RegNo;
        }
    }

    // ===================================================================
    // TEMPLATE HELPERS - visibility in DataItemTemplate
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

    protected string GetRowClass(string status)
    {
        switch ((status ?? "").ToUpper().Trim())
        {
            case "LATE REGISTERED": return "sr-row--late";
            case "CLEARED":         return "sr-row--cleared";
            case "DISCONTINUED":    return "sr-row--discont";
            case "HALTED":          return "sr-row--halted";
            case "DEAD YEAR":       return "sr-row--dead";
            default:                return "";
        }
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

    protected void btnSearch_Click(object sender, EventArgs e)                   { RedirectToList(1); }

    protected void ddlAcadYear_SelectedIndexChanged(object sender, EventArgs e)   { RedirectToList(1); }
    protected void ddlSemester_SelectedIndexChanged(object sender, EventArgs e)   { RedirectToList(1); }
    protected void ddlStudyYear_SelectedIndexChanged(object sender, EventArgs e)  { RedirectToList(1); }
    protected void ddlRegStatus_SelectedIndexChanged(object sender, EventArgs e)  { RedirectToList(1); }
    protected void ddlProgramme_SelectedIndexChanged(object sender, EventArgs e)  { RedirectToList(1); }
    protected void ddlExamClearance_SelectedIndexChanged(object sender, EventArgs e) { RedirectToList(1); }
    protected void ddlIDCard_SelectedIndexChanged(object sender, EventArgs e)     { RedirectToList(1); }
    protected void ddlResidence_SelectedIndexChanged(object sender, EventArgs e)  { RedirectToList(1); }
    protected void ddlBilling_SelectedIndexChanged(object sender, EventArgs e)    { RedirectToList(1); }
    protected void txtSearch_TextChanged(object sender, EventArgs e)              { /* handled by btnSearch_Click */ }
    protected void ddlPageSize_Changed(object sender, EventArgs e)               { RedirectToList(1); }

    protected void btnReset_Click(object sender, EventArgs e)
    {
        RedirectCleanList();
    }
    protected void btnRefresh_Click(object sender, EventArgs e) { LoadStats(); BindGrid(); }

    // ===================================================================
    // INDIVIDUAL ACTIONS
    // ===================================================================

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
    // DELETE REGISTRATION  (rich flow: summary + optional courses + billings)
    // One connection via AcctConnStr (root) uses fully-qualified names across the 3 DBs:
    //   campus_dynamics.acad_registration | campus_dynamics_portal.acad_course_registration
    //   campus_dynamics_accounts.fin_studentfeestracking (+ fin_ledger mirror, MyISAM)
    // Every delete is BACKED UP first into *_regdel_bak tables (fin_ledger is MyISAM = no
    // rollback, so the backup is the recovery guarantee).
    // ===================================================================

    private void WriteJsonEnd(string json)
    {
        // Response.End() throws ThreadAbortException — kept OUTSIDE any try/catch so it propagates to
        // the framework (which finishes the request) instead of being mis-reported as an error.
        Response.Clear(); Response.ContentType = "application/json"; Response.Write(json); Response.End();
    }

    /// <summary>Summary of what a semester-registration delete would remove: the registration, its
    /// courses (flagging any that already carry marks), and the fee bills/payments for that semester.</summary>
    private void HandleDeleteRegSummary()
    {
        var js = new JavaScriptSerializer();
        string json;
        try
        {
            int id; int.TryParse(Request.QueryString["id"], out id);
            if (id <= 0) { WriteJsonEnd(js.Serialize(new { success = false, message = "Missing registration id." })); return; }

            string regno = "", acadYear = "", sem = "", sy = "", status = "", name = "";
            int courseCount = 0, courseWithMarks = 0, sftBills = 0, sftPays = 0, glCount = 0;
            decimal billAmt = 0, payAmt = 0;
            var sampleCourses = new List<string>();

            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    @"SELECT r.regno, r.acad_year, r.semester, r.studyyear, r.regstatus,
                             TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) nm
                        FROM campus_dynamics.acad_registration r
                        LEFT JOIN campus_dynamics.acad_student s ON s.regno=r.regno
                       WHERE r.ID=@id LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read()) { WriteJsonEnd(js.Serialize(new { success = false, message = "Registration not found." })); return; }
                        regno = S(rdr, "regno"); acadYear = S(rdr, "acad_year"); sem = S(rdr, "semester");
                        sy = S(rdr, "studyyear"); status = S(rdr, "regstatus"); name = S(rdr, "nm");
                    }
                }

                using (var cmd = new MySqlCommand(
                    @"SELECT cr.courseID, IF(cr.provisional_total_marks IS NOT NULL OR UPPER(IFNULL(cr.mark_stage,'NOT_ENTERED'))<>'NOT_ENTERED',1,0) hasmark
                        FROM campus_dynamics_portal.acad_course_registration cr
                       WHERE TRIM(cr.regno)=@r AND cr.acad_year=@a AND cr.semester=@s ORDER BY cr.courseID", conn))
                {
                    cmd.Parameters.AddWithValue("@r", regno); cmd.Parameters.AddWithValue("@a", acadYear); cmd.Parameters.AddWithValue("@s", sem);
                    using (var rdr = cmd.ExecuteReader())
                        while (rdr.Read())
                        {
                            courseCount++;
                            if (S(rdr, "hasmark") == "1") courseWithMarks++;
                            if (sampleCourses.Count < 12) sampleCourses.Add(S(rdr, "courseID"));
                        }
                }

                using (var cmd = new MySqlCommand(
                    @"SELECT SUM(trans_type='Bill') bills, SUM(trans_type='Payment') pays,
                             SUM(CASE WHEN trans_type='Bill' THEN amount ELSE 0 END) billamt,
                             SUM(CASE WHEN trans_type='Payment' THEN amount ELSE 0 END) payamt
                        FROM campus_dynamics_accounts.fin_studentfeestracking
                       WHERE TRIM(regno)=@r AND acadyear=@a AND semester=@s", conn))
                {
                    cmd.Parameters.AddWithValue("@r", regno); cmd.Parameters.AddWithValue("@a", acadYear); cmd.Parameters.AddWithValue("@s", sem);
                    using (var rdr = cmd.ExecuteReader())
                        if (rdr.Read())
                        {
                            sftBills = rdr["bills"] == DBNull.Value ? 0 : Convert.ToInt32(rdr["bills"]);
                            sftPays = rdr["pays"] == DBNull.Value ? 0 : Convert.ToInt32(rdr["pays"]);
                            billAmt = rdr["billamt"] == DBNull.Value ? 0 : Convert.ToDecimal(rdr["billamt"]);
                            payAmt = rdr["payamt"] == DBNull.Value ? 0 : Convert.ToDecimal(rdr["payamt"]);
                        }
                }

                using (var cmd = new MySqlCommand(
                    @"SELECT COUNT(*) FROM campus_dynamics_accounts.fin_ledger fl
                       WHERE fl.accountcode=@r AND EXISTS (
                             SELECT 1 FROM campus_dynamics_accounts.fin_studentfeestracking t
                              WHERE TRIM(t.regno)=@r AND t.acadyear=@a AND t.semester=@s
                                AND (fl.tracking_ref=t.TID OR fl.voucherNo=t.TID OR fl.folio=CONCAT('BillNo:',t.TID)))", conn))
                {
                    cmd.Parameters.AddWithValue("@r", regno); cmd.Parameters.AddWithValue("@a", acadYear); cmd.Parameters.AddWithValue("@s", sem);
                    var o = cmd.ExecuteScalar(); glCount = o == null || o == DBNull.Value ? 0 : Convert.ToInt32(o);
                }
            }

            json = js.Serialize(new
            {
                success = true,
                reg = new { regno, name, acadYear, studyYear = sy, semester = sem, status },
                courses = new { count = courseCount, withMarks = courseWithMarks, sample = sampleCourses },
                billings = new { sftCount = sftBills + sftPays, bills = sftBills, pays = sftPays, glCount, billAmt, payAmt }
            });
        }
        catch (Exception ex) { json = js.Serialize(new { success = false, message = ex.Message }); }
        WriteJsonEnd(json);
    }

    /// <summary>Executes the delete: backs up every affected row, then removes the registration plus
    /// (optionally) the semester's courses and the semester's fee bills/payments (+ their GL mirror).</summary>
    private void HandleDeleteRegExecute()
    {
        var js = new JavaScriptSerializer();
        string json;
        try
        {
            int id; int.TryParse(Request.Form["id"] ?? Request.QueryString["id"], out id);
            bool delCourses  = (Request.Form["courses"]  ?? "") == "1";
            bool delBillings = (Request.Form["billings"] ?? "") == "1";
            if (id <= 0) { WriteJsonEnd(js.Serialize(new { success = false, message = "Missing registration id." })); return; }

            string regno = "", acadYear = "", sem = "";
            int delCoursesN = 0, delSftN = 0, delGlN = 0;

            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT regno, acad_year, semester FROM campus_dynamics.acad_registration WHERE ID=@id LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read()) { WriteJsonEnd(js.Serialize(new { success = false, message = "Registration not found (already deleted?)." })); return; }
                        regno = S(rdr, "regno"); acadYear = S(rdr, "acad_year"); sem = S(rdr, "semester");
                    }
                }

                Action<string, string> run = (sql, tag) => { using (var c = new MySqlCommand(sql, conn)) { c.CommandTimeout = 120; c.Parameters.AddWithValue("@id", id); c.Parameters.AddWithValue("@r", regno); c.Parameters.AddWithValue("@a", acadYear); c.Parameters.AddWithValue("@s", sem); c.ExecuteNonQuery(); } };
                Func<string, int> runN = (sql) => { using (var c = new MySqlCommand(sql, conn)) { c.CommandTimeout = 120; c.Parameters.AddWithValue("@id", id); c.Parameters.AddWithValue("@r", regno); c.Parameters.AddWithValue("@a", acadYear); c.Parameters.AddWithValue("@s", sem); return c.ExecuteNonQuery(); } };

                const string glMatch = @" fl.accountcode=@r AND EXISTS (SELECT 1 FROM campus_dynamics_accounts.fin_studentfeestracking t WHERE TRIM(t.regno)=@r AND t.acadyear=@a AND t.semester=@s AND (fl.tracking_ref=t.TID OR fl.voucherNo=t.TID OR fl.folio=CONCAT('BillNo:',t.TID)))";

                // ---- 1) BACKUP everything first (recovery net) ----
                run("INSERT INTO campus_dynamics.acad_registration_regdel_bak SELECT * FROM campus_dynamics.acad_registration WHERE ID=@id", "reg");
                if (delCourses)
                    run("INSERT INTO campus_dynamics_portal.acad_course_registration_regdel_bak SELECT * FROM campus_dynamics_portal.acad_course_registration WHERE TRIM(regno)=@r AND acad_year=@a AND semester=@s", "crs");
                if (delBillings)
                {
                    run("INSERT INTO campus_dynamics_accounts.fin_studentfeestracking_regdel_bak SELECT * FROM campus_dynamics_accounts.fin_studentfeestracking WHERE TRIM(regno)=@r AND acadyear=@a AND semester=@s", "sft");
                    run("INSERT INTO campus_dynamics_accounts.fin_ledger_regdel_bak SELECT fl.* FROM campus_dynamics_accounts.fin_ledger fl WHERE" + glMatch, "gl");
                }

                // ---- 2) DELETE (fin_ledger MyISAM first while its tracking rows still exist) ----
                if (delBillings)
                {
                    delGlN  = runN("DELETE fl FROM campus_dynamics_accounts.fin_ledger fl WHERE" + glMatch);
                    delSftN = runN("DELETE FROM campus_dynamics_accounts.fin_studentfeestracking WHERE TRIM(regno)=@r AND acadyear=@a AND semester=@s");
                }
                if (delCourses)
                    delCoursesN = runN("DELETE FROM campus_dynamics_portal.acad_course_registration WHERE TRIM(regno)=@r AND acad_year=@a AND semester=@s");
                runN("DELETE FROM campus_dynamics.acad_registration WHERE ID=@id");

                // ---- 3) Log ----
                string actor = ""; try { actor = HttpContext.Current.User.Identity.Name; } catch { }
                using (var cmd = new MySqlCommand(
                    @"INSERT INTO campus_dynamics.acad_activity_log (user_id, page_function, par, comments, access_date)
                      VALUES (@u, 'StudentsRegistration:DeleteRegistration', @p, @c, NOW())", conn))
                {
                    cmd.Parameters.AddWithValue("@u", string.IsNullOrEmpty(actor) ? "system" : actor);
                    cmd.Parameters.AddWithValue("@p", (regno + "|" + acadYear + "|S" + sem));
                    cmd.Parameters.AddWithValue("@c", ("Deleted registration" + (delCourses ? " + " + delCoursesN + " courses" : "") + (delBillings ? " + " + delSftN + " bills/pays, " + delGlN + " GL" : "") + " (backed up to *_regdel_bak)"));
                    cmd.ExecuteNonQuery();
                }
            }

            json = js.Serialize(new { success = true, deletedCourses = delCoursesN, deletedBillings = delSftN, deletedGl = delGlN,
                message = "Registration deleted for " + regno + " (" + acadYear + " Sem " + sem + ")." });
        }
        catch (Exception ex) { json = js.Serialize(new { success = false, message = ex.Message }); }
        WriteJsonEnd(json);
    }

    private static string S(MySqlDataReader r, string c)
    { try { object o = r[c]; return o == null || o == DBNull.Value ? "" : o.ToString().Trim(); } catch { return ""; } }

    protected void btnDeleteReg_Click(object sender, EventArgs e)
    {
        int id = GetLinkButtonID(sender);
        if (id <= 0) return;
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                // Fetch the record details for the activity log
                string regno = "", acadYear = "";
                int semester = 0;
                using (var cmd = new MySqlCommand(
                    "SELECT regno, acad_year, semester FROM acad_registration WHERE ID=@id LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            regno    = rdr["regno"].ToString();
                            acadYear = rdr["acad_year"].ToString();
                            semester = Convert.ToInt32(rdr["semester"]);
                        }
                    }
                }

                if (string.IsNullOrEmpty(regno))
                {
                    ShowToast(false, "Registration record not found.");
                    return;
                }

                // Delete the registration record
                using (var cmd = new MySqlCommand("DELETE FROM acad_registration WHERE ID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    bool ok = cmd.ExecuteNonQuery() > 0;
                    if (ok)
                    {
                        // Log the deletion
                        using (var logCmd = new MySqlCommand(
                            "INSERT INTO acad_activity_log (user_id, page_function, par, comments, access_date) VALUES (@usr, 'Delete Registration', @par, 'Deleted registration record', NOW())", conn))
                        {
                            logCmd.Parameters.AddWithValue("@usr", GetCurrentUser());
                            logCmd.Parameters.AddWithValue("@par", string.Format("{0} | {1} Sem {2}", regno, acadYear, semester));
                            logCmd.ExecuteNonQuery();
                        }
                        ShowToast(true, string.Format("Registration deleted for {0} ({1} Sem {2}).", regno, acadYear, semester));
                    }
                    else
                    {
                        ShowToast(false, "Could not delete registration record.");
                    }
                }
            }
        }
        catch (Exception ex) { ShowToast(false, "Delete failed: " + ex.Message); }
        LoadStats(); BindGrid();
    }

    // ===================================================================
    // BATCH ACTIONS
    // ===================================================================

    protected void btnBatchClear_Click(object sender, EventArgs e)             { RunBatch("clear");        }
    protected void btnBatchUndoReg_Click(object sender, EventArgs e)           { RunBatch("undoreg");      }
    protected void btnBatchUndoClear_Click(object sender, EventArgs e)         { RunBatch("undoclear");    }
    protected void btnBatchDiscontinue_Click(object sender, EventArgs e)       { RunBatch("discontinue"); }
    protected void btnBatchHalt_Click(object sender, EventArgs e)              { RunBatch("halt");         }
    protected void btnBatchDeadYear_Click(object sender, EventArgs e)          { RunBatch("deadyear");     }
    protected void btnBatchReactivate_Click(object sender, EventArgs e)        { RunBatch("reactivate");   }
    protected void btnBatchDelete_Click(object sender, EventArgs e)             { RunBatch("delete");       }

    private void RunBatch(string action)
    {
        string idsRaw = (hdnBatchIds.Value ?? "").Trim();
        hdnBatchIds.Value = "";
        if (string.IsNullOrEmpty(idsRaw))
        {
            ShowToast(false, "No students selected.");
            return;
        }
        var idList = new List<int>();
        foreach (string s in idsRaw.Split(','))
        {
            int id;
            if (int.TryParse(s.Trim(), out id) && id > 0)
                idList.Add(id);
        }
        if (idList.Count == 0)
        {
            ShowToast(false, "No students selected.");
            return;
        }
        int processed = 0, skipped = 0;
        foreach (int id in idList)
        {
            bool ok = false;
            switch (action)
            {
                case "clear":       ok = ClearStudent(id);  break;
                case "undoreg":     ok = BatchUndoReg(id);  break;
                case "undoclear":   ok = BatchUndoClear(id); break;
                case "discontinue": ok = ForceStatus(id, "DISCONTINUED"); break;
                case "halt":        ok = ForceStatus(id, "HALTED");       break;
                case "deadyear":    ok = ForceStatus(id, "DEAD YEAR");    break;
                case "reactivate":  ok = BatchReactivate(id);             break;
                case "delete":      ok = BatchDeleteReg(id);              break;
            }
            if (ok) processed++; else skipped++;
        }
        string msg = processed + (action == "delete" ? " registration(s) deleted." : " student(s) updated.");
        if (skipped > 0) msg += " " + skipped + " skipped.";
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

        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                // Check student exists
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

                // Check duplicate
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

                // Insert
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

            // Close modal and show toast
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
        // Re-open modal WITHOUT hiding the error div
        ScriptManager.RegisterStartupScript(this, GetType(), "reopenAdd",
            "document.getElementById('addRegModal').classList.add('open');", true);
    }

    // ===================================================================
    // EDIT REGISTRATION RECORD MODAL  (full admin adjustment)
    // ===================================================================

    protected void btnDoEditReg_Click(object sender, EventArgs e)
    {
        int id = SafeInt(hdnEditID.Value, 0);
        if (id <= 0) { ShowEditError("Invalid record ID."); return; }

        string acadYear  = ddlEditAcadYear.SelectedValue;
        int    semester  = SafeInt(ddlEditSemester.SelectedValue, 0);
        int    studyYear = SafeInt(ddlEditStudyYear.SelectedValue, 0);
        string regStatus = (ddlEditRegStatus.SelectedValue ?? "").Trim();
        string residence = (ddlEditResidence.SelectedValue ?? "NON-RESIDENT").Trim();
        string idCard    = (ddlEditIDCard.SelectedValue == "ISSUED") ? "ISSUED" : "NOT ISSUED";
        string examClear = (ddlEditExamClearance.SelectedValue ?? "UNCLEARED").Trim();
        string clearDate = (hdnEditClearanceDate.Value ?? "").Trim();   // yyyy-MM-dd or ""

        if (string.IsNullOrEmpty(acadYear)) { ShowEditError("Please select an academic year."); return; }
        if (semester < 1 || semester > 3)   { ShowEditError("Please select a valid semester."); return; }
        if (studyYear < 1 || studyYear > 9) { ShowEditError("Please select a valid study year."); return; }
        if (string.IsNullOrEmpty(regStatus)) { ShowEditError("Please select a registration status."); return; }

        // Normalise a clearance date if needed
        DateTime parsedDate;
        bool haveDate = DateTime.TryParse(clearDate, out parsedDate);

        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                // Load the current record
                string curRegno = "", curYear = "";
                int curSem = 0, curStudyYear = 0;
                using (var cmd = new MySqlCommand(
                    "SELECT regno, acad_year, semester, COALESCE(studyyear,0) studyyear FROM acad_registration WHERE ID=@id LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read()) { ShowEditError("Registration record not found."); return; }
                        curRegno = rdr["regno"].ToString();
                        curYear  = rdr["acad_year"].ToString();
                        curSem   = Convert.ToInt32(rdr["semester"]);
                        curStudyYear = Convert.ToInt32(rdr["studyyear"]);
                    }
                }

                // Guard against creating a duplicate for the same student + period
                bool periodChanged = !string.Equals(curYear, acadYear, StringComparison.OrdinalIgnoreCase) || curSem != semester;
                if (periodChanged)
                {
                    using (var dup = new MySqlCommand(
                        "SELECT COUNT(*) FROM acad_registration WHERE regno=@r AND acad_year=@y AND semester=@s AND ID<>@id", conn))
                    {
                        dup.Parameters.AddWithValue("@r", curRegno);
                        dup.Parameters.AddWithValue("@y", acadYear);
                        dup.Parameters.AddWithValue("@s", semester);
                        dup.Parameters.AddWithValue("@id", id);
                        if (Convert.ToInt64(dup.ExecuteScalar()) > 0)
                        {
                            ShowEditError(string.Format(
                                "Student {0} already has a record for {1}, Semester {2}. Duplicate entries are not allowed.",
                                Server.HtmlEncode(curRegno), Server.HtmlEncode(acadYear), semester));
                            return;
                        }
                    }

                    // A course the student ALREADY holds at the destination would collide with
                    // the one being moved there. Refuse the whole edit and name the courses,
                    // rather than move some and silently drop the rest.
                    string clash = CourseClashAtTarget(curRegno, curYear, curSem, acadYear, semester);
                    if (clash.Length > 0)
                    {
                        ShowEditError(string.Format(
                            "Cannot move this registration to {0} Semester {1}: the student already has {2} there. "
                            + "Resolve the duplicate course registration(s) first.",
                            Server.HtmlEncode(acadYear), semester, Server.HtmlEncode(clash)));
                        return;
                    }
                }

                // Resolve exam-clearance side effects
                bool isCleared = (examClear == "CLEARED" || examClear == "PRINTED");
                string user = GetCurrentUser();

                var sql = new StringBuilder("UPDATE acad_registration SET ");
                sql.Append("acad_year=@ay, semester=@sem, studyyear=@sy, regstatus=@rs, ");
                sql.Append("residence_status=@res, id_cardStatus=@idc, examClearance=@ec");

                if (isCleared)
                {
                    if (haveDate) sql.Append(", examClearanceDate=@ecdate");
                    else          sql.Append(", examClearanceDate=COALESCE(examClearanceDate, NOW())");
                    sql.Append(", clearedBy=COALESCE(NULLIF(clearedBy,''), @user)");
                }
                else
                {
                    sql.Append(", examClearanceDate=NULL, clearedBy=NULL");
                }

                // Registration auditing column
                if (regStatus == "REGISTERED" || regStatus == "LATE REGISTERED" || regStatus == "CLEARED")
                    sql.Append(", registeredBy=COALESCE(NULLIF(registeredBy,''), @user)");
                else if (regStatus == "UNREGISTERED")
                    sql.Append(", registeredBy=NULL");

                sql.Append(" WHERE ID=@id");

                bool ok;
                using (var cmd = new MySqlCommand(sql.ToString(), conn))
                {
                    cmd.Parameters.AddWithValue("@ay",  acadYear);
                    cmd.Parameters.AddWithValue("@sem", semester);
                    cmd.Parameters.AddWithValue("@sy",  studyYear);
                    cmd.Parameters.AddWithValue("@rs",  regStatus);
                    cmd.Parameters.AddWithValue("@res", residence);
                    cmd.Parameters.AddWithValue("@idc", idCard);
                    cmd.Parameters.AddWithValue("@ec",  examClear);
                    if (isCleared && haveDate)
                        cmd.Parameters.AddWithValue("@ecdate", parsedDate.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@user", user);
                    cmd.Parameters.AddWithValue("@id",  id);
                    ok = cmd.ExecuteNonQuery() >= 0;   // >=0: update may set identical values (0 rows) yet still succeed
                }

                // ── Harmonise everything keyed to the sitting that just moved ──────────
                // acad_course_registration and acad_results are both keyed on
                // (regno, acad_year, semester). Moving the registration on its own left them
                // behind at the old sitting: the student's courses and published marks became
                // detached from the registration they belong to, which is why editing a
                // registration looked like it deleted the course registrations.
                string harmonised = "";
                if (periodChanged || studyYear != curStudyYear)
                    harmonised = HarmoniseSitting(curRegno, curYear, curSem, acadYear, semester, studyYear, user);

                // Audit log
                using (var logCmd = new MySqlCommand(
                    "INSERT INTO acad_activity_log (user_id, page_function, par, comments, access_date) VALUES (@usr, 'Edit Registration', @par, @cmt, NOW())", conn))
                {
                    logCmd.Parameters.AddWithValue("@usr", user);
                    logCmd.Parameters.AddWithValue("@par", string.Format("{0} | {1} Sem {2}", curRegno, acadYear, semester));
                    string cmt = string.Format("Status={0}; StudyYr={1}; Res={2}; IDCard={3}; ExamClr={4}", regStatus, studyYear, residence, idCard, examClear);
                    if (periodChanged) cmt += string.Format("; Moved from {0} Sem {1}", curYear, curSem);
                    if (harmonised.Length > 0) cmt += "; " + harmonised;
                    logCmd.Parameters.AddWithValue("@cmt", cmt.Length > 200 ? cmt.Substring(0, 200) : cmt);
                    logCmd.ExecuteNonQuery();
                }

                // Auto-bill if now Registered / Late / Cleared and not yet billed
                string billWarn = null;
                if (regStatus == "REGISTERED" || regStatus == "LATE REGISTERED" || regStatus == "CLEARED")
                    billWarn = AutoBillStudent(id);

                ScriptManager.RegisterStartupScript(this, GetType(), "closeEdit", "closeModal('editRegModal');", true);
                // Say what moved with the registration — an operator who changes a semester
                // needs to see that the courses and marks came too, not hope that they did.
                ShowToast(ok, harmonised.Length > 0
                    ? "Registration record updated — " + harmonised + "."
                    : "Registration record updated.");
                if (!string.IsNullOrEmpty(billWarn))
                    ScriptManager.RegisterStartupScript(this, GetType(), "editBillWarn",
                        "setTimeout(function(){showToast(false,'" + billWarn.Replace("'", "\\'") + "');},700);", true);
            }
        }
        catch (Exception ex) { ShowEditError("Update failed: " + ex.Message); return; }

        LoadStats();
        BindGrid();
    }

    private void ShowEditError(string msg)
    {
        editResult.Visible = true;
        editResult.Attributes["class"] = "hr-result hr-result--err";
        litEditResult.Text = Server.HtmlEncode(msg);
        ScriptManager.RegisterStartupScript(this, GetType(), "reopenEdit",
            "document.getElementById('editRegModal').classList.add('open');", true);
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
            SELECT
                COALESCE(NULLIF(TRIM(sreg.regno), ''), NULLIF(TRIM(semail.regno), ''), TRIM(r.regno)) AS regno,
                NULLIF(TRIM(CONCAT_WS(' ', NULLIF(COALESCE(sreg.firstname, semail.firstname), ''), NULLIF(COALESCE(sreg.othername, semail.othername), ''))), '') AS student_name,
                COALESCE(NULLIF(sreg.progid, ''), NULLIF(semail.progid, ''), '') AS progcode,
                COALESCE(NULLIF(preg.progname, ''), NULLIF(pemail.progname, ''), '') AS progname,
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
            LEFT JOIN acad_student sreg ON sreg.regno=r.regno
            LEFT JOIN (
                SELECT LOWER(TRIM(email)) AS email_key, MIN(regno) AS regno
                FROM acad_student
                WHERE IFNULL(email, '') <> ''
                GROUP BY LOWER(TRIM(email))
                HAVING COUNT(*) = 1
            ) su ON su.email_key = LOWER(TRIM(r.regno))
            LEFT JOIN acad_student semail ON semail.regno = su.regno
            LEFT JOIN acad_programme preg ON sreg.progid=preg.progcode
            LEFT JOIN acad_programme pemail ON semail.progid=pemail.progcode
            LEFT JOIN (
                SELECT regno, acadyear, semester,
                    COUNT(CASE WHEN trans_type='Bill' THEN 1 END) AS bill_count,
                    SUM(CASE WHEN trans_type='Bill' THEN amount ELSE 0 END) AS total_billed,
                    SUM(CASE WHEN trans_type='Payment' THEN amount ELSE 0 END) AS total_paid
                FROM campus_dynamics_accounts.fin_studentfeestracking
                GROUP BY regno, acadyear, semester
            ) b ON b.regno=COALESCE(NULLIF(TRIM(sreg.regno), ''), NULLIF(TRIM(semail.regno), ''), TRIM(r.regno))
               AND b.acadyear=r.acad_year AND b.semester=r.semester
            WHERE 1=1");

        var parameters = new List<MySqlParameter>();
        if (!string.IsNullOrEmpty(acadYear)) { sb.Append(" AND r.acad_year=@acadYear"); parameters.Add(new MySqlParameter("@acadYear", acadYear)); }
        if (!string.IsNullOrEmpty(semStr))   { sb.Append(" AND r.semester=@semester");  parameters.Add(new MySqlParameter("@semester", SafeInt(semStr, 1))); }
        if (!string.IsNullOrEmpty(ddlStudyYear.SelectedValue))     { sb.Append(" AND r.studyyear=@sy"); parameters.Add(new MySqlParameter("@sy", SafeInt(ddlStudyYear.SelectedValue, 0))); }
        if (!string.IsNullOrEmpty(ddlRegStatus.SelectedValue))     { sb.Append(" AND r.regstatus=@rs"); parameters.Add(new MySqlParameter("@rs", ddlRegStatus.SelectedValue)); }
        if (!string.IsNullOrEmpty(ddlProgramme.SelectedValue))     { sb.Append(" AND COALESCE(NULLIF(sreg.progid, ''), NULLIF(semail.progid, ''))=@prog"); parameters.Add(new MySqlParameter("@prog", ddlProgramme.SelectedValue)); }
        if (!string.IsNullOrEmpty(ddlExamClearance.SelectedValue)) { sb.Append(" AND r.examClearance=@ec"); parameters.Add(new MySqlParameter("@ec", ddlExamClearance.SelectedValue)); }
        if (!string.IsNullOrEmpty(ddlIDCard.SelectedValue))        { if (ddlIDCard.SelectedValue == "ISSUED") sb.Append(" AND r.id_cardStatus='ISSUED'"); else sb.Append(" AND (r.id_cardStatus IS NULL OR r.id_cardStatus!='ISSUED')"); }
        if (!string.IsNullOrEmpty(ddlResidence.SelectedValue))     { if (ddlResidence.SelectedValue == "RESIDENT") sb.Append(" AND r.residence_status='RESIDENT'"); else sb.Append(" AND r.residence_status!='RESIDENT'"); }
        if (!string.IsNullOrEmpty(ddlBilling.SelectedValue))        { if (ddlBilling.SelectedValue == "BILLED") sb.Append(" AND r.regstatus IN ('REGISTERED','LATE REGISTERED','CLEARED') AND EXISTS(SELECT 1 FROM campus_dynamics_accounts.fin_studentfeestracking ft WHERE ft.regno=COALESCE(NULLIF(TRIM(sreg.regno), ''), NULLIF(TRIM(semail.regno), ''), TRIM(r.regno)) AND ft.acadyear=r.acad_year AND ft.semester=r.semester AND ft.trans_type='Bill')"); else sb.Append(" AND r.regstatus IN ('REGISTERED','LATE REGISTERED','CLEARED') AND NOT EXISTS(SELECT 1 FROM campus_dynamics_accounts.fin_studentfeestracking ft WHERE ft.regno=COALESCE(NULLIF(TRIM(sreg.regno), ''), NULLIF(TRIM(semail.regno), ''), TRIM(r.regno)) AND ft.acadyear=r.acad_year AND ft.semester=r.semester AND ft.trans_type='Bill')"); }
        if (!string.IsNullOrEmpty(search))                         { sb.Append(" AND (COALESCE(NULLIF(TRIM(sreg.regno), ''), NULLIF(TRIM(semail.regno), ''), TRIM(r.regno)) LIKE @s OR TRIM(CONCAT_WS(' ', NULLIF(COALESCE(sreg.firstname, semail.firstname), ''), NULLIF(COALESCE(sreg.othername, semail.othername), ''))) LIKE @s)"); parameters.Add(new MySqlParameter("@s", "%" + search + "%")); }
        sb.Append(" ORDER BY COALESCE(sreg.firstname, semail.firstname), COALESCE(sreg.othername, semail.othername), COALESCE(NULLIF(TRIM(sreg.regno), ''), NULLIF(TRIM(semail.regno), ''), TRIM(r.regno))");

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
                        string filename = string.Format("registration_{0}_{1}.csv", yearPart, semPart);
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
    // DATABASE OPERATION PRIMITIVES
    // ===================================================================

    /// <summary>
    /// Sets regstatus to newStatus and updates an auditing column, but only if the
    /// current regstatus matches the allowed value.
    /// </summary>
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

                using (var chk = new MySqlCommand(
                    "SELECT COUNT(*) FROM fin_studentfeestracking WHERE TRIM(regno)=TRIM(@r) AND acadyear=@a AND semester=@s AND trans_type='Bill'", conn))
                {
                    chk.Parameters.AddWithValue("@r", regno);
                    chk.Parameters.AddWithValue("@a", acadYear);
                    chk.Parameters.AddWithValue("@s", semester);
                    if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                        return null;
                }
            }

            string warn = "Billing could not be confirmed for " + regno + " (" + acadYear + " Sem " + semester + "). Please use Fix Billing or Bill Student to complete.";
            LogBillingError(regno, acadYear, semester, warn, "AutoBill-NoRows");
            return warn;
        }
        catch (Exception ex)
        {
            string warn = "Auto-billing failed for " + regno + " (" + acadYear + " Sem " + semester + "): " + ex.Message;
            try { LogBillingError(regno, acadYear, semester, warn, "AutoBill-Exception"); } catch { }
            return warn;
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
        catch { }
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

    // ===================================================================
    //  SITTING HARMONISATION
    //
    //  A semester registration is not a standalone row. Two other tables are
    //  keyed on exactly the same (regno, acad_year, semester):
    //
    //     campus_dynamics_portal.acad_course_registration  — the courses taken
    //     campus_dynamics.acad_results                     — the marks published
    //
    //  Editing the registration's year or semester used to move only the
    //  registration, leaving both behind at the sitting it came from. The
    //  courses stopped belonging to any registration and the marks stopped
    //  belonging to the semester they were earned in — which is what looked,
    //  from the screen, like the course registrations had been deleted.
    //
    //  Both are now carried across with it, in one transaction on a connection
    //  that can reach all three databases.
    // ===================================================================

    /// <summary>
    /// Courses the student already holds at the destination sitting that would collide with
    /// the ones being moved there. Empty when the move is clean.
    /// </summary>
    private string CourseClashAtTarget(string regno, string fromYear, int fromSem, string toYear, int toSem)
    {
        try
        {
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    @"SELECT GROUP_CONCAT(DISTINCT dst.courseID ORDER BY dst.courseID SEPARATOR ', ')
                        FROM campus_dynamics_portal.acad_course_registration dst
                        JOIN campus_dynamics_portal.acad_course_registration src
                          ON src.regno = dst.regno AND src.courseID = dst.courseID
                       WHERE dst.regno=@r AND dst.acad_year=@ty AND dst.semester=@ts
                         AND src.acad_year=@fy AND src.semester=@fs", conn))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    cmd.Parameters.AddWithValue("@ty", toYear);
                    cmd.Parameters.AddWithValue("@ts", toSem);
                    cmd.Parameters.AddWithValue("@fy", fromYear);
                    cmd.Parameters.AddWithValue("@fs", fromSem);
                    object v = cmd.ExecuteScalar();
                    return v == null || v == DBNull.Value ? "" : v.ToString();
                }
            }
        }
        catch { return ""; }   // a failed check must not block a legitimate edit
    }

    /// <summary>
    /// Moves the course registrations and published results of a sitting to the sitting the
    /// registration has just been changed to, and keeps the study year on the results in step.
    /// Returns a short summary for the audit log.
    /// </summary>
    private string HarmoniseSitting(string regno, string fromYear, int fromSem,
                                    string toYear, int toSem, int toStudyYear, string user)
    {
        int courses = 0, results = 0;
        bool periodMoved = !string.Equals(fromYear, toYear, StringComparison.OrdinalIgnoreCase) || fromSem != toSem;

        try
        {
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        if (periodMoved)
                        {
                            using (var cmd = new MySqlCommand(
                                @"UPDATE campus_dynamics_portal.acad_course_registration
                                     SET acad_year=@ty, semester=@ts
                                   WHERE regno=@r AND acad_year=@fy AND semester=@fs", conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@ty", toYear);
                                cmd.Parameters.AddWithValue("@ts", toSem);
                                cmd.Parameters.AddWithValue("@r", regno);
                                cmd.Parameters.AddWithValue("@fy", fromYear);
                                cmd.Parameters.AddWithValue("@fs", fromSem);
                                courses = cmd.ExecuteNonQuery();
                            }

                            // Published marks move with the sitting, or the transcript would
                            // keep reporting them under a semester the student is no longer
                            // registered for.
                            using (var cmd = new MySqlCommand(
                                @"UPDATE campus_dynamics.acad_results
                                     SET acad=@ty, semester=@ts, studyyear=@sy
                                   WHERE TRIM(regno)=@r AND TRIM(acad)=@fy AND semester=@fs", conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@ty", toYear);
                                cmd.Parameters.AddWithValue("@ts", toSem);
                                cmd.Parameters.AddWithValue("@sy", toStudyYear);
                                cmd.Parameters.AddWithValue("@r", regno.Trim());
                                cmd.Parameters.AddWithValue("@fy", fromYear.Trim());
                                cmd.Parameters.AddWithValue("@fs", fromSem);
                                results = cmd.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            // Only the study year changed — the sitting stays put, but the
                            // results' year label has to follow it.
                            using (var cmd = new MySqlCommand(
                                @"UPDATE campus_dynamics.acad_results SET studyyear=@sy
                                   WHERE TRIM(regno)=@r AND TRIM(acad)=@ty AND semester=@ts", conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@sy", toStudyYear);
                                cmd.Parameters.AddWithValue("@r", regno.Trim());
                                cmd.Parameters.AddWithValue("@ty", toYear.Trim());
                                cmd.Parameters.AddWithValue("@ts", toSem);
                                results = cmd.ExecuteNonQuery();
                            }
                        }

                        tx.Commit();
                    }
                    catch { try { tx.Rollback(); } catch { } throw; }
                }
            }
        }
        catch (Exception ex)
        {
            return "harmonise FAILED: " + ex.Message;
        }

        if (courses == 0 && results == 0) return "";
        return periodMoved
            ? string.Format("moved {0} course reg(s) + {1} result(s) to {2} Sem {3}", courses, results, toYear, toSem)
            : string.Format("restamped {0} result(s) to study year {1}", results, toStudyYear);
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

    private bool BatchDeleteReg(int id)
    {
        try
        {
            using (var conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                // Fetch details for audit log
                string regno = "", acadYear = "";
                int semester = 0;
                using (var cmd = new MySqlCommand("SELECT regno, acad_year, semester FROM acad_registration WHERE ID=@id LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            regno    = rdr["regno"].ToString();
                            acadYear = rdr["acad_year"].ToString();
                            semester = Convert.ToInt32(rdr["semester"]);
                        }
                    }
                }
                if (string.IsNullOrEmpty(regno)) return false;

                using (var cmd = new MySqlCommand("DELETE FROM acad_registration WHERE ID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    bool ok = cmd.ExecuteNonQuery() > 0;
                    if (ok)
                    {
                        using (var logCmd = new MySqlCommand(
                            "INSERT INTO acad_activity_log (user_id, page_function, par, comments, access_date) VALUES (@usr, 'Batch Delete Registration', @par, 'Deleted registration record', NOW())", conn))
                        {
                            logCmd.Parameters.AddWithValue("@usr", GetCurrentUser());
                            logCmd.Parameters.AddWithValue("@par", string.Format("{0} | {1} Sem {2}", regno, acadYear, semester));
                            logCmd.ExecuteNonQuery();
                        }
                    }
                    return ok;
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
        return string.IsNullOrEmpty(s) ? "0" : s;
    }

    private string FormatCount(object val)
    {
        if (val == null || val == DBNull.Value) return "0";
        long n;
        if (long.TryParse(val.ToString(), out n)) return n.ToString("N0");
        return val.ToString();
    }

    /// <summary>Escape a value for safe inclusion inside a JS single-quoted string literal.</summary>
    protected string JsEncode(object val)
    {
        string s = (val == null || val == DBNull.Value) ? "" : val.ToString();
        return s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "");
    }
}
