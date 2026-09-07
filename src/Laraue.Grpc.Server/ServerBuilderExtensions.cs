using Grpc.AspNetCore.Server;
using Microsoft.Extensions.DependencyInjection;

namespace Laraue.Grpc.Server;

public static class ServerBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="ServerTelemetryInterceptor"/> globally for all gRPC services.
    /// Traces are emitted on the <c>"Laraue.Grpc"</c> <see cref="System.Diagnostics.ActivitySource"/>
    /// and metrics on the matching <see cref="System.Diagnostics.Metrics.Meter"/> - subscribe with
    /// <c>AddSource</c>/<c>AddMeter</c> in the app's own OpenTelemetry setup.
    /// </summary>
    public static IGrpcServerBuilder AddLaraueGrpcTelemetry(this IGrpcServerBuilder builder)
    {
        builder.Services.Configure<GrpcServiceOptions>(options =>
            options.Interceptors.Add<ServerTelemetryInterceptor>());

        return builder;
    }
}
