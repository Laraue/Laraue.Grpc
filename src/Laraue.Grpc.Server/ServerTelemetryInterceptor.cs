using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Laraue.Grpc.Common.Telemetry;

namespace Laraue.Grpc.Server;

/// <summary>
/// Wraps every incoming call in a server <see cref="Activity"/> and records
/// <see cref="LaraueGrpcTelemetry"/> duration/failure metrics for it.
/// A call the client cancelled (or whose deadline passed) ends with <see cref="StatusCode.Cancelled"/>:
/// the <see cref="OperationCanceledException"/> the handler throws from <see cref="ServerCallContext.CancellationToken"/>
/// is turned into an <see cref="RpcException"/>, which ASP.NET Core gRPC logs at Information instead of
/// "Error when executing service method".
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
        catch (OperationCanceledException ex) when (context.CancellationToken.IsCancellationRequested)
        {
            throw Cancelled(scope, ex);
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
        catch (OperationCanceledException ex) when (context.CancellationToken.IsCancellationRequested)
        {
            throw Cancelled(scope, ex);
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
        catch (OperationCanceledException ex) when (context.CancellationToken.IsCancellationRequested)
        {
            throw Cancelled(scope, ex);
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
        catch (OperationCanceledException ex) when (context.CancellationToken.IsCancellationRequested)
        {
            throw Cancelled(scope, ex);
        }
        catch (Exception ex)
        {
            scope.Complete(ex);
            throw;
        }
    }

    /// <summary>Only called when <see cref="ServerCallContext.CancellationToken"/> is cancelled, i.e. the client went
    /// away or the deadline passed - an <see cref="OperationCanceledException"/> from any other token is the
    /// service's own failure and is left as it is.</summary>
    private static RpcException Cancelled(CallScope scope, OperationCanceledException exception)
    {
        var rpcException = new RpcException(new Status(StatusCode.Cancelled, "The call was cancelled.", exception));
        scope.Complete(rpcException);
        return rpcException;
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
