namespace FoodDelivery.Domain.Menus;

/// <summary>
/// A single opening window that starts on <see cref="Day"/>. When <see cref="Closes"/> is
/// earlier than or equal to <see cref="Opens"/> the window runs past midnight into the next day
/// (e.g. Friday 18:00 - 02:00).
/// </summary>
public sealed record OpeningPeriod(DayOfWeek Day, TimeOnly Opens, TimeOnly Closes)
{
    public bool CrossesMidnight => Closes <= Opens;

    public TimeSpan Duration => CrossesMidnight
        ? TimeSpan.FromDays(1) - (Opens - Closes)
        : Closes - Opens;

    public override string ToString() => $"{Day} {Opens:HH\:mm}-{Closes:HH\:mm}";
}
