using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;

// =====================================================================
//  Fixed Assets: the register (plan 2.2 and 4.3).
//  Create, edit, number, list and read assets. Status, value, location
//  and custody are never written here directly: creation writes an
//  Acquisition (or Opening) record and posting derives the row.
// =====================================================================
public static class FaAssets
{
    public static readonly string[] OpenStatuses = { "IN_USE", "IN_STORE", "UNDER_REPAIR", "LOST" };
    private static readonly Regex AssetNoRx = new Regex(@"^[A-Za-z0-9][A-Za-z0-9\-/\.]{2,39}$");

    public static bool IsOpen(string status) { return Array.IndexOf(OpenStatuses, status) >= 0; }

    // ─────────────────────────── Numbering ───────────────────────────

    public static string Format(FaCategory sub, long seq)
    {
        string prefix = FaSettings.Get("asset_no_prefix", "MRU");
        string cat = sub.Parent == null ? sub.Code : sub.Parent.Code;
        return prefix + "-" + cat + "-" + sub.Code + "-" + seq.ToString("00000", CultureInfo.InvariantCulture);
    }

    /// <summary>Preview of the next number for a sub-category (claimed only on save).</summary>
    public static string Preview(MySqlConnection c, MySqlTransaction tx, int subId)
    {
        var sub = FaCategories.GetLite(c, tx, subId);
        if (sub == null || sub.ParentId == 0) return "";
        long seq = sub.NextSeq;
        // Skip numbers already taken (for example typed by hand or imported).
        for (int i = 0; i < 1000; i++)
        {
            string n = Format(sub, seq);
            if (FaDb.Scalar(c, tx, "SELECT 1 FROM fa_asset WHERE asset_no=@n", "@n", n) == null) return n;
            seq++;
        }
        return Format(sub, seq);
    }

    /// <summary>Claims the next free number, row-locking the sub-category so two clerks never get the same one.</summary>
    public static string Claim(MySqlConnection c, MySqlTransaction tx, FaCategory sub)
    {
        for (int i = 0; i < 1000; i++)
        {
            FaDb.Exec(c, tx, "UPDATE fa_category SET next_seq = LAST_INSERT_ID(next_seq + 1) WHERE id=@id", "@id", sub.Id);
            long seq = FaDb.L(FaDb.Scalar(c, tx, "SELECT LAST_INSERT_ID()")) - 1;
            string n = Format(sub, seq);
            if (FaDb.Scalar(c, tx, "SELECT 1 FROM fa_asset WHERE asset_no=@n", "@n", n) == null) return n;
        }
        throw new Exception("Could not find a free asset number.");
    }

    // ─────────────────────────── Reading ───────────────────────────

    public static DataRow Row(MySqlConnection c, MySqlTransaction tx, int id, bool forUpdate)
    {
        DataTable t = FaDb.Table(c, tx, "SELECT * FROM fa_asset WHERE id=@id" + (forUpdate ? " FOR UPDATE" : ""), "@id", id);
        return t.Rows.Count == 0 ? null : t.Rows[0];
    }

    public const string ListFrom =
        " FROM fa_asset a JOIN fa_category sc ON sc.id=a.category_id LEFT JOIN fa_category pc ON pc.id=sc.parent_id " +
        " LEFT JOIN acad_campuses cp ON cp.ID=a.campus_id LEFT JOIN hrm_departments d ON d.ID=a.department_id " +
        " LEFT JOIN hrm_employee e ON e.empID=a.custodian_emp_id ";

    public const string ListCols =
        "a.id, a.asset_no, a.tag_no, a.name, a.description, a.serial_no, a.make, a.model, a.quantity, a.asset_type, a.category_id, " +
        "sc.code sub_code, sc.name sub_name, pc.id cat_id, pc.code cat_code, pc.name cat_name, a.campus_id, IFNULL(cp.campus_name,'') campus_name, " +
        "a.building, a.room, a.department_id, IFNULL(d.dept_name,'') dept_name, a.custodian_emp_id, IFNULL(e.emp_name,'') custodian_name, " +
        "IFNULL(e.EMP_CODE,'') custodian_code, a.custodian_since, a.supplier_name, a.purchase_date, a.invoice_ref, a.order_ref, a.funding_source, " +
        "a.warranty_expiry, a.original_cost, a.cost_basis, a.current_value, a.accum_depreciation, a.reval_surplus, a.residual_value, " +
        "a.dep_method, a.useful_life_years, a.dep_rate_pct, a.status, a.depreciated_to, a.last_valuation_date, a.last_verified_date, a.closed_on, " +
        "a.created_at, a.created_by";

    /// <summary>Builds WHERE and parameters for the register filters (shared by the list, exports and reports).</summary>
    public static string Where(Dictionary<string, object> f, List<object> prm)
    {
        var w = new StringBuilder(" WHERE 1=1 ");
        int campus = FaJson.Int(f, "campusId"), cat = FaJson.Int(f, "categoryId"), sub = FaJson.Int(f, "subCategoryId");
        int dept = FaJson.Int(f, "departmentId"), cus = FaJson.Int(f, "custodianEmpId"), id = FaJson.Int(f, "assetId");
        string status = FaJson.Str(f, "status").ToUpperInvariant(), q = FaJson.Str(f, "q"), flag = FaJson.Str(f, "flag");
        string fy = FaJson.Str(f, "finYear");
        DateTime? from = FaJson.Date(f, "from"), to = FaJson.Date(f, "to");

        if (id > 0) { w.Append(" AND a.id=@fid"); prm.Add("@fid"); prm.Add(id); }
        var ids = FaJson.IntList(f, "ids");
        if (ids.Count > 0)
        {
            var parts = new List<string>();
            foreach (int x in ids) parts.Add(x.ToString(CultureInfo.InvariantCulture));
            w.Append(" AND a.id IN (" + string.Join(",", parts.ToArray()) + ")");
        }
        if (campus > 0) { w.Append(" AND a.campus_id=@fcp"); prm.Add("@fcp"); prm.Add(campus); }
        if (sub > 0) { w.Append(" AND a.category_id=@fsc"); prm.Add("@fsc"); prm.Add(sub); }
        else if (cat > 0) { w.Append(" AND sc.parent_id=@fct"); prm.Add("@fct"); prm.Add(cat); }
        if (dept > 0) { w.Append(" AND a.department_id=@fdp"); prm.Add("@fdp"); prm.Add(dept); }
        if (cus > 0) { w.Append(" AND a.custodian_emp_id=@fcu"); prm.Add("@fcu"); prm.Add(cus); }

        if (status == "" ) w.Append(" AND a.status<>'VOID'");
        else if (status == "OPEN") w.Append(" AND a.status IN ('IN_USE','IN_STORE','UNDER_REPAIR','LOST')");
        else if (status == "CLOSED") w.Append(" AND a.status IN ('DISPOSED','WRITTEN_OFF')");
        else if (status == "ALL") { }
        else if (Array.IndexOf(FaFmt.Statuses, status) >= 0) { w.Append(" AND a.status=@fst"); prm.Add("@fst"); prm.Add(status); }

        if (fy != "")
        {
            w.Append(" AND a.purchase_date BETWEEN @ffy1 AND @ffy2");
            prm.Add("@ffy1"); prm.Add(FaFinYear.StartOfLabel(fy)); prm.Add("@ffy2"); prm.Add(FaFinYear.EndOfLabel(fy));
        }
        if (from.HasValue && FaJson.Str(f, "dateField") == "purchase") { w.Append(" AND a.purchase_date>=@ffr"); prm.Add("@ffr"); prm.Add(from.Value); }
        if (to.HasValue && FaJson.Str(f, "dateField") == "purchase") { w.Append(" AND a.purchase_date<=@fto"); prm.Add("@fto"); prm.Add(to.Value); }

        if (q != "")
        {
            w.Append(" AND (a.asset_no LIKE @fq OR a.tag_no LIKE @fq OR a.name LIKE @fq OR a.serial_no LIKE @fq OR a.make LIKE @fq OR a.model LIKE @fq)");
            prm.Add("@fq"); prm.Add("%" + q + "%");
        }

        const string open = " AND a.status IN ('IN_USE','IN_STORE','UNDER_REPAIR','LOST')";
        switch (flag)
        {
            case "no_custodian": w.Append(open + " AND a.custodian_emp_id IS NULL"); break;
            case "not_tagged": w.Append(open + " AND (a.tag_no IS NULL OR a.tag_no='')"); break;
            case "revaluation_due":
                w.Append(open + " AND COALESCE(sc.revalue_every_months, pc.revalue_every_months) IS NOT NULL" +
                         " AND COALESCE(a.last_valuation_date, a.purchase_date) < DATE_SUB(CURDATE(), INTERVAL COALESCE(sc.revalue_every_months, pc.revalue_every_months) MONTH)");
                break;
            case "life_ended": w.Append(open + " AND a.dep_method<>'NONE' AND a.current_value<=a.residual_value"); break;
            case "not_verified": w.Append(open + " AND (a.last_verified_date IS NULL OR a.last_verified_date < DATE_SUB(CURDATE(), INTERVAL 12 MONTH))"); break;
            case "warranty_soon": w.Append(open + " AND a.warranty_expiry BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 90 DAY)"); break;
        }
        return w.ToString();
    }

    public static string OrderBy(Dictionary<string, object> f)
    {
        string dir = FaJson.Str(f, "dir").ToLowerInvariant() == "desc" ? " DESC" : " ASC";
        switch (FaJson.Str(f, "sort"))
        {
            case "name": return " ORDER BY a.name" + dir + ", a.asset_no";
            case "cost": return " ORDER BY a.original_cost" + dir + ", a.asset_no";
            case "value": return " ORDER BY a.current_value" + dir + ", a.asset_no";
            case "purchase": return " ORDER BY a.purchase_date" + dir + ", a.asset_no";
            case "status": return " ORDER BY a.status" + dir + ", a.asset_no";
            case "category": return " ORDER BY pc.sort_order" + dir + ", sc.sort_order" + dir + ", a.asset_no";
            case "custodian": return " ORDER BY e.emp_name" + dir + ", a.asset_no";
            case "campus": return " ORDER BY cp.campus_name" + dir + ", a.building, a.room, a.asset_no";
            case "department": return " ORDER BY d.dept_name" + dir + ", a.asset_no";
            default: return " ORDER BY a.asset_no" + dir;
        }
    }

    /// <summary>Rows for the register (one page, or all when size is 0) and the totals over every filtered row.</summary>
    public static DataTable Query(MySqlConnection c, Dictionary<string, object> f, int page, int size, out int total, out decimal sumCost, out decimal sumValue)
    {
        var prm = new List<object>();
        string where = Where(f, prm);
        DataTable tt = FaDb.Table(c, null, "SELECT COUNT(*), IFNULL(SUM(a.original_cost),0), IFNULL(SUM(a.current_value),0)" + ListFrom + where, prm.ToArray());
        total = FaDb.I(tt.Rows[0][0]); sumCost = FaDb.M(tt.Rows[0][1]); sumValue = FaDb.M(tt.Rows[0][2]);
        string sql = "SELECT " + ListCols + ListFrom + where + OrderBy(f);
        if (size > 0)
        {
            if (page < 1) page = 1;
            sql += " LIMIT " + ((page - 1) * size).ToString(CultureInfo.InvariantCulture) + "," + size.ToString(CultureInfo.InvariantCulture);
        }
        return FaDb.Table(c, null, sql, prm.ToArray());
    }

    public static string Location(DataRow r)
    {
        var p = new List<string>();
        string campus = FaDb.S(r["campus_name"]).Replace(" CAMPUS", "").Replace(" Campus", "");
        if (campus != "") p.Add(Title(campus));
        if (FaDb.S(r["building"]) != "") p.Add(FaDb.S(r["building"]));
        if (FaDb.S(r["room"]) != "") p.Add(FaDb.S(r["room"]));
        return string.Join(", ", p.ToArray());
    }

    public static string Title(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.ToUpperInvariant() != s) return s;
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
    }

    public static decimal ChangePct(DataRow r)
    {
        decimal cost = FaDb.M(r["original_cost"]);
        if (cost == 0m) return 0m;
        return Math.Round((FaDb.M(r["current_value"]) - cost) / cost * 100m, 1);
    }

    public static object ListItem(DataRow r)
    {
        return new
        {
            id = FaDb.I(r["id"]), assetNo = FaDb.S(r["asset_no"]), tagNo = FaDb.S(r["tag_no"]), name = FaDb.S(r["name"]),
            serialNo = FaDb.S(r["serial_no"]), make = FaDb.S(r["make"]), model = FaDb.S(r["model"]), quantity = FaDb.I(r["quantity"]),
            category = FaDb.S(r["cat_name"]), subCategory = FaDb.S(r["sub_name"]), subCode = FaDb.S(r["cat_code"]) + "-" + FaDb.S(r["sub_code"]),
            campusId = FaDb.I(r["campus_id"]), campus = Title(FaDb.S(r["campus_name"])), location = Location(r),
            department = Title(FaDb.S(r["dept_name"])), custodian = FaDb.S(r["custodian_name"]), custodianCode = FaDb.S(r["custodian_code"]),
            purchaseDate = FaFmt.Date(r["purchase_date"]), cost = FaDb.M(r["original_cost"]), value = FaDb.M(r["current_value"]),
            accumDep = FaDb.M(r["accum_depreciation"]), changePct = ChangePct(r),
            status = FaDb.S(r["status"]), statusLabel = FaFmt.Status(FaDb.S(r["status"]))
        };
    }

    // ─────────────────────────── Detail ───────────────────────────

    public static object Detail(MySqlConnection c, int id, Dictionary<string, bool> rights)
    {
        var f = new Dictionary<string, object>(); f["assetId"] = id; f["status"] = "ALL";
        int total; decimal sc, sv;
        DataTable t = Query(c, f, 1, 1, out total, out sc, out sv);
        if (t.Rows.Count == 0) return null;
        DataRow r = t.Rows[0];
        DataRow raw = Row(c, null, id, false);
        var sub = FaCategories.Get(c, null, FaDb.I(raw["category_id"]));

        // Ledger, newest first, with reversal links resolved.
        DataTable rec = FaDb.Table(c, null,
            "SELECT r.*, IFNULL(fc.campus_name,'') from_campus, IFNULL(tc.campus_name,'') to_campus, IFNULL(fd.dept_name,'') from_dept, " +
            "IFNULL(td.dept_name,'') to_dept, IFNULL(fe.emp_name,'') from_cus, IFNULL(te.emp_name,'') to_cus " +
            "FROM fa_record r LEFT JOIN acad_campuses fc ON fc.ID=r.from_campus_id LEFT JOIN acad_campuses tc ON tc.ID=r.to_campus_id " +
            "LEFT JOIN hrm_departments fd ON fd.ID=r.from_department_id LEFT JOIN hrm_departments td ON td.ID=r.to_department_id " +
            "LEFT JOIN hrm_employee fe ON fe.empID=r.from_custodian_emp_id LEFT JOIN hrm_employee te ON te.empID=r.to_custodian_emp_id " +
            "WHERE r.asset_id=@id ORDER BY r.record_date DESC, r.id DESC", "@id", id);

        long latestValue = 0;
        var latestByType = new Dictionary<string, long>();
        foreach (DataRow x in rec.Rows)
        {
            if (x["reversed_by_record_id"] != DBNull.Value || FaDb.S(x["record_type"]) == "REVERSAL") continue;
            string ty = FaDb.S(x["record_type"]);
            if (latestValue == 0 && FaPosting.IsValueType(ty)) latestValue = FaDb.L(x["id"]);
            if (!latestByType.ContainsKey(ty)) latestByType[ty] = FaDb.L(x["id"]);
        }

        var records = new List<object>();
        var series = new List<object>();
        foreach (DataRow x in rec.Rows)
        {
            long rid = FaDb.L(x["id"]);
            string ty = FaDb.S(x["record_type"]);
            bool live = x["reversed_by_record_id"] == DBNull.Value && ty != "REVERSAL";
            string why;
            bool canReverse = live && FaRecords.CanReverse(c, null, x, latestValue, latestByType, rights, out why);
            records.Add(new
            {
                id = rid, type = ty, typeLabel = FaFmt.RecordType(ty), date = FaFmt.Date(x["record_date"]), finYear = FaDb.S(x["fin_year"]),
                before = FaDb.M(x["value_before"]), change = FaDb.M(x["change_amount"]), after = FaDb.M(x["value_after"]),
                valueRecord = FaPosting.IsValueType(ty) || (ty == "REVERSAL" && FaDb.S(x["value_class"]) != "NONE"),
                details = FaRecords.Describe(x), reason = FaDb.S(x["reason"]), reference = FaDb.S(x["reference"]),
                approvalRef = FaDb.S(x["approval_ref"]), by = FaDb.S(x["recorded_by"]),
                at = FaDb.D(x["recorded_at"]).HasValue ? FaDb.D(x["recorded_at"]).Value.ToString("d MMM yyyy, HH:mm") : "",
                reversed = x["reversed_by_record_id"] != DBNull.Value, reversedBy = FaDb.L(x["reversed_by_record_id"]),
                reverses = FaDb.L(x["reverses_record_id"]), canReverse = canReverse, runId = FaDb.L(x["run_id"])
            });
        }
        // Value over time: live value records, oldest first.
        for (int i = rec.Rows.Count - 1; i >= 0; i--)
        {
            DataRow x = rec.Rows[i];
            string ty = FaDb.S(x["record_type"]);
            if (x["reversed_by_record_id"] != DBNull.Value || ty == "REVERSAL" || !FaPosting.IsValueType(ty)) continue;
            series.Add(new { date = FaFmt.Iso(x["record_date"]), label = FaFmt.Date(x["record_date"]), value = FaDb.M(x["value_after"]) });
        }

        var atts = new List<object>();
        foreach (DataRow x in FaDb.Table(c, null,
                 "SELECT id, record_id, kind, original_name, size_bytes, mime, uploaded_by, uploaded_at FROM fa_attachment WHERE asset_id=@id AND is_active=1 ORDER BY id DESC", "@id", id).Rows)
            atts.Add(new { id = FaDb.I(x["id"]), recordId = FaDb.L(x["record_id"]), kind = FaDb.S(x["kind"]), name = FaDb.S(x["original_name"]),
                           size = FaDb.L(x["size_bytes"]), mime = FaDb.S(x["mime"]), by = FaDb.S(x["uploaded_by"]), at = FaFmt.Date(x["uploaded_at"]) });

        var audit = new List<object>();
        foreach (DataRow x in FaDb.Table(c, null,
                 "SELECT action, before_json, after_json, reason, actor, created_at FROM fa_audit WHERE asset_id=@id ORDER BY id DESC LIMIT 300", "@id", id).Rows)
        {
            var b = FaJson.Parse(FaDb.S(x["before_json"])); var a = FaJson.Parse(FaDb.S(x["after_json"]));
            var ch = new List<object>();
            var keys = new List<string>(a.Keys); foreach (string k in b.Keys) if (!keys.Contains(k)) keys.Add(k);
            foreach (string k in keys)
            {
                object bv, av; b.TryGetValue(k, out bv); a.TryGetValue(k, out av);
                ch.Add(new { field = k, before = bv == null ? "" : Convert.ToString(bv, CultureInfo.InvariantCulture), after = av == null ? "" : Convert.ToString(av, CultureInfo.InvariantCulture) });
            }
            audit.Add(new { action = FaDb.S(x["action"]), reason = FaDb.S(x["reason"]), actor = FaDb.S(x["actor"]),
                            at = FaDb.D(x["created_at"]).HasValue ? FaDb.D(x["created_at"]).Value.ToString("d MMM yyyy, HH:mm") : "", changes = ch });
        }

        decimal cost = FaDb.M(r["original_cost"]), val = FaDb.M(r["current_value"]);
        string direction = val > cost ? "Appreciating" : (val < cost ? "Depreciating" : "Unchanged");
        bool hasDep = FaDb.I(FaDb.Scalar(c, null,
            "SELECT COUNT(*) FROM fa_record WHERE asset_id=@id AND reversed_by_record_id IS NULL AND record_type IN ('DEPRECIATION','REVALUATION','APPRECIATION','MAINTENANCE','ESTIMATE','DISPOSAL')", "@id", id)) > 0;

        return new
        {
            asset = new
            {
                id = id, assetNo = FaDb.S(raw["asset_no"]), assetNoEdits = FaDb.I(raw["asset_no_edits"]), tagNo = FaDb.S(raw["tag_no"]),
                name = FaDb.S(raw["name"]), description = FaDb.S(raw["description"]), serialNo = FaDb.S(raw["serial_no"]),
                model = FaDb.S(raw["model"]), make = FaDb.S(raw["make"]), categoryId = FaDb.I(raw["category_id"]),
                parentCategoryId = sub == null ? 0 : sub.ParentId, category = FaDb.S(r["cat_name"]), subCategory = FaDb.S(r["sub_name"]),
                subCode = FaDb.S(r["cat_code"]) + "-" + FaDb.S(r["sub_code"]), assetType = FaDb.S(raw["asset_type"]), quantity = FaDb.I(raw["quantity"]),
                campusId = FaDb.I(raw["campus_id"]), campus = Title(FaDb.S(r["campus_name"])), building = FaDb.S(raw["building"]),
                room = FaDb.S(raw["room"]), roomId = FaDb.IN(raw["room_id"]), location = Location(r),
                departmentId = FaDb.IN(raw["department_id"]), department = Title(FaDb.S(r["dept_name"])),
                custodianEmpId = FaDb.IN(raw["custodian_emp_id"]), custodian = FaDb.S(r["custodian_name"]), custodianCode = FaDb.S(r["custodian_code"]),
                custodianSince = FaFmt.Date(raw["custodian_since"]),
                supplierId = FaDb.IN(raw["supplier_id"]), supplierName = FaDb.S(raw["supplier_name"]), purchaseDate = FaFmt.Iso(raw["purchase_date"]),
                purchaseDateLabel = FaFmt.Date(raw["purchase_date"]), invoiceRef = FaDb.S(raw["invoice_ref"]), orderRef = FaDb.S(raw["order_ref"]),
                requisitionRef = FaDb.S(raw["requisition_ref"]), fundingSource = FaDb.S(raw["funding_source"]),
                warrantyExpiry = FaFmt.Iso(raw["warranty_expiry"]), warrantyExpiryLabel = FaFmt.Date(raw["warranty_expiry"]),
                originalCost = cost, method = FaDb.S(raw["dep_method"]), methodLabel = FaFmt.Method(FaDb.S(raw["dep_method"])),
                lifeYears = FaDb.MN(raw["useful_life_years"]), ratePct = FaDb.MN(raw["dep_rate_pct"]), residualValue = FaDb.M(raw["residual_value"]),
                depStartDate = FaFmt.Iso(raw["dep_start_date"]), depStartLabel = FaFmt.Date(raw["dep_start_date"]),
                status = FaDb.S(raw["status"]), statusLabel = FaFmt.Status(FaDb.S(raw["status"])), currentValue = val,
                costBasis = FaDb.M(raw["cost_basis"]), accumDep = FaDb.M(raw["accum_depreciation"]), revalSurplus = FaDb.M(raw["reval_surplus"]),
                depreciatedTo = FaFmt.Date(raw["depreciated_to"]), depreciatedToIso = FaFmt.Iso(raw["depreciated_to"]),
                lastValuation = FaFmt.Date(raw["last_valuation_date"]), lastVerified = FaFmt.Date(raw["last_verified_date"]),
                closedOn = FaFmt.Date(raw["closed_on"]), notes = FaDb.S(raw["notes"]), rowVersion = FaDb.I(raw["row_version"]),
                createdBy = FaDb.S(raw["created_by"]), createdAt = FaFmt.Date(raw["created_at"]), hasValueHistory = hasDep,
                open = IsOpen(FaDb.S(raw["status"]))
            },
            records = records,
            series = series,
            trend = new { direction = direction, change = val - cost, pct = ChangePct(r) },
            attachments = atts,
            audit = audit
        };
    }

    // ─────────────────────────── Writes ───────────────────────────

    private class Input
    {
        public string AssetNo, TagNo, Name, Description, SerialNo, Model, Make, AssetType, Building, Room, SupplierName,
                      InvoiceRef, OrderRef, RequisitionRef, FundingSource, Notes, Method, Status, Reason;
        public int SubId, CampusId, Quantity;
        public int? RoomId, DepartmentId, CustodianEmpId, SupplierId;
        public DateTime? PurchaseDate, WarrantyExpiry, DepStart, OpeningDate;
        public decimal? Cost, Life, Rate, Residual, OpeningAccum;
        public bool Opening, OpeningCalc;
    }

    private static Input ReadInput(Dictionary<string, object> d)
    {
        var x = new Input();
        x.AssetNo = FaJson.Str(d, "assetNo"); x.TagNo = FaJson.Str(d, "tagNo"); x.Name = FaJson.Str(d, "name");
        x.Description = FaJson.Str(d, "description"); x.SerialNo = FaJson.Str(d, "serialNo"); x.Model = FaJson.Str(d, "model");
        x.Make = FaJson.Str(d, "make"); x.AssetType = FaJson.Str(d, "assetType").ToUpperInvariant();
        x.Building = FaJson.Str(d, "building"); x.Room = FaJson.Str(d, "room"); x.RoomId = FaJson.IntN(d, "roomId");
        x.SupplierName = FaJson.Str(d, "supplierName"); x.SupplierId = FaJson.IntN(d, "supplierId");
        x.InvoiceRef = FaJson.Str(d, "invoiceRef"); x.OrderRef = FaJson.Str(d, "orderRef"); x.RequisitionRef = FaJson.Str(d, "requisitionRef");
        x.FundingSource = FaJson.Str(d, "fundingSource"); x.Notes = FaJson.Str(d, "notes");
        x.Method = FaJson.Str(d, "method").ToUpperInvariant(); x.Status = FaJson.Str(d, "status").ToUpperInvariant();
        x.Reason = FaJson.Str(d, "reason");
        x.SubId = FaJson.Int(d, "categoryId"); x.CampusId = FaJson.Int(d, "campusId");
        x.Quantity = FaJson.Int(d, "quantity"); if (x.Quantity < 1) x.Quantity = 1;
        x.DepartmentId = FaJson.IntN(d, "departmentId"); if (x.DepartmentId == 0) x.DepartmentId = null;
        x.CustodianEmpId = FaJson.IntN(d, "custodianEmpId"); if (x.CustodianEmpId == 0) x.CustodianEmpId = null;
        if (x.SupplierId == 0) x.SupplierId = null; if (x.RoomId == 0) x.RoomId = null;
        x.PurchaseDate = FaJson.Date(d, "purchaseDate"); x.WarrantyExpiry = FaJson.Date(d, "warrantyExpiry");
        x.DepStart = FaJson.Date(d, "depStartDate"); x.Cost = FaJson.Dec(d, "originalCost");
        x.Life = FaJson.Dec(d, "lifeYears"); x.Rate = FaJson.Dec(d, "ratePct"); x.Residual = FaJson.Dec(d, "residualValue");
        x.Opening = FaJson.Bool(d, "opening"); x.OpeningDate = FaJson.Date(d, "openingDate");
        x.OpeningCalc = FaJson.Str(d, "openingAccumDep").ToUpperInvariant() == "CALC" || FaJson.Bool(d, "openingCalc");
        x.OpeningAccum = x.OpeningCalc ? null : FaJson.Dec(d, "openingAccumDep");
        return x;
    }

    private static string Check(Input x, FaCategory sub, MySqlConnection c, MySqlTransaction tx, List<string> warnings)
    {
        if (x.Name.Length < 2) return "Enter the asset name.";
        if (x.Name.Length > 200) return "The name is longer than 200 characters.";
        if (sub == null || sub.ParentId == 0) return "Choose a sub-category.";
        if (x.CampusId <= 0 || FaDb.Scalar(c, tx, "SELECT 1 FROM acad_campuses WHERE ID=@c AND ID<>0", "@c", x.CampusId) == null) return "Choose a campus.";
        if (!x.PurchaseDate.HasValue) return "Enter the purchase date.";
        if (x.PurchaseDate.Value > DateTime.Today) return "The purchase date cannot be in the future.";
        if (x.PurchaseDate.Value.Year < 1950) return "Check the purchase date.";
        if (!x.Cost.HasValue || x.Cost.Value <= 0m) return "Enter the original purchase value (more than 0). A donated asset is entered at its fair value.";
        if (x.Cost.Value > 999999999999m) return "Check the purchase value.";
        if (x.Method != "SL" && x.Method != "RB" && x.Method != "NONE") return "Choose a depreciation method.";
        if (x.Method == "SL" && (!x.Life.HasValue || x.Life <= 0 || x.Life > 200)) return "Straight line needs a useful life between 0 and 200 years.";
        if (x.Method == "RB" && (!x.Rate.HasValue || x.Rate <= 0 || x.Rate > 100)) return "Reducing balance needs an annual rate between 0 and 100 percent.";
        if (x.Residual.HasValue && (x.Residual < 0 || x.Residual >= x.Cost)) return "The residual value must be at least 0 and less than the cost.";
        if (x.DepStart.HasValue && x.DepStart.Value < x.PurchaseDate.Value) return "Depreciation cannot start before the purchase date.";
        if (x.AssetType != "TANGIBLE" && x.AssetType != "INTANGIBLE") return "Choose tangible or intangible.";
        if (x.DepartmentId.HasValue && FaDb.Scalar(c, tx, "SELECT 1 FROM hrm_departments WHERE ID=@d", "@d", x.DepartmentId.Value) == null) return "Choose a department from the list.";
        if (x.CustodianEmpId.HasValue && FaDb.Scalar(c, tx, "SELECT 1 FROM hrm_employee WHERE empID=@e", "@e", x.CustodianEmpId.Value) == null) return "Choose the responsible person from the staff list.";
        if (x.WarrantyExpiry.HasValue && x.WarrantyExpiry.Value < x.PurchaseDate.Value) return "The warranty cannot expire before the purchase date.";
        string[] lens = { x.TagNo, x.SerialNo, x.Model, x.Make, x.InvoiceRef, x.OrderRef };
        foreach (string s in lens) if (s.Length > 100) return "One of the reference fields is longer than 100 characters.";
        if (x.Description.Length > 1000) return "The description is longer than 1000 characters.";

        decimal threshold = sub.ECapThreshold;
        if (threshold > 0m && x.Cost.Value < threshold)
            warnings.Add("The value is below the capitalisation threshold of UGX " + FaFmt.Money(threshold) + ". Check that this item should be on the asset register.");
        return null;
    }

    /// <summary>Creates an asset with its Acquisition (or Opening) record. Returns null or the reason it was refused.</summary>
    public static string Create(Dictionary<string, object> d, string clientOpId, out int id, out string assetNo, List<string> warnings)
    {
        id = 0; assetNo = "";
        using (var c = FaDb.Open())
        using (var tx = c.BeginTransaction())
        {
            try
            {
                if (clientOpId != "" && FaDb.Scalar(c, tx, "SELECT 1 FROM fa_record WHERE client_op_id=@o", "@o", clientOpId) != null)
                { tx.Rollback(); return "This asset has already been saved."; }
                string err = CreateCore(c, tx, d, clientOpId, null, warnings, out id, out assetNo);
                if (err != null) { tx.Rollback(); id = 0; assetNo = ""; return err; }
                tx.Commit();
            }
            catch (MySqlException ex)
            {
                try { tx.Rollback(); } catch { }
                FaLog.Error("FaAssets.Create", ex);
                id = 0; assetNo = "";
                return ex.Number == 1062 ? "That asset number was taken a moment ago. Save again to get the next number." : "The asset could not be saved. Nothing was changed.";
            }
        }
        return null;
    }

    /// <summary>
    /// Creates one asset inside the caller's transaction. Returns null or the reason it was refused; the caller rolls back.
    /// Used by the form, and by the import (once to rehearse every row, once to commit).
    /// </summary>
    public static string CreateCore(MySqlConnection c, MySqlTransaction tx, Dictionary<string, object> d, string clientOpId,
                                    long? importBatchId, List<string> warnings, out int id, out string assetNo)
    {
        id = 0; assetNo = "";
        Input x = ReadInput(d);
        {

            var sub = FaCategories.GetLite(c, tx, x.SubId);
            if (sub != null && !sub.EActive) { return "That sub-category is inactive. Choose another."; }
            if (sub != null)
            {
                if (x.Method == "") x.Method = sub.EMethod;
                if (x.AssetType == "") x.AssetType = sub.EAssetType;
                if (!x.Life.HasValue && x.Method == "SL") x.Life = sub.ELife ?? FaMath.LifeFromRate(sub.ERate);
                if (!x.Rate.HasValue && x.Method == "RB") x.Rate = sub.ERate;
                if (!x.Residual.HasValue && x.Cost.HasValue) x.Residual = FaMath.Round0(x.Cost.Value * sub.EResidualPct / 100m);
            }
            if (x.Method == "SL" && x.Life.HasValue) x.Rate = FaMath.RateFromLife(x.Life);
            if (x.Method == "NONE") { x.Life = null; x.Rate = null; }
            if (!x.Residual.HasValue) x.Residual = 0m;
            if (x.Status == "") x.Status = "IN_USE";
            if (x.Status != "IN_USE" && x.Status != "IN_STORE") { return "A new asset starts In use or In store."; }

            string err = Check(x, sub, c, tx, warnings);
            if (err != null) { return err; }

            DateTime depStart = x.DepStart ?? x.PurchaseDate.Value;
            string fy = FaFinYear.Of(x.PurchaseDate.Value);

            // Opening balance (asset bought before the register started).
            DateTime openDate = DateTime.MinValue; decimal openValue = x.Cost.Value; int remaining = 0;
            if (x.Opening)
            {
                if (!x.OpeningDate.HasValue) { return "Enter the opening date (the date the opening value applies to)."; }
                openDate = FaFinYear.MonthEnd(x.OpeningDate.Value);
                if (openDate < FaFinYear.MonthEnd(x.PurchaseDate.Value)) { return "The opening date must be after the purchase date."; }
                if (openDate > FaFinYear.MonthEnd(DateTime.Today)) { return "The opening date cannot be in the future."; }
                // The opening value at a year end is the brought-forward position on the first day of the next year,
                // so the record is dated then: it belongs to the new year, not to the (possibly locked) year being closed.
                if (FaFinYear.IsLocked(c, tx, FaFinYear.Of(openDate.AddDays(1))))
                    return "Financial year " + FaFinYear.Of(openDate.AddDays(1)) + " is locked. Use an opening date at the end of a later year.";
                int lifeM = FaMath.LifeMonths(x.Life);
                int used = depStart > openDate ? 0 : FaFinYear.MonthsInclusive(depStart, openDate);
                remaining = Math.Max(0, lifeM - used);
                decimal accum;
                if (x.OpeningCalc || !x.OpeningAccum.HasValue)
                {
                    var st = new FaMath.AssetState();
                    st.Method = x.Method; st.RatePct = x.Rate ?? 0m; st.Residual = x.Residual.Value; st.Value = x.Cost.Value;
                    st.BaseValue = x.Cost.Value; st.BaseMonth = FaFinYear.MonthStart(depStart); st.BaseRemainingMonths = lifeM; st.DepStart = depStart;
                    decimal running = x.Cost.Value;
                    st.ValueAtYearStart = delegate (DateTime fs) { return running; };
                    accum = 0m;
                    foreach (var s in FaMath.Plan(st, openDate, FaFinYear.StartMonth)) { accum += s.Charge; running = s.After; }
                }
                else accum = x.OpeningAccum.Value;
                if (accum < 0m || accum > x.Cost.Value - x.Residual.Value) { return "Opening accumulated depreciation must be between 0 and the cost less the residual value."; }
                openValue = x.Cost.Value - accum;
                fy = FaFinYear.Of(openDate.AddDays(1));
            }
            else if (FaFinYear.IsLocked(c, tx, fy)) { return "Financial year " + fy + " is locked. Record the asset with an opening balance in an open year."; }

            // Number: typed (must be unique) or claimed.
            if (x.AssetNo != "")
            {
                if (!AssetNoRx.IsMatch(x.AssetNo)) { return "The asset number may use letters, digits, hyphens, slashes and dots (3 to 40 characters)."; }
                if (FaDb.Scalar(c, tx, "SELECT 1 FROM fa_asset WHERE asset_no=@n", "@n", x.AssetNo) != null)
                {
                    // The previewed number may have been taken meanwhile: claim a fresh one if it was a generated number.
                    if (x.AssetNo.StartsWith(FaSettings.Get("asset_no_prefix", "MRU") + "-" + (sub.Parent == null ? "" : sub.Parent.Code) + "-" + sub.Code + "-"))
                        x.AssetNo = Claim(c, tx, sub);
                    else { return "Asset number " + x.AssetNo + " is already in use."; }
                }
                else if (x.AssetNo == Preview(c, tx, sub.Id)) Claim(c, tx, sub);
            }
            else x.AssetNo = Claim(c, tx, sub);

            if (FaDb.Scalar(c, tx, "SELECT 1 FROM fa_asset WHERE name=@n AND purchase_date=@d AND original_cost=@c AND status<>'VOID' LIMIT 1",
                            "@n", x.Name, "@d", x.PurchaseDate.Value, "@c", x.Cost.Value) != null)
                warnings.Add("An asset named " + x.Name + " bought on " + FaFmt.Date(x.PurchaseDate) + " for UGX " + FaFmt.Money(x.Cost.Value) + " is already on the register. Check this is not entered twice.");
            if (x.SerialNo != "" && FaDb.Scalar(c, tx, "SELECT 1 FROM fa_asset WHERE serial_no=@s AND status<>'VOID'", "@s", x.SerialNo) != null)
                warnings.Add("Another asset already has serial number " + x.SerialNo + ". Check this is not a duplicate.");

            id = (int)FaDb.Insert(c, tx,
                "INSERT INTO fa_asset (asset_no, tag_no, name, description, serial_no, model, make, category_id, asset_type, quantity, campus_id, " +
                "building, room_id, room, department_id, custodian_emp_id, custodian_since, supplier_id, supplier_name, purchase_date, invoice_ref, " +
                "order_ref, requisition_ref, funding_source, warranty_expiry, original_cost, dep_method, useful_life_years, dep_rate_pct, " +
                "residual_value, dep_start_date, status, current_value, notes, created_by, created_at) VALUES " +
                "(@no,@tag,@name,@desc,@ser,@mod,@make,@sub,@at,@qty,@cp,@bl,@rid,@rm,@dep,@cus,@css,@sid,@sn,@pd,@inv,@ord,@req,@fund,@war," +
                "@cost,@m,@life,@rate,@res,@ds,@st,@cost,@notes,@u,NOW())",
                "@no", x.AssetNo, "@tag", FaDb.NullIfEmpty(x.TagNo), "@name", x.Name, "@desc", FaDb.NullIfEmpty(x.Description),
                "@ser", FaDb.NullIfEmpty(x.SerialNo), "@mod", FaDb.NullIfEmpty(x.Model), "@make", FaDb.NullIfEmpty(x.Make),
                "@sub", x.SubId, "@at", x.AssetType, "@qty", x.Quantity, "@cp", x.CampusId, "@bl", FaDb.NullIfEmpty(x.Building),
                "@rid", FaDb.DbVal(x.RoomId), "@rm", FaDb.NullIfEmpty(x.Room), "@dep", FaDb.DbVal(x.DepartmentId),
                "@cus", FaDb.DbVal(x.CustodianEmpId), "@css", x.CustodianEmpId.HasValue ? (object)(x.Opening ? openDate : x.PurchaseDate.Value) : DBNull.Value,
                "@sid", FaDb.DbVal(x.SupplierId), "@sn", FaDb.NullIfEmpty(x.SupplierName), "@pd", x.PurchaseDate.Value,
                "@inv", FaDb.NullIfEmpty(x.InvoiceRef), "@ord", FaDb.NullIfEmpty(x.OrderRef), "@req", FaDb.NullIfEmpty(x.RequisitionRef),
                "@fund", FaDb.NullIfEmpty(x.FundingSource), "@war", FaDb.DbVal(x.WarrantyExpiry), "@cost", x.Cost.Value,
                "@m", x.Method, "@life", FaDb.DbVal(x.Life), "@rate", FaDb.DbVal(x.Rate), "@res", x.Residual.Value, "@ds", depStart,
                "@st", x.Status, "@notes", FaDb.NullIfEmpty(x.Notes), "@u", FaAccess.Username());

            var det = new Dictionary<string, object>();
            det["to"] = new Dictionary<string, object> {
                { "building", x.Building }, { "room", x.Room }, { "roomId", x.RoomId }, { "departmentId", x.DepartmentId }, { "custodianEmpId", x.CustodianEmpId } };
            if (x.Opening) det["opening"] = new Dictionary<string, object> { { "cost", x.Cost.Value }, { "accumulated", x.Cost.Value - openValue }, { "calculated", x.OpeningCalc || !x.OpeningAccum.HasValue }, { "asAt", openDate.ToString("yyyy-MM-dd") } };

            FaRecords.InsertRecord(c, tx, new FaRecords.Rec
            {
                AssetId = id, Type = x.Opening ? "OPENING" : "ACQUISITION", ValueClass = "COST",
                Date = x.Opening ? openDate.AddDays(1) : x.PurchaseDate.Value, Before = 0m, After = openValue, Change = openValue,
                StatusAfter = x.Status, ToCampusId = x.CampusId, ToDepartmentId = x.DepartmentId, ToCustodian = x.CustodianEmpId,
                ToLocation = JoinLoc(x.Building, x.Room), Reference = x.InvoiceRef, ImportBatchId = importBatchId, Reason = x.Opening ? "Opening balance when the register began" : "Asset acquired",
                NewRemainingMonths = x.Opening ? (int?)remaining : null, Quantity = x.Quantity, Details = det, ClientOpId = clientOpId,
                CostAmount = x.Cost.Value
            });

            FaPosting.Recompute(c, tx, id, null, !importBatchId.HasValue);
            var snap = Snapshot(Row(c, tx, id, false));
            FaAudit.Write(c, tx, "ASSET", id, id, "CREATE", null, snap, importBatchId.HasValue ? "Imported in batch " + importBatchId.Value : null,
                          "Asset created: " + x.AssetNo + " " + x.Name, !importBatchId.HasValue);
            assetNo = x.AssetNo;
        }
        return null;
    }

    public static string JoinLoc(string building, string room)
    {
        var p = new List<string>();
        if (!string.IsNullOrEmpty(building)) p.Add(building);
        if (!string.IsNullOrEmpty(room)) p.Add(room);
        return string.Join(", ", p.ToArray());
    }

    public static Dictionary<string, object> Snapshot(DataRow r)
    {
        var d = new Dictionary<string, object>();
        string[] f = { "asset_no", "tag_no", "name", "description", "serial_no", "model", "make", "category_id", "asset_type", "quantity",
                       "supplier_id", "supplier_name", "purchase_date", "invoice_ref", "order_ref", "requisition_ref", "funding_source",
                       "warranty_expiry", "original_cost", "dep_method", "useful_life_years", "dep_rate_pct", "residual_value", "dep_start_date", "notes" };
        foreach (string k in f)
        {
            object v = r[k];
            if (v is DateTime) v = ((DateTime)v).ToString("yyyy-MM-dd");
            else if (v == DBNull.Value) v = null;
            d[k] = v;
        }
        return d;
    }

    /// <summary>
    /// Edits the descriptive fields of an asset. Location, custody, status and value are not editable here
    /// (they go through records). Cost, purchase date and depreciation settings may be corrected only while
    /// the asset has no depreciation or other value history; afterwards a Change of estimate is used.
    /// </summary>
    public static string Update(Dictionary<string, object> d, List<string> warnings)
    {
        int id = FaJson.Int(d, "id");
        int rv = FaJson.Int(d, "rowVersion");
        Input x = ReadInput(d);
        if (x.Reason.Length < 5) return "Give a reason for the change (at least 5 characters).";
        using (var c = FaDb.Open())
        using (var tx = c.BeginTransaction())
        {
            DataRow b = Row(c, tx, id, true);
            if (b == null) { tx.Rollback(); return "That asset no longer exists."; }
            if (FaDb.I(b["row_version"]) != rv) { tx.Rollback(); return "Someone else changed this asset since you opened it. Reload and try again."; }
            string status = FaDb.S(b["status"]);
            if (status == "VOID") { tx.Rollback(); return "A void asset cannot be edited."; }

            var sub = FaCategories.GetLite(c, tx, x.SubId);
            if (x.Method == "") x.Method = FaDb.S(b["dep_method"]);
            if (x.AssetType == "") x.AssetType = FaDb.S(b["asset_type"]);
            if (x.Method == "SL" && x.Life.HasValue) x.Rate = FaMath.RateFromLife(x.Life);
            if (x.Method == "NONE") { x.Life = null; x.Rate = null; }
            if (!x.Residual.HasValue) x.Residual = FaDb.M(b["residual_value"]);
            x.CampusId = FaDb.I(b["campus_id"]);                 // placement is not edited here
            x.DepartmentId = FaDb.IN(b["department_id"]); x.CustodianEmpId = FaDb.IN(b["custodian_emp_id"]);
            if (sub != null && sub.Id != FaDb.I(b["category_id"]) && !sub.EActive) { tx.Rollback(); return "That sub-category is inactive."; }
            string err = Check(x, sub, c, tx, warnings);
            if (err != null) { tx.Rollback(); return err; }

            // Asset number: may be changed once.
            string no = x.AssetNo == "" ? FaDb.S(b["asset_no"]) : x.AssetNo;
            int edits = FaDb.I(b["asset_no_edits"]);
            if (no != FaDb.S(b["asset_no"]))
            {
                if (edits >= 1) { tx.Rollback(); return "The asset number has already been changed once and cannot be changed again."; }
                if (!AssetNoRx.IsMatch(no)) { tx.Rollback(); return "The asset number may use letters, digits, hyphens, slashes and dots (3 to 40 characters)."; }
                if (FaDb.Scalar(c, tx, "SELECT 1 FROM fa_asset WHERE asset_no=@n AND id<>@id", "@n", no, "@id", id) != null)
                { tx.Rollback(); return "Asset number " + no + " is already in use."; }
                edits++;
            }

            bool hasHistory = FaDb.I(FaDb.Scalar(c, tx,
                "SELECT COUNT(*) FROM fa_record WHERE asset_id=@id AND reversed_by_record_id IS NULL AND record_type NOT IN ('ACQUISITION','OPENING','TRANSFER','STATUS','VERIFICATION','REVERSAL')",
                "@id", id)) > 0;
            DateTime depStart = x.DepStart ?? FaDb.D(b["dep_start_date"]) ?? x.PurchaseDate.Value;
            if (x.DepStart == null && FaDb.D(b["purchase_date"]) != x.PurchaseDate && FaDb.D(b["dep_start_date"]) == FaDb.D(b["purchase_date"]))
                depStart = x.PurchaseDate.Value;

            bool costChanged = FaDb.M(b["original_cost"]) != x.Cost.Value || FaDb.D(b["purchase_date"]) != x.PurchaseDate;
            bool settingsChanged = FaDb.S(b["dep_method"]) != x.Method || FaDb.MN(b["useful_life_years"]) != x.Life ||
                                   FaDb.MN(b["dep_rate_pct"]) != x.Rate || FaDb.M(b["residual_value"]) != x.Residual.Value ||
                                   FaDb.D(b["dep_start_date"]) != depStart;
            if ((costChanged || settingsChanged) && hasHistory)
            {
                tx.Rollback();
                return costChanged
                    ? "The cost or purchase date cannot be edited once the asset has depreciation or other value records. Reverse those first, or record a revaluation."
                    : "Depreciation settings cannot be edited once depreciation has been posted. Use the Change of estimate record instead.";
            }

            // An acquisition that was entered wrongly is corrected by reversing it and recording it again, so the ledger shows both.
            if (costChanged)
            {
                DataTable acq = FaDb.Table(c, tx, "SELECT * FROM fa_record WHERE asset_id=@id AND record_type IN ('ACQUISITION','OPENING') AND reversed_by_record_id IS NULL ORDER BY id DESC LIMIT 1", "@id", id);
                if (acq.Rows.Count == 1)
                {
                    DataRow a0 = acq.Rows[0];
                    if (FaDb.S(a0["record_type"]) == "OPENING") { tx.Rollback(); return "An opening balance cannot be edited. Void the asset and enter it again."; }
                    if (FaFinYear.IsLocked(c, tx, FaDb.S(a0["fin_year"])) || FaFinYear.IsLocked(c, tx, FaFinYear.Of(x.PurchaseDate.Value)))
                    { tx.Rollback(); return "The acquisition falls in a locked financial year."; }
                    long revId = FaRecords.InsertRecord(c, tx, new FaRecords.Rec
                    {
                        AssetId = id, Type = "REVERSAL", ValueClass = "COST", Date = FaDb.D(a0["record_date"]).Value,
                        Before = FaDb.M(a0["value_after"]), After = 0m, Change = -FaDb.M(a0["value_after"]),
                        Reason = "Acquisition corrected: " + x.Reason, ReversesId = FaDb.L(a0["id"])
                    });
                    FaDb.Exec(c, tx, "UPDATE fa_record SET reversed_by_record_id=@r WHERE id=@id", "@r", revId, "@id", FaDb.L(a0["id"]));
                    FaRecords.InsertRecord(c, tx, new FaRecords.Rec
                    {
                        AssetId = id, Type = "ACQUISITION", ValueClass = "COST", Date = x.PurchaseDate.Value, Before = 0m,
                        After = x.Cost.Value, Change = x.Cost.Value, StatusAfter = FaDb.S(a0["status_after"]),
                        ToCampusId = FaDb.IN(a0["to_campus_id"]), ToDepartmentId = FaDb.IN(a0["to_department_id"]),
                        ToCustodian = FaDb.IN(a0["to_custodian_emp_id"]), ToLocation = FaDb.S(a0["to_location"]),
                        Reference = x.InvoiceRef, Reason = "Acquisition corrected: " + x.Reason,
                        DetailsRaw = FaDb.S(a0["details_json"]), Quantity = x.Quantity, CostAmount = x.Cost.Value
                    });
                }
            }

            var before = Snapshot(b);
            FaDb.Exec(c, tx,
                "UPDATE fa_asset SET asset_no=@no, asset_no_edits=@ed, tag_no=@tag, name=@name, description=@desc, serial_no=@ser, model=@mod, make=@make, " +
                "category_id=@sub, asset_type=@at, quantity=@qty, supplier_id=@sid, supplier_name=@sn, purchase_date=@pd, invoice_ref=@inv, order_ref=@ord, " +
                "requisition_ref=@req, funding_source=@fund, warranty_expiry=@war, original_cost=@cost, dep_method=@m, useful_life_years=@life, " +
                "dep_rate_pct=@rate, residual_value=@res, dep_start_date=@ds, notes=@notes, updated_by=@u, updated_at=NOW(), row_version=row_version+1 WHERE id=@id",
                "@no", no, "@ed", edits, "@tag", FaDb.NullIfEmpty(x.TagNo), "@name", x.Name, "@desc", FaDb.NullIfEmpty(x.Description),
                "@ser", FaDb.NullIfEmpty(x.SerialNo), "@mod", FaDb.NullIfEmpty(x.Model), "@make", FaDb.NullIfEmpty(x.Make), "@sub", x.SubId,
                "@at", x.AssetType, "@qty", x.Quantity, "@sid", FaDb.DbVal(x.SupplierId), "@sn", FaDb.NullIfEmpty(x.SupplierName),
                "@pd", x.PurchaseDate.Value, "@inv", FaDb.NullIfEmpty(x.InvoiceRef), "@ord", FaDb.NullIfEmpty(x.OrderRef),
                "@req", FaDb.NullIfEmpty(x.RequisitionRef), "@fund", FaDb.NullIfEmpty(x.FundingSource), "@war", FaDb.DbVal(x.WarrantyExpiry),
                "@cost", x.Cost.Value, "@m", x.Method, "@life", FaDb.DbVal(x.Life), "@rate", FaDb.DbVal(x.Rate), "@res", x.Residual.Value,
                "@ds", depStart, "@notes", FaDb.NullIfEmpty(x.Notes), "@u", FaAccess.Username(), "@id", id);

            var after = Snapshot(Row(c, tx, id, false));
            Dictionary<string, object> db, da;
            FaAudit.Diff(before, after, out db, out da);
            if (da.Count == 0 && !costChanged) { tx.Rollback(); return null; }
            FaAudit.Write(c, tx, "ASSET", id, id, "UPDATE", db, da, x.Reason, "Asset edited: " + no);
            FaPosting.Recompute(c, tx, id, "edit");
            tx.Commit();
        }
        return null;
    }

    // ─────────────────────────── Lookups ───────────────────────────

    public static List<object> Campuses(MySqlConnection c)
    {
        var l = new List<object>();
        foreach (DataRow r in FaDb.Table(c, null, "SELECT ID, campus_name, campus_short_name FROM acad_campuses WHERE ID<>0 ORDER BY ID").Rows)
            l.Add(new { id = FaDb.I(r[0]), name = Title(FaDb.S(r[1])), shortName = FaDb.S(r[2]) });
        return l;
    }

    public static List<object> Departments(MySqlConnection c)
    {
        var l = new List<object>();
        foreach (DataRow r in FaDb.Table(c, null, "SELECT ID, dept_name FROM hrm_departments WHERE dept_name NOT LIKE 'TEST%' ORDER BY dept_name").Rows)
            l.Add(new { id = FaDb.I(r[0]), name = Title(FaDb.S(r[1])) });
        return l;
    }

    public static List<object> Rooms(MySqlConnection c)
    {
        var l = new List<object>();
        foreach (DataRow r in FaDb.Table(c, null, "SELECT RoomID, RoomName, campusId FROM acad_lecturerooms WHERE IFNULL(is_active,1)=1 ORDER BY RoomName").Rows)
            l.Add(new { id = FaDb.I(r[0]), name = FaDb.S(r[1]), campusId = FaDb.I(r[2]) });
        return l;
    }

    public static List<object> SearchStaff(MySqlConnection c, string q)
    {
        var l = new List<object>();
        q = (q ?? "").Trim();
        if (q.Length < 2) return l;
        DataTable t = FaDb.Table(c, null,
            "SELECT e.empID, e.emp_name, IFNULL(e.EMP_CODE,'') code, IFNULL(d.dept_name,'') dept FROM hrm_employee e " +
            "LEFT JOIN hrm_emp_contracts ct ON ct.ID = hr_current_contract_id(e.empID) LEFT JOIN hrm_departments d ON d.ID = ct.departmentID " +
            "WHERE e.emp_name LIKE @q OR e.EMP_CODE LIKE @q ORDER BY e.emp_name LIMIT 20", "@q", "%" + q + "%");
        foreach (DataRow r in t.Rows)
            l.Add(new { id = FaDb.I(r[0]), name = FaDb.S(r[1]), code = FaDb.S(r[2]), department = Title(FaDb.S(r[3])) });
        return l;
    }

    public static List<object> SearchSuppliers(MySqlConnection c, string q)
    {
        var l = new List<object>();
        q = (q ?? "").Trim();
        if (q.Length < 2) return l;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (DataRow r in FaDb.Table(c, null, "SELECT supplierID, supplierName FROM campus_dynamics_accounts.supplier WHERE supplierName LIKE @q ORDER BY supplierName LIMIT 15", "@q", "%" + q + "%").Rows)
            { string n = FaDb.S(r[1]).Trim(); if (seen.Add(n)) l.Add(new { id = FaDb.I(r[0]), name = n }); }
            foreach (DataRow r in FaDb.Table(c, null, "SELECT SupplierName FROM campus_dynamics_accounts.inv_supplierdetails WHERE SupplierName LIKE @q ORDER BY SupplierName LIMIT 15", "@q", "%" + q + "%").Rows)
            { string n = FaDb.S(r[0]).Trim(); if (seen.Add(n)) l.Add(new { id = 0, name = n }); }
            foreach (DataRow r in FaDb.Table(c, null, "SELECT DISTINCT supplier_name FROM fa_asset WHERE supplier_name LIKE @q LIMIT 15", "@q", "%" + q + "%").Rows)
            { string n = FaDb.S(r[0]).Trim(); if (n != "" && seen.Add(n)) l.Add(new { id = 0, name = n }); }
        }
        catch { }
        return l;
    }

    public static List<string> FinYears(MySqlConnection c)
    {
        var l = new List<string>();
        object mn = FaDb.Scalar(c, null, "SELECT MIN(purchase_date) FROM fa_asset");
        DateTime from = FaDb.D(mn) ?? DateTime.Today;
        if (from > DateTime.Today) from = DateTime.Today;
        var r = FaFinYear.Range(from, DateTime.Today);
        r.Reverse();
        return r;
    }
}
