"""S2.1 ``row-relation-head`` GENERATOR unit tests (WI-PFW-S2i, OpenSpec change
``perception-operator-rule-framework``).

Pins the S2.1 operator contract locally, mirroring the gates of
``evidence/S2-acceptance-protocol.md`` that are reachable without engine
routing (G-1 v1n guard, G-4 input freeze, G-7 determinism/trace):

* basic low-anchor composition: 3 rows → 3 navigation candidates, captions /
  icons / toggles absorbed as NonInteractive satellites;
* v1n guard: a ``'Volume, vibration, Do Not Disturb'`` subtitle NEVER becomes a
  menu_item (in-band absorption AND the geometric subtitle-continuation guard);
* same-text different-position rows stay distinct (no merge);
* ambiguity fail-closed: equal-width, same-line, distinct-text heads → no
  candidate + recorded reason;
* OCR-only bands (no detector anchor) fail closed;
* determinism: identical decision records and trace bytes across replays;
* input freeze: the run entry takes only raw arrays — no composed-candidate
  parameter exists;
* registration: the contract is in the registry, the default rule set still
  lints with 0 diagnostics, and — since S2ii wired engine routing — the
  declared topology is the 3-operator pipeline with the frozen-input runner
  adapter in ``RUNNERS`` (see ``s2-delta-report.md``).

All inputs are synthetic raw visual regions (uncombined detector boxes + OCR
text blocks in pixel ``boundsPx``), deterministic, no network.
"""
from __future__ import annotations

import unittest

from uniclaw_perception.operators import (
    DEFAULT_RULE_SET,
    REGISTRY,
    OperatorAuthority,
    lint_rule_set,
)
from uniclaw_perception.operators.row_relation_head import (
    ROW_RELATION_HEAD_PARAM_BOUNDS,
    ROW_RELATION_HEAD_PARAM_DEFAULTS,
    record_trace_bytes,
    run,
)

_WIDTH, _HEIGHT = 720, 900


def _det(
    identifier: str,
    label: str,
    x1: float, y1: float, x2: float, y2: float,
    confidence: float = 0.9,
) -> dict:
    """Raw detector box (to_json-shaped: pixel boundsPx)."""
    return {
        "id": identifier,
        "label": label,
        "confidence": confidence,
        "boundsPx": [x1, y1, x2, y2],
    }


def _ocr(
    identifier: str,
    text: str,
    x1: float, y1: float, x2: float, y2: float,
    confidence: float = 0.99,
) -> dict:
    """Raw OCR text block (to_json-shaped: pixel boundsPx)."""
    return {
        "id": identifier,
        "text": text,
        "confidence": confidence,
        "boundsPx": [x1, y1, x2, y2],
    }


def _candidate_texts(record: dict) -> list[str]:
    return [candidate["text"] for candidate in record["candidates"]]


def _satellite_texts(record: dict) -> list[str]:
    return [satellite["text"] for satellite in record["satellites"]]


class BasicCompositionTests(unittest.TestCase):
    """G-1-friendly low-anchor composition on a clean synthetic viewport."""

    def test_three_row_low_anchor_composition(self):
        # Three title rows; row 1 also carries a caption line, a left icon and
        # a right toggle (all absorbed as NonInteractive satellites).  Row 1's
        # title is deliberately NOT a detector-proven list_item row only — the
        # composition must come from the raw regions alone.
        detections = [
            _det("r1_title", "text_block", 120, 100, 900, 130, 0.9),
            _det("r1_icon", "icon", 150, 108, 185, 126, 0.88),
            _det("r1_toggle", "toggle", 600, 108, 660, 126, 0.95),
            _det("r2_title", "text_block", 120, 220, 700, 250, 0.9),
            _det("r3_title", "text_block", 120, 340, 700, 370, 0.9),
        ]
        ocr_tokens = [
            _ocr("r1_ocr", "Volume", 122, 102, 478, 128),
            _ocr("r1_caption", "Adjust media volume", 122, 132, 478, 156),
            _ocr("r2_ocr", "Bluetooth", 122, 222, 478, 248),
            _ocr("r3_ocr", "Wi-Fi", 122, 342, 478, 368),
        ]
        record = run(detections, ocr_tokens, _WIDTH, _HEIGHT)

        self.assertEqual(record["status"], "activated")
        self.assertEqual(record["emitted"], 3)
        self.assertEqual(
            _candidate_texts(record), ["Volume", "Bluetooth", "Wi-Fi"],
            "one navigation candidate per band, in top-to-bottom band order",
        )
        # Captions/icons/toggles never become candidates.
        self.assertNotIn("Adjust media volume", _candidate_texts(record))

        # Satellites: icon + toggle + caption absorbed with provenance.
        roles = {satellite["role"] for satellite in record["satellites"]}
        self.assertEqual(roles, {"icon", "toggle", "caption"})
        caption = next(
            satellite for satellite in record["satellites"]
            if satellite["role"] == "caption"
        )
        self.assertEqual(caption["type"], "NonInteractive")
        self.assertEqual(caption["text"], "Adjust media volume")
        self.assertIn("r1_caption", caption["evidence"]["allIds"])
        self.assertEqual(
            caption["evidence"]["typeInferred"], "row_relation_head_satellite"
        )
        # Head provenance: the row_relation_head reason + its OCR evidence.
        head = record["candidates"][0]
        self.assertEqual(head["evidence"]["typeInferred"], "row_relation_head")
        self.assertIn("r1_ocr", head["evidence"]["ocrIds"])
        self.assertEqual(head["type"], "menu_item")

    def test_satellite_cap_bounded(self):
        # A band with more members than the bounded cap keeps only the first N
        # (deterministic order) and still emits exactly one candidate.
        detections = [
            _det("t", "text_block", 120, 100, 900, 130),
            *[
                _det(f"s{i}", "icon", 150 + i * 40, 108, 185 + i * 40, 126)
                for i in range(10)
            ],
        ]
        ocr_tokens = [_ocr("to", "Volume", 122, 102, 478, 128)]
        record = run(detections, ocr_tokens, _WIDTH, _HEIGHT, {"max_satellites_per_row": 3})
        self.assertEqual(record["emitted"], 1)
        self.assertLessEqual(len(record["satellites"]), 3)


class V1nGuardTests(unittest.TestCase):
    """G-1: the v1n subtitle must never become a navigation candidate.

    The synthetic frame mirrors the real v1n corpus geometry
    (``v1n_low_anchor_viewport_subtitle_fail_closed``): the
    ``'Sound & vibration'`` title row carries an equal-width ``text_block``
    detection and OCR line for the subtitle right below it, plus a left icon.
    """

    def _v1n_frame(self):
        detections = [
            _det("sound_title", "text_block", 120, 234, 480, 264, 0.9),
            _det("sound_icon", "icon", 55, 243, 90, 279, 0.88),
            _det("sound_subtitle", "text_block", 120, 266, 480, 294, 0.82),
            _det("bt_title", "text_block", 120, 420, 480, 450, 0.9),
            _det("wf_title", "text_block", 120, 560, 480, 590, 0.9),
        ]
        ocr_tokens = [
            _ocr("sound_ocr", "Sound & vibration", 122, 236, 478, 262),
            _ocr("subtitle_ocr", "Volume, vibration, Do Not Disturb", 122, 268, 478, 292),
            _ocr("bt_ocr", "Bluetooth", 122, 422, 478, 448),
            _ocr("wf_ocr", "Wi-Fi", 122, 562, 478, 588),
        ]
        return detections, ocr_tokens

    def test_v1n_subtitle_never_menu_item(self):
        detections, ocr_tokens = self._v1n_frame()
        record = run(detections, ocr_tokens, _WIDTH, _HEIGHT)

        self.assertEqual(record["status"], "activated")
        texts = _candidate_texts(record)
        self.assertNotIn(
            "Volume, vibration, Do Not Disturb", texts,
            "v1n: the subtitle must NEVER be a menu_item",
        )
        self.assertNotIn(
            "Volume,", texts, "no caption fragment may leak into a candidate"
        )
        # The three real title rows compose (low-anchor viewport, 3 anchors).
        self.assertEqual(
            texts, ["Sound & vibration", "Bluetooth", "Wi-Fi"],
        )
        # The subtitle is absorbed as a NonInteractive caption satellite.
        self.assertIn("Volume, vibration, Do Not Disturb", _satellite_texts(record))
        subtitle = next(
            satellite for satellite in record["satellites"]
            if satellite["text"] == "Volume, vibration, Do Not Disturb"
        )
        self.assertEqual(subtitle["type"], "NonInteractive")

    def test_wrapped_caption_line_never_becomes_candidate(self):
        # A multi-line caption under a title is absorbed in-band (adjacent to
        # its title) and can never be elected a head: the geometry makes any
        # immediate continuation line part of the same vertical band, where it
        # is recorded as a caption satellite, not a navigation candidate.
        detections = [
            _det("row1_title", "text_block", 120, 100, 700, 130),
            _det("row1_caption", "text_block", 120, 134, 460, 158),
            _det("row1_wrap", "text_block", 120, 162, 460, 186),
            _det("row2_title", "text_block", 120, 400, 700, 430),
        ]
        ocr_tokens = [
            _ocr("row1_ocr", "Bluetooth", 122, 102, 478, 128),
            _ocr("row1_cap_ocr", "Bluetooth device name", 122, 136, 458, 156),
            _ocr("row1_wrap_ocr", "More Bluetooth details here", 122, 164, 458, 184),
            _ocr("row2_ocr", "Wi-Fi", 122, 402, 478, 428),
        ]
        record = run(detections, ocr_tokens, _WIDTH, _HEIGHT)

        # One candidate per real row; the caption AND its wrapped continuation
        # line are NonInteractive satellites, never candidates.
        self.assertEqual(
            _candidate_texts(record), ["Bluetooth", "Wi-Fi"],
        )
        self.assertNotIn(
            "More Bluetooth details here", _candidate_texts(record),
        )
        self.assertIn("More Bluetooth details here", _satellite_texts(record))
        wrap = next(
            satellite for satellite in record["satellites"]
            if satellite["text"] == "More Bluetooth details here"
        )
        self.assertEqual(wrap["type"], "NonInteractive")

    def test_subtitle_continuation_guard_predicate(self):
        # The geometric subtitle guard (line-level unit test of the reachable
        # predicate): a candidate head that reproduces the previous band's
        # caption offset at the same column is a caption continuation and is
        # rejected fail-closed.  (End-to-end, such a line is adjacent and is
        # absorbed in-band — see test_wrapped_caption_line_never_becomes_candidate —
        # so this test pins the guard predicate itself, which the spec names.)
        from uniclaw_perception.operators.row_relation_head import (
            _Box,
            _ElectedBand,
            _RelationHeadParams,
            _is_subtitle_continuation,
        )

        def box(identifier, y1, y2, x1=120, x2=480, label="text_block"):
            return _Box(
                {"id": identifier, "label": label, "confidence": 0.9,
                 "boundsPx": [x1, y1, x2, y2]},
                is_ocr=False,
            )

        # Previous band: title head at y1=100 with an in-band caption at
        # offset 34 (caption y1=134).  Candidate band B: a head at y1=134,
        # same column — the caption offset reproduced → continuation → reject.
        previous = _ElectedBand(
            band_index=0,
            head=box("t1", 100, 130),
            head_text="Bluetooth",
            caption_boxes=(
                _Box({"id": "c1", "text": "Bluetooth device name",
                      "confidence": 0.99, "boundsPx": [122, 134, 460, 158]},
                     is_ocr=True),
            ),
        )
        continuation = _ElectedBand(
            band_index=1,
            head=box("c2", 134, 162),
            head_text="More Bluetooth details here",
        )
        params = _RelationHeadParams(column_tolerance=0.05)
        self.assertTrue(
            _is_subtitle_continuation(continuation, previous, params, float(_WIDTH)),
            "a head at the previous band's caption offset and column is a "
            "caption continuation → reject the band",
        )
        # Negative: different column → not a continuation.
        off_column = _ElectedBand(band_index=1, head=box("x", 134, 162, x1=600, x2=900))
        self.assertFalse(
            _is_subtitle_continuation(off_column, previous, params, float(_WIDTH)),
        )
        # Negative: different vertical offset → a genuine next row.
        next_row = _ElectedBand(band_index=1, head=box("r2", 400, 430))
        self.assertFalse(
            _is_subtitle_continuation(next_row, previous, params, float(_WIDTH)),
        )


class DistinctRowsTests(unittest.TestCase):
    def test_same_text_different_position_rows_do_not_merge(self):
        # Two 'Network' rows in different vertical bands (well beyond the
        # adjacency gap) must stay two distinct candidates, never merged.
        detections = [
            _det("n1", "text_block", 120, 100, 480, 130),
            _det("n2", "text_block", 120, 420, 480, 450),
        ]
        ocr_tokens = [
            _ocr("n1o", "Network", 122, 102, 478, 128),
            _ocr("n2o", "Network", 122, 422, 478, 448),
        ]
        record = run(detections, ocr_tokens, _WIDTH, _HEIGHT)

        self.assertEqual(record["emitted"], 2)
        network = [
            candidate for candidate in record["candidates"]
            if candidate["text"] == "Network"
        ]
        self.assertEqual(len(network), 2)
        self.assertNotEqual(
            network[0]["boundsPx"], network[1]["boundsPx"],
            "same-text rows at different positions are separate candidates",
        )
        self.assertEqual(
            {candidate["id"] for candidate in network},
            {"relation_head_band_0", "relation_head_band_1"},
        )


class FailClosedTests(unittest.TestCase):
    def test_equal_width_same_line_ambiguity_rejects_band(self):
        # Two equal-width text-bearing detections at the same column and line
        # with DISTINCT texts: a genuine ambiguity — no candidate, reason kept.
        detections = [
            _det("a", "text_block", 120, 100, 480, 130),
            _det("b", "text_block", 120, 102, 480, 132),
        ]
        ocr_tokens = [
            _ocr("ao", "Network", 122, 102, 478, 128),
            _ocr("bo", "Advanced", 122, 104, 478, 130),
        ]
        record = run(detections, ocr_tokens, _WIDTH, _HEIGHT)

        self.assertEqual(record["status"], "noop")
        self.assertEqual(record["emitted"], 0)
        self.assertEqual(record["bands"][0]["status"], "rejected")
        self.assertIn("fail-closed", record["bands"][0]["reason"])
        self.assertIn("tie", record["bands"][0]["reason"])

    def test_ocr_only_band_fail_closed(self):
        # A band with only OCR text (no detector box at its column — e.g. a
        # stray subtitle line with no detector anchor) never produces a head.
        detections = []
        ocr_tokens = [
            _ocr("stray", "Volume, vibration, Do Not Disturb", 122, 262, 478, 286),
        ]
        record = run(detections, ocr_tokens, _WIDTH, _HEIGHT)

        self.assertEqual(record["status"], "noop")
        self.assertEqual(record["emitted"], 0)
        self.assertEqual(record["bands"][0]["status"], "rejected")
        self.assertIn("fail-closed", record["bands"][0]["reason"])
        self.assertEqual(record["candidates"], [])

    def test_no_visual_regions_noop(self):
        record = run([], [], _WIDTH, _HEIGHT)
        self.assertEqual(record["status"], "noop")
        self.assertEqual(record["emitted"], 0)
        self.assertIn("fail-closed", record["detail"])

    def test_invalid_geometry_rejected(self):
        with self.assertRaises(ValueError):
            run([], [], 0, 100)
        with self.assertRaises(ValueError):
            run(
                [_det("bad", "text_block", 900, 100, 120, 130)],
                [], _WIDTH, _HEIGHT,
            )


class DeterminismTests(unittest.TestCase):
    """G-7: same inputs + same params ⇒ identical outputs and trace bytes."""

    def test_identical_outputs_and_trace_bytes(self):
        detections = [
            _det("r1_title", "text_block", 120, 100, 900, 130),
            _det("r1_toggle", "toggle", 600, 108, 660, 126),
            _det("r2_title", "text_block", 120, 220, 700, 250),
            _det("r3_title", "text_block", 120, 340, 700, 370),
        ]
        ocr_tokens = [
            _ocr("r1_ocr", "Volume", 122, 102, 478, 128),
            _ocr("r1_caption", "Adjust media volume", 122, 132, 478, 156),
            _ocr("r2_ocr", "Bluetooth", 122, 222, 478, 248),
            _ocr("r3_ocr", "Wi-Fi", 122, 342, 478, 368),
        ]
        first = run(detections, ocr_tokens, _WIDTH, _HEIGHT)
        second = run(detections, ocr_tokens, _WIDTH, _HEIGHT)
        self.assertEqual(first, second)
        self.assertEqual(
            record_trace_bytes(first), record_trace_bytes(second),
            "trace bytes must be byte-identical across replays",
        )


class InputFreezeTests(unittest.TestCase):
    """G-4: the run entry consumes ONLY raw visual regions + derived geometry.

    The signature is ``run(detections, ocr_tokens, width, height, params=None)``
    — there is no composed-candidate parameter at all.
    """

    def test_signature_accepts_raw_inputs_only(self):
        import inspect

        signature = inspect.signature(run)
        self.assertEqual(
            list(signature.parameters),
            ["detections", "ocr_tokens", "width", "height", "params"],
        )
        self.assertIsNone(signature.parameters["params"].default)
        # No parameter that would accept already-composed row groups.
        self.assertNotIn("candidates", signature.parameters)

    def test_parameters_optional_and_defaults_resolve(self):
        detections = [_det("t", "text_block", 120, 100, 480, 130)]
        ocr_tokens = [_ocr("to", "Volume", 122, 102, 478, 128)]
        without = run(detections, ocr_tokens, _WIDTH, _HEIGHT)
        with_defaults = run(
            detections, ocr_tokens, _WIDTH, _HEIGHT,
            dict(ROW_RELATION_HEAD_PARAM_DEFAULTS),
        )
        self.assertEqual(without, with_defaults)


class RegistrationTests(unittest.TestCase):
    """Contract registration + default rule set lint cleanliness.

    S2ii wired the operator into the executed pipeline (the engine consumes
    ``operators_for_pipeline()`` and the executor routes the frozen-input
    adapter in ``RUNNERS``) — the S2 routed topology is asserted here, citing
    the S2 delta report.
    """

    def test_contract_registered_generator_with_bounded_params(self):
        contract = REGISTRY.lookup("row-relation-head")
        self.assertIs(contract.authority, OperatorAuthority.GENERATOR)
        self.assertEqual(contract.version, "1.0.0")
        self.assertEqual(contract.output_kind, "row_group_proposal")
        self.assertIn("enabled", contract.parameters)  # built-in GENERATOR param
        for name, default in ROW_RELATION_HEAD_PARAM_DEFAULTS.items():
            spec = contract.parameters[name]
            self.assertEqual(spec.default, default)
            kind, (low, high) = ROW_RELATION_HEAD_PARAM_BOUNDS[name]
            self.assertEqual((spec.bounds.min_value, spec.bounds.max_value), (low, high))
        self.assertIn("detection", contract.input_kinds)
        self.assertIn("ocr", contract.input_kinds)

    def test_default_rule_set_lints_clean(self):
        # 0 diagnostics over the default root rule set (which now includes the
        # row-relation-head root rule pinning every parameter to its default).
        self.assertEqual(lint_rule_set(DEFAULT_RULE_SET, REGISTRY), [])
        resolved_default = next(
            rule for rule in DEFAULT_RULE_SET
            if rule.rule_id == "root-row-relation-head"
        )
        self.assertIn("row-relation-head.enabled", resolved_default.params)

    def test_s2_wired_topology_and_runner_adapter(self):
        # S2ii routing landed, then S4 appended the two annotate-only VALIDATORs
        # (Leader-sanctioned; see s2-delta-report.md "Trace shape change" and
        # the S4 acceptance record): the declared pipeline is the 5-operator
        # topology with row-relation-head between the S1 pair, registered with
        # GENERATOR authority, and the frozen-input runner adapter is wired in
        # RUNNERS (registry_defaults.py / relation_head_router.py).
        self.assertEqual(
            REGISTRY.operators_for_pipeline(),
            (
                "uniform-list-row-grouping",
                "row-relation-head",
                "spacing-verifier",
                "text-relation-check",
                "structured-corroboration",
            ),
        )
        contract = REGISTRY.lookup("row-relation-head")
        self.assertIs(contract.authority, OperatorAuthority.GENERATOR)
        from uniclaw_perception.operators.registry_defaults import RUNNERS
        from uniclaw_perception.operators.relation_head_router import (
            run_row_relation_head_routed,
        )
        self.assertIs(
            RUNNERS["row-relation-head"], run_row_relation_head_routed,
            "row-relation-head must be routed through its frozen-input runner "
            "adapter (raw visual regions, never composed candidates)",
        )
        self.assertTrue(getattr(RUNNERS["row-relation-head"], "handles_raw_sources", False))


if __name__ == "__main__":
    unittest.main()