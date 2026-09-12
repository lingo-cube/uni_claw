"""Closed status vocabulary for fusion operator decision records.

Every decision/verdict record produced by a composition operator carries a
``status`` drawn from this single frozen set.  The vocabulary is the actual
surface of the pipeline today (generators, validators, router, tracer); values
are unchanged so trace consumers and existing assertions stay valid — this
module only makes the vocabulary explicit and guard-tested (no free-form
status strings may enter the deterministic pipeline trace).  See the operator
ownership / fallback note in ``docs/analysis/runtime-stability-engineering-landscape.md``
§13: the MATCH/PARTIAL/NOOP/REJECTED/ERROR OperatorResult draft there remains a
proposal and is NOT applied here.
"""
from __future__ import annotations

#: A generator operator produced output and owns the handled evidence.
ACTIVATED = "activated"
#: The operator ran but produced nothing / consumed no evidence
#: (no ownership; remaining evidence stays eligible for fallback).
NOOP = "noop"
#: A validator vetoed the evidence (fail-closed).
REJECTED = "rejected"
#: A validator confirmed the evidence.
VERIFIED = "verified"
#: Router / instrumentation decided to fail closed without a verdict.
FAIL_CLOSED = "fail_closed"
#: Row-relation-head band-level status: the band was composed into a row head.
COMPOSED = "composed"

#: The frozen, closed status set.  Guard-tested by
#: ``platforms/perception/tests/test_operator_status_vocabulary.py`` (AST scan
#: of every operator module: no literal status outside this set may appear).
ALL: frozenset[str] = frozenset(
    {ACTIVATED, NOOP, REJECTED, VERIFIED, FAIL_CLOSED, COMPOSED}
)


def validate_status(status: str) -> str:
    """Return ``status`` if it is a member of the closed vocabulary, else raise.

    Intended at decision-record construction points that must never emit a
    free-form status into the deterministic pipeline trace.
    """
    if status not in ALL:
        raise ValueError(
            f"unknown operator status {status!r}; closed set: {sorted(ALL)}"
        )
    return status