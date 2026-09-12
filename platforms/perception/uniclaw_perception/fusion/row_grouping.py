"""Frame-local uniform vertical navigation-list row grouping — compatibility shim.

This module is deliberately perception-internal.  It derives a narrow visual
layout relation from candidates already produced by the single YOLO/OCR pass;
it does not use UI hierarchy, history, content meaning, or Runtime state.

Since OpenSpec change ``perception-operator-rule-framework`` S1B (WI-PFW-S1B),
the implementation lives in the operator framework as the
``uniform-list-row-grouping`` GENERATOR
(``uniclaw_perception/operators/uniform_list_row_grouping.py``); this file is
a thin compatibility shim re-exporting the retained candidate's public entry
point :func:`apply_uniform_list_grouping` so the legacy call surface stays
byte-identical.  The engine now executes the operator via the declared
pipeline (registry ``declare_pipeline`` + resolved root-rule defaults), not
through this shim.

Behavior contract (unchanged): fail-closed activation from four or more
existing actionable rows, at most three consecutive cadence slots filled only
when every slot is unambiguous, never extrapolating a bracket beyond its
confirmed neighbors.  Raw YOLO/OCR arrays are owned by the caller and are not
modified.
"""
from __future__ import annotations

from typing import Any

from uniclaw_perception.operators.uniform_list_row_grouping import (
    GROUPING_PARAM_DEFAULTS,
    apply_uniform_list_grouping_params,
)

__all__ = ["apply_uniform_list_grouping"]


def apply_uniform_list_grouping(
    candidates: list[dict[str, Any]],
    _yolo_detections: list[Any],
) -> None:
    """Legacy entry point: ported operator with the retained candidate's
    current-constant parameter defaults (the default rule set's resolution).

    Byte-identical to the pre-port implementation for the same inputs.
    """
    apply_uniform_list_grouping_params(
        candidates, _yolo_detections, params=GROUPING_PARAM_DEFAULTS
    )