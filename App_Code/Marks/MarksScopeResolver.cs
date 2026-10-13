using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

/// <summary>
/// The marks/results data scope for the current signed-in user.
///   • Super admin  → everything (no restriction).
///   • Dean         → their faculty(ies)  (faculty-wise).
///   • HOD / staff  → their department(s) (department-wise).
///   • Otherwise    → nothing.
/// Enforced server-side so the UI cannot widen a user's access.
/// </summary>
public class MarksScope
{
    public bool IsAdmin = false;
    public string Mode = "none";           // all | faculty | department | none
    public string Label = "No access";     // human readable scope description
    public string RoleNote = "";           // Administrator | Dean | Head of Department | …
    public List<string> FacultyCodes = new List<string>();
    public List<int> DepartmentIds = new List<int>();
    public List<string> AllowedProgCodes = new List<string>(); // null => unrestricted (admin)

    public bool HasAccess { get { return IsAdmin || AllowedProgCodes == null || AllowedProgCodes.Count > 0; } }   // null => unrestricted (admin, Graduation Officer)

    /// <summary>SQL fragment restricting &lt;alias&gt;.prog_id to the user's scope.</summary>
    public string ProgFilter(string alias) { return ProgFilter(alias, "prog_id"); }

    /// <summary>SQL fragment restricting &lt;alias&gt;.&lt;column&gt; (the programme code column)
    /// to the user's scope. Use "progid" for acad_results/acad_examresults_faculty,
    /// "prog_id" for acad_course_registration, "progcode" for acad_programme.</summary>
    public string ProgFilter(string alias, string column)
    {
        if (IsAdmin || AllowedProgCodes == null) return "";       // unrestricted
        if (AllowedProgCodes.Count == 0) return " AND 1=0 ";       // no access → no rows
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < AllowedProgCodes.Count; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append("'").Append(AllowedProgCodes[i].Replace("'", "''")).Append("'");
        }
        return " AND " + alias + "." + column + " IN (" + sb.ToString() + ") ";
    }

    /// <summary>SQL fragment restricting an arbitrary programme-code SQL expression
    /// (e.g. a correlated subquery) to the user's scope. "" for admin, " AND 1=0 " for none.</summary>
    public string ProgFilterExpr(string progExpr)
    {
        if (IsAdmin || AllowedProgCodes == null) return "";
        if (AllowedProgCodes.Count == 0) return " AND 1=0 ";
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < AllowedProgCodes.Count; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append("'").Append(AllowedProgCodes[i].Replace("'", "''")).Append("'");
        }
        return " AND (" + progExpr + ") IN (" + sb.ToString() + ") ";
    }

    /// <summary>True if a programme code is within scope (admin → always true).</summary>
    public bool AllowsProg(string progcode)
    {
        if (IsAdmin || AllowedProgCodes == null) return true;
        return AllowedProgCodes.Contains((progcode ?? "").Trim());
    }
}

public static class MarksScopeResolver
{
    private static readonly HashSet<string> TITLES = new HashSet<string>
        { "DR","PROF","ASSOC","MR","MRS","MS","ENG","HON","REV","SIR","MADAM","PROFESSOR","DOCTOR","AG","DEAN" };

    private static HashSet<string> NameTokens(string s)
    {
        var set = new HashSet<string>();
        if (string.IsNullOrEmpty(s)) return set;
        foreach (var t in s.ToUpperInvariant().Split(new[] { ' ', '.', ',', '-', '/', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string tok = t.Trim();
            if (tok.Length < 2 || TITLES.Contains(tok)) continue;
            set.Add(tok);
        }
        return set;
    }

    private static string ConnStr()
    {
        var cs = ConfigurationManager.ConnectionStrings["vacConnectionString"];
        return cs != null ? cs.ConnectionString
                          : "Server=localhost;Database=campus_dynamics;Uid=root;Pwd=24thdecember1977;";
    }

    public static MarksScope Resolve()
    {
        var scope = new MarksScope();
        HttpContext ctx = HttpContext.Current;
        var session = ctx != null ? ctx.Session : null;
        string username = (session != null ? session["username"] as string : null);
        if (string.IsNullOrEmpty(username)) { scope.Label = "Not signed in"; return scope; }

        // 1) Super admin → unrestricted everything.
        try
        {
            object slugObj = session[RoleAccessService.SESSION_SLUGS];
            if (slugObj == null || string.IsNullOrEmpty(slugObj as string))
                RoleAccessService.LoadUserAccess(username);
            else
                RoleAccessService.MaybeRefresh(username);

            string slugs = session[RoleAccessService.SESSION_SLUGS] as string ?? "";
            if (slugs.IndexOf(RoleAccessService.ADMIN_WILDCARD, StringComparison.Ordinal) >= 0)
            {
                scope.IsAdmin = true;
                scope.Mode = "all";
                scope.AllowedProgCodes = null;
                scope.Label = "All faculties & departments";
                scope.RoleNote = "Administrator";
                return scope;
            }
        }
        catch { }

        // 1b) Institution-wide offices. The Graduation Officer runs graduation for every
        //     faculty, so a scope derived from a home department (or none, when no department
        //     is linked) would show them nobody to act on. Role-based, like the Registrar's
        //     right in GraduationRights.
        try
        {
            using (var conn = new MySqlConnection(ConnStr()))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM sys_user_roles ur JOIN sys_roles r ON r.id=ur.role_id " +
                    "WHERE ur.username=@u AND ur.is_active=1 AND r.is_active=1 " +
                    "  AND (ur.expires_at IS NULL OR ur.expires_at>NOW()) AND r.role_code='graduation_officer'", conn))
                {
                    cmd.Parameters.AddWithValue("@u", username);
                    if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                    {
                        scope.Mode = "all";
                        scope.AllowedProgCodes = null;
                        scope.Label = "All faculties & departments";
                        scope.RoleNote = "Graduation Officer";
                        return scope;
                    }
                }
            }
        }
        catch { }

        // 2) Resolve identity → dean faculties / departments.
        try
        {
            using (var conn = new MySqlConnection(ConnStr()))
            {
                conn.Open();

                string empName = null; int empId = 0; int homeDeptId = 0;
                using (var cmd = new MySqlCommand(
                    "SELECT empID, IFNULL(emp_name,''), IFNULL(dept_id,0) FROM hrm_employee " +
                    "WHERE UPPER(TRIM(usernames))=UPPER(TRIM(@u)) LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@u", username);
                    using (var dr = cmd.ExecuteReader())
                        if (dr.Read()) { empId = Convert.ToInt32(dr[0]); empName = dr[1].ToString(); homeDeptId = Convert.ToInt32(dr[2]); }
                }

                string nameForMatch = empName;
                if (string.IsNullOrEmpty(nameForMatch)) nameForMatch = session["ScreenName"] as string;
                var myTokens = NameTokens(nameForMatch);

                // Dean faculties — ≥2 shared name tokens with acad_faculty.faculty_dean.
                var facultyCodes = new List<string>();
                var facultyNames = new List<string>();
                if (myTokens.Count >= 2)
                {
                    var rows = new List<string[]>();
                    using (var cmd = new MySqlCommand(
                        "SELECT faculty_code, IFNULL(faculty_name,''), IFNULL(faculty_dean,'') FROM acad_faculty WHERE faculty_code<>'00'", conn))
                    using (var dr = cmd.ExecuteReader())
                        while (dr.Read()) rows.Add(new[] { dr[0].ToString(), dr[1].ToString(), dr[2].ToString() });

                    foreach (var row in rows)
                    {
                        var deanTokens = NameTokens(row[2]);
                        int common = 0;
                        foreach (var tk in myTokens) if (deanTokens.Contains(tk)) common++;
                        if (common >= 2) { facultyCodes.Add(row[0].Trim()); facultyNames.Add(row[1]); }
                    }
                }

                // Departments: ones the user heads + their employment department(s).
                var deptIds = new List<int>();
                bool isHead = false;
                if (empId > 0)
                {
                    using (var cmd = new MySqlCommand("SELECT ID FROM hrm_departments WHERE dept_headID=@e", conn))
                    {
                        cmd.Parameters.AddWithValue("@e", empId);
                        using (var dr = cmd.ExecuteReader())
                            while (dr.Read()) { int id = Convert.ToInt32(dr[0]); if (!deptIds.Contains(id)) { deptIds.Add(id); isHead = true; } }
                    }
                    try
                    {
                        using (var cmd = new MySqlCommand("SELECT DISTINCT departmentID FROM hrm_emp_contracts WHERE empID=@e AND departmentID>0", conn))
                        {
                            cmd.Parameters.AddWithValue("@e", empId);
                            using (var dr = cmd.ExecuteReader())
                                while (dr.Read()) { int id = Convert.ToInt32(dr[0]); if (!deptIds.Contains(id)) deptIds.Add(id); }
                        }
                    }
                    catch { }
                }
                if (homeDeptId > 0 && !deptIds.Contains(homeDeptId)) deptIds.Add(homeDeptId);

                // 3) Faculty wins (broadest); else department; else nothing.
                if (facultyCodes.Count > 0)
                {
                    scope.FacultyCodes = facultyCodes;
                    scope.Mode = "faculty";
                    scope.RoleNote = "Dean";
                    scope.Label = "Faculty — " + string.Join(", ", facultyNames.ToArray());
                    scope.AllowedProgCodes = ProgsForFaculties(conn, facultyCodes);
                }
                else if (deptIds.Count > 0)
                {
                    scope.DepartmentIds = deptIds;
                    scope.Mode = "department";
                    scope.RoleNote = isHead ? "Head of Department" : "Departmental staff";
                    scope.Label = "Department — " + DeptLabel(conn, deptIds);
                    scope.AllowedProgCodes = ProgsForDepartments(conn, deptIds);
                }
                else
                {
                    scope.Mode = "none";
                    scope.Label = "No faculty or department is linked to your account";
                    scope.AllowedProgCodes = new List<string>();
                }
            }
        }
        catch (Exception ex)
        {
            scope.Mode = "none";
            scope.Label = "Scope error: " + ex.Message;
            scope.AllowedProgCodes = new List<string>();
        }

        return scope;
    }

    private static List<string> ProgsForFaculties(MySqlConnection conn, List<string> facs)
    {
        var list = new List<string>();
        if (facs.Count == 0) return list;
        using (var cmd = new MySqlCommand("SELECT progcode FROM acad_programme WHERE faculty_code IN (" + InStr(facs) + ")", conn))
        using (var dr = cmd.ExecuteReader())
            while (dr.Read()) list.Add(dr[0].ToString().Trim());
        return list;
    }

    private static List<string> ProgsForDepartments(MySqlConnection conn, List<int> depts)
    {
        var list = new List<string>();
        if (depts.Count == 0) return list;
        using (var cmd = new MySqlCommand("SELECT progcode FROM acad_programme WHERE department_id IN (" + InInt(depts) + ")", conn))
        using (var dr = cmd.ExecuteReader())
            while (dr.Read()) list.Add(dr[0].ToString().Trim());
        return list;
    }

    private static string DeptLabel(MySqlConnection conn, List<int> depts)
    {
        var names = new List<string>();
        using (var cmd = new MySqlCommand("SELECT dept_name FROM hrm_departments WHERE ID IN (" + InInt(depts) + ")", conn))
        using (var dr = cmd.ExecuteReader())
            while (dr.Read()) names.Add(dr[0].ToString());
        return names.Count > 0 ? string.Join(", ", names.ToArray()) : "your department";
    }

    private static string InStr(List<string> vals)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < vals.Count; i++) { if (i > 0) sb.Append(","); sb.Append("'").Append(vals[i].Replace("'", "''")).Append("'"); }
        return sb.Length == 0 ? "''" : sb.ToString();
    }

    private static string InInt(List<int> vals)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < vals.Count; i++) { if (i > 0) sb.Append(","); sb.Append(vals[i]); }
        return sb.Length == 0 ? "0" : sb.ToString();
    }
}
