"""S1.5 governance-binding tests (WI-PFW-S1C, OpenSpec
``perception-operator-rule-framework``).

Proves the rule-set content axis is bound into the EXISTING perception
governance chain (configId → deploymentId → receipt) with zero behavior
difference when a config carries no rule set:

* config_hash / manifest axis: same rule set ⇒ same identity; ANY byte change
  (or presence vs absence) ⇒ different identity;
* lint-rejected or structurally invalid rule-set content fails the config
  loader fail-closed, naming the diagnostic — an unloadable rule set never
  enters runtime;
* absent ruleset content resolves to ``registry_defaults.DEFAULT_RULE_SET``
  (the resolved identity equals the default serialization byte-for-byte);
* an unpromoted candidate rule set never affects resolution unless it is the
  one present in the loaded config ("Unpromoted candidate rules never run");
* identity-falsifier style: two configs differing ONLY in rule-set content
  produce distinct manifests / configIds / deploymentIds.

Read-only contract: CURRENT-ACTIVE receipt and deployments/ artifacts are
NOT touched by the code under test or by these tests.
"""
from __future__ import annotations

import unittest

from evaluation.identity import sha256_bytes

from governance.config_manifest import (
    PerceptionConfigManifest,
    build_from_perception_config,
    ruleset_content_hash,
)
from governance.deployment import PerceptionDeploymentCandidate
from uniclaw_perception.config import (
    DEFAULT_RULESET_MARKER,
    PerceptionConfig,
    RuleSetResolutionError,
    compute_config_hash,
    resolve_active_rule_set,
)
from uniclaw_perception.operators import (
    DEFAULT_CONTEXT,
    DEFAULT_RULE_SET,
    REGISTRY,
    FrameContext,
    Rule,
    deserialize_rule_set,
    load_rule_set,
    resolve,
    serialize_rule_set,
)

_SCHEMA = "uniclaw.localVisionEvidence.v1"


def _ruleset_cfg(ruleset_content: str | None) -> PerceptionConfig:
    cfg = PerceptionConfig()
    cfg.ruleset_content = ruleset_content
    return cfg


def _root_with_min_anchors(min_anchors: int) -> list[Rule]:
    """DEFAULT_RULE_SET with root-uniform-list-row-grouping.minAnchors changed."""
    rules = list(DEFAULT_RULE_SET)
    index = next(
        i for i, r in enumerate(rules)
        if r.rule_id == "root-uniform-list-row-grouping"
    )
    root = rules[index]
    params = dict(root.params)
    params["uniform-list-row-grouping.minAnchors"] = min_anchors
    rules[index] = Rule(root.rule_id, dict(root.pins), root.tags_pins, params)
    return rules


class ConfigHashAxisTests(unittest.TestCase):
    """(i) config_hash: same ruleset ⇒ same hash; ANY byte change ⇒ different."""

    def test_same_ruleset_same_config_hash(self):
        text = serialize_rule_set(DEFAULT_RULE_SET)
        self.assertEqual(
            compute_config_hash(b"{}", text),
            compute_config_hash(b"{}", text),
        )

    def test_any_ruleset_byte_change_changes_config_hash(self):
        text = serialize_rule_set(DEFAULT_RULE_SET)
        # byte-level: a single trailing byte changes the hash
        self.assertNotEqual(
            compute_config_hash(b"{}", text),
            compute_config_hash(b"{}", text + " "),
        )
        # semantic: a different parameter value serializes differently
        self.assertNotEqual(
            compute_config_hash(b"{}", text),
            compute_config_hash(b"{}", serialize_rule_set(
                _root_with_min_anchors(5))),
        )

    def test_absent_ruleset_binds_stable_default_marker(self):
        a = compute_config_hash(b"{}", None)
        b = compute_config_hash(b"{}", None)
        self.assertEqual(a, b)  # same absent state ⇒ same hash
        # absent ≠ any serialized rule set
        self.assertNotEqual(
            a, compute_config_hash(b"{}", serialize_rule_set(DEFAULT_RULE_SET))
        )
        # the marker is not a valid rule-set document (no collision possible)
        with self.assertRaises(ValueError):
            deserialize_rule_set(DEFAULT_RULESET_MARKER)

    def test_ruleset_axis_also_affects_label_mapping_hash_domain(self):
        """With the same label-mapping bytes, adding a ruleset still changes
        the composite config hash (same file, different ruleset ⇒ different
        identity)."""
        raw = b'{"detection": {"confidence": 0.35}}'
        self.assertNotEqual(
            compute_config_hash(raw, None),
            compute_config_hash(raw, serialize_rule_set(DEFAULT_RULE_SET)),
        )


class FailClosedLoadTests(unittest.TestCase):
    """(ii) unloadable rule-set content must fail the loader fail-closed,
    naming the diagnostic."""

    def test_out_of_bounds_ruleset_fails_with_diagnostic(self):
        bad = (
            '{"schemaVersion": 1, "rules": [{"ruleId": "bad", "params": '
            '{"uniform-list-row-grouping.minAnchors": 999}}]}'
        )
        with self.assertRaises(RuleSetResolutionError) as ctx:
            resolve_active_rule_set(_ruleset_cfg(bad))
        self.assertIn("out_of_bounds", str(ctx.exception))
        self.assertIn("uniform-list-row-grouping.minAnchors", str(ctx.exception))

    def test_structural_invalid_ruleset_fails_load(self):
        with self.assertRaises(RuleSetResolutionError):
            resolve_active_rule_set(_ruleset_cfg("{not json"))
        with self.assertRaises(RuleSetResolutionError):
            resolve_active_rule_set(
                _ruleset_cfg('{"schemaVersion": 99, "rules": []}')
            )
        with self.assertRaises(RuleSetResolutionError):
            resolve_active_rule_set(
                _ruleset_cfg('{"rules": "not-an-array"}')
            )

    def test_valid_serialized_ruleset_resolves_clean(self):
        loaded = resolve_active_rule_set(
            _ruleset_cfg(serialize_rule_set(DEFAULT_RULE_SET)))
        self.assertTrue(loaded.is_valid)
        self.assertEqual(tuple(loaded.rules), DEFAULT_RULE_SET)


class DefaultRuleSetFallbackTests(unittest.TestCase):
    """(iii) absent field ⇒ DEFAULT_RULE_SET, zero behavior difference."""

    def test_absent_field_resolves_to_default_rule_set(self):
        cfg = PerceptionConfig()  # ruleset_content is None
        loaded = resolve_active_rule_set(cfg)
        self.assertTrue(loaded.is_valid)
        self.assertEqual(loaded.diagnostics, ())
        self.assertEqual(tuple(loaded.rules), DEFAULT_RULE_SET)
        # the resolved identity equals the default serialization byte-for-byte
        self.assertEqual(
            serialize_rule_set(loaded.rules),
            serialize_rule_set(DEFAULT_RULE_SET),
        )
        # and the same rules come back from the strict loader round trip
        round_tripped = load_rule_set(
            serialize_rule_set(DEFAULT_RULE_SET), REGISTRY)
        self.assertEqual(round_tripped.rules, loaded.rules)

    def test_default_resolution_matches_contract_defaults(self):
        loaded = resolve_active_rule_set(PerceptionConfig())
        resolved = resolve(loaded.rules, DEFAULT_CONTEXT, REGISTRY)
        by_id = {entry.operator_id: entry for entry in resolved}
        for operator_id, entry in by_id.items():
            contract = REGISTRY.lookup(operator_id)
            for name, spec in contract.parameters.items():
                self.assertEqual(
                    entry.values[name], spec.default,
                    f"{operator_id}.{name} must resolve to its contract default",
                )


class UnpromotedCandidateNonInterferenceTests(unittest.TestCase):
    """(iv) a candidate rule set never affects resolution unless it is the one
    present in the loaded config."""

    CANDIDATE = [
        Rule("android-minanchors-5", {"system": "android"},
             params={"uniform-list-row-grouping.minAnchors": 5}),
    ]
    ANDROID_CTX = FrameContext(system="android")

    def test_candidate_not_present_in_config_never_resolves(self):
        # The candidate exists only here (validation-side store analog); the
        # loaded config carries no ruleset ⇒ runtime resolves the defaults
        # even in a context where the candidate WOULD match.
        loaded = resolve_active_rule_set(PerceptionConfig())
        self.assertEqual(tuple(loaded.rules), DEFAULT_RULE_SET)
        self.assertNotIn(
            self.CANDIDATE[0].rule_id, [r.rule_id for r in loaded.rules])
        resolved = resolve(loaded.rules, self.ANDROID_CTX, REGISTRY)
        by_id = {entry.operator_id: entry for entry in resolved}
        self.assertEqual(
            by_id["uniform-list-row-grouping"].param("minAnchors"), 4)

    def test_candidate_present_in_loaded_config_resolves(self):
        cfg = PerceptionConfig()
        cfg.ruleset_content = serialize_rule_set(
            [*DEFAULT_RULE_SET, *self.CANDIDATE])
        loaded = resolve_active_rule_set(cfg)
        self.assertTrue(loaded.is_valid)
        self.assertIn(
            self.CANDIDATE[0].rule_id, [r.rule_id for r in loaded.rules])
        resolved = resolve(loaded.rules, self.ANDROID_CTX, REGISTRY)
        by_id = {entry.operator_id: entry for entry in resolved}
        self.assertEqual(
            by_id["uniform-list-row-grouping"].param("minAnchors"), 5)
        # and a non-matching context still resolves the root default
        other = resolve(loaded.rules, FrameContext(system="ios"), REGISTRY)
        other_by_id = {entry.operator_id: entry for entry in other}
        self.assertEqual(
            other_by_id["uniform-list-row-grouping"].param("minAnchors"), 4)

    def test_candidate_change_changes_the_config_hash(self):
        a = compute_config_hash(b"{}", serialize_rule_set(DEFAULT_RULE_SET))
        b = compute_config_hash(b"{}", serialize_rule_set(
            [*DEFAULT_RULE_SET, *self.CANDIDATE]))
        self.assertNotEqual(a, b)


class ManifestRulesetAxisTests(unittest.TestCase):
    """(v) identity-falsifier style: manifests differ ONLY when the ruleset
    axis differs; the axis travels configId → deploymentId."""

    def test_manifest_carries_ruleset_axis(self):
        m = build_from_perception_config(PerceptionConfig())
        axis = m._identity_content()["ruleset"]
        self.assertEqual(axis["contentHash"], DEFAULT_RULESET_MARKER)
        self.assertTrue(axis["evidenceRelevant"])

    def test_same_ruleset_same_manifest_identity(self):
        text = serialize_rule_set(DEFAULT_RULE_SET)
        a = build_from_perception_config(_ruleset_cfg(text))
        b = build_from_perception_config(_ruleset_cfg(text))
        self.assertEqual(a.config_id, b.config_id)
        axis = a._identity_content()["ruleset"]
        self.assertEqual(
            axis["contentHash"], f"sha256:{sha256_bytes(text.encode('utf-8'))}")

    def test_two_configs_differing_only_in_ruleset_produce_distinct_manifests(self):
        a = build_from_perception_config(_ruleset_cfg(None))
        b = build_from_perception_config(
            _ruleset_cfg(serialize_rule_set(DEFAULT_RULE_SET)))
        self.assertNotEqual(a.config_id, b.config_id)
        self.assertNotEqual(a.to_json(), b.to_json())
        self.assertNotEqual(
            a._identity_content()["ruleset"]["contentHash"],
            b._identity_content()["ruleset"]["contentHash"],
        )

    def test_ruleset_change_travels_config_id_to_deployment_id(self):
        def candidate(config_id: str) -> PerceptionDeploymentCandidate:
            return PerceptionDeploymentCandidate(
                schema_version=_SCHEMA, model_id="m" * 64,
                config_id=config_id, pipeline_revision="prev:1",
            )

        a = build_from_perception_config(_ruleset_cfg(None))
        b = build_from_perception_config(
            _ruleset_cfg(serialize_rule_set(_root_with_min_anchors(5))))
        self.assertNotEqual(
            candidate(a.config_id).deployment_id,
            candidate(b.config_id).deployment_id,
        )

    def test_ruleset_content_hash_helper(self):
        self.assertEqual(ruleset_content_hash(None), DEFAULT_RULESET_MARKER)
        text = serialize_rule_set(DEFAULT_RULE_SET)
        self.assertEqual(
            ruleset_content_hash(text),
            f"sha256:{sha256_bytes(text.encode('utf-8'))}",
        )

    def test_legacy_manifest_without_ruleset_axis_parses_as_before(self):
        payload = {
            "schema": "uniclaw.perceptionConfig.v1",
            "preprocessing": {"maxWidth": 720, "cropTopRatio": 0.0625,
                              "cropBottomRatio": 0.0625},
            "yolo": {"confidence": 0.35},
            "ocr": {"backend": "rapidocr", "mode": "full", "textScore": 0.5,
                    "language": "en", "roiPadding": {"x": 0.15}},
            "scroll": {"edgeThreshold": 0.92},
            "referencedArtifacts": {},
            "completeness": "COMPLETE",
            "configId": "config:legacy",
        }
        m = PerceptionConfigManifest.from_json(payload)
        self.assertEqual(m.ruleset, {})  # absent axis ⇒ default (parse as before)
        # deterministic round trip re-introduces the (empty) axis
        self.assertIn("ruleset", m.to_json())
        self.assertEqual(m.to_json()["ruleset"], {})


if __name__ == "__main__":
    unittest.main()