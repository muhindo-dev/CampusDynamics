using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Web.Services;

// General Ledger: adjusting entries (plan section 3). Maker prepares and submits; a different person
// with the approve slug posts. Posting only adds lines; see GlAdjust for every rule it enforces.
public partial class COOPERP_NewScreens_AccountsAdjustments : System.Web.UI.Page
{
    protected string BootJson = "{}";

    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, GlAccess.Adjust);
        var boot = new Dictionary<string, object>();
        boot["rights"] = GlAccess.RightsJson();
        boot["purposes"] = GlAdjust.Purposes;
        boot["today"] = GlFmt.Iso(DateTime.Today);
        boot["base"] = GlAdjust.VoucherBase;
        BootJson = FaJson.Ser(boot).Replace("<", "\\u003c");
    }

    [WebMethod(EnableSession = true)]
    public static string LoadAdjustments(string status)
    {
        return GlApi.Read(GlAccess.Adjust, delegate
        {
            var all = GlAdjust.List(null);
            var counts = new Dictionary<string, int>();
            using (var c = GlDb.Read())
                foreach (DataRow r in GlDb.Table(c, null, "SELECT status, COUNT(*) FROM gl_adjustment GROUP BY status").Rows) counts[GlDb.S(r[0])] = GlDb.I(r[1]);
            return GlApi.Ok(new { rows = string.IsNullOrEmpty(status) ? all : GlAdjust.List(status), counts });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string LoadAdjustment(long id)
    {
        return GlApi.Read(GlAccess.Adjust, delegate { return GlApi.Ok(new { entry = GlAdjust.Get(id) }); });
    }

    [WebMethod(EnableSession = true)]
    public static string SearchAccounts(string q)
    {
        return GlApi.Read(GlAccess.Adjust, delegate
        {
            var rows = GlReports.SearchAccounts(q, 20).Where(o => (string)o.GetType().GetProperty("kind").GetValue(o, null) != "Missing from the chart, not mapped").ToList();
            return GlApi.Ok(new { rows });
        });
    }

    /// <summary>A voucher's lines and difference, for completing it.</summary>
    [WebMethod(EnableSession = true)]
    public static string LoadVoucherForAdjust(long voucher)
    {
        return GlApi.Read(GlAccess.Adjust, delegate
        {
            GlCalc.Snapshot s = GlCalc.Get(false);
            using (var c = GlDb.Read())
            {
                int n; long diff = GlAdjust.VoucherDiff(c, null, voucher, false, out n);
                if (n == 0) throw new GlRefusal("No ledger line carries voucher number " + voucher + ".");
                var lines = GlDb.Table(c, null, "SELECT transactionDate, accountcode, account_type, transactionType, CAST(transaction_amount AS SIGNED), particulars FROM fin_ledger WHERE voucherNo=@v ORDER BY transactionDate, TID LIMIT 50", "@v", voucher).Rows.Cast<DataRow>()
                    .Select(r =>
                    {
                        string key = GlCalc.KeyOf(s, GlDb.S(r[1]), GlDb.S(r[2])); GlAcct a; s.Accounts.TryGetValue(key, out a);
                        return new { date = GlFmt.Date(r[0]), iso = GlFmt.Iso(r[0]), code = GlDb.S(r[1]).Trim(), key, name = a == null ? "" : a.Name, sub = a != null && a.Kind == "SUBLEDGER", side = GlDb.S(r[3]), amount = GlFmt.Money(GlDb.L(r[4])), particulars = GlDb.S(r[5]) };
                    }).ToList();
                return GlApi.Ok(new { voucher, lines, count = n, diff, diffText = GlFmt.Money(diff), balanced = diff == 0 });
            }
        });
    }

    [WebMethod(EnableSession = true)]
    public static string PreviewAdjustment(string json)
    {
        return GlApi.Read(GlAccess.Adjust, delegate
        {
            var d = FaJson.Parse(json);
            int? v = FaJson.IntN(d, "voucher");
            return GlApi.Ok(new { preview = GlAdjust.Preview(FaJson.Str(d, "mode") == "COMPLETE" ? "COMPLETE" : "BALANCED", v.HasValue ? (long?)v.Value : null, FaJson.Str(d, "entryDate"), d.ContainsKey("lines") ? d["lines"] : null) });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string SaveAdjustmentDraft(string json)
    {
        return GlApi.Write(GlAccess.Adjust, delegate { long id = GlAdjust.SaveDraft(FaJson.Parse(json)); return GlApi.Ok(new { id, entry = GlAdjust.Get(id), message = "Draft saved." }); });
    }

    [WebMethod(EnableSession = true)]
    public static string SubmitAdjustment(long id, int version)
    {
        return GlApi.Write(GlAccess.Adjust, delegate { GlAdjust.Submit(id, version); return GlApi.Ok(new { entry = GlAdjust.Get(id), message = "Submitted for approval. Another approver must post it." }); });
    }

    [WebMethod(EnableSession = true)]
    public static string TakeBackAdjustment(long id, int version, string reason)
    {
        return GlApi.Write(GlAccess.Adjust, delegate { GlAdjust.ReturnToDraft(id, version, reason); return GlApi.Ok(new { entry = GlAdjust.Get(id), message = "Taken back to draft." }); });
    }

    [WebMethod(EnableSession = true)]
    public static string CancelAdjustment(long id, int version, string reason)
    {
        return GlApi.Write(GlAccess.Adjust, delegate { GlAdjust.Cancel(id, version, reason); return GlApi.Ok(new { entry = GlAdjust.Get(id), message = "Cancelled. It stays on record." }); });
    }

    [WebMethod(EnableSession = true)]
    public static string RejectAdjustment(long id, int version, string reason)
    {
        return GlApi.Write(GlAccess.AdjustApprove, delegate { GlAdjust.Reject(id, version, reason); return GlApi.Ok(new { entry = GlAdjust.Get(id), message = "Rejected. The maker sees your reason." }); });
    }

    [WebMethod(EnableSession = true)]
    public static string ApproveAdjustment(long id, int version, string note, string overrideNote)
    {
        return GlApi.Write(GlAccess.AdjustApprove, delegate
        {
            GlAdjust.PostResult r = GlAdjust.Approve(id, version, note, overrideNote, false);
            return GlApi.Ok(new { entry = GlAdjust.Get(id), message = "Posted: " + r.Lines + " new ledger lines under voucher " + r.Voucher + ". The ledger grew from " + GlFmt.Count(r.LedgerBefore) + " to " + GlFmt.Count(r.LedgerAfter) + " lines." });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string CreateReversalDraft(long id)
    {
        return GlApi.Write(GlAccess.Adjust, delegate { long nid = GlAdjust.DraftReversal(id); return GlApi.Ok(new { id = nid, message = "A reversal draft was prepared. Review it, then submit." }); });
    }
}
