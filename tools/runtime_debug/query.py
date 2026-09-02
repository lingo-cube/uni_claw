"""Query Core — pure, deterministic, read-only projections over an EvidencePacket.

CLI and (future) TUI call exactly these functions; no other module implements
correlation or projection logic. Results are contract-limited: they never
compute FDP, root cause, Owner, or repair eligibility.
"""

from __future__ import annotations

from .packet import EvidencePacket, PACKET_SCHEMA_DIGEST, validate as validate_packet
from .status import (
    AMBIGUOUS_OCCURRENCE,
    EVIDENCE_UNAVAILABLE,
    IDENTITY_MISMATCH,
    INSUFFICIENT_TRACE_COVERAGE,
    OK,
)

_SELECTOR_FIELDS = ("occurrenceId", "stableKey", "rowId")

# CLI flag kind (snake_case) -> packet selector field (camelCase).
_SELECTOR_FIELD_MAP = {
    "occurrence_id": "occurrenceId",
    "stable_key": "stableKey",
    "row_id": "rowId",
    "evidence_ref": "evidenceRef",
}


# Closed stage vocabulary of the causal/evidence chain (FDP main view).
CAUSAL_STAGES = ("raw", "normalized", "fused", "canonical",
                 "semanticAdmission", "affordance", "runtimeState")


def causal_tree(packet: EvidencePacket, prune: tuple[str, ...] = (),
                only_decisions: bool = False, only_evidence: bool = False) -> dict:
    """Causal/evidence tree projection (FDP main view). Prune-only: hidden stages
    are omitted from the projection, never removed from the packet."""
    chain = packet.debug_ir.get("EvidenceChain")
    if not isinstance(chain, dict):
        return _fail(INSUFFICIENT_TRACE_COVERAGE, "debugIr.EvidenceChain is absent")
    pruned = frozenset(prune or ())
    stages = []
    for stage in CAUSAL_STAGES:
        entry = chain[stage]
        if stage in pruned:
            continue
        if not isinstance(entry, dict):
            continue
        if only_decisions and not (entry.get("decisionRefs") or []):
            continue
        if only_evidence and not (entry.get("inputRefs") or entry.get("outputRefs") or entry.get("decisionRefs")):
            continue
        stages.append({
            "stage": stage,
            "status": entry.get("status"),
            "summary": entry.get("summary"),
            "inputRefs": sorted(entry.get("inputRefs") or []),
            "decisionRefs": sorted(entry.get("decisionRefs") or []),
            "outputRefs": sorted(entry.get("outputRefs") or []),
        })
    return {
        "status": OK,
        "tree": {
            "kind": "CAUSAL",
            "stages": stages,
            "pruned": {"stageNames": sorted(pruned & frozenset(chain)),
                       "onlyDecisions": only_decisions,
                       "onlyEvidence": only_evidence},
        },
    }


def evidence_chain(packet: EvidencePacket, ref_id: str) -> dict:
    """Evidence chain query: where one EvidenceRef participates across stages."""
    index = packet.index_by_ref_id()
    if ref_id not in index:
        return _fail(EVIDENCE_UNAVAILABLE, f"no evidence ref '{ref_id}' in packet index")
    entry = index[ref_id]
    if entry.get("integrity") == IDENTITY_MISMATCH:
        return _fail(IDENTITY_MISMATCH, f"evidence '{ref_id}' is stored as IDENTITY_MISMATCH")

    chain = packet.debug_ir.get("EvidenceChain")
    positions = []
    if isinstance(chain, dict):
        for stage in CAUSAL_STAGES:
            stage_entry = chain[stage]
            for role in ("inputRefs", "decisionRefs", "outputRefs"):
                if ref_id in (stage_entry.get(role) or []):
                    positions.append({"stage": stage, "role": role.rstrip("s"),
                                      "status": stage_entry.get("status")})
    related = sorted({r for e in index.values()
                      if isinstance(e, dict) and (e.get("selector") or {}).get("evidenceRef") == ref_id
                      for r in (e.get("selector") or {}).values() if isinstance(r, str)})
    return {
        "status": OK,
        "ref": {
            "refId": ref_id,
            "kind": entry.get("kind"),
            "uri": entry.get("uri"),
            "digest": entry.get("digest"),
            "mediaType": entry.get("mediaType"),
            "integrity": entry.get("integrity", "OK"),
            "selector": entry.get("selector") or {},
        },
        "chainPositions": positions,
    }


def compare(packet: EvidencePacket) -> dict:
    """Packet-scoped differential projection: stored Good/Bad comparisons plus
    LastGood/FirstBad. Projects stored facts only — never computes them."""
    ir = packet.debug_ir
    good = ir.get("GoodComparison")
    bad = ir.get("BadComparison")
    if not isinstance(good, dict) or not isinstance(bad, dict):
        return _fail(INSUFFICIENT_TRACE_COVERAGE, "GoodComparison/BadComparison is absent")
    return {
        "status": OK,
        "comparison": {
            "good": _comparison_projection(good),
            "bad": _comparison_projection(bad),
            "lastGood": ir.get("LastGood"),
            "firstBad": ir.get("FirstBad"),
        },
    }


def assets(bundle) -> dict:
    """AssetRef index projection for one capture bundle (AssetRef first-class)."""
    refs = bundle.asset_refs()
    return {"status": OK, "count": len(refs), "assets": refs}


def asset_show(bundle, asset_id: str) -> dict:
    """One AssetRef by id; no file content is read."""
    for ref in bundle.asset_refs():
        if ref["assetId"] == asset_id:
            return {"status": OK, "asset": ref}
    return _fail(EVIDENCE_UNAVAILABLE, f"no asset '{asset_id}' in bundle")


def asset_related(bundle, asset_id: str) -> dict:
    """Parent/child relations by DerivedFromArtifactId (no content dereference)."""
    refs = bundle.asset_refs()
    by_id = {ref["assetId"]: ref for ref in refs}
    if asset_id not in by_id:
        return _fail(EVIDENCE_UNAVAILABLE, f"no asset '{asset_id}' in bundle")
    parents = []
    cursor = by_id[asset_id].get("parentAssetRef")
    while cursor and cursor in by_id:
        parents.append({"assetId": cursor, "assetType": by_id[cursor]["assetType"],
                        "sha256": by_id[cursor]["sha256"]})
        cursor = by_id[cursor].get("parentAssetRef")
    children = sorted(
        ({"assetId": ref["assetId"], "assetType": ref["assetType"],
          "sha256": ref["sha256"], "observationSeq": ref["observationSeq"]}
         for ref in refs if ref.get("parentAssetRef") == asset_id),
        key=lambda c: c["assetId"])
    return {"status": OK, "asset": by_id[asset_id], "parents": parents, "children": children}


def generate_packet(bundle, case_id: str, target_seq: int | None = None) -> dict:
    """Generate one complete P0 packet without fabricating diagnosis."""
    refs = bundle.asset_refs()
    target_observation = _target_observation(bundle, target_seq)
    target_seq_number = target_observation["observationSeq"]
    index_entries, target_frame_refs = _asset_index_entries(refs, target_seq_number)

    target_asset_ids = [r["refId"] for r in target_frame_refs]
    all_ref_ids = sorted(entry["refId"] for entry in index_entries)
    target_observation["evidenceRefs"] = target_asset_ids
    occurrence_status = "CANDIDATE" if target_seq_number is not None else "NOT_APPLICABLE"
    occurrence_summary = (
        "The capture stores a target observation but no occurrence identity; correlation remains a candidate."
        if target_seq_number is not None else
        "The capture stores no observation record, so occurrence correlation is not applicable."
    )
    occurrence_proof = (
        "Only the captureSessionId, observationSeq, frameId, and verified artifact refs are stored; "
        "StableKey is not same-occurrence proof."
        if target_seq_number is not None else
        "No recorded observation exists from which an occurrence candidate could be formed."
    )
    missing_evidence = [
        {"missingId": "evidence-chain-stages", "requiredFor": "FDP", "stage": "normalized",
         "description": "The capture does not store the normalized-to-runtime semantic causal chain.",
         "collectionHint": "Collect approved stage/trace evidence for all semantically relevant stages."},
        {"missingId": "expected-reality", "requiredFor": "FDP", "stage": None,
         "description": "The capture does not encode the expected user-visible reality.",
         "collectionHint": "Supply the approved scenario expectation before locating FirstBad."},
        {"missingId": "good-bad-comparison", "requiredFor": "DIFFERENTIAL", "stage": None,
         "description": "No controlled Good/Bad comparison is present in a single capture bundle.",
         "collectionHint": "Provide an identity-bound good packet or label comparison as not applicable."},
        {"missingId": "occurrence-identity", "requiredFor": "FDP", "stage": None,
         "description": "No OccurrenceId, StableKey, or RowId identity proof is stored in the capture.",
         "collectionHint": "Collect occurrence correlation evidence; do not infer identity from text, bounds, or index."},
        {"missingId": "owner-evidence", "requiredFor": "OWNER", "stage": None,
         "description": "Without a confirmed FirstBad seam, the production owner cannot be established.",
         "collectionHint": "Confirm FirstBad, then route the seam to its owning domain."},
    ]
    missing_stage = {
        "status": "MISSING",
        "summary": "This semantic stage is not represented by the raw capture bundle.",
        "inputRefs": [],
        "decisionRefs": [],
        "outputRefs": [],
    }
    debug_ir = {
        "SchemaVersion": "runtime-debug-ir.v0",
        "CaseId": case_id,
        "ExpectedReality": "UNAVAILABLE: expected user-visible reality is not stored in the capture bundle.",
        "ObservedReality": (
            "Stored capture terminal facts: "
            f"runtimeSucceeded={bundle.manifest.get('runtimeSucceeded')!r}, "
            f"runtimeOutcome={bundle.manifest.get('runtimeOutcome')!r}."
        ),
        "TerminalState": _terminal_state(bundle),
        "TargetObservation": target_observation,
        "TargetOccurrence": {
            "status": occurrence_status,
            "runId": None,
            "observationSeq": target_seq_number,
            "occurrenceId": None,
            "stableKey": None,
            "rowId": None,
            "spanIds": [],
            "summary": occurrence_summary,
            "proof": occurrence_proof,
            "counterevidence": ["StableKey/text/bounds/index identity evidence is absent."],
            "evidenceRefs": target_asset_ids,
        },
        "GoodComparison": {"status": "NOT_AVAILABLE", "label": "",
                           "summary": "No controlled good comparison is stored in this capture.",
                           "axes": [], "evidenceRefs": []},
        "BadComparison": {"status": "NOT_AVAILABLE", "label": "",
                          "summary": "No labelled bad comparison is stored beyond this unresolved target.",
                          "axes": [], "evidenceRefs": []},
        "EvidenceChain": {stage: dict(missing_stage) for stage in CAUSAL_STAGES},
        "LastGood": {"status": "UNRESOLVED", "stage": None,
                     "summary": "LastGood cannot be located without the missing semantic evidence chain.",
                     "evidenceRefs": []},
        "FirstBad": {"status": "UNRESOLVED", "stage": None,
                     "summary": "FirstBad cannot be inferred from the terminal symptom or raw capture alone.",
                     "evidenceRefs": []},
        "GapKind": "UNKNOWN",
        "Owner": {"status": "UNRESOLVED", "domain": "UNKNOWN", "seam": None,
                  "basis": "No confirmed FirstBad seam is present in the capture bundle.",
                  "evidenceRefs": []},
        "EvidenceRefs": all_ref_ids,
        "MissingEvidence": missing_evidence,
        "Confidence": {"level": "UNASSESSED",
                       "basis": "The packet contains verified capture facts but no semantic diagnosis.",
                       "evidenceRefs": target_asset_ids},
        "Disposition": "EVIDENCE_COLLECTION",
    }

    packet = {
        "packetVersion": "runtime-debug-evidence-packet.v0",
        "packetId": f"packet-{case_id}-generated",
        "sourceIdentity": {
            "runId": "MISSING",
            "captureSessionId": bundle.capture_session_id,
            "traceId": bundle.trace_id,
            "deploymentReceiptRef": None,
            "runtimeRevision": None,
            "environmentRef": None,
        },
        "debugIr": debug_ir,
        "evidenceIndex": index_entries,
        "repairGate": {
            "eligible": False,
            "blockers": ["DISPOSITION_NOT_MINIMAL_REPAIR", "MISSING_REQUIRED_EVIDENCE", "NO_FDP", "NO_OWNER"],
            "summary": "Evidence collection is required: FDP and Owner are unresolved; implementation is blocked.",
        },
        "generation": {
            "producer": "runtime-debug.packet-generator",
            "producerVersion": "2",
            "schemaDigest": PACKET_SCHEMA_DIGEST,
            "deterministicInputDigest": _packet_input_digest(index_entries),
        },
        "notes": [
            "Complete P0 packet generated mechanically from a validated capture bundle.",
            "Unknown diagnosis is represented by explicit absence states; no FDP, Owner, or repair is inferred.",
        ],
    }
    validate_packet(packet)
    return {"status": OK, "packet": packet}


def _asset_index_entries(refs: list[dict], target_seq: int | None) -> tuple[list[dict], list[dict]]:
    import hashlib
    entries = []
    for ref in refs:
        asset_id = ref["assetId"]
        ref_id = asset_id if all(ch.isalnum() or ch in "._:-" for ch in asset_id) \
            else f"asset:{hashlib.sha256(asset_id.encode('utf-8')).hexdigest()}"
        entries.append({
            "refId": ref_id,
            "kind": "FRAME" if ref["metadata"].get("frameId") is not None else "STAGE_ARTIFACT",
            "uri": ref["path"],
            "selector": {
                "runId": None,
                "observationSeq": ref["observationSeq"],
                "occurrenceId": None,
                "stableKey": None,
                "rowId": None,
                "evidenceRef": ref_id,
                "spanId": None,
                "frameId": ref["metadata"].get("frameId"),
                "jsonPointer": None,
                "lineAnchor": None,
            },
            "digest": f"sha256:{ref['sha256']}" if ref.get("sha256") else None,
            "integrity": "VERIFIED",
            "mediaType": ref.get("mimeType") or "application/octet-stream",
            "summary": f"Verified captured artifact (AssetRef {asset_id}).",
        })
    target_entries = [entry for entry in entries
                      if target_seq is not None and entry["selector"].get("observationSeq") == target_seq]
    return entries, target_entries


def _target_observation(bundle, target_seq: int | None) -> dict:
    observations = [r for r in bundle.records
                    if isinstance(r, dict) and r.get("kind") == "Observation"
                    and isinstance(r.get("sequenceNumber"), int)]
    if target_seq is not None:
        if not any(r["sequenceNumber"] == target_seq for r in observations):
            return {"status": "UNRESOLVED", "runId": None, "observationSeq": None,
                    "summary": f"Requested observation sequence {target_seq} is not recorded.",
                    "evidenceRefs": []}
        selected = target_seq
    else:
        selected = max((r["sequenceNumber"] for r in observations), default=None)
    return {
        "status": "UNRESOLVED",
        "runId": None,
        "observationSeq": selected,
        "summary": "Mechanically extracted from capture records; semantic evaluation pending.",
        "evidenceRefs": [],
    }


def _terminal_state(bundle) -> dict:
    outcome = bundle.manifest.get("runtimeOutcome")
    return {
        "status": "OBSERVED",
        "summary": f"Stored published manifest: finalState={bundle.manifest.get('finalState')!r}, "
                   f"runtimeSucceeded={bundle.manifest.get('runtimeSucceeded')!r}, "
                   f"runtimeOutcome={outcome!r}.",
        "evidenceRefs": [],
    }


def _packet_input_digest(entries: list[dict]) -> str:
    """Follows the P0 convention: sorted 'refId:<sha256 hex>' UTF-8 lines, each
    terminated by newline; hashed with sha256 over the joined lines."""
    import hashlib
    lines = []
    for entry in entries:
        digest = entry.get("digest") or ""
        lines.append(f"{entry['refId']}:{digest.removeprefix('sha256:')}")
    body = "".join(f"{line}\n" for line in sorted(lines))
    return f"sha256:{hashlib.sha256(body.encode('utf-8')).hexdigest()}"


def _bundle_digest(bundle) -> str:
    """Stable digest over verified AssetRef identities and content hashes."""
    import hashlib
    lines = [f"{ref['assetId']}:{ref['sha256'] or ''}" for ref in bundle.asset_refs()]
    body = "".join(f"{line}\n" for line in sorted(lines))
    return f"sha256:{hashlib.sha256(body.encode('utf-8')).hexdigest()}"


def compare_bundles(good_bundle, bad_bundle) -> dict:
    """Run compare (P2a): paired-bundle structural-facts diff of two capture
    bundles. Reports UNCHANGED/CHANGED axes plus added/removed/changed assets;
    never infers the first SEMANTICALLY relevant change (that needs semantics).

    Axis semantics: terminal (manifest stored), records (counts + last
    observation sequence), assets (by artifact id: same id + same hash =
    UNCHANGED; same id + different hash = CHANGED; presence only on one side =
    added/removed).
    """
    good = good_bundle
    bad = bad_bundle

    def terminal_view(bundle):
        runtime_succeeded = bundle.manifest.get("runtimeSucceeded")
        return {
            "finalState": bundle.manifest.get("finalState"),
            "runtimeSucceeded": runtime_succeeded,
            "runtimeOutcome": bundle.manifest.get("runtimeOutcome"),
        }

    def records_view(bundle):
        observations = [r for r in bundle.records
                        if isinstance(r, dict) and r.get("kind") == "Observation"]
        actions = [r for r in bundle.records
                   if isinstance(r, dict) and r.get("kind") in ("ActionDispatch", "ActionResult")]
        last_obs = max((r.get("sequenceNumber") for r in observations
                        if isinstance(r.get("sequenceNumber"), int)), default=None)
        return {
            "recordCount": len(bundle.records),
            "observations": len(observations),
            "actions": len(actions),
            "lastObservationSeq": last_obs,
        }

    good_terminal, bad_terminal = terminal_view(good), terminal_view(bad)
    good_records, bad_records = records_view(good), records_view(bad)

    def asset_map(bundle):
        return {ref["assetId"]: ref for ref in bundle.asset_refs()}

    good_assets, bad_assets = asset_map(good), asset_map(bad)
    added = sorted(set(bad_assets) - set(good_assets))
    removed = sorted(set(good_assets) - set(bad_assets))
    shared = sorted(set(good_assets) & set(bad_assets))
    changed_or_same = {
        asset_id: ("CHANGED" if good_assets[asset_id].get("sha256") != bad_assets[asset_id].get("sha256")
                   else "UNCHANGED")
        for asset_id in shared
    }

    return {
        "status": OK,
        "good": {
            "bundleId": good.capture_session_id,
            "traceId": good.trace_id,
            "digest": _bundle_digest(good),
            "terminal": good_terminal,
            "records": good_records,
            "assetCount": len(good_assets),
        },
        "bad": {
            "bundleId": bad.capture_session_id,
            "traceId": bad.trace_id,
            "digest": _bundle_digest(bad),
            "terminal": bad_terminal,
            "records": bad_records,
            "assetCount": len(bad_assets),
        },
        "axes": {
            "terminal": "CHANGED" if good_terminal != bad_terminal else "UNCHANGED",
            "records": "CHANGED" if good_records != bad_records else "UNCHANGED",
            "assets": "CHANGED" if added or removed or any(
                v == "CHANGED" for v in changed_or_same.values()) else "UNCHANGED",
        },
        "assets": {
            "added": added,
            "removed": removed,
            "changedOrSame": dict(sorted(changed_or_same.items())),
        },
        "note": "Structural facts only; first SEMANTICALLY relevant change is not inferred.",
    }


def diff_packets(good_packet: EvidencePacket, bad_packet: EvidencePacket) -> dict:
    """trace-diff (P2b): packet-vs-packet EvidenceChain differential.

    Mechanical only: per-stage status/refs axes (UNCHANGED/CHANGED/ADDED/REMOVED),
    the FIRST mechanically changed stage, ref add/remove lists, and both packets'
    stored LastGood/FirstBad projected verbatim. Never infers
    FIRST_SEMANTICALLY_RELEVANT_CHANGE.
    """
    good_chain = good_packet.debug_ir.get("EvidenceChain")
    bad_chain = bad_packet.debug_ir.get("EvidenceChain")
    if not isinstance(good_chain, dict) or not isinstance(bad_chain, dict):
        return _fail(INSUFFICIENT_TRACE_COVERAGE,
                     "both packets must carry an EvidenceChain for trace-diff")

    ordered_stages = list(CAUSAL_STAGES)

    stages = []
    first_mechanically_changed = None
    for stage in ordered_stages:
        good_entry = good_chain.get(stage) if isinstance(good_chain.get(stage), dict) else None
        bad_entry = bad_chain.get(stage) if isinstance(bad_chain.get(stage), dict) else None
        if good_entry is None and bad_entry is None:
            continue
        if good_entry is None:
            present = "ADDED"
        elif bad_entry is None:
            present = "REMOVED"
        else:
            present = "UNCHANGED" if good_entry.get("status") == bad_entry.get("status") else "CHANGED"
        good_refs = _stage_refs(good_entry)
        bad_refs = _stage_refs(bad_entry)
        refs_axis = "UNCHANGED" if good_refs == bad_refs else "CHANGED"
        changed = present != "UNCHANGED" or refs_axis != "UNCHANGED"
        if changed and first_mechanically_changed is None:
            first_mechanically_changed = stage
        stages.append({
            "stage": stage,
            "present": present,
            "statusAxis": present,
            "refsAxis": refs_axis,
            "good": {"status": good_entry.get("status") if good_entry else None,
                     "refs": sorted(good_refs)},
            "bad": {"status": bad_entry.get("status") if bad_entry else None,
                    "refs": sorted(bad_refs)},
        })

    def all_refs(chain):
        return {r for entry in chain.values() if isinstance(entry, dict)
                for r in _stage_refs(entry)}

    good_refs = all_refs(good_chain)
    bad_refs = all_refs(bad_chain)
    return {
        "status": OK,
        "good": {"caseId": good_packet.debug_ir.get("CaseId"), "packetId": good_packet.packet_id},
        "bad": {"caseId": bad_packet.debug_ir.get("CaseId"), "packetId": bad_packet.packet_id},
        "stages": stages,
        "firstMechanicallyChangedStage": first_mechanically_changed,
        "refs": {
            "goodOnly": sorted(good_refs - bad_refs),
            "badOnly": sorted(bad_refs - good_refs),
        },
        "storedLastGoodFirstBad": {
            "good": {"lastGood": good_packet.debug_ir.get("LastGood"),
                     "firstBad": good_packet.debug_ir.get("FirstBad")},
            "bad": {"lastGood": bad_packet.debug_ir.get("LastGood"),
                    "firstBad": bad_packet.debug_ir.get("FirstBad")},
        },
        "note": "Mechanical chain diff; FIRST_SEMANTICALLY_RELEVANT_CHANGE is not inferred.",
    }


def terminal_chain(packet: EvidencePacket) -> dict:
    """terminal-chain (P2c): mechanical terminal causal chain view.

    Projects the stored TerminalState, the ordered chain stages (status/summary/
    refs), stored LastGood/FirstBad, and — when the packet stores them — the
    diagnosis fields (GapKind/Owner/Disposition/Confidence) marked as STORED
    facts. Nothing is recomputed; absent fields stay absent.
    """
    ir = packet.debug_ir
    chain = ir["EvidenceChain"]
    stages = []
    for stage in CAUSAL_STAGES:
        entry = chain[stage]
        stages.append({
            "stage": stage,
            "status": entry["status"],
            "summary": entry["summary"],
            "refs": sorted(_stage_refs(entry)),
        })

    stored_diagnostics = {}
    for key in ("GapKind", "Confidence", "Disposition"):
        if key in ir:
            stored_diagnostics[key] = ir[key]
    owner = ir.get("Owner")
    if isinstance(owner, dict):
        stored_diagnostics["Owner"] = {k: owner.get(k) for k in ("status", "domain", "seam", "basis") if k in owner}

    result = {
        "status": OK,
        "terminalState": ir["TerminalState"],
        "chain": stages,
        "storedDiagnostics": stored_diagnostics,
        "note": "Mechanical projection of stored facts; diagnosis fields are STORED, never recomputed.",
    }
    if "LastGood" in ir:
        result["lastGood"] = ir["LastGood"]
    if "FirstBad" in ir:
        result["firstBad"] = ir["FirstBad"]
    return result


def execution_tree(bundle, hide_layers: frozenset[str] = frozenset(),
                   hide_components: frozenset[str] = frozenset(),
                   hide_names: frozenset[str] = frozenset(),
                   only_errors: bool = False,
                   time_from: int | None = None, time_to: int | None = None) -> dict:
    """execution-tree (P2d): EXECUTION tree (Run→Span→Event→ChildSpan) of the
    bundle's observability trace with multi-dimensional pruning.

    Pruning is projection-only (hidden != deleted; the bundle stays byte-identical):
      - hide_layers / hide_components / hide_names remove matched spans and their subtrees;
      - only_errors keeps FAILED/CANCELLED spans plus every ancestor on the path
        to a root (causal spine preserved);
      - time_from / time_to (monotonic offsets) keep spans overlapping the window
        plus their ancestors.
    Fails closed (EVIDENCE_UNAVAILABLE) when the bundle carries no trace.
    """
    trace = bundle.trace
    if trace is None:
        return _fail(EVIDENCE_UNAVAILABLE, "bundle has no observability trace")
    raw_spans = trace.get("spans") if isinstance(trace.get("spans"), list) else []
    def _anchor_attributes(span: dict) -> dict:
        anchors = {}
        for attribute in span.get("attributes") or []:
            if not isinstance(attribute, dict):
                continue
            key = attribute.get("key")
            if key in ("observation.seq", "observation.frame", "action.kind"):
                anchors[key] = attribute.get("value")
        return anchors

    spans = []
    for raw in raw_spans:
        if not isinstance(raw, dict):
            continue
        span = {k: raw.get(k) for k in ("spanId", "parentSpanId", "name", "layer",
                                        "component", "outcome", "startOffsetNs",
                                        "durationNs") if k in raw}
        span["anchors"] = _anchor_attributes(raw)
        spans.append(span)

    by_id = {s["spanId"]: s for s in spans}
    children_of = {}
    roots = []
    for span in spans:
        parent = span.get("parentSpanId")
        if parent and parent in by_id:
            children_of.setdefault(parent, []).append(span)
        else:
            roots.append(span)

    def descendants(span_id):
        out = {span_id}
        stack = list(children_of.get(span_id, []))
        while stack:
            node = stack.pop()
            if node["spanId"] in out:
                continue
            out.add(node["spanId"])
            stack.extend(children_of.get(node["spanId"], []))
        return out

    # Explicit structural hides (layer/component/name) cut the node and its
    # ENTIRE subtree — they are absolute exclusions.
    structural_hidden: set[str] = set()
    for span in spans:
        if span.get("name") in hide_names or span.get("layer") in hide_layers \
                or span.get("component") in hide_components:
            structural_hidden |= descendants(span["spanId"])

    # Filter hides (only_errors / time window) may be overridden by kept
    # descendants: their ancestors are re-kept to preserve the causal spine.
    def filter_hidden(span) -> bool:
        if only_errors and span.get("outcome") not in ("FAILED", "CANCELLED"):
            return True
        if time_from is not None or time_to is not None:
            start = span.get("startOffsetNs")
            duration = span.get("durationNs") or 0
            if isinstance(start, int):
                end = start + duration
                if (time_from is not None and end < time_from) or \
                   (time_to is not None and start > time_to):
                    return True
        return False

    filter_hidden_ids = {s["spanId"] for s in spans if filter_hidden(s)}
    kept = {s["spanId"] for s in spans
            if s["spanId"] not in structural_hidden and s["spanId"] not in filter_hidden_ids}
    # Re-keep ancestors of kept nodes that were hidden only by filters (spine);
    # filter-hidden leaves that are nobody's ancestor stay excluded.
    for span in spans:
        if span["spanId"] not in kept:
            continue
        cursor = span.get("parentSpanId")
        while cursor and cursor in by_id and cursor in filter_hidden_ids:
            kept.add(cursor)
            cursor = by_id[cursor].get("parentSpanId")
    # Structural hides win: re-remove anything under a structural cut.
    for span in spans:
        if span["spanId"] in structural_hidden:
            kept.discard(span["spanId"])
    hidden_by_rule = {s["spanId"] for s in spans if s["spanId"] not in kept}
    def build(span_id: str):
        span = by_id[span_id]
        children = [build(child["spanId"]) for child in sorted(
            children_of.get(span_id, []), key=lambda c: c.get("startOffsetNs") or 0)]
        return {
            "spanId": span["spanId"],
            "name": span.get("name"),
            "layer": span.get("layer"),
            "component": span.get("component"),
            "outcome": span.get("outcome"),
            "startOffsetNs": span.get("startOffsetNs"),
            "durationNs": span.get("durationNs"),
            "anchors": span.get("anchors", {}),
            "children": [c for c in children if c["spanId"] in kept],
        }

    # Span → AssetRef join by observation sequence (evidence anchors; candidate
    # correlation only — never world truth). Assets of the same observation seq.
    frame_assets_by_seq = {}
    for ref in bundle.asset_refs():
        obs_seq = ref.get("observationSeq")
        if obs_seq is not None:
            frame_assets_by_seq.setdefault(obs_seq, []).append({
                "assetId": ref["assetId"], "path": ref["path"],
                "contentHash": ref["sha256"], "frameId": ref["metadata"].get("frameId")})

    def attach_anchors(node):
        obs_seq = node.get("anchors", {}).get("observation.seq")
        if obs_seq is not None:
            try:
                seq = int(obs_seq)
            except (TypeError, ValueError):
                seq = None
            node["observationSeq"] = seq
            node["frameAssetRefs"] = sorted(frame_assets_by_seq.get(seq, []),
                                            key=lambda a: a["assetId"])
        node["actionKind"] = node.get("anchors", {}).get("action.kind")
        for child in node.get("children") or []:
            attach_anchors(child)
        return node

    visible_roots = [attach_anchors(build(r["spanId"])) for r in roots if r["spanId"] in kept]
    return {
        "status": OK,
        "kind": "EXECUTION",
        "treeId": {"traceId": trace.get("traceId"), "traceRunId": trace.get("traceRunId")},
        "roots": visible_roots,
        "stats": {"totalSpanCount": len(spans), "shownSpanCount": len(kept),
                  "hiddenSpanCount": len(hidden_by_rule)},
        "pruned": {
            "layers": sorted(hide_layers), "components": sorted(hide_components),
            "names": sorted(hide_names), "onlyErrors": only_errors,
            "window": {"from": time_from, "to": time_to},
        },
        "note": "Pruning is projection-only; the bundle and trace stay byte-identical.",
    }


def _stage_refs(entry: dict | None) -> set:
    if not isinstance(entry, dict):
        return set()
    refs = set()
    for key in ("inputRefs", "decisionRefs", "outputRefs"):
        value = entry.get(key)
        if isinstance(value, list):
            refs.update(r for r in value if isinstance(r, str))
    return refs


def _comparison_projection(comparison: dict) -> dict:
    return {
        "status": comparison.get("status"),
        "label": comparison.get("label"),
        "summary": comparison.get("summary"),
        "axes": comparison.get("axes") or [],
        "evidenceRefs": sorted(comparison.get("evidenceRefs") or []),
    }


def _missing_evidence_entry(value) -> dict:
    if isinstance(value, str):
        return {"missingId": None, "requiredFor": None, "description": value}
    if isinstance(value, dict):
        return {k: value.get(k) for k in ("missingId", "requiredFor", "stage", "description") if k in value}
    return {"missingId": None, "requiredFor": None, "description": str(value)}


def summarize(packet: EvidencePacket) -> dict:
    """Contract-limited projection of terminal, target scope, evidence availability,
    missing evidence, and repair blockers. Not a diagnostic."""
    target_observation = packet.target_observation
    target_occurrence = packet.target_occurrence
    index = packet.index_by_ref_id()
    ir_refs = packet.ir_evidence_refs
    integrity = "OK" if all(ref in index for ref in ir_refs) else "MISSING_REF"

    return {
        "terminalState": packet.debug_ir.get("TerminalState"),
        "targetObservation": _scoped(target_observation),
        "targetOccurrence": _scoped(target_occurrence),
        "evidenceAvailability": {
            "count": len(index),
            "integrity": integrity,
            "refs": sorted(index),
        },
        "missingEvidence": [_missing_evidence_entry(e) for e in packet.missing_evidence],
        "repairBlockers": sorted(packet.repair_gate.get("blockers") or []),
    }


def occurrence(packet: EvidencePacket, selector: str | None, selector_kind: str) -> dict:
    """Typed occurrence query. Exactly one selector_kind is guaranteed by the CLI
    (INVALID_INPUT otherwise). Returns candidates with status; never guesses identity."""
    occurrence = packet.target_occurrence
    index = packet.index_by_ref_id()
    field = _SELECTOR_FIELD_MAP.get(selector_kind, selector_kind)

    if field == "evidenceRef":
        linked_entries = [index.get(selector)] if selector in index else []
        target_matches = selector in (occurrence.get("evidenceRefs") or [])
    else:
        linked_entries = [entry for entry in index.values()
                          if entry.get("selector", {}).get(field) == selector]
        target_matches = bool(occurrence.get(field)) and occurrence.get(field) == selector

    # Candidate pool: the stored TargetOccurrence when it matches, plus every
    # indexed evidence coordinate that matches (used for evidence-ref / coverage).
    candidates = []
    if target_matches:
        candidates.append(_candidate_from_target(packet, occurrence))
    for entry in linked_entries:
        if not target_matches:
            # Indexed coordinate without a full target occurrence — not enough to
            # prove an occurrence; coverage is insufficient.
            candidates.append(_candidate_from_indexed(entry))

    if not candidates:
        return _fail(EVIDENCE_UNAVAILABLE,
                     f"no candidate matches {selector_kind}='{selector}'")

    # Integrity: an indexed evidence stored as IDENTITY_MISMATCH surfaces that status.
    for entry in linked_entries:
        if entry.get("integrity") == IDENTITY_MISMATCH:
            return _fail(IDENTITY_MISMATCH,
                         f"evidence '{entry.get('refId')}' is stored as IDENTITY_MISMATCH")

    # Ambiguity: several incompatible stored identity tuples for one selector.
    tuples = {_identity_tuple(c) for c in candidates}
    if len(tuples) > 1:
        return {
            "status": AMBIGUOUS_OCCURRENCE,
            "candidates": sorted(candidates, key=_sort_key),
            "diagnostic": (
                f"selector {selector_kind}='{selector}' maps to "
                f"{len(tuples)} incompatible stored identities"
            ),
        }

    # Insufficient coverage: candidates without a full target occurrence.
    if not target_matches:
        return _fail(INSUFFICIENT_TRACE_COVERAGE,
                     f"selector {selector_kind}='{selector}' hits only indexed evidence, no TargetOccurrence")

    ordered = sorted(candidates, key=_sort_key)
    return {"status": OK, "candidates": ordered}


def _candidate_from_target(packet: EvidencePacket, occurrence: dict) -> dict:
    index = packet.index_by_ref_id()
    linked = []
    for ref in sorted(occurrence.get("evidenceRefs") or packet.ir_evidence_refs):
        entry = index.get(ref)
        if entry is not None:
            linked.append({
                "refId": entry.get("refId"),
                "kind": entry.get("kind"),
                "uri": entry.get("uri"),
                "integrity": entry.get("integrity", "OK"),
                "selector": entry.get("selector") or {},
            })
    return {
        "source": "TargetOccurrence",
        "status": occurrence.get("status"),
        "runId": occurrence.get("runId"),
        "observationSeq": occurrence.get("observationSeq"),
        "occurrenceId": occurrence.get("occurrenceId"),
        "stableKey": occurrence.get("stableKey"),
        "rowId": occurrence.get("rowId"),
        "spanIds": list(occurrence.get("spanIds") or []),
        "summary": occurrence.get("summary"),
        "proof": occurrence.get("proof"),
        "counterevidence": occurrence.get("counterevidence"),
        "linkedEvidence": linked,
    }


def _candidate_from_indexed(entry: dict) -> dict:
    selector = entry.get("selector") or {}
    return {
        "source": "evidenceIndex",
        "status": "INDEXED",
        "runId": selector.get("runId"),
        "observationSeq": selector.get("observationSeq"),
        "occurrenceId": selector.get("occurrenceId"),
        "stableKey": selector.get("stableKey"),
        "rowId": selector.get("rowId"),
        "spanIds": [selector["spanId"]] if selector.get("spanId") else [],
        "summary": None,
        "proof": None,
        "counterevidence": None,
        "linkedEvidence": [{
            "refId": entry.get("refId"),
            "kind": entry.get("kind"),
            "uri": entry.get("uri"),
            "integrity": entry.get("integrity", "OK"),
            "selector": selector,
        }],
    }


def _scoped(source: dict) -> dict:
    """Stored scope/status only — never recompute semantic claims from a packet."""
    keys = ("status", "runId", "observationSeq", "occurrenceId", "stableKey", "rowId", "summary")
    return {k: source.get(k) for k in keys if k in source}


def _identity_tuple(candidate: dict) -> tuple:
    return (candidate.get("runId"), candidate.get("observationSeq"),
            candidate.get("occurrenceId"), candidate.get("stableKey"), candidate.get("rowId"))


def _sort_key(candidate: dict) -> tuple:
    # RunId(null last) → ObservationSeq(null last) → OccurrenceId → StableKey → RowId → refId
    def null_last(value):
        return (value is None, value if value is not None else "")

    ref = (candidate.get("linkedEvidence") or [{}])[0].get("refId") or ""
    return (null_last(candidate.get("runId")), null_last(candidate.get("observationSeq")),
            null_last(candidate.get("occurrenceId")), null_last(candidate.get("stableKey")),
            null_last(candidate.get("rowId")), ref)


def _fail(status: str, message: str) -> dict:
    return {"status": status, "candidates": None, "diagnostic": message}
