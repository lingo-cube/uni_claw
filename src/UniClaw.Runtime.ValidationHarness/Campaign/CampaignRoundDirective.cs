using UniClaw.Runtime.Model;
using UniClaw.Runtime.ValidationHarness.Fixtures;

namespace UniClaw.Runtime.ValidationHarness.Campaign;

/// <summary>
/// One round's authored directive input (Phase 2.6, spec "Frozen iterative loop
/// with independent runs"): the human-readable goal prose (never transported,
/// never inferred from), the strategy directive the fixture module authored, and
/// the transport device selector. The directive carries the round's NEW
/// StrategyId — round independence is a runner-enforced contract: a repeated
/// StrategyId is rejected because idempotency is UniAgent-owned, outside this
/// change. The harness authors neither the strategy nor the completion; it only
/// transports what the planner authored through the frozen <c>run.strategy.start</c>.
/// </summary>
public sealed record CampaignRoundDirective
{
    /// <summary>Require all three authored inputs (a round always carries a
    /// concrete directive — goal-only inputs belong to the single-run driver
    /// path, not to a campaign round).</summary>
    public CampaignRoundDirective(string goal, StrategyDirective directive, string device)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(directive);
        ArgumentNullException.ThrowIfNull(device);
        Goal = goal;
        Directive = directive;
        Device = device;
    }

    /// <summary>Human-readable goal prose (authored; never transported, never
    /// inferred from — design D2).</summary>
    public string Goal { get; }

    /// <summary>The strategy directive for this round (authored by the fixture
    /// module or the upper-agent loop).</summary>
    public StrategyDirective Directive { get; }

    /// <summary>The transport device selector.</summary>
    public string Device { get; }

    /// <summary>The round's strategy identity (the runner never invents one).</summary>
    public string StrategyId => Directive.StrategyId;

    /// <summary>Adapt to the graduated ScenarioRunner fixture input (deterministic
    /// mode; the validation / transport / call-log path is identical to live
    /// mode, design D2).</summary>
    public DirectiveFixtureRecord ToFixtureRecord() => new(Goal, Directive, Device);
}