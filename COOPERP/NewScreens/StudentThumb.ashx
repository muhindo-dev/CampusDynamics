<%@ WebHandler Language="C#" Class="StudentThumb" %>

using System;
using System.Configuration;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Web;
using System.Web.SessionState;
using MySql.Data.MySqlClient;

/// <summary>
/// A small, cached square thumbnail of a student's photo.
///
/// WHY THIS EXISTS: the photos on disk have a median size of 469 KB and 89% of them exceed
/// 300 KB, because most are uncompressed bitmaps that were saved with a .jpg extension. A list
/// of fifty candidates with their faces on it would pull roughly 23 MB. At 72px and JPEG
/// quality 82 the same fifty cost about 200 KB.
///
/// The resize happens ONCE per student: the result is written beside the originals in a _thumb
/// folder and served straight from there afterwards. The box is CPU-bound, not disk-bound, so
/// paying once is much better than resizing on every request with only an output cache.
///
/// SIZES: ?s= picks one of a fixed set. A whitelist, not a number, because an open size
/// parameter lets anyone fill the disk with variants of every student's face.
///
///   72   the avatar in a table row or a modal header  - square centre-crop
///   144  the same avatar on a high-density screen     - square centre-crop
///   480  the full view a reviewer opens to identify   - NOT cropped
///
/// The crop is the important difference. A square centre-crop is right for a small round
/// avatar, where the alternative is a squashed face. It is wrong for the large view: on a
/// portrait photograph it slices off the top of the head or the chin, which is exactly the
/// moment a reviewer is trying to confirm they have the right person. Above 144 the whole
/// photograph is kept and fitted inside the box.
///
/// SAFETY: the caller passes a student number, never a filename. The file name comes from
/// acad_student.photofile and is stripped to its base name before it touches the filesystem, so
/// nothing a caller sends can walk out of the photos folder.
/// </summary>
public class StudentThumb : IHttpHandler, IReadOnlySessionState
{
    private const int SIZE = 72;
    /// <summary>Every size this handler will ever render. See the note on ?s= above.</summary>
    private static readonly int[] SIZES = { 72, 144, 480 };
    /// <summary>At or below this, a square centre-crop; above it, the whole photograph.</summary>
    private const int CROP_UPTO = 144;
    private const string PHOTO_DIR = "~/COOPERP/StudentInfo/photos/";
    private const string THUMB_DIR = "~/COOPERP/StudentInfo/photos/_thumb/";

    public bool IsReusable { get { return false; } }

    public void ProcessRequest(HttpContext ctx)
    {
        // Signed-in callers only. Student photographs are personal data and this handler is
        // reachable by URL; without this it would hand them to anyone who could guess a
        // student number.
        bool signedIn = false;
        try
        {
            signedIn = (ctx.Session != null && ctx.Session["username"] != null &&
                        ctx.Session["username"].ToString().Trim() != "")
                       || (ctx.User != null && ctx.User.Identity != null && ctx.User.Identity.IsAuthenticated);
        }
        catch { }
        if (!signedIn) { Fallback(ctx); return; }

        string regno = (ctx.Request.QueryString["r"] ?? "").Trim();
        if (regno == "" || regno.Length > 85) { Fallback(ctx); return; }

        int size = SIZE;
        int asked;
        if (int.TryParse(ctx.Request.QueryString["s"] ?? "", out asked))
        {
            size = 0;
            foreach (int allowed in SIZES) if (allowed == asked) { size = allowed; break; }
            if (size == 0) size = SIZE;      // anything else quietly gets the default
        }
        bool crop = size <= CROP_UPTO;

        string file = "";
        try
        {
            string cs = ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString;
            using (var c = new MySqlConnection(cs))
            {
                c.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT IFNULL(photofile,'') FROM acad_student WHERE regno=@r LIMIT 1", c))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    object o = cmd.ExecuteScalar();
                    if (o != null && o != DBNull.Value) file = o.ToString().Trim();
                }
            }
        }
        catch { }

        if (file == "" || file == "-") { Fallback(ctx); return; }

        // Whatever the database holds, only the bare file name is ever used.
        file = Path.GetFileName(file);
        if (file == "") { Fallback(ctx); return; }

        string src = ctx.Server.MapPath(PHOTO_DIR + file);
        if (!File.Exists(src)) { Fallback(ctx); return; }

        string thumbDir = ctx.Server.MapPath(THUMB_DIR);
        // The size is in the name, so the 72px thumbnails already on disk stay valid and each
        // size is built once.
        string thumb = Path.Combine(thumbDir, Path.GetFileNameWithoutExtension(file) + "_" + size + ".jpg");

        try
        {
            if (!File.Exists(thumb) || File.GetLastWriteTimeUtc(thumb) < File.GetLastWriteTimeUtc(src))
            {
                if (!Directory.Exists(thumbDir)) Directory.CreateDirectory(thumbDir);
                MakeThumb(src, thumb, size, crop);
            }
        }
        catch { /* a failed resize must still show a face-shaped placeholder, not an error */ }

        if (File.Exists(thumb)) Send(ctx, thumb, "image/jpeg");
        else Fallback(ctx);
    }

    /// <summary>
    /// Scales a photograph into <paramref name="size"/>.
    ///
    /// Square centre-crop for an avatar, because the alternative at 72px is a squashed face.
    /// For the large view the whole photograph is kept and letterboxed instead: a centre-crop
    /// of a portrait cuts off the top of the head, which defeats the one thing the large view
    /// is for.
    /// </summary>
    private static void MakeThumb(string src, string dest, int size, bool square)
    {
        using (var img = Image.FromFile(src))
        {
            Rectangle crop;
            int outW, outH;

            if (square)
            {
                int side = Math.Min(img.Width, img.Height);
                crop = new Rectangle((img.Width - side) / 2, (img.Height - side) / 2, side, side);
                outW = outH = size;
            }
            else
            {
                crop = new Rectangle(0, 0, img.Width, img.Height);
                // Never enlarge: a 300px original blown up to 480 is worse than a small sharp one.
                double k = Math.Min(1.0, Math.Min((double)size / img.Width, (double)size / img.Height));
                outW = Math.Max(1, (int)Math.Round(img.Width * k));
                outH = Math.Max(1, (int)Math.Round(img.Height * k));
            }

            using (var bmp = new Bitmap(outW, outH, PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(img, new Rectangle(0, 0, outW, outH), crop, GraphicsUnit.Pixel);
                }

                ImageCodecInfo jpeg = null;
                foreach (ImageCodecInfo e in ImageCodecInfo.GetImageEncoders())
                    if (e.MimeType == "image/jpeg") { jpeg = e; break; }

                using (var p = new EncoderParameters(1))
                {
                    p.Param[0] = new EncoderParameter(Encoder.Quality, size > CROP_UPTO ? 88L : 82L);
                    // Written to a temporary name and moved, so a half-written file is never
                    // served to the next request that arrives mid-resize.
                    string tmp = dest + ".tmp";
                    if (jpeg != null) bmp.Save(tmp, jpeg, p); else bmp.Save(tmp, ImageFormat.Jpeg);
                    if (File.Exists(dest)) File.Delete(dest);
                    File.Move(tmp, dest);
                }
            }
        }
    }

    private static void Send(HttpContext ctx, string path, string mime)
    {
        ctx.Response.ContentType = mime;
        // A student's photograph changes rarely; the browser should not ask twice in a day.
        ctx.Response.Cache.SetCacheability(HttpCacheability.Private);
        ctx.Response.Cache.SetMaxAge(TimeSpan.FromHours(12));
        ctx.Response.Cache.SetLastModified(File.GetLastWriteTime(path));
        ctx.Response.TransmitFile(path);
    }

    private static void Fallback(HttpContext ctx)
    {
        try
        {
            string def = ctx.Server.MapPath(PHOTO_DIR + "default.png");
            if (File.Exists(def)) { Send(ctx, def, "image/png"); return; }
        }
        catch { }
        ctx.Response.ContentType = "image/gif";
        ctx.Response.Cache.SetCacheability(HttpCacheability.Private);
        // A 1x1 transparent GIF, so a missing photo leaves a gap rather than a broken icon.
        ctx.Response.BinaryWrite(Convert.FromBase64String(
            "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7"));
    }
}
