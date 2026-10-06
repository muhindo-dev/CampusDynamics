using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using MySql.Data.MySqlClient;

// =====================================================================
//  Fixed Assets: bulk import from Excel (plan 4.4).
//  1. Template: a real .xlsx with the Assets sheet (header in row 1),
//     a Lists sheet of valid codes and drop-downs, and Notes.
//  2. Upload: .xlsx, Excel 2003 XML or .csv; at most 3,000 rows.
//  3. Validate: every row is created for real inside a transaction that
//     is then rolled back, so the report is exactly what commit would do.
//  4. Commit: all rows in one transaction, or none.
// =====================================================================
public static class FaImport
{
    public const int MaxRows = 3000;

    public class Column { public string Key, Header, Note; public bool Required; }

    public static readonly Column[] Columns = {
        new Column { Key = "sub_category_code", Header = "sub_category_code", Required = true, Note = "Code from the Lists sheet, for example CE-LAP." },
        new Column { Key = "name", Header = "name", Required = true, Note = "What the asset is, for example HP ProBook 450 G8 laptop." },
        new Column { Key = "campus", Header = "campus", Required = true, Note = "Kakeeka or Kirumba." },
        new Column { Key = "purchase_date", Header = "purchase_date", Required = true, Note = "Date bought, as 2024-10-07 or 07/10/2024." },
        new Column { Key = "original_cost", Header = "original_cost", Required = true, Note = "Purchase value in UGX, numbers only." },
        new Column { Key = "status", Header = "status", Required = true, Note = "In use or In store." },
        new Column { Key = "asset_no", Header = "asset_no", Note = "Leave blank to generate. Fill in only to keep an existing engraved number." },
        new Column { Key = "tag_no", Header = "tag_no", Note = "Barcode or tag already fixed to the asset, if any." },
        new Column { Key = "description", Header = "description", Note = "" },
        new Column { Key = "serial_no", Header = "serial_no", Note = "" },
        new Column { Key = "model", Header = "model", Note = "" },
        new Column { Key = "make", Header = "make", Note = "Make or brand." },
        new Column { Key = "quantity", Header = "quantity", Note = "For a lot (for example 120 chairs or 300 books). Blank means 1." },
        new Column { Key = "building", Header = "building", Note = "Building or block." },
        new Column { Key = "room", Header = "room", Note = "Room, office or store." },
        new Column { Key = "department", Header = "department", Note = "Department name exactly as on the Lists sheet." },
        new Column { Key = "custodian_staff_no", Header = "custodian_staff_no", Note = "Staff number of the responsible person." },
        new Column { Key = "supplier", Header = "supplier", Note = "" },
        new Column { Key = "invoice_ref", Header = "invoice_ref", Note = "" },
        new Column { Key = "order_ref", Header = "order_ref", Note = "Purchase order or LPO number." },
        new Column { Key = "funding_source", Header = "funding_source", Note = "For example University funds, Donation, Government grant." },
        new Column { Key = "warranty_expiry", Header = "warranty_expiry", Note = "" },
        new Column { Key = "method", Header = "method", Note = "Blank uses the sub-category default. Otherwise SL, RB or NONE." },
        new Column { Key = "useful_life_years", Header = "useful_life_years", Note = "Blank uses the default. Total life in years (straight line)." },
        new Column { Key = "rate_pct", Header = "rate_pct", Note = "Blank uses the default. Annual percent (reducing balance)." },
        new Column { Key = "residual_value", Header = "residual_value", Note = "Blank means 0." },
        new Column { Key = "opening_date", Header = "opening_date", Note = "Only for assets bought before the register started: the date the opening value applies to, normally 31 July." },
        new Column { Key = "opening_accum_dep", Header = "opening_accum_dep", Note = "Depreciation charged up to the opening date, in UGX, or CALC to calculate it from the method and life." },
        new Column { Key = "notes", Header = "notes", Note = "" } };

    // ─────────────────────────── Template ───────────────────────────

    private static string Esc(string s) { return System.Security.SecurityElement.Escape(s ?? "") ?? ""; }

    private static string ColName(int i)
    {
        string s = ""; i++;
        while (i > 0) { int m = (i - 1) % 26; s = (char)('A' + m) + s; i = (i - 1) / 26; }
        return s;
    }

    private static string SheetXml(List<string[]> rows, bool boldHeader, int[] widths, string extra)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        sb.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
        if (widths != null && widths.Length > 0)
        {
            sb.Append("<cols>");
            for (int i = 0; i < widths.Length; i++) sb.Append("<col min=\"" + (i + 1) + "\" max=\"" + (i + 1) + "\" width=\"" + widths[i] + "\" customWidth=\"1\"/>");
            sb.Append("</cols>");
        }
        sb.Append("<sheetData>");
        for (int r = 0; r < rows.Count; r++)
        {
            sb.Append("<row r=\"" + (r + 1) + "\">");
            for (int c = 0; c < rows[r].Length; c++)
            {
                string v = rows[r][c] ?? "";
                if (v == "") continue;
                sb.Append("<c r=\"" + ColName(c) + (r + 1) + "\" t=\"inlineStr\"" + (r == 0 && boldHeader ? " s=\"1\"" : "") + "><is><t xml:space=\"preserve\">" + Esc(v) + "</t></is></c>");
            }
            sb.Append("</row>");
        }
        sb.Append("</sheetData>");
        sb.Append(extra ?? "");
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    public static byte[] Template(MySqlConnection c)
    {
        // Lists
        var subs = new List<string>(); var subNames = new List<string>();
        foreach (DataRow r in FaDb.Table(c, null,
            "SELECT CONCAT(p.code,'-',s.code) code, CONCAT(p.name,' / ',s.name) name FROM fa_category s JOIN fa_category p ON p.id=s.parent_id " +
            "WHERE s.is_active=1 AND p.is_active=1 ORDER BY p.sort_order, s.sort_order").Rows)
        { subs.Add(FaDb.S(r[0])); subNames.Add(FaDb.S(r[1])); }
        var campuses = new List<string>();
        foreach (DataRow r in FaDb.Table(c, null, "SELECT campus_name FROM acad_campuses WHERE ID<>0 ORDER BY ID").Rows)
            campuses.Add(FaAssets.Title(FaDb.S(r[0])).Replace(" Campus", ""));
        var depts = new List<string>();
        foreach (DataRow r in FaDb.Table(c, null, "SELECT dept_name FROM hrm_departments WHERE dept_name NOT LIKE 'TEST%' ORDER BY dept_name").Rows)
            depts.Add(FaDb.S(r[0]));
        string[] statuses = { "In use", "In store" };
        string[] methods = { "SL", "RB", "NONE" };

        var lists = new List<string[]>();
        lists.Add(new[] { "sub_category_code", "sub_category", "campus", "status", "method", "department" });
        int n = Math.Max(Math.Max(subs.Count, depts.Count), 3);
        for (int i = 0; i < n; i++)
            lists.Add(new[] { i < subs.Count ? subs[i] : "", i < subNames.Count ? subNames[i] : "", i < campuses.Count ? campuses[i] : "",
                              i < statuses.Length ? statuses[i] : "", i < methods.Length ? methods[i] : "", i < depts.Count ? depts[i] : "" });

        var head = new string[Columns.Length];
        var widths = new int[Columns.Length];
        for (int i = 0; i < Columns.Length; i++) { head[i] = Columns[i].Header; widths[i] = Math.Max(14, Columns[i].Header.Length + 4); }
        widths[1] = 36;
        var asset = new List<string[]> { head };
        asset.Add(new[] { subs.Count > 0 ? (subs.Contains("CE-LAP") ? "CE-LAP" : subs[0]) : "", "Example: delete this row before importing", campuses.Count > 0 ? campuses[0] : "",
                          DateTime.Today.ToString("yyyy-MM-dd"), "3500000", "In use" });

        int lastRow = MaxRows + 1;
        var val = new StringBuilder("<dataValidations count=\"5\">");
        Action<int, string, int> add = delegate (int col, string lc, int count)
        {
            string rng = ColName(col) + "2:" + ColName(col) + lastRow;
            val.Append("<dataValidation type=\"list\" allowBlank=\"1\" showErrorMessage=\"1\" sqref=\"" + rng + "\"><formula1>Lists!$" + lc + "$2:$" + lc + "$" + (count + 1) + "</formula1></dataValidation>");
        };
        add(0, "A", subs.Count); add(2, "C", campuses.Count); add(5, "D", statuses.Length); add(22, "E", methods.Length); add(15, "F", depts.Count);
        val.Append("</dataValidations>");

        var notes = new List<string[]> { new[] { "column", "required", "what to enter" } };
        foreach (var col in Columns) notes.Add(new[] { col.Header, col.Required ? "Yes" : "", col.Note });
        notes.Add(new[] { "", "", "" });
        notes.Add(new[] { "Rules", "", "Keep the header row as it is. One asset per row. At most " + MaxRows.ToString("#,##0") + " rows per file." });
        notes.Add(new[] { "", "", "The file is checked in full before anything is saved. If any row has an error, nothing is imported." });
        notes.Add(new[] { "", "", "Save as Excel workbook (.xlsx) or CSV." });

        var files = new List<KeyValuePair<string, byte[]>>();
        var enc = new UTF8Encoding(false);
        string[] names = { "Assets", "Lists", "Notes" };
        string[] xml = { SheetXml(asset, true, widths, val.ToString()), SheetXml(lists, true, new[] { 18, 52, 14, 12, 10, 48 }, null),
                         SheetXml(notes, true, new[] { 22, 10, 110 }, null) };
        const string WS = "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml";
        files.Add(new KeyValuePair<string, byte[]>("[Content_Types].xml", enc.GetBytes(
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"" + WS + "\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"" + WS + "\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet3.xml\" ContentType=\"" + WS + "\"/></Types>")));
        files.Add(new KeyValuePair<string, byte[]>("_rels/.rels", enc.GetBytes(
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>")));
        var sheetsXml = new StringBuilder(); var rels = new StringBuilder();
        for (int i = 0; i < 3; i++)
        {
            sheetsXml.Append("<sheet name=\"" + names[i] + "\" sheetId=\"" + (i + 1) + "\" r:id=\"rId" + (i + 1) + "\"/>");
            rels.Append("<Relationship Id=\"rId" + (i + 1) + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet" + (i + 1) + ".xml\"/>");
        }
        rels.Append("<Relationship Id=\"rId9\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
        files.Add(new KeyValuePair<string, byte[]>("xl/workbook.xml", enc.GetBytes(
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
            "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>" + sheetsXml + "</sheets></workbook>")));
        files.Add(new KeyValuePair<string, byte[]>("xl/_rels/workbook.xml.rels", enc.GetBytes(
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" + rels + "</Relationships>")));
        files.Add(new KeyValuePair<string, byte[]>("xl/styles.xml", enc.GetBytes(
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<fonts count=\"2\"><font><sz val=\"10\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"10\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font></fonts>" +
            "<fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF05275C\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
            "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/></cellXfs></styleSheet>")));
        for (int i = 0; i < 3; i++) files.Add(new KeyValuePair<string, byte[]>("xl/worksheets/sheet" + (i + 1) + ".xml", enc.GetBytes(xml[i])));
        return FaZip.Write(files);
    }

    // ─────────────────────────── Reading ───────────────────────────

    /// <summary>Reads the first sheet (or the one named Assets) into rows of strings. Row 0 is the header.</summary>
    public static List<string[]> Read(string fileName, byte[] bytes, out string error)
    {
        error = null;
        string ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        try
        {
            if (ext == ".csv" || ext == ".txt") return ReadCsv(bytes);
            if (ext == ".xlsx") return ReadXlsx(bytes);
            if (ext == ".xls" || ext == ".xml")
            {
                string head = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 400));
                if (head.Contains("<?xml") || head.Contains("<Workbook")) return ReadSpreadsheetMl(bytes);
                error = "This is an old binary Excel file. Open it in Excel and save it as Excel Workbook (.xlsx) or CSV.";
                return null;
            }
            error = "Upload an Excel workbook (.xlsx) or a CSV file.";
            return null;
        }
        catch (Exception ex)
        {
            FaLog.Error("FaImport.Read", ex);
            error = "The file could not be read. Check that it is the import template saved as .xlsx or CSV.";
            return null;
        }
    }

    private static List<string[]> ReadCsv(byte[] bytes)
    {
        string text = Encoding.UTF8.GetString(bytes);
        if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);
        var rows = new List<string[]>();
        var cur = new List<string>(); var cell = new StringBuilder(); bool q = false;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (q)
            {
                if (ch == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; } else q = false; }
                else cell.Append(ch);
            }
            else if (ch == '"') q = true;
            else if (ch == ',') { cur.Add(cell.ToString()); cell.Length = 0; }
            else if (ch == '\r') { }
            else if (ch == '\n') { cur.Add(cell.ToString()); cell.Length = 0; rows.Add(cur.ToArray()); cur = new List<string>(); }
            else cell.Append(ch);
        }
        if (cell.Length > 0 || cur.Count > 0) { cur.Add(cell.ToString()); rows.Add(cur.ToArray()); }
        rows.RemoveAll(delegate (string[] r) { return r.Length > 0 && r[0].StartsWith("#"); });
        return rows;
    }

    private static List<string[]> ReadXlsx(byte[] bytes)
    {
        var entries = FaZip.Read(bytes);
        Func<string, XmlDocument> Doc = delegate (string name)
        {
            byte[] b; if (!entries.TryGetValue(name, out b)) return null;
            var x = new XmlDocument(); using (var ms = new MemoryStream(b)) x.Load(ms); return x;
        };
        var ns = new XmlNamespaceManager(new NameTable());
        ns.AddNamespace("m", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        ns.AddNamespace("pr", "http://schemas.openxmlformats.org/package/2006/relationships");
        XmlDocument wdoc = Doc("xl/workbook.xml");
        string relId = null;
        foreach (XmlNode sh in wdoc.SelectNodes("//m:sheets/m:sheet", ns))
        {
            string nm = sh.Attributes["name"].Value;
            string id = sh.Attributes["id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"].Value;
            if (relId == null) relId = id;
            if (nm.Equals("Assets", StringComparison.OrdinalIgnoreCase)) { relId = id; break; }
        }
        string sheetPath = "xl/worksheets/sheet1.xml";
        XmlDocument rdoc = Doc("xl/_rels/workbook.xml.rels");
        if (rdoc != null)
            foreach (XmlNode r in rdoc.SelectNodes("//pr:Relationship", ns))
                if (r.Attributes["Id"].Value == relId)
                {
                    string t = r.Attributes["Target"].Value.TrimStart('/');
                    sheetPath = t.StartsWith("xl/") ? t : "xl/" + t;
                }
        var sst = new List<string>();
        XmlDocument sdoc = Doc("xl/sharedStrings.xml");
        if (sdoc != null)
            foreach (XmlNode si in sdoc.SelectNodes("//m:si", ns))
            {
                var sb = new StringBuilder();
                foreach (XmlNode t in si.SelectNodes(".//m:t", ns)) sb.Append(t.InnerText);
                sst.Add(sb.ToString());
            }
        XmlDocument doc = Doc(sheetPath);
        var rows = new List<string[]>();
        foreach (XmlNode row in doc.SelectNodes("//m:sheetData/m:row", ns))
        {
            int rn = row.Attributes["r"] != null ? int.Parse(row.Attributes["r"].Value, CultureInfo.InvariantCulture) - 1 : rows.Count;
            while (rows.Count < rn) rows.Add(new string[0]);
            var cells = new List<string>();
            foreach (XmlNode c in row.SelectNodes("m:c", ns))
            {
                string refr = c.Attributes["r"] != null ? c.Attributes["r"].Value : "";
                int col = 0;
                foreach (char ch in refr) { if (ch >= 'A' && ch <= 'Z') col = col * 26 + (ch - 'A' + 1); else break; }
                col = col == 0 ? cells.Count : col - 1;
                while (cells.Count < col) cells.Add("");
                string t = c.Attributes["t"] != null ? c.Attributes["t"].Value : "";
                string v = "";
                if (t == "inlineStr") { var n = c.SelectSingleNode(".//m:t", ns); v = n == null ? "" : n.InnerText; }
                else
                {
                    var n = c.SelectSingleNode("m:v", ns); v = n == null ? "" : n.InnerText;
                    if (t == "s") { int i; if (int.TryParse(v, out i) && i < sst.Count) v = sst[i]; }
                    else if (t == "b") v = v == "1" ? "TRUE" : "FALSE";
                }
                cells.Add(v);
            }
            rows.Add(cells.ToArray());
        }
        return rows;
    }

    private static List<string[]> ReadSpreadsheetMl(byte[] bytes)
    {
        var doc = new XmlDocument(); using (var ms = new MemoryStream(bytes)) doc.Load(ms);
        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("ss", "urn:schemas-microsoft-com:office:spreadsheet");
        XmlNode ws = null;
        foreach (XmlNode w in doc.SelectNodes("//ss:Worksheet", ns))
        {
            if (ws == null) ws = w;
            var nm = w.Attributes["ss:Name"];
            if (nm != null && nm.Value.Equals("Assets", StringComparison.OrdinalIgnoreCase)) { ws = w; break; }
        }
        var rows = new List<string[]>();
        foreach (XmlNode row in ws.SelectNodes(".//ss:Table/ss:Row", ns))
        {
            var cells = new List<string>();
            foreach (XmlNode c in row.SelectNodes("ss:Cell", ns))
            {
                var idx = c.Attributes["ss:Index"];
                if (idx != null) { int i = int.Parse(idx.Value) - 1; while (cells.Count < i) cells.Add(""); }
                var data = c.SelectSingleNode("ss:Data", ns);
                cells.Add(data == null ? "" : data.InnerText);
            }
            rows.Add(cells.ToArray());
        }
        return rows;
    }

    // ─────────────────────────── Mapping rows ───────────────────────────

    private class Lookups
    {
        public Dictionary<string, int> Subs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> Campuses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> Depts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<int>> Staff = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> Suppliers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    private static Lookups Load(MySqlConnection c, MySqlTransaction tx)
    {
        var l = new Lookups();
        foreach (DataRow r in FaDb.Table(c, tx, "SELECT s.id, CONCAT(p.code,'-',s.code) FROM fa_category s JOIN fa_category p ON p.id=s.parent_id").Rows)
            l.Subs[FaDb.S(r[1])] = FaDb.I(r[0]);
        foreach (DataRow r in FaDb.Table(c, tx, "SELECT ID, campus_name, campus_short_name FROM acad_campuses WHERE ID<>0").Rows)
        {
            int id = FaDb.I(r[0]); string nm = FaDb.S(r[1]).Trim();
            l.Campuses[nm] = id; l.Campuses[nm.Replace(" CAMPUS", "").Trim()] = id; l.Campuses[FaDb.S(r[2]).Trim()] = id; l.Campuses[id.ToString()] = id;
        }
        foreach (DataRow r in FaDb.Table(c, tx, "SELECT ID, dept_name FROM hrm_departments").Rows)
        { l.Depts[FaDb.S(r[1]).Trim()] = FaDb.I(r[0]); l.Depts[FaDb.I(r[0]).ToString()] = FaDb.I(r[0]); }
        foreach (DataRow r in FaDb.Table(c, tx, "SELECT empID, EMP_CODE FROM hrm_employee WHERE IFNULL(EMP_CODE,'')<>''").Rows)
        {
            string k = FaDb.S(r[1]).Trim();
            if (!l.Staff.ContainsKey(k)) l.Staff[k] = new List<int>();
            l.Staff[k].Add(FaDb.I(r[0]));
        }
        try
        {
            foreach (DataRow r in FaDb.Table(c, tx, "SELECT supplierID, supplierName FROM campus_dynamics_accounts.supplier").Rows)
                l.Suppliers[FaDb.S(r[1]).Trim()] = FaDb.I(r[0]);
        }
        catch { }
        return l;
    }

    private static string Date(string v, out DateTime? d)
    {
        d = null;
        v = (v ?? "").Trim();
        if (v == "") return null;
        double serial;
        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out serial) && serial > 10000 && serial < 80000)
        { d = DateTime.FromOADate(serial).Date; return null; }
        DateTime x;
        string[] f = { "yyyy-MM-dd", "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "d MMM yyyy", "d MMMM yyyy", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.fff" };
        if (DateTime.TryParseExact(v, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out x)) { d = x.Date; return null; }
        return "is not a date (use 2024-10-07)";
    }

    private static string Money(string v, out decimal? m)
    {
        m = null; v = (v ?? "").Replace(",", "").Replace(" ", "").Replace("UGX", "").Trim();
        if (v == "") return null;
        decimal x;
        if (decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out x)) { m = x; return null; }
        return "is not a number";
    }

    public class RowError { public int row; public string column, value, message; }

    /// <summary>Turns sheet rows into the same dictionaries the asset form sends, collecting format errors.</summary>
    private static List<Dictionary<string, object>> Map(MySqlConnection c, MySqlTransaction tx, List<string[]> sheet, List<RowError> errors)
    {
        var outp = new List<Dictionary<string, object>>();
        if (sheet == null || sheet.Count == 0) { errors.Add(new RowError { row = 1, column = "", value = "", message = "The file is empty." }); return outp; }
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < sheet[0].Length; i++) { string h = (sheet[0][i] ?? "").Trim().Replace(" ", "_").Replace("*", ""); if (h != "" && !map.ContainsKey(h)) map[h] = i; }
        foreach (var col in Columns)
            if (col.Required && !map.ContainsKey(col.Key))
                errors.Add(new RowError { row = 1, column = col.Header, value = "", message = "The column " + col.Header + " is missing. Use the template header row." });
        if (errors.Count > 0) return outp;

        var lk = Load(c, tx);
        var seenNo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int r = 1; r < sheet.Count; r++)
        {
            string[] cells = sheet[r];
            Func<string, string> g = delegate (string k) { int i; return map.TryGetValue(k, out i) && i < cells.Length ? (cells[i] ?? "").Trim() : ""; };
            bool empty = true; foreach (string s in cells) if (!string.IsNullOrEmpty((s ?? "").Trim())) { empty = false; break; }
            if (empty) continue;
            if (g("name").StartsWith("Example:", StringComparison.OrdinalIgnoreCase)) continue;
            if (outp.Count >= MaxRows) { errors.Add(new RowError { row = r + 1, column = "", value = "", message = "More than " + MaxRows.ToString("#,##0") + " rows. Split the file." }); break; }
            int rowNo = r + 1;
            Action<string, string, string> err = delegate (string col, string val, string msg) { errors.Add(new RowError { row = rowNo, column = col, value = val, message = msg }); };
            var d = new Dictionary<string, object>();
            d["_row"] = rowNo;

            string sub = g("sub_category_code").ToUpperInvariant();
            int sid; if (sub == "") err("sub_category_code", sub, "is required"); else if (!lk.Subs.TryGetValue(sub, out sid)) err("sub_category_code", sub, "is not a sub-category code (see the Lists sheet)"); else d["categoryId"] = sid;
            d["name"] = g("name"); if (g("name") == "") err("name", "", "is required");
            string cp = g("campus"); int cid;
            if (cp == "") err("campus", "", "is required");
            else if (lk.Campuses.TryGetValue(cp, out cid) || lk.Campuses.TryGetValue(cp.Replace(" Campus", "").Replace(" campus", ""), out cid)) d["campusId"] = cid;
            else err("campus", cp, "is not a campus (Kakeeka or Kirumba)");
            DateTime? pd; string pe = Date(g("purchase_date"), out pd);
            if (g("purchase_date") == "") err("purchase_date", "", "is required"); else if (pe != null) err("purchase_date", g("purchase_date"), pe);
            else if (pd.Value > DateTime.Today) err("purchase_date", g("purchase_date"), "is in the future");
            else d["purchaseDate"] = pd.Value.ToString("yyyy-MM-dd");
            decimal? cost; string ce = Money(g("original_cost"), out cost);
            if (g("original_cost") == "") err("original_cost", "", "is required"); else if (ce != null) err("original_cost", g("original_cost"), ce); else d["originalCost"] = cost.Value.ToString(CultureInfo.InvariantCulture);
            string st = g("status").ToLowerInvariant().Replace("_", " ");
            if (st == "in use" || st == "") d["status"] = "IN_USE"; else if (st == "in store") d["status"] = "IN_STORE"; else err("status", g("status"), "must be In use or In store");

            string no = g("asset_no");
            if (no != "")
            {
                int prev; if (seenNo.TryGetValue(no, out prev)) err("asset_no", no, "is also used on row " + prev); else seenNo[no] = rowNo;
                d["assetNo"] = no;
            }
            d["tagNo"] = g("tag_no"); d["description"] = g("description"); d["serialNo"] = g("serial_no"); d["model"] = g("model"); d["make"] = g("make");
            if (g("quantity") != "") { int q; if (int.TryParse(g("quantity"), out q) && q >= 1) d["quantity"] = q; else err("quantity", g("quantity"), "must be a whole number of 1 or more"); }
            d["building"] = g("building"); d["room"] = g("room");
            string dp = g("department"); int did;
            if (dp != "") { if (lk.Depts.TryGetValue(dp, out did)) d["departmentId"] = did; else err("department", dp, "is not a department (see the Lists sheet)"); }
            string sn = g("custodian_staff_no"); List<int> emps;
            if (sn != "")
            {
                if (!lk.Staff.TryGetValue(sn, out emps)) err("custodian_staff_no", sn, "is not a staff number");
                else if (emps.Count > 1) err("custodian_staff_no", sn, "belongs to more than one staff record; fix the staff records or leave it blank and assign later");
                else d["custodianEmpId"] = emps[0];
            }
            string sup = g("supplier"); d["supplierName"] = sup; int supId; if (sup != "" && lk.Suppliers.TryGetValue(sup, out supId)) d["supplierId"] = supId;
            d["invoiceRef"] = g("invoice_ref"); d["orderRef"] = g("order_ref"); d["fundingSource"] = g("funding_source"); d["notes"] = g("notes");
            DateTime? we; string wee = Date(g("warranty_expiry"), out we); if (wee != null) err("warranty_expiry", g("warranty_expiry"), wee); else if (we.HasValue) d["warrantyExpiry"] = we.Value.ToString("yyyy-MM-dd");
            string m = g("method").ToUpperInvariant();
            if (m == "STRAIGHT LINE") m = "SL"; else if (m == "REDUCING BALANCE") m = "RB"; else if (m == "NOT DEPRECIATED") m = "NONE";
            if (m != "" && m != "SL" && m != "RB" && m != "NONE") err("method", g("method"), "must be SL, RB or NONE"); else d["method"] = m;
            decimal? life, rate, res; string e1 = Money(g("useful_life_years"), out life), e2 = Money(g("rate_pct"), out rate), e3 = Money(g("residual_value"), out res);
            if (e1 != null) err("useful_life_years", g("useful_life_years"), e1); else if (life.HasValue) d["lifeYears"] = life.Value.ToString(CultureInfo.InvariantCulture);
            if (e2 != null) err("rate_pct", g("rate_pct"), e2); else if (rate.HasValue) d["ratePct"] = rate.Value.ToString(CultureInfo.InvariantCulture);
            if (e3 != null) err("residual_value", g("residual_value"), e3); else if (res.HasValue) d["residualValue"] = res.Value.ToString(CultureInfo.InvariantCulture);
            if (m != "" && (life.HasValue || rate.HasValue) == false && m != "NONE")
            {
                // Method given without its parameter: fill from the sub-category at create time.
            }
            DateTime? od; string oe = Date(g("opening_date"), out od);
            if (oe != null) err("opening_date", g("opening_date"), oe);
            else if (od.HasValue)
            {
                d["opening"] = "true"; d["openingDate"] = od.Value.ToString("yyyy-MM-dd");
                string oa = g("opening_accum_dep");
                if (oa.ToUpperInvariant() == "CALC" || oa == "") d["openingAccumDep"] = "CALC";
                else { decimal? a; string ae = Money(oa, out a); if (ae != null) err("opening_accum_dep", oa, "must be an amount or CALC"); else d["openingAccumDep"] = a.Value.ToString(CultureInfo.InvariantCulture); }
            }
            else if (g("opening_accum_dep") != "") err("opening_date", "", "is needed when opening_accum_dep is filled in");
            outp.Add(d);
        }
        if (outp.Count == 0 && errors.Count == 0) errors.Add(new RowError { row = 2, column = "", value = "", message = "There are no asset rows in the file." });
        return outp;
    }

    // ─────────────────────────── Validate and commit ───────────────────────────

    private static string Sha1(byte[] b)
    {
        using (var h = SHA1.Create()) { var sb = new StringBuilder(); foreach (byte x in h.ComputeHash(b)) sb.Append(x.ToString("x2")); return sb.ToString(); }
    }

    /// <summary>Reads, maps and rehearses the whole file. Stores a batch. Returns the report.</summary>
    public static object Validate(string fileName, byte[] bytes)
    {
        string readErr;
        var sheet = Read(fileName, bytes, out readErr);
        if (readErr != null) return new { success = false, message = readErr };
        var errors = new List<RowError>();
        var warnings = new List<object>();
        List<Dictionary<string, object>> rows;
        int ok = 0;
        string sha = Sha1(bytes);
        bool seenBefore;
        using (var c = FaDb.Open())
        {
            seenBefore = FaDb.Scalar(c, null, "SELECT 1 FROM fa_import_batch WHERE sha1=@s AND status='COMMITTED'", "@s", sha) != null;
            using (var tx = c.BeginTransaction())
            {
                rows = Map(c, tx, sheet, errors);
                // Rehearse every row that read cleanly, even when other rows have format errors, so one upload reports everything.
                var badRows = new HashSet<int>();
                foreach (var e0 in errors) badRows.Add(e0.row);
                bool structural = errors.Exists(delegate (RowError e1) { return e1.row <= 1; });
                if (!structural)
                {
                    foreach (var d in rows)
                    {
                        if (badRows.Contains(FaJson.Int(d, "_row"))) continue;
                        var w = new List<string>();
                        int id; string no;
                        string e;
                        try { e = FaAssets.CreateCore(c, tx, d, "", 0, w, out id, out no); }
                        catch (MySqlException ex) { e = ex.Number == 1062 ? "the asset number is already in use" : "could not be saved (" + ex.Number + ")"; }
                        int rn = FaJson.Int(d, "_row");
                        if (e != null) errors.Add(new RowError { row = rn, column = "", value = FaJson.Str(d, "name"), message = e });
                        else ok++;
                        foreach (string s in w) warnings.Add(new { row = rn, message = s });
                    }
                }
                tx.Rollback();         // rehearsal only
            }
        }
        if (seenBefore) warnings.Insert(0, new { row = 0, message = "This exact file has been imported before. Importing it again will create duplicates." });

        long batchId;
        using (var c = FaDb.Open())
        using (var tx = c.BeginTransaction())
        {
            batchId = FaDb.Insert(c, tx,
                "INSERT INTO fa_import_batch (file_name, sha1, rows_total, rows_ok, rows_error, status, payload, report, created_by, created_at) " +
                "VALUES (@f,@s,@t,@o,@e,'VALIDATED',@p,@r,@u,NOW())",
                "@f", FaAudit.Cut(Path.GetFileName(fileName), 200), "@s", sha, "@t", rows.Count, "@o", errors.Count == 0 ? ok : 0,
                "@e", errors.Count, "@p", FaJson.Ser(rows), "@r", FaJson.Ser(new { errors = errors, warnings = warnings }), "@u", FaAccess.Username());
            FaAudit.Write(c, tx, "IMPORT", batchId, null, "VALIDATE", null, new { file = fileName, rows = rows.Count, errors = errors.Count }, null,
                          "Asset import checked: " + Path.GetFileName(fileName) + ", " + rows.Count + " rows, " + errors.Count + " errors");
            tx.Commit();
        }
        errors.Sort(delegate (RowError a1, RowError b1) { return a1.row.CompareTo(b1.row); });
        return new { success = true, batchId = batchId, total = rows.Count, ok = errors.Count == 0 ? ok : 0, errorCount = errors.Count,
                     errors = errors.Count > 500 ? errors.GetRange(0, 500) : errors, warnings = warnings, canCommit = errors.Count == 0 && rows.Count > 0 };
    }

    public static string Commit(long batchId, out int created)
    {
        created = 0;
        using (var c = FaDb.Open())
        using (var tx = c.BeginTransaction())
        {
            try
            {
                DataTable t = FaDb.Table(c, tx, "SELECT * FROM fa_import_batch WHERE id=@id FOR UPDATE", "@id", batchId);
                if (t.Rows.Count == 0) { tx.Rollback(); return "That import no longer exists."; }
                DataRow b = t.Rows[0];
                if (FaDb.S(b["status"]) != "VALIDATED") { tx.Rollback(); return "This import has already been " + FaDb.S(b["status"]).ToLowerInvariant() + "."; }
                if (FaDb.I(b["rows_error"]) > 0) { tx.Rollback(); return "Fix the errors and upload the file again."; }
                if (FaDb.S(b["created_by"]) != FaAccess.Username() && !RoleAccessService.IsAdmin()) { tx.Rollback(); return "Only the person who checked the file can import it."; }
                var arr = FaJson.J.DeserializeObject(FaDb.S(b["payload"])) as object[];
                if (arr == null || arr.Length == 0) { tx.Rollback(); return "The import has no rows."; }
                foreach (object o in arr)
                {
                    var d = o as Dictionary<string, object>;
                    var w = new List<string>(); int id; string no;
                    string e = FaAssets.CreateCore(c, tx, d, "", batchId, w, out id, out no);
                    if (e != null) { tx.Rollback(); return "Row " + FaJson.Int(d, "_row") + ": " + e + ". Nothing was imported; check the file again."; }
                    created++;
                }
                FaDb.Exec(c, tx, "UPDATE fa_import_batch SET status='COMMITTED', committed_by=@u, committed_at=NOW() WHERE id=@id", "@u", FaAccess.Username(), "@id", batchId);
                FaAudit.Write(c, tx, "IMPORT", batchId, null, "COMMIT", new { status = "VALIDATED" }, new { status = "COMMITTED", created = created }, null,
                              "Asset import committed: " + FaFmt.Plural(created, "asset", "assets") + " from " + FaDb.S(b["file_name"]));
                tx.Commit();
            }
            catch (MySqlException ex)
            {
                try { tx.Rollback(); } catch { }
                FaLog.Error("FaImport.Commit", ex);
                created = 0;
                return "The import could not be saved. Nothing was imported.";
            }
        }
        return null;
    }

    public static string Abandon(long batchId)
    {
        using (var c = FaDb.Open())
        using (var tx = c.BeginTransaction())
        {
            int n = FaDb.Exec(c, tx, "UPDATE fa_import_batch SET status='ABANDONED' WHERE id=@id AND status='VALIDATED'", "@id", batchId);
            if (n == 0) { tx.Rollback(); return "That import cannot be abandoned."; }
            FaAudit.Write(c, tx, "IMPORT", batchId, null, "ABANDON", null, null, null, "Asset import abandoned: batch " + batchId);
            tx.Commit();
        }
        return null;
    }

    public static List<object> Recent(MySqlConnection c)
    {
        var l = new List<object>();
        foreach (DataRow r in FaDb.Table(c, null, "SELECT id, file_name, rows_total, rows_error, status, created_by, created_at, committed_at FROM fa_import_batch ORDER BY id DESC LIMIT 15").Rows)
            l.Add(new { id = FaDb.L(r["id"]), file = FaDb.S(r["file_name"]), rows = FaDb.I(r["rows_total"]), errors = FaDb.I(r["rows_error"]),
                        status = FaDb.S(r["status"]), statusLabel = FaDb.S(r["status"]) == "COMMITTED" ? "Imported" : FaDb.S(r["status"]) == "VALIDATED" ? "Checked" : "Abandoned",
                        by = FaDb.S(r["created_by"]), at = FaFmt.Date(r["created_at"]) });
        return l;
    }
}
