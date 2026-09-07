using System.Diagnostics;
using Laraue.Grpc.Common.Telemetry;

namespace Laraue.Grpc.Common.UnitTests.Infrastructure;

/// <summary>Captures every <see cref="Activity"/> stopped on the <c>"Laraue.Grpc"</c> source while subscribed.</summary>
public sealed class ActivitySpy : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _activities = [];

    public IReadOnlyList<Activity> Activities => _activities;

    public ActivitySpy()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == LaraueGrpcTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _activities.Add,
        };

        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();
}
