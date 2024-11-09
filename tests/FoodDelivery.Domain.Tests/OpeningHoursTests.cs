using FoodDelivery.Domain.Menus;

namespace FoodDelivery.Domain.Tests;

public class OpeningHoursTests
{
    // 2026-03-02 is a Monday.
    private static DateTime Monday(int hour, int minute = 0) => new(2026, 3, 2, hour, minute, 0);

    private static readonly OpeningHours LunchAndDinner = new(
    [
        new OpeningPeriod(DayOfWeek.Monday, new TimeOnly(11, 30), new TimeOnly(14, 30)),
        new OpeningPeriod(DayOfWeek.Monday, new TimeOnly(18, 0), new TimeOnly(22, 0)),
    ]);

    [Theory]
    [InlineData(11, 30, true)]
    [InlineData(14, 29, true)]
    [InlineData(14, 30, false)]
    [InlineData(16, 0, false)]
    [InlineData(18, 0, true)]
    [InlineData(21, 59, true)]
    [InlineData(22, 0, false)]
    [InlineData(9, 0, false)]
    public void Opens_inclusive_and_closes_exclusive(int hour, int minute, bool expected)
    {
        Assert.Equal(expected, LunchAndDinner.IsOpenAt(Monday(hour, minute)));
    }

    [Fact]
    public void Overnight_period_spills_into_the_next_day()
    {
        var lateNight = new OpeningHours([new OpeningPeriod(DayOfWeek.Friday, new TimeOnly(18, 0), new TimeOnly(2, 0))]);
        var friday = new DateTime(2026, 3, 6);

        Assert.True(lateNight.IsOpenAt(friday.AddHours(23)));
        Assert.True(lateNight.IsOpenAt(friday.AddDays(1).AddHours(1).AddMinutes(59)));
        Assert.False(lateNight.IsOpenAt(friday.AddDays(1).AddHours(2)));
        Assert.False(lateNight.IsOpenAt(friday.AddHours(1)));
    }

    [Fact]
    public void Sunday_overnight_wraps_to_monday()
    {
        var hours = new OpeningHours([new OpeningPeriod(DayOfWeek.Sunday, new TimeOnly(20, 0), new TimeOnly(1, 0))]);
        Assert.True(hours.IsOpenAt(Monday(0, 30)));
        Assert.False(hours.IsOpenAt(Monday(1, 0)));
    }

    [Fact]
    public void Same_open_and_close_time_means_open_all_day()
    {
        var hours = new OpeningHours([new OpeningPeriod(DayOfWeek.Monday, new TimeOnly(0, 0), new TimeOnly(0, 0))]);
        Assert.True(hours.IsOpenAt(Monday(0)));
        Assert.True(hours.IsOpenAt(Monday(23, 59)));
        Assert.False(hours.IsOpenAt(Monday(0).AddDays(1)));
    }

    [Fact]
    public void Instant_is_evaluated_in_the_restaurant_time_zone()
    {
        var tz = TimeZoneInfo.CreateCustomTimeZone("UTC+2", TimeSpan.FromHours(2), "UTC+2", "UTC+2");
        // 09:45 UTC is 11:45 local -> inside lunch.
        var instant = new DateTimeOffset(2026, 3, 2, 9, 45, 0, TimeSpan.Zero);

        Assert.True(LunchAndDinner.IsOpenAt(instant, tz));
        Assert.False(LunchAndDinner.IsOpenAt(instant, TimeZoneInfo.Utc));
    }

    [Fact]
    public void Next_opening_finds_the_upcoming_period_the_same_day()
    {
        Assert.Equal(Monday(18), LunchAndDinner.NextOpening(Monday(15)));
    }

    [Fact]
    public void Next_opening_rolls_over_to_next_week()
    {
        Assert.Equal(Monday(11, 30).AddDays(7), LunchAndDinner.NextOpening(Monday(22, 30)));
    }

    [Fact]
    public void Next_opening_returns_input_when_open()
    {
        Assert.Equal(Monday(12), LunchAndDinner.NextOpening(Monday(12)));
    }

    [Fact]
    public void Empty_schedule_is_always_closed()
    {
        Assert.False(OpeningHours.AlwaysClosed.IsOpenAt(Monday(12)));
        Assert.Null(OpeningHours.AlwaysClosed.NextOpening(Monday(12)));
    }

    [Fact]
    public void Overlapping_periods_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new OpeningHours(
        [
            new OpeningPeriod(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(15, 0)),
            new OpeningPeriod(DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(20, 0)),
        ]));
    }

    [Fact]
    public void Overnight_period_overlapping_next_morning_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new OpeningHours(
        [
            new OpeningPeriod(DayOfWeek.Saturday, new TimeOnly(20, 0), new TimeOnly(3, 0)),
            new OpeningPeriod(DayOfWeek.Sunday, new TimeOnly(2, 0), new TimeOnly(10, 0)),
        ]));
    }

    [Fact]
    public void Sunday_overnight_overlapping_monday_morning_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new OpeningHours(
        [
            new OpeningPeriod(DayOfWeek.Monday, new TimeOnly(0, 30), new TimeOnly(10, 0)),
            new OpeningPeriod(DayOfWeek.Sunday, new TimeOnly(22, 0), new TimeOnly(1, 0)),
        ]));
    }

    [Fact]
    public void Adjacent_periods_are_allowed()
    {
        var hours = new OpeningHours(
        [
            new OpeningPeriod(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(14, 0)),
            new OpeningPeriod(DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(20, 0)),
        ]);

        Assert.True(hours.IsOpenAt(Monday(14)));
    }
}
