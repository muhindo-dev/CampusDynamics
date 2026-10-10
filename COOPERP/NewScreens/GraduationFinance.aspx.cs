using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web.Script.Serialization;
using System.Web.Services;

// =====================================================================
//  Graduation Centre, Fees Clearance.
//
//  The Bursar's queue: every student the Academic Registry has put on a
//  graduation list, with what they owe right now, and the tools to settle
//  and clear them without leaving the page. All rules live in
//  App_Code/Graduation/GradFeesClearance.cs; this page only asks.
//
//  Every method re-checks the caller's rights. PageMethods do not run
//  Page_Load, so the page-level permission check alone would not protect
//  them.
// =====================================================================
public partial class COOPERP_NewScreens_GraduationFinance : System.Web.UI.Page
{
    private static readonly JavaScriptSerializer J = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
    private const int PAGE_SIZE = 100;
    private const int BULK_CAP = 300;
    private const int EXPORT_CAP = 20000;

    public string BootJson = "null";
    public string ColsJson = "[]";

    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, GradFeesClearance.SLUG);

        string fmt = Request.Form["gradExport"];
        GradFeesClearance.Rights rt = GradFeesClearance.GetRights();
        if (!string.IsNullOrEmpty(fmt))
        {
            if (rt.View) Export(fmt);
            return;
        }
        try
        {
            ColsJson = GraduationBootstrap.ForScriptBlock(J.Serialize(GraduationExport.Catalogue(Catalogue())));
            BootJson = rt.View ? GraduationBootstrap.ForScriptBlock(GradFeesClearance.Bootstrap(rt))
                               : GraduationBootstrap.ForScriptBlock(GradFeesClearance.Denied(null));
        }
        catch (Exception ex) { BootJson = GraduationBootstrap.ForScriptBlock(J.Serialize(new { success = false, message = ex.Message })); }
    }

    // =================================================================
    //  Filter
    // =================================================================
    private static GradFeesClearance.Filter Parse(string json)
    {
        var f = new GradFeesClearance.Filter();
        if (string.IsNullOrEmpty(json)) return f;
        try
        {
            var d = J.Deserialize<Dictionary<string, object>>(json);
            f.acadYear = GraduationBootstrap.S(d, "acadYear");
            f.faculty = GraduationBootstrap.S(d, "faculty");
            f.department = GraduationBootstrap.S(d, "department");
            f.programme = GraduationBootstrap.S(d, "programme");
            f.search = GraduationBootstrap.S(d, "search");
            f.status = GraduationBootstrap.S(d, "status");
            int n;
            if (int.TryParse(GraduationBootstrap.S(d, "page"), out n) && n > 0) f.page = n;
        }
        catch { }
        if (f.search.Length > 60) f.search = f.search.Substring(0, 60);
        return f;
    }

    // =================================================================
    //  Endpoints
    // =================================================================
    [WebMethod(EnableSession = true)]
    public static string GetBootstrap()
    {
        GradFeesClearance.Rights rt = GradFeesClearance.GetRights();
        if (!rt.View) return GradFeesClearance.Denied(null);
        try { return GradFeesClearance.Bootstrap(rt); }
        catch (Exception ex) { return J.Serialize(new { success = false, message = ex.Message }); }
    }

    [WebMethod(EnableSession = true)]
    public static string GetQueue(string configJson)
    {
        GradFeesClearance.Rights rt = GradFeesClearance.GetRights();
        if (!rt.View) return GradFeesClearance.Denied(null);
        try
        {
            GradFeesClearance.Filter f = Parse(configJson);
            GradFeesClearance.Settings st = GradFeesClearance.GetSettings();
            List<GradFeesClearance.Row> all = GradFeesClearance.Fetch(f, st);
            object kpis = GradFeesClearance.Kpis(all);
            List<GradFeesClearance.Row> rows = GradFeesClearance.Narrow(all, f.status);
            int total = rows.Count;
            int from = (f.page - 1) * PAGE_SIZE;
            if (from >= total && total > 0) { f.page = (total - 1) / PAGE_SIZE + 1; from = (f.page - 1) * PAGE_SIZE; }
            List<GradFeesClearance.Row> pageRows = rows.GetRange(Math.Max(0, from), Math.Max(0, Math.Min(PAGE_SIZE, total - from)));
            return J.Serialize(new { success = true, rows = pageRows, total, page = f.page, size = PAGE_SIZE, kpis,
                                     gated = st.IsGated(f.acadYear) });
        }
        catch (Exception ex) { return J.Serialize(new { success = false, message = ex.Message }); }
    }

    [WebMethod(EnableSession = true)]
    public static string GetStudent(string regno)
    {
        GradFeesClearance.Rights rt = GradFeesClearance.GetRights();
        if (!rt.View) return GradFeesClearance.Denied(null);
        try { return GradFeesClearance.Detail(rt, regno); }
        catch (Exception ex) { return J.Serialize(new { success = false, message = ex.Message }); }
    }

    /// <summary>What the reason dialog suggests, chosen from this student's own findings.</summary>
    [WebMethod(EnableSession = true)]
    public static string HoldReasons(string regno)
    {
        GradFeesClearance.Rights rt = GradFeesClearance.GetRights();
        if (!rt.View) return GradFeesClearance.Denied(null);
        var sug = new List<object>();
        try
        {
            GradFeesClearance.Settings st = GradFeesClearance.GetSettings();
            foreach (GradFeesClearance.Row r in GradFeesClearance.Fetch(new GradFeesClearance.Filter { search = (regno ?? "").Trim() }, st))
            {
                if (!string.Equals(r.regno, (regno ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                if (r.balance > 0)
                    sug.Add(new { text = "Outstanding balance of UGX " + GradFeesClearance.Money(r.balance) + ". Clear the balance at the Bursar's office before clearance.",
                                  why = "the account is not settled" });
                if (r.gradFeeRequired && !r.gradFeeBilled)
                    sug.Add(new { text = "Graduation Fee for the " + r.acadyear + " graduation not yet paid.", why = "no Graduation Fee on the account" });
                if (r.pendingReceipts > 0)
                    sug.Add(new { text = "Payment made through SchoolPay is still being confirmed. Clearance will follow once it posts.",
                                  why = r.pendingReceipts + " SchoolPay receipt(s) not yet posted" });
            }
        }
        catch { }
        sug.Add(new { text = "Sponsor's undertaking to pay the outstanding balance has been received and is on file.", why = "for an override" });
        sug.Add(new { text = "Agreed payment plan signed with the Bursar; balance to be settled before the ceremony.", why = "for an override" });
        sug.Add(new { text = "Student to present proof of payment (bank slip) for verification.", why = "payment claimed but not seen" });
        return J.Serialize(new { success = true, suggestions = sug });
    }

    [WebMethod(EnableSession = true)]
    public static string Decide(string regno, string verdict, string reason, bool asOverride)
    {
        GradFeesClearance.Rights rt = GradFeesClearance.GetRights();
        if (!rt.View) return GradFeesClearance.Denied(null);
        GradFeesClearance.Outcome o = GradFeesClearance.Decide(rt, regno, verdict, reason, asOverride, null);
        return J.Serialize(new { success = o.ok, message = o.message });
    }

    /// <summary>
    /// Clears every selected student whose account is settled. Anyone with something outstanding
    /// is skipped, never overridden, and the reason is returned per student.
    /// </summary>
    [WebMethod(EnableSession = true)]
    public static string BulkClear(List<string> regnos)
    {
        GradFeesClearance.Rights rt = GradFeesClearance.GetRights();
        if (!rt.Edit) return GradFeesClearance.Denied("You may view Fees Clearance but not clear students.");
        return Bulk(regnos, delegate (string r, string batch)
        { return GradFeesClearance.Decide(rt, r, "CLEARED", "", false, batch); });
    }

    [WebMethod(EnableSession = true)]
    public static string BulkGradFee(List<string> regnos, decimal amount)
    {
        GradFeesClearance.Rights rt = GradFeesClearance.GetRights();
        if (!rt.Edit) return GradFeesClearance.Denied("You may view Fees Clearance but not post bills.");
        return Bulk(regnos, delegate (string r, string batch)
        { return GradFeesClearance.BillGradFee(rt, r, amount, batch); });
    }

    private delegate GradFeesClearance.Outcome OneAction(string regno, string batch);

    private static string Bulk(List<string> regnos, OneAction act)
    {
        if (regnos == null || regnos.Count == 0) return J.Serialize(new { success = false, message = "Select at least one student." });
        if (regnos.Count > BULK_CAP) return J.Serialize(new { success = false, message = "At most " + BULK_CAP + " students at a time." });
        string batch = "GFB-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "-" +
                       Guid.NewGuid().ToString("N").Substring(0, 6);
        var done = new List<string>();
        var skipped = new List<object>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in regnos)
        {
            string r = (raw ?? "").Trim();
            if (r == "" || !seen.Add(r)) continue;
            GradFeesClearance.Outcome o;
            try { o = act(r, batch); }
            catch (Exception ex) { o = new GradFeesClearance.Outcome { regno = r, message = ex.Message }; }
            if (o.ok) done.Add(r); else skipped.Add(new { regno = r, why = o.message });
        }
        return J.Serialize(new { success = true, done = done.Count, doneIds = done, skipped, batch });
    }

    [WebMethod(EnableSession = true)]
    public static string BillItem(string regno, int itemCode, decimal amount, string acadYear, int semester, string detail)
    {
        GradFeesClearance.Rights rt = GradFeesClearance.GetRights();
        if (!rt.View) return GradFeesClearance.Denied(null);
        GradFeesClearance.Outcome o = GradFeesClearance.Bill(rt, regno, itemCode, amount, acadYear, semester, detail, null);
        return J.Serialize(new { success = o.ok, message = o.message });
    }

    [WebMethod(EnableSession = true)]
    public static string BillGradFee(string regno, decimal amount)
    {
        GradFeesClearance.Rights rt = GradFeesClearance.GetRights();
        if (!rt.View) return GradFeesClearance.Denied(null);
        GradFeesClearance.Outcome o = GradFeesClearance.BillGradFee(rt, regno, amount, null);
        return J.Serialize(new { success = o.ok, message = o.message });
    }

    [WebMethod(EnableSession = true)]
    public static string RecordPayment(string regno, string bankCode, decimal amount, string payDate,
                                       string reference, string payer, bool confirmSameDay)
    {
        GradFeesClearance.Rights rt = GradFeesClearance.GetRights();
        if (!rt.View) return GradFeesClearance.Denied(null);
        GradFeesClearance.Outcome o = GradFeesClearance.RecordPayment(rt, regno, bankCode, amount, payDate, reference, payer, confirmSameDay);
        bool sameDay = !o.ok && o.message.StartsWith("SAME_DAY|", StringComparison.Ordinal);
        return J.Serialize(new { success = o.ok, sameDay, message = sameDay ? o.message.Substring(9) : o.message });
    }

    [WebMethod(EnableSession = true)]
    public static string CountExport(string configJson)
    {
        GradFeesClearance.Rights rt = GradFeesClearance.GetRights();
        if (!rt.View) return GradFeesClearance.Denied(null);
        try
        {
            GradFeesClearance.Filter f = Parse(configJson);
            int n = GradFeesClearance.Narrow(GradFeesClearance.Fetch(f, GradFeesClearance.GetSettings()), f.status).Count;
            return J.Serialize(new { success = true, total = n, note = "", capped = n > EXPORT_CAP ? EXPORT_CAP : 0 });
        }
        catch (Exception ex) { return J.Serialize(new { success = false, message = ex.Message }); }
    }

    // =================================================================
    //  Export
    // =================================================================
    private const string ID = "Identity", FN = "Finance", DC = "The decision";

    private static List<GraduationExport.Col<GradFeesClearance.Row>> Catalogue()
    {
        var c = new List<GraduationExport.Col<GradFeesClearance.Row>>();
        c.Add(new GraduationExport.Col<GradFeesClearance.Row>("seq", "#", ID, true, r => r.seq.ToString(CultureInfo.InvariantCulture)));
        c.Add(new GraduationExport.Col<GradFeesClearance.Row>("regno", "Student Number", ID, r => r.regno));
        c.Add(new GraduationExport.Col<GradFeesClearance.Row>("name", "Name", ID, r => r.name));
        c.Add(new GraduationExport.Col<GradFeesClearance.Row>("progcode", "Programme Code", ID, r => r.progcode));
        c.Add(new GraduationExport.Col<GradFeesClearance.Row>("progname", "Programme", ID, r => r.progname));
        c.Add(new GraduationExport.Col<GradFeesClearance.Row>("faculty", "Faculty", ID, r => r.faculty).Off());
        c.Add(new GraduationExport.Col<GradFeesClearance.Row>("year", "Graduation Year", ID, r => r.acadyear));

        c.Add(new GraduationExport.Col<GradFeesClearance.Row>("status", "Finance Clearance", FN, r => StatusWord(r)));
        c.Add(new GraduationExport.Col<GradFeesClearance.Row>("balance", "Balance (UGX, + owes)", FN, true,
              r => Math.Round(r.balance).ToString("0", CultureInfo.InvariantCulture)));
        c.Add(new GraduationExport.Col<GradFeesClearance.Row>("gradfee", "Graduation Fee Billed", FN,
              r => !r.gradFeeRequired ? "Not required" : (r.gradFeeBilled ? "Yes" : "No")));
        c.Add(new GraduationExport.Col<GradFeesClearance.Row>("findings", "Outstanding Checks", FN, r =>
        {
            var sb = new StringBuilder();
            foreach (GradFeesClearance.Finding fd in r.findings)
            { if (fd.level == "INFO") continue; if (sb.Length > 0) sb.Append("; "); sb.Append(fd.text); }
            return sb.ToString();
        }));

        c.Add(new GraduationExport.Col<GradFeesClearance.Row>("basis", "Basis", DC, r => BasisWord(r.basis)));
        c.Add(new GraduationExport.Col<GradFeesClearance.Row>("by", "Decided By", DC, r => r.actor));
        c.Add(new GraduationExport.Col<GradFeesClearance.Row>("on", "Decided On", DC, r => r.decidedAt));
        c.Add(new GraduationExport.Col<GradFeesClearance.Row>("reason", "Reason / Note", DC, r => r.reason));
        return c;
    }

    private static string StatusWord(GradFeesClearance.Row r)
    {
        if (r.status == "CLEARED") return r.owesSinceClearance ? "Cleared (now owes again)" : "Cleared";
        if (r.status == "HELD") return "On finance hold";
        return r.eligible ? "Pending (ready to clear)" : "Pending";
    }

    private static string BasisWord(string b)
    {
        switch (b)
        {
            case "NO_BALANCE": return "Nothing owed";
            case "IN_CREDIT": return "In credit";
            case "OVERRIDE": return "Override";
            default: return "";
        }
    }

    private void Export(string fmt)
    {
        GradFeesClearance.Filter f = Parse(Request.Form["gradConfig"] ?? "");
        List<GradFeesClearance.Row> rows = GradFeesClearance.Narrow(GradFeesClearance.Fetch(f, GradFeesClearance.GetSettings()), f.status);
        bool truncated = rows.Count > EXPORT_CAP;
        if (truncated) rows.RemoveRange(EXPORT_CAP, rows.Count - EXPORT_CAP);
        string lastProg = null; int n = 0;
        foreach (GradFeesClearance.Row r in rows)
        {
            string key = r.progname == "" ? r.progcode : r.progname;
            if (key != lastProg) { lastProg = key; n = 0; }
            r.seq = ++n;
        }
        GraduationExport.Truncation = truncated ? "More than " + EXPORT_CAP + " names matched. This file carries the first " + EXPORT_CAP + "." : null;
        GraduationExport.Sheet sheet = GraduationExport.Build("Fees clearance", f.acadYear == "" ? "all years" : f.acadYear,
                                                              Catalogue(), rows, Request.Form["gradCols"]);
        var sheets = new List<GraduationExport.Sheet> { sheet };
        if (GraduationExport.Wants(Request.Form["gradSheets"] ?? "", "summary")) sheets.Add(Summary(rows));

        var cover = new List<KeyValuePair<string, string>>();
        cover.Add(new KeyValuePair<string, string>("Graduation year", f.acadYear == "" ? "All years" : f.acadYear));
        cover.Add(new KeyValuePair<string, string>("Showing", StatusFilterWord(f.status)));
        if (f.search != "") cover.Add(new KeyValuePair<string, string>("Search", f.search));
        cover.Add(new KeyValuePair<string, string>("Names", rows.Count.ToString(CultureInfo.InvariantCulture)));
        cover.Add(new KeyValuePair<string, string>("Balances", "Live at the moment of export: billed minus paid; a positive figure is owed to the University"));
        string file = GraduationExport.FileName("fees-clearance", f.acadYear);
        string title = "Graduation Fees Clearance";
        try
        {
            if (fmt == "csv") GraduationExport.Csv(Response, file, title, "All faculties", cover, sheet.Columns, sheet.Rows);
            else if (fmt == "xls") GraduationExport.Workbook(Response, file, title, "All faculties", cover, sheets);
            else
            {
                var groups = new List<string>();
                foreach (GradFeesClearance.Row r in rows) groups.Add(r.progname == "" ? r.progcode : r.progname + "   (" + r.progcode + ")");
                GraduationExport.Sheet pdf = GraduationExport.WithoutGroupColumns(sheet, "prog");
                GraduationPdf.Send(Response, file, title, f.acadYear, "All faculties", cover,
                                   GraduationExport.PdfCols(pdf), GraduationExport.ToTable(pdf, groups),
                                   GraduationExport.GROUP_COL, true, GraduationExport.Truncation);
            }
        }
        finally { GraduationExport.Truncation = null; }
    }

    private static string StatusFilterWord(string s)
    {
        switch (s ?? "")
        {
            case "pending": return "Not yet decided by Finance";
            case "ready": return "Pending and ready to clear";
            case "blocked": return "Something outstanding";
            case "cleared": return "Cleared by Finance";
            case "held": return "On finance hold";
            case "owing": return "Owing money";
            case "drift": return "Cleared but owing again";
            case "nofee": return "Graduation Fee not billed";
            default: return "Everyone on the list";
        }
    }

    private static GraduationExport.Sheet Summary(List<GradFeesClearance.Row> rows)
    {
        var sh = new GraduationExport.Sheet();
        sh.Name = "By programme";
        sh.Columns = new[] { "Programme", "Graduands", "Cleared", "Pending", "On hold", "Owing", "Total owed (UGX)" };
        for (int i = 1; i <= 6; i++) sh.NumericColumns.Add(i);
        var order = new List<string>();
        var t = new Dictionary<string, decimal[]>();
        foreach (GradFeesClearance.Row r in rows)
        {
            string k = r.progname == "" ? r.progcode : r.progname;
            if (!t.ContainsKey(k)) { t[k] = new decimal[6]; order.Add(k); }
            decimal[] a = t[k];
            a[0]++;
            if (r.status == "CLEARED") a[1]++; else if (r.status == "HELD") a[3]++; else a[2]++;
            if (r.balance > 0) { a[4]++; a[5] += r.balance; }
        }
        order.Sort(StringComparer.OrdinalIgnoreCase);
        decimal[] tot = new decimal[6];
        foreach (string k in order)
        {
            decimal[] a = t[k];
            for (int i = 0; i < 6; i++) tot[i] += a[i];
            sh.Rows.Add(new[] { k, a[0].ToString("0"), a[1].ToString("0"), a[2].ToString("0"), a[3].ToString("0"), a[4].ToString("0"), Math.Round(a[5]).ToString("0") });
        }
        sh.Rows.Add(new[] { "Total", tot[0].ToString("0"), tot[1].ToString("0"), tot[2].ToString("0"), tot[3].ToString("0"), tot[4].ToString("0"), Math.Round(tot[5]).ToString("0") });
        return sh;
    }
}
