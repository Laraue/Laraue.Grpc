using Grpc.Core;
using Laraue.Core.Exceptions.Web;

namespace Laraue.Grpc.IntegrationTests.DemoApp;

/// <summary>Minimal service implementation - just enough behavior to prove the telemetry and
/// exception-translation interceptors work: a call that succeeds, one that throws a raw
/// <see cref="RpcException"/> directly, and one that throws an <see cref="HttpException"/> for
/// <see cref="ExceptionTranslationInterceptorTests"/> to translate.</summary>
public sealed class GreeterService : Greeter.GreeterBase
{
    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        return Task.FromResult(new HelloReply { Message = $"Hello {request.Name}" });
    }

    public override Task<HelloReply> Fail(HelloRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.NotFound, $"{request.Name} not found"));
    }

    /// <summary>Throws whichever <see cref="HttpException"/> subtype <paramref name="request"/>'s
    /// name names, so one rpc can exercise every mapping
    /// <see cref="Laraue.Grpc.Server.ExceptionTranslationInterceptor"/> knows about.</summary>
    public override Task<HelloReply> FailWithHttpException(HelloRequest request, ServerCallContext context)
    {
        throw request.Name switch
        {
            "BadRequest" => new BadRequestException(nameof(request.Name), "bad request"),
            "Unauthorized" => new UnauthorizedException("unauthorized"),
            "Forbidden" => new ForbiddenException("forbidden"),
            "NotFound" => new NotFoundException("not found"),
            "NotAcceptable" => new NotAcceptableException("not acceptable"),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Name, "Unknown exception name."),
        };
    }

    /// <summary>Never answers: waits on the call's token the way a database query does, so the call ends only when
    /// the client cancels it.</summary>
    public override async Task<HelloReply> WaitForCancellation(HelloRequest request, ServerCallContext context)
    {
        await Task.Delay(Timeout.Infinite, context.CancellationToken);
        throw new InvalidOperationException("Unreachable.");
    }
}
