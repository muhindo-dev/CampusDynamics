using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

// =====================================================================
//  Fixed Assets: reports (plan section 7).
//  Each report is declared once: its filters, groupings and columns.
//  The same definition drives the export dialog, the Excel and CSV
//  files and the PDF, so the formats cannot disagree.
// =====================================================================
public class FaCol
{
    public string Key, Header; public bool Numeric, Sum, On = true, Blank; public float Width;
    public FaCol(string key, string header, float width) { Key = key; Header = header; Width = width; }
    public FaCol Num() { Numeric = true; Sum = true; return this; }
    public FaCol Count() { Numeric = true; return this; }
    public FaCol Off() { On = false; return this; }
    public FaCol Empty() { Blank = true; return this; }
}

public class FaReportData
{
    public string Key, Title, Subtitle, FileWhat;
    public List<KeyValuePair<string, string>> Cover = new List<KeyValuePair<string, string>>();
    public List<FaCol> Cols = new List<FaCol>();
    public List<object[]> Rows = new List<object[]>();
    public List<string> Groups;          // one per row when grouped
    public bool PageBreakPerGroup, ShowTotals = true;
    public string GroupFooterNote, FootNote, Noun = "asset", NounPlural = "assets";
    public string[] Signatories;
    public float RowHeight = 15f;
    public bool? Landscape;
}

public static class FaReports
{
    public class Def
    {
        public string Key, Title, Description;
        public string[] Filters;     // campus, category, department, custodian, status, asOf, period, finYear, asset, run, recordType, search
        public string[] Groups;      // keys of allowed groupings, first is the default
        public List<FaCol> Cols;
    }

    private static readonly Dictionary<string, string> GroupLabels = new Dictionary<string, string> {
        { "category", "Category" }, { "subcategory", "Sub-category" }, { "campus", "Campus" }, { "department", "Department" },
        { "custodian", "Responsible person" }, { "status", "Status" }, { "location", "Campus and building" }, { "method", "Disposal method" },
        { "type", "Record type" }, { "none", "No grouping" } };

    public static List<Def> Catalogue()
    {
        var l = new List<Def>();
        l.Add(new Def { Key = "register", Title = "Fixed Asset Register", Description = "Every asset with its values and custodian, as at a date.",
            Filters = new[] { "campus", "category", "department", "custodian", "status", "asOf", "search" },
            Groups = new[] { "category", "subcategory", "campus", "department", "custodian", "status", "none" },
            Cols = new List<FaCol> { new FaCol("assetNo", "Asset no", 112), new FaCol("tag", "Tag", 60).Off(), new FaCol("name", "Name", 150),
                new FaCol("serial", "Serial no", 80).Off(), new FaCol("makeModel", "Make and model", 90).Off(), new FaCol("category", "Category", 90).Off(),
                new FaCol("subcategory", "Sub-category", 90), new FaCol("campus", "Campus", 60).Off(), new FaCol("location", "Location", 110),
                new FaCol("department", "Department", 100), new FaCol("custodian", "Responsible person", 100), new FaCol("purchaseDate", "Purchased", 68),
                new FaCol("supplier", "Supplier", 90).Off(), new FaCol("invoice", "Invoice", 60).Off(), new FaCol("cost", "Cost", 72).Num(),
                new FaCol("accum", "Accumulated depreciation", 72).Num(), new FaCol("value", "Book value", 72).Num(), new FaCol("status", "Status", 55) } });
        l.Add(new Def { Key = "by-category", Title = "Assets by Category and Sub-category", Description = "Counts and values per sub-category with category totals.",
            Filters = new[] { "campus", "category", "department", "status" }, Groups = new[] { "category" },
            Cols = new List<FaCol> { new FaCol("subcategory", "Sub-category", 170), new FaCol("count", "Assets", 50).Count(), new FaCol("cost", "Cost", 90).Num(),
                new FaCol("accum", "Accumulated depreciation", 90).Num(), new FaCol("value", "Book value", 90).Num(), new FaCol("share", "Share of value", 60) } });
        l.Add(new Def { Key = "by-location", Title = "Assets by Campus and Department", Description = "Counts and values per department on each campus.",
            Filters = new[] { "campus", "category", "department", "status" }, Groups = new[] { "campus" },
            Cols = new List<FaCol> { new FaCol("department", "Department", 190), new FaCol("count", "Assets", 50).Count(), new FaCol("cost", "Cost", 90).Num(),
                new FaCol("accum", "Accumulated depreciation", 90).Num(), new FaCol("value", "Book value", 90).Num() } });
        l.Add(new Def { Key = "custody", Title = "Custody List", Description = "Assets per responsible person, one signable page each.",
            Filters = new[] { "campus", "category", "department", "custodian", "status" }, Groups = new[] { "custodian" },
            Cols = new List<FaCol> { new FaCol("assetNo", "Asset no", 112), new FaCol("tag", "Tag", 65), new FaCol("name", "Name", 170),
                new FaCol("serial", "Serial no", 90), new FaCol("location", "Location", 120), new FaCol("since", "Assigned", 65),
                new FaCol("value", "Book value", 75).Num().Off() } });
        l.Add(new Def { Key = "depreciation", Title = "Depreciation Schedule", Description = "Opening value, additions, depreciation, revaluation, disposals and closing value for a financial year.",
            Filters = new[] { "finYear", "campus", "category", "department" }, Groups = new[] { "category", "subcategory", "campus", "none" },
            Cols = new List<FaCol> { new FaCol("assetNo", "Asset no", 108), new FaCol("name", "Name", 140), new FaCol("subcategory", "Sub-category", 90).Off(),
                new FaCol("method", "Method", 70), new FaCol("opening", "Opening value", 72).Num(), new FaCol("brought", "Brought into register", 66).Num(),
                new FaCol("additions", "Additions", 66).Num(),
                new FaCol("depreciation", "Depreciation", 66).Num(), new FaCol("revaluation", "Revaluation", 66).Num(),
                new FaCol("disposals", "Disposals", 66).Num(), new FaCol("closing", "Closing value", 72).Num() } });
        l.Add(new Def { Key = "revaluations", Title = "Revaluation and Appreciation Report", Description = "Revaluations and appreciations recorded in a period.",
            Filters = new[] { "period", "campus", "category" }, Groups = new[] { "category", "none" },
            Cols = new List<FaCol> { new FaCol("date", "Date", 66), new FaCol("assetNo", "Asset no", 108), new FaCol("name", "Name", 130), new FaCol("type", "Type", 65),
                new FaCol("before", "Value before", 72).Num(), new FaCol("after", "New value", 72).Num(), new FaCol("change", "Change", 70).Num(),
                new FaCol("valuer", "Valuer", 90), new FaCol("reference", "Reference", 80), new FaCol("reason", "Reason", 140).Off(), new FaCol("by", "Recorded by", 70).Off() } });
        l.Add(new Def { Key = "disposals", Title = "Disposals Report", Description = "Assets disposed of or written off in a period, with gain or loss.",
            Filters = new[] { "period", "campus", "category" }, Groups = new[] { "method", "category", "none" },
            Cols = new List<FaCol> { new FaCol("date", "Date", 66), new FaCol("assetNo", "Asset no", 108), new FaCol("name", "Name", 130), new FaCol("category", "Category", 90).Off(),
                new FaCol("method", "Method", 70), new FaCol("carrying", "Carrying amount", 76).Num(), new FaCol("proceeds", "Proceeds", 70).Num(),
                new FaCol("gainLoss", "Gain or loss", 70).Num(), new FaCol("approval", "Approval reference", 90), new FaCol("reason", "Reason", 140).Off(), new FaCol("by", "Approved by", 70) } });
        l.Add(new Def { Key = "movements", Title = "Asset Movement Report", Description = "Transfers of campus, location, department or responsible person in a period.",
            Filters = new[] { "period", "campus", "category" }, Groups = new[] { "none", "campus" },
            Cols = new List<FaCol> { new FaCol("date", "Date", 66), new FaCol("assetNo", "Asset no", 108), new FaCol("name", "Name", 120),
                new FaCol("from", "From", 160), new FaCol("to", "To", 160), new FaCol("reason", "Reason", 120), new FaCol("by", "Recorded by", 70) } });
        l.Add(new Def { Key = "history", Title = "Asset History Statement", Description = "The full ledger of one asset.",
            Filters = new[] { "asset" }, Groups = new[] { "none" },
            Cols = new List<FaCol> { new FaCol("date", "Date", 60), new FaCol("type", "Type", 80), new FaCol("details", "Details", 190),
                new FaCol("before", "Before", 70).Num(), new FaCol("change", "Change", 70).Num(), new FaCol("after", "After", 70).Num(),
                new FaCol("reason", "Reason", 120), new FaCol("reference", "Reference", 70).Off(), new FaCol("by", "Recorded by", 70) } });
        l.Add(new Def { Key = "verification", Title = "Verification Sheet", Description = "Printable count sheet per location with columns to tick.",
            Filters = new[] { "campus", "category", "department", "custodian" }, Groups = new[] { "location", "department", "custodian" },
            Cols = new List<FaCol> { new FaCol("assetNo", "Asset no", 112), new FaCol("tag", "Tag", 60), new FaCol("name", "Name", 150), new FaCol("serial", "Serial no", 80),
                new FaCol("location", "Recorded location", 100), new FaCol("custodian", "Responsible person", 95),
                new FaCol("found", "Found", 40).Empty(), new FaCol("condition", "Condition", 60).Empty(), new FaCol("remarks", "Remarks", 110).Empty() } });
        l.Add(new Def { Key = "reconciliation", Title = "Register to General Ledger Reconciliation", Description = "Register values against general ledger balances per account.",
            Filters = new[] { "asOf" }, Groups = new[] { "none" },
            Cols = new List<FaCol> { new FaCol("account", "Account", 60), new FaCol("accountName", "Account name", 170), new FaCol("kind", "Kind", 90),
                new FaCol("register", "Register", 90).Num(), new FaCol("ledger", "General ledger", 90).Num(), new FaCol("difference", "Difference", 90).Num() } });
        l.Add(new Def { Key = "journal", Title = "Depreciation Journal Summary", Description = "Debits and credits per account for a run or a financial year, for posting by Finance.",
            Filters = new[] { "finYear", "run" }, Groups = new[] { "none" },
            Cols = new List<FaCol> { new FaCol("account", "Account", 70), new FaCol("accountName", "Account name", 180), new FaCol("note", "Covers", 150),
                new FaCol("debit", "Debit", 90).Num(), new FaCol("credit", "Credit", 90).Num() } });
        l.Add(new Def { Key = "ledger", Title = "Asset Records Ledger", Description = "Every record posted in a period, across the register.",
            Filters = new[] { "period", "campus", "category", "recordType", "search" }, Groups = new[] { "none", "type", "category" },
            Cols = new List<FaCol> { new FaCol("date", "Date", 66), new FaCol("assetNo", "Asset no", 108), new FaCol("name", "Name", 120), new FaCol("type", "Type", 75),
                new FaCol("details", "Details", 150), new FaCol("before", "Before", 70).Num(), new FaCol("change", "Change", 70).Num(), new FaCol("after", "After", 70).Num(),
                new FaCol("reason", "Reason", 120).Off(), new FaCol("by", "Recorded by", 70) } });
        return l;
    }

    public static Def Find(string key)
    {
        foreach (var d in Catalogue()) if (d.Key == key) return d;
        return null;
    }

    public static object CatalogueJson()
    {
        var l = new List<object>();
        foreach (var d in Catalogue())
        {
            var cols = new List<object>(); foreach (var c in d.Cols) if (!c.Blank) cols.Add(new { k = c.Key, t = c.Header, on = c.On });
            var groups = new List<object>(); foreach (string g in d.Groups) groups.Add(new { k = g, t = GroupLabels[g] });
            l.Add(new { key = d.Key, title = d.Title, description = d.Description, filters = d.Filters, groups = groups, cols = cols });
        }
        return l;
    }

    // ─────────────────────────── Filters and cover ───────────────────────────

    private static DateTime Period(Dictionary<string, object> f, string k, DateTime dflt) { return FaJson.Date(f, k) ?? dflt; }

    private static void Cover(MySqlConnection c, FaReportData d, Dictionary<string, object> f)
    {
        int campus = FaJson.Int(f, "campusId"), cat = FaJson.Int(f, "categoryId"), sub = FaJson.Int(f, "subCategoryId");
        int dept = FaJson.Int(f, "departmentId"), cus = FaJson.Int(f, "custodianEmpId");
        d.Cover.Add(new KeyValuePair<string, string>("Campus", campus > 0 ? FaAssets.Title(FaDb.S(FaDb.Scalar(c, null, "SELECT campus_name FROM acad_campuses WHERE ID=@i", "@i", campus))) : "All campuses"));
        if (sub > 0) d.Cover.Add(new KeyValuePair<string, string>("Category", FaDb.S(FaDb.Scalar(c, null,
            "SELECT CONCAT(IFNULL(p.name,''),' / ',s.name) FROM fa_category s LEFT JOIN fa_category p ON p.id=s.parent_id WHERE s.id=@i", "@i", sub))));
        else d.Cover.Add(new KeyValuePair<string, string>("Category", cat > 0 ? FaDb.S(FaDb.Scalar(c, null, "SELECT name FROM fa_category WHERE id=@i", "@i", cat)) : "All categories"));
        if (dept > 0) d.Cover.Add(new KeyValuePair<string, string>("Department", FaAssets.Title(FaDb.S(FaDb.Scalar(c, null, "SELECT dept_name FROM hrm_departments WHERE ID=@i", "@i", dept)))));
        if (cus > 0) d.Cover.Add(new KeyValuePair<string, string>("Responsible person", FaDb.S(FaDb.Scalar(c, null, "SELECT emp_name FROM hrm_employee WHERE empID=@i", "@i", cus))));
        string st = FaJson.Str(f, "status").ToUpperInvariant();
        if (st != "") d.Cover.Add(new KeyValuePair<string, string>("Status", st == "OPEN" ? "Assets still held" : st == "CLOSED" ? "Disposed and written off" : st == "ALL" ? "All, including void" : FaFmt.Status(st)));
        if (FaJson.Str(f, "q") != "") d.Cover.Add(new KeyValuePair<string, string>("Search", FaJson.Str(f, "q")));
        string flag = FaJson.Str(f, "flag");
        if (flag != "") d.Cover.Add(new KeyValuePair<string, string>("List", FlagLabel(flag)));
        if (FaJson.IntList(f, "ids").Count > 0) d.Cover.Add(new KeyValuePair<string, string>("Selection", FaFmt.Plural(FaJson.IntList(f, "ids").Count, "selected asset", "selected assets")));
    }

    public static string FlagLabel(string flag)
    {
        switch (flag)
        {
            case "no_custodian": return "No responsible person";
            case "not_tagged": return "Not yet tagged";
            case "revaluation_due": return "Revaluation due";
            case "life_ended": return "Useful life ended";
            case "not_verified": return "Not verified in 12 months";
            case "warranty_soon": return "Warranty ends within 90 days";
            default: return flag;
        }
    }

    // ─────────────────────────── Running a report ───────────────────────────

    public static FaReportData Run(string key, Dictionary<string, object> f, string groupBy)
    {
        Def def = Find(key);
        if (def == null) throw new ArgumentException("Unknown report.");
        if (string.IsNullOrEmpty(groupBy) || Array.IndexOf(def.Groups, groupBy) < 0) groupBy = def.Groups[0];
        var d = new FaReportData();
        d.Key = key; d.Title = def.Title; d.FileWhat = key; d.Cols = def.Cols;
        d.Signatories = new[] { "Prepared by (Assets Officer)", "Checked by (Accountant)", "Approved by (University Bursar)" };
        using (var c = FaDb.Open())
        {
            switch (key)
            {
                case "register": Register(c, d, f, groupBy); break;
                case "by-category": ByCategory(c, d, f); break;
                case "by-location": ByLocation(c, d, f); break;
                case "custody": Custody(c, d, f); break;
                case "depreciation": Depreciation(c, d, f, groupBy); break;
                case "revaluations": Records(c, d, f, "'REVALUATION','APPRECIATION'", groupBy); break;
                case "disposals": Records(c, d, f, "'DISPOSAL'", groupBy); break;
                case "movements": Records(c, d, f, "'TRANSFER'", groupBy); break;
                case "ledger": Records(c, d, f, null, groupBy); break;
                case "history": History(c, d, f); break;
                case "verification": Verification(c, d, f, groupBy); break;
                case "reconciliation": Reconciliation(c, d, f); break;
                case "journal": Journal(c, d, f); break;
            }
        }
        return d;
    }

    private static string Group(DataRow r, string groupBy)
    {
        switch (groupBy)
        {
            case "category": return FaDb.S(r["cat_name"]);
            case "subcategory": return FaDb.S(r["cat_name"]) + " / " + FaDb.S(r["sub_name"]);
            case "campus": return FaAssets.Title(FaDb.S(r["campus_name"]));
            case "department": { string s = FaAssets.Title(FaDb.S(r["dept_name"])); return s == "" ? "No department" : s; }
            case "custodian":
            {
                string n = FaDb.S(r["custodian_name"]);
                if (n == "") return "No responsible person";
                string code = FaDb.S(r["custodian_code"]);
                return n + (code != "" ? " (" + code + ")" : "");
            }
            case "status": return FaFmt.Status(FaDb.S(r["status"]));
            case "location":
            {
                string s = FaAssets.Title(FaDb.S(r["campus_name"]));
                string b = FaDb.S(r["building"]);
                return s + (b != "" ? ", " + b : ", building not recorded");
            }
        }
        return "";
    }

    private static void Register(MySqlConnection c, FaReportData d, Dictionary<string, object> f, string groupBy)
    {
        DateTime? asOf = FaJson.Date(f, "asOf");
        bool historic = asOf.HasValue && asOf.Value < DateTime.Today;
        var prm = new List<object>();
        string where = FaAssets.Where(f, prm);
        string valueCol = "a.current_value", accumCol = "a.accum_depreciation";
        if (historic)
        {
            where += " AND a.purchase_date<=@asof AND (a.closed_on IS NULL OR a.closed_on>@asof)";
            prm.Add("@asof"); prm.Add(asOf.Value);
            valueCol = "IFNULL((SELECT x.value_after FROM fa_record x WHERE x.asset_id=a.id AND x.value_class<>'NONE' AND x.record_date<=@asof ORDER BY x.record_date DESC, x.id DESC LIMIT 1),0)";
            accumCol = "IFNULL((SELECT -SUM(x.change_amount) FROM fa_record x WHERE x.asset_id=a.id AND x.value_class='DEPRECIATION' AND x.record_date<=@asof),0)";
            d.Subtitle = "as at " + FaFmt.Date(asOf);
        }
        else d.Subtitle = "as at " + FaFmt.Date(DateTime.Today);
        string order = groupBy == "custodian" ? " ORDER BY e.emp_name IS NULL, e.emp_name, a.asset_no"
                     : groupBy == "campus" ? " ORDER BY cp.campus_name, a.asset_no"
                     : groupBy == "department" ? " ORDER BY d.dept_name IS NULL, d.dept_name, a.asset_no"
                     : groupBy == "status" ? " ORDER BY a.status, a.asset_no"
                     : groupBy == "none" ? FaAssets.OrderBy(f)
                     : " ORDER BY pc.sort_order, sc.sort_order, a.asset_no";
        DataTable t = FaDb.Table(c, null, "SELECT " + FaAssets.ListCols + ", " + valueCol + " v_at, " + accumCol + " ad_at" + FaAssets.ListFrom + where + order, prm.ToArray());
        Cover(c, d, f);
        d.Cover.Add(new KeyValuePair<string, string>("Values as at", historic ? FaFmt.Date(asOf) : FaFmt.Date(DateTime.Today)));
        if (groupBy != "none") d.Groups = new List<string>();
        foreach (DataRow r in t.Rows)
        {
            d.Rows.Add(new object[] {
                FaDb.S(r["asset_no"]), FaDb.S(r["tag_no"]), FaDb.S(r["name"]), FaDb.S(r["serial_no"]),
                FaAssets.JoinLoc(FaDb.S(r["make"]), FaDb.S(r["model"])), FaDb.S(r["cat_name"]), FaDb.S(r["sub_name"]),
                FaAssets.Title(FaDb.S(r["campus_name"])), FaAssets.Location(r), FaAssets.Title(FaDb.S(r["dept_name"])), FaDb.S(r["custodian_name"]),
                FaFmt.Date(r["purchase_date"]), FaDb.S(r["supplier_name"]), FaDb.S(r["invoice_ref"]), FaDb.M(r["original_cost"]),
                FaDb.M(r["ad_at"]), FaDb.M(r["v_at"]), FaFmt.Status(FaDb.S(r["status"])) });
            if (d.Groups != null) d.Groups.Add(Group(r, groupBy));
        }
        d.Landscape = true;
    }

    private static void ByCategory(MySqlConnection c, FaReportData d, Dictionary<string, object> f)
    {
        var prm = new List<object>();
        string where = FaAssets.Where(f, prm);
        DataTable t = FaDb.Table(c, null,
            "SELECT pc.name cat_name, sc.name sub_name, COUNT(*) n, SUM(a.original_cost) cost, SUM(a.accum_depreciation) ad, SUM(a.current_value) v" +
            FaAssets.ListFrom + where + " GROUP BY pc.id, sc.id ORDER BY pc.sort_order, sc.sort_order", prm.ToArray());
        decimal tot = 0; foreach (DataRow r in t.Rows) tot += FaDb.M(r["v"]);
        Cover(c, d, f);
        d.Subtitle = "as at " + FaFmt.Date(DateTime.Today);
        d.Groups = new List<string>(); d.Noun = "sub-category"; d.NounPlural = "sub-categories";
        foreach (DataRow r in t.Rows)
        {
            decimal v = FaDb.M(r["v"]);
            d.Rows.Add(new object[] { FaDb.S(r["sub_name"]), FaDb.I(r["n"]).ToString("#,##0"), FaDb.M(r["cost"]), FaDb.M(r["ad"]), v,
                                      tot == 0 ? "" : Math.Round(v / tot * 100m, 1).ToString("0.0") + "%" });
            d.Groups.Add(FaDb.S(r["cat_name"]));
        }
    }

    private static void ByLocation(MySqlConnection c, FaReportData d, Dictionary<string, object> f)
    {
        var prm = new List<object>();
        string where = FaAssets.Where(f, prm);
        DataTable t = FaDb.Table(c, null,
            "SELECT cp.campus_name, IFNULL(d.dept_name,'') dept_name, COUNT(*) n, SUM(a.original_cost) cost, SUM(a.accum_depreciation) ad, SUM(a.current_value) v" +
            FaAssets.ListFrom + where + " GROUP BY a.campus_id, a.department_id ORDER BY cp.campus_name, d.dept_name IS NULL, d.dept_name", prm.ToArray());
        Cover(c, d, f);
        d.Subtitle = "as at " + FaFmt.Date(DateTime.Today);
        d.Groups = new List<string>(); d.Noun = "department"; d.NounPlural = "departments";
        foreach (DataRow r in t.Rows)
        {
            string dept = FaAssets.Title(FaDb.S(r["dept_name"]));
            d.Rows.Add(new object[] { dept == "" ? "No department" : dept, FaDb.I(r["n"]).ToString("#,##0"), FaDb.M(r["cost"]), FaDb.M(r["ad"]), FaDb.M(r["v"]) });
            d.Groups.Add(FaAssets.Title(FaDb.S(r["campus_name"])));
        }
    }

    private static void Custody(MySqlConnection c, FaReportData d, Dictionary<string, object> f)
    {
        if (FaJson.Str(f, "status") == "") f["status"] = "OPEN";
        var prm = new List<object>();
        string where = FaAssets.Where(f, prm);
        DataTable t = FaDb.Table(c, null, "SELECT " + FaAssets.ListCols + FaAssets.ListFrom + where +
                                 " ORDER BY a.custodian_emp_id IS NULL, e.emp_name, a.asset_no", prm.ToArray());
        Cover(c, d, f);
        d.Subtitle = "as at " + FaFmt.Date(DateTime.Today);
        d.Groups = new List<string>();
        foreach (DataRow r in t.Rows)
        {
            d.Rows.Add(new object[] { FaDb.S(r["asset_no"]), FaDb.S(r["tag_no"]), FaDb.S(r["name"]), FaDb.S(r["serial_no"]), FaAssets.Location(r),
                                      FaFmt.Date(r["custodian_since"]), FaDb.M(r["current_value"]) });
            string g = Group(r, "custodian");
            string dept = FaAssets.Title(FaDb.S(r["dept_name"]));
            d.Groups.Add(g + (dept != "" && FaDb.S(r["custodian_name"]) != "" ? ", " + dept : ""));
        }
        d.PageBreakPerGroup = true;
        d.GroupFooterNote = "I confirm that the assets listed above are in my custody. I will take reasonable care of them and report any loss, " +
                            "damage or movement to the Estates Office without delay.";
        d.Signatories = new[] { "Issued by (Assets Officer)" };
        d.ShowTotals = false;
    }

    private static void Depreciation(MySqlConnection c, FaReportData d, Dictionary<string, object> f, string groupBy)
    {
        string fy = FaJson.Str(f, "finYear");
        if (fy == "") fy = FaFinYear.Of(DateTime.Today);
        DateTime fs = FaFinYear.StartOfLabel(fy), fe = FaFinYear.EndOfLabel(fy);
        var f2 = new Dictionary<string, object>(f); f2["status"] = "ALL"; f2.Remove("finYear");
        var prm = new List<object>();
        string where = FaAssets.Where(f2, prm) + " AND a.status<>'VOID' AND a.purchase_date<=@fe AND (a.closed_on IS NULL OR a.closed_on>=@fs)";
        prm.Add("@fs"); prm.Add(fs); prm.Add("@fe"); prm.Add(fe); prm.Add("@fy"); prm.Add(fy);
        string sub = "SELECT IFNULL(SUM(x.change_amount),0) FROM fa_record x WHERE x.asset_id=a.id AND x.fin_year=@fy AND x.value_class=";
        DataTable t = FaDb.Table(c, null,
            "SELECT " + FaAssets.ListCols + ", " +
            "IFNULL((SELECT x.value_after FROM fa_record x WHERE x.asset_id=a.id AND x.value_class<>'NONE' AND x.record_date<@fs ORDER BY x.record_date DESC, x.id DESC LIMIT 1),0) op, " +
            "(" + sub + "'COST' AND x.record_type='OPENING') brought, (" + sub + "'COST' AND x.record_type<>'OPENING') adds, (" + sub + "'DEPRECIATION') dep, (" + sub + "'REVALUATION') rev, (" + sub + "'DISPOSAL') dis, " +
            "IFNULL((SELECT x.value_after FROM fa_record x WHERE x.asset_id=a.id AND x.value_class<>'NONE' AND x.record_date<=@fe ORDER BY x.record_date DESC, x.id DESC LIMIT 1),0) cl" +
            FaAssets.ListFrom + where + " ORDER BY pc.sort_order, sc.sort_order, a.asset_no", prm.ToArray());
        Cover(c, d, f);
        d.Subtitle = "financial year " + fy;
        d.Cover.Insert(0, new KeyValuePair<string, string>("Financial year", fy + " (" + FaFmt.Date(fs) + " to " + FaFmt.Date(fe) + ")"));
        if (groupBy != "none") d.Groups = new List<string>();
        foreach (DataRow r in t.Rows)
        {
            if (FaDb.M(r["op"]) == 0m && FaDb.M(r["brought"]) == 0m && FaDb.M(r["adds"]) == 0m && FaDb.M(r["dep"]) == 0m &&
                FaDb.M(r["rev"]) == 0m && FaDb.M(r["dis"]) == 0m && FaDb.M(r["cl"]) == 0m) continue;     // not on the register in this year
            string m = FaDb.S(r["dep_method"]);
            string ms = m == "SL" ? "SL " + FaDb.M(r["useful_life_years"]).ToString("0.##") + " yrs" : m == "RB" ? "RB " + FaDb.M(r["dep_rate_pct"]).ToString("0.##") + "%" : "None";
            d.Rows.Add(new object[] { FaDb.S(r["asset_no"]), FaDb.S(r["name"]), FaDb.S(r["sub_name"]), ms, FaDb.M(r["op"]), FaDb.M(r["brought"]), FaDb.M(r["adds"]),
                                      -FaDb.M(r["dep"]), FaDb.M(r["rev"]), -FaDb.M(r["dis"]), FaDb.M(r["cl"]) });
            if (d.Groups != null) d.Groups.Add(Group(r, groupBy));
        }
        d.FootNote = "Closing value = opening value + brought into register + additions - depreciation + revaluation - disposals. Brought into register is the opening balance of an asset entered when the register began. Depreciation is straight line or reducing balance, " +
                     "charged by month from the month the asset was bought; the month of disposal is not charged.";
        d.Landscape = true;
    }

    private static void Records(MySqlConnection c, FaReportData d, Dictionary<string, object> f, string types, string groupBy)
    {
        DateTime fyStart = FaFinYear.Start(DateTime.Today);
        DateTime from = Period(f, "from", fyStart), to = Period(f, "to", DateTime.Today);
        var w = new StringBuilder(" WHERE r.record_date BETWEEN @from AND @to AND r.reversed_by_record_id IS NULL AND r.record_type<>'REVERSAL'");
        var prm = new List<object> { "@from", from, "@to", to };
        if (types != null) w.Append(" AND r.record_type IN (" + types + ")");
        else
        {
            string rt = FaJson.Str(f, "recordType").ToUpperInvariant();
            if (rt != "" && System.Text.RegularExpressions.Regex.IsMatch(rt, "^[A-Z]+$")) { w.Append(" AND r.record_type=@rt"); prm.Add("@rt"); prm.Add(rt); }
            if (FaJson.Bool(f, "includeReversals")) w = new StringBuilder(w.ToString().Replace(" AND r.reversed_by_record_id IS NULL AND r.record_type<>'REVERSAL'", ""));
        }
        int campus = FaJson.Int(f, "campusId"), cat = FaJson.Int(f, "categoryId"), sub = FaJson.Int(f, "subCategoryId");
        if (campus > 0) { w.Append(" AND (a.campus_id=@cp OR r.from_campus_id=@cp OR r.to_campus_id=@cp)"); prm.Add("@cp"); prm.Add(campus); }
        if (sub > 0) { w.Append(" AND a.category_id=@sc"); prm.Add("@sc"); prm.Add(sub); }
        else if (cat > 0) { w.Append(" AND sc.parent_id=@ct"); prm.Add("@ct"); prm.Add(cat); }
        string q = FaJson.Str(f, "q");
        if (q != "") { w.Append(" AND (a.asset_no LIKE @q OR a.name LIKE @q OR r.reason LIKE @q OR r.reference LIKE @q)"); prm.Add("@q"); prm.Add("%" + q + "%"); }
        string order = groupBy == "method" ? " ORDER BY r.disposal_method, r.record_date, r.id"
                     : groupBy == "category" ? " ORDER BY pc.sort_order, sc.sort_order, r.record_date, r.id"
                     : groupBy == "type" ? " ORDER BY r.record_type, r.record_date, r.id"
                     : groupBy == "campus" ? " ORDER BY tc.campus_name, r.record_date, r.id"
                     : " ORDER BY r.record_date, r.id";
        DataTable t = FaDb.Table(c, null,
            "SELECT r.*, a.asset_no, a.name, pc.name cat_name, sc.name sub_name, IFNULL(fc.campus_name,'') from_campus, IFNULL(tc.campus_name,'') to_campus, " +
            "IFNULL(fd.dept_name,'') from_dept, IFNULL(td.dept_name,'') to_dept, IFNULL(fe.emp_name,'') from_cus, IFNULL(te.emp_name,'') to_cus " +
            "FROM fa_record r JOIN fa_asset a ON a.id=r.asset_id JOIN fa_category sc ON sc.id=a.category_id LEFT JOIN fa_category pc ON pc.id=sc.parent_id " +
            "LEFT JOIN acad_campuses fc ON fc.ID=r.from_campus_id LEFT JOIN acad_campuses tc ON tc.ID=r.to_campus_id " +
            "LEFT JOIN hrm_departments fd ON fd.ID=r.from_department_id LEFT JOIN hrm_departments td ON td.ID=r.to_department_id " +
            "LEFT JOIN hrm_employee fe ON fe.empID=r.from_custodian_emp_id LEFT JOIN hrm_employee te ON te.empID=r.to_custodian_emp_id" +
            w + order + " LIMIT 20000", prm.ToArray());
        Cover(c, d, f);
        d.Subtitle = FaFmt.Date(from) + " to " + FaFmt.Date(to);
        d.Cover.Insert(0, new KeyValuePair<string, string>("Period", FaFmt.Date(from) + " to " + FaFmt.Date(to)));
        d.Noun = "record"; d.NounPlural = "records";
        if (groupBy != "none") d.Groups = new List<string>();
        foreach (DataRow r in t.Rows)
        {
            string ty = FaDb.S(r["record_type"]);
            if (d.Key == "revaluations")
                d.Rows.Add(new object[] { FaFmt.Date(r["record_date"]), FaDb.S(r["asset_no"]), FaDb.S(r["name"]), FaFmt.RecordType(ty),
                    FaDb.M(r["value_before"]), FaDb.M(r["value_after"]), FaDb.M(r["change_amount"]), FaDb.S(r["valuer"]), FaDb.S(r["reference"]),
                    FaDb.S(r["reason"]), FaDb.S(r["recorded_by"]) });
            else if (d.Key == "disposals")
                d.Rows.Add(new object[] { FaFmt.Date(r["record_date"]), FaDb.S(r["asset_no"]), FaDb.S(r["name"]), FaDb.S(r["cat_name"]),
                    FaFmt.Disposal(FaDb.S(r["disposal_method"])), FaDb.M(r["value_before"]), FaDb.M(r["proceeds"]), FaDb.M(r["gain_loss"]),
                    FaDb.S(r["approval_ref"]), FaDb.S(r["reason"]), FaDb.S(r["approved_by"]) });
            else if (d.Key == "movements")
                d.Rows.Add(new object[] { FaFmt.Date(r["record_date"]), FaDb.S(r["asset_no"]), FaDb.S(r["name"]),
                    Place(FaDb.S(r["from_campus"]), FaDb.S(r["from_location"]), FaDb.S(r["from_dept"]), FaDb.S(r["from_cus"])),
                    Place(FaDb.S(r["to_campus"]), FaDb.S(r["to_location"]), FaDb.S(r["to_dept"]), FaDb.S(r["to_cus"])),
                    FaDb.S(r["reason"]), FaDb.S(r["recorded_by"]) });
            else
                d.Rows.Add(new object[] { FaFmt.Date(r["record_date"]), FaDb.S(r["asset_no"]), FaDb.S(r["name"]), FaFmt.RecordType(ty),
                    FaRecords.Describe(r), FaDb.M(r["value_before"]), FaDb.M(r["change_amount"]), FaDb.M(r["value_after"]),
                    FaDb.S(r["reason"]), FaDb.S(r["recorded_by"]) });
            if (d.Groups != null)
                d.Groups.Add(groupBy == "method" ? FaFmt.Disposal(FaDb.S(r["disposal_method"])) : groupBy == "category" ? FaDb.S(r["cat_name"])
                           : groupBy == "type" ? FaFmt.RecordType(ty) : FaAssets.Title(FaDb.S(r["to_campus"])));
        }
        if (d.Key == "movements" || d.Key == "ledger") d.ShowTotals = false;
        if (d.Key == "disposals") d.Signatories = new[] { "Prepared by (Assets Officer)", "Checked by (Accountant)", "Approved by (University Bursar)" };
        d.Landscape = true;
    }

    private static string Place(string campus, string loc, string dept, string person)
    {
        var p = new List<string>();
        if (campus != "") p.Add(FaAssets.Title(campus).Replace(" Campus", ""));
        if (loc != "") p.Add(loc);
        if (dept != "") p.Add(FaAssets.Title(dept));
        if (person != "") p.Add(person);
        return string.Join("; ", p.ToArray());
    }

    private static void History(MySqlConnection c, FaReportData d, Dictionary<string, object> f)
    {
        int id = FaJson.Int(f, "assetId");
        var det = FaAssets.Detail(c, id, null);
        if (det == null) throw new ArgumentException("Choose an asset.");
        var fs = new Dictionary<string, object>(); fs["assetId"] = id; fs["status"] = "ALL";
        int total; decimal sc, sv;
        DataRow a = FaAssets.Query(c, fs, 1, 1, out total, out sc, out sv).Rows[0];
        DataRow raw = FaAssets.Row(c, null, id, false);
        d.Subtitle = FaDb.S(a["asset_no"]);
        d.FileWhat = "history-" + FaDb.S(a["asset_no"]).Replace("/", "-");
        d.Cover.Add(new KeyValuePair<string, string>("Asset", FaDb.S(a["asset_no"]) + ", " + FaDb.S(a["name"])));
        d.Cover.Add(new KeyValuePair<string, string>("Category", FaDb.S(a["cat_name"]) + " / " + FaDb.S(a["sub_name"])));
        d.Cover.Add(new KeyValuePair<string, string>("Serial, make, model", FaAssets.JoinLoc(FaDb.S(a["serial_no"]), FaAssets.JoinLoc(FaDb.S(a["make"]), FaDb.S(a["model"])))));
        d.Cover.Add(new KeyValuePair<string, string>("Location", FaAssets.Location(a) + (FaDb.S(a["dept_name"]) != "" ? "; " + FaAssets.Title(FaDb.S(a["dept_name"])) : "")));
        d.Cover.Add(new KeyValuePair<string, string>("Responsible person", FaDb.S(a["custodian_name"]) == "" ? "None" : FaDb.S(a["custodian_name"])));
        d.Cover.Add(new KeyValuePair<string, string>("Purchased", FaFmt.Date(a["purchase_date"]) + " for UGX " + FaFmt.Money(a["original_cost"]) +
                                                     (FaDb.S(a["supplier_name"]) != "" ? " from " + FaDb.S(a["supplier_name"]) : "")));
        d.Cover.Add(new KeyValuePair<string, string>("Depreciation", FaFmt.Method(FaDb.S(raw["dep_method"])) +
            (FaDb.S(raw["dep_method"]) == "SL" ? ", " + FaDb.M(raw["useful_life_years"]).ToString("0.##") + " years" : FaDb.S(raw["dep_method"]) == "RB" ? ", " + FaDb.M(raw["dep_rate_pct"]).ToString("0.##") + "% a year" : "") +
            (FaDb.D(raw["depreciated_to"]).HasValue ? ", charged to " + FaFmt.Date(raw["depreciated_to"]) : "")));
        d.Cover.Add(new KeyValuePair<string, string>("Position now", FaFmt.Status(FaDb.S(a["status"])) + ", book value UGX " + FaFmt.Money(a["current_value"])));
        DataTable t = FaDb.Table(c, null,
            "SELECT r.*, IFNULL(fc.campus_name,'') from_campus, IFNULL(tc.campus_name,'') to_campus, IFNULL(fd.dept_name,'') from_dept, IFNULL(td.dept_name,'') to_dept, " +
            "IFNULL(fe.emp_name,'') from_cus, IFNULL(te.emp_name,'') to_cus FROM fa_record r " +
            "LEFT JOIN acad_campuses fc ON fc.ID=r.from_campus_id LEFT JOIN acad_campuses tc ON tc.ID=r.to_campus_id " +
            "LEFT JOIN hrm_departments fd ON fd.ID=r.from_department_id LEFT JOIN hrm_departments td ON td.ID=r.to_department_id " +
            "LEFT JOIN hrm_employee fe ON fe.empID=r.from_custodian_emp_id LEFT JOIN hrm_employee te ON te.empID=r.to_custodian_emp_id " +
            "WHERE r.asset_id=@id ORDER BY r.record_date, r.id", "@id", id);
        d.Noun = "record"; d.NounPlural = "records";
        foreach (DataRow r in t.Rows)
        {
            string ty = FaDb.S(r["record_type"]);
            string label = FaFmt.RecordType(ty) + (r["reversed_by_record_id"] != DBNull.Value ? " (reversed)" : "");
            d.Rows.Add(new object[] { FaFmt.Date(r["record_date"]), label, FaRecords.Describe(r), FaDb.M(r["value_before"]), FaDb.M(r["change_amount"]),
                                      FaDb.M(r["value_after"]), FaDb.S(r["reason"]), FaDb.S(r["reference"]), FaDb.S(r["recorded_by"]) });
        }
        d.ShowTotals = false;
        d.Signatories = new[] { "Certified by (Assets Officer)", "Approved by (University Bursar)" };
        d.Landscape = true;
    }

    private static void Verification(MySqlConnection c, FaReportData d, Dictionary<string, object> f, string groupBy)
    {
        f["status"] = "OPEN";
        var prm = new List<object>();
        string where = FaAssets.Where(f, prm);
        string order = groupBy == "department" ? " ORDER BY d.dept_name IS NULL, d.dept_name, a.building, a.room, a.asset_no"
                     : groupBy == "custodian" ? " ORDER BY e.emp_name IS NULL, e.emp_name, a.asset_no"
                     : " ORDER BY cp.campus_name, a.building IS NULL, a.building, a.room, a.asset_no";
        DataTable t = FaDb.Table(c, null, "SELECT " + FaAssets.ListCols + FaAssets.ListFrom + where + order, prm.ToArray());
        Cover(c, d, f);
        d.Subtitle = "count of " + FaFmt.Date(DateTime.Today);
        d.Groups = new List<string>();
        foreach (DataRow r in t.Rows)
        {
            d.Rows.Add(new object[] { FaDb.S(r["asset_no"]), FaDb.S(r["tag_no"]), FaDb.S(r["name"]), FaDb.S(r["serial_no"]),
                                      FaAssets.Location(r), FaDb.S(r["custodian_name"]), "", "", "" });
            d.Groups.Add(Group(r, groupBy));
        }
        d.RowHeight = 22f; d.ShowTotals = false; d.Landscape = true;
        d.FootNote = "Tick Found for each asset seen. Record its condition as Good, Fair, Poor or Unserviceable. List any asset found here that is not on this sheet under Remarks.";
        d.Signatories = new[] { "Verifying officer", "Custodian or head of unit", "Witness" };
    }

    private static void Reconciliation(MySqlConnection c, FaReportData d, Dictionary<string, object> f)
    {
        DateTime asOf = FaJson.Date(f, "asOf") ?? DateTime.Today;
        d.Subtitle = "as at " + FaFmt.Date(asOf);
        d.Cover.Add(new KeyValuePair<string, string>("As at", FaFmt.Date(asOf)));
        var reg = new Dictionary<string, decimal[]>();    // account -> {amount, kind 0 cost / 1 accum}
        DataTable t = FaDb.Table(c, null,
            "SELECT COALESCE(NULLIF(sc.gl_cost_account,''), pc.gl_cost_account, '') cost_ac, COALESCE(NULLIF(sc.gl_accum_account,''), pc.gl_accum_account, '') acc_ac, " +
            "a.id, a.cost_basis, a.current_value, a.reval_surplus FROM fa_asset a JOIN fa_category sc ON sc.id=a.category_id LEFT JOIN fa_category pc ON pc.id=sc.parent_id " +
            "WHERE a.status IN ('IN_USE','IN_STORE','UNDER_REPAIR','LOST') AND a.purchase_date<=@d", "@d", asOf);
        foreach (DataRow r in t.Rows)
        {
            string ca = FaDb.S(r["cost_ac"]), aa = FaDb.S(r["acc_ac"]);
            decimal carrying = FaDb.M(r["cost_basis"]) + FaDb.M(r["reval_surplus"]);
            decimal accum = carrying - FaDb.M(r["current_value"]);
            if (ca != "") { if (!reg.ContainsKey(ca)) reg[ca] = new decimal[] { 0, 0 }; reg[ca][0] += carrying; }
            if (aa != "") { if (!reg.ContainsKey(aa)) reg[aa] = new decimal[] { 0, 1 }; reg[aa][0] += accum; }
        }
        // Every account any category maps to appears, even with nothing in the register yet.
        foreach (DataRow r in FaDb.Table(c, null, "SELECT gl_cost_account, gl_accum_account FROM fa_category").Rows)
        {
            string ca = FaDb.S(r[0]), aa = FaDb.S(r[1]);
            if (ca != "" && !reg.ContainsKey(ca)) reg[ca] = new decimal[] { 0, 0 };
            if (aa != "" && !reg.ContainsKey(aa)) reg[aa] = new decimal[] { 0, 1 };
        }
        var keys = new List<string>(reg.Keys); keys.Sort();
        foreach (string k in keys)
        {
            bool isCost = reg[k][1] == 0;
            DataTable g = FaDb.Table(c, null,
                "SELECT IFNULL(SUM(CASE WHEN transactionType='DR' THEN ugx_amount ELSE 0 END),0) dr, IFNULL(SUM(CASE WHEN transactionType='CR' THEN ugx_amount ELSE 0 END),0) cr, " +
                "(SELECT AccountName FROM campus_dynamics_accounts.fin_subaccounts WHERE AccountCode=@a) nm " +
                "FROM campus_dynamics_accounts.fin_ledger WHERE accountcode=@a AND transactionDate<=@d", "@a", k, "@d", asOf);
            decimal dr = FaDb.M(g.Rows[0]["dr"]), cr = FaDb.M(g.Rows[0]["cr"]);
            decimal led = isCost ? dr - cr : cr - dr;
            decimal regAmt = FaMath.Round0(reg[k][0]);
            d.Rows.Add(new object[] { k, FaDb.S(g.Rows[0]["nm"]), isCost ? "Cost or valuation" : "Accumulated depreciation", regAmt, FaMath.Round0(led), regAmt - FaMath.Round0(led) });
        }
        d.Noun = "account"; d.NounPlural = "accounts";
        d.FootNote = "Register cost includes capital additions and net revaluation. Differences are expected until every asset held at the opening date has been entered " +
                     "on the register. The register does not post to the general ledger; post depreciation through Journal Entries using the journal summary.";
        d.Signatories = new[] { "Prepared by (Accountant)", "Approved by (University Bursar)" };
    }

    private static void Journal(MySqlConnection c, FaReportData d, Dictionary<string, object> f)
    {
        long run = FaJson.Int(f, "runId");
        string fy = FaJson.Str(f, "finYear");
        if (run <= 0 && fy == "") fy = FaFinYear.Of(DateTime.Today);
        d.Subtitle = run > 0 ? "run " + run : "financial year " + fy;
        d.Cover.Add(new KeyValuePair<string, string>(run > 0 ? "Depreciation run" : "Financial year", run > 0 ? run.ToString() : fy));
        d.Noun = "line"; d.NounPlural = "lines";
        foreach (object o in FaDepreciation.Journal(c, run, fy))
        {
            var p = o.GetType();
            d.Rows.Add(new object[] { (string)p.GetProperty("account").GetValue(o, null), (string)p.GetProperty("accountName").GetValue(o, null),
                                      (string)p.GetProperty("note").GetValue(o, null), (decimal)p.GetProperty("debit").GetValue(o, null),
                                      (decimal)p.GetProperty("credit").GetValue(o, null) });
        }
        d.FootNote = "Post as one journal: debit each depreciation expense account and credit each accumulated depreciation account with the amounts shown.";
        d.Signatories = new[] { "Prepared by (Accountant)", "Approved by (University Bursar)" };
    }

    // ─────────────────────────── Output ───────────────────────────

    /// <summary>Indexes of the columns to output: the ticked keys, else the defaults. Blank (tick) columns follow their report.</summary>
    private static List<int> Pick(FaReportData d, string keys, bool pdf)
    {
        var want = new List<string>();
        if (!string.IsNullOrEmpty(keys)) foreach (string k in keys.Split(',')) if (k.Trim() != "") want.Add(k.Trim());
        var l = new List<int>();
        for (int i = 0; i < d.Cols.Count; i++)
        {
            var c = d.Cols[i];
            if (c.Blank) { if (pdf) l.Add(i); continue; }
            if (want.Count == 0 ? c.On : want.Contains(c.Key)) l.Add(i);
        }
        if (l.Count == 0) for (int i = 0; i < d.Cols.Count; i++) if (d.Cols[i].On && (!d.Cols[i].Blank || pdf)) l.Add(i);
        return l;
    }

    private static string Text(object v, bool numeric)
    {
        if (v == null) return "";
        if (v is decimal) return numeric ? FaFmt.Num((decimal)v) : FaFmt.Money((decimal)v);
        return Convert.ToString(v, CultureInfo.InvariantCulture);
    }

    public static void Send(HttpResponse resp, FaReportData d, string format, string colKeys)
    {
        string fileBase = FaExport.FileName(d.FileWhat);
        bool pdf = format == "pdf";
        var idx = Pick(d, colKeys, pdf);
        string groupLabel = d.Groups == null ? null : "Group";

        if (!pdf)
        {
            var sh = new FaExport.Sheet();
            sh.Name = d.Title; sh.Subtitle = d.Subtitle ?? "";
            var heads = new List<string>();
            if (d.Groups != null) heads.Add(GroupHeading(d));
            foreach (int i in idx) heads.Add(d.Cols[i].Header);
            sh.Columns = heads.ToArray();
            int off = d.Groups != null ? 1 : 0;
            for (int j = 0; j < idx.Count; j++) if (d.Cols[idx[j]].Numeric) sh.NumericColumns.Add(j + off);
            var sums = new decimal[idx.Count];
            for (int r = 0; r < d.Rows.Count; r++)
            {
                var cells = new List<string>();
                if (d.Groups != null) cells.Add(d.Groups[r]);
                for (int j = 0; j < idx.Count; j++)
                {
                    object v = d.Rows[r][idx[j]];
                    cells.Add(Text(v, d.Cols[idx[j]].Numeric));
                    if (d.Cols[idx[j]].Sum && v is decimal) sums[j] += (decimal)v;
                }
                sh.Rows.Add(cells.ToArray());
            }
            bool anySum = false; foreach (int i in idx) if (d.Cols[i].Sum) anySum = true;
            if (anySum && d.ShowTotals)
            {
                var tot = new List<string>();
                if (d.Groups != null) tot.Add("Total");
                bool labelled = d.Groups != null;
                for (int j = 0; j < idx.Count; j++)
                {
                    if (d.Cols[idx[j]].Sum) tot.Add(FaFmt.Num(sums[j]));
                    else if (!labelled) { tot.Add("Total"); labelled = true; }
                    else tot.Add("");
                }
                sh.Totals = tot.ToArray();
            }
            if (format == "csv") FaExport.Csv(resp, fileBase, d.Title, d.Subtitle, d.Cover, sh);
            else FaExport.Workbook(resp, fileBase, d.Title, d.Subtitle, d.Cover, new List<FaExport.Sheet> { sh });
            return;
        }

        var spec = new FaPdf.Spec();
        spec.FileBase = fileBase; spec.Title = d.Title; spec.Subtitle = d.Subtitle; spec.Cover = d.Cover;
        spec.Grouped = d.Groups != null; spec.PageBreakPerGroup = d.PageBreakPerGroup; spec.GroupFooterNote = d.GroupFooterNote;
        if (d.Signatories != null) spec.Signatories = d.Signatories;
        if (d.FootNote != null) spec.FootNote = d.FootNote;
        spec.RowHeight = d.RowHeight; spec.ShowTotals = d.ShowTotals; spec.Landscape = d.Landscape;
        spec.Noun = d.Noun; spec.NounPlural = d.NounPlural;
        var t = new DataTable();
        foreach (int i in idx)
        {
            var c = d.Cols[i];
            t.Columns.Add("F" + i, c.Sum ? typeof(decimal) : typeof(string));
            spec.Cols.Add(new FaPdf.PCol("F" + i, c.Header, c.Width, c.Numeric, c.Sum) { Blank = c.Blank });
        }
        if (spec.Grouped) t.Columns.Add(FaPdf.GROUP_COL, typeof(string));
        for (int r = 0; r < d.Rows.Count; r++)
        {
            DataRow row = t.NewRow();
            foreach (int i in idx)
            {
                object v = d.Rows[r][i];
                if (d.Cols[i].Sum) row["F" + i] = v is decimal ? (object)FaMath.Round0((decimal)v) : 0m;
                else row["F" + i] = Text(v, false);
            }
            if (spec.Grouped) row[FaPdf.GROUP_COL] = d.Groups[r];
            t.Rows.Add(row);
        }
        spec.Rows = t;
        FaPdf.Send(resp, spec);
    }

    private static string GroupHeading(FaReportData d)
    {
        switch (d.Key)
        {
            case "by-category": return "Category";
            case "by-location": return "Campus";
            case "custody": return "Responsible person";
            default: return "Group";
        }
    }

    /// <summary>Row count for the export dialog.</summary>
    public static int Count(string key, Dictionary<string, object> f, string groupBy)
    {
        try { return Run(key, f, groupBy).Rows.Count; } catch { return 0; }
    }
}
