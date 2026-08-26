// Tier B 验证宿主（Phase 2.5）— 真实 Android Emulator 上的 UniAgent Emulator
// Validation 入口。组合生产管线（CanonicalVisionHostFactory 管理的 UDS 感知
// host + AdbScreenshotSource + AdbDispatchTarget + Fixture 语义绑定），用已
// 验收的 ValidationHarness EmulatorDriver 驱动 run.strategy.start，并把
// ResultCollector / BoundaryVerifier / Gates 组装为 Tier B 报告。
//
// 用法：dotnet run --project src/UniClaw.Runtime.ValidationHarness -- tierb <s1>
// 环境前提：emulator-5554 已启动、com.uniclaw.fixture 已安装并处于 CAPSTONE 根页。
using System.Text.Json;
using UniClaw.Runtime.Adapters;
using UniClaw.Runtime.Adapters.Device;
using UniClaw.Runtime.Adapters.Operator;
using UniClaw.Runtime.Adapters.Perception;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.ValidationHarness.Emulator;
using UniClaw.Runtime.ValidationHarness.Fixtures;
using UniClaw.Runtime.ValidationHarness.Results;
using UniClaw.Runtime.ValidationHarness.Fixtures;
using UniClaw.Runtime.ValidationHarness.Hosting;
using UniClaw.Vision.Host;

namespace UniClaw.Runtime.ValidationHarness;

public static class TierBProgram
{
    private const string App = "com.uniclaw.fixture";
    private const string AdbPath = "/opt/homebrew/share/android-commandlinetools/platform-tools/adb";
    private const string Serial = "emulator-5554";
    private const string RootPage = "Fixture Root";

    public static async Task<int> Main(string[] args)
    {
        if (args.Length < 2 || args[0] != "tierb" || (args[1] != "s1" && args[1] != "s2" && args[1] != "s3"))
        {
            Console.Error.WriteLine("usage: tierb s1   (S2/S3 follow after S1 evidence review)");
            return 2;
        }

        var repoRoot = FindRepoRoot();
        var receipt = Path.Combine(repoRoot, "platforms/perception/governance/artifacts/current-active-identity.json");
        var python = Path.Combine(repoRoot, ".venv-local-vision/bin/python");

        using var vision = CanonicalVisionHostFactory.Create(receipt, pythonExecutable: python, repoRoot: repoRoot);
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var anomaly = args.Contains("--anomaly-nav");
        await vision.StartAsync(cts.Token);
        await Console.Error.WriteLineAsync($"[tierb] vision host {vision.State} at {vision.SocketPath}");
        var runStartedAt = DateTimeOffset.UtcNow;

        // 真实环境（生产管线，仅测试侧语义绑定包裹 — 与 Capstone 真机测试同构）。
        // VISION-ONLY composition (B1, mirrors the graduated capstone test
        // pipeline EXACTLY): no structuredUiSource. A prior remediation
        // iteration added the ADB hierarchy channel "for normalization" —
        // that DEVIATED from the graduated semantics: the auxiliary tier's
        // fallback classification (clickable LinearLayout row →
        // NavigationCandidate) introduced dual-tier nav occurrences whose
        // overlap ambiguity the normalizer correctly refuses (fail-closed).
        // The graduated pipeline is vision-only; the harness realigns to it.
        var raw = new PhysicalEnvironment(
            new AdbScreenshotSource(Serial, AdbPath),
            new LocalVisionPerceptionSource(vision.SocketPath),
            new AdbDispatchTarget(Serial, AdbPath),
            App, 1080, 1920);
        var environment = new FixtureSemanticEnvironment(raw, ContextAwareRoleClassifier);

        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(
            environment, App, ResolveSemanticPage,
            launchIntentAction: "com.uniclaw.fixture.action.CAPSTONE");
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(
            startup, traversal,
            ct => environment.ObserveAsync(ct),
            ResolveSemanticPage,
            page => new RuntimeContainer(
                page,
                o => string.Equals(ResolveSemanticPage(o), page, StringComparison.Ordinal),
                traversal.ExecuteStep),
            recovery);

        var graph = new RunExecutionGraph(agent, environment);
        RunGraphFactory factory = selector => graph;

        var compiler = new StrategyContractCompiler([new RealityFixtureStrategyBinding()]);
        using var host = new TierAHost(factory, compiler);
        var driver = new EmulatorDriver(new LoopbackEmulatorTransport(host.BoundPort));

        // 与 Tier A S1 同形的合法 directive（closed vocabulary，零禁载内容）。
        var directive = DirectiveFixtureCatalog.BuildLegalDirective(
            "tierb-s1-real-emulator", maximumDepth: 1,
            application: App, semanticRoot: RootPage);
        var record = new DirectiveFixtureRecord(
            "Explore the real fixture scope and record everything reachable (Tier B goal).",
            directive,
            FixtureComposition.FixtureDeviceText);

        // S2 anomaly (environment-side, never an Emulator Run-internal call):
        // a HOME-key press lands the device on the launcher mid-run — an
        // external world event the Runtime must dispose of autonomously.
        if (anomaly)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(6), cts.Token);
                try
                {
                    // Verifiable external-world anomaly: force-stop removes the
                    // fixture from the foreground entirely (a real crash/exit
                    // class event). The launch intent is NEVER re-sent — the
                    // Runtime must dispose of the aftermath autonomously.
                    var psi = new System.Diagnostics.ProcessStartInfo(
                        AdbPath, $"-s {Serial} shell am force-stop {App}")
                    { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                    using var proc = System.Diagnostics.Process.Start(psi);
                    await proc!.WaitForExitAsync(cts.Token);
                    await Console.Error.WriteLineAsync("[tierb] anomaly injected: fixture force-stop (external world event)");
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { await Console.Error.WriteLineAsync($"[tierb] anomaly injection failed: {ex.Message}"); }
            });
        }

        if (args[1] == "s3")
        {
            var d1 = await driver.StartAsync(record with { Directive = DirectiveFixtureCatalog.BuildLegalDirective(
                "tierb-s3-run1", 1, App, RootPage) }, cts.Token);
            var t1 = d1 as DriverDispatchResult.Transported;
            var c1 = new ResultCollector(new WireReadSurface(host.BoundPort), t1!.Admission);
            var r1 = await c1.CollectAsync(cts.Token);
            await Console.Error.WriteLineAsync($"[tierb] run1 terminal: {r1.Terminal.TerminalState.Value} events={r1.Lifecycle.Events.Value.Length}");

            // Harness-local adaptation: a Result-1 fact (event count) is
            // embedded in Run 2's strategy identity — closed vocabulary, the
            // payload diff is the strategyId ONLY, no Runtime state touched.
            var fact = $"evh3-{r1.Lifecycle.Events.Value.Length}-events";
            var d2 = await driver.StartAsync(record with { Directive = DirectiveFixtureCatalog.BuildLegalDirective(
                $"tierb-s3-run2-derived-{fact}", 1, App, RootPage) }, cts.Token);
            var t2 = d2 as DriverDispatchResult.Transported;
            var c2 = new ResultCollector(new WireReadSurface(host.BoundPort), t2!.Admission);
            var r2 = await c2.CollectAsync(cts.Token);
            await Console.Error.WriteLineAsync($"[tierb] run2 terminal: {r2.Terminal.TerminalState.Value} events={r2.Lifecycle.Events.Value.Length}");
            var s3Outcome = new
            {
                tier = "B", scenario = "s3",
                run1 = new { runId = t1.Admission.RunId, terminal = r1.Terminal.TerminalState.Value.ToString(), events = r1.Lifecycle.Events.Value.Select(e => e.Kind).ToArray() },
                run2 = new { runId = t2.Admission.RunId, terminal = r2.Terminal.TerminalState.Value.ToString(), events = r2.Lifecycle.Events.Value.Select(e => e.Kind).ToArray() },
                adaptationFact = fact,
                distinctRunIds = !string.Equals(t1.Admission.RunId, t2.Admission.RunId, StringComparison.Ordinal),
                emulatorCalls = driver.CallLog.Entries.Select(e => new { e.Method, e.Outcome, e.Detail }).ToArray(),
            };
            Console.WriteLine(JsonSerializer.Serialize(s3Outcome, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        await Console.Error.WriteLineAsync($"[tierb] dispatching at +{(DateTimeOffset.UtcNow - runStartedAt).TotalSeconds:F1}s");
        var dispatch = await driver.StartAsync(record, cts.Token);
        await Console.Error.WriteLineAsync($"[tierb] dispatch returned at +{(DateTimeOffset.UtcNow - runStartedAt).TotalSeconds:F1}s");
        var transported = dispatch as DriverDispatchResult.Transported;
        var admission = transported?.Admission;

        var collector = new ResultCollector(
            new WireReadSurface(host.BoundPort), admission ?? RejectedView(dispatch));
        var result = await collector.CollectAsync(cts.Token);

        // ── SCENARIO ACCEPTANCE (independent of Runtime completion) ────────
        // RUNTIME_COMPLETED != VALIDATION_SCENARIO_PASS: the harness reads the
        // fixture's OWN external state (uiautomator — test-time device-state
        // collection only, never a Runtime input) and requires the scenario's
        // full coverage marker for a scenario PASS verdict.
        string? scenarioCoverage = null;
        var scenarioPass = false;
        string? scenarioFailReason = null;
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(AdbPath, $"-s {Serial} shell uiautomator dump /sdcard/tierb.xml")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            using var dump = System.Diagnostics.Process.Start(psi);
            dump!.WaitForExit(20000);
            var cat = new System.Diagnostics.ProcessStartInfo(AdbPath, $"-s {Serial} shell cat /sdcard/tierb.xml")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            using var catProc = System.Diagnostics.Process.Start(cat);
            var xml = catProc!.StandardOutput.ReadToEnd();
            var visitedMatch = System.Text.RegularExpressions.Regex.Match(
                xml, @"Visited\s*(\d+)\s*/\s*(\d+)");
            if (visitedMatch.Success)
            {
                var visited = int.Parse(visitedMatch.Groups[1].Value);
                var total = int.Parse(visitedMatch.Groups[2].Value);
                scenarioCoverage = $"{visited}/{total}";
                scenarioPass = visited >= total;
                if (!scenarioPass)
                    scenarioFailReason = $"FAIL_INSUFFICIENT_SCENARIO_COVERAGE: fixture external state Visited {visited}/{total}; the scenario requires {total}/{total}.";
            }
            else
            {
                scenarioCoverage = "unavailable";
                scenarioPass = false;
                scenarioFailReason = "FAIL_SCENARIO_STATE_UNREADABLE: fixture Visited state line not found in the post-run device dump.";
            }
        }
        catch (Exception ex)
        {
            scenarioCoverage = "unavailable";
            scenarioFailReason = $"FAIL_SCENARIO_STATE_UNREADABLE: {ex.Message}";
        }

        var outcome = new
        {
            tier = "B",
            scenario = args[1],
            scenarioAcceptance = new
            {
                requiredCoverage = "8/8 (fixture SharedState — the scenario's own claimed coverage)",
                observedCoverage = scenarioCoverage,
                scenarioPass,
                failReason = scenarioFailReason,
                semantics = "RUNTIME_COMPLETED != VALIDATION_SCENARIO_PASS; a Completed terminal without 8/8 fixture state is FAIL_INSUFFICIENT_SCENARIO_COVERAGE.",
            },
            expected = "Directive admitted; Runtime explores the real emulator autonomously; evidence-backed terminal; wire tier reports coverage truthfully unavailable.",
            admission = new
            {
                accepted = admission?.Accepted,
                runId = admission?.RunId,
                rejection = admission?.RejectionCode is null ? null : new { code = admission.RejectionCode, reason = admission.RejectionReason },
            },
            emulatorCalls = driver.CallLog.Entries.Select(e => new { e.Method, e.Outcome, e.Detail }).ToArray(),
            terminal = new
            {
                state = result.Terminal.TerminalState.Value.ToString(),
                reason = result.Terminal.TerminalReason.Value,
                classification = result.Terminal.TerminalState.Classification.ToString(),
            },
            coverage = new
            {
                availability = result.Coverage.Availability.Value,
                ledgerClassified = result.Coverage.Ledger.Classification.ToString(),
                note = "wire tier: ledger-level coverage truthfully unavailable (never fabricated)",
            },
            events = result.Lifecycle.Events.Value.IsDefault
                ? []
                : result.Lifecycle.Events.Value.Select(e => e.Kind).ToArray(),
            snapshotDiagnostics = result.Snapshot.Diagnostics.Value.IsDefault
                ? []
                : result.Snapshot.Diagnostics.Value.ToArray(),
        };

        Console.WriteLine(JsonSerializer.Serialize(outcome, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static StrategyRunAdmissionView RejectedView(DriverDispatchResult dispatch)
        => new(false, null, null, "DISPATCH_FAILED", dispatch.ToString());

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "AGENTS.md")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? ".";
    }

    // ── 语义解析与角色分类：与 Capstone 真机测试同一套 vision-only 规则 ──

    private static string? ResolveSemanticPage(Observation observation)
    {
        var childTitles = observation.Elements
            .Where(e => e.Text is not null && e.Text.StartsWith("Child ", StringComparison.Ordinal))
            .Select(e => e.Text!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var hasVisited = observation.Elements.Any(e =>
            e.Text is not null && e.Text.Contains("Visited", StringComparison.Ordinal));
        var hasRootTitle = observation.Elements.Any(e =>
            string.Equals(e.Text, RootPage, StringComparison.Ordinal));
        if (hasVisited || childTitles.Length > 1 || (hasRootTitle && childTitles.Length == 0))
            return RootPage;
        if (childTitles.Length == 1)
            return childTitles[0];
        return hasRootTitle ? RootPage : null;
    }

    // Page-aware role classification (goal-evaluation alignment): the
    // "Fixture Root" text is the parent-return control ONLY on child pages —
    // on the root page it is the title (null role; classified non-interactive
    // upstream) and must never block root-completeness as an unresolved
    // parent-return affordance (root has no parent by definition).
    private static FixtureSemanticRole? ContextAwareRoleClassifier(
        UniClaw.Runtime.Model.Observation observation, ObservedElement element, int index)
    {
        var text = element.Text;
        if (string.IsNullOrWhiteSpace(text))
            return FixtureSemanticRole.NonInteractive; // empty OCR element is never an interaction target

        // Page context (mirrors the graduated capstone classifier semantics):
        // a child page carries exactly ONE distinct "Child NN" title — that
        // title is page decoration (NonInteractive), NOT a navigation
        // candidate; classifying it as nav would duplicate the nav signature
        // inside one frame (dialog title + row) and fail-closed the
        // normalization. The root page's "Child NN" rows ARE nav candidates.
        var childTitles = observation.Elements
            .Where(e => e.Text is not null && e.Text.StartsWith("Child ", StringComparison.Ordinal))
            .Select(e => e.Text!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var hasVisited = observation.Elements.Any(e =>
            e.Text is not null && e.Text.Contains("Visited", StringComparison.Ordinal));
        var isChildPage = childTitles.Length == 1 && !hasVisited;

        // Truncated "Child" OCR cannot ground to a branch — non-interactive
        // decoration, never a nav candidate (signature-stability rule).
        var isTruncatedChild = text.StartsWith("Child", StringComparison.Ordinal)
            && !System.Text.RegularExpressions.Regex.IsMatch(text, @"^Child \d{2}$");
        if (isTruncatedChild)
            return FixtureSemanticRole.NonInteractive;
        if (text.StartsWith("Child ", StringComparison.Ordinal))
            return isChildPage
                ? FixtureSemanticRole.NonInteractive // the child page's own title
                : FixtureSemanticRole.NavigationCandidate; // a root row
        if (!string.Equals(text, RootPage, StringComparison.Ordinal))
            return FixtureSemanticRole.NonInteractive; // state line, dialog caption, OCR noise — explicit non-target (required for completeness)
        var page = RealityFixtureStrategyBinding.ResolvePage(observation);
        if (string.Equals(page, RootPage, StringComparison.Ordinal))
        {
            // ROOT-FRAME DISAMBIGUATION: the root page carries the "Fixture
            // Root" TITLE (not a control). But the popup child's dialog frame
            // may OCR with its "Child NN" title dropped, leaving the dialog's
            // "Fixture Root" BUTTON as the only root-labeled text — the page
            // resolver's fallback then misresolves it as the root page. The
            // popup caption token disambiguates: with an obstruction caption
            // present, the frame is the popup child and its button IS the
            // parent-return control (dismiss+finish per the fixture source).
            var hasObstructionCaption = observation.Elements.Any(e =>
                e.Text is not null
                && (e.Text.Contains("popup", StringComparison.OrdinalIgnoreCase)
                    || e.Text.Contains("dialog", StringComparison.OrdinalIgnoreCase)));
            return hasObstructionCaption
                ? FixtureSemanticRole.ParentReturnControl
                : FixtureSemanticRole.NonInteractive; // genuine root title
        }
        return FixtureSemanticRole.ParentReturnControl; // return button on child pages
    }
}
