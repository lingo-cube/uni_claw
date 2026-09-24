using System.Text.Json;

namespace UniClaw.Simulation.Tests;

/// <summary>投影拒绝（fail-closed；不得静默兜底）。</summary>
internal sealed class ExpectationProjectionException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>
/// SIM-003 G5：certified JSON → executable expectation 的**唯一**投影入口。
///
/// expectations 的 authoring truth 只有一个：scenarios/SCN-*.json。本类把
/// certified expectations 投影为 ScenarioExpectation（不创建第三套 DTO——
/// 解析复用 ScenarioCertification.ExpectationsSnapshot，值对象复用
/// SimContract.ScenarioExpectation）。
///
/// 校验链（全部 fail-closed）：
///  1. 场景文件存在且可解析；
///  2. certification.schemaVersion == 2（v1 废弃）；
///  3. expectationsDigest 自洽（改期望未重认证 → 拒绝投影）；
///  4. executionDigest 自洽（改 carrier/options 未重认证 → 拒绝投影）；
///  5. 期望值 enum 可解析（RunDriveStatus / TerminalClassification / GoalSatisfaction）。
///
/// 刻意不校验 runtimeSourceHash：源码哈希的执法面在认证层
/// （ScenarioCertificationTests / scenario_certify.py --check / coverage），
/// 且逐次投影哈希全部 Kernel+Agent 源不可接受。
/// </summary>
internal static class ScenarioExpectations
{
    /// <summary>投影 certified expectations（scenariosDir 可注入供测试/temp 副本用）。</summary>
    public static ScenarioExpectation Load(string scenarioId, string? scenariosDirectory = null)
    {
        var verified = Verify(scenarioId, scenariosDirectory);
        return Project(verified.Expectations);
    }

    internal sealed record VerifiedScenario(
        string ScenarioId,
        ScenarioCertification.ExpectationsSnapshot Expectations,
        ScenarioCertification.ExecutionSnapshot Execution,
        ScenarioCertification.CertificationBlock Certification,
        string Path);

    /// <summary>定位 + 解析 + v2 认证自洽校验（expectations 与 execution 双 digest）。</summary>
    internal static VerifiedScenario Verify(string scenarioId, string? scenariosDirectory = null)
    {
        var directory = scenariosDirectory
            ?? Path.Combine(GoldenPaths.RepoRoot(), "scenarios");
        var path = Path.Combine(directory, scenarioId + ".json");
        if (!File.Exists(path))
            throw new ExpectationProjectionException($"场景条目不存在: {path}");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException e)
        {
            throw new ExpectationProjectionException($"{scenarioId}: JSON 解析失败（{e.Message}）", e);
        }
        using var _ = document;
        var root = document.RootElement;

        ScenarioCertification.ExpectationsSnapshot expectations;
        ScenarioCertification.ExecutionSnapshot execution;
        ScenarioCertification.CertificationBlock certification;
        try
        {
            if (!root.TryGetProperty("expectations", out var expectationsElement))
                throw new ExpectationProjectionException($"{scenarioId}: 缺 expectations 块");
            expectations = ScenarioCertification.ParseExpectations(expectationsElement);

            if (!root.TryGetProperty("execution", out var executionElement)
                || executionElement.ValueKind != JsonValueKind.Object)
                throw new ExpectationProjectionException($"{scenarioId}: 缺 execution 块（SIM-003 G3）");
            execution = ScenarioCertification.ParseExecution(executionElement);

            if (!root.TryGetProperty("certification", out var certificationElement)
                || certificationElement.ValueKind != JsonValueKind.Object)
                throw new ExpectationProjectionException($"{scenarioId}: 缺 certification 块");
            certification = new ScenarioCertification.CertificationBlock(
                SchemaVersion: certificationElement.TryGetProperty("schemaVersion", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0,
                ExpectationsDigest: certificationElement.TryGetProperty("expectationsDigest", out var d) ? d.GetString() ?? "" : "",
                ExecutionDigest: certificationElement.TryGetProperty("executionDigest", out var x) ? x.GetString() : null,
                RuntimeSourceHash: certificationElement.TryGetProperty("runtimeSourceHash", out var h) ? h.GetString() ?? "" : "",
                CertifiedByChange: certificationElement.TryGetProperty("certifiedByChange", out var c) ? c.GetString() ?? "" : "",
                CertifiedAt: certificationElement.TryGetProperty("certifiedAt", out var a) ? a.GetString() ?? "" : "");
        }
        catch (ExpectationProjectionException)
        {
            throw;
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new ExpectationProjectionException($"{scenarioId}: 块字段缺失/类型异常（{e.Message}）", e);
        }

        if (certification.SchemaVersion != ScenarioCertification.BlockSchemaVersion)
            throw new ExpectationProjectionException(
                $"{scenarioId}: certification.schemaVersion 须为 {ScenarioCertification.BlockSchemaVersion}"
                + $"（实际 {certification.SchemaVersion}；v1 已废弃——经 tools/scenario_certify.py --change SIM-003 迁移）");

        var actual = ScenarioCertification.DigestOf(expectations);
        if (certification.ExpectationsDigest != actual)
            throw new ExpectationProjectionException(
                $"{scenarioId}: expectations 摘要不匹配——期望值在认证后被改动且未重认证"
                + $"（certified={certification.ExpectationsDigest[..Math.Min(12, certification.ExpectationsDigest.Length)]}"
                + $" actual={actual[..12]}…；经 tools/scenario_certify.py --change <致因change> 重认证）");

        if (certification.ExecutionDigest is null)
            throw new ExpectationProjectionException($"{scenarioId}: certification.executionDigest 缺失（SIM-003 G2 v2）");
        var actualExecution = ScenarioCertification.DigestOfExecution(execution);
        if (certification.ExecutionDigest != actualExecution)
            throw new ExpectationProjectionException(
                $"{scenarioId}: execution 摘要不匹配——执行绑定在认证后被改动且未重认证"
                + $"（certified={certification.ExecutionDigest[..Math.Min(12, certification.ExecutionDigest.Length)]}"
                + $" actual={actualExecution[..12]}…；carrier/options 变更必须搭乘致因 change 重认证）");

        return new VerifiedScenario(scenarioId, expectations, execution, certification, path);
    }

    /// <summary>快照 → ScenarioExpectation（1:1 映射；enum 可解析性 fail-closed）。</summary>
    internal static ScenarioExpectation Project(ScenarioCertification.ExpectationsSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Status)
            || !Enum.TryParse<UniClaw.Kernel.Runtime.RunDriveStatus>(snapshot.Status, out _))
            throw new ExpectationProjectionException($"status '{snapshot.Status}' 不是合法 RunDriveStatus 名");
        if (snapshot.Classification is { } classification
            && !Enum.TryParse<UniClaw.Kernel.Run.TerminalClassification>(classification, out _))
            throw new ExpectationProjectionException($"classification '{classification}' 不是合法 TerminalClassification 名");
        if (snapshot.GoalSatisfaction is { } satisfaction
            && !Enum.TryParse<UniClaw.Agent.Evaluation.GoalSatisfaction>(satisfaction, out _))
            throw new ExpectationProjectionException($"goalSatisfaction '{satisfaction}' 不是合法 GoalSatisfaction 名");
        return new ScenarioExpectation(
            snapshot.Status,
            snapshot.Classification,
            snapshot.Effects,
            snapshot.AgentConsultations,
            snapshot.UnconsumedStimuli,
            snapshot.GoalSatisfaction);
    }
}
