-- ---------------------------------------------------------------------------
-- Fixed Assets module: schema step 2 (database campus_dynamics)
-- Adds fa_record.details_json (structured detail of a record: transfer building/room,
-- change-of-estimate old and new settings) and extends the immutability trigger to it.
-- Idempotent: the column is only added when missing. Run with the mysql client.
-- ---------------------------------------------------------------------------

DROP PROCEDURE IF EXISTS fa_tmp_add_details_json;
DELIMITER $$
CREATE PROCEDURE fa_tmp_add_details_json()
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                  WHERE table_schema = DATABASE() AND table_name = 'fa_record' AND column_name = 'details_json') THEN
    ALTER TABLE fa_record ADD COLUMN details_json TEXT NULL AFTER reason;
  END IF;
END$$
DELIMITER ;
CALL fa_tmp_add_details_json();
DROP PROCEDURE fa_tmp_add_details_json;

DROP TRIGGER IF EXISTS trg_fa_record_bu;
DELIMITER $$
CREATE TRIGGER trg_fa_record_bu BEFORE UPDATE ON fa_record FOR EACH ROW
BEGIN
  -- Only the reversal links (dep_active, reversed_by_record_id) may change.
  IF NEW.asset_id <> OLD.asset_id OR NEW.record_type <> OLD.record_type
     OR NEW.record_date <> OLD.record_date OR NEW.value_before <> OLD.value_before
     OR NEW.value_after <> OLD.value_after OR NEW.change_amount <> OLD.change_amount
     OR NOT (NEW.period_to <=> OLD.period_to) OR NOT (NEW.period_from <=> OLD.period_from)
     OR NEW.value_class <> OLD.value_class OR NEW.recorded_by <> OLD.recorded_by
     OR NEW.recorded_at <> OLD.recorded_at OR NOT (NEW.details_json <=> OLD.details_json)
     OR NOT (NEW.reason <=> OLD.reason) OR NOT (NEW.reference <=> OLD.reference)
     OR NOT (NEW.approval_ref <=> OLD.approval_ref) OR NOT (NEW.proceeds <=> OLD.proceeds) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'fa_record values cannot be changed: post a reversal instead';
  END IF;
END$$
DELIMITER ;

-- Capitalisation threshold warning default (decision Q5): UGX 500,000. Warning only, never a block.
UPDATE fa_settings SET setting_value = '500000', updated_by = 'phase2-decision', updated_at = NOW()
 WHERE setting_key = 'cap_threshold_default' AND setting_value = '0';
