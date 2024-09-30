namespace FoodDelivery.Domain.Menus;

/// <summary>
/// Weekly opening schedule expressed in the restaurant's local time zone.
/// </summary>
public sealed class OpeningHours
{
    private readonly IReadOnlyList<OpeningPeriod> _periods;

    public OpeningHours(IEnumerable<OpeningPeriod> periods)
    {
        ArgumentNullException.ThrowIfNull(periods);
        _periods = periods.OrderBy(p => p.Day).ThenBy(p => p.Opens).ToArray();
        EnsureNoOverlap(_periods);
    }

    public IReadOnlyList<OpeningPeriod> Periods => _periods;

    public static OpeningHours AlwaysClosed { get; } = new([]);

    /// <summary>Checks whether the restaurant is open at a wall-clock local time.</summary>
    public bool IsOpenAt(DateTime localTime) => FindPeriodCovering(localTime) is not null;

    /// <summary>Checks whether the restaurant is open at an absolute instant.</summary>
    public bool IsOpenAt(DateTimeOffset instant, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        return IsOpenAt(TimeZoneInfo.ConvertTime(instant, timeZone).DateTime);
    }

    /// <summary>
    /// Returns the local wall-clock time the restaurant next opens at or after <paramref name="localTime"/>,
    /// or <c>null</c> when the schedule is empty. If it is currently open, the input is returned.
    /// </summary>
    public DateTime? NextOpening(DateTime localTime)
    {
        if (_periods.Count == 0)
        {
            return null;
        }

        if (IsOpenAt(localTime))
        {
            return localTime;
        }

        DateTime? best = null;
        for (var offset = 0; offset <= 7; offset++)
        {
            var date = localTime.Date.AddDays(offset);
            foreach (var period in _periods.Where(p => p.Day == date.DayOfWeek))
            {
                var start = date + period.Opens.ToTimeSpan();
                if (start >= localTime && (best is null || start < best))
                {
                    best = start;
                }
            }

            if (best is not null)
            {
                return best;
            }
        }

        return best;
    }

    private OpeningPeriod? FindPeriodCovering(DateTime localTime)
    {
        var time = TimeOnly.FromDateTime(localTime);
        var today = localTime.DayOfWeek;
        var yesterday = (DayOfWeek)(((int)today + 6) % 7);

        foreach (var period in _periods)
        {
            if (period.Day == today)
            {
                var closesToday = period.CrossesMidnight || time < period.Closes;
                if (time >= period.Opens && closesToday)
                {
                    return period;
                }
            }
            else if (period.Day == yesterday && period.CrossesMidnight && time < period.Closes)
            {
                return period;
            }
        }

        return null;
    }

    private static void EnsureNoOverlap(IReadOnlyList<OpeningPeriod> periods)
    {
        // Project every period onto a minute-of-week line and check neighbours.
        const int minutesPerWeek = 7 * 24 * 60;
        var ranges = periods
            .Select(p =>
            {
                var start = ((int)p.Day * 24 * 60) + (p.Opens.Hour * 60) + p.Opens.Minute;
                return (Period: p, Start: start, End: start + (int)p.Duration.TotalMinutes);
            })
            .OrderBy(r => r.Start)
            .ToArray();

        for (var i = 0; i < ranges.Length; i++)
        {
            var current = ranges[i];
            var next = ranges[(i + 1) % ranges.Length];
            if (ranges.Length == 1)
            {
                break;
            }

            var nextStart = i == ranges.Length - 1 ? next.Start + minutesPerWeek : next.Start;
            if (current.End > nextStart)
            {
                throw new ArgumentException($"Opening periods overlap: {current.Period} and {next.Period}.", nameof(periods));
            }
        }
    }
}
