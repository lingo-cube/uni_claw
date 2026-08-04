using Xunit;

namespace UniClaw.TraceTool.Tests;

/// <summary>
/// Shared fixture: loads the success and failure snapshot runs once per test class via
/// TraceRunLoader (read-only replay of trace.jsonl into an InMemoryTraceService).
/// </summary>
public sealed class TraceRunFixture : IAsyncLifetime
{
    public TraceRun SuccessRun { get; private set; } = null!;

    public TraceRun FailureRun { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        SuccessRun = await TraceRunLoader.LoadAsync(FixturePath("success"));
        FailureRun = await TraceRunLoader.LoadAsync(FixturePath("failure"));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Resolve a fixture directory relative to the test output directory.</summary>
    public static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
}
