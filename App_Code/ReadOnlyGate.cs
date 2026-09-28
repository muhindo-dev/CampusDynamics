using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

/// <summary>
/// Makes a read-only role actually read-only.
///
/// WHY THIS EXISTS AT ALL. The RBAC tables carry can_view, can_edit and can_delete, and
/// nothing in the application has ever read the last two: a search of the codebase finds them
/// only in the statement that clones a role. Page access is barely enforced either, since
/// RequireSlug is called by 11 files out of 195 screens, so the sidebar hides links from a
/// user who can still reach every one of them by typing the URL. A role built out of those
/// pieces would look read-only and would not be one.
///
/// So this gate stands in front of every request, in Global.asax, where the session is
/// available and the page has not run yet. Two questions, in order:
///
///   1. May this user open this page at all? Their granted menu slugs resolve to a set of
///      page names; anything else is refused whether or not it appears in their menu.
///   2. Is this operation a read? PageMethods do not run the page lifecycle, so nothing a
///      page does in Page_Load can guard them. They are judged here by name.
///
/// THE SECOND TEST IS AN ALLOWLIST, and that is the important decision. Across the new
/// screens there are 162 distinct PageMethod names. They nearly separate by verb, but not
/// well enough to denylist: CreateAndPreview writes a draft record despite saying Preview,
/// and Admin* covers both AdminGetX and AdminSaveX. A denylist of write verbs would let
/// several real writes through, and would let through every method written after today. An
/// allowlist fails the other way: a new method nobody has classified is refused, which for
/// this role is the right direction to be wrong in.
///
/// Users who are not in a read-only role never touch any of this.
/// </summary>
public static class ReadOnlyGate
{
    private const string SESS_PAGES = "ro_pages";
    private const string SESS_FLAG  = "ro_isreadonly";

    /// <summary>
    /// Words that mean a write wherever they appear in a method name. Checked before the
    /// read prefixes, so CreateAndPreview is refused on "create" rather than accepted on
    /// "preview".
    /// </summary>
    private static readonly string[] WRITES = {
        "save", "delete", "remove", "create", "update", "insert", "publish", "approve",
        "reject", "reverse", "reset", "force", "execute", "repair", "commit", "cancel",
        "assign", "merge", "send", "import", "generate", "apply", "revoke", "grant",
        "advance", "sync", "migrate", "purge", "archive", "restore", "upload", "bulk",
        "review", "release", "withdraw", "void", "post", "settle", "close", "lock",
        "unlock", "enroll", "register", "deregister", "swap", "move", "rename", "add"
    };

    /// <summary>A name beginning with one of these is a read unless a write word says otherwise.</summary>
    private static readonly string[] READS = {
        "get", "load", "list", "browse", "search", "find", "stats", "stat", "count",
        "detail", "init", "record", "progress", "export", "report", "summar", "analy",
        "view", "lookup", "fetch", "filter", "chart", "trend", "history", "audit",
        "preview", "log", "dashboard", "print", "download", "read", "page", "options"
    };

    /// <summary>
    /// Reads whose names say nothing useful. Each one was read in the source before being
    /// put here, which is the only reason it is safe to name-check the rest.
    /// </summary>
    private static readonly string[] ALLOW = {
        "holdreasons",          // returns suggested hold reasons
        "regsearchstudents",    // student picker
        "regsearchcourses",     // course picker
        "stagedriftcount",      // a COUNT
        "analyse", "summarise", "filters"
    };

    /// <summary>Paths every user needs whatever their role, including the way back out.</summary>
    private static readonly string[] ALWAYS = {
        "/default.aspx", "/accessdenied.aspx", "/logout.aspx", "/multilogin.aspx",
        "/forcepasswordchange.aspx", "/webresource.axd", "/scriptresource.axd",
        "/dx.ashx", "/dxx.axd", "/dxxrd.axd", "/studentthumb.ashx"
    };

    private static string Conn
    {
        get
        {
            var cs = ConfigurationManager.ConnectionStrings["vacConnectionString"];
            return cs == null ? "" : cs.ConnectionString;
        }
    }



    // ── the gate ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called once per request. Returns true when the request was refused and the response
    /// already written, so the caller stops.
    /// </summary>
    public static bool Intercept(HttpContext ctx)
    {
        // Session is deliberately NOT required here. A PageMethod request reaches this point
        // with HttpContext.Session still null even when the method declares
        // EnableSession = true, because the session state module decides what to acquire from
        // the handler and the handler has not run yet. Requiring a session therefore skipped
        // the gate on exactly the requests that do the writing. Identity comes from the forms
        // ticket instead, which is established at AuthenticateRequest, long before this.
        if (ctx == null || ctx.Request == null) return false;

        // A PageMethod arrives as Page.aspx/MethodName, and the method name is path info: it
        // leaves GetExtension with nothing to find, so testing the extension of the raw path
        // skipped every PageMethod request, which is to say every request worth gating. Cut
        // the path at the page's own extension first, then test that.
        string raw = (ctx.Request.Url == null ? "" : ctx.Request.Url.AbsolutePath) ?? "";
        string path = raw.ToLowerInvariant();
        if (path == "") return false;

        int cut = -1; string ext = "";
        foreach (string e in new[] { ".aspx", ".ashx", ".asmx" })
        {
            int k = path.IndexOf(e, StringComparison.Ordinal);
            if (k >= 0 && (cut < 0 || k < cut)) { cut = k; ext = e; }
        }
        if (cut < 0) return false;                       // static file or framework endpoint
        path = path.Substring(0, cut + ext.Length);
        foreach (string a in ALWAYS) if (path.EndsWith(a)) return false;


        if (!IsReadOnlyUser(ctx)) return false;

        string page = System.IO.Path.GetFileName(path);

        // 1. May they open this page?
        HashSet<string> allowed = AllowedPages(ctx);
        if (allowed.Count > 0 && !allowed.Contains(page))
            return Refuse(ctx, "PAGE", null,
                "This screen is not part of the Auditor's access. The Auditor role opens the " +
                "dashboards, audit trails, reports and record lists, and nothing that changes data.");

        // 2. Is this a read?
        string op = Operation(ctx);
        if (op != "" && !IsRead(op))
            return Refuse(ctx, "WRITE", op,
                "The Auditor role is read-only. \"" + op + "\" changes data, so it was not run. " +
                "Nothing has been altered.");

        return false;
    }

    /// <summary>
    /// The operation being asked for, if the request names one. A PageMethod arrives as
    /// Page.aspx/MethodName; the older screens pass ?action=Name. A plain page view names
    /// neither and is a read by definition.
    /// </summary>
    private static string Operation(HttpContext ctx)
    {
        try
        {
            string raw = (ctx.Request.Url == null ? "" : ctx.Request.Url.AbsolutePath) ?? "";
            int i = raw.ToLowerInvariant().IndexOf(".aspx/", StringComparison.Ordinal);
            if (i >= 0)
            {
                string m = raw.Substring(i + 6).Trim('/');
                if (m != "") return m;
            }
            string a = ctx.Request["action"];
            if (!string.IsNullOrEmpty(a)) return a.Trim();
        }
        catch { }
        return "";
    }

    /// <summary>
    /// Write words first, then the read prefixes, then refuse.
    ///
    /// The write test is on WORDS, not substrings, and the difference is not academic: a plain
    /// substring search finds "review" inside "PreviewBatchWorkflow" and refuses a method that
    /// only counts rows. Method names are camel case, so they split into words cleanly, and
    /// matching whole words removes that class of mistake entirely.
    ///
    /// A word counts as a write if it IS a write word, or begins with one that is long enough
    /// for the prefix to be meaningful, which catches "Deletes" and "Publishing" without
    /// letting "Additional" match "add".
    /// </summary>
    public static bool IsRead(string op)
    {
        string n = (op ?? "").Trim().ToLowerInvariant();
        if (n == "") return true;

        foreach (string a in ALLOW) if (n == a) return true;

        foreach (string word in Words(op))
            foreach (string w in WRITES)
                if (word == w || (w.Length >= 5 && word.StartsWith(w, StringComparison.Ordinal)))
                    return false;

        foreach (string r in READS) if (n.StartsWith(r, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>
    /// "PreviewBatchWorkflow" becomes preview, batch, workflow; "delete_record" becomes
    /// delete, record. Runs of capitals stay together so "GetCGPA" does not shatter.
    /// </summary>
    private static List<string> Words(string name)
    {
        var outp = new List<string>();
        var cur = new StringBuilder();
        string s = name ?? "";
        for (int i = 0; i < s.Length; i++)
        {
            char ch = s[i];
            if (!char.IsLetterOrDigit(ch))
            {
                if (cur.Length > 0) { outp.Add(cur.ToString().ToLowerInvariant()); cur.Length = 0; }
                continue;
            }
            bool boundary = cur.Length > 0 && char.IsUpper(ch) && !char.IsUpper(s[i - 1]);
            if (boundary) { outp.Add(cur.ToString().ToLowerInvariant()); cur.Length = 0; }
            cur.Append(ch);
        }
        if (cur.Length > 0) outp.Add(cur.ToString().ToLowerInvariant());
        return outp;
    }

    // ── who and what ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Who is asking. The session where there is one, the forms ticket otherwise, because a
    /// PageMethod request has the second and not always the first.
    /// </summary>
    private static string CurrentUser(HttpContext ctx)
    {
        try
        {
            if (ctx.Session != null)
            {
                string s = Convert.ToString(ctx.Session["username"] ?? "").Trim();
                if (s != "") return s;
            }
        }
        catch { }
        try
        {
            if (ctx.User != null && ctx.User.Identity != null && ctx.User.Identity.IsAuthenticated)
                return (ctx.User.Identity.Name ?? "").Trim();
        }
        catch { }
        return "";
    }

    /// <summary>
    /// Cached against the application, not the session, for the same reason: the requests that
    /// most need gating are the ones without a session to cache in. Sixty seconds is long
    /// enough to keep two queries off a busy page and short enough that a role change takes
    /// effect while the administrator is still watching.
    /// </summary>
    private static object CacheGet(string key)
    {
        try { return HttpRuntime.Cache["rogate:" + key]; } catch { return null; }
    }
    private static void CachePut(string key, object v)
    {
        try
        {
            HttpRuntime.Cache.Insert("rogate:" + key, v, null,
                DateTime.UtcNow.AddSeconds(60), System.Web.Caching.Cache.NoSlidingExpiration);
        }
        catch { }
    }

    private static bool IsReadOnlyUser(HttpContext ctx)
    {
        string user = CurrentUser(ctx);
        if (user == "") return false;

        object cached = CacheGet(SESS_FLAG + ":" + user);
        if (cached is bool) return (bool)cached;

        bool ro = false;
        {
            try
            {
                using (var c = new MySqlConnection(Conn))
                {
                    c.Open();
                    using (var cmd = new MySqlCommand(
                        "SELECT COUNT(*) FROM sys_user_roles ur JOIN sys_roles r ON r.id = ur.role_id " +
                        "WHERE ur.username=@u AND ur.is_active=1 AND r.is_active=1 " +
                        "  AND (ur.expires_at IS NULL OR ur.expires_at > NOW()) AND r.is_read_only=1", c))
                    {
                        cmd.Parameters.AddWithValue("@u", user);
                        ro = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                    // A read-only role only holds if the user has nothing else. Somebody who is
                    // both an auditor and a bursar is a bursar, and pretending otherwise would
                    // lock a working account out of its real job.
                    if (ro)
                        using (var cmd = new MySqlCommand(
                            "SELECT COUNT(*) FROM sys_user_roles ur JOIN sys_roles r ON r.id = ur.role_id " +
                            "WHERE ur.username=@u AND ur.is_active=1 AND r.is_active=1 " +
                            "  AND (ur.expires_at IS NULL OR ur.expires_at > NOW()) AND r.is_read_only=0", c))
                        {
                            cmd.Parameters.AddWithValue("@u", user);
                            if (Convert.ToInt32(cmd.ExecuteScalar()) > 0) ro = false;
                        }
                }
            }
            catch { ro = false; }
        }

        CachePut(SESS_FLAG + ":" + user, ro);
        return ro;
    }

    /// <summary>The page file names behind this user's granted slugs, resolved once per session.</summary>
    private static HashSet<string> AllowedPages(HttpContext ctx)
    {
        string user = CurrentUser(ctx);
        var set = CacheGet(SESS_PAGES + ":" + user) as HashSet<string>;
        if (set != null) return set;

        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT m.url FROM sys_user_roles ur " +
                    " JOIN sys_roles r            ON r.id = ur.role_id " +
                    " JOIN sys_role_permissions p ON p.role_id = r.id " +
                    " JOIN sys_menu_items m       ON m.menu_slug = p.menu_slug " +
                    "WHERE ur.username=@u AND ur.is_active=1 AND r.is_active=1 AND p.can_view=1 " +
                    "  AND IFNULL(m.url,'')<>''", c))
                {
                    cmd.Parameters.AddWithValue("@u", user);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                        {
                            string u = Convert.ToString(r[0]);
                            if (string.IsNullOrEmpty(u)) continue;
                            int q = u.IndexOf('?'); if (q >= 0) u = u.Substring(0, q);
                            string f = System.IO.Path.GetFileName(u.Replace('\\', '/'));
                            if (f != "") set.Add(f);
                        }
                }
            }
        }
        catch { }

        // The dashboard is where a refusal sends them, so it can never be absent.
        set.Add("NewDashboard.aspx");
        set.Add("AccessDenied.aspx");
        CachePut(SESS_PAGES + ":" + user, set);
        return set;
    }

    /// <summary>Clears the per-session cache, for when a user's roles change mid-session.</summary>
    public static void Forget(HttpContext ctx)
    {
        if (ctx == null) return;
        string user = CurrentUser(ctx);
        if (user == "") return;
        try
        {
            HttpRuntime.Cache.Remove("rogate:" + SESS_PAGES + ":" + user);
            HttpRuntime.Cache.Remove("rogate:" + SESS_FLAG + ":" + user);
        }
        catch { }
    }

    // ── refusing ────────────────────────────────────────────────────────────────────────

    private static bool Refuse(HttpContext ctx, string kind, string op, string message)
    {
        Log(ctx, kind, op, message);

        bool ajax = !string.IsNullOrEmpty(op) &&
                    (ctx.Request.ContentType ?? "").IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0;

        ctx.Response.Clear();
        if (ajax)
        {
            // The screens read o.d and show .message, so a refusal arrives as a normal
            // failure rather than as a broken page.
            ctx.Response.ContentType = "application/json";
            ctx.Response.StatusCode = 403;
            ctx.Response.Write("{\"d\":\"{\\\"success\\\":false,\\\"message\\\":\\\"" +
                               Escape(message) + "\\\"}\"}");
        }
        else
        {
            ctx.Response.ContentType = "text/html";
            ctx.Response.StatusCode = 403;
            ctx.Response.Write(Page(message));
        }
        ctx.Response.Flush();
        ctx.ApplicationInstance.CompleteRequest();
        return true;
    }

    private static string Escape(string s)
    {
        return (s ?? "").Replace("\\", "\\\\\\\\").Replace("\"", "\\\\\\\"");
    }

    private static string Page(string message)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Read only</title><style>");
        sb.Append("body{font-family:Segoe UI,Arial,sans-serif;background:#f5f7fa;margin:0;padding:24px;color:#1a1a2e}");
        sb.Append(".c{max-width:560px;margin:60px auto;background:#fff;border:1px solid #e0e5ed;padding:24px}");
        sb.Append("h1{font-size:17px;color:#05275C;margin:0 0 8px}p{font-size:13px;line-height:1.65;color:#475467;margin:0 0 14px}");
        sb.Append("a{display:inline-block;padding:7px 13px;background:#174DA4;color:#fff;text-decoration:none;font-size:12px}");
        sb.Append("</style></head><body><div class='c'><h1>Read only</h1><p>");
        sb.Append(HttpUtility.HtmlEncode(message));
        sb.Append("</p><p>This refusal has been recorded. If you need this screen, ask the ");
        sb.Append("system administrator to widen the Auditor role.</p>");
        sb.Append("<a href='/COOPERP/NewScreens/NewDashboard.aspx'>Back to the dashboard</a>");
        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    private static void Log(HttpContext ctx, string kind, string op, string reason)
    {
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                using (var cmd = new MySqlCommand(
                    "INSERT INTO sys_access_denied_log " +
                    "(username, role_code, path, operation, http_method, denied_for, reason, ip_address, denied_at) " +
                    "VALUES (@u,@rc,@p,@o,@m,@k,@r,@ip,NOW())", c))
                {
                    cmd.Parameters.AddWithValue("@u", Cut(CurrentUser(ctx), 100));
                    cmd.Parameters.AddWithValue("@rc", Cut(SafeRole(), 60));
                    cmd.Parameters.AddWithValue("@p", Cut(ctx.Request.RawUrl, 400));
                    cmd.Parameters.AddWithValue("@o", string.IsNullOrEmpty(op) ? (object)DBNull.Value : Cut(op, 160));
                    cmd.Parameters.AddWithValue("@m", Cut(ctx.Request.HttpMethod, 10));
                    cmd.Parameters.AddWithValue("@k", kind);
                    cmd.Parameters.AddWithValue("@r", Cut(reason, 255));
                    cmd.Parameters.AddWithValue("@ip", Cut(ctx.Request.UserHostAddress, 45));
                    cmd.ExecuteNonQuery();
                }
            }
        }
        catch { /* a refusal must still refuse even if it cannot be written down */ }
    }

    private static string SafeRole()
    {
        try { return RoleAccessService.GetRoleCode(); } catch { return ""; }
    }

    private static string Cut(string s, int n)
    {
        s = (s ?? "").Trim();
        return s.Length > n ? s.Substring(0, n) : s;
    }
}
