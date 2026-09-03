using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

/// <summary>
/// admissions.aspx — Staff-facing admissions management API.
/// All applicant-facing (register, wizard, submit) endpoints are in apply.aspx.
///
/// Column mapping (actual DB schema):
///   acad_applications:    stud_entry_no (PK), stud_name, stud_email, stud_phone,
///                         stud_birthdate, stud_sex, stud_entry_method, app_created_at
///   acad_applicant_choices: stud_entry_no (FK), Choice, prog_id, adm_status, adm_session, stud_reg_no
/// </summary>
public partial class API_v2_admissions : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        if (ApiHelper.HandleCors(Request, Response)) return;
        if (ApiHelper.IsRateLimited(Request, Response)) return;

        string action = ApiHelper.Param(Request, "action", "").ToLower();

        try
        {
            switch (action)
            {
                case "list":               HandleList();              break;
                case "detail":             HandleDetail();            break;
                case "review":             HandleReview();            break;
                case "admit":              HandleAdmit();             break;
                case "reject":             HandleReject();            break;
                case "withdraw":           HandleWithdraw();          break;
                case "register":           HandleRegister();          break;
                case "repair":             HandleRepair();            break;
                case "ensure_account":     HandleEnsureAccount();     break;
                case "add_note":           HandleAddNote();           break;
                case "notes":              HandleNotes();             break;
                case "notify":             HandleNotify();            break;
                case "stats":              HandleStats();             break;
                case "application_status": HandleApplicationStatus(); break;
                default:
                    ApiHelper.Error(Response,
                        "Unknown action: " + action + ". Valid actions: list, detail, review, admit, reject, withdraw, register, repair, ensure_account, add_note, notes, notify, stats, application_status",
                        "INVALID_ACTION");
                    break;
            }
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Internal server error: " + ex.Message, "SERVER_ERROR");
        }
    }

    // adm_status: 0=PENDING, 1=ADMITTED, 2=REJECTED, 3=WITHDRAWN
    private string AdmStatusLabel(int status)
    {
        switch (status) { case 0: return "PENDING"; case 1: return "ADMITTED"; case 2: return "REJECTED"; case 3: return "WITHDRAWN"; default: return "UNKNOWN"; }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  LIST  — paginated applicant list (staff only)
    // ═══════════════════════════════════════════════════════════════════

    private void HandleList()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string statusFilter = ApiHelper.Param(Request, "status",    "");
        string prog         = ApiHelper.Param(Request, "prog",       "");
        string session      = ApiHelper.Param(Request, "session",    "");
        string q            = ApiHelper.Param(Request, "q",          "");
        string acadYear     = ApiHelper.Param(Request, "acad_year",  "");
        int page            = Math.Max(1, ApiHelper.ParamInt(Request, "page", 1));
        int size            = Math.Min(100, Math.Max(1, ApiHelper.ParamInt(Request, "size", 20)));
        int offset          = (page - 1) * size;

        var where = new StringBuilder("WHERE 1=1");
        var parms = new List<MySqlParameter>();

        if (!string.IsNullOrEmpty(statusFilter))
        {
            int statusNum = -1;
            switch (statusFilter.ToUpper())
            {
                case "PENDING":   statusNum = 0; break;
                case "ADMITTED":  statusNum = 1; break;
                case "REJECTED":  statusNum = 2; break;
                case "WITHDRAWN": statusNum = 3; break;
            }
            if (statusNum >= 0) { where.Append(" AND ch.adm_status = @st"); parms.Add(new MySqlParameter("@st", statusNum)); }
        }
        if (!string.IsNullOrEmpty(prog))     { where.Append(" AND ch.prog_id = @prog"); parms.Add(new MySqlParameter("@prog", prog)); }
        if (!string.IsNullOrEmpty(session))  { where.Append(" AND ch.adm_session = @sess"); parms.Add(new MySqlParameter("@sess", session)); }
        if (!string.IsNullOrEmpty(acadYear)) { where.Append(" AND ch.adm_session LIKE @ay"); parms.Add(new MySqlParameter("@ay", "%" + acadYear + "%")); }
        if (!string.IsNullOrEmpty(q))
        {
            where.Append(" AND (a.stud_name LIKE @q OR a.stud_email LIKE @q OR a.stud_phone LIKE @q OR a.stud_entry_no LIKE @q)");
            parms.Add(new MySqlParameter("@q", "%" + q + "%"));
        }

        string baseSql = @"FROM acad_applicant_choices ch
                           INNER JOIN acad_applications a ON a.stud_entry_no = ch.stud_entry_no
                           LEFT JOIN acad_programme p ON p.progcode = ch.prog_id " + where;

        var countParms = new List<MySqlParameter>(parms);
        int total = Convert.ToInt32(ApiHelper.Scalar("SELECT COUNT(*) " + baseSql, countParms.ToArray()));

        parms.Add(new MySqlParameter("@lim", size));
        parms.Add(new MySqlParameter("@off", offset));

        DataTable dt = ApiHelper.Query(
            @"SELECT ch.id AS choice_id, a.stud_entry_no, ch.prog_id, p.progname AS programme_name,
                     ch.adm_session AS session, ch.adm_status,
                     a.stud_name, a.stud_email, a.stud_phone,
                     IFNULL(a.stud_entry_method,'') AS entry_mode,
                     DATE_FORMAT(a.app_created_at,'%Y-%m-%d') AS application_date,
                     IFNULL(a.app_status,'DRAFT') AS app_status,
                     IFNULL(ch.stud_reg_no,'') AS regno
              " + baseSql + " ORDER BY a.app_created_at DESC LIMIT @lim OFFSET @off",
            parms.ToArray());

        var rows = ApiHelper.TableToList(dt);
        foreach (var row in rows)
        {
            int st = row.ContainsKey("adm_status") ? Convert.ToInt32(row["adm_status"]) : 0;
            row["status_label"]  = AdmStatusLabel(st);
            row["is_registered"] = row.ContainsKey("regno") && row["regno"] != null && !string.IsNullOrEmpty(row["regno"].ToString());
        }

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "total", total }, { "page", page }, { "size", size },
            { "pages", (int)Math.Ceiling(total / (double)size) },
            { "applicants", rows }
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DETAIL  — full applicant record
    // ═══════════════════════════════════════════════════════════════════

    private void HandleDetail()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        int    choiceId = ApiHelper.ParamInt(Request, "choice_id", 0);
        string entryNo  = ApiHelper.Param(Request, "entry_no", "").Trim();
        if (choiceId <= 0 && string.IsNullOrEmpty(entryNo)) { ApiHelper.Error(Response, "choice_id or entry_no is required.", "MISSING_PARAM"); return; }

        string condition = choiceId > 0 ? "ch.id = @id" : "a.stud_entry_no = @id";
        object idVal     = choiceId > 0 ? (object)choiceId : (object)entryNo;

        DataTable dt = ApiHelper.Query(
            @"SELECT ch.id AS choice_id, a.stud_entry_no,
                     ch.prog_id, p.progname AS programme_name,
                     ch.adm_session AS session, ch.adm_status, IFNULL(ch.stud_reg_no,'') AS regno,
                     a.stud_name, a.stud_email, a.stud_phone,
                     DATE_FORMAT(a.stud_birthdate,'%Y-%m-%d') AS date_of_birth,
                     a.stud_sex AS gender, a.stud_nationality AS nationality,
                     IFNULL(a.stud_mar_stat,'') AS marital_status,
                     IFNULL(a.physicalDisability,'') AS disability,
                     IFNULL(a.stud_id_number,'') AS national_id,
                     IFNULL(a.stud_phy_address,'') AS address,
                     IFNULL(a.next_kin,'') AS nok_name,
                     IFNULL(a.kin_contacts,'') AS nok_phone,
                     IFNULL(a.kin_relationship,'') AS nok_relationship,
                     IFNULL(a.stud_sponsor,'') AS sponsor,
                     IFNULL(a.olevel_school,'') AS olevel_school,
                     IFNULL(a.olevel_index,'') AS olevel_index,
                     IFNULL(a.stud_pob,'') AS olevel_year,
                     IFNULL(a.stud_district,'') AS olevel_agg,
                     IFNULL(a.alevel_school,'') AS alevel_school,
                     IFNULL(a.alevel_index,'') AS alevel_index,
                     IFNULL(a.alevel_year,0) AS alevel_year,
                     IFNULL(a.stud_ward,'') AS alevel_points,
                     IFNULL(a.stud_entry_method,'') AS entry_mode,
                     IFNULL(a.app_status,'DRAFT') AS app_status,
                     DATE_FORMAT(a.app_submitted_at,'%Y-%m-%d') AS submitted_at,
                     DATE_FORMAT(a.app_created_at,'%Y-%m-%d %H:%i') AS application_date
              FROM acad_applicant_choices ch
              INNER JOIN acad_applications a ON a.stud_entry_no = ch.stud_entry_no
              LEFT JOIN acad_programme p ON p.progcode = ch.prog_id
              WHERE " + condition + " AND ch.Choice = 1 LIMIT 1",
            new MySqlParameter("@id", idVal));

        if (dt.Rows.Count == 0) { ApiHelper.Error(Response, "Applicant not found.", "NOT_FOUND"); return; }

        var applicant = ApiHelper.FirstRowToDict(dt);
        int st = Convert.ToInt32(applicant["adm_status"]);
        applicant["status_label"] = AdmStatusLabel(st);

        // Documents
        try
        {
            DataTable docs = ApiHelper.Query(
                "SELECT id, doc_type, original_filename, file_size_bytes, DATE_FORMAT(uploaded_at,'%Y-%m-%d') AS uploaded_at FROM apply_documents WHERE stud_entry_no=@eno ORDER BY uploaded_at",
                new MySqlParameter("@eno", applicant["stud_entry_no"]));
            applicant["documents"] = ApiHelper.TableToList(docs);
        }
        catch { applicant["documents"] = new List<object>(); }

        // Reviewer notes
        try
        {
            DataTable notes = ApiHelper.Query(
                @"SELECT note_id, note_text, added_by, DATE_FORMAT(added_at,'%Y-%m-%d %H:%i') AS added_at
                  FROM acad_application_notes WHERE stud_entry_no=@eno ORDER BY added_at DESC",
                new MySqlParameter("@eno", applicant["stud_entry_no"]));
            applicant["notes"] = ApiHelper.TableToList(notes);
        }
        catch { applicant["notes"] = new List<object>(); }

        ApiHelper.Success(Response, applicant);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ADMIT  — set adm_status = 1, update app_status = ADMITTED
    // ═══════════════════════════════════════════════════════════════════

    private void HandleAdmit()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        int choiceId = ApiHelper.ParamInt(Request, "choice_id", 0);
        if (choiceId <= 0) { ApiHelper.Error(Response, "choice_id is required.", "MISSING_PARAM"); return; }

        DataTable dt = ApiHelper.Query(
            "SELECT id, adm_status, stud_entry_no, prog_id FROM acad_applicant_choices WHERE id=@id LIMIT 1",
            new MySqlParameter("@id", choiceId));
        if (dt.Rows.Count == 0) { ApiHelper.Error(Response, "Applicant choice not found.", "NOT_FOUND"); return; }

        string entryNo = dt.Rows[0]["stud_entry_no"].ToString();

        ApiHelper.Execute("UPDATE acad_applicant_choices SET adm_status=1 WHERE id=@id",
            new MySqlParameter("@id", choiceId));
        ApiHelper.Execute("UPDATE acad_applications SET app_status='ADMITTED', app_last_updated_at=@now WHERE stud_entry_no=@eno",
            new MySqlParameter("@now", DateTime.UtcNow),
            new MySqlParameter("@eno", entryNo));

        LogAudit(entryNo, auth.UserId, "ADMITTED", "Admitted via API");
        NotifyApplicant(entryNo, "Congratulations! Your application " + entryNo + " has been ADMITTED. Please check your email for further instructions.");

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "choice_id", choiceId }, { "entry_no", entryNo }, { "adm_status", 1 }, { "status_label", "ADMITTED" }
        }, "Applicant admitted");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  REJECT  — set adm_status = 2
    // ═══════════════════════════════════════════════════════════════════

    private void HandleReject()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        int    choiceId = ApiHelper.ParamInt(Request, "choice_id", 0);
        if (choiceId <= 0) { ApiHelper.Error(Response, "choice_id is required.", "MISSING_PARAM"); return; }
        string reason   = ApiHelper.Param(Request, "reason", "");

        DataTable dt = ApiHelper.Query("SELECT stud_entry_no FROM acad_applicant_choices WHERE id=@id LIMIT 1", new MySqlParameter("@id", choiceId));
        if (dt.Rows.Count == 0) { ApiHelper.Error(Response, "Applicant choice not found.", "NOT_FOUND"); return; }

        string entryNo = dt.Rows[0]["stud_entry_no"].ToString();

        ApiHelper.Execute("UPDATE acad_applicant_choices SET adm_status=2 WHERE id=@id", new MySqlParameter("@id", choiceId));
        ApiHelper.Execute("UPDATE acad_applications SET app_status='REJECTED', app_last_updated_at=@now WHERE stud_entry_no=@eno",
            new MySqlParameter("@now", DateTime.UtcNow), new MySqlParameter("@eno", entryNo));

        if (!string.IsNullOrEmpty(reason)) SaveNote(entryNo, "REJECTION REASON: " + reason, auth.UserId);
        LogAudit(entryNo, auth.UserId, "REJECTED", reason);
        NotifyApplicant(entryNo, "Your application " + entryNo + " has not been successful at this time. " + (string.IsNullOrEmpty(reason) ? "" : "Reason: " + reason));

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "choice_id", choiceId }, { "entry_no", entryNo }, { "adm_status", 2 }, { "status_label", "REJECTED" }
        }, "Applicant rejected");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  WITHDRAW  — set adm_status = 3
    // ═══════════════════════════════════════════════════════════════════

    private void HandleWithdraw()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        int    choiceId = ApiHelper.ParamInt(Request, "choice_id", 0);
        if (choiceId <= 0) { ApiHelper.Error(Response, "choice_id is required.", "MISSING_PARAM"); return; }
        string reason   = ApiHelper.Param(Request, "reason", "");

        DataTable dt = ApiHelper.Query("SELECT stud_entry_no FROM acad_applicant_choices WHERE id=@id LIMIT 1", new MySqlParameter("@id", choiceId));
        if (dt.Rows.Count == 0) { ApiHelper.Error(Response, "Applicant choice not found.", "NOT_FOUND"); return; }

        string entryNo = dt.Rows[0]["stud_entry_no"].ToString();

        ApiHelper.Execute("UPDATE acad_applicant_choices SET adm_status=3 WHERE id=@id", new MySqlParameter("@id", choiceId));
        ApiHelper.Execute("UPDATE acad_applications SET app_status='WITHDRAWN', app_last_updated_at=@now WHERE stud_entry_no=@eno",
            new MySqlParameter("@now", DateTime.UtcNow), new MySqlParameter("@eno", entryNo));

        if (!string.IsNullOrEmpty(reason)) SaveNote(entryNo, "WITHDRAWAL REASON: " + reason, auth.UserId);
        LogAudit(entryNo, auth.UserId, "WITHDRAWN", reason);

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "choice_id", choiceId }, { "entry_no", entryNo }, { "adm_status", 3 }, { "status_label", "WITHDRAWN" }
        }, "Application withdrawn");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  REGISTER  — convert ADMITTED applicant → student record
    //  Uses acad_RegNoCreator SP to generate a proper MRU regno.
    // ═══════════════════════════════════════════════════════════════════

    private void HandleRegister()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        int choiceId = ApiHelper.ParamInt(Request, "choice_id", 0);
        if (choiceId <= 0) { ApiHelper.Error(Response, "choice_id is required.", "MISSING_PARAM"); return; }

        DataTable choiceDt = ApiHelper.Query(
            @"SELECT ch.id, ch.stud_entry_no, ch.prog_id, ch.adm_session AS session,
                     ch.adm_status, IFNULL(ch.stud_reg_no,'') AS existing_regno,
                     a.stud_name, a.stud_email, a.stud_phone, a.stud_sex AS gender,
                     a.stud_nationality AS nationality, IFNULL(a.stud_entry_method,'') AS entry_mode,
                     IFNULL(a.stud_intake,'') AS intake, IFNULL(a.stud_campus,'') AS campus,
                     IFNULL(a.stud_entry_year,'') AS entry_year
              FROM acad_applicant_choices ch
              INNER JOIN acad_applications a ON a.stud_entry_no = ch.stud_entry_no
              WHERE ch.id=@id AND ch.Choice=1 LIMIT 1",
            new MySqlParameter("@id", choiceId));

        if (choiceDt.Rows.Count == 0) { ApiHelper.Error(Response, "Choice not found.", "NOT_FOUND"); return; }

        DataRow choice = choiceDt.Rows[0];
        string entryNo = choice["stud_entry_no"].ToString();

        int admStatus = Convert.ToInt32(choice["adm_status"]);
        if (admStatus != 1) { ApiHelper.Error(Response, "Applicant must be ADMITTED before registration.", "BUSINESS_ERROR"); return; }

        // If a reg number is already on the application, there are two cases:
        //  (a) the acad_student row also exists  -> genuinely already registered, refuse.
        //  (b) no acad_student row               -> a stranded/incomplete registration
        //      (reg number was committed but the student was never created). Clear the
        //      stale/placeholder number and fall through to (re)generate + create cleanly.
        string existingRegno = choice["existing_regno"].ToString();
        if (!string.IsNullOrEmpty(existingRegno) && existingRegno != "-")
        {
            bool studentExists = Convert.ToInt32(ApiHelper.Scalar(
                "SELECT COUNT(*) FROM acad_student WHERE regno=@eno",
                new MySqlParameter("@eno", entryNo))) > 0;
            if (studentExists)
            {
                ApiHelper.Error(Response, "Applicant is already registered as student: " + existingRegno, "ALREADY_REGISTERED"); return;
            }
            ApiHelper.Execute("UPDATE acad_applications SET stud_reg_no='' WHERE stud_entry_no=@eno",
                new MySqlParameter("@eno", entryNo));
        }

        // Generate regno using the SP
        string regno = null;
        try
        {
            object spResult = ApiHelper.Scalar("SELECT acad_RegNoCreator(@eno)", new MySqlParameter("@eno", entryNo));
            regno = (spResult != null && spResult != DBNull.Value) ? spResult.ToString() : null;
        }
        catch { }

        // Fallback: manual format matching MRU student regno pattern
        if (string.IsNullOrEmpty(regno) || regno == "-")
        {
            string prog = choice["prog_id"].ToString().Replace("/", "");
            string progCode = prog.Length > 3 ? prog.Substring(0, 3) : prog;
            int seq  = Convert.ToInt32(ApiHelper.Scalar("SELECT COUNT(*)+1 FROM acad_student WHERE progid=@p", new MySqlParameter("@p", choice["prog_id"].ToString())));
            regno = "MRU" + DateTime.Now.Year + progCode + seq.ToString("D5");
        }

        // Update acad_applications with the generated regno
        ApiHelper.Execute("UPDATE acad_applications SET stud_reg_no=@r WHERE stud_entry_no=@eno",
            new MySqlParameter("@r",   regno),
            new MySqlParameter("@eno", entryNo));

        // Use the applicant's ACTUAL entry year (the SP only allows the current academic
        // year). Using DateTime.Now.Year silently mismatched during the year transition and
        // stranded reg numbers with no student row.
        int entryYear;
        if (!int.TryParse(choice["entry_year"].ToString(), out entryYear) || entryYear < 2000)
            entryYear = DateTime.Now.Year;

        // Register via SP. This is NOT one DB transaction, so if the SP aborts (e.g. the
        // year-guard) we must UNDO the reg number we just committed — otherwise the applicant
        // is left "registered" with no acad_student row (a stranded orphan). The undo returns
        // them to a clean ADMITTED state that can be retried.
        try
        {
            ApiHelper.Execute("CALL acad_RegisterApplicant(@yr, @eno, @usr)",
                new MySqlParameter("@yr",  entryYear),
                new MySqlParameter("@eno", entryNo),
                new MySqlParameter("@usr", auth.UserId));
        }
        catch (Exception spEx)
        {
            ApiHelper.Execute("UPDATE acad_applications SET stud_reg_no='' WHERE stud_entry_no=@eno AND stud_reg_no=@r",
                new MySqlParameter("@eno", entryNo), new MySqlParameter("@r", regno));
            ApiHelper.Error(Response, "Registration SP failed (no changes were saved): " + spEx.Message, "SERVER_ERROR"); return;
        }

        // Verify the student row was actually created; if not, undo and fail cleanly.
        bool created = Convert.ToInt32(ApiHelper.Scalar(
            "SELECT COUNT(*) FROM acad_student WHERE regno=@eno", new MySqlParameter("@eno", entryNo))) > 0;
        if (!created)
        {
            ApiHelper.Execute("UPDATE acad_applications SET stud_reg_no='' WHERE stud_entry_no=@eno AND stud_reg_no=@r",
                new MySqlParameter("@eno", entryNo), new MySqlParameter("@r", regno));
            ApiHelper.Error(Response, "Registration did not create the student record (no changes were saved).", "SERVER_ERROR"); return;
        }

        // NOTE: we deliberately do NOT create an acad_registration (semester) row here.
        // Registration = making the person a student (acad_student). SEMESTER registration
        // is a separate, billed, student-initiated step — per policy, never auto-created
        // (see the trg_acadreg_block_autoreg guard against automatic registeredBy).

        // Link regno back to choice row
        ApiHelper.Execute("UPDATE acad_applicant_choices SET choice_reg_no=@r WHERE id=@id",
            new MySqlParameter("@r",  regno),
            new MySqlParameter("@id", choiceId));

        LogAudit(entryNo, auth.UserId, "REGISTERED", "regno=" + regno);

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "choice_id", choiceId }, { "entry_no", entryNo }, { "regno", regno }
        }, "Student registered successfully");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ENSURE ACCOUNT — the canonical "fix a student account" endpoint.
    //  Given any student number (MRU number or slash-form), if it exists in the
    //  system it admits + registers + creates acad_student (if missing) and
    //  provisions the portal login (default password "123"). Idempotent.
    //  Auth: a trusted system caller (SysApiKey header/param — used by the eportal
    //  login self-heal) OR a normal staff token. Runs the single source-of-truth SP
    //  campus_dynamics.sp_EnsureStudentAccount so every caller behaves identically.
    // ═══════════════════════════════════════════════════════════════════
    private void HandleEnsureAccount()
    {
        string sysKey = ConfigurationManager.AppSettings["SysApiKey"] ?? "";
        string provided = (Request.Headers["X-Sys-Key"] ?? ApiHelper.Param(Request, "sys_key", "")).Trim();
        bool systemCall = !string.IsNullOrEmpty(sysKey) && string.Equals(provided, sysKey, StringComparison.Ordinal);

        string actor = "system";
        if (!systemCall)
        {
            TokenInfo auth = TokenManager.RequireAuth(Request, Response);
            if (auth == null) return;
            if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }
            actor = auth.UserId;
        }

        string studentNo = ApiHelper.RequireParam(Request, Response, "student_no");
        if (studentNo == null) return;
        studentNo = studentNo.Trim();
        if (studentNo.Length == 0) { ApiHelper.Error(Response, "student_no is required.", "MISSING_PARAM"); return; }

        DataTable dt;
        try
        {
            dt = ApiHelper.QueryProc("sp_EnsureStudentAccount", new MySqlParameter("p_in", studentNo));
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Account fix failed: " + ex.Message, "SERVER_ERROR"); return;
        }

        bool found = false; string regno = "", resolvedNo = studentNo, action = "not_found"; bool hadLogin = false;
        if (dt != null && dt.Rows.Count > 0)
        {
            DataRow r = dt.Rows[0];
            found      = Convert.ToInt32(r["found"]) == 1;
            regno      = (r["regno"] ?? "").ToString();
            resolvedNo = (r["student_no"] ?? studentNo).ToString();
            hadLogin   = Convert.ToInt32(r["had_login"]) == 1;
            action     = (r["action"] ?? "").ToString();
        }

        if (found)
            LogAudit(resolvedNo, actor, "ACCOUNT_FIX", "ensure_account -> " + action + " (regno=" + regno + ")");

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "found",       found },
            { "can_login",   found },     // fixed accounts sign in with the default password
            { "student_no",  resolvedNo },
            { "regno",       regno },
            { "had_login",   hadLogin },
            { "action",      action }
        }, found ? "Student account is ready." : "No matching student was found for that number.");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  REPAIR  — fix incomplete/missing student or registration records
    // ═══════════════════════════════════════════════════════════════════

    private void HandleRepair()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string regno = ApiHelper.RequireParam(Request, Response, "regno"); if (regno == null) return;

        bool studentExists = Convert.ToInt32(ApiHelper.Scalar("SELECT COUNT(*) FROM acad_student WHERE regno=@r", new MySqlParameter("@r", regno))) > 0;
        bool regExists     = Convert.ToInt32(ApiHelper.Scalar("SELECT COUNT(*) FROM acad_registration WHERE regno=@r", new MySqlParameter("@r", regno))) > 0;

        var repairs = new List<string>();

        if (!studentExists)
        {
            repairs.Add("acad_student record missing — cannot auto-create without source data. Use register endpoint.");
        }

        if (!regExists && studentExists)
        {
            // POLICY: repair must never manufacture a semester registration. Registration is a
            // billed, student-initiated act performed in the eportal wizard (or individually by a
            // named staff member on an admin screen). Creating an 'UNREGISTERED' placeholder here
            // produced unrequested registrations that were subsequently billed. The database also
            // rejects unattributed inserts — see
            // sql/guardrails/acad_registration_require_attribution.sql.
            repairs.Add("No semester registration exists. This is NOT auto-created: the student "
                      + "must register for the semester through the eportal.");
        }

        if (repairs.Count == 0) repairs.Add("No repairs needed — records are intact.");

        ApiHelper.Success(Response, new Dictionary<string, object> { { "regno", regno }, { "actions_taken", repairs } }, "Repair completed");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  REVIEW  — move SUBMITTED → UNDER_REVIEW
    // ═══════════════════════════════════════════════════════════════════

    private void HandleReview()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string entryNo  = ApiHelper.Param(Request, "entry_no", "").Trim();
        int    choiceId = ApiHelper.ParamInt(Request, "choice_id", 0);

        if (string.IsNullOrEmpty(entryNo) && choiceId > 0)
        {
            object v = ApiHelper.Scalar("SELECT stud_entry_no FROM acad_applicant_choices WHERE id=@id LIMIT 1", new MySqlParameter("@id", choiceId));
            if (v != null && v != DBNull.Value) entryNo = v.ToString();
        }
        if (string.IsNullOrEmpty(entryNo)) { ApiHelper.Error(Response, "entry_no or choice_id is required.", "MISSING_PARAM"); return; }

        DataTable dt = ApiHelper.Query(
            "SELECT app_status FROM acad_applications WHERE stud_entry_no=@eno LIMIT 1",
            new MySqlParameter("@eno", entryNo));

        if (dt.Rows.Count == 0) { ApiHelper.Error(Response, "Application not found.", "NOT_FOUND"); return; }

        object curStatusObj = dt.Rows[0]["app_status"];
        string currentStatus = (curStatusObj == null || curStatusObj == DBNull.Value) ? "" : curStatusObj.ToString();
        if (currentStatus != "SUBMITTED")
        {
            ApiHelper.Error(Response,
                "Only SUBMITTED applications can be moved to UNDER_REVIEW. Current status: " + currentStatus,
                "BUSINESS_ERROR");
            return;
        }

        ApiHelper.Execute(
            "UPDATE acad_applications SET app_status='UNDER_REVIEW', app_last_updated_at=@now WHERE stud_entry_no=@eno",
            new MySqlParameter("@now", DateTime.UtcNow),
            new MySqlParameter("@eno", entryNo));

        LogAudit(entryNo, auth.UserId, "UNDER_REVIEW", "Moved to under review via API");
        NotifyApplicant(entryNo, "Your application " + entryNo + " is now under review. You will be notified of the outcome.");

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "entry_no", entryNo }, { "app_status", "UNDER_REVIEW" }
        }, "Application moved to UNDER_REVIEW");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  NOTES  — list reviewer notes for an application
    // ═══════════════════════════════════════════════════════════════════

    private void HandleNotes()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string entryNo  = ApiHelper.Param(Request, "entry_no", "").Trim();
        int    choiceId = ApiHelper.ParamInt(Request, "choice_id", 0);

        if (string.IsNullOrEmpty(entryNo) && choiceId > 0)
        {
            object v = ApiHelper.Scalar("SELECT stud_entry_no FROM acad_applicant_choices WHERE id=@id LIMIT 1", new MySqlParameter("@id", choiceId));
            if (v != null && v != DBNull.Value) entryNo = v.ToString();
        }
        if (string.IsNullOrEmpty(entryNo)) { ApiHelper.Error(Response, "entry_no or choice_id is required.", "MISSING_PARAM"); return; }

        DataTable dt = ApiHelper.Query(
            @"SELECT note_id, note_text, added_by,
                     DATE_FORMAT(added_at,'%Y-%m-%d %H:%i') AS added_at
              FROM acad_application_notes WHERE stud_entry_no=@eno ORDER BY added_at DESC",
            new MySqlParameter("@eno", entryNo));

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "entry_no", entryNo },
            { "total",    dt.Rows.Count },
            { "notes",    ApiHelper.TableToList(dt) }
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  NOTIFY  — send a custom in-app notification to the applicant
    // ═══════════════════════════════════════════════════════════════════

    private void HandleNotify()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string entryNo  = ApiHelper.Param(Request, "entry_no", "").Trim();
        int    choiceId = ApiHelper.ParamInt(Request, "choice_id", 0);
        string message  = ApiHelper.RequireParam(Request, Response, "message"); if (message == null) return;

        if (string.IsNullOrEmpty(entryNo) && choiceId > 0)
        {
            object v = ApiHelper.Scalar("SELECT stud_entry_no FROM acad_applicant_choices WHERE id=@id LIMIT 1", new MySqlParameter("@id", choiceId));
            if (v != null && v != DBNull.Value) entryNo = v.ToString();
        }
        if (string.IsNullOrEmpty(entryNo)) { ApiHelper.Error(Response, "entry_no or choice_id is required.", "MISSING_PARAM"); return; }

        // Verify application exists
        int appExists = Convert.ToInt32(ApiHelper.Scalar(
            "SELECT COUNT(*) FROM acad_applications WHERE stud_entry_no=@eno",
            new MySqlParameter("@eno", entryNo)));
        if (appExists == 0) { ApiHelper.Error(Response, "Application not found.", "NOT_FOUND"); return; }

        NotifyApplicant(entryNo, message.Trim());
        LogAudit(entryNo, auth.UserId, "NOTIFIED", message.Trim());

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "entry_no", entryNo }, { "notified", true }
        }, "Notification sent to applicant");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ADD_NOTE  — save reviewer comment
    // ═══════════════════════════════════════════════════════════════════

    private void HandleAddNote()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string entryNo  = ApiHelper.Param(Request, "entry_no", "").Trim();
        int    choiceId = ApiHelper.ParamInt(Request, "choice_id", 0);

        if (string.IsNullOrEmpty(entryNo) && choiceId > 0)
        {
            object v = ApiHelper.Scalar("SELECT stud_entry_no FROM acad_applicant_choices WHERE id=@id LIMIT 1", new MySqlParameter("@id", choiceId));
            if (v != null && v != DBNull.Value) entryNo = v.ToString();
        }
        if (string.IsNullOrEmpty(entryNo)) { ApiHelper.Error(Response, "entry_no or choice_id is required.", "MISSING_PARAM"); return; }

        string noteText = ApiHelper.RequireParam(Request, Response, "note"); if (noteText == null) return;

        long noteId = SaveNote(entryNo, noteText, auth.UserId);
        ApiHelper.Success(Response, new Dictionary<string, object> { { "note_id", noteId }, { "entry_no", entryNo } }, "Note added");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  STATS
    // ═══════════════════════════════════════════════════════════════════

    private void HandleStats()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string session  = ApiHelper.Param(Request, "session",  "");
        string acadYear = ApiHelper.Param(Request, "acad_year","");

        var parms = new List<MySqlParameter>();
        var where = new StringBuilder("WHERE ch.Choice=1");
        if (!string.IsNullOrEmpty(session))  { where.Append(" AND ch.adm_session=@sess"); parms.Add(new MySqlParameter("@sess", session)); }
        if (!string.IsNullOrEmpty(acadYear)) { where.Append(" AND ch.adm_session LIKE @ay"); parms.Add(new MySqlParameter("@ay", "%" + acadYear + "%")); }

        DataTable overall = ApiHelper.Query(
            @"SELECT COUNT(*) AS total,
                     SUM(ch.adm_status=0) AS pending,
                     SUM(ch.adm_status=1) AS admitted,
                     SUM(ch.adm_status=2) AS rejected,
                     SUM(ch.adm_status=3) AS withdrawn,
                     SUM(ch.adm_status=1 AND ch.stud_reg_no IS NOT NULL AND ch.stud_reg_no<>'') AS registered
              FROM acad_applicant_choices ch " + where,
            parms.ToArray());

        var countParms2 = new List<MySqlParameter>(parms);
        DataTable byProg = ApiHelper.Query(
            @"SELECT ch.prog_id, p.progname AS programme_name, COUNT(*) AS total,
                     SUM(ch.adm_status=0) AS pending, SUM(ch.adm_status=1) AS admitted
              FROM acad_applicant_choices ch
              LEFT JOIN acad_programme p ON p.progcode=ch.prog_id " + where +
            " GROUP BY ch.prog_id ORDER BY total DESC LIMIT 20",
            countParms2.ToArray());

        var stats = overall.Rows.Count > 0 ? ApiHelper.FirstRowToDict(overall) : new Dictionary<string, object>();
        stats["by_programme"] = ApiHelper.TableToList(byProg);

        ApiHelper.Success(Response, stats);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  APPLICATION_STATUS  — public endpoint (no token required)
    // ═══════════════════════════════════════════════════════════════════

    private void HandleApplicationStatus()
    {
        string entryNo = ApiHelper.Param(Request, "entry_no",  "").Trim();
        string appNum  = ApiHelper.Param(Request, "app_number","").Trim(); // backward compat alias
        string dob     = ApiHelper.Param(Request, "dob",       "").Trim();

        if (string.IsNullOrEmpty(entryNo)) entryNo = appNum;
        if (string.IsNullOrEmpty(entryNo) || string.IsNullOrEmpty(dob))
        {
            ApiHelper.Error(Response, "entry_no and dob (YYYY-MM-DD) are required.", "MISSING_PARAM"); return;
        }

        DataTable dt = ApiHelper.Query(
            @"SELECT a.stud_entry_no, a.stud_name, IFNULL(a.app_status,'DRAFT') AS app_status,
                     DATE_FORMAT(a.app_submitted_at,'%Y-%m-%d') AS submitted_at,
                     ch.prog_id, p.progname AS programme_name,
                     ch.adm_status AS choice_status, IFNULL(ch.stud_reg_no,'') AS regno
              FROM acad_applications a
              LEFT JOIN acad_applicant_choices ch ON ch.stud_entry_no=a.stud_entry_no AND ch.Choice=1
              LEFT JOIN acad_programme p ON p.progcode=ch.prog_id
              WHERE TRIM(a.stud_entry_no)=@eno AND DATE(a.stud_birthdate)=@dob LIMIT 1",
            new MySqlParameter("@eno", entryNo),
            new MySqlParameter("@dob", dob));

        if (dt.Rows.Count == 0) { ApiHelper.Error(Response, "Application not found. Check your reference number and date of birth.", "NOT_FOUND"); return; }

        var row = ApiHelper.FirstRowToDict(dt);
        object csObj = row["choice_status"];
        if (csObj != null && csObj != DBNull.Value)
        {
            int cs = Convert.ToInt32(csObj);
            row["status_label"] = AdmStatusLabel(cs);
            if (cs != 1) row.Remove("regno");
        }

        ApiHelper.Success(Response, row);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════

    private long SaveNote(string entryNo, string noteText, string addedBy)
    {
        try
        {
            ApiHelper.Execute(@"CREATE TABLE IF NOT EXISTS acad_application_notes (
                note_id      INT AUTO_INCREMENT PRIMARY KEY,
                stud_entry_no VARCHAR(30) NOT NULL,
                note_text    TEXT NOT NULL,
                added_by     VARCHAR(100) NOT NULL,
                added_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                INDEX idx_ann_eno (stud_entry_no)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
        }
        catch { }

        return ApiHelper.ExecuteInsert(
            "INSERT INTO acad_application_notes (stud_entry_no, note_text, added_by) VALUES (@eno, @nt, @ab)",
            new MySqlParameter("@eno", entryNo),
            new MySqlParameter("@nt",  noteText),
            new MySqlParameter("@ab",  addedBy));
    }

    private void LogAudit(string entryNo, string actor, string action, string detail)
    {
        try
        {
            ApiHelper.Execute(@"CREATE TABLE IF NOT EXISTS apply_audit_log (
                id INT AUTO_INCREMENT PRIMARY KEY,
                stud_entry_no VARCHAR(30) NOT NULL,
                actor VARCHAR(100) NOT NULL,
                action VARCHAR(30) NOT NULL,
                detail TEXT NULL,
                created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                INDEX idx_aal_eno (stud_entry_no)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
        }
        catch { }
        try
        {
            ApiHelper.Execute(
                "INSERT INTO apply_audit_log (stud_entry_no, actor, action, detail) VALUES (@eno, @act, @acn, @det)",
                new MySqlParameter("@eno", entryNo),
                new MySqlParameter("@act", actor),
                new MySqlParameter("@acn", action),
                new MySqlParameter("@det", detail ?? ""));
        }
        catch { }
    }

    private void NotifyApplicant(string entryNo, string message)
    {
        try
        {
            object userId = ApiHelper.Scalar(
                "SELECT applicant_user_id FROM acad_applications WHERE stud_entry_no=@eno LIMIT 1",
                new MySqlParameter("@eno", entryNo));
            if (userId == null || userId == DBNull.Value) return;

            ApiHelper.Execute(
                "INSERT INTO apply_notifications (user_id, message, is_read, link, created_at) VALUES (@uid, @msg, 0, '', @now)",
                new MySqlParameter("@uid", userId),
                new MySqlParameter("@msg", message),
                new MySqlParameter("@now", DateTime.UtcNow));
        }
        catch { }
    }
}
