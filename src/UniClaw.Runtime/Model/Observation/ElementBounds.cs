namespace UniClaw.Runtime.Model;

/// <summary>
/// Immutable normalized element bounds relative to the canonical Observation frame.
///
/// Canonical frame: full-screenshot, top-left origin, normalized [0,1]×[0,1].
/// This is the same normalized space produced by the upstream perception pipeline
/// (fusion.py → _remap_coords → original full-screen space → normalized).
///
/// Invariants: 0 ≤ X1 ≤ X2 ≤ 1, 0 ≤ Y1 ≤ Y2 ≤ 1.
/// Malformed bounds must not silently enter semantic reasoning.
///
/// Bounds are SPATIAL EVIDENCE, not element identity, not semantic truth.
/// Coordinate ≠ Element Identity (frozen).
/// Bounds ≠ Page Identity (frozen).
/// </summary>
/// <param name="X1">Left edge, normalized [0,1].</param>
/// <param name="Y1">Top edge, normalized [0,1].</param>
/// <param name="X2">Right edge, normalized [0,1].</param>
/// <param name="Y2">Bottom edge, normalized [0,1].</param>
public sealed record ElementBounds(float X1, float Y1, float X2, float Y2)
{
    /// <summary>Center X coordinate, normalized [0,1].</summary>
    public float CenterX => (X1 + X2) / 2f;

    /// <summary>Center Y coordinate, normalized [0,1].</summary>
    public float CenterY => (Y1 + Y2) / 2f;

    /// <summary>Width in normalized space.</summary>
    public float Width => X2 - X1;

    /// <summary>Height in normalized space.</summary>
    public float Height => Y2 - Y1;

    /// <summary>Validates bounds invariants. Returns true if bounds are well-formed.</summary>
    public bool IsValid =>
        X1 >= 0f && X1 <= 1f
        && Y1 >= 0f && Y1 <= 1f
        && X2 >= X1 && X2 <= 1f
        && Y2 >= Y1 && Y2 <= 1f;
}
