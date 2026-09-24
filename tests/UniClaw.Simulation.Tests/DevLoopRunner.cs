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

namespace UniClaw.Simulation.Tests;

/// <summary>
/// SIM-002 G1：Product Host 剥离仿真档后的 dev/test 组合根。与产品
/// HostRunner 装配**同一 Kernel 真件**（双 Host 对称 C2：EvidenceLedger /
/// WorldModel / RunModel / ControlLoop / RuntimeAssurance / EffectBoundary /
/// UniKernel / KernelRunDriver 全真实），只有外部缝是仿真 double：
/// 帧源（V0 / Replay / ServiceReplay）、咨询（V0Runtime.Consult）、
/// 投递（DeterministicEffectDriver；半真档注入 AdbLiveEffectDriver）。
/// 承载原 HostTests / HostReplayTests / HostServiceReplayTests /
/// HostLiveEffectTests 的端到端断言（含 journal/trace/facts 落盘与 digest）。
/// </summary>
internal static class DevLoopRunner
{
    internal sealed record DevRunResult(
        string RunDir,
        RunDriveStatus Status,
        string? Reason,
        string? OutcomeClassification,
        int DeliveredEffects,
        IReadOnlyList<string> ReceiptOutcomes,
        string FactsDigest,
        long JournalBytes);

    /// <summary>帧源工厂：注入共享虚拟时钟（freshness / provenance / 投递
    /// 时间戳同源）；Owner 非空时由 runner 负责 Dispose。</summary>
    internal delegate (Func<ObservationDirective, RunDriverInput?> Next, IDisposable? Owner)
        MakeFeed(V0Runtime.VirtualClock clock);

    internal static DevRunResult RunOnce(
        string runRoot,
        MakeFeed makeFeed,
        string targetState = "on",
        Func<AgentDecisionContext, AgentDecision?>? consult = null,
        Func<V0Runtime.VirtualClock, IEffectDriver>? makeDriver = null,
        string runName = "dev")
    {
        ArgumentNullException.ThrowIfNull(runRoot);
        ArgumentNullException.ThrowIfNull(makeFeed);
        consult ??= context => V0Runtime.Consult(context, targetState);
        Directory.CreateDirectory(runRoot);
        var runDir = Path.Combine(runRoot, $"run-{runName}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}");
        Directory.CreateDirectory(runDir);
        var journalPath = Path.Combine(runDir, "exec.journal");

        // ---- 组合（镜像原 HostRunner：Kernel 真件 + 仿真外部缝）----------
        var clock = new V0Runtime.VirtualClock();
        var scope = new HashSet<string>
        {
            ProductAssociationStrategy.ScreenIdentitySubject,
            SharedSubjects.Frame,
            SharedSubjects.State("switch"),
        };
        var (nextInput, feedOwner) = makeFeed(clock);
        try
        {
            var world = new WorldModel(
                scope,
                new ProductAssociationStrategy(),
                new V0Runtime.FrameOccurrenceStrategy());
            var assurance = new RuntimeAssurance(
                new ProductFreshnessEvaluator(() => clock.Now, TimeSpan.FromMinutes(5)));
            // journal 必注入（D3）：产品路径不存在「未注入执行源」的默认（两档同律）
            //（SIM-003 verify 期最小修复：using 保证 journal 句柄在 RunOnce 返回前
            // 关闭——否则 Windows 上临时目录清理 Directory.Delete 因文件占用失败；
            // 既有缺陷，与期望绑定无关，Linux 检出不触发）
            using var journal = new FileExecutionJournal(journalPath);
            IEffectDriver delivery = makeDriver is not null
                ? makeDriver(clock)
                : new DeterministicEffectDriver();
            var effectBoundary = new EffectBoundary(delivery, journal);
            var metrics = new RuntimeStageMetrics();
            var planPolicy = new AgentPlanPolicy();
            var traceScope = RunTraceFactory.BeginRun(new RunCorrelation("sim:dev-loop"));
            var kernel = new UniKernel(
                new EvidenceLedger(), world, traceScope.Trace,
                new RunModel(), new ControlLoop(planPolicy), assurance, effectBoundary, metrics);
            var driver = new KernelRunDriver(
                kernel, planPolicy,
                new RunDriverInputs
                {
                    NextInput = nextInput,
                    ConsultAgent = consult,
                });

            var admission = kernel.AdmitContract(new ExecutionContract(
                Version: "v0",
                Objective: "flip-switch",
                Scope: scope,
                AllowedEffects: new HashSet<string> { "tap" },
                ForbiddenEffects: new HashSet<string>(),
                ProofCriteria: new[] { "switch-state-on" },
                Obligations: new[]
                {
                    new RunObligation(
                        "obj-switch-state", RunObligationKind.Objective,
                        Subject: SharedSubjects.State("switch"), RequiredValue: targetState, Mandatory: true),
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

            // ---- 产物落盘（承载原 Host 端到端断言）-------------------------
            var artifact = traceScope.FinalizeArtifact();
            File.WriteAllText(Path.Combine(runDir, "trace.json"), TryJson(artifact));

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
                journalBytes = new FileInfo(journalPath).Length,
            };
            var factsJson = JsonSerializer.Serialize(facts, JsonOptions);
            File.WriteAllText(Path.Combine(runDir, "facts.json"), factsJson);
            var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"{facts.status}|{facts.outcome}|{facts.delivered}|{string.Join(",", receipts)}|{facts.journalBytes}")));

            return new DevRunResult(
                runDir, drive.Status, drive.Reason, facts.outcome, facts.delivered,
                receipts, digest, facts.journalBytes);
        }
        finally
        {
            feedOwner?.Dispose();
        }
    }

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static string TryJson<T>(T value)
    {
        try { return JsonSerializer.Serialize(value, JsonOptions); }
        catch (Exception) { return value?.ToString() ?? "<null>"; }
    }
}
