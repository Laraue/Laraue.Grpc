using System.Diagnostics;
using Grpc.Core;

namespace Laraue.Grpc.Common.Telemetry;

/// <summary>
/// Tracks a single gRPC call from start to completion: opens an <see cref="Activity"/>,
/// tags it per <see cref="GrpcTags"/>, and records <see cref="LaraueGrpcTelemetry"/>
/// duration/failure metrics on <see cref="Complete(Status)"/>/<see cref="Complete(Exception)"/>.
/// Shared by the server and client interceptors so both sides tag/measure calls identically.
/// </summary>
public readonly struct CallScope : IDisposable
{
    private readonly Activity? _activity;
    private readonly GrpcMethodInfo _method;
    private readonly ActivityKind _kind;
    private readonly long _startTimestamp;

    /// <summary>The activity started for this call, if a listener is subscribed. Used by the client
    /// interceptor to inject W3C trace context into outgoing gRPC metadata.</summary>
    public Activity? Activity => _activity;

    private CallScope(Activity? activity, GrpcMethodInfo method, ActivityKind kind, long startTimestamp)
    {
        _activity = activity;
        _method = method;
        _kind = kind;
        _startTimestamp = startTimestamp;
    }

    /// <param name="kind">Only <see cref="ActivityKind.Server"/> and <see cref="ActivityKind.Client"/> are
    /// meaningful - it picks <see cref="LaraueGrpcTelemetry.ServerCallDuration"/> vs.
    /// <see cref="LaraueGrpcTelemetry.ClientCallDuration"/> on <see cref="Complete(Status)"/>.</param>
    /// <param name="parentContext">The parent extracted from incoming gRPC metadata via
    /// <see cref="GrpcTraceContextPropagator.Extract"/> (server side). Left <see langword="default"/>
    /// on the client side, where the ambient <see cref="Activity.Current"/> is the correct parent.</param>
    public static CallScope Start(string fullMethodName, ActivityKind kind, ActivityContext parentContext = default)
    {
        var method = GrpcMethodInfo.Parse(fullMethodName);

        var activity = parentContext == default
            ? LaraueGrpcTelemetry.ActivitySource.StartActivity(method.DisplayName, kind)
            : LaraueGrpcTelemetry.ActivitySource.StartActivity(method.DisplayName, kind, parentContext);

        activity?.SetTag(GrpcTags.RpcSystem, GrpcTags.RpcSystemValue);
        activity?.SetTag(GrpcTags.RpcService, method.ServiceName);
        activity?.SetTag(GrpcTags.RpcMethod, method.MethodName);

        LaraueGrpcTelemetry.CallsStarted.Add(1, BaseTags(method));

        return new CallScope(activity, method, kind, Stopwatch.GetTimestamp());
    }

    public void Complete(Status status) => Complete((int)status.StatusCode, status.Detail);

    public void Complete(Exception exception)
    {
        var statusCode = exception is RpcException rpcException
            ? (int)rpcException.StatusCode
            : (int)StatusCode.Unknown;

        Complete(statusCode, exception.Message);
    }

    private void Complete(int statusCode, string? errorDetail)
    {
        _activity?.SetTag(GrpcTags.RpcGrpcStatusCode, statusCode);

        if (statusCode != (int)StatusCode.OK)
        {
            _activity?.SetStatus(ActivityStatusCode.Error, errorDetail);
            LaraueGrpcTelemetry.CallsFailed.Add(1, BaseTags(_method, statusCode));
        }

        var elapsedMs = Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds;
        var durationHistogram = _kind == ActivityKind.Server
            ? LaraueGrpcTelemetry.ServerCallDuration
            : LaraueGrpcTelemetry.ClientCallDuration;

        durationHistogram.Record(elapsedMs, BaseTags(_method, statusCode));
    }

    private static TagList BaseTags(GrpcMethodInfo method, int? statusCode = null)
    {
        var tags = new TagList
        {
            { GrpcTags.RpcSystem, GrpcTags.RpcSystemValue },
            { GrpcTags.RpcService, method.ServiceName },
            { GrpcTags.RpcMethod, method.MethodName },
        };

        if (statusCode is not null)
        {
            tags.Add(GrpcTags.RpcGrpcStatusCode, statusCode);
        }

        return tags;
    }

    public void Dispose() => _activity?.Dispose();
}
