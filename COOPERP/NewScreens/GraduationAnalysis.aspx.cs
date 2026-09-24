using System;
using System.Collections.Generic;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using MySql.Data.MySqlClient;
using System.Configuration;
using System.IO;
using System.Text;

public partial class COOPERP_NewScreens_GraduationAnalysis : System.Web.UI.Page
{
    private string connectionString = ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString;

    /// <summary>
    /// The signed-in user's faculties and programmes.
    ///
    /// This page had none. Every query read acad_graduands with no restriction at all, and the
    /// faculty dropdown was "SELECT DISTINCT faculty_code, faculty_name FROM acad_faculty" — so a
    /// Dean or a HOD opening Graduation Analysis saw the whole university's graduands, while the
    /// four pages beside it in the same menu correctly showed them only their own. That is not a
    /// difference in presentation; it is the same menu answering the same question two different
    /// ways depending on which item you click.
    /// </summary>
    private MarksScope Scope
    {
        get
        {
            if (_scope == null) _scope = MarksScopeResolver.Resolve();
            return _scope;
        }
    }
    private MarksScope _scope;

    protected void Page_Load(object sender, EventArgs e)
    {
        if (!Scope.HasAccess)
        {
            pnlNoAccess.Visible = true;
            pnlAnalysis.Visible = false;
            return;
        }
        if (!IsPostBack)
        {
            lblScope.Text = Scope.RoleNote + (Scope.Label == "" ? "" : "  \u00b7  " + Scope.Label);
            LoadFilters();
            LoadAnalysisData();
        }
    }

    protected void LoadFilters()
    {
        using (MySqlConnection conn = new MySqlConnection(connectionString))
        {
            conn.Open();

            // Faculties
            // Only faculties this user actually has programmes in. Offering a filter that
            // returns nothing is worse than not offering it.
            string sqlFaculty =
                "SELECT DISTINCT f.faculty_code, f.faculty_name FROM acad_faculty f " +
                "WHERE EXISTS (SELECT 1 FROM acad_programme p " +
                "              WHERE TRIM(p.faculty_code)=TRIM(f.faculty_code)" +
                Scope.ProgFilter("p", "progcode") + ") " +
                "ORDER BY f.faculty_name";
            using (MySqlCommand cmd = new MySqlCommand(sqlFaculty, conn))
            {
                using (MySqlDataReader dr = cmd.ExecuteReader())
                {
                    ddlFaculty.Items.Clear();
                    ddlFaculty.Items.Add(new ListItem("-- All Faculties --", ""));
                    while (dr.Read())
                    {
                        ddlFaculty.Items.Add(new ListItem(dr["faculty_name"].ToString(), dr["faculty_code"].ToString()));
                    }
                }
            }

            // Convocations (from graduands table)
            string sqlConv = "SELECT DISTINCT convocation FROM acad_graduands WHERE convocation IS NOT NULL ORDER BY convocation DESC";
            using (MySqlCommand cmd = new MySqlCommand(sqlConv, conn))
            {
                using (MySqlDataReader dr = cmd.ExecuteReader())
                {
                    ddlConvocation.Items.Clear();
                    ddlConvocation.Items.Add(new ListItem("-- All --", ""));
                    while (dr.Read())
                    {
                        string conv = dr["convocation"].ToString();
                        ddlConvocation.Items.Add(new ListItem(conv, conv));
                    }
                }
            }

            // Set default convocation if available
            if (ddlConvocation.Items.Count > 1)
            {
                ddlConvocation.SelectedIndex = 1;
                litConvocationDisplay.Text = ddlConvocation.SelectedValue;
            }
            else
            {
                litConvocationDisplay.Text = "All";
            }
        }
    }

    protected void ddlFaculty_SelectedIndexChanged(object sender, EventArgs e)
    {
        LoadProgrammes();
        LoadAnalysisData();
    }

    protected void LoadProgrammes()
    {
        using (MySqlConnection conn = new MySqlConnection(connectionString))
        {
            conn.Open();
            string sql = "SELECT progcode, progname FROM acad_programme p WHERE 1=1";
            if (!string.IsNullOrEmpty(ddlFaculty.SelectedValue))
            {
                sql += " AND p.faculty_code = @fac";
            }
            sql += Scope.ProgFilter("p", "progcode");
            sql += " ORDER BY progname";

            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (!string.IsNullOrEmpty(ddlFaculty.SelectedValue))
                {
                    cmd.Parameters.AddWithValue("@fac", ddlFaculty.SelectedValue);
                }

                using (MySqlDataReader dr = cmd.ExecuteReader())
                {
                    ddlProgramme.Items.Clear();
                    ddlProgramme.Items.Add(new ListItem("-- All Programmes --", ""));
                    while (dr.Read())
                    {
                        ddlProgramme.Items.Add(new ListItem(dr["progname"].ToString(), dr["progcode"].ToString()));
                    }
                }
            }
        }
    }

    protected void ddlProgramme_SelectedIndexChanged(object sender, EventArgs e)
    {
        LoadAnalysisData();
    }

    protected void ddlConvocation_SelectedIndexChanged(object sender, EventArgs e)
    {
        litConvocationDisplay.Text = string.IsNullOrEmpty(ddlConvocation.SelectedValue) ? "All" : ddlConvocation.SelectedValue;
        LoadAnalysisData();
    }

    protected void btnRefresh_Click(object sender, EventArgs e)
    {
        LoadAnalysisData();
    }

    protected void LoadAnalysisData()
    {
        LoadFacultySummary();
        LoadProgrammeSummary();
        LoadClassSummary();
        LoadDetailedList();
        UpdateOverallStats();
    }

    private string GetWhereClause(string tableAlias = "g")
    {
        StringBuilder where = new StringBuilder("WHERE 1=1");
        
        if (!string.IsNullOrEmpty(ddlConvocation.SelectedValue))
        {
            where.AppendFormat(" AND {0}.convocation = @conv", tableAlias);
        }
        if (!string.IsNullOrEmpty(ddlProgramme.SelectedValue))
        {
            where.AppendFormat(" AND {0}.progcode = @prog", tableAlias);
        }

        // Every query on this page runs through here, which is why the scope belongs here: one
        // place, and no way to add a sixth query that quietly forgets it.
        where.Append(Scope.ProgFilter(tableAlias, "progcode"));

        return where.ToString();
    }

    private void AddWhereParameters(MySqlCommand cmd)
    {
        if (!string.IsNullOrEmpty(ddlConvocation.SelectedValue))
        {
            cmd.Parameters.AddWithValue("@conv", ddlConvocation.SelectedValue);
        }
        if (!string.IsNullOrEmpty(ddlProgramme.SelectedValue))
        {
            cmd.Parameters.AddWithValue("@prog", ddlProgramme.SelectedValue);
        }
    }

    protected void LoadFacultySummary()
    {
        using (MySqlConnection conn = new MySqlConnection(connectionString))
        {
            conn.Open();
            
            string whereClause = GetWhereClause();
            string facultyFilter = "";
            if (!string.IsNullOrEmpty(ddlFaculty.SelectedValue))
            {
                facultyFilter = " AND p.faculty_code = @fac";
            }

            string sql = string.Format(@"
                SELECT 
                    IFNULL(f.faculty_name, 'Unknown') AS faculty,
                    SUM(CASE WHEN s.gender IN ('M', 'Male') THEN 1 ELSE 0 END) AS male_count,
                    SUM(CASE WHEN s.gender IN ('F', 'Female') THEN 1 ELSE 0 END) AS female_count,
                    COUNT(*) AS total
                FROM acad_graduands g
                LEFT JOIN acad_student s ON g.regno = s.regno
                LEFT JOIN acad_programme p ON g.progcode = p.progcode
                LEFT JOIN acad_faculty f ON p.faculty_code = f.faculty_code
                {0} {1}
                GROUP BY f.faculty_name
                ORDER BY f.faculty_name", whereClause, facultyFilter);

            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                AddWhereParameters(cmd);
                if (!string.IsNullOrEmpty(ddlFaculty.SelectedValue))
                {
                    cmd.Parameters.AddWithValue("@fac", ddlFaculty.SelectedValue);
                }

                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    da.Fill(dt);

                    // Add totals row
                    if (dt.Rows.Count > 0)
                    {
                        int totalMale = 0, totalFemale = 0, grandTotal = 0;
                        foreach (DataRow row in dt.Rows)
                        {
                            totalMale += Convert.ToInt32(row["male_count"]);
                            totalFemale += Convert.ToInt32(row["female_count"]);
                            grandTotal += Convert.ToInt32(row["total"]);
                        }

                        DataRow totalRow = dt.NewRow();
                        totalRow["faculty"] = "TOTAL";
                        totalRow["male_count"] = totalMale;
                        totalRow["female_count"] = totalFemale;
                        totalRow["total"] = grandTotal;
                        dt.Rows.Add(totalRow);
                    }

                    _dtFaculty = dt;
                    gvFacultySummary.DataSource = dt;
                    gvFacultySummary.DataBind();
                }
            }
        }
    }

    protected void LoadProgrammeSummary()
    {
        using (MySqlConnection conn = new MySqlConnection(connectionString))
        {
            conn.Open();

            string whereClause = GetWhereClause();
            string facultyFilter = "";
            if (!string.IsNullOrEmpty(ddlFaculty.SelectedValue))
            {
                facultyFilter = " AND p.faculty_code = @fac";
            }

            string sql = string.Format(@"
                SELECT 
                    IFNULL(p.progname, g.progcode) AS prog_name,
                    SUM(CASE WHEN s.gender IN ('M', 'Male') THEN 1 ELSE 0 END) AS male_count,
                    SUM(CASE WHEN s.gender IN ('F', 'Female') THEN 1 ELSE 0 END) AS female_count,
                    COUNT(*) AS total
                FROM acad_graduands g
                LEFT JOIN acad_student s ON g.regno = s.regno
                LEFT JOIN acad_programme p ON g.progcode = p.progcode
                {0} {1}
                GROUP BY p.progname, g.progcode
                ORDER BY p.progname", whereClause, facultyFilter);

            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                AddWhereParameters(cmd);
                if (!string.IsNullOrEmpty(ddlFaculty.SelectedValue))
                {
                    cmd.Parameters.AddWithValue("@fac", ddlFaculty.SelectedValue);
                }

                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    da.Fill(dt);

                    // Add totals row
                    if (dt.Rows.Count > 0)
                    {
                        int totalMale = 0, totalFemale = 0, grandTotal = 0;
                        foreach (DataRow row in dt.Rows)
                        {
                            totalMale += Convert.ToInt32(row["male_count"]);
                            totalFemale += Convert.ToInt32(row["female_count"]);
                            grandTotal += Convert.ToInt32(row["total"]);
                        }

                        DataRow totalRow = dt.NewRow();
                        totalRow["prog_name"] = "TOTAL";
                        totalRow["male_count"] = totalMale;
                        totalRow["female_count"] = totalFemale;
                        totalRow["total"] = grandTotal;
                        dt.Rows.Add(totalRow);
                    }

                    _dtProgramme = dt;
                    gvProgrammeSummary.DataSource = dt;
                    gvProgrammeSummary.DataBind();
                }
            }
        }
    }

    protected void LoadClassSummary()
    {
        using (MySqlConnection conn = new MySqlConnection(connectionString))
        {
            conn.Open();

            string whereClause = GetWhereClause();
            string facultyFilter = "";
            if (!string.IsNullOrEmpty(ddlFaculty.SelectedValue))
            {
                facultyFilter = " AND p.faculty_code = @fac";
            }

            // First get total count
            string countSql = string.Format(@"
                SELECT COUNT(*) FROM acad_graduands g
                LEFT JOIN acad_programme p ON g.progcode = p.progcode
                {0} {1}", whereClause, facultyFilter);

            int totalGraduands = 0;
            using (MySqlCommand countCmd = new MySqlCommand(countSql, conn))
            {
                AddWhereParameters(countCmd);
                if (!string.IsNullOrEmpty(ddlFaculty.SelectedValue))
                {
                    countCmd.Parameters.AddWithValue("@fac", ddlFaculty.SelectedValue);
                }
                totalGraduands = Convert.ToInt32(countCmd.ExecuteScalar());
            }

            string sql = string.Format(@"
                SELECT 
                    IFNULL(g.degclass, 'Unclassified') AS degclass,
                    SUM(CASE WHEN s.gender IN ('M', 'Male') THEN 1 ELSE 0 END) AS male_count,
                    SUM(CASE WHEN s.gender IN ('F', 'Female') THEN 1 ELSE 0 END) AS female_count,
                    COUNT(*) AS total
                FROM acad_graduands g
                LEFT JOIN acad_student s ON g.regno = s.regno
                LEFT JOIN acad_programme p ON g.progcode = p.progcode
                {0} {1}
                GROUP BY g.degclass
                ORDER BY 
                    CASE g.degclass 
                        WHEN 'First Class' THEN 1
                        WHEN 'Second Class Upper' THEN 2
                        WHEN 'Second Class Lower' THEN 3
                        WHEN 'Pass' THEN 4
                        ELSE 5
                    END", whereClause, facultyFilter);

            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                AddWhereParameters(cmd);
                if (!string.IsNullOrEmpty(ddlFaculty.SelectedValue))
                {
                    cmd.Parameters.AddWithValue("@fac", ddlFaculty.SelectedValue);
                }

                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    da.Fill(dt);

                    // Add percentage column
                    dt.Columns.Add("percentage", typeof(decimal));
                    foreach (DataRow row in dt.Rows)
                    {
                        int classTotal = Convert.ToInt32(row["total"]);
                        row["percentage"] = totalGraduands > 0 ? (decimal)classTotal * 100 / totalGraduands : 0;
                    }

                    // Add totals row
                    if (dt.Rows.Count > 0)
                    {
                        int totalMale = 0, totalFemale = 0, grandTotal = 0;
                        foreach (DataRow row in dt.Rows)
                        {
                            totalMale += Convert.ToInt32(row["male_count"]);
                            totalFemale += Convert.ToInt32(row["female_count"]);
                            grandTotal += Convert.ToInt32(row["total"]);
                        }

                        DataRow totalRow = dt.NewRow();
                        totalRow["degclass"] = "TOTAL";
                        totalRow["male_count"] = totalMale;
                        totalRow["female_count"] = totalFemale;
                        totalRow["total"] = grandTotal;
                        totalRow["percentage"] = 100m;
                        dt.Rows.Add(totalRow);
                    }

                    _dtClass = dt;
                    gvClassSummary.DataSource = dt;
                    gvClassSummary.DataBind();
                }
            }
        }
    }

    protected void LoadDetailedList()
    {
        using (MySqlConnection conn = new MySqlConnection(connectionString))
        {
            conn.Open();

            string whereClause = GetWhereClause();
            string facultyFilter = "";
            if (!string.IsNullOrEmpty(ddlFaculty.SelectedValue))
            {
                facultyFilter = " AND p.faculty_code = @fac";
            }

            string sql = string.Format(@"
                SELECT 
                    g.regno,
                    g.stud_name,
                    IFNULL(p.progname, g.progcode) AS prog_name,
                    g.cgpa,
                    g.degclass,
                    IFNULL(s.gender, '') AS gen,
                    g.grad_date,
                    g.convocation
                FROM acad_graduands g
                LEFT JOIN acad_student s ON g.regno = s.regno
                LEFT JOIN acad_programme p ON g.progcode = p.progcode
                {0} {1}
                ORDER BY g.stud_name", whereClause, facultyFilter);

            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                AddWhereParameters(cmd);
                if (!string.IsNullOrEmpty(ddlFaculty.SelectedValue))
                {
                    cmd.Parameters.AddWithValue("@fac", ddlFaculty.SelectedValue);
                }

                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    da.Fill(dt);

                    _dtDetail = dt;
                    gvGraduandsDetail.DataSource = dt;
                    gvGraduandsDetail.DataBind();
                }
            }
        }
    }

    protected void UpdateOverallStats()
    {
        using (MySqlConnection conn = new MySqlConnection(connectionString))
        {
            conn.Open();

            string whereClause = GetWhereClause();
            string facultyFilter = "";
            if (!string.IsNullOrEmpty(ddlFaculty.SelectedValue))
            {
                facultyFilter = " AND p.faculty_code = @fac";
            }

            string sql = string.Format(@"
                SELECT 
                    COUNT(*) AS total,
                    SUM(CASE WHEN s.gender IN ('M', 'Male') THEN 1 ELSE 0 END) AS male_count,
                    SUM(CASE WHEN s.gender IN ('F', 'Female') THEN 1 ELSE 0 END) AS female_count
                FROM acad_graduands g
                LEFT JOIN acad_student s ON g.regno = s.regno
                LEFT JOIN acad_programme p ON g.progcode = p.progcode
                {0} {1}", whereClause, facultyFilter);

            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                AddWhereParameters(cmd);
                if (!string.IsNullOrEmpty(ddlFaculty.SelectedValue))
                {
                    cmd.Parameters.AddWithValue("@fac", ddlFaculty.SelectedValue);
                }

                using (MySqlDataReader dr = cmd.ExecuteReader())
                {
                    if (dr.Read())
                    {
                        litTotalGraduands.Text = dr["total"].ToString();
                        litMaleCount.Text = dr["male_count"].ToString();
                        litFemaleCount.Text = dr["female_count"].ToString();
                    }
                }
            }
        }
    }

    private DataTable _dtFaculty, _dtProgramme, _dtClass, _dtDetail;

    // =================================================================
    //  Exports.
    //
    //  These used to call ExportToExcel(gv), which did three things
    //  wrong. It wrote an HTML table with a .xls extension, which makes
    //  Excel open a "the file format does not match" warning every time.
    //  It set gv.AllowPaging = false and called gv.DataBind() — but
    //  LoadAnalysisData only runs when !IsPostBack, so on an export
    //  postback the grid had no DataSource and DataBind() emptied it:
    //  the file that came out had headings and no rows. And nothing
    //  recorded what filters produced it.
    //
    //  They now rebuild the data for the filters currently on screen and
    //  go through GraduationExport, the same writer the rest of the
    //  Graduation module uses — so a file from this page is branded and
    //  carries the same cover sheet as one from the graduation list.
    // =================================================================

    private List<KeyValuePair<string, string>> Cover()
    {
        var c = new List<KeyValuePair<string, string>>();
        c.Add(new KeyValuePair<string, string>("Convocation",
            ddlConvocation.SelectedValue == "" ? "All convocations" : ddlConvocation.SelectedItem.Text));
        c.Add(new KeyValuePair<string, string>("Faculty",
            ddlFaculty.SelectedValue == "" ? "All faculties" : ddlFaculty.SelectedItem.Text));
        c.Add(new KeyValuePair<string, string>("Programme",
            ddlProgramme.SelectedValue == "" ? "All programmes" : ddlProgramme.SelectedItem.Text));
        return c;
    }

    /// <summary>A DataTable as a branded sheet, headings taken from the columns themselves.</summary>
    private static GraduationExport.Sheet SheetOf(DataTable dt, string name)
    {
        var sh = new GraduationExport.Sheet();
        sh.Name = name;
        if (dt == null) { sh.Columns = new[] { "No data" }; return sh; }

        var heads = new string[dt.Columns.Count];
        for (int i = 0; i < dt.Columns.Count; i++)
        {
            heads[i] = Title(dt.Columns[i].ColumnName);
            Type t = dt.Columns[i].DataType;
            if (t == typeof(int) || t == typeof(long) || t == typeof(decimal) ||
                t == typeof(double) || t == typeof(float) || t == typeof(short))
                sh.NumericColumns.Add(i);
        }
        sh.Columns = heads;

        foreach (DataRow r in dt.Rows)
        {
            var cells = new string[dt.Columns.Count];
            for (int i = 0; i < dt.Columns.Count; i++)
                cells[i] = r[i] == null || r[i] == DBNull.Value ? "" : r[i].ToString();
            sh.Rows.Add(cells);
        }
        return sh;
    }

    /// <summary>"male_count" reads as a column name; "Male Count" reads as a heading.</summary>
    private static string Title(string col)
    {
        if (string.IsNullOrEmpty(col)) return "";
        string[] parts = col.Replace('_', ' ').Split(' ');
        var sb = new StringBuilder();
        foreach (string w in parts)
        {
            if (w.Length == 0) continue;
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(char.ToUpperInvariant(w[0])).Append(w.Substring(1));
        }
        return sb.ToString();
    }

    private void Send(DataTable dt, string sheetName, string title, string fileWhat)
    {
        GraduationExport.Workbook(Response,
            GraduationExport.FileName(fileWhat, ddlConvocation.SelectedValue),
            title, Scope.Label, Cover(),
            new List<GraduationExport.Sheet> { SheetOf(dt, sheetName) });
    }

    protected void btnExportFacultyExcel_Click(object sender, EventArgs e)
    {
        if (!Scope.HasAccess) return;
        LoadFacultySummary();
        Send(_dtFaculty, "By faculty", "Graduation Analysis - By Faculty", "graduation-analysis-faculty");
    }

    protected void btnExportProgExcel_Click(object sender, EventArgs e)
    {
        if (!Scope.HasAccess) return;
        LoadProgrammeSummary();
        Send(_dtProgramme, "By programme", "Graduation Analysis - By Programme", "graduation-analysis-programme");
    }

    protected void btnExportClassExcel_Click(object sender, EventArgs e)
    {
        if (!Scope.HasAccess) return;
        LoadClassSummary();
        Send(_dtClass, "By class", "Graduation Analysis - By Class of Award", "graduation-analysis-class");
    }

    /// <summary>
    /// The detailed list, as one workbook carrying all four tables. A reader asking for the
    /// detail almost always wants the summaries that explain it in the same file.
    /// </summary>
    protected void btnExportDetailExcel_Click(object sender, EventArgs e)
    {
        if (!Scope.HasAccess) return;
        LoadAnalysisData();
        var sheets = new List<GraduationExport.Sheet>();
        sheets.Add(SheetOf(_dtDetail, "Graduands"));
        sheets.Add(SheetOf(_dtFaculty, "By faculty"));
        sheets.Add(SheetOf(_dtProgramme, "By programme"));
        sheets.Add(SheetOf(_dtClass, "By class"));
        GraduationExport.Workbook(Response,
            GraduationExport.FileName("graduation-analysis", ddlConvocation.SelectedValue),
            "Graduation Analysis", Scope.Label, Cover(), sheets);
    }

    protected void btnExportFullPDF_Click(object sender, EventArgs e)
    {
        ScriptManager.RegisterStartupScript(this, GetType(), "print", "window.print();", true);
    }

    public override void VerifyRenderingInServerForm(Control control)
    {
        // Required for exporting GridView
    }
}
