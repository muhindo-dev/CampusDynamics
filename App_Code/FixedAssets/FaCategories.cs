using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;

// =====================================================================
//  Fixed Assets: categories and sub-categories (plan 2.1 and 4.2).
//  Two levels only. A sub-category inherits every default it leaves
//  NULL from its parent; the effective values are resolved here and
//  copied onto an asset when it is created, never later.
// =====================================================================
public class FaCategory
{
    public int Id, ParentId, SortOrder, NextSeq, RowVersion, AssetCount;
    public string Code, Name, Description, AssetType, Method, GlCost, GlAccum, GlExpense;
    public decimal? LifeYears, RatePct, ResidualPct, CapThreshold;
    public int? RevalueMonths;
    public bool Active;
    public FaCategory Parent;

    // Effective values (own value, else parent's, else the setting default).
    public string EAssetType { get { return First(AssetType, Parent == null ? null : Parent.AssetType, "TANGIBLE"); } }
    public string EMethod { get { return First(Method, Parent == null ? null : Parent.Method, "SL"); } }
    public decimal? ELife { get { return LifeYears ?? (Parent == null ? null : Parent.LifeYears); } }
    public decimal? ERate { get { return RatePct ?? (Parent == null ? null : Parent.RatePct); } }
    public decimal EResidualPct { get { return ResidualPct ?? (Parent == null ? null : Parent.ResidualPct) ?? 0m; } }
    public int? ERevalue { get { return RevalueMonths ?? (Parent == null ? null : Parent.RevalueMonths); } }
    public decimal ECapThreshold { get { return CapThreshold ?? (Parent == null ? null : Parent.CapThreshold) ?? FaSettings.Dec("cap_threshold_default", 0m); } }
    public string EGlCost { get { return First(GlCost, Parent == null ? null : Parent.GlCost, ""); } }
    public string EGlAccum { get { return First(GlAccum, Parent == null ? null : Parent.GlAccum, ""); } }
    public string EGlExpense { get { return First(GlExpense, Parent == null ? null : Parent.GlExpense, ""); } }
    public bool EActive { get { return Active && (Parent == null || Parent.Active); } }
    public string FullCode { get { return Parent == null ? Code : Parent.Code + "-" + Code; } }
    public string FullName { get { return Parent == null ? Name : Parent.Name + " / " + Name; } }

    private static string First(string a, string b, string c)
    {
        if (!string.IsNullOrEmpty(a)) return a;
        if (!string.IsNullOrEmpty(b)) return b;
        return c;
    }

    public Dictionary<string, object> Snapshot()
    {
        var d = new Dictionary<string, object>();
        d["parent_id"] = ParentId; d["code"] = Code; d["name"] = Name; d["description"] = Description;
        d["asset_type"] = AssetType; d["dep_method"] = Method; d["useful_life_years"] = LifeYears; d["dep_rate_pct"] = RatePct;
        d["residual_pct"] = ResidualPct; d["revalue_every_months"] = RevalueMonths; d["cap_threshold"] = CapThreshold;
        d["gl_cost_account"] = GlCost; d["gl_accum_account"] = GlAccum; d["gl_expense_account"] = GlExpense;
        d["sort_order"] = SortOrder; d["is_active"] = Active ? 1 : 0;
        return d;
    }
}

public static class FaCategories
{
    private const string Cols =
        "c.id, c.parent_id, c.code, c.name, c.description, c.asset_type, c.dep_method, c.useful_life_years, c.dep_rate_pct, " +
        "c.residual_pct, c.revalue_every_months, c.cap_threshold, c.gl_cost_account, c.gl_accum_account, c.gl_expense_account, " +
        "c.next_seq, c.sort_order, c.is_active, c.row_version";

    private static FaCategory Read(DataRow r)
    {
        var c = new FaCategory();
        c.Id = FaDb.I(r["id"]); c.ParentId = FaDb.I(r["parent_id"]); c.Code = FaDb.S(r["code"]); c.Name = FaDb.S(r["name"]);
        c.Description = FaDb.S(r["description"]); c.AssetType = FaDb.S(r["asset_type"]); c.Method = FaDb.S(r["dep_method"]);
        c.LifeYears = FaDb.MN(r["useful_life_years"]); c.RatePct = FaDb.MN(r["dep_rate_pct"]); c.ResidualPct = FaDb.MN(r["residual_pct"]);
        c.RevalueMonths = FaDb.IN(r["revalue_every_months"]); c.CapThreshold = FaDb.MN(r["cap_threshold"]);
        c.GlCost = FaDb.S(r["gl_cost_account"]); c.GlAccum = FaDb.S(r["gl_accum_account"]); c.GlExpense = FaDb.S(r["gl_expense_account"]);
        c.NextSeq = FaDb.I(r["next_seq"]); c.SortOrder = FaDb.I(r["sort_order"]); c.Active = FaDb.I(r["is_active"]) == 1;
        c.RowVersion = FaDb.I(r["row_version"]);
        if (r.Table.Columns.Contains("asset_count")) c.AssetCount = FaDb.I(r["asset_count"]);
        return c;
    }

    /// <summary>Every category with its parent linked, in display order.</summary>
    public static List<FaCategory> All(MySqlConnection c, MySqlTransaction tx)
    {
        DataTable t = FaDb.Table(c, tx,
            "SELECT " + Cols + ", IFNULL(n.cnt,0) asset_count FROM fa_category c " +
            "LEFT JOIN (SELECT category_id, COUNT(*) cnt FROM fa_asset WHERE status<>'VOID' GROUP BY category_id) n ON n.category_id=c.id " +
            "ORDER BY c.parent_id=0 DESC, c.sort_order, c.name");
        var list = new List<FaCategory>();
        var byId = new Dictionary<int, FaCategory>();
        foreach (DataRow r in t.Rows) { var x = Read(r); list.Add(x); byId[x.Id] = x; }
        foreach (var x in list)
            if (x.ParentId != 0 && byId.ContainsKey(x.ParentId)) x.Parent = byId[x.ParentId];
        // Parent counts include their children's assets.
        foreach (var x in list)
            if (x.Parent != null) x.Parent.AssetCount += x.AssetCount;
        return list;
    }

    /// <summary>One node with its parent linked, without asset counts (cheap; used on every asset create).</summary>
    public static FaCategory GetLite(MySqlConnection c, MySqlTransaction tx, int id)
    {
        DataTable t = FaDb.Table(c, tx, "SELECT " + Cols + " FROM fa_category c WHERE c.id=@id OR c.id=(SELECT parent_id FROM fa_category WHERE id=@id)", "@id", id);
        FaCategory self = null, parent = null;
        foreach (DataRow r in t.Rows) { var x = Read(r); if (x.Id == id) self = x; else parent = x; }
        if (self != null && self.ParentId != 0) self.Parent = parent;
        return self;
    }

    public static FaCategory Get(MySqlConnection c, MySqlTransaction tx, int id)
    {
        foreach (var x in All(c, tx)) if (x.Id == id) return x;
        return null;
    }

    private static object Node(FaCategory x, List<FaCategory> all)
    {
        var kids = new List<object>();
        foreach (var k in all) if (k.ParentId == x.Id) kids.Add(Node(k, all));
        return new
        {
            id = x.Id, parentId = x.ParentId, code = x.Code, fullCode = x.FullCode, name = x.Name, description = x.Description,
            assetType = x.AssetType, method = x.Method, lifeYears = x.LifeYears, ratePct = x.RatePct, residualPct = x.ResidualPct,
            revalueMonths = x.RevalueMonths, capThreshold = x.CapThreshold, glCost = x.GlCost, glAccum = x.GlAccum, glExpense = x.GlExpense,
            active = x.Active, effectiveActive = x.EActive, sort = x.SortOrder, assetCount = x.AssetCount, rowVersion = x.RowVersion,
            effective = new
            {
                assetType = x.EAssetType, method = x.EMethod, methodLabel = FaFmt.Method(x.EMethod), lifeYears = x.ELife, ratePct = x.ERate,
                residualPct = x.EResidualPct, revalueMonths = x.ERevalue, capThreshold = x.ECapThreshold,
                glCost = x.EGlCost, glAccum = x.EGlAccum, glExpense = x.EGlExpense
            },
            children = kids
        };
    }

    /// <summary>The tree as the screens need it. activeOnly hides deactivated nodes (pickers for new assets).</summary>
    public static List<object> Tree(MySqlConnection c, bool activeOnly)
    {
        var all = All(c, null);
        if (activeOnly) all = all.FindAll(delegate (FaCategory x) { return x.EActive; });
        var outp = new List<object>();
        foreach (var x in all) if (x.ParentId == 0) outp.Add(Node(x, all));
        return outp;
    }

    /// <summary>Posting accounts from the chart of accounts, for the GL pickers.</summary>
    public static List<object> GlAccounts(MySqlConnection c)
    {
        var l = new List<object>();
        try
        {
            DataTable t = FaDb.Table(c, null,
                "SELECT s.AccountCode, s.AccountName, IFNULL(m.AccountName,'') main FROM campus_dynamics_accounts.fin_subaccounts s " +
                "LEFT JOIN campus_dynamics_accounts.fin_mainaccounts m ON m.AccountCode=s.MainAccountCode " +
                "WHERE m.GeneralCategory IN ('Assets','Expense','Equity','Expenses','Capital') OR s.MainAccountCode IN ('AC8004','AC9001','AC9100','AC9200','AC2500','AC7003') " +
                "ORDER BY s.AccountCode");
            foreach (DataRow r in t.Rows)
                l.Add(new { code = FaDb.S(r[0]), name = FaDb.S(r[1]), group = FaDb.S(r[2]) });
        }
        catch { }
        return l;
    }

    // ─────────────────────────── Writes ───────────────────────────

    private static readonly Regex CodeRx = new Regex("^[A-Z][A-Z0-9]{1,9}$");

    /// <summary>Create or update a node. Returns null on success, otherwise the reason it was refused.</summary>
    public static string Save(Dictionary<string, object> d, out int id)
    {
        id = FaJson.Int(d, "id");
        int parentId = FaJson.Int(d, "parentId");
        string code = FaJson.Str(d, "code").ToUpperInvariant();
        string name = FaJson.Str(d, "name");
        string reason = FaJson.Str(d, "reason");
        if (!CodeRx.IsMatch(code)) return "The code must be 2 to 10 capital letters or digits, starting with a letter.";
        if (parentId != 0 && code.Length != 3) return "A sub-category code must be exactly 3 characters, because it forms part of asset numbers.";
        if (parentId == 0 && (code.Length < 2 || code.Length > 3)) return "A category code must be 2 or 3 characters, because it forms part of asset numbers.";
        if (name.Length < 2) return "Enter a name.";
        if (name.Length > 120) return "The name is longer than 120 characters.";

        string method = FaJson.Str(d, "method").ToUpperInvariant();
        if (method != "" && method != "SL" && method != "RB" && method != "NONE") return "Choose a depreciation method.";
        string assetType = FaJson.Str(d, "assetType").ToUpperInvariant();
        if (assetType != "" && assetType != "TANGIBLE" && assetType != "INTANGIBLE") return "Choose tangible or intangible.";
        decimal? life = FaJson.Dec(d, "lifeYears"), rate = FaJson.Dec(d, "ratePct"), resid = FaJson.Dec(d, "residualPct"), cap = FaJson.Dec(d, "capThreshold");
        int? reval = FaJson.IntN(d, "revalueMonths");
        if (life.HasValue && (life <= 0 || life > 200)) return "Useful life must be between 0 and 200 years.";
        if (rate.HasValue && (rate <= 0 || rate > 100)) return "The rate must be between 0 and 100 percent.";
        if (resid.HasValue && (resid < 0 || resid >= 100)) return "Residual value must be between 0 and 100 percent of cost.";
        if (cap.HasValue && cap < 0) return "The capitalisation threshold cannot be negative.";
        if (reval.HasValue && (reval < 1 || reval > 240)) return "The revaluation interval must be between 1 and 240 months.";
        if (parentId == 0)
        {
            // A category is the root of inheritance: it must define the essentials.
            if (method == "") return "A category needs a depreciation method.";
            if (method == "SL" && !life.HasValue) return "Straight line needs a useful life in years.";
            if (method == "RB" && !rate.HasValue) return "Reducing balance needs an annual rate.";
            if (assetType == "") return "Choose tangible or intangible.";
        }
        else
        {
            if (method == "SL" && !life.HasValue && !rate.HasValue) { /* inherits the parent's life */ }
        }
        // For straight line the rate is derived from the life, so the two cannot disagree.
        if (method == "SL" && life.HasValue) rate = FaMath.RateFromLife(life);

        using (var c = FaDb.Open())
        using (var tx = c.BeginTransaction())
        {
            if (parentId != 0)
            {
                object pp = FaDb.Scalar(c, tx, "SELECT parent_id FROM fa_category WHERE id=@p", "@p", parentId);
                if (pp == null) { tx.Rollback(); return "The parent category no longer exists."; }
                if (FaDb.I(pp) != 0) { tx.Rollback(); return "Sub-categories can only sit under a category (two levels)."; }
            }
            object dup = FaDb.Scalar(c, tx, "SELECT id FROM fa_category WHERE parent_id=@p AND code=@c AND id<>@id",
                                     "@p", parentId, "@c", code, "@id", id);
            if (dup != null) { tx.Rollback(); return "Another " + (parentId == 0 ? "category" : "sub-category here") + " already uses the code " + code + "."; }

            string[] gl = { FaJson.Str(d, "glCost"), FaJson.Str(d, "glAccum"), FaJson.Str(d, "glExpense") };
            foreach (string g in gl)
                if (g != "" && FaDb.Scalar(c, tx, "SELECT 1 FROM campus_dynamics_accounts.fin_subaccounts WHERE AccountCode=@a", "@a", g) == null)
                { tx.Rollback(); return "The account " + g + " is not in the chart of accounts."; }

            object[] vals = {
                "@pid", parentId, "@code", code, "@name", name, "@desc", FaDb.NullIfEmpty(FaJson.Str(d, "description")),
                "@at", assetType == "" ? (object)DBNull.Value : assetType, "@m", method == "" ? (object)DBNull.Value : method,
                "@life", FaDb.DbVal(life), "@rate", FaDb.DbVal(rate), "@res", FaDb.DbVal(resid), "@rev", FaDb.DbVal(reval),
                "@cap", FaDb.DbVal(cap), "@glc", FaDb.NullIfEmpty(gl[0]), "@gla", FaDb.NullIfEmpty(gl[1]), "@gle", FaDb.NullIfEmpty(gl[2]),
                "@u", FaAccess.Username(), "@id", id, "@rv", FaJson.Int(d, "rowVersion") };

            if (id == 0)
            {
                object mx = FaDb.Scalar(c, tx, "SELECT IFNULL(MAX(sort_order),0)+10 FROM fa_category WHERE parent_id=@p", "@p", parentId);
                var list = new List<object>(vals); list.Add("@sort"); list.Add(FaDb.I(mx));
                id = (int)FaDb.Insert(c, tx,
                    "INSERT INTO fa_category (parent_id, code, name, description, asset_type, dep_method, useful_life_years, dep_rate_pct, residual_pct, " +
                    "revalue_every_months, cap_threshold, gl_cost_account, gl_accum_account, gl_expense_account, sort_order, created_by, created_at) " +
                    "VALUES (@pid,@code,@name,@desc,@at,@m,@life,@rate,@res,@rev,@cap,@glc,@gla,@gle,@sort,@u,NOW())", list.ToArray());
                var after = Get(c, tx, id);
                FaAudit.Write(c, tx, "CATEGORY", id, null, "CREATE", null, after.Snapshot(), reason,
                              "Category created: " + after.FullCode + " " + after.Name);
            }
            else
            {
                var before = Get(c, tx, id);
                if (before == null) { tx.Rollback(); return "That category no longer exists."; }
                if (before.ParentId != parentId) { tx.Rollback(); return "Move sub-categories by dragging them in the tree."; }
                if (before.Code != code && before.AssetCount > 0)
                { tx.Rollback(); return "The code cannot change while assets use it, because it is part of their asset numbers."; }
                int n = FaDb.Exec(c, tx,
                    "UPDATE fa_category SET code=@code, name=@name, description=@desc, asset_type=@at, dep_method=@m, useful_life_years=@life, " +
                    "dep_rate_pct=@rate, residual_pct=@res, revalue_every_months=@rev, cap_threshold=@cap, gl_cost_account=@glc, " +
                    "gl_accum_account=@gla, gl_expense_account=@gle, updated_by=@u, updated_at=NOW(), row_version=row_version+1 " +
                    "WHERE id=@id AND row_version=@rv", vals);
                if (n == 0) { tx.Rollback(); return "Someone else changed this category since you opened it. Reload and try again."; }
                var after = Get(c, tx, id);
                Dictionary<string, object> b, a;
                FaAudit.Diff(before.Snapshot(), after.Snapshot(), out b, out a);
                if (a.Count > 0)
                    FaAudit.Write(c, tx, "CATEGORY", id, null, "UPDATE", b, a, reason, "Category updated: " + after.FullCode + " " + after.Name);
            }
            tx.Commit();
        }
        return null;
    }

    public static string SetActive(int id, bool active, string reason)
    {
        if ((reason ?? "").Trim().Length < 5) return "Give a reason of at least 5 characters.";
        using (var c = FaDb.Open())
        using (var tx = c.BeginTransaction())
        {
            var x = Get(c, tx, id);
            if (x == null) { tx.Rollback(); return "That category no longer exists."; }
            if (x.Active == active) { tx.Rollback(); return null; }
            FaDb.Exec(c, tx, "UPDATE fa_category SET is_active=@a, updated_by=@u, updated_at=NOW(), row_version=row_version+1 WHERE id=@id",
                      "@a", active ? 1 : 0, "@u", FaAccess.Username(), "@id", id);
            FaAudit.Write(c, tx, "CATEGORY", id, null, active ? "ACTIVATE" : "DEACTIVATE",
                          new { is_active = x.Active ? 1 : 0 }, new { is_active = active ? 1 : 0 }, reason,
                          (active ? "Category activated: " : "Category deactivated: ") + x.FullCode + " " + x.Name);
            tx.Commit();
        }
        return null;
    }

    /// <summary>
    /// Applies a reorder/move from the tree in one transaction. ops: [{id, parentId, sort, reason}].
    /// Categories may only reorder; sub-categories may reorder or move to another category.
    /// </summary>
    public static string SaveLayout(List<Dictionary<string, object>> ops, out int changed)
    {
        changed = 0;
        if (ops == null || ops.Count == 0) return null;
        if (ops.Count > 500) return "Too many changes at once.";
        using (var c = FaDb.Open())
        using (var tx = c.BeginTransaction())
        {
            var all = All(c, tx);
            var byId = new Dictionary<int, FaCategory>();
            foreach (var x in all) byId[x.Id] = x;

            foreach (var op in ops)
            {
                int id = FaJson.Int(op, "id"), parentId = FaJson.Int(op, "parentId"), sort = FaJson.Int(op, "sort");
                string reason = FaJson.Str(op, "reason");
                if (!byId.ContainsKey(id)) { tx.Rollback(); return "A category in your changes no longer exists. Reload."; }
                var x = byId[id];
                if (x.ParentId == 0 && parentId != 0) { tx.Rollback(); return x.Name + " is a category and cannot be placed under another category."; }
                if (x.ParentId != 0 && parentId == 0) { tx.Rollback(); return x.Name + " is a sub-category and must stay under a category."; }
                if (parentId != 0 && (!byId.ContainsKey(parentId) || byId[parentId].ParentId != 0))
                { tx.Rollback(); return "The destination for " + x.Name + " is not a category."; }
                bool moved = x.ParentId != parentId;
                if (moved)
                {
                    if (x.AssetCount > 0 && reason.Length < 10)
                    { tx.Rollback(); return "Moving " + x.Name + " affects " + x.AssetCount + " assets. Give a reason of at least 10 characters."; }
                    if (FaDb.Scalar(c, tx, "SELECT id FROM fa_category WHERE parent_id=@p AND code=@c AND id<>@id",
                                    "@p", parentId, "@c", x.Code, "@id", id) != null)
                    { tx.Rollback(); return byId[parentId].Name + " already has a sub-category coded " + x.Code + "."; }
                }
                if (!moved && x.SortOrder == sort) continue;
                FaDb.Exec(c, tx, "UPDATE fa_category SET parent_id=@p, sort_order=@s, updated_by=@u, updated_at=NOW(), row_version=row_version+1 WHERE id=@id",
                          "@p", parentId, "@s", sort, "@u", FaAccess.Username(), "@id", id);
                FaAudit.Write(c, tx, "CATEGORY", id, null, moved ? "MOVE" : "REORDER",
                              new { parent_id = x.ParentId, sort_order = x.SortOrder }, new { parent_id = parentId, sort_order = sort },
                              moved ? reason : null,
                              moved ? "Sub-category " + x.Name + " moved from " + (x.Parent == null ? "" : x.Parent.Name) + " to " + byId[parentId].Name
                                    : "Category order changed: " + x.Name);
                changed++;
            }
            tx.Commit();
        }
        return null;
    }
}
