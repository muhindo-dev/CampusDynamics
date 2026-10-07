using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Web.Services;

// General Ledger: periods and close (plan section 3). Months and years with their movement and checks,
// the review and sign-off record, and the year-end closing entry prepared as a draft adjusting entry.
public partial class COOPERP_NewScreens_AccountsPeriods : System.Web.UI.Page
{
    protected string BootJson = "{}";

    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, GlAccess.Periods);
        var boot = new Dictionary<string, object>();
        try
        {
            GlCalc.Snapshot s = GlCalc.Get(false);
            using (var c = GlDb.Read())
            {
                var years = GlCalc.Years(c);
                GlCalc.FinYear cur = GlCalc.YearOf(years, DateTime.Today);
                boot["years"] = years.OrderByDescending(y => y.Start).Select(y => new { key = "FY:" + GlFmt.Iso(y.Start), label = y.Label, dates = y.DateLabel, status = y.Status, current = cur != null && cur.Id == y.Id }).ToList();
            }
            boot["equity"] = s.Accounts.Values.Where(a => a.Kind == "CHART" && a.Category == "Equity").OrderBy(a => a.Code).Select(a => new { code = a.Code, name = a.Code + " " + a.Name }).ToList();
        }
        catch (Exception ex)
        {
            GlLog.Error("AccountsPeriods.Load", ex);
            boot["error"] = ex is GlRefusal ? ex.Message : "Periods could not be loaded. Reload the page, and tell MIS if it keeps happening.";
        }
        boot["rights"] = GlAccess.RightsJson();
        BootJson = FaJson.Ser(boot).Replace("<", "\\u003c");
    }

    private static object Status(DataRow r)
    {
        if (r == null) return new { action = "", by = "", when = "" };
        return new { action = GlDb.S(r[0]), by = GlDb.S(r[1]), when = GlFmt.When(r[2]) };
    }

    [WebMethod(EnableSession = true)]
    public static string LoadPeriods(string yearKey)
    {
        return GlApi.Read(GlAccess.Periods, delegate
        {
            GlCalc.Snapshot s = GlCalc.Get(false);
            using (var c = GlDb.Read())
            {
                GlPeriods.Period y = GlPeriods.Resolve(c, yearKey);
                if (y.Kind != "YEAR") throw new GlRefusal("Choose a financial year.");
                var latest = GlDb.Table(c, null, "SELECT p.period_key, p.action, p.actor, p.created_at FROM gl_period_signoff p WHERE p.id = (SELECT MAX(q.id) FROM gl_period_signoff q WHERE q.period_key = p.period_key)").Rows.Cast<DataRow>()
                    .ToDictionary(r => GlDb.S(r[0]), r => r);
                long nvd; int nvl;
                var ub = GlCalc.UnbalancedVouchers(null, new DateTime(2099, 12, 31), "WHOLE", out nvd, out nvl);
                Func<string, DateTime, DateTime, string, object> row = (key, a, b, label) =>
                {
                    GlCalc.Totals t = GlCalc.Total(s, a, b, "WHOLE");
                    DataRow st; latest.TryGetValue(key, out st);
                    return new
                    {
                        key, label, from = GlFmt.Iso(a), to = GlFmt.Iso(b), lines = GlFmt.Count(t.Lines), dr = GlFmt.Money(t.Dr), cr = GlFmt.Money(t.Cr), diff = GlFmt.Money(t.Diff), balanced = t.Diff == 0,
                        unbalanced = ub.Count(v => v.First >= a && v.First <= b), ended = b < DateTime.Today, started = a <= DateTime.Today,
                        status = st == null ? "" : GlDb.S(st[1]), statusBy = st == null ? "" : GlDb.S(st[2]), statusWhen = st == null ? "" : GlFmt.When(st[3])
                    };
                };
                var months = new List<object>();
                for (DateTime m = new DateTime(y.From.Year, y.From.Month, 1); m <= y.To; m = m.AddMonths(1))
                {
                    DateTime a = m < y.From ? y.From : m, b = m.AddMonths(1).AddDays(-1) > y.To ? y.To : m.AddMonths(1).AddDays(-1);
                    months.Add(row(m.ToString("yyyy-MM", CultureInfo.InvariantCulture), a, b, m.ToString("MMMM yyyy", CultureInfo.InvariantCulture)));
                }
                return GlApi.Ok(new { year = row(y.Key, y.From, y.To, y.Label), months, yearStatus = y.Year.Status });
            }
        });
    }

    [WebMethod(EnableSession = true)]
    public static string LoadPeriodChecklist(string key)
    {
        return GlApi.Read(GlAccess.Periods, delegate
        {
            GlPeriods.Period p; List<object> hist;
            using (var c = GlDb.Read()) { p = GlPeriods.Resolve(c, key); hist = GlPeriods.History(c, key); }
            var items = GlPeriods.Checklist(p);
            object close = null;
            if (p.Kind == "YEAR")
            {
                GlCalc.Snapshot s = GlCalc.Get(false);
                int e = GlCalc.DayInt(p.To); long ie = 0; var accts = new HashSet<string>();
                foreach (GlAgg g in s.Agg) if (g.Day <= e) { GlAcct a = s.Accounts[g.Key]; if ((a.Kind == "CHART" || a.Kind == "PROVISIONAL") && (a.Category == "Income" || a.Category == "Expense")) { ie += g.Dr - g.Cr; accts.Add(a.Key); } }
                close = new { net = GlFmt.Money(Math.Abs(ie)), surplus = ie < 0, accounts = accts.Count, date = GlFmt.Date(p.To) };
            }
            return GlApi.Ok(new { key = p.Key, kind = p.Kind, label = p.Label, from = GlFmt.Date(p.From), to = GlFmt.Date(p.To), ended = p.To < DateTime.Today, items, history = hist, close });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string SavePeriodAction(string key, string action, string note)
    {
        return GlApi.Write(GlAccess.PeriodsManage, delegate
        {
            GlPeriods.Record(key, action, note);
            return GlApi.Ok(new { message = action == "SIGNED_OFF" ? "Signed off. Later postings into this period will be flagged." : action == "REOPENED" ? "Reopened." : "Review recorded." });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string CreateYearEndDraft(string key, string retained)
    {
        return GlApi.Write(GlAccess.PeriodsManage, delegate
        {
            if (!GlAccess.Can(GlAccess.Adjust)) throw new GlRefusal("Preparing the closing entry also needs permission to make adjusting entries.");
            GlPeriods.Period p; using (var c = GlDb.Read()) p = GlPeriods.Resolve(c, key);
            if (p.Kind != "YEAR") throw new GlRefusal("Choose a financial year.");
            if (p.To >= DateTime.Today) throw new GlRefusal("The year has not ended.");
            long id = GlAdjust.DraftYearEnd(p.Year, retained);
            return GlApi.Ok(new { id, message = "The closing entry was prepared as a draft adjusting entry. Review it, then submit it for approval." });
        });
    }
}
