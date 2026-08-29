"""``vlm-annotation`` ADVISOR operator (S4, WI-PFW-S4) — OFFLINE interface only.

Per OpenSpec change ``perception-operator-rule-framework`` (spec *"Authority
classes constrain generation"*): VLM is *offline annotation or low-frequency
advisory only and SHALL NOT enter the authorization path* — VLM use in the
authorization path is FORBIDDEN.  This module is the S4 contract-completeness
slice: the ``vlm-annotation`` ADVISOR is REGISTERED in the operator registry
(with ``enabled`` default ``False`` — see the ADVISOR-enabled contract
extension) but is NEVER part of the executed pipeline: it is not in
``declare_pipeline`` and has no entry in ``RUNNERS``, so there is NO online
call path (asserted in ``tests/test_s4_validators.py``).

The only executable surface is the pure OFFLINE interface
:func:`propose_parameter_adjustments` — currently a DETERMINISTIC no-op stub
that returns no suggestions for every input; it is the documented future VLM
integration point (estimation, annotation, parameter-adjustment proposals),
kept stub-only in this slice because S5 (learning loop) is DEFERRED behind a
separate post-S2 decision (spec: S5 SHALL NOT be entered automatically).

Determinism: no randomness, no wall-clock, no network — calling the stub with
the same arguments always returns the same value.
"""
from __future__ import annotations

from typing import Any, Mapping, Sequence

__all__ = [
    "VLM_ENABLED_DEFAULT",
    "propose_parameter_adjustments",
]

#: Contract-level ``enabled`` default for the ADVISOR: offline advisory is
#: opt-in and starts DISABLED.  (The leader-frozen authority rule: ADVISOR
#: may carry ``enabled`` with default false; VALIDATOR never can.)
VLM_ENABLED_DEFAULT: bool = False


def propose_parameter_adjustments(
    frames: Sequence[Mapping[str, Any]],
    current_params: Mapping[str, Any],
) -> list[dict[str, Any]]:
    """Offline VLM annotation / parameter-adjustment proposal interface.

    OFFLINE ONLY — never invoked by the perception pipeline (``vlm-annotation``
    is not in the declared pipeline and has no runner).  Future contract
    (documented here so the integration point is fixed; S5 is deferred):

    * ``frames`` — an ordered sequence of validated frame records (the
      caller's offline analysis set; exact schema deferred to the S5 design).
    * ``current_params`` — the currently resolved (or candidate) rule-parameter
      values, namespaced ``operatorId.paramName``, as the resolver produces.
    * returns a list of adjustment suggestions, each a dict of the shape
      ``{"operatorId", "parameter", "value", "evidenceRefs", "rationale"}``.
      Suggestions are proposals ONLY — promotion is always human-approved
      (spec: "promotion to production SHALL occur only through a new
      governance config manifest and receipt switch with human approval").

    This slice returns an empty list (deterministic no-op): no suggestions are
    ever produced, no state is touched, and ``frames`` / ``current_params``
    are treated as read-only.  Deterministic for every input.
    """
    # No-op stub: consume nothing, mutate nothing, propose nothing.
    del frames, current_params  # read-only inputs; unused by the stub
    return []