namespace MarineInsight.Application.Errors;

/// <summary>
/// Signals an operational cache backend failure. Cache callers may bypass this exception
/// because the cache is an optimization layer, not the source of business truth.
/// </summary>
public sealed class CacheUnavailableException : MarineInsightException
{
    public CacheUnavailableException(string message, Exception? innerException = null)
        : base(MarineInsightErrorCodes.CacheUnavailable, message, innerException)
    {
    }
}
