using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Web.Services;

// General Ledger: voucher (plan section 3). Every line under one voucher number, its balance check and
// pattern, the source records behind it, and lines edited or deleted under the same number.
public partial class COOPERP_NewScreens_AccountsVoucher : System.Web.UI.Page
{
    protected string BootJson = "{}";

    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, GlAccess.Voucher);
        var boot = new Dictionary<string, object>();
        long v;
        if (long.TryParse(Request.QueryString["v"], out v) && v > 0)
        {
            try { boot = Load(v); }
            catch (Exception ex)
            {
                if (!(ex is GlRefusal)) GlLog.Error("AccountsVoucher.Load", ex);
                boot["error"] = ex is GlRefusal ? ex.Message : "The voucher could not be loaded. Reload the page, and tell MIS if it keeps happening.";
            }
        }
        boot["rights"] = GlAccess.RightsJson();
        BootJson = FaJson.Ser(boot).Replace("<", "\\u003c");
    }

    private static Dictionary<string, object> Load(long v)
    {
        var d = new Dictionary<string, object>();
        GlCalc.Snapshot s = GlCalc.Get(false);
        d["voucher"] = v;
        using (var c = GlDb.Read())
        {
            DataTable t = GlDb.Table(c, null,
                "SELECT TID, transactionDate, accountcode, account_type, transactionType, CAST(transaction_amount AS SIGNED), particulars, IFNULL(source_system,''), teller, timeLog, IFNULL(RefNo,''), IFNULL(folio,''), IFNULL(tracking_ref,0), IFNULL(journal_no,'') " +
                "FROM fin_ledger WHERE voucherNo = @v ORDER BY transactionDate, TID LIMIT 2001", "@v", v);
            if (t.Rows.Count == 0 && GlDb.L(GlDb.Scalar(c, null, "SELECT COUNT(*) FROM fin_deleted_ledger WHERE voucherNo=@v", "@v", v)) == 0)
                throw new GlRefusal("No ledger line carries voucher number " + v.ToString(CultureInfo.InvariantCulture) + ".");
            long dr = 0, cr = 0;
            var lines = new List<object>();
            var trackingRefs = new HashSet<long>(); var journals = new HashSet<string>(); var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in t.Rows)
            {
                string code = GlDb.S(r[2]).Trim(), type = GlDb.S(r[3]);
                string key = GlCalc.KeyOf(s, code, type);
                GlAcct a; s.Accounts.TryGetValue(key, out a);
                long amt = GlDb.L(r[5]); bool isDr = GlDb.S(r[4]) == "DR";
                if (isDr) dr += amt; else cr += amt;
                if (GlDb.L(r[12]) > 0) trackingRefs.Add(GlDb.L(r[12]));
                if (GlDb.S(r[13]).Trim() != "") journals.Add(GlDb.S(r[13]).Trim());
                if (GlDb.S(r[10]) != "") refs.Add(GlDb.S(r[10]));
                lines.Add(new
                {
                    tid = GlDb.L(r[0]), date = GlFmt.Date(r[1]), iso = GlFmt.Iso(r[1]), code, type,
                    key, accountName = a == null ? "" : a.Name, kind = a == null ? "UNMAPPED" : a.Kind, member = a != null && a.Kind == "SUBLEDGER" ? code : "",
                    dr = isDr ? GlFmt.Money(amt) : "", cr = isDr ? "" : GlFmt.Money(amt), particulars = GlDb.S(r[6]), source = GlDb.S(r[7]), teller = GlDb.S(r[8]),
                    recorded = GlFmt.When(r[9]), refNo = GlDb.S(r[10]), folio = GlDb.S(r[11]).Trim()
                });
            }
            d["lines"] = lines.Take(2000).ToList();
            d["truncated"] = t.Rows.Count > 2000;
            d["totals"] = new { dr = GlFmt.Money(dr), cr = GlFmt.Money(cr), diff = GlFmt.Money(dr - cr), balanced = dr == cr, rawDiff = dr - cr };
            var dates = t.Rows.Cast<DataRow>().Select(r => GlFmt.Iso(r[1])).Distinct().Count();
            var sources = t.Rows.Cast<DataRow>().Select(r => GlDb.S(r[7])).Distinct().Count();
            d["shape"] = new { dates, sources, lines = t.Rows.Count };

            long nvd; int nvl;
            GlVoucherIssue issue = GlCalc.UnbalancedVouchers(null, new DateTime(2099, 12, 31), "WHOLE", out nvd, out nvl).FirstOrDefault(x => x.VoucherNo == v);
            if (issue != null) d["issue"] = new { pattern = issue.Pattern, text = GlCalc.PatternText(issue.Pattern), cause = GlCalc.PatternCause(issue.Pattern) };
            else if (dates > 1 || sources > 1) d["issue"] = new { pattern = "REUSED_BALANCED", text = "The number is used on " + dates + " dates by " + sources + " sources", cause = "It balances overall, but the lines may belong to unrelated postings that only share the number." };

            // Source records
            {
                var prm = new List<object> { "@v", v }; var names = new List<string>(); int i = 0;
                foreach (string j in journals.Take(20)) { names.Add("@j" + i); prm.Add("@j" + i); prm.Add(j); i++; }
                d["journals"] = GlDb.Table(c, null, "SELECT JournalNo, PostStatus, journalType, journalDate, journalParticulars, Teller FROM fin_journalnumbers WHERE GL_VoucherNo = @v" +
                                                    (names.Count > 0 ? " OR JournalNo IN (" + string.Join(",", names) + ")" : "") + " LIMIT 20", prm.ToArray()).Rows.Cast<DataRow>()
                    .Select(r => new { no = GlDb.S(r[0]).Trim(), status = GlDb.S(r[1]), type = GlDb.S(r[2]), date = GlFmt.Date(r[3]), particulars = GlDb.S(r[4]), by = GlDb.S(r[5]) }).ToList();
            }
            if (trackingRefs.Count > 0)
            {
                var ids = trackingRefs.Take(200).ToList(); var prm = new List<object>(); var names = new List<string>();
                for (int i = 0; i < ids.Count; i++) { names.Add("@t" + i); prm.Add("@t" + i); prm.Add(ids[i]); }
                d["tracking"] = GlDb.Table(c, null, "SELECT TID, regno, trans_type, amount, item_code, acadyear, semester, detail, trans_date, post_status FROM fin_studentfeestracking WHERE TID IN (" + string.Join(",", names) + ")", prm.ToArray()).Rows.Cast<DataRow>()
                    .Select(r => new { tid = GlDb.L(r[0]), regno = GlDb.S(r[1]), type = GlDb.S(r[2]), amount = GlFmt.Money(GlDb.M(r[3])), item = GlDb.S(r[4]), year = GlDb.S(r[5]), sem = GlDb.S(r[6]), detail = GlDb.S(r[7]), date = GlFmt.Date(r[8]), status = GlDb.S(r[9]) }).ToList();
            }
            if (refs.Count > 0)
            {
                var prm = new List<object>(); var names = new List<string>(); int i = 0;
                foreach (string rf in refs.Take(50)) { names.Add("@r" + i); prm.Add("@r" + i); prm.Add(rf); i++; }
                d["requisitions"] = GlDb.Table(c, null, "SELECT ID, req_number, title, status, total_amount FROM campus_dynamics.sys_requisitions WHERE ledger_ref IN (" + string.Join(",", names) + ")", prm.ToArray()).Rows.Cast<DataRow>()
                    .Select(r => new { id = GlDb.L(r[0]), no = GlDb.S(r[1]), title = GlDb.S(r[2]), status = GlDb.S(r[3]), amount = GlFmt.Money(GlDb.M(r[4])) }).ToList();
            }

            // Lines edited or deleted under this number
            d["deleted"] = GlDb.Table(c, null, "SELECT TID, transactionDate, accountcode, transactionType, transaction_amount, particulars, delete_date, IFNULL(deleted_by,'unknown') FROM fin_deleted_ledger WHERE voucherNo=@v ORDER BY delete_date LIMIT 500", "@v", v).Rows.Cast<DataRow>()
                .Select(r => new { tid = GlDb.L(r[0]), date = GlFmt.Date(r[1]), code = GlDb.S(r[2]), side = GlDb.S(r[3]), amount = GlFmt.Money(GlDb.M(r[4])), particulars = GlDb.S(r[5]), when = r[6] is DBNull ? "Undated" : GlFmt.When(r[6]), by = GlDb.S(r[7]) }).ToList();
            d["edited"] = GlDb.Table(c, null, "SELECT TID, accountcode, IFNULL(old_transactionType,''), IFNULL(old_transaction_amount,''), IFNULL(new_transactionType,''), IFNULL(new_transaction_amount,''), trigger_date, IFNULL(triggered_by,'unknown') FROM edit_ledger WHERE voucherNo=@v ORDER BY trigger_date LIMIT 500", "@v", v).Rows.Cast<DataRow>()
                .Select(r => new { tid = GlDb.L(r[0]), code = GlDb.S(r[1]), before = GlDb.S(r[2]) + " " + GlDb.S(r[3]), after = GlDb.S(r[4]) + " " + GlDb.S(r[5]), when = r[6] is DBNull ? "Undated" : GlFmt.When(r[6]), by = GlDb.S(r[7]) }).ToList();

            // Acceptance on file
            string vk = v.ToString(CultureInfo.InvariantCulture);
            d["acks"] = GlDb.Table(c, null, "SELECT id, rule_code, reason, created_by, created_at FROM gl_record_ack WHERE rule_code IN ('W01','W05') AND record_key=@k AND is_active=1", "@k", vk).Rows.Cast<DataRow>()
                .Select(r => new { id = GlDb.L(r[0]), rule = GlDb.S(r[1]), reason = GlDb.S(r[2]), by = GlDb.S(r[3]), when = GlFmt.When(r[4]) }).ToList();
        }
        return d;
    }

    [WebMethod(EnableSession = true)]
    public static string SaveAcceptance(string rule, long voucher, string reason)
    {
        return GlApi.Write(GlAccess.WarningsManage, delegate
        {
            if (rule != "W01" && rule != "W05") throw new GlRefusal("Choose what to accept.");
            long id = GlWrite.AckRecord(rule, voucher.ToString(CultureInfo.InvariantCulture), reason, null);
            return GlApi.Ok(new { id, message = "Accepted. The next detection run leaves this voucher out of " + rule + "." });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string RemoveAcceptance(long id, string reason)
    {
        return GlApi.Write(GlAccess.WarningsManage, delegate
        {
            GlWrite.RevokeRecordAck(id, reason);
            return GlApi.Ok(new { message = "Acceptance withdrawn. The voucher counts again from the next detection run." });
        });
    }
}
