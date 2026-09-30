using System;
using System.Configuration;
using System.Globalization;
using System.Text;
using System.Collections.Generic;
using System.Web;
using System.Web.UI;
using MySql.Data.MySqlClient;

/// <summary>
/// Student Photo Change Controller (eadmin).
/// Reviews student self-service photo changes recorded by the eportal uploader.
/// Approve  -> photo_status = APPROVED (the new photo stays live).
/// Reject   -> photo_status = REJECTED and acad_student.photofile is BLANKED, so the
///             rejected photo is no longer served anywhere; the student must upload a
///             new one or delete. Supports per-row and batch approve/reject.
/// </summary>
public partial class COOPERP_NewScreens_PhotoChangeController : System.Web.UI.Page
{
    private const int PAGE_SIZE = 20;
    private const string PHOTO_BASE = "/COOPERP/StudentInfo/photos/";

    private string ConnectionString
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    private string GetCurrentUser()
    {
        if (Session["username"] != null && Session["username"].ToString().Trim() != "")
            return Session["username"].ToString().Trim();
        if (HttpContext.Current.User != null && HttpContext.Current.User.Identity.IsAuthenticated)
            return HttpContext.Current.User.Identity.Name;
        return "admin";
    }

    /// <summary>
    /// True when the request carries a signed-in staff session.
    ///
    /// Accepts either signal the eadmin screens use — a forms-authentication identity or
    /// the Session["username"] the older pages set — because different entry points
    /// establish one or the other, and requiring both would lock out real administrators.
    /// </summary>
    private bool IsCallerAuthenticated()
    {
        try
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated
                && !string.IsNullOrEmpty(User.Identity.Name)) return true;
        }
        catch { }
        try
        {
            if (Session != null)
            {
                object u = Session["username"];
                if (u != null && !string.IsNullOrEmpty(u.ToString().Trim())) return true;
            }
        }
        catch { }
        return false;
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        string action = Request.QueryString["action"] ?? Request.Form["action"];
        if (!string.IsNullOrEmpty(action))
        {
            Response.Clear();
            Response.ContentType = "application/json";
            Response.Cache.SetCacheability(HttpCacheability.NoCache);

            /* Every action below changes a student's official photograph — approving,
               rejecting, banning, deleting, and now replacing the image outright.
               None of it was gated: an anonymous POST to this page ran the handler and
               reached the database, and GetCurrentUser() quietly signed the audit trail
               "admin". Verified against production before this was added.

               200 with success:false, NOT 401: FormsAuthenticationModule rewrites a 401
               into a 302 to the login page, so the caller receives an HTML login form
               where it expected JSON and reports "request failed" instead of "your
               session expired". The status code buys nothing — the request is already
               refused — and what matters is that the caller can read why. */
            if (!IsCallerAuthenticated())
            {
                Response.Write("{\"success\":false,\"message\":\"Your session has expired. Please sign in again, then retry.\"}");
                try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
                return;
            }

            try
            {
                if (action == "review") Response.Write(HandleReview());
                else if (action == "batch") Response.Write(HandleBatch());
                else if (action == "deleteversion") Response.Write(HandleDeleteVersion());
                else if (action == "admininit") Response.Write(HandleAdminInit());
                else if (action == "unban") Response.Write(HandleUnban());
                else if (action == "restoreversion") Response.Write(HandleRestoreVersion());
                else if (action == "lookupstudent") Response.Write(HandleLookupStudent());
                else if (action == "adminupload") Response.Write(HandleAdminUpload());
                else if (action == "idcardscan") Response.Write(HandleIdCardScan());
                else if (action == "idcardplace") Response.Write(HandleIdCardPlace());
                else Response.Write("{\"success\":false,\"message\":\"Unknown action.\"}");
            }
            catch (Exception ex)
            {
                Response.Write("{\"success\":false,\"message\":\"" + JsEnc(ex.Message) + "\"}");
            }
            try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
            return;
        }

        litBody.Text = BuildList();
    }

    // ===================================================================
    // REVIEW (approve / reject)
    // ===================================================================
    private string HandleReview()
    {
        int id = SafeInt(Request.Form["id"], 0);
        bool approve = string.Equals(Request.Form["decision"], "approve", StringComparison.OrdinalIgnoreCase);
        string comment = (Request.Form["comment"] ?? "").Trim();
        bool ban = Request.Form["ban"] == "1";
        if (id <= 0) return "{\"success\":false,\"message\":\"Missing record id.\"}";

        using (var conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            string outcome = ReviewOne(conn, id, approve, comment, GetCurrentUser(), ban);
            bool ok = outcome == "OK" || outcome.StartsWith("OK+");
            string msg;
            if (!ok) msg = outcome;
            else if (approve) msg = "Photograph approved.";
            else
            {
                int swept = 0;
                if (outcome.StartsWith("OK+")) int.TryParse(outcome.Substring(3), out swept);
                msg = swept > 0
                    ? "Photograph rejected, along with " + swept + " other pending version" + (swept == 1 ? "" : "s") + " from this student."
                    : "Photograph rejected and removed from the student.";
            }
            return "{\"success\":" + (ok ? "true" : "false") + ",\"message\":\"" + JsEnc(msg) + "\"}";
        }
    }

    private string HandleBatch()
    {
        string idsCsv = Request.Form["ids"] ?? "";
        bool approve = string.Equals(Request.Form["decision"], "approve", StringComparison.OrdinalIgnoreCase);
        string comment = (Request.Form["comment"] ?? "").Trim();
        bool ban = Request.Form["ban"] == "1";

        var ids = new List<int>();
        foreach (string part in idsCsv.Split(','))
        {
            int v; if (int.TryParse(part.Trim(), out v) && v > 0) ids.Add(v);
        }
        if (ids.Count == 0) return "{\"success\":false,\"message\":\"No records selected.\"}";

        int done = 0, skipped = 0;
        string user = GetCurrentUser();
        using (var conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            foreach (int id in ids)
            {
                string r = ReviewOne(conn, id, approve, comment, user, ban);
                if (r == "OK" || r.StartsWith("OK+")) done++; else skipped++;
            }
        }
        string verb = approve ? "approved" : "rejected";
        return "{\"success\":true,\"message\":\"" + done + " " + verb + (skipped > 0 ? ", " + skipped + " skipped (already reviewed)" : "") + ".\"}";
    }

    // ===================================================================
    // DELETE VERSION (remove a duplicate / unwanted submitted photo)
    // ===================================================================
    /// <summary>
    /// Deletes one or more photo *versions* (stud_photo_change rows). Used when a
    /// student submitted several photographs and only one is acceptable: the extra
    /// junk versions are removed from the review queue. Soft-marked DELETED (kept for
    /// audit) and the underlying thumbnail is physically removed only when nothing else
    /// references it. The student's CURRENT live photograph is never deletable this way
    /// (use Approve / Reject for it) so a delete can never blank a student's photo.
    /// Accepts a single id (Form["id"]) or a CSV of ids (Form["ids"]).
    /// </summary>
    private string HandleDeleteVersion()
    {
        string idsCsv = Request.Form["ids"];
        if (string.IsNullOrEmpty(idsCsv)) idsCsv = Request.Form["id"] ?? "";
        string comment = (Request.Form["comment"] ?? "").Trim();

        var ids = new List<int>();
        foreach (string part in idsCsv.Split(','))
        {
            int v; if (int.TryParse(part.Trim(), out v) && v > 0) ids.Add(v);
        }
        if (ids.Count == 0) return "{\"success\":false,\"message\":\"No versions selected.\"}";

        int done = 0, skipped = 0; string lastSkip = "";
        string user = GetCurrentUser();
        using (var conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            foreach (int id in ids)
            {
                string r = DeleteOne(conn, id, comment, user);
                if (r == "OK") done++; else { skipped++; lastSkip = r; }
            }
        }
        string msg = done + " version" + (done == 1 ? "" : "s") + " deleted"
            + (skipped > 0 ? ", " + skipped + " skipped" + (lastSkip != "" ? " (" + lastSkip + ")" : "") : "") + ".";
        return "{\"success\":true,\"message\":\"" + JsEnc(msg) + "\"}";
    }

    /// <summary>Soft-deletes a single change row. Returns "OK" or a skip reason.</summary>
    private string DeleteOne(MySqlConnection conn, int id, string comment, string user)
    {
        string regno = "", newPhoto = "", status = "";
        using (var cmd = new MySqlCommand("SELECT regno, COALESCE(new_photofile,''), status FROM stud_photo_change WHERE id=@id LIMIT 1", conn))
        {
            cmd.Parameters.AddWithValue("@id", id);
            using (var rd = cmd.ExecuteReader())
            {
                if (!rd.Read()) return "not found";
                regno = rd.GetString(0);
                newPhoto = rd.GetString(1);
                status = rd.GetString(2).ToUpperInvariant();
            }
        }
        if (status == "DELETED") return "already deleted";

        // Guard: never delete the student's current live photograph via this path.
        string livePhoto = "";
        using (var cmd = new MySqlCommand("SELECT COALESCE(photofile,'') FROM acad_student WHERE regno=@r LIMIT 1", conn))
        { cmd.Parameters.AddWithValue("@r", regno); object o = cmd.ExecuteScalar(); livePhoto = (o == null || o == DBNull.Value) ? "" : o.ToString(); }

        if (newPhoto != "" && string.Equals(newPhoto, livePhoto, StringComparison.OrdinalIgnoreCase))
            return "current live photo — approve or reject it instead";

        using (var tx = conn.BeginTransaction())
        {
            try
            {
                using (var cmd = new MySqlCommand(
                    "UPDATE stud_photo_change SET status='DELETED', reviewed_by=@by, reviewed_at=NOW(), review_comment=@c WHERE id=@id AND status<>'DELETED'", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@by", user);
                    cmd.Parameters.AddWithValue("@c", comment == "" ? (object)DBNull.Value : comment);
                    cmd.Parameters.AddWithValue("@id", id);
                    if (cmd.ExecuteNonQuery() == 0) { tx.Rollback(); return "already deleted"; }
                }
                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        // Reclaim the junk thumbnail from disk only when nothing else points at it.
        TryDeletePhotoFile(conn, newPhoto, livePhoto);
        return "OK";
    }

    /// <summary>
    /// Physically removes a photo thumbnail from disk, but ONLY when it is truly orphaned:
    /// not the passed live photo, not any student's current photofile, and not referenced by
    /// any surviving (non-DELETED) change row as either the new or the previous photo. Safe to
    /// no-op — the record delete has already succeeded regardless.
    /// </summary>
    private void TryDeletePhotoFile(MySqlConnection conn, string file, string livePhoto)
    {
        if (string.IsNullOrEmpty(file)) return;
        if (string.Equals(file, livePhoto, StringComparison.OrdinalIgnoreCase)) return;
        // Path-traversal guard: only a bare filename is ever a valid photofile value.
        if (file.IndexOf('/') >= 0 || file.IndexOf('\\') >= 0 || file.IndexOf("..", StringComparison.Ordinal) >= 0) return;

        using (var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM stud_photo_change WHERE status<>'DELETED' AND (new_photofile=@f OR old_photofile=@f)", conn))
        { cmd.Parameters.AddWithValue("@f", file); if (Convert.ToInt32(cmd.ExecuteScalar()) > 0) return; }
        using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM acad_student WHERE photofile=@f", conn))
        { cmd.Parameters.AddWithValue("@f", file); if (Convert.ToInt32(cmd.ExecuteScalar()) > 0) return; }

        try
        {
            string full = Server.MapPath("~/COOPERP/StudentInfo/photos/" + file);
            if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
        }
        catch { /* non-critical — the version is already removed from the queue */ }
    }

    /// <summary>
    /// Admin-initiated override: create a record for a specific student and set their
    /// photo_status to ANY value (PENDING / APPROVED / REJECTED). REJECTED blanks the
    /// photofile (blocks it), matching the normal reject. Recorded as an admin action.
    /// </summary>
    private string HandleAdminInit()
    {
        string regno = (Request.Form["regno"] ?? "").Trim();
        string status = (Request.Form["status"] ?? "").Trim().ToUpperInvariant();
        string comment = (Request.Form["comment"] ?? "").Trim();
        bool ban = Request.Form["ban"] == "1";

        if (regno == "") return "{\"success\":false,\"message\":\"Please enter a registration number.\"}";
        if (status != "PENDING" && status != "APPROVED" && status != "REJECTED")
            return "{\"success\":false,\"message\":\"Choose a valid status (PENDING, APPROVED or REJECTED).\"}";

        using (var conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();

            string curPhoto = null, name = "";
            using (var cmd = new MySqlCommand(
                "SELECT COALESCE(photofile,''), TRIM(CONCAT(COALESCE(firstname,''),' ',COALESCE(othername,''))) FROM acad_student WHERE regno=@r LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) return "{\"success\":false,\"message\":\"No student found with reg no '" + JsEnc(regno) + "'.\"}";
                    curPhoto = rd.IsDBNull(0) ? "" : rd.GetString(0);
                    name = rd.IsDBNull(1) ? "" : rd.GetString(1);
                }
            }

            string user = GetCurrentUser();
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    if (status == "REJECTED")
                    {
                        using (var cmd = new MySqlCommand("UPDATE acad_student SET photo_status='REJECTED', photofile='' WHERE regno=@r", conn, tx))
                        { cmd.Parameters.AddWithValue("@r", regno); cmd.ExecuteNonQuery(); }
                    }
                    else
                    {
                        using (var cmd = new MySqlCommand("UPDATE acad_student SET photo_status=@s WHERE regno=@r", conn, tx))
                        { cmd.Parameters.AddWithValue("@s", status); cmd.Parameters.AddWithValue("@r", regno); cmd.ExecuteNonQuery(); }
                    }

                    using (var cmd = new MySqlCommand(
                        "INSERT INTO stud_photo_change (regno, old_photofile, new_photofile, status, source, requested_at, reviewed_by, reviewed_at, review_comment) " +
                        "VALUES (@r, @o, @n, @s, 'eadmin-admin', NOW(), @by, NOW(), @c)", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@r", regno);
                        cmd.Parameters.AddWithValue("@o", curPhoto);
                        cmd.Parameters.AddWithValue("@n", status == "REJECTED" ? "" : curPhoto);
                        cmd.Parameters.AddWithValue("@s", status);
                        cmd.Parameters.AddWithValue("@by", user);
                        cmd.Parameters.AddWithValue("@c", comment == "" ? (object)DBNull.Value : comment);
                        cmd.ExecuteNonQuery();
                    }
                    // If this admin action rejected the photo, apply ban rules (manual tick or 3+ rejections).
                    if (status == "REJECTED") ApplyBanIfNeeded(conn, tx, regno, ban, comment, user);
                    tx.Commit();
                }
                catch { tx.Rollback(); throw; }
            }

            string who = name == "" ? regno : name + " (" + regno + ")";
            return "{\"success\":true,\"message\":\"Official-photograph status for " + JsEnc(who) + " set to " + status + ".\"}";
        }
    }

    // ===================================================================
    // ADMIN UPLOAD — put a photograph on a student's record on their behalf
    // ===================================================================
    //
    // WHY IT IS TWO STEPS. The one serious mistake available here is putting a
    // photograph on the WRONG student, and that mistake is invisible afterwards:
    // the record looks perfectly normal, just with somebody else's face on it, and
    // it flows onward to the ID card and the transcript. So the registration number
    // is resolved to a person FIRST — name, programme, and the photograph currently
    // on file — and the administrator confirms that person before any file is sent.
    // A single-step "type a reg no and upload" form would be quicker and would
    // eventually put a stranger's face on somebody's ID card.

    /// <summary>
    /// action=lookupstudent — resolve a registration number to a person, so the
    /// administrator can see who they are about to change before they change them.
    /// Read-only.
    /// </summary>
    private string HandleLookupStudent()
    {
        string regno = (Request.Form["regno"] ?? Request.QueryString["regno"] ?? "").Trim();
        if (regno == "") return "{\"success\":false,\"message\":\"Enter a registration number.\"}";

        using (var conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();

            string name = "", prog = "", photo = "", status = "";
            bool banned = false;
            string banReason = "";
            using (var cmd = new MySqlCommand(
                "SELECT TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) AS nm, " +
                "       COALESCE(p.progname, COALESCE(s.progid,'')) AS prog, " +
                "       COALESCE(s.photofile,'') AS pf, COALESCE(s.photo_status,'') AS ps, " +
                "       COALESCE(s.photo_banned,0) AS bn, COALESCE(s.photo_ban_reason,'') AS br " +
                "FROM acad_student s LEFT JOIN acad_programme p ON p.progcode = s.progid " +
                "WHERE s.regno=@r LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read())
                        return "{\"success\":false,\"message\":\"No student found with registration number '" + JsEnc(regno) + "'. Check the number and try again.\"}";
                    name = Safe(rd, "nm");
                    prog = Safe(rd, "prog");
                    photo = Safe(rd, "pf");
                    status = Safe(rd, "ps");
                    banned = Safe(rd, "bn") == "1";
                    banReason = Safe(rd, "br");
                }
            }

            // How many versions are already waiting in the queue for this student.
            // Uploading approves this one and clears those, so the administrator is
            // told the number rather than discovering it afterwards.
            int pending = 0;
            using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM stud_photo_change WHERE regno=@r AND status='PENDING'", conn))
            { cmd.Parameters.AddWithValue("@r", regno); pending = Convert.ToInt32(cmd.ExecuteScalar()); }

            // Only offer a photo URL when the file is genuinely on disk. A broken image
            // in the confirmation panel would read as "this student has no photo" and
            // invite exactly the wrong conclusion.
            string photoUrl = (HasPhoto(photo) && PhotoFileExists(photo)) ? PHOTO_BASE + photo : "";

            var sb = new StringBuilder();
            sb.Append("{\"success\":true");
            sb.Append(",\"regno\":\"").Append(JsEnc(regno)).Append("\"");
            sb.Append(",\"name\":\"").Append(JsEnc(name)).Append("\"");
            sb.Append(",\"programme\":\"").Append(JsEnc(prog)).Append("\"");
            sb.Append(",\"photoUrl\":\"").Append(JsEnc(photoUrl)).Append("\"");
            sb.Append(",\"status\":\"").Append(JsEnc(status)).Append("\"");
            sb.Append(",\"banned\":").Append(banned ? "true" : "false");
            sb.Append(",\"banReason\":\"").Append(JsEnc(banReason)).Append("\"");
            sb.Append(",\"pending\":").Append(pending);
            sb.Append("}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// action=adminupload — an administrator sets a student's official photograph
    /// directly, for the counter case: the student cannot upload (banned, no phone,
    /// no data) and has brought a printed or digital photograph to the office.
    ///
    /// The result is an APPROVED photograph, because an administrator doing this IS
    /// the approval — routing their own upload back into their own review queue would
    /// be theatre. It is still written to stud_photo_change as a normal version with
    /// their username on it, so the change is as auditable and as reversible as any
    /// other: the previous photograph is recorded as old_photofile and can be put back
    /// with the existing "revert to this version" action.
    /// </summary>
    private string HandleAdminUpload()
    {
        string regno = (Request.Form["regno"] ?? "").Trim();
        string comment = (Request.Form["comment"] ?? "").Trim();
        if (regno == "") return "{\"success\":false,\"message\":\"Enter a registration number.\"}";

        HttpPostedFile posted = Request.Files["photoFile"];
        if (posted == null || posted.ContentLength <= 0)
            return "{\"success\":false,\"message\":\"No photograph was received. Please choose a file and try again.\"}";

        // Refuse an oversized upload before pulling it into memory.
        if (posted.ContentLength > StudentPhotoValidator.MaxUploadBytes)
            return "{\"success\":false,\"message\":\"This photo is " + StudentPhotoValidator.SizeLabel(posted.ContentLength)
                 + ", and the biggest we can take is " + StudentPhotoValidator.SizeLabel(StudentPhotoValidator.MaxUploadBytes)
                 + ". Please use a smaller copy.\"}";

        byte[] original;
        using (System.IO.Stream input = posted.InputStream)
        {
            int len = Convert.ToInt32(input.Length);
            original = new byte[len];
            int off = 0, read;
            while (off < len && (read = input.Read(original, off, len - off)) > 0) off += read;
        }

        // Exactly the same validation the student's own uploader applies — same rules,
        // same wording. An administrator should not be able to store a file a student
        // could not, and vice versa.
        string why;
        if (!StudentPhotoValidator.IsUsablePhoto(original, out why))
            return "{\"success\":false,\"message\":\"" + JsEnc(why) + "\"}";

        using (var conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();

            // Resolve the student BEFORE writing anything to disk, so a mistyped
            // registration number cannot leave a stray thumbnail behind.
            string name = "", oldPhoto = "";
            bool wasBanned = false;
            using (var cmd = new MySqlCommand(
                "SELECT TRIM(CONCAT(COALESCE(firstname,''),' ',COALESCE(othername,''))), COALESCE(photofile,''), COALESCE(photo_banned,0) " +
                "FROM acad_student WHERE regno=@r LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read())
                        return "{\"success\":false,\"message\":\"No student found with registration number '" + JsEnc(regno) + "'.\"}";
                    name = rd.IsDBNull(0) ? "" : rd.GetString(0).Trim();
                    oldPhoto = rd.IsDBNull(1) ? "" : rd.GetString(1).Trim();
                    wasBanned = !rd.IsDBNull(2) && rd.GetValue(2).ToString().Trim() == "1";
                }
            }

            byte[] thumb;
            try
            {
                thumb = new imageManager().MakeThumb(original, StudentPhotoValidator.ThumbJpegQuality);
            }
            catch (OutOfMemoryException)
            {
                // GDI+ reports a corrupt or unsupported image as OutOfMemoryException,
                // which is almost never about memory.
                return "{\"success\":false,\"message\":\"This photo file is damaged and could not be opened. Please use another copy, saved as JPG.\"}";
            }
            catch
            {
                return "{\"success\":false,\"message\":\"This photo could not be opened. Please save it as a JPG and try again.\"}";
            }

            string fileName = Guid.NewGuid().ToString("N") + ".jpg";
            string folder = Server.MapPath("~/COOPERP/StudentInfo/photos/");
            string fullPath = System.IO.Path.Combine(folder, fileName);
            try
            {
                if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);
                System.IO.File.WriteAllBytes(fullPath, thumb);
            }
            catch (Exception ex)
            {
                return "{\"success\":false,\"message\":\"The photograph could not be saved to disk: " + JsEnc(ex.Message) + "\"}";
            }

            string user = GetCurrentUser();
            var orphans = new List<string>();
            int newId;
            try
            {
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        // The version record first, so it carries an id the purge can keep.
                        using (var cmd = new MySqlCommand(
                            "INSERT INTO stud_photo_change (regno, old_photofile, new_photofile, status, source, requested_at, reviewed_by, reviewed_at, review_comment) " +
                            "VALUES (@r, @o, @n, 'APPROVED', 'eadmin-admin', NOW(), @by, NOW(), @c)", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@r", regno);
                            cmd.Parameters.AddWithValue("@o", oldPhoto);
                            cmd.Parameters.AddWithValue("@n", fileName);
                            cmd.Parameters.AddWithValue("@by", user);
                            cmd.Parameters.AddWithValue("@c", comment == ""
                                ? "Photograph uploaded at the office by an administrator on the student's behalf."
                                : comment);
                            cmd.ExecuteNonQuery();
                        }
                        using (var cmd = new MySqlCommand("SELECT LAST_INSERT_ID()", conn, tx))
                            newId = Convert.ToInt32(cmd.ExecuteScalar());

                        using (var cmd = new MySqlCommand(
                            "UPDATE acad_student SET photofile=@p, photo_status='APPROVED' WHERE regno=@r", conn, tx))
                        { cmd.Parameters.AddWithValue("@p", fileName); cmd.Parameters.AddWithValue("@r", regno); cmd.ExecuteNonQuery(); }

                        // Same consequence as approving in the queue: this photograph is now
                        // the official one, so the student's other submitted versions go.
                        // Without this the new photo would sit alongside stale PENDING cards
                        // that an administrator could later "approve" back over the top of it.
                        orphans = PurgeOtherVersions(conn, tx, regno, newId, user);

                        // An administrator uploading FOR a banned student is resolving the
                        // very thing the ban exists to force. Leaving the ban on would block
                        // them from ever fixing their own photograph again.
                        if (wasBanned)
                        {
                            using (var cmd = new MySqlCommand(
                                "UPDATE acad_student SET photo_banned=0, photo_ban_reason=NULL, photo_ban_at=NULL, photo_ban_by=NULL WHERE regno=@r", conn, tx))
                            { cmd.Parameters.AddWithValue("@r", regno); cmd.ExecuteNonQuery(); }
                        }

                        tx.Commit();
                    }
                    catch { tx.Rollback(); throw; }
                }
            }
            catch (Exception ex)
            {
                // The database did not take it, so the file must not survive either —
                // otherwise every failed attempt leaves an orphan thumbnail on disk.
                try { System.IO.File.Delete(fullPath); } catch { }
                return "{\"success\":false,\"message\":\"The photograph was not saved: " + JsEnc(ex.Message) + "\"}";
            }

            // Cleanup AFTER the commit — never before, or a rollback would leave the
            // database pointing at files that are already gone. TryDeletePhotoFile is
            // told which file is now live so it cannot reclaim the one just saved, and
            // it re-checks every other reference before unlinking anything.
            foreach (string f in orphans) TryDeletePhotoFile(conn, f, fileName);

            // An ID card halted for want of a photograph can go back in the queue.
            ResumeHaltedIdCards(conn, regno);

            string who = name == "" ? regno : name + " (" + regno + ")";
            string msg = "Official photograph set for " + who + ".";
            if (wasBanned) msg += " Their upload ban has been lifted.";
            return "{\"success\":true,\"message\":\"" + JsEnc(msg) + "\",\"photoUrl\":\"" + JsEnc(PHOTO_BASE + fileName) + "\"}";
        }
    }

    /// <summary>
    /// Returns any ID-card requests halted for want of a photograph to the print queue,
    /// mirroring what the student's own uploader does. Never fatal: the photograph is
    /// already saved by this point, and a card that stays halted is a nuisance rather
    /// than a corruption.
    /// </summary>
    private void ResumeHaltedIdCards(MySqlConnection conn, string regno)
    {
        try
        {
            var halted = new List<int>();
            using (var cmd = new MySqlCommand("SELECT id FROM idcard_requests WHERE regno=@r AND status='HALTED'", conn))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read()) halted.Add(rd.GetInt32(0));
            }
            foreach (int rid in halted)
            {
                using (var cmd = new MySqlCommand(
                    "UPDATE idcard_requests SET status='SUBMITTED', halt_reason=NULL, updated_at=NOW() WHERE id=@id AND status='HALTED'", conn))
                { cmd.Parameters.AddWithValue("@id", rid); cmd.ExecuteNonQuery(); }
                using (var cmd = new MySqlCommand(
                    "INSERT INTO idcard_request_events (request_id, from_status, to_status, actor, actor_role, channel, note, created_at) " +
                    "VALUES (@id,'HALTED','SUBMITTED',@by,'admin','eadmin','Auto-resubmitted after an administrator set the student photograph', NOW())", conn))
                { cmd.Parameters.AddWithValue("@id", rid); cmd.Parameters.AddWithValue("@by", GetCurrentUser()); cmd.ExecuteNonQuery(); }
            }
        }
        catch { /* non-critical — the photograph is already saved */ }
    }

    /// <summary>
    /// The most recent photograph this student had APPROVED, other than the one being rejected —
    /// the version to fall back to so a rejection removes one picture rather than all of them.
    /// Returns null when there is nothing to fall back to. The file must still exist on disk:
    /// pointing acad_student at a thumbnail that was reclaimed long ago would be worse than blank.
    /// </summary>
    private string FindApprovedFallback(MySqlConnection conn, MySqlTransaction tx, string regno, int excludeId, string excludeFile)
    {
        using (var cmd = new MySqlCommand(
            "SELECT new_photofile FROM stud_photo_change " +
            "WHERE regno=@r AND status='APPROVED' AND id<>@id " +
            "  AND IFNULL(new_photofile,'')<>'' AND new_photofile<>@f " +
            "ORDER BY id DESC LIMIT 5", conn, tx))
        {
            cmd.Parameters.AddWithValue("@r", regno);
            cmd.Parameters.AddWithValue("@id", excludeId);
            cmd.Parameters.AddWithValue("@f", excludeFile ?? "");
            var candidates = new List<string>();
            using (var rd = cmd.ExecuteReader())
                while (rd.Read()) candidates.Add(Convert.ToString(rd[0]));
            foreach (string f in candidates)
                if (PhotoFileExists(f)) return f;
        }
        return null;
    }

    /// <summary>A stored filename that actually names a photograph. Blank and the "-" placeholder
    /// both mean there is none.</summary>
    private static bool HasPhoto(string file)
    {
        return !string.IsNullOrEmpty(file) && file.Trim() != "" && file.Trim() != "-";
    }

    /// <summary>True when the thumbnail is still on disk. Guards every restore.</summary>
    private bool PhotoFileExists(string file)
    {
        if (string.IsNullOrEmpty(file) || file == "-") return false;
        try
        {
            string path = Server.MapPath("~/COOPERP/StudentInfo/photos/" + file);
            return System.IO.File.Exists(path);
        }
        catch { return false; }
    }

    private const int BAN_AFTER_REJECTIONS = 3;

    /// <summary>
    /// Applies an indefinite photo-upload ban when the student should be banned: either the admin
    /// explicitly ticked "ban" on this rejection, or they have now reached BAN_AFTER_REJECTIONS
    /// rejections. A banned student cannot re-upload until an admin lifts the ban. Runs inside the
    /// caller's transaction; the stud_photo_change row must already be marked REJECTED so the count
    /// includes the current rejection.
    /// </summary>
    private void ApplyBanIfNeeded(MySqlConnection conn, MySqlTransaction tx, string regno, bool manualBan, string comment, string user)
    {
        // Count rejection OCCASIONS, not rejected rows.
        //
        // A student who uploads the same unusable photograph three times in five minutes, and has
        // all three cleared out in one sweep of the queue, used to be counted as three strikes and
        // banned instantly — by a single administrator action, before anyone had told them what
        // was wrong. MRU2026004812 is exactly that: three uploads at 21:12–21:17, all rejected at
        // 23:12, banned. That is not three chances; it is one.
        //
        // Rejections made in the same minute are therefore one occasion. Three separate sittings —
        // where the student has been told, uploaded again, and been refused again — still bans.
        int rej = 0;
        using (var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM (SELECT DISTINCT DATE_FORMAT(reviewed_at,'%Y-%m-%d %H:%i') occ " +
            "FROM stud_photo_change WHERE regno=@r AND status='REJECTED' AND reviewed_at IS NOT NULL) t", conn, tx))
        { cmd.Parameters.AddWithValue("@r", regno); rej = Convert.ToInt32(cmd.ExecuteScalar()); }

        bool autoBan = rej >= BAN_AFTER_REJECTIONS;
        if (!manualBan && !autoBan) return;

        string reason = manualBan
            ? ("Photo uploads suspended by the administrator." + (string.IsNullOrEmpty(comment) ? "" : (" Reason: " + comment)))
            : ("Photo uploads suspended automatically after " + rej + " rejections. Please visit the University IT Office / Help Desk with a perfect, professional passport-size studio photograph to have the ban lifted.");
        using (var cmd = new MySqlCommand(
            "UPDATE acad_student SET photo_banned=1, photo_ban_reason=@rz, photo_ban_at=NOW(), photo_ban_by=@by WHERE regno=@rn", conn, tx))
        {
            cmd.Parameters.AddWithValue("@rz", reason);
            cmd.Parameters.AddWithValue("@by", user);
            cmd.Parameters.AddWithValue("@rn", regno);
            cmd.ExecuteNonQuery();
        }
    }

    // ===================================================================
    // RESTORE A PREVIOUS VERSION
    // ===================================================================
    /// <summary>
    /// Puts an earlier photograph back as the student's official one.
    ///
    /// Until now a rejection was one-way: the picture was blanked and the only route back was the
    /// student uploading again — which a banned student cannot do, and which is absurd when a
    /// perfectly good earlier photograph is sitting in the history. Any version the student ever
    /// submitted can now be made live again, provided its file still exists.
    ///
    /// The restore is itself written to stud_photo_change as an APPROVED row attributed to the
    /// administrator, so the history reads forwards and the next rejection has something to fall
    /// back to. It does not lift a ban — that is a separate decision, deliberately.
    /// </summary>
    private string HandleRestoreVersion()
    {
        int id = SafeInt(Request.Form["id"], 0);
        string comment = (Request.Form["comment"] ?? "").Trim();
        if (id <= 0) return "{\"success\":false,\"message\":\"Missing record id.\"}";

        using (var conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();

            string regno = "", file = "", status = "";
            using (var cmd = new MySqlCommand(
                "SELECT regno, COALESCE(new_photofile,''), status FROM stud_photo_change WHERE id=@id LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) return "{\"success\":false,\"message\":\"That version was not found.\"}";
                    regno = rd.GetString(0); file = rd.GetString(1); status = rd.GetString(2).ToUpperInvariant();
                }
            }

            if (file == "" || file == "-")
                return "{\"success\":false,\"message\":\"That entry has no photograph to restore.\"}";
            if (!PhotoFileExists(file))
                return "{\"success\":false,\"message\":\"The image file for that version is no longer on the server, so it cannot be restored.\"}";

            string curPhoto = "";
            using (var cmd = new MySqlCommand("SELECT COALESCE(photofile,'') FROM acad_student WHERE regno=@r LIMIT 1", conn))
            { cmd.Parameters.AddWithValue("@r", regno); object o = cmd.ExecuteScalar(); curPhoto = o == null ? "" : Convert.ToString(o); }

            if (curPhoto == file)
                return "{\"success\":false,\"message\":\"That version is already the student's current photograph.\"}";

            string user = GetCurrentUser();
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    using (var cmd = new MySqlCommand(
                        "UPDATE acad_student SET photofile=@f, photo_status='APPROVED' WHERE regno=@r", conn, tx))
                    { cmd.Parameters.AddWithValue("@f", file); cmd.Parameters.AddWithValue("@r", regno); cmd.ExecuteNonQuery(); }

                    // The restored version becomes APPROVED in its own right, so it is a valid
                    // fallback next time and no longer reads as rejected in the history.
                    using (var cmd = new MySqlCommand(
                        "UPDATE stud_photo_change SET status='APPROVED', reviewed_by=@by, reviewed_at=NOW(), " +
                        " review_comment=CONCAT('Restored by administrator', CASE WHEN @c='' THEN '' ELSE CONCAT(' — ', @c) END) " +
                        "WHERE id=@id", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@by", user);
                        cmd.Parameters.AddWithValue("@c", comment);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = new MySqlCommand(
                        "INSERT INTO stud_photo_change (regno, old_photofile, new_photofile, status, source, requested_at, reviewed_by, reviewed_at, review_comment) " +
                        "VALUES (@r, @o, @n, 'APPROVED', 'eadmin-restore', NOW(), @by, NOW(), @c)", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@r", regno);
                        cmd.Parameters.AddWithValue("@o", curPhoto);
                        cmd.Parameters.AddWithValue("@n", file);
                        cmd.Parameters.AddWithValue("@by", user);
                        cmd.Parameters.AddWithValue("@c", "Reverted to an earlier photograph" + (comment == "" ? "." : " — " + comment));
                        cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
                catch { tx.Rollback(); throw; }
            }
            return "{\"success\":true,\"message\":\"Photograph reverted to the earlier version.\"}";
        }
    }

    /// <summary>Lifts a student's photo-upload ban so they can upload again (visited the office).</summary>
    private string HandleUnban()
    {
        string regno = (Request.Form["regno"] ?? "").Trim();
        if (regno == "") return "{\"success\":false,\"message\":\"Missing registration number.\"}";
        using (var conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            string name = "";
            using (var cmd = new MySqlCommand("SELECT TRIM(CONCAT(COALESCE(firstname,''),' ',COALESCE(othername,''))) FROM acad_student WHERE regno=@r LIMIT 1", conn))
            { cmd.Parameters.AddWithValue("@r", regno); object o = cmd.ExecuteScalar(); if (o == null) return "{\"success\":false,\"message\":\"No student found.\"}"; name = o == DBNull.Value ? "" : o.ToString().Trim(); }

            int rows;
            using (var cmd = new MySqlCommand(
                "UPDATE acad_student SET photo_banned=0, photo_ban_reason=NULL, photo_ban_at=NULL, photo_ban_by=NULL WHERE regno=@r", conn))
            { cmd.Parameters.AddWithValue("@r", regno); rows = cmd.ExecuteNonQuery(); }

            // Audit note.
            try
            {
                using (var cmd = new MySqlCommand(
                    "INSERT INTO stud_photo_change (regno, old_photofile, new_photofile, status, source, requested_at, reviewed_by, reviewed_at, review_comment) " +
                    "VALUES (@r,'','','UNBANNED','eadmin-admin',NOW(),@by,NOW(),'Photo-upload ban lifted by administrator.')", conn))
                { cmd.Parameters.AddWithValue("@r", regno); cmd.Parameters.AddWithValue("@by", GetCurrentUser()); cmd.ExecuteNonQuery(); }
            }
            catch { }

            string who = name == "" ? regno : name + " (" + regno + ")";
            return "{\"success\":true,\"message\":\"Ban lifted for " + JsEnc(who) + ". They can now upload a photograph again.\"}";
        }
    }

    /// <summary>Reviews a single PENDING change. Returns "OK" or a skip reason.</summary>
    private string ReviewOne(MySqlConnection conn, int id, bool approve, string comment, string user, bool ban)
    {
        string regno = "", newPhoto = "", status = "";
        using (var cmd = new MySqlCommand("SELECT regno, new_photofile, status FROM stud_photo_change WHERE id=@id LIMIT 1", conn))
        {
            cmd.Parameters.AddWithValue("@id", id);
            using (var rd = cmd.ExecuteReader())
            {
                if (!rd.Read()) return "Record not found.";
                regno = rd["regno"].ToString();
                newPhoto = rd["new_photofile"].ToString();
                status = rd["status"].ToString().ToUpperInvariant();
            }
        }
        if (status != "PENDING") return "Already " + status.ToLowerInvariant() + ".";

        // Files freed by an approval's purge of the student's other versions — cleaned up after commit.
        var orphanFiles = new List<string>();
        // How many of the student's other pending versions this rejection swept up with it.
        int alsoRejected = 0;
        using (var tx = conn.BeginTransaction())
        {
            try
            {
                using (var cmd = new MySqlCommand(
                    "UPDATE stud_photo_change SET status=@st, reviewed_by=@by, reviewed_at=NOW(), review_comment=@c WHERE id=@id AND status='PENDING'", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@st", approve ? "APPROVED" : "REJECTED");
                    cmd.Parameters.AddWithValue("@by", user);
                    cmd.Parameters.AddWithValue("@c", comment == "" ? (object)DBNull.Value : comment);
                    cmd.Parameters.AddWithValue("@id", id);
                    if (cmd.ExecuteNonQuery() == 0) { tx.Rollback(); return "Already reviewed."; }
                }

                if (approve)
                {
                    // The approved photograph becomes the student's official, live photo. Admin has
                    // explicitly chosen this version, so it overrides any later upload.
                    string setSql = newPhoto != ""
                        ? "UPDATE acad_student SET photo_status='APPROVED', photofile=@p WHERE regno=@r"
                        : "UPDATE acad_student SET photo_status='APPROVED' WHERE regno=@r";
                    using (var cmd = new MySqlCommand(setSql, conn, tx))
                    { cmd.Parameters.AddWithValue("@r", regno); if (newPhoto != "") cmd.Parameters.AddWithValue("@p", newPhoto); cmd.ExecuteNonQuery(); }

                    // Approving one photo clears the student's other submitted versions from the queue.
                    orphanFiles = PurgeOtherVersions(conn, tx, regno, id, user);
                }
                else
                {
                    // Rejecting one version rejects EVERY pending version this student has
                    // submitted, exactly as approving one clears the others. A student who
                    // uploads three attempts at the same unusable photograph is making one
                    // submission, not three, and an administrator should settle it once rather
                    // than opening the same card three times.
                    //
                    // Only PENDING versions are swept. An older APPROVED photograph is left
                    // alone deliberately — it is the one the student falls back to below.
                    //
                    // The files are captured BEFORE the sweep, because the student's live photo
                    // may be any one of them, not necessarily the version the administrator
                    // clicked. Without this, rejecting version B while version A is the live one
                    // would reject A on paper and leave it on display.
                    var rejectedFiles = new List<string>();
                    if (HasPhoto(newPhoto)) rejectedFiles.Add(newPhoto);
                    using (var cmd = new MySqlCommand(
                        "SELECT COALESCE(new_photofile,'') FROM stud_photo_change " +
                        "WHERE regno=@r AND id<>@id AND status='PENDING'", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@r", regno);
                        cmd.Parameters.AddWithValue("@id", id);
                        using (var rd = cmd.ExecuteReader())
                            while (rd.Read()) { string f = rd.GetString(0); if (HasPhoto(f)) rejectedFiles.Add(f); }
                    }

                    using (var cmd = new MySqlCommand(
                        "UPDATE stud_photo_change SET status='REJECTED', reviewed_by=@by, reviewed_at=NOW(), " +
                        " review_comment=CASE WHEN @c='' THEN 'Rejected together with the other photographs submitted at the same time.' " +
                        "                     ELSE CONCAT(@c, ' (rejected together with the other photographs submitted at the same time.)') END " +
                        "WHERE regno=@r AND id<>@id AND status='PENDING'", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@by", user);
                        cmd.Parameters.AddWithValue("@c", comment);
                        cmd.Parameters.AddWithValue("@r", regno);
                        cmd.Parameters.AddWithValue("@id", id);
                        alsoRejected = cmd.ExecuteNonQuery();
                    }

                    // The student is left with the most recent photograph they had APPROVED, if
                    // there is one — the "revert to the older version" this page now supports,
                    // applied automatically. Only when there is nothing to fall back to is the
                    // student left with no picture.
                    string restore = FindApprovedFallback(conn, tx, regno, id, newPhoto);

                    // Replace the live photograph only if it is one of the ones just rejected.
                    // A student whose live photo is an older approved one keeps it untouched.
                    var live = new StringBuilder();
                    var lp = new List<MySqlParameter>();
                    for (int k = 0; k < rejectedFiles.Count; k++)
                    {
                        if (k > 0) live.Append(",");
                        live.Append("@f").Append(k);
                        lp.Add(new MySqlParameter("@f" + k, rejectedFiles[k]));
                    }
                    if (live.Length > 0)
                    {
                        string sql = restore != null
                            ? "UPDATE acad_student SET photo_status='APPROVED', photofile=@keep WHERE regno=@r AND photofile IN (" + live + ")"
                            : "UPDATE acad_student SET photo_status='REJECTED', photofile='' WHERE regno=@r AND photofile IN (" + live + ")";
                        using (var cmd = new MySqlCommand(sql, conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@r", regno);
                            if (restore != null) cmd.Parameters.AddWithValue("@keep", restore);
                            foreach (var p in lp) cmd.Parameters.Add(new MySqlParameter(p.ParameterName, p.Value));
                            cmd.ExecuteNonQuery();
                        }
                    }
                    // Auto/ manual ban after repeated rejections (stud_photo_change is already REJECTED above).
                    ApplyBanIfNeeded(conn, tx, regno, ban, comment, user);
                }
                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        // Reclaim the superseded thumbnails now the transaction is committed. The approved
        // photo (newPhoto) is the live one, so TryDeletePhotoFile keeps it and any still-referenced file.
        foreach (string f in orphanFiles) TryDeletePhotoFile(conn, f, newPhoto);
        // The caller reports how many other pending versions went with this decision.
        return alsoRejected > 0 ? "OK+" + alsoRejected : "OK";
    }

    /// <summary>
    /// Marks every OTHER (non-DELETED) change row for the student as DELETED because one of their
    /// photographs has just been approved. Returns the files those rows pointed at so the caller can
    /// clean up any that become orphaned. Runs inside the caller's transaction.
    /// </summary>
    private List<string> PurgeOtherVersions(MySqlConnection conn, MySqlTransaction tx, string regno, int keepId, string user)
    {
        var files = new List<string>();
        using (var cmd = new MySqlCommand(
            "SELECT COALESCE(new_photofile,'') FROM stud_photo_change WHERE regno=@r AND id<>@id AND status<>'DELETED'", conn, tx))
        {
            cmd.Parameters.AddWithValue("@r", regno); cmd.Parameters.AddWithValue("@id", keepId);
            using (var rd = cmd.ExecuteReader())
                while (rd.Read()) { string f = rd.GetString(0); if (f != "") files.Add(f); }
        }
        using (var cmd = new MySqlCommand(
            "UPDATE stud_photo_change SET status='DELETED', reviewed_by=@by, reviewed_at=NOW(), " +
            "review_comment='Superseded — another photograph was approved for this student.' " +
            "WHERE regno=@r AND id<>@id AND status<>'DELETED'", conn, tx))
        {
            cmd.Parameters.AddWithValue("@by", user);
            cmd.Parameters.AddWithValue("@r", regno);
            cmd.Parameters.AddWithValue("@id", keepId);
            cmd.ExecuteNonQuery();
        }
        return files;
    }

    // ===================================================================
    // LIST
    // ===================================================================
    /// <summary>
    /// Appends the free-text search predicate, tokenised on whitespace: every token must
    /// appear in at least one of the student number, registration number, or either name
    /// part. Tokens are ANDed across, ORed within — so extra tokens narrow the result and
    /// never widen it.
    ///
    /// Two things this fixes beyond the plumbing:
    ///   * acad_student.entryno (the STUDENT NUMBER, e.g. 24/U/BPLM/0011/K/DAY) was not
    ///     searched at all, so searching by student ID could never return anything.
    ///   * names are stored firstname-then-othername, but people type the surname first;
    ///     matching per token rather than against the concatenated string makes the order
    ///     and any stray spacing irrelevant.
    ///
    /// <paramref name="regnoCol"/> differs by branch: the banned list reads acad_student
    /// directly, the queue reads stud_photo_change joined to it.
    /// </summary>
    private static void AppendSearch(StringBuilder w, List<MySqlParameter> pr, string q, string regnoCol)
    {
        if (string.IsNullOrEmpty(q)) return;
        string[] tokens = q.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        int used = 0;
        for (int i = 0; i < tokens.Length && used < 6; i++)
        {
            string tok = tokens[i].Trim();
            if (tok.Length == 0) continue;
            string p = "@q" + used;
            w.Append(" AND (").Append(regnoCol).Append(" LIKE ").Append(p)
             .Append(" OR COALESCE(s.entryno,'') LIKE ").Append(p)
             .Append(" OR COALESCE(s.firstname,'') LIKE ").Append(p)
             .Append(" OR COALESCE(s.othername,'') LIKE ").Append(p)
             .Append(")");
            pr.Add(new MySqlParameter(p, "%" + tok + "%"));
            used++;
        }
    }

    // ===================================================================
    // BULK ID-CARD REQUESTS  --  scan, review, then place
    //
    // A student whose photograph has been approved may ask for an ID card from the
    // portal, and most never do: the request is the last step of a journey they have
    // already stopped paying attention to. This places those requests for them, from
    // the desk that just approved the photographs.
    //
    // Deliberately three steps, never one button:
    //
    //   scan    a read-only query answering "who qualifies" -- and, just as
    //           importantly, "who does not, and why". The three skip buckets are
    //           returned beside the answer, because a bare "480 qualify" is
    //           indistinguishable from a broken filter until you can see the rest of
    //           the intake accounted for.
    //   review  every candidate is listed with a tick, and the operator can untick
    //           anyone. Nothing has been written at this point.
    //   place   the ticked students only, in chunks, through the SAME
    //           IDCardService.CreateRequest the student wizard calls. A bulk-placed
    //           request is therefore an ordinary request: its own request number, its
    //           own event trail, the same eligibility gate. There is no second code
    //           path to keep in step with the first.
    //
    // The list the browser sends back is NEVER trusted. Every registration number is
    // re-tested against the same qualification rule at the moment of writing, so a
    // stale tab, a double click or a hand-edited request cannot create a card request
    // for an alumnus, for somebody whose photograph is not approved, or for somebody
    // who already asked.
    // ===================================================================

    private const int IDCARD_MIN_YEAR_DEFAULT = 2026;
    private const int IDCARD_PREVIEW_CAP = 1500;   // the list is reviewed by eye; beyond this it is not
    private const int IDCARD_PLACE_CHUNK = 60;     // one HTTP request's worth, so progress stays honest

    /// <summary>
    /// The single definition of "qualifies for a card and has not asked". Used by the
    /// scan to build the list AND again, unchanged, to re-check every write. One
    /// string, so the preview can never promise something the commit does not apply.
    /// </summary>
    private const string IDCARD_QUALIFY_SQL =
        " COALESCE(s.entryyear,0) >= @y " +
        " AND UPPER(TRIM(COALESCE(s.photo_status,''))) = 'APPROVED' " +
        " AND COALESCE(TRIM(s.photofile),'') NOT IN ('','-') " +
        " AND UPPER(TRIM(COALESCE(s.new_status,''))) <> 'ALUMNI' " +
        " AND NOT EXISTS (SELECT 1 FROM idcard_requests r WHERE r.regno = s.regno " +
        "                   AND r.status NOT IN ('REQUESTED','CANCELLED')) ";
    //
    // "Has no request at all" was the wrong test. Creating a request leaves it at
    // REQUESTED -- which every screen now calls "Not submitted" -- and it only reaches
    // SUBMITTED when the requester submits it. Placing a batch therefore produced a pile
    // of unsubmitted drafts that nobody was ever going to finish, and they did not appear
    // under Submitted because they were not submitted.
    //
    // So a student still qualifies while their only request is a draft: the placement
    // submits the one they have instead of creating a second. CANCELLED is likewise not a
    // request -- it was withdrawn and a fresh one is an ordinary new card, which is exactly
    // how IDCardService.CheckEligibility treats it.
    //
    // r.regno = s.regno, NOT TRIM(r.regno) = TRIM(s.regno). Wrapping the indexed column
    // in TRIM() makes ix_regno unusable and turns the EXISTS into a scan of every request
    // per student: measured at 2.28s against 0.050s, 45x. Both columns are utf8_general_ci,
    // which is PAD SPACE, so = already ignores trailing spaces -- and the two forms were
    // checked against each other over the whole student table: 274 = 274, zero rows
    // disagreeing. Same answer, 45x cheaper.

    /// <summary>Who qualifies, who does not and why, plus the list itself.</summary>
    private string HandleIdCardScan()
    {
        int minYear = SafeInt(Request.QueryString["minyear"] ?? Request.Form["minyear"], IDCARD_MIN_YEAR_DEFAULT);
        if (minYear < 2000 || minYear > 2100) minYear = IDCARD_MIN_YEAR_DEFAULT;

        int qualify = 0, already = 0, noPhoto = 0, alumni = 0, drafts = 0;
        var rows = new StringBuilder();
        int listed = 0;
        string windowLabel = "";
        bool windowOpen = false;

        using (var conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();

            // One pass over the intake, bucketed in the order the rules are applied.
            using (var cmd = new MySqlCommand(
                "SELECT CASE " +
                "  WHEN UPPER(TRIM(COALESCE(s.new_status,''))) = 'ALUMNI' THEN 'alumni' " +
                "  WHEN UPPER(TRIM(COALESCE(s.photo_status,''))) <> 'APPROVED' " +
                "    OR COALESCE(TRIM(s.photofile),'') IN ('','-') THEN 'nophoto' " +
                "  WHEN EXISTS (SELECT 1 FROM idcard_requests r WHERE r.regno = s.regno " +
"                 AND r.status NOT IN ('REQUESTED','CANCELLED')) THEN 'already' " +
                "  ELSE 'qualify' END AS g_bucket, COUNT(*) AS n " +
                "FROM acad_student s WHERE COALESCE(s.entryyear,0) >= @y GROUP BY g_bucket", conn))
            {
                cmd.Parameters.AddWithValue("@y", minYear);
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read())
                    {
                        string b = Convert.ToString(rd[0]);
                        int n = Convert.ToInt32(rd[1]);
                        if (b == "qualify") qualify = n;
                        else if (b == "already") already = n;
                        else if (b == "nophoto") noPhoto = n;
                        else if (b == "alumni") alumni = n;
                    }
            }

            using (var cmd = new MySqlCommand(
                "SELECT s.regno, " +
                "       TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) AS name, " +
                "       COALESCE(s.progid,'') AS prog, COALESCE(p.progname,'') AS progname, " +
                "       COALESCE(s.entryyear,0) AS ey, UPPER(TRIM(COALESCE(s.new_status,''))) AS st, " +
                "       COALESCE(s.photofile,'') AS pf, " +
                "       COALESCE((SELECT r2.request_no FROM idcard_requests r2 " +
                "                 WHERE r2.regno = s.regno AND r2.status = 'REQUESTED' " +
                "                 ORDER BY r2.id DESC LIMIT 1),'') AS draft_no " +
                "FROM acad_student s LEFT JOIN acad_programme p ON p.progcode = s.progid " +
                "WHERE " + IDCARD_QUALIFY_SQL +
                "ORDER BY s.entryyear DESC, s.regno LIMIT " + IDCARD_PREVIEW_CAP, conn))
            {
                cmd.Parameters.AddWithValue("@y", minYear);
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read())
                    {
                        if (listed > 0) rows.Append(",");
                        rows.Append("{\"regno\":\"").Append(JsEnc(Safe(rd, "regno")))
                            .Append("\",\"name\":\"").Append(JsEnc(Safe(rd, "name")))
                            .Append("\",\"prog\":\"").Append(JsEnc(Safe(rd, "prog")))
                            .Append("\",\"progname\":\"").Append(JsEnc(Safe(rd, "progname")))
                            .Append("\",\"year\":\"").Append(JsEnc(Safe(rd, "ey")))
                            .Append("\",\"status\":\"").Append(JsEnc(Safe(rd, "st")))
                            .Append("\",\"photo\":\"").Append(JsEnc(Safe(rd, "pf")))
                            .Append("\",\"draft\":\"").Append(JsEnc(Safe(rd, "draft_no")))
                            .Append("\"}");
                        if (Safe(rd, "draft_no") != "") drafts++;
                        listed++;
                    }
            }
        }

        // The request window gates STUDENTS, not this screen -- an operator placing a
        // request on somebody's behalf is not turned away by it. It is reported anyway,
        // because "the window shut yesterday" is exactly the sort of thing worth knowing
        // before creating four hundred requests.
        try
        {
            using (var c = new MySqlConnection(IDCardService.ConnStr))
            {
                c.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT id, closes_at, (NOW() BETWEEN opens_at AND closes_at) AS open_now " +
                    "FROM idcard_windows WHERE is_active = 1 " +
                    "  AND UPPER(COALESCE(requester_scope,'BOTH')) IN ('BOTH','STUDENT') " +
                    "ORDER BY open_now DESC, id DESC LIMIT 1", c))
                using (var rd = cmd.ExecuteReader())
                {
                    if (rd.Read())
                    {
                        windowOpen = Convert.ToInt32(rd[2]) == 1;
                        DateTime closes = Convert.ToDateTime(rd[1]);
                        windowLabel = windowOpen
                            ? "Student requests are open until " + closes.ToString("d MMM yyyy")
                            : "The student request window closed on " + closes.ToString("d MMM yyyy");
                    }
                    else { windowOpen = true; windowLabel = "No request window is set, so nothing is gating requests."; }
                }
            }
        }
        catch { windowLabel = ""; }

        var sb = new StringBuilder();
        sb.Append("{\"success\":true,\"minYear\":").Append(minYear)
          .Append(",\"qualify\":").Append(qualify)
          .Append(",\"already\":").Append(already)
          .Append(",\"noPhoto\":").Append(noPhoto)
          .Append(",\"alumni\":").Append(alumni)
          .Append(",\"listed\":").Append(listed)
          .Append(",\"drafts\":").Append(drafts)
          .Append(",\"capped\":").Append(qualify > listed ? "true" : "false")
          .Append(",\"windowOpen\":").Append(windowOpen ? "true" : "false")
          .Append(",\"windowLabel\":\"").Append(JsEnc(windowLabel)).Append("\"")
          .Append(",\"photoBase\":\"").Append(JsEnc(PHOTO_BASE)).Append("\"")
          .Append(",\"rows\":[").Append(rows).Append("]}");
        return sb.ToString();
    }

    /// <summary>
    /// Places requests for the ticked students, after re-proving every one of them.
    /// Chunked by the caller; this handler is deliberately small and re-entrant, so a
    /// dropped connection halfway through costs at most one chunk and never a
    /// half-written batch -- each request is its own committed unit anyway.
    /// </summary>
    private string HandleIdCardPlace()
    {
        int minYear = SafeInt(Request.Form["minyear"], IDCARD_MIN_YEAR_DEFAULT);
        if (minYear < 2000 || minYear > 2100) minYear = IDCARD_MIN_YEAR_DEFAULT;

        var wanted = new List<string>();
        foreach (string part in (Request.Form["regnos"] ?? "").Split(','))
        {
            string r = part.Trim();
            if (r != "" && !wanted.Contains(r)) wanted.Add(r);
        }
        if (wanted.Count == 0) return "{\"success\":false,\"message\":\"Nothing was selected.\"}";
        if (wanted.Count > IDCARD_PLACE_CHUNK)
            return "{\"success\":false,\"message\":\"Too many in one go. Send " + IDCARD_PLACE_CHUNK + " or fewer.\"}";

        // Re-prove every one of them against the same rule the scan used. The browser
        // is a source of intent, never of authority.
        var okPhoto = new Dictionary<string, string>();
        using (var conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            var names = new StringBuilder();
            for (int i = 0; i < wanted.Count; i++) { if (i > 0) names.Append(","); names.Append("@r").Append(i); }
            using (var cmd = new MySqlCommand(
                "SELECT s.regno, COALESCE(s.photofile,'') AS pf FROM acad_student s " +
                "WHERE s.regno IN (" + names + ") AND " + IDCARD_QUALIFY_SQL, conn))
            {
                cmd.Parameters.AddWithValue("@y", minYear);
                for (int i = 0; i < wanted.Count; i++) cmd.Parameters.AddWithValue("@r" + i, wanted[i]);
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read()) okPhoto[Safe(rd, "regno")] = Safe(rd, "pf");
            }
        }

        int windowId = CurrentIdCardWindowId();
        string actor = GetCurrentUser();
        string note = "Placed by the ID office on the student's behalf (bulk).";

        int submitted = 0, blocked = 0, skipped = 0, failed = 0;
        var detail = new StringBuilder();
        foreach (string regno in wanted)
        {
            string outcome = "", requestNo = "";
            if (!okPhoto.ContainsKey(regno))
            {
                // No longer qualifies: somebody submitted in the meantime, the photograph
                // was pulled, or the row never qualified at all.
                skipped++; outcome = "skipped";
            }
            else
            {
                // A student may already be holding an unsubmitted draft -- their own, or one
                // from an earlier batch. Submit that one rather than creating a second, which
                // CheckEligibility would refuse anyway.
                requestNo = ExistingDraftNo(regno);
                if (requestNo == "")
                {
                    var res = IDCardService.CreateRequest("STUDENT", regno, 0, "NEW",
                                                          okPhoto[regno], true, true, windowId, actor);
                    if (res.Ok) { requestNo = res.RequestNo ?? ""; StampRequestNote(requestNo, note); }
                    else { failed++; outcome = res.Message ?? "could not be created"; }
                }

                if (outcome == "")
                {
                    // The same call the student's own Submit button makes: it runs the photo
                    // gate, runs the fee check, writes the snapshot, and lands the request on
                    // SUBMITTED or BLOCKED. Going straight to SUBMITTED is legal for staff and
                    // would have been one line shorter -- and would have quietly waved four
                    // hundred students past the fee gate their classmates have to clear.
                    IDCardService.SubmitJson(requestNo, null, null, null, null, actor);

                    string now = StatusOf(requestNo);
                    if (now == "SUBMITTED") { submitted++; outcome = "submitted"; }
                    else if (now == "BLOCKED") { blocked++; outcome = "blocked"; }
                    else { failed++; outcome = "stopped at " + IDCardService.StatusLabel(now); }
                }
            }
            if (detail.Length > 0) detail.Append(",");
            detail.Append("{\"regno\":\"").Append(JsEnc(regno))
                  .Append("\",\"outcome\":\"").Append(JsEnc(outcome))
                  .Append("\",\"requestNo\":\"").Append(JsEnc(requestNo)).Append("\"}");
        }

        return "{\"success\":true,\"submitted\":" + submitted + ",\"blocked\":" + blocked +
               ",\"skipped\":" + skipped + ",\"failed\":" + failed +
               ",\"detail\":[" + detail + "]}";
    }

    /// <summary>This student's newest unsubmitted request, or "".</summary>
    private string ExistingDraftNo(string regno)
    {
        using (var c = new MySqlConnection(IDCardService.ConnStr))
        {
            c.Open();
            using (var cmd = new MySqlCommand(
                "SELECT request_no FROM idcard_requests WHERE regno = @r AND status = 'REQUESTED' " +
                "ORDER BY id DESC LIMIT 1", c))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                object v = cmd.ExecuteScalar();
                return (v == null || v == DBNull.Value) ? "" : v.ToString();
            }
        }
    }

    /// <summary>Where a request actually ended up, read back rather than assumed.</summary>
    private string StatusOf(string requestNo)
    {
        if (string.IsNullOrEmpty(requestNo)) return "";
        using (var c = new MySqlConnection(IDCardService.ConnStr))
        {
            c.Open();
            using (var cmd = new MySqlCommand("SELECT status FROM idcard_requests WHERE request_no = @rn LIMIT 1", c))
            {
                cmd.Parameters.AddWithValue("@rn", requestNo);
                object v = cmd.ExecuteScalar();
                return (v == null || v == DBNull.Value) ? "" : v.ToString().ToUpperInvariant();
            }
        }
    }    /// <summary>The open student window, or 0. Never throws: the window is informational here.</summary>
    private int CurrentIdCardWindowId()
    {
        try
        {
            using (var c = new MySqlConnection(IDCardService.ConnStr))
            {
                c.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT id FROM idcard_windows WHERE is_active = 1 " +
                    "  AND NOW() BETWEEN opens_at AND closes_at " +
                    "  AND UPPER(COALESCE(requester_scope,'BOTH')) IN ('BOTH','STUDENT') " +
                    "ORDER BY id DESC LIMIT 1", c))
                {
                    object v = cmd.ExecuteScalar();
                    return (v == null || v == DBNull.Value) ? 0 : Convert.ToInt32(v);
                }
            }
        }
        catch { return 0; }
    }

    /// <summary>
    /// Records on the request itself that the office placed it, not the student.
    /// Written after creation rather than through CreateRequest, because that method is
    /// shared code duplicated byte-for-byte with the portal and is not ours to fork.
    /// </summary>
    private void StampRequestNote(string requestNo, string note)
    {
        if (string.IsNullOrEmpty(requestNo)) return;
        try
        {
            using (var c = new MySqlConnection(IDCardService.ConnStr))
            {
                c.Open();
                using (var cmd = new MySqlCommand(
                    "UPDATE idcard_requests SET notes = @n, updated_at = NOW() WHERE request_no = @rn", c))
                {
                    cmd.Parameters.AddWithValue("@n", note);
                    cmd.Parameters.AddWithValue("@rn", requestNo);
                    cmd.ExecuteNonQuery();
                }
            }
        }
        catch { /* the note is a courtesy; never fail a placed request over it */ }
    }    private string BuildList()
    {
        string status = (Request.QueryString["status"] ?? "PENDING").Trim().ToUpperInvariant();
        if (status != "ALL" && status != "PENDING" && status != "APPROVED" && status != "REJECTED" && status != "DELETED" && status != "BANNED") status = "PENDING";
        string q = (Request.QueryString["q"] ?? "").Trim();
        int page = SafeInt(Request.QueryString["page"], 1); if (page < 1) page = 1;
        bool bannedView = (status == "BANNED");

        int total = 0, pendingCount = 0, bannedCount = 0;
        var rows = new List<Dictionary<string, string>>();
        using (var conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM stud_photo_change WHERE status='PENDING'", conn))
                pendingCount = Convert.ToInt32(cmd.ExecuteScalar());
            using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM acad_student WHERE photo_banned=1", conn))
                bannedCount = Convert.ToInt32(cmd.ExecuteScalar());

            var pr = new List<MySqlParameter>();
            if (bannedView)
            {
                // Banned students live on acad_student (not stud_photo_change).
                var w = new StringBuilder("WHERE s.photo_banned=1");
                AppendSearch(w, pr, q, "s.regno");
                using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM acad_student s " + w, conn))
                { foreach (var p in pr) cmd.Parameters.Add(Clone(p)); total = Convert.ToInt32(cmd.ExecuteScalar()); }
                int pgs = Math.Max(1, (int)Math.Ceiling(total / (double)PAGE_SIZE)); if (page > pgs) page = pgs; int off = (page - 1) * PAGE_SIZE;
                string sql = "SELECT s.regno, TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) AS name, COALESCE(s.progid,'') AS prog, " +
                             "COALESCE(s.photo_ban_reason,'') AS ban_reason, s.photo_ban_at AS ban_at, COALESCE(s.photo_ban_by,'') AS ban_by, " +
                             "(SELECT COUNT(*) FROM stud_photo_change c WHERE c.regno=s.regno AND c.status='REJECTED') AS rej_count, " +
                             "COALESCE((SELECT c2.new_photofile FROM stud_photo_change c2 WHERE c2.regno=s.regno AND c2.new_photofile<>'' ORDER BY c2.id DESC LIMIT 1),'') AS last_photo " +
                             "FROM acad_student s " + w + " ORDER BY s.photo_ban_at DESC LIMIT " + off + "," + PAGE_SIZE;
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    foreach (var p in pr) cmd.Parameters.Add(Clone(p));
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read())
                        {
                            var d = new Dictionary<string, string>();
                            d["regno"] = Safe(rd, "regno"); d["name"] = Safe(rd, "name"); d["prog"] = Safe(rd, "prog");
                            d["ban_reason"] = Safe(rd, "ban_reason"); d["ban_by"] = Safe(rd, "ban_by");
                            d["ban_at"] = rd["ban_at"] == DBNull.Value ? "" : Convert.ToDateTime(rd["ban_at"]).ToString("dd MMM yyyy HH:mm");
                            d["rej_count"] = Safe(rd, "rej_count"); d["last_photo"] = Safe(rd, "last_photo");
                            rows.Add(d);
                        }
                }
            }
            else
            {
                var where = new StringBuilder("WHERE 1=1");
                if (status != "ALL") { where.Append(" AND c.status=@st"); pr.Add(new MySqlParameter("@st", status)); }
                AppendSearch(where, pr, q, "c.regno");
                using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM stud_photo_change c LEFT JOIN acad_student s ON s.regno=c.regno " + where, conn))
                { foreach (var p in pr) cmd.Parameters.Add(Clone(p)); total = Convert.ToInt32(cmd.ExecuteScalar()); }
                int pgs = Math.Max(1, (int)Math.Ceiling(total / (double)PAGE_SIZE)); if (page > pgs) page = pgs; int off = (page - 1) * PAGE_SIZE;
                string sql = "SELECT c.id, c.regno, TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) AS name, " +
                             "COALESCE(s.progid,'') AS prog, COALESCE(c.old_photofile,'') AS oldf, c.new_photofile AS newf, c.status, " +
                             "COALESCE(c.source,'') AS source, c.requested_at, COALESCE(c.reviewed_by,'') AS reviewed_by, c.reviewed_at, " +
                             "COALESCE(c.review_comment,'') AS review_comment, COALESCE(s.photo_banned,0) AS banned, " +
                             "CASE WHEN c.new_photofile<>'' AND c.new_photofile=s.photofile THEN 1 ELSE 0 END AS is_live " +
                             "FROM stud_photo_change c LEFT JOIN acad_student s ON s.regno=c.regno " + where +
                             " ORDER BY (c.status='PENDING') DESC, c.id DESC LIMIT " + off + "," + PAGE_SIZE;
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    foreach (var p in pr) cmd.Parameters.Add(Clone(p));
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read())
                        {
                            var d = new Dictionary<string, string>();
                            d["id"] = rd["id"].ToString(); d["regno"] = Safe(rd, "regno"); d["name"] = Safe(rd, "name");
                            d["prog"] = Safe(rd, "prog"); d["oldf"] = Safe(rd, "oldf"); d["newf"] = Safe(rd, "newf");
                            d["status"] = Safe(rd, "status");
                            d["requested_at"] = rd["requested_at"] == DBNull.Value ? "" : Convert.ToDateTime(rd["requested_at"]).ToString("dd MMM yyyy HH:mm");
                            d["reviewed_by"] = Safe(rd, "reviewed_by");
                            d["reviewed_at"] = rd["reviewed_at"] == DBNull.Value ? "" : Convert.ToDateTime(rd["reviewed_at"]).ToString("dd MMM yyyy HH:mm");
                            d["comment"] = Safe(rd, "review_comment"); d["banned"] = Safe(rd, "banned");
                            d["is_live"] = Safe(rd, "is_live");
                            rows.Add(d);
                        }
                }
            }

            int pages = Math.Max(1, (int)Math.Ceiling(total / (double)PAGE_SIZE));

            var sb = new StringBuilder();
            // Filter bar
            sb.Append("<div class='pc-bar'>");
            sb.Append("<div class='pc-tabs'>");
            sb.Append(Tab("PENDING", "Pending", status, q, pendingCount));
            sb.Append(Tab("APPROVED", "Approved", status, q, -1));
            sb.Append(Tab("REJECTED", "Rejected", status, q, -1));
            sb.Append(Tab("BANNED", "Banned", status, q, bannedCount));
            sb.Append(Tab("DELETED", "Deleted", status, q, -1));
            sb.Append(Tab("ALL", "All", status, q, -1));
            sb.Append("</div>");
            sb.Append("<div class='pc-bar__right'>");
            // Deliberately NOT a <form>. This markup is emitted inside the master page's
            // <form id="form1" runat="server">, and HTML does not allow nested forms: the
            // browser silently drops the inner <form> tag, so its inputs join the OUTER
            // ASP.NET form and the button (which had no type, so defaulted to submit) posted
            // that form back to the same URL. The typed term went out as a POST field while
            // BuildList reads Request.QueryString["q"] — which is why the box appeared to
            // clear itself and nothing was ever filtered.
            //
            // pcSearch() instead navigates to a real query-string URL, so search is a genuine
            // GET: bookmarkable, shareable, and correct on Back.
            sb.Append("<div class='pc-search'>" +
                      "<input type='hidden' id='pcStatus' value='" + HE(status) + "'/>" +
                      "<input type='text' id='pcQ' value='" + HE(q) + "' placeholder='Search student no, reg no or name...' class='pc-search__in' autocomplete='off'/>" +
                      "<button type='button' class='pc-btn pc-btn--sm' onclick='pcSearch()'>Search</button>" +
                      (q != "" ? "<a class='pc-clear' href='PhotoChangeController.aspx?status=" + HE(status) + "'>clear</a>" : "") +
                      "</div>");
            sb.Append("<button type='button' class='pc-btn pc-btn--up' onclick='pcOpenUp()' title='Put a photograph on a student&rsquo;s record for them'>" + CameraIcon() + "Upload a photo for a student</button>");
            // Offered on the Approved tab only. That is where the operator has just
            // finished settling photographs, and an approved photograph is the single
            // thing standing between a student and a card they never asked for.
            if (status == "APPROVED")
                sb.Append("<button type='button' class='pc-btn pc-btn--idc' onclick='pcOpenIdc()' title='Place ID-card requests for students who qualify but never asked'>" + CardIcon() + "Place ID-card requests</button>");
            sb.Append("<button type='button' class='pc-btn pc-btn--nav' onclick='pcOpenInit()'>&#43; Set a student&rsquo;s photograph status</button>");
            sb.Append("</div>");
            sb.Append("</div>");

            // Batch action bar (pending review only — not for the banned list)
            if (!bannedView)
            {
                sb.Append("<div class='pc-batch' id='pcBatch'>");
                sb.Append("<label class='pc-selall'><input type='checkbox' id='pcAll' onclick='pcToggleAll(this)'/> Select all on this page</label>");
                sb.Append("<span class='pc-batch__spacer'></span>");
                sb.Append("<span id='pcSelCount' class='pc-selcount'>0 selected</span>");
                sb.Append("<button type='button' class='pc-btn pc-btn--ok' onclick='pcBatch(true)'>Approve selected</button>");
                sb.Append("<button type='button' class='pc-btn pc-btn--danger' onclick='pcBatch(false)'>Reject selected</button>");
                sb.Append("<button type='button' class='pc-btn pc-btn--del' onclick='pcDeleteBatch()' title='Remove the selected versions from the queue (a live photo is skipped)'>Delete selected</button>");
                sb.Append("</div>");
            }

            if (rows.Count == 0)
            {
                sb.Append("<div class='pc-empty'>" + (bannedView ? "No banned students." : "No photo changes match this filter.") + "</div>");
                return sb.ToString();
            }

            sb.Append("<div class='pc-grid'>");
            foreach (var d in rows) sb.Append(bannedView ? BannedCard(d) : Card(d));
            sb.Append("</div>");

            // Pager
            if (pages > 1)
            {
                sb.Append("<div class='pc-pager'>");
                sb.AppendFormat("<span>{0} record{1} &middot; page {2} of {3}</span>", total, total == 1 ? "" : "s", page, pages);
                string baseUrl = "PhotoChangeController.aspx?status=" + HE(status) + (q != "" ? "&q=" + HttpUtility.UrlEncode(q) : "") + "&page=";
                sb.Append("<span class='pc-pager__btns'>");
                sb.Append("<a class='pc-pg " + (page <= 1 ? "off" : "") + "' href='" + baseUrl + (page - 1) + "'>&lsaquo; Prev</a>");
                sb.Append("<a class='pc-pg " + (page >= pages ? "off" : "") + "' href='" + baseUrl + (page + 1) + "'>Next &rsaquo;</a>");
                sb.Append("</span></div>");
            }
            return sb.ToString();
        }
    }

    private string Card(Dictionary<string, string> d)
    {
        string st = d["status"].ToUpperInvariant();
        bool banned = d.ContainsKey("banned") && d["banned"] == "1";
        bool live = d.ContainsKey("is_live") && d["is_live"] == "1";
        string badge = "<span class='pc-st pc-st--" + st.ToLowerInvariant() + "'>" + HE(d["status"]) + "</span>";
        if (live) badge += "<span class='pc-st pc-st--live' title='This is the student&#39;s current live photograph — approve or reject it, it cannot be deleted'>CURRENT</span>";
        if (banned) badge += "<span class='pc-st pc-st--banned' title='This student cannot re-upload until an admin lifts the ban'>BANNED</span>";
        // "-" is this system's placeholder for "no photograph" and appears on 407 of these rows.
        // Testing only for "" turned each one into <img src=".../-">, which is a 404 per card.
        string newUrl = HasPhoto(d["newf"]) ? PHOTO_BASE + Uri.EscapeDataString(d["newf"]) : "";
        string oldUrl = HasPhoto(d["oldf"]) ? PHOTO_BASE + Uri.EscapeDataString(d["oldf"]) : "";
        bool pending = st == "PENDING";
        bool deletable = st != "DELETED" && !live;

        var sb = new StringBuilder();
        sb.Append("<div class='pc-card' data-id='" + HE(d["id"]) + "'>");
        // header
        sb.Append("<div class='pc-card__h'>");
        if (pending) sb.Append("<input type='checkbox' class='pc-chk' value='" + HE(d["id"]) + "' onclick='pcCount()'/>");
        sb.Append("<div class='pc-card__id'><b>" + HE(d["name"] == "" ? d["regno"] : d["name"]) + "</b><span>" + HE(d["regno"]) + (d["prog"] != "" ? " &middot; " + HE(d["prog"]) : "") + "</span></div>");
        sb.Append("<div class='pc-badges'>" + badge + "</div>");
        sb.Append("</div>");
        // photos: new (big) + old (small) — click to view full size
        sb.Append("<div class='pc-imgs'>");
        sb.Append("<div class='pc-img pc-img--new'>");
        if (newUrl != "") sb.Append("<img src='" + HE(newUrl) + "' alt='new' loading='lazy' class='pc-clickimg' onclick=\"pcView('" + HE(newUrl) + "')\"/>"); else sb.Append("<div class='pc-img__none'>none</div>");
        sb.Append("<span class='pc-img__lbl'>New</span></div>");
        sb.Append("<div class='pc-img pc-img--old'>");
        if (oldUrl != "") sb.Append("<img src='" + HE(oldUrl) + "' alt='old' loading='lazy' class='pc-clickimg' onclick=\"pcView('" + HE(oldUrl) + "')\"/>"); else sb.Append("<div class='pc-img__none'>no previous</div>");
        sb.Append("<span class='pc-img__lbl'>Previous</span></div>");
        sb.Append("</div>");
        // meta
        sb.Append("<div class='pc-meta'>Requested " + HE(d["requested_at"]) + "</div>");
        string revLabel = st == "REJECTED" ? "Rejected" : st == "APPROVED" ? "Approved" : st == "DELETED" ? "Deleted" : "Reviewed";
        if (d["reviewed_at"] != "") sb.Append("<div class='pc-meta'>" + HE(revLabel) + " by " + HE(d["reviewed_by"]) + " &middot; " + HE(d["reviewed_at"]) + (d["comment"] != "" ? "<br/><i>&ldquo;" + HE(d["comment"]) + "&rdquo;</i>" : "") + "</div>");
        // actions
        if (pending)
        {
            sb.Append("<div class='pc-acts'>");
            sb.Append("<button type='button' class='pc-btn pc-btn--ok' onclick=\"pcReview(" + HE(d["id"]) + ",true)\">Approve</button>");
            sb.Append("<button type='button' class='pc-btn pc-btn--danger' onclick=\"pcReview(" + HE(d["id"]) + ",false)\">Reject</button>");
            sb.Append("</div>");
        }
        else if (banned)
        {
            sb.Append("<div class='pc-acts'><button type='button' class='pc-btn pc-btn--ok' onclick=\"pcUnban('" + HE(d["regno"]) + "')\">Lift ban</button></div>");
        }
        // Put an earlier photograph back. Offered on any non-pending version that holds a picture
        // and is not already the live one — including a rejected one, because a rejection is a
        // judgement an administrator is allowed to change their mind about.
        if (!pending && HasPhoto(d["newf"]) && d["is_live"] != "1")
        {
            sb.Append("<div class='pc-acts'><button type='button' class='pc-btn' onclick=\"pcRestore(" + HE(d["id"]) +
                      ")\" title='Make this the student&#39;s official photograph again'>Revert to this version</button></div>");
        }
        // Delete this version (removes an extra / unwanted submission; never the live photo).
        if (deletable)
        {
            sb.Append("<div class='pc-del'><a href='javascript:void(0)' onclick=\"pcDelete(" + HE(d["id"]) + ")\" title='Remove this photo version from the queue'>" + TrashIcon() + "Delete this version</a></div>");
        }
        sb.Append("</div>");
        return sb.ToString();
    }

    private static string TrashIcon()
    {
        return "<svg width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round' style='vertical-align:-2px;margin-right:5px'><polyline points='3 6 5 6 21 6'></polyline><path d='M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2'></path><line x1='10' y1='11' x2='10' y2='17'></line><line x1='14' y1='11' x2='14' y2='17'></line></svg>";
    }

    /// <summary>Feather-style camera glyph for the admin upload button (inline SVG, never an emoji).</summary>
    private static string CameraIcon()
    {
        return "<svg width='13' height='13' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round' style='vertical-align:-2px;margin-right:6px'><path d='M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z'></path><circle cx='12' cy='13' r='4'></circle></svg>";
    }

    /// <summary>Card for the Banned tab (student-level, from acad_student).</summary>
    private string BannedCard(Dictionary<string, string> d)
    {
        string photoUrl = HasPhoto(d["last_photo"]) ? PHOTO_BASE + Uri.EscapeDataString(d["last_photo"]) : "";
        var sb = new StringBuilder();
        sb.Append("<div class='pc-card pc-card--banned'>");
        sb.Append("<div class='pc-card__h'>");
        sb.Append("<div class='pc-card__id'><b>" + HE(d["name"] == "" ? d["regno"] : d["name"]) + "</b><span>" + HE(d["regno"]) + (d["prog"] != "" ? " &middot; " + HE(d["prog"]) : "") + "</span></div>");
        sb.Append("<div class='pc-badges'><span class='pc-st pc-st--banned'>BANNED</span></div>");
        sb.Append("</div>");
        sb.Append("<div class='pc-imgs pc-imgs--one'>");
        sb.Append("<div class='pc-img pc-img--new'>");
        if (photoUrl != "") sb.Append("<img src='" + HE(photoUrl) + "' alt='last' loading='lazy' class='pc-clickimg' onclick=\"pcView('" + HE(photoUrl) + "')\"/>"); else sb.Append("<div class='pc-img__none'>no photo</div>");
        sb.Append("<span class='pc-img__lbl'>Last submitted</span></div>");
        sb.Append("</div>");
        sb.Append("<div class='pc-meta'><b>" + HE(d["rej_count"]) + "</b> rejection(s) &middot; banned " + HE(d["ban_at"]) + (d["ban_by"] != "" ? " by " + HE(d["ban_by"]) : "") + "</div>");
        if (d["ban_reason"] != "") sb.Append("<div class='pc-meta'><i>&ldquo;" + HE(d["ban_reason"]) + "&rdquo;</i></div>");
        sb.Append("<div class='pc-acts'><button type='button' class='pc-btn pc-btn--ok' onclick=\"pcUnban('" + HE(d["regno"]) + "')\">Lift ban &amp; allow re-upload</button></div>");
        sb.Append("</div>");
        return sb.ToString();
    }

    private string Tab(string key, string label, string cur, string q, int count)
    {
        string on = key == cur ? " pc-tab--on" : "";
        string href = "PhotoChangeController.aspx?status=" + key + (q != "" ? "&q=" + HttpUtility.UrlEncode(q) : "");
        string badge = count >= 0 ? " <em>(" + count + ")</em>" : "";
        return "<a class='pc-tab" + on + "' href='" + href + "'>" + HE(label) + badge + "</a>";
    }

    // ---- helpers ----
    private static string Safe(MySqlDataReader rd, string col) { return rd[col] == DBNull.Value ? "" : rd[col].ToString().Trim(); }
    private static string CardIcon()
    {
        return "<svg xmlns='http://www.w3.org/2000/svg' width='14' height='14' viewBox='0 0 24 24' fill='none' " +
               "stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round' style='vertical-align:-2px;margin-right:6px'>" +
               "<rect x='2' y='4' width='20' height='16' rx='2'></rect><circle cx='8.5' cy='11' r='2.5'></circle>" +
               "<path d='M4.5 17.5c.9-1.6 2.3-2.5 4-2.5s3.1.9 4 2.5'></path>" +
               "<line x1='15' y1='9' x2='19' y2='9'></line><line x1='15' y1='13' x2='19' y2='13'></line></svg>";
    }

    private string HE(string s) { return Server.HtmlEncode(s ?? ""); }
    private static int SafeInt(string s, int def) { int v; return int.TryParse((s ?? "").Trim(), out v) ? v : def; }
    private static MySqlParameter Clone(MySqlParameter p) { return new MySqlParameter(p.ParameterName, p.Value); }
    private static string JsEnc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder();
        foreach (char c in s) { if (c == '"' || c == '\\') sb.Append('\\').Append(c); else if (c == '\n' || c == '\r' || c == '\t') sb.Append(' '); else sb.Append(c); }
        return sb.ToString();
    }
}
