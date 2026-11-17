using Grpc.Net.Client;
using Laraue.Grpc.Client;
using Laraue.Grpc.IntegrationTests.DemoApp;
using Laraue.Grpc.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Laraue.Grpc.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the demo <see cref="GreeterService"/> on an in-memory <see cref="TestServer"/> and hands
/// back a real <see cref="Greeter.GreeterClient"/> wired through
/// <see cref="ClientBuilderExtensions.AddLaraueGrpcClient{TClient}"/>, so
/// <see cref="ServerTelemetryInterceptor"/>/<see cref="ClientTelemetryInterceptor"/> and both sides'
/// <c>ExceptionTranslationInterceptor</c> all run for real against each other instead of being
/// exercised in isolation.
/// </summary>
public sealed class GrpcTestHost : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly ServiceProvider _clientServices;
    private readonly GrpcChannel _rawChannel;

    /// <summary>Everything the server host logged, for tests that check what a call was logged as.</summary>
    public LogSpy Logs { get; }

    private GrpcTestHost(IHost host, ServiceProvider clientServices, GrpcChannel rawChannel, LogSpy logs)
    {
        Logs = logs;
        _host = host;
        _clientServices = clientServices;
        _rawChannel = rawChannel;
    }

    public static async Task<GrpcTestHost> StartAsync()
    {
        var logs = new LogSpy();
        var hostBuilder = new HostBuilder()
            .ConfigureLogging(logging => logging.AddProvider(logs))
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services => services
                    .AddGrpc()
                    .AddLaraueGrpcTelemetry()
                    .AddLaraueGrpcExceptionHandling());
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapGrpcService<GreeterService>());
                });
            });

        var host = await hostBuilder.StartAsync();
        var testServer = host.GetTestServer();

        var clientServices = new ServiceCollection();
        clientServices.AddLaraueGrpcClient<Greeter.GreeterClient>(options =>
        {
            options.Address = new Uri("http://localhost");
            options.ChannelOptionsActions.Add(channelOptions =>
                channelOptions.HttpHandler = testServer.CreateHandler());
        });

        var rawChannel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = testServer.CreateHandler(),
        });

        return new GrpcTestHost(host, clientServices.BuildServiceProvider(), rawChannel, logs);
    }

    public Greeter.GreeterClient CreateGreeterClient() => _clientServices.GetRequiredService<Greeter.GreeterClient>();

    /// <summary>
    /// A plain <see cref="Greeter.GreeterClient"/> with none of <c>Laraue.Grpc.Client</c>'s
    /// interceptors attached - for tests that need to observe the raw <see cref="Grpc.Core.RpcException"/>/
    /// status code a server produces, independent of the client-side
    /// <c>ExceptionTranslationInterceptor</c> that <see cref="CreateGreeterClient"/>'s client would
    /// otherwise apply on top.
    /// </summary>
    public Greeter.GreeterClient CreateRawGreeterClient() => new(_rawChannel);

    public async ValueTask DisposeAsync()
    {
        _rawChannel.Dispose();
        _clientServices.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }
}
