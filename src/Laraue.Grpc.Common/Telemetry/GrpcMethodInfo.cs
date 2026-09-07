namespace Laraue.Grpc.Common.Telemetry;

/// <summary>
/// The service and method names parsed out of a gRPC full method string,
/// e.g. <c>/orders.v1.OrdersService/GetOrder</c>.
/// </summary>
public readonly record struct GrpcMethodInfo(string ServiceName, string MethodName)
{
    public static GrpcMethodInfo Parse(string fullMethodName)
    {
        var trimmed = fullMethodName.AsSpan().TrimStart('/');
        var separatorIndex = trimmed.IndexOf('/');

        return separatorIndex < 0
            ? new GrpcMethodInfo(ServiceName: string.Empty, MethodName: trimmed.ToString())
            : new GrpcMethodInfo(
                ServiceName: trimmed[..separatorIndex].ToString(),
                MethodName: trimmed[(separatorIndex + 1)..].ToString());
    }

    /// <summary>Activity/display name in the conventional <c>Service/Method</c> shape.</summary>
    public string DisplayName => $"{ServiceName}/{MethodName}";
}
