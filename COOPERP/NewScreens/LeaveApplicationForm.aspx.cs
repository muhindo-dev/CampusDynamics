using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

/// <summary>
/// Leave application form: Section 1 (employee), 2 (Head of Department), 3 (HR) and
/// 4 (Vice Chancellor). Used by every employee, by whoever they chose as Supervisor / HOD,
/// by HR and by the Vice Chancellor; the role rules below decide who may act at each stage.
/// ?print=1 renders the official form through HrDocument with all four sections and
/// signature lines.
/// </summary>
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
        string ajax = (Request.QueryString["ajax"] ?? "").Trim();
        if (!string.IsNullOrEmpty(ajax))
        {
            Response.Clear();
            Response.ContentType = "application/json";
            Response.Cache.SetNoStore();
            if (!HrAccess.IsSignedIn())
            {
                Response.StatusCode = 401;
                Response.Write("{\"ok\":false,\"error\":\"Your session has expired. Please sign in again.\"}");
                Response.Flush();
                Response.End();
                return;
            }
            RoleAccessService.RequireSlug(this, "hr.leave_applications");
            HandleAjax(ajax);
            Response.End();
            return;
        }

        if (!HrAccess.IsSignedIn()) { Response.Redirect("~/Default.aspx", true); return; }
        RoleAccessService.RequireSlug(this, "hr.leave_applications");

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

        if (isPrint && appId > 0)
        {
            RenderPrint(appId, username, isAdmin, isHr, isVc, isHod);
            return;
        }

        if (appId <= 0)
            RenderNewForm(username, screenName);
        else
            RenderExistingForm(appId, username, screenName, roleCode, isAdmin, isHr, isVc, isHod);
    }

    /// <summary>Employees see only their own; the chosen Supervisor / HOD sees theirs; HR, VC and HOD roles see all.</summary>
    private static bool CanView(Dictionary<string, object> app, string username, bool isAdmin, bool isHr, bool isVc, bool isHod)
    {
        string createdBy = S(app, "created_by");
        string supUser   = S(app, "supervisor_username").Trim();
        bool isAssignedHod = supUser.Length > 0 && string.Equals(supUser, username.Trim(), StringComparison.OrdinalIgnoreCase);
        return isAdmin || isHr || isVc || isHod || isAssignedHod ||
               string.Equals(createdBy, username, StringComparison.OrdinalIgnoreCase);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  NEW APPLICATION
    // ══════════════════════════════════════════════════════════════════════════

    private void RenderNewForm(string username, string screenName)
    {
        litPageTitle.Text   = "New leave application";
        litStatusBadge.Text = StatusBadge("DRAFT");
        litHeaderSub.Text   = "Complete section 1 and submit it to your Head of Department";
        litAppId.Text       = "0";
        litPrintBtn.Text    = "";
        litTimeline.Text    = BuildTimeline("DRAFT");

        StaffProfile prof   = LoadStaffProfile(username);
        if (string.IsNullOrEmpty(prof.Name)) prof.Name = screenName;
        litSection1.Text    = BuildSection1Editable(null, prof, username);
        litSection2.Text    = SectionLocked(2, "Opens when the application is submitted.");
        litSection3.Text    = SectionLocked(3, "Opens after the Head of Department approves.");
        litSection4.Text    = SectionLocked(4, "Opens after HR approves.");
        litAuditTrail.Text  = "";
        litActionBar.Text   = DraftButtons();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  EXISTING APPLICATION
    // ══════════════════════════════════════════════════════════════════════════

    private void RenderExistingForm(int appId, string username, string screenName,
        string roleCode, bool isAdmin, bool isHr, bool isVc, bool isHod)
    {
        Dictionary<string, object> app = LoadApp(appId);

        if (app == null)
        {
            litPageTitle.Text = "Leave application";
            litSection1.Text  = "<div class=\"hr-notice hr-notice--bad\">This application was not found or has been removed.</div>";
            litActionBar.Text = "";
            return;
        }

        string status    = S(app, "status");
        string empName   = S(app, "emp_name");
        string createdBy = S(app, "created_by");
        string supUser   = S(app, "supervisor_username");

        // Only the assigned Supervisor / HOD (or an admin) can act: the same rule
        // the hod_approve / hod_decline handlers enforce.
        bool isAssignedHod = !string.IsNullOrEmpty(supUser) &&
                             string.Equals(supUser.Trim(), username.Trim(), StringComparison.OrdinalIgnoreCase);

        if (!CanView(app, username, isAdmin, isHr, isVc, isHod))
        {
            litPageTitle.Text = "Leave application";
            litSection1.Text  = "<div class=\"hr-notice hr-notice--bad\">You do not have permission to view this application.</div>";
            litActionBar.Text = "";
            return;
        }

        litPageTitle.Text   = "Leave application: " + Enc(empName);
        litStatusBadge.Text = StatusBadge(status);
        litHeaderSub.Text   = Enc(Ref(appId) + ", " + LeaveTypeLabel(S(app, "leave_type")) + ", " +
                              FormatDate(app, "leave_from") + " to " + FormatDate(app, "leave_to"));
        litAppId.Text       = appId.ToString();
        litPrintBtn.Text    =
            "<a class=\"hr-btn hr-btn--inverse\" href=\"LeaveApplicationForm.aspx?id=" + appId + "&amp;print=1\" target=\"_blank\">" +
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><polyline points=\"6 9 6 2 18 2 18 9\"/><path d=\"M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2\"/><rect x=\"6\" y=\"14\" width=\"12\" height=\"8\"/></svg>" +
            "Print</a>";

        litTimeline.Text = BuildTimeline(status);

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
            litSection2.Text = SectionLocked(2, "Opens when the application is submitted.");
        else if (status == "SUBMITTED" && canActHod)
            litSection2.Text = SectionWaiting(2, "Awaiting your decision", "Approve to forward the application to HR, or decline it.");
        else if (status == "SUBMITTED")
            litSection2.Text = SectionLocked(2, "Awaiting the Head of Department.");
        else if (status == "CANCELLED" && string.IsNullOrEmpty(S(app, "hod_action")))
            litSection2.Text = SectionLocked(2, "Not reached.");
        else
            litSection2.Text = BuildSection2Readonly(app);

        // ── Section 3 ──────────────────────────────────────────────────────────
        bool canActHr = (status == "HOD_APPROVED") && (isAdmin || isHr);

        if (status == "DRAFT" || status == "SUBMITTED")
            litSection3.Text = SectionLocked(3, "Opens after the Head of Department approves.");
        else if (status == "HOD_DECLINED")
            litSection3.Text = SectionLocked(3, "Not required: declined by the Head of Department.");
        else if (canActHr)
            litSection3.Text = SectionWaiting(3, "Awaiting your decision",
                "Duties handed over to " + Or(S(app, "hod_handover_to"), "nobody named") + ". Approved by " + Or(S(app, "hod_actor_name"), "the Head of Department") + ".");
        else if (status == "HOD_APPROVED")
            litSection3.Text = SectionLocked(3, "Awaiting HR.");
        else if (status == "CANCELLED" && string.IsNullOrEmpty(S(app, "hr_action")))
            litSection3.Text = SectionLocked(3, "Not reached.");
        else
            litSection3.Text = BuildSection3Readonly(app);

        // ── Section 4 ──────────────────────────────────────────────────────────
        bool canActVc = (status == "HR_APPROVED") && (isAdmin || isVc);
        bool vcDone   = (status == "VC_GRANTED" || status == "VC_NOT_GRANTED" || status == "VC_POSTPONED");

        if (status == "HOD_DECLINED" || status == "HR_DECLINED")
            litSection4.Text = SectionLocked(4, "Not required: the application was declined.");
        else if (status == "DRAFT" || status == "SUBMITTED" || status == "HOD_APPROVED")
            litSection4.Text = SectionLocked(4, "Opens after HR approves.");
        else if (canActVc)
            litSection4.Text = SectionWaiting(4, "Awaiting your decision",
                "HR set the leave from " + Or(FormatDate(app, "hr_effective_from"), "the requested date") + " to " + Or(FormatDate(app, "hr_effective_to"), "the requested date") + ".");
        else if (status == "HR_APPROVED")
            litSection4.Text = SectionLocked(4, "Awaiting the Vice Chancellor.");
        else if (vcDone)
            litSection4.Text = BuildSection4Readonly(app);
        else
            litSection4.Text = SectionLocked(4, "Not reached.");

        litAuditTrail.Text = BuildAuditTrail(appId);
        litActionBar.Text  = BuildActionBar(status, isAdmin, isHr, canEditDraft, canActHod, canActHr, canActVc) +
            AppJsData(app);
    }

    // Values the approver modals pre-fill from (dates, days, cover arrangement).
    private static string AppJsData(Dictionary<string, object> app)
    {
        int nd = N(app, "num_days");
        string summary =
            "<strong>" + Enc(S(app, "emp_name")) + "</strong>, " +
            Enc(LeaveTypeLabel(S(app, "leave_type"))) + "<br/>" +
            Enc(LongDate(app, "leave_from")) + " to " +
            Enc(LongDate(app, "leave_to")) +
            (nd > 0 ? ", " + nd + (nd == 1 ? " day" : " days") : "");
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
            "<button type=\"button\" class=\"hr-btn hr-btn--secondary\" id=\"btnSaveDraft\" onclick=\"saveDraft(false)\">Save draft</button>" +
            "<button type=\"button\" class=\"hr-btn hr-btn--primary\" id=\"btnSubmitApp\" onclick=\"saveDraft(true)\">" +
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><line x1=\"22\" y1=\"2\" x2=\"11\" y2=\"13\"/><polygon points=\"22 2 15 22 11 13 2 9 22 2\"/></svg>" +
            "Submit application</button>";
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  STAGES
    // ══════════════════════════════════════════════════════════════════════════

    private static string BuildTimeline(string status)
    {
        // 0 = draft, 1 = with HOD, 2 = with HR, 3 = with VC, 4 = decided
        string[] cls = new string[4];
        switch (status)
        {
            case "DRAFT":          cls = new[] { "is-now", "", "", "" }; break;
            case "SUBMITTED":      cls = new[] { "is-done", "is-now", "", "" }; break;
            case "HOD_DECLINED":   cls = new[] { "is-done", "is-stop", "", "" }; break;
            case "HOD_APPROVED":   cls = new[] { "is-done", "is-done", "is-now", "" }; break;
            case "HR_DECLINED":    cls = new[] { "is-done", "is-done", "is-stop", "" }; break;
            case "HR_APPROVED":    cls = new[] { "is-done", "is-done", "is-done", "is-now" }; break;
            case "VC_GRANTED":     cls = new[] { "is-done", "is-done", "is-done", "is-done" }; break;
            case "VC_NOT_GRANTED":
            case "VC_POSTPONED":   cls = new[] { "is-done", "is-done", "is-done", "is-stop" }; break;
            default:               cls = new[] { "is-done", "", "", "" }; break;
        }
        string[] labels = { "Employee", "Head of Department", "HR", "Vice Chancellor" };
        StringBuilder sb = new StringBuilder("<ul class=\"hr-stages\">");
        for (int i = 0; i < 4; i++)
            sb.Append("<li").Append(cls[i] != "" ? " class=\"" + cls[i] + "\"" : "").Append("><span></span>")
              .Append(labels[i]).Append("</li>");
        sb.Append("</ul>");
        return sb.ToString();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SECTION 1: EMPLOYEE
    // ══════════════════════════════════════════════════════════════════════════

    private const string ReqMark = " <span class=\"hr-req\" title=\"Required\">*</span>";

    private static readonly string[][] LeaveTypes =
    {
        new[] { "annual",      "Annual leave" },
        new[] { "study",       "Study leave" },
        new[] { "sick",        "Sick leave" },
        new[] { "maternity",   "Maternity leave" },
        new[] { "bereavement", "Family bereavement" }
    };

    private static string SectionTitle(int n)
    {
        switch (n)
        {
            case 1: return "Section 1: Leave request";
            case 2: return "Section 2: Head of Department";
            case 3: return "Section 3: Human Resources";
            default: return "Section 4: Vice Chancellor";
        }
    }

    private static string SectionHead(int n, string badgeHtml)
    {
        return "<div class=\"hr-card__head\"><div class=\"hr-card__title\">" + SectionTitle(n) + "</div>" + badgeHtml + "</div>";
    }

    private static string Badge(string kind, string text)
    {
        return "<span class=\"hr-badge hr-badge--" + kind + "\">" + HttpUtility.HtmlEncode(text) + "</span>";
    }

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

        StringBuilder sb = new StringBuilder();
        sb.Append("<div class=\"hr-card lf-sec lf-sec--now\" id=\"sec1\">");
        sb.Append(SectionHead(1, Badge("neutral", "Draft")));
        sb.Append("<div class=\"hr-card__body\" id=\"sec1Form\">");

        // Leave requested
        sb.Append("<div class=\"lf-group\">Leave requested</div>");
        sb.Append("<div class=\"hr-form\">");
        sb.Append("<div class=\"hr-field hr-full\" id=\"fld_leaveType\">");
        sb.Append("<span class=\"hr-label\" id=\"lblLeaveType\">Type of leave" + ReqMark + "</span>");
        sb.Append("<div class=\"hr-radios\" role=\"radiogroup\" aria-labelledby=\"lblLeaveType\">");
        bool known = false;
        foreach (string[] lt in LeaveTypes)
        {
            bool chk = string.Equals(lt[0], lvType, StringComparison.OrdinalIgnoreCase);
            if (chk) known = true;
            sb.Append(LvTypeRadio(lt[0], lt[1], chk));
        }
        if (!known && !string.IsNullOrEmpty(lvType))   // legacy value saved on an old draft
            sb.Append(LvTypeRadio(lvType, LeaveTypeLabel(lvType), true));
        sb.Append("</div><div class=\"hr-error lf-err\" role=\"alert\"></div></div>");

        sb.Append(Fld("leaveFrom", "First day", true, InputHtml("leaveFrom", "date", lvFrom, ""), ""));
        sb.Append(Fld("leaveTo", "Last day", true, InputHtml("leaveTo", "date", lvTo, ""), ""));
        sb.Append("<div class=\"hr-full hr-hint lf-days\" id=\"leaveSummary\" aria-live=\"polite\"></div>");
        sb.Append("<div class=\"hr-full\">");
        sb.Append(Fld("substituteArrangement", "Who will cover your duties", true,
            TextareaHtml("substituteArrangement", subst, "Name and title of the colleague, and what they will handle"), ""));
        sb.Append("</div>");
        sb.Append("<div class=\"hr-full\">");
        string supHint = "";
        if (app == null && !string.IsNullOrEmpty(prof.SupName) && !supFound)
            supHint = Enc(prof.SupName) + " cannot approve leave online yet. Choose your Head of Department.";
        sb.Append(Fld("supUsername", "Head of Department or supervisor", true,
            "<select id=\"supUsername\" class=\"hr-select\"><option value=\"\">Select the person who approves your leave</option>" + supOptions + "</select>",
            supHint));
        sb.Append("</div>");
        sb.Append("</div>");

        // Employee details
        sb.Append("<div class=\"lf-group\">Employee details</div>");
        sb.Append("<div class=\"hr-form hr-form--3\">");
        sb.Append(Fld("empName", "Full name", true, InputHtml("empName", "text", empName, ""), ""));
        sb.Append(Fld("empCode", "Staff number", false, InputHtml("empCode", "text", empCode, ""), ""));
        sb.Append(Fld("mobileContact", "Mobile number", true, InputHtml("mobileContact", "tel", mobile, ""), ""));
        sb.Append(Fld("empDept", "Faculty, department or section", true, InputHtml("empDept", "text", dept, ""), ""));
        sb.Append(Fld("positionHeld", "Position held", false, InputHtml("positionHeld", "text", position, ""), ""));
        sb.Append(Fld("officeLocation", "Office location", false, InputHtml("officeLocation", "text", office, "Building and room"), ""));
        sb.Append("</div>");

        // While on leave
        sb.Append("<div class=\"lf-group\">While on leave</div>");
        sb.Append("<div class=\"hr-form hr-form--3\">");
        sb.Append("<div class=\"hr-full\">");
        sb.Append(Fld("residenceAddress", "Where you will stay", false,
            TextareaHtml("residenceAddress", address, "Village or town, district"), ""));
        sb.Append("</div>");
        sb.Append(Fld("nokName", "Next of kin", false, InputHtml("nokName", "text", nokName, "Name and relationship"), ""));
        sb.Append(Fld("nokMobile", "Next of kin phone", false, InputHtml("nokMobile", "tel", nokMob, ""), ""));
        sb.Append(Fld("nokAddress", "Next of kin address", false, InputHtml("nokAddress", "text", nokAddr, ""), ""));
        sb.Append("</div>");

        sb.Append("</div></div>"); // body + card
        return sb.ToString();
    }

    private static string LvTypeRadio(string value, string label, bool chk)
    {
        string v = HttpUtility.HtmlAttributeEncode(value);
        return string.Format(
            "<label for=\"lt_{0}\"><input type=\"radio\" name=\"leaveTypeRadio\" id=\"lt_{0}\" value=\"{0}\"{2} />{1}</label>",
            v, HttpUtility.HtmlEncode(label), chk ? " checked=\"checked\"" : "");
    }

    private static string BuildSection1Readonly(Dictionary<string, object> app)
    {
        string status = S(app, "status");
        string badge  = status == "CANCELLED" ? Badge("neutral", "Cancelled") : Badge("ok", "Submitted");
        int nd = N(app, "num_days");
        string supDisp = S(app, "supervisor_name");
        if (string.IsNullOrEmpty(supDisp)) supDisp = S(app, "supervisor_username");

        StringBuilder sb = new StringBuilder();
        sb.Append("<div class=\"hr-card lf-sec\">");
        sb.Append(SectionHead(1, badge));
        sb.Append("<div class=\"hr-card__body\">");

        sb.Append("<div class=\"lf-group\">Leave requested</div><dl class=\"hr-dl\">");
        sb.Append(Dl("Type of leave", LeaveTypeLabel(S(app, "leave_type"))));
        sb.Append(Dl("First day", LongDate(app, "leave_from")));
        sb.Append(Dl("Last day",  LongDate(app, "leave_to")));
        sb.Append(Dl("Number of days", nd > 0 ? nd + (nd == 1 ? " day" : " days") : ""));
        sb.Append(Dl("Head of Department or supervisor", supDisp));
        sb.Append(Dl("Submitted", FormatDateTime(app, "employee_submitted_at")));
        sb.Append(DlWide("Cover arrangement", S(app, "substitute_arrangement")));
        sb.Append("</dl>");

        sb.Append("<div class=\"lf-group\">Employee details</div><dl class=\"hr-dl\">");
        sb.Append(Dl("Full name",       S(app, "emp_name")));
        sb.Append(Dl("Staff number",    S(app, "emp_code")));
        sb.Append(Dl("Department",      S(app, "faculty_dept")));
        sb.Append(Dl("Position held",   S(app, "position_held")));
        sb.Append(Dl("Office location", S(app, "office_location")));
        sb.Append(Dl("Mobile number",   S(app, "mobile_contact")));
        sb.Append("</dl>");

        sb.Append("<div class=\"lf-group\">While on leave</div><dl class=\"hr-dl\">");
        sb.Append(Dl("Next of kin",         S(app, "nok_name")));
        sb.Append(Dl("Next of kin phone",   S(app, "nok_mobile")));
        sb.Append(Dl("Next of kin address", S(app, "nok_address")));
        sb.Append(DlWide("Staying at",      S(app, "residence_address")));
        sb.Append("</dl>");

        sb.Append("</div></div>");
        return sb.ToString();
    }

    // One dt/dd pair of a definition list. Empty values read "Not recorded".
    private static string Dl(string label, string value)
    {
        bool empty = string.IsNullOrWhiteSpace(value);
        return "<div><dt>" + HttpUtility.HtmlEncode(label) + "</dt><dd" + (empty ? " class=\"hr-muted\"" : "") + ">" +
               (empty ? "Not recorded" : Multiline(value.Trim())) + "</dd></div>";
    }

    private static string DlWide(string label, string value)
    {
        return Dl(label, value).Replace("<div>", "<div class=\"lf-wide\">");
    }

    // Field wrapper: label + control + optional hint + inline error slot.
    private static string Fld(string id, string label, bool required, string control, string hintHtml)
    {
        return "<div class=\"hr-field\" id=\"fld_" + id + "\">" +
               "<label class=\"hr-label\" for=\"" + id + "\">" + HttpUtility.HtmlEncode(label) + (required ? ReqMark : "") + "</label>" +
               control +
               (string.IsNullOrEmpty(hintHtml) ? "" : "<div class=\"hr-hint\">" + hintHtml + "</div>") +
               "<div class=\"hr-error lf-err\" role=\"alert\"></div></div>";
    }

    private static string InputHtml(string id, string type, string value, string placeholder)
    {
        return string.Format("<input type=\"{0}\" id=\"{1}\" class=\"hr-input\" value=\"{2}\" placeholder=\"{3}\" maxlength=\"250\" />",
            type, id, HttpUtility.HtmlAttributeEncode(value ?? ""), HttpUtility.HtmlAttributeEncode(placeholder ?? ""));
    }

    private static string TextareaHtml(string id, string value, string placeholder)
    {
        return string.Format("<textarea id=\"{0}\" class=\"hr-textarea\" placeholder=\"{1}\" rows=\"2\">{2}</textarea>",
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
    //  SECTIONS 2 TO 4
    // ══════════════════════════════════════════════════════════════════════════

    private static string SectionLocked(int n, string reason)
    {
        return "<div class=\"hr-card lf-sec lf-sec--locked\">" + SectionHead(n, "") +
               "<div class=\"hr-card__body hr-muted\">" + HttpUtility.HtmlEncode(reason) + "</div></div>";
    }

    private static string SectionWaiting(int n, string badge, string line)
    {
        return "<div class=\"hr-card lf-sec lf-sec--now\">" + SectionHead(n, Badge("info", badge)) +
               "<div class=\"hr-card__body\">" + HttpUtility.HtmlEncode(HrExport.Clean(line)) + "</div></div>";
    }

    private static string BuildSection2Readonly(Dictionary<string, object> app)
    {
        bool approved = S(app, "hod_action") == "APPROVED";
        StringBuilder sb = new StringBuilder();
        sb.Append("<div class=\"hr-card lf-sec\">");
        sb.Append(SectionHead(2, approved ? Badge("ok", "Approved") : Badge("bad", "Declined")));
        sb.Append("<div class=\"hr-card__body\"><dl class=\"hr-dl\">");
        sb.Append(Dl("Decision", approved ? "Approved" : "Declined"));
        sb.Append(Dl("By", S(app, "hod_actor_name")));
        sb.Append(Dl("Date", FormatDate(app, "hod_action_at")));
        if (approved)
        {
            sb.Append(DlWide("Duties handed over to", S(app, "hod_handover_to")));
            if (!string.IsNullOrEmpty(S(app, "hod_notes"))) sb.Append(DlWide("Remarks", S(app, "hod_notes")));
        }
        else
            sb.Append(DlWide("Reason", S(app, "hod_notes")));
        sb.Append("</dl></div></div>");
        return sb.ToString();
    }

    private static string BuildSection3Readonly(Dictionary<string, object> app)
    {
        bool approved = S(app, "hr_action") == "APPROVED";
        StringBuilder sb = new StringBuilder();
        sb.Append("<div class=\"hr-card lf-sec\">");
        sb.Append(SectionHead(3, approved ? Badge("ok", "Approved") : Badge("bad", "Declined")));
        sb.Append("<div class=\"hr-card__body\"><dl class=\"hr-dl\">");
        sb.Append(Dl("Decision", approved ? "Approved" : "Declined"));
        sb.Append(Dl("By", S(app, "hr_actor_name")));
        sb.Append(Dl("Date", FormatDate(app, "hr_action_at")));
        if (approved)
        {
            sb.Append(Dl("Leave from", FormatDate(app, "hr_effective_from")));
            sb.Append(Dl("Leave to",   FormatDate(app, "hr_effective_to")));
        }
        string hrNotes = S(app, "hr_notes");
        if (!string.IsNullOrEmpty(hrNotes)) sb.Append(DlWide(approved ? "HR notes" : "Reason", hrNotes));
        sb.Append("</dl></div></div>");
        return sb.ToString();
    }

    private static string BuildSection4Readonly(Dictionary<string, object> app)
    {
        string vcDec   = S(app, "vc_decision");
        bool granted   = (vcDec == "GRANTED");
        bool postponed = (vcDec == "POSTPONED");
        string label   = granted ? "Granted" : (postponed ? "Postponed" : "Not granted");
        string kind    = granted ? "ok" : (postponed ? "warn" : "bad");
        int accum = N(app, "vc_accumulated_days");
        int taken = N(app, "vc_days_taken");

        StringBuilder sb = new StringBuilder();
        sb.Append("<div class=\"hr-card lf-sec\">");
        sb.Append(SectionHead(4, Badge(kind, label)));
        sb.Append("<div class=\"hr-card__body\"><dl class=\"hr-dl\">");
        sb.Append(Dl("Decision", label));
        sb.Append(Dl("By", S(app, "vc_actor_name")));
        sb.Append(Dl("Date", FormatDate(app, "vc_action_at")));
        if (granted)
        {
            sb.Append(Dl("Accumulated leave days", accum > 0 ? accum.ToString() : ""));
            sb.Append(Dl("Days granted", taken > 0 ? taken.ToString() : ""));
        }
        string vcReason = S(app, "vc_reason");
        if (!string.IsNullOrEmpty(vcReason)) sb.Append(DlWide(granted ? "Remarks" : "Reason", vcReason));
        sb.Append("</dl></div></div>");
        return sb.ToString();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  ACTION BAR
    // ══════════════════════════════════════════════════════════════════════════

    private static string BuildActionBar(string status, bool isAdmin, bool isHr,
        bool canEditDraft, bool canActHod, bool canActHr, bool canActVc)
    {
        StringBuilder sb = new StringBuilder();

        if (canEditDraft)
            sb.Append(DraftButtons());
        if (canActHod)
        {
            sb.Append("<button type=\"button\" class=\"hr-btn hr-btn--primary\" onclick=\"openHodApprove()\">Approve and forward to HR</button>");
            sb.Append("<button type=\"button\" class=\"hr-btn hr-btn--danger\" onclick=\"openModal('modalHodDecline')\">Decline</button>");
        }
        if (canActHr)
        {
            sb.Append("<button type=\"button\" class=\"hr-btn hr-btn--primary\" onclick=\"openHrApprove()\">Approve and forward to the Vice Chancellor</button>");
            sb.Append("<button type=\"button\" class=\"hr-btn hr-btn--danger\" onclick=\"openModal('modalHrDecline')\">Decline</button>");
        }
        if (canActVc)
            sb.Append("<button type=\"button\" class=\"hr-btn hr-btn--primary\" onclick=\"openVcDecision('')\">Record decision</button>");

        if (!canEditDraft && !canActHod && !canActHr && !canActVc)
            sb.AppendFormat("<span class=\"lf-note\">{0}</span>", HttpUtility.HtmlEncode(StatusNote(status)));

        // Cancel for admin/HR
        if ((isAdmin || isHr) && status != "CANCELLED" && status != "VC_GRANTED")
        {
            sb.Append("<span class=\"hr-spacer\"></span>");
            sb.Append("<button type=\"button\" class=\"hr-btn hr-btn--danger\" onclick=\"cancelAppForm()\">Cancel application</button>");
        }
        return sb.ToString();
    }

    private static string StatusNote(string status)
    {
        switch (status)
        {
            case "SUBMITTED":      return "Submitted. Awaiting the Head of Department.";
            case "HOD_APPROVED":   return "Approved by the Head of Department. Awaiting HR.";
            case "HOD_DECLINED":   return "Declined by the Head of Department.";
            case "HR_APPROVED":    return "Approved by HR. Awaiting the Vice Chancellor.";
            case "HR_DECLINED":    return "Declined by HR.";
            case "VC_GRANTED":     return "Leave granted by the Vice Chancellor.";
            case "VC_NOT_GRANTED": return "Leave not granted by the Vice Chancellor.";
            case "VC_POSTPONED":   return "Leave postponed by the Vice Chancellor.";
            case "CANCELLED":      return "This application has been cancelled.";
            case "DRAFT":          return "Draft. Only the applicant can edit it.";
            default:               return "";
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  HISTORY (audit trail)
    // ══════════════════════════════════════════════════════════════════════════

    private string BuildAuditTrail(int appId)
    {
        StringBuilder rows = new StringBuilder();
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr()))
            {
                conn.Open();
                const string sql = @"
                    SELECT action_code, actor_name, actor_role, remarks, created_at
                    FROM hrm_leave_audit WHERE application_id=@id ORDER BY created_at ASC";
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", appId);
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            string remarks = dr["remarks"] == DBNull.Value ? "" : dr["remarks"].ToString();
                            DateTime when  = Convert.ToDateTime(dr["created_at"]);
                            string role    = RoleWords(dr["actor_role"].ToString());
                            rows.Append("<tr><td style=\"white-space:nowrap\">").Append(when.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture)).Append("</td>")
                                .Append("<td>").Append(HttpUtility.HtmlEncode(AuditLabel(dr["action_code"].ToString()))).Append("</td>")
                                .Append("<td>").Append(Enc(dr["actor_name"].ToString()))
                                .Append(role == "" ? "" : "<span class=\"hr-sub\">" + Enc(role) + "</span>").Append("</td>")
                                .Append("<td>").Append(Enc(remarks)).Append("</td></tr>");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning("LeaveApplicationForm history: " + ex.Message);
        }
        if (rows.Length == 0) return "";
        return "<div class=\"hr-card\"><div class=\"hr-card__head\"><div class=\"hr-card__title\">History</div></div>" +
               "<div class=\"hr-table-wrap\"><table class=\"hr-table\"><thead><tr><th>Date</th><th>Action</th><th>By</th><th>Remarks</th></tr></thead><tbody>" +
               rows + "</tbody></table></div></div>";
    }

    private static string RoleWords(string role)
    {
        switch ((role ?? "").Trim().ToLowerInvariant())
        {
            case "employee":                 return "Employee";
            case "hod / supervisor":         return "Head of Department";
            case "hr department":            return "HR";
            case "vice chancellor's office": return "Vice Chancellor";
            case "admin":                    return "Administrator";
            case "hr_manager":               return "HR";
            case "vc":                       return "Vice Chancellor";
            default:                         return "";
        }
    }

    private static string AuditLabel(string code)
    {
        switch (code)
        {
            case "DRAFT_SAVED":    return "Draft saved";
            case "SUBMITTED":      return "Submitted";
            case "HOD_APPROVED":   return "Approved by the Head of Department";
            case "HOD_DECLINED":   return "Declined by the Head of Department";
            case "HR_APPROVED":    return "Approved by HR";
            case "HR_DECLINED":    return "Declined by HR";
            case "VC_GRANTED":     return "Granted by the Vice Chancellor";
            case "VC_NOT_GRANTED": return "Not granted by the Vice Chancellor";
            case "VC_POSTPONED":   return "Postponed by the Vice Chancellor";
            case "CANCELLED":      return "Cancelled";
            default:               return code;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PRINT (HrDocument)
    // ══════════════════════════════════════════════════════════════════════════

    private void RenderPrint(int appId, string username, bool isAdmin, bool isHr, bool isVc, bool isHod)
    {
        Dictionary<string, object> app = LoadApp(appId);
        string html;
        if (app == null || !CanView(app, username, isAdmin, isHr, isVc, isHod))
        {
            HrDocument.Options o0 = new HrDocument.Options();
            o0.BackUrl = ResolveUrl("~/COOPERP/NewScreens/LeaveApplications.aspx");
            html = HrDocument.Page("Leave application",
                HrDocument.Paragraph(app == null ? "This application was not found." : "You do not have permission to view this application."), o0);
        }
        else
            html = HrDocument.Page("Leave application form", PrintBody(app, appId), PrintOptions(app, appId));

        html = html.Replace("</body>", "<script>window.onload=function(){window.print();};</script></body>");
        Response.Clear();
        Response.ContentType = "text/html; charset=utf-8";
        Response.Write(html);
        Response.End();
    }

    private HrDocument.Options PrintOptions(Dictionary<string, object> app, int appId)
    {
        HrDocument.Options o = new HrDocument.Options();
        o.Reference = "Ref: " + Ref(appId) + "   |   Status: " + StatusLabel(S(app, "status"));
        o.BackUrl = ResolveUrl("~/COOPERP/NewScreens/LeaveApplicationForm.aspx") + "?id=" + appId;
        return o;
    }

    private static string PrintBody(Dictionary<string, object> app, int appId)
    {
        string status = S(app, "status");
        int nd = N(app, "num_days");
        string sup = S(app, "supervisor_name");
        if (sup == "") sup = S(app, "supervisor_username");
        StringBuilder b = new StringBuilder();

        // Section 1
        b.Append(HrDocument.Heading("Section 1: Leave request"));
        b.Append(HrDocument.Meta(
            "Full name", S(app, "emp_name"),               "Staff number", S(app, "emp_code"),
            "Department", S(app, "faculty_dept"),          "Position held", S(app, "position_held"),
            "Office location", S(app, "office_location"),  "Mobile number", S(app, "mobile_contact"),
            "Type of leave", LeaveTypeLabel(S(app, "leave_type")), "Number of days", nd > 0 ? nd.ToString() : "",
            "First day", FormatDate(app, "leave_from"),    "Last day", FormatDate(app, "leave_to"),
            "Next of kin", S(app, "nok_name"),             "Next of kin phone", S(app, "nok_mobile"),
            "Staying at", S(app, "residence_address"),     "Next of kin address", S(app, "nok_address"),
            "Head of Department", sup,                     "Submitted", FormatDateTime(app, "employee_submitted_at")));
        b.Append("<div class=\"box\"><strong>Cover arrangement:</strong> ").Append(HrDocument.Multiline(S(app, "substitute_arrangement"))).Append("</div>");
        b.Append(HrDocument.Signatures("Employee", S(app, "emp_name"),
            Signed("Submitted", S(app, "employee_submitted_at") == "" ? null : app["employee_submitted_at"])));

        // Section 2
        string hodAction = S(app, "hod_action");
        b.Append(HrDocument.Heading("Section 2: Head of Department"));
        b.Append(HrDocument.Meta(
            "Decision", hodAction == "APPROVED" ? "Approved" : hodAction == "DECLINED" ? "Declined" : "",
            "Date", FormatDate(app, "hod_action_at"),
            "Duties handed over to", S(app, "hod_handover_to"),
            hodAction == "DECLINED" ? "Reason" : "Remarks", S(app, "hod_notes")));
        b.Append(HrDocument.Signatures("Head of Department", S(app, "hod_actor_name"),
            Signed(hodAction == "APPROVED" ? "Approved" : "Declined", hodAction == "" ? null : Obj(app, "hod_action_at"))));

        // Section 3
        string hrAction = S(app, "hr_action");
        b.Append(HrDocument.Heading("Section 3: Human Resources"));
        b.Append(HrDocument.Meta(
            "Decision", hrAction == "APPROVED" ? "Approved" : hrAction == "DECLINED" ? "Declined" : "",
            "Date", FormatDate(app, "hr_action_at"),
            "Leave from", FormatDate(app, "hr_effective_from"),
            "Leave to", FormatDate(app, "hr_effective_to"),
            hrAction == "DECLINED" ? "Reason" : "HR notes", S(app, "hr_notes")));
        b.Append(HrDocument.Signatures("Human Resources", S(app, "hr_actor_name"),
            Signed(hrAction == "APPROVED" ? "Approved" : "Declined", hrAction == "" ? null : Obj(app, "hr_action_at"))));

        // Section 4
        string vc = S(app, "vc_decision");
        string vcLabel = vc == "GRANTED" ? "Granted" : vc == "NOT_GRANTED" ? "Not granted" : vc == "POSTPONED" ? "Postponed" : "";
        int accum = N(app, "vc_accumulated_days"), taken = N(app, "vc_days_taken");
        b.Append(HrDocument.Heading("Section 4: Vice Chancellor"));
        b.Append(HrDocument.Meta(
            "Decision", vcLabel,
            "Date", FormatDate(app, "vc_action_at"),
            "Accumulated leave days", accum > 0 ? accum.ToString() : "",
            "Days granted", taken > 0 ? taken.ToString() : "",
            vc == "GRANTED" || vc == "" ? "Remarks" : "Reason", S(app, "vc_reason")));
        b.Append(HrDocument.Signatures("Vice Chancellor", S(app, "vc_actor_name"),
            Signed(vcLabel, vc == "" ? null : Obj(app, "vc_action_at"))));

        if (status == "CANCELLED")
            b.Append(HrDocument.Paragraph("This application was cancelled."));
        b.Append(HrDocument.Paragraph("On return from leave, report to the Human Resource Office before resuming duties."));
        return b.ToString();
    }

    private static object Obj(Dictionary<string, object> d, string key) { object v; return d.TryGetValue(key, out v) ? v : null; }

    private static string Signed(string what, object when)
    {
        if (when == null || when is DBNull || string.IsNullOrEmpty(what)) return "";
        DateTime dt;
        if (when is DateTime) dt = (DateTime)when;
        else if (!DateTime.TryParse(when.ToString(), out dt)) return "";
        return "Signed electronically on " + dt.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
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
                case "hod_approve": AjaxHodApprove(username, screenName, ip, isAdmin); break;
                case "hod_decline": AjaxHodDecline(username, screenName, ip, isAdmin); break;
                case "hr_approve":  AjaxHrApprove(username, screenName, ip, isAdmin, isHr);  break;
                case "hr_decline":  AjaxHrDecline(username, screenName, ip, isAdmin, isHr);  break;
                case "vc_decision": AjaxVcDecision(username, screenName, ip, isAdmin, isVc); break;
                case "cancel":      AjaxCancel(username, screenName, roleCode, ip, isAdmin, isHr); break;
                default: Err("Unknown action."); break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("LeaveApplicationForm " + action + ": " + ex);
            Response.Clear();
            Err("The change could not be saved. Try again, and contact MIS if it keeps happening.");
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
        { Err("Enter your full name."); return; }
        if (string.IsNullOrEmpty(lvType))
        { Err("Choose the type of leave."); return; }

        DateTime dtFrom, dtTo;
        if (!DateTime.TryParse(lvFrom, out dtFrom) || !DateTime.TryParse(lvTo, out dtTo))
        { Err("Enter valid first and last days."); return; }
        if (dtTo < dtFrom)
        { Err("The last day must be on or after the first day."); return; }

        int numDays = FormInt("num_days");
        if (numDays <= 0) numDays = (int)Math.Round((dtTo - dtFrom).TotalDays) + 1;

        string supUser = FormStr("supervisor_username");
        string supName = FormStr("supervisor_name");
        if (submit && string.IsNullOrEmpty(supUser))
        { Err("Choose your Head of Department or supervisor."); return; }
        if (submit && !isAdmin && string.Equals(supUser.Trim(), username.Trim(), StringComparison.OrdinalIgnoreCase))
        { Err("You cannot approve your own leave. Choose your Head of Department or supervisor."); return; }

        string newStatus = submit ? "SUBMITTED" : "DRAFT";

        using (MySqlConnection conn = new MySqlConnection(ConnStr()))
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
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    BindDraftParams(cmd, empName, numDays, dtFrom, dtTo, newStatus, supUser, supName, username, submit);
                    cmd.ExecuteNonQuery();
                    appId = (int)cmd.LastInsertedId;
                }
            }
            else
            {
                string existStatus = "", existCreator = "";
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT status,created_by FROM hrm_leave_applications WHERE id=@id AND is_active=1", conn))
                {
                    cmd.Parameters.AddWithValue("@id", appId);
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read()) { Err("This application was not found."); return; }
                        existStatus = dr[0].ToString(); existCreator = dr[1].ToString();
                    }
                }
                if (existStatus != "DRAFT") { Err("Only a draft can be edited. This application has already been submitted."); return; }
                if (!isAdmin && !string.Equals(existCreator, username, StringComparison.OrdinalIgnoreCase))
                { Err("Only the applicant can edit this draft."); return; }

                const string upd = @"
                    UPDATE hrm_leave_applications SET
                        emp_name=@en,emp_code=@ec,faculty_dept=@fd,office_location=@ol,position_held=@ph,
                        residence_address=@ra,mobile_contact=@mc,nok_name=@nn,nok_address=@na,nok_mobile=@nm,
                        leave_type=@lt,leave_from=@lf,leave_to=@lt2,num_days=@nd,substitute_arrangement=@sa,
                        supervisor_username=@su,supervisor_name=@sn,status=@st,
                        employee_submitted_at=@sub_at,updated_at=NOW()
                    WHERE id=@id";
                using (MySqlCommand cmd = new MySqlCommand(upd, conn))
                {
                    BindDraftParams(cmd, empName, numDays, dtFrom, dtTo, newStatus, supUser, supName, username, submit);
                    cmd.Parameters.AddWithValue("@id", appId);
                    cmd.ExecuteNonQuery();
                }
            }

            LogAudit(conn, appId,
                submit ? "SUBMITTED" : "DRAFT_SAVED",
                "DRAFT", newStatus, screenName, username, "Employee",
                submit ? "Submitted to the Head of Department." : "Draft saved.", ip);
        }

        Response.Write("{\"ok\":true,\"id\":" + appId + "}");
    }

    private static void BindDraftParams(MySqlCommand cmd, string empName, int numDays,
        DateTime dtFrom, DateTime dtTo, string newStatus,
        string supUser, string supName, string username, bool submit)
    {
        HttpRequest rq = HttpContext.Current.Request;
        cmd.Parameters.AddWithValue("@en",     empName);
        cmd.Parameters.AddWithValue("@ec",     rq.Form["emp_code"] ?? "");
        cmd.Parameters.AddWithValue("@fd",     rq.Form["faculty_dept"] ?? "");
        cmd.Parameters.AddWithValue("@ol",     rq.Form["office_location"] ?? "");
        cmd.Parameters.AddWithValue("@ph",     rq.Form["position_held"] ?? "");
        cmd.Parameters.AddWithValue("@ra",     rq.Form["residence_address"] ?? "");
        cmd.Parameters.AddWithValue("@mc",     rq.Form["mobile_contact"] ?? "");
        cmd.Parameters.AddWithValue("@nn",     rq.Form["nok_name"] ?? "");
        cmd.Parameters.AddWithValue("@na",     rq.Form["nok_address"] ?? "");
        cmd.Parameters.AddWithValue("@nm",     rq.Form["nok_mobile"] ?? "");
        cmd.Parameters.AddWithValue("@lt",     rq.Form["leave_type"] ?? "annual");
        cmd.Parameters.AddWithValue("@lf",     dtFrom.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@lt2",    dtTo.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@nd",     numDays);
        cmd.Parameters.AddWithValue("@sa",     rq.Form["substitute_arrangement"] ?? "");
        cmd.Parameters.AddWithValue("@su",     supUser);
        cmd.Parameters.AddWithValue("@sn",     supName);
        cmd.Parameters.AddWithValue("@st",     newStatus);
        cmd.Parameters.AddWithValue("@sub_at", submit ? (object)DateTime.Now : DBNull.Value);
        cmd.Parameters.AddWithValue("@cb",     username);
    }

    // ── HOD Approve ────────────────────────────────────────────────────────────

    private void AjaxHodApprove(string username, string screenName, string ip, bool isAdmin)
    {
        // No role gate here: the assigned Supervisor / HOD (whatever their role) or an
        // admin may act, enforced by the supervisor_username check below.
        int    appId      = FormInt("id");
        string handoverTo = FormStr("hod_handover_to");
        string notes      = FormStr("hod_notes");

        using (MySqlConnection conn = new MySqlConnection(ConnStr()))
        {
            conn.Open();
            string status = "", supUser = "";
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT status,supervisor_username FROM hrm_leave_applications WHERE id=@id AND is_active=1", conn))
            {
                cmd.Parameters.AddWithValue("@id", appId);
                using (MySqlDataReader dr = cmd.ExecuteReader())
                {
                    if (!dr.Read()) { Err("This application was not found."); return; }
                    status = dr[0].ToString(); supUser = dr[1].ToString();
                }
            }
            if (status != "SUBMITTED") { Err("This application is no longer awaiting the Head of Department."); return; }
            if (!isAdmin && !string.Equals(supUser, username, StringComparison.OrdinalIgnoreCase))
            { Err("Only the Head of Department chosen on this application can approve it."); return; }

            using (MySqlCommand cmd = new MySqlCommand(@"
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

    private void AjaxHodDecline(string username, string screenName, string ip, bool isAdmin)
    {
        // No role gate here: the assigned Supervisor / HOD (whatever their role) or an
        // admin may act, enforced by the supervisor_username check below.
        int    appId  = FormInt("id");
        string reason = FormStr("reason");
        if (string.IsNullOrEmpty(reason)) { Err("Give a reason for declining."); return; }

        using (MySqlConnection conn = new MySqlConnection(ConnStr()))
        {
            conn.Open();
            string status = "", supUser = "";
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT status,supervisor_username FROM hrm_leave_applications WHERE id=@id AND is_active=1", conn))
            {
                cmd.Parameters.AddWithValue("@id", appId);
                using (MySqlDataReader dr = cmd.ExecuteReader())
                {
                    if (!dr.Read()) { Err("This application was not found."); return; }
                    status = dr[0].ToString(); supUser = dr[1].ToString();
                }
            }
            if (status != "SUBMITTED") { Err("This application is no longer awaiting the Head of Department."); return; }
            if (!isAdmin && !string.Equals(supUser, username, StringComparison.OrdinalIgnoreCase))
            { Err("Only the Head of Department chosen on this application can decline it."); return; }

            using (MySqlCommand cmd = new MySqlCommand(@"
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
        if (!isAdmin && !isHr) { Err("Only HR can approve at this stage."); return; }
        int    appId   = FormInt("id");
        string effFrom = FormStr("hr_effective_from");
        string effTo   = FormStr("hr_effective_to");
        string notes   = FormStr("hr_notes");

        DateTime dtFrom, dtTo;
        if (!DateTime.TryParse(effFrom, out dtFrom) || !DateTime.TryParse(effTo, out dtTo))
        { Err("Enter both leave dates."); return; }

        using (MySqlConnection conn = new MySqlConnection(ConnStr()))
        {
            conn.Open();
            string status = GetStatus(conn, appId);
            if (status == null) { Err("This application was not found."); return; }
            if (status != "HOD_APPROVED") { Err("This application is not awaiting HR."); return; }

            using (MySqlCommand cmd = new MySqlCommand(@"
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
        if (!isAdmin && !isHr) { Err("Only HR can decline at this stage."); return; }
        int    appId  = FormInt("id");
        string reason = FormStr("reason");
        if (string.IsNullOrEmpty(reason)) { Err("Give a reason for declining."); return; }

        using (MySqlConnection conn = new MySqlConnection(ConnStr()))
        {
            conn.Open();
            string status = GetStatus(conn, appId);
            if (status == null) { Err("This application was not found."); return; }
            if (status != "HOD_APPROVED") { Err("This application is not awaiting HR."); return; }

            using (MySqlCommand cmd = new MySqlCommand(@"
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

    private void AjaxVcDecision(string username, string screenName, string ip, bool isAdmin, bool isVc)
    {
        if (!isAdmin && !isVc) { Err("Only the Vice Chancellor can decide at this stage."); return; }
        int    appId    = FormInt("id");
        string decision = FormStr("vc_decision").ToUpper();
        string reason   = FormStr("vc_reason");
        int    accum    = FormInt("vc_accumulated_days");
        int    taken    = FormInt("vc_days_taken");

        string[] valid = { "GRANTED", "NOT_GRANTED", "POSTPONED" };
        if (Array.IndexOf(valid, decision) < 0) { Err("Choose Grant, Not grant or Postpone."); return; }
        if (decision != "GRANTED" && string.IsNullOrEmpty(reason))
        { Err("Give a reason for this decision."); return; }

        string newStatus = "VC_" + decision;

        using (MySqlConnection conn = new MySqlConnection(ConnStr()))
        {
            conn.Open();
            string status = GetStatus(conn, appId);
            if (status == null) { Err("This application was not found."); return; }
            if (status != "HR_APPROVED") { Err("This application is not awaiting the Vice Chancellor."); return; }

            using (MySqlCommand cmd = new MySqlCommand(@"
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
        if (!isAdmin && !isHr) { Err("Only HR can cancel an application."); return; }
        int appId = FormInt("id");

        using (MySqlConnection conn = new MySqlConnection(ConnStr()))
        {
            conn.Open();
            string status = GetStatus(conn, appId);
            if (status == null) { Err("This application was not found."); return; }
            if (status == "CANCELLED") { Err("This application is already cancelled."); return; }

            using (MySqlCommand cmd = new MySqlCommand(
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
    // The staff login column is hrm_employee.usernames.
    private List<string[]> LoadSupervisors()
    {
        List<string[]> list = new List<string[]>();
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr()))
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
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                using (MySqlDataReader dr = cmd.ExecuteReader())
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
        catch (Exception ex) { System.Diagnostics.Trace.TraceWarning("LeaveApplicationForm approvers: " + ex.Message); }
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
    // name that is posted as supervisor_name.
    private static string BuildSupervisorOptions(List<string[]> list, string selUser, string selName,
        bool forceSelected, string currentUser, out bool found)
    {
        found = false;
        string sel = (selUser ?? "").Trim();
        string me  = (currentUser ?? "").Trim();
        StringBuilder sb = new StringBuilder();
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
            selected ? " selected=\"selected\"" : "",
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
    // EMP_CODE, else emp_email, each only when it identifies exactly one employee.
    private StaffProfile LoadStaffProfile(string username)
    {
        StaffProfile p = new StaffProfile();
        string u = (username ?? "").Trim();
        if (u.Length == 0) return p;
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr()))
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
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", empId);
                    using (MySqlDataReader dr = cmd.ExecuteReader())
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
                    using (MySqlCommand cmd = new MySqlCommand(@"
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
        catch (Exception ex) { System.Diagnostics.Trace.TraceWarning("LeaveApplicationForm profile: " + ex.Message); }
        return p;
    }

    private static int UniqueEmpId(MySqlConnection conn, string where, string u)
    {
        using (MySqlCommand cmd = new MySqlCommand("SELECT empID FROM hrm_employee WHERE " + where + " LIMIT 2", conn))
        {
            cmd.Parameters.AddWithValue("@u", u);
            int id = 0, n = 0;
            using (MySqlDataReader dr = cmd.ExecuteReader())
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
        StringBuilder sb = new StringBuilder("\"");
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
            using (MySqlConnection conn = new MySqlConnection(ConnStr()))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT * FROM hrm_leave_applications WHERE id=@id AND is_active=1", conn))
                {
                    cmd.Parameters.AddWithValue("@id", appId);
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (!dr.Read()) return null;
                        Dictionary<string, object> d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < dr.FieldCount; i++)
                            d[dr.GetName(i)] = dr.IsDBNull(i) ? null : dr.GetValue(i);
                        return d;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("LeaveApplicationForm load: " + ex);
            return null;
        }
    }

    private static string GetStatus(MySqlConnection conn, int appId)
    {
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT status FROM hrm_leave_applications WHERE id=@id AND is_active=1", conn))
        {
            cmd.Parameters.AddWithValue("@id", appId);
            using (MySqlDataReader dr = cmd.ExecuteReader())
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
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
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
        catch (Exception ex) { System.Diagnostics.Trace.TraceWarning("LeaveApplicationForm audit: " + ex.Message); }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  LABELS
    // ══════════════════════════════════════════════════════════════════════════

    private static string StatusLabel(string status)
    {
        switch (status)
        {
            case "DRAFT":          return "Draft";
            case "SUBMITTED":      return "Awaiting HOD";
            case "HOD_APPROVED":   return "Awaiting HR";
            case "HOD_DECLINED":   return "Declined by HOD";
            case "HR_APPROVED":    return "Awaiting Vice Chancellor";
            case "HR_DECLINED":    return "Declined by HR";
            case "VC_GRANTED":     return "Granted";
            case "VC_NOT_GRANTED": return "Not granted";
            case "VC_POSTPONED":   return "Postponed";
            case "CANCELLED":      return "Cancelled";
            default:               return status;
        }
    }

    private static string StatusBadge(string status)
    {
        string kind;
        switch (status)
        {
            case "SUBMITTED": case "HOD_APPROVED": case "HR_APPROVED": kind = "info"; break;
            case "VC_GRANTED": kind = "ok"; break;
            case "VC_POSTPONED": kind = "warn"; break;
            case "HOD_DECLINED": case "HR_DECLINED": case "VC_NOT_GRANTED": kind = "bad"; break;
            default: kind = "neutral"; break;
        }
        return "<span class=\"hr-badge hr-badge--" + kind + "\">" + HttpUtility.HtmlEncode(StatusLabel(status)) + "</span>";
    }

    private static string LeaveTypeLabel(string type)
    {
        switch (type)
        {
            case "annual":      return "Annual leave";
            case "study":       return "Study leave";
            case "sick":        return "Sick leave";
            case "maternity":   return "Maternity leave";
            case "bereavement": return "Family bereavement";
            default:            return type;
        }
    }

    private static string Ref(int appId) { return "LV-" + appId.ToString("0000"); }

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
        return dt.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
    }
    private static string FormatDateTime(Dictionary<string, object> d, string key)
    {
        object v;
        if (!d.TryGetValue(key, out v) || v == null || v is DBNull) return "";
        DateTime dt; if (v is DateTime) dt = (DateTime)v;
        else if (!DateTime.TryParse(v.ToString(), out dt)) return "";
        return dt.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
    }

    private static string Or(string v, string fallback) { return string.IsNullOrWhiteSpace(v) ? fallback : v.Trim(); }
    private static string Enc(string s) { return HttpUtility.HtmlEncode(HrExport.Clean(s ?? "")); }
    private static string Multiline(string s) { return Enc(s).Replace("\r\n", "<br/>").Replace("\n", "<br/>"); }

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
        ConnectionStringSettings cs = ConfigurationManager.ConnectionStrings["vacConnectionString"];
        if (cs != null && !string.IsNullOrEmpty(cs.ConnectionString)) return cs.ConnectionString;
        cs = ConfigurationManager.ConnectionStrings["DefaultConnection"];
        if (cs != null) return cs.ConnectionString;
        throw new InvalidOperationException("No valid connection string.");
    }
}
