using System;
using System.Collections.Generic;
using System.Web;                 // HttpContext, for the page-method auth guard
using System.Web.Configuration;
using System.Web.Services;
using System.Web.UI;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

// ODEL monitoring dashboard (eadmin). Institution KPIs + lecturer compliance
// + student submission rates. Reads odel_* from campus_dynamics_portal.
public partial class COOPERP_NewScreens_OdelDashboard : Page
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    protected void Page_Load(object sender, EventArgs e) { }

    // campus_dynamics_portalConnectionString -> campus_dynamics_portal (odel_* + acad_course_registration)
    private static string ConnStr() { return WebConfigurationManager.ConnectionStrings["campus_dynamics_portalConnectionString"].ConnectionString; }
    private static string S(MySqlDataReader r, int i) { return r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i)); }
    private static long L(MySqlDataReader r, int i) { return r.IsDBNull(i) ? 0L : Convert.ToInt64(r.GetValue(i)); }

    /// <summary>
    /// True when this request carries a signed-in eadmin session.
    ///
    /// Page methods bypass the redirect that protects the page itself, so every
    /// [WebMethod] below has to ask for itself. Accepts either signal the eadmin
    /// screens establish -- a forms-authentication identity or the Session["username"]
    /// the older pages set -- because different entry points set one or the other.
    /// </summary>
    private static bool Authed()
    {
        try
        {
            HttpContext c = HttpContext.Current;
            if (c == null) return false;
            if (c.User != null && c.User.Identity != null && c.User.Identity.IsAuthenticated
                && !string.IsNullOrEmpty(c.User.Identity.Name)) return true;
            if (c.Session != null)
            {
                object u = c.Session["username"];
                if (u != null && !string.IsNullOrEmpty(u.ToString().Trim())) return true;
            }
        }
        catch { }
        return false;
    }

    private static string NotSignedIn()
    {
        // 200 with success:false, not 401: a 401 is rewritten into the HTML login page
        // and the caller reports a failed request instead of an expired session.
        return Json.Serialize(new { success = false, message = "Your session has expired. Please sign in again, then retry." });
    }

    [WebMethod(EnableSession = true)]
    public static string GetDashboard()
    {
        if (!Authed()) return NotSignedIn();
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            using (MySqlConnection conn = new MySqlConnection(ConnStr()))
            {
                conn.Open();
                // KPIs
                long spaces = Scalar(conn, "SELECT COUNT(*) FROM odel_course_space WHERE status IN ('ACTIVE','FROZEN')");
                long assignments = Scalar(conn, "SELECT COUNT(*) FROM odel_assignment WHERE is_published=1");
                long materials = Scalar(conn, "SELECT COUNT(*) FROM odel_material WHERE is_published=1");
                long subsTotal = Scalar(conn, "SELECT COUNT(*) FROM odel_submission WHERE status='SUBMITTED'");
                long subsWeek = Scalar(conn, "SELECT COUNT(*) FROM odel_submission WHERE status='SUBMITTED' AND submitted_at>=DATE_SUB(NOW(),INTERVAL 7 DAY)");
                long ungraded = Scalar(conn, "SELECT COUNT(*) FROM odel_submission su WHERE su.status='SUBMITTED' AND NOT EXISTS (SELECT 1 FROM odel_submission_grade g WHERE g.submission_id=su.id AND g.is_current=1)");
                long pushes = Scalar(conn, "SELECT COUNT(*) FROM odel_cw_push");
                long students = Scalar(conn, "SELECT COUNT(DISTINCT regno) FROM odel_submission WHERE status='SUBMITTED'");

                // submissions trend (last 8 weeks)
                List<object> trend = new List<object>();
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT DATE_FORMAT(submitted_at,'%x-W%v') wk, COUNT(*) n FROM odel_submission " +
                    "WHERE status='SUBMITTED' AND submitted_at>=DATE_SUB(NOW(),INTERVAL 8 WEEK) GROUP BY wk ORDER BY wk", conn))
                using (MySqlDataReader r = cmd.ExecuteReader())
                    while (r.Read()) trend.Add(new { label = S(r, 0), count = L(r, 1) });

                // lecturer compliance: per space, assignments vs a nominal min (2), roster, ungraded
                List<object> compliance = new List<object>();
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT sp.id, sp.courseID, sp.acad_year, sp.semester, sp.owner_empid, " +
                    " (SELECT COUNT(*) FROM odel_assignment a WHERE a.space_id=sp.id AND a.is_published=1) asg, " +
                    " (SELECT COUNT(*) FROM acad_course_registration cr WHERE TRIM(cr.courseID)=TRIM(sp.courseID) AND cr.acad_year=sp.acad_year AND cr.semester=sp.semester AND cr.lecturer_status='APPROVED') roster, " +
                    " (SELECT COUNT(*) FROM odel_submission su JOIN odel_assignment a ON a.id=su.assignment_id WHERE a.space_id=sp.id AND su.status='SUBMITTED' AND NOT EXISTS (SELECT 1 FROM odel_submission_grade g WHERE g.submission_id=su.id AND g.is_current=1)) ungr, " +
                    " (SELECT IFNULL(MAX(version),0) FROM odel_cw_push p WHERE p.space_id=sp.id) pushed " +
                    "FROM odel_course_space sp WHERE sp.status IN ('ACTIVE','FROZEN') ORDER BY asg ASC, sp.courseID LIMIT 200", conn))
                using (MySqlDataReader r = cmd.ExecuteReader())
                    while (r.Read())
                        compliance.Add(new Dictionary<string, object> {
                            {"space",L(r,0)},{"courseID",S(r,1)},{"term",S(r,2)+" S"+L(r,3)},{"owner",L(r,4)},
                            {"assignments",L(r,5)},{"roster",L(r,6)},{"ungraded",L(r,7)},{"pushed",L(r,8)}
                        });

                // coursework entered via ODEL (rows whose mark_stage_changed_by starts 'ODEL:')
                long cwViaOdel = Scalar(conn, "SELECT COUNT(*) FROM acad_course_registration WHERE mark_stage_changed_by LIKE 'ODEL:%'");

                return Json.Serialize(new
                {
                    success = true,
                    roleNote = scope.RoleNote, scopeLabel = scope.Label,
                    kpi = new { spaces = spaces, assignments = assignments, materials = materials, subsTotal = subsTotal, subsWeek = subsWeek, ungraded = ungraded, pushes = pushes, students = students, cwViaOdel = cwViaOdel },
                    trend = trend, compliance = compliance
                });
            }
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    private static long Scalar(MySqlConnection conn, string sql)
    { using (MySqlCommand cmd = new MySqlCommand(sql, conn)) { object o = cmd.ExecuteScalar(); return o == null || o == DBNull.Value ? 0 : Convert.ToInt64(o); } }
}
