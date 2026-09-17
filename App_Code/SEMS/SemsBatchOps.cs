using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

// =====================================================================
//  SEMS batch operations — preview, commit, export.
//
//  The flow is deliberately two-phase:
//
//    PreviewBatch  → allocates addresses, RESERVES each one in the
//                    directory, and writes a DRAFT batch nobody has to
//                    trust: it is on the server, reviewable, editable.
//    CommitBatch   → applies exactly the rows that were approved, one
//                    transaction per row, idempotent and resumable.
//
//  Because reservation happens at preview, two admins previewing at the
//  same moment cannot be handed the same address; and because the commit
//  re-checks under a UNIQUE index, even a race ends as one clean failed
//  row rather than a duplicate account.
// =====================================================================
public static partial class SemsBatch
{
    // ── option bag ───────────────────────────────────────────────────
    public class Options
    {
        public string Scope = "filter";          // filter | regnos
        public string Stage = "PENDING_CREATION";
        public string Campus = "", Programme = "", Year = "", Q = "";
        public List<string> Regnos = new List<string>();
        public int Limit = 500;
        public int OtherLen = 3;
        public string Domain = DefaultDomain;
        public string NameOrder = "OTHER_IS_SURNAME";   // or FIRST_IS_SURNAME
        /// <summary>default = the house password every student gets; unique = one each; fixed = a shared one this batch only.</summary>
        public string PwMode = "default";               // default | unique | fixed
        public string PwFixed = "";
        /// <summary>Must already exist in Google — "/" is the root and always does.</summary>
        public string OrgUnit = "/";
        public bool ChangePwNext = true;
        public string TargetStage = "READY_FOR_COLLECTION";
        public bool Notify = true;
    }

    private static string GetS(Dictionary<string, object> d, string k, string dflt)
    {
        object v; return (d != null && d.TryGetValue(k, out v) && v != null && v.ToString().Trim() != "") ? v.ToString().Trim() : dflt;
    }
    private static int GetI(Dictionary<string, object> d, string k, int dflt)
    {
        object v; int n;
        return (d != null && d.TryGetValue(k, out v) && v != null && int.TryParse(v.ToString(), out n)) ? n : dflt;
    }
    private static bool GetB(Dictionary<string, object> d, string k, bool dflt)
    {
        object v; bool b;
        return (d != null && d.TryGetValue(k, out v) && v != null && bool.TryParse(v.ToString(), out b)) ? b : dflt;
    }

    public static Options ReadOptions(string json)
    {
        var o = new Options();
        if (string.IsNullOrWhiteSpace(json)) return o;
        var d = Js().Deserialize<Dictionary<string, object>>(json);
        o.Scope = GetS(d, "scope", o.Scope);
        o.Stage = GetS(d, "stage", o.Stage);
        o.Campus = GetS(d, "campus", "");
        o.Programme = GetS(d, "programme", "");
        o.Year = GetS(d, "year", "");
        o.Q = GetS(d, "q", "");
        o.Limit = Math.Max(1, Math.Min(HardBatchCap, GetI(d, "limit", o.Limit)));
        o.OtherLen = Math.Max(1, Math.Min(12, GetI(d, "otherLen", o.OtherLen)));
        o.Domain = GetS(d, "domain", o.Domain).ToLowerInvariant().TrimStart('@');
        o.NameOrder = GetS(d, "nameOrder", o.NameOrder).ToUpperInvariant();
        o.PwMode = GetS(d, "pwMode", o.PwMode).ToLowerInvariant();
        o.PwFixed = GetS(d, "pwFixed", "");
        o.OrgUnit = GetS(d, "orgUnit", o.OrgUnit);
        o.ChangePwNext = GetB(d, "changePwNext", o.ChangePwNext);
        o.TargetStage = GetS(d, "targetStage", o.TargetStage).ToUpperInvariant();
        o.Notify = GetB(d, "notify", o.Notify);
        object rr;
        if (d != null && d.TryGetValue("regnos", out rr) && rr is System.Collections.IEnumerable && !(rr is string))
            foreach (var x in (System.Collections.IEnumerable)rr)
                if (x != null && x.ToString().Trim() != "") o.Regnos.Add(x.ToString().Trim());
        return o;
    }

    /// <summary>
    /// The password a row in this batch is issued with. The house default is the answer
    /// unless the operator deliberately asked for something else, so an intake ends up with
    /// one password the ICT desk can say out loud rather than 500 nobody can look up.
    /// </summary>
    private static string PasswordFor(Options o)
    {
        if (o.PwMode == "fixed") return (o.PwFixed ?? "").Trim();
        if (o.PwMode == "unique") return RandomPassword();
        return DefaultPassword;
    }

    // ── one candidate student, as the wizard shows them ───────────────
    private class Cand
    {
        public string Regno = "", EntryNo = "", Name = "", First = "", Other = "", Campus = "", Prog = "", ProgName = "";
        public string Year = "", Phone = "", Personal = "", District = "", CurEmail = "", Stage = "";
        public string Surname { get { return SurnameOf(this); } }
    }
    [ThreadStatic] private static string _nameOrder;
    private static string SurnameOf(Cand c) { return _nameOrder == "FIRST_IS_SURNAME" ? c.First : c.Other; }
    private static string GivenOf(Cand c) { return _nameOrder == "FIRST_IS_SURNAME" ? c.Other : c.First; }

    /// <summary>One proposed row, exactly as it is shown, stored and later exported.</summary>
    public class PreviewRow
    {
        public string regno { get; set; }
        public string name { get; set; }
        public string surname { get; set; }
        public string given { get; set; }
        public string year { get; set; }
        public string campus { get; set; }
        public string programme { get; set; }
        public string email { get; set; }
        public string password { get; set; }
        public string orgUnit { get; set; }
        public string recovery { get; set; }
        public string phone { get; set; }
        public string severity { get; set; }
        public string message { get; set; }
        public string strategy { get; set; }
    }

    /// <summary>Org unit path with {year} / {campus} / {prog} filled in.</summary>
    private static string OrgUnitFor(string template, Cand c)
    {
        string t = string.IsNullOrWhiteSpace(template) ? "/Students" : template.Trim();
        t = t.Replace("{year}", c.Year ?? "").Replace("{campus}", CampusName(c.Campus)).Replace("{prog}", c.Prog ?? "");
        if (!t.StartsWith("/")) t = "/" + t;
        while (t.Contains("//")) t = t.Replace("//", "/");
        if (t.Length > 1 && t.EndsWith("/")) t = t.Substring(0, t.Length - 1);
        return t;
    }

    public static string CampusName(string c)
    { c = (c ?? "").Trim(); return c == "1" ? "Kakeeka" : c == "2" ? "Kirumba" : (c == "" ? "" : c); }

    // =================================================================
    //  PREVIEW — allocate + reserve + write the draft
    // =================================================================
    public static string PreviewBatch(string optionsJson)
    {
        Options o;
        try { o = ReadOptions(optionsJson); }
        catch (Exception ex) { return Fail("Could not read the wizard options: " + ex.Message); }

        if (o.PwMode == "fixed" && !IsUsablePassword(o.PwFixed, ""))
            return Fail("A shared password must be at least " + MinPasswordLength +
                        " characters — Google rejects anything shorter.");
        if (o.Domain.Length < 3 || !o.Domain.Contains("."))
            return Fail("The domain looks wrong: " + o.Domain);

        _nameOrder = o.NameOrder;

        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                ReleaseStaleReservations(c);
                SyncDirectory(c, o.Domain);
                var taken = LoadTaken(c, o.Domain);

                var cands = LoadCandidates(c, o);
                if (cands.Count == 0)
                    return Js().Serialize(new { success = false, message = "No students match this selection. Everyone in scope may already have an address." });

                // Allocate for all rows first — purely in memory, so a name clash inside the
                // batch is resolved against the batch itself, not just against the database.
                var items = new List<PreviewRow>();
                var toReserve = new List<string[]>();     // local, regno, name
                int okCount = 0, warnCount = 0, errCount = 0;

                foreach (var cd in cands)
                {
                    string surname = SurnameOf(cd), given = GivenOf(cd);
                    string severity = "OK", message = "", email = "", strategy = "";

                    if (!string.IsNullOrEmpty(cd.CurEmail))
                    {
                        severity = "SKIP"; message = "Already has " + cd.CurEmail; email = cd.CurEmail;
                    }
                    else if (Slug(surname).Length == 0 && Slug(given).Length == 0)
                    {
                        severity = "ERROR"; message = "No usable name on the student record — fix the name first.";
                    }
                    else
                    {
                        string local = Allocate(taken, surname, given, cd.Year, o.OtherLen, out strategy);
                        if (local.Length == 0)
                        {
                            severity = "ERROR";
                            message = strategy == "no-name" ? "No usable name." : "Could not find a free address for this name.";
                        }
                        else
                        {
                            taken.Add(local);                       // reserve inside this batch too
                            email = local + "@" + o.Domain;
                            if (Yy(cd.Year).Length < 2) { severity = "WARN"; message = "No entry year on the record — address has no year suffix. "; }
                            if (Slug(given).Length == 0) { severity = "WARN"; message += "Only one name available. "; }
                            if (strategy == "extended") { severity = severity == "OK" ? "WARN" : severity; message += "Name clash — used a longer first name. "; }
                            else if (strategy == "second-name") { severity = severity == "OK" ? "WARN" : severity; message += "Name clash — used a second given name. "; }
                            else if (strategy == "numbered") { severity = "WARN"; message += "Name clash — a number was appended. "; }
                            if (Slug(surname).Length < 2) { severity = "WARN"; message += "Very short surname. "; }
                            toReserve.Add(new[] { local, cd.Regno, cd.Name });
                        }
                    }

                    string pw = (severity == "ERROR" || severity == "SKIP") ? "" : PasswordFor(o);
                    string rec = RecoveryEmailFor(cd, o.Domain);
                    string phone = ToE164(cd.Phone);

                    if (severity == "OK" && rec == "" && phone == "")
                        { severity = "WARN"; message += "No recovery email or phone — the student cannot self-recover. "; }

                    if (severity == "OK") okCount++; else if (severity == "WARN") { warnCount++; okCount++; }
                    else if (severity == "ERROR") errCount++;

                    items.Add(new PreviewRow
                    {
                        regno = cd.Regno,
                        name = cd.Name,
                        surname = surname,
                        given = given,
                        year = cd.Year,
                        campus = CampusName(cd.Campus),
                        programme = string.IsNullOrEmpty(cd.ProgName) ? cd.Prog : cd.ProgName,
                        email = email,
                        password = pw,
                        orgUnit = OrgUnitFor(o.OrgUnit, cd),
                        recovery = rec,
                        phone = phone,
                        severity = severity,
                        message = message.Trim(),
                        strategy = strategy
                    });
                }

                // Persist the draft + the reservations in one transaction: either the whole
                // proposal is on the server and protected, or none of it is.
                string batchRef = "SEC" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" +
                                  Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();
                int batchId;
                using (var tx = c.BeginTransaction())
                {
                    using (var cmd = new MySqlCommand(
                        "INSERT INTO campus_dynamics_portal.sems_email_batches " +
                        "(batch_ref,batch_type,status,params_json,total_rows,ok_rows,skipped_rows,failed_rows,created_by,created_at) " +
                        "VALUES (@r,'CREATE','DRAFT',@p,@t,0,0,0,@who,NOW())", c, tx))
                    {
                        cmd.Parameters.AddWithValue("@r", batchRef);
                        cmd.Parameters.AddWithValue("@p", optionsJson ?? "");
                        cmd.Parameters.AddWithValue("@t", items.Count);
                        cmd.Parameters.AddWithValue("@who", Actor());
                        cmd.ExecuteNonQuery();
                        batchId = (int)cmd.LastInsertedId;
                    }

                    int rowNo = 0;
                    foreach (PreviewRow it in items)
                    {
                        rowNo++;
                        using (var cmd = new MySqlCommand(
                            "INSERT INTO campus_dynamics_portal.sems_email_batch_items " +
                            "(batch_id,row_no,regno,student_name,email,temp_password,action,result,message,payload_json,created_at) " +
                            "VALUES (@b,@n,@r,@sn,@e,@p,@a,'PENDING',@m,@j,NOW())", c, tx))
                        {
                            cmd.Parameters.AddWithValue("@b", batchId);
                            cmd.Parameters.AddWithValue("@n", rowNo);
                            cmd.Parameters.AddWithValue("@r", it.regno);
                            cmd.Parameters.AddWithValue("@sn", Trunc(it.name, 150));
                            cmd.Parameters.AddWithValue("@e", N(it.email));
                            cmd.Parameters.AddWithValue("@p", N(it.password));
                            // ERROR / SKIP rows are stored too — the record of what was NOT done
                            // is as important as the record of what was.
                            cmd.Parameters.AddWithValue("@a", (it.severity == "ERROR" || it.severity == "SKIP") ? "SKIP" : "CREATE");
                            cmd.Parameters.AddWithValue("@m", Trunc(it.message, 250));
                            cmd.Parameters.AddWithValue("@j", Js().Serialize(it));
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Reserving here — not at commit — is what stops two admins previewing at
                    // the same moment from being handed the same address.
                    foreach (var r in toReserve)
                    {
                        using (var cmd = new MySqlCommand(
                            "INSERT INTO campus_dynamics_portal.sems_email_directory " +
                            "(email,local_part,domain,source,owner_type,owner_ref,display_name,status,first_seen_at,last_seen_at,notes) " +
                            "VALUES (@e,@l,@d,'PIPELINE','STUDENT',@o,@n,'RESERVED',NOW(),NOW(),@nt) " +
                            "ON DUPLICATE KEY UPDATE last_seen_at=NOW()", c, tx))
                        {
                            cmd.Parameters.AddWithValue("@e", r[0] + "@" + o.Domain);
                            cmd.Parameters.AddWithValue("@l", r[0]);
                            cmd.Parameters.AddWithValue("@d", o.Domain);
                            cmd.Parameters.AddWithValue("@o", r[1]);
                            cmd.Parameters.AddWithValue("@n", Trunc(r[2], 150));
                            cmd.Parameters.AddWithValue("@nt", "reserved by draft " + batchRef);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    tx.Commit();
                }

                return Js().Serialize(new
                {
                    success = true,
                    batchRef,
                    batchId,
                    total = items.Count,
                    ready = okCount,
                    warn = warnCount,
                    errors = errCount,
                    skipped = items.Count - okCount - errCount,
                    domain = o.Domain,
                    rows = items
                });
            }
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }

    private static string Trunc(string s, int n) { s = s ?? ""; return s.Length <= n ? s : s.Substring(0, n); }

    /// <summary>A personal address to use for account recovery — never one on our own domain.</summary>
    private static string RecoveryEmailFor(Cand c, string domain)
    {
        string p = (c.Personal ?? "").Trim().ToLowerInvariant();
        if (p.Length == 0 || !p.Contains("@")) return "";
        if (p.EndsWith("@" + domain)) return "";
        return p;
    }

    private static List<Cand> LoadCandidates(MySqlConnection c, Options o)
    {
        var list = new List<Cand>();
        var ps = new List<MySqlParameter>();
        var w = new StringBuilder("WHERE 1=1 ");

        if (o.Scope == "regnos")
        {
            if (o.Regnos.Count == 0) return list;
            var names = new List<string>();
            for (int i = 0; i < o.Regnos.Count && i < HardBatchCap; i++)
            { names.Add("@g" + i); ps.Add(new MySqlParameter("@g" + i, o.Regnos[i])); }
            w.Append("AND p.regno IN (").Append(string.Join(",", names.ToArray())).Append(") ");
        }
        else
        {
            if (!string.IsNullOrEmpty(o.Stage)) { w.Append("AND p.current_stage=@st "); ps.Add(new MySqlParameter("@st", o.Stage)); }
            if (!string.IsNullOrEmpty(o.Campus)) { w.Append("AND p.campus=@cm "); ps.Add(new MySqlParameter("@cm", o.Campus)); }
            if (!string.IsNullOrEmpty(o.Programme)) { w.Append("AND p.programme=@pr "); ps.Add(new MySqlParameter("@pr", o.Programme)); }
            if (!string.IsNullOrEmpty(o.Year)) { w.Append("AND p.admission_year=@yr "); ps.Add(new MySqlParameter("@yr", o.Year)); }
            if (!string.IsNullOrEmpty(o.Q))
            {
                w.Append("AND (p.regno LIKE @q OR p.student_name LIKE @q OR p.entryno LIKE @q) ");
                ps.Add(new MySqlParameter("@q", "%" + o.Q + "%"));
            }
            // A batch only ever creates addresses for students who do not have one.
            w.Append("AND IFNULL(p.email_address,'')='' ");
        }

        // CONVERT on the small (pipeline) side keeps the eq_ref lookup on acad_student's
        // primary key — the cross-DB collation difference would otherwise force a scan.
        using (var cmd = new MySqlCommand(
            "SELECT p.regno, IFNULL(p.entryno,'') entryno, IFNULL(p.student_name,'') nm, p.admission_year, " +
            "       IFNULL(p.campus,'') campus, IFNULL(p.programme,'') prog, IFNULL(p.email_address,'') cur, p.current_stage, " +
            "       IFNULL(s.firstname,'') firstname, IFNULL(s.othername,'') othername, IFNULL(s.studPhone,'') phone, " +
            "       IFNULL(s.email,'') personal, IFNULL(s.home_dist,'') dist, IFNULL(pr.progname,'') progname " +
            "FROM campus_dynamics_portal.sems_email_creations p " +
            "LEFT JOIN campus_dynamics.acad_student s ON s.regno = CONVERT(p.regno USING utf8) " +
            "LEFT JOIN campus_dynamics.acad_programme pr ON pr.progcode = CONVERT(p.programme USING utf8) " +
            w + "ORDER BY p.student_name, p.regno LIMIT " + o.Limit, c))
        {
            cmd.CommandTimeout = 180;
            foreach (var p in ps) cmd.Parameters.Add(new MySqlParameter(p.ParameterName, p.Value));
            using (var rd = cmd.ExecuteReader())
                while (rd.Read())
                    list.Add(new Cand
                    {
                        Regno = S(rd["regno"]),
                        EntryNo = S(rd["entryno"]),
                        Name = S(rd["nm"]),
                        Year = S(rd["admission_year"]),
                        Campus = S(rd["campus"]),
                        Prog = S(rd["prog"]),
                        ProgName = S(rd["progname"]),
                        CurEmail = S(rd["cur"]),
                        Stage = S(rd["current_stage"]),
                        First = S(rd["firstname"]),
                        Other = S(rd["othername"]),
                        Phone = S(rd["phone"]),
                        Personal = S(rd["personal"]),
                        District = S(rd["dist"])
                    });
        }
        return list;
    }

    // =================================================================
    //  EDIT a draft row — the admin's override, still uniqueness-checked
    // =================================================================
    public static string UpdateDraftEmail(string batchRef, string regno, string newEmail)
    {
        batchRef = (batchRef ?? "").Trim(); regno = (regno ?? "").Trim();
        newEmail = (newEmail ?? "").Trim().ToLowerInvariant();
        if (batchRef == "" || regno == "") return Fail("Missing draft or student.");
        if (!IsValidEmail(newEmail))
            return Fail("\"" + newEmail + "\" is not a valid address. Use letters, digits and dots only, starting with a letter.");
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                string batchStatus;
                int batchId = BatchIdOf(c, batchRef, "DRAFT", out batchStatus);
                if (batchId == 0) return Fail(DraftClosedMessage(batchRef, batchStatus));

                string local = newEmail.Substring(0, newEmail.IndexOf('@'));
                string domain = newEmail.Substring(newEmail.IndexOf('@') + 1);

                // The address has to be on the batch's own domain. An off-domain one used to be
                // accepted, reserved and then silently dropped from the Google sheet (which only
                // ever carries its own domain) — the student ended up with nothing and no error.
                string wantDomain = ReadOptions(ParamsOf(c, batchId)).Domain;
                if (!string.Equals(domain, wantDomain, StringComparison.OrdinalIgnoreCase))
                    return Fail("This batch creates addresses on @" + wantDomain + ". \"" + newEmail +
                                "\" is on @" + domain + ", which a Google Workspace sheet for @" + wantDomain +
                                " cannot carry.");

                // Taken by anybody other than this student?
                using (var q = new MySqlCommand(
                    "SELECT owner_ref, source, status FROM campus_dynamics_portal.sems_email_directory WHERE email=@e LIMIT 1", c))
                {
                    q.Parameters.AddWithValue("@e", newEmail);
                    using (var rd = q.ExecuteReader())
                        if (rd.Read() && !string.Equals(S(rd["owner_ref"]), regno, StringComparison.OrdinalIgnoreCase))
                            return Fail(newEmail + " is already held by " + S(rd["source"]).ToLowerInvariant() +
                                        " record " + (S(rd["owner_ref"]) == "" ? "(reserved)" : S(rd["owner_ref"])) + ".");
                }

                string oldEmail = "";
                using (var q = new MySqlCommand(
                    "SELECT IFNULL(email,'') FROM campus_dynamics_portal.sems_email_batch_items WHERE batch_id=@b AND regno=@r LIMIT 1", c))
                { q.Parameters.AddWithValue("@b", batchId); q.Parameters.AddWithValue("@r", regno); oldEmail = S(q.ExecuteScalar()); }

                using (var tx = c.BeginTransaction())
                {
                    if (oldEmail != "" && !oldEmail.Equals(newEmail, StringComparison.OrdinalIgnoreCase))
                        using (var d = new MySqlCommand(
                            "DELETE FROM campus_dynamics_portal.sems_email_directory WHERE email=@e AND status='RESERVED'", c, tx))
                        { d.Parameters.AddWithValue("@e", oldEmail); d.ExecuteNonQuery(); }

                    using (var ins = new MySqlCommand(
                        "INSERT INTO campus_dynamics_portal.sems_email_directory " +
                        "(email,local_part,domain,source,owner_type,owner_ref,status,first_seen_at,last_seen_at,notes) " +
                        "VALUES (@e,@l,@d,'PIPELINE','STUDENT',@o,'RESERVED',NOW(),NOW(),@nt) " +
                        "ON DUPLICATE KEY UPDATE last_seen_at=NOW(), owner_ref=@o", c, tx))
                    {
                        ins.Parameters.AddWithValue("@e", newEmail); ins.Parameters.AddWithValue("@l", local);
                        ins.Parameters.AddWithValue("@d", domain); ins.Parameters.AddWithValue("@o", regno);
                        ins.Parameters.AddWithValue("@nt", "reserved by draft " + batchRef + " (admin edit)");
                        ins.ExecuteNonQuery();
                    }

                    using (var up = new MySqlCommand(
                        "UPDATE campus_dynamics_portal.sems_email_batch_items " +
                        "SET email=@e, action='CREATE', message=CONCAT('address set by ',@who) WHERE batch_id=@b AND regno=@r", c, tx))
                    {
                        up.Parameters.AddWithValue("@e", newEmail); up.Parameters.AddWithValue("@who", Actor());
                        up.Parameters.AddWithValue("@b", batchId); up.Parameters.AddWithValue("@r", regno);
                        up.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
                return Js().Serialize(new { success = true, email = newEmail, message = "Address updated for " + regno + "." });
            }
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }

    private static int BatchIdOf(MySqlConnection c, string batchRef, string requiredStatus)
    {
        string ignored;
        return BatchIdOf(c, batchRef, requiredStatus, out ignored);
    }

    /// <summary>
    /// The batch id, or 0 — with <paramref name="actualStatus"/> set to what the batch really
    /// is (or "" when there is no such reference), so a refusal can name the reason instead of
    /// guessing at one.
    /// </summary>
    private static int BatchIdOf(MySqlConnection c, string batchRef, string requiredStatus, out string actualStatus)
    {
        actualStatus = "";
        int id = 0;
        using (var q = new MySqlCommand(
            "SELECT id, status FROM campus_dynamics_portal.sems_email_batches WHERE batch_ref=@r LIMIT 1", c))
        {
            q.Parameters.AddWithValue("@r", batchRef);
            using (var rd = q.ExecuteReader())
                if (rd.Read()) { id = Convert.ToInt32(rd[0]); actualStatus = S(rd[1]); }
        }
        if (id == 0) return 0;
        return (requiredStatus == null || actualStatus == requiredStatus) ? id : 0;
    }

    /// <summary>Plain-English reason a draft is not open for editing or committing.</summary>
    private static string DraftClosedMessage(string batchRef, string actualStatus)
    {
        switch (actualStatus)
        {
            case "": return "Draft " + batchRef + " no longer exists.";
            case "EXPIRED": return "Draft " + batchRef + " expired after " + DraftExpiryHours +
                                   " hours and released the addresses it was holding. Build it again.";
            case "CANCELLED": return "Draft " + batchRef + " was cancelled.";
            case "APPLIED":
            case "PARTIAL": return "Draft " + batchRef + " has already been applied.";
            default: return "Draft " + batchRef + " is " + actualStatus.ToLowerInvariant() + ", not open.";
        }
    }

    private static string ParamsOf(MySqlConnection c, int batchId)
    {
        using (var q = new MySqlCommand("SELECT IFNULL(params_json,'') FROM campus_dynamics_portal.sems_email_batches WHERE id=@b", c))
        { q.Parameters.AddWithValue("@b", batchId); return S(q.ExecuteScalar()); }
    }

    // =================================================================
    //  COMMIT — apply the approved draft
    // =================================================================
    public static string CommitBatch(string batchRef, string excludeJson)
    {
        batchRef = (batchRef ?? "").Trim();
        if (batchRef == "") return Fail("Missing draft reference.");
        var exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!string.IsNullOrWhiteSpace(excludeJson))
                foreach (var x in Js().Deserialize<List<string>>(excludeJson)) if (x != null) exclude.Add(x.Trim());
        }
        catch { }

        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                string batchStatus;
                int batchId = BatchIdOf(c, batchRef, "DRAFT", out batchStatus);
                if (batchId == 0) return Fail(DraftClosedMessage(batchRef, batchStatus));

                var o = ReadOptions(ParamsOf(c, batchId));

                var rows = new List<string[]>();     // regno, email, pw, payload
                using (var q = new MySqlCommand(
                    "SELECT regno, IFNULL(email,''), IFNULL(temp_password,''), IFNULL(payload_json,''), id " +
                    "FROM campus_dynamics_portal.sems_email_batch_items " +
                    "WHERE batch_id=@b AND action='CREATE' AND result='PENDING' ORDER BY row_no", c))
                {
                    q.Parameters.AddWithValue("@b", batchId);
                    using (var rd = q.ExecuteReader())
                        while (rd.Read())
                            rows.Add(new[] { S(rd[0]), S(rd[1]), S(rd[2]), S(rd[3]), S(rd[4]) });
                }

                int ok = 0, skipped = 0, failed = 0;
                var failures = new List<object>();

                foreach (var r in rows)
                {
                    string regno = r[0], email = r[1], pw = r[2], payload = r[3], itemId = r[4];
                    if (exclude.Contains(regno))
                    {
                        MarkItem(c, itemId, "SKIPPED", "excluded by the operator at commit");
                        ReleaseReservation(c, email);
                        skipped++; continue;
                    }
                    if (email == "" || pw == "")
                    {
                        MarkItem(c, itemId, "SKIPPED", "no address or password on the draft row");
                        skipped++; continue;
                    }
                    if (!email.EndsWith("@" + o.Domain, StringComparison.OrdinalIgnoreCase))
                    {
                        // A Google sheet only ever carries its own domain, so an off-domain
                        // address would be reserved here and then never exported — the student
                        // would sit waiting for an account nobody was ever asked to create.
                        MarkItem(c, itemId, "SKIPPED", "not on @" + o.Domain + " — cannot be created by this sheet");
                        ReleaseReservation(c, email);
                        skipped++; continue;
                    }

                    string org = "", rec = "", phone = "";
                    try
                    {
                        var pj = Js().Deserialize<Dictionary<string, object>>(payload);
                        org = GetS(pj, "orgUnit", ""); rec = GetS(pj, "recovery", ""); phone = GetS(pj, "phone", "");
                    }
                    catch { }

                    // One transaction per student: a bad row can never take the batch with it.
                    try
                    {
                        using (var tx = c.BeginTransaction())
                        {
                            string curEmail = null; int pipeId = 0; string stage = "";
                            using (var q = new MySqlCommand(
                                "SELECT id, IFNULL(email_address,''), current_stage FROM campus_dynamics_portal.sems_email_creations " +
                                "WHERE regno=@r LIMIT 1 FOR UPDATE", c, tx))
                            {
                                q.Parameters.AddWithValue("@r", regno);
                                using (var rd = q.ExecuteReader())
                                    if (rd.Read()) { pipeId = Convert.ToInt32(rd[0]); curEmail = S(rd[1]); stage = S(rd[2]); }
                            }

                            if (pipeId == 0)
                            { tx.Rollback(); MarkItem(c, itemId, "FAILED", "student is no longer in the pipeline"); failed++; failures.Add(new { regno, message = "not in pipeline" }); continue; }

                            // Idempotent: re-running a partly-applied batch is a no-op for done rows.
                            if (curEmail != "" && !curEmail.Equals(email, StringComparison.OrdinalIgnoreCase))
                            { tx.Rollback(); MarkItem(c, itemId, "SKIPPED", "already has " + curEmail); ReleaseReservation(c, email); skipped++; continue; }
                            if (curEmail.Equals(email, StringComparison.OrdinalIgnoreCase))
                            { tx.Rollback(); MarkItem(c, itemId, "OK", "already applied"); ok++; continue; }

                            // Is the address still ours? The UNIQUE index would catch a collision
                            // anyway, but it reports a duplicate-key number; the directory knows
                            // WHO holds it, and a draft can lose its reservation to the 24-hour
                            // sweep, so the answer is worth asking for out loud.
                            string heldBy;
                            using (var q = new MySqlCommand(
                                "SELECT IFNULL(owner_ref,'') FROM campus_dynamics_portal.sems_email_directory " +
                                "WHERE email=@e AND status<>'RELEASED' LIMIT 1", c, tx))
                            { q.Parameters.AddWithValue("@e", email); heldBy = S(q.ExecuteScalar()); }
                            if (heldBy != "" && !heldBy.Equals(regno, StringComparison.OrdinalIgnoreCase))
                            {
                                tx.Rollback();
                                MarkItem(c, itemId, "FAILED", email + " is now held by " + heldBy);
                                failed++; failures.Add(new { regno, message = email + " is now held by " + heldBy });
                                continue;
                            }

                            // PROPOSED, not issued. The address and password are recorded and
                            // reserved so the Google sheet can carry them, but the student's
                            // stage does not move and they are told nothing: the mailbox does
                            // not exist until Google says it does. The import promotes them.
                            using (var up = new MySqlCommand(
                                "UPDATE campus_dynamics_portal.sems_email_creations SET " +
                                " email_address=@e, temp_password=@p, google_status='PROPOSED', google_org_unit=@ou, " +
                                " recovery_email=@rc, recovery_phone=@ph, " +
                                " last_batch_id=@b, last_updated_by=@who, last_updated_at=NOW() " +
                                "WHERE id=@id", c, tx))
                            {
                                up.Parameters.AddWithValue("@e", email);
                                up.Parameters.AddWithValue("@p", pw);
                                up.Parameters.AddWithValue("@ou", N(org));
                                up.Parameters.AddWithValue("@rc", N(rec));
                                up.Parameters.AddWithValue("@ph", N(phone));
                                up.Parameters.AddWithValue("@b", batchId);
                                up.Parameters.AddWithValue("@who", Actor());
                                up.Parameters.AddWithValue("@id", pipeId);
                                up.ExecuteNonQuery();
                            }

                            // Upsert, not UPDATE: the reservation row may have been swept away
                            // by the 24-hour expiry, and an UPDATE that matches nothing would
                            // leave the directory blind to an address the pipeline has issued.
                            UpsertDirectory(c, tx, email, "PIPELINE", "STUDENT", regno, "", "ACTIVE",
                                            "issued by batch " + batchRef);

                            LogTx(c, tx, pipeId, regno, "propose_email", stage, stage,
                                  email + " proposed (batch " + batchRef + ") — awaiting Google");
                            // No student notification here on purpose: nothing has been created
                            // in Google yet, and a student told to collect an address that does
                            // not exist is worse than a student told nothing.
                            tx.Commit();
                        }
                        MarkItem(c, itemId, "OK", "created");
                        ok++;
                    }
                    catch (MySqlException mex)
                    {
                        string msg = mex.Number == 1062 ? "address was taken by another record in the meantime" : mex.Message;
                        MarkItem(c, itemId, "FAILED", Trunc(msg, 250));
                        failed++; failures.Add(new { regno, message = msg });
                    }
                    catch (Exception ex)
                    {
                        MarkItem(c, itemId, "FAILED", Trunc(ex.Message, 250));
                        failed++; failures.Add(new { regno, message = ex.Message });
                    }
                }

                string status = failed == 0 ? "APPLIED" : (ok > 0 ? "PARTIAL" : "FAILED");
                using (var up = new MySqlCommand(
                    "UPDATE campus_dynamics_portal.sems_email_batches SET status=@s, ok_rows=@o, skipped_rows=@k, failed_rows=@f, completed_at=NOW() WHERE id=@b", c))
                {
                    up.Parameters.AddWithValue("@s", status); up.Parameters.AddWithValue("@o", ok);
                    up.Parameters.AddWithValue("@k", skipped); up.Parameters.AddWithValue("@f", failed);
                    up.Parameters.AddWithValue("@b", batchId); up.ExecuteNonQuery();
                }

                return Js().Serialize(new
                {
                    success = true,
                    batchRef,
                    created = ok,
                    skipped,
                    failed,
                    status,
                    failures,
                    message = ok + " address(es) allocated and reserved" + (skipped > 0 ? ", " + skipped + " skipped" : "") + (failed > 0 ? ", " + failed + " failed" : "") +
                              ". They are not live until Google confirms them."
                });
            }
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }

    private static void MarkItem(MySqlConnection c, string itemId, string result, string message)
    {
        try
        {
            using (var up = new MySqlCommand(
                "UPDATE campus_dynamics_portal.sems_email_batch_items SET result=@r, message=@m WHERE id=@id", c))
            {
                up.Parameters.AddWithValue("@r", result);
                up.Parameters.AddWithValue("@m", Trunc(message, 250));
                up.Parameters.AddWithValue("@id", itemId);
                up.ExecuteNonQuery();
            }
        }
        catch { }
    }

    private static void ReleaseReservation(MySqlConnection c, string email)
    {
        if (string.IsNullOrEmpty(email)) return;
        try
        {
            using (var d = new MySqlCommand(
                "DELETE FROM campus_dynamics_portal.sems_email_directory WHERE email=@e AND status='RESERVED'", c))
            { d.Parameters.AddWithValue("@e", email); d.ExecuteNonQuery(); }
        }
        catch { }
    }

    /// <summary>
    /// Hands back every address that was allocated and exported but that Google never confirmed —
    /// the rows left behind when an upload runs out of licences or is abandoned.
    ///
    /// The address, password and org unit are cleared, the reservation is released so the name is
    /// free again, and the student returns to Pending with a clean slate. Confirmed accounts
    /// (IN_GOOGLE) and students already past Pending are never touched: those are live mailboxes.
    ///
    /// The address being surrendered is written to the activity log first, so what a student was
    /// once offered is always recoverable even though the field is now empty.
    /// </summary>
    public static string ReleaseUnconfirmed(string note)
    {
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                var rows = new List<string[]>();
                using (var q = new MySqlCommand(
                    "SELECT id, regno, IFNULL(email_address,'') FROM campus_dynamics_portal.sems_email_creations " +
                    "WHERE google_status IN ('PROPOSED','EXPORTED') AND current_stage='PENDING_CREATION' " +
                    "  AND IFNULL(email_address,'') <> '' ORDER BY regno", c))
                using (var rd = q.ExecuteReader())
                    while (rd.Read()) rows.Add(new[] { S(rd[0]), S(rd[1]), S(rd[2]) });

                if (rows.Count == 0)
                    return Js().Serialize(new { success = true, released = 0, message = "Nothing to release — no unconfirmed allocations." });

                int released = 0, failed = 0;
                foreach (var r in rows)
                {
                    string id = r[0], regno = r[1], email = r[2];
                    try
                    {
                        using (var tx = c.BeginTransaction())
                        {
                            LogTx(c, tx, 0, regno, "release_unconfirmed", "PENDING_CREATION", "PENDING_CREATION",
                                  email + " released — never confirmed by Google" + (string.IsNullOrWhiteSpace(note) ? "" : " (" + note.Trim() + ")"));

                            using (var up = new MySqlCommand(
                                "UPDATE campus_dynamics_portal.sems_email_creations SET " +
                                " email_address=NULL, temp_password=NULL, google_status='NOT_CREATED', google_org_unit=NULL, " +
                                " google_synced_at=NULL, google_last_seen_at=NULL, recovery_email=NULL, recovery_phone=NULL, " +
                                " exported_at=NULL, export_count=0, email_created_at=NULL, last_batch_id=NULL, " +
                                " last_updated_by=@who, last_updated_at=NOW() WHERE id=@id", c, tx))
                            {
                                up.Parameters.AddWithValue("@who", Actor());
                                up.Parameters.AddWithValue("@id", id);
                                up.ExecuteNonQuery();
                            }

                            using (var d = new MySqlCommand(
                                "DELETE FROM campus_dynamics_portal.sems_email_directory WHERE email=@e AND owner_ref=@o", c, tx))
                            { d.Parameters.AddWithValue("@e", email); d.Parameters.AddWithValue("@o", regno); d.ExecuteNonQuery(); }

                            tx.Commit();
                        }
                        released++;
                    }
                    catch { failed++; }
                }

                return Js().Serialize(new
                {
                    success = true,
                    released,
                    failed,
                    message = released + " unconfirmed allocation(s) released — those students are back to Pending with no address." +
                              (failed > 0 ? " " + failed + " could not be released." : "")
                });
            }
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }

    /// <summary>Throws the draft away and frees every address it was holding.</summary>
    public static string CancelBatch(string batchRef)
    {
        batchRef = (batchRef ?? "").Trim();
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                int batchId = BatchIdOf(c, batchRef, "DRAFT");
                if (batchId == 0) return Fail("That draft is not open.");
                using (var tx = c.BeginTransaction())
                {
                    using (var d = new MySqlCommand(
                        "DELETE d FROM campus_dynamics_portal.sems_email_directory d " +
                        "JOIN campus_dynamics_portal.sems_email_batch_items i ON i.email = d.email " +
                        "WHERE i.batch_id=@b AND d.status='RESERVED'", c, tx))
                    { d.Parameters.AddWithValue("@b", batchId); d.ExecuteNonQuery(); }
                    using (var up = new MySqlCommand(
                        "UPDATE campus_dynamics_portal.sems_email_batches SET status='CANCELLED', completed_at=NOW() WHERE id=@b", c, tx))
                    { up.Parameters.AddWithValue("@b", batchId); up.ExecuteNonQuery(); }
                    tx.Commit();
                }
                return Js().Serialize(new { success = true, message = "Draft cancelled and all reserved addresses released." });
            }
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }

    // ── activity log + notification (same tables the single-student flow uses) ──
    private static void LogTx(MySqlConnection c, MySqlTransaction tx, int creationId, string regno, string action, string from, string to, string detail)
    {
        try
        {
            using (var cmd = new MySqlCommand(
                "INSERT INTO campus_dynamics_portal.sems_activity_log (creation_id,regno,action,stage_from,stage_to,actor,actor_role,detail,created_at) " +
                "VALUES (@id,@r,@a,@f,@t,@who,'ADMIN',@d,NOW())", c, tx))
            {
                cmd.Parameters.AddWithValue("@id", creationId <= 0 ? (object)DBNull.Value : creationId);
                cmd.Parameters.AddWithValue("@r", regno ?? "");
                cmd.Parameters.AddWithValue("@a", action);
                cmd.Parameters.AddWithValue("@f", N(from));
                cmd.Parameters.AddWithValue("@t", N(to));
                cmd.Parameters.AddWithValue("@who", Actor());
                cmd.Parameters.AddWithValue("@d", N(detail));
                cmd.ExecuteNonQuery();
            }
        }
        catch { }
    }

    private static void NotifyTx(MySqlConnection c, MySqlTransaction tx, string regno, string title, string msg, string icon)
    {
        try
        {
            using (var cmd = new MySqlCommand(
                "INSERT INTO campus_dynamics_portal.sems_notifications (regno,title,message,icon,is_read,created_at) VALUES (@r,@t,@m,@i,'No',NOW())", c, tx))
            {
                cmd.Parameters.AddWithValue("@r", regno); cmd.Parameters.AddWithValue("@t", title);
                cmd.Parameters.AddWithValue("@m", N(msg)); cmd.Parameters.AddWithValue("@i", N(icon));
                cmd.ExecuteNonQuery();
            }
        }
        catch { }
    }

    // =================================================================
    //  BATCH HISTORY
    // =================================================================
    public static string BatchList(int limit)
    {
        if (limit < 1 || limit > 200) limit = 30;
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                var list = new List<object>();
                using (var cmd = new MySqlCommand(
                    "SELECT batch_ref, batch_type, status, total_rows, ok_rows, skipped_rows, failed_rows, " +
                    "IFNULL(created_by,'') who, DATE_FORMAT(created_at,'%Y-%m-%d %H:%i') at, IFNULL(notes,'') notes " +
                    "FROM campus_dynamics_portal.sems_email_batches ORDER BY id DESC LIMIT " + limit, c))
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read())
                        list.Add(new
                        {
                            batchRef = S(rd["batch_ref"]), type = S(rd["batch_type"]), status = S(rd["status"]),
                            total = Convert.ToInt32(rd["total_rows"]), ok = Convert.ToInt32(rd["ok_rows"]),
                            skipped = Convert.ToInt32(rd["skipped_rows"]), failed = Convert.ToInt32(rd["failed_rows"]),
                            who = S(rd["who"]), at = S(rd["at"]), notes = S(rd["notes"])
                        });
                return Js().Serialize(new { success = true, batches = list });
            }
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }

    public static string BatchDetail(string batchRef, int limit)
    {
        batchRef = (batchRef ?? "").Trim();
        if (limit < 1 || limit > 3000) limit = 500;
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                object head = null;
                using (var cmd = new MySqlCommand(
                    "SELECT batch_ref, batch_type, status, total_rows, ok_rows, skipped_rows, failed_rows, IFNULL(params_json,'') pj, " +
                    "IFNULL(created_by,'') who, DATE_FORMAT(created_at,'%Y-%m-%d %H:%i') at FROM campus_dynamics_portal.sems_email_batches WHERE batch_ref=@r", c))
                {
                    cmd.Parameters.AddWithValue("@r", batchRef);
                    using (var rd = cmd.ExecuteReader())
                        if (rd.Read())
                            head = new
                            {
                                batchRef = S(rd["batch_ref"]), type = S(rd["batch_type"]), status = S(rd["status"]),
                                total = Convert.ToInt32(rd["total_rows"]), ok = Convert.ToInt32(rd["ok_rows"]),
                                skipped = Convert.ToInt32(rd["skipped_rows"]), failed = Convert.ToInt32(rd["failed_rows"]),
                                who = S(rd["who"]), at = S(rd["at"]), options = S(rd["pj"])
                            };
                }
                if (head == null) return Fail("Batch not found.");

                var rows = new List<object>();
                using (var cmd = new MySqlCommand(
                    "SELECT i.row_no, i.regno, IFNULL(i.student_name,'') nm, IFNULL(i.email,'') em, IFNULL(i.temp_password,'') pw, " +
                    "IFNULL(i.action,'') act, i.result, IFNULL(i.message,'') msg, IFNULL(i.payload_json,'') pj " +
                    "FROM campus_dynamics_portal.sems_email_batch_items i " +
                    "JOIN campus_dynamics_portal.sems_email_batches b ON b.id=i.batch_id " +
                    "WHERE b.batch_ref=@r ORDER BY i.row_no LIMIT " + limit, c))
                {
                    cmd.Parameters.AddWithValue("@r", batchRef);
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read())
                        {
                            string sev = "OK", org = "", rec = "", ph = "", sur = "", giv = "", camp = "", prog = "", yr = "";
                            try
                            {
                                var pj = Js().Deserialize<Dictionary<string, object>>(S(rd["pj"]));
                                sev = GetS(pj, "severity", "OK"); org = GetS(pj, "orgUnit", ""); rec = GetS(pj, "recovery", "");
                                ph = GetS(pj, "phone", ""); sur = GetS(pj, "surname", ""); giv = GetS(pj, "given", "");
                                camp = GetS(pj, "campus", ""); prog = GetS(pj, "programme", ""); yr = GetS(pj, "year", "");
                            }
                            catch { }
                            rows.Add(new
                            {
                                rowNo = Convert.ToInt32(rd["row_no"]), regno = S(rd["regno"]), name = S(rd["nm"]),
                                email = S(rd["em"]), password = S(rd["pw"]), action = S(rd["act"]), result = S(rd["result"]),
                                message = S(rd["msg"]), severity = sev, orgUnit = org, recovery = rec, phone = ph,
                                surname = sur, given = giv, campus = camp, programme = prog, year = yr
                            });
                        }
                }
                return Js().Serialize(new { success = true, batch = head, rows });
            }
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }

    // =================================================================
    //  DIRECTORY health — duplicates and coverage
    // =================================================================
    public static string DirectoryStats()
    {
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                SyncDirectory(c, DefaultDomain);
                Func<string, int> n = w =>
                {
                    using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM campus_dynamics_portal.sems_email_directory " + w, c))
                    { var v = cmd.ExecuteScalar(); return v == null || v == DBNull.Value ? 0 : Convert.ToInt32(v); }
                };
                int total = n(""), students = n("WHERE source IN ('STUDENT','PIPELINE')"), staff = n("WHERE source='STAFF'"),
                    google = n("WHERE source='GOOGLE'"), reserved = n("WHERE status='RESERVED'"), sys = n("WHERE source='RESERVED'");

                // The same address on two student records — the exact fault this module exists
                // to stop. Surfaced rather than silently repaired: a human must decide who keeps it.
                var dups = new List<object>();
                using (var cmd = new MySqlCommand(
                    "SELECT LOWER(TRIM(email)) e, COUNT(*) n, GROUP_CONCAT(TRIM(regno) SEPARATOR ', ') regs " +
                    "FROM campus_dynamics.acad_student WHERE email LIKE '%@mru.ac.ug' " +
                    "GROUP BY 1 HAVING n > 1 ORDER BY n DESC, e LIMIT 200", c))
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read())
                        dups.Add(new { email = S(rd["e"]), count = Convert.ToInt32(rd["n"]), regnos = S(rd["regs"]) });

                var byGoogle = new List<object>();
                using (var cmd = new MySqlCommand(
                    "SELECT google_status, COUNT(*) n FROM campus_dynamics_portal.sems_email_creations " +
                    "WHERE IFNULL(email_address,'')<>'' GROUP BY google_status", c))
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read()) byGoogle.Add(new { status = S(rd[0]), count = Convert.ToInt32(rd[1]) });

                return Js().Serialize(new { success = true, total, students, staff, google, reserved, system = sys, duplicates = dups, googleStatus = byGoogle });
            }
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }

    /// <summary>Live check for the single-student Create modal and the wizard's edit box.</summary>
    public static string CheckAddress(string email, string regno)
    {
        email = (email ?? "").Trim().ToLowerInvariant();
        regno = (regno ?? "").Trim();
        if (!IsValidEmail(email))
            return Js().Serialize(new { success = true, available = false, reason = "Not a valid address — letters, digits and dots only, starting with a letter." });
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                using (var q = new MySqlCommand(
                    "SELECT source, IFNULL(owner_ref,'') owner, IFNULL(display_name,'') nm, status " +
                    "FROM campus_dynamics_portal.sems_email_directory WHERE email=@e LIMIT 1", c))
                {
                    q.Parameters.AddWithValue("@e", email);
                    using (var rd = q.ExecuteReader())
                        if (rd.Read())
                        {
                            string owner = S(rd["owner"]);
                            if (owner != "" && owner.Equals(regno, StringComparison.OrdinalIgnoreCase))
                                return Js().Serialize(new { success = true, available = true, reason = "Already this student's own address." });
                            string src = S(rd["source"]).ToLowerInvariant();
                            return Js().Serialize(new
                            {
                                success = true,
                                available = false,
                                reason = "Taken — " + src + (owner == "" ? "" : " (" + owner + (S(rd["nm"]) == "" ? "" : ", " + S(rd["nm"])) + ")") +
                                         (S(rd["status"]) == "RESERVED" ? ", reserved by an open draft" : "")
                            });
                        }
                }
                return Js().Serialize(new { success = true, available = true, reason = "Available." });
            }
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }

    /// <summary>Suggests the house address for one student — used by the single-student modal.</summary>
    public static string SuggestFor(string regno, int otherLen)
    {
        regno = (regno ?? "").Trim();
        if (otherLen < 1 || otherLen > 12) otherLen = 3;
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                SyncDirectory(c, DefaultDomain);
                var taken = LoadTaken(c, DefaultDomain);
                string first = "", other = "", year = "";
                using (var q = new MySqlCommand(
                    "SELECT IFNULL(s.firstname,''), IFNULL(s.othername,''), IFNULL(s.entryyear,'') FROM campus_dynamics.acad_student s WHERE s.regno=@r LIMIT 1", c))
                {
                    q.Parameters.AddWithValue("@r", regno);
                    using (var rd = q.ExecuteReader())
                        if (rd.Read()) { first = S(rd[0]); other = S(rd[1]); year = S(rd[2]); }
                        else return Fail("Student not found: " + regno);
                }
                string strat;
                string house = Allocate(taken, other, first, year, otherLen, out strat);      // surname = othername
                var alt = new List<string>();
                if (house != "") { alt.Add(house + "@" + DefaultDomain); taken.Add(house); }
                string swapped = Allocate(taken, first, other, year, otherLen, out strat);    // names the other way round
                if (swapped != "" && swapped != house) alt.Add(swapped + "@" + DefaultDomain);
                return Js().Serialize(new
                {
                    success = true,
                    regno,
                    surname = other,
                    given = first,
                    year,
                    suggestions = alt,
                    password = DefaultPassword
                });
            }
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }
}
