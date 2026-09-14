using System.Diagnostics;
using Grpc.Core;
using Laraue.Grpc.IntegrationTests.DemoApp;
using Laraue.Grpc.IntegrationTests.Infrastructure;

namespace Laraue.Grpc.IntegrationTests;

/// <summary>
/// End-to-end tests: a real <see cref="Greeter.GreeterClient"/> talks to a real
/// <see cref="GreeterService"/> over an in-memory <see cref="Microsoft.AspNetCore.TestHost.TestServer"/>,
/// through both <c>Laraue.Grpc.Server</c> and <c>Laraue.Grpc.Client</c> interceptors, to prove traces and
/// metrics actually show up rather than just unit-testing the interceptor logic in isolation.
/// All facts run sequentially - not just within this class (xunit's default) but also relative to
/// any other class sharing the "GrpcIntegrationTests" collection - since <c>ActivitySpy</c>/
/// <c>MetricSpy</c> subscribe to the shared process-wide "Laraue.Grpc" source/meter and would
/// otherwise pick up calls made by a concurrently-running test class too.
/// </summary>
[Collection("GrpcIntegrationTests")]
public class GrpcTelemetryTests : IAsyncLifetime
{
    private GrpcTestHost _host = null!;

    public async Task InitializeAsync() => _host = await GrpcTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task SayHello_ShouldRecordClientAndServerActivitiesSharingOneTrace_Always()
    {
        using var activitySpy = new ActivitySpy();

        var client = _host.CreateGreeterClient();
        var reply = await client.SayHelloAsync(new HelloRequest { Name = "World" });

        Assert.Equal("Hello World", reply.Message);

        Assert.Equal(2, activitySpy.Activities.Count);

        var clientActivity = Assert.Single(activitySpy.Activities, a => a.Kind == ActivityKind.Client);
        var serverActivity = Assert.Single(activitySpy.Activities, a => a.Kind == ActivityKind.Server);

        Assert.Equal("laraue.grpc.tests.Greeter/SayHello", clientActivity.DisplayName);
        Assert.Equal("laraue.grpc.tests.Greeter/SayHello", serverActivity.DisplayName);

        // The client injects traceparent into gRPC metadata and the server picks it up as its
        // parent - if they share a trace ID, propagation across the wire actually worked.
        Assert.Equal(clientActivity.TraceId, serverActivity.TraceId);
        Assert.Equal(clientActivity.SpanId, serverActivity.ParentSpanId);

        Assert.Equal((int)StatusCode.OK, clientActivity.GetTagItem("rpc.grpc.status_code"));
        Assert.Equal((int)StatusCode.OK, serverActivity.GetTagItem("rpc.grpc.status_code"));
    }

    [Fact]
    public async Task SayHello_ShouldRecordDurationOnBothSides_Always()
    {
        using var metricSpy = new MetricSpy();

        var client = _host.CreateGreeterClient();
        await client.SayHelloAsync(new HelloRequest { Name = "World" });

        var clientDuration = Assert.Single(metricSpy.Measurements, m => m.InstrumentName == "rpc.client.duration");
        var serverDuration = Assert.Single(metricSpy.Measurements, m => m.InstrumentName == "rpc.server.duration");

        Assert.True(clientDuration.Value >= 0);
        Assert.True(serverDuration.Value >= 0);
        Assert.DoesNotContain(metricSpy.Measurements, m => m.InstrumentName == "rpc.calls.failed");
    }

    [Fact]
    public async Task Fail_ShouldMarkBothActivitiesAsErrorAndRecordFailureMetrics_WhenServiceThrowsRpcException()
    {
        using var activitySpy = new ActivitySpy();
        using var metricSpy = new MetricSpy();

        var client = _host.CreateGreeterClient();

        var thrown = await Assert.ThrowsAsync<RpcException>(
            () => client.FailAsync(new HelloRequest { Name = "World" }).ResponseAsync);

        Assert.Equal(StatusCode.NotFound, thrown.StatusCode);

        var clientActivity = Assert.Single(activitySpy.Activities, a => a.Kind == ActivityKind.Client);
        var serverActivity = Assert.Single(activitySpy.Activities, a => a.Kind == ActivityKind.Server);

        Assert.Equal(ActivityStatusCode.Error, clientActivity.Status);
        Assert.Equal(ActivityStatusCode.Error, serverActivity.Status);
        Assert.Equal((int)StatusCode.NotFound, clientActivity.GetTagItem("rpc.grpc.status_code"));
        Assert.Equal((int)StatusCode.NotFound, serverActivity.GetTagItem("rpc.grpc.status_code"));

        Assert.Equal(2, metricSpy.Measurements.Count(m => m.InstrumentName == "rpc.calls.failed"));
    }
}
