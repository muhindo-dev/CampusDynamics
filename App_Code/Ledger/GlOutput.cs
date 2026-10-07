using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;

// =====================================================================
//  General Ledger: Excel, CSV and PDF from a report result.
//  The files carry exactly the rows, totals and checks the screen shows
//  for the same parameters, search and sort. Adapted from the Fixed
//  Assets writers (FaExport, FaPdf); those classes are left untouched.
// =====================================================================

public static class GlExport
{
    public const int MaxRowsPdf = 20000;
    public const int MaxRowsFile = 500000;

    public static string FileBase(GlResult r)
    {
        string what = new string((r.FileWhat ?? "report").Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-').ToArray());
        while (what.Contains("--")) what = what.Replace("--", "-");
        return "MRU-ledger-" + what.Trim('-') + "-" + DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
    }

    private static string X(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length + 16);
        foreach (char ch in s)
        {
            if (ch == '&') sb.Append("&amp;"); else if (ch == '<') sb.Append("&lt;"); else if (ch == '>') sb.Append("&gt;");
            else if (ch == '"') sb.Append("&quot;"); else if (ch < 32 && ch != '\t' && ch != '\n' && ch != '\r') sb.Append(' '); else sb.Append(ch);
        }
        return sb.ToString();
    }

    private static void Cell(StringBuilder sb, string style, object v, GlCol col)
    {
        bool num = col != null && col.Numeric && v != null && !(v is DBNull) && (v is decimal || v is long || v is int || v is double);
        string text = num ? Convert.ToDecimal(v, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) : (col == null ? Convert.ToString(v, CultureInfo.InvariantCulture) : GlReports.Cell(col, v));
        if (num && col.Kind == "pct") text = (Convert.ToDecimal(v, CultureInfo.InvariantCulture) / 100m).ToString(CultureInfo.InvariantCulture);
        string st = style;
        if (num) st += col.Kind == "pct" ? "Pct" : col.Kind == "count" ? "Cnt" : "Num";
        sb.Append("<Cell ss:StyleID=\"").Append(st).Append("\"><Data ss:Type=\"").Append(num ? "Number" : "String").Append("\">").Append(X(text)).Append("</Data></Cell>");
    }

    private static void TextRow(StringBuilder sb, string style, params string[] cells)
    {
        sb.Append("<Row>");
        foreach (string c in cells) sb.Append("<Cell ss:StyleID=\"").Append(style).Append("\"><Data ss:Type=\"String\">").Append(X(c)).Append("</Data></Cell>");
        sb.AppendLine("</Row>");
    }

    private static string Styles()
    {
        string border = "<Borders><Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#E0E5ED\"/></Borders>";
        string top = "<Borders><Border ss:Position=\"Top\" ss:LineStyle=\"Continuous\" ss:Weight=\"2\" ss:Color=\"#05275C\"/></Borders>";
        var sb = new StringBuilder("<Styles>");
        sb.Append("<Style ss:ID=\"sTitle\"><Font ss:Bold=\"1\" ss:Size=\"15\" ss:Color=\"#05275C\"/></Style>");
        sb.Append("<Style ss:ID=\"sRpt\"><Font ss:Bold=\"1\" ss:Size=\"11\" ss:Color=\"#174DA4\"/></Style>");
        sb.Append("<Style ss:ID=\"sSub\"><Font ss:Size=\"9\" ss:Color=\"#555555\"/><Alignment ss:WrapText=\"1\"/></Style>");
        sb.Append("<Style ss:ID=\"sHdr\"><Font ss:Bold=\"1\" ss:Size=\"10\" ss:Color=\"#FFFFFF\"/><Interior ss:Color=\"#05275C\" ss:Pattern=\"Solid\"/><Alignment ss:Vertical=\"Center\" ss:WrapText=\"1\"/></Style>");
        sb.Append("<Style ss:ID=\"sLbl\"><Font ss:Bold=\"1\" ss:Size=\"10\" ss:Color=\"#174DA4\"/></Style>");
        foreach (var k in new[] { new[] { "c", "", "" }, new[] { "h", "<Font ss:Bold=\"1\" ss:Size=\"10\" ss:Color=\"#05275C\"/>", "<Interior ss:Color=\"#F0F4FA\" ss:Pattern=\"Solid\"/>" },
                                  new[] { "s", "<Font ss:Bold=\"1\" ss:Size=\"10\"/>", "<Interior ss:Color=\"#E6ECF5\" ss:Pattern=\"Solid\"/>" }, new[] { "t", "<Font ss:Bold=\"1\" ss:Size=\"10\" ss:Color=\"#05275C\"/>", "<Interior ss:Color=\"#E6ECF5\" ss:Pattern=\"Solid\"/>" } })
        {
            string font = k[1] == "" ? "<Font ss:Size=\"10\"/>" : k[1], b = k[0] == "t" ? top : border;
            sb.Append("<Style ss:ID=\"" + k[0] + "\">" + font + k[2] + b + "<Alignment ss:Vertical=\"Top\" ss:WrapText=\"1\"/></Style>");
            sb.Append("<Style ss:ID=\"" + k[0] + "Num\">" + font + k[2] + b + "<Alignment ss:Horizontal=\"Right\" ss:Vertical=\"Top\"/><NumberFormat ss:Format=\"#,##0;(#,##0);0\"/></Style>");
            sb.Append("<Style ss:ID=\"" + k[0] + "Cnt\">" + font + k[2] + b + "<Alignment ss:Horizontal=\"Right\" ss:Vertical=\"Top\"/><NumberFormat ss:Format=\"#,##0\"/></Style>");
            sb.Append("<Style ss:ID=\"" + k[0] + "Pct\">" + font + k[2] + b + "<Alignment ss:Horizontal=\"Right\" ss:Vertical=\"Top\"/><NumberFormat ss:Format=\"0.0%\"/></Style>");
        }
        sb.Append("<Style ss:ID=\"sFail\"><Font ss:Bold=\"1\" ss:Size=\"10\" ss:Color=\"#B42318\"/></Style><Style ss:ID=\"sPass\"><Font ss:Bold=\"1\" ss:Size=\"10\" ss:Color=\"#166534\"/></Style>");
        sb.Append("<Style ss:ID=\"sFoot\"><Font ss:Size=\"8\" ss:Italic=\"1\" ss:Color=\"#777777\"/></Style>");
        sb.Append("</Styles>");
        return sb.ToString();
    }

    private static List<int> Visible(GlResult r, HashSet<string> keep)
    {
        return Enumerable.Range(0, r.Cols.Count).Where(i => !r.Cols[i].Hidden && (keep == null || keep.Count == 0 || keep.Contains(r.Cols[i].Key))).ToList();
    }

    private static IEnumerable<GlCheck> Flat(List<GlCheck> l)
    {
        foreach (GlCheck c in l)
        {
            yield return c;
            if (c.parts != null) foreach (GlCheck p in c.parts) yield return new GlCheck { code = c.code + "." + p.code, title = "    " + p.title, status = p.status, amount = p.amount, count = p.count, cause = p.cause };
        }
    }

    public static void Workbook(HttpResponse resp, GlResult r, HashSet<string> keep)
    {
        var vis = Visible(r, keep);
        var sb = new StringBuilder(1024 * 128);
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?><?mso-application progid=\"Excel.Sheet\"?>");
        sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:x=\"urn:schemas-microsoft-com:office:excel\" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
        sb.AppendLine(Styles());

        // Cover
        sb.AppendLine("<Worksheet ss:Name=\"Cover\"><Table><Column ss:Width=\"170\"/><Column ss:Width=\"420\"/>");
        TextRow(sb, "sTitle", GlFmt.University); TextRow(sb, "sSub", GlFmt.Office); TextRow(sb, "sRpt", r.Title);
        if (!string.IsNullOrEmpty(r.Subtitle)) TextRow(sb, "sSub", r.Subtitle);
        sb.AppendLine("<Row></Row>");
        Action<string, string> kv = (k, v) => { sb.Append("<Row>"); sb.Append("<Cell ss:StyleID=\"sLbl\"><Data ss:Type=\"String\">" + X(k) + "</Data></Cell><Cell ss:StyleID=\"c\"><Data ss:Type=\"String\">" + X(v) + "</Data></Cell>"); sb.AppendLine("</Row>"); };
        kv("Generated", DateTime.Now.ToString("d MMMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture));
        kv("Generated by", GlAccess.User());
        foreach (var p in r.Cover) kv(p.Key, p.Value ?? "");
        kv("Rows", r.Rows.Count.ToString("#,##0", CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(r.Basis)) kv("What the figures include", r.Basis);
        int fails = r.Checks.Count(c => c.status == "fail");
        kv("Checks", r.Checks.Count == 0 ? "None" : fails == 0 ? "All passed" : fails + " of " + r.Checks.Count + " need attention (see the Checks sheet)");
        sb.AppendLine("<Row></Row>");
        TextRow(sb, "sFoot", "Figures are read from the ledger through a read-only account. Nothing in the ledger was changed to produce them.");
        sb.AppendLine("</Table></Worksheet>");

        // Data
        sb.AppendLine("<Worksheet ss:Name=\"Report\"><Table>");
        foreach (int i in vis) sb.AppendLine("<Column ss:AutoFitWidth=\"0\" ss:Width=\"" + (int)Math.Max(45, Math.Min(320, r.Cols[i].Width * 1.3f)) + "\"/>");
        TextRow(sb, "sTitle", GlFmt.University);
        TextRow(sb, "sRpt", r.Title + (string.IsNullOrEmpty(r.Subtitle) ? "" : ", " + r.Subtitle));
        TextRow(sb, "sSub", "Generated " + DateTime.Now.ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture) + ". " + r.Rows.Count.ToString("#,##0") + " rows");
        sb.AppendLine("<Row></Row>");
        sb.Append("<Row ss:Height=\"22\">");
        foreach (int i in vis) sb.Append("<Cell ss:StyleID=\"sHdr\"><Data ss:Type=\"String\">" + X(r.Cols[i].Header) + "</Data></Cell>");
        sb.AppendLine("</Row>");
        for (int n = 0; n < r.Rows.Count; n++)
        {
            string k = r.Kinds[n] == "" ? "c" : r.Kinds[n];
            sb.Append("<Row>");
            foreach (int i in vis) Cell(sb, k, i < r.Rows[n].Length ? r.Rows[n][i] : null, r.Cols[i]);
            sb.AppendLine("</Row>");
        }
        if (r.Totals != null)
        {
            sb.Append("<Row>"); bool labelled = false;
            foreach (int i in vis)
            {
                object v = r.Totals[i];
                if (v == null && !labelled && !r.Cols[i].Numeric) { sb.Append("<Cell ss:StyleID=\"t\"><Data ss:Type=\"String\">Total</Data></Cell>"); labelled = true; }
                else Cell(sb, "t", v, r.Cols[i]);
            }
            sb.AppendLine("</Row>");
        }
        sb.AppendLine("</Table><WorksheetOptions xmlns=\"urn:schemas-microsoft-com:office:excel\"><FreezePanes/><FrozenNoSplit/><SplitHorizontal>5</SplitHorizontal><TopRowBottomPane>5</TopRowBottomPane><ActivePane>2</ActivePane></WorksheetOptions></Worksheet>");

        // Checks
        if (r.Checks.Count > 0)
        {
            sb.AppendLine("<Worksheet ss:Name=\"Checks\"><Table><Column ss:Width=\"60\"/><Column ss:Width=\"320\"/><Column ss:Width=\"110\"/><Column ss:Width=\"70\"/><Column ss:Width=\"460\"/>");
            TextRow(sb, "sRpt", "Checks for " + r.Title);
            sb.AppendLine("<Row></Row>");
            sb.Append("<Row>"); foreach (string h in new[] { "Result", "Check", "Amount", "Count", "Cause and what to do" }) sb.Append("<Cell ss:StyleID=\"sHdr\"><Data ss:Type=\"String\">" + h + "</Data></Cell>"); sb.AppendLine("</Row>");
            foreach (GlCheck c in Flat(r.Checks))
            {
                sb.Append("<Row>");
                sb.Append("<Cell ss:StyleID=\"" + (c.status == "fail" ? "sFail" : c.status == "pass" ? "sPass" : "sLbl") + "\"><Data ss:Type=\"String\">" + (c.status == "fail" ? "Needs attention" : c.status == "pass" ? "Passed" : "Note") + "</Data></Cell>");
                sb.Append("<Cell ss:StyleID=\"c\"><Data ss:Type=\"String\">" + X(c.title) + "</Data></Cell>");
                sb.Append("<Cell ss:StyleID=\"cNum\"><Data ss:Type=\"Number\">" + c.amount.ToString(CultureInfo.InvariantCulture) + "</Data></Cell>");
                sb.Append("<Cell ss:StyleID=\"cCnt\"><Data ss:Type=\"Number\">" + c.count.ToString(CultureInfo.InvariantCulture) + "</Data></Cell>");
                sb.Append("<Cell ss:StyleID=\"c\"><Data ss:Type=\"String\">" + X((c.cause ?? "") + (c.fix != null ? " Fix: " + c.fix.label + "." : "")) + "</Data></Cell>");
                sb.AppendLine("</Row>");
            }
            sb.AppendLine("</Table></Worksheet>");
        }
        sb.AppendLine("</Workbook>");
        FaExport.Send(resp, sb.ToString(), "application/vnd.ms-excel", FileBase(r) + ".xls");
    }

    public static void Csv(HttpResponse resp, GlResult r, HashSet<string> keep)
    {
        var vis = Visible(r, keep);
        var sb = new StringBuilder(1024 * 64);
        sb.Append('﻿');
        sb.AppendLine("# " + GlFmt.University);
        sb.AppendLine("# " + r.Title + (string.IsNullOrEmpty(r.Subtitle) ? "" : ", " + r.Subtitle));
        sb.AppendLine("# Generated: " + DateTime.Now.ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture) + " by " + GlAccess.User());
        foreach (var p in r.Cover) sb.AppendLine("# " + p.Key + ": " + (p.Value ?? ""));
        if (!string.IsNullOrEmpty(r.Basis)) sb.AppendLine("# Basis: " + r.Basis);
        sb.AppendLine("# Rows: " + r.Rows.Count.ToString(CultureInfo.InvariantCulture));
        foreach (GlCheck c in Flat(r.Checks)) sb.AppendLine("# Check: " + (c.status == "fail" ? "NEEDS ATTENTION" : c.status == "pass" ? "passed" : "note") + ", " + c.title.Trim() + (c.amount != 0 ? ", " + c.amount.ToString(CultureInfo.InvariantCulture) : ""));
        sb.AppendLine("#");
        sb.AppendLine(Line(vis.Select(i => (object)r.Cols[i].Header).ToList(), null, vis, r));
        for (int n = 0; n < r.Rows.Count; n++) sb.AppendLine(Line(vis.Select(i => i < r.Rows[n].Length ? r.Rows[n][i] : null).ToList(), r.Kinds[n], vis, r));
        if (r.Totals != null) sb.AppendLine(Line(vis.Select(i => r.Totals[i]).ToList(), "t", vis, r));
        FaExport.Send(resp, sb.ToString(), "text/csv", FileBase(r) + ".csv");
    }

    private static string Line(List<object> cells, string kind, List<int> vis, GlResult r)
    {
        var sb = new StringBuilder();
        for (int j = 0; j < cells.Count; j++)
        {
            if (j > 0) sb.Append(',');
            object o = cells[j]; GlCol col = kind == null ? null : r.Cols[vis[j]];
            string v;
            if (o == null || o is DBNull) v = (kind == "t" && j == 0) ? "Total" : "";
            else if (col != null && col.Numeric && (o is decimal || o is long || o is int)) v = Convert.ToDecimal(o, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            else if (o is DateTime) v = GlFmt.Iso((DateTime)o);
            else { v = Convert.ToString(o, CultureInfo.InvariantCulture); if (v.Length > 0 && "=+-@".IndexOf(v[0]) >= 0) v = "'" + v; }
            if (v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0) sb.Append('"').Append(v.Replace("\"", "\"\"")).Append('"'); else sb.Append(v);
        }
        return sb.ToString();
    }
}

public static class GlPdf
{
    private static readonly Color NAVY = Color.FromArgb(0x05, 0x27, 0x5C);
    private static readonly Color ACCENT = Color.FromArgb(0x17, 0x4D, 0xA4);
    private static readonly Color INK = Color.FromArgb(0x1A, 0x1A, 0x2E);
    private static readonly Color MUTE = Color.FromArgb(0x6B, 0x72, 0x80);
    private static readonly Color LINE = Color.FromArgb(0xD8, 0xDF, 0xE8);
    private static readonly Color BAND = Color.FromArgb(0xEE, 0xF3, 0xF9);
    private static readonly Color TOT = Color.FromArgb(0xE6, 0xEC, 0xF5);
    private static readonly Color BAD = Color.FromArgb(0xB4, 0x23, 0x18);
    private static readonly Color OK = Color.FromArgb(0x16, 0x65, 0x34);
    private const string FONT = "Segoe UI";

    private static Image Crest()
    {
        try
        {
            string p = HttpContext.Current.Server.MapPath("~/COOPERP/images/mru-crest.png");
            if (File.Exists(p)) using (var fs = new FileStream(p, FileMode.Open, FileAccess.Read)) using (var tmp = Image.FromStream(fs)) return new Bitmap(tmp);
        }
        catch { }
        return null;
    }

    private static XRLabel L(string text, float x, float y, float w, float h, float size, FontStyle style, Color fore)
    {
        var l = new XRLabel(); l.Text = text ?? ""; l.Font = new Font(FONT, size, style); l.ForeColor = fore;
        l.BoundsF = new RectangleF(x, y, w, h); l.Padding = new PaddingInfo(0, 0, 0, 0); return l;
    }

    public static void Send(HttpResponse resp, GlResult r, HashSet<string> keep)
    {
        var vis = Enumerable.Range(0, r.Cols.Count).Where(i => !r.Cols[i].Hidden && (keep == null || keep.Count == 0 || keep.Contains(r.Cols[i].Key))).ToList();
        var t = new DataTable();
        t.Columns.Add("_kind", typeof(string));
        foreach (int i in vis) t.Columns.Add("c" + i, typeof(string));
        for (int n = 0; n < r.Rows.Count; n++)
        {
            DataRow dr = t.NewRow(); dr["_kind"] = r.Kinds[n] ?? "";
            foreach (int i in vis) dr["c" + i] = GlReports.Cell(r.Cols[i], i < r.Rows[n].Length ? r.Rows[n][i] : null);
            t.Rows.Add(dr);
        }

        var rep = new XtraReport { PaperKind = PaperKind.A4, DataSource = t };
        float need = vis.Sum(i => r.Cols[i].Width);
        rep.Landscape = need > 545f;
        rep.Margins = new Margins(36, 36, 34, 40);
        float page = (rep.Landscape ? 1123f : 794f) - 72f;
        float scale = need > 0 ? page / need : 1f;
        var w = vis.Select(i => r.Cols[i].Width * scale).ToArray();

        var head = new ReportHeaderBand(); rep.Bands.Add(head);
        Image crest = Crest(); float tl = 0;
        if (crest != null) { var pic = new XRPictureBox { Image = crest, Sizing = ImageSizeMode.ZoomImage, BoundsF = new RectangleF(0, 0, 58, 58) }; head.Controls.Add(pic); tl = 68; }
        head.Controls.Add(L(GlFmt.University.ToUpperInvariant(), tl, 2, page - tl, 22, 15f, FontStyle.Bold, NAVY));
        head.Controls.Add(L(GlFmt.Office, tl, 23, page - tl, 14, 8.5f, FontStyle.Regular, MUTE));
        head.Controls.Add(L(r.Title.ToUpperInvariant() + (string.IsNullOrEmpty(r.Subtitle) ? "" : ", " + r.Subtitle.ToUpperInvariant()), tl, 39, page - tl, 17, 10.5f, FontStyle.Bold, ACCENT));
        var rule = new XRLine { BoundsF = new RectangleF(0, 62, page, 3), ForeColor = NAVY, LineWidth = 2 }; head.Controls.Add(rule);
        float y = 71;
        foreach (var kv in r.Cover.Take(8))
        {
            head.Controls.Add(L(kv.Key.ToUpperInvariant(), 0, y, 130, 11, 7f, FontStyle.Bold, MUTE));
            var v = L(kv.Value ?? "", 130, y, page - 130, 11, 8f, FontStyle.Regular, INK); v.WordWrap = false; head.Controls.Add(v); y += 13;
        }
        if (!string.IsNullOrEmpty(r.Basis))
        {
            head.Controls.Add(L("BASIS", 0, y, 130, 11, 7f, FontStyle.Bold, MUTE));
            var b = L(r.Basis, 130, y, page - 130, 24, 7.5f, FontStyle.Regular, INK); b.Multiline = true; b.CanGrow = true; head.Controls.Add(b); y += 27;
        }
        head.Controls.Add(L("EXTRACTED", 0, y, 130, 11, 7f, FontStyle.Bold, MUTE));
        head.Controls.Add(L(DateTime.Now.ToString("d MMMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture) + " by " + GlAccess.User() + ". " + r.Rows.Count.ToString("#,##0") + " rows", 130, y, page - 130, 11, 8f, FontStyle.Regular, INK));
        y += 17; head.HeightF = y;

        var ph = new PageHeaderBand { HeightF = 23 }; rep.Bands.Add(ph);
        float x = 0;
        for (int j = 0; j < vis.Count; j++)
        {
            GlCol c = r.Cols[vis[j]];
            var h = L(c.Header.ToUpperInvariant(), x, 0, w[j], 22, 6.5f, FontStyle.Bold, Color.White);
            h.TextAlignment = c.Numeric ? TextAlignment.MiddleRight : TextAlignment.MiddleLeft; h.BackColor = NAVY; h.Padding = new PaddingInfo(3, 3, 0, 0); h.WordWrap = true;
            ph.Controls.Add(h); x += w[j];
        }

        var bold = new FormattingRule { Name = "glBold", Condition = "[_kind] <> ''" };
        bold.Formatting.Font = new Font(FONT, 8f, FontStyle.Bold); bold.Formatting.BackColor = TOT; bold.Formatting.ForeColor = NAVY;
        rep.FormattingRuleSheet.Add(bold);

        var detail = new DetailBand { HeightF = 15f, KeepTogether = true }; rep.Bands.Add(detail);
        var table = new XRTable(); table.BeginInit(); table.BoundsF = new RectangleF(0, 0, page, 15f);
        var row = new XRTableRow { HeightF = 15f }; table.Rows.Add(row);
        for (int j = 0; j < vis.Count; j++)
        {
            GlCol c = r.Cols[vis[j]];
            var cell = new XRTableCell(); cell.DataBindings.Add("Text", null, "c" + vis[j]);
            cell.Font = new Font(FONT, 7.5f); cell.ForeColor = INK; cell.TextAlignment = c.Numeric ? TextAlignment.TopRight : TextAlignment.TopLeft;
            cell.WidthF = w[j]; cell.Padding = new PaddingInfo(3, 3, 2, 1); cell.Borders = BorderSide.Bottom; cell.BorderColor = LINE; cell.WordWrap = !c.Numeric; cell.CanGrow = true;
            cell.FormattingRules.Add(bold);
            row.Cells.Add(cell);
        }
        table.EndInit(); detail.Controls.Add(table);
        detail.EvenStyleName = "even"; var even = new XRControlStyle { Name = "even", BackColor = BAND }; rep.StyleSheet.Add(even);

        var foot = new ReportFooterBand(); rep.Bands.Add(foot);
        float fy = 0;
        if (r.Totals != null)
        {
            x = 0; bool labelled = false;
            for (int j = 0; j < vis.Count; j++)
            {
                GlCol c = r.Cols[vis[j]]; object v = r.Totals[vis[j]];
                string text = v != null ? GlReports.Cell(c, v) : (!labelled && !c.Numeric ? "Total" : "");
                if (text == "Total") labelled = true;
                var l = L(text, x, 0, w[j], 16, 7.5f, FontStyle.Bold, NAVY); l.BackColor = TOT; l.Borders = BorderSide.Top; l.BorderColor = NAVY; l.BorderWidth = 1;
                l.Padding = new PaddingInfo(3, 3, 0, 0); l.TextAlignment = c.Numeric ? TextAlignment.MiddleRight : TextAlignment.MiddleLeft; l.WordWrap = false;
                foot.Controls.Add(l); x += w[j];
            }
            fy = 22;
        }
        if (r.Checks.Count > 0)
        {
            foot.Controls.Add(L("CHECKS", 0, fy + 6, page, 13, 8f, FontStyle.Bold, NAVY)); fy += 22;
            foreach (GlCheck c in r.Checks.Take(14))
            {
                string res = c.status == "fail" ? "Needs attention" : c.status == "pass" ? "Passed" : "Note";
                foot.Controls.Add(L(res, 0, fy, 80, 12, 7.5f, FontStyle.Bold, c.status == "fail" ? BAD : c.status == "pass" ? OK : MUTE));
                foot.Controls.Add(L(c.title + (c.amount != 0 ? ": " + GlFmt.Money(c.amount) : ""), 80, fy, page - 80, 12, 7.5f, FontStyle.Bold, INK)); fy += 12;
                if (!string.IsNullOrEmpty(c.cause)) { var cl = L(c.cause, 80, fy, page - 80, 11, 7f, FontStyle.Regular, MUTE); cl.Multiline = true; cl.CanGrow = true; foot.Controls.Add(cl); fy += 12 + (c.cause.Length / 190) * 9; }
                if (c.parts != null) foreach (GlCheck p in c.parts) { foot.Controls.Add(L(p.title + ": " + GlFmt.Plural(p.count, "voucher", "vouchers") + ", " + GlFmt.Money(p.amount), 96, fy, page - 96, 11, 7f, FontStyle.Regular, INK)); fy += 11; }
                fy += 4;
            }
        }
        var fn = L("Figures are read from the ledger through a read-only account; nothing was changed to produce them.", 0, fy + 6, page, 12, 7f, FontStyle.Italic, MUTE); foot.Controls.Add(fn); fy += 20;
        int ns = r.Signatories.Length; float sw = (page - 30f * (ns - 1)) / ns;
        for (int i = 0; i < ns; i++)
        {
            float sx = i * (sw + 30);
            foot.Controls.Add(new XRLine { BoundsF = new RectangleF(sx, fy + 36, sw, 2), ForeColor = INK });
            foot.Controls.Add(L(r.Signatories[i], sx, fy + 40, sw, 12, 8f, FontStyle.Bold, INK));
            foot.Controls.Add(L("Name, signature and date", sx, fy + 52, sw, 11, 7f, FontStyle.Regular, MUTE));
        }
        foot.HeightF = fy + 70;

        var pf = new PageFooterBand { HeightF = 22 }; rep.Bands.Add(pf);
        pf.Controls.Add(new XRLine { BoundsF = new RectangleF(0, 0, page, 2), ForeColor = LINE });
        pf.Controls.Add(L(GlFmt.University + ". " + r.Title, 0, 5, page - 150, 12, 7f, FontStyle.Regular, MUTE));
        var pi = new XRPageInfo { PageInfo = DevExpress.XtraPrinting.PageInfo.NumberOfTotal, Format = "Page {0} of {1}", Font = new Font(FONT, 7f), ForeColor = MUTE, TextAlignment = TextAlignment.MiddleRight, BoundsF = new RectangleF(page - 150, 5, 150, 12) };
        pf.Controls.Add(pi);

        using (var ms = new MemoryStream()) { rep.ExportToPdf(ms); FaPdf.WritePdf(resp, ms.ToArray(), GlExport.FileBase(r)); }
    }
}
