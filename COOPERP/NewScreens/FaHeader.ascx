<%@ Control Language="C#" ClassName="FaHeader" %>
<script runat="server">
    // Fixed Assets: the header and module tabs shared by every Fixed Assets screen.
    public string Title { get; set; }
    public string Sub { get; set; }
    public string Icon { get; set; }
    public string Active { get; set; }

    private static readonly System.Collections.Generic.Dictionary<string, string> Icons = new System.Collections.Generic.Dictionary<string, string> {
        { "chart", "<line x1='18' y1='20' x2='18' y2='10'/><line x1='12' y1='20' x2='12' y2='4'/><line x1='6' y1='20' x2='6' y2='14'/>" },
        { "box", "<path d='M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z'/><polyline points='3.27 6.96 12 12.01 20.73 6.96'/><line x1='12' y1='22.08' x2='12' y2='12'/>" },
        { "list", "<line x1='8' y1='6' x2='21' y2='6'/><line x1='8' y1='12' x2='21' y2='12'/><line x1='8' y1='18' x2='21' y2='18'/><line x1='3' y1='6' x2='3.01' y2='6'/><line x1='3' y1='12' x2='3.01' y2='12'/><line x1='3' y1='18' x2='3.01' y2='18'/>" },
        { "layers", "<polygon points='12 2 2 7 12 12 22 7 12 2'/><polyline points='2 17 12 22 22 17'/><polyline points='2 12 12 17 22 12'/>" },
        { "file", "<path d='M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z'/><polyline points='14 2 14 8 20 8'/><line x1='16' y1='13' x2='8' y2='13'/><line x1='16' y1='17' x2='8' y2='17'/>" },
        { "upload", "<path d='M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4'/><polyline points='17 8 12 3 7 8'/><line x1='12' y1='3' x2='12' y2='15'/>" } };

    protected string IconSvg()
    {
        string p; if (!Icons.TryGetValue(Icon ?? "box", out p)) p = Icons["box"];
        return "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'>" + p + "</svg>";
    }

    protected string Tabs()
    {
        string[][] t = {
            new[] { "dashboard", "AssetsDashboard.aspx", "Dashboard", FaAccess.Dashboard },
            new[] { "assets", "Assets.aspx", "Assets", FaAccess.Register },
            new[] { "records", "AssetRecords.aspx", "Asset records", FaAccess.Records },
            new[] { "categories", "AssetCategories.aspx", "Categories", FaAccess.Categories },
            new[] { "reports", "AssetReports.aspx", "Reports", FaAccess.Reports } };
        var sb = new System.Text.StringBuilder();
        foreach (string[] x in t)
        {
            if (!FaAccess.Can(x[3])) continue;
            sb.Append("<a class=\"fa-tab").Append(x[0] == Active ? " fa-tab--active" : "").Append("\" href=\"").Append(x[1]).Append("\">")
              .Append(x[2]).Append("</a>");
        }
        return sb.ToString();
    }
</script>
<div class="fa-header">
    <div class="fa-header__left">
        <div class="fa-header__icon"><%= IconSvg() %></div>
        <div>
            <div class="fa-header__title"><%= HttpUtility.HtmlEncode(Title) %></div>
            <div class="fa-header__sub"><%= HttpUtility.HtmlEncode(Sub) %></div>
        </div>
    </div>
    <div class="fa-header__actions" id="faHeaderActions"></div>
</div>
<div class="fa-tabs"><%= Tabs() %></div>
