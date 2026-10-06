using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Web;
using MySql.Data.MySqlClient;

// =====================================================================
//  Student Disciplinary module: notices to the student (plan section 7).
//  The portal notice is written in the same transaction as the event,
//  so it cannot be lost. Email is a second channel: sent after commit on
//  a background thread, its outcome recorded on the notice, retried by a
//  sweep up to three times. Letters are not attached to email (they are
//  confidential); the email points to My Disciplinary Cases.
// =====================================================================
public static class DcNotify
{
    /// <summary>Records a notice for the student and a "student notified" entry. Returns the notice id.</summary>
    public static long Queue(MySqlConnection c, MySqlTransaction tx, int caseId, string regno, string kind, string title, string message, long? letterId)
    {
        string email = Address(c, tx, regno);
        long id = FaDb.Insert(c, tx,
            "INSERT INTO dc_notification (case_id, regno, kind, title, message, letter_id, email_to, email_status, created_by, created_at) " +
            "VALUES (@c,@r,@k,@t,@m,@l,@e,@s,@u,NOW())",
            "@c", caseId, "@r", regno, "@k", kind, "@t", DcAudit.Cut(title, 200), "@m", DcAudit.Cut(message, 2000),
            "@l", letterId.HasValue ? (object)letterId.Value : DBNull.Value, "@e", FaDb.NullIfEmpty(email), "@s", email == "" ? "NO_ADDRESS" : "PENDING",
            "@u", DcAccess.Username() == "" ? "system" : DcAccess.Username());
        long eid = DcEntries.Add(c, tx, caseId, "STUDENT_NOTIFIED", "Student notified: " + title,
            "Shown on the student portal" + (email == "" ? ". No email address on record." : " and emailed to " + email + "."), false, null, null, null, null,
            new { noticeId = id }, null, null);
        FaDb.Exec(c, tx, "UPDATE dc_notification SET entry_id=@e WHERE id=@id", "@e", eid, "@id", id);
        DcAudit.Write(c, tx, "NOTIFICATION", id, caseId, "QUEUE", null, new { kind = kind, title = title, email = email }, null, null);
        return id;
    }

    /// <summary>The University address from the email directory when there is one, otherwise the address on the student record.</summary>
    public static string Address(MySqlConnection c, MySqlTransaction tx, string regno)
    {
        string a = "";
        try
        {
            a = FaDb.S(FaDb.Scalar(c, tx,
                "SELECT email FROM campus_dynamics_portal.sems_email_directory WHERE owner_type='STUDENT' AND status='ACTIVE' AND owner_ref=@r ORDER BY last_seen_at DESC LIMIT 1", "@r", regno)).Trim();
        }
        catch { }
        if (a == "" || !a.Contains("@")) a = FaDb.S(FaDb.Scalar(c, tx, "SELECT email FROM acad_student WHERE regno=@r", "@r", regno)).Trim();
        return a.Contains("@") && a.Contains(".") ? DcAudit.Cut(a, 200) : "";
    }

    public static void SendAsync(List<long> ids)
    {
        if (ids == null || ids.Count == 0) return;
        var copy = new List<long>(ids);
        string cs = FaDb.ConnString();
        ThreadPool.QueueUserWorkItem(delegate { foreach (long id in copy) SendOne(cs, id); });
    }

    /// <summary>Retries failed or unsent emails (at most 3 attempts each). Called when the dashboard opens; cheap when there is nothing to do.</summary>
    public static void Sweep()
    {
        var ids = new List<long>();
        try
        {
            using (var c = FaDb.Open())
                foreach (DataRow r in FaDb.Table(c, null,
                    "SELECT id FROM dc_notification WHERE email_status IN ('PENDING','FAILED') AND email_attempts<3 AND created_at < DATE_SUB(NOW(), INTERVAL 5 MINUTE) ORDER BY id LIMIT 50").Rows)
                    ids.Add(FaDb.L(r[0]));
        }
        catch (Exception ex) { DcLog.Error("notify sweep", ex); }
        SendAsync(ids);
    }

    private static void SendOne(string connString, long id)
    {
        try
        {
            using (var c = new MySqlConnection(connString))
            {
                c.Open();
                DataTable t = FaDb.Table(c, null,
                    "SELECT n.*, c.case_no, c.student_name FROM dc_notification n JOIN dc_case c ON c.id=n.case_id WHERE n.id=@i AND n.email_status IN ('PENDING','FAILED') AND n.email_attempts<3", "@i", id);
                if (t.Rows.Count == 0) return;
                DataRow r = t.Rows[0];
                string to = FaDb.S(r["email_to"]);
                if (to == "") { FaDb.Exec(c, null, "UPDATE dc_notification SET email_status='NO_ADDRESS' WHERE id=@i", "@i", id); return; }
                // Claim the attempt first so two sweeps never send the same notice twice.
                int claimed = FaDb.Exec(c, null, "UPDATE dc_notification SET email_attempts=email_attempts+1 WHERE id=@i AND email_attempts=@a",
                    "@i", id, "@a", FaDb.I(r["email_attempts"]));
                if (claimed != 1) return;
                string result = EmailSenderProtocol.SendHtmlEmail(Html(r), to, FaDb.S(r["title"]) + " (" + FaDb.S(r["case_no"]) + ")", DcFmt.University);
                bool ok = (result ?? "").IndexOf("success", StringComparison.OrdinalIgnoreCase) >= 0;
                FaDb.Exec(c, null, "UPDATE dc_notification SET email_status=@s, email_error=@e, email_sent_at=IF(@s='SENT',NOW(),email_sent_at) WHERE id=@i",
                    "@s", ok ? "SENT" : "FAILED", "@e", ok ? (object)DBNull.Value : DcAudit.Cut(result, 500), "@i", id);
            }
        }
        catch (Exception ex) { DcLog.Error("notify send " + id, ex); }
    }

    private static string Html(DataRow r)
    {
        string portal = System.Configuration.ConfigurationManager.AppSettings["PortalBaseUrl"];
        if (string.IsNullOrEmpty(portal)) portal = "https://eportal.mru.ac.ug";
        return "<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1A1A2E;max-width:600px\">" +
               "<div style=\"background:#05275C;color:#fff;padding:14px 18px;font-weight:600\">" + HttpUtility.HtmlEncode(DcFmt.University) + "</div>" +
               "<div style=\"border:1px solid #E0E5ED;border-top:0;padding:18px\">" +
               "<p style=\"margin:0 0 12px\">Dear " + HttpUtility.HtmlEncode(FaDb.S(r["student_name"])) + ",</p>" +
               "<p style=\"margin:0 0 12px;font-weight:600;color:#05275C\">" + HttpUtility.HtmlEncode(FaDb.S(r["title"])) + "</p>" +
               "<p style=\"margin:0 0 12px;line-height:1.6\">" + HttpUtility.HtmlEncode(FaDb.S(r["message"])).Replace("\n", "<br/>") + "</p>" +
               "<p style=\"margin:0 0 12px;line-height:1.6\">Sign in to the student portal and open <b>My Disciplinary Cases</b> to read the details" +
               (r["letter_id"] == DBNull.Value ? "" : " and download your letter") + ": <a href=\"" + HttpUtility.HtmlAttributeEncode(portal) + "\">" + HttpUtility.HtmlEncode(portal) + "</a></p>" +
               "<p style=\"margin:0;color:#6B7280;font-size:12px\">Case " + HttpUtility.HtmlEncode(FaDb.S(r["case_no"])) + ". This message is confidential. " +
               "If you have questions, contact the " + HttpUtility.HtmlEncode(DcSettings.ContactOffice) +
               (DcSettings.ContactDetails == "" ? "" : " (" + HttpUtility.HtmlEncode(DcSettings.ContactDetails) + ")") + ".</p></div></div>";
    }
}
