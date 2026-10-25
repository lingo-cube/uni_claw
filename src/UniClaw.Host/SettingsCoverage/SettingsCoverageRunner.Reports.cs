using System.Text.Json;

namespace UniClaw.Host.SettingsCoverage;

public sealed partial class SettingsCoverageRunner
{
    private static bool ReportStorageError(Exception error) => error is IOException or UnauthorizedAccessException
        or InvalidDataException or JsonException or InvalidOperationException or ArgumentException or OperationCanceledException;

    private static async Task TryWriteReportProjectionAsync(string directory, string name, string content, ICollection<string> problems)
    {
        try { await WriteTextAsync(directory, name, content).ConfigureAwait(false); }
        catch (Exception error) when (ReportStorageError(error)) { problems.Add("coverage-projection:" + name + ":" + error.Message); }
    }
}
