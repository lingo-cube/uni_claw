using UniClaw.Kernel.Capability;
using Xunit;

namespace UniClaw.Kernel.Tests.Capability;

/// <summary>
/// CAP-012 — Language Inspection 能力契约（skill 第 6 步）：L2 确定性规则、
/// registry 执法（LanguageInspection 类别）、R5 可替换性。
/// </summary>
public sealed class LanguageFormatInspectorTests
{
    private static LanguageInspectorRequest Request(
        string expectedLanguage = "en",
        InspectorInputState inputState = InspectorInputState.Accepted,
        InspectorCoverage coverage = InspectorCoverage.Full,
        params (string OccurrenceId, string? Declared, string? Rendered)[] items) =>
        new(new CapabilityCorrelation("run-1"),
            "rule.test.v1", expectedLanguage, coverage, inputState,
            items.Select(i => new ObservationTextItem(i.OccurrenceId, $"src:{i.OccurrenceId}", i.Declared, i.Rendered)));

    [Fact]
    public void Inspect_EnglishTexts_Pass()
    {
        var inspector = new LanguageFormatInspector();
        var request = Request("en", items: new (string, string?, string?)[] { ("occ-1", "Wi\u2011Fi", "Wi\u2011Fi settings"), ("occ-2", "123, Main St.", null) });

        var finding = inspector.Inspect(request);

        Assert.Equal(FindingDisposition.Pass, finding.Disposition);
        Assert.Equal(CapabilityStatus.Completed, finding.Status);
        Assert.Equal(LanguageInspectionProtocol.CapabilityId, finding.SourceOwner);
        Assert.Equal("rule.test.v1", finding.RuleVersion);
    }

    [Fact]
    public void Inspect_LocalePrefix_NormalizedAndBothScriptsSupported()
    {
        var inspector = new LanguageFormatInspector();

        Assert.Equal(FindingDisposition.Pass,
            inspector.Inspect(Request("en-US", items: ("o", "Network", null))).Disposition);
        Assert.Equal(FindingDisposition.Pass,
            inspector.Inspect(Request("zh-CN", items: ("o", "无线网络设置", null))).Disposition);
        Assert.Equal(FindingDisposition.Violation,
            inspector.Inspect(Request("zh", items: ("o", "Wireless 设置", null))).Disposition);
    }

    [Fact]
    public void Inspect_WrongScriptCharacter_ViolationWithOccurrenceAndCodepoint()
    {
        var inspector = new LanguageFormatInspector();
        var request = Request("en", items: new (string, string?, string?)[] { ("occ-1", "Network", null), ("occ-2", "Settings 设置", null) });

        var finding = inspector.Inspect(request);

        Assert.Equal(FindingDisposition.Violation, finding.Disposition);
        Assert.Equal(CapabilityStatus.Completed, finding.Status);
        Assert.Contains("occ-2", finding.Diagnostic, StringComparison.Ordinal);
        Assert.Contains("U+8BBE", finding.Diagnostic, StringComparison.Ordinal); // '设'
    }

    [Fact]
    public void Inspect_HonestUnknown_ForMissingInputOrUnsupportedLanguageOrNoText()
    {
        var inspector = new LanguageFormatInspector();

        Assert.Equal(FindingDisposition.Unknown,
            inspector.Inspect(Request("en", inputState: InspectorInputState.MissingInput)).Disposition);
        Assert.Equal(FindingDisposition.Unknown,
            inspector.Inspect(Request("en", coverage: InspectorCoverage.Partial)).Disposition);
        var unsupported = inspector.Inspect(Request("fr", items: ("o", "Bonjour", null)));
        Assert.Equal(FindingDisposition.Unknown, unsupported.Disposition);
        Assert.Contains("not supported", unsupported.Diagnostic, StringComparison.Ordinal);
        Assert.Equal(FindingDisposition.Unknown,
            inspector.Inspect(Request("en", items: ("o", "", null))).Disposition);
    }

    [Fact]
    public void CanonicalDescription_MatchesLanguageInspectionCategory()
    {
        ICapability capability = new LanguageFormatInspector();

        Assert.IsAssignableFrom<ILanguageInspector>(capability);
        var description = capability.Description;
        Assert.Equal(LanguageInspectionProtocol.CapabilityId, description.CapabilityId);
        Assert.Equal(CapabilityCategory.LanguageInspection, description.Category);
        Assert.Equal(CapabilityScope.RuntimeIntegration, description.Scope);
        var protocol = Assert.Single(description.Protocols);
        Assert.Equal((LanguageInspectionProtocol.Inspection, LanguageInspectionProtocol.Version),
            (protocol.Name, protocol.Version));
    }

    // ---- Registry 执法（LanguageInspection 类别，ModelRouting 同构） ----

    [Fact]
    public void Registry_AcceptsInstance_InRuntimeIntegrationDomain()
    {
        var registry = new CapabilityRegistry(TrustDomain.RuntimeIntegration);

        registry.Register(new LanguageFormatInspector(), "test");

        var resolved = registry.Resolve(LanguageInspectionProtocol.CapabilityId);
        Assert.IsAssignableFrom<ILanguageInspector>(resolved);
    }

    [Fact]
    public void Registry_RejectsDescriptionOnly_WrongProtocol_AndWrongScope()
    {
        var registry = new CapabilityRegistry(TrustDomain.RuntimeIntegration);

        Assert.Throws<ArgumentException>(() => registry.Register(
            LanguageFormatInspector.CanonicalDescription, "test"));

        var wrongProtocol = new LanguageFormatInspector(new CapabilityDescription(
            "x.lang", "1.0.0", CapabilityScope.RuntimeIntegration,
            new[] { new CapabilityProtocol("Something Else", "1.0") },
            Array.Empty<CapabilityDependency>(), HealthStatus.Unknown,
            CapabilityCategory.LanguageInspection, Array.Empty<CapabilityRole>(), Array.Empty<CapabilityRelationship>()));
        Assert.Throws<ArgumentException>(() => registry.Register(wrongProtocol, "test"));

        var wrongScope = new LanguageFormatInspector(new CapabilityDescription(
            "x.lang", "1.0.0", CapabilityScope.ProductRuntime,
            new[] { new CapabilityProtocol(LanguageInspectionProtocol.Inspection, LanguageInspectionProtocol.Version) },
            Array.Empty<CapabilityDependency>(), HealthStatus.Unknown,
            CapabilityCategory.LanguageInspection, Array.Empty<CapabilityRole>(), Array.Empty<CapabilityRelationship>()));
        Assert.Throws<ArgumentException>(() => registry.Register(wrongScope, "test"));
    }

    private sealed class NonInspectorCapability : ICapability
    {
        public CapabilityDescription Description { get; } = new(
            "x.lang", "1.0.0", CapabilityScope.RuntimeIntegration,
            new[] { new CapabilityProtocol(LanguageInspectionProtocol.Inspection, LanguageInspectionProtocol.Version) },
            Array.Empty<CapabilityDependency>(), HealthStatus.Unknown,
            CapabilityCategory.LanguageInspection, Array.Empty<CapabilityRole>(), Array.Empty<CapabilityRelationship>());
    }

    [Fact]
    public void Registry_RejectsInstanceShapeMismatch_BothDirections()
    {
        var registry = new CapabilityRegistry(TrustDomain.RuntimeIntegration);

        Assert.Throws<ArgumentException>(() => registry.Register(new NonInspectorCapability(), "test"));

        var wrongCategory = new LanguageFormatInspector(new CapabilityDescription(
            "x.lang", "1.0.0", CapabilityScope.RuntimeIntegration,
            new[] { new CapabilityProtocol("Whatever", "1.0") },
            Array.Empty<CapabilityDependency>(), HealthStatus.Unknown,
            CapabilityCategory.Generic, Array.Empty<CapabilityRole>(), Array.Empty<CapabilityRelationship>()));
        Assert.Throws<ArgumentException>(() => registry.Register(wrongCategory, "test"));
    }

    // ---- R5 可替换性：同一 ILanguageInspector 消费闭包喂双实现 ----

    private sealed class AlwaysUnknownInspector : ILanguageInspector
    {
        public CapabilityDescription Description { get; } = new(
            "test.always-unknown-inspector", "1.0.0", CapabilityScope.RuntimeIntegration,
            new[] { new CapabilityProtocol(LanguageInspectionProtocol.Inspection, LanguageInspectionProtocol.Version) },
            Array.Empty<CapabilityDependency>(), HealthStatus.Unknown,
            CapabilityCategory.LanguageInspection, Array.Empty<CapabilityRole>(), Array.Empty<CapabilityRelationship>());

        public Finding Inspect(LanguageInspectorRequest request) => request.CreateFinding(
            "stub-unknown", "test.always-unknown-inspector",
            FindingDisposition.Unknown, CapabilityStatus.Unknown, "stub has no rule");
    }

    [Fact]
    public void Realizations_AreInterchangeable_ThroughInterface()
    {
        static FindingDisposition InspectFirst(ILanguageInspector inspector) =>
            inspector.Inspect(Request("en", items: ("o", "Network", null))).Disposition;

        var rule = new LanguageFormatInspector();
        var stub = new AlwaysUnknownInspector();

        // 各自语义生效；消费闭包不认识具体类。
        Assert.Equal(FindingDisposition.Pass, InspectFirst(rule));
        Assert.Equal(FindingDisposition.Unknown, InspectFirst(stub));
        // 同一失败面：违例输入下规则实现诚实 Violation，且形状同构。
        var violation = rule.Inspect(Request("en", items: ("o", "Network 网络", null)));
        Assert.Equal(FindingDisposition.Violation, violation.Disposition);
        Assert.NotNull(violation.Correlation);
    }
}
