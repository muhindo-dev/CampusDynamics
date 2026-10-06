using System;
using System.Collections.Generic;
using System.Data;
using System.Web.Services;

// Student Disciplinary module: Record Updates (plan 5.4). A feed of case-file entries across every case the user may see.
public partial class COOPERP_NewScreens_DisciplinaryUpdates : System.Web.UI.Page
{
    protected string BootJson = "{}";

    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, DcAccess.Updates);
        if (Request.HttpMethod == "POST" && !string.IsNullOrEmpty(Request.Form["faFormat"])) { Export(); return; }
        using (var c = FaDb.Open())
        {
            var boot = new Dictionary<string, object>();
            boot["types"] = DcLookups.CaseTypes(c, false);
            boot["faculties"] = DcLookups.Faculties(c);
            boot["from"] = DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd");
            boot["today"] = DateTime.Today.ToString("yyyy-MM-dd");
            var et = new List<object>();
            foreach (string t in new[] { "CASE_OPENED", "NOTE", "FINDING", "STATEMENT", "EVIDENCE", "INTERIM_MEASURE", "HEARING_SCHEDULED", "SUMMON", "ADJOURNED", "HEARING_HELD",
                                         "DECISION", "APPEAL_LODGED", "APPEAL_DECISION", "SANCTION_VARIED", "SANCTION_LIFTED", "BLOCK_APPLIED", "BLOCK_LIFTED", "STATUS_CHANGE",
                                         "LETTER_ISSUED", "MARKS_ACTION", "FEES_ACTION", "CORRECTION", "CASE_CLOSED", "CASE_WITHDRAWN", "STUDENT_NOTIFIED", "SYSTEM" })
                et.Add(new { id = t, name = DcFmt.EntryType(t) });
            boot["entryTypes"] = et;
            BootJson = FaJson.Ser(boot).Replace("<", "\\u003c");
        }
    }

    private static object[] Row(DataRow r)
    {
        return new object[] { DcFmt.When(r["recorded_at"]), FaDb.S(r["case_no"]), FaDb.S(r["student_name"]), FaDb.S(r["regno"]), DcFmt.EntryType(FaDb.S(r["entry_type"])),
                              FaDb.S(r["title"]), FaDb.S(r["recorded_by"]), FaDb.I(r["student_visible"]) == 1 ? "Yes" : "No" };
    }

    private void Export()
    {
        if (!MarksAntiForgeryService.ValidateRequest()) { Response.StatusCode = 403; Response.Write("Security check failed. Reload the page."); Response.End(); return; }
        try
        {
            int total; DataTable t;
            using (var c = FaDb.Open()) t = DcCases.Feed(c, FaJson.Parse(Request.Form["faConfig"]), 1, 0, out total);
            var d = new DcReportData { Key = "updates", Title = "Record updates", FileWhat = "updates", Landscape = true, Noun = "entry", NounPlural = "entries" };
            d.Cols.Add(new DcCol("when", "Recorded", 80)); d.Cols.Add(new DcCol("case_no", "Case no", 80)); d.Cols.Add(new DcCol("student", "Student", 120));
            d.Cols.Add(new DcCol("regno", "Reg no", 90)); d.Cols.Add(new DcCol("entry", "Entry", 80)); d.Cols.Add(new DcCol("title", "Title", 200));
            d.Cols.Add(new DcCol("by", "Recorded by", 80)); d.Cols.Add(new DcCol("visible", "Student sees", 50));
            foreach (DataRow r in t.Rows) d.Rows.Add(Row(r));
            DcReports.Send(Response, d, Request.Form["faFormat"], Request.Form["faCols"]);
        }
        catch (Exception ex)
        {
            DcLog.Error("DisciplinaryUpdates.Export", ex);
            Response.Clear(); Response.ContentType = "text/plain"; Response.Write("The feed could not be exported."); Response.End();
        }
    }

    [WebMethod(EnableSession = true)]
    public static string GetFeed(string configJson, int page, int size)
    {
        return DcApi.Read(DcAccess.Updates, delegate
        {
            int total; var rows = new List<object>();
            using (var c = FaDb.Open())
                foreach (DataRow r in DcCases.Feed(c, FaJson.Parse(configJson), Math.Max(1, page), Math.Min(Math.Max(size, 10), 200), out total).Rows)
                {
                    string body = FaDb.S(r["body"]); if (body.Length > 240) body = body.Substring(0, 237) + "...";
                    rows.Add(new
                    {
                        id = FaDb.I(r["id"]), caseId = FaDb.I(r["case_id"]), caseNo = FaDb.S(r["case_no"]), name = FaDb.S(r["student_name"]), regno = FaDb.S(r["regno"]),
                        type = FaDb.S(r["entry_type"]), typeText = DcFmt.EntryType(FaDb.S(r["entry_type"])), title = FaDb.S(r["title"]), body = body,
                        when = DcFmt.When(r["recorded_at"]), by = FaDb.S(r["recorded_by"]), via = FaDb.S(r["interface"]), visible = FaDb.I(r["student_visible"]) == 1,
                        caseType = FaDb.S(r["type_name"]), statusText = DcFmt.Status(FaDb.S(r["status"]))
                    });
                }
            return FaJson.Ser(new { success = true, rows = rows, total = total });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string CountExport(string report, string configJson, string groupBy)
    {
        return DcApi.Read(DcAccess.Updates, delegate
        {
            int total; using (var c = FaDb.Open()) DcCases.Feed(c, FaJson.Parse(configJson), 1, 1, out total);
            return FaJson.Ser(new { success = true, count = total });
        });
    }
}
