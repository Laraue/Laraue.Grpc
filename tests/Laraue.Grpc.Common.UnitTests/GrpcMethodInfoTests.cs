using Laraue.Grpc.Common.Telemetry;

namespace Laraue.Grpc.Common.UnitTests;

public class GrpcMethodInfoTests
{
    [Fact]
    public void Parse_ShouldSplitServiceAndMethod_WhenFullMethodNameIsWellFormed()
    {
        var method = GrpcMethodInfo.Parse("/orders.v1.OrdersService/GetOrder");

        Assert.Equal("orders.v1.OrdersService", method.ServiceName);
        Assert.Equal("GetOrder", method.MethodName);
    }

    [Fact]
    public void Parse_ShouldNotRequireLeadingSlash_Always()
    {
        var method = GrpcMethodInfo.Parse("orders.v1.OrdersService/GetOrder");

        Assert.Equal("orders.v1.OrdersService", method.ServiceName);
        Assert.Equal("GetOrder", method.MethodName);
    }

    [Fact]
    public void Parse_ShouldTreatWholeInputAsMethodName_WhenNoServiceSeparatorPresent()
    {
        var method = GrpcMethodInfo.Parse("GetOrder");

        Assert.Equal(string.Empty, method.ServiceName);
        Assert.Equal("GetOrder", method.MethodName);
    }

    [Fact]
    public void DisplayName_ShouldJoinServiceAndMethodWithSlash_Always()
    {
        var method = GrpcMethodInfo.Parse("/orders.v1.OrdersService/GetOrder");

        Assert.Equal("orders.v1.OrdersService/GetOrder", method.DisplayName);
    }
}
