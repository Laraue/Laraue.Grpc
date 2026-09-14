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

    /// <summary>
    /// Registers <see cref="ExceptionTranslationInterceptor"/> globally for all gRPC services.
    /// Call this <b>after</b> <see cref="AddLaraueGrpcTelemetry"/> in the chain if using both -
    /// interceptors registered later run closer to the actual handler, so exception translation
    /// happening before telemetry observes the call means telemetry records the final, translated
    /// gRPC status rather than the raw exception.
    /// </summary>
    public static IGrpcServerBuilder AddLaraueGrpcExceptionHandling(this IGrpcServerBuilder builder)
    {
        builder.Services.Configure<GrpcServiceOptions>(options =>
            options.Interceptors.Add<ExceptionTranslationInterceptor>());

        return builder;
    }
}
