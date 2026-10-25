using System.Runtime.ExceptionServices;
using System.Text.Json;
using UniClaw.Agent.Dsh;

namespace UniClaw.Host.Dsh;

public sealed partial class RuntimeToolHost
{
    /// <summary>Opt-in Development Harness output; no Product profile dependency.</summary>
    public sealed record DevelopmentAssetRequest(string ChangeId, string? AssetConfig = null, string? RetryOf = null);

    private sealed record DevelopmentAssetScope(string ChangeId, string RunId, string Root, string Raw, string Results)
    {
        public string PacketPath => Path.Combine(Raw, "diagnosis.packet.json");
        public string SignalDirectory => Path.Combine(Results, "evolution");
        public void ValidateOutput(string path)
        {
            _ = Reference(path);
            for (DirectoryInfo? current = new(Path.GetDirectoryName(Path.GetFullPath(path))!); current is not null; current = current.Parent)
                if (current.LinkTarget is not null)
                    throw new InvalidOperationException("development-assets-output-invalid: output path contains a symbolic link");
            if (new FileInfo(path).LinkTarget is not null)
                throw new InvalidOperationException("development-assets-output-invalid: output path is a symbolic link");
        }
        public string Reference(string path)
        {
            var relative = Path.GetRelativePath(Root, Path.GetFullPath(path));
            if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException("development-assets-outside-run: invalid output reference");
            return $"test-assets:{ChangeId}/{RunId}/{relative.Replace(Path.DirectorySeparatorChar, '/')}";
        }
    }

    /// <summary>Explicit dev invocation; Python owns layout resolution and asset
    /// inventories. This adapter only owns diagnosis output and its finalization.</summary>
    public async Task<DiagnosisResult> InvokeProcedureWithAssetsAsync(
        string name, string runsRoot, string runDirName, DiagnosisTransport transport,
        ModelConfiguration model, DevelopmentAssetRequest request, TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ChangeId))
            throw new InvalidOperationException("development-assets-begin-failed: changeId is required");
        var (_, skillPath, exitEntry) = PrepareDiagnosisTool(name);
        var runDir = ResolveRunDir(runsRoot, runDirName);
        var reportPath = Path.Combine(ResolveOutputDir(runsRoot, runDirName, runDir), "report.json");
        var entry = Path.Combine(_repoRoot, "tools", "test-assets");
        if (!File.Exists(entry)) throw new InvalidOperationException("development-assets-unavailable: missing asset CLI");
        var args = new List<string> { "begin", request.ChangeId, "--operation", "diagnosis",
            "--protected-root", Path.GetFullPath(runsRoot), "--input", reportPath, "--input", skillPath,
            "--input", Path.Combine(runDir, "facts.json"),
            "--source", Path.Combine(_repoRoot, "src", "UniClaw.Host.Dsh", "RuntimeToolHost.cs"),
            "--source", Path.Combine(_repoRoot, "src", "UniClaw.Host.Dsh", "RuntimeToolHost.DevelopmentAssets.cs"),
            "--source", Path.Combine(_repoRoot, "src", "UniClaw.Host.Dsh", "RuntimeDiagnosisPacket.cs"),
            "--context", JsonSerializer.Serialize(new { tool = name, model = new { model.Provider, model.Name }, timeoutMilliseconds = (timeout ?? DefaultDiagnosisTimeout).TotalMilliseconds }) };
        if (exitEntry is not null) args.AddRange(["--source", exitEntry]);
        if (request.AssetConfig is not null) args.AddRange(["--asset-config", request.AssetConfig]);
        if (request.RetryOf is not null) args.AddRange(["--retry-of", request.RetryOf]);
        var begin = await RunScriptAsync(entry, args.ToArray()).ConfigureAwait(false);
        if (begin.ExitCode != 0) throw new InvalidOperationException("development-assets-begin-failed: " + Limit(begin.Stderr));
        DevelopmentAssetScope scope;
        using (var doc = JsonDocument.Parse(begin.Stdout))
        {
            var root = doc.RootElement;
            scope = new DevelopmentAssetScope(root.GetProperty("changeId").GetString()!, root.GetProperty("runId").GetString()!,
                root.GetProperty("root").GetString()!, root.GetProperty("raw").GetString()!, root.GetProperty("results").GetString()!);
        }
        DiagnosisResult? result = null;
        Exception? failure = null;
        var status = "ERROR";
        try
        {
            result = await InvokeProcedureCoreAsync(name, runsRoot, runDirName, transport, model, timeout, scope, cancellationToken).ConfigureAwait(false);
            status = "SUCCEEDED";
        }
        catch (Exception error)
        {
            failure = error;
            status = error is OperationCanceledException ? "CANCELLED" : "ERROR";
        }
        string? finalizationError = null;
        try
        {
            var summary = JsonSerializer.Serialize(new { operation = "diagnosis", status,
                consumersCompleted = result is not null, error = failure?.Message,
                packetRef = result?.PacketRef, packetSha256 = result?.PacketSha256,
                signalRefs = result?.SignalRefs });
            var finish = await RunScriptAsync(entry, ["finish", scope.ChangeId, scope.RunId, "--summary", summary]).ConfigureAwait(false);
            if (finish.ExitCode != 0) finalizationError = "development-assets-finalization-pending: " + Limit(finish.Stderr);
        }
        catch (Exception error)
        {
            finalizationError = "development-assets-finalization-pending: " + error.Message;
        }
        if (failure is not null)
        {
            failure.Data["assetChangeId"] = scope.ChangeId;
            failure.Data["assetRunId"] = scope.RunId;
            if (finalizationError is not null) failure.Data["assetFinalizationError"] = finalizationError;
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        return result! with { AssetChangeId = scope.ChangeId, AssetRunId = scope.RunId, AssetFinalizationError = finalizationError };
    }
}
