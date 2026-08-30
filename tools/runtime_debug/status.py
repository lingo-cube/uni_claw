"""Closed command status vocabulary and its fixed exit-code mapping (P0 tooling contract)."""

from __future__ import annotations

OK = "OK"
INVALID_INPUT = "INVALID_INPUT"
EVIDENCE_UNAVAILABLE = "EVIDENCE_UNAVAILABLE"
IDENTITY_MISMATCH = "IDENTITY_MISMATCH"
AMBIGUOUS_OCCURRENCE = "AMBIGUOUS_OCCURRENCE"
INSUFFICIENT_TRACE_COVERAGE = "INSUFFICIENT_TRACE_COVERAGE"
SCHEMA_VIOLATION = "SCHEMA_VIOLATION"

CLOSED_STATUSES = frozenset({
    OK,
    INVALID_INPUT,
    EVIDENCE_UNAVAILABLE,
    IDENTITY_MISMATCH,
    AMBIGUOUS_OCCURRENCE,
    INSUFFICIENT_TRACE_COVERAGE,
    SCHEMA_VIOLATION,
})

# Fixed mapping — do not renumber (CLI contract).
EXIT_CODES = {
    OK: 0,
    INVALID_INPUT: 2,
    EVIDENCE_UNAVAILABLE: 3,
    IDENTITY_MISMATCH: 4,
    AMBIGUOUS_OCCURRENCE: 5,
    INSUFFICIENT_TRACE_COVERAGE: 6,
    SCHEMA_VIOLATION: 7,
}


def exit_code(status: str) -> int:
    if status not in EXIT_CODES:
        raise ValueError(f"not a closed status: {status!r}")
    return EXIT_CODES[status]