using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using MySql.Data.MySqlClient;

// =====================================================================
//  Student Disciplinary module: letters (plan 5.8, D13).
//  A template is merged with the case, previewed, then issued: the PDF
//  is built once, stored under Data_Private\Disciplinary\{case}, and the
//  merged text kept in dc_letter exactly as issued. An issued letter is
//  never rebuilt or edited (the table refuses updates); a correction is
//  a new letter.
// =====================================================================
public static class DcLetters
{
    public static List<object> TemplateChoices(MySqlConnection c)
    {
        var l = new List<object>();
        foreach (DataRow r in FaDb.Table(c, null, "SELECT code, name, student_copy FROM dc_letter_template WHERE is_active=1 ORDER BY sort_order, name").Rows)
            l.Add(new { code = FaDb.S(r[0]), name = FaDb.S(r[1]), studentCopy = FaDb.I(r[2]) == 1 });
        return l;
    }

    public static List<object> Templates(MySqlConnection c)
    {
        var l = new List<object>();
        foreach (DataRow r in FaDb.Table(c, null, "SELECT * FROM dc_letter_template ORDER BY sort_order, name").Rows)
            l.Add(new
            {
                id = FaDb.I(r["id"]), code = FaDb.S(r["code"]), name = FaDb.S(r["name"]), subject = FaDb.S(r["subject"]), body = FaDb.S(r["body"]),
                studentCopy = FaDb.I(r["student_copy"]) == 1, sort = FaDb.I(r["sort_order"]), active = FaDb.I(r["is_active"]) == 1, version = FaDb.I(r["row_version"])
            });
        return l;
    }

    public static readonly string[] MergeFields = {
        "student_name", "regno", "programme", "faculty", "campus", "case_no", "case_type", "incident_date", "incident_place",
        "hearing_date", "hearing_time", "hearing_venue", "decision_text", "sanctions", "appeal_deadline", "appeal_window_days",
        "appellate_authority", "sanction_lifted", "lift_reason", "suspension_from", "suspension_to", "contact_office", "today" };

    /// <summary>The merge fields known from the case itself. Callers add the ones that belong to the action.</summary>
    public static Dictionary<string, string> Fields(MySqlConnection c, MySqlTransaction tx, DataRow cs)
    {
        var f = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        DataRow st = DcLookups.Student(c, tx, FaDb.S(cs["regno"]));
        DataRow inc = FaDb.Table(c, tx, "SELECT * FROM dc_incident WHERE id=@i", "@i", FaDb.I(cs["incident_id"])).Rows[0];
        string fac = FaDb.S(FaDb.Scalar(c, tx, "SELECT faculty_name FROM acad_faculty WHERE faculty_code=@f", "@f", FaDb.S(cs["faculty_code"])));
        f["student_name"] = FaDb.S(cs["student_name"]);
        f["regno"] = FaDb.S(cs["regno"]);
        f["programme"] = st == null ? FaDb.S(cs["progcode"]) : FaDb.S(st["progname"]);
        f["faculty"] = DcLookups.Title(fac);
        f["campus"] = st == null ? "" : DcLookups.Title(FaDb.S(st["campus_name"]));
        f["case_no"] = FaDb.S(cs["case_no"]);
        f["case_type"] = cs.Table.Columns.Contains("type_name") ? FaDb.S(cs["type_name"]).ToLowerInvariant()
                         : FaDb.S(FaDb.Scalar(c, tx, "SELECT name FROM dc_case_type WHERE id=@t", "@t", FaDb.I(cs["case_type_id"]))).ToLowerInvariant();
        f["incident_date"] = DcFmt.LongDate(inc["occurred_at"]);
        f["incident_place"] = FaDb.S(inc["place"]) == "" ? "the University" : FaDb.S(inc["place"]);
        DateTime? h = FaDb.D(cs["hearing_at"]);
        f["hearing_date"] = h.HasValue ? h.Value.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture) : "";
        f["hearing_time"] = h.HasValue ? h.Value.ToString("h:mm tt", CultureInfo.InvariantCulture).ToLowerInvariant() : "";
        f["hearing_venue"] = FaDb.S(cs["hearing_venue"]);
        f["decision_text"] = FaDb.S(cs["decision_summary"]);
        f["appeal_deadline"] = DcFmt.LongDate(cs["appeal_deadline"]);
        f["appeal_window_days"] = DcSettings.AppealWindowDays.ToString(CultureInfo.InvariantCulture);
        f["appellate_authority"] = DcSettings.AppellateAuthority;
        f["contact_office"] = DcSettings.ContactOffice;
        f["today"] = DateTime.Today.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
        // Sanctions in force now, for templates issued outside a decision (for example a clearance or a re-issued notice).
        var parts = new List<string>();
        foreach (DataRow s in FaDb.Table(c, tx, "SELECT s.*, t.name, t.needs_dates FROM dc_sanction s JOIN dc_sanction_type t ON t.id=s.sanction_type_id WHERE s.case_id=@c AND s.status='ACTIVE' ORDER BY s.id", "@c", FaDb.I(cs["id"])).Rows)
            parts.Add(DcSanctions.Describe(s, FaDb.S(s["name"])));
        f["sanctions"] = parts.Count == 0 ? "none" : string.Join("; ", parts.ToArray());
        DataRow sus = null;
        foreach (DataRow s in FaDb.Table(c, tx, "SELECT * FROM dc_sanction WHERE case_id=@c AND status='ACTIVE' AND FIND_IN_SET('SUSPENSION',effects)>0 ORDER BY id DESC LIMIT 1", "@c", FaDb.I(cs["id"])).Rows) sus = s;
        f["suspension_from"] = sus == null ? "" : DcFmt.LongDate(sus["starts_on"]);
        f["suspension_to"] = sus == null ? "" : (sus["ends_on"] == DBNull.Value ? "further notice" : DcFmt.LongDate(sus["ends_on"]));
        f["sanction_lifted"] = "";
        f["lift_reason"] = "";
        return f;
    }

    public static string Merge(string text, Dictionary<string, string> f)
    {
        return Regex.Replace(text ?? "", @"\{\{\s*([a-zA-Z_]+)\s*\}\}", m =>
        {
            string v; return f.TryGetValue(m.Groups[1].Value, out v) ? (v ?? "") : "";
        });
    }

    /// <summary>Fields still blank after merging, so the officer sees them before issuing.</summary>
    public static List<string> Blanks(string text, Dictionary<string, string> f)
    {
        var l = new List<string>();
        foreach (Match m in Regex.Matches(text ?? "", @"\{\{\s*([a-zA-Z_]+)\s*\}\}"))
        {
            string k = m.Groups[1].Value, v;
            if ((!f.TryGetValue(k, out v) || string.IsNullOrEmpty((v ?? "").Trim())) && !l.Contains(k)) l.Add(k);
        }
        return l;
    }

    private static DataRow Template(MySqlConnection c, MySqlTransaction tx, string code)
    {
        DataTable t = FaDb.Table(c, tx, "SELECT * FROM dc_letter_template WHERE code=@c AND is_active=1", "@c", code);
        if (t.Rows.Count == 0) throw new DcRefusal("That letter template is not available.");
        return t.Rows[0];
    }

    public static object Preview(int caseId, string code, Dictionary<string, object> extra)
    {
        using (var c = FaDb.Open())
        {
            DataRow cs = DcAccess.VisibleCase(c, null, caseId, false);
            if (cs == null) throw new DcRefusal("That case was not found.");
            DataRow t = Template(c, null, code);
            var f = Fields(c, null, cs);
            foreach (var kv in extra) f[kv.Key] = Convert.ToString(kv.Value, CultureInfo.InvariantCulture);
            return new
            {
                subject = Merge(FaDb.S(t["subject"]), f), body = Merge(FaDb.S(t["body"]), f), studentCopy = FaDb.I(t["student_copy"]) == 1,
                blanks = Blanks(FaDb.S(t["subject"]) + "\n" + FaDb.S(t["body"]), f), name = FaDb.S(t["name"])
            };
        }
    }

    /// <summary>Builds, stores and records a letter inside the caller's transaction.</summary>
    public static void Issue(MySqlConnection c, MySqlTransaction tx, DataRow cs, string code, Dictionary<string, string> f, out long letterId, out long entryId)
    {
        DataRow t = Template(c, tx, code);
        int caseId = FaDb.I(cs["id"]);
        string subject = Merge(FaDb.S(t["subject"]), f);
        string body = Merge(FaDb.S(t["body"]), f);
        bool studentCopy = FaDb.I(t["student_copy"]) == 1;
        int n = FaDb.I(FaDb.Scalar(c, tx, "SELECT COUNT(*) FROM dc_letter WHERE case_id=@c", "@c", caseId)) + 1;
        string letterNo = FaDb.S(cs["case_no"]) + "/L" + n;
        while (FaDb.Scalar(c, tx, "SELECT 1 FROM dc_letter WHERE letter_no=@n", "@n", letterNo) != null) { n++; letterNo = FaDb.S(cs["case_no"]) + "/L" + n; }

        byte[] pdf = DcLetterPdf.Build(letterNo, DateTime.Today, f, subject, body, true);
        string sha;
        using (var h = SHA1.Create()) { var sb = new StringBuilder(); foreach (byte b in h.ComputeHash(pdf)) sb.Append(b.ToString("x2")); sha = sb.ToString(); }
        string stored = "letter-" + n.ToString(CultureInfo.InvariantCulture) + "-" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".pdf";
        File.WriteAllBytes(Path.Combine(DcPaths.CaseFolder(caseId), stored), pdf);

        entryId = DcEntries.Add(c, tx, caseId, "LETTER_ISSUED", "Letter issued: " + DcFmt.Lc(FaDb.S(t["name"])) + " (" + letterNo + ")",
            subject, studentCopy, null, null, null, null, new { letterNo = letterNo, template = code }, null, null);
        letterId = FaDb.Insert(c, tx,
            "INSERT INTO dc_letter (case_id, letter_no, template_code, subject, body, stored_name, sha1, student_visible, issued_by, issued_at, entry_id) " +
            "VALUES (@c,@n,@t,@s,@b,@f,@h,@v,@u,NOW(),@e)",
            "@c", caseId, "@n", letterNo, "@t", code, "@s", DcAudit.Cut(subject, 250), "@b", body, "@f", stored, "@h", sha, "@v", studentCopy ? 1 : 0,
            "@u", DcAccess.Username(), "@e", entryId);
        DcAudit.Write(c, tx, "LETTER", letterId, caseId, "ISSUE", null, new { letterNo = letterNo, template = code, sha1 = sha }, null,
                      "Disciplinary letter " + letterNo + " issued");
    }

    /// <summary>Streams an issued letter to a user who may see the case.</summary>
    public static bool Stream(HttpContext ctx, int letterId, bool inline)
    {
        using (var c = FaDb.Open())
        {
            DataTable t = FaDb.Table(c, null, "SELECT * FROM dc_letter WHERE id=@l", "@l", letterId);
            if (t.Rows.Count == 0) return false;
            DataRow cs = DcAccess.VisibleCase(c, null, FaDb.I(t.Rows[0]["case_id"]), false);
            if (cs == null) return false;
            DcAccess.LogRestrictedAccess(c, cs, "LETTER");
            string path = Path.Combine(DcPaths.CaseFolder(FaDb.I(cs["id"])), Path.GetFileName(FaDb.S(t.Rows[0]["stored_name"])));
            if (!File.Exists(path)) return false;
            ctx.Response.ContentType = "application/pdf";
            ctx.Response.AddHeader("Content-Disposition", (inline ? "inline" : "attachment") + "; filename=\"" + FaDb.S(t.Rows[0]["letter_no"]).Replace("/", "-") + ".pdf\"");
            ctx.Response.AddHeader("X-Content-Type-Options", "nosniff");
            ctx.Response.Cache.SetCacheability(HttpCacheability.Private);
            ctx.Response.TransmitFile(path);
            return true;
        }
    }
}

/// <summary>The letter on University letterhead (DevExpress), one page or more as the text needs.</summary>
public static class DcLetterPdf
{
    private static readonly Color NAVY = Color.FromArgb(0x05, 0x27, 0x5C);
    private static readonly Color ACCENT = Color.FromArgb(0x17, 0x4D, 0xA4);
    private static readonly Color GOLD = Color.FromArgb(0xD4, 0xA0, 0x17);
    private static readonly Color INK = Color.FromArgb(0x1A, 0x1A, 0x2E);
    private static readonly Color MUTE = Color.FromArgb(0x6B, 0x72, 0x80);
    private const string FONT = "Segoe UI";

    private static XRLabel L(string text, float x, float y, float w, float h, float size, FontStyle style, Color fore)
    {
        var l = new XRLabel();
        l.Text = text ?? ""; l.Font = new Font(FONT, size, style); l.ForeColor = fore;
        l.BoundsF = new RectangleF(x, y, w, h); l.Padding = new PaddingInfo(0, 0, 0, 0);
        return l;
    }

    private static XRLabel P(string text, float x, float y, float w, float size, FontStyle style, Color fore)
    {
        var l = L(text, x, y, w, size * 1.9f, size, style, fore);
        l.Multiline = true; l.WordWrap = true; l.CanGrow = true; l.CanShrink = false; l.KeepTogether = false;
        return l;
    }

    private static Image LoadImage(string virtualPath)
    {
        try
        {
            if (string.IsNullOrEmpty(virtualPath)) return null;
            string p = HttpContext.Current.Server.MapPath(virtualPath.StartsWith("~") ? virtualPath : "~" + virtualPath);
            if (!File.Exists(p)) return null;
            using (var fs = new FileStream(p, FileMode.Open, FileAccess.Read))
            using (var tmp = Image.FromStream(fs))
                return new Bitmap(tmp);
        }
        catch { return null; }
    }

    private static Dictionary<string, string> Registrar()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using (var c = FaDb.Open())
                foreach (DataRow r in FaDb.Table(c, null, "SELECT cfg_key, cfg_value FROM admission_letter_config WHERE cfg_key IN ('registrar_name','registrar_title','registrar_sig_path')").Rows)
                    d[FaDb.S(r[0])] = FaDb.S(r[1]);
        }
        catch { }
        return d;
    }

    public static byte[] Build(string letterNo, DateTime date, Dictionary<string, string> f, string subject, string body, bool confidential)
    {
        var rep = new XtraReport();
        rep.PaperKind = PaperKind.A4; rep.Landscape = false;
        rep.Margins = new Margins(64, 64, 40, 50);
        float page = 794f - 128f;

        var head = new ReportHeaderBand(); rep.Bands.Add(head);
        Image crest = LoadImage("~/COOPERP/images/mru-crest.png");
        if (crest != null)
        {
            var pic = new XRPictureBox(); pic.Image = crest; pic.Sizing = ImageSizeMode.ZoomImage; pic.BoundsF = new RectangleF((page - 64) / 2f, 0, 64, 64);
            head.Controls.Add(pic);
        }
        var un = L(DcFmt.University.ToUpperInvariant(), 0, 68, page, 22, 15f, FontStyle.Bold, NAVY); un.TextAlignment = TextAlignment.MiddleCenter; head.Controls.Add(un);
        var of = L(DcSettings.LetterOffice, 0, 90, page, 15, 9.5f, FontStyle.Bold, ACCENT); of.TextAlignment = TextAlignment.MiddleCenter; head.Controls.Add(of);
        string contact = "Kampala, Uganda  |  www.mru.ac.ug" + (DcSettings.ContactDetails == "" ? "" : "  |  " + DcSettings.ContactDetails);
        var ct = L(contact, 0, 106, page, 13, 8f, FontStyle.Regular, MUTE); ct.TextAlignment = TextAlignment.MiddleCenter; head.Controls.Add(ct);
        var r1 = new XRLine(); r1.BoundsF = new RectangleF(0, 124, page, 3); r1.ForeColor = NAVY; r1.LineWidth = 2; head.Controls.Add(r1);
        var r2 = new XRLine(); r2.BoundsF = new RectangleF(0, 127, page, 2); r2.ForeColor = GOLD; r2.LineWidth = 1; head.Controls.Add(r2);
        head.HeightF = 136;

        var d = new DetailBand(); rep.Bands.Add(d);
        float y = 4;
        d.Controls.Add(L("Our ref: " + letterNo, 0, y, page / 2f, 14, 9f, FontStyle.Regular, INK));
        var dt = L(date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture), page / 2f, y, page / 2f, 14, 9f, FontStyle.Regular, INK);
        dt.TextAlignment = TextAlignment.TopRight; d.Controls.Add(dt);
        y += 22;
        if (confidential) { d.Controls.Add(L("CONFIDENTIAL", 0, y, page, 14, 9f, FontStyle.Bold, NAVY)); y += 20; }

        string addr = Get(f, "student_name") + "\nRegistration number: " + Get(f, "regno") +
                      (Get(f, "programme") == "" ? "" : "\n" + Get(f, "programme")) + (Get(f, "campus") == "" ? "" : "\n" + Get(f, "campus"));
        var a = P(addr, 0, y, page * 0.7f, 9.5f, FontStyle.Regular, INK); d.Controls.Add(a);
        y += 30;   // the address grows downwards and pushes what follows
        d.Controls.Add(P("Dear " + Get(f, "student_name") + ",", 0, y, page, 10f, FontStyle.Regular, INK)); y += 26;
        var sj = P(("RE: " + subject).ToUpperInvariant(), 0, y, page, 10f, FontStyle.Bold | FontStyle.Underline, INK); d.Controls.Add(sj); y += 34;
        foreach (string para in Regex.Split((body ?? "").Replace("\r", ""), @"\n\s*\n"))
        {
            string t = para.Trim();
            if (t == "") continue;
            var pl = P(t, 0, y, page, 10f, FontStyle.Regular, INK); pl.TextAlignment = TextAlignment.TopJustify; d.Controls.Add(pl);
            y += 30;
        }
        y += 8;
        d.Controls.Add(P("Yours faithfully,", 0, y, page, 10f, FontStyle.Regular, INK)); y += 22;
        var reg = Registrar();
        string sig; reg.TryGetValue("registrar_sig_path", out sig);
        Image sigImg = LoadImage(sig);
        if (sigImg != null)
        {
            var sp = new XRPictureBox(); sp.Image = sigImg; sp.Sizing = ImageSizeMode.ZoomImage; sp.BoundsF = new RectangleF(0, y, 150, 48); d.Controls.Add(sp);
        }
        y += 52;
        var ln = new XRLine(); ln.BoundsF = new RectangleF(0, y, 210, 2); ln.ForeColor = INK; d.Controls.Add(ln); y += 4;
        string rn, rt; reg.TryGetValue("registrar_name", out rn); reg.TryGetValue("registrar_title", out rt);
        d.Controls.Add(P(string.IsNullOrEmpty(rn) ? "Academic Registrar" : rn, 0, y, page, 10f, FontStyle.Bold, INK)); y += 18;
        d.Controls.Add(P(DcLookups.Title(string.IsNullOrEmpty(rt) ? "ACADEMIC REGISTRAR" : rt), 0, y, page, 9.5f, FontStyle.Regular, INK)); y += 18;
        d.Controls.Add(P("For: Students Disciplinary Committee", 0, y, page, 9f, FontStyle.Regular, MUTE)); y += 26;
        d.Controls.Add(P("cc: " + DcSettings.ContactOffice + "; Student file", 0, y, page, 8.5f, FontStyle.Regular, MUTE)); y += 20;
        d.HeightF = y + 4;

        var pf = new PageFooterBand(); pf.HeightF = 22; rep.Bands.Add(pf);
        var pr = new XRLine(); pr.BoundsF = new RectangleF(0, 0, page, 2); pr.ForeColor = Color.FromArgb(0xD8, 0xDF, 0xE8); pf.Controls.Add(pr);
        pf.Controls.Add(L(DcFmt.University + ". Letter " + letterNo, 0, 5, page - 150, 12, 7f, FontStyle.Regular, MUTE));
        var pi = new XRPageInfo(); pi.PageInfo = DevExpress.XtraPrinting.PageInfo.NumberOfTotal; pi.Format = "Page {0} of {1}";
        pi.Font = new Font(FONT, 7f); pi.ForeColor = MUTE; pi.TextAlignment = TextAlignment.MiddleRight; pi.BoundsF = new RectangleF(page - 150, 5, 150, 12);
        pf.Controls.Add(pi);

        using (var ms = new MemoryStream())
        {
            rep.ExportToPdf(ms);
            return ms.ToArray();
        }
    }

    private static string Get(Dictionary<string, string> f, string k) { string v; return f.TryGetValue(k, out v) ? (v ?? "") : ""; }
}
