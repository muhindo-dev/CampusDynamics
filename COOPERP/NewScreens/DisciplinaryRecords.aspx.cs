using System;
using System.Collections.Generic;
using System.Data;
using System.Web.Services;

// Student Disciplinary module: Disciplinary Records (plan 5.2). The case list, the new-case form and list export.
public partial class COOPERP_NewScreens_DisciplinaryRecords : System.Web.UI.Page
{
    protected string BootJson = "{}";

    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, DcAccess.Records);
        if (Request.HttpMethod == "POST" && !string.IsNullOrEmpty(Request.Form["faFormat"])) { Export(); return; }
        using (var c = FaDb.Open())
        {
            var boot = new Dictionary<string, object>();
            boot["rights"] = DcAccess.RightsJson();
            boot["campuses"] = DcLookups.Campuses(c);
            boot["faculties"] = DcLookups.Faculties(c);
            boot["departments"] = DcLookups.Departments(c);
            boot["programmes"] = DcLookups.Programmes(c);
            boot["types"] = DcLookups.CaseTypes(c, true);
            boot["interim"] = InterimChoices(c);
            boot["years"] = DcLookups.AcadYears(c);
            boot["year"] = DcSeq.AcadYear();
            boot["now"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm");
            boot["me"] = DcAccess.Username();
            BootJson = FaJson.Ser(boot).Replace("<", "\\u003c");
        }
    }

    private static List<object> InterimChoices(MySql.Data.MySqlClient.MySqlConnection c)
    {
        var l = new List<object>();
        foreach (DataRow r in FaDb.Table(c, null, "SELECT code, name, effects FROM dc_sanction_type WHERE is_active=1 AND allowed_interim=1 ORDER BY sort_order").Rows)
            l.Add(new { code = FaDb.S(r[0]), name = FaDb.S(r[1]), effects = DcFmt.Effects(FaDb.S(r[2])) });
        return l;
    }

    private void Export()
    {
        if (!MarksAntiForgeryService.ValidateRequest()) { Response.StatusCode = 403; Response.Write("Security check failed. Reload the page."); Response.End(); return; }
        try
        {
            var f = FaJson.Parse(Request.Form["faConfig"]);
            int total;
            DataTable t;
            using (var c = FaDb.Open()) t = DcCases.Query(c, f, 1, 0, out total);
            var d = new DcReportData { Key = "list", Title = "Disciplinary records", FileWhat = "records", Landscape = true };
            d.Cols.Add(new DcCol("case_no", "Case no", 80)); d.Cols.Add(new DcCol("opened", "Reported", 62)); d.Cols.Add(new DcCol("student", "Student", 120));
            d.Cols.Add(new DcCol("regno", "Reg no", 90)); d.Cols.Add(new DcCol("programme", "Programme", 120)); d.Cols.Add(new DcCol("campus", "Campus", 60));
            d.Cols.Add(new DcCol("type", "Type", 100)); d.Cols.Add(new DcCol("severity", "Severity", 50)); d.Cols.Add(new DcCol("status", "Status", 70));
            d.Cols.Add(new DcCol("hearing", "Next hearing", 80)); d.Cols.Add(new DcCol("in_force", "In force", 120)); d.Cols.Add(new DcCol("updated", "Updated", 62));
            d.Cols.Add(new DcCol("officer", "Officer", 70) { On = false });
            foreach (DataRow r in t.Rows)
                d.Rows.Add(new object[] { FaDb.S(r["case_no"]), DcFmt.Date(r["created_at"]), FaDb.S(r["student_name"]), FaDb.S(r["regno"]), FaDb.S(r["progname"]),
                    DcLookups.Title(FaDb.S(r["campus_name"])), FaDb.S(r["type_name"]), DcFmt.Severity(FaDb.S(r["severity"])), DcFmt.Status(FaDb.S(r["status"])),
                    DcFmt.When(r["hearing_at"]), FaDb.S(r["in_force"]), DcFmt.Date(r["last_entry_at"]), FaDb.S(r["case_officer"]) });
            d.Cover.Add(new KeyValuePair<string, string>("Visibility", DcAccess.Current().SeeRestricted ? "Includes restricted cases" : "Restricted cases are not included"));
            using (var c = FaDb.Open())
                foreach (DataRow r in t.Rows)
                    if (FaDb.I(r["is_restricted"]) == 1) DcAccess.LogRestrictedAccess(c, r, "EXPORT");
            DcReports.Send(Response, d, Request.Form["faFormat"], Request.Form["faCols"]);
        }
        catch (Exception ex)
        {
            DcLog.Error("DisciplinaryRecords.Export", ex);
            Response.Clear(); Response.ContentType = "text/plain"; Response.Write("The list could not be exported. Go back and try again."); Response.End();
        }
    }

    [WebMethod(EnableSession = true)]
    public static string GetCases(string configJson, int page, int size)
    {
        return DcApi.Read(DcAccess.Records, delegate
        {
            int total;
            var rows = new List<object>();
            using (var c = FaDb.Open())
                foreach (DataRow r in DcCases.Query(c, FaJson.Parse(configJson), Math.Max(1, page), Math.Min(Math.Max(size, 10), 200), out total).Rows)
                    rows.Add(DcCases.RowJson(r));
            return FaJson.Ser(new { success = true, rows = rows, total = total });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string CountExport(string report, string configJson, string groupBy)
    {
        return DcApi.Read(DcAccess.Records, delegate
        {
            using (var c = FaDb.Open()) return FaJson.Ser(new { success = true, count = DcCases.Count(c, FaJson.Parse(configJson)) });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string SearchStudents(string q)
    {
        return DcApi.Read(DcAccess.Records, delegate { using (var c = FaDb.Open()) return FaJson.Ser(new { success = true, rows = DcLookups.SearchStudents(c, q) }); });
    }

    [WebMethod(EnableSession = true)]
    public static string SearchStaff(string q)
    {
        return DcApi.Read(DcAccess.Records, delegate { using (var c = FaDb.Open()) return FaJson.Ser(new { success = true, rows = DcLookups.SearchStaff(c, q) }); });
    }

    [WebMethod(EnableSession = true)]
    public static string SearchTickets(string q)
    {
        return DcApi.Read(DcAccess.Records, delegate { using (var c = FaDb.Open()) return FaJson.Ser(new { success = true, rows = DcLookups.SearchTickets(c, q) }); });
    }

    [WebMethod(EnableSession = true)]
    public static string SearchExamPapers(string q)
    {
        return DcApi.Read(DcAccess.Records, delegate { using (var c = FaDb.Open()) return FaJson.Ser(new { success = true, rows = DcLookups.SearchExamPapers(c, q) }); });
    }

    [WebMethod(EnableSession = true)]
    public static string CreateIncident(string json)
    {
        return DcApi.Write(DcAccess.Records, delegate
        {
            if (!DcAccess.CanAny(DcAccess.Report, DcAccess.Manage, DcAccess.ManageAll, DcAccess.ManageExam) && !DcAccess.IsAdmin())
                return FaJson.Fail("You do not have permission to report cases.");
            var res = DcCases.Create(FaJson.Parse(json));
            var d = new Dictionary<string, object>();
            d["success"] = true;
            foreach (var p in res.GetType().GetProperties()) d[p.Name] = p.GetValue(res, null);
            return FaJson.Ser(d);
        });
    }

    /// <summary>One joint hearing for several cases (for example the students in one incident). Each case gets its own summons.</summary>
    [WebMethod(EnableSession = true)]
    public static string ScheduleJointHearing(string json)
    {
        return DcApi.Write(DcAccess.Hearing, delegate
        {
            var d = FaJson.Parse(json);
            var ids = FaJson.IntList(d, "caseIds");
            if (ids.Count == 0) return FaJson.Fail("Select the cases first.");
            if (ids.Count > 40) return FaJson.Fail("Schedule at most 40 cases at once.");
            var done = new List<string>(); var failed = new List<string>();
            foreach (int id in ids)
            {
                var one = new Dictionary<string, object>(d);
                one["caseId"] = id; one.Remove("version"); one["opId"] = "";
                try { DcWorkflow.ScheduleHearing(one); done.Add(id.ToString()); }
                catch (DcRefusal r) { failed.Add(CaseNo(id) + ": " + r.Message); }
                catch (MySql.Data.MySqlClient.MySqlException mx) { failed.Add(CaseNo(id) + ": " + (mx.Number == 1644 ? mx.Message : "not saved")); DcLog.Error("joint hearing " + id, mx); }
            }
            return FaJson.Ser(new { success = true, done = done.Count, failed = failed });
        });
    }

    private static string CaseNo(int id)
    {
        try { using (var c = FaDb.Open()) { var r = DcAccess.VisibleCase(c, null, id, false); return r == null ? "Case " + id : FaDb.S(r["case_no"]); } }
        catch { return "Case " + id; }
    }
}
