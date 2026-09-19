using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Web;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

/// <summary>
/// Communications.aspx.cs — Code-behind for Communications Management page.
///
/// AJAX Endpoints (via ?ajax=...):
///   GET  ?ajax=list[&amp;status=&amp;audience=&amp;priority=&amp;search=] — list communications with stats
///   GET  ?ajax=get&amp;id=                — get single communication with attachments
///   POST ?ajax=save                    — create or update communication (JSON body)
///   POST ?ajax=delete                  — delete communication (JSON body: {id})
///   POST ?ajax=changestatus            — publish or archive (JSON body: {id, status})
///   POST ?ajax=upload&amp;commId=          — upload attachment file (multipart form)
///   POST ?ajax=removeattachment        — remove attachment (JSON body: {id})
///   GET  ?ajax=readstats&amp;id=           — read tracking statistics for a communication
///
/// Tables:
///   sys_communications             — main communication records (CRUD target)
///   sys_communication_attachments  — file attachments per communication
///   sys_communication_reads        — per-user read + confirmation tracking
///   sys_communication_comments     — user comments on communications
///
/// Design Rules:
///   - C# 4.0 compatible: no ?. operator, no string interpolation ($""), no out var
///   - vacConnectionString only — no hardcoded credentials
///   - Session["username"] for created_by, Session["ScreenName"] | Session["screenName"] for display name
///
/// Task: COMMUNICATIONS_MODULE_TASKS.md — Task T2
/// Created: 2025
/// </summary>
public partial class COOPERP_NewScreens_Communications : System.Web.UI.Page
{
    // ─────────────────────── Connection ──────────────────────────────────

    private string ConnStr
    {
        get
        {
            return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString;
        }
    }

    // ─────────────────────── Helpers ─────────────────────────────────────

    private DataTable ExecuteQuery(string sql, params MySqlParameter[] parms)
    {
        DataTable dt = new DataTable();
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null)
                    foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd))
                    da.Fill(dt);
            }
        }
        return dt;
    }

    private int ExecuteNonQuery(string sql, params MySqlParameter[] parms)
    {
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null)
                    foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                return cmd.ExecuteNonQuery();
            }
        }
    }

    private object ExecuteScalar(string sql, params MySqlParameter[] parms)
    {
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null)
                    foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                return cmd.ExecuteScalar();
            }
        }
    }

    private string RespondJson(object obj)
    {
        JavaScriptSerializer ser = new JavaScriptSerializer();
        ser.MaxJsonLength = int.MaxValue;
        string json = ser.Serialize(obj);
        Response.Clear();
        Response.ContentType = "application/json";
        Response.Write(json);
        Response.End();
        return null;
    }

    private string GetSession(string key)
    {
        if (Session[key] == null) return "";
        return Session[key].ToString();
    }

    private string SafeStr(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        return val.ToString();
    }

    private int SafeInt(object val)
    {
        if (val == null || val == DBNull.Value) return 0;
        int result;
        if (int.TryParse(val.ToString(), out result)) return result;
        return 0;
    }

    private string GetCurrentUser()
    {
        string u = GetSession("username");
        if (string.IsNullOrEmpty(u)) u = GetSession("regno");
        return u;
    }

    private string GetCurrentUserName()
    {
        string n = GetSession("ScreenName");
        if (string.IsNullOrEmpty(n)) n = GetSession("screenName");
        if (string.IsNullOrEmpty(n)) n = GetCurrentUser();
        return n;
    }

    private Dictionary<string, object> ReadJsonBody()
    {
        Request.InputStream.Position = 0;
        using (StreamReader sr = new StreamReader(Request.InputStream))
        {
            string body = sr.ReadToEnd();
            if (string.IsNullOrEmpty(body)) return new Dictionary<string, object>();
            JavaScriptSerializer ser = new JavaScriptSerializer();
            ser.MaxJsonLength = int.MaxValue;
            return ser.Deserialize<Dictionary<string, object>>(body);
        }
    }

    private string DictStr(Dictionary<string, object> d, string key)
    {
        object v;
        if (d.TryGetValue(key, out v) && v != null) return v.ToString().Trim();
        return "";
    }

    private int DictInt(Dictionary<string, object> d, string key)
    {
        string s = DictStr(d, key);
        int result;
        if (int.TryParse(s, out result)) return result;
        return 0;
    }

    // ─────────────────────── Upload Path ─────────────────────────────────

    private string UploadDir
    {
        get { return "~/Data_Uploads/Communications/"; }
    }

    private string EnsureUploadDir()
    {
        string path = Server.MapPath(UploadDir);
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        return path;
    }

    // ─────────────────────── Lifecycle ───────────────────────────────────

    protected void Page_Load(object sender, EventArgs e)
    {
        string ajax = (Request.QueryString["ajax"] ?? "").Trim().ToLower();
        if (string.IsNullOrEmpty(ajax)) return;

        try
        {
            string action = ajax;
            int ampIdx = ajax.IndexOf('&');
            if (ampIdx > 0) action = ajax.Substring(0, ampIdx);

            switch (action)
            {
                case "list":             HandleList();            break;
                case "get":              HandleGet();             break;
                case "save":             HandleSave();            break;
                case "delete":           HandleDelete();          break;
                case "changestatus":     HandleChangeStatus();    break;
                case "upload":           HandleUpload();          break;
                case "removeattachment": HandleRemoveAttachment();break;
                case "readstats":        HandleReadStats();       break;
                default:
                    RespondJson(new { ok = false, error = "Unknown action: " + action });
                    break;
            }
        }
        catch (System.Threading.ThreadAbortException) { /* Response.End */ }
        catch (Exception ex)
        {
            RespondJson(new { ok = false, error = ex.Message });
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LIST — All communications with filtering + stats
    // ═══════════════════════════════════════════════════════════════════════

    private void HandleList()
    {
        string fStatus   = (Request.QueryString["status"]   ?? "").Trim();
        string fAudience = (Request.QueryString["audience"]  ?? "").Trim();
        string fPriority = (Request.QueryString["priority"]  ?? "").Trim();
        string fSearch   = (Request.QueryString["search"]    ?? "").Trim();

        // Build WHERE clause
        List<string> conditions = new List<string>();
        List<MySqlParameter> parms = new List<MySqlParameter>();

        if (!string.IsNullOrEmpty(fStatus))
        {
            conditions.Add("c.status = @status");
            parms.Add(new MySqlParameter("@status", fStatus));
        }
        if (!string.IsNullOrEmpty(fAudience))
        {
            conditions.Add("c.target_audience = @audience");
            parms.Add(new MySqlParameter("@audience", fAudience));
        }
        if (!string.IsNullOrEmpty(fPriority))
        {
            conditions.Add("c.priority = @priority");
            parms.Add(new MySqlParameter("@priority", fPriority));
        }
        if (!string.IsNullOrEmpty(fSearch))
        {
            conditions.Add("c.title LIKE @search");
            parms.Add(new MySqlParameter("@search", "%" + fSearch + "%"));
        }

        string where = conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions.ToArray()) : "";

        string sql = @"
            SELECT c.*,
                   COALESCE(att.cnt, 0) AS attachCount,
                   COALESCE(rd.readCount, 0) AS readCount,
                   COALESCE(rd.confirmedCount, 0) AS confirmedCount
            FROM sys_communications c
            LEFT JOIN (
                SELECT communication_id, COUNT(*) AS cnt
                FROM sys_communication_attachments
                GROUP BY communication_id
            ) att ON att.communication_id = c.ID
            LEFT JOIN (
                SELECT communication_id,
                       COUNT(*) AS readCount,
                       SUM(CASE WHEN confirmed_at IS NOT NULL THEN 1 ELSE 0 END) AS confirmedCount
                FROM sys_communication_reads
                GROUP BY communication_id
            ) rd ON rd.communication_id = c.ID" +
            where + " ORDER BY c.created_at DESC";

        DataTable dt = ExecuteQuery(sql, parms.ToArray());

        List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
        foreach (DataRow row in dt.Rows)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["ID"]              = SafeInt(row["ID"]);
            d["title"]           = SafeStr(row["title"]);
            d["target_audience"] = SafeStr(row["target_audience"]);
            d["is_force_read"]   = SafeInt(row["is_force_read"]) == 1;
            d["priority"]        = SafeStr(row["priority"]);
            d["status"]          = SafeStr(row["status"]);
            d["published_at"]    = SafeStr(row["published_at"]);
            d["created_at"]      = SafeStr(row["created_at"]);
            d["created_by_name"] = SafeStr(row["created_by_name"]);
            d["attachCount"]     = SafeInt(row["attachCount"]);
            d["readCount"]       = SafeInt(row["readCount"]);
            d["confirmedCount"]  = SafeInt(row["confirmedCount"]);
            rows.Add(d);
        }

        // Stats
        DataTable statsDt = ExecuteQuery(@"
            SELECT
                COUNT(*)                                                      AS total,
                SUM(CASE WHEN status = 'PUBLISHED' THEN 1 ELSE 0 END)       AS published,
                SUM(CASE WHEN status = 'DRAFT'     THEN 1 ELSE 0 END)       AS draft,
                SUM(CASE WHEN status = 'ARCHIVED'  THEN 1 ELSE 0 END)       AS archived,
                SUM(CASE WHEN is_force_read = 1    THEN 1 ELSE 0 END)       AS forceRead
            FROM sys_communications");

        Dictionary<string, object> stats = new Dictionary<string, object>();
        if (statsDt.Rows.Count > 0)
        {
            stats["total"]     = SafeInt(statsDt.Rows[0]["total"]);
            stats["published"] = SafeInt(statsDt.Rows[0]["published"]);
            stats["draft"]     = SafeInt(statsDt.Rows[0]["draft"]);
            stats["archived"]  = SafeInt(statsDt.Rows[0]["archived"]);
            stats["forceRead"] = SafeInt(statsDt.Rows[0]["forceRead"]);
        }

        RespondJson(new { ok = true, rows = rows, stats = stats });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GET — Single communication with attachments
    // ═══════════════════════════════════════════════════════════════════════

    private void HandleGet()
    {
        int id = 0;
        string idStr = Request.QueryString["id"] ?? "";
        int.TryParse(idStr, out id);
        if (id <= 0)
        {
            RespondJson(new { ok = false, error = "Invalid ID" });
            return;
        }

        DataTable dt = ExecuteQuery("SELECT * FROM sys_communications WHERE ID = @id",
            new MySqlParameter("@id", id));

        if (dt.Rows.Count == 0)
        {
            RespondJson(new { ok = false, error = "Communication not found" });
            return;
        }

        DataRow row = dt.Rows[0];
        Dictionary<string, object> comm = new Dictionary<string, object>();
        comm["ID"]               = SafeInt(row["ID"]);
        comm["title"]            = SafeStr(row["title"]);
        comm["content"]          = SafeStr(row["content"]);
        comm["target_audience"]  = SafeStr(row["target_audience"]);
        comm["is_force_read"]    = SafeInt(row["is_force_read"]) == 1;
        comm["force_read_expiry"]= SafeStr(row["force_read_expiry"]);
        comm["priority"]         = SafeStr(row["priority"]);
        comm["status"]           = SafeStr(row["status"]);
        comm["allow_comments"]   = SafeInt(row["allow_comments"]) == 1;
        comm["show_in_marquee"]  = SafeInt(row["show_in_marquee"]) == 1;
        comm["marquee_expiry"]   = SafeStr(row["marquee_expiry"]);
        comm["created_by"]       = SafeStr(row["created_by"]);
        comm["created_by_name"]  = SafeStr(row["created_by_name"]);
        comm["created_at"]       = SafeStr(row["created_at"]);
        comm["published_at"]     = SafeStr(row["published_at"]);

        // Attachments
        DataTable attDt = ExecuteQuery(
            "SELECT * FROM sys_communication_attachments WHERE communication_id = @id ORDER BY uploaded_at",
            new MySqlParameter("@id", id));

        List<Dictionary<string, object>> attachments = new List<Dictionary<string, object>>();
        foreach (DataRow aRow in attDt.Rows)
        {
            Dictionary<string, object> a = new Dictionary<string, object>();
            a["ID"]        = SafeInt(aRow["ID"]);
            a["file_name"] = SafeStr(aRow["file_name"]);
            a["file_path"] = SafeStr(aRow["file_path"]);
            a["file_type"] = SafeStr(aRow["file_type"]);
            a["file_size"] = SafeInt(aRow["file_size"]);
            attachments.Add(a);
        }
        comm["attachments"] = attachments;

        RespondJson(new { ok = true, comm = comm });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SAVE — Create or Update communication
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Reads an expiry date from the form and returns the LAST MOMENT of that day.
    ///
    /// The field is a date, not a date and time, so a bare parse gives midnight - which means
    /// "until the 20th" would actually stop at the very start of the 20th, a day earlier than
    /// whoever typed it intended. Returning 23:59:59 makes the chosen day count.
    ///
    /// The date box posts yyyy-MM-dd, so that is tried first and exactly; the looser parse is
    /// only a fallback for anything saved by an older version of the form.
    /// </summary>
    private static bool TryReadExpiry(string raw, out DateTime endOfDay)
    {
        endOfDay = DateTime.MinValue;
        if (raw == null) return false;
        raw = raw.Trim();
        if (raw.Length == 0) return false;

        DateTime parsed;
        bool ok = DateTime.TryParseExact(raw, "yyyy-MM-dd",
                      System.Globalization.CultureInfo.InvariantCulture,
                      System.Globalization.DateTimeStyles.None, out parsed)
               || DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                      System.Globalization.DateTimeStyles.None, out parsed);
        if (!ok) return false;

        endOfDay = parsed.Date.AddDays(1).AddSeconds(-1);
        return true;
    }

    private void HandleSave()
    {
        Dictionary<string, object> body = ReadJsonBody();
        int id = DictInt(body, "id");
        string title = DictStr(body, "title");
        string content = DictStr(body, "content");
        string audience = DictStr(body, "target_audience");
        string priority = DictStr(body, "priority");
        string status = DictStr(body, "status");
        int forceRead = DictInt(body, "is_force_read");
        string expiry = DictStr(body, "force_read_expiry");
        int allowComments = DictInt(body, "allow_comments");
        int showInMarquee = DictInt(body, "show_in_marquee");
        string marqueeExpiry = DictStr(body, "marquee_expiry");
        string user = GetCurrentUser();
        string userName = GetCurrentUserName();

        if (string.IsNullOrEmpty(title))
        {
            RespondJson(new { ok = false, error = "Title is required" });
            return;
        }
        if (string.IsNullOrEmpty(content))
        {
            RespondJson(new { ok = false, error = "Content is required" });
            return;
        }

        // Validate audience
        if (audience != "STUDENT" && audience != "STAFF" && audience != "BOTH") audience = "BOTH";
        if (priority != "NORMAL" && priority != "HIGH" && priority != "URGENT") priority = "NORMAL";
        if (status != "DRAFT" && status != "PUBLISHED" && status != "ARCHIVED") status = "DRAFT";

        // Parse expiry date.
        //
        // These used to fall back to DBNull whenever the text would not parse. For a force-read
        // notice DBNull means "never expires" - ForceRead.aspx and PortalMaster both gate on
        // "force_read_expiry IS NULL OR force_read_expiry > NOW()" - so a mistyped date quietly
        // became an indefinite block on every student in the audience, and the save reported
        // success. Now an unreadable date is refused and the admin is told which field.
        MySqlParameter expiryParam;
        if (forceRead == 1 && !string.IsNullOrEmpty(expiry))
        {
            DateTime expiryDate;
            if (!TryReadExpiry(expiry, out expiryDate))
            {
                RespondJson(new { ok = false, error = "The force-read end date could not be read. Pick it from the date box." });
                return;
            }
            expiryParam = new MySqlParameter("@expiry", expiryDate);
        }
        else
        {
            expiryParam = new MySqlParameter("@expiry", DBNull.Value);
        }

        // Parse marquee expiry date
        MySqlParameter marqueeExpiryParam;
        if (showInMarquee == 1 && !string.IsNullOrEmpty(marqueeExpiry))
        {
            DateTime mExpDate;
            if (!TryReadExpiry(marqueeExpiry, out mExpDate))
            {
                RespondJson(new { ok = false, error = "The banner end date could not be read. Pick it from the date box." });
                return;
            }
            marqueeExpiryParam = new MySqlParameter("@marqueeExpiry", mExpDate);
        }
        else
        {
            marqueeExpiryParam = new MySqlParameter("@marqueeExpiry", DBNull.Value);
        }

        if (id > 0)
        {
            // UPDATE
            string publishedClause = "";
            if (status == "PUBLISHED")
            {
                // Only set published_at if currently not published
                DataTable existing = ExecuteQuery("SELECT status FROM sys_communications WHERE ID = @id",
                    new MySqlParameter("@id", id));
                if (existing.Rows.Count > 0 && SafeStr(existing.Rows[0]["status"]) != "PUBLISHED")
                    publishedClause = ", published_at = NOW()";
            }

            string sql = "UPDATE sys_communications SET " +
                "title = @title, content = @content, target_audience = @audience, " +
                "priority = @priority, status = @status, " +
                "is_force_read = @force, force_read_expiry = @expiry, " +
                "allow_comments = @allowComments, " +
                "show_in_marquee = @showMarquee, marquee_expiry = @marqueeExpiry" + publishedClause +
                " WHERE ID = @id";

            ExecuteNonQuery(sql,
                new MySqlParameter("@title", title),
                new MySqlParameter("@content", content),
                new MySqlParameter("@audience", audience),
                new MySqlParameter("@priority", priority),
                new MySqlParameter("@status", status),
                new MySqlParameter("@force", forceRead),
                expiryParam,
                new MySqlParameter("@allowComments", allowComments),
                new MySqlParameter("@showMarquee", showInMarquee),
                marqueeExpiryParam,
                new MySqlParameter("@id", id));

            RespondJson(new { ok = true, id = id });
        }
        else
        {
            // INSERT
            string publishedVal = status == "PUBLISHED" ? "NOW()" : "NULL";
            string sql = "INSERT INTO sys_communications " +
                "(title, content, target_audience, priority, status, " +
                "is_force_read, force_read_expiry, allow_comments, " +
                "show_in_marquee, marquee_expiry, " +
                "created_by, created_by_name, published_at) " +
                "VALUES (@title, @content, @audience, @priority, @status, " +
                "@force, @expiry, @allowComments, " +
                "@showMarquee, @marqueeExpiry, " +
                "@user, @userName, " + publishedVal + ")";

            ExecuteNonQuery(sql,
                new MySqlParameter("@title", title),
                new MySqlParameter("@content", content),
                new MySqlParameter("@audience", audience),
                new MySqlParameter("@priority", priority),
                new MySqlParameter("@status", status),
                new MySqlParameter("@force", forceRead),
                expiryParam,
                new MySqlParameter("@allowComments", allowComments),
                new MySqlParameter("@showMarquee", showInMarquee),
                marqueeExpiryParam,
                new MySqlParameter("@user", user),
                new MySqlParameter("@userName", userName));

            object newId = ExecuteScalar("SELECT LAST_INSERT_ID()");
            RespondJson(new { ok = true, id = SafeInt(newId) });
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DELETE
    // ═══════════════════════════════════════════════════════════════════════

    private void HandleDelete()
    {
        Dictionary<string, object> body = ReadJsonBody();
        int id = DictInt(body, "id");
        if (id <= 0)
        {
            RespondJson(new { ok = false, error = "Invalid ID" });
            return;
        }

        // Delete attachments from disk first
        DataTable attDt = ExecuteQuery(
            "SELECT file_path FROM sys_communication_attachments WHERE communication_id = @id",
            new MySqlParameter("@id", id));
        foreach (DataRow row in attDt.Rows)
        {
            string fp = SafeStr(row["file_path"]);
            if (!string.IsNullOrEmpty(fp))
            {
                try
                {
                    string fullPath = Server.MapPath("~/" + fp);
                    if (File.Exists(fullPath)) File.Delete(fullPath);
                }
                catch { }
            }
        }

        // CASCADE deletes attachments, reads, comments
        ExecuteNonQuery("DELETE FROM sys_communications WHERE ID = @id",
            new MySqlParameter("@id", id));

        RespondJson(new { ok = true });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CHANGE STATUS — Publish or Archive
    // ═══════════════════════════════════════════════════════════════════════

    private void HandleChangeStatus()
    {
        Dictionary<string, object> body = ReadJsonBody();
        int id = DictInt(body, "id");
        string status = DictStr(body, "status");
        if (id <= 0 || (status != "PUBLISHED" && status != "ARCHIVED" && status != "DRAFT"))
        {
            RespondJson(new { ok = false, error = "Invalid parameters" });
            return;
        }

        string publishedClause = "";
        if (status == "PUBLISHED")
            publishedClause = ", published_at = COALESCE(published_at, NOW())";

        ExecuteNonQuery(
            "UPDATE sys_communications SET status = @status" + publishedClause + " WHERE ID = @id",
            new MySqlParameter("@status", status),
            new MySqlParameter("@id", id));

        RespondJson(new { ok = true });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UPLOAD — File attachment
    // ═══════════════════════════════════════════════════════════════════════

    private void HandleUpload()
    {
        string commIdStr = Request.QueryString["commId"] ?? "";
        int commId;
        if (!int.TryParse(commIdStr, out commId) || commId <= 0)
        {
            RespondJson(new { ok = false, error = "Invalid communication ID" });
            return;
        }

        if (Request.Files.Count == 0)
        {
            RespondJson(new { ok = false, error = "No file uploaded" });
            return;
        }

        HttpPostedFile file = Request.Files[0];
        if (file == null || file.ContentLength == 0)
        {
            RespondJson(new { ok = false, error = "Empty file" });
            return;
        }

        // Keep original name for display in the UI
        string originalName = Path.GetFileName(file.FileName); // strip any path prefix from older browsers
        string ext = Path.GetExtension(originalName).ToLower();
        string nameWithoutExt = Path.GetFileNameWithoutExtension(originalName);

        // Sanitize: replace spaces and non-alphanumeric chars (except hyphens) with underscores
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (char c in nameWithoutExt)
        {
            if (char.IsLetterOrDigit(c) || c == '-')
                sb.Append(c);
            else
                sb.Append('_');
        }
        string safeName = sb.ToString().Trim('_');
        if (string.IsNullOrEmpty(safeName)) safeName = "file";

        // Ensure unique filename with a timestamp+random suffix
        string uniqueName = safeName + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + new Random().Next(1000, 9999).ToString() + ext;

        string dir = EnsureUploadDir();
        string fullPath = Path.Combine(dir, uniqueName);

        try
        {
            file.SaveAs(fullPath);
        }
        catch (Exception ex)
        {
            RespondJson(new { ok = false, error = "File save failed: " + ex.Message });
            return;
        }

        string relativePath = "Data_Uploads/Communications/" + uniqueName;
        string mimeType = file.ContentType ?? "";

        try
        {
            ExecuteNonQuery(
                "INSERT INTO sys_communication_attachments (communication_id, file_name, file_path, file_type, file_size) " +
                "VALUES (@commId, @fileName, @filePath, @fileType, @fileSize)",
                new MySqlParameter("@commId", commId),
                new MySqlParameter("@fileName", originalName),      // original name for display
                new MySqlParameter("@filePath", relativePath),       // sanitized path on disk
                new MySqlParameter("@fileType", mimeType),
                new MySqlParameter("@fileSize", file.ContentLength));
        }
        catch (Exception ex)
        {
            // Clean up the orphaned file if DB insert fails
            try { if (File.Exists(fullPath)) File.Delete(fullPath); } catch { }
            RespondJson(new { ok = false, error = "Database error: " + ex.Message });
            return;
        }

        object newId = ExecuteScalar("SELECT LAST_INSERT_ID()");
        RespondJson(new { ok = true, id = SafeInt(newId), name = originalName, path = relativePath });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // REMOVE ATTACHMENT
    // ═══════════════════════════════════════════════════════════════════════

    private void HandleRemoveAttachment()
    {
        Dictionary<string, object> body = ReadJsonBody();
        int id = DictInt(body, "id");
        if (id <= 0)
        {
            RespondJson(new { ok = false, error = "Invalid attachment ID" });
            return;
        }

        // Get file path before deleting
        DataTable dt = ExecuteQuery(
            "SELECT file_path FROM sys_communication_attachments WHERE ID = @id",
            new MySqlParameter("@id", id));
        if (dt.Rows.Count > 0)
        {
            string fp = SafeStr(dt.Rows[0]["file_path"]);
            if (!string.IsNullOrEmpty(fp))
            {
                try
                {
                    string fullPath = Server.MapPath("~/" + fp);
                    if (File.Exists(fullPath)) File.Delete(fullPath);
                }
                catch { }
            }
        }

        ExecuteNonQuery("DELETE FROM sys_communication_attachments WHERE ID = @id",
            new MySqlParameter("@id", id));

        RespondJson(new { ok = true });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // READ STATS — Per-communication reader list
    // ═══════════════════════════════════════════════════════════════════════

    private void HandleReadStats()
    {
        int id = 0;
        string idStr = Request.QueryString["id"] ?? "";
        int.TryParse(idStr, out id);
        if (id <= 0)
        {
            RespondJson(new { ok = false, error = "Invalid ID" });
            return;
        }

        // Communication title
        object titleObj = ExecuteScalar("SELECT title FROM sys_communications WHERE ID = @id",
            new MySqlParameter("@id", id));
        string title = titleObj != null ? titleObj.ToString() : "";

        // Reader list
        DataTable dt = ExecuteQuery(
            "SELECT * FROM sys_communication_reads WHERE communication_id = @id ORDER BY read_at DESC",
            new MySqlParameter("@id", id));

        int readCount = dt.Rows.Count;
        int confirmedCount = 0;
        List<Dictionary<string, object>> readers = new List<Dictionary<string, object>>();
        foreach (DataRow row in dt.Rows)
        {
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["user_id"]           = SafeStr(row["user_id"]);
            r["user_type"]         = SafeStr(row["user_type"]);
            r["user_name"]         = SafeStr(row["user_name"]);
            r["read_at"]           = SafeStr(row["read_at"]);
            r["confirmed_at"]      = SafeStr(row["confirmed_at"]);
            r["confirmation_text"] = SafeStr(row["confirmation_text"]);
            readers.Add(r);

            if (!string.IsNullOrEmpty(SafeStr(row["confirmed_at"]))) confirmedCount++;
        }

        RespondJson(new { ok = true, title = title, readCount = readCount, confirmedCount = confirmedCount, readers = readers });
    }
}
