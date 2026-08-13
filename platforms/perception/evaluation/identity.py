"""Content-addressed identity + deterministic canonical serialization helpers."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
from typing import Any


def sha256_bytes(data: bytes) -> str:
    """Full lowercase hex SHA-256."""
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: str | Path) -> str:
    """Content hash of a file. Path-independent — only bytes matter."""
    return sha256_bytes(Path(path).read_bytes())


def content_id(data: bytes) -> str:
    """Canonical content-addressed identity: 'sha256:<full hex>'."""
    return f"sha256:{sha256_bytes(data)}"


def canonical_json(obj: Any) -> str:
    """Deterministic canonical serialization: sorted keys, compact separators."""
    return json.dumps(obj, sort_keys=True, ensure_ascii=False, separators=(",", ":"))


def canonical_hash(obj: Any) -> str:
    """SHA-256 over canonical JSON — identity for structured records."""
    return sha256_bytes(canonical_json(obj).encode("utf-8"))
