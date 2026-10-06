using System;
using System.Collections.Generic;
using System.Data;
using MySql.Data.MySqlClient;

// =====================================================================
//  Fixed Assets: posting (plan 2.11).
//
//  An asset's status, value, location and custody are never typed into
//  the asset row. They are the result of replaying its live records in
//  (record_date, id) order. Reversed records and the reversal rows that
//  cancel them are skipped as a pair, so a reversal undoes its record
//  exactly. Replay runs inside the caller's transaction at the end of
//  every write, so the row and the ledger can never disagree.
// =====================================================================
public class FaPosted
{
    public string Status = "IN_USE";
    public decimal Value, CostBasis, Accum, Reval, BaseValue;
    public DateTime? BaseMonth, DepreciatedTo, LastValuation, LastVerified, ClosedOn, CustodianSince;
    public int? BaseRemaining, DepartmentId, CustodianEmpId, RoomId;
    public int CampusId;
    public string Building, Room;
    public long? LastRecordId;
}

public static class FaPosting
{
    public static readonly string[] ValueTypes = { "ACQUISITION", "OPENING", "DEPRECIATION", "REVALUATION", "APPRECIATION", "MAINTENANCE", "DISPOSAL", "VOID" };

    public static bool IsValueType(string t) { return Array.IndexOf(ValueTypes, t) >= 0; }

    /// <summary>Months between two month starts (b minus a).</summary>
    private static int MonthsBetween(DateTime a, DateTime b) { return (b.Year - a.Year) * 12 + (b.Month - a.Month); }

    private static DateTime M1(DateTime d) { return new DateTime(d.Year, d.Month, 1); }

    /// <summary>
    /// The month a new straight-line base starts from after an event on <paramref name="eventDate"/>:
    /// the month after depreciation stops, or the event's month if nothing has been charged yet.
    /// </summary>
    public static DateTime NewBaseMonth(DateTime eventDate, DateTime? depreciatedTo, DateTime depStart)
    {
        if (depreciatedTo.HasValue) return M1(depreciatedTo.Value).AddMonths(1);
        DateTime m = M1(eventDate), s = M1(depStart);
        return m > s ? m : s;
    }

    /// <summary>Remaining months of life at a new base month, carried on from the previous base.</summary>
    public static int RemainingAt(FaPosted p, DateTime newBaseMonth)
    {
        if (!p.BaseRemaining.HasValue || !p.BaseMonth.HasValue) return 0;
        int r = p.BaseRemaining.Value - MonthsBetween(p.BaseMonth.Value, newBaseMonth);
        return r < 0 ? 0 : r;
    }

    /// <summary>Replays the ledger of one asset. Pure apart from the reads.</summary>
    public static FaPosted Replay(MySqlConnection c, MySqlTransaction tx, int assetId)
    {
        DataRow a = FaDb.Table(c, tx,
            "SELECT original_cost, useful_life_years, dep_start_date, campus_id, building, room, room_id, department_id, custodian_emp_id, custodian_since " +
            "FROM fa_asset WHERE id=@id", "@id", assetId).Rows[0];
        DateTime depStart = FaDb.D(a["dep_start_date"]) ?? DateTime.Today;
        int lifeMonths = FaMath.LifeMonths(FaDb.MN(a["useful_life_years"]));

        var p = new FaPosted();
        // Defaults if a record does not say otherwise.
        p.CampusId = FaDb.I(a["campus_id"]); p.Building = FaDb.S(a["building"]); p.Room = FaDb.S(a["room"]);
        p.RoomId = FaDb.IN(a["room_id"]); p.DepartmentId = FaDb.IN(a["department_id"]);
        p.CustodianEmpId = FaDb.IN(a["custodian_emp_id"]); p.CustodianSince = FaDb.D(a["custodian_since"]);

        DataTable t = FaDb.Table(c, tx,
            "SELECT * FROM fa_record WHERE asset_id=@id AND reversed_by_record_id IS NULL AND record_type<>'REVERSAL' " +
            "ORDER BY record_date, id", "@id", assetId);

        foreach (DataRow r in t.Rows)
        {
            string type = FaDb.S(r["record_type"]);
            DateTime date = FaDb.D(r["record_date"]) ?? DateTime.Today;
            decimal after = FaDb.M(r["value_after"]), change = FaDb.M(r["change_amount"]);
            p.LastRecordId = FaDb.L(r["id"]);
            Dictionary<string, object> det = FaJson.Parse(FaDb.S(r["details_json"]));

            switch (type)
            {
                case "ACQUISITION":
                    p.Value = after; p.CostBasis = after; p.Accum = 0m; p.Reval = 0m;
                    p.BaseValue = after; p.BaseMonth = M1(depStart); p.BaseRemaining = lifeMonths;
                    p.DepreciatedTo = null; p.ClosedOn = null;
                    if (FaDb.S(r["status_after"]) != "") p.Status = FaDb.S(r["status_after"]);
                    ApplyPlacement(p, r, det, date);
                    break;

                case "OPENING":
                    // Value as at the opening date, after the depreciation charged before the register began.
                    p.Value = after; p.CostBasis = FaDb.M(a["original_cost"]); p.Accum = p.CostBasis - after; p.Reval = 0m;
                    // Dated the day after the opening date: depreciation is already charged to that date.
                    p.DepreciatedTo = date.AddDays(-1);
                    p.BaseValue = after; p.BaseMonth = M1(date);
                    p.BaseRemaining = FaDb.IN(r["new_remaining_months"]) ?? 0;
                    p.ClosedOn = null;
                    if (FaDb.S(r["status_after"]) != "") p.Status = FaDb.S(r["status_after"]);
                    ApplyPlacement(p, r, det, date);
                    break;

                case "DEPRECIATION":
                    p.Value = after; p.Accum += -change;
                    DateTime? to = FaDb.D(r["period_to"]);
                    if (to.HasValue) p.DepreciatedTo = to;
                    break;

                case "REVALUATION":
                case "APPRECIATION":
                {
                    DateTime nb = NewBaseMonth(date, p.DepreciatedTo, depStart);
                    int rem = FaDb.IN(r["new_remaining_months"]) ?? RemainingAt(p, nb);
                    p.Value = after; p.Reval += change; p.Accum = 0m;
                    p.BaseValue = after; p.BaseMonth = nb; p.BaseRemaining = rem;
                    p.LastValuation = date;
                    break;
                }

                case "MAINTENANCE":
                    if (FaDb.I(r["is_capital"]) == 1 && change != 0m)
                    {
                        DateTime nb = NewBaseMonth(date, p.DepreciatedTo, depStart);
                        int rem = RemainingAt(p, nb);
                        p.Value = after; p.CostBasis += change;
                        p.BaseValue = after; p.BaseMonth = nb; p.BaseRemaining = rem;
                    }
                    break;

                case "ESTIMATE":
                {
                    DateTime nb = NewBaseMonth(date, p.DepreciatedTo, depStart);
                    p.BaseValue = p.Value; p.BaseMonth = nb;
                    p.BaseRemaining = FaDb.IN(r["new_remaining_months"]) ?? RemainingAt(p, nb);
                    break;
                }

                case "TRANSFER":
                    ApplyPlacement(p, r, det, date);
                    break;

                case "STATUS":
                    if (FaDb.S(r["status_after"]) != "") p.Status = FaDb.S(r["status_after"]);
                    break;

                case "VERIFICATION":
                    p.LastVerified = date;
                    break;

                case "DISPOSAL":
                case "VOID":
                    p.Value = after;
                    p.Status = FaDb.S(r["status_after"]) != "" ? FaDb.S(r["status_after"]) : (type == "VOID" ? "VOID" : "DISPOSED");
                    p.ClosedOn = date;
                    break;
            }
        }
        return p;
    }

    /// <summary>Applies the "to" side of a placement (acquisition, opening or transfer).</summary>
    private static void ApplyPlacement(FaPosted p, DataRow r, Dictionary<string, object> det, DateTime date)
    {
        if (r["to_campus_id"] != DBNull.Value) p.CampusId = FaDb.I(r["to_campus_id"]);
        if (r["to_department_id"] != DBNull.Value) p.DepartmentId = FaDb.IN(r["to_department_id"]);
        var to = FaJson.Obj(det, "to");
        if (to.Count > 0)
        {
            if (to.ContainsKey("building")) p.Building = FaJson.Str(to, "building");
            if (to.ContainsKey("room")) p.Room = FaJson.Str(to, "room");
            if (to.ContainsKey("roomId")) p.RoomId = FaJson.IntN(to, "roomId");
            if (to.ContainsKey("departmentId")) p.DepartmentId = FaJson.IntN(to, "departmentId");
            if (to.ContainsKey("custodianEmpId"))
            {
                int? nc = FaJson.IntN(to, "custodianEmpId");
                if (nc != p.CustodianEmpId) { p.CustodianEmpId = nc; p.CustodianSince = nc.HasValue ? (DateTime?)date : null; }
            }
        }
        else if (r["to_custodian_emp_id"] != DBNull.Value)
        {
            int? nc = FaDb.IN(r["to_custodian_emp_id"]);
            if (nc != p.CustodianEmpId) { p.CustodianEmpId = nc; p.CustodianSince = date; }
        }
    }

    /// <summary>
    /// Replays the ledger and writes the result to the asset row, with a before/after audit of what changed.
    /// Call at the end of every write, inside the same transaction.
    /// </summary>
    public static FaPosted Recompute(MySqlConnection c, MySqlTransaction tx, int assetId, string summary, bool logActivity = true)
    {
        DataRow b = FaDb.Table(c, tx, "SELECT * FROM fa_asset WHERE id=@id FOR UPDATE", "@id", assetId).Rows[0];
        FaPosted p = Replay(c, tx, assetId);

        FaDb.Exec(c, tx,
            "UPDATE fa_asset SET status=@st, current_value=@v, cost_basis=@cb, accum_depreciation=@ad, reval_surplus=@rs, " +
            "base_value=@bv, base_date=@bd, base_remaining_months=@br, depreciated_to=@dt, last_valuation_date=@lv, last_verified_date=@lvf, " +
            "closed_on=@co, last_record_id=@lr, campus_id=@cp, building=@bl, room=@rm, room_id=@rid, department_id=@dep, " +
            "custodian_emp_id=@cus, custodian_since=@css, updated_by=@u, updated_at=NOW(), row_version=row_version+1 WHERE id=@id",
            "@st", p.Status, "@v", p.Value, "@cb", p.CostBasis, "@ad", p.Accum, "@rs", p.Reval, "@bv", p.BaseValue,
            "@bd", FaDb.DbVal(p.BaseMonth), "@br", FaDb.DbVal(p.BaseRemaining), "@dt", FaDb.DbVal(p.DepreciatedTo),
            "@lv", FaDb.DbVal(p.LastValuation), "@lvf", FaDb.DbVal(p.LastVerified), "@co", FaDb.DbVal(p.ClosedOn),
            "@lr", FaDb.DbVal(p.LastRecordId), "@cp", p.CampusId, "@bl", FaDb.NullIfEmpty(p.Building), "@rm", FaDb.NullIfEmpty(p.Room),
            "@rid", FaDb.DbVal(p.RoomId), "@dep", FaDb.DbVal(p.DepartmentId), "@cus", FaDb.DbVal(p.CustodianEmpId),
            "@css", FaDb.DbVal(p.CustodianSince), "@u", FaAccess.Username() == "" ? "system" : FaAccess.Username(), "@id", assetId);

        var before = PostedSnapshot(b);
        var after = PostedSnapshot(FaDb.Table(c, tx, "SELECT * FROM fa_asset WHERE id=@id", "@id", assetId).Rows[0]);
        Dictionary<string, object> db, da;
        FaAudit.Diff(before, after, out db, out da);
        if (da.Count > 0)
            FaAudit.Write(c, tx, "ASSET", assetId, assetId, "POST", db, da, null,
                          "Asset " + FaDb.S(b["asset_no"]) + " posted" + (string.IsNullOrEmpty(summary) ? "" : ": " + summary), logActivity);
        return p;
    }

    private static Dictionary<string, object> PostedSnapshot(DataRow r)
    {
        var d = new Dictionary<string, object>();
        string[] f = { "status", "current_value", "cost_basis", "accum_depreciation", "reval_surplus", "base_value", "base_date",
                       "base_remaining_months", "depreciated_to", "last_valuation_date", "last_verified_date", "closed_on",
                       "campus_id", "building", "room", "room_id", "department_id", "custodian_emp_id", "custodian_since" };
        foreach (string k in f)
        {
            object v = r[k];
            if (v is DateTime) v = ((DateTime)v).ToString("yyyy-MM-dd");
            else if (v == DBNull.Value) v = null;
            d[k] = v;
        }
        return d;
    }

    /// <summary>Admin check: replays every asset without writing and lists any posted field that disagrees.</summary>
    public static List<object> Integrity()
    {
        var outp = new List<object>();
        using (var c = FaDb.Open())
        {
            DataTable t = FaDb.Table(c, null, "SELECT id, asset_no, status, current_value, accum_depreciation, depreciated_to, campus_id, custodian_emp_id FROM fa_asset");
            foreach (DataRow r in t.Rows)
            {
                int id = FaDb.I(r["id"]);
                FaPosted p = Replay(c, null, id);
                var diffs = new List<string>();
                if (p.Status != FaDb.S(r["status"])) diffs.Add("status " + FaDb.S(r["status"]) + " should be " + p.Status);
                if (p.Value != FaDb.M(r["current_value"])) diffs.Add("value " + FaFmt.Money(r["current_value"]) + " should be " + FaFmt.Money(p.Value));
                if (p.Accum != FaDb.M(r["accum_depreciation"])) diffs.Add("accumulated depreciation " + FaFmt.Money(r["accum_depreciation"]) + " should be " + FaFmt.Money(p.Accum));
                if (FaFmt.Iso(p.DepreciatedTo) != FaFmt.Iso(r["depreciated_to"])) diffs.Add("depreciated to " + FaFmt.Iso(r["depreciated_to"]) + " should be " + FaFmt.Iso(p.DepreciatedTo));
                if (p.CampusId != FaDb.I(r["campus_id"])) diffs.Add("campus");
                if (p.CustodianEmpId != FaDb.IN(r["custodian_emp_id"])) diffs.Add("responsible person");
                if (diffs.Count > 0) outp.Add(new { id = id, assetNo = FaDb.S(r["asset_no"]), problems = string.Join("; ", diffs.ToArray()) });
            }
        }
        return outp;
    }
}
