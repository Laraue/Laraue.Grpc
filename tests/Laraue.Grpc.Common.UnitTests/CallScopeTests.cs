using System.Diagnostics;
using Grpc.Core;
using Laraue.Grpc.Common.Telemetry;
using Laraue.Grpc.Common.UnitTests.Infrastructure;

namespace Laraue.Grpc.Common.UnitTests;

public class CallScopeTests
{
    [Fact]
    public void Start_ShouldTagActivityAndIncrementCallsStarted_Always()
    {
        using var activitySpy = new ActivitySpy();
        using var metricSpy = new MetricSpy();

        using (var scope = CallScope.Start("/orders.v1.OrdersService/GetOrder", ActivityKind.Server))
        {
            scope.Complete(new Status(StatusCode.OK, string.Empty));
        }

        var activity = Assert.Single(activitySpy.Activities);
        Assert.Equal("orders.v1.OrdersService/GetOrder", activity.DisplayName);
        Assert.Equal(ActivityKind.Server, activity.Kind);
        Assert.Equal(GrpcTags.RpcSystemValue, activity.GetTagItem(GrpcTags.RpcSystem));
        Assert.Equal("orders.v1.OrdersService", activity.GetTagItem(GrpcTags.RpcService));
        Assert.Equal("GetOrder", activity.GetTagItem(GrpcTags.RpcMethod));

        Assert.Contains(metricSpy.Measurements, m => m.InstrumentName == "rpc.calls.started");
    }

    [Fact]
    public void Complete_ShouldNotMarkActivityAsError_WhenStatusIsOk()
    {
        using var activitySpy = new ActivitySpy();

        using (var scope = CallScope.Start("/svc/Method", ActivityKind.Client))
        {
            scope.Complete(new Status(StatusCode.OK, string.Empty));
        }

        var activity = Assert.Single(activitySpy.Activities);
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Equal((int)StatusCode.OK, activity.GetTagItem(GrpcTags.RpcGrpcStatusCode));
    }

    [Fact]
    public void Complete_ShouldMarkActivityAsErrorAndRecordFailureMetric_WhenStatusIsNotOk()
    {
        using var activitySpy = new ActivitySpy();
        using var metricSpy = new MetricSpy();

        using (var scope = CallScope.Start("/svc/Method", ActivityKind.Server))
        {
            scope.Complete(new Status(StatusCode.NotFound, "not found"));
        }

        var activity = Assert.Single(activitySpy.Activities);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal((int)StatusCode.NotFound, activity.GetTagItem(GrpcTags.RpcGrpcStatusCode));

        Assert.Contains(metricSpy.Measurements, m => m.InstrumentName == "rpc.calls.failed");
    }

    [Fact]
    public void Complete_ShouldUseRpcExceptionStatusCode_WhenExceptionIsRpcException()
    {
        using var activitySpy = new ActivitySpy();

        using (var scope = CallScope.Start("/svc/Method", ActivityKind.Client))
        {
            scope.Complete(new RpcException(new Status(StatusCode.Unavailable, "down")));
        }

        var activity = Assert.Single(activitySpy.Activities);
        Assert.Equal((int)StatusCode.Unavailable, activity.GetTagItem(GrpcTags.RpcGrpcStatusCode));
    }

    [Fact]
    public void Complete_ShouldMapToUnknownStatusCode_WhenExceptionIsNotRpcException()
    {
        using var activitySpy = new ActivitySpy();

        using (var scope = CallScope.Start("/svc/Method", ActivityKind.Client))
        {
            scope.Complete(new InvalidOperationException("boom"));
        }

        var activity = Assert.Single(activitySpy.Activities);
        Assert.Equal((int)StatusCode.Unknown, activity.GetTagItem(GrpcTags.RpcGrpcStatusCode));
    }

    [Fact]
    public void Complete_ShouldRecordNonNegativeServerDurationMetric_WhenKindIsServer()
    {
        using var metricSpy = new MetricSpy();

        using (var scope = CallScope.Start("/svc/Method", ActivityKind.Server))
        {
            scope.Complete(new Status(StatusCode.OK, string.Empty));
        }

        var duration = Assert.Single(metricSpy.Measurements, m => m.InstrumentName == "rpc.server.duration");
        Assert.True(duration.Value >= 0);
        Assert.DoesNotContain(metricSpy.Measurements, m => m.InstrumentName == "rpc.client.duration");
    }

    [Fact]
    public void Complete_ShouldRecordNonNegativeClientDurationMetric_WhenKindIsClient()
    {
        using var metricSpy = new MetricSpy();

        using (var scope = CallScope.Start("/svc/Method", ActivityKind.Client))
        {
            scope.Complete(new Status(StatusCode.OK, string.Empty));
        }

        var duration = Assert.Single(metricSpy.Measurements, m => m.InstrumentName == "rpc.client.duration");
        Assert.True(duration.Value >= 0);
        Assert.DoesNotContain(metricSpy.Measurements, m => m.InstrumentName == "rpc.server.duration");
    }
}
