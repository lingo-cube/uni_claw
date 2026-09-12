"""FUSION_PUBLICATION_BOUNDARY_REPAIR_GATE — top-level world-occurrence
publication boundary for ``row-relation-head`` satellites.

Frozen invariants (Leader gate):

    INTERNAL_COMPOSITION_ARTIFACT   !=  CANONICAL_WORLD_OCCURRENCE
    RAW_EVIDENCE_CONSUMED_BY_PARENT !=  INDEPENDENT_WORLD_OBJECT
    NO_INTERACTION_EVIDENCE         !=  PROVEN_NONINTERACTIVE

A ``row-relation-head`` band's satellite is an INTERNAL composition artifact
of its owning band: its raw source (YOLO/OCR id) is ALREADY consumed into the
band's ``evidence.allIds`` (the same detection also composes the row,
``RAW_EVIDENCE_CONSUMED_BY_PARENT_COMPOSITION``), and it carries no
independent interaction evidence. Re-publishing it as a top-level fused
candidate re-emits the same raw detection as an independent world object —
the phantom-fragment origin: text-less (or relegated) fragments were entering
the canonical occurrence inventory as independent objects, getting no semantic
verdict, and blocking completeness as Unknown obligations.

Suppression predicate — EVERY condition must hold for a candidate to be an
``INTERNAL_SUPPORTING_FRAGMENT`` (suppressed from top-level publication):

    1. produced by row-relation-head composition
       (``evidence.typeInferred == "row_relation_head_satellite"``);
    2. same field is the explicit internal satellite marker
       (``row_relation_head_satellite``);
    3. valid ``evidence.headId``;
    4. ``headId`` resolves to a CURRENTLY EMITTED relation-head band in the
       same frame (a candidate whose id == headId and whose
       ``evidence.typeInferred == "row_relation_head"``);
    5. the satellite's raw source id(s) are ALL contained in the owning
       band's ``evidence.allIds`` (the band consumed the same evidence);
    6. the satellite carries NO independent primary interaction evidence
       (its raw source is not a switch/checkbox/toggle/slider-shaped
       control — ``role != "toggle"``).

If ANY condition fails → keep the existing fail-closed publication behavior
(the item remains a top-level candidate). None of  ``text == ""`` /
``type == "NonInteractive"`` / bounds overlap / containment / no-clickable /
same-text alone may decide suppression: they are not identity or ownership
proofs; only the marker + parent band + consumed-evidence +
no-interaction-evidence conjunction proves ownership.

Observability preserved: satellites remain observable in the operator trace
(``pipeline_trace`` steps), the per-stage candidate views (``fusionStages``
via ``stage_sink``), and the operator-level composition record
(``run_relation_head`` ``record["satellites"]``); the engine additionally
reports the suppressed ids in ``result["_diagnostics"]
["internalSatellitesSuppressed"]``. Only the top-level world-occurrence
projection (``result["candidates"]`` → the C# ``Observation.Elements``)
excludes them.

None of the following are modified by this module: the band composition
semantics of ``row-relation-head``, OCR, Pattern-5, the semantic capability,
``SourceGroundingNormalizer``, ``InteractionAffordanceAnalyzer``, completeness
logic, and there is no textless → NonInteractive fallback.
"""
from __future__ import annotations

from typing import Any, Mapping

_INTERNAL_SATELLITE_MARKER: str = "row_relation_head_satellite"
_HEAD_BAND_MARKER: str = "row_relation_head"

#: Interaction evidence: a satellite whose raw source is a switch/checkbox/
#: toggle/slider-shaped control carries independent interaction evidence and
#: must stay published (the row's control is a real world object).  The
#: satellite ``role`` encodes the raw label via ``_SATELLITE_ROLE_BY_LABEL``
#: in the row-relation-head operator ("toggle" for the control family).
_INTERACTION_EVIDENCE_ROLES: frozenset[str] = frozenset({"toggle"})


def internal_supporting_fragment(
    candidate: Mapping[str, Any],
    candidates_by_id: Mapping[str, Mapping[str, Any]],
) -> bool:
    """True when ``candidate`` is a row-relation-head INTERNAL supporting
    fragment (ALL predicate conditions hold) — it must NOT be published as a
    top-level world occurrence.  Any violation keeps the item published
    (fail-closed)."""
    evidence = candidate.get("evidence") or {}
    # 1 + 2: explicit internal satellite marker.
    if evidence.get("typeInferred") != _INTERNAL_SATELLITE_MARKER:
        return False
    # 3: valid headId.
    head_id = evidence.get("headId")
    if not head_id:
        return False
    # 4: headId resolves to a currently emitted relation-head band.
    head = candidates_by_id.get(head_id)
    if head is None:
        return False
    head_evidence = head.get("evidence") or {}
    if head_evidence.get("typeInferred") != _HEAD_BAND_MARKER:
        return False
    # 5: the satellite's raw source id(s) are consumed by the owning band.
    source_ids: set[str] = set(evidence.get("allIds") or [])
    if evidence.get("yoloId"):
        source_ids.add(evidence["yoloId"])
    source_ids.update(evidence.get("ocrIds") or [])
    band_ids: set[str] = set(head_evidence.get("allIds") or [])
    if not source_ids or not source_ids.issubset(band_ids):
        return False
    # 6: no independent primary interaction evidence.
    if candidate.get("role") in _INTERACTION_EVIDENCE_ROLES:
        return False
    return True


def partition_internal_satellites(
    candidates: list[dict[str, Any]],
) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    """Split the fused candidate list into (published, internal_satellites).

    Deterministic: preserves the original candidate order in both partitions.
    ``internal_satellites`` are INTERNAL composition artifacts — they remain
    observable in operator trace / fusion stages / diagnostics but must never
    reach the top-level world-occurrence projection."""
    by_id: dict[str, Mapping[str, Any]] = {
        str(c.get("id", "")): c for c in candidates if c.get("id")
    }
    published: list[dict[str, Any]] = []
    internal: list[dict[str, Any]] = []
    for candidate in candidates:
        if internal_supporting_fragment(candidate, by_id):
            internal.append(candidate)
        else:
            published.append(candidate)
    return published, internal