<%@ Control Language="C#" ClassName="GlHeader" %>
<script runat="server">
    // General Ledger: the header and tabs shared by every Accounts screen (plan section 3).
    public string Title { get; set; }
    public string Sub { get; set; }
    public string Icon { get; set; }
    public string Active { get; set; }

    private static readonly System.Collections.Generic.Dictionary<string, string> Icons = new System.Collections.Generic.Dictionary<string, string> {
        { "chart", "<line x1='18' y1='20' x2='18' y2='10'/><line x1='12' y1='20' x2='12' y2='4'/><line x1='6' y1='20' x2='6' y2='14'/>" },
        { "book", "<path d='M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z'/><path d='M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z'/>" },
        { "file", "<path d='M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z'/><polyline points='14 2 14 8 20 8'/><line x1='16' y1='13' x2='8' y2='13'/><line x1='16' y1='17' x2='8' y2='17'/>" },
        { "alert", "<path d='M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z'/><line x1='12' y1='9' x2='12' y2='13'/><line x1='12' y1='17' x2='12.01' y2='17'/>" },
        { "edit", "<path d='M12 20h9'/><path d='M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z'/>" },
        { "calendar", "<rect x='3' y='4' width='18' height='18' rx='2' ry='2'/><line x1='16' y1='2' x2='16' y2='6'/><line x1='8' y1='2' x2='8' y2='6'/><line x1='3' y1='10' x2='21' y2='10'/>" },
        { "hash", "<line x1='4' y1='9' x2='20' y2='9'/><line x1='4' y1='15' x2='20' y2='15'/><line x1='10' y1='3' x2='8' y2='21'/><line x1='16' y1='3' x2='14' y2='21'/>" } };

    protected string IconSvg()
    {
        string p; if (!Icons.TryGetValue(Icon ?? "book", out p)) p = Icons["book"];
        return "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'>" + p + "</svg>";
    }

    protected string Tabs()
    {
        string[][] t = {
            new[] { "dashboard", "AccountsDashboard.aspx", "Dashboard", GlAccess.Dashboard },
            new[] { "reports", "AccountsReports.aspx", "Reports", GlAccess.Reports },
            new[] { "warnings", "AccountsWarnings.aspx", "Finance Warnings", GlAccess.Warnings },
            new[] { "adjust", "AccountsAdjustments.aspx", "Adjusting entries", GlAccess.Adjust },
            new[] { "periods", "AccountsPeriods.aspx", "Periods and close", GlAccess.Periods } };
        var sb = new System.Text.StringBuilder();
        foreach (string[] x in t)
        {
            if (!GlAccess.Can(x[3])) continue;
            sb.Append("<a class=\"fa-tab").Append(x[0] == Active ? " fa-tab--active" : "").Append("\" href=\"").Append(x[1]).Append("\">").Append(x[2]).Append("</a>");
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
    <div class="fa-header__actions" id="glHeaderActions"></div>
</div>
<div class="fa-tabs"><%= Tabs() %></div>
