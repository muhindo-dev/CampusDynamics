using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web;

/// <summary>
/// One export standard for the HR module. Every HR screen builds a <see cref="HrExport.Report"/>
/// and sends it as .xlsx (formatted, letterhead, totals) or .csv (plain data).
///
/// Workbook layout, identical on every sheet:
///   1 MUTEESA I ROYAL UNIVERSITY
///   2 Office (Human Resource Office by default)
///   3 Report title
///   4 Scope: the filters that produced the data
///   5 Generated date, time and user
///   7 Column headers (navy), then data rows (banded), then an optional totals row.
/// Header row frozen and filterable; prints landscape, fitted to page width, header repeated,
/// page numbers in the footer. File names: MRU-HR-{slug}-{yyyyMMdd-HHmm}.xlsx
///
/// The .xlsx is written directly as OpenXML (no external library): a minimal stored ZIP.
/// </summary>
public static class HrExport
{
    public const string University = "MUTEESA I ROYAL UNIVERSITY";

    public enum Kind { Text, Number, Money, Decimal, Date, DateTime, Percent }

    public class Col
    {
        public string Header;
        public Kind Kind;
        public double Width;      // 0 = automatic
        public bool Total;        // sum this column in the totals row
        public Col(string header) : this(header, Kind.Text, 0, false) { }
        public Col(string header, Kind kind) : this(header, kind, 0, false) { }
        public Col(string header, Kind kind, bool total) : this(header, kind, 0, total) { }
        public Col(string header, Kind kind, double width, bool total)
        {
            Header = header; Kind = kind; Width = width; Total = total;
        }
    }

    public class Sheet
    {
        public string Name;
        public List<Col> Cols = new List<Col>();
        public List<object[]> Rows = new List<object[]>();
        public string Title;     // optional; defaults to the report title
        public Sheet(string name) { Name = name; }
        public Sheet Add(string header) { Cols.Add(new Col(header)); return this; }
        public Sheet Add(string header, Kind kind) { Cols.Add(new Col(header, kind)); return this; }
        public Sheet Add(string header, Kind kind, bool total) { Cols.Add(new Col(header, kind, total)); return this; }
        public void Row(params object[] values) { Rows.Add(values); }
        public bool HasTotals { get { foreach (Col c in Cols) if (c.Total) return true; return false; } }
    }

    public class Report
    {
        public string Title = "";
        public string Slug = "report";
        public string Office = "Human Resource Office";
        public string PreparedBy = "";
        public List<KeyValuePair<string, string>> Scope = new List<KeyValuePair<string, string>>();
        public List<Sheet> Sheets = new List<Sheet>();
        public DateTime Generated = DateTime.Now;
        public Report(string title, string slug) { Title = title; Slug = slug; }
        public Report AddScope(string label, string value)
        {
            if (!string.IsNullOrEmpty(value)) Scope.Add(new KeyValuePair<string, string>(label, value));
            return this;
        }
        public Sheet NewSheet(string name) { Sheet s = new Sheet(name); Sheets.Add(s); return s; }
        public string ScopeLine
        {
            get
            {
                if (Scope.Count == 0) return "All records";
                List<string> parts = new List<string>();
                foreach (KeyValuePair<string, string> kv in Scope) parts.Add(kv.Key + ": " + kv.Value);
                return string.Join("   |   ", parts.ToArray());
            }
        }
        public string GeneratedLine
        {
            get
            {
                return "Generated " + Generated.ToString("d MMMM yyyy, HH:mm", CultureInfo.InvariantCulture) +
                       (string.IsNullOrEmpty(PreparedBy) ? "" : " by " + PreparedBy);
            }
        }
    }

    // ── text hygiene shared by screens, exports and print ──────────────
    /// <summary>Normalises dashes, quotes and spacing so text reads plainly in every output.</summary>
    public static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        StringBuilder sb = new StringBuilder(s.Length);
        foreach (char ch in s)
        {
            switch (ch)
            {
                case '—': case '–': case '‒': case '―': sb.Append('-'); break;
                case '‘': case '’': sb.Append('\''); break;
                case '“': case '”': sb.Append('"'); break;
                case ' ': sb.Append(' '); break;
                case '…': sb.Append("..."); break;
                default: sb.Append(ch); break;
            }
        }
        return sb.ToString().Trim();
    }

    public static string FileName(Report r, string ext)
    {
        string slug = (r.Slug ?? "report").ToLowerInvariant();
        StringBuilder sb = new StringBuilder();
        foreach (char c in slug) sb.Append(char.IsLetterOrDigit(c) ? c : '-');
        return "MRU-HR-" + sb.ToString().Trim('-') + "-" + r.Generated.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + "." + ext;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CSV: plain data, one sheet, UTF-8 with BOM (opens cleanly in Excel)
    // ═══════════════════════════════════════════════════════════════════
    public static void SendCsv(HttpResponse resp, Report r, int sheetIndex)
    {
        Sheet sh = r.Sheets[Math.Max(0, Math.Min(sheetIndex, r.Sheets.Count - 1))];
        StringBuilder sb = new StringBuilder();
        List<string> head = new List<string>();
        foreach (Col c in sh.Cols) head.Add(CsvCell(c.Header));
        sb.Append(string.Join(",", head.ToArray())).Append("\r\n");
        foreach (object[] row in sh.Rows)
        {
            List<string> cells = new List<string>();
            for (int i = 0; i < sh.Cols.Count; i++)
                cells.Add(CsvCell(CsvValue(i < row.Length ? row[i] : null, sh.Cols[i].Kind)));
            sb.Append(string.Join(",", cells.ToArray())).Append("\r\n");
        }
        byte[] bom = Encoding.UTF8.GetPreamble();
        byte[] body = Encoding.UTF8.GetBytes(sb.ToString());
        byte[] all = new byte[bom.Length + body.Length];
        Buffer.BlockCopy(bom, 0, all, 0, bom.Length);
        Buffer.BlockCopy(body, 0, all, bom.Length, body.Length);
        Send(resp, all, "text/csv", FileName(r, "csv"));
    }

    private static string CsvValue(object v, Kind k)
    {
        if (v == null || v == DBNull.Value) return "";
        if (k == Kind.Date || k == Kind.DateTime)
        {
            DateTime d;
            if (v is DateTime) d = (DateTime)v;
            else if (!DateTime.TryParse(v.ToString(), out d)) return Clean(v.ToString());
            return d.ToString(k == Kind.Date ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
        if (k != Kind.Text)
        {
            double n;
            if (TryNum(v, out n)) return n.ToString(k == Kind.Number || k == Kind.Money ? "0" : "0.##", CultureInfo.InvariantCulture);
        }
        return Clean(v.ToString());
    }

    private static string CsvCell(string s)
    {
        s = s ?? "";
        // Free text starting with = + - @ would run as a formula in Excel
        if (s.Length > 0 && "=+-@".IndexOf(s[0]) >= 0)
        {
            double probe;
            if (!double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out probe)) s = "'" + s;
        }
        if (s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0) return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  XLSX
    // ═══════════════════════════════════════════════════════════════════
    // cellXfs indices (see Styles())
    private const int X_TITLE = 1, X_OFFICE = 2, X_REPORT = 3, X_META = 4, X_HEAD = 5;
    private const int X_TEXT = 6, X_NUM = 8, X_DEC = 10, X_DATE = 12, X_DT = 14, X_PCT = 16;   // +1 = banded row
    private const int X_TOT_LABEL = 18, X_TOT_NUM = 19, X_TOT_DEC = 20, X_TOT_BLANK = 21;
    private const int HEADER_ROW = 7;

    public static void SendXlsx(HttpResponse resp, Report r)
    {
        Send(resp, BuildXlsx(r), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", FileName(r, "xlsx"));
    }

    public static byte[] BuildXlsx(Report r)
    {
        if (r.Sheets.Count == 0) r.NewSheet("Data").Add("No data");
        List<KeyValuePair<string, byte[]>> files = new List<KeyValuePair<string, byte[]>>();
        StringBuilder ct = new StringBuilder();
        ct.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        ct.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
        ct.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
        ct.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
        ct.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
        ct.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
        ct.Append("<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>");
        for (int i = 1; i <= r.Sheets.Count; i++)
            ct.Append("<Override PartName=\"/xl/worksheets/sheet" + i + ".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        ct.Append("</Types>");
        files.Add(Part("[Content_Types].xml", ct.ToString()));

        files.Add(Part("_rels/.rels",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>" +
            "</Relationships>"));

        files.Add(Part("docProps/core.xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" " +
            "xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
            "<dc:title>" + X(Clean(r.Title)) + "</dc:title><dc:creator>Muteesa I Royal University</dc:creator>" +
            "<dcterms:created xsi:type=\"dcterms:W3CDTF\">" + r.Generated.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture) + "</dcterms:created>" +
            "</cp:coreProperties>"));

        StringBuilder wb = new StringBuilder();
        StringBuilder wbRels = new StringBuilder();
        StringBuilder names = new StringBuilder();
        wb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        wb.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
        wbRels.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < r.Sheets.Count; i++)
        {
            Sheet sh = r.Sheets[i];
            string name = SheetName(sh.Name, used);
            wb.Append("<sheet name=\"" + X(name) + "\" sheetId=\"" + (i + 1) + "\" r:id=\"rId" + (i + 1) + "\"/>");
            wbRels.Append("<Relationship Id=\"rId" + (i + 1) + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet" + (i + 1) + ".xml\"/>");
            int lastCol = Math.Max(1, sh.Cols.Count);
            string quoted = "'" + name.Replace("'", "''") + "'";
            names.Append("<definedName name=\"_xlnm.Print_Titles\" localSheetId=\"" + i + "\">" + X(quoted) + "!$" + HEADER_ROW + ":$" + HEADER_ROW + "</definedName>");
            if (sh.Rows.Count > 0)
                names.Append("<definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"" + i + "\" hidden=\"1\">" + X(quoted) + "!$A$" + HEADER_ROW + ":$" + ColName(lastCol) + "$" + (HEADER_ROW + sh.Rows.Count) + "</definedName>");
            files.Add(Part("xl/worksheets/sheet" + (i + 1) + ".xml", SheetXml(r, sh)));
        }
        wb.Append("</sheets>");
        if (names.Length > 0) wb.Append("<definedNames>").Append(names).Append("</definedNames>");
        wb.Append("</workbook>");
        wbRels.Append("<Relationship Id=\"rId" + (r.Sheets.Count + 1) + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
        wbRels.Append("</Relationships>");
        files.Add(Part("xl/workbook.xml", wb.ToString()));
        files.Add(Part("xl/_rels/workbook.xml.rels", wbRels.ToString()));
        files.Add(Part("xl/styles.xml", Styles()));

        return Zip(files);
    }

    private static string SheetXml(Report r, Sheet sh)
    {
        int nCols = Math.Max(1, sh.Cols.Count);
        string last = ColName(nCols);
        StringBuilder x = new StringBuilder();
        x.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        x.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
        x.Append("<sheetPr><pageSetUpPr fitToPage=\"1\"/></sheetPr>");
        x.Append("<dimension ref=\"A1:" + last + (HEADER_ROW + sh.Rows.Count + 1) + "\"/>");
        x.Append("<sheetViews><sheetView workbookViewId=\"0\" showGridLines=\"0\"><pane ySplit=\"" + HEADER_ROW + "\" topLeftCell=\"A" + (HEADER_ROW + 1) + "\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
        x.Append("<sheetFormatPr defaultRowHeight=\"15\"/>");

        x.Append("<cols>");
        for (int c = 0; c < nCols; c++)
        {
            double w = c < sh.Cols.Count && sh.Cols[c].Width > 0 ? sh.Cols[c].Width : AutoWidth(sh, c);
            x.Append("<col min=\"" + (c + 1) + "\" max=\"" + (c + 1) + "\" width=\"" + w.ToString("0.##", CultureInfo.InvariantCulture) + "\" customWidth=\"1\"/>");
        }
        x.Append("</cols><sheetData>");

        string title = string.IsNullOrEmpty(sh.Title) ? r.Title : sh.Title;
        TextRow(x, 1, University, X_TITLE, 22);
        TextRow(x, 2, r.Office, X_OFFICE, 16);
        TextRow(x, 3, title, X_REPORT, 20);
        TextRow(x, 4, r.ScopeLine, X_META, 15);
        TextRow(x, 5, r.GeneratedLine, X_META, 15);

        x.Append("<row r=\"" + HEADER_ROW + "\" ht=\"22\" customHeight=\"1\">");
        for (int c = 0; c < sh.Cols.Count; c++) Cell(x, c, HEADER_ROW, Clean(sh.Cols[c].Header), X_HEAD);
        x.Append("</row>");

        int rowNo = HEADER_ROW;
        double[] sums = new double[sh.Cols.Count];
        for (int i = 0; i < sh.Rows.Count; i++)
        {
            rowNo++;
            object[] row = sh.Rows[i];
            int band = (i % 2 == 1) ? 1 : 0;
            x.Append("<row r=\"" + rowNo + "\">");
            for (int c = 0; c < sh.Cols.Count; c++)
            {
                object v = c < row.Length ? row[c] : null;
                Kind k = sh.Cols[c].Kind;
                double n;
                if (k == Kind.Text || v == null || v == DBNull.Value)
                {
                    Cell(x, c, rowNo, v == null || v == DBNull.Value ? "" : Clean(v.ToString()), X_TEXT + band);
                }
                else if (k == Kind.Date || k == Kind.DateTime)
                {
                    DateTime d;
                    bool ok = v is DateTime ? true : DateTime.TryParse(v.ToString(), out d);
                    d = v is DateTime ? (DateTime)v : (ok ? DateTime.Parse(v.ToString()) : DateTime.MinValue);
                    if (ok && d.Year > 1900)
                        NumCell(x, c, rowNo, (d - new DateTime(1899, 12, 30)).TotalDays, (k == Kind.Date ? X_DATE : X_DT) + band);
                    else Cell(x, c, rowNo, Clean(v.ToString()), X_TEXT + band);
                }
                else if (TryNum(v, out n))
                {
                    int style = k == Kind.Decimal ? X_DEC : k == Kind.Percent ? X_PCT : X_NUM;
                    NumCell(x, c, rowNo, n, style + band);
                    if (sh.Cols[c].Total) sums[c] += n;
                }
                else Cell(x, c, rowNo, Clean(v.ToString()), X_TEXT + band);
            }
            x.Append("</row>");
        }

        if (sh.HasTotals && sh.Rows.Count > 0)
        {
            rowNo++;
            x.Append("<row r=\"" + rowNo + "\" ht=\"18\" customHeight=\"1\">");
            bool labelled = false;
            for (int c = 0; c < sh.Cols.Count; c++)
            {
                if (sh.Cols[c].Total)
                    NumCell(x, c, rowNo, sums[c], sh.Cols[c].Kind == Kind.Decimal ? X_TOT_DEC : X_TOT_NUM);
                else if (!labelled) { Cell(x, c, rowNo, "Total (" + sh.Rows.Count + " rows)", X_TOT_LABEL); labelled = true; }
                else Cell(x, c, rowNo, "", X_TOT_BLANK);
            }
            x.Append("</row>");
        }
        else if (sh.Rows.Count == 0)
        {
            rowNo++;
            x.Append("<row r=\"" + rowNo + "\">");
            Cell(x, 0, rowNo, "No records match the selected filters.", X_META);
            x.Append("</row>");
        }
        x.Append("</sheetData>");

        if (sh.Rows.Count > 0) x.Append("<autoFilter ref=\"A" + HEADER_ROW + ":" + last + (HEADER_ROW + sh.Rows.Count) + "\"/>");
        x.Append("<mergeCells count=\"5\">");
        for (int i = 1; i <= 5; i++) x.Append("<mergeCell ref=\"A" + i + ":" + last + i + "\"/>");
        x.Append("</mergeCells>");
        x.Append("<pageMargins left=\"0.4\" right=\"0.4\" top=\"0.5\" bottom=\"0.6\" header=\"0.3\" footer=\"0.3\"/>");
        x.Append("<pageSetup paperSize=\"9\" orientation=\"landscape\" fitToWidth=\"1\" fitToHeight=\"0\"/>");
        x.Append("<headerFooter><oddFooter>&amp;L&amp;8Muteesa I Royal University - " + X(Clean(title)) + "&amp;R&amp;8Page &amp;P of &amp;N</oddFooter></headerFooter>");
        x.Append("</worksheet>");
        return x.ToString();
    }

    private static void TextRow(StringBuilder x, int row, string text, int style, int height)
    {
        x.Append("<row r=\"" + row + "\" ht=\"" + height + "\" customHeight=\"1\">");
        Cell(x, 0, row, Clean(text), style);
        x.Append("</row>");
    }

    private static void Cell(StringBuilder x, int col, int row, string text, int style)
    {
        x.Append("<c r=\"" + ColName(col + 1) + row + "\" s=\"" + style + "\"");
        if (string.IsNullOrEmpty(text)) { x.Append("/>"); return; }
        x.Append(" t=\"inlineStr\"><is><t xml:space=\"preserve\">" + X(text) + "</t></is></c>");
    }

    private static void NumCell(StringBuilder x, int col, int row, double v, int style)
    {
        x.Append("<c r=\"" + ColName(col + 1) + row + "\" s=\"" + style + "\"><v>" +
                 v.ToString("R", CultureInfo.InvariantCulture) + "</v></c>");
    }

    private static double AutoWidth(Sheet sh, int c)
    {
        int len = c < sh.Cols.Count ? sh.Cols[c].Header.Length : 8;
        int scanned = 0;
        foreach (object[] row in sh.Rows)
        {
            if (++scanned > 500) break;
            if (c >= row.Length || row[c] == null) continue;
            Kind k = sh.Cols[c].Kind;
            int l = k == Kind.Date ? 11 : k == Kind.DateTime ? 17 : (k == Kind.Text ? row[c].ToString().Length : row[c].ToString().Length + 4);
            if (l > len) len = l;
        }
        return Math.Max(8, Math.Min(55, len * 1.1 + 2));
    }

    private static bool TryNum(object v, out double n)
    {
        n = 0;
        if (v == null || v == DBNull.Value) return false;
        if (v is bool) { n = (bool)v ? 1 : 0; return true; }
        if (v is int || v is long || v is double || v is decimal || v is float || v is short || v is byte || v is uint || v is ulong)
        { n = Convert.ToDouble(v, CultureInfo.InvariantCulture); return true; }
        string s = v.ToString().Replace(",", "").Trim();
        return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out n);
    }

    private static string Styles()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
        "<numFmts count=\"4\">" +
            "<numFmt numFmtId=\"164\" formatCode=\"dd mmm yyyy\"/>" +
            "<numFmt numFmtId=\"165\" formatCode=\"dd mmm yyyy hh:mm\"/>" +
            "<numFmt numFmtId=\"166\" formatCode=\"0.0&quot;%&quot;\"/>" +
            "<numFmt numFmtId=\"167\" formatCode=\"#,##0.00\"/>" +
        "</numFmts>" +
        "<fonts count=\"7\">" +
            "<font><sz val=\"10\"/><color rgb=\"FF1A1A2E\"/><name val=\"Calibri\"/></font>" +              // 0 body
            "<font><b/><sz val=\"14\"/><color rgb=\"FF05275C\"/><name val=\"Calibri\"/></font>" +           // 1 university
            "<font><sz val=\"11\"/><color rgb=\"FF174DA4\"/><name val=\"Calibri\"/></font>" +                // 2 office
            "<font><b/><sz val=\"12\"/><color rgb=\"FF1A1A2E\"/><name val=\"Calibri\"/></font>" +           // 3 report title
            "<font><sz val=\"9\"/><color rgb=\"FF666666\"/><name val=\"Calibri\"/></font>" +                 // 4 meta
            "<font><b/><sz val=\"10\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font>" +           // 5 header
            "<font><b/><sz val=\"10\"/><color rgb=\"FF05275C\"/><name val=\"Calibri\"/></font>" +           // 6 totals
        "</fonts>" +
        "<fills count=\"5\">" +
            "<fill><patternFill patternType=\"none\"/></fill>" +
            "<fill><patternFill patternType=\"gray125\"/></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF05275C\"/><bgColor indexed=\"64\"/></patternFill></fill>" +   // 2 navy
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFF3F6FB\"/><bgColor indexed=\"64\"/></patternFill></fill>" +   // 3 band
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFE8EEF8\"/><bgColor indexed=\"64\"/></patternFill></fill>" +   // 4 totals
        "</fills>" +
        "<borders count=\"4\">" +
            "<border><left/><right/><top/><bottom/><diagonal/></border>" +
            "<border><left/><right/><top/><bottom style=\"thin\"><color rgb=\"FFE0E5ED\"/></bottom><diagonal/></border>" +                        // 1 row line
            "<border><left style=\"thin\"><color rgb=\"FF05275C\"/></left><right style=\"thin\"><color rgb=\"FF05275C\"/></right><top style=\"thin\"><color rgb=\"FF05275C\"/></top><bottom style=\"thin\"><color rgb=\"FF05275C\"/></bottom><diagonal/></border>" + // 2 header
            "<border><left/><right/><top style=\"medium\"><color rgb=\"FF05275C\"/></top><bottom style=\"medium\"><color rgb=\"FF05275C\"/></bottom><diagonal/></border>" + // 3 totals
        "</borders>" +
        "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
        "<cellXfs count=\"22\">" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +                                                                       // 0
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +                                                         // 1 title
            "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +                                                         // 2 office
            "<xf numFmtId=\"0\" fontId=\"3\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +                                                         // 3 report
            "<xf numFmtId=\"0\" fontId=\"4\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +                                                         // 4 meta
            "<xf numFmtId=\"0\" fontId=\"5\" fillId=\"2\" borderId=\"2\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"center\" wrapText=\"1\"/></xf>" + // 5 header
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\" wrapText=\"1\"/></xf>" + // 6 text
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\" wrapText=\"1\"/></xf>" + // 7 text band
            "<xf numFmtId=\"3\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"/>" +                              // 8 number
            "<xf numFmtId=\"3\" fontId=\"0\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" +             // 9
            "<xf numFmtId=\"167\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"/>" +                            // 10 decimal
            "<xf numFmtId=\"167\" fontId=\"0\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" +           // 11
            "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\"/></xf>" + // 12 date
            "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\"/></xf>" + // 13
            "<xf numFmtId=\"165\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\"/></xf>" + // 14 datetime
            "<xf numFmtId=\"165\" fontId=\"0\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\"/></xf>" + // 15
            "<xf numFmtId=\"166\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"/>" +                            // 16 percent
            "<xf numFmtId=\"166\" fontId=\"0\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" +           // 17
            "<xf numFmtId=\"0\" fontId=\"6\" fillId=\"4\" borderId=\"3\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" +                     // 18 total label
            "<xf numFmtId=\"3\" fontId=\"6\" fillId=\"4\" borderId=\"3\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" + // 19 total number
            "<xf numFmtId=\"167\" fontId=\"6\" fillId=\"4\" borderId=\"3\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" + // 20 total decimal
            "<xf numFmtId=\"0\" fontId=\"6\" fillId=\"4\" borderId=\"3\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" +                     // 21 total blank
        "</cellXfs>" +
        "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
        "</styleSheet>";
    }

    private static string SheetName(string name, HashSet<string> used)
    {
        string n = Clean(name ?? "Sheet");
        foreach (char c in new[] { '[', ']', ':', '*', '?', '/', '\\' }) n = n.Replace(c, ' ');
        n = n.Trim().Trim('\'');
        if (n == "") n = "Sheet";
        if (n.Length > 31) n = n.Substring(0, 31).Trim();
        string b = n; int k = 2;
        while (used.Contains(n)) { string suf = " (" + k++ + ")"; n = (b.Length + suf.Length > 31 ? b.Substring(0, 31 - suf.Length) : b) + suf; }
        used.Add(n);
        return n;
    }

    private static string ColName(int n)
    {
        string s = "";
        while (n > 0) { int m = (n - 1) % 26; s = (char)('A' + m) + s; n = (n - 1) / 26; }
        return s;
    }

    private static string X(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        StringBuilder sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (c == '&') sb.Append("&amp;");
            else if (c == '<') sb.Append("&lt;");
            else if (c == '>') sb.Append("&gt;");
            else if (c == '"') sb.Append("&quot;");
            else if (c < 0x20 && c != '\t' && c != '\n' && c != '\r') continue;   // invalid in XML
            else sb.Append(c);
        }
        return sb.ToString();
    }

    private static KeyValuePair<string, byte[]> Part(string name, string xml)
    {
        return new KeyValuePair<string, byte[]>(name, new UTF8Encoding(false).GetBytes(xml));
    }

    private static void Send(HttpResponse resp, byte[] data, string contentType, string fileName)
    {
        resp.Clear();
        resp.ContentType = contentType;
        resp.AddHeader("Content-Disposition", "attachment; filename=\"" + fileName + "\"");
        resp.AddHeader("Content-Length", data.Length.ToString(CultureInfo.InvariantCulture));
        resp.BinaryWrite(data);
        resp.Flush();
        if (HttpContext.Current != null) HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    // ── minimal ZIP (stored entries) ─────────────────────────────────────
    private static uint[] _crc;
    private static uint Crc32(byte[] data)
    {
        if (_crc == null)
        {
            uint[] t = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[i] = c;
            }
            _crc = t;
        }
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in data) crc = _crc[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }

    public static byte[] Zip(List<KeyValuePair<string, byte[]>> files)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter w = new BinaryWriter(ms))
        {
            DateTime now = DateTime.Now;
            ushort dosTime = (ushort)((now.Hour << 11) | (now.Minute << 5) | (now.Second / 2));
            ushort dosDate = (ushort)(((now.Year - 1980) << 9) | (now.Month << 5) | now.Day);
            List<long> offsets = new List<long>();
            List<uint> crcs = new List<uint>();
            foreach (KeyValuePair<string, byte[]> f in files)
            {
                byte[] name = Encoding.UTF8.GetBytes(f.Key);
                uint crc = Crc32(f.Value);
                offsets.Add(ms.Position); crcs.Add(crc);
                w.Write(0x04034b50u); w.Write((ushort)20); w.Write((ushort)0x0800); w.Write((ushort)0);
                w.Write(dosTime); w.Write(dosDate); w.Write(crc);
                w.Write((uint)f.Value.Length); w.Write((uint)f.Value.Length);
                w.Write((ushort)name.Length); w.Write((ushort)0);
                w.Write(name); w.Write(f.Value);
            }
            long cdStart = ms.Position;
            for (int i = 0; i < files.Count; i++)
            {
                byte[] name = Encoding.UTF8.GetBytes(files[i].Key);
                w.Write(0x02014b50u); w.Write((ushort)20); w.Write((ushort)20); w.Write((ushort)0x0800); w.Write((ushort)0);
                w.Write(dosTime); w.Write(dosDate); w.Write(crcs[i]);
                w.Write((uint)files[i].Value.Length); w.Write((uint)files[i].Value.Length);
                w.Write((ushort)name.Length); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)0);
                w.Write(0u); w.Write((uint)offsets[i]); w.Write(name);
            }
            long cdEnd = ms.Position;
            w.Write(0x06054b50u); w.Write((ushort)0); w.Write((ushort)0);
            w.Write((ushort)files.Count); w.Write((ushort)files.Count);
            w.Write((uint)(cdEnd - cdStart)); w.Write((uint)cdStart); w.Write((ushort)0);
            w.Flush();
            return ms.ToArray();
        }
    }
}
