using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web;

// =====================================================================
//  Fixed Assets: attachments (plan 2.7).
//  Files live under ~/App_Data/FixedAssets/{assetId}/, which IIS never
//  serves directly; FaFile.ashx streams them after a permission check.
//  Removing an attachment hides it with a reason; the file is kept.
// =====================================================================
public static class FaFiles
{
    private static readonly Dictionary<string, string> Allowed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
        { ".pdf", "application/pdf" }, { ".jpg", "image/jpeg" }, { ".jpeg", "image/jpeg" }, { ".png", "image/png" },
        { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" } };

    public static string Folder(int assetId)
    {
        string root = HttpContext.Current.Server.MapPath("~/App_Data/FixedAssets");
        string dir = Path.Combine(root, assetId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>The first bytes must agree with the extension, so a renamed executable cannot pass as a PDF.</summary>
    private static bool MagicOk(string ext, byte[] b)
    {
        if (b.Length < 4) return false;
        switch (ext.ToLowerInvariant())
        {
            case ".pdf": return b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46;               // %PDF
            case ".jpg": case ".jpeg": return b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;
            case ".png": return b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47;
            case ".docx": case ".xlsx": return b[0] == 0x50 && b[1] == 0x4B && b[2] == 0x03 && b[3] == 0x04;  // zip
        }
        return false;
    }

    public static string Save(int assetId, long? recordId, string kind, HttpPostedFile file, out int attachmentId)
    {
        attachmentId = 0;
        if (file == null || file.ContentLength == 0) return "Choose a file.";
        string name = Path.GetFileName(file.FileName ?? "");
        string ext = Path.GetExtension(name);
        if (!Allowed.ContainsKey(ext)) return "Attach a PDF, JPG, PNG, Word (.docx) or Excel (.xlsx) file.";
        int maxMb = FaSettings.Int("attachment_max_mb", 10);
        if (file.ContentLength > maxMb * 1024 * 1024) return "The file is larger than " + maxMb + " MB.";
        string[] kinds = { "PHOTO", "INVOICE", "WARRANTY", "VALUATION", "DISPOSAL", "OTHER" };
        kind = (kind ?? "").ToUpperInvariant();
        if (Array.IndexOf(kinds, kind) < 0) kind = "OTHER";
        if (kind == "PHOTO" && ext.ToLowerInvariant() != ".jpg" && ext.ToLowerInvariant() != ".jpeg" && ext.ToLowerInvariant() != ".png") return "A photo must be a JPG or PNG.";

        byte[] bytes;
        using (var ms = new MemoryStream()) { file.InputStream.CopyTo(ms); bytes = ms.ToArray(); }
        if (!MagicOk(ext, bytes)) return "The file's content does not match its type.";
        string sha;
        using (var h = SHA1.Create()) { var sb = new StringBuilder(); foreach (byte x in h.ComputeHash(bytes)) sb.Append(x.ToString("x2")); sha = sb.ToString(); }

        using (var c = FaDb.Open())
        using (var tx = c.BeginTransaction())
        {
            var a = FaAssets.Row(c, tx, assetId, true);
            if (a == null) { tx.Rollback(); return "That asset no longer exists."; }
            if (recordId.HasValue && FaDb.Scalar(c, tx, "SELECT 1 FROM fa_record WHERE id=@r AND asset_id=@a", "@r", recordId.Value, "@a", assetId) == null)
            { tx.Rollback(); return "That record does not belong to this asset."; }
            if (FaDb.Scalar(c, tx, "SELECT 1 FROM fa_attachment WHERE asset_id=@a AND sha1=@s AND is_active=1", "@a", assetId, "@s", sha) != null)
            { tx.Rollback(); return "This file is already attached to the asset."; }
            string stored = DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ext.ToLowerInvariant();
            File.WriteAllBytes(Path.Combine(Folder(assetId), stored), bytes);
            attachmentId = (int)FaDb.Insert(c, tx,
                "INSERT INTO fa_attachment (asset_id, record_id, kind, original_name, stored_name, mime, size_bytes, sha1, uploaded_by, uploaded_at) " +
                "VALUES (@a,@r,@k,@n,@s,@m,@z,@h,@u,NOW())",
                "@a", assetId, "@r", FaDb.DbVal(recordId), "@k", kind, "@n", FaAudit.Cut(name, 200), "@s", stored, "@m", Allowed[ext],
                "@z", bytes.Length, "@h", sha, "@u", FaAccess.Username());
            FaAudit.Write(c, tx, "ATTACHMENT", attachmentId, assetId, "UPLOAD", null, new { kind = kind, name = name, size = bytes.Length }, null,
                          "Attachment added to " + FaDb.S(a["asset_no"]) + ": " + name);
            tx.Commit();
        }
        return null;
    }

    public static string Remove(int attachmentId, string reason)
    {
        if ((reason ?? "").Trim().Length < 5) return "Give a reason of at least 5 characters.";
        using (var c = FaDb.Open())
        using (var tx = c.BeginTransaction())
        {
            DataTable t = FaDb.Table(c, tx, "SELECT * FROM fa_attachment WHERE id=@id AND is_active=1 FOR UPDATE", "@id", attachmentId);
            if (t.Rows.Count == 0) { tx.Rollback(); return "That attachment no longer exists."; }
            FaDb.Exec(c, tx, "UPDATE fa_attachment SET is_active=0, removed_by=@u, removed_at=NOW(), removed_reason=@r WHERE id=@id",
                      "@u", FaAccess.Username(), "@r", FaAudit.Cut(reason, 500), "@id", attachmentId);
            FaAudit.Write(c, tx, "ATTACHMENT", attachmentId, FaDb.I(t.Rows[0]["asset_id"]), "REMOVE", new { name = FaDb.S(t.Rows[0]["original_name"]) }, null, reason,
                          "Attachment removed: " + FaDb.S(t.Rows[0]["original_name"]));
            tx.Commit();
        }
        return null;
    }

    /// <summary>Streams an attachment to a signed-in user who may view the register.</summary>
    public static void Stream(HttpContext ctx, int attachmentId, bool inline)
    {
        DataTable t;
        using (var c = FaDb.Open())
            t = FaDb.Table(c, null, "SELECT * FROM fa_attachment WHERE id=@id AND is_active=1", "@id", attachmentId);
        if (t.Rows.Count == 0) { ctx.Response.StatusCode = 404; return; }
        DataRow r = t.Rows[0];
        string path = Path.Combine(Folder(FaDb.I(r["asset_id"])), Path.GetFileName(FaDb.S(r["stored_name"])));
        if (!File.Exists(path)) { ctx.Response.StatusCode = 404; return; }
        string name = FaDb.S(r["original_name"]).Replace("\"", "");
        ctx.Response.ContentType = FaDb.S(r["mime"]);
        ctx.Response.AddHeader("Content-Disposition", (inline ? "inline" : "attachment") + "; filename=\"" + name + "\"");
        ctx.Response.AddHeader("X-Content-Type-Options", "nosniff");
        ctx.Response.Cache.SetCacheability(HttpCacheability.Private);
        ctx.Response.TransmitFile(path);
    }
}
