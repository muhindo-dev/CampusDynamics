<%@ Control Language="C#" ClassName="DcStudentBanner" %>
<script runat="server">
    // Student Disciplinary module: a one-line banner on staff screens about a student who is under a
    // disciplinary restriction (plan 6.3). Self-contained: it finds the student from ?regno= (registration
    // or entry number) unless Regno is set. Shows restrictions to everyone who can open the student, and the
    // case numbers and a link only to officers who may open Student Discipline. Says nothing when clear.
    public string Regno { get; set; }
    /// <summary>ID of a HiddenField on the page that holds the student (for screens that load the student on postback).</summary>
    public string RegnoField { get; set; }

    private static Control Find(Control root, string id)
    {
        if (root == null) return null;
        if (root.ID == id) return root;
        foreach (Control ch in root.Controls) { Control f = Find(ch, id); if (f != null) return f; }
        return null;
    }

    protected string Html()
    {
        try
        {
            string key = Regno;
            if (string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(RegnoField))
            {
                var hf = Find(Page, RegnoField) as HiddenField;
                if (hf == null) return "";
                key = hf.Value;
            }
            if (string.IsNullOrEmpty(key) && string.IsNullOrEmpty(RegnoField)) key = Request.QueryString["regno"] ?? Request.QueryString["reg"];
            key = (key ?? "").Trim();
            if (key == "") return "";
            DcClearanceResult r;
            using (var c = FaDb.Open())
            {
                string regno = FaDb.S(FaDb.Scalar(c, null, "SELECT regno FROM acad_student WHERE regno=@r OR entryno=@r LIMIT 1", "@r", key));
                if (regno == "") return "";
                r = DcClearance.Check(c, null, regno);
            }
            if (r.Effects.Count == 0 && r.OpenCases == 0 && !r.PendingCancellation) return "";
            bool officer = DcAccess.Can(DcAccess.Records);
            string text;
            if (r.Effects.Count > 0)
                text = "Disciplinary restriction in force: " + (officer ? r.Summary : DcFmt.Restrictions(string.Join(",", new System.Collections.Generic.List<string>(r.Effects).ToArray()))) + ".";
            else if (!officer) return "";
            else if (r.PendingCancellation) text = r.Summary + ".";
            else text = r.Summary + " (no restriction in force).";
            string link = officer ? " <a href=\"" + ResolveUrl("~/COOPERP/NewScreens/DisciplinaryRecords.aspx") + "?q=" + HttpUtility.UrlEncode(r.Regno) +
                                     "\" style=\"color:#05275C;font-weight:600\">Open in Student Discipline</a>" : "";
            return "<div role=\"status\" style=\"margin:10px 0;padding:9px 12px;border:1px solid #f4c7c3;border-left:3px solid #b42318;background:#fdecec;color:#7a1a12;font-size:12.5px\">" +
                   "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"13\" height=\"13\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" style=\"vertical-align:-2px;margin-right:6px\"><path d=\"M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z\"/></svg>" +
                   HttpUtility.HtmlEncode(text) + link + "</div>";
        }
        catch (Exception ex) { DcLog.Error("DcStudentBanner", ex); return ""; }
    }
</script>
<%= Html() %>
