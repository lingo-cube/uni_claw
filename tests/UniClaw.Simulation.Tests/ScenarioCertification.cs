using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// SIM-002 G2（S2）：golden 认证持久化——场景库条目的期望值快照经
/// 认证记录钉扎（certification block），test runtime 只允许 Verify。
///
/// 认证绑定（C8 golden 期望更新协议的机械执法面；SIM-003 G2 升级 v2）：
///  1. expectationsDigest——expectations 块 canonical rendering 的 SHA-256
///     （钉「测什么结果」）；
///  2. executionDigest——execution 绑定（kind/carrier/options）canonical
///     rendering 的 SHA-256（钉「用什么 executable carrier 测」）；v2 新增，
///     v1 fail-closed 废弃；
///  3. runtimeSourceHash——认证时的 Kernel+Agent **源码**状态哈希
///     （源码而非 DLL：DLL 哈希依赖构建环境不可跨机复现；源码哈希
///     内容寻址、python/C# 双侧可独立重算；SIM-003 G1 起对文本源统一
///     CRLF→LF 归一化——LF/CRLF checkout 同 digest）；
///  4. certifiedByChange——致因 change 引用（无记录的期望改动 = 认证
///     失效，覆盖率工具与 C# 验证器双双 fail）。
///
/// canonical 规范（与 tools/scenario-certify.py 逐字节一致，双侧独立
/// 实现互为一致性检查）：
///  expectations（cert-exp-v1）:
///    line1 = "cert-exp-v1"
///    line2 = "status={s}|classification={c 或 -}|effects={i}"
///          + "|agentConsultations={i}|unconsumedStimuli={i}"
///          + "|goalSatisfaction={g 或 -}"
///    digest = sha256(utf8(line1 + "\n" + line2)) 小写 hex
///  execution（cert-exec-v1）:
///    line1 = "cert-exec-v1"
///    line2 = "kind={kind}|carrier={carrier 或 -}"
///          + "|options={k=v,… key 升序逗号连接；无 options → -}"
///    digest = sha256(utf8(line1 + "\n" + line2)) 小写 hex
///  源码哈希（cert-src-v1）:
///    输入 = src/UniClaw.Kernel 与 src/UniClaw.Agent 下全部 *.cs / *.csproj
///    （排除 bin/obj；相对 POSIX 路径排序），逐文件先 CRLF→LF 归一化，再
///    "{pathLen}:{path}{contentLen}:" + 归一化字节
///    digest = sha256(utf8("cert-src-v1\x00") ++ 各文件帧) 小写 hex
/// </summary>
internal static class ScenarioCertification
{
    internal const string ExpectationsSchemaTag = "cert-exp-v1";
    internal const string ExecutionSchemaTag = "cert-exec-v1";
    internal const string SourceHashTag = "cert-src-v1";
    internal const int BlockSchemaVersion = 2;

    /// <summary>期望值快照（JSON expectations 块的解析面）。</summary>
    internal sealed record ExpectationsSnapshot(
        string Status,
        string? Classification,
        int Effects,
        int AgentConsultations,
        int UnconsumedStimuli,
        string? GoalSatisfaction);

    /// <summary>执行绑定快照（JSON execution 块的解析面；options key 升序）。</summary>
    internal sealed record ExecutionSnapshot(
        string Kind,
        string? Carrier,
        IReadOnlyList<(string Key, string Value)> Options);

    internal sealed record CertificationBlock(
        int SchemaVersion,
        string ExpectationsDigest,
        string? ExecutionDigest,
        string RuntimeSourceHash,
        string CertifiedByChange,
        string CertifiedAt);

    // ---- canonical rendering / digests ---------------------------------

    internal static string RenderCanonical(ExpectationsSnapshot snapshot) =>
        ExpectationsSchemaTag + "\n"
        + "status=" + snapshot.Status
        + "|classification=" + (snapshot.Classification is null ? "-" : snapshot.Classification)
        + "|effects=" + snapshot.Effects.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + "|agentConsultations=" + snapshot.AgentConsultations.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + "|unconsumedStimuli=" + snapshot.UnconsumedStimuli.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + "|goalSatisfaction=" + (snapshot.GoalSatisfaction is null ? "-" : snapshot.GoalSatisfaction);

    internal static string DigestOf(ExpectationsSnapshot snapshot) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(RenderCanonical(snapshot)))).ToLowerInvariant();

    internal static string RenderExecutionCanonical(ExecutionSnapshot snapshot)
    {
        var options = snapshot.Options.Count == 0
            ? "-"
            : string.Join(",", snapshot.Options.Select(pair => pair.Key + "=" + pair.Value));
        return ExecutionSchemaTag + "\n"
            + "kind=" + snapshot.Kind
            + "|carrier=" + (snapshot.Carrier is null ? "-" : snapshot.Carrier)
            + "|options=" + options;
    }

    internal static string DigestOfExecution(ExecutionSnapshot snapshot) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(RenderExecutionCanonical(snapshot)))).ToLowerInvariant();

    /// <summary>Kernel+Agent 源码状态哈希（认证时的运行时源）。
    /// SIM-003 G1：文本源逐文件 CRLF→LF 归一化后哈希——LF/CRLF checkout
    /// 同 digest（与 python 侧逐字节一致；.gitattributes 只是第二道防线）。</summary>
    internal static string RuntimeSourceHash(string repoRoot)
    {
        var files = new List<string>();
        foreach (var project in new[] { "src/UniClaw.Kernel", "src/UniClaw.Agent" })
        {
            var dir = Path.Combine(repoRoot, Path.GetDirectoryName(project)!, Path.GetFileName(project));
            foreach (var pattern in new[] { "*.cs", "*.csproj" })
                files.AddRange(Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories));
        }
        var relative = files
            .Select(f => Path.GetRelativePath(repoRoot, f).Replace('\\', '/'))
            .Where(p => !p.Contains("/bin/", StringComparison.Ordinal) && !p.Contains("/obj/", StringComparison.Ordinal))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
        using var stream = new MemoryStream();
        stream.Write(Encoding.UTF8.GetBytes(SourceHashTag + "\0"));
        foreach (var path in relative)
        {
            var bytes = NormalizeCrLf(File.ReadAllBytes(Path.Combine(repoRoot, path)));
            stream.Write(Encoding.UTF8.GetBytes(
                path.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + path));
            stream.Write(Encoding.UTF8.GetBytes(
                bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":"));
            stream.Write(bytes);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    internal static byte[] NormalizeCrLf(byte[] source)
    {
        // 顺序扫描 CRLF→LF；独立 LF/CR 原样保留（与 python bytes.replace 语义一致）
        var output = new List<byte>(source.Length);
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] == (byte)'\r' && i + 1 < source.Length && source[i + 1] == (byte)'\n')
            {
                output.Add((byte)'\n');
                i++;
            }
            else
            {
                output.Add(source[i]);
            }
        }
        return output.ToArray();
    }

    // ---- Verify（只读；test runtime 无 re-Seal / 写回路径）----------------

    /// <summary>验证单个场景文件，返回违规清单（空 = 通过）。</summary>
    internal static IReadOnlyList<string> VerifyFile(string scenarioJsonPath, string repoRoot)
    {
        var violations = new List<string>();
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(scenarioJsonPath));
        }
        catch (JsonException e)
        {
            return new[] { $"{Path.GetFileName(scenarioJsonPath)}: JSON 解析失败（{e.Message}）" };
        }
        using var _ = document;
        var root = document.RootElement;

        if (!root.TryGetProperty("expectations", out var expectations))
            return new[] { $"{Path.GetFileName(scenarioJsonPath)}: 缺 expectations 块" };
        ExpectationsSnapshot snapshot;
        try
        {
            snapshot = ParseExpectations(expectations);
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return new[] { $"{Path.GetFileName(scenarioJsonPath)}: expectations 块字段缺失/类型异常（{e.Message}）" };
        }

        if (!root.TryGetProperty("certification", out var certification))
            return new[] { $"{Path.GetFileName(scenarioJsonPath)}: 缺 certification 块（SIM-002 G2：经 tools/scenario-certify.py --change <致因change> 认证）" };

        var block = new CertificationBlock(
            SchemaVersion: certification.TryGetProperty("schemaVersion", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0,
            ExpectationsDigest: certification.TryGetProperty("expectationsDigest", out var d) ? d.GetString() ?? "" : "",
            ExecutionDigest: certification.TryGetProperty("executionDigest", out var x) ? x.GetString() : null,
            RuntimeSourceHash: certification.TryGetProperty("runtimeSourceHash", out var h) ? h.GetString() ?? "" : "",
            CertifiedByChange: certification.TryGetProperty("certifiedByChange", out var c) ? c.GetString() ?? "" : "",
            CertifiedAt: certification.TryGetProperty("certifiedAt", out var a) ? a.GetString() ?? "" : "");

        if (block.SchemaVersion != BlockSchemaVersion)
            violations.Add(
                $"{Path.GetFileName(scenarioJsonPath)}: certification.schemaVersion 须为 {BlockSchemaVersion}"
                + $"（实际 {block.SchemaVersion}；v1 已废弃——经 tools/scenario_certify.py --change SIM-003 迁移）");
        if (block.CertifiedByChange.Length == 0)
            violations.Add($"{Path.GetFileName(scenarioJsonPath)}: certification.certifiedByChange 缺失（C8：期望改动必须搭乘致因 change）");
        if (block.CertifiedAt.Length == 0)
            violations.Add($"{Path.GetFileName(scenarioJsonPath)}: certification.certifiedAt 缺失");

        var recomputed = DigestOf(snapshot);
        if (block.ExpectationsDigest != recomputed)
            violations.Add(
                $"{Path.GetFileName(scenarioJsonPath)}: expectations 摘要不匹配——期望值在认证后被改动且未重认证"
                + $"（certified={block.ExpectationsDigest[..Math.Min(12, block.ExpectationsDigest.Length)]}"
                + $" actual={recomputed[..12]}…；经 tools/scenario_certify.py --change <致因change> 重认证）");

        // SIM-003 G3/G2：execution 绑定 + executionDigest 钉扎
        if (!root.TryGetProperty("execution", out var execution) || execution.ValueKind != JsonValueKind.Object)
        {
            violations.Add($"{Path.GetFileName(scenarioJsonPath)}: 缺 execution 块（SIM-003 G3：execution.kind/carrier/options）");
        }
        else
        {
            ScenarioCertification.ExecutionSnapshot executionSnapshot;
            try
            {
                executionSnapshot = ParseExecution(execution);
            }
            catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException)
            {
                violations.Add($"{Path.GetFileName(scenarioJsonPath)}: execution 块字段缺失/类型异常（{e.Message}）");
                return violations;
            }
            if (executionSnapshot.Kind is not ("golden-bundle" or "none"))
                violations.Add($"{Path.GetFileName(scenarioJsonPath)}: execution.kind 非法 '{executionSnapshot.Kind}'（legal: golden-bundle|none）");
            else if (executionSnapshot.Kind == "golden-bundle" && string.IsNullOrEmpty(executionSnapshot.Carrier))
                violations.Add($"{Path.GetFileName(scenarioJsonPath)}: execution.kind=golden-bundle 须携带非空 carrier");
            else if (executionSnapshot.Kind == "none" && executionSnapshot.Carrier is not null)
                violations.Add($"{Path.GetFileName(scenarioJsonPath)}: execution.kind=none 不得携带 carrier");

            var recomputedExecution = DigestOfExecution(executionSnapshot);
            if (block.ExecutionDigest is null)
                violations.Add($"{Path.GetFileName(scenarioJsonPath)}: certification.executionDigest 缺失（SIM-003 G2 v2）");
            else if (block.ExecutionDigest != recomputedExecution)
                violations.Add(
                    $"{Path.GetFileName(scenarioJsonPath)}: execution 摘要不匹配——执行绑定在认证后被改动且未重认证"
                    + $"（certified={block.ExecutionDigest[..Math.Min(12, block.ExecutionDigest.Length)]}"
                    + $" actual={recomputedExecution[..12]}…；carrier/options 变更必须搭乘致因 change 重认证）");
        }

        var sourceHash = RuntimeSourceHash(repoRoot);
        if (block.RuntimeSourceHash.Length == 0)
            violations.Add($"{Path.GetFileName(scenarioJsonPath)}: certification.runtimeSourceHash 缺失");
        else if (block.RuntimeSourceHash != sourceHash)
            violations.Add(
                $"{Path.GetFileName(scenarioJsonPath)}: 运行时源码哈希不匹配——Kernel/Agent 源在认证后变更"
                + $"（certified={block.RuntimeSourceHash[..Math.Min(12, block.RuntimeSourceHash.Length)]}"
                + $" actual={sourceHash[..12]}…；经 tools/scenario_certify.py --change <致因change> 重认证；"
                + "期望值是否需要随动由该 change 评审裁决）");

        return violations;
    }

    /// <summary>验证整个场景库；返回 (违规清单, 文件数)。</summary>
    internal static (IReadOnlyList<string> Violations, int Files) VerifyLibrary(string scenariosDir, string repoRoot)
    {
        var violations = new List<string>();
        var files = Directory.EnumerateFiles(scenariosDir, "SCN-*.json").OrderBy(p => p, StringComparer.Ordinal).ToList();
        foreach (var file in files)
            violations.AddRange(VerifyFile(file, repoRoot));
        return (violations, files.Count);
    }

    internal static ExpectationsSnapshot ParseExpectations(JsonElement expectations) => new(
        Status: expectations.GetProperty("status").GetString()!,
        Classification: expectations.TryGetProperty("classification", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null,
        Effects: expectations.GetProperty("effects").GetInt32(),
        AgentConsultations: expectations.GetProperty("agentConsultations").GetInt32(),
        UnconsumedStimuli: expectations.GetProperty("unconsumedStimuli").GetInt32(),
        GoalSatisfaction: expectations.TryGetProperty("goalSatisfaction", out var g) && g.ValueKind == JsonValueKind.String ? g.GetString() : null);

    /// <summary>
    /// execution 块解析（fail-closed）。options 渲染为 key 升序的
    /// (key, "true"/"false"/字符串值) 对——与 cert-exec-v1 canonical 及
    /// python 侧 _render_option_value 逐字节一致。
    /// </summary>
    internal static ExecutionSnapshot ParseExecution(JsonElement execution)
    {
        var kind = execution.GetProperty("kind").GetString()!;
        string? carrier = execution.TryGetProperty("carrier", out var carrierElement)
            && carrierElement.ValueKind == JsonValueKind.String
            ? carrierElement.GetString()
            : null;
        var options = new List<(string Key, string Value)>();
        if (execution.TryGetProperty("options", out var optionsElement)
            && optionsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in optionsElement.EnumerateObject())
            {
                var rendered = property.Value.ValueKind switch
                {
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.String => property.Value.GetString()!,
                    JsonValueKind.Number => property.Value.GetRawText(),
                    _ => throw new InvalidOperationException($"option {property.Name} 类型异常: {property.Value.ValueKind}"),
                };
                options.Add((property.Name, rendered));
            }
        }
        options.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
        return new ExecutionSnapshot(kind, carrier, options);
    }

    /// <summary>执行快照 → RunOptions 投影（已知 options 全集；未知 key 由 schema 层拒绝）。</summary>
    internal static bool TryRunOptions(ExecutionSnapshot execution, out RunOptions options)
    {
        options = new RunOptions();
        foreach (var (key, value) in execution.Options)
        {
            switch (key)
            {
                case "duplicateActivation":
                    options = options with { DuplicateActivation = value == "true" };
                    break;
                case "phased":
                    options = options with { Phased = value == "true" };
                    break;
                default:
                    return false;
            }
        }
        return true;
    }

    /// <summary>反向投影：ScenarioExpectation → 期望快照（tripwire 用——同一 canonical 面）。</summary>
    internal static ExpectationsSnapshot SnapshotOf(ScenarioExpectation expectation) => new(
        expectation.ExpectedStatus,
        expectation.ExpectedClassification,
        expectation.ExpectedEffects,
        expectation.ExpectedAgentConsultations,
        expectation.ExpectedUnconsumedStimuli,
        expectation.ExpectedGoalSatisfaction);
}
