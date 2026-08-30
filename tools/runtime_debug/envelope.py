"""Canonical result envelope (P1a machine envelope) — deterministic serialization.

No timestamps, absolute paths, pids, or stack traces are ever emitted.
"""

from __future__ import annotations

import json
from typing import Any

CONTRACT_VERSION = "runtime-debug-cli.p1a"


def source_ref(packet_version: str, packet_id: str, source_identity: dict) -> dict:
    """'source' block: only packet version/id and the packet-stored source identity."""
    return {
        "packetVersion": packet_version,
        "packetId": packet_id,
        "sourceIdentity": source_identity,
    }


def diagnostic(code: str, message: str, evidence_refs: list[str] | None = None) -> dict:
    return {"code": code, "message": message, "evidenceRefs": sorted(evidence_refs or [])}


def build(
    command: str,
    status_vocab: str,
    source: dict,
    result: Any,
    diagnostics: list[dict],
) -> dict:
    """One envelope object; diagnostics are stably sorted."""
    return {
        "contractVersion": CONTRACT_VERSION,
        "command": command,
        "status": status_vocab,
        "source": source,
        "result": result,
        "diagnostics": sorted(diagnostics, key=lambda d: (d["code"], d["message"])),
    }


def render(envelope: dict) -> str:
    """UTF-8, sorted keys, fixed separators, single trailing newline."""
    return json.dumps(
        envelope,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=False,
    ) + "\n"