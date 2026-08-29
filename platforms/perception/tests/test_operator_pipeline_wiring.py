"""Operator-pipeline wiring tests (OpenSpec
``perception-operator-rule-framework``).

Proves the S1 zero-difference port at the wiring level, updated for the S2ii
routing append (WI-PFW-S2iii):

* the declared topology ``[uniform-list-row-grouping, row-relation-head,
  spacing-verifier]`` runs through the operator framework (registry +
  resolution + executor).  On every ≥4-confirmed-anchor frame the
  row-relation-head step is a delegated noop, so the pipeline stays byte-equal
  to the legacy shim path (the G-2 hard gate); low-anchor frames compose via
  relation-head (Leader-sanctioned deltas; see
  ``evidence/s2-delta-report.md``);
* ``spacing-verifier`` is a VALIDATOR that accepts every output the retained
  candidate produces (no new rejection surface) and fails closed on malformed
  or unauthorized structure, with the executor rolling the generator's output
  back on veto;
* the deterministic trace (input fingerprint, rule-set hash, resolved params
  with provenance, per-step decisions — now three steps) replays
  byte-identically: same inputs + same rule set ⇒ same output + same trace
  bytes;
* the default (root-only) rule set resolves to exactly the operator contract
  defaults and lints clean.

S1B wiring gate alongside the byte-level equivalence gate
(``test_row_composition_equivalence.py``); the assertion updates below are
the Leader-owned S2ii follow-up sanctioned in ``evidence/s2-delta-report.md``.
"""
from __future__ import annotations

import copy
import json
import math
import unittest
from collections import Counter
from pathlib import Path

from uniclaw_perception.fusion.engine import fuse_evidence, fuse_evidence_from_crops
from uniclaw_perception.fusion.row_grouping import apply_uniform_list_grouping
from uniclaw_perception.operators import (
    DEFAULT_CONTEXT,
    DEFAULT_RULE_SET,
    REGISTRY,
    OperatorAuthority,
    Rule,
    SafeDirection,
    execute_pipeline,
    load_rule_set,
    replay,
    resolve,
    rule_set_hash,
    serialize_rule_set,
)
from uniclaw_perception.operators.resolver import DIAG_UNSAFE_VALIDATOR_ADJUSTMENT, lint_rule_set
from uniclaw_perception.operators.spacing_verifier import (
    GENERATED_ROW_REASONS,
    VERIFIER_PARAM_DEFAULTS,
    verify,
)
from uniclaw_perception.schema import Box, Detection, OcrToken

_REPO_ROOT = Path(__file__).resolve().parents[3]  # platforms/perception -> repo root
_CORPUS = _REPO_ROOT / "platforms/perception/tests/corpus/navigation_row_corpus.json"
_BASELINE = (
    _REPO_ROOT / "openspec/changes/perception-operator-rule-framework/evidence/"
    "s1-equivalence-baseline/baseline.json"
)

_DEFAULT_PARAMS = {"promote_unmatched_ocr": False, "max_ocr_distance_ratio": 0.055}


def _load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def _corpus() -> list[dict]:
    return _load_json(_CORPUS)


def _baseline_by_case() -> dict[str, dict]:
    return {entry["case_id"]: entry for entry in _load_json(_BASELINE)}


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


def _case_params(case: dict) -> dict:
    params = dict(_DEFAULT_PARAMS)
    params.update(case.get("params", {}))
    return params


def _deep_round(value):
    if isinstance(value, float):
        return round(value, 6)
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


def _canonical(candidates: list[dict]) -> list[dict]:
    return sorted((_deep_round(c) for c in candidates), key=_candidate_sort_key)


def _candidate_key(candidate: dict) -> str:
    return json.dumps(candidate, sort_keys=True)


def _canonical_counter(candidates: list[dict]) -> Counter:
    return Counter(_candidate_key(c) for c in _canonical(candidates))


def _relation_head_additions(legacy: list[dict], pipeline: list[dict]) -> list[dict]:
    """Pipeline candidates strictly beyond the legacy output (multiset diff).

    Under the S2 routing a low-anchor frame NEVER rewrites the retained
    candidate — relation-head may only ADD rows on top of it (Leader-sanctioned
    deltas on exactly the two low-anchor cases; see ``s2-delta-report.md``).
    """
    legacy_count = _canonical_counter(legacy)
    pipeline_count = _canonical_counter(pipeline)
    additions: list[dict] = []
    for key, count in pipeline_count.items():
        for _ in range(count - legacy_count.get(key, 0)):
            additions.append(json.loads(key))
    return additions


def _relation_head_is_delegated(trace: dict) -> bool:
    """True when the routed relation-head step delegated to uniform-list
    (≥4-confirmed-anchor frames; code-owned routing gate, G-2)."""
    step = next(
        entry for entry in trace["steps"]
        if entry["operator"] == "row-relation-head"
    )
    return step["status"] == "noop" and "delegated" in step["detail"]


def _engine_inputs(case: dict) -> tuple[list[Detection], list[OcrToken]]:
    detections = [_to_detection(e) for e in case["yolo"]]
    tokens = [_to_ocr_token(t) for t in case["ocr"]]
    return detections, tokens


# ---------------------------------------------------------------------------
# The pre-port fusion engine (retained-candidate path) rebuilt for the
# byte-equality comparison: identical to fuse_evidence/fuse_evidence_from_crops
# before S1B wiring, with row grouping via the legacy shim.
# ---------------------------------------------------------------------------

_DEFAULT_INTERACTIVE_LABELS = {
    "button", "list_item", "toggle", "switch", "input", "tab", "icon",
    "popup", "toolbar", "back", "checkbox", "slider", "text_block",
}


def _legacy_fuse(case: dict) -> dict:
    """The pre-port fuse_evidence for one corpus case (shim row grouping)."""
    from uniclaw_perception.fusion.heuristics import (
        apply_chevron_heuristic,
        apply_search_box_labeling,
        apply_toggle_inference_heuristic,
        prune_empty_text_artifacts,
        primary_line_text,
    )
    from uniclaw_perception.fusion.scoring import (
        candidate_risks,
        combined_confidence,
        match_score,
        normalized_center,
    )

    detections, tokens = _engine_inputs(case)
    width, height = int(case["width"]), int(case["height"])
    params = _case_params(case)

    yolo = sorted(
        [d for d in detections if d.label in _DEFAULT_INTERACTIVE_LABELS],
        key=lambda d: (d.box.y1, d.box.x1, d.box.y2, d.box.x2),
    )
    ocr = sorted(
        [t for t in tokens if t.text.strip()],
        key=lambda t: (t.box.y1, t.box.x1, t.box.y2, t.box.x2),
    )
    candidates: list[dict] = []
    matched_ocr_ids: set[str] = set()
    screen_diag = math.hypot(width, height)
    max_distance = screen_diag * float(params["max_ocr_distance_ratio"])

    for index, detection in enumerate(yolo, start=1):
        matches = [
            (token, match_score(detection, token, max_distance))
            for token in ocr
        ]
        matches = [(token, score) for token, score in matches if score > 0]
        matches.sort(key=lambda pair: (-pair[1], pair[0].box.y1, pair[0].box.x1))
        selected = [token for token, _ in matches]
        for token in selected:
            matched_ocr_ids.add(token.id)
        text = primary_line_text(selected)
        evidence_ids = [detection.id] + [token.id for token in selected]
        risks = candidate_risks(detection, selected)
        candidates.append({
            "id": f"candidate_{index}",
            "type": detection.label,
            "text": text,
            "confidence": round(combined_confidence(detection, selected), 6),
            "bounds": detection.box.normalized(width, height),
            "boundsPx": [
                round(detection.box.x1), round(detection.box.y1),
                round(detection.box.x2), round(detection.box.y2),
            ],
            "center": normalized_center(detection, width, height),
            "centerPx": [round(v) for v in detection.box.center()],
            "evidence": {
                "yoloId": detection.id,
                "ocrIds": [token.id for token in selected],
                "allIds": evidence_ids,
            },
            "riskFlags": risks,
        })

    if bool(params["promote_unmatched_ocr"]):
        next_index = len(candidates) + 1
        for token in ocr:
            if token.id in matched_ocr_ids:
                continue
            candidates.append({
                "id": f"candidate_{next_index}",
                "type": "text_block",
                "text": token.text,
                "confidence": round(token.confidence * 0.75, 6),
                "bounds": token.box.normalized(width, height),
                "boundsPx": [
                    round(token.box.x1), round(token.box.y1),
                    round(token.box.x2), round(token.box.y2),
                ],
                "center": normalized_center(token, width, height),
                "centerPx": [round(v) for v in token.box.center()],
                "evidence": {
                    "yoloId": None,
                    "ocrIds": [token.id],
                    "allIds": [token.id],
                },
                "riskFlags": ["ocr_only"],
            })
            next_index += 1

    apply_search_box_labeling(candidates)
    apply_chevron_heuristic(candidates, yolo)
    apply_uniform_list_grouping(candidates, yolo)  # legacy shim path
    apply_toggle_inference_heuristic(candidates, image=None)
    prune_empty_text_artifacts(candidates)
    return {"candidates": candidates}


def _legacy_fuse_crops(case: dict) -> dict:
    from uniclaw_perception.fusion.heuristics import (
        apply_chevron_heuristic,
        apply_search_box_labeling,
        prune_empty_text_artifacts,
        primary_line_text,
    )
    from uniclaw_perception.fusion.scoring import (
        candidate_risks,
        combined_confidence,
        normalized_center,
    )

    detections = [_to_detection(e) for e in case["yolo"]]
    tokens = [_to_ocr_token(t) for t in case["ocr"]]
    width, height = int(case["width"]), int(case["height"])
    by_id = {token.id: token for token in tokens}
    crops_ocr = [[by_id[i] for i in slot] for slot in case["crops"]]

    candidates: list[dict] = []
    all_tokens: list[OcrToken] = []
    for detection, crop_tokens in zip(detections, crops_ocr):
        all_tokens.extend(crop_tokens)
        selected = [t for t in crop_tokens if t.text.strip()]
        text = primary_line_text(selected)
        risks = candidate_risks(detection, selected)
        candidates.append({
            "id": f"candidate_{len(candidates) + 1}",
            "type": detection.label,
            "text": text,
            "confidence": round(combined_confidence(detection, selected), 6),
            "confidenceDetail": {
                "yolo": round(detection.confidence, 6),
                "ocr": (
                    round(sum(t.confidence for t in selected) / len(selected), 6)
                    if selected else None
                ),
            },
            "bounds": detection.box.normalized(width, height),
            "boundsPx": [
                round(detection.box.x1), round(detection.box.y1),
                round(detection.box.x2), round(detection.box.y2),
            ],
            "center": normalized_center(detection, width, height),
            "centerPx": [round(v) for v in detection.box.center()],
            "evidence": {
                "yoloId": detection.id,
                "ocrIds": [t.id for t in selected],
                "allIds": [detection.id] + [t.id for t in selected],
            },
            "riskFlags": risks,
        })

    apply_search_box_labeling(candidates)
    apply_chevron_heuristic(candidates, list(detections))
    apply_uniform_list_grouping(candidates, list(detections))  # legacy shim path
    prune_empty_text_artifacts(candidates)
    return {"candidates": candidates}


# ---------------------------------------------------------------------------
# Synthetic candidate builders for verifier/rollback tests
# ---------------------------------------------------------------------------

def _row(identifier: str, y_center: float, *, type_: str = "menu_item",
         x1: float = 120.0, text: str | None = None,
         type_inferred: str | None = None) -> dict:
    height = 30.0
    candidate = {
        "id": identifier,
        "type": type_,
        "text": text if text is not None else f"Row {identifier}",
        "confidence": 0.95,
        "bounds": {"x1": 0.1, "y1": y_center / 1000.0, "x2": 0.4, "y2": (y_center + height) / 1000.0},
        "boundsPx": [int(x1), int(y_center - height / 2), int(x1 + 180), int(y_center + height / 2)],
        "center": {"x": 0.2, "y": y_center / 1000.0},
        "centerPx": [int(x1) + 90, int(y_center)],
        "evidence": {"yoloId": identifier, "ocrIds": [], "allIds": [identifier]},
        "riskFlags": [],
    }
    if type_inferred is not None:
        candidate["evidence"]["typeInferred"] = type_inferred
    return candidate


class PipelineWiringTests(unittest.TestCase):
    """Topology, resolution, and byte-equal wiring vs the legacy shim."""

    def test_declared_topology_and_authority(self):
        # S2 routing (Leader-sanctioned wiring; see s2-delta-report.md
        # "Trace shape change"): the declared pipeline is the 5-operator
        # topology [uniform-list-row-grouping, row-relation-head,
        # spacing-verifier, text-relation-check, structured-corroboration]
        # (S4 wiring; vlm-annotation is ADVISOR, offline-only, never routed).
        self.assertEqual(
            REGISTRY.operators_for_pipeline(),
            ("uniform-list-row-grouping", "row-relation-head", "spacing-verifier",
             "text-relation-check", "structured-corroboration"),
        )
        generator = REGISTRY.lookup("uniform-list-row-grouping")
        self.assertIs(generator.authority, OperatorAuthority.GENERATOR)
        self.assertIn("enabled", generator.parameters)  # built-in GENERATOR param
        relation_head = REGISTRY.lookup("row-relation-head")
        self.assertIs(relation_head.authority, OperatorAuthority.GENERATOR)
        self.assertIn("enabled", relation_head.parameters)  # built-in GENERATOR param
        verifier = REGISTRY.lookup("spacing-verifier")
        self.assertIs(verifier.authority, OperatorAuthority.VALIDATOR)
        self.assertNotIn("enabled", verifier.parameters)  # cannot be disabled
        for name, spec in verifier.parameters.items():
            self.assertIs(spec.safe_direction, SafeDirection.TIGHTEN_ONLY)

    def test_default_rule_set_lints_clean_and_loads(self):
        self.assertEqual(lint_rule_set(DEFAULT_RULE_SET, REGISTRY), [])
        loaded = load_rule_set(serialize_rule_set(DEFAULT_RULE_SET), REGISTRY)
        self.assertTrue(loaded.is_valid)
        # S2i registered row-relation-head (register-only); S2ii wired it into
        # the executed topology — the default rule set carries one root rule
        # per REGISTERED operator (3) and the EXECUTED topology is the
        # 3-operator pipeline (see registry_defaults.py and the S2 routing).
        self.assertEqual(len(loaded.rules), 6)  # S4: one root rule per registered operator (vlm included)

    def test_resolved_params_equal_contract_defaults(self):
        resolved = resolve(DEFAULT_RULE_SET, DEFAULT_CONTEXT, REGISTRY)
        by_id = {entry.operator_id: entry for entry in resolved}
        self.assertEqual(
            set(by_id),
            {"uniform-list-row-grouping", "row-relation-head", "spacing-verifier",
             "text-relation-check", "structured-corroboration", "vlm-annotation"},
        )
        for operator_id, entry in by_id.items():
            contract = REGISTRY.lookup(operator_id)
            for name, spec in contract.parameters.items():
                self.assertEqual(
                    entry.values[name], spec.default,
                    f"{operator_id}.{name} must resolve to its contract default",
                )
                self.assertEqual(
                    entry.provenance[name].rule_id, f"root-{operator_id}",
                    "root rule must carry the provenance of the default value",
                )
                self.assertEqual(entry.provenance[name].specificity, 0)

    def test_pipeline_byte_equal_to_legacy_shim(self):
        # S2 routing: on ≥4-confirmed-anchor frames the row-relation-head step
        # is a delegated noop, so the pipeline is byte-equal to the legacy shim
        # (G-2 hard gate).  On low-anchor frames relation-head composes on top
        # of the retained candidate (Leader-sanctioned deltas; see
        # s2-delta-report.md) — every shim candidate must still appear
        # byte-identically, and every ADDED candidate must carry the
        # ``row_relation_head`` provenance.
        for case in _corpus():
            trace_sink: list[dict] = []
            detections, tokens = _engine_inputs(case)
            params = _case_params(case)
            if case.get("mode", "full") == "crops":
                by_id = {token.id: token for token in tokens}
                crops_ocr = [[by_id[i] for i in slot] for slot in case["crops"]]
                via_pipeline = fuse_evidence_from_crops(
                    detections, crops_ocr,
                    image_width=int(case["width"]),
                    image_height=int(case["height"]),
                    trace_sink=trace_sink.append,
                )["candidates"]
                legacy = _legacy_fuse_crops(case)["candidates"]
            else:
                via_pipeline = fuse_evidence(
                    detections, tokens,
                    image_width=int(case["width"]),
                    image_height=int(case["height"]),
                    promote_unmatched_ocr=bool(params["promote_unmatched_ocr"]),
                    max_ocr_distance_ratio=float(params["max_ocr_distance_ratio"]),
                    trace_sink=trace_sink.append,
                )["candidates"]
                legacy = _legacy_fuse(case)["candidates"]
            self.assertEqual(len(trace_sink), 1, case["case_id"])
            if _relation_head_is_delegated(trace_sink[0]):
                # ≥4-anchor frame: byte-identical to the legacy shim (G-2).
                self.assertEqual(
                    via_pipeline, legacy,
                    f"S2 routed wiring drifted from the retained candidate on "
                    f"the ≥4-anchor corpus case {case['case_id']!r}: framework "
                    "pipeline output differs byte-wise from the legacy shim path",
                )
            else:
                # Low-anchor frame: shim preserved + sanctioned relation-head
                # additions only (s2-delta-report.md).
                shim_keys = _canonical_counter(legacy)
                pipeline_keys = _canonical_counter(via_pipeline)
                for key, count in shim_keys.items():
                    self.assertGreaterEqual(
                        pipeline_keys[key], count,
                        f"low-anchor corpus case {case['case_id']!r}: legacy "
                        "shim candidate missing from the routed pipeline output",
                    )
                for added in _relation_head_additions(legacy, via_pipeline):
                    self.assertEqual(
                        added["evidence"].get("typeInferred"),
                        "row_relation_head",
                        f"low-anchor corpus case {case['case_id']!r}: routed "
                        f"pipeline added candidate {added.get('id')!r} without "
                        "the sanctioned row_relation_head provenance",
                    )


class SpacingVerifierTests(unittest.TestCase):
    """VALIDATOR acceptance (no new rejection surface) and fail-closed veto."""

    def test_verifier_accepts_all_baseline_outputs(self):
        baseline = _baseline_by_case()
        for case_id, entry in sorted(baseline.items()):
            verdict = verify(entry["candidates"], [], VERIFIER_PARAM_DEFAULTS)
            self.assertEqual(
                verdict["status"], "verified",
                f"spacing-verifier must accept the retained candidate's output "
                f"for corpus case {case_id!r} (no new rejection surface)",
            )

    def test_verifier_rejects_malformed_structure(self):
        good = _row("g1", 100.0)
        cases = {
            "empty-text": [_row("e", 100.0, text="   ")],
            "inverted-bounds": [
                {**_row("i", 100.0), "boundsPx": [300, 10, 120, 40]}
            ],
            "center-outside-bounds": [
                {**_row("c", 100.0), "centerPx": [5, 5]}
            ],
            "unauthorized-provenance": [
                _row("p", 100.0, type_inferred="text_relation_fabricated")
            ],
        }
        for label, candidates in cases.items():
            verdict = verify(candidates, [], VERIFIER_PARAM_DEFAULTS)
            self.assertEqual(
                verdict["status"], "rejected", f"malformed {label!r} must veto"
            )
            self.assertIn("fail-closed", verdict["detail"])
        self.assertEqual(
            verify([good], [], VERIFIER_PARAM_DEFAULTS)["status"], "verified"
        )

    def test_verifier_cap_compliance(self):
        many = [
            _row(f"r{i}", 100.0 + i * 100.0, type_inferred="uniform_list_bracketed_row")
            for i in range(201)
        ]
        verdict = verify(many, [], {"maxMenuItems": 200})
        self.assertEqual(verdict["status"], "rejected")
        self.assertIn("cap", verdict["detail"])

    def test_tighten_only_rejects_loosening_rules(self):
        for param in sorted(VERIFIER_PARAM_DEFAULTS):
            default = VERIFIER_PARAM_DEFAULTS[param]
            loosened = default * 0.5 if isinstance(default, float) else default // 2
            rules = [Rule(f"loose-{param}", pins={}, params={
                f"spacing-verifier.{param}": loosened})]
            diagnostics = lint_rule_set(rules, REGISTRY)
            unsafe = [d for d in diagnostics if d.kind == DIAG_UNSAFE_VALIDATOR_ADJUSTMENT]
            self.assertEqual(
                len(unsafe), 1,
                f"loosening tighten-only spacing-verifier.{param} must be rejected",
            )
        # At/above the default is a legal tightening.
        tightened = [Rule("tight", pins={}, params={
            "spacing-verifier.minStepRatio": 0.5})]
        self.assertEqual(lint_rule_set(tightened, REGISTRY), [])

    def test_pipeline_rolls_back_on_verifier_veto(self):
        # A proven 4-anchor grid with one 2-step gap (a real bracketed row is
        # generated at y=400) plus an unauthorized menu_item at y=600: the
        # generator activates and mutates candidates, then the verifier vets
        # the unauthorized provenance and the executor rolls the generator's
        # output back (fail-closed; unreachable for pipeline outputs, but the
        # mechanism must be deterministic).
        candidates = [
            _row("a1", 100.0),
            _row("a2", 200.0),
            _row("a3", 300.0),
            _row("a5", 500.0),
            _row("slot", 400.0, type_="text_block", text="Missing row"),
            _row("fake", 600.0, type_inferred="text_relation_fabricated"),
        ]
        expected = copy.deepcopy(candidates)
        result, trace = execute_pipeline(
            candidates, [],
            registry=REGISTRY, rules=DEFAULT_RULE_SET, context=DEFAULT_CONTEXT,
        )
        # S2 routing: the veto now lands on the THIRD step (spacing-verifier)
        # after the row-relation-head delegated-noop step; the ≥4-anchor grid
        # means relation-head delegates (see s2-delta-report.md "Trace shape
        # change").  Rollback semantics are unchanged.
        self.assertEqual(result, expected, "veto must roll back generator output")
        self.assertEqual(trace.steps[0]["status"], "activated")
        self.assertEqual(trace.steps[1]["operator"], "row-relation-head")
        self.assertEqual(trace.steps[1]["status"], "noop")
        self.assertIn("delegated", trace.steps[1]["detail"])
        self.assertEqual(trace.steps[2]["operator"], "spacing-verifier")
        self.assertEqual(trace.steps[2]["status"], "fail_closed")
        self.assertIn("text_relation_fabricated", trace.steps[2]["detail"])

    def test_generator_disabled_by_rule_is_noop(self):
        root = next(r for r in DEFAULT_RULE_SET if r.rule_id == "root-uniform-list-row-grouping")
        params = {k: v for k, v in root.params.items() if k != "uniform-list-row-grouping.enabled"}
        rules = [
            Rule("root-uniform-list-row-grouping", pins={}, params={
                **params, "uniform-list-row-grouping.enabled": False}),
            next(r for r in DEFAULT_RULE_SET if r.rule_id == "root-spacing-verifier"),
        ]
        self.assertEqual(lint_rule_set(rules, REGISTRY), [])
        candidates = [_row("s", 400.0, type_="text_block", text="Stray title")]
        result, trace = execute_pipeline(
            candidates, [], registry=REGISTRY, rules=rules, context=DEFAULT_CONTEXT,
        )
        # S2 routing (see s2-delta-report.md): with uniform-list disabled the
        # pipeline still runs the row-relation-head step; this executor call
        # supplies no raw visual sources, so the adapter fails closed there
        # too (it never composes from composed candidates) — the candidates
        # stay untouched and the verifier then passes.
        self.assertEqual(result, candidates, "disabled generator must not mutate")
        self.assertEqual(trace.steps[0]["status"], "noop")
        self.assertIn("disabled by rule configuration", trace.steps[0]["detail"])
        self.assertEqual(trace.steps[1]["operator"], "row-relation-head")
        self.assertEqual(trace.steps[1]["status"], "noop")
        self.assertIn("fail-closed", trace.steps[1]["detail"])
        self.assertEqual(trace.steps[2]["operator"], "spacing-verifier")
        self.assertEqual(trace.steps[2]["status"], "verified")


class TraceReplayTests(unittest.TestCase):
    """Deterministic trace and offline replay (S1.8)."""

    def test_replay_deterministic_bytes(self):
        for case in _corpus()[:6]:
            _, first = replay(case)
            _, second = replay(case)
            self.assertEqual(
                first.to_bytes(), second.to_bytes(),
                f"trace must be byte-identical across replays of {case['case_id']!r}",
            )
            self.assertEqual(first.rule_set_hash, rule_set_hash(DEFAULT_RULE_SET))
            self.assertIsNotNone(first.input_fingerprint)

    def test_replay_matches_frozen_baseline(self):
        baseline = _baseline_by_case()
        for case in _corpus():
            candidates, trace = replay(case)
            # G-7 (unchanged strength): replay stays byte-stable — including
            # on the two Leader-sanctioned low-anchor deltas, whose frozen
            # equivalence is re-asserted below on the shape, not the bytes.
            candidates_again, _ = replay(case)
            self.assertEqual(
                _canonical(candidates), _canonical(candidates_again),
                f"offline replay of {case['case_id']!r} must be byte-stable",
            )
            frozen = baseline[case["case_id"]]["candidates"]
            if _relation_head_is_delegated(trace.to_dict()):
                # ≥4-anchor frame: byte-equal to the frozen S1 baseline (G-2).
                self.assertEqual(
                    _canonical(candidates), frozen,
                    f"offline replay of the ≥4-anchor corpus case "
                    f"{case['case_id']!r} diverged from the frozen baseline",
                )
            else:
                # Low-anchor frame: relation-head only ADDS to the frozen
                # candidates (Leader-sanctioned deltas; see s2-delta-report.md
                # Changed-case-1/2) — every frozen candidate stays
                # byte-identically and additions carry row_relation_head.
                frozen_keys = _canonical_counter(frozen)
                pipeline_keys = _canonical_counter(candidates)
                for key, count in frozen_keys.items():
                    self.assertGreaterEqual(
                        pipeline_keys[key], count,
                        f"low-anchor corpus case {case['case_id']!r}: frozen "
                        "baseline candidate missing from the routed replay",
                    )
                for added in _relation_head_additions(frozen, candidates):
                    self.assertEqual(
                        added["evidence"].get("typeInferred"),
                        "row_relation_head",
                        f"low-anchor corpus case {case['case_id']!r}: routed "
                        f"replay added candidate {added.get('id')!r} without "
                        "the sanctioned row_relation_head provenance",
                    )

    def test_trace_steps_record_each_decision(self):
        # S2 routing: three pipeline steps in declared order — uniform-list →
        # row-relation-head → spacing-verifier (the relation-head step is a
        # delegated noop on ≥4-anchor frames; see s2-delta-report.md "Trace
        # shape change").
        for case in _corpus():
            _, trace = replay(case)
            self.assertEqual(
                [step["operator"] for step in trace.steps],
                ["uniform-list-row-grouping", "row-relation-head", "spacing-verifier",
                 "text-relation-check", "structured-corroboration"],
                case["case_id"],
            )
            self.assertEqual(trace.steps[0]["authority"], "GENERATOR")
            self.assertIn(
                trace.steps[0]["status"], ("activated", "noop"), case["case_id"]
            )
            if trace.steps[0]["status"] == "noop":
                self.assertIn("fail-closed", trace.steps[0]["detail"])
            self.assertEqual(trace.steps[1]["authority"], "GENERATOR")
            self.assertIn(
                trace.steps[1]["status"], ("activated", "noop"), case["case_id"]
            )
            if trace.steps[1]["status"] == "noop":
                self.assertTrue(
                    "delegated" in trace.steps[1]["detail"]
                    or "fail-closed" in trace.steps[1]["detail"],
                    f"relation-head noop must record its routing or "
                    f"fail-closed reason: {case['case_id']!r}",
                )
            self.assertEqual(trace.steps[2]["operator"], "spacing-verifier")
            self.assertEqual(trace.steps[2]["authority"], "VALIDATOR")
            self.assertEqual(trace.steps[2]["status"], "verified", case["case_id"])

    def test_different_rule_set_changes_hash_keeps_fingerprint(self):
        extra = Rule("android-5", {"system": "android"},
                     params={"uniform-list-row-grouping.minAnchors": 5})
        rules_b = [*DEFAULT_RULE_SET, extra]
        candidates_a, trace_a = execute_pipeline(
            [], [], registry=REGISTRY, rules=DEFAULT_RULE_SET, context=DEFAULT_CONTEXT,
            input_sources={"yolo": [], "ocr": []},
        )
        candidates_b, trace_b = execute_pipeline(
            [], [], registry=REGISTRY, rules=rules_b, context=DEFAULT_CONTEXT,
            input_sources={"yolo": [], "ocr": []},
        )
        self.assertEqual(candidates_a, candidates_b)
        self.assertEqual(trace_a.input_fingerprint, trace_b.input_fingerprint)
        self.assertNotEqual(trace_a.rule_set_hash, trace_b.rule_set_hash)
        self.assertNotEqual(trace_a.to_bytes(), trace_b.to_bytes())

    def test_resolved_params_embedded_in_trace(self):
        _, trace = replay(_corpus()[0])
        resolved = {
            entry["operatorId"]: entry
            for entry in trace.resolved_params
        }
        # The trace embeds the FULL default-rule-set resolution (all registered
        # operators); row-relation-head is wired into the executed topology
        # (S2ii routing — see registry_defaults.py and s2-delta-report.md).
        self.assertEqual(
            set(resolved),
            {"uniform-list-row-grouping", "row-relation-head", "spacing-verifier",
             "text-relation-check", "structured-corroboration", "vlm-annotation"},
        )
        contract = REGISTRY.lookup("uniform-list-row-grouping")
        for name, spec in contract.parameters.items():
            self.assertEqual(
                resolved["uniform-list-row-grouping"]["values"][name], spec.default
            )
            self.assertEqual(
                resolved["uniform-list-row-grouping"]["provenance"][name]["ruleId"],
                "root-uniform-list-row-grouping",
            )


if __name__ == "__main__":
    unittest.main()