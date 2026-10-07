-- ---------------------------------------------------------------------------
-- General Ledger rebuild: "no historical row changed" check (read-only).
-- Counts, totals and an order-independent checksum of the finance tables, limited to rows that existed at the
-- baseline (TID up to the recorded maximum) so that new postings by the SchoolPay sweep and tellers do not
-- disturb the comparison. curr_balance and timeLog are left out: the daily balance job rewrites curr_balance.
-- Usage: set @max_ledger / @max_tracking to the baseline maxima, run before and after, compare.
-- ---------------------------------------------------------------------------
SELECT 'fin_ledger' tbl, COUNT(*) n, SUM(transaction_amount) amt,
       BIT_XOR(CRC32(CONCAT_WS('|',TID,accountcode,account_type,transactionType,transaction_amount,particulars,voucherNo,transactionDate,folio,IFNULL(source_system,''),IFNULL(RefNo,''),journal_no))) chk
  FROM campus_dynamics_accounts.fin_ledger WHERE TID <= @max_ledger
UNION ALL
SELECT 'fin_studentfeestracking', COUNT(*), SUM(amount),
       BIT_XOR(CRC32(CONCAT_WS('|',TID,regno,semester,acadyear,amount,item_code,trans_type,detail,trans_date,post_status)))
  FROM campus_dynamics_accounts.fin_studentfeestracking WHERE TID <= @max_tracking
UNION ALL
SELECT 'fin_journalnumbers', COUNT(*), 0, BIT_XOR(CRC32(CONCAT_WS('|',JournalNo,Teller,PostStatus,journalType,journalDate,IFNULL(GL_VoucherNo,''))))
  FROM campus_dynamics_accounts.fin_journalnumbers
UNION ALL
SELECT 'fin_journal_details', COUNT(*), SUM(transaction_amount), BIT_XOR(CRC32(CONCAT_WS('|',TID,accountcode,transactionType,transaction_amount,voucherNo,transactionDate)))
  FROM campus_dynamics_accounts.fin_journal_details
UNION ALL
SELECT 'fin_subaccounts', COUNT(*), 0, BIT_XOR(CRC32(CONCAT_WS('|',AccountCode,MainAccountCode,AccountName,IFNULL(accounttype,''),IFNULL(collectionLedgerType,''))))
  FROM campus_dynamics_accounts.fin_subaccounts
UNION ALL
SELECT 'fin_mainaccounts', COUNT(*), 0, BIT_XOR(CRC32(CONCAT_WS('|',AccountCode,AccountName,GeneralCategory,SubCategory)))
  FROM campus_dynamics_accounts.fin_mainaccounts
UNION ALL
SELECT 'fin_financial_years', COUNT(*), 0, BIT_XOR(CRC32(CONCAT_WS('|',id,finacial_Year,start_date,end_date,status)))
  FROM campus_dynamics_accounts.fin_financial_years;
