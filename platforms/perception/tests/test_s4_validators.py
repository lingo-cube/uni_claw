"""S4 validator / advisor tests (WI-PFW-S4, OpenSpec change
``perception-operator-rule-framework``).

Covers the three non-generating operators added by S4:

* ``text-relation-check`` VALIDATOR — conflict-checks composed head texts
  (empty/too-short head; verbatim duplicate head text at the same position);
  may only veto or downgrade confidence; NEVER generates candidates; no
  ``enabled`` parameter; annotate-only (candidates byte-unchanged).
* ``structured-corroboration`` VALIDATOR — optional uiautomator-style
  structured tier cross-check; absent channel passes trivially; XML never an
  identity source; the only veto is the maximally conservative
  strong-contradiction case; no ``enabled`` parameter.
* ``vlm-annotation`` ADVISOR — offline-only deterministic no-op stub
  (``propose_parameter_adjustments``), registered in the REGISTRY with
  ``enabled`` default ``False``, deliberately NOT in the declared pipeline and
  NOT in ``RUNNERS`` (no online call path).

Plus the leader-frozen pipeline assertion (the executed topology is exactly
the previous 3 operators + the 2 new VALIDATORs) and the corpus-wide
zero-veto gate over all 34 cases (28 S1 navigation-row + 6 cross-UI).
"""
from __future__ import annotations

import copy
import json
import unittest
from pathlib import Path

from uniclaw_perception.operators import (
    DEFAULT_CONTEXT,
    DEFAULT_RULE_SET,
    REGISTRY,
    RUNNERS,
    OperatorAuthority,
    ParameterType,
    SafeDirection,
    lint_rule_set,
    resolve,
)
from uniclaw_perception.operators.contracts import OperatorContract, ParameterSpec
from uniclaw_perception.operators.structured_corroboration import (
    CORROBORATION_PARAM_BOUNDS,
    CORROBORATION_PARAM_DEFAULTS,
    IN_DOUBT_DELTA,
    STATIC_LABEL_DELTA,
    corroborate,
    run as run_structured_corroboration,
)
from uniclaw_perception.operators.text_relation_check import (
    CONFLICT_DELTA,
    TEXT_RELATION_PARAM_BOUNDS,
    TEXT_RELATION_PARAM_DEFAULTS,
    check as check_text_relation,
    run as run_text_relation_check,
)
from uniclaw_perception.operators.trace import replay
from uniclaw_perception.operators.vlm_annotation import (
    VLM_ENABLED_DEFAULT,
    propose_parameter_adjustments,
)

_REPO_ROOT = Path(__file__).resolve().parents[3]
_NAV_CORPUS = (
    _REPO_ROOT / "platforms/perception/tests/corpus/navigation_row_corpus.json"
)
_CROSS_UI_CORPUS = (
    _REPO_ROOT / "platforms/perception/tests/corpus/cross_ui_row_corpus.json"
)

_PIPELINE_5 = (
    "uniform-list-row-grouping",
    "row-relation-head",
    "spacing-verifier",
    "text-relation-check",
    "structured-corroboration",
)


def _load_json(path: Path) -> list[dict]:
    return json.loads(path.read_text(encoding="utf-8"))


def _all_corpus_cases() -> list[dict]:
    cases = list(_load_json(_NAV_CORPUS))
    cases.extend(_load_json(_CROSS_UI_CORPUS))
    return cases


def _row(
    candidate_id: str,
    text: str,
    bounds: tuple[float, float, float, float],
    type_: str = "menu_item",
    type_inferred: str | None = "row_relation_head",
) -> dict:
    x1, y1, x2, y2 = bounds
    candidate = {
        "id": candidate_id,
        "type": type_,
        "text": text,
        "confidence": 0.9,
        "boundsPx": [x1, y1, x2, y2],
        "centerPx": [(x1 + x2) / 2.0, (y1 + y2) / 2.0],
        "evidence": {"yoloId": candidate_id, "ocrIds": [], "allIds": [candidate_id]},
        "riskFlags": [],
    }
    if type_inferred is not None:
        candidate["evidence"]["typeInferred"] = type_inferred
    return candidate


def _structured(
    node_id: str,
    text: str,
    bounds: tuple[float, float, float, float],
    clickable: bool = False,
    focusable: bool = False,
) -> dict:
    d = {
        "id": node_id,
        "text": text,
        "clickable": clickable,
        "focusable": focusable,
        "boundsPx": list(bounds),
    }
    return d


class ContractAuthorityTests(unittest.TestCase):
    """S4 operator contracts: authority classes, param bounds, enabled rules."""

    def test_text_relation_check_validator_no_enabled(self):
        contract = REGISTRY.lookup("text-relation-check")
        self.assertIs(contract.authority, OperatorAuthority.VALIDATOR)
        self.assertEqual(contract.version, "1.0.0")
        self.assertNotIn("enabled", contract.parameters)
        for name, default in TEXT_RELATION_PARAM_DEFAULTS.items():
            spec = contract.parameters[name]
            self.assertEqual(spec.default, default)
            kind, (low, high) = TEXT_RELATION_PARAM_BOUNDS[name]
            self.assertEqual(
                (spec.bounds.min_value, spec.bounds.max_value), (low, high)
            )
            self.assertIs(spec.safe_direction, SafeDirection.TIGHTEN_ONLY)

    def test_structured_corroboration_validator_no_enabled(self):
        contract = REGISTRY.lookup("structured-corroboration")
        self.assertIs(contract.authority, OperatorAuthority.VALIDATOR)
        self.assertNotIn("enabled", contract.parameters)
        for name, default in CORROBORATION_PARAM_DEFAULTS.items():
            spec = contract.parameters[name]
            self.assertEqual(spec.default, default)
            kind, (low, high) = CORROBORATION_PARAM_BOUNDS[name]
            self.assertEqual(
                (spec.bounds.min_value, spec.bounds.max_value), (low, high)
            )
            self.assertIs(spec.safe_direction, SafeDirection.TIGHTEN_ONLY)

    def test_vlm_annotation_advisor_enabled_false(self):
        contract = REGISTRY.lookup("vlm-annotation")
        self.assertIs(contract.authority, OperatorAuthority.ADVISOR)
        enabled = contract.parameter("enabled")
        self.assertIsNotNone(enabled)
        self.assertIs(enabled.type, ParameterType.BOOLEAN)
        self.assertIs(enabled.default, False)
        self.assertIs(enabled.default, VLM_ENABLED_DEFAULT)

    def test_validator_enabled_still_rejected_advisor_allowed(self):
        # The authority boundary stays hard for VALIDATORs; S4 extends ADVISOR
        # only (leader-frozen: "ADVISOR may carry enabled, default false").
        with self.assertRaises(ValueError):
            OperatorContract(
                operator_id="v", version="1.0.0",
                authority=OperatorAuthority.VALIDATOR,
                input_kinds=frozenset(), output_kind="k",
                parameters={"enabled": _enabled_spec()},
                fail_closed_description="d",
            )
        advisor = OperatorContract(
            operator_id="a", version="1.0.0",
            authority=OperatorAuthority.ADVISOR,
            input_kinds=frozenset(), output_kind="k",
            parameters={"enabled": _enabled_spec()},
            fail_closed_description="d",
        )
        self.assertIs(advisor.parameter("enabled").default, False)

    def test_pipeline_exact_membership_no_vlm(self):
        # Leader-frozen: the executed topology is exactly the previous 3
        # operators + the 2 new VALIDATORs; vlm-annotation is NEVER declared.
        self.assertEqual(REGISTRY.operators_for_pipeline(), _PIPELINE_5)
        self.assertNotIn("vlm-annotation", REGISTRY.operators_for_pipeline())

    def test_vlm_has_no_runner_or_pipeline_slot(self):
        self.assertNotIn("vlm-annotation", RUNNERS)
        for new_validator in ("text-relation-check", "structured-corroboration"):
            self.assertIn(new_validator, RUNNERS)

    def test_default_rule_set_lints_clean_with_s4_operators(self):
        self.assertEqual(lint_rule_set(DEFAULT_RULE_SET, REGISTRY), [])
        resolved = resolve(DEFAULT_RULE_SET, DEFAULT_CONTEXT, REGISTRY)
        by_id = {entry.operator_id: entry for entry in resolved}
        for operator_id in _PIPELINE_5 + ("vlm-annotation",):
            contract = REGISTRY.lookup(operator_id)
            for name, spec in contract.parameters.items():
                self.assertEqual(
                    by_id[operator_id].values[name], spec.default,
                    f"{operator_id}.{name} must resolve to its contract default",
                )


def _enabled_spec():
    return ParameterSpec("enabled", ParameterType.BOOLEAN, False)


class TextRelationCheckTests(unittest.TestCase):
    """Veto / downgrade semantics for ``text-relation-check``."""

    def test_veto_empty_head_text(self):
        candidates = [_row("r1", "   ", (0, 0, 100, 40))]
        verdict = check_text_relation(candidates, TEXT_RELATION_PARAM_DEFAULTS)
        self.assertEqual(verdict["status"], "rejected")
        self.assertIn("fail-closed", verdict["detail"])
        self.assertEqual(verdict["annotations"], [])

    def test_veto_head_below_min_head_text_length(self):
        candidates = [_row("r1", "Ab", (0, 0, 100, 40))]
        verdict = check_text_relation(
            candidates, {"min_head_text_length": 5}
        )
        self.assertEqual(verdict["status"], "rejected")

    def test_veto_duplicate_text_at_same_position(self):
        candidates = [
            _row("r1", "Duplicate", (10, 20, 200, 60)),
            _row("r2", "Duplicate", (12, 21, 198, 59)),
        ]
        verdict = check_text_relation(candidates, TEXT_RELATION_PARAM_DEFAULTS)
        self.assertEqual(verdict["status"], "rejected")
        self.assertIn("same position", verdict["detail"])

    def test_same_text_different_positions_verified_and_annotated(self):
        candidates = [
            _row("r1", "Network", (0, 100, 200, 140)),
            _row("r2", "Network", (0, 300, 200, 340)),
        ]
        before = copy.deepcopy(candidates)
        verdict = check_text_relation(candidates, TEXT_RELATION_PARAM_DEFAULTS)
        self.assertEqual(verdict["status"], "verified")
        deltas = [a for a in verdict["annotations"]
                  if a["kind"] == "confidence_delta"]
        self.assertEqual(len(deltas), 2)
        self.assertEqual(deltas[0]["confidenceDelta"], CONFLICT_DELTA)
        self.assertEqual(candidates, before, "check must never mutate candidates")

    def test_non_head_empty_text_not_an_anomaly(self):
        candidates = [_row("t1", "", (0, 0, 100, 40), type_="text_block")]
        verdict = check_text_relation(candidates, TEXT_RELATION_PARAM_DEFAULTS)
        self.assertEqual(verdict["status"], "verified")

    def test_never_generates_candidates(self):
        verdict = check_text_relation([], TEXT_RELATION_PARAM_DEFAULTS)
        self.assertEqual(verdict["status"], "verified")
        self.assertEqual(verdict["annotations"], [])

    def test_runner_protocol(self):
        candidates = [_row("r1", "Fine", (0, 0, 100, 40))]
        verdict = run_text_relation_check(candidates, [], TEXT_RELATION_PARAM_DEFAULTS)
        self.assertEqual(verdict["status"], "verified")


class StructuredCorroborationTests(unittest.TestCase):
    """Structured-tier cross-checks for ``structured-corroboration``."""

    def test_absent_channel_passes_trivially(self):
        candidates = [_row("r1", "Settings", (0, 0, 200, 40))]
        # Executor call shape: three arguments, no raw_sources at all.
        verdict = run_structured_corroboration(
            candidates, [], CORROBORATION_PARAM_DEFAULTS
        )
        self.assertEqual(verdict["status"], "verified")
        self.assertIn("no structured evidence", verdict["detail"])
        # raw_sources present but without a ``structured`` key: same trivial pass.
        verdict2 = run_structured_corroboration(
            candidates, [], CORROBORATION_PARAM_DEFAULTS,
            raw_sources={"detections": [], "ocr": [], "width": 400, "height": 800},
        )
        self.assertEqual(verdict2["status"], "verified")
        self.assertIn("no structured evidence", verdict2["detail"])

    def test_non_clickable_static_label_downgrades(self):
        candidates = [_row("r1", "Wallpaper", (0, 0, 200, 40))]
        nodes = [_structured("n1", "Wallpaper", (0, 0, 200, 40))]
        before = copy.deepcopy(candidates)
        verdict = corroborate(candidates, nodes, CORROBORATION_PARAM_DEFAULTS)
        self.assertEqual(verdict["status"], "verified")  # downgrade, NOT veto
        deltas = [a for a in verdict["annotations"]
                  if a["kind"] == "confidence_delta"]
        self.assertEqual(len(deltas), 1)
        self.assertEqual(deltas[0]["confidenceDelta"], STATIC_LABEL_DELTA)
        self.assertEqual(candidates, before)

    def test_clickable_focusable_corroborates(self):
        candidates = [_row("r1", "Wallpaper", (0, 0, 200, 40))]
        nodes = [_structured("n1", "Wallpaper", (0, 0, 200, 40),
                             clickable=True, focusable=True)]
        verdict = corroborate(candidates, nodes, CORROBORATION_PARAM_DEFAULTS)
        self.assertEqual(verdict["status"], "verified")
        corroborated = [a for a in verdict["annotations"]
                        if a["kind"] == "corroborated"]
        self.assertEqual(len(corroborated), 1)
        self.assertEqual(corroborated[0]["confidenceDelta"], 0.0)

    def test_strong_contradiction_veto(self):
        # Region fully represented by TEXT-BEARING nodes, head text nowhere.
        candidates = [_row("r1", "Mystery Row", (0, 0, 200, 40))]
        nodes = [
            _structured("n1", "Something else", (0, 0, 200, 40)),
            _structured("n2", "Another label", (0, 50, 200, 90)),
        ]
        verdict = corroborate(candidates, nodes, CORROBORATION_PARAM_DEFAULTS)
        self.assertEqual(verdict["status"], "rejected")
        self.assertIn("fail-closed", verdict["detail"])

    def test_textless_region_in_doubt_downgrades_only(self):
        candidates = [_row("r1", "Mystery Row", (0, 0, 200, 40))]
        nodes = [_structured("n1", "", (0, 0, 200, 40))]
        verdict = corroborate(candidates, nodes, CORROBORATION_PARAM_DEFAULTS)
        self.assertEqual(verdict["status"], "verified")  # in doubt ⇒ downgrade
        deltas = [a for a in verdict["annotations"]
                  if a["kind"] == "confidence_delta"]
        self.assertEqual(len(deltas), 1)
        self.assertEqual(deltas[0]["confidenceDelta"], IN_DOUBT_DELTA)

    def test_no_overlap_region_not_authoritative(self):
        candidates = [_row("r1", "Top Row", (0, 0, 200, 40))]
        nodes = [_structured("n1", "Top Row", (0, 900, 200, 940))]
        verdict = corroborate(candidates, nodes, CORROBORATION_PARAM_DEFAULTS)
        self.assertEqual(verdict["status"], "verified")
        self.assertEqual(verdict["annotations"], [])

    def test_xml_never_creates_identity(self):
        # Rows exist only in the structured tier: the operator emits nothing,
        # and the candidate list (here empty) is never extended.
        nodes = [_structured("n1", "Fabricated Row", (0, 0, 200, 40),
                             clickable=True, focusable=True)]
        candidates: list[dict] = []
        before = copy.deepcopy(candidates)
        verdict = corroborate(candidates, nodes, CORROBORATION_PARAM_DEFAULTS)
        self.assertEqual(verdict["status"], "verified")
        self.assertEqual(candidates, before)


class VlmAnnotationTests(unittest.TestCase):
    """``vlm-annotation`` offline-only interface."""

    def test_offline_signature_deterministic_noop(self):
        frames = [{"frameId": "f1", "yolo": [], "ocr": []}]
        params = {"uniform-list-row-grouping.minAnchors": 4}
        suggestions = propose_parameter_adjustments(frames, params)
        self.assertEqual(suggestions, [])
        # Deterministic: same inputs ⇒ identical result every call.
        self.assertEqual(
            propose_parameter_adjustments(frames, params), suggestions
        )
        self.assertEqual(
            json.dumps(propose_parameter_adjustments(frames, params),
                       sort_keys=True),
            json.dumps(suggestions, sort_keys=True),
        )

    def test_offline_interface_is_pure(self):
        frames = [{"frameId": "f1"}]
        params = {"spacing-verifier.minStepRatio": 0.15}
        frames_before = copy.deepcopy(frames)
        params_before = copy.deepcopy(params)
        propose_parameter_adjustments(frames, params)
        self.assertEqual(frames, frames_before)
        self.assertEqual(params, params_before)

    def test_no_online_call_path(self):
        # Registered for contract completeness, but neither declared nor
        # runnable: there is no way for the executed pipeline to invoke it.
        self.assertNotIn("vlm-annotation", REGISTRY.operators_for_pipeline())
        self.assertNotIn("vlm-annotation", RUNNERS)


class CorpusZeroVetoTests(unittest.TestCase):
    """All 34 corpus cases pass both new VALIDATORs with zero vetoes."""

    def test_all_corpus_cases_pass_both_validators(self):
        cases = _all_corpus_cases()
        self.assertEqual(len(cases), 34)
        for case in cases:
            with self.subTest(case=case["case_id"]):
                candidates, trace = replay(case)
                text_verdict = run_text_relation_check(
                    candidates, [], TEXT_RELATION_PARAM_DEFAULTS
                )
                self.assertEqual(
                    text_verdict["status"], "verified",
                    f"{case['case_id']}: text-relation-check must not veto "
                    "current corpus output (zero new rejection surface)",
                )
                struct_verdict = run_structured_corroboration(
                    candidates, [], CORROBORATION_PARAM_DEFAULTS
                )
                self.assertEqual(
                    struct_verdict["status"], "verified",
                    f"{case['case_id']}: structured-corroboration must pass "
                    "trivially with no structured evidence",
                )
                steps = {step["operator"]: step for step in trace.steps}
                self.assertEqual(
                    steps["text-relation-check"]["status"], "verified",
                    case["case_id"],
                )
                self.assertEqual(
                    steps["structured-corroboration"]["status"], "verified",
                    case["case_id"],
                )
                self.assertNotIn(
                    "fail_closed", [step["status"] for step in trace.steps],
                    f"{case['case_id']}: unexpected fail-closed step",
                )

    def test_validators_never_alter_corpus_candidates(self):
        for case in _all_corpus_cases():
            with self.subTest(case=case["case_id"]):
                candidates, _ = replay(case)
                before = copy.deepcopy(candidates)
                run_text_relation_check(candidates, [], TEXT_RELATION_PARAM_DEFAULTS)
                run_structured_corroboration(
                    candidates, [], CORROBORATION_PARAM_DEFAULTS
                )
                self.assertEqual(
                    candidates, before,
                    f"{case['case_id']}: S4 validators must be annotate-only "
                    "(candidates byte-unchanged)",
                )


if __name__ == "__main__":
    unittest.main()