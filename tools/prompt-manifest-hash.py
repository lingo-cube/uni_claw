#!/usr/bin/env python3
"""PRF-003 — product prompt manifest hash tool.

Computes the deterministic artifact identity for a product prompt manifest
directory (manifest.json bytes followed by each segment's bytes, in the order
listed by `segments`) and writes/verifies `prompt-hash.txt` in that directory.

Usage:
  python3 tools/prompt-manifest-hash.py <manifest-dir>          # write hash
  python3 tools/prompt-manifest-hash.py <manifest-dir> --check  # verify only

Fail-closed: unknown segment file, missing manifest, or hash mismatch (in
--check mode) exits non-zero. Canonical dir: product/prompt/uniagent-prod/;
the plugin package copy (dsh/uniclaw-decision-channel/prompt/) must stay
byte-identical (enforced by the plugin sync test).
"""
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path


def compute(root: Path) -> str:
    manifest_path = root / "manifest.json"
    if not manifest_path.is_file():
        raise SystemExit(f"prompt-manifest: manifest.json not found in {root}")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    segments = manifest.get("segments")
    if not isinstance(segments, list) or not segments:
        raise SystemExit("prompt-manifest: manifest.segments must be a non-empty list")
    digest = hashlib.sha256()
    digest.update(manifest_path.read_bytes())
    for name in segments:
        segment = root / name
        if not segment.is_file():
            raise SystemExit(f"prompt-manifest: segment file missing: {name}")
        digest.update(segment.read_bytes())
    return digest.hexdigest()


def main(argv: list[str]) -> int:
    if len(argv) not in (2, 3):
        raise SystemExit(__doc__)
    root = Path(argv[1])
    check = "--check" in argv[2:]
    digest = compute(root)
    hash_path = root / "prompt-hash.txt"
    if check:
        if not hash_path.is_file():
            raise SystemExit(f"prompt-manifest: prompt-hash.txt missing in {root}")
        recorded = hash_path.read_text(encoding="utf-8").strip()
        if recorded != digest:
            raise SystemExit(
                f"prompt-manifest: hash mismatch (recorded {recorded}, computed {digest}); "
                "regenerate with tools/prompt-manifest-hash.py and bump promptRevision")
        print(f"prompt-manifest: PASS ({digest[:12]}…)")
        return 0
    hash_path.write_text(digest + "\n", encoding="utf-8")
    print(f"prompt-manifest: wrote {digest}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
