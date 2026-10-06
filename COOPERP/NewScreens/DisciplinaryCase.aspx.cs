using System;
using System.Collections.Generic;
using System.Web.Services;

// Student Disciplinary module: the case file (plan 5.3). Timeline, sanctions, hearings, letters, attachments, audit,
// and every action the case allows. Each action re-checks permission and status on the server.
public partial class COOPERP_NewScreens_DisciplinaryCase : System.Web.UI.Page
{
    protected string BootJson = "{}";

    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, DcAccess.Records);
        if (Request.HttpMethod == "POST" && !string.IsNullOrEmpty(Request.Form["faFormat"])) { Export(); return; }
        int id; int.TryParse(Request.QueryString["id"] ?? "", out id);
        var boot = new Dictionary<string, object>();
        boot["id"] = id;
        boot["rights"] = DcAccess.RightsJson();
        boot["today"] = DateTime.Today.ToString("yyyy-MM-dd");
        boot["now"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm");
        boot["year"] = DcSeq.AcadYear();
        boot["me"] = DcAccess.Username();
        boot["appealDays"] = DcSettings.AppealWindowDays;
        boot["noticeDays"] = DcSettings.Int("summon_notice_days", 3);
        BootJson = FaJson.Ser(boot).Replace("<", "\\u003c");
    }

    private void Export()
    {
        if (!MarksAntiForgeryService.ValidateRequest()) { Response.StatusCode = 403; Response.Write("Security check failed. Reload the page."); Response.End(); return; }
        try
        {
            var d = DcReports.Run("statement", FaJson.Parse(Request.Form["faConfig"]), "");
            DcReports.Send(Response, d, Request.Form["faFormat"], Request.Form["faCols"]);
        }
        catch (DcRefusal r) { Response.Clear(); Response.ContentType = "text/plain"; Response.Write(r.Message); Response.End(); }
        catch (Exception ex)
        {
            DcLog.Error("DisciplinaryCase.Export", ex);
            Response.Clear(); Response.ContentType = "text/plain"; Response.Write("The statement could not be produced."); Response.End();
        }
    }

    [WebMethod(EnableSession = true)]
    public static string GetCase(int id)
    {
        return DcApi.Read(DcAccess.Records, delegate { return Wrap(DcCases.Detail(id)); });
    }

    private static string Wrap(object o)
    {
        var d = new Dictionary<string, object>();
        d["success"] = true;
        if (o != null) foreach (var p in o.GetType().GetProperties()) d[p.Name] = p.GetValue(o, null);
        return FaJson.Ser(d);
    }

    /// <summary>Every workflow action goes through here; DcWorkflow checks permission, status and fields, and writes one transaction.</summary>
    [WebMethod(EnableSession = true)]
    public static string Act(string action, string json)
    {
        return DcApi.Write(DcAccess.Records, delegate
        {
            var d = FaJson.Parse(json);
            switch (action)
            {
                case "entry": return Wrap(DcWorkflow.AddEntry(d));
                case "investigate": return Wrap(DcWorkflow.Investigate(d));
                case "officer": return Wrap(DcWorkflow.SetOfficer(d));
                case "restricted": return Wrap(DcWorkflow.SetRestricted(d));
                case "schedule": return Wrap(DcWorkflow.ScheduleHearing(d));
                case "summon": return Wrap(DcWorkflow.Summon(d));
                case "adjourn": return Wrap(DcWorkflow.Adjourn(d));
                case "hearing": return Wrap(DcWorkflow.RecordHearing(d));
                case "decide": return Wrap(DcWorkflow.RecordDecision(d));
                case "measure": return Wrap(DcWorkflow.ApplyMeasure(d));
                case "lift": return Wrap(DcWorkflow.EndSanction(d));
                case "vary": return Wrap(DcWorkflow.VarySanction(d));
                case "followup": return Wrap(DcWorkflow.FollowUpDone(d));
                case "appeal": return Wrap(DcWorkflow.LodgeAppeal(d));
                case "decideAppeal": return Wrap(DcWorkflow.DecideAppeal(d));
                case "letter": return Wrap(DcWorkflow.IssueLetter(d));
                case "notify": return Wrap(DcWorkflow.NotifyAgain(d));
                case "close": return Wrap(DcWorkflow.Close(d));
                case "withdraw": return Wrap(DcWorkflow.Withdraw(d));
                case "removeFile":
                    string err = DcFiles.Remove(FaJson.Int(d, "attachmentId"), FaJson.Str(d, "reason"));
                    return err == null ? Wrap(null) : FaJson.Fail(err);
                default: return FaJson.Fail("Unknown action.");
            }
        });
    }

    [WebMethod(EnableSession = true)]
    public static string PreviewLetter(int caseId, string template, string fieldsJson)
    {
        return DcApi.Read(DcAccess.Records, delegate { return Wrap(DcLetters.Preview(caseId, template, FaJson.Parse(fieldsJson))); });
    }

    [WebMethod(EnableSession = true)]
    public static string SearchStaff(string q)
    {
        return DcApi.Read(DcAccess.Records, delegate { using (var c = FaDb.Open()) return FaJson.Ser(new { success = true, rows = DcLookups.SearchStaff(c, q) }); });
    }

    [WebMethod(EnableSession = true)]
    public static string Committee()
    {
        return DcApi.Read(DcAccess.Records, delegate
        {
            var l = new List<object>();
            using (var c = FaDb.Open())
                foreach (System.Data.DataRow r in FaDb.Table(c, null,
                    "SELECT member_name, panel_role FROM dc_committee_member WHERE is_active=1 AND acad_year=@y AND panel_role<>'APPELLATE' ORDER BY FIELD(panel_role,'CHAIR','SECRETARY','MEMBER','STUDENT_REP'), sort_order, member_name",
                    "@y", DcSeq.AcadYear()).Rows)
                    l.Add(new { name = FaDb.S(r[0]), role = DcFmt.PanelRole(FaDb.S(r[1])) });
            return FaJson.Ser(new { success = true, rows = l });
        });
    }
}
