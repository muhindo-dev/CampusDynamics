using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

/// <summary>
/// API/v2/me — everything a student does TO THEIR OWN RECORD.
///
/// The rest of the student surface is spread across academic/finance/odel/idcard by subject.
/// This module is organised by ownership instead: one place for the things where the student is
/// both the subject and the author. An app has one endpoint to open a home screen with, and one
/// module to look in for "change something about me".
///
/// EVERY ACTION IS SELF-SCOPED AND THERE IS NO regno PARAMETER. The registration number is taken
/// from the token and nowhere else, so there is no argument for a caller to tamper with. Staff
/// tokens are refused outright — a member of staff editing a student's name goes through eadmin,
/// where it is attributed to them and reversible.
///
/// The business rules here are not new. They are the same rules the portal enforces, re-derived
/// server-side against what is actually stored, because the portal is not the authority — the
/// endpoint is. Where a canonical implementation already exists elsewhere in this application
/// (photograph upload), this module hands out a ticket to it rather than growing a second copy
/// that can drift.
/// </summary>
public partial class API_v2_me : System.Web.UI.Page
{
    // Same rules as the portal's MyDateOfBirth page.
    private const int MAX_DOB_CHANGES = 3;
    private const int MIN_AGE_TODAY = 14;
    private const int MAX_AGE_TODAY = 90;
    private const int MIN_AGE_AT_ENTRY = 14;

    // Must match SelfPhotoUpload.ashx and the portal's StudentPhoto page.
    private const string PhotoSecretDefault = "MRU-StudentPhoto-2026-c7f3a9e1b284d6f05a1e9c3b7d24f8a6";
    private const string PhotoUploadUrlDefault = "https://eadmin.mru.ac.ug/COOPERP/StudentInfo/SelfPhotoUpload.ashx";
    private const string PhotoBaseUrlDefault = "https://eadmin.mru.ac.ug/COOPERP/StudentInfo/photos/";

    protected void Page_Load(object sender, EventArgs e)
    {
        if (ApiHelper.HandleCors(Request, Response)) return;
        if (ApiHelper.IsRateLimited(Request, Response)) return;

        string action = ApiHelper.Param(Request, "action", "").ToLower();

        try
        {
            switch (action)
            {
                case "summary":          HandleSummary();        break;
                case "name":             HandleName();           break;
                case "save_name":        HandleSaveName();       break;
                case "dob":              HandleDob();            break;
                case "save_dob":         HandleSaveDob();        break;
                case "photo":            HandlePhoto();          break;
                case "change_password":  HandleChangePassword(); break;
                case "email_journey":    HandleEmailJourney();   break;
                case "notifications":    HandleNotifications();  break;
                case "read_notification": HandleReadNotification(); break;
                case "fee_structure":    HandleFeeStructure();   break;
                case "ping":             ApiHelper.Success(Response, new { service = "me", time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }, "OK"); break;
                default:
                    ApiHelper.Error(Response,
                        "Unknown action: " + action + ". Valid actions: summary, name, save_name, dob, " +
                        "save_dob, photo, change_password, email_journey, notifications, " +
                        "read_notification, fee_structure, ping",
                        "INVALID_ACTION");
                    break;
            }
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Internal server error: " + ex.Message, "SERVER_ERROR");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AUTH — student tokens only, and always about themselves
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>The signed-in student's registration number, or null with the response already
    /// written. Never reads a regno parameter: this module is only ever about the caller.</summary>
    private string Me()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return null;

        if (!string.Equals(auth.UserType, "student", StringComparison.OrdinalIgnoreCase))
        {
            // The superuser integration token is allowed through for testing, but must then say
            // which student it means — it has no record of its own.
            if (TokenManager.IsSpecialToken(auth))
            {
                string asStudent = ApiHelper.Param(Request, "regno", "");
                if (!string.IsNullOrEmpty(asStudent)) return asStudent.Trim();
            }
            ApiHelper.Error(Response,
                "This endpoint is for a signed-in student. Staff manage student records in eadmin, " +
                "where the change is attributed and reversible.", "STUDENT_TOKEN_REQUIRED");
            return null;
        }
        return (auth.UserId ?? "").Trim();
    }

    private static string S(DataRow r, string col)
    {
        return r.Table.Columns.Contains(col) && r[col] != DBNull.Value ? Convert.ToString(r[col]).Trim() : "";
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SUMMARY — one call an app can open its home screen with
    // ═══════════════════════════════════════════════════════════════════

    private void HandleSummary()
    {
        string regno = Me(); if (regno == null) return;

        DataTable s = ApiHelper.Query(
            "SELECT TRIM(s.regno) AS regno, TRIM(IFNULL(s.entryno,'')) AS student_no, " +
            "  TRIM(CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,''))) AS full_name, " +
            "  IFNULL(s.gender,'') AS gender, IFNULL(DATE_FORMAT(s.dob,'%Y-%m-%d'),'') AS dob, " +
            "  IFNULL(s.email,'') AS email, IFNULL(s.studPhone,'') AS phone, " +
            "  TRIM(IFNULL(s.progid,'')) AS programme_code, IFNULL(p.progname,'') AS programme_name, " +
            "  IFNULL(s.entryyear,'') AS entry_year, IFNULL(s.studsesion,'') AS study_session, " +
            "  IFNULL(s.intake,'') AS intake, IFNULL(s.new_status,'') AS status, " +
            "  IFNULL(s.photofile,'') AS photo_file, IFNULL(s.photo_status,'APPROVED') AS photo_status, " +
            "  IFNULL(s.photo_banned,0) AS photo_banned " +
            "FROM acad_student s LEFT JOIN acad_programme p ON p.progcode = TRIM(s.progid) " +
            "WHERE TRIM(s.regno) = @r LIMIT 1",
            new MySqlParameter("@r", regno));

        if (s.Rows.Count == 0)
        {
            ApiHelper.Error(Response, "No student record found for this account.", "NOT_FOUND");
            return;
        }
        DataRow r = s.Rows[0];

        // Registration standing — the most recent semester the student is enrolled in.
        DataTable reg = ApiHelper.Query(
            "SELECT acad_year, semester, studyyear, regstatus FROM acad_registration " +
            "WHERE TRIM(regno) = @r ORDER BY acad_year DESC, semester DESC LIMIT 1",
            new MySqlParameter("@r", regno));

        object registration = null;
        if (reg.Rows.Count > 0)
            registration = new
            {
                acad_year = S(reg.Rows[0], "acad_year"),
                semester = reg.Rows[0]["semester"] == DBNull.Value ? 0 : Convert.ToInt32(reg.Rows[0]["semester"]),
                study_year = reg.Rows[0]["studyyear"] == DBNull.Value ? 0 : Convert.ToInt32(reg.Rows[0]["studyyear"]),
                status = S(reg.Rows[0], "regstatus")
            };

        // Cumulative GPA is derived, never stored.
        double cgpa = 0;
        try
        {
            object v = ApiHelper.Scalar("SELECT acad_CGPAFinder(@r)", new MySqlParameter("@r", regno));
            if (v != null && v != DBNull.Value) cgpa = Convert.ToDouble(v);
        }
        catch { }

        int courses = 0;
        try
        {
            object v = ApiHelper.Scalar("SELECT COUNT(*) FROM acad_results WHERE TRIM(regno) = @r",
                new MySqlParameter("@r", regno));
            courses = v == null ? 0 : Convert.ToInt32(v);
        }
        catch { }

        // Outstanding things the student has asked for, so a home screen can badge them.
        int pendingMarks = 0, pendingRemovals = 0, pendingSemDeletions = 0;
        try
        {
            object v = ApiHelper.ScalarPortal(
                "SELECT COUNT(*) FROM acad_marks_requests WHERE TRIM(regno)=@r AND UPPER(IFNULL(status,''))IN('PENDING','SUBMITTED','IN PROGRESS')",
                new MySqlParameter("@r", regno));
            pendingMarks = v == null ? 0 : Convert.ToInt32(v);
        }
        catch { }
        try
        {
            object v = ApiHelper.ScalarPortal(
                "SELECT COUNT(*) FROM acad_course_deletion_requests WHERE TRIM(regno)=@r AND UPPER(IFNULL(status,''))='PENDING'",
                new MySqlParameter("@r", regno));
            pendingRemovals = v == null ? 0 : Convert.ToInt32(v);
        }
        catch { }
        try
        {
            object v = ApiHelper.ScalarPortal(
                "SELECT COUNT(*) FROM acad_semester_deletion_requests WHERE TRIM(regno)=@r AND UPPER(IFNULL(status,''))='PENDING'",
                new MySqlParameter("@r", regno));
            pendingSemDeletions = v == null ? 0 : Convert.ToInt32(v);
        }
        catch { }

        string photoFile = S(r, "photo_file");
        string dobIso = S(r, "dob");
        DateTime dob;
        bool hasDob = TryIso(dobIso, out dob);

        ApiHelper.Success(Response, new
        {
            student = new
            {
                regno = S(r, "regno"),
                student_no = S(r, "student_no"),
                full_name = S(r, "full_name"),
                gender = S(r, "gender"),
                date_of_birth = dobIso,
                date_of_birth_display = hasDob ? Transcriptly(dob) : "",
                email = S(r, "email"),
                phone = S(r, "phone"),
                programme_code = S(r, "programme_code"),
                programme_name = S(r, "programme_name"),
                entry_year = S(r, "entry_year"),
                study_session = S(r, "study_session"),
                intake = S(r, "intake"),
                status = S(r, "status")
            },
            photo = new
            {
                file = photoFile,
                url = photoFile == "" || photoFile == "-" ? "" : PhotoBaseUrl() + photoFile,
                status = S(r, "photo_status"),
                banned = S(r, "photo_banned") == "1",
                has_photo = photoFile != "" && photoFile != "-"
            },
            registration = registration,
            academics = new { cgpa = Math.Round(cgpa, 2), courses_with_results = courses },
            pending_requests = new
            {
                mark_corrections = pendingMarks,
                course_removals = pendingRemovals,
                semester_deletions = pendingSemDeletions,
                total = pendingMarks + pendingRemovals + pendingSemDeletions
            }
        }, "Student summary");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  NAME — reorder the words, never change them
    // ═══════════════════════════════════════════════════════════════════

    private void HandleName()
    {
        string regno = Me(); if (regno == null) return;

        DataTable dt = ApiHelper.Query(
            "SELECT IFNULL(firstname,'') AS firstname, IFNULL(othername,'') AS othername " +
            "FROM acad_student WHERE TRIM(regno) = @r LIMIT 1", new MySqlParameter("@r", regno));
        if (dt.Rows.Count == 0) { ApiHelper.Error(Response, "No student record found.", "NOT_FOUND"); return; }

        string full = (S(dt.Rows[0], "firstname") + " " + S(dt.Rows[0], "othername")).Trim();
        List<string> words = Words(full);

        DataTable hist = ApiHelper.Query(
            "SELECT IFNULL(old_full,'') AS from_name, IFNULL(new_full,'') AS to_name, " +
            "  DATE_FORMAT(changed_at,'%Y-%m-%d %H:%i') AS changed_at, IFNULL(source,'') AS source " +
            "FROM stud_name_arrangement WHERE TRIM(regno) = @r AND IFNULL(record_type,'NAME')='NAME' " +
            "ORDER BY id DESC LIMIT 20", new MySqlParameter("@r", regno));

        var history = new List<object>();
        foreach (DataRow h in hist.Rows)
            history.Add(new
            {
                from_name = S(h, "from_name"),
                to_name = S(h, "to_name"),
                changed_at = S(h, "changed_at"),
                by_registrar = S(h, "source").IndexOf("reversal", StringComparison.OrdinalIgnoreCase) >= 0
            });

        ApiHelper.Success(Response, new
        {
            full_name = full,
            words = words.ToArray(),
            can_rearrange = words.Count > 1,
            note = "Only the ORDER may change. The words themselves are fixed — a misspelt or " +
                   "missing name is a change of name and must go to the Academic Registrar.",
            history = history
        }, "Name");
    }

    private void HandleSaveName()
    {
        string regno = Me(); if (regno == null) return;

        string ordered = ApiHelper.Param(Request, "words", "");
        if (string.IsNullOrEmpty(ordered))
        {
            ApiHelper.Error(Response, "Send the words in the order you want them, comma-separated, " +
                "as words=NAKATO,SARAH.", "MISSING_PARAM");
            return;
        }
        var wanted = new List<string>();
        foreach (string p in ordered.Split(','))
        {
            string t = (p ?? "").Trim();
            if (t.Length > 0) wanted.Add(t);
        }

        DataTable dt = ApiHelper.Query(
            "SELECT IFNULL(firstname,'') AS firstname, IFNULL(othername,'') AS othername " +
            "FROM acad_student WHERE TRIM(regno) = @r LIMIT 1", new MySqlParameter("@r", regno));
        if (dt.Rows.Count == 0) { ApiHelper.Error(Response, "No student record found.", "NOT_FOUND"); return; }

        string oldFirst = S(dt.Rows[0], "firstname"), oldOther = S(dt.Rows[0], "othername");
        string oldFull = (oldFirst + " " + oldOther).Trim();
        List<string> current = Words(oldFull);

        if (current.Count < 2)
        { ApiHelper.Error(Response, "Your name is a single word, so there is nothing to rearrange.", "NOT_APPLICABLE"); return; }

        // The one rule, checked against what is STORED — not against what the caller says the
        // current name is. Compared case-sensitively: letting "Mugerwa" pass as "MUGERWA" would
        // turn a reordering feature into a way to edit a name.
        string diff;
        if (!SameWords(current, wanted, out diff))
        { ApiHelper.Error(Response, "That is not the same name — " + diff + ". You may only change the order.", "NOT_A_PERMUTATION"); return; }

        string newFull = string.Join(" ", wanted.ToArray());
        if (string.Equals(newFull, oldFull, StringComparison.Ordinal))
        { ApiHelper.Success(Response, new { full_name = oldFull, unchanged = true }, "That is already how your name is arranged."); return; }

        string newFirst = wanted[0];
        string newOther = wanted.Count > 1 ? string.Join(" ", wanted.GetRange(1, wanted.Count - 1).ToArray()) : "";
        if (newFirst.Length > 45 || newOther.Length > 45)
        { ApiHelper.Error(Response, "That order cannot be stored — one part of your name is too long for the field.", "TOO_LONG"); return; }

        ApiHelper.Execute("UPDATE acad_student SET firstname=@f, othername=@o WHERE TRIM(regno)=@r",
            new MySqlParameter("@f", newFirst), new MySqlParameter("@o", newOther), new MySqlParameter("@r", regno));

        ApiHelper.Execute(
            "INSERT INTO stud_name_arrangement (regno, record_type, old_firstname, old_othername, " +
            " new_firstname, new_othername, old_full, new_full, changed_by, source, ip_address, changed_at) " +
            "VALUES (@r,'NAME',@of,@oo,@nf,@no,@ofull,@nfull,@by,'api',@ip,NOW())",
            new MySqlParameter("@r", regno), new MySqlParameter("@of", oldFirst), new MySqlParameter("@oo", oldOther),
            new MySqlParameter("@nf", newFirst), new MySqlParameter("@no", newOther),
            new MySqlParameter("@ofull", oldFull), new MySqlParameter("@nfull", newFull),
            new MySqlParameter("@by", regno), new MySqlParameter("@ip", (object)ClientIp() ?? DBNull.Value));

        ApiHelper.Success(Response, new { full_name = newFull, words = wanted.ToArray() },
            "Saved. Your name now reads \"" + newFull + "\" everywhere, including your transcript.");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DATE OF BIRTH
    // ═══════════════════════════════════════════════════════════════════

    private void HandleDob()
    {
        string regno = Me(); if (regno == null) return;

        DataTable dt = ApiHelper.Query(
            "SELECT IFNULL(DATE_FORMAT(dob,'%Y-%m-%d'),'') AS dob, IFNULL(entryyear,'') AS entry_year " +
            "FROM acad_student WHERE TRIM(regno) = @r LIMIT 1", new MySqlParameter("@r", regno));
        if (dt.Rows.Count == 0) { ApiHelper.Error(Response, "No student record found.", "NOT_FOUND"); return; }

        string iso = S(dt.Rows[0], "dob");
        DateTime d; bool has = TryIso(iso, out d);
        int used = DobChangesUsed(regno);

        bool swappable = false; string swapIso = "", swapDisplay = "";
        if (has && d.Day <= 12 && d.Day != d.Month)
        {
            try
            {
                DateTime sw = new DateTime(d.Year, d.Day, d.Month);
                swappable = true; swapIso = sw.ToString("yyyy-MM-dd"); swapDisplay = Transcriptly(sw);
            }
            catch { }
        }

        DataTable hist = ApiHelper.Query(
            "SELECT IFNULL(old_full,'') AS from_date, IFNULL(new_full,'') AS to_date, " +
            "  DATE_FORMAT(changed_at,'%Y-%m-%d %H:%i') AS changed_at, IFNULL(source,'') AS source " +
            "FROM stud_name_arrangement WHERE TRIM(regno)=@r AND record_type='DOB' ORDER BY id DESC LIMIT 20",
            new MySqlParameter("@r", regno));
        var history = new List<object>();
        foreach (DataRow h in hist.Rows)
            history.Add(new
            {
                from_date = S(h, "from_date"),
                to_date = S(h, "to_date"),
                changed_at = S(h, "changed_at"),
                by_registrar = S(h, "source").IndexOf("reversal", StringComparison.OrdinalIgnoreCase) >= 0
            });

        ApiHelper.Success(Response, new
        {
            date_of_birth = iso,
            display = has ? Transcriptly(d) : "",
            has_date_of_birth = has,
            day = has ? d.Day : 0,
            month = has ? d.Month : 0,
            year = has ? d.Year : 0,
            entry_year = S(dt.Rows[0], "entry_year"),
            min_year = DateTime.Today.Year - MAX_AGE_TODAY,
            max_year = DateTime.Today.Year - MIN_AGE_TODAY,
            corrections_used = used,
            corrections_remaining = Math.Max(0, MAX_DOB_CHANGES - used),
            can_change = used < MAX_DOB_CHANGES,
            day_month_swap = new { available = swappable, date_of_birth = swapIso, display = swapDisplay },
            history = history
        }, "Date of birth");
    }

    private void HandleSaveDob()
    {
        string regno = Me(); if (regno == null) return;

        int day = ApiHelper.ParamInt(Request, "day", 0);
        int month = ApiHelper.ParamInt(Request, "month", 0);
        int year = ApiHelper.ParamInt(Request, "year", 0);

        // Accept an ISO date too — an app should not have to take a date apart.
        string iso = ApiHelper.Param(Request, "date_of_birth", "");
        if (!string.IsNullOrEmpty(iso))
        {
            DateTime parsed;
            if (!TryIso(iso, out parsed))
            { ApiHelper.Error(Response, "date_of_birth must be YYYY-MM-DD.", "BAD_PARAM"); return; }
            day = parsed.Day; month = parsed.Month; year = parsed.Year;
        }

        if (month < 1 || month > 12) { ApiHelper.Error(Response, "That month does not exist.", "BAD_DATE"); return; }
        if (year < 1900 || year > DateTime.Today.Year) { ApiHelper.Error(Response, "That year does not look right.", "BAD_DATE"); return; }
        if (day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            ApiHelper.Error(Response, CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month) + " " + year +
                " has only " + DateTime.DaysInMonth(year, month) + " days.", "BAD_DATE");
            return;
        }

        DateTime wanted = new DateTime(year, month, day);
        if (wanted > DateTime.Today) { ApiHelper.Error(Response, "That date has not happened yet.", "BAD_DATE"); return; }

        int ageToday = YearsBetween(wanted, DateTime.Today);
        if (ageToday < MIN_AGE_TODAY)
        { ApiHelper.Error(Response, "That date would make you " + ageToday + " years old, which cannot be right for a university student.", "IMPLAUSIBLE_AGE"); return; }
        if (ageToday > MAX_AGE_TODAY)
        { ApiHelper.Error(Response, "That date would make you " + ageToday + " years old. If it is correct, see the Academic Registrar.", "IMPLAUSIBLE_AGE"); return; }

        DataTable dt = ApiHelper.Query(
            "SELECT IFNULL(DATE_FORMAT(dob,'%Y-%m-%d'),'') AS dob, IFNULL(entryyear,'') AS entry_year " +
            "FROM acad_student WHERE TRIM(regno)=@r LIMIT 1", new MySqlParameter("@r", regno));
        if (dt.Rows.Count == 0) { ApiHelper.Error(Response, "No student record found.", "NOT_FOUND"); return; }

        string oldIso = S(dt.Rows[0], "dob");
        string entryYear = S(dt.Rows[0], "entry_year");

        int used = DobChangesUsed(regno);
        if (used >= MAX_DOB_CHANGES)
        {
            ApiHelper.Error(Response, "You have already corrected your date of birth " + used +
                " times. Any further correction must go to the Academic Registrar.", "LIMIT_REACHED");
            return;
        }

        string newIso = wanted.ToString("yyyy-MM-dd");
        if (string.Equals(newIso, oldIso, StringComparison.Ordinal))
        { ApiHelper.Success(Response, new { date_of_birth = oldIso, unchanged = true }, "That is already the date on your record."); return; }

        int ey;
        if (int.TryParse(entryYear, out ey) && ey > 1990 && ey <= DateTime.Today.Year + 1)
        {
            int ageAtEntry = YearsBetween(wanted, new DateTime(ey, 12, 31));
            if (ageAtEntry < MIN_AGE_AT_ENTRY)
            {
                ApiHelper.Error(Response, "That date would make you " + Math.Max(0, ageAtEntry) +
                    " years old when you joined the University in " + ey + ".", "IMPLAUSIBLE_AGE");
                return;
            }
        }

        ApiHelper.Execute("UPDATE acad_student SET dob=@d WHERE TRIM(regno)=@r",
            new MySqlParameter("@d", wanted), new MySqlParameter("@r", regno));

        ApiHelper.Execute(
            "INSERT INTO stud_name_arrangement (regno, record_type, old_full, new_full, changed_by, source, ip_address, changed_at) " +
            "VALUES (@r,'DOB',@ofull,@nfull,@by,'api',@ip,NOW())",
            new MySqlParameter("@r", regno), new MySqlParameter("@ofull", oldIso), new MySqlParameter("@nfull", newIso),
            new MySqlParameter("@by", regno), new MySqlParameter("@ip", (object)ClientIp() ?? DBNull.Value));

        int left = Math.Max(0, MAX_DOB_CHANGES - (used + 1));
        ApiHelper.Success(Response, new
        {
            date_of_birth = newIso,
            display = Transcriptly(wanted),
            corrections_remaining = left
        }, "Saved. Your date of birth now reads \"" + Transcriptly(wanted) + "\" on your transcript.");
    }

    private int DobChangesUsed(string regno)
    {
        object v = ApiHelper.Scalar(
            "SELECT COUNT(*) FROM stud_name_arrangement WHERE TRIM(regno)=@r AND record_type='DOB' " +
            "AND IFNULL(source,'') NOT LIKE '%reversal%'", new MySqlParameter("@r", regno));
        return v == null || v == DBNull.Value ? 0 : Convert.ToInt32(v);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PHOTOGRAPH
    //
    //  The file itself is NOT accepted here. SelfPhotoUpload.ashx in this same application is
    //  already the one place that validates an image, builds the thumbnail, names the file and
    //  writes both acad_student.photofile and the stud_photo_change audit row. A second
    //  implementation would be a second set of rules to drift apart. So this returns the state
    //  plus a short-lived ticket for that handler — the same ticket the web page uses.
    // ═══════════════════════════════════════════════════════════════════

    private void HandlePhoto()
    {
        string regno = Me(); if (regno == null) return;

        DataTable dt = ApiHelper.Query(
            "SELECT IFNULL(photofile,'') AS photo_file, IFNULL(photo_status,'APPROVED') AS photo_status, " +
            "  IFNULL(photo_banned,0) AS photo_banned, IFNULL(photo_ban_reason,'') AS ban_reason " +
            "FROM acad_student WHERE TRIM(regno)=@r LIMIT 1", new MySqlParameter("@r", regno));
        if (dt.Rows.Count == 0) { ApiHelper.Error(Response, "No student record found.", "NOT_FOUND"); return; }

        string file = S(dt.Rows[0], "photo_file");
        string status = S(dt.Rows[0], "photo_status");
        bool banned = S(dt.Rows[0], "photo_banned") == "1";
        bool has = file != "" && file != "-";

        DataTable last = ApiHelper.Query(
            "SELECT status, IFNULL(review_comment,'') AS review_comment, " +
            "  DATE_FORMAT(requested_at,'%Y-%m-%d %H:%i') AS requested_at, " +
            "  IFNULL(DATE_FORMAT(reviewed_at,'%Y-%m-%d %H:%i'),'') AS reviewed_at " +
            "FROM stud_photo_change WHERE TRIM(regno)=@r ORDER BY id DESC LIMIT 1",
            new MySqlParameter("@r", regno));

        object lastRequest = null;
        if (last.Rows.Count > 0)
            lastRequest = new
            {
                status = S(last.Rows[0], "status"),
                review_comment = S(last.Rows[0], "review_comment"),
                requested_at = S(last.Rows[0], "requested_at"),
                reviewed_at = S(last.Rows[0], "reviewed_at")
            };

        // 30 minutes, the same window the web page issues.
        DateTime expUtc = DateTime.UtcNow.AddMinutes(30);
        long exp = ToUnix(expUtc);
        string subject = regno + "." + exp.ToString(CultureInfo.InvariantCulture);
        string ticket = banned ? "" : subject + "." + SignPhoto(subject);

        ApiHelper.Success(Response, new
        {
            file = file,
            url = has ? PhotoBaseUrl() + file : "",
            has_photo = has,
            status = status,
            banned = banned,
            ban_reason = S(dt.Rows[0], "ban_reason"),
            can_upload = !banned,
            last_request = lastRequest,
            upload = banned ? null : new
            {
                url = PhotoUploadUrl(),
                method = "POST",
                content_type = "multipart/form-data",
                file_field = "photoFile",
                token_field = "token",
                token = ticket,
                expires_at = expUtc.ToString("yyyy-MM-dd HH:mm:ss") + " UTC",
                expires_unix = exp,
                max_bytes = 2 * 1024 * 1024,
                accepted = new string[] { "jpg", "jpeg", "png", "bmp", "gif" },
                note = "POST the image to this URL with the token. The photograph is then PENDING " +
                       "until an administrator approves it."
            }
        }, "Photograph");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PASSWORD
    // ═══════════════════════════════════════════════════════════════════

    private void HandleChangePassword()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;

        string current = ApiHelper.RequireParam(Request, Response, "current_password");
        if (current == null) return;
        string next = ApiHelper.RequireParam(Request, Response, "new_password");
        if (next == null) return;

        string message;
        if (!PasswordService.ChangePassword(auth.UserId, auth.UserType, current, next, out message))
        { ApiHelper.Error(Response, message, "PASSWORD_CHANGE_FAILED"); return; }

        ApiHelper.Success(Response, new { changed = true }, message);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  UNIVERSITY EMAIL (SEMS)
    //
    //  The 2026 intake onwards are issued a university email through an onboarding journey —
    //  learn, practise, take a short quiz, then the address is revealed. 1,964 addresses,
    //  586 creations and 403 quiz attempts sit behind it, and none of it was reachable.
    // ═══════════════════════════════════════════════════════════════════

    private void HandleEmailJourney()
    {
        string regno = Me(); if (regno == null) return;

        string uniEmail = "";
        string dirStatus = "";
        try
        {
            DataTable d = ApiHelper.QueryPortal(
                "SELECT email, IFNULL(status,'') AS status FROM sems_email_directory " +
                "WHERE owner_type='STUDENT' AND TRIM(owner_ref)=@r ORDER BY id DESC LIMIT 1",
                new MySqlParameter("@r", regno));
            if (d.Rows.Count > 0)
            {
                uniEmail = Convert.ToString(d.Rows[0]["email"]).Trim();
                dirStatus = Convert.ToString(d.Rows[0]["status"]).Trim();
            }
        }
        catch { }

        int attempts = 0, best = 0; bool passed = false; string lastAttempt = "";
        try
        {
            DataTable q = ApiHelper.QueryPortal(
                "SELECT COUNT(*) AS attempts, IFNULL(MAX(score),0) AS best, " +
                "  MAX(CASE WHEN passed='Yes' THEN 1 ELSE 0 END) AS passed, " +
                "  IFNULL(DATE_FORMAT(MAX(completed_at),'%Y-%m-%d %H:%i'),'') AS last_at " +
                "FROM sems_quiz_attempts WHERE TRIM(regno)=@r", new MySqlParameter("@r", regno));
            if (q.Rows.Count > 0)
            {
                attempts = Convert.ToInt32(q.Rows[0]["attempts"]);
                best = Convert.ToInt32(q.Rows[0]["best"]);
                passed = Convert.ToInt32(q.Rows[0]["passed"]) == 1;
                lastAttempt = Convert.ToString(q.Rows[0]["last_at"]);
            }
        }
        catch { }

        int unread = 0;
        try
        {
            object v = ApiHelper.ScalarPortal(
                "SELECT COUNT(*) FROM sems_notifications WHERE TRIM(regno)=@r AND is_read='No'",
                new MySqlParameter("@r", regno));
            unread = v == null ? 0 : Convert.ToInt32(v);
        }
        catch { }

        // The address is only revealed once the quiz is passed — that is the point of the
        // journey, so the API keeps the same order rather than handing it over early.
        bool revealed = passed && uniEmail != "";

        ApiHelper.Success(Response, new
        {
            has_university_email = uniEmail != "",
            university_email = revealed ? uniEmail : "",
            directory_status = dirStatus,
            revealed = revealed,
            quiz = new
            {
                attempts = attempts,
                best_score = best,
                passed = passed,
                last_attempt_at = lastAttempt
            },
            unread_notifications = unread,
            next_step = uniEmail == "" ? "No university address has been created for you yet."
                      : !passed ? "Take the short email quiz to reveal your address."
                      : "Your university email is ready to use."
        }, "University email");
    }

    private void HandleNotifications()
    {
        string regno = Me(); if (regno == null) return;
        int limit = ApiHelper.ParamInt(Request, "limit", 25);
        if (limit < 1 || limit > 100) limit = 25;

        var items = new List<object>();
        int unread = 0;
        try
        {
            DataTable dt = ApiHelper.QueryPortal(
                "SELECT id, IFNULL(title,'') AS title, IFNULL(message,'') AS message, IFNULL(icon,'') AS icon, " +
                "  is_read, DATE_FORMAT(created_at,'%Y-%m-%d %H:%i') AS created_at " +
                "FROM sems_notifications WHERE TRIM(regno)=@r ORDER BY id DESC LIMIT " + limit,
                new MySqlParameter("@r", regno));
            foreach (DataRow r in dt.Rows)
            {
                bool isRead = string.Equals(Convert.ToString(r["is_read"]), "Yes", StringComparison.OrdinalIgnoreCase);
                if (!isRead) unread++;
                items.Add(new
                {
                    id = Convert.ToInt64(r["id"]),
                    title = Convert.ToString(r["title"]),
                    message = Convert.ToString(r["message"]),
                    icon = Convert.ToString(r["icon"]),
                    is_read = isRead,
                    created_at = Convert.ToString(r["created_at"])
                });
            }
        }
        catch { }

        ApiHelper.Success(Response, new { notifications = items, unread_count = unread, count = items.Count },
            "Notifications");
    }

    private void HandleReadNotification()
    {
        string regno = Me(); if (regno == null) return;
        long id = 0;
        long.TryParse(ApiHelper.Param(Request, "id", "0"), out id);

        int n;
        if (id > 0)
            n = ApiHelper.ExecutePortal("UPDATE sems_notifications SET is_read='Yes' WHERE id=@i AND TRIM(regno)=@r",
                new MySqlParameter("@i", id), new MySqlParameter("@r", regno));
        else
            n = ApiHelper.ExecutePortal("UPDATE sems_notifications SET is_read='Yes' WHERE TRIM(regno)=@r AND is_read='No'",
                new MySqlParameter("@r", regno));

        ApiHelper.Success(Response, new { marked_read = n }, id > 0 ? "Marked read." : "All marked read.");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  FEE STRUCTURE — the student's OWN programme, not the whole book
    // ═══════════════════════════════════════════════════════════════════

    private void HandleFeeStructure()
    {
        string regno = Me(); if (regno == null) return;

        DataTable s = ApiHelper.Query(
            "SELECT TRIM(IFNULL(progid,'')) AS prog, IFNULL(p.progname,'') AS prog_name " +
            "FROM acad_student st LEFT JOIN acad_programme p ON p.progcode=TRIM(st.progid) " +
            "WHERE TRIM(st.regno)=@r LIMIT 1", new MySqlParameter("@r", regno));
        if (s.Rows.Count == 0) { ApiHelper.Error(Response, "No student record found.", "NOT_FOUND"); return; }
        string prog = S(s.Rows[0], "prog");

        // Resolved by this student's own study session — see fin_ProgFeeRow. Without it a
        // programme carrying both a MAIN and an INSERVICE structure would return whichever
        // row the engine happened to pick.
        DataTable f = ApiHelper.QueryAccounts(
            "SELECT * FROM fin_programme_fees" +
            " WHERE ID = fin_ProgFeeRow(@p, (SELECT s.studsesion" +
            "                                FROM campus_dynamics.acad_student s" +
            "                                WHERE s.regno = @r LIMIT 1))",
            new MySqlParameter("@p", prog), new MySqlParameter("@r", regno));
        if (f.Rows.Count == 0)
        {
            ApiHelper.Error(Response, "No active fee structure is set for your programme yet.", "NO_FEE_STRUCTURE");
            return;
        }
        DataRow r0 = f.Rows[0];

        // The table is one wide row of yN_sM_tuition / yN_sM_functional cells. Unfolded here so
        // an app gets a list it can render, instead of forty column names to know about.
        var years = new List<object>();
        double grand = 0;
        for (int y = 1; y <= 4; y++)
        {
            if (!r0.Table.Columns.Contains("has_year_" + y)) continue;
            if (!string.Equals(S(r0, "has_year_" + y), "Yes", StringComparison.OrdinalIgnoreCase)) continue;

            var sems = new List<object>();
            double yearTotal = 0;
            for (int m = 1; m <= 3; m++)
            {
                string tc = "y" + y + "_s" + m + "_tuition", fc = "y" + y + "_s" + m + "_functional";
                if (!r0.Table.Columns.Contains(tc)) continue;
                double t = r0[tc] == DBNull.Value ? 0 : Convert.ToDouble(r0[tc]);
                double fn = r0.Table.Columns.Contains(fc) && r0[fc] != DBNull.Value ? Convert.ToDouble(r0[fc]) : 0;
                if (t == 0 && fn == 0) continue;
                sems.Add(new { semester = m, tuition = t, functional = fn, total = t + fn });
                yearTotal += t + fn;
            }
            if (sems.Count == 0) continue;
            years.Add(new { study_year = y, semesters = sems, year_total = yearTotal });
            grand += yearTotal;
        }

        ApiHelper.Success(Response, new
        {
            programme_code = prog,
            programme_name = S(s.Rows[0], "prog_name"),
            currency = "UGX",
            years = years,
            programme_total = grand,
            note = "Tuition and functional fees as currently set for your programme. Retakes, " +
                   "residence and other charges are billed separately."
        }, "Fee structure");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>The words of a name. Any run of whitespace separates words; each is kept exactly
    /// as stored, including capitalisation, apostrophes and hyphens.</summary>
    private static List<string> Words(string s)
    {
        var outp = new List<string>();
        if (string.IsNullOrEmpty(s)) return outp;
        foreach (string w in s.Split(new[] { ' ', '\t', '\r', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string t = w.Trim();
            if (t.Length > 0) outp.Add(t);
        }
        return outp;
    }

    /// <summary>True when two lists hold exactly the same words the same number of times, in any
    /// order. Ordinal, so capitalisation cannot be edited under cover of a reordering.</summary>
    private static bool SameWords(List<string> a, List<string> b, out string difference)
    {
        difference = "";
        if (a.Count != b.Count)
        {
            difference = a.Count > b.Count
                ? "a name is missing — arranging cannot remove one"
                : "there is an extra name — arranging cannot add one";
            return false;
        }
        var left = new List<string>(a); var right = new List<string>(b);
        left.Sort(StringComparer.Ordinal); right.Sort(StringComparer.Ordinal);
        for (int i = 0; i < left.Count; i++)
            if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
            {
                difference = "\"" + right[i] + "\" is not one of your names — arranging cannot change spelling or capitalisation";
                return false;
            }
        return true;
    }

    /// <summary>The date exactly as the transcript prints it — "03 July,2001". Same format string
    /// as acad_GetTranscriptStudData, so the app and the document cannot disagree.</summary>
    public static string Transcriptly(DateTime d)
    {
        return d.ToString("dd", CultureInfo.InvariantCulture) + " " +
               CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(d.Month) + "," +
               d.Year.ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryIso(string iso, out DateTime d)
    {
        d = DateTime.MinValue;
        if (string.IsNullOrEmpty(iso) || iso.StartsWith("0000")) return false;
        return DateTime.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
    }

    private static int YearsBetween(DateTime from, DateTime to)
    {
        int y = to.Year - from.Year;
        if (to.Month < from.Month || (to.Month == from.Month && to.Day < from.Day)) y--;
        return y;
    }

    private static long ToUnix(DateTime utc)
    {
        return (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
    }

    private static string SignPhoto(string data)
    {
        string secret = ConfigurationManager.AppSettings["StudentPhoto.UploadSecret"];
        if (string.IsNullOrEmpty(secret)) secret = PhotoSecretDefault;
        using (HMACSHA256 h = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
        {
            byte[] hash = h.ComputeHash(Encoding.UTF8.GetBytes(data));
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }

    private static string PhotoUploadUrl()
    {
        string v = ConfigurationManager.AppSettings["StudentPhoto.UploadUrl"];
        return string.IsNullOrEmpty(v) ? PhotoUploadUrlDefault : v;
    }

    private static string PhotoBaseUrl()
    {
        string v = ConfigurationManager.AppSettings["StudentPhoto.BaseUrl"];
        return string.IsNullOrEmpty(v) ? PhotoBaseUrlDefault : v;
    }

    private string ClientIp()
    {
        try
        {
            string f = Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            if (!string.IsNullOrEmpty(f)) return f.Split(',')[0].Trim();
            return Request.ServerVariables["REMOTE_ADDR"] ?? Request.UserHostAddress;
        }
        catch { return null; }
    }
}
