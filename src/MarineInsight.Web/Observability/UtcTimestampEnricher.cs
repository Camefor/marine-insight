using Serilog.Core;
using Serilog.Events;

namespace MarineInsight.Web.Observability;

public sealed class UtcTimestampEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        logEvent.AddOrUpdateProperty(
            propertyFactory.CreateProperty("timestamp", logEvent.Timestamp.UtcDateTime));
    }
}
