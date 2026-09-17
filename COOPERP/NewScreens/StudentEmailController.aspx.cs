using System;
using System.Web;
using System.Web.Services;
using System.Web.UI;

// eAdmin — Student Email Controller (SEMS). Thin page: all logic is in SemsAdmin.
// Page view is auth-gated by SidebarMaster.master; the WebMethods (which bypass the
// master) carry their own session guard.
public partial class COOPERP_NewScreens_StudentEmailController : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e) { }

    private const string DENIED = "{\"success\":false,\"message\":\"Your session has expired. Please sign in again.\"}";
    private static bool NoAuth()
    {
        return HttpContext.Current == null || HttpContext.Current.Session == null
            || HttpContext.Current.Session["username"] == null;
    }

    [WebMethod(EnableSession = true)]
    public static string Stats() { return NoAuth() ? DENIED : SemsAdmin.Stats(); }

    [WebMethod(EnableSession = true)]
    public static string GenerateEligible() { return NoAuth() ? DENIED : SemsAdmin.GenerateEligible(); }

    [WebMethod(EnableSession = true)]
    public static string Search(string q, string stage, string campus, string programme, string year, string verification, int page, int pageSize)
    { return NoAuth() ? DENIED : SemsAdmin.Search(q, stage, campus, programme, year, verification, page, pageSize); }

    [WebMethod(EnableSession = true)]
    public static string Filters() { return NoAuth() ? DENIED : SemsAdmin.Filters(); }

    [WebMethod(EnableSession = true)]
    public static string CreateEmail(string regno, string email, string tempPw, string notes)
    { return NoAuth() ? DENIED : SemsAdmin.CreateEmail(regno, email, tempPw, notes); }

    [WebMethod(EnableSession = true)]
    public static string Complaints(string status) { return NoAuth() ? DENIED : SemsAdmin.Complaints(status); }

    [WebMethod(EnableSession = true)]
    public static string RespondComplaint(long id, string status, string response)
    { return NoAuth() ? DENIED : SemsAdmin.RespondComplaint(id, status, response); }

    // ── 360 controls ─────────────────────────────────────────────────
    [WebMethod(EnableSession = true)]
    public static string Candidates(string q, string payFilter, int page, int pageSize)
    { return NoAuth() ? DENIED : SemsAdmin.Candidates(q, payFilter, page, pageSize); }

    [WebMethod(EnableSession = true)]
    public static string AddToPipeline(string regno, string note)
    { return NoAuth() ? DENIED : SemsAdmin.AddToPipeline(regno, note); }

    [WebMethod(EnableSession = true)]
    public static string Detail(string regno) { return NoAuth() ? DENIED : SemsAdmin.Detail(regno); }

    [WebMethod(EnableSession = true)]
    public static string SetStatus(string regno, string stage, string note)
    { return NoAuth() ? DENIED : SemsAdmin.SetStatus(regno, stage, note); }

    [WebMethod(EnableSession = true)]
    public static string SetPassword(string regno, string tempPw)
    { return NoAuth() ? DENIED : SemsAdmin.SetPassword(regno, tempPw); }

    [WebMethod(EnableSession = true)]
    public static string DeleteRecord(string regno, string note)
    { return NoAuth() ? DENIED : SemsAdmin.DeleteRecord(regno, note); }

    // ── Batch creation + Google Workspace sync (SemsBatch) ───────────
    // File upload/download for the same feature lives in SemsFile.ashx —
    // a PageMethod can neither receive a file nor stream one.

    /// <summary>Allocates addresses for the selection and parks them in a reviewable draft.</summary>
    [WebMethod(EnableSession = true)]
    public static string BatchPreview(string options)
    { return NoAuth() ? DENIED : SemsBatch.PreviewBatch(options); }

    /// <summary>Overrides one draft row's address, re-checking uniqueness.</summary>
    [WebMethod(EnableSession = true)]
    public static string BatchSetEmail(string batchRef, string regno, string email)
    { return NoAuth() ? DENIED : SemsBatch.UpdateDraftEmail(batchRef, regno, email); }

    /// <summary>Applies the approved draft. Excluded students keep nothing reserved.</summary>
    [WebMethod(EnableSession = true)]
    public static string BatchCommit(string batchRef, string exclude)
    { return NoAuth() ? DENIED : SemsBatch.CommitBatch(batchRef, exclude); }

    [WebMethod(EnableSession = true)]
    public static string BatchCancel(string batchRef)
    { return NoAuth() ? DENIED : SemsBatch.CancelBatch(batchRef); }

    /// <summary>Clears addresses allocated and exported but never confirmed by Google.</summary>
    [WebMethod(EnableSession = true)]
    public static string ReleaseUnconfirmed(string note)
    { return NoAuth() ? DENIED : SemsBatch.ReleaseUnconfirmed(note); }

    [WebMethod(EnableSession = true)]
    public static string BatchList(int limit) { return NoAuth() ? DENIED : SemsBatch.BatchList(limit); }

    [WebMethod(EnableSession = true)]
    public static string BatchDetail(string batchRef, int limit)
    { return NoAuth() ? DENIED : SemsBatch.BatchDetail(batchRef, limit); }

    [WebMethod(EnableSession = true)]
    public static string ExportCount(string scope) { return NoAuth() ? DENIED : SemsBatch.ExportCount(scope); }

    /// <summary>Org unit paths Google has already accepted — the export screen offers these.</summary>
    [WebMethod(EnableSession = true)]
    public static string OrgUnits() { return NoAuth() ? DENIED : SemsBatch.OrgUnits(); }

    [WebMethod(EnableSession = true)]
    public static string ImportRows(string importRef, string action, int page, int pageSize)
    { return NoAuth() ? DENIED : SemsBatch.ImportRows(importRef, action, page, pageSize); }

    [WebMethod(EnableSession = true)]
    public static string ImportApply(string importRef, string options)
    { return NoAuth() ? DENIED : SemsBatch.ImportApply(importRef, options); }

    [WebMethod(EnableSession = true)]
    public static string ImportDiscard(string importRef)
    { return NoAuth() ? DENIED : SemsBatch.ImportDiscard(importRef); }

    /// <summary>Directory health: totals by source, plus addresses issued twice.</summary>
    [WebMethod(EnableSession = true)]
    public static string DirectoryStats() { return NoAuth() ? DENIED : SemsBatch.DirectoryStats(); }

    /// <summary>Live "is this address free?" for the create modal and draft edits.</summary>
    [WebMethod(EnableSession = true)]
    public static string CheckAddress(string email, string regno)
    { return NoAuth() ? DENIED : SemsBatch.CheckAddress(email, regno); }

    /// <summary>House-format suggestion for one student, collision-checked.</summary>
    [WebMethod(EnableSession = true)]
    public static string SuggestFor(string regno, int otherLen)
    { return NoAuth() ? DENIED : SemsBatch.SuggestFor(regno, otherLen); }
}
