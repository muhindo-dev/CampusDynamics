using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Web;
using System.Web.Services;
using System.Web.UI;
using MySql.Data.MySqlClient;

/// <summary>
/// General Ledger — primary transaction viewer with filtering and pagination.
/// 
/// REFACTORED (Phase 1):
///  ✓ Hardcoded connection string → FinanceDB.AcctConnStr
///  ✓ Inline LoadAccountsDropDown → AccountCache.BindAccountDropDown
///  ✓ Calendar-month defaults → FinancePeriod.GetDefaultDateRange
///  ✓ Inline FormatCurrency → MoneyHelper.FormatUGX
///  ✓ Inline BuildPagerHtml → PaginationHelper.BuildPagerHtml
///  ✓ 120-line god method → decomposed into BuildWhereClause, LoadStats, LoadPageData
///  ✓ Dead conditional (same netClass both branches) → fixed
///  ✓ No error handling → try/catch + FinanceLogger.LogError
///  ✓ Swallowed pageSize exception → PaginationHelper.ParsePageSize
///  ✓ Inline SVG moved to ternary (markup stays in .aspx where possible)
/// </summary>
public partial class COOPERP_NewScreens_GeneralLedger : System.Web.UI.Page
{
    private const string PAGE_NAME = "GeneralLedger";
    private const string DefaultSortColumn = "transactionDate";
    private const string DefaultSortDirection = "DESC";

    public class TransactionAjaxRequest
    {
        public string TransactionDate { get; set; }
        public string Reference { get; set; }
        public string DebitAccount { get; set; }
        public string CreditAccount { get; set; }
        public string Amount { get; set; }
        public string DebitParticulars { get; set; }
        public string CreditParticulars { get; set; }
    }

    public class TransactionAjaxResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Reference { get; set; }
    }

    protected string BuildSortUrl(string column)
    {
        string normalized = NormalizeSortColumn(column);
        string currentSort = NormalizeSortColumn(Request.QueryString["sort"]);
        string currentDir = NormalizeSortDirection(Request.QueryString["dir"]);
        string nextDir = (currentSort == normalized && currentDir == "ASC") ? "DESC" : "ASC";
        return BuildPageUrl(1, normalized, nextDir);
    }

    protected string GetSortIndicator(string column)
    {
        string normalized = NormalizeSortColumn(column);
        string currentSort = NormalizeSortColumn(Request.QueryString["sort"]);
        if (!string.Equals(normalized, currentSort, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        string dir = NormalizeSortDirection(Request.QueryString["dir"]);
        return dir == "ASC" ? "▲" : "▼";
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        if (!IsPostBack)
        {
            // Default dates from open financial period (not calendar month)
            var range = FinancePeriod.GetDefaultDateRange();
            txtStartDate.Text = range.Item1.ToString("yyyy-MM-dd");
            txtEndDate.Text   = range.Item2.ToString("yyyy-MM-dd");

            // Cached account dropdown — single DB hit shared across pages
            AccountCache.BindAccountDropDown(ddlAccount, includeAll: true, allText: "All Accounts");
            AccountCache.BindAccountDropDownWithSelect(ddlTxnDrAccount);
            AccountCache.BindAccountDropDownWithSelect(ddlTxnCrAccount);
            BindAcademicYearDropDown();

            txtTxnDate.Text = DateTime.Today.ToString("yyyy-MM-dd");

            ApplyQueryStringFilters();

            LoadLedger();
        }
    }

    protected void btnCreateTransaction_Click(object sender, EventArgs e)
    {
        var request = new TransactionAjaxRequest
        {
            TransactionDate = txtTxnDate.Text,
            Reference = txtTxnReference.Text,
            DebitAccount = ddlTxnDrAccount.SelectedValue,
            CreditAccount = ddlTxnCrAccount.SelectedValue,
            Amount = txtTxnAmount.Text,
            DebitParticulars = txtTxnDrParticulars.Text,
            CreditParticulars = txtTxnCrParticulars.Text
        };

        TransactionAjaxResponse response = CreateTransactionCore(request);
        ShowMessage(response.Message, response.Success);
        if (response.Success)
        {
            ResetTransactionForm();
            LoadLedger();
        }
        else
        {
            OpenTransactionModal();
        }
    }

    [WebMethod(EnableSession = true)]
    public static TransactionAjaxResponse SaveTransactionAjax(TransactionAjaxRequest request)
    {
        // Emergency guard (Expenditure and Accounts audit D01, 7 Oct 2026): this method posts to fin_ledger and
        // used to run for anyone, signed in or not. It now needs a signed-in user who holds the General Ledger slug.
        string who = HttpContext.Current != null && HttpContext.Current.Session != null && HttpContext.Current.Session["username"] != null
            ? HttpContext.Current.Session["username"].ToString().Trim() : "";
        if (who == "")
            return new TransactionAjaxResponse { Success = false, Message = "Your session has ended. Sign in again." };
        bool allowed = false;
        try
        {
            if (string.IsNullOrEmpty(HttpContext.Current.Session[RoleAccessService.SESSION_SLUGS] as string)) RoleAccessService.LoadUserAccess(who);
            allowed = RoleAccessService.CanAccess("accounts.ledgers.general_ledger");
        }
        catch { allowed = false; }
        if (!allowed)
            return new TransactionAjaxResponse { Success = false, Message = "You do not have permission to post to the general ledger." };
        return CreateTransactionCore(request);
    }

    private static TransactionAjaxResponse CreateTransactionCore(TransactionAjaxRequest request)
    {
        if (request == null)
        {
            return new TransactionAjaxResponse { Success = false, Message = "Invalid request payload." };
        }

        DateTime txnDate;
        if (!DateTime.TryParse((request.TransactionDate ?? string.Empty).Trim(), out txnDate))
        {
            return new TransactionAjaxResponse { Success = false, Message = "Please provide a valid transaction date." };
        }

        if (!FinancePeriod.IsDateInOpenPeriod(txnDate))
        {
            return new TransactionAjaxResponse { Success = false, Message = "Transaction date is not in an open financial period." };
        }

        string drAccount = (request.DebitAccount ?? string.Empty).Trim();
        string crAccount = (request.CreditAccount ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(drAccount) || string.IsNullOrEmpty(crAccount))
        {
            return new TransactionAjaxResponse { Success = false, Message = "Select both Debit and Credit accounts." };
        }
        if (string.Equals(drAccount, crAccount, StringComparison.OrdinalIgnoreCase))
        {
            return new TransactionAjaxResponse { Success = false, Message = "Debit and Credit accounts must be different." };
        }
        if (!AccountExists(drAccount) || !AccountExists(crAccount))
        {
            return new TransactionAjaxResponse { Success = false, Message = "One or both selected accounts are invalid. Please choose from the suggestion list." };
        }

        decimal amount = MoneyHelper.ParseMoney(request.Amount);
        if (amount <= 0)
        {
            return new TransactionAjaxResponse { Success = false, Message = "Enter a valid amount greater than zero." };
        }

        string drParticulars = (request.DebitParticulars ?? string.Empty).Trim();
        string crParticulars = (request.CreditParticulars ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(drParticulars))
        {
            return new TransactionAjaxResponse { Success = false, Message = "Debit Particulars is required." };
        }
        if (drParticulars.Length > 200)
        {
            return new TransactionAjaxResponse { Success = false, Message = "Debit Particulars is too long. Maximum is 200 characters." };
        }

        if (string.IsNullOrEmpty(crParticulars))
        {
            crParticulars = "Credit entry for: " + drParticulars;
        }
        if (crParticulars.Length > 200)
        {
            crParticulars = crParticulars.Substring(0, 200);
        }

        string drType = AccountCache.GetAccountType(drAccount);
        string crType = AccountCache.GetAccountType(crAccount);
        if (string.IsNullOrEmpty(drType) || string.IsNullOrEmpty(crType))
        {
            return new TransactionAjaxResponse { Success = false, Message = "Unable to resolve account type for one or both accounts." };
        }

        string reference = (request.Reference ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(reference))
        {
            reference = "GL-" + DateTime.Now.ToString("yyyyMMddHHmmss");
        }
        if (!IsSafeReference(reference))
        {
            return new TransactionAjaxResponse { Success = false, Message = "Reference contains invalid characters." };
        }
        if (reference.Length > 50)
        {
            reference = reference.Substring(0, 50);
        }

        string user = HttpContext.Current != null && HttpContext.Current.User != null
            ? (HttpContext.Current.User.Identity.Name ?? string.Empty)
            : string.Empty;
        if (string.IsNullOrEmpty(user)) user = "SYSTEM";

        try
        {
            DateTime timeLog = DateTime.Now;
            long voucherNo = 0;
            FinanceDB.ExecuteInTransaction((conn, tx) =>
            {
                using (var jCmd = new MySqlCommand("fin_CreateJournal", conn))
                {
                    jCmd.Transaction = tx;
                    jCmd.CommandType = CommandType.StoredProcedure;
                    jCmd.Parameters.AddWithValue("@typ", "General");
                    jCmd.Parameters.AddWithValue("@JDate", txnDate.Date);
                    jCmd.Parameters.AddWithValue("@usr", user);
                    jCmd.ExecuteNonQuery();
                }

                using (var idCmd = new MySqlCommand("SELECT LAST_INSERT_ID()", conn))
                {
                    idCmd.Transaction = tx;
                    voucherNo = Convert.ToInt64(idCmd.ExecuteScalar());
                }

                using (var updJ = new MySqlCommand(
                    "UPDATE fin_journalnumbers SET RefNo = @ref, journalParticulars = @part WHERE JournalNo = @jno", conn))
                {
                    updJ.Transaction = tx;
                    updJ.Parameters.AddWithValue("@ref", reference);
                    updJ.Parameters.AddWithValue("@part", drParticulars);
                    updJ.Parameters.AddWithValue("@jno", voucherNo);
                    updJ.ExecuteNonQuery();
                }

                const string insertSql = @"INSERT INTO fin_ledger
                    (transactionDate, accountcode, account_type, particulars, transactionType, transaction_amount,
                     voucherNo, RefNo, teller, timeLog, folio, journal_no, trans_currency, actual_amount, curr_balance, forex_rate, ugx_amount)
                    VALUES
                    (@date, @account, @accType, @particulars, @typ, @amount,
                     @voucherNo, @refNo, @teller, @timeLog, @folio, @journalNo, 'UGX', @actualAmount, '-', 1, @ugxAmount)";

                using (var drCmd = new MySqlCommand(insertSql, conn))
                {
                    drCmd.Transaction = tx;
                    drCmd.Parameters.AddWithValue("@date", txnDate);
                    drCmd.Parameters.AddWithValue("@account", drAccount);
                    drCmd.Parameters.AddWithValue("@accType", drType);
                    drCmd.Parameters.AddWithValue("@particulars", drParticulars);
                    drCmd.Parameters.AddWithValue("@typ", "DR");
                    drCmd.Parameters.AddWithValue("@amount", amount);
                    drCmd.Parameters.AddWithValue("@voucherNo", voucherNo);
                    drCmd.Parameters.AddWithValue("@refNo", reference);
                    drCmd.Parameters.AddWithValue("@teller", user);
                    drCmd.Parameters.AddWithValue("@timeLog", timeLog);
                    drCmd.Parameters.AddWithValue("@folio", drAccount);
                    drCmd.Parameters.AddWithValue("@journalNo", voucherNo.ToString());
                    drCmd.Parameters.AddWithValue("@actualAmount", amount);
                    drCmd.Parameters.AddWithValue("@ugxAmount", amount);
                    drCmd.ExecuteNonQuery();
                }

                using (var crCmd = new MySqlCommand(insertSql, conn))
                {
                    crCmd.Transaction = tx;
                    crCmd.Parameters.AddWithValue("@date", txnDate);
                    crCmd.Parameters.AddWithValue("@account", crAccount);
                    crCmd.Parameters.AddWithValue("@accType", crType);
                    crCmd.Parameters.AddWithValue("@particulars", crParticulars);
                    crCmd.Parameters.AddWithValue("@typ", "CR");
                    crCmd.Parameters.AddWithValue("@amount", amount);
                    crCmd.Parameters.AddWithValue("@voucherNo", voucherNo);
                    crCmd.Parameters.AddWithValue("@refNo", reference);
                    crCmd.Parameters.AddWithValue("@teller", user);
                    crCmd.Parameters.AddWithValue("@timeLog", timeLog);
                    crCmd.Parameters.AddWithValue("@folio", crAccount);
                    crCmd.Parameters.AddWithValue("@journalNo", voucherNo.ToString());
                    crCmd.Parameters.AddWithValue("@actualAmount", amount);
                    crCmd.Parameters.AddWithValue("@ugxAmount", amount);
                    crCmd.ExecuteNonQuery();
                }
            });

            FinanceLogger.LogAction(PAGE_NAME, "CreateManualTransaction",
                "VoucherNo=" + voucherNo + " Ref=" + reference + " DR=" + drAccount + " CR=" + crAccount + " Amount=" + amount.ToString("N2"));

            return new TransactionAjaxResponse
            {
                Success = true,
                Message = "Transaction posted successfully. Voucher #: " + voucherNo + " | Ref: " + reference,
                Reference = reference
            };
        }
        catch (Exception ex)
        {
            FinanceLogger.LogError(PAGE_NAME, "CreateManualTransaction", ex);
            return new TransactionAjaxResponse
            {
                Success = false,
                Message = "Error posting transaction: " + ex.Message
            };
        }
    }

    // ───────────────────────── Main Load ──────────────────────────────────

    private void LoadLedger()
    {
        try
        {
            // 1. Parse filter inputs
            var defaultRange = FinancePeriod.GetDefaultDateRange();
            DateTime startDate = ParseDate(Request.QueryString["start"], defaultRange.Item1);
            DateTime endDate = ParseDate(Request.QueryString["end"], defaultRange.Item2);
            string accountCode = (Request.QueryString["acc"] ?? string.Empty).Trim();
            string academicYear = (Request.QueryString["ay"] ?? string.Empty).Trim();
            string descriptionSearch = (Request.QueryString["q"] ?? string.Empty).Trim();
            string transType = (Request.QueryString["type"] ?? string.Empty).Trim().ToUpperInvariant();
            int pageSize = ParsePageSize(Request.QueryString["ps"]);
            int page = ParsePage(Request.QueryString["pg"]);
            string sortColumn = NormalizeSortColumn(Request.QueryString["sort"]);
            string sortDirection = NormalizeSortDirection(Request.QueryString["dir"]);

            DateTime ayStart;
            DateTime ayEndExclusive;
            if (TryParseAcademicYearRange(academicYear, out ayStart, out ayEndExclusive))
            {
                startDate = ayStart;
                endDate = ayEndExclusive.AddDays(-1);
            }

            txtStartDate.Text = startDate.ToString("yyyy-MM-dd");
            txtEndDate.Text = endDate.ToString("yyyy-MM-dd");
            if (ddlAccount.Items.FindByValue(accountCode) != null)
            {
                ddlAccount.SelectedValue = accountCode;
            }
            if (ddlType.Items.FindByValue(transType) != null)
            {
                ddlType.SelectedValue = transType;
            }
            if (ddlAcademicYear.Items.FindByValue(academicYear) != null)
            {
                ddlAcademicYear.SelectedValue = academicYear;
            }
            txtDescriptionSearch.Text = descriptionSearch;
            if (ddlPageSize.Items.FindByValue(pageSize.ToString()) != null)
            {
                ddlPageSize.SelectedValue = pageSize.ToString();
            }

            // 2. Build shared WHERE clause + parameters
            string where;
            var filterParams = BuildWhereClause(startDate, endDate, accountCode, transType, academicYear, descriptionSearch, out where);

            // 3. Load aggregate stats (single query)
            long totalRows;
            decimal sumDR, sumCR;
            LoadStats(where, filterParams, out totalRows, out sumDR, out sumCR);

            // 4. Render stats cards + totals bar
            RenderStatsCards(sumDR, sumCR, totalRows);

            // 5. Paging
            int totalPages = totalRows > 0 ? (int)Math.Ceiling((double)totalRows / pageSize) : 1;
            page = Math.Max(1, Math.Min(page, totalPages));

            // 6. Load data page
            DataTable dt = LoadPageData(where, filterParams, page, pageSize, sortColumn, sortDirection);
            rptLedger.DataSource = dt;
            rptLedger.DataBind();
            phNoData.Visible = (dt.Rows.Count == 0);

            // 7. Footer + pager
            lblGridFooter.Text = BuildFooterText(totalRows, page, pageSize);
            litPager.Text = BuildPagerHtml(page, totalPages, sortColumn, sortDirection);

            // 8. Period badge
            string periodText = string.Format("{0:dd MMM yyyy} - {1:dd MMM yyyy}", startDate, endDate);
            litPeriodBadge.Text = periodText;
            litPeriodBadgeView.Text = periodText;
        }
        catch (Exception ex)
        {
            FinanceLogger.LogError(PAGE_NAME, "LoadLedger", ex);
            phNoData.Visible = true;
        }
    }

    // ───────────────────────── Decomposed Methods ─────────────────────────

    /// <summary>
    /// Builds the parameterised WHERE clause and returns a reusable parameter list.
    /// </summary>
    private static List<MySql.Data.MySqlClient.MySqlParameter> BuildWhereClause(
        DateTime startDate, DateTime endDate, string accountCode, string transType, string academicYear, string descriptionSearch,
        out string where)
    {
        where = " WHERE 1=1";
        var parms = new List<MySql.Data.MySqlClient.MySqlParameter>();

        DateTime ayStart;
        DateTime ayEndExclusive;
        if (TryParseAcademicYearRange(academicYear, out ayStart, out ayEndExclusive))
        {
            where += " AND transactionDate >= @ayStart AND transactionDate < @ayEndEx";
            parms.Add(FinanceDB.P("@ayStart", ayStart));
            parms.Add(FinanceDB.P("@ayEndEx", ayEndExclusive));
        }
        else
        {
            where += " AND transactionDate BETWEEN @sDate AND @eDate";
            parms.Add(FinanceDB.P("@sDate", startDate));
            parms.Add(FinanceDB.P("@eDate", endDate));
        }

        if (!string.IsNullOrEmpty(accountCode))
        {
            where += " AND accountcode = @acc";
            parms.Add(FinanceDB.P("@acc", accountCode));
        }
        if (!string.IsNullOrEmpty(transType))
        {
            where += " AND transactionType = @typ";
            parms.Add(FinanceDB.P("@typ", transType));
        }
        if (!string.IsNullOrEmpty(descriptionSearch))
        {
            where += " AND particulars LIKE @desc";
            parms.Add(FinanceDB.P("@desc", "%" + descriptionSearch + "%"));
        }

        return parms;
    }

    /// <summary>
    /// Single aggregate query for all stats cards.
    /// </summary>
    private static void LoadStats(string where,
        List<MySql.Data.MySqlClient.MySqlParameter> filterParams,
        out long totalRows, out decimal sumDR, out decimal sumCR)
    {
        string sql = "SELECT COUNT(*) AS total_rows,"
            + " SUM(CASE WHEN transactionType='DR' THEN transaction_amount ELSE 0 END) AS sum_dr,"
            + " SUM(CASE WHEN transactionType='CR' THEN transaction_amount ELSE 0 END) AS sum_cr"
            + " FROM fin_ledger" + where;

        var row = FinanceDB.ExecuteAggregateRow(sql, filterParams.ToArray());

        totalRows = row.ContainsKey("total_rows") && row["total_rows"] != null
            ? Convert.ToInt64(row["total_rows"]) : 0;
        sumDR = row.ContainsKey("sum_dr") && row["sum_dr"] != null
            ? MoneyHelper.ToDecimal(row["sum_dr"]) : 0m;
        sumCR = row.ContainsKey("sum_cr") && row["sum_cr"] != null
            ? MoneyHelper.ToDecimal(row["sum_cr"]) : 0m;
    }

    /// <summary>
    /// Renders the four stats cards and the totals bar.
    /// </summary>
    private void RenderStatsCards(decimal sumDR, decimal sumCR, long totalRows)
    {
        litSumDR.Text       = MoneyHelper.FormatUGX(sumDR);
        litSumCR.Text       = MoneyHelper.FormatUGX(sumCR);
        litNetBalance.Text  = MoneyHelper.FormatUGX(MoneyHelper.Difference(sumDR, sumCR));
        litRecordCount.Text = totalRows.ToString("N0");

        litTotalBarDR.Text = MoneyHelper.FormatUGX(sumDR);
        litTotalBarCR.Text = MoneyHelper.FormatUGX(sumCR);

        decimal net = sumDR - sumCR;
        // Fixed: previously both branches had identical class
        string netClass = net >= 0 ? "ft-totals__pill--net-dr" : "ft-totals__pill--net-cr";
        string netLabel = MoneyHelper.NetLabel(sumDR, sumCR);
        string netIcon = net >= 0
            ? "<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2.5'><circle cx='12' cy='12' r='10'/><line x1='12' y1='8' x2='12' y2='16'/><line x1='8' y1='12' x2='16' y2='12'/></svg>"
            : "<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2.5'><circle cx='12' cy='12' r='10'/><line x1='8' y1='12' x2='16' y2='12'/></svg>";
        litTotalBarNet.Text = string.Format(
            "<span class='ft-totals__pill {0}'>{1} {2}: {3}</span>",
            netClass, netIcon, netLabel, MoneyHelper.FormatUGX(Math.Abs(net)));
    }

    /// <summary>
    /// Loads one page of ledger data using LIMIT/OFFSET.
    /// Uses cloned parameters to avoid reuse conflicts.
    /// </summary>
    private static DataTable LoadPageData(string where,
        List<MySql.Data.MySqlClient.MySqlParameter> filterParams,
        int page, int pageSize, string sortColumn, string sortDirection)
    {
        string orderBy = BuildOrderBy(sortColumn, sortDirection);
        string sql = "SELECT TID, transactionDate, accountcode, account_type, particulars,"
            + " transactionType, transaction_amount, voucherNo, teller"
            + " FROM fin_ledger" + where
            + " ORDER BY " + orderBy + " LIMIT @pgOffset, @pgSize";

        // Clone parameters and add paging params
        var parms = new List<MySql.Data.MySqlClient.MySqlParameter>();
        foreach (var p in filterParams)
            parms.Add(FinanceDB.P(p.ParameterName, p.Value));
        parms.Add(FinanceDB.P("@pgOffset", (page - 1) * pageSize));
        parms.Add(FinanceDB.P("@pgSize", pageSize));

        return FinanceDB.ExecuteDataTable(sql, parms.ToArray());
    }

    // ───────────────────────── Utility ─────────────────────────────────────

    private void ApplyQueryStringFilters()
    {
        string qStart = (Request.QueryString["start"] ?? string.Empty).Trim();
        string qEnd = (Request.QueryString["end"] ?? string.Empty).Trim();
        string qAcc = (Request.QueryString["acc"] ?? string.Empty).Trim();
        string qAy = (Request.QueryString["ay"] ?? string.Empty).Trim();
        string qDesc = (Request.QueryString["q"] ?? string.Empty).Trim();
        string qType = (Request.QueryString["type"] ?? string.Empty).Trim();
        string qPs = (Request.QueryString["ps"] ?? string.Empty).Trim();

        if (!string.IsNullOrEmpty(qStart)) txtStartDate.Text = qStart;
        if (!string.IsNullOrEmpty(qEnd)) txtEndDate.Text = qEnd;
        if (!string.IsNullOrEmpty(qAcc) && ddlAccount.Items.FindByValue(qAcc) != null)
            ddlAccount.SelectedValue = qAcc;
        if (!string.IsNullOrEmpty(qAy) && ddlAcademicYear.Items.FindByValue(qAy) != null)
            ddlAcademicYear.SelectedValue = qAy;
        if (!string.IsNullOrEmpty(qDesc)) txtDescriptionSearch.Text = qDesc;
        if (!string.IsNullOrEmpty(qType) && ddlType.Items.FindByValue(qType) != null)
            ddlType.SelectedValue = qType;
        if (!string.IsNullOrEmpty(qPs) && ddlPageSize.Items.FindByValue(qPs) != null)
            ddlPageSize.SelectedValue = qPs;
    }

    private static int ParsePageSize(string value)
    {
        int parsed;
        if (!int.TryParse(value, out parsed)) return 50;
        if (parsed <= 30) return 30;
        if (parsed <= 50) return 50;
        if (parsed <= 100) return 100;
        return 200;
    }

    private static int ParsePage(string value)
    {
        int parsed;
        if (!int.TryParse(value, out parsed)) return 1;
        return parsed < 1 ? 1 : parsed;
    }

    private static string NormalizeSortColumn(string sort)
    {
        string value = (sort ?? string.Empty).Trim();
        switch (value)
        {
            case "TID":
            case "transactionDate":
            case "accountcode":
            case "account_type":
            case "particulars":
            case "transactionType":
            case "transaction_amount":
            case "voucherNo":
            case "teller":
                return value;
            default:
                return DefaultSortColumn;
        }
    }

    private static string NormalizeSortDirection(string direction)
    {
        return string.Equals(direction, "ASC", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
    }

    private static string BuildOrderBy(string column, string direction)
    {
        string sort = NormalizeSortColumn(column);
        string dir = NormalizeSortDirection(direction);
        if (sort == "TID") return "TID " + dir;
        return sort + " " + dir + ", TID DESC";
    }

    private string BuildFooterText(long totalRows, int page, int pageSize)
    {
        if (totalRows <= 0) return "Showing 0 records";
        long from = ((page - 1) * pageSize) + 1;
        long to = Math.Min(totalRows, page * pageSize);
        return string.Format("Showing {0:N0} - {1:N0} of {2:N0}", from, to, totalRows);
    }

    private string BuildPagerHtml(int page, int totalPages, string sort, string dir)
    {
        if (totalPages <= 1) return string.Empty;

        var sb = new StringBuilder();
        sb.Append("<span class='ft-pager__btns'>");

        if (page > 1)
        {
            sb.AppendFormat("<a class='ft-pager__btn' href='{0}'>&laquo;</a>", BuildPageUrl(page - 1, sort, dir));
        }
        else
        {
            sb.Append("<span class='ft-pager__btn' disabled='disabled'>&laquo;</span>");
        }

        int start = Math.Max(1, page - 2);
        int end = Math.Min(totalPages, page + 2);
        if (start > 1)
        {
            sb.AppendFormat("<a class='ft-pager__btn' href='{0}'>1</a>", BuildPageUrl(1, sort, dir));
            if (start > 2) sb.Append("<span class='ft-pager__ellipsis'>…</span>");
        }

        for (int i = start; i <= end; i++)
        {
            if (i == page)
            {
                sb.AppendFormat("<span class='ft-pager__btn ft-pager__btn--active'>{0}</span>", i);
            }
            else
            {
                sb.AppendFormat("<a class='ft-pager__btn' href='{0}'>{1}</a>", BuildPageUrl(i, sort, dir), i);
            }
        }

        if (end < totalPages)
        {
            if (end < totalPages - 1) sb.Append("<span class='ft-pager__ellipsis'>…</span>");
            sb.AppendFormat("<a class='ft-pager__btn' href='{0}'>{1}</a>", BuildPageUrl(totalPages, sort, dir), totalPages);
        }

        if (page < totalPages)
        {
            sb.AppendFormat("<a class='ft-pager__btn' href='{0}'>&raquo;</a>", BuildPageUrl(page + 1, sort, dir));
        }
        else
        {
            sb.Append("<span class='ft-pager__btn' disabled='disabled'>&raquo;</span>");
        }

        sb.Append("</span>");
        return sb.ToString();
    }

    private string BuildPageUrl(int page, string sort, string dir)
    {
        string start = ParseDate(Request.QueryString["start"], FinancePeriod.GetDefaultDateRange().Item1).ToString("yyyy-MM-dd");
        string end = ParseDate(Request.QueryString["end"], FinancePeriod.GetDefaultDateRange().Item2).ToString("yyyy-MM-dd");
        string acc = (Request.QueryString["acc"] ?? string.Empty).Trim();
        string ay = (Request.QueryString["ay"] ?? string.Empty).Trim();
        string q = (Request.QueryString["q"] ?? string.Empty).Trim();
        string type = (Request.QueryString["type"] ?? string.Empty).Trim();
        string ps = ParsePageSize(Request.QueryString["ps"]).ToString();
        string adv = (Request.QueryString["adv"] ?? string.Empty).Trim();

        var parts = new List<string>();
        parts.Add("start=" + HttpUtility.UrlEncode(start));
        parts.Add("end=" + HttpUtility.UrlEncode(end));
        if (!string.IsNullOrEmpty(acc)) parts.Add("acc=" + HttpUtility.UrlEncode(acc));
        if (!string.IsNullOrEmpty(ay)) parts.Add("ay=" + HttpUtility.UrlEncode(ay));
        if (!string.IsNullOrEmpty(q)) parts.Add("q=" + HttpUtility.UrlEncode(q));
        if (!string.IsNullOrEmpty(type)) parts.Add("type=" + HttpUtility.UrlEncode(type));
        parts.Add("ps=" + HttpUtility.UrlEncode(ps));
        if (adv == "1") parts.Add("adv=1");
        parts.Add("pg=" + page);
        parts.Add("sort=" + HttpUtility.UrlEncode(NormalizeSortColumn(sort)));
        parts.Add("dir=" + HttpUtility.UrlEncode(NormalizeSortDirection(dir)));
        return "GeneralLedger.aspx?" + string.Join("&", parts);
    }

    private void BindAcademicYearDropDown()
    {
        ddlAcademicYear.Items.Clear();
        ddlAcademicYear.Items.Add(new System.Web.UI.WebControls.ListItem("All Academic Years", ""));

        int currentStartYear = DateTime.Today.Month >= 7 ? DateTime.Today.Year : DateTime.Today.Year - 1;
        for (int year = currentStartYear + 1; year >= currentStartYear - 8; year--)
        {
            string value = year + "/" + (year + 1);
            ddlAcademicYear.Items.Add(new System.Web.UI.WebControls.ListItem(value, value));
        }
    }

    private static bool TryParseAcademicYearRange(string academicYear, out DateTime start, out DateTime endExclusive)
    {
        start = DateTime.MinValue;
        endExclusive = DateTime.MinValue;

        if (string.IsNullOrWhiteSpace(academicYear)) return false;
        string[] parts = academicYear.Split('/');
        if (parts.Length != 2) return false;

        int startYear;
        int endYear;
        if (!int.TryParse(parts[0], out startYear) || !int.TryParse(parts[1], out endYear)) return false;
        if (endYear != startYear + 1) return false;

        start = new DateTime(startYear, 7, 1);
        endExclusive = new DateTime(endYear, 7, 1);
        return true;
    }

    private static bool AccountExists(string accountCode)
    {
        if (string.IsNullOrWhiteSpace(accountCode)) return false;
        long count = FinanceDB.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM fin_subaccounts WHERE AccountCode = @acc",
            FinanceDB.P("@acc", accountCode));
        return count > 0;
    }

    private static bool IsSafeReference(string reference)
    {
        foreach (char c in reference)
        {
            if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '/' || c == '.' || c == ' '))
            {
                return false;
            }
        }
        return true;
    }

    private void ResetTransactionForm()
    {
        txtTxnDate.Text = DateTime.Today.ToString("yyyy-MM-dd");
        txtTxnReference.Text = string.Empty;
        txtTxnAmount.Text = string.Empty;
        txtTxnDrParticulars.Text = string.Empty;
        txtTxnCrParticulars.Text = string.Empty;
        if (ddlTxnDrAccount.Items.Count > 0) ddlTxnDrAccount.SelectedIndex = 0;
        if (ddlTxnCrAccount.Items.Count > 0) ddlTxnCrAccount.SelectedIndex = 0;
    }

    private void OpenTransactionModal()
    {
        ScriptManager.RegisterStartupScript(this, GetType(), "openTxnModal", "openTxnModal();", true);
    }

    private void ShowMessage(string message, bool success)
    {
        lblMessage.Text = "<div class='ft-alert " + (success ? "ft-alert--ok" : "ft-alert--err") + "'>"
            + HttpUtility.HtmlEncode(message) + "</div>";
    }

    /// <summary>
    /// Safely parses a date string with a fallback default.
    /// </summary>
    private static DateTime ParseDate(string text, DateTime fallback)
    {
        DateTime dt;
        return DateTime.TryParse(text, out dt) ? dt : fallback;
    }
}
