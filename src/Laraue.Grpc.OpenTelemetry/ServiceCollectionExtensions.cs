using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Laraue.Grpc.OpenTelemetry;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// One-liner for wiring up Laraue.Grpc's traces and metrics into OpenTelemetry, instead of
    /// every service having to remember to <c>AddSource("Laraue.Grpc")</c> /
    /// <c>AddMeter("Laraue.Grpc")</c> by hand. Exporters (OTLP, Console, Prometheus, ...) are still
    /// the caller's choice via <paramref name="configureTracing"/>/<paramref name="configureMetrics"/> -
    /// this only wires up what's specific to Laraue.Grpc, it doesn't pick exporters for you.
    /// </summary>
    public static IOpenTelemetryBuilder AddLaraueGrpcTelemetry(
        this IServiceCollection services,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        return services
            .AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing.AddLaraueGrpc();
                configureTracing?.Invoke(tracing);
            })
            .WithMetrics(metrics =>
            {
                metrics.AddLaraueGrpc();
                configureMetrics?.Invoke(metrics);
            });
    }
}
