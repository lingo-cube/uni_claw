using UniClaw.Kernel.Runtime;

namespace UniClaw.Host.SettingsCoverage;

internal enum CoverageDirectiveKind { Enter, ReEnter, NestedEnter, Scroll, Back, Obstacle }

/// <summary>Selection facts are independent of the text shown to the Agent.</summary>
internal sealed record CoverageDirective(CoverageDirectiveKind Kind, string? TargetDescriptor, string Text)
{
    public string KindName => Kind switch
    {
        CoverageDirectiveKind.Enter => "enter",
        CoverageDirectiveKind.ReEnter => "re-enter",
        CoverageDirectiveKind.NestedEnter => "nested-enter",
        CoverageDirectiveKind.Scroll => "scroll",
        CoverageDirectiveKind.Back => "back",
        CoverageDirectiveKind.Obstacle => "obstacle",
        _ => "unknown",
    };

    public bool MatchesRecordedStep(CoverageStepRecord step) => Kind switch
    {
        CoverageDirectiveKind.Scroll => step.EffectClass is "swipe-up" or "swipe-down",
        CoverageDirectiveKind.Enter or CoverageDirectiveKind.ReEnter or CoverageDirectiveKind.NestedEnter
            or CoverageDirectiveKind.Back or CoverageDirectiveKind.Obstacle =>
            step.EffectClass is "tap" or "click" && step.TargetDescriptor == TargetDescriptor,
        _ => false,
    };
}

internal sealed record CoverageStepAttribution(
    string DecisionId, string? Directive, AgentActionStep Step, CoverageDirective? SelectedDirective = null);

/// <summary>Compatibility reader for historical text-only records. New selected
/// directives never pass through this parser; unknown legacy text retains its
/// historical interpretation instead of manufacturing new selection facts.</summary>
internal static class LegacyCoverageDirective
{
    public static bool IsObstacle(string? text) => text is not null
        && (text.StartsWith("Clear the popup", StringComparison.Ordinal)
            || text.StartsWith("Dismiss the popup", StringComparison.Ordinal));

    public static bool Matches(SettingsCoverageConfig config, CoverageStepRecord step)
    {
        if (step.Directive is not { } text) return true;
        var descriptor = Descriptor(text);
        return Kind(text) switch
        {
            "scroll" => step.EffectClass is "swipe-up" or "swipe-down",
            "back" => step.EffectClass is "tap" or "click" && step.TargetDescriptor == config.BackDescriptor,
            "enter" or "re-enter" or "nested-enter" => step.EffectClass is "tap" or "click"
                && step.TargetDescriptor == descriptor,
            _ => true,
        };
    }

    private static string? Descriptor(string text)
    {
        var start = text.IndexOf('<');
        var end = text.IndexOf('>');
        return start >= 0 && end > start ? text[(start + 1)..end] : null;
    }

    private static string? Kind(string text)
    {
        if (text.Contains("Scroll", StringComparison.Ordinal)) return "scroll";
        if (text.StartsWith("Return to", StringComparison.Ordinal)) return "back";
        if (text.Contains(" again", StringComparison.Ordinal)) return "re-enter";
        if (text.StartsWith("Enter", StringComparison.Ordinal)) return "enter";
        return null;
    }
}
