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
//  Graduation Centre, the PDF.
//
//  Built on the server with XtraReport.ExportToPdf, which is how every
//  other PDF on this system is produced (TransactionAuditTrail,
//  AcademicDocumentPdfService, API/doc_verification). Nothing is
//  assembled in the browser: a graduation list goes to Senate, to
//  Council and sometimes to NCHE, and a document that only exists
//  because a particular browser rendered it a particular way is not one
//  anybody can stand behind.
//
//  It is laid out as a graduation document rather than a table dump:
//  the crest, the institution's own navy, a certification block stating
//  exactly what the list is and what produced it, groups per programme
//  that repeat their heading across page breaks, names numbered within
//  the programme the way a list is read out, and a signature block.
// =====================================================================
public static class GraduationPdf
{
    // The design tokens the rest of the system uses, in the form a report needs them.
    private static readonly Color NAVY = Color.FromArgb(0x05, 0x27, 0x5C);
    private static readonly Color ACCENT = Color.FromArgb(0x17, 0x4D, 0xA4);
    private static readonly Color INK = Color.FromArgb(0x1A, 0x1A, 0x2E);
    private static readonly Color MUTE = Color.FromArgb(0x6B, 0x72, 0x80);
    private static readonly Color LINE = Color.FromArgb(0xD8, 0xDF, 0xE8);
    private static readonly Color BAND = Color.FromArgb(0xEE, 0xF3, 0xF9);

    private const string FONT = "Segoe UI";

    /// <summary>The crest, resolved from the web root. Absent is not fatal.</summary>
    private static Image Crest()
    {
        try
        {
            string p = HttpContext.Current.Server.MapPath("~/COOPERP/images/mru-crest.png");
            if (File.Exists(p))
            {
                // Read through a stream and copy, so the report does not hold the file open.
                using (var fs = new FileStream(p, FileMode.Open, FileAccess.Read))
                using (var tmp = Image.FromStream(fs))
                    return new Bitmap(tmp);
            }
        }
        catch { }
        return null;
    }

    /// <summary>Row number, filled by <see cref="Number"/>.</summary>
    public const string COL_SEQ = "_pdfSeq";
    /// <summary>"12 candidates", filled by <see cref="Number"/> on every row of the group.</summary>
    public const string COL_GROUPCOUNT = "_pdfGroupCount";

    /// <summary>
    /// Adds the numbering columns the report binds to.
    ///
    /// Done here, over the rows in their final order, because the number printed beside a name
    /// has to mean "nth in this programme on this list", which is only true once the sort and
    /// the grouping have both been settled.
    /// </summary>
    public static void Number(DataTable t, string groupField, bool numberInGroup)
    {
        if (t == null) return;
        if (!t.Columns.Contains(COL_SEQ)) t.Columns.Add(COL_SEQ, typeof(string));
        if (!t.Columns.Contains(COL_GROUPCOUNT)) t.Columns.Add(COL_GROUPCOUNT, typeof(string));

        bool grouped = !string.IsNullOrEmpty(groupField) && t.Columns.Contains(groupField);

        // Pass one: how many rows each group holds.
        var tally = new Dictionary<string, int>();
        foreach (DataRow r in t.Rows)
        {
            string k = grouped ? Convert.ToString(r[groupField]) : "";
            if (!tally.ContainsKey(k)) tally[k] = 0;
            tally[k]++;
        }

        // Pass two: the running number. A counter PER KEY rather than one that resets whenever
        // the key changes, so a caller that has not grouped its rows still gets 1..n per
        // programme instead of restarting on every row.
        var seen = new Dictionary<string, int>();
        int flat = 0;
        foreach (DataRow r in t.Rows)
        {
            string k = grouped ? Convert.ToString(r[groupField]) : "";
            int n;
            if (grouped && numberInGroup)
            {
                seen.TryGetValue(k, out n);
                n++;
                seen[k] = n;
            }
            else n = ++flat;
            r[COL_SEQ] = n.ToString(CultureInfo.InvariantCulture);
            int c = tally.ContainsKey(k) ? tally[k] : 0;
            r[COL_GROUPCOUNT] = c.ToString(CultureInfo.InvariantCulture) +
                                (c == 1 ? " candidate" : " candidates");
        }
    }

    /// <summary>One column of the printed table.</summary>
    public class PCol
    {
        public string Field;
        public string Header;
        public float Width;
        public bool Right;
        public PCol(string field, string header, float width) { Field = field; Header = header; Width = width; }
        public PCol(string field, string header, float width, bool right)
        { Field = field; Header = header; Width = width; Right = right; }
    }

    private static XRLabel L(string text, float x, float y, float w, float h,
                            float size, FontStyle style, Color fore)
    {
        var l = new XRLabel();
        l.Text = text ?? "";
        l.Font = new Font(FONT, size, style);
        l.ForeColor = fore;
        l.BoundsF = new RectangleF(x, y, w, h);
        l.Padding = new PaddingInfo(0, 0, 0, 0);
        return l;
    }

    /// <summary>
    /// Builds and streams the document.
    /// </summary>
    /// <param name="rows">The data. Column names must match the PCol fields.</param>
    /// <param name="groupField">Column to group on, or "" for a flat list. Repeats across pages.</param>
    /// <param name="numberInGroup">True to number 1..n within each group rather than straight through.</param>
    public static void Send(HttpResponse resp, string fileBase, string reportTitle, string acadYear,
                            string scopeLabel, List<KeyValuePair<string, string>> cover,
                            List<PCol> cols, DataTable rows, string groupField, bool numberInGroup,
                            string truncation)
    {
        XtraReport rep = Build(reportTitle, acadYear, scopeLabel, cover, cols, rows,
                               groupField, numberInGroup, truncation);
        using (var ms = new MemoryStream())
        {
            rep.ExportToPdf(ms);
            ms.Position = 0;
            resp.Clear();
            resp.ContentType = "application/pdf";
            resp.AddHeader("Content-Disposition", "attachment; filename=\"" + fileBase + ".pdf\"");
            resp.BinaryWrite(ms.ToArray());
            resp.Flush();
            resp.End();
        }
    }

    private static XtraReport Build(string reportTitle, string acadYear, string scopeLabel,
                                    List<KeyValuePair<string, string>> cover, List<PCol> cols,
                                    DataTable rows, string groupField, bool numberInGroup,
                                    string truncation)
    {
        Number(rows, groupField, numberInGroup);

        var rep = new XtraReport();
        rep.PaperKind = PaperKind.A4;
        rep.DataSource = rows;

        // Total width the columns ask for decides orientation, rather than a guess. A
        // fifteen-column export is landscape; the standard six-column list is portrait, which
        // is what a graduation list is expected to look like.
        float need = 0;
        for (int i = 0; i < cols.Count; i++) need += cols[i].Width;
        rep.Landscape = need > 545f;                 // 545 = A4 portrait minus 30pt margins, at 100dpi
        rep.Margins = new Margins(40, 40, 36, 44);

        float page = (rep.Landscape ? 1123f : 794f) - 80f;   // A4 at 100dpi, less the side margins

        // Scale the columns to the page rather than letting the last one fall off the edge.
        float scale = need > 0 ? page / need : 1f;
        var w = new float[cols.Count];
        for (int i = 0; i < cols.Count; i++) w[i] = cols[i].Width * scale;

        float seqW = 28f;
        // ── Report header: the letterhead and the certification block ──
        var head = new ReportHeaderBand();
        head.HeightF = 150;
        rep.Bands.Add(head);

        Image crest = Crest();
        float textLeft = 0;
        if (crest != null)
        {
            var pic = new XRPictureBox();
            pic.Image = crest;
            pic.Sizing = ImageSizeMode.ZoomImage;
            pic.BoundsF = new RectangleF(0, 0, 58, 58);
            head.Controls.Add(pic);
            textLeft = 68;
        }

        head.Controls.Add(L(GraduationExport.UNIVERSITY.ToUpperInvariant(), textLeft, 2, page - textLeft, 22,
                            15f, FontStyle.Bold, NAVY));
        head.Controls.Add(L("Office of the Academic Registrar", textLeft, 23, page - textLeft, 14,
                            8.5f, FontStyle.Regular, MUTE));
        head.Controls.Add(L(reportTitle.ToUpperInvariant() +
                            (acadYear == "" ? "" : ", " + acadYear),
                            textLeft, 39, page - textLeft, 17, 10.5f, FontStyle.Bold, ACCENT));

        var rule = new XRLine();
        rule.BoundsF = new RectangleF(0, 62, page, 3);
        rule.ForeColor = NAVY;
        rule.LineWidth = 2;
        head.Controls.Add(rule);

        // The certification block. A list with no provenance is a list nobody can defend.
        float y = 71;
        head.Controls.Add(L("PREPARED FOR", 0, y, 120, 11, 7f, FontStyle.Bold, MUTE));
        head.Controls.Add(L(scopeLabel ?? "", 120, y, page - 120, 11, 8f, FontStyle.Regular, INK));
        y += 13;
        if (cover != null)
        {
            foreach (KeyValuePair<string, string> kv in cover)
            {
                if (y > 130) break;                  // the block is a summary, not the whole filter
                head.Controls.Add(L(kv.Key.ToUpperInvariant(), 0, y, 120, 11, 7f, FontStyle.Bold, MUTE));
                head.Controls.Add(L(kv.Value ?? "", 120, y, page - 120, 11, 8f, FontStyle.Regular, INK));
                y += 13;
            }
        }
        head.Controls.Add(L("EXTRACTED", 0, y, 120, 11, 7f, FontStyle.Bold, MUTE));
        head.Controls.Add(L(DateTime.Now.ToString("dddd, d MMMM yyyy 'at' HH:mm") +
                            "   ·   " + rows.Rows.Count.ToString(CultureInfo.InvariantCulture) +
                            (rows.Rows.Count == 1 ? " name" : " names"),
                            120, y, page - 120, 11, 8f, FontStyle.Regular, INK));
        y += 15;

        if (!string.IsNullOrEmpty(truncation))
        {
            var warn = L(truncation, 0, y, page, 22, 8f, FontStyle.Bold,
                         Color.FromArgb(0x8C, 0x20, 0x19));
            warn.Multiline = true;
            head.Controls.Add(warn);
            y += 24;
        }
        head.HeightF = y + 4;

        // ── Group per programme, heading repeated on every page it runs onto ──
        GroupHeaderBand grp = null;
        if (!string.IsNullOrEmpty(groupField) && rows.Columns.Contains(groupField))
        {
            grp = new GroupHeaderBand();
            grp.GroupFields.Add(new GroupField(groupField));
            grp.RepeatEveryPage = true;
            grp.HeightF = 34;
            grp.KeepTogether = true;
            rep.Bands.Add(grp);

            var gl = new XRLabel();
            // DataBindings, not ExpressionBindings: the latter arrived in v16.2 and this build
            // is v16.1. Everything the report needs is a plain column on the table.
            gl.DataBindings.Add("Text", null, groupField);
            gl.Font = new Font(FONT, 9.5f, FontStyle.Bold);
            gl.ForeColor = NAVY;
            gl.BoundsF = new RectangleF(0, 2, page - 90, 15);
            gl.Padding = new PaddingInfo(2, 0, 0, 0);
            grp.Controls.Add(gl);

            // The per-group tally is computed in C# and carried as a column, rather than left
            // to a running summary in a band that prints before its rows are read.
            var gc = new XRLabel();
            if (rows.Columns.Contains(COL_GROUPCOUNT)) gc.DataBindings.Add("Text", null, COL_GROUPCOUNT);
            gc.Font = new Font(FONT, 7.5f, FontStyle.Regular);
            gc.ForeColor = MUTE;
            gc.TextAlignment = TextAlignment.MiddleRight;
            gc.BoundsF = new RectangleF(page - 90, 2, 90, 15);
            grp.Controls.Add(gc);

            // The column headings live in the group so they follow each programme, instead of
            // sitting once at the top of a page that may have started mid-programme.
            AddHeaderRow(grp, cols, w, seqW, 18);
        }

        if (grp == null)
        {
            var ph = new PageHeaderBand();
            ph.HeightF = 17;
            rep.Bands.Add(ph);
            AddHeaderRow(ph, cols, w, seqW, 0);
        }

        // ── The rows ──
        var detail = new DetailBand();
        detail.HeightF = 15;
        rep.Bands.Add(detail);

        // Numbered within the programme, which is how a graduation list is read out and signed.
        // Precomputed in Number() below, so the figure on the page is one this code decided
        // rather than one a report engine inferred.
        var seq = new XRLabel();
        if (rows.Columns.Contains(COL_SEQ)) seq.DataBindings.Add("Text", null, COL_SEQ);
        seq.Font = new Font(FONT, 7.5f, FontStyle.Regular);
        seq.ForeColor = MUTE;
        seq.TextAlignment = TextAlignment.MiddleRight;
        seq.BoundsF = new RectangleF(0, 0, seqW - 5, 15);
        detail.Controls.Add(seq);

        float x = seqW;
        for (int i = 0; i < cols.Count; i++)
        {
            var c = new XRLabel();
            c.DataBindings.Add("Text", null, cols[i].Field);
            c.Font = new Font(FONT, 8f, FontStyle.Regular);
            c.ForeColor = INK;
            c.TextAlignment = cols[i].Right ? TextAlignment.MiddleRight : TextAlignment.MiddleLeft;
            c.BoundsF = new RectangleF(x, 0, w[i], 15);
            c.Padding = new PaddingInfo(3, 3, 0, 0);
            c.Borders = BorderSide.Bottom;
            c.BorderColor = LINE;
            c.WordWrap = false;
            detail.Controls.Add(c);
            x += w[i];
        }
        // Every other row tinted, so the eye does not slip a line across a wide table.
        detail.EvenStyleName = "even";
        var even = new XRControlStyle();
        even.Name = "even";
        even.BackColor = BAND;
        rep.StyleSheet.Add(even);

        // ── Signature block, once, at the very end ──
        var foot = new ReportFooterBand();
        foot.HeightF = 92;
        rep.Bands.Add(foot);

        foot.Controls.Add(L("Every name on this list was cleared by a named reviewer against the results on " +
                            "record at the time of clearing. The evidence behind each decision is retained in " +
                            "full and can be produced on request.",
                            0, 8, page, 24, 7.5f, FontStyle.Italic, MUTE));

        float colW = (page - 40) / 2f;
        for (int i = 0; i < 2; i++)
        {
            float sx = i * (colW + 40);
            var sline = new XRLine();
            sline.BoundsF = new RectangleF(sx, 62, colW, 2);
            sline.ForeColor = INK;
            foot.Controls.Add(sline);
            foot.Controls.Add(L(i == 0 ? "Academic Registrar" : "Chairperson, Senate",
                                sx, 66, colW, 12, 8f, FontStyle.Bold, INK));
            foot.Controls.Add(L("Signature and date", sx, 77, colW, 11, 7f, FontStyle.Regular, MUTE));
        }

        // ── Page footer ──
        var pf = new PageFooterBand();
        pf.HeightF = 22;
        rep.Bands.Add(pf);

        var pfRule = new XRLine();
        pfRule.BoundsF = new RectangleF(0, 0, page, 2);
        pfRule.ForeColor = LINE;
        pf.Controls.Add(pfRule);

        pf.Controls.Add(L(GraduationExport.UNIVERSITY + "   ·   " + reportTitle +
                          (acadYear == "" ? "" : "   ·   " + acadYear),
                          0, 5, page - 150, 12, 7f, FontStyle.Regular, MUTE));

        var pi = new XRPageInfo();
        pi.PageInfo = DevExpress.XtraPrinting.PageInfo.NumberOfTotal;
        pi.Format = "Page {0} of {1}";
        pi.Font = new Font(FONT, 7f, FontStyle.Regular);
        pi.ForeColor = MUTE;
        pi.TextAlignment = TextAlignment.MiddleRight;
        pi.BoundsF = new RectangleF(page - 150, 5, 150, 12);
        pf.Controls.Add(pi);

        return rep;
    }

    /// <summary>
    /// The navy heading strip.
    ///
    /// The background is painted on the labels themselves rather than on a panel behind them.
    /// A panel wider than the printable area makes XtraReports split the report HORIZONTALLY,
    /// a first attempt used a 10000-unit panel as a convenient full-bleed background and turned
    /// a 60-row list into 21 pages.
    /// </summary>
    private static void AddHeaderRow(Band band, List<PCol> cols, float[] w, float seqW, float top)
    {
        var hash = L("#", 0, top, seqW - 3, 16, 7f, FontStyle.Bold, Color.White);
        hash.TextAlignment = TextAlignment.MiddleRight;
        hash.BackColor = NAVY;
        hash.Padding = new PaddingInfo(0, 3, 0, 0);
        band.Controls.Add(hash);

        float x = seqW - 3;
        var gap = L("", x, top, 3, 16, 7f, FontStyle.Regular, Color.White);
        gap.BackColor = NAVY;
        band.Controls.Add(gap);

        x = seqW;
        for (int i = 0; i < cols.Count; i++)
        {
            var h = L(cols[i].Header.ToUpperInvariant(), x, top, w[i], 16, 7f, FontStyle.Bold, Color.White);
            h.TextAlignment = cols[i].Right ? TextAlignment.MiddleRight : TextAlignment.MiddleLeft;
            h.BackColor = NAVY;
            h.Padding = new PaddingInfo(3, 3, 0, 0);
            h.WordWrap = false;
            band.Controls.Add(h);
            x += w[i];
        }
    }
}
