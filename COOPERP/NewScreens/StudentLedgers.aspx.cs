using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Web.Configuration;
using System.Web.UI;
using System.Web.UI.WebControls;
using MySql.Data.MySqlClient;

public partial class COOPERP_NewScreens_StudentLedgers : System.Web.UI.Page
{
    // ── Static lookup cache (shared across requests, 5-minute TTL) ────────
    private static List<string[]> _cachedProgrammes;
    private static List<string> _cachedEntryYears;
    private static List<string> _cachedAcadYears;
    private static DateTime _lookupExpiry = DateTime.MinValue;
    private static readonly object _lookupLock = new object();

    // ── Balance-cache refresh throttle (see EnsureBalanceCache) ───────────
    private static DateTime _balCacheNextCheck = DateTime.MinValue;
    private static readonly object _balCacheGate = new object();
    private const int BalCacheTtlSeconds = 300;       // rebuild when older than 5 min
    private const int BalCacheThrottleSeconds = 30;   // don't re-check DB freshness more often than this

    private string AcctConnStr
    {
        get { return WebConfigurationManager.ConnectionStrings["accountsConnectionString"].ConnectionString; }
    }

    private string MainConnStr
    {
        get { return WebConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        string ajax = (Request.QueryString["ajax"] ?? "").Trim().ToLower();
        if (!string.IsNullOrEmpty(ajax))
        {
            HandleAjax(ajax);
            return;
        }

        LoadLookups();

        // ── All filters, sort and pagination state are read from the query
        //    string (GET). This keeps the listing fully bookmarkable / shareable
        //    and avoids WebForms POST postbacks for navigation.
        SetDdlFromQuery(ddlProgramme, "prog");
        SetDdlFromQuery(ddlEntryYear, "entry");
        SetDdlFromQuery(ddlAcadYear, "acad");
        SetDdlFromQuery(ddlSemester, "sem");
        SetDdlFromQuery(ddlBalanceState, "bal");
        SetDdlFromQuery(ddlBalanceOp, "balop");
        SetDdlFromQuery(ddlPageSize, "rows");

        txtSearch.Text      = (Request.QueryString["q"]       ?? "").Trim();
        txtBalanceFrom.Text = (Request.QueryString["balfrom"] ?? "").Trim();
        txtBalanceTo.Text   = (Request.QueryString["balto"]   ?? "").Trim();
        chkWithTransactions.Checked = (Request.QueryString["tx"] ?? "") == "1";

        string sortField = (Request.QueryString["sort"] ?? "").Trim();
        string sortDir   = (Request.QueryString["dir"]  ?? "").Trim().ToUpper();
        hfSortField.Value = string.IsNullOrEmpty(sortField) ? "balance" : sortField;
        // Default direction ASC: with the new sign convention the most negative
        // balances are the biggest amounts OWING, so they surface first by default.
        hfSortDir.Value   = sortDir == "DESC" ? "DESC" : "ASC";
        hfPageIndex.Value = (Request.QueryString["page"] ?? "0").Trim();

        // Export is also GET-driven (?export=csv|excel) so it honours the same filters.
        string export = (Request.QueryString["export"] ?? "").Trim().ToLower();
        if (export == "csv")   { ExportData("csv");   return; }
        if (export == "excel") { ExportData("excel"); return; }

        LoadLedgers();
    }

    private void HandleAjax(string ajax)
    {
        Response.Clear();
        Response.ContentType = "application/json";
        Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
        Response.Cache.SetNoStore();
        Response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));

        try
        {
            if (ajax == "ledger")
                AjaxLedgerDetails();
            else if (ajax == "fixbillingscan")
                AjaxFixBillingScan();
            else if (ajax == "fixbillingapply")
                AjaxFixBillingApply();
            else if (ajax == "fixbilling")
                AjaxFixBilling();
            else if (ajax == "removedouble")
                AjaxRemoveDoubleBilling();
            else
                Response.Write("{\"ok\":false,\"error\":\"Unsupported action.\"}");
        }
        catch (Exception ex)
        {
            string err = ex.Message;
            if (ex.InnerException != null && !string.IsNullOrEmpty(ex.InnerException.Message))
                err = err + " | " + ex.InnerException.Message;
            Response.Write("{\"ok\":false,\"error\":\"" + JsEsc(err) + "\"}");
        }

        try { Response.End(); }
        catch (System.Threading.ThreadAbortException) { }
    }

    private void LoadLookups()
    {
        ddlProgramme.Items.Clear();
        ddlProgramme.Items.Add(new ListItem("All Programmes", ""));

        ddlEntryYear.Items.Clear();
        ddlEntryYear.Items.Add(new ListItem("All Entry Years", ""));

        ddlAcadYear.Items.Clear();
        ddlAcadYear.Items.Add(new ListItem("All Current Years", ""));

        ddlSemester.Items.Clear();
        ddlSemester.Items.Add(new ListItem("All Semesters", ""));
        ddlSemester.Items.Add(new ListItem("Semester 1", "1"));
        ddlSemester.Items.Add(new ListItem("Semester 2", "2"));
        ddlSemester.Items.Add(new ListItem("Semester 3", "3"));

        ddlBalanceState.Items.Clear();
        ddlBalanceState.Items.Add(new ListItem("All Balances", ""));
        ddlBalanceState.Items.Add(new ListItem("Owing School (-)", "owing"));
        ddlBalanceState.Items.Add(new ListItem("Cleared (Zero)", "zero"));
        ddlBalanceState.Items.Add(new ListItem("Overpaid / Credit (+)", "overpaid"));

        ddlBalanceOp.Items.Clear();
        ddlBalanceOp.Items.Add(new ListItem("No amount filter", ""));
        ddlBalanceOp.Items.Add(new ListItem("= Equal", "eq"));
        ddlBalanceOp.Items.Add(new ListItem("< Less than", "lt"));
        ddlBalanceOp.Items.Add(new ListItem("> Greater than", "gt"));
        ddlBalanceOp.Items.Add(new ListItem("Between", "between"));

        ddlPageSize.Items.Clear();
        ddlPageSize.Items.Add(new ListItem("25", "25"));
        ddlPageSize.Items.Add(new ListItem("50", "50"));
        ddlPageSize.Items.Add(new ListItem("100", "100"));
        ddlPageSize.Items.Add(new ListItem("200", "200"));

        // Use static cache for DB-driven lookups (5-minute TTL)
        bool needRefresh;
        lock (_lookupLock)
        {
            needRefresh = _cachedProgrammes == null || DateTime.UtcNow > _lookupExpiry;
        }

        if (needRefresh)
        {
            List<string[]> progs = new List<string[]>();
            List<string> entryYears = new List<string>();
            List<string> acadYears = new List<string>();

            using (MySqlConnection conn = new MySqlConnection(MainConnStr))
            {
                conn.Open();

                using (MySqlCommand cmd = new MySqlCommand("SELECT progcode, progname FROM acad_programme ORDER BY progname", conn))
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        progs.Add(new string[] { rdr["progcode"].ToString(), rdr["progname"].ToString() });
                    }
                }

                using (MySqlCommand cmd = new MySqlCommand("SELECT DISTINCT TRIM(entryyear) AS entryyear FROM acad_student WHERE entryyear IS NOT NULL AND TRIM(entryyear) <> '' ORDER BY entryyear DESC", conn))
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        entryYears.Add(rdr["entryyear"].ToString());
                    }
                }

                using (MySqlCommand cmd = new MySqlCommand("SELECT DISTINCT acad_year FROM acad_registration WHERE acad_year IS NOT NULL AND acad_year <> '' ORDER BY acad_year DESC", conn))
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        acadYears.Add(rdr["acad_year"].ToString());
                    }
                }
            }

            lock (_lookupLock)
            {
                _cachedProgrammes = progs;
                _cachedEntryYears = entryYears;
                _cachedAcadYears = acadYears;
                _lookupExpiry = DateTime.UtcNow.AddMinutes(5);
            }
        }

        // Populate dropdowns from cache
        lock (_lookupLock)
        {
            foreach (string[] p in _cachedProgrammes)
                ddlProgramme.Items.Add(new ListItem(p[1], p[0]));
            foreach (string y in _cachedEntryYears)
                ddlEntryYear.Items.Add(new ListItem(y, y));
            foreach (string y in _cachedAcadYears)
                ddlAcadYear.Items.Add(new ListItem(y, y));
        }
    }

    /// <summary>Selects the list item matching the given query-string value, if present.</summary>
    private void SetDdlFromQuery(DropDownList ddl, string key)
    {
        string v = (Request.QueryString[key] ?? "").Trim();
        if (string.IsNullOrEmpty(v)) return;
        ListItem item = ddl.Items.FindByValue(v);
        if (item != null)
        {
            ddl.ClearSelection();
            item.Selected = true;
        }
    }

    // ── Balance cache ─────────────────────────────────────────────────────
    // Materialises the canonical per-student billed/paid/balance (the same
    // dual-source dedup formula FinanceEngine / the fees statement uses) into
    // fin_student_balance_cache so the grid can read accurate totals instantly.
    // Rebuilt at most once per TTL, guarded by a server-wide named lock so only
    // one request rebuilds at a time; the rebuild runs inside a transaction so
    // concurrent readers never see an empty/partial table.
    private void EnsureBalanceCache(MySqlConnection conn)
    {
        // Cheap in-process throttle: skip the DB freshness probe if we checked recently.
        lock (_balCacheGate)
        {
            if (DateTime.UtcNow < _balCacheNextCheck) return;
        }

        // Ensure the cache table exists.
        using (MySqlCommand cmd = new MySqlCommand(
            "CREATE TABLE IF NOT EXISTS campus_dynamics_accounts.fin_student_balance_cache (" +
            "  regno VARCHAR(50) NOT NULL," +
            "  total_billed DECIMAL(18,2) NOT NULL DEFAULT 0," +
            "  total_paid DECIMAL(18,2) NOT NULL DEFAULT 0," +
            "  total_balance DECIMAL(18,2) NOT NULL DEFAULT 0," +
            "  tx_count INT NOT NULL DEFAULT 0," +
            "  updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP," +
            "  PRIMARY KEY (regno)" +
            ") ENGINE=InnoDB", conn))
        {
            cmd.CommandTimeout = 30;
            cmd.ExecuteNonQuery();
        }

        if (!IsBalanceCacheStale(conn))
        {
            lock (_balCacheGate) { _balCacheNextCheck = DateTime.UtcNow.AddSeconds(BalCacheThrottleSeconds); }
            return;
        }

        // Become the rebuilder. On first build (empty table) wait briefly for the
        // lock so the user gets data; on a routine TTL refresh, don't wait — just
        // serve the slightly-stale cache while another request refreshes.
        bool empty = BalanceCacheRowCount(conn) == 0;
        int lockTimeout = empty ? 30 : 0;
        bool gotLock = false;
        using (MySqlCommand cmd = new MySqlCommand("SELECT GET_LOCK('sl_bal_cache_refresh', @t)", conn))
        {
            cmd.Parameters.AddWithValue("@t", lockTimeout);
            object v = cmd.ExecuteScalar();
            gotLock = v != null && v != DBNull.Value && Convert.ToInt32(v) == 1;
        }

        if (!gotLock)
        {
            // Another request is already rebuilding — use existing data, retry soon.
            lock (_balCacheGate) { _balCacheNextCheck = DateTime.UtcNow.AddSeconds(5); }
            return;
        }

        try
        {
            // Re-check under the lock; another request may have just rebuilt.
            if (IsBalanceCacheStale(conn))
            {
                using (MySqlTransaction tx = conn.BeginTransaction())
                {
                    using (MySqlCommand del = new MySqlCommand(
                        "DELETE FROM campus_dynamics_accounts.fin_student_balance_cache", conn, tx))
                    {
                        del.CommandTimeout = 60;
                        del.ExecuteNonQuery();
                    }
                    using (MySqlCommand ins = new MySqlCommand(GetBalanceCacheRebuildSql(), conn, tx))
                    {
                        ins.CommandTimeout = 180;
                        ins.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
            }
        }
        finally
        {
            using (MySqlCommand cmd = new MySqlCommand("SELECT RELEASE_LOCK('sl_bal_cache_refresh')", conn))
            {
                try { cmd.ExecuteScalar(); } catch { }
            }
            lock (_balCacheGate) { _balCacheNextCheck = DateTime.UtcNow.AddSeconds(BalCacheThrottleSeconds); }
        }
    }

    private bool IsBalanceCacheStale(MySqlConnection conn)
    {
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT COUNT(*) AS c, " +
            "       COALESCE(TIMESTAMPDIFF(SECOND, MAX(updated_at), NOW()), 999999999) AS age " +
            "FROM campus_dynamics_accounts.fin_student_balance_cache", conn))
        {
            cmd.CommandTimeout = 15;
            using (MySqlDataReader rdr = cmd.ExecuteReader())
            {
                if (rdr.Read())
                {
                    long c = rdr["c"] != DBNull.Value ? Convert.ToInt64(rdr["c"]) : 0;
                    long age = rdr["age"] != DBNull.Value ? Convert.ToInt64(rdr["age"]) : long.MaxValue;
                    return c == 0 || age > BalCacheTtlSeconds;
                }
            }
        }
        return true;
    }

    private long BalanceCacheRowCount(MySqlConnection conn)
    {
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM campus_dynamics_accounts.fin_student_balance_cache", conn))
        {
            cmd.CommandTimeout = 15;
            object v = cmd.ExecuteScalar();
            return v != null && v != DBNull.Value ? Convert.ToInt64(v) : 0;
        }
    }

    /// <summary>
    /// Canonical dual-source balance aggregation, restricted to real students.
    /// Mirrors FinanceEngine.ComputePeriodBalance (all-time): fin_ledger GL plus
    /// fin_studentfeestracking bills/payments that are NOT already mirrored in the
    /// ledger (triple dedup on voucherNo / folio / amount+date+type+particulars).
    /// </summary>
    private string GetBalanceCacheRebuildSql()
    {
        return
            "INSERT INTO campus_dynamics_accounts.fin_student_balance_cache " +
            "(regno, total_billed, total_paid, total_balance, tx_count) " +
            "SELECT u.regno, " +
            "       SUM(u.dr) AS total_billed, " +
            "       SUM(u.cr) AS total_paid, " +
            // + overpaid (credit), - owing (debit); grid recomputes from the
            // billed/paid columns so this stored value is informational only.
            "       SUM(u.cr) - SUM(u.dr) AS total_balance, " +
            "       COUNT(*) AS tx_count " +
            "FROM ( " +
            "  SELECT fl.accountcode AS regno, " +
            "         CASE WHEN fl.transactionType='DR' THEN fl.transaction_amount ELSE 0 END AS dr, " +
            "         CASE WHEN fl.transactionType='CR' THEN fl.transaction_amount ELSE 0 END AS cr " +
            "  FROM campus_dynamics_accounts.fin_ledger fl " +
            "  WHERE fl.transaction_amount > 0 " +
            "  UNION ALL " +
            "  SELECT t.regno AS regno, " +
            "         CASE WHEN t.trans_type='Bill'    THEN t.amount ELSE 0 END AS dr, " +
            "         CASE WHEN t.trans_type='Payment' THEN t.amount ELSE 0 END AS cr " +
            "  FROM campus_dynamics_accounts.fin_studentfeestracking t " +
            "  WHERE t.post_status = 'Posted' " +
            "    AND NOT EXISTS ( " +
            "      SELECT 1 FROM campus_dynamics_accounts.fin_ledger fl2 " +
            "      WHERE fl2.accountcode = t.regno " +
            "        AND ( fl2.voucherNo = CAST(t.TID AS CHAR) " +
            "           OR fl2.folio = CONCAT('BillNo:', CAST(t.TID AS CHAR)) " +
            "           OR ( fl2.transaction_amount = t.amount " +
            "                AND DATE(fl2.transactionDate) = DATE(t.trans_date) " +
            "                AND fl2.transactionType = CASE WHEN t.trans_type='Payment' THEN 'CR' ELSE 'DR' END " +
            "                AND (t.trans_type = 'Payment' OR fl2.particulars = t.detail OR t.detail IS NULL OR t.detail = '') ) ) " +
            "    ) " +
            ") u " +
            "INNER JOIN campus_dynamics.acad_student s ON s.regno = u.regno " +
            "GROUP BY u.regno";
    }

    // ── Performance-critical base query ───────────────────────────────────
    // Per-student billed/paid/balance totals come from the canonical dual-source
    // formula (fin_ledger GL + unmirrored fin_studentfeestracking, deduplicated) —
    // identical to FinanceEngine.ComputePeriodBalance, which is what the student
    // account and fees-statement pages use.
    //
    // An earlier version of this grid summed fin_ledger ONLY. That omitted the
    // ~47% of billing/payment records that live solely in fin_studentfeestracking,
    // so the grid balance was badly understated versus the fees statement.
    //
    // The canonical formula is too expensive (~15s anti-join) to run inline on
    // every sort/page postback, so it is materialised once into
    // fin_student_balance_cache (refreshed on a short TTL by EnsureBalanceCache).
    // The grid just joins that cache for instant, accurate reads.
    //
    // SIGN CONVENTION: total_balance = total_paid - total_billed, so a POSITIVE
    // balance = OVERPAID (credit) and a NEGATIVE balance = still OWING (debit).
    // This matches FinanceEngine's public balance ( -(charges-payments) ) used by
    // the student account and fees statement.
    private string BuildBaseSql()
    {
        // When the user is searching, include ALL students (any status)
        // so they can look up any student by reg number, name, etc.
        // Default listing still shows only ACTIVE students.
        bool hasSearch = !string.IsNullOrEmpty(txtSearch.Text.Trim());
        string statusFilter = hasSearch
            ? "WHERE 1=1"
            : "WHERE UPPER(COALESCE(s.new_status,'')) = 'ACTIVE'";

        return @"
            SELECT 
                s.regno,
                COALESCE(s.entryno, '') AS entry_no,
                TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) AS student_name,
                COALESCE(s.progid, '') AS progid,
                COALESCE(p.progname, 'N/A') AS programme_name,
                COALESCE(s.entryyear, '') AS entry_year,
                COALESCE(lr.acad_year, '-') AS current_acad_year,
                COALESCE(lr.semester, 0) AS current_semester,
                COALESCE(lr.studyyear, 0) AS current_study_year,
                COALESCE(ft.total_billed, 0) AS total_billed,
                COALESCE(ft.total_paid, 0) AS total_paid,
                (COALESCE(ft.total_paid, 0) - COALESCE(ft.total_billed, 0)) AS total_balance,
                COALESCE(ft.tx_count, 0) AS tx_count
            FROM campus_dynamics.acad_student s
            LEFT JOIN campus_dynamics.acad_programme p ON p.progcode = s.progid
            LEFT JOIN (
                SELECT r.regno, r.acad_year, r.semester, r.studyyear
                FROM campus_dynamics.acad_registration r
                INNER JOIN (
                    SELECT regno, MAX(ID) AS max_id
                    FROM campus_dynamics.acad_registration
                    GROUP BY regno
                ) m ON m.regno = r.regno AND r.ID = m.max_id
            ) lr ON lr.regno = s.regno
            LEFT JOIN campus_dynamics_accounts.fin_student_balance_cache ft ON ft.regno = s.regno
            " + statusFilter;
    }

    private string BuildWhereClause(List<MySqlParameter> parameters)
    {
        StringBuilder where = new StringBuilder(" WHERE 1=1");

        if (!string.IsNullOrEmpty(ddlProgramme.SelectedValue))
        {
            where.Append(" AND x.progid = @prog");
            parameters.Add(new MySqlParameter("@prog", ddlProgramme.SelectedValue));
        }

        if (!string.IsNullOrEmpty(ddlEntryYear.SelectedValue))
        {
            where.Append(" AND x.entry_year = @entry");
            parameters.Add(new MySqlParameter("@entry", ddlEntryYear.SelectedValue));
        }

        if (!string.IsNullOrEmpty(ddlAcadYear.SelectedValue))
        {
            where.Append(" AND x.current_acad_year = @ay");
            parameters.Add(new MySqlParameter("@ay", ddlAcadYear.SelectedValue));
        }

        if (!string.IsNullOrEmpty(ddlSemester.SelectedValue))
        {
            where.Append(" AND x.current_semester = @sem");
            parameters.Add(new MySqlParameter("@sem", ddlSemester.SelectedValue));
        }

        // ── Balance STATE filter — sets the DIRECTION (sign) of the balance ──
        // Sign convention: POSITIVE balance = OVERPAID (credit); NEGATIVE balance
        // = still OWING the school (debit). Zero = cleared.
        string bal = ddlBalanceState.SelectedValue;
        if (bal == "owing") where.Append(" AND x.total_balance < 0");
        else if (bal == "zero") where.Append(" AND x.total_balance = 0");
        else if (bal == "overpaid") where.Append(" AND x.total_balance > 0");

        // ── Balance AMOUNT filter — matches the SIZE (magnitude) of the balance ──
        // Users type plain positive amounts, so the comparison runs against
        // ABS(balance). That way a "Between 100 and 10,000" range correctly
        // catches BOTH a 700 debit AND a 700 credit — the sign is no longer
        // silently excluding the (majority) credit balances. Combine with the
        // Balance State filter above to restrict to a single direction.
        string balOp = ddlBalanceOp.SelectedValue;
        decimal balFrom;
        decimal balTo;
        bool hasFrom = TryParseDecimal(txtBalanceFrom.Text, out balFrom);
        bool hasTo = TryParseDecimal(txtBalanceTo.Text, out balTo);
        decimal magFrom = Math.Abs(balFrom);
        decimal magTo = Math.Abs(balTo);

        if (balOp == "eq" && hasFrom)
        {
            where.Append(" AND ABS(x.total_balance) = @balFrom");
            parameters.Add(new MySqlParameter("@balFrom", magFrom));
        }
        else if (balOp == "lt" && hasFrom)
        {
            where.Append(" AND ABS(x.total_balance) < @balFrom");
            parameters.Add(new MySqlParameter("@balFrom", magFrom));
        }
        else if (balOp == "gt" && hasFrom)
        {
            where.Append(" AND ABS(x.total_balance) > @balFrom");
            parameters.Add(new MySqlParameter("@balFrom", magFrom));
        }
        else if (balOp == "between" && hasFrom && hasTo)
        {
            decimal min = magFrom <= magTo ? magFrom : magTo;
            decimal max = magFrom <= magTo ? magTo : magFrom;
            where.Append(" AND ABS(x.total_balance) BETWEEN @balMin AND @balMax");
            parameters.Add(new MySqlParameter("@balMin", min));
            parameters.Add(new MySqlParameter("@balMax", max));
        }

        if (chkWithTransactions.Checked)
        {
            where.Append(" AND x.tx_count > 0");
        }

        string search = txtSearch.Text.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            where.Append(" AND (x.regno LIKE @q OR x.entry_no LIKE @q OR x.student_name LIKE @q OR x.programme_name LIKE @q)");
            parameters.Add(new MySqlParameter("@q", "%" + search + "%"));
        }

        return where.ToString();
    }

    /// <summary>Returns the bare column name for the current sort field.</summary>
    private string ResolveSortColumn()
    {
        string field = (hfSortField.Value ?? "").Trim().ToLower();
        switch (field)
        {
            case "student_name": return "student_name";
            case "regno": return "regno";
            case "programme": return "progid";
            case "entry_year": return "entry_year";
            case "current_year": return "current_study_year";
            case "current_sem": return "current_semester";
            case "billed": return "total_billed";
            case "paid": return "total_paid";
            case "balance": return "total_balance";
            default:
                hfSortField.Value = "balance";
                hfSortDir.Value = "ASC";
                return "total_balance";
        }
    }

    /// <summary>Returns "ASC" or "DESC" for the current sort direction.</summary>
    private string ResolveSortDir()
    {
        return (hfSortDir.Value ?? "DESC").Trim().ToUpper() == "ASC" ? "ASC" : "DESC";
    }

    /// <summary>Returns sort SQL with x. prefix for use in subquery-wrapped contexts.</summary>
    private string ResolveSortSql()
    {
        return "x." + ResolveSortColumn() + " " + ResolveSortDir() + ", x.student_name ASC";
    }

    private int ParsePageSize()
    {
        int pageSize = 50;
        int parsed;
        if (int.TryParse(ddlPageSize.SelectedValue, out parsed) && (parsed == 25 || parsed == 50 || parsed == 100 || parsed == 200))
            pageSize = parsed;
        return pageSize;
    }

    private int ParsePageIndex()
    {
        int pageIndex = 0;
        int parsed;
        if (int.TryParse(hfPageIndex.Value, out parsed) && parsed >= 0)
            pageIndex = parsed;
        return pageIndex;
    }

    private void AddParams(MySqlCommand cmd, List<MySqlParameter> source)
    {
        for (int i = 0; i < source.Count; i++)
        {
            cmd.Parameters.AddWithValue(source[i].ParameterName, source[i].Value);
        }
    }

    // ── Temp-table approach: execute the heavy base query ONCE, then read
    //    count/stats/paginated-data from the lightweight temp table. ────────
    private void LoadLedgers()
    {
        string baseSql = BuildBaseSql();
        List<MySqlParameter> filterParams = new List<MySqlParameter>();
        string where = BuildWhereClause(filterParams);
        string sortCol = ResolveSortColumn();
        string sortDir = ResolveSortDir();

        int pageSize = ParsePageSize();
        int pageIndex = ParsePageIndex();

        long totalRows = 0;
        decimal statBilled = 0;
        decimal statPaid = 0;
        decimal statBalance = 0;

        DataTable dt = new DataTable();

        using (MySqlConnection conn = new MySqlConnection(MainConnStr))
        {
            conn.Open();

            // Make sure the per-student balance cache is fresh before we read it.
            EnsureBalanceCache(conn);

            // Clean up any leftover temp table from a prior pooled-connection reuse
            using (MySqlCommand cmd = new MySqlCommand("DROP TEMPORARY TABLE IF EXISTS _sl_page", conn))
            {
                cmd.CommandTimeout = 10;
                cmd.ExecuteNonQuery();
            }

            // 1. Execute the expensive base query ONCE into a session temp table
            string createSql = String.Format(
                "CREATE TEMPORARY TABLE _sl_page ENGINE=MEMORY AS SELECT * FROM ({0}) x {1}",
                baseSql, where);
            using (MySqlCommand cmd = new MySqlCommand(createSql, conn))
            {
                cmd.CommandTimeout = 120;
                AddParams(cmd, filterParams);
                cmd.ExecuteNonQuery();
            }

            // 2. Count + aggregated stats from temp table (instant — small row set)
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT COUNT(*) AS c, COALESCE(SUM(total_billed),0) AS b, " +
                "COALESCE(SUM(total_paid),0) AS p, COALESCE(SUM(total_balance),0) AS bal " +
                "FROM _sl_page", conn))
            {
                cmd.CommandTimeout = 10;
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        totalRows = rdr["c"] != DBNull.Value ? Convert.ToInt64(rdr["c"]) : 0;
                        statBilled = rdr["b"] != DBNull.Value ? Convert.ToDecimal(rdr["b"]) : 0;
                        statPaid = rdr["p"] != DBNull.Value ? Convert.ToDecimal(rdr["p"]) : 0;
                        statBalance = rdr["bal"] != DBNull.Value ? Convert.ToDecimal(rdr["bal"]) : 0;
                    }
                }
            }

            int totalPages = totalRows > 0 ? (int)Math.Ceiling((double)totalRows / pageSize) : 1;
            if (pageIndex >= totalPages) pageIndex = totalPages - 1;
            if (pageIndex < 0) pageIndex = 0;
            hfPageIndex.Value = pageIndex.ToString();

            litStatStudents.Text = totalRows.ToString("N0");
            litStatBilled.Text = FormatMoney(statBilled);
            litStatPaid.Text = FormatMoney(statPaid);
            litStatBalance.Text = FormatBalance(statBalance);

            // 3. Paginated data from temp table (instant)
            string dataSql = String.Format(
                "SELECT * FROM _sl_page ORDER BY {0} {1}, student_name ASC LIMIT @offset, @ps",
                sortCol, sortDir);
            using (MySqlCommand cmd = new MySqlCommand(dataSql, conn))
            {
                cmd.CommandTimeout = 10;
                cmd.Parameters.AddWithValue("@offset", pageIndex * pageSize);
                cmd.Parameters.AddWithValue("@ps", pageSize);
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd))
                {
                    da.Fill(dt);
                }
            }

            // 4. Clean up temp table
            using (MySqlCommand cmd = new MySqlCommand("DROP TEMPORARY TABLE IF EXISTS _sl_page", conn))
            {
                cmd.ExecuteNonQuery();
            }

            rptLedgers.DataSource = dt;
            rptLedgers.DataBind();
            pnlEmpty.Visible = dt.Rows.Count == 0;

            long from = totalRows == 0 ? 0 : (pageIndex * pageSize) + 1;
            long to = Math.Min(totalRows, (pageIndex + 1) * pageSize);
            string statusLabel = string.IsNullOrEmpty(txtSearch.Text.Trim()) ? "active students" : "students";
            litRecordInfo.Text = String.Format("Showing {0:N0} - {1:N0} of {2:N0} {3}", from, to, totalRows, statusLabel);

            litPager.Text = BuildPagerHtml(pageIndex, pageSize, totalRows);
        }
    }

    private string BuildPagerHtml(int pageIndex, int pageSize, long totalRows)
    {
        int totalPages = totalRows > 0 ? (int)Math.Ceiling((double)totalRows / pageSize) : 1;
        if (totalPages <= 1) return "";

        StringBuilder sb = new StringBuilder();

        bool isFirst = pageIndex <= 0;
        bool isLast = pageIndex >= totalPages - 1;

        sb.AppendFormat("<button type='button' class='sl-pager__btn' onclick='goPage(0)' {0}>&laquo;</button>", isFirst ? "disabled='disabled'" : "");
        sb.AppendFormat("<button type='button' class='sl-pager__btn' onclick='goPage({0})' {1}>&lsaquo;</button>", pageIndex - 1, isFirst ? "disabled='disabled'" : "");

        int start = Math.Max(0, pageIndex - 3);
        int end = Math.Min(totalPages - 1, pageIndex + 3);

        if (start > 0) sb.Append("<span class='sl-pager__btn'>...</span>");

        for (int i = start; i <= end; i++)
        {
            string cls = i == pageIndex ? "sl-pager__btn sl-pager__btn--active" : "sl-pager__btn";
            sb.AppendFormat("<button type='button' class='{0}' onclick='goPage({1})'>{2}</button>", cls, i, i + 1);
        }

        if (end < totalPages - 1) sb.Append("<span class='sl-pager__btn'>...</span>");

        sb.AppendFormat("<button type='button' class='sl-pager__btn' onclick='goPage({0})' {1}>&rsaquo;</button>", pageIndex + 1, isLast ? "disabled='disabled'" : "");
        sb.AppendFormat("<button type='button' class='sl-pager__btn' onclick='goPage({0})' {1}>&raquo;</button>", totalPages - 1, isLast ? "disabled='disabled'" : "");

        return sb.ToString();
    }

    private void ExportData(string format)
    {
        string baseSql = BuildBaseSql();
        List<MySqlParameter> filterParams = new List<MySqlParameter>();
        string where = BuildWhereClause(filterParams);
        string sortCol = ResolveSortColumn();
        string sortDir = ResolveSortDir();

        string sql = String.Format(
            "SELECT * FROM ({0}) x {1} ORDER BY x.{2} {3}, x.student_name ASC",
            baseSql, where, sortCol, sortDir);

        DataTable dt = new DataTable();
        using (MySqlConnection conn = new MySqlConnection(MainConnStr))
        {
            conn.Open();

            // Make sure the per-student balance cache is fresh before we read it.
            EnsureBalanceCache(conn);

            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.CommandTimeout = 180;
                AddParams(cmd, filterParams);
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd))
                {
                    da.Fill(dt);
                }
            }
        }

        if (format == "excel")
        {
            Response.Clear();
            Response.ContentType = "application/vnd.ms-excel";
            Response.AddHeader("Content-Disposition", "attachment;filename=StudentLedgers_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xls");
            Response.ContentEncoding = Encoding.UTF8;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Student Name\tReg No\tProgramme\tEntry Year\tCurrent Year\tSemester\tTotal Billed\tTotal Paid\tBalance");
            foreach (DataRow r in dt.Rows)
            {
                sb.AppendFormat("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\r\n",
                    TsvSafe(r["student_name"]),
                    TsvSafe(r["regno"]),
                    TsvSafe(r["progid"]),
                    TsvSafe(r["entry_year"]),
                    TsvSafe(r["current_study_year"]),
                    TsvSafe(r["current_semester"]),
                    TsvSafe(r["total_billed"]),
                    TsvSafe(r["total_paid"]),
                    TsvSafe(r["total_balance"]));
            }

            Response.Write(sb.ToString());
        }
        else
        {
            Response.Clear();
            Response.ContentType = "text/csv";
            Response.AddHeader("Content-Disposition", "attachment;filename=StudentLedgers_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
            Response.ContentEncoding = Encoding.UTF8;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Student Name,Reg No,Programme,Entry Year,Current Year,Semester,Total Billed,Total Paid,Balance");
            foreach (DataRow r in dt.Rows)
            {
                sb.AppendFormat("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",{6},{7},{8}\r\n",
                    CsvSafe(r["student_name"]),
                    CsvSafe(r["regno"]),
                    CsvSafe(r["progid"]),
                    CsvSafe(r["entry_year"]),
                    CsvSafe(r["current_study_year"]),
                    CsvSafe(r["current_semester"]),
                    CsvSafe(r["total_billed"]),
                    CsvSafe(r["total_paid"]),
                    CsvSafe(r["total_balance"]));
            }

            Response.Write(sb.ToString());
        }

        try { Response.End(); }
        catch (System.Threading.ThreadAbortException) { }
    }

    private string CsvSafe(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        return val.ToString().Replace("\"", "\"\"");
    }

    private string TsvSafe(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        return val.ToString().Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
    }

    private void ShowToast(string message, bool ok)
    {
        pnlToast.Visible = true;
        pnlToast.CssClass = ok ? "sl-toast sl-toast--show sl-toast--ok" : "sl-toast sl-toast--show sl-toast--err";
        litToast.Text = Server.HtmlEncode(message);
    }

    private void AjaxLedgerDetails()
    {
        string regno = (Request.QueryString["regno"] ?? "").Trim();
        if (string.IsNullOrEmpty(regno))
        {
            Response.Write("{\"ok\":false,\"error\":\"Missing regno.\"}");
            return;
        }

        string studentName = "";
        decimal billed = 0, paid = 0;
        List<LedgerLine> lines = new List<LedgerLine>();
        List<decimal> balances = new List<decimal>();
        List<string> rows = new List<string>();

        using (MySqlConnection conn = new MySqlConnection(MainConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand("SELECT TRIM(CONCAT(COALESCE(firstname,''),' ',COALESCE(othername,''))) FROM acad_student WHERE regno=@r LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                object v = cmd.ExecuteScalar();
                studentName = v != null && v != DBNull.Value ? v.ToString() : "";
            }
        }

        using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();

            const string sql =
                "SELECT " +
                "  fl.voucherNo AS voucherNo," +
                "  DATE_FORMAT(fl.transactionDate,'%d/%m/%Y') AS formated_date," +
                "  DATE_FORMAT(fl.transactionDate,'%Y-%m-%d') AS sort_date," +
                "  fl.particulars AS particulars," +
                "  CASE WHEN fl.transactionType='DR' THEN fl.transaction_amount ELSE 0 END AS dr_amount," +
                "  CASE WHEN fl.transactionType='CR' THEN fl.transaction_amount ELSE 0 END AS cr_amount " +
                "FROM fin_ledger fl " +
                "WHERE fl.accountcode = @reg " +
                "  AND fl.transaction_amount > 0 " +
                "UNION ALL " +
                "SELECT " +
                "  CAST(t.TID AS CHAR) AS voucherNo," +
                "  DATE_FORMAT(t.trans_date,'%d/%m/%Y') AS formated_date," +
                "  DATE_FORMAT(t.trans_date,'%Y-%m-%d') AS sort_date," +
                "  t.detail AS particulars," +
                "  CASE WHEN t.trans_type='Bill' THEN t.amount ELSE 0 END AS dr_amount," +
                "  CASE WHEN t.trans_type='Payment' THEN t.amount ELSE 0 END AS cr_amount " +
                "FROM fin_studentfeestracking t " +
                "WHERE t.regno = @reg " +
                "  AND t.post_status = 'Posted' " +
                "  AND NOT EXISTS (" +
                "    SELECT 1 FROM fin_ledger fl2 " +
                "    WHERE fl2.accountcode = t.regno " +
                "      AND (" +
                "        fl2.voucherNo = CAST(t.TID AS CHAR)" +
                "        OR fl2.folio = CONCAT('BillNo:', CAST(t.TID AS CHAR))" +
                "        OR (" +
                "          fl2.transaction_amount = t.amount" +
                "          AND DATE(fl2.transactionDate) = DATE(t.trans_date)" +
                "          AND fl2.transactionType = CASE WHEN t.trans_type='Payment' THEN 'CR' ELSE 'DR' END" +
                "          AND (t.trans_type = 'Payment' OR fl2.particulars = t.detail OR t.detail IS NULL OR t.detail = '')" +
                "        )" +
                "      )" +
                "  ) " +
                "ORDER BY sort_date ASC, voucherNo ASC";

            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.CommandTimeout = 60;
                cmd.Parameters.AddWithValue("@reg", regno);
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        LedgerLine line = new LedgerLine();
                        line.Ref = rdr["voucherNo"] != DBNull.Value ? rdr["voucherNo"].ToString() : "";
                        line.Date = rdr["formated_date"] != DBNull.Value ? rdr["formated_date"].ToString() : "";
                        line.Particulars = rdr["particulars"] != DBNull.Value ? rdr["particulars"].ToString() : "";
                        line.Debit = rdr["dr_amount"] != DBNull.Value ? Convert.ToDecimal(rdr["dr_amount"]) : 0;
                        line.Credit = rdr["cr_amount"] != DBNull.Value ? Convert.ToDecimal(rdr["cr_amount"]) : 0;
                        billed += line.Debit;
                        paid += line.Credit;
                        lines.Add(line);
                    }
                }
            }
        }

        // Running balance follows the institution convention: payments (credit)
        // increase the balance, bills (debit) decrease it. So a POSITIVE running
        // balance = overpaid (credit), NEGATIVE = still owing (debit).
        decimal runningBal = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            runningBal += lines[i].Credit - lines[i].Debit;
            balances.Add(runningBal);
        }

        for (int i = lines.Count - 1; i >= 0; i--)
        {
            LedgerLine line = lines[i];
            decimal lineBal = balances[i];
            string balStr = FormatBalance(lineBal);
            int displayNum = lines.Count - i;

            rows.Add(string.Format(
                "{{\"idx\":{0},\"date\":\"{1}\",\"ref\":\"{2}\",\"particulars\":\"{3}\",\"debit\":{4},\"credit\":{5},\"balance\":\"{6}\"}}",
                displayNum,
                JsEsc(line.Date),
                JsEsc(line.Ref),
                JsEsc(line.Particulars),
                line.Debit.ToString("F0"),
                line.Credit.ToString("F0"),
                JsEsc(balStr)));
        }

        decimal netBalance = paid - billed; // + overpaid (credit), - owing (debit)
        Response.Write(string.Format("{{\"ok\":true,\"regno\":\"{0}\",\"student_name\":\"{1}\",\"total_billed\":{2},\"total_paid\":{3},\"total_balance\":{4},\"rows\":[{5}]}}",
            JsEsc(regno), JsEsc(studentName), billed.ToString("F0"), paid.ToString("F0"), netBalance.ToString("F0"), string.Join(",", rows.ToArray())));
    }

    private void AjaxFixBilling()
    {
        AjaxFixBillingApply();
    }

    // =====================================================================
    //  FIX BILLING V2 – Comprehensive bill repair
    //  Scenarios handled:
    //    A) Unbilled semesters – registered but no tracking Bill entries
    //    B) Missing DR mirrors – tracking Bill exists, no ledger DR
    //    C) Missing income CR – ledger DR exists, no income account CR
    //    D) Metadata alignment – non-canonical folio / tracking_ref
    //    E) Account-type normalisation
    // =====================================================================

    private void AjaxFixBillingScan()
    {
        string regno = (Request.QueryString["regno"] ?? "").Trim();
        if (string.IsNullOrEmpty(regno))
        {
            Response.Write("{\"ok\":false,\"error\":\"Missing regno.\"}");
            return;
        }

        FixPlan plan;
        using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();
            plan = BuildFixPlan(conn, null, regno);
        }

        List<string> sj = new List<string>();
        foreach (FixSample s in plan.Samples)
        {
            sj.Add(string.Format(
                "{{\"cat\":\"{0}\",\"period\":\"{1}\",\"feeType\":\"{2}\",\"amount\":{3},\"detail\":\"{4}\"}}",
                JsEsc(s.Category), JsEsc(s.Period), JsEsc(s.FeeType),
                s.Amount.ToString("F0"), JsEsc(s.Detail)));
        }

        bool canApply = plan.UnbilledCount > 0 || plan.MissingDrCount > 0 ||
                        plan.MissingCrCount > 0 || plan.AlignCount > 0 || plan.NormaliseCount > 0;

        Response.Write(string.Format(
            "{{\"ok\":true,\"regno\":\"{0}\"," +
            "\"unbilled_count\":{1},\"unbilled_amount\":{2}," +
            "\"missing_dr_count\":{3},\"missing_dr_amount\":{4}," +
            "\"missing_cr_count\":{5},\"missing_cr_amount\":{6}," +
            "\"align_count\":{7},\"normalise_count\":{8}," +
            "\"can_apply\":{9},\"samples\":[{10}]}}",
            JsEsc(regno),
            plan.UnbilledCount, plan.UnbilledAmount.ToString("F0"),
            plan.MissingDrCount, plan.MissingDrAmount.ToString("F0"),
            plan.MissingCrCount, plan.MissingCrAmount.ToString("F0"),
            plan.AlignCount, plan.NormaliseCount,
            canApply ? "true" : "false",
            string.Join(",", sj.ToArray())));
    }

    private void AjaxFixBillingApply()
    {
        string regno = (Request.QueryString["regno"] ?? "").Trim();
        if (string.IsNullOrEmpty(regno))
        {
            Response.Write("{\"ok\":false,\"error\":\"Missing regno.\"}");
            return;
        }

        int insertedUnbilled = 0;
        int insertedDr = 0;
        int insertedCr = 0;
        int aligned = 0;
        int normalised = 0;
        decimal beforeBalance = 0;
        decimal afterBalance = 0;

        using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();
            beforeBalance = ComputeStudentBalance(conn, regno);

            using (MySqlTransaction tx = conn.BeginTransaction())
            {
                try
                {
                    // Phase A – Create bills for unbilled semesters
                    List<UnbilledSem> unbilled = GetUnbilledSemesters(conn, tx, regno);
                    foreach (UnbilledSem us in unbilled)
                    {
                        if (!us.HasTuition && us.Tuition > 0)
                            insertedUnbilled += CreateBillEntries(conn, tx, regno, us, 1, "Tuition Fees", "AC6006", us.Tuition);
                        if (!us.HasFunctional && us.Functional > 0)
                            insertedUnbilled += CreateBillEntries(conn, tx, regno, us, 52, "Function Fees", "AC6007", us.Functional);
                    }

                    // Phase B – Insert missing DR ledger mirrors for existing tracking
                    using (MySqlCommand cmd = new MySqlCommand(GetFixBillingInsertSql(), conn, tx))
                    {
                        cmd.CommandTimeout = 180;
                        cmd.Parameters.AddWithValue("@reg", regno);
                        insertedDr = cmd.ExecuteNonQuery();
                    }

                    // Phase C – Insert missing income-account CR mirrors
                    insertedCr = InsertMissingIncomeCr(conn, tx, regno);

                    // Phase D – Align metadata to canonical billing format
                    using (MySqlCommand cmd = new MySqlCommand(GetFixBillingAlignSql(), conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@reg", regno);
                        aligned = cmd.ExecuteNonQuery();
                    }

                    // Phase E – Normalise account_type
                    using (MySqlCommand cmd = new MySqlCommand(GetFixBillingNormaliseSql(), conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@reg", regno);
                        normalised = cmd.ExecuteNonQuery();
                    }

                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }

            afterBalance = ComputeStudentBalance(conn, regno);
        }

        // Keep registration status consistent with billing: any semester we just billed
        // that was still UNREGISTERED is promoted to REGISTERED, so the fix never leaves a
        // student billed-but-unregistered (best-effort; billing has already committed).
        if (insertedUnbilled > 0)
            PromoteBilledUnregistered(regno);

        Response.Write(string.Format(
            "{{\"ok\":true," +
            "\"inserted_unbilled\":{0},\"inserted_dr\":{1},\"inserted_cr\":{2}," +
            "\"aligned\":{3},\"normalised\":{4}," +
            "\"before_balance\":\"{5}\",\"after_balance\":\"{6}\"}}",
            insertedUnbilled, insertedDr, insertedCr,
            aligned, normalised,
            JsEsc(FormatBalance(beforeBalance)),
            JsEsc(FormatBalance(afterBalance))));
    }

    /// <summary>
    /// After Fix Billing creates bills for a student, promote any of their UNREGISTERED
    /// semesters that now carry a Bill to REGISTERED — so a billed student is never left
    /// showing as unregistered. Best-effort and idempotent; runs on the main DB.
    /// </summary>
    private void PromoteBilledUnregistered(string regno)
    {
        try
        {
            string actor = (Session["username"] != null && Session["username"].ToString().Trim() != "")
                ? Session["username"].ToString().Trim() : "FixBilling";

            using (MySqlConnection conn = new MySqlConnection(MainConnStr))
            {
                conn.Open();
                string sql = @"
                    UPDATE acad_registration r
                    SET r.regstatus = 'REGISTERED',
                        r.examClearance = CASE WHEN IFNULL(TRIM(r.examClearance),'') = '' THEN 'UNCLEARED' ELSE r.examClearance END,
                        r.registeredBy  = CASE WHEN IFNULL(TRIM(r.registeredBy),'')  = '' THEN @actor ELSE r.registeredBy END
                    WHERE r.regno = @reg
                      AND UPPER(TRIM(IFNULL(r.regstatus,''))) = 'UNREGISTERED'
                      AND EXISTS (
                          SELECT 1 FROM campus_dynamics_accounts.fin_studentfeestracking t
                          WHERE t.regno = r.regno AND t.acadyear = r.acad_year
                            AND t.semester = r.semester AND t.trans_type = 'Bill'
                      )";
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.CommandTimeout = 60;
                    cmd.Parameters.AddWithValue("@reg", regno);
                    cmd.Parameters.AddWithValue("@actor", actor);
                    cmd.ExecuteNonQuery();
                }
            }
        }
        catch { /* non-fatal: billing already succeeded; status promotion is best-effort */ }
    }

    // ---- Detection ----

    private FixPlan BuildFixPlan(MySqlConnection conn, MySqlTransaction tx, string regno)
    {
        FixPlan plan = new FixPlan();

        // A – Unbilled semesters (missing tracking entries)
        List<UnbilledSem> unbilled = GetUnbilledSemesters(conn, tx, regno);
        foreach (UnbilledSem us in unbilled)
        {
            if (!us.HasTuition && us.Tuition > 0)
            {
                plan.UnbilledCount++;
                plan.UnbilledAmount += us.Tuition;
                plan.Samples.Add(new FixSample
                {
                    Category = "unbilled",
                    Period = us.AcadYear + " Sem " + us.Semester,
                    FeeType = "Tuition",
                    Amount = us.Tuition,
                    Detail = "No bill exists (Year " + us.StudyYear + ")"
                });
            }
            if (!us.HasFunctional && us.Functional > 0)
            {
                plan.UnbilledCount++;
                plan.UnbilledAmount += us.Functional;
                plan.Samples.Add(new FixSample
                {
                    Category = "unbilled",
                    Period = us.AcadYear + " Sem " + us.Semester,
                    FeeType = "Functional",
                    Amount = us.Functional,
                    Detail = "No bill exists (Year " + us.StudyYear + ")"
                });
            }
        }

        // B – Missing DR ledger mirrors
        string srcSql = GetFixBillingSourceSql();
        string countDrSql = "SELECT COUNT(*) AS c, COALESCE(SUM(src.amount),0) AS amt FROM (" + srcSql + ") src";
        using (MySqlCommand cmd = tx == null ? new MySqlCommand(countDrSql, conn) : new MySqlCommand(countDrSql, conn, tx))
        {
            cmd.Parameters.AddWithValue("@reg", regno);
            using (MySqlDataReader rdr = cmd.ExecuteReader())
            {
                if (rdr.Read())
                {
                    plan.MissingDrCount = rdr["c"] != DBNull.Value ? Convert.ToInt32(rdr["c"]) : 0;
                    plan.MissingDrAmount = rdr["amt"] != DBNull.Value ? Convert.ToDecimal(rdr["amt"]) : 0;
                }
            }
        }

        string sampleDrSql = "SELECT src.voucherNo, DATE_FORMAT(src.transactionDate,'%Y-%m-%d') AS dt, src.amount, src.particulars " +
                              "FROM (" + srcSql + ") src ORDER BY src.transactionDate DESC LIMIT 5";
        using (MySqlCommand cmd = tx == null ? new MySqlCommand(sampleDrSql, conn) : new MySqlCommand(sampleDrSql, conn, tx))
        {
            cmd.Parameters.AddWithValue("@reg", regno);
            using (MySqlDataReader rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    plan.Samples.Add(new FixSample
                    {
                        Category = "missing_dr",
                        Period = rdr["dt"] != DBNull.Value ? rdr["dt"].ToString() : "",
                        FeeType = "",
                        Amount = rdr["amount"] != DBNull.Value ? Convert.ToDecimal(rdr["amount"]) : 0,
                        Detail = rdr["particulars"] != DBNull.Value ? rdr["particulars"].ToString() : ""
                    });
                }
            }
        }

        // C – Missing income-account CR mirrors
        string crSql = GetMissingCrSourceSql();
        string countCrSql = "SELECT COUNT(*) AS c, COALESCE(SUM(src.amount),0) AS amt FROM (" + crSql + ") src";
        using (MySqlCommand cmd = tx == null ? new MySqlCommand(countCrSql, conn) : new MySqlCommand(countCrSql, conn, tx))
        {
            cmd.Parameters.AddWithValue("@reg", regno);
            using (MySqlDataReader rdr = cmd.ExecuteReader())
            {
                if (rdr.Read())
                {
                    plan.MissingCrCount = rdr["c"] != DBNull.Value ? Convert.ToInt32(rdr["c"]) : 0;
                    plan.MissingCrAmount = rdr["amt"] != DBNull.Value ? Convert.ToDecimal(rdr["amt"]) : 0;
                }
            }
        }

        // D – Align count
        string alignCountSql =
            "SELECT COUNT(*) FROM fin_ledger l " +
            "INNER JOIN fin_studentfeestracking t ON t.regno=l.accountcode AND t.TID=l.voucherNo AND t.trans_type='Bill' AND t.post_status='Posted' " +
            "WHERE l.accountcode=@reg AND l.account_type='Student' AND l.transactionType='DR' " +
            "AND (l.source_system IS NULL OR l.source_system='' OR l.source_system='Manual' OR l.teller='LedgerFix' OR l.folio=l.accountcode OR l.folio NOT LIKE 'BillNo:%' OR l.tracking_ref IS NULL)";
        using (MySqlCommand cmd = tx == null ? new MySqlCommand(alignCountSql, conn) : new MySqlCommand(alignCountSql, conn, tx))
        {
            cmd.Parameters.AddWithValue("@reg", regno);
            object v = cmd.ExecuteScalar();
            plan.AlignCount = v != null && v != DBNull.Value ? Convert.ToInt32(v) : 0;
        }

        // E – Normalise count
        string normCountSql = "SELECT COUNT(*) FROM fin_ledger WHERE accountcode=@reg AND account_type NOT IN ('Student','Chart Account')";
        using (MySqlCommand cmd = tx == null ? new MySqlCommand(normCountSql, conn) : new MySqlCommand(normCountSql, conn, tx))
        {
            cmd.Parameters.AddWithValue("@reg", regno);
            object v = cmd.ExecuteScalar();
            plan.NormaliseCount = v != null && v != DBNull.Value ? Convert.ToInt32(v) : 0;
        }

        return plan;
    }

    // ---- Unbilled semesters ----

    private List<UnbilledSem> GetUnbilledSemesters(MySqlConnection conn, MySqlTransaction tx, string regno)
    {
        List<UnbilledSem> result = new List<UnbilledSem>();

        // Find registered semesters missing tuition and/or functional tracking
        string sql = @"
            SELECT sub.* FROM (
                SELECT r.acad_year, r.semester, r.studyyear, s.progid,
                       TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) AS student_name,
                       (SELECT COUNT(*) FROM fin_studentfeestracking t
                        WHERE t.regno = r.regno AND t.acadyear = r.acad_year
                          AND t.semester = r.semester AND t.trans_type = 'Bill' AND t.item_code = 1) AS has_tuition,
                       (SELECT COUNT(*) FROM fin_studentfeestracking t
                        WHERE t.regno = r.regno AND t.acadyear = r.acad_year
                          AND t.semester = r.semester AND t.trans_type = 'Bill' AND t.item_code = 52) AS has_functional
                FROM campus_dynamics.acad_registration r
                INNER JOIN campus_dynamics.acad_student s ON s.regno = r.regno
                WHERE r.regno = @reg
                  -- A registration instance is billable whether or not the student has
                  -- completed self-registration. Admin-enrolled students sit UNREGISTERED
                  -- but still owe fees for that semester, so detect those too — only the
                  -- terminal statuses (and graduated alumni) are genuinely non-billable.
                  AND UPPER(TRIM(IFNULL(r.regstatus,''))) NOT IN ('DISCONTINUED','HALTED','DEAD YEAR')
                  AND UPPER(TRIM(IFNULL(s.new_status,''))) <> 'ALUMNI'
            ) sub
            WHERE sub.has_tuition = 0 OR sub.has_functional = 0
            ORDER BY sub.acad_year, sub.semester";

        List<UnbilledSem> raw = new List<UnbilledSem>();
        using (MySqlCommand cmd = tx == null ? new MySqlCommand(sql, conn) : new MySqlCommand(sql, conn, tx))
        {
            cmd.Parameters.AddWithValue("@reg", regno);
            using (MySqlDataReader rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    raw.Add(new UnbilledSem
                    {
                        AcadYear = rdr["acad_year"] != DBNull.Value ? rdr["acad_year"].ToString() : "",
                        Semester = rdr["semester"] != DBNull.Value ? Convert.ToInt32(rdr["semester"]) : 0,
                        StudyYear = rdr["studyyear"] != DBNull.Value ? Convert.ToInt32(rdr["studyyear"]) : 0,
                        ProgCode = rdr["progid"] != DBNull.Value ? rdr["progid"].ToString() : "",
                        StudentName = rdr["student_name"] != DBNull.Value ? rdr["student_name"].ToString() : "",
                        HasTuition = Convert.ToInt32(rdr["has_tuition"]) > 0,
                        HasFunctional = Convert.ToInt32(rdr["has_functional"]) > 0
                    });
                }
            }
        }

        // Look up fee amounts from fee structure for each unbilled semester
        foreach (UnbilledSem us in raw)
        {
            if (string.IsNullOrEmpty(us.ProgCode) || us.StudyYear < 1 || us.StudyYear > 3 || us.Semester < 1 || us.Semester > 3)
                continue;

            string colT = "y" + us.StudyYear + "_s" + us.Semester + "_tuition";
            string colF = "y" + us.StudyYear + "_s" + us.Semester + "_functional";

            // Resolved by this student's own study session (fin_ProgFeeRow). A programme may
            // hold both a MAIN and an INSERVICE structure, and "WHERE progcode=X LIMIT 1"
            // would pick an arbitrary one — quoting an unbilled semester at the wrong rate.
            string feeSql = "SELECT COALESCE(" + colT + ",0) AS tuition, COALESCE(" + colF + ",0) AS functional " +
                            "FROM fin_programme_fees " +
                            "WHERE ID = fin_ProgFeeRow(@prog, (SELECT s.studsesion " +
                            "                                  FROM campus_dynamics.acad_student s " +
                            "                                  WHERE s.regno=@feeReg LIMIT 1))";

            using (MySqlCommand cmd = tx == null ? new MySqlCommand(feeSql, conn) : new MySqlCommand(feeSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@prog", us.ProgCode);
                cmd.Parameters.AddWithValue("@feeReg", regno);
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        us.Tuition = rdr["tuition"] != DBNull.Value ? Convert.ToDecimal(rdr["tuition"]) : 0;
                        us.Functional = rdr["functional"] != DBNull.Value ? Convert.ToDecimal(rdr["functional"]) : 0;
                    }
                }
            }

            if ((!us.HasTuition && us.Tuition > 0) || (!us.HasFunctional && us.Functional > 0))
                result.Add(us);
        }

        return result;
    }

    /// <summary>Creates one complete bill entry set: tracking + ledger DR + ledger CR. Returns rows inserted (3 on success).</summary>
    private int CreateBillEntries(MySqlConnection conn, MySqlTransaction tx, string regno,
        UnbilledSem us, int itemCode, string itemName, string glAccount, decimal amount)
    {
        int rows = 0;

        // Guard: check fin_ledger for an existing student DR before inserting anything.
        // GetUnbilledSemesters already confirmed no tracking row exists, but a DR may still
        // live in fin_ledger as an orphan (e.g. tracking deleted manually after a prior billing run).
        // Inserting here without this check would create a duplicate DR → double billing.
        // Pattern covers both the fix-tool format ("for Semester:") and the SP format ("for Term:")
        string partLike = itemName + " for %:" + us.Semester + ", " + us.AcadYear + "%";
        string chkDrSql =
            "SELECT COUNT(*) FROM fin_ledger l " +
            "WHERE l.accountcode = @reg " +
            "  AND l.account_type = 'Student' " +
            "  AND l.transactionType = 'DR' " +
            "  AND (" +
            "    l.particulars LIKE @pl " +
            "    OR (l.tracking_ref IS NOT NULL AND l.tracking_ref IN (" +
            "      SELECT TID FROM fin_studentfeestracking " +
            "      WHERE regno = @reg AND item_code = @itemCode AND acadyear = @acad AND semester = @sem" +
            "    ))" +
            "  )";
        long existingDr;
        using (MySqlCommand chk = new MySqlCommand(chkDrSql, conn, tx))
        {
            chk.Parameters.AddWithValue("@reg", regno);
            chk.Parameters.AddWithValue("@pl", partLike);
            chk.Parameters.AddWithValue("@itemCode", itemCode);
            chk.Parameters.AddWithValue("@acad", us.AcadYear);
            chk.Parameters.AddWithValue("@sem", us.Semester);
            existingDr = Convert.ToInt64(chk.ExecuteScalar());
        }
        if (existingDr > 0)
            return 0; // ledger DR already exists — skip to prevent double billing

        // 1. Insert tracking entry
        string detail = itemName + " Sem :" + us.Semester + ", " + us.AcadYear + ": " + regno + " [FIX]";
        string insTrk =
            "INSERT INTO fin_studentfeestracking " +
            "(regno, amount, item_code, trans_type, post_status, acadyear, semester, trans_date, detail) " +
            "VALUES (@reg, @amt, @item, 'Bill', 'Pending', @acad, @sem, NOW(), @det)";
        long tid;
        using (MySqlCommand cmd = new MySqlCommand(insTrk, conn, tx))
        {
            cmd.Parameters.AddWithValue("@reg", regno);
            cmd.Parameters.AddWithValue("@amt", amount);
            cmd.Parameters.AddWithValue("@item", itemCode);
            cmd.Parameters.AddWithValue("@acad", us.AcadYear);
            cmd.Parameters.AddWithValue("@sem", us.Semester);
            cmd.Parameters.AddWithValue("@det", detail);
            rows += cmd.ExecuteNonQuery();
        }
        using (MySqlCommand cmd = new MySqlCommand("SELECT LAST_INSERT_ID()", conn, tx))
        {
            tid = Convert.ToInt64(cmd.ExecuteScalar());
        }

        // 2. Insert ledger DR (student debit)
        string particulars = itemName + " for Semester:" + us.Semester + ", " + us.AcadYear;
        string folio = "BillNo:" + tid;
        string insDr =
            "INSERT INTO fin_ledger " +
            "(accountcode, account_type, transactionType, transaction_amount, particulars, " +
            " voucherNo, transactionDate, teller, timeLog, folio, tracking_ref, source_system, " +
            " journal_no, trans_currency, actual_amount, curr_balance, forex_rate, ugx_amount) " +
            "VALUES (@acct, 'Student', 'DR', @amt, @part, " +
            "        @tid, NOW(), @reg, NOW(), @fol, @tid, 'Billing', " +
            "        '-', 'UGX', @amt, 0, 1, @amt)";
        using (MySqlCommand cmd = new MySqlCommand(insDr, conn, tx))
        {
            cmd.Parameters.AddWithValue("@acct", regno);
            cmd.Parameters.AddWithValue("@amt", amount);
            cmd.Parameters.AddWithValue("@part", particulars);
            cmd.Parameters.AddWithValue("@tid", tid);
            cmd.Parameters.AddWithValue("@reg", regno);
            cmd.Parameters.AddWithValue("@fol", folio);
            rows += cmd.ExecuteNonQuery();
        }

        // 3. Insert ledger CR (income account credit)
        string crPart = itemName + " Receivable from " + regno + " (" + us.StudentName + ") for Semester:" + us.Semester + ", " + us.AcadYear;
        string insCr =
            "INSERT INTO fin_ledger " +
            "(accountcode, account_type, transactionType, transaction_amount, particulars, " +
            " voucherNo, transactionDate, teller, timeLog, folio, tracking_ref, source_system, " +
            " journal_no, trans_currency, actual_amount, curr_balance, forex_rate, ugx_amount) " +
            "VALUES (@gl, 'Chart Account', 'CR', @amt, @part, " +
            "        @tid, NOW(), @reg, NOW(), @fol, @tid, 'Billing', " +
            "        '-', 'UGX', @amt, 0, 1, @amt)";
        using (MySqlCommand cmd = new MySqlCommand(insCr, conn, tx))
        {
            cmd.Parameters.AddWithValue("@gl", glAccount);
            cmd.Parameters.AddWithValue("@amt", amount);
            cmd.Parameters.AddWithValue("@part", crPart);
            cmd.Parameters.AddWithValue("@tid", tid);
            cmd.Parameters.AddWithValue("@reg", regno);
            cmd.Parameters.AddWithValue("@fol", folio);
            rows += cmd.ExecuteNonQuery();
        }

        // 4. Mark tracking as Posted
        using (MySqlCommand cmd = new MySqlCommand("UPDATE fin_studentfeestracking SET post_status='Posted' WHERE TID=@tid", conn, tx))
        {
            cmd.Parameters.AddWithValue("@tid", tid);
            cmd.ExecuteNonQuery();
        }

        return rows;
    }

    // ---- Missing income CR ----

    private string GetMissingCrSourceSql()
    {
        return
            "SELECT l.TID AS ledger_tid, l.transaction_amount AS amount, l.transactionDate, " +
            "  l.folio, l.tracking_ref, t.item_code, " +
            "  CASE WHEN t.item_code = 1 THEN 'AC6006' WHEN t.item_code = 52 THEN 'AC6007' ELSE NULL END AS income_gl, " +
            "  CASE WHEN t.item_code = 1 THEN 'Tuition Fees' WHEN t.item_code = 52 THEN 'Function Fees' ELSE 'Unknown' END AS item_name, " +
            "  l.particulars, t.regno " +
            "FROM fin_ledger l " +
            "INNER JOIN fin_studentfeestracking t ON t.TID = l.tracking_ref AND t.trans_type = 'Bill' AND t.post_status='Posted' " +
            "WHERE l.accountcode = @reg " +
            "  AND l.account_type = 'Student' " +
            "  AND l.transactionType = 'DR' " +
            "  AND l.tracking_ref IS NOT NULL " +
            "  AND NOT EXISTS ( " +
            "      SELECT 1 FROM fin_ledger cr " +
            "      WHERE cr.tracking_ref = l.tracking_ref " +
            "        AND cr.account_type = 'Chart Account' " +
            "        AND cr.transactionType = 'CR' " +
            "  )";
    }

    private int InsertMissingIncomeCr(MySqlConnection conn, MySqlTransaction tx, string regno)
    {
        List<MissingCrEntry> entries = new List<MissingCrEntry>();
        string sql = GetMissingCrSourceSql();
        using (MySqlCommand cmd = new MySqlCommand(sql, conn, tx))
        {
            cmd.Parameters.AddWithValue("@reg", regno);
            using (MySqlDataReader rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    string gl = rdr["income_gl"] != DBNull.Value ? rdr["income_gl"].ToString() : null;
                    if (string.IsNullOrEmpty(gl)) continue;
                    entries.Add(new MissingCrEntry
                    {
                        TrackingRef = Convert.ToInt64(rdr["tracking_ref"]),
                        Amount = rdr["amount"] != DBNull.Value ? Convert.ToDecimal(rdr["amount"]) : 0,
                        IncomeGl = gl,
                        ItemName = rdr["item_name"] != DBNull.Value ? rdr["item_name"].ToString() : "",
                        Folio = rdr["folio"] != DBNull.Value ? rdr["folio"].ToString() : "",
                        TransDate = rdr["transactionDate"] != DBNull.Value ? Convert.ToDateTime(rdr["transactionDate"]) : DateTime.Now
                    });
                }
            }
        }

        int inserted = 0;
        foreach (MissingCrEntry e in entries)
        {
            string crPart = e.ItemName + " Receivable from " + regno + " " + e.Folio;
            // INSERT IGNORE: uq_billing_entry UNIQUE(tracking_ref, accountcode, transactionType)
            // silently skips if this CR already exists, preventing duplicates on concurrent runs.
            string insSql =
                "INSERT IGNORE INTO fin_ledger " +
                "(accountcode, account_type, transactionType, transaction_amount, particulars, " +
                " voucherNo, transactionDate, teller, timeLog, folio, tracking_ref, source_system, " +
                " journal_no, trans_currency, actual_amount, curr_balance, forex_rate, ugx_amount) " +
                "VALUES (@gl, 'Chart Account', 'CR', @amt, @part, " +
                "        @tid, @dt, @reg, NOW(), @fol, @tid, 'Billing', " +
                "        '-', 'UGX', @amt, 0, 1, @amt)";
            using (MySqlCommand cmd = new MySqlCommand(insSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@gl", e.IncomeGl);
                cmd.Parameters.AddWithValue("@amt", e.Amount);
                cmd.Parameters.AddWithValue("@part", crPart);
                cmd.Parameters.AddWithValue("@tid", e.TrackingRef);
                cmd.Parameters.AddWithValue("@dt", e.TransDate);
                cmd.Parameters.AddWithValue("@reg", regno);
                cmd.Parameters.AddWithValue("@fol", e.Folio);
                inserted += cmd.ExecuteNonQuery();
            }
        }

        return inserted;
    }

    // ---- SQL helpers (existing, unchanged) ----

    private string GetFixBillingSourceSql()
    {
        return
            "SELECT " +
            "  fst.regno AS regno, " +
            "  'DR' AS transactionType, " +
            "  fst.amount AS amount, " +
            "  IFNULL(fst.detail, CONCAT(fst.trans_type, ' TID ', fst.TID)) AS particulars, " +
            "  fst.TID AS voucherNo, " +
            "  fst.trans_date AS transactionDate, " +
            "  fst.regno AS folio " +
            "FROM fin_studentfeestracking fst " +
            "WHERE fst.regno = @reg AND fst.post_status='Posted' AND fst.trans_type='Bill' AND fst.amount > 0 " +
            "AND NOT EXISTS ( " +
            "  SELECT 1 FROM fin_ledger fl " +
            "  WHERE fl.accountcode = fst.regno " +
            "  AND fl.transaction_amount = fst.amount " +
            "  AND DATE(fl.transactionDate) = DATE(fst.trans_date) " +
            "  AND fl.transactionType = 'DR' " +
            "  AND (fl.particulars = fst.detail OR fl.voucherNo = fst.TID OR fl.folio = CONCAT('BillNo:', fst.TID))" +
            ")";
    }

    private string GetFixBillingInsertSql()
    {
        // INSERT IGNORE: the uq_billing_entry UNIQUE index on (tracking_ref, accountcode, transactionType)
        // silently skips the row if a DR for this tracking_ref+accountcode already exists.
        // This prevents a concurrent fix run or race condition from creating duplicate DRs.
        return
            "INSERT IGNORE INTO fin_ledger " +
            "(accountcode, account_type, transactionType, transaction_amount, particulars, voucherNo, transactionDate, teller, timeLog, folio, tracking_ref, source_system, journal_no, trans_currency, actual_amount, curr_balance, forex_rate, ugx_amount) " +
            "SELECT src.regno, 'Student', src.transactionType, src.amount, src.particulars, src.voucherNo, src.transactionDate, src.regno, NOW(), CONCAT('BillNo:', src.voucherNo), src.voucherNo, 'Billing', '-', 'UGX', src.amount, 0, 1, src.amount " +
            "FROM (" + GetFixBillingSourceSql() + ") src";
    }

    private string GetFixBillingAlignSql()
    {
        return
            "UPDATE fin_ledger l " +
            "INNER JOIN fin_studentfeestracking t ON t.regno=l.accountcode AND t.TID=l.voucherNo AND t.trans_type='Bill' AND t.post_status='Posted' " +
            "SET l.folio = CONCAT('BillNo:', t.TID), " +
            "    l.tracking_ref = t.TID, " +
            "    l.source_system = 'Billing', " +
            "    l.teller = l.accountcode, " +
            "    l.journal_no = CASE WHEN l.journal_no IS NULL OR l.journal_no='' OR l.journal_no='-' OR l.journal_no LIKE 'Sync:%' THEN '-' ELSE l.journal_no END " +
            "WHERE l.accountcode=@reg " +
            "  AND l.account_type='Student' " +
            "  AND l.transactionType='DR' " +
            "  AND (l.source_system IS NULL OR l.source_system='' OR l.source_system='Manual' OR l.teller='LedgerFix' OR l.folio=l.accountcode OR l.folio NOT LIKE 'BillNo:%' OR l.tracking_ref IS NULL)";
    }

    private string GetFixBillingNormaliseSql()
    {
        return "UPDATE fin_ledger SET account_type='Student' WHERE accountcode=@reg AND account_type NOT IN ('Student','Chart Account')";
    }

    // ---- Data classes ----

    private class FixPlan
    {
        public int UnbilledCount;
        public decimal UnbilledAmount;
        public int MissingDrCount;
        public decimal MissingDrAmount;
        public int MissingCrCount;
        public decimal MissingCrAmount;
        public int AlignCount;
        public int NormaliseCount;
        public List<FixSample> Samples = new List<FixSample>();
    }

    private class FixSample
    {
        public string Category;
        public string Period;
        public string FeeType;
        public decimal Amount;
        public string Detail;
    }

    private class UnbilledSem
    {
        public string AcadYear;
        public int Semester;
        public int StudyYear;
        public string ProgCode;
        public string StudentName;
        public bool HasTuition;
        public bool HasFunctional;
        public decimal Tuition;
        public decimal Functional;
    }

    private class MissingCrEntry
    {
        public long TrackingRef;
        public decimal Amount;
        public string IncomeGl;
        public string ItemName;
        public string Folio;
        public DateTime TransDate;
    }

    private void AjaxRemoveDoubleBilling()
    {
        string regno = (Request.QueryString["regno"] ?? "").Trim();
        if (string.IsNullOrEmpty(regno))
        {
            Response.Write("{\"ok\":false,\"error\":\"Missing regno.\"}");
            return;
        }

        int deleted = 0;
        decimal balBefore = 0;
        decimal balAfter = 0;

        using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();

            balBefore = ComputeStudentBalance(conn, regno);

            HashSet<long> dupTids = new HashSet<long>();
            long dupDrAmount = 0;
            int m1, m2, m3, m4;
            DetectDuplicates(conn, regno, dupTids, ref dupDrAmount, out m1, out m2, out m3, out m4);

            if (dupTids.Count > 0)
            {
                List<long> tids = new List<long>(dupTids);
                for (int i = 0; i < tids.Count; i += 500)
                {
                    int end = Math.Min(i + 500, tids.Count);
                    List<string> batch = new List<string>();
                    for (int j = i; j < end; j++) batch.Add(tids[j].ToString());
                    string delSql = "DELETE FROM fin_ledger WHERE TID IN (" + string.Join(",", batch.ToArray()) + ")";
                    using (MySqlCommand cmd = new MySqlCommand(delSql, conn))
                    {
                        deleted += cmd.ExecuteNonQuery();
                    }
                }
            }

            balAfter = ComputeStudentBalance(conn, regno);
        }

        Response.Write(string.Format("{{\"ok\":true,\"deleted\":{0},\"balance_before\":\"{1}\",\"balance_after\":\"{2}\"}}",
            deleted, JsEsc(FormatBalance(balBefore)), JsEsc(FormatBalance(balAfter))));
    }

    private decimal ComputeStudentBalance(MySqlConnection conn, string regno)
    {
        // Canonical dual-source balance — same formula as FinanceEngine /
        // the fees statement / the grid's fin_student_balance_cache. Must count
        // unmirrored tracking PAYMENTS (CR) as well as bills (DR); a prior version
        // only added bills, which overstated the balance for students with
        // tracking-only payments.
        // Sign convention: payments(CR) - bills(DR) => POSITIVE = overpaid (credit),
        // NEGATIVE = still owing (debit). Matches the grid and the fees statement.
        const string sql =
            "SELECT IFNULL(SUM(cr),0) - IFNULL(SUM(dr),0) AS bal " +
            "FROM ( " +
            "  SELECT " +
            "    CASE WHEN fl.transactionType='DR' THEN fl.transaction_amount ELSE 0 END AS dr, " +
            "    CASE WHEN fl.transactionType='CR' THEN fl.transaction_amount ELSE 0 END AS cr " +
            "  FROM fin_ledger fl " +
            "  WHERE fl.accountcode = @reg AND fl.transaction_amount > 0 " +
            "  UNION ALL " +
            "  SELECT " +
            "    CASE WHEN t.trans_type='Bill'    THEN t.amount ELSE 0 END AS dr, " +
            "    CASE WHEN t.trans_type='Payment' THEN t.amount ELSE 0 END AS cr " +
            "  FROM fin_studentfeestracking t " +
            "  WHERE t.regno = @reg AND t.post_status='Posted' " +
            "    AND NOT EXISTS ( " +
            "      SELECT 1 FROM fin_ledger fl2 " +
            "      WHERE fl2.accountcode = t.regno " +
            "        AND ( fl2.voucherNo = CAST(t.TID AS CHAR) " +
            "           OR fl2.folio = CONCAT('BillNo:', CAST(t.TID AS CHAR)) " +
            "           OR ( fl2.transaction_amount = t.amount " +
            "                AND DATE(fl2.transactionDate) = DATE(t.trans_date) " +
            "                AND fl2.transactionType = CASE WHEN t.trans_type='Payment' THEN 'CR' ELSE 'DR' END " +
            "                AND (t.trans_type = 'Payment' OR fl2.particulars = t.detail OR t.detail IS NULL OR t.detail = '') ) ) " +
            "    ) " +
            ") c";

        using (MySqlCommand cmd = new MySqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@reg", regno);
            object v = cmd.ExecuteScalar();
            return v != null && v != DBNull.Value ? Convert.ToDecimal(v) : 0;
        }
    }

    private void DetectDuplicates(MySqlConnection conn, string regno, HashSet<long> dupTids, ref long dupDrAmount, out int m1, out int m2, out int m3, out int m4)
    {
        const string sql1 =
            "SELECT l.TID, l.transaction_amount AS amt, l.transactionType " +
            "FROM fin_ledger l " +
            "INNER JOIN ( " +
            "  SELECT tracking_ref, transactionType, MIN(TID) AS keep_tid " +
            "  FROM fin_ledger " +
            "  WHERE accountcode=@reg AND account_type='Student' AND tracking_ref IS NOT NULL " +
            "  GROUP BY tracking_ref, transactionType HAVING COUNT(*) > 1 " +
            ") d ON l.tracking_ref=d.tracking_ref AND l.transactionType=d.transactionType AND l.TID > d.keep_tid " +
            "WHERE l.accountcode=@reg AND l.account_type='Student'";

        m1 = RunDupDetection(conn, sql1, regno, dupTids, ref dupDrAmount);

        string excl = BuildTidExclusion(dupTids);
        string sql2 =
            "SELECT l.TID, l.transaction_amount AS amt, l.transactionType " +
            "FROM fin_ledger l " +
            "INNER JOIN ( " +
            "  SELECT folio, transactionType, MIN(TID) AS keep_tid " +
            "  FROM fin_ledger " +
            "  WHERE accountcode=@reg AND account_type='Student' AND folio LIKE 'BillNo:%' " +
            "  GROUP BY folio, transactionType HAVING COUNT(*) > 1 " +
            ") d ON l.folio=d.folio AND l.transactionType=d.transactionType AND l.TID > d.keep_tid " +
            "WHERE l.accountcode=@reg AND l.account_type='Student' AND l.TID NOT IN (" + excl + ")";

        m2 = RunDupDetection(conn, sql2, regno, dupTids, ref dupDrAmount);

        excl = BuildTidExclusion(dupTids);
        string sql3 =
            "SELECT g.TID, g.transaction_amount AS amt, g.transactionType " +
            "FROM fin_ledger g " +
            "WHERE g.accountcode=@reg AND g.account_type='Student' AND g.teller='GLSync' " +
            "  AND g.TID NOT IN (" + excl + ") " +
            "  AND EXISTS ( " +
            "    SELECT 1 FROM fin_ledger o WHERE o.accountcode=g.accountcode AND o.account_type='Student' " +
            "      AND o.transactionType=g.transactionType AND o.teller<>'GLSync' AND o.TID<>g.TID " +
            "      AND (o.folio=CONCAT('BillNo:', g.voucherNo) " +
            "        OR (o.tracking_ref IS NOT NULL AND g.tracking_ref IS NOT NULL AND o.tracking_ref=g.tracking_ref) " +
            "        OR (o.transaction_amount=g.transaction_amount AND o.transactionDate=g.transactionDate AND o.folio LIKE 'BillNo:%')) " +
            "  )";

        m3 = RunDupDetection(conn, sql3, regno, dupTids, ref dupDrAmount);

        excl = BuildTidExclusion(dupTids);
        string sql4 =
            "SELECT b.TID, b.transaction_amount AS amt, b.transactionType " +
            "FROM fin_ledger b " +
            "WHERE b.accountcode=@reg AND b.account_type='Student' AND b.transactionType='DR' " +
            "  AND b.particulars LIKE '%BATCH%' AND b.TID NOT IN (" + excl + ") " +
            "  AND EXISTS ( " +
            "    SELECT 1 FROM fin_ledger o WHERE o.accountcode=b.accountcode AND o.account_type='Student' " +
            "      AND o.transactionType='DR' AND o.TID<>b.TID AND o.particulars NOT LIKE '%BATCH%' " +
            "      AND LEFT(REPLACE(b.particulars,' BATCH',''),30)=LEFT(o.particulars,30) " +
            "  )";

        m4 = RunDupDetection(conn, sql4, regno, dupTids, ref dupDrAmount);
    }

    private int RunDupDetection(MySqlConnection conn, string sql, string regno, HashSet<long> dupTids, ref long dupDrAmount)
    {
        int count = 0;
        using (MySqlCommand cmd = new MySqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@reg", regno);
            using (MySqlDataReader rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    long tid = Convert.ToInt64(rdr["TID"]);
                    if (!dupTids.Add(tid)) continue;
                    count++;
                    if (rdr["transactionType"].ToString() == "DR")
                        dupDrAmount += Convert.ToInt64(rdr["amt"]);
                }
            }
        }
        return count;
    }

    private string BuildTidExclusion(HashSet<long> tids)
    {
        if (tids.Count == 0) return "0";
        List<string> items = new List<string>();
        foreach (long t in tids) items.Add(t.ToString());
        return string.Join(",", items.ToArray());
    }

    /// <summary>Plain money formatter for magnitudes (billed / paid totals).</summary>
    protected string FormatMoney(object value)
    {
        decimal amount = 0;
        if (value != null && value != DBNull.Value)
            amount = Convert.ToDecimal(value);
        return string.Format("{0:N0}", amount);
    }

    /// <summary>
    /// Formats a SIGNED balance using the institution's convention:
    /// POSITIVE = overpaid (credit → "CR"), NEGATIVE = still owing (debit → "DR"),
    /// zero = cleared.
    /// </summary>
    protected string FormatBalance(object value)
    {
        decimal amount = 0;
        if (value != null && value != DBNull.Value)
            amount = Convert.ToDecimal(value);
        if (amount > 0) return string.Format("{0:N0} CR", amount);
        if (amount < 0) return string.Format("{0:N0} DR", Math.Abs(amount));
        return "0";
    }

    protected string GetBalanceClass(object value)
    {
        decimal amount = 0;
        if (value != null && value != DBNull.Value)
            amount = Convert.ToDecimal(value);

        if (amount > 0) return "sl-balance--credit"; // overpaid
        if (amount < 0) return "sl-balance--debit";  // owing
        return "sl-balance--zero";
    }

    protected string FormatStudyYear(object value)
    {
        if (value == null || value == DBNull.Value) return "-";
        int y;
        if (!int.TryParse(value.ToString(), out y) || y <= 0) return "-";
        return "Year " + y;
    }

    protected string FormatSemester(object value)
    {
        if (value == null || value == DBNull.Value) return "-";
        int s;
        if (!int.TryParse(value.ToString(), out s) || s <= 0) return "-";
        return "Sem " + s;
    }

    protected string Js(object value)
    {
        if (value == null || value == DBNull.Value) return "";
        return JsEsc(value.ToString());
    }

    protected string GetRegno(object dataItem)
    {
        object v = DataBinder.Eval(dataItem, "regno");
        return v == null || v == DBNull.Value ? "" : v.ToString();
    }

    private string JsEsc(string val)
    {
        if (string.IsNullOrEmpty(val)) return "";
        StringBuilder sb = new StringBuilder(val.Length + 16);
        for (int i = 0; i < val.Length; i++)
        {
            char c = val[i];
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 32)
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    private bool TryParseDecimal(string input, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;
        string cleaned = input.Replace(",", "").Trim();
        return decimal.TryParse(cleaned, out value);
    }

    private class LedgerLine
    {
        public string Ref;
        public string Date;
        public string Particulars;
        public decimal Debit;
        public decimal Credit;
    }
}
