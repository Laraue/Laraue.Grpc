using System.Net;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Laraue.Core.Exceptions.Web;

namespace Laraue.Grpc.Server;

/// <summary>
/// Catches any <see cref="HttpException"/> thrown by a gRPC method and translates it into the
/// matching <see cref="RpcException"/>, picking the gRPC <see cref="StatusCode"/> from
/// <see cref="HttpException.StatusCode"/> - so business logic shared with a REST host (which would
/// otherwise rely on <c>Laraue.Core.Exceptions.ExceptionHandleMiddleware</c>) can throw the same
/// exceptions here too, instead of every gRPC method hand-rolling its own try/catch translation.
/// Register via <see cref="ServerBuilderExtensions.AddLaraueGrpcExceptionHandling"/> - see that
/// method's doc for why registration order relative to <see cref="ServerTelemetryInterceptor"/>
/// matters.
/// </summary>
public sealed class ExceptionTranslationInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (HttpException ex)
        {
            throw ToRpcException(ex);
        }
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            await continuation(request, responseStream, context);
        }
        catch (HttpException ex)
        {
            throw ToRpcException(ex);
        }
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(requestStream, context);
        }
        catch (HttpException ex)
        {
            throw ToRpcException(ex);
        }
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            await continuation(requestStream, responseStream, context);
        }
        catch (HttpException ex)
        {
            throw ToRpcException(ex);
        }
    }

    private static RpcException ToRpcException(HttpException ex) =>
        new(new Status(ToStatusCode(ex.StatusCode), ex.Message));

    /// <summary>
    /// The canonical HTTP-status-to-gRPC-status mapping for the subset of <see cref="HttpStatusCode"/>
    /// values <see cref="HttpException"/>'s subtypes actually use. Anything else falls back to
    /// <see cref="StatusCode.Unknown"/> rather than guessing further.
    /// </summary>
    private static StatusCode ToStatusCode(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest => StatusCode.InvalidArgument,
        HttpStatusCode.Unauthorized => StatusCode.Unauthenticated,
        HttpStatusCode.Forbidden => StatusCode.PermissionDenied,
        HttpStatusCode.NotFound => StatusCode.NotFound,
        _ => StatusCode.Unknown,
    };
}
