using Grpc.Core;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.DependencyInjection;

namespace Laraue.Grpc.Client;

public static class ClientBuilderExtensions
{
    /// <summary>
    /// Registers a typed gRPC client the same way as <c>AddGrpcClient</c>, with
    /// <see cref="ClientTelemetryInterceptor"/> attached so every call is traced and measured, and
    /// <see cref="ExceptionTranslationInterceptor"/> attached so a call that fails with a
    /// <see cref="Grpc.Core.StatusCode"/> a server-side <c>Laraue.Grpc.Server.ExceptionTranslationInterceptor</c>
    /// would have produced from an <see cref="Laraue.Core.Exceptions.Web.HttpException"/> rethrows
    /// that same exception type here, instead of a raw <see cref="Grpc.Core.RpcException"/>.
    /// Traces are emitted on the <c>"Laraue.Grpc"</c> <see cref="System.Diagnostics.ActivitySource"/>
    /// and metrics on the matching <see cref="System.Diagnostics.Metrics.Meter"/> - subscribe with
    /// <c>AddSource</c>/<c>AddMeter</c> in the app's own OpenTelemetry setup.
    /// <see cref="ExceptionTranslationInterceptor"/> is registered *before*
    /// <see cref="ClientTelemetryInterceptor"/> on purpose - interceptors registered earlier sit
    /// farther from the actual call, so telemetry (registered later, closer to the transport) is
    /// the first to see and record the real <see cref="Grpc.Core.RpcException"/>/status code before
    /// exception translation replaces it with an <see cref="Laraue.Core.Exceptions.Web.HttpException"/>
    /// for the caller - the reverse of the concern the server-side ordering doc talks about, since
    /// there it's the final wire status (post-translation) that matters to record, not the
    /// pre-translation one.
    /// </summary>
    public static IHttpClientBuilder AddLaraueGrpcClient<TClient>(
        this IServiceCollection services,
        Action<GrpcClientFactoryOptions> configureClient)
        where TClient : ClientBase<TClient>
    {
        return services
            .AddGrpcClient<TClient>(configureClient)
            .AddInterceptor(() => new ExceptionTranslationInterceptor())
            .AddInterceptor(() => new ClientTelemetryInterceptor());
    }
}
