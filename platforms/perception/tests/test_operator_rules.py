"""Framework-core tests for perception Operator & Cascading-Rule framework.

Covers the S1.1–S1.4 slice of OpenSpec change
``perception-operator-rule-framework``: operator contracts/registry, five-
dimension + tags selector model with ``default`` semantics, specificity-cascade
resolution with provenance, intersection-scoped equal-specificity conflict
detection (the four normative scenarios), deterministic serialization, the
loader/linter rejection list, and seeded property-style order-independence.
"""
from __future__ import annotations

import random
import unittest

from uniclaw_perception.operators import (
    DEFAULT,
    DIAG_COMPLEXITY_BUDGET,
    DIAG_DEAD_RULE,
    DIAG_DUPLICATE_RULE_ID,
    DIAG_INVALID_ENUM_VALUE,
    DIAG_INVALID_SELECTOR,
    DIAG_OUT_OF_BOUNDS,
    DIAG_SPECIFICITY_CONFLICT,
    DIAG_UNKNOWN_PARAMETER,
    DIAG_UNSAFE_VALIDATOR_ADJUSTMENT,
    DIAG_VALIDATOR_DISABLE,
    EnumBounds,
    FrameContext,
    NumericBounds,
    OperatorAuthority,
    OperatorContract,
    OperatorRegistry,
    ParameterSpec,
    ParameterType,
    ResolutionConflictError,
    Rule,
    SafeDirection,
    deserialize_rule_set,
    lint_rule_set,
    load_rule_set,
    matches,
    resolve,
    serialize_rule_set,
    specificity,
)


def _context(**kwargs) -> FrameContext:
    """Build a FrameContext from canonical-dimension kwargs plus ``tags``."""
    tags = kwargs.pop("tags", ())
    return FrameContext.from_mapping(kwargs, tags=tags)


def _build_registry() -> OperatorRegistry:
    """Test registry mirroring the S1 worked example around
    ``uniform-list-row-grouping`` (root defaults = the retained candidate
    constants from fusion/row_grouping.py: min anchors 4, inference cap 0.50,
    continuation cap 0.30)."""
    registry = OperatorRegistry()
    registry.register(
        OperatorContract(
            operator_id="uniform-list-row-grouping",
            version="1.0.0",
            authority=OperatorAuthority.GENERATOR,
            input_kinds=frozenset({"detection", "ocr"}),
            output_kind="row_group",
            parameters={
                "minAnchors": ParameterSpec(
                    "minAnchors",
                    ParameterType.INTEGER,
                    4,
                    NumericBounds(1, 8),
                ),
                "inferenceCap": ParameterSpec(
                    "inferenceCap",
                    ParameterType.FLOAT,
                    0.50,
                    NumericBounds(0.0, 1.0),
                ),
                "continuationCap": ParameterSpec(
                    "continuationCap",
                    ParameterType.FLOAT,
                    0.30,
                    NumericBounds(0.0, 1.0),
                ),
            },
            fail_closed_description=(
                "activates only from four or more confirmed anchors; "
                "never guesses a row"
            ),
        )
    )
    registry.register(
        OperatorContract(
            operator_id="spacing-verifier",
            version="1.0.0",
            authority=OperatorAuthority.VALIDATOR,
            input_kinds=frozenset({"row_group"}),
            output_kind="verdict",
            parameters={
                "minPitchRatio": ParameterSpec(
                    "minPitchRatio",
                    ParameterType.FLOAT,
                    0.5,
                    NumericBounds(0.1, 1.0),
                    SafeDirection.TIGHTEN_ONLY,
                ),
            },
            fail_closed_description=(
                "vetoes any proposed row whose spacing is not verified"
            ),
        )
    )
    registry.register(
        OperatorContract(
            operator_id="text-relation-check",
            version="1.0.0",
            authority=OperatorAuthority.VALIDATOR,
            input_kinds=frozenset({"row_group", "ocr"}),
            output_kind="verdict",
            parameters={},
            fail_closed_description=(
                "text semantics only veto or downgrade confidence; never "
                "fabricates candidates"
            ),
        )
    )
    registry.register(
        OperatorContract(
            operator_id="vlm-annotation",
            version="1.0.0",
            authority=OperatorAuthority.ADVISOR,
            input_kinds=frozenset({"row_group"}),
            output_kind="annotation",
            parameters={
                "minConfidence": ParameterSpec(
                    "minConfidence",
                    ParameterType.FLOAT,
                    0.7,
                    NumericBounds(0.0, 1.0),
                ),
            },
            fail_closed_description="offline or low-frequency advisory only",
        )
    )
    registry.register(
        OperatorContract(
            operator_id="structured-corroboration",
            version="1.0.0",
            authority=OperatorAuthority.ADVISOR,
            input_kinds=frozenset({"row_group", "xml"}),
            output_kind="annotation",
            parameters={
                "mode": ParameterSpec(
                    "mode",
                    ParameterType.ENUM,
                    "passive",
                    EnumBounds(("passive", "active")),
                ),
            },
            fail_closed_description="XML is auxiliary corroboration only",
        )
    )
    return registry


def _rule(rule_id: str, pins=None, tags=(), params=None) -> Rule:
    return Rule(
        rule_id=rule_id,
        pins=dict(pins or {}),
        tags_pins=frozenset(tags),
        params=dict(params or {}),
    )


def _context(**kwargs) -> FrameContext:
    """Build a FrameContext from canonical-dimension kwargs plus ``tags``."""
    tags = kwargs.pop("tags", ())
    return FrameContext.from_mapping(kwargs, tags=tags)


def _kinds(diagnostics, *kinds) -> list:
    want = set(kinds)
    return [d for d in diagnostics if d.kind in want]


class OperatorContractTests(unittest.TestCase):
    def test_duplicate_registration_rejected(self):
        registry = _build_registry()
        duplicate = OperatorContract(
            operator_id="uniform-list-row-grouping",
            version="1.0.0",
            authority=OperatorAuthority.GENERATOR,
            input_kinds=frozenset(),
            output_kind="row_group",
            parameters={},
            fail_closed_description="duplicate",
        )
        with self.assertRaises(ValueError):
            registry.register(duplicate)

    def test_lookup_latest_version_and_exact(self):
        registry = OperatorRegistry()
        base = OperatorContract(
            operator_id="sample-oper",
            version="1.0.0",
            authority=OperatorAuthority.ADVISOR,
            input_kinds=frozenset({"detection"}),
            output_kind="annotation",
            parameters={},
            fail_closed_description="desc",
        )
        newer = OperatorContract(
            operator_id="sample-oper",
            version="1.2.0",
            authority=OperatorAuthority.ADVISOR,
            input_kinds=frozenset({"detection"}),
            output_kind="annotation",
            parameters={},
            fail_closed_description="desc",
        )
        registry.register(base).register(newer)
        self.assertEqual(registry.lookup("sample-oper"), newer)
        self.assertEqual(registry.lookup("sample-oper", "1.0.0"), base)
        with self.assertRaises(KeyError):
            registry.lookup("missing")

    def test_generator_has_builtin_enabled_param(self):
        registry = _build_registry()
        contract = registry.lookup("uniform-list-row-grouping")
        self.assertIn("enabled", contract.parameters)
        self.assertIs(contract.parameter("enabled").type, ParameterType.BOOLEAN)
        self.assertIs(contract.parameter("enabled").default, True)
        # GENERATOR disable is legal configuration:
        rules = [_rule("r1", {"system": "android"}, params={"uniform-list-row-grouping.enabled": False})]
        self.assertEqual(lint_rule_set(rules, registry), [])
        resolved = resolve(rules, _context(system="android"), registry)
        enabled = [r for r in resolved if r.operator_id == "uniform-list-row-grouping"][0]
        self.assertIs(enabled.values["enabled"], False)

    def test_non_generator_has_no_enabled_param(self):
        registry = _build_registry()
        for operator_id in ("spacing-verifier", "text-relation-check", "vlm-annotation"):
            self.assertNotIn("enabled", registry.lookup(operator_id).parameters)

    def test_explicit_enabled_rejected_on_contracts(self):
        with self.assertRaises(ValueError):
            OperatorContract(
                operator_id="g",
                version="1.0.0",
                authority=OperatorAuthority.GENERATOR,
                input_kinds=frozenset(),
                output_kind="k",
                parameters={
                    "enabled": ParameterSpec("enabled", ParameterType.BOOLEAN, True),
                },
                fail_closed_description="d",
            )
        with self.assertRaises(ValueError):
            OperatorContract(
                operator_id="v",
                version="1.0.0",
                authority=OperatorAuthority.VALIDATOR,
                input_kinds=frozenset(),
                output_kind="k",
                parameters={
                    "enabled": ParameterSpec("enabled", ParameterType.BOOLEAN, True),
                },
                fail_closed_description="d",
            )

    def test_safe_direction_confined_to_validator_numeric_params(self):
        with self.assertRaises(ValueError):
            ParameterSpec(
                "x", ParameterType.BOOLEAN, True, safe_direction=SafeDirection.TIGHTEN_ONLY
            )
        with self.assertRaises(ValueError):
            OperatorContract(
                operator_id="g",
                version="1.0.0",
                authority=OperatorAuthority.GENERATOR,
                input_kinds=frozenset(),
                output_kind="k",
                parameters={
                    "x": ParameterSpec(
                        "x",
                        ParameterType.INTEGER,
                        3,
                        NumericBounds(1, 5),
                        SafeDirection.TIGHTEN_ONLY,
                    ),
                },
                fail_closed_description="d",
            )

    def test_declare_pipeline(self):
        registry = _build_registry()
        registry.declare_pipeline(
            ["uniform-list-row-grouping", "spacing-verifier", "text-relation-check"]
        )
        self.assertEqual(
            registry.operators_for_pipeline(),
            ("uniform-list-row-grouping", "spacing-verifier", "text-relation-check"),
        )
        with self.assertRaises(KeyError):
            registry.declare_pipeline(["uniform-list-row-grouping", "ghost"])
        with self.assertRaises(ValueError):
            registry.declare_pipeline(["spacing-verifier", "spacing-verifier"])


class SelectorMatchTests(unittest.TestCase):
    def test_five_dimension_equality(self):
        rule = _rule(
            "all-dims",
            {
                "system": "android",
                "systemVersion": "api-35",
                "app": "com.android.settings",
                "appVersion": "15",
                "device": "Pixel 7",
            },
        )
        self.assertTrue(matches(rule, _context(
            system="android", systemVersion="api-35",
            app="com.android.settings", appVersion="15", device="Pixel 7",
        )))
        self.assertFalse(matches(rule, _context(
            system="android", systemVersion="api-35",
            app="com.android.settings", appVersion="15", device="Pixel 8",
        )))

    def test_missing_dim_resolves_to_default(self):
        # Context without appVersion -> appVersion=DEFAULT; pin "default" matches,
        # concrete pin does not, unpinned matches.
        rule_default = _rule("d", {"appVersion": DEFAULT})
        rule_concrete = _rule("c", {"appVersion": "15"})
        rule_unpinned = _rule("u", {"system": "android"})
        context = _context(system="android", app="com.android.settings")
        self.assertEqual(context.dim_value("appVersion"), DEFAULT)
        self.assertTrue(matches(rule_default, context))
        self.assertFalse(matches(rule_concrete, context))
        self.assertTrue(matches(rule_unpinned, context))
        # Concrete context breaks the default pin (mutual exclusivity).
        context_with_version = _context(appVersion="15")
        self.assertFalse(matches(rule_default, context_with_version))
        self.assertTrue(matches(rule_concrete, context_with_version))

    def test_tags_subset_semantics(self):
        rule = _rule("t", {"system": "android"}, tags=("display=triple-screen",))
        self.assertTrue(matches(rule, _context(
            system="android", tags=("display=triple-screen", "locale=en")
        )))
        self.assertFalse(matches(rule, _context(system="android", tags=("locale=en",))))
        self.assertTrue(matches(_rule("none", {"system": "android"}), _context(
            system="android", tags=("display=triple-screen",)
        )))

    def test_specificity_counts_pins_and_tags(self):
        self.assertEqual(specificity(_rule("r")), 0)
        self.assertEqual(specificity(_rule("r", {"system": "android"})), 1)
        self.assertEqual(
            specificity(_rule("r", {"system": "android", "app": "pkg"}, tags=("a=1", "b=2"))),
            4,
        )


class SpecificityResolutionTests(unittest.TestCase):
    def test_higher_pin_count_wins(self):
        registry = _build_registry()
        rules = [
            _rule("system-default", {"system": "android"},
                  params={"uniform-list-row-grouping.minAnchors": 4}),
            _rule("version-override", {"system": "android", "systemVersion": "api-35"},
                  params={"uniform-list-row-grouping.minAnchors": 5}),
        ]
        self.assertEqual(lint_rule_set(rules, registry), [])
        resolved = resolve(rules, _context(system="android", systemVersion="api-35"), registry)
        target = [r for r in resolved if r.operator_id == "uniform-list-row-grouping"][0]
        self.assertEqual(target.values["minAnchors"], 5)
        provenance = target.provenance["minAnchors"]
        self.assertEqual(provenance.rule_id, "version-override")
        self.assertEqual(provenance.specificity, 2)
        self.assertEqual(provenance.pins["systemVersion"], "api-35")
        # Different version -> the system-only rule applies.
        resolved_other = resolve(
            rules, _context(system="android", systemVersion="api-34"), registry
        )
        target_other = [r for r in resolved_other if r.operator_id == "uniform-list-row-grouping"][0]
        self.assertEqual(target_other.values["minAnchors"], 4)
        self.assertEqual(target_other.provenance["minAnchors"].rule_id, "system-default")

    def test_tags_break_specificity_tie(self):
        registry = _build_registry()
        rules = [
            _rule("plain", {"system": "android"},
                  params={"uniform-list-row-grouping.minAnchors": 4}),
            _rule("triple-screen", {"system": "android"}, tags=("display=triple-screen",),
                  params={"uniform-list-row-grouping.minAnchors": 6}),
        ]
        target = lambda ctx: [r for r in resolve(rules, ctx, registry)
                              if r.operator_id == "uniform-list-row-grouping"][0]
        self.assertEqual(target(_context(system="android", tags=("display=triple-screen",))).values["minAnchors"], 6)
        self.assertEqual(target(_context(system="android")).values["minAnchors"], 4)
        self.assertEqual(
            target(_context(system="android")).provenance["minAnchors"].rule_id,
            "plain",
        )

    def test_default_values_carry_contract_provenance(self):
        registry = _build_registry()
        resolved = resolve([], _context(system="android"), registry)
        target = [r for r in resolved if r.operator_id == "uniform-list-row-grouping"][0]
        self.assertEqual(target.values["minAnchors"], 4)
        self.assertIsNone(target.provenance["minAnchors"].rule_id)
        self.assertEqual(target.provenance["minAnchors"].specificity, 0)


class ConflictDetectionTests(unittest.TestCase):
    """The four normative scenarios of spec *"Specificity cascade with
    intersection-scoped conflict detection"*."""

    def test_scenario_a_version_override_beats_system_default(self):
        registry = _build_registry()
        rules = [
            _rule("system-default", {"system": "android"},
                  params={"uniform-list-row-grouping.minAnchors": 4}),
            _rule("version-override", {"system": "android", "systemVersion": "api-35"},
                  params={"uniform-list-row-grouping.minAnchors": 5}),
        ]
        self.assertEqual(lint_rule_set(rules, registry), [])
        resolved = resolve(rules, _context(system="android", systemVersion="api-35"), registry)
        target = [r for r in resolved if r.operator_id == "uniform-list-row-grouping"][0]
        self.assertEqual(target.values["minAnchors"], 5)
        self.assertEqual(target.provenance["minAnchors"].rule_id, "version-override")

    def test_scenario_b_mutually_exclusive_rules_are_not_conflict(self):
        registry = _build_registry()
        rules = [
            _rule("settings-app", {"app": "com.android.settings"},
                  params={"uniform-list-row-grouping.minAnchors": 4}),
            _rule("other-app", {"app": "com.example.other"},
                  params={"uniform-list-row-grouping.minAnchors": 5}),
        ]
        self.assertEqual(lint_rule_set(rules, registry), [])

    def test_scenario_c_uncovered_equal_specificity_clash_is_load_error(self):
        registry = _build_registry()
        rules = [
            _rule("system-version", {"system": "android", "systemVersion": "api-35"},
                  params={"uniform-list-row-grouping.minAnchors": 4}),
            _rule("system-app", {"system": "android", "app": "com.android.settings"},
                  params={"uniform-list-row-grouping.minAnchors": 5}),
        ]
        diagnostics = lint_rule_set(rules, registry)
        conflicts = _kinds(diagnostics, DIAG_SPECIFICITY_CONFLICT)
        self.assertEqual(len(conflicts), 1)
        message = conflicts[0].message
        self.assertIn("system-version", message)
        self.assertIn("system-app", message)
        self.assertIn("system=android", message)
        self.assertIn("systemVersion=api-35", message)
        self.assertIn("app=com.android.settings", message)
        self.assertEqual(conflicts[0].rule_ids, frozenset({"system-version", "system-app"}))
        loaded = load_rule_set(serialize_rule_set(rules), registry)
        self.assertFalse(loaded.is_valid)

    def test_scenario_c_fail_closed_resolution_on_intersection_context(self):
        registry = _build_registry()
        rules = [
            _rule("system-version", {"system": "android", "systemVersion": "api-35"},
                  params={"uniform-list-row-grouping.minAnchors": 4}),
            _rule("system-app", {"system": "android", "app": "com.android.settings"},
                  params={"uniform-list-row-grouping.minAnchors": 5}),
        ]
        with self.assertRaises(ResolutionConflictError):
            resolve(rules, _context(
                system="android", systemVersion="api-35", app="com.android.settings"
            ), registry)
        # A context outside the intersection resolves deterministically.
        resolved = resolve(rules, _context(system="ios"), registry)
        target = [r for r in resolved if r.operator_id == "uniform-list-row-grouping"][0]
        self.assertEqual(target.values["minAnchors"], 4)  # contract default

    def test_scenario_d_intersection_covered_by_higher_specificity_rule(self):
        registry = _build_registry()
        rules = [
            _rule("system-version", {"system": "android", "systemVersion": "api-35"},
                  params={"uniform-list-row-grouping.minAnchors": 4}),
            _rule("system-app", {"system": "android", "app": "com.android.settings"},
                  params={"uniform-list-row-grouping.minAnchors": 5}),
            _rule("intersection", {"system": "android", "systemVersion": "api-35",
                                   "app": "com.android.settings"},
                  params={"uniform-list-row-grouping.minAnchors": 6}),
        ]
        self.assertEqual(lint_rule_set(rules, registry), [])
        resolved = resolve(rules, _context(
            system="android", systemVersion="api-35", app="com.android.settings"
        ), registry)
        target = [r for r in resolved if r.operator_id == "uniform-list-row-grouping"][0]
        self.assertEqual(target.values["minAnchors"], 6)
        self.assertEqual(target.provenance["minAnchors"].rule_id, "intersection")


class OrderIndependenceTests(unittest.TestCase):
    def test_lint_result_independent_of_rule_order(self):
        registry = _build_registry()
        rules = [
            _rule("r1", {"system": "android"},
                  params={"uniform-list-row-grouping.minAnchors": 4}),
            _rule("r2", {"system": "android", "systemVersion": "api-35"},
                  params={"uniform-list-row-grouping.minAnchors": 5}),
            _rule("r3", {"system": "ios"},
                  params={"spacing-verifier.minPitchRatio": 0.3}),
            _rule("r4", {"app": "pkg.a"}, tags=("display=triple-screen",),
                  params={"uniform-list-row-grouping.minAnchors": 6}),
            _rule("r5", {"app": "pkg.b"},
                  params={"uniform-list-row-grouping.minAnchors": 7}),
            _rule("r6", {"color": "red"},
                  params={"uniform-list-row-grouping.minAnchors": 4}),
            _rule("r7", {"system": "android", "app": "pkg.a"},
                  params={"vlm-annotation.minConfidence": 0.4}),
            _rule("r8", {"system": "android", "app": "pkg.a"},
                  params={"vlm-annotation.minConfidence": 0.9}),
        ]
        baseline = lint_rule_set(rules, registry)
        self.assertTrue(baseline)  # invalid selector + unsafe + conflict diagnostics exist
        for seed in range(10):
            shuffled = rules[:]
            random.Random(seed).shuffle(shuffled)
            self.assertEqual(lint_rule_set(shuffled, registry), baseline)

    def test_resolution_independent_of_rule_order(self):
        registry = _build_registry()
        rules = [
            _rule("r1", {"system": "android"},
                  params={"uniform-list-row-grouping.minAnchors": 4}),
            _rule("r2", {"system": "android", "systemVersion": "api-35"},
                  params={"uniform-list-row-grouping.minAnchors": 5}),
            _rule("r3", {"system": "android"}, tags=("display=triple-screen",),
                  params={"uniform-list-row-grouping.inferenceCap": 0.25}),
            _rule("r4", {"system": "ios"},
                  params={"spacing-verifier.minPitchRatio": 0.8}),
        ]
        self.assertEqual(lint_rule_set(rules, registry), [])
        context = _context(system="android", systemVersion="api-35", tags=("display=triple-screen",))
        baseline = resolve(rules, context, registry)
        for seed in range(10):
            shuffled = rules[:]
            random.Random(seed).shuffle(shuffled)
            self.assertEqual(resolve(shuffled, context, registry), baseline)


class DeterminismTests(unittest.TestCase):
    def test_serialize_deserialize_round_trip_byte_stable(self):
        rules = [
            _rule("z-last", {"app": "pkg.z"}, tags=("label=设置",),
                  params={"uniform-list-row-grouping.minAnchors": 5}),
            _rule("a-first", {"system": "android"},
                  params={"uniform-list-row-grouping.minAnchors": 4,
                          "vlm-annotation.minConfidence": 0.9}),
            _rule("m-mid", {"system": "ios"}, tags=("display=triple-screen",),
                  params={"spacing-verifier.minPitchRatio": 0.6}),
        ]
        text = serialize_rule_set(rules)
        self.assertTrue(text.startswith('{\n  "schemaVersion": 1,'))
        self.assertFalse(text.encode("utf-8").startswith(b"\xef\xbb\xbf"))  # no BOM
        self.assertNotIn("timestamp", text)
        parsed = deserialize_rule_set(text)
        self.assertEqual(
            sorted(parsed, key=lambda rule: rule.rule_id),
            sorted(rules, key=lambda rule: rule.rule_id),
        )
        self.assertEqual(serialize_rule_set(parsed), text)
        # Rules appear sorted by ruleId in the document regardless of input order.
        document_order = [
            rule["ruleId"] for rule in __import__("json").loads(text)["rules"]
        ]
        self.assertEqual(document_order, ["a-first", "m-mid", "z-last"])

    def test_round_trip_byte_stable_across_shuffles(self):
        registry = _build_registry()
        rules = [
            _rule("r%d" % i, {"system": ["android", "ios"][i % 2]},
                  params={"uniform-list-row-grouping.minAnchors": 2 + (i % 5)})
            for i in range(8)
        ]
        baseline = serialize_rule_set(rules)
        for seed in range(5):
            shuffled = rules[:]
            random.Random(seed).shuffle(shuffled)
            self.assertEqual(serialize_rule_set(shuffled), baseline)

    def test_deserialize_strict_unknown_fields_rejected(self):
        self.assertRaises(ValueError, deserialize_rule_set, '{"schemaVersion": 1}')
        self.assertRaises(
            ValueError, deserialize_rule_set,
            '{"schemaVersion": 1, "rules": [], "mystery": 1}',
        )
        self.assertRaises(
            ValueError, deserialize_rule_set,
            '{"schemaVersion": 2, "rules": []}',
        )
        self.assertRaises(
            ValueError, deserialize_rule_set,
            '{"schemaVersion": 1, "rules": [{"ruleId": "r", "extra": 1}]}',
        )
        self.assertRaises(
            ValueError, deserialize_rule_set,
            '{"schemaVersion": 1, "rules": [{"ruleId": "r", "pins": {"system": 5}}]}',
        )


class ValidatorSafetyTests(unittest.TestCase):
    def test_tighten_only_below_default_rejected(self):
        registry = _build_registry()
        rules = [_rule("loose", {"system": "android"},
                       params={"spacing-verifier.minPitchRatio": 0.4})]
        diagnostics = lint_rule_set(rules, registry)
        unsafe = _kinds(diagnostics, DIAG_UNSAFE_VALIDATOR_ADJUSTMENT)
        self.assertEqual(len(unsafe), 1)
        self.assertIn("minPitchRatio", unsafe[0].message)

    def test_tighten_only_at_or_above_default_accepted(self):
        registry = _build_registry()
        for value in (0.5, 0.7, 1.0):
            rules = [_rule("ok", {"system": "android"},
                           params={"spacing-verifier.minPitchRatio": value})]
            self.assertEqual(lint_rule_set(rules, registry), [])

    def test_generator_param_below_default_allowed(self):
        registry = _build_registry()
        rules = [_rule("gen-looser", {"system": "android"},
                       params={"uniform-list-row-grouping.minAnchors": 3})]
        self.assertEqual(lint_rule_set(rules, registry), [])

    def test_validator_disable_rejected(self):
        registry = _build_registry()
        rules = [_rule("disable-v", {"system": "android"},
                       params={"text-relation-check.enabled": False})]
        diagnostics = lint_rule_set(rules, registry)
        self.assertEqual(len(_kinds(diagnostics, DIAG_VALIDATOR_DISABLE)), 1)
        advisor = [_rule("disable-a", {"system": "android"},
                         params={"vlm-annotation.enabled": False})]
        self.assertEqual(len(_kinds(lint_rule_set(advisor, registry), DIAG_VALIDATOR_DISABLE)), 1)


class LintTests(unittest.TestCase):
    def test_unknown_parameter_rejected(self):
        registry = _build_registry()
        rules = [_rule("bad", {"system": "android"},
                       params={"uniform-list-row-grouping.nope": 4})]
        diagnostics = lint_rule_set(rules, registry)
        self.assertEqual(len(_kinds(diagnostics, DIAG_UNKNOWN_PARAMETER)), 1)

    def test_out_of_bounds_rejected(self):
        registry = _build_registry()
        too_low = [_rule("low", params={"uniform-list-row-grouping.minAnchors": 0})]
        self.assertEqual(len(_kinds(lint_rule_set(too_low, registry), DIAG_OUT_OF_BOUNDS)), 1)
        too_high = [_rule("high", params={"uniform-list-row-grouping.minAnchors": 9})]
        self.assertEqual(len(_kinds(lint_rule_set(too_high, registry), DIAG_OUT_OF_BOUNDS)), 1)
        wrong_type = [_rule("type", params={"uniform-list-row-grouping.minAnchors": "four"})]
        self.assertEqual(len(_kinds(lint_rule_set(wrong_type, registry), DIAG_OUT_OF_BOUNDS)), 1)

    def test_enum_violation_rejected(self):
        registry = _build_registry()
        rules = [_rule("mode", params={"structured-corroboration.mode": "extreme"})]
        diagnostics = lint_rule_set(rules, registry)
        self.assertEqual(len(_kinds(diagnostics, DIAG_INVALID_ENUM_VALUE)), 1)

    def test_enum_valid_value_accepted(self):
        registry = _build_registry()
        rules = [_rule("mode", params={"structured-corroboration.mode": "active"})]
        self.assertEqual(lint_rule_set(rules, registry), [])

    def test_dead_rule_unregistered_operator_rejected(self):
        registry = _build_registry()
        rules = [_rule("ghost", {"system": "android"},
                       params={"no-such-operator.minAnchors": 4})]
        diagnostics = lint_rule_set(rules, registry)
        self.assertEqual(len(_kinds(diagnostics, DIAG_DEAD_RULE)), 1)

    def test_invalid_selector_dimension_rejected(self):
        registry = _build_registry()
        rules = [_rule("bad-dim", {"color": "red"},
                       params={"uniform-list-row-grouping.minAnchors": 4})]
        diagnostics = lint_rule_set(rules, registry)
        self.assertEqual(len(_kinds(diagnostics, DIAG_INVALID_SELECTOR)), 1)
        # Such a rule never matches a real context.
        self.assertFalse(matches(rules[0], _context(system="android")))

    def test_malformed_tag_rejected(self):
        registry = _build_registry()
        rules = [_rule("bad-tag", {"system": "android"}, tags=("nokeyvalue",),
                       params={"uniform-list-row-grouping.minAnchors": 4})]
        diagnostics = lint_rule_set(rules, registry)
        self.assertEqual(len(_kinds(diagnostics, DIAG_INVALID_SELECTOR)), 1)

    def test_duplicate_rule_id_rejected(self):
        registry = _build_registry()
        rules = [
            _rule("dup", {"system": "android"},
                  params={"uniform-list-row-grouping.minAnchors": 4}),
            _rule("dup", {"system": "ios"},
                  params={"uniform-list-row-grouping.minAnchors": 5}),
        ]
        diagnostics = lint_rule_set(rules, registry)
        self.assertEqual(len(_kinds(diagnostics, DIAG_DUPLICATE_RULE_ID)), 1)

    def test_complexity_budget_overrun_rejected(self):
        registry = _build_registry()
        rules = [
            # Root rule (zero pins) holds defaults and never counts.
            _rule("root", params={"uniform-list-row-grouping.minAnchors": 4}),
            _rule("n1", {"system": "android"},
                  params={"uniform-list-row-grouping.minAnchors": 5}),
            _rule("n2", {"system": "android", "systemVersion": "api-35"},
                  params={"uniform-list-row-grouping.inferenceCap": 0.6}),
            _rule("n3", {"device": "Pixel 7"},
                  params={"uniform-list-row-grouping.continuationCap": 0.25}),
        ]
        diagnostics = lint_rule_set(rules, registry, complexity_budget=2)
        overruns = _kinds(diagnostics, DIAG_COMPLEXITY_BUDGET)
        self.assertEqual(len(overruns), 1)
        self.assertIn("uniform-list-row-grouping", overruns[0].message)
        # Default budget (32) is not reached.
        self.assertEqual(lint_rule_set(rules, registry), [])

    def test_invalid_param_key_rejected_at_construction(self):
        with self.assertRaises(ValueError):
            _rule("bad-key", params={"no-dot-here": 1})


class LoaderTests(unittest.TestCase):
    def test_load_parses_and_lints(self):
        registry = _build_registry()
        text = serialize_rule_set([
            _rule("ok", {"system": "android"},
                  params={"uniform-list-row-grouping.minAnchors": 5}),
            _rule("conflict-a", {"system": "android", "systemVersion": "api-35"},
                  params={"uniform-list-row-grouping.minAnchors": 4}),
            _rule("conflict-b", {"system": "android", "app": "com.android.settings"},
                  params={"uniform-list-row-grouping.minAnchors": 5}),
        ])
        loaded = load_rule_set(text, registry)
        self.assertEqual(len(loaded.rules), 3)
        self.assertFalse(loaded.is_valid)
        self.assertEqual(
            len(_kinds(list(loaded.diagnostics), DIAG_SPECIFICITY_CONFLICT)), 1
        )

    def test_load_strict_rejects_unknown_fields(self):
        registry = _build_registry()
        with self.assertRaises(ValueError):
            load_rule_set('{"schemaVersion": 1, "rules": [], "junk": 0}', registry)


class PropertyTests(unittest.TestCase):
    """Seeded property-style checks: random rule sets must lint and resolve
    identically across every permutation of the input list."""

    _DIM_POOL = ["system", "systemVersion", "app", "device"]
    _VALUE_POOL = {
        "system": ["android", "ios"],
        "systemVersion": ["api-34", "api-35"],
        "app": ["pkg.a", "pkg.b"],
        "device": ["Pixel 7", "Pixel 8"],
    }
    _TAG_POOL = ["display=triple-screen", "locale=en"]
    _PARAM_POOL = [
        ("uniform-list-row-grouping.minAnchors", [2, 3, 4, 5, 6]),
        ("uniform-list-row-grouping.inferenceCap", [0.3, 0.5, 0.7]),
        ("spacing-verifier.minPitchRatio", [0.5, 0.6, 0.8]),
        ("vlm-annotation.minConfidence", [0.6, 0.7, 0.9]),
    ]

    def _random_rule(self, rng: random.Random, index: int) -> Rule:
        pins = {}
        for dim in rng.sample(self._DIM_POOL, rng.randint(0, 2)):
            pins[dim] = rng.choice(self._VALUE_POOL[dim])
        tags = rng.sample(self._TAG_POOL, rng.randint(0, 2))
        params = {}
        for key, choices in rng.sample(self._PARAM_POOL, rng.randint(1, 2)):
            params[key] = rng.choice(choices)
        return _rule(f"r{index}", pins, tags, params)

    def _random_context(self, rng: random.Random, rules) -> FrameContext:
        if rules and rng.random() < 0.5:
            seed_rule = rng.choice(rules)
            dims = dict(seed_rule.pins)
            for dim in rng.sample(self._DIM_POOL, rng.randint(0, 1)):
                dims.setdefault(dim, rng.choice(self._VALUE_POOL[dim]))
            tags = set(seed_rule.tags_pins)
            tags.update(t for t in self._TAG_POOL if rng.random() < 0.4)
            return FrameContext.from_mapping(dims, tags=tags)
        dims = {d: rng.choice(v) for d, v in self._VALUE_POOL.items() if rng.random() < 0.8}
        tags = [t for t in self._TAG_POOL if rng.random() < 0.5]
        return FrameContext.from_mapping(dims, tags=tags)

    def test_permutation_consistency_across_random_rule_sets(self):
        registry = _build_registry()
        rng = random.Random(20260827)
        for trial in range(30):
            rules = [self._random_rule(rng, i) for i in range(rng.randint(3, 6))]
            context = self._random_context(rng, rules)
            baseline_diagnostics = lint_rule_set(rules, registry)
            baseline_text = serialize_rule_set(rules)
            for seed in (1, 7, 42):
                shuffled = rules[:]
                random.Random(seed + trial).shuffle(shuffled)
                self.assertEqual(lint_rule_set(shuffled, registry), baseline_diagnostics)
                self.assertEqual(serialize_rule_set(shuffled), baseline_text)
                if not baseline_diagnostics:
                    self.assertEqual(resolve(shuffled, context, registry),
                                     resolve(rules, context, registry))
            if not baseline_diagnostics:
                self.assertEqual(resolve(rules, context, registry),
                                 resolve(rules, context, registry))


if __name__ == "__main__":
    unittest.main()