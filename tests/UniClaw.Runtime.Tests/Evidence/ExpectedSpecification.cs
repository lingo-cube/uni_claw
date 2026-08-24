using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;

namespace UniClaw.Runtime.Tests.Evidence;

/// <summary>
/// Declarative expected specification for the Evidence + Specification driven
/// validation model.
///
/// Describes WHAT the Runtime must prove through evidence — never HOW to do it:
///   - exploration scope (application + semantic root)
///   - depth constraint
///   - required coverage (which container identities must be discovered)
///   - completion criteria (goal evidence signal)
///   - optional state-changing controls (categories + handling policy)
///
/// The specification is a pure expectation; it contains no execution plan,
/// no click order, no route, and no hidden answers. It maps onto the production
/// <see cref="TypeLevelTraversalSpecification"/> for open-world execution and is
/// compared against actual Runtime output by the generic evaluator.
/// </summary>
public sealed record ExpectedSpecification(
    string ApplicationIdentity,
    string RootContainerIdentity,
    ImmutableHashSet<string> RequiredCoverage,
    int MaximumDepth,
    bool RequireGoalEvidenceSatisfied = true,
    bool IncludeStateChangingControls = false)
{
    /// <summary>Builds the production type-level traversal specification for open-world execution.</summary>
    public TypeLevelTraversalSpecification ToTypeLevelSpecification()
    {
        var categories = ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer);
        if (IncludeStateChangingControls)
        {
            categories = categories.Add(TypeLevelElementCategory.StateChangingControl);
        }

        TypeLevelDispatchPolicy? policy = null;
        if (IncludeStateChangingControls)
        {
            policy = new TypeLevelDispatchPolicy(
                ImmutableDictionary.CreateRange(new Dictionary<TypeLevelElementCategory, TypeLevelHandling>
                {
                    [TypeLevelElementCategory.NavigableContainer] = TypeLevelHandling.EnterAndTraverse,
                    [TypeLevelElementCategory.StateChangingControl] = TypeLevelHandling.SetDesiredState,
                }));
        }

        return new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(ApplicationIdentity, RootContainerIdentity),
            categories,
            MaximumDepth,
            new TypeLevelSafetyBoundary(categories),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(ApplicationIdentity, RootContainerIdentity),
            policy);
    }
}
