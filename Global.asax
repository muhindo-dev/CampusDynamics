<%@ Application Language="C#" %>

<script runat="server">

    /// <summary>
    /// Two console URLs for ONE physical page.
    ///
    /// fin_programme_fees holds a separate fee structure per study session, so the fee
    /// console has two contexts: the standard (day/weekend) rates and the in-service rates.
    /// Rather than duplicate a 2,100-line screen and its 3,400-line code-behind — which would
    /// then have to be kept in step for ever — both URLs are served by the same
    /// FeesStructure.aspx, and the route tells it which structure it is administering.
    ///
    ///   COOPERP/NewScreens/FeesStructure.aspx           -> standard (unchanged, still works)
    ///   COOPERP/NewScreens/MainFeesStructure.aspx       -> standard, explicitly
    ///   COOPERP/NewScreens/InServiceFeesStructure.aspx  -> in-service
    ///
    /// The slugs deliberately sit at the SAME folder depth as the real page. That screen links
    /// to its siblings with relative hrefs (FeesManagement.aspx, FeesRegistration.aspx, ...);
    /// a root-level slug such as /inservice-fees would resolve those against the wrong folder
    /// and silently break every tab on the page.
    ///
    /// RouteExistingFiles stays false (the default), so every physical .aspx in the
    /// application continues to be served directly and only these two invented names are
    /// handled by routing.
    /// </summary>
    void RegisterFeeStructureRoutes()
    {
        try
        {
            var routes = System.Web.Routing.RouteTable.Routes;
            const string page = "~/COOPERP/NewScreens/FeesStructure.aspx";

            if (routes["FeeStructure_InService"] == null)
            {
                var r = new System.Web.Routing.Route(
                    "COOPERP/NewScreens/InServiceFeesStructure.aspx",
                    new System.Web.Routing.RouteValueDictionary { { "mode", "INSERVICE" } },
                    new System.Web.Routing.PageRouteHandler(page));
                routes.Add("FeeStructure_InService", r);
            }

            if (routes["FeeStructure_Main"] == null)
            {
                var r = new System.Web.Routing.Route(
                    "COOPERP/NewScreens/MainFeesStructure.aspx",
                    new System.Web.Routing.RouteValueDictionary { { "mode", "MAIN" } },
                    new System.Web.Routing.PageRouteHandler(page));
                routes.Add("FeeStructure_Main", r);
            }
        }
        catch
        {
            // A failed route registration must never stop the application starting. The
            // physical FeesStructure.aspx keeps working and simply defaults to standard.
        }
    }

    void Application_BeginRequest(object sender, EventArgs e)
    {
        // Enforce HTTPS across the entire application
        // Skip for local development requests
        if (HttpContext.Current.Request.IsLocal)
            return;

        // Check if already secure (direct SSL or behind a reverse proxy)
        bool isSecure = HttpContext.Current.Request.IsSecureConnection;
        string forwardedProto = HttpContext.Current.Request.Headers["X-Forwarded-Proto"];
        if (!string.IsNullOrEmpty(forwardedProto))
            isSecure = forwardedProto.Equals("https", StringComparison.OrdinalIgnoreCase);

        if (!isSecure)
        {
            string url = "https://" + HttpContext.Current.Request.Url.Host
                       + HttpContext.Current.Request.Url.PathAndQuery;
            Response.Redirect(url, true);
        }
    }

    void Application_Start(object sender, EventArgs e)
    {
        // Code that runs on application startup
        Application["UsersLoggedIn"] = new System.Collections.Generic.List<string>();

        RegisterFeeStructureRoutes();

        // Schema migration: rename acad_applicant_choices.stud_reg_no → choice_reg_no
        // to avoid ambiguity with acad_applications.stud_reg_no in acad_GetApplicants SP.
        try
        {
            using (var conn = ApiHelper.GetConnection())
            {
                conn.Open();
                long hasOld = (long)new MySql.Data.MySqlClient.MySqlCommand(
                    "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
                    "WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='acad_applicant_choices' AND COLUMN_NAME='stud_reg_no'",
                    conn).ExecuteScalar();
                long hasNew = (long)new MySql.Data.MySqlClient.MySqlCommand(
                    "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
                    "WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='acad_applicant_choices' AND COLUMN_NAME='choice_reg_no'",
                    conn).ExecuteScalar();

                if (hasOld > 0 && hasNew == 0)
                    new MySql.Data.MySqlClient.MySqlCommand(
                        "ALTER TABLE acad_applicant_choices CHANGE COLUMN stud_reg_no choice_reg_no VARCHAR(50) NULL",
                        conn).ExecuteNonQuery();
                else if (hasOld == 0 && hasNew == 0)
                    new MySql.Data.MySqlClient.MySqlCommand(
                        "ALTER TABLE acad_applicant_choices ADD COLUMN choice_reg_no VARCHAR(50) NULL",
                        conn).ExecuteNonQuery();
            }
        }
        catch { /* best-effort: don't prevent startup if migration fails */ }

        // Schema migration: ensure all optional columns exist in acad_applications.
        // This eliminates per-request ColExists overhead and guarantees saves work.
        try
        {
            using (var conn = ApiHelper.GetConnection())
            {
                conn.Open();
                var appCols = new string[]
                {
                    "olevel_school VARCHAR(200) NULL",
                    "olevel_index VARCHAR(45) NULL",
                    "olevel_year INT NULL",
                    "olevel_agg VARCHAR(50) NULL",
                    "alevel_school VARCHAR(200) NULL",
                    "alevel_index VARCHAR(45) NULL",
                    "alevel_year INT NULL",
                    "alevel_points VARCHAR(50) NULL",
                    "other_institution VARCHAR(200) NULL",
                    "other_qualification VARCHAR(100) NULL",
                    "other_year INT NULL",
                    "other_grade VARCHAR(50) NULL",
                    "home_district VARCHAR(100) NULL",
                    "post_box VARCHAR(45) NULL",
                    "residence_country VARCHAR(45) NULL",
                    "sponsor_contact VARCHAR(100) NULL",
                    "stud_id_number VARCHAR(50) NULL",
                    "kin_relationship VARCHAR(50) NULL",
                    "kin_contacts VARCHAR(150) NULL",
                    "stud_entry_method VARCHAR(100) NULL",
                    "stud_mar_stat VARCHAR(20) NULL",
                    "title VARCHAR(10) NULL",
                    "physicalDisability VARCHAR(200) NULL",
                    "app_last_updated_at DATETIME NULL"
                };
                foreach (var colDef in appCols)
                {
                    string colName = colDef.Split(' ')[0];
                    long cnt = (long)new MySql.Data.MySqlClient.MySqlCommand(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
                        "WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='acad_applications' AND COLUMN_NAME='" + colName + "'",
                        conn).ExecuteScalar();
                    if (cnt == 0)
                        new MySql.Data.MySqlClient.MySqlCommand(
                            "ALTER TABLE acad_applications ADD COLUMN " + colDef,
                            conn).ExecuteNonQuery();
                }
            }
        }
        catch { /* best-effort */ }

        // Schema migration: ensure acad_student.completion_date exists (admin-entered academic
        // completion date that overrides the transcript's auto-computed completion date).
        try
        {
            using (var conn = ApiHelper.GetConnection())
            {
                conn.Open();
                long cnt = (long)new MySql.Data.MySqlClient.MySqlCommand(
                    "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
                    "WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='acad_student' AND COLUMN_NAME='completion_date'",
                    conn).ExecuteScalar();
                if (cnt == 0)
                    new MySql.Data.MySqlClient.MySqlCommand(
                        "ALTER TABLE acad_student ADD COLUMN completion_date DATE NULL", conn).ExecuteNonQuery();
            }
        }
        catch { /* best-effort */ }

        // Routine sql_mode fix: any procedure or function created with STRICT_TRANS_TABLES
        // embedded will always execute in strict mode regardless of session sql_mode.
        // The only fix is to DROP and CREATE each routine with sql_mode='' in effect.
        try
        {
            using (var conn = ApiHelper.GetConnection())
            {
                conn.Open();
                // Find every routine in this schema that has a strict mode baked in
                var strictRoutines = new System.Collections.Generic.List<string[]>();
                using (var infoCmd = new MySql.Data.MySqlClient.MySqlCommand(
                    "SELECT ROUTINE_NAME, ROUTINE_TYPE FROM INFORMATION_SCHEMA.ROUTINES " +
                    "WHERE ROUTINE_SCHEMA = DATABASE() " +
                    "AND (SQL_MODE LIKE '%STRICT_TRANS_TABLES%' OR SQL_MODE LIKE '%STRICT_ALL_TABLES%')", conn))
                using (var rdr = infoCmd.ExecuteReader())
                {
                    while (rdr.Read())
                        strictRoutines.Add(new[] { rdr.GetString(0), rdr.GetString(1) });
                }
                foreach (var r in strictRoutines)
                    FixRoutineSqlMode(conn, r[0], r[1]);
            }
        }
        catch { }

        // SchoolPay AUTO-SYNC engine: pings SchoolPay every few minutes and posts anything we are
        // missing (idempotent). Self-contained + self-healing; monitored/restartable from the
        // SchoolPay controller's Auto-Sync tab. Best-effort — never block app startup.
        try { SchoolPaySyncJob.EnsureStarted(); }
        catch { }

        // BILLING AUTO-RECONCILE engine: every N hours, force-registers + bills any student who is
        // enrolled (has courses) for the current semester but was left UNREGISTERED / unbilled
        // (idempotent, capped against runaways). Enforces "enrolled = registered + billed".
        // Best-effort — never block app startup.
        try { BillingReconciliationJob.EnsureStarted(); }
        catch { }

        // ID Card module: wire the notification hook to THIS app's EmailSenderProtocol
        // (main-app signature: message, recipients, subject, sender).
        try { IDCardService.Mailer = delegate(string to, string subj, string body) {
            try { EmailSenderProtocol.SendHtmlEmail(body, to, subj, "Muteesa I Royal University"); } catch { } }; }
        catch { }
    }

    void FixRoutineSqlMode(MySql.Data.MySqlClient.MySqlConnection conn, string name, string type)
    {
        // type = "PROCEDURE" or "FUNCTION"
        string createSql = null;
        try
        {
            string showSql = (type == "FUNCTION")
                ? "SHOW CREATE FUNCTION `" + name + "`"
                : "SHOW CREATE PROCEDURE `" + name + "`";
            string colName = (type == "FUNCTION") ? "Create Function" : "Create Procedure";

            using (var cmd = new MySql.Data.MySqlClient.MySqlCommand(showSql, conn))
            using (var rdr = cmd.ExecuteReader())
            {
                if (rdr.Read())
                    createSql = rdr[colName] == DBNull.Value ? null : rdr[colName].ToString();
            }
        }
        catch { return; }

        if (string.IsNullOrEmpty(createSql)) return;

        // Strip DEFINER — requires SUPER/SET_USER_ID privilege the app account may not have
        createSql = System.Text.RegularExpressions.Regex.Replace(
            createSql,
            @"CREATE\s+DEFINER\s*=\s*`[^`]*`\s*@\s*`[^`]*`\s+",
            "CREATE ",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        try
        {
            new MySql.Data.MySqlClient.MySqlCommand("SET SESSION sql_mode = ''", conn).ExecuteNonQuery();
            string dropSql = (type == "FUNCTION")
                ? "DROP FUNCTION IF EXISTS `" + name + "`"
                : "DROP PROCEDURE IF EXISTS `" + name + "`";
            new MySql.Data.MySqlClient.MySqlCommand(dropSql, conn).ExecuteNonQuery();
            new MySql.Data.MySqlClient.MySqlCommand(createSql, conn).ExecuteNonQuery();
        }
        catch { }
    }

    void Application_End(object sender, EventArgs e) 
    {
        //  Code that runs on application shutdown

    }
        
    void Application_Error(object sender, EventArgs e) 
    { 
        // Code that runs when an unhandled error occurs

    }

    void Session_Start(object sender, EventArgs e) 
    {
        // Code that runs when a new session is started

    }

    void Session_End(object sender, EventArgs e) 
    {
        

    }
    protected void Application_PreRequestHandlerExecute(Object sender, EventArgs e)
    {
        if (HttpContext.Current.Session != null)
        {
            if (Session["usernm"] != null)
            {
                string cacheKey = Session["usernm"].ToString();
                string cachedSessionId = (string)HttpContext.Current.Cache[cacheKey];

                if (cachedSessionId == null)
                {
                    // Cache was lost (app pool recycle) but user still has a valid
                    // session — re-register rather than forcing logout.
                    TimeSpan cacheTimeout = TimeSpan.FromHours(24);
                    HttpContext.Current.Cache.Insert(cacheKey,
                        Session.SessionID,
                        null,
                        DateTime.MaxValue,
                        cacheTimeout,
                        System.Web.Caching.CacheItemPriority.NotRemovable,
                        null);
                }
                else if (cachedSessionId != Session.SessionID)
                {
                    // A different session logged in with the same credentials
                    // — enforce single-session rule.
                    FormsAuthentication.SignOut();
                    Session.Abandon();
                    Response.Redirect("~/MultiLogin.aspx");
                }
            }
        }
    }

       
</script>
