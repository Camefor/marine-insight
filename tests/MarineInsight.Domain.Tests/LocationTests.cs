using MarineInsight.Domain.Forecast;
using MarineInsight.Domain.Location;
using LocationEntity = MarineInsight.Domain.Location.Location;

namespace MarineInsight.Domain.Tests;

public sealed class LocationTests
{
    [Fact]
    public void LocationNormalizesNamesAndKeepsForecastCoordinates()
    {
        var location = new LocationEntity(
            Guid.NewGuid(),
            " Dongji-Island ",
            " 东极岛 ",
            30.194,
            122.687,
            "Asia/Shanghai",
            LocationType.Island,
            null,
            true,
            new DateTimeOffset(2026, 7, 16, 8, 0, 0, TimeSpan.FromHours(8)));

        Assert.Equal("dongji-island", location.NormalizedName);
        Assert.Equal("东极岛", location.DisplayName);
        Assert.Equal(new GeoPoint(30.194, 122.687), location.Coordinates);
        Assert.Equal(DateTimeOffset.UtcNow.Offset, location.CreatedAtUtc.Offset);
    }

    [Fact]
    public void LocationRejectsInvalidCoastOrientation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LocationEntity(
            Guid.NewGuid(),
            "test",
            "Test",
            30,
            122,
            "Asia/Shanghai",
            LocationType.Island,
            360,
            true,
            DateTimeOffset.UtcNow));
    }
}
