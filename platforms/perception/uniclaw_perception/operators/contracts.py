"""Operator contract types and the operator registry.

Implements the spec requirement *"Operator contract"* (stable id/version,
authority class, typed input/output, bounded parameter schema with safe
direction for validators, fail-closed contract) and *"Authority classes
constrain generation"* (only GENERATOR operators may produce identity
candidates).  This is the S1.1 slice of the perception Operator & Cascading-
Rule framework: pure types and a registry that stores contracts.  Pipeline
topology (which operators run, in what order) is declared separately by code
via :meth:`OperatorRegistry.declare_pipeline`; rules only parameterize
operators (spec: *"Pipeline topology is code-owned; rules parameterize only"*).

Bounds are mandatory for numeric parameters and enum parameters so that every
operator parameter is *bounded* (spec: "a bounded parameter schema (typed
min/max/enum with safe direction for validators)").
"""
from __future__ import annotations

import math
from dataclasses import dataclass
from enum import Enum
from typing import Any, Mapping, Sequence

__all__ = [
    "OperatorAuthority",
    "ParameterType",
    "SafeDirection",
    "NumericBounds",
    "EnumBounds",
    "ParameterSpec",
    "OperatorContract",
    "OperatorRegistry",
]


class OperatorAuthority(str, Enum):
    """Authority class of a perception composition operator.

    Per spec *"Authority classes constrain generation"*: navigation/menu
    identity SHALL be generatable only by GENERATOR operators; VALIDATOR
    operators may only confirm/veto/downgrade confidence; ADVISOR operators
    only annotate or advise (offline or low-frequency) and never enter the
    authorization path.
    """

    GENERATOR = "GENERATOR"
    VALIDATOR = "VALIDATOR"
    ADVISOR = "ADVISOR"


class ParameterType(str, Enum):
    """Declared value type of an operator parameter."""

    INTEGER = "integer"
    FLOAT = "float"
    BOOLEAN = "boolean"
    ENUM = "enum"


class SafeDirection(str, Enum):
    """Allowed adjustment direction for a VALIDATOR parameter.

    ``tighten_only`` means a rule may only move the parameter toward stricter
    values; loosening is rejected at load time (spec *"Validators cannot be
    bypassed by configuration"*).  The tightening direction is encoded as
    ``value >= default``: a tighten-only validator parameter may never be
    decreased below its declared default.  (E.g. ``minAnchors`` with default 4
    rejects ``3`` — the v1n relaxation — while accepting ``5``.)
    """

    TIGHTEN_ONLY = "tighten_only"


@dataclass(frozen=True)
class NumericBounds:
    """Inclusive numeric bounds for INTEGER/FLOAT parameters."""

    min_value: float
    max_value: float

    def __post_init__(self) -> None:
        if self.min_value > self.max_value:
            raise ValueError(
                f"numeric bounds min {self.min_value} > max {self.max_value}"
            )


@dataclass(frozen=True)
class EnumBounds:
    """Finite value set for ENUM parameters."""

    values: tuple[Any, ...]

    def __post_init__(self) -> None:
        if not self.values:
            raise ValueError("enum bounds must contain at least one value")
        if len(set(self.values)) != len(self.values):
            raise ValueError("enum bounds contain duplicate values")


BoundSpec = NumericBounds | EnumBounds


@dataclass(frozen=True)
class ParameterSpec:
    """A bounded parameter of an operator contract.

    ``violation`` returns a ``(kind, detail)`` pair for an unacceptable value
    (``None`` when the value is acceptable).  Kinds: ``type_mismatch``,
    ``out_of_bounds``, ``invalid_enum_value``.  The safe-direction check is a
    load-time linter concern (the resolver's linter applies it), not part of
    the raw value domain check here.
    """

    name: str
    type: ParameterType
    default: Any
    bounds: BoundSpec | None = None
    safe_direction: SafeDirection | None = None

    def __post_init__(self) -> None:
        if not self.name:
            raise ValueError("parameter name must be non-empty")
        if "." in self.name:
            raise ValueError(
                f"parameter name {self.name!r} must not contain '.' "
                "(the rule namespace separator)"
            )
        if self.type is ParameterType.BOOLEAN:
            if self.bounds is not None:
                raise ValueError("boolean parameters take no bounds")
            if not isinstance(self.default, bool):
                raise ValueError(
                    f"boolean parameter {self.name!r} default must be a bool"
                )
        elif self.type in (ParameterType.INTEGER, ParameterType.FLOAT):
            if not isinstance(self.bounds, NumericBounds):
                raise ValueError(
                    f"numeric parameter {self.name!r} must declare NumericBounds"
                )
            if _is_number(self.default) and not _within(self.default, self.bounds):
                raise ValueError(
                    f"default {self.default!r} out of bounds for {self.name!r}"
                )
            if self.type is ParameterType.INTEGER and not isinstance(self.default, int):
                raise ValueError(
                    f"integer parameter {self.name!r} default must be an int"
                )
        elif self.type is ParameterType.ENUM:
            if not isinstance(self.bounds, EnumBounds):
                raise ValueError(
                    f"enum parameter {self.name!r} must declare EnumBounds"
                )
            if self.default not in self.bounds.values:
                raise ValueError(
                    f"default {self.default!r} not in enum values for {self.name!r}"
                )
        if (
            self.safe_direction is not None
            and self.type not in (ParameterType.INTEGER, ParameterType.FLOAT)
        ):
            raise ValueError(
                f"safe_direction is only meaningful for numeric parameters "
                f"({self.name!r} is {self.type.value})"
            )

    def violation(self, value: Any) -> tuple[str, str] | None:
        """Return ``(kind, detail)`` if ``value`` violates this parameter's
        domain, else ``None``.  Deterministic pure function."""
        kind = "type_mismatch"
        if self.type is ParameterType.BOOLEAN:
            if not isinstance(value, bool):
                return (kind, f"expected boolean, got {type(value).__name__}")
            return None
        if self.type is ParameterType.INTEGER:
            if isinstance(value, bool) or not isinstance(value, int):
                return (kind, f"expected integer, got {type(value).__name__}")
            if not _within(value, self.bounds):
                return (
                    "out_of_bounds",
                    f"integer {value} out of bounds "
                    f"[{self.bounds.min_value:g}, {self.bounds.max_value:g}]",
                )
            return None
        if self.type is ParameterType.FLOAT:
            if isinstance(value, bool) or not _is_number(value):
                return (kind, f"expected number, got {type(value).__name__}")
            if isinstance(value, float) and not math.isfinite(value):
                return (kind, "expected finite number, got non-finite value")
            if not _within(value, self.bounds):
                return (
                    "out_of_bounds",
                    f"number {value} out of bounds "
                    f"[{self.bounds.min_value:g}, {self.bounds.max_value:g}]",
                )
            return None
        # ENUM
        if value not in self.bounds.values:
            return (
                "invalid_enum_value",
                f"value {value!r} not in enum {sorted(map(_json_scalar, self.bounds.values))!r}",
            )
        return None


def _is_number(value: Any) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool)


def _within(value: Any, bounds: NumericBounds) -> bool:
    return bounds.min_value <= value <= bounds.max_value


def _json_scalar(value: Any) -> Any:
    """Coerce a possibly non-JSON enum value (e.g. int) for display only."""
    return value


@dataclass(frozen=True)
class OperatorContract:
    """A declared composition operator (spec: *"Operator contract"*).

    GENERATOR contracts automatically carry a built-in ``enabled`` boolean
    parameter (default ``True``) so generation can be disabled by rule
    configuration; VALIDATOR contracts never declare ``enabled`` (any rule
    attempting to disable a validator fails linting at load time — spec:
    *"Validators cannot be bypassed by configuration"*).  ADVISOR contracts
    MAY declare a plain boolean ``enabled`` parameter (S4: offline
    advisors are opt-in — ``vlm-annotation`` defaults it to ``False``; VLM
    use in the authorization path is FORBIDDEN, it is offline advisory
    only); an ADVISOR without a declared ``enabled`` simply has none.
    """

    operator_id: str
    version: str
    authority: OperatorAuthority
    input_kinds: frozenset[str]
    output_kind: str
    parameters: Mapping[str, ParameterSpec]
    fail_closed_description: str

    def __post_init__(self) -> None:
        if not self.operator_id or "." in self.operator_id:
            raise ValueError(
                f"operator_id {self.operator_id!r} must be non-empty and contain no '.'"
            )
        _check_version(self.version)
        if not self.output_kind:
            raise ValueError(f"operator {self.operator_id!r} must declare an output_kind")
        parameters = dict(self.parameters)
        if self.authority is OperatorAuthority.GENERATOR:
            if "enabled" in parameters:
                raise ValueError(
                    f"operator {self.operator_id!r}: 'enabled' is a built-in "
                    "GENERATOR parameter and must not be declared explicitly"
                )
            parameters["enabled"] = ParameterSpec(
                name="enabled",
                type=ParameterType.BOOLEAN,
                default=True,
            )
        elif self.authority is OperatorAuthority.ADVISOR and "enabled" in parameters:
            _check_advisor_enabled(parameters["enabled"], self.operator_id)
        elif "enabled" in parameters:
            raise ValueError(
                f"operator {self.operator_id!r}: {self.authority.value} contracts "
                "must not declare 'enabled' (a VALIDATOR cannot be disabled by "
                "configuration)"
            )
        for name, spec in parameters.items():
            if spec.name != name:
                raise ValueError(
                    f"operator {self.operator_id!r}: parameter key {name!r} "
                    f"does not match spec name {spec.name!r}"
                )
            if (
                spec.safe_direction is not None
                and self.authority is not OperatorAuthority.VALIDATOR
            ):
                raise ValueError(
                    f"operator {self.operator_id!r}: safe_direction on "
                    f"{name!r} is only allowed on VALIDATOR parameters"
                )
        object.__setattr__(self, "parameters", parameters)

    def parameter(self, name: str) -> ParameterSpec | None:
        """Return the parameter spec (including the built-in ``enabled`` void
        parameter for GENERATORs) or ``None``."""
        return self.parameters.get(name)


def _check_version(version: str) -> None:
    if not isinstance(version, str) or not version:
        raise ValueError("operator version must be a non-empty string")
    parts = version.split(".")
    if not parts or any(not part.isdigit() for part in parts):
        raise ValueError(
            f"operator version {version!r} must be dot-separated integers "
            "(e.g. '1.0.0')"
        )


def _check_advisor_enabled(spec: ParameterSpec, operator_id: str) -> None:
    """Validate an ADVISOR-declared ``enabled`` parameter (S4).

    An ADVISOR may declare a plain boolean ``enabled`` (default ``False`` by
    convention — offline advisory is opt-in and never enters the
    authorization path).  It must be a plain boolean: no bounds, no safe
    direction (ADVISOR parameters are not validator-tightened).
    """
    if spec.type is not ParameterType.BOOLEAN:
        raise ValueError(
            f"operator {operator_id!r}: ADVISOR 'enabled' must be a BOOLEAN "
            f"parameter, got {spec.type.value}"
        )
    if spec.bounds is not None:
        raise ValueError(
            f"operator {operator_id!r}: ADVISOR 'enabled' takes no bounds"
        )
    if spec.safe_direction is not None:
        raise ValueError(
            f"operator {operator_id!r}: ADVISOR 'enabled' takes no safe_direction"
        )


def _version_key(version: str) -> tuple[int, ...]:
    return tuple(int(part) for part in version.split("."))


class OperatorRegistry:
    """Stores operator contracts; declares the code-owned pipeline order.

    S1A slice: the registry only *stores* contracts and the declared ordered
    pipeline list.  It does not wire anything into the fusion pipeline —
    wiring (S1B) will call :meth:`declare_pipeline` and consume
    :meth:`operators_for_pipeline`.
    """

    def __init__(self) -> None:
        self._contracts: dict[tuple[str, str], OperatorContract] = {}
        self._latest: dict[str, OperatorContract] = {}
        self._pipeline: tuple[str, ...] = ()

    def register(self, contract: OperatorContract) -> "OperatorRegistry":
        """Register one contract.  Duplicate (operator_id, version) is
        rejected.  Returns ``self`` for chaining."""
        key = (contract.operator_id, contract.version)
        if key in self._contracts:
            raise ValueError(
                f"duplicate operator registration: "
                f"{contract.operator_id} {contract.version}"
            )
        self._contracts[key] = contract
        current = self._latest.get(contract.operator_id)
        if current is None or _version_key(contract.version) > _version_key(
            current.version
        ):
            self._latest[contract.operator_id] = contract
        return self

    def lookup(self, operator_id: str, version: str | None = None) -> OperatorContract:
        """Look up a contract.  Without ``version``, returns the highest
        registered version of that operator (deterministic)."""
        if version is None:
            try:
                return self._latest[operator_id]
            except KeyError:
                raise KeyError(f"no registered operator {operator_id!r}") from None
        key = (operator_id, version)
        try:
            return self._contracts[key]
        except KeyError:
            raise KeyError(f"no registered operator {operator_id!r} {version!r}") from None

    def contains(self, operator_id: str) -> bool:
        return operator_id in self._latest

    def contracts(self) -> tuple[OperatorContract, ...]:
        """All registered contracts, sorted by (operator_id, version)."""
        return tuple(
            self._contracts[key]
            for key in sorted(self._contracts, key=lambda k: (k[0], _version_key(k[1])))
        )

    def effective_contracts(self) -> tuple[OperatorContract, ...]:
        """One contract per operator id (highest version), sorted by id.

        Rules bind to the highest registered version of an operator because a
        rule carries no version (``operatorId.paramName`` namespacing only).
        """
        return tuple(
            self._latest[operator_id]
            for operator_id in sorted(self._latest)
        )

    def declare_pipeline(self, ordered_ids: Sequence[str]) -> "OperatorRegistry":
        """Declare the code-owned ordered operator list.

        S1B (wiring) will call this with the fixed execution order.  Unknown
        ids and duplicates are rejected here.  This slice stores the order
        only; pipeline invariants (every GENERATOR output passes every
        applicable VALIDATOR) are enforced by S1B wiring, per spec *"Pipeline
        topology is code-owned; rules parameterize only"*.
        """
        ordered = tuple(ordered_ids)
        seen: set[str] = set()
        for operator_id in ordered:
            if not self.contains(operator_id):
                raise KeyError(
                    f"cannot declare pipeline: unregistered operator {operator_id!r}"
                )
            if operator_id in seen:
                raise ValueError(
                    f"pipeline declares operator {operator_id!r} more than once"
                )
            seen.add(operator_id)
        object.__setattr__(self, "_pipeline", ordered)
        return self

    def operators_for_pipeline(self) -> tuple[str, ...]:
        """The code-declared ordered operator ids (empty until declared)."""
        return self._pipeline