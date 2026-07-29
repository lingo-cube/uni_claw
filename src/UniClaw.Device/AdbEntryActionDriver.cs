using System.Collections.Immutable;
using System.Text.RegularExpressions;
using UniClaw.Core.Traversal;

namespace UniClaw.Device;

public sealed partial class AdbEntryActionDriver : IEntryActionDriver
{
    private const string RemoteHierarchyPath = "/sdcard/uniclaw-entry-dump.xml";

    private readonly IAdbCommandRunner _runner;
    private readonly TimeSpan _timeout;

    public AdbEntryActionDriver(
        IAdbCommandRunner runner,
        TimeSpan? timeout = null)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _timeout = timeout ?? TimeSpan.FromSeconds(20);
        if (_timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    public async Task<bool> OpenDeepLinkAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(target, UriKind.Absolute, out _))
            return false;
        var result = await _runner.RunAsync(
            AdbCommandRequest.Create(
                [
                    "shell", "am", "start",
                    "-a", "android.intent.action.VIEW",
                    "-d", target,
                ],
                _timeout),
            cancellationToken);
        ThrowIfCancelled(result, cancellationToken);
        return result.Succeeded;
    }

    public async Task<bool> ColdLaunchAsync(
        string targetApp,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetApp)
            || !PackageNameRegex().IsMatch(targetApp))
        {
            return false;
        }

        var stop = await _runner.RunAsync(
            AdbCommandRequest.Create(
                ["shell", "am", "force-stop", targetApp],
                _timeout),
            cancellationToken);
        ThrowIfCancelled(stop, cancellationToken);
        if (!stop.Succeeded)
            return false;

        var launch = await _runner.RunAsync(
            AdbCommandRequest.Create(
                [
                    "shell", "monkey", "-p", targetApp,
                    "-c", "android.intent.category.LAUNCHER", "1",
                ],
                _timeout),
            cancellationToken);
        ThrowIfCancelled(launch, cancellationToken);
        return launch.Succeeded;
    }

    public Task WaitAsync(
        int milliseconds,
        CancellationToken cancellationToken = default)
    {
        if (milliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(milliseconds));
        return Task.Delay(milliseconds, cancellationToken);
    }

    public async Task<bool> CheckConditionAsync(
        IReadOnlyDictionary<string, object>? waitCondition,
        CancellationToken cancellationToken = default)
    {
        if (waitCondition is null || waitCondition.Count == 0)
            return true;

        foreach (var condition in waitCondition.OrderBy(
                     pair => pair.Key,
                     StringComparer.Ordinal))
        {
            if (condition.Value is not string expected
                || string.IsNullOrWhiteSpace(expected))
            {
                return false;
            }

            var passed = condition.Key switch
            {
                "package" => await WindowContainsAsync(expected, cancellationToken),
                "text" => await HierarchyContainsAsync(expected, cancellationToken),
                _ => false,
            };
            if (!passed)
                return false;
        }

        return true;
    }

    private async Task<bool> WindowContainsAsync(
        string expected,
        CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            AdbCommandRequest.Create(
                ["shell", "dumpsys", "activity", "activities"],
                _timeout),
            cancellationToken);
        ThrowIfCancelled(result, cancellationToken);
        return result.Succeeded
               && result.StandardOutput.Contains(
                   expected,
                   StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> HierarchyContainsAsync(
        string expected,
        CancellationToken cancellationToken)
    {
        var dump = await _runner.RunAsync(
            AdbCommandRequest.Create(
                ["shell", "uiautomator", "dump", RemoteHierarchyPath],
                _timeout),
            cancellationToken);
        ThrowIfCancelled(dump, cancellationToken);
        if (!dump.Succeeded)
            return false;

        var read = await _runner.RunAsync(
            new AdbCommandRequest(
                ImmutableArray.Create("exec-out", "cat", RemoteHierarchyPath),
                _timeout),
            cancellationToken);
        ThrowIfCancelled(read, cancellationToken);
        return read.Succeeded
               && read.StandardOutput.Contains(
                   expected,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static void ThrowIfCancelled(
        AdbCommandResult result,
        CancellationToken cancellationToken)
    {
        if (result.Failure?.Kind == "cancelled")
            throw new OperationCanceledException(
                result.Failure.Message,
                cancellationToken);
    }

    [GeneratedRegex(@"^[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex PackageNameRegex();
}
