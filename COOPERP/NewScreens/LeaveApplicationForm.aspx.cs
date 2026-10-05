using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

public partial class COOPERP_NewScreens_LeaveApplicationForm : System.Web.UI.Page
{
    private static readonly HashSet<string> HrRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "hr_manager", "admin" };
    private static readonly HashSet<string> VcRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "vc", "admin" };
    private static readonly HashSet<string> HodRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "dean", "registrar", "hr_manager", "admin" };

    // ══════════════════════════════════════════════════════════════════════════
    //  PAGE LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════

    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, "hr.leave_applications");

        string ajax = (Request.QueryString["ajax"] ?? "").Trim();
        if (!string.IsNullOrEmpty(ajax))
        {
            Response.ContentType = "application/json";
            Response.Cache.SetNoStore();
            HandleAjax(ajax);
            Response.End();
            return;
        }

        if (!IsPostBack)
            LoadForm();
    }

    private void LoadForm()
    {
        string roleCode   = RoleAccessService.GetRoleCode();
        string username   = Session["username"]   as string ?? "";
        string screenName = Session["ScreenName"] as string ?? username;
        bool   isAdmin    = RoleAccessService.IsAdmin();
        bool   isHr       = HrRoles.Contains(roleCode);
        bool   isVc       = VcRoles.Contains(roleCode);
        bool   isHod      = HodRoles.Contains(roleCode);

        int appId = 0;
        int.TryParse(Request.QueryString["id"] ?? "0", out appId);
        bool isPrint = (Request.QueryString["print"] == "1");

        if (appId <= 0)
            RenderNewForm(username, screenName, isAdmin, isHr, isVc, isHod);
        else
            RenderExistingForm(appId, username, screenName, roleCode, isAdmin, isHr, isVc, isHod, isPrint);

        if (isPrint)
            Page.ClientScript.RegisterStartupScript(GetType(), "autoPrint",
                "window.onload = function(){ window.print(); };", true);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  NEW APPLICATION
    // ══════════════════════════════════════════════════════════════════════════

    private void RenderNewForm(string username, string screenName,
        bool isAdmin, bool isHr, bool isVc, bool isHod)
    {
        litPageTitle.Text   = "New Leave Application";
        litStatusBadge.Text = StatusBadge("DRAFT");
        litHeaderSub.Text   = "Fill in Section 1 and submit it to your Supervisor / HOD.";
        litAppId.Text       = "0";
        litPrintBtn.Text    = "";
        litLetterRef.Text   = "New application";
        litTimeline.Text    = BuildTimeline("DRAFT", "", "", "");

        StaffProfile prof   = LoadStaffProfile(username);
        if (string.IsNullOrEmpty(prof.Name)) prof.Name = screenName;
        litSection1.Text    = BuildSection1Editable(null, prof, username);
        litSection2.Text    = SectionLocked("2", "Section 2 — Head of Department Approval",
                                  "Waiting for employee to submit the application.");
        litSection3.Text    = SectionLocked("3", "Section 3 — Human Resources Department",
                                  "Requires HOD approval before HR can act.");
        litSection4.Text    = SectionLocked("4", "Section 4 — Vice Chancellor's Office",
                                  "Requires HR approval before VC can act.");
        litAuditTrail.Text  = "";

        var ab = new StringBuilder();
        ab.Append(DraftButtons());
        ab.Append("<span class=\"lf-actions__note\">Save a draft to finish later. Submitting sends it to the Supervisor / HOD you chose.</span>");
        litActionBar.Text = ab.ToString();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  EXISTING APPLICATION
    // ══════════════════════════════════════════════════════════════════════════

    private void RenderExistingForm(int appId, string username, string screenName,
        string roleCode, bool isAdmin, bool isHr, bool isVc, bool isHod, bool isPrint)
    {
        Dictionary<string, object> app = LoadApp(appId);

        if (app == null)
        {
            litSection1.Text = "<div style=\"color:#dc2626;padding:20px;\">Application not found or has been cancelled.</div>";
            litActionBar.Text = "";
            return;
        }

        string status    = S(app, "status");
        string empName   = S(app, "emp_name");
        string createdBy = S(app, "created_by");
        string supUser   = S(app, "supervisor_username");

        // Only the assigned Supervisor / HOD (or an admin) can act - the same rule
        // the hod_approve / hod_decline handlers enforce.
        bool isAssignedHod = !string.IsNullOrEmpty(supUser) &&
                             string.Equals(supUser.Trim(), username.Trim(), StringComparison.OrdinalIgnoreCase);

        // Access check: employees see only their own; the assigned supervisor sees theirs
        if (!isAdmin && !isHr && !isVc && !isHod && !isAssignedHod &&
            !string.Equals(createdBy, username, StringComparison.OrdinalIgnoreCase))
        {
            litSection1.Text  = "<div style=\"color:#dc2626;padding:20px;\">You do not have permission to view this application.</div>";
            litActionBar.Text = "";
            return;
        }

        litPageTitle.Text   = "Leave Application — " + HttpUtility.HtmlEncode(empName);
        litStatusBadge.Text = StatusBadge(status);
        litHeaderSub.Text   = "Application #" + appId + " &nbsp;·&nbsp; " +
                              HttpUtility.HtmlEncode(LeaveTypeLabel(S(app, "leave_type"))) +
                              " &nbsp;·&nbsp; " + FormatDate(app, "leave_from") +
                              " to " + FormatDate(app, "leave_to");
        litAppId.Text       = appId.ToString();
        string submittedOn  = FormatDate(app, "employee_submitted_at");
        litLetterRef.Text   = "Application #" + appId +
                              (string.IsNullOrEmpty(submittedOn) ? "" : " &nbsp;&middot;&nbsp; Submitted " + HttpUtility.HtmlEncode(submittedOn));
        litPrintBtn.Text    =
            "<button type=\"button\" class=\"lf-btn lf-btn--outline\" onclick=\"printForm()\">" +
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"11\" height=\"11\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><polyline points=\"6 9 6 2 18 2 18 9\"/><path d=\"M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2\"/><rect x=\"6\" y=\"14\" width=\"12\" height=\"8\"/></svg>" +
            " Print</button>";

        litTimeline.Text = BuildTimeline(status, S(app, "hod_actor_name"), S(app, "hr_actor_name"), S(app, "vc_decision"));

        // ── Section 1 ──────────────────────────────────────────────────────────
        bool canEditDraft = (status == "DRAFT") &&
            (isAdmin || string.Equals(createdBy, username, StringComparison.OrdinalIgnoreCase));
        if (canEditDraft)
        {
            // Fill any blank employee details from the creator's staff record.
            StaffProfile prof = LoadStaffProfile(createdBy);
            litSection1.Text = BuildSection1Editable(app, prof, username);
        }
        else
            litSection1.Text = BuildSection1Readonly(app);

        // ── Section 2 ──────────────────────────────────────────────────────────
        bool canActHod = (status == "SUBMITTED") && (isAdmin || isAssignedHod);

        if (status == "DRAFT")
            litSection2.Text = SectionLocked("2", "Section 2 — Head of Department Approval",
                "Waiting for employee to submit the application.");
        else if (status == "SUBMITTED" && canActHod)
            litSection2.Text = BuildSection2Active(app);
        else if (status == "SUBMITTED")
            litSection2.Text = SectionLocked("2", "Section 2 — Head of Department Approval",
                "Application submitted — awaiting HOD / Supervisor action.");
        else
            litSection2.Text = BuildSection2Readonly(app);

        // ── Section 3 ──────────────────────────────────────────────────────────
        bool canActHr = (status == "HOD_APPROVED") && (isAdmin || isHr);

        if (status == "DRAFT" || status == "SUBMITTED" || status == "HOD_DECLINED")
            litSection3.Text = SectionLocked("3", "Section 3 — Human Resources Department",
                status == "HOD_DECLINED" ? "HOD declined — HR review not required." : "Requires HOD approval before HR can act.");
        else if (canActHr)
            litSection3.Text = BuildSection3Active(app);
        else if (status == "HOD_APPROVED")
            litSection3.Text = SectionLocked("3", "Section 3 — Human Resources Department",
                "HOD approved — awaiting HR Department action.");
        else
            litSection3.Text = BuildSection3Readonly(app);

        // ── Section 4 ──────────────────────────────────────────────────────────
        bool canActVc = (status == "HR_APPROVED") && (isAdmin || isVc);
        bool vcDone   = (status == "VC_GRANTED" || status == "VC_NOT_GRANTED" || status == "VC_POSTPONED");

        if (status == "DRAFT" || status == "SUBMITTED" || status == "HOD_DECLINED" ||
            status == "HOD_APPROVED" || status == "HR_DECLINED")
            litSection4.Text = SectionLocked("4", "Section 4 — Vice Chancellor's Office",
                "Requires HR approval before VC can act.");
        else if (canActVc)
            litSection4.Text = BuildSection4Active(app);
        else if (status == "HR_APPROVED")
            litSection4.Text = SectionLocked("4", "Section 4 — Vice Chancellor's Office",
                "HR approved — awaiting Vice Chancellor's decision.");
        else if (vcDone)
            litSection4.Text = BuildSection4Readonly(app);
        else
            litSection4.Text = SectionLocked("4", "Section 4 — Vice Chancellor's Office", "");

        litAuditTrail.Text = BuildAuditTrail(appId);
        litActionBar.Text  = BuildActionBar(app, appId, status, username,
            isAdmin, isHr, isVc, isHod, canEditDraft, canActHod, canActHr, canActVc) +
            AppJsData(app);
    }

    // Values the approver modals pre-fill from (dates, days, cover arrangement).
    private static string AppJsData(Dictionary<string, object> app)
    {
        int nd = N(app, "num_days");
        string summary =
            "<strong>" + HttpUtility.HtmlEncode(S(app, "emp_name")) + "</strong> &middot; " +
            HttpUtility.HtmlEncode(LeaveTypeLabel(S(app, "leave_type"))) + "<br/>" +
            HttpUtility.HtmlEncode(LongDate(app, "leave_from")) + " to " +
            HttpUtility.HtmlEncode(LongDate(app, "leave_to")) +
            (nd > 0 ? " &middot; " + nd + (nd == 1 ? " day" : " days") : "");
        return "<script>window.LF_APP={" +
            "from:" + JsStr(DateVal(app, "leave_from")) + "," +
            "to:"   + JsStr(DateVal(app, "leave_to"))   + "," +
            "days:" + nd + "," +
            "cover:"   + JsStr(S(app, "substitute_arrangement")) + "," +
            "summary:" + JsStr(summary) + "};</script>";
    }

    private static string DraftButtons()
    {
        return
            "<button type=\"button\" class=\"lf-btn lf-btn--outline\" id=\"btnSaveDraft\" onclick=\"saveDraft(false)\">" +
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"12\" height=\"12\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z\"/><polyline points=\"17 21 17 13 7 13 7 21\"/><polyline points=\"7 3 7 8 15 8\"/></svg>" +
            " Save Draft</button>" +
            "<button type=\"button\" class=\"lf-btn lf-btn--primary\" id=\"btnSubmitApp\" onclick=\"saveDraft(true)\">" +
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"12\" height=\"12\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><line x1=\"22\" y1=\"2\" x2=\"11\" y2=\"13\"/><polygon points=\"22 2 15 22 11 13 2 9 22 2\"/></svg>" +
            " Submit Application</button>";
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  TIMELINE
    // ══════════════════════════════════════════════════════════════════════════

    private static string BuildTimeline(string status, string hodActor, string hrActor, string vcDecision)
    {
        // Step states
        bool isNew         = (status == "DRAFT");
        bool hodDeclined   = (status == "HOD_DECLINED");
        bool hrDeclined    = (status == "HR_DECLINED");
        bool vcNotGood     = (status == "VC_NOT_GRANTED" || status == "VC_POSTPONED");
        bool vcGranted     = (status == "VC_GRANTED");

        bool submitted     = !isNew;
        bool hodActed      = submitted && status != "SUBMITTED";
        bool hodOk         = hodActed && !hodDeclined;
        bool hrActed       = hodOk && status != "HOD_APPROVED";
        bool hrOk          = hrActed && !hrDeclined;
        bool vcActed       = hrOk && status != "HR_APPROVED";

        string step1Cls = isNew      ? "active" : "done";
        string step1Sub = isNew      ? "Filling form" : "Submitted";

        string step2Cls = !submitted ? "" : (hodDeclined ? "declined" : (hodActed ? "done" : "active"));
        string step2Sub = !submitted ? "Awaiting submission"
                        : hodDeclined ? "Declined by HOD"
                        : hodActed   ? "Approved" + (string.IsNullOrEmpty(hodActor) ? "" : " by " + hodActor)
                        : "Awaiting HOD action";

        string step3Cls = !hodOk ? "" : (hrDeclined ? "declined" : (hrActed ? "done" : "active"));
        string step3Sub = !hodOk ? "Pending HOD approval"
                        : hrDeclined ? "Declined by HR"
                        : hrActed   ? "Approved" + (string.IsNullOrEmpty(hrActor) ? "" : " by " + hrActor)
                        : "Awaiting HR Department";

        string step4Cls = !hrOk ? "" : (vcNotGood ? "declined" : (vcActed ? "done" : "active"));
        string step4Sub = !hrOk ? "Pending HR approval"
                        : vcGranted ? "Leave Granted"
                        : vcNotGood ? (status == "VC_POSTPONED" ? "Postponed" : "Not Granted")
                        : vcActed   ? "Decision issued"
                        : "Awaiting VC decision";

        var sb = new StringBuilder();
        sb.Append("<div class=\"lf-timeline\">");
        sb.Append(TlStep("1", "Employee", step1Sub, step1Cls));
        sb.Append(TlArrow(step1Cls == "done"));
        sb.Append(TlStep("2", "HOD Approval", step2Sub, step2Cls));
        sb.Append(TlArrow(step2Cls == "done"));
        sb.Append(TlStep("3", "HR Department", step3Sub, step3Cls));
        sb.Append(TlArrow(step3Cls == "done"));
        sb.Append(TlStep("4", "Vice Chancellor", step4Sub, step4Cls));
        sb.Append("</div>");
        return sb.ToString();
    }

    private static string TlStep(string num, string label, string sub, string cls)
    {
        string dotCls = cls == "done" ? "lf-step__dot--done"
                      : cls == "active" ? "lf-step__dot--active"
                      : cls == "declined" ? "lf-step__dot--declined" : "";
        string icon   = cls == "done" ? "&#10003;" : cls == "declined" ? "&#10007;" : num;
        return string.Format(
            "<div class=\"lf-step\">" +
            "<div class=\"lf-step__dot {0}\">{1}</div>" +
            "<div class=\"lf-step__info\">" +
            "<div class=\"lf-step__label\">{2}</div>" +
            "<div class=\"lf-step__sub\">{3}</div>" +
            "</div></div>",
            dotCls, icon,
            HttpUtility.HtmlEncode(label),
            HttpUtility.HtmlEncode(sub));
    }
    private static string TlArrow(bool done)
    {
        return "<div class=\"lf-step__arrow" + (done ? " lf-step__arrow--done" : "") + "\"></div>";
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SECTION 1 — EMPLOYEE DETAILS
    // ══════════════════════════════════════════════════════════════════════════

    private const string ReqMark = "<span class=\"lf-req\" title=\"Required\">*</span>";

    private static readonly string[][] LeaveTypes =
    {
        new[] { "annual",      "Annual leave",       "Your yearly paid leave entitlement." },
        new[] { "study",       "Study leave",        "Time off for approved courses or examinations." },
        new[] { "sick",        "Sick leave",         "Illness, injury or medical treatment." },
        new[] { "maternity",   "Maternity leave",    "Before and after the birth of your child." },
        new[] { "bereavement", "Family bereavement", "Death or burial of a close family member." }
    };

    private string BuildSection1Editable(Dictionary<string, object> app, StaffProfile prof, string currentUser)
    {
        if (prof == null) prof = new StaffProfile();

        // Saved draft values win; blanks fall back to the staff record.
        string empName  = Pick(app, "emp_name",          prof.Name);
        string empCode  = Pick(app, "emp_code",          prof.Code);
        string dept     = Pick(app, "faculty_dept",      prof.Dept);
        string position = Pick(app, "position_held",     prof.Position);
        string office   = Pick(app, "office_location",   "");
        string mobile   = Pick(app, "mobile_contact",    prof.Mobile);
        string address  = Pick(app, "residence_address", prof.Residence);
        string nokName  = Pick(app, "nok_name",          prof.NokName);
        string nokMob   = Pick(app, "nok_mobile",        prof.NokPhone);
        string nokAddr  = Pick(app, "nok_address",       "");
        string lvType   = app != null ? S(app, "leave_type") : "";
        string lvFrom   = app != null ? DateVal(app, "leave_from") : "";
        string lvTo     = app != null ? DateVal(app, "leave_to")   : "";
        string subst    = app != null ? S(app, "substitute_arrangement") : "";

        // Approver: the draft's choice, else the supervisor on the staff record.
        string supUser  = app != null ? S(app, "supervisor_username") : "";
        string supName  = app != null ? S(app, "supervisor_name")     : "";
        bool   supForce = !string.IsNullOrEmpty(supUser);      // keep a saved choice even if no longer listed
        if (string.IsNullOrEmpty(supUser) && !string.IsNullOrEmpty(prof.SupUsername))
        {
            supUser  = prof.SupUsername;
            supName  = prof.SupName;
            supForce = prof.SupCanApprove;
        }
        bool supFound;
        string supOptions = BuildSupervisorOptions(LoadSupervisors(), supUser, supName, supForce, currentUser, out supFound);

        bool fromRecord = prof.EmpId > 0;

        var sb = new StringBuilder();
        sb.Append("<div class=\"lf-section lf-section--active\" id=\"sec1\">");
        sb.Append("<div class=\"lf-section__head\">");
        sb.Append("<div class=\"lf-section__num\">1</div>");
        sb.Append("<div class=\"lf-section__title\">Section 1 &mdash; Leave Request</div>");
        sb.Append("<span class=\"lf-section__badge badge--waiting\">Draft</span>");
        sb.Append("</div><div class=\"lf-section__body lf-form\" id=\"sec1Form\">");
        sb.Append("<p class=\"lf-intro\">Fields marked " + ReqMark + " are needed to submit. You can save a draft at any time and finish later.</p>");

        // ── (a) Leave requested ─────────────────────────────────────────────
        sb.Append(GroupHead(IconCalendar, "Leave requested", ""));
        sb.Append("<div class=\"lf-leave-grid\">");

        sb.Append("<div class=\"lf-field\" id=\"fld_leaveType\">");
        sb.Append("<span class=\"lf-label\" id=\"lblLeaveType\">Type of leave" + ReqMark + "</span>");
        sb.Append("<div class=\"lf-radios\" role=\"radiogroup\" aria-labelledby=\"lblLeaveType\">");
        bool known = false;
        foreach (string[] lt in LeaveTypes)
        {
            bool chk = string.Equals(lt[0], lvType, StringComparison.OrdinalIgnoreCase);
            if (chk) known = true;
            sb.Append(LvTypeRadio(lt[0], lt[1], lt[2], chk));
        }
        if (!known && !string.IsNullOrEmpty(lvType))   // legacy value saved on an old draft
            sb.Append(LvTypeRadio(lvType, LeaveTypeLabel(lvType), "Saved on this draft.", true));
        sb.Append("</div><div class=\"lf-err\" role=\"alert\"></div></div>");

        sb.Append("<div style=\"min-width:0;\">");
        sb.Append("<div class=\"lf-grid\">");
        sb.Append(Fld("leaveFrom", "First day", true,
            InputHtml("leaveFrom", "date", lvFrom, ""), ""));
        sb.Append(Fld("leaveTo", "Last day", true,
            InputHtml("leaveTo", "date", lvTo, ""), ""));
        sb.Append("</div>");
        sb.Append("<div class=\"lf-days\" id=\"leaveSummary\" aria-live=\"polite\"></div>");
        sb.Append("<div style=\"margin-top:12px;\">");
        sb.Append(Fld("substituteArrangement", "Who will cover your duties?", true,
            TextareaHtml("substituteArrangement", subst,
                "Name and title of the colleague, and what they will handle. e.g. Ms. Jane Nakato, Assistant Lecturer, will take my classes."),
            ""));
        sb.Append("</div></div>");
        sb.Append("</div>");   // lf-leave-grid
        sb.Append("</div>");   // group (opened by GroupHead)

        // ── (b) Approver ─────────────────────────────────────────────────────
        sb.Append(GroupHead(IconUserCheck, "Approver", ""));
        string supHint = "Your request goes to this person first, then to Human Resources and the Vice Chancellor. Type a name to jump to it.";
        if (app == null && supFound && !string.IsNullOrEmpty(prof.SupUsername))
            supHint = "Pre-selected from your staff record. " + supHint;
        else if (app == null && !string.IsNullOrEmpty(prof.SupName) && !supFound)
            supHint = "Your staff record lists " + HttpUtility.HtmlEncode(prof.SupName) +
                      " as your supervisor, but they cannot approve leave online yet. Choose your Head of Department from the list. " + supHint;
        sb.Append("<div class=\"lf-grid\"><div class=\"lf-span\" style=\"max-width:520px;\">");
        sb.Append(Fld("supUsername", "Supervisor / Head of Department", true,
            "<select id=\"supUsername\"><option value=\"\">Select your Supervisor / HOD</option>" + supOptions + "</select>",
            supHint));
        sb.Append("</div></div>");
        sb.Append("</div>");

        // ── (c) Your details ────────────────────────────────────────────────
        sb.Append(GroupHead(IconUser, "Your details",
            fromRecord ? "Filled in from your staff record. Correct anything that is out of date." : ""));
        sb.Append("<div class=\"lf-grid lf-grid--3\">");
        sb.Append(Fld("empName", "Full name", true, InputHtml("empName", "text", empName, "As on your staff record"), ""));
        sb.Append(Fld("empCode", "Employee code", false, InputHtml("empCode", "text", empCode, "e.g. MRU0123"), ""));
        sb.Append(Fld("mobileContact", "Mobile number", true, InputHtml("mobileContact", "tel", mobile, "e.g. 0772 123456"), ""));
        sb.Append(Fld("empDept", "Faculty / department / section", true, InputHtml("empDept", "text", dept, "e.g. Faculty of Education"), ""));
        sb.Append(Fld("positionHeld", "Position held", false, InputHtml("positionHeld", "text", position, "e.g. Senior Lecturer"), ""));
        sb.Append(Fld("officeLocation", "Office location", false, InputHtml("officeLocation", "text", office, "Building and room, e.g. Admin Block, Room 4"), ""));
        sb.Append("</div>");
        sb.Append("</div>");

        // ── (d) While you are on leave ──────────────────────────────────────
        sb.Append(GroupHead(IconHome, "While you are on leave", "So the University can reach you or your family if needed."));
        sb.Append("<div class=\"lf-grid\">");
        sb.Append("<div class=\"lf-span\">");
        sb.Append(Fld("residenceAddress", "Where you will stay", false,
            TextareaHtml("residenceAddress", address, "Village / town, district, and a landmark if helpful"), ""));
        sb.Append("</div>");
        sb.Append("</div>");
        sb.Append("<div class=\"lf-grid lf-grid--3\" style=\"margin-top:14px;\">");
        sb.Append(Fld("nokName", "Next of kin", false, InputHtml("nokName", "text", nokName, "Name and relationship, e.g. Sarah Kato (wife)"), ""));
        sb.Append(Fld("nokMobile", "Next of kin phone", false, InputHtml("nokMobile", "tel", nokMob, "e.g. 0701 123456"), ""));
        sb.Append(Fld("nokAddress", "Next of kin address", false, InputHtml("nokAddress", "text", nokAddr, "Village / town, district"), ""));
        sb.Append("</div>");
        sb.Append("</div>");

        sb.Append("</div></div>"); // body + section
        return sb.ToString();
    }

    private static string LvTypeRadio(string value, string label, string desc, bool chk)
    {
        string v = HttpUtility.HtmlAttributeEncode(value);
        return string.Format(
            "<label class=\"lf-radio{3}\" for=\"lt_{0}\">" +
            "<input type=\"radio\" name=\"leaveTypeRadio\" id=\"lt_{0}\" value=\"{0}\"{4} />" +
            "<span class=\"lf-radio__text\"><span class=\"lf-radio__title\">{1}</span>" +
            "<span class=\"lf-radio__desc\">{2}</span></span></label>",
            v, HttpUtility.HtmlEncode(label), HttpUtility.HtmlEncode(desc),
            chk ? " is-checked" : "", chk ? " checked" : "");
    }

    // Opens a group; the caller closes it with "</div>".
    private static string GroupHead(string icon, string title, string note)
    {
        return "<div class=\"lf-group\"><div class=\"lf-group__head\">" +
               "<span class=\"lf-group__icon\">" + icon + "</span>" +
               "<span class=\"lf-group__title\">" + HttpUtility.HtmlEncode(title) + "</span>" +
               (string.IsNullOrEmpty(note) ? "" : "<span class=\"lf-group__note\">" + HttpUtility.HtmlEncode(note) + "</span>") +
               "</div>";
    }

    private const string IconCalendar  = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"13\" height=\"13\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><rect x=\"3\" y=\"4\" width=\"18\" height=\"18\"/><line x1=\"16\" y1=\"2\" x2=\"16\" y2=\"6\"/><line x1=\"8\" y1=\"2\" x2=\"8\" y2=\"6\"/><line x1=\"3\" y1=\"10\" x2=\"21\" y2=\"10\"/></svg>";
    private const string IconUserCheck = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"13\" height=\"13\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2\"/><circle cx=\"8.5\" cy=\"7\" r=\"4\"/><polyline points=\"17 11 19 13 23 9\"/></svg>";
    private const string IconUser      = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"13\" height=\"13\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2\"/><circle cx=\"12\" cy=\"7\" r=\"4\"/></svg>";
    private const string IconHome      = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"13\" height=\"13\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z\"/><polyline points=\"9 22 9 12 15 12 15 22\"/></svg>";

    private static string BuildSection1Readonly(Dictionary<string, object> app)
    {
        string status = S(app, "status");
        string cls    = (status == "CANCELLED") ? "" : "lf-section--completed";
        string badge  = (status == "CANCELLED")
            ? "<span class=\"lf-section__badge badge--locked\">Cancelled</span>"
            : "<span class=\"lf-section__badge badge--approved\">Submitted</span>";

        var sb = new StringBuilder();
        sb.AppendFormat("<div class=\"lf-section {0}\">", cls);
        sb.Append("<div class=\"lf-section__head\">");
        sb.Append("<div class=\"lf-section__num\">1</div>");
        sb.Append("<div class=\"lf-section__title\">Section 1 &mdash; Leave Request</div>");
        sb.Append(badge);
        sb.Append("</div><div class=\"lf-section__body\">");

        int nd = N(app, "num_days");
        sb.Append("<div class=\"lf-ro-group\"><div class=\"lf-ro-title\">Leave requested</div><dl class=\"lf-dl\">");
        sb.Append(Dl("Type of leave", LeaveTypeLabel(S(app, "leave_type")), false));
        sb.Append(Dl("Number of days", nd > 0 ? nd + (nd == 1 ? " day" : " days") : "", false));
        sb.Append(Dl("First day", LongDate(app, "leave_from"), false));
        sb.Append(Dl("Last day",  LongDate(app, "leave_to"),   false));
        sb.Append(Dl("Cover arrangement", S(app, "substitute_arrangement"), true));
        sb.Append("</dl></div>");

        string supDisp = S(app, "supervisor_name");
        if (string.IsNullOrEmpty(supDisp)) supDisp = S(app, "supervisor_username");
        sb.Append("<div class=\"lf-ro-group\"><div class=\"lf-ro-title\">Approver</div><dl class=\"lf-dl\">");
        sb.Append(Dl("Supervisor / HOD", supDisp, true));
        sb.Append("</dl></div>");

        sb.Append("<div class=\"lf-ro-group\"><div class=\"lf-ro-title\">Employee details</div><dl class=\"lf-dl\">");
        sb.Append(Dl("Full name",       S(app, "emp_name"),        false));
        sb.Append(Dl("Employee code",   S(app, "emp_code"),        false));
        sb.Append(Dl("Department",      S(app, "faculty_dept"),    false));
        sb.Append(Dl("Position held",   S(app, "position_held"),   false));
        sb.Append(Dl("Office location", S(app, "office_location"), false));
        sb.Append(Dl("Mobile number",   S(app, "mobile_contact"),  false));
        sb.Append("</dl></div>");

        sb.Append("<div class=\"lf-ro-group\"><div class=\"lf-ro-title\">While on leave</div><dl class=\"lf-dl\">");
        sb.Append(Dl("Staying at",          S(app, "residence_address"), true));
        sb.Append(Dl("Next of kin",         S(app, "nok_name"),          false));
        sb.Append(Dl("Next of kin phone",   S(app, "nok_mobile"),        false));
        sb.Append(Dl("Next of kin address", S(app, "nok_address"),       true));
        sb.Append("</dl></div>");

        string submitted = FormatDateTime(app, "employee_submitted_at");
        if (!string.IsNullOrEmpty(submitted))
            sb.AppendFormat("<p class=\"lf-meta\">Submitted on {0}</p>", HttpUtility.HtmlEncode(submitted));

        // Signature lines appear on the printed form only.
        sb.Append("<div class=\"lf-sign\"><div>Employee&rsquo;s signature and date</div><div>Supervisor / HOD signature and date</div></div>");

        sb.Append("</div></div>");
        return sb.ToString();
    }

    // One dt/dd pair of the read-only definition list; wide pairs take a full row.
    private static string Dl(string label, string value, bool wide)
    {
        bool empty = string.IsNullOrWhiteSpace(value);
        string cls = ((wide ? "lf-dl__wide " : "") + (empty ? "lf-dl__empty" : "")).Trim();
        return string.Format("<dt{0}>{1}</dt><dd{2}>{3}</dd>",
            wide ? " style=\"grid-column:1;\"" : "",
            HttpUtility.HtmlEncode(label),
            cls.Length > 0 ? " class=\"" + cls + "\"" : "",
            empty ? "&mdash;" : HttpUtility.HtmlEncode(value.Trim()));
    }

    // Field wrapper: label + control + optional hint + inline error slot.
    private static string Fld(string id, string label, bool required, string control, string hintHtml)
    {
        return "<div class=\"lf-field\" id=\"fld_" + id + "\">" +
               "<label for=\"" + id + "\">" + HttpUtility.HtmlEncode(label) + (required ? ReqMark : "") + "</label>" +
               control +
               (string.IsNullOrEmpty(hintHtml) ? "" : "<div class=\"lf-hint\">" + hintHtml + "</div>") +
               "<div class=\"lf-err\" role=\"alert\"></div></div>";
    }

    private static string InputHtml(string id, string type, string value, string placeholder)
    {
        return string.Format("<input type=\"{0}\" id=\"{1}\" value=\"{2}\" placeholder=\"{3}\" maxlength=\"250\" />",
            type, id, HttpUtility.HtmlAttributeEncode(value ?? ""), HttpUtility.HtmlAttributeEncode(placeholder ?? ""));
    }

    private static string TextareaHtml(string id, string value, string placeholder)
    {
        return string.Format("<textarea id=\"{0}\" placeholder=\"{1}\" rows=\"2\">{2}</textarea>",
            id, HttpUtility.HtmlAttributeEncode(placeholder ?? ""), HttpUtility.HtmlEncode(value ?? ""));
    }

    private static string Pick(Dictionary<string, object> app, string key, string fallback)
    {
        string v = app != null ? S(app, key).Trim() : "";
        return v.Length > 0 ? v : (fallback ?? "");
    }

    private static string LongDate(Dictionary<string, object> d, string key)
    {
        object v;
        if (!d.TryGetValue(key, out v) || v == null || v is DBNull) return "";
        DateTime dt; if (v is DateTime) dt = (DateTime)v;
        else if (!DateTime.TryParse(v.ToString(), out dt)) return "";
        return dt.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SECTION 2 — HOD
    // ══════════════════════════════════════════════════════════════════════════

    private static string BuildSection2Active(Dictionary<string, object> app)
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"lf-section lf-section--active\">");
        sb.Append("<div class=\"lf-section__head\">");
        sb.Append("<div class=\"lf-section__num\">2</div>");
        sb.Append("<div class=\"lf-section__title\">Section 2 — Head of Department Approval</div>");
        sb.Append("<span class=\"lf-section__badge badge--waiting\">Awaiting Your Action</span>");
        sb.Append("</div><div class=\"lf-section__body\">");
        sb.Append("<p style=\"font-size:12px;color:#374151;margin:0 0 8px;\">Review the employee details above. Approve and forward to HR, or decline.</p>");
        sb.Append("<p style=\"font-size:11px;color:var(--muted);margin:0;\">Use the action buttons in the bar below to approve or decline this application.</p>");
        sb.Append("</div></div>");
        return sb.ToString();
    }

    private static string BuildSection2Readonly(Dictionary<string, object> app)
    {
        bool approved = S(app, "hod_action") == "APPROVED";
        string cls   = approved ? "lf-section--completed" : "lf-section--declined";
        string badge = approved
            ? "<span class=\"lf-section__badge badge--approved\">Approved</span>"
            : "<span class=\"lf-section__badge badge--declined\">Declined</span>";
        string dot   = approved ? "&#10003;" : "&#10007;";

        var sb = new StringBuilder();
        sb.AppendFormat("<div class=\"lf-section {0}\">", cls);
        sb.Append("<div class=\"lf-section__head\">");
        sb.AppendFormat("<div class=\"lf-section__num\">{0}</div>", dot);
        sb.Append("<div class=\"lf-section__title\">Section 2 — Head of Department Approval</div>");
        sb.Append(badge);
        sb.Append("</div><div class=\"lf-section__body\">");
        sb.Append("<div class=\"lf-grid\" style=\"margin-bottom:14px;\">");
        sb.Append(RoField("HOD Decision",  approved ? "Approved" : "Declined"));
        sb.Append(RoField("Actioned By",   S(app, "hod_actor_name")));
        sb.Append("</div>");
        if (approved)
        {
            sb.Append("<div class=\"lf-grid\" style=\"margin-bottom:14px;\">");
            sb.Append(RoField("Handover To", S(app, "hod_handover_to")));
            sb.Append(RoField("Date",        FormatDate(app, "hod_action_at")));
            sb.Append("</div>");
        }
        else
        {
            sb.Append("<div class=\"lf-grid\" style=\"margin-bottom:14px;\">");
            sb.Append(RoField("Date",   FormatDate(app, "hod_action_at")));
            sb.Append(RoField("Reason", S(app, "hod_notes")));
            sb.Append("</div>");
        }
        string notes = S(app, "hod_notes");
        if (approved && !string.IsNullOrEmpty(notes))
            sb.Append(RoField("Notes / Remarks", notes));
        sb.Append("</div></div>");
        return sb.ToString();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SECTION 3 — HR
    // ══════════════════════════════════════════════════════════════════════════

    private static string BuildSection3Active(Dictionary<string, object> app)
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"lf-section lf-section--active\">");
        sb.Append("<div class=\"lf-section__head\">");
        sb.Append("<div class=\"lf-section__num\">3</div>");
        sb.Append("<div class=\"lf-section__title\">Section 3 — Human Resources Department</div>");
        sb.Append("<span class=\"lf-section__badge badge--waiting\">Awaiting HR Action</span>");
        sb.Append("</div><div class=\"lf-section__body\">");
        sb.Append("<div class=\"lf-grid\" style=\"margin-bottom:14px;\">");
        sb.Append(RoField("HOD Handover To",  S(app, "hod_handover_to")));
        sb.Append(RoField("HOD Approved By",  S(app, "hod_actor_name")));
        sb.Append("</div>");
        sb.Append("<p style=\"font-size:11px;color:var(--muted);margin:0;\">Use the Approve or Decline buttons in the action bar to process this application.</p>");
        sb.Append("</div></div>");
        return sb.ToString();
    }

    private static string BuildSection3Readonly(Dictionary<string, object> app)
    {
        bool approved = S(app, "hr_action") == "APPROVED";
        string cls   = approved ? "lf-section--completed" : "lf-section--declined";
        string badge = approved
            ? "<span class=\"lf-section__badge badge--approved\">Approved</span>"
            : "<span class=\"lf-section__badge badge--declined\">Declined</span>";

        var sb = new StringBuilder();
        sb.AppendFormat("<div class=\"lf-section {0}\">", cls);
        sb.Append("<div class=\"lf-section__head\">");
        sb.AppendFormat("<div class=\"lf-section__num\">{0}</div>", approved ? "&#10003;" : "&#10007;");
        sb.Append("<div class=\"lf-section__title\">Section 3 — Human Resources Department</div>");
        sb.Append(badge);
        sb.Append("</div><div class=\"lf-section__body\">");
        sb.Append("<div class=\"lf-grid\" style=\"margin-bottom:14px;\">");
        sb.Append(RoField("HR Decision",   approved ? "Approved" : "Declined"));
        sb.Append(RoField("Actioned By",   S(app, "hr_actor_name")));
        sb.Append("</div>");
        if (approved)
        {
            sb.Append("<div class=\"lf-grid\" style=\"margin-bottom:14px;\">");
            sb.Append(RoField("Effective From", FormatDate(app, "hr_effective_from")));
            sb.Append(RoField("Effective To",   FormatDate(app, "hr_effective_to")));
            sb.Append("</div>");
        }
        sb.Append("<div class=\"lf-grid\" style=\"margin-bottom:14px;\">");
        sb.Append(RoField("Date Actioned", FormatDate(app, "hr_action_at")));
        string hrNotes = S(app, "hr_notes");
        if (!string.IsNullOrEmpty(hrNotes)) sb.Append(RoField("HR Notes", hrNotes));
        sb.Append("</div>");
        sb.Append("</div></div>");
        return sb.ToString();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SECTION 4 — VC
    // ══════════════════════════════════════════════════════════════════════════

    private static string BuildSection4Active(Dictionary<string, object> app)
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"lf-section lf-section--active\">");
        sb.Append("<div class=\"lf-section__head\">");
        sb.Append("<div class=\"lf-section__num\">4</div>");
        sb.Append("<div class=\"lf-section__title\">Section 4 — Vice Chancellor's Office</div>");
        sb.Append("<span class=\"lf-section__badge badge--waiting\">Awaiting VC Decision</span>");
        sb.Append("</div><div class=\"lf-section__body\">");
        sb.Append("<div class=\"lf-grid\" style=\"margin-bottom:14px;\">");
        sb.Append(RoField("HR Effective From", FormatDate(app, "hr_effective_from")));
        sb.Append(RoField("HR Effective To",   FormatDate(app, "hr_effective_to")));
        sb.Append("</div>");
        sb.Append("<p style=\"font-size:11px;color:var(--muted);margin:0;\">Use <strong>Record VC Decision</strong> in the action bar to grant, not grant or postpone this leave.</p>");
        sb.Append("</div></div>");
        return sb.ToString();
    }

    private static string BuildSection4Readonly(Dictionary<string, object> app)
    {
        string vcDec  = S(app, "vc_decision");
        bool granted  = (vcDec == "GRANTED");
        bool postponed = (vcDec == "POSTPONED");
        string cls    = granted ? "lf-section--completed" : "lf-section--declined";
        string badgeLbl = granted ? "Granted" : (postponed ? "Postponed" : "Not Granted");
        string badgeCls = granted ? "badge--granted" : (postponed ? "badge--postponed" : "badge--notgranted");

        var sb = new StringBuilder();
        sb.AppendFormat("<div class=\"lf-section {0}\">", cls);
        sb.Append("<div class=\"lf-section__head\">");
        sb.AppendFormat("<div class=\"lf-section__num\">{0}</div>", granted ? "&#10003;" : "&#10007;");
        sb.Append("<div class=\"lf-section__title\">Section 4 — Vice Chancellor's Office</div>");
        sb.AppendFormat("<span class=\"lf-section__badge {0}\">{1}</span>", badgeCls, badgeLbl);
        sb.Append("</div><div class=\"lf-section__body\">");
        sb.Append("<div class=\"lf-grid\" style=\"margin-bottom:14px;\">");
        sb.Append(RoField("VC Decision",  badgeLbl));
        sb.Append(RoField("Actioned By",  S(app, "vc_actor_name")));
        sb.Append("</div>");
        sb.Append("<div class=\"lf-grid\" style=\"margin-bottom:14px;\">");
        int accum = N(app, "vc_accumulated_days");
        int taken = N(app, "vc_days_taken");
        sb.Append(RoField("Accumulated Leave Days",    accum > 0 ? accum.ToString() : "—"));
        sb.Append(RoField("Days Granted (This Leave)", taken > 0 ? taken.ToString() : "—"));
        sb.Append("</div>");
        string vcReason = S(app, "vc_reason");
        if (!string.IsNullOrEmpty(vcReason)) sb.Append(RoField("Remarks", vcReason));
        sb.Append(RoField("Date", FormatDate(app, "vc_action_at")));
        sb.Append("</div></div>");
        return sb.ToString();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  ACTION BAR
    // ══════════════════════════════════════════════════════════════════════════

    private static string BuildActionBar(Dictionary<string, object> app, int appId, string status,
        string username, bool isAdmin, bool isHr, bool isVc, bool isHod,
        bool canEditDraft, bool canActHod, bool canActHr, bool canActVc)
    {
        var sb = new StringBuilder();

        if (canEditDraft)
        {
            sb.Append(DraftButtons());
        }
        if (canActHod)
        {
            sb.Append("<button type=\"button\" class=\"lf-btn lf-btn--success\" onclick=\"openHodApprove()\">Approve &amp; Forward to HR</button>");
            sb.Append("<button type=\"button\" class=\"lf-btn lf-btn--danger\" onclick=\"openModal('modalHodDecline')\">Decline</button>");
        }
        if (canActHr)
        {
            sb.Append("<button type=\"button\" class=\"lf-btn lf-btn--success\" onclick=\"openHrApprove()\">Approve &amp; Forward to VC</button>");
            sb.Append("<button type=\"button\" class=\"lf-btn lf-btn--danger\" onclick=\"openModal('modalHrDecline')\">Decline</button>");
        }
        if (canActVc)
        {
            sb.Append("<button type=\"button\" class=\"lf-btn lf-btn--primary\" onclick=\"openVcDecision('')\">" +
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"12\" height=\"12\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><polyline points=\"9 11 12 14 22 4\"/><path d=\"M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11\"/></svg>" +
                " Record VC Decision</button>");
        }

        // Cancel for admin/HR
        if ((isAdmin || isHr) && status != "CANCELLED" && status != "VC_GRANTED")
        {
            if (sb.Length > 0) sb.Append("<span style=\"flex:1\"></span>");
            sb.Append("<button type=\"button\" class=\"lf-btn lf-btn--danger\" onclick=\"cancelAppForm()\">" +
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"11\" height=\"11\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><circle cx=\"12\" cy=\"12\" r=\"10\"/><line x1=\"15\" y1=\"9\" x2=\"9\" y2=\"15\"/><line x1=\"9\" y1=\"9\" x2=\"15\" y2=\"15\"/></svg>" +
                " Cancel Application</button>");
        }

        if (!canEditDraft && !canActHod && !canActHr && !canActVc)
        {
            sb.AppendFormat("<span class=\"lf-actions__note\" style=\"color:var(--txt);font-weight:600;\">{0}</span>",
                HttpUtility.HtmlEncode(StatusNote(status)));
        }

        // cancelAppForm JS (uses APP_ID from the page)
        sb.Append("<script>function cancelAppForm(){" +
            "if(!confirm('Cancel this leave application? This cannot be undone.'))return;" +
            "fetch('LeaveApplicationForm.aspx?ajax=cancel',{method:'POST'," +
            "headers:{'Content-Type':'application/x-www-form-urlencoded'}," +
            "body:'id='+encodeURIComponent(APP_ID)})" +
            ".then(function(r){return r.json();})" +
            ".then(function(d){if(d.ok){showToast('Application cancelled.','ok');" +
            "setTimeout(function(){location.href='LeaveApplications.aspx';},1300);}else showToast(d.error||'Failed.','err');});" +
            "}</script>");

        return sb.ToString();
    }

    private static string StatusNote(string status)
    {
        switch (status)
        {
            case "SUBMITTED":      return "Application submitted — awaiting HOD approval.";
            case "HOD_APPROVED":   return "HOD approved — awaiting HR Department review.";
            case "HOD_DECLINED":   return "Application was declined by the Head of Department.";
            case "HR_APPROVED":    return "HR approved — awaiting Vice Chancellor's decision.";
            case "HR_DECLINED":    return "Application was declined by the HR Department.";
            case "VC_GRANTED":     return "Leave granted by the Vice Chancellor.";
            case "VC_NOT_GRANTED": return "Leave was not granted by the Vice Chancellor.";
            case "VC_POSTPONED":   return "Leave has been postponed by the Vice Chancellor.";
            case "CANCELLED":      return "This application has been cancelled.";
            default:               return "Application is in " + status + " status.";
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  AUDIT TRAIL
    // ══════════════════════════════════════════════════════════════════════════

    private string BuildAuditTrail(int appId)
    {
        var sb = new StringBuilder();
        try
        {
            using (var conn = new MySqlConnection(ConnStr()))
            {
                conn.Open();
                const string sql = @"
                    SELECT action_code, old_status, new_status,
                           actor_name, actor_role, remarks, created_at
                    FROM hrm_leave_audit WHERE application_id=@id ORDER BY created_at ASC";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", appId);
                    using (var dr = cmd.ExecuteReader())
                    {
                        bool any = false;
                        while (dr.Read())
                        {
                            if (!any)
                            {
                                sb.Append("<div class=\"lf-audit__title\">Audit Trail</div>");
                                any = true;
                            }
                            string code    = dr["action_code"].ToString();
                            string actor   = dr["actor_name"].ToString();
                            string role    = dr["actor_role"].ToString();
                            string remarks = dr["remarks"] == DBNull.Value ? "" : dr["remarks"].ToString();
                            DateTime when  = Convert.ToDateTime(dr["created_at"]);

                            string dotCls = "audit-dot";
                            if (code.Contains("APPROVED") || code == "VC_GRANTED") dotCls += " audit-dot--ok";
                            else if (code.Contains("DECLINED") || code == "VC_NOT_GRANTED" || code == "CANCELLED") dotCls += " audit-dot--err";
                            else if (code == "SUBMITTED") dotCls += " audit-dot--warn";

                            sb.Append("<div class=\"audit-entry\">");
                            sb.AppendFormat("<div class=\"{0}\"></div>", dotCls);
                            sb.Append("<div class=\"audit-body\">");
                            sb.AppendFormat("<div class=\"audit-action\">{0}</div>",
                                HttpUtility.HtmlEncode(AuditLabel(code)));
                            sb.AppendFormat("<div class=\"audit-meta\">{0} — {1} &nbsp;·&nbsp; {2}</div>",
                                HttpUtility.HtmlEncode(actor),
                                HttpUtility.HtmlEncode(role),
                                HttpUtility.HtmlEncode(when.ToString("dd MMM yyyy, h:mm tt")));
                            if (!string.IsNullOrEmpty(remarks))
                                sb.AppendFormat("<div class=\"audit-remark\">&ldquo;{0}&rdquo;</div>",
                                    HttpUtility.HtmlEncode(remarks));
                            sb.Append("</div></div>");
                        }
                    }
                }
            }
        }
        catch { /* don't break page on audit errors */ }
        return sb.ToString();
    }

    private static string AuditLabel(string code)
    {
        switch (code)
        {
            case "DRAFT_SAVED":    return "Draft saved";
            case "SUBMITTED":      return "Application submitted";
            case "HOD_APPROVED":   return "Approved by Head of Department";
            case "HOD_DECLINED":   return "Declined by Head of Department";
            case "HR_APPROVED":    return "Approved by HR Department";
            case "HR_DECLINED":    return "Declined by HR Department";
            case "VC_GRANTED":     return "Leave Granted by Vice Chancellor";
            case "VC_NOT_GRANTED": return "Not Granted by Vice Chancellor";
            case "VC_POSTPONED":   return "Postponed by Vice Chancellor";
            case "CANCELLED":      return "Application Cancelled";
            default:               return code;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  AJAX ROUTER
    // ══════════════════════════════════════════════════════════════════════════

    private void HandleAjax(string action)
    {
        string username   = Session["username"]   as string ?? "";
        string screenName = Session["ScreenName"] as string ?? username;
        string roleCode   = RoleAccessService.GetRoleCode();
        string ip         = Request.UserHostAddress;
        bool   isAdmin    = RoleAccessService.IsAdmin();
        bool   isHr       = HrRoles.Contains(roleCode);
        bool   isVc       = VcRoles.Contains(roleCode);
        bool   isHod      = HodRoles.Contains(roleCode);

        try
        {
            switch (action)
            {
                case "save_draft":  AjaxSaveDraft(username, screenName, ip, false, isAdmin); break;
                case "submit":      AjaxSaveDraft(username, screenName, ip, true,  isAdmin); break;
                case "hod_approve": AjaxHodApprove(username, screenName, ip, isAdmin, isHod); break;
                case "hod_decline": AjaxHodDecline(username, screenName, ip, isAdmin, isHod); break;
                case "hr_approve":  AjaxHrApprove(username, screenName, ip, isAdmin, isHr);  break;
                case "hr_decline":  AjaxHrDecline(username, screenName, ip, isAdmin, isHr);  break;
                case "vc_decision": AjaxVcDecision(username, screenName, roleCode, ip, isAdmin, isVc); break;
                case "cancel":      AjaxCancel(username, screenName, roleCode, ip, isAdmin, isHr); break;
                default: Response.Write("{\"ok\":false,\"error\":\"Unknown action.\"}"); break;
            }
        }
        catch (Exception ex)
        {
            Response.Write("{\"ok\":false,\"error\":" + JsonStr(ex.Message) + "}");
        }
    }

    // ── Save Draft / Submit ────────────────────────────────────────────────────

    private void AjaxSaveDraft(string username, string screenName, string ip, bool submit, bool isAdmin)
    {
        int    appId    = FormInt("id");
        string empName  = FormStr("emp_name");
        string lvType   = FormStr("leave_type");
        string lvFrom   = FormStr("leave_from");
        string lvTo     = FormStr("leave_to");

        if (string.IsNullOrEmpty(empName))
        { Err("Employee name is required."); return; }
        if (string.IsNullOrEmpty(lvType))
        { Err("Leave type is required."); return; }

        DateTime dtFrom, dtTo;
        if (!DateTime.TryParse(lvFrom, out dtFrom) || !DateTime.TryParse(lvTo, out dtTo))
        { Err("Invalid leave dates."); return; }
        if (dtTo < dtFrom)
        { Err("End date must be after start date."); return; }

        int numDays = FormInt("num_days");
        if (numDays <= 0) numDays = (int)Math.Round((dtTo - dtFrom).TotalDays) + 1;

        string supUser = FormStr("supervisor_username");
        string supName = FormStr("supervisor_name");
        if (submit && string.IsNullOrEmpty(supUser))
        { Err("Please select a Supervisor / HOD."); return; }
        if (submit && !isAdmin && string.Equals(supUser.Trim(), username.Trim(), StringComparison.OrdinalIgnoreCase))
        { Err("You cannot approve your own leave. Please choose your Supervisor / HOD."); return; }

        string newStatus = submit ? "SUBMITTED" : "DRAFT";

        using (var conn = new MySqlConnection(ConnStr()))
        {
            conn.Open();

            if (appId <= 0)
            {
                const string sql = @"
                    INSERT INTO hrm_leave_applications
                        (emp_name,emp_code,faculty_dept,office_location,position_held,
                         residence_address,mobile_contact,nok_name,nok_address,nok_mobile,
                         leave_type,leave_from,leave_to,num_days,substitute_arrangement,
                         supervisor_username,supervisor_name,status,
                         employee_submitted_at,created_by,created_at,updated_at)
                    VALUES
                        (@en,@ec,@fd,@ol,@ph,@ra,@mc,@nn,@na,@nm,
                         @lt,@lf,@lt2,@nd,@sa,@su,@sn,@st,@sub_at,@cb,NOW(),NOW())";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    BindDraftParams(cmd, empName, numDays, dtFrom, dtTo, newStatus, supUser, supName, username, submit);
                    cmd.ExecuteNonQuery();
                    appId = (int)cmd.LastInsertedId;
                }
            }
            else
            {
                string existStatus = "", existCreator = "";
                using (var cmd = new MySqlCommand(
                    "SELECT status,created_by FROM hrm_leave_applications WHERE id=@id AND is_active=1", conn))
                {
                    cmd.Parameters.AddWithValue("@id", appId);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read()) { Err("Application not found."); return; }
                        existStatus = dr[0].ToString(); existCreator = dr[1].ToString();
                    }
                }
                if (existStatus != "DRAFT") { Err("Only DRAFT applications can be edited."); return; }
                if (!isAdmin && !string.Equals(existCreator, username, StringComparison.OrdinalIgnoreCase))
                { Err("Permission denied."); return; }

                const string upd = @"
                    UPDATE hrm_leave_applications SET
                        emp_name=@en,emp_code=@ec,faculty_dept=@fd,office_location=@ol,position_held=@ph,
                        residence_address=@ra,mobile_contact=@mc,nok_name=@nn,nok_address=@na,nok_mobile=@nm,
                        leave_type=@lt,leave_from=@lf,leave_to=@lt2,num_days=@nd,substitute_arrangement=@sa,
                        supervisor_username=@su,supervisor_name=@sn,status=@st,
                        employee_submitted_at=@sub_at,updated_at=NOW()
                    WHERE id=@id";
                using (var cmd = new MySqlCommand(upd, conn))
                {
                    BindDraftParams(cmd, empName, numDays, dtFrom, dtTo, newStatus, supUser, supName, username, submit);
                    cmd.Parameters.AddWithValue("@id", appId);
                    cmd.ExecuteNonQuery();
                }
            }

            LogAudit(conn, appId,
                submit ? "SUBMITTED" : "DRAFT_SAVED",
                "DRAFT", newStatus, screenName, username, "Employee",
                submit ? "Application submitted for HOD approval." : "Draft saved.", ip);
        }

        Response.Write("{\"ok\":true,\"id\":" + appId + "}");
    }

    private static void BindDraftParams(MySqlCommand cmd, string empName, int numDays,
        DateTime dtFrom, DateTime dtTo, string newStatus,
        string supUser, string supName, string username, bool submit)
    {
        cmd.Parameters.AddWithValue("@en",     empName);
        cmd.Parameters.AddWithValue("@ec",     HttpContext.Current.Request.Form["emp_code"] ?? "");
        cmd.Parameters.AddWithValue("@fd",     HttpContext.Current.Request.Form["faculty_dept"] ?? "");
        cmd.Parameters.AddWithValue("@ol",     HttpContext.Current.Request.Form["office_location"] ?? "");
        cmd.Parameters.AddWithValue("@ph",     HttpContext.Current.Request.Form["position_held"] ?? "");
        cmd.Parameters.AddWithValue("@ra",     HttpContext.Current.Request.Form["residence_address"] ?? "");
        cmd.Parameters.AddWithValue("@mc",     HttpContext.Current.Request.Form["mobile_contact"] ?? "");
        cmd.Parameters.AddWithValue("@nn",     HttpContext.Current.Request.Form["nok_name"] ?? "");
        cmd.Parameters.AddWithValue("@na",     HttpContext.Current.Request.Form["nok_address"] ?? "");
        cmd.Parameters.AddWithValue("@nm",     HttpContext.Current.Request.Form["nok_mobile"] ?? "");
        cmd.Parameters.AddWithValue("@lt",     HttpContext.Current.Request.Form["leave_type"] ?? "annual");
        cmd.Parameters.AddWithValue("@lf",     dtFrom.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@lt2",    dtTo.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@nd",     numDays);
        cmd.Parameters.AddWithValue("@sa",     HttpContext.Current.Request.Form["substitute_arrangement"] ?? "");
        cmd.Parameters.AddWithValue("@su",     supUser);
        cmd.Parameters.AddWithValue("@sn",     supName);
        cmd.Parameters.AddWithValue("@st",     newStatus);
        cmd.Parameters.AddWithValue("@sub_at", submit ? (object)DateTime.Now : DBNull.Value);
        cmd.Parameters.AddWithValue("@cb",     username);
    }

    // ── HOD Approve ────────────────────────────────────────────────────────────

    private void AjaxHodApprove(string username, string screenName, string ip, bool isAdmin, bool isHod)
    {
        // No role gate here: the assigned Supervisor / HOD (whatever their role) or an
        // admin may act - enforced by the supervisor_username check below.
        int    appId      = FormInt("id");
        string handoverTo = FormStr("hod_handover_to");
        string notes      = FormStr("hod_notes");

        using (var conn = new MySqlConnection(ConnStr()))
        {
            conn.Open();
            string status = "", supUser = "";
            using (var cmd = new MySqlCommand(
                "SELECT status,supervisor_username FROM hrm_leave_applications WHERE id=@id AND is_active=1", conn))
            {
                cmd.Parameters.AddWithValue("@id", appId);
                using (var dr = cmd.ExecuteReader())
                {
                    if (!dr.Read()) { Err("Application not found."); return; }
                    status = dr[0].ToString(); supUser = dr[1].ToString();
                }
            }
            if (status != "SUBMITTED") { Err("Application is not in SUBMITTED status."); return; }
            if (!isAdmin && !string.Equals(supUser, username, StringComparison.OrdinalIgnoreCase))
            { Err("You are not the assigned supervisor for this application."); return; }

            using (var cmd = new MySqlCommand(@"
                UPDATE hrm_leave_applications SET
                    status='HOD_APPROVED',hod_action='APPROVED',
                    hod_handover_to=@ht,hod_notes=@no,
                    hod_actor=@ac,hod_actor_name=@an,hod_action_at=NOW(),updated_at=NOW()
                WHERE id=@id", conn))
            {
                cmd.Parameters.AddWithValue("@ht", handoverTo);
                cmd.Parameters.AddWithValue("@no", notes);
                cmd.Parameters.AddWithValue("@ac", username);
                cmd.Parameters.AddWithValue("@an", screenName);
                cmd.Parameters.AddWithValue("@id", appId);
                cmd.ExecuteNonQuery();
            }
            LogAudit(conn, appId, "HOD_APPROVED", "SUBMITTED", "HOD_APPROVED",
                screenName, username, "HOD / Supervisor",
                string.IsNullOrEmpty(notes) ? null : notes, ip);
        }
        Response.Write("{\"ok\":true}");
    }

    // ── HOD Decline ────────────────────────────────────────────────────────────

    private void AjaxHodDecline(string username, string screenName, string ip, bool isAdmin, bool isHod)
    {
        // No role gate here: the assigned Supervisor / HOD (whatever their role) or an
        // admin may act - enforced by the supervisor_username check below.
        int    appId  = FormInt("id");
        string reason = FormStr("reason");
        if (string.IsNullOrEmpty(reason)) { Err("A reason is required."); return; }

        using (var conn = new MySqlConnection(ConnStr()))
        {
            conn.Open();
            string status = "", supUser = "";
            using (var cmd = new MySqlCommand(
                "SELECT status,supervisor_username FROM hrm_leave_applications WHERE id=@id AND is_active=1", conn))
            {
                cmd.Parameters.AddWithValue("@id", appId);
                using (var dr = cmd.ExecuteReader())
                {
                    if (!dr.Read()) { Err("Application not found."); return; }
                    status = dr[0].ToString(); supUser = dr[1].ToString();
                }
            }
            if (status != "SUBMITTED") { Err("Application is not in SUBMITTED status."); return; }
            if (!isAdmin && !string.Equals(supUser, username, StringComparison.OrdinalIgnoreCase))
            { Err("You are not the assigned supervisor."); return; }

            using (var cmd = new MySqlCommand(@"
                UPDATE hrm_leave_applications SET
                    status='HOD_DECLINED',hod_action='DECLINED',
                    hod_notes=@no,hod_actor=@ac,hod_actor_name=@an,
                    hod_action_at=NOW(),updated_at=NOW()
                WHERE id=@id", conn))
            {
                cmd.Parameters.AddWithValue("@no", reason);
                cmd.Parameters.AddWithValue("@ac", username);
                cmd.Parameters.AddWithValue("@an", screenName);
                cmd.Parameters.AddWithValue("@id", appId);
                cmd.ExecuteNonQuery();
            }
            LogAudit(conn, appId, "HOD_DECLINED", "SUBMITTED", "HOD_DECLINED",
                screenName, username, "HOD / Supervisor", reason, ip);
        }
        Response.Write("{\"ok\":true}");
    }

    // ── HR Approve ─────────────────────────────────────────────────────────────

    private void AjaxHrApprove(string username, string screenName, string ip, bool isAdmin, bool isHr)
    {
        if (!isAdmin && !isHr) { Err("Access denied."); return; }
        int    appId  = FormInt("id");
        string effFrom = FormStr("hr_effective_from");
        string effTo   = FormStr("hr_effective_to");
        string notes   = FormStr("hr_notes");

        DateTime dtFrom, dtTo;
        if (!DateTime.TryParse(effFrom, out dtFrom) || !DateTime.TryParse(effTo, out dtTo))
        { Err("Effective dates are required."); return; }

        using (var conn = new MySqlConnection(ConnStr()))
        {
            conn.Open();
            string status = GetStatus(conn, appId);
            if (status == null) { Err("Application not found."); return; }
            if (status != "HOD_APPROVED") { Err("Application must be in HOD_APPROVED status."); return; }

            using (var cmd = new MySqlCommand(@"
                UPDATE hrm_leave_applications SET
                    status='HR_APPROVED',hr_action='APPROVED',
                    hr_effective_from=@ef,hr_effective_to=@et,hr_notes=@no,
                    hr_actor=@ac,hr_actor_name=@an,hr_action_at=NOW(),updated_at=NOW()
                WHERE id=@id", conn))
            {
                cmd.Parameters.AddWithValue("@ef", dtFrom.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@et", dtTo.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@no", notes);
                cmd.Parameters.AddWithValue("@ac", username);
                cmd.Parameters.AddWithValue("@an", screenName);
                cmd.Parameters.AddWithValue("@id", appId);
                cmd.ExecuteNonQuery();
            }
            LogAudit(conn, appId, "HR_APPROVED", "HOD_APPROVED", "HR_APPROVED",
                screenName, username, "HR Department",
                string.IsNullOrEmpty(notes) ? null : notes, ip);
        }
        Response.Write("{\"ok\":true}");
    }

    // ── HR Decline ─────────────────────────────────────────────────────────────

    private void AjaxHrDecline(string username, string screenName, string ip, bool isAdmin, bool isHr)
    {
        if (!isAdmin && !isHr) { Err("Access denied."); return; }
        int    appId  = FormInt("id");
        string reason = FormStr("reason");
        if (string.IsNullOrEmpty(reason)) { Err("A reason is required."); return; }

        using (var conn = new MySqlConnection(ConnStr()))
        {
            conn.Open();
            string status = GetStatus(conn, appId);
            if (status == null) { Err("Application not found."); return; }
            if (status != "HOD_APPROVED") { Err("Application must be HOD_APPROVED."); return; }

            using (var cmd = new MySqlCommand(@"
                UPDATE hrm_leave_applications SET
                    status='HR_DECLINED',hr_action='DECLINED',
                    hr_notes=@no,hr_actor=@ac,hr_actor_name=@an,
                    hr_action_at=NOW(),updated_at=NOW()
                WHERE id=@id", conn))
            {
                cmd.Parameters.AddWithValue("@no", reason);
                cmd.Parameters.AddWithValue("@ac", username);
                cmd.Parameters.AddWithValue("@an", screenName);
                cmd.Parameters.AddWithValue("@id", appId);
                cmd.ExecuteNonQuery();
            }
            LogAudit(conn, appId, "HR_DECLINED", "HOD_APPROVED", "HR_DECLINED",
                screenName, username, "HR Department", reason, ip);
        }
        Response.Write("{\"ok\":true}");
    }

    // ── VC Decision ────────────────────────────────────────────────────────────

    private void AjaxVcDecision(string username, string screenName, string roleCode,
        string ip, bool isAdmin, bool isVc)
    {
        if (!isAdmin && !isVc) { Err("Access denied."); return; }
        int    appId    = FormInt("id");
        string decision = FormStr("vc_decision").ToUpper();
        string reason   = FormStr("vc_reason");
        int    accum    = FormInt("vc_accumulated_days");
        int    taken    = FormInt("vc_days_taken");

        string[] valid = { "GRANTED", "NOT_GRANTED", "POSTPONED" };
        if (Array.IndexOf(valid, decision) < 0) { Err("Invalid decision value."); return; }
        if (decision != "GRANTED" && string.IsNullOrEmpty(reason))
        { Err("A reason is required for this decision."); return; }

        string newStatus = "VC_" + decision;

        using (var conn = new MySqlConnection(ConnStr()))
        {
            conn.Open();
            string status = GetStatus(conn, appId);
            if (status == null) { Err("Application not found."); return; }
            if (status != "HR_APPROVED") { Err("Application must be HR_APPROVED."); return; }

            using (var cmd = new MySqlCommand(@"
                UPDATE hrm_leave_applications SET
                    status=@ns,vc_decision=@vd,vc_reason=@vr,
                    vc_accumulated_days=@va,vc_days_taken=@vt,
                    vc_actor=@ac,vc_actor_name=@an,vc_action_at=NOW(),updated_at=NOW()
                WHERE id=@id", conn))
            {
                cmd.Parameters.AddWithValue("@ns", newStatus);
                cmd.Parameters.AddWithValue("@vd", decision);
                cmd.Parameters.AddWithValue("@vr", string.IsNullOrEmpty(reason) ? (object)DBNull.Value : reason);
                cmd.Parameters.AddWithValue("@va", accum > 0 ? (object)accum : DBNull.Value);
                cmd.Parameters.AddWithValue("@vt", taken > 0 ? (object)taken : DBNull.Value);
                cmd.Parameters.AddWithValue("@ac", username);
                cmd.Parameters.AddWithValue("@an", screenName);
                cmd.Parameters.AddWithValue("@id", appId);
                cmd.ExecuteNonQuery();
            }
            LogAudit(conn, appId, "VC_" + decision, "HR_APPROVED", newStatus,
                screenName, username, "Vice Chancellor's Office",
                string.IsNullOrEmpty(reason) ? null : reason, ip);
        }
        Response.Write("{\"ok\":true}");
    }

    // ── Cancel ─────────────────────────────────────────────────────────────────

    private void AjaxCancel(string username, string screenName, string roleCode,
        string ip, bool isAdmin, bool isHr)
    {
        if (!isAdmin && !isHr) { Err("Only Admin or HR can cancel applications."); return; }
        int appId = FormInt("id");

        using (var conn = new MySqlConnection(ConnStr()))
        {
            conn.Open();
            string status = GetStatus(conn, appId);
            if (status == null) { Err("Application not found."); return; }
            if (status == "CANCELLED") { Err("Already cancelled."); return; }

            using (var cmd = new MySqlCommand(
                "UPDATE hrm_leave_applications SET status='CANCELLED',updated_at=NOW() WHERE id=@id", conn))
            {
                cmd.Parameters.AddWithValue("@id", appId);
                cmd.ExecuteNonQuery();
            }
            LogAudit(conn, appId, "CANCELLED", status, "CANCELLED",
                screenName, username, roleCode, "Application cancelled.", ip);
        }
        Response.Write("{\"ok\":true}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SUPERVISOR OPTIONS
    // ══════════════════════════════════════════════════════════════════════════

    private const string ApproverRolesSql = "'dean','registrar','hr_manager','admin','hod'";

    // Everyone who can be chosen as Supervisor / HOD: [0]=login username, [1]=display name, [2]=role label.
    // (The previous version joined hrm_employee.username, a column that does not exist, so it
    //  always fell back to bare usernames. The staff login column is hrm_employee.usernames.)
    private List<string[]> LoadSupervisors()
    {
        var list = new List<string[]>();
        try
        {
            using (var conn = new MySqlConnection(ConnStr()))
            {
                conn.Open();
                string sql = @"
                    SELECT ur.username,
                           (SELECT e.emp_name FROM hrm_employee e
                             WHERE e.usernames = ur.username ORDER BY e.empID LIMIT 1) AS emp_name,
                           GROUP_CONCAT(DISTINCT r.role_code) AS roles
                    FROM sys_user_roles ur
                    JOIN sys_roles r ON r.id = ur.role_id AND r.role_code IN (" + ApproverRolesSql + @")
                    WHERE ur.is_active = 1
                      AND (ur.expires_at IS NULL OR ur.expires_at > NOW())
                    GROUP BY ur.username";
                using (var cmd = new MySqlCommand(sql, conn))
                using (var dr  = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        string u = dr[0].ToString().Trim();
                        if (u.Length == 0) continue;
                        string n = dr.IsDBNull(1) ? "" : dr[1].ToString().Trim();
                        string roles = dr.IsDBNull(2) ? "" : dr[2].ToString();
                        list.Add(new[] { u, n.Length > 0 ? n : u, RoleLabel(roles) });
                    }
                }
            }
        }
        catch { /* an empty list still lets a saved choice render */ }
        list.Sort(delegate(string[] a, string[] b) { return string.Compare(a[1], b[1], StringComparison.OrdinalIgnoreCase); });
        return list;
    }

    private static string RoleLabel(string roles)
    {
        string r = "," + (roles ?? "").ToLowerInvariant() + ",";
        if (r.Contains(",dean,"))       return "Dean";
        if (r.Contains(",hod,"))        return "HOD";
        if (r.Contains(",registrar,"))  return "Registrar";
        if (r.Contains(",hr_manager,")) return "HR";
        return "";
    }

    // Renders the <option>s. The option text carries the role; data-name carries the plain
    // name that is posted as supervisor_name (unchanged contract).
    private static string BuildSupervisorOptions(List<string[]> list, string selUser, string selName,
        bool forceSelected, string currentUser, out bool found)
    {
        found = false;
        string sel = (selUser ?? "").Trim();
        string me  = (currentUser ?? "").Trim();
        var sb = new StringBuilder();
        foreach (string[] s in list)
        {
            bool isSel = sel.Length > 0 && string.Equals(s[0], sel, StringComparison.OrdinalIgnoreCase);
            // Nobody approves their own leave: hide the applicant unless already saved.
            if (!isSel && me.Length > 0 && string.Equals(s[0], me, StringComparison.OrdinalIgnoreCase)) continue;
            if (isSel) found = true;
            sb.Append(SupOption(s[0], s[1], s[2], isSel));
        }
        if (!found && sel.Length > 0 && forceSelected)
        {
            sb.Insert(0, SupOption(sel, string.IsNullOrEmpty(selName) ? sel : selName, "", true));
            found = true;
        }
        return sb.ToString();
    }

    private static string SupOption(string user, string name, string role, bool selected)
    {
        return string.Format("<option value=\"{0}\" data-name=\"{1}\"{2}>{3}</option>",
            HttpUtility.HtmlAttributeEncode(user),
            HttpUtility.HtmlAttributeEncode(name),
            selected ? " selected" : "",
            HttpUtility.HtmlEncode(string.IsNullOrEmpty(role) ? name : name + " (" + role + ")"));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  STAFF PROFILE (pre-fill for a new application)
    // ══════════════════════════════════════════════════════════════════════════

    private sealed class StaffProfile
    {
        public int    EmpId;
        public string Name = "", Code = "", Dept = "", Position = "", Mobile = "", Residence = "";
        public string NokName = "", NokPhone = "";
        public string SupUsername = "", SupName = "";
        public bool   SupCanApprove;   // supervisor has an active RBAC role (can open this page)
    }

    // Matches the login to hrm_employee: usernames (case-insensitive, trimmed), else
    // EMP_CODE, else emp_email - each only when it identifies exactly one employee.
    private StaffProfile LoadStaffProfile(string username)
    {
        var p = new StaffProfile();
        string u = (username ?? "").Trim();
        if (u.Length == 0) return p;
        try
        {
            using (var conn = new MySqlConnection(ConnStr()))
            {
                conn.Open();
                int empId = UniqueEmpId(conn, "LOWER(TRIM(usernames)) = LOWER(@u)", u);
                if (empId <= 0) empId = UniqueEmpId(conn, "LOWER(TRIM(EMP_CODE)) = LOWER(@u)", u);
                if (empId <= 0 && u.IndexOf('@') > 0) empId = UniqueEmpId(conn, "LOWER(TRIM(emp_email)) = LOWER(@u)", u);
                if (empId <= 0) return p;
                p.EmpId = empId;

                const string sql = @"
                    SELECT e.emp_name, e.EMP_CODE, e.emp_phone, e.contact_person, e.relation,
                           e.phone_contacts, e.current_residence,
                           d.dept_name, j.jobname, hd.dept_name AS home_dept,
                           s.emp_name AS sup_name, s.usernames AS sup_login
                    FROM hrm_employee e
                    LEFT JOIN hrm_emp_contracts c ON c.ID = (
                         SELECT c2.ID FROM hrm_emp_contracts c2
                          WHERE c2.empID = e.empID AND c2.contractStatus = 'VALID'
                          ORDER BY c2.contractStart DESC, c2.ID DESC LIMIT 1)
                    LEFT JOIN hrm_departments d  ON d.ID  = c.departmentID
                    LEFT JOIN hrm_jobs        j  ON j.ID  = c.jobID
                    LEFT JOIN hrm_departments hd ON hd.ID = e.dept_id
                    LEFT JOIN hrm_employee    s  ON s.empID = e.supervisorID AND s.empID <> e.empID
                    WHERE e.empID = @id";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", empId);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            p.Name      = Col(dr, "emp_name");
                            p.Code      = Col(dr, "EMP_CODE");
                            p.Mobile    = CleanPhone(Col(dr, "emp_phone"));
                            p.Residence = CleanText(Col(dr, "current_residence")).Replace("\r", "").Replace("\n", ", ");
                            p.Dept      = Col(dr, "dept_name");
                            if (p.Dept.Length == 0) p.Dept = Col(dr, "home_dept");
                            p.Position  = Col(dr, "jobname");

                            string nok = CleanText(Col(dr, "contact_person"));
                            string rel = CleanText(Col(dr, "relation"));
                            if (nok.Length > 0)
                            {
                                p.NokName  = rel.Length > 0 && nok.IndexOf(rel, StringComparison.OrdinalIgnoreCase) < 0
                                    ? nok + " (" + rel.ToLowerInvariant() + ")" : nok;
                                p.NokPhone = CleanPhone(Col(dr, "phone_contacts"));
                            }

                            p.SupName     = Col(dr, "sup_name");
                            p.SupUsername = Col(dr, "sup_login");
                        }
                    }
                }

                if (p.SupUsername.Length > 0)
                {
                    using (var cmd = new MySqlCommand(@"
                        SELECT COUNT(*) FROM sys_user_roles ur
                        JOIN sys_roles r ON r.id = ur.role_id AND r.is_active = 1
                        WHERE ur.username = @u AND ur.is_active = 1
                          AND (ur.expires_at IS NULL OR ur.expires_at > NOW())", conn))
                    {
                        cmd.Parameters.AddWithValue("@u", p.SupUsername);
                        p.SupCanApprove = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }
            }
        }
        catch { /* pre-fill is a convenience; never break the form */ }
        return p;
    }

    private static int UniqueEmpId(MySqlConnection conn, string where, string u)
    {
        using (var cmd = new MySqlCommand("SELECT empID FROM hrm_employee WHERE " + where + " LIMIT 2", conn))
        {
            cmd.Parameters.AddWithValue("@u", u);
            int id = 0, n = 0;
            using (var dr = cmd.ExecuteReader())
                while (dr.Read()) { n++; id = Convert.ToInt32(dr[0]); }
            return n == 1 ? id : 0;
        }
    }

    private static string Col(MySqlDataReader dr, string name)
    {
        object v = dr[name];
        return (v == null || v is DBNull) ? "" : v.ToString().Trim();
    }

    // HR records use "-", "0", "1" etc. as placeholders; keep only real text.
    private static string CleanText(string s)
    {
        s = (s ?? "").Trim().Trim('-', '.', ',', ' ').Trim();
        if (s.Length < 2) return "";
        foreach (char ch in s) if (char.IsLetter(ch)) return s;
        return "";
    }

    private static string CleanPhone(string s)
    {
        s = (s ?? "").Trim().Trim('-', ' ').Trim();
        int digits = 0;
        foreach (char ch in s) if (char.IsDigit(ch)) digits++;
        return digits >= 9 ? s : "";
    }

    // JavaScript string literal safe to place inside a <script> block.
    private static string JsStr(string s)
    {
        if (s == null) return "\"\"";
        var sb = new StringBuilder("\"");
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"':  sb.Append("\\\""); break;
                case '\n': sb.Append("\\n");  break;
                case '\r': sb.Append("\\r");  break;
                case '<':  sb.Append("\\u003c"); break;
                case '>':  sb.Append("\\u003e"); break;
                case '&':  sb.Append("\\u0026"); break;
                case (char)0x2028: sb.Append("\\u2028"); break;
                case (char)0x2029: sb.Append("\\u2029"); break;
                default:
                    if (c < ' ') sb.AppendFormat("\\u{0:x4}", (int)c); else sb.Append(c);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  DB HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private Dictionary<string, object> LoadApp(int appId)
    {
        try
        {
            using (var conn = new MySqlConnection(ConnStr()))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT * FROM hrm_leave_applications WHERE id=@id AND is_active=1", conn))
                {
                    cmd.Parameters.AddWithValue("@id", appId);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read()) return null;
                        var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < dr.FieldCount; i++)
                            d[dr.GetName(i)] = dr.IsDBNull(i) ? null : dr.GetValue(i);
                        return d;
                    }
                }
            }
        }
        catch { return null; }
    }

    private static string GetStatus(MySqlConnection conn, int appId)
    {
        using (var cmd = new MySqlCommand(
            "SELECT status FROM hrm_leave_applications WHERE id=@id AND is_active=1", conn))
        {
            cmd.Parameters.AddWithValue("@id", appId);
            using (var dr = cmd.ExecuteReader())
                return dr.Read() ? dr[0].ToString() : null;
        }
    }

    private static void LogAudit(MySqlConnection conn, int appId,
        string actionCode, string oldStatus, string newStatus,
        string actorName, string actorUsername, string actorRole, string remarks, string ip)
    {
        try
        {
            const string sql = @"
                INSERT INTO hrm_leave_audit
                    (application_id,action_code,old_status,new_status,
                     actor_name,actor_username,actor_role,remarks,ip_address,created_at)
                VALUES(@ai,@ac,@os,@ns,@an,@au,@ar,@rm,@ip,NOW())";
            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@ai", appId);
                cmd.Parameters.AddWithValue("@ac", actionCode);
                cmd.Parameters.AddWithValue("@os", string.IsNullOrEmpty(oldStatus) ? (object)DBNull.Value : oldStatus);
                cmd.Parameters.AddWithValue("@ns", string.IsNullOrEmpty(newStatus) ? (object)DBNull.Value : newStatus);
                cmd.Parameters.AddWithValue("@an", actorName);
                cmd.Parameters.AddWithValue("@au", actorUsername);
                cmd.Parameters.AddWithValue("@ar", actorRole);
                cmd.Parameters.AddWithValue("@rm", string.IsNullOrEmpty(remarks) ? (object)DBNull.Value : remarks);
                cmd.Parameters.AddWithValue("@ip", ip ?? "");
                cmd.ExecuteNonQuery();
            }
        }
        catch { /* audit must never break main flow */ }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  HTML HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private static string LfField(string label, string id, string type, string value, string placeholder)
    {
        return string.Format(
            "<div class=\"lf-field\"><label>{0}</label>" +
            "<input type=\"{1}\" id=\"{2}\" value=\"{3}\" placeholder=\"{4}\" /></div>",
            HttpUtility.HtmlEncode(label), type, id,
            HttpUtility.HtmlAttributeEncode(value ?? ""),
            HttpUtility.HtmlAttributeEncode(placeholder ?? ""));
    }

    private static string LfTextarea(string label, string id, string value, string placeholder)
    {
        return string.Format(
            "<div class=\"lf-field\"><label>{0}</label>" +
            "<textarea id=\"{1}\" placeholder=\"{2}\">{3}</textarea></div>",
            HttpUtility.HtmlEncode(label), id,
            HttpUtility.HtmlAttributeEncode(placeholder ?? ""),
            HttpUtility.HtmlEncode(value ?? ""));
    }

    private static string RoField(string label, string value)
    {
        bool empty = string.IsNullOrWhiteSpace(value);
        return string.Format(
            "<div class=\"lf-field\"><label>{0}</label>" +
            "<div class=\"lf-read-val{1}\">{2}</div></div>",
            HttpUtility.HtmlEncode(label),
            empty ? " lf-read-val--empty" : "",
            empty ? "&mdash;" : HttpUtility.HtmlEncode(value));
    }

    private static string SectionLocked(string num, string title, string reason)
    {
        return string.Format(
            "<div class=\"lf-section lf-section--locked\">" +
            "<div class=\"lf-section__head\">" +
            "<div class=\"lf-section__num\">{0}</div>" +
            "<div class=\"lf-section__title\">{1}</div>" +
            "<span class=\"lf-section__badge badge--locked\">Locked</span>" +
            "</div>" +
            "<div class=\"lf-section__body\">" +
            "<p style=\"font-size:12px;color:var(--muted);margin:0;\">{2}</p>" +
            "</div></div>",
            num, HttpUtility.HtmlEncode(title), HttpUtility.HtmlEncode(reason));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  BADGE / LABEL HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private static string StatusBadge(string status)
    {
        string cls, label;
        switch (status)
        {
            case "DRAFT":          cls = "draft";           label = "Draft";        break;
            case "SUBMITTED":      cls = "submitted";       label = "Pending HOD";  break;
            case "HOD_APPROVED":   cls = "hod_approved";   label = "Pending HR";   break;
            case "HOD_DECLINED":   cls = "hod_declined";   label = "HOD Declined"; break;
            case "HR_APPROVED":    cls = "hr_approved";    label = "Pending VC";   break;
            case "HR_DECLINED":    cls = "hr_declined";    label = "HR Declined";  break;
            case "VC_GRANTED":     cls = "vc_granted";     label = "Granted";      break;
            case "VC_NOT_GRANTED": cls = "vc_not_granted"; label = "Not Granted";  break;
            case "VC_POSTPONED":   cls = "vc_postponed";   label = "Postponed";    break;
            case "CANCELLED":      cls = "cancelled";      label = "Cancelled";    break;
            default:               cls = "draft";           label = status;         break;
        }
        return string.Format("<span class=\"lf-status lf-status--{0}\">{1}</span>", cls, label);
    }

    private static string LeaveTypeLabel(string type)
    {
        switch (type)
        {
            case "annual":      return "Annual Leave";
            case "study":       return "Study Leave";
            case "sick":        return "Sick Leave";
            case "maternity":   return "Maternity Leave";
            case "bereavement": return "Family Bereavement";
            default:            return type;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  DATA UTILITIES
    // ══════════════════════════════════════════════════════════════════════════

    private static string S(Dictionary<string, object> d, string key)
    {
        object v; return d.TryGetValue(key, out v) && v != null ? v.ToString() : "";
    }
    private static int N(Dictionary<string, object> d, string key)
    {
        object v; int i = 0;
        if (d.TryGetValue(key, out v) && v != null && !(v is DBNull)) int.TryParse(v.ToString(), out i);
        return i;
    }
    private static string DateVal(Dictionary<string, object> d, string key)
    {
        object v;
        if (!d.TryGetValue(key, out v) || v == null || v is DBNull) return "";
        DateTime dt; if (v is DateTime) dt = (DateTime)v;
        else if (!DateTime.TryParse(v.ToString(), out dt)) return "";
        return dt.ToString("yyyy-MM-dd");
    }
    private static string FormatDate(Dictionary<string, object> d, string key)
    {
        object v;
        if (!d.TryGetValue(key, out v) || v == null || v is DBNull) return "";
        DateTime dt; if (v is DateTime) dt = (DateTime)v;
        else if (!DateTime.TryParse(v.ToString(), out dt)) return "";
        return dt.ToString("dd MMM yyyy");
    }
    private static string FormatDateTime(Dictionary<string, object> d, string key)
    {
        object v;
        if (!d.TryGetValue(key, out v) || v == null || v is DBNull) return "";
        DateTime dt; if (v is DateTime) dt = (DateTime)v;
        else if (!DateTime.TryParse(v.ToString(), out dt)) return "";
        return dt.ToString("dd MMM yyyy, h:mm tt");
    }

    private string FormStr(string key) { return (Request.Form[key] ?? "").Trim(); }
    private int    FormInt(string key) { int v = 0; int.TryParse(FormStr(key), out v); return v; }

    private void Err(string msg)
    {
        Response.Write("{\"ok\":false,\"error\":" + JsonStr(msg) + "}");
    }

    private static string JsonStr(string s)
    {
        if (s == null) return "null";
        return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                        .Replace("\n", "\\n").Replace("\r", "\\r") + "\"";
    }

    private static string ConnStr()
    {
        var cs = ConfigurationManager.ConnectionStrings["vacConnectionString"];
        if (cs != null && !string.IsNullOrEmpty(cs.ConnectionString)) return cs.ConnectionString;
        cs = ConfigurationManager.ConnectionStrings["DefaultConnection"];
        if (cs != null) return cs.ConnectionString;
        throw new InvalidOperationException("No valid connection string.");
    }
}
