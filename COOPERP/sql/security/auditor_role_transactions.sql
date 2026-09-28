-- ---------------------------------------------------------------------------
-- Transactions for the Auditor.
--
-- The whole family, not only the screen literally labelled "Transactions".
-- Auditing money means following it: the fees transactions are where a payment
-- is captured, the general ledger is where it lands, and the vouchers and
-- journals are the instruments that moved it. Granting one and withholding the
-- others would leave an auditor able to see an entry and unable to see what
-- produced it.
--
-- The voucher and journal screens are where those documents are RAISED, which
-- sounds like the wrong thing to hand a read-only role. It is not: the gate
-- refuses every write on them, so what the Auditor gets is the register, which
-- is exactly what an audit reads. can_edit stays 0 here too.
--
-- accounts.transactions is the group heading and carries no URL of its own; it
-- is granted so the group appears in the sidebar above its children.
-- ---------------------------------------------------------------------------

SET @rid := (SELECT id FROM sys_roles WHERE role_code = 'auditor' LIMIT 1);

INSERT INTO sys_role_permissions (role_id, menu_slug, can_view, can_edit, can_delete, granted_by, granted_at)
SELECT @rid, m.menu_slug, 1, 0, 0, 'system', NOW()
  FROM sys_menu_items m
 WHERE m.is_active = 1
   AND m.menu_slug IN (
        'fees.fee_admin.transactions',            -- FeesTransactions.aspx, the headline screen
        'accounts.transactions',                  -- the group heading
        'accounts.transactions.payment_vouchers', -- PaymentVouchers.aspx
        'accounts.transactions.journal_entries',  -- JournalEntries.aspx
        'accounts.transactions.contra_vouchers',  -- ContraVouchers.aspx
        'accounts.ledgers.general_ledger',        -- GeneralLedger.aspx
        'accounts.ledgers.categories'             -- LedgerCategories.aspx
   )
   AND NOT EXISTS (SELECT 1 FROM sys_role_permissions p
                    WHERE p.role_id = @rid AND p.menu_slug = m.menu_slug);

SELECT CONCAT('auditor now holds ',
              (SELECT COUNT(*) FROM sys_role_permissions WHERE role_id = @rid),
              ' slugs') AS result;
