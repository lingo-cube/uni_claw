using System.Diagnostics;
using System.Text;

namespace UniClaw.Host.Dsh;

/// <summary>
/// PNL-008 / ADR-0039：Harness 工具暴露清单（tool-registry.yaml）在 Runtime
/// Host 侧的读取与执行执法。本类是 adapter：只消费清单声明的暴露元数据，
/// 不硬编码任何工具路径；run-report 等工具经「清单条目 → 进程调用」执行。
/// Registry 文件格式刻意受限（两层映射 + 标量 + 行内列表），解析器只支持
/// 该子集，词汇执法在 tools/validate-tool-registry.py（确定性脚本）。
/// </summary>
public sealed class RuntimeToolHost
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

    private readonly IReadOnlyDictionary<string, ToolDescriptor> _tools;
    private readonly string _repoRoot;

    private RuntimeToolHost(IReadOnlyDictionary<string, ToolDescriptor> tools, string repoRoot)
    {
        _tools = tools;
        _repoRoot = repoRoot;
    }

    public static RuntimeToolHost Load(string registryPath)
    {
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
        return new RuntimeToolHost(tools, repoRoot);
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
        var outDir = Path.Combine(runDir, "report");

        var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("UNICLAW_TOOL_PYTHON") ?? "python3")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add(entry);
        info.ArgumentList.Add("--run-dir");
        info.ArgumentList.Add(runDir);
        info.ArgumentList.Add("--out-dir");
        info.ArgumentList.Add(outDir);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("tool-invoke-failed: process did not start");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120_000))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException("tool-invoke-failed: tool timed out after 120s");
        }
        var stdout = Limit(await stdoutTask) ?? string.Empty;
        var stderr = Limit(await stderrTask) ?? string.Empty;
        var reportJson = Path.Combine(outDir, "report.json");
        var reportMd = Path.Combine(outDir, "report.md");
        return new InvokeResult(
            process.ExitCode,
            stdout,
            stderr,
            File.Exists(reportJson) ? Relative(runsRoot, reportJson) : null,
            File.Exists(reportMd) ? Relative(runsRoot, reportMd) : null);
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
