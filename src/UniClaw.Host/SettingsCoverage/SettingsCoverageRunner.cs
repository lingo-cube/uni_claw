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
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Host.SettingsCoverage;

/// <summary>
/// AGT-005 — 可配置、有界、可追溯的 Settings 菜单覆盖遍历 runner。
/// 单 Kernel run + director 包装 ConsultAgent；bounds/coverage/targets/
/// termination 全部来自 SettingsCoverageConfig（不写死在逻辑中）。
/// 逐步 trace 经 NextInput 包装缝交错记账（receipts↔routes 按观察拉取点
/// 配对；receipts↔verifications 按派发序配对；DecisionId/Directive 按
/// (role,descriptor,effect) 元组 FIFO 归属——grounding 失败的采纳步骤
/// 不产生 receipt，不参与账本 executed 记账），产物 coverage-steps.json +
/// coverage-report.json + 既有 trace/facts/settings-trace。
/// </summary>
public sealed class SettingsCoverageRunner
{
    public sealed record Options(
        string DeviceId,
        LivePerception.LiveAssets Live,
        SettingsCoverageConfig Config,
        Func<AgentDecisionContext, AgentDecision?> UnderlyingConsult,
        int? ViewportWidth = null,
        int? ViewportHeight = null,
        Func<string?>? DshSessionIdAccessor = null,
        // 测试缝（确定性场景认证用）：默认 null = SettingsTraversalLiveFeed
        // + AdbLiveEffectDriver（真实档）。
        Func<ObservationDirective, RunDriverInput?>? FeedNext = null,
        Func<string?>? CurrentCaptureId = null,
        IEffectDriver? EffectDriver = null,
        // PER-019：UniPerception fetch 缝（透传 feed 异步流水；null = slow 关闭）。
        UniPerceptionPipeline.Fetch? SlowConsult = null);

    public sealed record StepArtifact(
        int Index,
        string DecisionId,
        string? DshSessionId,
        string? Directive,
        string TargetRole,
        string? TargetDescriptor,
        string EffectClass,
        string? DesiredState,
        bool PreActionTargetUnique,
        string? ReceiptId,
        string? ReceiptOutcome,
        string? ReceiptCommand,
        string? RouteBefore,
        string? RouteAfter,
        string? PostCaptureId,
        bool Verified,
        string? FailureReason,
        IReadOnlyList<KeyValuePair<string, bool>> Checks);

    public sealed record RunResult(
        string RunDir,
        RunDriveStatus Status,
        string? Reason,
        string? Outcome,
        CoverageReport Report,
        IReadOnlyList<StepArtifact> Steps,
        IReadOnlyList<SettingsCoverageDirector.ConsultRecord> Consults,
        IReadOnlyList<string> DshDiagnostics,
        string FactsDigest);

    public static RunResult Run(string runRoot, Options options)
    {
        ArgumentNullException.ThrowIfNull(runRoot);
        ArgumentNullException.ThrowIfNull(options);
        var config = options.Config;
        if (config.ActionPolicyRequired && config.ActionPolicy is null)
            throw new InvalidOperationException(
                "PROFILE_CONTRACT_NOT_READY: required Settings action policy is not loaded");
        Directory.CreateDirectory(runRoot);
        var runDir = Path.Combine(runRoot, $"run-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}");
        Directory.CreateDirectory(runDir);
        var journalPath = Path.Combine(runDir, "exec.journal");

        var clock = new HostUtilities.VirtualClock();
        var scope = new HashSet<string>
        {
            ProductAssociationStrategy.ScreenIdentitySubject,
            ProductAssociationStrategy.ScreenRouteSubject,
            ProductAssociationStrategy.PopupSubject, // AGT-009：弹窗 typed 声明
            SharedSubjects.Frame,
            "ui.node.*",
        };
        var ledger = new SettingsCoverageLedger();
        var evidenceSettings = config.EvidenceSettings;
        var evidenceDir = options.FeedNext is null
            ? Path.Combine(runDir, "evidence")
            : null;
        var liveFeed = options.FeedNext is null
            ? new SettingsTraversalLiveFeed(
                clock, options.Live, evidenceDir,
                evidenceSettings.PersistScreenshots, evidenceSettings.PersistHierarchies,
                coverageConfig: config, uniPerceptionFetch: options.SlowConsult)
            : null;
        if (liveFeed is not null)
            WriteText(runDir, "environment-preflight.json", TryJson(new
            {
                schemaVersion = "uniclaw.environment-preflight.v1",
                visionServiceWarmup = liveFeed.EnvironmentPreflight,
                formalFlowStartedAfterPreflight = true,
            }));
        var feedNext = options.FeedNext ?? liveFeed!.Next;
        var currentCaptureId = options.CurrentCaptureId
            ?? (liveFeed is not null ? () => liveFeed.Trace.Count > 0 ? liveFeed.Trace[^1].CaptureId : null : () => null);
        UniKernel? kernel = null;
        var journal = new CoverageStepJournal(
            ledger,
            () => kernel?.PostActionVerifications
                ?? (IReadOnlyList<PostActionEffectVerification>)Array.Empty<PostActionEffectVerification>(),
            options.DshSessionIdAccessor);
        var director = new SettingsCoverageDirector(
            options.UnderlyingConsult, ledger, config, journal);
        try
        {
            var world = new WorldModel(
                scope,
                new ProductAssociationStrategy(),
                new UiHierarchyOccurrenceStrategy());
            var assurance = new RuntimeAssurance(
                new ProductFreshnessEvaluator(() => clock.Now, TimeSpan.FromMinutes(5)));
            IEffectDriver delivery = options.EffectDriver ?? new AdbLiveEffectDriver(
                options.DeviceId,
                options.ViewportWidth,
                options.ViewportHeight,
                adbExecutable: "adb",
                clock: () => clock.Now);
            var effectBoundary = new EffectBoundary(delivery, new FileExecutionJournal(journalPath));
            var metrics = new RuntimeStageMetrics();
            var planPolicy = new AgentPlanPolicy();
            var traceScope = RunTraceFactory.BeginRun(new RunCorrelation("host:v0-settings-coverage"));
            var evidenceLedger = new EvidenceLedger();
            kernel = new UniKernel(
                evidenceLedger, world, traceScope.Trace,
                new RunModel(), new ControlLoop(planPolicy), assurance, effectBoundary, metrics);

            // ---- NextInput 包装缝：观察/回执交错记账 -----------------------
            var interleave = new InterleaveBookkeeper(kernel, feedNext, currentCaptureId, ledger, journal);
            director.SyncBeforeSnapshot = interleave.SyncNow;
            // AGT-009：feed 的有界 Slow 咨询投影目标 = 当前 run kernel。
            if (liveFeed is not null)
                liveFeed.KernelProvider = () => kernel;
            var driver = new KernelRunDriver(
                kernel, planPolicy,
                new RunDriverInputs
                {
                    NextInput = interleave.WrappedNext,
                    ConsultAgent = director.Consult,
                });

            var admission = kernel.AdmitContract(new ExecutionContract(
                Version: "v0",
                Objective: "Traverse the Android Settings menu for coverage per the configured plan: "
                    + "enter every configured first-level entry, discover and enter at least one "
                    + "scroll-only entry, verify back navigation and repeated entry, and end on the "
                    + "Settings root page. Choose every target only from the current semantic hierarchy.",
                Scope: scope,
                AllowedEffects: new HashSet<string>(StringComparer.Ordinal) { "tap", "swipe-up" },
                ForbiddenEffects: config.ActionPolicy is { } actionPolicy
                    ? new HashSet<string>(actionPolicy.ForbiddenActionClasses, StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.Ordinal),
                ProofCriteria: new[] { "settings-coverage" },
                Obligations: new[]
                {
                    new RunObligation(
                        "obj-end-on-root", RunObligationKind.Objective,
                        Subject: ProductAssociationStrategy.ScreenRouteSubject,
                        RequiredValue: config.RootRoute,
                        Mandatory: true),
                },
                MaxConsultations: config.Bounds.MaxConsultRounds,
                MaxTotalSteps: config.Bounds.MaxSteps));
            if (!admission.Accepted)
                throw new InvalidOperationException($"contract rejected: {admission.RejectionReason}");

            driver.Activate();
            RunDriveResult drive = default!;
            var driveCap = config.Bounds.MaxConsultRounds * 2 + 8;
            for (var i = 0; i < driveCap; i++)
            {
                drive = driver.Drive();
                if (drive.Status is not RunDriveStatus.WaitingForInput)
                    break;
            }
            interleave.FlushObservation();
            journal.Drain(director.AdoptedSteps);

            // ---- 产物落盘 ---------------------------------------------------
            WriteText(runDir, "trace.json", TryJson(traceScope.FinalizeArtifact()));
            if (liveFeed is not null)
                WriteText(runDir, "settings-trace.json", TryJson(liveFeed.Trace));

            var receipts = kernel.EffectReceipts;
            var steps = journal.Artifacts();
            var report = ledger.Report(config);
            WriteText(runDir, "coverage-steps.json", TryJson(steps));
            WriteText(runDir, "coverage-report.json", TryJson(report));

            var consultSummaries = director.ConsultLog.Select(c => new
            {
                c.DecisionId,
                c.Phase,
                c.Directive,
                c.DirectiveKind,
                c.DecisionKind,
                c.DeviationReason,
                c.Attempts,
                c.Justification,
                c.PolicyDigest,
                c.GuardVerdict,
            }).ToArray();
            var facts = new
            {
                schemaVersion = "uniclaw.settings-coverage-facts.v2",
                runDir,
                status = drive.Status.ToString(),
                reason = drive.Reason,
                outcome = drive.Outcome?.Classification.ToString(),
                delivered = drive.DeliveredEffects,
                receipts = receipts.Select(r => r.Outcome.ToString()).ToArray(),
                terminal = kernel.IsRunTerminal,
                coverageStatus = report.Status,
                coverageRate = report.CoverageRate,
                stepSuccessRate = report.StepSuccessRate,
                uncovered = report.UncoveredItems,
                firstDivergence = report.FirstDivergence,
                terminalJustification = director.TerminalJustification,
                diagnostics = new
                {
                    firstDivergence = report.FirstDivergence,
                    terminalJustification = director.TerminalJustification,
                    nextFiles = new[] { "facts.json", "environment-preflight.json", "coverage-report.json", "coverage-steps.json", "trace.json", "settings-trace.json", "exec.journal", "failure.json" },
                    guidance = report.FirstDivergence is null && drive.Status == RunDriveStatus.Completed
                        ? "运行完成；如需核对每一步，先看 coverage-steps.json，再用 DecisionId 对照 trace.json。"
                        : "先看 firstDivergence/reason，再用 DecisionId 对照 coverage-steps.json、trace.json 和 exec.journal。"
                },
                actionPolicy = config.ActionPolicy is { } policy
                    ? new { policy.PolicyRef, policy.Digest, policy.SourcePath }
                    : null,
                evidenceDir = evidenceDir,
                evidenceFiles = evidenceDir is not null && Directory.Exists(evidenceDir)
                    ? Directory.GetFiles(evidenceDir).Length
                    : 0,
                journalBytes = new FileInfo(journalPath).Length,
                consults = consultSummaries,
                steps = driver.CompletedSteps
                    .Select(s => new { decision = s.DecisionN, step = s.StepIndex, receipt = s.ReceiptId })
                    .ToArray(),
            };
            var factsJson = JsonSerializer.Serialize(facts, JsonOptions);
            WriteText(runDir, "facts.json", factsJson);
            var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"{facts.status}|{facts.coverageStatus}|{facts.delivered}|{string.Join(",", facts.receipts)}|{facts.journalBytes}")));

            return new RunResult(
                runDir, drive.Status, drive.Reason, facts.outcome, report, steps,
                director.ConsultLog,
                Array.Empty<string>(),
                digest);
        }
        catch (Exception error)
        {
            try
            {
                WriteText(runDir, "failure.json", JsonSerializer.Serialize(new
                {
                    schemaVersion = "uniclaw.settings-coverage-failure.v1",
                    runDir,
                    errorType = error.GetType().FullName,
                    message = error.Message,
                    guidance = "先看 message；再检查 environment-preflight.json、exec.journal、trace.json、settings-trace.json 和 coverage-report.json（若存在）。"
                }, JsonOptions));
            }
            catch
            {
                // Preserve the original failure if diagnostics cannot be written.
            }
            throw;
        }
        finally
        {
            liveFeed?.Dispose();
        }
    }

    /// <summary>
    /// 交错记账器：包装 feed.Next。每次观察拉取点把 (a) 上一周期 route+可见
    /// occurrences 记入账本、(b) 自上次拉取以来的新 receipts 与前后 route
    /// 配对后经 CoverageStepJournal 等待 Drain（咨询开始时携带 verification
    /// 与 DecisionId/Directive 归属写入账本）。
    /// </summary>
    private sealed class InterleaveBookkeeper
    {
        private readonly UniKernel _kernel;
        private readonly Func<ObservationDirective, RunDriverInput?> _feedNext;
        private readonly Func<string?> _currentCaptureId;
        private readonly SettingsCoverageLedger _ledger;
        private readonly CoverageStepJournal _journal;
        private string? _lastCycleRoute;
        private int _syncedReceipts;
        private bool _observedOnce;
        private bool _cycleRecorded;

        public InterleaveBookkeeper(
            UniKernel kernel,
            Func<ObservationDirective, RunDriverInput?> feedNext,
            Func<string?> currentCaptureId,
            SettingsCoverageLedger ledger,
            CoverageStepJournal journal)
        {
            _kernel = kernel;
            _feedNext = feedNext;
            _currentCaptureId = currentCaptureId;
            _ledger = ledger;
            _journal = journal;
        }

        public RunDriverInput? WrappedNext(ObservationDirective directive)
        {
            // (a) 上一周期已处理完毕：其 route + 当前 belief 的可见 occurrences
            // 入账本（occurrences 为该屏 grounded 景观）。
            RecordPendingObservation();

            var input = _feedNext(directive);

            // (b) 本周期输入的 route（该屏指纹）；期间新 receipts 与
            // (前 route=上一周期, 后 route=本周期) 配对入 journal。
            var routeNow = RouteOf(input);
            var captureNow = _currentCaptureId();
            // AGT-009：本周期弹窗声明入账本（director 快照分支依据）。
            var popupNow = PopupStateOf(input);
            if (popupNow is not null)
                _ledger.RecordPopupState(popupNow);
            var receipts = _kernel.EffectReceipts;
            for (var i = _syncedReceipts; i < receipts.Count; i++)
                _journal.Enqueue(receipts[i], _lastCycleRoute, routeNow, captureNow);
            _syncedReceipts = receipts.Count;
            _lastCycleRoute = routeNow;
            _observedOnce = true;
            _cycleRecorded = false;
            return input;
        }

        /// <summary>幂等补记当前周期（director 在快照前调用——首个观察的
        /// 处理完成先于首次咨询，但普通记账点在下一次拉取时才到）。</summary>
        public void SyncNow() => RecordPendingObservation();

        public void FlushObservation()
        {
            RecordPendingObservation();
            var receipts = _kernel.EffectReceipts;
            for (var i = _syncedReceipts; i < receipts.Count; i++)
                _journal.Enqueue(receipts[i], _lastCycleRoute, null, null);
            _syncedReceipts = receipts.Count;
        }

        private void RecordPendingObservation()
        {
            if (!_observedOnce || _lastCycleRoute is null || _cycleRecorded)
                return;
            _ledger.RecordObservation(
                _lastCycleRoute,
                _kernel.CurrentBelief?.Occurrences?
                    .Select(o => (o.Role, o.SemanticDescriptor, o.Native?.Value)).ToArray()
                ?? Array.Empty<(string, string?, string?)>());
            _cycleRecorded = true;
        }

        private static string? RouteOf(RunDriverInput? input) =>
            input is RunDriverInput.Observation observation
                ? observation.Proposals
                    .Where(p => p.Claim.Subject == ProductAssociationStrategy.ScreenRouteSubject)
                    .Select(p => p.Claim.Value)
                    .FirstOrDefault()
                : null;

        /// <summary>AGT-009：从本周期 batch 提取弹窗声明（无声明 → null）。</summary>
        private static string? PopupStateOf(RunDriverInput? input) =>
            input is RunDriverInput.Observation observation
                ? observation.Proposals
                    .Where(p => p.Claim.Subject == ProductAssociationStrategy.PopupSubject)
                    .Select(p => p.Claim.Value)
                    .FirstOrDefault()
                : null;
    }

    /// <summary>
    /// 步骤账本缝：Enqueue（wrapper）→ Drain（咨询开始/收尾）。Drain 把
    /// 每个已配对 route 的 receipt 与 (i) 按派发序对应的 verification、
    /// (ii) 按 (role,descriptor,effect) 元组 FIFO 匹配的已采纳咨询步骤
    /// 组装成 CoverageStepRecord 写入账本（运行中实时供 director 快照）。
    /// </summary>
    public sealed class CoverageStepJournal
    {
        private readonly SettingsCoverageLedger _ledger;
        private readonly Func<IReadOnlyList<PostActionEffectVerification>> _verifications;
        private readonly Func<string?>? _sessionIdAccessor;
        private readonly List<StepArtifact> _artifacts = new();
        private readonly List<(EffectReceipt Receipt, string? RouteBefore, string? RouteAfter, string? PostCaptureId)> _pending = new();
        private readonly List<(string DecisionId, string? Directive, AgentActionStep Step)> _attributionQueue = new();
        private int _consumedVerifications;
        private int _queuedAdoptedCount;

        public CoverageStepJournal(
            SettingsCoverageLedger ledger,
            Func<IReadOnlyList<PostActionEffectVerification>> verifications,
            Func<string?>? sessionIdAccessor)
        {
            _ledger = ledger;
            _verifications = verifications;
            _sessionIdAccessor = sessionIdAccessor;
        }

        public IReadOnlyList<StepArtifact> Artifacts() => _artifacts;

        public void Enqueue(
            EffectReceipt receipt, string? routeBefore, string? routeAfter, string? postCaptureId) =>
            _pending.Add((receipt, routeBefore, routeAfter, postCaptureId));

        public void Drain(IReadOnlyList<(string DecisionId, string? Directive, AgentActionStep Step)> adoptedSteps)
        {
            // 归属队列增量同步：新采纳步骤按序入队（消费式匹配需要移除）。
            while (_queuedAdoptedCount < adoptedSteps.Count)
            {
                _attributionQueue.Add(adoptedSteps[_queuedAdoptedCount]);
                _queuedAdoptedCount++;
            }
            if (_pending.Count == 0)
                return;
            var dshSession = _sessionIdAccessor?.Invoke();
            var verifications = _verifications();
            foreach (var fact in _pending)
            {
                var verification = _consumedVerifications < verifications.Count
                    ? verifications[_consumedVerifications]
                    : null;
                if (verification is not null)
                    _consumedVerifications++;
                var attribution = Attribute(fact.Receipt, verification);
                var checks = (verification?.Checks ?? Array.Empty<AssuranceCheck>())
                    .Select(c => new KeyValuePair<string, bool>(c.Name, c.Passed))
                    .ToArray();
                var artifact = new StepArtifact(
                    Index: _artifacts.Count,
                    DecisionId: attribution?.DecisionId ?? $"unattributed-{_artifacts.Count}",
                    DshSessionId: dshSession,
                    Directive: attribution?.Directive,
                    TargetRole: attribution?.Step.TargetRole
                        ?? verification?.Target.Role ?? "",
                    TargetDescriptor: attribution?.Step.TargetDescriptor
                        ?? verification?.Target.SemanticDescriptor,
                    EffectClass: attribution?.Step.EffectClass
                        ?? verification?.Target.EffectClass ?? "",
                    DesiredState: attribution?.Step.DesiredState
                        ?? verification?.Target.DesiredState?.ToString(),
                    PreActionTargetUnique: verification != null && verification.Checks.Any(
                        c => c.Name == "post-action-target-unique" && c.Passed),
                    ReceiptId: fact.Receipt.ReceiptId,
                    ReceiptOutcome: fact.Receipt.Outcome.ToString(),
                    ReceiptCommand: fact.Receipt.Report,
                    RouteBefore: fact.RouteBefore,
                    RouteAfter: fact.RouteAfter,
                    PostCaptureId: fact.PostCaptureId,
                    Verified: verification?.IsVerified == true,
                    FailureReason: verification?.RejectionReason ?? fact.Receipt.Reason,
                    Checks: checks);
                _artifacts.Add(artifact);
                _ledger.RecordStep(new CoverageStepRecord(
                    artifact.Index, artifact.DecisionId, artifact.DshSessionId,
                    artifact.TargetRole, artifact.TargetDescriptor, artifact.EffectClass,
                    artifact.DesiredState,
                    artifact.PreActionTargetUnique, artifact.ReceiptId, artifact.ReceiptOutcome,
                    artifact.ReceiptCommand,
                    artifact.RouteBefore, artifact.RouteAfter, artifact.PostCaptureId,
                    artifact.Verified, artifact.FailureReason, artifact.Directive));
            }
            _pending.Clear();
        }

        /// <summary>元组匹配：verification.Target 与归属队列按
        /// (role, descriptor, effectClass) 相等消费（匹配即移除——重复进入
        /// 同目标的第二次步骤归属到第二次咨询）；无 verification 的
        /// （Unconfirmed 提前终止）按队首归属。</summary>
        private (string DecisionId, string? Directive, AgentActionStep Step)? Attribute(
            EffectReceipt receipt,
            PostActionEffectVerification? verification)
        {
            if (verification is not null)
            {
                var index = _attributionQueue.FindIndex(candidate =>
                    candidate.Step.TargetRole == verification.Target.Role
                    && candidate.Step.TargetDescriptor == verification.Target.SemanticDescriptor
                    && candidate.Step.EffectClass == verification.Target.EffectClass);
                if (index < 0)
                    return null;
                var matched = _attributionQueue[index];
                _attributionQueue.RemoveAt(index);
                return matched;
            }
            if (_attributionQueue.Count == 0)
                return null;
            var head = _attributionQueue[0];
            _attributionQueue.RemoveAt(0);
            return head;
        }
    }

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
