using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Web.Services;

// General Ledger: Finance Warnings (plan sections 3 and 5). Detection is read only; the page's own
// writes (acknowledge, reopen, assign, note, accept a record, run now) go through GlWrite and gl_audit.
public partial class COOPERP_NewScreens_AccountsWarnings : System.Web.UI.Page
{
    protected string BootJson = "{}";

    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, GlAccess.Warnings);
        var boot = new Dictionary<string, object>();
        try
        {
            GlWarnings.EnsureFresh(GlAccess.User());
            using (var c = GlDb.Read())
            {
                boot["summary"] = Summary(c);
                if (GlAccess.Can(GlAccess.WarningsManage)) boot["users"] = GlWarnings.FinanceUsers(c);
            }
            boot["rules"] = GlWarnings.Rules.Select(r => new { code = r.Code, title = r.Title, severity = r.Severity, detection = r.Detection, cause = r.Cause, recordAck = r.RecordAck, fixes = r.Fixes }).ToList();
        }
        catch (Exception ex)
        {
            GlLog.Error("AccountsWarnings.Load", ex);
            boot["error"] = ex is GlRefusal ? ex.Message : "The warnings could not be loaded. Reload the page, and tell MIS if it keeps happening.";
        }
        boot["rights"] = GlAccess.RightsJson();
        BootJson = FaJson.Ser(boot).Replace("<", "\\u003c");
    }

    private static object Summary(MySql.Data.MySqlClient.MySqlConnection c)
    {
        GlWarnings.Summary s = GlWarnings.GetSummary(c);
        int[] h = GlWarnings.Health(c, null);
        var lost = GlDb.Table(c, null, "SELECT rule_code, severity, COUNT(*), SUM(amount) FROM gl_warning WHERE status IN ('OPEN','REAPPEARED') GROUP BY rule_code, severity").Rows.Cast<DataRow>()
            .GroupBy(r => GlDb.S(r[0])).Select(g =>
            {
                string worst = g.Select(r => GlDb.S(r[1])).OrderByDescending(v => GlWarnings.Weight(v)).First();
                GlRule rule = GlWarnings.Rule(g.Key);
                return new { rule = g.Key, title = rule == null ? g.Key : rule.Title, severity = worst, points = GlWarnings.Weight(worst), open = g.Sum(r => GlDb.I(r[2])), amount = GlFmt.Money(g.Sum(r => GlDb.M(r[3]))) };
            }).Where(x => x.points > 0).OrderByDescending(x => x.points).ThenBy(x => x.rule).ToList();
        return new
        {
            open = s.Open, critical = s.Critical, high = s.High, medium = s.Medium, info = s.Info, acknowledged = s.Acknowledged, fixedThisMonth = s.FixedThisMonth,
            health = s.Health, criticalRules = h[1], highRules = h[2], mediumRules = h[3], lost = lost,
            lastRun = s.LastRun.HasValue ? GlFmt.When(s.LastRun.Value) : "", oldest = s.OldestOpen.HasValue ? GlFmt.Date(s.OldestOpen.Value) : "", running = s.Running,
            interval = GlSettings.Int("detection_interval_hours", 6)
        };
    }

    [WebMethod(EnableSession = true)]
    public static string LoadWarnings(string status)
    {
        return GlApi.Read(GlAccess.Warnings, delegate
        {
            string where = status == "ACKNOWLEDGED" ? "status = 'ACKNOWLEDGED'" : status == "FIXED" ? "status = 'FIXED'" : "status IN ('OPEN','REAPPEARED')";
            using (var c = GlDb.Read())
            {
                var rows = GlDb.Table(c, null,
                    "SELECT id, rule_code, scope_key, title, severity, status, record_count, amount, cause, where_text, first_detected, last_seen, fixed_at, IFNULL(assigned_to,''), IFNULL(ack_by,''), ack_at, IFNULL(ack_reason,'') " +
                    "FROM gl_warning WHERE " + where + " ORDER BY FIELD(severity,'CRITICAL','HIGH','MEDIUM','INFO'), amount DESC LIMIT 500").Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        id = GlDb.L(r[0]), rule = GlDb.S(r[1]), scope = GlDb.S(r[2]), title = GlDb.S(r[3]), severity = GlDb.S(r[4]), status = GlDb.S(r[5]), count = GlDb.L(r[6]), amount = GlFmt.Money(GlDb.M(r[7])),
                        cause = GlDb.S(r[8]), where = GlDb.S(r[9]), first = GlFmt.When(r[10]), last = GlFmt.When(r[11]), fixedAt = GlFmt.When(r[12]), assigned = GlDb.S(r[13]),
                        ackBy = GlDb.S(r[14]), ackAt = GlFmt.When(r[15]), ackReason = GlDb.S(r[16])
                    }).ToList();
                return GlApi.Ok(new { rows, summary = Summary(c) });
            }
        });
    }

    [WebMethod(EnableSession = true)]
    public static string LoadWarningDetail(long id)
    {
        return GlApi.Read(GlAccess.Warnings, delegate
        {
            using (var c = GlDb.Read())
            {
                DataRow w = GlDb.Table(c, null, "SELECT id, rule_code, scope_key, IFNULL(sample_json,'') FROM gl_warning WHERE id=@i", "@i", id).Rows.Cast<DataRow>().FirstOrDefault();
                if (w == null) throw new GlRefusal("That warning no longer exists.");
                var events = GlDb.Table(c, null, "SELECT event_type, IFNULL(detail,''), record_count, amount, actor, created_at FROM gl_warning_event WHERE warning_id=@i ORDER BY id DESC LIMIT 100", "@i", id).Rows.Cast<DataRow>()
                    .Select(r => new { type = GlDb.S(r[0]), detail = GlDb.S(r[1]), count = r[2] is DBNull ? "" : GlFmt.Count(GlDb.L(r[2])), amount = r[3] is DBNull ? "" : GlFmt.Money(GlDb.M(r[3])), actor = GlDb.S(r[4]), when = GlFmt.When(r[5]) }).ToList();
                var trend = GlDb.Table(c, null,
                    "SELECT t.run_id, r.finished_at, t.record_count, t.amount FROM gl_warning_trend t JOIN gl_warning_run r ON r.id = t.run_id WHERE t.rule_code=@r AND t.scope_key=@s ORDER BY t.run_id DESC LIMIT 40",
                    "@r", GlDb.S(w[1]), "@s", GlDb.S(w[2])).Rows.Cast<DataRow>().Reverse()
                    .Select(r => new { when = GlFmt.When(r[1]), count = GlDb.L(r[2]), amount = GlDb.M(r[3]) }).ToList();
                object sample = null;
                try { if (GlDb.S(w[3]) != "") sample = FaJson.J.DeserializeObject(GlDb.S(w[3])); } catch { }
                return GlApi.Ok(new { events, trend, sample });
            }
        });
    }

    /// <summary>The records behind a warning, found again now (not the stored sample), paged, with acceptances marked.</summary>
    [WebMethod(EnableSession = true)]
    public static string LoadRecords(long id, int page, int size, string search)
    {
        return GlApi.Read(GlAccess.Warnings, delegate
        {
            string rule, scope;
            using (var c = GlDb.Read())
            {
                DataRow w = GlDb.Table(c, null, "SELECT rule_code, scope_key FROM gl_warning WHERE id=@i", "@i", id).Rows.Cast<DataRow>().FirstOrDefault();
                if (w == null) throw new GlRefusal("That warning no longer exists.");
                rule = GlDb.S(w[0]); scope = GlDb.S(w[1]);
            }
            GlFinding f = GlWarnings.DetectOnly(rule).FirstOrDefault(x => x.Scope == scope);
            var recs = f == null || f.Records == null ? new List<GlRec>() : f.Records;
            string q = (search ?? "").Trim().ToLowerInvariant();
            if (q != "") recs = recs.Where(r => (r.key + " " + r.reference + " " + r.account + " " + r.text + " " + r.date).ToLowerInvariant().Contains(q)).ToList();
            var acks = new Dictionary<string, long>();
            using (var c = GlDb.Read())
                foreach (DataRow r in GlDb.Table(c, null, "SELECT id, record_key FROM gl_record_ack WHERE rule_code=@r AND is_active=1", "@r", rule).Rows) acks[GlDb.S(r[1])] = GlDb.L(r[0]);
            size = Math.Max(10, Math.Min(250, size)); page = Math.Max(1, page);
            var rows = recs.Skip((page - 1) * size).Take(size).Select(r => new
            {
                key = r.key, date = r.date, reference = r.reference, account = r.account, text = r.text, amount = GlFmt.Money(r.amount), link = r.link,
                ackId = acks.ContainsKey(r.key ?? "") ? acks[r.key] : 0
            }).ToList();
            return GlApi.Ok(new
            {
                rows, total = recs.Count, page, size, found = f != null, recordAck = GlWarnings.Rule(rule).RecordAck,
                note = f == null ? "The rule no longer finds anything for this warning. The next detection run will mark it fixed." : f.Records == null ? "This warning has no record list; its cause and where it applies are shown above." : ""
            });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string LoadHistory()
    {
        return GlApi.Read(GlAccess.Warnings, delegate
        {
            using (var c = GlDb.Read())
            {
                var runs = GlDb.Table(c, null, "SELECT id, started_at, finished_at, started_by, trigger_kind, duration_ms, rules_run, open_total, critical_open, high_open, medium_open, health_score, IFNULL(errors,'') FROM gl_warning_run ORDER BY id DESC LIMIT 60").Rows.Cast<DataRow>()
                    .Select(r => new { id = GlDb.L(r[0]), started = GlFmt.When(r[1]), finished = GlFmt.When(r[2]), by = GlDb.S(r[3]), trigger = GlDb.S(r[4]), seconds = r[5] is DBNull ? "" : (GlDb.L(r[5]) / 1000m).ToString("0.0", CultureInfo.InvariantCulture),
                                      rules = GlDb.I(r[6]), open = GlDb.I(r[7]), critical = GlDb.I(r[8]), high = GlDb.I(r[9]), medium = GlDb.I(r[10]), health = r[11] is DBNull ? (int?)null : GlDb.I(r[11]), errors = GlDb.S(r[12]) }).ToList();
                var byRule = GlDb.Table(c, null,
                    "SELECT t.rule_code, t.run_id, SUM(t.record_count), SUM(t.amount) FROM gl_warning_trend t WHERE t.run_id IN (SELECT id FROM (SELECT id FROM gl_warning_run ORDER BY id DESC LIMIT 30) z) GROUP BY t.rule_code, t.run_id ORDER BY t.rule_code, t.run_id").Rows.Cast<DataRow>()
                    .GroupBy(r => GlDb.S(r[0])).Select(g => new { rule = g.Key, title = GlWarnings.Rule(g.Key) == null ? g.Key : GlWarnings.Rule(g.Key).Title, counts = g.Select(r => GlDb.L(r[2])).ToList(), amounts = g.Select(r => GlDb.M(r[3])).ToList(), last = GlFmt.Money(GlDb.M(g.Last()[3])), lastCount = GlFmt.Count(GlDb.L(g.Last()[2])) }).ToList();
                return GlApi.Ok(new { runs, byRule });
            }
        });
    }

    [WebMethod(EnableSession = true)]
    public static string RunDetectionNow()
    {
        return GlApi.Write(GlAccess.WarningsManage, delegate
        {
            GlWarnings.RunResult r = GlWarnings.Run("MANUAL", GlAccess.User());
            if (r == null) throw new GlRefusal("A detection run is already in progress. Wait a minute and reload.");
            System.Web.HttpRuntime.Cache.Remove("gl:badge");
            return GlApi.Ok(new { message = "Detection finished in " + (r.Ms / 1000m).ToString("0.0", CultureInfo.InvariantCulture) + " s: " + r.Found + " findings, " + r.New + " new, " + r.Reappeared + " reappeared, " + r.Fixed + " fixed." + (r.Errors.Count > 0 ? " " + r.Errors.Count + " rule(s) failed; see History." : "") });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string SaveAcknowledgement(long id, string reason)
    {
        return GlApi.Write(GlAccess.WarningsManage, delegate { GlWrite.Acknowledge(id, reason); return GlApi.Ok(new { message = "Acknowledged. It no longer counts in the health score unless it grows." }); });
    }

    [WebMethod(EnableSession = true)]
    public static string ReopenWarning(long id, string reason)
    {
        return GlApi.Write(GlAccess.WarningsManage, delegate { GlWrite.Reopen(id, reason); return GlApi.Ok(new { message = "Reopened." }); });
    }

    [WebMethod(EnableSession = true)]
    public static string AssignWarning(long id, string user, string note)
    {
        return GlApi.Write(GlAccess.WarningsManage, delegate { GlWrite.Assign(id, user, note); return GlApi.Ok(new { message = string.IsNullOrEmpty(user) ? "Unassigned." : "Assigned to " + user + "." }); });
    }

    [WebMethod(EnableSession = true)]
    public static string AddWarningNote(long id, string text)
    {
        return GlApi.Write(GlAccess.WarningsManage, delegate { GlWrite.Note(id, text); return GlApi.Ok(new { message = "Note added." }); });
    }

    [WebMethod(EnableSession = true)]
    public static string SaveRecordAcceptance(long id, string key, string reason, string amount)
    {
        return GlApi.Write(GlAccess.WarningsManage, delegate
        {
            string rule;
            using (var c = GlDb.Read()) rule = GlDb.S(GlDb.Scalar(c, null, "SELECT rule_code FROM gl_warning WHERE id=@i", "@i", id));
            decimal a; decimal? amt = decimal.TryParse((amount ?? "").Replace(",", "").Replace("(", "-").Replace(")", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out a) ? a : (decimal?)null;
            GlWrite.AckRecord(rule, key, reason, amt);
            return GlApi.Ok(new { message = "Record accepted. The next detection run leaves it out." });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string RemoveRecordAcceptance(long ackId, string reason)
    {
        return GlApi.Write(GlAccess.WarningsManage, delegate { GlWrite.RevokeRecordAck(ackId, reason); return GlApi.Ok(new { message = "Acceptance withdrawn. The record counts again from the next run." }); });
    }
}
