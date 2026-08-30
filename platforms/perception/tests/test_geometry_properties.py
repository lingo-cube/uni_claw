"""Property-based stability suite for canonical geometry normalization.

Targets the geometry invariants called out in
``docs/analysis/runtime-stability-engineering-landscape.md`` (§3 Geometry /
Numerical Stability, §14 Property-Based Testing):

    1. Valid pixel box → normalized box stays valid (finite, ordered, [0..1]).
    2. Reconstruction never fabricates an out-of-frame bound: for a VALID
       in-frame box, ``nx1 + (nx2 - nx1)`` must not exceed 1 by more than the
       float-stability tolerance (1e-6 — the same ``FitTolerance`` as
       ``SemanticNormalizedBounds`` in the C# runtime).
    3. Full-width elements normalize to exactly 1.0 (no 1.0000000063-class
       false positive).
    4. ``Box.from_list`` is the exact inverse of the constructor.
    5. Genuinely invalid input still fails closed (``ValueError``).

Deterministic-by-seed; Hypothesis shrinks any failure to a minimal
counter-example.
"""
from __future__ import annotations

import math

import pytest
from hypothesis import given, settings, strategies as st

from uniclaw_perception.schema import Box

#: Reconstruction tolerance, mirroring SemanticNormalizedBounds.FitTolerance
#: (1e-6) in src/UniClaw.Runtime/Capabilities/Perception/Semantic/V2/
#: SemanticEvidenceV2.cs. 1e-6 ≈ 10× the float32 ulp of 1.0 (1.19e-7) and far
#: below any genuine out-of-frame amount.
FIT_TOLERANCE = 1e-6

_PX = st.floats(
    min_value=0.0, max_value=4096.0, allow_nan=False, allow_infinity=False
)
_DIMS = st.integers(min_value=2, max_value=4096)


@st.composite
def in_frame_box(draw: st.DrawFn) -> tuple[Box, int, int]:
    """A valid pixel-space box strictly inside the frame:
    ``0 <= x1 <= x2 <= width``, ``0 <= y1 <= y2 <= height`` (zero width/height
    allowed — degenerate detections are legal; both coordinates stay within
    the drawn frame dimensions)."""
    width = draw(_DIMS)
    height = draw(_DIMS)
    x1 = draw(
        st.floats(min_value=0.0, max_value=float(width), allow_nan=False, allow_infinity=False)
    )
    x2 = draw(
        st.floats(min_value=x1, max_value=float(width), allow_nan=False, allow_infinity=False)
    )
    y1 = draw(
        st.floats(min_value=0.0, max_value=float(height), allow_nan=False, allow_infinity=False)
    )
    y2 = draw(
        st.floats(min_value=y1, max_value=float(height), allow_nan=False, allow_infinity=False)
    )
    return Box(x1, y1, x2, y2), width, height


@st.composite
def out_of_frame_box(draw: st.DrawFn) -> tuple[Box, int, int]:
    """A box whose right/bottom edge exceeds the frame (still finite):
    normalized output may exceed 1.0 — upstream ``ElementBounds.IsValid`` in
    the C# runtime is the fail-closed gate for such input; ``normalized`` must
    stay finite and deterministic, never NaN."""
    width = draw(_DIMS)
    height = draw(_DIMS)
    # Strictly beyond the frame right/bottom edge (still finite).
    x2 = draw(
        st.floats(min_value=float(width) + 0.5, max_value=float(width) * 2.0, allow_nan=False, allow_infinity=False)
    )
    y2 = draw(
        st.floats(min_value=float(height) + 0.5, max_value=float(height) * 2.0, allow_nan=False, allow_infinity=False)
    )
    return Box(0.0, 0.0, x2, y2), width, height


@settings(max_examples=300, deadline=None)
@given(framed=in_frame_box())
def test_valid_in_frame_box_normalizes_to_valid_bounds(framed: tuple[Box, int, int]) -> None:
    box, width, height = framed
    n = box.normalized(width, height)
    assert all(math.isfinite(v) for v in n.values())
    assert 0.0 <= n["x1"] <= n["x2"] <= 1.0
    assert 0.0 <= n["y1"] <= n["y2"] <= 1.0


@settings(max_examples=300, deadline=None)
@given(framed=in_frame_box())
def test_reconstruction_never_fabricates_out_of_frame(framed: tuple[Box, int, int]) -> None:
    # The Runtime later reconstructs left + (right - left); rounding at
    # round(..., 6) must never push that sum above 1 by more than the
    # float-stability tolerance (the 1.0000000063 false positive family).
    box, width, height = framed
    n = box.normalized(width, height)
    assert n["x1"] + (n["x2"] - n["x1"]) <= 1.0 + FIT_TOLERANCE
    assert n["y1"] + (n["y2"] - n["y1"]) <= 1.0 + FIT_TOLERANCE


@settings(max_examples=200, deadline=None)
@given(framed=out_of_frame_box())
def test_out_of_frame_input_normalizes_finite_and_detectably_out(framed: tuple[Box, int, int]) -> None:
    # Inputs beyond the frame are NOT silently clipped here: normalized output
    # exceeds 1.0 (detectable), stays finite, and remains deterministic. The
    # fail-closed decision belongs upstream (ElementBounds.IsValid in C#).
    box, width, height = framed
    n = box.normalized(width, height)
    assert all(math.isfinite(v) for v in n.values())
    assert n["x2"] > 1.0 or n["y2"] > 1.0


@settings(max_examples=50, deadline=None)
@given(width=_DIMS, height=_DIMS)
def test_full_width_element_normalizes_exactly_to_frame_edge(width: int, height: int) -> None:
    # X1 == 0, X2 == width: the bounds reach the right/bottom edge exactly and
    # must normalize to 1.0 exactly, not 1.0000000063.
    n = Box(0.0, 0.0, float(width), float(height)).normalized(width, height)
    assert n["x2"] == 1.0
    assert n["y2"] == 1.0
    assert n["x1"] + (n["x2"] - n["x1"]) <= 1.0 + FIT_TOLERANCE


def test_near_full_width_document_case_reconstructs_within_tolerance() -> None:
    # The real Display toolbar title case from the SEMANTIC_PROJECTION_BOUNDS
    # diagnostics (x1_px ≈ 0.002778 * W, X2 == W): the C# widen-first fix
    # reconstructs X2 exactly; the Python normalized form must show the same
    # in-frame property.
    width, height = 720, 1600
    box = Box(width * 0.002778, height * 0.0625, float(width), height * 0.120625)
    n = box.normalized(width, height)
    assert n["x1"] + (n["x2"] - n["x1"]) <= 1.0 + FIT_TOLERANCE
    assert abs(n["x2"] - 1.0) <= FIT_TOLERANCE


@settings(max_examples=300, deadline=None)
@given(framed=in_frame_box())
def test_from_list_round_trips_constructor(framed: tuple[Box, int, int]) -> None:
    box, _, _ = framed
    rebuilt = Box.from_list([box.x1, box.y1, box.x2, box.y2])
    assert rebuilt == box


@given(width=_DIMS, height=_DIMS)
def test_invalid_input_fails_closed(width: int, height: int) -> None:
    with pytest.raises(ValueError):
        Box(0.0, 0.0, 10.0, 10.0).normalized(0, height)   # width <= 0
    with pytest.raises(ValueError):
        Box(0.0, 0.0, 10.0, 10.0).normalized(width, -1)   # height <= 0
    with pytest.raises(ValueError):
        Box(math.nan, 0.0, 10.0, 10.0).normalized(width, height)  # non-finite
    with pytest.raises(ValueError):
        Box(0.0, 0.0, math.inf, 10.0).normalized(width, height)