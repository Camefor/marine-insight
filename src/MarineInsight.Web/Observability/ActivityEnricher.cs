using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace MarineInsight.Web.Observability;

public sealed class ActivityEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        var activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("traceId", activity.TraceId.ToHexString()));
        logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("spanId", activity.SpanId.ToHexString()));
    }
}
