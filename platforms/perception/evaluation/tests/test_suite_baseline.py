"""Suite versioning + baseline immutability falsifiers: PF2, B16.

BaselineTests now build the full persisted canonical chain; the report
is always DERIVED (GAP-004 FINAL) — scope, counts and quality come from
persisted records, never from caller arguments.
"""
from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from evaluation import EVALUATION_SCHEMA_VERSION
from evaluation.asset import CorpusRole, PerceptionTask
from evaluation.baseline import (
    BaselineImmutabilityError, BaselineReport, persist_baseline,
)
from evaluation.suite import EvaluationSuite, SuiteMembership, save_suite
from evaluation.tests.chain_helpers import (
    Chain, asset_id, persist_scored_member,
)


def _suite(asset_ids: list[str]) -> EvaluationSuite:
    return EvaluationSuite(
        suite_schema_version=EVALUATION_SCHEMA_VERSION,
        backend="L2_RECORDED_IMAGE_INFERENCE",
        evaluator_revision="evaluator-v1",
        required_tasks=(PerceptionTask.ELEMENT_DETECTION,),
        members=tuple(SuiteMembership(asset_id=a,
                                      roles=(CorpusRole.CALIBRATION,))
                      for a in asset_ids),
        description="test suite",
    )


class SuiteTests(unittest.TestCase):
    def test_PF2_new_membership_creates_new_suite_version_not_mutation(self):
        v1 = _suite(["sha256:a"])
        v2 = v1.with_members(
            tuple(SuiteMembership(asset_id=a,
                                  roles=(CorpusRole.CALIBRATION,))
                  for a in ["sha256:a", "sha256:b"]))
        self.assertNotEqual(v1.suite_id, v2.suite_id)
        # v1 remains intact — no mutation
        self.assertEqual(len(v1.members), 1)
        self.assertEqual(v1.suite_id, _suite(["sha256:a"]).suite_id)

    def test_suite_persist_then_load_roundtrip(self):
        with tempfile.TemporaryDirectory() as tmp:
            v1 = _suite(["sha256:a"])
            p = save_suite(v1, Path(tmp))
            from evaluation.suite import load_suite
            loaded = load_suite(p)
            self.assertEqual(loaded.suite_id, v1.suite_id)
            self.assertEqual(len(loaded.members), 1)
            self.assertEqual(loaded.members[0].asset_id, "sha256:a")


class BaselineTests(unittest.TestCase):
    """GAP-004 FINAL: baseline creation is a derivation over a persisted
    canonical chain (request → suite → terminal result → member records)."""

    def _report(self, *, members=(asset_id(1),), gt_version: str = "1",
                scope: tuple[str, ...] = ()) -> BaselineReport:
        chain = Chain()
        self.addCleanup(chain.close)
        suite = chain.build_suite(members)
        request = chain.build_request(suite, asset_scope=scope)
        outcomes = []
        for m in members:
            persist_scored_member(chain, m, request, gt_version)
            outcomes.append(chain.scorable(m, gt_version))
        chain.build_result(request, tuple(outcomes))
        return BaselineReport.create(
            request_id=request.request_id,
            run_dir=chain.run_dir, suite_dir=chain.suite_dir,
            prediction_dir=chain.prediction_dir,
            ground_truth_dir=chain.gt_dir,
            asset_manifest_dir=chain.manifest_dir)

    def test_B16_baseline_immutable_after_creation(self):
        with tempfile.TemporaryDirectory() as tmp:
            r = self._report()
            p1 = persist_baseline(r, Path(tmp))
            self.assertTrue(p1.exists())
            content = p1.read_text(encoding="utf-8")
            # identical re-persist is a no-op (same content)
            p2 = persist_baseline(r, Path(tmp))
            self.assertEqual(p1, p2)
            self.assertEqual(p1.read_text(encoding="utf-8"), content)
            # Direct filesystem tampering is never repairable by a write:
            # same identity with different bytes must be refused.
            with self.assertRaises(BaselineImmutabilityError):
                target = Path(tmp) / f"{r.baseline_id.replace('baseline:', '')}.json"
                target.write_text(json.dumps({"tampered": True}))
                persist_baseline(r, Path(tmp))

    def test_IMM_05_baseline_overwrite_escape_hatch_is_not_accepted(self):
        with tempfile.TemporaryDirectory() as tmp:
            report = self._report()
            persist_baseline(report, Path(tmp))
            with self.assertRaises(TypeError):
                persist_baseline(report, Path(tmp), overwrite=True)

    def test_new_inputs_new_baseline_id(self):
        r1 = self._report(members=(asset_id(1),))
        r2 = self._report(members=(asset_id(2),))
        self.assertNotEqual(r1.baseline_id, r2.baseline_id)
        r3 = self._report(members=(asset_id(1),), gt_version="2")
        self.assertNotEqual(r1.baseline_id, r3.baseline_id)

    def test_baseline_fields_truthful_defaults(self):
        r = self._report()
        self.assertEqual(r.holdout_status, "NONE")
        self.assertEqual(r.numeric_thresholds, "NOT_FROZEN")

    def test_canonical_scope_authority_from_persisted_records(self):
        """Population is the request's asset_scope (or suite membership) —
        never a caller-selected count.  The report reflects exactly the
        canonical requested population, 1 or 2 members."""
        r1 = self._report(members=(asset_id(1), asset_id(2)))
        self.assertEqual(r1.asset_count, 2)
        self.assertEqual(r1.scored_count, 2)
        # request scope subset → population is the scope
        chain = Chain()
        self.addCleanup(chain.close)
        suite = chain.build_suite((asset_id(1), asset_id(2)))
        request = chain.build_request(suite,
                                      asset_scope=(asset_id(1),))
        persist_scored_member(chain, asset_id(1), request)
        chain.build_result(request, (chain.scorable(asset_id(1)),))
        r2 = BaselineReport.create(
            request_id=request.request_id,
            run_dir=chain.run_dir, suite_dir=chain.suite_dir,
            prediction_dir=chain.prediction_dir,
            ground_truth_dir=chain.gt_dir,
            asset_manifest_dir=chain.manifest_dir)
        self.assertEqual(r2.asset_count, 1)
        self.assertEqual(r2.scored_count, 1)


if __name__ == "__main__":
    unittest.main()
