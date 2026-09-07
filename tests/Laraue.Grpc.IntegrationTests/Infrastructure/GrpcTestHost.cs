using Laraue.Grpc.Client;
using Laraue.Grpc.IntegrationTests.DemoApp;
using Laraue.Grpc.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Laraue.Grpc.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the demo <see cref="GreeterService"/> on an in-memory <see cref="TestServer"/> and hands
/// back a real <see cref="Greeter.GreeterClient"/> wired through
/// <see cref="ClientBuilderExtensions.AddLaraueGrpcClient{TClient}"/>, so
/// <see cref="ServerTelemetryInterceptor"/> and <see cref="ClientTelemetryInterceptor"/> both run
/// for real against each other instead of being exercised in isolation.
/// </summary>
public sealed class GrpcTestHost : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly ServiceProvider _clientServices;

    private GrpcTestHost(IHost host, ServiceProvider clientServices)
    {
        _host = host;
        _clientServices = clientServices;
    }

    public static async Task<GrpcTestHost> StartAsync()
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services => services.AddGrpc().AddLaraueGrpcTelemetry());
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

        return new GrpcTestHost(host, clientServices.BuildServiceProvider());
    }

    public Greeter.GreeterClient CreateGreeterClient() => _clientServices.GetRequiredService<Greeter.GreeterClient>();

    public async ValueTask DisposeAsync()
    {
        _clientServices.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }
}
