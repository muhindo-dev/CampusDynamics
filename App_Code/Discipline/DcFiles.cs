using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web;

// =====================================================================
//  Student Disciplinary module: evidence and other attachments.
//  Files live under Data_Private\Disciplinary\{caseId}\, outside both web
//  roots; DcFile.ashx streams them after the visibility check (and logs
//  the opening of a restricted case). Removing hides with a reason.
// =====================================================================
public static class DcFiles
{
    public static readonly Dictionary<string, string> Allowed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
        { ".pdf", "application/pdf" }, { ".jpg", "image/jpeg" }, { ".jpeg", "image/jpeg" }, { ".png", "image/png" },
        { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" } };

    public static readonly string[] Kinds = { "STATEMENT", "PHOTO", "SCRIPT", "SCREENSHOT", "MINUTES", "LETTER", "APPEAL", "EVIDENCE", "OTHER" };

    public static bool MagicOk(string ext, byte[] b)
    {
        if (b.Length < 4) return false;
        switch (ext.ToLowerInvariant())
        {
            case ".pdf": return b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46;
            case ".jpg": case ".jpeg": return b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;
            case ".png": return b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47;
            case ".docx": case ".xlsx": return b[0] == 0x50 && b[1] == 0x4B && b[2] == 0x03 && b[3] == 0x04;
        }
        return false;
    }

    public static string Sha1(byte[] bytes)
    {
        using (var h = SHA1.Create()) { var sb = new StringBuilder(); foreach (byte x in h.ComputeHash(bytes)) sb.Append(x.ToString("x2")); return sb.ToString(); }
    }

    public static string Save(int caseId, int? entryId, string kind, string description, bool visible, HttpPostedFile file, out int attachmentId)
    {
        attachmentId = 0;
        if (file == null || file.ContentLength == 0) return "Choose a file.";
        string name = Path.GetFileName(file.FileName ?? "");
        string ext = Path.GetExtension(name);
        if (!Allowed.ContainsKey(ext)) return "Attach a PDF, JPG, PNG, Word (.docx) or Excel (.xlsx) file.";
        int maxMb = DcSettings.Int("attachment_max_mb", 15);
        if (file.ContentLength > maxMb * 1024 * 1024) return "The file is larger than " + maxMb + " MB.";
        kind = (kind ?? "").ToUpperInvariant();
        if (Array.IndexOf(Kinds, kind) < 0) kind = "EVIDENCE";
        byte[] bytes;
        using (var ms = new MemoryStream()) { file.InputStream.CopyTo(ms); bytes = ms.ToArray(); }
        if (!MagicOk(ext, bytes)) return "The file's content does not match its type.";
        string sha = Sha1(bytes);

        using (var c = FaDb.Open())
        using (var tx = c.BeginTransaction())
        {
            DataRow cs = DcAccess.VisibleCase(c, tx, caseId, true);
            if (cs == null) { tx.Rollback(); return "That case was not found."; }
            bool open = FaDb.S(cs["status"]) != "CLOSED" && FaDb.S(cs["status"]) != "WITHDRAWN";
            if (!DcAccess.CanManageCase(cs) && !(DcAccess.IsReporter(cs) && open)) { tx.Rollback(); return "You cannot add files to this case."; }
            if (entryId.HasValue && FaDb.Scalar(c, tx, "SELECT 1 FROM dc_entry WHERE id=@e AND case_id=@c", "@e", entryId.Value, "@c", caseId) == null)
            { tx.Rollback(); return "That entry does not belong to this case."; }
            if (FaDb.Scalar(c, tx, "SELECT 1 FROM dc_attachment WHERE case_id=@c AND sha1=@s AND is_active=1", "@c", caseId, "@s", sha) != null)
            { tx.Rollback(); return "This file is already attached to the case."; }
            string stored = "att-" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ext.ToLowerInvariant();
            File.WriteAllBytes(Path.Combine(DcPaths.CaseFolder(caseId), stored), bytes);
            attachmentId = (int)FaDb.Insert(c, tx,
                "INSERT INTO dc_attachment (case_id, entry_id, kind, description, original_name, stored_name, mime, size_bytes, sha1, student_visible, uploaded_by, uploaded_via, uploaded_at) " +
                "VALUES (@c,@e,@k,@d,@n,@s,@m,@z,@h,@v,@u,'EADMIN',NOW())",
                "@c", caseId, "@e", entryId.HasValue ? (object)entryId.Value : DBNull.Value, "@k", kind, "@d", FaDb.NullIfEmpty(DcAudit.Cut(description, 300)),
                "@n", DcAudit.Cut(name, 200), "@s", stored, "@m", Allowed[ext], "@z", bytes.Length, "@h", sha, "@v", visible ? 1 : 0, "@u", DcAccess.Username());
            if (!entryId.HasValue)
                DcEntries.Add(c, tx, caseId, kind == "STATEMENT" ? "STATEMENT" : "EVIDENCE", "File added: " + name,
                    string.IsNullOrEmpty(description) ? "" : description, visible, null, null, null, null, new { attachmentId = attachmentId }, null, null);
            DcAudit.Write(c, tx, "ATTACHMENT", attachmentId, caseId, "UPLOAD", null, new { kind = kind, name = name, size = bytes.Length, sha1 = sha, visible = visible }, null,
                          "File attached to " + FaDb.S(cs["case_no"]) + ": " + name);
            FaDb.Exec(c, tx, "UPDATE dc_case SET last_entry_at=NOW() WHERE id=@c", "@c", caseId);
            tx.Commit();
        }
        return null;
    }

    public static string Remove(int attachmentId, string reason)
    {
        if ((reason ?? "").Trim().Length < 10) return "Give a reason of at least 10 characters.";
        using (var c = FaDb.Open())
        using (var tx = c.BeginTransaction())
        {
            DataTable t = FaDb.Table(c, tx, "SELECT * FROM dc_attachment WHERE id=@id AND is_active=1 FOR UPDATE", "@id", attachmentId);
            if (t.Rows.Count == 0) { tx.Rollback(); return "That file is no longer attached."; }
            DataRow cs = DcAccess.VisibleCase(c, tx, FaDb.I(t.Rows[0]["case_id"]), true);
            if (cs == null || !DcAccess.CanManageCase(cs)) { tx.Rollback(); return "Only an officer managing the case can remove a file."; }
            FaDb.Exec(c, tx, "UPDATE dc_attachment SET is_active=0, removed_by=@u, removed_at=NOW(), removed_reason=@r WHERE id=@id",
                      "@u", DcAccess.Username(), "@r", DcAudit.Cut(reason, 500), "@id", attachmentId);
            DcEntries.Add(c, tx, FaDb.I(cs["id"]), "NOTE", "File hidden: " + FaDb.S(t.Rows[0]["original_name"]), "Reason: " + reason, false, null, null, null, null, new { attachmentId = attachmentId }, null, null);
            DcAudit.Write(c, tx, "ATTACHMENT", attachmentId, FaDb.I(cs["id"]), "REMOVE", new { name = FaDb.S(t.Rows[0]["original_name"]), active = 1 }, new { active = 0 }, reason,
                          "File removed from " + FaDb.S(cs["case_no"]) + ": " + FaDb.S(t.Rows[0]["original_name"]));
            tx.Commit();
        }
        return null;
    }

    public static bool Stream(HttpContext ctx, int attachmentId, bool inline)
    {
        using (var c = FaDb.Open())
        {
            DataTable t = FaDb.Table(c, null, "SELECT * FROM dc_attachment WHERE id=@id", "@id", attachmentId);
            if (t.Rows.Count == 0) return false;
            DataRow r = t.Rows[0];
            DataRow cs = DcAccess.VisibleCase(c, null, FaDb.I(r["case_id"]), false);
            if (cs == null) return false;
            if (FaDb.I(r["is_active"]) != 1 && !DcAccess.CanManageCase(cs)) return false;
            DcAccess.LogRestrictedAccess(c, cs, "ATTACHMENT");
            string path = Path.Combine(DcPaths.CaseFolder(FaDb.I(cs["id"])), Path.GetFileName(FaDb.S(r["stored_name"])));
            if (!File.Exists(path)) return false;
            ctx.Response.ContentType = FaDb.S(r["mime"]);
            ctx.Response.AddHeader("Content-Disposition", (inline ? "inline" : "attachment") + "; filename=\"" + FaDb.S(r["original_name"]).Replace("\"", "") + "\"");
            ctx.Response.AddHeader("X-Content-Type-Options", "nosniff");
            ctx.Response.Cache.SetCacheability(HttpCacheability.Private);
            ctx.Response.TransmitFile(path);
            return true;
        }
    }
}
