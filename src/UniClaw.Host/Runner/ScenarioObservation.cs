using System.Text.RegularExpressions;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using UniClaw.Device;

namespace UniClaw.Host.Runner;

public sealed record class ScenarioObservation(
    byte[] Screenshot,
    PageAnalysis Analysis,
    string PageIdentity,
    string PackageName,
    DateTimeOffset Timestamp);

public interface IScenarioObservationSource
{
    Task<ScenarioObservation> ObserveAsync(
        CancellationToken cancellationToken = default);
}

public sealed class ScenarioObservationException : Exception
{
    public string Kind { get; }

    public ScenarioObservationException(string kind, string message)
        : base(message)
    {
        Kind = kind;
    }
}

public sealed class AdbScenarioObservationSource : IScenarioObservationSource
{
    private readonly IAdbSession _session;
    private readonly AdbScreenCapture _capture;
    private readonly IObservableScreenStateProvider _screenState;
    private readonly IPageAnalyzer _pageAnalyzer;

    public AdbScenarioObservationSource(
        IAdbSession session,
        AdbScreenCapture capture,
        IObservableScreenStateProvider screenState,
        IPageAnalyzer pageAnalyzer)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _screenState = screenState
                       ?? throw new ArgumentNullException(nameof(screenState));
        _pageAnalyzer = pageAnalyzer
                        ?? throw new ArgumentNullException(nameof(pageAnalyzer));
    }

    public async Task<ScenarioObservation> ObserveAsync(
        CancellationToken cancellationToken = default)
    {
        var screenshot = await _capture.CaptureAsync(cancellationToken);
        var state = await _screenState.RefreshAsync(cancellationToken);
        if (!state.Succeeded)
        {
            throw new ScenarioObservationException(
                state.Failure?.Kind ?? state.Status,
                state.Failure?.Message
                ?? $"Screen-state observation failed: {state.Status}");
        }

        var packageName = await GetCurrentPackageAsync(cancellationToken);

        // The AI observation (core-observation-pipeline D1) lives in the
        // ObservationPipeline; the source only consumes its analysis.
        var analysis = await _pageAnalyzer.AnalyzeCurrentPageAsync(cancellationToken)
                       ?? throw new ScenarioObservationException(
                           "analysis_empty",
                           "Page analyzer returned no analysis.");
        var pageIdentity = analysis.CurrentPath.LastOrDefault() ?? "unknown";
        return new ScenarioObservation(
            screenshot,
            analysis,
            pageIdentity,
            packageName,
            DateTimeOffset.UtcNow);
    }

    private async Task<string> GetCurrentPackageAsync(
        CancellationToken cancellationToken)
    {
        var result = await _session.ExecuteShellAsync(
            "dumpsys activity activities",
            cancellationToken);
        if (!result.Success)
        {
            throw new ScenarioObservationException(
                "adb_failure",
                string.IsNullOrWhiteSpace(result.StandardError)
                    ? "Could not read current package."
                    : result.StandardError);
        }

        var match = Regex.Match(
            result.StandardOutput,
            @"(?:mResumedActivity|topResumedActivity|mCurrentFocus|mFocusedApp)[^\r\n]*?\s(?<package>[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)+)/",
            RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["package"].Value : "unknown";
    }
}
