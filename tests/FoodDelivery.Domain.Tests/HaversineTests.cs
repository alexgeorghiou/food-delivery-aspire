using FoodDelivery.Domain.Geo;

namespace FoodDelivery.Domain.Tests;

public class HaversineTests
{
    [Fact]
    public void Distance_to_self_is_zero()
    {
        var p = new GeoPoint(51.5007, -0.1246);
        Assert.Equal(0, Haversine.DistanceKm(p, p), precision: 9);
    }

    [Fact]
    public void London_to_Paris_is_about_344_km()
    {
        var london = new GeoPoint(51.5074, -0.1278);
        var paris = new GeoPoint(48.8566, 2.3522);

        var km = Haversine.DistanceKm(london, paris);

        Assert.InRange(km, 342, 346);
    }

    [Fact]
    public void Distance_is_symmetric()
    {
        var a = new GeoPoint(40.7128, -74.0060);
        var b = new GeoPoint(34.0522, -118.2437);

        Assert.Equal(Haversine.DistanceKm(a, b), Haversine.DistanceKm(b, a), precision: 9);
    }

    [Fact]
    public void One_degree_of_latitude_is_about_111_km()
    {
        var km = Haversine.DistanceKm(new GeoPoint(0, 0), new GeoPoint(1, 0));
        Assert.InRange(km, 111.1, 111.3);
    }

    [Fact]
    public void Crossing_the_antimeridian_takes_the_short_way()
    {
        var west = new GeoPoint(0, 179.5);
        var east = new GeoPoint(0, -179.5);

        Assert.InRange(Haversine.DistanceKm(west, east), 111.1, 111.3);
    }

    [Fact]
    public void Antipodal_points_are_half_the_circumference_apart()
    {
        var km = Haversine.DistanceKm(new GeoPoint(0, 0), new GeoPoint(0, 180));
        Assert.Equal(Math.PI * Haversine.EarthRadiusKm, km, precision: 6);
    }

    [Fact]
    public void Meters_are_km_times_1000()
    {
        var a = new GeoPoint(52.5200, 13.4050);
        var b = new GeoPoint(52.5163, 13.3777);

        Assert.Equal(Haversine.DistanceKm(a, b) * 1000, Haversine.DistanceMeters(a, b), precision: 6);
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-90.0001, 0)]
    [InlineData(0, 180.1)]
    [InlineData(double.NaN, 0)]
    public void Invalid_coordinates_are_rejected(double lat, double lon)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeoPoint(lat, lon));
    }
}
