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
/// Organisation and pay scales: departments, positions, stations, pay scales, banks and payroll
/// items. Each list has add and edit dialogs. These tables have no active flag, so a row that
/// other records use cannot be deleted (the screen shows where it is used); unused rows can.
/// The head of department is chosen from the employee list.
/// </summary>
public partial class COOPERP_NewScreens_HRSettings : System.Web.UI.Page
{
    private static readonly CultureInfo IC = CultureInfo.InvariantCulture;

    private class UserError : Exception { public UserError(string m) : base(m) { } }

    private class Section
    {
        public string Key, Title, Single, Table, Id;
        public Section(string key, string title, string single, string table, string id)
        { Key = key; Title = title; Single = single; Table = table; Id = id; }
    }

    private static readonly Section[] Sections = {
        new Section("departments", "Departments", "department", "hrm_departments", "ID"),
        new Section("positions", "Positions", "position", "hrm_jobs", "ID"),
        new Section("stations", "Stations", "station", "hrm_stations", "ID"),
        new Section("scales", "Pay scales", "pay scale", "hrm_payscales", "ID"),
        new Section("banks", "Banks", "bank", "banks", "bank_id"),
        new Section("items", "Payroll items", "payroll item", "hrm_allowance_deductions", "ID")
    };

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

    private int Count(string sql, params MySqlParameter[] parms)
    {
        try
        {
            DataTable dt = Q(sql, parms);
            return dt.Rows.Count > 0 ? Int(dt.Rows[0][0]) : 0;
        }
        catch { return 0; }
    }

    private static int Int(object v)
    {
        if (v == null || v == DBNull.Value) return 0;
        int i;
        return int.TryParse(v.ToString(), out i) ? i : 0;
    }

    private static decimal Dec(object v)
    {
        if (v == null || v == DBNull.Value) return 0m;
        decimal d;
        return decimal.TryParse(v.ToString(), NumberStyles.Any, IC, out d) ? d : 0m;
    }

    private static string Str(object v) { return v == null || v == DBNull.Value ? "" : v.ToString().Trim(); }
    private static string E(object s) { return HttpUtility.HtmlEncode(HrExport.Clean(Str(s))); }
    private static string A(object s) { return HttpUtility.HtmlAttributeEncode(Str(s)); }
    private static string Trunc(string s, int n) { s = s ?? ""; return s.Length <= n ? s : s.Substring(0, n); }
    private static string Word(string s) { return IC.TextInfo.ToTitleCase((s ?? "").Trim().ToLowerInvariant()); }

    private void Log(string what, string detail)
    {
        try
        {
            X("INSERT INTO acad_activity_log (user_id, page_function, par, comments, access_date) VALUES (@u, 'HR Settings', @p, @c, NOW())",
                P("@u", Trunc(HrAccess.Username(), 100)), P("@p", Trunc(detail, 300)), P("@c", Trunc(what, 200)));
        }
        catch { }
    }

    private static Section Find(string key)
    {
        foreach (Section s in Sections) if (s.Key == key) return s;
        return null;
    }

    // =================================================================
    //  Page load and dispatch
    // =================================================================

    protected void Page_Load(object sender, EventArgs e)
    {
        string action = Request.QueryString["action"];
        if (!string.IsNullOrEmpty(action))
        {
            if (!HrAccess.RequireHr(true)) return;
            string json;
            try
            {
                if (Request.HttpMethod != "POST") throw new UserError("Invalid request.");
                Section s = Find(Request.Form["section"]);
                if (s == null) throw new UserError("Unknown list.");
                if (action == "save") json = Save(s);
                else if (action == "delete") json = Delete(s);
                else throw new UserError("Unknown action.");
            }
            catch (UserError ue) { json = Result(false, ue.Message); }
            catch (Exception ex)
            {
                Log("Error", action + ": " + ex.Message);
                json = Result(false, "The change could not be saved. Please try again.");
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
            litBody.Text = "<div class=\"hr-notice hr-notice--bad\">Settings could not be loaded. Please refresh the page.</div>";
        }
    }

    private static string Result(bool ok, string message)
    {
        Dictionary<string, object> d = new Dictionary<string, object>();
        d["ok"] = ok;
        d["message"] = message;
        return new JavaScriptSerializer().Serialize(d);
    }

    // =================================================================
    //  Save and delete
    // =================================================================

    private string Text(string field, string label, int max, bool required)
    {
        string v = (Request.Form[field] ?? "").Trim();
        if (required && v == "") throw new UserError("Enter the " + label + ".");
        if (v.Length > max) throw new UserError("The " + label + " can have at most " + max + " characters.");
        return v;
    }

    private decimal Number(string field, string label)
    {
        decimal v;
        if (!decimal.TryParse((Request.Form[field] ?? "").Replace(",", "").Trim(), NumberStyles.Number, IC, out v) || v < 0)
            throw new UserError("Enter a valid " + label + ".");
        return v;
    }

    private string Pick(string field, string label, string[] allowed, string keep)
    {
        string v = (Request.Form[field] ?? "").Trim().ToUpperInvariant();
        if (Array.IndexOf(allowed, v) < 0 && v != (keep ?? "").ToUpperInvariant()) throw new UserError("Select the " + label + ".");
        return v;
    }

    private string Save(Section s)
    {
        int id = Int(Request.Form["id"]);
        DataRow old = null;
        if (id > 0)
        {
            DataTable o = Q("SELECT * FROM " + s.Table + " WHERE " + s.Id + "=@id", P("@id", id));
            if (o.Rows.Count == 0) throw new UserError("This " + s.Single + " no longer exists.");
            old = o.Rows[0];
        }

        string name;
        List<string> cols = new List<string>();
        List<MySqlParameter> parms = new List<MySqlParameter>();
        switch (s.Key)
        {
            case "departments":
                name = Text("name", "department name", 200, true);
                int head = Int(Request.Form["head"]);
                if (head > 0 && Count("SELECT COUNT(*) FROM hrm_employee WHERE empID=@e", P("@e", head)) == 0) throw new UserError("Select the head of department from the list.");
                cols.Add("dept_name"); parms.Add(P("@dept_name", name));
                cols.Add("dept_headID"); parms.Add(P("@dept_headID", head));
                break;
            case "positions":
                name = Text("name", "position", 45, true);
                cols.Add("jobname"); parms.Add(P("@jobname", name));
                cols.Add("min_qualifications"); parms.Add(P("@min_qualifications", Text("qual", "minimum qualifications", 45, false)));
                break;
            case "stations":
                name = Text("name", "station name", 150, true);
                cols.Add("station_name"); parms.Add(P("@station_name", name));
                break;
            case "scales":
                name = Text("name", "scale name", 45, true);
                cols.Add("scale_name"); parms.Add(P("@scale_name", name));
                cols.Add("basicpay"); parms.Add(P("@basicpay", Number("amount", "basic pay")));
                break;
            case "banks":
                name = Text("name", "bank name", 150, true);
                cols.Add("bank_name"); parms.Add(P("@bank_name", name));
                break;
            default:
                name = Text("name", "item name", 45, true);
                cols.Add("dedall_name"); parms.Add(P("@dedall_name", name));
                cols.Add("ded_allowance"); parms.Add(P("@ded_allowance", Pick("kind", "kind", new[] { "ALLOWANCE", "DEDUCTION" }, old == null ? null : Str(old["ded_allowance"]))));
                cols.Add("dedall_type"); parms.Add(P("@dedall_type", Pick("category", "category", new[] { "STATUTORY", "MANDATORY", "OPTIONAL" }, old == null ? null : Str(old["dedall_type"]))));
                cols.Add("computation_by"); parms.Add(P("@computation_by", Pick("computation", "computation", new[] { "FIXED", "PERCENTAGE" }, old == null ? null : Str(old["computation_by"]))));
                cols.Add("dedall_amount"); parms.Add(P("@dedall_amount", Number("amount", "amount or rate")));
                break;
        }

        string nameCol = cols[0];
        if (Count("SELECT COUNT(*) FROM " + s.Table + " WHERE TRIM(" + nameCol + ") = @n AND " + s.Id + " <> @id", P("@n", name), P("@id", id)) > 0)
            throw new UserError("A " + s.Single + " with this name already exists.");

        parms.Add(P("@id", id));
        if (id > 0)
        {
            List<string> sets = new List<string>();
            foreach (string c in cols) sets.Add(c + "=@" + c);
            X("UPDATE " + s.Table + " SET " + string.Join(", ", sets.ToArray()) + " WHERE " + s.Id + "=@id", parms.ToArray());
            if (s.Key == "stations" && old != null && Str(old["station_name"]) != name)
                X("UPDATE hrm_employee SET Entry_Satation=@n WHERE Entry_Satation=@o", P("@n", name), P("@o", Str(old["station_name"])));
            Log("Settings changed", s.Title + " " + id + ": " + name);
            return Result(true, Word(s.Single).Substring(0, 1) + s.Single.Substring(1) + " saved.");
        }
        List<string> names = new List<string>();
        foreach (string c in cols) names.Add("@" + c);
        X("INSERT INTO " + s.Table + " (" + string.Join(", ", cols.ToArray()) + ") VALUES (" + string.Join(", ", names.ToArray()) + ")", parms.ToArray());
        Log("Settings added", s.Title + ": " + name);
        return Result(true, Word(s.Single).Substring(0, 1) + s.Single.Substring(1) + " added.");
    }

    /// <summary>Where a row is used, as readable parts ("12 contracts"). Empty when unused.</summary>
    private List<string> Usage(Section s, int id, string name)
    {
        List<string> parts = new List<string>();
        Action<int, string, string> add = delegate (int n, string one, string many) { if (n > 0) parts.Add(n + " " + (n == 1 ? one : many)); };
        switch (s.Key)
        {
            case "departments":
                add(Count("SELECT COUNT(*) FROM hrm_emp_contracts WHERE departmentID=@id", P("@id", id)), "contract", "contracts");
                add(Count("SELECT COUNT(*) FROM hrm_employee WHERE dept_id=@id", P("@id", id)), "employee", "employees");
                add(Count("SELECT COUNT(*) FROM hr_contract_renewals WHERE cur_department_id=@id", P("@id", id)), "contract renewal", "contract renewals");
                add(Count("SELECT COUNT(*) FROM hrm_payroll WHERE target_type='DEPARTMENT' AND FIND_IN_SET(@id, REPLACE(IFNULL(target_ids,''),' ',''))", P("@id", id)), "payroll run", "payroll runs");
                break;
            case "positions":
                add(Count("SELECT COUNT(*) FROM hrm_emp_contracts WHERE jobID=@id", P("@id", id)), "contract", "contracts");
                add(Count("SELECT COUNT(*) FROM hr_contract_renewals WHERE cur_job_id=@id", P("@id", id)), "contract renewal", "contract renewals");
                break;
            case "stations":
                add(Count("SELECT COUNT(*) FROM hrm_employee WHERE Entry_Satation=@n", P("@n", name)), "employee", "employees");
                break;
            case "scales":
                add(Count("SELECT COUNT(*) FROM hrm_emp_contracts WHERE payscale=@id", P("@id", id)), "contract", "contracts");
                break;
            case "banks":
                add(Count("SELECT COUNT(*) FROM hrm_employee WHERE bankID=@id", P("@id", id)), "employee", "employees");
                break;
            default:
                add(Count("SELECT COUNT(*) FROM hrm_ded_allowance_stafflist WHERE ded_allID=@id", P("@id", id)), "staff assignment", "staff assignments");
                add(Count("SELECT COUNT(*) FROM hrm_exemptions WHERE ded_allID=@id", P("@id", id)), "exemption", "exemptions");
                add(Count("SELECT COUNT(*) FROM hrm_monthly_ded_allowance WHERE ded_allID=@id", P("@id", id)), "payroll record", "payroll records");
                break;
        }
        return parts;
    }

    private string Delete(Section s)
    {
        int id = Int(Request.Form["id"]);
        DataTable o = Q("SELECT * FROM " + s.Table + " WHERE " + s.Id + "=@id", P("@id", id));
        if (o.Rows.Count == 0) throw new UserError("This " + s.Single + " no longer exists.");
        string name = Str(o.Rows[0][1]);
        List<string> used = Usage(s, id, name);
        if (used.Count > 0)
        {
            string list = used.Count == 1 ? used[0] : string.Join(", ", used.GetRange(0, used.Count - 1).ToArray()) + " and " + used[used.Count - 1];
            throw new UserError("This " + s.Single + " is used by " + list + ", so it cannot be deleted.");
        }
        X("DELETE FROM " + s.Table + " WHERE " + s.Id + "=@id", P("@id", id));
        Log("Settings deleted", s.Title + " " + id + ": " + name);
        return Result(true, Word(s.Single).Substring(0, 1) + s.Single.Substring(1) + " deleted.");
    }

    // =================================================================
    //  Screen
    // =================================================================

    private Dictionary<string, int> Grouped(string sql)
    {
        Dictionary<string, int> d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try { foreach (DataRow r in Q(sql).Rows) d[Str(r[0])] = Int(r[1]); }
        catch { }
        return d;
    }

    private static int Get(Dictionary<string, int> d, string k) { int v; return d.TryGetValue(k, out v) ? v : 0; }

    private void Render()
    {
        Section s = Find(Request.QueryString["tab"]) ?? Sections[0];
        StringBuilder h = new StringBuilder();

        h.Append("<div class=\"hr-subtabs\">");
        foreach (Section x in Sections)
            h.Append("<a class=\"hr-subtab").Append(x == s ? " hr-subtab--active" : "").Append("\" href=\"HRSettings.aspx?tab=").Append(x.Key).Append("\">").Append(x.Title).Append("</a>");
        h.Append("</div>");

        DataTable dt;
        string[] heads;
        bool[] num;
        Dictionary<string, int> u1 = null, u2 = null;
        switch (s.Key)
        {
            case "departments":
                dt = Q(@"SELECT d.ID, d.dept_name, d.dept_headID, IFNULL(e.emp_name,'') AS head_name FROM hrm_departments d
                         LEFT JOIN hrm_employee e ON e.empID = d.dept_headID ORDER BY d.dept_name");
                u1 = Grouped("SELECT departmentID, COUNT(*) FROM hrm_emp_contracts GROUP BY departmentID");
                u2 = Grouped("SELECT dept_id, COUNT(*) FROM hrm_employee GROUP BY dept_id");
                heads = new[] { "Department", "Head of department", "Contracts", "Employees" };
                num = new[] { false, false, true, true };
                break;
            case "positions":
                dt = Q("SELECT ID, jobname, min_qualifications FROM hrm_jobs ORDER BY jobname");
                u1 = Grouped("SELECT jobID, COUNT(*) FROM hrm_emp_contracts GROUP BY jobID");
                heads = new[] { "Position", "Minimum qualifications", "Contracts" };
                num = new[] { false, false, true };
                break;
            case "stations":
                dt = Q("SELECT ID, station_name FROM hrm_stations ORDER BY station_name");
                u1 = Grouped("SELECT Entry_Satation, COUNT(*) FROM hrm_employee GROUP BY Entry_Satation");
                heads = new[] { "Station", "Employees" };
                num = new[] { false, true };
                break;
            case "scales":
                dt = Q("SELECT ID, scale_name, basicpay FROM hrm_payscales ORDER BY scale_name");
                u1 = Grouped("SELECT payscale, COUNT(*) FROM hrm_emp_contracts GROUP BY payscale");
                heads = new[] { "Pay scale", "Basic pay (UGX)", "Contracts" };
                num = new[] { false, true, true };
                break;
            case "banks":
                dt = Q("SELECT bank_id, bank_name FROM banks ORDER BY bank_name");
                u1 = Grouped("SELECT bankID, COUNT(*) FROM hrm_employee GROUP BY bankID");
                heads = new[] { "Bank", "Employees" };
                num = new[] { false, true };
                break;
            default:
                dt = Q("SELECT ID, dedall_name, ded_allowance, dedall_type, computation_by, dedall_amount FROM hrm_allowance_deductions ORDER BY ded_allowance, dedall_name");
                u1 = Grouped("SELECT ded_allID, COUNT(*) FROM hrm_ded_allowance_stafflist GROUP BY ded_allID");
                heads = new[] { "Name", "Kind", "Category", "Computation", "Amount or rate", "Staff assigned" };
                num = new[] { false, false, false, false, true, true };
                break;
        }

        h.Append("<div class=\"hr-card\"><div class=\"hr-card__head\"><div class=\"hr-card__title\">").Append(s.Title)
         .Append(" <span class=\"hr-card__meta\">").Append(dt.Rows.Count).Append("</span></div><div class=\"hr-row\">")
         .Append("<input type=\"text\" class=\"hr-input\" style=\"width:220px\" placeholder=\"Filter\" oninput=\"filterRows(this.value)\" aria-label=\"Filter\" />")
         .Append("<button type=\"button\" class=\"hr-btn hr-btn--primary hr-btn--sm\" onclick=\"openEdit(null)\">Add ").Append(s.Single).Append("</button></div></div>");

        if (dt.Rows.Count == 0)
        {
            h.Append("<div class=\"hr-empty\">No ").Append(s.Title.ToLowerInvariant()).Append(" recorded.</div>");
        }
        else
        {
            h.Append("<div class=\"hr-table-wrap\"><table class=\"hr-table\" id=\"setTable\"><thead><tr>");
            for (int i = 0; i < heads.Length; i++) h.Append("<th").Append(num[i] ? " class=\"hr-num\"" : "").Append(">").Append(heads[i]).Append("</th>");
            h.Append("<th></th></tr></thead><tbody>");
            foreach (DataRow r in dt.Rows)
            {
                string id = Str(r[0]);
                List<string> cells = new List<string>();
                Dictionary<string, string> data = new Dictionary<string, string>();
                data["id"] = id;
                int used = 0;
                switch (s.Key)
                {
                    case "departments":
                        int c1 = Get(u1, id), c2 = Get(u2, id);
                        used = c1 + c2;
                        cells.Add(E(r["dept_name"]));
                        cells.Add(Str(r["head_name"]) == "" ? "<span class=\"hr-muted\">Not set</span>" : E(r["head_name"]));
                        cells.Add(c1.ToString(IC)); cells.Add(c2.ToString(IC));
                        data["name"] = Str(r["dept_name"]); data["head"] = Int(r["dept_headID"]) > 0 ? Str(r["dept_headID"]) : "";
                        break;
                    case "positions":
                        used = Get(u1, id);
                        cells.Add(E(r["jobname"])); cells.Add(E(r["min_qualifications"])); cells.Add(used.ToString(IC));
                        data["name"] = Str(r["jobname"]); data["qual"] = Str(r["min_qualifications"]);
                        break;
                    case "stations":
                        used = Get(u1, Str(r["station_name"]));
                        cells.Add(E(r["station_name"])); cells.Add(used.ToString(IC));
                        data["name"] = Str(r["station_name"]);
                        break;
                    case "scales":
                        used = Get(u1, id);
                        cells.Add(E(r["scale_name"])); cells.Add(Dec(r["basicpay"]).ToString("#,##0", IC)); cells.Add(used.ToString(IC));
                        data["name"] = Str(r["scale_name"]); data["amount"] = Dec(r["basicpay"]).ToString("0.##", IC);
                        break;
                    case "banks":
                        used = Get(u1, id);
                        cells.Add(E(r["bank_name"])); cells.Add(used.ToString(IC));
                        data["name"] = Str(r["bank_name"]);
                        break;
                    default:
                        used = Get(u1, id);
                        string kind = Str(r["ded_allowance"]).ToUpperInvariant(), comp = Str(r["computation_by"]).ToUpperInvariant();
                        cells.Add(E(r["dedall_name"]));
                        cells.Add("<span class=\"hr-badge hr-badge--neutral\">" + (kind == "ALLOWANCE" ? "Allowance" : "Deduction") + "</span>");
                        cells.Add(E(Word(Str(r["dedall_type"]))));
                        cells.Add(E(Word(comp)));
                        cells.Add(comp == "PERCENTAGE" ? Dec(r["dedall_amount"]).ToString("0.##", IC) + "% of basic" : Dec(r["dedall_amount"]).ToString("#,##0", IC));
                        cells.Add(used.ToString(IC));
                        data["name"] = Str(r["dedall_name"]); data["kind"] = kind; data["category"] = Str(r["dedall_type"]).ToUpperInvariant();
                        data["computation"] = comp; data["amount"] = Dec(r["dedall_amount"]).ToString("0.##", IC);
                        break;
                }
                h.Append("<tr");
                foreach (KeyValuePair<string, string> kv in data) h.Append(" data-").Append(kv.Key).Append("=\"").Append(A(kv.Value)).Append("\"");
                h.Append(">");
                for (int i = 0; i < cells.Count; i++) h.Append("<td").Append(num[i] ? " class=\"hr-num\"" : "").Append(">").Append(cells[i]).Append("</td>");
                h.Append("<td class=\"hr-right\" style=\"white-space:nowrap\"><button type=\"button\" class=\"hr-btn hr-btn--secondary hr-btn--sm\" onclick=\"openEdit(this)\">Edit</button>");
                if (used == 0)
                    h.Append(" <button type=\"button\" class=\"hr-btn hr-btn--danger hr-btn--sm\" onclick=\"del(this)\">Delete</button>");
                h.Append("</td></tr>");
            }
            h.Append("</tbody></table></div>");
        }
        h.Append("<div class=\"hr-card__foot\"><span>Items in use cannot be deleted.</span></div></div>");
        litBody.Text = h.ToString();
        litSection.Text = "<script type=\"text/javascript\">var SECTION = '" + s.Key + "', SINGLE = '" + s.Single + "';</script>";

        // Employee options for the head of department
        if (s.Key == "departments")
        {
            StringBuilder o = new StringBuilder("<option value=\"\">Not set</option>");
            foreach (DataRow r in Q("SELECT empID, emp_name, IFNULL(EMP_CODE,'') AS code FROM hrm_employee WHERE TRIM(emp_name) <> '' ORDER BY emp_name").Rows)
            {
                string code = Str(r["code"]);
                o.Append("<option value=\"").Append(Int(r["empID"])).Append("\">").Append(E(r["emp_name"]))
                 .Append(code != "" && code != "-" ? " (" + E(code) + ")" : "").Append("</option>");
            }
            litHeadOptions.Text = o.ToString();
        }
    }
}
