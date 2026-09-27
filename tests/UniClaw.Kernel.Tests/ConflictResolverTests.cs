using System.Text.RegularExpressions;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>PER-014：ConflictResolver 的 typed semantic.checked 回归。</summary>
public sealed class ConflictResolverTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(3);

    private static ConflictResolver.ConflictCase Conflict(params string[] values) =>
        new("ui.node.cap-1#0.checked", values.Select((value, index) =>
            new ConflictResolver.ConflictingClaim(
                index == 0 ? "semantic.checked" : "semantic.checked.peer",
                value,
                T0,
                ConflictResolver.ClaimDomain.SemanticChecked)).ToArray());

    [Fact]
    public void A1_CheckedVsUnchecked_UsesTypedCheckedAuthority()
    {
        var result = ConflictResolver.Resolve(
            Conflict("checked", "unchecked"),
            ObservedValue<CheckedState>.Observed(CheckedState.Checked),
            T0,
            Window);

        Assert.Equal(ConflictResolver.Tier.CategoryAuthority, result.Tier);
        Assert.Equal(CheckedState.Checked, result.ResolvedSemantic);
        Assert.Equal("checked", result.ResolvedValue);
    }

    [Fact]
    public void A2_CheckedVsChecked_RemainsChecked()
    {
        var result = ConflictResolver.Resolve(
            Conflict("checked", "checked"),
            ObservedValue<CheckedState>.Observed(CheckedState.Checked),
            T0,
            Window);

        Assert.Equal(ConflictResolver.Tier.CategoryAuthority, result.Tier);
        Assert.Equal("checked", result.ResolvedValue);
        Assert.Null(result.OverruledProducer);
    }

    [Fact]
    public void A3_UncheckedVsUnchecked_RemainsUnchecked()
    {
        var result = ConflictResolver.Resolve(
            Conflict("unchecked", "unchecked"),
            ObservedValue<CheckedState>.Observed(CheckedState.Unchecked),
            T0,
            Window);

        Assert.Equal(ConflictResolver.Tier.CategoryAuthority, result.Tier);
        Assert.Equal(CheckedState.Unchecked, result.ResolvedSemantic);
        Assert.Equal("unchecked", result.ResolvedValue);
    }

    [Fact]
    public void A4_Partial_IsInsufficient_NotUnchecked()
    {
        var result = ConflictResolver.Resolve(
            Conflict("partial", "unchecked"),
            ObservedValue<CheckedState>.Observed(CheckedState.Partial),
            T0,
            Window);

        Assert.Equal(ConflictResolver.Tier.VisionDomain, result.Tier);
        Assert.Null(result.ResolvedValue);
        Assert.Contains("partial", result.Basis);
    }

    [Fact]
    public void A5_Unknown_IsInsufficient_NotUnchecked()
    {
        var result = ConflictResolver.Resolve(
            Conflict("unknown", "unchecked"),
            ObservedValue<CheckedState>.Unknown("partial-unrepresentable"),
            T0,
            Window);

        Assert.Equal(ConflictResolver.Tier.VisionDomain, result.Tier);
        Assert.Null(result.ResolvedValue);
        Assert.Contains("Unknown", result.Basis);
    }

    [Fact]
    public void A6_Unsupported_IsInsufficient_NotUnchecked()
    {
        var result = ConflictResolver.Resolve(
            Conflict("unsupported", "unchecked"),
            ObservedValue<CheckedState>.Unsupported("capability:checked-absent"),
            T0,
            Window);

        Assert.Equal(ConflictResolver.Tier.VisionDomain, result.Tier);
        Assert.Null(result.ResolvedValue);
        Assert.Contains("Unsupported", result.Basis);
    }

    [Fact]
    public void A7_RenderedOff_DoesNotBecomeSemanticUnchecked()
    {
        var result = ConflictResolver.Resolve(
            new ConflictResolver.ConflictCase(
                "ui.node.cap-1#0.checked",
                new[]
                {
                    new ConflictResolver.ConflictingClaim(
                        "rendered.appearance", "off", T0,
                        ConflictResolver.ClaimDomain.RenderedAppearance),
                }),
            ObservedValue<CheckedState>.Observed(CheckedState.Checked),
            T0,
            Window);

        Assert.Equal(ConflictResolver.Tier.CategoryAuthority, result.Tier);
        Assert.Equal(CheckedState.Checked, result.ResolvedSemantic);
        Assert.Equal("checked", result.ResolvedValue);
        Assert.Null(result.OverruledProducer);
    }

    [Fact]
    public void A8_ProductionResolverSourceHasNoLegacyStateReader()
    {
        var root = RepoRoot();
        var source = File.ReadAllText(Path.Combine(
            root, "src/UniClaw.Kernel/World/ConflictResolver.cs"));
        Assert.DoesNotContain(".state", source, StringComparison.Ordinal);
        Assert.DoesNotContain("XmlAuthoritySnapshot", source, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex("EndsWith\\(\\\"\\.state\\\"", RegexOptions.CultureInvariant), source);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "UniClaw.Kernel.slnx")))
                return directory.FullName;
            directory = directory.Parent!;
        }

        throw new InvalidOperationException("未定位到仓库根");
    }
}
