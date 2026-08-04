using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using UniClaw.Device;
using UniClaw.Host.Artifacts;
using UniClaw.Host.Runner;

namespace UniClaw.Host.Tests.Runner;

/// <summary>
/// Shared fake action-executor / entry-driver / ADB helpers for Host-level
/// unit tests (<see cref="EnginePathTests"/> and others).
/// </summary>
internal static class RunnerTestHarness
{
    public static MenuItem Item(string name) =>
        new(
            name,
            new Coordinate(0.5, 0.7),
            MenuItemType.MenuItem,
            ExpectedAction: ExpectedAction.Navigate,
            ExpectsPageChange: true);

    public static MenuInfo Menu(string name, double x = 0.5, double y = 0.7) =>
        new(name, new Coordinate(x, y));

    /// <summary>
    /// Build a <see cref="ScenarioObservation"/> for a Settings home/child page.
    /// <paramref name="level1Menus"/> fills <see cref="PageAnalysis.Level1Menus"/>
    /// (the enumerate planner consumes this); <paramref name="item"/> fills
    /// <see cref="PageAnalysis.Items"/> (the locate planner consumes this).
    /// </summary>
    public static ScenarioObservation Observation(
        string fingerprint,
        string page,
        MenuItem? item = null,
        bool hasScroll = false,
        bool isEnd = false,
        byte[]? screenshot = null,
        ImmutableArray<MenuInfo>? level1Menus = null) =>
        new(
            screenshot ?? [1, 2, 3],
            $"<hierarchy fingerprint=\"{fingerprint}\" />",
            new PageAnalysis(
                Direction.Left,
                Direction.Left,
                Level1Menus: level1Menus ?? ImmutableArray<MenuInfo>.Empty,
                CurrentPath: [page],
                Items: item is null ? [] : [item],
                HasScroll: hasScroll,
                IsEndOfList: isEnd),
            page,
            "com.android.settings",
            fingerprint,
            isEnd ? "verified_end_of_list" : hasScroll ? "scrollable" : "no_scroll",
            DateTimeOffset.UtcNow);

    public static RunManifestInput Manifest(string runId) =>
        new(
            runId,
            null,
            null,
            "revision",
            "fake-device",
            "AOSP API 35",
            "mock",
            "deterministic-settings-v1",
            "mode-a");
}

internal sealed class FakeActionExecutor : IActionExecutor
{
    public List<string> Calls { get; } = [];

    public Task<bool> TapAsync(
        double x,
        double y,
        CancellationToken cancellationToken = default) =>
        Called("click");

    public Task<bool> SwipeAsync(
        double startX,
        double startY,
        double endX,
        double endY,
        int durationMs,
        CancellationToken cancellationToken = default) =>
        Called("scroll");

    public Task<bool> PressBackAsync(
        CancellationToken cancellationToken = default) =>
        Called("back");

    public Task<bool> InputTextAsync(
        string text,
        CancellationToken cancellationToken = default) =>
        Called("input");

    public Task<bool> LongPressAsync(
        double x,
        double y,
        int durationMs,
        CancellationToken cancellationToken = default) =>
        Called("long_press");

    public Task WaitAsync(
        int milliseconds,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public List<ActionRecord> GetHistory() => [];

    private Task<bool> Called(string action)
    {
        Calls.Add(action);
        return Task.FromResult(true);
    }
}

internal sealed class FakeEntryDriver : IEntryActionDriver
{
    public Task<bool> OpenDeepLinkAsync(
        string target,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<bool> ColdLaunchAsync(
        string targetApp,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task WaitAsync(
        int milliseconds,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<bool> CheckConditionAsync(
        IReadOnlyDictionary<string, object>? waitCondition,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}

internal sealed class FakeAdbRunner : IAdbSession
{
    public string Serial => "fake-device";

    public Task<byte[]> CaptureScreenshotAsync(CancellationToken ct = default) =>
        throw new InvalidOperationException("ADB must not be used by fake runner.");

    public Task<ShellResult> ExecuteShellAsync(
        string command,
        CancellationToken ct = default) =>
        throw new InvalidOperationException("ADB must not be used by fake runner.");

    public Task<string> DumpUiHierarchyAsync(CancellationToken ct = default) =>
        throw new InvalidOperationException("ADB must not be used by fake runner.");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}