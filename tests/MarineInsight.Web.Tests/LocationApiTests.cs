using System.Net;
using System.Text.Json;

namespace MarineInsight.Web.Tests;

public sealed class LocationApiTests
{
    [Fact]
    public async Task SearchReturnsPresetLocationProjection()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();
        using var client = factory.CreateClient();
        await factory.MigrateDatabaseAsync();

        using var response = await client.GetAsync("/api/v1/locations/search?q=东极岛");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var location = Assert.Single(document.RootElement.EnumerateArray());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(response.Headers.GetValues("Trace-Id").Single()));
        Assert.Equal("东极岛", location.GetProperty("displayName").GetString());
        Assert.Equal("island", location.GetProperty("locationType").GetString());
        Assert.Equal("Asia/Shanghai", location.GetProperty("timeZone").GetString());
        Assert.Equal("preset", location.GetProperty("source").GetString());
    }

    [Fact]
    public async Task NearbyReturnsTheClosestPresetLocationFirst()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();
        using var client = factory.CreateClient();
        await factory.MigrateDatabaseAsync();

        using var response = await client.GetAsync(
            "/api/v1/locations/nearby?lat=30.194&lon=122.687&radiusKm=500&limit=3");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var locations = document.RootElement.EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, locations.Length);
        Assert.Equal("东极岛", locations[0].GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task SearchWithoutTextReturnsValidationProblemDetails()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/locations/search");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", document.RootElement.GetProperty("code").GetString());
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("q", out _));
    }
}
