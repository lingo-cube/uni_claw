"""Replay fixture facts (P4a) — pure, deterministic Core projections.

Scope: `replay-extract` builds a replay fixture from a capture bundle
(records + AssetRefs + trace summary), and `replay` validates one fixture and
summarizes it. Minimization (RED → minimal falsifier → repair → GREEN) is a
contract placeholder only — this module never mutates or minimizes anything.
Zero Runtime/device access; read-only; fail-closed.
"""

from __future__ import annotations

import hashlib
import json
from typing import Any

from .sources import bundle as bundle_source
from .status import EVIDENCE_UNAVAILABLE, SCHEMA_VIOLATION

REPLAY_SCHEMA = "runtime-debug-replay.v0"

_STEP_FIELDS = ("order", "kind", "sequenceNumber", "frameId", "actionId",
                "actionKind", "targetIndex", "targetState", "resultOutcome")


def build_replay_fixture(bundle, case_id: str) -> dict:
    """Mechanical replay fixture from one bundle: ordered records as steps,
    AssetRefs, and a trace summary. Stored facts only — no inferred state."""
    steps = []
    for record in bundle.records:
        step = {field: record.get(field) for field in _STEP_FIELDS if field in record}
        steps.append(step)

    assets = [{
        "assetId": ref["assetId"],
        "contentHash": ref["sha256"],
        "path": ref["path"],
        "observationSeq": ref["observationSeq"],
        "frameId": ref["metadata"].get("frameId"),
    } for ref in bundle.asset_refs()]

    trace_summary = None
    if bundle.trace is not None:
        spans = bundle.trace.get("spans") if isinstance(bundle.trace.get("spans"), list) else []
        trace_summary = {"traceId": bundle.trace.get("traceId"), "spanCount": len(spans)}

    fixture = {
        "schemaVersion": REPLAY_SCHEMA,
        "replayId": f"{case_id}-{bundle.capture_session_id}",
        "caseId": case_id,
        "scope": {"applicationIdentity": None, "semanticRoot": None},
        "steps": steps,
        "assets": sorted(assets, key=lambda a: a["assetId"]),
        "trace": trace_summary,
        "generation": {
            "producer": "runtime-debug.replay-extract",
            "producerVersion": "1",
            "deterministicInputDigest": _fixture_digest(bundle),
        },
    }
    return fixture


def validate_replay_fixture(raw: Any) -> dict:
    """Fail-closed validation + summary of one fixture; returns a summary dict
    with status 'OK' or raises FixtureError."""
    if not isinstance(raw, dict):
        raise FixtureError(SCHEMA_VIOLATION, "replay fixture must be an object")
    schema = raw.get("schemaVersion")
    if not isinstance(schema, str) or not schema.startswith("runtime-debug-replay"):
        raise FixtureError(SCHEMA_VIOLATION, "replay fixture schemaVersion must reference the replay schema")
    replay_id = raw.get("replayId")
    if not isinstance(replay_id, str) or not replay_id:
        raise FixtureError(SCHEMA_VIOLATION, "replay fixture replayId must be a non-empty string")
    case_id = raw.get("caseId")
    if not isinstance(case_id, str) or not case_id:
        raise FixtureError(SCHEMA_VIOLATION, "replay fixture caseId must be a non-empty string")
    steps = raw.get("steps")
    if not isinstance(steps, list):
        raise FixtureError(SCHEMA_VIOLATION, "replay fixture steps must be a list")
    seen_orders: set[int] = set()
    for index, step in enumerate(steps):
        if not isinstance(step, dict):
            raise FixtureError(SCHEMA_VIOLATION, f"replay step {index} must be an object")
        order = step.get("order")
        if not isinstance(order, int) or order < 1 or order in seen_orders:
            raise FixtureError(SCHEMA_VIOLATION, f"replay step {index} order must be a unique positive integer")
        seen_orders.add(order)
    assets = raw.get("assets")
    if not isinstance(assets, list) or not all(isinstance(a, dict) and isinstance(a.get("assetId"), str) for a in assets):
        raise FixtureError(SCHEMA_VIOLATION, "replay fixture assets must be a list of asset objects")
    return {
        "status": "OK",
        "caseId": case_id,
        "replayId": replay_id,
        "stepCount": len(steps),
        "assetCount": len(assets),
        "spanCount": (raw.get("trace") or {}).get("spanCount"),
        "digest": (raw.get("generation") or {}).get("deterministicInputDigest"),
    }


def project_replay_run(fixture: dict) -> dict:
    """Deterministic dry-run projection over one replay fixture (P4b).

    Replays the STORED step sequence mechanically — no device, no Runtime, no
    state simulation. Outputs the ordered trajectory, action/observation counts,
    and the first mechanically non-OK step. Minimization stays a contract.
    """
    summary = validate_replay_fixture(fixture)
    steps_view = []
    observation_count = 0
    action_count = 0
    failed_steps = []
    last_observation_seq = None
    for step in fixture.get("steps") or []:
        kind = step.get("kind")
        sequence = step.get("sequenceNumber")
        if kind == "Observation":
            observation_count += 1
            if isinstance(sequence, int):
                last_observation_seq = max(last_observation_seq or 0, sequence)
        elif kind in ("ActionDispatch", "ActionResult"):
            action_count += 1
        outcome = step.get("resultOutcome")
        if isinstance(outcome, str) and outcome not in ("Dispatched", "Succeeded"):
            failed_steps.append(step.get("order"))
        steps_view.append({
            "order": step.get("order"),
            "kind": kind,
            "sequenceNumber": sequence,
            "actionId": step.get("actionId"),
            "actionKind": step.get("actionKind"),
            "targetIndex": step.get("targetIndex"),
            "targetState": step.get("targetState"),
            "resultOutcome": outcome,
            "frameId": step.get("frameId"),
        })
    return {
        "status": "OK",
        "caseId": summary["caseId"],
        "replayId": summary["replayId"],
        "trajectory": steps_view,
        "counts": {
            "steps": len(steps_view),
            "observations": observation_count,
            "actions": action_count,
            "lastObservationSeq": last_observation_seq,
        },
        "firstMechanicallyFailedStep": failed_steps[0] if failed_steps else None,
        "note": "Mechanical dry-run over stored steps; no state simulation or minimization.",
    }


def minimize_fixture(fixture: dict) -> dict:
    """Deterministic greedy minimization (P4c): the smallest step subset that
    still fails the mechanical projection (`firstMechanicallyFailedStep` stays
    intact). Read-only — returns a minimal-slice view, never mutates the input.

    Mechanical only: "still fails" == the same stored non-OK resultOutcome
    remains present. Semantic sufficiency is out of scope (a later gate).
    """
    validate_replay_fixture(fixture)
    projection = project_replay_run(fixture)
    failed_order = projection["firstMechanicallyFailedStep"]
    if failed_order is None:
        return {
            "status": "OK",
            "caseId": projection["caseId"],
            "replayId": projection["replayId"],
            "hadFailure": False,
            "minimalSteps": list(fixture.get("steps") or []),
            "removedOrders": [],
            "iterations": 0,
            "note": "No mechanical failure to minimize.",
        }

    steps = list(fixture.get("steps") or [])
    # The failing step itself must stay; anything after it cannot matter for the
    # mechanical predicate and is dropped. Work from the failing step backward,
    # greedily dropping each earlier step while the predicate still holds.
    retained = [s for s in steps if (s.get("order") or 0) <= failed_order]
    removed = [s.get("order") for s in steps if (s.get("order") or 0) > failed_order]
    iterations = 1
    index = len(retained) - 1
    while index >= 0:
        step = retained[index]
        if (step.get("order") or 0) == failed_order:
            index -= 1
            continue
        candidate = [s for s in retained if s is not step]
        if _still_fails(fixture, candidate) == failed_order:
            retained = candidate
            removed.append(step.get("order"))
        index -= 1
        iterations += 1

    return {
        "status": "OK",
        "caseId": projection["caseId"],
        "replayId": projection["replayId"],
        "hadFailure": True,
        "minimalSteps": [dict(s) for s in retained],
        "removedOrders": sorted(removed),
        "iterations": iterations,
        "note": "Mechanical minimal failure-preserving slice; semantic sufficiency is out of scope.",
    }


def _still_fails(fixture: dict, steps: list[dict]) -> int | None:
    variant = dict(fixture)
    variant["steps"] = steps
    return project_replay_run(variant)["firstMechanicallyFailedStep"]


def read_fixture_file(path: str) -> dict:
    """Read one fixture file (fail-closed)."""
    if not __import__("os").path.isfile(path):
        raise FixtureError(EVIDENCE_UNAVAILABLE, "replay fixture file not found")
    try:
        with open(path, "rb") as handle:
            return json.loads(handle.read().decode("utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise FixtureError(SCHEMA_VIOLATION, f"replay fixture is not valid UTF-8 JSON: {exc}") from exc


class FixtureError(Exception):
    """Fail-closed fixture failure carrying a closed status."""

    def __init__(self, status: str, message: str):
        super().__init__(message)
        self.status = status
        self.message = message


def _fixture_digest(bundle) -> str:
    """Deterministic digest over the bundle's stored facts (records + assets),
    following the P0 digest convention (sorted 'refId:<sha256>' lines)."""
    payload = []
    for record in sorted(bundle.records, key=lambda r: r.get("order") or 0):
        payload.append(f"step-{record.get('order')}:{record.get('kind')}:{record.get('sequenceNumber')}")
    for ref in sorted(bundle.asset_refs(), key=lambda a: a["assetId"]):
        payload.append(f"{ref['assetId']}:{ref['sha256'] or ''}")
    body = "".join(f"{line}\n" for line in payload)
    return f"sha256:{hashlib.sha256(body.encode('utf-8')).hexdigest()}"