namespace UniClaw.Runtime.Harness;

/// <summary>Asset provenance — immutable historical evidence classification.</summary>
public enum AssetMaturity
{
    Synthetic = 0,
    RealitySeeded = 1,
    RecordedReality = 2,
    LiveCapture = 3,
}
