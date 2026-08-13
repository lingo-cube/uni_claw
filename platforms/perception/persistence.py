"""Small write-once persistence primitive for semantic-history records.

This module deliberately owns no artifact taxonomy or lifecycle.  Callers
provide the canonical semantic path and JSON-compatible payload; this helper
only makes replacement impossible while preserving byte-identical retries.
"""
from __future__ import annotations

import json
import os
import tempfile
from pathlib import Path
from typing import Any


class WriteOnceIntegrityError(RuntimeError):
    """A canonical history path already contains different bytes."""


def canonical_json_bytes(payload: Any) -> bytes:
    """Return deterministic UTF-8 JSON bytes for a semantic-history record."""
    return json.dumps(
        payload, ensure_ascii=False, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")


def write_once_json(path: str | Path, payload: Any) -> Path:
    """Persist *payload* without ever replacing an existing canonical record.

    The completed temporary file is linked into its final name atomically.  A
    concurrent or prior writer wins only when it wrote the exact same
    canonical bytes; otherwise the collision is refused and its bytes remain
    untouched.
    """
    target = Path(path)
    target.parent.mkdir(parents=True, exist_ok=True)
    expected = canonical_json_bytes(payload)

    def existing_or_raise() -> Path:
        actual = target.read_bytes()
        if actual == expected:
            return target
        # Legacy pretty-printed records: accept ONLY when the parsed
        # semantic payload exactly equals the newly derived payload. The
        # existing bytes are never normalized or rewritten; semantically
        # different content is still refused (collision).
        try:
            existing_payload = json.loads(actual.decode("utf-8"))
            if existing_payload == json.loads(expected.decode("utf-8")):
                return target
        except Exception:
            pass
        raise WriteOnceIntegrityError(
            f"canonical history collision at {target}: existing bytes differ")

    try:
        return existing_or_raise()
    except FileNotFoundError:
        pass

    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{target.name}.", suffix=".tmp", dir=target.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(expected)
            stream.flush()
            os.fsync(stream.fileno())
        try:
            os.link(temporary, target)
        except FileExistsError:
            return existing_or_raise()
        return target
    finally:
        try:
            temporary.unlink()
        except FileNotFoundError:
            pass
