using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web;

// =====================================================================
//  Student Disciplinary module: Excel and CSV writer.
//  The Fixed Assets writer (SpreadsheetML workbook with a Cover sheet,
//  CSV with BOM, provenance lines and a formula guard) with
//  disciplinary wording.
// =====================================================================
public static class DcExport
{
    public class Sheet
    {
        public string Name = "Data";
        public string Subtitle = "";
        public string[] Columns;
        public List<string[]> Rows = new List<string[]>();
        public List<int> NumericColumns = new List<int>();
        /// <summary>Optional final row, printed bold (for example "Total").</summary>
        public string[] Totals;
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
            else if (ch < 32 && ch != '\t' && ch != '\n' && ch != '\r') sb.Append(' ');
            else sb.Append(ch);
        }
        return sb.ToString();
    }

    private static void Cell(StringBuilder sb, string style, string text, bool number)
    {
        sb.Append("<Cell");
        if (!string.IsNullOrEmpty(style)) sb.Append(" ss:StyleID=\"").Append(style).Append("\"");
        sb.Append("><Data ss:Type=\"").Append(number ? "Number" : "String").Append("\">").Append(X(text)).Append("</Data></Cell>");
    }

    private static void Row(StringBuilder sb, string style, params string[] cells)
    {
        sb.Append("<Row>");
        foreach (string c in cells) Cell(sb, style, c, false);
        sb.AppendLine("</Row>");
    }

    private static void Kv(StringBuilder sb, string k, string v)
    {
        sb.Append("<Row>"); Cell(sb, "sLbl", k, false); Cell(sb, "sCell", v, false); sb.AppendLine("</Row>");
    }

    /// <summary>Numbers are written with separators for reading; strip them to store a real number.</summary>
    private static bool ToNumber(string v, out string clean)
    {
        clean = "";
        if (string.IsNullOrEmpty(v)) return false;
        string s = v.Replace(",", "").Replace(" ", "");
        decimal d;
        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out d))
        { clean = d.ToString(CultureInfo.InvariantCulture); return true; }
        return false;
    }

    public static void Workbook(HttpResponse resp, string fileBase, string reportTitle, string subtitle,
                                List<KeyValuePair<string, string>> cover, List<Sheet> sheets)
    {
        var sb = new StringBuilder(1024 * 96);
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
        sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" xmlns:o=\"urn:schemas-microsoft-com:office:office\" " +
                      "xmlns:x=\"urn:schemas-microsoft-com:office:excel\" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
        sb.AppendLine("<Styles>");
        sb.AppendLine("<Style ss:ID=\"sTitle\"><Font ss:Bold=\"1\" ss:Size=\"15\" ss:Color=\"#05275C\"/></Style>");
        sb.AppendLine("<Style ss:ID=\"sRpt\"><Font ss:Bold=\"1\" ss:Size=\"11\" ss:Color=\"#174DA4\"/></Style>");
        sb.AppendLine("<Style ss:ID=\"sSub\"><Font ss:Size=\"9\" ss:Color=\"#555555\"/></Style>");
        sb.AppendLine("<Style ss:ID=\"sHdr\"><Font ss:Bold=\"1\" ss:Size=\"10\" ss:Color=\"#FFFFFF\"/><Interior ss:Color=\"#05275C\" ss:Pattern=\"Solid\"/>" +
                      "<Alignment ss:Vertical=\"Center\" ss:WrapText=\"1\"/></Style>");
        sb.AppendLine("<Style ss:ID=\"sLbl\"><Font ss:Bold=\"1\" ss:Size=\"10\" ss:Color=\"#174DA4\"/></Style>");
        string border = "<Borders><Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#E0E5ED\"/></Borders>";
        sb.AppendLine("<Style ss:ID=\"sCell\"><Font ss:Size=\"10\"/>" + border + "</Style>");
        sb.AppendLine("<Style ss:ID=\"sNum\"><Font ss:Size=\"10\"/><Alignment ss:Horizontal=\"Right\"/><NumberFormat ss:Format=\"#,##0\"/>" + border + "</Style>");
        sb.AppendLine("<Style ss:ID=\"sCellAlt\"><Font ss:Size=\"10\"/><Interior ss:Color=\"#F0F4FA\" ss:Pattern=\"Solid\"/>" + border + "</Style>");
        sb.AppendLine("<Style ss:ID=\"sNumAlt\"><Font ss:Size=\"10\"/><Alignment ss:Horizontal=\"Right\"/><NumberFormat ss:Format=\"#,##0\"/><Interior ss:Color=\"#F0F4FA\" ss:Pattern=\"Solid\"/>" + border + "</Style>");
        sb.AppendLine("<Style ss:ID=\"sTot\"><Font ss:Bold=\"1\" ss:Size=\"10\" ss:Color=\"#05275C\"/><Interior ss:Color=\"#E6ECF5\" ss:Pattern=\"Solid\"/>" +
                      "<Borders><Border ss:Position=\"Top\" ss:LineStyle=\"Continuous\" ss:Weight=\"2\" ss:Color=\"#05275C\"/></Borders></Style>");
        sb.AppendLine("<Style ss:ID=\"sTotNum\"><Font ss:Bold=\"1\" ss:Size=\"10\" ss:Color=\"#05275C\"/><Alignment ss:Horizontal=\"Right\"/><NumberFormat ss:Format=\"#,##0\"/>" +
                      "<Interior ss:Color=\"#E6ECF5\" ss:Pattern=\"Solid\"/><Borders><Border ss:Position=\"Top\" ss:LineStyle=\"Continuous\" ss:Weight=\"2\" ss:Color=\"#05275C\"/></Borders></Style>");
        sb.AppendLine("<Style ss:ID=\"sFoot\"><Font ss:Size=\"8\" ss:Italic=\"1\" ss:Color=\"#777777\"/></Style>");
        sb.AppendLine("</Styles>");

        // Cover
        sb.AppendLine("<Worksheet ss:Name=\"Cover\"><Table><Column ss:Width=\"170\"/><Column ss:Width=\"340\"/>");
        Row(sb, "sTitle", DcFmt.University);
        Row(sb, "sSub", "Office of the Dean of Students");
        Row(sb, "sRpt", reportTitle);
        if (!string.IsNullOrEmpty(subtitle)) Row(sb, "sSub", subtitle);
        sb.AppendLine("<Row></Row>");
        Kv(sb, "Generated", DateTime.Now.ToString("d MMMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture));
        Kv(sb, "Generated by", DcAccess.Username());
        if (cover != null) foreach (var kv in cover) Kv(sb, kv.Key, kv.Value ?? "");
        int total = 0;
        if (sheets != null) foreach (Sheet s in sheets) total += s.Rows == null ? 0 : s.Rows.Count;
        Kv(sb, "Rows", total.ToString("#,##0", CultureInfo.InvariantCulture));
        sb.AppendLine("<Row></Row>");
        Row(sb, "sFoot", "Taken from the disciplinary records. Confidential: for official use only.");
        Row(sb, "sFoot", "Restricted cases are included only when the person who exported the file may see them.");
        sb.AppendLine("</Table></Worksheet>");

        if (sheets != null)
        {
            foreach (Sheet sh in sheets)
            {
                if (sh.Columns == null) continue;
                sb.Append("<Worksheet ss:Name=\"").Append(X(SafeSheetName(sh.Name))).AppendLine("\"><Table>");
                for (int i = 0; i < sh.Columns.Length; i++)
                {
                    int w = sh.NumericColumns.Contains(i) ? 95 : (sh.Columns[i].Length > 14 || i == 1 ? 170 : 110);
                    sb.AppendLine("<Column ss:AutoFitWidth=\"0\" ss:Width=\"" + w + "\"/>");
                }
                Row(sb, "sTitle", DcFmt.University);
                Row(sb, "sRpt", reportTitle + (sh.Subtitle == "" ? "" : ", " + sh.Subtitle));
                Row(sb, "sSub", "Generated " + DateTime.Now.ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture) + ". " +
                                (sh.Rows == null ? 0 : sh.Rows.Count).ToString("#,##0") + " rows");
                sb.AppendLine("<Row></Row>");
                sb.Append("<Row ss:Height=\"22\">");
                foreach (string col in sh.Columns) Cell(sb, "sHdr", col, false);
                sb.AppendLine("</Row>");
                int n = 0;
                foreach (string[] r in sh.Rows)
                {
                    bool alt = (n++ % 2) == 1;
                    sb.Append("<Row>");
                    for (int i = 0; i < sh.Columns.Length; i++)
                    {
                        string v = i < r.Length ? (r[i] ?? "") : "";
                        string clean;
                        if (sh.NumericColumns.Contains(i) && ToNumber(v, out clean)) Cell(sb, alt ? "sNumAlt" : "sNum", clean, true);
                        else Cell(sb, alt ? "sCellAlt" : "sCell", v, false);
                    }
                    sb.AppendLine("</Row>");
                }
                if (sh.Totals != null)
                {
                    sb.Append("<Row>");
                    for (int i = 0; i < sh.Columns.Length; i++)
                    {
                        string v = i < sh.Totals.Length ? (sh.Totals[i] ?? "") : "";
                        string clean;
                        if (sh.NumericColumns.Contains(i) && ToNumber(v, out clean)) Cell(sb, "sTotNum", clean, true);
                        else Cell(sb, "sTot", v, false);
                    }
                    sb.AppendLine("</Row>");
                }
                sb.AppendLine("</Table>");
                sb.AppendLine("<WorksheetOptions xmlns=\"urn:schemas-microsoft-com:office:excel\"><FreezePanes/><FrozenNoSplit/>" +
                              "<SplitHorizontal>5</SplitHorizontal><TopRowBottomPane>5</TopRowBottomPane><ActivePane>2</ActivePane></WorksheetOptions>");
                sb.AppendLine("</Worksheet>");
            }
        }
        sb.AppendLine("</Workbook>");
        Send(resp, sb.ToString(), "application/vnd.ms-excel", fileBase + ".xls");
    }

    private static string SafeSheetName(string n)
    {
        n = (n ?? "Data").Replace(":", " ").Replace("\\", " ").Replace("/", " ").Replace("?", " ").Replace("*", " ").Replace("[", "(").Replace("]", ")").Trim();
        if (n == "") n = "Data";
        return n.Length > 31 ? n.Substring(0, 31) : n;
    }

    public static void Csv(HttpResponse resp, string fileBase, string reportTitle, string subtitle,
                           List<KeyValuePair<string, string>> cover, Sheet sh)
    {
        var sb = new StringBuilder(1024 * 64);
        sb.Append('﻿');
        sb.AppendLine("# " + DcFmt.University);
        sb.AppendLine("# " + reportTitle + (string.IsNullOrEmpty(subtitle) ? "" : ", " + subtitle));
        sb.AppendLine("# Generated: " + DateTime.Now.ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture) + " by " + DcAccess.Username());
        if (cover != null) foreach (var kv in cover) sb.AppendLine("# " + kv.Key + ": " + (kv.Value ?? ""));
        sb.AppendLine("# Rows: " + sh.Rows.Count.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("#");
        sb.AppendLine(Line(sh.Columns, null));
        foreach (string[] r in sh.Rows) sb.AppendLine(Line(r, sh));
        if (sh.Totals != null) sb.AppendLine(Line(sh.Totals, sh));
        Send(resp, sb.ToString(), "text/csv", fileBase + ".csv");
    }

    private static string Line(string[] cells, Sheet sh)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < cells.Length; i++)
        {
            if (i > 0) sb.Append(',');
            string v = cells[i] ?? "";
            string clean;
            // Numbers go out without separators so a spreadsheet reads them as numbers.
            if (sh != null && sh.NumericColumns.Contains(i) && ToNumber(v, out clean)) v = clean;
            else if (v.Length > 0 && "=+-@".IndexOf(v[0]) >= 0) v = "'" + v;
            if (v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0) sb.Append('"').Append(v.Replace("\"", "\"\"")).Append('"');
            else sb.Append(v);
        }
        return sb.ToString();
    }

    public static void Send(HttpResponse resp, string body, string mime, string fileName)
    {
        resp.Clear();
        resp.ContentType = mime;
        resp.ContentEncoding = Encoding.UTF8;
        resp.AddHeader("Content-Disposition", "attachment; filename=\"" + fileName + "\"");
        resp.Write(body);
        resp.Flush();
        resp.SuppressContent = true;
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    public static string FileName(string what)
    {
        return "MRU-discipline-" + what + "-" + DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
    }
}
