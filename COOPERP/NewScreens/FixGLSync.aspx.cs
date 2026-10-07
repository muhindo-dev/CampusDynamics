using System;
using System.Data;
using System.Text;
using System.Web.UI;
using System.Web.Configuration;
using MySql.Data.MySqlClient;

public partial class FixGLSync : System.Web.UI.Page
{
    private string AcctConn
    {
        get
        {
            return WebConfigurationManager.ConnectionStrings["accountsConnectionString"].ConnectionString;
        }
    }

    // Retired (Expenditure and Accounts audit D02, 7 Oct 2026). This one-off repair tool had no sign-in check and
    // bulk-inserts into and rewrites fin_ledger. It now refuses every request; its buttons cannot be reached.
    // Proposed for deletion; see COOPERP/docs/expenditure-accounts-plan.md.
    protected override void OnInit(EventArgs e)
    {
        Response.Clear();
        Response.StatusCode = 410;
        Response.ContentType = "text/plain";
        Response.Write("This repair tool has been retired. Ledger corrections are made as adjusting entries in Expenditure & Accounts.");
        Response.End();
    }

    protected void Page_Load(object sender, EventArgs e) { }

    protected void btnRunDiag_Click(object sender, EventArgs e)
    {
        var sb = new StringBuilder();
        try
        {
            using (var conn = new MySqlConnection(AcctConn))
            {
                conn.Open();

                // ── 1. What fin_ledger contains for ALL students with account_type != 'Student'
                sb.Append("<h3>D1 — account_type values in fin_ledger (all students)</h3>");
                sb.Append(RunTable(conn,
                    "SELECT account_type, COUNT(*) AS row_count, SUM(transaction_amount) AS total_amount " +
                    "FROM fin_ledger GROUP BY account_type ORDER BY row_count DESC"));

                // ── 2. All fin_ledger rows for MRU2025004200
                sb.Append("<h3>D2 — All fin_ledger rows for MRU2025004200</h3>");
                sb.Append(RunTable(conn,
                    "SELECT TID, voucherNo, transactionDate, transactionType, transaction_amount, " +
                    "particulars, account_type " +
                    "FROM fin_ledger WHERE accountcode='MRU2025004200' ORDER BY transactionDate, TID"));

                // ── 3. Posted rows in fin_studentfeestracking MISSING from fin_ledger
                sb.Append("<h3>D3 — Posted fin_studentfeestracking rows MISSING from fin_ledger (content-match)</h3>");
                string missingSql =
                    "SELECT fst.TID, fst.regno, fst.trans_type, fst.amount, fst.detail, " +
                    "fst.trans_date, fst.acadyear, fst.semester " +
                    "FROM fin_studentfeestracking fst " +
                    "WHERE fst.post_status = 'Posted' " +
                    "AND NOT EXISTS (" +
                    "  SELECT 1 FROM fin_ledger fl " +
                    "  WHERE fl.accountcode = fst.regno " +
                    "  AND fl.transaction_amount = fst.amount " +
                    "  AND DATE(fl.transactionDate) = DATE(fst.trans_date) " +
                    "  AND fl.transactionType = CASE WHEN fst.trans_type IN ('Payment','Waiver') THEN 'CR' ELSE 'DR' END " +
                    "  AND (fl.particulars = fst.detail OR fl.voucherNo = fst.TID) " +
                    ") ORDER BY fst.regno, fst.trans_date DESC";
                var missingDt = RunTable(conn, missingSql);
                sb.Append(missingDt);

                // ── 4. Specific MRU2025004200 fin_studentfeestracking rows
                sb.Append("<h3>D4 — MRU2025004200 fin_studentfeestracking (all Posted)</h3>");
                sb.Append(RunTable(conn,
                    "SELECT TID, trans_type, amount, detail, trans_date, acadyear, semester, post_status " +
                    "FROM fin_studentfeestracking WHERE regno='MRU2025004200' AND post_status='Posted' " +
                    "ORDER BY trans_date, TID"));

                // ── 5. Current balance summary
                sb.Append("<h3>D5 — Current portal balance for MRU2025004200 (account_type='Student' only)</h3>");
                sb.Append(RunTable(conn,
                    "SELECT " +
                    "SUM(CASE WHEN transactionType='DR' THEN transaction_amount ELSE 0 END) AS total_billed, " +
                    "SUM(CASE WHEN transactionType='CR' THEN transaction_amount ELSE 0 END) AS total_paid, " +
                    "SUM(CASE WHEN transactionType='DR' THEN transaction_amount ELSE 0 END) - " +
                    "SUM(CASE WHEN transactionType='CR' THEN transaction_amount ELSE 0 END) AS balance_owed " +
                    "FROM fin_ledger WHERE accountcode='MRU2025004200' AND account_type='Student'"));
            }

            pnlDiag.Controls.Add(new LiteralControl(sb.ToString()));
        }
        catch (Exception ex)
        {
            pnlDiag.Controls.Add(new LiteralControl(
                "<pre class='err'>ERROR: " + ex.Message + "\n" + ex.StackTrace + "</pre>"));
        }
    }

    protected void btnRunFix_Click(object sender, EventArgs e)
    {
        var sb = new StringBuilder();
        try
        {
            using (var conn = new MySqlConnection(AcctConn))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        // ── FIX B: Insert missing GL entries using content-based dedup
                        string fixSql =
                            "INSERT INTO fin_ledger " +
                            "(accountcode, account_type, transactionType, transaction_amount, particulars, " +
                            " voucherNo, transactionDate, teller, timeLog, folio, " +
                            " journal_no, trans_currency, actual_amount, curr_balance, forex_rate, ugx_amount) " +
                            "SELECT " +
                            " fst.regno, 'Student', " +
                            " CASE WHEN fst.trans_type IN ('Payment','Waiver') THEN 'CR' ELSE 'DR' END, " +
                            " fst.amount, " +
                            " IFNULL(fst.detail, CONCAT(fst.trans_type, ' TID ', fst.TID)), " +
                            " fst.TID, fst.trans_date, " +
                            " 'Admin (GL Sync Fix)', NOW(), fst.regno, '-', 'UGX', " +
                            " fst.amount, 0, 1, fst.amount " +
                            "FROM fin_studentfeestracking fst " +
                            "WHERE fst.post_status = 'Posted' " +
                            "AND NOT EXISTS (" +
                            "  SELECT 1 FROM fin_ledger fl " +
                            "  WHERE fl.accountcode = fst.regno " +
                            "  AND fl.transaction_amount = fst.amount " +
                            "  AND DATE(fl.transactionDate) = DATE(fst.trans_date) " +
                            "  AND fl.transactionType = CASE WHEN fst.trans_type IN ('Payment','Waiver') THEN 'CR' ELSE 'DR' END " +
                            "  AND (fl.particulars = fst.detail OR fl.voucherNo = fst.TID) " +
                            ")";

                        var cmd = new MySqlCommand(fixSql, conn, tx);
                        int inserted = cmd.ExecuteNonQuery();

                        // ── FIX C: Normalise account_type for AUTO-billed student GL rows
                        string fixTypeSql =
                            "UPDATE fin_ledger fl " +
                            "INNER JOIN campus_dynamics.acad_student s ON s.regno = fl.accountcode " +
                            "SET fl.account_type = 'Student' " +
                            "WHERE fl.account_type NOT IN ('Student', 'Chart Account')";
                        var cmd2 = new MySqlCommand(fixTypeSql, conn, tx);
                        int updated = cmd2.ExecuteNonQuery();

                        tx.Commit();

                        sb.Append("<h3 class='ok'>✔ Fix applied successfully</h3>");
                        sb.Append("<div class='summary-box'>");
                        sb.AppendFormat("<p><span class='ok'>✔ {0}</span> missing GL row(s) inserted into fin_ledger</p>", inserted);
                        sb.AppendFormat("<p><span class='ok'>✔ {0}</span> fin_ledger row(s) had account_type normalised to 'Student'</p>", updated);
                        sb.Append("</div>");
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }

                // ── Verification after fix
                sb.Append("<h3>Verification — MRU2025004200 fin_ledger after fix</h3>");
                sb.Append(RunTable(conn,
                    "SELECT TID, voucherNo, transactionDate, transactionType, transaction_amount, " +
                    "particulars, account_type " +
                    "FROM fin_ledger WHERE accountcode='MRU2025004200' ORDER BY transactionDate, TID"));

                sb.Append("<h3>Verification — Balance summary after fix (account_type='Student')</h3>");
                sb.Append(RunTable(conn,
                    "SELECT " +
                    "SUM(CASE WHEN transactionType='DR' THEN transaction_amount ELSE 0 END) AS total_billed, " +
                    "SUM(CASE WHEN transactionType='CR' THEN transaction_amount ELSE 0 END) AS total_paid, " +
                    "SUM(CASE WHEN transactionType='DR' THEN transaction_amount ELSE 0 END) - " +
                    "SUM(CASE WHEN transactionType='CR' THEN transaction_amount ELSE 0 END) AS balance_owed " +
                    "FROM fin_ledger WHERE accountcode='MRU2025004200' AND account_type='Student'"));

                sb.Append("<hr/><p class='warn'>⚠ Delete FixGLSync.aspx and FixGLSync.aspx.cs from the server once done.</p>");
            }

            pnlResult.Controls.Add(new LiteralControl(sb.ToString()));
        }
        catch (Exception ex)
        {
            pnlResult.Controls.Add(new LiteralControl(
                "<pre class='err'>ERROR: " + ex.Message + "\n" + ex.StackTrace + "</pre>"));
        }
    }

    private string RunTable(MySqlConnection conn, string sql)
    {
        var sb = new StringBuilder();
        try
        {
            var dt = new DataTable();
            new MySqlDataAdapter(new MySqlCommand(sql, conn)).Fill(dt);

            if (dt.Rows.Count == 0)
            {
                sb.Append("<p class='ok'>✔ No rows — nothing to fix here.</p>");
                return sb.ToString();
            }

            sb.AppendFormat("<p style='color:#94a3b8;'>{0} row(s)</p>", dt.Rows.Count);
            sb.Append("<table><tr>");
            foreach (DataColumn col in dt.Columns)
                sb.AppendFormat("<th>{0}</th>", col.ColumnName);
            sb.Append("</tr>");

            foreach (DataRow row in dt.Rows)
            {
                sb.Append("<tr>");
                foreach (DataColumn col in dt.Columns)
                {
                    string val = row[col] == DBNull.Value ? "<span style='color:#555'>NULL</span>" : System.Web.HttpUtility.HtmlEncode(row[col].ToString());
                    if (col.ColumnName == "transactionType" || col.ColumnName == "trans_type")
                    {
                        string v = row[col].ToString();
                        string cls = (v == "CR" || v == "Payment") ? "badge badge-cr" : "badge badge-dr";
                        val = string.Format("<span class='{0}'>{1}</span>", cls, v);
                    }
                    sb.AppendFormat("<td>{0}</td>", val);
                }
                sb.Append("</tr>");
            }
            sb.Append("</table>");
        }
        catch (Exception ex)
        {
            sb.AppendFormat("<pre class='err'>Query error: {0}</pre>", ex.Message);
        }
        return sb.ToString();
    }
}
