"""P4-4E — incremental asset flow proof.

Adds one new synthetic fixture through the full workflow:
  Candidate → classification → GroundTruth → SuiteVersion+1 → new
  EvaluationRun → updated Scorecard/Coverage → NEW baseline.
The previous baseline/suite files are never mutated (PF2/B16).
"""
from __future__ import annotations

from pathlib import Path

from . import EVALUATION_SCHEMA_VERSION
from .asset import (
    AdmissionStance, ComponentClass, CorpusRole, Criticality, Difficulty,
    EvaluationAsset, PerceptionTask, Provenance, ScenarioDomain, SystemFamily,
    save_asset_manifest,
)
from .deployment import DeploymentSnapshot
from .first_baseline import (
    BASELINES_DIR, SUITES_DIR, build_seed_suite, execute_baseline,
)
from .groundtruth import save_groundtruth
from .seed import (
    SYNTHETIC_FIXTURE_2, _SYNTHETIC_2_RECTS, _generate_synthetic_fixture,
    synthetic_fixture_ground_truth,
)
from .suite import EvaluationSuite, SuiteMembership, load_suite, save_suite


def _suite_members(suite: EvaluationSuite) -> tuple[SuiteMembership, ...]:
    return suite.members


def run_incremental(previous_baseline_id: str,
                    previous_suite: EvaluationSuite) -> dict:
    """Onboard synthetic-2, create suite v2, execute → new baseline."""
    # 1. candidate → classification → GT
    _generate_synthetic_fixture(SYNTHETIC_FIXTURE_2, rects=_SYNTHETIC_2_RECTS)
    s2 = EvaluationAsset.from_file(
        SYNTHETIC_FIXTURE_2, EVALUATION_SCHEMA_VERSION,
        admission=AdmissionStance.ADMITTED,
        provenance=Provenance.SYNTHETIC,
        corpus_roles=(CorpusRole.CALIBRATION,),
        system_family=SystemFamily.UNKNOWN,
        scenario_domain=ScenarioDomain.SETTINGS,
        perception_tasks=(PerceptionTask.ELEMENT_DETECTION, PerceptionTask.BOUNDS,
                          PerceptionTask.SAFETY),
        component_class=ComponentClass.TEXT,
        difficulty=Difficulty.NORMAL,
        criticality=Criticality.NORMAL,
        theme_tags=("synthetic",),
    )
    manifest = save_asset_manifest(
        s2, Path(__file__).resolve().parent / "assets" / "manifests")
    gt = synthetic_fixture_ground_truth(s2.asset_id, _SYNTHETIC_2_RECTS, version="1")
    gt_path = save_groundtruth(
        gt, Path(__file__).resolve().parent / "assets" / "groundtruth")

    # 2. suite v2 = v1 members + s2 (new suite id — PF2, never mutation)
    new_members = _suite_members(previous_suite) + (
        SuiteMembership(asset_id=s2.asset_id, roles=(CorpusRole.CALIBRATION,)),)
    suite_v2 = previous_suite.with_members(
        new_members, description="seed suite + incremental synthetic-2")
    suite_path = save_suite(suite_v2, SUITES_DIR)

    # 3. execute → new run → new baseline
    deployment = DeploymentSnapshot.current_active()
    result = execute_baseline(suite_v2, deployment,
                              description="INCREMENTAL_BASELINE_V2",
                              performance_asset_id=None)

    # 4. verify previous baseline + suite files untouched
    prev_baseline_file = BASELINES_DIR / f"{previous_baseline_id.replace('baseline:', '')}.json"
    prev_suite_file = SUITES_DIR / f"{previous_suite.suite_id.replace('suite:', '')}.json"
    from .identity import sha256_file
    prev_baseline_hash = sha256_file(prev_baseline_file)
    prev_suite_hash = sha256_file(prev_suite_file)

    return {
        "newAssetId": s2.asset_id,
        "newManifest": str(manifest),
        "newGt": str(gt_path),
        "newSuiteId": suite_v2.suite_id,
        "newSuitePath": str(suite_path),
        "newBaselineId": result["baselineId"],
        "previousBaselineIntact": prev_baseline_hash,
        "previousSuiteIntact": prev_suite_hash,
        "newRunId": result["runId"],
        "evidenceSufficiency": result["evidenceSufficiency"],
    }


def main() -> int:
    # locate the previous (v1) suite + baseline from disk
    suite_files = sorted(SUITES_DIR.glob("*.json"))
    baseline_files = sorted(BASELINES_DIR.glob("*.json"))
    assert suite_files and baseline_files, "run first_baseline.main() first"
    prev_suite = load_suite(suite_files[0])
    import json
    prev_baseline_id = json.loads(baseline_files[0].read_text(encoding="utf-8"))["baselineId"]
    from .identity import sha256_file
    pre_b_hash = sha256_file(baseline_files[0])
    pre_s_hash = sha256_file(suite_files[0])

    result = run_incremental(prev_baseline_id, prev_suite)

    print(f"previous baseline id: {prev_baseline_id}")
    print(f"previous suite id:    {prev_suite.suite_id}")
    print(f"new asset:            {result['newAssetId']}")
    print(f"new suite:            {result['newSuiteId']}")
    print(f"new baseline:         {result['newBaselineId']}")
    assert result["newSuiteId"] != prev_suite.suite_id
    assert result["newBaselineId"] != prev_baseline_id
    assert result["previousBaselineIntact"] == pre_b_hash, "baseline was mutated!"
    assert result["previousSuiteIntact"] == pre_s_hash, "suite was mutated!"
    print("PF2/B16 incremental proof: PASS — new versions created, "
          "previous artifacts byte-identical")
    print(f"evidence sufficiency: {result['evidenceSufficiency']['stance']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
