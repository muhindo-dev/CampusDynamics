-- ---------------------------------------------------------------------------
-- Stop one real payment being posted to the ledger more than once.
--
-- WHAT HAPPENED. One Centenary deposit of UGX 1,455,000 for MRU2024000688 was
-- posted NINE times between 12:48 on 19 May 2026 and 13:36 the following day, by
-- two different operators. Four of the nine landed inside eighty-one seconds of
-- each other. Each posting allocated its own voucher number and wrote a balanced
-- CR/DR pair, so nothing looked wrong to the books: the student simply appeared
-- to have paid UGX 13,095,000 against bills of UGX 14,222,000.
--
-- WHY NOTHING CAUGHT IT. fin_ApproveStudentReceipt does guard itself, posting
-- only while the source voucher reads 'New' and flipping it afterwards, so this
-- was not one receipt approved nine times. These rows carry no source voucher at
-- all, which means they were entered afresh each time. Nothing anywhere refused
-- a payment identical to one already on the account, so every attempt succeeded
-- and the operator, seeing no confirmation, tried again.
--
-- THE RULE, AND WHY IT IS SAFE. Across the ENTIRE history of fin_ledger exactly
-- one group of student fee credits shares a student, an amount and a narrative:
-- this one. Zero false positives, ever. The narratives carry a bank reference or
-- a mobile-money transaction number, so two genuine payments never read the
-- same. A 180 day window is applied anyway, so a generic narrative repeating
-- legitimately in a later year is still allowed through.
--
-- Scope is deliberately narrow. Chart-account, bursary and supplier lines repeat
-- identically all the time and are none of this rule's business: applying it to
-- everything would have rejected 571 legitimate postings in 2026 alone.
--
-- WHY THIS EDITS AN EXISTING TRIGGER RATHER THAN ADDING ONE. MySQL 5.6 allows a
-- single BEFORE INSERT trigger per table, and fin_ledger already has one. The
-- original body is reproduced below UNCHANGED: the three validations, and the
-- teller alias mapping that rewrites a shared login to the account it posts as.
-- The duplicate check is appended. Losing any of the original would be a worse
-- bug than the one being fixed.
-- ---------------------------------------------------------------------------

DROP TRIGGER IF EXISTS trg_fin_ledger_before_insert;

DELIMITER $$

CREATE TRIGGER trg_fin_ledger_before_insert
BEFORE INSERT ON fin_ledger
FOR EACH ROW
BEGIN
    DECLARE dup_count INT DEFAULT 0;
    DECLARE dup_when  DATE DEFAULT NULL;
    DECLARE msg VARCHAR(400);

    -- ── original body, unchanged ──────────────────────────────────────────
    IF NEW.transaction_amount = 0 THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'transaction_amount must be greater than 0';
    END IF;

    IF NEW.transactionType NOT IN ('DR', 'CR') THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'transactionType must be DR or CR';
    END IF;

    IF NEW.accountcode = '' OR NEW.accountcode IS NULL THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'accountcode must not be empty';
    END IF;

    -- ── new: refuse the same student payment twice ────────────────────────
    IF NEW.account_type = 'FSTEADFees'
       AND NEW.transactionType = 'CR'
       AND IFNULL(TRIM(NEW.particulars), '') <> '' THEN

        SELECT COUNT(*), MAX(transactionDate)
          INTO dup_count, dup_when
          FROM fin_ledger
         WHERE account_type       = 'FSTEADFees'
           AND transactionType    = 'CR'
           AND accountcode        = NEW.accountcode
           AND transaction_amount = NEW.transaction_amount
           AND particulars        = NEW.particulars
           AND transactionDate   >= DATE_SUB(IFNULL(NEW.transactionDate, CURDATE()), INTERVAL 180 DAY);

        IF dup_count > 0 THEN
            -- MESSAGE_TEXT is capped at 128 characters by MySQL and overflowing it
            -- replaces the whole explanation with "Data too long for condition item",
            -- which tells the operator nothing. LEFT() makes that impossible whatever
            -- the account code turns out to be.
            SET msg = LEFT(CONCAT(
                'Duplicate payment refused for ', NEW.accountcode,
                ': same amount and narrative already posted ', IFNULL(dup_when, '?'),
                '. Check the ledger.'), 128);
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = msg;
        END IF;
    END IF;

    -- ── original body, unchanged: shared logins post as their alias ───────
    SET NEW.teller = IFNULL(
        (SELECT post_as FROM campus_dynamics_accounts.fin_teller_alias
          WHERE username = NEW.teller), NEW.teller);
END$$

DELIMITER ;

-- ---------------------------------------------------------------------------
-- Detection, for what the trigger cannot refuse.
--
-- The trigger blocks the exact case going forward. This view shows what already
-- looks the same across BOTH tables that carry a student's money, so finance can
-- look at anything suspicious without the system deciding for them. It is a
-- report, not a control: nothing is blocked on the strength of it.
--
-- Scoped to 2026 onward, both because that is the period under review and
-- because grouping the whole of fin_ledger on a 350 character narrative is too
-- slow to be a useful screen.
-- ---------------------------------------------------------------------------

CREATE OR REPLACE VIEW fin_duplicate_payment_watch AS
SELECT 'fin_ledger'       AS source_table,
       accountcode        AS regno,
       transaction_amount AS amount,
       particulars        AS narrative,
       COUNT(*)           AS postings,
       COUNT(*) - 1       AS extra_postings,
       (COUNT(*) - 1) * transaction_amount AS overstated_by,
       MIN(transactionDate) AS first_seen,
       MAX(transactionDate) AS last_seen,
       DATEDIFF(MAX(transactionDate), MIN(transactionDate)) AS days_apart
  FROM fin_ledger
 WHERE account_type = 'FSTEADFees'
   AND transactionType = 'CR'
   AND transactionDate >= '2026-01-01'
   AND IFNULL(TRIM(particulars), '') <> ''
 GROUP BY accountcode, transaction_amount, particulars
HAVING COUNT(*) > 1

UNION ALL

SELECT 'fin_studentfeestracking',
       regno, amount, TRIM(detail),
       COUNT(*), COUNT(*) - 1, (COUNT(*) - 1) * amount,
       MIN(trans_date), MAX(trans_date),
       DATEDIFF(MAX(trans_date), MIN(trans_date))
  FROM fin_studentfeestracking
 WHERE trans_type = 'Payment'
   AND trans_date >= '2026-01-01'
   AND IFNULL(TRIM(detail), '') <> ''
 GROUP BY regno, amount, TRIM(detail), acadyear, semester, item_code
HAVING COUNT(*) > 1;
