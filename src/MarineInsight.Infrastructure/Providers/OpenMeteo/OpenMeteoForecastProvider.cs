using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MarineInsight.Application.Errors;
using MarineInsight.Application.Forecast;
using MarineInsight.Domain.Forecast;
using Microsoft.Extensions.Options;

namespace MarineInsight.Infrastructure.Providers.OpenMeteo;

public abstract class OpenMeteoForecastProvider
{
    protected const string ProviderCodeValue = "open-meteo";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IOptions<OpenMeteoOptions> _options;
    private readonly TimeProvider _timeProvider;

    protected OpenMeteoForecastProvider(
        HttpClient httpClient,
        IOptions<OpenMeteoOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _httpClient = httpClient;
        _options = options;
        _timeProvider = timeProvider;
        _options.Value.Validate();
    }

    protected OpenMeteoOptions Options => _options.Value;

    protected DateTimeOffset UtcNow => _timeProvider.GetUtcNow().ToUniversalTime();

    protected async Task<OpenMeteoForecastResponse> GetResponseAsync(
        GeoPoint location,
        ForecastRange range,
        string endpoint,
        string model,
        IReadOnlyCollection<string> hourlyVariables,
        bool isWeather,
        CancellationToken cancellationToken)
    {
        if (!Options.Enabled)
        {
            throw new ProviderException(
                ProviderCodeValue,
                ProviderFailureKind.Unavailable,
                "The Open-Meteo provider is disabled.",
                isTransient: false);
        }

        if (range.Hours is not (24 or 72 or 168))
        {
            throw new ArgumentOutOfRangeException(nameof(range), "Forecast range must be 24, 72, or 168 hours.");
        }

        var requestUri = OpenMeteoRequestBuilder.Build(
            endpoint,
            location,
            range,
            model,
            hourlyVariables,
            isWeather,
            Options.ApiKey);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(Options.Timeout);

        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProviderTimeoutException(
                ProviderCodeValue,
                "The Open-Meteo request timed out.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ProviderException(
                ProviderCodeValue,
                ProviderFailureKind.Unavailable,
                "The Open-Meteo endpoint could not be reached.",
                isTransient: true,
                innerException: exception);
        }

        using (response)
        {
            EnsureSuccess(response);

            string payload;
            try
            {
                payload = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ProviderTimeoutException(
                    ProviderCodeValue,
                    "The Open-Meteo response timed out while being read.",
                    exception);
            }

            try
            {
                var result = JsonSerializer.Deserialize<OpenMeteoForecastResponse>(payload, JsonOptions);
                return result ?? throw new JsonException("The Open-Meteo response was empty.");
            }
            catch (JsonException exception)
            {
                throw new ProviderContractException(
                    ProviderCodeValue,
                    "The Open-Meteo response JSON does not match the expected contract.",
                    exception);
            }
        }
    }

    private void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var statusCode = response.StatusCode;
        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new ProviderAuthenticationException(
                ProviderCodeValue,
                "The Open-Meteo endpoint rejected the configured credentials.");
        }

        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            throw new ProviderRateLimitedException(
                ProviderCodeValue,
                "The Open-Meteo endpoint rate-limited the request.",
                ParseRetryAfter(response.Headers.RetryAfter));
        }

        if (statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout)
        {
            throw new ProviderTimeoutException(
                ProviderCodeValue,
                "The Open-Meteo endpoint reported a request timeout.");
        }

        if ((int)statusCode >= 500)
        {
            throw new ProviderException(
                ProviderCodeValue,
                ProviderFailureKind.Unavailable,
                "The Open-Meteo endpoint reported a temporary server failure.",
                isTransient: true);
        }

        // Do not include the request URI or response body: either can contain a provider key.
        throw new ProviderContractException(
            ProviderCodeValue,
            $"The Open-Meteo endpoint rejected the request with HTTP {(int)statusCode}.");
    }

    private DateTimeOffset? ParseRetryAfter(RetryConditionHeaderValue? retryAfter)
    {
        if (retryAfter is null)
        {
            return null;
        }

        if (retryAfter.Delta is { } delta)
        {
            return UtcNow.Add(delta);
        }

        return retryAfter.Date?.ToUniversalTime();
    }
}
