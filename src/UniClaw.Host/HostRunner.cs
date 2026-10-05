using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Diagnostics;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Effects.ExecutionSource;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using UniClaw.Host.SettingsCoverage;

namespace UniClaw.Host;

/// <summary>
/// HOST-001 — Product Host 最小 composition root（spec v0.3）。单次
/// headless run：真实核心（产品 association / freshness / 执行边界 /
/// journal / 自驱驱动器）+ 真实外部（live 感知 / 真机 ADB 投递 / 显式
/// 注入的 agent 咨询）。全部经抽象缝组合；./runs/&lt;runid&gt;/ 落
/// journal + trace + facts，按终局退出。
/// SIM-002 G1：无仿真默认路径——帧源与咨询两缝均 fail-closed（未显式
/// 提供即抛）；仿真/回放/半真档经测试侧 dev-loop 组合根（Simulation
/// Host）承载（双 Host 对称：同一 Kernel 真件）。
/// </summary>
public sealed class HostRunner
{
    /// <summary>Host-neutral launch identity supplied by the owning Runtime.
    /// HostRunner carries and verifies this context; it does not mint or infer
    /// Product identity from a DSH session or a filesystem path.</summary>
    public sealed record LaunchContext(
        string RunId,
        string ProductSessionId,
        string LaunchId,
        string IdempotencyKey,
        string CorrelationId);

    public sealed record HostOptions(
        string? DeviceId = null,
        // CSC-001 Slice B：viewport 魔数默认（1080×2400）已删除——
        // null = 不配置（dispatch 前 wm size 实测，实测不到 fail-closed）；
        // 显式值 = 已验证配置（优先级低于设备实况）。
        int? ViewportWidth = null,
        int? ViewportHeight = null,
        // TargetState 使用 typed checked/unchecked 词汇。
        string TargetState = "checked",
        string TargetSemanticDescriptor = "Wi-Fi",
        LivePerception.LiveAssets? Live = null,
        Func<AgentDecisionContext, AgentDecision?>? ConsultAgent = null,
        bool SettingsTraversal = false,
        LaunchContext? Launch = null,
        SettingsActionPolicy? SettingsActionPolicy = null);

    public sealed record HostRunResult(
        string RunDir,
        RunDriveStatus Status,
        string? Reason,
        string? OutcomeClassification,
        int DeliveredEffects,
        IReadOnlyList<string> ReceiptOutcomes,
        string FactsDigest,
        long JournalBytes,
        string? RunId = null,
        string? ProductSessionId = null,
        string? LaunchId = null,
        string? IdempotencyKey = null,
        string? CorrelationId = null,
        string? ActionPolicyDigest = null);

    public static HostRunResult RunOnce(string runRoot, HostOptions? options = null)
    {
        options ??= new HostOptions();
        ArgumentNullException.ThrowIfNull(runRoot);
        // SIM-002 G1 fail-closed：产品组合根不再内置仿真档——外部缝必须
        // 显式提供（隐藏模拟路径违反 simulation baseline C4 反向闭包）
        if (options.Live is null)
            throw new InvalidOperationException(
                "Product Host 无仿真/回放帧源（SIM-002 G1）：须显式提供 Live 感知资产；"
                + "仿真/回放/半真档由 Simulation Host（测试侧 dev-loop 组合根）承载");
        if (options.ConsultAgent is null)
            throw new InvalidOperationException(
                "Product Host 无内置仿真咨询（SIM-002 G1）：须显式注入 ConsultAgent；"
                + "确定性单步咨询 double 随 dev 档住在 Simulation Host（测试侧）");
        if (options.Launch is { } launch)
        {
            if (string.IsNullOrWhiteSpace(launch.RunId)
                || string.IsNullOrWhiteSpace(launch.ProductSessionId)
                || string.IsNullOrWhiteSpace(launch.LaunchId)
                || string.IsNullOrWhiteSpace(launch.IdempotencyKey)
                || string.IsNullOrWhiteSpace(launch.CorrelationId))
                throw new InvalidOperationException("launch-context-incomplete: RunId, ProductSessionId, LaunchId, IdempotencyKey and CorrelationId are required");
        }
        Directory.CreateDirectory(runRoot);
        var runDir = Path.Combine(runRoot, $"run-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}");
        Directory.CreateDirectory(runDir);
        var journalPath = Path.Combine(runDir, "exec.journal");

        // ---- 组合根：全部经抽象缝（利用抽象能力构建完整流程）----------
        var clock = new HostUtilities.VirtualClock();
        var scope = new HashSet<string>
        {
            ProductAssociationStrategy.ScreenIdentitySubject,
            ProductAssociationStrategy.ScreenRouteSubject,
            SharedSubjects.Frame,
            "ui.node.*",
        };
        LivePerception.LiveFrameFeed? liveFeed = null;
        SettingsTraversalLiveFeed? settingsFeed = null;
        Func<ObservationDirective, RunDriverInput?> nextInput;
        IUiObservationStrategy observationStrategy;
        if (options.SettingsTraversal)
        {
            // AGT-011 §4：SettingsTraversal 模式证据持久化（AGT-008 缺省全开）。
            settingsFeed = new SettingsTraversalLiveFeed(
                clock, options.Live, Path.Combine(runDir, "evidence"));
            nextInput = settingsFeed.Next;
            observationStrategy = new UiHierarchyOccurrenceStrategy();
        }
        else
        {
            liveFeed = new LivePerception.LiveFrameFeed(
                clock, options.Live, () => LivePerception.LiveFrameFeed.ReadWifiState(options.Live.DeviceId));
            nextInput = liveFeed.Next;
            observationStrategy = new ScreenFrameOccurrenceStrategy();
        }
        try
        {
            var world = new WorldModel(
                scope,
                new ProductAssociationStrategy(),
                observationStrategy);
            var assurance = new RuntimeAssurance(
                new ProductFreshnessEvaluator(() => clock.Now, TimeSpan.FromMinutes(5)));
            // journal 必注入（D3）：产品路径不存在「未注入执行源」的默认
            var deviceId = options.DeviceId
                ?? throw new InvalidOperationException("ENVIRONMENT_UNAVAILABLE: HostOptions.DeviceId 是必需的。");
            IEffectDriver delivery = new AdbLiveEffectDriver(
                deviceId,
                options.ViewportWidth,
                options.ViewportHeight,
                adbExecutable: "adb",
                clock: () => clock.Now);
            var effectBoundary = new EffectBoundary(delivery, new FileExecutionJournal(journalPath));
            var metrics = new RuntimeStageMetrics();
            var planPolicy = new AgentPlanPolicy();
            var traceScope = RunTraceFactory.BeginRun(new RunCorrelation(options.Launch?.RunId ?? "host:v0-flip-switch"));
            var ledger = new EvidenceLedger();
            var policyGuardResults = new List<SettingsActionGuardResult>();
            var kernel = new UniKernel(
                ledger, world, traceScope.Trace,
                new RunModel(), new ControlLoop(planPolicy), assurance, effectBoundary, metrics);
            var driver = new KernelRunDriver(
                kernel, planPolicy,
                new RunDriverInputs
                {
                    NextInput = nextInput,
                    ConsultAgent = context =>
                    {
                        var policy = options.SettingsActionPolicy;
                        var projected = policy is null
                            ? context
                            : context with
                            {
                                Objective = context.Objective
                                    + "\n\nSETTINGS ACTION POLICY (read-only; Runtime Guard is authoritative): "
                                    + policy.AgentProjection(),
                            };
                        var decision = options.ConsultAgent(projected);
                        if (policy is null)
                            return decision;
                        var guarded = SettingsActionGuard.GuardDecision(
                            decision, projected, policy, out var guardResult);
                        if (guardResult is not null)
                            policyGuardResults.Add(guardResult);
                        return guarded;
                    },
                });

            // ---- 单次 run ---------------------------------------------------
            var admission = kernel.AdmitContract(new ExecutionContract(
                Version: "v0",
                Objective: options.SettingsTraversal
                    ? "Confirm the Wi-Fi state in Android Settings and leave Wi-Fi enabled. Choose every next control only from the current semantic hierarchy; navigate as needed to reach the Wi-Fi control, and never toggle it when the observed state is already enabled."
                    : "flip-switch",
                Scope: scope,
                AllowedEffects: new HashSet<string> { "tap" },
                ProofCriteria: new[] { "switch-state-checked" },
                ForbiddenEffects: options.SettingsActionPolicy is { } policy
                    ? new HashSet<string>(policy.ForbiddenActionClasses, StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.Ordinal),
                // 显式义务（PER-014 R3 typed 迁移）：role-scoped semantic
                // checked requirement——Subject 不再是 legacy state subject，
                // RequiredValue 为 typed checked 词汇（checked/unchecked）；
                // 满足判定经 RuntimeAssurance → R1 SemanticCheckedResolver
                // 缝（CheckedState 相等；Unknown/Unsupported ≠ satisfied）。
                Obligations: new[]
                {
                    new RunObligation(
                        "obj-switch-checked", RunObligationKind.Objective,
                        Subject: "ui.role.switch.checked",
                        RequiredValue: options.TargetState, Mandatory: true,
                        EntityScope: new TargetDescriptor(
                            "switch", options.SettingsTraversal ? options.TargetSemanticDescriptor : null)),
                }), options.Launch?.RunId);
            if (!admission.Accepted)
                throw new InvalidOperationException($"contract rejected: {admission.RejectionReason}");
            if (options.Launch is { } supplied && !string.Equals(kernel.RunId, supplied.RunId, StringComparison.Ordinal))
                throw new InvalidOperationException($"launch-run-mismatch: supplied Runtime RunId '{supplied.RunId}' does not match Kernel canonical RunId '{kernel.RunId}'");

            driver.Activate();
            RunDriveResult drive = default!;
            for (var i = 0; i < 16; i++)
            {
                drive = driver.Drive();
                if (drive.Status is not RunDriveStatus.WaitingForInput)
                    break;
            }

            // ---- 产物落盘 ---------------------------------------------------
            var artifact = traceScope.FinalizeArtifact();
            WriteText(runDir, "trace.json", TryJson(artifact));
            if (settingsFeed is not null)
                WriteText(runDir, "settings-trace.json", TryJson(settingsFeed.Trace));

            var receipts = kernel.EffectReceipts
                .Select(r => r.Outcome.ToString())
                .ToArray();
            var facts = new
            {
                schemaVersion = "uniclaw.host-facts.v2",
                runDir,
                runId = kernel.RunId,
                correlationId = options.Launch?.CorrelationId,
                status = drive.Status.ToString(),
                reason = drive.Reason,
                outcome = drive.Outcome?.Classification.ToString(),
                delivered = drive.DeliveredEffects,
                receipts,
                terminal = kernel.IsRunTerminal,
                journalBytes = new FileInfo(journalPath).Length,
                diagnostics = new
                {
                    nextFiles = new[] { "facts.json", "trace.json", "settings-trace.json", "exec.journal" },
                    guidance = drive.Status == RunDriveStatus.Completed
                        ? "运行完成；如需核对动作，先看 facts.completedSteps，再用 DecisionId 对照 trace.json。"
                        : "先看 facts.reason 和 policyGuard，再用 DecisionId 对照 trace.json 与 exec.journal。"
                },
                completionAnchors = driver.PendingCompletionDossier?.Results
                    .Select(r => new { anchor = r.Anchor, verified = r.Verified })
                    .ToArray(),
                completedSteps = driver.CompletedSteps
                    .Select(s => new { decision = s.DecisionN, step = s.StepIndex, receipt = s.ReceiptId })
                    .ToArray(),
                actionPolicy = options.SettingsActionPolicy is { } factsPolicy
                    ? new { factsPolicy.PolicyRef, factsPolicy.Digest, factsPolicy.SourcePath }
                    : null,
                policyGuard = policyGuardResults,
            };
            var factsJson = JsonSerializer.Serialize(facts, JsonOptions);
            WriteText(runDir, "facts.json", factsJson);
            var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"{facts.status}|{facts.outcome}|{facts.delivered}|{string.Join(",", receipts)}|{facts.journalBytes}")));

            return new HostRunResult(
                runDir, drive.Status, drive.Reason, facts.outcome, facts.delivered,
                receipts, digest, facts.journalBytes,
                kernel.RunId,
                options.Launch?.ProductSessionId,
                options.Launch?.LaunchId,
                options.Launch?.IdempotencyKey,
                options.Launch?.CorrelationId,
                options.SettingsActionPolicy?.Digest);
        }
        finally
        {
            settingsFeed?.Dispose();
            liveFeed?.Dispose();
        }
    }

    public static int ExitCode(RunDriveStatus status) => status switch
    {
        RunDriveStatus.Completed => 0,
        RunDriveStatus.TerminalNotProven => 3,
        RunDriveStatus.WaitingForInput => 4,
        _ => 1,
    };

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static string TryJson<T>(T value)
    {
        try { return JsonSerializer.Serialize(value, JsonOptions); }
        catch (Exception) { return value?.ToString() ?? "<null>"; }
    }

    private static void WriteText(string dir, string name, string content) =>
        File.WriteAllText(Path.Combine(dir, name), content);
}
