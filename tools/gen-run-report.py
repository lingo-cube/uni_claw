#!/usr/bin/env python3
"""PNL-006 — full-link single-run test report generator (deterministic correlator).

Read-only projection over one run directory's source-native artifacts
(metadata.json / facts.json / trace.json / settings-trace.json / exec.journal /
consultations.json / environment-preflight.json / failure.json) and produces:

  report.json — uniclaw.workspace.run-report.v1 (canonical machine read model)
  report.md   — human rendering derived ONLY from the same JSON facts

Discipline (PNL-003/004/005 lineage):
- zero new authority: every section declares source/availability; the report
  never becomes a second run truth;
- no fabricated ordering: cross-track interleaving uses each source's native
  ordinal (cycle / journalSeq / captureSequence) and documented derived rules;
  no wall-clock timeline is invented (Kernel trace is deliberately untimed);
- byte-deterministic output: same inputs -> identical bytes (no timestamps,
  no environment-dependent fields); file digests cover raw input bytes;
- degradation over rejection by default (--strict flips it for CI/evidence).

exec.journal format: 4-byte big-endian record length + UTF-8 JSON per record
(K: prepare|submission|receipt|...).

Exit codes: 0 ok · 1 usage/io · 2 strict violation · 3 check mismatch.

Usage:
  python3 tools/gen-run-report.py --run-dir <dir> --out-dir <dir> [--strict]
  python3 tools/gen-run-report.py --run-dir <dir> --check --expect-dir <dir>
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import struct
import sys
from pathlib import Path

SCHEMA_VERSION = "uniclaw.workspace.run-report.v1"
CONTRACT_VERSION = "uniclaw.workspace.contract.v1"
REPORT_KIND = "full-link-single-run"
GENERATOR_NAME = "gen-run-report"
GENERATOR_VERSION = 1

# Artifact name -> file name inside the run directory (fixed contract set).
ARTIFACT_FILES = [
    ("metadata", "metadata.json"),
    ("facts", "facts.json"),
    ("trace", "trace.json"),
    ("settingsTrace", "settings-trace.json"),
    ("journal", "exec.journal"),
    ("consultations", "consultations.json"),
    ("environmentPreflight", "environment-preflight.json"),
    ("failure", "failure.json"),
    ("runtimeRunEvents", "runtime-run-events.json"),
]
STRICT_REQUIRED = ["metadata", "facts", "trace", "journal"]

AV_PRESENT = "present"
AV_PARTIAL = "partial"
AV_NOT_COLLECTED = "not-collected"

STRUCTURAL_OUTCOMES = {0: "Completed", 1: "Faulted", 2: "Cancelled", 3: "Incomplete"}
RECORDER_TERMINALS = {0: "Finalized", 1: "Quarantined", 2: "CaptureFailed"}
TIMESPAN_RE = re.compile(r"^(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})(?:\.(\d+))?$")


# ---------------------------------------------------------------- helpers ----

def fail(message: str, code: int = 1) -> "NoReturn":  # type: ignore[valid-type]
    print(f"ERROR {message}", file=sys.stderr)
    raise SystemExit(code)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 16), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_json(path: Path):
    with path.open(encoding="utf-8") as handle:
        return json.load(handle)


def parse_journal(path: Path):
    """4-byte big-endian length prefix + UTF-8 JSON per record."""
    records = []
    data = path.read_bytes()
    offset = 0
    while offset < len(data):
        if offset + 4 > len(data):
            raise ValueError(f"truncated journal prefix at byte {offset}")
        (length,) = struct.unpack_from(">I", data, offset)
        offset += 4
        if offset + length > len(data):
            raise ValueError(f"truncated journal record at byte {offset}")
        records.append(json.loads(data[offset:offset + length].decode("utf-8")))
        offset += length
    return records


def timespan_ms(value):
    if not isinstance(value, str):
        return None
    match = TIMESPAN_RE.match(value.strip())
    if not match:
        return None
    days, hours, minutes, seconds, fraction = match.groups()
    total = int(days or 0) * 86400 + int(hours) * 3600 + int(minutes) * 60 + int(seconds)
    if fraction:
        total += int(fraction) / (10 ** len(fraction))
    return round(total * 1000, 3)


def field(container, key, default=None):
    if isinstance(container, dict):
        return container.get(key, default)
    return default


def identity(value, availability, value_origin, source=None):
    entry = {
        "value": value if isinstance(value, str) and value else None,
        "availability": availability,
        "valueOrigin": value_origin,
    }
    if source:
        entry["source"] = source
    return entry


def ref_kind(identifier: str) -> str:
    if identifier.startswith("ev-"):
        return "evidence"
    if identifier.startswith("rev-"):
        return "revision"
    if identifier.startswith("receipt-"):
        return "receipt"
    if identifier.startswith("attempt-"):
        return "attempt"
    if identifier.startswith("bind-"):
        return "binding"
    if identifier.startswith("intent-"):
        return "intent"
    if identifier.startswith("capture-"):
        return "capture"
    if identifier.startswith("sp-"):
        return "span"
    if identifier.startswith("trc-"):
        return "trace"
    if identifier.startswith("run-"):
        return "run"
    if identifier.startswith("session-"):
        return "session"
    return "run"


class ReferenceIndex:
    """L4: closure over every identifier the report itself mentions."""

    def __init__(self):
        self._entries: dict[str, dict] = {}

    def add(self, identifier: str, artifact: str, locator: str):
        if not identifier:
            return
        self._entries.setdefault(identifier, {
            "id": identifier,
            "kind": ref_kind(identifier),
            "artifact": artifact,
            "locator": locator,
        })

    def sorted_entries(self):
        return [self._entries[key] for key in sorted(self._entries)]


# ------------------------------------------------------------------ build ----

def build_report(run_dir: Path, run_dir_argument: str | None = None, requirement_text: str | None = None):
    anomalies: list[dict] = []
    refs = ReferenceIndex()

    # ---- input census ------------------------------------------------------
    artifacts = []
    parsed: dict[str, object] = {}
    for name, filename in ARTIFACT_FILES:
        path = run_dir / filename
        entry = {"name": name, "availability": AV_NOT_COLLECTED}
        if path.is_file():
            entry.update({
                "availability": AV_PRESENT,
                "file": filename,
                "bytes": path.stat().st_size,
                "sha256": sha256_file(path),
            })
            try:
                parsed[name] = parse_journal(path) if name == "journal" else load_json(path)
            except Exception as error:  # degraded mode: parse failure is an observation
                entry["availability"] = AV_PARTIAL
                entry["note"] = f"parse failed: {error}"
                anomalies.append({
                    "code": "artifact-parse-failed",
                    "severity": "warning",
                    "detail": f"{filename}: {error}",
                    "refs": [],
                })
        artifacts.append(entry)

    metadata = parsed.get("metadata") if isinstance(parsed.get("metadata"), dict) else {}
    facts = parsed.get("facts") if isinstance(parsed.get("facts"), dict) else {}
    trace = parsed.get("trace") if isinstance(parsed.get("trace"), dict) else {}
    settings_trace = parsed.get("settingsTrace") if isinstance(parsed.get("settingsTrace"), list) else []
    journal = parsed.get("journal") if isinstance(parsed.get("journal"), list) else []
    consultations = parsed.get("consultations") if isinstance(parsed.get("consultations"), list) else None
    preflight = parsed.get("environmentPreflight") if isinstance(parsed.get("environmentPreflight"), dict) else None
    failure = parsed.get("failure") if isinstance(parsed.get("failure"), dict) else None
    runtime_events = parsed.get("runtimeRunEvents") if isinstance(parsed.get("runtimeRunEvents"), list) else None

    # ---- identities --------------------------------------------------------
    run_id_value = field(facts, "runId") or field(trace, "runId")
    run_id_source = "facts.json" if field(facts, "runId") else ("trace.json" if field(trace, "runId") else None)
    trace_id_value = field(trace, "traceId")
    identities = {
        "runId": identity(
            run_id_value,
            AV_PRESENT if run_id_value else AV_NOT_COLLECTED,
            "observed",
            run_id_source,
        ),
        "traceId": identity(trace_id_value, AV_PRESENT if trace_id_value else AV_NOT_COLLECTED, "observed", "trace.json"),
        "productSessionId": identity(
            field(metadata, "productSessionId"),
            AV_PRESENT if field(metadata, "productSessionId") else AV_NOT_COLLECTED,
            "observed", "metadata.json",
        ),
        "dshSessionId": identity(
            field(metadata, "dshSessionId"),
            AV_PRESENT if field(metadata, "dshSessionId") else AV_NOT_COLLECTED,
            "observed", "metadata.json",
        ),
        "runDir": identity(run_dir_argument or str(run_dir), AV_PRESENT, "configured"),
    }
    for key in ("runId", "traceId", "productSessionId", "dshSessionId"):
        if identities[key]["value"]:
            refs.add(identities[key]["value"], "report envelope", f"identities.{key}")

    # ---- kernel trace census ------------------------------------------------
    spans = trace.get("spans") if isinstance(trace.get("spans"), list) else []
    admits = [s for s in spans if s.get("spanDefinitionId") == "evidence.admit"]
    reconciles = [s for s in spans if s.get("spanDefinitionId") == "world.reconcile"]
    checkpoints = [s for s in spans if s.get("spanDefinitionId") in ("world.derive-slice", "world.resolve-current")]

    def revision_of(span):
        for event in span.get("events") or []:
            for reference in event.get("references") or []:
                if str(reference.get("value", "")).startswith("rev-"):
                    return reference["value"]
        return None

    revision_numbers = []
    for span in reconciles:
        revision = revision_of(span)
        if revision:
            revision_numbers.append(int(revision.split("-", 1)[1]))
    revision_numbers.sort()
    first_revision = f"rev-{revision_numbers[0]}" if revision_numbers else None
    last_revision = f"rev-{revision_numbers[-1]}" if revision_numbers else None

    recorder_terminal = trace.get("recorderTerminal")
    integrity_sha = field(trace, "integritySha256")
    if trace:
        if recorder_terminal != 0:
            anomalies.append({
                "code": "trace-recorder-not-finalized",
                "severity": "warning",
                "detail": f"recorderTerminal={RECORDER_TERMINALS.get(recorder_terminal, recorder_terminal)}",
                "refs": [trace_id_value] if trace_id_value else [],
            })
        if not (isinstance(integrity_sha, str) and len(integrity_sha) == 64):
            anomalies.append({
                "code": "trace-integrity-missing",
                "severity": "warning",
                "detail": "trace.json integritySha256 absent or malformed",
                "refs": [],
            })
    faulted = [s for s in spans if s.get("structuralOutcome") not in (0, None)]
    if faulted:
        anomalies.append({
            "code": "trace-spans-not-completed",
            "severity": "warning",
            "detail": f"{len(faulted)} span(s) with structuralOutcome != Completed",
            "refs": [s.get("spanId") for s in faulted[:10]],
        })

    # ---- effects (journal) ---------------------------------------------------
    attempts: dict[str, dict] = {}
    for record in journal:
        attempt_id = record.get("AttemptId")
        if not attempt_id:
            continue
        slot = attempts.setdefault(attempt_id, {"attemptId": attempt_id, "stages": {}})
        kind = record.get("K")
        if kind:
            slot["stages"][kind] = record
    attempt_list = [attempts[key] for key in sorted(attempts)]

    for slot in attempt_list:
        prepare = slot["stages"].get("prepare") or {}
        receipt = slot["stages"].get("receipt") or {}
        slot.update({
            "effectClass": prepare.get("EffectClass"),
            "targetSubject": prepare.get("TargetSubject"),
            "intentId": prepare.get("IntentId"),
            "bindingId": prepare.get("BindingId"),
            "effectRef": prepare.get("EffectRef"),
            "executorId": prepare.get("ExecutorId"),
            "admissionNote": prepare.get("AdmissionNote"),
            "revisionNumber": prepare.get("RevisionNumber"),
            "receiptOutcome": receipt.get("Outcome"),
        })

    bad_receipts = [s.get("receiptOutcome") for s in attempt_list if s.get("receiptOutcome") != "DeliveryCompleted"]
    if bad_receipts:
        anomalies.append({
            "code": "effect-receipt-not-delivered",
            "severity": "warning",
            "detail": f"{len(bad_receipts)} attempt(s) without DeliveryCompleted receipt",
            "refs": [s["attemptId"] for s in attempt_list if s.get("receiptOutcome") != "DeliveryCompleted"],
        })
    if journal and not attempts:
        anomalies.append({
            "code": "journal-attempts-unrecoverable",
            "severity": "warning",
            "detail": "journal records present but no AttemptId grouping possible",
            "refs": [],
        })

    # ---- terminal facts -------------------------------------------------------
    status = field(facts, "status") or field(metadata, "status")
    outcome = field(facts, "outcome") or field(metadata, "outcome")
    terminal = field(facts, "terminal")
    if facts and terminal is False:
        anomalies.append({
            "code": "run-not-terminal",
            "severity": "warning",
            "detail": f"facts.terminal=false (status={status})",
            "refs": [run_id_value] if run_id_value else [],
        })
    if failure:
        anomalies.append({
            "code": "failure-artifact-present",
            "severity": "failure",
            "detail": f"failure.json present: {field(failure, 'message') or field(failure, 'reason') or 'see artifact'}",
            "refs": [],
        })

    # ---- coverage -------------------------------------------------------------
    host_available = bool(facts or metadata)
    agent_count = field(metadata, "consultations")
    coverage_sections = [
        {"section": "host",
         "availability": AV_PRESENT if runtime_events is not None else (AV_PARTIAL if host_available else AV_NOT_COLLECTED),
         "reason": (None if runtime_events is not None else
                    ("Runtime Host lifecycle 事件（.runtime-runs）不在输入内；仅由 run 目录产物重建"
                     if host_available else "run 目录内无 metadata/facts 可用")),
         },
        {"section": "agent",
         "availability": AV_PRESENT if consultations is not None else (AV_PARTIAL if agent_count is not None else AV_NOT_COLLECTED),
         "reason": None if consultations is not None else ("consultations.json 缺失；仅有 metadata.consultations 计数" if agent_count is not None else "无任何 UniAgent 侧观测"),
         },
        {"section": "capability", "availability": AV_PRESENT if settings_trace else AV_PARTIAL, "reason": None if settings_trace else "settings-trace.json 缺失；capability 侧只剩 journal 中的 executor 标识"},
        {"section": "worldModel", "availability": AV_PRESENT if reconciles else AV_NOT_COLLECTED, "reason": None if reconciles else "trace.json 无 world.reconcile span"},
        {"section": "evidence", "availability": AV_PRESENT if admits else AV_NOT_COLLECTED, "reason": None if admits else "trace.json 无 evidence.admit span"},
        {"section": "operations", "availability": AV_PRESENT if attempt_list else (AV_PARTIAL if journal else AV_NOT_COLLECTED), "reason": None if attempt_list else ("exec.journal 存在但无法还原 attempt" if journal else "exec.journal 缺失")},
        {"section": "timeline", "availability": AV_PRESENT if (settings_trace or attempt_list or checkpoints) else AV_PARTIAL, "reason": None if (settings_trace or attempt_list or checkpoints) else "无任何可排序 anchor"},
    ]
    coverage_sections = [{k: v for k, v in section.items() if v is not None} for section in coverage_sections]

    # ---- L2 timeline ------------------------------------------------------------
    # Merge rule (documented, no fabricated clocks):
    #   1. External observation cycles first (native cycle order);
    #   2. kernel checkpoints placed by revision: before the first attempt whose
    #      RevisionNumber >= checkpoint revision (journal prepare carries the
    #      revision the effect was bound to), else before terminal;
    #   3. k-th agent consultation precedes k-th attempt (consult -> decide -> act);
    #   4. post-action observation cycle k follows attempt k (context=PostActionEffectFlow);
    #   5. terminal last.
    timeline = []

    def push(track, kind, basis, **details):
        timeline.append({
            "ordinal": len(timeline) + 1,
            "track": track,
            "kind": kind,
            "orderingBasis": basis,
            **details,
        })

    external_cycles = [c for c in settings_trace if c.get("context") == "External"]
    post_cycles = [c for c in settings_trace if c.get("context") != "External"]

    def push_cycle(cycle):
        push("observation", "observation-cycle", "cycle",
             cycle=cycle.get("cycle"),
             context=cycle.get("context"),
             captureId=cycle.get("captureId"),
             observationCycleId=cycle.get("observationCycleId"),
             screenIdentity=cycle.get("screenIdentity"),
             proposalCount=cycle.get("proposalCount"),
             fastLatencyMs=timespan_ms(cycle.get("fastLatency")),
             hierarchyLatencyMs=timespan_ms(cycle.get("hierarchyLatency")),
             popup=cycle.get("popup"),
             viewportDigest=cycle.get("viewportDigest"),
             refs=[cycle.get("captureId")] if cycle.get("captureId") else [])
        refs.add(cycle.get("captureId"), "settings-trace.json", f"cycle {cycle.get('cycle')}")

    for cycle in external_cycles:
        push_cycle(cycle)

    consultation_records = consultations or []
    checkpoint_queue = []
    for span in checkpoints:
        prior_revision = 0
        for reconcile in reconciles:
            if reconcile.get("captureSequence", 0) < span.get("captureSequence", 0):
                revision = revision_of(reconcile)
                if revision:
                    prior_revision = max(prior_revision, int(revision.split("-", 1)[1]))
            else:
                break
        checkpoint_queue.append({"span": span, "revision": prior_revision})
    checkpoint_queue.sort(key=lambda item: item["span"].get("captureSequence", 0))

    def flush_checkpoints(before_revision):
        while checkpoint_queue and (before_revision is None or checkpoint_queue[0]["revision"] <= before_revision):
            item = checkpoint_queue.pop(0)
            span = item["span"]
            push("kernel", span.get("spanDefinitionId"), "captureSequence",
                 spanId=span.get("spanId"),
                 revisionNumber=item["revision"] or None,
                 refs=[span.get("spanId")] if span.get("spanId") else [])
            refs.add(span.get("spanId"), "trace.json", f"spans / {span.get('spanId')}")

    for index, slot in enumerate(attempt_list):
        flush_checkpoints(slot.get("revisionNumber") or 0)
        if index < len(consultation_records):
            record = consultation_records[index]
            push("agent", "agent-consultation", "derived",
                 consultationIndex=index + 1,
                 durationMs=record.get("durationMs"),
                 refs=[])
        push("effect", "effect-attempt", "journalSeq",
             attemptId=slot["attemptId"],
             effectClass=slot.get("effectClass"),
             targetSubject=slot.get("targetSubject"),
             intentId=slot.get("intentId"),
             executorId=slot.get("executorId"),
             admissionNote=slot.get("admissionNote"),
             revisionNumber=slot.get("revisionNumber"),
             receipt=slot.get("receiptOutcome"),
             refs=[slot["attemptId"], slot.get("bindingId")])
        refs.add(slot["attemptId"], "exec.journal", f"attempt {slot['attemptId']}")
        refs.add(slot.get("bindingId"), "exec.journal", f"attempt {slot['attemptId']} prepare.BindingId")
        refs.add(slot.get("effectRef"), "exec.journal", f"attempt {slot['attemptId']} prepare.EffectRef")
        refs.add(slot.get("intentId"), "exec.journal", f"attempt {slot['attemptId']} prepare.IntentId")
        if index < len(post_cycles):
            push_cycle(post_cycles[index])

    flush_checkpoints(None)
    if status or outcome is not None:
        push("terminal", "run-terminal", "terminal",
             status=status,
             outcome=outcome,
             reason=field(facts, "reason"),
             refs=[run_id_value] if run_id_value else [])

    # ---- L3 layers ----------------------------------------------------------------
    host_layer = {
        "availability": AV_PRESENT if runtime_events is not None else (AV_PARTIAL if host_available else AV_NOT_COLLECTED),
        "reason": None if (runtime_events is not None or host_available) else "无 metadata/facts",
        "artifactsWritten": [entry["name"] for entry in artifacts if entry["availability"] != AV_NOT_COLLECTED],
        "lifecycle": [
            {
                "sequence": event.get("sequence"),
                "eventId": event.get("eventId"),
                "eventType": event.get("eventType"),
                "source": event.get("source"),
                "authority": event.get("authority"),
                "summary": event.get("summary"),
                "occurredAt": event.get("occurredAt"),
            }
            for event in (runtime_events or [])
        ] if runtime_events is not None else None,
        "preflight": preflight,
    }
    if host_layer["reason"] is None:
        del host_layer["reason"]
    if host_layer["lifecycle"] is None:
        del host_layer["lifecycle"]
    if host_layer["preflight"] is None:
        host_layer["preflight"] = {"availability": AV_NOT_COLLECTED}

    agent_layer = {
        "availability": AV_PRESENT if consultation_records else (AV_PARTIAL if agent_count is not None else AV_NOT_COLLECTED),
        "consultationCount": len(consultation_records) if consultation_records else agent_count,
        "model": field(metadata, "productModel"),
        "modelSource": "metadata.json" if field(metadata, "productModel") else None,
        "consultations": consultation_records if consultation_records else None,
    }
    agent_layer = {k: v for k, v in agent_layer.items() if v is not None}

    executors = sorted({slot.get("executorId") for slot in attempt_list if slot.get("executorId")})
    capability_layer = {
        "availability": AV_PRESENT if settings_trace else AV_PARTIAL,
        "perceptionCycles": settings_trace,
        "executors": executors,
        "model": field(metadata, "productModel"),
    }

    world_layer = {
        "availability": AV_PRESENT if reconciles else AV_NOT_COLLECTED,
        "revisionCount": len(revision_numbers),
        "firstRevision": first_revision,
        "lastRevision": last_revision,
        "checkpoints": [
            {"spanId": span.get("spanId"), "spanDefinitionId": span.get("spanDefinitionId"),
             "captureSequence": span.get("captureSequence")}
            for span in checkpoints
        ],
    }
    refs.add(first_revision, "trace.json", "first world.reconcile event reference")
    refs.add(last_revision, "trace.json", "last world.reconcile event reference")

    evidence_layer = {
        "availability": AV_PRESENT if admits else AV_NOT_COLLECTED,
        "admissionCount": len(admits),
        "recorderTerminal": recorder_terminal,
        "recorderTerminalLabel": RECORDER_TERMINALS.get(recorder_terminal) if recorder_terminal is not None else None,
        "integritySha256": integrity_sha,
        "integrityDeclared": bool(isinstance(integrity_sha, str) and len(integrity_sha) == 64),
    }
    evidence_layer = {k: v for k, v in evidence_layer.items() if v is not None}

    completed_steps = field(facts, "completedSteps") or []
    operations_layer = {
        "availability": AV_PRESENT if attempt_list else (AV_PARTIAL if journal else AV_NOT_COLLECTED),
        "attempts": attempt_list,
        "completedSteps": completed_steps,
        "receipts": field(facts, "receipts") or field(metadata, "receipts") or [],
    }
    for step in completed_steps:
        refs.add(step.get("receipt"), "facts.json", "completedSteps[].receipt")

    layers = {
        "host": host_layer,
        "agent": agent_layer,
        "capability": capability_layer,
        "worldModel": world_layer,
        "evidence": evidence_layer,
        "operations": operations_layer,
    }

    # ---- L1 summary (census) + achievement (verdict view) ----------------------
    anchors = field(facts, "completionAnchors")
    if isinstance(anchors, list):
        anchors_total = len(anchors)
        anchors_verified = sum(1 for a in anchors if isinstance(a, dict) and a.get("verified") is True)
        anchors_availability = AV_PRESENT
    else:
        anchors_total = anchors_verified = 0
        anchors_availability = AV_NOT_COLLECTED

    # 需求解析链（PNL-011）：metadata.requirement（finalize 投影）→
    # --requirement（操作者显式）→ 未采集。
    metadata_requirement = field(metadata, "requirement")
    if metadata_requirement:
        requirement = {
            "text": metadata_requirement,
            "availability": AV_PRESENT,
            "valueOrigin": "observed",
            "source": "metadata.json",
        }
    elif requirement_text:
        requirement = {
            "text": requirement_text,
            "availability": AV_PRESENT,
            "valueOrigin": "configured",
            "source": "operator --requirement",
        }
    else:
        requirement = {
            "text": None,
            "availability": AV_NOT_COLLECTED,
            "valueOrigin": "observed",
        }

    receipts = field(facts, "receipts") or field(metadata, "receipts") or []
    achievement = {
        "availability": AV_PRESENT if (status is not None or outcome is not None or facts) else AV_NOT_COLLECTED,
        "status": status,
        "outcome": outcome,
        "terminal": terminal,
        "deliveredEffects": field(facts, "delivered", field(metadata, "deliveredEffects")),
        "completedSteps": len(completed_steps),
        "anchorsAvailability": anchors_availability,
        "anchorsTotal": anchors_total,
        "anchorsVerified": anchors_verified,
        "receipts": receipts,
    }
    achievement = {key: value for key, value in achievement.items() if value is not None}

    summary = {
        "title": field(metadata, "productSessionTitle"),
        "requirement": requirement,
        "environment": {
            "device": field(metadata, "device"),
            "androidApi": field(metadata, "androidApi"),
            "wmSize": field(metadata, "wmSize"),
            "real": field(metadata, "real"),
            "taskSet": field(metadata, "taskSet"),
            "workspace": field(metadata, "workspace"),
            "dshEndpoint": field(metadata, "dshEndpoint"),
        },
        "model": field(metadata, "productModel"),
        "counts": {
            "consultations": (len(consultation_records) if consultation_records else agent_count) or 0,
            "deliveredEffects": field(facts, "delivered", field(metadata, "deliveredEffects")),
            "completedSteps": len(completed_steps),
            "observationCycles": len(settings_trace),
            "evidenceAdmissions": len(admits),
            "worldRevisions": len(revision_numbers),
            "journalRecords": len(journal),
        },
        "achievement": achievement,
    }

    report = {
        "schemaVersion": SCHEMA_VERSION,
        "contractVersion": CONTRACT_VERSION,
        "reportKind": REPORT_KIND,
        "generator": {"name": GENERATOR_NAME, "version": GENERATOR_VERSION},
        "identities": identities,
        "input": {"artifacts": artifacts},
        "coverage": {"sections": coverage_sections},
        "anomalies": anomalies,
        "summary": summary,
        "timeline": timeline,
        "layers": layers,
        "references": refs.sorted_entries(),
    }
    return report, anomalies


# ------------------------------------------------------------------ render ----

AV_LABELS = {
    "present": "完整", "partial": "降级", "not-collected": "未采集",
    "not-applicable": "不适用", "unavailable": "不可用",
    "not-associated": "未关联", "permission-denied": "无权限",
}


def md_escape(value):
    if value is None:
        return "—"
    text = str(value)
    return text.replace("|", "\\|").replace("\n", " ")


def fmt(value):
    if value is None:
        return "—"
    if isinstance(value, bool):
        return "是" if value else "否"
    if isinstance(value, float):
        return f"{value:g}"
    return md_escape(value)


def render_markdown(report: dict) -> str:
    identities = report["identities"]
    summary = report["summary"]
    achievement = summary.get("achievement") or {}
    environment = summary.get("environment") or {}
    counts = summary.get("counts") or {}
    layers = report["layers"]
    coverage = {section["section"]: section for section in report["coverage"]["sections"]}
    lines: list[str] = []
    add = lines.append

    # 异常或未正常完成 → 细节区默认展开（错误的东西才展示更细的细节）
    problematic = bool(report["anomalies"]) or achievement.get("status") not in ("Completed", "completed", None)
    details_open = " open" if problematic else ""

    def av_label(section_name, fallback_layer_availability=None):
        entry = coverage.get(section_name)
        availability = entry["availability"] if entry else fallback_layer_availability
        return AV_LABELS.get(availability, availability or "—")

    # ---- 漏斗顶部：① 需求 → ② 效果达成 → ③ 组件概览 ----
    add(f"# 全链路测试报告 · {fmt(summary.get('title') or identities['runDir']['value'])}")
    add("")
    add("| 身份 | 值 | 身份 | 值 |")
    add("|---|---|---|---|")
    add(f"| RunId | `{fmt(identities['runId']['value'])}` | 设备 | {fmt(environment.get('device'))}"
        f" (API {fmt(environment.get('androidApi'))}, {fmt(environment.get('wmSize'))}) |")
    add(f"| ProductSession | `{fmt(identities['productSessionId']['value'])}` | 模型 | {fmt(summary.get('model'))} |")
    add(f"| DSH Session | `{fmt(identities['dshSessionId']['value'])}` | 测试集 | {fmt(environment.get('taskSet'))} |")
    add(f"| TraceId | `{fmt(identities['traceId']['value'])}` | 输入目录 | `{md_escape(identities['runDir']['value'])}` |")
    add("")

    add("## ① 需求")
    add("")
    requirement = summary.get("requirement") or {}
    if requirement.get("availability") == "present":
        add(f"> {requirement.get('text')}")
        add("")
        add(f"（需求来源：{fmt(requirement.get('source'))}）")
    else:
        add("> 〔run 产物未携带任务需求原文，此处以 session 标题代替；需求落盘属 P2 产品侧改动〕")
        add("")
        add(f"> {fmt(summary.get('title'))}")
    add("")

    add("## ② 效果达成")
    add("")
    add("| 判定项 | 值 |")
    add("|---|---|")
    add(f"| 执行终态 | {fmt(achievement.get('status'))} · {fmt(achievement.get('outcome'))} · terminal={fmt(achievement.get('terminal'))} |")
    add(f"| 效果投递 / 完成步骤 | {fmt(achievement.get('deliveredEffects'))} / {fmt(achievement.get('completedSteps'))} |")
    if achievement.get("anchorsAvailability") == "present":
        add(f"| 验证锚点 | {fmt(achievement.get('anchorsVerified'))}/{fmt(achievement.get('anchorsTotal'))} verified |")
    else:
        add("| 验证锚点 | 未采集（facts.completionAnchors 为空） |")
    receipts = achievement.get("receipts") or []
    add(f"| Receipts | {' '.join(f'`{r}`' for r in receipts) or '—'} |")
    add("")

    add("## ③ 各组件做到什么程度")
    add("")
    add("| 组件 | 关键量化 | 观测状态 |")
    add("|---|---|---|")
    host = layers["host"]
    add(f"| Runtime Host | 终态 {fmt(achievement.get('status'))} · 落盘产物 {len(host.get('artifactsWritten', []))} 件 | {av_label('host')} |")
    agent = layers["agent"]
    add(f"| UniAgent | 咨询 {fmt(agent.get('consultationCount'))} · 模型 {fmt(agent.get('model'))} | {av_label('agent')} |")
    capability = layers["capability"]
    add(f"| Capability | 观察 cycle {fmt(counts.get('observationCycles'))} · "
        f"{'/'.join(capability.get('executors', [])) or '—'} | {av_label('capability')} |")
    world = layers["worldModel"]
    add(f"| WorldModel | revision {fmt(world.get('revisionCount'))}（{fmt(world.get('firstRevision'))}→{fmt(world.get('lastRevision'))}）· "
        f"控制读取点 {len(world.get('checkpoints', []))} | {av_label('worldModel')} |")
    evidence = layers["evidence"]
    add(f"| Evidence | admission {fmt(evidence.get('admissionCount'))} · {fmt(evidence.get('recorderTerminalLabel'))} · "
        f"integrity {'已声明' if evidence.get('integrityDeclared') else '未声明'} | {av_label('evidence')} |")
    operations = layers["operations"]
    add(f"| 执行操作 | attempt {len(operations.get('attempts', []))} · receipts "
        f"{'/'.join(operations.get('receipts', [])) or '—'} | {av_label('operations')} |")
    add("")

    add("## 异常观察")
    add("")
    if report["anomalies"]:
        add("| 级别 | 代码 | 详情 | 引用 |")
        add("|---|---|---|---|")
        for anomaly in report["anomalies"]:
            add(f"| {anomaly['severity']} | `{anomaly['code']}` | {fmt(anomaly['detail'])} | {' '.join(f'`{r}`' for r in anomaly['refs'])} |")
    else:
        add("无。")
    add("")

    # ---- 细节区：默认折叠，按需展开 ----
    add("---")
    add("")
    add(f"# 细节区（默认折叠{ '；本 run 有异常/未完成，已自动展开' if problematic else '；异常或需核查时展开'}）")
    add("")
    add(f"<details{details_open}>")
    add("<summary><b>L2 · 全链路时间线</b></summary>")
    add("")
    add("")
    add("跨轨排序说明：各事件只按其来源原生序（cycle / journalSeq / captureSequence）排列；"
        "咨询→效果、效果→PostAction 观察的先后为 documented derived 规则，非时间戳。")
    add("")
    add("| # | 轨道 | 事件 | 关键细节 | 引用 |")
    add("|---|---|---|---|---|")
    for event in report["timeline"]:
        details = []
        for key in ("context", "screenIdentity", "proposalCount", "fastLatencyMs", "effectClass",
                    "targetSubject", "executorId", "revisionNumber", "receipt", "status", "outcome",
                    "spanDefinitionId", "reason", "admissionNote"):
            if key in event and event[key] is not None:
                details.append(f"{key}={fmt(event[key])}")
        add(f"| {event['ordinal']} | {event['track']} | {event['kind']} | {'; '.join(details) or '—'} | "
            f"{' '.join(f'`{r}`' for r in event.get('refs', []) if r) or '—'} |")
    add("")

    add("</details>")
    add("")
    add(f"<details{details_open}>")
    add("<summary><b>L3 · 分层明细</b>（含覆盖度说明）</summary>")
    add("")
    add("### 覆盖度")
    add("")
    add("| 分区 | 状态 | 说明 |")
    add("|---|---|---|")
    for section in report["coverage"]["sections"]:
        add(f"| {section['section']} | {AV_LABELS.get(section['availability'], section['availability'])} | {fmt(section.get('reason'))} |")
    add("")
    layers = report["layers"]

    add("### Runtime Host")
    host = layers["host"]
    add("")
    add(f"- availability: {AV_LABELS.get(host['availability'], host['availability'])}")
    add(f"- 落盘产物: {', '.join(host.get('artifactsWritten', [])) or '—'}")
    lifecycle = host.get("lifecycle") or []
    if lifecycle:
        add("")
        add("| seq | 事件 | source | authority | 摘要 |")
        add("|---|---|---|---|---|")
        for event in lifecycle:
            add(f"| {fmt(event.get('sequence'))} | `{fmt(event.get('eventType'))}` | {fmt(event.get('source'))} | "
                f"{fmt(event.get('authority'))} | {fmt(event.get('summary'))} |")
    preflight = host.get("preflight") or {}
    if preflight.get("availability") != "not-collected":
        add(f"- environment-preflight: `{json.dumps(preflight, ensure_ascii=False)[:400]}`")
    add("")

    add("### UniAgent")
    agent = layers["agent"]
    add("")
    add(f"- availability: {AV_LABELS.get(agent['availability'], agent['availability'])}")
    add(f"- consultation 数: {fmt(agent.get('consultationCount'))}")
    add(f"- 模型: {fmt(agent.get('model'))}（来源 {fmt(agent.get('modelSource'))}）")
    if agent.get("consultations"):
        add("")
        add("| # | durationMs | decision | guarded |")
        add("|---|---|---|---|")
        for index, record in enumerate(agent["consultations"], 1):
            decision = json.dumps(record.get("decision"), ensure_ascii=False)[:120] if record.get("decision") is not None else "—"
            guarded = json.dumps(record.get("guardedDecision"), ensure_ascii=False)[:120] if record.get("guardedDecision") is not None else "—"
            add(f"| {index} | {fmt(record.get('durationMs'))} | `{md_escape(decision)}` | `{md_escape(guarded)}` |")
    add("")

    add("### Capability（感知 / 执行 / 模型）")
    capability = layers["capability"]
    add("")
    add(f"- availability: {AV_LABELS.get(capability['availability'], capability['availability'])}")
    add(f"- executors: {' '.join(f'`{e}`' for e in capability.get('executors', [])) or '—'}")
    cycles = capability.get("perceptionCycles") or []
    if cycles:
        add("")
        add("| cycle | context | proposals | fast(ms) | hierarchy(ms) | screenIdentity | popup |")
        add("|---|---|---|---|---|---|---|")
        for cycle in cycles:
            add(f"| {fmt(cycle.get('cycle'))} | {fmt(cycle.get('context'))} | {fmt(cycle.get('proposalCount'))} | "
                f"{fmt(timespan_ms(cycle.get('fastLatency')))} | {fmt(timespan_ms(cycle.get('hierarchyLatency')))} | "
                f"`{fmt(cycle.get('screenIdentity'))}` | {fmt(cycle.get('popup'))} |")
    add("")

    add("### WorldModel")
    world = layers["worldModel"]
    add("")
    add(f"- availability: {AV_LABELS.get(world['availability'], world['availability'])}")
    add(f"- revision 数: {fmt(world.get('revisionCount'))}（{fmt(world.get('firstRevision'))} → {fmt(world.get('lastRevision'))}）")
    checkpoints = world.get("checkpoints") or []
    if checkpoints:
        add(f"- 控制读取点: {len(checkpoints)} 个（derive-slice / resolve-current，见时间线 kernel 轨道）")
    add("")

    add("### Evidence")
    evidence = layers["evidence"]
    add("")
    add(f"- availability: {AV_LABELS.get(evidence['availability'], evidence['availability'])}")
    add(f"- admission 数: {fmt(evidence.get('admissionCount'))}")
    add(f"- recorderTerminal: {fmt(evidence.get('recorderTerminalLabel'))}")
    add(f"- integrity: {'已声明（64 hex）' if evidence.get('integrityDeclared') else '未声明'}")
    add("")

    add("### 执行操作")
    operations = layers["operations"]
    add("")
    add(f"- availability: {AV_LABELS.get(operations['availability'], operations['availability'])}")
    for slot in operations.get("attempts", []):
        add("")
        add(f"#### {slot['attemptId']}")
        add("")
        add("| 项 | 值 |")
        add("|---|---|")
        add(f"| effectClass / target | {fmt(slot.get('effectClass'))} / `{fmt(slot.get('targetSubject'))}` |")
        add(f"| intent / binding | `{fmt(slot.get('intentId'))}` / `{fmt(slot.get('bindingId'))}` |")
        add(f"| executor | {fmt(slot.get('executorId'))} |")
        add(f"| 绑定时 revision | {fmt(slot.get('revisionNumber'))} |")
        add(f"| admission | `{fmt(slot.get('admissionNote'))}` |")
        add(f"| receipt | {fmt(slot.get('receiptOutcome'))} |")
    steps = operations.get("completedSteps") or []
    if steps:
        add("")
        add("completedSteps: " + "; ".join(
            f"decision={s.get('decision')} step={s.get('step')} receipt=`{s.get('receipt')}`" for s in steps))
    add("")

    add("</details>")
    add("")
    add("<details>")
    add("<summary><b>L4 · 引用索引</b></summary>")
    add("")
    add("")
    add("| id | kind | artifact | locator |")
    add("|---|---|---|---|")
    for entry in report["references"]:
        add(f"| `{entry['id']}` | {entry['kind']} | {entry['artifact']} | {md_escape(entry['locator'])} |")
    add("")
    add("</details>")
    add("")

    add("---")
    add("")
    add(f"生成器：`{GENERATOR_NAME}` v{GENERATOR_VERSION} · {SCHEMA_VERSION} · "
        "输入 artifact 的 SHA256 清单见 report.json `input.artifacts`。")
    add("")
    return "\n".join(lines)


# -------------------------------------------------------------------- main ----

def serialize_json(report: dict) -> bytes:
    return (json.dumps(report, ensure_ascii=False, indent=1) + "\n").encode("utf-8")


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="PNL-006 full-link single-run report generator")
    parser.add_argument("--run-dir", required=True, type=Path, help="run artifact directory")
    parser.add_argument("--out-dir", type=Path, help="output directory (default: --run-dir/report)")
    parser.add_argument("--strict", action="store_true",
                        help="exit 2 when metadata/facts/trace/journal missing or runId unresolvable")
    parser.add_argument("--check", action="store_true", help="byte-compare against expect-dir instead of writing")
    parser.add_argument("--expect-dir", type=Path, help="golden directory for --check")
    parser.add_argument("--requirement", help="task requirement text (run artifacts do not carry it yet; P2 will embed it)")
    args = parser.parse_args(argv)

    run_dir = args.run_dir.resolve()
    if not run_dir.is_dir():
        fail(f"--run-dir is not a directory: {run_dir}")
    if args.check and not args.expect_dir:
        fail("--check requires --expect-dir")

    # Byte-determinism: the report records the operator-supplied path verbatim
    # (never the resolved absolute path, which varies per checkout/machine).
    report, anomalies = build_report(run_dir, run_dir_argument=str(args.run_dir),
                                     requirement_text=args.requirement)

    if args.strict:
        violations = []
        present = {entry["name"]: entry["availability"] for entry in report["input"]["artifacts"]}
        for name in STRICT_REQUIRED:
            if present.get(name) != "present":
                violations.append(f"artifact {name}: {present.get(name)}")
        if report["identities"]["runId"]["availability"] != "present":
            violations.append("runId unresolvable")
        if violations:
            for violation in violations:
                print(f"STRICT-VIOLATION {violation}", file=sys.stderr)
            return 2

    report_bytes = serialize_json(report)
    markdown = render_markdown(report)
    md_bytes = markdown.encode("utf-8")

    if args.check:
        mismatches = []
        for filename, actual in (("report.json", report_bytes), ("report.md", md_bytes)):
            expected_path = args.expect_dir / filename
            if not expected_path.is_file():
                mismatches.append(f"{filename}: missing golden {expected_path}")
                continue
            expected = expected_path.read_bytes()
            if expected != actual:
                mismatches.append(f"{filename}: differs from golden ({len(actual)} vs {len(expected)} bytes)")
        if mismatches:
            for mismatch in mismatches:
                print(f"CHECK-FAIL {mismatch}", file=sys.stderr)
            return 3
        print(f"CHECK-PASS runDir={run_dir} expectDir={args.expect_dir.resolve()}")
        return 0

    out_dir = (args.out_dir or (run_dir / "report")).resolve()
    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / "report.json").write_bytes(report_bytes)
    (out_dir / "report.md").write_bytes(md_bytes)
    print(f"WROTE {out_dir / 'report.json'} ({len(report_bytes)} bytes)")
    print(f"WROTE {out_dir / 'report.md'} ({len(md_bytes)} bytes)")
    anomaly_count = len(anomalies)
    print(f"anomalies={anomaly_count} timelineEvents={len(report['timeline'])} references={len(report['references'])}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
