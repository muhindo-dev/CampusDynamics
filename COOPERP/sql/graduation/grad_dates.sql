-- Graduation dates, per graduation list year (2026-10-13).
--   graduation_date  -> the ceremony date printed on the CERTIFICATE (acad_graduands.grad_date)
--   completion_date  -> the date of completion printed on the TRANSCRIPT (acad_graduands.comp_date)
-- Set on Graduation Centre by an administrator, the Registrar or the Graduation Officer.
-- A completion date entered on one student (acad_student.completion_date) still wins over the year's.
USE campus_dynamics;
CREATE TABLE IF NOT EXISTS acad_grad_dates (
    acadyear        VARCHAR(20)  NOT NULL PRIMARY KEY,
    graduation_date DATE         NULL,
    completion_date DATE         NULL,
    is_current      TINYINT(1)   NOT NULL DEFAULT 0,
    note            VARCHAR(300) NULL,
    updated_by      VARCHAR(100) NULL,
    updated_at      DATETIME     NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS acad_grad_dates_log (
    id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    acadyear        VARCHAR(20)  NOT NULL,
    old_graduation  DATE NULL, new_graduation DATE NULL,
    old_completion  DATE NULL, new_completion DATE NULL,
    graduands_updated INT NOT NULL DEFAULT 0,
    actor           VARCHAR(100) NOT NULL,
    created_at      DATETIME NOT NULL,
    KEY ix_gdl_year (acadyear)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
