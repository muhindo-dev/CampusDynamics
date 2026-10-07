using System;
using System.Data;
using System.Web;
using System.Web.UI;
using MySql.Data.MySqlClient;

/// <summary>
/// Journal Entries — creation, detail management, and approval workflow.
/// 
/// REFACTORED (Phase 1):
///  ✓ Hardcoded connection string → FinanceDB
///  ✓ LoadAccountsCombo/LoadJournals outside !IsPostBack → moved inside
///  ✓ Race condition (SELECT after INSERT) → LAST_INSERT_ID()
///  ✓ Two separate UPDATEs for RefNo/Particulars → single UPDATE
///  ✓ double for money → decimal via MoneyHelper
///  ✓ Unbounded journal list → LIMIT 500
///  ✓ Inline IsInOpenFinancialPeriod → FinancePeriod.IsInOpenFinancialPeriod
///  ✓ No logging → FinanceLogger for create/approve/errors
///  ✓ Period check added to approval (was missing)
///  ✓ Financial period check for journal detail add
/// </summary>
public partial class COOPERP_NewScreens_JournalEntries : System.Web.UI.Page
{
    private const string PAGE_NAME = "JournalEntries";

    protected void Page_Load(object sender, EventArgs e)
    {
        if (!IsPostBack)
        {
            // Default dates from open financial period
            var range = FinancePeriod.GetDefaultDateRange();
            txtStartDate.Text = range.Item1.ToString("yyyy-MM-dd");
            txtEndDate.Text   = range.Item2.ToString("yyyy-MM-dd");

            LoadAccountsCombo();
            LoadJournals();
        }

        // Restore active journal detail if in session (must run on postbacks too)
        if (Session["ActiveJournalNo"] != null && pnlJournalDetail.Visible)
        {
            LoadJournalDetail(Convert.ToInt32(Session["ActiveJournalNo"]));
        }
    }

    // ───────────────────────── Account Combo ──────────────────────────────

    private void LoadAccountsCombo()
    {
        DataTable dt = AccountCache.GetSubAccounts();
        cboDetailAccount.DataSource = dt;
        cboDetailAccount.DataBind();
    }

    // ───────────────────────── Journal List ──────────────────────────────

    protected void btnFilter_Click(object sender, EventArgs e)
    {
        LoadJournals();
    }

    private void LoadJournals()
    {
        DateTime startDate, endDate;
        DateTime.TryParse(txtStartDate.Text, out startDate);
        DateTime.TryParse(txtEndDate.Text, out endDate);
        if (startDate == DateTime.MinValue) startDate = DateTime.Today.AddMonths(-1);
        if (endDate == DateTime.MinValue) endDate = DateTime.Today;

        string journalType = ddlJournalType.SelectedValue;
        string status = ddlStatus.SelectedValue;

        // Build dynamic SQL with optional filters + bounded result set
        string sql = @"SELECT JournalNo, journalType, journalDate, RefNo, journalParticulars, 
                              GL_VoucherNo, Teller, PostStatus 
                       FROM fin_journalnumbers 
                       WHERE journalDate BETWEEN @sDate AND @eDate";

        var parms = new System.Collections.Generic.List<MySqlParameter>
        {
            FinanceDB.P("@sDate", startDate),
            FinanceDB.P("@eDate", endDate)
        };

        if (!string.IsNullOrEmpty(journalType))
        {
            sql += " AND journalType = @typ";
            parms.Add(FinanceDB.P("@typ", journalType));
        }
        if (!string.IsNullOrEmpty(status))
        {
            sql += " AND PostStatus = @status";
            parms.Add(FinanceDB.P("@status", status));
        }
        sql += " ORDER BY JournalNo DESC LIMIT 500";

        DataTable dt = FinanceDB.ExecuteDataTable(sql, parms.ToArray());
        gvJournals.DataSource = dt;
        gvJournals.DataBind();
    }

    // ───────────────────────── Journal Creation ───────────────────────────

    protected void btnCreateJournal_Click(object sender, EventArgs e)
    {
        pnlCreateJournal.Visible = true;
    }

    protected void btnCancelCreate_Click(object sender, EventArgs e)
    {
        pnlCreateJournal.Visible = false;
    }

    protected void btnConfirmCreate_Click(object sender, EventArgs e)
    {
        string journalType = ddlNewJournalType.SelectedValue;
        string refNo = txtNewRefNo.Text.Trim();
        string particulars = txtNewParticulars.Text.Trim();
        string user = HttpContext.Current.User.Identity.Name;

        // Check open financial period
        if (!FinancePeriod.IsInOpenFinancialPeriod())
        {
            ShowMessage("Cannot create journal: No open financial period.", false);
            return;
        }

        try
        {
            int newJournalNo = 0;

            FinanceDB.ExecuteInTransaction((conn, tx) =>
            {
                // Create journal header via SP
                using (var cmd = new MySqlCommand("fin_CreateJournal", conn))
                {
                    cmd.Transaction = tx;
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@typ", journalType);
                    cmd.Parameters.AddWithValue("@JDate", DateTime.Today);
                    cmd.Parameters.AddWithValue("@usr", user);
                    cmd.ExecuteNonQuery();
                }

                // FIX: Use LAST_INSERT_ID() instead of fragile SELECT ... ORDER BY DESC
                // This eliminates the race condition where concurrent users could get wrong ID
                using (var cmd = new MySqlCommand("SELECT LAST_INSERT_ID()", conn))
                {
                    cmd.Transaction = tx;
                    newJournalNo = Convert.ToInt32(cmd.ExecuteScalar());
                }

                // FIX: Single UPDATE instead of two separate ones
                if (!string.IsNullOrEmpty(refNo) || !string.IsNullOrEmpty(particulars))
                {
                    using (var cmd = new MySqlCommand(
                        "UPDATE fin_journalnumbers SET RefNo = @ref, journalParticulars = @part WHERE JournalNo = @jno", conn))
                    {
                        cmd.Transaction = tx;
                        cmd.Parameters.AddWithValue("@ref", string.IsNullOrEmpty(refNo) ? (object)DBNull.Value : refNo);
                        cmd.Parameters.AddWithValue("@part", string.IsNullOrEmpty(particulars) ? (object)DBNull.Value : particulars);
                        cmd.Parameters.AddWithValue("@jno", newJournalNo);
                        cmd.ExecuteNonQuery();
                    }
                }
            });

            Session["ActiveJournalNo"] = newJournalNo;
            FinanceLogger.LogJournalCreated(newJournalNo, journalType, user);
            ShowMessage("Journal #" + newJournalNo + " created. Add detail lines below.", true);
            pnlCreateJournal.Visible = false;
            LoadJournals();
            LoadJournalDetail(newJournalNo);
        }
        catch (Exception ex)
        {
            FinanceLogger.LogError(PAGE_NAME, "CreateJournal", ex);
            ShowMessage("Error creating journal: " + ex.Message, false);
        }
    }

    // ───────────────────────── Journal Detail ─────────────────────────────

    protected void gvJournals_FocusedRowChanged(object sender, EventArgs e)
    {
        if (gvJournals.FocusedRowIndex >= 0)
        {
            object key = gvJournals.GetRowValues(gvJournals.FocusedRowIndex, "JournalNo");
            if (key != null)
            {
                int jno = Convert.ToInt32(key);
                Session["ActiveJournalNo"] = jno;
                LoadJournalDetail(jno);
            }
        }
    }

    private void LoadJournalDetail(int journalNo)
    {
        pnlJournalDetail.Visible = true;

        try
        {
            // Load header
            FinanceDB.ExecuteReader(
                @"SELECT JournalNo, journalType, journalDate, RefNo, PostStatus, journalParticulars 
                  FROM fin_journalnumbers WHERE JournalNo = @jno",
                reader =>
                {
                    if (reader.Read())
                    {
                        lblJournalNo.Text = reader["JournalNo"].ToString();
                        lblJournalType.Text = reader["journalType"].ToString();
                        lblJournalDate.Text = reader.GetDateTime("journalDate").ToString("dd MMM yyyy");
                        lblRefNo.Text = reader["RefNo"] != DBNull.Value ? reader["RefNo"].ToString() : "-";
                        string ps = reader["PostStatus"].ToString();
                        string psCls = ps == "Approved" ? "fs-badge--green" : "fs-badge--amber";
                        lblPostStatus.Text = "<span class='fs-badge " + psCls + "'>" + System.Web.HttpUtility.HtmlEncode(ps) + "</span>";

                        bool isNew = ps == "New";
                        pnlAddLine.Visible = isNew;
                        btnApproveJournal.Visible = isNew;
                    }
                },
                FinanceDB.P("@jno", journalNo));

            // Load detail lines
            DataTable dt = FinanceDB.ExecuteDataTable(
                @"SELECT TID, accountcode, account_type, transactionType, transaction_amount, particulars 
                  FROM fin_journal_details WHERE journal_no = @jno ORDER BY TID",
                FinanceDB.P("@jno", journalNo));

            gvDetails.DataSource = dt;
            gvDetails.DataBind();

            // Calculate balance using decimal (was double)
            decimal totalDR = 0m, totalCR = 0m;
            foreach (DataRow row in dt.Rows)
            {
                decimal amt = MoneyHelper.ToDecimal(row["transaction_amount"]);
                if (row["transactionType"].ToString() == "DR") totalDR += amt;
                else totalCR += amt;
            }

            if (dt.Rows.Count == 0)
            {
                lblBalanceIndicator.Text = "<span class='je-bal je-bal--empty'>No lines added yet</span>";
            }
            else if (MoneyHelper.IsBalanced(totalDR, totalCR))
            {
                lblBalanceIndicator.Text = string.Format(
                    "<span class='je-bal je-bal--ok'><svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><path d='M22 11.08V12a10 10 0 1 1-5.93-9.14'/><polyline points='22 4 12 14.01 9 11.01'/></svg>BALANCED &mdash; DR {0} &bull; CR {1}</span>",
                    MoneyHelper.FormatNumber(totalDR), MoneyHelper.FormatNumber(totalCR));
            }
            else
            {
                lblBalanceIndicator.Text = string.Format(
                    "<span class='je-bal je-bal--off'><svg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><circle cx='12' cy='12' r='10'/><line x1='15' y1='9' x2='9' y2='15'/><line x1='9' y1='9' x2='15' y2='15'/></svg>UNBALANCED &mdash; DR {0} &bull; CR {1}</span>",
                    MoneyHelper.FormatNumber(totalDR), MoneyHelper.FormatNumber(totalCR));
            }
        }
        catch (Exception ex)
        {
            FinanceLogger.LogError(PAGE_NAME, "LoadJournalDetail", ex);
        }
    }

    // ───────────────────────── Add Detail Line ────────────────────────────

    protected void btnAddLine_Click(object sender, EventArgs e)
    {
        if (Session["ActiveJournalNo"] == null)
        {
            ShowMessage("No active journal selected.", false);
            return;
        }

        int jno = Convert.ToInt32(Session["ActiveJournalNo"]);
        string accCode = cboDetailAccount.Value != null ? cboDetailAccount.Value.ToString() : "";
        string transType = ddlDetailType.SelectedValue;
        string details = txtDetailParticulars.Text.Trim();
        decimal amount = MoneyHelper.ParseMoney(txtDetailAmount.Text);

        if (string.IsNullOrEmpty(accCode) || amount <= 0)
        {
            ShowMessage("Select an account and enter a valid amount.", false);
            return;
        }

        // Verify financial period is still open
        if (!FinancePeriod.IsInOpenFinancialPeriod())
        {
            ShowMessage("Cannot add line: No open financial period.", false);
            return;
        }

        try
        {
            // Get account type from cache (no DB round-trip)
            string accType = AccountCache.GetAccountType(accCode);

            FinanceDB.ExecuteNonQuerySP("fin_AddJournalDetails",
                FinanceDB.P("@jno", jno),
                FinanceDB.P("@usr", HttpContext.Current.User.Identity.Name),
                FinanceDB.P("@accCode", accCode),
                FinanceDB.P("@AccType", accType),
                FinanceDB.P("@details", string.IsNullOrEmpty(details) ? accCode + " entry" : details),
                FinanceDB.P("@typ", transType),
                FinanceDB.P("@refNo", amount));

            txtDetailAmount.Text = "";
            txtDetailParticulars.Text = "";
            LoadJournalDetail(jno);
        }
        catch (Exception ex)
        {
            FinanceLogger.LogError(PAGE_NAME, "AddLine", ex);
            ShowMessage("Error adding line: " + ex.Message, false);
        }
    }

    // ───────────────────────── Delete Detail Line ─────────────────────────

    protected void gvDetails_RowDeleting(object sender, DevExpress.Web.Data.ASPxDataDeletingEventArgs e)
    {
        e.Cancel = true;
        int tid = Convert.ToInt32(e.Keys["TID"]);
        int jno = Session["ActiveJournalNo"] != null ? Convert.ToInt32(Session["ActiveJournalNo"]) : 0;

        // Emergency guard (Expenditure and Accounts audit D03, 7 Oct 2026). fin_Delete_journal_item deletes
        // fin_ledger WHERE TID=_id when the journal is not Pending, and _id here is a journal-detail id, so it
        // could delete an unrelated posted ledger row. Only a line that belongs to this journal, while the
        // journal is still Pending, may be removed.
        try
        {
            string status = Convert.ToString(FinanceDB.ExecuteScalar<object>(
                "SELECT PostStatus FROM fin_journalnumbers WHERE JournalNo=@j", FinanceDB.P("@j", jno)) ?? "");
            long owns = Convert.ToInt64(FinanceDB.ExecuteScalar<object>(
                "SELECT COUNT(*) FROM fin_journal_details WHERE TID=@t AND TRIM(journal_no)=@j", FinanceDB.P("@t", tid), FinanceDB.P("@j", jno.ToString())) ?? 0);
            if (!string.Equals(status.Trim(), "Pending", StringComparison.OrdinalIgnoreCase) || owns != 1)
            {
                ShowMessage("Only a line of a pending journal can be removed. Posted journals are corrected with an adjusting entry.", false);
                return;
            }
        }
        catch (Exception ex)
        {
            FinanceLogger.LogError(PAGE_NAME, "DeleteLineGuard", ex);
            ShowMessage("The line could not be checked, so it was not removed.", false);
            return;
        }

        try
        {
            FinanceDB.ExecuteNonQuerySP("fin_Delete_journal_item",
                FinanceDB.P("@_id", tid),
                FinanceDB.P("@jno", jno));
            LoadJournalDetail(jno);
        }
        catch (Exception ex)
        {
            FinanceLogger.LogError(PAGE_NAME, "DeleteLine", ex);
            ShowMessage("Error removing line: " + ex.Message, false);
        }
    }

    // ───────────────────────── Approve Journal ────────────────────────────

    protected void btnApproveJournal_Click(object sender, EventArgs e)
    {
        if (Session["ActiveJournalNo"] == null) return;
        int jno = Convert.ToInt32(Session["ActiveJournalNo"]);

        // FIX: Period check was missing on approval path
        if (!FinancePeriod.IsInOpenFinancialPeriod())
        {
            ShowMessage("Cannot approve journal: No open financial period.", false);
            return;
        }

        try
        {
            string journalType = FinanceDB.ExecuteScalar<string>(
                "SELECT journalType FROM fin_journalnumbers WHERE JournalNo = @jno",
                FinanceDB.P("@jno", jno)) ?? "General";

            FinanceDB.ExecuteNonQuerySP("fin_ApproveJournal_Safe",
                FinanceDB.P("@jno", jno),
                FinanceDB.P("@usr", HttpContext.Current.User.Identity.Name),
                FinanceDB.P("@typ", journalType));

            FinanceLogger.LogJournalApproved(jno, journalType, 0m, 0m,
                HttpContext.Current.User.Identity.Name);

            ShowMessage("Journal #" + jno + " approved and posted to ledger.", true);
            LoadJournalDetail(jno);
            LoadJournals();
        }
        catch (Exception ex)
        {
            FinanceLogger.LogError(PAGE_NAME, "ApproveJournal", ex);
            ShowMessage("Error approving: " + ex.Message, false);
        }
    }

    // ───────────────────────── Close Detail Panel ─────────────────────────

    protected void btnCloseDetail_Click(object sender, EventArgs e)
    {
        pnlJournalDetail.Visible = false;
        Session["ActiveJournalNo"] = null;
    }

    // ───────────────────────── UI Helpers ──────────────────────────────────

    private void ShowMessage(string msg, bool success)
    {
        string icon = success
            ? "<svg xmlns='http://www.w3.org/2000/svg' width='14' height='14' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><path d='M22 11.08V12a10 10 0 1 1-5.93-9.14'/><polyline points='22 4 12 14.01 9 11.01'/></svg>"
            : "<svg xmlns='http://www.w3.org/2000/svg' width='14' height='14' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><circle cx='12' cy='12' r='10'/><line x1='15' y1='9' x2='9' y2='15'/><line x1='9' y1='9' x2='15' y2='15'/></svg>";
        lblMessage.Text = "<div class='je-msg " + (success ? "je-msg--success" : "je-msg--error") + "'>" + icon + System.Web.HttpUtility.HtmlEncode(msg) + "</div>";
    }
}
