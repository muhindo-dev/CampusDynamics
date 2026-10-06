using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;

// =====================================================================
//  Student Disciplinary module: case types, sanctions, letter templates,
//  committee and settings (plan 5.5). Nothing is deleted: rows are made
//  inactive. Every change is audited with its before and after.
// =====================================================================
public static class DcAdmin
{
    private static readonly string[] Effects = { "PORTAL_BLOCK", "RESULTS_WITHHELD", "SUSPENSION", "EXPULSION", "GRADUATION_BAR", "CANCEL_PAPER", "CANCEL_SEMESTER", "FINE", "RESTITUTION" };

    public static object Load()
    {
        using (var c = FaDb.Open())
        {
            var settings = new List<object>();
            foreach (DataRow r in FaDb.Table(c, null, "SELECT * FROM dc_settings ORDER BY setting_key").Rows)
                settings.Add(new { key = FaDb.S(r["setting_key"]), value = FaDb.S(r["setting_value"]), description = FaDb.S(r["description"]),
                                   by = FaDb.S(r["updated_by"]), at = DcFmt.When(r["updated_at"]) });
            var members = new List<object>();
            foreach (DataRow r in FaDb.Table(c, null, "SELECT * FROM dc_committee_member ORDER BY acad_year DESC, is_active DESC, FIELD(panel_role,'CHAIR','SECRETARY','MEMBER','STUDENT_REP','APPELLATE'), sort_order, member_name").Rows)
                members.Add(new { id = FaDb.I(r["id"]), year = FaDb.S(r["acad_year"]), empId = FaDb.I(r["emp_id"]), username = FaDb.S(r["username"]),
                                  name = FaDb.S(r["member_name"]), role = FaDb.S(r["panel_role"]), roleText = DcFmt.PanelRole(FaDb.S(r["panel_role"])),
                                  active = FaDb.I(r["is_active"]) == 1 });
            return new
            {
                caseTypes = DcLookups.CaseTypes(c, false), sanctionTypes = DcLookups.SanctionTypes(c, false), templates = DcLetters.Templates(c),
                members = members, settings = settings, effects = Effects, mergeFields = DcLetters.MergeFields, year = DcSeq.AcadYear(),
                canEdit = DcAccess.Can(DcAccess.SettingsManage) || DcAccess.IsAdmin()
            };
        }
    }

    private static void Need(bool ok, string msg) { if (!ok) throw new DcRefusal(msg); }

    private static string Code(Dictionary<string, object> d)
    {
        string code = FaJson.Str(d, "code").ToUpperInvariant().Replace(' ', '_');
        Need(Regex.IsMatch(code, "^[A-Z][A-Z0-9_]{1,19}$"), "The code must be 2 to 20 capital letters, digits or underscores, starting with a letter.");
        return code;
    }

    private static Dictionary<string, object> RowDict(DataRow r, params string[] cols)
    {
        var d = new Dictionary<string, object>();
        foreach (string k in cols) d[k] = r[k] == DBNull.Value ? null : r[k];
        return d;
    }

    public static object SaveCaseType(Dictionary<string, object> d)
    {
        int id = FaJson.Int(d, "id");
        string code = Code(d);
        string name = FaJson.Str(d, "name");
        Need(name.Length >= 3 && name.Length <= 150, "Give the case type a name of 3 to 150 characters.");
        string sev = FaJson.Str(d, "severity").ToUpperInvariant();
        Need(sev == "MINOR" || sev == "SERIOUS" || sev == "GROSS", "Choose the severity.");
        var sanctions = FaJson.IntList(d, "sanctions");
        using (var c = FaDb.Open())
        using (var tx = c.BeginTransaction())
        {
            Need(FaDb.Scalar(c, tx, "SELECT 1 FROM dc_case_type WHERE code=@c AND id<>@id", "@c", code, "@id", id) == null, "Another case type already uses that code.");
            var after = new Dictionary<string, object> {
                { "code", code }, { "name", name }, { "description", FaJson.Str(d, "description") }, { "severity", sev },
                { "is_exam_related", FaJson.Bool(d, "exam") ? 1 : 0 }, { "default_restricted", FaJson.Bool(d, "restricted") ? 1 : 0 },
                { "sort_order", FaJson.Int(d, "sort") }, { "is_active", FaJson.Bool(d, "active") ? 1 : 0 } };
            if (id == 0)
            {
                id = (int)FaDb.Insert(c, tx,
                    "INSERT INTO dc_case_type (code, name, description, severity, is_exam_related, default_restricted, sort_order, is_active, created_by, created_at) " +
                    "VALUES (@code,@name,@desc,@sev,@ex,@rs,@so,@ac,@u,NOW())",
                    "@code", code, "@name", name, "@desc", FaDb.NullIfEmpty(FaJson.Str(d, "description")), "@sev", sev, "@ex", after["is_exam_related"],
                    "@rs", after["default_restricted"], "@so", after["sort_order"], "@ac", 1, "@u", DcAccess.Username());
                DcAudit.Write(c, tx, "CASE_TYPE", id, null, "CREATE", null, after, null, "Disciplinary case type added: " + name);
            }
            else
            {
                DataTable t = FaDb.Table(c, tx, "SELECT * FROM dc_case_type WHERE id=@id FOR UPDATE", "@id", id);
                Need(t.Rows.Count == 1, "That case type no longer exists.");
                Need(FaJson.Int(d, "version") == FaDb.I(t.Rows[0]["row_version"]), "Someone else changed this case type. Reload and try again.");
                var before = RowDict(t.Rows[0], "code", "name", "description", "severity", "is_exam_related", "default_restricted", "sort_order", "is_active");
                Dictionary<string, object> b, a; FaAudit.Diff(before, after, out b, out a);
                FaDb.Exec(c, tx,
                    "UPDATE dc_case_type SET code=@code, name=@name, description=@desc, severity=@sev, is_exam_related=@ex, default_restricted=@rs, sort_order=@so, is_active=@ac, " +
                    "updated_by=@u, updated_at=NOW(), row_version=row_version+1 WHERE id=@id",
                    "@code", code, "@name", name, "@desc", FaDb.NullIfEmpty(FaJson.Str(d, "description")), "@sev", sev, "@ex", after["is_exam_related"],
                    "@rs", after["default_restricted"], "@so", after["sort_order"], "@ac", after["is_active"], "@u", DcAccess.Username(), "@id", id);
                if (a.Count > 0) DcAudit.Write(c, tx, "CASE_TYPE", id, null, "UPDATE", b, a, null, "Disciplinary case type changed: " + name);
            }
            var old = new List<int>();
            foreach (DataRow r in FaDb.Table(c, tx, "SELECT sanction_type_id FROM dc_case_type_sanction WHERE case_type_id=@t AND is_active=1", "@t", id).Rows) old.Add(FaDb.I(r[0]));
            old.Sort(); var nw = new List<int>(sanctions); nw.Sort();
            if (string.Join(",", old) != string.Join(",", nw))
            {
                // Pairings are switched off, never deleted; the audit row keeps the before and after lists.
                FaDb.Exec(c, tx, "UPDATE dc_case_type_sanction SET is_active=0 WHERE case_type_id=@t", "@t", id);
                int n = 0;
                foreach (int s in sanctions)
                    FaDb.Exec(c, tx, "INSERT INTO dc_case_type_sanction (case_type_id, sanction_type_id, sort_order, is_active) VALUES (@t,@s,@o,1) " +
                                     "ON DUPLICATE KEY UPDATE sort_order=VALUES(sort_order), is_active=1", "@t", id, "@s", s, "@o", ++n);
                DcAudit.Write(c, tx, "CASE_TYPE", id, null, "DEFAULT_SANCTIONS", new { sanctions = old }, new { sanctions = nw }, null, null);
            }
            tx.Commit();
        }
        return new { id = id };
    }

    public static object SaveSanctionType(Dictionary<string, object> d)
    {
        int id = FaJson.Int(d, "id");
        string code = Code(d);
        string name = FaJson.Str(d, "name");
        Need(name.Length >= 3 && name.Length <= 150, "Give the sanction a name of 3 to 150 characters.");
        string outcome = FaJson.Str(d, "outcome").ToUpperInvariant();
        Need(outcome == "SANCTION" || outcome == "DISMISSAL" || outcome == "ACQUITTAL", "Choose the outcome.");
        var effects = new List<string>();
        foreach (string e in FaJson.Str(d, "effects").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        { string x = e.Trim().ToUpperInvariant(); Need(Array.IndexOf(Effects, x) >= 0, "Unknown effect " + x + "."); if (!effects.Contains(x)) effects.Add(x); }
        Need(outcome == "SANCTION" || effects.Count == 0, "A dismissal or acquittal has no effects.");
        string dates = FaJson.Str(d, "needsDates").ToUpperInvariant();
        if (dates == "") dates = "NONE";
        Need(dates == "NONE" || dates == "FROM" || dates == "FROM_TO", "Choose which dates the sanction needs.");
        bool restrictive = effects.Contains("PORTAL_BLOCK") || effects.Contains("RESULTS_WITHHELD") || effects.Contains("SUSPENSION") || effects.Contains("EXPULSION") || effects.Contains("GRADUATION_BAR");
        Need(!restrictive || dates != "NONE", "A sanction that restricts the student needs at least a start date.");
        string effSet = string.Join(",", effects.ToArray());
        using (var c = FaDb.Open())
        using (var tx = c.BeginTransaction())
        {
            Need(FaDb.Scalar(c, tx, "SELECT 1 FROM dc_sanction_type WHERE code=@c AND id<>@id", "@c", code, "@id", id) == null, "Another sanction already uses that code.");
            var after = new Dictionary<string, object> {
                { "code", code }, { "name", name }, { "description", FaJson.Str(d, "description") }, { "outcome", outcome }, { "effects", effSet },
                { "needs_amount", FaJson.Bool(d, "needsAmount") ? 1 : 0 }, { "needs_dates", dates }, { "needs_course", FaJson.Bool(d, "needsCourse") ? 1 : 0 },
                { "needs_semester", FaJson.Bool(d, "needsSemester") ? 1 : 0 }, { "allowed_interim", FaJson.Bool(d, "interim") ? 1 : 0 },
                { "sort_order", FaJson.Int(d, "sort") }, { "is_active", id == 0 ? 1 : (FaJson.Bool(d, "active") ? 1 : 0) } };
            if (id == 0)
            {
                id = (int)FaDb.Insert(c, tx,
                    "INSERT INTO dc_sanction_type (code, name, description, outcome, effects, needs_amount, needs_dates, needs_course, needs_semester, allowed_interim, sort_order, is_active, created_by, created_at) " +
                    "VALUES (@code,@name,@desc,@oc,@ef,@na,@nd,@nc,@ns,@ai,@so,1,@u,NOW())",
                    "@code", code, "@name", name, "@desc", FaDb.NullIfEmpty(FaJson.Str(d, "description")), "@oc", outcome, "@ef", effSet, "@na", after["needs_amount"],
                    "@nd", dates, "@nc", after["needs_course"], "@ns", after["needs_semester"], "@ai", after["allowed_interim"], "@so", after["sort_order"], "@u", DcAccess.Username());
                DcAudit.Write(c, tx, "SANCTION_TYPE", id, null, "CREATE", null, after, null, "Disciplinary sanction added: " + name);
            }
            else
            {
                DataTable t = FaDb.Table(c, tx, "SELECT * FROM dc_sanction_type WHERE id=@id FOR UPDATE", "@id", id);
                Need(t.Rows.Count == 1, "That sanction no longer exists.");
                Need(FaJson.Int(d, "version") == FaDb.I(t.Rows[0]["row_version"]), "Someone else changed this sanction. Reload and try again.");
                var before = RowDict(t.Rows[0], "code", "name", "description", "outcome", "effects", "needs_amount", "needs_dates", "needs_course", "needs_semester", "allowed_interim", "sort_order", "is_active");
                Dictionary<string, object> b, a; FaAudit.Diff(before, after, out b, out a);
                FaDb.Exec(c, tx,
                    "UPDATE dc_sanction_type SET code=@code, name=@name, description=@desc, outcome=@oc, effects=@ef, needs_amount=@na, needs_dates=@nd, needs_course=@nc, needs_semester=@ns, " +
                    "allowed_interim=@ai, sort_order=@so, is_active=@ac, updated_by=@u, updated_at=NOW(), row_version=row_version+1 WHERE id=@id",
                    "@code", code, "@name", name, "@desc", FaDb.NullIfEmpty(FaJson.Str(d, "description")), "@oc", outcome, "@ef", effSet, "@na", after["needs_amount"],
                    "@nd", dates, "@nc", after["needs_course"], "@ns", after["needs_semester"], "@ai", after["allowed_interim"], "@so", after["sort_order"],
                    "@ac", after["is_active"], "@u", DcAccess.Username(), "@id", id);
                if (a.Count > 0) DcAudit.Write(c, tx, "SANCTION_TYPE", id, null, "UPDATE", b, a, null, "Disciplinary sanction changed: " + name);
            }
            tx.Commit();
        }
        return new { id = id };
    }

    public static object SaveTemplate(Dictionary<string, object> d)
    {
        int id = FaJson.Int(d, "id");
        string code = Code(d);
        string name = FaJson.Str(d, "name"), subject = FaJson.Str(d, "subject"), body = FaJson.Str(d, "body");
        Need(name.Length >= 3, "Give the template a name.");
        Need(subject.Length >= 3 && subject.Length <= 250, "Give a subject of 3 to 250 characters.");
        Need(body.Length >= 20 && body.Length <= 20000, "Write the letter text (20 to 20,000 characters).");
        foreach (Match m in Regex.Matches(subject + body, @"\{\{\s*([a-zA-Z_]+)\s*\}\}"))
            Need(Array.IndexOf(DcLetters.MergeFields, m.Groups[1].Value) >= 0, "{{" + m.Groups[1].Value + "}} is not a merge field. Use one from the list.");
        using (var c = FaDb.Open())
        using (var tx = c.BeginTransaction())
        {
            Need(FaDb.Scalar(c, tx, "SELECT 1 FROM dc_letter_template WHERE code=@c AND id<>@id", "@c", code, "@id", id) == null, "Another template already uses that code.");
            var after = new Dictionary<string, object> { { "code", code }, { "name", name }, { "subject", subject }, { "body", body },
                { "student_copy", FaJson.Bool(d, "studentCopy") ? 1 : 0 }, { "sort_order", FaJson.Int(d, "sort") }, { "is_active", id == 0 ? 1 : (FaJson.Bool(d, "active") ? 1 : 0) } };
            if (id == 0)
            {
                id = (int)FaDb.Insert(c, tx,
                    "INSERT INTO dc_letter_template (code, name, subject, body, student_copy, sort_order, is_active, created_by, created_at) VALUES (@code,@n,@s,@b,@sc,@so,1,@u,NOW())",
                    "@code", code, "@n", name, "@s", subject, "@b", body, "@sc", after["student_copy"], "@so", after["sort_order"], "@u", DcAccess.Username());
                DcAudit.Write(c, tx, "TEMPLATE", id, null, "CREATE", null, after, null, "Disciplinary letter template added: " + name);
            }
            else
            {
                DataTable t = FaDb.Table(c, tx, "SELECT * FROM dc_letter_template WHERE id=@id FOR UPDATE", "@id", id);
                Need(t.Rows.Count == 1, "That template no longer exists.");
                Need(FaJson.Int(d, "version") == FaDb.I(t.Rows[0]["row_version"]), "Someone else changed this template. Reload and try again.");
                var before = RowDict(t.Rows[0], "code", "name", "subject", "body", "student_copy", "sort_order", "is_active");
                Dictionary<string, object> b, a; FaAudit.Diff(before, after, out b, out a);
                FaDb.Exec(c, tx,
                    "UPDATE dc_letter_template SET code=@code, name=@n, subject=@s, body=@b, student_copy=@sc, sort_order=@so, is_active=@ac, updated_by=@u, updated_at=NOW(), row_version=row_version+1 WHERE id=@id",
                    "@code", code, "@n", name, "@s", subject, "@b", body, "@sc", after["student_copy"], "@so", after["sort_order"], "@ac", after["is_active"], "@u", DcAccess.Username(), "@id", id);
                if (a.Count > 0) DcAudit.Write(c, tx, "TEMPLATE", id, null, "UPDATE", b, a, null, "Disciplinary letter template changed: " + name);
            }
            tx.Commit();
        }
        return new { id = id };
    }

    public static object SaveMember(Dictionary<string, object> d)
    {
        int id = FaJson.Int(d, "id");
        string year = FaJson.Str(d, "year");
        Need(Regex.IsMatch(year, @"^\d{4}/\d{4}$"), "Give the academic year as 2026/2027.");
        string name = FaJson.Str(d, "name");
        Need(name.Length >= 3, "Choose the member.");
        string role = FaJson.Str(d, "role").ToUpperInvariant();
        Need(Array.IndexOf(new[] { "CHAIR", "MEMBER", "SECRETARY", "STUDENT_REP", "APPELLATE" }, role) >= 0, "Choose the panel role.");
        int? emp = FaJson.IntN(d, "empId");
        string user = FaJson.Str(d, "username");
        using (var c = FaDb.Open())
        using (var tx = c.BeginTransaction())
        {
            bool active = id == 0 || FaJson.Bool(d, "active");
            if (role == "CHAIR" && active)
                Need(FaDb.Scalar(c, tx, "SELECT 1 FROM dc_committee_member WHERE acad_year=@y AND panel_role='CHAIR' AND is_active=1 AND id<>@id", "@y", year, "@id", id) == null,
                     "This year already has a chair. Make the current chair inactive first.");
            var after = new Dictionary<string, object> { { "acad_year", year }, { "emp_id", emp }, { "username", user }, { "member_name", name }, { "panel_role", role }, { "is_active", active ? 1 : 0 } };
            if (id == 0)
            {
                id = (int)FaDb.Insert(c, tx,
                    "INSERT INTO dc_committee_member (acad_year, emp_id, username, member_name, panel_role, sort_order, is_active, created_by, created_at) VALUES (@y,@e,@u,@n,@r,0,1,@by,NOW())",
                    "@y", year, "@e", emp.HasValue && emp.Value > 0 ? (object)emp.Value : DBNull.Value, "@u", FaDb.NullIfEmpty(user), "@n", DcAudit.Cut(name, 150), "@r", role, "@by", DcAccess.Username());
                DcAudit.Write(c, tx, "MEMBER", id, null, "CREATE", null, after, null, "Disciplinary committee member added: " + name + " (" + year + ")");
            }
            else
            {
                DataTable t = FaDb.Table(c, tx, "SELECT * FROM dc_committee_member WHERE id=@id FOR UPDATE", "@id", id);
                Need(t.Rows.Count == 1, "That member no longer exists.");
                var before = RowDict(t.Rows[0], "acad_year", "emp_id", "username", "member_name", "panel_role", "is_active");
                Dictionary<string, object> b, a; FaAudit.Diff(before, after, out b, out a);
                FaDb.Exec(c, tx, "UPDATE dc_committee_member SET acad_year=@y, emp_id=@e, username=@u, member_name=@n, panel_role=@r, is_active=@a, updated_by=@by, updated_at=NOW() WHERE id=@id",
                    "@y", year, "@e", emp.HasValue && emp.Value > 0 ? (object)emp.Value : DBNull.Value, "@u", FaDb.NullIfEmpty(user), "@n", DcAudit.Cut(name, 150), "@r", role,
                    "@a", active ? 1 : 0, "@by", DcAccess.Username(), "@id", id);
                if (a.Count > 0) DcAudit.Write(c, tx, "MEMBER", id, null, "UPDATE", b, a, null, "Disciplinary committee member changed: " + name);
            }
            tx.Commit();
        }
        return new { id = id };
    }

    public static object SaveSetting(Dictionary<string, object> d)
    {
        string key = FaJson.Str(d, "key");
        string value = FaJson.Str(d, "value");
        Need(value.Length > 0 && value.Length <= 1000, "Give a value (up to 1,000 characters).");
        if (key.EndsWith("_days")) { int n; Need(int.TryParse(value, out n) && n >= 1 && n <= 365, "Give a number of days from 1 to 365."); }
        using (var c = FaDb.Open())
        using (var tx = c.BeginTransaction())
        {
            DataTable t = FaDb.Table(c, tx, "SELECT * FROM dc_settings WHERE setting_key=@k FOR UPDATE", "@k", key);
            Need(t.Rows.Count == 1, "That setting does not exist.");
            string old = FaDb.S(t.Rows[0]["setting_value"]);
            if (old != value)
            {
                FaDb.Exec(c, tx, "UPDATE dc_settings SET setting_value=@v, updated_by=@u, updated_at=NOW() WHERE setting_key=@k", "@v", value, "@u", DcAccess.Username(), "@k", key);
                DcAudit.Write(c, tx, "SETTING", 0, null, "UPDATE:" + DcAudit.Cut(key, 30), new { value = old }, new { value = value }, null, "Disciplinary setting changed: " + key);
            }
            tx.Commit();
        }
        DcSettings.Forget();
        return new { };
    }
}
