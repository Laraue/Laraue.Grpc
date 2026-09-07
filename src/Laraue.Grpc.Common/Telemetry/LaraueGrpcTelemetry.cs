using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Laraue.Grpc.Common.Telemetry;

/// <summary>
/// Shared <see cref="ActivitySource"/> and <see cref="Meter"/> used by both
/// <c>Laraue.Grpc.Server</c> and <c>Laraue.Grpc.Client</c> interceptors.
/// Consuming apps opt in by name, no OpenTelemetry package reference is required here:
/// <code>
/// services.AddOpenTelemetry()
///     .WithTracing(t => t.AddSource(LaraueGrpcTelemetry.SourceName))
///     .WithMetrics(m => m.AddMeter(LaraueGrpcTelemetry.SourceName));
/// </code>
/// </summary>
public static class LaraueGrpcTelemetry
{
    public const string SourceName = "Laraue.Grpc";

    private static readonly string Version =
        typeof(LaraueGrpcTelemetry).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    public static readonly ActivitySource ActivitySource = new(SourceName, Version);

    private static readonly Meter Meter = new(SourceName, Version);

    /// <summary>
    /// Duration of an outgoing gRPC call in milliseconds, tagged with <see cref="GrpcTags.RpcSystem"/> et al.
    /// Named per the OpenTelemetry semconv metric <c>rpc.client.duration</c>.
    /// </summary>
    public static readonly Histogram<double> ClientCallDuration = Meter.CreateHistogram<double>(
        "rpc.client.duration",
        unit: "ms",
        description: "Duration of an outgoing gRPC call.");

    /// <summary>
    /// Duration of an incoming gRPC call in milliseconds, tagged with <see cref="GrpcTags.RpcSystem"/> et al.
    /// Named per the OpenTelemetry semconv metric <c>rpc.server.duration</c>.
    /// </summary>
    public static readonly Histogram<double> ServerCallDuration = Meter.CreateHistogram<double>(
        "rpc.server.duration",
        unit: "ms",
        description: "Duration of an incoming gRPC call.");

    public static readonly Counter<long> CallsStarted = Meter.CreateCounter<long>(
        "rpc.calls.started",
        description: "Number of gRPC calls started.");

    public static readonly Counter<long> CallsFailed = Meter.CreateCounter<long>(
        "rpc.calls.failed",
        description: "Number of gRPC calls that completed with a non-OK status.");
}
