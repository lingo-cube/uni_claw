"""Seed corpus onboarding (P4-4A) — inventory existing assets truthfully.

Ground truth sources are restricted to:
  • authoritative repository evidence (Harness screenshots manifest
    verificationCredential),
  • test-only synthetic fixtures.
No visual guessing (I8). No prediction copied into GT (B4).
"""
from __future__ import annotations

from pathlib import Path
from typing import Any

from PIL import Image

from .asset import (
    AdmissionStance, ComponentClass, CorpusRole, Criticality, Difficulty,
    EvaluationAsset, PerceptionTask, Provenance, ScenarioDomain, SystemFamily,
    save_asset_manifest,
)
from .groundtruth import GroundTruth, GroundTruthElement, save_groundtruth
from .stage import EvaluationTargetStage, LabelSpace
from . import EVALUATION_SCHEMA_VERSION

# ── repository paths ────────────────────────────────────────────
_UNICLAW_ARTIFACTS = Path("/Users/fran/Documents/Code/spacex/uni-claw/artifacts")
_SCREENSHOTS = _UNICLAW_ARTIFACTS / "assets" / "screenshots"
_FIXTURES_SRC = Path("/Users/fran/Documents/Code/spacex/uni-agent/platforms/perception/tests/fixtures")

# ── synthetic fixture generation (test-only, deterministic) ────

def _generate_synthetic_fixture(out_path: Path, width: int = 400, height: int = 800,
                                rects: list[tuple[float, float, float, float]] | None = None,
                                seed_fill: tuple[int, int, int] = (255, 255, 255),
                                rect_fill: tuple[int, int, int] = (20, 20, 20)) -> Path:
    """Deterministic synthetic screenshot: white canvas + black rectangles.

    Truth is derived from the geometry we drew — legitimate test-only
    synthetic fixture GT (I9). Provenance stays SYNTHETIC; these fixtures
    never inflate the real-model quality claim (scorecard slices by
    provenance).
    """
    if rects is None:
        rects = [
            (0.10, 0.10, 0.40, 0.20),   # text_block-like
            (0.10, 0.30, 0.60, 0.40),   # text_block-like
            (0.10, 0.50, 0.30, 0.60),   # icon-like
            (0.10, 0.70, 0.45, 0.80),   # list_item-like
        ]
    img = Image.new("RGB", (width, height), seed_fill)
    from PIL import ImageDraw
    draw = ImageDraw.Draw(img)
    for r in rects:
        x1, y1, x2, y2 = r
        draw.rectangle([x1 * width, y1 * height, x2 * width, y2 * height],
                       fill=rect_fill)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    img.save(out_path, format="PNG")
    return out_path


SYNTHETIC_FIXTURE_1 = Path(__file__).resolve().parent / "assets" / "fixtures" / "synthetic-1.png"
SYNTHETIC_FIXTURE_2 = Path(__file__).resolve().parent / "assets" / "fixtures" / "synthetic-2.png"

_SYNTHETIC_1_RECTS = [
    (0.10, 0.10, 0.40, 0.20),
    (0.10, 0.30, 0.60, 0.40),
    (0.10, 0.50, 0.30, 0.60),
    (0.10, 0.70, 0.45, 0.80),
]
_SYNTHETIC_2_RECTS = [
    (0.15, 0.12, 0.45, 0.22),
    (0.15, 0.55, 0.35, 0.65),
]


def synthetic_fixture_ground_truth(asset_id: str, rects: list[tuple[float, float, float, float]],
                                   version: str = "1") -> GroundTruth:
    """GT derived from geometry drawn into the synthetic fixture.

    Stage: FUSED_EVIDENCE / FUSED_OUTPUT_V1 — the fixture truth describes
    what the fused candidate output should report (text_block candidates
    at drawn geometry), which is how the evaluation matcher consumes it.
    """
    return GroundTruth(
        schema_version=EVALUATION_SCHEMA_VERSION,
        asset_id=asset_id,
        gt_version=version,
        source="synthetic-fixture",
        review_status="reviewed",
        evaluation_target_stage=EvaluationTargetStage.FUSED_EVIDENCE,
        label_space=LabelSpace.FUSED_OUTPUT_V1,
        declared_tasks=(PerceptionTask.ELEMENT_DETECTION, PerceptionTask.BOUNDS),
        elements=tuple(
            GroundTruthElement(gt_class="text_block", bounds=r)
            for r in rects
        ),
    )


# ── seed inventory (P4-4A) ──────────────────────────────────────

def onboarding_report() -> dict[str, Any]:
    """Truthful inventory of discovered assets (R4/I34)."""
    a1 = _SCREENSHOTS / "settings-home-api35-full-20260803.png"
    a2 = _SCREENSHOTS / "settings-diag-20260803.png"
    return {
        "settings-home-api35-full-20260803.png": {
            "status": "ADMITTED",
            "groundTruth": "harness-manifest-v1 (ocrExpected + yoloExpectedCounts)",
            "systemFamily": "ANDROID_AOSP (emulator manifest evidence: uniclaw-lite-api35)",
        },
        "settings-diag-20260803.png": {
            "status": "NEEDS_GROUND_TRUTH",
            "groundTruth": None,
            "systemFamily": "UNKNOWN",
        },
        "vision_test_controlled_screen.evidence.json": {
            "status": "INFORMATIONAL_ONLY", "reason": "stored historical model output",
        },
        "vision_test_controlled_screen.android-ui-yolo.evidence.json": {
            "status": "INFORMATIONAL_ONLY", "reason": "stored historical model output",
        },
        "settings-real.android-ui-yolo.evidence.json": {
            "status": "INFORMATIONAL_ONLY", "reason": "stored historical model output",
        },
        "synthetic-1.png": {"status": "ADMITTED", "groundTruth": "synthetic-fixture"},
        "synthetic-2.png": {"status": "ADMITTED", "groundTruth": "synthetic-fixture"},
    }


def onboarding() -> dict[str, Any]:
    """Execute onboarding: create manifests + GT records; return summary."""
    out_dir = Path(__file__).resolve().parent / "assets"
    manifests = out_dir / "manifests"
    gt_dir = out_dir / "groundtruth"

    created: list[str] = []
    inventory: list[EvaluationAsset] = []

    # A1 — settings-home-api35: ADMITTED, GT from Harness manifest (I8/I35)
    a1 = EvaluationAsset.from_file(
        _SCREENSHOTS / "settings-home-api35-full-20260803.png",
        EVALUATION_SCHEMA_VERSION,
        admission=AdmissionStance.ADMITTED,
        provenance=Provenance.RECORDED_REALITY,
        corpus_roles=(CorpusRole.CALIBRATION,),
        system_family=SystemFamily.ANDROID_AOSP,   # evidenced, not inferred
        scenario_domain=ScenarioDomain.SETTINGS,
        perception_tasks=(PerceptionTask.ELEMENT_DETECTION, PerceptionTask.OCR,
                          PerceptionTask.SAFETY),
        component_class=ComponentClass.UNKNOWN,
        difficulty=Difficulty.UNKNOWN,
        criticality=Criticality.NORMAL,
        source_relations={"harnessManifest": "artifacts/assets/screenshots/manifest.json"},
    )
    p = save_asset_manifest(a1, manifests)
    created.append(str(p))
    inventory.append(a1)

    a1_gt = GroundTruth(
        schema_version=EVALUATION_SCHEMA_VERSION,
        asset_id=a1.asset_id,
        gt_version="1",
        source="harness-manifest-v1",
        review_status="reviewed",
        # T0-C: historical expectation — field name suggests RAW_DETECTION
        # stage, but the exact vocabulary boundary (pre/post alias
        # normalization, pre/post interactive filtering) is UNRESOLVED.
        # Result: DIAGNOSTIC_ONLY, NOT_RELEASE_ELIGIBLE.
        evaluation_target_stage=EvaluationTargetStage.RAW_DETECTION,
        label_space=LabelSpace.UNRESOLVED,
        declared_tasks=(PerceptionTask.ELEMENT_DETECTION, PerceptionTask.OCR),
        expected_class_counts={"text_block": 17, "icon": 13, "list_item": 3},
        expected_texts=(
            "About emulated device", "Security & privacy", "Search settings",
            "Passwords, passkeys & accounts", "System",
        ),
        notes={"sourceFile": "artifacts/assets/screenshots/manifest.json",
               "verificationCredential": "yoloExpectedCounts + ocrExpected"},
    )
    p = save_groundtruth(a1_gt, gt_dir)
    created.append(str(p))

    # A2 — settings-diag: NEEDS_GROUND_TRUTH (I8: no fabrication)
    a2 = EvaluationAsset.from_file(
        _SCREENSHOTS / "settings-diag-20260803.png",
        EVALUATION_SCHEMA_VERSION,
        admission=AdmissionStance.NEEDS_GROUND_TRUTH,
        provenance=Provenance.RECORDED_REALITY,
        corpus_roles=(CorpusRole.CALIBRATION,),
        system_family=SystemFamily.UNKNOWN,
        scenario_domain=ScenarioDomain.UNKNOWN,
        perception_tasks=(),
        component_class=ComponentClass.UNKNOWN,
        difficulty=Difficulty.UNKNOWN,
        criticality=Criticality.UNKNOWN,
        source_relations={"harnessManifest": "artifacts/assets/screenshots/manifest.json"},
    )
    p = save_asset_manifest(a2, manifests)
    created.append(str(p))
    inventory.append(a2)

    # A3-A5 — stored historical output: INFORMATIONAL_ONLY (I39/PF3)
    for name, provenance in [
        ("vision_test_controlled_screen.evidence.json", Provenance.SYNTHETIC),
        ("vision_test_controlled_screen.android-ui-yolo.evidence.json", Provenance.SYNTHETIC),
        ("settings-real.android-ui-yolo.evidence.json", Provenance.REALITY_SEEDED),
    ]:
        src = _FIXTURES_SRC / name
        a = EvaluationAsset.from_file(
            src, EVALUATION_SCHEMA_VERSION,
            admission=AdmissionStance.INFORMATIONAL_ONLY,
            provenance=provenance,
            corpus_roles=(),
            system_family=SystemFamily.UNKNOWN,
            scenario_domain=ScenarioDomain.UNKNOWN,
            perception_tasks=(),
            component_class=ComponentClass.UNKNOWN,
            difficulty=Difficulty.UNKNOWN,
            criticality=Criticality.UNKNOWN,
            source_relations={"note": "stored historical perception output — "
                                      "never current-model accuracy evidence"},
        )
        p = save_asset_manifest(a, manifests)
        created.append(str(p))
        inventory.append(a)

    # S1 — synthetic fixture 1: ADMITTED (mechanics proof, SYNTHETIC)
    _generate_synthetic_fixture(SYNTHETIC_FIXTURE_1, rects=_SYNTHETIC_1_RECTS)
    s1 = EvaluationAsset.from_file(
        SYNTHETIC_FIXTURE_1, EVALUATION_SCHEMA_VERSION,
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
    p = save_asset_manifest(s1, manifests)
    created.append(str(p))
    inventory.append(s1)
    s1_gt = synthetic_fixture_ground_truth(s1.asset_id, _SYNTHETIC_1_RECTS)
    p = save_groundtruth(s1_gt, gt_dir)
    created.append(str(p))

    return {
        "created": created,
        "inventory": inventory,
        "admitted": [a.asset_id for a in inventory
                     if a.admission == AdmissionStance.ADMITTED],
        "needsGroundTruth": [a.asset_id for a in inventory
                             if a.admission == AdmissionStance.NEEDS_GROUND_TRUTH],
        "informationalOnly": [a.asset_id for a in inventory
                              if a.admission == AdmissionStance.INFORMATIONAL_ONLY],
    }
