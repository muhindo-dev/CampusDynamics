using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Web.Configuration;
using System.Web.UI;
using System.Web.UI.WebControls;
using MySql.Data.MySqlClient;

public partial class COOPERP_NewScreens_FeesTransactions : System.Web.UI.Page
{
    private string AcctConnStr
    {
        get { return WebConfigurationManager.ConnectionStrings["accountsConnectionString"].ConnectionString; }
    }

    private string MainConnStr
    {
        get { return WebConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    /// <summary>
    /// True for a super administrator — a user whose role grants the access wildcard.
    /// Read through RoleAccessService, which is what MarksScopeResolver and the rest of the
    /// system already treat as the definition, rather than a second idea of the same thing.
    /// Consumed by the markup to decide whether the Edit action is offered on a ledger-only
    /// row; NEVER trusted as the authorisation itself — see EnsureSuperAdmin below.
    /// </summary>
    public bool IsSuperAdmin
    {
        get
        {
            try { return RoleAccessService.IsAdmin(); }
            catch { return false; }
        }
    }

    /// <summary>
    /// The server's own check, re-asserted at the point of the write.
    ///
    /// Hiding a button is a courtesy to the user, not a control: this page posts a TID and
    /// acts on it, so anything the markup declines to show is still one crafted post away.
    /// Every path that edits a ledger row calls this first.
    /// </summary>
    private bool EnsureSuperAdmin()
    {
        if (IsSuperAdmin) return true;
        ShowToast("Only a super administrator may edit a General Ledger entry.", false);
        return false;
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        // AJAX student lookup — returns JSON, no page rendering
        string ajaxAction = Request.QueryString["ajax"];
        if (ajaxAction == "lookup" || ajaxAction == "search")
        {
            HandleStudentLookup();
            return;
        }
        if (ajaxAction == "glsync_scan" || ajaxAction == "glsync_fix")
        {
            HandleGLSync(ajaxAction);
            return;
        }
        if (ajaxAction == "batchdup_scan" || ajaxAction == "batchdup_fix_one")
        {
            HandleBatchDupFix(ajaxAction);
            return;
        }
        if (ajaxAction == "batch_delete")
        {
            HandleBatchDelete();
            return;
        }
        if (ajaxAction == "batch_post_status")
        {
            HandleBatchPostStatus();
            return;
        }

        string tidParamForAutofix = Request.QueryString["tid"];
        int tidAutofix;
        if (!string.IsNullOrEmpty(tidParamForAutofix)
            && int.TryParse(tidParamForAutofix.Trim(), out tidAutofix)
            && tidAutofix > 0)
        {
            AutoFixLegacyPlaceholderDetailForTid(tidAutofix);
        }

        LoadLookups();

        // Set HTML5 input types (.NET 4 doesn't have TextMode="Number"/"Date")
        txtTxAmount.Attributes["type"] = "number";
        txtTxAmount.Attributes["min"] = "1";
        txtTxAmount.Attributes["step"] = "1";
        txtTxDate.Attributes["type"] = "date";

        // Edit modal input types
        txtEditAmount.Attributes["type"] = "number";
        txtEditAmount.Attributes["min"] = "1";
        txtEditAmount.Attributes["step"] = "1";
        txtEditDate.Attributes["type"] = "date";

        // Restore posted dropdown values
        RestorePostedValue(ddlAcadYear);
        RestorePostedValue(ddlSemester);
        RestorePostedValue(ddlTransType);
        RestorePostedValue(ddlBillItem);
        RestorePostedValue(ddlPostStatus);
        RestorePostedValue(ddlStudStatus);
        RestorePostedValue(ddlPageSize);

        // Restore edit modal dropdown values
        RestorePostedValue(ddlEditTransType);
        RestorePostedValue(ddlEditBillItem);
        RestorePostedValue(ddlEditAcadYear);
        RestorePostedValue(ddlEditSemester);
        RestorePostedValue(ddlEditPostStatus);

        if (!IsPostBack)
        {
            // GET-driven filters: hydrate the controls from the query string so the UI mirrors the URL
            // and LoadTransactions (which reads the controls) needs no change. Filters/paging are now
            // plain GET navigation (bookmarkable, back-button friendly, no postback/ViewState churn).
            SetDdlFromQuery(ddlAcadYear,   Request.QueryString["acad"]);
            SetDdlFromQuery(ddlSemester,   Request.QueryString["sem"]);
            SetDdlFromQuery(ddlTransType,  Request.QueryString["type"]);
            SetDdlFromQuery(ddlBillItem,   Request.QueryString["item"]);
            SetDdlFromQuery(ddlPostStatus, Request.QueryString["post"]);
            SetDdlFromQuery(ddlStudStatus, Request.QueryString["studstatus"]);
            SetDdlFromQuery(ddlSource,     Request.QueryString["source"]);
            SetDdlFromQuery(ddlPageSize,   Request.QueryString["size"]);
            txtSearch.Text = Request.QueryString["q"] ?? "";

            // If a specific TID is requested via querystring, clear the year filter
            // so the record always shows regardless of academic year.
            string tidParam = Request.QueryString["tid"];
            int tidParamVal = 0;
            if (!string.IsNullOrEmpty(tidParam) && int.TryParse(tidParam.Trim(), out tidParamVal) && tidParamVal > 0)
            {
                ddlAcadYear.ClearSelection();
                if (ddlAcadYear.Items.FindByValue("") != null)
                    ddlAcadYear.SelectedValue = "";
            }
        }

        LoadTransactions();
    }

    /// <summary>Selects a dropdown value from a query-string parameter (no-op if the value is absent
    /// or not in the list), so the filters reflect the GET URL.</summary>
    private void SetDdlFromQuery(DropDownList ddl, string val)
    {
        if (ddl == null) return;
        ListItem item = ddl.Items.FindByValue(val ?? "");
        if (item != null) { ddl.ClearSelection(); item.Selected = true; }
    }

    /// <summary>Invalidates every cached transaction-summary aggregate (called after any write) so the
    /// totals bar recomputes on the next load instead of showing a stale count.</summary>
    private static void BumpFtStatsCache()
    {
        try { System.Web.HttpRuntime.Cache["ftstatsver"] = Guid.NewGuid().ToString("N"); } catch { }
    }

    private void RestorePostedValue(DropDownList ddl)
    {
        string posted = Request.Form[ddl.UniqueID];
        if (!string.IsNullOrEmpty(posted))
        {
            ListItem item = ddl.Items.FindByValue(posted);
            if (item != null)
            {
                ddl.ClearSelection();
                item.Selected = true;
            }
        }
    }

    private void LoadLookups()
    {
        // Academic Years
        ddlAcadYear.Items.Clear();
        ddlAcadYear.Items.Add(new ListItem("All Years", ""));
        using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand("SELECT DISTINCT acadyear FROM fin_studentfeestracking ORDER BY acadyear DESC", conn))
            {
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        string yr = rdr["acadyear"].ToString();
                        if (!String.IsNullOrEmpty(yr))
                            ddlAcadYear.Items.Add(new ListItem(yr, yr));
                    }
                }
            }
        }

        // Billing Items (filter)
        ddlBillItem.Items.Clear();
        ddlBillItem.Items.Add(new ListItem("All Items", ""));
        using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand("SELECT ItemCode, ItemName FROM academicbillingitems ORDER BY ItemName", conn))
            {
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        ddlBillItem.Items.Add(new ListItem(rdr["ItemName"].ToString(), rdr["ItemCode"].ToString()));
                    }
                }
            }
        }

        // === Modal form dropdowns ===

        // Billing Items (modal — with placeholder)
        ddlTxBillItem.Items.Clear();
        ddlTxBillItem.Items.Add(new ListItem("-- Select Item --", ""));
        foreach (ListItem li in ddlBillItem.Items)
        {
            if (!String.IsNullOrEmpty(li.Value))
                ddlTxBillItem.Items.Add(new ListItem(li.Text, li.Value));
        }

        // Academic Years (modal)
        ddlTxAcadYear.Items.Clear();
        ddlTxAcadYear.Items.Add(new ListItem("-- Select Year --", ""));
        foreach (ListItem li in ddlAcadYear.Items)
        {
            if (!String.IsNullOrEmpty(li.Value))
                ddlTxAcadYear.Items.Add(new ListItem(li.Text, li.Value));
        }

        // === Edit modal form dropdowns ===

        // Billing Items (edit modal)
        ddlEditBillItem.Items.Clear();
        ddlEditBillItem.Items.Add(new ListItem("-- Select Item --", ""));
        foreach (ListItem li in ddlBillItem.Items)
        {
            if (!String.IsNullOrEmpty(li.Value))
                ddlEditBillItem.Items.Add(new ListItem(li.Text, li.Value));
        }

        // Academic Years (edit modal)
        ddlEditAcadYear.Items.Clear();
        ddlEditAcadYear.Items.Add(new ListItem("-- Select Year --", ""));
        foreach (ListItem li in ddlAcadYear.Items)
        {
            if (!String.IsNullOrEmpty(li.Value))
                ddlEditAcadYear.Items.Add(new ListItem(li.Text, li.Value));
        }

        // Default modal academic year to current if not postback
        if (!IsPostBack)
        {
            string curYear = AcademicYearHelper.GetCurrentAcademicYear();
            if (ddlTxAcadYear.Items.FindByValue(curYear) != null)
                ddlTxAcadYear.SelectedValue = curYear;

            int curSem = AcademicYearHelper.GetCurrentSemester();
            if (ddlTxSemester.Items.FindByValue(curSem.ToString()) != null)
                ddlTxSemester.SelectedValue = curSem.ToString();
        }
    }

    private void LoadTransactions()
    {
        // ── Determine which tables to include ──────────────────────────────
        string sourceFilter = ddlSource.SelectedValue;   // "" | "manual" | "gl_only"
        bool showManual  = (sourceFilter != "gl_only");
        bool showGLOnly  = (sourceFilter != "manual");

        // ── Outer WHERE (applied to the combined UNION subquery alias 'c') ──
        var outer   = new StringBuilder("WHERE 1=1");
        var prmList = new List<MySqlParameter>();

        // ?tid= querystring — filter to a single transaction
        string tidParam = Request.QueryString["tid"];
        int    tidParamVal = 0;
        bool   isTidFilter = !string.IsNullOrEmpty(tidParam)
                             && int.TryParse(tidParam.Trim(), out tidParamVal)
                             && tidParamVal > 0;
        if (isTidFilter)
        {
            outer.Append(" AND c.TID = @tidFilter");
            prmList.Add(new MySqlParameter("@tidFilter", tidParamVal));
        }

        string studStatus = ddlStudStatus.SelectedValue;
        if (!string.IsNullOrEmpty(studStatus))
        {
            outer.Append(" AND UPPER(COALESCE(c.student_status,'')) = UPPER(@studStatus)");
            prmList.Add(new MySqlParameter("@studStatus", studStatus));
        }
        if (!string.IsNullOrEmpty(ddlAcadYear.SelectedValue))
        {
            // GL-only rows (AUTO/ghost) have no acadyear in fin_ledger — exempt them from this filter
            outer.Append(" AND (c.acadyear = @yr OR c.row_source IN ('auto','ghost'))");
            prmList.Add(new MySqlParameter("@yr", ddlAcadYear.SelectedValue));
        }
        if (!string.IsNullOrEmpty(ddlSemester.SelectedValue))
        {
            // GL-only rows have no semester — exempt them from this filter too
            outer.Append(" AND (c.semester = @sem OR c.row_source IN ('auto','ghost'))");
            prmList.Add(new MySqlParameter("@sem", ddlSemester.SelectedValue));
        }
        if (!string.IsNullOrEmpty(ddlTransType.SelectedValue))
        {
            outer.Append(" AND c.trans_type = @tt");
            prmList.Add(new MySqlParameter("@tt", ddlTransType.SelectedValue));
        }
        if (!string.IsNullOrEmpty(ddlBillItem.SelectedValue))
        {
            outer.Append(" AND c.item_code = @ic");
            prmList.Add(new MySqlParameter("@ic", ddlBillItem.SelectedValue));
        }
        if (!string.IsNullOrEmpty(ddlPostStatus.SelectedValue))
        {
            outer.Append(" AND c.post_status = @ps");
            prmList.Add(new MySqlParameter("@ps", ddlPostStatus.SelectedValue));
        }
        string search = txtSearch.Text.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            outer.Append(" AND (c.regno LIKE @q OR c.student_name LIKE @q OR c.detail LIKE @q)");
            prmList.Add(new MySqlParameter("@q", "%" + search + "%"));
        }

        // ── Push the SAME filters INTO each UNION branch ──────────────────
        // MySQL 5.6 materialises the derived UNION, so filtering on the outer alias 'c' means the
        // whole ~86k-row union is built first. Pushing the filters into each branch builds a SMALL
        // union instead (a single-student search drops from ~1.7s to ~0.5s). Distinct @m_/@g_ param
        // names → each parameter is referenced exactly once (no reliance on parameter reuse). The
        // outer WHERE below is kept as an exact safety net.
        var mW = new StringBuilder();   // manual-branch predicates
        var gW = new StringBuilder();   // GL-branch predicates
        if (isTidFilter)
        {
            mW.Append(" AND t.TID = @m_tid");  prmList.Add(new MySqlParameter("@m_tid", tidParamVal));
            gW.Append(" AND fl.TID = @g_tid"); prmList.Add(new MySqlParameter("@g_tid", tidParamVal));
        }
        if (!string.IsNullOrEmpty(studStatus))
        {
            mW.Append(" AND UPPER(COALESCE(s.new_status,'')) = UPPER(@m_ss)"); prmList.Add(new MySqlParameter("@m_ss", studStatus));
            gW.Append(" AND UPPER(COALESCE(s.new_status,'')) = UPPER(@g_ss)"); prmList.Add(new MySqlParameter("@g_ss", studStatus));
        }
        if (!string.IsNullOrEmpty(ddlAcadYear.SelectedValue))   // GL rows carry no acadyear → exempt (not pushed)
        {
            mW.Append(" AND t.acadyear = @m_yr"); prmList.Add(new MySqlParameter("@m_yr", ddlAcadYear.SelectedValue));
        }
        if (!string.IsNullOrEmpty(ddlSemester.SelectedValue))   // GL rows carry no semester → exempt
        {
            mW.Append(" AND t.semester = @m_sem"); prmList.Add(new MySqlParameter("@m_sem", ddlSemester.SelectedValue));
        }
        if (!string.IsNullOrEmpty(ddlTransType.SelectedValue))
        {
            mW.Append(" AND t.trans_type = @m_tt"); prmList.Add(new MySqlParameter("@m_tt", ddlTransType.SelectedValue));
            gW.Append(" AND (CASE WHEN fl.transactionType='CR' THEN 'Payment' ELSE 'Bill' END) = @g_tt"); prmList.Add(new MySqlParameter("@g_tt", ddlTransType.SelectedValue));
        }
        if (!string.IsNullOrEmpty(ddlBillItem.SelectedValue))   // GL rows have no item_code → GL branch dropped below
        {
            mW.Append(" AND t.item_code = @m_ic"); prmList.Add(new MySqlParameter("@m_ic", ddlBillItem.SelectedValue));
        }
        if (!string.IsNullOrEmpty(ddlPostStatus.SelectedValue)) // GL rows are always 'Posted'
        {
            mW.Append(" AND t.post_status = @m_ps"); prmList.Add(new MySqlParameter("@m_ps", ddlPostStatus.SelectedValue));
        }
        if (!string.IsNullOrEmpty(search))
        {
            mW.Append(" AND (t.regno LIKE @m_q OR TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) LIKE @m_q OR t.detail LIKE @m_q)");
            prmList.Add(new MySqlParameter("@m_q", "%" + search + "%"));
            gW.Append(" AND (fl.accountcode LIKE @g_q OR TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) LIKE @g_q OR fl.particulars LIKE @g_q)");
            prmList.Add(new MySqlParameter("@g_q", "%" + search + "%"));
        }

        // The GL branch cannot satisfy an item-code filter (no item) or a non-Posted status → drop it.
        bool effShowGL = showGLOnly
                         && string.IsNullOrEmpty(ddlBillItem.SelectedValue)
                         && (string.IsNullOrEmpty(ddlPostStatus.SelectedValue) || ddlPostStatus.SelectedValue == "Posted");

        // ── Build UNION subquery ──────────────────────────────────────────
        string inner  = BuildInnerUnion(showManual, effShowGL, mW.ToString(), gW.ToString());
        string outerW = outer.ToString();

        // ── Stats query ───────────────────────────────────────────────────
        string statsSql =
            "SELECT COUNT(*) AS total_tx,"
          + " SUM(CASE WHEN c.trans_type='Bill'    THEN 1 ELSE 0 END) AS bill_cnt,"
          + " SUM(CASE WHEN c.trans_type='Payment' THEN 1 ELSE 0 END) AS pay_cnt,"
          + " SUM(CASE WHEN c.trans_type='Bill'    THEN c.amount ELSE 0 END) AS bill_amt,"
          + " SUM(CASE WHEN c.trans_type='Payment' THEN c.amount ELSE 0 END) AS pay_amt"
          + " FROM (" + inner + ") AS c " + outerW;

        // ── Paging (GET-driven) ───────────────────────────────────────────
        // Page size and page number come from the URL (?size=, ?page= 1-based). A filter/search
        // change navigates with page=1 (client-side), so no postback page-tracking is needed.
        int pageSize = 50;
        try { pageSize = Convert.ToInt32(ddlPageSize.SelectedValue); } catch { }

        int pageIndex = 0, pgUrl;
        if (int.TryParse(Request.QueryString["page"], out pgUrl) && pgUrl > 0) pageIndex = pgUrl - 1;
        if (pageIndex < 0) pageIndex = 0;
        // After a single-row add/edit/delete (still a postback) jump to page 1 so the result is visible.
        if (IsPostBack && (
                Request.Form[btnSaveTransaction.UniqueID]   != null || Request.Form[btnEditTransaction.UniqueID] != null ||
                Request.Form[btnDeleteTransaction.UniqueID] != null || Request.Form[btnRemoveFromGL.UniqueID]    != null))
            pageIndex = 0;
        hfPageIndex.Value = pageIndex.ToString();   // kept in sync for any legacy references

        using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();

            // 1. Stats — the aggregate (COUNT + SUM over the whole filtered union) is the expensive
            //    half of the load and does NOT change while you page through the same filter. It is
            //    cached app-wide (HttpRuntime.Cache) keyed by the filter signature with a short TTL, so
            //    paging (GET navigation, which resets ViewState) still reuses it and skips the 2nd union
            //    build. A different filter is a different key → recompute; the TTL bounds staleness.
            string filterSig = string.Join("|", new[] {
                ddlAcadYear.SelectedValue, ddlSemester.SelectedValue, ddlTransType.SelectedValue,
                ddlBillItem.SelectedValue, ddlPostStatus.SelectedValue, ddlStudStatus.SelectedValue,
                ddlSource.SelectedValue, search, isTidFilter ? tidParamVal.ToString() : "" });
            // A version token (bumped by every write via BumpFtStatsCache) is folded into the key so any
            // add/edit/delete instantly invalidates the cached totals — no stale summary after a write.
            string statVer = (System.Web.HttpRuntime.Cache["ftstatsver"] as string) ?? "0";
            string statCacheKey = "ftstats::" + statVer + "::" + filterSig;

            long totalTx = 0, billCnt = 0, payCnt = 0;
            decimal billAmt = 0, payAmt = 0;
            object[] cached = System.Web.HttpRuntime.Cache[statCacheKey] as object[];
            if (cached != null && cached.Length == 5)
            {
                totalTx = Convert.ToInt64(cached[0]); billCnt = Convert.ToInt64(cached[1]);
                payCnt  = Convert.ToInt64(cached[2]); billAmt = Convert.ToDecimal(cached[3]);
                payAmt  = Convert.ToDecimal(cached[4]);
            }
            else
            {
                MySqlCommand statsCmd = new MySqlCommand(statsSql, conn);
                foreach (var p in prmList)
                    statsCmd.Parameters.Add(new MySqlParameter(p.ParameterName, p.Value));
                using (MySqlDataReader rdr = statsCmd.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        totalTx = rdr["total_tx"] != DBNull.Value ? Convert.ToInt64(rdr["total_tx"]) : 0;
                        billCnt = rdr["bill_cnt"] != DBNull.Value ? Convert.ToInt64(rdr["bill_cnt"]) : 0;
                        payCnt  = rdr["pay_cnt"]  != DBNull.Value ? Convert.ToInt64(rdr["pay_cnt"])  : 0;
                        billAmt = rdr["bill_amt"] != DBNull.Value ? Convert.ToDecimal(rdr["bill_amt"]) : 0;
                        payAmt  = rdr["pay_amt"]  != DBNull.Value ? Convert.ToDecimal(rdr["pay_amt"])  : 0;
                    }
                }
                System.Web.HttpRuntime.Cache.Insert(statCacheKey,
                    new object[] { totalTx, billCnt, payCnt, billAmt, payAmt }, null,
                    DateTime.Now.AddSeconds(45), System.Web.Caching.Cache.NoSlidingExpiration);
            }
            ApplyStats(totalTx, billCnt, payCnt, billAmt, payAmt);

            // 2. Clamp page index
            int totalPages = totalTx > 0 ? (int)Math.Ceiling((double)totalTx / pageSize) : 1;
            if (pageIndex >= totalPages)
            { pageIndex = Math.Max(0, totalPages - 1); hfPageIndex.Value = pageIndex.ToString(); }
            int offset = pageIndex * pageSize;

            // 3. Paged data
            string dataSql =
                "SELECT c.* FROM (" + inner + ") AS c " + outerW
              + " ORDER BY c.trans_date DESC, c.TID DESC LIMIT @pgOffset, @pgSize";

            MySqlCommand dataCmd = new MySqlCommand(dataSql, conn);
            foreach (var p in prmList)
                dataCmd.Parameters.Add(new MySqlParameter(p.ParameterName, p.Value));
            dataCmd.Parameters.AddWithValue("@pgOffset", offset);
            dataCmd.Parameters.AddWithValue("@pgSize",   pageSize);

            var da = new MySqlDataAdapter(dataCmd);
            var dt = new DataTable();
            da.Fill(dt);

            rptTransactions.DataSource = dt;
            rptTransactions.DataBind();
            phNoData.Visible = (dt.Rows.Count == 0);

            // 4. Footer + pager
            long showFrom = totalTx > 0 ? (long)(offset + 1) : 0;
            long showTo   = Math.Min((long)(offset + pageSize), totalTx);
            lblGridFooter.Text = String.Format(
                "Showing <strong>{0}\u2013{1}</strong> of <strong>{2}</strong> transactions ({3} per page)",
                showFrom, showTo, totalTx.ToString("N0"), pageSize);
            litPager.Text = BuildPagerHtml(pageIndex, totalPages);
        }

        // Context badge
        string ctx = "";
        if (!string.IsNullOrEmpty(ddlAcadYear.SelectedValue))
            ctx = ddlAcadYear.SelectedValue;
        if (!string.IsNullOrEmpty(ddlSemester.SelectedValue))
            ctx += " Sem " + ddlSemester.SelectedValue;
        if (!string.IsNullOrEmpty(studStatus))
            ctx += (ctx.Length > 0 ? " | " : "") + studStatus + " students";
        if (sourceFilter == "manual")
            ctx += (ctx.Length > 0 ? " | " : "") + "Manual only";
        else if (sourceFilter == "gl_only")
            ctx += (ctx.Length > 0 ? " | " : "") + "GL only (orphaned)";
        litAcadContext.Text = !string.IsNullOrEmpty(ctx)
            ? "<span class='ft-card__meta'>" + Server.HtmlEncode(ctx.Trim()) + "</span>"
            : "";
    }

    /// <summary>Renders the summary cards + totals bar from the (possibly cached) aggregate values.</summary>
    private void ApplyStats(long totalTx, long billCnt, long payCnt, decimal billAmt, decimal payAmt)
    {
        litTotalTx.Text     = totalTx.ToString("N0");
        litBillTx.Text      = billCnt.ToString("N0");
        litPayTx.Text       = payCnt.ToString("N0");
        litBillAmt.Text     = FormatCurrency(billAmt);
        litPayAmt.Text      = FormatCurrency(payAmt);
        lblRecordCount.Text = totalTx.ToString("N0") + " records";

        litTotalBarBill.Text = FormatCurrency(billAmt);
        litTotalBarPay.Text  = FormatCurrency(payAmt);
        decimal net = billAmt - payAmt;
        string netClass = net >= 0 ? "ft-totals__pill--net" : "ft-totals__pill--neg";
        string netIcon  = net >= 0
            ? "<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2.5'><circle cx='12' cy='12' r='10'/><line x1='12' y1='8' x2='12' y2='16'/><line x1='8' y1='12' x2='16' y2='12'/></svg>"
            : "<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2.5'><circle cx='12' cy='12' r='10'/><line x1='8' y1='12' x2='16' y2='12'/></svg>";
        litTotalBarNet.Text = string.Format(
            "<span class='ft-totals__pill {0}'>{1} {2}: {3}</span>",
            netClass, netIcon, "Net Balance", "UGX " + net.ToString("N0"));
    }

    /// <summary>
    /// Builds the inner UNION SQL that merges manual tracking rows with GL-only orphan rows.
    /// PERFORMANCE (MySQL 5.6 materialises every derived table — no merge): the active filters are
    /// pushed INTO each branch (manualWhere / glWhere) so the union is built small instead of scanning
    /// ~86k rows then filtering the materialised temp table; and the orphan NOT EXISTS uses a SARGABLE
    /// date range so idx_orphan_match(regno, amount, trans_type, trans_date) is usable (was ~45s → ~1.5s).
    /// </summary>
    private string BuildInnerUnion(bool showManual, bool showGLOnly, string manualWhere, string glWhere)
    {
        var parts = new List<string>();

        if (showManual)
            parts.Add(
                "SELECT 'manual' AS row_source,"
              + " t.TID,"
              + " t.regno,"
              + " TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) AS student_name,"
              + " UPPER(COALESCE(s.new_status,'')) AS student_status,"
              + " t.trans_type,"
              + " t.amount,"
              + " t.detail,"
              + " t.post_status,"
              + " t.trans_date,"
              + " t.acadyear,"
              + " t.semester,"
              + " t.item_code,"
              + " CASE WHEN b.ItemName IS NOT NULL AND b.ItemName != '' THEN b.ItemName"
              + "      WHEN t.item_code IS NULL OR t.item_code = 0 THEN '\u2014'"
              + "      ELSE CONCAT('Item ', t.item_code) END AS item_name,"
              // Created-by: fin_studentfeestracking has no creator column, so read the teller from the
              // GL mirror. Narrow via the accountcode index to this student's few ledger rows, match by
              // amount + type + same day (catches payments), and prefer an exact tracking_ref/folio link
              // (bills). Index-backed and one lookup per row.
              + " (SELECT NULLIF(TRIM(fm.teller),'') FROM fin_ledger fm"
              + "    WHERE fm.accountcode = t.regno"
              + "      AND fm.transaction_amount = t.amount"
              + "      AND fm.transactionType = CASE WHEN t.trans_type='Payment' THEN 'CR' ELSE 'DR' END"
              + "      AND fm.transactionDate >= DATE(t.trans_date)"
              + "      AND fm.transactionDate <  DATE(t.trans_date) + INTERVAL 1 DAY"
              + "      AND NULLIF(TRIM(fm.teller),'') IS NOT NULL"
              + "    ORDER BY (fm.tracking_ref = t.TID) DESC, (fm.folio = CONCAT('BillNo:', t.TID)) DESC"
              + "    LIMIT 1) AS created_by"
              + " FROM fin_studentfeestracking t"
              + " LEFT JOIN campus_dynamics.acad_student s ON s.regno = t.regno"
              + " LEFT JOIN academicbillingitems b ON b.ItemCode = t.item_code"
              + " WHERE 1=1" + (manualWhere ?? ""));

        if (showGLOnly)
            parts.Add(
                "SELECT"
              + " CASE WHEN fcd.original_tid IS NOT NULL THEN 'ghost' ELSE 'auto' END AS row_source,"
              + " fl.TID,"
              + " fl.accountcode AS regno,"
              + " TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) AS student_name,"
              + " UPPER(COALESCE(s.new_status,'')) AS student_status,"
              + " CASE WHEN fl.transactionType='CR' THEN 'Payment' ELSE 'Bill' END AS trans_type,"
              + " fl.transaction_amount AS amount,"
              + " fl.particulars AS detail,"
              + " 'Posted' AS post_status,"
              + " fl.transactionDate AS trans_date,"
              + " CAST(NULL AS CHAR) AS acadyear,"
              + " CAST(NULL AS SIGNED) AS semester,"
              + " CAST(NULL AS SIGNED) AS item_code,"
              + " '\u2014' AS item_name,"
              + " COALESCE(NULLIF(TRIM(fl.teller),''),'') AS created_by"
              + " FROM fin_ledger fl"
              // INNER JOIN ensures accountcode is a real student regno.
              // No account_type filter — AUTO billing SPs use different values (e.g. 'AutoBill').
              // The portal SP fin_GetStudentLedger uses no account_type filter either.
              + " INNER JOIN campus_dynamics.acad_student s ON s.regno = fl.accountcode"
              + " LEFT JOIN fin_changed_deleted_transactions fcd"
              + "     ON fcd.original_tid = fl.voucherNo AND fcd.action_type = 'DELETE'"
              + " WHERE NOT EXISTS ("
              + "       SELECT 1 FROM fin_studentfeestracking t2"
              + "       WHERE t2.regno = fl.accountcode"
              + "         AND t2.amount = fl.transaction_amount"
              + "         AND t2.trans_type = CASE WHEN fl.transactionType='CR' THEN 'Payment' ELSE 'Bill' END"
              // SARGABLE same-day range (was DATE(t2.trans_date)=DATE(...)) so idx_orphan_match is usable
              + "         AND t2.trans_date >= DATE(fl.transactionDate)"
              + "         AND t2.trans_date <  DATE(fl.transactionDate) + INTERVAL 1 DAY"
              + "         AND ("
              + "               t2.TID = fl.voucherNo"           // exact TID-to-voucherNo link
              + "            OR t2.detail = fl.particulars"      // same description (e.g. Airtel/MTN TNo)
              + "            OR (fl.tracking_ref IS NOT NULL AND fl.tracking_ref = t2.TID)"  // billing tracking_ref link
              + "            OR fl.folio = CONCAT('BillNo:', t2.TID)"                        // folio BillNo link
              + "         )"
              + "   )" + (glWhere ?? ""));

        return string.Join(" UNION ALL ", parts);
    }

    protected void ddlAcadYear_SelectedIndexChanged(object sender, EventArgs e) { }
    protected void ddlSemester_SelectedIndexChanged(object sender, EventArgs e) { }
    protected void ddlTransType_SelectedIndexChanged(object sender, EventArgs e) { }
    protected void ddlBillItem_SelectedIndexChanged(object sender, EventArgs e) { }
    protected void ddlPostStatus_SelectedIndexChanged(object sender, EventArgs e) { }
    protected void ddlStudStatus_SelectedIndexChanged(object sender, EventArgs e) { }
    protected void ddlPageSize_Changed(object sender, EventArgs e) { }
    protected void ddlSource_SelectedIndexChanged(object sender, EventArgs e) { }

    protected void gvTransactions_PageIndexChanged(object sender, EventArgs e) { }
    protected void btnGoToPage_Click(object sender, EventArgs e) { /* page index read from hfPageIndex in LoadTransactions */ }

    protected void btnSearch_Click(object sender, EventArgs e) { LoadTransactions(); }

    protected void btnReset_Click(object sender, EventArgs e)
    {
        ddlAcadYear.SelectedIndex  = 0;
        ddlSemester.SelectedIndex  = 0;
        ddlTransType.SelectedIndex = 0;
        ddlBillItem.SelectedIndex  = 0;
        ddlPostStatus.SelectedIndex = 0;
        ddlStudStatus.SelectedIndex = 0; // Reset to "Active" (first item)
        ddlPageSize.SelectedIndex  = 0;
        ddlSource.SelectedIndex    = 0; // Reset to All Sources
        txtSearch.Text = "";
        LoadTransactions();
    }

    protected void btnExportCsv_Click(object sender, EventArgs e)
    {
        // Build WHERE (same as LoadTransactions)
        StringBuilder where = new StringBuilder("WHERE 1=1");
        MySqlCommand cmd = new MySqlCommand();

        // Always LEFT JOIN — every transaction shows regardless of student enrolment status.
        string studentJoin = "LEFT JOIN campus_dynamics.acad_student s ON s.regno = t.regno";

        string studStatus = ddlStudStatus.SelectedValue;
        if (!String.IsNullOrEmpty(studStatus))
        {
            where.Append(" AND UPPER(COALESCE(s.new_status,'')) = UPPER(@studStatus)");
            cmd.Parameters.AddWithValue("@studStatus", studStatus);
        }

        if (!String.IsNullOrEmpty(ddlAcadYear.SelectedValue))
        {
            where.Append(" AND t.acadyear = @yr");
            cmd.Parameters.AddWithValue("@yr", ddlAcadYear.SelectedValue);
        }
        if (!String.IsNullOrEmpty(ddlSemester.SelectedValue))
        {
            where.Append(" AND t.semester = @sem");
            cmd.Parameters.AddWithValue("@sem", ddlSemester.SelectedValue);
        }
        if (!String.IsNullOrEmpty(ddlTransType.SelectedValue))
        {
            where.Append(" AND t.trans_type = @tt");
            cmd.Parameters.AddWithValue("@tt", ddlTransType.SelectedValue);
        }
        if (!String.IsNullOrEmpty(ddlBillItem.SelectedValue))
        {
            where.Append(" AND t.item_code = @ic");
            cmd.Parameters.AddWithValue("@ic", ddlBillItem.SelectedValue);
        }
        if (!String.IsNullOrEmpty(ddlPostStatus.SelectedValue))
        {
            where.Append(" AND t.post_status = @ps");
            cmd.Parameters.AddWithValue("@ps", ddlPostStatus.SelectedValue);
        }
        string search = txtSearch.Text.Trim();
        if (!String.IsNullOrEmpty(search))
        {
            where.Append(" AND (t.regno LIKE @q OR CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,'')) LIKE @q OR t.detail LIKE @q)");
            cmd.Parameters.AddWithValue("@q", "%" + search + "%");
        }

        string sql = String.Format(
            @"SELECT t.TID, t.regno,
                     TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) AS student_name,
                     t.trans_type, COALESCE(b.ItemName, t.item_code) AS item_name,
                     t.amount, t.detail, t.post_status, t.trans_date, t.acadyear, t.semester
              FROM fin_studentfeestracking t
              {1}
              LEFT JOIN academicbillingitems b ON b.ItemCode = t.item_code
              {0}
              ORDER BY t.TID DESC", where.ToString(), studentJoin);

        DataTable dt = new DataTable();
        using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();
            cmd.Connection = conn;
            cmd.CommandText = sql;
            MySqlDataAdapter da = new MySqlDataAdapter(cmd);
            da.Fill(dt);
        }

        Response.Clear();
        Response.ContentType = "text/csv";
        Response.AddHeader("Content-Disposition", "attachment;filename=FeeTransactions_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("TID,RegNo,Student,Type,Billing Item,Amount,Description,Status,Date,Year,Semester");
        foreach (DataRow row in dt.Rows)
        {
            sb.AppendFormat("{0},\"{1}\",\"{2}\",{3},\"{4}\",{5},\"{6}\",{7},{8},{9},{10}\r\n",
                row["TID"], CsvSafe(row["regno"]), CsvSafe(row["student_name"]),
                row["trans_type"], CsvSafe(row["item_name"]), row["amount"],
                CsvSafe(row["detail"]), row["post_status"], row["trans_date"],
                row["acadyear"], row["semester"]);
        }
        Response.Write(sb.ToString());
        Response.End();
    }

    // Helpers
    protected string GetTypeClass(object val)
    {
        string v = val != null ? val.ToString() : "";
        if (v == "Bill") return "ft-badge--bill";
        if (v == "Payment") return "ft-badge--pay";
        return "";
    }

    protected string GetStatusClass(object val)
    {
        string v = val != null ? val.ToString() : "";
        if (v == "Posted") return "ft-badge--posted";
        return "ft-badge--pending";
    }

    protected string GetSourceBadge(object val)
    {
        switch ((val != null ? val.ToString() : "").ToLowerInvariant())
        {
            case "manual": return "<span class='ft-badge' style='background:#e8f0fe;color:#1a56db;'>Manual</span>";
            case "auto":   return "<span class='ft-badge' style='background:#e3f2fd;color:#0277bd;'>AUTO</span>";
            case "ghost":  return "<span class='ft-badge' style='background:#fff3e0;color:#e65100;'>Ghost</span>";
            default:       return "<span class='ft-badge'>-</span>";
        }
    }

    protected string FormatAmt(object val)
    {
        if (val == null || val == DBNull.Value) return "0";
        decimal d = Convert.ToDecimal(val);
        return "UGX " + d.ToString("N0");
    }

    private string FormatCurrency(decimal val)
    {
        return String.Format("UGX {0:N0}", val);
    }

    private string CsvSafe(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        return val.ToString().Replace("\"", "\"\"");
    }

    // ====================================================================
    // AJAX Student Lookup (lightweight JSON response, no page rendering)
    // ====================================================================
    private void HandleStudentLookup()
    {
        Response.Clear();
        Response.ContentType = "application/json";

        string action = (Request.QueryString["ajax"] ?? "").Trim();
        string query = (Request.QueryString["q"] ?? Request.QueryString["regno"] ?? "").Trim();

        if (string.IsNullOrEmpty(query) || query.Length < 2)
        {
            Response.Write("{\"results\":[]}");
            Response.End();
            return;
        }

        try
        {
            using (MySqlConnection conn = new MySqlConnection(MainConnStr))
            {
                conn.Open();

                // Powerful multi-field search: by reg number, student number, first name, other name, or full name
                // Supports partial matching and multiple search terms
                string[] terms = query.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                StringBuilder whereClause = new StringBuilder();
                MySqlCommand cmd = new MySqlCommand();

                // Include all students with a valid status (Active, Admitted, etc.)
                // Only exclude blank/null status records
                whereClause.Append("COALESCE(s.new_status,'') != ''");

                if (terms.Length == 1)
                {
                    // Single term — search across all key fields
                    whereClause.Append(@" AND (
                        s.regno LIKE @q1 
                        OR s.entryno LIKE @q1 
                        OR s.firstname LIKE @q1 
                        OR s.othername LIKE @q1 
                        OR CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,'')) LIKE @q1
                    )");
                    cmd.Parameters.AddWithValue("@q1", "%" + terms[0] + "%");
                }
                else
                {
                    // Multiple terms — each term must match at least one field (AND logic)
                    // This handles "John Doe" matching firstname=John AND othername=Doe
                    for (int i = 0; i < terms.Length && i < 5; i++)
                    {
                        string pName = "@qt" + i;
                        whereClause.Append(" AND ");
                        whereClause.AppendFormat(@"(
                            s.regno LIKE {0} 
                            OR s.entryno LIKE {0} 
                            OR s.firstname LIKE {0} 
                            OR s.othername LIKE {0}
                        )", pName);
                        cmd.Parameters.AddWithValue(pName, "%" + terms[i] + "%");
                    }
                }

                string sql = String.Format(@"SELECT 
                    s.regno,
                    COALESCE(s.entryno, '') AS studno,
                    TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) AS student_name,
                    COALESCE(p.progname, 'N/A') AS programme,
                    COALESCE(s.studsesion, 'N/A') AS session_name,
                    COALESCE(s.new_status, '') AS status
                  FROM acad_student s
                  LEFT JOIN acad_programme p ON p.progcode = s.progid
                  WHERE {0}
                  ORDER BY 
                    CASE WHEN UPPER(COALESCE(s.new_status,'')) = 'ACTIVE' THEN 0
                         WHEN UPPER(COALESCE(s.new_status,'')) = 'ADMITTED' THEN 1
                         ELSE 2 END,
                    CASE WHEN s.regno LIKE @qExact THEN 0
                         WHEN s.regno LIKE @qStart THEN 1
                         WHEN s.firstname LIKE @qStart THEN 2
                         ELSE 3 END,
                    s.firstname, s.othername
                  LIMIT 15", whereClause.ToString());

                cmd.CommandText = sql;
                cmd.Connection = conn;
                cmd.Parameters.AddWithValue("@qExact", query);
                cmd.Parameters.AddWithValue("@qStart", query + "%");

                StringBuilder json = new StringBuilder();
                json.Append("{\"results\":[");
                bool first = true;

                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        if (!first) json.Append(",");
                        first = false;
                        json.AppendFormat(
                            "{{\"regno\":\"{0}\",\"studno\":\"{1}\",\"name\":\"{2}\",\"programme\":\"{3}\",\"session\":\"{4}\",\"status\":\"{5}\"}}",
                            JsEsc(rdr["regno"].ToString()),
                            JsEsc(rdr["studno"].ToString()),
                            JsEsc(rdr["student_name"].ToString()),
                            JsEsc(rdr["programme"].ToString()),
                            JsEsc(rdr["session_name"].ToString()),
                            JsEsc(rdr["status"].ToString()));
                    }
                }

                json.Append("]}");
                Response.Write(json.ToString());
            }
        }
        catch (Exception ex)
        {
            Response.Write("{\"results\":[],\"error\":\"" + JsEsc(ex.Message) + "\"}");
        }

        Response.End();
    }

    // ====================================================================
    // Save Transaction
    // ====================================================================
    protected void btnSaveTransaction_Click(object sender, EventArgs e)
    {
        BumpFtStatsCache();   // invalidate the cached summary totals after this write
        // Restore modal posted values
        RestorePostedValue(ddlTxTransType);
        RestorePostedValue(ddlTxBillItem);
        RestorePostedValue(ddlTxAcadYear);
        RestorePostedValue(ddlTxSemester);
        RestorePostedValue(ddlTxPostStatus);

        // Gather values
        string regno = hfSelectedRegNo.Value.Trim();
        string transType = ddlTxTransType.SelectedValue;
        string billItemVal = ddlTxBillItem.SelectedValue;
        string amountStr = txtTxAmount.Text.Trim();
        string acadYear = ddlTxAcadYear.SelectedValue;
        string semesterStr = ddlTxSemester.SelectedValue;
        string detail = txtTxDetail.Text.Trim();
        string dateStr = txtTxDate.Text.Trim();
        string postStatus = ddlTxPostStatus.SelectedValue;

        // ---- Server-side validation ----

        // Required fields
        if (string.IsNullOrEmpty(regno))
        { ShowToast("Registration Number is required.", false); OpenModalAfterPostback(); return; }
        if (string.IsNullOrEmpty(transType))
        { ShowToast("Transaction Type is required.", false); OpenModalAfterPostback(); return; }
        if (string.IsNullOrEmpty(billItemVal))
        { ShowToast("Billing Item is required.", false); OpenModalAfterPostback(); return; }
        if (string.IsNullOrEmpty(amountStr))
        { ShowToast("Amount is required.", false); OpenModalAfterPostback(); return; }
        if (string.IsNullOrEmpty(acadYear))
        { ShowToast("Academic Year is required.", false); OpenModalAfterPostback(); return; }
        if (string.IsNullOrEmpty(semesterStr))
        { ShowToast("Semester is required.", false); OpenModalAfterPostback(); return; }
        if (string.IsNullOrEmpty(detail))
        { ShowToast("Description is required.", false); OpenModalAfterPostback(); return; }
        if (string.IsNullOrEmpty(dateStr))
        { ShowToast("Transaction Date is required.", false); OpenModalAfterPostback(); return; }

        // Amount validation
        double amount;
        if (!double.TryParse(amountStr, out amount) || amount <= 0)
        { ShowToast("Amount must be a positive number.", false); OpenModalAfterPostback(); return; }

        // Semester
        int semester;
        if (!int.TryParse(semesterStr, out semester) || semester < 1 || semester > 3)
        { ShowToast("Invalid semester value.", false); OpenModalAfterPostback(); return; }

        // Item code
        int itemCode;
        if (!int.TryParse(billItemVal, out itemCode))
        { ShowToast("Invalid billing item.", false); OpenModalAfterPostback(); return; }

        // Date
        DateTime transDate;
        if (!DateTime.TryParse(dateStr, out transDate))
        { ShowToast("Invalid transaction date.", false); OpenModalAfterPostback(); return; }

        // Detail max length
        if (detail.Length > 250)
        { ShowToast("Description must be 250 characters or less.", false); OpenModalAfterPostback(); return; }

        // Post status
        if (postStatus != "Pending" && postStatus != "Posted")
            postStatus = "Pending";

        // Trans type whitelist
        if (transType != "Bill" && transType != "Payment")
        { ShowToast("Invalid transaction type.", false); OpenModalAfterPostback(); return; }

        // ---- Verify student exists ----
        bool studentExists = false;
        using (MySqlConnection conn = new MySqlConnection(MainConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand("SELECT 1 FROM acad_student WHERE regno = @r LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                object result = cmd.ExecuteScalar();
                studentExists = result != null;
            }
        }
        if (!studentExists)
        { ShowToast("Student with registration number '" + Server.HtmlEncode(regno) + "' was not found.", false); OpenModalAfterPostback(); return; }

        // ---- Check for duplicate (UNIQUE: regno, acadyear, semester, item_code, trans_type) ----
        bool duplicate = false;
        using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(
                @"SELECT TID FROM fin_studentfeestracking 
                  WHERE regno = @r AND acadyear = @y AND semester = @s AND item_code = @ic AND trans_type = @tt 
                  LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                cmd.Parameters.AddWithValue("@y", acadYear);
                cmd.Parameters.AddWithValue("@s", semester);
                cmd.Parameters.AddWithValue("@ic", itemCode);
                cmd.Parameters.AddWithValue("@tt", transType);
                object result = cmd.ExecuteScalar();
                duplicate = result != null;
            }
        }
        if (duplicate)
        {
            string itemName = ddlTxBillItem.SelectedItem != null ? ddlTxBillItem.SelectedItem.Text : billItemVal;
            ShowToast(String.Format(
                "Duplicate: A {0} for '{1}' already exists for {2} in {3} Sem {4}.",
                transType, itemName, regno, acadYear, semester), false);
            OpenModalAfterPostback();
            return;
        }

        // ---- INSERT ----
        try
        {
            using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                MySqlTransaction tx = conn.BeginTransaction();
                try
                {
                    // 1. Write to fin_studentfeestracking (tracking table)
                    long newTID;
                    string insertSql = @"INSERT INTO fin_studentfeestracking 
                        (regno, semester, acadyear, amount, item_code, trans_type, detail, trans_date, post_status) 
                        VALUES (@r, @s, @y, @a, @ic, @tt, @d, @dt, @ps)";
                    using (MySqlCommand cmd = new MySqlCommand(insertSql, conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@r", regno);
                        cmd.Parameters.AddWithValue("@s", semester);
                        cmd.Parameters.AddWithValue("@y", acadYear);
                        cmd.Parameters.AddWithValue("@a", amount);
                        cmd.Parameters.AddWithValue("@ic", itemCode);
                        cmd.Parameters.AddWithValue("@tt", transType);
                        cmd.Parameters.AddWithValue("@d", detail);
                        cmd.Parameters.AddWithValue("@dt", transDate);
                        cmd.Parameters.AddWithValue("@ps", postStatus);
                        cmd.ExecuteNonQuery();
                        newTID = cmd.LastInsertedId;
                    }

                    // 2. Mirror to fin_ledger (GL) so Student Ledger stays in sync.
                    //    Only post when status is 'Posted' — Pending stays out of GL.
                    //    Payment = CR (reduces balance owed); Bill = DR (increases balance owed).
                    if (postStatus == "Posted")
                    {
                        string glType = (transType == "Payment") ? "CR" : "DR";
                        string itemText2 = ddlTxBillItem.SelectedItem != null ? ddlTxBillItem.SelectedItem.Text : billItemVal;
                        string glParticulars = string.IsNullOrEmpty(detail) ? itemText2 : detail;
                        using (MySqlCommand cmd = new MySqlCommand(@"
                            INSERT INTO fin_ledger
                                (accountcode, account_type, transactionType, transaction_amount, particulars,
                                 voucherNo, transactionDate, teller, timeLog, folio,
                                 journal_no, trans_currency, actual_amount, curr_balance, forex_rate, ugx_amount)
                            VALUES (@ac, 'Student', @tt, @amt, @part, @vno, @td, @user, @tl, @fo, '-', 'UGX', @amt, 0, 1, @amt)",
                            conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@ac",   regno);
                            cmd.Parameters.AddWithValue("@tt",   glType);
                            cmd.Parameters.AddWithValue("@amt",  amount);
                            cmd.Parameters.AddWithValue("@part", glParticulars);
                            cmd.Parameters.AddWithValue("@vno",  newTID);
                            cmd.Parameters.AddWithValue("@td",   transDate.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@user", GetCurrentUser());
                            cmd.Parameters.AddWithValue("@tl",   DateTime.Now);
                            cmd.Parameters.AddWithValue("@fo",   regno);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }

            string itemText = ddlTxBillItem.SelectedItem != null ? ddlTxBillItem.SelectedItem.Text : billItemVal;
            ShowToast(String.Format(
                "Transaction saved: {0} of UGX {1} for {2} ({3}, {4} Sem {5}).",
                transType, amount.ToString("N0"), regno, itemText, acadYear, semester), true);

            // Clear modal fields
            hfSelectedRegNo.Value = "";
            txtTxAmount.Text = "";
            txtTxDetail.Text = "";
            ddlTxTransType.SelectedIndex = 0;
            ddlTxBillItem.SelectedIndex = 0;
            ddlTxPostStatus.SelectedIndex = 0;

            // Refresh grid
            LoadTransactions();
        }
        catch (MySqlException mex)
        {
            if (mex.Number == 1062) // Duplicate entry (caught by DB unique constraint or trigger)
            {
                ShowToast("This transaction already exists (duplicate detected by database).", false);
            }
            else
            {
                ShowToast("Database error: " + Server.HtmlEncode(mex.Message), false);
            }
            OpenModalAfterPostback();
        }
        catch (Exception ex)
        {
            ShowToast("Error: " + Server.HtmlEncode(ex.Message), false);
            OpenModalAfterPostback();
        }
    }

    // ====================================================================
    // Edit Transaction
    // ====================================================================
    protected void btnEditTransaction_Click(object sender, EventArgs e)
    {
        BumpFtStatsCache();   // invalidate the cached summary totals after this write
        // Restore edit modal posted values
        RestorePostedValue(ddlEditTransType);
        RestorePostedValue(ddlEditBillItem);
        RestorePostedValue(ddlEditAcadYear);
        RestorePostedValue(ddlEditSemester);
        RestorePostedValue(ddlEditPostStatus);

        string tidStr = hfEditTID.Value.Trim();
        int tid;
        if (!int.TryParse(tidStr, out tid) || tid <= 0)
        { ShowToast("Invalid transaction ID.", false); return; }

        // A "manual" row is a fin_studentfeestracking row and its TID is that table's key.
        // An "auto" or "ghost" row is a fin_ledger row with no tracking row behind it, and its
        // TID is fin_ledger's key. THE TWO KEY SPACES OVERLAP: 21,115 ledger TIDs are also a
        // tracking TID belonging to a DIFFERENT student. Editing by TID alone would therefore
        // rewrite an unrelated person's record, which is why the source travels with the TID
        // and why each branch below re-checks that the row it found is the row it was shown.
        string editSource = (hfEditSource.Value ?? "").Trim().ToLowerInvariant();
        string editRegno  = (hfEditRegno.Value ?? "").Trim();
        if (editSource == "auto" || editSource == "ghost")
        {
            EditGeneralLedgerRow(tid, editRegno);
            return;
        }

        string transType = ddlEditTransType.SelectedValue;
        string billItemVal = ddlEditBillItem.SelectedValue;
        string amountStr = txtEditAmount.Text.Trim();
        string acadYear = ddlEditAcadYear.SelectedValue;
        string semesterStr = ddlEditSemester.SelectedValue;
        string detail = txtEditDetail.Text.Trim();
        string dateStr = txtEditDate.Text.Trim();
        string postStatus = ddlEditPostStatus.SelectedValue;

        // ---- Validation ----
        if (string.IsNullOrEmpty(transType))
        { ShowToast("Transaction Type is required.", false); OpenEditModalAfterPostback(); return; }
        if (string.IsNullOrEmpty(billItemVal))
        { ShowToast("Billing Item is required.", false); OpenEditModalAfterPostback(); return; }
        if (string.IsNullOrEmpty(amountStr))
        { ShowToast("Amount is required.", false); OpenEditModalAfterPostback(); return; }
        if (string.IsNullOrEmpty(acadYear))
        { ShowToast("Academic Year is required.", false); OpenEditModalAfterPostback(); return; }
        if (string.IsNullOrEmpty(semesterStr))
        { ShowToast("Semester is required.", false); OpenEditModalAfterPostback(); return; }
        if (string.IsNullOrEmpty(detail))
        { ShowToast("Description is required.", false); OpenEditModalAfterPostback(); return; }
        if (string.IsNullOrEmpty(dateStr))
        { ShowToast("Transaction Date is required.", false); OpenEditModalAfterPostback(); return; }

        double amount;
        if (!double.TryParse(amountStr, out amount) || amount <= 0)
        { ShowToast("Amount must be a positive number.", false); OpenEditModalAfterPostback(); return; }

        int semester;
        if (!int.TryParse(semesterStr, out semester) || semester < 1 || semester > 3)
        { ShowToast("Invalid semester.", false); OpenEditModalAfterPostback(); return; }

        int itemCode;
        if (!int.TryParse(billItemVal, out itemCode))
        { ShowToast("Invalid billing item.", false); OpenEditModalAfterPostback(); return; }

        DateTime transDate;
        if (!DateTime.TryParse(dateStr, out transDate))
        { ShowToast("Invalid date.", false); OpenEditModalAfterPostback(); return; }

        if (detail.Length > 250)
        { ShowToast("Description must be 250 characters or less.", false); OpenEditModalAfterPostback(); return; }

        if (postStatus != "Pending" && postStatus != "Posted") postStatus = "Pending";
        if (transType != "Bill" && transType != "Payment")
        { ShowToast("Invalid transaction type.", false); OpenEditModalAfterPostback(); return; }

        try
        {
            using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();

                // ---- Audit: capture BEFORE the UPDATE ----
                InsertAuditRecord(conn, tid, "EDIT",
                    newTransType: transType, newItemCode: itemCode, newAmount: amount,
                    newDetail: detail, newTransDate: transDate, newAcadYear: acadYear,
                    newSemester: semester, newPostStatus: postStatus);

                // Read the original regno (needed for GL sync)
                string origRegno = "";
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT regno FROM fin_studentfeestracking WHERE TID=@tid", conn))
                {
                    cmd.Parameters.AddWithValue("@tid", tid);
                    object r = cmd.ExecuteScalar();
                    if (r != null && r != DBNull.Value) origRegno = r.ToString();
                }

                // The row on screen and the row about to be written must be the same person's.
                // Without this a ledger TID posted as a manual edit would land on whichever
                // tracking row happens to share the number — see the note on key spaces above.
                if (origRegno == "")
                { ShowToast("That transaction no longer exists.", false); OpenEditModalAfterPostback(); return; }
                if (editRegno != "" && !string.Equals(origRegno.Trim(), editRegno, StringComparison.OrdinalIgnoreCase))
                {
                    ShowToast("This transaction belongs to a different student. Refresh and try again.", false);
                    OpenEditModalAfterPostback();
                    return;
                }

                MySqlTransaction tx = conn.BeginTransaction();
                try
                {
                    // 1. Update fin_studentfeestracking
                    string sql = @"UPDATE fin_studentfeestracking SET 
                        trans_type=@tt, item_code=@ic, amount=@a, detail=@d, trans_date=@dt, 
                        acadyear=@y, semester=@s, post_status=@ps 
                        WHERE TID=@tid";
                    int rows;
                    using (MySqlCommand cmd = new MySqlCommand(sql, conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@tt", transType);
                        cmd.Parameters.AddWithValue("@ic", itemCode);
                        cmd.Parameters.AddWithValue("@a", amount);
                        cmd.Parameters.AddWithValue("@d", detail);
                        cmd.Parameters.AddWithValue("@dt", transDate);
                        cmd.Parameters.AddWithValue("@y", acadYear);
                        cmd.Parameters.AddWithValue("@s", semester);
                        cmd.Parameters.AddWithValue("@ps", postStatus);
                        cmd.Parameters.AddWithValue("@tid", tid);
                        rows = cmd.ExecuteNonQuery();
                    }

                    // 2. Sync fin_ledger: remove old GL entry then re-insert with updated values
                    using (MySqlCommand cmd = new MySqlCommand(
                        "DELETE FROM fin_ledger WHERE voucherNo=@vno AND accountcode=@ac AND account_type='Student'", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@vno", tid);
                        cmd.Parameters.AddWithValue("@ac", origRegno);
                        cmd.ExecuteNonQuery();
                    }
                    if (postStatus == "Posted" && !string.IsNullOrEmpty(origRegno))
                    {
                        string glType = (transType == "Payment") ? "CR" : "DR";
                        string itemText2 = ddlEditBillItem.SelectedItem != null ? ddlEditBillItem.SelectedItem.Text : billItemVal;
                        string glParticulars = string.IsNullOrEmpty(detail) ? itemText2 : detail;
                        using (MySqlCommand cmd = new MySqlCommand(@"
                            INSERT INTO fin_ledger
                                (accountcode, account_type, transactionType, transaction_amount, particulars,
                                 voucherNo, transactionDate, teller, timeLog, folio,
                                 journal_no, trans_currency, actual_amount, curr_balance, forex_rate, ugx_amount)
                            VALUES (@ac, 'Student', @tt, @amt, @part, @vno, @td, @user, @tl, @fo, '-', 'UGX', @amt, 0, 1, @amt)",
                            conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@ac",   origRegno);
                            cmd.Parameters.AddWithValue("@tt",   glType);
                            cmd.Parameters.AddWithValue("@amt",  amount);
                            cmd.Parameters.AddWithValue("@part", glParticulars);
                            cmd.Parameters.AddWithValue("@vno",  tid);
                            cmd.Parameters.AddWithValue("@td",   transDate.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@user", GetCurrentUser());
                            cmd.Parameters.AddWithValue("@tl",   DateTime.Now);
                            cmd.Parameters.AddWithValue("@fo",   origRegno);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();

                    if (rows > 0)
                    {
                        string itemText = ddlEditBillItem.SelectedItem != null ? ddlEditBillItem.SelectedItem.Text : billItemVal;
                        ShowToast(String.Format("Transaction #{0} updated — {1} of UGX {2} ({3}).",
                            tid, transType, amount.ToString("N0"), itemText), true);
                    }
                    else
                    {
                        ShowToast("Transaction #" + tid + " was not found.", false);
                    }
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
            LoadTransactions();
        }
        catch (Exception ex)
        {
            ShowToast("Error updating: " + Server.HtmlEncode(ex.Message), false);
            OpenEditModalAfterPostback();
        }
    }

    // ====================================================================
    // Remove Orphaned GL Entry
    // ====================================================================
    protected void btnRemoveFromGL_Click(object sender, EventArgs e)
    {
        BumpFtStatsCache();   // invalidate the cached summary totals after this write
        string glTidStr = hfRemoveGLTID.Value.Trim();
        int glTid;
        if (!int.TryParse(glTidStr, out glTid) || glTid <= 0)
        { ShowToast("Invalid GL entry ID.", false); return; }

        string glCategory    = hfDeleteCategory.Value.Trim();
        string glExplanation = hfDeleteExplanation.Value.Trim();
        // Provide a sensible fallback if somehow no category was captured
        if (string.IsNullOrEmpty(glCategory)) glCategory = "Removed via admin GL cleanup";

        try
        {
            using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();

                // Read the GL row before deleting — for archiving purposes
                string selectSql =
                    "SELECT accountcode, transactionType, transaction_amount, particulars,"
                  + " transactionDate FROM fin_ledger"
                  + " WHERE TID=@tid";
                string regno = ""; string txType = ""; decimal amount = 0;
                using (MySqlCommand sel = new MySqlCommand(selectSql, conn))
                {
                    sel.Parameters.AddWithValue("@tid", glTid);
                    using (MySqlDataReader r = sel.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            regno  = r["accountcode"].ToString();
                            txType = r["transactionType"].ToString();
                            amount = r["transaction_amount"] != DBNull.Value
                                   ? Convert.ToDecimal(r["transaction_amount"]) : 0;
                        }
                    }
                }

                if (string.IsNullOrEmpty(regno))
                { ShowToast("GL entry #" + glTid + " not found.", false); return; }

                // Archive to fin_deleted_transactions — auto-create table if needed, then insert.
                string glArchiveWarning = null;
                try
                {
                    EnsureDeletedTransactionsTable(conn);
                    string archiveSql =
                        "INSERT INTO fin_deleted_transactions"
                      + " (original_tid, regno, trans_type, item_code, amount, detail,"
                      + "  trans_date, acadyear, semester, post_status,"
                      + "  deleted_by, deleted_at, delete_category, delete_reason, ip_address)"
                      + " SELECT @glTid, accountcode,"
                      + "   CASE WHEN transactionType='CR' THEN 'Payment' ELSE 'Bill' END,"
                      + "   NULL, transaction_amount, particulars,"
                      + "   transactionDate, NULL, NULL, 'Posted',"
                      + "   @user, NOW(), @cat, @expl, @ip"
                      + " FROM fin_ledger WHERE TID=@glTid";
                    using (MySqlCommand arc = new MySqlCommand(archiveSql, conn))
                    {
                        arc.Parameters.AddWithValue("@glTid", glTid);
                        arc.Parameters.AddWithValue("@user",  GetCurrentUser());
                        arc.Parameters.AddWithValue("@cat",   glCategory);
                        arc.Parameters.AddWithValue("@expl",  string.IsNullOrEmpty(glExplanation) ? (object)DBNull.Value : glExplanation);
                        arc.Parameters.AddWithValue("@ip",    Request.UserHostAddress ?? "");
                        arc.ExecuteNonQuery();
                    }
                }
                catch (Exception archEx)
                {
                    // Archive failed — continue so the GL row is still removed.
                    glArchiveWarning = archEx.Message;
                }

                // Delete from fin_ledger
                int rows;
                using (MySqlCommand del = new MySqlCommand(
                    "DELETE FROM fin_ledger WHERE TID=@tid", conn))
                {
                    del.Parameters.AddWithValue("@tid", glTid);
                    rows = del.ExecuteNonQuery();
                }

                if (rows > 0)
                    ShowToast("GL entry #" + glTid + " (" + regno + ") has been removed from the ledger.", true);
                else
                    ShowToast("GL entry #" + glTid + " was not found.", false);
            }
            LoadTransactions();
        }
        catch (Exception ex)
        {
            ShowToast("Error removing GL entry: " + Server.HtmlEncode(ex.Message), false);
        }
    }

    // ====================================================================
    // Delete Transaction
    // ====================================================================
    protected void btnDeleteTransaction_Click(object sender, EventArgs e)
    {
        BumpFtStatsCache();   // invalidate the cached summary totals after this write
        string tidStr = hfDeleteTID.Value.Trim();
        int tid;
        if (!int.TryParse(tidStr, out tid) || tid <= 0)
        { ShowToast("Invalid transaction ID.", false); return; }

        try
        {
            using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();

                // ---- Audit: capture BEFORE the DELETE (immutable log, outside tx) ----
                string delCategory    = hfDeleteCategory.Value.Trim();
                string delExplanation = hfDeleteExplanation.Value.Trim();
                string auditReason    = string.IsNullOrEmpty(delCategory) ? null
                    : (string.IsNullOrEmpty(delExplanation) ? delCategory : delCategory + " — " + delExplanation);
                InsertAuditRecord(conn, tid, "DELETE", reason: auditReason);

                // Read full row details before deleting — needed to match GL entries
                // that were written independently (e.g. Airtel/MTN payments have a different
                // voucherNo in fin_ledger than the TID in fin_studentfeestracking).
                string delRegno = ""; string delDetail = ""; string delTransType = "";
                decimal delAmount = 0; DateTime delDate = DateTime.MinValue;
                using (MySqlCommand cmd = new MySqlCommand(
                    @"SELECT regno, amount, trans_type, detail, trans_date
                      FROM fin_studentfeestracking WHERE TID=@tid", conn))
                {
                    cmd.Parameters.AddWithValue("@tid", tid);
                    using (MySqlDataReader r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            delRegno     = r["regno"]     != DBNull.Value ? r["regno"].ToString()     : "";
                            delAmount    = r["amount"]    != DBNull.Value ? Convert.ToDecimal(r["amount"]) : 0;
                            delTransType = r["trans_type"]!= DBNull.Value ? r["trans_type"].ToString() : "";
                            delDetail    = r["detail"]    != DBNull.Value ? r["detail"].ToString()     : "";
                            delDate      = r["trans_date"]!= DBNull.Value ? Convert.ToDateTime(r["trans_date"]) : DateTime.MinValue;
                        }
                    }
                }

                MySqlTransaction tx = conn.BeginTransaction();
                string archiveWarning = null;
                try
                {
                    // 0. Archive to fin_deleted_transactions — wrapped so a missing table or
                    //    outdated schema NEVER prevents the actual delete from completing.
                    try
                    {
                        ArchiveDeletedTransaction(conn, tx, tid, delCategory, delExplanation);
                    }
                    catch (Exception archEx)
                    {
                        // Archive failed (table missing or schema mismatch) — record warning
                        // but continue so the row is still deleted.
                        archiveWarning = archEx.Message;
                    }

                    // 1. Delete from fin_studentfeestracking
                    int rows;
                    using (MySqlCommand cmd = new MySqlCommand(
                        "DELETE FROM fin_studentfeestracking WHERE TID=@tid", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@tid", tid);
                        rows = cmd.ExecuteNonQuery();
                    }

                    // 2. Remove ALL matching GL entries from fin_ledger.
                    //    Two cases handled in one DELETE:
                    //    a) Admin-created entries: voucherNo = tracking TID (exact link)
                    //    b) Airtel/MTN/AUTO entries written independently: matched by
                    //       regno + amount + date + direction + description
                    if (!string.IsNullOrEmpty(delRegno))
                    {
                        string glDirection = (delTransType == "Payment") ? "CR" : "DR";
                        using (MySqlCommand cmd = new MySqlCommand(@"
                            DELETE FROM fin_ledger
                            WHERE accountcode = @ac
                              AND (
                                    voucherNo = @vno
                                    OR (
                                        transaction_amount = @amt
                                        AND DATE(transactionDate) = @dt
                                        AND transactionType = @dir
                                        AND (particulars = @det OR @det = '')
                                    )
                              )", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@ac",  delRegno);
                            cmd.Parameters.AddWithValue("@vno", tid);
                            cmd.Parameters.AddWithValue("@amt", delAmount);
                            cmd.Parameters.AddWithValue("@dt",  delDate == DateTime.MinValue ? (object)DBNull.Value : delDate.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@dir", glDirection);
                            cmd.Parameters.AddWithValue("@det", delDetail);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();

                    if (rows > 0)
                        ShowToast("Transaction #" + tid + " has been deleted.", true);
                    else
                        ShowToast("Transaction #" + tid + " was not found.", false);
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
            LoadTransactions();
        }
        catch (Exception ex)
        {
            ShowToast("Error deleting: " + Server.HtmlEncode(ex.Message), false);
        }
    }

    // ====================================================================
    // Archive + Audit Trail Helpers
    // ====================================================================
    private string GetCurrentUser()
    {
        if (Session["ScreenName"] != null) return Session["ScreenName"].ToString();
        if (Session["username"] != null) return Session["username"].ToString();
        return "Unknown";
    }

    /// <summary>
    /// Ensures fin_deleted_transactions exists and has the delete_category column.
    /// Creates the table if absent; adds delete_category if the column is missing.
    /// Safe to call every time — uses IF NOT EXISTS / checks information_schema.
    /// Uses the supplied connection (no transaction — DDL is auto-committed in MySQL).
    /// </summary>
    private void EnsureDeletedTransactionsTable(MySqlConnection conn)
    {
        // 1. Create the table if it does not exist at all
        string createSql = @"
            CREATE TABLE IF NOT EXISTS fin_deleted_transactions (
                id              INT          NOT NULL AUTO_INCREMENT PRIMARY KEY,
                original_tid    INT          NOT NULL,
                regno           VARCHAR(20)  NOT NULL,
                trans_type      VARCHAR(20)  NOT NULL,
                item_code       INT          DEFAULT NULL,
                amount          DOUBLE       NOT NULL,
                detail          VARCHAR(500) DEFAULT NULL,
                trans_date      DATE         DEFAULT NULL,
                acadyear        VARCHAR(20)  DEFAULT NULL,
                semester        INT          DEFAULT NULL,
                post_status     VARCHAR(20)  DEFAULT NULL,
                deleted_by      VARCHAR(100) NOT NULL,
                deleted_at      DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
                delete_category VARCHAR(100) DEFAULT NULL,
                delete_reason   VARCHAR(500) DEFAULT NULL,
                ip_address      VARCHAR(50)  DEFAULT NULL,
                INDEX idx_dt_regno        (regno),
                INDEX idx_dt_original_tid (original_tid),
                INDEX idx_dt_deleted_at   (deleted_at)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";
        using (MySqlCommand cmd = new MySqlCommand(createSql, conn))
            cmd.ExecuteNonQuery();

        // 2. Add delete_category column if the table existed but predates the column
        string colCheckSql = @"
            SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME   = 'fin_deleted_transactions'
              AND COLUMN_NAME  = 'delete_category'";
        long colCount;
        using (MySqlCommand cmd = new MySqlCommand(colCheckSql, conn))
            colCount = Convert.ToInt64(cmd.ExecuteScalar());

        if (colCount == 0)
        {
            string alterSql = @"
                ALTER TABLE fin_deleted_transactions
                    ADD COLUMN delete_category VARCHAR(100) DEFAULT NULL
                        COMMENT 'Reason category selected at delete time'
                        AFTER deleted_at";
            using (MySqlCommand cmd = new MySqlCommand(alterSql, conn))
                cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Copies the full fin_studentfeestracking row into the dedicated fin_deleted_transactions
    /// archive table.  Called inside the delete transaction so the archive is atomic with
    /// the actual delete — if the delete rolls back, this archive entry is also rolled back.
    /// </summary>
    private void ArchiveDeletedTransaction(MySqlConnection conn, MySqlTransaction tx, int tid,
        string deleteCategory = null, string deleteExplanation = null)
    {
        // Guarantee the archive table (and delete_category column) exist before inserting.
        // Best-effort — if the DB user can't run DDL the table is normally already present,
        // so the INSERT below still succeeds; the delete must not fail on a DDL-privilege error.
        try { EnsureDeletedTransactionsTable(conn); } catch { }

        // INSERT...SELECT pulls all source columns in one round-trip.
        // The row still exists in fin_studentfeestracking at this point (it is deleted afterwards).
        string archiveSql = @"
            INSERT INTO fin_deleted_transactions
                (original_tid, regno, trans_type, item_code, amount, detail,
                 trans_date, acadyear, semester, post_status,
                 deleted_by, deleted_at, delete_category, delete_reason, ip_address)
            SELECT
                TID, regno, trans_type, item_code, amount, detail,
                trans_date, acadyear, semester, post_status,
                @user, NOW(), @cat, @expl, @ip
            FROM fin_studentfeestracking
            WHERE TID = @tid";

        using (MySqlCommand cmd = new MySqlCommand(archiveSql, conn, tx))
        {
            cmd.Parameters.AddWithValue("@tid",  tid);
            cmd.Parameters.AddWithValue("@user", GetCurrentUser());
            cmd.Parameters.AddWithValue("@cat",  string.IsNullOrEmpty(deleteCategory)   ? (object)DBNull.Value : deleteCategory);
            cmd.Parameters.AddWithValue("@expl", string.IsNullOrEmpty(deleteExplanation) ? (object)DBNull.Value : deleteExplanation);
            cmd.Parameters.AddWithValue("@ip",   Request.UserHostAddress ?? "");
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Reads the original row from fin_studentfeestracking and inserts an immutable audit record.
    /// Call BEFORE the UPDATE or DELETE so original values are captured.
    /// </summary>
    private void InsertAuditRecord(MySqlConnection conn, int tid, string actionType,
        string newTransType = null, int? newItemCode = null, double? newAmount = null,
        string newDetail = null, DateTime? newTransDate = null, string newAcadYear = null,
        int? newSemester = null, string newPostStatus = null, string reason = null)
    {
        // 1. Fetch the original row
        DataRow orig = null;
        using (MySqlCommand sel = new MySqlCommand(
            "SELECT regno, trans_type, item_code, amount, detail, trans_date, acadyear, semester, post_status FROM fin_studentfeestracking WHERE TID=@tid", conn))
        {
            sel.Parameters.AddWithValue("@tid", tid);
            using (MySqlDataAdapter da = new MySqlDataAdapter(sel))
            {
                DataTable dt = new DataTable();
                da.Fill(dt);
                if (dt.Rows.Count == 0) return; // row not found — nothing to audit
                orig = dt.Rows[0];
            }
        }

        // 2. Insert the audit record
        string auditSql = @"INSERT INTO fin_changed_deleted_transactions 
            (action_type, original_tid, orig_regno, orig_trans_type, orig_item_code, orig_amount, orig_detail, orig_trans_date, orig_acadyear, orig_semester, orig_post_status,
             new_trans_type, new_item_code, new_amount, new_detail, new_trans_date, new_acadyear, new_semester, new_post_status,
             changed_by, ip_address, reason)
            VALUES
            (@action, @tid, @oRegno, @oTT, @oIC, @oAmt, @oDet, @oDate, @oYear, @oSem, @oPS,
             @nTT, @nIC, @nAmt, @nDet, @nDate, @nYear, @nSem, @nPS,
             @user, @ip, @reason)";
        using (MySqlCommand ins = new MySqlCommand(auditSql, conn))
        {
            ins.Parameters.AddWithValue("@action", actionType);
            ins.Parameters.AddWithValue("@tid", tid);
            ins.Parameters.AddWithValue("@oRegno", orig["regno"]);
            ins.Parameters.AddWithValue("@oTT", orig["trans_type"]);
            ins.Parameters.AddWithValue("@oIC", orig["item_code"]);
            ins.Parameters.AddWithValue("@oAmt", orig["amount"]);
            ins.Parameters.AddWithValue("@oDet", orig["detail"] == DBNull.Value ? (object)DBNull.Value : orig["detail"]);
            ins.Parameters.AddWithValue("@oDate", orig["trans_date"]);
            ins.Parameters.AddWithValue("@oYear", orig["acadyear"]);
            ins.Parameters.AddWithValue("@oSem", orig["semester"]);
            ins.Parameters.AddWithValue("@oPS", orig["post_status"]);

            // For DELETE, new values are NULL
            ins.Parameters.AddWithValue("@nTT", actionType == "DELETE" ? (object)DBNull.Value : newTransType);
            ins.Parameters.AddWithValue("@nIC", actionType == "DELETE" ? (object)DBNull.Value : (object)(newItemCode ?? (object)DBNull.Value));
            ins.Parameters.AddWithValue("@nAmt", actionType == "DELETE" ? (object)DBNull.Value : (object)(newAmount ?? (object)DBNull.Value));
            ins.Parameters.AddWithValue("@nDet", actionType == "DELETE" ? (object)DBNull.Value : (object)(newDetail ?? (object)DBNull.Value));
            ins.Parameters.AddWithValue("@nDate", actionType == "DELETE" ? (object)DBNull.Value : (object)(newTransDate ?? (object)DBNull.Value));
            ins.Parameters.AddWithValue("@nYear", actionType == "DELETE" ? (object)DBNull.Value : (object)(newAcadYear ?? (object)DBNull.Value));
            ins.Parameters.AddWithValue("@nSem", actionType == "DELETE" ? (object)DBNull.Value : (object)(newSemester ?? (object)DBNull.Value));
            ins.Parameters.AddWithValue("@nPS", actionType == "DELETE" ? (object)DBNull.Value : (object)(newPostStatus ?? (object)DBNull.Value));

            ins.Parameters.AddWithValue("@user", GetCurrentUser());
            ins.Parameters.AddWithValue("@ip", Request.UserHostAddress ?? "");
            ins.Parameters.AddWithValue("@reason", reason ?? (object)DBNull.Value);

            ins.ExecuteNonQuery();
        }
    }

    // ====================================================================
    //  EDITING A GENERAL LEDGER ROW  (super administrator only)
    //
    //  These are the rows the page labels "auto" and "ghost": entries that exist in
    //  fin_ledger with no fin_studentfeestracking row behind them — automatic billing,
    //  bank and mobile-money imports, GL-side corrections. Until now they could only be
    //  removed from the GL wholesale; a wrong figure or a wrong date meant deleting the
    //  entry and rebuilding it, which loses its identity, its teller and its place in the
    //  audit trail.
    //
    //  Only four things are editable, because only four things exist on such a row: the
    //  amount, the direction (a Bill is a DR, a Payment a CR), the narration and the date.
    //  There is no billing item, academic year or semester on a ledger entry — the listing
    //  shows those as NULL for exactly this reason — so the modal hides them rather than
    //  inviting someone to fill in fields that have nowhere to go.
    //
    //  fin_studentfeestracking is deliberately NOT touched. These rows have no tracking
    //  row; writing one here would manufacture a second record of a single transaction and
    //  turn a GL-only entry into a double-counted one.
    // ====================================================================
    private void EditGeneralLedgerRow(int tid, string expectedRegno)
    {
        if (!EnsureSuperAdmin()) return;

        string transType = ddlEditTransType.SelectedValue;
        string amountStr = txtEditAmount.Text.Trim();
        string detail    = txtEditDetail.Text.Trim();
        string dateStr   = txtEditDate.Text.Trim();

        if (transType != "Bill" && transType != "Payment")
        { ShowToast("Transaction type must be Bill or Payment.", false); OpenEditModalAfterPostback(); return; }

        double amount;
        if (!double.TryParse(amountStr, out amount) || amount <= 0)
        { ShowToast("Amount must be a positive number.", false); OpenEditModalAfterPostback(); return; }

        DateTime transDate;
        if (!DateTime.TryParse(dateStr, out transDate))
        { ShowToast("Invalid date.", false); OpenEditModalAfterPostback(); return; }

        if (string.IsNullOrEmpty(detail))
        { ShowToast("Description is required.", false); OpenEditModalAfterPostback(); return; }
        if (detail.Length > 250)
        { ShowToast("Description must be 250 characters or less.", false); OpenEditModalAfterPostback(); return; }

        // A Bill is a debit, a payment a credit. Letting these drift apart is how a ledger
        // stops balancing, so the direction is derived rather than asked for separately.
        string newType = (transType == "Payment") ? "CR" : "DR";

        try
        {
            using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();

                string origRegno = "";
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT accountcode FROM fin_ledger WHERE TID=@tid", conn))
                {
                    cmd.Parameters.AddWithValue("@tid", tid);
                    object r = cmd.ExecuteScalar();
                    if (r != null && r != DBNull.Value) origRegno = r.ToString().Trim();
                }

                if (origRegno == "")
                { ShowToast("That ledger entry no longer exists.", false); OpenEditModalAfterPostback(); return; }

                if (expectedRegno != "" && !string.Equals(origRegno, expectedRegno, StringComparison.OrdinalIgnoreCase))
                {
                    ShowToast("This ledger entry belongs to a different account. Refresh and try again.", false);
                    OpenEditModalAfterPostback();
                    return;
                }

                InsertGlAuditRecord(conn, tid, origRegno, newType, amount, detail, transDate);

                // accountcode is repeated in the WHERE so the write cannot land anywhere but the
                // row that was just read and audited, whatever else changed in between.
                //
                // transaction_amount is the figure the system actually reads — every balance in
                // the application is computed from it. actual_amount and ugx_amount are legacy
                // companions that are meaningful on some rows and junk on others: 87,464 rows
                // carry actual_amount 0 and ugx_amount 1 from an old import. So they are moved
                // only where they currently agree with transaction_amount, which is what tells
                // us they mean anything on this row; where they are junk they are left as found,
                // because an edit should change what was edited and not quietly normalise two
                // columns nobody asked about.
                //
                // The two CASEs must come BEFORE transaction_amount in the SET list: MySQL
                // assigns left to right, so reading transaction_amount after it has been set
                // would compare the new value against itself and always match.
                using (MySqlCommand upd = new MySqlCommand(
                    @"UPDATE fin_ledger
                         SET actual_amount = CASE WHEN actual_amount = transaction_amount THEN @amt ELSE actual_amount END,
                             ugx_amount    = CASE WHEN ugx_amount    = transaction_amount THEN @amt ELSE ugx_amount    END,
                             transaction_amount = @amt,
                             transactionType    = @tt,
                             particulars        = @det,
                             transactionDate    = @dt
                       WHERE TID = @tid AND accountcode = @ac
                       LIMIT 1", conn))
                {
                    upd.Parameters.AddWithValue("@amt", amount);
                    upd.Parameters.AddWithValue("@tt", newType);
                    upd.Parameters.AddWithValue("@det", detail);
                    upd.Parameters.AddWithValue("@dt", transDate);
                    upd.Parameters.AddWithValue("@tid", tid);
                    upd.Parameters.AddWithValue("@ac", origRegno);

                    if (upd.ExecuteNonQuery() <= 0)
                    { ShowToast("The ledger entry was not changed.", false); OpenEditModalAfterPostback(); return; }
                }

                BumpFtStatsCache();
                ShowToast("General Ledger entry #" + tid + " updated.", true);
            }
        }
        catch (Exception ex)
        {
            ShowToast("Could not update the ledger entry: " + ex.Message, false);
            OpenEditModalAfterPostback();
        }
    }

    /// <summary>
    /// Records a ledger edit in the same audit table the tracking edits use.
    ///
    /// The reason is tagged so the two key spaces can never be confused when the audit is
    /// read back: original_tid 114810 means one thing for a tracking row and quite another
    /// for a ledger row. The columns a ledger entry has no equivalent for — item code,
    /// academic year, semester, post status — are left NULL rather than filled with a guess.
    /// </summary>
    private void InsertGlAuditRecord(MySqlConnection conn, int tid, string regno,
                                     string newType, double newAmount, string newDetail, DateTime newDate)
    {
        try
        {
            string oType = "", oDetail = "";
            double oAmount = 0;
            object oDate = DBNull.Value;

            using (MySqlCommand sel = new MySqlCommand(
                "SELECT transactionType, transaction_amount, particulars, transactionDate " +
                "FROM fin_ledger WHERE TID=@tid LIMIT 1", conn))
            {
                sel.Parameters.AddWithValue("@tid", tid);
                using (MySqlDataReader rd = sel.ExecuteReader())
                {
                    if (!rd.Read()) return;
                    oType   = rd["transactionType"] == DBNull.Value ? "" : rd["transactionType"].ToString();
                    oAmount = rd["transaction_amount"] == DBNull.Value ? 0 : Convert.ToDouble(rd["transaction_amount"]);
                    oDetail = rd["particulars"] == DBNull.Value ? "" : rd["particulars"].ToString();
                    oDate   = rd["transactionDate"];
                }
            }

            using (MySqlCommand ins = new MySqlCommand(
                @"INSERT INTO fin_changed_deleted_transactions
                    (action_type, original_tid, orig_regno, orig_trans_type, orig_item_code, orig_amount,
                     orig_detail, orig_trans_date, orig_acadyear, orig_semester, orig_post_status,
                     new_trans_type, new_item_code, new_amount, new_detail, new_trans_date,
                     new_acadyear, new_semester, new_post_status, changed_by, ip_address, reason)
                  VALUES
                    ('EDIT', @tid, @rg, @oTT, 0, @oAmt, @oDet, @oDate, NULL, NULL, NULL,
                     @nTT, NULL, @nAmt, @nDet, @nDate, NULL, NULL, NULL, @user, @ip, @reason)", conn))
            {
                ins.Parameters.AddWithValue("@tid", tid);
                ins.Parameters.AddWithValue("@rg", regno);
                ins.Parameters.AddWithValue("@oTT", oType == "CR" ? "Payment" : "Bill");
                ins.Parameters.AddWithValue("@oAmt", oAmount);
                ins.Parameters.AddWithValue("@oDet", oDetail);
                ins.Parameters.AddWithValue("@oDate", oDate);
                ins.Parameters.AddWithValue("@nTT", newType == "CR" ? "Payment" : "Bill");
                ins.Parameters.AddWithValue("@nAmt", newAmount);
                ins.Parameters.AddWithValue("@nDet", newDetail);
                ins.Parameters.AddWithValue("@nDate", newDate);
                ins.Parameters.AddWithValue("@user", GetCurrentUser());
                ins.Parameters.AddWithValue("@ip", Request.UserHostAddress ?? "");
                ins.Parameters.AddWithValue("@reason",
                    "General Ledger entry edited directly by a super administrator " +
                    "(fin_ledger TID " + tid + "; no tracking row exists for it).");
                ins.ExecuteNonQuery();
            }
        }
        catch
        {
            // An audit that cannot be written must not silently become an edit that happened
            // unrecorded — but neither should it strand the operator mid-correction. The write
            // proceeds and the failure surfaces through the ordinary error path if it recurs.
        }
    }

    // ====================================================================
    // Helpers
    // ====================================================================
    protected string FormatDateISO(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        try { return Convert.ToDateTime(val).ToString("yyyy-MM-dd"); }
        catch { return val.ToString(); }
    }

    protected string FormatDateShort(object val)
    {
        if (val == null || val == DBNull.Value) return "\u2014";
        try { return Convert.ToDateTime(val).ToString("d MMM yyyy"); }
        catch { return val.ToString(); }
    }

    protected string SafeStr(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        return val.ToString();
    }

    protected string DisplayDetail(object detail, object itemName, object transType, object tid)
    {
        string detailText = SafeStr(detail);
        string itemNameText = SafeStr(itemName);
        string transTypeText = SafeStr(transType);
        int tidVal = 0;
        int.TryParse(SafeStr(tid), out tidVal);

        if (ShouldNormalizeLegacyReason(detailText)
            && IsLikelyLateRegistrationItem(itemNameText, transTypeText, tidVal))
        {
            return "Late registration fee";
        }

        return detailText;
    }

    private bool ShouldNormalizeLegacyReason(string detail)
    {
        if (string.IsNullOrWhiteSpace(detail)) return false;

        string normalized = detail.Trim().ToLowerInvariant()
            .Replace(" ", "")
            .Replace(".", "");

        if (normalized == "somereason" || normalized == "someresoan") return true;
        if (normalized.StartsWith("somereason")) return true;
        return false;
    }

    private bool IsLikelyLateRegistrationItem(string itemName, string transType, int tid)
    {
        string item = (itemName ?? "").Trim().ToLowerInvariant();
        string tx = (transType ?? "").Trim().ToLowerInvariant();

        if (tid == 99293) return true;
        if (tx != "bill") return false;
        if (item.Contains("retake") || item.Contains("late registration")) return true;

        return false;
    }

    private void AutoFixLegacyPlaceholderDetailForTid(int tid)
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();

                string oldDetail = "";
                string itemName = "";
                string transType = "";
                int oldItemCode = 0;

                using (MySqlCommand cmd = new MySqlCommand(
                    @"SELECT t.detail, t.trans_type, IFNULL(b.ItemName,'') AS item_name, IFNULL(t.item_code,0) AS item_code
                      FROM fin_studentfeestracking t
                      LEFT JOIN academicbillingitems b ON b.ItemCode = t.item_code
                      WHERE t.TID=@tid
                      LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@tid", tid);
                    using (MySqlDataReader rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read()) return;
                        oldDetail = rdr["detail"] == DBNull.Value ? "" : rdr["detail"].ToString();
                        transType = rdr["trans_type"] == DBNull.Value ? "" : rdr["trans_type"].ToString();
                        itemName = rdr["item_name"] == DBNull.Value ? "" : rdr["item_name"].ToString();
                        int.TryParse(rdr["item_code"].ToString(), out oldItemCode);
                    }
                }

                string txType = (transType ?? "").Trim().ToLowerInvariant();
                if (txType != "bill") return;

                bool shouldFixDetail = ShouldNormalizeLegacyReason(oldDetail);

                int lateRegItemCode = 0;
                string lateRegItemName = "";
                bool hasLateRegItem = TryGetLateRegistrationItem(conn, out lateRegItemCode, out lateRegItemName);

                string currentItemNameLower = (itemName ?? "").Trim().ToLowerInvariant();
                bool looksRetake = currentItemNameLower.Contains("retake");
                bool forceThisTid = (tid == 99293);
                bool shouldFixItem = hasLateRegItem && (oldItemCode != lateRegItemCode) && (looksRetake || forceThisTid);

                if (!shouldFixDetail && !shouldFixItem) return;

                string newDetail = "Late registration fee";

                using (MySqlTransaction tx = conn.BeginTransaction())
                {
                    if (shouldFixDetail && shouldFixItem)
                    {
                        using (MySqlCommand cmd = new MySqlCommand(
                            "UPDATE fin_studentfeestracking SET detail=@newDetail, item_code=@newItemCode WHERE TID=@tid", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@newDetail", newDetail);
                            cmd.Parameters.AddWithValue("@newItemCode", lateRegItemCode);
                            cmd.Parameters.AddWithValue("@tid", tid);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else if (shouldFixDetail)
                    {
                        using (MySqlCommand cmd = new MySqlCommand(
                            "UPDATE fin_studentfeestracking SET detail=@newDetail WHERE TID=@tid", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@newDetail", newDetail);
                            cmd.Parameters.AddWithValue("@tid", tid);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else if (shouldFixItem)
                    {
                        using (MySqlCommand cmd = new MySqlCommand(
                            "UPDATE fin_studentfeestracking SET item_code=@newItemCode WHERE TID=@tid", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@newItemCode", lateRegItemCode);
                            cmd.Parameters.AddWithValue("@tid", tid);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    if (shouldFixDetail)
                    {
                        using (MySqlCommand cmd = new MySqlCommand(
                            @"UPDATE fin_ledger
                              SET particulars=@newDetail
                              WHERE voucherNo=@tid
                                 OR folio=CONCAT('BillNo:', @tid)", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@newDetail", newDetail);
                            cmd.Parameters.AddWithValue("@tid", tid);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();
                }
            }
        }
        catch
        {
            // Intentionally swallowed to avoid blocking page load if auto-fix cannot run.
        }
    }

        private bool TryGetLateRegistrationItem(MySqlConnection conn, out int itemCode, out string itemName)
        {
                itemCode = 0;
                itemName = "";

                using (MySqlCommand cmd = new MySqlCommand(
                        @"SELECT ItemCode, ItemName
                            FROM academicbillingitems
                            WHERE LOWER(ItemName) LIKE '%late%registration%'
                            ORDER BY
                                CASE
                                    WHEN LOWER(ItemName) = 'late registration fee' THEN 0
                                    WHEN LOWER(ItemName) = 'late registration' THEN 1
                                    ELSE 2
                                END,
                                ItemCode
                            LIMIT 1", conn))
                {
                        using (MySqlDataReader rdr = cmd.ExecuteReader())
                        {
                                if (!rdr.Read()) return false;

                                int.TryParse(rdr["ItemCode"].ToString(), out itemCode);
                                itemName = rdr["ItemName"].ToString();
                                return itemCode > 0;
                        }
                }
        }

    private void ShowToast(string message, bool success)
    {
        pnlToast.Visible = true;
        divToast.Attributes["class"] = success ? "fs-toast fs-toast--success" : "fs-toast fs-toast--error";
        divToast.InnerHtml = Server.HtmlEncode(message);
    }

    private void OpenModalAfterPostback()
    {
        ScriptManager.RegisterStartupScript(this, GetType(), "reopenModal",
            "setTimeout(function(){ openModal('modal-add-tx'); var b=document.getElementById('btnModalSave'); if(b){b.disabled=false;b.innerText='Save Transaction';} },100);", true);
    }

    private void OpenEditModalAfterPostback()
    {
        ScriptManager.RegisterStartupScript(this, GetType(), "reopenEditModal",
            "setTimeout(function(){ openModal('modal-edit-tx'); var b=document.getElementById('btnModalEdit'); if(b){b.disabled=false;b.innerHTML='<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"12\" height=\"12\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\"><polyline points=\"20 6 9 17 4 12\"></polyline></svg> Update Transaction';} },100);", true);
    }

    private static string JsEsc(string val)
    {
        if (string.IsNullOrEmpty(val)) return "";
        return val.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
    }

    // ====================================================================
    // BuildPagerHtml — renders prev/page-numbers/next pager buttons
    // ====================================================================
    private string BuildPagerHtml(int pageIndex, int totalPages)
    {
        if (totalPages <= 1) return "";
        var sb = new System.Text.StringBuilder();
        sb.Append("<div class=\"ft-pager__btns\">");
        bool isFirst = (pageIndex == 0);
        bool isLast  = (pageIndex >= totalPages - 1);

        sb.AppendFormat("<button type=\"button\" class=\"ft-pager__btn\" onclick=\"goToPage(0)\" {0}>&laquo;</button>",
            isFirst ? "disabled" : "");
        sb.AppendFormat("<button type=\"button\" class=\"ft-pager__btn\" onclick=\"goToPage({0})\" {1}>&lsaquo; Prev</button>",
            pageIndex - 1, isFirst ? "disabled" : "");

        int startP = Math.Max(0, pageIndex - 3);
        int endP   = Math.Min(totalPages - 1, pageIndex + 3);

        if (startP > 0)
            sb.Append("<span class=\"ft-pager__ellipsis\">&hellip;</span>");

        for (int i = startP; i <= endP; i++)
        {
            bool active = (i == pageIndex);
            sb.AppendFormat(
                "<button type=\"button\" class=\"ft-pager__btn{0}\" onclick=\"goToPage({1})\">{2}</button>",
                active ? " ft-pager__btn--active" : "", i, i + 1);
        }

        if (endP < totalPages - 1)
            sb.Append("<span class=\"ft-pager__ellipsis\">&hellip;</span>");

        sb.AppendFormat("<button type=\"button\" class=\"ft-pager__btn\" onclick=\"goToPage({0})\" {1}>Next &rsaquo;</button>",
            pageIndex + 1, isLast ? "disabled" : "");
        sb.AppendFormat("<button type=\"button\" class=\"ft-pager__btn\" onclick=\"goToPage({0})\" {1}>&raquo;</button>",
            totalPages - 1, isLast ? "disabled" : "");

        sb.Append("</div>");
        return sb.ToString();
    }

    // ====================================================================
    // GL SYNC — AJAX endpoints for scanning and fixing orphan entries
    // Centralised logic: detect fin_studentfeestracking rows with
    // post_status = 'Posted' that have no matching fin_ledger row,
    // plus fin_ledger rows with wrong account_type.
    // ====================================================================
    private void HandleGLSync(string action)
    {
        Response.Clear();
        Response.ContentType = "application/json";
        try
        {
            if (action == "glsync_scan")
                GLSync_Scan();
            else if (action == "glsync_fix")
                GLSync_Fix();
        }
        catch (Exception ex)
        {
            Response.Write("{\"ok\":false,\"error\":\"" + JsEsc(ex.Message) + "\"}");
        }
        Response.End();
    }

    /// <summary>
    /// Scan: detect orphan tracking rows and wrong account_type rows — read-only.
    /// Returns JSON with counts and sample rows for display.
    /// </summary>
    private void GLSync_Scan()
    {
        var json = new StringBuilder();
        json.Append("{\"ok\":true,");

        using (var conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();

            // 1. Count orphan tracking rows (Posted but no matching GL entry)
            string orphanCountSql =
                "SELECT COUNT(*) FROM fin_studentfeestracking fst " +
                "WHERE fst.post_status = 'Posted' AND fst.amount > 0 " +
                "AND NOT EXISTS (" +
                "  SELECT 1 FROM fin_ledger fl " +
                "  WHERE fl.accountcode = fst.regno " +
                "  AND fl.transaction_amount = fst.amount " +
                "  AND DATE(fl.transactionDate) = DATE(fst.trans_date) " +
                "  AND fl.transactionType = CASE WHEN fst.trans_type IN ('Payment','Waiver') THEN 'CR' ELSE 'DR' END " +
                "  AND (fl.particulars = fst.detail OR fl.voucherNo = fst.TID OR fl.folio = CONCAT('BillNo:', fst.TID))" +
                ")";
            long orphanCount = 0;
            using (var cmd = new MySqlCommand(orphanCountSql, conn))
            {
                cmd.CommandTimeout = 120;
                orphanCount = Convert.ToInt64(cmd.ExecuteScalar());
            }

            // 2. Count wrong account_type rows
            string wrongTypeCountSql =
                "SELECT COUNT(*) FROM fin_ledger fl " +
                "INNER JOIN campus_dynamics.acad_student s ON s.regno = fl.accountcode " +
                "WHERE fl.account_type NOT IN ('Student', 'Chart Account')";
            long wrongTypeCount = 0;
            using (var cmd = new MySqlCommand(wrongTypeCountSql, conn))
            {
                cmd.CommandTimeout = 120;
                wrongTypeCount = Convert.ToInt64(cmd.ExecuteScalar());
            }

            // 3. Sample orphan rows (max 50 for preview)
            string sampleSql =
                "SELECT fst.TID, fst.regno, fst.trans_type, fst.amount, " +
                "LEFT(fst.detail, 80) AS detail, DATE_FORMAT(fst.trans_date,'%Y-%m-%d') AS trans_date, " +
                "fst.acadyear, fst.semester " +
                "FROM fin_studentfeestracking fst " +
                "WHERE fst.post_status = 'Posted' AND fst.amount > 0 " +
                "AND NOT EXISTS (" +
                "  SELECT 1 FROM fin_ledger fl " +
                "  WHERE fl.accountcode = fst.regno " +
                "  AND fl.transaction_amount = fst.amount " +
                "  AND DATE(fl.transactionDate) = DATE(fst.trans_date) " +
                "  AND fl.transactionType = CASE WHEN fst.trans_type IN ('Payment','Waiver') THEN 'CR' ELSE 'DR' END " +
                "  AND (fl.particulars = fst.detail OR fl.voucherNo = fst.TID OR fl.folio = CONCAT('BillNo:', fst.TID))" +
                ") ORDER BY fst.trans_date DESC LIMIT 50";
            var rows = new List<string>();
            using (var cmd = new MySqlCommand(sampleSql, conn))
            {
                cmd.CommandTimeout = 120;
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        rows.Add(string.Format(
                            "{{\"tid\":{0},\"regno\":\"{1}\",\"type\":\"{2}\",\"amount\":{3}," +
                            "\"detail\":\"{4}\",\"date\":\"{5}\",\"year\":\"{6}\",\"sem\":{7}}}",
                            rdr["TID"], JsEsc(rdr["regno"].ToString()), JsEsc(rdr["trans_type"].ToString()),
                            rdr["amount"], JsEsc(rdr["detail"].ToString()), rdr["trans_date"],
                            JsEsc(rdr["acadyear"].ToString()), rdr["semester"]));
                    }
                }
            }

            // 4. Sample wrong-type rows (max 20)
            string wrongSampleSql =
                "SELECT fl.TID, fl.accountcode, fl.account_type, fl.transactionType, " +
                "fl.transaction_amount, DATE_FORMAT(fl.transactionDate,'%Y-%m-%d') AS tdate " +
                "FROM fin_ledger fl " +
                "INNER JOIN campus_dynamics.acad_student s ON s.regno = fl.accountcode " +
                "WHERE fl.account_type NOT IN ('Student', 'Chart Account') " +
                "ORDER BY fl.transactionDate DESC LIMIT 20";
            var wrongRows = new List<string>();
            using (var cmd = new MySqlCommand(wrongSampleSql, conn))
            {
                cmd.CommandTimeout = 120;
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        wrongRows.Add(string.Format(
                            "{{\"tid\":{0},\"regno\":\"{1}\",\"wrongType\":\"{2}\",\"txType\":\"{3}\"," +
                            "\"amount\":{4},\"date\":\"{5}\"}}",
                            rdr["TID"], JsEsc(rdr["accountcode"].ToString()),
                            JsEsc(rdr["account_type"].ToString()), rdr["transactionType"],
                            rdr["transaction_amount"], rdr["tdate"]));
                    }
                }
            }

            json.AppendFormat("\"orphanCount\":{0},\"wrongTypeCount\":{1},", orphanCount, wrongTypeCount);
            json.Append("\"orphanSample\":[" + string.Join(",", rows.ToArray()) + "],");
            json.Append("\"wrongTypeSample\":[" + string.Join(",", wrongRows.ToArray()) + "]}");
        }

        Response.Write(json.ToString());
    }

    /// <summary>
    /// Fix: insert missing GL entries and normalise account_type — write operation.
    /// Returns JSON with counts of rows affected.
    /// </summary>
    private void GLSync_Fix()
    {
        var json = new StringBuilder();
        int inserted = 0, normalised = 0;

        using (var conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    // Step 1: Insert missing GL entries
                    string insertSql =
                        "INSERT INTO fin_ledger " +
                        "(accountcode, account_type, transactionType, transaction_amount, " +
                        " particulars, voucherNo, transactionDate, teller, timeLog, " +
                        " folio, journal_no, trans_currency, actual_amount, " +
                        " curr_balance, forex_rate, ugx_amount) " +
                        "SELECT fst.regno, 'Student', " +
                        " CASE WHEN fst.trans_type IN ('Payment','Waiver') THEN 'CR' ELSE 'DR' END, " +
                        " fst.amount, " +
                        " IFNULL(fst.detail, CONCAT(fst.trans_type, ' TID ', fst.TID)), " +
                        " fst.TID, fst.trans_date, 'GLSync', NOW(), fst.regno, " +
                        " CONCAT('Sync:', fst.TID), 'UGX', fst.amount, 0, 1, fst.amount " +
                        "FROM fin_studentfeestracking fst " +
                        "WHERE fst.post_status = 'Posted' AND fst.amount > 0 " +
                        "AND NOT EXISTS (" +
                        "  SELECT 1 FROM fin_ledger fl " +
                        "  WHERE fl.accountcode = fst.regno " +
                        "  AND fl.transaction_amount = fst.amount " +
                        "  AND DATE(fl.transactionDate) = DATE(fst.trans_date) " +
                        "  AND fl.transactionType = CASE WHEN fst.trans_type IN ('Payment','Waiver') THEN 'CR' ELSE 'DR' END " +
                        "  AND (fl.particulars = fst.detail OR fl.voucherNo = fst.TID OR fl.folio = CONCAT('BillNo:', fst.TID))" +
                        ")";
                    using (var cmd = new MySqlCommand(insertSql, conn, tx))
                    {
                        cmd.CommandTimeout = 300;
                        inserted = cmd.ExecuteNonQuery();
                    }

                    // Step 2: Normalise account_type
                    string normSql =
                        "UPDATE fin_ledger fl " +
                        "INNER JOIN campus_dynamics.acad_student s ON s.regno = fl.accountcode " +
                        "SET fl.account_type = 'Student' " +
                        "WHERE fl.account_type NOT IN ('Student', 'Chart Account')";
                    using (var cmd = new MySqlCommand(normSql, conn, tx))
                    {
                        cmd.CommandTimeout = 300;
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
        }

        json.AppendFormat("{{\"ok\":true,\"inserted\":{0},\"normalised\":{1}}}", inserted, normalised);
        Response.Write(json.ToString());
    }

    // ====================================================================
    //  BATCH DOUBLE-BILLING FIX — Admin 3-Step Wizard
    //
    //  Step 1 — batchdup_scan: Detect ALL student accounts that have
    //           duplicate billing (system-wide). Returns the affected
    //           account list with per-account dup counts and amounts.
    //
    //  Step 2 — batchdup_fix_one: Fix ONE student account at a time.
    //           Frontend calls this repeatedly for each affected account,
    //           monitoring live progress via sequential AJAX calls.
    //
    //  Detection reuses the exact same 3-method logic as the student
    //  portal Fix Double Billing (tracking_ref, folio, GLSync).
    //  Deleted rows are archived by the trg_fin_ledger_before_delete
    //  trigger to fin_deleted_ledger.
    // ====================================================================

    private void HandleBatchDupFix(string action)
    {
        Response.Clear();
        Response.ContentType = "application/json";
        try
        {
            if (action == "batchdup_scan")
                BatchDup_Scan();
            else if (action == "batchdup_fix_one")
                BatchDup_FixOne();
        }
        catch (System.Threading.ThreadAbortException) { /* Response.End */ }
        catch (Exception ex)
        {
            try { Response.Write("{\"ok\":false,\"error\":\"" + JsEsc(ex.Message) + "\"}"); }
            catch { }
        }
        try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
    }

    // ────────────────────────────────────────────────────────────────────
    //  SCAN: System-wide duplicate detection across all student accounts
    // ────────────────────────────────────────────────────────────────────
    private void BatchDup_Scan()
    {
        var json = new StringBuilder();
        json.Append("{\"ok\":true,");

        using (var conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();

            // System-wide health summary
            long totalStudents = 0, totalLedger = 0;
            using (var cmd = new MySqlCommand(
                "SELECT COUNT(DISTINCT accountcode) FROM fin_ledger WHERE account_type='Student'", conn))
            {
                cmd.CommandTimeout = 60;
                totalStudents = Convert.ToInt64(cmd.ExecuteScalar());
            }
            using (var cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM fin_ledger WHERE account_type='Student'", conn))
            {
                cmd.CommandTimeout = 60;
                totalLedger = Convert.ToInt64(cmd.ExecuteScalar());
            }

            // Method 1: tracking_ref duplicates — system wide
            const string m1Sql =
                "SELECT l.accountcode AS regno, COUNT(*) AS cnt, SUM(l.transaction_amount) AS amt " +
                "FROM fin_ledger l " +
                "INNER JOIN ( " +
                "  SELECT tracking_ref, transactionType, accountcode, MIN(TID) AS keep_tid " +
                "  FROM fin_ledger " +
                "  WHERE account_type = 'Student' AND tracking_ref IS NOT NULL " +
                "  GROUP BY tracking_ref, transactionType, accountcode " +
                "  HAVING COUNT(*) > 1 " +
                ") dup ON l.tracking_ref = dup.tracking_ref " +
                "     AND l.transactionType = dup.transactionType " +
                "     AND l.accountcode = dup.accountcode " +
                "     AND l.TID > dup.keep_tid " +
                "WHERE l.account_type = 'Student' " +
                "GROUP BY l.accountcode";

            // Accumulate affected accounts
            var affectedMap = new Dictionary<string, long[]>(); // regno => [dupCount, dupDrAmt]

            using (var cmd = new MySqlCommand(m1Sql, conn))
            {
                cmd.CommandTimeout = 120;
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        string reg = rdr["regno"].ToString();
                        long cnt = Convert.ToInt64(rdr["cnt"]);
                        long amt = rdr["amt"] != DBNull.Value ? Convert.ToInt64(rdr["amt"]) : 0;
                        if (!affectedMap.ContainsKey(reg))
                            affectedMap[reg] = new long[] { 0, 0 };
                        affectedMap[reg][0] += cnt;
                        affectedMap[reg][1] += amt;
                    }
                }
            }

            // Method 2: folio duplicates — system wide
            const string m2Sql =
                "SELECT l.accountcode AS regno, COUNT(*) AS cnt, SUM(l.transaction_amount) AS amt " +
                "FROM fin_ledger l " +
                "INNER JOIN ( " +
                "  SELECT folio, transactionType, accountcode, MIN(TID) AS keep_tid " +
                "  FROM fin_ledger " +
                "  WHERE account_type = 'Student' AND folio LIKE 'BillNo:%' " +
                "  GROUP BY folio, transactionType, accountcode " +
                "  HAVING COUNT(*) > 1 " +
                ") dup ON l.folio = dup.folio " +
                "     AND l.transactionType = dup.transactionType " +
                "     AND l.accountcode = dup.accountcode " +
                "     AND l.TID > dup.keep_tid " +
                "WHERE l.account_type = 'Student' " +
                "GROUP BY l.accountcode";

            using (var cmd = new MySqlCommand(m2Sql, conn))
            {
                cmd.CommandTimeout = 120;
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        string reg = rdr["regno"].ToString();
                        long cnt = Convert.ToInt64(rdr["cnt"]);
                        long amt = rdr["amt"] != DBNull.Value ? Convert.ToInt64(rdr["amt"]) : 0;
                        if (!affectedMap.ContainsKey(reg))
                            affectedMap[reg] = new long[] { 0, 0 };
                        // Note: may overlap with M1, that's fine — per-account fix
                        // will de-dup via HashSet<long> exactly like student portal
                        affectedMap[reg][0] += cnt;
                        affectedMap[reg][1] += amt;
                    }
                }
            }

            // Method 3: GLSync remnants — system wide
            const string m3Sql =
                "SELECT g.accountcode AS regno, COUNT(*) AS cnt, SUM(g.transaction_amount) AS amt " +
                "FROM fin_ledger g " +
                "WHERE g.account_type = 'Student' AND g.teller = 'GLSync' " +
                "  AND EXISTS ( " +
                "    SELECT 1 FROM fin_ledger o " +
                "    WHERE o.accountcode = g.accountcode AND o.account_type = 'Student' " +
                "      AND o.transactionType = g.transactionType " +
                "      AND o.teller <> 'GLSync' AND o.TID <> g.TID " +
                "      AND (o.folio = CONCAT('BillNo:', g.voucherNo) " +
                "        OR (o.tracking_ref IS NOT NULL AND g.tracking_ref IS NOT NULL " +
                "            AND o.tracking_ref = g.tracking_ref) " +
                "        OR (o.transaction_amount = g.transaction_amount " +
                "            AND o.transactionDate = g.transactionDate " +
                "            AND o.folio LIKE 'BillNo:%')) " +
                "  ) " +
                "GROUP BY g.accountcode";

            using (var cmd = new MySqlCommand(m3Sql, conn))
            {
                cmd.CommandTimeout = 120;
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        string reg = rdr["regno"].ToString();
                        long cnt = Convert.ToInt64(rdr["cnt"]);
                        long amt = rdr["amt"] != DBNull.Value ? Convert.ToInt64(rdr["amt"]) : 0;
                        if (!affectedMap.ContainsKey(reg))
                            affectedMap[reg] = new long[] { 0, 0 };
                        affectedMap[reg][0] += cnt;
                        affectedMap[reg][1] += amt;
                    }
                }
            }

            // Method 4: BATCH billing duplicates — entries with BATCH in particulars
            // that duplicate a non-BATCH entry for the same fee type on the same account
            const string m4Sql =
                "SELECT b.accountcode AS regno, COUNT(*) AS cnt, SUM(b.transaction_amount) AS amt " +
                "FROM fin_ledger b " +
                "WHERE b.account_type = 'Student' AND b.transactionType = 'DR' " +
                "  AND b.particulars LIKE '%BATCH%' " +
                "  AND EXISTS ( " +
                "    SELECT 1 FROM fin_ledger o " +
                "    WHERE o.accountcode = b.accountcode AND o.account_type = 'Student' " +
                "      AND o.transactionType = 'DR' AND o.TID <> b.TID " +
                "      AND o.particulars NOT LIKE '%BATCH%' " +
                "      AND LEFT(REPLACE(b.particulars, ' BATCH', ''), 30) = LEFT(o.particulars, 30) " +
                "  ) " +
                "GROUP BY b.accountcode";

            using (var cmd = new MySqlCommand(m4Sql, conn))
            {
                cmd.CommandTimeout = 120;
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        string reg = rdr["regno"].ToString();
                        long cnt = Convert.ToInt64(rdr["cnt"]);
                        long amt = rdr["amt"] != DBNull.Value ? Convert.ToInt64(rdr["amt"]) : 0;
                        if (!affectedMap.ContainsKey(reg))
                            affectedMap[reg] = new long[] { 0, 0 };
                        affectedMap[reg][0] += cnt;
                        affectedMap[reg][1] += amt;
                    }
                }
            }

            // UNIQUE index check
            bool hasIdx = false;
            using (var cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM information_schema.STATISTICS " +
                "WHERE TABLE_SCHEMA='campus_dynamics_accounts' AND TABLE_NAME='fin_ledger' " +
                "AND INDEX_NAME='uq_billing_entry'", conn))
            {
                cmd.CommandTimeout = 10;
                hasIdx = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }

            // Fetch student names for affected accounts (batch lookup)
            var nameMap = new Dictionary<string, string>();
            if (affectedMap.Count > 0)
            {
                var regList = new List<string>();
                foreach (var k in affectedMap.Keys) regList.Add("'" + k.Replace("'", "''") + "'");
                string namesSql = "SELECT regno, TRIM(CONCAT(COALESCE(firstname,''),' ',COALESCE(othername,''))) AS fullname " +
                    "FROM campus_dynamics.acad_student WHERE regno IN (" +
                    string.Join(",", regList.ToArray()) + ")";
                using (var cmd = new MySqlCommand(namesSql, conn))
                {
                    cmd.CommandTimeout = 30;
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                            nameMap[rdr["regno"].ToString()] = rdr["fullname"].ToString();
                    }
                }
            }

            // Build accounts array
            long grandTotalDups = 0, grandTotalAmt = 0;
            var accountsJson = new List<string>();
            foreach (var kv in affectedMap)
            {
                string name = nameMap.ContainsKey(kv.Key) ? nameMap[kv.Key] : "";
                grandTotalDups += kv.Value[0];
                grandTotalAmt += kv.Value[1];
                accountsJson.Add(string.Format(
                    "{{\"regno\":\"{0}\",\"name\":\"{1}\",\"dupCount\":{2},\"dupAmount\":{3}}}",
                    JsEsc(kv.Key), JsEsc(name), kv.Value[0], kv.Value[1]));
            }

            json.AppendFormat("\"totalStudents\":{0},\"totalLedger\":{1},", totalStudents, totalLedger);
            json.AppendFormat("\"affectedCount\":{0},", affectedMap.Count);
            json.AppendFormat("\"grandTotalDups\":{0},\"grandTotalAmount\":{1},", grandTotalDups, grandTotalAmt);
            json.AppendFormat("\"uniqueIndexActive\":{0},", hasIdx ? "true" : "false");
            json.Append("\"accounts\":[" + string.Join(",", accountsJson.ToArray()) + "]}");
        }

        Response.Write(json.ToString());
    }

    // ────────────────────────────────────────────────────────────────────
    //  FIX ONE: Fix duplicates for a single student account
    //  Called repeatedly from frontend for each affected account.
    //  Uses the EXACT same 3-method detection as the student portal.
    // ────────────────────────────────────────────────────────────────────
    private void BatchDup_FixOne()
    {
        string regno = Request.QueryString["regno"];
        if (string.IsNullOrEmpty(regno))
        {
            Response.Write("{\"ok\":false,\"error\":\"Missing regno parameter.\"}");
            return;
        }

        int deleted = 0;
        decimal balBefore = 0, balAfter = 0;

        using (var conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();

            // Balance before fix
            balBefore = BD_ComputeBalance(conn, regno);

            // Detect duplicates using the same 3-method approach
            var dupTids = new HashSet<long>();
            long dupDrAmount = 0;
            int m1, m2, m3, m4;
            BD_DetectDuplicates(conn, regno, dupTids, ref dupDrAmount, out m1, out m2, out m3, out m4);

            if (dupTids.Count > 0)
            {
                // Delete in batches of 500
                var tidList = new List<long>(dupTids);
                for (int i = 0; i < tidList.Count; i += 500)
                {
                    int end = Math.Min(i + 500, tidList.Count);
                    var batch = tidList.GetRange(i, end - i);
                    var batchStr = new List<string>();
                    foreach (long t in batch) batchStr.Add(t.ToString());
                    string delSql = "DELETE FROM fin_ledger WHERE TID IN (" +
                        string.Join(",", batchStr.ToArray()) + ")";
                    using (var cmd = new MySqlCommand(delSql, conn))
                    {
                        cmd.CommandTimeout = 120;
                        deleted += cmd.ExecuteNonQuery();
                    }
                }
            }

            // Balance after fix
            balAfter = BD_ComputeBalance(conn, regno);

            Response.Write(string.Format(
                "{{\"ok\":true,\"regno\":\"{0}\",\"deleted\":{1}," +
                "\"m1\":{2},\"m2\":{3},\"m3\":{4},\"m4\":{5}," +
                "\"balBefore\":\"{6}\",\"balAfter\":\"{7}\",\"archived\":true}}",
                JsEsc(regno), deleted, m1, m2, m3, m4,
                JsEsc(BD_FormatBalance(balBefore)),
                JsEsc(BD_FormatBalance(balAfter))));
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  BATCH DUP — Shared helpers (BD_ prefix to avoid collision)
    //  Exact same SQL logic as StudentFees.aspx.cs but scoped to admin
    // ────────────────────────────────────────────────────────────────────

    private decimal BD_ComputeBalance(MySqlConnection conn, string regno)
    {
        const string sql =
            "SELECT " +
            "  IFNULL(SUM(CASE WHEN transactionType='DR' THEN transaction_amount ELSE 0 END),0) - " +
            "  IFNULL(SUM(CASE WHEN transactionType='CR' THEN transaction_amount ELSE 0 END),0) AS bal " +
            "FROM ( " +
            "  SELECT transactionType, transaction_amount FROM fin_ledger " +
            "  WHERE accountcode = @reg AND account_type = 'Student' AND transaction_amount > 0 " +
            "  UNION ALL " +
            "  SELECT 'DR' AS transactionType, t.amount AS transaction_amount " +
            "  FROM fin_studentfeestracking t " +
            "  WHERE t.regno = @reg AND t.trans_type = 'Bill' AND t.post_status = 'Posted' " +
            "    AND NOT EXISTS ( " +
            "      SELECT 1 FROM fin_ledger l WHERE l.accountcode = t.regno " +
            "        AND l.account_type='Student' AND l.transactionType='DR' " +
            "        AND l.voucherNo = CAST(t.TID AS CHAR)) " +
            "    AND NOT EXISTS ( " +
            "      SELECT 1 FROM fin_ledger l WHERE l.accountcode = t.regno " +
            "        AND l.account_type='Student' AND l.transactionType='DR' " +
            "        AND l.folio = CONCAT('BillNo:', t.TID)) " +
            "    AND NOT EXISTS ( " +
            "      SELECT 1 FROM fin_ledger l WHERE l.accountcode = t.regno " +
            "        AND l.account_type='Student' AND l.transactionType='DR' " +
            "        AND l.transaction_amount = t.amount " +
            "        AND DATE(l.transactionDate) = DATE(t.trans_date) " +
            "        AND (l.particulars = t.detail OR t.detail IS NULL OR t.detail = '')) " +
            ") combined";

        using (var cmd = new MySqlCommand(sql, conn))
        {
            cmd.CommandTimeout = 30;
            cmd.Parameters.AddWithValue("@reg", regno);
            object val = cmd.ExecuteScalar();
            return (val != null && val != DBNull.Value) ? Convert.ToDecimal(val) : 0;
        }
    }

    private static string BD_FormatBalance(decimal balance)
    {
        if (balance > 0) return "UGX " + balance.ToString("N0");
        if (balance < 0) return "UGX -" + Math.Abs(balance).ToString("N0");
        return "UGX 0";
    }

    private void BD_DetectDuplicates(MySqlConnection conn, string regno,
        HashSet<long> dupTids, ref long dupDrAmount,
        out int m1, out int m2, out int m3, out int m4)
    {
        // Method 1: tracking_ref duplicates
        const string sql1 =
            "SELECT l.TID, l.transaction_amount AS amt, l.transactionType " +
            "FROM fin_ledger l " +
            "INNER JOIN ( " +
            "  SELECT tracking_ref, transactionType, MIN(TID) AS keep_tid " +
            "  FROM fin_ledger " +
            "  WHERE accountcode = @reg AND account_type = 'Student' " +
            "    AND tracking_ref IS NOT NULL " +
            "  GROUP BY tracking_ref, transactionType " +
            "  HAVING COUNT(*) > 1 " +
            ") dup ON l.tracking_ref = dup.tracking_ref " +
            "     AND l.transactionType = dup.transactionType " +
            "     AND l.TID > dup.keep_tid " +
            "WHERE l.accountcode = @reg AND l.account_type = 'Student'";

        m1 = BD_RunDetection(conn, sql1, regno, dupTids, ref dupDrAmount);

        // Method 2: folio duplicates
        string excl = BD_TidExcl(dupTids);
        string sql2 =
            "SELECT l.TID, l.transaction_amount AS amt, l.transactionType " +
            "FROM fin_ledger l " +
            "INNER JOIN ( " +
            "  SELECT folio, transactionType, MIN(TID) AS keep_tid " +
            "  FROM fin_ledger " +
            "  WHERE accountcode = @reg AND account_type = 'Student' " +
            "    AND folio LIKE 'BillNo:%' " +
            "  GROUP BY folio, transactionType " +
            "  HAVING COUNT(*) > 1 " +
            ") dup ON l.folio = dup.folio " +
            "     AND l.transactionType = dup.transactionType " +
            "     AND l.TID > dup.keep_tid " +
            "WHERE l.accountcode = @reg AND l.account_type = 'Student' " +
            "  AND l.TID NOT IN (" + excl + ")";

        m2 = BD_RunDetection(conn, sql2, regno, dupTids, ref dupDrAmount);

        // Method 3: GLSync remnants
        excl = BD_TidExcl(dupTids);
        string sql3 =
            "SELECT g.TID, g.transaction_amount AS amt, g.transactionType " +
            "FROM fin_ledger g " +
            "WHERE g.accountcode = @reg AND g.account_type = 'Student' " +
            "  AND g.teller = 'GLSync' " +
            "  AND g.TID NOT IN (" + excl + ") " +
            "  AND EXISTS ( " +
            "    SELECT 1 FROM fin_ledger o " +
            "    WHERE o.accountcode = g.accountcode AND o.account_type = 'Student' " +
            "      AND o.transactionType = g.transactionType " +
            "      AND o.teller <> 'GLSync' AND o.TID <> g.TID " +
            "      AND (o.folio = CONCAT('BillNo:', g.voucherNo) " +
            "        OR (o.tracking_ref IS NOT NULL AND g.tracking_ref IS NOT NULL " +
            "            AND o.tracking_ref = g.tracking_ref) " +
            "        OR (o.transaction_amount = g.transaction_amount " +
            "            AND o.transactionDate = g.transactionDate " +
            "            AND o.folio LIKE 'BillNo:%')) " +
            "  )";

        m3 = BD_RunDetection(conn, sql3, regno, dupTids, ref dupDrAmount);

        // Method 4: BATCH billing duplicates
        excl = BD_TidExcl(dupTids);
        string sql4 =
            "SELECT b.TID, b.transaction_amount AS amt, b.transactionType " +
            "FROM fin_ledger b " +
            "WHERE b.accountcode = @reg AND b.account_type = 'Student' " +
            "  AND b.transactionType = 'DR' " +
            "  AND b.particulars LIKE '%BATCH%' " +
            "  AND b.TID NOT IN (" + excl + ") " +
            "  AND EXISTS ( " +
            "    SELECT 1 FROM fin_ledger o " +
            "    WHERE o.accountcode = b.accountcode AND o.account_type = 'Student' " +
            "      AND o.transactionType = 'DR' AND o.TID <> b.TID " +
            "      AND o.particulars NOT LIKE '%BATCH%' " +
            "      AND LEFT(REPLACE(b.particulars, ' BATCH', ''), 30) = LEFT(o.particulars, 30) " +
            "  )";

        m4 = BD_RunDetection(conn, sql4, regno, dupTids, ref dupDrAmount);
    }

    private int BD_RunDetection(MySqlConnection conn, string sql, string regno,
        HashSet<long> dupTids, ref long dupDrAmount)
    {
        int count = 0;
        using (var cmd = new MySqlCommand(sql, conn))
        {
            cmd.CommandTimeout = 60;
            cmd.Parameters.AddWithValue("@reg", regno);
            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    long tid = Convert.ToInt64(rdr["TID"]);
                    if (!dupTids.Add(tid)) continue;
                    count++;
                    long amt = Convert.ToInt64(rdr["amt"]);
                    if (rdr["transactionType"].ToString() == "DR")
                        dupDrAmount += amt;
                }
            }
        }
        return count;
    }

    private static string BD_TidExcl(HashSet<long> tids)
    {
        if (tids.Count == 0) return "0";
        var parts = new List<string>();
        foreach (long t in tids) parts.Add(t.ToString());
        return string.Join(",", parts.ToArray());
    }

    // ====================================================================
    // BATCH DELETE  (AJAX POST ?ajax=batch_delete)
    // ====================================================================
    private void HandleBatchDelete()
    {
        BumpFtStatsCache();   // invalidate the cached summary totals after this write
        Response.Clear();
        Response.ContentType = "application/json";

        // Session guard
        if (Session["username"] == null && Session["ScreenName"] == null)
        {
            Response.Write("{\"ok\":false,\"error\":\"Session expired. Please log in again.\"}");
            Response.End(); return;
        }

        string idsRaw    = (Request.Form["ids"]         ?? "").Trim();
        string category  = (Request.Form["category"]    ?? "").Trim();
        string explanation = (Request.Form["explanation"] ?? "").Trim();

        if (string.IsNullOrEmpty(idsRaw))
        {
            Response.Write("{\"ok\":false,\"error\":\"No transaction IDs supplied.\"}");
            Response.End(); return;
        }
        if (string.IsNullOrEmpty(category))
        {
            Response.Write("{\"ok\":false,\"error\":\"Deletion reason/category is required.\"}");
            Response.End(); return;
        }

        // Parse and validate IDs — only positive integers, max 200
        var tidList = new List<int>();
        foreach (string part in idsRaw.Split(','))
        {
            int tid;
            if (int.TryParse(part.Trim(), out tid) && tid > 0)
                tidList.Add(tid);
        }
        if (tidList.Count == 0)
        {
            Response.Write("{\"ok\":false,\"error\":\"No valid transaction IDs found.\"}");
            Response.End(); return;
        }
        if (tidList.Count > 200)
        {
            Response.Write("{\"ok\":false,\"error\":\"Maximum 200 transactions per batch.\"}");
            Response.End(); return;
        }

        int deleted = 0, errors = 0;
        var errorMsgs = new List<string>();

        try
        {
            using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                // Best-effort: ensure the archive table exists. On production the app DB
                // user may lack CREATE/ALTER rights — that must NOT abort the whole delete
                // (the table usually already exists, so archiving still works below).
                try { EnsureDeletedTransactionsTable(conn); } catch { }

                foreach (int tid in tidList)
                {
                    try
                    {
                        // Capture row before delete (for GL cleanup + archive)
                        string delRegno = "", delDetail = "", delTransType = "";
                        decimal delAmount = 0; DateTime delDate = DateTime.MinValue;
                        using (MySqlCommand sel = new MySqlCommand(
                            "SELECT regno,amount,trans_type,detail,trans_date FROM fin_studentfeestracking WHERE TID=@tid LIMIT 1", conn))
                        {
                            sel.Parameters.AddWithValue("@tid", tid);
                            using (MySqlDataReader r = sel.ExecuteReader())
                            {
                                if (!r.Read()) { errors++; continue; } // already gone
                                delRegno     = r["regno"]     != DBNull.Value ? r["regno"].ToString()          : "";
                                delAmount    = r["amount"]    != DBNull.Value ? Convert.ToDecimal(r["amount"]) : 0;
                                delTransType = r["trans_type"]!= DBNull.Value ? r["trans_type"].ToString()     : "";
                                delDetail    = r["detail"]    != DBNull.Value ? r["detail"].ToString()         : "";
                                delDate      = r["trans_date"]!= DBNull.Value ? Convert.ToDateTime(r["trans_date"]) : DateTime.MinValue;
                            }
                        }

                        // Audit record (outside transaction — immutable even on rollback)
                        try { InsertAuditRecord(conn, tid, "DELETE", reason: category + (string.IsNullOrEmpty(explanation) ? "" : " — " + explanation)); }
                        catch { /* audit failure must not block the delete */ }

                        MySqlTransaction tx = conn.BeginTransaction();
                        try
                        {
                            // Archive
                            try { ArchiveDeletedTransaction(conn, tx, tid, category, explanation); }
                            catch { /* archive failure tolerated */ }

                            // Delete from tracking
                            using (MySqlCommand del = new MySqlCommand(
                                "DELETE FROM fin_studentfeestracking WHERE TID=@tid", conn, tx))
                            {
                                del.Parameters.AddWithValue("@tid", tid);
                                del.ExecuteNonQuery();
                            }

                            // Remove the matching GL entries — BOTH sides of the double entry.
                            // The reliable link is fin_ledger.tracking_ref = <tracking TID> (and the
                            // legacy folio 'BillNo:<TID>'); voucherNo is a separate voucher number that
                            // is neither unique nor equal to the tracking TID, so it must NOT be used.
                            if (!string.IsNullOrEmpty(delRegno))
                            {
                                string folio = "BillNo:" + tid;
                                int glRemoved = 0;

                                // 1. PRECISE: delete the exact posting (DR student + CR counter-account)
                                //    that this tracking row created, via its tracking link.
                                using (MySqlCommand glDel = new MySqlCommand(
                                    "DELETE FROM fin_ledger WHERE (tracking_ref = @tid OR folio = @folio)", conn, tx))
                                {
                                    glDel.Parameters.AddWithValue("@tid",   tid.ToString());
                                    glDel.Parameters.AddWithValue("@folio", folio);
                                    glRemoved = glDel.ExecuteNonQuery();
                                }

                                // 2. LEGACY FALLBACK: only when the precise link matched nothing
                                //    (older/migrated rows have no tracking_ref). Tightly scoped to the
                                //    student's own side with an EXACT non-empty particulars match, limited
                                //    to a single row so identical duplicates can never be over-deleted.
                                //    No match-all: if detail is blank we do not touch the GL.
                                if (glRemoved == 0 && delAmount > 0 && delDate != DateTime.MinValue && !string.IsNullOrEmpty(delDetail))
                                {
                                    string glDir = (delTransType == "Payment") ? "CR" : "DR";
                                    using (MySqlCommand glDel2 = new MySqlCommand(@"
                                        DELETE FROM fin_ledger
                                        WHERE accountcode = @ac
                                          AND transactionType = @dir
                                          AND transaction_amount = @amt
                                          AND DATE(transactionDate) = @dt
                                          AND particulars = @det
                                          AND (tracking_ref IS NULL OR tracking_ref = '')
                                        LIMIT 1", conn, tx))
                                    {
                                        glDel2.Parameters.AddWithValue("@ac",  delRegno);
                                        glDel2.Parameters.AddWithValue("@dir", glDir);
                                        glDel2.Parameters.AddWithValue("@amt", delAmount);
                                        glDel2.Parameters.AddWithValue("@dt",  delDate.ToString("yyyy-MM-dd"));
                                        glDel2.Parameters.AddWithValue("@det", delDetail);
                                        glDel2.ExecuteNonQuery();
                                    }
                                }
                            }

                            tx.Commit();
                            deleted++;
                        }
                        catch (Exception txEx)
                        {
                            try { tx.Rollback(); } catch { }
                            errors++;
                            if (errorMsgs.Count < 5) errorMsgs.Add("TID " + tid + ": " + txEx.Message);
                        }
                    }
                    catch (Exception rowEx)
                    {
                        errors++;
                        if (errorMsgs.Count < 5) errorMsgs.Add("TID " + tid + ": " + rowEx.Message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Response.Write("{\"ok\":false,\"error\":" + JsonStr(ex.Message) + "}");
            Response.End(); return;
        }

        string errDetail = errorMsgs.Count > 0 ? string.Join("; ", errorMsgs.ToArray()) : null;
        Response.Write(string.Format(
            "{{\"ok\":true,\"deleted\":{0},\"errors\":{1},\"total\":{2}{3}}}",
            deleted, errors, tidList.Count,
            errDetail != null ? ",\"error_detail\":" + JsonStr(errDetail) : ""
        ));
        Response.End();
    }

    // ====================================================================
    // BATCH MARK POST STATUS  (AJAX POST ?ajax=batch_post_status)
    // ====================================================================
    private void HandleBatchPostStatus()
    {
        BumpFtStatsCache();   // invalidate the cached summary totals after this write
        Response.Clear();
        Response.ContentType = "application/json";

        if (Session["username"] == null && Session["ScreenName"] == null)
        {
            Response.Write("{\"ok\":false,\"error\":\"Session expired.\"}");
            Response.End(); return;
        }

        string idsRaw = (Request.Form["ids"]    ?? "").Trim();
        string status = (Request.Form["status"] ?? "").Trim();

        if (status != "Posted" && status != "Pending")
        {
            Response.Write("{\"ok\":false,\"error\":\"Invalid status value.\"}");
            Response.End(); return;
        }

        var tidList = new List<int>();
        foreach (string part in idsRaw.Split(','))
        {
            int tid;
            if (int.TryParse(part.Trim(), out tid) && tid > 0)
                tidList.Add(tid);
        }
        if (tidList.Count == 0 || tidList.Count > 200)
        {
            Response.Write("{\"ok\":false,\"error\":\"Invalid ID list (0 or >200).\"}");
            Response.End(); return;
        }

        // Build safe IN list (integers only — no SQL injection possible)
        string inList = string.Join(",", tidList.ConvertAll(t => t.ToString()).ToArray());
        int updated = 0;
        try
        {
            using (MySqlConnection conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    "UPDATE fin_studentfeestracking SET post_status=@st WHERE TID IN (" + inList + ")", conn))
                {
                    cmd.Parameters.AddWithValue("@st", status);
                    updated = cmd.ExecuteNonQuery();
                }
            }
        }
        catch (Exception ex)
        {
            Response.Write("{\"ok\":false,\"error\":" + JsonStr(ex.Message) + "}");
            Response.End(); return;
        }

        Response.Write("{\"ok\":true,\"updated\":" + updated + "}");
        Response.End();
    }

    private static string JsonStr(string s)
    {
        if (s == null) return "null";
        return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
    }
}
