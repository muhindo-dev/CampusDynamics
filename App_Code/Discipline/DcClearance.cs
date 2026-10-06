using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using MySql.Data.MySqlClient;

// =====================================================================
//  Student Disciplinary module: the clearance question asked by other
//  modules (plan 6.7). Graduation (C9), the transcript and certificate
//  print gate, exam clearance, the exam card and the staff banners all
//  ask here, and the answer comes from the database functions that the
//  registration triggers and the portal use too (plan D3).
//
//  Every method fails OPEN for a database error on purpose: an outage of
//  this module must not stop graduation or printing; the error is logged.
//  The registration triggers and the portal block are the hard guards.
// =====================================================================
public class DcClearanceResult
{
    public string Regno = "";
    public int OpenCases;
    public HashSet<string> Effects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public bool PendingCancellation;          // a decided cancellation the marks office has not yet carried out
    public List<string> CaseNos = new List<string>();
    public string Summary = "";               // "Suspended until 30 Jun 2027 under DC/2026-27/0042"

    public bool Has(string effect) { return Effects.Contains(effect); }
    public bool BlocksGraduation { get { return Has("GRADUATION_BAR") || Has("EXPULSION") || Has("SUSPENSION") || PendingCancellation; } }
    public bool BlocksExamCard { get { return Has("SUSPENSION") || Has("EXPULSION") || Has("RESULTS_WITHHELD"); } }
    public bool BlocksDocuments { get { return Has("RESULTS_WITHHELD") || Has("EXPULSION"); } }
    public bool BlocksRegistration { get { return Has("SUSPENSION") || Has("EXPULSION"); } }
    public bool BlocksExamClearance { get { return Has("SUSPENSION") || Has("EXPULSION"); } }
    public bool Any { get { return OpenCases > 0 || Effects.Count > 0 || PendingCancellation; } }
}

public static class DcClearance
{
    /// <summary>Students whose graduation is blocked by discipline (plan 6.4), for set-based counts (one regno per row).</summary>
    public const string GraduationBlockSql =
        "SELECT DISTINCT regno FROM dc_sanction WHERE " +
        "(status='ACTIVE' AND starts_on<=CURDATE() AND (ends_on IS NULL OR ends_on>=CURDATE()) " +
        " AND (FIND_IN_SET('GRADUATION_BAR',effects)>0 OR FIND_IN_SET('EXPULSION',effects)>0 OR FIND_IN_SET('SUSPENSION',effects)>0)) " +
        "OR (follow_up='PENDING' AND status IN ('ACTIVE','EXPIRED') AND (FIND_IN_SET('CANCEL_PAPER',effects)>0 OR FIND_IN_SET('CANCEL_SEMESTER',effects)>0))";

    /// <summary>The graduation check C9: BLOCK, WARN or "" (nothing to report), with a sentence a Registrar can act on.</summary>
    public static void ForGraduation(DcClearanceResult r, out string level, out string detail)
    {
        level = ""; detail = "";
        if (r == null) return;
        if (r.BlocksGraduation)
        {
            level = "BLOCK";
            detail = (r.Summary == "" ? "A disciplinary sanction is in force" : r.Summary) + ".";
        }
        else if (r.OpenCases > 0)
        {
            level = "WARN";
            detail = "Open disciplinary case " + string.Join(", ", r.CaseNos.ToArray()) + ". Graduation is not barred, but the case is undecided.";
        }
    }

    public static DcClearanceResult Check(string regno)
    {
        try { using (var c = FaDb.Open()) return Check(c, null, regno); }
        catch (Exception ex) { DcLog.Error("clearance " + regno, ex); return new DcClearanceResult { Regno = regno ?? "" }; }
    }

    public static DcClearanceResult Check(MySqlConnection c, MySqlTransaction tx, string regno)
    {
        var r = new DcClearanceResult { Regno = (regno ?? "").Trim() };
        if (r.Regno == "") return r;
        try
        {
            if (FaDb.Scalar(c, tx, "SELECT 1 FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='dc_case'") == null) return r;
            r.OpenCases = FaDb.I(FaDb.Scalar(c, tx, "SELECT dc_open_case_count(@r)", "@r", r.Regno));
            DataTable t = FaDb.Table(c, tx,
                "SELECT s.effects, s.ends_on, s.follow_up, s.status, s.starts_on, c.case_no, st.name FROM dc_sanction s JOIN dc_case c ON c.id=s.case_id " +
                "JOIN dc_sanction_type st ON st.id=s.sanction_type_id WHERE s.regno=@r AND s.status IN ('ACTIVE','EXPIRED') ORDER BY s.id", "@r", r.Regno);
            var parts = new List<string>();
            foreach (DataRow s in t.Rows)
            {
                string eff = FaDb.S(s["effects"]);
                bool live = FaDb.S(s["status"]) == "ACTIVE" && FaDb.D(s["starts_on"]).HasValue && FaDb.D(s["starts_on"]).Value <= DateTime.Today
                            && (!FaDb.D(s["ends_on"]).HasValue || FaDb.D(s["ends_on"]).Value >= DateTime.Today);
                if (FaDb.S(s["follow_up"]) == "PENDING" && (("," + eff + ",").Contains(",CANCEL_PAPER,") || ("," + eff + ",").Contains(",CANCEL_SEMESTER,")))
                { r.PendingCancellation = true; if (!r.CaseNos.Contains(FaDb.S(s["case_no"]))) r.CaseNos.Add(FaDb.S(s["case_no"])); }
                if (!live) continue;
                foreach (string e in eff.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (e == "FINE" || e == "RESTITUTION" || e == "CANCEL_PAPER" || e == "CANCEL_SEMESTER") continue;
                    r.Effects.Add(e);
                }
                if (DcFmt.Restrictions(eff) != "")
                {
                    if (!r.CaseNos.Contains(FaDb.S(s["case_no"]))) r.CaseNos.Add(FaDb.S(s["case_no"]));
                    parts.Add(FaDb.S(s["name"]) + (FaDb.D(s["ends_on"]).HasValue ? " until " + DcFmt.Date(s["ends_on"]) : "") + " under " + FaDb.S(s["case_no"]));
                }
            }
            if (r.CaseNos.Count == 0 && r.OpenCases > 0)
                foreach (DataRow o in FaDb.Table(c, tx, "SELECT case_no FROM dc_case WHERE regno=@r AND status NOT IN ('CLOSED','WITHDRAWN') ORDER BY id", "@r", r.Regno).Rows)
                    r.CaseNos.Add(FaDb.S(o[0]));
            r.Summary = string.Join("; ", parts.ToArray());
            if (r.Summary == "" && r.PendingCancellation) r.Summary = "A results cancellation decided under " + string.Join(", ", r.CaseNos.ToArray()) + " has not yet been carried out";
            if (r.Summary == "" && r.OpenCases > 0) r.Summary = "Open disciplinary case " + string.Join(", ", r.CaseNos.ToArray());
        }
        catch (Exception ex) { DcLog.Error("clearance " + r.Regno, ex); }
        return r;
    }

    /// <summary>Restrictions for many students at once (graduation lists): regno to result, only for students with something to report.</summary>
    public static Dictionary<string, DcClearanceResult> CheckMany(MySqlConnection c, IEnumerable<string> regnos)
    {
        var d = new Dictionary<string, DcClearanceResult>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        foreach (string r in regnos) { string x = (r ?? "").Trim(); if (x != "" && !list.Contains(x)) list.Add(x); }
        if (list.Count == 0) return d;
        try
        {
            if (FaDb.Scalar(c, null, "SELECT 1 FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='dc_case'") == null) return d;
            var hits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < list.Count; i += 500)
            {
                var sb = new StringBuilder();
                var p = new List<object>();
                for (int j = i; j < Math.Min(list.Count, i + 500); j++) { if (sb.Length > 0) sb.Append(','); sb.Append("@r" + j); p.Add("@r" + j); p.Add(list[j]); }
                foreach (DataRow r in FaDb.Table(c, null,
                    "SELECT regno FROM dc_case WHERE regno IN (" + sb + ") AND status NOT IN ('CLOSED','WITHDRAWN') " +
                    "UNION SELECT regno FROM dc_sanction WHERE regno IN (" + sb + ") AND (status='ACTIVE' OR follow_up='PENDING')", p.ToArray()).Rows)
                    hits.Add(FaDb.S(r[0]));
            }
            foreach (string h in hits) d[h] = Check(c, null, h);
        }
        catch (Exception ex) { DcLog.Error("clearance many", ex); }
        return d;
    }
}
