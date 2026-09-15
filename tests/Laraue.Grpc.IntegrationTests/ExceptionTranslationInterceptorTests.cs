using Grpc.Core;
using Laraue.Core.Exceptions.Web;
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

    /// <summary>
    /// Uses <see cref="GrpcTestHost.CreateRawGreeterClient"/> (no client-side interceptors) so this
    /// observes the status code the server actually put on the wire, independent of
    /// <c>Laraue.Grpc.Client</c>'s own <c>ExceptionTranslationInterceptor</c> reversing it again -
    /// that reversal is covered separately below.
    /// </summary>
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
        var client = _host.CreateRawGreeterClient();

        var thrown = await Assert.ThrowsAsync<RpcException>(
            () => client.FailWithHttpExceptionAsync(new HelloRequest { Name = exceptionName }).ResponseAsync);

        Assert.Equal(expectedStatusCode, thrown.StatusCode);
    }

    /// <summary>
    /// The client-side counterpart of the test above: <c>Laraue.Grpc.Client</c>'s own
    /// <see cref="Client.ExceptionTranslationInterceptor"/> (attached automatically by
    /// <see cref="Client.ClientBuilderExtensions.AddLaraueGrpcClient{TClient}"/>, which
    /// <see cref="Infrastructure.GrpcTestHost.CreateGreeterClient"/> uses) should reverse the
    /// mapping above and hand the caller back the original <see cref="HttpException"/> subtype
    /// instead of a raw <see cref="RpcException"/>, for the same status codes the server-side
    /// interceptor produces from one.
    /// </summary>
    [Theory]
    [InlineData("BadRequest", typeof(BadRequestException))]
    [InlineData("Unauthorized", typeof(UnauthorizedException))]
    [InlineData("Forbidden", typeof(ForbiddenException))]
    [InlineData("NotFound", typeof(NotFoundException))]
    public async Task FailWithHttpException_ShouldTranslateBackToHttpException_OnTheClient(
        string exceptionName,
        Type expectedExceptionType)
    {
        var client = _host.CreateGreeterClient();

        var thrown = await Record.ExceptionAsync(
            () => client.FailWithHttpExceptionAsync(new HelloRequest { Name = exceptionName }).ResponseAsync);

        Assert.IsType(expectedExceptionType, thrown);
    }

    /// <summary>
    /// <see cref="StatusCode.Unknown"/> (what the server maps an <see cref="NotAcceptableException"/>
    /// to, since it has no dedicated status code) has no reverse mapping either - the client still
    /// sees a raw <see cref="RpcException"/>, same as any status a service throws directly rather
    /// than via an <see cref="HttpException"/> (e.g. <see cref="StatusCode.FailedPrecondition"/>).
    /// </summary>
    [Fact]
    public async Task FailWithHttpException_ShouldLeaveRpcExceptionUntranslated_WhenStatusCodeHasNoMapping()
    {
        var client = _host.CreateGreeterClient();

        await Assert.ThrowsAsync<RpcException>(
            () => client.FailWithHttpExceptionAsync(new HelloRequest { Name = "NotAcceptable" }).ResponseAsync);
    }
}
