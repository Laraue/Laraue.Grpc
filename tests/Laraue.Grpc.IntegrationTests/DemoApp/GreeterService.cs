using Grpc.Core;

namespace Laraue.Grpc.IntegrationTests.DemoApp;

/// <summary>Minimal service implementation - just enough behavior to prove the telemetry
/// interceptors work: a call that succeeds and one that always fails.</summary>
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
}
