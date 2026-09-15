using Grpc.Core;
using Grpc.Core.Interceptors;
using Laraue.Core.Exceptions.Web;

namespace Laraue.Grpc.Client;

/// <summary>
/// Catches the <see cref="RpcException"/> a call raises and, for the same subset of
/// <see cref="StatusCode"/>s <c>Laraue.Grpc.Server.ExceptionTranslationInterceptor</c> produces
/// from an <see cref="HttpException"/> server-side, rethrows the matching <see cref="HttpException"/>
/// subtype here too - so a caller sharing exception-handling logic with a REST client (or an
/// <c>ExceptionHandleMiddleware</c>-based host) doesn't need to special-case a raw
/// <see cref="RpcException"/> for the common cases. Any other status code (including
/// <see cref="StatusCode.Unknown"/>, and anything a service throws directly rather than via an
/// <see cref="HttpException"/> - e.g. <see cref="StatusCode.FailedPrecondition"/>) passes through
/// unchanged - a caller that needs to handle those still catches <see cref="RpcException"/> itself.
/// Attached automatically by <see cref="ClientBuilderExtensions.AddLaraueGrpcClient{TClient}"/>.
/// </summary>
public sealed class ExceptionTranslationInterceptor : Interceptor
{
    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        try
        {
            return continuation(request, context);
        }
        catch (RpcException ex) when (ToHttpException(ex) is { } httpException)
        {
            throw httpException;
        }
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var call = continuation(request, context);

        return new AsyncUnaryCall<TResponse>(
            TranslateAsync(call.ResponseAsync),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    private static async Task<TResponse> TranslateAsync<TResponse>(Task<TResponse> responseTask)
    {
        try
        {
            return await responseTask;
        }
        catch (RpcException ex) when (ToHttpException(ex) is { } httpException)
        {
            throw httpException;
        }
    }

    /// <summary>
    /// The reverse of <c>Laraue.Grpc.Server.ExceptionTranslationInterceptor.ToStatusCode</c> -
    /// only the subset of codes that mapping actually produces round-trips back to an
    /// <see cref="HttpException"/>; anything else returns <see langword="null"/> so the original
    /// <see cref="RpcException"/> keeps propagating.
    /// </summary>
    private static HttpException? ToHttpException(RpcException ex) => ex.StatusCode switch
    {
        StatusCode.InvalidArgument => new BadRequestException(string.Empty, ex.Status.Detail),
        StatusCode.Unauthenticated => new UnauthorizedException(ex.Status.Detail),
        StatusCode.PermissionDenied => new ForbiddenException(ex.Status.Detail),
        StatusCode.NotFound => new NotFoundException(ex.Status.Detail),
        _ => null,
    };
}
