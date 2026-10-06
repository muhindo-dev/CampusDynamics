using System;
using System.Collections.Generic;

// =====================================================================
//  Fixed Assets: depreciation arithmetic. Pure functions, no database,
//  so every rule can be unit tested on its own (plan section 5).
//
//  Convention (plan 5.3): month based. The month in which depreciation
//  starts counts as a full month; the month of disposal is not charged.
//  The financial year runs 1 August to 31 July; a charge never crosses
//  a year boundary (the caller splits by year).
//
//  Straight line works from a BASE: a value, the month it applies from,
//  and the months of life remaining at that month. Acquisition, opening,
//  revaluation, capital additions and changes of estimate reset the base.
//  Each charge is the difference of rounded cumulative amounts, so a
//  life charged monthly and the same life charged annually agree to the
//  shilling and never drift below the residual value.
// =====================================================================
public static class FaMath
{
    public static decimal Round0(decimal v) { return Math.Round(v, 0, MidpointRounding.AwayFromZero); }

    /// <summary>
    /// Straight-line charge for <paramref name="months"/> months, starting <paramref name="monthsAlreadyCharged"/>
    /// months after the base month.
    /// </summary>
    /// <param name="baseValue">Carrying amount at the base.</param>
    /// <param name="residual">Residual value; the carrying amount never goes below it.</param>
    /// <param name="remainingMonths">Months of life left at the base month.</param>
    /// <param name="currentValue">Carrying amount now (before this charge).</param>
    public static decimal StraightLine(decimal baseValue, decimal residual, int remainingMonths,
                                       int monthsAlreadyCharged, int months, decimal currentValue)
    {
        if (months <= 0 || remainingMonths <= 0) return 0m;
        decimal depreciable = baseValue - residual;
        if (depreciable <= 0m) return 0m;

        int k = monthsAlreadyCharged < 0 ? 0 : monthsAlreadyCharged;
        int upto = k + months;
        if (k >= remainingMonths) return Cap(currentValue - residual, currentValue - residual);
        if (upto >= remainingMonths)
        {
            // Last stretch of the life: bring the value to the residual exactly.
            return Cap(currentValue - residual, currentValue - residual);
        }
        decimal monthly = depreciable / remainingMonths;
        decimal charge = Round0(monthly * upto) - Round0(monthly * k);
        return Cap(charge, currentValue - residual);
    }

    /// <summary>
    /// Reducing-balance charge: opening carrying amount of the financial year (or the base value if the
    /// base falls inside the year) x annual rate x months / 12, never below the residual value.
    /// </summary>
    public static decimal ReducingBalance(decimal yearOpeningValue, decimal ratePct, int months,
                                          decimal residual, decimal currentValue)
    {
        if (months <= 0 || ratePct <= 0m) return 0m;
        decimal charge = Round0(yearOpeningValue * ratePct / 100m * months / 12m);
        return Cap(charge, currentValue - residual);
    }

    private static decimal Cap(decimal charge, decimal max)
    {
        if (max < 0m) max = 0m;
        if (charge < 0m) charge = 0m;
        return charge > max ? max : charge;
    }

    /// <summary>Months of life at the start: life in years x 12, rounded.</summary>
    public static int LifeMonths(decimal? lifeYears)
    {
        if (!lifeYears.HasValue || lifeYears.Value <= 0m) return 0;
        return (int)Round0(lifeYears.Value * 12m);
    }

    /// <summary>Straight-line life implied by a rate (100 / rate years), for display and defaults.</summary>
    public static decimal? LifeFromRate(decimal? ratePct)
    {
        if (!ratePct.HasValue || ratePct.Value <= 0m) return null;
        return Math.Round(100m / ratePct.Value, 2);
    }

    public static decimal? RateFromLife(decimal? lifeYears)
    {
        if (!lifeYears.HasValue || lifeYears.Value <= 0m) return null;
        return Math.Round(100m / lifeYears.Value, 4);
    }

    /// <summary>One slice of a depreciation run for one asset, inside one financial year.</summary>
    public class Slice
    {
        public DateTime From;      // first day of the first month charged
        public DateTime To;        // last day of the last month charged
        public int Months;
        public decimal Before;
        public decimal Charge;
        public decimal After;
        public string FinYear;
    }

    /// <summary>The state the engine needs, as posted on the asset row.</summary>
    public class AssetState
    {
        public string Method;              // SL, RB, NONE
        public decimal RatePct;            // RB
        public decimal Residual;
        public decimal Value;              // current carrying amount
        public decimal BaseValue;
        public DateTime BaseMonth;         // first month the base applies to
        public int BaseRemainingMonths;
        public DateTime? DepreciatedTo;    // last month-end already charged
        public DateTime DepStart;          // first month depreciation may be charged
        /// <summary>Carrying amount at the start of a financial year, for reducing balance. Supplied by the caller.</summary>
        public Func<DateTime, decimal> ValueAtYearStart;
    }

    /// <summary>
    /// Works out the charges needed to bring an asset up to <paramref name="periodEnd"/> (a month-end),
    /// one slice per financial year. Pure: the caller decides whether to post them.
    /// </summary>
    public static List<Slice> Plan(AssetState a, DateTime periodEnd, int fyStartMonth)
    {
        var outp = new List<Slice>();
        if (a == null || a.Method == "NONE") return outp;
        periodEnd = new DateTime(periodEnd.Year, periodEnd.Month, 1).AddMonths(1).AddDays(-1);

        DateTime first = new DateTime(a.DepStart.Year, a.DepStart.Month, 1);
        if (a.DepreciatedTo.HasValue)
        {
            DateTime next = new DateTime(a.DepreciatedTo.Value.Year, a.DepreciatedTo.Value.Month, 1).AddMonths(1);
            if (next > first) first = next;
        }
        if (first > periodEnd) return outp;

        decimal value = a.Value;
        DateTime cur = first;
        while (cur <= periodEnd)
        {
            // End of this slice: the earlier of the period end and the end of cur's financial year.
            int fyY = cur.Month >= fyStartMonth ? cur.Year : cur.Year - 1;
            DateTime fyStart = new DateTime(fyY, fyStartMonth, 1);
            DateTime fyEnd = fyStart.AddYears(1).AddDays(-1);
            DateTime sliceEnd = periodEnd < fyEnd ? periodEnd : fyEnd;
            int months = (sliceEnd.Year - cur.Year) * 12 + (sliceEnd.Month - cur.Month) + 1;

            decimal charge = 0m;
            if (a.Method == "SL")
            {
                int already = (cur.Year - a.BaseMonth.Year) * 12 + (cur.Month - a.BaseMonth.Month);
                charge = StraightLine(a.BaseValue, a.Residual, a.BaseRemainingMonths, already, months, value);
            }
            else if (a.Method == "RB")
            {
                decimal opening;
                if (a.BaseMonth >= fyStart) opening = a.BaseValue;
                else if (cur == fyStart) opening = value;
                else opening = a.ValueAtYearStart != null ? a.ValueAtYearStart(fyStart) : value;
                charge = ReducingBalance(opening, a.RatePct, months, a.Residual, value);
            }

            if (charge > 0m)
            {
                var s = new Slice();
                s.From = cur; s.To = sliceEnd; s.Months = months;
                s.Before = value; s.Charge = charge; s.After = value - charge;
                s.FinYear = fyStartMonth == 1 ? fyY.ToString() : fyY + "/" + (fyY + 1);
                outp.Add(s);
                value -= charge;
            }
            if (value <= a.Residual) break;
            cur = sliceEnd.AddDays(1);
        }
        return outp;
    }
}
