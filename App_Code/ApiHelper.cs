using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Caching;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;
using System.Configuration;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// API v2 Helper — Standard response formatting, token auth, and database utilities.
/// Place this file in App_Code so it's auto-compiled.
/// </summary>
public static class ApiHelper
{
    private const string API_VERSION = "2.1";
    private static readonly JavaScriptSerializer _serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

    #region Response Helpers

    /// <summary>Sends a standard JSON success response and ends the request.</summary>
    public static void Success(HttpResponse response, object data, string message = "OK")
    {
        if (HttpContext.Current != null && HttpContext.Current.Items.Contains("_api_response_sent")) return;

        WriteJson(response, new Dictionary<string, object>
        {
            { "success", true },
            { "message", message },
            { "data", data },
            { "timestamp", DateTime.UtcNow.ToString("o") }
        });
    }

    /// <summary>Sends a standard JSON error response and ends the request.</summary>
    public static void Error(HttpResponse response, string message, string errorCode = "SERVER_ERROR", int httpStatus = 200)
    {
        if (HttpContext.Current != null && HttpContext.Current.Items.Contains("_api_response_sent")) return;

        WriteJson(response, new Dictionary<string, object>
        {
            { "success", false },
            { "message", message },
            { "error_code", errorCode },
            { "data", null },
            { "timestamp", DateTime.UtcNow.ToString("o") }
        });
    }

    private static void WriteJson(HttpResponse response, object obj)
    {
        response.Clear();
        // ROB-05: Content-Type always set first, before any logic that could throw
        response.ContentType = "application/json";
        response.Charset = "utf-8";
        // ROB-08: API version in every response
        response.AppendHeader("X-API-Version", API_VERSION);
        ApplyCorsHeaders(response);
        response.Write(_serializer.Serialize(obj));

        if (HttpContext.Current != null)
            HttpContext.Current.Items["_api_response_sent"] = true;

        response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    /// <summary>
    /// Applies CORS headers. ROB-12: reads CorsAllowedOrigins from appSettings;
    /// defaults to * if not configured.
    /// </summary>
    private static void ApplyCorsHeaders(HttpResponse response)
    {
        string allowedOrigins = ConfigurationManager.AppSettings["CorsAllowedOrigins"] ?? "*";

        if (allowedOrigins == "*")
        {
            response.AppendHeader("Access-Control-Allow-Origin", "*");
        }
        else
        {
            // Match the incoming Origin against the whitelist
            string requestOrigin = "";
            if (HttpContext.Current != null && HttpContext.Current.Request != null)
                requestOrigin = HttpContext.Current.Request.Headers["Origin"] ?? "";

            bool matched = false;
            foreach (string origin in allowedOrigins.Split(','))
            {
                if (string.Equals(origin.Trim(), requestOrigin, StringComparison.OrdinalIgnoreCase))
                {
                    response.AppendHeader("Access-Control-Allow-Origin", requestOrigin);
                    response.AppendHeader("Vary", "Origin");
                    matched = true;
                    break;
                }
            }
            if (!matched)
                response.AppendHeader("Access-Control-Allow-Origin", allowedOrigins.Split(',')[0].Trim());
        }

        response.AppendHeader("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
        response.AppendHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");
    }

    #endregion

    #region Rate Limiting (ROB-01)

    // Sliding-window rate limiter using HttpRuntime.Cache.
    // Limits: login = 10/min per IP; general = 120/min per token-or-IP.
    private const int RATE_LIMIT_GENERAL = 120;
    private const int RATE_LIMIT_LOGIN   = 10;
    private const int RATE_WINDOW_SECS   = 60;

    /// <summary>
    /// Checks rate limit for the current request. Returns true (rate limited) and sends 429 if exceeded.
    /// Call at the top of Page_Load before action dispatch.
    /// action="login" uses a stricter limit; all others use the general limit.
    /// </summary>
    public static bool IsRateLimited(HttpRequest request, HttpResponse response, string action = "")
    {
        try
        {
            string key;
            int limit;
            if (action == "login")
            {
                key   = "rl_login_" + (request.UserHostAddress ?? "");
                limit = RATE_LIMIT_LOGIN;
            }
            else
            {
                string tokenOrIp = request["token"] ?? request.UserHostAddress ?? "anon";
                key   = "rl_api_" + tokenOrIp;
                limit = RATE_LIMIT_GENERAL;
            }

            var cache = HttpRuntime.Cache;
            int count = 1;
            object existing = cache.Get(key);
            if (existing != null)
                count = (int)existing + 1;

            cache.Insert(key, count, null,
                Cache.NoAbsoluteExpiration,
                TimeSpan.FromSeconds(RATE_WINDOW_SECS),
                CacheItemPriority.Low, null);

            if (count > limit)
            {
                if (!response.IsRequestBeingRedirected)
                {
                    response.StatusCode = 429;
                    response.AppendHeader("Retry-After", RATE_WINDOW_SECS.ToString());
                    Error(response, "Rate limit exceeded. Max " + limit + " requests per " + RATE_WINDOW_SECS + " seconds.", "RATE_LIMITED");
                }
                return true;
            }
        }
        catch { /* never block a request due to rate-limiter failure */ }

        return false;
    }

    #endregion

    #region DataTable to Object Conversion

    /// <summary>Converts a DataTable to a List of Dictionaries for JSON serialization.</summary>
    public static List<Dictionary<string, object>> TableToList(DataTable dt)
    {
        var rows = new List<Dictionary<string, object>>();
        if (dt == null) return rows;
        foreach (DataRow dr in dt.Rows)
        {
            var row = new Dictionary<string, object>();
            foreach (DataColumn col in dt.Columns)
            {
                object val = dr[col];
                if (val is DBNull) val = null;
                row[col.ColumnName] = val;
            }
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>Converts first row of DataTable to a Dictionary. Returns null if empty.</summary>
    public static Dictionary<string, object> FirstRowToDict(DataTable dt)
    {
        if (dt == null || dt.Rows.Count == 0) return null;
        var row = new Dictionary<string, object>();
        DataRow dr = dt.Rows[0];
        foreach (DataColumn col in dt.Columns)
        {
            object val = dr[col];
            if (val is DBNull) val = null;
            row[col.ColumnName] = val;
        }
        return row;
    }

    #endregion

    #region Database Helpers

    /// <summary>Gets a MySQL connection using the vacConnectionString.</summary>
    public static MySqlConnection GetConnection()
    {
        string connStr = ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString;
        return new MySqlConnection(connStr);
    }

    /// <summary>Gets a MySQL connection for the portal database.</summary>
    public static MySqlConnection GetPortalConnection()
    {
        string connStr = ConfigurationManager.ConnectionStrings["campus_dynamics_portalConnectionString"].ConnectionString;
        return new MySqlConnection(connStr);
    }

    /// <summary>Gets a MySQL connection for the accounts database.</summary>
    public static MySqlConnection GetAccountsConnection()
    {
        string connStr = ConfigurationManager.ConnectionStrings["accountsConnectionString"].ConnectionString;
        return new MySqlConnection(connStr);
    }

    /// <summary>Executes a query and returns a DataTable.</summary>
    public static DataTable Query(string sql, params MySqlParameter[] parameters)
    {
        using (var conn = GetConnection())
        {
            conn.Open();
            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.CommandTimeout = 60;
                if (parameters != null)
                {
                    foreach (var p in parameters)
                        cmd.Parameters.Add(p);
                }
                using (var adapter = new MySqlDataAdapter(cmd))
                {
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>Executes a query against the PORTAL database (campus_dynamics_portal) and returns a DataTable.
    /// Use for ODEL (odel_*) and other portal-owned tables. Cross-DB joins to campus_dynamics.* work on the same server.</summary>
    public static DataTable QueryPortal(string sql, params MySqlParameter[] parameters)
    {
        using (var conn = GetPortalConnection())
        {
            conn.Open();
            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.CommandTimeout = 60;
                if (parameters != null)
                    foreach (var p in parameters) cmd.Parameters.Add(p);
                using (var adapter = new MySqlDataAdapter(cmd))
                {
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>Executes a non-query (INSERT/UPDATE/DELETE) against the PORTAL database.</summary>
    /// <summary>
    /// True when this statement touches the provisional coursework or exam marks, which the
    /// database triggers audit. Checked so that only mark writes pay for the extra round trip.
    /// </summary>
    private static bool TouchesMarks(string sql)
    {
        if (string.IsNullOrEmpty(sql)) return false;
        return sql.IndexOf("provisional_course_work_marks", StringComparison.OrdinalIgnoreCase) >= 0
            || sql.IndexOf("provisional_exam_marks", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Names the caller for the audit trigger, on the connection the write is about to use.
    /// Without this the change is still recorded — the trigger cannot be bypassed — but it is
    /// recorded as 'system', which is a fact about the audit rather than about the marks.
    /// </summary>
    private static void NameTheActor(MySqlConnection conn, string sql, string source)
    {
        if (!TouchesMarks(sql)) return;
        try { MarkAuditContext.Set(conn, null, source, "Mark written through the staff API"); }
        catch { }
    }

    public static int ExecutePortal(string sql, params MySqlParameter[] parameters)
    {
        using (var conn = GetPortalConnection())
        {
            conn.Open();
            NameTheActor(conn, sql, "API/v2:ExecutePortal");
            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.CommandTimeout = 60;
                if (parameters != null)
                    foreach (var p in parameters) cmd.Parameters.Add(p);
                return cmd.ExecuteNonQuery();
            }
        }
    }

    /// <summary>Executes an INSERT against the PORTAL database and returns the auto-generated ID.</summary>
    public static long ExecuteInsertPortal(string sql, params MySqlParameter[] parameters)
    {
        using (var conn = GetPortalConnection())
        {
            conn.Open();
            NameTheActor(conn, sql, "API/v2:ExecuteInsertPortal");
            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.CommandTimeout = 60;
                if (parameters != null)
                    foreach (var p in parameters) cmd.Parameters.Add(p);
                cmd.ExecuteNonQuery();
                return cmd.LastInsertedId;
            }
        }
    }

    /// <summary>Executes a scalar query against the PORTAL database and returns the result.</summary>
    public static object ScalarPortal(string sql, params MySqlParameter[] parameters)
    {
        using (var conn = GetPortalConnection())
        {
            conn.Open();
            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.CommandTimeout = 60;
                if (parameters != null)
                    foreach (var p in parameters) cmd.Parameters.Add(p);
                return cmd.ExecuteScalar();
            }
        }
    }

    /// <summary>Executes a query against the accounts database and returns a DataTable.</summary>
    public static DataTable QueryAccounts(string sql, params MySqlParameter[] parameters)
    {
        using (var conn = GetAccountsConnection())
        {
            conn.Open();
            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.CommandTimeout = 60;
                if (parameters != null)
                {
                    foreach (var p in parameters)
                        cmd.Parameters.Add(p);
                }
                using (var adapter = new MySqlDataAdapter(cmd))
                {
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>Executes a stored procedure and returns a DataTable.</summary>
    public static DataTable QueryProc(string procName, params MySqlParameter[] parameters)
    {
        using (var conn = GetConnection())
        {
            conn.Open();
            using (var cmd = new MySqlCommand(procName, conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 60;
                if (parameters != null)
                {
                    foreach (var p in parameters)
                        cmd.Parameters.Add(p);
                }
                using (var adapter = new MySqlDataAdapter(cmd))
                {
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    /// <summary>Executes a non-query command (INSERT, UPDATE, DELETE).</summary>
    public static int Execute(string sql, params MySqlParameter[] parameters)
    {
        using (var conn = GetConnection())
        {
            conn.Open();
            NameTheActor(conn, sql, "API/v2:Execute");
            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.CommandTimeout = 60;
                if (parameters != null)
                {
                    foreach (var p in parameters)
                        cmd.Parameters.Add(p);
                }
                return cmd.ExecuteNonQuery();
            }
        }
    }

    /// <summary>Executes an INSERT and returns the auto-generated ID. Uses a single connection so LAST_INSERT_ID is reliable.</summary>
    public static long ExecuteInsert(string sql, params MySqlParameter[] parameters)
    {
        using (var conn = GetConnection())
        {
            conn.Open();
            NameTheActor(conn, sql, "API/v2:ExecuteInsert");
            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.CommandTimeout = 60;
                if (parameters != null)
                    foreach (var p in parameters)
                        cmd.Parameters.Add(p);
                cmd.ExecuteNonQuery();
                return cmd.LastInsertedId;
            }
        }
    }

    /// <summary>Executes a scalar query and returns the result.</summary>
    public static object Scalar(string sql, params MySqlParameter[] parameters)
    {
        using (var conn = GetConnection())
        {
            conn.Open();
            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.CommandTimeout = 60;
                if (parameters != null)
                    foreach (var p in parameters)
                        cmd.Parameters.Add(p);
                return cmd.ExecuteScalar();
            }
        }
    }

    /// <summary>Executes a non-query command on the accounts database (INSERT/UPDATE/DELETE).</summary>
    public static int ExecuteAccounts(string sql, params MySqlParameter[] parameters)
    {
        using (var conn = GetAccountsConnection())
        {
            conn.Open();
            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.CommandTimeout = 60;
                if (parameters != null)
                    foreach (var p in parameters)
                        cmd.Parameters.Add(p);
                return cmd.ExecuteNonQuery();
            }
        }
    }

    /// <summary>Executes a scalar query against the accounts database and returns the result.</summary>
    public static object ScalarAccounts(string sql, params MySqlParameter[] parameters)
    {
        using (var conn = GetAccountsConnection())
        {
            conn.Open();
            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.CommandTimeout = 60;
                if (parameters != null)
                    foreach (var p in parameters)
                        cmd.Parameters.Add(p);
                return cmd.ExecuteScalar();
            }
        }
    }

    /// <summary>
    /// Logs a billing failure to fin_billing_errors so admins can identify and fix unbilled students.
    /// Never throws — logging must not affect the caller's flow.
    /// </summary>
    public static void LogBillingError(string regno, string acadYear, int semester, string message, string source)
    {
        try
        {
            using (var conn = GetAccountsConnection())
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    @"INSERT IGNORE INTO fin_billing_errors (regno, acad_year, semester, triggered_by, trigger_source, error_message)
                      VALUES (@r, @a, @s, 'API', @src, @msg)", conn))
                {
                    cmd.Parameters.AddWithValue("@r",   regno   ?? "");
                    cmd.Parameters.AddWithValue("@a",   acadYear ?? "");
                    cmd.Parameters.AddWithValue("@s",   semester);
                    cmd.Parameters.AddWithValue("@src", source  ?? "API");
                    cmd.Parameters.AddWithValue("@msg", message ?? "");
                    cmd.ExecuteNonQuery();
                }
            }
        }
        catch { }
    }

    #endregion

    #region Request Helpers

    /// <summary>Gets a required parameter from the request. Sends error if missing.</summary>
    public static string RequireParam(HttpRequest request, HttpResponse response, string name)
    {
        string val = request[name];
        if (string.IsNullOrEmpty(val))
        {
            Error(response, "Missing required parameter: " + name, "MISSING_PARAM");
            return null; // will never reach here because Error calls Response.End()
        }
        return val;
    }

    /// <summary>Gets an optional parameter from the request with a default value.</summary>
    public static string Param(HttpRequest request, string name, string defaultValue = "")
    {
        string val = request[name];
        return string.IsNullOrEmpty(val) ? defaultValue : val;
    }

    /// <summary>Gets an optional integer parameter.</summary>
    public static int ParamInt(HttpRequest request, string name, int defaultValue = 0)
    {
        string val = request[name];
        int result;
        return int.TryParse(val, out result) ? result : defaultValue;
    }

    #endregion

    #region Security Helpers

    /// <summary>Generates a secure random token string.</summary>
    public static string GenerateToken()
    {
        byte[] tokenBytes = new byte[32];
        using (var rng = new RNGCryptoServiceProvider())
        {
            rng.GetBytes(tokenBytes);
        }
        return Convert.ToBase64String(tokenBytes).Replace("+", "").Replace("/", "").Replace("=", "");
    }

    /// <summary>Hashes a password using SHA256 (matches the portal's approach).</summary>
    public static string HashPassword(string password)
    {
        using (var sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            var sb = new StringBuilder();
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    /// <summary>Handles CORS preflight OPTIONS requests. ROB-12: uses configurable whitelist.</summary>
    public static bool HandleCors(HttpRequest request, HttpResponse response)
    {
        // ROB-05: set Content-Type immediately so every response is JSON even on early exit
        response.ContentType = "application/json";
        response.Charset = "utf-8";
        response.AppendHeader("X-API-Version", API_VERSION);
        ApplyCorsHeaders(response);
        if (request.HttpMethod == "OPTIONS")
        {
            response.StatusCode = 200;
            CompleteResponse(response);
            return true;
        }
        return false;
    }

    /// <summary>ROB-10: Validates that an academic year string matches YYYY/YYYY format.</summary>
    public static bool ValidateAcadYear(string acadYear, HttpResponse response = null)
    {
        if (string.IsNullOrEmpty(acadYear)) return true; // optional param — allow empty
        bool valid = Regex.IsMatch(acadYear, @"^\d{4}/\d{4}$");
        if (!valid && response != null)
            Error(response, "Invalid acad_year format. Expected YYYY/YYYY (e.g. 2025/2026).", "INVALID_PARAM");
        return valid;
    }

    /// <summary>ROB-04: Validates a parameter contains no SQL-dangerous content (belt-and-suspenders; always use parameterised queries too).</summary>
    public static string SanitiseParam(string val)
    {
        if (string.IsNullOrEmpty(val)) return val;
        // Strip null bytes and trim
        return val.Replace("\0", "").Trim();
    }

    /// <summary>
    /// Ends the response safely without throwing ThreadAbortException.
    /// Use this instead of Response.End() for binary or non-JSON responses.
    /// </summary>
    public static void CompleteResponse(HttpResponse response)
    {
        if (HttpContext.Current != null)
            HttpContext.Current.Items["_api_response_sent"] = true;
        response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    #endregion
}
