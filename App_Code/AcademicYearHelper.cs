using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Web.UI.WebControls;
using MySql.Data.MySqlClient;

/// <summary>
/// Centralised helper for academic year management.
/// Uses the acad_acadyears table as the single source of truth.
/// All pages should call these static methods instead of
/// duplicating year-calculation logic locally.
/// </summary>
public static class AcademicYearHelper
{
    // ───────────────────────────────────────────────────────────
    //  CONNECTION
    // ───────────────────────────────────────────────────────────

    private static string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    // ───────────────────────────────────────────────────────────
    //  GET CURRENT ACADEMIC YEAR  (DB first, date-based fallback)
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the current academic year string (e.g. "2025/2026").
    /// Reads is_current_year = 'Yes' from acad_acadyears first;
    /// if none is set, falls back to date-based calculation.
    /// </summary>
    public static string GetCurrentAcademicYear()
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT acadyear FROM acad_acadyears WHERE is_current_year = 'Yes' LIMIT 1", conn))
                {
                    object result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        string yr = result.ToString().Trim();
                        if (yr.Length > 0) return yr;
                    }
                }
            }
        }
        catch { /* fallback below */ }

        return CalculateAcademicYearFromDate(DateTime.Now);
    }

    /// <summary>
    /// Returns the current financial year string (e.g. "2025/2026").
    /// Reads is_current_financial_year = 'Yes' from acad_acadyears;
    /// falls back to current academic year.
    /// </summary>
    public static string GetCurrentFinancialYear()
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT acadyear FROM acad_acadyears WHERE is_current_financial_year = 'Yes' LIMIT 1", conn))
                {
                    object result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        string yr = result.ToString().Trim();
                        if (yr.Length > 0) return yr;
                    }
                }
            }
        }
        catch { /* fallback */ }

        return GetCurrentAcademicYear();
    }

    // ───────────────────────────────────────────────────────────
    //  CURRENT SEMESTER (date-based)
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns current semester number: Aug-Dec → 1, Jan-Apr → 2, May-Jul → 3.
    /// </summary>
    public static int GetCurrentSemester()
    {
        int month = DateTime.Now.Month;
        if (month >= 8 && month <= 12) return 1;
        if (month >= 1 && month <= 4) return 2;
        return 3;
    }

    // ───────────────────────────────────────────────────────────
    //  DATE-BASED FALLBACK CALCULATION
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// Calculates the academic year from a date. Aug-Dec → YYYY/(YYYY+1); Jan-Jul → (YYYY-1)/YYYY.
    /// </summary>
    public static string CalculateAcademicYearFromDate(DateTime dt)
    {
        int year = dt.Year;
        int month = dt.Month;
        if (month >= 8)
            return string.Format("{0}/{1}", year, year + 1);
        else
            return string.Format("{0}/{1}", year - 1, year);
    }

    // ───────────────────────────────────────────────────────────
    //  DROPDOWN POPULATION
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// Populates a DropDownList with active academic years from the DB.
    /// Filters to (currentYear + 1) maximum. Optionally adds an "All" item.
    /// </summary>
    /// <param name="ddl">The DropDownList to populate.</param>
    /// <param name="includeAll">If true, inserts "All Academic Years" (value = "") at position 0.</param>
    /// <param name="selectCurrent">If true, sets selection to the current academic year.</param>
    public static void PopulateDropDown(DropDownList ddl, bool includeAll, bool selectCurrent)
    {
        ddl.Items.Clear();
        if (includeAll)
            ddl.Items.Add(new ListItem("All Academic Years", ""));

        string currentAcad = GetCurrentAcademicYear();

        // Calculate the maximum year we should show (current calendar year + 1)
        int maxStartYear = DateTime.Now.Year + 1;
        string maxYear = string.Format("{0}/{1}", maxStartYear, maxStartYear + 1);

        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT acadyear FROM acad_acadyears WHERE status = 'Active' AND acadyear <= @maxYear ORDER BY acadyear DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@maxYear", maxYear);
                    using (MySqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            string yr = rdr["acadyear"].ToString();
                            ddl.Items.Add(new ListItem(yr, yr));
                        }
                    }
                }
            }
        }
        catch
        {
            // Fallback: generate years in code if DB unavailable
            int cy = DateTime.Now.Year;
            for (int i = cy + 1; i >= cy - 10; i--)
            {
                string lbl = string.Format("{0}/{1}", i, i + 1);
                ddl.Items.Add(new ListItem(lbl, lbl));
            }
        }

        if (selectCurrent && ddl.Items.FindByValue(currentAcad) != null)
            ddl.SelectedValue = currentAcad;
    }

    /// <summary>
    /// Overload: populate with "All" option and auto-select current year.
    /// </summary>
    public static void PopulateDropDown(DropDownList ddl)
    {
        PopulateDropDown(ddl, true, true);
    }

    /// <summary>
    /// Populates a DropDownList with entry years (plain integers, not acad year format).
    /// Shows from (currentYear + 1) down to (currentYear - 10).
    /// </summary>
    public static void PopulateEntryYearDropDown(DropDownList ddl)
    {
        ddl.Items.Clear();
        int cy = DateTime.Now.Year;
        for (int i = cy + 1; i >= cy - 10; i--)
            ddl.Items.Add(new ListItem(i.ToString(), i.ToString()));
    }

    // ───────────────────────────────────────────────────────────
    //  CRUD — called by management page
    // ───────────────────────────────────────────────────────────

    /// <summary>Returns all academic years ordered by acadyear DESC.</summary>
    public static DataTable GetAllAcademicYears()
    {
        DataTable dt = new DataTable();
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                using (MySqlDataAdapter da = new MySqlDataAdapter(
                    "SELECT * FROM acad_acadyears ORDER BY acadyear DESC", conn))
                {
                    da.Fill(dt);
                }
            }
        }
        catch { /* empty table returned */ }
        return dt;
    }

    // ───────────────────────────────────────────────────────────
    //  ADMISSION  (apply_intakes)
    // ───────────────────────────────────────────────────────────
    //
    //  Whether applicants may apply FOR a given academic year. The row lives in
    //  apply_intakes, which the eportal apply wizard and the v2 API were already
    //  reading before anything could write it:
    //
    //      eportal  apply/apply-step3.aspx.cs  → is_open = 1 and inside the window
    //      API      API/v2/apply.aspx.cs       → HandleIntakes
    //
    //  "2026/2027" is stored as intake_year 2026 with the label "2026/2027".
    //  intake_year remains the value the applicant's choice is saved as, because
    //  acad_applications.stud_intake already holds bare years from the old
    //  fallback; changing the shape now would orphan those.

    /// <summary>The start year of an academic year string: "2026/2027" → 2026.</summary>
    public static int IntakeYearOf(string acadyear)
    {
        if (string.IsNullOrEmpty(acadyear)) return 0;
        string head = acadyear.Trim();
        int slash = head.IndexOf('/');
        if (slash > 0) head = head.Substring(0, slash);
        int n;
        return int.TryParse(head.Trim(), out n) ? n : 0;
    }

    /// <summary>One academic year's admission settings.</summary>
    public class AdmissionState
    {
        public bool IsOpen;
        public DateTime? OpenFrom;
        public DateTime? OpenTo;
        public bool HasRow;          // false when admission was never configured for this year

        /// <summary>Open, and inside its window if one was set — the same test the
        /// eportal applies when it builds the applicant's intake list.</summary>
        public bool IsAcceptingNow
        {
            get
            {
                if (!IsOpen) return false;
                DateTime now = DateTime.Now;
                if (OpenFrom.HasValue && now < OpenFrom.Value) return false;
                if (OpenTo.HasValue && now > OpenTo.Value) return false;
                return true;
            }
        }

        /// <summary>Plain words for the grid, so an officer does not have to work out
        /// why an "open" year is not accepting anyone.</summary>
        public string Describe()
        {
            if (!HasRow) return "Not configured";
            if (!IsOpen) return "Closed";
            DateTime now = DateTime.Now;
            if (OpenFrom.HasValue && now < OpenFrom.Value)
                return "Opens " + OpenFrom.Value.ToString("d MMM yyyy");
            if (OpenTo.HasValue && now > OpenTo.Value)
                return "Closed " + OpenTo.Value.ToString("d MMM yyyy");
            if (OpenTo.HasValue) return "Open until " + OpenTo.Value.ToString("d MMM yyyy");
            return "Open";
        }
    }

    /// <summary>Admission settings for every academic year that has them, keyed by
    /// the academic year string.</summary>
    public static Dictionary<string, AdmissionState> GetAdmissionMap()
    {
        var map = new Dictionary<string, AdmissionState>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT intake_year, intake_label, is_open, open_from, open_to " +
                    "FROM apply_intakes ORDER BY id", conn))
                using (MySqlDataReader rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        int yr = rd.IsDBNull(0) ? 0 : Convert.ToInt32(rd[0]);
                        if (yr <= 0) continue;
                        var st = new AdmissionState();
                        st.HasRow = true;
                        st.IsOpen = !rd.IsDBNull(2) && Convert.ToInt32(rd[2]) == 1;
                        if (!rd.IsDBNull(3)) st.OpenFrom = Convert.ToDateTime(rd[3]);
                        if (!rd.IsDBNull(4)) st.OpenTo = Convert.ToDateTime(rd[4]);
                        // Key on the academic year the label names when it looks like one,
                        // otherwise derive it from the intake year.
                        string key = rd.IsDBNull(1) ? "" : Convert.ToString(rd[1]).Trim();
                        if (key.IndexOf('/') < 0) key = yr + "/" + (yr + 1);
                        map[key] = st;
                    }
                }
            }
        }
        catch { /* table absent or unreadable — every year simply reads as unconfigured */ }
        return map;
    }

    /// <summary>Admission settings for one academic year.</summary>
    public static AdmissionState GetAdmission(string acadyear)
    {
        Dictionary<string, AdmissionState> map = GetAdmissionMap();
        AdmissionState st;
        if (map.TryGetValue((acadyear ?? "").Trim(), out st)) return st;
        return new AdmissionState();
    }

    /// <summary>
    /// Opens or closes admission for an academic year, with an optional window.
    /// Returns "" on success or a message describing what stopped it.
    ///
    /// apply_intakes has no unique key on intake_year, so this updates in place where a
    /// row exists and inserts only when none does — otherwise repeated saves would leave
    /// several rows for one year and the application list would show it twice.
    /// </summary>
    public static string SetAdmission(string acadyear, bool isOpen,
                                      DateTime? openFrom, DateTime? openTo, string user)
    {
        int yr = IntakeYearOf(acadyear);
        if (yr <= 0) return "That academic year could not be read.";
        if (openFrom.HasValue && openTo.HasValue && openTo.Value < openFrom.Value)
            return "The admission close date cannot be before the open date.";

        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                int affected;
                using (MySqlCommand cmd = new MySqlCommand(
                    "UPDATE apply_intakes SET intake_label=@lbl, is_open=@open, " +
                    "open_from=@from, open_to=@to WHERE intake_year=@yr", conn))
                {
                    cmd.Parameters.AddWithValue("@lbl", acadyear.Trim());
                    cmd.Parameters.AddWithValue("@open", isOpen ? 1 : 0);
                    cmd.Parameters.AddWithValue("@from", openFrom.HasValue ? (object)openFrom.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@to", openTo.HasValue ? (object)openTo.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@yr", yr);
                    affected = cmd.ExecuteNonQuery();
                }

                if (affected == 0)
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        "INSERT INTO apply_intakes (intake_year, intake_label, session_type, " +
                        " is_open, open_from, open_to, created_at) " +
                        "VALUES (@yr, @lbl, 'ALL', @open, @from, @to, NOW())", conn))
                    {
                        cmd.Parameters.AddWithValue("@yr", yr);
                        cmd.Parameters.AddWithValue("@lbl", acadyear.Trim());
                        cmd.Parameters.AddWithValue("@open", isOpen ? 1 : 0);
                        cmd.Parameters.AddWithValue("@from", openFrom.HasValue ? (object)openFrom.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@to", openTo.HasValue ? (object)openTo.Value : DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            return "";
        }
        catch (MySqlException ex)
        {
            return "Admission settings could not be saved: " + ex.Message;
        }
    }

    /// <summary>Academic years currently accepting applications, newest first. This is
    /// what an applicant should be offered.</summary>
    public static List<string> GetOpenAdmissionYears()
    {
        var open = new List<string>();
        foreach (KeyValuePair<string, AdmissionState> kv in GetAdmissionMap())
            if (kv.Value.IsAcceptingNow) open.Add(kv.Key);
        open.Sort();
        open.Reverse();
        return open;
    }

    /// <summary>
    /// The year a NEW application belongs to — the year the University is admitting for,
    /// which is not necessarily the year the form is being filled in.
    ///
    /// This is what goes into the application number (MRU + year + sequence) and into
    /// stud_entry_year. Before this existed both were DateTime.Now.Year, so an applicant
    /// choosing the 2027/2028 intake in September 2026 still got an MRU2026 number.
    ///
    /// When more than one year is open the latest wins: a year left open by accident is far
    /// more likely to be a stale past one than a future one, and the applicant's own intake
    /// list is ordered the same way.
    ///
    /// Falls back to the calendar year only when admission has never been configured, the
    /// same rule the portal's intake list follows.
    /// </summary>
    public static int CurrentApplicationYear()
    {
        List<string> open = GetOpenAdmissionYears();   // already latest-first
        for (int i = 0; i < open.Count; i++)
        {
            int y = IntakeYearOf(open[i]);
            if (y > 0) return y;
        }
        return DateTime.Now.Year;
    }

    /// <summary>Returns a single academic year row by its acadyear string.</summary>
    public static DataRow GetAcademicYear(string acadyear)
    {
        DataTable dt = new DataTable();
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT * FROM acad_acadyears WHERE acadyear = @ay", conn))
                {
                    cmd.Parameters.AddWithValue("@ay", acadyear);
                    using (MySqlDataAdapter da = new MySqlDataAdapter(cmd))
                        da.Fill(dt);
                }
            }
        }
        catch { }
        return dt.Rows.Count > 0 ? dt.Rows[0] : null;
    }

    /// <summary>Returns a single academic year row by its ID.</summary>
    public static DataRow GetAcademicYearById(int id)
    {
        DataTable dt = new DataTable();
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT * FROM acad_acadyears WHERE ID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (MySqlDataAdapter da = new MySqlDataAdapter(cmd))
                        da.Fill(dt);
                }
            }
        }
        catch { }
        return dt.Rows.Count > 0 ? dt.Rows[0] : null;
    }

    /// <summary>
    /// Ensures the three semester_X_is_active columns exist on acad_acadyears.
    /// Runs ALTER TABLE automatically the first time it is needed (MySQL 5.6 compatible).
    /// </summary>
    private static void EnsureSemesterColumns(MySqlConnection conn)
    {
        // Check whether the column already exists
        using (MySqlCommand chk = new MySqlCommand(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'acad_acadyears' " +
            "AND COLUMN_NAME = 'semester_1_is_active'", conn))
        {
            int exists = Convert.ToInt32(chk.ExecuteScalar());
            if (exists > 0) return;   // already present — nothing to do
        }

        // Add all three columns (MySQL 5.6 — no IF NOT EXISTS on ALTER)
        using (MySqlCommand alter = new MySqlCommand(
            "ALTER TABLE acad_acadyears " +
            "ADD COLUMN semester_1_is_active ENUM('Yes','No') NOT NULL DEFAULT 'No', " +
            "ADD COLUMN semester_2_is_active ENUM('Yes','No') NOT NULL DEFAULT 'No', " +
            "ADD COLUMN semester_3_is_active ENUM('Yes','No') NOT NULL DEFAULT 'No'", conn))
        {
            alter.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Adds a new academic year. Returns empty string on success, error message on failure.
    /// </summary>
    public static string AddAcademicYear(string acadyear, DateTime startDate, DateTime endDate,
        int semesterCount, string description, string status, string createdBy,
        string sem1Active = "No", string sem2Active = "No", string sem3Active = "No")
    {
        string err = ValidateAcademicYear(acadyear, startDate, endDate);
        if (!string.IsNullOrEmpty(err)) return err;

        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                // Check duplicate
                using (MySqlCommand chk = new MySqlCommand(
                    "SELECT COUNT(*) FROM acad_acadyears WHERE acadyear = @ay", conn))
                {
                    chk.Parameters.AddWithValue("@ay", acadyear);
                    if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                        return "Academic year '" + acadyear + "' already exists.";
                }

                using (MySqlCommand cmd = new MySqlCommand(@"
                    INSERT INTO acad_acadyears
                        (acadyear, start_date, end_date, semester_count,
                         description, status, created_date, modified_by)
                    VALUES
                        (@ay, @sd, @ed, @sc, @desc, @st, NOW(), @by)", conn))
                {
                    cmd.Parameters.AddWithValue("@ay",   acadyear);
                    cmd.Parameters.AddWithValue("@sd",   startDate);
                    cmd.Parameters.AddWithValue("@ed",   endDate);
                    cmd.Parameters.AddWithValue("@sc",   semesterCount);
                    cmd.Parameters.AddWithValue("@desc", string.IsNullOrEmpty(description) ? (object)DBNull.Value : description);
                    cmd.Parameters.AddWithValue("@st",   status);
                    cmd.Parameters.AddWithValue("@by",   createdBy);
                    cmd.ExecuteNonQuery();
                }

                // Auto-create semester columns if they don't exist, then save values.
                try
                {
                    EnsureSemesterColumns(conn);
                    using (MySqlCommand semCmd = new MySqlCommand(@"
                        UPDATE acad_acadyears
                        SET semester_1_is_active = @s1,
                            semester_2_is_active = @s2,
                            semester_3_is_active = @s3
                        WHERE acadyear = @ay", conn))
                    {
                        semCmd.Parameters.AddWithValue("@ay", acadyear);
                        semCmd.Parameters.AddWithValue("@s1", sem1Active == "Yes" ? "Yes" : "No");
                        semCmd.Parameters.AddWithValue("@s2", sem2Active == "Yes" ? "Yes" : "No");
                        semCmd.Parameters.AddWithValue("@s3", sem3Active == "Yes" ? "Yes" : "No");
                        semCmd.ExecuteNonQuery();
                    }
                }
                catch { /* DB permission error — semester columns not saved */ }
            }
            return "";
        }
        catch (Exception ex)
        {
            return "Database error: " + ex.Message;
        }
    }

    /// <summary>
    /// Updates an existing academic year. Returns empty string on success.
    /// </summary>
    public static string UpdateAcademicYear(int id, string acadyear, DateTime startDate, DateTime endDate,
        int semesterCount, string description, string status, string modifiedBy,
        string sem1Active = "No", string sem2Active = "No", string sem3Active = "No")
    {
        string err = ValidateAcademicYear(acadyear, startDate, endDate);
        if (!string.IsNullOrEmpty(err)) return err;

        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                // Check duplicate (exclude self)
                using (MySqlCommand chk = new MySqlCommand(
                    "SELECT COUNT(*) FROM acad_acadyears WHERE acadyear = @ay AND ID != @id", conn))
                {
                    chk.Parameters.AddWithValue("@ay", acadyear);
                    chk.Parameters.AddWithValue("@id", id);
                    if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                        return "Academic year '" + acadyear + "' already exists.";
                }

                using (MySqlCommand cmd = new MySqlCommand(@"
                    UPDATE acad_acadyears SET
                        acadyear              = @ay,
                        start_date            = @sd,
                        end_date              = @ed,
                        semester_count        = @sc,
                        description           = @desc,
                        status                = @st,
                        modified_date         = NOW(),
                        modified_by           = @by
                    WHERE ID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id",   id);
                    cmd.Parameters.AddWithValue("@ay",   acadyear);
                    cmd.Parameters.AddWithValue("@sd",   startDate);
                    cmd.Parameters.AddWithValue("@ed",   endDate);
                    cmd.Parameters.AddWithValue("@sc",   semesterCount);
                    cmd.Parameters.AddWithValue("@desc", string.IsNullOrEmpty(description) ? (object)DBNull.Value : description);
                    cmd.Parameters.AddWithValue("@st",   status);
                    cmd.Parameters.AddWithValue("@by",   modifiedBy);
                    cmd.ExecuteNonQuery();
                }

                // Auto-create semester columns if they don't exist, then save values.
                try
                {
                    EnsureSemesterColumns(conn);
                    using (MySqlCommand semCmd = new MySqlCommand(@"
                        UPDATE acad_acadyears
                        SET semester_1_is_active = @s1,
                            semester_2_is_active = @s2,
                            semester_3_is_active = @s3
                        WHERE ID = @id", conn))
                    {
                        semCmd.Parameters.AddWithValue("@id", id);
                        semCmd.Parameters.AddWithValue("@s1", sem1Active == "Yes" ? "Yes" : "No");
                        semCmd.Parameters.AddWithValue("@s2", sem2Active == "Yes" ? "Yes" : "No");
                        semCmd.Parameters.AddWithValue("@s3", sem3Active == "Yes" ? "Yes" : "No");
                        semCmd.ExecuteNonQuery();
                    }
                }
                catch { /* DB permission error — semester columns not saved */ }
            }
            return "";
        }
        catch (Exception ex)
        {
            return "Database error: " + ex.Message;
        }
    }

    /// <summary>
    /// Deletes an academic year (only if not used anywhere). Returns empty string on success.
    /// </summary>
    public static string DeleteAcademicYear(int id)
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();

                // Get the acadyear value first
                string acadyear = "";
                using (MySqlCommand getCmd = new MySqlCommand("SELECT acadyear FROM acad_acadyears WHERE ID = @id", conn))
                {
                    getCmd.Parameters.AddWithValue("@id", id);
                    object r = getCmd.ExecuteScalar();
                    if (r == null) return "Academic year not found.";
                    acadyear = r.ToString();
                }

                // Check if used in registrations
                using (MySqlCommand chk = new MySqlCommand(
                    "SELECT COUNT(*) FROM acad_registration WHERE acad_year = @ay LIMIT 1", conn))
                {
                    chk.Parameters.AddWithValue("@ay", acadyear);
                    if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                        return "Cannot delete — this academic year has registration records.";
                }

                using (MySqlCommand cmd = new MySqlCommand("DELETE FROM acad_acadyears WHERE ID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
            return "";
        }
        catch (Exception ex)
        {
            return "Database error: " + ex.Message;
        }
    }

    /// <summary>
    /// Sets is_current_year = 'Yes' for the given acadyear and 'No' for all others.
    /// </summary>
    public static string SetCurrentAcademicYear(string acadyear, string modifiedBy)
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(@"
                    UPDATE acad_acadyears SET is_current_year = 'No', modified_date = NOW(), modified_by = @by;
                    UPDATE acad_acadyears SET is_current_year = 'Yes', modified_date = NOW(), modified_by = @by WHERE acadyear = @ay;", conn))
                {
                    cmd.Parameters.AddWithValue("@ay", acadyear);
                    cmd.Parameters.AddWithValue("@by", modifiedBy);
                    cmd.ExecuteNonQuery();
                }
            }
            return "";
        }
        catch (Exception ex)
        {
            return "Database error: " + ex.Message;
        }
    }

    /// <summary>
    /// Sets is_current_financial_year = 'Yes' for the given acadyear and 'No' for all others.
    /// </summary>
    public static string SetCurrentFinancialYear(string acadyear, string modifiedBy)
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(@"
                    UPDATE acad_acadyears SET is_current_financial_year = 'No', modified_date = NOW(), modified_by = @by;
                    UPDATE acad_acadyears SET is_current_financial_year = 'Yes', modified_date = NOW(), modified_by = @by WHERE acadyear = @ay;", conn))
                {
                    cmd.Parameters.AddWithValue("@ay", acadyear);
                    cmd.Parameters.AddWithValue("@by", modifiedBy);
                    cmd.ExecuteNonQuery();
                }
            }
            return "";
        }
        catch (Exception ex)
        {
            return "Database error: " + ex.Message;
        }
    }

    // ───────────────────────────────────────────────────────────
    //  VALIDATION
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// Validates the acadyear format (e.g. "2025/2026") and date range.
    /// Returns empty string on success, error message on failure.
    /// </summary>
    public static string ValidateAcademicYear(string acadyear, DateTime startDate, DateTime endDate)
    {
        if (string.IsNullOrEmpty(acadyear))
            return "Academic year is required.";

        string[] parts = acadyear.Split('/');
        if (parts.Length != 2)
            return "Academic year must be in the format YYYY/YYYY (e.g. 2025/2026).";

        int y1, y2;
        if (!int.TryParse(parts[0], out y1) || !int.TryParse(parts[1], out y2))
            return "Academic year must contain valid years (e.g. 2025/2026).";

        if (y2 != y1 + 1)
            return "The second year must be exactly one more than the first (e.g. 2025/2026).";

        if (y1 < 2000 || y1 > 2099)
            return "Academic year must be between 2000 and 2099.";

        if (endDate <= startDate)
            return "End date must be after start date.";

        return "";
    }

    /// <summary>
    /// Validates only the format of an acadyear string. Returns true if valid.
    /// </summary>
    public static bool IsValidFormat(string acadyear)
    {
        if (string.IsNullOrEmpty(acadyear)) return false;
        string[] parts = acadyear.Split('/');
        if (parts.Length != 2) return false;
        int y1, y2;
        if (!int.TryParse(parts[0], out y1) || !int.TryParse(parts[1], out y2)) return false;
        return y2 == y1 + 1;
    }

    // ───────────────────────────────────────────────────────────
    //  SEMESTER ACTIVE STATUS
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the given semester number (1, 2 or 3) is marked as active
    /// on the current academic year row in acad_acadyears.
    /// Defaults to false when the column is absent or the DB is unavailable.
    /// </summary>
    public static bool IsSemesterActive(int semesterNum)
    {
        if (semesterNum < 1 || semesterNum > 3) return false;
        string colName = "semester_" + semesterNum + "_is_active";
        string acadYear = GetCurrentAcademicYear();
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                string sql = "SELECT " + colName + " FROM acad_acadyears WHERE acadyear = @ay LIMIT 1";
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ay", acadYear);
                    object result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                        return result.ToString() == "Yes";
                }
            }
        }
        catch { /* fallback: false */ }
        return false;
    }

    /// <summary>
    /// Returns a human-readable comma-separated list of active semesters
    /// for the given academic year (e.g. "Sem 1, Sem 2").
    /// Pass null to use the current academic year.
    /// Returns "None" when no semesters are active.
    /// </summary>
    public static string GetActiveSemestersDisplay(string acadYear = null)
    {
        if (string.IsNullOrEmpty(acadYear))
            acadYear = GetCurrentAcademicYear();

        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    @"SELECT semester_1_is_active, semester_2_is_active, semester_3_is_active
                      FROM acad_acadyears WHERE acadyear = @ay LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@ay", acadYear);
                    using (MySqlDataReader rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            var active = new System.Collections.Generic.List<string>();
                            if (rdr["semester_1_is_active"].ToString() == "Yes") active.Add("Sem 1");
                            if (rdr["semester_2_is_active"].ToString() == "Yes") active.Add("Sem 2");
                            if (rdr["semester_3_is_active"].ToString() == "Yes") active.Add("Sem 3");
                            return active.Count > 0 ? string.Join(", ", active.ToArray()) : "None";
                        }
                    }
                }
            }
        }
        catch { }
        return "-";
    }

    // ───────────────────────────────────────────────────────────
    //  REGISTRATION YEAR ENFORCEMENT
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the given academic year matches the current academic year.
    /// Used to enforce that registrations can only be created for the current year.
    /// </summary>
    public static bool IsCurrentAcademicYear(string acadYear)
    {
        if (string.IsNullOrEmpty(acadYear)) return false;
        string current = GetCurrentAcademicYear();
        return string.Equals(acadYear.Trim(), current.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Converts an entry year integer (e.g. 2025) to an academic year string ("2025/2026")
    /// and checks if it matches the current academic year.
    /// </summary>
    public static bool IsCurrentEntryYear(int entryYear)
    {
        string acadYear = string.Format("{0}/{1}", entryYear, entryYear + 1);
        return IsCurrentAcademicYear(acadYear);
    }

    /// <summary>
    /// Returns the current academic year string for use in error messages.
    /// </summary>
    public static string GetCurrentYearDisplay()
    {
        string cur = GetCurrentAcademicYear();
        return !string.IsNullOrEmpty(cur) ? cur : "(not set)";
    }
}
