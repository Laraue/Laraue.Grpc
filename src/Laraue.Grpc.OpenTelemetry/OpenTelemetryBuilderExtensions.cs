using Laraue.Grpc.Common.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Laraue.Grpc.OpenTelemetry;

/// <summary>
/// Subscribes the <c>"Laraue.Grpc"</c> <see cref="System.Diagnostics.ActivitySource"/>/
/// <see cref="System.Diagnostics.Metrics.Meter"/> by name, so callers don't need to know or
/// hard-code that string themselves.
/// </summary>
public static class OpenTelemetryBuilderExtensions
{
    public static TracerProviderBuilder AddLaraueGrpc(this TracerProviderBuilder builder) =>
        builder.AddSource(LaraueGrpcTelemetry.SourceName);

    public static MeterProviderBuilder AddLaraueGrpc(this MeterProviderBuilder builder) =>
        builder.AddMeter(LaraueGrpcTelemetry.SourceName);
}
