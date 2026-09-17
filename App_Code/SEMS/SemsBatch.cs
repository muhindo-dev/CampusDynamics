using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

// =====================================================================
//  SEMS — Batch email creation + Google Workspace interchange.
//
//  Three jobs, one rule: an address is issued once and never again.
//
//   1. ALLOCATE  house format [surname][first N of other name][yy]@mru.ac.ug,
//                collision-resolved deterministically against every address
//                known on the domain (students, staff, pipeline, Google
//                imports, reserved names) — sems_email_directory.
//   2. EXPORT    the exact 28-column Google Workspace bulk-upload CSV.
//   3. IMPORT    a Google export back, matched on Employee ID (= student
//                number), classified row by row, applied only after review.
//
//  Nothing writes to live data without an approved server-side draft
//  (sems_email_batches + _items) or staging table (sems_import_staging),
//  so every change is reviewable before and auditable after.
// =====================================================================
public static partial class SemsBatch
{
    public const string DefaultDomain = "mru.ac.ug";
    private const int MaxLocalPart = 64;         // RFC / Google local-part ceiling
    private const int HardBatchCap = 2000;       // one batch may never exceed this
    private const int DraftExpiryHours = 24;     // stale drafts release their reservations

    private static string Conn
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    private static string Actor()
    {
        try { var u = HttpContext.Current.Session["username"]; return u == null ? "admin" : u.ToString(); }
        catch { return "admin"; }
    }

    private static string S(object v) { return v == null || v == DBNull.Value ? "" : v.ToString().Trim(); }
    private static object N(string v) { return string.IsNullOrEmpty(v) ? (object)DBNull.Value : v; }
    private static JavaScriptSerializer Js()
    {
        var js = new JavaScriptSerializer();
        js.MaxJsonLength = 64 * 1024 * 1024;     // a 2000-row preview is well within this
        return js;
    }
    private static string Fail(string msg) { return Js().Serialize(new { success = false, message = msg }); }

    // =================================================================
    //  1. NAME → ADDRESS
    // =================================================================

    /// <summary>
    /// Reduces a name to the letters an address may contain: accents folded,
    /// apostrophes/hyphens/spaces/digits dropped, lowercased. "O'Brien-Musisi" → "obrienmusisi".
    /// </summary>
    public static string Slug(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        string norm = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(norm.Length);
        foreach (char ch in norm)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            char c = char.ToLowerInvariant(ch);
            if (c >= 'a' && c <= 'z') sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>First whitespace-separated token of a name ("SANYU HELLEN" → "SANYU").</summary>
    private static string FirstToken(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var parts = s.Trim().Split(new[] { ' ', '\t', ',', '.' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : "";
    }

    private static string[] Tokens(string s)
    {
        if (string.IsNullOrEmpty(s)) return new string[0];
        return s.Trim().Split(new[] { ' ', '\t', ',', '.' }, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>Last two digits of the entry year ("2026" → "26").</summary>
    public static string Yy(object year)
    {
        string y = new string((S(year) ?? "").Where(char.IsDigit).ToArray());
        if (y.Length == 0) return "";
        return y.Length >= 2 ? y.Substring(y.Length - 2) : y.PadLeft(2, '0');
    }

    /// <summary>
    /// The house format: surname + the first <paramref name="otherLen"/> letters of the other
    /// name + the two-digit entry year. Falls back sensibly when a name part is missing —
    /// it never invents letters.
    /// </summary>
    public static string BuildLocal(string surname, string otherName, object year, int otherLen)
    {
        string sur = Slug(surname);
        string oth = Slug(FirstToken(otherName));
        string yy = Yy(year);
        if (otherLen < 1) otherLen = 1;
        if (oth.Length > otherLen) oth = oth.Substring(0, otherLen);
        string local = sur + oth + yy;
        if (local.Length > MaxLocalPart) local = local.Substring(0, MaxLocalPart);
        return local;
    }

    /// <summary>Google/RFC-safe local part: starts with a letter, letters+digits+dots only.</summary>
    public static bool IsValidLocal(string local)
    {
        if (string.IsNullOrEmpty(local) || local.Length > MaxLocalPart) return false;
        if (!char.IsLetter(local[0])) return false;
        if (local.EndsWith(".") || local.Contains("..")) return false;
        foreach (char c in local)
            if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.')) return false;
        return true;
    }

    public static bool IsValidEmail(string email)
    {
        email = (email ?? "").Trim().ToLowerInvariant();
        int at = email.IndexOf('@');
        if (at <= 0 || at == email.Length - 1) return false;
        if (email.IndexOf('@', at + 1) >= 0) return false;
        string dom = email.Substring(at + 1);
        return IsValidLocal(email.Substring(0, at)) && dom.Contains(".") && dom.Length <= 80;
    }

    // =================================================================
    //  2. DIRECTORY — the single oracle of "is this address taken?"
    // =================================================================

    /// <summary>
    /// Refreshes the directory from every place an @domain address can already live:
    /// student records, staff records and the SEMS pipeline itself. Upserts, so it is safe
    /// to call before every allocation — and it must be, or an address issued outside SEMS
    /// could be handed out twice.
    /// </summary>
    public static int SyncDirectory(MySqlConnection c, string domain)
    {
        domain = (domain ?? DefaultDomain).Trim().ToLowerInvariant();
        string like = "%@" + domain;
        int n = 0;

        // Students (any intake — a 2019 address still blocks the name today).
        using (var cmd = new MySqlCommand(
            "INSERT INTO campus_dynamics_portal.sems_email_directory " +
            " (email, local_part, domain, source, owner_type, owner_ref, display_name, status, first_seen_at, last_seen_at) " +
            "SELECT LOWER(TRIM(s.email)), SUBSTRING_INDEX(LOWER(TRIM(s.email)),'@',1), SUBSTRING_INDEX(LOWER(TRIM(s.email)),'@',-1), " +
            "       'STUDENT','STUDENT', TRIM(s.regno), NULLIF(TRIM(CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,''))),''), " +
            "       'ACTIVE', NOW(), NOW() " +
            "FROM campus_dynamics.acad_student s WHERE LOWER(TRIM(s.email)) LIKE @lk " +
            "ON DUPLICATE KEY UPDATE last_seen_at=NOW()", c))
        { cmd.CommandTimeout = 120; cmd.Parameters.AddWithValue("@lk", like); n += cmd.ExecuteNonQuery(); }

        // Staff — the same domain, so their addresses are equally unavailable.
        using (var cmd = new MySqlCommand(
            "INSERT INTO campus_dynamics_portal.sems_email_directory " +
            " (email, local_part, domain, source, owner_type, owner_ref, display_name, status, first_seen_at, last_seen_at) " +
            "SELECT LOWER(TRIM(e.emp_email)), SUBSTRING_INDEX(LOWER(TRIM(e.emp_email)),'@',1), SUBSTRING_INDEX(LOWER(TRIM(e.emp_email)),'@',-1), " +
            "       'STAFF','STAFF', TRIM(e.EMP_CODE), NULLIF(TRIM(e.emp_name),''), " +
            "       'ACTIVE', NOW(), NOW() " +
            "FROM campus_dynamics.hrm_employee e WHERE LOWER(TRIM(e.emp_email)) LIKE @lk " +
            "ON DUPLICATE KEY UPDATE last_seen_at=NOW()", c))
        { cmd.CommandTimeout = 120; cmd.Parameters.AddWithValue("@lk", like); n += cmd.ExecuteNonQuery(); }

        // The pipeline's own assignments.
        using (var cmd = new MySqlCommand(
            "INSERT INTO campus_dynamics_portal.sems_email_directory " +
            " (email, local_part, domain, source, owner_type, owner_ref, display_name, status, first_seen_at, last_seen_at) " +
            "SELECT LOWER(TRIM(p.email_address)), SUBSTRING_INDEX(LOWER(TRIM(p.email_address)),'@',1), SUBSTRING_INDEX(LOWER(TRIM(p.email_address)),'@',-1), " +
            "       'PIPELINE','STUDENT', p.regno, p.student_name, 'ACTIVE', NOW(), NOW() " +
            "FROM campus_dynamics_portal.sems_email_creations p WHERE IFNULL(TRIM(p.email_address),'') <> '' " +
            "ON DUPLICATE KEY UPDATE last_seen_at=NOW()", c))
        { cmd.CommandTimeout = 120; n += cmd.ExecuteNonQuery(); }

        return n;
    }

    /// <summary>
    /// Frees reservations left behind by drafts nobody committed — and closes those drafts.
    ///
    /// Releasing the address while leaving the draft open was a hole: the draft could still be
    /// committed a week later and would write an address the directory no longer knew was
    /// taken, so the same name could reach two students. A draft and its reservations now
    /// expire together.
    /// </summary>
    private static void ReleaseStaleReservations(MySqlConnection c)
    {
        using (var cmd = new MySqlCommand(
            "UPDATE campus_dynamics_portal.sems_email_batches SET status='EXPIRED', completed_at=NOW(), " +
            " notes=LEFT(CONCAT(IFNULL(notes,''),' — expired unapplied after ',@h,'h'),255) " +
            "WHERE batch_type='CREATE' AND status='DRAFT' AND created_at < DATE_SUB(NOW(), INTERVAL @h HOUR)", c))
        { cmd.Parameters.AddWithValue("@h", DraftExpiryHours); cmd.ExecuteNonQuery(); }

        using (var cmd = new MySqlCommand(
            "DELETE d FROM campus_dynamics_portal.sems_email_directory d " +
            "WHERE d.status='RESERVED' AND d.first_seen_at < DATE_SUB(NOW(), INTERVAL @h HOUR)", c))
        { cmd.Parameters.AddWithValue("@h", DraftExpiryHours); cmd.ExecuteNonQuery(); }
    }

    /// <summary>Every local part in use on the domain, for in-memory allocation.</summary>
    private static HashSet<string> LoadTaken(MySqlConnection c, string domain)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = new MySqlCommand(
            "SELECT local_part FROM campus_dynamics_portal.sems_email_directory WHERE domain=@d AND status<>'RELEASED'", c))
        {
            cmd.Parameters.AddWithValue("@d", domain);
            using (var rd = cmd.ExecuteReader()) while (rd.Read()) set.Add(S(rd[0]));
        }
        return set;
    }

    /// <summary>
    /// Deterministic collision resolution. In order: the house format, then a longer slice of
    /// the other name (readable), then an initial from a second given name, and only then a
    /// numeric suffix. Same inputs always give the same address.
    /// </summary>
    private static string Allocate(HashSet<string> taken, string surname, string otherName,
                                   object year, int otherLen, out string strategy)
    {
        strategy = "format";
        string sur = Slug(surname);
        string othFull = Slug(FirstToken(otherName));
        string yy = Yy(year);

        if (sur.Length == 0 && othFull.Length == 0) { strategy = "no-name"; return ""; }
        if (sur.Length == 0) { sur = othFull; othFull = ""; strategy = "surname-missing"; }

        var tried = new List<string>();
        Func<string, bool> ok = local =>
        {
            if (!IsValidLocal(local)) return false;
            tried.Add(local);
            return !taken.Contains(local);
        };

        // 1. the house format
        string baseLocal = BuildLocal(sur, othFull, year, otherLen);
        if (ok(baseLocal)) return baseLocal;

        // 2. more letters of the other name — still a real, readable name
        for (int len = otherLen + 1; len <= othFull.Length; len++)
        {
            string cand = sur + othFull.Substring(0, len) + yy;
            if (ok(cand)) { strategy = "extended"; return cand; }
        }

        // 3. an initial from a second given name (SANYU HELLEN → …sanh26)
        var toks = Tokens(otherName);
        for (int t = 1; t < toks.Length; t++)
        {
            string extra = Slug(toks[t]);
            if (extra.Length == 0) continue;
            for (int k = 1; k <= Math.Min(3, extra.Length); k++)
            {
                string cand = sur + (othFull.Length > otherLen ? othFull.Substring(0, otherLen) : othFull) + extra.Substring(0, k) + yy;
                if (ok(cand)) { strategy = "second-name"; return cand; }
            }
        }

        // 4. last resort — a counter, so the batch can never stall
        for (int i = 2; i <= 99; i++)
        {
            string cand = baseLocal + i.ToString(CultureInfo.InvariantCulture);
            if (cand.Length > MaxLocalPart) cand = cand.Substring(0, MaxLocalPart);
            if (ok(cand)) { strategy = "numbered"; return cand; }
        }

        strategy = "exhausted";
        return "";
    }

    // =================================================================
    //  3. PASSWORDS
    // =================================================================

    /// <summary>
    /// The house default every new student account is created with.
    ///
    /// One password for the whole intake is deliberate: it is the thing the onboarding guide,
    /// the notice board and the ICT desk can all say out loud, so a student who never receives
    /// a slip of paper can still sign in. It is safe to say out loud because the Google sheet
    /// sets "Change Password at Next Sign-In", so it survives exactly one login.
    ///
    /// Exactly 8 characters — Google's floor. It may not be shortened.
    /// </summary>
    public const string DefaultPassword = "mru12345";

    /// <summary>Google refuses to create an account with a password shorter than this.</summary>
    public const int MinPasswordLength = 8;

    private static readonly char[] PwLetters = "abcdefghjkmnpqrstuvwxyz".ToCharArray(); // no i/l/o
    private static readonly char[] PwDigits = "23456789".ToCharArray();                 // no 0/1

    /// <summary>
    /// A per-student password, for the rare batch an admin chooses to issue that way.
    /// Mru + letters + digits + a symbol: speakable over a counter, not guessable from the
    /// student's name. Not the default — <see cref="DefaultPassword"/> is.
    /// </summary>
    public static string RandomPassword()
    {
        var bytes = new byte[16];
        using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(bytes);
        var sb = new StringBuilder("Mru");
        for (int i = 0; i < 4; i++) sb.Append(PwLetters[bytes[i] % PwLetters.Length]);
        for (int i = 4; i < 7; i++) sb.Append(PwDigits[bytes[i] % PwDigits.Length]);
        sb.Append('#');
        return sb.ToString();                       // e.g. Mruqzkt472#  (11 chars)
    }

    /// <summary>
    /// Would Google accept this as the password for <paramref name="email"/>? A blank one, one
    /// under the floor, or the address used as its own password (a legacy import did exactly
    /// that) has to be replaced before the sheet leaves the building.
    /// </summary>
    public static bool IsUsablePassword(string pw, string email)
    {
        pw = (pw ?? "").Trim();
        if (pw.Length < MinPasswordLength) return false;
        email = (email ?? "").Trim();
        if (email.Length == 0) return true;
        if (pw.Equals(email, StringComparison.OrdinalIgnoreCase)) return false;
        int at = email.IndexOf('@');
        return !(at > 0 && pw.Equals(email.Substring(0, at), StringComparison.OrdinalIgnoreCase));
    }

    // =================================================================
    //  3b. ORG UNIT — the one shape Google will accept
    // =================================================================

    /// <summary>
    /// An org unit path Google can resolve: absolute, no doubled separators, no trailing
    /// slash. "students/" — which is what one export actually carried — becomes "/students",
    /// and "/Students/" becomes "/Students". A blank one is the root, which always exists.
    ///
    /// This has to be applied on the way INTO the sheet, not just on the way into our own
    /// records: an upload whose Org Unit Path does not resolve fails every single row with
    /// OU_INVALID, and Google will not create the org unit for you.
    /// </summary>
    public static string NormaliseOrgUnit(string path)
    {
        string t = (path ?? "").Trim();
        if (t.Length == 0) return "/";
        // Backslashes are separators only when the value has no real separator in it — someone
        // typing a Windows-style "Students\2026". A path that already has forward slashes is
        // left alone, because a genuine Google org unit name may CONTAIN a backslash:
        // "/Students/ALL MRU STUDENTS/2015 - 2016/.../BED\P - Bachelor of Education (Primary)"
        // is a real one, and rewriting it would invent an org unit that does not exist.
        if (t.IndexOf('/') < 0) t = t.Replace('\\', '/');
        if (!t.StartsWith("/")) t = "/" + t;
        while (t.Contains("//")) t = t.Replace("//", "/");
        if (t.Length > 1 && t.EndsWith("/")) t = t.Substring(0, t.Length - 1);
        return t.Length == 0 ? "/" : t;
    }

    // =================================================================
    //  4. PHONE — E.164 (Google rejects anything else)
    // =================================================================
    /// <summary>
    /// "0700979104/0703530404" → "+256700979104". Returns "" when the number cannot be
    /// trusted: a blank cell is accepted by Google, a malformed one fails the whole row.
    /// </summary>
    public static string ToE164(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        string first = raw.Split(new[] { '/', ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)[0];
        bool plus = first.TrimStart().StartsWith("+");
        string d = new string(first.Where(char.IsDigit).ToArray());
        if (d.Length == 0) return "";

        if (plus)                                   // already international
            return (d.Length >= 8 && d.Length <= 15) ? "+" + d : "";
        if (d.StartsWith("256") && d.Length == 12) return "+" + d;
        if (d.StartsWith("0") && d.Length == 10) return "+256" + d.Substring(1);
        if (d.Length == 9 && (d[0] == '7' || d[0] == '4' || d[0] == '3')) return "+256" + d;
        if (d.StartsWith("00")) { string t = d.Substring(2); return (t.Length >= 8 && t.Length <= 15) ? "+" + t : ""; }
        return "";                                  // unrecognisable — send nothing
    }

    // =================================================================
    //  5. CSV — writer and RFC-4180 reader
    // =================================================================
    public static string CsvCell(string v)
    {
        v = v ?? "";
        // Only '=' is escaped as a formula. NOT '+': every recovery phone is E.164 and starts
        // with one, and Google rejects "'+256…" outright — the sheet has to be right for the
        // machine that reads it, not for Excel's autocorrect.
        if (v.Length > 0 && v[0] == '=') v = "'" + v;
        if (v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }

    /// <summary>Splits CSV/TSV text into rows of cells; handles quotes, embedded newlines and BOM.</summary>
    public static List<string[]> ParseDelimited(string text, out char delimiter)
    {
        delimiter = ',';
        var rows = new List<string[]>();
        if (string.IsNullOrEmpty(text)) return rows;
        if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);

        // Delimiter sniffed from the header line: whichever separator appears most outside quotes.
        int nl = text.IndexOf('\n');
        string head = nl > 0 ? text.Substring(0, nl) : text;
        int commas = head.Count(ch => ch == ','), tabs = head.Count(ch => ch == '\t'), semis = head.Count(ch => ch == ';');
        if (tabs > commas && tabs >= semis) delimiter = '\t';
        else if (semis > commas && semis > tabs) delimiter = ';';

        var cells = new List<string>();
        var cur = new StringBuilder();
        bool inQ = false;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (inQ)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cur.Append('"'); i++; }
                    else inQ = false;
                }
                else cur.Append(ch);
            }
            else if (ch == '"') inQ = true;
            else if (ch == delimiter) { cells.Add(cur.ToString()); cur.Length = 0; }
            else if (ch == '\r') { /* handled with \n */ }
            else if (ch == '\n')
            {
                cells.Add(cur.ToString()); cur.Length = 0;
                if (cells.Count > 1 || cells[0].Trim().Length > 0) rows.Add(cells.ToArray());
                cells.Clear();
            }
            else cur.Append(ch);
        }
        if (cur.Length > 0 || cells.Count > 0)
        {
            cells.Add(cur.ToString());
            if (cells.Count > 1 || cells[0].Trim().Length > 0) rows.Add(cells.ToArray());
        }
        return rows;
    }

    // The Google Workspace bulk template, verbatim and in order — Google matches on these
    // exact header strings, so they are never reworded.
    public static readonly string[] GoogleHeaders = {
        "First Name [Required]",
        "Last Name [Required]",
        "Email Address [Required]",
        "Password [Required]",
        "Password Hash Function [UPLOAD ONLY]",
        "Org Unit Path [Required]",
        "New Primary Email [UPLOAD ONLY]",
        "Recovery Email",
        "Home Secondary Email",
        "Work Secondary Email",
        "Recovery Phone [MUST BE IN THE E.164 FORMAT]",
        "Work Phone",
        "Home Phone",
        "Mobile Phone",
        "Work Address",
        "Home Address",
        "Employee ID",
        "Employee Type",
        "Employee Title",
        "Manager Email",
        "Department",
        "Cost Center",
        "Building ID",
        "Floor Name",
        "Floor Section",
        "Change Password at Next Sign-In",
        "New Status [UPLOAD ONLY]",
        "Advanced Protection Program enrollment"
    };

    /// <summary>Header text → canonical key: "Recovery Phone [MUST BE…]" → "recoveryphone".</summary>
    public static string HeaderKey(string h)
    {
        if (h == null) return "";
        int b = h.IndexOf('[');
        if (b > 0) h = h.Substring(0, b);
        var sb = new StringBuilder();
        foreach (char c in h.ToLowerInvariant())
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        return sb.ToString();
    }
}
