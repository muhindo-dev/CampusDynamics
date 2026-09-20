using System;

/// <summary>
/// Single source of truth for the "active student" rule used by ALL exam-results
/// stats and summaries.
///
/// A student is ACTIVE only after completing onboarding and being flagged
/// <c>'ACTIVE STUDENT'</c> in the portal login store
/// <c>campus_dynamics_portal.my_aspnet_users.user_verification_status</c>
/// (where <c>name = regno</c>). Alumni ('ALUMNI'), not-yet-onboarded (NULL) and
/// accountless regnos are excluded, so counts — especially "not entered" — reflect
/// real active students instead of the entire historical registration/results tables.
///
/// PERFORMANCE: <c>my_aspnet_users.name</c> is UNIQUE-indexed, so this correlated
/// EXISTS resolves as an eq_ref index lookup (fastest form). Safe to use inside
/// aggregate COUNT/SUM/GROUP BY queries over the large marks tables.
///
/// Cross-database: callers usually run on the campus_dynamics (vacConnectionString)
/// connection; the clause fully-qualifies the portal table so it works either way
/// (same MySQL server), matching how acad_course_registration is referenced.
/// </summary>
public static class ActiveStudentFilter
{
    /// <summary>
    /// Returns an " AND EXISTS(...)" clause (leading space, ready to append to a WHERE)
    /// restricting to active students. <paramref name="regnoExpr"/> is the SQL expression
    /// for the row's registration number, e.g. "r.regno", "cr.regno", "s.regno".
    /// </summary>
    public static string Clause(string regnoExpr)
    {
        return " AND EXISTS(SELECT 1 FROM campus_dynamics_portal.my_aspnet_users u_asf " +
               "WHERE u_asf.name=" + regnoExpr + " AND u_asf.user_verification_status='ACTIVE STUDENT') ";
    }

    /// <summary>
    /// JOIN form of the same rule, for aggregate queries (COUNT / GROUP BY) over the large
    /// marks tables. Returns a " JOIN ... " fragment to place directly after the FROM table.
    ///
    /// PERFORMANCE: the EXISTS form above is right for filtering rows that are being read
    /// anyway, but as the driver of an aggregate it makes MySQL walk the whole registration
    /// table and probe the user table once per row - the stage funnel measured 19.65s over
    /// 691,256 rows. Written as a join the optimiser drives from the ~4,700 active students
    /// instead and reads only their registrations, index-only against idx_acr_regno_stage:
    /// 0.178s for identical numbers. Join order in the SQL text does not matter; the
    /// optimiser picks the user table as the driver either way.
    /// </summary>
    public static string Join(string alias, string regnoColumn)
    {
        return " JOIN campus_dynamics_portal.my_aspnet_users u_asf" +
               " ON u_asf.name=" + alias + "." + regnoColumn +
               " AND u_asf.user_verification_status='ACTIVE STUDENT' ";
    }

    /// <summary>Same predicate without the leading "AND", for use after WHERE/AND yourself.</summary>
    public static string Predicate(string regnoExpr)
    {
        return " EXISTS(SELECT 1 FROM campus_dynamics_portal.my_aspnet_users u_asf " +
               "WHERE u_asf.name=" + regnoExpr + " AND u_asf.user_verification_status='ACTIVE STUDENT') ";
    }
}
