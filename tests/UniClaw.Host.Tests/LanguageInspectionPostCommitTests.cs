using System.Text.Json;
using UniClaw.Host.Capability;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class LanguageInspectionPostCommitTests
{
    [Fact]
    public void CompleteDeclaredText_ProducesPassAndPersistsFinding()
    {
        var root = TempRoot();
        try
        {
            var binding = Binding(root, "en");
            var sink = new LanguageInspectionPostCommit(
                binding, Path.Combine(root, "language-findings.json"));

            sink.InspectAfterCommit(new[] { Proposal("cap-1", "Settings") }, "cap-1");

            var finding = Assert.Single(sink.Findings);
            Assert.Equal(FindingDisposition.Pass, finding.Disposition);
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "language-findings.json")));
            Assert.Equal(1, json.RootElement.GetProperty("findings").GetArrayLength());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void IncompleteCapture_ProducesUnknown()
    {
        var root = TempRoot();
        try
        {
            var binding = Binding(root, "en");
            var sink = new LanguageInspectionPostCommit(
                binding, Path.Combine(root, "language-findings.json"));

            sink.InspectAfterCommit(new[]
            {
                Proposal("cap-1", "Settings", field: "text", completeness: CoverageCompleteness.Partial),
            }, "cap-1");

            Assert.Equal(FindingDisposition.Unknown, Assert.Single(sink.Findings).Disposition);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void IgnoredRoute_ProducesNotApplicableFinding()
    {
        var root = TempRoot();
        try
        {
            var binding = Binding(root, "en", "route:ignored");
            var sink = new LanguageInspectionPostCommit(
                binding, Path.Combine(root, "language-findings.json"));

            sink.InspectAfterCommit(new[]
            {
                Proposal("cap-1", "Settings"),
                new ObservationProposal(
                    new ObservationClaim(ProductAssociationStrategy.ScreenRouteSubject, "route:ignored"),
                    IngressKind.Observation,
                    ObservationContext.External,
                    new Provenance("test.route", DateTimeOffset.UtcNow, "scope:route", Array.Empty<string>())),
            }, "cap-1");

            Assert.Equal(FindingDisposition.NotApplicable, Assert.Single(sink.Findings).Disposition);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RepeatedCapture_IsDeduplicated()
    {
        var root = TempRoot();
        try
        {
            var binding = Binding(root, "en");
            var sink = new LanguageInspectionPostCommit(
                binding, Path.Combine(root, "language-findings.json"));
            var proposals = new[] { Proposal("cap-1", "Settings") };

            sink.InspectAfterCommit(proposals, "cap-1");
            sink.InspectAfterCommit(proposals, "cap-1");

            Assert.Single(sink.Findings);
            Assert.Equal(1, sink.DuplicateCaptures["cap-1"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CaptureWithoutText_ProducesUnknown()
    {
        var root = TempRoot();
        try
        {
            var binding = Binding(root, "en");
            var sink = new LanguageInspectionPostCommit(
                binding, Path.Combine(root, "language-findings.json"));

            sink.InspectAfterCommit(new[]
            {
                Proposal("cap-1", "android.widget.TextView", field: "class"),
            }, "cap-1");

            Assert.Equal(FindingDisposition.Unknown, Assert.Single(sink.Findings).Disposition);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static LanguageInspectionBinding Binding(
        string root,
        string expectedLanguage,
        params string[] ignoreRoutes)
    {
        var request = new LanguageInspectionTaskRequest(
            Required: true,
            ExpectedLanguage: expectedLanguage,
            IgnoreRoutes: ignoreRoutes,
            FixedCapabilityId: LanguageInspectionProtocol.CapabilityId);
        return LanguageInspectionBindingFactory.Create(
            RuntimeIntegrationCapabilityComposition.RegisterLanguageInspector(),
            "run-1",
            request)!;
    }

    private static ObservationProposal Proposal(
        string captureId,
        string value,
        string field = "text",
        CoverageCompleteness completeness = CoverageCompleteness.CompleteWithinDeclaredSurface) =>
        new(
            new ObservationClaim($"ui.node.{captureId}#0.{field}", value),
            IngressKind.Observation,
            ObservationContext.External,
            new Provenance(
                "test.producer",
                DateTimeOffset.UtcNow,
                $"scope:ui.node.{captureId}#0.{field}",
                Array.Empty<string>(),
                new HierarchyCaptureDescriptor(
                    captureId,
                    34,
                    UiHierarchyAcquirerKind.LegacyUiAutomatorXml,
                    "1.0",
                    UiHierarchyFormat.UiAutomatorXml,
                    "device-1",
                    "session-1",
                    "cycle-1",
                    DateTimeOffset.UtcNow,
                    null,
                    HierarchyCapability.SemanticText,
                    completeness,
                    completeness == CoverageCompleteness.Partial ? "test-partial" : null,
                    0,
                    null,
                    field)));

    private static string TempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"language-inspection-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }
}
