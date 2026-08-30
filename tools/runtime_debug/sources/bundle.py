"""Read-only, fail-closed Harness capture bundle adapter.

The adapter consumes the actual camelCase JSON emitted by
FileTraceCaptureStore.  It validates identity, publication, record fidelity,
checksum coverage, and streamed artifact bytes before emitting AssetRefs.
"""

from __future__ import annotations

import hashlib
import json
import os
import re
from typing import Any

from ..status import EVIDENCE_UNAVAILABLE, SCHEMA_VIOLATION

PRODUCER = "UniClaw.Runtime.Harness.capture"
MANIFEST = "capture-manifest.json"
RECORDS = "records.json"
CHECKSUMS = "checksums.sha256"
TRACE = "observability-trace.json"
ARTIFACT_DIR = "artifacts"
_HEX64 = re.compile(r"^[a-f0-9]{64}$")

# System.Text.Json serializes these enums as numbers in the Harness wire.  The
# string names remain accepted as an explicit compatibility shape for earlier
# hand-built diagnostic bundles; no other values are accepted.
RECORD_KINDS = {0: "Observation", 1: "ActionDispatch", 2: "ActionResult", 3: "CaptureFault"}
RECORD_KIND_NAMES = frozenset(RECORD_KINDS.values())


class SourceError(Exception):
    """Fail-closed source failure; messages never embed absolute paths."""

    def __init__(self, status: str, message: str):
        super().__init__(message)
        self.status = status
        self.message = message


class CaptureBundle:
    """Read-only view of one fully validated capture bundle."""

    def __init__(self, bundle_dir: str, manifest: dict, records: list[dict],
                 artifacts: list[dict], frame_sequences: dict[str, int],
                 trace: dict | None = None):
        self.bundle_dir = bundle_dir
        self.manifest = manifest
        self.records = records
        self.artifacts = artifacts
        self.frame_sequences = frame_sequences
        # Optional Harness observability trace (TraceRun JSON, camelCase) —
        # the execution-tree data source; None when the bundle has none.
        self.trace = trace

    @property
    def capture_session_id(self) -> str:
        return self.manifest["captureSessionId"]

    @property
    def trace_id(self) -> str | None:
        value = self.manifest.get("traceId")
        return value if isinstance(value, str) else None

    @property
    def scenario_id(self) -> str | None:
        value = self.manifest.get("scenarioId")
        return value if isinstance(value, str) else None

    def asset_refs(self) -> list[dict]:
        return [self._asset_ref(artifact)
                for artifact in sorted(self.artifacts, key=lambda item: item["artifactId"])]

    def _asset_ref(self, artifact: dict) -> dict:
        artifact_id = artifact["artifactId"]
        frame_id = artifact.get("frameId")
        content_type = artifact.get("contentType")
        return {
            "assetId": artifact_id,
            "assetType": content_type if isinstance(content_type, str) and content_type else "capture.artifact",
            "runId": None,
            "timestamp": None,
            "relativeTimestamp": None,
            "observationSeq": self.frame_sequences.get(frame_id) if isinstance(frame_id, str) else None,
            "traceId": self.trace_id,
            "spanId": None,
            "occurrenceId": None,
            "producer": PRODUCER,
            "path": f"{ARTIFACT_DIR}/{artifact_id}.bin",
            "mimeType": content_type if isinstance(content_type, str) else None,
            "sha256": artifact["contentHash"],
            "parentAssetRef": artifact.get("derivedFromArtifactId"),
            "cropBounds": None,
            "annotations": None,
            "metadata": {
                "fileName": artifact.get("fileName"),
                "frameId": frame_id,
                "byteCount": artifact["byteCount"],
            },
        }


def read_bundle(bundle_dir: str) -> CaptureBundle:
    """Fail-closed load of one explicitly named published capture bundle."""
    if not os.path.isdir(bundle_dir):
        raise SourceError(EVIDENCE_UNAVAILABLE, "capture bundle directory not found")
    _expect(not os.path.islink(bundle_dir), "capture bundle directory must not be a symbolic link")

    manifest = _read_required_json(bundle_dir, MANIFEST, "capture manifest")
    _expect(isinstance(manifest, dict), "capture manifest must be an object")
    _expect(manifest.get("schemaVersion") == 1, "capture manifest schemaVersion must equal 1")
    capture_session_id = manifest.get("captureSessionId")
    _expect(isinstance(capture_session_id, str) and _safe_id(capture_session_id),
            "capture manifest captureSessionId must be one safe path segment")
    final_state = manifest.get("finalState")
    _expect(final_state in (3, "Persisted"), "capture manifest finalState must be Persisted")
    for field in ("traceId", "scenarioId"):
        _expect(manifest.get(field) is None or isinstance(manifest.get(field), str),
                f"capture manifest {field} must be a string or null")

    records = _read_required_json(bundle_dir, RECORDS, "capture records")
    _expect(isinstance(records, list), "capture records must be a list")
    manifest_records = manifest.get("records")
    _expect(isinstance(manifest_records, list), "capture manifest records must be a list")
    _expect(manifest_records == records, "capture manifest records and records.json must be identical")
    normalized_records = _validate_records(records)

    artifacts = manifest.get("artifacts")
    _expect(isinstance(artifacts, list), "capture manifest artifacts must be a list")
    validated_artifacts = _validate_artifacts(artifacts)
    _verify_artifact_directory(bundle_dir, validated_artifacts)
    _verify_checksums(bundle_dir, validated_artifacts)

    frame_sequences: dict[str, int] = {}
    for record in normalized_records:
        if record["kind"] == "Observation" and isinstance(record.get("frameId"), str):
            frame_sequences.setdefault(record["frameId"], record["sequenceNumber"])

    return CaptureBundle(bundle_dir, manifest, normalized_records,
                         validated_artifacts, frame_sequences,
                         trace=_load_trace(bundle_dir))


def _load_trace(bundle_dir: str) -> dict | None:
    """Read the optional Harness observability trace (TraceRun JSON, camelCase).
    Malformed trace fails closed; a bundle without a trace returns None."""
    path = os.path.join(bundle_dir, TRACE)
    if not os.path.exists(path):
        return None
    if os.path.islink(path):
        _expect(False, "capture observability trace must not be a symbolic link")
    raw = _read_required_json(bundle_dir, TRACE, "capture observability trace")
    _expect(isinstance(raw, dict), "capture observability trace must be an object")
    _expect(raw.get("schemaVersion") == 1, "capture observability trace schemaVersion must equal 1")
    spans = raw.get("spans")
    _expect(isinstance(spans, list), "capture observability trace spans must be a list")
    span_ids: set[str] = set()
    for index, span in enumerate(spans):
        _expect(isinstance(span, dict), f"capture observability trace span {index} must be an object")
        span_id = span.get("spanId")
        _expect(isinstance(span_id, str) and span_id and span_id not in span_ids,
                f"capture observability trace span {index} spanId must be a unique non-empty string")
        span_ids.add(span_id)
        for field in ("parentSpanId", "name", "layer", "component", "outcome"):
            _expect(span.get(field) is None or isinstance(span.get(field), str),
                    f"capture observability trace span {index} {field} must be a string or null")
        for field in ("startOffsetNs", "durationNs"):
            value = span.get(field)
            _expect(isinstance(value, int) and not isinstance(value, bool) and value >= 0,
                    f"capture observability trace span {index} {field} must be a non-negative integer")
    return raw


def _validate_records(records: list[Any]) -> list[dict]:
    normalized: list[dict] = []
    for index, record in enumerate(records, start=1):
        _expect(isinstance(record, dict), f"capture record {index} must be an object")
        _expect(record.get("order") == index, "capture record order must be contiguous from one")
        kind_value = record.get("kind")
        if isinstance(kind_value, int) and not isinstance(kind_value, bool):
            kind = RECORD_KINDS.get(kind_value)
        elif isinstance(kind_value, str) and kind_value in RECORD_KIND_NAMES:
            kind = kind_value
        else:
            kind = None
        _expect(kind is not None, f"capture record {index} kind is unsupported")
        sequence = record.get("sequenceNumber")
        _expect(isinstance(sequence, int) and not isinstance(sequence, bool) and sequence >= 0,
                f"capture record {index} sequenceNumber must be a non-negative integer")
        frame_id = record.get("frameId")
        _expect(frame_id is None or isinstance(frame_id, str),
                f"capture record {index} frameId must be a string or null")
        copy = dict(record)
        copy["kind"] = kind
        normalized.append(copy)
    return normalized


def _validate_artifacts(artifacts: list[Any]) -> list[dict]:
    validated: list[dict] = []
    ids: set[str] = set()
    for index, artifact in enumerate(artifacts):
        _expect(isinstance(artifact, dict), f"capture artifact {index} must be an object")
        artifact_id = artifact.get("artifactId")
        _expect(isinstance(artifact_id, str) and _safe_id(artifact_id),
                f"capture artifact {index} artifactId must be one safe path segment")
        _expect(artifact_id not in ids, "capture artifact ids must be unique")
        ids.add(artifact_id)
        content_hash = artifact.get("contentHash")
        _expect(isinstance(content_hash, str) and _HEX64.fullmatch(content_hash) is not None,
                f"capture artifact {artifact_id} contentHash must be lowercase sha256")
        byte_count = artifact.get("byteCount")
        _expect(isinstance(byte_count, int) and not isinstance(byte_count, bool) and byte_count >= 0,
                f"capture artifact {artifact_id} byteCount must be non-negative")
        for field in ("frameId", "fileName", "contentType", "derivedFromArtifactId"):
            _expect(artifact.get(field) is None or isinstance(artifact.get(field), str),
                    f"capture artifact {artifact_id} {field} must be a string or null")
        validated.append(artifact)
    for artifact in validated:
        parent = artifact.get("derivedFromArtifactId")
        _expect(parent is None or (_safe_id(parent) and parent != artifact["artifactId"] and parent in ids),
                f"capture artifact {artifact['artifactId']} has an invalid derivedFromArtifactId")
    return validated


def _verify_artifact_directory(bundle_dir: str, artifacts: list[dict]) -> None:
    artifact_dir = os.path.join(bundle_dir, ARTIFACT_DIR)
    if artifacts:
        _expect(os.path.isdir(artifact_dir) and not os.path.islink(artifact_dir),
                "artifact directory is missing or unsafe")
    elif not os.path.exists(artifact_dir):
        return
    else:
        _expect(os.path.isdir(artifact_dir) and not os.path.islink(artifact_dir),
                "artifact directory is unsafe")

    declared_names = {f"{artifact['artifactId']}.bin" for artifact in artifacts}
    actual_names = set()
    for name in os.listdir(artifact_dir):
        path = os.path.join(artifact_dir, name)
        if name.endswith(".bin"):
            actual_names.add(name)
        _expect(not os.path.islink(path), "artifact directory contains a symbolic link")
    _expect(actual_names == declared_names, "artifact directory contents do not match the manifest")

    for artifact in artifacts:
        path = os.path.join(artifact_dir, f"{artifact['artifactId']}.bin")
        _expect(os.path.isfile(path) and not os.path.islink(path),
                f"artifact file is missing or unsafe: {artifact['artifactId']}")
        digest = hashlib.sha256()
        count = 0
        try:
            with open(path, "rb") as handle:
                while True:
                    block = handle.read(1024 * 1024)
                    if not block:
                        break
                    count += len(block)
                    digest.update(block)
        except OSError as exc:
            raise SourceError(SCHEMA_VIOLATION, f"artifact file is unreadable: {artifact['artifactId']}: {exc}") from exc
        _expect(count == artifact["byteCount"], f"artifact byteCount mismatch: {artifact['artifactId']}")
        _expect(digest.hexdigest() == artifact["contentHash"],
                f"artifact contentHash mismatch: {artifact['artifactId']}")


def _verify_checksums(bundle_dir: str, artifacts: list[dict]) -> None:
    path = _required_file(bundle_dir, CHECKSUMS, "capture checksums")
    try:
        with open(path, encoding="utf-8") as handle:
            lines = handle.read().splitlines()
    except (OSError, UnicodeError) as exc:
        raise SourceError(SCHEMA_VIOLATION, f"capture checksums unreadable: {exc}") from exc

    expected = {f"{ARTIFACT_DIR}/{item['artifactId']}.bin": item["contentHash"]
                for item in artifacts}
    seen: set[str] = set()
    for line in lines:
        if not line.strip():
            continue
        parts = line.split("  ", 1)
        _expect(len(parts) == 2, "invalid checksum entry")
        digest, relative = parts
        _expect(relative in expected and relative not in seen,
                "checksum references an undeclared or duplicate artifact")
        _expect(digest == expected[relative], "checksum disagrees with manifest contentHash")
        seen.add(relative)
    _expect(seen == set(expected), "checksum manifest does not cover every artifact")


def _read_required_json(bundle_dir: str, name: str, label: str) -> Any:
    path = _required_file(bundle_dir, name, label)
    try:
        with open(path, "rb") as handle:
            return json.loads(handle.read().decode("utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise SourceError(SCHEMA_VIOLATION, f"{label} is not valid readable UTF-8 JSON: {exc}") from exc


def _required_file(bundle_dir: str, name: str, label: str) -> str:
    path = os.path.join(bundle_dir, name)
    if not os.path.exists(path):
        raise SourceError(EVIDENCE_UNAVAILABLE, f"{label} not found")
    _expect(os.path.isfile(path) and not os.path.islink(path), f"{label} must be a regular file")
    return path


def _safe_id(value: str) -> bool:
    return bool(value and value not in (".", "..") and not value.lower().startswith(".staging-")
                and not os.path.isabs(value) and "/" not in value and "\\" not in value)


def _expect(condition: bool, message: str) -> None:
    if not condition:
        raise SourceError(SCHEMA_VIOLATION, message)
