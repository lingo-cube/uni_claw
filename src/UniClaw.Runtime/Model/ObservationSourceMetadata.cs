namespace UniClaw.Runtime.Model;

/// <summary>Source reliability tier for an observation channel.</summary>
public enum ObservationSourceTier
{
    /// <summary>Primary visual capture channel.</summary>
    PrimaryVision = 0,
    /// <summary>Optional auxiliary structured channel.</summary>
    AuxiliaryStructured = 1,
}

/// <summary>Immutable source identity and frame provenance for one observation channel.</summary>
public sealed record ObservationSourceMetadata
{
    /// <summary>Creates source provenance metadata.</summary>
    public ObservationSourceMetadata(ObservationSourceTier tier, bool available, long observationSequence, string frameReference, int displayWidth, int displayHeight, string provenance, string sourceId = "unspecified")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frameReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        if (observationSequence <= 0) throw new ArgumentOutOfRangeException(nameof(observationSequence));
        if (displayWidth <= 0) throw new ArgumentOutOfRangeException(nameof(displayWidth));
        if (displayHeight <= 0) throw new ArgumentOutOfRangeException(nameof(displayHeight));
        Tier = tier; Available = available; ObservationSequence = observationSequence;
        FrameReference = frameReference; DisplayWidth = displayWidth; DisplayHeight = displayHeight; Provenance = provenance; SourceId = sourceId;
    }

    /// <summary>Reliability tier.</summary>
    public ObservationSourceTier Tier { get; }
    /// <summary>Whether the source produced usable evidence.</summary>
    public bool Available { get; }
    /// <summary>Owning observation sequence.</summary>
    public long ObservationSequence { get; }
    /// <summary>Capture-frame correlation reference.</summary>
    public string FrameReference { get; }
    /// <summary>Capture display width.</summary>
    public int DisplayWidth { get; }
    /// <summary>Capture display height.</summary>
    public int DisplayHeight { get; }
    /// <summary>Mechanism provenance.</summary>
    public string Provenance { get; }
    /// <summary>Stable source identity independent of scenario meaning.</summary>
    public string SourceId { get; }
}
