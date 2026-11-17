using System.Diagnostics;
using Grpc.Core;
using Laraue.Grpc.IntegrationTests.DemoApp;
using Laraue.Grpc.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Laraue.Grpc.IntegrationTests;

/// <summary>
/// A client that cancels a call (an HTTP request it was made for was aborted) is not the server's failure:
/// the server should end the call with <see cref="StatusCode.Cancelled"/> and not log an Error for it (BRD-371).
/// </summary>
[Collection("GrpcIntegrationTests")]
public class CancellationTests : IAsyncLifetime
{
    private GrpcTestHost _host = null!;

    public async Task InitializeAsync() => _host = await GrpcTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task WaitForCancellation_ShouldEndServerCallAsCancelledWithoutErrorLog_WhenClientCancels()
    {
        using var activitySpy = new ActivitySpy();
        using var cts = new CancellationTokenSource();

        var client = _host.CreateRawGreeterClient();
        var call = client.WaitForCancellationAsync(new HelloRequest { Name = "World" }, cancellationToken: cts.Token);

        await Task.Delay(TimeSpan.FromMilliseconds(200));
        await cts.CancelAsync();

        var thrown = await Assert.ThrowsAsync<RpcException>(() => call.ResponseAsync);
        Assert.Equal(StatusCode.Cancelled, thrown.StatusCode);

        // The server side finishes on its own after the client has already given up.
        var serverActivity = await WaitForServerActivity(activitySpy);

        Assert.Equal((int)StatusCode.Cancelled, serverActivity.GetTagItem("rpc.grpc.status_code"));
        Assert.DoesNotContain(_host.Logs.Entries, e => e.Level >= LogLevel.Error);
    }

    private static async Task<Activity> WaitForServerActivity(ActivitySpy activitySpy)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            var activity = activitySpy.Activities.ToArray().SingleOrDefault(a => a.Kind == ActivityKind.Server);
            if (activity is not null)
            {
                // Give the gRPC host time to log the call's end after the interceptor returned.
                await Task.Delay(TimeSpan.FromMilliseconds(200));
                return activity;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20));
        }

        throw new TimeoutException("The server call didn't end.");
    }
}
