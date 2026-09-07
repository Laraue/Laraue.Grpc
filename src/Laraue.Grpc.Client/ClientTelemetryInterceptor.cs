using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Laraue.Grpc.Common.Telemetry;

namespace Laraue.Grpc.Client;

/// <summary>
/// Wraps every outgoing call in a client <see cref="Activity"/>, injects W3C trace context
/// (<c>traceparent</c>/<c>tracestate</c>) into the outgoing gRPC metadata so the server side
/// can continue the same trace, and records <see cref="LaraueGrpcTelemetry"/> duration/failure
/// metrics. Register via <see cref="ClientBuilderExtensions.AddLaraueGrpcClient{TClient}"/>.
/// </summary>
public sealed class ClientTelemetryInterceptor : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var scope = CallScope.Start(context.Method.FullName, ActivityKind.Client);
        var call = continuation(request, InjectTraceContext(context, scope.Activity));

        return new AsyncUnaryCall<TResponse>(
            AwaitAndCompleteResponse(call.ResponseAsync, call.GetStatus, scope),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var scope = CallScope.Start(context.Method.FullName, ActivityKind.Client);
        var call = continuation(InjectTraceContext(context, scope.Activity));

        return new AsyncClientStreamingCall<TRequest, TResponse>(
            call.RequestStream,
            AwaitAndCompleteResponse(call.ResponseAsync, call.GetStatus, scope),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var scope = CallScope.Start(context.Method.FullName, ActivityKind.Client);
        var call = continuation(request, InjectTraceContext(context, scope.Activity));

        return new AsyncServerStreamingCall<TResponse>(
            call.ResponseStream,
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            CompleteOnDispose(call, scope));
    }

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var scope = CallScope.Start(context.Method.FullName, ActivityKind.Client);
        var call = continuation(InjectTraceContext(context, scope.Activity));

        return new AsyncDuplexStreamingCall<TRequest, TResponse>(
            call.RequestStream,
            call.ResponseStream,
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            CompleteOnDispose(call, scope));
    }

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var scope = CallScope.Start(context.Method.FullName, ActivityKind.Client);
        try
        {
            var response = continuation(request, InjectTraceContext(context, scope.Activity));
            scope.Complete(new Status(StatusCode.OK, string.Empty));
            return response;
        }
        catch (Exception ex)
        {
            scope.Complete(ex);
            throw;
        }
        finally
        {
            scope.Dispose();
        }
    }

    /// <summary>Unary/client-streaming calls complete as soon as the response is awaited, so the
    /// scope is finished there and the caller's Dispose is left untouched (call.Dispose).</summary>
    private static async Task<TResponse> AwaitAndCompleteResponse<TResponse>(
        Task<TResponse> responseTask,
        Func<Status> getStatus,
        CallScope scope)
    {
        try
        {
            var response = await responseTask.ConfigureAwait(false);
            scope.Complete(getStatus());
            return response;
        }
        catch (Exception ex)
        {
            scope.Complete(ex);
            throw;
        }
        finally
        {
            scope.Dispose();
        }
    }

    /// <summary>Server-streaming/duplex calls have no single "response" task - completion is only
    /// observable when the caller disposes the call after finishing with the stream.</summary>
    private static Action CompleteOnDispose<TResponse>(AsyncServerStreamingCall<TResponse> call, CallScope scope) =>
        () => CompleteOnDispose(call.GetStatus, call.Dispose, scope);

    private static Action CompleteOnDispose<TRequest, TResponse>(AsyncDuplexStreamingCall<TRequest, TResponse> call, CallScope scope) =>
        () => CompleteOnDispose(call.GetStatus, call.Dispose, scope);

    private static void CompleteOnDispose(Func<Status> getStatus, Action dispose, CallScope scope)
    {
        try
        {
            scope.Complete(getStatus());
        }
        catch (InvalidOperationException)
        {
            // The call was disposed before it completed (e.g. the caller stopped reading early).
        }
        finally
        {
            scope.Dispose();
            dispose();
        }
    }

    private static ClientInterceptorContext<TRequest, TResponse> InjectTraceContext<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        Activity? activity)
        where TRequest : class
        where TResponse : class
    {
        var headers = context.Options.Headers ?? new Metadata();

        GrpcTraceContextPropagator.Inject(activity, headers);

        return new ClientInterceptorContext<TRequest, TResponse>(
            context.Method,
            context.Host,
            context.Options.WithHeaders(headers));
    }
}
