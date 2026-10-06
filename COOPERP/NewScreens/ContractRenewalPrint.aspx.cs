using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

/// <summary>
/// Contract renewal print documents, built on HrDocument (standalone page, no SidebarMaster, so it
/// gates itself with HrAccess):
///   ?id=N        the application pack: applicant and request, the Governance Council's evaluation
///                form (achievements of staff responsibilities and key performance areas),
///                supervisor recommendation, HR verification, Council decision, attached documents.
///   ?schedule=1[&amp;round=][&amp;status=|&amp;ids=]   the Council schedule, landscape (default: all at Council).
///                Its columns are ContractRenewals.ScheduleHeaders, the same as the Excel and CSV export.
/// E-signatures are typed lines ("Signed electronically by X on 6 Oct 2026, 14:05").
/// </summary>
public partial class COOPERP_NewScreens_ContractRenewalPrint : System.Web.UI.Page
{
    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        if (!HrAccess.RequireHr(false)) return;
        HrDocument.Options o = new HrDocument.Options();
        try
        {
            if (Request.QueryString["schedule"] == "1") litDoc.Text = RenderSchedule(o);
            else
            {
                int id;
                if (!int.TryParse(Request.QueryString["id"] ?? "", out id) || id <= 0)
                { Response.Redirect("~/COOPERP/NewScreens/ContractRenewals.aspx", false); Context.ApplicationInstance.CompleteRequest(); return; }
                litDoc.Text = RenderPack(id, o);
            }
        }
        catch (System.Threading.ThreadAbortException) { throw; }
        catch (Exception)
        {
            o.BackUrl = "ContractRenewals.aspx";
            litDoc.Text = HrDocument.Page("Contract renewal", HrDocument.Paragraph("The document could not be prepared. Reload the page, or contact MIS if this continues."), o);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  APPLICATION PACK
    // ═══════════════════════════════════════════════════════════════════
    private string RenderPack(int id, HrDocument.Options o)
    {
        o.BackUrl = "ContractRenewalView.aspx?id=" + id;
        DataTable dt = Q(
            @"SELECT r.*, IFNULL(rev.emp_name,'') AS reviewer_live_name, rd.title AS round_title
              FROM hr_contract_renewals r
              LEFT JOIN hrm_employee rev ON rev.empID = r.reviewer_id
              LEFT JOIN hr_renewal_rounds rd ON rd.round_id = r.round_id
              WHERE r.renewal_id = @id", new MySqlParameter("@id", id));
        if (dt.Rows.Count == 0)
        {
            o.BackUrl = "ContractRenewals.aspx";
            return HrDocument.Page("Contract renewal application", HrDocument.Paragraph("Application not found."), o);
        }
        DataRow r = dt.Rows[0];
        if (!COOPERP_NewScreens_ContractRenewalView.IsHrLevel(S(r["status"])))
        {
            o.BackUrl = "ContractRenewals.aspx";
            return HrDocument.Page("Contract renewal application", HrDocument.Paragraph(COOPERP_NewScreens_ContractRenewalView.NotReachedHr), o);
        }
        string refNo = S(r["ref_no"]) != "" ? S(r["ref_no"]) : "#" + id;
        o.Reference = "Ref: " + refNo;

        DataTable ach = Q(@"SELECT item_id, kpa, expected_standard, achievement, evidence, reviewer_comment
                            FROM hr_renewal_achievements WHERE renewal_id = @id ORDER BY sort_order, item_id", new MySqlParameter("@id", id));
        DataTable docs = Q(@"SELECT doc_id, doc_type, item_id, original_name, size_bytes, uploaded_at FROM hr_renewal_documents
                             WHERE renewal_id = @id ORDER BY FIELD(doc_type,'APPLICATION_LETTER','MOTIVATION_LETTER','CV','SUPPORTING','EVIDENCE'), doc_id",
            new MySqlParameter("@id", id));
        DataTable appr = Q(@"SELECT ar.final_percentage, ar.classification, ar.status, IFNULL(s.session_title,'') AS session_title
                             FROM appraisal_records ar LEFT JOIN appraisal_sessions s ON s.session_id = ar.session_id
                             WHERE ar.employee_id = @e AND ar.status IN ('COMPLETED','HR_REVIEWED')
                             ORDER BY COALESCE(ar.employee_submitted_at, ar.created_at) DESC, ar.record_id DESC LIMIT 3",
            new MySqlParameter("@e", SafeInt(r["employee_id"])));

        string reviewerName = S(r["sup_name"]) != "" ? S(r["sup_name"]) : S(r["reviewer_live_name"]);
        StringBuilder b = new StringBuilder();

        // ── page 1: applicant and request ──
        b.Append(HrDocument.Heading("Applicant and current contract"));
        b.Append(HrDocument.Meta(
            "Name", S(r["emp_name"]), "Staff no", S(r["emp_code"]),
            "Category", COOPERP_NewScreens_ContractRenewalView.CategoryLabel(S(r["staff_category"])), "Contract type", Words(S(r["cur_type"])),
            "Position", S(r["cur_job"]), "Department", S(r["cur_department"]),
            "Contract start", Dt(r["cur_start"]), "Contract end", Dt(r["cur_end"]),
            "Supervisor", reviewerName, "Contact", Join(", ", S(r["contact_phone"]), S(r["contact_email"]))));

        b.Append(HrDocument.Heading("Renewal request"));
        string daysAtSubmit = r["days_to_expiry_at_submit"] == DBNull.Value ? "" :
            SafeInt(r["days_to_expiry_at_submit"]) + (SafeInt(r["is_late"]) == 1 ? " (late)" : "");
        b.Append(HrDocument.Meta(
            "Requested term", SafeInt(r["requested_term_months"]) > 0 ? SafeInt(r["requested_term_months"]) + " months" : "", "Contract type", Words(S(r["requested_type"])),
            "Requested period", Range(r["requested_start"], r["requested_end"]), "Position", S(r["requested_position"]),
            "Submitted", DtT(r["submitted_at"]), "Days to contract end", daysAtSubmit,
            "Round", S(r["round_title"]), "Council sitting", S(r["council_sitting"]),
            "Status", COOPERP_NewScreens_ContractRenewalView.StatusLabel(S(r["status"]))));
        b.Append(Box("Why the contract should be renewed", S(r["justification"])));
        b.Append(Box("Plans for the next contract", S(r["future_plans"])));

        if (appr.Rows.Count > 0)
        {
            b.Append(HrDocument.Heading("Performance appraisal"));
            List<string[]> rows = new List<string[]>();
            foreach (DataRow a in appr.Rows)
                rows.Add(new string[] {
                    HrDocument.E(S(a["session_title"])),
                    a["final_percentage"] == DBNull.Value ? "" : Convert.ToDecimal(a["final_percentage"]).ToString("0.0", CultureInfo.InvariantCulture),
                    HrDocument.E(S(a["classification"])), HrDocument.E(AppraisalStatus(S(a["status"]))) });
            b.Append(HrDocument.Table(new string[] { "Session", "Score (%)", "Classification", "Status" }, rows, new bool[] { false, true, false, false }, null));
        }

        // ── page 2: the Council evaluation form ──
        b.Append("<div class=\"pb\"></div>");
        b.Append(HrDocument.Heading("Evaluation form: achievements of staff responsibilities and key performance areas"));
        b.Append(HrDocument.Meta(
            "Name", S(r["emp_name"]), "Staff no", S(r["emp_code"]),
            "Position held", S(r["cur_job"]), "Department", S(r["cur_department"]),
            "Reports to", S(r["reviewer_live_name"]) != "" ? S(r["reviewer_live_name"]) : reviewerName, "Supervisor's title", S(r["sup_title"]),
            "Date submitted", Dt(r["submitted_at"]), "Date of review", Dt(r["sup_signed_at"])));
        List<string[]> form = new List<string[]>();
        int i = 0;
        foreach (DataRow a in ach.Rows)
        {
            i++;
            StringBuilder files = new StringBuilder();
            foreach (DataRow d in docs.Rows)
                if (S(d["doc_type"]) == "EVIDENCE" && SafeInt(d["item_id"]) == SafeInt(a["item_id"]))
                    files.Append("<br/>Attached: " + HrDocument.E(S(d["original_name"])));
            form.Add(new string[] {
                i.ToString(), HrDocument.Multiline(S(a["kpa"])), HrDocument.Multiline(S(a["expected_standard"])), HrDocument.Multiline(S(a["achievement"])),
                HrDocument.Multiline(S(a["evidence"])) + files, HrDocument.Multiline(S(a["reviewer_comment"])) });
        }
        b.Append(HrDocument.Table(new string[] { "No", "Responsibilities or key performance areas", "Expected standards", "Achievements", "Evidence of the achievements", "Supervisor comment" },
            form, new bool[] { true }, null));
        string rec = RecLabel(S(r["sup_recommendation"]));
        if (rec != "" && SafeInt(r["sup_term_months"]) > 0) rec += ", suggested term " + SafeInt(r["sup_term_months"]) + " months";
        b.Append(Box("Supervisor's comments and approval", Join(". ", rec, S(r["sup_comments"]).Trim())));
        b.Append(HrDocument.Signatures(
            "Employee", S(r["emp_name"]), Signed("Signed", S(r["employee_sign_name"]), r["employee_signed_at"]),
            "Supervisor", reviewerName, D(r["sup_signed_at"]).HasValue ? Signed("Signed", reviewerName, r["sup_signed_at"]) : ""));

        // ── page 3: recommendation, verification, decision, documents ──
        b.Append("<div class=\"pb\"></div>");
        b.Append(HrDocument.Heading("Supervisor recommendation"));
        if (S(r["sup_recommendation"]) == "") b.Append(HrDocument.Paragraph("Not recorded."));
        else
            b.Append(HrDocument.Meta(
                "Recommendation", RecLabel(S(r["sup_recommendation"])), "Suggested term", SafeInt(r["sup_term_months"]) > 0 ? SafeInt(r["sup_term_months"]) + " months" : "",
                "Supervisor", Join(", ", reviewerName, S(r["sup_title"])), "Signed", DtT(r["sup_signed_at"])));

        b.Append(HrDocument.Heading("HR verification"));
        string cj = S(r["hr_checklist_json"]);
        List<string[]> checks = new List<string[]>();
        for (int k = 0; k < COOPERP_NewScreens_ContractRenewalView.CheckKeys.Length; k++)
        {
            string key = COOPERP_NewScreens_ContractRenewalView.CheckKeys[k];
            int p = cj.IndexOf("\"" + key + "\":", StringComparison.Ordinal);
            string mark = cj == "" ? "Not verified" : (p >= 0 && p + key.Length + 3 < cj.Length && cj[p + key.Length + 3] == '1' ? "Yes" : "No");
            checks.Add(new string[] { HrDocument.E(COOPERP_NewScreens_ContractRenewalView.CheckLabels[k]), mark });
        }
        b.Append(HrDocument.Table(new string[] { "Item", "Verified" }, checks, null, null));
        b.Append(Box("HR remarks", S(r["hr_comments"])));

        b.Append(HrDocument.Heading("Governance Council decision"));
        if (S(r["decision"]) == "") b.Append(HrDocument.Paragraph("Not recorded."));
        else
        {
            List<string> meta = new List<string> {
                "Decision", COOPERP_NewScreens_ContractRenewalView.StatusLabel(S(r["decision"])), "Council sitting", S(r["council_sitting"]) };
            if (S(r["decision"]) == "APPROVED")
                meta.AddRange(new string[] { "Approved term", SafeInt(r["decision_term_months"]) + " months", "New contract period", Range(r["decision_start"], r["decision_end"]) });
            meta.AddRange(new string[] { "Recorded by", Actor(S(r["decision_recorded_by"])), "Recorded on", DtT(r["decision_at"]) });
            if (SafeInt(r["new_contract_id"]) > 0)
                meta.AddRange(new string[] { "Contract issued", DtT(r["contract_issued_at"]), "Issued by", Actor(S(r["contract_issued_by"])) });
            b.Append(HrDocument.Meta(meta.ToArray()));
            if (S(r["decision_notes"]) != "") b.Append(Box("Council notes", S(r["decision_notes"])));
        }
        b.Append(HrDocument.Signatures(
            "Verified by (Human Resource)", Actor(S(r["hr_actor"])), D(r["hr_verified_at"]).HasValue ? Signed("Verified", Actor(S(r["hr_actor"])), r["hr_verified_at"]) : "",
            "Approved by (Secretary to Council)", "", ""));

        b.Append(HrDocument.Heading("Attached documents"));
        List<string[]> drows = new List<string[]>();
        int n = 0;
        foreach (DataRow d in docs.Rows)
        {
            n++;
            drows.Add(new string[] { n.ToString(), HrDocument.E(COOPERP_NewScreens_ContractRenewalView.DocTypeLabel(S(d["doc_type"]))),
                                     HrDocument.E(S(d["original_name"])), HrDocument.E(DtT(d["uploaded_at"])) });
        }
        b.Append(HrDocument.Table(new string[] { "No", "Type", "File", "Uploaded" }, drows, new bool[] { true }, null));

        return HrDocument.Page("Contract renewal application", b.ToString(), o);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  COUNCIL SCHEDULE (same columns as the Excel / CSV export)
    // ═══════════════════════════════════════════════════════════════════
    private string RenderSchedule(HrDocument.Options o)
    {
        o.BackUrl = "ContractRenewals.aspx";
        o.Landscape = true;
        int round;
        int.TryParse(Request.QueryString["round"] ?? "0", out round);
        string ids = Request.QueryString["ids"], status = Request.QueryString["status"];
        List<MySqlParameter> ps = new List<MySqlParameter>();
        string where = COOPERP_NewScreens_ContractRenewals.ScheduleWhere(ids, round, status, ps);
        DataTable dt = Q(COOPERP_NewScreens_ContractRenewals.ScheduleSql + where + COOPERP_NewScreens_ContractRenewals.ScheduleOrder, ps.ToArray());

        List<KeyValuePair<string, string>> scope = COOPERP_NewScreens_ContractRenewals.ScheduleScope(dt, ids, round, status, delegate (string sql) { return Q(sql); });
        List<string> refParts = new List<string>();
        foreach (KeyValuePair<string, string> kv in scope) refParts.Add(kv.Key + ": " + kv.Value);
        bool counted = false;
        foreach (KeyValuePair<string, string> kv in scope) if (kv.Key == "Applications") counted = true;
        if (!counted) refParts.Add(dt.Rows.Count + (dt.Rows.Count == 1 ? " application" : " applications"));
        o.Reference = string.Join("   |   ", refParts.ToArray());

        string[] h = COOPERP_NewScreens_ContractRenewals.ScheduleHeaders;
        List<string[]> rows = new List<string[]>();
        foreach (object[] v in COOPERP_NewScreens_ContractRenewals.ScheduleRows(dt))
        {
            string[] cells = new string[h.Length];
            for (int i = 0; i < h.Length; i++)
            {
                object x = i < v.Length ? v[i] : null;
                if (x == null) cells[i] = "";
                else if (x is DateTime) cells[i] = HrDocument.Date(x);
                else if (h[i] == "HR remarks") cells[i] = HrDocument.Multiline(x.ToString());
                else cells[i] = HrDocument.E(x.ToString());
            }
            rows.Add(cells);
        }
        bool[] numeric = new bool[h.Length];
        numeric[0] = true;
        numeric[Array.IndexOf(h, "Requested term (months)")] = true;

        StringBuilder b = new StringBuilder();
        b.Append(HrDocument.Table(h, rows, numeric, null));
        b.Append(HrDocument.Signatures(
            "Prepared by (HR Manager)", "", "",
            "Checked by", "", "",
            "Approved by (Secretary to Council)", "", ""));
        return HrDocument.Page("Contract renewal applications for the Governance Council", b.ToString(), o);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════
    /// <summary>Labelled free-text box (HrDocument .box). Empty text prints "Not recorded".</summary>
    private static string Box(string label, string text)
    {
        return "<p><strong>" + HrDocument.E(label) + "</strong></p><div class=\"box\">" +
               (string.IsNullOrEmpty((text ?? "").Trim()) ? "Not recorded." : HrDocument.Multiline(text)) + "</div>";
    }

    /// <summary>Typed e-signature line: "Signed electronically by X on 6 Oct 2026, 14:05".</summary>
    private static string Signed(string verb, string name, object at)
    {
        DateTime? d = D(at);
        if (string.IsNullOrEmpty((name ?? "").Trim()) || !d.HasValue) return "";
        return verb + " electronically by " + name.Trim() + " on " + d.Value.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
    }

    private static string RecLabel(string r)
    {
        return r == "RECOMMEND" ? "Recommended for renewal" : COOPERP_NewScreens_ContractRenewals.RecLabel(r);
    }

    private static string AppraisalStatus(string s)
    {
        switch (s)
        {
            case "PENDING": return "Not started";
            case "EMPLOYEE_IN_PROGRESS": return "In progress";
            case "EMPLOYEE_SUBMITTED": case "SUPERVISOR_IN_PROGRESS": return "With supervisor";
            case "COMPLETED": return "Awaiting HR";
            case "HR_REVIEWED": return "Reviewed by HR";
            default: return COOPERP_NewScreens_ContractRenewalView.Words(s);
        }
    }

    private static string Actor(string tag)
    {
        string s = (tag ?? "").Trim();
        int p = s.IndexOf(':');
        return p > 0 && p < 12 ? s.Substring(p + 1) : s;
    }

    private static string Join(string sep, params string[] parts)
    {
        List<string> keep = new List<string>();
        foreach (string p in parts) if (!string.IsNullOrEmpty((p ?? "").Trim())) keep.Add(p.Trim());
        return string.Join(sep, keep.ToArray());
    }

    private static string Range(object from, object to)
    {
        string a = Dt(from), b = Dt(to);
        if (a == "" && b == "") return "";
        return (a == "" ? "Not recorded" : a) + " to " + (b == "" ? "Not recorded" : b);
    }

    private static string Words(string s) { return COOPERP_NewScreens_ContractRenewalView.Words(s); }
    private static DateTime? D(object v) { return COOPERP_NewScreens_ContractRenewalView.D(v); }
    private static string Dt(object v) { DateTime? d = D(v); return d.HasValue ? d.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : ""; }
    private static string DtT(object v) { DateTime? d = D(v); return d.HasValue ? d.Value.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture) : ""; }
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
