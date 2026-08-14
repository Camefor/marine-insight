using MarineInsight.Web.Components.Features.Dashboard;

namespace MarineInsight.Web.Tests;

public sealed class ClientTimeZoneTests
{
    [Fact]
    public void ResolveNullOrUnknownIdReturnsFallbackShanghai()
    {
        Assert.Equal("Asia/Shanghai", ClientTimeZone.Resolve(null).Id);
        Assert.Equal("Asia/Shanghai", ClientTimeZone.Resolve("").Id);
        Assert.Equal("Asia/Shanghai", ClientTimeZone.Resolve("Not/AZone").Id);
    }

    [Fact]
    public void ResolveValidIanaIdReturnsZone()
    {
        Assert.Equal("America/New_York", ClientTimeZone.Resolve("America/New_York").Id);
    }

    [Fact]
    public void BuildDisplayLabelShanghaiIsBeijingUtcPlus8()
    {
        var zone = ClientTimeZone.Resolve("Asia/Shanghai");

        Assert.Equal("北京时间（UTC+8）", ClientTimeZone.BuildDisplayLabel(zone));
    }

    [Fact]
    public void ToUtcShanghaiLocalConvertsToUtc()
    {
        var zone = ClientTimeZone.FallbackZone;
        var local = new DateTime(2026, 7, 16, 11, 0, 0);

        var utc = ClientTimeZone.ToUtc(local, zone);

        Assert.Equal(new DateTimeOffset(2026, 7, 16, 3, 0, 0, TimeSpan.Zero), utc);
    }

    [Fact]
    public void ToLocalUtcConvertsToShanghaiAndRoundTrips()
    {
        var zone = ClientTimeZone.FallbackZone;
        var utc = new DateTimeOffset(2026, 7, 16, 3, 0, 0, TimeSpan.Zero);

        var local = ClientTimeZone.ToLocal(utc, zone);
        var roundTrip = ClientTimeZone.ToUtc(local.DateTime, zone);

        Assert.Equal(2026, local.Year);
        Assert.Equal(11, local.Hour);
        Assert.Equal(utc, roundTrip);
    }

    [Fact]
    public void NextLocalHourReturnsTopOfNextHour()
    {
        var zone = ClientTimeZone.FallbackZone;

        var next = ClientTimeZone.NextLocalHour(zone);

        Assert.Equal(0, next.Minute);
        Assert.Equal(0, next.Second);
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
        Assert.True(next > nowLocal.DateTime, "下一整点应在当前本地时间之后");
    }
}
