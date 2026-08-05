using System.Text.RegularExpressions;
using UniClaw.Core.Traversal;

namespace UniClaw.Device;

public sealed partial class AdbEntryActionDriver : IEntryActionDriver
{
    private readonly IAdbSession _session;

    public AdbEntryActionDriver(
        IAdbSession session,
        TimeSpan? timeout = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    public async Task<bool> OpenDeepLinkAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(target, UriKind.Absolute, out _))
            return false;
        var result = await _session.ExecuteShellAsync(
            $"am start -a android.intent.action.VIEW -d {target}",
            cancellationToken);
        return result.Success;
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

        var stop = await _session.ExecuteShellAsync(
            $"am force-stop {targetApp}",
            cancellationToken);
        if (!stop.Success)
            return false;

        var launch = await _session.ExecuteShellAsync(
            $"monkey -p {targetApp} -c android.intent.category.LAUNCHER 1",
            cancellationToken);
        return launch.Success;
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
                // UIA hierarchy conditions ("text") were removed with the UIA
                // pipeline (delete-uia); no device-side text source remains.
                "text" => false,
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
        var result = await _session.ExecuteShellAsync(
            "dumpsys activity activities",
            cancellationToken);
        return result.Success
               && result.StandardOutput.Contains(
                   expected,
                   StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"^[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex PackageNameRegex();
}
