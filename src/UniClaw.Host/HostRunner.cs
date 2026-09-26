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
    public sealed record HostOptions(
        string? DeviceId = null,
        // CSC-001 Slice B：viewport 魔数默认（1080×2400）已删除——
        // null = 不配置（dispatch 前 wm size 实测，实测不到 fail-closed）；
        // 显式值 = 已验证配置（优先级低于设备实况）。
        int? ViewportWidth = null,
        int? ViewportHeight = null,
        // PER-014 R3：TargetState 词汇 on/off → checked/unchecked（typed
        // semantic checked 值域；legacy *.state 仅剩 egress）。
        string TargetState = "checked",
        // PER-014 R5 / PER-012 M-08：legacy `*.state` 回滚旗（默认关）。关 =
        // 正常 typed run 零 XML role-state 发射（ConflictResolver legacy 路径
        // 休眠）；开 = MapTargetStateClaim legacy 路由恢复，run 如实标记
        // legacy/degraded（facts.legacyEgress）。wifi 探针 writer 不受此旗
        // 影响（单一 producer egress，零生产读者）。
        bool LegacyStateEgress = false,
        LivePerception.LiveAssets? Live = null,
        Func<AgentDecisionContext, AgentDecision?>? ConsultAgent = null);

    public sealed record HostRunResult(
        string RunDir,
        RunDriveStatus Status,
        string? Reason,
        string? OutcomeClassification,
        int DeliveredEffects,
        IReadOnlyList<string> ReceiptOutcomes,
        string FactsDigest,
        long JournalBytes);

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
        Directory.CreateDirectory(runRoot);
        var runDir = Path.Combine(runRoot, $"run-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}");
        Directory.CreateDirectory(runDir);
        var journalPath = Path.Combine(runDir, "exec.journal");

        // ---- 组合根：全部经抽象缝（利用抽象能力构建完整流程）----------
        var clock = new HostUtilities.VirtualClock();
        var scope = new HashSet<string>
        {
            ProductAssociationStrategy.ScreenIdentitySubject,
            SharedSubjects.Frame,
            SharedSubjects.State("switch"),
        };
        var liveFeed = new LivePerception.LiveFrameFeed(
            clock, options.Live, () => LivePerception.LiveFrameFeed.ReadWifiState(options.Live.DeviceId),
            legacyStateEgress: options.LegacyStateEgress);
        try
        {
            var world = new WorldModel(
                scope,
                new ProductAssociationStrategy(),
                new ScreenFrameOccurrenceStrategy());
            var assurance = new RuntimeAssurance(
                new ProductFreshnessEvaluator(() => clock.Now, TimeSpan.FromMinutes(5)));
            // journal 必注入（D3）：产品路径不存在「未注入执行源」的默认
            IEffectDriver delivery = new AdbLiveEffectDriver(
                options.DeviceId ?? "emulator-5554",
                options.ViewportWidth,
                options.ViewportHeight,
                adbExecutable: "adb",
                clock: () => clock.Now);
            var effectBoundary = new EffectBoundary(delivery, new FileExecutionJournal(journalPath));
            var metrics = new RuntimeStageMetrics();
            var planPolicy = new AgentPlanPolicy();
            var traceScope = RunTraceFactory.BeginRun(new RunCorrelation("host:v0-flip-switch"));
            var kernel = new UniKernel(
                new EvidenceLedger(), world, traceScope.Trace,
                new RunModel(), new ControlLoop(planPolicy), assurance, effectBoundary, metrics);
            var driver = new KernelRunDriver(
                kernel, planPolicy,
                new RunDriverInputs
                {
                    NextInput = liveFeed.Next,
                    ConsultAgent = options.ConsultAgent,
                });

            // ---- 单次 run ---------------------------------------------------
            var admission = kernel.AdmitContract(new ExecutionContract(
                Version: "v0",
                Objective: "flip-switch",
                Scope: scope,
                AllowedEffects: new HashSet<string> { "tap" },
                ForbiddenEffects: new HashSet<string>(),
                ProofCriteria: new[] { "switch-state-checked" },
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
                        EntityScope: new TargetDescriptor("switch")),
                }));
            if (!admission.Accepted)
                throw new InvalidOperationException($"contract rejected: {admission.RejectionReason}");

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

            var receipts = kernel.EffectReceipts
                .Select(r => r.Outcome.ToString())
                .ToArray();
            var facts = new
            {
                status = drive.Status.ToString(),
                reason = drive.Reason,
                outcome = drive.Outcome?.Classification.ToString(),
                delivered = drive.DeliveredEffects,
                receipts,
                terminal = kernel.IsRunTerminal,
                // PER-014 R5 / M-08：LegacyStateEgress 旗开启且实际发射过
                // legacy role-state → run 标记 legacy/degraded（如实留档）。
                legacyEgress = liveFeed.LegacyEgressObserved,
                journalBytes = new FileInfo(journalPath).Length,
            };
            var factsJson = JsonSerializer.Serialize(facts, JsonOptions);
            WriteText(runDir, "facts.json", factsJson);
            var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"{facts.status}|{facts.outcome}|{facts.delivered}|{string.Join(",", receipts)}|{facts.journalBytes}")));

            return new HostRunResult(
                runDir, drive.Status, drive.Reason, facts.outcome, facts.delivered,
                receipts, digest, facts.journalBytes);
        }
        finally
        {
            liveFeed.Dispose();
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
