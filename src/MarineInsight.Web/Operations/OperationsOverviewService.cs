using MarineInsight.Application.Operations;
using MarineInsight.Application.Operations.Ports;
using MarineInsight.Domain.Analysis;
using MarineInsight.Infrastructure.Providers.OpenMeteo;
using MarineInsight.Infrastructure.Providers.WorldTides;
using Microsoft.Extensions.Options;

namespace MarineInsight.Web.Operations;

public sealed class OperationsOverviewService(
    IOptions<OpenMeteoOptions> openMeteoOptions,
    IOptions<WorldTidesOptions> worldTidesOptions,
    IOperationalReadRepository auditRepository)
{
    public async Task<OperationsOverview> GetAsync(CancellationToken cancellationToken = default)
    {
        var openMeteo = openMeteoOptions.Value;
        var worldTides = worldTidesOptions.Value;
        var parameters = MarineAlgorithmParameters.CreateDefault();
        var providers = new[]
        {
            CreateOpenMeteo("weather", openMeteo.WeatherBaseUrl, openMeteo.WeatherModel, openMeteo),
            CreateOpenMeteo("marine", openMeteo.MarineBaseUrl, openMeteo.MarineModel, openMeteo),
            new ProviderOperationalStatus(
                "worldtides",
                "tide",
                worldTides.Enabled,
                !string.IsNullOrWhiteSpace(worldTides.ApiKey),
                Host(worldTides.BaseUrl),
                "worldtides-v3",
                worldTides.Enabled && !string.IsNullOrWhiteSpace(worldTides.ApiKey) ? "ready" : "degraded",
                worldTides.Enabled
                    ? "潮汐按 UTC 日期与坐标网格使用长 TTL 缓存。"
                    : "潮汐未启用，基础海况分析继续可用。")
        };
        var algorithm = new AlgorithmOperationalStatus(
            parameters.Version,
            parameters.SchemaVersion,
            parameters.ConfigurationHash,
            "published");
        return new OperationsOverview(
            providers,
            algorithm,
            await auditRepository.ListAuditLogsAsync(100, cancellationToken));
    }

    private static ProviderOperationalStatus CreateOpenMeteo(
        string domain,
        string endpoint,
        string model,
        OpenMeteoOptions options) => new(
        "open-meteo",
        domain,
        options.Enabled,
        true,
        Host(endpoint),
        model,
        options.Enabled ? "ready" : "disabled",
        options.Enabled ? "主数据源已启用；开放端点不要求凭据。" : "主数据源已关闭，基础查询将不可用。");

    private static string Host(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ? uri.Host : "invalid";
}
