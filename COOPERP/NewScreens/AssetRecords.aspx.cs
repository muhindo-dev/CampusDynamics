using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Web;
using System.Web.Services;

// Fixed Assets: asset records (plan 4.5). The ledger across the register, depreciation runs and year locks.
public partial class COOPERP_NewScreens_AssetRecords : System.Web.UI.Page
{
    protected string BootJson = "{}";

    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, FaAccess.Records);
        if (Request.HttpMethod == "POST" && !string.IsNullOrEmpty(Request.Form["faFormat"])) { Export(); return; }
        using (var c = FaDb.Open())
        {
            DateTime today = DateTime.Today;
            DateTime lastYearEnd = FaFinYear.Start(today).AddDays(-1);
            var boot = new Dictionary<string, object>();
            boot["rights"] = FaApi.RightsJson();
            boot["campuses"] = FaAssets.Campuses(c);
            boot["tree"] = FaCategories.Tree(c, false);
            boot["finYears"] = FaAssets.FinYears(c);
            boot["today"] = today.ToString("yyyy-MM-dd");
            boot["fyStart"] = FaFinYear.Start(today).ToString("yyyy-MM-dd");
            boot["lastYearEnd"] = lastYearEnd.ToString("yyyy-MM-dd");
            boot["lastMonthEnd"] = FaFinYear.MonthEnd(today.AddMonths(-1)).ToString("yyyy-MM-dd");
            boot["currentFinYear"] = FaFinYear.Of(today);
            var cols = new List<object>(); foreach (var col in FaReports.Find("ledger").Cols) cols.Add(new { k = col.Key, t = col.Header, on = col.On });
            boot["ledgerCols"] = cols;
            BootJson = FaJson.Ser(boot).Replace("<", "\\u003c");
        }
    }

    private void Export()
    {
        if (!MarksAntiForgeryService.ValidateRequest()) { Response.StatusCode = 403; Response.Write("Security check failed. Reload the page."); Response.End(); return; }
        string report = Request.Form["faReport"] ?? "ledger";
        if (report != "ledger" && report != "journal") report = "ledger";
        try
        {
            var d = FaReports.Run(report, FaJson.Parse(Request.Form["faConfig"]), Request.Form["faGroup"]);
            FaReports.Send(Response, d, Request.Form["faFormat"], Request.Form["faCols"]);
        }
        catch (Exception ex)
        {
            FaLog.Error("AssetRecords.Export", ex);
            Response.Clear(); Response.ContentType = "text/plain"; Response.Write("The export could not be produced. Go back and try again."); Response.End();
        }
    }

    [WebMethod(EnableSession = true)]
    public static string GetRecords(string configJson)
    {
        return FaApi.Read(FaAccess.Records, delegate
        {
            var f = FaJson.Parse(configJson);
            int page = Math.Max(1, FaJson.Int(f, "page")), size = 50;
            DateTime from = FaJson.Date(f, "from") ?? FaFinYear.Start(DateTime.Today), to = FaJson.Date(f, "to") ?? DateTime.Today;
            var w = new StringBuilder(" WHERE r.record_date BETWEEN @from AND @to");
            var prm = new List<object> { "@from", from, "@to", to };
            string type = FaJson.Str(f, "recordType").ToUpperInvariant();
            if (type != "" && System.Text.RegularExpressions.Regex.IsMatch(type, "^[A-Z]+$")) { w.Append(" AND r.record_type=@t"); prm.Add("@t"); prm.Add(type); }
            if (!FaJson.Bool(f, "includeReversals")) w.Append(" AND r.reversed_by_record_id IS NULL AND r.record_type<>'REVERSAL'");
            int campus = FaJson.Int(f, "campusId"), cat = FaJson.Int(f, "categoryId");
            if (campus > 0) { w.Append(" AND a.campus_id=@cp"); prm.Add("@cp"); prm.Add(campus); }
            if (cat > 0) { w.Append(" AND sc.parent_id=@ct"); prm.Add("@ct"); prm.Add(cat); }
            string q = FaJson.Str(f, "q");
            if (q != "") { w.Append(" AND (a.asset_no LIKE @q OR a.name LIKE @q OR r.reason LIKE @q OR r.reference LIKE @q OR r.recorded_by LIKE @q)"); prm.Add("@q"); prm.Add("%" + q + "%"); }
            const string from_ = " FROM fa_record r JOIN fa_asset a ON a.id=r.asset_id JOIN fa_category sc ON sc.id=a.category_id " +
                "LEFT JOIN acad_campuses fc ON fc.ID=r.from_campus_id LEFT JOIN acad_campuses tc ON tc.ID=r.to_campus_id " +
                "LEFT JOIN hrm_departments fd ON fd.ID=r.from_department_id LEFT JOIN hrm_departments td ON td.ID=r.to_department_id " +
                "LEFT JOIN hrm_employee fe ON fe.empID=r.from_custodian_emp_id LEFT JOIN hrm_employee te ON te.empID=r.to_custodian_emp_id";
            var rows = new List<object>();
            int total; var sums = new Dictionary<string, decimal>();
            using (var c = FaDb.Open())
            {
                total = FaDb.I(FaDb.Scalar(c, null, "SELECT COUNT(*)" + from_ + w, prm.ToArray()));
                foreach (DataRow s in FaDb.Table(c, null, "SELECT r.value_class, SUM(r.change_amount)" + from_ + w + " GROUP BY r.value_class", prm.ToArray()).Rows)
                    sums[FaDb.S(s[0])] = FaDb.M(s[1]);
                DataTable t = FaDb.Table(c, null,
                    "SELECT r.*, a.asset_no, a.name, IFNULL(fc.campus_name,'') from_campus, IFNULL(tc.campus_name,'') to_campus, IFNULL(fd.dept_name,'') from_dept, " +
                    "IFNULL(td.dept_name,'') to_dept, IFNULL(fe.emp_name,'') from_cus, IFNULL(te.emp_name,'') to_cus" + from_ + w +
                    " ORDER BY r.record_date DESC, r.id DESC LIMIT " + ((page - 1) * size) + "," + size, prm.ToArray());
                foreach (DataRow r in t.Rows)
                {
                    string ty = FaDb.S(r["record_type"]);
                    rows.Add(new
                    {
                        id = FaDb.L(r["id"]), assetId = FaDb.I(r["asset_id"]), assetNo = FaDb.S(r["asset_no"]), name = FaDb.S(r["name"]),
                        date = FaFmt.Date(r["record_date"]), finYear = FaDb.S(r["fin_year"]), type = ty, typeLabel = FaFmt.RecordType(ty),
                        details = FaRecords.Describe(r), valueRecord = FaDb.S(r["value_class"]) != "NONE",
                        before = FaDb.M(r["value_before"]), change = FaDb.M(r["change_amount"]), after = FaDb.M(r["value_after"]),
                        reason = FaDb.S(r["reason"]), reference = FaDb.S(r["reference"]), by = FaDb.S(r["recorded_by"]),
                        reversed = r["reversed_by_record_id"] != DBNull.Value, runId = FaDb.L(r["run_id"])
                    });
                }
            }
            decimal dep, rev, cost, dis;
            sums.TryGetValue("DEPRECIATION", out dep); sums.TryGetValue("REVALUATION", out rev); sums.TryGetValue("COST", out cost); sums.TryGetValue("DISPOSAL", out dis);
            return FaJson.Ser(new { success = true, rows = rows, total = total, page = page, size = size,
                                    totals = new { depreciation = -dep, revaluation = rev, additions = cost, disposals = -dis } });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string PreviewDepreciation(string json)
    {
        return FaApi.Read(FaAccess.Records, delegate
        {
            var d = FaJson.Parse(json);
            DateTime? pe = FaJson.Date(d, "periodEnd");
            if (!pe.HasValue) return FaJson.Fail("Choose the period end.");
            if (FaFinYear.MonthEnd(pe.Value) > FaFinYear.MonthEnd(DateTime.Today)) return FaJson.Fail("Depreciation cannot be charged for months that have not yet ended. Choose this month or earlier.");
            FaDepreciation.Plan p;
            using (var c = FaDb.Open()) p = FaDepreciation.Build(c, null, pe.Value, FaJson.Int(d, "campusId"), FaJson.Int(d, "categoryId"));
            return FaJson.Ser(FaDepreciation.PreviewJson(p, 500));
        });
    }

    [WebMethod(EnableSession = true)]
    public static string PostDepreciation(string json)
    {
        return FaApi.Write(FaAccess.Value, delegate
        {
            var d = FaJson.Parse(json);
            DateTime? pe = FaJson.Date(d, "periodEnd");
            if (!pe.HasValue) return FaJson.Fail("Choose the period end.");
            long runId; int count; decimal total;
            string e = FaDepreciation.Post(pe.Value, FaJson.Int(d, "campusId"), FaJson.Int(d, "categoryId"), FaJson.Str(d, "previewHash"),
                                           FaJson.Str(d, "notes"), FaJson.Str(d, "clientOpId"), out runId, out count, out total);
            if (e != null) return FaJson.Fail(e);
            return FaJson.Ser(new { success = true, runId = runId, count = count, total = total,
                                    message = "Run " + runId + " posted: " + FaFmt.Plural(count, "asset", "assets") + ", UGX " + FaFmt.Money(total) + "." });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string GetRuns()
    {
        return FaApi.Read(FaAccess.Records, delegate
        {
            List<object> l; using (var c = FaDb.Open()) l = FaDepreciation.Runs(c);
            return FaJson.Ser(new { success = true, rows = l });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string ReverseRun(long runId, string reason, string clientOpId)
    {
        return FaApi.Write(FaAccess.Value, delegate
        {
            string e = FaDepreciation.ReverseRun(runId, reason, clientOpId);
            return e == null ? FaJson.Ser(new { success = true }) : FaJson.Fail(e);
        });
    }

    [WebMethod(EnableSession = true)]
    public static string GetJournal(long runId, string finYear)
    {
        return FaApi.Read(FaAccess.Records, delegate
        {
            List<object> l; using (var c = FaDb.Open()) l = FaDepreciation.Journal(c, runId, finYear);
            return FaJson.Ser(new { success = true, lines = l });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string GetLocks()
    {
        return FaApi.Read(FaAccess.Records, delegate
        {
            List<object> l; using (var c = FaDb.Open()) l = FaDepreciation.Locks(c);
            return FaJson.Ser(new { success = true, rows = l });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string SetLock(string finYear, bool locked, string reason)
    {
        return FaApi.Write(FaAccess.YearLock, delegate
        {
            string e = FaDepreciation.SetLock(finYear, locked, reason);
            return e == null ? FaJson.Ser(new { success = true }) : FaJson.Fail(e);
        });
    }

    [WebMethod(EnableSession = true)]
    public static string CheckIntegrity()
    {
        return FaApi.Read(FaAccess.Value, delegate
        {
            var l = FaPosting.Integrity();
            return FaJson.Ser(new { success = true, problems = l });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string CountExport(string report, string configJson, string groupBy)
    {
        return FaApi.Read(FaAccess.Records, delegate
        {
            return FaJson.Ser(new { success = true, count = FaReports.Count(report == "journal" ? "journal" : "ledger", FaJson.Parse(configJson), groupBy) });
        });
    }
}
