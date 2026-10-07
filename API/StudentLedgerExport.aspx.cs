using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Web;
using System.Web.UI;
using MySql.Data.MySqlClient;

public partial class API_StudentLedgerExport : System.Web.UI.Page
{
    // ===== Public properties consumed by the ASPX template =====
    public string UniversityName = "Muteesa I Royal University";
    public string UniversityLogoUrl = "~/COOPERP/images/welcomelogo.png";
    public string ErrorMessage = "";
    public string RegNo = "";
    public string StudentName = "";
    public string Programme = "";
    public string SessionType = "";
    public string StudentPhotoUrl = "";
    public bool HasStudentPhoto = false;
    public string AcadYear = "";
    public int StudyYear = 0;
    public int Semester = 0;
    public DateTime LedgerStart;
    public DateTime LedgerEnd;
    public decimal TotalBilled = 0;
    public decimal TotalPaid = 0;
    public decimal ClosingBalance = 0;
    public decimal OpeningBalance = 0;
    public int TxCount = 0;

    private string AcctConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["accountsConnectionString"].ConnectionString; }
    }
    private string MainConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        RegNo = (Request.QueryString["reg"] ?? "").Trim();
        if (string.IsNullOrEmpty(RegNo))
        {
            ErrorMessage = "Missing student registration number (?reg=...).";
            return;
        }

        // Date range: the student's FULL history by default (LoadLedger sets the start to
        // the first transaction). The old default began on 01-01 of the previous calendar
        // year, which folded every earlier payment into one "Opening Balance" figure — so a
        // 2024 entrant's 2024 payments never appeared as lines on the statement. A caller
        // can still ask for a window with sDate/eDate; anything before it is carried in the
        // opening balance.
        LedgerEnd = DateTime.Today;
        LedgerStart = DateTime.MinValue;   // resolved in LoadLedger when no sDate is given

        string sDateStr = (Request.QueryString["sDate"] ?? "").Trim();
        string eDateStr = (Request.QueryString["eDate"] ?? "").Trim();
        DateTime tmp;
        if (!string.IsNullOrEmpty(sDateStr) && DateTime.TryParse(sDateStr, out tmp)) LedgerStart = tmp;
        if (!string.IsNullOrEmpty(eDateStr) && DateTime.TryParse(eDateStr, out tmp)) LedgerEnd = tmp;

        try
        {
            LoadStudentInfo();
            LoadLedger();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Error generating ledger: " + ex.Message;
        }
        if (LedgerStart == DateTime.MinValue) LedgerStart = LedgerEnd;
    }

    // ============================================================
    //  Student demographics from acad_student + latest registration
    // ============================================================
    private void LoadStudentInfo()
    {
        using (MySqlConnection conn = new MySqlConnection(MainConnStr))
        {
            conn.Open();

            // Name + programme code + session type
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) AS student_name, " +
                "       COALESCE(s.progid,'') AS progid, " +
                "       COALESCE(p.progname,'N/A') AS progname, " +
                "       COALESCE(s.studsesion,'') AS session_type " +
                "FROM acad_student s " +
                "LEFT JOIN acad_programme p ON p.progcode = s.progid " +
                "WHERE s.regno = @r LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@r", RegNo);
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        StudentName = rdr["student_name"] != DBNull.Value ? rdr["student_name"].ToString().Trim() : RegNo;
                        Programme = rdr["progname"] != DBNull.Value ? rdr["progname"].ToString() : "N/A";
                        SessionType = rdr["session_type"] != DBNull.Value ? rdr["session_type"].ToString() : "";
                    }
                    else
                    {
                        StudentName = RegNo;
                        Programme = "Unknown";
                    }
                }
            }

            StudentPhotoUrl = ResolveStudentPhotoUrl(conn);
            HasStudentPhoto = !string.IsNullOrEmpty(StudentPhotoUrl);

            // Latest registration → acad year, semester, study year
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT acad_year, semester, studyyear " +
                "FROM acad_registration WHERE regno = @r ORDER BY ID DESC LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@r", RegNo);
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        AcadYear = rdr["acad_year"] != DBNull.Value ? rdr["acad_year"].ToString() : "-";
                        Semester = rdr["semester"] != DBNull.Value ? Convert.ToInt32(rdr["semester"]) : 0;
                        StudyYear = rdr["studyyear"] != DBNull.Value ? Convert.ToInt32(rdr["studyyear"]) : 0;
                    }
                }
            }
        }
    }

    private string ResolveStudentPhotoUrl(MySqlConnection conn)
    {
        if (conn == null) return "";

        string photoFile = "";
        try
        {
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT photo FROM acad_student WHERE regno = @r LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@r", RegNo);
                object val = cmd.ExecuteScalar();
                if (val != null && val != DBNull.Value)
                    photoFile = val.ToString().Trim();
            }
        }
        catch
        {
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT photofile FROM acad_student WHERE regno = @r LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@r", RegNo);
                    object val = cmd.ExecuteScalar();
                    if (val != null && val != DBNull.Value)
                        photoFile = val.ToString().Trim();
                }
            }
            catch { }
        }

        if (string.IsNullOrEmpty(photoFile) || photoFile == "-" || photoFile.Equals("default.png", StringComparison.OrdinalIgnoreCase))
            return "";

        string localPathLower = Server.MapPath("~/COOPERP/StudentInfo/photos/" + photoFile);
        if (System.IO.File.Exists(localPathLower))
            return ResolveUrl("~/COOPERP/StudentInfo/photos/" + photoFile);

        string localPathUpper = Server.MapPath("~/COOPERP/StudentInfo/Photos/" + photoFile);
        if (System.IO.File.Exists(localPathUpper))
            return ResolveUrl("~/COOPERP/StudentInfo/Photos/" + photoFile);

        return "https://eadmin.mru.ac.ug/COOPERP/StudentInfo/photos/" + HttpUtility.UrlEncode(photoFile);
    }

    // ============================================================
    //  Ledger transactions (same UNION ALL approach as admin page)
    //  but also fetches teller (Created By) from fin_ledger
    // ============================================================
    private void LoadLedger()
    {
        List<LedgerRow> rows = new List<LedgerRow>();

        using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();

            const string sql =
                "SELECT " +
                "  fl.voucherNo AS voucherNo, " +
                "  fl.transactionDate AS raw_date, " +
                "  DATE_FORMAT(fl.transactionDate,'%d/%m/%Y') AS formated_date, " +
                "  DATE_FORMAT(fl.transactionDate,'%Y-%m-%d %H%i%s') AS sort_key, " +
                "  fl.particulars AS particulars, " +
                "  COALESCE(fl.teller,'') AS teller, " +
                "  CASE WHEN fl.transactionType='DR' THEN fl.transaction_amount ELSE 0 END AS dr_amount, " +
                "  CASE WHEN fl.transactionType='CR' THEN fl.transaction_amount ELSE 0 END AS cr_amount " +
                "FROM fin_ledger fl " +
                "WHERE fl.accountcode = @reg " +
                "  AND fl.transaction_amount > 0 " +
                "UNION ALL " +
                "SELECT " +
                "  CAST(t.TID AS CHAR) AS voucherNo, " +
                "  t.trans_date AS raw_date, " +
                "  DATE_FORMAT(t.trans_date,'%d/%m/%Y') AS formated_date, " +
                "  DATE_FORMAT(t.trans_date,'%Y-%m-%d %H%i%s') AS sort_key, " +
                "  t.detail AS particulars, " +
                "  'Tracking' AS teller, " +
                "  CASE WHEN t.trans_type='Bill' THEN t.amount ELSE 0 END AS dr_amount, " +
                "  CASE WHEN t.trans_type='Payment' THEN t.amount ELSE 0 END AS cr_amount " +
                "FROM fin_studentfeestracking t " +
                "WHERE t.regno = @reg " +
                "  AND t.post_status = 'Posted' " +
                "  AND NOT EXISTS (" +
                "    SELECT 1 FROM fin_ledger fl2 " +
                "    WHERE fl2.accountcode = t.regno " +
                "      AND (" +
                "        fl2.voucherNo = CAST(t.TID AS CHAR)" +
                "        OR fl2.folio = CONCAT('BillNo:', CAST(t.TID AS CHAR))" +
                "        OR (" +
                "          fl2.transaction_amount = t.amount" +
                "          AND DATE(fl2.transactionDate) = DATE(t.trans_date)" +
                "          AND fl2.transactionType = CASE WHEN t.trans_type='Payment' THEN 'CR' ELSE 'DR' END" +
                // Payment double-count fix: migration/imported ledger rows carry the student NAME
                // as particulars while the tracking detail says "Fees Payment ... Airtel Money", so a
                // strict particulars match fails and the same payment is counted twice. Waive the
                // particulars match for PAYMENTS only (bills keep the strict match) — matches the
                // canonical dedup in FinanceEngine / StudentLedgers (Fix B).
                "          AND (t.trans_type = 'Payment' OR fl2.particulars = t.detail OR t.detail IS NULL OR t.detail = '')" +
                "        )" +
                "      )" +
                "  ) " +
                "ORDER BY sort_key ASC, voucherNo ASC";

            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.CommandTimeout = 60;
                cmd.Parameters.AddWithValue("@reg", RegNo);
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        LedgerRow row = new LedgerRow();
                        row.VoucherNo = rdr["voucherNo"] != DBNull.Value ? rdr["voucherNo"].ToString() : "";
                        row.DateDisplay = rdr["formated_date"] != DBNull.Value ? rdr["formated_date"].ToString() : "";
                        row.RawDate = rdr["raw_date"] != DBNull.Value ? Convert.ToDateTime(rdr["raw_date"]) : DateTime.MinValue;
                        row.Particulars = rdr["particulars"] != DBNull.Value ? rdr["particulars"].ToString() : "";
                        row.Teller = rdr["teller"] != DBNull.Value ? rdr["teller"].ToString() : "";
                        row.Debit = rdr["dr_amount"] != DBNull.Value ? Convert.ToDecimal(rdr["dr_amount"]) : 0;
                        row.Credit = rdr["cr_amount"] != DBNull.Value ? Convert.ToDecimal(rdr["cr_amount"]) : 0;
                        rows.Add(row);
                    }
                }
            }
        }

        // No requested start: begin at the first transaction, so every line is shown.
        if (LedgerStart == DateTime.MinValue)
        {
            DateTime first = DateTime.Today;
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].RawDate != DateTime.MinValue && rows[i].RawDate.Date < first) first = rows[i].RawDate.Date;
            LedgerStart = first;
        }

        // Filter to ledger date range
        List<LedgerRow> inRange = new List<LedgerRow>();
        decimal preBilled = 0, prePaid = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].RawDate.Date < LedgerStart.Date)
            {
                preBilled += rows[i].Debit;
                prePaid += rows[i].Credit;
            }
            else if (rows[i].RawDate.Date <= LedgerEnd.Date)
            {
                inRange.Add(rows[i]);
            }
            // rows after LedgerEnd are excluded
        }
        OpeningBalance = preBilled - prePaid; // positive = DR

        // Calculate running balance and totals.
        // Legacy statement behavior: opening balance contributes to DR/CR totals,
        // so summary/closing totals reconcile exactly with closing balance.
        decimal runBal = OpeningBalance;
        TotalBilled = 0;
        TotalPaid = 0;
        if (OpeningBalance > 0)
            TotalBilled += OpeningBalance;
        else if (OpeningBalance < 0)
            TotalPaid += Math.Abs(OpeningBalance);
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < inRange.Count; i++)
        {
            LedgerRow r = inRange[i];
            runBal += r.Debit - r.Credit;
            TotalBilled += r.Debit;
            TotalPaid += r.Credit;

            string drStr = r.Debit > 0 ? r.Debit.ToString("N0") : "-";
            string crStr = r.Credit > 0 ? r.Credit.ToString("N0") : "-";

            sb.Append("<tr>");
            sb.Append("<td>").Append(Enc(r.DateDisplay)).Append("</td>");
            sb.Append("<td>").Append(Enc(r.Teller)).Append("</td>");
            sb.Append("<td class=\"particulars\">").Append(Enc(r.Particulars)).Append("</td>");
            sb.Append("<td>").Append(Enc(r.VoucherNo)).Append("</td>");
            sb.Append("<td class=\"r\">").Append(drStr).Append("</td>");
            sb.Append("<td class=\"r\">").Append(crStr).Append("</td>");
            sb.Append("<td class=\"r\">").Append(FormatBalanceHtml(runBal)).Append("</td>");
            sb.Append("</tr>\n");
        }

        ClosingBalance = runBal;
        TxCount = inRange.Count;
        litRows.Text = sb.ToString();
    }

    // ============================================================
    //  Helpers
    // ============================================================

    public string FormatBalance(decimal bal)
    {
        if (bal == 0) return "0";
        if (bal > 0) return bal.ToString("N0") + "DR";
        return Math.Abs(bal).ToString("N0") + "CR";
    }

    public string FormatBalanceHtml(decimal bal)
    {
        if (bal == 0) return "0";
        if (bal > 0) return "<span class=\"bal-dr\">" + bal.ToString("N0") + "DR</span>";
        return "<span class=\"bal-cr\">" + Math.Abs(bal).ToString("N0") + "CR</span>";
    }

    private string Enc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return Server.HtmlEncode(s);
    }

    private class LedgerRow
    {
        public string VoucherNo;
        public string DateDisplay;
        public DateTime RawDate;
        public string Particulars;
        public string Teller;
        public decimal Debit;
        public decimal Credit;
    }
}
