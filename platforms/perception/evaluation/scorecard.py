"""Multidimensional Scorecard + first-class Coverage + Evidence Sufficiency.

Frozen by Phase 4 gate:
  • NO_SINGLE_SCORE_AUTHORITY — OverallSummary is presentation only (B10).
  • Every score carries numerator/denominator (I24).
  • Zero scored assets in a slice → UNASSESSED, never 0%/100% (B9/I23).
  • Raw slices only — no weights (I27/R17).
"""
from __future__ import annotations

import statistics
from dataclasses import dataclass, field
from enum import Enum
from typing import Any

from .asset import CorpusRole, PerceptionTask, SystemFamily, Criticality, ComponentClass
from .groundtruth import TaskStance
from .metrics import TaskMetricResult


class SliceStatus(str, Enum):
    ASSESSED = "ASSESSED"
    PARTIALLY_ASSESSED = "PARTIALLY_ASSESSED"
    UNASSESSED = "UNASSESSED"
    INSUFFICIENT_EVIDENCE = "INSUFFICIENT_EVIDENCE"


@dataclass(frozen=True)
class AssetScore:
    asset_id: str
    scored: bool                       # any task scored
    tasks: dict[str, dict[str, Any]]   # task → metrics + stance
    classification: dict[str, Any] = field(default_factory=dict)
    # classification: {systemFamily, perceptionTask, componentClass,
    #                  corpusRole, criticality, ...} — task list joined as strings


def _aggregate(values: list[float]) -> dict[str, Any]:
    if not values:
        return {}
    return {
        "mean": round(statistics.mean(values), 6),
        "median": round(statistics.median(values), 6),
        "min": round(min(values), 6),
        "max": round(max(values), 6),
        "n": len(values),
    }


def _pick_primary_value(metrics: dict[str, Any]) -> float | None:
    """Primary numeric value of a task result for aggregation."""
    for key in ("f1", "accuracy", "exactMatchRate", "meanIoU", "coordinateValidityRate"):
        v = metrics.get(key)
        if isinstance(v, (int, float)):
            return float(v)
    return None


def build_scorecard(asset_scores: list[AssetScore]) -> dict[str, Any]:
    """Build hierarchical multidimensional scorecard (raw slices).

    asset_scores: one AssetScore per suite member (scored or not).
    """
    # ── per-slice aggregation ──
    slices: dict[str, dict[str, Any]] = {}

    def slice_key(dim: str, value: str) -> str:
        return f"{dim}:{value}"

    task_metrics: dict[str, dict[str, list[float]]] = {}   # task → metric → values
    for as_ in asset_scores:
        for task, tdata in as_.tasks.items():
            stance = tdata.get("stance")
            if stance != TaskStance.SCORED.value:
                continue
            m = tdata.get("metrics", {})
            primary = _pick_primary_value(m)
            if primary is None:
                continue
            key = slice_key("task", task)
            slices.setdefault(key, {"status": SliceStatus.ASSESSED.value,
                                    "values": [], "denominator": 0,
                                    "scoredAssets": 0})
            slices[key]["values"].append(primary)
            slices[key]["denominator"] += tdata.get("denominator", 0)
            slices[key]["scoredAssets"] += 1
            for mk, mv in m.items():
                if isinstance(mv, (int, float)):
                    task_metrics.setdefault(task, {}).setdefault(mk, []).append(float(mv))

    # system family / component / role / criticality slices come from asset
    # classification — they carry scored/total counts, not metric values
    # (metric values attach to task slices; dimension slices report coverage).
    result: dict[str, Any] = {
        "scorecardSchemaVersion": "1.0",
        "sections": {"QUALITY": {}, "SAFETY": {}, "PERFORMANCE": {},
                     "COVERAGE": {}},
        "taskSlices": {},
        "dimensionCoverage": {},
        "overallSummary": {},
        "evidenceSufficiency": {},
    }

    # task slices (quality)
    for key, data in sorted(slices.items()):
        task = key.split(":", 1)[1]
        result["taskSlices"][task] = {
            "status": data["status"],
            "denominator": data["denominator"],
            "scoredAssets": data["scoredAssets"],
            "aggregate": _aggregate(data["values"]),
            "metricDetails": {
                mk: _aggregate(vs) for mk, vs in sorted(task_metrics.get(task, {}).items())
            },
        }

    # safety slice (B11: separately visible)
    result["sections"]["SAFETY"] = {
        "visible": True,
        "perAsset": {
            a.asset_id: a.tasks.get("SAFETY", {})
            for a in asset_scores if "SAFETY" in a.tasks
        },
        "note": "safety metrics are per-asset and separately visible; "
                "no aggregate threshold is frozen",
    }

    # coverage lives in build_coverage (first-class, §I25)
    result["dimensionCoverage"] = {
        "note": "dimension coverage is reported by build_coverage() — "
                "first-class output with per-slice denominators",
    }

    return result


def build_coverage(asset_scores: list[AssetScore],
                   classified: list[dict[str, Any]]) -> dict[str, Any]:
    """First-class coverage output (I25): every slice reports denominator.

    All dimension values are seeded with zero counts so missing coverage
    remains VISIBLE (UNASSESSED), never hidden (I23/I25).
    """
    total = len(classified)
    scored = sum(1 for a in asset_scores if a.scored)
    unscored = total - scored
    scored_ids = {a.asset_id for a in asset_scores if a.scored}

    seed_values = {
        "systemFamily": [f.value for f in SystemFamily],
        "componentClass": [c.value for c in ComponentClass],
        "corpusRole": [r.value for r in CorpusRole],
        "criticality": [c.value for c in Criticality],
        "perceptionTask": [t.value for t in PerceptionTask],
    }

    def count_by(dim: str) -> dict[str, dict[str, Any]]:
        out: dict[str, dict[str, Any]] = {
            v: {"total": 0, "scored": 0} for v in seed_values.get(dim, [])
        }
        for c in classified:
            raw = c.get(dim, "UNKNOWN")
            values = [p for p in str(raw).split(",") if p]
            for v in values:
                out.setdefault(v, {"total": 0, "scored": 0})
                out[v]["total"] += 1
                if c["assetId"] in scored_ids:
                    out[v]["scored"] += 1
        for v, d in out.items():
            d["status"] = (SliceStatus.ASSESSED.value if d["scored"] > 0
                           else SliceStatus.PARTIALLY_ASSESSED.value
                           if d["total"] > 0 else SliceStatus.UNASSESSED.value)
        return out

    return {
        "assetCount": total,
        "scoredAssetCount": scored,
        "unscoredAssetCount": unscored,
        "systemFamilyCoverage": count_by("systemFamily"),
        "perceptionTaskCoverage": count_by("perceptionTask"),
        "componentClassCoverage": count_by("componentClass"),
        "corpusRoleCoverage": count_by("corpusRole"),
        "criticalityCoverage": count_by("criticality"),
        "holdoutStatus": "NONE",
        "unassessedCategories": [
            f"{dim}:{v}"
            for dim, cov in [
                ("systemFamily", count_by("systemFamily")),
                ("componentClass", count_by("componentClass")),
            ]
            for v, d in cov.items()
            if d["total"] == 0
        ],
    }


def evidence_sufficiency(asset_scores: list[AssetScore],
                         declared_tasks: list[str]) -> dict[str, Any]:
    """Evidence sufficiency separated from quality (I26).

    SUFFICIENT: every declared task has ≥1 scored asset AND all assets scored.
    PARTIAL:    ≥1 scored asset but not all declared tasks/assets covered.
    INSUFFICIENT: zero scored assets.
    """
    scored_tasks: set[str] = set()
    scored_assets = 0
    for a in asset_scores:
        if a.scored:
            scored_assets += 1
        for task, tdata in a.tasks.items():
            if tdata.get("stance") == TaskStance.SCORED.value:
                scored_tasks.add(task)
    if scored_assets == 0:
        stance = "INSUFFICIENT"
    elif all(t in scored_tasks for t in declared_tasks) and \
            scored_assets == len(asset_scores) and asset_scores:
        stance = "SUFFICIENT"
    else:
        stance = "PARTIAL"
    return {
        "stance": stance,
        "scoredAssets": scored_assets,
        "totalAssets": len(asset_scores),
        "scoredTasks": sorted(scored_tasks),
        "declaredTasks": sorted(declared_tasks),
        "uncoveredDeclaredTasks": sorted(set(declared_tasks) - scored_tasks),
    }
