-- ---------------------------------------------------------------------------
-- Every transcript and certificate the Registry generates, and every one it was
-- refused, in one place.
--
-- Two reasons this records refusals as well as prints. A blocked attempt is the
-- Registry finding out that somebody's approval has not been done yet, which is
-- information worth keeping and not an error. And a print that is denied leaves
-- no other trace at all, so without this row nobody can answer "why did nothing
-- come out when I clicked".
--
-- batch_id groups one click. Selecting forty students and pressing Generate is
-- one event that produced forty documents, and reading it back as forty separate
-- prints would misrepresent what happened.
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS acad_document_prints (
    id            BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    regno         VARCHAR(85)     NOT NULL,
    doc_kind      VARCHAR(12)     NOT NULL,           -- TRANSCRIPT | CERTIFICATE
    doc_template  VARCHAR(40)     NOT NULL,           -- Transcript | TranscriptList | TranscriptCompact | Certificate | TranscriptHTML

    -- The year the gate judged this student on, kept because the rule that allowed
    -- or refused the print cannot be re-derived later once records move on.
    acadyear      VARCHAR(25)     NOT NULL DEFAULT '',
    on_grad_list  TINYINT(1)      NOT NULL DEFAULT 0,
    is_candidate  TINYINT(1)      NOT NULL DEFAULT 0,

    outcome       VARCHAR(10)     NOT NULL DEFAULT 'PRINTED',   -- PRINTED | BLOCKED
    block_reason  VARCHAR(255)             DEFAULT NULL,

    batch_id      CHAR(36)        NOT NULL,
    batch_size    INT             NOT NULL DEFAULT 1,
    file_name     VARCHAR(190)             DEFAULT NULL,

    printed_by    VARCHAR(100)    NOT NULL DEFAULT '',
    printed_role  VARCHAR(60)              DEFAULT NULL,
    ip_address    VARCHAR(45)              DEFAULT NULL,
    printed_at    DATETIME        NOT NULL,

    PRIMARY KEY (id),
    KEY idx_dp_regno   (regno, printed_at),
    KEY idx_dp_batch   (batch_id),
    KEY idx_dp_when    (printed_at),
    KEY idx_dp_outcome (outcome, printed_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
