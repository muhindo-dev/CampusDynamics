using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

/// <summary>
/// The official appraisal form, printed through HrDocument (one letterhead, light table headers,
/// one HR signature block). Section headings are the same as on AppraisalView.
/// Opens as a preview; the user prints from the toolbar.
/// </summary>
public partial class COOPERP_NewScreens_AppraisalPrint : System.Web.UI.Page
{
    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    private int QsRecord
    {
        get { int v; return int.TryParse(Request.QueryString["rid"] ?? "0", out v) && v > 0 ? v : 0; }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        // Standalone page (no SidebarMaster): signed-in staff who may view the record may print it.
        if (!HrAccess.IsSignedIn()) { HrAccess.RequireHr(false); return; }
        if (QsRecord <= 0) { Response.Redirect("~/COOPERP/NewScreens/AppraisalView.aspx", true); return; }

        HrDocument.Options o = new HrDocument.Options();
        o.BackUrl = "AppraisalView.aspx?rid=" + QsRecord;
        try
        {
            string title;
            string body = BuildBody(QsRecord, o, out title);
            litDocument.Text = HrDocument.Page(title, body, o);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("AppraisalPrint: " + ex);
            litDocument.Text = HrDocument.Page("Staff performance appraisal form",
                HrDocument.Paragraph("The appraisal could not be loaded. Refresh the page or try again later."), o);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  BODY
    // ═══════════════════════════════════════════════════════════════════
    private string BuildBody(int rid, HrDocument.Options o, out string title)
    {
        title = "Staff performance appraisal form";
        DataTable dtRec = LoadMainRecord(rid);
        if (dtRec.Rows.Count == 0) return HrDocument.Paragraph("This appraisal record does not exist.");

        DataRow r = dtRec.Rows[0];
        string recStatus = SafeStr(r["status"]).ToUpper();
        if (recStatus != "COMPLETED" && recStatus != "HR_REVIEWED" && recStatus != "CANCELLED")
            return HrDocument.Paragraph("This appraisal has not reached HR yet.");
        string cat = SafeStr(r["staff_category"]).ToUpper();
        if (cat == "ACADEMIC") title = "Academic staff performance appraisal form";
        else if (cat == "ADMINISTRATIVE") title = "Administrative staff performance appraisal form";
        else if (cat == "SUPPORT") title = "Support staff performance appraisal form";

        o.Reference = SafeStr(r["session_title"]) + "   |   Appraisal period " +
                      Dt(r["period_start"]) + " to " + Dt(r["period_end"]);

        DataTable dtB = Q(
            @"SELECT slot_number, agreed_output, performance_indicators, result_areas, evidence_text,
                     IFNULL(is_na,0) AS is_na, na_reason, self_rating, supervisor_rating, comments
              FROM appraisal_section_b WHERE record_id = @rid ORDER BY slot_number", rid);
        DataTable dtEv = Q(
            "SELECT slot_number, original_name FROM appraisal_evidence WHERE record_id = @rid ORDER BY slot_number, evidence_id", rid);
        DataTable dtC = Q(
            @"SELECT competency_code, competency_name, category_name, rating, is_na, comment,
                     IFNULL(self_rating,0) AS self_rating, IFNULL(supervisor_comment,'') AS supervisor_comment
              FROM appraisal_section_c WHERE record_id = @rid ORDER BY entry_id", rid);
        DataTable dtD = Q(
            "SELECT performance_gap, agreed_action, time_frame FROM appraisal_section_d WHERE record_id = @rid ORDER BY entry_id", rid);
        DataTable dtE = Q(
            "SELECT question_number, question_text, response FROM appraisal_section_e WHERE record_id = @rid ORDER BY question_number", rid);

        StringBuilder h = new StringBuilder();
        h.Append(HrDocument.Paragraph(
            "Staff Performance Appraisal is part of the Performance Management System of Muteesa I Royal University. " +
            "It is used as a management tool for establishing the extent to which set targets within the overall goals of the University are achieved. " +
            "Through the staff performance appraisal, performance gaps and development needs of an individual employee are identified. " +
            "The appraisal process offers an opportunity to the appraisee and appraiser to dialogue and obtain a feedback on performance. " +
            "This therefore, calls for a participatory approach to the appraisal process. " +
            "The appraiser and appraisee are advised to read the detailed guidelines before filling this form."));

        SectionA(h, r, cat);
        int bApplicable = SectionB(h, dtB, dtEv, cat);
        SectionC(h, dtC, cat);
        SectionD(h, dtD);
        SectionE(h, dtE, cat);
        Score(h, r, bApplicable);
        SignOff(h, r);
        HrReview(h, r, cat);

        h.AppendFormat("<div class=\"ref\" style=\"text-align:right;margin-top:16px;\">Record reference APR-{0}</div>", rid);
        return h.ToString();
    }

    private DataTable LoadMainRecord(int rid)
    {
        const string tail =
            @" FROM appraisal_records ar
               INNER JOIN hrm_employee e ON e.empID = ar.employee_id
               LEFT JOIN hrm_employee rev ON rev.empID = ar.reviewer_id
               INNER JOIN appraisal_sessions s ON s.session_id = ar.session_id ";
        try
        {
            return Q(
                @"SELECT ar.*, e.emp_name, e.EMP_CODE,
                         IFNULL(e.emp_email,'') AS emp_email, IFNULL(e.emp_phone,'') AS emp_phone,
                         IFNULL(d.dept_name,'') AS department, IFNULL(j.jobname,'') AS designation,
                         c.contractStart AS contract_start, c.contractEnd AS contract_end,
                         IFNULL(c.contract_type,'') AS contract_type, IFNULL(c.contractStatus,'') AS contractStatus,
                         IFNULL(rev.emp_name,'') AS live_reviewer_name, IFNULL(revj.jobname,'') AS reviewer_designation,
                         s.session_title, s.period_start, s.period_end" + tail +
                @"LEFT JOIN hrm_emp_contracts c ON c.ID = (
                      SELECT c2.ID FROM hrm_emp_contracts c2 WHERE c2.empID = e.empID
                      ORDER BY (CASE WHEN c2.contractStatus='VALID' THEN 0 ELSE 1 END), c2.contractStart DESC LIMIT 1)
                  LEFT JOIN hrm_departments d ON d.ID = c.departmentID
                  LEFT JOIN hrm_jobs j ON j.ID = c.jobID
                  LEFT JOIN hrm_emp_contracts revc ON revc.ID = (
                      SELECT c2.ID FROM hrm_emp_contracts c2 WHERE c2.empID = rev.empID
                      ORDER BY (CASE WHEN c2.contractStatus='VALID' THEN 0 ELSE 1 END), c2.contractStart DESC LIMIT 1)
                  LEFT JOIN hrm_jobs revj ON revj.ID = revc.jobID
                  WHERE ar.record_id = @rid", rid);
        }
        catch
        {
            return Q(
                @"SELECT ar.*, e.emp_name, e.EMP_CODE,
                         IFNULL(e.emp_email,'') AS emp_email, IFNULL(e.emp_phone,'') AS emp_phone,
                         '' AS department, '' AS designation,
                         NULL AS contract_start, NULL AS contract_end, '' AS contract_type, '' AS contractStatus,
                         IFNULL(rev.emp_name,'') AS live_reviewer_name, '' AS reviewer_designation,
                         s.session_title, s.period_start, s.period_end" + tail +
                "WHERE ar.record_id = @rid", rid);
        }
    }

    // ── Section A ──────────────────────────────────────────────────────
    private void SectionA(StringBuilder h, DataRow r, string cat)
    {
        string liveRev  = SafeStr(r["live_reviewer_name"]);
        object reviewDt = r["reviewer_signed_at"] != DBNull.Value ? r["reviewer_signed_at"] : r["supervisor_submitted_at"];
        object submitDt = r["employee_signed_at"] != DBNull.Value ? r["employee_signed_at"] : r["employee_submitted_at"];

        h.Append(HrDocument.Heading("Section A: Personal details"));
        h.Append(HrDocument.Meta(
            "Name", SafeStr(r["emp_name"]),
            "Staff number", SafeStr(r["EMP_CODE"]),
            "Position held", First(SafeStr(r["snap_position"]), SafeStr(r["designation"])),
            "Department", First(SafeStr(r["snap_department"]), SafeStr(r["department"])),
            "Reports to", First(SafeStr(r["snap_reports_to"]), liveRev),
            "Staff category", CategoryWord(cat),
            "Supervisor", First(SafeStr(r["reviewer_name"]), liveRev),
            "Supervisor's title", First(SafeStr(r["reviewer_title"]), SafeStr(r["reviewer_designation"])),
            "Date submitted", Dt(submitDt),
            "Date of review", Dt(reviewDt),
            "Email", SafeStr(r["emp_email"]),
            "Telephone", SafeStr(r["emp_phone"]),
            "Terms of employment", SafeStr(r["contract_type"]),
            "Contract status", Word(SafeStr(r["contractStatus"])),
            "Contract start", Dt(r["contract_start"]),
            "Contract end", Dt(r["contract_end"])));

        List<string[]> blank = new List<string[]>();
        for (int i = 0; i < 4; i++) blank.Add(new string[] { "&nbsp;", "&nbsp;" });
        h.Append("<p style=\"margin:10px 0 2px;font-weight:600;\">Qualifications</p>");
        h.Append(HrDocument.Table(new string[] { "Award and institution", "Date obtained" }, blank, null, null));
    }

    // ── Section B ──────────────────────────────────────────────────────
    private int SectionB(StringBuilder h, DataTable dtB, DataTable dtEv, string cat)
    {
        h.Append(HrDocument.Heading("Section B: Achievements of responsibilities and key performance areas"));
        h.Append(HrDocument.Paragraph("Rating scale: " + RatingLegend(cat) + "."));

        Dictionary<int, List<string>> evBySlot = new Dictionary<int, List<string>>();
        foreach (DataRow ev in dtEv.Rows)
        {
            int slot = SafeInt(ev["slot_number"]);
            if (!evBySlot.ContainsKey(slot)) evBySlot[slot] = new List<string>();
            evBySlot[slot].Add(SafeStr(ev["original_name"]));
        }

        List<string[]> rows = new List<string[]>();
        int n = 0, applicable = 0, selfTotal = 0, supTotal = 0;
        foreach (DataRow b in dtB.Rows)
        {
            string kpa = SafeStr(b["agreed_output"]).Trim();
            bool isNa = SafeInt(b["is_na"]) == 1;
            if (kpa == "" && !isNa) continue;
            n++;
            int slot = SafeInt(b["slot_number"]);
            if (isNa)
            {
                string reason = SafeStr(b["na_reason"]).Trim();
                rows.Add(new string[] { n.ToString(), HrDocument.E(kpa), HrDocument.Multiline(SafeStr(b["performance_indicators"])),
                    "Not applicable" + (reason != "" ? ": " + HrDocument.E(reason) : ""), "", "N/A", "N/A",
                    HrDocument.Multiline(SafeStr(b["comments"])) });
                continue;
            }
            applicable++;
            int selfR = SafeInt(b["self_rating"]), supR = SafeInt(b["supervisor_rating"]);
            if (selfR > 0) selfTotal += selfR;
            if (supR > 0) supTotal += supR;
            string evCell = HrDocument.Multiline(SafeStr(b["evidence_text"]).Trim());
            if (evBySlot.ContainsKey(slot))
                evCell += (evCell != "" ? "<br/>" : "") + "Attached: " + HrDocument.E(string.Join("; ", evBySlot[slot].ToArray()));
            rows.Add(new string[] { n.ToString(), HrDocument.E(kpa), HrDocument.Multiline(SafeStr(b["performance_indicators"])),
                HrDocument.Multiline(SafeStr(b["result_areas"])), evCell,
                selfR > 0 ? selfR.ToString() : "", supR > 0 ? supR.ToString() : "",
                HrDocument.Multiline(SafeStr(b["comments"])) });
        }

        string[] totals = null;
        if (n > 0)
        {
            int max = applicable * 5;
            totals = new string[] { "", "Total, " + applicable + " applicable", "", "", "",
                selfTotal + " of " + max, supTotal + " of " + max, "" };
        }
        h.Append(HrDocument.Table(
            new string[] { "No.", "Responsibility or key performance area", "Expected standard", "Achievement", "Evidence", "Employee", "Supervisor", "Supervisor comment" },
            rows, new bool[] { true, false, false, false, false, true, true, false }, totals));
        return applicable;
    }

    // ── Section C ──────────────────────────────────────────────────────
    private void SectionC(StringBuilder h, DataTable dtC, string cat)
    {
        h.Append(HrDocument.Heading("Section C: Competencies"));
        h.Append(HrDocument.Paragraph("Rating scale: " + RatingLegend(cat) + ". N/A: not applicable."));

        StringBuilder t = new StringBuilder("<table class=\"grid\"><thead><tr>");
        t.Append("<th>Code</th><th>Competency</th><th class=\"n\">Employee</th><th class=\"n\">Supervisor</th><th>Employee comment</th><th>Supervisor comment</th>");
        t.Append("</tr></thead><tbody>");
        if (dtC.Rows.Count == 0)
        {
            t.Append("<tr><td colspan=\"6\" class=\"none\">None recorded.</td></tr></tbody></table>");
            h.Append(t.ToString());
            return;
        }

        bool subtotals = (cat == "ACADEMIC" || cat == "SUPPORT");
        string lastCat = "";
        int catSum = 0, catRated = 0, total = 0, applicable = 0, naCount = 0;
        foreach (DataRow c in dtC.Rows)
        {
            string group = SafeStr(c["category_name"]);
            if (group != lastCat)
            {
                if (lastCat != "" && subtotals) Subtotal(t, lastCat, catSum, catRated);
                t.AppendFormat("<tr><td colspan=\"6\" style=\"font-weight:700;color:#05275C;background:#f5f7fa;\">{0}</td></tr>", HrDocument.E(group));
                lastCat = group; catSum = 0; catRated = 0;
            }
            bool isNa = SafeInt(c["is_na"]) == 1;
            int selfR = SafeInt(c["self_rating"]), rating = SafeInt(c["rating"]);
            if (isNa) naCount++;
            else
            {
                applicable++;
                if (rating > 0) { total += rating; catSum += rating; catRated++; }
            }
            t.Append("<tr>");
            t.AppendFormat("<td>{0}</td>", HrDocument.E(SafeStr(c["competency_code"])));
            t.AppendFormat("<td>{0}</td>", HrDocument.E(SafeStr(c["competency_name"])));
            t.AppendFormat("<td class=\"n\">{0}</td>", isNa ? "N/A" : (selfR > 0 ? selfR.ToString() : ""));
            t.AppendFormat("<td class=\"n\">{0}</td>", isNa ? "N/A" : (rating > 0 ? rating.ToString() : ""));
            t.AppendFormat("<td>{0}</td>", HrDocument.Multiline(SafeStr(c["comment"])));
            t.AppendFormat("<td>{0}</td>", HrDocument.Multiline(SafeStr(c["supervisor_comment"])));
            t.Append("</tr>");
        }
        if (lastCat != "" && subtotals) Subtotal(t, lastCat, catSum, catRated);
        t.Append("</tbody><tfoot><tr>");
        t.AppendFormat("<td colspan=\"3\">Total, {0} applicable, {1} not applicable</td>", applicable, naCount);
        t.AppendFormat("<td class=\"n\">{0} of {1}</td><td colspan=\"2\"></td>", total, applicable * 5);
        t.Append("</tr></tfoot></table>");
        h.Append(t.ToString());
    }

    private static void Subtotal(StringBuilder t, string group, int sum, int rated)
    {
        t.AppendFormat("<tr><td colspan=\"3\" style=\"text-align:right;color:#555;\">Subtotal: {0}</td><td class=\"n\" style=\"font-weight:600;\">{1}</td><td colspan=\"2\"></td></tr>",
            HrDocument.E(group), rated > 0 ? sum + " of " + (rated * 5) : "");
    }

    // ── Section D ──────────────────────────────────────────────────────
    private void SectionD(StringBuilder h, DataTable dtD)
    {
        h.Append(HrDocument.Heading("Section D: Training and development plan"));
        h.Append(HrDocument.Paragraph("How would you like Management to assist you to improve your performance? " +
            "The plan may include training, coaching, mentoring, attachment, job rotation, counselling and the provision of other facilities and resources."));
        List<string[]> rows = new List<string[]>();
        int n = 0;
        foreach (DataRow d in dtD.Rows)
        {
            string gap = SafeStr(d["performance_gap"]).Trim(), action = SafeStr(d["agreed_action"]).Trim();
            if (gap == "" && action == "") continue;
            n++;
            rows.Add(new string[] { n.ToString(), HrDocument.Multiline(gap), HrDocument.Multiline(action), HrDocument.E(SafeStr(d["time_frame"])) });
        }
        if (n == 0)
            for (int i = 1; i <= 4; i++) rows.Add(new string[] { i.ToString(), "&nbsp;", "&nbsp;", "&nbsp;" });
        h.Append(HrDocument.Table(new string[] { "No.", "Performance gap or training need", "Agreed action", "Time frame" },
            rows, new bool[] { true, false, false, false }, null));
    }

    // ── Section E ──────────────────────────────────────────────────────
    private void SectionE(StringBuilder h, DataTable dtE, string cat)
    {
        h.Append(HrDocument.Heading("Section E: Comments"));
        if (cat == "SUPPORT" && dtE.Rows.Count == 0)
        {
            h.Append("<p style=\"margin:4px 0 2px;font-weight:600;\">Supervisor's remarks</p><div class=\"box\" style=\"min-height:60px;\">&nbsp;</div>");
            return;
        }
        if (dtE.Rows.Count == 0)
        {
            string[] qs = {
                "Describe how effectively you have been utilised by the University.",
                "What do you consider to be your major strengths with respect to your competencies?",
                "List any work you accomplished in addition to your agreed tasks and responsibilities.",
                "In respect of your key performance areas, what achievements are you particularly pleased with?",
                "Specify any areas where you could not meet the expected standards and give reasons.",
                "What are your aspirations in terms of career development?" };
            for (int i = 0; i < qs.Length; i++) Question(h, (i + 1).ToString(), qs[i], "");
            return;
        }
        foreach (DataRow q in dtE.Rows)
            Question(h, SafeStr(q["question_number"]), SafeStr(q["question_text"]), SafeStr(q["response"]).Trim());
    }

    private static void Question(StringBuilder h, string num, string text, string response)
    {
        h.AppendFormat("<div style=\"page-break-inside:avoid;margin-top:8px;\"><div style=\"font-weight:600;\">{0}. {1}</div>", HrDocument.E(num), HrDocument.E(text));
        h.AppendFormat("<div class=\"box\">{0}</div></div>", response != "" ? HrDocument.Multiline(response) : "&nbsp;");
    }

    // ── Score ──────────────────────────────────────────────────────────
    private void Score(StringBuilder h, DataRow r, int bApplicable)
    {
        h.Append(HrDocument.Heading("Score"));
        string pct = Dec(r["final_percentage"]);
        if (pct == "" && Dec(r["raw_score"]) == "")
        {
            h.Append(HrDocument.Paragraph("Not yet scored."));
            return;
        }
        h.Append(HrDocument.Meta(
            "Section B employee total", Dec(r["section_b_self_total"]),
            "Section B supervisor total", Dec(r["section_b_supervisor_total"]) + (bApplicable > 0 ? " of " + (bApplicable * 5) : ""),
            "Section C total", Dec(r["section_c_total"]),
            "Raw score", Dec(r["raw_score"]) + (Dec(r["max_possible"]) != "" ? " of " + Dec(r["max_possible"]) : ""),
            "Score %", pct,
            "Classification", Classify(r["final_percentage"], SafeStr(r["classification"]))));
    }

    // ── Sign-off ───────────────────────────────────────────────────────
    private void SignOff(StringBuilder h, DataRow r)
    {
        h.Append(HrDocument.Heading("Sign-off"));
        string empSign = SafeStr(r["employee_sign_name"]);
        string revSign = SafeStr(r["reviewer_sign_name"]);
        string ack = SafeStr(r["employee_ack"]).ToUpper();
        string empName = SafeStr(r["emp_name"]);

        h.Append(HrDocument.Meta(
            "Employee", empSign != "" ? "Signed electronically by " + empSign + ", " + DtTime(r["employee_signed_at"])
                       : (r["employee_submitted_at"] != DBNull.Value ? "Submitted " + DtTime(r["employee_submitted_at"]) : "Not signed"),
            "Supervisor", revSign != "" ? "Signed electronically by " + revSign + ", " + DtTime(r["reviewer_signed_at"])
                       : (r["supervisor_submitted_at"] != DBNull.Value ? "Completed " + DtTime(r["supervisor_submitted_at"]) : "Not signed")));

        string statement;
        if (ack == "AGREE" || ack == "DISAGREE")
            statement = "I, " + empName + ", have read and understood the rating of my appraisal and I therefore " +
                        (ack == "AGREE" ? "agree" : "disagree") + " with it. Recorded electronically " + DtTime(r["employee_ack_at"]) + ".";
        else
            statement = "I, " + empName + ", have read and understood the rating of my appraisal and I therefore: agree ______  disagree ______";
        h.Append("<p style=\"margin-top:10px;\"><strong>Employee acknowledgement.</strong> " + HrDocument.E(statement) + "</p>");
        string ackComment = SafeStr(r["employee_ack_comment"]).Trim();
        if (ackComment != "")
            h.Append("<div class=\"box\"><strong>Employee comment:</strong> " + HrDocument.Multiline(ackComment) + "</div>");
    }

    // ── HR review and signatures ───────────────────────────────────────
    private void HrReview(StringBuilder h, DataRow r, string cat)
    {
        h.Append(HrDocument.Heading("HR review"));
        bool reviewed = SafeStr(r["hr_status"]) == "REVIEWED";
        string hrOfficer = reviewed ? SafeStr(r["hr_officer_name"]) : "";
        string hrComments = reviewed ? SafeStr(r["hr_comments"]).Trim() : "";
        if (reviewed)
        {
            int rating = SafeInt(r["hr_overall_rating"]);
            h.Append(HrDocument.Meta(
                "HR officer", hrOfficer,
                "Date", Dt(r["hr_submitted_at"]),
                "HR rating", rating >= 1 && rating <= 5 ? rating + " " + RatingLabel(rating, cat) : "",
                "HR recommendation", RecommendationWord(SafeStr(r["hr_recommendation"]))));
        }
        string label = cat == "ACADEMIC"
            ? "Comments of the responsible officer (Dean, HR Manager, Deputy Vice Chancellor or Vice Chancellor)"
            : "Comments of the HR Manager";
        h.AppendFormat("<p style=\"margin:10px 0 2px;font-weight:600;\">{0}</p>", HrDocument.E(label));
        h.AppendFormat("<div class=\"box\" style=\"min-height:54px;\">{0}</div>", hrComments != "" ? HrDocument.Multiline(hrComments) : "&nbsp;");

        string liveRev = SafeStr(r["live_reviewer_name"]);
        string empSign = SafeStr(r["employee_sign_name"]);
        string revSign = SafeStr(r["reviewer_sign_name"]);
        h.Append(HrDocument.Signatures(
            "Employee", SafeStr(r["emp_name"]), empSign != "" ? "Signed electronically " + DtTime(r["employee_signed_at"]) : "",
            "Supervisor", First(SafeStr(r["reviewer_name"]), liveRev), revSign != "" ? "Signed electronically " + DtTime(r["reviewer_signed_at"]) : "",
            "HR", hrOfficer, reviewed ? "HR review recorded " + Dt(r["hr_submitted_at"]) : ""));
    }

    // ═══════════════════════════════════════════════════════════════════
    //  VOCABULARY AND HELPERS
    // ═══════════════════════════════════════════════════════════════════
    private static string CategoryWord(string c)
    {
        switch ((c ?? "").ToUpperInvariant())
        {
            case "ACADEMIC": return "Academic";
            case "ADMINISTRATIVE": return "Administrative";
            case "SUPPORT": return "Support";
            default: return c ?? "";
        }
    }

    private static string Word(string s)
    {
        s = (s ?? "").Trim();
        if (s.Length == 0) return "";
        return s.Substring(0, 1).ToUpperInvariant() + s.Substring(1).ToLowerInvariant().Replace('_', ' ');
    }

    private static string RatingLegend(string cat)
    {
        return cat == "ACADEMIC"
            ? "5 Exceptional, 4 Above expectations, 3 Satisfactory, 2 Development needed, 1 Unsatisfactory"
            : "5 Excellent, 4 Very good, 3 Good, 2 Fair, 1 Poor";
    }

    private static string RatingLabel(int rating, string cat)
    {
        string[] acad  = { "", "Unsatisfactory", "Development needed", "Satisfactory", "Above expectations", "Exceptional" };
        string[] other = { "", "Poor", "Fair", "Good", "Very good", "Excellent" };
        if (rating < 1 || rating > 5) return "";
        return cat == "ACADEMIC" ? acad[rating] : other[rating];
    }

    /// <summary>The one overall scale, mirrors SQL appraisal_classify().</summary>
    private static string Classify(object pct, string fallback)
    {
        decimal d;
        if (pct == null || pct == DBNull.Value || !decimal.TryParse(pct.ToString(), out d)) return fallback ?? "";
        if (d >= 90) return "Exceptional";
        if (d >= 75) return "Above expectations";
        if (d >= 60) return "Satisfactory";
        if (d >= 50) return "Development needed";
        return "Unsatisfactory";
    }

    private static string RecommendationWord(string rec)
    {
        switch ((rec ?? "").ToUpperInvariant())
        {
            case "CONFIRM":          return "Confirm appointment";
            case "EXTEND_PROBATION": return "Extend probation";
            case "PIP":              return "Performance improvement plan";
            case "PROMOTE":          return "Promote";
            case "OTHER":            return "Other";
            default:                 return rec ?? "";
        }
    }

    private static string First(string a, string b) { return !string.IsNullOrEmpty(a) && a.Trim() != "" ? a : (b ?? ""); }

    private static string Dt(object v)
    {
        if (v == null || v == DBNull.Value) return "";
        DateTime d;
        return DateTime.TryParse(v.ToString(), out d) && d.Year > 1900 ? d.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : "";
    }

    private static string DtTime(object v)
    {
        if (v == null || v == DBNull.Value) return "";
        DateTime d;
        return DateTime.TryParse(v.ToString(), out d) && d.Year > 1900 ? d.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture) : "";
    }

    private static string Dec(object v)
    {
        decimal d;
        if (v == null || v == DBNull.Value || !decimal.TryParse(v.ToString(), out d)) return "";
        return d.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static string SafeStr(object v) { return v == null || v == DBNull.Value ? "" : v.ToString(); }

    private static int SafeInt(object v)
    {
        if (v == null || v == DBNull.Value) return 0;
        if (v is bool) return (bool)v ? 1 : 0;   // TINYINT(1) arrives as bool
        int n;
        return int.TryParse(v.ToString(), out n) ? n : 0;
    }

    private DataTable Q(string sql, int rid)
    {
        DataTable dt = new DataTable();
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@rid", rid);
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) { da.Fill(dt); }
            }
        }
        return dt;
    }
}
