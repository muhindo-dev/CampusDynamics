using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

/// <summary>
/// Contract renewal print pages (standalone, no SidebarMaster - so it checks the login itself):
///   ?id=N                 the application pack: cover summary, the Governance Council's
///                         "Evaluation Form for Achievements of Staff Responsibilities and Key
///                         Performance Areas" laid out like the paper form, supervisor
///                         recommendation, HR verification, Council decision, attached documents.
///   ?schedule=1[&amp;round=][&amp;status=|&amp;ids=]   the Council schedule (default: all FORWARDED).
/// HR / administrator access only.
/// </summary>
public partial class COOPERP_NewScreens_ContractRenewalPrint : System.Web.UI.Page
{
    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        if (!IsCallerAuthenticated())
        {
            Response.Redirect("~/Default.aspx?ReturnUrl=" + HttpUtility.UrlEncode(Request.RawUrl), true);
            return;
        }
        if (!HasHrAccess())
        {
            litTitle.Text = "Access denied";
            litBody.Text = "<div class='pp-sheet'><p>Access denied. Contract renewal applications are available to HR and administrators only.</p></div>";
            return;
        }
        try
        {
            if (Request.QueryString["schedule"] == "1") RenderSchedule();
            else
            {
                int id;
                if (!int.TryParse(Request.QueryString["id"] ?? "", out id) || id <= 0)
                { Response.Redirect("~/COOPERP/NewScreens/ContractRenewals.aspx", true); return; }
                RenderPack(id);
            }
        }
        catch (System.Threading.ThreadAbortException) { throw; }
        catch (Exception ex)
        {
            litBody.Text = "<div class='pp-sheet'><p>Error: " + Enc(ex.Message) + "</p></div>";
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AUTH (same rule as the appraisal screens)
    // ═══════════════════════════════════════════════════════════════════
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

    private string CurrentUsername()
    {
        try
        {
            if (Session != null && Session["username"] != null && !string.IsNullOrEmpty(Session["username"].ToString().Trim()))
                return Session["username"].ToString().Trim();
        }
        catch { }
        try
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated) return User.Identity.Name ?? "";
        }
        catch { }
        return "";
    }

    private bool HasHrAccess()
    {
        try
        {
            if (RoleAccessService.IsAdmin()) return true;
            string u = CurrentUsername();
            if (string.IsNullOrEmpty(u)) return false;
            DataTable dt = Q(
                @"SELECT
                    (SELECT COUNT(*) FROM sys_user_roles ur
                       JOIN sys_roles r ON r.id = ur.role_id
                      WHERE ur.username = @u AND ur.is_active = 1 AND r.is_active = 1
                        AND (ur.expires_at IS NULL OR ur.expires_at > NOW())
                        AND r.role_code IN ('admin','hr_manager'))
                  + (SELECT COUNT(*) FROM my_aspnet_users mu
                       JOIN my_aspnet_usersinroles mur ON mur.userId = mu.id
                       JOIN my_aspnet_roles mr ON mr.id = mur.roleId
                      WHERE mu.name = @u
                        AND mr.name IN ('Administrator','System Admin','Human Resource','Human Resource Manager')) AS n",
                new MySqlParameter("@u", u));
            return dt.Rows.Count > 0 && SafeInt(dt.Rows[0]["n"]) > 0;
        }
        catch { return false; }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  APPLICATION PACK
    // ═══════════════════════════════════════════════════════════════════
    private void RenderPack(int id)
    {
        DataTable dt = Q(
            @"SELECT r.*, IFNULL(rev.emp_name,'') AS reviewer_live_name, rd.title AS round_title
              FROM hr_contract_renewals r
              LEFT JOIN hrm_employee rev ON rev.empID = r.reviewer_id
              LEFT JOIN hr_renewal_rounds rd ON rd.round_id = r.round_id
              WHERE r.renewal_id = @id", new MySqlParameter("@id", id));
        if (dt.Rows.Count == 0) { litTitle.Text = "Not found"; litBody.Text = "<div class='pp-sheet'><p>Application not found.</p></div>"; return; }
        DataRow r = dt.Rows[0];
        string refNo = S(r["ref_no"]) != "" ? S(r["ref_no"]) : "#" + id;
        litTitle.Text = Enc(refNo + " " + S(r["emp_name"]));

        DataTable ach = Q(@"SELECT item_id, kpa, expected_standard, achievement, evidence, reviewer_comment
                            FROM hr_renewal_achievements WHERE renewal_id = @id ORDER BY sort_order, item_id", new MySqlParameter("@id", id));
        DataTable docs = Q(@"SELECT doc_id, doc_type, item_id, original_name, size_bytes, uploaded_at FROM hr_renewal_documents
                             WHERE renewal_id = @id ORDER BY FIELD(doc_type,'APPLICATION_LETTER','MOTIVATION_LETTER','CV','SUPPORTING','EVIDENCE'), doc_id",
            new MySqlParameter("@id", id));
        DataTable appr = Q(@"SELECT ar.final_percentage, ar.classification, ar.status, IFNULL(s.session_title,'') AS session_title
                             FROM appraisal_records ar LEFT JOIN appraisal_sessions s ON s.session_id = ar.session_id
                             WHERE ar.employee_id = @e ORDER BY COALESCE(ar.employee_submitted_at, ar.created_at) DESC, ar.record_id DESC LIMIT 3",
            new MySqlParameter("@e", SafeInt(r["employee_id"])));

        string reviewerName = S(r["sup_name"]) != "" ? S(r["sup_name"]) : S(r["reviewer_live_name"]);
        StringBuilder b = new StringBuilder();

        // ── page 1: cover ──
        b.Append("<div class='pp-sheet'>");
        b.Append(Letterhead("CONTRACT RENEWAL APPLICATION"));
        b.AppendFormat("<div class='pp-refline'><span>Ref: <strong>{0}</strong></span><span>Status: <strong>{1}</strong></span><span>Printed {2}</span></div>",
            Enc(refNo), Enc(COOPERP_NewScreens_ContractRenewalView.StatusLabel(S(r["status"]))), DateTime.Now.ToString("d MMM yyyy HH:mm"));

        b.Append("<h3 class='pp-h'>Applicant and current contract</h3><table class='pp-kv'>");
        Kv(b, "Name", S(r["emp_name"]), "Employee No", S(r["emp_code"]));
        Kv(b, "Category", COOPERP_NewScreens_ContractRenewalView.CategoryLabel(S(r["staff_category"])), "Contract type", S(r["cur_type"]));
        Kv(b, "Position", S(r["cur_job"]), "Department", S(r["cur_department"]));
        Kv(b, "Contract start", Dt(r["cur_start"]), "Contract end", Dt(r["cur_end"]));
        Kv(b, "Supervisor", reviewerName, "Contact", (S(r["contact_phone"]) + (S(r["contact_email"]) != "" ? "  " + S(r["contact_email"]) : "")).Trim());
        b.Append("</table>");

        b.Append("<h3 class='pp-h'>Request</h3><table class='pp-kv'>");
        Kv(b, "Requested term", SafeInt(r["requested_term_months"]) > 0 ? SafeInt(r["requested_term_months"]) + " months" : "", "Contract type", S(r["requested_type"]));
        Kv(b, "Requested period", Dt(r["requested_start"]) + " to " + Dt(r["requested_end"]), "Position", S(r["requested_position"]));
        Kv(b, "Submitted", DtT(r["submitted_at"]), "Days to expiry at submission",
            r["days_to_expiry_at_submit"] == DBNull.Value ? "" : SafeInt(r["days_to_expiry_at_submit"]) + (SafeInt(r["is_late"]) == 1 ? " (LATE - under 3 months)" : ""));
        Kv(b, "Round", S(r["round_title"]), "Council sitting", S(r["council_sitting"]));
        b.Append("</table>");
        b.Append("<div class='pp-para'><div class='pp-para__l'>Why the contract should be renewed</div><div class='pp-para__t'>" + Nl(S(r["justification"])) + "</div></div>");
        b.Append("<div class='pp-para'><div class='pp-para__l'>Plans for the next contract</div><div class='pp-para__t'>" + Nl(S(r["future_plans"])) + "</div></div>");

        if (appr.Rows.Count > 0)
        {
            b.Append("<h3 class='pp-h'>Performance appraisal (latest)</h3><table class='pp-grid'><thead><tr><th>Session</th><th>Status</th><th>Score</th><th>Classification</th></tr></thead><tbody>");
            foreach (DataRow a in appr.Rows)
                b.AppendFormat("<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td></tr>", Enc(S(a["session_title"])), Enc(S(a["status"]).Replace("_", " ")),
                    a["final_percentage"] == DBNull.Value ? "" : Convert.ToDecimal(a["final_percentage"]).ToString("0.0") + "%", Enc(S(a["classification"])));
            b.Append("</tbody></table>");
        }
        b.Append("</div>");

        // ── page 2: the Council evaluation form ──
        b.Append("<div class='pp-sheet pp-break'>");
        b.Append(Letterhead("EVALUATION FORM FOR ACHIEVEMENTS OF STAFF RESPONSIBILITIES AND KEY PERFORMANCE AREAS"));
        b.Append("<table class='pp-form-head'>");
        b.AppendFormat("<tr><td><span>NAME</span>{0}</td><td><span>EMPLOYEE NO</span>{1}</td><td><span>DATE OF REVIEW</span>{2}</td></tr>",
            Enc(S(r["emp_name"])), Enc(S(r["emp_code"])), Dt(r["sup_signed_at"]));
        b.AppendFormat("<tr><td><span>POSITION HELD</span>{0}</td><td><span>DEPARTMENT</span>{1}</td><td><span>REPORTS TO</span>{2}</td></tr>",
            Enc(S(r["cur_job"])), Enc(S(r["cur_department"])), Enc(S(r["reviewer_live_name"]) != "" ? S(r["reviewer_live_name"]) : reviewerName));
        b.AppendFormat("<tr><td><span>REVIEWER'S NAME</span>{0}</td><td><span>REVIEWER'S TITLE</span>{1}</td><td><span>DATE SUBMITTED</span>{2}</td></tr>",
            Enc(reviewerName), Enc(S(r["sup_title"])), Dt(r["submitted_at"]));
        b.Append("</table>");

        b.Append("<table class='pp-grid pp-form'><thead><tr><th class='pp-no'>NO</th><th>EMPLOYEE RESPONSIBILITIES-ROLES / KEY PERFORMANCE AREAS</th><th>EXPECTED STANDARDS</th><th>ACHIEVEMENTS</th><th>EVIDENCE OF THE ACHIEVEMENTS</th></tr></thead><tbody>");
        int i = 0;
        foreach (DataRow a in ach.Rows)
        {
            i++;
            StringBuilder files = new StringBuilder();
            foreach (DataRow d in docs.Rows)
                if (S(d["doc_type"]) == "EVIDENCE" && SafeInt(d["item_id"]) == SafeInt(a["item_id"]))
                    files.Append("<div class='pp-file'>[Attached] " + Enc(S(d["original_name"])) + "</div>");
            b.AppendFormat("<tr><td class='pp-no'>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td><td>{4}{5}</td></tr>",
                i, Nl(S(a["kpa"])), Nl(S(a["expected_standard"])), Nl(S(a["achievement"])), Nl(S(a["evidence"])), files);
            if (S(a["reviewer_comment"]).Trim() != "")
                b.AppendFormat("<tr class='pp-revrow'><td></td><td colspan='4'><em>Reviewer's comment:</em> {0}</td></tr>", Nl(S(a["reviewer_comment"])));
        }
        if (ach.Rows.Count == 0) b.Append("<tr><td colspan='5' class='pp-empty'>No achievement rows entered.</td></tr>");
        b.Append("</tbody></table>");

        b.Append("<div class='pp-box'><div class='pp-box__l'>COMMENTS AND APPROVAL</div><div class='pp-box__t'>");
        if (S(r["sup_recommendation"]) != "")
            b.Append("<strong>" + Enc(RecLabel(S(r["sup_recommendation"]))) + "</strong>" + (SafeInt(r["sup_term_months"]) > 0 ? " &middot; suggested term " + SafeInt(r["sup_term_months"]) + " months" : "") + "<br/>");
        b.Append(Nl(S(r["sup_comments"])) + "</div></div>");

        b.Append("<table class='pp-sign'><tr>");
        b.AppendFormat("<td><div class='pp-sign__l'>EMPLOYEE SIGNATURE</div><div class='pp-sign__v'>{0}</div><div class='pp-sign__d'>DATE: {1}</div></td>",
            S(r["employee_sign_name"]) != "" ? "<span class='pp-esign'>" + Enc(S(r["employee_sign_name"])) + "</span><span class='pp-esign__n'>signed electronically</span>" : "&nbsp;",
            S(r["employee_signed_at"]) != "" ? DtT(r["employee_signed_at"]) : "____________");
        b.AppendFormat("<td><div class='pp-sign__l'>REVIEWER'S SIGNATURE</div><div class='pp-sign__v'>{0}</div><div class='pp-sign__d'>DATE: {1}</div></td>",
            S(r["sup_signed_at"]) != "" && reviewerName != "" ? "<span class='pp-esign'>" + Enc(reviewerName) + "</span><span class='pp-esign__n'>signed electronically</span>" : "&nbsp;",
            S(r["sup_signed_at"]) != "" ? DtT(r["sup_signed_at"]) : "____________");
        b.Append("</tr></table>");
        b.Append("</div>");

        // ── page 3: recommendation, HR verification, Council, documents ──
        b.Append("<div class='pp-sheet pp-break'>");
        b.Append(Letterhead("RECOMMENDATION, VERIFICATION AND DECISION"));

        b.Append("<h3 class='pp-h'>Supervisor recommendation</h3>");
        if (S(r["sup_recommendation"]) == "") b.Append("<p class='pp-empty'>No supervisor recommendation recorded.</p>");
        else
        {
            b.Append("<table class='pp-kv'>");
            Kv(b, "Recommendation", RecLabel(S(r["sup_recommendation"])), "Suggested term", SafeInt(r["sup_term_months"]) > 0 ? SafeInt(r["sup_term_months"]) + " months" : "");
            Kv(b, "Reviewer", reviewerName + (S(r["sup_title"]) != "" ? ", " + S(r["sup_title"]) : ""), "Signed", DtT(r["sup_signed_at"]));
            b.Append("</table>");
        }

        b.Append("<h3 class='pp-h'>HR verification</h3>");
        string cj = S(r["hr_checklist_json"]);
        b.Append("<table class='pp-grid'><tbody>");
        for (int k = 0; k < COOPERP_NewScreens_ContractRenewalView.CheckKeys.Length; k++)
        {
            string key = COOPERP_NewScreens_ContractRenewalView.CheckKeys[k];
            int p = cj.IndexOf("\"" + key + "\":", StringComparison.Ordinal);
            string mark = cj == "" ? "" : (p >= 0 && p + key.Length + 3 < cj.Length && cj[p + key.Length + 3] == '1' ? Tick(true) + " Yes" : Tick(false) + " No");
            b.AppendFormat("<tr><td>{0}</td><td class='pp-mark'>{1}</td></tr>", Enc(COOPERP_NewScreens_ContractRenewalView.CheckLabels[k]), mark == "" ? "Not yet verified" : mark);
        }
        b.Append("</tbody></table>");
        b.Append("<div class='pp-para'><div class='pp-para__l'>HR comments</div><div class='pp-para__t'>" + Nl(S(r["hr_comments"])) + "</div></div>");
        if (S(r["hr_verified_at"]) != "")
            b.AppendFormat("<p class='pp-small'>Verified by {0} on {1}; forwarded to the Governance Council ({2}) on {3}.</p>",
                Enc(S(r["hr_actor"])), DtT(r["hr_verified_at"]), Enc(S(r["council_sitting"])), DtT(r["forwarded_at"]));

        b.Append("<h3 class='pp-h'>Governance Council decision</h3>");
        if (S(r["decision"]) == "") b.Append("<p class='pp-empty'>No decision recorded.</p>");
        else
        {
            b.Append("<table class='pp-kv'>");
            Kv(b, "Decision", COOPERP_NewScreens_ContractRenewalView.StatusLabel(S(r["decision"])), "Council sitting", S(r["council_sitting"]));
            if (S(r["decision"]) == "APPROVED")
                Kv(b, "Approved term", SafeInt(r["decision_term_months"]) + " months", "New contract period", Dt(r["decision_start"]) + " to " + Dt(r["decision_end"]));
            Kv(b, "Recorded by", S(r["decision_recorded_by"]), "On", DtT(r["decision_at"]));
            if (SafeInt(r["new_contract_id"]) > 0)
                Kv(b, "New contract", "#" + SafeInt(r["new_contract_id"]), "Issued", DtT(r["contract_issued_at"]) + " by " + S(r["contract_issued_by"]));
            b.Append("</table>");
            if (S(r["decision_notes"]) != "")
                b.Append("<div class='pp-para'><div class='pp-para__l'>Council notes</div><div class='pp-para__t'>" + Nl(S(r["decision_notes"])) + "</div></div>");
        }

        b.Append("<h3 class='pp-h'>Attached documents</h3><table class='pp-grid'><thead><tr><th class='pp-no'>No</th><th>Type</th><th>File</th><th>Uploaded</th></tr></thead><tbody>");
        int n = 0;
        foreach (DataRow d in docs.Rows)
        {
            n++;
            b.AppendFormat("<tr><td class='pp-no'>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td></tr>", n,
                Enc(COOPERP_NewScreens_ContractRenewalView.DocTypeLabel(S(d["doc_type"]))), Enc(S(d["original_name"])), DtT(d["uploaded_at"]));
        }
        if (docs.Rows.Count == 0) b.Append("<tr><td colspan='4' class='pp-empty'>No documents uploaded.</td></tr>");
        b.Append("</tbody></table>");
        b.Append("</div>");

        litBody.Text = b.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  COUNCIL SCHEDULE
    // ═══════════════════════════════════════════════════════════════════
    private void RenderSchedule()
    {
        int round;
        int.TryParse(Request.QueryString["round"] ?? "0", out round);
        List<MySqlParameter> ps = new List<MySqlParameter>();
        string where = COOPERP_NewScreens_ContractRenewals.ScheduleWhere(Request.QueryString["ids"], round, Request.QueryString["status"], ps);
        DataTable dt = Q(COOPERP_NewScreens_ContractRenewals.ScheduleSql + where + " ORDER BY r.cur_department, r.emp_name", ps.ToArray());

        string sitting = "";
        if (round > 0)
        {
            DataTable rd = Q("SELECT council_sitting FROM hr_renewal_rounds WHERE round_id = @r", new MySqlParameter("@r", round));
            if (rd.Rows.Count > 0) sitting = S(rd.Rows[0]["council_sitting"]);
        }
        if (sitting == "" && dt.Rows.Count > 0) sitting = S(dt.Rows[0]["council_sitting"]);
        litTitle.Text = "Council schedule - contract renewals";
        litBodyClass.Text = "pp-landscape";

        StringBuilder b = new StringBuilder("<div class='pp-sheet pp-sheet--wide'>");
        b.Append(Letterhead("CONTRACT RENEWAL APPLICATIONS FOR THE GOVERNANCE COUNCIL" + (sitting != "" ? " - " + sitting.ToUpper() + " SITTING" : "")));
        b.AppendFormat("<div class='pp-refline'><span>{0} application(s)</span><span>Printed {1}</span></div>", dt.Rows.Count, DateTime.Now.ToString("d MMM yyyy HH:mm"));
        b.Append("<table class='pp-grid pp-sched'><thead><tr><th class='pp-no'>No</th><th>Name</th><th>Position</th><th>Department</th><th>Current contract end</th><th>Requested term</th><th>Supervisor recommendation</th><th>Appraisal score</th><th>HR remarks</th></tr></thead><tbody>");
        int i = 0;
        foreach (DataRow r in dt.Rows)
        {
            i++;
            b.AppendFormat("<tr><td class='pp-no'>{0}</td><td><strong>{1}</strong><div class='pp-small'>{2} &middot; {3}{4}</div></td><td>{5}</td><td>{6}</td><td class='pp-nowrap'>{7}</td><td>{8}</td><td>{9}</td><td>{10}</td><td>{11}</td></tr>",
                i, Enc(S(r["emp_name"])), Enc(S(r["emp_code"])), Enc(S(r["ref_no"])), SafeInt(r["is_late"]) == 1 ? " &middot; LATE" : "",
                Enc(S(r["cur_job"])), Enc(S(r["cur_department"])), Dt(r["cur_end"]),
                SafeInt(r["requested_term_months"]) > 0 ? SafeInt(r["requested_term_months"]) + " months" : "",
                Enc(RecLabel(S(r["sup_recommendation"]))) + (SafeInt(r["sup_term_months"]) > 0 ? "<div class='pp-small'>" + SafeInt(r["sup_term_months"]) + " months</div>" : ""),
                Enc(S(r["appraisal_score"])), Nl(S(r["hr_comments"])));
        }
        if (dt.Rows.Count == 0) b.Append("<tr><td colspan='9' class='pp-empty'>No applications match (by default the schedule lists applications forwarded to Council).</td></tr>");
        b.Append("</tbody></table>");
        b.Append("<table class='pp-sign' style='margin-top:30px;'><tr><td><div class='pp-sign__l'>PREPARED BY (HUMAN RESOURCE)</div><div class='pp-sign__v'>&nbsp;</div><div class='pp-sign__d'>DATE: ____________</div></td>" +
                 "<td><div class='pp-sign__l'>RECEIVED BY (SECRETARY TO COUNCIL)</div><div class='pp-sign__v'>&nbsp;</div><div class='pp-sign__d'>DATE: ____________</div></td></tr></table>");
        b.Append("</div>");
        litBody.Text = b.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════
    private string Letterhead(string title)
    {
        return "<div class='pp-lh'><div class='pp-lh__uni'>MUTEESA I ROYAL UNIVERSITY</div><div class='pp-lh__off'>Office of Human Resource</div></div>" +
               "<div class='pp-title'>" + Enc(title) + "</div>";
    }

    private static void Kv(StringBuilder b, string l1, string v1, string l2, string v2)
    {
        b.AppendFormat("<tr><th>{0}</th><td>{1}</td><th>{2}</th><td>{3}</td></tr>", Enc(l1), v1 == "" ? "&nbsp;" : Enc(v1), Enc(l2), v2 == "" ? "&nbsp;" : Enc(v2));
    }

    private static string Tick(bool yes)
    {
        return yes
            ? "<svg xmlns='http://www.w3.org/2000/svg' width='11' height='11' viewBox='0 0 24 24' fill='none' stroke='#155724' stroke-width='3'><polyline points='20 6 9 17 4 12'/></svg>"
            : "<svg xmlns='http://www.w3.org/2000/svg' width='11' height='11' viewBox='0 0 24 24' fill='none' stroke='#dc3545' stroke-width='3'><line x1='18' y1='6' x2='6' y2='18'/><line x1='6' y1='6' x2='18' y2='18'/></svg>";
    }

    private static string RecLabel(string r)
    {
        switch (r)
        {
            case "RECOMMEND": return "Recommended for renewal";
            case "RECOMMEND_WITH_CONDITIONS": return "Recommended with conditions";
            case "NOT_RECOMMENDED": return "Not recommended";
            default: return r ?? "";
        }
    }

    private static string Dt(object v) { DateTime? d = COOPERP_NewScreens_ContractRenewalView.D(v); return d.HasValue ? d.Value.ToString("d MMM yyyy") : ""; }
    private static string DtT(object v) { DateTime? d = COOPERP_NewScreens_ContractRenewalView.D(v); return d.HasValue ? d.Value.ToString("d MMM yyyy HH:mm") : ""; }
    private static string Nl(string s) { return string.IsNullOrEmpty((s ?? "").Trim()) ? "&nbsp;" : HttpUtility.HtmlEncode(s).Replace("\r\n", "\n").Replace("\n", "<br/>"); }
    private static string Enc(string s) { return HttpUtility.HtmlEncode(s ?? ""); }
    private static string S(object v) { return COOPERP_NewScreens_ContractRenewalView.SafeStr(v); }
    private static int SafeInt(object v) { return COOPERP_NewScreens_ContractRenewalView.SafeInt(v); }

    private DataTable Q(string sql, params MySqlParameter[] parms)
    {
        DataTable dt = new DataTable();
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null) foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) { da.Fill(dt); }
            }
        }
        return dt;
    }
}
