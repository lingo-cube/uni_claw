#!/usr/bin/env python3
"""Qualify explicitly supplied decision-input material for same-input statistics.

This is a material checklist, not a provider replay or model-quality test. It
never reconstructs a provider request from a prompt/context export.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any


HEX_SHA256 = re.compile(r"^[0-9a-f]{64}$")


def _nonempty(value: Any) -> bool:
    return isinstance(value, str) and bool(value.strip())


def _valid_digest(value: Any) -> bool:
    return isinstance(value, str) and HEX_SHA256.fullmatch(value) is not None


def _material_rows(document: Any, source_path: str) -> list[dict[str, Any]]:
    if isinstance(document, dict) and isinstance(document.get("materials"), list):
        return [row if isinstance(row, dict) else {"materialId": f"row-{i}", "_invalidRow": True}
                for i, row in enumerate(document["materials"], 1)]
    # Existing prompt exports are arrays of observed transport bodies. Preserve
    # them as diagnostic material; their prompt/messages are not provider-envelope proof.
    if isinstance(document, list):
        rows = []
        for index, row in enumerate(document, 1):
            if not isinstance(row, dict):
                row = {}
            rows.append({
                "materialId": f"{row.get('requestId', 'export')}-{row.get('generation', index)}",
                "sourceId": row.get("requestId", f"{source_path}:{index}"),
                "sourceDigest": row.get("sha256"),
                "observedEvidenceKind": row.get("evidenceKind"),
                "observedEligibility": row.get("comparisonEligibility"),
                "_legacyExport": True,
            })
        return rows
    return [{"materialId": Path(source_path).name or "input", "_invalidManifest": True}]


def _assess(row: dict[str, Any]) -> dict[str, Any]:
    reasons: list[str] = []
    if row.get("_invalidRow"):
        reasons.append("material-row-must-be-an-object")
    if row.get("_invalidManifest"):
        reasons.append("manifest-must-be-an-object-with-materials-array")

    final = row.get("finalProviderInput")
    canonical_key = None
    if not isinstance(final, dict):
        reasons.append("final-provider-input-envelope-unproven")
        # Report each independently required field so the result is actionable.
        reasons.extend(("final-messages-missing", "tools-not-recorded",
                        "truncation-rule-missing", "configuration-version-missing",
                        "provider-input-sha256-missing", "provider-capture-evidence-missing"))
    else:
        if final.get("captureBoundary") != "provider-request-envelope":
            reasons.append("final-provider-input-envelope-unproven")
        capture = final.get("captureEvidence")
        if not isinstance(capture, dict) or not _nonempty(capture.get("ref")) or not _valid_digest(capture.get("sha256")):
            reasons.append("provider-capture-evidence-missing")
        messages = final.get("messages")
        if not isinstance(messages, list) or not messages or any(not isinstance(m, dict) for m in messages):
            reasons.append("final-messages-missing")
        tools = final.get("tools")
        if not isinstance(tools, list) or any(not isinstance(t, dict) for t in tools):
            reasons.append("tools-not-recorded")
        if not _nonempty(final.get("truncationRule")):
            reasons.append("truncation-rule-missing")
        if not _nonempty(final.get("configurationVersion")):
            reasons.append("configuration-version-missing")
        if not _valid_digest(final.get("sha256")):
            reasons.append("provider-input-sha256-missing")
        envelope = final.get("requestEnvelope")
        if not isinstance(envelope, dict) or not envelope:
            reasons.append("provider-request-payload-missing")
        elif envelope.get("messages") != messages or envelope.get("tools") != tools:
            reasons.append("provider-envelope-fields-disagree")
        else:
            payload = json.dumps(envelope, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
            payload_digest = hashlib.sha256(payload.encode("utf-8")).hexdigest()
            if final.get("sha256") != payload_digest:
                reasons.append("provider-input-sha256-mismatch")
            canonical_key = hashlib.sha256(json.dumps({
                "request": envelope, "truncationRule": final.get("truncationRule"),
                "configurationVersion": final.get("configurationVersion"),
            }, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")).hexdigest()

    source_id = row.get("sourceId")
    if not _nonempty(source_id):
        reasons.append("source-id-missing")
    source_digest = row.get("sourceDigest")
    if not _valid_digest(source_digest):
        reasons.append("source-digest-missing-or-invalid")

    return {
        "materialId": row.get("materialId") if _nonempty(row.get("materialId")) else "unnamed-material",
        "sourceId": source_id if _nonempty(source_id) else None,
        "sourceDigest": source_digest if _valid_digest(source_digest) else None,
        "comparisonEligibility": "same-input-eligible" if not reasons else "diagnostic-only",
        "eligibleForSameInputStatistics": not reasons,
        "sameInputKey": canonical_key if not reasons else None,
        "reasons": sorted(set(reasons)),
    }


def audit(document: Any, source_path: str = "<input>") -> dict[str, Any]:
    rows = [_assess(row) for row in _material_rows(document, source_path)]
    # Copies with the same declared provenance cannot raise its qualification.
    groups: dict[str, list[dict[str, Any]]] = {}
    for row in rows:
        if row["sourceId"]:
            groups.setdefault(row["sourceId"], []).append(row)
    for group in groups.values():
        if len(group) > 1 and any(not row["eligibleForSameInputStatistics"] for row in group):
            for row in group:
                if row["eligibleForSameInputStatistics"]:
                    row["comparisonEligibility"] = "diagnostic-only"
                    row["eligibleForSameInputStatistics"] = False
                    row["sameInputKey"] = None
                    row["reasons"] = sorted(set(row["reasons"]) | {"same-source-copy-cannot-upgrade"})

    eligible_keys = sorted({row["sameInputKey"] for row in rows if row["sameInputKey"]})
    return {
        "schemaVersion": "decision-input-eligibility/v1",
        "purpose": "material-qualification-only; not provider replay or model-quality evidence",
        "sourcePath": source_path,
        "materials": rows,
        "sameInputEligibleCount": sum(row["eligibleForSameInputStatistics"] for row in rows),
        "sameInputEligibleKeys": eligible_keys,
        "diagnosticOnlyCount": sum(not row["eligibleForSameInputStatistics"] for row in rows),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", help="JSON material manifest (or legacy export to classify diagnostically)")
    parser.add_argument("-o", "--output", help="write report JSON here; stdout when omitted")
    args = parser.parse_args()
    source = Path(args.manifest)
    try:
        document = json.loads(source.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        print(f"cannot read manifest: {exc}", file=sys.stderr)
        return 2
    report = audit(document, str(source))
    serialized = json.dumps(report, ensure_ascii=False, indent=2) + "\n"
    if args.output:
        Path(args.output).write_text(serialized, encoding="utf-8")
    else:
        sys.stdout.write(serialized)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
