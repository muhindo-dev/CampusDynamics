using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.Security;
using MySql.Data.MySqlClient;

/// <summary>
/// Employees: directory, profile (Details, Contracts, Leave, Payroll), add and edit, photo,
/// one "Reset login" action and the staff register export. Contract columns use each
/// employee's current contract, hr_current_contract_id(empID). "Active" means that contract
/// is VALID and has not passed its end date.
/// </summary>
public partial class COOPERP_NewScreens_HREmployees : System.Web.UI.Page
{
    protected string QsSort
    {
        get
        {
            string sort = (Request.QueryString["sort"] ?? string.Empty).Trim().ToLower();
            switch (sort)
            {
                case "name": case "code": case "type": case "dept": case "job":
                case "status": case "pay": case "contractend":
                    return sort;
                default:
                    return string.Empty;
            }
        }
    }

    protected string QsSortDir
    {
        get { return string.Equals(Request.QueryString["dir"], "DESC", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC"; }
    }

    /// <summary>Status filter: ACTIVE, NOVALID, DUP or empty (older links: VALID, NONE).</summary>
    private string QsStatus
    {
        get
        {
            string s = (Request.QueryString["status"] ?? "").Trim().ToUpperInvariant();
            if (s == "VALID") return "ACTIVE";
            if (s == "NONE") return "NOVALID";
            return (s == "ACTIVE" || s == "NOVALID" || s == "DUP") ? s : "";
        }
    }

    private int QsPage { get { int p; return int.TryParse(Request.QueryString["page"], out p) && p > 0 ? p : 1; } }

    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    private const string ActiveSql = "(c.contractStatus = 'VALID' AND (c.contractEnd IS NULL OR c.contractEnd >= CURDATE()))";
    private const string DupCodesSql =
        "SELECT EMP_CODE FROM (SELECT EMP_CODE FROM hrm_employee WHERE EMP_CODE IS NOT NULL AND EMP_CODE NOT IN ('','-') GROUP BY EMP_CODE HAVING COUNT(*) > 1) dup";

    private TextBox txtNewName { get { return txtEmpName; } }
    private TextBox txtNewEmail { get { return txtEmpEmail; } }
    private TextBox txtNewPhone { get { return txtEmpPhone; } }
    private TextBox txtNewDOB { get { return txtEmpDOB; } }
    private DropDownList ddlNewGender { get { return ddlGender; } }
    private TextBox txtNewQualifications { get { return txtQualifications; } }
    private DropDownList ddlNewEducation { get { return ddlEducation; } }
    private DropDownList ddlNewNationality { get { return ddlNationality; } }
    private DropDownList ddlNewType { get { return ddlEmpType; } }
    private DropDownList ddlNewMarital { get { return ddlMarital; } }
    private TextBox txtNewAddress { get { return txtAddress; } }
    private TextBox txtNewResidence { get { return txtResidence; } }
    private TextBox txtNewReligion { get { return txtReligion; } }
    private TextBox txtNewTribe { get { return txtTribe; } }
    private TextBox txtNewTIN { get { return txtTIN; } }
    private TextBox txtNewNSSF { get { return txtNSSF; } }
    private DropDownList ddlNewBank { get { return ddlBank; } }
    private TextBox txtNewBankAccount { get { return txtBankAccount; } }
    private TextBox txtNewSpouse { get { return txtSpouse; } }
    private TextBox txtNewChildren { get { return txtChildren; } }
    private TextBox txtNewFather { get { return txtFather; } }
    private TextBox txtNewMother { get { return txtMother; } }
    private TextBox txtNewContactPerson { get { return txtContactPerson; } }
    private TextBox txtNewRelation { get { return txtRelation; } }
    private TextBox txtNewContactPhone { get { return txtContactPhone; } }
    private TextBox txtNewReferee1 { get { return txtReferee1; } }
    private TextBox txtNewReferee2 { get { return txtReferee2; } }
    private TextBox txtNewMedical { get { return txtMedical; } }
    private TextBox txtNewSchooling { get { return txtSchooling; } }
    private TextBox txtNewEmployment { get { return txtEmploymentHist; } }
    private TextBox txtNewEntryYear { get { return txtEntryYear; } }
    private DropDownList ddlNewStation { get { return ddlStation; } }

    protected void Page_Init(object sender, EventArgs e)
    {
        string action = Request.QueryString["ajax"];
        if (string.IsNullOrEmpty(action))
            action = Request.QueryString["action"];

        if (!string.IsNullOrEmpty(action))
        {
            // These handlers run before SidebarMaster's login check: HR access is required here
            // (they include login resets and an export with pay data).
            bool isExport = string.Equals(action, "export_employees", StringComparison.OrdinalIgnoreCase);
            if (!HrAccess.RequireHr(!isExport)) return;

            if (isExport)
            {
                SendStaffRegister();
                return;
            }

            HandleAjaxAction(action);
            return;
        }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        if (!HrAccess.RequireHr(false)) return;

        // Lists the add/edit dialog posts back against: rebuilt on every request.
        LoadFormLists();
        if (!IsPostBack)
        {
            LoadFilterDropdowns();
            ApplyFiltersFromQueryString();
        }
        LoadStats();
        BindEmployeeGrid();
    }

    private void ApplyFiltersFromQueryString()
    {
        txtSearch.Text = (Request.QueryString["q"] ?? string.Empty).Trim();
        SetSelectedValue(ddlFilterStatus, QsStatus);
        SetSelectedValue(ddlFilterType, Request.QueryString["type"]);
        SetSelectedValue(ddlFilterDept, Request.QueryString["dept"]);
        SetSelectedValue(ddlFilterStation, Request.QueryString["station"]);
        SetSelectedValue(ddlPageSize, Request.QueryString["sz"]);
    }

    private void SetSelectedValue(ListControl control, string value)
    {
        if (control == null || string.IsNullOrEmpty(value)) return;
        ListItem item = control.Items.FindByValue(value);
        if (item != null) control.SelectedValue = value;
    }

    #region Directory

    /// <summary>FROM/JOIN/WHERE shared by the directory and the staff register.</summary>
    private string DirectoryFrom(List<MySqlParameter> parms, string search, string dept, string station, string empType, string status)
    {
        StringBuilder sql = new StringBuilder(@"
        FROM hrm_employee e
        LEFT JOIN hrm_emp_contracts c ON c.ID = hr_current_contract_id(e.empID)
        LEFT JOIN hrm_departments d ON d.ID = c.departmentID
        LEFT JOIN hrm_stations st ON st.ID = e.Entry_Satation
        LEFT JOIN hrm_payscales ps ON ps.ID = c.payscale
        LEFT JOIN hrm_jobs j ON j.ID = c.jobID
        WHERE 1=1 ");

        if (!string.IsNullOrEmpty(search))
        {
            sql.Append(" AND (e.emp_name LIKE @search OR e.EMP_CODE LIKE @search OR e.emp_email LIKE @search OR e.emp_phone LIKE @search OR e.usernames LIKE @search) ");
            parms.Add(new MySqlParameter("@search", "%" + search + "%"));
        }
        if (!string.IsNullOrEmpty(dept))
        {
            sql.Append(" AND c.departmentID = @dept ");
            parms.Add(new MySqlParameter("@dept", dept));
        }
        if (!string.IsNullOrEmpty(station))
        {
            sql.Append(" AND e.Entry_Satation = @station ");
            parms.Add(new MySqlParameter("@station", station));
        }
        if (!string.IsNullOrEmpty(empType))
        {
            sql.Append(" AND e.EmpType = @empType ");
            parms.Add(new MySqlParameter("@empType", empType));
        }
        if (status == "ACTIVE") sql.Append(" AND " + ActiveSql + " ");
        else if (status == "NOVALID") sql.Append(" AND NOT IFNULL(" + ActiveSql + ", 0) ");
        else if (status == "DUP") sql.Append(" AND e.EMP_CODE IN (" + DupCodesSql + ") ");
        return sql.ToString();
    }

    private void BindEmployeeGrid()
    {
        List<MySqlParameter> parms = new List<MySqlParameter>();
        string from = DirectoryFrom(parms, txtSearch.Text.Trim(), ddlFilterDept.SelectedValue, ddlFilterStation.SelectedValue,
            ddlFilterType.SelectedValue, ddlFilterStatus.SelectedValue);

        int pageSize = 50;
        int.TryParse(ddlPageSize.SelectedValue, out pageSize);
        if (pageSize <= 0) pageSize = 50;

        int total = 0;
        DataTable cnt = ExecuteQuery("SELECT COUNT(*) " + from, CloneParams(parms));
        if (cnt.Rows.Count > 0) total = Convert.ToInt32(cnt.Rows[0][0]);
        int totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        int page = Math.Min(QsPage, totalPages);
        int offset = (page - 1) * pageSize;

        DataTable dt = ExecuteQuery(@"SELECT e.empID, e.EMP_CODE, e.emp_name, e.emp_email, e.emp_phone, e.EmpType,
                d.dept_name, st.station_name, c.ID AS contractID, c.contractStatus, c.contractStart, c.contractEnd,
                ps.scale_name, IFNULL(ps.basicpay, c.fixedamount) AS basicpay, j.jobname " + from +
            " ORDER BY " + GetOrderByClause() + " LIMIT " + pageSize + " OFFSET " + offset, CloneParams(parms));

        HashSet<string> duplicateCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (DataRow dr in ExecuteQuery(DupCodesSql).Rows) duplicateCodes.Add(dr["EMP_CODE"].ToString());
        }
        catch (Exception ex) { System.Diagnostics.Trace.TraceWarning("HREmployees duplicates: " + ex.Message); }

        StringBuilder body = new StringBuilder();
        foreach (DataRow r in dt.Rows)
        {
            string empID = SafeVal(r["empID"]);
            string empCode = Clean(r["EMP_CODE"]);
            string sub = JoinNonEmpty(UsableEmail(r["emp_email"]), UsablePhone(r["emp_phone"]));
            bool isDup = empCode != "" && duplicateCodes.Contains(empCode);

            body.Append("<tr class='is-click' onclick=\"openEmployeeProfile(").Append(empID).Append(")\">");
            body.Append("<td><strong>").Append(Enc(r["emp_name"])).Append("</strong><span class='hr-sub'>").Append(Enc(sub)).Append("</span></td>");
            body.Append("<td style='white-space:nowrap'>").Append(Enc(empCode))
                .Append(isDup ? " <span class='hr-badge hr-badge--warn' title='Another employee has the same staff number'>Duplicate</span>" : "").Append("</td>");
            body.Append("<td>").Append(Enc(r["EmpType"])).Append("</td>");
            body.Append("<td>").Append(Enc(r["dept_name"])).Append("</td>");
            body.Append("<td>").Append(Enc(r["jobname"])).Append("</td>");
            body.Append("<td>").Append(ContractBadge(r["contractID"], r["contractStatus"], r["contractEnd"]))
                .Append(r["contractEnd"] != DBNull.Value ? "<span class='hr-sub'>Ends " + FormatDate(r["contractEnd"]) + "</span>" : "").Append("</td>");
            body.Append("<td class='hr-num'>").Append(FormatAmount(r["basicpay"])).Append("</td>");
            body.Append("<td class='hr-right'><button type='button' class='hr-btn hr-btn--secondary hr-btn--sm' onclick=\"event.stopPropagation();openEmployeeProfile(")
                .Append(empID).Append(")\">Open</button></td>");
            body.Append("</tr>");
        }

        if (body.Length == 0)
            body.Append("<tr><td colspan='8' class='hr-empty'>No employees match these filters.</td></tr>");

        litGridBody.Text = body.ToString();
        litPagerInfo.Text = total == 0 ? "No records" :
            string.Format("{0} to {1} of {2}", offset + 1, Math.Min(offset + pageSize, total), total.ToString("N0"));
        litPager.Text = BuildPager(page, totalPages);
    }

    private string BuildPager(int page, int totalPages)
    {
        if (totalPages <= 1) return "";
        StringBuilder sb = new StringBuilder("<div class='hr-pager'>");
        sb.AppendFormat("<span>Page {0} of {1}</span>", page, totalPages);
        sb.Append(PagerButton("Previous", page - 1, page > 1));
        sb.Append(PagerButton("Next", page + 1, page < totalPages));
        sb.Append("</div>");
        return sb.ToString();
    }

    private static string PagerButton(string label, int target, bool enabled)
    {
        if (!enabled) return "<button type='button' disabled='disabled'>" + label + "</button>";
        return "<button type='button' onclick='goPage(" + target + ")'>" + label + "</button>";
    }

    /// <summary>A sortable column heading; the active column shows its direction.</summary>
    protected string SortHead(string key, string label)
    {
        string current = QsSort == "" ? "name" : QsSort;
        string arrow = "";
        if (current == key)
            arrow = QsSortDir == "ASC"
                ? "<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='3'><polyline points='18 15 12 9 6 15'/></svg>"
                : "<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='3'><polyline points='6 9 12 15 18 9'/></svg>";
        return "<a class='em-sort' onclick=\"doSort('" + key + "')\">" + HttpUtility.HtmlEncode(label) + arrow + "</a>";
    }

    private string GetOrderByClause()
    {
        switch (QsSort)
        {
            case "code":        return "e.EMP_CODE " + QsSortDir;
            case "type":        return "e.EmpType " + QsSortDir + ", e.emp_name ASC";
            case "dept":        return "d.dept_name " + QsSortDir + ", e.emp_name ASC";
            case "job":         return "j.jobname " + QsSortDir + ", e.emp_name ASC";
            case "status":      return "c.contractStatus " + QsSortDir + ", e.emp_name ASC";
            case "pay":         return "IFNULL(ps.basicpay, c.fixedamount) " + QsSortDir + ", e.emp_name ASC";
            case "contractend": return "c.contractEnd " + QsSortDir + ", e.emp_name ASC";
            default:            return "e.emp_name " + QsSortDir;
        }
    }

    /// <summary>KPIs that filter the list: All, Active, No valid contract, Duplicate records.</summary>
    private void LoadStats()
    {
        DataTable dt = ExecuteQuery(@"SELECT
                COUNT(*) AS total_cnt,
                SUM(CASE WHEN " + ActiveSql + @" THEN 1 ELSE 0 END) AS active_cnt,
                SUM(CASE WHEN e.EMP_CODE IN (" + DupCodesSql + @") THEN 1 ELSE 0 END) AS dup_cnt
            FROM hrm_employee e
            LEFT JOIN hrm_emp_contracts c ON c.ID = hr_current_contract_id(e.empID)");
        if (dt.Rows.Count == 0) return;
        DataRow r = dt.Rows[0];
        long total = ToLong(r["total_cnt"]), active = ToLong(r["active_cnt"]), dup = ToLong(r["dup_cnt"]);

        string current = ddlFilterStatus.SelectedValue;
        StringBuilder sb = new StringBuilder("<div class='hr-kpis'>");
        Kpi(sb, "All staff", total, "", current, "Employee records");
        Kpi(sb, "Active", active, "ACTIVE", current, "With a valid contract");
        Kpi(sb, "No valid contract", total - active, "NOVALID", current, "Ended or none recorded");
        Kpi(sb, "Duplicate records", dup, "DUP", current, "Shared staff numbers");
        sb.Append("</div>");
        litKpis.Text = sb.ToString();
    }

    private static void Kpi(StringBuilder sb, string label, long value, string status, string current, string sub)
    {
        sb.Append("<a class='hr-kpi").Append(current == status ? " em-kpi--on" : "").Append("' href='HREmployees.aspx")
          .Append(status == "" ? "" : "?status=" + status).Append("'>")
          .Append("<div class='hr-kpi__label'>").Append(HttpUtility.HtmlEncode(label)).Append("</div>")
          .Append("<div class='hr-kpi__value").Append(status == "DUP" && value > 0 ? " em-kpi__value--warn" : "").Append("'>").Append(value.ToString("N0")).Append("</div>")
          .Append("<div class='hr-kpi__sub'>").Append(HttpUtility.HtmlEncode(sub)).Append("</div></a>");
    }

    #endregion

    #region Lists

    private void LoadFilterDropdowns()
    {
        DataTable dtDepts = ExecuteQuery("SELECT ID, dept_name FROM hrm_departments WHERE dept_name IS NOT NULL AND dept_name <> '' ORDER BY dept_name");
        ddlFilterDept.Items.Clear();
        ddlFilterDept.Items.Add(new ListItem("All departments", ""));
        foreach (DataRow r in dtDepts.Rows)
            ddlFilterDept.Items.Add(new ListItem(HrExport.Clean(r["dept_name"].ToString()), r["ID"].ToString()));

        DataTable dtStations = ExecuteQuery("SELECT ID, station_name FROM hrm_stations ORDER BY station_name");
        ddlFilterStation.Items.Clear();
        ddlFilterStation.Items.Add(new ListItem("All stations", ""));
        foreach (DataRow r in dtStations.Rows)
            ddlFilterStation.Items.Add(new ListItem(r["station_name"].ToString(), r["ID"].ToString()));

        DataTable dtTypes = ExecuteQuery("SELECT DISTINCT EmpType FROM hrm_employee WHERE EmpType IS NOT NULL AND EmpType <> '' ORDER BY EmpType");
        ddlFilterType.Items.Clear();
        ddlFilterType.Items.Add(new ListItem("All categories", ""));
        foreach (DataRow r in dtTypes.Rows)
            ddlFilterType.Items.Add(new ListItem(r["EmpType"].ToString(), r["EmpType"].ToString()));
    }

    /// <summary>Station and bank lists of the add/edit dialog, and the supervisor/reviewer picker options.</summary>
    private void LoadFormLists()
    {
        string keepStation = Request.Form[ddlNewStation.UniqueID];
        string keepBank = Request.Form[ddlNewBank.UniqueID];

        DataTable dtStations = ExecuteQuery("SELECT ID, station_name FROM hrm_stations ORDER BY station_name");
        ddlNewStation.Items.Clear();
        ddlNewStation.Items.Add(new ListItem("Select station", ""));
        foreach (DataRow r in dtStations.Rows)
            ddlNewStation.Items.Add(new ListItem(r["station_name"].ToString(), r["ID"].ToString()));
        ListItem masaka = ddlNewStation.Items.FindByText("MASAKA");
        if (masaka != null) ddlNewStation.SelectedValue = masaka.Value;
        SetSelectedValue(ddlNewStation, keepStation);

        DataTable dtBanks = ExecuteQuery("SELECT bank_id, bank_name FROM banks ORDER BY bank_name");
        ddlNewBank.Items.Clear();
        ddlNewBank.Items.Add(new ListItem("Select bank", "0"));
        foreach (DataRow r in dtBanks.Rows)
            ddlNewBank.Items.Add(new ListItem(r["bank_name"].ToString(), r["bank_id"].ToString()));
        SetSelectedValue(ddlNewBank, keepBank);

        if (!IsPostBack) txtNewEntryYear.Text = DateTime.Now.Year.ToString();

        StringBuilder sb = new StringBuilder("<option value=\"\">None</option>");
        foreach (DataRow r in ExecuteQuery("SELECT empID, emp_name, EMP_CODE FROM hrm_employee ORDER BY emp_name").Rows)
        {
            string code = Clean(r["EMP_CODE"]);
            sb.Append("<option value=\"").Append(r["empID"]).Append("\">")
              .Append(Enc(SafeVal(r["emp_name"]) + (code == "" ? "" : " (" + code + ")"))).Append("</option>");
        }
        litPeopleOptions.Text = sb.ToString();
    }

    #endregion

    #region Add Employee

    protected void btnAddEmployee_Click(object sender, EventArgs e)
    {
        string name = txtNewName.Text.Trim();
        string email = txtNewEmail.Text.Trim();
        string phone = txtNewPhone.Text.Trim();

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(phone))
        {
            FormError("Enter the full name, email and phone number.");
            return;
        }

        string empCode = GenerateEmpCode();

        DateTime dob;
        bool hasDOB = DateTime.TryParse(txtNewDOB.Text, out dob);
        int bankId = 0;
        int.TryParse(ddlNewBank.SelectedValue, out bankId);
        int nChildren = 0;
        int.TryParse(txtNewChildren.Text.Trim(), out nChildren);
        int entryYear = DateTime.Now.Year;
        int.TryParse(txtNewEntryYear.Text.Trim(), out entryYear);
        int supervisorId = 0;
        int.TryParse(hfSupervisorID.Value, out supervisorId);
        int reviewerId = 0;
        int.TryParse(hfReviewerID.Value, out reviewerId);
        int toBeAppraised = 1;
        int.TryParse(ddlAppraised.SelectedValue, out toBeAppraised);
        DateTime dateJoined;
        bool hasDateJoined = DateTime.TryParse(txtDateJoined.Text, out dateJoined);
        DateTime probationEnd;
        bool hasProbationEnd = DateTime.TryParse(txtProbationEnd.Text, out probationEnd);

        string sql = @"INSERT INTO hrm_employee
            (EMP_CODE, emp_name, emp_email, emp_phone, emp_birthdate, gender, emp_qualifications,
             max_education, emp_nationality, EmpType, marital_status, address, current_residence,
             religion, tribe, nin, tin, nssf_no, bankID, bankAccount,
             spouse_name, no_children, father_name, mother_name,
             contact_person, relation, phone_contacts,
             referee_1, referee_2, medical_background, schooling_info, employment_info,
             supervisorID, reviewer_id, employment_status, date_joined, probation_end_date,
             to_be_appraised, appraisal_cycle,
             Entry_Year, Entry_Satation)
            VALUES
            (@code, @name, @email, @phone, @dob, @gender, @qual,
             @maxEdu, @nat, @empType, @marital, @addr, @residence,
             @religion, @tribe, @nin, @tin, @nssf, @bankID, @bankAcct,
             @spouse, @nChildren, @father, @mother,
             @contactPerson, @relation, @contactPhone,
             @ref1, @ref2, @medical, @schooling, @employment,
             @supervisorID, @reviewerID, @empStatus, @dateJoined, @probationEnd,
             @toBeAppraised, @appraisalCycle,
             @year, @station)";

        try
        {
            ExecuteNonQuery(sql,
                new MySqlParameter("@code", empCode),
                new MySqlParameter("@name", name),
                new MySqlParameter("@email", email),
                new MySqlParameter("@phone", phone),
                new MySqlParameter("@dob", hasDOB ? (object)dob : DBNull.Value),
                new MySqlParameter("@gender", ddlNewGender.SelectedValue),
                new MySqlParameter("@qual", txtNewQualifications.Text.Trim()),
                new MySqlParameter("@maxEdu", ddlNewEducation.SelectedValue),
                new MySqlParameter("@nat", ddlNewNationality.SelectedValue),
                new MySqlParameter("@empType", ddlNewType.SelectedValue),
                new MySqlParameter("@marital", ddlNewMarital.SelectedValue),
                new MySqlParameter("@addr", txtNewAddress.Text.Trim()),
                new MySqlParameter("@residence", txtNewResidence.Text.Trim()),
                new MySqlParameter("@religion", txtNewReligion.Text.Trim()),
                new MySqlParameter("@tribe", txtNewTribe.Text.Trim()),
                new MySqlParameter("@nin", txtNIN.Text.Trim()),
                new MySqlParameter("@tin", txtNewTIN.Text.Trim()),
                new MySqlParameter("@nssf", txtNewNSSF.Text.Trim()),
                new MySqlParameter("@bankID", bankId),
                new MySqlParameter("@bankAcct", txtNewBankAccount.Text.Trim()),
                new MySqlParameter("@spouse", txtNewSpouse.Text.Trim()),
                new MySqlParameter("@nChildren", nChildren),
                new MySqlParameter("@father", txtNewFather.Text.Trim()),
                new MySqlParameter("@mother", txtNewMother.Text.Trim()),
                new MySqlParameter("@contactPerson", txtNewContactPerson.Text.Trim()),
                new MySqlParameter("@relation", txtNewRelation.Text.Trim()),
                new MySqlParameter("@contactPhone", txtNewContactPhone.Text.Trim()),
                new MySqlParameter("@ref1", txtNewReferee1.Text.Trim()),
                new MySqlParameter("@ref2", txtNewReferee2.Text.Trim()),
                new MySqlParameter("@medical", txtNewMedical.Text.Trim()),
                new MySqlParameter("@schooling", txtNewSchooling.Text.Trim()),
                new MySqlParameter("@employment", txtNewEmployment.Text.Trim()),
                new MySqlParameter("@supervisorID", supervisorId > 0 ? (object)supervisorId : DBNull.Value),
                new MySqlParameter("@reviewerID",   reviewerId   > 0 ? (object)reviewerId   : DBNull.Value),
                new MySqlParameter("@empStatus",    ddlEmpStatus.SelectedValue),
                new MySqlParameter("@dateJoined",   hasDateJoined   ? (object)dateJoined   : DBNull.Value),
                new MySqlParameter("@probationEnd", hasProbationEnd ? (object)probationEnd : DBNull.Value),
                new MySqlParameter("@toBeAppraised",toBeAppraised),
                new MySqlParameter("@appraisalCycle", ddlAppraisalCycle.SelectedValue),
                new MySqlParameter("@year", entryYear),
                new MySqlParameter("@station", ddlNewStation.SelectedValue)
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("HREmployees add: " + ex);
            FormError("The employee could not be saved. Check the dates and numbers, then try again.");
            return;
        }

        ClearForm();
        BindEmployeeGrid();
        LoadStats();
        Toast(true, "Employee " + name + " added with staff number " + empCode + ".");
    }

    private void ClearForm()
    {
        foreach (TextBox t in new TextBox[] { txtNewName, txtNewEmail, txtNewPhone, txtNewDOB, txtNewQualifications, txtNewTIN,
                     txtNewNSSF, txtNewAddress, txtNewReligion, txtNewTribe, txtNewBankAccount, txtNewSpouse, txtNewFather,
                     txtNewMother, txtNewContactPerson, txtNewRelation, txtNewContactPhone, txtNewMedical, txtNewSchooling,
                     txtNewEmployment, txtNIN, txtDateJoined, txtProbationEnd })
            t.Text = "";
        txtNewResidence.Text = "UGANDA";
        txtNewChildren.Text = "0";
        txtNewReferee1.Text = "-";
        txtNewReferee2.Text = "-";
        txtNewEntryYear.Text = DateTime.Now.Year.ToString();
    }

    protected void btnEditEmployee_Click(object sender, EventArgs e)
    {
        int empID;
        if (!int.TryParse(hdnEditEmpID.Value, out empID) || empID <= 0)
            return;

        string name = txtNewName.Text.Trim();
        string email = txtNewEmail.Text.Trim();
        string phone = txtNewPhone.Text.Trim();

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(phone))
        {
            FormError("Enter the full name, email and phone number.");
            return;
        }

        // EMP_CODE: only update if the user unlocked and changed it
        string newCode  = txtEmpCodeDisplay.Text.Trim();
        string origCode = hfOriginalEmpCode.Value.Trim();
        bool codeChanged = !string.IsNullOrEmpty(newCode) &&
                           !string.Equals(newCode, origCode, StringComparison.OrdinalIgnoreCase);
        if (codeChanged)
        {
            DataTable dtDup = ExecuteQuery(
                "SELECT empID FROM hrm_employee WHERE EMP_CODE = @c AND empID != @id LIMIT 1",
                new MySqlParameter("@c", newCode),
                new MySqlParameter("@id", empID));
            if (dtDup.Rows.Count > 0)
            {
                FormError("Staff number " + newCode + " already belongs to another employee. Choose a different one.");
                return;
            }
        }

        DateTime dob;
        bool hasDOB = DateTime.TryParse(txtNewDOB.Text, out dob);
        int bankId = 0;
        int.TryParse(ddlNewBank.SelectedValue, out bankId);
        int nChildren = 0;
        int.TryParse(txtNewChildren.Text.Trim(), out nChildren);
        int entryYear = DateTime.Now.Year;
        int.TryParse(txtNewEntryYear.Text.Trim(), out entryYear);
        int supervisorId = 0;
        int.TryParse(hfSupervisorID.Value, out supervisorId);
        int reviewerId = 0;
        int.TryParse(hfReviewerID.Value, out reviewerId);
        int toBeAppraised = 1;
        int.TryParse(ddlAppraised.SelectedValue, out toBeAppraised);
        DateTime dateJoined;
        bool hasDateJoined = DateTime.TryParse(txtDateJoined.Text, out dateJoined);
        DateTime probationEnd;
        bool hasProbationEnd = DateTime.TryParse(txtProbationEnd.Text, out probationEnd);

        string codeClause = codeChanged ? " EMP_CODE = @newCode," : "";
        string sql = @"UPDATE hrm_employee SET" + codeClause + @"
                emp_name = @name, emp_email = @email, emp_phone = @phone, emp_birthdate = @dob,
                gender = @gender, emp_qualifications = @qual, max_education = @maxEdu,
                emp_nationality = @nat, EmpType = @empType, marital_status = @marital,
                address = @addr, current_residence = @residence, religion = @religion, tribe = @tribe,
                nin = @nin, tin = @tin, nssf_no = @nssf, bankID = @bankID, bankAccount = @bankAcct,
                spouse_name = @spouse, no_children = @nChildren, father_name = @father, mother_name = @mother,
                contact_person = @contactPerson, relation = @relation, phone_contacts = @contactPhone,
                referee_1 = @ref1, referee_2 = @ref2,
                medical_background = @medical, schooling_info = @schooling, employment_info = @employment,
                supervisorID = @supervisorID, reviewer_id = @reviewerID, employment_status = @empStatus,
                date_joined = @dateJoined, probation_end_date = @probationEnd,
                to_be_appraised = @toBeAppraised, appraisal_cycle = @appraisalCycle,
                Entry_Year = @year, Entry_Satation = @station
            WHERE empID = @id";

        List<MySqlParameter> editParams = new List<MySqlParameter>();
        if (codeChanged) editParams.Add(new MySqlParameter("@newCode", newCode));
        editParams.AddRange(new MySqlParameter[] {
            new MySqlParameter("@name", name),
            new MySqlParameter("@email", email),
            new MySqlParameter("@phone", phone),
            new MySqlParameter("@dob", hasDOB ? (object)dob : DBNull.Value),
            new MySqlParameter("@gender", ddlNewGender.SelectedValue),
            new MySqlParameter("@qual", txtNewQualifications.Text.Trim()),
            new MySqlParameter("@maxEdu", ddlNewEducation.SelectedValue),
            new MySqlParameter("@nat", ddlNewNationality.SelectedValue),
            new MySqlParameter("@empType", ddlNewType.SelectedValue),
            new MySqlParameter("@marital", ddlNewMarital.SelectedValue),
            new MySqlParameter("@addr", txtNewAddress.Text.Trim()),
            new MySqlParameter("@residence", txtNewResidence.Text.Trim()),
            new MySqlParameter("@religion", txtNewReligion.Text.Trim()),
            new MySqlParameter("@tribe", txtNewTribe.Text.Trim()),
            new MySqlParameter("@nin", txtNIN.Text.Trim()),
            new MySqlParameter("@tin", txtNewTIN.Text.Trim()),
            new MySqlParameter("@nssf", txtNewNSSF.Text.Trim()),
            new MySqlParameter("@bankID", bankId),
            new MySqlParameter("@bankAcct", txtNewBankAccount.Text.Trim()),
            new MySqlParameter("@spouse", txtNewSpouse.Text.Trim()),
            new MySqlParameter("@nChildren", nChildren),
            new MySqlParameter("@father", txtNewFather.Text.Trim()),
            new MySqlParameter("@mother", txtNewMother.Text.Trim()),
            new MySqlParameter("@contactPerson", txtNewContactPerson.Text.Trim()),
            new MySqlParameter("@relation", txtNewRelation.Text.Trim()),
            new MySqlParameter("@contactPhone", txtNewContactPhone.Text.Trim()),
            new MySqlParameter("@ref1", txtNewReferee1.Text.Trim()),
            new MySqlParameter("@ref2", txtNewReferee2.Text.Trim()),
            new MySqlParameter("@medical", txtNewMedical.Text.Trim()),
            new MySqlParameter("@schooling", txtNewSchooling.Text.Trim()),
            new MySqlParameter("@employment", txtNewEmployment.Text.Trim()),
            new MySqlParameter("@supervisorID", supervisorId > 0 ? (object)supervisorId : DBNull.Value),
            new MySqlParameter("@reviewerID",   reviewerId   > 0 ? (object)reviewerId   : DBNull.Value),
            new MySqlParameter("@empStatus",    ddlEmpStatus.SelectedValue),
            new MySqlParameter("@dateJoined",   hasDateJoined   ? (object)dateJoined   : DBNull.Value),
            new MySqlParameter("@probationEnd", hasProbationEnd ? (object)probationEnd : DBNull.Value),
            new MySqlParameter("@toBeAppraised",toBeAppraised),
            new MySqlParameter("@appraisalCycle", ddlAppraisalCycle.SelectedValue),
            new MySqlParameter("@year", entryYear),
            new MySqlParameter("@station", ddlNewStation.SelectedValue),
            new MySqlParameter("@id", empID)
        });

        try { ExecuteNonQuery(sql, editParams.ToArray()); }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("HREmployees edit: " + ex);
            FormError("The changes could not be saved. Check the dates and numbers, then try again.");
            return;
        }

        BindEmployeeGrid();
        LoadStats();
        Toast(true, "Changes to " + name + " saved.");
    }

    protected void btnDeleteEmployee_Click(object sender, EventArgs e)
    {
        int empID;
        if (!int.TryParse(hdnDeleteEmpID.Value, out empID) || empID <= 0)
            return;

        // The employee's login username and name (to remove the linked account, guard, and message).
        DataTable dt = ExecuteQuery(
            "SELECT IFNULL(usernames,'') AS u, IFNULL(emp_name,'') AS n FROM hrm_employee WHERE empID=@id LIMIT 1",
            new MySqlParameter("@id", empID));
        if (dt.Rows.Count == 0) { Toast(false, "This employee was not found. The record may already have been deleted."); BindEmployeeGrid(); return; }
        string uname = dt.Rows[0]["u"].ToString().Trim();
        string ename = dt.Rows[0]["n"].ToString().Trim();

        // Guard 1: never let an operator delete their own account.
        string me = (Session["username"] as string ?? "").Trim();
        if (!string.IsNullOrEmpty(uname) && !string.IsNullOrEmpty(me)
            && string.Equals(uname, me, StringComparison.OrdinalIgnoreCase))
        { Toast(false, "You cannot delete your own account."); return; }

        // Guard 2: block deleting a sitting department head (would orphan that department's HOD scope).
        DataTable heads = ExecuteQuery(
            "SELECT IFNULL(dept_name,CONCAT('#',ID)) AS dn FROM hrm_departments WHERE dept_headID=@id LIMIT 1",
            new MySqlParameter("@id", empID));
        if (heads.Rows.Count > 0)
        {
            Toast(false, ename + " is Head of Department of " + heads.Rows[0]["dn"].ToString()
                + ". Assign another head first, then delete.");
            return;
        }

        // Remove the linked login account (roles + membership + user) matched by username. Best-effort.
        int loginRows = 0;
        if (!string.IsNullOrEmpty(uname) && uname != "-")
        {
            DataTable u = ExecuteQuery("SELECT id FROM my_aspnet_users WHERE name=@u", new MySqlParameter("@u", uname));
            foreach (DataRow r in u.Rows)
            {
                int uid = Convert.ToInt32(r["id"]);
                ExecuteNonQuery("DELETE FROM my_aspnet_usersinroles WHERE userId=@u", new MySqlParameter("@u", uid));
                ExecuteNonQuery("DELETE FROM my_aspnet_membership WHERE userId=@u", new MySqlParameter("@u", uid));
                loginRows += ExecuteNonQuery("DELETE FROM my_aspnet_users WHERE id=@u", new MySqlParameter("@u", uid));
            }
        }

        int empRows = ExecuteNonQuery("DELETE FROM hrm_employee WHERE empID = @id", new MySqlParameter("@id", empID));

        BindEmployeeGrid();
        LoadStats();
        if (empRows > 0)
            Toast(true, "Deleted " + ename + (loginRows > 0 ? " and their login." : "."));
        else
            Toast(false, "No employee record was deleted.");
    }

    private void Toast(bool ok, string message)
    {
        ScriptManager.RegisterStartupScript(this, GetType(), "hrToast",
            "showToast('" + HttpUtility.JavaScriptStringEncode(HrExport.Clean(message ?? "")) + "'," + (ok ? "false" : "true") + ");", true);
    }

    /// <summary>Reopen the add/edit dialog with a plain error line after a failed postback.</summary>
    private void FormError(string message)
    {
        ScriptManager.RegisterStartupScript(this, GetType(), "empFormErr",
            "reopenEmpForm('" + HttpUtility.JavaScriptStringEncode(message) + "');", true);
    }

    private string GenerateEmpCode()
    {
        string yearPrefix = "MRU/" + DateTime.Now.Year.ToString() + "/";
        DataTable dt = ExecuteQuery(
            "SELECT EMP_CODE FROM hrm_employee WHERE EMP_CODE LIKE @prefix ORDER BY empID DESC LIMIT 1",
            new MySqlParameter("@prefix", yearPrefix + "%"));

        int nextNum = 1;
        if (dt.Rows.Count > 0)
        {
            string lastCode = dt.Rows[0]["EMP_CODE"].ToString();
            string numPart = lastCode.Substring(lastCode.LastIndexOf('/') + 1);
            int parsed;
            if (int.TryParse(numPart, out parsed)) nextNum = parsed + 1;
        }
        return yearPrefix + nextNum.ToString("D4");
    }

    #endregion

    #region Profile (Details, Contracts, Leave, Payroll)

    private string BuildDetailsHtml(DataRow emp)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(Group("Personal"));
        sb.Append("<dl class='hr-dl'>");
        AddItem(sb, "Full name", emp["emp_name"]);
        AddItem(sb, "Staff number", emp["EMP_CODE"]);
        AddItem(sb, "Date of birth", emp["emp_birthdate"] != DBNull.Value ? FormatDate(emp["emp_birthdate"]) : "");
        AddItem(sb, "Gender", emp["gender"]);
        AddItem(sb, "Nationality", Title(emp["emp_nationality"]));
        AddItem(sb, "National ID number", emp["nin"]);
        AddItem(sb, "Marital status", Title(emp["marital_status"]));
        AddItem(sb, "Religion", emp["religion"]);
        AddItem(sb, "Tribe", emp["tribe"]);
        AddItem(sb, "Email", emp["emp_email"]);
        AddItem(sb, "Phone", emp["emp_phone"]);
        AddItem(sb, "Residence", emp["current_residence"]);
        AddItem(sb, "Address", emp["address"]);
        sb.Append("</dl>");

        sb.Append(Group("Employment"));
        sb.Append("<dl class='hr-dl'>");
        AddItem(sb, "Category", emp["EmpType"]);
        AddItem(sb, "Position", emp["jobname"]);
        AddItem(sb, "Department", emp["dept_name"]);
        AddItem(sb, "Station", emp["station_name"]);
        AddItem(sb, "Entry year", emp["Entry_Year"]);
        AddItem(sb, "Date joined", emp["date_joined"] != DBNull.Value ? FormatDate(emp["date_joined"]) : "");
        AddItem(sb, "Employment status", Title(emp["employment_status"]));
        AddItem(sb, "Supervisor", emp["sup_name"]);
        AddItem(sb, "Reviewer", emp["rev_name"]);
        AddItem(sb, "Highest education", emp["max_education"]);
        AddItem(sb, "Qualifications", emp["emp_qualifications"]);
        AddItem(sb, "Login username", emp["usernames"]);
        sb.Append("</dl>");

        sb.Append(Group("Pay and statutory"));
        sb.Append("<dl class='hr-dl'>");
        AddItem(sb, "Bank", ResolveBankName(emp["bankID"]));
        AddItem(sb, "Bank account", emp["bankAccount"]);
        AddItem(sb, "TIN", emp["tin"]);
        AddItem(sb, "NSSF number", emp["nssf_no"]);
        AddItem(sb, "Pay scale", emp["scale_name"]);
        AddItem(sb, "Basic pay (UGX)", emp["basicpay"] != DBNull.Value ? FormatAmount(emp["basicpay"]) : "");
        sb.Append("</dl>");

        sb.Append(Group("Family and emergency contact"));
        sb.Append("<dl class='hr-dl'>");
        AddItem(sb, "Spouse", emp["spouse_name"]);
        AddItem(sb, "Children", emp["no_children"]);
        AddItem(sb, "Father", emp["father_name"]);
        AddItem(sb, "Mother", emp["mother_name"]);
        AddItem(sb, "Emergency contact", emp["contact_person"]);
        AddItem(sb, "Relationship", emp["relation"]);
        AddItem(sb, "Contact phone", emp["phone_contacts"]);
        AddItem(sb, "Referee 1", emp["referee_1"]);
        AddItem(sb, "Referee 2", emp["referee_2"]);
        sb.Append("</dl>");

        string schooling = Clean(emp["schooling_info"]), employment = Clean(emp["employment_info"]), medical = Clean(emp["medical_background"]);
        if (schooling != "" || employment != "" || medical != "")
        {
            sb.Append(Group("Background"));
            sb.Append("<dl class='hr-dl hr-dl--2'>");
            if (schooling != "") AddItem(sb, "Training", schooling);
            if (employment != "") AddItem(sb, "Employment history", employment);
            if (medical != "") AddItem(sb, "Medical", medical);
            sb.Append("</dl>");
        }

        sb.Append(Group("Qualifications on record"));
        sb.Append(BuildQualificationsHtml(SafeVal(emp["EMP_CODE"])));
        return sb.ToString();
    }

    private static string Group(string title)
    {
        return "<div class='em-group'>" + HttpUtility.HtmlEncode(title) + "</div>";
    }

    private string ResolveBankName(object bankID)
    {
        if (bankID == null || bankID == DBNull.Value) return "";
        int bid;
        if (!int.TryParse(bankID.ToString(), out bid) || bid == 0) return "";
        DataTable dt = ExecuteQuery("SELECT bank_name FROM banks WHERE bank_id = @id", new MySqlParameter("@id", bid));
        if (dt.Rows.Count > 0) return dt.Rows[0]["bank_name"].ToString();
        return bankID.ToString();
    }

    private static void AddItem(StringBuilder sb, string label, object value)
    {
        string v = Clean(value);
        sb.Append("<div><dt>").Append(HttpUtility.HtmlEncode(label)).Append("</dt><dd")
          .Append(v == "" ? " class='hr-muted'>Not recorded" : ">" + Enc(v).Replace("\r\n", "<br/>").Replace("\n", "<br/>"))
          .Append("</dd></div>");
    }

    private string BuildContractsHtml(int empID)
    {
        DataTable dt = ExecuteQuery(@"
            SELECT c.ID, c.contractStart, c.contractEnd, c.contractStatus, c.contract_type, c.comments,
                   c.fixedamount, j.jobname, d.dept_name, ps.scale_name, ps.basicpay
            FROM hrm_emp_contracts c
            LEFT JOIN hrm_jobs j ON j.ID = c.jobID
            LEFT JOIN hrm_departments d ON d.ID = c.departmentID
            LEFT JOIN hrm_payscales ps ON ps.ID = c.payscale
            WHERE c.empID = @id ORDER BY c.contractStart DESC",
            new MySqlParameter("@id", empID));

        if (dt.Rows.Count == 0)
            return "<div class='hr-empty'>No contracts recorded. <a href='HRContracts.aspx?status=NONE'>Add one in Contracts</a>.</div>";

        StringBuilder sb = new StringBuilder("<div class='hr-table-wrap'><table class='hr-table'><thead><tr>");
        sb.Append("<th>Start</th><th>End</th><th>Position</th><th>Department</th><th>Pay scale</th><th class='hr-num'>Basic pay (UGX)</th><th>Status</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (DataRow r in dt.Rows)
        {
            sb.Append("<tr>");
            sb.AppendFormat("<td style='white-space:nowrap'>{0}</td>", FormatDate(r["contractStart"]));
            sb.AppendFormat("<td style='white-space:nowrap'>{0}</td>", FormatDate(r["contractEnd"]));
            sb.AppendFormat("<td>{0}</td>", Enc(r["jobname"]));
            sb.AppendFormat("<td>{0}</td>", Enc(r["dept_name"]));
            sb.AppendFormat("<td>{0}</td>", Enc(r["scale_name"]));
            sb.AppendFormat("<td class='hr-num'>{0}</td>", FormatAmount(r["basicpay"] != DBNull.Value ? r["basicpay"] : r["fixedamount"]));
            sb.AppendFormat("<td>{0}</td>", ContractBadge(r["ID"], r["contractStatus"], r["contractEnd"]));
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table></div>");
        return sb.ToString();
    }

    private string BuildQualificationsHtml(string empCode)
    {
        DataTable dt = ExecuteQuery(@"
            SELECT qualif, institution, period_start, period_end, award_class
            FROM hrm_qualifications WHERE empcode = @code ORDER BY period_end DESC",
            new MySqlParameter("@code", empCode));

        if (dt.Rows.Count == 0)
            return "<div class='hr-muted' style='padding:4px 0'>None recorded.</div>";

        StringBuilder sb = new StringBuilder("<div class='hr-table-wrap'><table class='hr-table'><thead><tr>");
        sb.Append("<th>Qualification</th><th>Institution</th><th>From</th><th>To</th><th>Class</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (DataRow r in dt.Rows)
        {
            sb.Append("<tr>");
            sb.AppendFormat("<td>{0}</td>", Enc(r["qualif"]));
            sb.AppendFormat("<td>{0}</td>", Enc(r["institution"]));
            sb.AppendFormat("<td style='white-space:nowrap'>{0}</td>", FormatDate(r["period_start"]));
            sb.AppendFormat("<td style='white-space:nowrap'>{0}</td>", FormatDate(r["period_end"]));
            sb.AppendFormat("<td>{0}</td>", Enc(r["award_class"]));
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table></div>");
        return sb.ToString();
    }

    private string BuildLeaveHtml(int empID)
    {
        DataTable dtAlloc = ExecuteQuery(@"
            SELECT al.leave_year, al.default_days, COALESCE(SUM(lt.no_days),0) AS taken_days
            FROM hrm_annual_leave al
            LEFT JOIN hrm_leave_taken lt ON lt.leaveID = al.ID
            WHERE al.empID = @id
            GROUP BY al.ID, al.leave_year, al.default_days
            ORDER BY al.leave_year DESC",
            new MySqlParameter("@id", empID));

        if (dtAlloc.Rows.Count == 0)
            return "<div class='hr-empty'>No leave allocated or taken.</div>";

        StringBuilder sb = new StringBuilder(Group("Balances"));
        sb.Append("<div class='hr-table-wrap'><table class='hr-table'><thead><tr>");
        sb.Append("<th>Year</th><th class='hr-num'>Allocated</th><th class='hr-num'>Taken</th><th class='hr-num'>Remaining</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (DataRow r in dtAlloc.Rows)
        {
            int allocated = Convert.ToInt32(r["default_days"]);
            int taken = Convert.ToInt32(r["taken_days"]);
            sb.Append("<tr>");
            sb.AppendFormat("<td>{0}</td>", Enc(r["leave_year"]));
            sb.AppendFormat("<td class='hr-num'>{0}</td>", allocated);
            sb.AppendFormat("<td class='hr-num'>{0}</td>", taken);
            sb.AppendFormat("<td class='hr-num'><strong>{0}</strong></td>", allocated - taken);
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table></div>");

        DataTable dtDetails = ExecuteQuery(@"
            SELECT lt.startDate, lt.endDate, lt.no_days, al.leave_year
            FROM hrm_leave_taken lt
            JOIN hrm_annual_leave al ON al.ID = lt.leaveID
            WHERE al.empID = @id
            ORDER BY lt.startDate DESC LIMIT 20",
            new MySqlParameter("@id", empID));

        if (dtDetails.Rows.Count > 0)
        {
            sb.Append(Group("Leave taken"));
            sb.Append("<div class='hr-table-wrap'><table class='hr-table'><thead><tr>");
            sb.Append("<th>First day</th><th>Last day</th><th class='hr-num'>Days</th><th>Year</th>");
            sb.Append("</tr></thead><tbody>");
            foreach (DataRow r in dtDetails.Rows)
            {
                sb.Append("<tr>");
                sb.AppendFormat("<td>{0}</td>", FormatDate(r["startDate"]));
                sb.AppendFormat("<td>{0}</td>", FormatDate(r["endDate"]));
                sb.AppendFormat("<td class='hr-num'>{0}</td>", Enc(r["no_days"]));
                sb.AppendFormat("<td>{0}</td>", Enc(r["leave_year"]));
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table></div>");
        }
        return sb.ToString();
    }

    private string BuildPayrollHtml(int empID)
    {
        DataTable dt = ExecuteQuery(@"
            SELECT p.payroll_title, p.payroll_month, p.payroll_year,
                   pd.basic_pay, pd.paye, pd.nssf, pd.total_allowances, pd.total_deductions,
                   pd.gross_pay, pd.net_pay
            FROM hrm_payroll_details pd
            JOIN hrm_payroll p ON p.ID = pd.payrollID
            WHERE pd.empID = @id
            ORDER BY p.payroll_year DESC, p.payroll_month DESC
            LIMIT 24",
            new MySqlParameter("@id", empID));

        if (dt.Rows.Count == 0)
            return "<div class='hr-empty'>No payroll records.</div>";

        StringBuilder sb = new StringBuilder("<p class='hr-hint' style='margin:0 0 8px'>Amounts in UGX.</p><div class='hr-table-wrap'><table class='hr-table'><thead><tr>");
        sb.Append("<th>Period</th><th class='hr-num'>Basic pay</th><th class='hr-num'>Allowances</th>");
        sb.Append("<th class='hr-num'>Gross pay</th><th class='hr-num'>PAYE</th><th class='hr-num'>NSSF</th>");
        sb.Append("<th class='hr-num'>Deductions</th><th class='hr-num'>Net pay</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (DataRow r in dt.Rows)
        {
            sb.Append("<tr>");
            sb.AppendFormat("<td style='white-space:nowrap'>{0}<span class='hr-sub'>{1}</span></td>",
                PeriodLabel(r["payroll_month"], r["payroll_year"]), Enc(r["payroll_title"]));
            sb.AppendFormat("<td class='hr-num'>{0}</td>", FormatAmount(r["basic_pay"]));
            sb.AppendFormat("<td class='hr-num'>{0}</td>", FormatAmount(r["total_allowances"]));
            sb.AppendFormat("<td class='hr-num'>{0}</td>", FormatAmount(r["gross_pay"]));
            sb.AppendFormat("<td class='hr-num'>{0}</td>", FormatAmount(r["paye"]));
            sb.AppendFormat("<td class='hr-num'>{0}</td>", FormatAmount(r["nssf"]));
            sb.AppendFormat("<td class='hr-num'>{0}</td>", FormatAmount(r["total_deductions"]));
            sb.AppendFormat("<td class='hr-num'><strong>{0}</strong></td>", FormatAmount(r["net_pay"]));
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table></div>");
        return sb.ToString();
    }

    private static string PeriodLabel(object month, object year)
    {
        int m;
        string y = SafeVal(year);
        if (int.TryParse(SafeVal(month), out m) && m >= 1 && m <= 12)
            return new DateTime(2000, m, 1).ToString("MMMM", CultureInfo.InvariantCulture) + " " + y;
        return Enc(SafeVal(month) + " " + y);
    }

    #endregion

    #region AJAX

    private void HandleAjaxAction(string action)
    {
        Response.Clear();
        Response.ContentType = "application/json";
        try
        {
            switch (action)
            {
                case "search_emp":  WriteEmployeeSearchResults(); break;
                case "get_emp":     WriteEmployeeDetails(); break;
                case "get_profile": WriteEmployeeProfile(); break;
                case "fix_login":   WriteFixLoginAjax(); break;
                case "set_photo":   WriteSetPhotoAjax(); break;
                default:            WriteJson(new Dictionary<string, object> { { "error", "Unknown action." } }); break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("HREmployees " + action + ": " + ex);
            Response.Clear();
            WriteJson(new Dictionary<string, object> { { "error", "The request could not be completed. Try again, and contact MIS if it keeps happening." } });
        }
        Response.End();
    }

    private void WriteEmployeeSearchResults()
    {
        string q = (Request.QueryString["q"] ?? string.Empty).Trim();
        List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();
        if (!string.IsNullOrEmpty(q))
        {
            DataTable dt = ExecuteQuery(@"
                SELECT e.empID, e.EMP_CODE, e.emp_name, IFNULL(j.jobname, '') AS emp_position
                FROM hrm_employee e
                LEFT JOIN hrm_emp_contracts c ON c.ID = hr_current_contract_id(e.empID)
                LEFT JOIN hrm_jobs j ON j.ID = c.jobID
                WHERE e.emp_name LIKE @q OR e.EMP_CODE LIKE @q OR e.emp_email LIKE @q OR e.emp_phone LIKE @q
                ORDER BY e.emp_name ASC
                LIMIT 20",
                new MySqlParameter("@q", "%" + q + "%"));
            foreach (DataRow row in dt.Rows)
            {
                results.Add(new Dictionary<string, object>
                {
                    { "empID", SafeVal(row["empID"]) },
                    { "EMP_CODE", SafeVal(row["EMP_CODE"]) },
                    { "emp_name", SafeVal(row["emp_name"]) },
                    { "emp_position", SafeVal(row["emp_position"]) }
                });
            }
        }
        WriteJson(new Dictionary<string, object> { { "results", results } });
    }

    private void WriteEmployeeDetails()
    {
        int empID;
        if (!int.TryParse(Request.QueryString["id"], out empID) || empID <= 0)
        {
            WriteJson(new Dictionary<string, object> { { "error", "Choose an employee." } });
            return;
        }

        DataTable dt = ExecuteQuery(@"SELECT e.* FROM hrm_employee e WHERE e.empID = @id", new MySqlParameter("@id", empID));
        if (dt.Rows.Count == 0)
        {
            WriteJson(new Dictionary<string, object> { { "error", "This employee was not found." } });
            return;
        }

        DataRow row = dt.Rows[0];
        WriteJson(new Dictionary<string, object>
        {
            { "empID",    SafeVal(row["empID"]) },
            { "EMP_CODE", SafeVal(row["EMP_CODE"]) },
            { "emp_name", SafeVal(row["emp_name"]) },
            { "emp_email", SafeVal(row["emp_email"]) },
            { "emp_phone", SafeVal(row["emp_phone"]) },
            { "emp_birthdate", ToIsoDate(row["emp_birthdate"]) },
            { "gender", SafeVal(row["gender"]) },
            { "marital_status", SafeVal(row["marital_status"]) },
            { "emp_nationality", SafeVal(row["emp_nationality"]) },
            { "religion", SafeVal(row["religion"]) },
            { "tribe", SafeVal(row["tribe"]) },
            { "nin", SafeVal(row["nin"]) },
            { "current_residence", SafeVal(row["current_residence"]) },
            { "address", SafeVal(row["address"]) },
            { "EmpType", SafeVal(row["EmpType"]) },
            { "Entry_Satation", SafeVal(row["Entry_Satation"]) },
            { "Entry_Year", SafeVal(row["Entry_Year"]) },
            { "max_education", SafeVal(row["max_education"]) },
            { "emp_qualifications", SafeVal(row["emp_qualifications"]) },
            { "tin", SafeVal(row["tin"]) },
            { "nssf_no", SafeVal(row["nssf_no"]) },
            { "bankID", SafeVal(row["bankID"]) },
            { "bankAccount", SafeVal(row["bankAccount"]) },
            { "spouse_name", SafeVal(row["spouse_name"]) },
            { "no_children", SafeVal(row["no_children"]) },
            { "father_name", SafeVal(row["father_name"]) },
            { "mother_name", SafeVal(row["mother_name"]) },
            { "contact_person", SafeVal(row["contact_person"]) },
            { "relation", SafeVal(row["relation"]) },
            { "phone_contacts", SafeVal(row["phone_contacts"]) },
            { "referee_1", SafeVal(row["referee_1"]) },
            { "referee_2", SafeVal(row["referee_2"]) },
            { "medical_background", SafeVal(row["medical_background"]) },
            { "schooling_info", SafeVal(row["schooling_info"]) },
            { "employment_info", SafeVal(row["employment_info"]) },
            { "supervisorID",      SafeVal(row["supervisorID"]) },
            { "employment_status", SafeVal(row["employment_status"]) },
            { "date_joined",       ToIsoDate(row["date_joined"]) },
            { "probation_end_date",ToIsoDate(row["probation_end_date"]) },
            { "to_be_appraised",   SafeVal(row["to_be_appraised"]) },
            { "appraisal_cycle",   SafeVal(row["appraisal_cycle"]) },
            { "reviewer_id",       SafeVal(row["reviewer_id"]) }
        });
    }

    private void WriteEmployeeProfile()
    {
        int empID;
        if (!int.TryParse(Request.QueryString["id"], out empID) || empID <= 0)
        {
            WriteJson(new Dictionary<string, object> { { "error", "Choose an employee." } });
            return;
        }

        DataTable dtEmp = ExecuteQuery(@"
            SELECT e.*, d.dept_name, st.station_name, c.ID AS contractID, c.contractStatus, c.contractStart, c.contractEnd,
                   ps.scale_name, IFNULL(ps.basicpay, c.fixedamount) AS basicpay, j.jobname,
                   sup.emp_name AS sup_name, rev.emp_name AS rev_name
            FROM hrm_employee e
            LEFT JOIN hrm_emp_contracts c ON c.ID = hr_current_contract_id(e.empID)
            LEFT JOIN hrm_departments d ON d.ID = c.departmentID
            LEFT JOIN hrm_stations st ON st.ID = e.Entry_Satation
            LEFT JOIN hrm_payscales ps ON ps.ID = c.payscale
            LEFT JOIN hrm_jobs j ON j.ID = c.jobID
            LEFT JOIN hrm_employee sup ON sup.empID = e.supervisorID
            LEFT JOIN hrm_employee rev ON rev.empID = e.reviewer_id
            WHERE e.empID = @id",
            new MySqlParameter("@id", empID));

        if (dtEmp.Rows.Count == 0)
        {
            WriteJson(new Dictionary<string, object> { { "error", "This employee was not found." } });
            return;
        }

        DataRow emp = dtEmp.Rows[0];
        string empCode = SafeVal(emp["EMP_CODE"]);
        string login = Clean(emp["usernames"]);

        WriteJson(new Dictionary<string, object>
        {
            { "id", empID },
            { "name", HrExport.Clean(SafeVal(emp["emp_name"])) },
            { "code", Clean(emp["EMP_CODE"]) },
            { "email", Clean(emp["emp_email"]) },
            { "login", login },
            { "photo", GetPhotoUrl(empCode) },
            { "statusBadge", ContractBadge(emp["contractID"], emp["contractStatus"], emp["contractEnd"]) },
            { "type", Clean(emp["EmpType"]) },
            { "dept", Clean(emp["dept_name"]) },
            { "job", Clean(emp["jobname"]) },
            { "station", Clean(emp["station_name"]) },
            { "pay", emp["basicpay"] != DBNull.Value ? FormatAmount(emp["basicpay"]) : "" },
            { "contractEnd", emp["contractEnd"] != DBNull.Value ? FormatDate(emp["contractEnd"]) : "" },
            { "detailsHtml", BuildDetailsHtml(emp) },
            { "contractsHtml", BuildContractsHtml(empID) },
            { "leaveHtml", BuildLeaveHtml(empID) },
            { "payrollHtml", BuildPayrollHtml(empID) }
        });
    }

    private void WriteJson(object data)
    {
        JavaScriptSerializer serializer = new JavaScriptSerializer();
        serializer.MaxJsonLength = int.MaxValue;
        Response.Write(serializer.Serialize(data));
    }

    #endregion

    #region Staff register export

    private void SendStaffRegister()
    {
        string search  = (Request.QueryString["q"] ?? string.Empty).Trim();
        string dept    = (Request.QueryString["dept"] ?? string.Empty).Trim();
        string station = (Request.QueryString["station"] ?? string.Empty).Trim();
        string empType = (Request.QueryString["type"] ?? string.Empty).Trim();
        string status  = QsStatus;
        string fmt     = string.Equals(Request.QueryString["fmt"], "csv", StringComparison.OrdinalIgnoreCase) ? "csv" : "xlsx";

        HrExport.Report r = new HrExport.Report("Staff register", "staff-register");
        r.PreparedBy = HrAccess.Username();
        r.AddScope("Status", status == "ACTIVE" ? "Active (valid contract)" : status == "NOVALID" ? "No valid contract" : status == "DUP" ? "Duplicate staff numbers" : "");
        r.AddScope("Category", empType);
        if (dept != "") r.AddScope("Department", Lookup("SELECT dept_name FROM hrm_departments WHERE ID=@id", dept));
        if (station != "") r.AddScope("Station", Lookup("SELECT station_name FROM hrm_stations WHERE ID=@id", station));
        r.AddScope("Search", search);

        HrExport.Sheet sh = r.NewSheet("Staff");
        sh.Add("Staff No").Add("Name").Add("Category").Add("Department").Add("Position").Add("Email").Add("Phone")
          .Add("Contract status").Add("Contract start", HrExport.Kind.Date).Add("Contract end", HrExport.Kind.Date)
          .Add("Pay scale").Add("Basic pay (UGX)", HrExport.Kind.Money, true);

        List<MySqlParameter> parms = new List<MySqlParameter>();
        string from = DirectoryFrom(parms, search, dept, station, empType, status);
        DataTable dt = ExecuteQuery(@"SELECT e.EMP_CODE, e.emp_name, e.EmpType, e.emp_email, e.emp_phone,
                d.dept_name, j.jobname, c.ID AS contractID, c.contractStatus, c.contractStart, c.contractEnd,
                ps.scale_name, IFNULL(ps.basicpay, c.fixedamount) AS basicpay " + from + " ORDER BY " + GetOrderByClause(), parms.ToArray());

        foreach (DataRow row in dt.Rows)
        {
            bool has = row["contractID"] != DBNull.Value;
            sh.Row(Clean(row["EMP_CODE"]), Clean(row["emp_name"]), Clean(row["EmpType"]), Clean(row["dept_name"]), Clean(row["jobname"]),
                UsableEmail(row["emp_email"]), UsablePhone(row["emp_phone"]),
                ContractWords(row["contractID"], row["contractStatus"], row["contractEnd"]),
                has ? row["contractStart"] : null, has ? row["contractEnd"] : null,
                has ? (row["scale_name"] == DBNull.Value ? "Fixed amount" : Clean(row["scale_name"])) : "",
                has ? row["basicpay"] : null);
        }

        if (fmt == "csv") HrExport.SendCsv(Response, r, 0);
        else HrExport.SendXlsx(Response, r);
        Response.End();
    }

    private string Lookup(string sql, string id)
    {
        DataTable dt = ExecuteQuery(sql, new MySqlParameter("@id", id));
        return dt.Rows.Count > 0 ? SafeVal(dt.Rows[0][0]) : id;
    }

    #endregion

    #region Reset login

    /// <summary>
    /// The same lookup the eportal staff screens perform, so this tool can prove a repair worked.
    /// Kept in step with App_Code/Portal/StaffLookup.cs in the portal application: login as stored,
    /// then the local part of an email, then the work email, and EMP_CODE last because it is not unique.
    /// </summary>
    private int ResolveStaffByLogin(string login, out string note)
    {
        note = "";
        if (string.IsNullOrEmpty(login)) { note = "No username."; return 0; }
        string full = login.Trim();
        string local = full.Contains("@") ? full.Substring(0, full.IndexOf('@')).Trim() : "";

        try
        {
            DataTable dt = ExecuteQuery(
                "SELECT empID FROM hrm_employee WHERE UPPER(TRIM(IFNULL(usernames,''))) = UPPER(@u) " +
                "AND TRIM(IFNULL(usernames,'')) NOT IN ('','-') ORDER BY empID LIMIT 1",
                new MySqlParameter("@u", full));
            if (dt.Rows.Count > 0) { note = "Matched on the username."; return Convert.ToInt32(dt.Rows[0][0]); }

            if (local != "")
            {
                dt = ExecuteQuery(
                    "SELECT empID FROM hrm_employee WHERE UPPER(TRIM(IFNULL(usernames,''))) = UPPER(@u) " +
                    "AND TRIM(IFNULL(usernames,'')) NOT IN ('','-') ORDER BY empID LIMIT 1",
                    new MySqlParameter("@u", local));
                if (dt.Rows.Count > 0) { note = "Matched on the username before the @."; return Convert.ToInt32(dt.Rows[0][0]); }
            }

            dt = ExecuteQuery(
                "SELECT empID FROM hrm_employee WHERE UPPER(TRIM(IFNULL(emp_email,''))) = UPPER(@u) " +
                "AND TRIM(IFNULL(emp_email,'')) NOT IN ('','-') ORDER BY empID LIMIT 1",
                new MySqlParameter("@u", full));
            if (dt.Rows.Count > 0) { note = "Matched on the email address."; return Convert.ToInt32(dt.Rows[0][0]); }

            foreach (string key in new string[] { full, local })
            {
                if (key == "") continue;
                DataTable c = ExecuteQuery(
                    "SELECT COUNT(*) n FROM hrm_employee WHERE UPPER(TRIM(IFNULL(EMP_CODE,''))) = UPPER(@u) " +
                    "AND TRIM(IFNULL(EMP_CODE,'')) NOT IN ('','-')", new MySqlParameter("@u", key));
                int n = c.Rows.Count > 0 ? Convert.ToInt32(c.Rows[0][0]) : 0;
                if (n == 1)
                {
                    dt = ExecuteQuery(
                        "SELECT empID FROM hrm_employee WHERE UPPER(TRIM(IFNULL(EMP_CODE,''))) = UPPER(@u) LIMIT 1",
                        new MySqlParameter("@u", key));
                    if (dt.Rows.Count > 0) { note = "Matched on the staff number."; return Convert.ToInt32(dt.Rows[0][0]); }
                }
                else if (n > 1)
                {
                    note = "Staff number " + key + " is shared by " + n + " employees, so it cannot identify one person.";
                    return 0;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning("HREmployees lookup: " + ex.Message);
            note = "The staff lookup could not be checked.";
            return 0;
        }

        note = "No employee record carries the username " + full + (local == "" ? "" : " or " + local) + ".";
        return 0;
    }

    /// <summary>The employee's staff code if another employee shares it, otherwise "".</summary>
    private string SharedStaffCode(int empID)
    {
        try
        {
            DataTable dt = ExecuteQuery(
                "SELECT e.EMP_CODE FROM hrm_employee e " +
                "WHERE e.empID=@id AND TRIM(IFNULL(e.EMP_CODE,'')) NOT IN ('','-') " +
                "  AND (SELECT COUNT(*) FROM hrm_employee x WHERE UPPER(TRIM(IFNULL(x.EMP_CODE,'')))=UPPER(TRIM(e.EMP_CODE))) > 1 LIMIT 1",
                new MySqlParameter("@id", empID));
            return dt.Rows.Count > 0 ? SafeVal(dt.Rows[0][0]) : "";
        }
        catch { return ""; }
    }

    /// <summary>
    /// Reset login: username from the email (else the existing username, else the staff number),
    /// account created or repaired in both sign-in stores (eadmin and the staff portal), approved,
    /// unlocked, one new password set in both and proved with ValidateUser, roles carried over.
    /// </summary>
    private void WriteFixLoginAjax()
    {
        int empID;
        if (!int.TryParse(Request.QueryString["id"], out empID) || empID <= 0)
        {
            WriteJson(new Dictionary<string, object> { { "error", "Choose an employee." } });
            return;
        }

        try
        {
            DataTable dt = ExecuteQuery(
                "SELECT emp_name, usernames, emp_email, EMP_CODE FROM hrm_employee WHERE empID = @id LIMIT 1",
                new MySqlParameter("@id", empID));
            if (dt.Rows.Count == 0)
            {
                WriteJson(new Dictionary<string, object> { { "error", "This employee was not found." } });
                return;
            }

            DataRow emp = dt.Rows[0];
            string existingUsername = NormalizeLoginValue(emp["usernames"]);
            string email = NormalizeLoginValue(emp["emp_email"]);
            string empCode = NormalizeLoginValue(emp["EMP_CODE"]);
            string manualPassword = SafeVal(Request["new_password"]).Trim();

            List<string> log = new List<string>();
            bool usernameWritten = false;

            // Username is the full email address when there is one.
            string targetUsername = !string.IsNullOrEmpty(email) ? email : existingUsername;
            if (!string.IsNullOrEmpty(email))
                log.Add("Username taken from the email address " + email + ".");

            // No email and no username: the staff number. The eadmin sign-in resolves a staff
            // number through hrm_employee, so it is a username the person can actually use.
            if (string.IsNullOrEmpty(targetUsername) && !string.IsNullOrEmpty(empCode))
            {
                targetUsername = empCode;
                log.Add("No email or username on record, so the staff number " + empCode + " is the username.");
            }

            if (string.IsNullOrEmpty(targetUsername))
            {
                WriteJson(new Dictionary<string, object> { { "error", "This employee has no email, username or staff number. Add an email to the record, then reset the login." } });
                return;
            }

            if (!string.Equals(existingUsername, targetUsername, StringComparison.OrdinalIgnoreCase))
            {
                ExecuteNonQuery(
                    "UPDATE hrm_employee SET usernames = @u WHERE empID = @id",
                    new MySqlParameter("@u", targetUsername),
                    new MySqlParameter("@id", empID));
                log.Add("Employee record updated with username " + targetUsername + ".");
                usernameWritten = true;
            }
            else
            {
                log.Add("Username " + targetUsername + " already on the employee record.");
            }

            // eadmin signs in through the default provider (campus_dynamics); the staff side of the
            // portal through MySQLMembershipProviderAdmin (campus_dynamics_portal). Each store is
            // checked and repaired on its own, both get the same password, and each is proved with
            // ValidateUser, which is what the two sign-in screens call.
            string finalPassword = !string.IsNullOrEmpty(manualPassword) ? manualPassword : GenerateStrongPassword();
            if (finalPassword.Length < 6)
            {
                WriteJson(new Dictionary<string, object> { { "error", "Use a password of at least 6 characters, or leave it blank to generate one." }, { "log", log } });
                return;
            }
            bool customApplied = !string.IsNullOrEmpty(manualPassword);
            log.Add(customApplied ? "Password set to the one entered." : "Password generated.");

            List<string> alts = new List<string>();
            if (!string.IsNullOrEmpty(existingUsername)) alts.Add(existingUsername);
            if (!string.IsNullOrEmpty(email)) alts.Add(email);
            if (!string.IsNullOrEmpty(empCode)) alts.Add(empCode);

            bool provisioned = false;
            MembershipUser user = null;
            List<KeyValuePair<string, string>> stores = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("eadmin", null),
                new KeyValuePair<string, string>("Staff portal", "MySQLMembershipProviderAdmin")
            };
            foreach (KeyValuePair<string, string> st in stores)
            {
                MembershipProvider p = st.Value == null ? Membership.Provider : Membership.Providers[st.Value];
                if (p == null) { log.Add(st.Key + ": not configured on this server, skipped."); continue; }
                string err; bool created;
                MembershipUser u = EnsureLogin(p, StoreConn(p), targetUsername, email, alts, finalPassword, st.Key, log, out created, out err);
                if (u == null)
                {
                    WriteJson(new Dictionary<string, object> { { "error", "The " + st.Key + " login could not be reset: " + err + "." }, { "log", log } });
                    return;
                }
                provisioned |= created;
                if (st.Value == null) user = u;
            }
            if (user == null)
            {
                WriteJson(new Dictionary<string, object> { { "error", "The eadmin login could not be reset." }, { "log", log } });
                return;
            }

            // Roles live against the username. A login that was renamed leaves its role behind
            // under the old name, and a login with no role signs in to an empty menu.
            FixRoles(targetUsername, alts, log);

            // The staff screens in eportal look the signed-in user up in hrm_employee before
            // showing anything. Perform the same lookup and report what it found.
            string resolveNote;
            int resolvedEmp = ResolveStaffByLogin(user.UserName, out resolveNote);
            bool profileOk = resolvedEmp == empID;
            if (profileOk)
                log.Add("Checked: signing in as " + user.UserName + " opens this employee's record. " + resolveNote);
            else if (resolvedEmp > 0)
                log.Add("Attention: " + user.UserName + " opens a different employee's record. " + resolveNote);
            else
                log.Add("Attention: the staff portal will not find this employee's record. " + resolveNote);

            string dupCode = SharedStaffCode(empID);
            if (dupCode != "") log.Add("Attention: staff number " + dupCode + " is shared with another employee.");

            WriteJson(new Dictionary<string, object>
            {
                { "success", true },
                { "username", user.UserName },
                { "password", finalPassword },
                { "provisioned", provisioned },
                { "username_written", usernameWritten },
                { "custom_applied", customApplied },
                { "profile_resolves", profileOk },
                { "profile_note", resolveNote },
                { "log", log }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("HREmployees reset login: " + ex);
            WriteJson(new Dictionary<string, object> { { "error", "The login could not be reset. Try again, and contact MIS if it keeps happening." } });
        }
    }

    private string StoreConn(MembershipProvider p)
    {
        return string.Equals(p.Name, "MySQLMembershipProviderAdmin", StringComparison.OrdinalIgnoreCase)
            ? ConfigurationManager.ConnectionStrings["campus_dynamics_portalConnectionString"].ConnectionString
            : ConnStr;
    }

    /// <summary>
    /// One store: find the account by its proper username, then by email, then by any older
    /// name it was created under; rename a found account to the proper username; create it if
    /// there is none; approve it; unlock it; set the password; and prove it with ValidateUser.
    /// Returns null with the reason when it cannot.
    /// </summary>
    private MembershipUser EnsureLogin(MembershipProvider p, string conn, string target, string email,
                                       List<string> alts, string password, string label, List<string> log,
                                       out bool created, out string error)
    {
        created = false; error = "";
        MembershipUser u = null;
        try { u = p.GetUser(target, false); } catch { }

        if (u == null)
        {
            List<string> tries = new List<string>(alts);
            if (!string.IsNullOrEmpty(email))
            {
                try { string byMail = p.GetUserNameByEmail(email); if (!string.IsNullOrEmpty(byMail)) tries.Insert(0, byMail); } catch { }
            }
            foreach (string n in tries)
            {
                if (string.IsNullOrEmpty(n) || string.Equals(n, target, StringComparison.OrdinalIgnoreCase)) continue;
                MembershipUser old = null;
                try { old = p.GetUser(n, false); } catch { }
                if (old == null) continue;
                try
                {
                    using (MySqlConnection c = new MySqlConnection(conn))
                    {
                        c.Open();
                        using (MySqlCommand cmd = new MySqlCommand("UPDATE my_aspnet_users SET name=@n WHERE name=@o", c))
                        {
                            cmd.Parameters.AddWithValue("@n", target);
                            cmd.Parameters.AddWithValue("@o", old.UserName);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    log.Add(label + ": login found as " + old.UserName + ", renamed to " + target + ".");
                    u = p.GetUser(target, false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceWarning("HREmployees rename login: " + ex.Message);
                    log.Add(label + ": login found as " + old.UserName + " but it could not be renamed.");
                }
                break;
            }
        }

        if (u == null)
        {
            // A users row with no membership row is invisible to the provider and blocks
            // creation with DuplicateUserName. It holds nothing usable, so it goes.
            try
            {
                using (MySqlConnection c = new MySqlConnection(conn))
                {
                    c.Open();
                    using (MySqlCommand cmd = new MySqlCommand(
                        "DELETE u FROM my_aspnet_users u LEFT JOIN my_aspnet_membership m ON m.userId=u.id " +
                        "WHERE u.name=@n AND m.userId IS NULL", c))
                    {
                        cmd.Parameters.AddWithValue("@n", target);
                        if (cmd.ExecuteNonQuery() > 0) log.Add(label + ": removed an incomplete login for " + target + ".");
                    }
                }
            }
            catch { }

            // provider.CreateUser cannot work here: MySql.Web 6.6.7 inserts into my_aspnet_users
            // positionally and this table has extra columns, so every call fails. The two rows are
            // written with explicit columns instead, and the password is then set through the
            // provider, so the hash and salt are still entirely the provider's.
            try { CreateLoginRows(conn, target, email); }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("HREmployees create login: " + ex);
                error = "the login could not be created"; return null;
            }
            u = p.GetUser(target, false);
            if (u == null) { error = "the login was written but cannot be read back"; return null; }
            created = true;
            log.Add(label + ": no login existed, created " + target + ".");
        }
        else log.Add(label + ": login " + u.UserName + " exists.");

        try
        {
            if (!u.IsApproved) { u.IsApproved = true; p.UpdateUser(u); log.Add(label + ": login was not approved, now approved."); }
            if (u.IsLockedOut) { p.UnlockUser(u.UserName); log.Add(label + ": login was locked, now unlocked."); }
            string tmp = p.ResetPassword(u.UserName, null);
            if (!p.ChangePassword(u.UserName, tmp, password)) { error = "the password could not be set"; return null; }

            // Staff are LECTURER in both stores; older repairs wrote ALUMNI onto staff accounts.
            using (MySqlConnection c = new MySqlConnection(conn))
            {
                c.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    "UPDATE my_aspnet_users SET user_type='LECTURER', " +
                    "user_verification_status=CASE WHEN user_verification_status='ALUMNI' THEN NULL ELSE user_verification_status END " +
                    "WHERE name=@n AND (IFNULL(user_type,'') NOT IN ('LECTURER','STAFF') OR user_verification_status='ALUMNI')", c))
                {
                    cmd.Parameters.AddWithValue("@n", target);
                    if (cmd.ExecuteNonQuery() > 0) log.Add(label + ": account type corrected to staff.");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("HREmployees set password: " + ex);
            error = "the password could not be set"; return null;
        }

        bool ok = false;
        try { ok = p.ValidateUser(target, password); } catch { }
        if (!ok) { error = "signing in with the new password still fails"; return null; }
        log.Add(label + ": checked, signing in as " + target + " with the new password works.");
        return p.GetUser(target, false);
    }

    /// <summary>
    /// Write a new login's two rows with explicit columns. The password column holds a random
    /// placeholder that nothing can sign in with; the caller replaces it through the provider.
    /// </summary>
    private void CreateLoginRows(string conn, string name, string email)
    {
        using (MySqlConnection c = new MySqlConnection(conn))
        {
            c.Open();
            using (MySqlTransaction tx = c.BeginTransaction())
            {
                int appId = 1;
                using (MySqlCommand cmd = new MySqlCommand("SELECT id FROM my_aspnet_applications WHERE name='/' LIMIT 1", c, tx))
                {
                    object o = cmd.ExecuteScalar();
                    if (o != null && o != DBNull.Value) appId = Convert.ToInt32(o);
                }
                long uid;
                using (MySqlCommand cmd = new MySqlCommand(
                    "INSERT INTO my_aspnet_users (applicationId, name, isAnonymous, lastActivityDate, user_type, verified_email) " +
                    "VALUES (@a, @n, 0, UTC_TIMESTAMP(), 'LECTURER', @e)", c, tx))
                {
                    cmd.Parameters.AddWithValue("@a", appId);
                    cmd.Parameters.AddWithValue("@n", name);
                    cmd.Parameters.AddWithValue("@e", string.IsNullOrEmpty(email) ? (object)DBNull.Value : email);
                    cmd.ExecuteNonQuery();
                    uid = cmd.LastInsertedId;
                }
                byte[] key = new byte[16];
                new System.Security.Cryptography.RNGCryptoServiceProvider().GetBytes(key);
                using (MySqlCommand cmd = new MySqlCommand(
                    "INSERT INTO my_aspnet_membership (userId, Email, Comment, Password, PasswordKey, PasswordFormat, " +
                    " IsApproved, LastActivityDate, LastLoginDate, LastPasswordChangedDate, CreationDate, IsLockedOut, " +
                    " LastLockedOutDate, FailedPasswordAttemptCount, FailedPasswordAttemptWindowStart, " +
                    " FailedPasswordAnswerAttemptCount, FailedPasswordAnswerAttemptWindowStart) " +
                    "VALUES (@u, @e, '', @pw, @k, 1, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP(), UTC_TIMESTAMP(), UTC_TIMESTAMP(), 0, " +
                    " UTC_TIMESTAMP(), 0, UTC_TIMESTAMP(), 0, UTC_TIMESTAMP())", c, tx))
                {
                    cmd.Parameters.AddWithValue("@u", uid);
                    cmd.Parameters.AddWithValue("@e", string.IsNullOrEmpty(email) ? (object)DBNull.Value : email);
                    cmd.Parameters.AddWithValue("@pw", Guid.NewGuid().ToString("N"));
                    cmd.Parameters.AddWithValue("@k", Convert.ToBase64String(key));
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }
        }
    }

    /// <summary>Carry roles from older names to the proper username; flag a login with none.</summary>
    private void FixRoles(string target, List<string> alts, List<string> log)
    {
        try
        {
            foreach (string n in alts)
            {
                if (string.IsNullOrEmpty(n) || string.Equals(n, target, StringComparison.OrdinalIgnoreCase)) continue;
                int moved = ExecuteNonQuery(
                    "UPDATE sys_user_roles SET username=@t WHERE username=@o AND role_id NOT IN " +
                    "(SELECT role_id FROM (SELECT role_id FROM sys_user_roles WHERE username=@t) x)",
                    new MySqlParameter("@t", target), new MySqlParameter("@o", n));
                if (moved > 0) log.Add("Moved " + moved + (moved == 1 ? " role" : " roles") + " from the old username " + n + ".");
            }
            DataTable r = ExecuteQuery(
                "SELECT GROUP_CONCAT(ro.role_name SEPARATOR ', ') roles FROM sys_user_roles ur JOIN sys_roles ro ON ro.id=ur.role_id " +
                "WHERE ur.username=@t AND ur.is_active=1 AND ro.is_active=1 AND (ur.expires_at IS NULL OR ur.expires_at>NOW())",
                new MySqlParameter("@t", target));
            string roles = r.Rows.Count > 0 ? SafeVal(r.Rows[0]["roles"]) : "";
            log.Add(string.IsNullOrEmpty(roles)
                ? "Attention: this login has no active role, so eadmin opens with an empty menu. Assign one in Access Control."
                : "Roles: " + roles + ".");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning("HREmployees roles: " + ex.Message);
            log.Add("Roles could not be checked.");
        }
    }

    private string NormalizeLoginValue(object value)
    {
        string text = SafeVal(value).Trim();
        if (text == "-" || text == "0") return string.Empty;
        return text;
    }

    private static string GenerateStrongPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%&*";
        string all = upper + lower + digits + symbols;

        Random rnd = new Random(Guid.NewGuid().GetHashCode());
        StringBuilder sb = new StringBuilder();
        sb.Append(upper[rnd.Next(upper.Length)]);
        sb.Append(lower[rnd.Next(lower.Length)]);
        sb.Append(digits[rnd.Next(digits.Length)]);
        sb.Append(symbols[rnd.Next(symbols.Length)]);
        for (int i = 0; i < 8; i++) sb.Append(all[rnd.Next(all.Length)]);
        return sb.ToString();
    }

    #endregion

    #region Photo

    private void WriteSetPhotoAjax()
    {
        int empID;
        if (!int.TryParse(Request.QueryString["id"], out empID) || empID <= 0)
        {
            WriteJson(new Dictionary<string, object> { { "error", "Choose an employee." } });
            return;
        }

        try
        {
            DataTable dt = ExecuteQuery("SELECT EMP_CODE, emp_name FROM hrm_employee WHERE empID = @id LIMIT 1", new MySqlParameter("@id", empID));
            if (dt.Rows.Count == 0)
            {
                WriteJson(new Dictionary<string, object> { { "error", "This employee was not found." } });
                return;
            }

            string empCode = SafeVal(dt.Rows[0]["EMP_CODE"]).Trim();
            if (string.IsNullOrEmpty(empCode))
            {
                WriteJson(new Dictionary<string, object> { { "error", "This employee has no staff number, so the photo cannot be saved." } });
                return;
            }

            HttpPostedFile postedFile = Request.Files["photoFile"];
            if (postedFile == null || postedFile.ContentLength <= 0)
            {
                WriteJson(new Dictionary<string, object> { { "error", "Choose a photo to upload." } });
                return;
            }
            if (postedFile.ContentLength > (5 * 1024 * 1024))
            {
                WriteJson(new Dictionary<string, object> { { "error", "The photo is larger than 5 MB." } });
                return;
            }

            string ext = Path.GetExtension(postedFile.FileName ?? string.Empty).ToLowerInvariant();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".bmp" && ext != ".gif")
            {
                WriteJson(new Dictionary<string, object> { { "error", "Use a JPG, PNG, BMP or GIF image." } });
                return;
            }

            byte[] originalBytes;
            using (MemoryStream ms = new MemoryStream())
            {
                postedFile.InputStream.CopyTo(ms);
                originalBytes = ms.ToArray();
            }

            imageManager im = new imageManager();
            byte[] thumbBytes = im.MakeThumb(originalBytes);

            string photosFolder = Server.MapPath("~/COOPERP/staffimages/");
            if (!Directory.Exists(photosFolder))
                Directory.CreateDirectory(photosFolder);

            string fileName = SanitizeStaffPhotoFileName(empCode) + ".jpg";
            File.WriteAllBytes(Path.Combine(photosFolder, fileName), thumbBytes);

            WriteJson(new Dictionary<string, object>
            {
                { "success", true },
                { "message", "Photo updated." },
                { "photoUrl", ResolveUrl("~/COOPERP/staffimages/" + fileName) + "?v=" + DateTime.UtcNow.Ticks }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("HREmployees photo: " + ex);
            WriteJson(new Dictionary<string, object> { { "error", "The photo could not be saved. Try a different image." } });
        }
    }

    #endregion

    #region Helpers

    protected string GetPhotoUrl(object empCode)
    {
        if (empCode == null || empCode == DBNull.Value) return "../staffimages/default.jpg";
        return "../staffimages/" + SanitizeStaffPhotoFileName(empCode.ToString()) + ".jpg";
    }

    private string SanitizeStaffPhotoFileName(string code)
    {
        string value = (code ?? string.Empty).Trim().Replace("/", "_").Replace("\\", "_");
        if (string.IsNullOrEmpty(value)) return "default";
        StringBuilder sb = new StringBuilder(value.Length);
        foreach (char c in value)
            sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
        string normalized = sb.ToString().Trim('_');
        return string.IsNullOrEmpty(normalized) ? "default" : normalized;
    }

    /// <summary>Current-contract status in words: Active, Ending, Past end date, Expired, Terminated, Resigned, No contract.</summary>
    private static string ContractWords(object contractId, object status, object end)
    {
        if (contractId == null || contractId == DBNull.Value) return "No contract";
        string s = SafeVal(status).ToUpperInvariant();
        switch (s)
        {
            case "EXPIRED":    return "Expired";
            case "TERMINATED": return "Terminated";
            case "RESIGNED":   return "Resigned";
        }
        if (end == null || end == DBNull.Value) return "Active";
        int days = (Convert.ToDateTime(end).Date - DateTime.Today).Days;
        if (days < 0) return "Past end date";
        if (days <= 90) return "Ending";
        return "Active";
    }

    private static string ContractBadge(object contractId, object status, object end)
    {
        string w = ContractWords(contractId, status, end);
        string kind;
        switch (w)
        {
            case "Active":        kind = "ok"; break;
            case "Ending":        kind = "warn"; break;
            case "Past end date":
            case "No contract":   kind = "bad"; break;
            default:              kind = "neutral"; break;
        }
        return "<span class='hr-badge hr-badge--" + kind + "'>" + w + "</span>";
    }

    protected string FormatAmount(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        decimal d;
        return decimal.TryParse(val.ToString(), out d) ? d.ToString("#,##0", CultureInfo.InvariantCulture) : Enc(val);
    }

    private static string FormatDate(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        DateTime dt;
        if (val is DateTime) dt = (DateTime)val;
        else if (!DateTime.TryParse(val.ToString(), out dt)) return "";
        if (dt.Year < 1900) return "";
        return dt.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
    }

    private static string SafeVal(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        return val.ToString();
    }

    /// <summary>Trimmed text; the placeholders "-" and "0" that old records carry become blank.</summary>
    private static string Clean(object val)
    {
        string s = HrExport.Clean(SafeVal(val));
        return (s == "-" || s == "--" || s == "N/A" || s == "NA") ? "" : s;
    }

    private static string Enc(object val) { return HttpUtility.HtmlEncode(Clean(val)); }

    private static string UsableEmail(object val) { string s = Clean(val); return s.IndexOf('@') > 0 ? s : ""; }

    private static string UsablePhone(object val)
    {
        string s = Clean(val);
        int digits = 0;
        foreach (char ch in s) if (char.IsDigit(ch)) digits++;
        return digits >= 9 ? s : "";
    }

    private static string Title(object val)
    {
        string s = Clean(val).Replace("_", " ");
        if (s.Length == 0) return s;
        return s.Substring(0, 1).ToUpperInvariant() + s.Substring(1).ToLowerInvariant();
    }

    private static string JoinNonEmpty(string a, string b)
    {
        if (a != "" && b != "") return a + ", " + b;
        return a + b;
    }

    private static long ToLong(object v) { long n; return v != null && v != DBNull.Value && long.TryParse(v.ToString(), out n) ? n : 0; }

    private string ToIsoDate(object val)
    {
        if (val == null || val == DBNull.Value) return string.Empty;
        DateTime dt;
        return DateTime.TryParse(val.ToString(), out dt) ? dt.ToString("yyyy-MM-dd") : string.Empty;
    }

    private static MySqlParameter[] CloneParams(List<MySqlParameter> parms)
    {
        List<MySqlParameter> copy = new List<MySqlParameter>();
        foreach (MySqlParameter p in parms) copy.Add(new MySqlParameter(p.ParameterName, p.Value));
        return copy.ToArray();
    }

    #endregion

    #region Data Access

    private DataTable ExecuteQuery(string sql, params MySqlParameter[] parms)
    {
        DataTable dt = new DataTable();
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null) foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) da.Fill(dt);
            }
        }
        return dt;
    }

    private int ExecuteNonQuery(string sql, params MySqlParameter[] parms)
    {
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null) foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                return cmd.ExecuteNonQuery();
            }
        }
    }

    #endregion
}
