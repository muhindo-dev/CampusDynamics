using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MySql.Data.MySqlClient;

// =====================================================================
//  Fixed Assets: depreciation runs (plan 5.4 to 5.6, 4.5).
//
//  Preview computes what a run would charge; Post recomputes the same
//  thing inside one transaction with every affected asset row locked,
//  and refuses if the result differs from what the user previewed.
//  Four layers stop a period being charged twice: coverage (only months
//  after depreciated_to), row locks, the unique key on fa_record, and
//  the idempotent client operation id.
// =====================================================================
public static class FaDepreciation
{
    public class Line
    {
        public int AssetId; public string AssetNo, Name, Method, CatCode, CatName, FinYear;
        public DateTime From, To; public int Months; public decimal Before, Charge, After;
    }

    public class Plan
    {
        public DateTime PeriodEnd; public string FinYear;
        public List<Line> Lines = new List<Line>();
        public List<object> Skipped = new List<object>();
        public decimal Total; public int Assets;
        public string Hash;
    }

    private static string Sha1(string s)
    {
        using (var h = SHA1.Create())
        {
            byte[] b = h.ComputeHash(Encoding.UTF8.GetBytes(s));
            var sb = new StringBuilder(40);
            foreach (byte x in b) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }
    }

    /// <summary>Builds the plan for a period end and scope. With a transaction, locks the asset rows first.</summary>
    public static Plan Build(MySqlConnection c, MySqlTransaction tx, DateTime periodEnd, int campusId, int categoryId)
    {
        var p = new Plan();
        p.PeriodEnd = FaFinYear.MonthEnd(periodEnd);
        p.FinYear = FaFinYear.Of(p.PeriodEnd);

        var w = new StringBuilder(" WHERE a.status IN ('IN_USE','IN_STORE','UNDER_REPAIR','LOST') ");
        var prm = new List<object>();
        if (campusId > 0) { w.Append(" AND a.campus_id=@cp"); prm.Add("@cp"); prm.Add(campusId); }
        if (categoryId > 0) { w.Append(" AND (sc.parent_id=@ct OR sc.id=@ct)"); prm.Add("@ct"); prm.Add(categoryId); }

        if (tx != null)
            FaDb.Table(c, tx, "SELECT a.id FROM fa_asset a JOIN fa_category sc ON sc.id=a.category_id" + w + " ORDER BY a.id FOR UPDATE", prm.ToArray());

        DataTable t = FaDb.Table(c, tx,
            "SELECT a.*, sc.code sub_code, IFNULL(pc.code,'') cat_code, IFNULL(pc.name, sc.name) cat_name FROM fa_asset a " +
            "JOIN fa_category sc ON sc.id=a.category_id LEFT JOIN fa_category pc ON pc.id=sc.parent_id" + w + " ORDER BY a.asset_no", prm.ToArray());

        var locked = new Dictionary<string, bool>();
        var hash = new StringBuilder();
        foreach (DataRow a in t.Rows)
        {
            int id = FaDb.I(a["id"]);
            string no = FaDb.S(a["asset_no"]);
            string method = FaDb.S(a["dep_method"]);
            if (method == "NONE") { p.Skipped.Add(new { assetNo = no, name = FaDb.S(a["name"]), why = "Not depreciated" }); continue; }
            DateTime? depTo = FaDb.D(a["depreciated_to"]);
            if (depTo.HasValue && depTo.Value >= p.PeriodEnd) { p.Skipped.Add(new { assetNo = no, name = FaDb.S(a["name"]), why = "Already depreciated to " + FaFmt.Date(depTo) }); continue; }
            if (FaDb.M(a["current_value"]) <= FaDb.M(a["residual_value"])) { p.Skipped.Add(new { assetNo = no, name = FaDb.S(a["name"]), why = "Fully depreciated" }); continue; }
            DateTime ds = FaDb.D(a["dep_start_date"]) ?? DateTime.Today;
            if (FaFinYear.MonthStart(ds) > p.PeriodEnd) { p.Skipped.Add(new { assetNo = no, name = FaDb.S(a["name"]), why = "Depreciation starts " + FaFmt.Date(ds) }); continue; }

            var slices = FaRecords.DuePlan(c, tx, a, p.PeriodEnd);
            if (slices.Count == 0) { p.Skipped.Add(new { assetNo = no, name = FaDb.S(a["name"]), why = "Nothing due" }); continue; }

            string blocked = null;
            foreach (var s in slices)
            {
                bool lk;
                if (!locked.TryGetValue(s.FinYear, out lk)) { lk = FaFinYear.IsLocked(c, tx, s.FinYear); locked[s.FinYear] = lk; }
                if (lk) { blocked = s.FinYear; break; }
            }
            if (blocked != null) { p.Skipped.Add(new { assetNo = no, name = FaDb.S(a["name"]), why = "Financial year " + blocked + " is locked" }); continue; }

            p.Assets++;
            foreach (var s in slices)
            {
                var l = new Line();
                l.AssetId = id; l.AssetNo = no; l.Name = FaDb.S(a["name"]); l.Method = method; l.CatCode = FaDb.S(a["cat_code"]);
                l.CatName = FaDb.S(a["cat_name"]); l.FinYear = s.FinYear; l.From = s.From; l.To = s.To; l.Months = s.Months;
                l.Before = s.Before; l.Charge = s.Charge; l.After = s.After;
                p.Lines.Add(l); p.Total += s.Charge;
                hash.Append(id).Append('|').Append(s.From.ToString("yyyyMM")).Append('|').Append(s.To.ToString("yyyyMM")).Append('|')
                    .Append(s.Charge.ToString("0", CultureInfo.InvariantCulture)).Append('\n');
            }
        }
        p.Hash = Sha1(hash.ToString());
        return p;
    }

    public static object PreviewJson(Plan p, int maxLines)
    {
        var lines = new List<object>();
        var byCat = new Dictionary<string, decimal[]>();
        var catNames = new Dictionary<string, string>();
        foreach (var l in p.Lines)
        {
            if (lines.Count < maxLines)
                lines.Add(new { assetId = l.AssetId, assetNo = l.AssetNo, name = l.Name, method = FaFmt.Method(l.Method), finYear = l.FinYear,
                                from = FaFmt.Date(l.From), to = FaFmt.Date(l.To), months = l.Months, before = l.Before, charge = l.Charge, after = l.After });
            decimal[] v;
            if (!byCat.TryGetValue(l.CatCode, out v)) { v = new decimal[2]; byCat[l.CatCode] = v; catNames[l.CatCode] = l.CatName; }
            v[1] += l.Charge;
        }
        var assetsPerCat = new Dictionary<string, HashSet<int>>();
        foreach (var l in p.Lines)
        {
            if (!assetsPerCat.ContainsKey(l.CatCode)) assetsPerCat[l.CatCode] = new HashSet<int>();
            assetsPerCat[l.CatCode].Add(l.AssetId);
        }
        var cats = new List<object>();
        foreach (var kv in byCat)
            cats.Add(new { code = kv.Key, name = catNames[kv.Key], count = assetsPerCat[kv.Key].Count, charge = kv.Value[1] });
        return new
        {
            success = true, finYear = p.FinYear, periodEnd = FaFmt.Date(p.PeriodEnd), periodEndIso = FaFmt.Iso(p.PeriodEnd),
            rows = lines, rowsShown = lines.Count, rowsTotal = p.Lines.Count, byCategory = cats, total = p.Total, count = p.Assets,
            skipped = p.Skipped, previewHash = p.Hash
        };
    }

    /// <summary>Posts a run. Returns null or the reason it was refused.</summary>
    public static string Post(DateTime periodEnd, int campusId, int categoryId, string previewHash, string notes, string clientOp,
                              out long runId, out int count, out decimal total)
    {
        runId = 0; count = 0; total = 0m;
        if (FaFinYear.MonthEnd(periodEnd) > FaFinYear.MonthEnd(DateTime.Today)) return "Depreciation cannot be charged for months that have not yet ended. Choose this month or earlier.";
        using (var c = FaDb.Open())
        using (var tx = c.BeginTransaction())
        {
            try
            {
                if (!string.IsNullOrEmpty(clientOp) && FaDb.Scalar(c, tx, "SELECT 1 FROM fa_depreciation_run WHERE client_op_id=@o", "@o", clientOp) != null)
                { tx.Rollback(); return "This run has already been posted."; }
                if (FaFinYear.IsLocked(c, tx, FaFinYear.Of(periodEnd))) { tx.Rollback(); return "Financial year " + FaFinYear.Of(periodEnd) + " is locked."; }

                Plan p = Build(c, tx, periodEnd, campusId, categoryId);
                if (p.Lines.Count == 0) { tx.Rollback(); return "Nothing is due for this period."; }
                if (p.Hash != previewHash) { tx.Rollback(); return "Values changed since the preview. Preview again before posting."; }

                var scope = new Dictionary<string, object> { { "campusId", campusId }, { "categoryId", categoryId } };
                runId = FaDb.Insert(c, tx,
                    "INSERT INTO fa_depreciation_run (fin_year, period_end, scope_json, status, asset_count, total_amount, preview_hash, notes, posted_by, posted_at, client_op_id) " +
                    "VALUES (@fy,@pe,@sc,'POSTED',@n,@t,@h,@no,@u,NOW(),@op)",
                    "@fy", p.FinYear, "@pe", p.PeriodEnd, "@sc", FaJson.Ser(scope), "@n", p.Assets, "@t", p.Total, "@h", p.Hash,
                    "@no", FaDb.NullIfEmpty(FaAudit.Cut(notes, 500)), "@u", FaAccess.Username(), "@op", string.IsNullOrEmpty(clientOp) ? (object)DBNull.Value : clientOp);

                var byAsset = new Dictionary<int, List<FaMath.Slice>>();
                var order = new List<int>();
                foreach (var l in p.Lines)
                {
                    if (!byAsset.ContainsKey(l.AssetId)) { byAsset[l.AssetId] = new List<FaMath.Slice>(); order.Add(l.AssetId); }
                    var s = new FaMath.Slice(); s.From = l.From; s.To = l.To; s.Months = l.Months; s.Before = l.Before; s.Charge = l.Charge; s.After = l.After; s.FinYear = l.FinYear;
                    byAsset[l.AssetId].Add(s);
                }
                string reason = "Depreciation run " + runId + " to " + FaFmt.Date(p.PeriodEnd);
                foreach (int id in order)
                {
                    FaRecords.PostSlices(c, tx, id, byAsset[id], runId, reason);
                    FaPosting.Recompute(c, tx, id, "depreciation run " + runId, false);
                }
                FaAudit.Write(c, tx, "RUN", runId, null, "POST", null,
                              new { finYear = p.FinYear, periodEnd = FaFmt.Iso(p.PeriodEnd), assets = p.Assets, total = p.Total, campusId = campusId, categoryId = categoryId },
                              notes, "Depreciation run " + runId + " posted: " + FaFmt.Plural(p.Assets, "asset", "assets") + ", UGX " + FaFmt.Money(p.Total) + " to " + FaFmt.Date(p.PeriodEnd));
                count = p.Assets; total = p.Total;
                tx.Commit();
            }
            catch (MySqlException ex)
            {
                try { tx.Rollback(); } catch { }
                FaLog.Error("FaDepreciation.Post", ex);
                runId = 0;
                return ex.Number == 1062 ? "Another run charged some of these assets at the same moment. Nothing was posted; preview again."
                                         : "The run could not be posted. Nothing was changed.";
            }
        }
        return null;
    }

    /// <summary>Reverses the newest posted run. Returns null or the reason it was refused.</summary>
    public static string ReverseRun(long runId, string reason, string clientOp)
    {
        if ((reason ?? "").Trim().Length < 10) return "Give a reason of at least 10 characters.";
        using (var c = FaDb.Open())
        using (var tx = c.BeginTransaction())
        {
            try
            {
                DataTable rt = FaDb.Table(c, tx, "SELECT * FROM fa_depreciation_run WHERE id=@id FOR UPDATE", "@id", runId);
                if (rt.Rows.Count == 0) { tx.Rollback(); return "That run no longer exists."; }
                if (FaDb.S(rt.Rows[0]["status"]) != "POSTED") { tx.Rollback(); return "That run has already been reversed."; }
                long newest = FaDb.L(FaDb.Scalar(c, tx, "SELECT MAX(id) FROM fa_depreciation_run WHERE status='POSTED'"));
                if (newest != runId) { tx.Rollback(); return "Only the newest run can be reversed. Reverse run " + newest + " first."; }

                DataTable recs = FaDb.Table(c, tx, "SELECT * FROM fa_record WHERE run_id=@r AND reversed_by_record_id IS NULL ORDER BY asset_id, record_date DESC, id DESC", "@r", runId);
                var assets = new List<int>();
                foreach (DataRow x in recs.Rows) { int a = FaDb.I(x["asset_id"]); if (!assets.Contains(a)) assets.Add(a); }
                assets.Sort();
                foreach (int a in assets) FaAssets.Row(c, tx, a, true);   // lock in id order

                foreach (DataRow x in recs.Rows)
                    if (FaFinYear.IsLocked(c, tx, FaDb.S(x["fin_year"]))) { tx.Rollback(); return "Financial year " + FaDb.S(x["fin_year"]) + " is locked."; }

                // Every asset's run charges must be its latest value records.
                foreach (int a in assets)
                {
                    object later = FaDb.Scalar(c, tx,
                        "SELECT f.asset_no FROM fa_record r JOIN fa_asset f ON f.id=r.asset_id WHERE r.asset_id=@a AND r.reversed_by_record_id IS NULL " +
                        "AND r.record_type<>'REVERSAL' AND r.value_class<>'NONE' AND IFNULL(r.run_id,0)<>@run " +
                        "AND (r.record_date > (SELECT MAX(x.record_date) FROM fa_record x WHERE x.run_id=@run AND x.asset_id=@a) " +
                        "     OR (r.record_date = (SELECT MAX(x.record_date) FROM fa_record x WHERE x.run_id=@run AND x.asset_id=@a) AND r.id > (SELECT MAX(x.id) FROM fa_record x WHERE x.run_id=@run AND x.asset_id=@a))) LIMIT 1",
                        "@a", a, "@run", runId);
                    if (later != null) { tx.Rollback(); return "Asset " + FaDb.S(later) + " has a later value record. Reverse that first."; }
                }

                foreach (DataRow x in recs.Rows)
                {
                    var rev = new FaRecords.Rec();
                    rev.AssetId = FaDb.I(x["asset_id"]); rev.Type = "REVERSAL"; rev.ValueClass = "DEPRECIATION"; rev.Date = FaDb.D(x["record_date"]).Value;
                    rev.Before = FaDb.M(x["value_after"]); rev.After = FaDb.M(x["value_before"]); rev.Change = -FaDb.M(x["change_amount"]);
                    rev.Reason = "Run " + runId + " reversed: " + reason; rev.ReversesId = FaDb.L(x["id"]); rev.RunId = runId;
                    long revId = FaRecords.InsertRecord(c, tx, rev);
                    FaDb.Exec(c, tx, "UPDATE fa_record SET reversed_by_record_id=@r, dep_active=NULL WHERE id=@id", "@r", revId, "@id", FaDb.L(x["id"]));
                }
                foreach (int a in assets) FaPosting.Recompute(c, tx, a, "run " + runId + " reversed", false);
                FaDb.Exec(c, tx, "UPDATE fa_depreciation_run SET status='REVERSED', reversed_by=@u, reversed_at=NOW(), reverse_reason=@r WHERE id=@id",
                          "@u", FaAccess.Username(), "@r", FaAudit.Cut(reason, 1000), "@id", runId);
                FaAudit.Write(c, tx, "RUN", runId, null, "REVERSE", new { status = "POSTED" }, new { status = "REVERSED", assets = assets.Count }, reason,
                              "Depreciation run " + runId + " reversed (" + FaFmt.Plural(assets.Count, "asset", "assets") + ")");
                tx.Commit();
            }
            catch (MySqlException ex)
            {
                try { tx.Rollback(); } catch { }
                FaLog.Error("FaDepreciation.ReverseRun", ex);
                return "The run could not be reversed. Nothing was changed.";
            }
        }
        return null;
    }

    public static List<object> Runs(MySqlConnection c)
    {
        var l = new List<object>();
        long newest = FaDb.L(FaDb.Scalar(c, null, "SELECT MAX(id) FROM fa_depreciation_run WHERE status='POSTED'"));
        foreach (DataRow r in FaDb.Table(c, null, "SELECT * FROM fa_depreciation_run ORDER BY id DESC LIMIT 200").Rows)
        {
            var sc = FaJson.Parse(FaDb.S(r["scope_json"]));
            string scope = "All assets";
            int cp = FaJson.Int(sc, "campusId"), ct = FaJson.Int(sc, "categoryId");
            if (cp > 0 || ct > 0)
            {
                var parts = new List<string>();
                if (cp > 0) parts.Add(FaAssets.Title(FaDb.S(FaDb.Scalar(c, null, "SELECT campus_name FROM acad_campuses WHERE ID=@i", "@i", cp))));
                if (ct > 0) parts.Add(FaDb.S(FaDb.Scalar(c, null, "SELECT name FROM fa_category WHERE id=@i", "@i", ct)));
                scope = string.Join(", ", parts.ToArray());
            }
            l.Add(new
            {
                id = FaDb.L(r["id"]), finYear = FaDb.S(r["fin_year"]), periodEnd = FaFmt.Date(r["period_end"]), scope = scope,
                assets = FaDb.I(r["asset_count"]), total = FaDb.M(r["total_amount"]), status = FaDb.S(r["status"]),
                statusLabel = FaDb.S(r["status"]) == "POSTED" ? "Posted" : "Reversed", notes = FaDb.S(r["notes"]),
                postedBy = FaDb.S(r["posted_by"]), postedAt = FaFmt.Date(r["posted_at"]),
                reversedBy = FaDb.S(r["reversed_by"]), reversedAt = FaFmt.Date(r["reversed_at"]), reverseReason = FaDb.S(r["reverse_reason"]),
                canReverse = FaDb.S(r["status"]) == "POSTED" && FaDb.L(r["id"]) == newest
            });
        }
        return l;
    }

    /// <summary>
    /// The journal Finance posts by hand (plan 5.9): per category GL mapping, debit the expense account and
    /// credit the accumulated depreciation account. For one run, or for every live charge in a financial year.
    /// </summary>
    public static List<object> Journal(MySqlConnection c, long runId, string finYear)
    {
        string where = runId > 0 ? "r.run_id=@x AND r.record_type='DEPRECIATION'" : "r.fin_year=@x AND r.record_type='DEPRECIATION'";
        DataTable t = FaDb.Table(c, null,
            "SELECT COALESCE(NULLIF(sc.gl_expense_account,''), pc.gl_expense_account, '') exp_ac, COALESCE(NULLIF(sc.gl_accum_account,''), pc.gl_accum_account, '') acc_ac, " +
            "IFNULL(pc.name, sc.name) cat, SUM(-r.change_amount) amt FROM fa_record r JOIN fa_asset a ON a.id=r.asset_id " +
            "JOIN fa_category sc ON sc.id=a.category_id LEFT JOIN fa_category pc ON pc.id=sc.parent_id " +
            "WHERE " + where + " AND r.reversed_by_record_id IS NULL GROUP BY exp_ac, acc_ac, cat ORDER BY cat",
            "@x", runId > 0 ? (object)runId : finYear);
        var names = new Dictionary<string, string>();
        foreach (DataRow r in FaDb.Table(c, null, "SELECT AccountCode, AccountName FROM campus_dynamics_accounts.fin_subaccounts").Rows)
            names[FaDb.S(r[0])] = FaDb.S(r[1]);
        var dr = new Dictionary<string, decimal>(); var cr = new Dictionary<string, decimal>();
        var cats = new Dictionary<string, List<string>>();
        foreach (DataRow r in t.Rows)
        {
            string e = FaDb.S(r["exp_ac"]), a = FaDb.S(r["acc_ac"]); decimal m = FaDb.M(r["amt"]);
            if (e == "") e = "(no expense account)"; if (a == "") a = "(no accumulated depreciation account)";
            dr[e] = (dr.ContainsKey(e) ? dr[e] : 0m) + m;
            cr[a] = (cr.ContainsKey(a) ? cr[a] : 0m) + m;
            if (!cats.ContainsKey(a)) cats[a] = new List<string>();
            cats[a].Add(FaDb.S(r["cat"]));
        }
        var l = new List<object>();
        foreach (var kv in dr)
            l.Add(new { account = kv.Key, accountName = names.ContainsKey(kv.Key) ? names[kv.Key] : "", debit = kv.Value, credit = 0m, note = "Depreciation expense" });
        foreach (var kv in cr)
            l.Add(new { account = kv.Key, accountName = names.ContainsKey(kv.Key) ? names[kv.Key] : "", debit = 0m, credit = kv.Value, note = string.Join(", ", cats[kv.Key].ToArray()) });
        return l;
    }

    // ─────────────────────────── Year locks ───────────────────────────

    public static List<object> Locks(MySqlConnection c)
    {
        var have = new Dictionary<string, DataRow>();
        foreach (DataRow r in FaDb.Table(c, null, "SELECT * FROM fa_year_lock").Rows) have[FaDb.S(r["fin_year"])] = r;
        DateTime from = FaDb.D(FaDb.Scalar(c, null, "SELECT MIN(record_date) FROM fa_record")) ?? DateTime.Today;
        var years = FaFinYear.Range(from < DateTime.Today.AddYears(-3) ? from : DateTime.Today.AddYears(-3), DateTime.Today);
        foreach (string y in have.Keys) if (!years.Contains(y)) years.Add(y);
        years.Sort(); years.Reverse();
        var l = new List<object>();
        foreach (string y in years)
        {
            DataRow r; have.TryGetValue(y, out r);
            int recs = FaDb.I(FaDb.Scalar(c, null, "SELECT COUNT(*) FROM fa_record WHERE fin_year=@y", "@y", y));
            l.Add(new
            {
                finYear = y, locked = r != null && FaDb.I(r["is_locked"]) == 1, records = recs,
                lockedBy = r == null ? "" : FaDb.S(r["locked_by"]), lockedAt = r == null ? "" : FaFmt.Date(r["locked_at"]),
                reason = r == null ? "" : FaDb.S(r["reason"]),
                unlockedBy = r == null ? "" : FaDb.S(r["unlocked_by"]), unlockedAt = r == null ? "" : FaFmt.Date(r["unlocked_at"]),
                current = y == FaFinYear.Of(DateTime.Today)
            });
        }
        return l;
    }

    public static string SetLock(string finYear, bool locked, string reason)
    {
        if (string.IsNullOrEmpty(finYear) || finYear.Length != 9) return "Choose the financial year.";
        if ((reason ?? "").Trim().Length < 10) return "Give a reason of at least 10 characters.";
        using (var c = FaDb.Open())
        using (var tx = c.BeginTransaction())
        {
            DataTable t = FaDb.Table(c, tx, "SELECT * FROM fa_year_lock WHERE fin_year=@y FOR UPDATE", "@y", finYear);
            bool was = t.Rows.Count == 1 && FaDb.I(t.Rows[0]["is_locked"]) == 1;
            if (was == locked) { tx.Rollback(); return locked ? "That year is already locked." : "That year is not locked."; }
            if (locked)
                FaDb.Exec(c, tx,
                    "INSERT INTO fa_year_lock (fin_year, is_locked, reason, locked_by, locked_at) VALUES (@y,1,@r,@u,NOW()) " +
                    "ON DUPLICATE KEY UPDATE is_locked=1, reason=@r, locked_by=@u, locked_at=NOW(), unlocked_by=NULL, unlocked_at=NULL",
                    "@y", finYear, "@r", FaAudit.Cut(reason, 1000), "@u", FaAccess.Username());
            else
                FaDb.Exec(c, tx, "UPDATE fa_year_lock SET is_locked=0, unlocked_by=@u, unlocked_at=NOW() WHERE fin_year=@y",
                          "@u", FaAccess.Username(), "@y", finYear);
            FaAudit.Write(c, tx, "LOCK", 0, null, locked ? "LOCK" : "UNLOCK", new { finYear = finYear, locked = was }, new { finYear = finYear, locked = locked },
                          reason, "Financial year " + finYear + (locked ? " locked" : " unlocked"));
            tx.Commit();
        }
        return null;
    }
}
