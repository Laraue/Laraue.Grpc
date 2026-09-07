using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Laraue.Grpc.Common.Telemetry;

namespace Laraue.Grpc.Server;

/// <summary>
/// Wraps every incoming call in a server <see cref="Activity"/> and records
/// <see cref="LaraueGrpcTelemetry"/> duration/failure metrics for it.
/// Register via <see cref="ServerBuilderExtensions.AddLaraueGrpcTelemetry"/>.
/// </summary>
public sealed class ServerTelemetryInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        using var scope = StartServerScope(context);
        try
        {
            var response = await continuation(request, context);
            scope.Complete(context.Status);
            return response;
        }
        catch (Exception ex)
        {
            scope.Complete(ex);
            throw;
        }
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        using var scope = StartServerScope(context);
        try
        {
            await continuation(request, responseStream, context);
            scope.Complete(context.Status);
        }
        catch (Exception ex)
        {
            scope.Complete(ex);
            throw;
        }
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        using var scope = StartServerScope(context);
        try
        {
            var response = await continuation(requestStream, context);
            scope.Complete(context.Status);
            return response;
        }
        catch (Exception ex)
        {
            scope.Complete(ex);
            throw;
        }
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        using var scope = StartServerScope(context);
        try
        {
            await continuation(requestStream, responseStream, context);
            scope.Complete(context.Status);
        }
        catch (Exception ex)
        {
            scope.Complete(ex);
            throw;
        }
    }

    /// <summary>Extracts the caller's trace context from the incoming metadata so the server span
    /// continues the same trace instead of starting a new root - see
    /// <see cref="GrpcTraceContextPropagator"/> for why this can't be left to ambient
    /// <see cref="Activity.Current"/>/ASP.NET Core hosting instrumentation.</summary>
    private static CallScope StartServerScope(ServerCallContext context)
    {
        var parentContext = GrpcTraceContextPropagator.Extract(context.RequestHeaders);
        return CallScope.Start(context.Method, ActivityKind.Server, parentContext);
    }
}
