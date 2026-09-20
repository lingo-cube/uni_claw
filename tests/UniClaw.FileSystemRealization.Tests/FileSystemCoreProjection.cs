using UniClaw.Core;

namespace UniClaw.FileSystemRealization.Tests;

/// <summary>
/// 文件域到 Core 的唯一投影缝。文件域有效性判定在这里完成，Core 只检查投影结果。
/// </summary>
public static class FileSystemCoreProjection
{
    public static FileSystemProjection ProjectObservation(
        FileSystemWorld world,
        FileSystemObservation observation)
    {
        var segment = ProjectSegment(world);
        var readdirEvidence = new EvidenceRecord(
            new CoreId($"evidence:{observation.Id}:readdir"),
            segment.Id,
            observation.ObservedAt,
            "readdir",
            world.Resolve(observation.Directory),
            new[] { "raw-listing" });

        var slice = new Slice(
            new CoreId($"slice:{observation.Id}"),
            segment.Id,
            new[] { readdirEvidence.Id },
            observation.ObservedAt,
            $"{observation.Directory}/*",
            observation.Entries.Select(entry => new CoreId(entry.Identity)).ToArray(),
            $"{observation.Entries.Count} direct entries");

        return new FileSystemProjection(segment, readdirEvidence, slice);
    }

    public static Segment ProjectSegment(FileSystemWorld world)
        => new(new CoreId(world.WorldKey), "directory-subtree");

    public static StatProjection ProjectStat(
        FileSystemWorld world,
        FileObservation file,
        DateTimeOffset observedAt)
    {
        var subject = new CoreId(file.Identity);
        var evidence = new EvidenceRecord(
            new CoreId($"evidence:stat:{file.Identity}:{file.Version.SnapshotKey}"),
            subject,
            observedAt,
            "stat",
            world.Resolve(file.RelativePath),
            new[] { "stat" });

        var sizeClaim = new Claim(
            new CoreId($"claim:size:{file.Identity}:{file.Version.SnapshotKey}"),
            subject,
            "size-bytes",
            file.Version.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ClaimDisposition.Accepted,
            new[] { evidence.Id });

        return new StatProjection(file, evidence, sizeClaim);
    }

    public static IReadOnlyList<Claim> ProjectCorrection(
        StatProjection previous,
        StatProjection current)
    {
        if (previous.File.Version == current.File.Version)
            return Array.Empty<Claim>();

        var conflict = new Claim(
            new CoreId($"claim:conflict:{current.File.Identity}:{current.File.Version.SnapshotKey}"),
            new CoreId(current.File.Identity),
            "size-bytes",
            current.File.Version.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ClaimDisposition.Conflict,
            new[] { current.Evidence.Id });

        var accepted = new Claim(
            new CoreId($"claim:accepted:{current.File.Identity}:{current.File.Version.SnapshotKey}"),
            new CoreId(current.File.Identity),
            "size-bytes",
            current.File.Version.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ClaimDisposition.Accepted,
            new[] { current.Evidence.Id });

        return new[] { conflict, accepted };
    }

    public static TargetBinding ProjectCanonicalBinding(
        FileSystemWorld world,
        FileObservation file,
        DateTimeOffset observedAt)
    {
        var stat = ProjectStat(world, file, observedAt);
        return CreateBinding(world, file, stat.Evidence, BindingDisposition.Canonical);
    }

    public static TargetBinding EvaluateCurrentBinding(
        FileSystemWorld world,
        TargetBinding historicalBinding,
        string relativePath,
        DateTimeOffset observedAt)
    {
        var current = world.Describe(relativePath);
        var originalKey = historicalBinding.BasisReferences?.SingleOrDefault()?.SnapshotKey;
        var disposition = originalKey == current.Version.SnapshotKey
            ? BindingDisposition.Canonical
            : BindingDisposition.Stale;

        return historicalBinding with { Disposition = disposition };
    }

    public static Effect ProjectEffect(FileSystemWorld world, string relativePath)
        => new(
            new CoreId($"effect:{relativePath}"),
            "append",
            ProjectSegment(world).Id);

    public static Attempt ProjectAttempt(
        Effect effect,
        TargetBinding binding,
        DeliveryOutcome delivery = DeliveryOutcome.Unknown,
        DateTimeOffset? startedAt = null)
        => new(
            new CoreId($"attempt:{effect.Id.Value}:{binding.Id.Value}"),
            effect.Id,
            binding.Id,
            startedAt ?? DateTimeOffset.UtcNow,
            delivery,
            DeliveryEvidenceId: null);

    public static EvidenceRecord ProjectLateFeedback(
        FileSystemWorld world,
        Attempt attempt,
        DateTimeOffset observedAt)
        => new(
            new CoreId($"evidence:feedback:{attempt.Id.Value}:{observedAt.UtcTicks}"),
            attempt.Id,
            observedAt,
            "feedback",
            world.RootPath,
            new[] { "external-feedback" });

    private static TargetBinding CreateBinding(
        FileSystemWorld world,
        FileObservation file,
        EvidenceRecord statEvidence,
        BindingDisposition disposition)
        => new(
            new CoreId($"binding:{file.Identity}:{file.Version.SnapshotKey}"),
            ProjectSegment(world).Id,
            BasisSliceId: null,
            "path",
            file.RelativePath,
            disposition,
            new[] { new BasisReference(statEvidence.Id, "ResourceVersion", file.Version.SnapshotKey) });
}

public sealed record FileSystemProjection(
    Segment Segment,
    EvidenceRecord ReaddirEvidence,
    Slice Slice);

public sealed record StatProjection(
    FileObservation File,
    EvidenceRecord Evidence,
    Claim AcceptedSizeClaim);
