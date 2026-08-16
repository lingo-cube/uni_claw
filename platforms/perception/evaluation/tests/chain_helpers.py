"""Canonical-chain builders — GAP-004 FINAL falsifier fixtures.

BaselineReport.create is a pure DERIVATION over persisted canonical
records: EvaluationRunRequest -> EvaluationSuite -> terminal
EvaluationRunResult -> per-member Prediction / GroundTruth / manifest.
These helpers only persist records; they cannot declare population,
counts, coverage, sufficiency, safety, or GT-version authority.
"""
from __future__ import annotations

import tempfile
from pathlib import Path
from typing import Any

from evaluation import EVALUATION_SCHEMA_VERSION
from evaluation.asset import (
    AdmissionStance, ComponentClass, CorpusRole, Criticality, Difficulty,
    EvaluationAsset, PerceptionTask, Provenance, ScenarioDomain, SystemFamily,
    save_asset_manifest,
)
from evaluation.deployment import DeploymentSnapshot
from evaluation.groundtruth import (
    GroundTruth, GroundTruthElement, save_groundtruth,
)
from evaluation.prediction import Prediction, save_prediction
from evaluation.run import (
    AssetEvaluationOutcome, AssetOutcomeKind, EnvironmentProfile,
    EvaluationRunRequest, EvaluationRunResult, save_request, save_result,
)
from evaluation.suite import EvaluationSuite, SuiteMembership, save_suite


def asset_id(i: int) -> str:
    """Deterministic fake content-addressed asset id."""
    return f"sha256:{i:064x}"


def make_deployment() -> DeploymentSnapshot:
    return DeploymentSnapshot.current_active(
        service_version="1.0", model_name="test_model", canonical=False)


class Chain:
    """Persisted canonical-record store for one evaluation chain."""

    def __init__(self) -> None:
        self._temp = tempfile.TemporaryDirectory()
        self.root = Path(self._temp.name)
        self.run_dir = self.root / "runs"
        self.suite_dir = self.root / "suites"
        self.prediction_dir = self.root / "predictions"
        self.gt_dir = self.root / "groundtruth"
        self.manifest_dir = self.root / "manifests"
        for d in (self.run_dir, self.suite_dir, self.prediction_dir,
                  self.gt_dir, self.manifest_dir):
            d.mkdir(parents=True, exist_ok=True)
        self.deployment = make_deployment()

    def close(self) -> None:
        self._temp.cleanup()

    # ── per-member records ────────────────────────────────────────────────
    def add_gt(
        self, asset: str, gt_version: str = "1",
        *,
        declared_tasks: tuple[PerceptionTask, ...] = (
            PerceptionTask.ELEMENT_DETECTION,),
        elements: tuple[GroundTruthElement, ...] = (
            GroundTruthElement(gt_class="box",
                               bounds=(0.1, 0.1, 0.2, 0.2)),),
        expected_class_counts: dict[str, int] | None = None,
        source: str = "synthetic-fixture",
    ) -> GroundTruth:
        gt = GroundTruth(
            schema_version=EVALUATION_SCHEMA_VERSION, asset_id=asset,
            gt_version=gt_version, source=source,
            declared_tasks=declared_tasks, elements=elements,
            expected_class_counts=expected_class_counts)
        save_groundtruth(gt, self.gt_dir)
        return gt

    def add_prediction(
        self, asset: str, run_id: str,
        *, candidates: tuple[dict[str, Any], ...] = (),
        deployment_hash: str | None = None,
    ) -> Prediction:
        pred = Prediction(
            run_id=run_id, asset_id=asset,
            deployment_hash=deployment_hash or self.deployment.identity_hash,
            schema_version="test", candidates=candidates,
            yolo_count=0, ocr_count=0,
            stage_views={"fusedEvidence": [dict(c) for c in candidates]})
        save_prediction(pred, self.prediction_dir)
        return pred

    def add_manifest(
        self, asset: str,
        *,
        system_family: SystemFamily = SystemFamily.UNKNOWN,
        component_class: ComponentClass = ComponentClass.UNKNOWN,
        perception_tasks: tuple[PerceptionTask, ...] = (
            PerceptionTask.ELEMENT_DETECTION,),
        corpus_roles: tuple[CorpusRole, ...] = (CorpusRole.GOLDEN,),
        criticality: Criticality = Criticality.UNKNOWN,
    ) -> EvaluationAsset:
        manifest = EvaluationAsset(
            asset_schema_version=EVALUATION_SCHEMA_VERSION,
            content_hash=asset, source_path=f"fixtures/{asset}.png",
            admission=AdmissionStance.ADMITTED,
            provenance=Provenance.SYNTHETIC,
            corpus_roles=corpus_roles, system_family=system_family,
            scenario_domain=ScenarioDomain.UNKNOWN,
            perception_tasks=perception_tasks,
            component_class=component_class, difficulty=Difficulty.UNKNOWN,
            criticality=criticality)
        save_asset_manifest(manifest, self.manifest_dir)
        return manifest

    # ── canonical chain records ───────────────────────────────────────────
    def build_suite(
        self, members: tuple[str, ...],
        required_tasks: tuple[PerceptionTask, ...] = (
            PerceptionTask.ELEMENT_DETECTION,),
    ) -> EvaluationSuite:
        suite = EvaluationSuite(
            suite_schema_version=EVALUATION_SCHEMA_VERSION,
            required_tasks=required_tasks,
            members=tuple(
                SuiteMembership(asset_id=a, roles=(CorpusRole.GOLDEN,))
                for a in members))
        save_suite(suite, self.suite_dir)
        return suite

    def build_request(
        self, suite: EvaluationSuite,
        asset_scope: tuple[str, ...] = (),
    ) -> EvaluationRunRequest:
        request = EvaluationRunRequest.create(
            suite_id=suite.suite_id, deployment=self.deployment,
            backend="L2_RECORDED_IMAGE_INFERENCE",
            evaluator_revision="evaluator-v1",
            environment=EnvironmentProfile(
                os_name="test", cpu_arch="x86_64", python_version="3.13"),
            asset_scope=asset_scope)
        save_request(request, self.run_dir)
        return request

    def build_result(
        self, request: EvaluationRunRequest,
        outcomes: tuple[AssetEvaluationOutcome, ...],
    ) -> EvaluationRunResult:
        result = EvaluationRunResult.create(request.request_id, outcomes)
        save_result(result, self.run_dir)
        return result

    @staticmethod
    def scorable(asset: str, gt_version: str = "1",
                 gt_source: str = "synthetic-fixture") -> AssetEvaluationOutcome:
        return AssetEvaluationOutcome(
            asset_id=asset, kind=AssetOutcomeKind.SCORABLE,
            gt_version=gt_version, gt_source=gt_source)

    @staticmethod
    def insufficient(asset: str) -> AssetEvaluationOutcome:
        return AssetEvaluationOutcome(
            asset_id=asset, kind=AssetOutcomeKind.INSUFFICIENT_EVIDENCE,
            reason="no prediction evidence")


def persist_scored_member(chain: Chain, asset: str,
                          request: EvaluationRunRequest,
                          gt_version: str = "1") -> None:
    """Persist GT + prediction + scorable outcome for one member."""
    chain.add_gt(asset, gt_version)
    chain.add_prediction(asset, request.request_id)
