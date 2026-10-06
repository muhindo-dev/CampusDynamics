-- ---------------------------------------------------------------------------
-- Fixed Assets module: schema step 3 (database campus_dynamics)
-- client_op_id CHAR(36) -> VARCHAR(40). MySql.Data reads every CHAR(36) column as a GUID
-- and throws on any value that is not one (batch actions add a per-asset suffix), which
-- made the asset unreadable. Unique keys are kept. Idempotent.
-- ---------------------------------------------------------------------------
ALTER TABLE fa_record MODIFY client_op_id VARCHAR(40) NULL;
ALTER TABLE fa_depreciation_run MODIFY client_op_id VARCHAR(40) NULL;
