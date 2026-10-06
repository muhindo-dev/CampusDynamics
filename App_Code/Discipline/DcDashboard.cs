using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using MySql.Data.MySqlClient;

// =====================================================================
//  Student Disciplinary module: dashboard (plan 5.1). One call returns
//  tiles, series, students under sanction now and the overdue lists.
//  Every figure goes through the same visibility filter as the list, so
//  a restricted case is never counted for someone who cannot see it.
// =====================================================================
public static class DcDashboard
{
    private static string Where(Dictionary<string, object> f, List<object> p)
    {
        var sb = new StringBuilder(" WHERE 1=1");
        sb.Append(DcAccess.CaseFilter("c")); p.Add(DcAccess.MeParam); p.Add(DcAccess.Username());
        string v;
        if ((v = FaJson.Str(f, "campus")) != "") { sb.Append(" AND c.campus_id=@campus"); p.Add("@campus"); p.Add(FaJson.Int(f, "campus")); }
        if ((v = FaJson.Str(f, "faculty")) != "") { sb.Append(" AND c.faculty_code=@fac"); p.Add("@fac"); p.Add(v); }
        if ((v = FaJson.Str(f, "year")) != "") { sb.Append(" AND c.acad_year=@yr"); p.Add("@yr"); p.Add(v); }
        if ((v = FaJson.Str(f, "type")) != "") { sb.Append(" AND c.case_type_id=@type"); p.Add("@type"); p.Add(FaJson.Int(f, "type")); }
        return sb.ToString();
    }

    private static List<object> Series(MySqlConnection c, string sql, List<object> p)
    {
        var l = new List<object>();
        foreach (DataRow r in FaDb.Table(c, null, sql, p.ToArray()).Rows) l.Add(new { key = FaDb.S(r[0]), label = FaDb.S(r[1]), value = FaDb.I(r[2]) });
        return l;
    }

    public static object Build(Dictionary<string, object> f)
    {
        // The case file catches up with any sanction that ended while the nightly event was not running.
        try { using (var c0 = FaDb.Open()) FaDb.Exec(c0, null, "CALL dc_expire_sanctions()"); } catch (Exception ex) { DcLog.Error("dashboard expire", ex); }
        DcNotify.Sweep();

        DateTime from = FaJson.Date(f, "from") ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11);
        DateTime to = FaJson.Date(f, "to") ?? DateTime.Today;
        using (var c = FaDb.Open())
        {
            var p = new List<object>();
            string w = Where(f, p);
            const string B = " FROM dc_case c JOIN dc_case_type t ON t.id=c.case_type_id ";
            var pp = new List<object>(p); pp.Add("@from"); pp.Add(from); pp.Add("@to"); pp.Add(to.AddDays(1));
            DataRow k = FaDb.Table(c, null,
                "SELECT SUM(c.status NOT IN ('CLOSED','WITHDRAWN')) open_n, " +
                "SUM(c.status IN ('REPORTED','UNDER_INVESTIGATION','HEARING_SCHEDULED','SUMMONED')) await_hearing, " +
                "SUM(c.status='HEARD') await_decision, SUM(c.status='UNDER_APPEAL') appeal_n, " +
                "SUM(c.status IN ('CLOSED','WITHDRAWN') AND c.closed_at>=@from AND c.closed_at<@to) closed_n, " +
                "SUM(c.created_at>=@from AND c.created_at<@to) new_n, " +
                "AVG(CASE WHEN c.decided_at IS NOT NULL AND c.decided_at>=@from AND c.decided_at<@to THEN DATEDIFF(c.decided_at, c.created_at) END) avg_days, " +
                "SUM(c.is_restricted=1 AND c.status NOT IN ('CLOSED','WITHDRAWN')) restricted_open " + B + w, pp.ToArray()).Rows[0];

            var tiles = new
            {
                open = FaDb.I(k["open_n"]), awaitingHearing = FaDb.I(k["await_hearing"]), awaitingDecision = FaDb.I(k["await_decision"]),
                underAppeal = FaDb.I(k["appeal_n"]), closed = FaDb.I(k["closed_n"]), opened = FaDb.I(k["new_n"]),
                avgDays = k["avg_days"] == DBNull.Value ? (object)null : Math.Round(FaDb.M(k["avg_days"]), 1),
                restrictedOpen = FaDb.I(k["restricted_open"])
            };

            var byType = Series(c, "SELECT t.id, t.name, COUNT(*)" + B + w + " AND c.created_at>=@from AND c.created_at<@to GROUP BY t.id, t.name ORDER BY COUNT(*) DESC", pp);
            var byCampus = Series(c, "SELECT IFNULL(c.campus_id,0), IFNULL(cp.campus_name,'Not recorded'), COUNT(*)" + B + " LEFT JOIN acad_campuses cp ON cp.ID=c.campus_id" + w + " AND c.created_at>=@from AND c.created_at<@to GROUP BY 1,2 ORDER BY 3 DESC", pp);
            var byFaculty = Series(c, "SELECT IFNULL(c.faculty_code,''), IFNULL(fa.faculty_name,'Not recorded'), COUNT(*)" + B + " LEFT JOIN acad_faculty fa ON fa.faculty_code=c.faculty_code" + w + " AND c.created_at>=@from AND c.created_at<@to GROUP BY 1,2 ORDER BY 3 DESC", pp);
            var perMonth = Series(c, "SELECT DATE_FORMAT(c.created_at,'%Y-%m'), DATE_FORMAT(c.created_at,'%b %Y'), COUNT(*)" + B + w + " AND c.created_at>=@from AND c.created_at<@to GROUP BY 1,2 ORDER BY 1", pp);
            var perYear = Series(c, "SELECT c.acad_year, c.acad_year, COUNT(*)" + B + w + " GROUP BY c.acad_year ORDER BY c.acad_year", p);
            var statusLabelled = new List<object>();
            foreach (DataRow r in FaDb.Table(c, null, "SELECT c.status, COUNT(*)" + B + w + " GROUP BY c.status ORDER BY FIELD(c.status,'REPORTED','UNDER_INVESTIGATION','HEARING_SCHEDULED','SUMMONED','HEARD','DECIDED','UNDER_APPEAL','APPEAL_DECIDED','CLOSED','WITHDRAWN')", p.ToArray()).Rows)
                statusLabelled.Add(new { key = FaDb.S(r[0]), label = DcFmt.Status(FaDb.S(r[0])), value = FaDb.I(r[1]) });
            var sevLabelled = new List<object>();
            foreach (DataRow r in FaDb.Table(c, null, "SELECT c.severity, COUNT(*)" + B + w + " AND c.created_at>=@from AND c.created_at<@to GROUP BY c.severity ORDER BY FIELD(c.severity,'MINOR','SERIOUS','GROSS')", pp.ToArray()).Rows)
                sevLabelled.Add(new { key = FaDb.S(r[0]), label = DcFmt.Severity(FaDb.S(r[0])), value = FaDb.I(r[1]) });

            // Students under a restriction today.
            var effects = new List<object>();
            foreach (DataRow r in FaDb.Table(c, null,
                "SELECT e.effect, COUNT(DISTINCT s.regno) FROM dc_sanction s JOIN dc_case c ON c.id=s.case_id JOIN dc_case_type t ON t.id=c.case_type_id " +
                "JOIN (SELECT 'PORTAL_BLOCK' effect UNION ALL SELECT 'RESULTS_WITHHELD' UNION ALL SELECT 'SUSPENSION' UNION ALL SELECT 'EXPULSION' UNION ALL SELECT 'GRADUATION_BAR') e " +
                "  ON FIND_IN_SET(e.effect, s.effects)>0 " + w + " AND s.status='ACTIVE' AND s.starts_on<=CURDATE() AND (s.ends_on IS NULL OR s.ends_on>=CURDATE()) GROUP BY e.effect", p.ToArray()).Rows)
                effects.Add(new { key = FaDb.S(r[0]), label = DcFmt.Effect(FaDb.S(r[0])), value = FaDb.I(r[1]) });
            var underSanction = new List<object>();
            foreach (DataRow r in FaDb.Table(c, null,
                "SELECT c.id, c.case_no, c.regno, c.student_name, st.name, s.starts_on, s.ends_on, s.effects FROM dc_sanction s JOIN dc_case c ON c.id=s.case_id " +
                "JOIN dc_case_type t ON t.id=c.case_type_id JOIN dc_sanction_type st ON st.id=s.sanction_type_id " + w +
                " AND s.status='ACTIVE' AND s.effects<>'' AND s.effects NOT IN ('FINE','RESTITUTION') AND s.starts_on<=CURDATE() AND (s.ends_on IS NULL OR s.ends_on>=CURDATE()) " +
                "ORDER BY s.ends_on IS NULL, s.ends_on LIMIT 50", p.ToArray()).Rows)
                underSanction.Add(new { id = FaDb.I(r[0]), caseNo = FaDb.S(r[1]), regno = FaDb.S(r[2]), name = FaDb.S(r[3]), sanction = FaDb.S(r[4]),
                                        from = DcFmt.Date(r[5]), to = r[6] == DBNull.Value ? "Open" : DcFmt.Date(r[6]) });

            // Overdue and attention.
            int od = DcSettings.Int("overdue_no_update_days", 14), ac = DcSettings.Int("appeal_closing_days", 3);
            var pa = new List<object>(p); pa.Add("@od"); pa.Add(od); pa.Add("@ac"); pa.Add(ac);
            DataRow a = FaDb.Table(c, null,
                "SELECT SUM(c.status NOT IN ('CLOSED','WITHDRAWN') AND c.last_entry_at < DATE_SUB(NOW(), INTERVAL @od DAY)) stale, " +
                "SUM(c.status IN ('HEARING_SCHEDULED','SUMMONED') AND c.hearing_at < NOW()) hearing_passed, " +
                "SUM(c.status='DECIDED' AND c.appeal_deadline BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL @ac DAY)) appeal_closing, " +
                "SUM(c.status IN ('HEARING_SCHEDULED','SUMMONED') AND c.hearing_at BETWEEN NOW() AND DATE_ADD(NOW(), INTERVAL 7 DAY)) hearings_week " + B + w, pa.ToArray()).Rows[0];
            int followMarks = FaDb.I(FaDb.Scalar(c, null, "SELECT COUNT(*) FROM dc_sanction s JOIN dc_case c ON c.id=s.case_id JOIN dc_case_type t ON t.id=c.case_type_id " + w +
                " AND s.follow_up='PENDING' AND s.status IN ('ACTIVE','EXPIRED') AND (FIND_IN_SET('CANCEL_PAPER',s.effects)>0 OR FIND_IN_SET('CANCEL_SEMESTER',s.effects)>0)", p.ToArray()));
            int followFees = FaDb.I(FaDb.Scalar(c, null, "SELECT COUNT(*) FROM dc_sanction s JOIN dc_case c ON c.id=s.case_id JOIN dc_case_type t ON t.id=c.case_type_id " + w +
                " AND s.follow_up='PENDING' AND s.status IN ('ACTIVE','EXPIRED') AND (FIND_IN_SET('FINE',s.effects)>0 OR FIND_IN_SET('RESTITUTION',s.effects)>0)", p.ToArray()));
            int unread = FaDb.I(FaDb.Scalar(c, null, "SELECT COUNT(DISTINCT c.id) FROM dc_notification n JOIN dc_case c ON c.id=n.case_id JOIN dc_case_type t ON t.id=c.case_type_id " + w +
                " AND n.portal_read_at IS NULL AND n.created_at < DATE_SUB(NOW(), INTERVAL 7 DAY)", p.ToArray()));
            int emailFailed = FaDb.I(FaDb.Scalar(c, null, "SELECT COUNT(DISTINCT c.id) FROM dc_notification n JOIN dc_case c ON c.id=n.case_id JOIN dc_case_type t ON t.id=c.case_type_id " + w +
                " AND n.email_status IN ('FAILED','NO_ADDRESS')", p.ToArray()));
            int unrecorded = FaDb.I(FaDb.Scalar(c, null, "SELECT COUNT(*) FROM dc_sanction WHERE status='ACTIVE' AND ends_on < CURDATE()"));

            var upcoming = new List<object>();
            foreach (DataRow r in FaDb.Table(c, null, "SELECT c.id, c.case_no, c.regno, c.student_name, c.hearing_at, c.hearing_venue, c.status" + B + w +
                " AND c.status IN ('HEARING_SCHEDULED','SUMMONED') AND c.hearing_at >= DATE_SUB(NOW(), INTERVAL 1 DAY) ORDER BY c.hearing_at LIMIT 12", p.ToArray()).Rows)
                upcoming.Add(new { id = FaDb.I(r[0]), caseNo = FaDb.S(r[1]), regno = FaDb.S(r[2]), name = FaDb.S(r[3]), at = DcFmt.When(r[4]), venue = FaDb.S(r[5]),
                                   statusText = DcFmt.Status(FaDb.S(r[6])) });

            return new
            {
                success = true, from = from.ToString("yyyy-MM-dd"), to = to.ToString("yyyy-MM-dd"), tiles = tiles,
                byType = byType, bySeverity = sevLabelled, byStatus = statusLabelled, byCampus = byCampus, byFaculty = byFaculty, perMonth = perMonth, perYear = perYear,
                effects = effects, underSanction = underSanction, upcoming = upcoming,
                attention = new
                {
                    stale = FaDb.I(a["stale"]), staleDays = od, hearingPassed = FaDb.I(a["hearing_passed"]), appealClosing = FaDb.I(a["appeal_closing"]), appealDays = ac,
                    hearingsWeek = FaDb.I(a["hearings_week"]), followMarks = followMarks, followFees = followFees, unread = unread, emailFailed = emailFailed,
                    unrecorded = unrecorded
                }
            };
        }
    }
}
