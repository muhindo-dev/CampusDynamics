using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

public partial class API_v2_staff : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        if (ApiHelper.HandleCors(Request, Response)) return;

        string action = ApiHelper.Param(Request, "action", "").ToLower();

        try
        {
            switch (action)
            {
                case "profile":
                    HandleProfile();
                    break;
                case "photo":
                    HandlePhoto();
                    break;
                case "my_courses":
                    HandleMyCourses();
                    break;
                case "class_list":
                    HandleClassList();
                    break;
                case "marks":
                    HandleMarks();
                    break;
                case "submit_marks":
                    HandleSubmitMarks();
                    break;
                case "filter_options":
                    HandleFilterOptions();
                    break;
                case "all_courses":
                    HandleAllCourses();
                    break;
                case "mark_sheet":
                    HandleMarkSheet();
                    break;
                case "save_entry_marks":
                    HandleSaveEntryMarks();
                    break;
                case "submit_for_approval":
                    HandleSubmitForApproval();
                    break;
                case "sheet_status":
                    HandleSheetStatus();
                    break;
                case "deadlines":
                    HandleDeadlines();
                    break;
                case "lookup":
                    HandleLookup();
                    break;
                case "by_department":
                    HandleByDepartment();
                    break;
                // ── Provisional Marks ──
                case "provisional_marks_list":
                    HandleProvisionalMarksList();
                    break;
                case "provisional_mark_detail":
                    HandleProvisionalMarkDetail();
                    break;
                case "save_provisional_mark":
                    HandleSaveProvisionalMark();
                    break;
                case "save_provisional_mark_inline":
                    HandleSaveProvisionalMarkInline();
                    break;
                case "provisional_marks_summary":
                    HandleProvisionalMarksSummary();
                    break;
                case "bulk_save_marks":
                    HandleBulkSaveMarks();
                    break;
                case "mark_stats":
                    HandleMarkStats();
                    break;
                case "student_search":
                    HandleStudentSearch();
                    break;
                // ── Course Self-Allocation ──
                case "course_allocation_search":
                    HandleCourseAllocationSearch();
                    break;
                case "course_allocation_submit":
                    HandleCourseAllocationSubmit();
                    break;
                case "course_unassign":
                    HandleCourseUnassign();
                    break;
                // ── Course Registration (enroll students) ──
                case "course_reg_summary":
                    HandleCourseRegSummary();
                    break;
                case "course_reg_list":
                    HandleCourseRegList();
                    break;
                case "course_reg_validate_student":
                    HandleCourseRegValidateStudent();
                    break;
                case "course_reg_enroll":
                    HandleCourseRegEnroll();
                    break;
                case "course_reg_student_courses":
                    HandleCourseRegStudentCourses();
                    break;
                case "course_reg_popularity":
                    HandleCourseRegPopularity();
                    break;
                // ── Dashboard ──
                case "dashboard_stats":
                    HandleDashboardStats();
                    break;
                // ── Lecturer Mark Requests (portal workflow) ──
                case "lmr_requests":
                    HandleLmrRequests();
                    break;
                case "lmr_respond":
                    HandleLmrRespond();
                    break;
                case "lmr_reject":
                    HandleLmrReject();
                    break;
                // ── HR Employees ──
                case "employees":
                    HandleEmployees();
                    break;
                case "employee":
                    HandleEmployee();
                    break;
                case "create_employee":
                    HandleCreateEmployee();
                    break;
                case "update_employee":
                    HandleUpdateEmployee();
                    break;
                case "update_contract":
                    HandleUpdateContract();
                    break;
                case "departments":
                    HandleDepartments();
                    break;
                // ── Mark Requests ──
                case "mark_requests_list":
                    HandleMarkRequestsList();
                    break;
                case "create_mark_request":
                    HandleCreateMarkRequest();
                    break;
                case "mark_request_detail":
                    HandleMarkRequestDetail();
                    break;
                case "cancel_mark_request":
                    HandleCancelMarkRequest();
                    break;
                case "admin_mark_requests":
                    HandleAdminMarkRequests();
                    break;
                case "decide_mark_request":
                    HandleDecideMarkRequest();
                    break;
                default:
                    ApiHelper.Error(Response,
                        "Unknown action '" + action + "'. Valid actions — " +
                        "Core: profile, photo, my_courses, class_list, filter_options, all_courses, lookup, by_department, dashboard_stats | " +
                        "Course Allocation: course_allocation_search, course_allocation_submit, course_unassign | " +
                        "Course Registration: course_reg_summary, course_reg_list, course_reg_validate_student, course_reg_enroll, course_reg_student_courses, course_reg_popularity | " +
                        "Lecturer Mark Requests: lmr_requests, lmr_respond, lmr_reject | " +
                        "Mark Sheet: mark_sheet, save_entry_marks, submit_for_approval, sheet_status, deadlines | " +
                        "Provisional Marks: provisional_marks_list, provisional_mark_detail, save_provisional_mark, save_provisional_mark_inline, bulk_save_marks, provisional_marks_summary, mark_stats | " +
                        "Student: student_search | " +
                        "Mark Requests: mark_requests_list, create_mark_request, mark_request_detail, cancel_mark_request, admin_mark_requests, decide_mark_request | " +
                        "HR: employees, employee, create_employee, update_employee, update_contract, departments",
                        "INVALID_ACTION");
                    break;
            }
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Internal server error: " + ex.Message, "SERVER_ERROR");
        }
    }

    private void HandleProfile()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;

        // Staff can view own profile; admin can query other staff by ?staff_id=
        string staffUsername = auth.UserId;
        if (auth.UserType == "staff")
        {
            string requestedId = ApiHelper.Param(Request, "staff_id", "");
            if (!string.IsNullOrEmpty(requestedId))
                staffUsername = requestedId;
        }
        else
        {
            ApiHelper.Error(Response, "This endpoint is for staff members only.", "ACCESS_DENIED");
            return;
        }

        DataTable dt = ApiHelper.Query(
            @"SELECT e.empID, e.emp_name, e.EMP_CODE, e.EmpType,
                     e.emp_email, e.emp_phone, e.emp_nationality,
                     d.dept_name AS department,
                     e.emp_qualifications, e.usernames,
                     c.contractStart, c.contractEnd, c.contractStatus
              FROM hrm_employee e
              LEFT JOIN hrm_emp_contracts c ON c.empID = e.empID AND c.contractStatus = 'VALID'
              LEFT JOIN hrm_departments d ON c.departmentID = d.ID
              WHERE e.usernames = @uid",
            new MySqlParameter("@uid", staffUsername)
        );

        if (dt.Rows.Count == 0)
        {
            ApiHelper.Error(Response, "Staff member not found.", "NOT_FOUND");
            return;
        }

        var profile = ApiHelper.FirstRowToDict(dt);
        profile["photo_url"] = "/API/v2/staff.aspx?action=photo&token=" + ApiHelper.Param(Request, "token", "");

        ApiHelper.Success(Response, profile);
    }

    private void HandlePhoto()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;

        if (auth.UserType != "staff")
        {
            ApiHelper.Error(Response, "This endpoint is for staff members only.", "ACCESS_DENIED");
            return;
        }

        // Get empID from username
        DataTable empDt = ApiHelper.Query(
            "SELECT empID FROM hrm_employee WHERE usernames = @uid",
            new MySqlParameter("@uid", auth.UserId)
        );

        if (empDt.Rows.Count == 0)
        {
            ApiHelper.Error(Response, "Staff member not found.", "NOT_FOUND");
            return;
        }

        string empId = empDt.Rows[0]["empID"].ToString();

        // Try file-based photo (primary location)
        string photoPath = Server.MapPath("~/COOPERP/staffimages/" + empId + "_photo.jpg");
        if (File.Exists(photoPath))
        {
            Response.Clear();
            Response.ContentType = "image/jpeg";
            Response.AddHeader("Access-Control-Allow-Origin", "*");
            Response.WriteFile(photoPath);
            ApiHelper.CompleteResponse(Response);
            return;
        }

        // Try alternate location
        string altPath = Server.MapPath("~/staffimages/" + empId + ".jpg");
        if (File.Exists(altPath))
        {
            Response.Clear();
            Response.ContentType = "image/jpeg";
            Response.AddHeader("Access-Control-Allow-Origin", "*");
            Response.WriteFile(altPath);
            ApiHelper.CompleteResponse(Response);
            return;
        }

        ApiHelper.Error(Response, "Photo not found.", "NOT_FOUND");
    }

    private void HandleMyCourses()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;

        if (auth.UserType != "staff")
        {
            ApiHelper.Error(Response, "This endpoint is for staff members only.", "ACCESS_DENIED");
            return;
        }

        string acadYear  = ApiHelper.Param(Request, "acad_year", "");
        int    semester  = ApiHelper.ParamInt(Request, "semester", 0);

        // Resolve all staff identifiers — match usernames OR EMP_CODE (mirrors portal's ResolveStaffContext)
        DataTable empDt = ApiHelper.Query(
            @"SELECT EMP_CODE, empID FROM hrm_employee
              WHERE (NULLIF(TRIM(IFNULL(usernames,'')),'-') IS NOT NULL AND UPPER(TRIM(usernames)) = UPPER(@uid))
                 OR UPPER(TRIM(IFNULL(EMP_CODE,''))) = UPPER(@uid2)
              LIMIT 1",
            new MySqlParameter("@uid",  auth.UserId),
            new MySqlParameter("@uid2", auth.UserId));

        string staffCode = empDt.Rows.Count > 0 ? empDt.Rows[0]["EMP_CODE"].ToString() : "";
        string empId     = empDt.Rows.Count > 0 ? empDt.Rows[0]["empID"].ToString()     : "";

        // ── Source 1: programme course assignments (primary — matches portal logic) ──
        var sql1 = new System.Text.StringBuilder(
            @"SELECT pc.course_code,
                     COALESCE(c.courseName, pc.course_code) AS course_name,
                     COALESCE(c.CreditUnit, 0) AS credit_units,
                     pc.progcode AS programme_code,
                     COALESCE(p.progname, pc.progcode) AS programme_name,
                     IFNULL(pc.study_year, 1) AS study_year,
                     pc.semester,
                     '' AS acad_year, 0 AS campus_id, '' AS session,
                     1 AS is_active, 'programme_assignment' AS source
              FROM acad_programmecourses pc
              LEFT JOIN acad_course    c ON c.courseID  = pc.course_code
              LEFT JOIN acad_programme p ON p.progcode  = pc.progcode
              WHERE IFNULL(pc.lecturer_id, 0) = @empId
                AND UPPER(IFNULL(pc.is_lecturere_assigned, 'NO')) = 'YES'");
        var p1 = new List<MySqlParameter>();
        p1.Add(new MySqlParameter("@empId", empId));
        if (semester > 0) { sql1.Append(" AND pc.semester = @sem1"); p1.Add(new MySqlParameter("@sem1", semester)); }
        sql1.Append(" ORDER BY pc.progcode, pc.course_code");

        // ── Source 2: new marks-module teaching assignments ───────────────────
        var sql2 = new System.Text.StringBuilder(
            @"SELECT ta.course_id AS course_code,
                     COALESCE(c.courseName, ta.course_id) AS course_name,
                     COALESCE(c.CreditUnit, 0) AS credit_units,
                     ta.progid AS programme_code,
                     COALESCE(p.progname, ta.progid) AS programme_name,
                     ta.study_year,
                     ta.semester,
                     ta.acadyear AS acad_year, ta.campus_id, ta.stud_session AS session,
                     ta.is_active, 'assignments' AS source
              FROM acad_teaching_assignments ta
              LEFT JOIN acad_course    c ON c.courseID = ta.course_id
              LEFT JOIN acad_programme p ON p.progcode = ta.progid
              WHERE ta.teacher_username = @username AND ta.is_active = 1");
        var p2 = new List<MySqlParameter>();
        p2.Add(new MySqlParameter("@username", auth.UserId));
        if (!string.IsNullOrEmpty(acadYear)) { sql2.Append(" AND ta.acadyear = @ay2");  p2.Add(new MySqlParameter("@ay2",  acadYear)); }
        if (semester > 0)                    { sql2.Append(" AND ta.semester = @sem2"); p2.Add(new MySqlParameter("@sem2", semester)); }
        sql2.Append(" ORDER BY ta.acadyear DESC, ta.semester, ta.course_id");

        // ── Source 3: legacy teaching allocation ──────────────────────────────
        var sql3 = new System.Text.StringBuilder(
            @"SELECT ta.courseID AS course_code,
                     COALESCE(c.courseName, ta.courseID) AS course_name,
                     COALESCE(c.CreditUnit, 0) AS credit_units,
                     ta.progcode AS programme_code,
                     COALESCE(p.progname, ta.progcode) AS programme_name,
                     ta.cyear AS study_year,
                     ta.semester,
                     ta.acad_year, 0 AS campus_id, 'Day' AS session,
                     1 AS is_active, 'allocation' AS source
              FROM acad_teaching_allocation ta
              LEFT JOIN acad_course    c ON c.courseID = ta.courseID
              LEFT JOIN acad_programme p ON p.progcode = ta.progcode
              WHERE ta.staffCode = @staffCode");
        var p3 = new List<MySqlParameter>();
        p3.Add(new MySqlParameter("@staffCode", staffCode));
        if (!string.IsNullOrEmpty(acadYear)) { sql3.Append(" AND ta.acad_year = @ay3");  p3.Add(new MySqlParameter("@ay3",  acadYear)); }
        if (semester > 0)                    { sql3.Append(" AND ta.semester = @sem3");  p3.Add(new MySqlParameter("@sem3", semester)); }
        sql3.Append(" ORDER BY ta.acad_year DESC, ta.semester, ta.courseID");

        // Run all three sources; suppress query errors for optional sources
        DataTable dt1 = !string.IsNullOrEmpty(empId) ? ApiHelper.Query(sql1.ToString(), p1.ToArray()) : new DataTable();
        DataTable dt2 = ApiHelper.Query(sql2.ToString(), p2.ToArray());
        DataTable dt3 = !string.IsNullOrEmpty(staffCode) ? ApiHelper.Query(sql3.ToString(), p3.ToArray()) : new DataTable();

        // Merge all three sources — deduplicate by course+programme (programme_assignment rows lack acad_year)
        var courses = ApiHelper.TableToList(dt1);
        var seen    = new System.Collections.Generic.HashSet<string>();
        foreach (var row in courses)
            seen.Add(Convert.ToString(row["course_code"]) + "|" + Convert.ToString(row["programme_code"]));

        foreach (var row in ApiHelper.TableToList(dt2))
        {
            string key = Convert.ToString(row["course_code"]) + "|" + Convert.ToString(row["programme_code"]);
            if (!seen.Contains(key)) { courses.Add(row); seen.Add(key); }
        }

        foreach (var row in ApiHelper.TableToList(dt3))
        {
            string key = Convert.ToString(row["course_code"]) + "|" + Convert.ToString(row["programme_code"]);
            if (!seen.Contains(key)) { courses.Add(row); seen.Add(key); }
        }

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "total_courses", courses.Count },
            { "courses", courses }
        });
    }

    // ── Schema guard: adds allocation_request_* columns to acad_programmecourses if missing ──
    private static volatile bool _allocationSchemaReady = false;
    private static readonly object _allocationSchemaLock = new object();
    private void EnsureAllocationSchema()
    {
        if (_allocationSchemaReady) return;
        lock (_allocationSchemaLock)
        {
            if (_allocationSchemaReady) return;
            string[] alters = {
                "ALTER TABLE acad_programmecourses ADD COLUMN allocation_request_status VARCHAR(3) NULL DEFAULT 'No'",
                "ALTER TABLE acad_programmecourses ADD COLUMN allocation_request_lecturer_id INT NULL",
                "ALTER TABLE acad_programmecourses ADD COLUMN allocation_request_date DATETIME NULL",
                "ALTER TABLE acad_programmecourses ADD COLUMN allocation_request_message TEXT NULL",
                "ALTER TABLE acad_programmecourses ADD COLUMN allocation_request_admin_status VARCHAR(20) NULL DEFAULT 'Pending'",
                "ALTER TABLE acad_programmecourses ADD COLUMN allocation_request_admin_message TEXT NULL"
            };
            foreach (string sql in alters) { try { ApiHelper.Execute(sql); } catch { } }
            _allocationSchemaReady = true;
        }
    }

    private void HandleCourseAllocationSearch()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        EnsureAllocationSchema();

        string empId = GetLecturerEmpId(auth.UserId) ?? "";

        string q        = ApiHelper.Param(Request, "q", "").Trim();
        int    semester = ApiHelper.ParamInt(Request, "semester", 0);
        int    size     = Math.Min(100, Math.Max(1, ApiHelper.ParamInt(Request, "size", 50)));

        var sql = new System.Text.StringBuilder(
            @"SELECT pc.ID AS programme_course_id,
                     pc.course_code,
                     IFNULL(c.courseName, pc.course_code) AS course_name,
                     pc.progcode AS programme_code,
                     IFNULL(p.progname, pc.progcode) AS programme_name,
                     IFNULL(sp.spec, '') AS specialisation,
                     IFNULL(pc.study_year, 1) AS study_year,
                     IFNULL(pc.semester, 1) AS semester,
                     IFNULL(pc.lecturer_id, 0) AS current_lecturer_id,
                     UPPER(IFNULL(pc.is_lecturere_assigned, 'NO')) AS is_assigned,
                     IFNULL(e.emp_name, '') AS current_lecturer_name,
                     IFNULL(e.EMP_CODE, '') AS current_lecturer_code,
                     IFNULL(pc.allocation_request_status, 'No') AS allocation_request_status,
                     IFNULL(pc.allocation_request_admin_status, 'Pending') AS allocation_request_admin_status
              FROM acad_programmecourses pc
              LEFT JOIN acad_course    c  ON c.courseID    = pc.course_code
              LEFT JOIN acad_programme p  ON p.progcode    = pc.progcode
              LEFT JOIN acad_specialisation sp ON sp.spec_id = pc.specialisation_id
              LEFT JOIN hrm_employee   e  ON e.empID       = pc.lecturer_id
              WHERE 1=1");
        var parms = new List<MySqlParameter>();

        if (!string.IsNullOrEmpty(q))
        {
            sql.Append(" AND (pc.course_code LIKE @q OR IFNULL(c.courseName,'') LIKE @q OR IFNULL(p.progname,'') LIKE @q)");
            parms.Add(new MySqlParameter("@q", "%" + q + "%"));
        }
        if (semester > 0)
        {
            sql.Append(" AND pc.semester = @sem");
            parms.Add(new MySqlParameter("@sem", semester));
        }
        sql.Append(" ORDER BY IFNULL(c.courseName,''), pc.course_code LIMIT @lim");
        parms.Add(new MySqlParameter("@lim", size));

        DataTable dt = ApiHelper.Query(sql.ToString(), parms.ToArray());

        int myEmpId = 0;
        int.TryParse(empId, out myEmpId);

        var items = new List<Dictionary<string, object>>();
        foreach (DataRow r in dt.Rows)
        {
            int lecturerId = 0;
            int.TryParse(Convert.ToString(r["current_lecturer_id"]), out lecturerId);
            bool assignedYes = string.Equals(Convert.ToString(r["is_assigned"]), "YES", StringComparison.OrdinalIgnoreCase);
            bool alreadyMine = myEmpId > 0 && lecturerId == myEmpId && assignedYes;
            string lecturerName = Convert.ToString(r["current_lecturer_name"]).Trim();
            string lecturerCode = Convert.ToString(r["current_lecturer_code"]).Trim();
            string currentLecturerDisplay = assignedYes && !string.IsNullOrEmpty(lecturerName)
                ? (lecturerName + (string.IsNullOrEmpty(lecturerCode) ? "" : " (" + lecturerCode + ")"))
                : null;

            items.Add(new Dictionary<string, object>
            {
                { "programme_course_id",         Convert.ToString(r["programme_course_id"]) },
                { "course_code",                  Convert.ToString(r["course_code"]) },
                { "course_name",                  Convert.ToString(r["course_name"]) },
                { "programme_code",               Convert.ToString(r["programme_code"]) },
                { "programme_name",               Convert.ToString(r["programme_name"]) },
                { "specialisation",               Convert.ToString(r["specialisation"]) },
                { "study_year",                   Convert.ToString(r["study_year"]) },
                { "semester",                     Convert.ToString(r["semester"]) },
                { "is_assigned",                  assignedYes },
                { "current_lecturer",             currentLecturerDisplay },
                { "already_mine",                 alreadyMine },
                { "can_allocate",                 !alreadyMine },
                { "allocation_request_status",    Convert.ToString(r["allocation_request_status"]) },
                { "allocation_request_admin_status", Convert.ToString(r["allocation_request_admin_status"]) }
            });
        }

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "total", items.Count },
            { "courses", items }
        });
    }

    private void HandleCourseAllocationSubmit()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        EnsureAllocationSchema();

        string empIdStr = GetLecturerEmpId(auth.UserId);
        if (string.IsNullOrEmpty(empIdStr))
        {
            ApiHelper.Error(Response, "Staff profile not found. Cannot process allocation.", "NOT_FOUND");
            return;
        }
        int myEmpId = int.Parse(empIdStr);

        // Accept programme_course_ids as comma-separated string or repeated query params
        string idsRaw  = ApiHelper.Param(Request, "programme_course_ids", "").Trim();
        string message = ApiHelper.Param(Request, "message", "").Trim();

        if (string.IsNullOrEmpty(idsRaw))
        {
            ApiHelper.Error(Response, "programme_course_ids is required (comma-separated list of IDs).", "VALIDATION_ERROR");
            return;
        }
        if (string.IsNullOrEmpty(message))
        {
            ApiHelper.Error(Response, "message is required — briefly state why you are allocating yourself to this course.", "VALIDATION_ERROR");
            return;
        }
        if (message.Length > 1000) message = message.Substring(0, 1000);

        // Parse IDs
        var ids = new List<int>();
        foreach (string part in idsRaw.Split(','))
        {
            int id;
            if (int.TryParse(part.Trim(), out id) && id > 0 && !ids.Contains(id))
                ids.Add(id);
        }
        if (ids.Count == 0)
        {
            ApiHelper.Error(Response, "No valid IDs found in programme_course_ids.", "VALIDATION_ERROR");
            return;
        }

        int assignedCount = 0, alreadyMineCount = 0, notFoundCount = 0, unchangedCount = 0;

        using (var conn = ApiHelper.GetConnection())
        {
            conn.Open();
            using (var tx = conn.BeginTransaction())
            {
                foreach (int targetId in ids)
                {
                    int    lecturerId = 0;
                    string isAssigned = "No";

                    using (var cmdCheck = new MySqlCommand(
                        "SELECT IFNULL(lecturer_id,0) AS lid, IFNULL(is_lecturere_assigned,'No') AS ia FROM acad_programmecourses WHERE ID=@id",
                        conn, tx))
                    {
                        cmdCheck.Parameters.AddWithValue("@id", targetId);
                        using (var rdr = cmdCheck.ExecuteReader())
                        {
                            if (!rdr.Read()) { notFoundCount++; continue; }
                            int.TryParse(rdr["lid"].ToString(), out lecturerId);
                            isAssigned = rdr["ia"].ToString();
                        }
                    }

                    if (lecturerId == myEmpId && string.Equals(isAssigned, "Yes", StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyMineCount++;
                        continue;
                    }

                    using (var cmdUp = new MySqlCommand(
                        @"UPDATE acad_programmecourses
                          SET lecturer_id                        = @lid,
                              is_lecturere_assigned              = 'Yes',
                              allocation_request_status          = 'Yes',
                              allocation_request_lecturer_id     = @lid,
                              allocation_request_date            = NOW(),
                              allocation_request_message         = @msg,
                              allocation_request_admin_status    = 'Approved',
                              allocation_request_admin_message   = 'Auto-approved: assigned immediately by lecturer request.'
                          WHERE ID = @id",
                        conn, tx))
                    {
                        cmdUp.Parameters.AddWithValue("@lid", myEmpId);
                        cmdUp.Parameters.AddWithValue("@msg", message);
                        cmdUp.Parameters.AddWithValue("@id",  targetId);
                        int affected = cmdUp.ExecuteNonQuery();
                        if (affected > 0) assignedCount++; else unchangedCount++;
                    }
                }
                tx.Commit();
            }
        }

        if (assignedCount == 0)
        {
            string errMsg = alreadyMineCount > 0 && notFoundCount == 0 && unchangedCount == 0
                ? (alreadyMineCount == 1 ? "The selected course is already allocated to you." : "All selected courses are already allocated to you.")
                : "No courses were allocated. Please review your selection and try again.";
            ApiHelper.Error(Response, errMsg, "NO_CHANGE");
            return;
        }

        var parts = new List<string>();
        parts.Add(assignedCount + (assignedCount == 1 ? " course allocated" : " courses allocated"));
        if (alreadyMineCount > 0) parts.Add(alreadyMineCount + " already yours");
        if (notFoundCount > 0)    parts.Add(notFoundCount + " not found");
        if (unchangedCount > 0)   parts.Add(unchangedCount + " unchanged");

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "assigned_count",    assignedCount },
            { "already_mine_count", alreadyMineCount },
            { "not_found_count",   notFoundCount },
            { "skipped_count",     unchangedCount },
            { "message",           string.Join("; ", parts.ToArray()) + "." }
        });
    }

    private void HandleCourseUnassign()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        EnsureAllocationSchema();

        string empIdStr = GetLecturerEmpId(auth.UserId);
        if (string.IsNullOrEmpty(empIdStr))
        {
            ApiHelper.Error(Response, "Staff profile not found.", "NOT_FOUND");
            return;
        }
        int myEmpId = int.Parse(empIdStr);

        int programmeCourseId = ApiHelper.ParamInt(Request, "programme_course_id", 0);
        if (programmeCourseId <= 0)
        {
            ApiHelper.Error(Response, "programme_course_id is required.", "VALIDATION_ERROR");
            return;
        }

        int updated = ApiHelper.Execute(
            @"UPDATE acad_programmecourses
              SET lecturer_id                      = NULL,
                  is_lecturere_assigned            = 'No',
                  allocation_request_status        = 'No',
                  allocation_request_lecturer_id   = NULL,
                  allocation_request_date          = NULL,
                  allocation_request_message       = NULL,
                  allocation_request_admin_status  = 'Pending',
                  allocation_request_admin_message = ''
              WHERE ID = @id
                AND IFNULL(lecturer_id, 0) = @sid
                AND UPPER(IFNULL(is_lecturere_assigned, 'No')) = 'YES'",
            new MySqlParameter("@id",  programmeCourseId),
            new MySqlParameter("@sid", myEmpId));

        if (updated <= 0)
        {
            int exists = Convert.ToInt32(ApiHelper.Scalar(
                "SELECT COUNT(*) FROM acad_programmecourses WHERE ID = @id",
                new MySqlParameter("@id", programmeCourseId)));
            string msg = exists > 0
                ? "This course is not currently assigned to you."
                : "Selected course context was not found.";
            ApiHelper.Error(Response, msg, "NOT_FOUND");
            return;
        }

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "message", "Course unassigned successfully." }
        });
    }

    // ── Schema guard for acad_course_registration optional columns ─────────
    private static volatile bool _crSchemaReady = false;
    private static volatile bool _crHasCreatedDate = false;
    private static volatile bool _crHasStudSession  = false;
    private static readonly object _crSchemaLock = new object();
    private void EnsureCourseRegSchema()
    {
        if (_crSchemaReady) return;
        lock (_crSchemaLock)
        {
            if (_crSchemaReady) return;
            _crHasCreatedDate = Convert.ToInt32(ApiHelper.Scalar(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='campus_dynamics_portal' AND TABLE_NAME='acad_course_registration' AND COLUMN_NAME='created_date'")) > 0;
            _crHasStudSession = Convert.ToInt32(ApiHelper.Scalar(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='campus_dynamics_portal' AND TABLE_NAME='acad_course_registration' AND COLUMN_NAME='stud_session'")) > 0;
            _crSchemaReady = true;
        }
    }

    // Returns list of UPPER course codes assigned to this empId; null = no restriction; empty list = no courses
    private List<string> GetLecturerCourseCodes(string empIdStr)
    {
        int empId = 0;
        if (string.IsNullOrEmpty(empIdStr) || !int.TryParse(empIdStr, out empId) || empId <= 0)
            return new List<string>(); // no courses

        DataTable dt = ApiHelper.Query(
            "SELECT DISTINCT UPPER(TRIM(course_code)) AS cc FROM acad_programmecourses " +
            "WHERE IFNULL(lecturer_id,0)=@sid AND UPPER(IFNULL(is_lecturere_assigned,'NO'))='YES'",
            new MySqlParameter("@sid", empId));
        var list = new List<string>();
        foreach (DataRow r in dt.Rows)
        {
            string c = Convert.ToString(r["cc"]).Trim();
            if (!string.IsNullOrEmpty(c)) list.Add(c);
        }
        return list;
    }

    // Appends an IN(...) clause to sb; returns false if codes is empty (caller should short-circuit)
    private bool AppendCourseFilter(System.Text.StringBuilder sb, List<MySqlParameter> parms,
                                    List<string> courseCodes, string colExpr)
    {
        if (courseCodes == null) return true; // no restriction
        if (courseCodes.Count == 0) return false; // nothing matches
        sb.Append(" AND " + colExpr + " IN (");
        for (int i = 0; i < courseCodes.Count; i++)
        {
            if (i > 0) sb.Append(",");
            string pname = "@lc" + i;
            sb.Append(pname);
            parms.Add(new MySqlParameter(pname, courseCodes[i]));
        }
        sb.Append(")");
        return true;
    }

    private void HandleCourseRegSummary()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string empId      = GetLecturerEmpId(auth.UserId) ?? "";
        string acadYear   = ApiHelper.Param(Request, "acad_year", "").Trim();
        int    semester   = ApiHelper.ParamInt(Request, "semester", 0);
        string progCode   = ApiHelper.Param(Request, "programme_code", "").Trim();
        string courseCode = ApiHelper.Param(Request, "course_code", "").Trim();

        List<string> myCodes = GetLecturerCourseCodes(empId);
        if (myCodes.Count == 0)
        {
            ApiHelper.Success(Response, new Dictionary<string, object>
            {
                { "total_registration_rows", 0 }, { "total_students", 0 },
                { "total_unique_courses", 0 },    { "total_programmes", 0 },
                { "pending_rows", 0 },            { "approved_rows", 0 }
            });
            return;
        }

        var sql   = new System.Text.StringBuilder(@"
            SELECT COUNT(*) AS total_registration_rows,
                   COUNT(DISTINCT cr.regno) AS total_students,
                   COUNT(DISTINCT UPPER(TRIM(cr.courseID))) AS total_unique_courses,
                   COUNT(DISTINCT IFNULL(s.progid,'')) AS total_programmes,
                   SUM(CASE WHEN UPPER(IFNULL(cr.course_status,''))='PENDING'  THEN 1 ELSE 0 END) AS pending_rows,
                   SUM(CASE WHEN UPPER(IFNULL(cr.course_status,''))='APPROVED' THEN 1 ELSE 0 END) AS approved_rows
            FROM campus_dynamics_portal.acad_course_registration cr
            LEFT JOIN acad_student s ON s.regno = cr.regno
            WHERE 1=1");
        var parms = new List<MySqlParameter>();

        if (!string.IsNullOrEmpty(acadYear))  { sql.Append(" AND cr.acad_year = @a");              parms.Add(new MySqlParameter("@a",  acadYear)); }
        if (semester > 0)                     { sql.Append(" AND cr.semester = @sem");             parms.Add(new MySqlParameter("@sem", semester)); }
        if (!string.IsNullOrEmpty(progCode))  { sql.Append(" AND s.progid = @p");                 parms.Add(new MySqlParameter("@p",  progCode)); }
        if (!string.IsNullOrEmpty(courseCode)){ sql.Append(" AND UPPER(TRIM(cr.courseID))=UPPER(@cc)"); parms.Add(new MySqlParameter("@cc", courseCode)); }
        AppendCourseFilter(sql, parms, myCodes, "UPPER(TRIM(cr.courseID))");

        DataTable dt = ApiHelper.Query(sql.ToString(), parms.ToArray());
        DataRow row = dt.Rows.Count > 0 ? dt.Rows[0] : null;

        Func<DataRow, string, long> toNum = (r, col) => {
            if (r == null || r[col] == DBNull.Value) return 0;
            long v; long.TryParse(r[col].ToString(), out v); return v;
        };

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "total_registration_rows", toNum(row, "total_registration_rows") },
            { "total_students",          toNum(row, "total_students") },
            { "total_unique_courses",    toNum(row, "total_unique_courses") },
            { "total_programmes",        toNum(row, "total_programmes") },
            { "pending_rows",            toNum(row, "pending_rows") },
            { "approved_rows",           toNum(row, "approved_rows") }
        });
    }

    private void HandleCourseRegList()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string empId      = GetLecturerEmpId(auth.UserId) ?? "";
        string acadYear   = ApiHelper.Param(Request, "acad_year", "").Trim();
        int    semester   = ApiHelper.ParamInt(Request, "semester", 0);
        string progCode   = ApiHelper.Param(Request, "programme_code", "").Trim();
        string courseCode = ApiHelper.Param(Request, "course_code", "").Trim();
        string search     = ApiHelper.Param(Request, "search", "").Trim();
        int    page       = Math.Max(1, ApiHelper.ParamInt(Request, "page", 1));
        int    size       = Math.Min(200, Math.Max(1, ApiHelper.ParamInt(Request, "size", 50)));
        int    offset     = (page - 1) * size;

        List<string> myCodes = GetLecturerCourseCodes(empId);
        if (myCodes.Count == 0)
        {
            ApiHelper.Success(Response, new Dictionary<string, object>
            { { "total", 0 }, { "page", page }, { "size", size }, { "pages", 1 }, { "students", new List<object>() } });
            return;
        }

        var sql   = new System.Text.StringBuilder(@"
            SELECT SQL_CALC_FOUND_ROWS
                   cr.regno,
                   COALESCE(NULLIF(TRIM(CONCAT(IFNULL(s.othername,''),' ',IFNULL(s.firstname,''))), ''), cr.regno) AS student_name,
                   COALESCE(NULLIF(TRIM(IFNULL(s.entryno,'')), ''), cr.regno) AS entry_no,
                   IFNULL(s.progid, '')   AS programme_code,
                   IFNULL(p.progname, '') AS programme_name,
                   GROUP_CONCAT(DISTINCT UPPER(TRIM(cr.courseID)) ORDER BY UPPER(TRIM(cr.courseID)) SEPARATOR ', ') AS enrolled_courses,
                   COUNT(DISTINCT UPPER(TRIM(cr.courseID))) AS course_count,
                   cr.acad_year, cr.semester,
                   IFNULL(MAX(reg.studyyear), 0) AS study_year,
                   SUM(CASE WHEN UPPER(IFNULL(cr.course_status,''))='PENDING' THEN 1 ELSE 0 END) AS pending_count
            FROM campus_dynamics_portal.acad_course_registration cr
            LEFT JOIN acad_student    s   ON s.regno     = cr.regno
            LEFT JOIN acad_programme  p   ON p.progcode  = s.progid
            LEFT JOIN acad_registration reg ON reg.regno = cr.regno
                 AND reg.acad_year = cr.acad_year AND reg.semester = cr.semester
            WHERE 1=1");
        var parms = new List<MySqlParameter>();

        if (!string.IsNullOrEmpty(acadYear))  { sql.Append(" AND cr.acad_year = @a");              parms.Add(new MySqlParameter("@a",  acadYear)); }
        if (semester > 0)                     { sql.Append(" AND cr.semester = @sem");             parms.Add(new MySqlParameter("@sem", semester)); }
        if (!string.IsNullOrEmpty(progCode))  { sql.Append(" AND s.progid = @p");                 parms.Add(new MySqlParameter("@p",  progCode)); }
        if (!string.IsNullOrEmpty(courseCode)){ sql.Append(" AND UPPER(TRIM(cr.courseID))=UPPER(@cc)"); parms.Add(new MySqlParameter("@cc", courseCode)); }
        if (!string.IsNullOrEmpty(search))
        {
            sql.Append(" AND (cr.regno LIKE @q OR IFNULL(s.entryno,'') LIKE @q OR TRIM(CONCAT(IFNULL(s.othername,''),' ',IFNULL(s.firstname,''))) LIKE @q OR IFNULL(p.progname,'') LIKE @q)");
            parms.Add(new MySqlParameter("@q", "%" + search + "%"));
        }
        AppendCourseFilter(sql, parms, myCodes, "UPPER(TRIM(cr.courseID))");

        sql.Append(" GROUP BY cr.regno, s.progid, p.progname, cr.acad_year, cr.semester, s.othername, s.firstname, s.entryno");
        sql.Append(" ORDER BY cr.acad_year DESC, cr.semester DESC, student_name ASC");
        sql.Append(" LIMIT @off, @sz");
        parms.Add(new MySqlParameter("@off", offset));
        parms.Add(new MySqlParameter("@sz",  size));

        DataTable dt    = ApiHelper.Query(sql.ToString(), parms.ToArray());
        int       total = Convert.ToInt32(ApiHelper.Scalar("SELECT FOUND_ROWS()"));

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "total",    total },
            { "page",     page },
            { "size",     size },
            { "pages",    (int)Math.Ceiling(total / (double)size) },
            { "students", ApiHelper.TableToList(dt) }
        });
    }

    private void HandleCourseRegValidateStudent()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string regno = ApiHelper.Param(Request, "regno", "").Trim();
        if (string.IsNullOrEmpty(regno))
        {
            ApiHelper.Error(Response, "regno is required.", "VALIDATION_ERROR");
            return;
        }

        // Student info
        DataTable infodt = ApiHelper.Query(
            @"SELECT NULLIF(TRIM(CONCAT(IFNULL(s.othername,''),' ',IFNULL(s.firstname,''))), '') AS stud_name,
                     COALESCE(NULLIF(TRIM(IFNULL(s.entryno,'')), ''), s.regno) AS entry_no,
                     CONCAT(IFNULL(p.progcode,''), ' - ', IFNULL(p.progname,'')) AS prog_name,
                     IFNULL(p.progcode,'') AS prog_code
              FROM acad_student s
              LEFT JOIN acad_programme p ON p.progcode = s.progid
              WHERE s.regno = @r LIMIT 1",
            new MySqlParameter("@r", regno));

        if (infodt.Rows.Count == 0)
        {
            ApiHelper.Error(Response, "Student not found.", "NOT_FOUND");
            return;
        }

        DataRow info = infodt.Rows[0];

        // Registration options (semester enrolments from acad_registration)
        DataTable regDt = ApiHelper.Query(
            @"SELECT r.ID AS registration_id, r.regno,
                     IFNULL(r.acad_year, '') AS acad_year,
                     IFNULL(r.semester, 0)   AS semester,
                     IFNULL(r.studyyear, 0)  AS study_year,
                     IFNULL(r.regstatus, '') AS reg_status,
                     IFNULL(s.progid, '')    AS prog_id,
                     IFNULL(p.progname, '')  AS programme_name
              FROM acad_registration r
              LEFT JOIN acad_student   s ON s.regno    = r.regno
              LEFT JOIN acad_programme p ON p.progcode = s.progid
              WHERE r.regno = @r
              ORDER BY r.acad_year DESC, r.semester DESC, r.ID DESC",
            new MySqlParameter("@r", regno));

        var registrations = new List<Dictionary<string, object>>();
        foreach (DataRow r in regDt.Rows)
        {
            int sem = r["semester"] != DBNull.Value ? Convert.ToInt32(r["semester"]) : 0;
            int yr  = r["study_year"] != DBNull.Value ? Convert.ToInt32(r["study_year"]) : 0;
            string ay  = Convert.ToString(r["acad_year"]).Trim();
            string rst = Convert.ToString(r["reg_status"]).Trim();
            string label = ay + " — Sem " + sem + (yr > 0 ? ", Year " + yr : "") + (string.IsNullOrEmpty(rst) ? "" : " [" + rst + "]");

            registrations.Add(new Dictionary<string, object>
            {
                { "registration_id", Convert.ToInt32(r["registration_id"]) },
                { "acad_year",       ay },
                { "semester",        sem },
                { "study_year",      yr },
                { "reg_status",      rst },
                { "programme_name",  Convert.ToString(r["programme_name"]) },
                { "label",           label }
            });
        }

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "regno",         regno },
            { "student_name",  Convert.ToString(info["stud_name"]) },
            { "entry_no",      Convert.ToString(info["entry_no"]) },
            { "programme",     Convert.ToString(info["prog_name"]) },
            { "programme_code",Convert.ToString(info["prog_code"]) },
            { "registrations", registrations }
        });
    }

    private void HandleCourseRegEnroll()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        EnsureCourseRegSchema();

        string empIdStr = GetLecturerEmpId(auth.UserId);
        if (string.IsNullOrEmpty(empIdStr))
        {
            ApiHelper.Error(Response, "Staff profile not found. Cannot process enrollment.", "NOT_FOUND");
            return;
        }
        int myEmpId = int.Parse(empIdStr);

        string regno      = ApiHelper.Param(Request, "regno",     "").Trim();
        string courseId   = ApiHelper.Param(Request, "course_id", "").Trim().ToUpperInvariant();
        int    regId      = ApiHelper.ParamInt(Request, "registration_id", 0);
        string acadYear   = ApiHelper.Param(Request, "acad_year", "").Trim();
        int    semester   = ApiHelper.ParamInt(Request, "semester", 0);

        if (string.IsNullOrEmpty(regno) || string.IsNullOrEmpty(courseId))
        {
            ApiHelper.Error(Response, "regno and course_id are required.", "VALIDATION_ERROR");
            return;
        }

        // If registration_id supplied, look it up; otherwise require acad_year + semester
        string progId = "";
        if (regId > 0)
        {
            DataTable regDt = ApiHelper.Query(
                @"SELECT r.regno, IFNULL(r.acad_year,'') AS acad_year,
                         IFNULL(r.semester,0) AS semester, IFNULL(s.progid,'') AS prog_id
                  FROM acad_registration r
                  LEFT JOIN acad_student s ON s.regno = r.regno
                  WHERE r.ID = @id LIMIT 1",
                new MySqlParameter("@id", regId));

            if (regDt.Rows.Count == 0) { ApiHelper.Error(Response, "Registration record not found.", "NOT_FOUND"); return; }
            DataRow reg = regDt.Rows[0];
            // Allow override of regno by the registration record for safety
            regno    = Convert.ToString(reg["regno"]).Trim();
            acadYear = Convert.ToString(reg["acad_year"]).Trim();
            semester = reg["semester"] != DBNull.Value ? Convert.ToInt32(reg["semester"]) : 0;
            progId   = Convert.ToString(reg["prog_id"]).Trim();
        }
        else
        {
            if (string.IsNullOrEmpty(acadYear) || semester < 1 || semester > 3)
            {
                ApiHelper.Error(Response, "Either registration_id OR (acad_year + semester) must be provided.", "VALIDATION_ERROR");
                return;
            }
            // Look up progId from student
            object pv = ApiHelper.Scalar("SELECT IFNULL(progid,'') FROM acad_student WHERE regno=@r LIMIT 1",
                new MySqlParameter("@r", regno));
            progId = pv != null && pv != DBNull.Value ? pv.ToString().Trim() : "";
        }

        if (string.IsNullOrEmpty(acadYear) || semester < 1 || semester > 3)
        {
            ApiHelper.Error(Response, "Valid acad_year and semester (1-3) are required.", "VALIDATION_ERROR");
            return;
        }

        // 1. Verify lecturer owns this course for this semester
        int owned = Convert.ToInt32(ApiHelper.Scalar(
            "SELECT COUNT(*) FROM acad_programmecourses " +
            "WHERE IFNULL(lecturer_id,0)=@sid AND UPPER(IFNULL(is_lecturere_assigned,'NO'))='YES' " +
            "  AND course_code=@cid AND IFNULL(semester,0)=@sem",
            new MySqlParameter("@sid", myEmpId),
            new MySqlParameter("@cid", courseId),
            new MySqlParameter("@sem", semester)));
        if (owned <= 0)
        {
            ApiHelper.Error(Response, "You can only enroll students to your allocated course(s) for the selected semester.", "ACCESS_DENIED");
            return;
        }

        // 2. Verify course exists
        int courseExists = Convert.ToInt32(ApiHelper.Scalar(
            "SELECT COUNT(*) FROM acad_course WHERE courseID=@c",
            new MySqlParameter("@c", courseId)));
        if (courseExists <= 0)
        {
            ApiHelper.Error(Response, "Course not found: " + courseId + ".", "NOT_FOUND");
            return;
        }

        // 3. Duplicate check
        int dup = Convert.ToInt32(ApiHelper.Scalar(
            "SELECT COUNT(*) FROM campus_dynamics_portal.acad_course_registration " +
            "WHERE regno=@r AND courseID=@c AND acad_year=@a AND semester=@s",
            new MySqlParameter("@r", regno),
            new MySqlParameter("@c", courseId),
            new MySqlParameter("@a", acadYear),
            new MySqlParameter("@s", semester)));
        if (dup > 0)
        {
            ApiHelper.Error(Response, "Student is already enrolled for this course in the selected period.", "DUPLICATE");
            return;
        }

        // 4. Insert
        string insertSql;
        var ip = new List<MySqlParameter>();
        ip.Add(new MySqlParameter("@r", regno));
        ip.Add(new MySqlParameter("@c", courseId));
        ip.Add(new MySqlParameter("@p", progId));
        ip.Add(new MySqlParameter("@a", acadYear));
        ip.Add(new MySqlParameter("@s", semester));

        if (_crHasCreatedDate && _crHasStudSession)
        {
            insertSql = "INSERT INTO campus_dynamics_portal.acad_course_registration (regno,courseID,prog_id,acad_year,semester,course_status,stud_session,created_date) VALUES(@r,@c,@p,@a,@s,'REGULAR',@ss,NOW())";
            ip.Add(new MySqlParameter("@ss", "Day"));
        }
        else if (_crHasCreatedDate)
        {
            insertSql = "INSERT INTO campus_dynamics_portal.acad_course_registration (regno,courseID,prog_id,acad_year,semester,course_status,created_date) VALUES(@r,@c,@p,@a,@s,'REGULAR',NOW())";
        }
        else if (_crHasStudSession)
        {
            insertSql = "INSERT INTO campus_dynamics_portal.acad_course_registration (regno,courseID,prog_id,acad_year,semester,course_status,stud_session) VALUES(@r,@c,@p,@a,@s,'REGULAR',@ss)";
            ip.Add(new MySqlParameter("@ss", "Day"));
        }
        else
        {
            insertSql = "INSERT INTO campus_dynamics_portal.acad_course_registration (regno,courseID,prog_id,acad_year,semester,course_status) VALUES(@r,@c,@p,@a,@s,'REGULAR')";
        }

        ApiHelper.Execute(insertSql, ip.ToArray());

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "message",   "Student enrolled successfully." },
            { "regno",     regno },
            { "course_id", courseId },
            { "acad_year", acadYear },
            { "semester",  semester }
        });
    }

    private void HandleCourseRegStudentCourses()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string regno    = ApiHelper.Param(Request, "regno", "").Trim();
        string acadYear = ApiHelper.Param(Request, "acad_year", "").Trim();
        int    semester = ApiHelper.ParamInt(Request, "semester", 0);

        if (string.IsNullOrEmpty(regno))
        {
            ApiHelper.Error(Response, "regno is required.", "VALIDATION_ERROR");
            return;
        }

        DataTable dt = ApiHelper.Query(
            @"SELECT UPPER(TRIM(cr.courseID)) AS course_code,
                     IFNULL(c.courseName, cr.courseID) AS course_name,
                     IFNULL(c.CreditUnit, 0)          AS credit_units,
                     IFNULL(cr.course_status, '')      AS status,
                     cr.acad_year, cr.semester
              FROM campus_dynamics_portal.acad_course_registration cr
              LEFT JOIN acad_course c ON c.courseID = cr.courseID
              WHERE cr.regno = @r
                AND (@a = '' OR cr.acad_year = @a)
                AND (@sem = 0 OR cr.semester  = @sem)
              ORDER BY cr.acad_year DESC, cr.semester DESC, c.courseName ASC",
            new MySqlParameter("@r",   regno),
            new MySqlParameter("@a",   acadYear),
            new MySqlParameter("@sem", semester));

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "regno",   regno },
            { "total",   dt.Rows.Count },
            { "courses", ApiHelper.TableToList(dt) }
        });
    }

    private void HandleCourseRegPopularity()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string empId      = GetLecturerEmpId(auth.UserId) ?? "";
        string acadYear   = ApiHelper.Param(Request, "acad_year", "").Trim();
        int    semester   = ApiHelper.ParamInt(Request, "semester", 0);
        string progCode   = ApiHelper.Param(Request, "programme_code", "").Trim();
        int    top        = Math.Min(100, Math.Max(1, ApiHelper.ParamInt(Request, "top", 20)));

        List<string> myCodes = GetLecturerCourseCodes(empId);
        if (myCodes.Count == 0)
        {
            ApiHelper.Success(Response, new Dictionary<string, object> { { "total", 0 }, { "courses", new List<object>() } });
            return;
        }

        var sql   = new System.Text.StringBuilder(@"
            SELECT UPPER(TRIM(cr.courseID)) AS course_code,
                   IFNULL(c.courseName, cr.courseID) AS course_name,
                   COUNT(*) AS registration_count,
                   COUNT(DISTINCT cr.regno) AS student_count
            FROM campus_dynamics_portal.acad_course_registration cr
            LEFT JOIN acad_course  c ON c.courseID = cr.courseID
            LEFT JOIN acad_student s ON s.regno    = cr.regno
            WHERE 1=1");
        var parms = new List<MySqlParameter>();

        if (!string.IsNullOrEmpty(acadYear)) { sql.Append(" AND cr.acad_year = @a");  parms.Add(new MySqlParameter("@a",  acadYear)); }
        if (semester > 0)                    { sql.Append(" AND cr.semester = @sem"); parms.Add(new MySqlParameter("@sem", semester)); }
        if (!string.IsNullOrEmpty(progCode)) { sql.Append(" AND s.progid = @p");      parms.Add(new MySqlParameter("@p",  progCode)); }
        AppendCourseFilter(sql, parms, myCodes, "UPPER(TRIM(cr.courseID))");

        sql.Append(" GROUP BY UPPER(TRIM(cr.courseID)), IFNULL(c.courseName,cr.courseID)");
        sql.Append(" ORDER BY registration_count DESC, student_count DESC, course_name ASC");
        sql.Append(" LIMIT @top");
        parms.Add(new MySqlParameter("@top", top));

        DataTable dt = ApiHelper.Query(sql.ToString(), parms.ToArray());
        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "total",   dt.Rows.Count },
            { "courses", ApiHelper.TableToList(dt) }
        });
    }

    private void HandleClassList()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;

        if (auth.UserType != "staff")
        {
            ApiHelper.Error(Response, "This endpoint is for staff members only.", "ACCESS_DENIED");
            return;
        }

        string courseId = ApiHelper.RequireParam(Request, Response, "course_id");
        if (courseId == null) return;
        string acadYear = ApiHelper.RequireParam(Request, Response, "acad_year");
        if (acadYear == null) return;
        int semester    = ApiHelper.ParamInt(Request, "semester", 1);
        string progid   = ApiHelper.Param(Request, "progid", ""); // optional programme filter

        var parms = new List<MySqlParameter>();
        parms.Add(new MySqlParameter("@course", courseId));
        parms.Add(new MySqlParameter("@acad",   acadYear));
        parms.Add(new MySqlParameter("@sem",    semester));

        string progFilter = "";
        if (!string.IsNullOrEmpty(progid))
        {
            progFilter = " AND (TRIM(IFNULL(cr.prog_id,'')) = @prog OR TRIM(IFNULL(s.progid,'')) = @prog)";
            parms.Add(new MySqlParameter("@prog", progid));
        }

        DataTable dt = ApiHelper.Query(
            @"SELECT cr.id AS registration_id,
                     TRIM(cr.regno) AS regno,
                     COALESCE(NULLIF(TRIM(IFNULL(s.entryno,'')),'' ), cr.regno) AS entry_no,
                     TRIM(COALESCE(s.firstname,''))  AS firstname,
                     TRIM(COALESCE(s.othername,''))  AS othername,
                     CONCAT(TRIM(COALESCE(s.firstname,'')), ' ', TRIM(COALESCE(s.othername,''))) AS student_name,
                     s.gender,
                     COALESCE(s.studsesion,'Day') AS session,
                     cr.prog_id AS programme_code,
                     cr.provisional_course_work_marks AS cw_marks,
                     cr.provisional_exam_marks         AS exam_marks,
                     cr.provisional_total_marks        AS total_marks,
                     COALESCE(cr.provisional_marks_status,'not_entered') AS mark_status,
                     CASE WHEN cr.provisional_course_work_marks IS NOT NULL
                               AND cr.provisional_exam_marks IS NOT NULL
                               AND COALESCE(cr.provisional_marks_status,'pending') NOT IN ('published')
                          THEN 1 ELSE 0 END AS ready_to_publish
              FROM campus_dynamics_portal.acad_course_registration cr
              LEFT JOIN acad_student s ON TRIM(s.regno) = TRIM(cr.regno)
              WHERE cr.courseID = @course AND cr.acad_year = @acad AND cr.semester = @sem
              " + progFilter + @"
              ORDER BY s.firstname, s.othername, TRIM(cr.regno)",
            parms.ToArray());

        // Get course and programme info
        DataTable courseInfo = ApiHelper.Query(
            "SELECT courseName AS course_name, COALESCE(CreditUnit,0) AS credit_units FROM acad_course WHERE courseID = @c LIMIT 1",
            new MySqlParameter("@c", courseId));

        // Add grades
        var students = ApiHelper.TableToList(dt);
        int cntEntered = 0, cntPending = 0, cntApproved = 0, cntRejected = 0, cntPublished = 0;
        foreach (var row in students)
        {
            object totObj = row.ContainsKey("total_marks") ? row["total_marks"] : null;
            if (totObj != null && !(totObj is DBNull))
            {
                decimal tot = 0;
                if (decimal.TryParse(totObj.ToString(), out tot))
                    row["grade"] = ComputeProvisionalGrade((int)Math.Round(tot));
                else row["grade"] = null;
            }
            else row["grade"] = null;

            string ms = Convert.ToString(row["mark_status"]);
            if (ms == "pending")   cntPending++;
            else if (ms == "approved")  cntApproved++;
            else if (ms == "rejected")  cntRejected++;
            else if (ms == "published") cntPublished++;
            if (ms != "not_entered") cntEntered++;
        }

        var data = new Dictionary<string, object>
        {
            { "course_id",      courseId },
            { "course_name",    courseInfo.Rows.Count > 0 ? courseInfo.Rows[0]["course_name"].ToString() : courseId },
            { "credit_units",   courseInfo.Rows.Count > 0 ? courseInfo.Rows[0]["credit_units"]           : 0 },
            { "acad_year",      acadYear },
            { "semester",       semester },
            { "total_students", students.Count },
            { "marks_summary",  new Dictionary<string, object>
                {
                    { "entered",   cntEntered },
                    { "not_entered", students.Count - cntEntered },
                    { "pending",   cntPending },
                    { "approved",  cntApproved },
                    { "rejected",  cntRejected },
                    { "published", cntPublished }
                }
            },
            { "students", students }
        };

        ApiHelper.Success(Response, data);
    }

    private void HandleMarks()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;

        if (auth.UserType != "staff")
        {
            ApiHelper.Error(Response, "This endpoint is for staff members only.", "ACCESS_DENIED");
            return;
        }

        string courseId = ApiHelper.RequireParam(Request, Response, "course_id");
        if (courseId == null) return;
        string acad_year = ApiHelper.RequireParam(Request, Response, "acad_year");
        if (acad_year == null) return;
        int semester = ApiHelper.ParamInt(Request, "semester", 1);

        DataTable dt = ApiHelper.Query(
            @"SELECT r.regno, s.firstname, s.othername,
                     r.course_work, r.exam_total, r.score AS total, r.grade, r.gradept AS gp, r.result_comment AS remarks
              FROM acad_results r
              JOIN acad_student s ON r.regno = s.regno
              WHERE r.courseid = @course AND r.acad = @acad AND r.semester = @sem
              ORDER BY s.firstname, s.othername",
            new MySqlParameter("@course", courseId),
            new MySqlParameter("@acad", acad_year),
            new MySqlParameter("@sem", semester)
        );

        var data = new Dictionary<string, object>
        {
            { "course_id", courseId },
            { "acad_year", acad_year },
            { "semester", semester },
            { "total_students", dt.Rows.Count },
            { "marks", ApiHelper.TableToList(dt) }
        };

        ApiHelper.Success(Response, data);
    }

    private void HandleSubmitMarks()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;

        if (auth.UserType != "staff")
        {
            ApiHelper.Error(Response, "This endpoint is for staff members only.", "ACCESS_DENIED");
            return;
        }

        string courseId = ApiHelper.RequireParam(Request, Response, "course_id");
        if (courseId == null) return;
        string acad_year = ApiHelper.RequireParam(Request, Response, "acad_year");
        if (acad_year == null) return;
        int semester = ApiHelper.ParamInt(Request, "semester", 1);

        // Read marks data from request body
        string marksJson = ApiHelper.Param(Request, "marks", "");
        if (string.IsNullOrEmpty(marksJson))
        {
            // Try reading from request body
            using (var reader = new StreamReader(Request.InputStream))
            {
                marksJson = reader.ReadToEnd();
            }
        }

        if (string.IsNullOrEmpty(marksJson))
        {
            ApiHelper.Error(Response, "Missing marks data. Send JSON array: [{\"regno\":\"...\",\"coursework\":30,\"exam\":50}]", "MISSING_PARAM");
            return;
        }

        try
        {
            var serializer = new JavaScriptSerializer();
            var marksList = serializer.Deserialize<List<Dictionary<string, object>>>(marksJson);

            if (marksList == null || marksList.Count == 0)
            {
                ApiHelper.Error(Response, "Empty marks data.", "MISSING_PARAM");
                return;
            }

            // ROB-07: Fetch mark-sheet maxima for this course/semester to validate scores
            decimal maxCoursework = 50m; // default
            decimal maxExam       = 60m; // default
            try
            {
                DataTable msDef = ApiHelper.Query(
                    @"SELECT max_coursework, max_exam FROM acad_mark_sheets
                      WHERE course_id = @crs AND acad_year = @acad AND semester = @sem LIMIT 1",
                    new MySqlParameter("@crs", courseId),
                    new MySqlParameter("@acad", acad_year),
                    new MySqlParameter("@sem", semester)
                );
                if (msDef.Rows.Count > 0)
                {
                    decimal msCw, msEx;
                    if (decimal.TryParse(msDef.Rows[0]["max_coursework"].ToString(), out msCw) && msCw > 0) maxCoursework = msCw;
                    if (decimal.TryParse(msDef.Rows[0]["max_exam"].ToString(), out msEx) && msEx > 0) maxExam = msEx;
                }
            }
            catch { /* table may not exist; use defaults */ }

            int updated = 0;
            int inserted = 0;
            int errors = 0;
            var validationErrors = new List<string>();

            foreach (var mark in marksList)
            {
                try
                {
                    string regno = mark.ContainsKey("regno") ? mark["regno"].ToString() : "";
                    if (string.IsNullOrEmpty(regno)) continue;

                    // ROB-07: Validate scores are numeric and within range
                    decimal coursework = 0, exam = 0;
                    bool cwValid = true, exValid = true;

                    if (mark.ContainsKey("coursework"))
                        cwValid = decimal.TryParse(mark["coursework"].ToString(), out coursework);
                    if (mark.ContainsKey("exam"))
                        exValid = decimal.TryParse(mark["exam"].ToString(), out exam);

                    if (!cwValid || !exValid)
                    {
                        validationErrors.Add(regno + ": scores must be numeric");
                        errors++; continue;
                    }
                    if (coursework < 0 || exam < 0)
                    {
                        validationErrors.Add(regno + ": scores cannot be negative");
                        errors++; continue;
                    }
                    if (coursework > maxCoursework)
                    {
                        validationErrors.Add(regno + ": coursework " + coursework + " exceeds max " + maxCoursework);
                        errors++; continue;
                    }
                    if (exam > maxExam)
                    {
                        validationErrors.Add(regno + ": exam " + exam + " exceeds max " + maxExam);
                        errors++; continue;
                    }

                    decimal total = coursework + exam;
                    string grade = CalculateGrade(total);
                    double gp = CalculateGP(grade);
                    string remarks = total >= 50 ? "NP" : "PP"; // Normal Pass / Poor Performance

                    // Check if record exists
                    object existing = ApiHelper.Scalar(
                        "SELECT COUNT(*) FROM acad_results WHERE regno=@reg AND courseid=@crs AND acad=@acad AND semester=@sem",
                        new MySqlParameter("@reg", regno),
                        new MySqlParameter("@crs", courseId),
                        new MySqlParameter("@acad", acad_year),
                        new MySqlParameter("@sem", semester)
                    );

                    if (Convert.ToInt32(existing) > 0)
                    {
                        ApiHelper.Execute(
                            @"UPDATE acad_results SET course_work=@cw, exam_total=@ex, score=@tot, grade=@grd, gradept=@gp, result_comment=@rem
                              WHERE regno=@reg AND courseid=@crs AND acad=@acad AND semester=@sem",
                            new MySqlParameter("@cw", coursework),
                            new MySqlParameter("@ex", exam),
                            new MySqlParameter("@tot", total),
                            new MySqlParameter("@grd", grade),
                            new MySqlParameter("@gp", gp),
                            new MySqlParameter("@rem", remarks),
                            new MySqlParameter("@reg", regno),
                            new MySqlParameter("@crs", courseId),
                            new MySqlParameter("@acad", acad_year),
                            new MySqlParameter("@sem", semester)
                        );
                        updated++;
                    }
                    else
                    {
                        // Get student's programme info for insert
                        DataTable studDt = ApiHelper.Query(
                            @"SELECT s.progid, 
                                     COALESCE((SELECT MAX(r.studyyear) FROM acad_registration r WHERE r.regno = s.regno), 1) AS study_year
                              FROM acad_student s WHERE s.regno = @reg",
                            new MySqlParameter("@reg", regno)
                        );

                        string progcode = studDt.Rows.Count > 0 ? studDt.Rows[0]["progid"].ToString() : "";
                        string studyYear = studDt.Rows.Count > 0 ? studDt.Rows[0]["study_year"].ToString() : "1";

                        // Get course credit units
                        DataTable courseDt = ApiHelper.Query(
                            "SELECT CreditUnit FROM acad_course WHERE courseID=@crs",
                            new MySqlParameter("@crs", courseId)
                        );
                        string cu = courseDt.Rows.Count > 0 ? courseDt.Rows[0]["CreditUnit"].ToString() : "3";

                        ApiHelper.Execute(
                            @"INSERT INTO acad_results (regno, courseid, acad, semester, progid, studyyear, 
                              course_work, exam_total, score, grade, gradept, CreditUnits, result_comment)
                              VALUES (@reg, @crs, @acad, @sem, @prog, @yr, @cw, @ex, @tot, @grd, @gp, @cu, @rem)",
                            new MySqlParameter("@reg", regno),
                            new MySqlParameter("@crs", courseId),
                            new MySqlParameter("@acad", acad_year),
                            new MySqlParameter("@sem", semester),
                            new MySqlParameter("@prog", progcode),
                            new MySqlParameter("@yr", studyYear),
                            new MySqlParameter("@cw", coursework),
                            new MySqlParameter("@ex", exam),
                            new MySqlParameter("@tot", total),
                            new MySqlParameter("@grd", grade),
                            new MySqlParameter("@gp", gp),
                            new MySqlParameter("@cu", cu),
                            new MySqlParameter("@rem", remarks)
                        );
                        inserted++;
                    }
                }
                catch
                {
                    errors++;
                }
            }

            var resultData = new Dictionary<string, object>
            {
                { "updated", updated },
                { "inserted", inserted },
                { "errors", errors },
                { "total_processed", marksList.Count },
                { "max_coursework", maxCoursework },
                { "max_exam", maxExam }
            };
            if (validationErrors.Count > 0)
                resultData["validation_errors"] = validationErrors;

            string msg = errors > 0
                ? "Marks partially submitted — " + errors + " error(s). Check validation_errors."
                : "Marks submitted successfully";
            ApiHelper.Success(Response, resultData, msg);
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error processing marks: " + ex.Message, "SERVER_ERROR");
        }
    }

    private string CalculateGrade(decimal total)
    {
        if (total >= 80) return "A";
        if (total >= 75) return "B+";
        if (total >= 70) return "B";
        if (total >= 65) return "C+";
        if (total >= 60) return "C";
        if (total >= 55) return "D+";
        if (total >= 50) return "D";
        return "F";
    }

    private double CalculateGP(string grade)
    {
        switch (grade)
        {
            case "A":  return 5.0;
            case "B+": return 4.5;
            case "B":  return 4.0;
            case "C+": return 3.5;
            case "C":  return 3.0;
            case "D+": return 2.5;
            case "D":  return 2.0;
            default:   return 0;
        }
    }

    // MRU / NCHE grade scale (matches ProvisionalMarksController): 80 A · 75 B+ · 70 B · 65 C+ ·
    // 60 C · 55 D+ · 50 D · <50 F. No A-/B-/C-.
    private static string ComputeProvisionalGrade(int score)
    {
        if (score >= 80) return "A";
        if (score >= 75) return "B+";
        if (score >= 70) return "B";
        if (score >= 65) return "C+";
        if (score >= 60) return "C";
        if (score >= 55) return "D+";
        if (score >= 50) return "D";
        return "F";
    }

    /// <summary>
    /// Returns the entry-level mark sheet (from acad_examresults_faculty) for a specific
    /// course context. This includes entered marks (cw, test, exam), calculated totals,
    /// and grades — the data teachers work with in the Mark Entry page.
    /// </summary>
    private void HandleMarkSheet()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;

        if (auth.UserType != "staff")
        {
            ApiHelper.Error(Response, "This endpoint is for staff members only.", "ACCESS_DENIED");
            return;
        }

        string courseId = ApiHelper.RequireParam(Request, Response, "course_id");
        if (courseId == null) return;
        string progid = ApiHelper.RequireParam(Request, Response, "progid");
        if (progid == null) return;
        string acad_year = ApiHelper.RequireParam(Request, Response, "acad_year");
        if (acad_year == null) return;
        int semester = ApiHelper.ParamInt(Request, "semester", 1);
        int studyYear = ApiHelper.ParamInt(Request, "study_year", 1);
        int campusId = ApiHelper.ParamInt(Request, "campus_id", 1);
        string session = ApiHelper.Param(Request, "session", "Day");

        try
        {
            // Get mark ratios
            DataTable ratios = ApiHelper.Query(
                @"SELECT COALESCE(pcw, 0) AS cw_ratio, COALESCE(ptst, 0) AS test_ratio,
                         COALESCE(pexm, 0) AS exam_ratio, COALESCE(CreditUnit, 0) AS credit_units
                  FROM acad_programmecourses
                  WHERE course_code = @course AND progcode = @prog
                  LIMIT 1",
                new MySqlParameter("@course", courseId),
                new MySqlParameter("@prog", progid)
            );

            int cwRatio = 0, testRatio = 0, examRatio = 0, creditUnits = 0;
            if (ratios.Rows.Count > 0)
            {
                int.TryParse(ratios.Rows[0]["cw_ratio"].ToString(), out cwRatio);
                int.TryParse(ratios.Rows[0]["test_ratio"].ToString(), out testRatio);
                int.TryParse(ratios.Rows[0]["exam_ratio"].ToString(), out examRatio);
                int.TryParse(ratios.Rows[0]["credit_units"].ToString(), out creditUnits);
            }

            // Get student marks
            DataTable dt = ApiHelper.Query(
                @"SELECT ef.id AS entry_id, ef.regno,
                         TRIM(CONCAT(COALESCE(s.firstname,''), ' ', COALESCE(s.othername,''))) AS student_name,
                         COALESCE(ef.cw_mark_entered, 0) AS cw_entered,
                         COALESCE(ef.test_mark_entered, 0) AS test_entered,
                         COALESCE(ef.exam_mark_entered, 0) AS exam_entered,
                         COALESCE(ef.cw_mark, 0) AS cw_mark,
                         COALESCE(ef.test_mark, 0) AS test_mark,
                         COALESCE(ef.ex_mark, 0) AS exam_mark,
                         COALESCE(ef.total_mark, 0) AS total_mark,
                         COALESCE(ef.grade, '') AS grade
                  FROM acad_examresults_faculty ef
                  LEFT JOIN acad_student s ON s.regno = ef.regno
                  WHERE ef.course_id = @course AND ef.progid = @prog
                    AND ef.acad_year = @year AND ef.semester = @sem
                    AND ef.study_year = @sy AND ef.campus = @campus
                    AND ef.stud_session = @sess
                  ORDER BY s.firstname, s.othername, ef.regno",
                new MySqlParameter("@course", courseId),
                new MySqlParameter("@prog", progid),
                new MySqlParameter("@year", acad_year),
                new MySqlParameter("@sem", semester),
                new MySqlParameter("@sy", studyYear),
                new MySqlParameter("@campus", campusId),
                new MySqlParameter("@sess", session)
            );

            // Get workflow status
            DataTable statusDt = ApiHelper.Query(
                @"SELECT status, submitted_by, submitted_at, approved_by, approved_at, reject_reason
                  FROM acad_results_status
                  WHERE course_id = @course AND progid = @prog AND acadyear = @year
                    AND semester = @sem AND study_year = @sy AND campus_id = @campus
                    AND stud_session = @sess
                  LIMIT 1",
                new MySqlParameter("@course", courseId),
                new MySqlParameter("@prog", progid),
                new MySqlParameter("@year", acad_year),
                new MySqlParameter("@sem", semester),
                new MySqlParameter("@sy", studyYear),
                new MySqlParameter("@campus", campusId),
                new MySqlParameter("@sess", session)
            );

            string sheetStatus = "DRAFT";
            Dictionary<string, object> statusInfo = null;
            if (statusDt.Rows.Count > 0)
            {
                statusInfo = ApiHelper.FirstRowToDict(statusDt);
                sheetStatus = statusDt.Rows[0]["status"].ToString();
            }

            // Count marks entered
            int marksEntered = 0;
            foreach (DataRow row in dt.Rows)
            {
                decimal cw = 0, test = 0, exam = 0;
                decimal.TryParse(row["cw_entered"].ToString(), out cw);
                decimal.TryParse(row["test_entered"].ToString(), out test);
                decimal.TryParse(row["exam_entered"].ToString(), out exam);
                if (cw > 0 || test > 0 || exam > 0) marksEntered++;
            }

            var data = new Dictionary<string, object>
            {
                { "course_id", courseId },
                { "programme_code", progid },
                { "acad_year", acad_year },
                { "semester", semester },
                { "study_year", studyYear },
                { "campus_id", campusId },
                { "session", session },
                { "ratios", new Dictionary<string, object>
                    {
                        { "coursework", cwRatio },
                        { "test", testRatio },
                        { "exam", examRatio },
                        { "credit_units", creditUnits }
                    }
                },
                { "status", sheetStatus },
                { "status_detail", statusInfo },
                { "total_students", dt.Rows.Count },
                { "marks_entered", marksEntered },
                { "students", ApiHelper.TableToList(dt) }
            };

            ApiHelper.Success(Response, data);
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error fetching mark sheet: " + ex.Message, "SERVER_ERROR");
        }
    }

    /// <summary>
    /// Saves entry-level marks to acad_examresults_faculty. This is the workflow-aware
    /// counterpart to submit_marks (which writes directly to acad_results).
    /// Marks are saved as DRAFT — the teacher must call submit_for_approval separately.
    /// Expects JSON array: [{"entry_id":123, "cw_entered":25, "test_entered":10, "exam_entered":40}]
    /// </summary>
    private void HandleSaveEntryMarks()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;

        if (auth.UserType != "staff")
        {
            ApiHelper.Error(Response, "This endpoint is for staff members only.", "ACCESS_DENIED");
            return;
        }

        string marksJson = ApiHelper.Param(Request, "marks", "");
        if (string.IsNullOrEmpty(marksJson))
        {
            using (var reader = new StreamReader(Request.InputStream))
            {
                marksJson = reader.ReadToEnd();
            }
        }

        if (string.IsNullOrEmpty(marksJson))
        {
            ApiHelper.Error(Response, "Missing marks data. Send JSON array: [{\"entry_id\":123, \"cw_entered\":25, \"test_entered\":10, \"exam_entered\":40}]", "MISSING_PARAM");
            return;
        }

        try
        {
            var serializer = new JavaScriptSerializer();
            var marksList = serializer.Deserialize<List<Dictionary<string, object>>>(marksJson);

            if (marksList == null || marksList.Count == 0)
            {
                ApiHelper.Error(Response, "Empty marks data.", "MISSING_PARAM");
                return;
            }

            if (marksList.Count > 200)
            {
                ApiHelper.Error(Response, "Maximum 200 marks per request.", "VALIDATION_ERROR");
                return;
            }

            int updated = 0;
            int errors = 0;

            foreach (var mark in marksList)
            {
                try
                {
                    int entryId = 0;
                    if (mark.ContainsKey("entry_id"))
                        int.TryParse(mark["entry_id"].ToString(), out entryId);
                    if (entryId <= 0) { errors++; continue; }

                    decimal cwEntered = 0, testEntered = 0, examEntered = 0;
                    if (mark.ContainsKey("cw_entered"))
                        decimal.TryParse(mark["cw_entered"].ToString(), out cwEntered);
                    if (mark.ContainsKey("test_entered"))
                        decimal.TryParse(mark["test_entered"].ToString(), out testEntered);
                    if (mark.ContainsKey("exam_entered"))
                        decimal.TryParse(mark["exam_entered"].ToString(), out examEntered);

                    // Confirm the row exists before writing to it.
                    //
                    // This used to also fetch pc.pcw / pc.ptst / pc.pexm from acad_programmecourses
                    // to weight the marks. Those are not columns on that table, so the query failed
                    // and every entry in the batch fell into "errors++; continue;" - the endpoint
                    // could never save anything. The weighting is gone rather than repaired: MRU
                    // enters coursework out of 40 and the exam out of 60 and sums them plainly,
                    // which is what the old code did anyway whenever a ratio came back 0.
                    DataTable infoDt = ApiHelper.Query(
                        @"SELECT ef.course_id, ef.progid
                          FROM acad_examresults_faculty ef
                          WHERE ef.id = @id LIMIT 1",
                        new MySqlParameter("@id", entryId)
                    );

                    if (infoDt.Rows.Count == 0) { errors++; continue; }

                    decimal cwMark = cwEntered;
                    decimal testMark = testEntered;
                    decimal examMark = examEntered;
                    decimal totalMark = cwMark + testMark + examMark;
                    string grade = CalculateGrade(totalMark);

                    ApiHelper.Execute(
                        @"UPDATE acad_examresults_faculty 
                          SET cw_mark_entered = @cwE, test_mark_entered = @testE, exam_mark_entered = @examE,
                              cw_mark = @cwM, test_mark = @testM, ex_mark = @examM,
                              total_mark = @total, grade = @grade
                          WHERE id = @id",
                        new MySqlParameter("@cwE", cwEntered),
                        new MySqlParameter("@testE", testEntered),
                        new MySqlParameter("@examE", examEntered),
                        new MySqlParameter("@cwM", cwMark),
                        new MySqlParameter("@testM", testMark),
                        new MySqlParameter("@examM", examMark),
                        new MySqlParameter("@total", totalMark),
                        new MySqlParameter("@grade", grade),
                        new MySqlParameter("@id", entryId)
                    );
                    updated++;
                }
                catch
                {
                    errors++;
                }
            }

            ApiHelper.Success(Response, new Dictionary<string, object>
            {
                { "updated", updated },
                { "errors", errors },
                { "total_processed", marksList.Count }
            }, "Entry marks saved successfully");
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error saving entry marks: " + ex.Message, "SERVER_ERROR");
        }
    }

    /// <summary>
    /// Submits a mark sheet for dean approval. Changes the sheet status from DRAFT
    /// to SUBMITTED in the acad_results_status table. Requires full course context.
    /// </summary>
    private void HandleSubmitForApproval()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;

        if (auth.UserType != "staff")
        {
            ApiHelper.Error(Response, "This endpoint is for staff members only.", "ACCESS_DENIED");
            return;
        }

        string courseId = ApiHelper.RequireParam(Request, Response, "course_id");
        if (courseId == null) return;
        string progid = ApiHelper.RequireParam(Request, Response, "progid");
        if (progid == null) return;
        string acad_year = ApiHelper.RequireParam(Request, Response, "acad_year");
        if (acad_year == null) return;
        int semester = ApiHelper.ParamInt(Request, "semester", 1);
        int studyYear = ApiHelper.ParamInt(Request, "study_year", 1);
        int campusId = ApiHelper.ParamInt(Request, "campus_id", 1);
        string session = ApiHelper.Param(Request, "session", "Day");

        try
        {
            // Check current status — only DRAFT can be submitted
            DataTable statusDt = ApiHelper.Query(
                @"SELECT status FROM acad_results_status
                  WHERE course_id = @course AND progid = @prog AND acadyear = @year
                    AND semester = @sem AND study_year = @sy AND campus_id = @campus
                    AND stud_session = @sess LIMIT 1",
                new MySqlParameter("@course", courseId),
                new MySqlParameter("@prog", progid),
                new MySqlParameter("@year", acad_year),
                new MySqlParameter("@sem", semester),
                new MySqlParameter("@sy", studyYear),
                new MySqlParameter("@campus", campusId),
                new MySqlParameter("@sess", session)
            );

            string currentStatus = "DRAFT";
            if (statusDt.Rows.Count > 0)
                currentStatus = statusDt.Rows[0]["status"].ToString();

            if (currentStatus != "DRAFT")
            {
                ApiHelper.Error(Response, String.Format("Sheet cannot be submitted. Current status is {0}. Only DRAFT sheets can be submitted.", currentStatus), "BUSINESS_ERROR");
                return;
            }

            // Upsert status to SUBMITTED
            ApiHelper.Execute(
                @"INSERT INTO acad_results_status 
                    (course_id, progid, acadyear, semester, study_year, campus_id, stud_session, status, submitted_by, submitted_at)
                  VALUES (@course, @prog, @year, @sem, @sy, @campus, @sess, 'SUBMITTED', @user, NOW())
                  ON DUPLICATE KEY UPDATE
                    status = 'SUBMITTED', submitted_by = @user, submitted_at = NOW(), reject_reason = NULL",
                new MySqlParameter("@course", courseId),
                new MySqlParameter("@prog", progid),
                new MySqlParameter("@year", acad_year),
                new MySqlParameter("@sem", semester),
                new MySqlParameter("@sy", studyYear),
                new MySqlParameter("@campus", campusId),
                new MySqlParameter("@sess", session),
                new MySqlParameter("@user", auth.UserId)
            );

            ApiHelper.Success(Response, new Dictionary<string, object>
            {
                { "course_id", courseId },
                { "programme_code", progid },
                { "acad_year", acad_year },
                { "semester", semester },
                { "new_status", "SUBMITTED" },
                { "submitted_by", auth.UserId },
                { "submitted_at", DateTime.Now.ToString("o") }
            }, "Mark sheet submitted for dean approval");
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error submitting for approval: " + ex.Message, "SERVER_ERROR");
        }
    }

    /// <summary>
    /// Returns the workflow status of a mark sheet from acad_results_status.
    /// Status values: DRAFT, SUBMITTED, DEAN_APPROVED, PROVISIONAL_PUBLISHED, FINAL_PUBLISHED.
    /// </summary>
    /// <summary>
    /// Mark submission deadlines, with how long is left.
    ///
    /// This endpoint was documented as live and never existed — a call to it returned
    /// INVALID_ACTION while the docs promised it. The data was there the whole time
    /// (acad_deadlines), so it is built rather than the promise withdrawn.
    ///
    /// Deadlines are per campus, academic year, semester and study system. A row with no campus
    /// or no study system applies to all of them, so those match either exactly or as a wildcard
    /// rather than being dropped.
    /// </summary>
    private void HandleDeadlines()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff")
        {
            ApiHelper.Error(Response, "This endpoint is for staff members only.", "ACCESS_DENIED");
            return;
        }

        string acadYear = ApiHelper.Param(Request, "acad_year", "");
        int semester = ApiHelper.ParamInt(Request, "semester", 0);
        string studySystem = ApiHelper.Param(Request, "study_system", "");
        int campusId = ApiHelper.ParamInt(Request, "campus_id", 0);
        string type = ApiHelper.Param(Request, "deadline_type", "").Trim().ToUpperInvariant();

        string where = "WHERE IFNULL(is_active,1) = 1";
        var ps = new List<MySqlParameter>();
        if (!string.IsNullOrEmpty(acadYear)) { where += " AND AcademicYear = @ay"; ps.Add(new MySqlParameter("@ay", acadYear)); }
        if (semester > 0) { where += " AND Semester = @sem"; ps.Add(new MySqlParameter("@sem", semester)); }
        if (campusId > 0) { where += " AND (CampusID = @cid OR CampusID IS NULL OR CampusID = 0)"; ps.Add(new MySqlParameter("@cid", campusId)); }
        if (!string.IsNullOrEmpty(studySystem)) { where += " AND (StudySystem = @ss OR IFNULL(StudySystem,'') = '')"; ps.Add(new MySqlParameter("@ss", studySystem)); }
        if (type != "") { where += " AND deadline_type = @dt"; ps.Add(new MySqlParameter("@dt", type)); }

        DataTable dt = ApiHelper.Query(
            "SELECT ID AS deadline_id, IFNULL(ActivityName,'') AS activity, IFNULL(CampusID,0) AS campus_id, " +
            "  DATE_FORMAT(Deadline,'%Y-%m-%d') AS deadline_date, IFNULL(EndTime,'') AS end_time, " +
            "  IFNULL(AcademicYear,'') AS acad_year, IFNULL(Semester,0) AS semester, " +
            "  IFNULL(StudySystem,'') AS study_system, IFNULL(deadline_type,'OTHER') AS deadline_type " +
            "FROM acad_deadlines " + where + " ORDER BY Deadline, ID", ps.ToArray());

        var items = new List<object>();
        int past = 0, upcoming = 0;
        foreach (DataRow r in dt.Rows)
        {
            string dateStr = Convert.ToString(r["deadline_date"]);
            string endTime = Convert.ToString(r["end_time"]).Trim();

            // The date and the time of day are stored apart. Both matter: a deadline of "today"
            // has not passed at nine in the morning if it closes at half past ten at night.
            DateTime due;
            if (!DateTime.TryParse(dateStr + (endTime == "" ? " 23:59:59" : " " + endTime), out due))
                if (!DateTime.TryParse(dateStr, out due)) continue;

            TimeSpan left = due - DateTime.Now;
            bool isPast = left.TotalSeconds <= 0;
            if (isPast) past++; else upcoming++;

            items.Add(new
            {
                deadline_id = Convert.ToInt32(r["deadline_id"]),
                activity = Convert.ToString(r["activity"]),
                deadline_type = Convert.ToString(r["deadline_type"]),
                acad_year = Convert.ToString(r["acad_year"]),
                semester = Convert.ToInt32(r["semester"]),
                study_system = Convert.ToString(r["study_system"]),
                campus_id = Convert.ToInt32(r["campus_id"]),
                deadline_date = dateStr,
                end_time = endTime,
                due_at = due.ToString("yyyy-MM-dd HH:mm"),
                is_past_due = isPast,
                hours_remaining = isPast ? 0 : Math.Round(left.TotalHours, 1),
                days_remaining = isPast ? 0 : (int)Math.Floor(left.TotalDays)
            });
        }

        ApiHelper.Success(Response, new
        {
            deadlines = items,
            total = items.Count,
            upcoming = upcoming,
            past_due = past
        }, "Mark submission deadlines");
    }

    private void HandleSheetStatus()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;

        if (auth.UserType != "staff")
        {
            ApiHelper.Error(Response, "This endpoint is for staff members only.", "ACCESS_DENIED");
            return;
        }

        string courseId = ApiHelper.RequireParam(Request, Response, "course_id");
        if (courseId == null) return;
        string progid = ApiHelper.RequireParam(Request, Response, "progid");
        if (progid == null) return;
        string acad_year = ApiHelper.RequireParam(Request, Response, "acad_year");
        if (acad_year == null) return;
        int semester = ApiHelper.ParamInt(Request, "semester", 1);
        int studyYear = ApiHelper.ParamInt(Request, "study_year", 1);
        int campusId = ApiHelper.ParamInt(Request, "campus_id", 1);
        string session = ApiHelper.Param(Request, "session", "Day");

        try
        {
            DataTable dt = ApiHelper.Query(
                @"SELECT rs.status, rs.submitted_by, 
                         DATE_FORMAT(rs.submitted_at, '%Y-%m-%d %H:%i') AS submitted_at,
                         rs.approved_by,
                         DATE_FORMAT(rs.approved_at, '%Y-%m-%d %H:%i') AS approved_at,
                         rs.published_by,
                         DATE_FORMAT(rs.published_at, '%Y-%m-%d %H:%i') AS published_at,
                         rs.reject_reason,
                         DATE_FORMAT(rs.updated_at, '%Y-%m-%d %H:%i') AS updated_at
                  FROM acad_results_status rs
                  WHERE rs.course_id = @course AND rs.progid = @prog AND rs.acadyear = @year
                    AND rs.semester = @sem AND rs.study_year = @sy AND rs.campus_id = @campus
                    AND rs.stud_session = @sess
                  LIMIT 1",
                new MySqlParameter("@course", courseId),
                new MySqlParameter("@prog", progid),
                new MySqlParameter("@year", acad_year),
                new MySqlParameter("@sem", semester),
                new MySqlParameter("@sy", studyYear),
                new MySqlParameter("@campus", campusId),
                new MySqlParameter("@sess", session)
            );

            if (dt.Rows.Count == 0)
            {
                ApiHelper.Success(Response, new Dictionary<string, object>
                {
                    { "status", "DRAFT" },
                    { "note", "No status record found — sheet is in draft state." }
                });
                return;
            }

            ApiHelper.Success(Response, ApiHelper.FirstRowToDict(dt));
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error fetching sheet status: " + ex.Message, "SERVER_ERROR");
        }
    }

    /// <summary>
    /// Returns all filter dropdown options for the provisional marks interface.
    /// Scoped to the authenticated teacher's assigned courses (triple-source auth).
    /// Covers: academic years, semesters, programmes, courses, statuses, page sizes.
    /// </summary>
    private void HandleFilterOptions()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string filterAcadYear = ApiHelper.Param(Request, "acad_year", "");
        int    filterSemester = ApiHelper.ParamInt(Request, "semester", 0);


        try
        {
            // ── 1. Academic years ──────────────────────────────────────────────
            var yearParms = new List<MySqlParameter>();
            var yearWhere = new System.Text.StringBuilder("WHERE cr.regno IS NOT NULL AND cr.acad_year IS NOT NULL AND cr.acad_year <> ''");
            if (filterSemester > 0) { yearWhere.Append(" AND cr.semester = @semFlt"); yearParms.Add(new MySqlParameter("@semFlt", filterSemester)); }

            DataTable dtYears = ApiHelper.Query(
                "SELECT DISTINCT cr.acad_year " +
                "FROM campus_dynamics_portal.acad_course_registration cr " +
                yearWhere + " ORDER BY cr.acad_year DESC LIMIT 20",
                yearParms.ToArray());

            var years = new List<Dictionary<string, object>>();
            foreach (DataRow r in dtYears.Rows)
            {
                string y = r["acad_year"].ToString().Trim();
                if (!string.IsNullOrEmpty(y))
                    years.Add(new Dictionary<string, object> { { "value", y }, { "label", y } });
            }

            // ── 2. Semesters (static) ──────────────────────────────────────────
            var semesters = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { { "value", 1 }, { "label", "Semester 1" } },
                new Dictionary<string, object> { { "value", 2 }, { "label", "Semester 2" } },
                new Dictionary<string, object> { { "value", 3 }, { "label", "Semester 3" } }
            };

            // ── 3. Programmes ──────────────────────────────────────────────────
            var progParms = new List<MySqlParameter>();
            var progWhere = new System.Text.StringBuilder("WHERE cr.regno IS NOT NULL");
            if (!string.IsNullOrEmpty(filterAcadYear)) { progWhere.Append(" AND cr.acad_year = @ayFlt"); progParms.Add(new MySqlParameter("@ayFlt", filterAcadYear)); }
            if (filterSemester > 0)                    { progWhere.Append(" AND cr.semester = @semFlt2"); progParms.Add(new MySqlParameter("@semFlt2", filterSemester)); }

            DataTable dtProgs = ApiHelper.Query(
                "SELECT DISTINCT p.progcode AS value, COALESCE(NULLIF(TRIM(p.progname),''), p.progcode) AS label " +
                "FROM acad_programme p " +
                "INNER JOIN campus_dynamics_portal.acad_course_registration cr ON cr.prog_id = p.progcode " +
                progWhere + " ORDER BY label",
                progParms.ToArray());

            var programmes = ApiHelper.TableToList(dtProgs);

            // ── 4. Courses from all registrations ────────────────────────────────
            var cParms = new List<MySqlParameter>();
            var cWhere = new System.Text.StringBuilder("WHERE cr.courseID IS NOT NULL AND TRIM(cr.courseID) <> ''");
            if (!string.IsNullOrEmpty(filterAcadYear)) { cWhere.Append(" AND cr.acad_year = @ay_c"); cParms.Add(new MySqlParameter("@ay_c", filterAcadYear)); }
            if (filterSemester > 0)                    { cWhere.Append(" AND cr.semester = @sem_c"); cParms.Add(new MySqlParameter("@sem_c", filterSemester)); }

            DataTable dtCourses = ApiHelper.Query(
                "SELECT DISTINCT cr.courseID AS value, COALESCE(c.courseName, cr.courseID) AS label, " +
                "cr.prog_id AS programme_code, NULL AS acad_year, NULL AS semester " +
                "FROM campus_dynamics_portal.acad_course_registration cr " +
                "LEFT JOIN acad_course c ON c.courseID = cr.courseID " +
                cWhere + " ORDER BY cr.courseID LIMIT 500",
                cParms.ToArray());

            var courses = ApiHelper.TableToList(dtCourses);

            // ── 5. Statuses (static) ───────────────────────────────────────────
            var statuses = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { { "value", "" },            { "label", "All Statuses" } },
                new Dictionary<string, object> { { "value", "not_entered" }, { "label", "Not Entered" } },
                new Dictionary<string, object> { { "value", "pending" },     { "label", "Pending Review" } },
                new Dictionary<string, object> { { "value", "approved" },    { "label", "Approved" } },
                new Dictionary<string, object> { { "value", "rejected" },    { "label", "Rejected" } },
                new Dictionary<string, object> { { "value", "published" },   { "label", "Published" } }
            };

            // ── 6. Page sizes (static) ─────────────────────────────────────────
            var pageSizes = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { { "value", 25 },  { "label", "25 per page" } },
                new Dictionary<string, object> { { "value", 50 },  { "label", "50 per page" } },
                new Dictionary<string, object> { { "value", 100 }, { "label", "100 per page" } },
                new Dictionary<string, object> { { "value", 200 }, { "label", "200 per page" } }
            };

            var data = new Dictionary<string, object>
            {
                { "years",       years },
                { "semesters",   semesters },
                { "programmes",  programmes },
                { "courses",     courses },
                { "statuses",    statuses },
                { "page_sizes",  pageSizes }
            };

            ApiHelper.Success(Response, data);
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error fetching filter options: " + ex.Message, "SERVER_ERROR");
        }
    }

    /// <summary>
    /// Returns the full catalogue of course codes and names (no assignment restriction).
    /// Any authenticated staff member can call this — admins and ICT staff who are not
    /// lecturers get empty lists from my_courses but still need to browse courses for
    /// manual mark entry. Supports ?q= keyword search and ?prog_code= filter.
    /// </summary>
    private void HandleAllCourses()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string q      = ApiHelper.Param(Request, "q",         "").Trim();
        string prog   = ApiHelper.Param(Request, "prog_code", "");
        int    page   = Math.Max(1, ApiHelper.ParamInt(Request, "page", 1));
        int    size   = Math.Min(200, Math.Max(1, ApiHelper.ParamInt(Request, "size", 50)));
        int    offset = (page - 1) * size;

        try
        {
            var where = new System.Text.StringBuilder(
                "WHERE TRIM(COALESCE(c.courseName,'')) <> '' AND c.courseID IS NOT NULL");
            var parms = new List<MySqlParameter>();

            if (!string.IsNullOrEmpty(q))
            {
                where.Append(" AND (c.courseID LIKE @q OR c.courseName LIKE @q)");
                parms.Add(new MySqlParameter("@q", "%" + q + "%"));
            }
            if (!string.IsNullOrEmpty(prog))
            {
                where.Append(" AND pc.progcode = @prog");
                parms.Add(new MySqlParameter("@prog", prog));
            }

            string fromClause =
                "FROM acad_course c " +
                "LEFT JOIN acad_programmecourses pc ON pc.course_code = c.courseID " +
                "LEFT JOIN acad_programme p ON p.progcode = pc.progcode";

            var countParms = new List<MySqlParameter>(parms);
            int total = Convert.ToInt32(ApiHelper.Scalar(
                "SELECT COUNT(*) " + fromClause + " " + where, countParms.ToArray()));

            parms.Add(new MySqlParameter("@lim", size));
            parms.Add(new MySqlParameter("@off", offset));

            DataTable dt = ApiHelper.Query(
                "SELECT c.courseID AS course_code, " +
                "       TRIM(c.courseName) AS course_name, " +
                "       COALESCE(pc.progcode, '') AS prog_code, " +
                "       COALESCE(NULLIF(TRIM(p.progname),''), pc.progcode, '') AS prog_name, " +
                "       COALESCE(c.CreditUnit, 0) AS credit_units " +
                fromClause + " " + where + " " +
                "ORDER BY c.courseID, pc.progcode " +
                "LIMIT @lim OFFSET @off",
                parms.ToArray());

            ApiHelper.Success(Response, new Dictionary<string, object>
            {
                { "total",   total },
                { "page",    page },
                { "size",    size },
                { "pages",   total > 0 ? (int)Math.Ceiling(total / (double)size) : 1 },
                { "courses", ApiHelper.TableToList(dt) }
            });
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error fetching courses: " + ex.Message, "SERVER_ERROR");
        }
    }

    /// <summary>
    /// ODEL: Lookup staff member by email address.
    /// Used by Moodle to find/verify staff/lecturer accounts.
    /// </summary>
    private void HandleLookup()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;

        string email = ApiHelper.RequireParam(Request, Response, "email");
        if (email == null) return;

        DataTable dt = ApiHelper.Query(
            @"SELECT e.empID, e.EMP_CODE AS emp_code, e.emp_name, e.usernames AS username,
                     e.emp_email AS email, e.emp_phone AS phone, e.EmpType AS emp_type,
                     e.emp_status AS status, e.emp_nationality AS nationality,
                     e.emp_qualifications AS qualifications,
                     d.dept_name AS department, d.ID AS department_id,
                     f.faculty_name AS faculty, f.fax_code AS faculty_code
              FROM hrm_employee e
              LEFT JOIN hrm_emp_contracts c ON e.empID = c.empID AND c.contractStatus = 'Active'
              LEFT JOIN hrm_departments d ON c.departmentID = d.ID
              LEFT JOIN acad_faculty f ON d.fax_code = f.fax_code
              WHERE LOWER(e.emp_email) = LOWER(@email)
              LIMIT 1",
            new MySqlParameter("@email", email)
        );

        if (dt.Rows.Count > 0)
        {
            var staff = ApiHelper.FirstRowToDict(dt);
            staff["photo_url"] = "/API/v2/staff.aspx?action=photo&emp_code=" + Server.UrlEncode(Convert.ToString(staff["emp_code"]));
            var data = new Dictionary<string, object>
            {
                { "found", true },
                { "data", staff }
            };
            ApiHelper.Success(Response, data, "Staff member found");
        }
        else
        {
            var data = new Dictionary<string, object>
            {
                { "found", false },
                { "data", null }
            };
            ApiHelper.Success(Response, data, "No staff member found with this email");
        }
    }

    /// <summary>
    /// ODEL: List all staff in a department with optional filters.
    /// Used by Moodle to sync department rosters.
    /// </summary>
    private void HandleByDepartment()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;

        string deptId = ApiHelper.RequireParam(Request, Response, "department_id");
        if (deptId == null) return;

        string role = ApiHelper.Param(Request, "role", "").ToLower();
        string status = ApiHelper.Param(Request, "status", "Active");

        StringBuilder where = new StringBuilder("c.departmentID = @dept AND c.contractStatus = @status");
        List<MySqlParameter> parms = new List<MySqlParameter>();
        parms.Add(new MySqlParameter("@dept", deptId));
        parms.Add(new MySqlParameter("@status", status));

        if (!String.IsNullOrEmpty(role))
        {
            where.Append(" AND LOWER(e.EmpType) = @role");
            parms.Add(new MySqlParameter("@role", role));
        }

        string sql = String.Format(
            @"SELECT e.empID, e.EMP_CODE AS emp_code, e.emp_name, e.usernames AS username,
                     e.emp_email AS email, e.emp_phone AS phone, e.EmpType AS emp_type,
                     e.emp_status AS status, e.emp_qualifications AS qualifications,
                     d.dept_name AS department, f.faculty_name AS faculty
              FROM hrm_employee e
              INNER JOIN hrm_emp_contracts c ON e.empID = c.empID
              LEFT JOIN hrm_departments d ON c.departmentID = d.ID
              LEFT JOIN acad_faculty f ON d.fax_code = f.fax_code
              WHERE {0}
              ORDER BY e.emp_name", where.ToString());

        DataTable dt = ApiHelper.Query(sql, parms.ToArray());
        var staffList = ApiHelper.TableToList(dt);

        // Get department info
        DataTable dtDept = ApiHelper.Query(
            @"SELECT d.ID, d.dept_name, f.faculty_name, f.fax_code AS faculty_code
              FROM hrm_departments d
              LEFT JOIN acad_faculty f ON d.fax_code = f.fax_code
              WHERE d.ID = @dept",
            new MySqlParameter("@dept", deptId)
        );

        var data = new Dictionary<string, object>
        {
            { "department_id", deptId },
            { "department_name", dtDept.Rows.Count > 0 ? Convert.ToString(dtDept.Rows[0]["dept_name"]) : "" },
            { "faculty", dtDept.Rows.Count > 0 ? Convert.ToString(dtDept.Rows[0]["faculty_name"]) : "" },
            { "filter_role", role },
            { "filter_status", status },
            { "total", staffList.Count },
            { "staff", staffList }
        };
        ApiHelper.Success(Response, data);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PROVISIONAL MARKS
    // ═══════════════════════════════════════════════════════════════════

    private string GetLecturerEmpId(string username)
    {
        DataTable dt = ApiHelper.Query(
            @"SELECT empID FROM hrm_employee
              WHERE (NULLIF(TRIM(IFNULL(usernames,'')),'-') IS NOT NULL AND UPPER(TRIM(usernames)) = UPPER(@u))
                 OR UPPER(TRIM(IFNULL(EMP_CODE,''))) = UPPER(@u2)
              LIMIT 1",
            new MySqlParameter("@u",  username),
            new MySqlParameter("@u2", username));
        return dt.Rows.Count > 0 ? dt.Rows[0]["empID"].ToString() : null;
    }

    private void HandleProvisionalMarksList()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        // Filters — trim all string params so whitespace-only is treated as "not provided"
        string acadYear  = ApiHelper.Param(Request, "acad_year",  "").Trim();
        int    semester  = ApiHelper.ParamInt(Request, "semester", 0);
        string prog      = ApiHelper.Param(Request, "prog",       "").Trim();
        string courseId  = ApiHelper.Param(Request, "course_id",  "").Trim();
        string status        = ApiHelper.Param(Request, "status",        "").Trim();
        string sq            = ApiHelper.Param(Request, "sq",            "").Trim();
        string studentRegno  = ApiHelper.Param(Request, "student_regno", "").Trim();
        int    ready         = ApiHelper.ParamInt(Request, "ready",      0);
        int    page      = Math.Max(1, ApiHelper.ParamInt(Request, "page", 1));
        int    size      = Math.Min(200, Math.Max(1, ApiHelper.ParamInt(Request, "size", 50)));
        int    offset    = (page - 1) * size;

        // NOTE: an unfiltered call to this endpoint takes ~110 seconds. Narrowing it to the
        // current academic year (14,060 rows of 693,761) was tried and made no difference, so
        // the cost is the query SHAPE, not the row count — the cross-database join between
        // campus_dynamics_portal.acad_course_registration and campus_dynamics.acad_student,
        // which is the documented collation/TRIM pattern that defeats the indexes. Fixing it
        // belongs with that work, not here; a half-fix with a reassuring message attached
        // would be worse than the honest slow answer.

        var where = new System.Text.StringBuilder("WHERE cr.regno IS NOT NULL");
        var parms = new List<MySqlParameter>();

        if (!string.IsNullOrWhiteSpace(acadYear)) { where.Append(" AND cr.acad_year = @ay");  parms.Add(new MySqlParameter("@ay",  acadYear)); }
        if (semester > 0)                         { where.Append(" AND cr.semester = @sem");  parms.Add(new MySqlParameter("@sem", semester)); }
        if (!string.IsNullOrWhiteSpace(prog))     { where.Append(" AND (TRIM(IFNULL(cr.prog_id,'')) = @prog OR TRIM(IFNULL(s.progid,'')) = @prog)"); parms.Add(new MySqlParameter("@prog", prog)); }
        if (!string.IsNullOrWhiteSpace(courseId))     { where.Append(" AND cr.courseID = @cid");      parms.Add(new MySqlParameter("@cid",  courseId));     }
        if (!string.IsNullOrWhiteSpace(studentRegno)) { where.Append(" AND TRIM(cr.regno) = @sreg"); parms.Add(new MySqlParameter("@sreg", studentRegno)); }

        if (ready == 1)
        {
            // Both CW and Exam filled, not yet published — "ready to submit for review"
            where.Append(@" AND cr.provisional_course_work_marks IS NOT NULL
                            AND cr.provisional_exam_marks IS NOT NULL
                            AND COALESCE(cr.provisional_marks_status,'pending') NOT IN ('published')");
        }
        else if (!string.IsNullOrWhiteSpace(status))
        {
            if (status == "not_entered")
                where.Append(" AND cr.provisional_course_work_marks IS NULL AND cr.provisional_exam_marks IS NULL");
            else if (status == "pending")
                where.Append(@" AND (cr.provisional_course_work_marks IS NOT NULL OR cr.provisional_exam_marks IS NOT NULL)
                                AND COALESCE(cr.provisional_marks_status,'pending') = 'pending'");
            else
            {
                where.Append(" AND cr.provisional_marks_status = @status");
                parms.Add(new MySqlParameter("@status", status));
            }
        }

        if (!string.IsNullOrWhiteSpace(sq))
        {
            where.Append(@" AND (TRIM(cr.regno) LIKE @sq
                             OR TRIM(IFNULL(s.entryno,'')) LIKE @sq
                             OR CONCAT(TRIM(COALESCE(s.firstname,'')), ' ', TRIM(COALESCE(s.othername,''))) LIKE @sq
                             OR TRIM(COALESCE(s.firstname,'')) LIKE @sq
                             OR TRIM(COALESCE(s.othername,'')) LIKE @sq)");
            parms.Add(new MySqlParameter("@sq", "%" + sq + "%"));
        }

        string baseFrom = @"FROM campus_dynamics_portal.acad_course_registration cr
                            LEFT JOIN acad_student s ON TRIM(s.regno) = TRIM(cr.regno)
                            LEFT JOIN acad_course c ON c.courseID = cr.courseID";

        var countParms = new List<MySqlParameter>(parms);
        int total = Convert.ToInt32(ApiHelper.Scalar("SELECT COUNT(*) " + baseFrom + " " + where, countParms.ToArray()));

        parms.Add(new MySqlParameter("@lim", size));
        parms.Add(new MySqlParameter("@off", offset));

        string dataSql = @"SELECT cr.id,
                                  COALESCE(NULLIF(TRIM(IFNULL(s.entryno,'')),'' ), cr.regno) AS entry_no,
                                  CONCAT(TRIM(COALESCE(s.firstname,'')), ' ', TRIM(COALESCE(s.othername,''))) AS student_name,
                                  cr.courseID AS course_code,
                                  cr.acad_year, cr.semester, cr.prog_id AS programme_code,
                                  cr.provisional_course_work_marks AS cw_marks,
                                  cr.provisional_exam_marks         AS exam_marks,
                                  cr.provisional_total_marks        AS total_marks,
                                  COALESCE(cr.provisional_marks_status, 'not_entered') AS prov_status
                           " + baseFrom + " " + where
                        + @" ORDER BY FIELD(COALESCE(cr.provisional_marks_status,'not_entered'),
                                           'rejected','not_entered','pending','approved','published'),
                                      s.firstname, s.othername
                             LIMIT @lim OFFSET @off";

        DataTable dt = ApiHelper.Query(dataSql, parms.ToArray());

        var rows = ApiHelper.TableToList(dt);

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "total", total }, { "page", page }, { "size", size },
            { "pages", (int)Math.Ceiling(total / (double)size) },
            { "rows",  rows }
        });
    }

    private void HandleProvisionalMarkDetail()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        int id = ApiHelper.ParamInt(Request, "id", 0);
        if (id <= 0) { ApiHelper.Error(Response, "id is required.", "MISSING_PARAM"); return; }

        DataTable dt = ApiHelper.Query(
            @"SELECT cr.id,
                     TRIM(cr.regno) AS regno,
                     COALESCE(NULLIF(TRIM(IFNULL(s.entryno,'')),'' ), cr.regno) AS entry_no,
                     CONCAT(TRIM(COALESCE(s.firstname,'')), ' ', TRIM(COALESCE(s.othername,''))) AS student_name,
                     s.gender, COALESCE(s.studsesion,'Day') AS session,
                     cr.courseID AS course_code,
                     COALESCE(c.courseName, cr.courseID) AS course_name,
                     COALESCE(c.CreditUnit, 0) AS credit_units,
                     cr.acad_year, cr.semester, cr.prog_id AS programme_code,
                     COALESCE(p.progname, cr.prog_id) AS programme_name,
                     cr.provisional_course_work_marks AS cw_marks,
                     cr.provisional_exam_marks         AS exam_marks,
                     cr.provisional_total_marks        AS total_marks,
                     COALESCE(cr.provisional_marks_status, 'not_entered') AS prov_status,
                     COALESCE(cr.provisional_marks_review_comments, '') AS review_comments,
                     COALESCE(cr.provisional_marks_reviewed_by, '')     AS reviewed_by,
                     DATE_FORMAT(cr.provisional_marks_review_date,  '%Y-%m-%d %H:%i') AS review_date,
                     COALESCE(cr.provisional_submitted_by, '')  AS submitted_by,
                     COALESCE(cr.provisional_published_by, '')  AS published_by,
                     DATE_FORMAT(cr.provisional_published_date, '%Y-%m-%d %H:%i') AS published_date,
                     CASE WHEN cr.provisional_course_work_marks IS NOT NULL
                               AND cr.provisional_exam_marks IS NOT NULL
                               AND COALESCE(cr.provisional_marks_status,'pending') NOT IN ('published')
                          THEN 1 ELSE 0 END AS ready_to_publish
              FROM campus_dynamics_portal.acad_course_registration cr
              LEFT JOIN acad_student s    ON TRIM(s.regno)  = TRIM(cr.regno)
              LEFT JOIN acad_course c     ON c.courseID     = cr.courseID
              LEFT JOIN acad_programme p  ON p.progcode     = cr.prog_id
              WHERE cr.id = @id LIMIT 1",
            new MySqlParameter("@id", id));

        if (dt.Rows.Count == 0) { ApiHelper.Error(Response, "Record not found.", "NOT_FOUND"); return; }

        var row = ApiHelper.FirstRowToDict(dt);

        // Append calculated grade
        object totObj = row.ContainsKey("total_marks") ? row["total_marks"] : null;
        if (totObj != null && !(totObj is DBNull))
        {
            decimal tot = 0;
            if (decimal.TryParse(totObj.ToString(), out tot))
                row["grade"] = ComputeProvisionalGrade((int)Math.Round(tot));
            else
                row["grade"] = null;
        }
        else row["grade"] = null;

        ApiHelper.Success(Response, row);
    }

    private void HandleSaveProvisionalMark()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        int id = ApiHelper.ParamInt(Request, "id", 0);
        if (id <= 0) { ApiHelper.Error(Response, "id is required.", "MISSING_PARAM"); return; }

        string cwStr   = ApiHelper.RequireParam(Request, Response, "cw"); if (cwStr == null) return;
        string examStr = ApiHelper.RequireParam(Request, Response, "exam"); if (examStr == null) return;

        decimal cw, exam;
        if (!decimal.TryParse(cwStr, out cw) || !decimal.TryParse(examStr, out exam))
        {
            ApiHelper.Error(Response, "cw and exam must be numeric.", "VALIDATION_ERROR"); return;
        }
        if (cw < 0 || cw > 40) { ApiHelper.Error(Response, "Coursework must be 0–40.", "VALIDATION_ERROR"); return; }
        if (exam < 0 || exam > 60) { ApiHelper.Error(Response, "Exam must be 0–60.", "VALIDATION_ERROR"); return; }

        // Lock check
        DataTable lockDt = ApiHelper.Query(
            @"SELECT cr.id, COALESCE(cr.provisional_marks_status,'pending') AS prov_status
              FROM campus_dynamics_portal.acad_course_registration cr
              WHERE cr.id = @id LIMIT 1",
            new MySqlParameter("@id", id));

        if (lockDt.Rows.Count == 0) { ApiHelper.Error(Response, "Record not found.", "NOT_FOUND"); return; }
        if (lockDt.Rows[0]["prov_status"].ToString() == "published")
        {
            ApiHelper.Error(Response, "This record is published and cannot be modified.", "MARKS_LOCKED"); return;
        }

        decimal total = cw + exam;

        ApiHelper.Execute(
            @"UPDATE campus_dynamics_portal.acad_course_registration
              SET provisional_course_work_marks = @cw,
                  provisional_exam_marks         = @exam,
                  provisional_total_marks        = @total,
                  provisional_submitted_by       = COALESCE(provisional_submitted_by, @user),
                  provisional_marks_status       = CASE WHEN provisional_marks_status = 'published' THEN 'published' ELSE 'pending' END
              WHERE id = @id",
            new MySqlParameter("@cw",    cw),
            new MySqlParameter("@exam",  exam),
            new MySqlParameter("@total", total),
            new MySqlParameter("@user",  auth.UserId),
            new MySqlParameter("@id",    id));

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "id", id }, { "cw_marks", cw }, { "exam_marks", exam },
            { "total_marks", total }, { "grade", ComputeProvisionalGrade((int)Math.Round(total)) }
        }, "Provisional marks saved");
    }

    private void HandleSaveProvisionalMarkInline()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        int id    = ApiHelper.ParamInt(Request, "id", 0);
        if (id <= 0) { ApiHelper.Error(Response, "id is required.", "MISSING_PARAM"); return; }
        string field = ApiHelper.RequireParam(Request, Response, "field"); if (field == null) return;
        string val   = ApiHelper.RequireParam(Request, Response, "value"); if (val == null) return;

        // Lock check + fetch current values
        DataTable lockDt = ApiHelper.Query(
            @"SELECT cr.id, COALESCE(cr.provisional_marks_status,'pending') AS prov_status,
                     COALESCE(cr.provisional_course_work_marks, 0) AS cur_cw,
                     COALESCE(cr.provisional_exam_marks, 0) AS cur_exam
              FROM campus_dynamics_portal.acad_course_registration cr
              WHERE cr.id = @id LIMIT 1",
            new MySqlParameter("@id", id));

        if (lockDt.Rows.Count == 0) { ApiHelper.Error(Response, "Record not found.", "NOT_FOUND"); return; }
        if (lockDt.Rows[0]["prov_status"].ToString() == "published")
        {
            ApiHelper.Error(Response, "This record is published and cannot be modified.", "MARKS_LOCKED"); return;
        }

        decimal curCw   = Convert.ToDecimal(lockDt.Rows[0]["cur_cw"]);
        decimal curExam = Convert.ToDecimal(lockDt.Rows[0]["cur_exam"]);

        decimal newVal;
        if (!decimal.TryParse(val, out newVal))
        {
            ApiHelper.Error(Response, "value must be numeric.", "VALIDATION_ERROR"); return;
        }

        decimal newCw = curCw, newExam = curExam;
        if (field == "cw")
        {
            if (newVal < 0 || newVal > 40) { ApiHelper.Error(Response, "Coursework must be 0–40.", "VALIDATION_ERROR"); return; }
            newCw = newVal;
        }
        else if (field == "exam")
        {
            if (newVal < 0 || newVal > 60) { ApiHelper.Error(Response, "Exam must be 0–60.", "VALIDATION_ERROR"); return; }
            newExam = newVal;
        }
        else
        {
            ApiHelper.Error(Response, "field must be 'cw' or 'exam'.", "VALIDATION_ERROR"); return;
        }

        decimal total = newCw + newExam;

        ApiHelper.Execute(
            @"UPDATE campus_dynamics_portal.acad_course_registration
              SET provisional_course_work_marks = @cw,
                  provisional_exam_marks         = @exam,
                  provisional_total_marks        = @total,
                  provisional_submitted_by       = COALESCE(provisional_submitted_by, @user),
                  provisional_marks_status       = CASE WHEN provisional_marks_status = 'published' THEN 'published' ELSE 'pending' END
              WHERE id = @id",
            new MySqlParameter("@cw",    newCw),
            new MySqlParameter("@exam",  newExam),
            new MySqlParameter("@total", total),
            new MySqlParameter("@user",  auth.UserId),
            new MySqlParameter("@id",    id));

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "id", id }, { "field", field }, { "new_value", newVal },
            { "total_marks", total }, { "grade", ComputeProvisionalGrade((int)Math.Round(total)) }
        }, "Mark saved inline");
    }

    private void HandleProvisionalMarksSummary()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string acadYear = ApiHelper.Param(Request, "acad_year", "").Trim();
        int    semester = ApiHelper.ParamInt(Request, "semester", 0);

        var where = new System.Text.StringBuilder("WHERE cr.regno IS NOT NULL");
        var parms = new List<MySqlParameter>();

        if (!string.IsNullOrWhiteSpace(acadYear)) { where.Append(" AND cr.acad_year = @ay");  parms.Add(new MySqlParameter("@ay",  acadYear)); }
        if (semester > 0)                         { where.Append(" AND cr.semester = @sem");  parms.Add(new MySqlParameter("@sem", semester)); }

        // Per-course breakdown
        string sql = @"SELECT cr.courseID AS course_code,
                              COALESCE(c.courseName, cr.courseID) AS course_name,
                              COALESCE(p.progname,  cr.prog_id)   AS programme_name,
                              cr.acad_year, cr.semester, cr.prog_id AS programme_code,
                              COUNT(*) AS total_students,
                              SUM(CASE WHEN cr.provisional_course_work_marks IS NULL AND cr.provisional_exam_marks IS NULL    THEN 1 ELSE 0 END) AS not_entered,
                              SUM(CASE WHEN cr.provisional_course_work_marks IS NOT NULL OR  cr.provisional_exam_marks IS NOT NULL THEN 1 ELSE 0 END) AS partially_entered,
                              SUM(CASE WHEN cr.provisional_course_work_marks IS NOT NULL AND cr.provisional_exam_marks IS NOT NULL THEN 1 ELSE 0 END) AS fully_entered,
                              SUM(CASE WHEN COALESCE(cr.provisional_marks_status,'pending') = 'pending'
                                            AND cr.provisional_total_marks IS NOT NULL               THEN 1 ELSE 0 END) AS pending,
                              SUM(CASE WHEN cr.provisional_marks_status = 'approved'                THEN 1 ELSE 0 END) AS approved,
                              SUM(CASE WHEN cr.provisional_marks_status = 'rejected'                THEN 1 ELSE 0 END) AS rejected,
                              SUM(CASE WHEN cr.provisional_marks_status = 'published'               THEN 1 ELSE 0 END) AS published,
                              SUM(CASE WHEN cr.provisional_course_work_marks IS NOT NULL
                                            AND cr.provisional_exam_marks IS NOT NULL
                                            AND COALESCE(cr.provisional_marks_status,'pending') NOT IN ('published')
                                       THEN 1 ELSE 0 END) AS ready_to_publish
                       FROM campus_dynamics_portal.acad_course_registration cr
                       LEFT JOIN acad_course    c ON c.courseID = cr.courseID
                       LEFT JOIN acad_programme p ON p.progcode = cr.prog_id
                       " + where
                    + " GROUP BY cr.courseID, cr.acad_year, cr.semester, cr.prog_id"
                    + " ORDER BY cr.acad_year DESC, cr.semester, cr.courseID";

        DataTable dt = ApiHelper.Query(sql, parms.ToArray());

        // Aggregate totals across all courses
        int totStudents = 0, totNotEntered = 0, totPending = 0, totApproved = 0, totRejected = 0, totPublished = 0, totReady = 0;
        foreach (DataRow row in dt.Rows)
        {
            totStudents   += Convert.ToInt32(row["total_students"]);
            totNotEntered += Convert.ToInt32(row["not_entered"]);
            totPending    += Convert.ToInt32(row["pending"]);
            totApproved   += Convert.ToInt32(row["approved"]);
            totRejected   += Convert.ToInt32(row["rejected"]);
            totPublished  += Convert.ToInt32(row["published"]);
            totReady      += Convert.ToInt32(row["ready_to_publish"]);
        }

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "total_courses",  dt.Rows.Count },
            { "total_students", totStudents },
            { "totals", new Dictionary<string, object>
                {
                    { "not_entered",     totNotEntered },
                    { "pending",         totPending },
                    { "approved",        totApproved },
                    { "rejected",        totRejected },
                    { "published",       totPublished },
                    { "ready_to_publish",totReady }
                }
            },
            { "courses", ApiHelper.TableToList(dt) }
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  BULK & DASHBOARD MARKS ENDPOINTS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Bulk-save provisional marks for multiple students in one call.
    /// POST body or 'marks' param: JSON array [{id, cw, exam}, ...]
    /// Partial updates allowed — supply only cw or only exam to update one component.
    /// Validates 0–40 for cw, 0–60 for exam.  Blocks published records.
    /// Returns per-row outcome: saved | skipped | error.
    /// </summary>
    private void HandleBulkSaveMarks()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string json = ApiHelper.Param(Request, "marks", "");
        if (string.IsNullOrEmpty(json))
        {
            try { using (var rdr = new StreamReader(Request.InputStream)) json = rdr.ReadToEnd(); } catch { }
        }
        if (string.IsNullOrEmpty(json))
        {
            ApiHelper.Error(Response, "Missing marks data. Send JSON array: [{\"id\":123,\"cw\":30,\"exam\":50}, ...]", "MISSING_PARAM");
            return;
        }

        List<Dictionary<string, object>> marksList;
        try
        {
            var ser = new JavaScriptSerializer();
            marksList = ser.Deserialize<List<Dictionary<string, object>>>(json);
        }
        catch
        {
            ApiHelper.Error(Response, "Invalid JSON format.", "VALIDATION_ERROR"); return;
        }

        if (marksList == null || marksList.Count == 0)
        { ApiHelper.Error(Response, "Empty marks array.", "MISSING_PARAM"); return; }
        if (marksList.Count > 500)
        { ApiHelper.Error(Response, "Maximum 500 marks per bulk save.", "VALIDATION_ERROR"); return; }

        int saved = 0, skipped = 0, errors = 0;
        var detail = new List<Dictionary<string, object>>();

        foreach (var item in marksList)
        {
            int rowId = 0;
            if (item.ContainsKey("id")) int.TryParse(Convert.ToString(item["id"]), out rowId);
            if (rowId <= 0)
            {
                errors++;
                detail.Add(new Dictionary<string, object> { { "id", rowId }, { "error", "id required and must be > 0" } });
                continue;
            }

            string cwStr   = item.ContainsKey("cw")   ? Convert.ToString(item["cw"])   : null;
            string examStr = item.ContainsKey("exam") ? Convert.ToString(item["exam"]) : null;

            if (string.IsNullOrEmpty(cwStr) && string.IsNullOrEmpty(examStr))
            {
                skipped++;
                detail.Add(new Dictionary<string, object> { { "id", rowId }, { "skipped", "no cw or exam provided" } });
                continue;
            }

            decimal cwVal = 0, examVal = 0;
            if (!string.IsNullOrEmpty(cwStr) && (!decimal.TryParse(cwStr, out cwVal) || cwVal < 0 || cwVal > 40))
            {
                errors++;
                detail.Add(new Dictionary<string, object> { { "id", rowId }, { "error", "cw must be 0–40" } });
                continue;
            }
            if (!string.IsNullOrEmpty(examStr) && (!decimal.TryParse(examStr, out examVal) || examVal < 0 || examVal > 60))
            {
                errors++;
                detail.Add(new Dictionary<string, object> { { "id", rowId }, { "error", "exam must be 0–60" } });
                continue;
            }

            try
            {
                // Lock check
                DataTable lockDt = ApiHelper.Query(
                    @"SELECT cr.id,
                             COALESCE(cr.provisional_marks_status,'not_entered') AS prov_status,
                             COALESCE(cr.provisional_course_work_marks, 0) AS cur_cw,
                             COALESCE(cr.provisional_exam_marks, 0)        AS cur_exam
                      FROM campus_dynamics_portal.acad_course_registration cr
                      WHERE cr.id = @rowId LIMIT 1",
                    new MySqlParameter("@rowId", rowId));
                if (lockDt.Rows.Count == 0)
                {
                    skipped++;
                    detail.Add(new Dictionary<string, object> { { "id", rowId }, { "skipped", "record not found" } });
                    continue;
                }

                string provStatus = lockDt.Rows[0]["prov_status"].ToString();
                if (provStatus == "published")
                {
                    skipped++;
                    detail.Add(new Dictionary<string, object> { { "id", rowId }, { "skipped", "published — cannot modify" } });
                    continue;
                }

                // Partial update: keep current value if component not supplied
                decimal applyCw   = !string.IsNullOrEmpty(cwStr)   ? cwVal   : Convert.ToDecimal(lockDt.Rows[0]["cur_cw"]);
                decimal applyExam = !string.IsNullOrEmpty(examStr) ? examVal : Convert.ToDecimal(lockDt.Rows[0]["cur_exam"]);
                decimal total     = applyCw + applyExam;

                ApiHelper.Execute(
                    @"UPDATE campus_dynamics_portal.acad_course_registration
                      SET provisional_course_work_marks = @cw,
                          provisional_exam_marks         = @exam,
                          provisional_total_marks        = @total,
                          provisional_submitted_by       = COALESCE(provisional_submitted_by, @user),
                          provisional_marks_status       = CASE WHEN provisional_marks_status = 'published' THEN 'published' ELSE 'pending' END
                      WHERE id = @rowId",
                    new MySqlParameter("@cw",    applyCw),
                    new MySqlParameter("@exam",  applyExam),
                    new MySqlParameter("@total", total),
                    new MySqlParameter("@user",  auth.UserId),
                    new MySqlParameter("@rowId", rowId));

                saved++;
                detail.Add(new Dictionary<string, object>
                {
                    { "id",          rowId },
                    { "cw_marks",    applyCw },
                    { "exam_marks",  applyExam },
                    { "total_marks", total },
                    { "grade",       ComputeProvisionalGrade((int)total) }
                });
            }
            catch (Exception ex)
            {
                errors++;
                detail.Add(new Dictionary<string, object> { { "id", rowId }, { "error", ex.Message } });
            }
        }

        string msg = saved > 0
            ? "Bulk save complete: " + saved + " saved, " + skipped + " skipped, " + errors + " errors"
            : "No marks were saved";

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "saved",            saved },
            { "skipped",          skipped },
            { "errors",           errors },
            { "total_submitted",  marksList.Count },
            { "detail",           detail }
        }, msg);
    }

    /// <summary>
    /// Returns overall dashboard statistics for the authenticated teacher.
    /// Aggregates provisional mark counts across all assigned courses, with per-course breakdown.
    /// Filters: acad_year (optional), semester (optional).
    /// </summary>
    private void HandleMarkStats()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string acadYear = ApiHelper.Param(Request, "acad_year", "").Trim();
        int    semester = ApiHelper.ParamInt(Request, "semester", 0);

        var where = new System.Text.StringBuilder("WHERE cr.regno IS NOT NULL");
        var parms = new List<MySqlParameter>();
        if (!string.IsNullOrWhiteSpace(acadYear)) { where.Append(" AND cr.acad_year = @ay"); parms.Add(new MySqlParameter("@ay", acadYear)); }
        if (semester > 0)                         { where.Append(" AND cr.semester = @sem"); parms.Add(new MySqlParameter("@sem", semester)); }

        // ── Overall summary ───────────────────────────────────────────────
        string summarySql = @"SELECT
            COUNT(*) AS total_students,
            COUNT(DISTINCT cr.courseID) AS total_courses,
            COUNT(DISTINCT cr.prog_id)   AS total_programmes,
            SUM(CASE WHEN cr.provisional_course_work_marks IS NULL AND cr.provisional_exam_marks IS NULL                                    THEN 1 ELSE 0 END) AS not_entered,
            SUM(CASE WHEN cr.provisional_course_work_marks IS NOT NULL AND cr.provisional_exam_marks IS NOT NULL                            THEN 1 ELSE 0 END) AS fully_entered,
            SUM(CASE WHEN COALESCE(cr.provisional_marks_status,'pending') = 'pending' AND cr.provisional_total_marks IS NOT NULL            THEN 1 ELSE 0 END) AS pending_review,
            SUM(CASE WHEN cr.provisional_marks_status = 'approved'                                                                          THEN 1 ELSE 0 END) AS approved,
            SUM(CASE WHEN cr.provisional_marks_status = 'rejected'                                                                          THEN 1 ELSE 0 END) AS rejected,
            SUM(CASE WHEN cr.provisional_marks_status = 'published'                                                                         THEN 1 ELSE 0 END) AS published,
            SUM(CASE WHEN cr.provisional_course_work_marks IS NOT NULL AND cr.provisional_exam_marks IS NOT NULL
                          AND COALESCE(cr.provisional_marks_status,'pending') NOT IN ('published')                                           THEN 1 ELSE 0 END) AS ready_to_publish
        FROM campus_dynamics_portal.acad_course_registration cr " + where;

        var sumParms = new List<MySqlParameter>(parms);
        DataTable sumDt = ApiHelper.Query(summarySql, sumParms.ToArray());
        var summary = sumDt.Rows.Count > 0 ? ApiHelper.FirstRowToDict(sumDt) : new Dictionary<string, object>();

        // ── Per-course breakdown ──────────────────────────────────────────
        string courseSql = @"SELECT cr.courseID AS course_code,
                                    COALESCE(c.courseName, cr.courseID) AS course_name,
                                    COALESCE(p.progname,  cr.prog_id)   AS programme_name,
                                    cr.prog_id AS programme_code,
                                    cr.acad_year, cr.semester,
                                    COUNT(*) AS total_students,
                                    SUM(CASE WHEN cr.provisional_course_work_marks IS NULL AND cr.provisional_exam_marks IS NULL              THEN 1 ELSE 0 END) AS not_entered,
                                    SUM(CASE WHEN cr.provisional_course_work_marks IS NOT NULL AND cr.provisional_exam_marks IS NOT NULL      THEN 1 ELSE 0 END) AS fully_entered,
                                    SUM(CASE WHEN COALESCE(cr.provisional_marks_status,'pending') = 'pending' AND cr.provisional_total_marks IS NOT NULL THEN 1 ELSE 0 END) AS pending,
                                    SUM(CASE WHEN cr.provisional_marks_status = 'approved'                                                   THEN 1 ELSE 0 END) AS approved,
                                    SUM(CASE WHEN cr.provisional_marks_status = 'rejected'                                                   THEN 1 ELSE 0 END) AS rejected,
                                    SUM(CASE WHEN cr.provisional_marks_status = 'published'                                                  THEN 1 ELSE 0 END) AS published,
                                    SUM(CASE WHEN cr.provisional_course_work_marks IS NOT NULL AND cr.provisional_exam_marks IS NOT NULL
                                                  AND COALESCE(cr.provisional_marks_status,'pending') NOT IN ('published')                   THEN 1 ELSE 0 END) AS ready_to_publish,
                                    ROUND(AVG(CASE WHEN cr.provisional_total_marks IS NOT NULL THEN cr.provisional_total_marks END), 1) AS avg_total
                             FROM campus_dynamics_portal.acad_course_registration cr
                             LEFT JOIN acad_course    c ON c.courseID = cr.courseID
                             LEFT JOIN acad_programme p ON p.progcode = cr.prog_id
                             " + where
                          + " GROUP BY cr.courseID, cr.prog_id, cr.acad_year, cr.semester"
                          + " ORDER BY cr.acad_year DESC, cr.semester, cr.courseID"
                          + " LIMIT 100";

        var courseParms = new List<MySqlParameter>(parms);
        DataTable coursesDt = ApiHelper.Query(courseSql, courseParms.ToArray());

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "filter_acad_year", string.IsNullOrWhiteSpace(acadYear) ? null : (object)acadYear },
            { "filter_semester",  semester > 0 ? (object)semester : null },
            { "summary",  summary },
            { "courses",  ApiHelper.TableToList(coursesDt) }
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  STUDENT SEARCH
    // ═══════════════════════════════════════════════════════════════════

    private void HandleStudentSearch()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string q    = ApiHelper.Param(Request, "q",    "").Trim();
        string prog = ApiHelper.Param(Request, "prog", "").Trim();
        int    size = Math.Min(100, Math.Max(10, ApiHelper.ParamInt(Request, "size", 30)));

        var where = new System.Text.StringBuilder("WHERE 1=1");
        var parms = new List<MySqlParameter>();

        if (!string.IsNullOrWhiteSpace(q))
        {
            where.Append(@" AND (
                TRIM(s.regno) LIKE @q
                OR TRIM(IFNULL(s.entryno,'')) LIKE @q
                OR LOWER(TRIM(COALESCE(s.firstname,''))) LIKE @q
                OR LOWER(TRIM(COALESCE(s.othername,''))) LIKE @q
                OR LOWER(TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,'')))) LIKE @q
            )");
            parms.Add(new MySqlParameter("@q", "%" + q.ToLower() + "%"));
        }

        if (!string.IsNullOrWhiteSpace(prog))
        {
            where.Append(" AND s.progid = @prog");
            parms.Add(new MySqlParameter("@prog", prog));
        }

        // Closest-match ordering: exact ID first, then prefix, then contains
        string orderBy = !string.IsNullOrWhiteSpace(q)
            ? @"ORDER BY
                  CASE WHEN LOWER(TRIM(IFNULL(s.entryno,''))) = @qExact OR LOWER(TRIM(s.regno)) = @qExact THEN 0
                       WHEN TRIM(IFNULL(s.entryno,'')) LIKE @qPrefix OR s.regno LIKE @qPrefix THEN 1
                       WHEN LOWER(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) LIKE @qPrefix THEN 2
                       ELSE 3 END,
                  s.firstname, s.othername"
            : "ORDER BY s.firstname, s.othername";

        if (!string.IsNullOrWhiteSpace(q))
        {
            parms.Add(new MySqlParameter("@qExact",  q.ToLower()));
            parms.Add(new MySqlParameter("@qPrefix", q.ToLower() + "%"));
        }

        parms.Add(new MySqlParameter("@lim", size));

        DataTable dt = ApiHelper.Query(
            @"SELECT s.regno,
                     COALESCE(NULLIF(TRIM(IFNULL(s.entryno,'')),'' ), s.regno) AS entry_no,
                     TRIM(COALESCE(s.firstname,''))  AS firstname,
                     TRIM(COALESCE(s.othername,''))  AS othername,
                     TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) AS student_name,
                     s.gender,
                     COALESCE(s.studsesion,'Day') AS session,
                     s.progid AS programme_code,
                     COALESCE(p.progname, s.progid) AS programme_name,
                     COALESCE(s.stud_status,'Active') AS student_status,
                     IFNULL(TRIM(s.photofile),'') AS photofile
              FROM acad_student s
              LEFT JOIN acad_programme p ON p.progcode = s.progid
              " + where + " " + orderBy + @"
              LIMIT @lim",
            parms.ToArray());

        var students = ApiHelper.TableToList(dt);
        foreach (var row in students)
        {
            string pf = Convert.ToString(row["photofile"]);
            row["photo_url"] = !string.IsNullOrWhiteSpace(pf)
                ? "/COOPERP/StudentInfo/photos/" + pf
                : null;
            row.Remove("photofile");
        }

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "count",   students.Count },
            { "query",   q },
            { "results", students }
        });
    }

    //  HR EMPLOYEES
    // ═══════════════════════════════════════════════════════════════════

    private void HandleEmployees()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string dept   = ApiHelper.Param(Request, "dept", "");
        string type   = ApiHelper.Param(Request, "type", "");
        string status = ApiHelper.Param(Request, "status", "");
        string q      = ApiHelper.Param(Request, "q", "");
        int page      = Math.Max(1, ApiHelper.ParamInt(Request, "page", 1));
        int size      = Math.Min(100, Math.Max(1, ApiHelper.ParamInt(Request, "size", 20)));
        int offset    = (page - 1) * size;

        var where = new System.Text.StringBuilder("WHERE 1=1");
        var parms = new List<MySqlParameter>();

        if (!string.IsNullOrEmpty(dept)) { where.Append(" AND d.ID = @dept"); parms.Add(new MySqlParameter("@dept", dept)); }
        if (!string.IsNullOrEmpty(type)) { where.Append(" AND e.EmpType LIKE @type"); parms.Add(new MySqlParameter("@type", "%" + type + "%")); }
        if (!string.IsNullOrEmpty(status)) { where.Append(" AND e.emp_status = @st"); parms.Add(new MySqlParameter("@st", status)); }
        if (!string.IsNullOrEmpty(q))
        {
            where.Append(" AND (e.emp_name LIKE @q OR e.EMP_CODE LIKE @q OR e.emp_email LIKE @q)");
            parms.Add(new MySqlParameter("@q", "%" + q + "%"));
        }

        string baseSql = @"FROM hrm_employee e
                           LEFT JOIN hrm_emp_contracts c ON c.empID = e.empID AND c.contractStatus = 'VALID'
                           LEFT JOIN hrm_departments d ON c.departmentID = d.ID " + where;

        var countParms = new List<MySqlParameter>(parms);
        int total = Convert.ToInt32(ApiHelper.Scalar("SELECT COUNT(DISTINCT e.empID) " + baseSql, countParms.ToArray()));

        parms.Add(new MySqlParameter("@lim", size));
        parms.Add(new MySqlParameter("@off", offset));

        DataTable dt = ApiHelper.Query(
            @"SELECT e.empID, e.EMP_CODE AS emp_code, e.emp_name, e.EmpType AS emp_type,
                     e.emp_email AS email, e.emp_phone AS phone,
                     e.emp_status AS status, e.emp_nationality AS nationality,
                     d.dept_name AS department, d.ID AS department_id,
                     c.contractStart AS contract_start, c.contractEnd AS contract_end,
                     c.contractStatus AS contract_status "
            + baseSql + " GROUP BY e.empID ORDER BY e.emp_name LIMIT @lim OFFSET @off",
            parms.ToArray());

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "total", total }, { "page", page }, { "size", size },
            { "pages", (int)Math.Ceiling(total / (double)size) },
            { "employees", ApiHelper.TableToList(dt) }
        });
    }

    private void HandleEmployee()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string empId = ApiHelper.RequireParam(Request, Response, "emp_id"); if (empId == null) return;

        DataTable dt = ApiHelper.Query(
            @"SELECT e.empID, e.EMP_CODE AS emp_code, e.emp_name, e.EmpType AS emp_type,
                     e.emp_email AS email, e.emp_phone AS phone,
                     e.emp_status AS status, e.emp_nationality AS nationality,
                     e.emp_qualifications AS qualifications,
                     d.dept_name AS department, d.ID AS department_id
              FROM hrm_employee e
              LEFT JOIN hrm_emp_contracts c ON c.empID = e.empID AND c.contractStatus = 'VALID'
              LEFT JOIN hrm_departments d ON c.departmentID = d.ID
              WHERE e.empID = @id LIMIT 1",
            new MySqlParameter("@id", empId));

        if (dt.Rows.Count == 0) { ApiHelper.Error(Response, "Employee not found.", "NOT_FOUND"); return; }

        var emp = ApiHelper.FirstRowToDict(dt);

        // Contract history (excluding password/account fields)
        DataTable contracts = ApiHelper.Query(
            @"SELECT c.id, c.contractStart AS start_date, c.contractEnd AS end_date,
                     c.contractStatus AS status, c.departmentID AS department_id,
                     d.dept_name AS department
              FROM hrm_emp_contracts c
              LEFT JOIN hrm_departments d ON c.departmentID = d.ID
              WHERE c.empID = @id ORDER BY c.contractStart DESC",
            new MySqlParameter("@id", empId));

        emp["contracts"] = ApiHelper.TableToList(contracts);

        ApiHelper.Success(Response, emp);
    }

    private void HandleCreateEmployee()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string empCode     = ApiHelper.RequireParam(Request, Response, "emp_code"); if (empCode == null) return;
        string empName     = ApiHelper.RequireParam(Request, Response, "emp_name"); if (empName == null) return;
        string empType     = ApiHelper.Param(Request, "emp_type", "Academic");
        string empEmail    = ApiHelper.Param(Request, "emp_email", "");
        string empPhone    = ApiHelper.Param(Request, "emp_phone", "");
        string nationality = ApiHelper.Param(Request, "nationality", "");
        string qualifications = ApiHelper.Param(Request, "qualifications", "");
        string empStatus   = ApiHelper.Param(Request, "status", "Active");

        // Check for duplicate emp_code
        object existing = ApiHelper.Scalar(
            "SELECT COUNT(*) FROM hrm_employee WHERE EMP_CODE = @code",
            new MySqlParameter("@code", empCode));
        if (Convert.ToInt32(existing) > 0)
        {
            ApiHelper.Error(Response, "An employee with this code already exists.", "DUPLICATE"); return;
        }

        ApiHelper.Execute(
            @"INSERT INTO hrm_employee (EMP_CODE, emp_name, EmpType, emp_email, emp_phone, emp_nationality, emp_qualifications, emp_status)
              VALUES (@code, @name, @type, @email, @phone, @nat, @qual, @st)",
            new MySqlParameter("@code",  empCode),
            new MySqlParameter("@name",  empName),
            new MySqlParameter("@type",  empType),
            new MySqlParameter("@email", empEmail),
            new MySqlParameter("@phone", empPhone),
            new MySqlParameter("@nat",   nationality),
            new MySqlParameter("@qual",  qualifications),
            new MySqlParameter("@st",    empStatus));

        int newId = Convert.ToInt32(ApiHelper.Scalar("SELECT LAST_INSERT_ID()"));

        ApiHelper.Success(Response, new Dictionary<string, object> { { "emp_id", newId }, { "emp_code", empCode } },
            "Employee created");
    }

    private void HandleUpdateEmployee()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string empId  = ApiHelper.RequireParam(Request, Response, "emp_id"); if (empId == null) return;

        DataTable existing = ApiHelper.Query(
            "SELECT empID FROM hrm_employee WHERE empID = @id LIMIT 1",
            new MySqlParameter("@id", empId));
        if (existing.Rows.Count == 0) { ApiHelper.Error(Response, "Employee not found.", "NOT_FOUND"); return; }

        // Build dynamic update from provided fields
        var sets = new List<string>();
        var parms = new List<MySqlParameter>();
        parms.Add(new MySqlParameter("@id", empId));

        string empName  = ApiHelper.Param(Request, "emp_name", "");
        string empType  = ApiHelper.Param(Request, "emp_type", "");
        string empEmail = ApiHelper.Param(Request, "emp_email", "");
        string empPhone = ApiHelper.Param(Request, "emp_phone", "");
        string nat      = ApiHelper.Param(Request, "nationality", "");
        string qual     = ApiHelper.Param(Request, "qualifications", "");
        string status   = ApiHelper.Param(Request, "status", "");

        if (!string.IsNullOrEmpty(empName))  { sets.Add("emp_name = @name");    parms.Add(new MySqlParameter("@name",  empName));  }
        if (!string.IsNullOrEmpty(empType))  { sets.Add("EmpType = @type");      parms.Add(new MySqlParameter("@type",  empType));  }
        if (!string.IsNullOrEmpty(empEmail)) { sets.Add("emp_email = @email");   parms.Add(new MySqlParameter("@email", empEmail)); }
        if (!string.IsNullOrEmpty(empPhone)) { sets.Add("emp_phone = @phone");   parms.Add(new MySqlParameter("@phone", empPhone)); }
        if (!string.IsNullOrEmpty(nat))      { sets.Add("emp_nationality = @nat"); parms.Add(new MySqlParameter("@nat",  nat));     }
        if (!string.IsNullOrEmpty(qual))     { sets.Add("emp_qualifications = @qual"); parms.Add(new MySqlParameter("@qual", qual)); }
        if (!string.IsNullOrEmpty(status))   { sets.Add("emp_status = @st");     parms.Add(new MySqlParameter("@st",    status));   }

        if (sets.Count == 0) { ApiHelper.Error(Response, "No fields to update.", "VALIDATION_ERROR"); return; }

        ApiHelper.Execute(
            "UPDATE hrm_employee SET " + string.Join(", ", sets) + " WHERE empID = @id",
            parms.ToArray());

        ApiHelper.Success(Response, new Dictionary<string, object> { { "emp_id", empId } }, "Employee updated");
    }

    private void HandleUpdateContract()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string empId        = ApiHelper.RequireParam(Request, Response, "emp_id"); if (empId == null) return;
        string contractStart= ApiHelper.RequireParam(Request, Response, "contract_start"); if (contractStart == null) return;
        string contractEnd  = ApiHelper.RequireParam(Request, Response, "contract_end"); if (contractEnd == null) return;
        string contractStatus = ApiHelper.Param(Request, "contract_status", "VALID");
        string departmentId = ApiHelper.Param(Request, "department_id", "");

        // Expire existing VALID contracts for this employee
        ApiHelper.Execute(
            "UPDATE hrm_emp_contracts SET contractStatus = 'EXPIRED' WHERE empID = @eid AND contractStatus = 'VALID'",
            new MySqlParameter("@eid", empId));

        ApiHelper.Execute(
            @"INSERT INTO hrm_emp_contracts (empID, contractStart, contractEnd, contractStatus, departmentID)
              VALUES (@eid, @start, @end, @st, @dept)",
            new MySqlParameter("@eid",   empId),
            new MySqlParameter("@start", contractStart),
            new MySqlParameter("@end",   contractEnd),
            new MySqlParameter("@st",    contractStatus),
            new MySqlParameter("@dept",  string.IsNullOrEmpty(departmentId) ? (object)DBNull.Value : departmentId));

        int contractId = Convert.ToInt32(ApiHelper.Scalar("SELECT LAST_INSERT_ID()"));

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "emp_id", empId }, { "contract_id", contractId }, { "status", contractStatus }
        }, "Contract updated");
    }

    private void HandleDepartments()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        // Cache for 1 hour
        const string cacheKey = "api_v2_departments";
        var cached = HttpRuntime.Cache[cacheKey] as List<Dictionary<string, object>>;
        if (cached != null)
        {
            ApiHelper.Success(Response, new Dictionary<string, object> { { "departments", cached } });
            return;
        }

        DataTable dt = ApiHelper.Query(
            @"SELECT d.ID AS department_id, d.dept_name AS department_name,
                     f.faculty_name AS faculty, f.fax_code AS faculty_code,
                     COUNT(DISTINCT ec.empID) AS employee_count
              FROM hrm_departments d
              LEFT JOIN acad_faculty f ON d.fax_code = f.fax_code
              LEFT JOIN hrm_emp_contracts ec ON ec.departmentID = d.ID AND ec.contractStatus = 'VALID'
              GROUP BY d.ID ORDER BY d.dept_name");

        var rows = ApiHelper.TableToList(dt);
        HttpRuntime.Cache.Insert(cacheKey, rows, null,
            DateTime.Now.AddHours(1), System.Web.Caching.Cache.NoSlidingExpiration);

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "total", rows.Count }, { "departments", rows }
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  MARK REQUESTS
    // ═══════════════════════════════════════════════════════════════════

    private static readonly string[] VALID_REQUEST_TYPES = { "MARK_CHANGE", "INITIAL_SUBMISSION", "CORRECTION", "OTHER" };

    private void EnsureMarkRequestsSchema()
    {
        try
        {
            ApiHelper.Execute(@"CREATE TABLE IF NOT EXISTS acad_mark_requests (
                id               INT AUTO_INCREMENT PRIMARY KEY,
                teacher_username VARCHAR(100) NOT NULL,
                course_id        VARCHAR(50)  NOT NULL,
                regno            VARCHAR(50)  NOT NULL,
                registration_id  INT          NULL,
                acad_year        VARCHAR(20)  NOT NULL,
                semester         INT          NOT NULL DEFAULT 1,
                request_type     VARCHAR(80)  NOT NULL DEFAULT 'MARK_CHANGE',
                reason           TEXT         NOT NULL,
                old_cw           DECIMAL(5,2) NULL,
                old_exam         DECIMAL(5,2) NULL,
                new_cw           DECIMAL(5,2) NULL,
                new_exam         DECIMAL(5,2) NULL,
                status           VARCHAR(30)  NOT NULL DEFAULT 'PENDING',
                admin_comment    TEXT         NULL,
                decided_by       VARCHAR(100) NULL,
                created_at       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
                decided_at       DATETIME     NULL,
                INDEX idx_mr_teacher (teacher_username),
                INDEX idx_mr_status  (status),
                INDEX idx_mr_regno   (regno),
                INDEX idx_mr_reg_id  (registration_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
        }
        catch { }

        // Migrate existing tables: add columns silently if they don't exist yet
        string[] migrationAlters = {
            "ALTER TABLE acad_mark_requests ADD COLUMN registration_id INT NULL AFTER regno",
            "ALTER TABLE acad_mark_requests ADD COLUMN old_cw   DECIMAL(5,2) NULL AFTER reason",
            "ALTER TABLE acad_mark_requests ADD COLUMN old_exam DECIMAL(5,2) NULL AFTER old_cw",
            "ALTER TABLE acad_mark_requests ADD COLUMN new_cw   DECIMAL(5,2) NULL AFTER old_exam",
            "ALTER TABLE acad_mark_requests ADD COLUMN new_exam DECIMAL(5,2) NULL AFTER new_cw"
        };
        foreach (string alter in migrationAlters) { try { ApiHelper.Execute(alter); } catch { } }
    }

    // Common SELECT used by list, detail, and admin list handlers
    private const string MARK_REQUEST_SELECT =
        @"SELECT mr.id, mr.teacher_username, mr.course_id,
                 c.courseName AS course_name,
                 mr.regno,
                 CONCAT(TRIM(COALESCE(s.firstname,'')), ' ', TRIM(COALESCE(s.othername,''))) AS student_name,
                 mr.registration_id, mr.acad_year, mr.semester,
                 mr.request_type, mr.reason,
                 mr.old_cw, mr.old_exam,
                 ROUND(COALESCE(mr.old_cw,0) + COALESCE(mr.old_exam,0), 2) AS old_total,
                 mr.new_cw, mr.new_exam,
                 ROUND(COALESCE(mr.new_cw,0) + COALESCE(mr.new_exam,0), 2) AS new_total,
                 mr.status, mr.admin_comment, mr.decided_by,
                 DATE_FORMAT(mr.created_at, '%Y-%m-%d %H:%i') AS created_at,
                 DATE_FORMAT(mr.decided_at, '%Y-%m-%d %H:%i') AS decided_at
          FROM acad_mark_requests mr
          LEFT JOIN acad_course c ON c.courseID = mr.course_id
          LEFT JOIN acad_student s ON TRIM(s.regno) = TRIM(mr.regno)";

    private void HandleMarkRequestsList()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        EnsureMarkRequestsSchema();

        string status = ApiHelper.Param(Request, "status", "");
        int page      = Math.Max(1, ApiHelper.ParamInt(Request, "page", 1));
        int size      = Math.Min(100, Math.Max(1, ApiHelper.ParamInt(Request, "size", 20)));
        int offset    = (page - 1) * size;

        var where = new System.Text.StringBuilder("WHERE mr.teacher_username = @u");
        var parms = new List<MySqlParameter>();
        parms.Add(new MySqlParameter("@u", auth.UserId));

        if (!string.IsNullOrEmpty(status))
        {
            where.Append(" AND mr.status = @st");
            parms.Add(new MySqlParameter("@st", status.ToUpper()));
        }

        var countParms = new List<MySqlParameter>(parms);
        int total = Convert.ToInt32(ApiHelper.Scalar(
            "SELECT COUNT(*) FROM acad_mark_requests mr " + where, countParms.ToArray()));

        parms.Add(new MySqlParameter("@lim", size));
        parms.Add(new MySqlParameter("@off", offset));

        DataTable dt = ApiHelper.Query(
            MARK_REQUEST_SELECT + " " + where + " ORDER BY mr.created_at DESC LIMIT @lim OFFSET @off",
            parms.ToArray());

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "total", total }, { "page", page }, { "size", size },
            { "pages", (int)Math.Ceiling(total / (double)size) },
            { "requests", ApiHelper.TableToList(dt) }
        });
    }

    private void HandleCreateMarkRequest()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        EnsureMarkRequestsSchema();

        string courseId    = ApiHelper.RequireParam(Request, Response, "course_id"); if (courseId == null) return;
        string regno       = ApiHelper.RequireParam(Request, Response, "regno");     if (regno == null) return;
        string reason      = ApiHelper.RequireParam(Request, Response, "reason");    if (reason == null) return;
        string acadYear    = ApiHelper.Param(Request, "acad_year", "");
        int    semester    = ApiHelper.ParamInt(Request, "semester", 1);
        string requestType = ApiHelper.Param(Request, "request_type", "MARK_CHANGE").ToUpper();
        int    regId       = ApiHelper.ParamInt(Request, "registration_id", 0);

        // Validate request_type
        bool validType = false;
        foreach (string t in VALID_REQUEST_TYPES) if (t == requestType) { validType = true; break; }
        if (!validType)
        {
            ApiHelper.Error(Response, "Invalid request_type. Valid values: " + string.Join(", ", VALID_REQUEST_TYPES), "VALIDATION_ERROR");
            return;
        }

        // Parse optional mark values
        decimal? oldCw = null, oldExam = null, newCw = null, newExam = null;
        string oldCwStr   = ApiHelper.Param(Request, "old_cw",   "");
        string oldExamStr = ApiHelper.Param(Request, "old_exam", "");
        string newCwStr   = ApiHelper.Param(Request, "new_cw",   "");
        string newExamStr = ApiHelper.Param(Request, "new_exam", "");

        decimal tmp;
        if (!string.IsNullOrEmpty(oldCwStr))
        {
            if (!decimal.TryParse(oldCwStr, out tmp) || tmp < 0 || tmp > 40)
            { ApiHelper.Error(Response, "old_cw must be a number between 0 and 40.", "VALIDATION_ERROR"); return; }
            oldCw = tmp;
        }
        if (!string.IsNullOrEmpty(oldExamStr))
        {
            if (!decimal.TryParse(oldExamStr, out tmp) || tmp < 0 || tmp > 60)
            { ApiHelper.Error(Response, "old_exam must be a number between 0 and 60.", "VALIDATION_ERROR"); return; }
            oldExam = tmp;
        }
        if (!string.IsNullOrEmpty(newCwStr))
        {
            if (!decimal.TryParse(newCwStr, out tmp) || tmp < 0 || tmp > 40)
            { ApiHelper.Error(Response, "new_cw must be a number between 0 and 40.", "VALIDATION_ERROR"); return; }
            newCw = tmp;
        }
        if (!string.IsNullOrEmpty(newExamStr))
        {
            if (!decimal.TryParse(newExamStr, out tmp) || tmp < 0 || tmp > 60)
            { ApiHelper.Error(Response, "new_exam must be a number between 0 and 60.", "VALIDATION_ERROR"); return; }
            newExam = tmp;
        }

        // If registration_id supplied, fetch existing marks automatically to populate old_cw/old_exam
        if (regId > 0 && (oldCw == null || oldExam == null))
        {
            DataTable regDt = ApiHelper.Query(
                @"SELECT provisional_course_work_marks, provisional_exam_marks
                  FROM campus_dynamics_portal.acad_course_registration WHERE id = @id LIMIT 1",
                new MySqlParameter("@id", regId));
            if (regDt.Rows.Count > 0)
            {
                if (oldCw == null && regDt.Rows[0]["provisional_course_work_marks"] != DBNull.Value)
                    oldCw = Convert.ToDecimal(regDt.Rows[0]["provisional_course_work_marks"]);
                if (oldExam == null && regDt.Rows[0]["provisional_exam_marks"] != DBNull.Value)
                    oldExam = Convert.ToDecimal(regDt.Rows[0]["provisional_exam_marks"]);
            }
        }

        // Guard: block duplicate PENDING request for same teacher/course/student/semester
        int pendingCount = Convert.ToInt32(ApiHelper.Scalar(
            @"SELECT COUNT(*) FROM acad_mark_requests
              WHERE teacher_username = @u AND course_id = @cid AND regno = @r
                AND acad_year = @ay AND semester = @sem AND status = 'PENDING'",
            new MySqlParameter("@u",   auth.UserId),
            new MySqlParameter("@cid", courseId),
            new MySqlParameter("@r",   regno),
            new MySqlParameter("@ay",  acadYear),
            new MySqlParameter("@sem", semester)));

        if (pendingCount > 0)
        {
            ApiHelper.Error(Response,
                "A pending mark request already exists for this student and course. " +
                "Cancel or wait for the existing request to be decided before submitting another.",
                "DUPLICATE_REQUEST");
            return;
        }

        long newId = ApiHelper.ExecuteInsert(
            @"INSERT INTO acad_mark_requests
                (teacher_username, course_id, regno, registration_id, acad_year, semester,
                 request_type, reason, old_cw, old_exam, new_cw, new_exam)
              VALUES (@u, @cid, @r, @regid, @ay, @sem, @rt, @rs, @ocw, @oex, @ncw, @nex)",
            new MySqlParameter("@u",     auth.UserId),
            new MySqlParameter("@cid",   courseId),
            new MySqlParameter("@r",     regno),
            new MySqlParameter("@regid", regId > 0 ? (object)regId : DBNull.Value),
            new MySqlParameter("@ay",    acadYear),
            new MySqlParameter("@sem",   semester),
            new MySqlParameter("@rt",    requestType),
            new MySqlParameter("@rs",    reason),
            new MySqlParameter("@ocw",   oldCw.HasValue   ? (object)oldCw.Value   : DBNull.Value),
            new MySqlParameter("@oex",   oldExam.HasValue ? (object)oldExam.Value : DBNull.Value),
            new MySqlParameter("@ncw",   newCw.HasValue   ? (object)newCw.Value   : DBNull.Value),
            new MySqlParameter("@nex",   newExam.HasValue ? (object)newExam.Value : DBNull.Value));

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "id",           newId },
            { "status",       "PENDING" },
            { "request_type", requestType },
            { "old_cw",       oldCw },
            { "old_exam",     oldExam },
            { "new_cw",       newCw },
            { "new_exam",     newExam }
        }, "Mark change request submitted and is pending admin review");
    }

    private void HandleMarkRequestDetail()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        EnsureMarkRequestsSchema();

        int id = ApiHelper.ParamInt(Request, "id", 0);
        if (id <= 0) { ApiHelper.Error(Response, "id is required.", "MISSING_PARAM"); return; }

        // Teacher can only see their own; admin staff can pass ?admin=1 to bypass that restriction
        bool isAdmin = ApiHelper.Param(Request, "admin", "0") == "1";
        string whereClause = isAdmin
            ? "WHERE mr.id = @id LIMIT 1"
            : "WHERE mr.id = @id AND mr.teacher_username = @u LIMIT 1";

        var parms = new List<MySqlParameter> { new MySqlParameter("@id", id) };
        if (!isAdmin) parms.Add(new MySqlParameter("@u", auth.UserId));

        DataTable dt = ApiHelper.Query(MARK_REQUEST_SELECT + " " + whereClause, parms.ToArray());

        if (dt.Rows.Count == 0) { ApiHelper.Error(Response, "Request not found.", "NOT_FOUND"); return; }

        ApiHelper.Success(Response, ApiHelper.FirstRowToDict(dt));
    }

    private void HandleCancelMarkRequest()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        EnsureMarkRequestsSchema();

        int id = ApiHelper.ParamInt(Request, "id", 0);
        if (id <= 0) { ApiHelper.Error(Response, "id is required.", "MISSING_PARAM"); return; }

        // Fetch the request — must belong to this teacher and be PENDING
        DataTable dt = ApiHelper.Query(
            "SELECT id, status FROM acad_mark_requests WHERE id = @id AND teacher_username = @u LIMIT 1",
            new MySqlParameter("@id", id),
            new MySqlParameter("@u",  auth.UserId));

        if (dt.Rows.Count == 0)
        {
            ApiHelper.Error(Response, "Request not found.", "NOT_FOUND");
            return;
        }

        string currentStatus = dt.Rows[0]["status"].ToString();
        if (currentStatus != "PENDING")
        {
            ApiHelper.Error(Response,
                "Only PENDING requests can be cancelled. This request is " + currentStatus + ".",
                "INVALID_STATUS");
            return;
        }

        ApiHelper.Execute(
            "UPDATE acad_mark_requests SET status = 'CANCELLED', decided_at = NOW() WHERE id = @id",
            new MySqlParameter("@id", id));

        ApiHelper.Success(Response, new Dictionary<string, object> { { "id", id }, { "status", "CANCELLED" } },
            "Mark request cancelled");
    }

    private void HandleAdminMarkRequests()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        EnsureMarkRequestsSchema();

        string status          = ApiHelper.Param(Request, "status",           "PENDING");
        string teacherUsername = ApiHelper.Param(Request, "teacher_username", "");
        string filterCourseId  = ApiHelper.Param(Request, "course_id",        "");
        string filterAcadYear  = ApiHelper.Param(Request, "acad_year",        "");
        int    filterSemester  = ApiHelper.ParamInt(Request, "semester",      0);
        int    page            = Math.Max(1, ApiHelper.ParamInt(Request, "page", 1));
        int    size            = Math.Min(100, Math.Max(1, ApiHelper.ParamInt(Request, "size", 20)));
        int    offset          = (page - 1) * size;

        var where = new System.Text.StringBuilder("WHERE 1=1");
        var parms = new List<MySqlParameter>();

        if (!string.IsNullOrEmpty(status))          { where.Append(" AND mr.status = @st");  parms.Add(new MySqlParameter("@st",  status.ToUpper())); }
        if (!string.IsNullOrEmpty(teacherUsername)) { where.Append(" AND mr.teacher_username = @tu"); parms.Add(new MySqlParameter("@tu", teacherUsername)); }
        if (!string.IsNullOrEmpty(filterCourseId)) { where.Append(" AND mr.course_id = @ci"); parms.Add(new MySqlParameter("@ci", filterCourseId)); }
        if (!string.IsNullOrEmpty(filterAcadYear)) { where.Append(" AND mr.acad_year = @ay"); parms.Add(new MySqlParameter("@ay", filterAcadYear)); }
        if (filterSemester > 0)                    { where.Append(" AND mr.semester = @sem"); parms.Add(new MySqlParameter("@sem", filterSemester)); }

        var countParms = new List<MySqlParameter>(parms);
        int total = Convert.ToInt32(ApiHelper.Scalar(
            "SELECT COUNT(*) FROM acad_mark_requests mr " + where, countParms.ToArray()));

        parms.Add(new MySqlParameter("@lim", size));
        parms.Add(new MySqlParameter("@off", offset));

        DataTable dt = ApiHelper.Query(
            MARK_REQUEST_SELECT + " " + where + " ORDER BY mr.created_at DESC LIMIT @lim OFFSET @off",
            parms.ToArray());

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "total", total }, { "page", page }, { "size", size },
            { "pages", (int)Math.Ceiling(total / (double)size) },
            { "requests", ApiHelper.TableToList(dt) }
        });
    }

    private void HandleDecideMarkRequest()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        EnsureMarkRequestsSchema();

        int    id           = ApiHelper.ParamInt(Request, "id", 0);
        string decision     = ApiHelper.Param(Request, "decision", "").ToUpper();
        string adminComment = ApiHelper.Param(Request, "admin_comment", "");

        if (id <= 0)
        {
            ApiHelper.Error(Response, "id is required.", "MISSING_PARAM"); return;
        }
        if (decision != "APPROVED" && decision != "REJECTED")
        {
            ApiHelper.Error(Response, "decision must be APPROVED or REJECTED.", "VALIDATION_ERROR"); return;
        }

        // Fetch the request — must be PENDING
        DataTable dt = ApiHelper.Query(
            @"SELECT id, teacher_username, course_id, regno, registration_id,
                     acad_year, semester, request_type, new_cw, new_exam, status
              FROM acad_mark_requests WHERE id = @id LIMIT 1",
            new MySqlParameter("@id", id));

        if (dt.Rows.Count == 0)
        {
            ApiHelper.Error(Response, "Request not found.", "NOT_FOUND"); return;
        }

        string currentStatus = dt.Rows[0]["status"].ToString();
        if (currentStatus != "PENDING")
        {
            ApiHelper.Error(Response,
                "This request has already been decided (status: " + currentStatus + ").",
                "INVALID_STATUS");
            return;
        }

        // Update the request record
        ApiHelper.Execute(
            @"UPDATE acad_mark_requests
              SET status = @dec, admin_comment = @cmt, decided_by = @by, decided_at = NOW()
              WHERE id = @id",
            new MySqlParameter("@dec", decision),
            new MySqlParameter("@cmt", adminComment),
            new MySqlParameter("@by",  auth.UserId),
            new MySqlParameter("@id",  id));

        string markUpdateResult = null;

        // When APPROVED and new marks are supplied, apply the mark change to acad_course_registration
        if (decision == "APPROVED")
        {
            object regIdObj = dt.Rows[0]["registration_id"];
            bool hasRegId = regIdObj != DBNull.Value && regIdObj != null;
            int regId = hasRegId ? Convert.ToInt32(regIdObj) : 0;

            bool hasNewCw   = dt.Rows[0]["new_cw"]   != DBNull.Value && dt.Rows[0]["new_cw"] != null;
            bool hasNewExam = dt.Rows[0]["new_exam"]  != DBNull.Value && dt.Rows[0]["new_exam"] != null;

            if (regId > 0 && (hasNewCw || hasNewExam))
            {
                try
                {
                    // Fetch current marks to fill in any partial values
                    DataTable regDt = ApiHelper.Query(
                        @"SELECT provisional_course_work_marks, provisional_exam_marks, provisional_marks_status
                          FROM campus_dynamics_portal.acad_course_registration WHERE id = @id LIMIT 1",
                        new MySqlParameter("@id", regId));

                    if (regDt.Rows.Count > 0)
                    {
                        decimal curCw   = regDt.Rows[0]["provisional_course_work_marks"] != DBNull.Value
                            ? Convert.ToDecimal(regDt.Rows[0]["provisional_course_work_marks"]) : 0m;
                        decimal curExam = regDt.Rows[0]["provisional_exam_marks"] != DBNull.Value
                            ? Convert.ToDecimal(regDt.Rows[0]["provisional_exam_marks"]) : 0m;

                        decimal applyCw   = hasNewCw   ? Convert.ToDecimal(dt.Rows[0]["new_cw"])   : curCw;
                        decimal applyExam = hasNewExam ? Convert.ToDecimal(dt.Rows[0]["new_exam"]) : curExam;
                        decimal applyTotal = applyCw + applyExam;

                        // Reset status to 'approved' (unlocks from published so the change takes effect)
                        ApiHelper.Execute(
                            @"UPDATE campus_dynamics_portal.acad_course_registration
                              SET provisional_course_work_marks = @cw,
                                  provisional_exam_marks        = @ex,
                                  provisional_total_marks       = @tot,
                                  provisional_marks_status      = 'approved'
                              WHERE id = @id",
                            new MySqlParameter("@cw",  applyCw),
                            new MySqlParameter("@ex",  applyExam),
                            new MySqlParameter("@tot", applyTotal),
                            new MySqlParameter("@id",  regId));

                        markUpdateResult = string.Format(
                            "Marks updated: CW={0}, Exam={1}, Total={2}",
                            applyCw, applyExam, applyTotal);
                    }
                    else
                    {
                        markUpdateResult = "Warning: registration record " + regId + " not found; marks not updated.";
                    }
                }
                catch (Exception ex)
                {
                    markUpdateResult = "Request approved but mark update failed: " + ex.Message;
                }
            }
            else if (regId <= 0)
            {
                markUpdateResult = "Request approved. No registration_id supplied — apply marks manually.";
            }
            else
            {
                markUpdateResult = "Request approved. No new mark values supplied — no marks were changed.";
            }
        }

        var responseData = new Dictionary<string, object>
        {
            { "id",       id },
            { "decision", decision },
            { "decided_by", auth.UserId }
        };
        if (markUpdateResult != null) responseData["mark_update"] = markUpdateResult;

        ApiHelper.Success(Response, responseData,
            decision == "APPROVED" ? "Request approved" : "Request rejected");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  DASHBOARD STATS
    // ══════════════════════════════════════════════════════════════════════════

    private void HandleDashboardStats()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff")
        {
            ApiHelper.Error(Response, "This endpoint is for staff members only.", "ACCESS_DENIED");
            return;
        }

        // ── Resolve staff record ────────────────────────────────────────────
        DataTable empDt = ApiHelper.Query(
            @"SELECT e.empID, e.emp_name, e.EMP_CODE, e.EmpType,
                     d.dept_name AS department
              FROM hrm_employee e
              LEFT JOIN hrm_department d ON d.dept_id = e.dept_id
              WHERE (NULLIF(TRIM(IFNULL(e.usernames,'')),'-') IS NOT NULL
                     AND UPPER(TRIM(e.usernames)) = UPPER(@uid))
                 OR UPPER(TRIM(IFNULL(e.EMP_CODE,''))) = UPPER(@uid2)
              LIMIT 1",
            new MySqlParameter("@uid",  auth.UserId),
            new MySqlParameter("@uid2", auth.UserId));

        if (empDt.Rows.Count == 0)
        {
            ApiHelper.Error(Response, "Staff profile not found.", "NOT_FOUND");
            return;
        }

        int    staffId   = Convert.ToInt32(empDt.Rows[0]["empID"]);
        string staffName = empDt.Rows[0]["emp_name"].ToString();

        // ── Build lecturer's course-code list (upper-cased) ─────────────────
        // Primary source: acad_programmecourses (is_lecturere_assigned='YES')
        DataTable codesDt = ApiHelper.Query(
            @"SELECT DISTINCT UPPER(TRIM(pc.course_code)) AS cc
              FROM acad_programmecourses pc
              WHERE IFNULL(pc.lecturer_id,0) = @sid
                AND UPPER(IFNULL(pc.is_lecturere_assigned,'NO')) = 'YES'
              UNION
              SELECT DISTINCT UPPER(TRIM(ta.course_id)) AS cc
              FROM acad_teaching_assignments ta
              WHERE UPPER(TRIM(ta.teacher_username)) = UPPER(@uname)
                AND ta.is_active = 1
                AND ta.course_id IS NOT NULL AND ta.course_id <> ''
              UNION
              SELECT DISTINCT UPPER(TRIM(al.courseID)) AS cc
              FROM acad_teaching_allocation al
              WHERE UPPER(TRIM(al.staffCode)) = UPPER(@uname2)
                AND al.courseID IS NOT NULL AND al.courseID <> ''",
            new MySqlParameter("@sid",   staffId),
            new MySqlParameter("@uname", auth.UserId),
            new MySqlParameter("@uname2",auth.UserId));

        var myCodes = new List<string>();
        foreach (DataRow dr in codesDt.Rows)
            if (dr["cc"] != DBNull.Value && !string.IsNullOrEmpty(dr["cc"].ToString()))
                myCodes.Add(dr["cc"].ToString().ToUpper().Trim());

        // ── 1. Assigned courses — all time (acad_programmecourses) ──────────
        long assignedCoursesTotal = Convert.ToInt64(ApiHelper.Scalar(
            @"SELECT COUNT(DISTINCT course_code)
              FROM acad_programmecourses
              WHERE IFNULL(lecturer_id,0) = @sid
                AND UPPER(IFNULL(is_lecturere_assigned,'NO')) = 'YES'",
            new MySqlParameter("@sid", staffId)) ?? 0L);

        // ── 2. Current academic year / semester (from system settings) ───────
        string currentYear = "";
        int    currentSem  = 0;
        try
        {
            DataTable sysDt = ApiHelper.Query(
                "SELECT acad_year, semester FROM acad_semester WHERE is_current='1' OR is_current='Yes' LIMIT 1");
            if (sysDt.Rows.Count > 0)
            {
                currentYear = sysDt.Rows[0]["acad_year"].ToString();
                int.TryParse(sysDt.Rows[0]["semester"].ToString(), out currentSem);
            }
        }
        catch { }

        // Fallback: most recent year/semester from acad_registration
        if (string.IsNullOrEmpty(currentYear))
        {
            try
            {
                DataTable regDt = ApiHelper.Query(
                    "SELECT acad_year, MAX(semester) AS semester FROM acad_registration GROUP BY acad_year ORDER BY acad_year DESC LIMIT 1");
                if (regDt.Rows.Count > 0)
                {
                    currentYear = regDt.Rows[0]["acad_year"].ToString();
                    int.TryParse(regDt.Rows[0]["semester"].ToString(), out currentSem);
                }
            }
            catch { }
        }

        // ── 3-6. Student & mark stats from acad_course_registration ─────────
        long totalStudents      = 0;
        long totalRegistrations = 0;
        long pendingCW          = 0;
        long pendingExam        = 0;
        long bothMissing        = 0;
        long fullyEntered       = 0;
        long studentsThisSem    = 0;

        if (myCodes.Count > 0)
        {
            // Build IN list (parameterised)
            var inParams = new List<MySqlParameter>();
            var inPlaceholders = new List<string>();
            for (int i = 0; i < myCodes.Count; i++)
            {
                string p = "@cc" + i;
                inPlaceholders.Add(p);
                inParams.Add(new MySqlParameter(p, myCodes[i]));
            }
            string inClause = string.Join(",", inPlaceholders);

            string crSql = @"
                SELECT COUNT(*)                                                      AS total_rows,
                       COUNT(DISTINCT cr.regno)                                      AS total_students,
                       SUM(CASE WHEN cr.provisional_course_work_marks IS NULL
                                  OR cr.provisional_course_work_marks = 0 THEN 1 ELSE 0 END) AS pending_cw,
                       SUM(CASE WHEN cr.provisional_exam_marks IS NULL
                                  OR cr.provisional_exam_marks = 0       THEN 1 ELSE 0 END) AS pending_exam,
                       SUM(CASE WHEN (cr.provisional_course_work_marks IS NULL OR cr.provisional_course_work_marks = 0)
                                 AND (cr.provisional_exam_marks IS NULL OR cr.provisional_exam_marks = 0) THEN 1 ELSE 0 END) AS both_missing,
                       SUM(CASE WHEN cr.provisional_course_work_marks IS NOT NULL AND cr.provisional_course_work_marks > 0
                                 AND cr.provisional_exam_marks IS NOT NULL AND cr.provisional_exam_marks > 0 THEN 1 ELSE 0 END) AS fully_entered
                FROM campus_dynamics_portal.acad_course_registration cr
                WHERE UPPER(TRIM(cr.courseID)) IN (" + inClause + ")";

            DataTable crDt = ApiHelper.Query(crSql, inParams.ToArray());
            if (crDt.Rows.Count > 0)
            {
                DataRow r = crDt.Rows[0];
                Func<string, long> N = col => {
                    if (r[col] == DBNull.Value) return 0;
                    long v; long.TryParse(r[col].ToString(), out v); return v;
                };
                totalRegistrations = N("total_rows");
                totalStudents      = N("total_students");
                pendingCW          = N("pending_cw");
                pendingExam        = N("pending_exam");
                bothMissing        = N("both_missing");
                fullyEntered       = N("fully_entered");
            }

            // Students this semester (filtered by current year/sem)
            if (!string.IsNullOrEmpty(currentYear) && currentSem > 0)
            {
                var semParams = new List<MySqlParameter>(inParams);
                semParams.Add(new MySqlParameter("@ay",  currentYear));
                semParams.Add(new MySqlParameter("@sem", currentSem));
                DataTable semDt = ApiHelper.Query(
                    "SELECT COUNT(DISTINCT cr.regno) AS cnt " +
                    "FROM campus_dynamics_portal.acad_course_registration cr " +
                    "WHERE UPPER(TRIM(cr.courseID)) IN (" + inClause + ") " +
                    "AND cr.acad_year = @ay AND cr.semester = @sem",
                    semParams.ToArray());
                if (semDt.Rows.Count > 0 && semDt.Rows[0]["cnt"] != DBNull.Value)
                    long.TryParse(semDt.Rows[0]["cnt"].ToString(), out studentsThisSem);
            }
        }

        // ── 7. Results already submitted (acad_results) ──────────────────────
        long resultsEntered = 0;
        if (myCodes.Count > 0)
        {
            var inParams2 = new List<MySqlParameter>();
            var inPH2     = new List<string>();
            for (int i = 0; i < myCodes.Count; i++)
            {
                string p = "@rc" + i;
                inPH2.Add(p);
                inParams2.Add(new MySqlParameter(p, myCodes[i]));
            }
            DataTable resDt = ApiHelper.Query(
                "SELECT COUNT(*) AS cnt FROM acad_results " +
                "WHERE UPPER(TRIM(courseid)) IN (" + string.Join(",", inPH2) + ")",
                inParams2.ToArray());
            if (resDt.Rows.Count > 0 && resDt.Rows[0]["cnt"] != DBNull.Value)
                long.TryParse(resDt.Rows[0]["cnt"].ToString(), out resultsEntered);
        }

        // ── 8. Mark requests breakdown ────────────────────────────────────────
        long mrPendingLecturer  = 0;
        long mrPendingSupervisor= 0;
        long mrPendingAdmin     = 0;
        long mrApproved         = 0;
        long mrRejected         = 0;
        long mrTotal            = 0;
        try
        {
            DataTable mrDt = ApiHelper.Query(
                @"SELECT
                    SUM(CASE WHEN status='PENDING_LECTURER'  THEN 1 ELSE 0 END) AS pl,
                    SUM(CASE WHEN status='PENDING_SUPERVISOR'THEN 1 ELSE 0 END) AS ps,
                    SUM(CASE WHEN status='PENDING_ADMIN'     THEN 1 ELSE 0 END) AS pa,
                    SUM(CASE WHEN status='APPROVED'          THEN 1 ELSE 0 END) AS ap,
                    SUM(CASE WHEN status='REJECTED'          THEN 1 ELSE 0 END) AS rj,
                    COUNT(*)                                                     AS total
                  FROM campus_dynamics_portal.acad_marks_requests
                  WHERE lecturer_id = @sid",
                new MySqlParameter("@sid", staffId));
            if (mrDt.Rows.Count > 0)
            {
                DataRow r = mrDt.Rows[0];
                Func<string, long> N = col => {
                    if (r[col] == DBNull.Value) return 0;
                    long v; long.TryParse(r[col].ToString(), out v); return v;
                };
                mrPendingLecturer   = N("pl");
                mrPendingSupervisor = N("ps");
                mrPendingAdmin      = N("pa");
                mrApproved          = N("ap");
                mrRejected          = N("rj");
                mrTotal             = N("total");
            }
        }
        catch { }

        // ── 9. Mark sheet status (acad_results_status) ────────────────────────
        long sheetsSubmitted = 0;
        long sheetsApproved  = 0;
        long sheetsDraft     = 0;
        try
        {
            if (myCodes.Count > 0)
            {
                var inParams3 = new List<MySqlParameter>();
                var inPH3     = new List<string>();
                for (int i = 0; i < myCodes.Count; i++)
                {
                    string p = "@sc" + i;
                    inPH3.Add(p);
                    inParams3.Add(new MySqlParameter(p, myCodes[i]));
                }
                DataTable sheetDt = ApiHelper.Query(
                    "SELECT " +
                    "SUM(CASE WHEN UPPER(status)='SUBMITTED' THEN 1 ELSE 0 END) AS submitted, " +
                    "SUM(CASE WHEN UPPER(status)='APPROVED'  THEN 1 ELSE 0 END) AS approved, " +
                    "SUM(CASE WHEN UPPER(status)='DRAFT'     THEN 1 ELSE 0 END) AS draft " +
                    "FROM acad_results_status " +
                    "WHERE UPPER(TRIM(course_id)) IN (" + string.Join(",", inPH3) + ")",
                    inParams3.ToArray());
                if (sheetDt.Rows.Count > 0)
                {
                    DataRow r = sheetDt.Rows[0];
                    Func<string, long> N = col => {
                        if (r[col] == DBNull.Value) return 0;
                        long v; long.TryParse(r[col].ToString(), out v); return v;
                    };
                    sheetsSubmitted = N("submitted");
                    sheetsApproved  = N("approved");
                    sheetsDraft     = N("draft");
                }
            }
        }
        catch { }

        // ── 10. Mark requests received in last 7 and 30 days ──────────────────
        long mrLast7Days  = 0;
        long mrLast30Days = 0;
        try
        {
            DataTable recentDt = ApiHelper.Query(
                @"SELECT
                    SUM(CASE WHEN created_at >= DATE_SUB(NOW(), INTERVAL 7  DAY) THEN 1 ELSE 0 END) AS last7,
                    SUM(CASE WHEN created_at >= DATE_SUB(NOW(), INTERVAL 30 DAY) THEN 1 ELSE 0 END) AS last30
                  FROM campus_dynamics_portal.acad_marks_requests
                  WHERE lecturer_id = @sid",
                new MySqlParameter("@sid", staffId));
            if (recentDt.Rows.Count > 0)
            {
                DataRow r = recentDt.Rows[0];
                Func<string, long> N = col => {
                    if (r[col] == DBNull.Value) return 0;
                    long v; long.TryParse(r[col].ToString(), out v); return v;
                };
                mrLast7Days  = N("last7");
                mrLast30Days = N("last30");
            }
        }
        catch { }

        // ── Compute completion rate ───────────────────────────────────────────
        double completionRate = totalRegistrations > 0
            ? Math.Round((double)fullyEntered / totalRegistrations * 100.0, 1)
            : 0.0;

        // ── Build response ────────────────────────────────────────────────────
        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            // Lecturer identity
            { "staff_id",   staffId },
            { "staff_name", staffName },
            { "current_acad_year", currentYear },
            { "current_semester",  currentSem  },

            // ── Courses ──
            { "assigned_courses", new Dictionary<string, object>
                {
                    // Total unique course codes ever assigned (all time)
                    { "total",           assignedCoursesTotal },
                    // Number of distinct course code entries tracked in system
                    { "unique_codes",    myCodes.Count },
                }
            },

            // ── Students ──
            { "students", new Dictionary<string, object>
                {
                    // Distinct students registered in any of this lecturer's courses (all time)
                    { "total_in_my_courses",   totalStudents },
                    // Distinct students this semester only
                    { "this_semester",         studentsThisSem },
                    // Total registration rows (one student can be in multiple courses)
                    { "total_registrations",   totalRegistrations },
                }
            },

            // ── Mark entry progress ──
            // These show how many student-course registrations are missing marks.
            // pending_coursework = registrations where CW mark is NULL or 0.
            // pending_exam       = registrations where Exam mark is NULL or 0.
            // both_missing       = neither CW nor Exam entered yet.
            // fully_entered      = both CW and Exam are present and > 0.
            // completion_rate    = % of registrations with both marks entered.
            { "marks", new Dictionary<string, object>
                {
                    { "pending_coursework",  pendingCW     },
                    { "pending_exam_marks",  pendingExam   },
                    { "both_missing",        bothMissing   },
                    { "fully_entered",       fullyEntered  },
                    { "results_in_system",   resultsEntered},
                    { "completion_rate_pct", completionRate},
                }
            },

            // ── Mark requests (portal student workflow) ──
            // pending_lecturer   = NEW requests awaiting YOUR response (action required).
            // pending_supervisor = You've responded; awaiting supervisor sign-off.
            // pending_admin      = Awaiting registry/admin final approval.
            // approved           = Fully resolved and marks published.
            // rejected           = Rejected at any stage.
            { "mark_requests", new Dictionary<string, object>
                {
                    { "pending_lecturer",   mrPendingLecturer  },
                    { "pending_supervisor", mrPendingSupervisor},
                    { "pending_admin",      mrPendingAdmin     },
                    { "approved",           mrApproved         },
                    { "rejected",           mrRejected         },
                    { "total",              mrTotal            },
                    { "new_last_7_days",    mrLast7Days        },
                    { "new_last_30_days",   mrLast30Days       },
                }
            },

            // ── Mark sheets (formal submission workflow) ──
            // draft     = Sheets created but not yet submitted for approval.
            // submitted = Submitted to dean/HOD for approval.
            // approved  = Approved and finalised.
            { "mark_sheets", new Dictionary<string, object>
                {
                    { "draft",     sheetsDraft     },
                    { "submitted", sheetsSubmitted },
                    { "approved",  sheetsApproved  },
                }
            },
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  LECTURER MARK REQUESTS (portal acad_marks_requests workflow)
    // ══════════════════════════════════════════════════════════════════════════

    private int GetSupervisorIdForLecturer(int staffId)
    {
        if (staffId <= 0) return 0;
        try
        {
            object obj = ApiHelper.Scalar(
                "SELECT IFNULL(supervisorID,0) FROM hrm_employee WHERE empID=@sid LIMIT 1",
                new MySqlParameter("@sid", staffId));
            int sup;
            return (obj != null && obj != DBNull.Value && int.TryParse(obj.ToString(), out sup)) ? sup : 0;
        }
        catch { return 0; }
    }

    private void HandleLmrRequests()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff")
        {
            ApiHelper.Error(Response, "This endpoint is for staff members only.", "ACCESS_DENIED");
            return;
        }

        string empIdStr = GetLecturerEmpId(auth.UserId);
        if (string.IsNullOrEmpty(empIdStr))
        {
            ApiHelper.Error(Response, "Staff profile not found.", "NOT_FOUND");
            return;
        }
        int staffId = int.Parse(empIdStr);

        string statusFilter = ApiHelper.Param(Request, "status", "ALL").Trim().ToUpper();
        bool allStatus = string.IsNullOrEmpty(statusFilter) || statusFilter == "ALL";

        var parms = new List<MySqlParameter> { new MySqlParameter("@sid", staffId) };
        string whereStatus = allStatus ? "" : " AND r.status = @sf ";
        if (!allStatus) parms.Add(new MySqlParameter("@sf", statusFilter));

        string sql = @"
            SELECT r.id, r.regno,
                   IFNULL(s.entryno, '') AS entry_no,
                   IFNULL(NULLIF(TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))),''), r.regno) AS student_name,
                   r.course_id, IFNULL(c.courseName, r.course_id) AS course_name,
                   r.acad_year, r.semester, r.request_type, r.student_reason, r.status,
                   IFNULL(r.lecturer_response,'') AS lecturer_response,
                   IFNULL(r.supervisor_response,'') AS supervisor_response,
                   IFNULL(r.admin_response,'') AS admin_response,
                   r.proposed_cw, r.proposed_exam, r.proposed_total,
                   cr.provisional_course_work_marks AS original_cw,
                   cr.provisional_exam_marks AS original_exam,
                   IFNULL(ar.score, cr.provisional_total_marks) AS original_total,
                   IFNULL(ar.grade, '') AS original_grade,
                   DATE_FORMAT(r.created_at,'%Y-%m-%d %H:%i') AS created_at,
                   DATE_FORMAT(r.updated_at,'%Y-%m-%d %H:%i') AS updated_at
            FROM campus_dynamics_portal.acad_marks_requests r
            LEFT JOIN acad_course c ON c.courseID = r.course_id
            LEFT JOIN acad_student s ON s.regno = r.regno
            LEFT JOIN campus_dynamics_portal.acad_course_registration cr ON cr.id = r.course_reg_id
            LEFT JOIN acad_results ar ON ar.regno = r.regno
                AND ar.courseid = r.course_id AND ar.acad = r.acad_year AND ar.semester = r.semester
            WHERE r.lecturer_id = @sid
            " + whereStatus + @"
            ORDER BY CASE r.status
                WHEN 'PENDING_LECTURER' THEN 0
                WHEN 'PENDING_SUPERVISOR' THEN 1
                WHEN 'PENDING_ADMIN' THEN 2
                ELSE 3
            END, r.created_at DESC
            LIMIT 500";

        DataTable dt;
        try
        {
            dt = ApiHelper.Query(sql, parms.ToArray());
        }
        catch
        {
            // Fallback: drop student name concat in case acad_student schema differs
            sql = sql.Replace(
                "IFNULL(NULLIF(TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))),''), r.regno) AS student_name",
                "r.regno AS student_name");
            dt = ApiHelper.Query(sql, parms.ToArray());
        }

        int supervisorId = GetSupervisorIdForLecturer(staffId);

        var rows = new List<Dictionary<string, object>>();
        foreach (DataRow dr in dt.Rows)
        {
            var row = new Dictionary<string, object>();
            foreach (DataColumn col in dt.Columns)
            {
                object val = dr[col];
                if (val is DBNull) val = null;
                row[col.ColumnName] = val;
            }
            string status = (row["status"] ?? "").ToString();
            row["can_review"] = string.Equals(status, "PENDING_LECTURER", StringComparison.OrdinalIgnoreCase);
            rows.Add(row);
        }

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "requests", rows },
            { "total", rows.Count },
            { "lecturer_has_supervisor", supervisorId > 0 }
        });
    }

    private void HandleLmrRespond()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff")
        {
            ApiHelper.Error(Response, "This endpoint is for staff members only.", "ACCESS_DENIED");
            return;
        }

        string empIdStr = GetLecturerEmpId(auth.UserId);
        if (string.IsNullOrEmpty(empIdStr))
        {
            ApiHelper.Error(Response, "Staff profile not found.", "NOT_FOUND");
            return;
        }
        int staffId = int.Parse(empIdStr);

        int requestId = ApiHelper.ParamInt(Request, "request_id", 0);
        if (requestId <= 0)
        {
            ApiHelper.Error(Response, "Missing required parameter: request_id.", "MISSING_PARAM");
            return;
        }

        string lecturerResponse = ApiHelper.Param(Request, "response", "").Trim();

        // Parse nullable proposed_cw / proposed_exam
        int? proposedCw = null, proposedExam = null;
        string cwStr = ApiHelper.Param(Request, "proposed_cw", "");
        string exStr = ApiHelper.Param(Request, "proposed_exam", "");
        int cwVal, exVal;
        if (!string.IsNullOrEmpty(cwStr) && int.TryParse(cwStr, out cwVal)) proposedCw = cwVal;
        if (!string.IsNullOrEmpty(exStr) && int.TryParse(exStr, out exVal)) proposedExam = exVal;

        if (proposedCw.HasValue && (proposedCw.Value < 0 || proposedCw.Value > 40))
        {
            ApiHelper.Error(Response, "Coursework mark must be between 0 and 40.", "VALIDATION_ERROR");
            return;
        }
        if (proposedExam.HasValue && (proposedExam.Value < 0 || proposedExam.Value > 60))
        {
            ApiHelper.Error(Response, "Exam mark must be between 0 and 60.", "VALIDATION_ERROR");
            return;
        }

        int supervisorId = GetSupervisorIdForLecturer(staffId);

        string reqType = "";
        string reqRegno = "";
        string reqCourseId = "";
        string reqAcadYear = "";
        int reqSemester = 0;
        int courseRegId = 0;
        bool marksPublished = false;
        int publishedTotal = 0;
        string nextStatus = "";

        using (var conn = ApiHelper.GetConnection())
        {
            conn.Open();

            using (var cmd = new MySqlCommand(@"
                SELECT IFNULL(request_type,'') AS request_type,
                       IFNULL(course_reg_id,0) AS course_reg_id,
                       IFNULL(regno,'') AS regno,
                       IFNULL(course_id,'') AS course_id,
                       IFNULL(acad_year,'') AS acad_year,
                       IFNULL(semester,0) AS semester
                FROM campus_dynamics_portal.acad_marks_requests
                WHERE id = @id AND lecturer_id = @sid
                LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@id",  requestId);
                cmd.Parameters.AddWithValue("@sid", staffId);
                using (var rdr = cmd.ExecuteReader(System.Data.CommandBehavior.SingleRow))
                {
                    if (!rdr.Read())
                    {
                        ApiHelper.Error(Response,
                            "Request not found or not assigned to you.",
                            "NOT_FOUND");
                        return;
                    }
                    reqType   = rdr["request_type"].ToString();
                    int.TryParse(rdr["course_reg_id"].ToString(), out courseRegId);
                    reqRegno    = rdr["regno"].ToString();
                    reqCourseId = rdr["course_id"].ToString();
                    reqAcadYear = rdr["acad_year"].ToString();
                    int.TryParse(rdr["semester"].ToString(), out reqSemester);
                }
            }

            if (reqType == "MARK_CHANGE" && (!proposedCw.HasValue || !proposedExam.HasValue))
            {
                ApiHelper.Error(Response,
                    "You must provide both coursework and exam marks for a mark change request.",
                    "VALIDATION_ERROR");
                return;
            }
            if (reqType == "MISSING_MARK" && !proposedCw.HasValue)
            {
                ApiHelper.Error(Response, "Please provide the coursework mark.", "VALIDATION_ERROR");
                return;
            }

            int? proposedTotal = (proposedCw.HasValue && proposedExam.HasValue)
                ? (int?)(proposedCw.Value + proposedExam.Value)
                : proposedCw;

            nextStatus = (reqType == "MARK_CHANGE" && supervisorId > 0) ? "PENDING_SUPERVISOR" : "APPROVED";

            using (var tx = conn.BeginTransaction())
            {
                using (var cmd = new MySqlCommand(@"
                    UPDATE campus_dynamics_portal.acad_marks_requests
                    SET lecturer_response     = @lresp,
                        lecturer_responded_at = NOW(),
                        supervisor_id         = @supid,
                        proposed_cw           = @cw,
                        proposed_exam         = @exam,
                        proposed_total        = @total,
                        status                = @ns,
                        updated_at            = NOW()
                    WHERE id = @id AND lecturer_id = @sid", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@lresp", lecturerResponse);
                    cmd.Parameters.AddWithValue("@supid", supervisorId > 0 ? (object)supervisorId : DBNull.Value);
                    cmd.Parameters.AddWithValue("@cw",    proposedCw.HasValue   ? (object)proposedCw.Value   : DBNull.Value);
                    cmd.Parameters.AddWithValue("@exam",  proposedExam.HasValue ? (object)proposedExam.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@total", proposedTotal.HasValue ? (object)proposedTotal.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@ns",    nextStatus);
                    cmd.Parameters.AddWithValue("@id",    requestId);
                    cmd.Parameters.AddWithValue("@sid",   staffId);
                    int n = cmd.ExecuteNonQuery();
                    if (n == 0)
                    {
                        tx.Rollback();
                        ApiHelper.Error(Response,
                            "Update failed. The request may have already been processed.", "CONFLICT");
                        return;
                    }
                }

                // Publish marks directly when MISSING_MARK goes straight to APPROVED
                if (nextStatus == "APPROVED"
                    && !string.IsNullOrEmpty(reqRegno)
                    && !string.IsNullOrEmpty(reqCourseId)
                    && proposedCw.HasValue)
                {
                    try
                    {
                        publishedTotal = proposedCw.Value + (proposedExam.HasValue ? proposedExam.Value : 0);
                        string grade = CalculateGrade(publishedTotal);
                        double gp    = CalculateGP(grade);

                        int studyYear = 1;
                        using (var cmd = new MySqlCommand(
                            "SELECT IFNULL(MAX(studyyear),1) FROM acad_registration WHERE regno=@r AND acad_year=@ay",
                            conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@r",  reqRegno);
                            cmd.Parameters.AddWithValue("@ay", reqAcadYear);
                            object sy = cmd.ExecuteScalar();
                            int tmp;
                            if (sy != null && sy != DBNull.Value && int.TryParse(sy.ToString(), out tmp))
                                studyYear = tmp;
                        }

                        int cu = 3;
                        using (var cmd = new MySqlCommand(
                            "SELECT COALESCE(NULLIF(CreditUnit,0),3) FROM acad_course WHERE courseID=@cid LIMIT 1",
                            conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@cid", reqCourseId);
                            object cuObj = cmd.ExecuteScalar();
                            int tmp;
                            if (cuObj != null && cuObj != DBNull.Value && int.TryParse(cuObj.ToString(), out tmp) && tmp > 0)
                                cu = tmp;
                        }

                        string comment = "Published via marks request #" + requestId + " by lecturer; type=" + reqType;

                        int upd;
                        using (var cmd = new MySqlCommand(@"
                            UPDATE acad_results
                            SET course_work = @cw, exam_total = @ex, score = @tot,
                                grade = @grd, gradept = @gp, CreditUnits = @cu,
                                studyyear = @sy, result_comment = @rem
                            WHERE regno = @r AND courseid = @cid AND acad = @ay AND semester = @sem",
                            conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@cw",  proposedCw.HasValue   ? (object)proposedCw.Value   : DBNull.Value);
                            cmd.Parameters.AddWithValue("@ex",  proposedExam.HasValue ? (object)proposedExam.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@tot", publishedTotal);
                            cmd.Parameters.AddWithValue("@grd", grade);
                            cmd.Parameters.AddWithValue("@gp",  gp);
                            cmd.Parameters.AddWithValue("@cu",  cu);
                            cmd.Parameters.AddWithValue("@sy",  studyYear);
                            cmd.Parameters.AddWithValue("@rem", comment);
                            cmd.Parameters.AddWithValue("@r",   reqRegno);
                            cmd.Parameters.AddWithValue("@cid", reqCourseId);
                            cmd.Parameters.AddWithValue("@ay",  reqAcadYear);
                            cmd.Parameters.AddWithValue("@sem", reqSemester);
                            upd = cmd.ExecuteNonQuery();
                        }

                        if (upd == 0)
                        {
                            string progId = "";
                            using (var cmd = new MySqlCommand(
                                "SELECT IFNULL(progid,'') FROM acad_student WHERE regno=@r LIMIT 1",
                                conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@r", reqRegno);
                                object po = cmd.ExecuteScalar();
                                if (po != null && po != DBNull.Value) progId = po.ToString();
                            }

                            using (var cmd = new MySqlCommand(@"
                                INSERT INTO acad_results
                                    (regno, courseid, acad, semester, progid, studyyear,
                                     course_work, exam_total, score, grade, gradept, CreditUnits, result_comment)
                                VALUES (@r, @cid, @ay, @sem, @prog, @sy, @cw, @ex, @tot, @grd, @gp, @cu, @rem)",
                                conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@r",    reqRegno);
                                cmd.Parameters.AddWithValue("@cid",  reqCourseId);
                                cmd.Parameters.AddWithValue("@ay",   reqAcadYear);
                                cmd.Parameters.AddWithValue("@sem",  reqSemester);
                                cmd.Parameters.AddWithValue("@prog", progId);
                                cmd.Parameters.AddWithValue("@sy",   studyYear);
                                cmd.Parameters.AddWithValue("@cw",   proposedCw.HasValue   ? (object)proposedCw.Value   : DBNull.Value);
                                cmd.Parameters.AddWithValue("@ex",   proposedExam.HasValue ? (object)proposedExam.Value : DBNull.Value);
                                cmd.Parameters.AddWithValue("@tot",  publishedTotal);
                                cmd.Parameters.AddWithValue("@grd",  grade);
                                cmd.Parameters.AddWithValue("@gp",   gp);
                                cmd.Parameters.AddWithValue("@cu",   cu);
                                cmd.Parameters.AddWithValue("@rem",  comment);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        marksPublished = true;
                    }
                    catch { /* non-fatal; request status already updated */ }
                }

                // For MISSING_MARK: mirror marks into provisional registration record
                if (reqType == "MISSING_MARK" && proposedCw.HasValue && courseRegId > 0)
                {
                    try
                    {
                        int regTotal = proposedCw.Value + (proposedExam.HasValue ? proposedExam.Value : 0);
                        // The one mark write in this file that does not go through ApiHelper,
                        // so it names the actor for itself.
                        MarkAuditContext.Set(conn, tx, "API/v2:missing-mark-mirror",
                            "Missing-mark request mirrored onto the course registration");
                        using (var cmd = new MySqlCommand(@"
                            UPDATE campus_dynamics_portal.acad_course_registration
                            SET provisional_course_work_marks = @cw,
                                provisional_exam_marks        = @exam,
                                provisional_total_marks       = @total,
                                provisional_marks_status      = @ps
                            WHERE id = @crid", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@cw",    proposedCw.Value);
                            cmd.Parameters.AddWithValue("@exam",  proposedExam.HasValue ? (object)proposedExam.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@total", regTotal);
                            cmd.Parameters.AddWithValue("@ps",    proposedExam.HasValue ? "pending" : "not_entered");
                            cmd.Parameters.AddWithValue("@crid",  courseRegId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    catch { /* non-fatal */ }
                }

                tx.Commit();
            }
        }

        var result = new Dictionary<string, object>
        {
            { "request_id", requestId },
            { "next_status", nextStatus }
        };
        if (marksPublished)
        {
            result["marks_published"]  = true;
            result["published_total"]  = publishedTotal;
        }

        string msg = nextStatus == "APPROVED"
            ? "Response submitted. Marks have been published and the request is now approved."
            : "Response submitted. The request has been forwarded to your supervisor for review.";

        ApiHelper.Success(Response, result, msg);
    }

    private void HandleLmrReject()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff")
        {
            ApiHelper.Error(Response, "This endpoint is for staff members only.", "ACCESS_DENIED");
            return;
        }

        string empIdStr = GetLecturerEmpId(auth.UserId);
        if (string.IsNullOrEmpty(empIdStr))
        {
            ApiHelper.Error(Response, "Staff profile not found.", "NOT_FOUND");
            return;
        }
        int staffId = int.Parse(empIdStr);

        // Matching the portal: reject requires a supervisor to be assigned
        int supervisorId = GetSupervisorIdForLecturer(staffId);
        if (supervisorId <= 0)
        {
            ApiHelper.Error(Response,
                "You do not have a supervisor assigned. You cannot reject requests until a supervisor is assigned to your staff profile.",
                "ACCESS_DENIED");
            return;
        }

        int requestId = ApiHelper.ParamInt(Request, "request_id", 0);
        if (requestId <= 0)
        {
            ApiHelper.Error(Response, "Missing required parameter: request_id.", "MISSING_PARAM");
            return;
        }

        string rejectReason = ApiHelper.Param(Request, "reason", "").Trim();
        if (rejectReason.Length < 10)
        {
            ApiHelper.Error(Response,
                "Please provide a rejection reason of at least 10 characters.",
                "VALIDATION_ERROR");
            return;
        }

        int n = ApiHelper.Execute(@"
            UPDATE campus_dynamics_portal.acad_marks_requests
            SET lecturer_response     = @reason,
                lecturer_responded_at = NOW(),
                status                = 'REJECTED',
                updated_at            = NOW()
            WHERE id = @id AND lecturer_id = @sid",
            new MySqlParameter("@reason", rejectReason),
            new MySqlParameter("@id",     requestId),
            new MySqlParameter("@sid",    staffId));

        if (n == 0)
        {
            ApiHelper.Error(Response,
                "Request not found or not assigned to you.", "NOT_FOUND");
            return;
        }

        ApiHelper.Success(Response,
            new Dictionary<string, object> { { "request_id", requestId } },
            "Request rejected successfully.");
    }
}
