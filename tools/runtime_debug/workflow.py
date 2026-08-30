"""P5 — Diagnosis workflow + evidence gate (Agent/Harness integration surface).

Deterministic, read-only orchestration over the existing Core projections:
given a good/bad bundle pair and a case id, `diagnose_workflow` aggregates what
the toolchain has already proven (structural diff, generated packet, failed
spans, replay facts) into one report, and `evidence_gate` projects the §12
"implementation gate" (FDP/owner/evidence refs present, else EVIDENCE_COLLECTION).

This module performs NO analysis and NO authority: it only composes Core
outputs and re-states their projections. Semantic FDP/Owner/Disposition
judgment remains the Agent's, over this deterministic surface.
"""

from __future__ import annotations

from . import query, replay as replay_core
from .sources import bundle as bundle_source
from .status import OK


def diagnose_workflow(good_bundle_dir: str, bad_bundle_dir: str, case_id: str,
                      minimize: bool = False) -> dict:
    """One-pass diagnosis material for a good/bad pair — composed from Core
    projections only (run-compare, packet-generate, execution-tree --only-errors,
    replay extract/run, optional minimize). Fail-closed on any source error."""
    good = bundle_source.read_bundle(good_bundle_dir)
    bad = bundle_source.read_bundle(bad_bundle_dir)

    structural = query.compare_bundles(good, bad)
    packet_result = query.generate_packet(bad, case_id)
    packet = packet_result.get("packet")
    tree = query.execution_tree(bad, only_errors=True)
    fixture = replay_core.build_replay_fixture(bad, case_id)
    dry_run = replay_core.project_replay_run(fixture)
    minimized = replay_core.minimize_fixture(fixture) if minimize else None

    failed_spans = []
    if tree.get("roots") is not None:
        stack = list(tree["roots"])
        while stack:
            node = stack.pop()
            if node.get("outcome") in ("FAILED", "CANCELLED"):
                failed_spans.append({"spanId": node.get("spanId"),
                                     "name": node.get("name"), "outcome": node.get("outcome")})
            stack.extend(node.get("children") or [])

    report = {
        "status": OK,
        "caseId": case_id,
        "good": structural.get("good"),
        "bad": structural.get("bad"),
        "axes": structural.get("axes"),
        "packet": packet,
        "failedSpans": failed_spans,
        "replay": {"fixture": fixture, "dryRun": dry_run,
                   "minimized": minimized},
    }
    report["gate"] = evidence_gate(report)
    return report


def evidence_gate(report: dict) -> dict:
    """Project the §12 implementation gate over deterministic facts:

      - FDP present: a mechanically identified failure (structural axes CHANGED
        or a failed span or a firstMechanicallyFailedStep) exists;
      - Owner present: the packet stores an UNRESOLVED/confirmed Owner seam;
      - EvidenceRefs present: packet evidenceIndex non-empty.

    If FDP and evidence refs are present, disposition is EVIDENCE_COLLECTION
    until an Agent confirms semantic Owner/GapKind (this surface never invents
    them). The gate is a projection, not Runtime authority.
    """
    packet = report.get("packet") or {}
    ir = packet.get("debugIr") or {}
    index = packet.get("evidenceIndex") or []

    fdp_present = bool(
        (report.get("axes") or {}).get("terminal") == "CHANGED"
        or report.get("failedSpans")
        or (report.get("replay") or {}).get("dryRun", {}).get("firstMechanicallyFailedStep") is not None)
    owner = ir.get("Owner")
    owner_present = isinstance(owner, dict) and bool(owner.get("seam") or owner.get("domain"))
    evidence_refs_present = len(index) > 0

    if fdp_present and evidence_refs_present:
        disposition = "EVIDENCE_COLLECTION"
        blocked_by = []
        if not owner_present:
            blocked_by.append("OWNER_UNRESOLVED")
        if (report.get("packet") or {}).get("debugIr", {}).get("GapKind") in (None, "UNKNOWN"):
            blocked_by.append("GAPKIND_UNKNOWN")
    else:
        disposition = "INSUFFICIENT_EVIDENCE"
        blocked_by = []
        if not fdp_present:
            blocked_by.append("FDP_ABSENT")
        if not evidence_refs_present:
            blocked_by.append("EVIDENCEREFS_ABSENT")

    return {
        "status": OK,
        "fdpPresent": fdp_present,
        "ownerPresent": owner_present,
        "evidenceRefsPresent": evidence_refs_present,
        "disposition": disposition,
        "blockedBy": sorted(set(blocked_by)),
        "note": "Deterministic gate projection over toolchain facts; semantic FDP/Owner/GapKind judgment belongs to the Agent.",
    }