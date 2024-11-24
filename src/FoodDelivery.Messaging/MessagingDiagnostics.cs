using System.Diagnostics;
using System.Text;

namespace FoodDelivery.Messaging;

/// <summary>
/// Propagates W3C trace context through AMQP headers so a request that places an order,
/// the dispatch decision and the courier-assigned update show up as one trace in the dashboard.
/// </summary>
public static class MessagingDiagnostics
{
    public const string ActivitySourceName = "FoodDelivery.Messaging";
    public const string TraceParentHeader = "traceparent";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static void InjectTraceContext(IDictionary<string, object?> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        if (Activity.Current is { } current)
        {
            headers[TraceParentHeader] = current.Id;
        }
    }

    public static ActivityContext ExtractTraceContext(IDictionary<string, object?>? headers)
    {
        if (headers is not null
            && headers.TryGetValue(TraceParentHeader, out var raw)
            && raw is byte[] bytes
            && ActivityContext.TryParse(Encoding.UTF8.GetString(bytes), null, out var context))
        {
            return context;
        }

        return default;
    }
}
