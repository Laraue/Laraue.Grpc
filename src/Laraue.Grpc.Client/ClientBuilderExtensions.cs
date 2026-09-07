using Grpc.Core;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.DependencyInjection;

namespace Laraue.Grpc.Client;

public static class ClientBuilderExtensions
{
    /// <summary>
    /// Registers a typed gRPC client the same way as <c>AddGrpcClient</c>, with
    /// <see cref="ClientTelemetryInterceptor"/> attached so every call is traced and measured.
    /// Traces are emitted on the <c>"Laraue.Grpc"</c> <see cref="System.Diagnostics.ActivitySource"/>
    /// and metrics on the matching <see cref="System.Diagnostics.Metrics.Meter"/> - subscribe with
    /// <c>AddSource</c>/<c>AddMeter</c> in the app's own OpenTelemetry setup.
    /// </summary>
    public static IHttpClientBuilder AddLaraueGrpcClient<TClient>(
        this IServiceCollection services,
        Action<GrpcClientFactoryOptions> configureClient)
        where TClient : ClientBase<TClient>
    {
        return services
            .AddGrpcClient<TClient>(configureClient)
            .AddInterceptor(() => new ClientTelemetryInterceptor());
    }
}
