using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Configuration;
using System.Web.Services;
using System.Web.UI;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

// ODEL Policy Centre (eadmin). Versioned institution-level policies (+ optional
// per-course-term overrides). Every change supersedes the prior active value.
public partial class COOPERP_NewScreens_OdelPolicy : Page
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    protected void Page_Load(object sender, EventArgs e) { }
    private static string ConnStr() { return WebConfigurationManager.ConnectionStrings["campus_dynamics_portalConnectionString"].ConnectionString; }
    private static string User() { HttpContext c = HttpContext.Current; return c != null && c.Session != null ? (c.Session["username"] as string ?? "") : ""; }

    // Known policy keys with labels + defaults (self-heal missing rows).
    private static readonly string[][] KEYS = new string[][] {
        new string[]{"cw_share","Coursework share ODEL fills (of 40)","40"},
        new string[]{"cw_mode","Push mode (FULL/PARTIAL/ADVISORY)","PARTIAL"},
        new string[]{"min_assignments_per_course","Min assignments per lecturer per course","2"},
        new string[]{"min_submissions_per_student","Min submissions per student (0=all required)","0"},
        new string[]{"best_n_of_m","Best N of M assignments count (0=all)","0"},
        new string[]{"late_window_hours","Default late window (hours)","72"},
        new string[]{"late_penalty_pct","Late penalty (% per day)","10"},
        new string[]{"max_file_mb","Max upload size (MB)","20"},
        new string[]{"max_files","Max files per submission","5"},
        new string[]{"autosave_seconds","Draft autosave interval (seconds)","30"}
    };

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
    public static string GetPolicies()
    {
        if (!Authed()) return NotSignedIn();
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr()))
            {
                conn.Open();
                List<object> rows = new List<object>();
                foreach (string[] k in KEYS)
                {
                    string val = k[2];
                    using (MySqlCommand cmd = new MySqlCommand("SELECT pvalue FROM odel_policy_value WHERE policy_key=@k AND scope_level='INSTITUTION' AND active=1 ORDER BY id DESC LIMIT 1", conn))
                    { cmd.Parameters.AddWithValue("@k", k[0]); object o = cmd.ExecuteScalar(); if (o != null && o != DBNull.Value) val = Convert.ToString(o); }
                    rows.Add(new Dictionary<string, object> { { "key", k[0] }, { "label", k[1] }, { "value", val }, { "default", k[2] } });
                }
                // recent overrides
                List<object> overrides = new List<object>();
                using (MySqlCommand cmd = new MySqlCommand("SELECT policy_key,scope_ref,pvalue,set_by,set_at FROM odel_policy_value WHERE scope_level='COURSE_TERM' AND active=1 ORDER BY id DESC LIMIT 50", conn))
                using (MySqlDataReader r = cmd.ExecuteReader())
                    while (r.Read()) overrides.Add(new Dictionary<string, object> { { "key", RS(r, 0) }, { "scope", RS(r, 1) }, { "value", RS(r, 2) }, { "by", RS(r, 3) }, { "at", RS(r, 4) } });
                return Json.Serialize(new { success = true, policies = rows, overrides = overrides });
            }
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    [WebMethod(EnableSession = true)]
    public static string SavePolicy(string key, string value, string scopeLevel, string scopeRef)
    {
        if (!Authed()) return NotSignedIn();
        try
        {
            if (string.IsNullOrEmpty(key) || value == null) return Json.Serialize(new { success = false, message = "Key and value required." });
            string lvl = scopeLevel == "COURSE_TERM" ? "COURSE_TERM" : "INSTITUTION";
            string sref = lvl == "COURSE_TERM" ? (scopeRef ?? "") : "";
            using (MySqlConnection conn = new MySqlConnection(ConnStr()))
            {
                conn.Open();
                using (MySqlTransaction tx = conn.BeginTransaction())
                {
                    using (MySqlCommand cmd = new MySqlCommand("UPDATE odel_policy_value SET active=0 WHERE policy_key=@k AND scope_level=@l AND scope_ref=@r AND active=1", conn, tx))
                    { cmd.Parameters.AddWithValue("@k", key); cmd.Parameters.AddWithValue("@l", lvl); cmd.Parameters.AddWithValue("@r", sref); cmd.ExecuteNonQuery(); }
                    using (MySqlCommand cmd = new MySqlCommand("INSERT INTO odel_policy_value (policy_key,scope_level,scope_ref,pvalue,active,set_by,set_at) VALUES (@k,@l,@r,@v,1,@by,NOW())", conn, tx))
                    { cmd.Parameters.AddWithValue("@k", key); cmd.Parameters.AddWithValue("@l", lvl); cmd.Parameters.AddWithValue("@r", sref); cmd.Parameters.AddWithValue("@v", value.Trim()); cmd.Parameters.AddWithValue("@by", User()); cmd.ExecuteNonQuery(); }
                    tx.Commit();
                }
                return Json.Serialize(new { success = true });
            }
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    private static string RS(MySqlDataReader r, int i) { return r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i)); }
}
