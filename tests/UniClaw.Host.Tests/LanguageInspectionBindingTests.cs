using UniClaw.Host.Capability;
using UniClaw.Kernel.Capability;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class LanguageInspectionBindingTests
{
    private static CapabilityRegistry Registry() =>
        RuntimeIntegrationCapabilityComposition.RegisterLanguageInspector();

    [Fact]
    public void NoTaskRequest_DoesNotLoadCapability()
    {
        Assert.Null(LanguageInspectionBindingFactory.Create(null, "run-1", null));
    }

    [Fact]
    public void OptionalRequestWithoutSelection_DoesNotBind()
    {
        var request = new LanguageInspectionTaskRequest(false, "en");
        Assert.Null(LanguageInspectionBindingFactory.Create(null, "run-1", request));
    }

    [Fact]
    public void FixedTaskSelection_BindsRegisteredInspector()
    {
        var request = new LanguageInspectionTaskRequest(
            Required: true,
            ExpectedLanguage: "en-US",
            IgnoreRoutes: new[] { "route:ignore" },
            FixedCapabilityId: LanguageInspectionProtocol.CapabilityId);

        var binding = LanguageInspectionBindingFactory.Create(Registry(), "run-1", request);

        Assert.NotNull(binding);
        Assert.Equal(LanguageInspectionProtocol.CapabilityId, binding!.CapabilityId);
        Assert.Equal("en-US", binding.ExpectedLanguage);
        Assert.Equal(new[] { "route:ignore" }, binding.IgnoreRoutes);
        Assert.StartsWith("language-inspection-binding-", binding.BindingId, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentSelection_BindsOnlyWhenItMatchesTaskParameters()
    {
        var request = new LanguageInspectionTaskRequest(
            Required: true,
            ExpectedLanguage: "zh-CN",
            IgnoreRoutes: new[] { "route:ignore" });
        var selection = new LanguageInspectionSelection(
            LanguageInspectionProtocol.CapabilityId,
            "zh-CN",
            new[] { "route:ignore" });

        var binding = LanguageInspectionBindingFactory.Create(
            Registry(), "run-1", request, selection);

        Assert.NotNull(binding);
        Assert.Equal("zh-CN", binding!.ExpectedLanguage);
    }

    [Fact]
    public void FixedTaskSelection_TakesPrecedenceOverAgentSelection()
    {
        var request = new LanguageInspectionTaskRequest(
            Required: true,
            ExpectedLanguage: "en",
            FixedCapabilityId: LanguageInspectionProtocol.CapabilityId);
        var conflicting = new LanguageInspectionSelection("other.capability", "zh");

        var binding = LanguageInspectionBindingFactory.Create(
            Registry(), "run-1", request, conflicting);

        Assert.NotNull(binding);
        Assert.Equal(LanguageInspectionProtocol.CapabilityId, binding!.CapabilityId);
    }

    [Fact]
    public void RequiredRequestWithoutSelection_FailsClosed()
    {
        var request = new LanguageInspectionTaskRequest(true, "en");

        var error = Assert.Throws<InvalidOperationException>(() =>
            LanguageInspectionBindingFactory.Create(Registry(), "run-1", request));

        Assert.Equal("language-inspection-required-capability-not-bound", error.Message);
    }

    [Fact]
    public void SelectionParameterMismatch_FailsClosed()
    {
        var request = new LanguageInspectionTaskRequest(true, "en");
        var selection = new LanguageInspectionSelection(
            LanguageInspectionProtocol.CapabilityId, "zh");

        var error = Assert.Throws<InvalidOperationException>(() =>
            LanguageInspectionBindingFactory.Create(Registry(), "run-1", request, selection));

        Assert.Equal("language-inspection-selection-expected-language-mismatch", error.Message);
    }
}
