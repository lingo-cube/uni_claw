using Xunit;
using UniClaw.Core.Domain.Models.Vision;

namespace UniClaw.Core.Tests.Domain.Vision;

/// <summary>
/// SelectionState 单元测试 — PRD §5.1: FromString 精确 HashSet 别名+回落 Normal;
/// 补 highlight/highlighted/inactive/grayed/SELECTED; IsValid(string)
/// </summary>
public class SelectionStateTests
{
    // ── Selected aliases ──

    [Theory]
    [InlineData("selected", SelectionState.Selected)]
    [InlineData("active", SelectionState.Selected)]
    [InlineData("checked", SelectionState.Selected)]
    [InlineData("highlight", SelectionState.Selected)]       // previously missing
    [InlineData("highlighted", SelectionState.Selected)]     // previously missing
    public void FromString_ShouldMapToSelected(string input, SelectionState expected)
    {
        Assert.Equal(expected, SelectionStateExtensions.FromString(input));
    }

    // ── Disabled aliases ──

    [Theory]
    [InlineData("disabled", SelectionState.Disabled)]
    [InlineData("inactive", SelectionState.Disabled)]        // not Selected (no substring "active" match)
    [InlineData("hidden", SelectionState.Disabled)]
    [InlineData("gray", SelectionState.Disabled)]
    [InlineData("grayed", SelectionState.Disabled)]
    [InlineData("dimmed", SelectionState.Disabled)]
    public void FromString_ShouldMapToDisabled(string input, SelectionState expected)
    {
        Assert.Equal(expected, SelectionStateExtensions.FromString(input));
    }

    // ── Unknown → Normal ──

    [Theory]
    [InlineData("normal", SelectionState.Normal)]
    [InlineData("activated", SelectionState.Normal)]         // not Selected (not in alias set)
    [InlineData("nonsense", SelectionState.Normal)]
    [InlineData("", SelectionState.Normal)]
    public void FromString_ShouldFallBackToNormal(string input, SelectionState expected)
    {
        Assert.Equal(expected, SelectionStateExtensions.FromString(input));
    }

    // ── Case-insensitive ──

    [Theory]
    [InlineData("SELECTED", SelectionState.Selected)]
    [InlineData("DISABLED", SelectionState.Disabled)]
    [InlineData("Normal", SelectionState.Normal)]
    public void FromString_CaseInsensitive(string input, SelectionState expected)
    {
        Assert.Equal(expected, SelectionStateExtensions.FromString(input));
    }

    // ── IsInteractive / IsActive ──

    [Fact]
    public void IsInteractive_ShouldBeFalseForDisabled()
    {
        Assert.False(SelectionState.Disabled.IsInteractive());
    }

    [Fact]
    public void IsInteractive_ShouldBeTrueForSelectedAndNormal()
    {
        Assert.True(SelectionState.Selected.IsInteractive());
        Assert.True(SelectionState.Normal.IsInteractive());
    }

    // ── IsValid(string) ──

    [Theory]
    [InlineData("selected", true)]
    [InlineData("checked", true)]                            // alias
    [InlineData("highlight", true)]                          // alias
    [InlineData("disabled", true)]
    [InlineData("inactive", true)]                           // alias
    [InlineData("normal", true)]                             // exact enum value
    public void IsValid_String_ReturnsTrueForKnownAndAlias(string value, bool expected)
    {
        Assert.Equal(expected, SelectionStateExtensions.IsValid(value));
    }

    [Theory]
    [InlineData("activated", false)]                         // not in any alias set
    [InlineData("", false)]                                  // empty
    [InlineData("nonsense", false)]                          // unknown
    public void IsValid_String_ReturnsFalseForUnknown(string value, bool expected)
    {
        Assert.Equal(expected, SelectionStateExtensions.IsValid(value));
    }
}
