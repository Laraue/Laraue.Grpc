using System.Diagnostics;
using Laraue.Grpc.Common.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Laraue.Grpc.OpenTelemetry.UnitTests;

/// <summary>
/// Proves <see cref="ServiceCollectionExtensions.AddLaraueGrpcTelemetry"/> actually wires
/// "Laraue.Grpc" into a real OpenTelemetry pipeline (using the InMemory exporter to observe what
/// would otherwise go to OTLP/Prometheus/etc.), not just that it compiles.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLaraueGrpcTelemetry_ShouldExportActivitiesFromLaraueGrpcSource_Always()
    {
        var exportedActivities = new List<Activity>();

        var services = new ServiceCollection();
        services.AddLaraueGrpcTelemetry(configureTracing: t => t.AddInMemoryExporter(exportedActivities));

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TracerProvider>();

        using (LaraueGrpcTelemetry.ActivitySource.StartActivity("TestOp"))
        {
        }

        var exported = Assert.Single(exportedActivities);
        Assert.Equal("TestOp", exported.DisplayName);
    }

    [Fact]
    public void AddLaraueGrpcTelemetry_ShouldNotExportActivitiesFromUnrelatedSources_Always()
    {
        var exportedActivities = new List<Activity>();
        using var unrelatedSource = new ActivitySource("Some.Other.Source");

        var services = new ServiceCollection();
        services.AddLaraueGrpcTelemetry(configureTracing: t => t.AddInMemoryExporter(exportedActivities));

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TracerProvider>();

        using (unrelatedSource.StartActivity("Unrelated"))
        {
        }

        Assert.Empty(exportedActivities);
    }

    [Fact]
    public void AddLaraueGrpcTelemetry_ShouldExportMetricsFromLaraueGrpcMeter_Always()
    {
        var exportedMetrics = new List<Metric>();

        var services = new ServiceCollection();
        services.AddLaraueGrpcTelemetry(configureMetrics: m => m.AddInMemoryExporter(exportedMetrics));

        using var provider = services.BuildServiceProvider();
        var meterProvider = provider.GetRequiredService<MeterProvider>();

        LaraueGrpcTelemetry.CallsStarted.Add(1);
        meterProvider.ForceFlush();

        Assert.Contains(exportedMetrics, m => m.Name == "rpc.calls.started");
    }
}
