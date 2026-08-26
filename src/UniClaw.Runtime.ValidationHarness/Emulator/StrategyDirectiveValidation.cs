namespace UniClaw.Runtime.ValidationHarness.Emulator;

/// <summary>
/// Forbidden payload-content categories the driver scans for (design D5
/// "Directive payload scans"; spec "Emulator driver boundary"): coordinates,
/// UI page paths, click sequences, element locators/selectors, action
/// selections, callbacks, and unresolved prose occupying a closed-value field.
/// Detection is deterministic: fixed lexical marker tables live in
/// <see cref="StrategyDirectiveValidator"/>; the same payload always yields the
/// same outcome before any wire call.
/// </summary>
public enum DirectiveForbiddenCategory
{
    /// <summary>No forbidden content detected.</summary>
    None = 0,

    /// <summary>Numeric coordinate content (axis values, numeric pairs, pixel units).</summary>
    Coordinate = 1,

    /// <summary>UI page path / breadcrumb content.</summary>
    UiPagePath = 2,

    /// <summary>Click / gesture sequence content.</summary>
    ClickSequence = 3,

    /// <summary>Element locator / selector content.</summary>
    ElementLocator = 4,

    /// <summary>Action selection content.</summary>
    ActionSelection = 5,

    /// <summary>Callback / handler content.</summary>
    Callback = 6,

    /// <summary>Free-form prose occupying a closed-value field.</summary>
    UnresolvedProse = 7,
}

/// <summary>
/// Deterministic directive validation outcome (task 3.1). <see cref="Legal"/>
/// means the payload may be transported; <see cref="Rejected"/> means it was
/// refused before any wire call. Rejection is a typed result, never an
/// exception-as-control-flow.
/// </summary>
public abstract record DirectiveValidationResult
{
    private DirectiveValidationResult()
    {
    }

    /// <summary>The directive satisfies the closed vocabulary and carries no forbidden content.</summary>
    public sealed record Legal : DirectiveValidationResult;

    /// <summary>
    /// The directive was rejected. <see cref="Category"/> is set for forbidden
    /// payload content and is null for closed-vocabulary / shape violations.
    /// </summary>
    public sealed record Rejected(DirectiveForbiddenCategory? Category, string Reason) : DirectiveValidationResult;

    /// <summary>Whether this result permits transport.</summary>
    public bool IsLegal => this is Legal;
}