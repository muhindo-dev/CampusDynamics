DROP PROCEDURE IF EXISTS fin_AutoPayCapture;
DELIMITER $$
CREATE DEFINER=`dbmanager`@`%` PROCEDURE `fin_AutoPayCapture`(
    reg CHAR(25),
    stud_nm CHAR(65),
    bankCode CHAR(25),
    amount DOUBLE,
    TDate DATE,
    teller CHAR(25),
    bankName VARCHAR(250),
    tid INT
)
BEGIN
    DECLARE CRaccountType, DRParticulars, CRParticulars, StudySys, acad VARCHAR(200);
    DECLARE TransNo, semes, TCheck, currYear INT;
    DECLARE alreadyPosted INT DEFAULT 0;

    SELECT acad_year, semester, studyYear
      INTO acad, semes, currYear
      FROM campus_dynamics.acad_registration
     WHERE regno = reg
     ORDER BY ID DESC
     LIMIT 1;

    SELECT COUNT(*)
      INTO TCheck
      FROM fin_schoolpaydata
     WHERE receiptno = tid AND CaptureStatus = 'Pending';

    
    SELECT COUNT(*)
      INTO alreadyPosted
      FROM fin_ledger
     WHERE folio = CONCAT('TransCode:', tid);

    START TRANSACTION;

    IF TCheck = 1 THEN

        SET StudySys = IFNULL(CAST(currYear AS CHAR), '');
        SET CRaccountType = fin_GetStudentLedgerName(reg);

        
        SET CRParticulars = CONCAT(
            'Fees Payment for ', StudySys, ' ', IFNULL(semes, ''), ', ', IFNULL(acad, ''), ' on ',
            DATE_FORMAT(TDate, '%d/%m/%Y'), ' thru ', IFNULL(bankName, 'SchoolPay'), ' TNo: ', tid
        );
        SET DRParticulars = CONCAT(
            'Paid on ', DATE_FORMAT(TDate, '%d/%m/%Y'), ' by ', IFNULL(stud_nm, ''), ' TNo: ', tid
        );

        IF alreadyPosted = 0 THEN
            
            CALL fin_TransactionCreator(
                reg, CRaccountType, CRParticulars, bankCode, 'Chart Account',
                DRParticulars, amount, 0, SYSDATE(), 'autocapture', 'UGX', CONCAT('TransCode:', tid)
            );
            INSERT IGNORE INTO fin_studentfeestracking
                (regno, acadyear, semester, amount, item_code, trans_type, detail, trans_date, post_status)
            SELECT reg, IFNULL(acad, ''), IFNULL(semes, 0), amount, 0, 'Payment', CRParticulars, SYSDATE(), 'Posted' FROM DUAL;
        ELSE
            
            INSERT INTO fin_studentfeestracking
                (regno, acadyear, semester, amount, item_code, trans_type, detail, trans_date, post_status)
            SELECT reg, IFNULL(acad, ''), IFNULL(semes, 0), amount, 0, 'Payment', CRParticulars, SYSDATE(), 'Posted' FROM DUAL
            WHERE NOT EXISTS (SELECT 1 FROM fin_studentfeestracking WHERE regno = reg AND detail LIKE CONCAT('%TNo: ', tid));
        END IF;

        UPDATE fin_schoolpaydata SET CaptureStatus = 'Captured' WHERE ReceiptNo = tid;
        CALL fin_UpdateLedgerBalances(reg);
        CALL fin_UpdateLedgerBalances(bankCode);

    END IF;

    COMMIT;
END$$
DELIMITER ;
