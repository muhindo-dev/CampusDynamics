using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;

// =====================================================================
//  Graduation Centre, exports.
//
//  One writer, used by all four pages, so every file that leaves this
//  module is branded and laid out the same way. A graduation list goes
//  to Senate, to Council and sometimes to NCHE: it should not look like
//  a raw dump, and two exports of the same data should not look like
//  they came from different systems.
//
//  Every workbook opens with a Cover sheet that states what the data is,
//  who produced it, the exact scope and filters behind it, and when it
//  was taken. A spreadsheet with no provenance is a spreadsheet nobody
//  can defend in a meeting.
//
//  Format is SpreadsheetML (the same XML workbook ResultsExporter
//  produces) because it opens natively in Excel, carries styling, and
//  needs no third-party library on a server we do not control.
// =====================================================================
public static class GraduationExport
{
    public const string UNIVERSITY = "Muteesa I Royal University";

    /// <summary>
    /// Set by a caller that had to stop short of the full result set. Printed on the cover in
    /// words. [ThreadStatic] because one request must never inherit another's warning.
    /// </summary>
    [ThreadStatic] public static string Truncation;

    /// <summary>One tab of a workbook.</summary>
    public class Sheet
    {
        public string Name = "Data";
        public string Subtitle = "";
        public string[] Columns;
        public List<string[]> Rows = new List<string[]>();
        /// <summary>Column indexes that hold numbers, so Excel treats them as numbers.</summary>
        public List<int> NumericColumns = new List<int>();
    }

    // =================================================================
    //  The column catalogue.
    //
    //  A page declares its columns once - key, heading, which group it
    //  belongs to in the dialog, whether it is a number, and how to read
    //  it off a row. That one list drives BOTH the checkboxes the user
    //  sees and the sheet that comes back, so the dialog and the workbook
    //  cannot disagree about what a column is or what order they come in.
    // =================================================================

    /// <summary>One selectable column of an export.</summary>
    public class Col<T>
    {
        public string Key;
        public string Header;
        /// <summary>Heading the dialog files this column under.</summary>
        public string Group;
        public bool Numeric;
        /// <summary>True for a column that is on unless the user turns it off.</summary>
        public bool Default = true;
        public Func<T, string> Read;

        public Col(string key, string header, string group, Func<T, string> read)
        { Key = key; Header = header; Group = group; Read = read; }

        public Col(string key, string header, string group, bool numeric, Func<T, string> read)
        { Key = key; Header = header; Group = group; Numeric = numeric; Read = read; }

        public Col<T> Off() { Default = false; return this; }
    }

    /// <summary>
    /// The catalogue as the dialog needs it: key, label, group and whether it starts ticked.
    /// Emitted into the page so the checkboxes are built from the same list the sheet is.
    /// </summary>
    public static object Catalogue<T>(List<Col<T>> cols)
    {
        var outp = new List<object>();
        foreach (Col<T> c in cols)
            outp.Add(new { k = c.Key, t = c.Header, g = c.Group, on = c.Default });
        return outp;
    }

    /// <summary>
    /// Builds a sheet from the catalogue and the keys the user ticked.
    ///
    /// Column ORDER always follows the catalogue, never the order the keys arrived in, so two
    /// people exporting the same columns get identical files. An empty or unrecognised selection
    /// falls back to the catalogue's own defaults rather than producing a sheet with no columns.
    /// </summary>
    public static Sheet Build<T>(string name, string subtitle, List<Col<T>> cols,
                                 IEnumerable<T> rows, string selectedKeys)
    {
        var want = new List<string>();
        if (!string.IsNullOrEmpty(selectedKeys))
            foreach (string k in selectedKeys.Split(','))
            { string t = k.Trim(); if (t != "") want.Add(t); }

        var use = new List<Col<T>>();
        foreach (Col<T> c in cols)
        {
            bool on = want.Count == 0 ? c.Default : want.Contains(c.Key);
            if (on) use.Add(c);
        }
        if (use.Count == 0)
            foreach (Col<T> c in cols) if (c.Default) use.Add(c);

        var sh = new Sheet();
        sh.Name = name;
        sh.Subtitle = subtitle ?? "";
        var heads = new string[use.Count];
        for (int i = 0; i < use.Count; i++)
        {
            heads[i] = use[i].Header;
            if (use[i].Numeric) sh.NumericColumns.Add(i);
        }
        sh.Columns = heads;

        if (rows != null)
        {
            foreach (T row in rows)
            {
                var cells = new string[use.Count];
                for (int i = 0; i < use.Count; i++)
                {
                    try { cells[i] = use[i].Read(row) ?? ""; }
                    catch { cells[i] = ""; }
                }
                sh.Rows.Add(cells);
            }
        }
        return sh;
    }

    /// <summary>Which of a page's optional extra sheets the user asked for.</summary>
    public static bool Wants(string list, string key)
    {
        if (string.IsNullOrEmpty(list)) return false;
        foreach (string k in list.Split(','))
            if (k.Trim() == key) return true;
        return false;
    }

    // =================================================================
    //  The bridge to the PDF.
    //
    //  The workbook and the document are built from the SAME Sheet, so
    //  the columns a user ticked cannot mean one thing in Excel and
    //  another in the PDF.
    // =================================================================

    /// <summary>Hidden column carrying the value the PDF groups on.</summary>
    public const string GROUP_COL = "_grp";

    /// <summary>
    /// A sheet as a bindable table. Fields are named C0..Cn rather than by heading, because a
    /// heading like "Class of Award" or "Zero / Unmarked" is not a legal binding expression.
    /// </summary>
    public static DataTable ToTable(Sheet sh, List<string> groupValues)
    {
        var t = new DataTable();
        for (int i = 0; i < sh.Columns.Length; i++) t.Columns.Add("C" + i, typeof(string));
        if (groupValues != null) t.Columns.Add(GROUP_COL, typeof(string));

        for (int r = 0; r < sh.Rows.Count; r++)
        {
            string[] src = sh.Rows[r];
            DataRow row = t.NewRow();
            for (int i = 0; i < sh.Columns.Length; i++) row["C" + i] = i < src.Length ? (src[i] ?? "") : "";
            if (groupValues != null)
                row[GROUP_COL] = r < groupValues.Count ? (groupValues[r] ?? "") : "";
            t.Rows.Add(row);
        }
        return t;
    }

    /// <summary>
    /// Printed widths, derived from the heading and the data rather than hard-coded, so adding a
    /// column to a catalogue does not mean editing a parallel table of numbers. The report
    /// scales whatever comes back to the page width, so these are proportions in practice.
    /// </summary>
    /// <summary>
    /// Columns the group heading already states, and which therefore should not be repeated on
    /// every row of that group. Grouping by programme prints
    /// "BACHELOR OF INFORMATION TECHNOLOGY   (BIT)" above the rows; printing the same two
    /// values again in every row costs about a third of the page width and tells the reader
    /// nothing. The data is not lost, it is in the heading.
    /// </summary>
    public static Sheet WithoutGroupColumns(Sheet sh, string groupBy)
    {
        if (sh == null || string.IsNullOrEmpty(groupBy)) return sh;

        var drop = new List<string>();
        if (groupBy == "prog") { drop.Add("Programme"); drop.Add("Programme Code"); }
        else if (groupBy == "fac") { drop.Add("Faculty"); }

        var keep = new List<int>();
        for (int i = 0; i < sh.Columns.Length; i++)
            if (!drop.Contains(sh.Columns[i])) keep.Add(i);
        if (keep.Count == sh.Columns.Length || keep.Count == 0) return sh;

        var outp = new Sheet();
        outp.Name = sh.Name;
        outp.Subtitle = sh.Subtitle;
        var heads = new string[keep.Count];
        for (int k = 0; k < keep.Count; k++)
        {
            heads[k] = sh.Columns[keep[k]];
            if (sh.NumericColumns.Contains(keep[k])) outp.NumericColumns.Add(k);
        }
        outp.Columns = heads;
        foreach (string[] r in sh.Rows)
        {
            var row = new string[keep.Count];
            for (int k = 0; k < keep.Count; k++) row[k] = keep[k] < r.Length ? r[keep[k]] : "";
            outp.Rows.Add(row);
        }
        return outp;
    }

    public static List<GraduationPdf.PCol> PdfCols(Sheet sh)
    {
        var l = new List<GraduationPdf.PCol>();
        for (int i = 0; i < sh.Columns.Length; i++)
        {
            string head = sh.Columns[i] ?? "";
            bool num = sh.NumericColumns.Contains(i);

            // How wide the content actually runs, sampled rather than assumed.
            int widest = head.Length;
            int sample = Math.Min(sh.Rows.Count, 200);
            for (int r = 0; r < sample; r++)
            {
                string[] row = sh.Rows[r];
                if (i < row.Length && row[i] != null && row[i].Length > widest) widest = row[i].Length;
            }
            if (widest > 46) widest = 46;          // one long reason must not starve every other column

            float w = 16f + widest * 4.6f;
            // A numeric column is narrow, but never narrower than its own heading: capping at a
            // flat 64 printed "CREDITS EARNE" with the D cut off.
            float headNeeds = 12f + head.Length * 4.4f;
            if (num && w > 64f) w = 64f;
            if (w < headNeeds) w = headNeeds;
            if (w < 40f) w = 40f;
            l.Add(new GraduationPdf.PCol("C" + i, head, w, num));
        }
        return l;
    }

    private static string X(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length + 16);
        foreach (char ch in s)
        {
            if (ch == '&') sb.Append("&amp;");
            else if (ch == '<') sb.Append("&lt;");
            else if (ch == '>') sb.Append("&gt;");
            else if (ch == '"') sb.Append("&quot;");
            else if (ch == '\'') sb.Append("&apos;");
            // Control characters are not legal in XML 1.0 and will make Excel refuse the file.
            else if (ch < 32 && ch != '\t' && ch != '\n' && ch != '\r') sb.Append(' ');
            else sb.Append(ch);
        }
        return sb.ToString();
    }

    private static void Cell(StringBuilder sb, string style, string text, bool number)
    {
        sb.Append("<Cell");
        if (!string.IsNullOrEmpty(style)) sb.Append(" ss:StyleID=\"").Append(style).Append("\"");
        sb.Append("><Data ss:Type=\"").Append(number ? "Number" : "String").Append("\">")
          .Append(X(text)).Append("</Data></Cell>");
    }

    private static void Row(StringBuilder sb, string style, params string[] cells)
    {
        sb.Append("<Row>");
        foreach (string c in cells) Cell(sb, style, c, false);
        sb.AppendLine("</Row>");
    }

    private static void Kv(StringBuilder sb, string k, string v)
    {
        sb.Append("<Row>");
        Cell(sb, "sLbl", k, false);
        Cell(sb, "sCell", v, false);
        sb.AppendLine("</Row>");
    }

    private static void Blank(StringBuilder sb) { sb.AppendLine("<Row></Row>"); }

    private static bool LooksNumeric(string v)
    {
        if (string.IsNullOrEmpty(v)) return false;
        double d;
        return double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out d);
    }

    /// <summary>
    /// Writes a branded workbook straight to the response and ends it.
    /// </summary>
    public static void Workbook(HttpResponse resp, string fileBase, string reportTitle,
                                string scopeLabel, List<KeyValuePair<string, string>> cover,
                                List<Sheet> sheets)
    {
        var sb = new StringBuilder(1024 * 96);
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
        sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" " +
            "xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:x=\"urn:schemas-microsoft-com:office:excel\" " +
            "xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");

        sb.AppendLine("<Styles>");
        sb.AppendLine("<Style ss:ID=\"sTitle\"><Font ss:Bold=\"1\" ss:Size=\"15\" ss:Color=\"#05275C\"/></Style>");
        sb.AppendLine("<Style ss:ID=\"sRpt\"><Font ss:Bold=\"1\" ss:Size=\"11\" ss:Color=\"#174DA4\"/></Style>");
        sb.AppendLine("<Style ss:ID=\"sSub\"><Font ss:Size=\"9\" ss:Color=\"#555555\"/></Style>");
        sb.AppendLine("<Style ss:ID=\"sHdr\"><Font ss:Bold=\"1\" ss:Size=\"10\" ss:Color=\"#FFFFFF\"/>" +
            "<Interior ss:Color=\"#05275C\" ss:Pattern=\"Solid\"/>" +
            "<Alignment ss:Vertical=\"Center\" ss:WrapText=\"1\"/>" +
            "<Borders><Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#05275C\"/></Borders></Style>");
        sb.AppendLine("<Style ss:ID=\"sLbl\"><Font ss:Bold=\"1\" ss:Size=\"10\" ss:Color=\"#174DA4\"/></Style>");
        sb.AppendLine("<Style ss:ID=\"sCell\"><Font ss:Size=\"10\"/>" +
            "<Borders><Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#E0E5ED\"/></Borders></Style>");
        sb.AppendLine("<Style ss:ID=\"sNum\"><Font ss:Size=\"10\"/><Alignment ss:Horizontal=\"Right\"/>" +
            "<Borders><Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#E0E5ED\"/></Borders></Style>");
        sb.AppendLine("<Style ss:ID=\"sFoot\"><Font ss:Size=\"8\" ss:Italic=\"1\" ss:Color=\"#777777\"/></Style>");
        sb.AppendLine("</Styles>");

        // ── Cover: what this is, and everything needed to reproduce it ──
        sb.AppendLine("<Worksheet ss:Name=\"Cover\"><Table>");
        sb.AppendLine("<Column ss:Width=\"170\"/><Column ss:Width=\"320\"/>");
        Row(sb, "sTitle", UNIVERSITY);
        Row(sb, "sRpt", reportTitle);
        Row(sb, "sSub", "Graduation Centre  ·  generated " + DateTime.Now.ToString("dddd, d MMMM yyyy 'at' HH:mm"));
        Blank(sb);
        Kv(sb, "Produced for", scopeLabel ?? "");
        if (cover != null)
            foreach (KeyValuePair<string, string> kv in cover)
                Kv(sb, kv.Key, kv.Value ?? "");
        Blank(sb);
        int total = 0;
        if (sheets != null) foreach (Sheet s in sheets) total += (s.Rows == null ? 0 : s.Rows.Count);
        Kv(sb, "Rows in this workbook", total.ToString(CultureInfo.InvariantCulture));
        Blank(sb);
        // A file that stops short must say so on its own face. The previous build capped every
        // export at fifty rows and stated that number here as though it were the whole answer.
        if (!string.IsNullOrEmpty(Truncation))
        {
            Row(sb, "sRpt", "THIS EXPORT IS INCOMPLETE");
            Row(sb, "sCell", Truncation);
            Blank(sb);
        }
        Row(sb, "sFoot", "Every name on a graduation list produced here was cleared by a named reviewer");
        Row(sb, "sFoot", "against the results on record at the time. The evidence behind each decision is");
        Row(sb, "sFoot", "retained in full and can be produced on request.");
        sb.AppendLine("</Table></Worksheet>");

        // ── Data sheets ──
        if (sheets != null)
        {
            foreach (Sheet sh in sheets)
            {
                if (sh.Columns == null) continue;
                sb.Append("<Worksheet ss:Name=\"").Append(X(SafeSheetName(sh.Name))).AppendLine("\">");
                sb.AppendLine("<Table>");
                for (int i = 0; i < sh.Columns.Length; i++)
                    sb.AppendLine("<Column ss:AutoFitWidth=\"0\" ss:Width=\"" +
                        (i == 0 ? 110 : (sh.Columns[i].Length > 14 ? 160 : 95)) + "\"/>");

                Row(sb, "sTitle", UNIVERSITY);
                Row(sb, "sRpt", reportTitle + (sh.Subtitle == "" ? "" : "  ·  " + sh.Subtitle));
                Row(sb, "sSub", "Generated " + DateTime.Now.ToString("d MMM yyyy HH:mm") +
                    "  ·  " + (sh.Rows == null ? 0 : sh.Rows.Count) + " rows");
                Blank(sb);

                sb.Append("<Row ss:Height=\"22\">");
                foreach (string c in sh.Columns) Cell(sb, "sHdr", c, false);
                sb.AppendLine("</Row>");

                if (sh.Rows != null)
                {
                    foreach (string[] r in sh.Rows)
                    {
                        sb.Append("<Row>");
                        for (int i = 0; i < sh.Columns.Length; i++)
                        {
                            string v = i < r.Length ? r[i] : "";
                            bool num = sh.NumericColumns.Contains(i) && LooksNumeric(v);
                            Cell(sb, num ? "sNum" : "sCell", v, num);
                        }
                        sb.AppendLine("</Row>");
                    }
                }
                sb.AppendLine("</Table>");
                // Freeze the header so a thousand-row list stays readable while scrolling.
                sb.AppendLine("<WorksheetOptions xmlns=\"urn:schemas-microsoft-com:office:excel\">" +
                    "<FreezePanes/><FrozenNoSplit/><SplitHorizontal>5</SplitHorizontal>" +
                    "<TopRowBottomPane>5</TopRowBottomPane><ActivePane>2</ActivePane></WorksheetOptions>");
                sb.AppendLine("</Worksheet>");
            }
        }

        sb.AppendLine("</Workbook>");
        Send(resp, sb.ToString(), "application/vnd.ms-excel", fileBase + ".xls");
    }

    /// <summary>Excel rejects these characters in a tab name, and 31 is its limit.</summary>
    private static string SafeSheetName(string n)
    {
        n = (n ?? "Data").Replace(":", " ").Replace("\\", " ").Replace("/", " ")
                         .Replace("?", " ").Replace("*", " ").Replace("[", "(").Replace("]", ")");
        n = n.Trim();
        if (n == "") n = "Data";
        return n.Length > 31 ? n.Substring(0, 31) : n;
    }

    /// <summary>
    /// A plain CSV of the same rows, with the provenance carried in comment lines above the
    /// header so it survives the trip into someone else's spreadsheet.
    /// </summary>
    public static void Csv(HttpResponse resp, string fileBase, string reportTitle, string scopeLabel,
                           List<KeyValuePair<string, string>> cover, string[] columns, List<string[]> rows)
    {
        var sb = new StringBuilder(1024 * 64);
        sb.Append('﻿');                       // BOM, so Excel reads UTF-8 correctly
        sb.AppendLine("# " + UNIVERSITY);
        sb.AppendLine("# " + reportTitle);
        sb.AppendLine("# Produced for: " + (scopeLabel ?? ""));
        sb.AppendLine("# Generated: " + DateTime.Now.ToString("d MMM yyyy HH:mm"));
        if (cover != null)
            foreach (KeyValuePair<string, string> kv in cover)
                sb.AppendLine("# " + kv.Key + ": " + (kv.Value ?? ""));
        sb.AppendLine("# Rows: " + (rows == null ? 0 : rows.Count).ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(Truncation))
            sb.AppendLine("# INCOMPLETE: " + Truncation);
        sb.AppendLine("#");

        sb.AppendLine(Line(columns));
        if (rows != null) foreach (string[] r in rows) sb.AppendLine(Line(r));
        Send(resp, sb.ToString(), "text/csv", fileBase + ".csv");
    }

    private static string Line(string[] cells)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < cells.Length; i++)
        {
            if (i > 0) sb.Append(',');
            string v = cells[i] ?? "";
            // A leading =, +, - or @ makes Excel treat a cell as a formula. Prefix it so a
            // student name can never execute anything in someone else's spreadsheet.
            if (v.Length > 0 && "=+-@".IndexOf(v[0]) >= 0) v = "'" + v;
            if (v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
                sb.Append('"').Append(v.Replace("\"", "\"\"")).Append('"');
            else sb.Append(v);
        }
        return sb.ToString();
    }

    private static void Send(HttpResponse resp, string body, string mime, string fileName)
    {
        resp.Clear();
        resp.ContentType = mime;
        resp.ContentEncoding = Encoding.UTF8;
        resp.AddHeader("Content-Disposition", "attachment; filename=\"" + fileName + "\"");
        resp.Write(body);
        resp.Flush();
        resp.End();
    }

    /// <summary>A file name that is safe on disk and says what it holds.</summary>
    public static string FileName(string what, string year)
    {
        string y = (year ?? "").Replace("/", "-").Trim();
        if (y == "") y = "all-years";
        return "MRU-" + what + "-" + y + "-" + DateTime.Now.ToString("yyyyMMdd-HHmm");
    }
}
