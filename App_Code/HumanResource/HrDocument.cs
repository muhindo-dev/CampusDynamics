using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web;

/// <summary>
/// One print standard for HR documents (appraisal forms, payslips, registers, renewal packs,
/// leave forms, session reports). A screen builds the body with the helpers below and wraps it
/// with <see cref="Page"/>, which adds the letterhead, the print toolbar and the footer.
/// Restrained official layout: crest, university name, office, document title between rules,
/// navy text headings, thin-ruled tables, signature blocks. No colour fills beyond a light band.
/// </summary>
public static class HrDocument
{
    public class Options
    {
        public string Office = "Human Resource Office";
        public string Reference = "";          // e.g. "Ref: CR-2026-0004" or "Period: October 2026"
        public bool Landscape = false;
        public bool Confidential = true;
        public string BackUrl = "";            // toolbar "Back" link; empty = history.back()
    }

    public static string E(string s) { return HttpUtility.HtmlEncode(HrExport.Clean(s ?? "")); }

    /// <summary>Encoded text with line breaks kept.</summary>
    public static string Multiline(string s) { return E(s).Replace("\r\n", "<br/>").Replace("\n", "<br/>"); }

    public static string Page(string title, string bodyHtml, Options o)
    {
        if (o == null) o = new Options();
        HttpRequest req = HttpContext.Current != null ? HttpContext.Current.Request : null;
        string crest = req == null ? "" : req.Url.GetLeftPart(UriPartial.Authority) + VirtualPathUtility.ToAbsolute("~/COOPERP/images/mru-crest.png");
        string printed = DateTime.Now.ToString("d MMMM yyyy, HH:mm", CultureInfo.InvariantCulture);

        StringBuilder h = new StringBuilder();
        h.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\" />");
        h.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        h.Append("<title>").Append(E(title)).Append(" | Muteesa I Royal University</title>");
        h.Append("<style>").Append(Css(o.Landscape)).Append("</style></head><body>");
        h.Append("<div class=\"tb no-print\">");
        h.Append(string.IsNullOrEmpty(o.BackUrl) ? "<a href=\"javascript:history.back()\">Back</a>" : "<a href=\"" + HttpUtility.HtmlAttributeEncode(o.BackUrl) + "\">Back</a>");
        h.Append("<button type=\"button\" onclick=\"window.print()\">Print</button></div>");
        h.Append("<div class=\"doc\">");
        h.Append("<div class=\"lh\">");
        if (crest != "") h.Append("<img src=\"").Append(HttpUtility.HtmlAttributeEncode(crest)).Append("\" alt=\"\" />");
        h.Append("<div class=\"lh__t\"><div class=\"lh__u\">MUTEESA I ROYAL UNIVERSITY</div>");
        h.Append("<div class=\"lh__o\">").Append(E(o.Office)).Append("</div></div>");
        if (crest != "") h.Append("<div class=\"lh__sp\"></div>");
        h.Append("</div>");
        h.Append("<div class=\"ttl\">").Append(E(title).ToUpperInvariant()).Append("</div>");
        if (!string.IsNullOrEmpty(o.Reference)) h.Append("<div class=\"ref\">").Append(E(o.Reference)).Append("</div>");
        h.Append(bodyHtml);
        h.Append("<div class=\"ft\"><span>Muteesa I Royal University").Append(o.Confidential ? " | Confidential" : "")
         .Append("</span><span>Printed ").Append(printed).Append("</span></div>");
        h.Append("</div></body></html>");
        return h.ToString();
    }

    /// <summary>Label/value grid, two pairs per row. Values are encoded.</summary>
    public static string Meta(params string[] labelValue)
    {
        StringBuilder h = new StringBuilder("<table class=\"meta\">");
        for (int i = 0; i + 1 < labelValue.Length; i += 4)
        {
            h.Append("<tr><th>").Append(E(labelValue[i])).Append("</th><td>").Append(Or(labelValue[i + 1])).Append("</td>");
            if (i + 3 < labelValue.Length)
                h.Append("<th>").Append(E(labelValue[i + 2])).Append("</th><td>").Append(Or(labelValue[i + 3])).Append("</td>");
            else h.Append("<th></th><td></td>");
            h.Append("</tr>");
        }
        return h.Append("</table>").ToString();
    }

    private static string Or(string v) { return string.IsNullOrEmpty(v) ? "&nbsp;" : E(v); }

    public static string Heading(string text) { return "<h2>" + E(text) + "</h2>"; }

    public static string Paragraph(string text) { return "<p>" + Multiline(text) + "</p>"; }

    /// <summary>Table from already-encoded cell HTML. numeric[i] right-aligns column i.</summary>
    public static string Table(string[] headers, List<string[]> rows, bool[] numeric, string[] totals)
    {
        StringBuilder h = new StringBuilder("<table class=\"grid\"><thead><tr>");
        for (int i = 0; i < headers.Length; i++)
            h.Append("<th").Append(IsNum(numeric, i) ? " class=\"n\"" : "").Append(">").Append(E(headers[i])).Append("</th>");
        h.Append("</tr></thead><tbody>");
        if (rows == null || rows.Count == 0)
            h.Append("<tr><td colspan=\"").Append(headers.Length).Append("\" class=\"none\">None recorded.</td></tr>");
        else
            foreach (string[] r in rows)
            {
                h.Append("<tr>");
                for (int i = 0; i < headers.Length; i++)
                    h.Append("<td").Append(IsNum(numeric, i) ? " class=\"n\"" : "").Append(">").Append(i < r.Length ? r[i] : "").Append("</td>");
                h.Append("</tr>");
            }
        h.Append("</tbody>");
        if (totals != null)
        {
            h.Append("<tfoot><tr>");
            for (int i = 0; i < headers.Length; i++)
                h.Append("<td").Append(IsNum(numeric, i) ? " class=\"n\"" : "").Append(">").Append(i < totals.Length ? totals[i] : "").Append("</td>");
            h.Append("</tr></tfoot>");
        }
        return h.Append("</table>").ToString();
    }

    private static bool IsNum(bool[] n, int i) { return n != null && i < n.Length && n[i]; }

    /// <summary>Signature blocks. Each entry: role, name (optional), e-signed line (optional).</summary>
    public static string Signatures(params string[] roleNameSigned)
    {
        StringBuilder h = new StringBuilder("<div class=\"sig\">");
        for (int i = 0; i + 2 < roleNameSigned.Length + 2; i += 3)
        {
            if (i >= roleNameSigned.Length) break;
            string role = roleNameSigned[i];
            string name = i + 1 < roleNameSigned.Length ? roleNameSigned[i + 1] : "";
            string signed = i + 2 < roleNameSigned.Length ? roleNameSigned[i + 2] : "";
            h.Append("<div><div class=\"sig__role\">").Append(E(role)).Append("</div>");
            if (!string.IsNullOrEmpty(name)) h.Append("<div class=\"sig__name\">").Append(E(name)).Append("</div>");
            if (!string.IsNullOrEmpty(signed)) h.Append("<div class=\"sig__e\">").Append(E(signed)).Append("</div>");
            h.Append("<div class=\"sig__line\"><span>Signature</span><span>Date</span></div></div>");
        }
        return h.Append("</div>").ToString();
    }

    public static string Money(object v)
    {
        if (v == null || v == DBNull.Value) return "";
        decimal d;
        return decimal.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d)
            ? d.ToString("#,##0", CultureInfo.InvariantCulture) : E(v.ToString());
    }

    public static string Date(object v)
    {
        if (v == null || v == DBNull.Value) return "";
        DateTime d;
        return DateTime.TryParse(v.ToString(), out d) ? d.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : E(v.ToString());
    }

    private static string Css(bool landscape)
    {
        return
"*{box-sizing:border-box;-webkit-print-color-adjust:exact;print-color-adjust:exact;}" +
"body{margin:0;background:#fff;color:#1a1a2e;font-family:'Segoe UI',Arial,sans-serif;font-size:10pt;line-height:1.45;}" +
".doc{max-width:" + (landscape ? "1080px" : "820px") + ";margin:0 auto;padding:24px 28px;}" +
".tb{position:fixed;top:10px;right:14px;display:flex;gap:6px;z-index:10;}" +
".tb a,.tb button{background:#05275C;color:#fff;border:none;padding:6px 14px;font:600 11px 'Segoe UI',Arial,sans-serif;text-decoration:none;cursor:pointer;}" +
".lh{display:flex;align-items:center;gap:14px;padding-bottom:8px;border-bottom:2px solid #05275C;}" +
".lh img{width:64px;height:64px;object-fit:contain;flex:0 0 auto;}" +
".lh__t{flex:1;text-align:center;}" +
".lh__u{font-size:16pt;font-weight:700;color:#05275C;letter-spacing:.8px;}" +
".lh__o{font-size:10pt;color:#174DA4;margin-top:2px;}" +
".lh__sp{width:64px;flex:0 0 auto;}" +
".ttl{text-align:center;font-size:11.5pt;font-weight:700;letter-spacing:.6px;color:#1a1a2e;padding:8px 0;margin-top:2px;border-bottom:1px solid #c8d0dc;}" +
".ref{text-align:center;font-size:9pt;color:#555;margin:6px 0 4px;}" +
"h2{font-size:10.5pt;font-weight:700;color:#05275C;margin:16px 0 6px;padding-bottom:3px;border-bottom:1px solid #05275C;text-transform:uppercase;letter-spacing:.4px;}" +
"p{margin:4px 0 8px;}" +
"table{width:100%;border-collapse:collapse;}" +
".meta{margin-top:10px;font-size:9.5pt;}" +
".meta th{width:17%;text-align:left;font-weight:600;color:#555;padding:4px 8px;border:1px solid #d6dce5;background:#f5f7fa;font-size:8.5pt;text-transform:uppercase;letter-spacing:.3px;}" +
".meta td{width:33%;padding:4px 8px;border:1px solid #d6dce5;}" +
".grid{font-size:9pt;margin-top:4px;}" +
".grid th{text-align:left;padding:5px 7px;background:#eef2f8;color:#05275C;font-size:8pt;text-transform:uppercase;letter-spacing:.3px;border:1px solid #c8d0dc;}" +
".grid td{padding:5px 7px;border:1px solid #d6dce5;vertical-align:top;}" +
".grid tbody tr:nth-child(even) td{background:#f8fafc;}" +
".grid tfoot td{font-weight:700;color:#05275C;background:#eef2f8;border-top:2px solid #05275C;}" +
".grid .n{text-align:right;white-space:nowrap;}" +
".grid .none{color:#888;font-style:italic;text-align:center;}" +
".box{border:1px solid #d6dce5;padding:6px 9px;margin-top:6px;min-height:26px;}" +
".sig{display:grid;grid-template-columns:repeat(3,1fr);gap:22px;margin-top:26px;}" +
".sig__role{font-size:8.5pt;font-weight:700;color:#05275C;text-transform:uppercase;letter-spacing:.3px;border-top:1px solid #1a1a2e;padding-top:4px;}" +
".sig__name{font-size:9.5pt;margin-top:2px;}" +
".sig__e{font-size:8pt;color:#155724;margin-top:2px;}" +
".sig__line{display:flex;justify-content:space-between;margin-top:20px;font-size:8pt;color:#888;border-bottom:1px dotted #999;padding-bottom:2px;}" +
".ft{display:flex;justify-content:space-between;margin-top:22px;padding-top:6px;border-top:1px solid #c8d0dc;font-size:7.5pt;color:#888;}" +
"@media print{.no-print{display:none!important;}.doc{padding:0;max-width:none;}h2{page-break-after:avoid;}tr{page-break-inside:avoid;}.pb{page-break-before:always;}}" +
"@page{size:A4 " + (landscape ? "landscape" : "portrait") + ";margin:12mm 12mm 14mm;}";
    }
}
