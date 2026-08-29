import copy
import contextlib
import importlib.util
import io
import json
import re
import tempfile
import unittest
from pathlib import Path
from unittest import mock


REPO_ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location(
    "agent_profile_validator", REPO_ROOT / "tools" / "agent_profile_validator.py"
)
validator = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(validator)


class AgentProfileWorkflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.registries = validator.load_registries()

    def valid_work_item(self, execution="development", module="runtime-core"):
        write = ["src/UniClaw.Runtime/Agent/Agent.cs"]
        if execution == "test-authoring":
            write = ["tests/UniClaw.Runtime.Tests/Scenario/ExampleTests.cs"]
        elif execution in ("verification", "semantic-analysis", "tool-only"):
            write = []
        payload = {
            "id": "WI-001",
            "change_set_id": "CS-001",
            "base_revision": "abc123",
            "role_profile": "module-worker",
            "execution_profile": execution,
            "module_profile": module,
            "worker_owner": "worker-1",
            "objective": "完成一个可验证的局部目标",
            "scope": {"write": write, "read_hints": []},
            "anchors": [{"path": write[0] if write else "src/UniClaw.Runtime/Agent/Agent.cs", "symbol": "Agent"}],
            "change_principles": ["保持现有架构边界"],
            "contract_refs": ["docs/system/constitution/runtime-architecture-contract.md"],
            "acceptance": ["目标验证通过"],
            "forbidden": ["修改未授权文件"],
            "escalation": ["遇到架构问题返回 coding-leader"],
            "leader_decisions_frozen": True,
            "unresolved_architecture": [],
        }
        return validator.build_work_item(
            payload,
            "当前任务已有明确目标和授权范围，需要在所选模块内完成局部修改，并由指定测试门确认结果。",
            ["修改范围位于已授权路径", "验证结果由指定测试门确认"],
            self.registries,
        )

    def valid_work_result(self):
        return {
            "id": "WI-001",
            "status": "DONE",
            "base_revision": "abc123",
            "changed": [],
            "verification": [],
            "module_context_delta": {
                "affected_symbols": ["Agent"],
                "new_test_refs": [],
                "contract_changes": [],
                "obsolete_refs": [],
            },
            "deviations": [],
            "unresolved": [],
        }

    def test_01_role_profiles_parse_independently(self):
        data = validator._registry("roles.json")
        self.assertEqual({"coding-leader", "module-worker"}, set(validator.index_profiles(data)))

    def test_02_execution_profiles_parse_independently(self):
        data = validator._registry("execution.json")
        self.assertEqual(5, len(data["profiles"]))

    def test_03_module_profiles_parse_independently(self):
        data = validator._registry("modules.json")
        self.assertEqual(4, len(data["profiles"]))

    def test_04_profile_merge_is_stable(self):
        first = validator.compose_profile("module-worker", "development", "runtime-core", self.registries)
        second = validator.compose_profile("module-worker", "development", "runtime-core", self.registries)
        self.assertEqual(first, second)

    def test_05_missing_profile_blocks_dispatch(self):
        with self.assertRaises(validator.ProfileError):
            validator.compose_profile("module-worker", "missing", "runtime-core", self.registries)

    def test_06_work_item_requires_one_scalar_owner(self):
        item = self.valid_work_item()
        item["worker_owner"] = ["worker-1", "worker-2"]
        self.assertTrue(any("worker_owner" in error for error in validator.validate_work_item(item, self.registries)))

    def test_07_development_can_write_authorized_production_code(self):
        self.assertEqual([], validator.validate_work_item(self.valid_work_item(), self.registries))

    def test_08_development_cannot_write_another_module(self):
        item = self.valid_work_item()
        item["scope"]["write"] = ["src/UniClaw.Semantic.Settings/SettingsSemanticCapability.cs"]
        self.assertTrue(any("outside module scope" in error for error in validator.validate_work_item(item, self.registries)))

    def test_09_test_author_can_write_tests(self):
        item = self.valid_work_item("test-authoring")
        self.assertEqual([], validator.validate_work_item(item, self.registries))

    def test_10_test_author_cannot_write_production_code(self):
        item = self.valid_work_item("test-authoring")
        item["scope"]["write"] = ["src/UniClaw.Runtime/Agent/Agent.cs"]
        self.assertTrue(any("outside module test paths" in error for error in validator.validate_work_item(item, self.registries)))

    def test_11_verifier_cannot_write_source(self):
        item = self.valid_work_item("verification")
        item["scope"]["write"] = ["tests/UniClaw.Runtime.Tests/Scenario/ExampleTests.cs"]
        self.assertTrue(any("forbids source write" in error for error in validator.validate_work_item(item, self.registries)))

    def test_12_semantic_analyzer_cannot_write_source(self):
        item = self.valid_work_item("semantic-analysis")
        item["scope"]["write"] = ["src/UniClaw.Semantic.Infrastructure/Fast/Provider.cs"]
        self.assertTrue(any("forbids source write" in error for error in validator.validate_work_item(item, self.registries)))

    def test_13_worker_profiles_cannot_spawn_subagents(self):
        for profile in self.registries["execution"]["profiles"]:
            self.assertIs(profile["permissions"]["spawn_agent"], False)

    def test_14_module_id_resolves_uniquely(self):
        self.assertEqual("semantic-capability", validator.resolve_module_for_path("src/UniClaw.Semantic.Settings/SettingsSemanticCapability.cs", self.registries))

    def test_15_unknown_module_returns_to_leader(self):
        self.assertEqual("coding-leader", validator.resolve_module_for_path("unknown/place/file.cs", self.registries))

    def test_16_local_agents_are_merged_into_context(self):
        manifest = validator.build_context_manifest("runtime-core", "development", "abc123", registries=self.registries)
        agents = manifest["context_sources"]["effective_agents"]
        self.assertIn("AGENTS.md", agents)
        self.assertIn("src/UniClaw.Runtime/AGENTS.md", agents)

    def test_16b_dependency_agents_are_merged_into_semantic_context(self):
        manifest = validator.build_context_manifest("semantic-capability", "semantic-analysis", "abc123", registries=self.registries)
        self.assertIn("src/UniClaw.Runtime/AGENTS.md", manifest["context_sources"]["effective_agents"])

    def test_17_rule_conflicts_are_not_silently_overwritten(self):
        with self.assertRaises(validator.ProfileError):
            validator.merge_mapping_strict({"permissions": {"write": False}}, {"permissions": {"write": True}})

    def test_18_context_key_changes_with_rules(self):
        left = validator.profile_context_key("1", "1", "1", "1", "rules-a", "rev")
        right = validator.profile_context_key("1", "1", "1", "1", "rules-b", "rev")
        self.assertNotEqual(left, right)

    def test_19_context_key_changes_with_revision(self):
        left = validator.profile_context_key("1", "1", "1", "1", "rules", "rev-a")
        right = validator.profile_context_key("1", "1", "1", "1", "rules", "rev-b")
        self.assertNotEqual(left, right)

    def test_20_sol_luna_model_binding_is_correct(self):
        self.assertEqual(
            {"coding-leader": "gpt-5.6-sol", "module-worker": "gpt-5.6-luna"},
            validator.codex_model_bindings(),
        )

    def test_21_deterministic_read_routes_to_tool_only(self):
        self.assertEqual("tool-only", validator.route_task({"deterministic": True})["route"])

    def test_22_atomic_module_task_routes_to_luna_worker_adapter(self):
        route = validator.route_task({"atomic": True, "module_id": "runtime-core", "change_principles_frozen": True})
        self.assertEqual("module-worker", route["agent"])

    def test_23_strongly_coupled_cross_module_task_stays_with_sol(self):
        route = validator.route_task({"cross_module": True, "contract_frozen": False})
        self.assertEqual("coding-leader", route["route"])

    def test_24_worker_result_does_not_auto_apply_context_delta(self):
        self.assertIsNone(validator.accept_module_context_delta(self.valid_work_result(), accepted_by_leader=False))

    def test_25_leader_can_accept_context_delta(self):
        delta = validator.accept_module_context_delta(self.valid_work_result(), accepted_by_leader=True)
        self.assertEqual(["Agent"], delta["affected_symbols"])

    def test_26_same_file_cannot_have_two_worker_owners(self):
        first = self.valid_work_item()
        second = copy.deepcopy(first)
        second["id"] = "WI-002"
        second["worker_owner"] = "worker-2"
        errors = validator.validate_change_set([first, second])
        self.assertTrue(any("concurrent writers" in error for error in errors))

    def test_27_work_item_cannot_carry_unresolved_architecture(self):
        item = self.valid_work_item()
        item["unresolved_architecture"] = ["Who owns lifecycle?"]
        self.assertTrue(any("unresolved architecture" in error for error in validator.validate_work_item(item, self.registries)))

    def test_28_semantic_buyer_is_not_a_production_symbol(self):
        production = "\n".join(path.read_text(encoding="utf-8", errors="ignore") for path in (REPO_ROOT / "src").rglob("*.cs"))
        self.assertNotIn("SemanticBuyer", production)

    def test_29_custom_agents_follow_required_schema(self):
        for name in ("module-worker.toml", "test-author.toml", "verifier.toml", "semantic-analyzer.toml"):
            self.assertEqual([], validator.validate_custom_agent_file(REPO_ROOT / ".codex" / "agents" / name))

    def test_30_work_item_schemas_parse(self):
        for name in ("work-item.schema.json", "work-result.schema.json"):
            with (REPO_ROOT / ".ai" / "schemas" / name).open(encoding="utf-8") as handle:
                self.assertIsInstance(json.load(handle), dict)

    def test_31_unknown_work_item_fields_are_rejected(self):
        item = self.valid_work_item()
        item["worker_owners"] = ["worker-1", "worker-2"]
        self.assertTrue(any("unknown WorkItem field" in error for error in validator.validate_work_item(item, self.registries)))

    def test_32_model_binding_version_is_derived_from_routing_config(self):
        self.assertEqual("model-routing-v5", validator.current_model_binding_version())

    def test_33_missing_semantic_brief_is_rejected(self):
        item = self.valid_work_item()
        item.pop("semantic_brief")
        self.assertTrue(any("semantic_brief" in error for error in validator.validate_work_item(item, self.registries)))

    def test_34_missing_semantic_summary_is_rejected(self):
        item = self.valid_work_item()
        item["semantic_brief"].pop("summary")
        self.assertTrue(any("summary" in error for error in validator.validate_work_item(item, self.registries)))

    def test_35_missing_semantic_core_points_is_rejected(self):
        item = self.valid_work_item()
        item["semantic_brief"].pop("core_points")
        self.assertTrue(any("core_points" in error for error in validator.validate_work_item(item, self.registries)))

    def test_36_empty_semantic_summary_is_rejected(self):
        item = self.valid_work_item()
        item["semantic_brief"]["summary"] = ""
        self.assertTrue(any("非空" in error for error in validator.validate_work_item(item, self.registries)))

    def test_37_semantic_summary_over_240_chars_is_rejected(self):
        item = self.valid_work_item()
        item["semantic_brief"]["summary"] = "状" * 241
        self.assertTrue(any("240" in error for error in validator.validate_work_item(item, self.registries)))

    def test_38_empty_semantic_core_points_is_rejected(self):
        item = self.valid_work_item()
        item["semantic_brief"]["core_points"] = []
        self.assertTrue(any("不能为空" in error for error in validator.validate_work_item(item, self.registries)))

    def test_39_more_than_five_semantic_core_points_is_rejected(self):
        item = self.valid_work_item()
        item["semantic_brief"]["core_points"] = ["要点%d" % index for index in range(6)]
        self.assertTrue(any("5项" in error for error in validator.validate_work_item(item, self.registries)))

    def test_40_semantic_core_point_over_100_chars_is_rejected(self):
        item = self.valid_work_item()
        item["semantic_brief"]["core_points"] = ["点" * 101]
        self.assertTrue(any("100" in error for error in validator.validate_work_item(item, self.registries)))

    def test_41_unknown_semantic_brief_field_is_rejected(self):
        item = self.valid_work_item()
        item["semantic_brief"]["background"] = "不应出现的自由扩展字段"
        self.assertTrue(any("未知字段" in error for error in validator.validate_work_item(item, self.registries)))

    def test_42_valid_concise_chinese_work_item_passes(self):
        self.assertEqual([], validator.validate_work_item(self.valid_work_item(), self.registries))

    def test_43_work_item_builder_generates_complete_semantic_brief(self):
        item = self.valid_work_item()
        self.assertEqual({"summary", "core_points"}, set(item["semantic_brief"]))
        self.assertTrue(item["semantic_brief"]["summary"])
        self.assertGreaterEqual(len(item["semantic_brief"]["core_points"]), 1)

    def test_44_example_has_no_bilingual_or_objective_summary_duplication(self):
        workflow = (REPO_ROOT / ".ai" / "workflows" / "uniflow-coding-workflow.md").read_text(encoding="utf-8")
        example_text = re.search(r"```json\n(.*?)\n```", workflow, flags=re.DOTALL).group(1)
        example = json.loads(example_text)
        self.assertNotEqual(example["objective"], example["semantic_brief"]["summary"])
        self.assertNotIn("objective_en", example)
        self.assertNotIn("summary_en", example["semantic_brief"])

    def test_45_unanchored_constraint_in_core_points_is_rejected(self):
        item = self.valid_work_item()
        item["semantic_brief"]["core_points"] = ["不得改变公开状态枚举"]
        self.assertTrue(any("正式约束锚点" in error for error in validator.validate_work_item(item, self.registries)))

    def test_46_anchored_constraint_in_core_points_passes(self):
        item = self.valid_work_item()
        item["semantic_brief"]["core_points"] = ["不得改变公开状态枚举"]
        item["forbidden"].append("不得改变公开状态枚举")
        self.assertEqual([], validator.validate_work_item(item, self.registries))

    def test_47_serialized_work_item_contains_semantic_brief(self):
        serialized = validator.serialize_work_item(self.valid_work_item(), self.registries)
        self.assertIn("semantic_brief", json.loads(serialized))

    def test_48_builder_emits_explicit_required_skills(self):
        self.assertEqual([], self.valid_work_item()["required_skills"])

    def test_49_required_skills_preserve_leader_order(self):
        item = self.valid_work_item()
        item["required_skills"] = [
            "evidence-driven-debugging",
            "runtime-behavior-debugging",
        ]
        self.assertEqual([], validator.validate_work_item(item, self.registries))
        self.assertEqual(item["required_skills"], json.loads(
            validator.serialize_work_item(item, self.registries))["required_skills"])

    def test_50_legacy_work_item_omission_is_read_only_compatible(self):
        item = self.valid_work_item()
        item.pop("required_skills")
        before = copy.deepcopy(item)
        self.assertEqual([], validator.validate_work_item(item, self.registries))
        self.assertEqual(before, item)

    def test_51_required_skill_rejects_path_or_malformed_name(self):
        for name in ("../outside", ".agents/skills/example", "/absolute", "Bad_Name"):
            item = self.valid_work_item()
            item["required_skills"] = [name]
            self.assertTrue(any("invalid required Skill name" in error
                                for error in validator.validate_work_item(
                                    item, self.registries)), name)

    def test_52_required_skill_rejects_duplicates(self):
        item = self.valid_work_item()
        item["required_skills"] = [
            "evidence-driven-debugging", "evidence-driven-debugging"]
        self.assertTrue(any("duplicates" in error for error in
                            validator.validate_work_item(item, self.registries)))

    def test_53_required_skill_rejects_missing_source(self):
        item = self.valid_work_item()
        item["required_skills"] = ["missing-project-skill"]
        self.assertTrue(any("not found" in error for error in
                            validator.validate_work_item(item, self.registries)))

    def test_54_required_skill_source_is_portable_core_only(self):
        self.assertEqual(
            (REPO_ROOT / ".ai" / "skills",),
            validator.SKILL_SOURCE_DIRS,
        )

    def test_54b_required_skill_rejects_ambiguous_resolver_configuration(self):
        root = REPO_ROOT / ".ai" / "skills"
        with mock.patch.object(validator, "SKILL_SOURCE_DIRS", (root, root)):
            with self.assertRaises(validator.ProfileError) as caught:
                validator.resolve_required_skills(["evidence-driven-debugging"])
        self.assertIn("ambiguous", str(caught.exception))

    def test_55_required_skill_enters_manifest_and_context_key(self):
        without_skill = validator.build_context_manifest(
            "engineering-governance", "development", "abc123",
            registries=self.registries)
        with_skill = validator.build_context_manifest(
            "engineering-governance", "development", "abc123",
            registries=self.registries,
            required_skills=["evidence-driven-debugging"])
        self.assertEqual(
            [".ai/skills/evidence-driven-debugging/SKILL.md"],
            with_skill["context_sources"]["required_skills"])
        skill_context = with_skill["required_skill_context"]
        self.assertEqual("BLOCKED_FOR_SPEC", skill_context["failure_status"])
        self.assertEqual("REQUIRED_SKILL_UNAVAILABLE",
                         skill_context["failure_reason"])
        self.assertEqual(
            ["evidence-driven-debugging"],
            [document["name"] for document in skill_context["documents"]])
        self.assertIn("name: evidence-driven-debugging",
                      skill_context["documents"][0]["content"])
        self.assertNotEqual(without_skill["profile_context_key"],
                            with_skill["profile_context_key"])

    def test_56_context_cli_loads_required_skills_from_work_item(self):
        item = self.valid_work_item()
        item["required_skills"] = ["evidence-driven-debugging"]
        with tempfile.TemporaryDirectory(prefix="wi-skill-") as temp_dir:
            path = Path(temp_dir) / "work-item.json"
            path.write_text(json.dumps(item, ensure_ascii=False), encoding="utf-8")
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                exit_code = validator.main(["context", "--work-item", str(path)])
        self.assertEqual(0, exit_code)
        manifest = json.loads(output.getvalue())
        self.assertEqual(
            [".ai/skills/evidence-driven-debugging/SKILL.md"],
            manifest["context_sources"]["required_skills"])

    def test_57_required_skill_context_preserves_full_ordered_documents(self):
        manifest = validator.build_context_manifest(
            "engineering-governance", "development", "abc123",
            registries=self.registries,
            required_skills=[
                "evidence-driven-debugging",
                "runtime-behavior-debugging",
            ])
        documents = manifest["required_skill_context"]["documents"]
        self.assertEqual(
            ["evidence-driven-debugging", "runtime-behavior-debugging"],
            [document["name"] for document in documents])
        self.assertEqual(
            manifest["context_sources"]["required_skills"],
            [document["path"] for document in documents])
        for document in documents:
            self.assertTrue(document["content"].strip())
            self.assertEqual(64, len(document["content_sha256"]))


if __name__ == "__main__":
    unittest.main()
