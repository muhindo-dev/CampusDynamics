using ResultsDataTableAdapters;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Collections;
using System.Data;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.UI;
using System.Web.UI.WebControls;
using DevExpress.XtraReports.UI;
using MySql.Data.MySqlClient;

public partial class API_doc_verification : System.Web.UI.Page
{
    private class CachedPdf
    {
        public byte[] Bytes;
        public string FileName;
        public string PolicyFingerprint;
    }

    private class AccessCriterion
    {
        public string Rule;
        public bool Passed;
        public string Detail;
        public string Threshold;
        public string Actual;
    }

    private class AccessSnapshot
    {
        public bool Success;
        public bool Allowed;
        public bool HasPolicy;
        public string VerdictReason;
        public string Guidance;
        public string PolicyTitle;
        public string PolicyLogic;
        public string PolicyAcademicYear;
        public string PolicySemester;
        public decimal TotalBill;
        public decimal TotalPaid;
        public decimal Balance;
        public decimal AmountOwing;
        public decimal CreditBalance;
        public decimal PercentagePaid;
        public string Currency;
        public List<AccessCriterion> Criteria = new List<AccessCriterion>();
    }

    private string AcadConn
    {
        get
        {
            var cs = ConfigurationManager.ConnectionStrings["campus_dynamics_portalConnectionString"];
            if (cs != null && !string.IsNullOrEmpty(cs.ConnectionString)) return cs.ConnectionString;
            return "server=102.34.160.47;User Id=dbmanager;password=24thdecember1977;database=campus_dynamics;Convert Zero Datetime=True";
        }
    }

    private string AcadRegistrationConn
    {
        get
        {
            var csMain = ConfigurationManager.ConnectionStrings["vacConnectionString"];
            if (csMain != null && !string.IsNullOrEmpty(csMain.ConnectionString))
                return csMain.ConnectionString;

            var csPortal = ConfigurationManager.ConnectionStrings["campus_dynamics_portalConnectionString"];
            if (csPortal != null && !string.IsNullOrEmpty(csPortal.ConnectionString))
            {
                string c = csPortal.ConnectionString;
                if (c.IndexOf("database=campus_dynamics_portal", StringComparison.OrdinalIgnoreCase) >= 0)
                    return c.Replace("database=campus_dynamics_portal", "database=campus_dynamics");

                if (c.IndexOf("Initial Catalog=campus_dynamics_portal", StringComparison.OrdinalIgnoreCase) >= 0)
                    return c.Replace("Initial Catalog=campus_dynamics_portal", "Initial Catalog=campus_dynamics");

                return c;
            }

            return "server=102.34.160.47;User Id=dbmanager;password=24thdecember1977;database=campus_dynamics;Convert Zero Datetime=True";
        }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        // Student Discipline: results withheld (or suspension, for the exam card) stops these
        // documents for the student, whoever asks. Lifted only in Student Discipline.
        string dcDoc = Request["doc"] ?? "";
        if (dcDoc == "ResultStatement" || dcDoc == "Transcript" || dcDoc == "Certificate" || dcDoc == "Student Exam Card")
        {
            string dcReg = (Request["reg"] ?? "").Trim();
            DcClearanceResult dcr = DcClearance.Check(dcReg);
            bool stop = dcDoc == "Student Exam Card" ? dcr.BlocksExamCard : dcr.BlocksDocuments;
            if (stop)
            {
                RenderDisciplinaryDenied(dcDoc == "Student Exam Card" ? "The examination card" : "This document", dcr);
                return;
            }
        }

        if (Request["doc"] == "ResultStatement")
        {
            Transcript RPT = new Transcript();
            acad_GetStudentTranscriptTableAdapter ResStatement = new acad_GetStudentTranscriptTableAdapter();
            acad_universityTableAdapter UNIV = new acad_universityTableAdapter();
            ResultsData DS = new ResultsData();
            ResStatement.Fill(DS.acad_GetStudentTranscript, Request["reg"].ToString());
            UNIV.Fill(DS.acad_university);
            RPT.DataSource = DS;
            ReportViewer1.Report = RPT;
        }
        else if (Request["doc"] == "Transcript")
        {
            acad_GetBatchStudentTranscriptDataTableAdapter ResStatement = new acad_GetBatchStudentTranscriptDataTableAdapter();
            acad_GetBatchStudentTranscript_Col1TableAdapter COL_1 = new acad_GetBatchStudentTranscript_Col1TableAdapter();
            acad_GetBatchStudentTranscript_Col2TableAdapter COL_2 = new acad_GetBatchStudentTranscript_Col2TableAdapter();
            acad_universityTableAdapter UNIV = new acad_universityTableAdapter();
            ResultsData DS = new ResultsData();
            DS.EnforceConstraints = false;
            string regNo = Request["reg"].ToString().Trim();
            ResStatement.SingleTranscript(DS.acad_GetBatchStudentTranscriptData, regNo);
            CertificateDataHelper.EnsureSingleStudentData(DS, regNo);
            //ResStatement.Fill(DS.acad_GetBatchStudentTranscriptData, Session["prog"].ToString(), Session["acad"].ToString());
            UNIV.Fill(DS.acad_university);
            FinalTranscript TRANS_RPT = new FinalTranscript();
            TRANS_RPT.DataSource = (DS);
            ReportViewer1.Report = TRANS_RPT;
            // Response.Write(string.Format("Printing Marksheet PG{0},ACAD {1}", Session["prog"].ToString(), Session["acad"].ToString()));
        }
       else if (Request["doc"] == "Certificate")
        {
            Certificate RPT = new Certificate();
            acad_universityTableAdapter UNIV = new acad_universityTableAdapter();
            acad_GetBatchStudentTranscriptDataTableAdapter ResStatement = new acad_GetBatchStudentTranscriptDataTableAdapter();
            ResultsData DS = new ResultsData();
            DS.EnforceConstraints = false;
            UNIV.Fill(DS.acad_university);
            string regNo = Request["reg"].ToString().Trim();
            ResStatement.SingleTranscript(DS.acad_GetBatchStudentTranscriptData, regNo);
            CertificateDataHelper.EnsureSingleStudentData(DS, regNo);
            RPT.DataSource = DS;
            ReportViewer1.Report = RPT;
        }


        else if (Request["doc"] == "StudentLedger")
        {
            // Redirect to the new HTML-based ledger export
            string reg = Server.UrlEncode(Request["reg"] ?? "");
            string url = "StudentLedgerExport.aspx?reg=" + reg;
            Response.Redirect(url, true);
            return;
        }

        else if (Request["doc"] == "Student Exam Card")
        {
            string reg = (Request["reg"] ?? "").Trim();
            string acadYear = (Request["acadyear"] ?? "").Trim();
            string sem = (Request["sem"] ?? "").Trim();
            string typ = (Request["typ"] ?? "").Trim();
            string fmt = (Request["format"] ?? "").Trim();

            AccessSnapshot policy = GetAccessSnapshot(reg);
            string policyFingerprint = BuildPolicyFingerprint(policy);
            if (policy == null || !policy.Success)
            {
                RenderPolicyDenied(reg, "Unable to validate fee access policy at the moment.", null);
                return;
            }

            if (!policy.HasPolicy)
            {
                RenderPolicyDenied(reg, "Exam clearance card generation is unavailable because Fee Access Policy is currently disabled.", policy);
                return;
            }

            if (!policy.Allowed)
            {
                RenderPolicyDenied(reg, policy.VerdictReason, policy);
                return;
            }

            if (fmt.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            {
                string pdfCacheKey = "examcard:pdf:" + reg.ToUpperInvariant() + ":" + acadYear + ":" + sem + ":" + typ + ":" + policyFingerprint;
                CachedPdf cachedPdf = HttpRuntime.Cache[pdfCacheKey] as CachedPdf;
                if (cachedPdf != null
                    && cachedPdf.Bytes != null
                    && cachedPdf.Bytes.Length > 0
                    && string.Equals(cachedPdf.PolicyFingerprint, policyFingerprint, StringComparison.Ordinal))
                {
                    DisableReportUi();
                    WritePdfResponse(cachedPdf.Bytes, cachedPdf.FileName);
                    return;
                }
            }

            ExamPass RPT = new ExamPass();
            RPT.Parameters["acad"].Value = acadYear;
            RPT.Parameters["sem"].Value = sem;
            RPT.Parameters["reg"].Value = reg;
            RPT.Parameters["prog"].Value = "-";
            RPT.Parameters["entyear"].Value = 0;
            RPT.Parameters["typ"].Value = typ;
            ApplyPolicySummaryToReport(RPT, policy);

            if (fmt.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            {
                using (var ms = new MemoryStream())
                {
                    RPT.ExportToPdf(ms);
                    byte[] bytes = ms.ToArray();
                    string safeReg = reg.Replace("/", "_").Replace("\\", "_").Replace(" ", "_");
                    if (string.IsNullOrWhiteSpace(safeReg)) safeReg = "STUDENT";
                    string filename = "Exam_Clearance_Card_" + safeReg + ".pdf";

                    string pdfCacheKey = "examcard:pdf:" + reg.ToUpperInvariant() + ":" + acadYear + ":" + sem + ":" + typ + ":" + policyFingerprint;
                    HttpRuntime.Cache.Insert(
                        pdfCacheKey,
                        new CachedPdf { Bytes = bytes, FileName = filename, PolicyFingerprint = policyFingerprint },
                        null,
                        DateTime.UtcNow.AddSeconds(45),
                        System.Web.Caching.Cache.NoSlidingExpiration);

                    DisableReportUi();
                    WritePdfResponse(bytes, filename);
                    return;
                }
            }

            ReportViewer1.Report = RPT;
        }
        else if (Request["doc"] == "StudentExamCardVerification")
        {
            RenderExamCardVerification();
            return;
        }
    }

    private void RenderExamCardVerification()
    {
        string reg = (Request["reg"] ?? "").Trim();
        string sid = (Request["sid"] ?? "").Trim();

        DisableReportUi();

        if (string.IsNullOrWhiteSpace(reg))
        {
            WriteHtmlResponse("<html><body style='font-family:Segoe UI,Arial,sans-serif;padding:24px'><h2>Exam Card Verification</h2><p>Missing student ID.</p></body></html>");
            return;
        }

        string acadYear = "-";
        string sem = "-";
        string regStatus = "-";
        string examClearance = "UNKNOWN";
        string examClearanceDate = "-";
        string clearedBy = "-";
        bool found = false;

        try
        {
            using (var conn = new MySqlConnection(AcadRegistrationConn))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT regno, acad_year, semester, regstatus, examClearance, examClearanceDate, clearedBy " +
                    "FROM acad_registration WHERE regno=@r ORDER BY acad_year DESC, CAST(semester AS UNSIGNED) DESC LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@r", reg);
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            found = true;
                            acadYear = rd["acad_year"] != DBNull.Value ? rd["acad_year"].ToString().Trim() : "-";
                            sem = rd["semester"] != DBNull.Value ? rd["semester"].ToString().Trim() : "-";
                            regStatus = rd["regstatus"] != DBNull.Value ? rd["regstatus"].ToString().Trim() : "-";
                            examClearance = rd["examClearance"] != DBNull.Value ? rd["examClearance"].ToString().Trim().ToUpperInvariant() : "UNKNOWN";
                            examClearanceDate = rd["examClearanceDate"] != DBNull.Value ? Convert.ToDateTime(rd["examClearanceDate"]).ToString("yyyy-MM-dd HH:mm") : "-";
                            clearedBy = rd["clearedBy"] != DBNull.Value ? rd["clearedBy"].ToString().Trim() : "-";
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            WriteHtmlResponse("<html><body style='font-family:Segoe UI,Arial,sans-serif;padding:24px'><h2>Exam Card Verification</h2><p>Error: " + HttpUtility.HtmlEncode(ex.Message) + "</p></body></html>");
            return;
        }

        AccessSnapshot policy = GetAccessSnapshot(reg);
        bool policyKnown = policy != null && policy.Success;
        bool policyAllowed = policyKnown && policy.Allowed;

        bool sidMatches = string.IsNullOrWhiteSpace(sid) || sid.Equals(reg, StringComparison.OrdinalIgnoreCase);
        bool clearanceOk = (examClearance == "CLEARED" || examClearance == "PRINTED");
        bool isVerified = found && sidMatches && clearanceOk && policyAllowed;
        string verdictColor = isVerified ? "#166534" : "#991b1b";
        string verdictBg = isVerified ? "#dcfce7" : "#fee2e2";
        string verdictText = isVerified ? "VERIFIED" : "NOT VERIFIED";

        var criteriaHtml = new StringBuilder();
        criteriaHtml.Append("<div style='margin-top:16px;border-top:1px solid #e2e8f0;padding-top:14px;'>");
        criteriaHtml.Append("<div style='font-weight:700;color:#0f172a;margin-bottom:8px;'>Policy Qualification Checks</div>");
        if (!policyKnown)
        {
            criteriaHtml.Append("<div style='font-size:13px;color:#b91c1c;'>Policy evaluation unavailable.</div>");
        }
        else
        {
            criteriaHtml.Append("<div style='font-size:13px;color:#334155;margin-bottom:8px;'><strong>Policy:</strong> " + HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(policy.PolicyTitle) ? "-" : policy.PolicyTitle) +
                                " | <strong>Logic:</strong> " + HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(policy.PolicyLogic) ? "-" : policy.PolicyLogic) +
                                " | <strong>Verdict:</strong> " + HttpUtility.HtmlEncode(policy.Allowed ? "QUALIFIED" : "NOT QUALIFIED") + "</div>");
            criteriaHtml.Append("<div style='font-size:13px;color:#334155;margin-bottom:10px;'><strong>Reason:</strong> " + HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(policy.VerdictReason) ? "-" : policy.VerdictReason) + "</div>");

            criteriaHtml.Append("<div style='font-size:13px;color:#0f172a;margin-bottom:8px;'><strong>Financials</strong>: "
                + "Bill " + HttpUtility.HtmlEncode((policy.Currency ?? "UGX") + " " + policy.TotalBill.ToString("N0"))
                + " | Paid " + HttpUtility.HtmlEncode((policy.Currency ?? "UGX") + " " + policy.TotalPaid.ToString("N0"))
                + " | " + (policy.Balance < 0
                    ? HttpUtility.HtmlEncode("Credit " + (policy.Currency ?? "UGX") + " " + Math.Abs(policy.Balance).ToString("N0"))
                    : HttpUtility.HtmlEncode("Balance " + (policy.Currency ?? "UGX") + " " + policy.Balance.ToString("N0")))
                + " | Paid % " + HttpUtility.HtmlEncode(policy.PercentagePaid.ToString("0.0") + "%") + "</div>");

            if (policy.Criteria != null && policy.Criteria.Count > 0)
            {
                criteriaHtml.Append("<div style='display:grid;gap:6px;'>");
                for (int i = 0; i < policy.Criteria.Count; i++)
                {
                    AccessCriterion c = policy.Criteria[i];
                    string cBg = c.Passed ? "#ecfdf3" : "#fef2f2";
                    string cColor = c.Passed ? "#166534" : "#991b1b";
                    criteriaHtml.Append("<div style='border:1px solid #e2e8f0;background:" + cBg + ";padding:8px 10px;'>"
                        + "<div style='font-size:13px;font-weight:700;color:" + cColor + ";'>" + HttpUtility.HtmlEncode(c.Rule) + " — " + (c.Passed ? "PASS" : "FAIL") + "</div>"
                        + "<div style='font-size:12px;color:#334155;margin-top:2px;'>" + HttpUtility.HtmlEncode(c.Detail) + "</div>"
                        + (string.IsNullOrWhiteSpace(c.Threshold) ? "" : "<div style='font-size:11px;color:#64748b;margin-top:2px;'><strong>Threshold:</strong> " + HttpUtility.HtmlEncode(c.Threshold) + "</div>")
                        + (string.IsNullOrWhiteSpace(c.Actual) ? "" : "<div style='font-size:11px;color:#64748b;'><strong>Actual:</strong> " + HttpUtility.HtmlEncode(c.Actual) + "</div>")
                        + "</div>");
                }
                criteriaHtml.Append("</div>");
            }

            if (!policy.Allowed && !string.IsNullOrWhiteSpace(policy.Guidance))
                criteriaHtml.Append("<div style='margin-top:10px;font-size:12px;color:#b45309;background:#fffbeb;border:1px solid #fde68a;padding:8px 10px;'><strong>Guidance:</strong> " + HttpUtility.HtmlEncode(policy.Guidance) + "</div>");
        }
        criteriaHtml.Append("</div>");

        string html = "<html><head><meta charset='utf-8'><title>Exam Card Verification</title></head><body style='font-family:Segoe UI,Arial,sans-serif;background:#f8fafc;padding:24px;'>"
            + "<div style='max-width:760px;margin:0 auto;background:#fff;border:1px solid #e2e8f0;'>"
            + "<div style='padding:16px 18px;border-bottom:1px solid #e2e8f0;background:#05275C;color:#fff;font-weight:700;'>Exam Clearance Card Verification</div>"
            + "<div style='padding:16px 18px;'>"
            + "<div style='display:inline-block;padding:6px 12px;background:" + verdictBg + ";color:" + verdictColor + ";font-weight:800;letter-spacing:.5px;'>" + verdictText + "</div>"
            + "<div style='margin-top:14px;font-size:14px;line-height:1.7;color:#0f172a;'>"
            + "<div><strong>Student ID:</strong> " + HttpUtility.HtmlEncode(reg) + "</div>"
            + "<div><strong>SID in QR:</strong> " + HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(sid) ? "-" : sid) + "</div>"
            + "<div><strong>Academic Year:</strong> " + HttpUtility.HtmlEncode(acadYear) + "</div>"
            + "<div><strong>Semester:</strong> " + HttpUtility.HtmlEncode(sem) + "</div>"
            + "<div><strong>Registration Status:</strong> " + HttpUtility.HtmlEncode(regStatus) + "</div>"
            + "<div><strong>Exam Clearance:</strong> " + HttpUtility.HtmlEncode(examClearance) + "</div>"
            + "<div><strong>Clearance Date:</strong> " + HttpUtility.HtmlEncode(examClearanceDate) + "</div>"
            + "<div><strong>Cleared By:</strong> " + HttpUtility.HtmlEncode(clearedBy) + "</div>"
            + "<div><strong>Policy Qualification:</strong> " + HttpUtility.HtmlEncode(policyKnown ? (policyAllowed ? "Qualified" : "Not Qualified") : "Unknown") + "</div>"
            + "</div>"
            + criteriaHtml.ToString()
            + "<div style='margin-top:14px;font-size:12px;color:#64748b;'>This verification confirms whether the presented exam clearance card details match current system records.</div>"
            + "</div></div></body></html>";

        WriteHtmlResponse(html);
    }

    private AccessSnapshot GetAccessSnapshot(string regno)
    {
        if (string.IsNullOrWhiteSpace(regno))
            return new AccessSnapshot { Success = false, VerdictReason = "Missing student ID." };

        try
        {
            var data = FinanceEngine.EvaluateAccessPolicy(regno);

            var snapshot = new AccessSnapshot();
            snapshot.Success       = true;
            snapshot.Allowed       = ToBool(data.ContainsKey("access_allowed") ? data["access_allowed"] : null);
            snapshot.HasPolicy     = ToBool(data.ContainsKey("has_policy")     ? data["has_policy"]     : null);
            snapshot.VerdictReason = ToStr(data.ContainsKey("verdict_reason")  ? data["verdict_reason"] : null);
            snapshot.Guidance      = ToStr(data.ContainsKey("guidance")        ? data["guidance"]       : null);

            var pol = AsMap(data.ContainsKey("policy") ? data["policy"] : null);
            if (pol != null)
            {
                snapshot.PolicyTitle       = ToStr(pol.ContainsKey("title")              ? pol["title"]              : null);
                snapshot.PolicyLogic       = ToStr(pol.ContainsKey("combination_logic")  ? pol["combination_logic"]  : null);
                snapshot.PolicyAcademicYear = ToStr(pol.ContainsKey("academic_year")     ? pol["academic_year"]      : null);
                snapshot.PolicySemester    = ToStr(pol.ContainsKey("semester")           ? pol["semester"]           : null);
            }

            var fin = AsMap(data.ContainsKey("finance") ? data["finance"] : null);
            if (fin != null)
            {
                snapshot.TotalBill      = ToDec(fin.ContainsKey("total_bill")      ? fin["total_bill"]      : null);
                snapshot.TotalPaid      = ToDec(fin.ContainsKey("total_paid")      ? fin["total_paid"]      : null);
                snapshot.Balance        = ToDec(fin.ContainsKey("balance")         ? fin["balance"]         : null);
                snapshot.AmountOwing    = ToDec(fin.ContainsKey("amount_owing")    ? fin["amount_owing"]    : null);
                snapshot.CreditBalance  = ToDec(fin.ContainsKey("credit_balance")  ? fin["credit_balance"]  : null);
                snapshot.PercentagePaid = ToDec(fin.ContainsKey("percentage_paid") ? fin["percentage_paid"] : null);
                snapshot.Currency       = ToStr(fin.ContainsKey("currency")        ? fin["currency"]        : null);

                if (snapshot.AmountOwing <= 0m && snapshot.Balance < 0m)
                    snapshot.AmountOwing = Math.Abs(snapshot.Balance);
                if (snapshot.AmountOwing > 0m)
                    snapshot.Balance = snapshot.AmountOwing;
            }

            var criteriaList = data.ContainsKey("criteria")
                ? data["criteria"] as List<Dictionary<string, object>>
                : null;
            if (criteriaList != null)
            {
                foreach (var c in criteriaList)
                {
                    if (c == null) continue;
                    snapshot.Criteria.Add(new AccessCriterion
                    {
                        Rule      = ToStr(c.ContainsKey("rule")         ? c["rule"]         : null),
                        Passed    = ToBool(c.ContainsKey("passed")      ? c["passed"]       : null),
                        Detail    = ToStr(c.ContainsKey("detail")       ? c["detail"]       : null),
                        Threshold = ToStr(c.ContainsKey("threshold")    ? c["threshold"]    : null),
                        Actual    = ToStr(c.ContainsKey("actual_value") ? c["actual_value"] : null)
                    });
                }
            }

            return snapshot;
        }
        catch (Exception ex)
        {
            return new AccessSnapshot { Success = false, VerdictReason = "Unable to validate fee access policy: " + ex.Message };
        }
    }

    private static bool ToBool(object value)
    {
        if (value == null) return false;
        if (value is bool) return (bool)value;
        bool b;
        return bool.TryParse(value.ToString(), out b) && b;
    }

    private static decimal ToDec(object value)
    {
        if (value == null) return 0m;
        decimal d;
        return decimal.TryParse(value.ToString(), out d) ? d : 0m;
    }

    private static string ToStr(object value)
    {
        return value == null ? "" : value.ToString();
    }

    private static Dictionary<string, object> AsMap(object value)
    {
        return value as Dictionary<string, object>;
    }

    private static ArrayList AsList(object value)
    {
        return value as ArrayList;
    }

    private static string BuildPolicyFingerprint(AccessSnapshot snapshot)
    {
        if (snapshot == null || !snapshot.Success)
            return "policy-unavailable";

        int passed = 0;
        int failed = 0;
        if (snapshot.Criteria != null)
        {
            for (int i = 0; i < snapshot.Criteria.Count; i++)
            {
                if (snapshot.Criteria[i].Passed) passed++; else failed++;
            }
        }

        string key = (snapshot.PolicyTitle ?? "") + "|"
            + (snapshot.PolicyLogic ?? "") + "|"
            + (snapshot.Allowed ? "1" : "0") + "|"
            + passed + "|"
            + failed + "|"
            + snapshot.PercentagePaid.ToString("0.0") + "|"
            + snapshot.TotalBill.ToString("0") + "|"
            + snapshot.TotalPaid.ToString("0");

        return HttpUtility.UrlEncode(key);
    }

    private void ApplyPolicySummaryToReport(ExamPass rpt, AccessSnapshot snapshot)
    {
        if (rpt == null || snapshot == null || !snapshot.Success) return;

        XRLabel lbl = rpt.FindControl("xrLabel10", true) as XRLabel;
        XRLabel badge = rpt.FindControl("xrPolicyStatusBadge", true) as XRLabel;
        XRLabel financeLbl = rpt.FindControl("xrFinancialSummary", true) as XRLabel;
        XRLabel qualLbl = rpt.FindControl("xrQualificationSummary", true) as XRLabel;
        if (lbl != null)
        {
            lbl.CanGrow = true;
            lbl.Multiline = true;
            lbl.WordWrap = true;
            lbl.Font = new System.Drawing.Font("Tahoma", 8F, System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic);
            lbl.LocationFloat = new DevExpress.Utils.PointFloat(214F, 124F);
            lbl.SizeF = new System.Drawing.SizeF(430F, 22F);
            lbl.Text = "This permit is valid for ONLY [study_system] [sem], [acad]";
        }

        int passedCount = 0;
        int failedCount = 0;
        var failedRules = new List<string>();
        for (int i = 0; i < snapshot.Criteria.Count; i++)
        {
            AccessCriterion c = snapshot.Criteria[i];
            if (c.Passed)
            {
                passedCount++;
            }
            else
            {
                failedCount++;
                if (!string.IsNullOrWhiteSpace(c.Rule))
                    failedRules.Add(c.Rule);
            }
        }

        string currency = string.IsNullOrWhiteSpace(snapshot.Currency) ? "UGX" : snapshot.Currency;
        if (financeLbl != null)
        {
            decimal outstanding = snapshot.AmountOwing > 0m
                ? snapshot.AmountOwing
                : (snapshot.Balance > 0m ? snapshot.Balance : 0m);

            decimal credit = snapshot.CreditBalance > 0m
                ? snapshot.CreditBalance
                : (snapshot.Balance < 0m ? Math.Abs(snapshot.Balance) : 0m);

            string balLabel = outstanding > 0m
                ? "Outstanding " + currency + " " + outstanding.ToString("N0")
                : (credit > 0m ? "Credit " + currency + " " + credit.ToString("N0") : "Outstanding " + currency + " 0");

            financeLbl.Text = "Financials: "
                + currency + " Bill " + snapshot.TotalBill.ToString("N0")
                + "   |   " + currency + " Paid " + snapshot.TotalPaid.ToString("N0")
                + "   |   " + balLabel
                + "   |   Paid " + snapshot.PercentagePaid.ToString("0.0") + "%";
        }

        string policyLine = string.IsNullOrWhiteSpace(snapshot.PolicyTitle)
            ? "Policy: Not specified"
            : "Policy: " + snapshot.PolicyTitle + (string.IsNullOrWhiteSpace(snapshot.PolicyLogic) ? "" : " (" + snapshot.PolicyLogic + ")");

        if (badge != null)
        {
            if (snapshot.Allowed)
            {
                badge.Text = "QUALIFICATION: APPROVED";
                badge.BackColor = System.Drawing.Color.FromArgb(220, 252, 231);
                badge.ForeColor = System.Drawing.Color.FromArgb(22, 101, 52);
            }
            else
            {
                badge.Text = "QUALIFICATION: NOT APPROVED";
                badge.BackColor = System.Drawing.Color.FromArgb(254, 226, 226);
                badge.ForeColor = System.Drawing.Color.FromArgb(153, 27, 27);
            }
        }

        if (qualLbl != null)
        {
            string checksLine = "Checks: Passed " + passedCount + " | Failed " + failedCount;
            if (!snapshot.Allowed && failedRules.Count > 0)
                checksLine += " | Pending: " + string.Join(", ", failedRules.ToArray());

            qualLbl.Text = policyLine + "  •  " + checksLine;

            if (snapshot.Allowed)
            {
                qualLbl.BackColor = System.Drawing.Color.FromArgb(240, 253, 244);
                qualLbl.ForeColor = System.Drawing.Color.FromArgb(22, 101, 52);
            }
            else
            {
                qualLbl.BackColor = System.Drawing.Color.FromArgb(255, 241, 242);
                qualLbl.ForeColor = System.Drawing.Color.FromArgb(127, 29, 29);
            }
        }

        // ── Criteria table ────────────────────────────────────────────────
        // Build a dynamic XRTable with one header + one row per criterion,
        // appended inside xrPolicyPanel. Panel and GroupHeader1 are resized.
        if (snapshot.Criteria != null && snapshot.Criteria.Count > 0)
        {
            DevExpress.XtraReports.UI.XRPanel panel =
                rpt.FindControl("xrPolicyPanel", true) as DevExpress.XtraReports.UI.XRPanel;
            if (panel != null)
            {
                float panelW    = panel.WidthF;
                float tableY    = 113f;   // just below xrQualificationSummary
                float rowH      = 20f;
                float headerH   = 19f;
                float colW0     = 58f;    // STATUS
                float colW1     = 200f;   // RULE
                float colW2     = panelW - colW0 - colW1 - 10f; // DETAIL

                // Colours
                var colHeader  = System.Drawing.Color.FromArgb(30, 64, 175);   // blue header bg
                var colHdrFg   = System.Drawing.Color.White;
                var colPassBg  = System.Drawing.Color.FromArgb(240, 253, 244);
                var colFailBg  = System.Drawing.Color.FromArgb(255, 241, 242);
                var colPassFg  = System.Drawing.Color.FromArgb(22, 101, 52);
                var colFailFg  = System.Drawing.Color.FromArgb(153, 27, 27);
                var colMut     = System.Drawing.Color.FromArgb(71, 84, 103);
                var colBd      = System.Drawing.Color.FromArgb(224, 224, 224);
                var fontHdr    = new System.Drawing.Font("Tahoma", 8f, System.Drawing.FontStyle.Bold);
                var fontCell   = new System.Drawing.Font("Tahoma", 7.5f);
                var fontStatus = new System.Drawing.Font("Tahoma", 7.5f, System.Drawing.FontStyle.Bold);
                var pad        = new DevExpress.XtraPrinting.PaddingInfo(4, 2, 0, 0, 100f);

                // Build XRTable — one header row + N data rows
                int totalRows  = 1 + snapshot.Criteria.Count;
                float tableH   = headerH + snapshot.Criteria.Count * rowH;

                var tbl = new DevExpress.XtraReports.UI.XRTable();
                tbl.Dpi           = 100f;
                tbl.LocationFloat = new DevExpress.Utils.PointFloat(5f, tableY);
                tbl.SizeF         = new System.Drawing.SizeF(panelW - 10f, tableH);
                tbl.Borders       = DevExpress.XtraPrinting.BorderSide.None;
                ((System.ComponentModel.ISupportInitialize)tbl).BeginInit();

                // Header row
                var hdr = new DevExpress.XtraReports.UI.XRTableRow();
                hdr.Dpi     = 100f;
                hdr.HeightF = headerH;
                hdr.BackColor = colHeader;
                hdr.StylePriority.UseBackColor = false;

                string[] hdrTitles = { "STATUS", "RULE / CHECK", "DETAIL" };
                float[]  hdrWidths = { colW0, colW1, colW2 };
                for (int h = 0; h < 3; h++)
                {
                    var hCell = new DevExpress.XtraReports.UI.XRTableCell();
                    hCell.Dpi      = 100f;
                    hCell.Weight   = hdrWidths[h];
                    hCell.Text     = hdrTitles[h];
                    hCell.Font     = fontHdr;
                    hCell.ForeColor = colHdrFg;
                    hCell.Padding  = pad;
                    hCell.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
                    hCell.StylePriority.UseFont       = false;
                    hCell.StylePriority.UseForeColor  = false;
                    hCell.StylePriority.UsePadding    = false;
                    hCell.StylePriority.UseTextAlignment = false;
                    hdr.Cells.Add(hCell);
                }
                tbl.Rows.Add(hdr);

                // Data rows
                for (int ci = 0; ci < snapshot.Criteria.Count; ci++)
                {
                    AccessCriterion c = snapshot.Criteria[ci];
                    bool pass = c.Passed;

                    var row = new DevExpress.XtraReports.UI.XRTableRow();
                    row.Dpi      = 100f;
                    row.HeightF  = rowH;
                    row.BackColor = pass ? colPassBg : colFailBg;
                    row.StylePriority.UseBackColor = false;

                    // STATUS cell
                    var cStatus = new DevExpress.XtraReports.UI.XRTableCell();
                    cStatus.Dpi     = 100f;
                    cStatus.Weight  = colW0;
                    cStatus.Text    = pass ? "PASS" : "FAIL";
                    cStatus.Font    = fontStatus;
                    cStatus.ForeColor = pass ? colPassFg : colFailFg;
                    cStatus.Padding = pad;
                    cStatus.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
                    cStatus.StylePriority.UseFont       = false;
                    cStatus.StylePriority.UseForeColor  = false;
                    cStatus.StylePriority.UsePadding    = false;
                    cStatus.StylePriority.UseTextAlignment = false;

                    // RULE cell
                    var cRule = new DevExpress.XtraReports.UI.XRTableCell();
                    cRule.Dpi     = 100f;
                    cRule.Weight  = colW1;
                    cRule.Text    = string.IsNullOrWhiteSpace(c.Rule) ? "-" : c.Rule;
                    cRule.Font    = new System.Drawing.Font("Tahoma", 7.5f, System.Drawing.FontStyle.Bold);
                    cRule.ForeColor = pass ? colPassFg : colFailFg;
                    cRule.Padding = pad;
                    cRule.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
                    cRule.StylePriority.UseFont       = false;
                    cRule.StylePriority.UseForeColor  = false;
                    cRule.StylePriority.UsePadding    = false;
                    cRule.StylePriority.UseTextAlignment = false;

                    // DETAIL cell
                    var cDetail = new DevExpress.XtraReports.UI.XRTableCell();
                    cDetail.Dpi     = 100f;
                    cDetail.Weight  = colW2;
                    cDetail.Text    = string.IsNullOrWhiteSpace(c.Detail) ? "-" : c.Detail
                        + (string.IsNullOrWhiteSpace(c.Actual) ? "" : "  [Actual: " + c.Actual + "]");
                    cDetail.Font    = fontCell;
                    cDetail.ForeColor = colMut;
                    cDetail.Padding = pad;
                    cDetail.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
                    cDetail.StylePriority.UseFont       = false;
                    cDetail.StylePriority.UseForeColor  = false;
                    cDetail.StylePriority.UsePadding    = false;
                    cDetail.StylePriority.UseTextAlignment = false;

                    row.Cells.Add(cStatus);
                    row.Cells.Add(cRule);
                    row.Cells.Add(cDetail);
                    tbl.Rows.Add(row);
                }

                ((System.ComponentModel.ISupportInitialize)tbl).EndInit();
                panel.Controls.Add(tbl);

                // Resize panel to fit the table plus bottom padding
                float newPanelH = tableY + tableH + 8f;
                panel.SizeF = new System.Drawing.SizeF(panel.WidthF, newPanelH);

                // Resize GroupHeader1 by the same delta
                DevExpress.XtraReports.UI.GroupHeaderBand gh =
                    rpt.Bands[DevExpress.XtraReports.UI.BandKind.GroupHeader] as
                    DevExpress.XtraReports.UI.GroupHeaderBand;
                if (gh != null)
                {
                    float delta = newPanelH - 118f;
                    gh.HeightF += delta;
                }
            }
        }
    }

    /// <summary>Plain refusal for a document withheld under a disciplinary decision.</summary>
    private void RenderDisciplinaryDenied(string what, DcClearanceResult dcr)
    {
        DisableReportUi();
        Response.Clear();
        Response.StatusCode = 403;
        Response.ContentType = "text/html; charset=utf-8";
        Response.Cache.SetCacheability(HttpCacheability.NoCache);
        Response.Cache.SetNoStore();
        string contact = DcSettings.ContactOffice + (DcSettings.ContactDetails == "" ? "" : " (" + DcSettings.ContactDetails + ")");
        Response.Write("<html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1' /><title>Document withheld</title></head>" +
            "<body style=\"margin:0;background:#F5F7FA;font-family:Segoe UI,Arial,sans-serif;color:#1a1a2e\">" +
            "<div style=\"max-width:560px;margin:40px auto;background:#fff;border:1px solid #E0E5ED\">" +
            "<div style=\"background:#05275C;color:#fff;padding:14px 18px;font-weight:600\">Muteesa I Royal University</div>" +
            "<div style=\"padding:18px;font-size:14px;line-height:1.6\"><p style=\"margin:0 0 10px;font-weight:600;color:#05275C\">" + HttpUtility.HtmlEncode(what) + " is withheld</p>" +
            "<p style=\"margin:0 0 10px\">It is withheld under a disciplinary decision" + (dcr.CaseNos.Count > 0 ? " (case " + HttpUtility.HtmlEncode(string.Join(", ", dcr.CaseNos.ToArray())) + ")" : "") + ".</p>" +
            "<p style=\"margin:0\">Contact the " + HttpUtility.HtmlEncode(contact) + ".</p></div></div></body></html>");
        Response.Flush();
        Response.SuppressContent = true;
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    private void RenderPolicyDenied(string regno, string reason, AccessSnapshot snapshot)
    {
        DisableReportUi();
        Response.Clear();
        Response.ContentType = "text/html; charset=utf-8";
        Response.Cache.SetCacheability(HttpCacheability.NoCache);
        Response.Cache.SetNoStore();

        string finance = "";
        string checks = "";
        int passed = 0;
        int failed = 0;
        if (snapshot != null && snapshot.Success)
        {
            finance = "<div class='fac-finance'>"
                + "<div class='fac-finance__item'><div class='fac-finance__val'>" + HttpUtility.HtmlEncode((snapshot.Currency ?? "UGX") + " " + snapshot.TotalBill.ToString("N0")) + "</div><div class='fac-finance__label'>Total Bill</div></div>"
                + "<div class='fac-finance__item'><div class='fac-finance__val'>" + HttpUtility.HtmlEncode((snapshot.Currency ?? "UGX") + " " + snapshot.TotalPaid.ToString("N0")) + "</div><div class='fac-finance__label'>Total Paid</div></div>"
                + "<div class='fac-finance__item'><div class='fac-finance__val'>" + HttpUtility.HtmlEncode((snapshot.Currency ?? "UGX") + " " + (snapshot.AmountOwing > 0 ? snapshot.AmountOwing : snapshot.Balance).ToString("N0")) + "</div><div class='fac-finance__label'>Outstanding Balance</div></div>"
                + "<div class='fac-finance__item'><div class='fac-finance__val'>" + HttpUtility.HtmlEncode(snapshot.PercentagePaid.ToString("0.0") + "%") + "</div><div class='fac-finance__label'>Percentage Paid</div></div>"
                + "</div>";

            if (snapshot.Criteria != null && snapshot.Criteria.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append("<div class='fac-criteria-wrap'><div class='fac-criteria-title'>Qualification Rule Breakdown</div><div class='fac-criteria'>");
                foreach (AccessCriterion c in snapshot.Criteria)
                {
                    if (c.Passed) passed++; else failed++;
                    string cls = c.Passed ? "fac-criterion fac-criterion--pass" : "fac-criterion fac-criterion--fail";
                    string state = c.Passed ? "PASS" : "FAIL";
                    string th = string.IsNullOrWhiteSpace(c.Threshold) ? "" : "<div class='fac-criterion__meta'><strong>Threshold:</strong> " + HttpUtility.HtmlEncode(c.Threshold) + "</div>";
                    string av = string.IsNullOrWhiteSpace(c.Actual) ? "" : "<div class='fac-criterion__meta'><strong>Actual:</strong> " + HttpUtility.HtmlEncode(c.Actual) + "</div>";
                    sb.Append("<div class='" + cls + "'><div class='fac-criterion__chip'>" + state + "</div><div class='fac-criterion__body'><div class='fac-criterion__rule'>" + HttpUtility.HtmlEncode(c.Rule) + "</div><div class='fac-criterion__detail'>" + HttpUtility.HtmlEncode(c.Detail) + "</div>" + th + av + "</div></div>");
                }
                sb.Append("</div></div>");
                checks = sb.ToString();
            }
        }

        string summaryLine = "";
        if (snapshot != null && snapshot.Success)
        {
            summaryLine = "<div class='fac-meta'><span><strong>Student ID:</strong> " + HttpUtility.HtmlEncode(regno) + "</span>"
                + "<span><strong>Policy:</strong> " + HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(snapshot.PolicyTitle) ? "Active Fee Access Policy" : snapshot.PolicyTitle) + "</span>"
                + "<span><strong>Checks Passed:</strong> " + passed + "</span>"
                + "<span><strong>Checks Failed:</strong> " + failed + "</span></div>";
        }
        else
        {
            summaryLine = "<div class='fac-meta'><span><strong>Student ID:</strong> " + HttpUtility.HtmlEncode(regno) + "</span></div>";
        }

        string guidance = (snapshot != null && snapshot.Success && !string.IsNullOrWhiteSpace(snapshot.Guidance))
            ? HttpUtility.HtmlEncode(snapshot.Guidance)
            : "Please clear the outstanding policy requirements, then generate the card again from the student portal.";

        string html = "<html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1' /><title>Exam Card Access Denied</title>"
            + "<style>"
            + ":root{--pri:#05275C;--acc:#174DA4;--ok:#047857;--okbg:#f0faf3;--err:#b91c1c;--errbg:#fef4f2;--bd:#e0e0e0;--txt:#1a1a2e;--mut:#667085;}"
            + "*{box-sizing:border-box}body{margin:0;font-family:Segoe UI,Arial,sans-serif;background:#f5f7fb;color:var(--txt)}"
            + ".fac-wrap{max-width:860px;margin:28px auto;padding:0 14px}"
            + ".fac-page-header{display:flex;align-items:center;justify-content:space-between;gap:8px;padding:14px 18px;background:#fff;border-bottom:2px solid var(--acc);margin-bottom:16px}"
            + ".fac-title{font-size:17px;font-weight:700}.fac-sub{font-size:12px;color:#555;margin-top:2px}"
            + ".fac-card{background:#fff;border:1px solid var(--bd)}"
            + ".fac-verdict{padding:16px 18px;background:var(--errbg);border-bottom:1px solid var(--bd)}"
            + ".fac-badge{display:inline-block;padding:6px 10px;background:#fee2e2;color:#991b1b;font-weight:800;letter-spacing:.4px;font-size:12px}"
            + ".fac-reason{margin-top:10px;font-size:13px;color:#344054;line-height:1.6}"
            + ".fac-meta{display:flex;flex-wrap:wrap;gap:12px;padding:10px 18px;background:#f9fafb;border-bottom:1px solid var(--bd);font-size:12px;color:#555}"
            + ".fac-meta strong{color:#111827}"
            + ".fac-finance{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));border-bottom:1px solid var(--bd)}"
            + ".fac-finance__item{padding:12px 10px;text-align:center;border-right:1px solid var(--bd)}.fac-finance__item:last-child{border-right:none}"
            + ".fac-finance__val{font-size:16px;font-weight:700}.fac-finance__label{font-size:11px;color:#667085;margin-top:2px}"
            + ".fac-criteria-wrap{padding:12px 18px}.fac-criteria-title{font-weight:700;font-size:13px;margin-bottom:8px}"
            + ".fac-criteria{display:grid;gap:8px}"
            + ".fac-criterion{display:flex;align-items:flex-start;gap:10px;border:1px solid var(--bd);padding:9px 10px}"
            + ".fac-criterion--pass{background:#ecfdf3}.fac-criterion--fail{background:#fef2f2}"
            + ".fac-criterion__chip{font-size:11px;font-weight:800;padding:4px 7px;border-radius:2px;background:#fff;border:1px solid #ddd;min-width:52px;text-align:center}"
            + ".fac-criterion--pass .fac-criterion__chip{color:#166534;border-color:#86efac}.fac-criterion--fail .fac-criterion__chip{color:#991b1b;border-color:#fca5a5}"
            + ".fac-criterion__body{flex:1}.fac-criterion__rule{font-size:13px;font-weight:700}.fac-criterion__detail{font-size:12px;color:#475467;margin-top:2px}.fac-criterion__meta{font-size:11px;color:#667085;margin-top:2px}"
            + ".fac-guidance{margin:0 18px 14px;background:#fffbeb;border:1px solid #fde68a;color:#7c5e10;padding:10px 12px;font-size:12px}"
            + ".fac-actions{display:flex;flex-wrap:wrap;gap:10px;padding:0 18px 18px}"
            + ".fac-btn{border:1px solid #d0d5dd;background:#fff;color:#111827;padding:8px 12px;font-size:12px;font-weight:600;cursor:pointer}.fac-btn:hover{background:#f8fafc}"
            + ".fac-btn--pri{background:var(--pri);border-color:var(--pri);color:#fff}.fac-btn--pri:hover{background:var(--acc)}"
            + "@media(max-width:640px){.fac-finance{grid-template-columns:1fr 1fr}}"
            + "</style>"
            + "</head><body><div class='fac-wrap'>"
            + "<div class='fac-page-header'><div><div class='fac-title'>Exam Clearance Card Generation Blocked</div><div class='fac-sub'>Policy evaluation result for exam card generation request</div></div></div>"
            + "<div class='fac-card'>"
            + "<div class='fac-verdict'><span class='fac-badge'>ACCESS DENIED</span><div class='fac-reason'><strong>Reason:</strong> " + HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(reason) ? "You do not currently meet the active policy requirements." : reason) + "</div></div>"
            + summaryLine
            + finance
            + checks
            + "<div class='fac-guidance'><strong>Next Step:</strong> " + guidance + "</div>"
            + "<div class='fac-actions'><button class='fac-btn' onclick='window.location.reload()'>Retry</button></div>"
            + "</div></div>"
            + "<script>(function(){var p=new URLSearchParams(window.location.search);var reg=p.get('reg');if(reg){var e=document.createElement('div');e.style.cssText='padding:0 18px 16px;color:#667085;font-size:12px';e.innerHTML='Student ID: <strong style=\"color:#111827\">'+reg.replace(/</g,'&lt;')+'</strong>';var card=document.querySelector('.fac-card');if(card) card.insertBefore(e, card.children[1]);}})();</script>"
            + "</body></html>";

        WriteHtmlResponse(html);
    }

    private string DownloadJsonWithTimeout(string url, int timeoutMs)
    {
        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
        req.Method = "GET";
        req.Timeout = timeoutMs;
        req.ReadWriteTimeout = timeoutMs;
        req.Proxy = null;
        req.KeepAlive = false;

        using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
        using (Stream stream = res.GetResponseStream())
        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
        {
            return reader.ReadToEnd();
        }
    }

    private void DisableReportUi()
    {
        if (ReportViewer1 != null) ReportViewer1.Visible = false;
        if (ReportToolbar1 != null) ReportToolbar1.Visible = false;
        if (ASPxRoundPanel1 != null) ASPxRoundPanel1.Visible = false;
    }

    private void WriteHtmlResponse(string html)
    {
        DisableReportUi();
        Response.Clear();
        Response.ContentType = "text/html; charset=utf-8";
        Response.TrySkipIisCustomErrors = true;
        Response.Write(html);
        Response.Flush();
        try { Response.End(); }
        catch (System.Threading.ThreadAbortException) { }
    }

    private void WritePdfResponse(byte[] bytes, string fileName)
    {
        DisableReportUi();
        Response.Clear();
        Response.ContentType = "application/pdf";
        Response.TrySkipIisCustomErrors = true;
        Response.AddHeader("Content-Disposition", "inline; filename=" + fileName);
        Response.BinaryWrite(bytes);
        Response.Flush();
        try { Response.End(); }
        catch (System.Threading.ThreadAbortException) { }
    }
}