using Grpc.Core;
using Laraue.Grpc.IntegrationTests.DemoApp;
using Laraue.Grpc.IntegrationTests.Infrastructure;

namespace Laraue.Grpc.IntegrationTests;

/// <summary>
/// End-to-end tests: a real <see cref="Greeter.GreeterClient"/> talks to a real
/// <see cref="GreeterService"/> over an in-memory <see cref="Microsoft.AspNetCore.TestHost.TestServer"/>,
/// proving <c>Laraue.Grpc.Server</c>'s <c>ExceptionTranslationInterceptor</c> actually translates an
/// <c>HttpException</c> thrown by a handler into the matching <see cref="RpcException"/>, rather than
/// just unit-testing the mapping logic in isolation. Shares the "GrpcIntegrationTests" collection
/// with <see cref="GrpcTelemetryTests"/> so the two never run concurrently - see that class's doc
/// for why (both rely on the same in-memory <c>TestServer</c> setup, and telemetry's spies listen
/// process-wide).
/// </summary>
[Collection("GrpcIntegrationTests")]
public class ExceptionTranslationInterceptorTests : IAsyncLifetime
{
    private GrpcTestHost _host = null!;

    public async Task InitializeAsync() => _host = await GrpcTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Theory]
    [InlineData("BadRequest", StatusCode.InvalidArgument)]
    [InlineData("Unauthorized", StatusCode.Unauthenticated)]
    [InlineData("Forbidden", StatusCode.PermissionDenied)]
    [InlineData("NotFound", StatusCode.NotFound)]
    [InlineData("NotAcceptable", StatusCode.Unknown)]
    public async Task FailWithHttpException_ShouldTranslateToMatchingRpcStatusCode_WhenHandlerThrowsHttpException(
        string exceptionName,
        StatusCode expectedStatusCode)
    {
        var client = _host.CreateGreeterClient();

        var thrown = await Assert.ThrowsAsync<RpcException>(
            () => client.FailWithHttpExceptionAsync(new HelloRequest { Name = exceptionName }).ResponseAsync);

        Assert.Equal(expectedStatusCode, thrown.StatusCode);
    }
}
