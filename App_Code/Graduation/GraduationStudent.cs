using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

// =====================================================================
//  Graduation Centre — the evidence behind one student.
//
//  Three of the four pages open a student, and they must all show the
//  same thing: a Registrar should not have to learn two layouts for the
//  same question. So the payload is built once, here, and each page's
//  GetStudent is a single line.
//
//  Always read live. A graduation decision is not taken on the numbers a
//  list rendered some minutes ago.
// =====================================================================
public static class GraduationStudent
{
    private static readonly JavaScriptSerializer J = new JavaScriptSerializer();

    private static string Conn()
    { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }

    public static string Detail(string regno)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!scope.HasAccess) return GraduationBootstrap.Denied();

            regno = (regno ?? "").Trim();
            if (regno == "") return J.Serialize(new { success = false, message = "No student was given." });

            var results = new List<object>();
            var structure = new List<object>();
            var history = new List<object>();
            GradCandidate g;

            using (var c = new MySqlConnection(Conn()))
            {
                c.Open();
                g = GraduationService.Load(c, scope, regno);
                if (g == null)
                    return J.Serialize(new { success = false, message = "No student record for " + regno + "." });
                if (!scope.AllowsProg(g.progcode))
                    return J.Serialize(new { success = false, message = "That student is outside the programmes you can see." });

                using (var cmd = new MySqlCommand(
                    "SELECT r.acad, r.studyyear, r.semester, r.courseid, IFNULL(c2.courseName,''), " +
                    " IFNULL(r.CreditUnits,0), r.score, IFNULL(r.grade,''), IFNULL(r.gradept,0) " +
                    "FROM acad_results r LEFT JOIN acad_course c2 ON c2.courseID=r.courseid " +
                    "WHERE r.regno=@r ORDER BY r.acad DESC, r.studyyear DESC, r.semester, r.courseid", c))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            results.Add(new
                            {
                                acad = r[0].ToString(),
                                sy = r[1].ToString(),
                                sem = r[2].ToString(),
                                code = r[3].ToString(),
                                name = r[4].ToString(),
                                cu = Convert.ToDouble(r[5]),
                                score = r.IsDBNull(6) ? (object)null : Convert.ToInt32(r[6]),
                                grade = r[7].ToString(),
                                gp = Convert.ToDouble(r[8])
                            });
                }

                // Only shown when a real specialisation resolves. Otherwise it would be a list
                // of courses from somebody else's curriculum, which is worse than none.
                if (!g.specIsPlaceholder)
                {
                    using (var cmd = new MySqlCommand(
                        "SELECT pc.study_year, pc.semester, pc.course_code, IFNULL(ac.courseName,''), " +
                        " IFNULL(ac.CreditUnit,0), pc.course_type, " +
                        " (SELECT r.score FROM acad_results r WHERE r.regno=@r AND r.courseid=pc.course_code LIMIT 1) " +
                        "FROM acad_programmecourses pc LEFT JOIN acad_course ac ON ac.courseID=pc.course_code " +
                        "WHERE pc.progcode=@p AND IFNULL(pc.specialisation_id,0)=@sp AND pc.status='Active' " +
                        "ORDER BY pc.study_year, pc.semester, pc.course_code", c))
                    {
                        cmd.Parameters.AddWithValue("@r", regno);
                        cmd.Parameters.AddWithValue("@p", g.progcode);
                        cmd.Parameters.AddWithValue("@sp", g.specialisation);
                        using (var r = cmd.ExecuteReader())
                            while (r.Read())
                                structure.Add(new
                                {
                                    sy = r[0].ToString(),
                                    sem = r[1].ToString(),
                                    code = r[2].ToString(),
                                    name = r[3].ToString(),
                                    cu = Convert.ToDouble(r[4]),
                                    type = r[5].ToString(),
                                    score = r.IsDBNull(6) ? (object)null : Convert.ToInt32(r[6])
                                });
                    }
                }

                using (var cmd = new MySqlCommand(
                    "SELECT verdict, IFNULL(reason,''), actor, IFNULL(actor_role,''), " +
                    " DATE_FORMAT(created_at,'%e %b %Y, %H:%i'), acadyear, IF(superseded_at IS NULL,1,0) " +
                    "FROM acad_grad_review WHERE regno=@r ORDER BY id DESC LIMIT 40", c))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            history.Add(new
                            {
                                verdict = r[0].ToString(),
                                reason = r[1].ToString(),
                                actor = r[2].ToString(),
                                role = r[3].ToString(),
                                at = r[4].ToString(),
                                year = r[5].ToString(),
                                inForce = Convert.ToInt32(r[6]) == 1
                            });
                }
            }

            return J.Serialize(new
            {
                success = true,
                student = g,
                results = results,
                structure = structure,
                history = history
            });
        }
        catch (Exception ex) { return J.Serialize(new { success = false, message = ex.Message }); }
    }
}
