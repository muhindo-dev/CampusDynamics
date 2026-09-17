using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

// =====================================================================
//  SEMS ⇄ Google Workspace interchange.
//
//  OUT  BuildExportCsv  — the exact 28-column bulk-upload sheet.
//       Two modes, and the distinction matters: a CREATE export carries
//       passwords (Google needs them for new accounts); an UPDATE export
//       leaves Password blank, because re-uploading a password for an
//       account that already exists RESETS the student out of their mail.
//
//  IN   ImportParse     — a Google export (or any sheet on the template)
//                         parsed into sems_import_staging and classified
//                         row by row: confirm / adopt / email-change /
//                         suspend / orphan / error. Nothing is applied.
//       ImportApply     — applies only the classes the admin ticked.
//
//  Employee ID carries the student number both ways, so a sheet that has
//  been through Google still matches back to the right student even if
//  somebody edited the name or the address in the admin console.
// =====================================================================
public static partial class SemsBatch
{
    // =================================================================
    //  EXPORT
    // =================================================================
    public class ExportScope
    {
        public string Mode = "create";        // create | update | all
        public string BatchRef = "";
        public string Stage = "", Campus = "", Programme = "", Year = "", GoogleStatus = "";
        public List<string> Regnos = new List<string>();
        public bool ChangePwNext = true;
        public string Domain = DefaultDomain;
        /// <summary>
        /// intake = file each student in their OWN admission year's org unit; fixed = one org
        /// unit for the whole sheet. A single batch routinely spans four intakes, so "intake"
        /// is the default — one path for all of them files a 2023 finalist in the 2026 cohort.
        /// </summary>
        public string OrgUnitMode = "intake";        // intake | fixed
        /// <summary>The single org unit, or the fallback when an intake has none. Must already exist in Google.</summary>
        public string OrgUnit = "/";
        public int Limit = HardBatchCap;
    }

    public static ExportScope ReadScope(string json)
    {
        var s = new ExportScope();
        if (string.IsNullOrWhiteSpace(json)) return s;
        var d = Js().Deserialize<Dictionary<string, object>>(json);
        s.Mode = GetS(d, "mode", s.Mode).ToLowerInvariant();
        s.BatchRef = GetS(d, "batchRef", "");
        s.Stage = GetS(d, "stage", "");
        s.Campus = GetS(d, "campus", "");
        s.Programme = GetS(d, "programme", "");
        s.Year = GetS(d, "year", "");
        s.GoogleStatus = GetS(d, "googleStatus", "");
        s.ChangePwNext = GetB(d, "changePwNext", true);
        s.Domain = GetS(d, "domain", s.Domain).ToLowerInvariant().TrimStart('@');
        s.OrgUnit = NormaliseOrgUnit(GetS(d, "orgUnit", s.OrgUnit));
        s.OrgUnitMode = GetS(d, "orgUnitMode", s.OrgUnitMode).ToLowerInvariant();
        s.Limit = Math.Max(1, Math.Min(HardBatchCap * 5, GetI(d, "limit", s.Limit)));
        object rr;
        if (d != null && d.TryGetValue("regnos", out rr) && rr is System.Collections.IEnumerable && !(rr is string))
            foreach (var x in (System.Collections.IEnumerable)rr)
                if (x != null && x.ToString().Trim() != "") s.Regnos.Add(x.ToString().Trim());
        return s;
    }

    /// <summary>Header-only sheet, for an admin who wants to fill it in by hand.</summary>
    public static string ExportTemplateCsv()
    {
        return string.Join(",", GoogleHeaders.Select(CsvCell).ToArray()) + "\r\n";
    }

    /// <summary>
    /// Builds the Google sheet. Returns the CSV and, out of band, how many rows it holds —
    /// the caller streams it as a download and records the export as a batch.
    /// </summary>
    public static string BuildExportCsv(ExportScope sc, out int rowCount, out string batchRef)
    {
        rowCount = 0;
        batchRef = "";
        var sb = new StringBuilder();
        sb.Append(string.Join(",", GoogleHeaders.Select(CsvCell).ToArray())).Append("\r\n");

        var ps = new List<MySqlParameter>();

        // A Google Workspace sheet can only contain addresses ON the domain it is uploaded to.
        // One legacy pipeline row carried a gmail address (with the address itself stored as
        // the password); it has no business in this file and Google would reject the row.
        var w = new StringBuilder("WHERE IFNULL(p.email_address,'') <> '' AND LOWER(TRIM(p.email_address)) LIKE @dom ");
        ps.Add(new MySqlParameter("@dom", "%@" + (string.IsNullOrEmpty(sc.Domain) ? DefaultDomain : sc.Domain)));

        if (!string.IsNullOrEmpty(sc.BatchRef))
        {
            w.Append("AND p.regno IN (SELECT i.regno FROM campus_dynamics_portal.sems_email_batch_items i " +
                     "JOIN campus_dynamics_portal.sems_email_batches b ON b.id=i.batch_id " +
                     "WHERE b.batch_ref=@br AND i.result='OK') ");
            ps.Add(new MySqlParameter("@br", sc.BatchRef));
        }
        else if (sc.Regnos.Count > 0)
        {
            var names = new List<string>();
            for (int i = 0; i < sc.Regnos.Count && i < HardBatchCap; i++)
            { names.Add("@r" + i); ps.Add(new MySqlParameter("@r" + i, sc.Regnos[i])); }
            w.Append("AND p.regno IN (").Append(string.Join(",", names.ToArray())).Append(") ");
        }
        else
        {
            if (!string.IsNullOrEmpty(sc.Stage)) { w.Append("AND p.current_stage=@st "); ps.Add(new MySqlParameter("@st", sc.Stage)); }
            if (!string.IsNullOrEmpty(sc.Campus)) { w.Append("AND p.campus=@cm "); ps.Add(new MySqlParameter("@cm", sc.Campus)); }
            if (!string.IsNullOrEmpty(sc.Programme)) { w.Append("AND p.programme=@pr "); ps.Add(new MySqlParameter("@pr", sc.Programme)); }
            if (!string.IsNullOrEmpty(sc.Year)) { w.Append("AND p.admission_year=@yr "); ps.Add(new MySqlParameter("@yr", sc.Year)); }
            if (!string.IsNullOrEmpty(sc.GoogleStatus)) { w.Append("AND p.google_status=@gs "); ps.Add(new MySqlParameter("@gs", sc.GoogleStatus)); }
        }

        // The safety rail: a sheet that creates accounts only ever contains accounts Google
        // has not confirmed, and an "update" sheet never contains a password.
        // PROPOSED and EXPORTED both mean "not live yet" — the sheet may have been lost, or
        // the upload never done; a second download must give the same addresses.
        if (sc.Mode == "create" || sc.Mode == "awaiting")
            w.Append("AND p.google_status IN ('NOT_CREATED','PROPOSED','EXPORTED') ");
        else if (sc.Mode == "update") w.Append("AND p.google_status IN ('IN_GOOGLE','SUSPENDED') ");

        using (var c = new MySqlConnection(Conn))
        {
            c.Open();

            // A batch routinely spans several admission years — the last one covered 2023 to
            // 2026 — and the Google sheet carries an org unit PER ROW, so each student can be
            // filed in their own cohort instead of all of them landing in one place.
            Dictionary<int, string> byIntake = null;
            if (sc.OrgUnitMode != "fixed")
            {
                Dictionary<string, int> discovered;
                byIntake = IntakeOrgUnits(c, out discovered);
            }

            var regnos = new List<string>();
            var pwFixups = new List<string>();      // regnos whose stored password the sheet has just set
            using (var cmd = new MySqlCommand(
                "SELECT p.regno, IFNULL(p.student_name,'') nm, p.email_address, IFNULL(p.temp_password,'') pw, " +
                "  IFNULL(p.google_org_unit,'') ou, IFNULL(p.recovery_email,'') rec, IFNULL(p.recovery_phone,'') ph, " +
                "  p.admission_year, IFNULL(p.campus,'') campus, IFNULL(p.programme,'') prog, p.google_status, p.current_stage, " +
                "  IFNULL(s.firstname,'') firstname, IFNULL(s.othername,'') othername, IFNULL(s.studPhone,'') sphone, " +
                "  IFNULL(s.email,'') personal, IFNULL(s.home_dist,'') dist, IFNULL(pr.progname,'') progname " +
                "FROM campus_dynamics_portal.sems_email_creations p " +
                "LEFT JOIN campus_dynamics.acad_student s ON s.regno = CONVERT(p.regno USING utf8) " +
                "LEFT JOIN campus_dynamics.acad_programme pr ON pr.progcode = CONVERT(p.programme USING utf8) " +
                w + "ORDER BY p.student_name, p.regno LIMIT " + sc.Limit, c))
            {
                cmd.CommandTimeout = 300;
                foreach (var p in ps) cmd.Parameters.Add(new MySqlParameter(p.ParameterName, p.Value));
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read())
                    {
                        string regno = S(rd["regno"]);
                        string given = S(rd["firstname"]);
                        string surname = S(rd["othername"]);
                        if (given == "" && surname == "")          // fall back to the pipeline's copy
                        {
                            var t = S(rd["nm"]).Split(' ');
                            given = t.Length > 0 ? t[0] : "";
                            surname = t.Length > 1 ? string.Join(" ", t.Skip(1).ToArray()) : "";
                        }
                        // Google rejects a blank required name; a placeholder is better than a
                        // failed row, and it is visible enough that somebody will fix it.
                        if (given.Trim() == "") given = surname.Trim() == "" ? "Student" : surname;
                        if (surname.Trim() == "") surname = given;

                        // Org unit must ALREADY EXIST in Google — it will not be created by an
                        // upload, and a missing one fails every row with OU_INVALID. "/" is the
                        // root org unit, which always exists.
                        //
                        // Normalised on the way into the sheet, not just on the way into our own
                        // records. An export once carried "students/" exactly as it was typed and
                        // Google refused all 320 rows; the wizard had been normalising the same
                        // string to "/students" for its own copy, so the fault was invisible here.
                        string org = NormaliseOrgUnit(sc.OrgUnit);
                        if (byIntake != null)
                        {
                            int yr; string cohort;
                            if (int.TryParse(S(rd["admission_year"]), out yr) && byIntake.TryGetValue(yr, out cohort))
                                org = cohort;            // else the chosen path stands as the fallback
                        }
                        string email = S(rd["email_address"]);
                        string pw = S(rd["pw"]);
                        // Only a CONFIRMED Google account suppresses the password. A proposed or
                        // exported one does not — that sheet may never have been uploaded.
                        string gst = S(rd["google_status"]);
                        bool inGoogle = (gst == "IN_GOOGLE" || gst == "SUSPENDED");

                        // Every account this sheet CREATES carries the one house password, and
                        // the sheet tells Google to force a change at first sign-in. A single
                        // known password is what lets a student who never received a slip of
                        // paper still sign in on day one.
                        //
                        // Only students still WAITING for an account are normalised. Anyone
                        // already past Pending was handed credentials at some point — theirs may
                        // already be live in Google under a result we never imported — so their
                        // password is only replaced when it is one Google would reject outright
                        // (blank, too short, or the address used as its own password: a legacy
                        // import did exactly that).
                        if (!inGoogle)
                        {
                            bool waiting = S(rd["current_stage"]) == "PENDING_CREATION";
                            if (waiting ? pw != DefaultPassword : !IsUsablePassword(pw, email))
                            {
                                pw = DefaultPassword;
                                pwFixups.Add(regno);
                            }
                        }

                        // Deliberately minimal: the five fields Google REQUIRES to create an
                        // account, plus Employee ID (the student number, which is how the sheet
                        // finds its way home on import) and the first-sign-in password flag.
                        // Every other column is left blank. Each extra field was a way for the
                        // upload to fail — a phone Excel had rewritten, an org unit that did not
                        // exist, a department nobody had created — for data the university
                        // already holds in its own records.
                        var cells = new string[]
                        {
                            given,                                      // First Name      [required]
                            surname,                                    // Last Name       [required]
                            email,                                      // Email Address   [required]
                            inGoogle ? "" : pw,                         // Password        [required] — blank for updates
                            "",                                         // Password Hash Function
                            org,                                        // Org Unit Path   [required]
                            "",                                         // New Primary Email
                            "",                                         // Recovery Email
                            "",                                         // Home Secondary Email
                            "",                                         // Work Secondary Email
                            "",                                         // Recovery Phone
                            "",                                         // Work Phone
                            "",                                         // Home Phone
                            "",                                         // Mobile Phone
                            "",                                         // Work Address
                            "",                                         // Home Address
                            regno,                                      // Employee ID — the import join key
                            "",                                         // Employee Type
                            "",                                         // Employee Title
                            "",                                         // Manager Email
                            "",                                         // Department
                            "",                                         // Cost Center
                            "",                                         // Building ID
                            "",                                         // Floor Name
                            "",                                         // Floor Section
                            inGoogle ? "" : (sc.ChangePwNext ? "TRUE" : "FALSE"),   // Change Password at Next Sign-In
                            "",                                         // New Status
                            ""                                          // Advanced Protection
                        };
                        sb.Append(string.Join(",", cells.Select(CsvCell).ToArray())).Append("\r\n");
                        regnos.Add(regno);
                        rowCount++;
                    }
            }

            // Persist the passwords the sheet just set, BEFORE the sheet leaves, so the record
            // and the file can never disagree about what a student was given. One statement per
            // 400 students rather than one per student: they all carry the same value.
            for (int i = 0; i < pwFixups.Count; i += 400)
            {
                var chunk = pwFixups.Skip(i).Take(400).ToList();
                var names = new List<string>();
                var chunkPs = new List<MySqlParameter>();
                for (int k = 0; k < chunk.Count; k++)
                { names.Add("@f" + k); chunkPs.Add(new MySqlParameter("@f" + k, chunk[k])); }
                using (var up = new MySqlCommand(
                    "UPDATE campus_dynamics_portal.sems_email_creations SET temp_password=@p, last_updated_at=NOW() " +
                    "WHERE regno IN (" + string.Join(",", names.ToArray()) + ")", c))
                {
                    up.Parameters.AddWithValue("@p", DefaultPassword);
                    foreach (var pp in chunkPs) up.Parameters.Add(pp);
                    up.ExecuteNonQuery();
                }
            }

            if (rowCount > 0)
            {
                batchRef = "SEX" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" +
                           Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();
                int batchId;
                using (var cmd = new MySqlCommand(
                    "INSERT INTO campus_dynamics_portal.sems_email_batches " +
                    "(batch_ref,batch_type,status,params_json,total_rows,ok_rows,created_by,created_at,completed_at,notes) " +
                    "VALUES (@r,'EXPORT','APPLIED',@p,@t,@t,@who,NOW(),NOW(),@n)", c))
                {
                    cmd.Parameters.AddWithValue("@r", batchRef);
                    cmd.Parameters.AddWithValue("@p", Js().Serialize(sc));
                    cmd.Parameters.AddWithValue("@t", rowCount);
                    cmd.Parameters.AddWithValue("@who", Actor());
                    cmd.Parameters.AddWithValue("@n", Trunc(sc.Mode + " sheet for Google Workspace", 250));
                    cmd.ExecuteNonQuery();
                    batchId = (int)cmd.LastInsertedId;
                }

                // Mark what left the building, so "exported but never confirmed in Google" is
                // a question the console can answer later.
                for (int i = 0; i < regnos.Count; i += 400)
                {
                    var chunk = regnos.Skip(i).Take(400).ToList();
                    var names = new List<string>();
                    var cmdPs = new List<MySqlParameter>();
                    for (int k = 0; k < chunk.Count; k++)
                    { names.Add("@x" + k); cmdPs.Add(new MySqlParameter("@x" + k, chunk[k])); }
                    using (var cmd = new MySqlCommand(
                        "UPDATE campus_dynamics_portal.sems_email_creations SET exported_at=NOW(), export_count=export_count+1, " +
                        "google_status=CASE WHEN google_status IN ('NOT_CREATED','PROPOSED') THEN 'EXPORTED' ELSE google_status END " +
                        "WHERE regno IN (" + string.Join(",", names.ToArray()) + ")", c))
                    { foreach (var p in cmdPs) cmd.Parameters.Add(p); cmd.ExecuteNonQuery(); }

                    using (var cmd = new MySqlCommand(
                        "INSERT INTO campus_dynamics_portal.sems_email_batch_items (batch_id,row_no,regno,action,result,created_at) " +
                        "SELECT @b, 0, regno, 'EXPORT', 'OK', NOW() FROM campus_dynamics_portal.sems_email_creations " +
                        "WHERE regno IN (" + string.Join(",", names.ToArray()) + ")", c))
                    {
                        cmd.Parameters.AddWithValue("@b", batchId);
                        foreach (var p in cmdPs) cmd.Parameters.Add(new MySqlParameter(p.ParameterName, p.Value));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// The whole first half of the protocol in one action: take the students who are eligible,
    /// generated and still pending, allocate a collision-free address for each, record it as
    /// PROPOSED (reserved, but not yet the student's — their stage does not move and they are
    /// told nothing), and return the Google sheet that will create the accounts.
    ///
    /// The addresses only become real when that sheet has been uploaded and the Google export
    /// is imported back, which is what promotes each student and notifies them.
    /// </summary>
    public static string BuildPendingSheet(string optionsJson, out int rowCount, out string batchRef, out string message)
    {
        rowCount = 0; batchRef = ""; message = "";
        try
        {
            var prev = Js().Deserialize<Dictionary<string, object>>(PreviewBatch(optionsJson));
            if (!GetB(prev, "success", false))
            { message = GetS(prev, "message", "There are no pending students to export."); return ""; }

            batchRef = GetS(prev, "batchRef", "");
            if (batchRef == "") { message = "The allocation did not produce a batch."; return ""; }

            var com = Js().Deserialize<Dictionary<string, object>>(CommitBatch(batchRef, "[]"));
            if (!GetB(com, "success", false))
            { message = GetS(com, "message", "The allocated addresses could not be reserved."); return ""; }

            var o = ReadOptions(optionsJson);
            var sc = new ExportScope
            {
                Mode = "create",
                BatchRef = batchRef,
                Domain = o.Domain,
                ChangePwNext = o.ChangePwNext,
                OrgUnit = o.OrgUnit,
                OrgUnitMode = o.OrgUnitMode
            };
            string bref2;
            string csv = BuildExportCsv(sc, out rowCount, out bref2);
            if (rowCount == 0)
                message = "Addresses were allocated but the sheet came back empty — check the batch in Recent batches.";
            return csv;
        }
        catch (Exception ex) { message = ex.Message; return ""; }
    }

    /// <summary>
    /// The internal hand-out sheet: who got which address and which temporary password.
    /// Deliberately a different file from the Google sheet — this one is credentials, and
    /// it is generated from what was actually applied, not from what was proposed.
    /// </summary>
    public static string BuildCredentialsCsv(string batchRef, out int rowCount)
    {
        rowCount = 0;
        var sb = new StringBuilder();
        sb.Append("Student No.,Student Name,Programme,Campus,Entry Year,University Email,Temporary Password,Org Unit,Stage,Google Status\r\n");
        using (var c = new MySqlConnection(Conn))
        {
            c.Open();
            string sql =
                "SELECT p.regno, IFNULL(p.student_name,'') nm, IFNULL(pr.progname,IFNULL(p.programme,'')) prog, " +
                "IFNULL(p.campus,'') campus, p.admission_year, IFNULL(p.email_address,'') em, IFNULL(p.temp_password,'') pw, " +
                "IFNULL(p.google_org_unit,'') ou, p.current_stage, p.google_status " +
                "FROM campus_dynamics_portal.sems_email_creations p " +
                "LEFT JOIN campus_dynamics.acad_programme pr ON pr.progcode = CONVERT(p.programme USING utf8) " +
                (string.IsNullOrWhiteSpace(batchRef)
                    ? "WHERE IFNULL(p.email_address,'')<>'' "
                    : "WHERE p.regno IN (SELECT i.regno FROM campus_dynamics_portal.sems_email_batch_items i " +
                      "JOIN campus_dynamics_portal.sems_email_batches b ON b.id=i.batch_id WHERE b.batch_ref=@r AND i.result='OK') ") +
                "ORDER BY p.student_name, p.regno LIMIT " + (HardBatchCap * 5);
            using (var cmd = new MySqlCommand(sql, c))
            {
                cmd.CommandTimeout = 300;
                if (!string.IsNullOrWhiteSpace(batchRef)) cmd.Parameters.AddWithValue("@r", batchRef.Trim());
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read())
                    {
                        var cells = new[]
                        {
                            S(rd["regno"]), S(rd["nm"]), S(rd["prog"]), CampusName(S(rd["campus"])),
                            S(rd["admission_year"]), S(rd["em"]), S(rd["pw"]), S(rd["ou"]),
                            S(rd["current_stage"]), S(rd["google_status"])
                        };
                        sb.Append(string.Join(",", cells.Select(CsvCell).ToArray())).Append("\r\n");
                        rowCount++;
                    }
            }
        }
        return sb.ToString();
    }

    /// <summary>One org unit Google is known to hold.</summary>
    private class OrgUnitRow
    {
        public string path { get; set; }
        public int accounts { get; set; }
        /// <summary>An intake cohort ("2026 - 2027") rather than a faculty or a programme.</summary>
        public bool yearGroup { get; set; }
        public bool students { get; set; }
        /// <summary>Known only as a parent of something else — real, but with no account filed directly in it.</summary>
        public bool derived { get; set; }
    }

    /// <summary>The last segment of a path — the org unit's own name.</summary>
    private static string LeafOf(string path)
    {
        path = path ?? "";
        int i = path.LastIndexOf('/');
        return i < 0 ? path : path.Substring(i + 1);
    }

    /// <summary>Does this org unit name carry both halves of the <paramref name="year"/> academic year?</summary>
    /// <remarks>
    /// MRU's year org units were spelled by hand and no two generations agree: "2026 - 2027",
    /// "2025 -2026", "2019-2020", "2022-2023 student", "2024-2025 Students". A template that
    /// BUILDS the name is wrong for most of them, so the name is always matched, never made.
    /// </remarks>
    private static bool IsYearGroupFor(string leaf, int year)
    {
        if (string.IsNullOrEmpty(leaf)) return false;
        string a = year.ToString(CultureInfo.InvariantCulture);
        string b = (year + 1).ToString(CultureInfo.InvariantCulture);
        int i = leaf.IndexOf(a, StringComparison.Ordinal);
        return i >= 0 && leaf.IndexOf(b, i + a.Length, StringComparison.Ordinal) > 0;
    }

    /// <summary>Any four-digit year in the name marks a cohort rather than a faculty.</summary>
    private static bool LooksLikeYearGroup(string leaf)
    {
        if (string.IsNullOrEmpty(leaf)) return false;
        for (int i = 0; i + 4 <= leaf.Length; i++)
        {
            if (leaf[i] != '1' && leaf[i] != '2') continue;
            if (char.IsDigit(leaf[i + 1]) && char.IsDigit(leaf[i + 2]) && char.IsDigit(leaf[i + 3])) return true;
        }
        return false;
    }

    /// <summary>The intake the caller named, else the one the waiting students actually belong to.</summary>
    private static int IntakeYear(MySqlConnection c, string year)
    {
        int n;
        if (int.TryParse((year ?? "").Trim(), out n) && n >= 2000 && n <= 2100) return n;
        using (var q = new MySqlCommand(
            "SELECT admission_year FROM campus_dynamics_portal.sems_email_creations " +
            "WHERE current_stage='PENDING_CREATION' AND IFNULL(admission_year,0)>0 " +
            "GROUP BY admission_year ORDER BY COUNT(*) DESC LIMIT 1", c))
        {
            var v = q.ExecuteScalar();
            if (v != null && v != DBNull.Value && int.TryParse(v.ToString(), out n)) return n;
        }
        // An academic year starts in August, so before then "this intake" is still last year's.
        return DateTime.Now.Month >= 8 ? DateTime.Now.Year : DateTime.Now.Year - 1;
    }

    /// <summary>
    /// Every org unit path Google is known to hold, with the number of accounts filed directly
    /// in each (0 for one known only as somebody's parent).
    ///
    /// Two sources, both evidence rather than guesswork: the accounts Google confirmed for our
    /// own students, and the org unit column of any Google directory export that has been
    /// imported — that second one is Google's own view of the whole tree, irregular names and
    /// all. Every ancestor is added too, because Google cannot hold ".../2019-2020/Faculty of
    /// Education/..." unless each step of it exists; without that the cohort org units are
    /// invisible whenever nobody is filed directly in them.
    /// </summary>
    private static Dictionary<string, int> DiscoverOrgUnits(MySqlConnection c)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Action<string, int> add = (raw, n) =>
        {
            string ou = NormaliseOrgUnit(raw);
            if (ou == "/") return;                       // the root is always offered separately
            int cur;
            counts[ou] = counts.TryGetValue(ou, out cur) ? cur + n : n;
        };

        using (var cmd = new MySqlCommand(
            "SELECT google_org_unit ou, COUNT(*) n FROM campus_dynamics_portal.sems_email_creations " +
            "WHERE google_status IN ('IN_GOOGLE','SUSPENDED') AND IFNULL(google_org_unit,'')<>'' GROUP BY 1", c))
        {
            cmd.CommandTimeout = 90;
            using (var rd = cmd.ExecuteReader()) while (rd.Read()) add(S(rd["ou"]), Convert.ToInt32(rd["n"]));
        }

        using (var cmd = new MySqlCommand(
            "SELECT org_unit ou, COUNT(*) n FROM campus_dynamics_portal.sems_import_staging " +
            "WHERE IFNULL(org_unit,'')<>'' GROUP BY 1 ORDER BY n DESC LIMIT 800", c))
        {
            cmd.CommandTimeout = 90;
            using (var rd = cmd.ExecuteReader()) while (rd.Read()) add(S(rd["ou"]), Convert.ToInt32(rd["n"]));
        }

        foreach (var known in new List<string>(counts.Keys))
        {
            int cut = known.LastIndexOf('/');
            while (cut > 0)
            {
                string parent = known.Substring(0, cut);
                if (counts.ContainsKey(parent)) break;   // and so is everything above it
                counts[parent] = 0;
                cut = parent.LastIndexOf('/');
            }
        }
        return counts;
    }

    /// <summary>
    /// The org unit an intake belongs in: the shallowest student cohort whose name carries both
    /// halves of the academic year. "" when Google holds no such org unit.
    /// </summary>
    private static string CohortOrgUnit(Dictionary<string, int> known, int intake)
    {
        string best = "";
        int bestDepth = int.MaxValue, bestAccounts = -1;
        foreach (var kv in known)
        {
            if (!kv.Key.StartsWith("/Students", StringComparison.OrdinalIgnoreCase)) continue;
            if (!IsYearGroupFor(LeafOf(kv.Key), intake)) continue;
            int depth = kv.Key.Split('/').Length;
            if (depth < bestDepth || (depth == bestDepth && kv.Value > bestAccounts))
            { best = kv.Key; bestDepth = depth; bestAccounts = kv.Value; }
        }
        return best;
    }

    /// <summary>
    /// Every intake in the pipeline mapped to the org unit it belongs in. Built once per sheet:
    /// the Google CSV carries an org unit PER ROW, so a batch spanning four admission years can
    /// file each student in their own cohort instead of dropping all of them in one place.
    /// </summary>
    private static Dictionary<int, string> IntakeOrgUnits(MySqlConnection c, out Dictionary<string, int> known)
    {
        known = DiscoverOrgUnits(c);
        var map = new Dictionary<int, string>();
        using (var cmd = new MySqlCommand(
            "SELECT DISTINCT admission_year FROM campus_dynamics_portal.sems_email_creations " +
            "WHERE IFNULL(admission_year,0) BETWEEN 2000 AND 2100", c))
        {
            cmd.CommandTimeout = 60;
            using (var rd = cmd.ExecuteReader())
                while (rd.Read())
                {
                    int y = Convert.ToInt32(rd[0]);
                    string ou = CohortOrgUnit(known, y);
                    if (ou != "") map[y] = ou;
                }
        }
        return map;
    }

    /// <summary>
    /// Every org unit path Google is known to hold, and which one this intake belongs in.
    ///
    /// Two sources, both evidence rather than guesswork: the accounts Google confirmed for our
    /// own students, and the org unit column of any Google directory export that has been
    /// imported — that second one is Google's own view of the whole tree, irregular names and
    /// all. The export screen offers these instead of a free-text box, because a path that does
    /// not resolve fails EVERY row of an upload with OU_INVALID, and an upload never creates one.
    /// </summary>
    public static string OrgUnits(string year)
    {
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                int intake = IntakeYear(c, year);

                Dictionary<string, int> counts;
                var byIntake = IntakeOrgUnits(c, out counts);

                var list = new List<OrgUnitRow>();
                foreach (var kv in counts)
                    list.Add(new OrgUnitRow
                    {
                        path = kv.Key,
                        accounts = kv.Value,
                        derived = kv.Value == 0,
                        yearGroup = LooksLikeYearGroup(LeafOf(kv.Key)),
                        students = kv.Key.StartsWith("/Students", StringComparison.OrdinalIgnoreCase)
                    });
                list.Sort((x, y) => string.Compare(x.path, y.path, StringComparison.OrdinalIgnoreCase));

                string suggested = CohortOrgUnit(counts, intake);
                int suggestedAccounts;
                if (!counts.TryGetValue(suggested ?? "", out suggestedAccounts)) suggestedAccounts = 0;

                // What "file each student in their own intake" would actually do, per year, so
                // the screen can show it before the sheet is built rather than after Google says no.
                var plan = new List<object>();
                using (var cmd = new MySqlCommand(
                    "SELECT admission_year yr, COUNT(*) n FROM campus_dynamics_portal.sems_email_creations " +
                    "WHERE current_stage='PENDING_CREATION' AND IFNULL(admission_year,0)>0 " +
                    "GROUP BY 1 ORDER BY 1 DESC", c))
                {
                    cmd.CommandTimeout = 60;
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read())
                        {
                            int y = Convert.ToInt32(rd["yr"]);
                            string ou;
                            plan.Add(new
                            {
                                year = y,
                                students = Convert.ToInt32(rd["n"]),
                                path = byIntake.TryGetValue(y, out ou) ? ou : ""
                            });
                        }
                }

                return Js().Serialize(new
                {
                    success = true,
                    intake,
                    suggested,
                    suggestedAccounts,
                    intakePlan = plan,
                    orgUnits = list
                });
            }
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }

    /// <summary>How many rows each export mode would produce — shown before the download.</summary>
    public static string ExportCount(string scopeJson)
    {
        try
        {
            var sc = ReadScope(scopeJson);
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                // Counted with the same domain rule the sheet uses, so the number on screen is
                // the number of rows that come out.
                Func<string, int> cnt = extra =>
                {
                    using (var cmd = new MySqlCommand(
                        "SELECT COUNT(*) FROM campus_dynamics_portal.sems_email_creations p WHERE IFNULL(p.email_address,'')<>'' " +
                        "AND LOWER(TRIM(p.email_address)) LIKE '%@" + DefaultDomain + "' " + extra, c))
                    {
                        cmd.CommandTimeout = 120;
                        var v = cmd.ExecuteScalar(); return v == null || v == DBNull.Value ? 0 : Convert.ToInt32(v);
                    }
                };
                // "pending" is the population this whole module exists for: eligible students
                // who were generated into the pipeline and are still waiting for an address.
                // They CANNOT be exported — there is nothing to put in the Email column yet —
                // so the count is returned to say so plainly instead of handing back an empty
                // sheet and letting the admin guess why.
                int pending;
                using (var cmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM campus_dynamics_portal.sems_email_creations " +
                    "WHERE IFNULL(email_address,'')='' AND current_stage='PENDING_CREATION'", c))
                { cmd.CommandTimeout = 120; pending = Convert.ToInt32(cmd.ExecuteScalar()); }

                return Js().Serialize(new
                {
                    success = true,
                    pending,
                    // Allocated and in a sheet, but Google has not confirmed them yet.
                    awaiting = cnt("AND p.google_status IN ('NOT_CREATED','PROPOSED','EXPORTED')"),
                    create = cnt("AND p.google_status IN ('NOT_CREATED','PROPOSED','EXPORTED')"),
                    update = cnt("AND p.google_status IN ('IN_GOOGLE','SUSPENDED')"),
                    all = cnt("")
                });
            }
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }

    // =================================================================
    //  IMPORT — parse + classify into staging
    // =================================================================
    private static string Col(Dictionary<string, int> map, string[] cells, string key)
    {
        int i;
        if (!map.TryGetValue(key, out i) || i < 0 || i >= cells.Length) return "";
        return (cells[i] ?? "").Trim();
    }

    /// <summary>
    /// Parses an uploaded sheet into staging and decides what each row means. Writes nothing
    /// to student records — the admin sees the classification first.
    /// </summary>
    /// <summary>
    /// Google's bulk-upload RESULT log — "2:dineabd26@mru.ac.ug:ACTION_SUCCEEDED" or
    /// "…:ACTION_FAILED:INSUFFICIENT_LICENSES". It has no header and is not a CSV, so it is
    /// recognised by shape before the CSV parser is reached.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex ResultLine =
        new System.Text.RegularExpressions.Regex(
            @"^\s*(\d+)\s*:\s*([^:\s]+@[^:\s]+)\s*:\s*ACTION_(SUCCEEDED|FAILED)\s*(?::\s*(.*))?$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static bool LooksLikeResultLog(string text)
    {
        int looked = 0, matched = 0;
        foreach (string raw in text.Split('\n'))
        {
            string line = (raw ?? "").Trim();
            if (line.Length == 0) continue;
            looked++;
            if (ResultLine.IsMatch(line)) matched++;
            if (looked >= 20) break;
        }
        return looked > 0 && matched > 0 && matched * 2 >= looked;
    }

    /// <summary>
    /// Applies the meaning of an upload result: SUCCEEDED means the mailbox now exists, which
    /// is the only evidence that lets an address become the student's. FAILED rows are recorded
    /// with Google's reason and touch nothing — a student whose account hit the licence ceiling
    /// must stay exactly where they were, ready for the next attempt.
    /// </summary>
    private static string ImportResultLog(string fileName, string text)
    {
        string importRef = "SIM" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" +
                           Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();
        int confirm = 0, orphan = 0, failed = 0, skip = 0;
        var reasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var c = new MySqlConnection(Conn))
        {
            c.Open();
            SyncDirectory(c, DefaultDomain);

            foreach (string raw in text.Split('\n'))
            {
                string line = (raw ?? "").Trim();
                if (line.Length == 0) continue;
                var m = ResultLine.Match(line);
                if (!m.Success) continue;

                int rowNo; int.TryParse(m.Groups[1].Value, out rowNo);
                string email = m.Groups[2].Value.Trim().ToLowerInvariant();
                bool ok = m.Groups[3].Value.Equals("SUCCEEDED", StringComparison.OrdinalIgnoreCase);
                string reason = (m.Groups[4].Value ?? "").Trim();

                string action, severity, message, matchRegno = "", matchType = "NONE";

                if (!seen.Add(email))
                { action = "SKIP"; severity = "WARN"; message = "Repeated in the file."; skip++; }
                else if (ok)
                {
                    using (var q = new MySqlCommand(
                        "SELECT regno FROM campus_dynamics_portal.sems_email_creations WHERE email_address=@e LIMIT 1", c))
                    { q.Parameters.AddWithValue("@e", email); matchRegno = S(q.ExecuteScalar()); }

                    if (matchRegno != "")
                    { matchType = "EMAIL"; action = "CONFIRM"; severity = "OK"; message = "Created in Google."; confirm++; }
                    else
                    { action = "ORPHAN"; severity = "WARN"; message = "Created in Google but no student holds this address."; orphan++; }
                }
                else
                {
                    action = "FAILED"; severity = "ERROR";
                    message = "Google refused this account" + (reason == "" ? "." : ": " + reason);
                    failed++;
                    string key = reason == "" ? "(no reason given)" : reason;
                    int n; reasons[key] = reasons.TryGetValue(key, out n) ? n + 1 : 1;

                    using (var q = new MySqlCommand(
                        "SELECT regno FROM campus_dynamics_portal.sems_email_creations WHERE email_address=@e LIMIT 1", c))
                    { q.Parameters.AddWithValue("@e", email); matchRegno = S(q.ExecuteScalar()); if (matchRegno != "") matchType = "EMAIL"; }
                }

                using (var ins = new MySqlCommand(
                    "INSERT INTO campus_dynamics_portal.sems_import_staging " +
                    "(import_ref,row_no,email,google_status,raw_json,match_regno,match_type,action,severity,message,created_at) " +
                    "VALUES (@ref,@no,@em,@st,@raw,@mr,@mt,@ac,@sv,@msg,NOW())", c))
                {
                    ins.Parameters.AddWithValue("@ref", importRef);
                    ins.Parameters.AddWithValue("@no", rowNo);
                    ins.Parameters.AddWithValue("@em", Trunc(email, 150));
                    ins.Parameters.AddWithValue("@st", Trunc(ok ? "SUCCEEDED" : "FAILED", 35));
                    ins.Parameters.AddWithValue("@raw", Trunc(line, 4000));
                    ins.Parameters.AddWithValue("@mr", Trunc(matchRegno, 40));
                    ins.Parameters.AddWithValue("@mt", matchType);
                    ins.Parameters.AddWithValue("@ac", action);
                    ins.Parameters.AddWithValue("@sv", severity);
                    ins.Parameters.AddWithValue("@msg", Trunc(message, 250));
                    ins.ExecuteNonQuery();
                }
            }

            int total = confirm + orphan + failed + skip;
            using (var cmd = new MySqlCommand(
                "INSERT INTO campus_dynamics_portal.sems_email_batches " +
                "(batch_ref,batch_type,status,params_json,total_rows,created_by,created_at,notes) " +
                "VALUES (@r,'IMPORT','DRAFT',@p,@t,@who,NOW(),@n)", c))
            {
                cmd.Parameters.AddWithValue("@r", importRef);
                cmd.Parameters.AddWithValue("@p", Js().Serialize(new { fileName, format = "google-result-log" }));
                cmd.Parameters.AddWithValue("@t", total);
                cmd.Parameters.AddWithValue("@who", Actor());
                cmd.Parameters.AddWithValue("@n", Trunc("upload result " + (fileName ?? "log"), 250));
                cmd.ExecuteNonQuery();
            }

            var reasonList = new List<object>();
            foreach (var kv in reasons) reasonList.Add(new { reason = kv.Key, count = kv.Value });

            return Js().Serialize(new
            {
                success = true,
                importRef,
                fileName,
                format = "Google upload result",
                total,
                confirm,
                adopt = 0,
                change = 0,
                suspend = 0,
                orphan,
                error = failed,
                failed,
                skip,
                reasons = reasonList
            });
        }
    }

    public static string ImportParse(string fileName, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Fail("The file is empty.");

        // The result Google hands back after an upload is not a CSV and has no header.
        if (LooksLikeResultLog(text)) return ImportResultLog(fileName, text);

        char delim;
        var rows = ParseDelimited(text, out delim);
        if (rows.Count < 2) return Fail("The file has no data rows.");

        // Map by header NAME, so a sheet with columns in a different order — or with extra
        // columns Google added — still imports correctly.
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var header = rows[0];
        for (int i = 0; i < header.Length; i++)
        {
            string k = HeaderKey(header[i]);
            if (k.Length > 0 && !map.ContainsKey(k)) map[k] = i;
        }
        if (!map.ContainsKey("emailaddress") && !map.ContainsKey("email"))
            return Fail("No \"Email Address\" column found. Use the Google Workspace template — download a blank one from this page if needed.");
        if (map.ContainsKey("email") && !map.ContainsKey("emailaddress")) map["emailaddress"] = map["email"];

        string importRef = "SIM" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" +
                           Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();

        int confirm = 0, adopt = 0, change = 0, suspend = 0, orphan = 0, skip = 0, error = 0;
        var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                SyncDirectory(c, DefaultDomain);

                for (int r = 1; r < rows.Count && r <= HardBatchCap * 5; r++)
                {
                    var cells = rows[r];
                    if (cells.All(x => (x ?? "").Trim().Length == 0)) continue;

                    string email = Col(map, cells, "emailaddress").ToLowerInvariant();
                    string newPrimary = Col(map, cells, "newprimaryemail").ToLowerInvariant();
                    string empId = Col(map, cells, "employeeid");
                    string first = Col(map, cells, "firstname");
                    string last = Col(map, cells, "lastname");
                    string org = Col(map, cells, "orgunitpath");
                    string status = Col(map, cells, "newstatus");
                    if (status == "") status = Col(map, cells, "status");
                    string rec = Col(map, cells, "recoveryemail");
                    string phone = Col(map, cells, "recoveryphone");
                    string dept = Col(map, cells, "department");

                    string action, severity = "OK", message = "", matchRegno = "", matchType = "NONE";
                    string effective = newPrimary != "" ? newPrimary : email;

                    if (effective == "" || !IsValidEmail(effective))
                    {
                        action = "ERROR"; severity = "ERROR";
                        message = effective == "" ? "No email address in this row." : "Not a valid email address.";
                    }
                    else if (!seenInFile.Add(effective))
                    {
                        action = "ERROR"; severity = "ERROR";
                        message = "This address appears more than once in the file.";
                    }
                    else
                    {
                        // Match: Employee ID first (survives renames), then the address itself.
                        string pipeEmail = "", pipeStage = "";
                        if (empId != "")
                        {
                            using (var q = new MySqlCommand(
                                "SELECT regno, IFNULL(email_address,''), current_stage FROM campus_dynamics_portal.sems_email_creations WHERE regno=@r LIMIT 1", c))
                            {
                                q.Parameters.AddWithValue("@r", empId);
                                using (var rd = q.ExecuteReader())
                                    if (rd.Read()) { matchRegno = S(rd[0]); pipeEmail = S(rd[1]); pipeStage = S(rd[2]); matchType = "EMPLOYEE_ID"; }
                            }
                        }
                        if (matchRegno == "")
                        {
                            using (var q = new MySqlCommand(
                                "SELECT regno, IFNULL(email_address,''), current_stage FROM campus_dynamics_portal.sems_email_creations " +
                                "WHERE email_address=@e LIMIT 1", c))
                            {
                                q.Parameters.AddWithValue("@e", effective);
                                using (var rd = q.ExecuteReader())
                                    if (rd.Read()) { matchRegno = S(rd[0]); pipeEmail = S(rd[1]); pipeStage = S(rd[2]); matchType = "EMAIL"; }
                            }
                        }

                        bool suspended = status.IndexOf("suspend", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         status.IndexOf("archiv", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (matchRegno == "")
                        {
                            action = "ORPHAN"; severity = "WARN";
                            message = "No student in the pipeline for this account — it will be recorded as taken so it is never re-issued.";
                        }
                        else if (pipeEmail == "")
                        {
                            action = "ADOPT"; severity = "WARN";
                            message = "Student has no address on file — this Google account will be adopted onto their record.";
                        }
                        else if (!pipeEmail.Equals(effective, StringComparison.OrdinalIgnoreCase))
                        {
                            action = "UPDATE_EMAIL"; severity = "WARN";
                            message = "Google has " + effective + " but the system has " + pipeEmail + ".";
                        }
                        else if (suspended)
                        {
                            action = "SUSPEND"; severity = "WARN";
                            message = "Account is suspended in Google.";
                        }
                        else
                        {
                            action = "CONFIRM";
                            message = "Confirmed live in Google.";
                        }
                    }

                    switch (action)
                    {
                        case "CONFIRM": confirm++; break;
                        case "ADOPT": adopt++; break;
                        case "UPDATE_EMAIL": change++; break;
                        case "SUSPEND": suspend++; break;
                        case "ORPHAN": orphan++; break;
                        case "ERROR": error++; break;
                        default: skip++; break;
                    }

                    using (var ins = new MySqlCommand(
                        "INSERT INTO campus_dynamics_portal.sems_import_staging " +
                        "(import_ref,row_no,first_name,last_name,email,new_primary_email,employee_id,org_unit,google_status," +
                        " recovery_email,recovery_phone,department,raw_json,match_regno,match_type,action,severity,message,created_at) " +
                        "VALUES (@ref,@no,@fn,@ln,@em,@np,@eid,@ou,@st,@rc,@ph,@dp,@raw,@mr,@mt,@ac,@sv,@msg,NOW())", c))
                    {
                        ins.Parameters.AddWithValue("@ref", importRef);
                        ins.Parameters.AddWithValue("@no", r);
                        ins.Parameters.AddWithValue("@fn", Trunc(first, 110));
                        ins.Parameters.AddWithValue("@ln", Trunc(last, 110));
                        ins.Parameters.AddWithValue("@em", Trunc(email, 150));
                        ins.Parameters.AddWithValue("@np", Trunc(newPrimary, 150));
                        ins.Parameters.AddWithValue("@eid", Trunc(empId, 50));
                        ins.Parameters.AddWithValue("@ou", Trunc(org, 150));
                        ins.Parameters.AddWithValue("@st", Trunc(status, 35));
                        ins.Parameters.AddWithValue("@rc", Trunc(rec, 150));
                        ins.Parameters.AddWithValue("@ph", Trunc(phone, 35));
                        ins.Parameters.AddWithValue("@dp", Trunc(dept, 110));
                        ins.Parameters.AddWithValue("@raw", Trunc(string.Join(" | ", cells.Take(28).ToArray()), 4000));
                        ins.Parameters.AddWithValue("@mr", Trunc(matchRegno, 40));
                        ins.Parameters.AddWithValue("@mt", matchType);
                        ins.Parameters.AddWithValue("@ac", action);
                        ins.Parameters.AddWithValue("@sv", severity);
                        ins.Parameters.AddWithValue("@msg", Trunc(message, 250));
                        ins.ExecuteNonQuery();
                    }
                }

                using (var cmd = new MySqlCommand(
                    "INSERT INTO campus_dynamics_portal.sems_email_batches " +
                    "(batch_ref,batch_type,status,params_json,total_rows,created_by,created_at,notes) " +
                    "VALUES (@r,'IMPORT','DRAFT',@p,@t,@who,NOW(),@n)", c))
                {
                    cmd.Parameters.AddWithValue("@r", importRef);
                    cmd.Parameters.AddWithValue("@p", Js().Serialize(new { fileName, delimiter = delim == '\t' ? "tab" : delim.ToString(), columns = header.Length }));
                    cmd.Parameters.AddWithValue("@t", confirm + adopt + change + suspend + orphan + skip + error);
                    cmd.Parameters.AddWithValue("@who", Actor());
                    cmd.Parameters.AddWithValue("@n", Trunc("uploaded " + (fileName ?? "sheet"), 250));
                    cmd.ExecuteNonQuery();
                }

                return Js().Serialize(new
                {
                    success = true,
                    importRef,
                    fileName,
                    columns = header.Length,
                    delimiter = delim == '\t' ? "tab" : delim.ToString(),
                    total = confirm + adopt + change + suspend + orphan + skip + error,
                    confirm, adopt, change, suspend, orphan, error, skip
                });
            }
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }

    /// <summary>Paged view of a parsed import, optionally filtered to one class of row.</summary>
    public static string ImportRows(string importRef, string action, int page, int pageSize)
    {
        importRef = (importRef ?? "").Trim();
        if (pageSize < 1 || pageSize > 500) pageSize = 50;
        if (page < 1) page = 1;
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                string w = "WHERE import_ref=@r" + (string.IsNullOrWhiteSpace(action) ? "" : " AND action=@a");
                int total;
                using (var q = new MySqlCommand("SELECT COUNT(*) FROM campus_dynamics_portal.sems_import_staging " + w, c))
                {
                    q.Parameters.AddWithValue("@r", importRef);
                    if (!string.IsNullOrWhiteSpace(action)) q.Parameters.AddWithValue("@a", action);
                    total = Convert.ToInt32(q.ExecuteScalar());
                }
                var rows = new List<object>();
                using (var q = new MySqlCommand(
                    "SELECT row_no, IFNULL(first_name,'') fn, IFNULL(last_name,'') ln, IFNULL(email,'') em, " +
                    "IFNULL(new_primary_email,'') np, IFNULL(employee_id,'') eid, IFNULL(org_unit,'') ou, " +
                    "IFNULL(google_status,'') gs, IFNULL(match_regno,'') mr, IFNULL(match_type,'') mt, " +
                    "IFNULL(action,'') ac, IFNULL(severity,'') sv, IFNULL(message,'') msg, applied " +
                    "FROM campus_dynamics_portal.sems_import_staging " + w + " ORDER BY row_no LIMIT @off,@ps", c))
                {
                    q.Parameters.AddWithValue("@r", importRef);
                    if (!string.IsNullOrWhiteSpace(action)) q.Parameters.AddWithValue("@a", action);
                    q.Parameters.AddWithValue("@off", (page - 1) * pageSize);
                    q.Parameters.AddWithValue("@ps", pageSize);
                    using (var rd = q.ExecuteReader())
                        while (rd.Read())
                            rows.Add(new
                            {
                                rowNo = Convert.ToInt32(rd["row_no"]), first = S(rd["fn"]), last = S(rd["ln"]),
                                email = S(rd["em"]), newPrimary = S(rd["np"]), employeeId = S(rd["eid"]),
                                orgUnit = S(rd["ou"]), status = S(rd["gs"]), regno = S(rd["mr"]), matchType = S(rd["mt"]),
                                action = S(rd["ac"]), severity = S(rd["sv"]), message = S(rd["msg"]),
                                applied = Convert.ToInt32(rd["applied"]) == 1
                            });
                }
                return Js().Serialize(new
                {
                    success = true, total, page, pageSize,
                    pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)), rows
                });
            }
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }

    // =================================================================
    //  IMPORT — apply the classes the admin ticked
    // =================================================================
    public static string ImportApply(string importRef, string optionsJson)
    {
        importRef = (importRef ?? "").Trim();
        if (importRef == "") return Fail("Missing import reference.");

        bool doConfirm = true, doAdopt = true, doChange = false, doSuspend = true, doOrphan = true;
        try
        {
            var d = Js().Deserialize<Dictionary<string, object>>(optionsJson ?? "{}");
            doConfirm = GetB(d, "confirm", true);
            doAdopt = GetB(d, "adopt", true);
            doChange = GetB(d, "changeEmail", false);
            doSuspend = GetB(d, "suspend", true);
            doOrphan = GetB(d, "orphan", true);
        }
        catch { }

        int confirmed = 0, adopted = 0, changed = 0, suspended = 0, orphans = 0, failed = 0;
        var failures = new List<object>();

        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();

                var wanted = new List<string>();
                if (doConfirm) wanted.Add("'CONFIRM'");
                if (doAdopt) wanted.Add("'ADOPT'");
                if (doChange) wanted.Add("'UPDATE_EMAIL'");
                if (doSuspend) wanted.Add("'SUSPEND'");
                if (doOrphan) wanted.Add("'ORPHAN'");
                if (wanted.Count == 0) return Fail("Nothing selected to apply.");

                var rows = new List<string[]>();
                using (var q = new MySqlCommand(
                    "SELECT id, IFNULL(match_regno,''), IFNULL(email,''), IFNULL(new_primary_email,''), IFNULL(org_unit,''), " +
                    "IFNULL(action,''), IFNULL(first_name,''), IFNULL(last_name,''), IFNULL(recovery_email,''), IFNULL(recovery_phone,'') " +
                    "FROM campus_dynamics_portal.sems_import_staging " +
                    "WHERE import_ref=@r AND applied=0 AND action IN (" + string.Join(",", wanted.ToArray()) + ") ORDER BY row_no", c))
                {
                    q.Parameters.AddWithValue("@r", importRef);
                    using (var rd = q.ExecuteReader())
                        while (rd.Read())
                            rows.Add(new[] { S(rd[0]), S(rd[1]), S(rd[2]), S(rd[3]), S(rd[4]), S(rd[5]), S(rd[6]), S(rd[7]), S(rd[8]), S(rd[9]) });
                }

                foreach (var r in rows)
                {
                    string id = r[0], regno = r[1], email = r[2], newPrimary = r[3], org = r[4], action = r[5];
                    string name = (r[6] + " " + r[7]).Trim(), rec = r[8], phone = r[9];
                    string effective = newPrimary != "" ? newPrimary : email;

                    try
                    {
                        using (var tx = c.BeginTransaction())
                        {
                            if (action == "ORPHAN")
                            {
                                UpsertDirectory(c, tx, effective, "GOOGLE", null, null, name, "ACTIVE",
                                                "seen in Google import " + importRef);
                                orphans++;
                            }
                            else if (action == "CONFIRM" || action == "SUSPEND")
                            {
                                // Confirmation is the moment the address becomes the student's.
                                // Everything before this was a proposal on paper.
                                string wasStage = StageOf(c, tx, regno);
                                bool promote = (action == "CONFIRM" && wasStage == "PENDING_CREATION");

                                using (var up = new MySqlCommand(
                                    "UPDATE campus_dynamics_portal.sems_email_creations SET google_status=@gs, google_synced_at=NOW(), " +
                                    "google_last_seen_at=NOW(), google_org_unit=COALESCE(NULLIF(@ou,''),google_org_unit), " +
                                    (promote ? "current_status='READY', current_stage='READY_FOR_COLLECTION', email_created_at=COALESCE(email_created_at,NOW()), " : "") +
                                    "last_updated_by=@who, last_updated_at=NOW() WHERE regno=@r", c, tx))
                                {
                                    up.Parameters.AddWithValue("@gs", action == "SUSPEND" ? "SUSPENDED" : "IN_GOOGLE");
                                    up.Parameters.AddWithValue("@ou", org);
                                    up.Parameters.AddWithValue("@who", Actor());
                                    up.Parameters.AddWithValue("@r", regno);
                                    up.ExecuteNonQuery();
                                }
                                UpsertDirectory(c, tx, effective, "GOOGLE", "STUDENT", regno, name, "ACTIVE", "confirmed in Google " + importRef);
                                if (promote)
                                {
                                    LogTx(c, tx, 0, regno, "google_confirm_email", wasStage, "READY_FOR_COLLECTION",
                                          effective + " confirmed live (import " + importRef + ")");
                                    NotifyTx(c, tx, regno, "Your university email address is ready",
                                             "Open the portal to collect your @mru.ac.ug address and its password. It takes about five minutes.", "mail");
                                }
                                if (action == "SUSPEND") suspended++; else confirmed++;
                            }
                            else if (action == "ADOPT" || action == "UPDATE_EMAIL")
                            {
                                string wasStage = StageOf(c, tx, regno);
                                bool promote = (wasStage == "PENDING_CREATION");

                                // current_status is assigned BEFORE current_stage: MySQL applies
                                // assignments left to right, so testing the stage after changing
                                // it would always miss.
                                using (var up = new MySqlCommand(
                                    "UPDATE campus_dynamics_portal.sems_email_creations SET email_address=@e, google_status='IN_GOOGLE', " +
                                    "google_synced_at=NOW(), google_last_seen_at=NOW(), google_org_unit=COALESCE(NULLIF(@ou,''),google_org_unit), " +
                                    "recovery_email=COALESCE(NULLIF(@rc,''),recovery_email), recovery_phone=COALESCE(NULLIF(@ph,''),recovery_phone), " +
                                    (promote ? "current_status='READY', current_stage='READY_FOR_COLLECTION', " : "") +
                                    "email_created_at=COALESCE(email_created_at,NOW()), last_updated_by=@who, last_updated_at=NOW() " +
                                    "WHERE regno=@r", c, tx))
                                {
                                    up.Parameters.AddWithValue("@e", effective);
                                    up.Parameters.AddWithValue("@ou", org);
                                    up.Parameters.AddWithValue("@rc", rec);
                                    up.Parameters.AddWithValue("@ph", ToE164(phone));
                                    up.Parameters.AddWithValue("@who", Actor());
                                    up.Parameters.AddWithValue("@r", regno);
                                    up.ExecuteNonQuery();
                                }
                                UpsertDirectory(c, tx, effective, "GOOGLE", "STUDENT", regno, name, "ACTIVE", "adopted from Google " + importRef);
                                LogTx(c, tx, 0, regno, action == "ADOPT" ? "google_adopt_email" : "google_change_email", wasStage,
                                      promote ? "READY_FOR_COLLECTION" : wasStage, effective + " (import " + importRef + ")");
                                if (promote)
                                    NotifyTx(c, tx, regno, "Your university email address is ready",
                                             "Open the portal to collect your @mru.ac.ug address and its password. It takes about five minutes.", "mail");
                                if (action == "ADOPT") adopted++; else changed++;
                            }

                            using (var up = new MySqlCommand(
                                "UPDATE campus_dynamics_portal.sems_import_staging SET applied=1, message=CONCAT(IFNULL(message,''),' — applied') WHERE id=@id", c, tx))
                            { up.Parameters.AddWithValue("@id", id); up.ExecuteNonQuery(); }

                            tx.Commit();
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        failures.Add(new { regno, email = effective, message = ex.Message });
                        try
                        {
                            using (var up = new MySqlCommand(
                                "UPDATE campus_dynamics_portal.sems_import_staging SET severity='ERROR', message=@m WHERE id=@id", c))
                            { up.Parameters.AddWithValue("@m", Trunc("apply failed: " + ex.Message, 250)); up.Parameters.AddWithValue("@id", id); up.ExecuteNonQuery(); }
                        }
                        catch { }
                    }
                }

                using (var up = new MySqlCommand(
                    "UPDATE campus_dynamics_portal.sems_email_batches SET status=@s, ok_rows=@o, failed_rows=@f, completed_at=NOW() WHERE batch_ref=@r", c))
                {
                    up.Parameters.AddWithValue("@s", failed == 0 ? "APPLIED" : "PARTIAL");
                    up.Parameters.AddWithValue("@o", confirmed + adopted + changed + suspended + orphans);
                    up.Parameters.AddWithValue("@f", failed);
                    up.Parameters.AddWithValue("@r", importRef);
                    up.ExecuteNonQuery();
                }

                return Js().Serialize(new
                {
                    success = true, importRef, confirmed, adopted, changed, suspended, orphans, failed, failures,
                    message = "Applied: " + confirmed + " confirmed, " + adopted + " adopted, " + changed + " address change(s), " +
                              suspended + " suspended, " + orphans + " external account(s) recorded" +
                              (failed > 0 ? ", " + failed + " failed" : "") + "."
                });
            }
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }

    private static string StageOf(MySqlConnection c, MySqlTransaction tx, string regno)
    {
        using (var q = new MySqlCommand(
            "SELECT current_stage FROM campus_dynamics_portal.sems_email_creations WHERE regno=@r LIMIT 1", c, tx))
        { q.Parameters.AddWithValue("@r", regno); return S(q.ExecuteScalar()); }
    }

    private static void UpsertDirectory(MySqlConnection c, MySqlTransaction tx, string email, string source,
                                        string ownerType, string ownerRef, string name, string status, string note)
    {
        email = (email ?? "").Trim().ToLowerInvariant();
        if (email.IndexOf('@') <= 0) return;
        string local = email.Substring(0, email.IndexOf('@'));
        string dom = email.Substring(email.IndexOf('@') + 1);
        using (var cmd = new MySqlCommand(
            "INSERT INTO campus_dynamics_portal.sems_email_directory " +
            "(email,local_part,domain,source,owner_type,owner_ref,display_name,status,first_seen_at,last_seen_at,notes) " +
            "VALUES (@e,@l,@d,@s,@ot,@o,@n,@st,NOW(),NOW(),@nt) " +
            "ON DUPLICATE KEY UPDATE last_seen_at=NOW(), status=@st, " +
            "  owner_ref=COALESCE(NULLIF(@o,''),owner_ref), display_name=COALESCE(NULLIF(@n,''),display_name), notes=@nt", c, tx))
        {
            cmd.Parameters.AddWithValue("@e", email);
            cmd.Parameters.AddWithValue("@l", local);
            cmd.Parameters.AddWithValue("@d", dom);
            cmd.Parameters.AddWithValue("@s", source);
            cmd.Parameters.AddWithValue("@ot", N(ownerType));
            cmd.Parameters.AddWithValue("@o", ownerRef ?? "");
            cmd.Parameters.AddWithValue("@n", Trunc(name ?? "", 150));
            cmd.Parameters.AddWithValue("@st", status);
            cmd.Parameters.AddWithValue("@nt", Trunc(note ?? "", 250));
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Throws away a parsed import that the admin decided not to apply.</summary>
    public static string ImportDiscard(string importRef)
    {
        importRef = (importRef ?? "").Trim();
        try
        {
            using (var c = new MySqlConnection(Conn))
            {
                c.Open();
                using (var d = new MySqlCommand("DELETE FROM campus_dynamics_portal.sems_import_staging WHERE import_ref=@r AND applied=0", c))
                { d.Parameters.AddWithValue("@r", importRef); d.ExecuteNonQuery(); }
                using (var u = new MySqlCommand(
                    "UPDATE campus_dynamics_portal.sems_email_batches SET status='CANCELLED', completed_at=NOW() WHERE batch_ref=@r AND status='DRAFT'", c))
                { u.Parameters.AddWithValue("@r", importRef); u.ExecuteNonQuery(); }
                return Js().Serialize(new { success = true, message = "Import discarded." });
            }
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }
}
