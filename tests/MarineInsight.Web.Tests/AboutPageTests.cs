using System.Net;

namespace MarineInsight.Web.Tests;

public sealed class AboutPageTests
{
    [Fact]
    public async Task AboutPageRendersFeaturesAndCta()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/about");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("海岛海况智能决策平台", html, StringComparison.Ordinal);
        Assert.Contains("开始查询", html, StringComparison.Ordinal);
        Assert.Contains("核心功能", html, StringComparison.Ordinal);
        Assert.Contains("适用场景", html, StringComparison.Ordinal);
        Assert.Contains("免责声明", html, StringComparison.Ordinal);
    }
}
