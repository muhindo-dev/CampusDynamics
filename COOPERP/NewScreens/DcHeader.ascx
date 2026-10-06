<%@ Control Language="C#" ClassName="DcHeader" %>
<script runat="server">
    // Student Disciplinary module: the header and module tabs shared by every disciplinary screen.
    public string Title { get; set; }
    public string Sub { get; set; }
    public string Icon { get; set; }
    public string Active { get; set; }

    private static readonly System.Collections.Generic.Dictionary<string, string> Icons = new System.Collections.Generic.Dictionary<string, string> {
        { "chart", "<line x1='18' y1='20' x2='18' y2='10'/><line x1='12' y1='20' x2='12' y2='4'/><line x1='6' y1='20' x2='6' y2='14'/>" },
        { "shield", "<path d='M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z'/>" },
        { "folder", "<path d='M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z'/>" },
        { "activity", "<polyline points='22 12 18 12 15 21 9 3 6 12 2 12'/>" },
        { "sliders", "<line x1='4' y1='21' x2='4' y2='14'/><line x1='4' y1='10' x2='4' y2='3'/><line x1='12' y1='21' x2='12' y2='12'/><line x1='12' y1='8' x2='12' y2='3'/><line x1='20' y1='21' x2='20' y2='16'/><line x1='20' y1='12' x2='20' y2='3'/><line x1='1' y1='14' x2='7' y2='14'/><line x1='9' y1='8' x2='15' y2='8'/><line x1='17' y1='16' x2='23' y2='16'/>" },
        { "file", "<path d='M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z'/><polyline points='14 2 14 8 20 8'/><line x1='16' y1='13' x2='8' y2='13'/><line x1='16' y1='17' x2='8' y2='17'/>" } };

    protected string IconSvg()
    {
        string p; if (!Icons.TryGetValue(Icon ?? "shield", out p)) p = Icons["shield"];
        return "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'>" + p + "</svg>";
    }

    protected string Tabs()
    {
        string[][] t = {
            new[] { "dashboard", "DisciplinaryDashboard.aspx", "Dashboard", DcAccess.Dashboard },
            new[] { "records", "DisciplinaryRecords.aspx", "Disciplinary records", DcAccess.Records },
            new[] { "updates", "DisciplinaryUpdates.aspx", "Record updates", DcAccess.Updates },
            new[] { "settings", "DisciplinarySettings.aspx", "Case types and sanctions", DcAccess.Settings },
            new[] { "reports", "DisciplinaryReports.aspx", "Reports", DcAccess.Reports } };
        var sb = new System.Text.StringBuilder();
        foreach (string[] x in t)
        {
            if (!DcAccess.Can(x[3])) continue;
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
