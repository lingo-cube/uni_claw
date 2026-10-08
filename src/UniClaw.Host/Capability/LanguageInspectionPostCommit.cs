using System.Text.Json;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Host.Capability;

/// <summary>
/// CAP-013：在 Kernel 完成一批 observation proposal 处理后，向已绑定的语言
/// Inspector 投影 capture 级文本并持久化 Finding。这个类只接受已经形成的
/// task-scoped binding；它不访问 Registry，也不参与主 authority path。
/// </summary>
public sealed class LanguageInspectionPostCommit
{
    private static readonly HashSet<string> TextFields = new(StringComparer.Ordinal)
    {
        "text", "content_desc", "hint",
    };

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly LanguageInspectionBinding _binding;
    private readonly string _outputPath;
    private readonly List<Finding> _findings = new();
    private readonly HashSet<string> _processedCaptures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _duplicateCaptures = new(StringComparer.Ordinal);
    private readonly List<string> _persistenceDiagnostics = new();

    public LanguageInspectionPostCommit(LanguageInspectionBinding binding, string outputPath)
    {
        _binding = binding ?? throw new ArgumentNullException(nameof(binding));
        _outputPath = string.IsNullOrWhiteSpace(outputPath)
            ? throw new ArgumentException("A value is required.", nameof(outputPath))
            : outputPath;
    }

    public IReadOnlyList<Finding> Findings => _findings;
    public IReadOnlyDictionary<string, int> DuplicateCaptures => _duplicateCaptures;
    public IReadOnlyList<string> PersistenceDiagnostics => _persistenceDiagnostics;
    public bool PersistenceHealthy => _persistenceDiagnostics.Count == 0;

    /// <summary>
    /// Post-commit hook：调用方必须保证同一批 proposals 已经走完 Kernel.Process。
    /// 每个 capture 只处理一次；重复 capture 只记诊断计数，避免重复计数。
    /// </summary>
    public void InspectAfterCommit(
        IReadOnlyList<ObservationProposal> proposals,
        string? captureId = null)
    {
        ArgumentNullException.ThrowIfNull(proposals);
        var fallbackCaptureId = captureId
            ?? proposals.Select(p => p.Provenance?.Hierarchy?.CaptureId)
                .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        var groups = new Dictionary<string, List<ObservationProposal>>(StringComparer.Ordinal);
        foreach (var proposal in proposals)
        {
            var id = proposal.Provenance?.Hierarchy?.CaptureId
                ?? fallbackCaptureId
                ?? "__capture-id-unavailable";
            if (!groups.TryGetValue(id, out var group))
            {
                group = new List<ObservationProposal>();
                groups.Add(id, group);
            }
            group.Add(proposal);
        }

        if (groups.Count == 0)
            groups["__capture-id-unavailable"] = new List<ObservationProposal>();

        foreach (var group in groups)
            InspectCapture(group.Key, group.Value);
    }

    private void InspectCapture(string captureKey, IReadOnlyList<ObservationProposal> proposals)
    {
        var captureId = captureKey == "__capture-id-unavailable" ? null : captureKey;
        if (captureId is not null && !_processedCaptures.Add(captureId))
        {
            _duplicateCaptures[captureId] = _duplicateCaptures.TryGetValue(captureId, out var count)
                ? count + 1
                : 1;
            Persist();
            return;
        }

        var route = proposals
            .Where(p => p.Claim.Subject == ProductAssociationStrategy.ScreenRouteSubject)
            .Select(p => p.Claim.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var correlation = new CapabilityCorrelation(
            _binding.RunId,
            captureId: captureId,
            observationCycleId: proposals
                .Select(p => p.Provenance?.Hierarchy?.ObservationCycleId)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)));

        Finding finding;
        if (route is not null && _binding.IgnoreRoutes.Contains(route, StringComparer.Ordinal))
        {
            finding = new Finding(
                "language-inspection-not-applicable",
                correlation,
                LanguageInspectionProtocol.Version,
                LanguageInspectionProtocol.CapabilityId,
                FindingDisposition.NotApplicable,
                CapabilityStatus.Completed,
                $"capture '{captureId ?? "unknown"}' skipped by configured ignore route '{route}'");
        }
        else
        {
            var items = proposals
                .Where(IsTextProposal)
                .Select(ToTextItem)
                .Where(item => item is not null)
                .Cast<ObservationTextItem>()
                .ToArray();
            var descriptors = proposals
                .Select(p => p.Provenance?.Hierarchy)
                .Where(descriptor => descriptor is not null)
                .Cast<HierarchyCaptureDescriptor>()
                .ToArray();
            var hasIncompleteHierarchy = descriptors.Any(descriptor =>
                descriptor.CoverageCompleteness != CoverageCompleteness.CompleteWithinDeclaredSurface);
            var coverage = hasIncompleteHierarchy
                ? InspectorCoverage.Partial
                : descriptors.Length is 0
                    ? InspectorCoverage.Missing
                    : InspectorCoverage.Full;
            var inputState = items.Length is 0
                ? InspectorInputState.MissingInput
                : InspectorInputState.Accepted;
            var request = new LanguageInspectorRequest(
                correlation,
                LanguageInspectionProtocol.Version,
                _binding.ExpectedLanguage,
                coverage,
                inputState,
                items);
            try
            {
                finding = _binding.Inspector.Inspect(request);
            }
            catch (Exception error)
            {
                finding = request.CreateFinding(
                    "language-inspection-integration-unknown",
                    LanguageInspectionProtocol.CapabilityId,
                    FindingDisposition.Unknown,
                    CapabilityStatus.Unknown,
                    $"language inspection integration failed: {error.GetType().Name}: {error.Message}");
            }
        }

        _findings.Add(finding);
        Persist();
    }

    private static bool IsTextProposal(ObservationProposal proposal) =>
        proposal.Provenance?.Hierarchy is { } hierarchy
        && TextFields.Contains(hierarchy.Field)
        && !string.IsNullOrWhiteSpace(proposal.Claim.Value)
        && !string.IsNullOrWhiteSpace(proposal.Provenance.Scope);

    private static ObservationTextItem? ToTextItem(ObservationProposal proposal)
    {
        var hierarchy = proposal.Provenance?.Hierarchy;
        if (hierarchy is null || string.IsNullOrWhiteSpace(proposal.Claim.Value))
            return null;
        return new ObservationTextItem(
            $"{hierarchy.CaptureId}#{hierarchy.NodeLocalIndex}.{hierarchy.Field}",
            proposal.Provenance!.Scope,
            declaredText: proposal.Claim.Value,
            renderedText: null);
    }

    private void Persist()
    {
        try
        {
            var directory = Path.GetDirectoryName(_outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            var artifact = new
            {
                schemaVersion = "uniclaw.language-findings.v1",
                runId = _binding.RunId,
                bindingId = _binding.BindingId,
                capabilityId = _binding.CapabilityId,
                capabilityVersion = _binding.CapabilityVersion,
                expectedLanguage = _binding.ExpectedLanguage,
                ignoreRoutes = _binding.IgnoreRoutes,
                findings = _findings,
                duplicateCaptures = _duplicateCaptures,
            };
            File.WriteAllText(_outputPath, JsonSerializer.Serialize(artifact, JsonOptions));
        }
        catch (Exception error)
        {
            _persistenceDiagnostics.Add(
                $"language findings persistence failed: {error.GetType().Name}: {error.Message}");
        }
    }
}
