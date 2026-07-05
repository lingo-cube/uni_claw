using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.StateMachine;
using Xunit;

namespace UniClaw.Core.Tests.StateMachine;

/// <summary>
/// OperationDispatcher tests (5 scenarios).
/// Tests the internal OperationType → IActionExecutor dispatch logic directly.
/// </summary>
public class OperationDispatcherTests
{
    [Fact]
    public async Task Dispatch_Click_Coordinate()
    {
        var mock = new MockActionExecutor { NextResult = true };
        var coord = new Coordinate(0.5, 0.75);
        var op = new Operation(OperationType.Click, new Target(TargetType.Coordinate, coord));

        var result = await OperationDispatcher.DispatchAsync(op, mock);

        Assert.True(result);
        Assert.Single(mock.CallLog);
        Assert.Equal("tap", mock.CallLog[0].Action);
        Assert.Equal(0.5, mock.CallLog[0].Parameters["x"]);
        Assert.Equal(0.75, mock.CallLog[0].Parameters["y"]);
    }

    [Fact]
    public async Task Dispatch_Swipe()
    {
        var mock = new MockActionExecutor { NextResult = true };
        var startCoord = new Coordinate(0.1, 0.9);
        var endCoord = new Coordinate(0.1, 0.1);
        var paramsDict = new Dictionary<string, object>
        {
            ["end_coordinate"] = endCoord,
            ["duration_ms"] = 500
        }.ToImmutableDictionary();
        var op = new Operation(
            OperationType.Swipe,
            new Target(TargetType.Coordinate, startCoord),
            Params: paramsDict);

        var result = await OperationDispatcher.DispatchAsync(op, mock);

        Assert.True(result);
        Assert.Single(mock.CallLog);
        Assert.Equal("swipe", mock.CallLog[0].Action);
        Assert.Equal(0.1, mock.CallLog[0].Parameters["start_x"]);
        Assert.Equal(0.9, mock.CallLog[0].Parameters["start_y"]);
        Assert.Equal(0.1, mock.CallLog[0].Parameters["end_x"]);
        Assert.Equal(0.1, mock.CallLog[0].Parameters["end_y"]);
        Assert.Equal(500, mock.CallLog[0].Parameters["duration_ms"]);
    }

    [Fact]
    public async Task Dispatch_Back()
    {
        var mock = new MockActionExecutor { NextResult = true };
        var op = new Operation(OperationType.Back); // no target needed

        var result = await OperationDispatcher.DispatchAsync(op, mock);

        Assert.True(result);
        Assert.Single(mock.CallLog);
        Assert.Equal("back", mock.CallLog[0].Action);
    }

    [Fact]
    public async Task Dispatch_InputText()
    {
        var mock = new MockActionExecutor { NextResult = true };
        var op = new Operation(OperationType.InputText, new Target(TargetType.Text, "hello world"));

        var result = await OperationDispatcher.DispatchAsync(op, mock);

        Assert.True(result);
        Assert.Single(mock.CallLog);
        Assert.Equal("input_text", mock.CallLog[0].Action);
        Assert.Equal("hello world", mock.CallLog[0].Parameters["text"]);
    }

    [Fact]
    public async Task Dispatch_NullTarget_Throws()
    {
        var mock = new MockActionExecutor { NextResult = true };
        // Click with null Target → InvalidOperationException
        var op = new Operation(OperationType.Click, null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => OperationDispatcher.DispatchAsync(op, mock));
    }
}
