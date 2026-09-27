using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Web;
using MySql.Data.MySqlClient;

/// <summary>
/// Who may have a transcript or a certificate printed, and the record of every one that was.
///
/// THE RULE, and the reasoning behind each half of it.
///
/// From the 2026/2027 graduation cycle onward a graduation document is only produced for a
/// student the Registry has approved. Before that cycle nothing changes: those records were
/// compiled under the old arrangement, approvals were never recorded for them, and refusing
/// to print a 2019 graduate's transcript would be a fault, not a control.
///
/// The gate applies to FINALISTS ONLY, not to everybody registered in a gated year. That
/// distinction is the whole design. Of the 3,957 students whose effective year is 2026/2027,
/// only 255 have actually reached the end of their programme; the rest are in year one or two
/// and a transcript for them is a record of studies, which nobody needs approval for. Gating
/// on the year alone would have stopped roughly 3,700 people getting an ordinary statement of
/// their marks in order to catch 220 who need approving. Candidacy is already computed, and
/// kept current, in acad_grad_stats, so the narrow rule costs nothing to apply.
///
/// The year a student is judged on is the graduation list's year where they are on it, and
/// their latest registration year where they are not. Using registration alone would have been
/// wrong in exactly the case that matters: the 35 students approved for the 2026/2027 list
/// last registered in 2025/2026, because you finish your final semester in one year and are
/// presented in the next. Judging them on registration would have exempted precisely the
/// people the rule exists for.
/// </summary>
public static class GraduationPrintGate
{
    /// <summary>
    /// The first graduation cycle that requires approval. Anything earlier prints as it always
    /// did. Comparison is on the leading year, so "2026/2027" is 2026.
    /// </summary>
    public const int FIRST_GATED_YEAR = 2026;

    public const string TRANSCRIPT  = "TRANSCRIPT";
    public const string CERTIFICATE = "CERTIFICATE";

    /// <summary>What the gate decided about one student.</summary>
    public class Verdict
    {
        public string RegNo = "";
        public string Name = "";
        public bool   Allowed;
        public string Reason = "";        // why it was refused; empty when allowed
        public string AcadYear = "";      // the year judged on
        public bool   OnList;
        public bool   IsCandidate;
    }

    public class Outcome
    {
        public List<Verdict> All     = new List<Verdict>();
        public List<Verdict> Blocked = new List<Verdict>();
        public List<string>  Allowed = new List<string>();
        public string BatchId = Guid.NewGuid().ToString();

        public bool AnyBlocked  { get { return Blocked.Count > 0; } }
        public bool NothingLeft { get { return Allowed.Count == 0; } }
    }

    private static string ConnStr
    {
        get
        {
            // vacConnectionString is the campus_dynamics connection this application actually
            // declares; the other name is what a few newer classes look for first.
            var cs = ConfigurationManager.ConnectionStrings["vacConnectionString"];
            if (cs == null) cs = ConfigurationManager.ConnectionStrings["campus_dynamicsConnectionString"];
            return cs == null ? "" : cs.ConnectionString;
        }
    }

    /// <summary>"2026/2027" and "2026-2027" and "2026" all read as 2026; anything else as 0.</summary>
    public static int YearOf(string acadYear)
    {
        string s = (acadYear ?? "").Trim();
        if (s.Length < 4) return 0;
        int n;
        return int.TryParse(s.Substring(0, 4), out n) ? n : 0;
    }

    public static string KindOf(string documentType)
    {
        string v = (documentType ?? "").Trim().ToUpperInvariant();
        return v.StartsWith("CERTIFICATE") ? CERTIFICATE : TRANSCRIPT;
    }

    /// <summary>
    /// Judge a batch. One query for the whole selection rather than one per student, because
    /// the Registry prints forty at a time and this sits directly in front of that click.
    /// </summary>
    public static Outcome Check(IEnumerable<string> regnos, string documentType)
    {
        var outp = new Outcome();
        var wanted = new List<string>();
        if (regnos != null)
            foreach (string r in regnos)
            {
                string t = (r ?? "").Trim();
                if (t != "" && !wanted.Contains(t)) wanted.Add(t);
            }
        if (wanted.Count == 0) return outp;

        string kind = KindOf(documentType);
        var found = new Dictionary<string, Verdict>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using (var c = new MySqlConnection(ConnStr))
            {
                c.Open();
                var qs = new List<string>();
                using (var cmd = new MySqlCommand("", c))
                {
                    for (int i = 0; i < wanted.Count; i++)
                    {
                        qs.Add("@r" + i);
                        cmd.Parameters.AddWithValue("@r" + i, wanted[i]);
                    }
                    cmd.CommandText =
                        "SELECT s.regno, " +
                        "       TRIM(CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,''))) AS nm, " +
                        "       IFNULL(g.acadyear,'') AS list_year, " +
                        "       (g.regno IS NOT NULL) AS on_list, " +
                        "       IFNULL(st.is_candidate,0) AS is_cand, " +
                        "       IFNULL((SELECT MAX(r.acad_year) FROM acad_registration r " +
                        "                WHERE r.regno=s.regno AND IFNULL(r.acad_year,'')<>''),'') AS reg_year " +
                        "  FROM acad_student s " +
                        "  LEFT JOIN acad_graduands  g  ON g.regno  = s.regno " +
                        "  LEFT JOIN acad_grad_stats st ON st.regno = s.regno " +
                        " WHERE s.regno IN (" + string.Join(",", qs.ToArray()) + ")";

                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                        {
                            var v = new Verdict();
                            v.RegNo       = Convert.ToString(r["regno"]).Trim();
                            v.Name        = Convert.ToString(r["nm"]).Trim();
                            v.OnList      = Convert.ToInt32(r["on_list"]) == 1;
                            v.IsCandidate = Convert.ToInt32(r["is_cand"]) == 1;

                            string listYear = Convert.ToString(r["list_year"]).Trim();
                            string regYear  = Convert.ToString(r["reg_year"]).Trim();
                            v.AcadYear = listYear != "" ? listYear : regYear;

                            Decide(v, kind);
                            found[v.RegNo] = v;
                        }
                }
            }
        }
        catch (Exception ex)
        {
            // A gate that fails open would be no gate at all, and one that fails closed on a
            // database blip would stop the Registry working. It fails closed and says so, which
            // is recoverable by trying again and is never silent.
            foreach (string reg in wanted)
            {
                var v = new Verdict { RegNo = reg, Allowed = false,
                    Reason = "The graduation check could not be completed (" + ex.Message +
                             "). Nothing was printed. Try again." };
                outp.All.Add(v); outp.Blocked.Add(v);
            }
            return outp;
        }

        foreach (string reg in wanted)
        {
            Verdict v;
            if (!found.TryGetValue(reg, out v))
                v = new Verdict { RegNo = reg, Allowed = false,
                    Reason = "No student record was found for " + reg + "." };
            outp.All.Add(v);
            if (v.Allowed) outp.Allowed.Add(v.RegNo); else outp.Blocked.Add(v);
        }
        return outp;
    }

    /// <summary>The rule itself, in one place so it reads as one sentence.</summary>
    private static void Decide(Verdict v, string kind)
    {
        if (v.OnList) { v.Allowed = true; return; }              // approved: nothing else to ask

        if (YearOf(v.AcadYear) < FIRST_GATED_YEAR)               // before the gate began
        { v.Allowed = true; return; }

        if (!v.IsCandidate)                                      // not finishing: a record of studies
        { v.Allowed = true; return; }

        v.Allowed = false;
        v.Reason = string.Format(
            "{0} has reached the end of their programme in {1} but is not on the graduation list, " +
            "so a {2} cannot be issued. From {3}/{4} a graduation document is only produced for a " +
            "student the Academic Registry has approved. Approve them in the Graduation Centre " +
            "under Candidates, then print.",
            v.RegNo,
            v.AcadYear == "" ? "this cycle" : v.AcadYear,
            kind == CERTIFICATE ? "certificate" : "transcript",
            FIRST_GATED_YEAR, FIRST_GATED_YEAR + 1);
    }

    // ── Recording ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Write the batch down, and where a document really was produced, mark it on the
    /// graduation list so the Registry can see at a glance who still needs collecting.
    /// Never throws: a print that succeeded must not be reported as failed because the
    /// bookkeeping after it did not land.
    /// </summary>
    public static void Record(Outcome outcome, string documentType, string fileName, string actor, string role)
    {
        if (outcome == null || outcome.All.Count == 0) return;
        string kind = KindOf(documentType);
        string ip = ClientIp();
        int printed = outcome.Allowed.Count;

        try
        {
            using (var c = new MySqlConnection(ConnStr))
            {
                c.Open();
                using (var tx = c.BeginTransaction())
                {
                    try
                    {
                        foreach (Verdict v in outcome.All)
                        {
                            using (var cmd = new MySqlCommand(
                                "INSERT INTO acad_document_prints " +
                                "(regno, doc_kind, doc_template, acadyear, on_grad_list, is_candidate, " +
                                " outcome, block_reason, batch_id, batch_size, file_name, printed_by, " +
                                " printed_role, ip_address, printed_at) VALUES " +
                                "(@r,@k,@t,@y,@ol,@ic,@o,@br,@b,@bs,@f,@by,@role,@ip,NOW())", c, tx))
                            {
                                cmd.Parameters.AddWithValue("@r", v.RegNo);
                                cmd.Parameters.AddWithValue("@k", kind);
                                cmd.Parameters.AddWithValue("@t", (documentType ?? "").Trim());
                                cmd.Parameters.AddWithValue("@y", v.AcadYear ?? "");
                                cmd.Parameters.AddWithValue("@ol", v.OnList ? 1 : 0);
                                cmd.Parameters.AddWithValue("@ic", v.IsCandidate ? 1 : 0);
                                cmd.Parameters.AddWithValue("@o", v.Allowed ? "PRINTED" : "BLOCKED");
                                cmd.Parameters.AddWithValue("@br",
                                    v.Allowed ? (object)DBNull.Value : Trim(v.Reason, 255));
                                cmd.Parameters.AddWithValue("@b", outcome.BatchId);
                                cmd.Parameters.AddWithValue("@bs", printed < 1 ? 1 : printed);
                                cmd.Parameters.AddWithValue("@f",
                                    v.Allowed ? (object)Trim(fileName, 190) : DBNull.Value);
                                cmd.Parameters.AddWithValue("@by", Trim(actor, 100) ?? "");
                                cmd.Parameters.AddWithValue("@role", Trim(role, 60));
                                cmd.Parameters.AddWithValue("@ip", ip);
                                cmd.ExecuteNonQuery();
                            }

                            // Only a student who is actually on the list has somewhere to record
                            // this, and only a document that was actually produced is a print.
                            if (v.Allowed && v.OnList) MarkOnList(c, tx, v.RegNo, kind, actor);
                        }
                        tx.Commit();
                    }
                    catch { try { tx.Rollback(); } catch { } throw; }
                }
            }
        }
        catch { /* bookkeeping must never fail the document */ }
    }

    /// <summary>
    /// The graduation list carries the state of each document. Printed is only ever moved
    /// forward from Ready: a transcript already collected must not fall back to merely
    /// printed because somebody ran a second copy.
    /// </summary>
    private static void MarkOnList(MySqlConnection c, MySqlTransaction tx, string regno, string kind, string actor)
    {
        string sql = kind == CERTIFICATE
            ? "UPDATE acad_graduands SET cert_status='Printed', cert_printer=@a, cert_date=NOW() " +
              "WHERE regno=@r AND IFNULL(cert_status,'') <> 'Picked'"
            : "UPDATE acad_graduands SET trans_status='Printed', trans_printer=@a, trans_date=NOW() " +
              "WHERE regno=@r AND IFNULL(trans_status,'') <> 'Picked'";
        using (var cmd = new MySqlCommand(sql, c, tx))
        {
            cmd.Parameters.AddWithValue("@r", regno);
            cmd.Parameters.AddWithValue("@a", Trim(actor, 45) ?? "");
            cmd.ExecuteNonQuery();
        }
    }

    private static object Trim(string s, int n)
    {
        if (s == null) return DBNull.Value;
        s = s.Trim();
        if (s == "") return DBNull.Value;
        return s.Length > n ? s.Substring(0, n) : s;
    }

    private static string ClientIp()
    {
        try
        {
            HttpContext h = HttpContext.Current;
            if (h == null || h.Request == null) return null;
            string fwd = h.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            if (!string.IsNullOrEmpty(fwd)) return fwd.Split(',')[0].Trim();
            return h.Request.UserHostAddress;
        }
        catch { return null; }
    }

    /// <summary>Plain-text explanation of a refusal, for a page that has to render one.</summary>
    public static string BlockedSummary(Outcome o)
    {
        if (o == null || !o.AnyBlocked) return "";
        var sb = new System.Text.StringBuilder();
        sb.Append(o.Blocked.Count == 1
            ? "This student cannot be issued a graduation document yet."
            : o.Blocked.Count + " of the students selected cannot be issued a graduation document yet.");
        foreach (Verdict v in o.Blocked)
        {
            sb.Append("\n\n");
            sb.Append(v.Name == "" ? v.RegNo : v.Name + " (" + v.RegNo + ")");
            sb.Append(": ");
            sb.Append(v.Reason);
        }
        return sb.ToString();
    }
}
