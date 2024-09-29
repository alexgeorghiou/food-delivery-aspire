namespace FoodDelivery.Domain.Geo;

/// <summary>
/// Great-circle distance using the haversine formula. Accurate to well under 0.5%
/// at city scale, which is far below the noise of phone GPS fixes.
/// </summary>
public static class Haversine
{
    /// <summary>Mean Earth radius in kilometres (IUGG).</summary>
    public const double EarthRadiusKm = 6371.0088;

    public static double DistanceKm(GeoPoint from, GeoPoint to)
    {
        var dLat = ToRadians(to.Latitude - from.Latitude);
        var dLon = ToRadians(to.Longitude - from.Longitude);
        var lat1 = ToRadians(from.Latitude);
        var lat2 = ToRadians(to.Latitude);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        // Clamp guards against tiny floating point overshoot for antipodal points.
        var c = 2 * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
        return EarthRadiusKm * c;
    }

    public static double DistanceMeters(GeoPoint from, GeoPoint to) => DistanceKm(from, to) * 1000.0;

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
