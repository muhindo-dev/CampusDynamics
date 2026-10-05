using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

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

    // ═══════════════════════════════════════════════════════════════════
    //  PAGE LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════
    protected void Page_Load(object sender, EventArgs e)
    {
        // Standalone page (no SidebarMaster) - nothing else checks the login, so
        // without this any appraisal could be printed anonymously by record id.
        if (!IsCallerAuthenticated())
        {
            Response.Redirect("~/Default.aspx?ReturnUrl=" + HttpUtility.UrlEncode(Request.RawUrl), true);
            return;
        }
        if (QsRecord <= 0) { Response.Redirect("~/COOPERP/NewScreens/AppraisalView.aspx", true); return; }
        if (!IsPostBack) RenderPrintContent(QsRecord);
    }

    private bool IsCallerAuthenticated()
    {
        try
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated
                && !string.IsNullOrEmpty(User.Identity.Name)) return true;
        }
        catch { }
        try
        {
            if (Session != null)
            {
                object u = Session["username"];
                if (u != null && !string.IsNullOrEmpty(u.ToString().Trim())) return true;
            }
        }
        catch { }
        return false;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  MAIN RENDERER
    // ═══════════════════════════════════════════════════════════════════
    private void RenderPrintContent(int rid)
    {
        DataTable dtRec = LoadMainRecord(rid);

        if (dtRec.Rows.Count == 0)
        {
            litContent.Text = "<p style='padding:20pt;'>Appraisal record not found.</p>";
            return;
        }

        DataRow r       = dtRec.Rows[0];
        string staffCat = SafeStr(r["staff_category"]).ToUpper();
        string empName  = SafeStr(r["emp_name"]);
        string status   = SafeStr(r["status"]).ToUpper();
        string period   = FmtDate(r["period_start"]) + " — " + FmtDate(r["period_end"]);
        string session  = SafeStr(r["session_title"]);

        string formTitle;
        if      (staffCat == "ACADEMIC")       formTitle = "ACADEMIC STAFF PERFORMANCE APPRAISAL FORM";
        else if (staffCat == "ADMINISTRATIVE") formTitle = "ADMINISTRATIVE STAFF PERFORMANCE APPRAISAL FORM";
        else if (staffCat == "SUPPORT")        formTitle = "SUPPORT STAFF PERFORMANCE APPRAISAL FORM";
        else                                   formTitle = "STAFF PERFORMANCE APPRAISAL FORM";

        litPageTitle.Text = HttpUtility.HtmlEncode(empName);
        litEmpRef.Text    = "Ref: " + HttpUtility.HtmlEncode(SafeStr(r["EMP_CODE"]));
        litFormTitle.Text = formTitle;
        litPeriod.Text    = HttpUtility.HtmlEncode(period);
        litSession.Text   = HttpUtility.HtmlEncode(session);

        DataTable dtB = ExecuteQuery(
            @"SELECT slot_number, IFNULL(is_catalogue,0) AS is_catalogue,
                     agreed_output, performance_indicators, result_areas, evidence_text,
                     IFNULL(is_na,0) AS is_na, na_reason,
                     self_rating, supervisor_rating, comments
              FROM appraisal_section_b WHERE record_id = @rid ORDER BY slot_number",
            new MySqlParameter("@rid", rid));

        DataTable dtEv = ExecuteQuery(
            @"SELECT evidence_id, slot_number, original_name
              FROM appraisal_evidence WHERE record_id = @rid ORDER BY slot_number, evidence_id",
            new MySqlParameter("@rid", rid));

        // Section C in entry order (keeps each competency group together), with the
        // employee's self rating and the supervisor's comment.
        DataTable dtC = ExecuteQuery(
            @"SELECT competency_code, competency_name, category_name, rating, is_na, comment,
                     IFNULL(self_rating,0) AS self_rating,
                     IFNULL(supervisor_comment,'') AS supervisor_comment
              FROM appraisal_section_c WHERE record_id = @rid ORDER BY entry_id",
            new MySqlParameter("@rid", rid));

        DataTable dtD = ExecuteQuery(
            "SELECT performance_gap, agreed_action, time_frame FROM appraisal_section_d WHERE record_id = @rid ORDER BY entry_id",
            new MySqlParameter("@rid", rid));

        DataTable dtE = ExecuteQuery(
            "SELECT question_number, question_text, response FROM appraisal_section_e WHERE record_id = @rid ORDER BY question_number",
            new MySqlParameter("@rid", rid));

        StringBuilder html = new StringBuilder();

        RenderPreamble(html, staffCat);
        RenderSectionA(html, r, staffCat);
        int bApplicable = RenderSectionB(html, dtB, dtEv, staffCat);
        RenderSectionC(html, dtC, staffCat);
        RenderSectionD(html, dtD);
        RenderSectionE(html, r, dtE, staffCat);
        RenderScoreSummary(html, r, staffCat, bApplicable);
        if (status == "HR_REVIEWED" || SafeStr(r["hr_status"]) == "REVIEWED")
            RenderHrReview(html, r, staffCat);
        RenderSignatures(html, r, staffCat);

        litContent.Text = html.ToString();
    }

    // ─── Load main record with contract details (with fallback) ──────────
    // ar.* carries the snapshot columns reviewer_name / reviewer_title, so the
    // live reviewer is aliased live_reviewer_name to avoid a duplicate column.
    private DataTable LoadMainRecord(int rid)
    {
        try
        {
            return ExecuteQuery(
                @"SELECT ar.*,
                         e.emp_name, e.EMP_CODE, e.EmpType,
                         IFNULL(e.emp_email,'') AS emp_email,
                         IFNULL(e.emp_phone,'') AS emp_phone,
                         IFNULL(d.dept_name,'') AS department,
                         IFNULL(j.jobname,'') AS designation,
                         c.contractStart AS contract_start,
                         c.contractEnd   AS contract_end,
                         IFNULL(c.contract_type,'') AS contract_type,
                         IFNULL(c.contractStatus,'') AS contractStatus,
                         IFNULL(rev.emp_name,'') AS live_reviewer_name,
                         IFNULL(revj.jobname,'') AS reviewer_designation,
                         s.session_title, s.period_start, s.period_end
                  FROM appraisal_records ar
                  INNER JOIN hrm_employee e   ON e.empID = ar.employee_id
                  LEFT JOIN hrm_emp_contracts c ON c.ID = (
                      SELECT c2.ID FROM hrm_emp_contracts c2 WHERE c2.empID = e.empID
                      ORDER BY (CASE WHEN c2.contractStatus='VALID' THEN 0 ELSE 1 END),
                               c2.contractStart DESC LIMIT 1)
                  LEFT JOIN hrm_departments d  ON d.ID = c.departmentID
                  LEFT JOIN hrm_jobs j          ON j.ID = c.jobID
                  LEFT JOIN hrm_employee rev   ON rev.empID = ar.reviewer_id
                  LEFT JOIN hrm_emp_contracts revc ON revc.ID = (
                      SELECT c2.ID FROM hrm_emp_contracts c2 WHERE c2.empID = rev.empID
                      ORDER BY (CASE WHEN c2.contractStatus='VALID' THEN 0 ELSE 1 END),
                               c2.contractStart DESC LIMIT 1)
                  LEFT JOIN hrm_jobs revj       ON revj.ID = revc.jobID
                  INNER JOIN appraisal_sessions s ON s.session_id = ar.session_id
                  WHERE ar.record_id = @rid",
                new MySqlParameter("@rid", rid));
        }
        catch
        {
            return ExecuteQuery(
                @"SELECT ar.*,
                         e.emp_name, e.EMP_CODE, e.EmpType,
                         IFNULL(e.emp_email,'') AS emp_email,
                         IFNULL(e.emp_phone,'') AS emp_phone,
                         '' AS department, IFNULL(e.EmpType,'') AS designation,
                         NULL AS contract_start, NULL AS contract_end,
                         '' AS contract_type, '' AS contractStatus,
                         IFNULL(rev.emp_name,'') AS live_reviewer_name,
                         '' AS reviewer_designation,
                         s.session_title, s.period_start, s.period_end
                  FROM appraisal_records ar
                  INNER JOIN hrm_employee e  ON e.empID = ar.employee_id
                  LEFT JOIN  hrm_employee rev ON rev.empID = ar.reviewer_id
                  INNER JOIN appraisal_sessions s ON s.session_id = ar.session_id
                  WHERE ar.record_id = @rid",
                new MySqlParameter("@rid", rid));
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PREAMBLE
    // ═══════════════════════════════════════════════════════════════════
    private void RenderPreamble(StringBuilder html, string staffCat)
    {
        html.Append("<div class='preamble-box'>");
        html.Append("<div class='preamble-title'>Preamble</div>");
        html.Append("<p>Staff Performance Appraisal is part of the Performance Management System of Muteesa I Royal University. " +
            "It is used as a management tool for establishing the extent to which set targets within the overall goals of the University are achieved. " +
            "Through the staff performance appraisal, performance gaps and development needs of an individual employee are identified. " +
            "The appraisal process offers an opportunity to the appraisee and appraiser to dialogue and obtain a feedback on performance. " +
            "This therefore, calls for a participatory approach to the appraisal process. " +
            "The appraiser and appraisee are advised to read the detailed guidelines before filling this form.</p>");
        html.Append("</div>");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SECTION A — HEADER (Council format) + personal information
    //  Snapshot columns first (frozen at submit / completion), live lookups
    //  as the fallback for older records where they are NULL.
    // ═══════════════════════════════════════════════════════════════════
    private void RenderSectionA(StringBuilder html, DataRow r, string staffCat)
    {
        html.Append("<div class='sec-header'>Section A &mdash; Employee &amp; Review Details</div>");

        string nb = "<span class='field-blank'>Not recorded</span>";

        string liveRev   = SafeStr(r["live_reviewer_name"]);
        string position  = FirstNonEmpty(SafeStr(r["snap_position"]), SafeStr(r["designation"]));
        string dept      = FirstNonEmpty(SafeStr(r["snap_department"]), SafeStr(r["department"]));
        string reportsTo = FirstNonEmpty(SafeStr(r["snap_reports_to"]), liveRev);
        string revName   = FirstNonEmpty(SafeStr(r["reviewer_name"]), liveRev);
        string revTitle  = FirstNonEmpty(SafeStr(r["reviewer_title"]), SafeStr(r["reviewer_designation"]));
        object reviewDt  = r["reviewer_signed_at"] != DBNull.Value ? r["reviewer_signed_at"] : r["supervisor_submitted_at"];
        object submitDt  = r["employee_signed_at"] != DBNull.Value ? r["employee_signed_at"] : r["employee_submitted_at"];

        html.Append("<table class='bio-grid council-grid'>");
        BioRow(html, "NAME",              Val(SafeStr(r["emp_name"]), nb, true),
                     "EMPLOYEE NO.",      Val(SafeStr(r["EMP_CODE"]), nb, false));
        BioRow(html, "DATE OF REVIEW",    reviewDt == DBNull.Value ? nb : Enc(FmtDate(reviewDt)),
                     "POSITION HELD",     Val(position, nb, false));
        BioRow(html, "DEPARTMENT",        Val(dept, nb, false),
                     "REPORTS TO",        Val(reportsTo, nb, false));
        BioRow(html, "REVIEWER&#39;S NAME",  Val(revName, nb, false),
                     "REVIEWER&#39;S TITLE", Val(revTitle, nb, false));
        BioRow(html, "DATE SUBMITTED",    submitDt == DBNull.Value ? nb : Enc(FmtDate(submitDt)),
                     "STAFF CATEGORY",    staffCat == "ACADEMIC" ? "Academic" : (staffCat == "ADMINISTRATIVE" ? "Administrative" : "Support"));
        html.Append("</table>");

        // Personal / contract details (unchanged from the previous form)
        string contractStat = Enc(SafeStr(r["contractStatus"]));
        string dateStart = r["contract_start"] != DBNull.Value ? Enc(FmtDate(r["contract_start"])) : "";
        string dateEnd   = r["contract_end"]   != DBNull.Value ? Enc(FmtDate(r["contract_end"]))   : "";
        string statBadge = !string.IsNullOrEmpty(contractStat)
            ? string.Format("<span class='status-chip status-chip--{0}'>{1}</span>", contractStat.ToLower(), contractStat)
            : nb;

        html.Append("<table class='bio-grid' style='margin-top:4pt;'>");
        BioRow(html, "Email Address", Val(SafeStr(r["emp_email"]), nb, false),
                     "Phone No.",     Val(SafeStr(r["emp_phone"]), nb, false));
        BioRow(html, "Terms of Employment", Val(SafeStr(r["contract_type"]), nb, false),
                     "Contract Status",     statBadge);
        BioRow(html, "Contract Start", dateStart != "" ? dateStart : nb,
                     "Contract End",   dateEnd   != "" ? dateEnd   : nb);
        html.Append("</table>");

        // Qualifications block
        html.Append("<div class='qual-block'>");
        html.Append("<div class='qual-title'>Qualifications:</div>");
        html.Append("<table class='bio-grid'>");
        html.Append("<thead><tr>" +
            "<th class='qual-th' style='width:75%;'>Award &amp; Institution</th>" +
            "<th class='qual-th' style='width:25%;'>Date Obtained</th>" +
            "</tr></thead><tbody>");
        for (int i = 0; i < 5; i++)
            html.Append("<tr><td style='height:13pt;'>&nbsp;</td><td>&nbsp;</td></tr>");
        html.Append("</tbody></table></div>");
    }

    private string Val(string raw, string blank, bool strong)
    {
        if (string.IsNullOrEmpty(raw) || raw.Trim() == "") return blank;
        return strong ? "<strong>" + Enc(raw) + "</strong>" : Enc(raw);
    }

    private void BioRow(StringBuilder html, string lbl1, string val1, string lbl2, string val2)
    {
        html.AppendFormat(
            "<tr><td class='label'>{0}</td><td>{1}</td><td class='label'>{2}</td><td>{3}</td></tr>",
            lbl1, val1, lbl2, val2);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SECTION B — RESPONSIBILITIES / KEY PERFORMANCE AREAS
    //  Returns the number of applicable rows (has a KPA and is not N/A).
    // ═══════════════════════════════════════════════════════════════════
    private int RenderSectionB(StringBuilder html, DataTable dtB, DataTable dtEv, string staffCat)
    {
        html.Append("<div class='sec-header'>Section B &mdash; Responsibilities / Key Performance Areas</div>");
        html.Append("<div class='sec-subheader'>Expected standard, achievement and evidence per responsibility &mdash; rated by the Appraisee and the Appraiser</div>");

        html.AppendFormat("<div class='rating-legend'><strong>Rating Scale:</strong> {0}</div>", RatingLegend(staffCat, false));

        Dictionary<int, List<string>> evBySlot = new Dictionary<int, List<string>>();
        foreach (DataRow ev in dtEv.Rows)
        {
            int slot = SafeInt(ev["slot_number"]);
            if (!evBySlot.ContainsKey(slot)) evBySlot[slot] = new List<string>();
            evBySlot[slot].Add(SafeStr(ev["original_name"]));
        }

        html.Append("<table class='sec-b-table'><thead><tr>");
        html.Append("<th style='width:3%'>#</th>");
        html.Append("<th style='width:16%'>Responsibility / KPA</th>");
        html.Append("<th style='width:18%'>Expected Standard</th>");
        html.Append("<th style='width:18%'>Achievement</th>");
        html.Append("<th style='width:16%'>Evidence</th>");
        html.Append("<th style='width:6%'>Self</th>");
        html.Append("<th style='width:7%'>Super&shy;visor</th>");
        html.Append("<th>Supervisor Comment</th>");
        html.Append("</tr></thead><tbody>");

        int rowNo = 0, applicable = 0, selfTotal = 0, supTotal = 0;

        foreach (DataRow b in dtB.Rows)
        {
            string kpa = SafeStr(b["agreed_output"]).Trim();
            bool isNa = SafeInt(b["is_na"]) == 1;
            if (string.IsNullOrEmpty(kpa) && !isNa) continue;
            rowNo++;
            int slot = SafeInt(b["slot_number"]);

            if (isNa)
            {
                string reason = SafeStr(b["na_reason"]).Trim();
                html.Append("<tr class='na-row'>");
                html.AppendFormat("<td class='num'>{0}</td>", rowNo);
                html.AppendFormat("<td>{0}</td>", Enc(kpa));
                html.AppendFormat("<td>{0}</td>", Enc(SafeStr(b["performance_indicators"])));
                html.AppendFormat("<td colspan='2'><em>Not applicable</em>{0}</td>",
                    reason != "" ? " &mdash; " + Enc(reason) : "");
                html.Append("<td class='num'>N/A</td><td class='num'>N/A</td>");
                html.AppendFormat("<td class='cmnt'>{0}</td>", Enc(SafeStr(b["comments"])));
                html.Append("</tr>");
                continue;
            }

            applicable++;
            int selfR = SafeInt(b["self_rating"]);
            int supR  = SafeInt(b["supervisor_rating"]);
            if (selfR > 0) selfTotal += selfR;
            if (supR  > 0) supTotal  += supR;

            StringBuilder evCell = new StringBuilder();
            string evText = SafeStr(b["evidence_text"]).Trim();
            if (evText != "") evCell.Append(Enc(evText));
            if (evBySlot.ContainsKey(slot))
            {
                evCell.Append("<div class='ev-files'>Attached: ");
                for (int i = 0; i < evBySlot[slot].Count; i++)
                {
                    if (i > 0) evCell.Append("; ");
                    evCell.Append(Enc(evBySlot[slot][i]));
                }
                evCell.Append("</div>");
            }

            html.Append("<tr>");
            html.AppendFormat("<td class='num'>{0}</td>", rowNo);
            html.AppendFormat("<td>{0}</td>", Enc(kpa));
            html.AppendFormat("<td>{0}</td>", Enc(SafeStr(b["performance_indicators"])));
            html.AppendFormat("<td>{0}</td>", Enc(SafeStr(b["result_areas"])));
            html.AppendFormat("<td class='cmnt'>{0}</td>", evCell.Length > 0 ? evCell.ToString() : "&mdash;");
            html.AppendFormat("<td class='num'>{0}</td>", selfR > 0 ? selfR.ToString() : "&mdash;");
            html.AppendFormat("<td class='num b-rating'>{0}</td>", supR > 0 ? supR.ToString() : "&mdash;");
            html.AppendFormat("<td class='cmnt'>{0}</td>", Enc(SafeStr(b["comments"])));
            html.Append("</tr>");
        }

        if (rowNo == 0)
        {
            html.Append("<tr><td colspan='8' class='empty-row'>No responsibilities / key performance areas recorded for this appraisal period.</td></tr>");
        }
        else
        {
            int maxB = applicable * 5;
            html.Append("<tr class='sec-b-total-row'>");
            html.AppendFormat("<td colspan='5' style='text-align:right;'>TOTAL &mdash; {0} applicable responsibilit{1} (N/A excluded)</td>",
                applicable, applicable == 1 ? "y" : "ies");
            html.AppendFormat("<td class='num'>{0}</td>", selfTotal > 0 ? selfTotal + "&nbsp;/&nbsp;" + maxB : "&mdash;");
            html.AppendFormat("<td class='num b-rating'>{0}</td>", supTotal > 0 ? supTotal + "&nbsp;/&nbsp;" + maxB : "&mdash;");
            html.Append("<td></td></tr>");
        }
        html.Append("</tbody></table>");
        return applicable;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SECTION C — COMPETENCY ASSESSMENT
    // ═══════════════════════════════════════════════════════════════════
    private void RenderSectionC(StringBuilder html, DataTable dtC, string staffCat)
    {
        html.Append("<div class='sec-header'>Section C &mdash; Assessment of Core Competencies</div>");
        html.Append("<div class='sec-subheader'>This section should be filled by the Appraiser after joint discussions</div>");

        html.AppendFormat("<div class='rating-legend'><strong>Rating:</strong> {0}<br/><em>Score: X &divide; Y &times; 100, where Y = applicable criteria &times; 5 (N/A excluded)</em></div>",
            RatingLegend(staffCat, true));

        if (dtC.Rows.Count == 0)
        {
            html.Append("<p class='empty-note'>No competencies recorded.</p>");
            return;
        }

        html.Append("<table class='sec-c-table'><thead><tr>");
        html.Append("<th style='width:6%'>Code</th>");
        html.Append("<th>Competency / Criterion</th>");
        html.Append("<th style='width:6%'>Self</th>");
        html.Append("<th style='width:7%'>Super&shy;visor</th>");
        html.Append("<th style='width:5%'>N/A</th>");
        html.Append("<th style='width:16%'>Employee Comment</th>");
        html.Append("<th style='width:16%'>Supervisor Comment</th>");
        html.Append("</tr></thead><tbody>");

        string lastCat       = "";
        int    catRating     = 0, catRatedCount = 0;
        int    totalRating   = 0, naCount = 0, rowCount = 0;
        bool   showSubtotals = (staffCat == "ACADEMIC" || staffCat == "SUPPORT");

        foreach (DataRow c in dtC.Rows)
        {
            rowCount++;
            string  cat        = SafeStr(c["category_name"]);
            string  code       = SafeStr(c["competency_code"]);
            string  name       = SafeStr(c["competency_name"]);
            int     isNa       = SafeInt(c["is_na"]);
            int     selfR      = SafeInt(c["self_rating"]);
            string  comment    = SafeStr(c["comment"]);
            string  supComment = SafeStr(c["supervisor_comment"]);

            if (cat != lastCat && !string.IsNullOrEmpty(lastCat) && showSubtotals)
                EmitCatSubtotal(html, lastCat, catRating, catRatedCount);

            if (cat != lastCat)
            {
                html.AppendFormat("<tr class='cat-row'><td colspan='7'>{0}</td></tr>", Enc(cat));
                lastCat = cat;
                if (showSubtotals) { catRating = 0; catRatedCount = 0; }
            }

            string ratingDisp;
            if (isNa == 1)
            {
                ratingDisp = "<em class='na-text'>N/A</em>";
                naCount++;
            }
            else
            {
                int rating = SafeInt(c["rating"]);
                if (rating > 0)
                {
                    ratingDisp     = "<strong>" + rating + "</strong>";
                    totalRating   += rating;
                    catRating     += rating;
                    catRatedCount++;
                }
                else ratingDisp = "<span class='na-text'>&mdash;</span>";
            }

            html.AppendFormat("<tr{0}>", isNa == 1 ? " class='na-row'" : "");
            html.AppendFormat("<td class='code'>{0}</td>", Enc(code));
            html.AppendFormat("<td>{0}</td>",             Enc(name));
            html.AppendFormat("<td class='num'>{0}</td>", isNa == 1 ? "<em class='na-text'>N/A</em>" : (selfR > 0 ? selfR.ToString() : "<span class='na-text'>&mdash;</span>"));
            html.AppendFormat("<td class='num'>{0}</td>", ratingDisp);
            html.AppendFormat("<td class='na'>{0}</td>",  isNa == 1 ? "Yes" : "");
            html.AppendFormat("<td class='cmnt'>{0}</td>", Enc(comment));
            html.AppendFormat("<td class='cmnt'>{0}</td>", Enc(supComment));
            html.Append("</tr>");
        }

        if (!string.IsNullOrEmpty(lastCat) && showSubtotals)
            EmitCatSubtotal(html, lastCat, catRating, catRatedCount);

        int ratedCount  = rowCount - naCount;
        int adjustedMax = ratedCount * 5;
        html.Append("<tr class='sec-b-total-row' style='background:#dde4f0;'>");
        html.AppendFormat(
            "<td colspan='3' style='text-align:right;'>SECTION C TOTAL &mdash; {0} applicable, {1} marked N/A</td>",
            ratedCount, naCount);
        html.AppendFormat("<td class='num b-rating'>{0}&nbsp;/&nbsp;{1}</td>", totalRating, adjustedMax);
        html.Append("<td colspan='3'></td></tr>");
        html.Append("</tbody></table>");
    }

    private void EmitCatSubtotal(StringBuilder html, string catName, int rating, int ratedCount)
    {
        html.Append("<tr class='cat-subtotal'>");
        html.AppendFormat(
            "<td colspan='3' style='text-align:right;'>Subtotal — {0}</td>", Enc(catName));
        html.AppendFormat(
            "<td class='num'>{0}&nbsp;/&nbsp;{1}</td>",
            ratedCount > 0 ? rating.ToString() : "&mdash;",
            ratedCount > 0 ? (ratedCount * 5).ToString() : "?");
        html.Append("<td colspan='3'></td></tr>");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SECTION D — ACTION PLAN
    // ═══════════════════════════════════════════════════════════════════
    private void RenderSectionD(StringBuilder html, DataTable dtD)
    {
        html.Append("<div class='sec-header'>Section D &mdash; Action Plan to Improve Performance</div>");
        html.Append("<p class='sec-instruction'>How would you like Management to assist you improve your performance? " +
            "The action plan may include: Training, Coaching, Mentoring, Attachment, Job Rotation, Counselling, " +
            "and/or provision of other facilities and resources.</p>");

        html.Append("<table class='sec-d-table'><thead><tr>");
        html.Append("<th style='width:5%'>#</th>");
        html.Append("<th style='width:34%'>Performance Gap / Training Need</th>");
        html.Append("<th style='width:41%'>Agreed Action / Intervention</th>");
        html.Append("<th style='width:20%'>Time Frame</th>");
        html.Append("</tr></thead><tbody>");

        int rowNum = 0;
        foreach (DataRow d in dtD.Rows)
        {
            string gap    = SafeStr(d["performance_gap"]).Trim();
            string action = SafeStr(d["agreed_action"]).Trim();
            string tf     = SafeStr(d["time_frame"]).Trim();
            if (string.IsNullOrEmpty(gap) && string.IsNullOrEmpty(action)) continue;
            rowNum++;
            html.Append("<tr>");
            html.AppendFormat("<td class='num'>{0}</td>", rowNum);
            html.AppendFormat("<td>{0}</td>",             Enc(gap));
            html.AppendFormat("<td>{0}</td>",             Enc(action));
            html.AppendFormat("<td>{0}</td>",             Enc(tf));
            html.Append("</tr>");
        }

        if (rowNum == 0)
        {
            for (int i = 1; i <= 4; i++)
                html.AppendFormat(
                    "<tr><td class='num'>{0}</td><td style='height:16pt;'>&nbsp;</td><td>&nbsp;</td><td>&nbsp;</td></tr>", i);
        }
        html.Append("</tbody></table>");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SECTION E — SELF-REFLECTION
    //  (The employee's Agree / Disagree is printed in the sign-off block for
    //   ALL categories, from employee_ack - not from the old support-only field.)
    // ═══════════════════════════════════════════════════════════════════
    private void RenderSectionE(StringBuilder html, DataRow r, DataTable dtE, string staffCat)
    {
        html.Append("<div class='sec-header'>Section E &mdash; Comments and Reflection</div>");

        if (staffCat == "SUPPORT" && dtE.Rows.Count == 0)
        {
            html.Append("<div class='sec-subheader'>Supervisor&#39;s Performance Remarks</div>");
            html.Append("<div class='lined-box'>&nbsp;</div>");
            return;
        }

        html.Append("<div class='sec-subheader'>Employee Self-Reflection</div>");

        string[] defaultQs = new string[]
        {
            "Describe how effectively you have been utilized by the University.",
            "What do you consider to be your major strength(s) with respect to your competencies?",
            "List down any work you accomplished in addition to your agreed tasks/responsibilities.",
            "In respect of your Key Performance Areas, what achievement(s) are you particularly pleased with?",
            "Specify any areas where you could not meet the expected standards and give reasons thereof.",
            "What are your aspirations in terms of career development?"
        };

        if (dtE.Rows.Count == 0)
        {
            for (int i = 0; i < defaultQs.Length; i++)
            {
                html.Append("<div class='sec-e-question avoid-break'>");
                html.AppendFormat("<div class='sec-e-qnum'>Q{0}.</div>", i + 1);
                html.AppendFormat("<div class='sec-e-qtext'>{0}</div>", defaultQs[i]);
                html.Append("<div class='sec-e-response sec-e-empty'>(No response provided)</div>");
                html.Append("</div>");
            }
        }
        else
        {
            foreach (DataRow eq in dtE.Rows)
            {
                string response = SafeStr(eq["response"]).Trim();
                html.Append("<div class='sec-e-question avoid-break'>");
                html.AppendFormat("<div class='sec-e-qnum'>Q{0}.</div>", SafeStr(eq["question_number"]));
                html.AppendFormat("<div class='sec-e-qtext'>{0}</div>", Enc(SafeStr(eq["question_text"])));
                if (!string.IsNullOrEmpty(response))
                    html.AppendFormat("<div class='sec-e-response'>{0}</div>", Enc(response));
                else
                    html.Append("<div class='sec-e-response sec-e-empty'>(No response provided)</div>");
                html.Append("</div>");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SCORE SUMMARY
    // ═══════════════════════════════════════════════════════════════════
    private void RenderScoreSummary(StringBuilder html, DataRow r, string staffCat, int bApplicable)
    {
        string pctStr  = SafeStr(r["final_percentage"]);
        string rawStr  = SafeStr(r["raw_score"]);
        string maxStr  = SafeStr(r["max_possible"]);
        string bSupStr = SafeStr(r["section_b_supervisor_total"]);
        string bSelStr = SafeStr(r["section_b_self_total"]);
        string cTotStr = SafeStr(r["section_c_total"]);

        if (string.IsNullOrEmpty(pctStr) && string.IsNullOrEmpty(rawStr)) return;

        html.Append("<div class='score-box avoid-break'>");
        html.Append("<div class='score-title'>Overall Performance Score Summary</div>");

        if (!string.IsNullOrEmpty(bSelStr))
            html.AppendFormat(
                "<div class='score-row'>" +
                "<span class='score-label'>Section B &mdash; Employee Self-Assessment</span>" +
                "<span class='score-val'>{0} <em class='score-note'>(for discussion only)</em></span></div>",
                bSelStr);

        if (!string.IsNullOrEmpty(bSupStr))
            html.AppendFormat(
                "<div class='score-row'>" +
                "<span class='score-label'>Section B &mdash; Supervisor Rating Total (max {0})</span>" +
                "<span class='score-val'>{1}</span></div>",
                bApplicable * 5, bSupStr);

        if (!string.IsNullOrEmpty(cTotStr))
            html.AppendFormat(
                "<div class='score-row'>" +
                "<span class='score-label'>Section C &mdash; Competency Total</span>" +
                "<span class='score-val'>{0}</span></div>",
                cTotStr);

        if (!string.IsNullOrEmpty(rawStr) && !string.IsNullOrEmpty(maxStr))
        {
            html.AppendFormat(
                "<div class='score-row score-row--rule'>" +
                "<span class='score-label'>Raw Score (X) / Adjusted Maximum (Y)</span>" +
                "<span class='score-val'>{0} / {1}</span></div>",
                rawStr, maxStr);

            decimal rawVal, maxVal;
            if (decimal.TryParse(rawStr, out rawVal) && decimal.TryParse(maxStr, out maxVal) && maxVal > 0)
            {
                html.AppendFormat(
                    "<div class='score-row'>" +
                    "<span class='score-label'>Formula &nbsp; X &divide; Y &times; 100</span>" +
                    "<span class='score-val'>{0:F0} &divide; {1:F0} &times; 100 = <strong>{2:F2}%</strong></span></div>",
                    rawVal, maxVal, rawVal / maxVal * 100m);
            }
        }

        if (!string.IsNullOrEmpty(pctStr))
        {
            decimal pctVal;
            decimal.TryParse(pctStr, out pctVal);
            html.AppendFormat(
                "<div class='score-final'>Final Percentage: <strong>{0:F2}%</strong>" +
                "&nbsp;&nbsp;|&nbsp;&nbsp;Classification: <strong>{1}</strong></div>",
                pctVal, Enc(Classify(pctVal)));
            html.AppendFormat(
                "<div class='score-band'>{0}</div>",
                ClassificationBand(pctVal));
        }

        html.Append("</div>");
    }

    /// <summary>The one classification scale - mirrors SQL appraisal_classify().</summary>
    private static string Classify(decimal pct)
    {
        if (pct >= 90m) return "Exceptional";
        if (pct >= 75m) return "Above Expectations";
        if (pct >= 60m) return "Satisfactory";
        if (pct >= 50m) return "Development Needed";
        return "Unsatisfactory";
    }

    private string ClassificationBand(decimal pct)
    {
        string label = Classify(pct);
        if (pct >= 90m) return label + " (&ge;90%) &mdash; Recognition, reward; potential promotion consideration";
        if (pct >= 75m) return label + " (75&ndash;89%) &mdash; Positive feedback; identify areas for further growth";
        if (pct >= 60m) return label + " (60&ndash;74%) &mdash; Meets standard; targeted improvement areas identified";
        if (pct >= 50m) return label + " (50&ndash;59%) &mdash; Performance Improvement Plan (PIP) required";
        return label + " (&lt;50%) &mdash; Formal PIP with defined timeline; potential consequences";
    }

    /// <summary>Item rating scale: Academic vs Administrative &amp; Support.</summary>
    private static string RatingLegend(string staffCat, bool withNa)
    {
        string s = staffCat == "ACADEMIC"
            ? "5 = Exceptional &nbsp;|&nbsp; 4 = Above Expectations &nbsp;|&nbsp; 3 = Satisfactory &nbsp;|&nbsp; 2 = Development Needed &nbsp;|&nbsp; 1 = Unsatisfactory"
            : "5 = Excellent &nbsp;|&nbsp; 4 = Very Good &nbsp;|&nbsp; 3 = Good &nbsp;|&nbsp; 2 = Fair &nbsp;|&nbsp; 1 = Poor";
        return withNa ? s + " &nbsp;|&nbsp; N/A = Not Applicable" : s;
    }

    private static string RatingLabel(int rating, string staffCat)
    {
        string[] acad  = { "", "Unsatisfactory", "Development Needed", "Satisfactory", "Above Expectations", "Exceptional" };
        string[] other = { "", "Poor", "Fair", "Good", "Very Good", "Excellent" };
        if (rating < 1 || rating > 5) return "";
        return staffCat == "ACADEMIC" ? acad[rating] : other[rating];
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HR REVIEW
    // ═══════════════════════════════════════════════════════════════════
    private void RenderHrReview(StringBuilder html, DataRow r, string staffCat)
    {
        string hrOfficer = Enc(SafeStr(r["hr_officer_name"]));
        int hrRating = SafeInt(r["hr_overall_rating"]);
        string hrRec = Enc(FormatHrRecommendation(SafeStr(r["hr_recommendation"])));
        string hrComm = Enc(SafeStr(r["hr_comments"]));
        string hrAt = r["hr_submitted_at"] != DBNull.Value
            ? Enc(Convert.ToDateTime(r["hr_submitted_at"]).ToString("d MMMM yyyy")) : "&mdash;";

        string hrRatingLabel = hrRating >= 1 && hrRating <= 5
            ? hrRating + " &mdash; " + RatingLabel(hrRating, staffCat) : "&mdash;";

        html.Append("<div class='sec-header'>Human Resources Department Review</div>");
        html.Append("<table class='bio-grid'>");
        BioRow(html, "HR Officer",        hrOfficer != "" ? hrOfficer : "&mdash;", "Review Date",    hrAt);
        BioRow(html, "HR Overall Rating", hrRatingLabel,                           "Recommendation", hrRec != "" ? hrRec : "&mdash;");
        html.Append("</table>");

        if (!string.IsNullOrEmpty(hrComm))
        {
            html.Append("<div class='hr-comment-box'>");
            html.Append("<strong>HR Comments:</strong><br/>");
            html.Append(hrComm);
            html.Append("</div>");
        }
    }

    private static string FormatHrRecommendation(string rec)
    {
        switch ((rec ?? "").ToUpper())
        {
            case "CONFIRM":          return "Confirm Appointment";
            case "EXTEND_PROBATION": return "Extend Probation";
            case "PIP":              return "Performance Improvement Plan";
            case "PROMOTE":          return "Promote";
            case "OTHER":            return "Other";
            default:                 return rec ?? "";
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SIGN-OFF: e-signatures, employee acknowledgement, responsible officer
    // ═══════════════════════════════════════════════════════════════════
    private void RenderSignatures(StringBuilder html, DataRow r, string staffCat)
    {
        string liveRev     = SafeStr(r["live_reviewer_name"]);
        string empName     = Enc(SafeStr(r["emp_name"]));
        string revName     = Enc(FirstNonEmpty(SafeStr(r["reviewer_name"]), liveRev));
        string revTitle    = Enc(FirstNonEmpty(SafeStr(r["reviewer_title"]), SafeStr(r["reviewer_designation"])));
        string empSign     = Enc(SafeStr(r["employee_sign_name"]));
        string revSign     = Enc(SafeStr(r["reviewer_sign_name"]));
        string empSignedAt = r["employee_signed_at"] != DBNull.Value ? Convert.ToDateTime(r["employee_signed_at"]).ToString("d MMM yyyy, HH:mm") : "";
        string revSignedAt = r["reviewer_signed_at"] != DBNull.Value ? Convert.ToDateTime(r["reviewer_signed_at"]).ToString("d MMM yyyy, HH:mm") : "";
        string submittedAt = r["employee_submitted_at"]  != DBNull.Value
            ? Convert.ToDateTime(r["employee_submitted_at"]).ToString("d MMM yyyy") : "";
        string completedAt = r["supervisor_submitted_at"] != DBNull.Value
            ? Convert.ToDateTime(r["supervisor_submitted_at"]).ToString("d MMM yyyy") : "";
        string ack         = SafeStr(r["employee_ack"]).ToUpper();
        string ackComment  = Enc(SafeStr(r["employee_ack_comment"]));
        string ackAt       = r["employee_ack_at"] != DBNull.Value ? Convert.ToDateTime(r["employee_ack_at"]).ToString("d MMM yyyy, HH:mm") : "";

        html.Append("<div class='sig-block avoid-break'>");
        html.Append("<div class='sig-title'>Comments, Approval and Signatures</div>");

        // Employee acknowledgement of the supervisor's rating (all categories)
        html.Append("<div class='comment-box'>");
        html.Append("<div class='comment-box__label'>Employee&#39;s Acknowledgement of the Rating:</div>");
        html.AppendFormat("<div style='font-size:9pt;margin-bottom:3pt;'>I &nbsp;<span class='emp-underline'>{0}</span>&nbsp; have read and understood the rating of my appraisal and I therefore:</div>", empName);
        html.AppendFormat("<div style='font-size:9pt;'><span class='decl-option{0}'>{1}AGREE</span><span class='decl-option{2}'>{3}DISAGREE</span></div>",
            ack == "AGREE" ? " decl-checked" : "", CheckBox(ack == "AGREE"),
            ack == "DISAGREE" ? " decl-checked" : "", CheckBox(ack == "DISAGREE"));
        if (ackComment != "")
            html.AppendFormat("<div class='ack-comment'><strong>Employee comment:</strong> {0}</div>", ackComment);
        html.AppendFormat("<div class='comment-box__footer'>{0}</div>",
            ack == "AGREE" || ack == "DISAGREE"
                ? "Recorded electronically" + (ackAt != "" ? " on " + Enc(ackAt) : "")
                : "Not yet acknowledged electronically");
        html.Append("</div>");

        // Responsible officer comments (paper)
        string roTitle = (staffCat == "ACADEMIC")
            ? "Comments of the Responsible Officer (Dean / HRM / Vice Chancellor / Deputy Vice Chancellor):"
            : "Comments of the HR Manager:";
        string blankLine = "<span class='sig-underline'>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;</span>";
        html.Append("<div class='comment-box'>");
        html.AppendFormat("<div class='comment-box__label'>{0}</div>", roTitle);
        html.Append("<div class='comment-box__body'>&nbsp;</div>");
        html.AppendFormat("<div class='comment-box__footer'>Name: {0}&nbsp;&nbsp;Title: {0}&nbsp;&nbsp;Signature: {0}&nbsp;&nbsp;Date: {0}</div>", blankLine);
        html.Append("</div>");

        // Three-column signature grid
        html.Append("<div class='sig-grid'>");

        // Appraisee
        html.Append("<div class='sig-item'>");
        html.Append("<div class='role'>Appraisee (Employee)</div>");
        html.AppendFormat("<div class='sig-name'>{0}</div>", empName);
        if (empSign != "")
            html.AppendFormat("<div class='esign'>E-signed: <strong>{0}</strong><br/>{1}</div>", empSign, Enc(empSignedAt));
        else
        {
            html.Append("<div class='sig-line'></div><div class='sig-field-label'>Signature</div>");
            html.Append("<div class='sig-line' style='margin-top:14pt;'></div><div class='sig-field-label'>Date</div>");
        }
        if (!string.IsNullOrEmpty(submittedAt))
            html.AppendFormat("<div class='sig-date'>Submitted: {0}</div>", submittedAt);
        html.Append("</div>");

        // Appraiser
        html.Append("<div class='sig-item'>");
        html.Append("<div class='role'>Appraiser (Reviewer)</div>");
        html.AppendFormat("<div class='sig-name'>{0}</div>", revName != "" ? revName : "&nbsp;");
        if (!string.IsNullOrEmpty(revTitle))
            html.AppendFormat("<div class='sig-field-label'>{0}</div>", revTitle);
        if (revSign != "")
            html.AppendFormat("<div class='esign'>E-signed: <strong>{0}</strong><br/>{1}</div>", revSign, Enc(revSignedAt));
        else
        {
            html.Append("<div class='sig-line' style='margin-top:12pt;'></div><div class='sig-field-label'>Signature</div>");
            html.Append("<div class='sig-line' style='margin-top:14pt;'></div><div class='sig-field-label'>Date</div>");
        }
        if (!string.IsNullOrEmpty(completedAt))
            html.AppendFormat("<div class='sig-date'>Completed: {0}</div>", completedAt);
        html.Append("</div>");

        // Responsible Officer / HR
        html.Append("<div class='sig-item'>");
        if (staffCat == "ACADEMIC")
        {
            html.Append("<div class='role'>Responsible Officer</div>");
            html.Append("<div class='sig-field-label'>Dean / VC / DVC / HRM</div>");
        }
        else { html.Append("<div class='role'>HR Manager</div>"); }
        string hrOfficer = SafeStr(r["hr_status"]) == "REVIEWED" ? Enc(SafeStr(r["hr_officer_name"])) : "";
        string hrAt = SafeStr(r["hr_status"]) == "REVIEWED" && r["hr_submitted_at"] != DBNull.Value
            ? Convert.ToDateTime(r["hr_submitted_at"]).ToString("d MMM yyyy") : "";
        if (hrOfficer != "")
            html.AppendFormat("<div class='esign'>HR review recorded by: <strong>{0}</strong><br/>{1}</div>", hrOfficer, Enc(hrAt));
        html.Append("<div class='sig-line' style='margin-top:22pt;'></div><div class='sig-field-label'>Name</div>");
        html.Append("<div class='sig-line' style='margin-top:14pt;'></div><div class='sig-field-label'>Signature</div>");
        html.Append("<div class='sig-line' style='margin-top:14pt;'></div><div class='sig-field-label'>Date</div>");
        html.Append("</div>");

        html.Append("</div></div>"); // sig-grid + sig-block
    }

    private static string CheckBox(bool on)
    {
        // Inline SVG box (no emoji / dingbat glyphs), ticked when chosen.
        return on
            ? "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='3' style='vertical-align:-1px;margin-right:3pt'><rect x='2' y='2' width='20' height='20'/><polyline points='6 12 10 16 18 8'/></svg>"
            : "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' style='vertical-align:-1px;margin-right:3pt'><rect x='2' y='2' width='20' height='20'/></svg>";
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════
    private static string FirstNonEmpty(string a, string b)
    {
        return !string.IsNullOrEmpty(a) && a.Trim() != "" ? a : (b ?? "");
    }

    private string SafeStr(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        return val.ToString();
    }

    private int SafeInt(object val)
    {
        if (val == null || val == DBNull.Value) return 0;
        // TINYINT(1) columns (is_na, is_active, is_catalogue) arrive as bool with this connection string.
        if (val is bool) return (bool)val ? 1 : 0;
        int n;
        return int.TryParse(val.ToString(), out n) ? n : 0;
    }

    private string FmtDate(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        DateTime d;
        if (val is DateTime) d = (DateTime)val;
        else if (!DateTime.TryParse(val.ToString(), out d)) return val.ToString();
        return d.ToString("d MMMM yyyy");
    }

    private string Enc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return HttpUtility.HtmlEncode(s).Replace("\n", "<br/>");
    }

    private DataTable ExecuteQuery(string sql, params MySqlParameter[] parms)
    {
        DataTable dt = new DataTable();
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null)
                    foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) { da.Fill(dt); }
            }
        }
        return dt;
    }
}
