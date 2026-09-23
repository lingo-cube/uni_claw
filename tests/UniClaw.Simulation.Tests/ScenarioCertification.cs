using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// SIM-002 G2（S2）：golden 认证持久化——场景库条目的期望值快照经
/// 认证记录钉扎（certification block），test runtime 只允许 Verify。
///
/// 认证绑定三元组（C8 golden 期望更新协议的机械执法面）：
///  1. expectationsDigest——expectations 块 canonical rendering 的 SHA-256；
///  2. runtimeSourceHash——认证时的 Kernel+Agent **源码**状态哈希
///     （源码而非 DLL：DLL 哈希依赖构建环境不可跨机复现；源码哈希
///     内容寻址、python/C# 双侧可独立重算）；
///  3. certifiedByChange——致因 change 引用（无记录的期望改动 = 认证
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
///  源码哈希（cert-src-v1）:
///    输入 = src/UniClaw.Kernel 与 src/UniClaw.Agent 下全部 *.cs / *.csproj
///    （排除 bin/obj；相对 POSIX 路径排序），逐文件
///    "{pathLen}:{path}{contentLen}:" + 原始字节
///    digest = sha256(utf8("cert-src-v1\x00") ++ 各文件帧) 小写 hex
/// </summary>
internal static class ScenarioCertification
{
    internal const string ExpectationsSchemaTag = "cert-exp-v1";
    internal const string SourceHashTag = "cert-src-v1";
    internal const int BlockSchemaVersion = 1;

    /// <summary>期望值快照（JSON expectations 块的解析面）。</summary>
    internal sealed record ExpectationsSnapshot(
        string Status,
        string? Classification,
        int Effects,
        int AgentConsultations,
        int UnconsumedStimuli,
        string? GoalSatisfaction);

    internal sealed record CertificationBlock(
        int SchemaVersion,
        string ExpectationsDigest,
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

    /// <summary>Kernel+Agent 源码状态哈希（认证时的运行时源）。</summary>
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
            var bytes = File.ReadAllBytes(Path.Combine(repoRoot, path));
            stream.Write(Encoding.UTF8.GetBytes(
                path.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + path));
            stream.Write(Encoding.UTF8.GetBytes(
                bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":"));
            stream.Write(bytes);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
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
            RuntimeSourceHash: certification.TryGetProperty("runtimeSourceHash", out var h) ? h.GetString() ?? "" : "",
            CertifiedByChange: certification.TryGetProperty("certifiedByChange", out var c) ? c.GetString() ?? "" : "",
            CertifiedAt: certification.TryGetProperty("certifiedAt", out var a) ? a.GetString() ?? "" : "");

        if (block.SchemaVersion != BlockSchemaVersion)
            violations.Add($"{Path.GetFileName(scenarioJsonPath)}: certification.schemaVersion 须为 {BlockSchemaVersion}（实际 {block.SchemaVersion}）");
        if (block.CertifiedByChange.Length == 0)
            violations.Add($"{Path.GetFileName(scenarioJsonPath)}: certification.certifiedByChange 缺失（C8：期望改动必须搭乘致因 change）");
        if (block.CertifiedAt.Length == 0)
            violations.Add($"{Path.GetFileName(scenarioJsonPath)}: certification.certifiedAt 缺失");

        var recomputed = DigestOf(snapshot);
        if (block.ExpectationsDigest != recomputed)
            violations.Add(
                $"{Path.GetFileName(scenarioJsonPath)}: expectations 摘要不匹配——期望值在认证后被改动且未重认证"
                + $"（certified={block.ExpectationsDigest[..Math.Min(12, block.ExpectationsDigest.Length)]}"
                + $" actual={recomputed[..12]}…；经 tools/scenario-certify.py --change <致因change> 重认证）");

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
}
