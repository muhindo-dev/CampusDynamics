using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using MySql.Data.MySqlClient;

/// <summary>
/// ID Card module engine (Phase 0). Single source of truth for the request
/// lifecycle: create → transition (audited) → terminal. Every state change from
/// eportal, eadmin, or the XAXU API funnels through <see cref="Transition"/>, so
/// there is exactly one place that validates the state machine, stamps the
/// timeline, writes an audit event, and (later) triggers email.
///
/// Design doc: COOPERP/IDCARD_WORKFLOW_DESIGN.md.
/// </summary>
public static partial class IDCardService
{
    // ── connection to campus_dynamics ──
    // NOTE: this service is compiled into BOTH the eadmin app AND the eportal app, and the
    // named connection strings differ between them (e.g. the portal's "vacConnectionString"
    // points at campus_dynamics_portal, the WRONG db). Both apps run against the same local
    // MySQL, so we resolve a connection that truly targets campus_dynamics: an explicit
    // "IDCard.ConnStr" app-setting override wins; otherwise the hardcoded local connection.
    public static string ConnStr
    {
        get
        {
            var ov = ConfigurationManager.AppSettings["IDCard.ConnStr"];
            if (!string.IsNullOrEmpty(ov)) return ov;
            return "server=localhost;User Id=root;password=24thdecember1977;database=campus_dynamics;DefaultCommandTimeout=600;Convert Zero Datetime=True;charset=utf8";
        }
    }

    // campus_dynamics_accounts (name differs across apps too — resolve robustly)
    public static string AccountsConnStr
    {
        get
        {
            var ov = ConfigurationManager.AppSettings["IDCard.AccountsConnStr"];
            if (!string.IsNullOrEmpty(ov)) return ov;
            var a = ConfigurationManager.ConnectionStrings["accountsConnectionString"]
                 ?? ConfigurationManager.ConnectionStrings["campus_dynamics_accountsConnectionString"];
            if (a != null && a.ConnectionString.IndexOf("campus_dynamics_accounts", StringComparison.OrdinalIgnoreCase) >= 0)
                return a.ConnectionString;
            return "server=localhost;User Id=root;password=24thdecember1977;database=campus_dynamics_accounts;DefaultCommandTimeout=600;Convert Zero Datetime=True;charset=utf8";
        }
    }

    // Email hook — each host app wires this to its own EmailSenderProtocol in Application_Start,
    // because the two apps' SendHtmlEmail signatures differ. Signature: (toEmail, subject, htmlBody).
    // If unset, notifications are silently skipped (never breaks a transition).
    public static Action<string, string, string> Mailer;

    // ── statuses ──
    public const string REQUESTED     = "REQUESTED";
    public const string FINANCE_CHECK = "FINANCE_CHECK";
    public const string BLOCKED       = "BLOCKED";
    public const string SUBMITTED     = "SUBMITTED";
    public const string APPROVED      = "APPROVED";
    public const string HALTED        = "HALTED";
    public const string PRINTED       = "PRINTED";
    public const string READY         = "READY";
    public const string COLLECTED     = "COLLECTED";
    public const string CANCELLED     = "CANCELLED";

    // -- one human name per status ------------------------------------------
    //
    // The status strings above are the machine's vocabulary. Screens kept inventing
    // their own words for them, and the same state ended up with three names on two
    // pages: the ID-card console's summary tile said "Draft" while its own table and
    // its own filter said "REQUESTED", and the student's tracker said "Approved by
    // XAXU" where the console said "APPROVED". An operator who had just placed a
    // batch of requests went looking for them under "Submitted" and could not tell
    // whether they had failed or were simply being called something else.
    //
    // So the name lives here, once, beside the constant it names, and every screen
    // -- staff or student, C# or JavaScript -- asks for it rather than choosing.
    // StatusLabelsJson() exists so a page can hand the same table to its JavaScript
    // instead of hardcoding a second copy that will drift.
    //
    // REQUESTED is deliberately "Not submitted" rather than "Draft": it says what is
    // missing, which is the question people actually arrive with.
    private static readonly string[][] Labels =
    {
        new[] { REQUESTED,     "Not submitted"        },
        new[] { FINANCE_CHECK, "Fee check"            },
        new[] { BLOCKED,       "Blocked by fees"      },
        new[] { SUBMITTED,     "Submitted"            },
        new[] { APPROVED,      "Approved"             },
        new[] { HALTED,        "Halted"               },
        new[] { PRINTED,       "Printed"              },
        new[] { READY,         "Ready for collection" },
        new[] { COLLECTED,     "Collected"            },
        new[] { CANCELLED,     "Cancelled"            },
    };

    /// <summary>The human name for a status. Unknown values come back unchanged.</summary>
    public static string StatusLabel(string status)
    {
        string s = (status ?? "").Trim().ToUpperInvariant();
        for (int i = 0; i < Labels.Length; i++)
            if (Labels[i][0] == s) return Labels[i][1];
        return status ?? "";
    }

    /// <summary>The whole table as a JSON object, for pages that label in JavaScript.</summary>
    public static string StatusLabelsJson()
    {
        var sb = new System.Text.StringBuilder("{");
        for (int i = 0; i < Labels.Length; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append('"').Append(Labels[i][0]).Append('"').Append(':')
              .Append('"').Append(Labels[i][1]).Append('"');
        }
        return sb.Append("}").ToString();
    }

    // ── Forced ID-card collection ──────────────────────────────────────────────
    //
    // A card that has been printed is useless sitting in a drawer, and chasing
    // students one by one does not scale. This lets the ID office put a student
    // under an obligation: from a chosen date, the portal stops them until they
    // have physically collected the card from a named office.
    //
    // Deliberately its own table rather than another column on idcard_requests:
    // the obligation is about a PERSON and a PLACE, it can be applied to someone
    // whose request was created long ago (or by the OmniPass backfill, which
    // created no request the student ever saw), and it has to be liftable by the
    // counter clerk who hands the card over without touching the request's own
    // state machine.
    //
    // A block bites only when all three are true: it is active, the card has not
    // been collected, and the enforce-from date has arrived. Anything else — a
    // future date, a cleared row, a database that cannot be read — means the
    // student is not blocked. A portal-wide gate must fail OPEN.
    public const string POINT_AR = "AR_OFFICE";
    public const string POINT_IT = "IT_OFFICE";

    /// <summary>Where the student has been told to collect from, in words.</summary>
    public static string CollectionPointLabel(string point)
    {
        return string.Equals((point ?? "").Trim(), POINT_IT, StringComparison.OrdinalIgnoreCase)
            ? "ICT Office"
            : "Academic Registrar's Office";
    }

    /// <summary>The collection obligation standing over a student, if any.</summary>
    public class ForceCollection
    {
        public bool Active;                 // in force right now (active, uncollected, date reached)
        public bool Exists;                 // a row exists, even if not yet in force
        public string Regno = "";
        public string CollectionPoint = "";
        public string PointLabel = "";
        public string Campus = "";
        public string GroupName = "";       // the batch the cards were sorted into
        public string Note = "";
        public DateTime? EnforceFrom;
        public bool Collected;
    }

    // The portal's page gate calls ForceCollectionFor on every request a student makes,
    // and that used to re-issue the CREATE TABLE every single time. The schema cannot
    // change under a running process, so it is built once and then skipped. Worst case
    // two threads race and both build it — the statements are idempotent, so that is
    // harmless and cheaper than locking.
    private static bool _fcSchemaReady;

    /// <summary>Creates the tracking table if it is not there yet. Runs once per process.</summary>
    public static void EnsureForceCollectionSchema(MySqlConnection conn)
    {
        if (_fcSchemaReady) return;
        try
        {
            // utf8/utf8_general_ci on purpose: regno is compared against
            // acad_student.regno, and a charset mismatch silently makes the index
            // unusable (this system has been bitten by exactly that before).
            Exec(conn, "CREATE TABLE IF NOT EXISTS idcard_force_collection (" +
                " id INT NOT NULL AUTO_INCREMENT PRIMARY KEY," +
                " regno VARCHAR(50) NOT NULL," +
                " is_active TINYINT NOT NULL DEFAULT 1," +
                " collection_point VARCHAR(20) NOT NULL DEFAULT 'AR_OFFICE'," +
                " campus VARCHAR(120) NULL," +
                " group_name VARCHAR(10) NULL," +
                " enforce_from DATE NOT NULL," +
                " note VARCHAR(255) NULL," +
                " collected TINYINT NOT NULL DEFAULT 0," +
                " collected_at DATETIME NULL," +
                " collected_by VARCHAR(150) NULL," +
                " created_by VARCHAR(150) NULL," +
                " created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP," +
                " updated_by VARCHAR(150) NULL," +
                " updated_at DATETIME NULL," +
                " UNIQUE KEY uq_regno (regno)," +
                " KEY ix_enforce (is_active, collected, enforce_from)" +
                ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci");

            // group_name arrived after the table did, so an installation created by an
            // earlier build needs it added. Checked rather than blind-ALTERed so a normal
            // start-up costs one cheap information_schema read and no failing statement.
            using (var chk = new MySqlCommand(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE()" +
                " AND TABLE_NAME='idcard_force_collection' AND COLUMN_NAME='group_name'", conn))
            {
                if (Convert.ToInt64(chk.ExecuteScalar()) == 0)
                    Exec(conn, "ALTER TABLE idcard_force_collection ADD COLUMN group_name VARCHAR(10) NULL AFTER campus");
            }

            _fcSchemaReady = true;
        }
        catch { /* the table already existing is the normal case */ }
    }

    /// <summary>
    /// What, if anything, stands over this student. Never throws: this is called by
    /// the portal's page gate, and an unreadable table must not lock the portal.
    /// </summary>
    public static ForceCollection ForceCollectionFor(string regno)
    {
        var fc = new ForceCollection { Regno = (regno ?? "").Trim() };
        if (fc.Regno == "") return fc;
        try
        {
            using (var c = new MySqlConnection(ConnStr))
            {
                c.Open();
                EnsureForceCollectionSchema(c);
                using (var cmd = new MySqlCommand(
                    "SELECT is_active, collected, collection_point, IFNULL(campus,'') AS campus," +
                    "       IFNULL(group_name,'') AS group_name, enforce_from, IFNULL(note,'') AS note," +
                    "       (is_active = 1 AND collected = 0 AND enforce_from <= CURDATE()) AS in_force " +
                    "FROM idcard_force_collection WHERE regno = @r LIMIT 1", c))
                {
                    cmd.Parameters.AddWithValue("@r", fc.Regno);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read()) return fc;
                        fc.Exists = true;
                        fc.Collected = Convert.ToInt32(r["collected"]) == 1;
                        fc.CollectionPoint = Convert.ToString(r["collection_point"]);
                        fc.PointLabel = CollectionPointLabel(fc.CollectionPoint);
                        fc.Campus = Convert.ToString(r["campus"]);
                        fc.GroupName = Convert.ToString(r["group_name"]);
                        fc.Note = Convert.ToString(r["note"]);
                        if (r["enforce_from"] != DBNull.Value) fc.EnforceFrom = Convert.ToDateTime(r["enforce_from"]);
                        fc.Active = Convert.ToInt32(r["in_force"]) == 1;
                    }
                }
            }
        }
        catch { return new ForceCollection { Regno = fc.Regno }; }   // fail open
        return fc;
    }

    // Terminal states = request is closed; a person may open a new one.
    private static readonly HashSet<string> Terminal =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { COLLECTED, CANCELLED };

    // Legal transitions (from → allowed set). Anything not listed is rejected.
    private static readonly Dictionary<string, HashSet<string>> Allowed =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
    {
        { REQUESTED,     Set(FINANCE_CHECK, SUBMITTED, CANCELLED) },   // SUBMITTED = staff (no finance gate)
        { FINANCE_CHECK, Set(SUBMITTED, BLOCKED, CANCELLED) },
        { BLOCKED,       Set(FINANCE_CHECK, CANCELLED) },
        { SUBMITTED,     Set(APPROVED, HALTED, CANCELLED) },
        { APPROVED,      Set(PRINTED, HALTED) },
        { HALTED,        Set(SUBMITTED, CANCELLED) },
        { PRINTED,       Set(READY) },
        { READY,         Set(COLLECTED) },
        { COLLECTED,     Set() },
        { CANCELLED,     Set() },
    };

    /// <summary>Every status the machine knows, in lifecycle order (drives the admin picker).</summary>
    public static readonly string[] AllStatuses =
        { REQUESTED, FINANCE_CHECK, BLOCKED, SUBMITTED, APPROVED, HALTED, PRINTED, READY, COLLECTED, CANCELLED };

    /// <summary>True when the state machine permits this move without an override.</summary>
    public static bool IsLegalMove(string from, string to)
    {
        HashSet<string> ok;
        return Allowed.TryGetValue((from ?? "").ToUpperInvariant(), out ok) && ok.Contains((to ?? "").ToUpperInvariant());
    }

    /// <summary>The statuses this one may move to normally — empty for a terminal state.</summary>
    public static string[] LegalNext(string from)
    {
        HashSet<string> ok;
        if (!Allowed.TryGetValue((from ?? "").ToUpperInvariant(), out ok)) return new string[0];
        var list = new List<string>();
        foreach (string s in AllStatuses) if (ok.Contains(s)) list.Add(s);   // keep lifecycle order
        return list.ToArray();
    }

    // status → timeline column stamped on entry (NULL = none)
    private static string TimeCol(string status)
    {
        switch ((status ?? "").ToUpperInvariant())
        {
            case SUBMITTED: return "submitted_at";
            case APPROVED:  return "approved_at";
            case PRINTED:   return "printed_at";
            case READY:     return "ready_at";
            case COLLECTED: return "collected_at";
            default:        return null;
        }
    }
    // status → actor column stamped on entry (NULL = none)
    private static string ActorCol(string status)
    {
        switch ((status ?? "").ToUpperInvariant())
        {
            case APPROVED:  return "approved_by";
            case PRINTED:   return "printed_by";
            case COLLECTED: return "collected_by";
            default:        return null;
        }
    }

    public static bool IsTerminal(string status) { return Terminal.Contains(status ?? ""); }

    // ── schema self-heal (safe on every controller load) ──
    public static void EnsureSchema(MySqlConnection conn)
    {
        try
        {
            Exec(conn, "CREATE TABLE IF NOT EXISTS idcard_requests (" +
                " id INT PRIMARY KEY AUTO_INCREMENT, request_no VARCHAR(20) NOT NULL," +
                " requester_type VARCHAR(10) NOT NULL, regno VARCHAR(35) NULL, emp_id INT NULL," +
                " card_type VARCHAR(15) NOT NULL, status VARCHAR(20) NOT NULL DEFAULT 'REQUESTED'," +
                " photo_ref VARCHAR(255) NULL, photo_confirmed TINYINT NOT NULL DEFAULT 0, guidelines_ack TINYINT NOT NULL DEFAULT 0," +
                " finance_ok TINYINT NULL, finance_snapshot_json TEXT NULL," +
                " replacement_fee_ref VARCHAR(60) NULL, replacement_fee_date DATE NULL, replacement_fee_method VARCHAR(20) NULL, replacement_fee_notes VARCHAR(255) NULL," +
                " window_id INT NULL, halt_reason VARCHAR(255) NULL," +
                " submitted_at DATETIME NULL, approved_at DATETIME NULL, printed_at DATETIME NULL, ready_at DATETIME NULL, collected_at DATETIME NULL," +
                " approved_by VARCHAR(150) NULL, printed_by VARCHAR(150) NULL, collected_by VARCHAR(150) NULL," +
                " notes VARCHAR(255) NULL, created_by VARCHAR(150) NULL, created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, updated_at DATETIME NULL," +
                " UNIQUE KEY uq_request_no (request_no), KEY ix_status (status), KEY ix_regno (regno), KEY ix_emp (emp_id), KEY ix_type (requester_type), KEY ix_window (window_id)" +
                ") ENGINE=InnoDB DEFAULT CHARSET=utf8");
            Exec(conn, "CREATE TABLE IF NOT EXISTS idcard_request_events (" +
                " id INT PRIMARY KEY AUTO_INCREMENT, request_id INT NOT NULL, from_status VARCHAR(20) NULL, to_status VARCHAR(20) NOT NULL," +
                " actor VARCHAR(150) NULL, actor_role VARCHAR(40) NULL, channel VARCHAR(12) NULL, note VARCHAR(500) NULL, email_sent TINYINT NULL," +
                " created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, KEY ix_req (request_id), KEY ix_to (to_status)" +
                ") ENGINE=InnoDB DEFAULT CHARSET=utf8");
            Exec(conn, "CREATE TABLE IF NOT EXISTS idcard_windows (" +
                " id INT PRIMARY KEY AUTO_INCREMENT, title VARCHAR(150) NOT NULL, requester_scope VARCHAR(10) NOT NULL DEFAULT 'BOTH'," +
                " opens_at DATETIME NOT NULL, closes_at DATETIME NOT NULL, is_active TINYINT NOT NULL DEFAULT 1, notes VARCHAR(255) NULL," +
                " created_by VARCHAR(150) NULL, created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, KEY ix_active (is_active), KEY ix_range (opens_at, closes_at)" +
                ") ENGINE=InnoDB DEFAULT CHARSET=utf8");
            AddCol(conn, "idcard_requests", "collection_point", "VARCHAR(200) NULL");
            AddCol(conn, "hrm_employee", "photo_file", "VARCHAR(255) NULL");
            AddCol(conn, "hrm_employee", "photo_updated_at", "DATETIME NULL");
        }
        catch { /* never break a page on self-heal */ }
    }

    // ── one active request per person ──
    /// <summary>Returns the request_no of the person's current non-terminal request, or null.</summary>
    public static string ActiveRequestNo(MySqlConnection conn, string requesterType, string regno, int empId)
    {
        string w = IsStaff(requesterType) ? " emp_id=@e " : " TRIM(regno)=TRIM(@r) ";
        using (var cmd = new MySqlCommand(
            "SELECT request_no FROM idcard_requests WHERE requester_type=@t AND" + w +
            " AND status NOT IN ('COLLECTED','CANCELLED') ORDER BY id DESC LIMIT 1", conn))
        {
            cmd.Parameters.AddWithValue("@t", Norm(requesterType));
            if (IsStaff(requesterType)) cmd.Parameters.AddWithValue("@e", empId);
            else cmd.Parameters.AddWithValue("@r", regno ?? "");
            object v = cmd.ExecuteScalar();
            return (v == null || v == DBNull.Value) ? null : v.ToString();
        }
    }

    // ── may this person start a request? ──
    //
    // "One active request" used to mean "any status except COLLECTED or CANCELLED",
    // which quietly locked out everybody whose card had already been produced. The
    // 2,486 students carried over from OmniPass sit at PRINTED — a request none of
    // them ever placed, created on their behalf by the backfill — and PRINTED is not
    // terminal, so the wizard showed them a progress tracker with no way forward and
    // no way to ask for a replacement. They are exactly the people who need one.
    //
    // The line is drawn at "has a card been produced for you yet":
    //
    //   REQUESTED, FINANCE_CHECK, BLOCKED, SUBMITTED, APPROVED, HALTED
    //        still moving through the pipeline. One at a time — finish or cancel it.
    //   READY
    //        the card is printed and waiting at the collection point. A card you have
    //        not picked up yet cannot have been lost, so collect it first. Blocking
    //        this state stops a replacement fee being charged for a card sitting on a
    //        desk in the ID office.
    //   PRINTED, COLLECTED
    //        a card exists and is in the student's hands as far as this system knows.
    //        Lost or damaged? That is a replacement, and it is allowed.
    //   CANCELLED
    //        nothing was ever produced, so the next one is an ordinary new card.
    private static readonly HashSet<string> BlocksNewRequest =
        Set(REQUESTED, FINANCE_CHECK, BLOCKED, SUBMITTED, APPROVED, HALTED, READY);

    /// <summary>Statuses that mean a physical card was produced for this person.</summary>
    private static readonly HashSet<string> CardWasProduced = Set(PRINTED, READY, COLLECTED);

    /// <summary>Whether a person may start a request now, and if not, exactly why.</summary>
    public class Eligibility
    {
        public bool CanRequest;
        public string BlockingRequestNo;      // the request standing in the way, if any
        public string BlockingStatus;
        public string Message;                // shown to the requester verbatim
        public bool CanEditBlocking;          // true when that request is still editable (pre-submission)
        public bool HasCard;                  // a card has been produced for them before
        public string SuggestedCardType;      // NEW or REPLACEMENT
    }

    public static Eligibility CheckEligibility(MySqlConnection conn, string requesterType, string regno, int empId)
    {
        var el = new Eligibility { CanRequest = true, SuggestedCardType = "NEW" };
        string type = Norm(requesterType);
        string w = IsStaff(type) ? " emp_id=@e " : " TRIM(regno)=TRIM(@r) ";

        // The newest request that is still standing in the way, if there is one.
        using (var cmd = new MySqlCommand(
            "SELECT request_no, status FROM idcard_requests WHERE requester_type=@t AND" + w +
            " AND status IN ('REQUESTED','FINANCE_CHECK','BLOCKED','SUBMITTED','APPROVED','HALTED','READY')" +
            " ORDER BY id DESC LIMIT 1", conn))
        {
            cmd.Parameters.AddWithValue("@t", type);
            if (IsStaff(type)) cmd.Parameters.AddWithValue("@e", empId); else cmd.Parameters.AddWithValue("@r", regno ?? "");
            using (var rd = cmd.ExecuteReader())
            {
                if (rd.Read())
                {
                    el.BlockingRequestNo = rd.GetString(0);
                    el.BlockingStatus = rd.GetString(1).ToUpperInvariant();
                    el.CanRequest = false;
                }
            }
        }

        // Has a card ever been produced for them? Checked across ALL their requests,
        // not just the newest: a student whose card was printed years ago and who has
        // since cancelled a later attempt still has a card, and asking for another one
        // is still a replacement.
        using (var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM idcard_requests WHERE requester_type=@t AND" + w +
            " AND status IN ('PRINTED','READY','COLLECTED')", conn))
        {
            cmd.Parameters.AddWithValue("@t", type);
            if (IsStaff(type)) cmd.Parameters.AddWithValue("@e", empId); else cmd.Parameters.AddWithValue("@r", regno ?? "");
            el.HasCard = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }
        if (el.HasCard) el.SuggestedCardType = "REPLACEMENT";

        if (!el.CanRequest)
        {
            if (el.BlockingStatus == READY)
            {
                el.Message = "Your ID card (" + el.BlockingRequestNo + ") has already been printed and is waiting for you at the collection point. "
                           + "Please collect it first. If it is lost or damaged after that, you can then ask for a replacement.";
            }
            else if (el.BlockingStatus == SUBMITTED || el.BlockingStatus == APPROVED)
            {
                el.Message = "You already have a request in progress (" + el.BlockingRequestNo + ", " + el.BlockingStatus.ToLowerInvariant()
                           + "). Please wait for it to finish before asking for another card.";
            }
            else
            {
                // REQUESTED / FINANCE_CHECK / BLOCKED / HALTED — theirs to finish or change.
                el.CanEditBlocking = true;
                el.Message = "You already have a request in progress (" + el.BlockingRequestNo + "). You can finish it, change it, or cancel it.";
            }
        }
        return el;
    }

    // ── create a request (Step 1) ──
    public class CreateResult { public bool Ok; public string RequestNo; public int Id; public string Message; }

    public static CreateResult CreateRequest(string requesterType, string regno, int empId, string cardType,
        string photoRef, bool photoConfirmed, bool guidelinesAck, int windowId, string createdBy)
    {
        var res = new CreateResult();
        string type = Norm(requesterType);
        if (type != "STUDENT" && type != "STAFF") { res.Message = "Invalid requester type."; return res; }
        string ct = (cardType ?? "").Trim().ToUpperInvariant();
        if (ct != "NEW" && ct != "REPLACEMENT") { res.Message = "Choose New or Replacement."; return res; }
        if (type == "STUDENT" && string.IsNullOrEmpty((regno ?? "").Trim())) { res.Message = "Missing student number."; return res; }
        if (type == "STAFF" && empId <= 0) { res.Message = "Missing staff id."; return res; }

        using (var conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            EnsureSchema(conn);

            // The single gate on starting a request. Checked here rather than only in the
            // wizard, because the wizard decides what to SHOW and this decides what is
            // ALLOWED — the portal, the console and the API all arrive through here.
            Eligibility el = CheckEligibility(conn, type, regno, empId);
            if (!el.CanRequest) { res.Message = el.Message; return res; }

            // generate a unique request_no, retry on the rare unique-key clash
            for (int attempt = 0; attempt < 5; attempt++)
            {
                string rn = NextRequestNo(conn);
                try
                {
                    int id;
                    using (var cmd = new MySqlCommand(
                        "INSERT INTO idcard_requests (request_no, requester_type, regno, emp_id, card_type, status," +
                        " photo_ref, photo_confirmed, guidelines_ack, window_id, created_by, created_at, updated_at)" +
                        " VALUES (@rn,@t,@r,@e,@ct,'REQUESTED',@pr,@pc,@ga,@w,@by,NOW(),NOW())", conn))
                    {
                        cmd.Parameters.AddWithValue("@rn", rn);
                        cmd.Parameters.AddWithValue("@t", type);
                        cmd.Parameters.AddWithValue("@r", type == "STUDENT" ? (object)regno.Trim() : DBNull.Value);
                        cmd.Parameters.AddWithValue("@e", type == "STAFF" ? (object)empId : DBNull.Value);
                        cmd.Parameters.AddWithValue("@ct", ct);
                        cmd.Parameters.AddWithValue("@pr", (object)(photoRef ?? "") );
                        cmd.Parameters.AddWithValue("@pc", photoConfirmed ? 1 : 0);
                        cmd.Parameters.AddWithValue("@ga", guidelinesAck ? 1 : 0);
                        cmd.Parameters.AddWithValue("@w", windowId > 0 ? (object)windowId : DBNull.Value);
                        cmd.Parameters.AddWithValue("@by", createdBy ?? "");
                        cmd.ExecuteNonQuery();
                        id = (int)cmd.LastInsertedId;
                    }
                    LogEvent(conn, id, null, REQUESTED, createdBy, RoleFor(type), "eportal", "Request created");
                    res.Ok = true; res.RequestNo = rn; res.Id = id; res.Message = "Request " + rn + " created.";
                    return res;
                }
                catch (MySqlException ex)
                {
                    if (ex.Number == 1062) continue;   // duplicate request_no → regenerate
                    res.Message = "Could not create request: " + ex.Message; return res;
                }
            }
            res.Message = "Could not allocate a request number, please retry.";
            return res;
        }
    }

    // IDR-YYYY-NNNNNN  (year-scoped running sequence)
    private static string NextRequestNo(MySqlConnection conn)
    {
        int year = ServerYear(conn);
        string prefix = "IDR-" + year + "-";
        int next = 1;
        using (var cmd = new MySqlCommand(
            "SELECT IFNULL(MAX(CAST(SUBSTRING(request_no, LENGTH(@p)+1) AS UNSIGNED)),0)+1 FROM idcard_requests WHERE request_no LIKE CONCAT(@p,'%')", conn))
        {
            cmd.Parameters.AddWithValue("@p", prefix);
            object v = cmd.ExecuteScalar();
            if (v != null && v != DBNull.Value) next = Convert.ToInt32(v);
        }
        return prefix + next.ToString("D6", CultureInfo.InvariantCulture);
    }

    // ── the single transition funnel ──
    public class TransitionResult { public bool Ok; public string Status; public string Message; public int RequestId; }

    public static TransitionResult Transition(string requestNo, string toStatus, string actor, string actorRole,
        string channel, string note, string haltReason)
    {
        var res = new TransitionResult();
        string to = Norm(toStatus);
        using (var conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            EnsureSchema(conn);

            int id; string from;
            using (var cmd = new MySqlCommand("SELECT id, status FROM idcard_requests WHERE request_no=@rn LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@rn", requestNo ?? "");
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) { res.Message = "Request not found."; return res; }
                    id = r.GetInt32(0); from = r.GetString(1);
                }
            }
            res.RequestId = id; res.Status = from;

            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            { res.Ok = true; res.Message = "Already " + to + "."; return res; }   // idempotent

            HashSet<string> ok;
            if (!Allowed.TryGetValue(from.ToUpperInvariant(), out ok) || !ok.Contains(to))
            { res.Message = "Cannot move from " + from + " to " + to + "."; return res; }

            if (to == HALTED && string.IsNullOrEmpty((haltReason ?? "").Trim()))
            { res.Message = "A reason is required to halt a request."; return res; }

            string tc = TimeCol(to), ac = ActorCol(to);
            var sb = new System.Text.StringBuilder("UPDATE idcard_requests SET status=@to, updated_at=NOW()");
            if (tc != null) sb.Append(", " + tc + "=NOW()");
            if (ac != null) sb.Append(", " + ac + "=@actor");
            if (to == HALTED) sb.Append(", halt_reason=@hr");
            sb.Append(" WHERE id=@id AND status=@from");   // optimistic guard: state must not have moved
            using (var up = new MySqlCommand(sb.ToString(), conn))
            {
                up.Parameters.AddWithValue("@to", to);
                if (ac != null) up.Parameters.AddWithValue("@actor", actor ?? "");
                if (to == HALTED) up.Parameters.AddWithValue("@hr", (haltReason ?? "").Trim());
                up.Parameters.AddWithValue("@id", id);
                up.Parameters.AddWithValue("@from", from);
                if (up.ExecuteNonQuery() == 0) { res.Message = "The request changed state — reload and retry."; return res; }
            }

            string evNote = note;
            if (to == HALTED) evNote = "Halted: " + (haltReason ?? "").Trim() + (string.IsNullOrEmpty(note) ? "" : (" — " + note));
            LogEvent(conn, id, from, to, actor, actorRole, channel, evNote);
            TryNotify(conn, id, to);   // email the requester on notable status changes (best-effort)

            res.Ok = true; res.Status = to; res.Message = "Moved to " + to + ".";
            return res;
        }
    }

    // ── audit event (also the hook where email will be sent later) ──
    public static void LogEvent(MySqlConnection conn, int requestId, string from, string to,
        string actor, string role, string channel, string note)
    {
        using (var cmd = new MySqlCommand(
            "INSERT INTO idcard_request_events (request_id, from_status, to_status, actor, actor_role, channel, note, created_at)" +
            " VALUES (@r,@f,@t,@a,@ro,@c,@n,NOW())", conn))
        {
            cmd.Parameters.AddWithValue("@r", requestId);
            cmd.Parameters.AddWithValue("@f", (object)from ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@t", to);
            cmd.Parameters.AddWithValue("@a", (object)(actor ?? ""));
            cmd.Parameters.AddWithValue("@ro", (object)(role ?? ""));
            cmd.Parameters.AddWithValue("@c", (object)(channel ?? "system"));
            cmd.Parameters.AddWithValue("@n", (object)(note ?? ""));
            cmd.ExecuteNonQuery();
        }
    }

    // ── helpers ──
    private static HashSet<string> Set(params string[] xs)
    { return new HashSet<string>(xs, StringComparer.OrdinalIgnoreCase); }
    private static bool IsStaff(string t) { return Norm(t) == "STAFF"; }
    private static string Norm(string t) { return (t ?? "").Trim().ToUpperInvariant(); }
    private static string RoleFor(string type) { return Norm(type) == "STAFF" ? "staff" : "student"; }
    private static void Exec(MySqlConnection conn, string sql) { using (var c = new MySqlCommand(sql, conn)) c.ExecuteNonQuery(); }
    private static int ServerYear(MySqlConnection conn) { using (var c = new MySqlCommand("SELECT YEAR(NOW())", conn)) return Convert.ToInt32(c.ExecuteScalar()); }
    private static void AddCol(MySqlConnection conn, string tbl, string col, string def)
    {
        using (var c = new MySqlCommand("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=@t AND COLUMN_NAME=@c", conn))
        {
            c.Parameters.AddWithValue("@t", tbl); c.Parameters.AddWithValue("@c", col);
            if (Convert.ToInt32(c.ExecuteScalar()) > 0) return;
        }
        using (var a = new MySqlCommand("ALTER TABLE " + tbl + " ADD COLUMN " + col + " " + def, conn)) a.ExecuteNonQuery();
    }
}
