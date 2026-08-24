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
        Assert.Contains("项目完全开源", html, StringComparison.Ordinal);
        Assert.Contains("https://github.com/Camefor/marine-insight", html, StringComparison.Ordinal);
        Assert.Contains("target=\"_blank\"", html, StringComparison.Ordinal);
        Assert.Contains("rel=\"noopener noreferrer\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-theme-toggle", html, StringComparison.Ordinal);
        Assert.Contains("js/theme.js", html, StringComparison.Ordinal);
        Assert.Contains("免责声明", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThemeScriptDetectsSystemPreferenceWithTimeFallbackAndExposesScrollHelper()
    {
        using var factory = new MarineAnalysisApiTests.ApiTestApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/js/theme.js");
        var script = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("prefers-color-scheme: dark", script, StringComparison.Ordinal);
        Assert.Contains("prefers-color-scheme: light", script, StringComparison.Ordinal);
        Assert.Contains("new Date().getHours()", script, StringComparison.Ordinal);
        Assert.Contains("document.documentElement.dataset.theme", script, StringComparison.Ordinal);
        Assert.Contains("window.scrollToAnchor", script, StringComparison.Ordinal);
        Assert.Contains("scrollIntoView", script, StringComparison.Ordinal);
        Assert.DoesNotContain("localStorage", script, StringComparison.Ordinal);
        Assert.DoesNotContain("data-theme-toggle", script, StringComparison.Ordinal);
    }
}
