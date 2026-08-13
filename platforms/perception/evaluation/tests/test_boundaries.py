"""Boundary falsifiers: B18, B20, PF6, PF7, PF8."""
from __future__ import annotations

import unittest

from evaluation import EVALUATION_SCHEMA_VERSION
from evaluation.asset import (
    AdmissionStance, ComponentClass, CorpusRole, Criticality, Difficulty,
    EvaluationAsset, PerceptionTask, Provenance, ScenarioDomain, SystemFamily,
)
from evaluation.scorecard import AssetScore, build_coverage, build_scorecard


class BoundaryTests(unittest.TestCase):
    def test_B18_training_role_not_conflated_with_evaluation_role(self):
        """CorpusRole has no training value; dataset membership is a
        separate future concern (DatasetVersion, not implemented)."""
        roles = [r.value for r in CorpusRole]
        self.assertNotIn("TRAINING", roles)
        self.assertNotIn("DATASET", roles)
        # and no module conflates them: no DatasetVersion type exists
        with self.assertRaises(ImportError):
            import evaluation.dataset  # noqa: F401 — must not exist

    def test_PF6_same_asset_id_quality_and_performance_roles(self):
        a = EvaluationAsset(
            asset_schema_version=EVALUATION_SCHEMA_VERSION,
            content_hash="sha256:x", source_path="/x.png",
            admission=AdmissionStance.ADMITTED,
            provenance=Provenance.RECORDED_REALITY,
            corpus_roles=(CorpusRole.CALIBRATION, CorpusRole.PERFORMANCE),
            system_family=SystemFamily.UNKNOWN,
            scenario_domain=ScenarioDomain.SETTINGS,
            perception_tasks=(PerceptionTask.ELEMENT_DETECTION,),
            component_class=ComponentClass.UNKNOWN,
            difficulty=Difficulty.UNKNOWN, criticality=Criticality.NORMAL,
        )
        self.assertIn(CorpusRole.PERFORMANCE, a.corpus_roles)
        self.assertIn(CorpusRole.CALIBRATION, a.corpus_roles)
        self.assertEqual(a.asset_id, "sha256:x")  # one identity, two roles

    def test_PF7_small_corpus_still_produces_complete_coverage_report(self):
        classified = [{"assetId": "sha256:a", "systemFamily": "UNKNOWN",
                       "perceptionTask": "ELEMENT_DETECTION",
                       "componentClass": "TEXT", "corpusRole": "CALIBRATION",
                       "criticality": "NORMAL"}]
        cov = build_coverage([AssetScore(asset_id="sha256:a", scored=True,
                                         tasks={})], classified)
        for section in ("systemFamilyCoverage", "perceptionTaskCoverage",
                        "componentClassCoverage", "corpusRoleCoverage",
                        "criticalityCoverage"):
            self.assertIn(section, cov)
        self.assertEqual(cov["assetCount"], 1)
        self.assertIn("holdoutStatus", cov)

    def test_PF8_no_runtime_semantic_dependency(self):
        """The evaluation package imports only stdlib + evaluation modules;
        no Runtime/Agent/Container/GoalEvidence concepts exist here."""
        import sys
        banned = ("UniClaw.Runtime", "SemanticRunResult", "GoalEvidence",
                  "BusinessIntent", "DeviceAction")
        import evaluation
        src = []
        import importlib, pkgutil, inspect
        for mod in pkgutil.walk_packages(evaluation.__path__,
                                         prefix="evaluation."):
            if "tests" in mod.name:
                continue
            m = importlib.import_module(mod.name)
            src.append(inspect.getsource(m))
        joined = "\n".join(src)
        for b in banned:
            self.assertNotIn(b, joined,
                             f"evaluation package must not reference {b}")

    def test_B20_evaluation_has_no_runtime_mutation_surface(self):
        """Evaluation artifacts are data records; no dispatch/mutation API
        exists in the evaluation package."""
        import evaluation
        import inspect
        import pkgutil, importlib
        surface = []
        for mod in pkgutil.walk_packages(evaluation.__path__,
                                         prefix="evaluation."):
            if "tests" in mod.name:
                continue
            m = importlib.import_module(mod.name)
            for name, obj in inspect.getmembers(m):
                if callable(obj) and not name.startswith("_"):
                    surface.append(name)
        for banned in ("dispatch", "mutate", "promote", "activate", "execute_action"):
            self.assertNotIn(banned, surface)

    def test_B19_unknown_preserved_through_manifest_roundtrip(self):
        a = EvaluationAsset(
            asset_schema_version=EVALUATION_SCHEMA_VERSION,
            content_hash="sha256:x", source_path="/x.png",
            admission=AdmissionStance.NEEDS_GROUND_TRUTH,
            provenance=Provenance.RECORDED_REALITY,
            corpus_roles=(CorpusRole.CALIBRATION,),
            system_family=SystemFamily.UNKNOWN,
            scenario_domain=ScenarioDomain.UNKNOWN,
            perception_tasks=(),
            component_class=ComponentClass.UNKNOWN,
            difficulty=Difficulty.UNKNOWN, criticality=Criticality.UNKNOWN,
        )
        j = a.to_manifest()
        b = EvaluationAsset.from_manifest(j)
        self.assertEqual(b.system_family, SystemFamily.UNKNOWN)
        self.assertEqual(b.difficulty, Difficulty.UNKNOWN)
        self.assertEqual(b.criticality, Criticality.UNKNOWN)


class _ImportGuard:
    """Ensure evaluation.dataset does not exist (B18)."""


if __name__ == "__main__":
    unittest.main()
