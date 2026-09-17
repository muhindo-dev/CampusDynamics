<%@ WebHandler Language="C#" Class="NewScreens_SemsFile" %>

using System;
using System.IO;
using System.Text;
using System.Web;
using System.Web.SessionState;

// =====================================================================
//  SEMS file endpoint — the two things a PageMethod cannot do:
//  stream a CSV download, and receive an uploaded sheet.
//
//  GET  ?action=template                     blank Google template
//  GET  ?action=export&mode=create|update|all[&batchRef=&stage=&campus=&year=]
//  GET  ?action=credentials&batchRef=…       address + password sheet for the registry
//  POST  multipart, action=import            a Google export to parse (JSON back)
//
//  Session-guarded exactly like the page's WebMethods: this streams live
//  credentials, so an expired session must get nothing.
// =====================================================================
public class NewScreens_SemsFile : IHttpHandler, IRequiresSessionState
{
    public bool IsReusable { get { return false; } }

    public void ProcessRequest(HttpContext ctx)
    {
        ctx.Response.Cache.SetNoStore();

        if (ctx.Session == null || ctx.Session["username"] == null)
        {
            ctx.Response.StatusCode = 403;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.Write("{\"success\":false,\"message\":\"Your session has expired. Please sign in again.\"}");
            return;
        }

        string action = (ctx.Request["action"] ?? "").Trim().ToLowerInvariant();
        try
        {
            switch (action)
            {
                case "template": Template(ctx); break;
                case "export": Export(ctx); break;
                case "credentials": Credentials(ctx); break;
                case "import": Import(ctx); break;
                default:
                    ctx.Response.ContentType = "application/json; charset=utf-8";
                    ctx.Response.Write("{\"success\":false,\"message\":\"Unknown action.\"}");
                    break;
            }
        }
        catch (System.Threading.ThreadAbortException)
        {
            // Never rewrite a response that has already been sent.
            throw;
        }
        catch (Exception ex)
        {
            ctx.Response.Clear();
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.Write(new System.Web.Script.Serialization.JavaScriptSerializer()
                .Serialize(new { success = false, message = ex.Message }));
        }
    }

    private static void SendCsv(HttpContext ctx, string csv, string fileName)
    {
        ctx.Response.Clear();
        ctx.Response.ContentType = "text/csv; charset=utf-8";
        ctx.Response.AddHeader("Content-Disposition", "attachment; filename=\"" + fileName + "\"");
        // UTF-8 BOM: without it Excel mangles any non-ASCII name in the sheet.
        ctx.Response.BinaryWrite(new byte[] { 0xEF, 0xBB, 0xBF });
        ctx.Response.Write(csv);
        ctx.Response.Flush();
        // CompleteRequest, NOT Response.End(). End() raises ThreadAbortException, which the
        // handler's own catch then turned into {"success":false,"message":"Thread was being
        // aborted."} written over the sheet that had just been sent — so Excel opened the
        // error instead of the export.
        ctx.ApplicationInstance.CompleteRequest();
    }

    private static void Template(HttpContext ctx)
    {
        SendCsv(ctx, SemsBatch.ExportTemplateCsv(), "google-workspace-template.csv");
    }

    private static void Export(HttpContext ctx)
    {
        string mode = (ctx.Request["mode"] ?? "create").Trim().ToLowerInvariant();

        // "pending" is the start of the protocol: allocate addresses for the students who
        // are eligible, generated and waiting, then hand back the sheet that creates them
        // in Google. Nothing is issued to a student here — that happens on import.
        if (mode == "pending")
        {
            var opts = new System.Collections.Generic.Dictionary<string, object>();
            opts["scope"] = "filter";
            opts["stage"] = "PENDING_CREATION";
            opts["campus"] = (ctx.Request["campus"] ?? "").Trim();
            opts["year"] = (ctx.Request["year"] ?? "").Trim();
            opts["limit"] = ToInt(ctx.Request["limit"], 2000);
            opts["otherLen"] = ToInt(ctx.Request["otherLen"], 3);
            opts["changePwNext"] = (ctx.Request["changePwNext"] ?? "true").Trim().ToLowerInvariant() != "false";
            opts["orgUnit"] = SemsBatch.NormaliseOrgUnit(ctx.Request["orgUnit"]);
            opts["orgUnitMode"] = (ctx.Request["orgUnitMode"] ?? "intake").Trim().ToLowerInvariant();
            opts["notify"] = false;

            int nRows; string bref, message;
            string sheet = SemsBatch.BuildPendingSheet(
                new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(opts),
                out nRows, out bref, out message);

            if (nRows == 0)
            {
                ctx.Response.ContentType = "application/json; charset=utf-8";
                ctx.Response.Write(new System.Web.Script.Serialization.JavaScriptSerializer()
                    .Serialize(new { success = false, message = string.IsNullOrEmpty(message) ? "There are no pending students to export." : message }));
                return;
            }
            SendCsv(ctx, sheet, "mru-google-new-accounts-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + "-" + nRows + "users.csv");
            return;
        }

        var sc = new SemsBatch.ExportScope();
        sc.Mode = mode;
        sc.BatchRef = (ctx.Request["batchRef"] ?? "").Trim();
        sc.Stage = (ctx.Request["stage"] ?? "").Trim();
        sc.Campus = (ctx.Request["campus"] ?? "").Trim();
        sc.Programme = (ctx.Request["programme"] ?? "").Trim();
        sc.Year = (ctx.Request["year"] ?? "").Trim();
        sc.GoogleStatus = (ctx.Request["googleStatus"] ?? "").Trim();
        sc.ChangePwNext = (ctx.Request["changePwNext"] ?? "true").Trim().ToLowerInvariant() != "false";
        // Normalised here too: this handler builds its own scope rather than going through
        // ReadScope, so it would otherwise be the one door a raw "students/" could still enter by.
        sc.OrgUnit = SemsBatch.NormaliseOrgUnit(ctx.Request["orgUnit"]);
        sc.OrgUnitMode = (ctx.Request["orgUnitMode"] ?? "intake").Trim().ToLowerInvariant();

        int rows; string batchRef;
        string csv = SemsBatch.BuildExportCsv(sc, out rows, out batchRef);
        if (rows == 0)
        {
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.Write("{\"success\":false,\"message\":\"Nothing to export for that selection.\"}");
            return;
        }
        SendCsv(ctx, csv, "mru-google-" + sc.Mode + "-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + "-" + rows + "users.csv");
    }

    private static int ToInt(string v, int dflt)
    { int n; return int.TryParse((v ?? "").Trim(), out n) && n > 0 ? n : dflt; }

    private static void Credentials(HttpContext ctx)
    {
        string batchRef = (ctx.Request["batchRef"] ?? "").Trim();
        int rows;
        string csv = SemsBatch.BuildCredentialsCsv(batchRef, out rows);
        if (rows == 0)
        {
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.Write("{\"success\":false,\"message\":\"That batch has no created rows.\"}");
            return;
        }
        SendCsv(ctx, csv, "mru-credentials-" + (batchRef == "" ? DateTime.Now.ToString("yyyyMMdd-HHmm") : batchRef) + ".csv");
    }

    private static void Import(HttpContext ctx)
    {
        ctx.Response.ContentType = "application/json; charset=utf-8";
        if (ctx.Request.Files == null || ctx.Request.Files.Count == 0)
        { ctx.Response.Write("{\"success\":false,\"message\":\"No file was uploaded.\"}"); return; }

        HttpPostedFile f = ctx.Request.Files[0];
        if (f == null || f.ContentLength == 0)
        { ctx.Response.Write("{\"success\":false,\"message\":\"The uploaded file is empty.\"}"); return; }
        if (f.ContentLength > 12 * 1024 * 1024)
        { ctx.Response.Write("{\"success\":false,\"message\":\"That file is larger than 12 MB — split it into smaller sheets.\"}"); return; }

        string name = Path.GetFileName(f.FileName ?? "upload.csv");
        string ext = (Path.GetExtension(name) ?? "").ToLowerInvariant();
        if (ext == ".xlsx" || ext == ".xls")
        {
            ctx.Response.Write("{\"success\":false,\"message\":\"Excel workbooks are not read directly. In Google Sheets or Excel choose File \\u2192 Download \\u2192 Comma-separated values (.csv), then upload that.\"}");
            return;
        }

        var buf = new byte[f.ContentLength];
        int read = 0, got;
        while (read < buf.Length && (got = f.InputStream.Read(buf, read, buf.Length - read)) > 0) read += got;

        // Google exports UTF-8 (usually with a BOM); a sheet re-saved by Excel may be ANSI.
        // StreamReader with detectEncodingFromByteOrderMarks handles both.
        string text;
        using (var ms = new MemoryStream(buf, 0, read))
        using (var sr = new StreamReader(ms, Encoding.UTF8, true))
            text = sr.ReadToEnd();

        ctx.Response.Write(SemsBatch.ImportParse(name, text));
    }
}
