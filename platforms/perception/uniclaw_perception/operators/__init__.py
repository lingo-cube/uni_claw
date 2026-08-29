"""Perception Operator & Cascading-Rule framework core (S1, slice A).

Public surface of the framework core — operator contracts and registry,
selector model, specificity-cascade resolver with provenance and
intersection-scoped conflict detection, and deterministic rule-set
serialization with a loader/linter.

This is the pure framework core per OpenSpec change
``perception-operator-rule-framework`` (S1.1–S1.4).  Zero wiring: nothing in
the fusion pipeline or server consumes this package yet; the S1B port will
declare the operator pipeline via :meth:`OperatorRegistry.declare_pipeline`.
"""
from __future__ import annotations

from .contracts import (
    BoundSpec,
    EnumBounds,
    NumericBounds,
    OperatorAuthority,
    OperatorContract,
    OperatorRegistry,
    ParameterSpec,
    ParameterType,
    SafeDirection,
)
from .resolver import (
    DEFAULT_COMPLEXITY_BUDGET,
    DIAG_COMPLEXITY_BUDGET,
    DIAG_DEAD_RULE,
    DIAG_DUPLICATE_RULE_ID,
    DIAG_INVALID_ENUM_VALUE,
    DIAG_INVALID_PARAM_KEY,
    DIAG_INVALID_SELECTOR,
    DIAG_OUT_OF_BOUNDS,
    DIAG_SPECIFICITY_CONFLICT,
    DIAG_UNKNOWN_PARAMETER,
    DIAG_UNSAFE_VALIDATOR_ADJUSTMENT,
    DIAG_VALIDATOR_DISABLE,
    LintDiagnostic,
    ResolutionConflictError,
    ResolutionProvenance,
    ResolvedParams,
    lint_rule_set,
    resolve,
)
from .ruleset import (
    SCHEMA_VERSION,
    RuleSetLoad,
    deserialize_rule_set,
    load_rule_set,
    serialize_rule_set,
)
from .selector import (
    DEFAULT,
    FrameContext,
    Rule,
    SelectorDimensions,
    matches,
    specificity,
)
from .registry_defaults import (
    DEFAULT_CONTEXT,
    DEFAULT_RULE_SET,
    REGISTRY,
    RUNNERS,
    default_rule_set,
)
from .trace import (
    TRACE_FORMAT,
    TRACE_FORMAT_VERSION,
    TraceRecord,
    execute_pipeline,
    input_fingerprint,
    replay,
    rule_set_hash,
)

__all__ = [
    # contracts
    "OperatorAuthority",
    "ParameterType",
    "SafeDirection",
    "NumericBounds",
    "EnumBounds",
    "BoundSpec",
    "ParameterSpec",
    "OperatorContract",
    "OperatorRegistry",
    # selector
    "DEFAULT",
    "SelectorDimensions",
    "FrameContext",
    "Rule",
    "matches",
    "specificity",
    # resolver
    "ResolvedParams",
    "ResolutionProvenance",
    "ResolutionConflictError",
    "LintDiagnostic",
    "lint_rule_set",
    "resolve",
    "DEFAULT_COMPLEXITY_BUDGET",
    "DIAG_INVALID_SELECTOR",
    "DIAG_INVALID_PARAM_KEY",
    "DIAG_DUPLICATE_RULE_ID",
    "DIAG_DEAD_RULE",
    "DIAG_UNKNOWN_PARAMETER",
    "DIAG_OUT_OF_BOUNDS",
    "DIAG_INVALID_ENUM_VALUE",
    "DIAG_VALIDATOR_DISABLE",
    "DIAG_UNSAFE_VALIDATOR_ADJUSTMENT",
    "DIAG_COMPLEXITY_BUDGET",
    "DIAG_SPECIFICITY_CONFLICT",
    # ruleset
    "SCHEMA_VERSION",
    "serialize_rule_set",
    "deserialize_rule_set",
    "load_rule_set",
    "RuleSetLoad",
    # S1B wiring (registry defaults + pipeline execution + trace/replay)
    "REGISTRY",
    "RUNNERS",
    "DEFAULT_CONTEXT",
    "DEFAULT_RULE_SET",
    "default_rule_set",
    "TRACE_FORMAT",
    "TRACE_FORMAT_VERSION",
    "TraceRecord",
    "execute_pipeline",
    "input_fingerprint",
    "rule_set_hash",
    "replay",
]