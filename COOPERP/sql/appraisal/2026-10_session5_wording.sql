-- Session 5 wording aligned with HR's circular of 4 Oct 2026 (contract renewal + online appraisal).
-- Plain ASCII on purpose: the portal connection has no charset, so dashes/quotes would break.
UPDATE appraisal_sessions
SET session_title = 'Performance Appraisal 2026/2027 - First Quarter',
    session_description = CONCAT(
'All Academic and Administrative staff are required to complete and submit their performance appraisal online through the Staff Portal (My Appraisals). Paper forms are no longer used.', '\n\n',
'Contract renewal: staff whose contracts expire in December 2026 or early 2027 must submit their appraisal before applying for renewal. The appraisal, the Staff Achievement Evaluation Form and the other renewal documents are due by Friday, 30 October 2026, ahead of the November 2026 sitting of the Governance Council.', '\n\n',
'For each of your responsibilities, record what you achieved against the expected standard for your office and the evidence that supports it, rate yourself, then sign and submit the appraisal to your supervisor. Please discuss it with your supervisor, who will review it and return it to you to acknowledge.', '\n\n',
'Need help? Contact the MIS Office, Mr. Muhindo Mubaraka, on 0783204665, or watch the demonstration videos.'),
    updated_at = NOW()
WHERE session_id = 5;
