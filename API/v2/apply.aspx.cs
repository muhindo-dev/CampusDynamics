using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

/// <summary>
/// apply.aspx — Student Application API (applicant-facing)
/// Handles the full applicant lifecycle: register → verify → fill wizard → submit → track.
/// Staff/admin admissions management stays in admissions.aspx.
/// </summary>
public partial class API_v2_apply : System.Web.UI.Page
{
    private static volatile bool _columnsEnsured = false;

    private static void EnsureApplyColumns()
    {
        if (_columnsEnsured) return;
        try
        {
            using (MySqlConnection conn = ApiHelper.GetConnection())
            {
                conn.Open();

                // acad_applicant_choices needs a column to store the assigned reg no.
                // Use 'choice_reg_no' to avoid ambiguity with acad_applications.stud_reg_no
                // in the acad_GetApplicants stored procedure (which JOINs both tables).
                long hasOld = Convert.ToInt64(new MySqlCommand(
                    "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
                    "WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='acad_applicant_choices' AND COLUMN_NAME='stud_reg_no'", conn).ExecuteScalar());
                long hasNew = Convert.ToInt64(new MySqlCommand(
                    "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
                    "WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='acad_applicant_choices' AND COLUMN_NAME='choice_reg_no'", conn).ExecuteScalar());

                if (hasOld > 0 && hasNew == 0)
                {
                    // Rename the conflicting column — fixes the stored procedure ambiguity
                    new MySqlCommand(
                        "ALTER TABLE acad_applicant_choices CHANGE COLUMN stud_reg_no choice_reg_no VARCHAR(50) NULL",
                        conn).ExecuteNonQuery();
                }
                else if (hasOld == 0 && hasNew == 0)
                {
                    // Fresh install — add with the correct name
                    new MySqlCommand(
                        "ALTER TABLE acad_applicant_choices ADD COLUMN choice_reg_no VARCHAR(50) NULL",
                        conn).ExecuteNonQuery();
                }
                // If both exist somehow, leave as-is (safe)
            }
        }
        catch { /* best-effort: don't block the API if DDL fails */ }
        finally { _columnsEnsured = true; }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        if (ApiHelper.HandleCors(Request, Response)) return;
        if (ApiHelper.IsRateLimited(Request, Response, action: Request["action"] == "login" ? "login" : "")) return;
        EnsureApplyColumns();

        string action = ApiHelper.Param(Request, "action", "").ToLower();

        try
        {
            switch (action)
            {
                // ── Public auth ──
                case "register":          HandleRegister();          break;
                case "login":             HandleLogin();             break;
                case "verify_email":      HandleVerifyEmail();       break;
                case "resend_otp":        HandleResendOtp();         break;
                case "forgot_password":   HandleForgotPassword();    break;
                case "reset_password":    HandleResetPassword();     break;
                // ── Applicant profile ──
                case "my_profile":        HandleMyProfile();         break;
                case "change_password":   HandleChangePassword();    break;
                // ── Application wizard ──
                case "get_draft":         HandleGetDraft();          break;
                case "save_step1":        HandleSaveStep1();         break;
                case "save_step2":        HandleSaveStep2();         break;
                case "save_step3":        HandleSaveStep3();         break;
                case "submit":            HandleSubmit();            break;
                case "my_application":    HandleMyApplication();     break;
                // ── Documents ──
                case "documents":         HandleDocuments();         break;
                case "upload_document":   HandleUploadDocument();    break;
                case "delete_document":   HandleDeleteDocument();    break;
                case "get_document":      HandleGetDocument();       break;
                // ── Notifications ──
                case "notifications":     HandleNotifications();     break;
                case "mark_read":         HandleMarkRead();          break;
                // ── Public reference data ──
                case "programmes":        HandleProgrammes();        break;
                case "faculties":         HandleFaculties();         break;
                case "campuses":          HandleCampuses();          break;
                case "intakes":           HandleIntakes();           break;
                case "check_status":      HandleCheckStatus();       break;
                default:
                    ApiHelper.Error(Response,
                        "Unknown action. Valid actions: register, login, verify_email, resend_otp, " +
                        "forgot_password, reset_password, my_profile, change_password, " +
                        "get_draft, save_step1, save_step2, save_step3, submit, my_application, " +
                        "documents, upload_document, delete_document, get_document, " +
                        "notifications, mark_read, " +
                        "programmes, faculties, campuses, intakes, check_status",
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
    //  AUTH — PUBLIC (no token required)
    // ═══════════════════════════════════════════════════════════════════

    private void HandleRegister()
    {
        string firstName = ApiHelper.RequireParam(Request, Response, "first_name"); if (firstName == null) return;
        string lastName  = ApiHelper.RequireParam(Request, Response, "last_name");  if (lastName == null) return;
        string email     = ApiHelper.RequireParam(Request, Response, "email");      if (email == null) return;
        string password  = ApiHelper.RequireParam(Request, Response, "password");   if (password == null) return;
        string phone     = ApiHelper.Param(Request, "phone", "");

        email = email.Trim().ToLowerInvariant();
        if (!email.Contains("@") || !email.Contains("."))
        {
            ApiHelper.Error(Response, "Invalid email address.", "VALIDATION_ERROR"); return;
        }
        if (password.Length < 6)
        {
            ApiHelper.Error(Response, "Password must be at least 6 characters.", "VALIDATION_ERROR"); return;
        }

        if (ApplicantEmailExists(email))
        {
            ApiHelper.Error(Response, "An account with this email already exists.", "EMAIL_EXISTS"); return;
        }

        string fullName = (firstName.Trim() + " " + lastName.Trim()).Trim();
        string salt     = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        string hashed   = HashPassword(password, salt);

        int appId = GetPortalAppId();

        // Insert user row
        long userId = PortalExecuteInsert(
            @"INSERT INTO my_aspnet_users (applicationId, name, isAnonymous, lastActivityDate, user_type, user_full_name, user_mobile)
              VALUES (@appId, @name, 0, @now, 'APPLICANT', @fullName, @phone)",
            new MySqlParameter("@appId",    appId),
            new MySqlParameter("@name",     email),
            new MySqlParameter("@now",      DateTime.UtcNow),
            new MySqlParameter("@fullName", fullName),
            new MySqlParameter("@phone",    phone));

        if (userId <= 0)
        {
            ApiHelper.Error(Response, "Account creation failed. Please try again.", "SERVER_ERROR"); return;
        }

        // Insert membership row — lastLockoutDate omitted intentionally (NULL default for new accounts)
        PortalExecute(
            @"INSERT INTO my_aspnet_membership
                (userId, password, passwordFormat, passwordKey, email, isApproved, isLockedOut,
                 creationDate, lastLoginDate, lastPasswordChangedDate,
                 failedPasswordAttemptCount, failedPasswordAttemptWindowStart)
              VALUES (@uid, @pwd, 1, @salt, @email, 1, 0, @now, @now, @now, 0, @now)",
            new MySqlParameter("@uid",   userId),
            new MySqlParameter("@pwd",   hashed),
            new MySqlParameter("@salt",  salt),
            new MySqlParameter("@email", email),
            new MySqlParameter("@now",   DateTime.UtcNow));

        // Generate 6-digit OTP and send verification email
        string otp = GenerateOtp();
        PortalExecute(
            "INSERT INTO apply_email_tokens (user_id, token, token_type, expires_at, used, created_at) VALUES (@uid, @t, 'EMAIL_VERIFY_OTP', @exp, 0, @now)",
            new MySqlParameter("@uid", userId),
            new MySqlParameter("@t",   otp),
            new MySqlParameter("@exp", DateTime.UtcNow.AddMinutes(30)),
            new MySqlParameter("@now", DateTime.UtcNow));

        SendApplyEmail(email, "MRU — Verify Your Email",
            "Dear " + HttpUtility.HtmlEncode(firstName) + ",<br><br>" +
            "Thank you for creating a Muteesa I Royal University applicant account.<br>" +
            "Your email verification code is: <b style='font-size:22px;letter-spacing:4px'>" + otp + "</b><br>" +
            "This code expires in 30 minutes.<br><br>Regards,<br>MRU Admissions Office");

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "user_id",    userId },
            { "email",      email },
            { "full_name",  fullName },
            { "otp_sent",   true },
            { "verified",   false }
        }, "Account created. Check your email for the verification code.");
    }

    private void HandleLogin()
    {
        string email    = ApiHelper.RequireParam(Request, Response, "email");    if (email == null) return;
        string password = ApiHelper.RequireParam(Request, Response, "password"); if (password == null) return;

        email = email.Trim().ToLowerInvariant();

        string lockMsg;
        if (IsApplicantLocked(email, out lockMsg))
        {
            ApiHelper.Error(Response, lockMsg, "ACCOUNT_LOCKED"); return;
        }

        DataTable dt = PortalQuery(
            @"SELECT u.id, u.name, IFNULL(u.verified_email,'') AS verified_email,
                     IFNULL(u.user_full_name,'') AS full_name, IFNULL(u.user_mobile,'') AS phone,
                     IFNULL(m.password,'') AS pwd_hash, IFNULL(m.passwordFormat,1) AS pwd_fmt,
                     IFNULL(m.passwordKey,'') AS pwd_key
              FROM my_aspnet_users u
              INNER JOIN my_aspnet_membership m ON m.userId = u.id
              WHERE u.user_type = 'APPLICANT'
                AND (LOWER(TRIM(u.name)) = @em OR LOWER(TRIM(IFNULL(u.verified_email,''))) = @em)
              ORDER BY u.id DESC LIMIT 1",
            new MySqlParameter("@em", email));

        if (dt.Rows.Count == 0)
        {
            ApiHelper.Error(Response, "Invalid email or password.", "INVALID_CREDENTIALS"); return;
        }

        DataRow r     = dt.Rows[0];
        string stored = r["pwd_hash"].ToString();
        string key    = r["pwd_key"].ToString();
        int    userId = Convert.ToInt32(r["id"]);

        bool ok = string.IsNullOrEmpty(key)
            ? string.Equals(stored, password)
            : string.Equals(stored, HashPassword(password, key));

        if (!ok)
        {
            IncrementFailedAttempts(userId);
            ApiHelper.Error(Response, "Invalid email or password.", "INVALID_CREDENTIALS"); return;
        }

        ResetFailedAttempts(userId);

        bool isVerified = !string.IsNullOrEmpty(r["verified_email"].ToString());
        string fullName = r["full_name"].ToString();

        TokenInfo token = TokenManager.CreateToken(userId.ToString(), "applicant", fullName, Request.UserHostAddress);

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "token",        token.Token },
            { "user_id",      userId },
            { "email",        email },
            { "full_name",    fullName },
            { "phone",        r["phone"].ToString() },
            { "is_verified",  isVerified },
            { "expires_at",   token.ExpiresAt.ToString("o") }
        }, "Login successful");
    }

    private void HandleVerifyEmail()
    {
        string email = ApiHelper.RequireParam(Request, Response, "email"); if (email == null) return;
        string otp   = ApiHelper.RequireParam(Request, Response, "otp");   if (otp == null) return;

        email = email.Trim().ToLowerInvariant();
        otp   = otp.Trim();

        DataTable userDt = PortalQuery(
            @"SELECT id, IFNULL(user_full_name,'') AS full_name, IFNULL(user_mobile,'') AS phone
              FROM my_aspnet_users
              WHERE user_type='APPLICANT'
                AND (LOWER(TRIM(name))=@em OR LOWER(TRIM(IFNULL(verified_email,'')))=@em) LIMIT 1",
            new MySqlParameter("@em", email));

        if (userDt.Rows.Count == 0) { ApiHelper.Error(Response, "Account not found.", "NOT_FOUND"); return; }

        int    userId   = Convert.ToInt32(userDt.Rows[0]["id"]);
        string fullName = userDt.Rows[0]["full_name"].ToString();
        string phone    = userDt.Rows[0]["phone"].ToString();

        DataTable tkDt = PortalQuery(
            "SELECT id FROM apply_email_tokens WHERE user_id=@uid AND token=@t AND token_type='EMAIL_VERIFY_OTP' AND used=0 AND expires_at >= @now LIMIT 1",
            new MySqlParameter("@uid", userId),
            new MySqlParameter("@t",   otp),
            new MySqlParameter("@now", DateTime.UtcNow));

        if (tkDt.Rows.Count == 0) { ApiHelper.Error(Response, "Invalid or expired verification code.", "INVALID_OTP"); return; }

        PortalExecute("UPDATE apply_email_tokens SET used=1 WHERE user_id=@uid AND token_type='EMAIL_VERIFY_OTP'",
            new MySqlParameter("@uid", userId));
        PortalExecute("UPDATE my_aspnet_users SET verified_email=name WHERE id=@uid",
            new MySqlParameter("@uid", userId));

        TokenInfo token = TokenManager.CreateToken(userId.ToString(), "applicant", fullName, Request.UserHostAddress);

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "verified",   true },
            { "token",      token.Token },
            { "user_id",    userId },
            { "email",      email },
            { "full_name",  fullName },
            { "phone",      phone },
            { "expires_at", token.ExpiresAt.ToString("o") }
        }, "Email verified successfully.");
    }

    private void HandleResendOtp()
    {
        string email = ApiHelper.RequireParam(Request, Response, "email"); if (email == null) return;
        email = email.Trim().ToLowerInvariant();

        DataTable dt = PortalQuery(
            "SELECT id, IFNULL(user_full_name,'') AS full_name FROM my_aspnet_users WHERE user_type='APPLICANT' AND LOWER(TRIM(name))=@em LIMIT 1",
            new MySqlParameter("@em", email));

        if (dt.Rows.Count == 0) { ApiHelper.Error(Response, "Account not found.", "NOT_FOUND"); return; }

        int userId = Convert.ToInt32(dt.Rows[0]["id"]);
        string firstName = dt.Rows[0]["full_name"].ToString().Split(' ')[0];

        // Invalidate previous OTPs
        PortalExecute("UPDATE apply_email_tokens SET used=1 WHERE user_id=@uid AND token_type='EMAIL_VERIFY_OTP' AND used=0",
            new MySqlParameter("@uid", userId));

        string otp = GenerateOtp();
        PortalExecute(
            "INSERT INTO apply_email_tokens (user_id, token, token_type, expires_at, used, created_at) VALUES (@uid, @t, 'EMAIL_VERIFY_OTP', @exp, 0, @now)",
            new MySqlParameter("@uid", userId),
            new MySqlParameter("@t",   otp),
            new MySqlParameter("@exp", DateTime.UtcNow.AddMinutes(30)),
            new MySqlParameter("@now", DateTime.UtcNow));

        SendApplyEmail(email, "MRU — Email Verification Code",
            "Dear " + HttpUtility.HtmlEncode(firstName) + ",<br><br>" +
            "Your new verification code is: <b style='font-size:22px;letter-spacing:4px'>" + otp + "</b><br>" +
            "This code expires in 30 minutes.<br><br>Regards,<br>MRU Admissions Office");

        ApiHelper.Success(Response, new Dictionary<string, object> { { "otp_sent", true } }, "Verification code sent.");
    }

    private void HandleForgotPassword()
    {
        string email = ApiHelper.RequireParam(Request, Response, "email"); if (email == null) return;
        email = email.Trim().ToLowerInvariant();

        DataTable dt = PortalQuery(
            "SELECT id, IFNULL(user_full_name,'') AS full_name FROM my_aspnet_users WHERE user_type='APPLICANT' AND (LOWER(TRIM(name))=@em OR LOWER(TRIM(IFNULL(verified_email,'')))=@em) LIMIT 1",
            new MySqlParameter("@em", email));

        // Always return success to prevent email enumeration
        if (dt.Rows.Count > 0)
        {
            int userId = Convert.ToInt32(dt.Rows[0]["id"]);
            string firstName = dt.Rows[0]["full_name"].ToString().Split(' ')[0];

            PortalExecute("UPDATE apply_email_tokens SET used=1 WHERE user_id=@uid AND token_type='PASSWORD_RESET_OTP' AND used=0",
                new MySqlParameter("@uid", userId));

            string otp = GenerateOtp();
            PortalExecute(
                "INSERT INTO apply_email_tokens (user_id, token, token_type, expires_at, used, created_at) VALUES (@uid, @t, 'PASSWORD_RESET_OTP', @exp, 0, @now)",
                new MySqlParameter("@uid", userId),
                new MySqlParameter("@t",   otp),
                new MySqlParameter("@exp", DateTime.UtcNow.AddMinutes(30)),
                new MySqlParameter("@now", DateTime.UtcNow));

            SendApplyEmail(email, "MRU — Password Reset Code",
                "Dear " + HttpUtility.HtmlEncode(firstName) + ",<br><br>" +
                "Your password reset code is: <b style='font-size:22px;letter-spacing:4px'>" + otp + "</b><br>" +
                "This code expires in 30 minutes. If you did not request this, ignore this email.<br><br>" +
                "Regards,<br>MRU Admissions Office");
        }

        ApiHelper.Success(Response, new Dictionary<string, object> { { "sent", true } },
            "If an account with that email exists, a reset code has been sent.");
    }

    private void HandleResetPassword()
    {
        string email       = ApiHelper.RequireParam(Request, Response, "email");        if (email == null) return;
        string otp         = ApiHelper.RequireParam(Request, Response, "otp");          if (otp == null) return;
        string newPassword = ApiHelper.RequireParam(Request, Response, "new_password"); if (newPassword == null) return;

        email = email.Trim().ToLowerInvariant();

        if (newPassword.Length < 6) { ApiHelper.Error(Response, "Password must be at least 6 characters.", "VALIDATION_ERROR"); return; }

        DataTable userDt = PortalQuery(
            "SELECT id FROM my_aspnet_users WHERE user_type='APPLICANT' AND (LOWER(TRIM(name))=@em OR LOWER(TRIM(IFNULL(verified_email,'')))=@em) LIMIT 1",
            new MySqlParameter("@em", email));

        if (userDt.Rows.Count == 0) { ApiHelper.Error(Response, "Account not found.", "NOT_FOUND"); return; }
        int userId = Convert.ToInt32(userDt.Rows[0]["id"]);

        DataTable tkDt = PortalQuery(
            "SELECT id FROM apply_email_tokens WHERE user_id=@uid AND token=@t AND token_type='PASSWORD_RESET_OTP' AND used=0 AND expires_at >= @now LIMIT 1",
            new MySqlParameter("@uid", userId),
            new MySqlParameter("@t",   otp.Trim()),
            new MySqlParameter("@now", DateTime.UtcNow));

        if (tkDt.Rows.Count == 0) { ApiHelper.Error(Response, "Invalid or expired reset code.", "INVALID_OTP"); return; }

        string newSalt   = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        string newHashed = HashPassword(newPassword, newSalt);

        PortalExecute("UPDATE apply_email_tokens SET used=1 WHERE user_id=@uid AND token_type='PASSWORD_RESET_OTP'",
            new MySqlParameter("@uid", userId));
        PortalExecute("UPDATE my_aspnet_membership SET password=@pwd, passwordFormat=1, passwordKey=@salt, lastPasswordChangedDate=@now, failedPasswordAttemptCount=0, isLockedOut=0 WHERE userId=@uid",
            new MySqlParameter("@pwd",  newHashed),
            new MySqlParameter("@salt", newSalt),
            new MySqlParameter("@now",  DateTime.UtcNow),
            new MySqlParameter("@uid",  userId));

        ApiHelper.Success(Response, new Dictionary<string, object> { { "reset", true } }, "Password reset successfully.");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PROFILE (applicant token required)
    // ═══════════════════════════════════════════════════════════════════

    private void HandleMyProfile()
    {
        int userId = RequireApplicantAuth(); if (userId == 0) return;

        DataTable dt = PortalQuery(
            "SELECT name AS email, IFNULL(verified_email,'') AS verified_email, IFNULL(user_full_name,'') AS full_name, IFNULL(user_mobile,'') AS phone FROM my_aspnet_users WHERE id=@uid LIMIT 1",
            new MySqlParameter("@uid", userId));

        if (dt.Rows.Count == 0) { ApiHelper.Error(Response, "Profile not found.", "NOT_FOUND"); return; }

        var profile = ApiHelper.FirstRowToDict(dt);
        bool isVerified = !string.IsNullOrEmpty(profile["verified_email"] == null ? null : profile["verified_email"].ToString());
        profile["is_verified"] = isVerified;

        // Application summary
        DataTable appDt = ApiHelper.Query(
            @"SELECT a.stud_entry_no, a.app_status,
                     IFNULL(c.prog_id,'') AS prog_id, c.adm_status,
                     DATE_FORMAT(a.app_submitted_at, '%Y-%m-%d') AS submitted_at
              FROM acad_applications a
              LEFT JOIN acad_applicant_choices c ON c.stud_entry_no = a.stud_entry_no AND c.Choice = 1
              WHERE a.applicant_user_id = @uid
              ORDER BY a.stud_entry_no DESC LIMIT 1",
            new MySqlParameter("@uid", userId));

        if (appDt.Rows.Count > 0)
        {
            profile["application"] = ApiHelper.FirstRowToDict(appDt);
        }
        else
        {
            profile["application"] = null;
        }

        ApiHelper.Success(Response, profile);
    }

    private void HandleChangePassword()
    {
        int userId = RequireApplicantAuth(); if (userId == 0) return;

        string current = ApiHelper.RequireParam(Request, Response, "current_password"); if (current == null) return;
        string newPwd  = ApiHelper.RequireParam(Request, Response, "new_password");     if (newPwd == null) return;

        if (newPwd.Length < 6) { ApiHelper.Error(Response, "New password must be at least 6 characters.", "VALIDATION_ERROR"); return; }

        DataTable dt = PortalQuery(
            "SELECT IFNULL(password,'') AS pwd, IFNULL(passwordKey,'') AS salt FROM my_aspnet_membership WHERE userId=@uid LIMIT 1",
            new MySqlParameter("@uid", userId));

        if (dt.Rows.Count == 0) { ApiHelper.Error(Response, "Account not found.", "NOT_FOUND"); return; }

        string stored = dt.Rows[0]["pwd"].ToString();
        string salt   = dt.Rows[0]["salt"].ToString();
        bool ok = string.IsNullOrEmpty(salt)
            ? string.Equals(stored, current)
            : string.Equals(stored, HashPassword(current, salt));

        if (!ok) { ApiHelper.Error(Response, "Current password is incorrect.", "INVALID_CREDENTIALS"); return; }

        string newSalt   = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        string newHashed = HashPassword(newPwd, newSalt);
        PortalExecute("UPDATE my_aspnet_membership SET password=@pwd, passwordKey=@salt, lastPasswordChangedDate=@now WHERE userId=@uid",
            new MySqlParameter("@pwd",  newHashed),
            new MySqlParameter("@salt", newSalt),
            new MySqlParameter("@now",  DateTime.UtcNow),
            new MySqlParameter("@uid",  userId));

        ApiHelper.Success(Response, new Dictionary<string, object> { { "changed", true } }, "Password changed successfully.");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  APPLICATION WIZARD
    // ═══════════════════════════════════════════════════════════════════

    private void HandleGetDraft()
    {
        int userId = RequireApplicantAuth(); if (userId == 0) return;

        DataTable dt = ApiHelper.Query(
            @"SELECT a.stud_entry_no, IFNULL(a.stud_name,'') AS stud_name,
                     IFNULL(a.stud_surname,'') AS surname, IFNULL(a.stud_other_names,'') AS other_names,
                     IFNULL(a.stud_sex,'') AS gender, IFNULL(a.stud_nationality,'') AS nationality,
                     IFNULL(a.stud_religion,'') AS religion,
                     DATE_FORMAT(a.stud_birthdate,'%Y-%m-%d') AS dob,
                     IFNULL(a.stud_phone,'') AS phone, IFNULL(a.stud_phy_address,'') AS address,
                     IFNULL(a.stud_mar_stat,'') AS marital, IFNULL(a.physicalDisability,'') AS disability,
                     IFNULL(a.stud_id_number,'') AS national_id,
                     IFNULL(a.olevel_school,'') AS olevel_school, IFNULL(a.olevel_index,'') AS olevel_index,
                     IFNULL(a.stud_pob,'') AS olevel_year, IFNULL(a.stud_district,'') AS olevel_agg,
                     IFNULL(a.alevel_school,'') AS alevel_school, IFNULL(a.alevel_index,'') AS alevel_index,
                     IFNULL(a.alevel_year,0) AS alevel_year, IFNULL(a.stud_ward,'') AS alevel_points,
                     IFNULL(a.stud_prevcampus,'') AS other_inst, IFNULL(a.stud_lg,'') AS other_qual,
                     IFNULL(a.stud_village,'') AS other_year, IFNULL(a.stud_county,'') AS other_grade,
                     IFNULL(a.stud_campus,'') AS campus, IFNULL(a.stud_intake,'') AS intake,
                     IFNULL(a.stud_sponsor,'') AS sponsor,
                     IFNULL(a.next_kin,'') AS emergency_name, IFNULL(a.kin_relationship,'') AS emergency_rel,
                     IFNULL(a.kin_contacts,'') AS emergency_phone,
                     IFNULL(a.app_status,'DRAFT') AS app_status,
                     DATE_FORMAT(a.app_submitted_at,'%Y-%m-%d %H:%i') AS submitted_at,
                     IFNULL(c.prog_id,'') AS programme, IFNULL(c.adm_session,'') AS session,
                     c.adm_status AS choice_status
              FROM acad_applications a
              LEFT JOIN acad_applicant_choices c ON c.stud_entry_no = a.stud_entry_no AND c.Choice = 1
              WHERE a.applicant_user_id = @uid
              ORDER BY a.stud_entry_no DESC LIMIT 1",
            new MySqlParameter("@uid", userId));

        if (dt.Rows.Count == 0)
        {
            ApiHelper.Success(Response, null, "No application started yet.");
            return;
        }

        var draft = ApiHelper.FirstRowToDict(dt);

        // Attach documents list
        string entryNo = draft["stud_entry_no"] == null ? null : draft["stud_entry_no"].ToString();
        if (!string.IsNullOrEmpty(entryNo))
        {
            DataTable docs = ApiHelper.Query(
                "SELECT id, doc_type, original_filename, file_size_bytes, DATE_FORMAT(uploaded_at,'%Y-%m-%d %H:%i') AS uploaded_at FROM apply_documents WHERE stud_entry_no=@eno ORDER BY uploaded_at",
                new MySqlParameter("@eno", entryNo));
            draft["documents"] = ApiHelper.TableToList(docs);
        }

        ApiHelper.Success(Response, draft);
    }

    private void HandleSaveStep1()
    {
        int userId = RequireApplicantAuth(); if (userId == 0) return;

        string surname    = ApiHelper.RequireParam(Request, Response, "surname");     if (surname == null) return;
        string otherNames = ApiHelper.RequireParam(Request, Response, "other_names"); if (otherNames == null) return;
        string gender     = ApiHelper.RequireParam(Request, Response, "gender");      if (gender == null) return;
        string nationality= ApiHelper.RequireParam(Request, Response, "nationality"); if (nationality == null) return;
        string phone      = ApiHelper.RequireParam(Request, Response, "phone");       if (phone == null) return;
        string dob        = ApiHelper.RequireParam(Request, Response, "dob");         if (dob == null) return;

        gender = gender.Trim().ToUpper();
        if (gender != "M" && gender != "F") { ApiHelper.Error(Response, "gender must be M or F.", "VALIDATION_ERROR"); return; }

        DateTime dobParsed;
        if (!DateTime.TryParse(dob, out dobParsed)) { ApiHelper.Error(Response, "dob must be a valid date (YYYY-MM-DD).", "VALIDATION_ERROR"); return; }

        string email = GetApplicantEmail(userId);
        string entryNo = EnsureDraftEntry(userId, email);

        if (!IsEditable(entryNo)) { ApiHelper.Error(Response, "Application has been submitted and cannot be edited.", "LOCKED"); return; }

        string fullName = (surname.Trim() + " " + otherNames.Trim()).Trim();

        ApiHelper.Execute(
            @"UPDATE acad_applications
              SET stud_name=@name, stud_surname=@surname, stud_other_names=@other,
                  stud_sex=@sex, stud_nationality=@nat, stud_religion=@rel,
                  stud_birthdate=@dob, stud_phone=@phone, stud_phy_address=@addr,
                  stud_email=@email, stud_mar_stat=@marital, physicalDisability=@dis,
                  stud_id_number=@natid, app_status='DRAFT', app_last_updated_at=@now
              WHERE stud_entry_no=@eno",
            new MySqlParameter("@name",    fullName),
            new MySqlParameter("@surname", surname.Trim()),
            new MySqlParameter("@other",   otherNames.Trim()),
            new MySqlParameter("@sex",     gender),
            new MySqlParameter("@nat",     nationality.Trim()),
            new MySqlParameter("@rel",     ApiHelper.Param(Request, "religion", "")),
            new MySqlParameter("@dob",     dobParsed.Date),
            new MySqlParameter("@phone",   phone.Trim()),
            new MySqlParameter("@addr",    ApiHelper.Param(Request, "address", "")),
            new MySqlParameter("@email",   email),
            new MySqlParameter("@marital", ApiHelper.Param(Request, "marital", "")),
            new MySqlParameter("@dis",     ApiHelper.Param(Request, "disability", "")),
            new MySqlParameter("@natid",   ApiHelper.Param(Request, "national_id", "")),
            new MySqlParameter("@now",     DateTime.UtcNow),
            new MySqlParameter("@eno",     entryNo));

        ApiHelper.Success(Response, new Dictionary<string, object> { { "entry_no", entryNo }, { "step", 1 } }, "Step 1 saved.");
    }

    private void HandleSaveStep2()
    {
        int userId = RequireApplicantAuth(); if (userId == 0) return;

        string olevelSchool = ApiHelper.RequireParam(Request, Response, "olevel_school"); if (olevelSchool == null) return;
        string olevelIndex  = ApiHelper.RequireParam(Request, Response, "olevel_index");  if (olevelIndex == null) return;
        string olevelYear   = ApiHelper.RequireParam(Request, Response, "olevel_year");   if (olevelYear == null) return;

        int oyr;
        if (!int.TryParse(olevelYear.Trim(), out oyr) || oyr < 1960 || oyr > DateTime.Now.Year)
        {
            ApiHelper.Error(Response, "Enter a valid O-Level year.", "VALIDATION_ERROR"); return;
        }

        string email   = GetApplicantEmail(userId);
        string entryNo = EnsureDraftEntry(userId, email);
        if (!IsEditable(entryNo)) { ApiHelper.Error(Response, "Application has been submitted and cannot be edited.", "LOCKED"); return; }

        ApiHelper.Execute(
            @"UPDATE acad_applications
              SET olevel_school=@os, olevel_index=@oi, stud_pob=@oy, stud_district=@oa,
                  alevel_school=@as_, alevel_index=@ai, alevel_year=@ay, stud_ward=@ap,
                  stud_prevcampus=@oi2, stud_lg=@oq, stud_village=@oyr2, stud_county=@og,
                  app_status='DRAFT', app_last_updated_at=@now
              WHERE stud_entry_no=@eno",
            new MySqlParameter("@os",   olevelSchool.Trim()),
            new MySqlParameter("@oi",   olevelIndex.Trim()),
            new MySqlParameter("@oy",   oyr.ToString()),
            new MySqlParameter("@oa",   ApiHelper.Param(Request, "olevel_agg", "")),
            new MySqlParameter("@as_",  ApiHelper.Param(Request, "alevel_school", "")),
            new MySqlParameter("@ai",   ApiHelper.Param(Request, "alevel_index", "")),
            new MySqlParameter("@ay",   ApiHelper.ParamInt(Request, "alevel_year", 0)),
            new MySqlParameter("@ap",   ApiHelper.Param(Request, "alevel_points", "")),
            new MySqlParameter("@oi2",  ApiHelper.Param(Request, "other_inst", "")),
            new MySqlParameter("@oq",   ApiHelper.Param(Request, "other_qual", "")),
            new MySqlParameter("@oyr2", ApiHelper.Param(Request, "other_year", "")),
            new MySqlParameter("@og",   ApiHelper.Param(Request, "other_grade", "")),
            new MySqlParameter("@now",  DateTime.UtcNow),
            new MySqlParameter("@eno",  entryNo));

        ApiHelper.Success(Response, new Dictionary<string, object> { { "entry_no", entryNo }, { "step", 2 } }, "Step 2 saved.");
    }

    private void HandleSaveStep3()
    {
        int userId = RequireApplicantAuth(); if (userId == 0) return;

        string programme = ApiHelper.RequireParam(Request, Response, "programme"); if (programme == null) return;
        string session   = ApiHelper.RequireParam(Request, Response, "session");   if (session == null) return;
        string campus    = ApiHelper.RequireParam(Request, Response, "campus");    if (campus == null) return;
        string intake    = ApiHelper.RequireParam(Request, Response, "intake");    if (intake == null) return;
        string kinName   = ApiHelper.RequireParam(Request, Response, "emergency_name"); if (kinName == null) return;

        string email   = GetApplicantEmail(userId);
        string entryNo = EnsureDraftEntry(userId, email);
        if (!IsEditable(entryNo)) { ApiHelper.Error(Response, "Application has been submitted and cannot be edited.", "LOCKED"); return; }

        string sponsor = ApiHelper.Param(Request, "sponsor", "");

        ApiHelper.Execute(
            @"UPDATE acad_applications
              SET stud_campus=@camp, stud_intake=@int, stud_sponsor=@spon,
                  next_kin=@kin, kin_relationship=@kinrel, kin_contacts=@kinph,
                  app_status='DRAFT', app_last_updated_at=@now
              WHERE stud_entry_no=@eno",
            new MySqlParameter("@camp",  campus.Trim()),
            new MySqlParameter("@int",   intake.Trim()),
            new MySqlParameter("@spon",  sponsor),
            new MySqlParameter("@kin",   kinName.Trim()),
            new MySqlParameter("@kinrel",ApiHelper.Param(Request, "emergency_rel", "")),
            new MySqlParameter("@kinph", ApiHelper.Param(Request, "emergency_phone", "")),
            new MySqlParameter("@now",   DateTime.UtcNow),
            new MySqlParameter("@eno",   entryNo));

        // Upsert the programme choice
        int existingChoice = Convert.ToInt32(ApiHelper.Scalar(
            "SELECT COUNT(*) FROM acad_applicant_choices WHERE stud_entry_no=@eno AND Choice=1",
            new MySqlParameter("@eno", entryNo)));

        if (existingChoice > 0)
        {
            ApiHelper.Execute(
                "UPDATE acad_applicant_choices SET prog_id=@prog, adm_session=@sess WHERE stud_entry_no=@eno AND Choice=1",
                new MySqlParameter("@prog", programme.Trim()),
                new MySqlParameter("@sess", session.Trim()),
                new MySqlParameter("@eno",  entryNo));
        }
        else
        {
            ApiHelper.Execute(
                "INSERT INTO acad_applicant_choices (stud_entry_no, Choice, prog_id, adm_session, adm_status) VALUES (@eno, 1, @prog, @sess, 0)",
                new MySqlParameter("@eno",  entryNo),
                new MySqlParameter("@prog", programme.Trim()),
                new MySqlParameter("@sess", session.Trim()));
        }

        ApiHelper.Success(Response, new Dictionary<string, object> { { "entry_no", entryNo }, { "step", 3 } }, "Step 3 saved.");
    }

    private void HandleSubmit()
    {
        int userId = RequireApplicantAuth(); if (userId == 0) return;

        string declared = ApiHelper.Param(Request, "declaration_accepted", "0");
        if (declared != "1" && declared.ToLower() != "true")
        {
            ApiHelper.Error(Response, "You must accept the declaration before submitting.", "VALIDATION_ERROR"); return;
        }

        string email   = GetApplicantEmail(userId);
        string entryNo = GetEditableEntryNo(userId);
        if (string.IsNullOrEmpty(entryNo))
        {
            ApiHelper.Error(Response, "No draft application found. Save at least Step 1 before submitting.", "NOT_FOUND"); return;
        }

        // Validate required fields before submission
        DataTable appDt = ApiHelper.Query(
            @"SELECT stud_name, stud_sex, stud_nationality, stud_birthdate, stud_phone,
                     olevel_school, olevel_index, next_kin, app_status
              FROM acad_applications WHERE stud_entry_no=@eno LIMIT 1",
            new MySqlParameter("@eno", entryNo));

        if (appDt.Rows.Count == 0) { ApiHelper.Error(Response, "Application not found.", "NOT_FOUND"); return; }

        DataRow a = appDt.Rows[0];
        if (a["app_status"].ToString() == "SUBMITTED" || a["app_status"].ToString() == "UNDER_REVIEW" || a["app_status"].ToString() == "ADMITTED")
        {
            ApiHelper.Error(Response, "Application already submitted.", "ALREADY_SUBMITTED"); return;
        }

        var missing = new List<string>();
        if (string.IsNullOrEmpty(a["stud_name"].ToString()))     missing.Add("Full name (Step 1)");
        if (string.IsNullOrEmpty(a["stud_sex"].ToString()))      missing.Add("Gender (Step 1)");
        if (string.IsNullOrEmpty(a["stud_nationality"].ToString())) missing.Add("Nationality (Step 1)");
        if (a["stud_birthdate"] == DBNull.Value)                 missing.Add("Date of birth (Step 1)");
        if (string.IsNullOrEmpty(a["stud_phone"].ToString()))    missing.Add("Phone number (Step 1)");
        if (string.IsNullOrEmpty(a["olevel_school"].ToString())) missing.Add("O-Level school (Step 2)");
        if (string.IsNullOrEmpty(a["olevel_index"].ToString()))  missing.Add("O-Level index (Step 2)");
        if (string.IsNullOrEmpty(a["next_kin"].ToString()))      missing.Add("Emergency contact (Step 3)");

        int choiceCount = Convert.ToInt32(ApiHelper.Scalar(
            "SELECT COUNT(*) FROM acad_applicant_choices WHERE stud_entry_no=@eno AND Choice=1 AND prog_id IS NOT NULL AND prog_id<>''",
            new MySqlParameter("@eno", entryNo)));
        if (choiceCount == 0) missing.Add("Programme selection (Step 3)");

        if (missing.Count > 0)
        {
            ApiHelper.Error(Response, "Please complete all required fields before submitting. Missing: " + string.Join(", ", missing.ToArray()), "INCOMPLETE"); return;
        }

        ApiHelper.Execute(
            @"UPDATE acad_applications SET app_status='SUBMITTED',
              app_last_updated_at=@now, app_submitted_at=IFNULL(app_submitted_at,@now)
              WHERE stud_entry_no=@eno",
            new MySqlParameter("@now", DateTime.UtcNow),
            new MySqlParameter("@eno", entryNo));

        // Notify applicant
        InsertNotification(userId, "Application " + entryNo + " submitted successfully. We will review it shortly.", "");

        // Confirmation email (non-fatal)
        string name = a["stud_name"].ToString();
        SendApplyEmail(email,
            "MRU — Application Received (Ref: " + entryNo + ")",
            "Dear " + HttpUtility.HtmlEncode(name) + ",<br><br>" +
            "Thank you for submitting your application to Muteesa I Royal University.<br>" +
            "Your application reference number is: <b>" + HttpUtility.HtmlEncode(entryNo) + "</b><br><br>" +
            "You can track your application status by logging into the MRU app at any time.<br><br>" +
            "Regards,<br>MRU Admissions Office");

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "entry_no", entryNo },
            { "status",   "SUBMITTED" }
        }, "Application submitted successfully.");
    }

    private void HandleMyApplication()
    {
        int userId = RequireApplicantAuth(); if (userId == 0) return;

        DataTable dt = ApiHelper.Query(
            @"SELECT a.stud_entry_no, a.stud_name, a.stud_email,
                     IFNULL(a.app_status,'DRAFT') AS app_status,
                     DATE_FORMAT(a.app_submitted_at,'%Y-%m-%d %H:%i') AS submitted_at,
                     DATE_FORMAT(a.app_last_updated_at,'%Y-%m-%d %H:%i') AS last_updated,
                     IFNULL(c.prog_id,'') AS programme, IFNULL(p.progname,'') AS programme_name,
                     c.adm_status AS choice_status, IFNULL(c.adm_session,'') AS session,
                     IFNULL(c.stud_reg_no,'') AS regno
              FROM acad_applications a
              LEFT JOIN acad_applicant_choices c ON c.stud_entry_no = a.stud_entry_no AND c.Choice = 1
              LEFT JOIN acad_programme p ON p.progcode = c.prog_id
              WHERE a.applicant_user_id = @uid
              ORDER BY a.stud_entry_no DESC LIMIT 1",
            new MySqlParameter("@uid", userId));

        if (dt.Rows.Count == 0) { ApiHelper.Success(Response, null, "No application found."); return; }

        var app = ApiHelper.FirstRowToDict(dt);

        // Choice status label
        object csObj = app["choice_status"];
        if (csObj != null && csObj != DBNull.Value)
        {
            int cs = Convert.ToInt32(csObj);
            string[] labels = { "PENDING", "ADMITTED", "REJECTED", "WITHDRAWN" };
            app["choice_status_label"] = cs >= 0 && cs < labels.Length ? labels[cs] : "UNKNOWN";
        }

        // Unread notification count
        int unread = Convert.ToInt32(ApiHelper.Scalar(
            "SELECT COUNT(*) FROM apply_notifications WHERE user_id=@uid AND is_read=0",
            new MySqlParameter("@uid", userId)));
        app["unread_notifications"] = unread;

        // Document count
        string entryNo = app["stud_entry_no"] == null ? null : app["stud_entry_no"].ToString();
        if (!string.IsNullOrEmpty(entryNo))
        {
            int docCount = Convert.ToInt32(ApiHelper.Scalar(
                "SELECT COUNT(*) FROM apply_documents WHERE stud_entry_no=@eno",
                new MySqlParameter("@eno", entryNo)));
            app["document_count"] = docCount;
        }

        ApiHelper.Success(Response, app);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DOCUMENTS
    // ═══════════════════════════════════════════════════════════════════

    private void HandleDocuments()
    {
        int userId = RequireApplicantAuth(); if (userId == 0) return;

        string entryNo = GetApplicantEntryNo(userId);
        if (string.IsNullOrEmpty(entryNo)) { ApiHelper.Success(Response, new List<object>(), "No application found."); return; }

        DataTable dt = ApiHelper.Query(
            "SELECT id, doc_type, original_filename, file_size_bytes, DATE_FORMAT(uploaded_at,'%Y-%m-%d %H:%i') AS uploaded_at FROM apply_documents WHERE stud_entry_no=@eno ORDER BY uploaded_at",
            new MySqlParameter("@eno", entryNo));

        string baseUrl  = Request.Url.GetLeftPart(UriPartial.Path);
        string rawToken = Request["token"] ?? "";
        var docs = ApiHelper.TableToList(dt);
        foreach (var doc in docs)
        {
            object idObj;
            if (doc.TryGetValue("id", out idObj))
                doc["url"] = baseUrl + "?action=get_document&id=" + idObj + "&token=" + Uri.EscapeDataString(rawToken);
        }
        ApiHelper.Success(Response, docs);
    }

    private void HandleUploadDocument()
    {
        int userId = RequireApplicantAuth(); if (userId == 0) return;

        string docType = ApiHelper.RequireParam(Request, Response, "doc_type"); if (docType == null) return;
        docType = docType.Trim().ToUpper();

        // Normalise common aliases from mobile clients
        var docTypeAliases = new System.Collections.Generic.Dictionary<string, string>
        {
            { "PASSPORT_PHOTO",   "PHOTO" },
            { "PROFILE_PHOTO",    "PHOTO" },
            { "PROFILE_PIC",      "PHOTO" },
            { "PICTURE",          "PHOTO" },
            { "O_LEVEL",          "OLEVEL" },
            { "O-LEVEL",          "OLEVEL" },
            { "OLEVELS",          "OLEVEL" },
            { "A_LEVEL",          "ALEVEL" },
            { "A-LEVEL",          "ALEVEL" },
            { "ALEVELS",          "ALEVEL" },
            { "NATIONAL_ID",      "NATID" },
            { "NATIONAL_IDCARD",  "NATID" },
            { "NIN",              "NATID" },
            { "ID_CARD",          "NATID" },
        };
        if (docTypeAliases.ContainsKey(docType)) docType = docTypeAliases[docType];

        string[] validTypes = {
            "PHOTO", "OLEVEL", "ALEVEL", "NATID",
            "PASSPORT", "TRANSCRIPT", "BIRTH_CERTIFICATE",
            "RECOMMENDATION", "MEDICAL", "OTHER"
        };
        bool validDocType = false;
        foreach (string vt in validTypes) if (vt == docType) { validDocType = true; break; }
        if (!validDocType) { ApiHelper.Error(Response, "doc_type must be one of: PHOTO, OLEVEL, ALEVEL, NATID, PASSPORT, TRANSCRIPT, BIRTH_CERTIFICATE, RECOMMENDATION, MEDICAL, OTHER.", "VALIDATION_ERROR"); return; }

        if (Request.Files.Count == 0) { ApiHelper.Error(Response, "No file uploaded. Send as multipart/form-data with field 'file'.", "MISSING_FILE"); return; }

        HttpPostedFile file = Request.Files[0];
        if (file == null || file.ContentLength == 0) { ApiHelper.Error(Response, "Uploaded file is empty.", "MISSING_FILE"); return; }
        if (file.ContentLength > 5 * 1024 * 1024) { ApiHelper.Error(Response, "File size must not exceed 5 MB.", "FILE_TOO_LARGE"); return; }

        string entryNo = GetApplicantEntryNo(userId);
        if (string.IsNullOrEmpty(entryNo)) { ApiHelper.Error(Response, "No application found. Complete Step 1 first.", "NOT_FOUND"); return; }

        string ext      = Path.GetExtension(file.FileName ?? "").ToLower();
        string[] allowed = { ".jpg", ".jpeg", ".png", ".pdf" };
        bool extOk = false;
        foreach (string a in allowed) if (a == ext) { extOk = true; break; }
        if (!extOk) { ApiHelper.Error(Response, "Only JPG, PNG, and PDF files are accepted.", "INVALID_FILE_TYPE"); return; }

        // Save file
        string uploadDir = GetUploadDir();
        string subDir    = Path.Combine(uploadDir, entryNo);
        if (!Directory.Exists(subDir)) Directory.CreateDirectory(subDir);

        string safeName    = docType + "_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ext;
        string storedPath  = Path.Combine(subDir, safeName);
        file.SaveAs(storedPath);

        // Upsert — each doc_type has one slot (replace if resubmitted, except OTHER which allows multiple)
        if (docType != "OTHER")
        {
            // Delete old file from disk before replacing record
            DataTable old = ApiHelper.Query(
                "SELECT stored_path FROM apply_documents WHERE stud_entry_no=@eno AND doc_type=@dt",
                new MySqlParameter("@eno", entryNo),
                new MySqlParameter("@dt",  docType));
            foreach (DataRow row in old.Rows)
            {
                try { string p = row["stored_path"].ToString(); if (File.Exists(p)) File.Delete(p); } catch { }
            }
            ApiHelper.Execute("DELETE FROM apply_documents WHERE stud_entry_no=@eno AND doc_type=@dt",
                new MySqlParameter("@eno", entryNo),
                new MySqlParameter("@dt",  docType));
        }

        long newId = ApiHelper.ExecuteInsert(
            "INSERT INTO apply_documents (stud_entry_no, doc_type, original_filename, stored_path, file_size_bytes, uploaded_at) VALUES (@eno, @dt, @fn, @sp, @sz, @now)",
            new MySqlParameter("@eno", entryNo),
            new MySqlParameter("@dt",  docType),
            new MySqlParameter("@fn",  Path.GetFileName(file.FileName)),
            new MySqlParameter("@sp",  storedPath),
            new MySqlParameter("@sz",  file.ContentLength),
            new MySqlParameter("@now", DateTime.UtcNow));

        string baseUrl  = Request.Url.GetLeftPart(UriPartial.Path);
        string rawToken = Request["token"] ?? "";
        string docUrl   = baseUrl + "?action=get_document&id=" + newId + "&token=" + Uri.EscapeDataString(rawToken);

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "id",                newId },
            { "doc_type",          docType },
            { "original_filename", Path.GetFileName(file.FileName) },
            { "file_size_bytes",   file.ContentLength },
            { "file_size_kb",      Math.Round(file.ContentLength / 1024.0, 1) },
            { "url",               docUrl },
            { "uploaded_at",       DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
        }, "Document uploaded.");
    }

    private void HandleDeleteDocument()
    {
        int userId = RequireApplicantAuth(); if (userId == 0) return;

        int docId = ApiHelper.ParamInt(Request, "id", 0);
        if (docId <= 0) { ApiHelper.Error(Response, "id is required.", "MISSING_PARAM"); return; }

        string entryNo = GetApplicantEntryNo(userId);
        if (string.IsNullOrEmpty(entryNo)) { ApiHelper.Error(Response, "Application not found.", "NOT_FOUND"); return; }

        DataTable dt = ApiHelper.Query(
            "SELECT id, stored_path FROM apply_documents WHERE id=@id AND stud_entry_no=@eno LIMIT 1",
            new MySqlParameter("@id",  docId),
            new MySqlParameter("@eno", entryNo));

        if (dt.Rows.Count == 0) { ApiHelper.Error(Response, "Document not found.", "NOT_FOUND"); return; }

        try { string p = dt.Rows[0]["stored_path"].ToString(); if (File.Exists(p)) File.Delete(p); } catch { }
        ApiHelper.Execute("DELETE FROM apply_documents WHERE id=@id", new MySqlParameter("@id", docId));

        ApiHelper.Success(Response, new Dictionary<string, object> { { "id", docId } }, "Document deleted.");
    }

    private void HandleGetDocument()
    {
        int userId = RequireApplicantAuth(); if (userId == 0) return;

        int docId = ApiHelper.ParamInt(Request, "id", 0);
        if (docId <= 0) { ApiHelper.Error(Response, "id is required.", "MISSING_PARAM"); return; }

        string entryNo = GetApplicantEntryNo(userId);
        if (string.IsNullOrEmpty(entryNo)) { ApiHelper.Error(Response, "Application not found.", "NOT_FOUND"); return; }

        DataTable dt = ApiHelper.Query(
            "SELECT stored_path, original_filename FROM apply_documents WHERE id=@id AND stud_entry_no=@eno LIMIT 1",
            new MySqlParameter("@id",  docId),
            new MySqlParameter("@eno", entryNo));

        if (dt.Rows.Count == 0) { ApiHelper.Error(Response, "Document not found.", "NOT_FOUND"); return; }

        string path     = dt.Rows[0]["stored_path"].ToString();
        string fileName = dt.Rows[0]["original_filename"].ToString();

        if (!File.Exists(path)) { ApiHelper.Error(Response, "File not found on server.", "NOT_FOUND"); return; }

        string ext = Path.GetExtension(path).ToLower();
        Response.Clear();
        Response.ContentType = ext == ".pdf" ? "application/pdf"
                             : ext == ".png" ? "image/png"
                             : "image/jpeg";
        Response.AddHeader("Content-Disposition", "inline; filename=\"" + fileName + "\"");
        Response.TransmitFile(path);
        ApiHelper.CompleteResponse(Response);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  NOTIFICATIONS
    // ═══════════════════════════════════════════════════════════════════

    private void HandleNotifications()
    {
        int userId = RequireApplicantAuth(); if (userId == 0) return;

        int page   = Math.Max(1, ApiHelper.ParamInt(Request, "page", 1));
        int size   = Math.Min(50, Math.Max(1, ApiHelper.ParamInt(Request, "size", 20)));
        int offset = (page - 1) * size;

        int total = Convert.ToInt32(ApiHelper.Scalar(
            "SELECT COUNT(*) FROM apply_notifications WHERE user_id=@uid",
            new MySqlParameter("@uid", userId)));

        DataTable dt = ApiHelper.Query(
            @"SELECT id, message, is_read, link, DATE_FORMAT(created_at,'%Y-%m-%d %H:%i') AS created_at
              FROM apply_notifications WHERE user_id=@uid ORDER BY created_at DESC LIMIT @lim OFFSET @off",
            new MySqlParameter("@uid", userId),
            new MySqlParameter("@lim", size),
            new MySqlParameter("@off", offset));

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "total", total }, { "page", page }, { "size", size },
            { "pages", (int)Math.Ceiling(total / (double)size) },
            { "notifications", ApiHelper.TableToList(dt) }
        });
    }

    private void HandleMarkRead()
    {
        int userId = RequireApplicantAuth(); if (userId == 0) return;

        int id = ApiHelper.ParamInt(Request, "id", 0);
        if (id > 0)
        {
            ApiHelper.Execute("UPDATE apply_notifications SET is_read=1 WHERE id=@id AND user_id=@uid",
                new MySqlParameter("@id", id), new MySqlParameter("@uid", userId));
        }
        else
        {
            // Mark all read
            ApiHelper.Execute("UPDATE apply_notifications SET is_read=1 WHERE user_id=@uid",
                new MySqlParameter("@uid", userId));
        }

        ApiHelper.Success(Response, new Dictionary<string, object> { { "marked", true } }, "Marked as read.");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PUBLIC REFERENCE DATA
    // ═══════════════════════════════════════════════════════════════════

    private void HandleProgrammes()
    {
        string faculty = ApiHelper.Param(Request, "faculty", "");
        string campus  = ApiHelper.Param(Request, "campus",  "");
        string q       = ApiHelper.Param(Request, "q",       "");

        var where = new StringBuilder("WHERE 1=1");
        var parms = new List<MySqlParameter>();

        if (!string.IsNullOrEmpty(faculty)) { where.Append(" AND p.faculty_code=@fac"); parms.Add(new MySqlParameter("@fac", faculty)); }
        if (!string.IsNullOrEmpty(q))       { where.Append(" AND (p.progname LIKE @q OR p.progcode LIKE @q)"); parms.Add(new MySqlParameter("@q", "%" + q + "%")); }

        DataTable dt = ApiHelper.Query(
            @"SELECT p.progcode, p.progname, p.faculty_code, f.faculty_name,
                     IFNULL(p.couselength, '') AS duration, IFNULL(p.study_system, '') AS award_type,
                     IFNULL(p.levelCode, '') AS level_code
              FROM acad_programme p
              LEFT JOIN acad_faculty f ON f.faculty_code = p.faculty_code
              " + where + " ORDER BY f.faculty_name, p.progname",
            parms.ToArray());

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "total", dt.Rows.Count }, { "programmes", ApiHelper.TableToList(dt) }
        });
    }

    private void HandleFaculties()
    {
        DataTable dt = ApiHelper.Query("SELECT faculty_code, faculty_name FROM acad_faculty ORDER BY faculty_name");
        ApiHelper.Success(Response, ApiHelper.TableToList(dt));
    }

    private void HandleCampuses()
    {
        DataTable dt = ApiHelper.Query("SELECT campus_code, campus_name FROM acad_campuses ORDER BY campus_name");
        if (dt.Rows.Count == 0)
        {
            // Fallback if acad_campuses doesn't exist — return hardcoded MRU campuses
            ApiHelper.Success(Response, new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { { "campus_code", "MAIN" }, { "campus_name", "Main Campus - Masaka" } }
            });
            return;
        }
        ApiHelper.Success(Response, ApiHelper.TableToList(dt));
    }

    /// <summary>
    /// The intakes an applicant may apply for. Set on eAdmin → Academic Years → Admission,
    /// which writes apply_intakes; the eportal wizard reads the same rows.
    ///
    /// This used to fall back to returning CLOSED intakes when none were open, and to seed an
    /// open one when the table was empty — between them an administrator could not actually
    /// close admissions. Now: never configured falls back, configured-and-closed means closed.
    /// </summary>
    private void HandleIntakes()
    {
        const string selectCols = @"SELECT id, intake_year, intake_label, session_type, is_open,
                     DATE_FORMAT(open_from,'%Y-%m-%d') AS open_from,
                     DATE_FORMAT(open_to,'%Y-%m-%d') AS open_to
              FROM apply_intakes";

        int configured = -1;
        try
        {
            DataTable cnt = ApiHelper.Query("SELECT COUNT(*) AS n FROM apply_intakes");
            configured = cnt.Rows.Count > 0 ? Convert.ToInt32(cnt.Rows[0]["n"]) : 0;

            DataTable dt = ApiHelper.Query(selectCols +
                @" WHERE is_open = 1
                     AND (open_from IS NULL OR NOW() >= open_from)
                     AND (open_to   IS NULL OR NOW() <= open_to)
                   ORDER BY intake_year DESC");

            if (dt.Rows.Count > 0) { ApiHelper.Success(Response, ApiHelper.TableToList(dt)); return; }

            // Configured, and nothing is open: admissions are closed. An empty list is the
            // honest answer — offering closed intakes would let an applicant start something
            // the server will refuse.
            if (configured > 0) { ApiHelper.Success(Response, new List<Dictionary<string, object>>()); return; }
        }
        catch { /* table missing — fall through and seed the historic default */ }

        // Never configured: return the historic fallback WITHOUT writing anything.
        //
        // This used to seed a row here, via an INSERT that was a MySQL syntax error
        // ("SELECT <literals> WHERE ..." needs a FROM clause), so it silently failed every
        // time and the table stayed empty. Repairing it would have been worse than leaving
        // it: the seeded window ran March-July of the current year, so the moment it
        // succeeded the next call would fall outside that window and admissions would shut
        // on their own. An unconfigured install should behave exactly as it always has
        // until someone opens a year on eAdmin > Academic Years > Admission.
        int year = DateTime.UtcNow.Year;
        string label = "August " + year + " Intake";

        ApiHelper.Success(Response, new List<Dictionary<string, object>>
        {
            new Dictionary<string, object>
            {
                { "id", 1 }, { "intake_year", year },
                { "intake_label", label },

                { "session_type", "ALL" }, { "is_open", 1 },
                { "open_from", year + "-03-01" }, { "open_to", year + "-07-31" }
            }
        });
    }

    private void HandleCheckStatus()
    {
        string entryNo = ApiHelper.Param(Request, "entry_no", "").Trim();
        string dob     = ApiHelper.Param(Request, "dob",      "").Trim();

        if (string.IsNullOrEmpty(entryNo) || string.IsNullOrEmpty(dob))
        {
            ApiHelper.Error(Response, "entry_no and dob (YYYY-MM-DD) are required.", "MISSING_PARAM"); return;
        }

        DataTable dt = ApiHelper.Query(
            @"SELECT a.stud_entry_no, a.stud_name, IFNULL(a.app_status,'DRAFT') AS app_status,
                     DATE_FORMAT(a.app_submitted_at,'%Y-%m-%d') AS submitted_at,
                     IFNULL(c.prog_id,'') AS programme, IFNULL(p.progname,'') AS programme_name,
                     c.adm_status AS choice_status, IFNULL(c.stud_reg_no,'') AS regno
              FROM acad_applications a
              LEFT JOIN acad_applicant_choices c ON c.stud_entry_no = a.stud_entry_no AND c.Choice = 1
              LEFT JOIN acad_programme p ON p.progcode = c.prog_id
              WHERE TRIM(a.stud_entry_no) = @eno AND DATE(a.stud_birthdate) = @dob LIMIT 1",
            new MySqlParameter("@eno", entryNo),
            new MySqlParameter("@dob", dob));

        if (dt.Rows.Count == 0) { ApiHelper.Error(Response, "Application not found. Check your reference number and date of birth.", "NOT_FOUND"); return; }

        var row = ApiHelper.FirstRowToDict(dt);
        object csObj = row["choice_status"];
        if (csObj != null && csObj != DBNull.Value)
        {
            int cs = Convert.ToInt32(csObj);
            string[] labels = { "PENDING", "ADMITTED", "REJECTED", "WITHDRAWN" };
            row["choice_status_label"] = cs >= 0 && cs < labels.Length ? labels[cs] : "UNKNOWN";
            if (cs != 1) row.Remove("regno"); // hide regno unless admitted
        }

        ApiHelper.Success(Response, row);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPER METHODS — Auth
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Validates applicant token. Returns userId (int) or sends 401 and returns 0.</summary>
    private int RequireApplicantAuth()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return 0;
        if (auth.UserType != "applicant")
        {
            ApiHelper.Error(Response, "Applicant access required.", "FORBIDDEN");
            return 0;
        }
        int uid;
        return int.TryParse(auth.UserId, out uid) ? uid : 0;
    }

    private bool ApplicantEmailExists(string email)
    {
        try
        {
            object v = PortalScalar(
                "SELECT COUNT(*) FROM my_aspnet_users WHERE user_type='APPLICANT' AND (LOWER(TRIM(name))=@em OR LOWER(TRIM(IFNULL(verified_email,'')))=@em)",
                new MySqlParameter("@em", email));
            return v != null && v != DBNull.Value && Convert.ToInt32(v) > 0;
        }
        catch { return false; }
    }

    private bool IsApplicantLocked(string email, out string message)
    {
        message = string.Empty;
        const int lockMinutes = 15;
        try
        {
            DataTable dt = PortalQuery(
                @"SELECT u.id, m.isLockedOut, m.lastLockoutDate
                  FROM my_aspnet_users u INNER JOIN my_aspnet_membership m ON m.userId=u.id
                  WHERE u.user_type='APPLICANT'
                    AND (LOWER(TRIM(u.name))=@em OR LOWER(TRIM(IFNULL(u.verified_email,'')))=@em) LIMIT 1",
                new MySqlParameter("@em", email));
            if (dt.Rows.Count == 0) return false;

            bool locked = dt.Rows[0]["isLockedOut"] != DBNull.Value && Convert.ToInt32(dt.Rows[0]["isLockedOut"]) != 0;
            if (!locked) return false;

            DateTime lockDate = dt.Rows[0]["lastLockoutDate"] == DBNull.Value
                ? DateTime.MinValue : Convert.ToDateTime(dt.Rows[0]["lastLockoutDate"]);
            int uid = Convert.ToInt32(dt.Rows[0]["id"]);

            if ((DateTime.UtcNow - lockDate).TotalMinutes >= lockMinutes)
            {
                PortalExecute("UPDATE my_aspnet_membership SET isLockedOut=0, failedPasswordAttemptCount=0 WHERE userId=@uid",
                    new MySqlParameter("@uid", uid));
                return false;
            }

            int remaining = lockMinutes - (int)(DateTime.UtcNow - lockDate).TotalMinutes;
            message = "Account temporarily locked due to too many failed login attempts. Try again in " + remaining + " minute(s).";
            return true;
        }
        catch { return false; }
    }

    private void IncrementFailedAttempts(int userId)
    {
        try
        {
            PortalExecute(
                @"UPDATE my_aspnet_membership
                  SET failedPasswordAttemptCount = failedPasswordAttemptCount + 1,
                      isLockedOut = IF(failedPasswordAttemptCount + 1 >= 5, 1, 0),
                      lastLockoutDate = IF(failedPasswordAttemptCount + 1 >= 5, @now, lastLockoutDate)
                  WHERE userId=@uid",
                new MySqlParameter("@now", DateTime.UtcNow),
                new MySqlParameter("@uid", userId));
        }
        catch { }
    }

    private void ResetFailedAttempts(int userId)
    {
        try
        {
            PortalExecute("UPDATE my_aspnet_membership SET failedPasswordAttemptCount=0, isLockedOut=0, lastLoginDate=@now WHERE userId=@uid",
                new MySqlParameter("@now", DateTime.UtcNow),
                new MySqlParameter("@uid", userId));
        }
        catch { }
    }

    private string GetApplicantEmail(int userId)
    {
        try
        {
            object v = PortalScalar("SELECT IFNULL(verified_email, name) FROM my_aspnet_users WHERE id=@uid LIMIT 1",
                new MySqlParameter("@uid", userId));
            return v != null && v != DBNull.Value ? v.ToString() : string.Empty;
        }
        catch { return string.Empty; }
    }

    private int GetPortalAppId()
    {
        try
        {
            object v = PortalScalar("SELECT id FROM my_aspnet_applications ORDER BY id LIMIT 1");
            return (v != null && v != DBNull.Value) ? Convert.ToInt32(v) : 1;
        }
        catch { return 1; }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPER METHODS — Application
    // ═══════════════════════════════════════════════════════════════════

    private string EnsureDraftEntry(int userId, string email)
    {
        // Return existing editable entry if one exists
        string existing = GetEditableEntryNo(userId);
        if (!string.IsNullOrEmpty(existing)) return existing;

        // The year being admitted FOR, not the year on the wall clock. Resolved once so the
        // number's prefix and stud_entry_year cannot disagree — the generator counts the
        // sequence with WHERE stud_entry_year = yr, so a mismatch restarts it at 1.
        int appYear = AcademicYearHelper.CurrentApplicationYear();

        // Generate new entry number
        string entryNo;
        try
        {
            object spResult = ApiHelper.Scalar(
                "SELECT acad_ApplicNoGenerator(@yr)",
                new MySqlParameter("@yr", appYear));
            entryNo = (spResult != null && spResult != DBNull.Value) ? spResult.ToString() : null;
        }
        catch { entryNo = null; }

        if (string.IsNullOrEmpty(entryNo))
            entryNo = "APL" + DateTime.UtcNow.ToString("yyMMddHHmmssff");

        // stud_entry_year was never set on this path, which is why applications created
        // through the API left it NULL and dropped out of the sequence count entirely.
        ApiHelper.Execute(
            @"INSERT INTO acad_applications (stud_entry_no, applicant_user_id, stud_email, stud_entry_year, app_status, app_created_at, app_last_updated_at)
              VALUES (@eno, @uid, @email, @year, 'DRAFT', @now, @now)",
            new MySqlParameter("@eno",   entryNo),
            new MySqlParameter("@uid",   userId),
            new MySqlParameter("@email", email),
            new MySqlParameter("@year",  appYear),
            new MySqlParameter("@now",   DateTime.UtcNow));

        return entryNo;
    }

    private string GetEditableEntryNo(int userId)
    {
        object v = ApiHelper.Scalar(
            "SELECT stud_entry_no FROM acad_applications WHERE applicant_user_id=@uid AND app_status IN ('DRAFT','REJECTED') ORDER BY stud_entry_no DESC LIMIT 1",
            new MySqlParameter("@uid", userId));
        return (v != null && v != DBNull.Value) ? v.ToString() : string.Empty;
    }

    private string GetApplicantEntryNo(int userId)
    {
        object v = ApiHelper.Scalar(
            "SELECT stud_entry_no FROM acad_applications WHERE applicant_user_id=@uid ORDER BY stud_entry_no DESC LIMIT 1",
            new MySqlParameter("@uid", userId));
        return (v != null && v != DBNull.Value) ? v.ToString() : string.Empty;
    }

    private bool IsEditable(string entryNo)
    {
        object v = ApiHelper.Scalar(
            "SELECT app_status FROM acad_applications WHERE stud_entry_no=@eno LIMIT 1",
            new MySqlParameter("@eno", entryNo));
        if (v == null || v == DBNull.Value) return true;
        string s = v.ToString();
        return s == "DRAFT" || s == "REJECTED";
    }

    private void InsertNotification(int userId, string message, string link)
    {
        try
        {
            ApiHelper.Execute(
                "INSERT INTO apply_notifications (user_id, message, is_read, link, created_at) VALUES (@uid, @msg, 0, @lnk, @now)",
                new MySqlParameter("@uid", userId),
                new MySqlParameter("@msg", message),
                new MySqlParameter("@lnk", link ?? ""),
                new MySqlParameter("@now", DateTime.UtcNow));
        }
        catch { }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPER METHODS — Portal DB
    // ═══════════════════════════════════════════════════════════════════

    private DataTable PortalQuery(string sql, params MySqlParameter[] parms)
    {
        using (var conn = ApiHelper.GetPortalConnection())
        {
            conn.Open();
            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.CommandTimeout = 30;
                if (parms != null) foreach (var p in parms) cmd.Parameters.Add(p);
                using (var adapter = new MySqlDataAdapter(cmd))
                {
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }
    }

    private void PortalExecute(string sql, params MySqlParameter[] parms)
    {
        using (var conn = ApiHelper.GetPortalConnection())
        {
            conn.Open();
            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.CommandTimeout = 30;
                if (parms != null) foreach (var p in parms) cmd.Parameters.Add(p);
                cmd.ExecuteNonQuery();
            }
        }
    }

    private long PortalExecuteInsert(string sql, params MySqlParameter[] parms)
    {
        using (var conn = ApiHelper.GetPortalConnection())
        {
            conn.Open();
            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.CommandTimeout = 30;
                if (parms != null) foreach (var p in parms) cmd.Parameters.Add(p);
                cmd.ExecuteNonQuery();
                return cmd.LastInsertedId;
            }
        }
    }

    private object PortalScalar(string sql, params MySqlParameter[] parms)
    {
        using (var conn = ApiHelper.GetPortalConnection())
        {
            conn.Open();
            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.CommandTimeout = 30;
                if (parms != null) foreach (var p in parms) cmd.Parameters.Add(p);
                return cmd.ExecuteScalar();
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPER METHODS — Crypto, Email, Files
    // ═══════════════════════════════════════════════════════════════════

    private static string HashPassword(string password, string base64Salt)
    {
        byte[] saltBytes     = Convert.FromBase64String(base64Salt);
        byte[] passwordBytes = Encoding.Unicode.GetBytes(password);
        byte[] combined      = new byte[saltBytes.Length + passwordBytes.Length];
        System.Buffer.BlockCopy(saltBytes,     0, combined, 0,               saltBytes.Length);
        System.Buffer.BlockCopy(passwordBytes, 0, combined, saltBytes.Length, passwordBytes.Length);
        using (var hmac = new HMACSHA256(saltBytes))
            return Convert.ToBase64String(hmac.ComputeHash(combined));
    }

    private static string GenerateOtp()
    {
        byte[] bytes = new byte[4];
        using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(bytes);
        uint num = BitConverter.ToUInt32(bytes, 0) % 900000 + 100000;
        return num.ToString();
    }

    private void SendApplyEmail(string toEmail, string subject, string body)
    {
        try
        {
            string host     = ConfigurationManager.AppSettings["MAIL_HOST"] ?? "";
            string portRaw  = ConfigurationManager.AppSettings["MAIL_PORT"] ?? "587";
            string user     = ConfigurationManager.AppSettings["MAIL_USERNAME"] ?? "";
            string pass     = ConfigurationManager.AppSettings["MAIL_PASSWORD"] ?? "";
            string from     = ConfigurationManager.AppSettings["MAIL_FROM_ADDRESS"] ?? user;
            string fromName = "MRU Admissions";
            string enc      = (ConfigurationManager.AppSettings["MAIL_ENCRYPTION"] ?? "ssl").ToLower();

            if (string.IsNullOrEmpty(host)) return;

            int port = 587;
            int.TryParse(portRaw, out port);

            using (var msg = new MailMessage())
            {
                msg.From    = new MailAddress(from, fromName);
                msg.To.Add(new MailAddress(toEmail));
                msg.Subject = subject;
                msg.Body    = body;
                msg.IsBodyHtml = true;

                using (var smtp = new SmtpClient(host, port))
                {
                    smtp.Credentials = new NetworkCredential(user, pass);
                    smtp.EnableSsl   = enc == "ssl" || enc == "tls";
                    smtp.Send(msg);
                }
            }
        }
        catch { /* email sending is non-fatal */ }
    }

    private string GetUploadDir()
    {
        string configured = ConfigurationManager.AppSettings["ApplyUploadPath"] ?? "";
        if (!string.IsNullOrEmpty(configured) && Directory.Exists(configured)) return configured;
        string serverPath = Server.MapPath("~/apply-uploads");
        if (!Directory.Exists(serverPath)) Directory.CreateDirectory(serverPath);
        return serverPath;
    }
}
