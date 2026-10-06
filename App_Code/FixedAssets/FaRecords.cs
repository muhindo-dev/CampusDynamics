using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using MySql.Data.MySqlClient;

// =====================================================================
//  Fixed Assets: asset records (plan 2.3, 2.4, 5.7, 5.8).
//  Every change to an asset's value, status, location or custody is a
//  new row in fa_record. Rows are never edited or deleted; a mistake is
//  corrected by a reversal that points at the row it cancels.
// =====================================================================
public static class FaRecords
{
    public class Rec
    {
        public int AssetId;
        public string Type, ValueClass = "NONE";
        public DateTime Date;
        public DateTime? PeriodFrom, PeriodTo;
        public int? Months, Quantity, NewRemainingMonths;
        public bool DepActive;
        public decimal Before, After, Change;
        public decimal? CostAmount, Proceeds, GainLoss;
        public bool? IsCapital, VerifiedFound;
        public string StatusBefore, StatusAfter, FromLocation, ToLocation, DisposalMethod, Valuer, Condition;
        public int? FromCampusId, ToCampusId, FromDepartmentId, ToDepartmentId, FromCustodian, ToCustodian;
        public string Reason, Reference, ApprovalRef, ApprovedBy;
        public bool Approved;
        public long? RunId, ImportBatchId, ReversesId;
        public Dictionary<string, object> Details;
        public string DetailsRaw;
        public string ClientOpId;
    }

    public static long InsertRecord(MySqlConnection c, MySqlTransaction tx, Rec r)
    {
        string details = r.DetailsRaw;
        if (r.Details != null) details = FaJson.Ser(r.Details);
        string user = FaAccess.Username() == "" ? "system" : FaAccess.Username();
        return FaDb.Insert(c, tx,
            "INSERT INTO fa_record (asset_id, record_type, value_class, record_date, fin_year, period_from, period_to, months, dep_active, " +
            "value_before, value_after, change_amount, quantity, cost_amount, is_capital, status_before, status_after, from_campus_id, to_campus_id, " +
            "from_department_id, to_department_id, from_location, to_location, from_custodian_emp_id, to_custodian_emp_id, disposal_method, proceeds, " +
            "gain_loss, valuer, new_remaining_months, condition_grade, verified_found, reason, details_json, reference, approval_ref, approved_by, approved_at, " +
            "run_id, import_batch_id, reverses_record_id, recorded_by, recorded_role, recorded_at, client_op_id) VALUES " +
            "(@a,@t,@vc,@d,@fy,@pf,@pt,@m,@da,@vb,@va,@ch,@q,@ca,@cap,@sb,@sa,@fc,@tc,@fd,@td,@fl,@tl,@fcu,@tcu,@dm,@pr,@gl,@val,@nrm,@cg,@vf," +
            "@rs,@dj,@ref,@apr,@apb,@apt,@run,@imp,@rev,@u,@role,NOW(),@op)",
            "@a", r.AssetId, "@t", r.Type, "@vc", r.ValueClass, "@d", r.Date.Date, "@fy", FaFinYear.Of(r.Date),
            "@pf", FaDb.DbVal(r.PeriodFrom), "@pt", FaDb.DbVal(r.PeriodTo), "@m", FaDb.DbVal(r.Months),
            "@da", r.DepActive ? (object)1 : DBNull.Value, "@vb", r.Before, "@va", r.After, "@ch", r.Change,
            "@q", FaDb.DbVal(r.Quantity), "@ca", FaDb.DbVal(r.CostAmount), "@cap", r.IsCapital.HasValue ? (object)(r.IsCapital.Value ? 1 : 0) : DBNull.Value,
            "@sb", FaDb.NullIfEmpty(r.StatusBefore), "@sa", FaDb.NullIfEmpty(r.StatusAfter),
            "@fc", FaDb.DbVal(r.FromCampusId), "@tc", FaDb.DbVal(r.ToCampusId), "@fd", FaDb.DbVal(r.FromDepartmentId), "@td", FaDb.DbVal(r.ToDepartmentId),
            "@fl", FaDb.NullIfEmpty(FaAudit.Cut(r.FromLocation, 250)), "@tl", FaDb.NullIfEmpty(FaAudit.Cut(r.ToLocation, 250)),
            "@fcu", FaDb.DbVal(r.FromCustodian), "@tcu", FaDb.DbVal(r.ToCustodian), "@dm", FaDb.NullIfEmpty(r.DisposalMethod),
            "@pr", FaDb.DbVal(r.Proceeds), "@gl", FaDb.DbVal(r.GainLoss), "@val", FaDb.NullIfEmpty(FaAudit.Cut(r.Valuer, 150)),
            "@nrm", FaDb.DbVal(r.NewRemainingMonths), "@cg", FaDb.NullIfEmpty(r.Condition),
            "@vf", r.VerifiedFound.HasValue ? (object)(r.VerifiedFound.Value ? 1 : 0) : DBNull.Value,
            "@rs", FaDb.NullIfEmpty(FaAudit.Cut(r.Reason, 1000)), "@dj", FaDb.NullIfEmpty(details),
            "@ref", FaDb.NullIfEmpty(FaAudit.Cut(r.Reference, 150)), "@apr", FaDb.NullIfEmpty(FaAudit.Cut(r.ApprovalRef, 150)),
            "@apb", r.Approved ? (object)user : DBNull.Value, "@apt", r.Approved ? (object)DateTime.Now : DBNull.Value,
            "@run", FaDb.DbVal(r.RunId), "@imp", FaDb.DbVal(r.ImportBatchId), "@rev", FaDb.DbVal(r.ReversesId),
            "@u", user, "@role", FaAccess.Role(), "@op", string.IsNullOrEmpty(r.ClientOpId) ? (object)DBNull.Value : r.ClientOpId);
    }

    /// <summary>The permission a record type needs (plan section 6).</summary>
    public static string SlugFor(string type, bool capital)
    {
        switch (type)
        {
            case "DEPRECIATION": case "REVALUATION": case "APPRECIATION": case "ESTIMATE": return FaAccess.Value;
            case "MAINTENANCE": return capital ? FaAccess.Value : FaAccess.Transfer;
            case "TRANSFER": case "STATUS": case "VERIFICATION": return FaAccess.Transfer;
            case "DISPOSAL": case "VOID": return FaAccess.Dispose;
            default: return FaAccess.Edit;
        }
    }

    /// <summary>One line describing what a record did, for the ledger.</summary>
    public static string Describe(DataRow x)
    {
        string t = FaDb.S(x["record_type"]);
        var det = FaJson.Parse(FaDb.S(x["details_json"]));
        switch (t)
        {
            case "ACQUISITION":
                return "Acquired" + (FaDb.S(x["to_location"]) != "" ? ", placed at " + FaDb.S(x["to_location"]) : "") +
                       (FaDb.S(x["to_cus"]) != "" ? ", responsible person " + FaDb.S(x["to_cus"]) : "");
            case "OPENING":
            {
                var o = FaJson.Obj(det, "opening");
                return "Opening balance" + (FaJson.Str(o, "asAt") != "" ? " as at " + FaFmt.Date(FaJson.Date(o, "asAt")) : "") + ": cost " + FaFmt.Money(FaJson.Dec(o, "cost") ?? 0m) + " less accumulated depreciation " +
                       FaFmt.Money(FaJson.Dec(o, "accumulated") ?? 0m) + (FaJson.Bool(o, "calculated") ? " (calculated)" : "");
            }
            case "DEPRECIATION":
                return FaFmt.Date(x["period_from"]) + " to " + FaFmt.Date(x["period_to"]) +
                       (x["months"] != DBNull.Value ? " (" + FaFmt.Plural(FaDb.I(x["months"]), "month", "months") + ")" : "") +
                       (x["run_id"] != DBNull.Value ? ", run " + FaDb.L(x["run_id"]) : ", manual");
            case "REVALUATION": case "APPRECIATION":
                return "New value " + FaFmt.Money(x["value_after"]) + (FaDb.S(x["valuer"]) != "" ? " by " + FaDb.S(x["valuer"]) : "") +
                       (x["new_remaining_months"] != DBNull.Value ? ", remaining life " + FaFmt.Plural(FaDb.I(x["new_remaining_months"]), "month", "months") : "");
            case "TRANSFER":
            {
                var parts = new List<string>();
                if (FaDb.S(x["from_campus"]) != FaDb.S(x["to_campus"])) parts.Add("campus " + FaAssets.Title(FaDb.S(x["from_campus"])) + " to " + FaAssets.Title(FaDb.S(x["to_campus"])));
                if (FaDb.S(x["from_location"]) != FaDb.S(x["to_location"])) parts.Add("location " + Or(FaDb.S(x["from_location"])) + " to " + Or(FaDb.S(x["to_location"])));
                if (FaDb.S(x["from_dept"]) != FaDb.S(x["to_dept"])) parts.Add("department " + Or(FaAssets.Title(FaDb.S(x["from_dept"]))) + " to " + Or(FaAssets.Title(FaDb.S(x["to_dept"]))));
                if (FaDb.S(x["from_cus"]) != FaDb.S(x["to_cus"])) parts.Add("responsible person " + Or(FaDb.S(x["from_cus"])) + " to " + Or(FaDb.S(x["to_cus"])));
                return parts.Count == 0 ? "Transfer" : "Moved: " + string.Join("; ", parts.ToArray());
            }
            case "STATUS":
                return FaFmt.Status(FaDb.S(x["status_before"])) + " to " + FaFmt.Status(FaDb.S(x["status_after"]));
            case "MAINTENANCE":
                return (FaDb.I(x["is_capital"]) == 1 ? "Capital improvement" : "Repair or maintenance") + ", cost " + FaFmt.Money(x["cost_amount"]);
            case "VERIFICATION":
                return (FaDb.I(x["verified_found"]) == 1 ? "Found" : "Not found") +
                       (FaDb.S(x["condition_grade"]) != "" ? ", condition " + FaFmt.Condition(FaDb.S(x["condition_grade"])).ToLowerInvariant() : "") +
                       (FaJson.Str(det, "verifiedBy") != "" ? ", verified by " + FaJson.Str(det, "verifiedBy") : "");
            case "ESTIMATE":
                return FaJson.Str(det, "summary");
            case "DISPOSAL":
                return FaFmt.Disposal(FaDb.S(x["disposal_method"])) + ", proceeds " + FaFmt.Money(x["proceeds"]) +
                       ", " + (FaDb.M(x["gain_loss"]) >= 0 ? "gain " : "loss ") + FaFmt.Money(Math.Abs(FaDb.M(x["gain_loss"])));
            case "VOID":
                return "Entered in error";
            case "REVERSAL":
                return "Reverses record " + FaDb.L(x["reverses_record_id"]);
        }
        return "";
    }

    private static string Or(string s) { return s == "" ? "none" : s; }

    // ─────────────────────────── Reversal rules ───────────────────────────

    public static bool CanReverse(MySqlConnection c, MySqlTransaction tx, DataRow x, long latestValueId,
                                  Dictionary<string, long> latestByType, Dictionary<string, bool> rights, out string why)
    {
        why = "";
        string t = FaDb.S(x["record_type"]);
        if (t == "ACQUISITION" || t == "OPENING") { why = "An acquisition cannot be reversed. Void the asset instead."; return false; }
        if (t == "ESTIMATE") { why = "Record a new change of estimate instead."; return false; }
        if (t == "REVERSAL") { why = "A reversal cannot be reversed."; return false; }
        if (x["reversed_by_record_id"] != DBNull.Value) { why = "Already reversed."; return false; }
        if (x["run_id"] != DBNull.Value) { why = "This charge belongs to a depreciation run. Reverse the run instead."; return false; }
        string slug = SlugFor(t, FaDb.I(x["is_capital"]) == 1);
        bool ok;
        if (rights != null && rights.TryGetValue(slug, out ok) && !ok) { why = "You do not have permission."; return false; }
        if (rights == null && !FaAccess.Can(slug)) { why = "You do not have permission."; return false; }
        if (FaFinYear.IsLocked(c, tx, FaDb.S(x["fin_year"]))) { why = "Financial year " + FaDb.S(x["fin_year"]) + " is locked."; return false; }
        long id = FaDb.L(x["id"]);
        bool valueRec = FaPosting.IsValueType(t) || (t == "MAINTENANCE");
        if (t == "MAINTENANCE" && FaDb.I(x["is_capital"]) != 1) valueRec = false;
        if (valueRec && id != latestValueId) { why = "Only the latest value record can be reversed. Reverse the later ones first."; return false; }
        long lt;
        if (!valueRec && latestByType.TryGetValue(t, out lt) && lt != id) { why = "Only the latest record of this kind can be reversed."; return false; }
        return true;
    }

    // ─────────────────────────── Adding records ───────────────────────────

    public class Result
    {
        public string Error;
        public long RecordId;
        public bool NeedsCatchUp;
        public int CatchUpMonths;
        public decimal CatchUpAmount;
        public List<string> Warnings = new List<string>();
    }

    private static FaMath.AssetState State(MySqlConnection c, MySqlTransaction tx, DataRow a)
    {
        var s = new FaMath.AssetState();
        s.Method = FaDb.S(a["dep_method"]); s.RatePct = FaDb.M(a["dep_rate_pct"]); s.Residual = FaDb.M(a["residual_value"]);
        s.Value = FaDb.M(a["current_value"]); s.BaseValue = FaDb.M(a["base_value"]);
        s.DepStart = FaDb.D(a["dep_start_date"]) ?? DateTime.Today;
        s.BaseMonth = FaDb.D(a["base_date"]) ?? FaFinYear.MonthStart(s.DepStart);
        s.BaseRemainingMonths = FaDb.I(a["base_remaining_months"]);
        s.DepreciatedTo = FaDb.D(a["depreciated_to"]);
        int assetId = FaDb.I(a["id"]);
        s.ValueAtYearStart = delegate (DateTime fyStart) { return ValueAsAt(c, tx, assetId, fyStart.AddDays(-1)); };
        return s;
    }

    /// <summary>Carrying amount on a date: the latest value record on or before it.</summary>
    public static decimal ValueAsAt(MySqlConnection c, MySqlTransaction tx, int assetId, DateTime date)
    {
        object o = FaDb.Scalar(c, tx,
            "SELECT value_after FROM fa_record WHERE asset_id=@a AND value_class<>'NONE' AND record_date<=@d ORDER BY record_date DESC, id DESC LIMIT 1",
            "@a", assetId, "@d", date);
        return FaDb.M(o);
    }

    /// <summary>The charges needed to bring an asset's depreciation up to a month-end. Empty when none are due.</summary>
    public static List<FaMath.Slice> DuePlan(MySqlConnection c, MySqlTransaction tx, DataRow a, DateTime monthEnd)
    {
        if (!FaAssets.IsOpen(FaDb.S(a["status"])) || FaDb.S(a["dep_method"]) == "NONE") return new List<FaMath.Slice>();
        return FaMath.Plan(State(c, tx, a), monthEnd, FaFinYear.StartMonth);
    }

    /// <summary>Posts depreciation slices for one asset (catch-up or run). Returns the total charged.</summary>
    public static decimal PostSlices(MySqlConnection c, MySqlTransaction tx, int assetId, List<FaMath.Slice> slices, long? runId, string reason)
    {
        decimal total = 0m;
        foreach (var s in slices)
        {
            InsertRecord(c, tx, new Rec
            {
                AssetId = assetId, Type = "DEPRECIATION", ValueClass = "DEPRECIATION", Date = s.To, PeriodFrom = s.From, PeriodTo = s.To,
                Months = s.Months, DepActive = true, Before = s.Before, After = s.After, Change = -s.Charge, RunId = runId, Reason = reason
            });
            total += s.Charge;
        }
        return total;
    }

    private static bool Has(Dictionary<string, object> d, string k) { return d != null && d.ContainsKey(k); }

    /// <summary>
    /// Adds one record to an asset. All checks, any catch-up depreciation, the record, posting and audit
    /// happen in one transaction.
    /// </summary>
    public static Result Add(Dictionary<string, object> d)
    {
        var res = new Result();
        int assetId = FaJson.Int(d, "assetId");
        string type = FaJson.Str(d, "type").ToUpperInvariant();
        string reason = FaJson.Str(d, "reason");
        string reference = FaJson.Str(d, "reference");
        string clientOp = FaJson.Str(d, "clientOpId");
        DateTime? date = FaJson.Date(d, "date");
        bool confirmCatchUp = FaJson.Bool(d, "confirmCatchUp");
        bool capital = FaJson.Bool(d, "isCapital");

        string[] allowed = { "DEPRECIATION", "REVALUATION", "APPRECIATION", "TRANSFER", "STATUS", "MAINTENANCE", "VERIFICATION", "ESTIMATE", "DISPOSAL", "VOID" };
        if (Array.IndexOf(allowed, type) < 0) { res.Error = "Choose a record type."; return res; }
        if (!FaAccess.Can(SlugFor(type, capital))) { res.Error = "You do not have permission to record this."; return res; }
        if (!date.HasValue) { res.Error = "Enter the date."; return res; }
        if (date.Value > DateTime.Today) { res.Error = "The date cannot be in the future."; return res; }

        using (var c = FaDb.Open())
        using (var tx = c.BeginTransaction())
        {
            try
            {
                if (clientOp != "" && FaDb.Scalar(c, tx, "SELECT 1 FROM fa_record WHERE client_op_id=@o", "@o", clientOp) != null)
                { tx.Rollback(); res.Error = "This record has already been saved."; return res; }

                DataRow a = FaAssets.Row(c, tx, assetId, true);
                if (a == null) { tx.Rollback(); res.Error = "That asset no longer exists."; return res; }
                string status = FaDb.S(a["status"]);
                string assetNo = FaDb.S(a["asset_no"]);
                if (!FaAssets.IsOpen(status)) { tx.Rollback(); res.Error = "This asset is " + FaFmt.Status(status).ToLowerInvariant() + ". No further records can be added; reverse the closing record first."; return res; }
                if (date.Value < (FaDb.D(a["purchase_date"]) ?? DateTime.MinValue)) { tx.Rollback(); res.Error = "The date cannot be before the purchase date."; return res; }
                string fy = FaFinYear.Of(date.Value);
                if (FaFinYear.IsLocked(c, tx, fy)) { tx.Rollback(); res.Error = "Financial year " + fy + " is locked."; return res; }

                decimal value = FaDb.M(a["current_value"]);
                decimal residual = FaDb.M(a["residual_value"]);
                DateTime? depTo = FaDb.D(a["depreciated_to"]);
                DateTime? lastValueDate = FaDb.D(FaDb.Scalar(c, tx,
                    "SELECT MAX(record_date) FROM fa_record WHERE asset_id=@a AND value_class<>'NONE' AND reversed_by_record_id IS NULL AND record_type<>'REVERSAL'", "@a", assetId));

                // Required fields first, so the user is not asked to confirm catch-up depreciation for a record
                // that would then be refused.
                string pre = PreCheck(type, d, reason, reference, capital);
                if (pre != null) { tx.Rollback(); res.Error = pre; return res; }

                bool valueEvent = type == "REVALUATION" || type == "APPRECIATION" || type == "ESTIMATE" || type == "DISPOSAL" || (type == "MAINTENANCE" && capital) || type == "DEPRECIATION";
                if (valueEvent && type != "DEPRECIATION")
                {
                    if (depTo.HasValue && date.Value <= depTo.Value)
                    { tx.Rollback(); res.Error = "Depreciation has already been charged to " + FaFmt.Date(depTo) + ". Date this record after that, or reverse the later depreciation first."; return res; }
                    if (lastValueDate.HasValue && date.Value < lastValueDate.Value)
                    { tx.Rollback(); res.Error = "A later value record exists (" + FaFmt.Date(lastValueDate) + "). Records must follow in date order."; return res; }

                    // Bring depreciation up to the end of the previous month first (plan 5.7).
                    DateTime target = FaFinYear.MonthEnd(date.Value).AddMonths(-1);
                    target = FaFinYear.MonthEnd(target);
                    var due = DuePlan(c, tx, a, target);
                    if (due.Count > 0)
                    {
                        foreach (var s in due)
                            if (FaFinYear.IsLocked(c, tx, s.FinYear))
                            { tx.Rollback(); res.Error = "Depreciation for " + s.FinYear + " is outstanding but that year is locked."; return res; }
                        decimal amt = 0m; int months = 0;
                        foreach (var s in due) { amt += s.Charge; months += s.Months; }
                        if (!confirmCatchUp)
                        {
                            tx.Rollback();
                            res.NeedsCatchUp = true; res.CatchUpAmount = amt; res.CatchUpMonths = months;
                            return res;
                        }
                        PostSlices(c, tx, assetId, due, null, "Brought up to date before the " + FaFmt.RecordType(type).ToLowerInvariant());
                        FaPosting.Recompute(c, tx, assetId, "catch-up depreciation", false);
                        a = FaAssets.Row(c, tx, assetId, true);
                        value = FaDb.M(a["current_value"]);
                        depTo = FaDb.D(a["depreciated_to"]);
                    }
                }

                var r = new Rec();
                r.AssetId = assetId; r.Type = type; r.Date = date.Value; r.Reason = reason; r.Reference = reference;
                r.Before = value; r.After = value; r.Change = 0m; r.ClientOpId = clientOp;
                string summary = FaFmt.RecordType(type) + " on " + assetNo;

                switch (type)
                {
                    case "DEPRECIATION":
                    {
                        if (reason.Length < 10) { tx.Rollback(); res.Error = "A manual depreciation needs a reason of at least 10 characters."; return res; }
                        if (FaDb.S(a["dep_method"]) == "NONE") { tx.Rollback(); res.Error = "This asset is not depreciated."; return res; }
                        decimal? amt = FaJson.Dec(d, "amount");
                        DateTime pTo = FaFinYear.MonthEnd(date.Value);
                        DateTime pFrom = depTo.HasValue ? depTo.Value.AddDays(1) : FaFinYear.MonthStart(FaDb.D(a["dep_start_date"]).Value);
                        if (pFrom > pTo) { tx.Rollback(); res.Error = "Depreciation is already charged to " + FaFmt.Date(depTo) + "."; return res; }
                        if (FaFinYear.Of(pFrom) != FaFinYear.Of(pTo)) { tx.Rollback(); res.Error = "A manual charge must stay within one financial year. Use a depreciation run to catch up across years."; return res; }
                        if (!amt.HasValue || amt.Value <= 0m) { tx.Rollback(); res.Error = "Enter the amount."; return res; }
                        if (amt.Value > value - residual) { tx.Rollback(); res.Error = "The amount would take the value below the residual value of " + FaFmt.Money(residual) + "."; return res; }
                        amt = FaMath.Round0(amt.Value);
                        r.ValueClass = "DEPRECIATION"; r.Date = pTo; r.PeriodFrom = pFrom; r.PeriodTo = pTo; r.Months = FaFinYear.MonthsInclusive(pFrom, pTo);
                        r.DepActive = true; r.After = value - amt.Value; r.Change = -amt.Value;
                        break;
                    }
                    case "REVALUATION":
                    case "APPRECIATION":
                    {
                        decimal? nv = FaJson.Dec(d, "newValue");
                        if (!nv.HasValue || nv.Value < 0m) { tx.Rollback(); res.Error = "Enter the new value."; return res; }
                        nv = FaMath.Round0(nv.Value);
                        if (reason.Length < 10) { tx.Rollback(); res.Error = "Give a reason of at least 10 characters."; return res; }
                        if (reference == "") { tx.Rollback(); res.Error = "Enter the valuation report or approval reference."; return res; }
                        if (type == "APPRECIATION" && nv.Value <= value) { tx.Rollback(); res.Error = "An appreciation must increase the value above " + FaFmt.Money(value) + ". Use a revaluation for a decrease."; return res; }
                        if (nv.Value == value) { tx.Rollback(); res.Error = "The new value is the same as the current value."; return res; }
                        string valuer = FaJson.Str(d, "valuer");
                        if (type == "REVALUATION" && valuer == "") { tx.Rollback(); res.Error = "Enter the valuer or valuation committee."; return res; }
                        decimal? remYears = FaJson.Dec(d, "remainingYears");
                        if (remYears.HasValue && (remYears <= 0 || remYears > 200)) { tx.Rollback(); res.Error = "The remaining life must be between 0 and 200 years."; return res; }
                        r.ValueClass = "REVALUATION"; r.After = nv.Value; r.Change = nv.Value - value; r.Valuer = valuer;
                        r.NewRemainingMonths = remYears.HasValue ? (int?)FaMath.LifeMonths(remYears) : null;
                        r.ApprovalRef = FaJson.Str(d, "approvalRef"); r.Approved = r.ApprovalRef != "";
                        if (nv.Value < residual) res.Warnings.Add("The new value is below the residual value; depreciation will stop.");
                        break;
                    }
                    case "TRANSFER":
                    {
                        if (reason.Length < 5) { tx.Rollback(); res.Error = "Give a reason for the transfer."; return res; }
                        var to = FaJson.Obj(d, "to");
                        int? toCampus = Has(to, "campusId") ? FaJson.IntN(to, "campusId") : FaDb.I(a["campus_id"]);
                        int? toDept = Has(to, "departmentId") ? FaJson.IntN(to, "departmentId") : FaDb.IN(a["department_id"]);
                        int? toCus = Has(to, "custodianEmpId") ? FaJson.IntN(to, "custodianEmpId") : FaDb.IN(a["custodian_emp_id"]);
                        if (toDept == 0) toDept = null; if (toCus == 0) toCus = null;
                        string toB = Has(to, "building") ? FaJson.Str(to, "building") : FaDb.S(a["building"]);
                        string toR = Has(to, "room") ? FaJson.Str(to, "room") : FaDb.S(a["room"]);
                        int? toRid = Has(to, "roomId") ? FaJson.IntN(to, "roomId") : FaDb.IN(a["room_id"]);
                        if (toRid == 0) toRid = null;
                        if (!toCampus.HasValue || toCampus <= 0 || FaDb.Scalar(c, tx, "SELECT 1 FROM acad_campuses WHERE ID=@c AND ID<>0", "@c", toCampus.Value) == null)
                        { tx.Rollback(); res.Error = "Choose the campus."; return res; }
                        if (toDept.HasValue && FaDb.Scalar(c, tx, "SELECT 1 FROM hrm_departments WHERE ID=@d", "@d", toDept.Value) == null) { tx.Rollback(); res.Error = "Choose a department from the list."; return res; }
                        if (toCus.HasValue && FaDb.Scalar(c, tx, "SELECT 1 FROM hrm_employee WHERE empID=@e", "@e", toCus.Value) == null) { tx.Rollback(); res.Error = "Choose the responsible person from the staff list."; return res; }
                        string fromLoc = FaAssets.JoinLoc(FaDb.S(a["building"]), FaDb.S(a["room"])), toLoc = FaAssets.JoinLoc(toB, toR);
                        bool changed = toCampus != FaDb.I(a["campus_id"]) || toDept != FaDb.IN(a["department_id"]) || toCus != FaDb.IN(a["custodian_emp_id"]) || fromLoc != toLoc;
                        if (!changed) { tx.Rollback(); res.Error = "Nothing changes in this transfer."; return res; }
                        r.FromCampusId = FaDb.I(a["campus_id"]); r.ToCampusId = toCampus; r.FromDepartmentId = FaDb.IN(a["department_id"]); r.ToDepartmentId = toDept;
                        r.FromCustodian = FaDb.IN(a["custodian_emp_id"]); r.ToCustodian = toCus; r.FromLocation = fromLoc; r.ToLocation = toLoc;
                        r.Details = new Dictionary<string, object> {
                            { "from", new Dictionary<string, object> { { "building", FaDb.S(a["building"]) }, { "room", FaDb.S(a["room"]) }, { "roomId", FaDb.IN(a["room_id"]) },
                                                                       { "departmentId", FaDb.IN(a["department_id"]) }, { "custodianEmpId", FaDb.IN(a["custodian_emp_id"]) } } },
                            { "to", new Dictionary<string, object> { { "building", toB }, { "room", toR }, { "roomId", toRid }, { "departmentId", toDept }, { "custodianEmpId", toCus } } } };
                        break;
                    }
                    case "STATUS":
                    {
                        string ns = FaJson.Str(d, "status").ToUpperInvariant();
                        if (Array.IndexOf(FaAssets.OpenStatuses, ns) < 0) { tx.Rollback(); res.Error = "Choose In use, In store, Under repair or Lost. Disposal and write-off are recorded as a disposal."; return res; }
                        if (ns == status) { tx.Rollback(); res.Error = "The asset is already " + FaFmt.Status(ns).ToLowerInvariant() + "."; return res; }
                        if (reason.Length < 5) { tx.Rollback(); res.Error = "Give a reason for the status change."; return res; }
                        r.StatusBefore = status; r.StatusAfter = ns;
                        break;
                    }
                    case "MAINTENANCE":
                    {
                        decimal? cost = FaJson.Dec(d, "cost");
                        if (!cost.HasValue || cost.Value < 0m) { tx.Rollback(); res.Error = "Enter the cost (0 if none)."; return res; }
                        if (reason.Length < 5) { tx.Rollback(); res.Error = "Describe the work done."; return res; }
                        cost = FaMath.Round0(cost.Value);
                        r.CostAmount = cost; r.IsCapital = capital;
                        if (capital)
                        {
                            if (cost.Value <= 0m) { tx.Rollback(); res.Error = "A capital improvement must have a cost."; return res; }
                            r.ValueClass = "COST"; r.After = value + cost.Value; r.Change = cost.Value;
                        }
                        r.Details = new Dictionary<string, object> { { "supplier", FaJson.Str(d, "supplier") } };
                        break;
                    }
                    case "VERIFICATION":
                    {
                        string cond = FaJson.Str(d, "condition").ToUpperInvariant();
                        string[] conds = { "GOOD", "FAIR", "POOR", "UNSERVICEABLE", "" };
                        if (Array.IndexOf(conds, cond) < 0) { tx.Rollback(); res.Error = "Choose the condition."; return res; }
                        bool found = FaJson.Bool(d, "found");
                        r.VerifiedFound = found; r.Condition = cond;
                        r.Details = new Dictionary<string, object> { { "verifiedBy", FaJson.Str(d, "verifiedBy") } };
                        if (!found) res.Warnings.Add("The asset was not found. Record a status change to Lost if it cannot be traced.");
                        break;
                    }
                    case "ESTIMATE":
                    {
                        if (reason.Length < 10) { tx.Rollback(); res.Error = "Give a reason of at least 10 characters."; return res; }
                        string nm = FaJson.Str(d, "method").ToUpperInvariant();
                        if (nm != "SL" && nm != "RB" && nm != "NONE") { tx.Rollback(); res.Error = "Choose the depreciation method."; return res; }
                        decimal? life = FaJson.Dec(d, "lifeYears"), rate = FaJson.Dec(d, "ratePct"), resid = FaJson.Dec(d, "residualValue");
                        if (nm == "SL" && (!life.HasValue || life <= 0 || life > 200)) { tx.Rollback(); res.Error = "Enter the total useful life in years."; return res; }
                        if (nm == "RB" && (!rate.HasValue || rate <= 0 || rate > 100)) { tx.Rollback(); res.Error = "Enter the annual rate."; return res; }
                        if (!resid.HasValue) resid = residual;
                        if (resid < 0 || resid >= FaDb.M(a["cost_basis"]) && FaDb.M(a["cost_basis"]) > 0) { tx.Rollback(); res.Error = "The residual value must be at least 0 and less than the cost."; return res; }
                        if (nm == "SL") rate = FaMath.RateFromLife(life);
                        if (nm == "NONE") { life = null; rate = null; }
                        DateTime depStart = FaDb.D(a["dep_start_date"]).Value;
                        int used = depTo.HasValue ? FaFinYear.MonthsInclusive(depStart, depTo.Value) : 0;
                        int? remaining = null;
                        if (nm == "SL")
                        {
                            remaining = FaMath.LifeMonths(life) - used;
                            if (remaining <= 0) { tx.Rollback(); res.Error = "The new life is shorter than the " + FaFmt.Plural(used, "month", "months") + " already used."; return res; }
                        }
                        string oldM = FaDb.S(a["dep_method"]);
                        decimal? oldL = FaDb.MN(a["useful_life_years"]), oldR = FaDb.MN(a["dep_rate_pct"]);
                        decimal oldRes = FaDb.M(a["residual_value"]);
                        if (oldM == nm && oldL == life && oldR == rate && oldRes == resid.Value) { tx.Rollback(); res.Error = "Nothing changes in this estimate."; return res; }
                        var parts = new List<string>();
                        if (oldM != nm) parts.Add("method " + FaFmt.Method(oldM).ToLowerInvariant() + " to " + FaFmt.Method(nm).ToLowerInvariant());
                        if (oldL != life) parts.Add("life " + (oldL.HasValue ? oldL.Value.ToString("0.##") : "none") + " to " + (life.HasValue ? life.Value.ToString("0.##") : "none") + " years");
                        if (nm == "RB" && oldR != rate) parts.Add("rate " + (oldR.HasValue ? oldR.Value.ToString("0.##") : "none") + "% to " + rate.Value.ToString("0.##") + "%");
                        if (oldRes != resid.Value) parts.Add("residual " + FaFmt.Money(oldRes) + " to " + FaFmt.Money(resid.Value));
                        r.NewRemainingMonths = remaining;
                        r.Details = new Dictionary<string, object> {
                            { "summary", "Changed " + string.Join("; ", parts.ToArray()) },
                            { "old", new Dictionary<string, object> { { "method", oldM }, { "lifeYears", oldL }, { "ratePct", oldR }, { "residualValue", oldRes } } },
                            { "new", new Dictionary<string, object> { { "method", nm }, { "lifeYears", life }, { "ratePct", rate }, { "residualValue", resid.Value } } } };
                        FaDb.Exec(c, tx, "UPDATE fa_asset SET dep_method=@m, useful_life_years=@l, dep_rate_pct=@r, residual_value=@res WHERE id=@id",
                                  "@m", nm, "@l", FaDb.DbVal(life), "@r", FaDb.DbVal(rate), "@res", resid.Value, "@id", assetId);
                        break;
                    }
                    case "DISPOSAL":
                    {
                        string dm = FaJson.Str(d, "disposalMethod").ToUpperInvariant();
                        string[] dms = { "SALE", "DONATION", "SCRAP", "WRITE_OFF", "TRADE_IN", "TRANSFER_OUT" };
                        if (Array.IndexOf(dms, dm) < 0) { tx.Rollback(); res.Error = "Choose how the asset was disposed of."; return res; }
                        string apr = FaJson.Str(d, "approvalRef");
                        if (apr.Length < 3) { tx.Rollback(); res.Error = "Enter the approval reference (Board of Survey or Council minute)."; return res; }
                        if (reason.Length < 10) { tx.Rollback(); res.Error = "Give a reason of at least 10 characters."; return res; }
                        decimal proceeds = FaMath.Round0(FaJson.Dec(d, "proceeds") ?? 0m);
                        if (proceeds < 0m) { tx.Rollback(); res.Error = "Proceeds cannot be negative."; return res; }
                        if ((dm == "SCRAP" || dm == "WRITE_OFF" || dm == "DONATION") && proceeds > 0m && dm != "SCRAP") { tx.Rollback(); res.Error = "A " + FaFmt.Disposal(dm).ToLowerInvariant() + " has no proceeds."; return res; }
                        r.ValueClass = "DISPOSAL"; r.DisposalMethod = dm; r.Proceeds = proceeds; r.GainLoss = proceeds - value;
                        r.After = 0m; r.Change = -value; r.ApprovalRef = apr; r.Approved = true;
                        r.StatusBefore = status; r.StatusAfter = dm == "WRITE_OFF" ? "WRITTEN_OFF" : "DISPOSED";
                        summary = FaFmt.Disposal(dm) + " of " + assetNo + ", proceeds " + FaFmt.Money(proceeds);
                        break;
                    }
                    case "VOID":
                    {
                        if (reason.Length < 10) { tx.Rollback(); res.Error = "Explain why the asset was entered in error (at least 10 characters)."; return res; }
                        int hist = FaDb.I(FaDb.Scalar(c, tx,
                            "SELECT COUNT(*) FROM fa_record WHERE asset_id=@a AND reversed_by_record_id IS NULL AND record_type IN ('DEPRECIATION','REVALUATION','APPRECIATION','MAINTENANCE','ESTIMATE','DISPOSAL')", "@a", assetId));
                        if (hist > 0) { tx.Rollback(); res.Error = "This asset has value history and cannot be voided. Dispose of or write it off instead."; return res; }
                        r.ValueClass = "DISPOSAL"; r.After = 0m; r.Change = -value; r.StatusBefore = status; r.StatusAfter = "VOID"; r.Approved = true;
                        break;
                    }
                }

                res.RecordId = InsertRecord(c, tx, r);
                FaAudit.Write(c, tx, "RECORD", res.RecordId, assetId, type, null,
                              new { type = type, date = FaFmt.Iso(r.Date), before = r.Before, after = r.After, change = r.Change, reference = reference, approvalRef = r.ApprovalRef },
                              reason, summary);
                FaPosting.Recompute(c, tx, assetId, FaFmt.RecordType(type).ToLowerInvariant(), false);
                tx.Commit();
            }
            catch (MySqlException ex)
            {
                try { tx.Rollback(); } catch { }
                res.Error = ex.Number == 1062 ? "This would post the same depreciation period twice, so it was refused." : "The record could not be saved. Nothing was changed.";
                FaLog.Error("FaRecords.Add", ex);
            }
        }
        return res;
    }

    /// <summary>The required fields of each record type, checked before anything is posted.</summary>
    private static string PreCheck(string type, Dictionary<string, object> d, string reason, string reference, bool capital)
    {
        switch (type)
        {
            case "REVALUATION":
            case "APPRECIATION":
                if (!FaJson.Dec(d, "newValue").HasValue) return "Enter the new value.";
                if (type == "REVALUATION" && FaJson.Str(d, "valuer") == "") return "Enter the valuer or valuation committee.";
                if (reference == "") return "Enter the valuation report or approval reference.";
                if (reason.Length < 10) return "Give a reason of at least 10 characters.";
                break;
            case "ESTIMATE":
                if (reason.Length < 10) return "Give a reason of at least 10 characters.";
                break;
            case "DISPOSAL":
                if (FaJson.Str(d, "disposalMethod") == "") return "Choose how the asset was disposed of.";
                if (FaJson.Str(d, "approvalRef").Length < 3) return "Enter the approval reference (Board of Survey or Council minute).";
                if (reason.Length < 10) return "Give a reason of at least 10 characters.";
                break;
            case "MAINTENANCE":
                if (!FaJson.Dec(d, "cost").HasValue) return "Enter the cost (0 if none).";
                if (capital && FaJson.Dec(d, "cost").Value <= 0m) return "A capital improvement must have a cost.";
                if (reason.Length < 5) return "Describe the work done.";
                break;
        }
        return null;
    }

    /// <summary>Reverses one record (newest first rules apply). Returns null or the reason it was refused.</summary>
    public static string Reverse(long recordId, string reason, string clientOp)
    {
        if ((reason ?? "").Trim().Length < 10) return "Give a reason of at least 10 characters.";
        using (var c = FaDb.Open())
        using (var tx = c.BeginTransaction())
        {
            try
            {
                if (!string.IsNullOrEmpty(clientOp) && FaDb.Scalar(c, tx, "SELECT 1 FROM fa_record WHERE client_op_id=@o", "@o", clientOp) != null)
                { tx.Rollback(); return "Already reversed."; }
                DataTable t = FaDb.Table(c, tx, "SELECT * FROM fa_record WHERE id=@id", "@id", recordId);
                if (t.Rows.Count == 0) { tx.Rollback(); return "That record no longer exists."; }
                DataRow x = t.Rows[0];
                int assetId = FaDb.I(x["asset_id"]);
                DataRow a = FaAssets.Row(c, tx, assetId, true);   // lock the asset first

                long latestValue = FaDb.L(FaDb.Scalar(c, tx,
                    "SELECT id FROM fa_record WHERE asset_id=@a AND reversed_by_record_id IS NULL AND record_type<>'REVERSAL' " +
                    "AND (record_type IN ('ACQUISITION','OPENING','DEPRECIATION','REVALUATION','APPRECIATION','DISPOSAL','VOID') OR (record_type='MAINTENANCE' AND is_capital=1)) " +
                    "ORDER BY record_date DESC, id DESC LIMIT 1", "@a", assetId));
                var latestByType = new Dictionary<string, long>();
                foreach (DataRow y in FaDb.Table(c, tx,
                    "SELECT record_type, id FROM fa_record WHERE asset_id=@a AND reversed_by_record_id IS NULL AND record_type<>'REVERSAL' ORDER BY record_date DESC, id DESC", "@a", assetId).Rows)
                    if (!latestByType.ContainsKey(FaDb.S(y[0]))) latestByType[FaDb.S(y[0])] = FaDb.L(y[1]);

                string why;
                if (!CanReverse(c, tx, x, latestValue, latestByType, null, out why)) { tx.Rollback(); return why; }

                string type = FaDb.S(x["record_type"]);
                string status = FaDb.S(a["status"]);
                if (!FaAssets.IsOpen(status) && type != "DISPOSAL" && type != "VOID") { tx.Rollback(); return "Reverse the disposal first."; }

                var rev = new Rec();
                rev.AssetId = assetId; rev.Type = "REVERSAL"; rev.ValueClass = FaDb.S(x["value_class"]); rev.Date = FaDb.D(x["record_date"]).Value;
                rev.Before = FaDb.M(x["value_after"]); rev.After = FaDb.M(x["value_before"]); rev.Change = -FaDb.M(x["change_amount"]);
                rev.StatusBefore = FaDb.S(x["status_after"]); rev.StatusAfter = FaDb.S(x["status_before"]);
                rev.FromCampusId = FaDb.IN(x["to_campus_id"]); rev.ToCampusId = FaDb.IN(x["from_campus_id"]);
                rev.FromDepartmentId = FaDb.IN(x["to_department_id"]); rev.ToDepartmentId = FaDb.IN(x["from_department_id"]);
                rev.FromCustodian = FaDb.IN(x["to_custodian_emp_id"]); rev.ToCustodian = FaDb.IN(x["from_custodian_emp_id"]);
                rev.FromLocation = FaDb.S(x["to_location"]); rev.ToLocation = FaDb.S(x["from_location"]);
                rev.Reason = reason; rev.ReversesId = recordId; rev.ClientOpId = clientOp;
                long revId = InsertRecord(c, tx, rev);
                FaDb.Exec(c, tx, "UPDATE fa_record SET reversed_by_record_id=@r, dep_active=NULL WHERE id=@id", "@r", revId, "@id", recordId);
                FaAudit.Write(c, tx, "RECORD", revId, assetId, "REVERSE", new { record = recordId, type = type }, new { reversal = revId }, reason,
                              "Reversed " + FaFmt.RecordType(type).ToLowerInvariant() + " on " + FaDb.S(a["asset_no"]));
                FaPosting.Recompute(c, tx, assetId, "reversal", false);
                tx.Commit();
            }
            catch (MySqlException ex)
            {
                try { tx.Rollback(); } catch { }
                FaLog.Error("FaRecords.Reverse", ex);
                return "The reversal could not be saved. Nothing was changed.";
            }
        }
        return null;
    }

    /// <summary>
    /// Applies the same transfer or status change to many assets, one transaction per asset so one
    /// refusal does not stop the rest. Returns the per-asset outcome.
    /// </summary>
    public static object Batch(Dictionary<string, object> d)
    {
        var ids = FaJson.IntList(d, "ids");
        if (ids.Count == 0) return new { success = false, message = "Select at least one asset." };
        if (ids.Count > 300) return new { success = false, message = "Select at most 300 assets at a time." };
        string type = FaJson.Str(d, "type").ToUpperInvariant();
        if (type != "TRANSFER" && type != "STATUS" && type != "VERIFICATION") return new { success = false, message = "Choose the batch action." };
        int done = 0;
        var skipped = new List<object>();
        string op = FaJson.Str(d, "clientOpId");
        foreach (int id in ids)
        {
            var one = new Dictionary<string, object>(d);
            one["assetId"] = id;
            one["clientOpId"] = op == "" ? "" : FaAudit.Cut(op, 26) + "-" + id.ToString(CultureInfo.InvariantCulture);
            var r = Add(one);
            if (r.Error == null && !r.NeedsCatchUp) done++;
            else
            {
                string no = "";
                try { using (var c = FaDb.Open()) no = FaDb.S(FaDb.Scalar(c, null, "SELECT asset_no FROM fa_asset WHERE id=@id", "@id", id)); } catch { }
                skipped.Add(new { id = id, assetNo = no, why = r.Error ?? "Needs depreciation brought up to date first." });
            }
        }
        return new { success = true, done = done, skipped = skipped,
                     message = FaFmt.Plural(done, "asset", "assets") + " updated" + (skipped.Count > 0 ? ", " + skipped.Count + " skipped" : "") + "." };
    }
}

/// <summary>Writes unexpected errors to the server log without showing internals to the user.</summary>
public static class FaLog
{
    public static void Error(string where, Exception ex)
    {
        try
        {
            string dir = System.Web.HttpContext.Current.Server.MapPath("~/App_Data/FixedAssets");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "errors.log"),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + where + " " + FaAccess.Username() + " " + ex + Environment.NewLine);
        }
        catch { }
    }
}
