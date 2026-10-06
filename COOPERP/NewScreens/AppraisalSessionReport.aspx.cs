using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

/// <summary>
/// Appraisal session report (print, HrDocument): session details, one summary table of status and
/// classification counts, category breakdown, roster grouped by category, outstanding items and
/// sign-off. Counts cover every appraisal_records row of the session; the roster lists only
/// appraisals that have reached HR (Awaiting HR, HR reviewed). Work still with the employee or
/// supervisor appears as counts only, never by name.
/// </summary>
public partial class COOPERP_NewScreens_AppraisalSessionReport : System.Web.UI.Page
{
    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    private int QsSid
    {
        get { int v; return int.TryParse(Request.QueryString["sid"] ?? "0", out v) && v > 0 ? v : 0; }
    }

    private static readonly string[] StatusOrder =
        { "PENDING", "EMPLOYEE_IN_PROGRESS", "RETURNED", "EMPLOYEE_SUBMITTED", "SUPERVISOR_IN_PROGRESS", "COMPLETED", "HR_REVIEWED", "CANCELLED" };
    private static readonly string[] Bands =
        { "Exceptional", "Above expectations", "Satisfactory", "Development needed", "Unsatisfactory" };
    private static readonly string[] Categories = { "ACADEMIC", "ADMINISTRATIVE", "SUPPORT" };

    protected void Page_Load(object sender, EventArgs e)
    {
        if (!HrAccess.RequireHr(false)) return;

        HrDocument.Options o = new HrDocument.Options();
        o.BackUrl = "AppraisalSessions.aspx";
        string title = "Appraisal session report";
        try
        {
            if (QsSid <= 0)
            {
                litDocument.Text = HrDocument.Page(title, HrDocument.Paragraph("Choose a session on the Sessions page to open its report."), o);
                return;
            }
            DataTable dtS = Q(
                @"SELECT s.*, IFNULL(e.emp_name,'') AS created_by_name
                  FROM appraisal_sessions s LEFT JOIN hrm_employee e ON e.empID = s.created_by
                  WHERE s.session_id = @sid");
            if (dtS.Rows.Count == 0)
            {
                litDocument.Text = HrDocument.Page(title, HrDocument.Paragraph("This appraisal session does not exist."), o);
                return;
            }
            DataRow s = dtS.Rows[0];
            o.Reference = SafeStr(s["session_title"]);
            litDocument.Text = HrDocument.Page(title, BuildBody(s, LoadCounts(), LoadRecords()), o);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("AppraisalSessionReport: " + ex);
            litDocument.Text = HrDocument.Page(title, HrDocument.Paragraph("The report could not be loaded. Refresh the page or try again later."), o);
        }
    }

    /// <summary>Every record of the session, without names: feeds the counts only.</summary>
    private DataTable LoadCounts()
    {
        return Q(
            @"SELECT ar.status, ar.staff_category, ar.final_percentage,
                     (IFNULL(ar.reviewer_id,0) > 0) AS has_supervisor
              FROM appraisal_records ar
              WHERE ar.session_id = @sid");
    }

    /// <summary>Roster rows: only appraisals that have reached HR.</summary>
    private DataTable LoadRecords()
    {
        const string order =
            @" AND ar.status IN ('COMPLETED','HR_REVIEWED')
               ORDER BY FIELD(ar.staff_category,'ACADEMIC','ADMINISTRATIVE','SUPPORT'), e.emp_name";
        try
        {
            return Q(
                @"SELECT ar.record_id, ar.status, ar.staff_category, ar.final_percentage, ar.classification,
                         IFNULL(e.emp_name, CONCAT('Staff record not found (ID ', ar.employee_id, ')')) AS emp_name, e.EMP_CODE,
                         IFNULL(NULLIF(ar.snap_department,''), IFNULL(d.dept_name,'')) AS department,
                         IFNULL(rev.emp_name,'') AS supervisor
                  FROM appraisal_records ar
                  LEFT JOIN hrm_employee e ON e.empID = ar.employee_id
                  LEFT JOIN hrm_emp_contracts c ON c.ID = (
                      SELECT c2.ID FROM hrm_emp_contracts c2 WHERE c2.empID = e.empID
                      ORDER BY (CASE WHEN c2.contractStatus='VALID' THEN 0 ELSE 1 END), c2.contractStart DESC LIMIT 1)
                  LEFT JOIN hrm_departments d ON d.ID = c.departmentID
                  LEFT JOIN hrm_employee rev ON rev.empID = ar.reviewer_id
                  WHERE ar.session_id = @sid" + order);
        }
        catch
        {
            return Q(
                @"SELECT ar.record_id, ar.status, ar.staff_category, ar.final_percentage, ar.classification,
                         IFNULL(e.emp_name, CONCAT('Staff record not found (ID ', ar.employee_id, ')')) AS emp_name, e.EMP_CODE, IFNULL(ar.snap_department,'') AS department,
                         IFNULL(rev.emp_name,'') AS supervisor
                  FROM appraisal_records ar
                  LEFT JOIN hrm_employee e ON e.empID = ar.employee_id
                  LEFT JOIN hrm_employee rev ON rev.empID = ar.reviewer_id
                  WHERE ar.session_id = @sid" + order);
        }
    }

    private class Agg
    {
        public int Total, Submitted, Completed, HrReviewed, Cancelled, Scored;
        public decimal Sum;
        public Dictionary<string, int> Status = new Dictionary<string, int>();
        public Dictionary<string, int> Band = new Dictionary<string, int>();
    }

    private static bool IsDone(string st) { return st == "COMPLETED" || st == "HR_REVIEWED"; }

    private static void Add(Agg a, string st, object pct)
    {
        a.Total++;
        if (!a.Status.ContainsKey(st)) a.Status[st] = 0;
        a.Status[st]++;
        if (st == "EMPLOYEE_SUBMITTED" || st == "SUPERVISOR_IN_PROGRESS" || IsDone(st)) a.Submitted++;
        if (IsDone(st)) a.Completed++;
        if (st == "HR_REVIEWED") a.HrReviewed++;
        if (st == "CANCELLED") a.Cancelled++;
        decimal d;
        if (IsDone(st) && pct != null && pct != DBNull.Value && decimal.TryParse(pct.ToString(), out d))
        {
            a.Scored++; a.Sum += d;
            string b = Classify(d);
            if (!a.Band.ContainsKey(b)) a.Band[b] = 0;
            a.Band[b]++;
        }
    }

    private string BuildBody(DataRow s, DataTable counts, DataTable recs)
    {
        Agg all = new Agg();
        Dictionary<string, Agg> byCat = new Dictionary<string, Agg>();
        List<string> stepOrder = new List<string>();
        Dictionary<string, int> steps = new Dictionary<string, int>();
        foreach (DataRow r in counts.Rows)
        {
            string st = SafeStr(r["status"]).ToUpperInvariant();
            string cat = SafeStr(r["staff_category"]).ToUpperInvariant();
            Add(all, st, r["final_percentage"]);
            if (!byCat.ContainsKey(cat)) byCat[cat] = new Agg();
            Add(byCat[cat], st, r["final_percentage"]);
            if (st == "HR_REVIEWED" || st == "CANCELLED") continue;
            string step = NextStep(st, SafeStr(r["has_supervisor"]) == "1");
            if (step == "") continue;
            if (!steps.ContainsKey(step)) { steps[step] = 0; stepOrder.Add(step); }
            steps[step]++;
        }
        Dictionary<string, int> rosterByCat = new Dictionary<string, int>();
        foreach (DataRow r in recs.Rows)
        {
            string cat = SafeStr(r["staff_category"]).ToUpperInvariant();
            if (!rosterByCat.ContainsKey(cat)) rosterByCat[cat] = 0;
            rosterByCat[cat]++;
        }

        StringBuilder h = new StringBuilder();

        // ── Session details ──
        h.Append(HrDocument.Heading("Session details"));
        List<string> cats = new List<string>();
        foreach (string c in SafeStr(s["target_categories"]).Split(','))
            if (c.Trim() != "") cats.Add(CategoryWord(c.Trim()));
        string created = SafeStr(s["created_by_name"]);
        h.Append(HrDocument.Meta(
            "Session", SafeStr(s["session_title"]),
            "Status", Word(SafeStr(s["status"])),
            "Appraisal period", Dt(s["period_start"]) + " to " + Dt(s["period_end"]),
            "Deadline", Dt(s["deadline"]),
            "Staff categories", string.Join(", ", cats.ToArray()),
            "Created", (created != "" ? created + ", " : "") + Dt(s["created_at"])));
        string desc = SafeStr(s["session_description"]).Trim();
        if (desc != "") h.Append(HrDocument.Paragraph(desc));

        // ── Summary: status and classification counts ──
        h.Append(HrDocument.Heading("Summary"));
        StringBuilder t = new StringBuilder("<table class=\"grid\"><thead><tr><th>Item</th><th class=\"n\">Count</th><th class=\"n\">Percent</th></tr></thead><tbody>");
        t.Append("<tr><td colspan=\"3\" style=\"font-weight:700;color:#05275C;background:#f5f7fa;\">Status (all appraisals)</td></tr>");
        foreach (string st in StatusOrder)
        {
            int n = all.Status.ContainsKey(st) ? all.Status[st] : 0;
            t.AppendFormat("<tr><td>{0}</td><td class=\"n\">{1}</td><td class=\"n\">{2}</td></tr>", StatusWord(st), n.ToString("N0"), Pct(n, all.Total));
        }
        t.AppendFormat("<tr><td><strong>Total appraisals</strong></td><td class=\"n\"><strong>{0}</strong></td><td class=\"n\"></td></tr>", all.Total.ToString("N0"));
        t.Append("<tr><td colspan=\"3\" style=\"font-weight:700;color:#05275C;background:#f5f7fa;\">Classification (scored appraisals completed by the supervisor)</td></tr>");
        foreach (string b in Bands)
        {
            int n = all.Band.ContainsKey(b) ? all.Band[b] : 0;
            t.AppendFormat("<tr><td>{0}</td><td class=\"n\">{1}</td><td class=\"n\">{2}</td></tr>", b, n.ToString("N0"), Pct(n, all.Scored));
        }
        t.AppendFormat("<tr><td><strong>Scored appraisals</strong></td><td class=\"n\"><strong>{0}</strong></td><td class=\"n\"></td></tr>", all.Scored.ToString("N0"));
        t.AppendFormat("<tr><td>Average score %</td><td class=\"n\">{0}</td><td class=\"n\"></td></tr>", all.Scored > 0 ? (all.Sum / all.Scored).ToString("0.0", CultureInfo.InvariantCulture) : "");
        t.Append("</tbody></table>");
        h.Append(t.ToString());

        // ── Category breakdown ──
        h.Append(HrDocument.Heading("By staff category"));
        List<string[]> rows = new List<string[]>();
        foreach (string c in Categories)
        {
            if (!byCat.ContainsKey(c)) continue;
            rows.Add(CatRow(CategoryWord(c), byCat[c]));
        }
        foreach (KeyValuePair<string, Agg> kv in byCat)
            if (Array.IndexOf(Categories, kv.Key) < 0) rows.Add(CatRow(CategoryWord(kv.Key), kv.Value));
        string[] tot = CatRow("Total", all);
        h.Append(HrDocument.Table(
            new string[] { "Category", "Appraisals", "Submitted", "Completed", "HR reviewed", "Cancelled", "Completion %", "Average score %" },
            rows, new bool[] { false, true, true, true, true, true, true, true }, tot));

        // ── Roster grouped by category ──
        h.Append("<div class=\"pb\"></div>");
        h.Append(HrDocument.Heading("Roster: appraisals that have reached HR"));
        StringBuilder rt = new StringBuilder("<table class=\"grid\"><thead><tr><th class=\"n\">No.</th><th>Staff number</th><th>Name</th><th>Department</th><th>Supervisor</th><th>Status</th><th class=\"n\">Score %</th><th>Classification</th></tr></thead><tbody>");
        if (recs.Rows.Count == 0) rt.Append("<tr><td colspan=\"8\" class=\"none\">None recorded.</td></tr>");
        string last = null;
        int no = 0;
        foreach (DataRow r in recs.Rows)
        {
            string cat = SafeStr(r["staff_category"]).ToUpperInvariant();
            if (cat != last)
            {
                int cnt = rosterByCat.ContainsKey(cat) ? rosterByCat[cat] : 0;
                rt.AppendFormat("<tr><td colspan=\"8\" style=\"font-weight:700;color:#05275C;background:#f5f7fa;\">{0} staff ({1})</td></tr>", CategoryWord(cat), cnt);
                last = cat; no = 0;
            }
            no++;
            string st = SafeStr(r["status"]).ToUpperInvariant();
            decimal d;
            bool scored = IsDone(st) && r["final_percentage"] != DBNull.Value && decimal.TryParse(r["final_percentage"].ToString(), out d);
            string pct = scored ? Convert.ToDecimal(r["final_percentage"]).ToString("0.0", CultureInfo.InvariantCulture) : "";
            rt.AppendFormat("<tr><td class=\"n\">{0}</td><td>{1}</td><td>{2}</td><td>{3}</td><td>{4}</td><td>{5}</td><td class=\"n\">{6}</td><td>{7}</td></tr>",
                no, HrDocument.E(SafeStr(r["EMP_CODE"])), HrDocument.E(SafeStr(r["emp_name"])), HrDocument.E(SafeStr(r["department"])),
                SafeStr(r["supervisor"]) != "" ? HrDocument.E(SafeStr(r["supervisor"])) : "Not assigned",
                StatusWord(st), pct, scored ? Classify(Convert.ToDecimal(r["final_percentage"])) : "");
        }
        rt.Append("</tbody></table>");
        h.Append(rt.ToString());

        // ── Outstanding items ──
        h.Append(HrDocument.Heading("Outstanding"));
        List<string[]> outRows = new List<string[]>();
        int outTotal = 0;
        foreach (string step in stepOrder)
        {
            outRows.Add(new string[] { HrDocument.E(step), steps[step].ToString("N0") });
            outTotal += steps[step];
        }
        h.Append(HrDocument.Table(new string[] { "Next step", "Appraisals" },
            outRows, new bool[] { false, true }, outRows.Count > 0 ? new string[] { "Total", outTotal.ToString("N0") } : null));

        // ── Sign-off ──
        h.Append(HrDocument.Heading("Sign-off"));
        h.Append(HrDocument.Signatures(
            "HR Manager", "", "",
            "Deputy Vice Chancellor (Academic Affairs)", "", "",
            "Vice Chancellor", "", ""));
        return h.ToString();
    }

    private static string[] CatRow(string label, Agg a)
    {
        return new string[] {
            HrDocument.E(label), a.Total.ToString("N0"), a.Submitted.ToString("N0"), a.Completed.ToString("N0"),
            a.HrReviewed.ToString("N0"), a.Cancelled.ToString("N0"),
            a.Total > 0 ? ((decimal)a.Completed * 100m / a.Total).ToString("0.0", CultureInfo.InvariantCulture) : "",
            a.Scored > 0 ? (a.Sum / a.Scored).ToString("0.0", CultureInfo.InvariantCulture) : "" };
    }

    private static string NextStep(string st, bool hasSupervisor)
    {
        switch (st)
        {
            case "PENDING":                return "Employee to start the appraisal";
            case "EMPLOYEE_IN_PROGRESS":   return "Employee to complete and submit";
            case "RETURNED":               return "Employee to revise and resubmit";
            case "EMPLOYEE_SUBMITTED":     return hasSupervisor ? "Supervisor to rate" : "HR to assign a supervisor";
            case "SUPERVISOR_IN_PROGRESS": return hasSupervisor ? "Supervisor to complete the rating" : "HR to assign a supervisor";
            case "COMPLETED":              return "HR to review";
            default:                       return "";
        }
    }

    // ── vocabulary and helpers ─────────────────────────────────────────
    private static string StatusWord(string s)
    {
        switch ((s ?? "").ToUpperInvariant())
        {
            case "PENDING":                return "Not started";
            case "EMPLOYEE_IN_PROGRESS":   return "In progress";
            case "EMPLOYEE_SUBMITTED":     return "Submitted";
            case "SUPERVISOR_IN_PROGRESS": return "With supervisor";
            case "RETURNED":               return "Returned";
            case "COMPLETED":              return "Awaiting HR";
            case "HR_REVIEWED":            return "HR reviewed";
            case "CANCELLED":              return "Cancelled";
            default:                       return HrDocument.E(s);
        }
    }

    private static string Classify(decimal d)
    {
        if (d >= 90) return "Exceptional";
        if (d >= 75) return "Above expectations";
        if (d >= 60) return "Satisfactory";
        if (d >= 50) return "Development needed";
        return "Unsatisfactory";
    }

    private static string CategoryWord(string c)
    {
        switch ((c ?? "").ToUpperInvariant())
        {
            case "ACADEMIC": return "Academic";
            case "ADMINISTRATIVE": return "Administrative";
            case "SUPPORT": return "Support";
            default: return c == "" ? "Not recorded" : Word(c);
        }
    }

    private static string Word(string s)
    {
        s = (s ?? "").Trim();
        return s.Length == 0 ? "" : s.Substring(0, 1).ToUpperInvariant() + s.Substring(1).ToLowerInvariant();
    }

    private static string Pct(int n, int of)
    {
        return of > 0 ? ((decimal)n * 100m / of).ToString("0.0", CultureInfo.InvariantCulture) : "";
    }

    private static string Dt(object v)
    {
        if (v == null || v == DBNull.Value) return "";
        DateTime d;
        return DateTime.TryParse(v.ToString(), out d) && d.Year > 1900 ? d.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : "";
    }

    private static string SafeStr(object o) { return (o == null || o == DBNull.Value) ? "" : o.ToString().Trim(); }

    private DataTable Q(string sql)
    {
        DataTable dt = new DataTable();
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@sid", QsSid);
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) { da.Fill(dt); }
            }
        }
        return dt;
    }
}
