namespace Laraue.Grpc.Common.Telemetry;

/// <summary>
/// Tag names following the OpenTelemetry semantic conventions for gRPC
/// (https://opentelemetry.io/docs/specs/semconv/rpc/grpc/).
/// </summary>
public static class GrpcTags
{
    public const string RpcSystem = "rpc.system";
    public const string RpcService = "rpc.service";
    public const string RpcMethod = "rpc.method";
    public const string RpcGrpcStatusCode = "rpc.grpc.status_code";

    public const string RpcSystemValue = "grpc";
}
