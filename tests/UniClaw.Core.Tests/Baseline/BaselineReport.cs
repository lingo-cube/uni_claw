using System.Collections.Immutable;
using UniClaw.Core.Simulation.ExpectedBehavior;

namespace UniClaw.Core.Tests.Baseline;

/// <summary>
/// Baseline test report data model containing verification results and numeric comparisons.
/// Used for JSON serialization and report generation.
/// </summary>
public sealed record class BaselineReport(
    string Scenario,
    DateTime Timestamp,
    bool AllPassed,
    ImmutableArray<RuleResult> Details,
    NumericAnchor ExpectedNumeric,
    NumericAnchor ActualNumeric);
