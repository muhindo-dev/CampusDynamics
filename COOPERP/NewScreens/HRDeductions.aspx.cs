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
/// Deductions by month (hrm_deduction_records). Items are PENDING until a payroll run holds them
/// (payroll_id set, still PENDING), SETTLED when that run is approved and locked, or CANCELLED.
/// Settled items cannot be changed. Changing an item that a pending run holds is allowed; the
/// run then shows that its payslips need regenerating and cannot be approved until they are.
/// Filters are query-string values, so they survive every reload.
/// </summary>
public partial class COOPERP_NewScreens_HRDeductions : System.Web.UI.Page
{
    private const int PageSize = 50;
    private const string PageUrl = "HRDeductions.aspx";
    private static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    private static readonly string[] MONTHS = {
        "JANUARY","FEBRUARY","MARCH","APRIL","MAY","JUNE",
        "JULY","AUGUST","SEPTEMBER","OCTOBER","NOVEMBER","DECEMBER"
    };
    private static readonly string[] DEFAULT_TYPES = { "Salary Advance", "Loan Repayment (Staff SACCO)", "Loan Repayment (Bank)", "Housing Loan Repayment", "Court Order / Garnishment", "Equipment / Asset Recovery", "Overpayment Recovery", "Insurance Premium", "Welfare Contribution", "Other" };

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
        DataTable dt = new DataTable();
        using (MySqlConnection c = new MySqlConnection(ConnStr))
        {
            c.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, c))
            {
                if (parms != null) foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) da.Fill(dt);
            }
        }
        return dt;
    }

    private int X(string sql, params MySqlParameter[] parms)
    {
        using (MySqlConnection c = new MySqlConnection(ConnStr))
        {
            c.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, c))
            {
                if (parms != null) foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                return cmd.ExecuteNonQuery();
            }
        }
    }

    private static decimal Dec(object v)
    {
        if (v == null || v == DBNull.Value) return 0m;
        decimal d;
        return decimal.TryParse(v.ToString(), NumberStyles.Any, IC, out d) ? d : 0m;
    }

    private static int Int(object v)
    {
        if (v == null || v == DBNull.Value) return 0;
        int i;
        return int.TryParse(v.ToString(), out i) ? i : 0;
    }

    private static string Str(object v) { return v == null || v == DBNull.Value ? "" : v.ToString().Trim(); }
    private static string E(object s) { return HttpUtility.HtmlEncode(HrExport.Clean(Str(s))); }
    private static string Money(object v) { return Dec(v).ToString("#,##0", IC); }
    private static string Trunc(string s, int n) { s = s ?? ""; return s.Length <= n ? s : s.Substring(0, n); }
    private static string MonthTitle(string m) { return IC.TextInfo.ToTitleCase((m ?? "").ToLowerInvariant()); }

    private static string ShortDate(object v)
    {
        DateTime d;
        return v != null && v != DBNull.Value && DateTime.TryParse(v.ToString(), out d) && d.Year > 1900 ? d.ToString("d MMM yyyy", IC) : "";
    }

    private string CurrentUser() { return HrAccess.Username(); }

    private void Log(string what, string detail)
    {
        try
        {
            X("INSERT INTO acad_activity_log (user_id, page_function, par, comments, access_date) VALUES (@u, 'HR Deductions', @p, @c, NOW())",
                P("@u", Trunc(CurrentUser(), 100)), P("@p", Trunc(detail, 300)), P("@c", Trunc(what, 200)));
        }
        catch { }
    }

    /// <summary>Types offered in the dialogs: optional deduction items from the payroll settings, then the usual one-off types.</summary>
    private List<string> Types()
    {
        List<string> list = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (DataRow r in Q("SELECT DISTINCT TRIM(dedall_name) AS n FROM hrm_allowance_deductions WHERE UPPER(ded_allowance) = 'DEDUCTION' AND UPPER(IFNULL(dedall_type,'')) NOT IN ('STATUTORY','MANDATORY') ORDER BY n").Rows)
            {
                string n = IC.TextInfo.ToTitleCase(Str(r["n"]).ToLowerInvariant());
                if (n != "" && seen.Add(n)) list.Add(n);
            }
        }
        catch { }
        foreach (string t in DEFAULT_TYPES) if (t != "Other" && seen.Add(t)) list.Add(t);
        list.Sort(StringComparer.OrdinalIgnoreCase);
        list.Add("Other");
        return list;
    }

    private static string StatusOf(DataRow r)
    {
        string s = Str(r["status"]);
        if (s == "PENDING" && Int(r["payroll_id"]) > 0) return "INRUN";
        return s;
    }

    private static string StatusWord(string s)
    {
        switch (s)
        {
            case "INRUN": return "In payroll run";
            case "SETTLED": return "Settled";
            case "CANCELLED": return "Cancelled";
            default: return "Pending";
        }
    }

    private static string Badge(string s)
    {
        string kind = s == "SETTLED" ? "ok" : s == "INRUN" ? "info" : s == "CANCELLED" ? "neutral" : "warn";
        return "<span class=\"hr-badge hr-badge--" + kind + "\">" + StatusWord(s) + "</span>";
    }

    // =================================================================
    //  Page load and dispatch
    // =================================================================

    protected void Page_Load(object sender, EventArgs e)
    {
        if (Request.QueryString["ajax"] == "search_emp")
        {
            if (!HrAccess.RequireHr(true)) return;
            string json = EmployeeSearch();
            Response.Clear();
            Response.ContentType = "application/json";
            Response.Write(json);
            Response.End();
            return;
        }

        string action = Request.QueryString["action"];
        if (!string.IsNullOrEmpty(action))
        {
            bool file = action == "export";
            if (!HrAccess.RequireHr(!file)) return;
            if (file)
            {
                Export();
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
        try { Render(); }
        catch (Exception ex)
        {
            Log("Error", "Page load: " + ex.Message);
            litBody.Text = "<div class=\"hr-notice hr-notice--bad\">Deductions could not be loaded. Please refresh the page.</div>";
        }
    }

    private static string Result(bool ok, string message)
    {
        Dictionary<string, object> d = new Dictionary<string, object>();
        d["ok"] = ok;
        d["message"] = message;
        return new JavaScriptSerializer().Serialize(d);
    }

    /// <summary>One JSON object per request: {"results":[...]}.</summary>
    private string EmployeeSearch()
    {
        List<Dictionary<string, string>> list = new List<Dictionary<string, string>>();
        string q = (Request.QueryString["q"] ?? "").Trim();
        if (q.Length > 0)
        {
            try
            {
                DataTable dt = Q(@"
                    SELECT e.empID, e.emp_name, e.EMP_CODE, IFNULL(j.jobname, '') AS emp_position
                    FROM hrm_employee e
                    LEFT JOIN hrm_emp_contracts c ON c.empID = e.empID
                        AND c.ID = (SELECT MAX(c2.ID) FROM hrm_emp_contracts c2 WHERE c2.empID = e.empID)
                    LEFT JOIN hrm_jobs j ON j.ID = c.jobID
                    WHERE e.emp_name LIKE @q OR e.EMP_CODE LIKE @q
                    ORDER BY e.emp_name LIMIT 15", P("@q", "%" + q + "%"));
                foreach (DataRow r in dt.Rows)
                {
                    Dictionary<string, string> d = new Dictionary<string, string>();
                    d["empID"] = Str(r["empID"]);
                    d["emp_name"] = Str(r["emp_name"]);
                    d["EMP_CODE"] = Str(r["EMP_CODE"]) == "-" ? "" : Str(r["EMP_CODE"]);
                    d["emp_position"] = IC.TextInfo.ToTitleCase(Str(r["emp_position"]).ToLowerInvariant());
                    list.Add(d);
                }
            }
            catch (Exception ex) { Log("Error", "Employee search: " + ex.Message); }
        }
        Dictionary<string, object> res = new Dictionary<string, object>();
        res["results"] = list;
        return new JavaScriptSerializer().Serialize(res);
    }

    private string HandleAction(string action)
    {
        if (Request.HttpMethod != "POST") throw new UserError("Invalid request.");
        switch (action)
        {
            case "add": return Add();
            case "edit": return Edit();
            case "cancel": return SetMany(false);
            case "delete": return SetMany(true);
        }
        throw new UserError("Unknown action.");
    }

    // =================================================================
    //  Writes
    // =================================================================

    private void ReadCommon(List<string> allowedTypes, string keepType, out string type, out decimal amount, out string month, out int year, out string desc)
    {
        type = (Request.Form["type"] ?? "").Trim();
        if (type == "" || (!allowedTypes.Contains(type) && type != keepType)) throw new UserError("Select the type.");
        if (!decimal.TryParse((Request.Form["amount"] ?? "").Replace(",", "").Trim(), NumberStyles.Number, IC, out amount) || amount <= 0)
            throw new UserError("Enter an amount greater than zero.");
        if (amount > 1000000000m) throw new UserError("The amount is too large.");
        month = (Request.Form["month"] ?? "").ToUpperInvariant();
        if (Array.IndexOf(MONTHS, month) < 0) throw new UserError("Select the month.");
        year = Int(Request.Form["year"]);
        if (year < 2020 || year > 2099) throw new UserError("Select the year.");
        desc = (Request.Form["description"] ?? "").Trim();
        if (desc.Length > 500) throw new UserError("The description can have at most 500 characters.");
    }

    private string Add()
    {
        int empId = Int(Request.Form["emp"]);
        if (empId <= 0 || Q("SELECT empID FROM hrm_employee WHERE empID=@e", P("@e", empId)).Rows.Count == 0)
            throw new UserError("Select the employee.");
        string type, month, desc;
        decimal amount;
        int year;
        ReadCommon(Types(), null, out type, out amount, out month, out year, out desc);
        int months = Int(Request.Form["months"]);
        if (months < 1 || months > 60) throw new UserError("Number of months must be between 1 and 60.");

        int start = Array.IndexOf(MONTHS, month), created = 0;
        string by = CurrentUser();
        using (MySqlConnection c = new MySqlConnection(ConnStr))
        {
            c.Open();
            using (MySqlTransaction tx = c.BeginTransaction())
            {
                for (int i = 0; i < months; i++)
                {
                    int y = year + (start + i) / 12;
                    if (y > 2099) break;
                    using (MySqlCommand cmd = new MySqlCommand(
                        "INSERT INTO hrm_deduction_records (empID, deduction_type, amount, description, date_recorded, status, to_deduct_month, to_deduct_year, recorded_by) " +
                        "VALUES (@eid, @type, @amount, @desc, CURDATE(), 'PENDING', @month, @year, @by)", c, tx))
                    {
                        cmd.Parameters.Add(P("@eid", empId));
                        cmd.Parameters.Add(P("@type", type));
                        cmd.Parameters.Add(P("@amount", amount));
                        cmd.Parameters.Add(P("@desc", desc.Replace("{n}", (i + 1).ToString(IC))));
                        cmd.Parameters.Add(P("@month", MONTHS[(start + i) % 12]));
                        cmd.Parameters.Add(P("@year", y));
                        cmd.Parameters.Add(P("@by", by));
                        cmd.ExecuteNonQuery();
                    }
                    created++;
                }
                tx.Commit();
            }
        }
        Log("Deduction added", "Employee " + empId + ": " + type + " " + amount.ToString("0", IC) + " x " + created + " from " + month + " " + year);
        return Result(true, created == 1 ? "Deduction added." : created + " monthly deductions added.");
    }

    private DataRow LoadItem(int id)
    {
        DataTable dt = Q("SELECT a.*, p.payroll_title FROM hrm_deduction_records a LEFT JOIN hrm_payroll p ON p.ID = a.payroll_id WHERE a.id=@id", P("@id", id));
        return dt.Rows.Count > 0 ? dt.Rows[0] : null;
    }

    private string Edit()
    {
        int id = Int(Request.Form["id"]);
        DataRow r = LoadItem(id);
        if (r == null) throw new UserError("Deduction not found.");
        if (Str(r["status"]) == "SETTLED") throw new UserError("A settled deduction cannot be changed.");
        if (Str(r["status"]) == "CANCELLED") throw new UserError("A cancelled deduction cannot be changed.");
        string type, month, desc;
        decimal amount;
        int year;
        ReadCommon(Types(), Str(r["deduction_type"]), out type, out amount, out month, out year, out desc);
        X("UPDATE hrm_deduction_records SET payroll_id=IF(to_deduct_month=@month AND to_deduct_year=@year, payroll_id, NULL), deduction_type=@type, amount=@amount, to_deduct_month=@month, to_deduct_year=@year, description=@desc WHERE id=@id AND status='PENDING'",
            P("@type", type), P("@amount", amount), P("@month", month), P("@year", year), P("@desc", desc), P("@id", id));
        Log("Deduction changed", "Item " + id + ": " + type + " " + amount.ToString("0", IC) + " " + month + " " + year);
        string run = Str(r["payroll_title"]);
        return Result(true, run == "" ? "Saved." : "Saved. Regenerate payroll run \"" + run + "\" to apply the change.");
    }

    private string SetMany(bool delete)
    {
        List<int> ids = new List<int>();
        foreach (string s in (Request.Form["ids"] ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int v;
            if (int.TryParse(s.Trim(), out v) && v > 0 && !ids.Contains(v)) ids.Add(v);
        }
        if (ids.Count == 0) throw new UserError("Select at least one deduction.");

        int done = 0, skipped = 0;
        List<string> runs = new List<string>();
        using (MySqlConnection c = new MySqlConnection(ConnStr))
        {
            c.Open();
            using (MySqlTransaction tx = c.BeginTransaction())
            {
                foreach (int id in ids)
                {
                    string run = "";
                    using (MySqlCommand cmd = new MySqlCommand("SELECT p.payroll_title FROM hrm_deduction_records a JOIN hrm_payroll p ON p.ID = a.payroll_id WHERE a.id=@id AND a.status='PENDING'", c, tx))
                    {
                        cmd.Parameters.Add(P("@id", id));
                        object o = cmd.ExecuteScalar();
                        run = o == null || o == DBNull.Value ? "" : o.ToString();
                    }
                    string sql = delete
                        ? "DELETE FROM hrm_deduction_records WHERE id=@id AND status <> 'SETTLED'"
                        : "UPDATE hrm_deduction_records SET status='CANCELLED', payroll_id=NULL WHERE id=@id AND status='PENDING'";
                    using (MySqlCommand cmd = new MySqlCommand(sql, c, tx))
                    {
                        cmd.Parameters.Add(P("@id", id));
                        if (cmd.ExecuteNonQuery() > 0) { done++; if (run != "" && !runs.Contains(run)) runs.Add(run); }
                        else skipped++;
                    }
                }
                tx.Commit();
            }
        }
        string msg = done + (done == 1 ? " deduction " : " deductions ") + (delete ? "deleted." : "cancelled.");
        if (skipped > 0) msg += " " + skipped + " skipped (settled" + (delete ? "" : " or already cancelled") + ").";
        if (runs.Count > 0) msg += " Regenerate payroll run \"" + string.Join("\", \"", runs.ToArray()) + "\" to apply the change.";
        if (done == 0) throw new UserError(delete ? "Settled deductions cannot be deleted." : "Only pending deductions can be cancelled.");
        Log(delete ? "Deductions deleted" : "Deductions cancelled", done + " item(s): " + Trunc(string.Join(",", ids.ConvertAll<string>(delegate (int i) { return i.ToString(IC); }).ToArray()), 250));
        return Result(true, msg);
    }

    // =================================================================
    //  Filters (query string) and list
    // =================================================================

    private class Filter
    {
        public string Q = "", Month = "", Status = "";
        public int Year, Page = 1;
    }

    private Filter ReadFilter()
    {
        Filter f = new Filter();
        f.Q = Trunc((Request.QueryString["q"] ?? "").Trim(), 60);
        f.Month = (Request.QueryString["month"] ?? "").ToUpperInvariant();
        if (Array.IndexOf(MONTHS, f.Month) < 0) f.Month = "";
        f.Year = Int(Request.QueryString["year"]);
        if (f.Year < 2000 || f.Year > 2099) f.Year = 0;
        f.Status = (Request.QueryString["status"] ?? "").ToUpperInvariant();
        if (f.Status != "PENDING" && f.Status != "INRUN" && f.Status != "SETTLED" && f.Status != "CANCELLED") f.Status = "";
        f.Page = Math.Max(1, Int(Request.QueryString["page"]));
        return f;
    }

    private static string Url(Filter f, int page, string extra)
    {
        List<string> p = new List<string>();
        if (f.Q != "") p.Add("q=" + HttpUtility.UrlEncode(f.Q));
        if (f.Month != "") p.Add("month=" + f.Month);
        if (f.Year > 0) p.Add("year=" + f.Year);
        if (f.Status != "") p.Add("status=" + f.Status);
        if (page > 1) p.Add("page=" + page);
        if (!string.IsNullOrEmpty(extra)) p.Add(extra);
        return PageUrl + (p.Count > 0 ? "?" + string.Join("&", p.ToArray()) : "");
    }

    private string Where(Filter f, List<MySqlParameter> parms)
    {
        string w = " WHERE 1=1";
        if (f.Q != "") { w += " AND (e.emp_name LIKE @q OR e.EMP_CODE LIKE @q)"; parms.Add(P("@q", "%" + f.Q + "%")); }
        if (f.Month != "") { w += " AND a.to_deduct_month = @month"; parms.Add(P("@month", f.Month)); }
        if (f.Year > 0) { w += " AND a.to_deduct_year = @yr"; parms.Add(P("@yr", f.Year)); }
        if (f.Status == "PENDING") w += " AND a.status = 'PENDING' AND a.payroll_id IS NULL";
        else if (f.Status == "INRUN") w += " AND a.status = 'PENDING' AND a.payroll_id IS NOT NULL";
        else if (f.Status != "") { w += " AND a.status = @st"; parms.Add(P("@st", f.Status)); }
        return w;
    }

    private const string Select = @"
        SELECT a.id, a.empID, a.deduction_type AS item_type, a.amount, a.to_deduct_month AS item_month, a.to_deduct_year AS item_year,
               a.status, a.description, a.date_recorded, a.recorded_by, a.payroll_id, a.payment_date,
               e.EMP_CODE, e.emp_name, p.payroll_title
        FROM hrm_deduction_records a
        JOIN hrm_employee e ON e.empID = a.empID
        LEFT JOIN hrm_payroll p ON p.ID = a.payroll_id";

    private const string Order = " ORDER BY a.to_deduct_year DESC, FIELD(a.to_deduct_month,'JANUARY','FEBRUARY','MARCH','APRIL','MAY','JUNE','JULY','AUGUST','SEPTEMBER','OCTOBER','NOVEMBER','DECEMBER') DESC, e.emp_name, a.id DESC";

    private void Render()
    {
        Filter f = ReadFilter();
        StringBuilder h = new StringBuilder();
        int cy = DateTime.Today.Year;

        DataTable k = Q(@"SELECT COALESCE(SUM(status='PENDING'),0) AS pending,
                                 COALESCE(SUM(status='PENDING' AND payroll_id IS NOT NULL),0) AS inrun,
                                 COALESCE(SUM(CASE WHEN status='PENDING' THEN amount ELSE 0 END),0) AS pending_amt,
                                 COALESCE(SUM(status='SETTLED' AND YEAR(payment_date) = @cy),0) AS settled,
                                 COALESCE(SUM(status='CANCELLED'),0) AS cancelled
                          FROM hrm_deduction_records", P("@cy", cy));
        DataRow kr = k.Rows[0];
        h.Append("<div class=\"hr-kpis\">");
        h.Append("<a class=\"hr-kpi\" href=\"").Append(PageUrl).Append("?status=PENDING\"><div class=\"hr-kpi__label\">Pending</div><div class=\"hr-kpi__value\">")
         .Append(Int(kr["pending"])).Append("</div><div class=\"hr-kpi__sub\">").Append(Int(kr["inrun"])).Append(" in a payroll run</div></a>");
        h.Append("<div class=\"hr-kpi\"><div class=\"hr-kpi__label\">Pending amount (UGX)</div><div class=\"hr-kpi__value\">").Append(Money(kr["pending_amt"]))
         .Append("</div><div class=\"hr-kpi__sub\">Not yet settled</div></div>");
        h.Append("<a class=\"hr-kpi\" href=\"").Append(PageUrl).Append("?status=SETTLED&amp;year=").Append(cy).Append("\"><div class=\"hr-kpi__label\">Settled in ").Append(cy)
         .Append("</div><div class=\"hr-kpi__value\">").Append(Int(kr["settled"])).Append("</div><div class=\"hr-kpi__sub\">Paid through approved runs</div></a>");
        h.Append("<a class=\"hr-kpi\" href=\"").Append(PageUrl).Append("?status=CANCELLED\"><div class=\"hr-kpi__label\">Cancelled</div><div class=\"hr-kpi__value\">")
         .Append(Int(kr["cancelled"])).Append("</div><div class=\"hr-kpi__sub\">All years</div></a>");
        h.Append("</div>");

        // Filters
        List<int> years = new List<int>();
        foreach (DataRow r in Q("SELECT DISTINCT to_deduct_year AS y FROM hrm_deduction_records ORDER BY y DESC").Rows) if (Int(r["y"]) > 0) years.Add(Int(r["y"]));
        if (!years.Contains(cy)) years.Add(cy);
        years.Sort(); years.Reverse();

        h.Append("<div class=\"hr-filters\">");
        h.Append("<div class=\"hr-filter hr-filter--grow\"><label for=\"fQ\">Employee</label><input type=\"text\" id=\"fQ\" class=\"hr-input\" placeholder=\"Name or staff no\" value=\"")
         .Append(HttpUtility.HtmlAttributeEncode(f.Q)).Append("\" onkeydown=\"if(event.key==='Enter'){event.preventDefault();applyFilters();}\" /></div>");
        h.Append("<div class=\"hr-filter\"><label for=\"fMonth\">Month</label><select id=\"fMonth\" class=\"hr-select\" onchange=\"applyFilters()\"><option value=\"\">All months</option>");
        foreach (string m in MONTHS) h.Append("<option value=\"").Append(m).Append("\"").Append(m == f.Month ? " selected" : "").Append(">").Append(MonthTitle(m)).Append("</option>");
        h.Append("</select></div>");
        h.Append("<div class=\"hr-filter\"><label for=\"fYear\">Year</label><select id=\"fYear\" class=\"hr-select\" onchange=\"applyFilters()\"><option value=\"\">All years</option>");
        foreach (int y in years) h.Append("<option value=\"").Append(y).Append("\"").Append(y == f.Year ? " selected" : "").Append(">").Append(y).Append("</option>");
        h.Append("</select></div>");
        h.Append("<div class=\"hr-filter\"><label for=\"fStatus\">Status</label><select id=\"fStatus\" class=\"hr-select\" onchange=\"applyFilters()\">");
        string[,] sts = { { "", "All statuses" }, { "PENDING", "Pending" }, { "INRUN", "In payroll run" }, { "SETTLED", "Settled" }, { "CANCELLED", "Cancelled" } };
        for (int i = 0; i < sts.GetLength(0); i++)
            h.Append("<option value=\"").Append(sts[i, 0]).Append("\"").Append(sts[i, 0] == f.Status ? " selected" : "").Append(">").Append(sts[i, 1]).Append("</option>");
        h.Append("</select></div>");
        h.Append("<div class=\"hr-filters__actions\"><button type=\"button\" class=\"hr-btn hr-btn--primary\" onclick=\"applyFilters()\">Search</button>")
         .Append("<a class=\"hr-btn hr-btn--secondary\" href=\"").Append(PageUrl).Append("\">Reset</a></div></div>");

        // List
        List<MySqlParameter> parms = new List<MySqlParameter>();
        string where = Where(f, parms);
        DataTable cnt = Q("SELECT COUNT(*) AS n, COALESCE(SUM(a.amount),0) AS amt FROM hrm_deduction_records a JOIN hrm_employee e ON e.empID = a.empID" + where, parms.ToArray());
        int total = Int(cnt.Rows[0]["n"]);
        int pages = Math.Max(1, (total + PageSize - 1) / PageSize);
        int page = Math.Min(f.Page, pages);
        List<MySqlParameter> parms2 = new List<MySqlParameter>();
        Where(f, parms2);
        DataTable dt = Q(Select + where + Order + " LIMIT " + PageSize + " OFFSET " + ((page - 1) * PageSize), parms2.ToArray());

        h.Append("<div class=\"hr-card\"><div class=\"hr-card__head\"><div class=\"hr-card__title\">Deductions</div><div class=\"hr-row\"><span class=\"hr-card__meta\">")
         .Append(total).Append(total == 1 ? " item, UGX " : " items, UGX ").Append(Money(cnt.Rows[0]["amt"])).Append("</span>");
        if (total > 0)
        {
            h.Append("<a class=\"hr-btn hr-btn--secondary hr-btn--sm\" href=\"").Append(HttpUtility.HtmlAttributeEncode(Url(f, 1, "action=export"))).Append("\">Export (xlsx)</a>");
            h.Append("<a class=\"hr-btn hr-btn--secondary hr-btn--sm\" href=\"").Append(HttpUtility.HtmlAttributeEncode(Url(f, 1, "action=export&format=csv"))).Append("\">Export (csv)</a>");
        }
        h.Append("</div></div>");
        h.Append("<div class=\"hr-bulk\" id=\"bulkBar\"><span id=\"bulkCount\">0 selected</span><span class=\"hr-spacer\"></span>")
         .Append("<button type=\"button\" class=\"hr-btn hr-btn--inverse hr-btn--sm\" onclick=\"bulk('cancel')\">Cancel selected</button>")
         .Append("<button type=\"button\" class=\"hr-btn hr-btn--inverse hr-btn--sm\" onclick=\"bulk('delete')\">Delete selected</button>")
         .Append("<button type=\"button\" class=\"hr-btn hr-btn--inverse hr-btn--sm\" onclick=\"clearSelection()\">Clear</button></div>");

        if (dt.Rows.Count == 0)
        {
            h.Append("<div class=\"hr-empty\">No deductions match the filters.</div>");
        }
        else
        {
            h.Append("<div class=\"hr-table-wrap\"><table class=\"hr-table\"><thead><tr>")
             .Append("<th style=\"width:32px\"><input type=\"checkbox\" id=\"chkAll\" onclick=\"selectAll(this)\" aria-label=\"Select all\" /></th>")
             .Append("<th>Staff no</th><th>Name</th><th>Type</th><th>Month</th><th class=\"hr-num\">Amount (UGX)</th><th>Status</th><th>Payroll run</th><th>Description</th><th>Recorded</th><th></th>")
             .Append("</tr></thead><tbody>");
            decimal sum = 0;
            foreach (DataRow r in dt.Rows)
            {
                int id = Int(r["id"]);
                string st = StatusOf(r);
                bool changeable = st == "PENDING" || st == "INRUN";
                bool deletable = st != "SETTLED";
                sum += Dec(r["amount"]);
                h.Append("<tr data-id=\"").Append(id).Append("\" data-type=\"").Append(HttpUtility.HtmlAttributeEncode(Str(r["item_type"])))
                 .Append("\" data-amount=\"").Append(Dec(r["amount"]).ToString("0.##", IC)).Append("\" data-month=\"").Append(Str(r["item_month"]))
                 .Append("\" data-year=\"").Append(Int(r["item_year"])).Append("\" data-desc=\"").Append(HttpUtility.HtmlAttributeEncode(Str(r["description"])))
                 .Append("\" data-name=\"").Append(HttpUtility.HtmlAttributeEncode(Str(r["emp_name"]))).Append("\">");
                h.Append("<td>").Append(deletable ? "<input type=\"checkbox\" class=\"row-chk\" value=\"" + id + "\" onclick=\"updateBulk()\" aria-label=\"Select\" />" : "").Append("</td>");
                h.Append("<td>").Append(E(Str(r["EMP_CODE"]) == "-" ? "" : Str(r["EMP_CODE"]))).Append("</td><td>").Append(E(r["emp_name"])).Append("</td>");
                h.Append("<td>").Append(E(r["item_type"])).Append("</td><td>").Append(E(MonthTitle(Str(r["item_month"])) + " " + Str(r["item_year"]))).Append("</td>");
                h.Append("<td class=\"hr-num\">").Append(Money(r["amount"])).Append("</td><td>").Append(Badge(st)).Append("</td>");
                h.Append("<td>").Append(E(r["payroll_title"]));
                if (st == "SETTLED" && Str(r["payment_date"]) != "") h.Append("<span class=\"hr-sub\">").Append(ShortDate(r["payment_date"])).Append("</span>");
                h.Append("</td>");
                string desc = Str(r["description"]);
                h.Append("<td title=\"").Append(HttpUtility.HtmlAttributeEncode(desc)).Append("\">").Append(E(desc.Length > 50 ? desc.Substring(0, 50).TrimEnd() + "..." : desc)).Append("</td>");
                h.Append("<td>").Append(ShortDate(r["date_recorded"]));
                if (Str(r["recorded_by"]) != "") h.Append("<span class=\"hr-sub\">").Append(E(r["recorded_by"])).Append("</span>");
                h.Append("</td><td class=\"hr-right\" style=\"white-space:nowrap\">");
                if (changeable)
                {
                    h.Append("<button type=\"button\" class=\"hr-btn hr-btn--secondary hr-btn--sm\" onclick=\"openEdit(this)\">Edit</button> ");
                    h.Append("<button type=\"button\" class=\"hr-btn hr-btn--secondary hr-btn--sm\" onclick=\"one('cancel',").Append(id).Append(")\">Cancel</button> ");
                }
                if (deletable) h.Append("<button type=\"button\" class=\"hr-btn hr-btn--danger hr-btn--sm\" onclick=\"one('delete',").Append(id).Append(")\">Delete</button>");
                h.Append("</td></tr>");
            }
            h.Append("</tbody>");
            if (pages == 1)
                h.Append("<tfoot><tr><td></td><td colspan=\"4\">Total (").Append(dt.Rows.Count).Append(")</td><td class=\"hr-num\">").Append(sum.ToString("#,##0", IC)).Append("</td><td colspan=\"5\"></td></tr></tfoot>");
            h.Append("</table></div>");
        }
        h.Append("<div class=\"hr-card__foot\"><span>").Append(pages > 1 ? "Page " + page + " of " + pages : "Settled deductions cannot be changed.").Append("</span>");
        if (pages > 1)
        {
            h.Append("<div class=\"hr-pager\"><button type=\"button\"").Append(page <= 1 ? " disabled" : "").Append(" onclick=\"location.href='")
             .Append(HttpUtility.JavaScriptStringEncode(Url(f, page - 1, null))).Append("'\">Previous</button><button type=\"button\"").Append(page >= pages ? " disabled" : "")
             .Append(" onclick=\"location.href='").Append(HttpUtility.JavaScriptStringEncode(Url(f, page + 1, null))).Append("'\">Next</button></div>");
        }
        h.Append("</div></div>");
        litBody.Text = h.ToString();

        // Dialog lists (rendered every load)
        StringBuilder t = new StringBuilder("<option value=\"\">Select type</option>");
        foreach (string s in Types()) t.Append("<option value=\"").Append(HttpUtility.HtmlAttributeEncode(s)).Append("\">").Append(E(s)).Append("</option>");
        litTypeOptions.Text = t.ToString();
        StringBuilder mo = new StringBuilder();
        for (int i = 0; i < 12; i++)
            mo.Append("<option value=\"").Append(MONTHS[i]).Append("\"").Append(i + 1 == DateTime.Today.Month ? " selected" : "").Append(">").Append(MonthTitle(MONTHS[i])).Append("</option>");
        litMonthOptions.Text = mo.ToString();
        StringBuilder yo = new StringBuilder();
        for (int y = cy + 2; y >= 2020; y--) yo.Append("<option value=\"").Append(y).Append("\"").Append(y == cy ? " selected" : "").Append(">").Append(y).Append("</option>");
        litYearOptions.Text = yo.ToString();
    }

    // =================================================================
    //  Export of the filtered items
    // =================================================================

    private void Export()
    {
        try
        {
            Filter f = ReadFilter();
            List<MySqlParameter> parms = new List<MySqlParameter>();
            DataTable dt = Q(Select + Where(f, parms) + Order, parms.ToArray());

            HrExport.Report rep = new HrExport.Report("Deductions", "deductions");
            rep.PreparedBy = CurrentUser();
            rep.AddScope("Employee", f.Q);
            rep.AddScope("Month", f.Month == "" ? "" : MonthTitle(f.Month));
            rep.AddScope("Year", f.Year > 0 ? f.Year.ToString(IC) : "");
            rep.AddScope("Status", f.Status == "" ? "" : StatusWord(f.Status));
            rep.AddScope("Amounts", "UGX");
            HrExport.Sheet s = rep.NewSheet("Deductions");
            s.Add("Staff No").Add("Name").Add("Type").Add("Month").Add("Amount", HrExport.Kind.Money, true).Add("Status")
             .Add("Payroll run").Add("Settled on", HrExport.Kind.Date).Add("Description").Add("Recorded on", HrExport.Kind.Date).Add("Recorded by");
            foreach (DataRow r in dt.Rows)
            {
                s.Row(Str(r["EMP_CODE"]) == "-" ? "" : Str(r["EMP_CODE"]), Str(r["emp_name"]), Str(r["item_type"]),
                    MonthTitle(Str(r["item_month"])) + " " + Str(r["item_year"]), Dec(r["amount"]), StatusWord(StatusOf(r)),
                    Str(r["payroll_title"]), r["payment_date"], Str(r["description"]), r["date_recorded"], Str(r["recorded_by"]));
            }
            if (Request.QueryString["format"] == "csv") HrExport.SendCsv(Response, rep, 0);
            else HrExport.SendXlsx(Response, rep);
            Log("Deductions export", dt.Rows.Count + " rows");
        }
        catch (Exception ex)
        {
            Log("Error", "Export: " + ex.Message);
            Response.Clear();
            Response.ContentType = "text/plain";
            Response.Write("The file could not be produced. Please try again.");
        }
    }
}
