"""Fail-closed reader for the frozen Runtime Debug Evidence Packet v0.

Validation mirrors the frozen P0 JSON Schemas and additionally enforces
packet-level reference closure and repair-gate equations. EvidenceRef URIs are
metadata; this reader never dereferences them.
"""

from __future__ import annotations

import json
import re
from typing import Any, Callable

from .status import SCHEMA_VIOLATION

PACKET_VERSION = "runtime-debug-evidence-packet.v0"
DEBUG_IR_VERSION = "runtime-debug-ir.v0"
PACKET_SCHEMA_DIGEST = "sha256:27daf6e400bc14eb053a154c308099132c0dfb6f161065d32ddd593d281a6d27"

STAGES = ("raw", "normalized", "fused", "canonical",
          "semanticAdmission", "affordance", "runtimeState")
DISPOSITIONS = frozenset(("EVIDENCE_COLLECTION", "MINIMAL_REPAIR",
                          "ARCHITECTURE_GATE", "ENVIRONMENT_GATE",
                          "INSUFFICIENT_EVIDENCE"))
GAP_KINDS = frozenset((
    "EVIDENCE_AVAILABILITY_GAP", "CONTRACT_REGRESSION", "REPRESENTATION_DRIFT",
    "CORRELATION_GAP", "COMPOSITION_GAP", "DECISION_LOGIC_GAP",
    "NUMERICAL_BOUNDARY_GAP", "CAPABILITY_COVERAGE_GAP", "BOUNDED_POLICY_GAP",
    "ENVIRONMENT_GAP", "TRACE_COVERAGE_GAP", "ARCHITECTURE_OWNERSHIP_GAP", "UNKNOWN",
))
OWNER_DOMAINS = frozenset((
    "AGENT", "CONTAINER", "TRAVERSAL", "ENVIRONMENT", "DEVICE_ADAPTER",
    "RUNTIME_PERCEPTION", "RUNTIME_WORLD", "SEMANTIC_CAPABILITY", "VISION_FUSION",
    "TEST_HARNESS", "VALIDATION_HARNESS", "DEPLOYMENT_COMPOSITION",
    "MULTI_OWNER_GATE", "UNKNOWN",
))
EVIDENCE_KINDS = frozenset((
    "RUN_REPORT", "RUNTIME_TRACE", "SPAN_TRACE", "FUSION_TRACE", "STAGE_ARTIFACT",
    "FRAME", "OBSERVATION", "ACTION_HISTORY", "REPLAY", "TEST_RESULT", "RECEIPT",
    "DECISION", "CODE_SYMBOL",
))
REPAIR_BLOCKERS = frozenset((
    "NO_FDP", "NO_OWNER", "INSUFFICIENT_EVIDENCE", "MISSING_REQUIRED_EVIDENCE",
    "AMBIGUOUS_OCCURRENCE", "IDENTITY_MISMATCH", "DISPOSITION_NOT_MINIMAL_REPAIR",
    "ARCHITECTURE_GATE_REQUIRED", "ENVIRONMENT_GATE_REQUIRED",
))
_REF_PATTERN = re.compile(r"^[A-Za-z0-9._:-]+$")
_DIGEST_PATTERN = re.compile(r"^sha256:[a-f0-9]{64}$")


class PacketError(Exception):
    """Fail-closed reader failure carrying the closed status and a message."""

    def __init__(self, status: str, message: str):
        super().__init__(message)
        self.status = status
        self.message = message


class EvidencePacket:
    """Validated P0 packet. Read-only views; no mutation APIs."""

    def __init__(self, raw: dict, packet_version: str, packet_id: str,
                 source_identity: dict, debug_ir: dict, evidence_index: list[dict],
                 repair_gate: dict):
        self._raw = raw
        self.packet_version = packet_version
        self.packet_id = packet_id
        self.source_identity = source_identity
        self.debug_ir = debug_ir
        self.evidence_index = evidence_index
        self.repair_gate = repair_gate

    @property
    def target_observation(self) -> dict:
        return self.debug_ir["TargetObservation"]

    @property
    def target_occurrence(self) -> dict:
        return self.debug_ir["TargetOccurrence"]

    @property
    def missing_evidence(self) -> list:
        return self.debug_ir["MissingEvidence"]

    @property
    def ir_evidence_refs(self) -> list[str]:
        return sorted(self.debug_ir["EvidenceRefs"])

    def index_by_ref_id(self) -> dict[str, dict]:
        return {entry["refId"]: entry for entry in self.evidence_index}


def _expect(condition: bool, message: str, field: str) -> None:
    if not condition:
        raise PacketError(SCHEMA_VIOLATION, f"{field}: {message}")


def _object(value: Any, field: str, required: tuple[str, ...],
            optional: tuple[str, ...] = ()) -> dict:
    _expect(isinstance(value, dict), "must be an object", field)
    missing = [name for name in required if name not in value]
    _expect(not missing, f"missing required field(s): {', '.join(missing)}", field)
    extras = sorted(set(value) - frozenset(required + optional))
    _expect(not extras, f"unknown field(s): {', '.join(extras)}", field)
    return value


def _nonempty(value: Any, field: str) -> str:
    _expect(isinstance(value, str) and len(value) > 0, "must be a non-empty string", field)
    return value


def _string(value: Any, field: str) -> str:
    _expect(isinstance(value, str), "must be a string", field)
    return value


def _enum(value: Any, allowed: frozenset[str] | tuple[str, ...], field: str) -> str:
    _expect(isinstance(value, str) and value in allowed,
            f"must be one of {', '.join(sorted(allowed))}", field)
    return value


def _nullable_string(value: Any, field: str) -> None:
    _expect(value is None or isinstance(value, str), "must be a string or null", field)


def _nullable_seq(value: Any, field: str) -> None:
    _expect(value is None or (isinstance(value, int) and not isinstance(value, bool) and value >= 0),
            "must be a non-negative integer or null", field)


def _ref(value: Any, field: str) -> str:
    _expect(isinstance(value, str) and _REF_PATTERN.fullmatch(value) is not None,
            "must be a valid EvidenceRef id", field)
    return value


def _array(value: Any, field: str, item: Callable[[Any, str], Any], *, unique: bool = False) -> list:
    _expect(isinstance(value, list), "must be an array", field)
    for index, entry in enumerate(value):
        item(entry, f"{field}[{index}]")
    if unique:
        _expect(len(value) == len(set(value)), "must contain unique values", field)
    return value


def _refs(value: Any, field: str) -> list[str]:
    return _array(value, field, _ref, unique=True)


def _digest(value: Any, field: str, nullable: bool = False) -> None:
    if nullable and value is None:
        return
    _expect(isinstance(value, str) and _DIGEST_PATTERN.fullmatch(value) is not None,
            "must be a sha256 digest", field)


def _terminal(value: Any) -> dict:
    field = "debugIr.TerminalState"
    obj = _object(value, field, ("status", "summary", "evidenceRefs"))
    _enum(obj["status"], frozenset(("OBSERVED", "NOT_REACHED", "UNAVAILABLE")), f"{field}.status")
    _nonempty(obj["summary"], f"{field}.summary")
    _refs(obj["evidenceRefs"], f"{field}.evidenceRefs")
    return obj


def _target_observation(value: Any) -> dict:
    field = "debugIr.TargetObservation"
    obj = _object(value, field, ("status", "runId", "observationSeq", "summary", "evidenceRefs"))
    _enum(obj["status"], frozenset(("CONFIRMED", "UNRESOLVED", "NOT_APPLICABLE")), f"{field}.status")
    _nullable_string(obj["runId"], f"{field}.runId")
    _nullable_seq(obj["observationSeq"], f"{field}.observationSeq")
    _nonempty(obj["summary"], f"{field}.summary")
    _refs(obj["evidenceRefs"], f"{field}.evidenceRefs")
    return obj


def _target_occurrence(value: Any) -> dict:
    field = "debugIr.TargetOccurrence"
    obj = _object(value, field, (
        "status", "runId", "observationSeq", "occurrenceId", "stableKey", "rowId",
        "spanIds", "summary", "proof", "counterevidence", "evidenceRefs",
    ))
    _enum(obj["status"], frozenset(("CONFIRMED", "CANDIDATE", "AMBIGUOUS", "NOT_APPLICABLE")), f"{field}.status")
    for name in ("runId", "occurrenceId", "stableKey", "rowId"):
        _nullable_string(obj[name], f"{field}.{name}")
    _nullable_seq(obj["observationSeq"], f"{field}.observationSeq")
    _array(obj["spanIds"], f"{field}.spanIds", _string, unique=True)
    _nonempty(obj["summary"], f"{field}.summary")
    _nonempty(obj["proof"], f"{field}.proof")
    _array(obj["counterevidence"], f"{field}.counterevidence", _nonempty)
    _refs(obj["evidenceRefs"], f"{field}.evidenceRefs")
    return obj


def _axis(value: Any, field: str) -> dict:
    obj = _object(value, field, ("name", "status", "value"))
    _nonempty(obj["name"], f"{field}.name")
    _enum(obj["status"], frozenset(("CONTROLLED", "INTENTIONALLY_CHANGED", "UNKNOWN")), f"{field}.status")
    _string(obj["value"], f"{field}.value")
    return obj


def _comparison(value: Any, field: str) -> dict:
    obj = _object(value, field, ("status", "label", "summary", "axes", "evidenceRefs"))
    _enum(obj["status"], frozenset(("AVAILABLE", "NOT_AVAILABLE", "NOT_APPLICABLE")), f"{field}.status")
    _string(obj["label"], f"{field}.label")
    _nonempty(obj["summary"], f"{field}.summary")
    axes = _array(obj["axes"], f"{field}.axes", _axis)
    names = [axis["name"] for axis in axes]
    _expect(len(names) == len(set(names)), "axis names must be unique", f"{field}.axes")
    _refs(obj["evidenceRefs"], f"{field}.evidenceRefs")
    return obj


def _stage(value: Any, field: str) -> dict:
    obj = _object(value, field, ("status", "summary", "inputRefs", "decisionRefs", "outputRefs"))
    _enum(obj["status"], frozenset(("PRESENT", "MISSING", "NOT_APPLICABLE")), f"{field}.status")
    _nonempty(obj["summary"], f"{field}.summary")
    for role in ("inputRefs", "decisionRefs", "outputRefs"):
        _refs(obj[role], f"{field}.{role}")
    return obj


def _evidence_chain(value: Any) -> dict:
    obj = _object(value, "debugIr.EvidenceChain", STAGES)
    for stage in STAGES:
        _stage(obj[stage], f"debugIr.EvidenceChain.{stage}")
    return obj


def _divergence(value: Any, field: str) -> dict:
    obj = _object(value, field, ("status", "stage", "summary", "evidenceRefs"))
    _enum(obj["status"], frozenset(("CONFIRMED", "UNRESOLVED", "NOT_APPLICABLE")), f"{field}.status")
    _expect(obj["stage"] is None or obj["stage"] in STAGES, "must be a canonical stage or null", f"{field}.stage")
    _nonempty(obj["summary"], f"{field}.summary")
    _refs(obj["evidenceRefs"], f"{field}.evidenceRefs")
    return obj


def _owner(value: Any) -> dict:
    field = "debugIr.Owner"
    obj = _object(value, field, ("status", "domain", "seam", "basis", "evidenceRefs"))
    _enum(obj["status"], frozenset(("CONFIRMED", "CANDIDATE", "UNRESOLVED")), f"{field}.status")
    _enum(obj["domain"], OWNER_DOMAINS, f"{field}.domain")
    _nullable_string(obj["seam"], f"{field}.seam")
    _nonempty(obj["basis"], f"{field}.basis")
    _refs(obj["evidenceRefs"], f"{field}.evidenceRefs")
    return obj


def _missing(value: Any, field: str) -> dict:
    obj = _object(value, field, ("missingId", "requiredFor", "stage", "description", "collectionHint"))
    _nonempty(obj["missingId"], f"{field}.missingId")
    _enum(obj["requiredFor"], frozenset(("FDP", "OWNER", "DIFFERENTIAL", "REPAIR",
                                         "FRESH_CONFIRMATION", "PACKET_INTEGRITY")), f"{field}.requiredFor")
    _expect(obj["stage"] is None or obj["stage"] in STAGES, "must be a canonical stage or null", f"{field}.stage")
    _nonempty(obj["description"], f"{field}.description")
    _string(obj["collectionHint"], f"{field}.collectionHint")
    return obj


def _confidence(value: Any) -> dict:
    field = "debugIr.Confidence"
    obj = _object(value, field, ("level", "basis", "evidenceRefs"))
    _enum(obj["level"], frozenset(("CONFIRMED", "HIGH", "MEDIUM", "LOW", "UNASSESSED")), f"{field}.level")
    _nonempty(obj["basis"], f"{field}.basis")
    _refs(obj["evidenceRefs"], f"{field}.evidenceRefs")
    return obj


def _debug_ir(value: Any) -> dict:
    required = ("SchemaVersion", "CaseId", "ExpectedReality", "ObservedReality", "TerminalState",
                "TargetObservation", "TargetOccurrence", "GoodComparison", "BadComparison",
                "EvidenceChain", "LastGood", "FirstBad", "GapKind", "Owner", "EvidenceRefs",
                "MissingEvidence", "Confidence", "Disposition")
    ir = _object(value, "debugIr", required)
    _expect(ir["SchemaVersion"] == DEBUG_IR_VERSION, f"must equal '{DEBUG_IR_VERSION}'", "debugIr.SchemaVersion")
    for name in ("CaseId", "ExpectedReality", "ObservedReality"):
        _nonempty(ir[name], f"debugIr.{name}")
    _terminal(ir["TerminalState"])
    _target_observation(ir["TargetObservation"])
    _target_occurrence(ir["TargetOccurrence"])
    _comparison(ir["GoodComparison"], "debugIr.GoodComparison")
    _comparison(ir["BadComparison"], "debugIr.BadComparison")
    _evidence_chain(ir["EvidenceChain"])
    _divergence(ir["LastGood"], "debugIr.LastGood")
    _divergence(ir["FirstBad"], "debugIr.FirstBad")
    _enum(ir["GapKind"], GAP_KINDS, "debugIr.GapKind")
    _owner(ir["Owner"])
    _refs(ir["EvidenceRefs"], "debugIr.EvidenceRefs")
    missing = _array(ir["MissingEvidence"], "debugIr.MissingEvidence", _missing)
    missing_ids = [entry["missingId"] for entry in missing]
    _expect(len(missing_ids) == len(set(missing_ids)), "missingId values must be unique", "debugIr.MissingEvidence")
    _confidence(ir["Confidence"])
    _enum(ir["Disposition"], DISPOSITIONS, "debugIr.Disposition")
    return ir


def _source_identity(value: Any) -> dict:
    field = "sourceIdentity"
    obj = _object(value, field, ("runId", "captureSessionId", "traceId", "deploymentReceiptRef",
                                 "runtimeRevision", "environmentRef"))
    _nonempty(obj["runId"], f"{field}.runId")
    for name in ("captureSessionId", "traceId", "deploymentReceiptRef", "runtimeRevision", "environmentRef"):
        _nullable_string(obj[name], f"{field}.{name}")
    return obj


def _selector(value: Any, field: str) -> dict:
    names = ("runId", "observationSeq", "occurrenceId", "stableKey", "rowId", "evidenceRef",
             "spanId", "frameId", "jsonPointer", "lineAnchor")
    obj = _object(value, field, names)
    _nullable_seq(obj["observationSeq"], f"{field}.observationSeq")
    for name in set(names) - {"observationSeq"}:
        _nullable_string(obj[name], f"{field}.{name}")
    return obj


def _evidence_entry(value: Any, field: str) -> dict:
    obj = _object(value, field, ("refId", "kind", "uri", "selector", "digest", "integrity", "mediaType", "summary"))
    _ref(obj["refId"], f"{field}.refId")
    _enum(obj["kind"], EVIDENCE_KINDS, f"{field}.kind")
    _nonempty(obj["uri"], f"{field}.uri")
    _selector(obj["selector"], f"{field}.selector")
    _digest(obj["digest"], f"{field}.digest", nullable=True)
    _enum(obj["integrity"], frozenset(("VERIFIED", "UNVERIFIED", "MISSING", "IDENTITY_MISMATCH")), f"{field}.integrity")
    _nullable_string(obj["mediaType"], f"{field}.mediaType")
    _nonempty(obj["summary"], f"{field}.summary")
    return obj


def _repair_gate(value: Any) -> dict:
    field = "repairGate"
    obj = _object(value, field, ("eligible", "blockers", "summary"))
    _expect(isinstance(obj["eligible"], bool), "must be a boolean", f"{field}.eligible")
    blockers = _array(obj["blockers"], f"{field}.blockers",
                      lambda v, f: _enum(v, REPAIR_BLOCKERS, f), unique=True)
    _nonempty(obj["summary"], f"{field}.summary")
    _expect(obj["eligible"] == (len(blockers) == 0),
            "eligible must be true exactly when blockers is empty", field)
    return obj


def _generation(value: Any) -> dict:
    field = "generation"
    obj = _object(value, field, ("producer", "producerVersion", "schemaDigest", "deterministicInputDigest"))
    _nonempty(obj["producer"], f"{field}.producer")
    _nonempty(obj["producerVersion"], f"{field}.producerVersion")
    _digest(obj["schemaDigest"], f"{field}.schemaDigest")
    _expect(obj["schemaDigest"] == PACKET_SCHEMA_DIGEST,
            f"must identify the frozen {PACKET_VERSION} schema", f"{field}.schemaDigest")
    _digest(obj["deterministicInputDigest"], f"{field}.deterministicInputDigest")
    return obj


def _derived_view(value: Any, field: str) -> dict:
    obj = _object(value, field, ("kind", "summary", "evidenceRefs"))
    _enum(obj["kind"], frozenset(("SUMMARY", "OCCURRENCE_TIMELINE", "TRACE_DIFF", "TERMINAL_CHAIN")), f"{field}.kind")
    _nonempty(obj["summary"], f"{field}.summary")
    _refs(obj["evidenceRefs"], f"{field}.evidenceRefs")
    return obj


def read_bytes(data: bytes) -> EvidencePacket:
    """Parse and fail-closed validate one P0 packet from UTF-8 bytes."""
    try:
        text = data.decode("utf-8")
    except UnicodeDecodeError as exc:
        raise PacketError(SCHEMA_VIOLATION, f"packet is not UTF-8: {exc}") from exc
    try:
        raw = json.loads(text)
    except json.JSONDecodeError as exc:
        raise PacketError(SCHEMA_VIOLATION, f"packet is not valid JSON: {exc}") from exc
    return validate(raw)


def validate(raw: Any) -> EvidencePacket:
    """Validate full frozen shape, internal references, and repair equations."""
    packet = _object(raw, "packet",
                     ("packetVersion", "packetId", "sourceIdentity", "debugIr",
                      "evidenceIndex", "repairGate", "generation"),
                     ("derivedViews", "notes"))
    _expect(packet["packetVersion"] == PACKET_VERSION,
            f"must equal '{PACKET_VERSION}'", "packetVersion")
    packet_id = _nonempty(packet["packetId"], "packetId")
    source_identity = _source_identity(packet["sourceIdentity"])
    debug_ir = _debug_ir(packet["debugIr"])
    evidence_index = _array(packet["evidenceIndex"], "evidenceIndex", _evidence_entry)
    ref_ids = [entry["refId"] for entry in evidence_index]
    _expect(len(ref_ids) == len(set(ref_ids)), "refIds must be unique", "evidenceIndex")
    repair_gate = _repair_gate(packet["repairGate"])
    _generation(packet["generation"])
    if "derivedViews" in packet:
        _array(packet["derivedViews"], "derivedViews", _derived_view)
    if "notes" in packet:
        _array(packet["notes"], "notes", _nonempty)

    result = EvidencePacket(packet, packet["packetVersion"], packet_id, source_identity,
                            debug_ir, evidence_index, repair_gate)
    _validate_reference_closure(result)
    _validate_repair_equations(result)
    return result


def _validate_reference_closure(packet: EvidencePacket) -> None:
    index = packet.index_by_ref_id()
    refs: list[tuple[str, str]] = []

    def collect(values: list[str], field: str) -> None:
        refs.extend((value, field) for value in values)

    ir = packet.debug_ir
    collect(ir["EvidenceRefs"], "debugIr.EvidenceRefs")
    collect(ir["TerminalState"]["evidenceRefs"], "debugIr.TerminalState.evidenceRefs")
    collect(ir["TargetObservation"]["evidenceRefs"], "debugIr.TargetObservation.evidenceRefs")
    collect(ir["TargetOccurrence"]["evidenceRefs"], "debugIr.TargetOccurrence.evidenceRefs")
    for name in ("GoodComparison", "BadComparison", "LastGood", "FirstBad", "Owner", "Confidence"):
        collect(ir[name]["evidenceRefs"], f"debugIr.{name}.evidenceRefs")
    for stage in STAGES:
        for role in ("inputRefs", "decisionRefs", "outputRefs"):
            collect(ir["EvidenceChain"][stage][role], f"debugIr.EvidenceChain.{stage}.{role}")
    for view in packet._raw.get("derivedViews", []):
        collect(view["evidenceRefs"], "derivedViews.evidenceRefs")
    for entry in packet.evidence_index:
        selector_ref = entry["selector"].get("evidenceRef")
        if selector_ref is not None:
            refs.append((selector_ref, f"evidenceIndex[{entry['refId']}].selector.evidenceRef"))
    for ref_id, field in refs:
        _expect(ref_id in index, f"'{ref_id}' is not present in evidenceIndex", field)


def _validate_repair_equations(packet: EvidencePacket) -> None:
    ir = packet.debug_ir
    blockers = frozenset(packet.repair_gate["blockers"])
    expected: set[str] = set()
    if ir["FirstBad"]["status"] != "CONFIRMED":
        expected.add("NO_FDP")
    if ir["Owner"]["status"] != "CONFIRMED":
        expected.add("NO_OWNER")
    if ir["TargetOccurrence"]["status"] == "AMBIGUOUS":
        expected.add("AMBIGUOUS_OCCURRENCE")
    if ir["Disposition"] != "MINIMAL_REPAIR":
        expected.add("DISPOSITION_NOT_MINIMAL_REPAIR")
    if ir["Disposition"] == "INSUFFICIENT_EVIDENCE":
        expected.add("INSUFFICIENT_EVIDENCE")
    if ir["Disposition"] == "ARCHITECTURE_GATE":
        expected.add("ARCHITECTURE_GATE_REQUIRED")
    if ir["Disposition"] == "ENVIRONMENT_GATE":
        expected.add("ENVIRONMENT_GATE_REQUIRED")
    if ir["MissingEvidence"]:
        expected.add("MISSING_REQUIRED_EVIDENCE")
    if any(entry["integrity"] == "IDENTITY_MISMATCH" for entry in packet.evidence_index):
        expected.add("IDENTITY_MISMATCH")
    _expect(expected.issubset(blockers),
            f"missing deterministic blocker(s): {', '.join(sorted(expected - blockers))}",
            "repairGate.blockers")
    if packet.repair_gate["eligible"]:
        _expect(ir["FirstBad"]["status"] == "CONFIRMED", "eligible repair requires confirmed FirstBad", "repairGate")
        _expect(ir["Owner"]["status"] == "CONFIRMED", "eligible repair requires confirmed Owner", "repairGate")
        _expect(ir["Disposition"] == "MINIMAL_REPAIR", "eligible repair requires MINIMAL_REPAIR", "repairGate")
        _expect(not ir["MissingEvidence"], "eligible repair cannot have MissingEvidence", "repairGate")
