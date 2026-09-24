using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

// =====================================================================
//  Graduation Centre — the write path.
//  Plan: COOPERP/NewScreens/GRADUATION_CENTRE_PLAN.md §6
//
//  Two tables move together or neither moves:
//    acad_graduands    — the system of record. Nineteen stored procedures
//                        and ten code files read it, including every
//                        transcript procedure. A student is "on the
//                        graduation list" iff they have a row here.
//    acad_grad_review  — the decision. Who, when, why, and the evidence
//                        as it stood at the time.
//
//  A graduand with no verdict, or a verdict with no graduand, is exactly
//  the inconsistency this module exists to prevent, so every write is a
//  transaction over both.
// =====================================================================
public static class GraduationService
{
    private static readonly JavaScriptSerializer J = new JavaScriptSerializer();

    private static string Conn()
    { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }

    private static string Actor()
    {
        try
        {
            HttpContext x = HttpContext.Current;
            if (x != null && x.Session != null && x.Session["username"] != null)
            {
                string u = x.Session["username"].ToString().Trim();
                if (u != "") return u.Length > 100 ? u.Substring(0, 100) : u;
            }
            if (x != null && x.User != null && x.User.Identity != null && x.User.Identity.IsAuthenticated)
                return x.User.Identity.Name;
        }
        catch { }
        return "system";
    }

    private static string Fail(string m) { return J.Serialize(new { success = false, message = m }); }
    private static string Ok(string m) { return J.Serialize(new { success = true, message = m }); }

    /// <summary>
    /// Loads one candidate and assesses them against live data.
    ///
    /// Always live, never from the list page's cached numbers: a decision as consequential as a
    /// graduation must be taken on what is true now, and the snapshot written alongside it has
    /// to be honest about what the reviewer was actually shown.
    /// </summary>
    public static GradCandidate Load(MySqlConnection c, MarksScope scope, string regno)
    {
        GradCandidate g = null;
        using (var cmd = new MySqlCommand(
            "SELECT s.regno, TRIM(CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,''))) nm, " +
            " TRIM(s.progid) progid, IFNULL(p.progname,'') progname, IFNULL(p.faculty_code,'') fac, " +
            " IFNULL(p.department_id,0) dep, IFNULL(s.entryyear,'') ey, IFNULL(s.specialisation,'') sp, " +
            " IFNULL(p.levelCode,3) lvl, IFNULL(NULLIF(p.couselength,0),3) plen, " +
            " IFNULL(s.nationality,'') nat, IFNULL(s.gender,'') gen " +
            "FROM acad_student s LEFT JOIN acad_programme p ON p.progcode=s.progid " +
            "WHERE s.regno=@r LIMIT 1", c))
        {
            cmd.Parameters.AddWithValue("@r", regno);
            using (var r = cmd.ExecuteReader())
                if (r.Read())
                {
                    g = new GradCandidate();
                    g.regno = r[0].ToString(); g.name = r[1].ToString(); g.progcode = r[2].ToString();
                    g.progname = r[3].ToString(); g.faculty = r[4].ToString(); g.department = r[5].ToString();
                    g.entryyear = r[6].ToString(); g.specialisation = r[7].ToString();
                    g.levelCode = Convert.ToInt32(r[8]); g.progLength = Convert.ToInt32(r[9]);
                    g.nationality = r[10].ToString(); g.gender = r[11].ToString();
                    g.specIsPlaceholder = (g.specialisation == "" || g.specialisation == "0" || g.specialisation == "13");
                }
        }
        if (g == null) return null;

        using (var cmd = new MySqlCommand(
            "SELECT MAX(r.studyyear), COUNT(*), " +
            " SUM(CASE WHEN r.score>=50 THEN IFNULL(r.CreditUnits,0) ELSE 0 END), " +
            " SUM(r.score>0 AND r.score<50), SUM(r.score=0), SUM(r.score IS NULL), " +
            " SUM(IFNULL(r.CreditUnits,0)*IFNULL(r.gradept,0)), SUM(IFNULL(r.CreditUnits,0)), " +
            " MIN(CASE WHEN r.acad REGEXP '^[0-9]{4}/[0-9]{4}$' THEN r.acad END), " +
            " MAX(CASE WHEN r.acad REGEXP '^[0-9]{4}/[0-9]{4}$' THEN r.acad END) " +
            "FROM acad_results r WHERE r.regno=@r", c))
        {
            cmd.Parameters.AddWithValue("@r", regno);
            using (var r = cmd.ExecuteReader())
                if (r.Read() && !r.IsDBNull(0))
                {
                    g.maxStudyYear = Convert.ToInt32(r[0]); g.coursesTaken = Convert.ToInt32(r[1]);
                    g.cuEarned = r.IsDBNull(2) ? 0 : Convert.ToDouble(r[2]);
                    g.failedPapers = r.IsDBNull(3) ? 0 : Convert.ToInt32(r[3]);
                    g.zeroMarks = r.IsDBNull(4) ? 0 : Convert.ToInt32(r[4]);
                    g.missingScores = r.IsDBNull(5) ? 0 : Convert.ToInt32(r[5]);
                    double num = r.IsDBNull(6) ? 0 : Convert.ToDouble(r[6]);
                    double den = r.IsDBNull(7) ? 0 : Convert.ToDouble(r[7]);
                    g.cgpa = den > 0 ? Math.Round(num / den, 2) : 0;
                    g.firstYear = r.IsDBNull(8) ? "" : r[8].ToString();
                    g.lastYear = r.IsDBNull(9) ? "" : r[9].ToString();
                }
        }

        // C4 and C5 for this one student. The evidence panel must run the SAME checks the list
        // ran, or a reviewer opens a row marked "needs a look" and finds nothing to look at.
        try
        {
            using (var cmd = new MySqlCommand(
                "SELECT COUNT(*) req, " +
                " SUM(NOT EXISTS(SELECT 1 FROM acad_results r WHERE r.regno=s.regno AND r.courseid=pc.course_code)) missing " +
                "FROM acad_student s " +
                "JOIN acad_programmecourses pc ON pc.progcode=s.progid " +
                " AND pc.specialisation_id=CAST(s.specialisation AS UNSIGNED) AND pc.status='Active' " +
                "WHERE s.regno=@r AND TRIM(IFNULL(s.specialisation,'')) NOT IN ('','0','13')", c))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                using (var r = cmd.ExecuteReader())
                    if (r.Read() && !r.IsDBNull(0) && Convert.ToInt32(r[0]) > 0)
                    {
                        g.coverageChecked = true;
                        g.coverageRequired = Convert.ToInt32(r[0]);
                        g.coverageMissing = r.IsDBNull(1) ? 0 : Convert.ToInt32(r[1]);
                    }
            }
        }
        catch { }

        try
        {
            using (var cmd = new MySqlCommand(
                "SELECT SUM(cr.mark_stage='ENTERED'), SUM(cr.mark_stage='CAPTURED'), SUM(cr.mark_stage='APPROVED') " +
                "FROM campus_dynamics_portal.acad_course_registration cr " +
                "WHERE cr.regno=@r AND cr.mark_stage IN ('ENTERED','CAPTURED','APPROVED')", c))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                using (var r = cmd.ExecuteReader())
                    if (r.Read() && !r.IsDBNull(0))
                    {
                        g.unpubEntered = Convert.ToInt32(r[0]);
                        g.unpubCaptured = Convert.ToInt32(r[1]);
                        g.unpubApproved = Convert.ToInt32(r[2]);
                    }
            }
        }
        catch { }

        using (var cmd = new MySqlCommand("SELECT acadyear FROM acad_graduands WHERE regno=@r LIMIT 1", c))
        { cmd.Parameters.AddWithValue("@r", regno); object o = cmd.ExecuteScalar(); if (o != null && o != DBNull.Value) g.graduatedYear = o.ToString(); }

        using (var cmd = new MySqlCommand(
            "SELECT verdict, IFNULL(reason,''), actor, DATE_FORMAT(created_at,'%e %b %Y') " +
            "FROM acad_grad_review WHERE regno=@r AND superseded_at IS NULL ORDER BY id DESC LIMIT 1", c))
        {
            cmd.Parameters.AddWithValue("@r", regno);
            using (var r = cmd.ExecuteReader())
                if (r.Read())
                {
                    if (r[0].ToString() == "HELD")
                    { g.holdReason = r[1].ToString(); g.holdActor = r[2].ToString(); g.holdAt = r[3].ToString(); }
                    else if (r[0].ToString() == "CLEARED")
                    { g.clearedActor = r[2].ToString(); g.clearedAt = r[3].ToString(); }
                }
        }

        GraduationEngine.Assess(c, g);
        return g;
    }

    /// <summary>
    /// Scope is re-checked on the server for every write. A hidden button is not access
    /// control; a Head of Department must not be able to clear a student in another department
    /// by replaying a request.
    /// </summary>
    private static bool InScope(MarksScope scope, string progcode)
    {
        if (scope == null || !scope.HasAccess) return false;
        return scope.AllowsProg((progcode ?? "").Trim());
    }

    private static void Audit(MySqlConnection c, MySqlTransaction tx, string actor, string regno,
                              string name, string prog, string year, string what)
    {
        try
        {
            using (var cmd = new MySqlCommand(
                "INSERT INTO acad_activity_log (user_id, page_function, par, comments, access_date) " +
                "VALUES (@u,'Graduation Centre',@p,@c,NOW())", c, tx))
            {
                cmd.Parameters.AddWithValue("@u", actor);
                cmd.Parameters.AddWithValue("@p", "Academic Year: " + year + " Reg No: " + regno +
                                                  " Student Name: " + name + " Programme: " + prog);
                cmd.Parameters.AddWithValue("@c", what);
                cmd.ExecuteNonQuery();
            }
        }
        catch { /* the audit table must never be the reason a decision fails to record */ }
    }

    private static void Supersede(MySqlConnection c, MySqlTransaction tx, string regno)
    {
        using (var cmd = new MySqlCommand(
            "UPDATE acad_grad_review SET superseded_at=NOW() WHERE regno=@r AND superseded_at IS NULL", c, tx))
        { cmd.Parameters.AddWithValue("@r", regno); cmd.ExecuteNonQuery(); }
    }

    private static void WriteVerdict(MySqlConnection c, MySqlTransaction tx, GradCandidate g,
                                     string year, string verdict, string reason, string actor, string role)
    {
        using (var cmd = new MySqlCommand(
            "INSERT INTO acad_grad_review (regno,acadyear,progcode,verdict,reason,snapshot_json," +
            " cgpa,degclass,cu_earned,cu_required,cu_source,actor,actor_role,created_at) " +
            "VALUES (@r,@y,@p,@v,@rs,@snap,@cg,@dc,@ce,@cr,@cs,@a,@ar,NOW())", c, tx))
        {
            cmd.Parameters.AddWithValue("@r", g.regno);
            cmd.Parameters.AddWithValue("@y", year);
            cmd.Parameters.AddWithValue("@p", g.progcode);
            cmd.Parameters.AddWithValue("@v", verdict);
            cmd.Parameters.AddWithValue("@rs", (object)(reason == "" ? null : reason) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@snap", J.Serialize(new
            {
                readiness = g.readiness,
                findings = g.findings,
                cuEarned = g.cuEarned,
                cuRequired = g.cuRequired,
                cuSource = g.cuSource,
                cgpa = g.cgpa,
                degClass = g.degClass,
                failedPapers = g.failedPapers,
                zeroMarks = g.zeroMarks,
                missingScores = g.missingScores,
                maxStudyYear = g.maxStudyYear,
                progLength = g.progLength,
                coursesTaken = g.coursesTaken
            }));
            cmd.Parameters.AddWithValue("@cg", g.cgpa);
            cmd.Parameters.AddWithValue("@dc", g.degClass);
            cmd.Parameters.AddWithValue("@ce", g.cuEarned);
            cmd.Parameters.AddWithValue("@cr", g.cuRequired);
            cmd.Parameters.AddWithValue("@cs", g.cuSource);
            cmd.Parameters.AddWithValue("@a", actor);
            cmd.Parameters.AddWithValue("@ar", (object)role ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Puts a candidate on the graduation list.
    ///
    /// Idempotent: a student already on a list is reported, not duplicated — acad_graduands has
    /// no unique key on regno (it already carries one duplicate from before this module), so the
    /// guard has to be here.
    ///
    /// Clearing a candidate the engine BLOCKS is allowed, because a Registrar may know something
    /// the data does not — but only with a written justification, which is stored on the verdict
    /// and shown on the graduation list beside the name. Silently clearing a student with five
    /// failed papers is the exact failure this module was built to stop.
    /// </summary>
    public static string Clear(MarksScope scope, string regno, string acadYear, string note, bool overrideBlock)
    {
        regno = (regno ?? "").Trim();
        acadYear = (acadYear ?? "").Trim();
        note = (note ?? "").Trim();
        if (regno == "") return Fail("No student was given.");
        if (acadYear == "") return Fail("Choose the graduation year first.");

        string actor = Actor();
        try
        {
            using (var c = new MySqlConnection(Conn()))
            {
                c.Open();
                GradCandidate g = Load(c, scope, regno);
                if (g == null) return Fail("No student record for " + regno + ".");
                if (!InScope(scope, g.progcode))
                    return Fail(g.progcode + " is outside the programmes you can act on.");
                if (g.graduatedYear != "")
                    return Fail(regno + " is already on the " + g.graduatedYear + " graduation list.");

                if (g.readiness == "BLOCKED" && !overrideBlock)
                {
                    var blockers = new List<string>();
                    foreach (GradFinding f in g.findings) if (f.level == "BLOCK") blockers.Add(f.detail);
                    return J.Serialize(new
                    {
                        success = false,
                        needsOverride = true,
                        message = "This candidate is blocked. " + string.Join(" ", blockers.ToArray()),
                        blockers = blockers
                    });
                }
                if (g.readiness == "BLOCKED" && note.Length < 10)
                    return Fail("Clearing a blocked candidate needs a written justification of at least 10 characters.");

                using (var tx = c.BeginTransaction())
                {
                    try
                    {
                        using (var cmd = new MySqlCommand(
                            "INSERT INTO acad_graduands (regno,acadyear,cgpa,degclass,nationality,stud_name," +
                            "progcode,gender,comp_date) VALUES (@r,@y,@cg,@dc,@nat,@nm,@p,@gen,CURDATE())", c, tx))
                        {
                            cmd.Parameters.AddWithValue("@r", g.regno);
                            cmd.Parameters.AddWithValue("@y", acadYear);
                            cmd.Parameters.AddWithValue("@cg", g.cgpa);
                            cmd.Parameters.AddWithValue("@dc", g.degClass == "" ? "-" : g.degClass);
                            cmd.Parameters.AddWithValue("@nat", g.nationality);
                            cmd.Parameters.AddWithValue("@nm", g.name);
                            cmd.Parameters.AddWithValue("@p", g.progcode);
                            cmd.Parameters.AddWithValue("@gen", g.gender);
                            cmd.ExecuteNonQuery();
                        }
                        Supersede(c, tx, g.regno);
                        WriteVerdict(c, tx, g, acadYear, "CLEARED", note, actor, scope.RoleNote);
                        // The summary the queues read must not still show them as outstanding.
                        GraduationStats.Touch(c, tx, g.regno);
                        Audit(c, tx, actor, g.regno, g.name, g.progcode, acadYear,
                              g.readiness == "BLOCKED"
                                ? "Cleared onto the graduation list OVER a block: " + note
                                : "Cleared onto the graduation list");
                        tx.Commit();
                    }
                    catch { try { tx.Rollback(); } catch { } throw; }
                }
                return Ok(g.name + " is on the " + acadYear + " graduation list" +
                          (g.degClass == "" ? "." : " — " + g.degClass + "."));
            }
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }

    /// <summary>
    /// Stops a candidate until something is investigated. The reason is mandatory and has a
    /// floor of ten characters, because a hold with no usable reason is a student who quietly
    /// never graduates and nobody can say why.
    /// </summary>
    public static string Hold(MarksScope scope, string regno, string acadYear, string reason)
    {
        regno = (regno ?? "").Trim();
        acadYear = (acadYear ?? "").Trim();
        reason = (reason ?? "").Trim();
        if (regno == "") return Fail("No student was given.");
        if (acadYear == "") return Fail("Choose the graduation year first.");
        if (reason.Length < 10)
            return Fail("Say why this candidate is being held — at least 10 characters. " +
                        "Whoever picks this up next has only this sentence to go on.");
        if (reason.Length > 1000) reason = reason.Substring(0, 1000);

        string actor = Actor();
        try
        {
            using (var c = new MySqlConnection(Conn()))
            {
                c.Open();
                GradCandidate g = Load(c, scope, regno);
                if (g == null) return Fail("No student record for " + regno + ".");
                if (!InScope(scope, g.progcode))
                    return Fail(g.progcode + " is outside the programmes you can act on.");
                if (g.graduatedYear != "")
                    return Fail(regno + " is already on the " + g.graduatedYear +
                                " graduation list. Remove them from it before holding them.");

                using (var tx = c.BeginTransaction())
                {
                    try
                    {
                        Supersede(c, tx, g.regno);
                        WriteVerdict(c, tx, g, acadYear, "HELD", reason, actor, scope.RoleNote);
                        Audit(c, tx, actor, g.regno, g.name, g.progcode, acadYear, "Held: " + reason);
                        tx.Commit();
                    }
                    catch { try { tx.Rollback(); } catch { } throw; }
                }
                return Ok(g.name + " is held. They stay off the " + acadYear + " list until released.");
            }
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }

    /// <summary>Lifts a hold and returns the candidate to the queue. The hold stays in history.</summary>
    public static string Release(MarksScope scope, string regno, string acadYear, string note)
    {
        regno = (regno ?? "").Trim();
        note = (note ?? "").Trim();
        if (regno == "") return Fail("No student was given.");
        string actor = Actor();
        try
        {
            using (var c = new MySqlConnection(Conn()))
            {
                c.Open();
                GradCandidate g = Load(c, scope, regno);
                if (g == null) return Fail("No student record for " + regno + ".");
                if (!InScope(scope, g.progcode))
                    return Fail(g.progcode + " is outside the programmes you can act on.");
                if (g.holdReason == "") return Fail(regno + " is not currently held.");

                using (var tx = c.BeginTransaction())
                {
                    try
                    {
                        Supersede(c, tx, g.regno);
                        WriteVerdict(c, tx, g, acadYear == "" ? "-" : acadYear, "RELEASED", note, actor, scope.RoleNote);
                        Audit(c, tx, actor, g.regno, g.name, g.progcode, acadYear, "Hold lifted" + (note == "" ? "" : ": " + note));
                        tx.Commit();
                    }
                    catch { try { tx.Rollback(); } catch { } throw; }
                }
                return Ok("The hold on " + g.name + " has been lifted. They are back in the candidate queue.");
            }
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }

    /// <summary>
    /// Takes a student off the graduation list. The verdict history is NOT deleted — a name
    /// having been on a list, and come off it, is exactly the kind of thing an audit later needs.
    /// </summary>
    public static string RemoveFromList(MarksScope scope, string regno, string reason)
    {
        regno = (regno ?? "").Trim();
        reason = (reason ?? "").Trim();
        if (regno == "") return Fail("No student was given.");
        if (reason.Length < 10)
            return Fail("Say why this name is coming off the graduation list — at least 10 characters.");
        string actor = Actor();
        try
        {
            using (var c = new MySqlConnection(Conn()))
            {
                c.Open();
                GradCandidate g = Load(c, scope, regno);
                if (g == null) return Fail("No student record for " + regno + ".");
                if (!InScope(scope, g.progcode))
                    return Fail(g.progcode + " is outside the programmes you can act on.");
                if (g.graduatedYear == "") return Fail(regno + " is not on any graduation list.");

                string year = g.graduatedYear;
                using (var tx = c.BeginTransaction())
                {
                    try
                    {
                        int n;
                        using (var cmd = new MySqlCommand("DELETE FROM acad_graduands WHERE regno=@r", c, tx))
                        { cmd.Parameters.AddWithValue("@r", g.regno); n = cmd.ExecuteNonQuery(); }
                        if (n == 0) { tx.Rollback(); return Fail("Nothing was removed — the list may have changed."); }

                        Supersede(c, tx, g.regno);
                        g.graduatedYear = "";
                        WriteVerdict(c, tx, g, year, "RELEASED", "Removed from the graduation list: " + reason,
                                     actor, scope.RoleNote);
                        GraduationStats.Touch(c, tx, g.regno);
                        Audit(c, tx, actor, g.regno, g.name, g.progcode, year,
                              "Removed from the graduation list: " + reason);
                        tx.Commit();
                    }
                    catch { try { tx.Rollback(); } catch { } throw; }
                }
                return Ok(g.name + " has been taken off the " + year + " graduation list.");
            }
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }
}
