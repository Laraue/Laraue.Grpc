# Laraue.Grpc

Shared plumbing for making gRPC calls between apps, with metrics and distributed
tracing available out of the box on both the server and client side.

| Package                    | Status      |
|-----------------------------|-------------|
| Laraue.Grpc.Common          | done        |
| Laraue.Grpc.Server          | done        |
| Laraue.Grpc.Client          | done        |
| Laraue.Grpc.OpenTelemetry   | done        |

## Get started

### 1. Install the packages

In the app that **hosts** a gRPC service:

```bash
dotnet add package Laraue.Grpc.Server
```

In the app that **calls** one:

```bash
dotnet add package Laraue.Grpc.Client
```

In whichever app(s) should actually export the traces/metrics (usually both):

```bash
dotnet add package Laraue.Grpc.OpenTelemetry
```

### 2. Define your contract

Contracts are **not** shipped by this library (see [Architecture](#architecture) below) - add your
own `.proto` to the server project, e.g. `Protos/orders.proto`:

```proto
syntax = "proto3";
option csharp_namespace = "Orders.Contracts";
package orders.v1;

service OrdersService {
  rpc GetOrder (GetOrderRequest) returns (GetOrderReply);
}

message GetOrderRequest { string order_id = 1; }
message GetOrderReply { string order_id = 1; string status = 2; }
```

Generate the server stub in the server project:

```xml
<ItemGroup>
  <PackageReference Include="Grpc.AspNetCore.Server" Version="2.66.0" />
  <PackageReference Include="Grpc.Tools" Version="2.66.0" PrivateAssets="All" />
</ItemGroup>
<ItemGroup>
  <Protobuf Include="Protos/orders.proto" GrpcServices="Server" />
</ItemGroup>
```

...and the client stub in the calling project, from its own copy of the same `.proto`
(`GrpcServices="Client"` instead, package `Grpc.Net.ClientFactory` instead of
`Grpc.AspNetCore.Server`).

### 3. Host the service (server)

```csharp
public class OrdersService : OrdersService.OrdersServiceBase
{
    public override Task<GetOrderReply> GetOrder(GetOrderRequest request, ServerCallContext context)
    {
        return Task.FromResult(new GetOrderReply { OrderId = request.OrderId, Status = "Shipped" });
    }
}
```

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddGrpc()
    .AddLaraueGrpcTelemetry();   // registers ServerTelemetryInterceptor

var app = builder.Build();
app.MapGrpcService<OrdersService>();
app.Run();
```

### 4. Call the service (client)

```csharp
builder.Services.AddLaraueGrpcClient<OrdersService.OrdersServiceClient>(options =>
{
    options.Address = new Uri("https://orders-service:5001");
});
```

```csharp
public class SomeConsumer(OrdersService.OrdersServiceClient ordersClient)
{
    public async Task<string> GetOrderStatusAsync(string orderId)
    {
        var reply = await ordersClient.GetOrderAsync(new GetOrderRequest { OrderId = orderId });
        return reply.Status;
    }
}
```

Every call made through `ordersClient` is now traced and measured on both sides. If the caller
already has an ambient `Activity` (e.g. from an incoming HTTP request it's handling), the trace
continues through into `OrdersService` on the other side - no extra code needed for that.

### 5. Export what's being recorded

Traces/metrics are recorded either way, but nothing leaves the process until an OpenTelemetry
pipeline with an exporter is registered. Do this in **both** the server and the client app - each
side exports what it locally recorded.

Via `Laraue.Grpc.OpenTelemetry` (recommended - one line, no magic strings):

```csharp
builder.Services.AddLaraueGrpcTelemetry(
    configureTracing: t => t.AddOtlpExporter(),
    configureMetrics: m => m.AddOtlpExporter());
```

Or by hand, e.g. if the app already builds its own OpenTelemetry pipeline for other sources:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource("Laraue.Grpc").AddOtlpExporter())
    .WithMetrics(m => m.AddMeter("Laraue.Grpc").AddOtlpExporter());
```

(`AddOtlpExporter()` needs the `OpenTelemetry.Exporter.OpenTelemetryProtocol` package; swap in
`AddConsoleExporter()`, `AddPrometheusExporter()`, etc. as needed.)

### What you get

- **Traces**: one span per call on each side, named `<package.Service>/<Method>` (e.g.
  `orders.v1.OrdersService/GetOrder`), tagged `rpc.system=grpc`, `rpc.service`, `rpc.method`,
  `rpc.grpc.status_code` - client and server spans share the same trace ID, so a call shows up as
  one connected trace across both processes, not two unrelated ones.
- **Metrics**: `rpc.client.duration`/`rpc.server.duration` histograms (ms) and
  `rpc.calls.started`/`rpc.calls.failed` counters, tagged with the same `rpc.*` attributes so they
  can be sliced by service/method/status in whatever backend the exporter sends to.
- **Errors**: a thrown `RpcException` or a non-OK `Status` marks the activity
  `ActivityStatusCode.Error` and increments `rpc.calls.failed`, on both sides.

## Architecture

- **`Laraue.Grpc.Common`** — the `ActivitySource`/`Meter` (`"Laraue.Grpc"`) shared by both
  sides, OpenTelemetry semantic-convention tag names (`GrpcTags`), a `GrpcMethodInfo` helper
  that parses gRPC's `/package.Service/Method` full method name, and `CallScope`, which both
  interceptors use to open/tag an `Activity` and record duration/failure metrics for a call.
- **`Laraue.Grpc.Server`** — `ServerTelemetryInterceptor` wraps every incoming call (unary,
  client/server-streaming, duplex) in a server `Activity`. Register with
  `services.AddGrpc().AddLaraueGrpcTelemetry()`.
- **`Laraue.Grpc.Client`** — `ClientTelemetryInterceptor` does the same on the client side and
  injects W3C `traceparent`/`tracestate` into outgoing gRPC metadata so the server continues the
  same trace. Register with
  `services.AddLaraueGrpcClient<TClient>(o => o.Address = new Uri(...))`, a thin wrapper over
  `Grpc.Net.ClientFactory`'s `AddGrpcClient<T>`.

Service contracts (`.proto` files) are **not** part of this library — each app keeps its
own `Protos/*.proto` and generated stubs, and references `Laraue.Grpc.Server` /
`Laraue.Grpc.Client` only for the interceptors and DI wiring.

Neither `Common`, `Server` nor `Client` depend on the OpenTelemetry SDK directly. Consuming apps
opt in by name, either by hand:

```csharp
services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource("Laraue.Grpc"))
    .WithMetrics(m => m.AddMeter("Laraue.Grpc"));
```

or, to avoid every service re-typing that magic string, via the optional
**`Laraue.Grpc.OpenTelemetry`** package:

```csharp
services.AddLaraueGrpcTelemetry(
    configureTracing: t => t.AddOtlpExporter(),
    configureMetrics: m => m.AddOtlpExporter());
```

`AddLaraueGrpcTelemetry` is `services.AddOpenTelemetry()` plus `AddSource("Laraue.Grpc")`/
`AddMeter("Laraue.Grpc")` already wired in - it doesn't pick exporters for you, those still go
through the `configureTracing`/`configureMetrics` callbacks (OTLP, Console, Prometheus, ...), so
apps that also emit their own traces/metrics can keep composing the same `TracerProviderBuilder`/
`MeterProviderBuilder`. `AddLaraueGrpc()` extension methods on `TracerProviderBuilder`/
`MeterProviderBuilder` are available separately too, for apps building their OTel pipeline by hand.

Server-side trace context is extracted directly from the incoming gRPC metadata
(`GrpcTraceContextPropagator`), not from ambient `Activity.Current`/ASP.NET Core hosting
instrumentation - so a trace continues across the wire even in an app that has no OpenTelemetry
AspNetCore instrumentation, only `Laraue.Grpc`'s own source/meter.

## Tests

- **`Laraue.Grpc.Common.UnitTests`** — pure unit tests for `GrpcMethodInfo` and `CallScope`
  (activity tagging, error status, duration/failure metrics), using in-process `ActivityListener`/
  `MeterListener` spies. No network, no hosting.
- **`Laraue.Grpc.IntegrationTests`** — a minimal demo `Greeter` service + `.proto` contract
  (`Protos/greeter.proto`) hosted on an in-memory `Microsoft.AspNetCore.TestHost.TestServer`, with
  a real `Greeter.GreeterClient` built through `AddLaraueGrpcClient`. End-to-end tests assert both
  interceptors actually run against each other: matching trace IDs across the wire, duration
  metrics on both sides, and error propagation on a thrown `RpcException`.
- **`Laraue.Grpc.OpenTelemetry.UnitTests`** — wires `AddLaraueGrpcTelemetry` into a real
  `TracerProvider`/`MeterProvider` with the OpenTelemetry `InMemory` exporter, and asserts it
  captures activities/metrics from the `"Laraue.Grpc"` source but not unrelated ones.
