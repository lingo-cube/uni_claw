"""S1 equivalence baseline gate (WI-PFW-S1E) — IR-G0 row-composition zero-diff.

Freezes the CURRENT fused-candidate output of the retained candidate on the
navigation-row corpus into a byte-level baseline.  After S1B ports the pipeline
into the operator framework, THIS test staying green is the S1 hard gate:
any byte difference between the ported operator output and the frozen baseline
means S1 failed (zero behavior difference required).

Two modes:
  * default (compare): runs the current pipeline over
    tests/corpus/navigation_row_corpus.json and asserts the canonical,
    deterministically serialized fused candidates are byte-identical to
    openspec/changes/perception-operator-rule-framework/evidence/
    s1-equivalence-baseline/baseline.json.  If the baseline is missing the test
    FAILS with regeneration instructions — it never silently passes.
  * P26_REGEN_BASELINE=1 (regenerate): recomputes baseline.json from the corpus
    and asserts capture determinism (two in-memory captures are byte-identical).

Determinism strategy: every candidate float is rounded to 6 decimals, candidates
are ordered by (type, text, x1, y1, id), JSON is dumped with sorted keys, 2-space
indent, LF, no timestamps.  Same inputs => same bytes, always.
"""
from __future__ import annotations

import json
import os
import unittest
from pathlib import Path

from uniclaw_perception.fusion.engine import fuse_evidence, fuse_evidence_from_crops
from uniclaw_perception.schema import Box, Detection, OcrToken

_REPO_MARKER = "AGENTS.md"
_ENV_REGEN = "P26_REGEN_BASELINE"
_FLOAT_PLACES = 6

_CORPUS_REL = Path("platforms/perception/tests/corpus/navigation_row_corpus.json")
_BASELINE_REL = Path(
    "openspec/changes/perception-operator-rule-framework/evidence/"
    "s1-equivalence-baseline/baseline.json"
)

_DEFAULT_PARAMS = {"promote_unmatched_ocr": False, "max_ocr_distance_ratio": 0.055}


def _repo_root() -> Path:
    """Walk up from this file to the directory containing the AGENTS.md marker."""
    start = Path(__file__).resolve().parent
    for directory in (start, *start.parents):
        if (directory / _REPO_MARKER).is_file():
            return directory
    raise RuntimeError(
        f"could not locate repo root: no {_REPO_MARKER} found above {start}"
    )


def _load_json(relative: Path):
    path = _repo_root() / relative
    if not path.is_file():
        raise FileNotFoundError(
            f"missing asset {relative} (resolved to {path}); the corpus/baseline "
            "must be checked in with the change"
        )
    return json.loads(path.read_text(encoding="utf-8"))


# ---------------------------------------------------------------------------
# Corpus → pipeline inputs (construction mirrors tests/test_navigation_row_composition.py;
# the corpus itself is the frozen asset, this code only interprets it).
# ---------------------------------------------------------------------------

def _to_detection(entry: dict) -> Detection:
    return Detection(
        entry["id"], entry["label"], float(entry["confidence"]),
        Box(*[float(v) for v in entry["bounds"]]),
    )


def _to_ocr_token(entry: dict) -> OcrToken:
    return OcrToken(
        entry["id"], entry["text"], float(entry["confidence"]),
        Box(*[float(v) for v in entry["bounds"]]),
    )


def _run_case(case: dict) -> list[dict]:
    """Run the current fusion pipeline for one corpus case; return fused candidates."""
    detections = [_to_detection(d) for d in case["yolo"]]
    tokens = [_to_ocr_token(t) for t in case["ocr"]]
    params = dict(_DEFAULT_PARAMS)
    params.update(case.get("params", {}))

    if case.get("mode", "full") == "crops":
        by_id = {t.id: t for t in tokens}
        crops_ocr = [[by_id[i] for i in slot] for slot in case["crops"]]
        evidence = fuse_evidence_from_crops(
            detections, crops_ocr,
            image_width=int(case["width"]), image_height=int(case["height"]),
        )
    else:
        evidence = fuse_evidence(
            detections, tokens,
            image_width=int(case["width"]), image_height=int(case["height"]),
            promote_unmatched_ocr=bool(params["promote_unmatched_ocr"]),
            max_ocr_distance_ratio=float(params["max_ocr_distance_ratio"]),
        )
    return evidence["candidates"]


# ---------------------------------------------------------------------------
# Canonical serialization (the byte contract of the S1 gate)
# ---------------------------------------------------------------------------

def _deep_round(value):
    """Round every float to 6 decimals (pipeline already rounds; defensive)."""
    if isinstance(value, float):
        return round(value, _FLOAT_PLACES)
    if isinstance(value, list):
        return [_deep_round(item) for item in value]
    if isinstance(value, dict):
        return {key: _deep_round(item) for key, item in value.items()}
    return value


def _candidate_sort_key(candidate: dict):
    x1, y1 = candidate.get("boundsPx", [0, 0])[0], candidate.get("boundsPx", [0, 0])[1]
    return (
        candidate.get("type", ""),
        candidate.get("text", ""),
        x1,
        y1,
        candidate.get("id", ""),
    )


def _canonical_candidates(candidates: list[dict]) -> list[dict]:
    return sorted((_deep_round(c) for c in candidates), key=_candidate_sort_key)


def _candidates_payload(candidates: list[dict]) -> str:
    return (
        json.dumps({"candidates": _canonical_candidates(candidates)},
                   sort_keys=True, indent=2, ensure_ascii=False)
        + "\n"
    )


def _capture_baseline(corpus: list[dict]) -> tuple[list[dict], bytes]:
    """Run every corpus case; return ([{case_id, candidates, notes}], blob-bytes)."""
    entries: list[dict] = []
    for case in corpus:
        candidates = _run_case(case)
        entries.append({
            "case_id": case["case_id"],
            "candidates": _canonical_candidates(candidates),
            "notes": {
                "source_tests": case.get("source_tests", []),
                "yoloCount": len(case["yolo"]),
                "ocrCount": len(case["ocr"]),
                "candidateCount": len(candidates),
            },
        })
    blob = (
        json.dumps(entries, sort_keys=True, indent=2, ensure_ascii=False) + "\n"
    ).encode("utf-8")
    return entries, blob


class RowCompositionEquivalenceTests(unittest.TestCase):
    """Byte-level S1 gate: current pipeline output == frozen baseline."""

    def test_fused_candidates_match_frozen_baseline(self):
        corpus = _load_json(_CORPUS_REL)
        baseline_path = _repo_root() / _BASELINE_REL
        regen = os.environ.get(_ENV_REGEN) == "1"

        if regen:
            self._regenerate(baseline_path, corpus)
            return

        if not baseline_path.is_file():
            self.fail(
                f"baseline missing at {_BASELINE_REL}: cannot run the S1 zero-diff "
                "gate. Regenerate it explicitly with:\n"
                "  P26_REGEN_BASELINE=1 ../../.venv-local-vision/bin/python "
                "-m pytest tests/test_row_composition_equivalence.py -q"
            )

        baseline = json.loads(baseline_path.read_text(encoding="utf-8"))
        by_case = {entry["case_id"]: entry for entry in baseline}
        missing = [case["case_id"] for case in corpus if case["case_id"] not in by_case]
        self.assertEqual(
            missing, [],
            f"baseline misses corpus cases (regenerate?): {missing}",
        )

        # Per-case byte comparison of the canonical fused candidates.
        for case in corpus:
            actual_payload = _candidates_payload(_run_case(case))
            stored_payload = _candidates_payload(by_case[case["case_id"]]["candidates"])
            self.assertEqual(
                actual_payload, stored_payload,
                f"S1 ZERO-DIFF FAILED for case {case['case_id']!r}: current pipeline "
                "output differs byte-wise from the frozen baseline. Any difference "
                "is an S1 hard-gate failure; do not edit the baseline by hand — "
                "regenerate ONLY after an intentional, authorized behavior change "
                "with P26_REGEN_BASELINE=1.",
            )

        # Whole-file byte gate: a fresh capture of the whole corpus must reproduce
        # the checked-in baseline file byte-for-byte (ordering, keys, whitespace).
        _, fresh_blob = _capture_baseline(corpus)
        self.assertEqual(
            fresh_blob, baseline_path.read_bytes(),
            "baseline.json is not byte-canonical: a fresh capture differs from the "
            "checked-in file. Regenerate with P26_REGEN_BASELINE=1 (or this is an "
            "S1 behavior drift).",
        )

    def _regenerate(self, baseline_path: Path, corpus: list[dict]) -> None:
        # Determinism gate: two independent captures must be byte-identical.
        _, first = _capture_baseline(corpus)
        _, second = _capture_baseline(corpus)
        self.assertEqual(first, second, "baseline capture is not deterministic")

        baseline_path.parent.mkdir(parents=True, exist_ok=True)
        baseline_path.write_bytes(first)
        self.assertEqual(
            baseline_path.read_bytes(), first,
            "regenerated baseline did not round-trip byte-identically",
        )


if __name__ == "__main__":
    unittest.main()