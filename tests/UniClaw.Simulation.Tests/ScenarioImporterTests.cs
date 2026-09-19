using System.Collections.Immutable;
using UniClaw.Kernel.Trace;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// Scenario Importer（D18 Derive 面）单元锁：sealed-only 门 +
/// run-correlation 门 + artifact-ref → asset → stimulus 映射。
/// </summary>
public sealed class ScenarioImporterTests
{
    private static RunTraceArtifact Artifact(
        RecorderTerminal terminal,
        string runId = "sim:wifi-off-to-on",
        ImmutableArray<TraceSpan> spans = default) => RunTraceArtifactIntegrity.Seal(new(
        SchemaVersion: "1",
        RunId: runId,
        TraceId: "t",
        RootSpanId: null,
        RecorderTerminal: terminal,
        Spans: spans.IsDefault ? ImmutableArray<TraceSpan>.Empty : spans,
        RecorderDiagnostics: ImmutableArray<TraceDiagnostic>.Empty));

    private static TraceSpan Span(int sequence = 1, string operation = "perception.observe", params TraceReference[] references) => new(
        $"span-{sequence}", null, operation, StructuralOutcome.Completed,
        references.ToImmutableArray(), ImmutableArray<TraceEvent>.Empty, CaptureSequence: sequence);

    [Fact]
    public void QuarantinedArtifact_FailsClosed()
    {
        Assert.Throws<ScenarioImportException>(
            () => ScenarioImporter.Derive(GoldenScenarioBundles.WifiToggleOffToOn(), Artifact(RecorderTerminal.Quarantined)));
    }

    [Fact]
    public void FinalizedArtifact_WithoutArtifactRefs_FailsClosed_NoDerivableStimuli()
    {
        Assert.Throws<ScenarioImportException>(
            () => ScenarioImporter.Derive(GoldenScenarioBundles.WifiToggleOffToOn(), Artifact(RecorderTerminal.Finalized)));
    }

    [Fact]
    public void RunIdMismatch_FailsClosed()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        Assert.Throws<ScenarioImportException>(
            () => ScenarioImporter.Derive(bundle, Artifact(RecorderTerminal.Finalized, runId: "sim:other")));
    }

    [Fact]
    public void FinalizedArtifact_TamperedAfterSeal_FailsClosed()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var artifact = Artifact(RecorderTerminal.Finalized) with { TraceId = "tampered" };

        var ex = Assert.Throws<ScenarioImportException>(() => ScenarioImporter.Derive(bundle, artifact));

        Assert.Equal("trace-integrity-invalid", ex.Message);
    }

    [Fact]
    public void MalformedArtifactRef_FailsClosed()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var artifact = Artifact(RecorderTerminal.Finalized,
            spans: ImmutableArray.Create(Span(references: new TraceReference(TraceReferenceKind.Artifact, "art-short"))));
        Assert.Throws<ScenarioImportException>(() => ScenarioImporter.Derive(bundle, artifact));
    }

    [Fact]
    public void UnmatchedArtifactRef_FailsClosed()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var artifact = Artifact(RecorderTerminal.Finalized,
            spans: ImmutableArray.Create(Span(references: new TraceReference(TraceReferenceKind.Artifact, "art-" + new string('0', 16)))));
        Assert.Throws<ScenarioImportException>(() => ScenarioImporter.Derive(bundle, artifact));
    }

    /// <summary>合法映射：artifact ref（16-hex 前缀）→ asset → stimulus 重版本化。</summary>
    [Fact]
    public void ArtifactRef_MatchingAssetPrefix_DerivesReVersionedStimulus()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var frame = (ScenarioStimulus.ObservationFrame)bundle.Stimuli[0];
        var asset = bundle.Assets.Single(a => a.AssetId == frame.PerceptionArtifactId);
        var artifactRef = "art-" + asset.Sha256[..16];
        var artifact = Artifact(RecorderTerminal.Finalized,
            spans: ImmutableArray.Create(Span(references: new TraceReference(TraceReferenceKind.Artifact, artifactRef))));

        var imported = ScenarioImporter.Derive(bundle, artifact);

        var derived = Assert.Single(imported.Bundle.Stimuli);
        Assert.Equal("import-1-" + frame.StimulusId, derived.StimulusId);
        var derivedFrame = Assert.IsType<ScenarioStimulus.ObservationFrame>(derived);
        Assert.Equal(frame.PerceptionArtifactId, derivedFrame.PerceptionArtifactId);
        Assert.Equal(frame.VirtualTime, derivedFrame.VirtualTime);
        Assert.Equal(frame.Context, derivedFrame.Context);
        Assert.Single(imported.DerivationLog);
    }

    [Fact]
    public void RepeatedObserveOccurrence_ReferencingSameArtifact_PreservesBothOccurrences()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var frame = (ScenarioStimulus.ObservationFrame)bundle.Stimuli[0];
        var asset = bundle.Assets.Single(a => a.AssetId == frame.PerceptionArtifactId);
        var reference = new TraceReference(TraceReferenceKind.Artifact, "art-" + asset.Sha256[..16]);
        var artifact = Artifact(RecorderTerminal.Finalized, spans: ImmutableArray.Create(
            Span(1, references: reference),
            Span(2, references: reference)));

        var imported = ScenarioImporter.Derive(bundle, artifact);

        Assert.Equal(2, imported.Bundle.Stimuli.Count);
        Assert.Equal(
            new[] { "import-1-" + frame.StimulusId, "import-2-" + frame.StimulusId },
            imported.Bundle.Stimuli.Select(stimulus => stimulus.StimulusId));
    }
}
