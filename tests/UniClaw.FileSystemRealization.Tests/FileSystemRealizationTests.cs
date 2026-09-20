using System.Reflection;
using UniClaw.Core;
using Xunit;

namespace UniClaw.FileSystemRealization.Tests;

public sealed class FileSystemRealizationTests
{
    [Fact]
    public void S1_InitialListingProjectsSliceAndEvidence()
    {
        using var fixture = new FileFixture();
        fixture.Write("a.txt", "a");
        var world = fixture.World;
        var observation = world.Observe(".", fixture.T0);

        var projection = FileSystemCoreProjection.ProjectObservation(world, observation);

        Assert.Equal("directory-subtree", projection.Segment.SemanticKind);
        Assert.Equal("readdir", projection.ReaddirEvidence.Source);
        Assert.Single(projection.Slice.ObservationEvidenceIds);
        Assert.Single(projection.Slice.ObservedRecordIds);
        Assert.Equal("1 direct entries", projection.Slice.Coverage);
    }

    [Fact]
    public void S2_NewListingDoesNotOverwriteOverlappingHistory()
    {
        using var fixture = new FileFixture();
        fixture.Write("a.txt", "a");
        var first = fixture.World.Observe(".", fixture.T0);
        var firstProjection = FileSystemCoreProjection.ProjectObservation(fixture.World, first);

        fixture.Write("b.txt", "b");
        var second = fixture.World.Observe(".", fixture.T0.AddMinutes(1));
        var secondProjection = FileSystemCoreProjection.ProjectObservation(fixture.World, second);

        Assert.Equal(2, fixture.World.History.Count);
        Assert.Single(firstProjection.Slice.ObservedRecordIds);
        Assert.Equal(2, secondProjection.Slice.ObservedRecordIds.Count);
        Assert.Contains(firstProjection.Slice.ObservedRecordIds[0], secondProjection.Slice.ObservedRecordIds);
    }

    [Fact]
    public void S3_ClaimCorrectionAppendsAndNeverMutatesPreviousClaim()
    {
        using var fixture = new FileFixture();
        fixture.Write("a.txt", "a");
        var previousFile = fixture.World.Describe("a.txt");
        var previous = FileSystemCoreProjection.ProjectStat(fixture.World, previousFile, fixture.T0);
        var original = previous.AcceptedSizeClaim;

        fixture.Write("a.txt", "changed");
        fixture.Touch("a.txt", fixture.T0.AddMinutes(1));
        var current = FileSystemCoreProjection.ProjectStat(
            fixture.World,
            fixture.World.Describe("a.txt"),
            fixture.T0.AddMinutes(1));
        var corrections = FileSystemCoreProjection.ProjectCorrection(previous, current);

        Assert.Equal(ClaimDisposition.Accepted, original.Disposition);
        Assert.Equal(previous.Evidence.Id, original.EvidenceBasis.Single());
        Assert.Collection(
            corrections,
            conflict => Assert.Equal(ClaimDisposition.Conflict, conflict.Disposition),
            accepted => Assert.Equal(ClaimDisposition.Accepted, accepted.Disposition));
        Assert.All(corrections, claim => Assert.Equal(current.Evidence.Id, claim.EvidenceBasis.Single()));
    }

    [Fact]
    public void S4_FileIdentityIsWorldOwnedAndStableAcrossListings()
    {
        using var fixture = new FileFixture();
        fixture.Write("a.txt", "a");
        var first = fixture.World.Observe(".", fixture.T0);
        fixture.Write("a.txt", "changed");
        var second = fixture.World.Observe(".", fixture.T0.AddMinutes(1));

        Assert.Equal(first.Entries.Single().Identity, second.Entries.Single().Identity);
        Assert.Equal(first.Entries.Single().Identity,
            FileSystemCoreProjection.ProjectObservation(fixture.World, first).Slice.ObservedRecordIds.Single().Value);
    }

    [Fact]
    public void S5_ResourceVersionBasisIsNonSliceAndDispatchableWhenCanonical()
    {
        using var fixture = new FileFixture();
        fixture.Write("a.txt", "a");
        var file = fixture.World.Describe("a.txt");
        var binding = FileSystemCoreProjection.ProjectCanonicalBinding(fixture.World, file, fixture.T0);

        Assert.Null(binding.BasisSliceId);
        Assert.True(CoreInvariants.HasFixedBasis(binding));
        Assert.Equal(BindingDisposition.Canonical, binding.Disposition);
        Assert.Equal("ResourceVersion", binding.BasisReferences!.Single().Kind);
        Assert.True(CoreInvariants.CanDispatch(binding));
    }

    [Fact]
    public void S6_ChangedResourceProjectsStaleWithoutRebindingHistory()
    {
        using var fixture = new FileFixture();
        fixture.Write("a.txt", "a");
        var historicalFile = fixture.World.Describe("a.txt");
        var historical = FileSystemCoreProjection.ProjectCanonicalBinding(fixture.World, historicalFile, fixture.T0);

        fixture.Write("a.txt", "changed");
        fixture.Touch("a.txt", fixture.T0.AddMinutes(1));
        var current = FileSystemCoreProjection.EvaluateCurrentBinding(
            fixture.World, historical, "a.txt", fixture.T0.AddMinutes(1));

        Assert.Equal(BindingDisposition.Canonical, historical.Disposition);
        Assert.Equal(historical.BasisReferences, current.BasisReferences);
        Assert.Equal(BindingDisposition.Stale, current.Disposition);
        Assert.False(CoreInvariants.CanDispatch(current));
    }

    [Fact]
    public void S7_UnknownAttemptStaysUnknownWhenLateFeedbackIsAppended()
    {
        using var fixture = new FileFixture();
        fixture.Write("a.txt", "a");
        var file = fixture.World.Describe("a.txt");
        var binding = FileSystemCoreProjection.ProjectCanonicalBinding(fixture.World, file, fixture.T0);
        var effect = FileSystemCoreProjection.ProjectEffect(fixture.World, "a.txt");
        var attempt = FileSystemCoreProjection.ProjectAttempt(effect, binding, DeliveryOutcome.Unknown, fixture.T0);
        var feedback = FileSystemCoreProjection.ProjectLateFeedback(fixture.World, attempt, fixture.T0.AddMinutes(1));

        Assert.Equal(DeliveryOutcome.Unknown, attempt.Delivery);
        Assert.Null(attempt.DeliveryEvidenceId);
        Assert.Equal("feedback", feedback.Source);
        Assert.False(CoreInvariants.IsTerminalDelivery(attempt));
    }

    [Fact]
    public void S8_EquivalentOrderPreservesOnlyTheRequiredSemantics()
    {
        using var fixture = new FileFixture();
        fixture.Write("a.txt", "a");
        var capturedAt = fixture.T0;
        var worldA = new FileSystemWorld(fixture.RootPath);
        var worldB = new FileSystemWorld(fixture.RootPath);
        var capturedA = worldA.Capture(".", capturedAt);
        var capturedB = worldB.Capture(".", capturedAt);

        var fileA = worldA.Describe("a.txt");
        worldA.Register(capturedA);
        var projectionA = FileSystemCoreProjection.ProjectObservation(worldA, capturedA);
        var bindingA = FileSystemCoreProjection.ProjectCanonicalBinding(worldA, fileA, capturedAt);
        var attemptA = FileSystemCoreProjection.ProjectAttempt(
            FileSystemCoreProjection.ProjectEffect(worldA, "a.txt"), bindingA,
            DeliveryOutcome.Unknown, capturedAt);

        var fileB = worldB.Describe("a.txt");
        var projectionB = FileSystemCoreProjection.ProjectObservation(worldB, capturedB);
        var bindingB = FileSystemCoreProjection.ProjectCanonicalBinding(worldB, fileB, capturedAt);
        var attemptB = FileSystemCoreProjection.ProjectAttempt(
            FileSystemCoreProjection.ProjectEffect(worldB, "a.txt"), bindingB,
            DeliveryOutcome.Unknown, capturedAt);
        worldB.Register(capturedB);

        Assert.Equal(
            Fingerprint(worldA, capturedA, projectionA, bindingA, attemptA),
            Fingerprint(worldB, capturedB, projectionB, bindingB, attemptB));
    }

    [Fact]
    public void Closure_TracerAssemblyReferencesOnlyCoreAmongUniClawAssemblies()
    {
        var references = typeof(FileSystemWorld).Assembly
            .GetReferencedAssemblies()
            .Select(name => name.Name)
            .Where(name => name is not null && name.StartsWith("UniClaw.", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(new[] { "UniClaw.Core" }, references.OrderBy(value => value));
    }

    private static string Fingerprint(
        FileSystemWorld world,
        FileSystemObservation observation,
        FileSystemProjection projection,
        TargetBinding binding,
        Attempt attempt)
        => string.Join("|", new[]
        {
            string.Join(",", observation.Entries.Select(entry => entry.Identity)),
            string.Join(",", projection.Slice.ObservationEvidenceIds.Select(id => id.Value)),
            string.Join(",", projection.Slice.ObservedRecordIds.Select(id => id.Value)),
            projection.Slice.Scope,
            world.History.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            binding.BasisReferences!.Single().SnapshotKey,
            binding.Disposition.ToString(),
            CoreInvariants.CanDispatch(binding).ToString(),
            attempt.Delivery.ToString()
        });
}

internal sealed class FileFixture : IDisposable
{
    public FileFixture()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "uniclaw-filesystem-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
        World = new FileSystemWorld(RootPath);
        T0 = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);
    }

    public string RootPath { get; }
    public FileSystemWorld World { get; }
    public DateTimeOffset T0 { get; }

    public void Write(string relativePath, string content)
    {
        var path = World.Resolve(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Touch(string relativePath, DateTimeOffset timestamp)
        => File.SetLastWriteTimeUtc(World.Resolve(relativePath), timestamp.UtcDateTime);

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
            Directory.Delete(RootPath, recursive: true);
    }
}
