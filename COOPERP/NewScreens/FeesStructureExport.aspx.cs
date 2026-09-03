using System;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

/// <summary>
/// FeesStructureExport — Downloads all programme fee structures as a native Excel SpreadsheetML file.
/// No third-party libraries required. Opens directly in Excel 2007+.
/// Mirrors the data shown in FeesStructurePrint.aspx but in spreadsheet format.
/// </summary>
public partial class COOPERP_NewScreens_FeesStructureExport : System.Web.UI.Page
{
    private const int TOTAL_COLS = 33; // Faculty|Prog|Code|Status | 7×Year1 | 7×Year2 | 7×Year3 | 7×Year4 | Grand

    private string AcctConnStr
    {
        get
        {
            var cs = ConfigurationManager.ConnectionStrings["accountsConnectionString"];
            return cs != null ? cs.ConnectionString
                : "server=102.34.160.47;database=campus_dynamics_accounts;uid=loaboronet_loaborolonet;pwd=L04B0R0143;charset=utf8;";
        }
    }

    private string MainConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    // ================================================================
    // PAGE LOAD — stream Excel file directly
    // ================================================================

    protected void Page_Load(object sender, EventArgs e)
    {
        string filename = "FeeStructures_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xls";

        Response.Clear();
        Response.ContentType = "application/vnd.ms-excel";
        Response.Charset = "UTF-8";
        Response.ContentEncoding = Encoding.UTF8;
        Response.AddHeader("Content-Disposition", "attachment; filename=\"" + filename + "\"");
        Response.BinaryWrite(Encoding.UTF8.GetPreamble()); // UTF-8 BOM so Excel reads accented chars correctly

        WriteExcel();

        Response.Flush();
        try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
    }

    // ================================================================
    // EXCEL GENERATION — SpreadsheetML (Office 2003 XML)
    // ================================================================

    private void WriteExcel()
    {
        DataTable dt = GetAllFeeStructures();
        string instName = GetSetting("InstitutionName", "Muteesa I Royal University");
        string acadYear = GetCurrentAcademicYear();
        string genBy    = GetCurrentUser();
        string genDate  = DateTime.Now.ToString("dd MMMM yyyy, hh:mm tt");

        var sb = new StringBuilder(1024 * 64);

        // ── XML declaration ──────────────────────────────────────
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
        sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
        sb.AppendLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
        sb.AppendLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
        sb.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");

        // ── Styles ───────────────────────────────────────────────
        sb.AppendLine("<Styles>");

        // Default
        sb.AppendLine("<Style ss:ID=\"Default\" ss:Name=\"Normal\">");
        sb.AppendLine("  <Font ss:FontName=\"Calibri\" ss:Size=\"10\"/>");
        sb.AppendLine("  <Alignment ss:Vertical=\"Center\"/>");
        sb.AppendLine("</Style>");

        // s_title — merged heading
        sb.AppendLine("<Style ss:ID=\"s_title\">");
        sb.AppendLine("  <Font ss:FontName=\"Calibri\" ss:Bold=\"1\" ss:Size=\"14\" ss:Color=\"#05275C\"/>");
        sb.AppendLine("  <Alignment ss:Horizontal=\"Center\" ss:Vertical=\"Center\"/>");
        sb.AppendLine("  <Interior ss:Color=\"#EEF2FA\" ss:Pattern=\"Solid\"/>");
        sb.AppendLine("</Style>");

        // s_info — generated-by line
        sb.AppendLine("<Style ss:ID=\"s_info\">");
        sb.AppendLine("  <Font ss:FontName=\"Calibri\" ss:Size=\"9\" ss:Italic=\"1\" ss:Color=\"#666666\"/>");
        sb.AppendLine("  <Alignment ss:Horizontal=\"Left\" ss:Vertical=\"Center\"/>");
        sb.AppendLine("  <Interior ss:Color=\"#EEF2FA\" ss:Pattern=\"Solid\"/>");
        sb.AppendLine("</Style>");

        // s_group — year group span headers (navy)
        sb.AppendLine("<Style ss:ID=\"s_group\">");
        sb.AppendLine("  <Font ss:FontName=\"Calibri\" ss:Bold=\"1\" ss:Size=\"10\" ss:Color=\"#FFFFFF\"/>");
        sb.AppendLine("  <Interior ss:Color=\"#05275C\" ss:Pattern=\"Solid\"/>");
        sb.AppendLine("  <Alignment ss:Horizontal=\"Center\" ss:Vertical=\"Center\"/>");
        sb.AppendLine("  <Borders><Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#FFFFFF\"/></Borders>");
        sb.AppendLine("</Style>");

        // s_hdr — column headers (blue)
        sb.AppendLine("<Style ss:ID=\"s_hdr\">");
        sb.AppendLine("  <Font ss:FontName=\"Calibri\" ss:Bold=\"1\" ss:Size=\"9\" ss:Color=\"#FFFFFF\"/>");
        sb.AppendLine("  <Interior ss:Color=\"#174DA4\" ss:Pattern=\"Solid\"/>");
        sb.AppendLine("  <Alignment ss:Horizontal=\"Center\" ss:Vertical=\"Center\" ss:WrapText=\"1\"/>");
        sb.AppendLine("  <Borders><Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"2\" ss:Color=\"#FFFFFF\"/></Borders>");
        sb.AppendLine("</Style>");

        // s_faculty — faculty separator row (dark navy)
        sb.AppendLine("<Style ss:ID=\"s_faculty\">");
        sb.AppendLine("  <Font ss:FontName=\"Calibri\" ss:Bold=\"1\" ss:Size=\"9\" ss:Color=\"#FFFFFF\"/>");
        sb.AppendLine("  <Interior ss:Color=\"#1a3a6e\" ss:Pattern=\"Solid\"/>");
        sb.AppendLine("  <Alignment ss:Vertical=\"Center\"/>");
        sb.AppendLine("</Style>");

        // s_text — regular text cell (active row)
        sb.AppendLine("<Style ss:ID=\"s_text\">");
        sb.AppendLine("  <Font ss:FontName=\"Calibri\" ss:Size=\"9\"/>");
        sb.AppendLine("  <Alignment ss:Vertical=\"Center\"/>");
        sb.AppendLine("  <Borders><Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#E8ECF2\"/></Borders>");
        sb.AppendLine("</Style>");

        // s_num — numeric cell with comma format (active row)
        sb.AppendLine("<Style ss:ID=\"s_num\">");
        sb.AppendLine("  <Font ss:FontName=\"Calibri\" ss:Size=\"9\"/>");
        sb.AppendLine("  <NumberFormat ss:Format=\"#,##0\"/>");
        sb.AppendLine("  <Alignment ss:Horizontal=\"Right\" ss:Vertical=\"Center\"/>");
        sb.AppendLine("  <Borders><Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#E8ECF2\"/></Borders>");
        sb.AppendLine("</Style>");

        // s_zero — 0 in gray (year not applicable)
        sb.AppendLine("<Style ss:ID=\"s_zero\">");
        sb.AppendLine("  <Font ss:FontName=\"Calibri\" ss:Size=\"9\" ss:Color=\"#BBBBBB\"/>");
        sb.AppendLine("  <NumberFormat ss:Format=\"#,##0\"/>");
        sb.AppendLine("  <Alignment ss:Horizontal=\"Right\" ss:Vertical=\"Center\"/>");
        sb.AppendLine("  <Borders><Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#E8ECF2\"/></Borders>");
        sb.AppendLine("</Style>");

        // s_total — year subtotal (bold, light blue bg)
        sb.AppendLine("<Style ss:ID=\"s_total\">");
        sb.AppendLine("  <Font ss:FontName=\"Calibri\" ss:Bold=\"1\" ss:Size=\"9\" ss:Color=\"#174DA4\"/>");
        sb.AppendLine("  <Interior ss:Color=\"#EBF0FA\" ss:Pattern=\"Solid\"/>");
        sb.AppendLine("  <NumberFormat ss:Format=\"#,##0\"/>");
        sb.AppendLine("  <Alignment ss:Horizontal=\"Right\" ss:Vertical=\"Center\"/>");
        sb.AppendLine("  <Borders><Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D0D8EE\"/></Borders>");
        sb.AppendLine("</Style>");

        // s_total_na — year total where year not applicable
        sb.AppendLine("<Style ss:ID=\"s_total_na\">");
        sb.AppendLine("  <Font ss:FontName=\"Calibri\" ss:Size=\"9\" ss:Color=\"#CCCCCC\"/>");
        sb.AppendLine("  <Interior ss:Color=\"#F5F5F5\" ss:Pattern=\"Solid\"/>");
        sb.AppendLine("  <NumberFormat ss:Format=\"#,##0\"/>");
        sb.AppendLine("  <Alignment ss:Horizontal=\"Right\" ss:Vertical=\"Center\"/>");
        sb.AppendLine("  <Borders><Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#E8ECF2\"/></Borders>");
        sb.AppendLine("</Style>");

        // s_grand — grand total (white on navy)
        sb.AppendLine("<Style ss:ID=\"s_grand\">");
        sb.AppendLine("  <Font ss:FontName=\"Calibri\" ss:Bold=\"1\" ss:Size=\"9\" ss:Color=\"#FFFFFF\"/>");
        sb.AppendLine("  <Interior ss:Color=\"#05275C\" ss:Pattern=\"Solid\"/>");
        sb.AppendLine("  <NumberFormat ss:Format=\"#,##0\"/>");
        sb.AppendLine("  <Alignment ss:Horizontal=\"Right\" ss:Vertical=\"Center\"/>");
        sb.AppendLine("</Style>");

        // s_itext — inactive row text
        sb.AppendLine("<Style ss:ID=\"s_itext\">");
        sb.AppendLine("  <Font ss:FontName=\"Calibri\" ss:Size=\"9\" ss:Color=\"#999999\" ss:Italic=\"1\"/>");
        sb.AppendLine("  <Interior ss:Color=\"#F8F8F8\" ss:Pattern=\"Solid\"/>");
        sb.AppendLine("  <Alignment ss:Vertical=\"Center\"/>");
        sb.AppendLine("  <Borders><Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#EEEEEE\"/></Borders>");
        sb.AppendLine("</Style>");

        // s_inum — inactive row number
        sb.AppendLine("<Style ss:ID=\"s_inum\">");
        sb.AppendLine("  <Font ss:FontName=\"Calibri\" ss:Size=\"9\" ss:Color=\"#BBBBBB\" ss:Italic=\"1\"/>");
        sb.AppendLine("  <Interior ss:Color=\"#F8F8F8\" ss:Pattern=\"Solid\"/>");
        sb.AppendLine("  <NumberFormat ss:Format=\"#,##0\"/>");
        sb.AppendLine("  <Alignment ss:Horizontal=\"Right\" ss:Vertical=\"Center\"/>");
        sb.AppendLine("  <Borders><Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#EEEEEE\"/></Borders>");
        sb.AppendLine("</Style>");

        // s_itotal — inactive year total
        sb.AppendLine("<Style ss:ID=\"s_itotal\">");
        sb.AppendLine("  <Font ss:FontName=\"Calibri\" ss:Size=\"9\" ss:Color=\"#BBBBBB\" ss:Italic=\"1\"/>");
        sb.AppendLine("  <Interior ss:Color=\"#F3F3F3\" ss:Pattern=\"Solid\"/>");
        sb.AppendLine("  <NumberFormat ss:Format=\"#,##0\"/>");
        sb.AppendLine("  <Alignment ss:Horizontal=\"Right\" ss:Vertical=\"Center\"/>");
        sb.AppendLine("  <Borders><Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#EEEEEE\"/></Borders>");
        sb.AppendLine("</Style>");

        // s_igrand — inactive grand total
        sb.AppendLine("<Style ss:ID=\"s_igrand\">");
        sb.AppendLine("  <Font ss:FontName=\"Calibri\" ss:Bold=\"1\" ss:Size=\"9\" ss:Color=\"#AAAAAA\"/>");
        sb.AppendLine("  <Interior ss:Color=\"#EEEEEE\" ss:Pattern=\"Solid\"/>");
        sb.AppendLine("  <NumberFormat ss:Format=\"#,##0\"/>");
        sb.AppendLine("  <Alignment ss:Horizontal=\"Right\" ss:Vertical=\"Center\"/>");
        sb.AppendLine("</Style>");

        sb.AppendLine("</Styles>");

        // ── Worksheet ────────────────────────────────────────────
        sb.AppendLine("<Worksheet ss:Name=\"Fee Structures\">");
        sb.AppendLine("<Table ss:DefaultRowHeight=\"14\">");

        // Column widths
        sb.AppendLine("<Column ss:Width=\"130\"/>"); // Faculty
        sb.AppendLine("<Column ss:Width=\"210\"/>"); // Programme
        sb.AppendLine("<Column ss:Width=\"72\"/>");  // Code
        sb.AppendLine("<Column ss:Width=\"52\"/>");  // Status
        // Year 1 (6 data + 1 total)
        sb.AppendLine("<Column ss:Width=\"66\"/>"); sb.AppendLine("<Column ss:Width=\"70\"/>");
        sb.AppendLine("<Column ss:Width=\"66\"/>"); sb.AppendLine("<Column ss:Width=\"70\"/>");
        sb.AppendLine("<Column ss:Width=\"66\"/>"); sb.AppendLine("<Column ss:Width=\"70\"/>");
        sb.AppendLine("<Column ss:Width=\"80\"/>");
        // Year 2 (6 data + 1 total)
        sb.AppendLine("<Column ss:Width=\"66\"/>"); sb.AppendLine("<Column ss:Width=\"70\"/>");
        sb.AppendLine("<Column ss:Width=\"66\"/>"); sb.AppendLine("<Column ss:Width=\"70\"/>");
        sb.AppendLine("<Column ss:Width=\"66\"/>"); sb.AppendLine("<Column ss:Width=\"70\"/>");
        sb.AppendLine("<Column ss:Width=\"80\"/>");
        // Year 3 (6 data + 1 total)
        sb.AppendLine("<Column ss:Width=\"66\"/>"); sb.AppendLine("<Column ss:Width=\"70\"/>");
        sb.AppendLine("<Column ss:Width=\"66\"/>"); sb.AppendLine("<Column ss:Width=\"70\"/>");
        sb.AppendLine("<Column ss:Width=\"66\"/>"); sb.AppendLine("<Column ss:Width=\"70\"/>");
        sb.AppendLine("<Column ss:Width=\"80\"/>");
        // Year 4 (6 data + 1 total)
        sb.AppendLine("<Column ss:Width=\"66\"/>"); sb.AppendLine("<Column ss:Width=\"70\"/>");
        sb.AppendLine("<Column ss:Width=\"66\"/>"); sb.AppendLine("<Column ss:Width=\"70\"/>");
        sb.AppendLine("<Column ss:Width=\"66\"/>"); sb.AppendLine("<Column ss:Width=\"70\"/>");
        sb.AppendLine("<Column ss:Width=\"80\"/>");
        // Grand Total
        sb.AppendLine("<Column ss:Width=\"92\"/>");

        // ── Row 1: Title ─────────────────────────────────────────
        sb.AppendLine("<Row ss:Height=\"24\">");
        sb.AppendFormat("<Cell ss:StyleID=\"s_title\" ss:MergeAcross=\"{0}\">", TOTAL_COLS - 1);
        sb.AppendFormat("<Data ss:Type=\"String\">{0} — Programme Fee Structures</Data></Cell>", X(instName));
        sb.AppendLine("</Row>");

        // ── Row 2: Meta ──────────────────────────────────────────
        sb.AppendLine("<Row ss:Height=\"15\">");
        sb.AppendFormat("<Cell ss:StyleID=\"s_info\" ss:MergeAcross=\"{0}\">", TOTAL_COLS - 1);
        sb.AppendFormat("<Data ss:Type=\"String\">Academic Year: {0}   |   Generated: {1}   |   By: {2}</Data></Cell>",
            X(acadYear), X(genDate), X(genBy));
        sb.AppendLine("</Row>");

        // ── Row 3: Blank spacer ───────────────────────────────────
        sb.AppendLine("<Row ss:Height=\"6\"/>");

        // ── Row 4: Year group headers (Navy, merged) ──────────────
        sb.AppendLine("<Row ss:Height=\"18\">");
        sb.AppendLine("<Cell ss:StyleID=\"s_group\"><Data ss:Type=\"String\">Faculty</Data></Cell>");
        sb.AppendLine("<Cell ss:StyleID=\"s_group\"><Data ss:Type=\"String\">Programme</Data></Cell>");
        sb.AppendLine("<Cell ss:StyleID=\"s_group\"><Data ss:Type=\"String\">Code</Data></Cell>");
        sb.AppendLine("<Cell ss:StyleID=\"s_group\"><Data ss:Type=\"String\">Status</Data></Cell>");
        sb.AppendLine("<Cell ss:StyleID=\"s_group\" ss:MergeAcross=\"6\"><Data ss:Type=\"String\">YEAR 1</Data></Cell>");
        sb.AppendLine("<Cell ss:StyleID=\"s_group\" ss:MergeAcross=\"6\"><Data ss:Type=\"String\">YEAR 2</Data></Cell>");
        sb.AppendLine("<Cell ss:StyleID=\"s_group\" ss:MergeAcross=\"6\"><Data ss:Type=\"String\">YEAR 3</Data></Cell>");
        sb.AppendLine("<Cell ss:StyleID=\"s_group\" ss:MergeAcross=\"6\"><Data ss:Type=\"String\">YEAR 4</Data></Cell>");
        sb.AppendLine("<Cell ss:StyleID=\"s_group\"><Data ss:Type=\"String\">GRAND TOTAL</Data></Cell>");
        sb.AppendLine("</Row>");

        // ── Row 5: Column headers (Blue) ──────────────────────────
        sb.AppendLine("<Row ss:Height=\"30\">");
        sb.AppendLine("<Cell ss:StyleID=\"s_hdr\"><Data ss:Type=\"String\">Faculty</Data></Cell>");
        sb.AppendLine("<Cell ss:StyleID=\"s_hdr\"><Data ss:Type=\"String\">Programme Name</Data></Cell>");
        sb.AppendLine("<Cell ss:StyleID=\"s_hdr\"><Data ss:Type=\"String\">Code</Data></Cell>");
        sb.AppendLine("<Cell ss:StyleID=\"s_hdr\"><Data ss:Type=\"String\">Status</Data></Cell>");
        for (int y = 1; y <= 4; y++)
        {
            sb.AppendFormat("<Cell ss:StyleID=\"s_hdr\"><Data ss:Type=\"String\">Y{0} S1 Tuition</Data></Cell>", y);
            sb.AppendFormat("<Cell ss:StyleID=\"s_hdr\"><Data ss:Type=\"String\">Y{0} S1 Functional</Data></Cell>", y);
            sb.AppendFormat("<Cell ss:StyleID=\"s_hdr\"><Data ss:Type=\"String\">Y{0} S2 Tuition</Data></Cell>", y);
            sb.AppendFormat("<Cell ss:StyleID=\"s_hdr\"><Data ss:Type=\"String\">Y{0} S2 Functional</Data></Cell>", y);
            sb.AppendFormat("<Cell ss:StyleID=\"s_hdr\"><Data ss:Type=\"String\">Y{0} S3 Tuition</Data></Cell>", y);
            sb.AppendFormat("<Cell ss:StyleID=\"s_hdr\"><Data ss:Type=\"String\">Y{0} S3 Functional</Data></Cell>", y);
            sb.AppendFormat("<Cell ss:StyleID=\"s_hdr\"><Data ss:Type=\"String\">Year {0} Total</Data></Cell>", y);
        }
        sb.AppendLine("<Cell ss:StyleID=\"s_hdr\"><Data ss:Type=\"String\">Grand Total (UGX)</Data></Cell>");
        sb.AppendLine("</Row>");

        // ── Data rows ─────────────────────────────────────────────
        if (dt != null && dt.Rows.Count > 0)
        {
            DataView dv = dt.DefaultView;
            dv.Sort = "faculty_name ASC, is_active DESC, progname ASC";
            DataTable sorted = dv.ToTable();

            string currentFaculty = null;

            foreach (DataRow r in sorted.Rows)
            {
                string faculty = Nvl(r["faculty_name"]);
                if (string.IsNullOrEmpty(faculty)) faculty = "No Faculty Assigned";

                // Faculty separator row
                if (currentFaculty == null || currentFaculty != faculty)
                {
                    currentFaculty = faculty;
                    sb.AppendLine("<Row ss:Height=\"16\">");
                    sb.AppendFormat("<Cell ss:StyleID=\"s_faculty\" ss:MergeAcross=\"{0}\">", TOTAL_COLS - 1);
                    sb.AppendFormat("<Data ss:Type=\"String\">{0}</Data></Cell>", X(faculty));
                    sb.AppendLine("</Row>");
                }

                // Programme data row
                bool active = Nvl(r["is_active"]).Equals("Yes", StringComparison.OrdinalIgnoreCase);
                bool hy1    = Nvl(r["has_year_1"]).Equals("Yes", StringComparison.OrdinalIgnoreCase);
                bool hy2    = Nvl(r["has_year_2"]).Equals("Yes", StringComparison.OrdinalIgnoreCase);
                bool hy3    = Nvl(r["has_year_3"]).Equals("Yes", StringComparison.OrdinalIgnoreCase);
                bool hy4    = Nvl(r["has_year_4"]).Equals("Yes", StringComparison.OrdinalIgnoreCase);

                // Select styles based on active/inactive
                string textSt  = active ? "s_text"  : "s_itext";
                string grandSt = active ? "s_grand" : "s_igrand";

                sb.AppendLine("<Row ss:Height=\"14\">");

                // Info columns
                sb.AppendFormat("<Cell ss:StyleID=\"{0}\"><Data ss:Type=\"String\">{1}</Data></Cell>", textSt, X(faculty));
                sb.AppendFormat("<Cell ss:StyleID=\"{0}\"><Data ss:Type=\"String\">{1}</Data></Cell>", textSt, X(Nvl(r["progname"])));
                sb.AppendFormat("<Cell ss:StyleID=\"{0}\"><Data ss:Type=\"String\">{1}</Data></Cell>", textSt, X(Nvl(r["progcode"])));
                sb.AppendFormat("<Cell ss:StyleID=\"{0}\"><Data ss:Type=\"String\">{1}</Data></Cell>", textSt, active ? "Active" : "Inactive");

                string[] yKeys  = { "y1", "y2", "y3", "y4" };
                string[] sKeys  = { "s1", "s2", "s3" };
                bool[]   hasY   = { hy1, hy2, hy3, hy4 };
                double   grand  = 0;

                for (int yi = 0; yi < 4; yi++)
                {
                    bool   yrOn    = hasY[yi];
                    string numSt   = !active ? "s_inum"    : (yrOn ? "s_num"      : "s_zero");
                    string totalSt = !active ? "s_itotal"  : (yrOn ? "s_total"    : "s_total_na");

                    double yrTotal = 0;
                    for (int si = 0; si < 3; si++)
                    {
                        double t = ToDouble(r[yKeys[yi] + "_" + sKeys[si] + "_tuition"]);
                        double f = ToDouble(r[yKeys[yi] + "_" + sKeys[si] + "_functional"]);
                        if (yrOn) yrTotal += t + f;

                        sb.AppendFormat("<Cell ss:StyleID=\"{0}\"><Data ss:Type=\"Number\">{1}</Data></Cell>", numSt, t);
                        sb.AppendFormat("<Cell ss:StyleID=\"{0}\"><Data ss:Type=\"Number\">{1}</Data></Cell>", numSt, f);
                    }

                    sb.AppendFormat("<Cell ss:StyleID=\"{0}\"><Data ss:Type=\"Number\">{1}</Data></Cell>", totalSt, yrTotal);
                    if (yrOn) grand += yrTotal;
                }

                sb.AppendFormat("<Cell ss:StyleID=\"{0}\"><Data ss:Type=\"Number\">{1}</Data></Cell>", grandSt, grand);
                sb.AppendLine("</Row>");
            }
        }
        else
        {
            sb.AppendLine("<Row>");
            sb.AppendFormat("<Cell ss:MergeAcross=\"{0}\"><Data ss:Type=\"String\">No fee structures found.</Data></Cell>", TOTAL_COLS - 1);
            sb.AppendLine("</Row>");
        }

        sb.AppendLine("</Table>");

        // Freeze top 5 rows (3 header + 1 blank + year-group row + col-header row)
        sb.AppendLine("<WorksheetOptions xmlns=\"urn:schemas-microsoft-com:office:excel\">");
        sb.AppendLine("  <FreezePanes/>");
        sb.AppendLine("  <FrozenNoSplit/>");
        sb.AppendLine("  <SplitHorizontal>5</SplitHorizontal>");
        sb.AppendLine("  <TopRowBottomPane>5</TopRowBottomPane>");
        sb.AppendLine("  <ActivePane>2</ActivePane>");
        sb.AppendLine("  <Panes>");
        sb.AppendLine("    <Pane><Number>2</Number></Pane>");
        sb.AppendLine("  </Panes>");
        sb.AppendLine("</WorksheetOptions>");

        sb.AppendLine("</Worksheet>");
        sb.AppendLine("</Workbook>");

        Response.Write(sb.ToString());
    }

    // ================================================================
    // DATA QUERY — identical to FeesStructurePrint
    // ================================================================

    private DataTable GetAllFeeStructures()
    {
        const string sql = @"
            SELECT pf.ID, pf.progcode,
                COALESCE(p.progname,'(Unknown Programme)') AS progname,
                COALESCE(f.faculty_name,'') AS faculty_name,
                pf.has_year_1, pf.has_year_2, pf.has_year_3, pf.has_year_4,
                pf.y1_s1_tuition, pf.y1_s1_functional,
                pf.y1_s2_tuition, pf.y1_s2_functional,
                pf.y1_s3_tuition, pf.y1_s3_functional,
                pf.y2_s1_tuition, pf.y2_s1_functional,
                pf.y2_s2_tuition, pf.y2_s2_functional,
                pf.y2_s3_tuition, pf.y2_s3_functional,
                pf.y3_s1_tuition, pf.y3_s1_functional,
                pf.y3_s2_tuition, pf.y3_s2_functional,
                pf.y3_s3_tuition, pf.y3_s3_functional,
                pf.y4_s1_tuition, pf.y4_s1_functional,
                pf.y4_s2_tuition, pf.y4_s2_functional,
                pf.y4_s3_tuition, pf.y4_s3_functional,
                pf.is_active
            FROM fin_programme_fees pf
            LEFT JOIN campus_dynamics.acad_programme p ON p.progcode = pf.progcode
            LEFT JOIN campus_dynamics.acad_faculty   f ON f.faculty_code = p.faculty_code
            -- MAIN only. fin_programme_fees now holds one row per (programme, session); without
            -- this filter every programme carrying in-service rates would export twice with no
            -- column distinguishing the two, which is worse than omitting them.
            -- TODO: extend this export to a session column and emit the in-service rows too.
            WHERE pf.stud_session = 'MAIN'
            ORDER BY f.faculty_name ASC, pf.is_active DESC, p.progname ASC";

        var dt = new DataTable();
        try
        {
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(sql, conn))
                using (var da  = new MySqlDataAdapter(cmd))
                    da.Fill(dt);
            }
        }
        catch (Exception ex)
        {
            // Log and return empty — Page_Load will flush whatever was written
            System.Diagnostics.Debug.WriteLine("FeesStructureExport error: " + ex.Message);
        }
        return dt;
    }

    // ================================================================
    // HELPERS
    // ================================================================

    private string GetCurrentAcademicYear()
    {
        try
        {
            const string sql = "SELECT setting_value FROM campus_dynamics.system_settings WHERE setting_name='current_academic_year' LIMIT 1";
            using (var conn = new MySqlConnection(MainConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    object val = cmd.ExecuteScalar();
                    if (val != null && val != DBNull.Value) return val.ToString();
                }
            }
        }
        catch { }
        int yr = DateTime.Now.Year;
        return DateTime.Now.Month >= 8 ? yr + "/" + (yr + 1) : (yr - 1) + "/" + yr;
    }

    private string GetCurrentUser()
    {
        if (Session["ScreenName"] != null) return Session["ScreenName"].ToString();
        if (Session["UserName"]   != null) return Session["UserName"].ToString();
        if (Session["username"]   != null) return Session["username"].ToString();
        if (Session["FullName"]   != null) return Session["FullName"].ToString();
        return HttpContext.Current.User.Identity.IsAuthenticated
            ? HttpContext.Current.User.Identity.Name : "System";
    }

    private string GetSetting(string key, string fallback)
    {
        try
        {
            const string sql = "SELECT SettingValue FROM sys_setting WHERE SettingKey = @k LIMIT 1";
            using (var conn = new MySqlConnection(MainConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@k", key);
                    object val = cmd.ExecuteScalar();
                    if (val != null && val != DBNull.Value && !string.IsNullOrEmpty(val.ToString()))
                        return val.ToString();
                }
            }
        }
        catch { }
        return fallback;
    }

    private double ToDouble(object val)
    {
        if (val == null || val == DBNull.Value) return 0;
        double d;
        return double.TryParse(val.ToString(), out d) ? d : 0;
    }

    private string Nvl(object val)
    {
        if (val == null || val == DBNull.Value) return string.Empty;
        return val.ToString().Trim();
    }

    /// <summary>XML-encodes a string for embedding in SpreadsheetML Data elements.</summary>
    private string X(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
    }
}
