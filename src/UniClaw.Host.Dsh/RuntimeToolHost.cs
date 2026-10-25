using System.Diagnostics;
using System.Text;
using UniClaw.Agent.Dsh;

namespace UniClaw.Host.Dsh;

/// <summary>
/// PNL-008 / ADR-0039：Harness 工具暴露清单（tool-registry.yaml）在 Runtime
/// Host 侧的读取与执行执法。本类是 adapter：只消费清单声明的暴露元数据，
/// 不硬编码任何工具路径；run-report 等工具经「清单条目 → 进程调用」执行。
/// Registry 文件格式刻意受限（两层映射 + 标量 + 行内列表），解析器只支持
/// 该子集，词汇执法在 tools/validate-tool-registry.py（确定性脚本）。
/// </summary>
public sealed partial class RuntimeToolHost
{
    private const int StdoutTailBytes = 8_192;

    public sealed record ToolDescriptor(
        string Name,
        string Summary,
        string Backing,
        string Invocation,
        string? Entry,
        string? SkillRef,
        string? OutputSchema,
        string RequiredCapability,
        string Posture,
        IReadOnlyList<string> Surfaces,
        string Status,
        IReadOnlyList<string> Consumes);

    public sealed record InvokeResult(
        int ExitCode,
        string StdoutTail,
        string StderrTail,
        string? ReportJson,
        string? ReportMd)
    {
        public bool Ok => ExitCode == 0;
    }

    public sealed record InvokeError(string Code, string Message)
    {
        public static InvokeError UnknownTool(string name) => new("tool-not-found", $"No tool named '{name}' is registered.");
        public static InvokeError NotInvokable(string reason) => new("tool-not-invokable", reason);
        public static InvokeError InvalidRunDir(string reason) => new("invalid-run-dir", reason);
    }

    /// <summary>
    /// PNL-012：model-procedure 诊断的传输缝，与 <see cref="DshSlowConsult.Transport"/>
    /// 同形（裁剪到纯文本：诊断无需图像）。生产实现经 <see cref="DshOpenedHttpPeer"/>
    /// 的 slow 端点；测试注入 fake。本传输缝仅返回文本，诊断包由Host导出到evidence。
    /// </summary>
    public delegate Task<string> DiagnosisTransport(
        string requestId, string prompt, ModelConfiguration model, CancellationToken cancellationToken);

    /// <summary>诊断结果：exitCode 概念不适用，改为 ok 语义（异常即失败）。</summary>
    public sealed record DiagnosisResult(string Model, string Text, string ReportRef,
        string PacketRef, string PacketSha256, IReadOnlyList<string>? SignalRefs = null)
    {
        public string? AssetChangeId { get; init; }
        public string? AssetRunId { get; init; }
        public string? AssetFinalizationError { get; init; }
    }

    private readonly IReadOnlyDictionary<string, ToolDescriptor> _tools;
    private readonly string _repoRoot;
    private readonly RuntimeToolOutputConfig _output;
    private readonly UniClaw.Host.Runtime.ReportDeliveryStorageOptions _reportStorage;

    /// <summary>产出路径配置（PNL-010）；只读投影给端点透出。</summary>
    public RuntimeToolOutputConfig Output => _output;

    private RuntimeToolHost(IReadOnlyDictionary<string, ToolDescriptor> tools, string repoRoot, RuntimeToolOutputConfig output,
        UniClaw.Host.Runtime.ReportDeliveryStorageOptions? reportStorage = null)
    {
        _tools = tools;
        _repoRoot = repoRoot;
        _output = output;
        _reportStorage = (reportStorage ?? new()) with { };
    }

    public static RuntimeToolHost Load(string registryPath, RuntimeToolOutputConfig? output = null)
        => LoadCore(registryPath, output, new());

    public static RuntimeToolHost LoadWithReportStorage(string registryPath, UniClaw.Host.Runtime.ReportDeliveryStorageOptions reportStorage,
        RuntimeToolOutputConfig? output = null) => LoadCore(registryPath, output, reportStorage);

    private static RuntimeToolHost LoadCore(string registryPath, RuntimeToolOutputConfig? output, UniClaw.Host.Runtime.ReportDeliveryStorageOptions reportStorage)
    {
        var config = output ?? RuntimeToolOutputConfig.Default;
        if (!config.IsValid)
            throw new InvalidOperationException($"tool-output-config-invalid: base='{config.Base}' subdir='{config.Subdir}'");
        var repoRoot = Path.GetFullPath(Path.GetDirectoryName(registryPath) ?? ".");
        var tools = new Dictionary<string, ToolDescriptor>(StringComparer.Ordinal);
        ToolDescriptor? current = null;
        foreach (var line in File.ReadAllLines(registryPath))
        {
            var text = line.Split('#', 2)[0].TrimEnd();
            if (text.Trim().Length == 0) continue;
            var indent = text.Length - text.TrimStart().Length;
            var content = text.Trim();
            if (indent == 0)
            {
                current = null;
                continue; // version: / tools: 顶层键
            }
            if (indent == 2 && content.EndsWith(':'))
            {
                current = NewBlank(content[..^1]);
                tools[current.Name] = current;
                continue;
            }
            if (indent >= 4 && current is not null)
            {
                var separator = content.IndexOf(':');
                if (separator <= 0) continue;
                var key = content[..separator].Trim();
                var value = content[(separator + 1)..].Trim();
                var updated = WithField(current, key, value);
                tools[updated.Name] = updated;
                current = updated;
            }
        }
        return new RuntimeToolHost(tools, repoRoot, config, reportStorage);
    }

    public IReadOnlyList<ToolDescriptor> ForSurface(string surface) =>
        _tools.Values
            .Where(tool => tool.Surfaces.Contains(surface, StringComparer.Ordinal))
            .OrderBy(tool => tool.Name, StringComparer.Ordinal)
            .ToList();

    public ToolDescriptor? Find(string name) =>
        _tools.TryGetValue(name, out var tool) ? tool : null;

    /// <summary>执行一个 deterministic-script 工具；所有前置条件在此执法。</summary>
    public async Task<InvokeResult> InvokeAsync(string name, string runsRoot, string runDirName)
    {
        var tool = Find(name) ?? throw new InvalidOperationException("tool-not-found");
        if (tool.Status != "implemented")
            throw new InvalidOperationException($"tool-not-invokable: tool '{name}' has status '{tool.Status}'");
        if (tool.Invocation != "deterministic-script")
            throw new InvalidOperationException($"tool-not-invokable: invocation '{tool.Invocation}' is not executable by the Runtime Host");
        if (!tool.Surfaces.Contains("workbench", StringComparer.Ordinal))
            throw new InvalidOperationException("tool-not-invokable: tool is not exposed to the workbench surface");
        if (string.IsNullOrWhiteSpace(tool.Entry))
            throw new InvalidOperationException("tool-not-invokable: registry entry has no executable entry path");

        var runDir = ResolveRunDir(runsRoot, runDirName);
        var entry = Path.GetFullPath(Path.Combine(_repoRoot, tool.Entry));
        if (!File.Exists(entry))
            throw new InvalidOperationException($"tool-not-invokable: entry does not exist: {tool.Entry}");
        var outDir = ResolveOutputDir(runsRoot, runDirName, runDir);

        var process = await RunScriptAsync(entry, ["--run-dir", runDir, "--out-dir", outDir]);
        var reportJson = Path.Combine(outDir, "report.json");
        var reportMd = Path.Combine(outDir, "report.md");
        return new InvokeResult(
            process.ExitCode,
            Limit(process.Stdout) ?? string.Empty,
            Limit(process.Stderr) ?? string.Empty,
            File.Exists(reportJson) ? Relative(runsRoot, reportJson) : null,
            File.Exists(reportMd) ? Relative(runsRoot, reportMd) : null);
    }

    /// <summary>PNL-012：执行一个 model-procedure 工具（消费 run-report 产出的
    /// 结论性诊断）。只读运行资料，诊断包独立写入repo evidence。所有前置
    /// 条件在此执法；报告缺失时显式报错，不自动生成（报告由 run-report 拥有）。</summary>
    public const string DiagnosisNotice = "诊断输出：非权威观察，不构成 Runtime truth";
    private static readonly TimeSpan DefaultDiagnosisTimeout = TimeSpan.FromSeconds(120);

    public Task<DiagnosisResult> InvokeProcedureAsync(
        string name, string runsRoot, string runDirName,
        DiagnosisTransport transport, ModelConfiguration model, TimeSpan? timeout = null) =>
        InvokeProcedureCoreAsync(name, runsRoot, runDirName, transport, model, timeout, null, CancellationToken.None);

    private (ToolDescriptor Tool, string SkillPath, string? ExitEntry) PrepareDiagnosisTool(string name)
    {
        var tool = Find(name) ?? throw new InvalidOperationException("tool-not-found");
        if (tool.Status != "implemented")
            throw new InvalidOperationException($"tool-not-invokable: tool '{name}' has status '{tool.Status}'");
        if (tool.Invocation != "model-procedure")
            throw new InvalidOperationException($"tool-not-invokable: invocation '{tool.Invocation}' is not a model-procedure tool");
        if (!tool.Surfaces.Contains("workbench", StringComparer.Ordinal))
            throw new InvalidOperationException("tool-not-invokable: tool is not exposed to the workbench surface");
        if (string.IsNullOrWhiteSpace(tool.SkillRef))
            throw new InvalidOperationException("tool-not-invokable: registry entry has no skillRef (procedure carrier)");
        var skillPath = Path.GetFullPath(Path.Combine(_repoRoot, tool.SkillRef, "SKILL.md"));
        if (!File.Exists(skillPath))
            throw new InvalidOperationException($"tool-not-invokable: procedure does not exist: {tool.SkillRef}/SKILL.md");
        if (tool.Posture != "local-write")
            throw new InvalidOperationException("tool-not-invokable: diagnosis packet export requires local-write posture");
        var exitEntry = string.IsNullOrWhiteSpace(tool.Entry) ? null : Path.GetFullPath(Path.Combine(_repoRoot, tool.Entry));
        if (exitEntry is not null && !File.Exists(exitEntry))
            throw new InvalidOperationException("tool-not-invokable: declared exit adapter does not exist");

        return (tool, skillPath, exitEntry);
    }

    private async Task<DiagnosisResult> InvokeProcedureCoreAsync(
        string name, string runsRoot, string runDirName,
        DiagnosisTransport transport, ModelConfiguration model, TimeSpan? timeout,
        DevelopmentAssetScope? assets, CancellationToken cancellationToken)
    {
        var (_, skillPath, exitEntry) = PrepareDiagnosisTool(name);
        var runDir = ResolveRunDir(runsRoot, runDirName);
        var outDir = ResolveOutputDir(runsRoot, runDirName, runDir);
        var reportPath = Path.Combine(outDir, "report.json");
        if (!File.Exists(reportPath))
            throw new InvalidOperationException(
                $"diagnosis-report-missing: no report.json under '{Relative(runsRoot, reportPath)}' — invoke the run-report tool for this run first");

        var procedure = await File.ReadAllTextAsync(skillPath).ConfigureAwait(false);
        var reportBytes = await File.ReadAllBytesAsync(reportPath).ConfigureAwait(false);
        var report = Encoding.UTF8.GetString(reportBytes);
        var prompt = BuildDiagnosisPrompt(procedure, report);

        var budget = timeout ?? DefaultDiagnosisTimeout;
        string text;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(budget);
            text = await transport($"{name}:{runDirName}", prompt, model, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new InvalidOperationException($"diagnosis-timeout: model transport exceeded {(int)budget.TotalSeconds}s");
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception error)
        {
            throw new InvalidOperationException($"diagnosis-transport-failed: {error.Message}");
        }
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("diagnosis-transport-failed: model returned empty text");
        var modelName = $"{model.Provider}/{model.Name}";
        var packet = RuntimeDiagnosisPacket.Encode(text, modelName, reportPath, reportBytes, runDir);
        var currentReportBytes = await File.ReadAllBytesAsync(reportPath).ConfigureAwait(false);
        if (!reportBytes.AsSpan().SequenceEqual(currentReportBytes))
            throw new InvalidOperationException("diagnosis-report-changed: report changed during diagnosis; retry with stable inputs");
        var packetPath = assets?.PacketPath ?? Path.Combine(_repoRoot, "evidence", "diagnosis", Guid.NewGuid().ToString("N"), "diagnosis.packet.json");
        if (Path.GetFullPath(packetPath).StartsWith(runDir + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("diagnosis-export-invalid: evidence output must be outside the run directory");
        try
        {
            assets?.ValidateOutput(packetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(packetPath)!);
            await using var stream = new FileStream(packetPath, FileMode.CreateNew, FileAccess.Write);
            await stream.WriteAsync(packet.Bytes).ConfigureAwait(false);
        }
        catch (IOException error)
        {
            throw new InvalidOperationException("diagnosis-export-failed: " + error.Message, error);
        }
        var signals = Array.Empty<string>();
        if (exitEntry is not null)
        {
            if (assets is not null) assets.ValidateOutput(assets.SignalDirectory);
            var exit = await RunScriptAsync(exitEntry, ["--run-dir", runDir, "--out-dir",
                assets?.SignalDirectory ?? Path.Combine(_repoRoot, "evidence", "evolution"), "--repo-root", _repoRoot,
                "--diagnosis", packetPath]);
            if (exit.ExitCode != 0)
                throw new InvalidOperationException($"diagnosis-signal-failed: packet saved at {Relative(_repoRoot, packetPath)}; {Limit(exit.Stderr)}");
            using var output = System.Text.Json.JsonDocument.Parse(exit.Stdout);
            signals = output.RootElement.GetProperty("signals").EnumerateArray().Select(item => item.GetString()!).ToArray();
        }
        return new DiagnosisResult(modelName, packet.Summary, Relative(runsRoot, reportPath),
            assets?.Reference(packetPath) ?? Relative(_repoRoot, packetPath), RuntimeDiagnosisPacket.Hash(packet.Bytes),
            assets is null ? signals : signals.Select(assets.Reference).ToArray());
    }

    /// <summary>Prompt 组装：procedure 全文 + report.json + 明确输出指示（结论性
    /// JSON packet；Host负责来源、落盘及非权威展示声明）。</summary>
    public static string BuildDiagnosisPrompt(string procedure, string reportJson)
    {
        return $"""
            你是UniClaw的run-diagnosis工具。严格按下方procedure诊断report，输出单个JSON对象，不加代码围栏。schema="uniclaw.diagnosis.packet.v0"，authority="non-authoritative"。必须含evidenceLevel(E0-E4)、evidenceSummary(非空字符串数组)、failureClass(A-F)、lifecycleStage、lastCorrectState、firstDivergence(where/expected/observed/非空evidenceRefs数组)、owner、rootCauseSupport数组、remainingUncertainty数组、escalation(required布尔值/decisionRequest数组，required为true时reason和非空decisionRequest必填)。meta和subject由Host按实际模型与输入填充，勿自报。仅使用报告证据；未知位置或Owner明确写unknown并列入remainingUncertainty；expected必须引用独立任务要求，不能从actual反推。无法据证据分类时明确失败，不猜类别。不得写文件、改Runtime或宣告修复。展示层声明为：{DiagnosisNotice}。

            ===== PROCEDURE（uniclaw-debug-evidence）=====
            {procedure}

            ===== REPORT（report.json）=====
            {reportJson}
            """;
    }

    /// <summary>PNL-010：产出路径可配置（run-dir | runs-root × subdir），解析后必须仍在 runs 根内。</summary>
    private string ResolveOutputDir(string runsRoot, string runDirName, string runDir)
    {
        var rootFull = Path.GetFullPath(runsRoot);
        var outDir = Path.GetFullPath(_output.Base == RuntimeToolOutputConfig.BaseRunsRoot
            ? Path.Combine(rootFull, _output.Subdir, runDirName)
            : Path.Combine(runDir, _output.Subdir));
        if (!outDir.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException($"tool-output-escapes-runs-root: resolved output '{_output.Base}/{_output.Subdir}' leaves the runs root");
        return outDir;
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunScriptAsync(string entry, string[] arguments)
    {
        var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("UNICLAW_TOOL_PYTHON") ?? "python3")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        info.ArgumentList.Add(entry);
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("tool-invoke-failed: process did not start");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        try { await process.WaitForExitAsync(budget.Token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) when (process.HasExited) { }
            await process.WaitForExitAsync().ConfigureAwait(false);
            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            throw new InvalidOperationException("tool-invoke-failed: tool timed out after 120s");
        }
        return (process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
    }

    private static string? Limit(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Length <= StdoutTailBytes ? text : "…" + text[^StdoutTailBytes..];
    }

    private static string Relative(string root, string path) => Path.GetRelativePath(root, path);

    private static string ResolveRunDir(string runsRoot, string runDirName)
    {
        if (string.IsNullOrWhiteSpace(runDirName)
            || runDirName.Contains('/') || runDirName.Contains('\\')
            || runDirName.Contains("..", StringComparison.Ordinal)
            || Path.GetFileName(runDirName) != runDirName)
            throw new InvalidOperationException($"invalid-run-dir: '{runDirName}' must be a single directory name under the runs root");
        var runDir = Path.GetFullPath(Path.Combine(runsRoot, runDirName));
        var root = Path.GetFullPath(runsRoot);
        if (!runDir.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("invalid-run-dir: resolved path escapes the runs root");
        if (!Directory.Exists(runDir))
            throw new InvalidOperationException($"invalid-run-dir: no run directory '{runDirName}' under the runs root");
        return runDir;
    }

    private static ToolDescriptor NewBlank(string name) => new(
        name, string.Empty, string.Empty, string.Empty, null, null, null, string.Empty, string.Empty,
        Array.Empty<string>(), string.Empty, Array.Empty<string>());

    private static ToolDescriptor WithField(ToolDescriptor tool, string key, string value) => key switch
    {
        "summary" => tool with { Summary = value },
        "backing" => tool with { Backing = value },
        "invocation" => tool with { Invocation = value },
        "entry" => tool with { Entry = value },
        "skillRef" => tool with { SkillRef = value },
        "outputSchema" => tool with { OutputSchema = value },
        "requiredCapability" => tool with { RequiredCapability = value },
        "posture" => tool with { Posture = value },
        "status" => tool with { Status = value },
        "surfaces" => tool with { Surfaces = ParseList(value) },
        "consumes" => tool with { Consumes = ParseList(value) },
        _ => tool,
    };

    private static string[] ParseList(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '[' || trimmed[^1] != ']')
            return Array.Empty<string>();
        return trimmed[1..^1]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }
}
