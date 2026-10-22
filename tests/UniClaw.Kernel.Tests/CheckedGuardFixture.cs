using UniClaw.Kernel.Evidence;

namespace UniClaw.Kernel.Tests;

/// <summary>Explicit companion observation for reviewed synthetic switch fixtures.
/// Call sites choose this fact; no ingestion adapter infers it from checked.</summary>
internal static class CheckedGuardFixture
{
    public static string SubjectFor(string checkedSubject) => checkedSubject[..^".checked".Length] + ".checkable";

    public static ObservationProposal Checkable(ObservationProposal checkedObservation) => checkedObservation with
    {
        Claim = new(SubjectFor(checkedObservation.Claim.Subject), "true"),
        Provenance = checkedObservation.Provenance with
        {
            Hierarchy = checkedObservation.Provenance.Hierarchy! with { Field = "checkable" },
        },
    };
}
