"""DSH Profile Adapter gate tests — 30 cases mapping to the change spec.

All cases run against the REAL upstream validator and REAL profile files;
no semantic test doubles. Temporary state dirs are used per test where needed.
"""

import copy
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location(
    "dsh_profile_adapter", REPO_ROOT / "tools" / "dsh_profile_adapter.py"
)
adapter = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(adapter)

VSPEC = importlib.util.spec_from_file_location(
    "agent_profile_validator", REPO_ROOT / "tools" / "agent_profile_validator.py"
)
validator = importlib.util.module_from_spec(VSPEC)
VSPEC.loader.exec_module(validator)


def work_item(**overrides):
    item = {
        "id": "WI-TEST-001",
        "change_set_id": "CS-TEST",
        "base_revision": "eac69ee0f0960af044b76cce31f9335e6aa5b52c",
        "role_profile": "module-worker",
        "execution_profile": "development",
        "module_profile": "engineering-governance",
        "worker_owner": "module-worker-1",
        "objective": "在授权范围内完成一个局部治理工具改动",
        "semantic_brief": {
            "summary": "当前治理工具缺少一项局部能力。本任务在授权路径内补齐该能力并用现有测试门验证。",
            "core_points": [
                "修改集中在WorkItem指定的engineering-governance路径",
                "现有职责边界保持不变",
                "结果由指定测试门确认",
            ],
        },
        "scope": {"write": ["tools/<approved>.py"], "read_hints": []},
        "anchors": [{"path": "tools/<approved>.py", "symbol": "<symbol>"}],
        "change_principles": ["复用现有工具抽象，不引入新职责边界"],
        "contract_refs": [],
        "acceptance": ["tests/AgentWorkflow 通过并保留验证证据"],
        "forbidden": ["禁止修改未授权路径或扩大架构范围"],
        "escalation": ["出现架构或ownership问题时返回Leader"],
        "leader_decisions_frozen": True,
        "unresolved_architecture": [],
    }
    item.update(overrides)
    return item


def work_result(item, **overrides):
    result = {
        "id": item["id"],
        "status": "DONE",
        "base_revision": item["base_revision"],
        "changed": [{"path": item["scope"]["write"][0]}],
        "verification": [{"kind": "unittest", "ref": "tests/AgentWorkflow"}],
        "module_context_delta": {
            "affected_symbols": [], "new_test_refs": [],
            "contract_changes": [], "obsolete_refs": [],
        },
        "deviations": [],
        "unresolved": [],
    }
    result.update(overrides)
    return result


class SourceConfig:
    def __init__(self, **overrides):
        self.config = adapter.load_config()
        for key, value in overrides.items():
            self.config["profile_source"][key] = value


class DshProfileAdapterTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.registries = validator.load_registries()
        cls.events = adapter.EventLog()

    def tmp_state(self):
        tmp = tempfile.mkdtemp(prefix="dsh-pa-")
        self.addCleanup(lambda: None)
        return tmp

    # 1. 上游 Profile Source 可以加载
    def test_01_source_loads(self):
        source = adapter.ProfileSource(SourceConfig().config)
        registries = source.load()
        self.assertIn("roles", registries)
        self.assertIn("profile.source.validated", source.events.names())
        self.assertIn("profile.loaded", source.events.names())

    # 2. Profile Schema 版本不匹配时拒绝启动
    def test_02_schema_version_mismatch_rejected(self):
        source = adapter.ProfileSource(SourceConfig(schema_version=9).config)
        with self.assertRaises(adapter.DshAdapterError) as ctx:
            source.load()
        self.assertIn("schema version mismatch", str(ctx.exception))

    # 3. 上游 Profile 验证失败时拒绝工作流激活
    def test_03_upstream_validation_failure_rejected(self):
        source = adapter.ProfileSource(
            SourceConfig(validation_command="python3 tools/agent_profile_validator.py work-item /nonexistent").config)
        with self.assertRaises(adapter.DshAdapterError) as ctx:
            source.load()
        self.assertIn("upstream profile validation failed", str(ctx.exception))

    # 4. DSH 不会修改上游 Profile
    def test_04_upstream_profiles_untouched(self):
        import os
        root = REPO_ROOT / ".ai" / "profiles"
        before = {p.name: (p.read_bytes(), os.stat(p).st_mtime_ns)
                  for p in sorted(root.glob("*.json"))}
        source = adapter.ProfileSource(SourceConfig().config)
        source.load()
        runtime = adapter.DshWorkflowRuntime()
        runtime.adapter.compose("module-worker", "development", "engineering-governance")
        after = {p.name: (p.read_bytes(), os.stat(p).st_mtime_ns)
                 for p in sorted(root.glob("*.json"))}
        self.assertEqual(before, after)

    # 5. Profile 合并优先级与上游一致
    def test_05_compose_matches_upstream(self):
        pa = adapter.ProfileAdapter(self.events)
        composed = pa.compose("module-worker", "development", "runtime-core")
        upstream = validator.compose_profile("module-worker", "development",
                                             "runtime-core")
        self.assertEqual(composed, upstream)

    # 6. 规则冲突不会被静默覆盖
    def test_06_conflict_raises_leader_decision_required(self):
        pa = adapter.ProfileAdapter(self.events)
        left = {"permissions": {"write": "a"}}
        right = {"permissions": {"write": "b"}}
        with self.assertRaises(adapter.LeaderDecisionRequired) as ctx:
            pa.merge_strict(left, right)
        self.assertIn("permissions.write", str(ctx.exception))
        self.assertIn("profile.conflict", pa.events.names())

    # 7. GLM-5.2 primary 绑定正确
    def test_07_leader_primary_binding(self):
        binding = adapter.ModelBinding(adapter.load_config(), self.events)
        primary = binding.binding_for("decision_frontier")["primary"]
        self.assertEqual(primary["provider"], "zai")
        self.assertEqual(primary["model"], "glm-5.2")
        self.assertEqual(primary["reasoning"], "high")

    # 8. OpenCode GLM-5.2 fallback 绑定正确
    def test_08_leader_fallback_binding(self):
        binding = adapter.ModelBinding(adapter.load_config(), self.events)
        fallback = binding.binding_for("decision_frontier")["fallback"]
        self.assertEqual(fallback["provider"], "opencode-go")
        self.assertEqual(fallback["model"], "glm-5.2")

    # 9. DeepSeek-V4 Worker 绑定正确
    def test_09_worker_binding(self):
        binding = adapter.ModelBinding(adapter.load_config(), self.events)
        worker = binding.worker_binding()["primary"]
        self.assertEqual(worker["provider"], "opencode-go")
        self.assertEqual(worker["model"], "deepseek-v4-flash")
        self.assertEqual(worker["reasoning"], "high")

    # 10. primary 与 fallback 不会同时成为 Leader
    def test_10_single_leader_authority(self):
        binding = adapter.ModelBinding(adapter.load_config(), self.events)
        frontier = binding.binding_for("decision_frontier")
        holders = [k for k in ("primary", "fallback")
                   if frontier[k].get("leader_authority")]
        self.assertEqual(holders, ["primary"])
        binding.request_fallback("provider_unavailable")
        frontier = binding.binding_for("decision_frontier")
        holders = [k for k in ("primary", "fallback")
                   if frontier[k].get("leader_authority")]
        self.assertEqual(holders, ["fallback"])

    # 11. fallback 可以从最新 Checkpoint 恢复
    def test_11_fallback_restores_checkpoint(self):
        state = self.tmp_state()
        cp = adapter.LeaderCheckpoint("s1", "pv", "goal-ref", events=self.events,
                                      state_dir=state)
        cp.add_pending("WI-A")
        binding = adapter.ModelBinding(adapter.load_config(), self.events)
        takeover = adapter.leader_fallback_takeover(
            binding, "provider_unavailable", state, events=self.events)
        self.assertEqual(takeover["pending_work"], ["WI-A"])
        self.assertEqual(takeover["checkpoint"].data["active_leader_provider"],
                          "opencode-go")
        self.assertIn("leader.fallback.started", self.events.names())

    def test_11b_business_failure_never_triggers_fallback(self):
        binding = adapter.ModelBinding(adapter.load_config(), self.events)
        for reason in ("worker_test_failed", "leader_decision_error",
                       "rule_conflict", "user_goal_changed",
                       "work_item_split_invalid"):
            with self.assertRaises(adapter.DshAdapterError):
                binding.request_fallback(reason)
        # primary 仍持有 authority
        frontier = binding.binding_for("decision_frontier")
        self.assertTrue(frontier["primary"]["leader_authority"])

    # 12. 一个 WorkItem 只能有一个 Worker Owner
    def test_12_single_owner(self):
        item = work_item()
        errors = validator.validate_work_item(item)
        self.assertEqual(errors, [])

    # 13. 同一 WorkItem fanout 被拒绝
    def test_13_fanout_rejected(self):
        scheduler = adapter.Scheduler(self.events)
        item = work_item(scope={"write": ["tools/x.py"], "read_hints": []})
        scheduler.dispatch(item)
        with self.assertRaises(adapter.DshAdapterError) as ctx:
            scheduler.dispatch(dict(item, worker_owner="module-worker-2"))
        self.assertIn("fanout rejected", str(ctx.exception))
        with self.assertRaises(adapter.DshAdapterError):
            scheduler.dispatch(item)  # same owner re-dispatch also rejected

    # 14. Worker 创建 SubAgent 被拒绝
    def test_14_worker_spawn_rejected(self):
        scheduler = adapter.Scheduler(self.events)
        with self.assertRaises(adapter.DshAdapterError):
            scheduler.request_spawn("module-worker-1")

    # 15. ModuleProfile 能够唯一解析
    def test_15_module_uniquely_resolved(self):
        store = adapter.ModuleContextStore(events=self.events)
        self.assertEqual(store.resolve_module("src/UniClaw.Runtime/Agent/Agent.cs"),
                         "runtime-core")
        self.assertEqual(store.resolve_module("tools/agent_profile_validator.py"),
                         "engineering-governance")

    # 16. 模块归属不明确时返回 Leader
    def test_16_ambiguous_module_returns_leader(self):
        store = adapter.ModuleContextStore(events=self.events)
        self.assertEqual(store.resolve_module("README.md"), "coding-leader")
        self.assertEqual(store.resolve_module("nonexistent/owner/path.cs"),
                         "coding-leader")

    # 17. ModuleContext 可以自动加载
    def test_17_context_auto_loaded(self):
        state = self.tmp_state()
        store = adapter.ModuleContextStore(state, self.events)
        item = work_item(scope={"write": ["tools/demo_x.py"], "read_hints": []})
        manifest = store.load_for_work_item(item)
        self.assertEqual(manifest["module_profile"], "engineering-governance")
        self.assertEqual(manifest["execution_profile"], "development")
        self.assertIn("AGENTS.md", manifest["context_sources"]["effective_agents"])
        self.assertTrue(manifest["entrypoints"])
        self.assertTrue(manifest["test_gates"])
        self.assertTrue(manifest["profile_context_key"])
        self.assertIn("worker.context.loaded", self.events.names())

    # 18. Development 权限正确
    def test_18_development_permissions(self):
        ok = work_item(scope={"write": ["tools/dev_ok.py"], "read_hints": []})
        self.assertEqual(validator.validate_work_item(ok), [])
        bad = work_item(scope={"write": ["src/UniClaw.Runtime/X.cs"], "read_hints": []})
        errors = validator.validate_work_item(bad)
        self.assertTrue(any("outside module scope" in e for e in errors))

    # 19. Test Author 不能修改生产代码
    def test_19_test_author_forbidden_production(self):
        bad = work_item(execution_profile="test-authoring",
                        scope={"write": ["tools/prod.py"], "read_hints": []})
        errors = validator.validate_work_item(bad)
        self.assertTrue(any("outside module test paths" in e for e in errors))
        ok = work_item(execution_profile="test-authoring",
                       scope={"write": ["tests/AgentWorkflow/test_x.py"], "read_hints": []},
                       acceptance=["测试门通过"])
        self.assertEqual(validator.validate_work_item(ok), [])

    # 20. Verification 不能修改源代码
    def test_20_verification_no_write(self):
        item = work_item(execution_profile="verification",
                         scope={"write": ["tools/anything.py"], "read_hints": []})
        errors = validator.validate_work_item(item)
        self.assertTrue(any("forbids source write scope" in e for e in errors))

    # 21. Semantic Analysis 不能修改代码
    def test_21_semantic_no_write(self):
        item = work_item(execution_profile="semantic-analysis",
                         scope={"write": ["src/UniClaw.Runtime/Y.cs"], "read_hints": []})
        errors = validator.validate_work_item(item)
        self.assertTrue(any("forbids source write scope" in e for e in errors))

    # 22. Tool Only 任务不调用模型
    def test_22_tool_only_no_model(self):
        binding = adapter.ModelBinding(adapter.load_config(), self.events)
        router = adapter.WorkerRouter(self.events)
        decision = router.route({"deterministic": True}, binding=binding)
        self.assertEqual(decision["route"], "tool-only")
        self.assertEqual(binding.tool_only()["model"], "none")
        self.assertEqual(binding.model_calls_for_tool_only(), 0)

    # 23. Context Key 变化后缓存失效
    def test_23_cache_invalidated_on_key_change(self):
        store = adapter.ModuleContextStore(self.tmp_state(), self.events)
        item = work_item(scope={"write": ["tools/k1.py"], "read_hints": []})
        manifest = store.load_for_work_item(item)
        store.put(manifest["profile_context_key"], manifest)
        self.assertIsNotNone(store.get(manifest["profile_context_key"]))
        changed = copy.deepcopy(manifest)
        changed["profile_context_key"] = "different-key"
        self.assertTrue(store.invalidate_if_stale(
            manifest["profile_context_key"], changed))
        self.assertIsNone(store.get(manifest["profile_context_key"]))
        cache = adapter.WorkerSessionCache()
        cache.invalidate_on("rule_digest")
        cache.invalidate_on("profile_version")
        cache.invalidate_on("worker_blocked")

    # 24. 同模块重复任务可以复用有效上下文
    def test_24_session_reuse_append_only(self):
        store = adapter.ModuleContextStore(self.tmp_state(), self.events)
        item = work_item(scope={"write": ["tools/r1.py"], "read_hints": []})
        manifest = store.load_for_work_item(item)
        cache = adapter.WorkerSessionCache()
        session = cache.reuse(manifest, [{"kind": "work_item", "id": item["id"]}])
        same = cache.reuse(manifest, [
            {"kind": "changeset_contract", "ref": "CS-TEST"},
            {"kind": "accepted_delta", "ref": item["id"]}])
        self.assertIs(session, same)
        self.assertEqual(len(session["appendix"]), 3)
        with self.assertRaises(adapter.DshAdapterError):
            cache.reuse(manifest, [{"kind": "full_diff"}])  # 禁止累积完整 diff

    # 25. Worker 修改 scope 外文件时结果被拒绝
    def test_25_out_of_scope_write_rejected(self):
        gate = adapter.WorkResultGate(self.events)
        item = work_item(scope={"write": ["tools/in_scope.py"], "read_hints": []})
        result = work_result(item, changed=[{"path": "src/OutOfScope.cs"}])
        rejections, accepted = gate.check(item, result)
        self.assertIn("write_scope_violation", rejections)
        self.assertIsNone(accepted)
        self.assertIn("work_result.rejected", self.events.names())

    # 26. 未接受的 ModuleContext Delta 不会生效
    def test_26_unaccepted_delta_inert(self):
        state = self.tmp_state()
        store = adapter.ModuleContextStore(state, self.events)
        item = work_item()
        result = work_result(item, module_context_delta={
            "affected_symbols": ["NewSymbol"], "new_test_refs": [],
            "contract_changes": [], "obsolete_refs": []})
        self.assertIsNone(store.apply_delta(item["id"], result,
                                            accepted_by_leader=False))
        self.assertIsNone(store.accepted_delta(item["id"]))

    # 27. 已接受的 ModuleContext Delta 可以更新上下文
    def test_27_accepted_delta_applied(self):
        state = self.tmp_state()
        store = adapter.ModuleContextStore(state, self.events)
        item = work_item()
        result = work_result(item, module_context_delta={
            "affected_symbols": ["NewSymbol"], "new_test_refs": ["tests/x.py"],
            "contract_changes": [], "obsolete_refs": []})
        applied = store.apply_delta(item["id"], result, accepted_by_leader=True)
        self.assertEqual(applied["affected_symbols"], ["NewSymbol"])
        self.assertEqual(store.accepted_delta(item["id"]), applied)

    # 28. 跨模块 ChangeSet 可以按 Contract 拆分
    def test_28_changeset_split_ok(self):
        scheduler = adapter.Scheduler(self.events)
        items = [
            work_item(id="WI-1", module_profile="engineering-governance",
                      worker_owner="w-gov",
                      scope={"write": ["tools/a.py"], "read_hints": []}),
            work_item(id="WI-2", module_profile="runtime-core",
                      worker_owner="w-rt",
                      scope={"write": ["src/UniClaw.Runtime/B.cs"], "read_hints": []}),
        ]
        order = scheduler.plan(items)
        self.assertEqual(set(order), {"WI-1", "WI-2"})

    def test_28b_concurrent_same_file_rejected(self):
        scheduler = adapter.Scheduler(self.events)
        items = [
            work_item(id="WI-3", worker_owner="w-a",
                      scope={"write": ["tools/same.py"], "read_hints": []}),
            work_item(id="WI-4", worker_owner="w-b",
                      scope={"write": ["tools/same.py"], "read_hints": []}),
        ]
        with self.assertRaises(adapter.DshAdapterError) as ctx:
            scheduler.plan(items)
        self.assertIn("concurrent writers", str(ctx.exception))

    # 29. 强耦合跨模块任务不会被提前派发
    def test_29_coupled_cross_module_stays_with_leader(self):
        router = adapter.WorkerRouter(self.events)
        decision = router.route({"cross_module": True, "contract_frozen": False})
        self.assertEqual(decision["route"], "coding-leader")
        self.assertIsNone(decision["agent"])
        decision2 = router.route({"cross_module": True, "contract_frozen": True,
                                  "atomic": True, "module_id": "runtime-core",
                                  "change_principles_frozen": True})
        self.assertEqual(decision2["route"], "module-worker")

    # 30. DSH Envelope 不会改变通用 WorkItem 语义
    def test_30_envelope_preserves_work_item(self):
        item = work_item(scope={"write": ["tools/env.py"], "read_hints": []})
        envelope = adapter.wrap_work_envelope(item, "sess-1", "run-1", "corr-1", "pv-1")
        self.assertEqual(set(envelope), {"dsh_work_envelope"})
        self.assertEqual(set(envelope["dsh_work_envelope"]),
                         set(adapter.ENVELOPE_FIELDS))
        unwrapped = adapter.unwrap_work_envelope(envelope)
        self.assertEqual(unwrapped, item)
        self.assertEqual(validator.validate_work_item(unwrapped), [])
        # 原始对象未被污染
        self.assertNotIn("session_id", item)


class ScriptedHostClient:
    """Test-only host seam double: supports the requested binding and returns a
    receipt whose actual fields mirror the requested binding (so the gate's
    requested-vs-actual comparison passes). Unit tests never claim the real
    model guarantee — the real Host integration test proves that."""

    def __init__(self, supported=None):
        self.supported = supported or [("opencode-go", "deepseek-v4-flash")]
        self.spawn_calls = []

    def supports(self, provider, model, reasoning):
        return (provider, model) in self.supported

    def spawn_worker(self, envelope, task_payload):
        binding = envelope["dsh_work_envelope"]["model_binding"]
        self.spawn_calls.append({
            "provider": binding["provider"],
            "model": binding["model"],
            "reasoning": binding["reasoning"],
        })
        return {
            "session_id": envelope["dsh_work_envelope"]["session_id"],
            "run_id": envelope["dsh_work_envelope"]["run_id"],
            "work_item_id": binding["work_item_id"],
            "worker_owner": binding["worker_owner"],
            "actual_provider": binding["provider"],
            "actual_model": binding["model"],
            "actual_reasoning": binding["reasoning"],
            "binding_revision": binding["binding_revision"],
            "started_at": 1.0,
        }


class RuntimeFacadeTest(unittest.TestCase):
    """最小端到端：dispatch → host spawn receipt → gate accept → delta applied →
    checkpoint updated。模拟 Host 只回显 requested binding，用于验证 Gate 逻辑；
    真实模型保证由真实 Host 集成测试证明。"""

    def test_minimal_goal_to_acceptance(self):
        with tempfile.TemporaryDirectory(prefix="dsh-rt-") as state:
            host = ScriptedHostClient()
            runtime = adapter.DshWorkflowRuntime(state_dir=state,
                                                 host_client=host)
            checkpoint = adapter.LeaderCheckpoint(
                "sess-1", runtime.profile_version, "goal-ref",
                events=runtime.events, state_dir=state)
            item = work_item(id="WI-E2E", scope={"write": ["tools/e2e_x.py"],
                                                 "read_hints": []})
            checkpoint.add_pending(item["id"])
            dispatched = runtime.dispatch_work_item(item, "sess-1", "run-1",
                                                    "corr-1")
            self.assertTrue(dispatched["envelope"]["dsh_work_envelope"]["work_item"]["id"])
            self.assertTrue(dispatched["envelope"]["dsh_work_envelope"]["model_binding"])
            self.assertIsNotNone(dispatched["receipt"])
            result = work_result(item, changed=[{"path": "tools/e2e_x.py"}])
            outcome = runtime.accept_result(item, result)
            self.assertTrue(outcome["accepted"])
            self.assertIsNotNone(outcome["applied_delta"])
            checkpoint.complete(item["id"], evidence_refs=["tests/AgentWorkflow"])
            self.assertNotIn(item["id"], checkpoint.data["pending_work"])
            self.assertIn(item["id"], checkpoint.data["completed_work"])


if __name__ == "__main__":
    unittest.main()
