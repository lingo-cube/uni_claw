using System.Diagnostics;
using System.Text.Json;
using SkiaSharp;
using UniClaw.Runtime.Adapters.Perception.Vision;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Perception;

/// <summary>
/// Parent task 4.2 integration proof — StateBeliefReducer end-to-end state
/// propagation through the REAL production chain (no manual candidate
/// injection, no Python oracle as authority, same-frame state extraction):
///
///   repo-owned reality fixture PNG
///   → REAL production Python perception pipeline (bridge emits candidates)
///   → canonical toggle candidate bounds (pixel, same frame)
///   → REAL ImageSwitchStateProvider (same PNG) → authoritative SwitchState
///   → production BindingAnalysis + BindingReconciler
///   → production StateBeliefReducer
///   → asserted truthful StateBelief (ON → true, OFF → false)
///
/// SEMANTIC MODELING (test-only, truthful): the fixture is the Android 15
/// Developer Options page; its real controls are modeled with semantic
/// identities that match the rendered text — NOT unrelated real-world objects.
///   ON:  "Use developer options (master)" → DeveloperOptionsMaster.Enabled
///   OFF: "Automatic system updates"        → AutomaticSystemUpdates.Enabled
/// These SemanticObject identities exist ONLY to verify production
/// Binding→StateBelief propagation through this test; they are NOT production
/// catalog entries and do NOT claim repository semantic registration.
///
/// Fixture: platforms/perception/tests/fixtures/reality/developer-options-falsification.png
///   (repo-owned, SHA-256 verified; groundtruth marks sw1/sw2 ON, sw3 OFF)
/// Bridge: bridge_emit_toggle_candidates.py (test-only; runs the production
///   perception pipeline and prints candidate JSON; no production mutation).
/// </summary>
public sealed class PerceptionToggleToStateBeliefIntegrationTests
{
    // Real controls rendered in the fixture (truthful semantic identities).
    private const string OnControlText = "Use developer options (master)";
    private const string OffControlText = "Automatic system updates";
    private const string OnSemanticIdentity = "DeveloperOptionsMaster";
    private const string OffSemanticIdentity = "AutomaticSystemUpdates";

    private const string FixtureName = "developer-options-falsification.png";

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "platforms", "perception")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Repository root not found from " + AppContext.BaseDirectory);
    }

    private static string FixturePath()
        => Path.Combine(RepoRoot(), "platforms", "perception", "tests", "fixtures", "reality", FixtureName);

    private static string BridgeScript()
        => Path.Combine(RepoRoot(), "tests", "UniClaw.Runtime.Tests", "Perception", "bridge_emit_toggle_candidates.py");

    /// <summary>Find a python3 executable, preferring the repo-local vision venv.</summary>
    private static string? FindPython()
    {
        var venv = Path.Combine(RepoRoot(), ".venv-local-vision", "bin", "python3");
        if (File.Exists(venv)) return venv;
        var pathEnv = System.Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, OperatingSystem.IsWindows() ? "python.exe" : "python3");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    /// <summary>
    /// Test-only bridge: run the REAL production perception pipeline against the
    /// fixture and read its toggle candidate JSON. This is the ONLY origin of
    /// candidate type/bounds — no manual candidate injection (parent §4).
    /// </summary>
    private static async Task<BridgeOutput> EmitCandidatesAsync()
    {
        var python = FindPython()
            ?? throw new InvalidOperationException("python3 not found; cannot run the perception bridge");
        var outFile = Path.Combine(Path.GetTempPath(), $"uniclaw-bridge-{Guid.NewGuid():N}.json");
        var psi = new ProcessStartInfo
        {
            FileName = python,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = RepoRoot(),
        };
        psi.ArgumentList.Add(BridgeScript());
        psi.ArgumentList.Add(FixturePath());
        psi.ArgumentList.Add(outFile);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("failed to start perception bridge");
        var stderrTask = process.StandardError.ReadToEndAsync();
        var exitTask = process.WaitForExitAsync();
        var completed = await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromSeconds(180)));
        if (completed != exitTask)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            throw new InvalidOperationException("perception bridge timed out after 180s");
        }
        await exitTask;
        var stderr = await stderrTask;
        if (process.ExitCode != 0 || !File.Exists(outFile))
        {
            throw new InvalidOperationException(
                $"bridge exited {process.ExitCode}, output missing\npython={python}\n" +
                $"script={BridgeScript()}\nfixture={FixturePath()}\nout={outFile}\nstderr:\n{stderr}");
        }
        var payload = await File.ReadAllTextAsync(outFile);
        try { File.Delete(outFile); } catch { /* best-effort */ }
        var parsed = JsonSerializer.Deserialize<BridgeOutput>(payload, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidOperationException($"bridge produced unparseable JSON\n{payload}");
        if (parsed.Candidates.Count == 0)
        {
            throw new InvalidOperationException($"bridge produced zero candidates\n{payload}");
        }
        return parsed;
    }

    private sealed class BridgeOutput
    {
        public string Fixture { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public List<BridgeCandidate> Candidates { get; set; } = [];
    }

    private sealed class BridgeCandidate
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public List<int> BoundsPx { get; set; } = [];
    }

    /// <summary>Normalize pixel bounds to [0,1] frame-relative ElementBounds.</summary>
    private static ElementBounds Normalized(int w, int h, List<int> px)
    {
        Assert.Equal(4, px.Count);
        return new ElementBounds(
            (float)px[0] / w,
            (float)px[1] / h,
            (float)px[2] / w,
            (float)px[3] / h);
    }

    /// <summary>Ground-truth label for a candidate region (fixture groundtruth).</summary>
    private static string GroundTruthLabel(List<int> px)
    {
        // sw1 [992,241,1044,273] "Use developer options (master)" — ON
        // sw2 [1012,636,1063,667] "Stay awake" — ON
        // sw3 [1012,1400,1063,1431] "Automatic system updates" — OFF
        var y = px[1];
        if (y < 500) return OnControlText;      // sw1 (y≈241)
        if (y < 1000) return "Stay awake";       // sw2 (y≈636)
        return OffControlText;                   // sw3 (y≈1400)
    }

    /// <summary>
    /// Regression guard: the test must never silently bind a real Developer
    /// Options control to an unrelated real-world semantic object (the prior
    /// WifiConnectivity mis-modeling). Semantic identities must stay aligned
    /// with the rendered text.
    /// </summary>
    private static void AssertTruthfulSemanticModeling(IEnumerable<SemanticObject> objects)
    {
        foreach (var obj in objects)
        {
            Assert.NotEqual("WifiConnectivity", obj.Identity);
            Assert.NotEqual("Bluetooth", obj.Identity);
        }
    }

    /// <summary>Build one test semantic object with an Enabled boolean dimension.</summary>
    private static SemanticObject TestSemanticObject(string identity)
        => new(identity, "DeveloperOptionsSetting", ["Enabled"]);

    /// <summary>Binding criteria anchoring one test object to its real control text.</summary>
    private static ElementBindingCriteria CriteriaFor(string identity, string controlText)
        => new(
            [TestSemanticObject(identity)],
            ImmutableDictionaryHelper.Of(identity, controlText),
            ImmutableDictionaryHelper.Of(identity, "toggle"));

    [Fact]
    public async Task RealPerceptionCandidates_ToStateBelief_OnAndOff_ThroughProductionChain()
    {
        // ── 1. Real Perception pipeline produces toggle candidates (same frame) ──
        var bridge = await EmitCandidatesAsync();

        // ── 2. Same-frame ImageSwitchStateProvider (production, authoritative) ──
        using var bitmap = SKBitmap.Decode(FixturePath());
        Assert.NotNull(bitmap);
        var provider = new ImageSwitchStateProvider(bitmap, bridge.Width, bridge.Height);

        var results = new List<(string Label, bool? State)>();
        foreach (var candidate in bridge.Candidates)
        {
            var bounds = Normalized(bridge.Width, bridge.Height, candidate.BoundsPx);
            var state = await provider.ReadAsync(bounds);
            results.Add((GroundTruthLabel(candidate.BoundsPx), state));
        }

        // Groundtruth: sw1 = ON (true), sw3 = OFF (false)
        var onRow = results.First(r => r.Label == OnControlText);
        var offRow = results.First(r => r.Label == OffControlText);
        Assert.True(onRow.State == true, $"expected ON (true) for {onRow.Label}, got {onRow.State}");
        Assert.True(offRow.State == false, $"expected OFF (false) for {offRow.Label}, got {offRow.State}");

        // ── 3. Production Binding + StateBelief for the ON row ────────────────
        var onCandidate = bridge.Candidates.First(c => GroundTruthLabel(c.BoundsPx) == OnControlText);
        var onBounds = Normalized(bridge.Width, bridge.Height, onCandidate.BoundsPx);
        var onCriteria = CriteriaFor(OnSemanticIdentity, OnControlText);
        AssertTruthfulSemanticModeling(onCriteria.KnownObjects);

        var obs = new Observation(
            [
                new ObservedElement(OnControlText, null, 0,
                    new ElementBounds(0.05f, 0.12f, 0.9f, 0.14f), "menuItem"),
                new ObservedElement("", onRow.State, 1, onBounds, "toggle"),
            ],
            "com.android.settings", 10);

        var evidence = BindingAnalysis.Analyze(obs, onCriteria);
        var bindings = BindingReconciler.Reconcile(evidence, onCriteria.KnownObjects);
        var belief = StateBeliefReducer.Reduce(obs, bindings);

        var onKey = $"{OnSemanticIdentity}.Enabled";
        if (!belief.TryGetValue(onKey, out var onBeliefValue))
        {
            throw new InvalidOperationException(
                $"no belief produced: key={onKey}, evidence={evidence.Length}, bindings={bindings.Length}, " +
                $"obsState={onRow.State}");
        }
        Assert.True(onBeliefValue == true,
            $"expected {onKey} == true for ON row, got {onBeliefValue}");

        // ── 4. OFF row → belief false ────────────────────────────────────────
        var offCandidate = bridge.Candidates.First(c => GroundTruthLabel(c.BoundsPx) == OffControlText);
        var offBounds = Normalized(bridge.Width, bridge.Height, offCandidate.BoundsPx);
        var offCriteria = CriteriaFor(OffSemanticIdentity, OffControlText);
        AssertTruthfulSemanticModeling(offCriteria.KnownObjects);

        var offObs = new Observation(
            [
                new ObservedElement(OffControlText, null, 0,
                    new ElementBounds(0.05f, 0.72f, 0.9f, 0.75f), "menuItem"),
                new ObservedElement("", offRow.State, 1, offBounds, "toggle"),
            ],
            "com.android.settings", 11);

        var offEvidence = BindingAnalysis.Analyze(offObs, offCriteria);
        var offBindings = BindingReconciler.Reconcile(offEvidence, offCriteria.KnownObjects);
        var offBelief = StateBeliefReducer.Reduce(offObs, offBindings);

        var offKey = $"{OffSemanticIdentity}.Enabled";
        if (!offBelief.TryGetValue(offKey, out var offBeliefValue))
        {
            throw new InvalidOperationException(
                $"no OFF belief produced: key={offKey}, evidence={offEvidence.Length}, bindings={offBindings.Length}, " +
                $"obsState={offRow.State}");
        }
        Assert.True(offBeliefValue == false,
            $"expected {offKey} == false for OFF row, got {offBeliefValue}");
    }
}

/// <summary>Minimal immutable-dictionary helper (avoids full collection init ceremony).</summary>
internal static class ImmutableDictionaryHelper
{
    public static System.Collections.Immutable.ImmutableDictionary<string, string> Of(string k, string v)
        => System.Collections.Immutable.ImmutableDictionary.Create<string, string>(System.StringComparer.Ordinal)
            .Add(k, v);
}
