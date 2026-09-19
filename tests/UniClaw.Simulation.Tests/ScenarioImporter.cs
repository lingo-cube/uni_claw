using UniClaw.Kernel.Trace;

namespace UniClaw.Simulation.Tests;

/// <summary>场景导入失败（fail closed；不得静默降级）。</summary>
internal sealed class ScenarioImportException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>Derive 产物：重导出 bundle + derivation 审计日志。</summary>
internal sealed record ImportedScenario(
    MinimalScenarioBundle Bundle,
    IReadOnlyList<string> DerivationLog);

/// <summary>
/// RFS-001 Scenario Importer（D18 real 面）：sealed trace artifact →
/// derived stimuli → 可重跑 bundle。
///
/// 门（fail closed）：
///  - RecorderTerminal != Finalized → ScenarioImportException；
///  - artifact.RunId != "sim:" + source.ScenarioId → run-correlation-mismatch。
///
/// Derivation：从 sealed artifact 的 perception.observe occurrences 按
/// CaptureSequence 收集恰一条 TraceReferenceKind.Artifact 引用（strategy /
/// emission span 中重复的因果引用不代表新的观测 occurrence）；
/// 每个 16-hex 前缀映射到 source bundle 中 Sha256 以该前缀开头的资产
/// （fail closed：无匹配 → 异常）；再按 trace 顺序重版本化每个
/// PerceptionArtifactId 命中的 source stimulus（StimulusId =
/// "import-{n}-{originalId}"，Context/elements/claims/VirtualTime 原样）。
/// 派生 bundle 以 ScenarioBundleDigest.Sealed 重封。
/// </summary>
internal static class ScenarioImporter
{
    /// <summary>
    /// TRW-001 D4：从持久化产物加载（SealedTraceStore.LoadVerified 复核
    /// canonical rendering SHA-256），再走既有 Derive 门（Finalized +
    /// correlation；integrity 已由 LoadVerified 证明）。完整性失败 →
    /// ScenarioImportException fail closed。
    /// </summary>
    public static ImportedScenario DeriveFromPersisted(
        MinimalScenarioBundle source, string directory, string runCorrelationId)
    {
        RunTraceArtifact artifact;
        try
        {
            artifact = SealedTraceStore.LoadVerified(directory, runCorrelationId);
        }
        catch (SealedTraceIntegrityException e)
        {
            throw new ScenarioImportException("persisted-trace-integrity:" + e.Message, e);
        }
        return Derive(source, artifact);
    }

    public static ImportedScenario Derive(MinimalScenarioBundle source, RunTraceArtifact sealedArtifact)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sealedArtifact);

        if (sealedArtifact.RecorderTerminal != RecorderTerminal.Finalized)
            throw new ScenarioImportException("trace-not-sealed:" + sealedArtifact.RecorderTerminal);
        if (!RunTraceArtifactIntegrity.IsValid(sealedArtifact))
            throw new ScenarioImportException("trace-integrity-invalid");
        if (sealedArtifact.RunId != "sim:" + source.ScenarioId)
            throw new ScenarioImportException(
                $"run-correlation-mismatch: artifact={sealedArtifact.RunId} expected=sim:{source.ScenarioId}");

        // observation occurrence 顺序的 artifact 引用。这里刻意不去重：
        // 同一 artifact 被观察两次是两个不同 occurrence，重放必须保留。
        var artifactRefs = new List<string>();
        foreach (var span in sealedArtifact.Spans
            .Where(candidate => candidate.SpanDefinitionId == "perception.observe")
            .OrderBy(candidate => candidate.CaptureSequence))
        {
            var references = span.References
                .Where(reference => reference.Kind == TraceReferenceKind.Artifact)
                .ToList();
            if (references.Count != 1)
                throw new ScenarioImportException(
                    $"observe-artifact-cardinality:{span.SpanId}:{references.Count}");
            artifactRefs.Add(references[0].Value);
        }

        // artifact ref（16-hex content 前缀）→ bundle asset（fail closed）
        var refToAsset = new Dictionary<string, BundleAssetEntry>(StringComparer.Ordinal);
        foreach (var artifactRef in artifactRefs)
        {
            if (!artifactRef.StartsWith("art-", StringComparison.Ordinal) || artifactRef.Length != "art-".Length + 16)
                throw new ScenarioImportException($"unknown-artifact-ref-format:{artifactRef}");
            var prefix = artifactRef["art-".Length..];
            var matches = source.Assets
                .Where(a => a.Sha256.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count == 0)
                throw new ScenarioImportException($"unmatched-artifact-ref:{artifactRef}");
            if (matches.Count > 1)
                throw new ScenarioImportException($"ambiguous-artifact-ref:{artifactRef}:{matches.Count}");
            refToAsset[artifactRef] = matches[0];
        }

        // 每个 observe occurrence 必须唯一映射回一个 source stimulus。
        var log = new List<string>();
        var derived = new List<ScenarioStimulus>();
        var n = 0;
        foreach (var artifactRef in artifactRefs)
        {
            var asset = refToAsset[artifactRef];
            var matches = source.Stimuli.OfType<ScenarioStimulus.ObservationFrame>()
                .Where(s => s.PerceptionArtifactId == asset.AssetId)
                .ToList();
            if (matches.Count != 1)
                throw new ScenarioImportException(
                    $"source-stimulus-cardinality:{asset.AssetId}:{matches.Count}");
            var stimulus = matches[0];
            n++;
            derived.Add(stimulus with { StimulusId = $"import-{n}-{stimulus.StimulusId}" });
            log.Add($"{artifactRef} -> {asset.AssetId} -> import-{n}-{stimulus.StimulusId}");
        }
        if (derived.Count == 0)
            throw new ScenarioImportException("no-derivable-stimuli: trace 未引用任何 bundle stimulus artifact");

        var bundle = ScenarioBundleDigest.Sealed(source with
        {
            BundleId = source.BundleId + "-import",
            ScenarioId = source.ScenarioId + "-imported",
            ScenarioVersion = "v2",
            Stimuli = derived,
            Expected = source.Expected with
            {
                // 同一 agent 语义（consult 不变）、同 effect 数、无遗留输入；
                // status/classification/goal 保持 source 期望（重跑等价语义）
                ExpectedUnconsumedStimuli = 0,
            },
        });
        return new ImportedScenario(bundle, log);
    }
}
