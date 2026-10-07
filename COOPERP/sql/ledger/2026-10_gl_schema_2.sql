-- ---------------------------------------------------------------------------
-- General Ledger rebuild, migration 2 (database campus_dynamics_accounts).
-- Additive only, on the module's own table. Idempotent.
--
-- gl_adjustment.mode: BALANCED  = a standalone entry under its own voucher number, debit equals credit.
--                     COMPLETE  = lines added under an existing unbalanced voucher number; after posting
--                                 that voucher must balance exactly (the fix for one-sided vouchers).
-- gl_settings.adjust_voucher_base: adjusting entries take voucher numbers above this base, a range no
--                                 classic sequence reaches, so they can never share a number.
-- ---------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS gl_mig_2;
DELIMITER $$
CREATE PROCEDURE gl_mig_2()
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'gl_adjustment' AND column_name = 'mode') THEN
    ALTER TABLE gl_adjustment ADD COLUMN mode ENUM('BALANCED','COMPLETE') NOT NULL DEFAULT 'BALANCED' AFTER status;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'gl_adjustment' AND column_name = 'override_note') THEN
    ALTER TABLE gl_adjustment ADD COLUMN override_note VARCHAR(1000) NULL AFTER decision_note;
  END IF;
END$$
DELIMITER ;
CALL gl_mig_2();
DROP PROCEDURE gl_mig_2;

INSERT INTO gl_settings (setting_key, setting_value, description, updated_by, updated_at)
SELECT 'adjust_voucher_base', '900000000', 'Adjusting entries use voucher numbers above this base (classic sequences are far below it).', 'gl-2026-10', NOW()
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM gl_settings WHERE setting_key = 'adjust_voucher_base');
