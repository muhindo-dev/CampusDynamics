using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using MySql.Data.MySqlClient;

// =====================================================================
//  Fixed Assets: dashboard figures (plan 4.1). One call returns
//  everything, because PageMethods serialise per session.
//  Values are as at the "to" date: today's figures come from the posted
//  columns; an earlier date is computed from the records.
// =====================================================================
public static class FaDashboard
{
    public static object Build(Dictionary<string, object> f)
    {
        int campus = FaJson.Int(f, "campusId"), cat = FaJson.Int(f, "categoryId"), sub = FaJson.Int(f, "subCategoryId");
        DateTime to = FaJson.Date(f, "to") ?? DateTime.Today;
        if (to > DateTime.Today) to = DateTime.Today;
        DateTime from = FaJson.Date(f, "from") ?? FaFinYear.Start(to);
        bool historic = to < DateTime.Today;

        var w = new StringBuilder(" WHERE a.status<>'VOID' ");
        var prm = new List<object>();
        if (campus > 0) { w.Append(" AND a.campus_id=@cp"); prm.Add("@cp"); prm.Add(campus); }
        if (sub > 0) { w.Append(" AND a.category_id=@sc"); prm.Add("@sc"); prm.Add(sub); }
        else if (cat > 0) { w.Append(" AND sc.parent_id=@ct"); prm.Add("@ct"); prm.Add(cat); }
        string baseW = w.ToString();
        var basePrm = new List<object>(prm);

        // Assets held on the "to" date.
        string held = baseW + " AND a.purchase_date<=@to AND (a.closed_on IS NULL OR a.closed_on>@to)";
        prm.Add("@to"); prm.Add(to);
        prm.Add("@from"); prm.Add(from);
        string value = historic
            ? "IFNULL((SELECT x.value_after FROM fa_record x WHERE x.asset_id=a.id AND x.value_class<>'NONE' AND x.record_date<=@to ORDER BY x.record_date DESC, x.id DESC LIMIT 1),0)"
            : "a.current_value";
        string accum = historic
            ? "IFNULL((SELECT -SUM(x.change_amount) FROM fa_record x WHERE x.asset_id=a.id AND x.value_class='DEPRECIATION' AND x.record_date<=@to),0)"
            : "a.accum_depreciation";
        const string from_ = " FROM fa_asset a JOIN fa_category sc ON sc.id=a.category_id LEFT JOIN fa_category pc ON pc.id=sc.parent_id ";
        object[] p = prm.ToArray();

        using (var c = FaDb.Open())
        {
            // Figures
            DataRow t = FaDb.Table(c, null, "SELECT COUNT(*) n, IFNULL(SUM(a.original_cost),0) cost, IFNULL(SUM(" + value + "),0) val, IFNULL(SUM(" + accum + "),0) ad, " +
                                            "IFNULL(SUM(a.reval_surplus),0) rs" + from_ + held, p).Rows[0];
            DataRow disp = FaDb.Table(c, null,
                "SELECT COUNT(*) n, IFNULL(SUM(r.proceeds),0) pr, IFNULL(SUM(r.gain_loss),0) gl FROM fa_record r JOIN fa_asset a ON a.id=r.asset_id " +
                "JOIN fa_category sc ON sc.id=a.category_id" + baseW + " AND r.record_type='DISPOSAL' AND r.reversed_by_record_id IS NULL AND r.record_date BETWEEN @from AND @to", p).Rows[0];
            DataRow adds = FaDb.Table(c, null, "SELECT COUNT(*) n, IFNULL(SUM(a.original_cost),0) cost" + from_ + baseW + " AND a.purchase_date BETWEEN @from AND @to", p).Rows[0];
            DataRow dep = FaDb.Table(c, null,
                "SELECT IFNULL(-SUM(r.change_amount),0) d FROM fa_record r JOIN fa_asset a ON a.id=r.asset_id JOIN fa_category sc ON sc.id=a.category_id" + baseW +
                " AND r.value_class='DEPRECIATION' AND r.record_date BETWEEN @from AND @to", p).Rows[0];

            // By status (current status of assets bought by the date)
            var byStatus = new List<object>();
            var stCounts = new Dictionary<string, decimal[]>();
            foreach (DataRow r in FaDb.Table(c, null, "SELECT a.status, COUNT(*), IFNULL(SUM(a.current_value),0)" + from_ + baseW + " AND a.purchase_date<=@to GROUP BY a.status", p).Rows)
                stCounts[FaDb.S(r[0])] = new decimal[] { FaDb.M(r[1]), FaDb.M(r[2]) };
            foreach (string s in new[] { "IN_USE", "IN_STORE", "UNDER_REPAIR", "LOST", "DISPOSED", "WRITTEN_OFF" })
            {
                decimal[] v; stCounts.TryGetValue(s, out v);
                byStatus.Add(new { status = s, label = FaFmt.Status(s), count = v == null ? 0 : (int)v[0], value = v == null ? 0m : v[1] });
            }

            var byCat = new List<object>();
            foreach (DataRow r in FaDb.Table(c, null, "SELECT pc.id, pc.code, pc.name, COUNT(*) n, SUM(a.original_cost) cost, SUM(" + value + ") v" + from_ + held +
                                                       " GROUP BY pc.id ORDER BY v DESC", p).Rows)
                byCat.Add(new { id = FaDb.I(r["id"]), code = FaDb.S(r["code"]), name = FaDb.S(r["name"]), count = FaDb.I(r["n"]), cost = FaDb.M(r["cost"]), value = FaDb.M(r["v"]) });

            var byCampus = new List<object>();
            foreach (DataRow r in FaDb.Table(c, null, "SELECT a.campus_id, IFNULL(cp.campus_name,'') nm, COUNT(*) n, SUM(" + value + ") v" + from_ +
                                                       " LEFT JOIN acad_campuses cp ON cp.ID=a.campus_id" + held + " GROUP BY a.campus_id ORDER BY nm", p).Rows)
                byCampus.Add(new { id = FaDb.I(r["campus_id"]), name = FaAssets.Title(FaDb.S(r["nm"])).Replace(" Campus", ""), count = FaDb.I(r["n"]), value = FaDb.M(r["v"]) });

            // Acquisitions per financial year, and book value at each year end (the trend).
            var byYear = new List<object>();
            DateTime first = FaDb.D(FaDb.Scalar(c, null, "SELECT MIN(a.purchase_date)" + from_ + baseW, basePrm.ToArray())) ?? DateTime.Today;
            if (first < to.AddYears(-12)) first = to.AddYears(-12);
            foreach (string y in FaFinYear.Range(first, to))
            {
                DateTime ys = FaFinYear.StartOfLabel(y), ye = FaFinYear.EndOfLabel(y);
                DateTime at = ye > to ? to : ye;
                var pp = new List<object>(basePrm); pp.Add("@ys"); pp.Add(ys); pp.Add("@ye"); pp.Add(ye); pp.Add("@at"); pp.Add(at);
                DataRow acq = FaDb.Table(c, null, "SELECT COUNT(*), IFNULL(SUM(a.original_cost),0)" + from_ + baseW + " AND a.purchase_date BETWEEN @ys AND @ye", pp.ToArray()).Rows[0];
                object bv = FaDb.Scalar(c, null,
                    "SELECT IFNULL(SUM((SELECT x.value_after FROM fa_record x WHERE x.asset_id=a.id AND x.value_class<>'NONE' AND x.record_date<=@at ORDER BY x.record_date DESC, x.id DESC LIMIT 1)),0)" +
                    from_ + baseW + " AND a.purchase_date<=@at", pp.ToArray());
                byYear.Add(new { finYear = y, count = FaDb.I(acq[0]), cost = FaDb.M(acq[1]), bookValue = FaDb.M(bv), partial = ye > to });
            }

            var top = new List<object>();
            foreach (DataRow r in FaDb.Table(c, null, "SELECT a.id, a.asset_no, a.name, sc.name sub, pc.name cat, " + value + " v" + from_ + held + " ORDER BY v DESC LIMIT 10", p).Rows)
                top.Add(new { id = FaDb.I(r["id"]), assetNo = FaDb.S(r["asset_no"]), name = FaDb.S(r["name"]), category = FaDb.S(r["cat"]) + " / " + FaDb.S(r["sub"]), value = FaDb.M(r["v"]) });

            // Action lists (always as of today: they are things to do now).
            var actions = new Dictionary<string, object>();
            foreach (string flag in new[] { "no_custodian", "not_tagged", "revaluation_due", "life_ended", "not_verified" })
            {
                var ff = new Dictionary<string, object>(); ff["flag"] = flag; ff["status"] = "OPEN";
                if (campus > 0) ff["campusId"] = campus; if (cat > 0) ff["categoryId"] = cat; if (sub > 0) ff["subCategoryId"] = sub;
                ff["sort"] = flag == "life_ended" || flag == "revaluation_due" ? "purchase" : "value"; ff["dir"] = flag == "life_ended" || flag == "revaluation_due" ? "asc" : "desc";
                int n; decimal a1, a2;
                var rows = new List<object>();
                foreach (DataRow r in FaAssets.Query(c, ff, 1, 5, out n, out a1, out a2).Rows)
                    rows.Add(new { id = FaDb.I(r["id"]), assetNo = FaDb.S(r["asset_no"]), name = FaDb.S(r["name"]) });
                actions[flag] = new { label = FaReports.FlagLabel(flag), count = n, rows = rows };
            }

            decimal cost = FaDb.M(t["cost"]), val = FaDb.M(t["val"]);
            return new
            {
                success = true, asAt = FaFmt.Date(to), from = FaFmt.Date(from), historic = historic,
                tiles = new
                {
                    count = FaDb.I(t["n"]), cost = cost, value = val, accumDep = FaDb.M(t["ad"]), revalSurplus = FaDb.M(t["rs"]),
                    additionsCount = FaDb.I(adds["n"]), additionsCost = FaDb.M(adds["cost"]), depreciation = FaDb.M(dep["d"]),
                    disposedCount = FaDb.I(disp["n"]), disposedProceeds = FaDb.M(disp["pr"]), disposedGainLoss = FaDb.M(disp["gl"]),
                    changePct = cost == 0 ? 0m : Math.Round((val - cost) / cost * 100m, 1)
                },
                byStatus = byStatus, byCategory = byCat, byCampus = byCampus, byYear = byYear, top = top, actions = actions
            };
        }
    }
}
