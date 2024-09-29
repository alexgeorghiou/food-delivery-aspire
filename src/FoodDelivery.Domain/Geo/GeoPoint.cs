namespace FoodDelivery.Domain.Geo;

/// <summary>
/// A WGS84 coordinate. Latitude is in [-90, 90], longitude in [-180, 180].
/// </summary>
public readonly record struct GeoPoint
{
    public GeoPoint(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be between -90 and 90.");
        }

        if (double.IsNaN(longitude) || longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be between -180 and 180.");
        }

        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }

    public double Longitude { get; }

    public override string ToString() => FormattableString.Invariant($"({Latitude:F6}, {Longitude:F6})");
}
