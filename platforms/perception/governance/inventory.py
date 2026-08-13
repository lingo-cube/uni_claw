"""Mechanized effective config inventory (P4-D1).

The Gate's 29-row audit as executable truth: every evidence-affecting
setting has EXACTLY ONE canonical identity owner. Zero-owner or
multi-owner material settings fail the guard (tested).
"""
from __future__ import annotations

from typing import Any

# Identity owner constants
MODEL_ID = "MODEL_ID"
CONFIG_ID = "CONFIG_ID"
PIPELINE_REVISION = "PIPELINE_REVISION"
SCHEMA_VERSION = "SCHEMA_VERSION"
OPERATIONAL_ONLY = "OPERATIONAL_ONLY"

# (name, stage, evidence_affecting, owner)
INVENTORY: tuple[tuple[str, str, bool, str], ...] = (
    ("model artifact",              "YOLO",       True,  MODEL_ID),
    ("yolo imgsz",                  "YOLO",       True,  PIPELINE_REVISION),
    ("yolo confidence",             "YOLO",       True,  CONFIG_ID),
    ("yolo device",                 "YOLO",       True,  PIPELINE_REVISION),
    ("nms iou threshold",           "YOLO",       True,  PIPELINE_REVISION),
    ("max_det",                     "YOLO",       True,  PIPELINE_REVISION),
    ("agnostic_nms",                "YOLO",       True,  PIPELINE_REVISION),
    ("half precision",              "YOLO",       True,  PIPELINE_REVISION),
    ("inference augment",           "YOLO",       True,  PIPELINE_REVISION),
    ("preprocess maxWidth",         "Preprocess", True,  CONFIG_ID),
    ("preprocess cropTop",          "Preprocess", True,  CONFIG_ID),
    ("preprocess cropBottom",       "Preprocess", True,  CONFIG_ID),
    ("ocr backend",                 "OCR",        True,  CONFIG_ID),
    ("ocr mode",                    "OCR",        True,  CONFIG_ID),
    ("ocr textScore",               "OCR",        True,  CONFIG_ID),
    ("ocr language",                "OCR",        True,  CONFIG_ID),
    ("ocr parallel workers",        "OCR",        False, OPERATIONAL_ONLY),
    ("roi padding spec",            "OCR",        True,  CONFIG_ID),
    ("ocr model bytes (pkg-owned)", "OCR",        True,  PIPELINE_REVISION),
    ("fusion max ocr distance",     "Fusion",     True,  PIPELINE_REVISION),
    ("chevron row tolerance",       "Fusion",     True,  PIPELINE_REVISION),
    ("interactive label set",       "Fusion",     True,  PIPELINE_REVISION),
    ("confidence weights",          "Fusion",     True,  PIPELINE_REVISION),
    ("text promotion/search-box",   "Fusion",     True,  PIPELINE_REVISION),
    ("label alias mapping",         "Normalize",  True,  PIPELINE_REVISION),
    ("coordinate contract",         "Remap",      True,  SCHEMA_VERSION),
    ("label-mapping adapter map",   "Adapter",    False, CONFIG_ID),
    ("scroll edge threshold",       "Scroll",     True,  CONFIG_ID),
    ("socket/omp/restarts/timeouts","Host",       False, OPERATIONAL_ONLY),
)

_OWNERS = (MODEL_ID, CONFIG_ID, PIPELINE_REVISION, SCHEMA_VERSION,
           OPERATIONAL_ONLY)


def material_settings() -> list[tuple[str, str, str]]:
    """Evidence-affecting settings and their single owner."""
    return [(n, s, o) for (n, s, e, o) in INVENTORY if e]


def verify_single_ownership() -> dict[str, Any]:
    """Every evidence-affecting setting has exactly one canonical owner."""
    violations: list[str] = []
    seen: dict[str, str] = {}
    for name, stage, evidence, owner in INVENTORY:
        if owner not in _OWNERS:
            violations.append(f"{name}: unknown owner {owner}")
        if evidence:
            if name in seen and seen[name] != owner:
                violations.append(f"{name}: multiple owners")
            seen[name] = owner
    zero_owner = [n for (n, s, e, o) in INVENTORY if e and o is None]
    violations.extend(f"{n}: zero owner" for n in zero_owner)
    return {
        "totalSettings": len(INVENTORY),
        "materialSettings": sum(1 for (_, _, e, _) in INVENTORY if e),
        "operationalSettings": sum(1 for (_, _, e, _) in INVENTORY if not e),
        "violations": violations,
        "pass": not violations,
    }
