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
/// journal / 自驱司机）+ 仿真外部（帧源 / 投递驱动 / 咨询）。全部经
/// 抽象缝组合；./runs/&lt;runid&gt;/ 落 journal + trace + facts，按终局退出。
/// </summary>
public sealed class HostRunner
{
    public sealed record HostRunResult(
        string RunDir,
        RunDriveStatus Status,
        string? Reason,
        string? OutcomeClassification,
        int DeliveredEffects,
        IReadOnlyList<string> ReceiptOutcomes,
        string FactsDigest,
        long JournalBytes);

    public static HostRunResult RunOnce(string runRoot)
    {
        ArgumentNullException.ThrowIfNull(runRoot);
        Directory.CreateDirectory(runRoot);
        var runDir = Path.Combine(runRoot, $"run-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}");
        Directory.CreateDirectory(runDir);
        var journalPath = Path.Combine(runDir, "exec.journal");

        // ---- 组合根：全部经抽象缝（利用抽象能力构建完整流程）----------
        var clock = new V0Runtime.VirtualClock();
        var scope = new HashSet<string>
        {
            ProductAssociationStrategy.ScreenIdentitySubject,
            "screen.frame",
            "switch.state",
        };
        var world = new WorldModel(
            scope,
            new ProductAssociationStrategy(),
            new V0Runtime.FrameOccurrenceStrategy());
        var assurance = new RuntimeAssurance(
            new ProductFreshnessEvaluator(() => clock.Now, TimeSpan.FromMinutes(5)));
        var delivery = new DeterministicDeliveryDriver(() => clock.Now);
        // journal 必注入（D3）：产品路径不存在「未注入执行源」的默认
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
                NextInput = new V0Runtime.FrameFeed(clock).Next,
                ConsultAgent = V0Runtime.Consult,
            });

        // ---- 单次 run ---------------------------------------------------
        var admission = kernel.AdmitContract(new ExecutionContract(
            Version: "v0",
            Objective: "flip-switch",
            Scope: scope,
            AllowedEffects: new HashSet<string> { "tap" },
            ForbiddenEffects: new HashSet<string>(),
            ProofCriteria: new[] { "switch-state-on" },
            // 显式义务（criteria 占位不可判定——RunModel 语义）：post-action
            // 世界 claim switch.state=on 即兑现 → Completion
            Obligations: new[]
            {
                new RunObligation(
                    "obj-switch-on", RunObligationKind.Objective,
                    Subject: "switch.state", RequiredValue: "on", Mandatory: true),
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

    /// <summary>v0 确定性投递驱动：记录 DispatchRequest、零外部副作用（dev-profile 件）。</summary>
    private sealed class DeterministicDeliveryDriver(Func<DateTimeOffset> clock) : IEffectDriver
    {
        private readonly List<DispatchRequest> _requests = new();

        public IReadOnlyList<DispatchRequest> Requests => _requests;

        public DispatchResult Deliver(DispatchRequest request)
        {
            _requests.Add(request);
            return new DispatchResult(DispatchOutcome.DeliveryCompleted, "v0-deterministic-delivery", clock());
        }
    }
}
