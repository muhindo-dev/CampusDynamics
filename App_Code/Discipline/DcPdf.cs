using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Web;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;

// =====================================================================
//  Student Disciplinary module: report PDFs (DevExpress XtraReports v16.1).
//  The Fixed Assets report builder (itself adapted from GraduationPdf)
//  with the office line, foot note and signatories set per report.
//  Letters are built separately in DcLetterPdf.
// =====================================================================
public static class DcPdf
{
    private static readonly Color NAVY = Color.FromArgb(0x05, 0x27, 0x5C);
    private static readonly Color ACCENT = Color.FromArgb(0x17, 0x4D, 0xA4);
    private static readonly Color INK = Color.FromArgb(0x1A, 0x1A, 0x2E);
    private static readonly Color MUTE = Color.FromArgb(0x6B, 0x72, 0x80);
    private static readonly Color LINE = Color.FromArgb(0xD8, 0xDF, 0xE8);
    private static readonly Color BAND = Color.FromArgb(0xEE, 0xF3, 0xF9);
    private static readonly Color TOT = Color.FromArgb(0xE6, 0xEC, 0xF5);
    private const string FONT = "Segoe UI";
    public const string GROUP_COL = "_grp";
    public const string SEQ_COL = "_seq";
    public const string GCOUNT_COL = "_gcount";

    public class PCol
    {
        public string Field, Header;
        public float Width;
        public bool Right, Sum, Blank;
        public PCol(string field, string header, float width, bool right, bool sum) { Field = field; Header = header; Width = width; Right = right; Sum = sum; }
    }

    public class Spec
    {
        public string FileBase, Title, Subtitle;
        public List<KeyValuePair<string, string>> Cover = new List<KeyValuePair<string, string>>();
        public List<PCol> Cols = new List<PCol>();
        public DataTable Rows;
        public bool Grouped;                 // rows carry GROUP_COL
        public bool PageBreakPerGroup;
        public string GroupFooterNote;       // printed under each group with a signature line (custody list)
        public string[] Signatories = { "Prepared by", "Checked by (Dean of Students)", "Approved by (Academic Registrar)" };
        public string Office = "Office of the Dean of Students";
        public string FootNote = "Taken from the disciplinary records. Confidential: for official use only.";
        public float RowHeight = 15f;
        public bool ShowTotals = true;
        public bool? Landscape;
        public string Noun = "case";
        public string NounPlural = "cases";
    }

    private static Image Crest()
    {
        try
        {
            string p = HttpContext.Current.Server.MapPath("~/COOPERP/images/mru-crest.png");
            if (File.Exists(p))
                using (var fs = new FileStream(p, FileMode.Open, FileAccess.Read))
                using (var tmp = Image.FromStream(fs))
                    return new Bitmap(tmp);
        }
        catch { }
        return null;
    }

    private static XRLabel L(string text, float x, float y, float w, float h, float size, FontStyle style, Color fore)
    {
        var l = new XRLabel();
        l.Text = text ?? ""; l.Font = new Font(FONT, size, style); l.ForeColor = fore;
        l.BoundsF = new RectangleF(x, y, w, h); l.Padding = new PaddingInfo(0, 0, 0, 0);
        return l;
    }

    /// <summary>Numbers rows within each group and records each group's count.</summary>
    private static void Number(DataTable t, bool grouped, string noun, string nounPlural)
    {
        if (!t.Columns.Contains(SEQ_COL)) t.Columns.Add(SEQ_COL, typeof(string));
        if (!t.Columns.Contains(GCOUNT_COL)) t.Columns.Add(GCOUNT_COL, typeof(string));
        var tally = new Dictionary<string, int>();
        foreach (DataRow r in t.Rows) { string k = grouped ? Convert.ToString(r[GROUP_COL]) : ""; tally[k] = (tally.ContainsKey(k) ? tally[k] : 0) + 1; }
        var seen = new Dictionary<string, int>();
        foreach (DataRow r in t.Rows)
        {
            string k = grouped ? Convert.ToString(r[GROUP_COL]) : "";
            int n; seen.TryGetValue(k, out n); n++; seen[k] = n;
            r[SEQ_COL] = n.ToString(CultureInfo.InvariantCulture);
            r[GCOUNT_COL] = tally[k].ToString("#,##0") + " " + (tally[k] == 1 ? noun : nounPlural);
        }
    }

    public static void Send(HttpResponse resp, Spec s)
    {
        XtraReport rep = Build(s);
        using (var ms = new MemoryStream())
        {
            rep.ExportToPdf(ms);
            WritePdf(resp, ms.ToArray(), s.FileBase);
        }
    }

    public static void WritePdf(HttpResponse resp, byte[] bytes, string fileBase)
    {
        resp.Clear();
        resp.ContentType = "application/pdf";
        resp.AddHeader("Content-Disposition", "attachment; filename=\"" + fileBase + ".pdf\"");
        resp.BinaryWrite(bytes);
        resp.Flush();
        resp.SuppressContent = true;
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    private static XtraReport Build(Spec s)
    {
        DataTable rows = s.Rows;
        bool grouped = s.Grouped && rows.Columns.Contains(GROUP_COL);
        Number(rows, grouped, s.Noun, s.NounPlural);

        var rep = new XtraReport();
        rep.PaperKind = PaperKind.A4;
        rep.DataSource = rows;
        float need = 0; foreach (var c in s.Cols) need += c.Width;
        rep.Landscape = s.Landscape ?? (need > 545f);
        rep.Margins = new Margins(40, 40, 36, 44);
        float page = (rep.Landscape ? 1123f : 794f) - 80f;
        float seqW = 28f;
        float scale = need > 0 ? (page - seqW) / need : 1f;
        var w = new float[s.Cols.Count];
        for (int i = 0; i < s.Cols.Count; i++) w[i] = s.Cols[i].Width * scale;

        // Letterhead and certification block
        var head = new ReportHeaderBand(); rep.Bands.Add(head);
        Image crest = Crest(); float tl = 0;
        if (crest != null)
        {
            var pic = new XRPictureBox(); pic.Image = crest; pic.Sizing = ImageSizeMode.ZoomImage; pic.BoundsF = new RectangleF(0, 0, 58, 58);
            head.Controls.Add(pic); tl = 68;
        }
        head.Controls.Add(L(DcFmt.University.ToUpperInvariant(), tl, 2, page - tl, 22, 15f, FontStyle.Bold, NAVY));
        head.Controls.Add(L(s.Office, tl, 23, page - tl, 14, 8.5f, FontStyle.Regular, MUTE));
        head.Controls.Add(L(s.Title.ToUpperInvariant() + (string.IsNullOrEmpty(s.Subtitle) ? "" : ", " + s.Subtitle.ToUpperInvariant()),
                            tl, 39, page - tl, 17, 10.5f, FontStyle.Bold, ACCENT));
        var rule = new XRLine(); rule.BoundsF = new RectangleF(0, 62, page, 3); rule.ForeColor = NAVY; rule.LineWidth = 2; head.Controls.Add(rule);
        float y = 71;
        foreach (var kv in s.Cover)
        {
            if (y > 150) break;
            head.Controls.Add(L(kv.Key.ToUpperInvariant(), 0, y, 130, 11, 7f, FontStyle.Bold, MUTE));
            var v = L(kv.Value ?? "", 130, y, page - 130, 11, 8f, FontStyle.Regular, INK); v.WordWrap = false; head.Controls.Add(v);
            y += 13;
        }
        head.Controls.Add(L("EXTRACTED", 0, y, 130, 11, 7f, FontStyle.Bold, MUTE));
        head.Controls.Add(L(DateTime.Now.ToString("d MMMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture) + " by " + DcAccess.Username() + ". " +
                            rows.Rows.Count.ToString("#,##0") + " " + (rows.Rows.Count == 1 ? s.Noun : s.NounPlural),
                            130, y, page - 130, 11, 8f, FontStyle.Regular, INK));
        y += 17;
        head.HeightF = y;

        GroupHeaderBand grp = null;
        if (grouped)
        {
            grp = new GroupHeaderBand();
            // Group on the order each group first appears, so groups print in the order the report gives (not alphabetically).
            if (!rows.Columns.Contains("_gord")) rows.Columns.Add("_gord", typeof(string));
            var firstSeen = new Dictionary<string, int>();
            foreach (DataRow r in rows.Rows)
            {
                string k = Convert.ToString(r[GROUP_COL]);
                if (!firstSeen.ContainsKey(k)) firstSeen[k] = firstSeen.Count;
                r["_gord"] = firstSeen[k].ToString("00000", CultureInfo.InvariantCulture);
            }
            grp.GroupFields.Add(new GroupField("_gord", XRColumnSortOrder.Ascending));
            grp.RepeatEveryPage = true; grp.HeightF = 42; grp.KeepTogether = true;
            if (s.PageBreakPerGroup) grp.PageBreak = PageBreak.BeforeBandExceptFirstEntry;
            rep.Bands.Add(grp);
            var gl = new XRLabel(); gl.DataBindings.Add("Text", null, GROUP_COL);
            gl.Font = new Font(FONT, 9.5f, FontStyle.Bold); gl.ForeColor = NAVY; gl.BoundsF = new RectangleF(0, 2, page - 110, 15);
            gl.WordWrap = false; grp.Controls.Add(gl);
            var gc = new XRLabel(); gc.DataBindings.Add("Text", null, GCOUNT_COL);
            gc.Font = new Font(FONT, 7.5f); gc.ForeColor = MUTE; gc.TextAlignment = TextAlignment.MiddleRight;
            gc.BoundsF = new RectangleF(page - 110, 2, 110, 15); grp.Controls.Add(gc);
            HeaderRow(grp, s.Cols, w, seqW, 19);
        }
        else
        {
            var ph = new PageHeaderBand(); ph.HeightF = 23; rep.Bands.Add(ph);
            HeaderRow(ph, s.Cols, w, seqW, 0);
        }

        // One table row per record, so every cell in a row grows to the same height when text wraps.
        var detail = new DetailBand(); detail.HeightF = s.RowHeight; detail.KeepTogether = true; rep.Bands.Add(detail);
        var table = new XRTable();
        table.BeginInit();
        table.BoundsF = new RectangleF(0, 0, page, s.RowHeight);
        var trow = new XRTableRow(); trow.HeightF = s.RowHeight; table.Rows.Add(trow);
        var seq = new XRTableCell(); seq.DataBindings.Add("Text", null, SEQ_COL);
        seq.Font = new Font(FONT, 7.5f); seq.ForeColor = MUTE; seq.TextAlignment = TextAlignment.TopRight;
        seq.WidthF = seqW; seq.Padding = new PaddingInfo(0, 5, 2, 0); seq.Borders = BorderSide.Bottom; seq.BorderColor = LINE;
        trow.Cells.Add(seq);
        for (int i = 0; i < s.Cols.Count; i++)
        {
            var col = s.Cols[i];
            var c = new XRTableCell();
            if (!col.Blank) c.DataBindings.Add("Text", null, col.Field, col.Sum ? "{0:#,##0}" : "");
            c.Font = new Font(FONT, 8f); c.ForeColor = INK;
            c.TextAlignment = col.Right ? TextAlignment.TopRight : TextAlignment.TopLeft;
            c.WidthF = w[i]; c.Padding = new PaddingInfo(3, 3, 2, 1);
            c.Borders = col.Blank ? BorderSide.All : BorderSide.Bottom; c.BorderColor = col.Blank ? MUTE : LINE;
            c.WordWrap = !col.Right; c.CanGrow = !col.Blank; c.Multiline = !col.Right;
            trow.Cells.Add(c);
        }
        table.EndInit();
        detail.Controls.Add(table);
        detail.EvenStyleName = "even";
        var even = new XRControlStyle(); even.Name = "even"; even.BackColor = BAND; rep.StyleSheet.Add(even);

        bool anySum = false; foreach (var c in s.Cols) if (c.Sum) anySum = true;

        if (grouped && (anySum && s.ShowTotals || !string.IsNullOrEmpty(s.GroupFooterNote)))
        {
            var gf = new GroupFooterBand(); rep.Bands.Add(gf);
            float gy = 0;
            if (anySum && s.ShowTotals) { TotalsRow(gf, s.Cols, w, seqW, 0, SummaryRunning.Group, "Subtotal"); gy = 20; }
            if (!string.IsNullOrEmpty(s.GroupFooterNote))
            {
                var note = L(s.GroupFooterNote, 0, gy + 8, page, 24, 8f, FontStyle.Regular, INK); note.Multiline = true; gf.Controls.Add(note);
                string[] sig = { "Name", "Signature", "Date" };
                float sw = (page - 40) / 3f;
                for (int i = 0; i < 3; i++)
                {
                    float sx = i * (sw + 20);
                    var ln = new XRLine(); ln.BoundsF = new RectangleF(sx, gy + 62, sw, 2); ln.ForeColor = INK; gf.Controls.Add(ln);
                    gf.Controls.Add(L(sig[i], sx, gy + 66, sw, 12, 7.5f, FontStyle.Regular, MUTE));
                }
                gy += 84;
            }
            gf.HeightF = gy + 6;
        }

        var foot = new ReportFooterBand(); rep.Bands.Add(foot);
        float fy = 0;
        if (anySum && s.ShowTotals) { TotalsRow(foot, s.Cols, w, seqW, 0, SummaryRunning.Report, "Total"); fy = 22; }
        if (!string.IsNullOrEmpty(s.FootNote))
        {
            var fn = L(s.FootNote, 0, fy + 8, page, 24, 7.5f, FontStyle.Italic, MUTE); fn.Multiline = true; fn.WordWrap = true; fn.CanGrow = true; foot.Controls.Add(fn); fy += 30;
        }
        if (s.Signatories != null && s.Signatories.Length > 0)
        {
            int n = s.Signatories.Length;
            float sw = (page - 30f * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                float sx = i * (sw + 30);
                var ln = new XRLine(); ln.BoundsF = new RectangleF(sx, fy + 40, sw, 2); ln.ForeColor = INK; foot.Controls.Add(ln);
                foot.Controls.Add(L(s.Signatories[i], sx, fy + 44, sw, 12, 8f, FontStyle.Bold, INK));
                foot.Controls.Add(L("Name, signature and date", sx, fy + 56, sw, 11, 7f, FontStyle.Regular, MUTE));
            }
            fy += 72;
        }
        foot.HeightF = fy + 4;

        var pf = new PageFooterBand(); pf.HeightF = 22; rep.Bands.Add(pf);
        var pr = new XRLine(); pr.BoundsF = new RectangleF(0, 0, page, 2); pr.ForeColor = LINE; pf.Controls.Add(pr);
        pf.Controls.Add(L(DcFmt.University + ". " + s.Title, 0, 5, page - 150, 12, 7f, FontStyle.Regular, MUTE));
        var pi = new XRPageInfo(); pi.PageInfo = DevExpress.XtraPrinting.PageInfo.NumberOfTotal; pi.Format = "Page {0} of {1}";
        pi.Font = new Font(FONT, 7f); pi.ForeColor = MUTE; pi.TextAlignment = TextAlignment.MiddleRight; pi.BoundsF = new RectangleF(page - 150, 5, 150, 12);
        pf.Controls.Add(pi);
        return rep;
    }

    private static void HeaderRow(Band band, List<PCol> cols, float[] w, float seqW, float top)
    {
        var hash = L("#", 0, top, seqW, 22, 7f, FontStyle.Bold, Color.White);
        hash.TextAlignment = TextAlignment.MiddleRight; hash.BackColor = NAVY; hash.Padding = new PaddingInfo(0, 5, 0, 0); band.Controls.Add(hash);
        float x = seqW;
        for (int i = 0; i < cols.Count; i++)
        {
            var h = L(cols[i].Header.ToUpperInvariant(), x, top, w[i], 22, 6.5f, FontStyle.Bold, Color.White);
            h.TextAlignment = cols[i].Right ? TextAlignment.MiddleRight : TextAlignment.MiddleLeft;
            h.BackColor = NAVY; h.Padding = new PaddingInfo(3, 3, 0, 0); h.WordWrap = true;
            band.Controls.Add(h); x += w[i];
        }
    }

    private static void TotalsRow(Band band, List<PCol> cols, float[] w, float seqW, float top, SummaryRunning running, string label)
    {
        float x = seqW;
        bool labelled = false;
        var lead = L("", 0, top, seqW, 16, 7.5f, FontStyle.Bold, NAVY); lead.BackColor = TOT; lead.Borders = BorderSide.Top; lead.BorderColor = NAVY; lead.BorderWidth = 1;
        band.Controls.Add(lead);
        for (int i = 0; i < cols.Count; i++)
        {
            var c = new XRLabel();
            c.Font = new Font(FONT, 8f, FontStyle.Bold); c.ForeColor = NAVY; c.BackColor = TOT;
            c.BoundsF = new RectangleF(x, top, w[i], 16); c.Padding = new PaddingInfo(3, 3, 0, 0);
            c.Borders = BorderSide.Top; c.BorderColor = NAVY; c.BorderWidth = 1;
            if (cols[i].Sum)
            {
                c.DataBindings.Add("Text", null, cols[i].Field);
                c.Summary = new XRSummary(running, SummaryFunc.Sum, "{0:#,##0}");
                c.TextAlignment = TextAlignment.MiddleRight;
            }
            else if (!labelled) { c.Text = label; labelled = true; c.TextAlignment = TextAlignment.MiddleLeft; }
            c.WordWrap = false;
            band.Controls.Add(c); x += w[i];
        }
    }
}
