"""Role stability metrics — Role Flip Rate (Experiment A / GAP-09).

Pure, deterministic computation of the stability contract
``SameOccurrence + EquivalentEvidence → StableRole``: for each occurrence
track (the same physical UI object observed across frames/observations),
count how often the perceived role changed.

Definition (docs/analysis/runtime-stability-engineering-landscape.md 附录 C):

    role_flip_rate = total_flips / total_transitions      (aggregate)
    flip           = roles[i] != roles[i-1]               (per transition)
    transitions    = len(roles) - 1 per track

A track with a single observation has no transitions and contributes no
denominator (never fabricated as zero).  ``None`` marks "not computable",
matching the evaluation suite's NOT_SCORABLE spirit.

Inputs are occurrence sequences provided by an external identity/association
(StableKey / row_id / a future association layer); this module computes
metrics only and does not itself decide identity.  It is intentionally NOT
wired into ``compute_task_metrics`` (that path scores one prediction against
one ground truth); Role Flip Rate is a cross-observation stability metric.
"""
from __future__ import annotations

from collections import Counter
from dataclasses import dataclass
from typing import Mapping, Sequence

__all__ = [
    "TrackRoleFlip",
    "RoleStabilityResult",
    "role_flip_rate",
]


@dataclass(frozen=True)
class TrackRoleFlip:
    """One occurrence track's role-stability summary.

    ``flip_rate`` is ``None`` when the track has no transitions (single
    observation) — never fabricated as zero.
    """

    track_id: str
    transitions: int
    flips: int
    flip_rate: float | None
    #: One ``(from_role, to_role)`` pair per observed flip, in observation order.
    flip_pairs: tuple[tuple[str, str], ...]


@dataclass(frozen=True)
class RoleStabilityResult:
    """Aggregate Role Flip Rate plus per-track detail."""

    track_count: int
    transition_count: int
    flip_count: int
    role_flip_rate: float | None
    tracks: tuple[TrackRoleFlip, ...]
    #: ``((from, to), count)`` sorted by count descending, then pair.
    pair_counts: tuple[tuple[tuple[str, str], int], ...]


def role_flip_rate(sequences: Mapping[str, Sequence[str]]) -> RoleStabilityResult:
    """Compute Role Flip Rate over occurrence role tracks.

    ``sequences`` maps a track id (identity/association-provided) to the
    ordered role observations of that occurrence.  Empty sequences and blank
    roles are invalid input (``ValueError``); single-observation tracks are
    valid and contribute no denominator.
    """
    tracks: list[TrackRoleFlip] = []
    for track_id, roles in sorted(sequences.items()):
        roles = tuple(roles)
        if not roles:
            raise ValueError(f"track {track_id!r}: empty role sequence")
        if any(not isinstance(r, str) or not r for r in roles):
            raise ValueError(f"track {track_id!r}: blank/non-string role")
        transitions = len(roles) - 1
        flips = 0
        pairs: list[tuple[str, str]] = []
        for i in range(1, len(roles)):
            if roles[i] != roles[i - 1]:
                flips += 1
                pairs.append((roles[i - 1], roles[i]))
        tracks.append(
            TrackRoleFlip(
                track_id=track_id,
                transitions=transitions,
                flips=flips,
                flip_rate=flips / transitions if transitions > 0 else None,
                flip_pairs=tuple(pairs),
            )
        )
    ordered_tracks = tuple(tracks)
    transition_count = sum(t.transitions for t in ordered_tracks)
    flip_count = sum(t.flips for t in ordered_tracks)
    pair_counts = Counter(p for t in ordered_tracks for p in t.flip_pairs)
    return RoleStabilityResult(
        track_count=len(ordered_tracks),
        transition_count=transition_count,
        flip_count=flip_count,
        role_flip_rate=flip_count / transition_count if transition_count > 0 else None,
        tracks=ordered_tracks,
        pair_counts=tuple(
            sorted(pair_counts.items(), key=lambda kv: (-kv[1], kv[0]))
        ),
    )