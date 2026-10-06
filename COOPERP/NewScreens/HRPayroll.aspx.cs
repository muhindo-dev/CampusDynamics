using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

/// <summary>
/// Payroll runs: list, run detail with a read-only register, generation, approval and the
/// payroll exports and register print.
///
/// One-off allowances and deductions (hrm_allowance_records / hrm_deduction_records):
///   generation reserves them for the run (payroll_id = run, status stays PENDING);
///   approve and lock settles them (SETTLED, payment_date);
///   regeneration, cancel and delete release them (payroll_id = NULL, PENDING) first,
///   so a regenerated run includes them again and a cancelled run frees them.
/// The register is read-only: pay changes go through allowances, deductions or the employee's
/// contract and are applied by regenerating the run (same formulas for every payslip).
/// Every write that touches several rows runs in one transaction.
/// </summary>
public partial class COOPERP_NewScreens_HRPayroll : System.Web.UI.Page
{
    private static readonly string[] MONTH_NAMES = {
        "", "JANUARY", "FEBRUARY", "MARCH", "APRIL", "MAY", "JUNE",
        "JULY", "AUGUST", "SEPTEMBER", "OCTOBER", "NOVEMBER", "DECEMBER"
    };
    private static readonly CultureInfo IC = CultureInfo.InvariantCulture;

    /// <summary>An error whose message is written for the user.</summary>
    private class UserError : Exception { public UserError(string m) : base(m) { } }

    // =================================================================
    //  Infrastructure
    // =================================================================

    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    private static MySqlParameter P(string name, object value) { return new MySqlParameter(name, value ?? DBNull.Value); }

    private DataTable Q(string sql, params MySqlParameter[] parms)
    {
        using (MySqlConnection c = new MySqlConnection(ConnStr))
        {
            c.Open();
            return Q(c, null, sql, parms);
        }
    }

    private static DataTable Q(MySqlConnection c, MySqlTransaction tx, string sql, params MySqlParameter[] parms)
    {
        DataTable dt = new DataTable();
        using (MySqlCommand cmd = new MySqlCommand(sql, c, tx))
        {
            if (parms != null) foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
            using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) da.Fill(dt);
        }
        return dt;
    }

    private int X(string sql, params MySqlParameter[] parms)
    {
        using (MySqlConnection c = new MySqlConnection(ConnStr))
        {
            c.Open();
            return X(c, null, sql, parms);
        }
    }

    private static int X(MySqlConnection c, MySqlTransaction tx, string sql, params MySqlParameter[] parms)
    {
        using (MySqlCommand cmd = new MySqlCommand(sql, c, tx))
        {
            if (parms != null) foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
            return cmd.ExecuteNonQuery();
        }
    }

    private static decimal SafeDecimal(object val)
    {
        if (val == null || val == DBNull.Value) return 0m;
        decimal d;
        return decimal.TryParse(val.ToString(), NumberStyles.Any, IC, out d) ? d : 0m;
    }

    private static int SafeInt(object val)
    {
        if (val == null || val == DBNull.Value) return 0;
        int i;
        return int.TryParse(val.ToString(), out i) ? i : 0;
    }

    private static string Str(object v) { return v == null || v == DBNull.Value ? "" : v.ToString().Trim(); }

    private static string E(object s) { return HttpUtility.HtmlEncode(HrExport.Clean(Str(s))); }

    private static string A(object s) { return HttpUtility.HtmlAttributeEncode(Str(s)); }

    private static string Money(object v) { return SafeDecimal(v).ToString("#,##0", IC); }

    private static string ShortDate(object v)
    {
        if (v == null || v == DBNull.Value) return "";
        DateTime d;
        return DateTime.TryParse(v.ToString(), out d) && d.Year > 1900 ? d.ToString("d MMM yyyy", IC) : "";
    }

    private static string Trunc(string s, int n) { s = s ?? ""; return s.Length <= n ? s : s.Substring(0, n); }

    private string CurrentUser() { return HrAccess.Username(); }

    private int GetCurrentEmployeeId()
    {
        string user = CurrentUser();
        if (string.IsNullOrEmpty(user)) return 0;
        DataTable dt = Q("SELECT empID FROM hrm_employee WHERE usernames = @u LIMIT 1", P("@u", user));
        return dt.Rows.Count > 0 ? SafeInt(dt.Rows[0]["empID"]) : 0;
    }

    private void Log(string what, string detail)
    {
        try
        {
            X("INSERT INTO acad_activity_log (user_id, page_function, par, comments, access_date) VALUES (@u, 'HR Payroll', @p, @c, NOW())",
                P("@u", Trunc(CurrentUser(), 100)), P("@p", Trunc(detail, 300)), P("@c", Trunc(what, 200)));
        }
        catch { /* logging must never break payroll */ }
    }

    private static string MonthTitle(object monthObj)
    {
        int m = SafeInt(monthObj);
        if (m >= 1 && m <= 12) return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(MONTH_NAMES[m].ToLowerInvariant());
        string raw = Str(monthObj);
        return raw == "" ? "" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(raw.ToLowerInvariant());
    }

    private static string Period(object month, object year) { return (MonthTitle(month) + " " + Str(year)).Trim(); }

    private static string StatusWord(string s)
    {
        switch ((s ?? "").ToUpperInvariant())
        {
            case "PENDING": return "Pending";
            case "PROCESSED": return "Approved";
            case "CANCELLED": return "Cancelled";
            case "APPROVED": return "Approved";
            case "REJECTED": return "Rejected";
            default: return s ?? "";
        }
    }

    private static string Badge(string status)
    {
        string s = (status ?? "").ToUpperInvariant();
        string kind = s == "PROCESSED" || s == "APPROVED" ? "ok" : s == "PENDING" ? "warn" : s == "REJECTED" ? "bad" : "neutral";
        return "<span class=\"hr-badge hr-badge--" + kind + "\">" + E(StatusWord(s)) + "</span>";
    }

    private static string Coverage(object type, object ids)
    {
        string t = Str(type).ToUpperInvariant();
        string list = Str(ids);
        int n = list == "" ? 0 : list.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Length;
        if (t == "DEPARTMENT") return n == 1 ? "1 department" : n + " departments";
        if (t == "EMPLOYEE") return n == 1 ? "1 selected employee" : n + " selected employees";
        return "All staff";
    }

    // =================================================================
    //  Page load and dispatch
    // =================================================================

    protected void Page_Load(object sender, EventArgs e)
    {
        string action = Request.QueryString["action"];
        if (!string.IsNullOrEmpty(action))
        {
            bool file = action == "export" || action == "print";
            if (!HrAccess.RequireHr(!file)) return;
            if (file)
            {
                HandleFile(action);
                Response.End();
                return;
            }
            string json;
            try { json = HandleAction(action); }
            catch (UserError ue) { json = Result(false, ue.Message); }
            catch (Exception ex)
            {
                Log("Error", action + ": " + ex.Message);
                json = Result(false, "The action could not be completed. Please try again.");
            }
            Response.Clear();
            Response.ContentType = "application/json";
            Response.Write(json);
            Response.End();
            return;
        }

        if (!HrAccess.RequireHr(false)) return;
        EnsureTablesExist();
        try
        {
            int runId = SafeInt(Request.QueryString["run"]);
            if (runId > 0) RenderDetail(runId);
            else RenderList();
            RenderCreateLists();
        }
        catch (Exception ex)
        {
            Log("Error", "Page load: " + ex.Message);
            litBody.Text = "<div class=\"hr-notice hr-notice--bad\">Payroll could not be loaded. Please refresh the page.</div>";
        }
    }

    private static string Result(bool ok, string message)
    {
        Dictionary<string, object> d = new Dictionary<string, object>();
        d["ok"] = ok;
        d["message"] = message;
        return new JavaScriptSerializer().Serialize(d);
    }

    private string HandleAction(string action)
    {
        if (Request.HttpMethod != "POST") throw new UserError("Invalid request.");
        int id = SafeInt(Request.Form["id"]);
        switch (action)
        {
            case "create": return CreateRun();
            case "preview": return Preview(id);
            case "generate":
                GenerateRun(id);
                return Result(true, "Payslips generated.");
            case "approve":
                ApproveRun(id);
                return Result(true, "Payroll run approved and locked.");
            case "cancel":
                CancelRun(id);
                return Result(true, "Payroll run cancelled.");
            case "delete":
                return Result(true, DeleteRun(id));
            case "bulk": return Bulk();
        }
        throw new UserError("Unknown action.");
    }

    // =================================================================
    //  Schema (unchanged from the previous version; runs once per app start)
    // =================================================================

    private void EnsureTablesExist()
    {
        if (Application["hr_payroll_schema_ok"] != null) return;
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                X(conn, null, @"
                    CREATE TABLE IF NOT EXISTS hrm_config (
                        id INT NOT NULL DEFAULT 1,
                        paye_b1_min DECIMAL(15,2) NOT NULL DEFAULT 0,
                        paye_b1_max DECIMAL(15,2) NOT NULL DEFAULT 235000,
                        paye_b1_rate DECIMAL(5,2) NOT NULL DEFAULT 0.00,
                        paye_b2_min DECIMAL(15,2) NOT NULL DEFAULT 235001,
                        paye_b2_max DECIMAL(15,2) NOT NULL DEFAULT 335000,
                        paye_b2_rate DECIMAL(5,2) NOT NULL DEFAULT 10.00,
                        paye_b3_min DECIMAL(15,2) NOT NULL DEFAULT 335001,
                        paye_b3_max DECIMAL(15,2) NOT NULL DEFAULT 410000,
                        paye_b3_rate DECIMAL(5,2) NOT NULL DEFAULT 20.00,
                        paye_b4_min DECIMAL(15,2) NOT NULL DEFAULT 410001,
                        paye_b4_max DECIMAL(15,2) NOT NULL DEFAULT 10000000,
                        paye_b4_rate DECIMAL(5,2) NOT NULL DEFAULT 30.00,
                        paye_b5_min DECIMAL(15,2) NOT NULL DEFAULT 10000001,
                        paye_b5_max DECIMAL(15,2) NULL,
                        paye_b5_rate DECIMAL(5,2) NOT NULL DEFAULT 40.00,
                        nssf_employee_rate DECIMAL(5,2) NOT NULL DEFAULT 5.00,
                        nssf_employer_rate DECIMAL(5,2) NOT NULL DEFAULT 10.00,
                        should_charge_kabaka ENUM('YES','NO') NOT NULL DEFAULT 'YES',
                        kabaka_rate DECIMAL(5,2) NOT NULL DEFAULT 1.00,
                        should_charge_local_tax ENUM('YES','NO') NOT NULL DEFAULT 'NO',
                        local_tax_rate DECIMAL(5,2) NOT NULL DEFAULT 1.00,
                        default_annual_leave_days INT NOT NULL DEFAULT 30,
                        default_maternity_leave_days INT NOT NULL DEFAULT 60,
                        default_paternity_leave_days INT NOT NULL DEFAULT 4,
                        default_sick_leave_days INT NOT NULL DEFAULT 30,
                        financial_year_start_month TINYINT NOT NULL DEFAULT 7,
                        probation_period_months INT NOT NULL DEFAULT 3,
                        notice_period_days INT NOT NULL DEFAULT 30,
                        overtime_rate_multiplier DECIMAL(4,2) NOT NULL DEFAULT 1.50,
                        gratuity_rate DECIMAL(5,2) NOT NULL DEFAULT 5.00,
                        working_days_per_month INT NOT NULL DEFAULT 22,
                        working_hours_per_day INT NOT NULL DEFAULT 8,
                        last_updated DATETIME NULL,
                        updated_by VARCHAR(100) NULL,
                        PRIMARY KEY (id)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
                X(conn, null, "INSERT IGNORE INTO hrm_config (id) VALUES (1)");
                X(conn, null, @"
                    CREATE TABLE IF NOT EXISTS hrm_payslips (
                        ID INT NOT NULL AUTO_INCREMENT,
                        payroll_id INT NOT NULL,
                        empID INT NOT NULL,
                        payroll_year INT NOT NULL,
                        payroll_month INT NOT NULL,
                        payroll_month_name ENUM('JANUARY','FEBRUARY','MARCH','APRIL','MAY','JUNE',
                            'JULY','AUGUST','SEPTEMBER','OCTOBER','NOVEMBER','DECEMBER') NOT NULL,
                        basic_pay DECIMAL(15,2) NOT NULL DEFAULT 0.00,
                        gross_salary DECIMAL(15,2) NOT NULL DEFAULT 0.00,
                        total_allowances DECIMAL(15,2) NOT NULL DEFAULT 0.00,
                        allowance_amount DECIMAL(15,2) NOT NULL DEFAULT 0.00,
                        allowance_details TEXT NULL,
                        total_deductions DECIMAL(15,2) NOT NULL DEFAULT 0.00,
                        deduction_amount DECIMAL(15,2) NOT NULL DEFAULT 0.00,
                        deduction_details TEXT NULL,
                        paye DECIMAL(15,2) NOT NULL DEFAULT 0.00,
                        nssf DECIMAL(15,2) NOT NULL DEFAULT 0.00,
                        kabaka_contribution DECIMAL(15,2) NOT NULL DEFAULT 0.00,
                        local_tax DECIMAL(15,2) NOT NULL DEFAULT 0.00,
                        net_salary DECIMAL(15,2) NOT NULL DEFAULT 0.00,
                        status ENUM('PENDING','APPROVED','REJECTED') NOT NULL DEFAULT 'PENDING',
                        date_generated DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        approved_by VARCHAR(100) NULL,
                        date_approved DATETIME NULL,
                        rejection_reason TEXT NULL,
                        PRIMARY KEY (ID),
                        UNIQUE KEY uq_payslip_payroll_emp (payroll_id, empID),
                        INDEX idx_ps_payroll (payroll_id),
                        INDEX idx_ps_emp (empID),
                        INDEX idx_ps_status (status),
                        INDEX idx_ps_period (payroll_year, payroll_month)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
                X(conn, null, @"
                    CREATE TABLE IF NOT EXISTS hrm_deduction_records (
                        id INT NOT NULL AUTO_INCREMENT,
                        empID INT NOT NULL,
                        deduction_type VARCHAR(100) NOT NULL,
                        amount DECIMAL(15,2) NOT NULL DEFAULT 0.00,
                        description TEXT NULL,
                        date_recorded DATE NOT NULL,
                        status ENUM('PENDING','SETTLED','CANCELLED') NOT NULL DEFAULT 'PENDING',
                        to_deduct_month ENUM('JANUARY','FEBRUARY','MARCH','APRIL','MAY','JUNE',
                            'JULY','AUGUST','SEPTEMBER','OCTOBER','NOVEMBER','DECEMBER') NOT NULL,
                        to_deduct_year INT NOT NULL,
                        payment_date DATE NULL,
                        payroll_id INT NULL,
                        recorded_by VARCHAR(100) NULL,
                        created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        PRIMARY KEY (id),
                        INDEX idx_ded_emp_period (empID, to_deduct_month, to_deduct_year),
                        INDEX idx_ded_status (status),
                        INDEX idx_ded_payroll (payroll_id)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
                X(conn, null, @"
                    CREATE TABLE IF NOT EXISTS hrm_allowance_records (
                        id INT NOT NULL AUTO_INCREMENT,
                        empID INT NOT NULL,
                        allowance_type VARCHAR(100) NOT NULL,
                        amount DECIMAL(15,2) NOT NULL DEFAULT 0.00,
                        description TEXT NULL,
                        date_recorded DATE NOT NULL,
                        status ENUM('PENDING','SETTLED','CANCELLED') NOT NULL DEFAULT 'PENDING',
                        to_add_month ENUM('JANUARY','FEBRUARY','MARCH','APRIL','MAY','JUNE',
                            'JULY','AUGUST','SEPTEMBER','OCTOBER','NOVEMBER','DECEMBER') NOT NULL,
                        to_add_year INT NOT NULL,
                        payment_date DATE NULL,
                        payroll_id INT NULL,
                        recorded_by VARCHAR(100) NULL,
                        created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        PRIMARY KEY (id),
                        INDEX idx_alw_emp_period (empID, to_add_month, to_add_year),
                        INDEX idx_alw_status (status),
                        INDEX idx_alw_payroll (payroll_id)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
                AddColumnIfMissing(conn, "hrm_payroll", "payroll_status",
                    "ENUM('PENDING','PROCESSED','CANCELLED') NOT NULL DEFAULT 'PENDING' AFTER payroll_date");
                AddColumnIfMissing(conn, "hrm_payroll", "target_type", "ENUM('ALL','DEPARTMENT','EMPLOYEE') NOT NULL DEFAULT 'ALL'");
                AddColumnIfMissing(conn, "hrm_payroll", "target_ids", "TEXT NULL");
                AddColumnIfMissing(conn, "hrm_payroll", "should_include_deductions", "ENUM('YES','NO') NOT NULL DEFAULT 'YES'");
                AddColumnIfMissing(conn, "hrm_payroll", "should_include_allowances", "ENUM('YES','NO') NOT NULL DEFAULT 'YES'");
                AddColumnIfMissing(conn, "hrm_payroll", "date_processed", "DATETIME NULL");
                AddColumnIfMissing(conn, "hrm_payroll", "processed_by", "VARCHAR(100) NULL");
                AddColumnIfMissing(conn, "hrm_payroll", "payroll_comments", "TEXT NULL");
            }
            Application["hr_payroll_schema_ok"] = true;
        }
        catch { /* the queries below report a missing table */ }
    }

    private static void AddColumnIfMissing(MySqlConnection conn, string table, string column, string definition)
    {
        try
        {
            DataTable dt = Q(conn, null, "SELECT COUNT(*) AS n FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @t AND COLUMN_NAME = @c",
                P("@t", table), P("@c", column));
            if (dt.Rows.Count > 0 && SafeInt(dt.Rows[0]["n"]) == 0)
                X(conn, null, "ALTER TABLE `" + table + "` ADD COLUMN `" + column + "` " + definition);
        }
        catch { }
    }

    // =================================================================
    //  Payroll configuration and PAYE (calculations unchanged)
    // =================================================================

    private struct PayrollConfig
    {
        public decimal PayeB1Max,  PayeB1Rate;
        public decimal PayeB2Max,  PayeB2Rate;
        public decimal PayeB3Max,  PayeB3Rate;
        public decimal PayeB4Max,  PayeB4Rate;
        public decimal             PayeB5Rate;
        public decimal NssfEmployeeRate;
        public bool    ChargeKabaka;
        public decimal KabakaRate;
        public bool    ChargeLocalTax;
        public decimal LocalTaxRate;
    }

    private PayrollConfig LoadPayrollConfig()
    {
        PayrollConfig def = new PayrollConfig
        {
            PayeB1Max = 235000m,   PayeB1Rate = 0m,
            PayeB2Max = 335000m,   PayeB2Rate = 10m,
            PayeB3Max = 410000m,   PayeB3Rate = 20m,
            PayeB4Max = 10000000m, PayeB4Rate = 30m,
                                   PayeB5Rate = 40m,
            NssfEmployeeRate = 5m,
            ChargeKabaka   = true,  KabakaRate   = 1m,
            ChargeLocalTax = false, LocalTaxRate = 1m
        };
        try
        {
            DataTable dt = Q(@"
                SELECT paye_b1_max, paye_b1_rate, paye_b2_max, paye_b2_rate, paye_b3_max, paye_b3_rate,
                       paye_b4_max, paye_b4_rate, paye_b5_rate, nssf_employee_rate,
                       should_charge_kabaka, kabaka_rate, should_charge_local_tax, local_tax_rate
                FROM hrm_config WHERE id = 1");
            if (dt.Rows.Count == 0) return def;
            DataRow r = dt.Rows[0];
            def.PayeB1Max  = ToDecimal(r["paye_b1_max"],  def.PayeB1Max);
            def.PayeB1Rate = ToDecimal(r["paye_b1_rate"], def.PayeB1Rate);
            def.PayeB2Max  = ToDecimal(r["paye_b2_max"],  def.PayeB2Max);
            def.PayeB2Rate = ToDecimal(r["paye_b2_rate"], def.PayeB2Rate);
            def.PayeB3Max  = ToDecimal(r["paye_b3_max"],  def.PayeB3Max);
            def.PayeB3Rate = ToDecimal(r["paye_b3_rate"], def.PayeB3Rate);
            def.PayeB4Max  = ToDecimal(r["paye_b4_max"],  def.PayeB4Max);
            def.PayeB4Rate = ToDecimal(r["paye_b4_rate"], def.PayeB4Rate);
            def.PayeB5Rate = ToDecimal(r["paye_b5_rate"], def.PayeB5Rate);
            def.NssfEmployeeRate = ToDecimal(r["nssf_employee_rate"], def.NssfEmployeeRate);
            def.ChargeKabaka     = r["should_charge_kabaka"].ToString().ToUpper() == "YES";
            def.KabakaRate       = ToDecimal(r["kabaka_rate"], def.KabakaRate);
            def.ChargeLocalTax   = r["should_charge_local_tax"].ToString().ToUpper() == "YES";
            def.LocalTaxRate     = ToDecimal(r["local_tax_rate"], def.LocalTaxRate);
        }
        catch { /* hrm_config missing: defaults */ }
        return def;
    }

    private static decimal ToDecimal(object val, decimal fallback)
    {
        if (val == null || val == DBNull.Value) return fallback;
        decimal d;
        return decimal.TryParse(val.ToString(), out d) ? d : fallback;
    }

    private static decimal CalculatePAYE(decimal taxableMonthly, PayrollConfig cfg)
    {
        if (taxableMonthly <= 0 || cfg.PayeB1Rate == 100) return 0;
        decimal paye = 0;
        decimal inB1 = Math.Min(taxableMonthly, cfg.PayeB1Max);
        paye += inB1 * cfg.PayeB1Rate / 100;
        if (taxableMonthly > cfg.PayeB1Max)
        {
            decimal inB2 = Math.Min(taxableMonthly, cfg.PayeB2Max) - cfg.PayeB1Max;
            if (inB2 > 0) paye += inB2 * cfg.PayeB2Rate / 100;
        }
        if (taxableMonthly > cfg.PayeB2Max)
        {
            decimal inB3 = Math.Min(taxableMonthly, cfg.PayeB3Max) - cfg.PayeB2Max;
            if (inB3 > 0) paye += inB3 * cfg.PayeB3Rate / 100;
        }
        if (taxableMonthly > cfg.PayeB3Max)
        {
            decimal inB4 = Math.Min(taxableMonthly, cfg.PayeB4Max) - cfg.PayeB3Max;
            if (inB4 > 0) paye += inB4 * cfg.PayeB4Rate / 100;
        }
        if (taxableMonthly > cfg.PayeB4Max)
        {
            decimal inB5 = taxableMonthly - cfg.PayeB4Max;
            paye += inB5 * cfg.PayeB5Rate / 100;
        }
        return Math.Round(paye, 0);
    }

    // =================================================================
    //  Create, preview, generate
    // =================================================================

    private string CreateRun()
    {
        string title = (Request.Form["title"] ?? "").Trim();
        int month = SafeInt(Request.Form["month"]);
        int year = SafeInt(Request.Form["year"]);
        string targetType = (Request.Form["target"] ?? "ALL").ToUpperInvariant();
        string comments = (Request.Form["comments"] ?? "").Trim();
        string inclAllow = Request.Form["allowances"] == "NO" ? "NO" : "YES";
        string inclDed = Request.Form["deductions"] == "NO" ? "NO" : "YES";

        if (title == "") throw new UserError("Enter a title for the payroll run.");
        if (title.Length > 45) throw new UserError("The title can have at most 45 characters.");
        if (comments.Length > 45) throw new UserError("Notes can have at most 45 characters.");
        if (month < 1 || month > 12) throw new UserError("Select the month.");
        if (year < 2000 || year > 2099) throw new UserError("Select the year.");
        if (targetType != "ALL" && targetType != "DEPARTMENT" && targetType != "EMPLOYEE") targetType = "ALL";

        string targetIds = "";
        if (targetType != "ALL")
        {
            List<string> ids = new List<string>();
            foreach (string s in (Request.Form["ids"] ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int v;
                if (int.TryParse(s.Trim(), out v) && v > 0 && !ids.Contains(v.ToString(IC))) ids.Add(v.ToString(IC));
            }
            if (ids.Count == 0)
                throw new UserError(targetType == "DEPARTMENT" ? "Select at least one department." : "Select at least one employee.");
            targetIds = string.Join(",", ids.ToArray());
        }

        int empId = GetCurrentEmployeeId();
        X(@"INSERT INTO hrm_payroll (payroll_title, payroll_month, payroll_year, payroll_comments,
                prepared_by, checked_by, approved_by, total_amount,
                payroll_date, lockStatus, payroll_status, target_type, target_ids,
                should_include_deductions, should_include_allowances)
            VALUES (@title, @month, @year, @comments, @prepBy, @checkBy, 0, 0,
                NOW(), 0, 'PENDING', @targetType, @targetIds, @inclDed, @inclAllow)",
            P("@title", title), P("@month", month), P("@year", year), P("@comments", comments),
            P("@prepBy", empId), P("@checkBy", empId), P("@targetType", targetType),
            P("@targetIds", targetIds), P("@inclDed", inclDed), P("@inclAllow", inclAllow));
        Log("Payroll run created", title + " (" + Period(month, year) + ")");
        return Result(true, "Payroll run created.");
    }

    private string Preview(int payrollID)
    {
        DataTable dt = Q("SELECT payroll_title, payroll_month, payroll_year, payroll_status, target_type, target_ids, should_include_deductions, should_include_allowances FROM hrm_payroll WHERE ID=@id", P("@id", payrollID));
        if (dt.Rows.Count == 0) throw new UserError("Payroll run not found.");
        DataRow pr = dt.Rows[0];
        if (Str(pr["payroll_status"]) != "PENDING") throw new UserError("Only a pending payroll run can be generated.");
        int staff;
        using (MySqlConnection c = new MySqlConnection(ConnStr)) { c.Open(); staff = ResolveTargetEmployees(c, null, payrollID).Count; }
        DataTable cnt = Q("SELECT COUNT(*) AS n, COALESCE(SUM(status='APPROVED'),0) AS a FROM hrm_payslips WHERE payroll_id=@id", P("@id", payrollID));
        int existing = SafeInt(cnt.Rows[0]["n"]), approved = SafeInt(cnt.Rows[0]["a"]);

        Dictionary<string, object> d = new Dictionary<string, object>();
        d["ok"] = true;
        d["title"] = Str(pr["payroll_title"]);
        d["period"] = Period(pr["payroll_month"], pr["payroll_year"]);
        d["coverage"] = Coverage(pr["target_type"], pr["target_ids"]);
        d["staff"] = staff;
        d["allowances"] = Str(pr["should_include_allowances"]).ToUpperInvariant() == "YES" ? "Included" : "Not included";
        d["deductions"] = Str(pr["should_include_deductions"]).ToUpperInvariant() == "YES" ? "Included" : "Not included";
        d["replace"] = existing - approved;
        d["keep"] = approved;
        return new JavaScriptSerializer().Serialize(d);
    }

    private void GenerateRun(int payrollID)
    {
        PayrollConfig cfg = LoadPayrollConfig();
        string title;
        using (MySqlConnection c = new MySqlConnection(ConnStr))
        {
            c.Open();
            using (MySqlTransaction tx = c.BeginTransaction())
            {
                title = GeneratePayslips(c, tx, payrollID, cfg);
                tx.Commit();
            }
        }
        Log("Payslips generated", title + " (run " + payrollID + ")");
    }

    /// <summary>
    /// Builds the payslips of a pending run. Approved payslips are kept; pending and rejected ones
    /// are rebuilt. The pay formulas are the ones used before the redesign, unchanged.
    /// </summary>
    private string GeneratePayslips(MySqlConnection c, MySqlTransaction tx, int payrollID, PayrollConfig cfg)
    {
        DataTable dtPayroll = Q(c, tx,
            @"SELECT payroll_title, payroll_month, payroll_year, payroll_status, should_include_deductions, should_include_allowances
              FROM hrm_payroll WHERE ID=@id FOR UPDATE", P("@id", payrollID));
        if (dtPayroll.Rows.Count == 0) throw new UserError("Payroll run not found.");

        DataRow pr = dtPayroll.Rows[0];
        if (Str(pr["payroll_status"]) != "PENDING") throw new UserError("Only a pending payroll run can be generated.");
        int payrollMonth = SafeInt(pr["payroll_month"]);
        int payrollYear = SafeInt(pr["payroll_year"]);
        bool inclDeductions = pr["should_include_deductions"].ToString().ToUpper() == "YES";
        bool inclAllowances = pr["should_include_allowances"].ToString().ToUpper() == "YES";
        string payrollMonthName = (payrollMonth >= 1 && payrollMonth <= 12) ? MONTH_NAMES[payrollMonth] : "";

        // Free the one-off items this run held, except those of payslips that stay (approved).
        ReleaseItems(c, tx, payrollID, true);

        X(c, tx, "DELETE FROM hrm_payslips WHERE payroll_id=@id AND status IN ('PENDING','REJECTED')", P("@id", payrollID));
        X(c, tx, "DELETE FROM hrm_payroll_details WHERE payrollID=@id", P("@id", payrollID));

        List<int> targetEmps = ResolveTargetEmployees(c, tx, payrollID);
        decimal totalNet = 0m;

        foreach (int empID in targetEmps)
        {
            DataTable dtApproved = Q(c, tx,
                "SELECT ID, basic_pay, gross_salary, total_allowances, paye, nssf, kabaka_contribution, local_tax, total_deductions, net_salary " +
                "FROM hrm_payslips WHERE payroll_id=@pid AND empID=@eid AND status='APPROVED'",
                P("@pid", payrollID), P("@eid", empID));
            if (dtApproved.Rows.Count > 0)
            {
                DataRow ap = dtApproved.Rows[0];
                decimal apNetSalary = SafeDecimal(ap["net_salary"]);
                totalNet += apNetSalary;
                try
                {
                    X(c, tx, @"INSERT INTO hrm_payroll_details (payrollID, empID, basic_pay, paye, nssf, total_allowances, total_deductions, gross_pay, net_pay)
                               VALUES (@pid, @eid, @basic, @paye, @nssf, @allow, @ded, @gross, @net)",
                        P("@pid", payrollID), P("@eid", empID), P("@basic", SafeDecimal(ap["basic_pay"])),
                        P("@paye", SafeDecimal(ap["paye"])), P("@nssf", SafeDecimal(ap["nssf"])),
                        P("@allow", SafeDecimal(ap["total_allowances"])), P("@ded", SafeDecimal(ap["total_deductions"])),
                        P("@gross", SafeDecimal(ap["gross_salary"])), P("@net", apNetSalary));
                }
                catch { /* legacy table, not critical */ }
                continue;
            }

            decimal basicPay = 0m;
            DataTable dtContract = Q(c, tx, @"
                SELECT IFNULL(ps.basicpay, c.fixedamount) AS basic_pay
                FROM hrm_emp_contracts c
                LEFT JOIN hrm_payscales ps ON ps.ID = c.payscale
                WHERE c.empID = @eid
                ORDER BY c.ID DESC LIMIT 1", P("@eid", empID));
            if (dtContract.Rows.Count > 0) basicPay = SafeDecimal(dtContract.Rows[0]["basic_pay"]);
            if (basicPay <= 0) continue;

            // Standard allowances
            decimal stdAllowances = 0m;
            List<string> allowanceDetails = new List<string>();
            DataTable dtAllow = Q(c, tx, @"
                SELECT ad.dedall_name, IFNULL(das.custom_amount, ad.dedall_amount) AS amount, ad.computation_by
                FROM hrm_allowance_deductions ad
                JOIN hrm_ded_allowance_stafflist das ON das.ded_allID = ad.ID
                LEFT JOIN hrm_exemptions ex ON ex.empID = @eid AND ex.ded_allID = ad.ID
                WHERE das.empID = @eid AND ad.ded_allowance = 'ALLOWANCE' AND ex.ID IS NULL", P("@eid", empID));
            foreach (DataRow a in dtAllow.Rows)
            {
                decimal amt = SafeDecimal(a["amount"]);
                string compBy = a["computation_by"] == null || a["computation_by"] == DBNull.Value ? "" : a["computation_by"].ToString();
                decimal final = compBy.ToUpper() == "PERCENTAGE" ? basicPay * amt / 100 : amt;
                stdAllowances += final;
                allowanceDetails.Add(a["dedall_name"].ToString() + ":" + Math.Round(final, 0).ToString("F0"));
            }

            // One-off allowances for the period (not held by another run)
            decimal adHocAllowanceAmount = 0m;
            if (inclAllowances && !string.IsNullOrEmpty(payrollMonthName))
            {
                DataTable dtAdAllow = Q(c, tx, @"
                    SELECT allowance_type, amount FROM hrm_allowance_records
                    WHERE empID=@eid AND to_add_month=@month AND to_add_year=@year AND status='PENDING'
                      AND (payroll_id IS NULL OR payroll_id=@pid)",
                    P("@eid", empID), P("@month", payrollMonthName), P("@year", payrollYear), P("@pid", payrollID));
                foreach (DataRow ar in dtAdAllow.Rows)
                {
                    decimal amt = SafeDecimal(ar["amount"]);
                    adHocAllowanceAmount += amt;
                    allowanceDetails.Add(ar["allowance_type"].ToString() + ":" + Math.Round(amt, 0).ToString("F0"));
                }
            }
            decimal totalAllowances = stdAllowances + adHocAllowanceAmount;

            // Standard deductions
            decimal nonStatDeductions = 0m;
            List<string> deductionDetails = new List<string>();
            DataTable dtDed = Q(c, tx, @"
                SELECT ad.dedall_name, IFNULL(das.custom_amount, ad.dedall_amount) AS amount, ad.computation_by
                FROM hrm_allowance_deductions ad
                JOIN hrm_ded_allowance_stafflist das ON das.ded_allID = ad.ID
                LEFT JOIN hrm_exemptions ex ON ex.empID = @eid AND ex.ded_allID = ad.ID
                WHERE das.empID = @eid AND ad.ded_allowance = 'DEDUCTION' AND ex.ID IS NULL", P("@eid", empID));
            foreach (DataRow d in dtDed.Rows)
            {
                decimal amt = SafeDecimal(d["amount"]);
                string compBy = d["computation_by"] == null || d["computation_by"] == DBNull.Value ? "" : d["computation_by"].ToString();
                decimal final = compBy.ToUpper() == "PERCENTAGE" ? basicPay * amt / 100 : amt;
                nonStatDeductions += final;
                deductionDetails.Add(d["dedall_name"].ToString() + ":" + Math.Round(final, 0).ToString("F0"));
            }

            // One-off deductions for the period (not held by another run)
            decimal adHocDeductionAmount = 0m;
            if (inclDeductions && !string.IsNullOrEmpty(payrollMonthName))
            {
                DataTable dtAdDed = Q(c, tx, @"
                    SELECT deduction_type, amount FROM hrm_deduction_records
                    WHERE empID=@eid AND to_deduct_month=@month AND to_deduct_year=@year AND status='PENDING'
                      AND (payroll_id IS NULL OR payroll_id=@pid)",
                    P("@eid", empID), P("@month", payrollMonthName), P("@year", payrollYear), P("@pid", payrollID));
                foreach (DataRow dr in dtAdDed.Rows)
                {
                    decimal amt = SafeDecimal(dr["amount"]);
                    adHocDeductionAmount += amt;
                    deductionDetails.Add(dr["deduction_type"].ToString() + ":" + Math.Round(amt, 0).ToString("F0"));
                }
            }

            // Statutory calculations (unchanged; see plan section 5 item 1)
            decimal grossPay = basicPay + totalAllowances;
            decimal nssf     = Math.Round(basicPay * cfg.NssfEmployeeRate / 100, 0);
            decimal kabaka   = cfg.ChargeKabaka   ? Math.Round(basicPay * cfg.KabakaRate   / 100, 0) : 0m;
            decimal localTax = cfg.ChargeLocalTax ? Math.Round(basicPay * cfg.LocalTaxRate / 100, 0) : 0m;
            decimal taxableIncome = grossPay - nssf;
            decimal paye = CalculatePAYE(taxableIncome, cfg);
            decimal totalDeductions = paye + nssf + kabaka + localTax + nonStatDeductions + adHocDeductionAmount;
            decimal netSalary = Math.Max(0, grossPay - totalDeductions);

            string allowDetailsStr = string.Join("|", allowanceDetails);
            string deductDetailsStr = string.Join("|", deductionDetails);
            totalNet += netSalary;

            X(c, tx, @"
                INSERT INTO hrm_payslips
                    (payroll_id, empID, payroll_year, payroll_month, payroll_month_name,
                     basic_pay, gross_salary, total_allowances, allowance_amount, allowance_details,
                     total_deductions, deduction_amount, deduction_details,
                     paye, nssf, kabaka_contribution, local_tax, net_salary, status, date_generated)
                VALUES
                    (@pid, @eid, @yr, @mo, @moname,
                     @basic, @gross, @totAllow, @allowAmt, @allowDet,
                     @totDed, @dedAmt, @dedDet,
                     @paye, @nssf, @kabaka, @localtax, @net, 'PENDING', NOW())",
                P("@pid", payrollID), P("@eid", empID), P("@yr", payrollYear), P("@mo", payrollMonth),
                P("@moname", payrollMonthName), P("@basic", basicPay), P("@gross", grossPay),
                P("@totAllow", totalAllowances), P("@allowAmt", adHocAllowanceAmount), P("@allowDet", allowDetailsStr),
                P("@totDed", totalDeductions), P("@dedAmt", adHocDeductionAmount), P("@dedDet", deductDetailsStr),
                P("@paye", paye), P("@nssf", nssf), P("@kabaka", kabaka), P("@localtax", localTax), P("@net", netSalary));

            try
            {
                X(c, tx, @"INSERT INTO hrm_payroll_details (payrollID, empID, basic_pay, paye, nssf, total_allowances, total_deductions, gross_pay, net_pay)
                           VALUES (@pid, @eid, @basic, @paye, @nssf, @allow, @ded, @gross, @net)",
                    P("@pid", payrollID), P("@eid", empID), P("@basic", basicPay), P("@paye", paye), P("@nssf", nssf),
                    P("@allow", totalAllowances), P("@ded", totalDeductions), P("@gross", grossPay), P("@net", netSalary));
            }
            catch { /* legacy table, not critical */ }

            // Hold the one-off items for this run. They are settled when the run is approved.
            if (!string.IsNullOrEmpty(payrollMonthName))
            {
                if (inclAllowances)
                    X(c, tx, @"UPDATE hrm_allowance_records SET payroll_id=@pid
                               WHERE empID=@eid AND to_add_month=@month AND to_add_year=@year AND status='PENDING'
                                 AND (payroll_id IS NULL OR payroll_id=@pid)",
                        P("@pid", payrollID), P("@eid", empID), P("@month", payrollMonthName), P("@year", payrollYear));
                if (inclDeductions)
                    X(c, tx, @"UPDATE hrm_deduction_records SET payroll_id=@pid
                               WHERE empID=@eid AND to_deduct_month=@month AND to_deduct_year=@year AND status='PENDING'
                                 AND (payroll_id IS NULL OR payroll_id=@pid)",
                        P("@pid", payrollID), P("@eid", empID), P("@month", payrollMonthName), P("@year", payrollYear));
            }
        }

        X(c, tx, "UPDATE hrm_payroll SET total_amount=@total WHERE ID=@pid", P("@total", totalNet), P("@pid", payrollID));
        return Str(pr["payroll_title"]);
    }

    /// <summary>
    /// Returns one-off items held by a run that is not approved to PENDING with no run.
    /// SETTLED items of an unapproved run come from the earlier rule that settled on generation.
    /// keepApproved leaves the items of employees whose payslip in the run is approved.
    /// </summary>
    private static void ReleaseItems(MySqlConnection c, MySqlTransaction tx, int payrollID, bool keepApproved)
    {
        string keep = keepApproved
            ? " AND empID NOT IN (SELECT ps.empID FROM hrm_payslips ps WHERE ps.payroll_id=@pid AND ps.status='APPROVED')"
            : "";
        X(c, tx, "UPDATE hrm_allowance_records SET status='PENDING', payroll_id=NULL, payment_date=NULL " +
                 "WHERE payroll_id=@pid AND status IN ('PENDING','SETTLED')" + keep, P("@pid", payrollID));
        X(c, tx, "UPDATE hrm_deduction_records SET status='PENDING', payroll_id=NULL, payment_date=NULL " +
                 "WHERE payroll_id=@pid AND status IN ('PENDING','SETTLED')" + keep, P("@pid", payrollID));
    }

    private static List<int> ResolveTargetEmployees(MySqlConnection c, MySqlTransaction tx, int payrollID)
    {
        List<int> result = new List<int>();
        DataTable dtPayroll = Q(c, tx, "SELECT target_type, target_ids FROM hrm_payroll WHERE ID=@id", P("@id", payrollID));
        if (dtPayroll.Rows.Count == 0) return result;

        string targetType = dtPayroll.Rows[0]["target_type"].ToString();
        string targetIds = Str(dtPayroll.Rows[0]["target_ids"]);
        const string baseSql = @"
            SELECT DISTINCT e.empID
            FROM hrm_employee e
            JOIN hrm_emp_contracts c ON c.empID = e.empID AND c.ID = (
                SELECT MAX(c2.ID) FROM hrm_emp_contracts c2 WHERE c2.empID = e.empID)
            LEFT JOIN hrm_payscales ps ON ps.ID = c.payscale
            WHERE c.contractStatus = 'VALID'
              AND c.contractEnd >= CURDATE()
              AND IFNULL(ps.basicpay, c.fixedamount) > 0";

        DataTable dtEmps;
        if (targetType == "ALL")
        {
            dtEmps = Q(c, tx, baseSql);
        }
        else if ((targetType == "DEPARTMENT" || targetType == "EMPLOYEE") && targetIds != "")
        {
            string[] arr = targetIds.Split(',');
            List<MySqlParameter> parms = new List<MySqlParameter>();
            List<string> names = new List<string>();
            for (int i = 0; i < arr.Length; i++)
            {
                names.Add("@t" + i);
                parms.Add(P("@t" + i, arr[i].Trim()));
            }
            string col = targetType == "DEPARTMENT" ? "c.departmentID" : "e.empID";
            dtEmps = Q(c, tx, baseSql + " AND " + col + " IN (" + string.Join(",", names.ToArray()) + ")", parms.ToArray());
        }
        else return result;

        foreach (DataRow r in dtEmps.Rows) result.Add(SafeInt(r["empID"]));
        return result;
    }

    // =================================================================
    //  Approve, cancel, delete, bulk
    // =================================================================

    private void ApproveRun(int payrollID)
    {
        string title;
        using (MySqlConnection c = new MySqlConnection(ConnStr))
        {
            c.Open();
            using (MySqlTransaction tx = c.BeginTransaction())
            {
                DataTable st = Q(c, tx, "SELECT payroll_title, payroll_status FROM hrm_payroll WHERE ID=@id FOR UPDATE", P("@id", payrollID));
                if (st.Rows.Count == 0) throw new UserError("Payroll run not found.");
                if (Str(st.Rows[0]["payroll_status"]) != "PENDING") throw new UserError("Only a pending payroll run can be approved.");
                title = Str(st.Rows[0]["payroll_title"]);

                DataTable cnt = Q(c, tx, "SELECT COUNT(*) AS n, COALESCE(SUM(status<>'APPROVED'),0) AS u FROM hrm_payslips WHERE payroll_id=@id", P("@id", payrollID));
                int total = SafeInt(cnt.Rows[0]["n"]), unapproved = SafeInt(cnt.Rows[0]["u"]);
                if (total == 0) throw new UserError("Generate the payslips before approving the run.");
                if (unapproved > 0) throw new UserError(unapproved == 1 ? "1 payslip is not approved yet." : unapproved + " payslips are not approved yet.");

                int stale = StaleCount(c, tx, payrollID);
                if (stale > 0)
                    throw new UserError((stale == 1 ? "1 payslip does" : stale + " payslips do") +
                        " not match the current allowances and deductions. Reject " + (stale == 1 ? "it" : "them") + " and regenerate the run.");

                X(c, tx, @"UPDATE hrm_payroll SET payroll_status='PROCESSED', lockStatus=1, date_processed=NOW(), processed_by=@user
                           WHERE ID=@id AND payroll_status='PENDING'", P("@user", CurrentUser()), P("@id", payrollID));
                X(c, tx, "UPDATE hrm_allowance_records SET status='SETTLED', payment_date=CURDATE() WHERE payroll_id=@id AND status='PENDING'", P("@id", payrollID));
                X(c, tx, "UPDATE hrm_deduction_records SET status='SETTLED', payment_date=CURDATE() WHERE payroll_id=@id AND status='PENDING'", P("@id", payrollID));
                tx.Commit();
            }
        }
        Log("Payroll run approved", title + " (run " + payrollID + ")");
    }

    /// <summary>Payslips whose one-off amounts differ from the items the run holds.</summary>
    private static int StaleCount(MySqlConnection c, MySqlTransaction tx, int payrollID)
    {
        DataTable dt = Q(c, tx, @"
            SELECT COUNT(*) AS n FROM hrm_payslips ps
            WHERE ps.payroll_id=@pid AND (
                ROUND(ps.allowance_amount,0) <> ROUND((SELECT IFNULL(SUM(a.amount),0) FROM hrm_allowance_records a
                    WHERE a.payroll_id=@pid AND a.empID=ps.empID AND a.status IN ('PENDING','SETTLED')),0)
             OR ROUND(ps.deduction_amount,0) <> ROUND((SELECT IFNULL(SUM(d.amount),0) FROM hrm_deduction_records d
                    WHERE d.payroll_id=@pid AND d.empID=ps.empID AND d.status IN ('PENDING','SETTLED')),0))",
            P("@pid", payrollID));
        return dt.Rows.Count > 0 ? SafeInt(dt.Rows[0]["n"]) : 0;
    }

    private void CancelRun(int payrollID)
    {
        string title;
        using (MySqlConnection c = new MySqlConnection(ConnStr))
        {
            c.Open();
            using (MySqlTransaction tx = c.BeginTransaction())
            {
                DataTable st = Q(c, tx, "SELECT payroll_title, payroll_status FROM hrm_payroll WHERE ID=@id FOR UPDATE", P("@id", payrollID));
                if (st.Rows.Count == 0) throw new UserError("Payroll run not found.");
                if (Str(st.Rows[0]["payroll_status"]) != "PENDING") throw new UserError("Only a pending payroll run can be cancelled.");
                title = Str(st.Rows[0]["payroll_title"]);
                X(c, tx, "UPDATE hrm_payroll SET payroll_status='CANCELLED' WHERE ID=@id AND payroll_status='PENDING'", P("@id", payrollID));
                ReleaseItems(c, tx, payrollID, false);
                tx.Commit();
            }
        }
        Log("Payroll run cancelled", title + " (run " + payrollID + ")");
    }

    private string DeleteRun(int payrollID)
    {
        string title;
        int remaining;
        using (MySqlConnection c = new MySqlConnection(ConnStr))
        {
            c.Open();
            using (MySqlTransaction tx = c.BeginTransaction())
            {
                DataTable st = Q(c, tx, "SELECT payroll_title, payroll_status FROM hrm_payroll WHERE ID=@id FOR UPDATE", P("@id", payrollID));
                if (st.Rows.Count == 0) throw new UserError("Payroll run not found.");
                if (Str(st.Rows[0]["payroll_status"]) != "PENDING") throw new UserError("Only a pending payroll run can be deleted.");
                title = Str(st.Rows[0]["payroll_title"]);

                ReleaseItems(c, tx, payrollID, true);
                X(c, tx, "DELETE FROM hrm_payslips WHERE payroll_id=@id AND status <> 'APPROVED'", P("@id", payrollID));
                X(c, tx, "DELETE FROM hrm_payroll_details WHERE payrollID=@id", P("@id", payrollID));
                try { X(c, tx, "DELETE FROM hrm_monthly_ded_allowance WHERE payrollID=@id", P("@id", payrollID)); } catch { }

                DataTable rem = Q(c, tx, "SELECT COUNT(*) AS n FROM hrm_payslips WHERE payroll_id=@id", P("@id", payrollID));
                remaining = SafeInt(rem.Rows[0]["n"]);
                if (remaining == 0) X(c, tx, "DELETE FROM hrm_payroll WHERE ID=@id", P("@id", payrollID));
                tx.Commit();
            }
        }
        Log(remaining == 0 ? "Payroll run deleted" : "Payroll run payslips deleted", title + " (run " + payrollID + ")");
        return remaining == 0
            ? "Payroll run deleted."
            : "Payslips not yet approved were deleted. The run stays because " + remaining + " approved payslip" + (remaining == 1 ? " remains." : "s remain.");
    }

    private string Bulk()
    {
        string op = (Request.Form["op"] ?? "").ToUpperInvariant();
        int done = 0, skipped = 0, failed = 0;
        foreach (string s in (Request.Form["ids"] ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int pid;
            if (!int.TryParse(s.Trim(), out pid) || pid <= 0) continue;
            try
            {
                if (op == "GENERATE") GenerateRun(pid);
                else if (op == "CANCEL") CancelRun(pid);
                else if (op == "DELETE") DeleteRun(pid);
                else throw new UserError("Unknown action.");
                done++;
            }
            catch (UserError) { skipped++; }
            catch (Exception ex) { failed++; Log("Error", "Bulk " + op + " run " + pid + ": " + ex.Message); }
        }
        string verb = op == "GENERATE" ? "generated" : op == "CANCEL" ? "cancelled" : "deleted";
        string msg = done + (done == 1 ? " run " : " runs ") + verb + ".";
        if (skipped > 0) msg += " " + skipped + " skipped (not pending).";
        if (failed > 0) msg += " " + failed + " could not be processed.";
        return Result(failed == 0, msg);
    }

    // =================================================================
    //  Screen: list
    // =================================================================

    private const string IcoMore = "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\"><circle cx=\"12\" cy=\"5\" r=\"1\"/><circle cx=\"12\" cy=\"12\" r=\"1\"/><circle cx=\"12\" cy=\"19\" r=\"1\"/></svg>";
    private const string IcoBack = "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><polyline points=\"15 18 9 12 15 6\"/></svg>";
    private const string IcoDownload = "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4\"/><polyline points=\"7 10 12 15 17 10\"/><line x1=\"12\" y1=\"15\" x2=\"12\" y2=\"3\"/></svg>";
    private const string IcoPrint = "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><polyline points=\"6 9 6 2 18 2 18 9\"/><path d=\"M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2\"/><rect x=\"6\" y=\"14\" width=\"12\" height=\"8\"/></svg>";

    private void RenderList()
    {
        string yearQs = Request.QueryString["year"];
        string year = yearQs == null ? DateTime.Today.Year.ToString(IC) : (yearQs == "all" ? "" : SafeInt(yearQs).ToString(IC));
        if (year == "0") year = "";
        string status = (Request.QueryString["status"] ?? "").ToUpperInvariant();
        if (status != "PENDING" && status != "PROCESSED" && status != "CANCELLED") status = "";

        // KPIs: runs counted once each (bug 3); pay totals exclude cancelled runs.
        string yWhere = "";
        if (year != "") { yWhere = " AND p.payroll_year = @yr"; }
        DataTable k1 = Q("SELECT COUNT(*) AS runs, COALESCE(SUM(p.payroll_status='PROCESSED'),0) AS approved, COALESCE(SUM(p.payroll_status='PENDING'),0) AS pending FROM hrm_payroll p WHERE 1=1" + yWhere,
            year != "" ? new[] { P("@yr", year) } : new MySqlParameter[0]);
        DataTable k2 = Q("SELECT COALESCE(SUM(ps.gross_salary),0) AS gross, COALESCE(SUM(ps.net_salary),0) AS net FROM hrm_payslips ps JOIN hrm_payroll p ON p.ID = ps.payroll_id WHERE p.payroll_status <> 'CANCELLED'" + yWhere,
            year != "" ? new[] { P("@yr", year) } : new MySqlParameter[0]);
        int runs = SafeInt(k1.Rows[0]["runs"]), approved = SafeInt(k1.Rows[0]["approved"]), pending = SafeInt(k1.Rows[0]["pending"]);
        string yearArg = year == "" ? "all" : year;

        StringBuilder h = new StringBuilder();
        h.Append("<div class=\"hr-kpis\">");
        h.Append("<a class=\"hr-kpi\" href=\"HRPayroll.aspx?year=").Append(yearArg).Append("\"><div class=\"hr-kpi__label\">Payroll runs</div><div class=\"hr-kpi__value\">")
         .Append(runs).Append("</div><div class=\"hr-kpi__sub\">").Append(approved).Append(" approved").Append("</div></a>");
        h.Append("<div class=\"hr-kpi\"><div class=\"hr-kpi__label\">Gross pay (UGX)</div><div class=\"hr-kpi__value\">").Append(Money(k2.Rows[0]["gross"]))
         .Append("</div><div class=\"hr-kpi__sub\">Excludes cancelled runs</div></div>");
        h.Append("<div class=\"hr-kpi\"><div class=\"hr-kpi__label\">Net pay (UGX)</div><div class=\"hr-kpi__value\">").Append(Money(k2.Rows[0]["net"]))
         .Append("</div><div class=\"hr-kpi__sub\">Excludes cancelled runs</div></div>");
        h.Append("<a class=\"hr-kpi").Append(pending > 0 ? " hr-kpi--alert" : "").Append("\" href=\"HRPayroll.aspx?year=").Append(yearArg).Append("&amp;status=PENDING\"><div class=\"hr-kpi__label\">Awaiting approval</div><div class=\"hr-kpi__value\">")
         .Append(pending).Append("</div><div class=\"hr-kpi__sub\">Pending runs</div></a>");
        h.Append("</div>");

        // Filters
        DataTable years = Q("SELECT DISTINCT payroll_year FROM hrm_payroll ORDER BY payroll_year DESC");
        List<string> yl = new List<string>();
        foreach (DataRow r in years.Rows) { string y = Str(r["payroll_year"]); if (y != "" && !yl.Contains(y)) yl.Add(y); }
        string cy = DateTime.Today.Year.ToString(IC);
        if (!yl.Contains(cy)) yl.Insert(0, cy);
        yl.Sort(delegate (string a, string b) { return string.CompareOrdinal(b, a); });

        h.Append("<div class=\"hr-filters\">");
        h.Append("<div class=\"hr-filter\"><label for=\"fYear\">Year</label><select id=\"fYear\" class=\"hr-select\" onchange=\"applyFilters()\">");
        h.Append("<option value=\"all\"").Append(year == "" ? " selected" : "").Append(">All years</option>");
        foreach (string y in yl) h.Append("<option value=\"").Append(y).Append("\"").Append(y == year ? " selected" : "").Append(">").Append(y).Append("</option>");
        h.Append("</select></div>");
        h.Append("<div class=\"hr-filter\"><label for=\"fStatus\">Status</label><select id=\"fStatus\" class=\"hr-select\" onchange=\"applyFilters()\">");
        string[,] sts = { { "", "All statuses" }, { "PENDING", "Pending" }, { "PROCESSED", "Approved" }, { "CANCELLED", "Cancelled" } };
        for (int i = 0; i < sts.GetLength(0); i++)
            h.Append("<option value=\"").Append(sts[i, 0]).Append("\"").Append(sts[i, 0] == status ? " selected" : "").Append(">").Append(sts[i, 1]).Append("</option>");
        h.Append("</select></div></div>");

        // Runs
        List<MySqlParameter> lp = new List<MySqlParameter>();
        string where = "WHERE 1=1";
        if (year != "") { where += " AND p.payroll_year = @yr"; lp.Add(P("@yr", year)); }
        if (status != "") { where += " AND p.payroll_status = @st"; lp.Add(P("@st", status)); }
        DataTable dt = Q(@"
            SELECT p.ID, p.payroll_title, p.payroll_month, p.payroll_year, p.payroll_date, p.payroll_status,
                   p.target_type, p.target_ids, IFNULL(pe.emp_name,'') AS prepared_name,
                   COUNT(ps.ID) AS staff, COALESCE(SUM(ps.gross_salary),0) AS gross, COALESCE(SUM(ps.net_salary),0) AS net
            FROM hrm_payroll p
            LEFT JOIN hrm_payslips ps ON ps.payroll_id = p.ID
            LEFT JOIN hrm_employee pe ON pe.empID = p.prepared_by
            " + where + @"
            GROUP BY p.ID
            ORDER BY p.payroll_year DESC, CAST(p.payroll_month AS UNSIGNED) DESC, p.ID DESC", lp.ToArray());

        h.Append("<div class=\"hr-card\"><div class=\"hr-card__head\"><div class=\"hr-card__title\">Payroll runs</div><div class=\"hr-card__meta\">")
         .Append(dt.Rows.Count).Append(dt.Rows.Count == 1 ? " run" : " runs").Append("</div></div>");
        h.Append("<div class=\"hr-bulk\" id=\"bulkBar\"><span id=\"bulkCount\">0 selected</span><span class=\"hr-spacer\"></span>")
         .Append("<button type=\"button\" class=\"hr-btn hr-btn--inverse hr-btn--sm\" onclick=\"bulkOp('GENERATE')\">Generate payslips</button>")
         .Append("<button type=\"button\" class=\"hr-btn hr-btn--inverse hr-btn--sm\" onclick=\"bulkOp('CANCEL')\">Cancel runs</button>")
         .Append("<button type=\"button\" class=\"hr-btn hr-btn--inverse hr-btn--sm\" onclick=\"bulkOp('DELETE')\">Delete runs</button>")
         .Append("<button type=\"button\" class=\"hr-btn hr-btn--inverse hr-btn--sm\" onclick=\"clearSelection()\">Clear</button></div>");

        if (dt.Rows.Count == 0)
        {
            h.Append("<div class=\"hr-empty\">No payroll runs for this filter.</div>");
        }
        else
        {
            h.Append("<div class=\"hr-table-wrap\"><table class=\"hr-table\"><thead><tr>")
             .Append("<th style=\"width:32px\"><input type=\"checkbox\" id=\"chkAll\" onclick=\"selectAll(this)\" aria-label=\"Select all\" /></th>")
             .Append("<th>Payroll run</th><th>Period</th><th>Coverage</th><th class=\"hr-num\">Staff</th><th class=\"hr-num\">Gross (UGX)</th><th class=\"hr-num\">Net (UGX)</th><th>Status</th><th>Prepared by</th><th>Created</th><th></th>")
             .Append("</tr></thead><tbody>");
            foreach (DataRow r in dt.Rows)
            {
                int id = SafeInt(r["ID"]);
                string st = Str(r["payroll_status"]);
                bool isPending = st == "PENDING";
                h.Append("<tr>");
                h.Append("<td>").Append(isPending ? "<input type=\"checkbox\" class=\"row-chk\" value=\"" + id + "\" onclick=\"updateBulk()\" aria-label=\"Select\" />" : "").Append("</td>");
                h.Append("<td><a href=\"HRPayroll.aspx?run=").Append(id).Append("\"><strong>").Append(E(r["payroll_title"])).Append("</strong></a></td>");
                h.Append("<td>").Append(E(Period(r["payroll_month"], r["payroll_year"]))).Append("</td>");
                h.Append("<td>").Append(E(Coverage(r["target_type"], r["target_ids"]))).Append("</td>");
                h.Append("<td class=\"hr-num\">").Append(SafeInt(r["staff"])).Append("</td>");
                h.Append("<td class=\"hr-num\">").Append(Money(r["gross"])).Append("</td>");
                h.Append("<td class=\"hr-num\">").Append(Money(r["net"])).Append("</td>");
                h.Append("<td>").Append(Badge(st)).Append("</td>");
                h.Append("<td>").Append(E(r["prepared_name"])).Append("</td>");
                h.Append("<td>").Append(ShortDate(r["payroll_date"])).Append("</td>");
                h.Append("<td class=\"hr-right\"><div class=\"run-menu\"><button type=\"button\" class=\"hr-btn hr-btn--secondary hr-btn--sm\" onclick=\"toggleMenu(this,event)\" aria-label=\"Actions\">")
                 .Append(IcoMore).Append("</button><div class=\"run-menu__list\">");
                h.Append("<a href=\"HRPayroll.aspx?run=").Append(id).Append("\">Open</a>");
                if (isPending) h.Append("<button type=\"button\" onclick=\"openGenerate(").Append(id).Append(")\">Generate payslips</button>");
                h.Append("<a href=\"HRPayslips.aspx?payroll_id=").Append(id).Append("\">Review payslips</a>");
                h.Append("<a href=\"HRPayroll.aspx?action=export&amp;type=register&amp;id=").Append(id).Append("\">Export register</a>");
                h.Append("<a href=\"HRPayroll.aspx?action=print&amp;type=register&amp;id=").Append(id).Append("\" target=\"_blank\">Print register</a>");
                if (isPending)
                {
                    h.Append("<button type=\"button\" onclick=\"runAction('cancel',").Append(id).Append(")\">Cancel run</button>");
                    h.Append("<button type=\"button\" class=\"is-danger\" onclick=\"runAction('delete',").Append(id).Append(")\">Delete run</button>");
                }
                h.Append("</div></div></td></tr>");
            }
            h.Append("</tbody></table></div>");
        }
        h.Append("</div>");
        litBody.Text = h.ToString();
    }

    // =================================================================
    //  Screen: run detail
    // =================================================================

    private DataRow LoadRun(int runId)
    {
        DataTable dt = Q(@"
            SELECT p.*, IFNULL(pe.emp_name,'') AS prepared_name, IFNULL(ce.emp_name,'') AS checked_name,
                   IFNULL((SELECT ae.emp_name FROM hrm_employee ae WHERE ae.usernames = p.processed_by AND p.processed_by <> '' LIMIT 1), IFNULL(p.processed_by,'')) AS approved_name
            FROM hrm_payroll p
            LEFT JOIN hrm_employee pe ON pe.empID = p.prepared_by
            LEFT JOIN hrm_employee ce ON ce.empID = p.checked_by
            WHERE p.ID = @id", P("@id", runId));
        return dt.Rows.Count > 0 ? dt.Rows[0] : null;
    }

    private DataTable LoadRegister(int runId)
    {
        return Q(@"
            SELECT ps.ID, ps.empID, e.EMP_CODE, e.emp_name, e.nssf_no, e.tin, e.bankAccount,
                   IFNULL(b.bank_name,'') AS bank_name, IFNULL(d.dept_name,'') AS dept_name, IFNULL(j.jobname,'') AS jobname,
                   ps.basic_pay, ps.total_allowances, ps.gross_salary, ps.paye, ps.nssf, ps.local_tax,
                   ps.kabaka_contribution, ps.total_deductions, ps.net_salary, ps.status
            FROM hrm_payslips ps
            JOIN hrm_employee e ON e.empID = ps.empID
            LEFT JOIN hrm_emp_contracts c ON c.ID = (SELECT MAX(c2.ID) FROM hrm_emp_contracts c2 WHERE c2.empID = ps.empID)
            LEFT JOIN hrm_departments d ON d.ID = c.departmentID
            LEFT JOIN hrm_jobs j ON j.ID = c.jobID
            LEFT JOIN banks b ON b.bank_id = e.bankID
            WHERE ps.payroll_id = @id
            ORDER BY e.emp_name", P("@id", runId));
    }

    private static decimal OtherDeductions(DataRow r)
    {
        decimal v = SafeDecimal(r["total_deductions"]) - SafeDecimal(r["paye"]) - SafeDecimal(r["nssf"])
                  - SafeDecimal(r["local_tax"]) - SafeDecimal(r["kabaka_contribution"]);
        return v < 0 ? 0 : v;
    }

    private static string Account(object v)
    {
        string s = Str(v);
        return s == "-" || s == "0" ? "" : s;
    }

    private void RenderDetail(int runId)
    {
        DataRow run = LoadRun(runId);
        StringBuilder h = new StringBuilder();
        h.Append("<div class=\"hr-row\" style=\"margin-bottom:10px\"><a class=\"hr-btn hr-btn--link\" href=\"HRPayroll.aspx\">").Append(IcoBack).Append("All payroll runs</a></div>");
        if (run == null)
        {
            h.Append("<div class=\"hr-notice hr-notice--bad\">Payroll run not found.</div>");
            litBody.Text = h.ToString();
            return;
        }

        string st = Str(run["payroll_status"]);
        bool pending = st == "PENDING";
        DataTable reg = LoadRegister(runId);
        int count = reg.Rows.Count, unapproved = 0;
        decimal tBasic = 0, tAllow = 0, tGross = 0, tPaye = 0, tNssf = 0, tLst = 0, tKab = 0, tOther = 0, tDed = 0, tNet = 0;
        foreach (DataRow r in reg.Rows)
        {
            if (Str(r["status"]) != "APPROVED") unapproved++;
            tBasic += SafeDecimal(r["basic_pay"]); tAllow += SafeDecimal(r["total_allowances"]); tGross += SafeDecimal(r["gross_salary"]);
            tPaye += SafeDecimal(r["paye"]); tNssf += SafeDecimal(r["nssf"]); tLst += SafeDecimal(r["local_tax"]);
            tKab += SafeDecimal(r["kabaka_contribution"]); tOther += OtherDeductions(r); tDed += SafeDecimal(r["total_deductions"]);
            tNet += SafeDecimal(r["net_salary"]);
        }

        int stale = 0, unheld = 0;
        if (pending && count > 0)
        {
            using (MySqlConnection c = new MySqlConnection(ConnStr)) { c.Open(); stale = StaleCount(c, null, runId); }
            int m = SafeInt(run["payroll_month"]);
            if (m >= 1 && m <= 12)
            {
                bool ia = Str(run["should_include_allowances"]).ToUpperInvariant() == "YES";
                bool id = Str(run["should_include_deductions"]).ToUpperInvariant() == "YES";
                string sql = "SELECT " +
                    (ia ? "(SELECT COUNT(*) FROM hrm_allowance_records a JOIN hrm_payslips ps ON ps.empID=a.empID AND ps.payroll_id=@id AND ps.status<>'APPROVED' WHERE a.status='PENDING' AND a.payroll_id IS NULL AND a.to_add_month=@mn AND a.to_add_year=@yr)" : "0") + " + " +
                    (id ? "(SELECT COUNT(*) FROM hrm_deduction_records d JOIN hrm_payslips ps ON ps.empID=d.empID AND ps.payroll_id=@id AND ps.status<>'APPROVED' WHERE d.status='PENDING' AND d.payroll_id IS NULL AND d.to_deduct_month=@mn AND d.to_deduct_year=@yr)" : "0") + " AS n";
                DataTable u = Q(sql, P("@id", runId), P("@mn", MONTH_NAMES[m]), P("@yr", SafeInt(run["payroll_year"])));
                unheld = SafeInt(u.Rows[0]["n"]);
            }
        }

        // Header card
        h.Append("<div class=\"hr-card\"><div class=\"hr-card__head\"><div class=\"hr-row\"><div class=\"hr-card__title\">").Append(E(run["payroll_title"]))
         .Append("</div>").Append(Badge(st)).Append("</div><div class=\"hr-row\">");
        if (pending)
        {
            h.Append("<button type=\"button\" class=\"hr-btn hr-btn--primary hr-btn--sm\" onclick=\"openGenerate(").Append(runId).Append(")\">")
             .Append(count > 0 ? "Regenerate payslips" : "Generate payslips").Append("</button>");
            if (count > 0)
                h.Append("<a class=\"hr-btn hr-btn--secondary hr-btn--sm\" href=\"HRPayslips.aspx?payroll_id=").Append(runId).Append("\">Review payslips</a>");
            bool canApprove = count > 0 && unapproved == 0 && stale == 0;
            h.Append("<button type=\"button\" class=\"hr-btn hr-btn--success hr-btn--sm\"").Append(canApprove ? "" : " disabled")
             .Append(" onclick=\"runAction('approve',").Append(runId).Append(")\">Approve and lock</button>");
            h.Append("<button type=\"button\" class=\"hr-btn hr-btn--secondary hr-btn--sm\" onclick=\"runAction('cancel',").Append(runId).Append(")\">Cancel run</button>");
            h.Append("<button type=\"button\" class=\"hr-btn hr-btn--danger hr-btn--sm\" onclick=\"runAction('delete',").Append(runId).Append(")\">Delete run</button>");
        }
        else if (count > 0)
        {
            h.Append("<a class=\"hr-btn hr-btn--secondary hr-btn--sm\" href=\"HRPayslips.aspx?payroll_id=").Append(runId).Append("\">View payslips</a>");
        }
        h.Append("</div></div><div class=\"hr-card__body\">");

        // Workflow position
        string[] stages = { "Created", "Payslips generated", "Payslips approved", "Approved and locked" };
        int reached = 1 + (count > 0 ? 1 : 0) + (count > 0 && unapproved == 0 ? 1 : 0) + (st == "PROCESSED" ? 1 : 0);
        h.Append("<ol class=\"hr-stages\" style=\"margin-bottom:16px\">");
        for (int i = 0; i < stages.Length; i++)
        {
            string cls = i < reached ? "is-done" : (i == reached ? (st == "CANCELLED" ? "is-stop" : "is-now") : "");
            h.Append("<li class=\"").Append(cls).Append("\"><span></span>").Append(i == reached && st == "CANCELLED" ? "Cancelled" : stages[i]).Append("</li>");
        }
        h.Append("</ol>");

        h.Append("<dl class=\"hr-dl\">");
        Dl(h, "Period", Period(run["payroll_month"], run["payroll_year"]));
        Dl(h, "Coverage", Coverage(run["target_type"], run["target_ids"]));
        Dl(h, "Created", ShortDate(run["payroll_date"]));
        Dl(h, "Prepared by", Str(run["prepared_name"]));
        Dl(h, "One-off allowances", Str(run["should_include_allowances"]).ToUpperInvariant() == "YES" ? "Included" : "Not included");
        Dl(h, "One-off deductions", Str(run["should_include_deductions"]).ToUpperInvariant() == "YES" ? "Included" : "Not included");
        if (st == "PROCESSED")
        {
            Dl(h, "Approved by", Str(run["approved_name"]));
            Dl(h, "Approved on", ShortDate(run["date_processed"]));
        }
        string notes = Str(run["payroll_comments"]);
        if (notes != "" && notes != "-") Dl(h, "Notes", notes);
        h.Append("</dl></div></div>");

        // Notices
        if (pending && count == 0)
            h.Append("<div class=\"hr-notice\">No payslips yet. Generate the payslips to build the register.</div>");
        if (pending && count > 0 && unapproved > 0)
            h.Append("<div class=\"hr-notice hr-notice--warn\">").Append(unapproved).Append(unapproved == 1 ? " payslip is" : " payslips are")
             .Append(" not approved yet. <a href=\"HRPayslips.aspx?payroll_id=").Append(runId).Append("&amp;status=PENDING\">Review payslips</a></div>");
        if (stale > 0)
            h.Append("<div class=\"hr-notice hr-notice--bad\">").Append(stale).Append(stale == 1 ? " payslip does" : " payslips do")
             .Append(" not match the current allowances and deductions. Regenerate the run; reject approved payslips first.</div>");
        if (unheld > 0)
            h.Append("<div class=\"hr-notice hr-notice--warn\">").Append(unheld).Append(unheld == 1 ? " one-off item" : " one-off items")
             .Append(" for this period was added after the payslips were generated. Regenerate the run to include ").Append(unheld == 1 ? "it." : "them.").Append("</div>");
        if (st == "PROCESSED")
            h.Append("<div class=\"hr-notice hr-notice--ok\">This payroll run is approved and locked.</div>");

        // Summary strip
        h.Append("<div class=\"hr-kpis\">");
        Kpi(h, "Staff", count.ToString(IC), "Payslips in this run");
        Kpi(h, "Gross pay (UGX)", tGross.ToString("#,##0", IC), "Basic " + tBasic.ToString("#,##0", IC));
        Kpi(h, "Total deductions (UGX)", tDed.ToString("#,##0", IC), "PAYE " + tPaye.ToString("#,##0", IC));
        Kpi(h, "Net pay (UGX)", tNet.ToString("#,##0", IC), "Paid to staff");
        h.Append("</div>");

        // Exports and prints
        h.Append("<div class=\"hr-card\"><div class=\"hr-card__head\"><div class=\"hr-card__title\">Register</div><div class=\"hr-row\">");
        if (count > 0)
        {
            h.Append("<a class=\"hr-btn hr-btn--secondary hr-btn--sm\" href=\"HRPayroll.aspx?action=export&amp;type=register&amp;id=").Append(runId).Append("\">").Append(IcoDownload).Append("Register (xlsx)</a>");
            h.Append("<a class=\"hr-btn hr-btn--secondary hr-btn--sm\" href=\"HRPayroll.aspx?action=export&amp;type=register-csv&amp;id=").Append(runId).Append("\">").Append(IcoDownload).Append("Register (csv)</a>");
            h.Append("<a class=\"hr-btn hr-btn--secondary hr-btn--sm\" href=\"HRPayroll.aspx?action=export&amp;type=statutory&amp;id=").Append(runId).Append("\">").Append(IcoDownload).Append("Statutory schedules</a>");
            h.Append("<a class=\"hr-btn hr-btn--secondary hr-btn--sm\" target=\"_blank\" href=\"HRPayroll.aspx?action=print&amp;type=register&amp;id=").Append(runId).Append("\">").Append(IcoPrint).Append("Print register</a>");
            h.Append("<a class=\"hr-btn hr-btn--secondary hr-btn--sm\" target=\"_blank\" href=\"HRPayslips.aspx?action=print&amp;run=").Append(runId).Append("\">").Append(IcoPrint).Append("Print payslips</a>");
        }
        h.Append("</div></div>");

        if (count == 0)
        {
            h.Append("<div class=\"hr-empty\">No payslips in this run.</div>");
        }
        else
        {
            h.Append("<div class=\"hr-table-wrap\"><table class=\"hr-table\"><thead><tr>")
             .Append("<th>Staff no</th><th>Name</th><th>Department</th><th class=\"hr-num\">Basic</th><th class=\"hr-num\">Allowances</th><th class=\"hr-num\">Gross</th>")
             .Append("<th class=\"hr-num\">PAYE</th><th class=\"hr-num\">NSSF</th><th class=\"hr-num\">Local service tax</th><th class=\"hr-num\">Kabaka</th><th class=\"hr-num\">Other</th>")
             .Append("<th class=\"hr-num\">Total deductions</th><th class=\"hr-num\">Net pay</th><th>Payslip</th></tr></thead><tbody>");
            foreach (DataRow r in reg.Rows)
            {
                h.Append("<tr><td>").Append(E(r["EMP_CODE"])).Append("</td><td>").Append(E(r["emp_name"])).Append("</td><td>").Append(E(r["dept_name"])).Append("</td>");
                h.Append("<td class=\"hr-num\">").Append(Money(r["basic_pay"])).Append("</td><td class=\"hr-num\">").Append(Money(r["total_allowances"])).Append("</td>");
                h.Append("<td class=\"hr-num\">").Append(Money(r["gross_salary"])).Append("</td><td class=\"hr-num\">").Append(Money(r["paye"])).Append("</td>");
                h.Append("<td class=\"hr-num\">").Append(Money(r["nssf"])).Append("</td><td class=\"hr-num\">").Append(Money(r["local_tax"])).Append("</td>");
                h.Append("<td class=\"hr-num\">").Append(Money(r["kabaka_contribution"])).Append("</td><td class=\"hr-num\">").Append(OtherDeductions(r).ToString("#,##0", IC)).Append("</td>");
                h.Append("<td class=\"hr-num\">").Append(Money(r["total_deductions"])).Append("</td><td class=\"hr-num\"><strong>").Append(Money(r["net_salary"])).Append("</strong></td>");
                h.Append("<td>").Append(Badge(Str(r["status"]))).Append("</td></tr>");
            }
            h.Append("</tbody><tfoot><tr><td colspan=\"3\">Total (").Append(count).Append(" staff)</td>");
            foreach (decimal v in new[] { tBasic, tAllow, tGross, tPaye, tNssf, tLst, tKab, tOther, tDed, tNet })
                h.Append("<td class=\"hr-num\">").Append(v.ToString("#,##0", IC)).Append("</td>");
            h.Append("<td></td></tr></tfoot></table></div>");
            h.Append("<div class=\"hr-card__foot\"><span>Amounts in UGX. To change a payslip, adjust the allowance, deduction or contract and regenerate the run.</span></div>");
        }
        h.Append("</div>");
        litBody.Text = h.ToString();
    }

    private static void Dl(StringBuilder h, string label, string value)
    {
        h.Append("<div><dt>").Append(E(label)).Append("</dt><dd>").Append(value == "" ? "<span class=\"hr-muted\">Not recorded</span>" : E(value)).Append("</dd></div>");
    }

    private static void Kpi(StringBuilder h, string label, string value, string sub)
    {
        h.Append("<div class=\"hr-kpi\"><div class=\"hr-kpi__label\">").Append(E(label)).Append("</div><div class=\"hr-kpi__value\">").Append(E(value))
         .Append("</div><div class=\"hr-kpi__sub\">").Append(E(sub)).Append("</div></div>");
    }

    // Lists for the create dialog (rendered on every load; no ViewState needed)
    private void RenderCreateLists()
    {
        StringBuilder y = new StringBuilder();
        int cy = DateTime.Today.Year;
        for (int yr = cy + 1; yr >= cy - 5; yr--)
            y.Append("<option value=\"").Append(yr).Append("\"").Append(yr == cy ? " selected" : "").Append(">").Append(yr).Append("</option>");
        litYearOptions.Text = y.ToString();

        StringBuilder m = new StringBuilder();
        for (int i = 1; i <= 12; i++)
            m.Append("<option value=\"").Append(i).Append("\"").Append(i == DateTime.Today.Month ? " selected" : "").Append(">").Append(MonthTitle(i)).Append("</option>");
        litMonthOptions.Text = m.ToString();

        StringBuilder d = new StringBuilder();
        foreach (DataRow r in Q("SELECT ID, dept_name FROM hrm_departments ORDER BY dept_name").Rows)
            d.Append("<label class=\"pick\"><input type=\"checkbox\" name=\"pickDept\" value=\"").Append(SafeInt(r["ID"])).Append("\" /> ").Append(E(r["dept_name"])).Append("</label>");
        litDeptChecks.Text = d.ToString();

        StringBuilder e = new StringBuilder();
        foreach (DataRow r in Q(@"SELECT DISTINCT e.empID, e.emp_name, IFNULL(e.EMP_CODE,'') AS code
                                  FROM hrm_employee e JOIN hrm_emp_contracts c ON c.empID = e.empID
                                  WHERE c.contractStatus = 'VALID' AND c.contractEnd >= CURDATE()
                                  ORDER BY e.emp_name").Rows)
        {
            string code = Str(r["code"]);
            e.Append("<label class=\"pick\" data-q=\"").Append(A((Str(r["emp_name"]) + " " + code).ToLowerInvariant())).Append("\"><input type=\"checkbox\" name=\"pickEmp\" value=\"")
             .Append(SafeInt(r["empID"])).Append("\" /> ").Append(E(r["emp_name"]));
            if (code != "" && code != "-") e.Append(" <span class=\"hr-muted\">").Append(E(code)).Append("</span>");
            e.Append("</label>");
        }
        litEmpChecks.Text = e.ToString();
    }

    // =================================================================
    //  Exports and print
    // =================================================================

    private void HandleFile(string action)
    {
        int runId = SafeInt(Request.QueryString["id"]);
        string type = Request.QueryString["type"] ?? "";
        try
        {
            DataRow run = LoadRun(runId);
            if (run == null) { PlainError("Payroll run not found."); return; }
            DataTable reg = LoadRegister(runId);
            if (action == "print") { PrintRegister(run, reg); return; }
            if (type == "statutory")
            {
                HrExport.SendXlsx(Response, StatutoryReport(run, reg));
                Log("Payroll export", "statutory: " + Str(run["payroll_title"]));
                return;
            }
            HrExport.Report rep = RegisterReport(run, reg);
            if (type == "register-csv") HrExport.SendCsv(Response, rep, 0);
            else HrExport.SendXlsx(Response, rep);
            Log("Payroll export", type + ": " + Str(run["payroll_title"]));
        }
        catch (Exception ex)
        {
            Log("Error", "Export " + type + " run " + runId + ": " + ex.Message);
            PlainError("The file could not be produced. Please try again.");
        }
    }

    private void PlainError(string msg)
    {
        Response.Clear();
        Response.ContentType = "text/plain";
        Response.Write(msg);
    }

    private HrExport.Report NewReport(DataRow run, string title, string slug)
    {
        HrExport.Report r = new HrExport.Report(title, slug);
        r.PreparedBy = CurrentUser();
        r.AddScope("Payroll run", Str(run["payroll_title"]));
        r.AddScope("Period", Period(run["payroll_month"], run["payroll_year"]));
        r.AddScope("Status", StatusWord(Str(run["payroll_status"])));
        r.AddScope("Amounts", "UGX");
        return r;
    }

    private HrExport.Report RegisterReport(DataRow run, DataTable reg)
    {
        HrExport.Report rep = NewReport(run, "Payroll register", "payroll-register");
        HrExport.Kind M = HrExport.Kind.Money;

        HrExport.Sheet s = rep.NewSheet("Register");
        s.Add("Staff No").Add("Name").Add("Department").Add("Position")
         .Add("Basic", M, true).Add("Allowances", M, true).Add("Gross", M, true).Add("PAYE", M, true).Add("NSSF", M, true)
         .Add("Local service tax", M, true).Add("Kabaka", M, true).Add("Other deductions", M, true).Add("Total deductions", M, true)
         .Add("Net pay", M, true).Add("Bank").Add("Account no");
        SortedDictionary<string, decimal[]> byDept = new SortedDictionary<string, decimal[]>(StringComparer.OrdinalIgnoreCase);
        List<object[]> bank = new List<object[]>();
        foreach (DataRow r in reg.Rows)
        {
            s.Row(Str(r["EMP_CODE"]), Str(r["emp_name"]), Str(r["dept_name"]), Str(r["jobname"]),
                SafeDecimal(r["basic_pay"]), SafeDecimal(r["total_allowances"]), SafeDecimal(r["gross_salary"]), SafeDecimal(r["paye"]),
                SafeDecimal(r["nssf"]), SafeDecimal(r["local_tax"]), SafeDecimal(r["kabaka_contribution"]), OtherDeductions(r),
                SafeDecimal(r["total_deductions"]), SafeDecimal(r["net_salary"]), Str(r["bank_name"]), Account(r["bankAccount"]));
            string dept = Str(r["dept_name"]);
            if (dept == "") dept = "No department recorded";
            decimal[] a;
            if (!byDept.TryGetValue(dept, out a)) { a = new decimal[4]; byDept[dept] = a; }
            a[0] += 1; a[1] += SafeDecimal(r["gross_salary"]); a[2] += SafeDecimal(r["total_deductions"]); a[3] += SafeDecimal(r["net_salary"]);
            bank.Add(new object[] { Str(r["emp_name"]), Str(r["bank_name"]), Account(r["bankAccount"]), SafeDecimal(r["net_salary"]) });
        }

        HrExport.Sheet sd = rep.NewSheet("Summary by department");
        sd.Add("Department").Add("Staff", HrExport.Kind.Number, true).Add("Gross", M, true).Add("Total deductions", M, true).Add("Net pay", M, true);
        foreach (KeyValuePair<string, decimal[]> kv in byDept) sd.Row(kv.Key, kv.Value[0], kv.Value[1], kv.Value[2], kv.Value[3]);

        HrExport.Sheet sb = rep.NewSheet("Bank schedule");
        sb.Add("Name").Add("Bank").Add("Account no").Add("Net pay", M, true);
        bank.Sort(delegate (object[] x, object[] y)
        {
            string bx = (string)x[1], by = (string)y[1];
            if (bx == "" && by != "") return 1;
            if (by == "" && bx != "") return -1;
            int c = string.Compare(bx, by, StringComparison.OrdinalIgnoreCase);
            return c != 0 ? c : string.Compare((string)x[0], (string)y[0], StringComparison.OrdinalIgnoreCase);
        });
        foreach (object[] b in bank) sb.Row(b);
        return rep;
    }

    private HrExport.Report StatutoryReport(DataRow run, DataTable reg)
    {
        HrExport.Report rep = NewReport(run, "Statutory schedules", "statutory-schedules");
        HrExport.Kind M = HrExport.Kind.Money;
        HrExport.Sheet paye = rep.NewSheet("PAYE");
        paye.Title = "PAYE schedule";
        paye.Add("Staff No").Add("Name").Add("TIN").Add("Gross pay", M, true).Add("PAYE", M, true);
        HrExport.Sheet nssf = rep.NewSheet("NSSF");
        nssf.Title = "NSSF schedule";
        nssf.Add("Staff No").Add("Name").Add("NSSF no").Add("Basic pay", M, true).Add("Employee NSSF", M, true);
        HrExport.Sheet lst = rep.NewSheet("Local service tax");
        lst.Title = "Local service tax schedule";
        lst.Add("Staff No").Add("Name").Add("Basic pay", M, true).Add("Local service tax", M, true);
        HrExport.Sheet kab = rep.NewSheet("Kabaka");
        kab.Title = "Kabaka contribution schedule";
        kab.Add("Staff No").Add("Name").Add("Basic pay", M, true).Add("Kabaka contribution", M, true);
        foreach (DataRow r in reg.Rows)
        {
            string code = Str(r["EMP_CODE"]), name = Str(r["emp_name"]);
            paye.Row(code, name, Account(r["tin"]), SafeDecimal(r["gross_salary"]), SafeDecimal(r["paye"]));
            nssf.Row(code, name, Account(r["nssf_no"]), SafeDecimal(r["basic_pay"]), SafeDecimal(r["nssf"]));
            if (SafeDecimal(r["local_tax"]) != 0) lst.Row(code, name, SafeDecimal(r["basic_pay"]), SafeDecimal(r["local_tax"]));
            if (SafeDecimal(r["kabaka_contribution"]) != 0) kab.Row(code, name, SafeDecimal(r["basic_pay"]), SafeDecimal(r["kabaka_contribution"]));
        }
        return rep;
    }

    private void PrintRegister(DataRow run, DataTable reg)
    {
        string st = Str(run["payroll_status"]);
        StringBuilder b = new StringBuilder();
        b.Append(HrDocument.Meta(
            "Payroll run", Str(run["payroll_title"]),
            "Period", Period(run["payroll_month"], run["payroll_year"]),
            "Coverage", Coverage(run["target_type"], run["target_ids"]),
            "Status", StatusWord(st),
            "Staff", reg.Rows.Count.ToString(IC),
            "Amounts", "UGX"));

        b.Append(HrDocument.Heading("Register"));
        string[] heads = { "Staff no", "Name", "Department", "Basic", "Allowances", "Gross", "PAYE", "NSSF", "LST", "Kabaka", "Other", "Total deductions", "Net pay" };
        bool[] num = { false, false, false, true, true, true, true, true, true, true, true, true, true };
        decimal[] t = new decimal[10];
        List<string[]> rows = new List<string[]>();
        SortedDictionary<string, decimal[]> byDept = new SortedDictionary<string, decimal[]>(StringComparer.OrdinalIgnoreCase);
        foreach (DataRow r in reg.Rows)
        {
            decimal[] v = { SafeDecimal(r["basic_pay"]), SafeDecimal(r["total_allowances"]), SafeDecimal(r["gross_salary"]), SafeDecimal(r["paye"]),
                            SafeDecimal(r["nssf"]), SafeDecimal(r["local_tax"]), SafeDecimal(r["kabaka_contribution"]), OtherDeductions(r),
                            SafeDecimal(r["total_deductions"]), SafeDecimal(r["net_salary"]) };
            string[] cells = new string[13];
            cells[0] = HrDocument.E(Str(r["EMP_CODE"])); cells[1] = HrDocument.E(Str(r["emp_name"])); cells[2] = HrDocument.E(Str(r["dept_name"]));
            for (int i = 0; i < 10; i++) { cells[3 + i] = HrDocument.Money(v[i]); t[i] += v[i]; }
            rows.Add(cells);
            string dept = Str(r["dept_name"]);
            if (dept == "") dept = "No department recorded";
            decimal[] a;
            if (!byDept.TryGetValue(dept, out a)) { a = new decimal[4]; byDept[dept] = a; }
            a[0] += 1; a[1] += v[2]; a[2] += v[8]; a[3] += v[9];
        }
        string[] tot = new string[13];
        tot[0] = "Total"; tot[1] = reg.Rows.Count + " staff"; tot[2] = "";
        for (int i = 0; i < 10; i++) tot[3 + i] = HrDocument.Money(t[i]);
        b.Append(HrDocument.Table(heads, rows, num, tot));

        b.Append(HrDocument.Heading("Summary by department"));
        List<string[]> drows = new List<string[]>();
        decimal[] dt = new decimal[4];
        foreach (KeyValuePair<string, decimal[]> kv in byDept)
        {
            drows.Add(new[] { HrDocument.E(kv.Key), kv.Value[0].ToString("0", IC), HrDocument.Money(kv.Value[1]), HrDocument.Money(kv.Value[2]), HrDocument.Money(kv.Value[3]) });
            for (int i = 0; i < 4; i++) dt[i] += kv.Value[i];
        }
        b.Append(HrDocument.Table(new[] { "Department", "Staff", "Gross", "Total deductions", "Net pay" }, drows,
            new[] { false, true, true, true, true },
            new[] { "Total", dt[0].ToString("0", IC), HrDocument.Money(dt[1]), HrDocument.Money(dt[2]), HrDocument.Money(dt[3]) }));

        b.Append(HrDocument.Signatures(
            "Prepared by", Str(run["prepared_name"]), "",
            "Checked by", "", "",
            "Approved by", st == "PROCESSED" ? Str(run["approved_name"]) : "", ""));

        HrDocument.Options o = new HrDocument.Options();
        o.Landscape = true;
        o.Reference = "Period: " + Period(run["payroll_month"], run["payroll_year"]);
        o.BackUrl = "HRPayroll.aspx?run=" + SafeInt(run["ID"]);
        Response.Clear();
        Response.ContentType = "text/html";
        Response.Write(HrDocument.Page("Payroll register", b.ToString(), o));
        Log("Payroll print", "Register: " + Str(run["payroll_title"]));
    }
}
