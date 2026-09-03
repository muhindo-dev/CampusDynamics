using System;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web;
using System.Web.UI;
using MySql.Data.MySqlClient;

public partial class COOPERP_NewScreens_FeesStructurePrint : System.Web.UI.Page
{
    // ── Connection strings ──────────────────────────────────────
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
        get
        {
            return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString;
        }
    }

    // ── Page load ───────────────────────────────────────────────
    protected void Page_Load(object sender, EventArgs e)
    {
        if (!IsPostBack)
        {
            string now = DateTime.Now.ToString("dd MMMM yyyy, hh:mm tt");
            litPrintDate.Text = now;
            litDocDate.Text = DateTime.Now.ToString("dd MMMM yyyy");
            litFootDate.Text = now;
            litAcadYear.Text = GetCurrentAcademicYear();
            litGenBy.Text = GetCurrentUser();

            // Dynamic institution branding from sys_setting
            string instName = GetSetting("InstitutionName", "Muteesa I Royal University");
            litTitleInst.Text = HttpUtility.HtmlEncode(instName);
            litInstitution.Text = HttpUtility.HtmlEncode(instName);
            litBarInst.Text = HttpUtility.HtmlEncode(instName);
            litFootInst.Text = HttpUtility.HtmlEncode(instName);
            litMotto.Text = HttpUtility.HtmlEncode(GetSetting("InstitutionMotto", ""));
            litFootContact.Text = GetSetting("InstitutionContact",
                "P.O.Box 14002, Kakeeka Mengo &bull; P.O.Box 322, Masaka City &bull; Tel: +256414670109 &bull; www.mru.ac.ug &bull; info@mru.ac.ug");

            string logoPath = GetSetting("LogoPath", "");
            if (!string.IsNullOrEmpty(logoPath))
            {
                imgLogo.ImageUrl = "~/" + logoPath;
                imgLogo.Visible = true;
            }
            else
            {
                // Fallback to the university logo
                imgLogo.ImageUrl = ResolveUrl("~/COOPERP/images/welcomelogo.png");
                imgLogo.Visible = true;
            }

            BuildReport();
        }
    }

    // ── Build the whole report ──────────────────────────────────
    private void BuildReport()
    {
        DataTable dt = GetAllFeeStructures();
        if (dt == null || dt.Rows.Count == 0)
        {
            litBody.Text = "<p style='text-align:center;padding:40px;color:#888;font-size:14px;'>No fee structures found.</p>";
            return;
        }

        // Count stats
        int total = dt.Rows.Count;
        int active = 0;
        int inactive = 0;
        foreach (DataRow r in dt.Rows)
        {
            if (Nvl(r["is_active"]).Equals("Yes", StringComparison.OrdinalIgnoreCase))
                active++;
            else
                inactive++;
        }

        // Group by faculty
        DataView dv = dt.DefaultView;
        dv.Sort = "faculty_name ASC, progname ASC";
        DataTable sorted = dv.ToTable();

        string currentFaculty = null;
        int facCount = 0;
        int progsInFaculty = 0;

        // Two-pass: first count faculties, then render
        System.Collections.Generic.HashSet<string> faculties = new System.Collections.Generic.HashSet<string>();
        foreach (DataRow r in sorted.Rows)
        {
            string fn = Nvl(r["faculty_name"]);
            if (string.IsNullOrEmpty(fn)) fn = "No Faculty Assigned";
            faculties.Add(fn);
        }
        facCount = faculties.Count;

        litSumTotal.Text = total.ToString("N0");
        litSumActive.Text = active.ToString("N0");
        litSumInactive.Text = inactive.ToString("N0");
        litSumFaculties.Text = facCount.ToString("N0");

        StringBuilder sb = new StringBuilder();
        currentFaculty = null;
        progsInFaculty = 0;

        // Count programmes per faculty for header
        System.Collections.Generic.Dictionary<string, int> facProgCount = new System.Collections.Generic.Dictionary<string, int>();
        foreach (DataRow r in sorted.Rows)
        {
            string fn = Nvl(r["faculty_name"]);
            if (string.IsNullOrEmpty(fn)) fn = "No Faculty Assigned";
            if (!facProgCount.ContainsKey(fn)) facProgCount[fn] = 0;
            facProgCount[fn]++;
        }

        foreach (DataRow r in sorted.Rows)
        {
            string fn = Nvl(r["faculty_name"]);
            if (string.IsNullOrEmpty(fn)) fn = "No Faculty Assigned";

            if (currentFaculty == null || currentFaculty != fn)
            {
                // Close previous faculty section
                if (currentFaculty != null)
                {
                    sb.Append("</tbody></table></div>"); // close previous table & section
                }

                currentFaculty = fn;
                int count = facProgCount.ContainsKey(fn) ? facProgCount[fn] : 0;

                // Open new faculty section
                sb.Append("<div class=\"fac-section\">");
                sb.AppendFormat("<div class=\"fac-header\"><span class=\"fac-header__name\">{0}</span><span class=\"fac-header__count\">{1} Programme{2}</span></div>",
                    HttpUtility.HtmlEncode(fn), count, count != 1 ? "s" : "");

                // Table header
                sb.Append("<table class=\"fee-table\"><thead>");

                // Row 1: Year groups spanning
                sb.Append("<tr>");
                sb.Append("<th rowspan=\"3\" class=\"th-prog\">Programme</th>");
                sb.Append("<th rowspan=\"3\">Code</th>");
                sb.Append("<th rowspan=\"3\">Status</th>");
                sb.Append("<th colspan=\"8\" class=\"th-group\">Year 1</th>");
                sb.Append("<th colspan=\"8\" class=\"th-group\">Year 2</th>");
                sb.Append("<th colspan=\"8\" class=\"th-group\">Year 3</th>");
                sb.Append("<th colspan=\"8\" class=\"th-group\">Year 4</th>");
                sb.Append("<th rowspan=\"3\" style=\"min-width:72px;\">Grand<br/>Total</th>");
                sb.Append("</tr>");

                // Row 2: Semester groups
                sb.Append("<tr>");
                for (int y = 0; y < 4; y++)
                {
                    sb.Append("<th colspan=\"2\">Sem 1</th>");
                    sb.Append("<th colspan=\"2\">Sem 2</th>");
                    sb.Append("<th colspan=\"2\">Sem 3</th>");
                    sb.AppendFormat("<th rowspan=\"2\">Yr {0}<br/>Total</th>", y + 1);
                    sb.AppendFormat("<th rowspan=\"2\">Has<br/>Yr {0}</th>", y + 1);
                }
                sb.Append("</tr>");

                // Row 3: Tuition / Functional
                sb.Append("<tr>");
                for (int y = 0; y < 4; y++)
                {
                    for (int s = 0; s < 3; s++)
                    {
                        sb.Append("<th>Tui</th><th>Func</th>");
                    }
                }
                sb.Append("</tr>");

                sb.Append("</thead><tbody>");
            }

            // Render programme row
            RenderProgrammeRow(sb, r);
        }

        // Close last section
        if (currentFaculty != null)
        {
            sb.Append("</tbody></table></div>");
        }

        litBody.Text = sb.ToString();
    }

    // ── Render a single programme row ───────────────────────────
    private void RenderProgrammeRow(StringBuilder sb, DataRow r)
    {
        string isActive = Nvl(r["is_active"]);
        bool active = isActive.Equals("Yes", StringComparison.OrdinalIgnoreCase);
        string rowClass = active ? "" : " class=\"row-inactive\"";

        sb.AppendFormat("<tr{0}>", rowClass);

        // Programme name
        sb.AppendFormat("<td class=\"td-prog\">{0}</td>", HttpUtility.HtmlEncode(Nvl(r["progname"])));

        // Programme code
        sb.AppendFormat("<td class=\"td-code\">{0}</td>", HttpUtility.HtmlEncode(Nvl(r["progcode"])));

        // Status
        sb.AppendFormat("<td style=\"text-align:center;\"><span class=\"{0}\">{1}</span></td>",
            active ? "status-active" : "status-inactive",
            active ? "Active" : "Inactive");

        double grandTotal = 0;
        string[] years = { "y1", "y2", "y3", "y4" };
        string[] semesters = { "s1", "s2", "s3" };
        string[] hasYears = { "has_year_1", "has_year_2", "has_year_3", "has_year_4" };

        for (int yi = 0; yi < 4; yi++)
        {
            string hasYr = Nvl(r[hasYears[yi]]);
            bool yrActive = hasYr.Equals("Yes", StringComparison.OrdinalIgnoreCase);
            double yrTotal = 0;

            for (int si = 0; si < 3; si++)
            {
                string tField = years[yi] + "_" + semesters[si] + "_tuition";
                string fField = years[yi] + "_" + semesters[si] + "_functional";
                double tVal = ToDouble(r[tField]);
                double fVal = ToDouble(r[fField]);

                if (yrActive)
                {
                    sb.AppendFormat("<td>{0}</td>", FormatAmt(tVal));
                    sb.AppendFormat("<td>{0}</td>", FormatAmt(fVal));
                    yrTotal += tVal + fVal;
                }
                else
                {
                    sb.Append("<td class=\"na\">&mdash;</td><td class=\"na\">&mdash;</td>");
                }
            }

            // Year total
            if (yrActive)
            {
                sb.AppendFormat("<td class=\"td-yr-total\">{0}</td>", FormatAmt(yrTotal));
                grandTotal += yrTotal;
            }
            else
            {
                sb.Append("<td class=\"na\">&mdash;</td>");
            }

            // Has year flag
            sb.AppendFormat("<td style=\"text-align:center;font-size:7px;\">{0}</td>",
                yrActive ? "<span style=\"color:#155724;\">&#10003;</span>" : "<span style=\"color:#ccc;\">&#10007;</span>");
        }

        // Grand total
        sb.AppendFormat("<td class=\"td-total\">{0}</td>", FormatAmt(grandTotal));

        sb.Append("</tr>");
    }

    // ── Query all fee structures ────────────────────────────────
    private DataTable GetAllFeeStructures()
    {
        string sql = @"SELECT pf.ID, pf.progcode,
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
            LEFT JOIN campus_dynamics.acad_faculty f ON f.faculty_code = p.faculty_code
            -- MAIN only, for the same reason as the export: one row per (programme, session)
            -- would otherwise print each in-service programme twice, indistinguishably.
            -- TODO: extend the printed sheet to show in-service rates as their own section.
            WHERE pf.stud_session = 'MAIN'
            ORDER BY f.faculty_name ASC, p.progname ASC, pf.is_active DESC";

        DataTable dt = new DataTable();
        try
        {
            using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    using (MySqlDataAdapter da = new MySqlDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            litBody.Text = "<p style='color:#dc3545;padding:20px;'>Error loading fee structures: " +
                HttpUtility.HtmlEncode(ex.Message) + "</p>";
        }
        return dt;
    }

    // ── Get current academic year ───────────────────────────────
    private string GetCurrentAcademicYear()
    {
        try
        {
            string sql = "SELECT setting_value FROM campus_dynamics.system_settings WHERE setting_name='current_academic_year' LIMIT 1";
            using (MySqlConnection conn = new MySqlConnection(MainConnStr))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    object val = cmd.ExecuteScalar();
                    if (val != null && val != DBNull.Value)
                        return val.ToString();
                }
            }
        }
        catch { }

        // Fallback: derive from current date
        int yr = DateTime.Now.Year;
        return DateTime.Now.Month >= 8 ? yr + "/" + (yr + 1) : (yr - 1) + "/" + yr;
    }

    // ── Get current user ────────────────────────────────────────
    private string GetCurrentUser()
    {
        if (Session["UserName"] != null) return Session["UserName"].ToString();
        if (Session["username"] != null) return Session["username"].ToString();
        if (Session["FullName"] != null) return Session["FullName"].ToString();
        return HttpContext.Current.User.Identity.IsAuthenticated
            ? HttpContext.Current.User.Identity.Name : "System";
    }

    // ── Helpers ─────────────────────────────────────────────────
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

    private string FormatAmt(double val)
    {
        if (val == 0) return "<span style='color:#ccc;'>0</span>";
        return val.ToString("N0");
    }

    // ── Read a value from sys_setting ───────────────────────────
    private string GetSetting(string key, string fallback)
    {
        try
        {
            string sql = "SELECT SettingValue FROM sys_setting WHERE SettingKey = @key LIMIT 1";
            using (MySqlConnection conn = new MySqlConnection(MainConnStr))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@key", key);
                    object val = cmd.ExecuteScalar();
                    if (val != null && val != DBNull.Value && !string.IsNullOrEmpty(val.ToString()))
                        return val.ToString();
                }
            }
        }
        catch { }
        return fallback;
    }
}