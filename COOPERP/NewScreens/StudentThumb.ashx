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
/// SAFETY: the caller passes a student number, never a filename. The file name comes from
/// acad_student.photofile and is stripped to its base name before it touches the filesystem, so
/// nothing a caller sends can walk out of the photos folder.
/// </summary>
public class StudentThumb : IHttpHandler, IReadOnlySessionState
{
    private const int SIZE = 72;
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
        string thumb = Path.Combine(thumbDir, Path.GetFileNameWithoutExtension(file) + "_" + SIZE + ".jpg");

        try
        {
            if (!File.Exists(thumb) || File.GetLastWriteTimeUtc(thumb) < File.GetLastWriteTimeUtc(src))
            {
                if (!Directory.Exists(thumbDir)) Directory.CreateDirectory(thumbDir);
                MakeThumb(src, thumb);
            }
        }
        catch { /* a failed resize must still show a face-shaped placeholder, not an error */ }

        if (File.Exists(thumb)) Send(ctx, thumb, "image/jpeg");
        else Fallback(ctx);
    }

    /// <summary>Square centre-crop, then scale. A face stays a face at 72px; a squashed one does not.</summary>
    private static void MakeThumb(string src, string dest)
    {
        using (var img = Image.FromFile(src))
        {
            int side = Math.Min(img.Width, img.Height);
            var crop = new Rectangle((img.Width - side) / 2, (img.Height - side) / 2, side, side);

            using (var bmp = new Bitmap(SIZE, SIZE, PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(img, new Rectangle(0, 0, SIZE, SIZE), crop, GraphicsUnit.Pixel);
                }

                ImageCodecInfo jpeg = null;
                foreach (ImageCodecInfo e in ImageCodecInfo.GetImageEncoders())
                    if (e.MimeType == "image/jpeg") { jpeg = e; break; }

                using (var p = new EncoderParameters(1))
                {
                    p.Param[0] = new EncoderParameter(Encoder.Quality, 82L);
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
