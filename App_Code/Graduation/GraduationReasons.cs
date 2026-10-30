using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

// =====================================================================
//  Graduation Centre, what to suggest when someone holds a candidate.
//
//  A static list of reasons would be a dropdown nobody reads. These are
//  chosen for the candidate in front of the reviewer: the engine has
//  just decided why this person is not ready, so the reasons offered are
//  the ones that follow from those findings, most specific first.
//
//  Whatever wording has actually been used on this database is offered
//  alongside them, most-used first, so the phrasing a Registrar settles
//  on spreads instead of being retyped slightly differently every time.
//  Today that is one row; the list improves on its own as the module is
//  used.
// =====================================================================
public static class GraduationReasons
{
    private static readonly JavaScriptSerializer J = new JavaScriptSerializer();

    private static string Conn()
    { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }

    /// <summary>How many suggestions the dialog shows. Six is what fits without scrolling.</summary>
    private const int SHOW = 6;

    private class Sug
    {
        public string text;
        public string why;      // the one-line justification shown under it
        public int rank;        // lower sorts first
        public Sug(string t, string w, int r) { text = t; why = w; rank = r; }
    }

    /// <summary>
    /// Suggestions for one candidate, in the order they should be offered.
    ///
    /// Rank 0-9 are driven by this candidate's own findings and are the ones a reviewer will
    /// usually want. Rank 20+ are the standing graduation-office reasons that apply to anyone.
    /// Wording already used on this database sits between them at rank 10+, because a phrase a
    /// colleague has used before is better than one this file invented, but not better than one
    /// that names the actual blocker.
    /// </summary>
    public static string For(string regno)
    {
        MarksScope scope = null;
        if (!string.IsNullOrEmpty(regno))
        {
            try { scope = MarksScopeResolver.Resolve(); } catch { scope = null; }
        }
        return For(scope, regno);
    }

    /// <summary>The same, under a scope already resolved (null offers only the standing reasons).</summary>
    public static string For(MarksScope scope, string regno)
    {
        var outp = new List<Sug>();
        try
        {
            GradCandidate g = null;
            if (!string.IsNullOrEmpty(regno) && scope != null)
            {
                // Load() assesses as it reads, and enforces scope, so a reviewer cannot pull
                // suggestions for a student they are not allowed to see.
                try
                {
                    if (scope.HasAccess)
                        using (var c = new MySqlConnection(Conn()))
                        {
                            c.Open();
                            g = GraduationService.Load(c, scope, regno.Trim());
                            if (g != null && !scope.AllowsProg(g.progcode)) g = null;
                        }
                }
                catch { g = null; }
            }

            if (g != null)
            {
                // Driven by the findings themselves, so a suggestion can only name a problem the
                // checks actually found, in the checks' own numbers.
                var lvl = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var detail = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (GradFinding f in g.findings)
                {
                    if (f.level != "BLOCK" && f.level != "WARN" && f.level != "NA") continue;
                    string key = f.code + "|" + f.name;
                    if (!lvl.ContainsKey(key)) { lvl[key] = f.level; detail[key] = f.detail; }
                }
                int fails = Math.Max(0, g.failedPapers - g.duplicateFails);

                if (g.priorYear != "" && g.graduatedYear == "")
                    outp.Add(new Sug(
                        "Already graduated from " + g.priorProg + " in " + g.priorYear + "; the earlier record is to be confirmed.",
                        "on the graduation records kept from before 25 Sep 2026", 0));

                if (g.pendingRetakes > 0)
                    outp.Add(new Sug(
                        "Waiting on the result of " + g.pendingRetakes +
                        (g.pendingRetakes == 1 ? " registered retake." : " registered retakes."),
                        "retake registered, not yet marked", 0));
                else if (fails > 0)
                    outp.Add(new Sug(
                        "Waiting on the retake result for " + fails + (fails == 1 ? " failed paper." : " failed papers."),
                        fails + " paper(s) marked between 1 and 49", 0));

                foreach (KeyValuePair<string, string> kv in lvl)
                {
                    if (kv.Key.StartsWith("C10|", StringComparison.Ordinal))
                        outp.Add(new Sug(
                            "Missing a final-year component: " + Missing(detail[kv.Key]) + ".",
                            "every recent graduate of the programme has one", 1));
                    else if (kv.Key.StartsWith("C6|", StringComparison.Ordinal) && kv.Value == "BLOCK")
                        outp.Add(new Sug(
                            detail[kv.Key].StartsWith("Has not sat", StringComparison.Ordinal)
                                ? "Has not sat the final semester."
                                : "Missing a full semester of results; the department to confirm.",
                            "from the semester-by-semester check", 1));
                    else if (kv.Key.StartsWith("C2|Course recorded twice", StringComparison.Ordinal))
                        outp.Add(new Sug(
                            "A course is recorded twice and must be merged before approval.",
                            "same course under two spellings of its code", 3));
                }

                if (g.UnpubTotal > 0)
                    outp.Add(new Sug(
                        "Marks are still in the pipeline and not yet published (" +
                        g.UnpubTotal + " outstanding).",
                        "sitting below PUBLISHED in the marks workflow", 1));

                if (g.cuSource != "NONE" && g.cuShort > 0)
                    outp.Add(new Sug(
                        "Credit shortfall to be verified: " + Math.Round(g.cuCounted) + " of " +
                        Math.Round(g.cuRequired) + " credits earned.",
                        "short by " + Math.Round(g.cuShort) + " credits", 2));

                if (g.zeroMarks + Math.Max(0, g.missingScores - g.pendingRetakes) > 0)
                    outp.Add(new Sug(
                        "Papers recorded as 0 or unmarked need confirming with the department.",
                        (g.zeroMarks + Math.Max(0, g.missingScores - g.pendingRetakes)) + " paper(s) with no real mark", 3));
            }

            // What this institution has actually written before.
            try
            {
                using (var c = new MySqlConnection(Conn()))
                {
                    c.Open();
                    using (var cmd = new MySqlCommand(
                        "SELECT reason, COUNT(*) n FROM acad_grad_review " +
                        "WHERE verdict='HELD' AND TRIM(IFNULL(reason,''))<>'' " +
                        "GROUP BY reason ORDER BY n DESC, MAX(id) DESC LIMIT 8", c))
                    using (var r = cmd.ExecuteReader())
                    {
                        int i = 0;
                        while (r.Read())
                        {
                            string txt = r[0].ToString().Trim();
                            int n = Convert.ToInt32(r[1]);
                            if (txt.Length < 10) continue;
                            // "she graduated" is the commonest hold reason on record, written
                            // before C1 could see earlier graduations. Offered to everyone it is
                            // a false statement about most of them; C1 now says it when it is true.
                            string low = txt.ToLowerInvariant().TrimEnd('.');
                            if ((low == "she graduated" || low == "he graduated") && (g == null || g.priorYear == "")) continue;
                            outp.Add(new Sug(txt,
                                "used " + n + (n == 1 ? " time before" : " times before"), 10 + i));
                            i++;
                        }
                    }
                }
            }
            catch { /* suggestions are a convenience; never fail the dialog over them */ }

            // The standing reasons, which have nothing to do with marks.
            outp.Add(new Sug("Financial clearance is outstanding.", "fees or other obligations", 20));
            outp.Add(new Sug("Academic documents are not yet verified.",
                             "entry qualifications or identity documents", 21));
            outp.Add(new Sug("A disciplinary matter is unresolved.", "referred by the Dean of Students", 22));
            outp.Add(new Sug("Name on record does not match the supporting documents.",
                             "must be corrected before a certificate is printed", 23));

            outp.Sort(delegate(Sug a, Sug b) { return a.rank.CompareTo(b.rank); });

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<object>();
            foreach (Sug sg in outp)
            {
                if (list.Count >= SHOW) break;
                if (!seen.Add(sg.text.Trim())) continue;
                list.Add(new { text = sg.text, why = sg.why });
            }

            return J.Serialize(new { success = true, suggestions = list });
        }
        catch (Exception ex)
        {
            return J.Serialize(new { success = false, message = ex.Message });
        }
    }

    /// <summary>"a research project or dissertation" out of C10's sentence, for a short reason.</summary>
    private static string Missing(string c10Detail)
    {
        const string head = "No pass on record for ";
        int i = c10Detail.IndexOf(head, StringComparison.Ordinal);
        if (i < 0) return "research, school practice or internship";
        string rest = c10Detail.Substring(i + head.Length);
        int dot = rest.IndexOf('.');
        return dot > 0 ? rest.Substring(0, dot) : rest;
    }

    /// <summary>
    /// Suggestions for a batch. There is no single candidate to read, so only the standing
    /// reasons and the wording already in use are offered, inventing a per-student reason for
    /// forty students at once would be a lie on thirty-nine of them.
    /// </summary>
    public static string ForBatch()
    {
        return For("");
    }
}
