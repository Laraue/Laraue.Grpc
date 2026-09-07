using System.Diagnostics;
using Grpc.Core;

namespace Laraue.Grpc.Common.Telemetry;

/// <summary>
/// W3C trace-context propagation over gRPC metadata, shared by the client interceptor (which
/// injects) and the server interceptor (which extracts). The server side extracts explicitly
/// rather than relying on ambient <see cref="Activity.Current"/>/ASP.NET Core hosting
/// instrumentation, since that is only populated when something (e.g. OpenTelemetry's
/// AspNetCore instrumentation) is already listening to the "Microsoft.AspNetCore" source - a
/// consuming app that only subscribes to "Laraue.Grpc" would otherwise silently start a fresh
/// root trace on every incoming call instead of continuing the caller's trace.
/// </summary>
public static class GrpcTraceContextPropagator
{
    public static void Inject(Activity? activity, Metadata headers)
    {
        DistributedContextPropagator.Current.Inject(
            activity,
            headers,
            static (carrier, key, value) => ((Metadata)carrier!).Add(key, value));
    }

    public static ActivityContext Extract(Metadata headers)
    {
        DistributedContextPropagator.Current.ExtractTraceIdAndState(
            headers,
            static (object? carrier, string fieldName, out string? fieldValue, out IEnumerable<string>? fieldValues) =>
            {
                fieldValues = null;
                fieldValue = ((Metadata)carrier!).FirstOrDefault(h => h.Key == fieldName)?.Value;
            },
            out var traceParent,
            out var traceState);

        return ActivityContext.TryParse(traceParent, traceState, isRemote: true, out var context)
            ? context
            : default;
    }
}
