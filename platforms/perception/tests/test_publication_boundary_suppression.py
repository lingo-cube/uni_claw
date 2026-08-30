"""FUSION_PUBLICATION_BOUNDARY_REPAIR_GATE — RED→GREEN falsifier and
counterexample preservation for the row-relation-head satellite publication
boundary.

Exact phantom falsifier (real r2 child-frame geometry, det_12/det_15
'Brightness level' row):

    RED   — the band AND the internal satellite both enter the top-level
            publication (``fuse_evidence`` → ``result["candidates"]``).
    GREEN — the band is published; the internal satellite is excluded from
            the top-level world-occurrence projection while remaining
            observable in the composition stage (fusionStages) and in the
            engine diagnostics.

The predicate may only suppress on ALL of: row-relation-head marker +
resolvable headId + raw-evidence consumed by the owning band + no independent
interaction evidence. None of text=="" / NonInteractive type / overlap /
containment / no-clickable / same-text alone decides suppression.
"""
from __future__ import annotations

import copy
import unittest

from uniclaw_perception.fusion.engine import fuse_evidence
from uniclaw_perception.fusion.publication import (
    partition_internal_satellites,
)
from uniclaw_perception.schema import Box, Detection, OcrToken


def _candidate(id_: str, type_: str, text: str, y1: float, y2: float,
               *, role: str | None = None, marker: bool = False,
               head_id: str | None = None, all_ids: tuple[str, ...] = (),
               type_inferred: str = "row_relation_head") -> dict:
    ev: dict = {
        "allIds": list(all_ids),
        "typeInferred": type_inferred,
    }
    if marker:
        ev["typeInferred"] = "row_relation_head_satellite"
        ev["headId"] = head_id
    candidate: dict = {
        "id": id_,
        "type": type_,
        "text": text,
        "confidence": 0.9,
        "bounds": {"x1": 66 / 1080, "y1": y1 / 2400, "x2": 432 / 1080, "y2": y2 / 2400},
        "boundsPx": [66, int(y1), 432, int(y2)],
        "centerPx": [249, int((y1 + y2) / 2)],
        "evidence": ev,
        "riskFlags": [],
    }
    if role is not None:
        candidate["role"] = role
    return candidate


def _band(band_id: str = "relation_head_band_1",
          all_ids: tuple[str, ...] = ("det_15", "ocr_3")) -> dict:
    return _candidate(band_id, "menu_item", "Brightness level", 800, 854,
                      all_ids=all_ids, type_inferred="row_relation_head")


def _sat(sat_id: str = "relation_head_band_1_sat_0",
         head: str = "relation_head_band_1",
         all_ids: tuple[str, ...] = ("det_12",),
         role: str = "control") -> dict:
    return _candidate(sat_id, "NonInteractive", "", 796, 896, role=role,
                      marker=True, head_id=head, all_ids=all_ids)


class PublicationBoundarySuppressionTests(unittest.TestCase):
    """Engine-level RED→GREEN + property + determinism."""

    def _falsifier_frame(self) -> tuple[list[Detection], list[OcrToken]]:
        # Real r2 child-frame 'Brightness level' row geometry: det_15 is the
        # elected band head, det_12 is the same-row text detection consumed by
        # the band and re-published as the satellite.
        detections = [
            Detection("det_15", "text_block", 0.68, Box(66, 800, 435, 854)),
            Detection("det_12", "text_block", 0.80, Box(66, 796, 432, 896)),
        ]
        ocr = [OcrToken("ocr_3", "Brightness level", 0.95, Box(66, 800, 432, 855))]
        return detections, ocr

    def test_exact_phantom_falsifier_satellite_not_published_top_level(self):
        # GATE §RED→GREEN test 1: band.allIds ⊇ det_12; satellite.headId →
        # band; satellite raw source = det_12; no independent interaction
        # evidence → the satellite must NOT reach the top-level publication.
        stages: list[dict] = []
        detections, ocr = self._falsifier_frame()
        result = fuse_evidence(
            detections, ocr,
            image_width=1080, image_height=2400,
            stage_sink=stages.append,
        )
        ids = [c["id"] for c in result["candidates"]]
        bands = [i for i in ids if i.startswith("relation_head_band_") and "_sat_" not in i]
        self.assertTrue(bands, f"relation-head band must be published, got {ids}")
        self.assertFalse(
            [i for i in ids if "_sat_" in i],
            f"internal satellites leaked to top-level publication: {ids}",
        )
        # Observability preserved: the satellite remains visible in the
        # composition stage candidate view (fusionStages) ...
        composed = [
            c for st in stages
            for c in (st.get("candidates") or [])
            if st.get("stage") == "composition-output"
        ]
        self.assertTrue(
            any("_sat_" in (c.get("id") or "") for c in composed),
            "the internal satellite must remain observable in the composition stage",
        )
        # ... and the engine reports the suppression diagnostically.
        diag = result.get("_diagnostics", {})
        self.assertTrue(
            diag.get("internalSatellitesSuppressed"),
            "suppression must be observable in _diagnostics",
        )

    def test_standalone_text_block_without_head_id_still_published(self):
        # GATE test 2: a genuine independent text_block (no headId) must keep
        # publishing.
        detections = [Detection("det_1", "text_block", 0.9, Box(127, 138, 427, 195))]
        ocr = [OcrToken("ocr_1", "Network & internet", 0.95, Box(130, 141, 430, 192))]
        result = fuse_evidence(detections, ocr, image_width=720, image_height=1400)
        self.assertTrue(
            any(c.get("text") == "Network & internet" for c in result["candidates"]),
            "standalone text_block must remain published",
        )

    def test_determinism_same_input_same_publication(self):
        # GATE test 9.
        detections, ocr = self._falsifier_frame()
        r1 = fuse_evidence(copy.deepcopy(detections), copy.deepcopy(ocr),
                           image_width=1080, image_height=2400)
        r2 = fuse_evidence(copy.deepcopy(detections), copy.deepcopy(ocr),
                           image_width=1080, image_height=2400)
        self.assertEqual([c["id"] for c in r1["candidates"]],
                         [c["id"] for c in r2["candidates"]])
        self.assertEqual(r1["candidates"], r2["candidates"])

    def test_property_publish_band_plus_satellite_equals_publish_band(self):
        # GATE property test: for an internal satellite S,
        #   Publish(Band + S) == Publish(Band)
        # at the TOP-LEVEL world-occurrence projection only; S stays
        # observable in trace/composition evidence.
        stages: list[dict] = []
        detections, ocr = self._falsifier_frame()
        result = fuse_evidence(
            detections, ocr,
            image_width=1080, image_height=2400,
            stage_sink=stages.append,
        )
        # The top-level published world projection must be exactly the
        # final-stage composition's non-internal projection — S is internal,
        # so the published projection equals the projection without S.
        final_stage = [
            c for st in stages
            for c in (st.get("candidates") or [])
            if st.get("stage") == "row-stabilization"
        ]
        published, internal = partition_internal_satellites(copy.deepcopy(final_stage))
        self.assertTrue(internal, "the falsifier frame must expose an internal satellite")
        self.assertEqual(
            {c["id"] for c in result["candidates"]},
            {c["id"] for c in published},
            "Publish(Band + S) must equal Publish(Band) at the top-level projection",
        )


class SuppressionPredicateCounterexampleTests(unittest.TestCase):
    """GATE counterexamples 3–6 + positive control for the predicate."""

    def test_positive_control_all_conditions_hold_suppresses(self):
        # The owning band must have CONSUMED the satellite's raw source
        # (det_12 ∈ band.allIds) for the predicate to hold.
        band = _band(all_ids=("det_15", "ocr_3", "det_12"))
        sat = _sat()
        published, internal = partition_internal_satellites([band, sat])
        self.assertEqual([c["id"] for c in published], [band["id"]])
        self.assertEqual([c["id"] for c in internal], [sat["id"]])

    def test_broken_parent_reference_stays_published(self):
        # GATE test 3: marker present but headId does not resolve → fail
        # closed, keep publishing.
        band = _band()
        sat = _sat(head="relation_head_band_missing")
        published, internal = partition_internal_satellites([band, sat])
        self.assertEqual(len(internal), 0)
        self.assertIn(sat["id"], [c["id"] for c in published])

    def test_broken_parent_type_stays_published(self):
        # headId resolves, but the target is NOT an emitted relation-head band
        # (typeInferred wrong) → not suppressed.
        fake_head = _candidate("relation_head_band_1", "text_block", "Brightness level",
                               800, 854, type_inferred="text_block")
        sat = _sat(head="relation_head_band_1")
        published, internal = partition_internal_satellites([fake_head, sat])
        self.assertEqual(len(internal), 0)

    def test_raw_source_not_consumed_stays_published(self):
        # GATE test 4: satellite raw id not in the owning band's allIds →
        # must NOT suppress.
        band = _band(all_ids=("det_15", "ocr_3"))
        sat = _sat(all_ids=("det_99",))
        published, internal = partition_internal_satellites([band, sat])
        self.assertEqual(len(internal), 0)
        self.assertIn(sat["id"], [c["id"] for c in published])

    def test_independent_interactive_child_stays_published(self):
        # GATE test 5: a satellite carrying independent interaction evidence
        # (switch/checkbox/toggle/slider-shaped raw source → role "toggle")
        # must NOT be suppressed.
        band = _band()
        sat = _sat(role="toggle", all_ids=("det_switch",))
        band_with_switch = _band(all_ids=("det_15", "ocr_3", "det_switch"))
        published, internal = partition_internal_satellites([band_with_switch, sat])
        self.assertEqual(len(internal), 0)
        self.assertIn(sat["id"], [c["id"] for c in published])

    def test_different_row_fragment_overlap_only_stays_published(self):
        # GATE test 6: bounds overlap / same text alone (no internal-satellite
        # marker, no parent+consumption evidence) → must NOT suppress.
        band = _band()
        overlapping_text = _candidate(
            "candidate_9", "text_block", "Brightness level", 800, 896,
            type_inferred="text_block",
        )
        published, internal = partition_internal_satellites([band, overlapping_text])
        self.assertEqual(len(internal), 0)
        self.assertIn(overlapping_text["id"], [c["id"] for c in published])

    def test_partition_is_deterministic(self):
        items = [_band(), _sat(), _sat("relation_head_band_1_sat_1",
                                        all_ids=("ocr_4",), role="caption")]
        p1, i1 = partition_internal_satellites(copy.deepcopy(items))
        p2, i2 = partition_internal_satellites(copy.deepcopy(items))
        self.assertEqual([c["id"] for c in p1], [c["id"] for c in p2])
        self.assertEqual([c["id"] for c in i1], [c["id"] for c in i2])


if __name__ == "__main__":
    unittest.main()