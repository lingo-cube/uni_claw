"""Selector model: five canonical dimensions + tags, matching, specificity.

Implements the spec requirement *"Selector dimensions and canonical values"*:
the selector dimensions SHALL be exactly ``system``, ``systemVersion``,
``app``, ``appVersion``, ``device`` plus supplementary ``tags`` (a
``key=value`` set).  Canonical representations are the caller's
responsibility (Adapter layer); this module provides equality semantics only.
A dimension absent in the frame context resolves to the ``DEFAULT`` sentinel
(``"default"``); rules MAY pin ``default`` to match value-absent contexts.

Matching is subset semantics (spec *"Specificity cascade ..."*): every pinned
dimension equals the context value (or ``default``) and the rule's tags are a
subset of the context tags.  Specificity equals the number of pinned
dimensions (each tag entry counts 1).  Absence of a pin means "unpinned" —
there is no wildcard literal.
"""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Iterable, Mapping

__all__ = [
    "DEFAULT",
    "SelectorDimensions",
    "FrameContext",
    "Rule",
    "matches",
    "specificity",
]

#: Sentinel value for a context dimension that is absent; a rule pinning
#: ``"default"`` explicitly matches such contexts (spec scenario:
#: "Version-less app matches a default pin").
DEFAULT = "default"

_TAG_SEPARATOR = "="


class SelectorDimensions:
    """The five canonical selector dimensions (spec: exact set)."""

    SYSTEM = "system"
    SYSTEM_VERSION = "systemVersion"
    APP = "app"
    APP_VERSION = "appVersion"
    DEVICE = "device"

    #: Canonical dimension names in declaration order.
    ALL: tuple[str, ...] = (SYSTEM, SYSTEM_VERSION, APP, APP_VERSION, DEVICE)


def _normalize_tags(tags: Iterable[str]) -> frozenset[str]:
    normalized = frozenset(tags)
    for tag in normalized:
        if not isinstance(tag, str):
            raise ValueError(f"tags must be 'key=value' strings, got {tag!r}")
    return normalized


def _require_finite(pins: Any) -> None:
    for name, value in pins.items():
        if not isinstance(value, str):
            raise ValueError(f"pin value for {name!r} must be a string, got {value!r}")


@dataclass(frozen=True)
class FrameContext:
    """Per-frame context header supplied by the Adapter layer with the
    analysis request (spec: "Context SHALL be supplied by the caller").

    A dimension passed as ``None`` (absent) is normalized to ``DEFAULT``.
    Tags are a free-form set of ``key=value`` strings; tag keys are not
    required to be unique.
    """

    system: str | None = DEFAULT
    system_version: str | None = DEFAULT
    app: str | None = DEFAULT
    app_version: str | None = DEFAULT
    device: str | None = DEFAULT
    tags: frozenset[str] = field(default_factory=frozenset)

    def __post_init__(self) -> None:
        names = ("system", "system_version", "app", "app_version", "device")
        for name in names:
            value = getattr(self, name)
            if value is None:
                object.__setattr__(self, name, DEFAULT)
            elif not isinstance(value, str):
                raise ValueError(
                    f"context dimension {name!r} must be a string, got {value!r}"
                )
        object.__setattr__(self, "tags", _normalize_tags(self.tags))

    @classmethod
    def from_mapping(
        cls, mapping: Mapping[str, str | None], tags: Iterable[str] = ()
    ) -> "FrameContext":
        """Build a context from a mapping keyed by the canonical dimension
        names; missing keys resolve to ``DEFAULT``."""
        return cls(
            system=mapping.get(SelectorDimensions.SYSTEM),
            system_version=mapping.get(SelectorDimensions.SYSTEM_VERSION),
            app=mapping.get(SelectorDimensions.APP),
            app_version=mapping.get(SelectorDimensions.APP_VERSION),
            device=mapping.get(SelectorDimensions.DEVICE),
            tags=tags,
        )

    def dim_value(self, dim: str) -> str:
        """Context value for a canonical dimension (missing → ``DEFAULT``).
        Raises ``ValueError`` for non-canonical dimensions."""
        if dim not in SelectorDimensions.ALL:
            raise ValueError(f"unknown selector dimension {dim!r}")
        return getattr(
            self,
            {
                SelectorDimensions.SYSTEM: "system",
                SelectorDimensions.SYSTEM_VERSION: "system_version",
                SelectorDimensions.APP: "app",
                SelectorDimensions.APP_VERSION: "app_version",
                SelectorDimensions.DEVICE: "device",
            }[dim],
        )

    def to_mapping(self) -> dict[str, str]:
        """Canonical-dimension mapping of this context (for provenance and
        display)."""
        return {dim: self.dim_value(dim) for dim in SelectorDimensions.ALL}


@dataclass(frozen=True)
class Rule:
    """A cascading rule: selector pins (dim → concrete value, no wildcards;
    absence = unpinned), tag pins, and parameter overrides namespaced per
    operator (``operatorId.paramName``).

    Construction validates structural shape only (string types, param-key
    syntax).  Semantic validation — canonical pin dimensions, parameter
    presence, value bounds, conflict detection — is the resolver linter's
    job (spec *"Governed, diffable rule assets"*).
    """

    rule_id: str
    pins: dict[str, str] = field(default_factory=dict)
    tags_pins: frozenset[str] = field(default_factory=frozenset)
    params: dict[str, Any] = field(default_factory=dict)

    def __post_init__(self) -> None:
        if not self.rule_id:
            raise ValueError("rule_id must be non-empty")
        if not isinstance(self.pins, dict) or not isinstance(self.params, dict):
            raise ValueError("pins and params must be dicts")
        object.__setattr__(self, "pins", dict(self.pins))
        object.__setattr__(self, "params", dict(self.params))
        _require_finite(self.pins)
        object.__setattr__(self, "tags_pins", _normalize_tags(self.tags_pins))
        for key in self.params:
            parts = key.split(".", maxsplit=1)
            if len(parts) != 2 or not parts[0] or not parts[1]:
                raise ValueError(
                    f"rule {self.rule_id!r}: param key {key!r} must be "
                    "'operatorId.paramName'"
                )


def matches(rule: Rule, context: FrameContext) -> bool:
    """True iff ``rule`` matches ``context``.

    Every pinned dimension must equal the context value (or ``default``) and
    the rule's tags must be a subset of the context tags.  A rule pinning a
    non-canonical dimension never matches (such a pin is additionally a lint
    error).  Pure, order-independent.
    """
    for dim, value in rule.pins.items():
        if dim not in SelectorDimensions.ALL:
            return False
        if context.dim_value(dim) != value:
            return False
    return rule.tags_pins.issubset(context.tags)


def specificity(rule: Rule) -> int:
    """Rule specificity: number of pinned dimensions (each tag entry counts 1)."""
    return len(rule.pins) + len(rule.tags_pins)