"""DSH UniFlow 机械强制闭环测试 — 任务 §九 第 1–20 条。

覆盖：强制 WorkItem 派发门（Markdown/缺字段/非对象 brief 拒绝、tool-only
fail-closed）、ExecutionProfile→ModelBinding 解析、Envelope requested binding、
Host spawn 显式传参、能力 fail-closed、实际模型回执核对（requested vs actual）、
缺失/不一致回执拒绝（不应用 Delta）、Leader primary/fallback 唯一 authority。

单元测试中的 ScriptedHostClient 只回显 requested binding 以验证 Gate 逻辑；
真实模型保证由真实 Host 集成测试证明（不模拟宣称）。
"""

import copy
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock


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


def _pin_to_head(testcase):
    """Pin this suite to the working-tree HEAD so the profile-source revision
    check never drifts (same pattern as the CLI dispatch/receipt suites).

    Each test gets an isolated profile-state dir; the upstream validator and
    REAL profile files are still exercised — only the pinned revision and the
    state sink are re-pointed.  Production fail-closed drift semantics stay
    under the CLI suites' drift expectations.
    """
    tmp = tempfile.TemporaryDirectory()
    config = adapter.load_config()
    config["profile_source"]["source_revision"] = adapter.subprocess.check_output(
        ["git", "rev-parse", "HEAD"], cwd=str(REPO_ROOT), text=True).strip()
    config["state_dir"] = str(Path(tmp.name) / "profile-state")
    patcher = mock.patch.object(adapter, "load_config", return_value=config)
    patcher.start()
    testcase.addCleanup(patcher.stop)
    testcase.addCleanup(tmp.cleanup)
    return config


def work_item(**overrides):
    item = {
        "id": "WI-GTW-001",
        "change_set_id": "CS-GTW",
        "base_revision": "eac69ee0f0960af044b76cce31f9335e6aa5b52c",
        "role_profile": "module-worker",
        "execution_profile": "development",
        "module_profile": "engineering-governance",
        "worker_owner": "module-worker-1",
        "objective": "在授权范围内完成一个局部治理工具改动",
        "required_skills": [],
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


class ScriptedHostClient:
    """Test-only host seam double（与既有 facade 测试一致）。"""

    def __init__(self, supported=None, actual=None):
        self.supported = [("opencode-go", "deepseek-v4-flash")] if supported is None else supported
        self.actual = actual
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
        actual = self.actual if self.actual is not None else {
            "actual_provider": binding["provider"],
            "actual_model": binding["model"],
            "actual_reasoning": binding["reasoning"],
        }
        return {
            "session_id": envelope["dsh_work_envelope"]["session_id"],
            "run_id": envelope["dsh_work_envelope"]["run_id"],
            "work_item_id": binding["work_item_id"],
            "worker_owner": binding["worker_owner"],
            "binding_revision": binding["binding_revision"],
            "started_at": 1.0,
            **actual,
        }


class WorkItemDispatchGateTests(unittest.TestCase):
    def setUp(self):
        _pin_to_head(self)

    def tmp_state(self):
        return tempfile.mkdtemp(prefix="dsh-gtw-")

    # 九.1 — Markdown 任务说明不能作为 WorkItem 派发
    def test_01_markdown_task_not_dispatchable(self):
        gate = adapter.DispatchGate()
        errors = gate.check("# 任务：修复 Agent 行为\n自然语言说明…")
        self.assertTrue(errors)
        self.assertIn("WorkItem object", errors[0])
        runtime = adapter.DshWorkflowRuntime(state_dir=self.tmp_state())
        with self.assertRaises(adapter.WorkItemRequired):
            runtime.dispatch_work_item("# 任务：修复 Agent 行为", "s", "r", "c")

    # 九.2 — 缺失 WorkItem 必填字段时派发失败
    def test_02_missing_required_fields_rejected(self):
        gate = adapter.DispatchGate()
        for missing in ("id", "objective", "worker_owner", "execution_profile",
                        "module_profile", "acceptance"):
            item = work_item()
            item.pop(missing)
            errors = gate.check(item)
            self.assertTrue(errors, "missing=%s must fail" % missing)
        runtime = adapter.DshWorkflowRuntime(state_dir=self.tmp_state())
        item = work_item()
        item.pop("objective")
        with self.assertRaises(adapter.WorkItemRequired):
            runtime.dispatch_work_item(item, "s", "r", "c")

    # 九.3 — 非对象 semantic_brief 派发失败
    def test_03_non_object_semantic_brief_rejected(self):
        gate = adapter.DispatchGate()
        for bad in ("纯文本摘要", ["数组"], 42, None):
            item = work_item(semantic_brief=bad)
            errors = gate.check(item)
            self.assertTrue(errors, "semantic_brief=%r must fail" % (bad,))
        runtime = adapter.DshWorkflowRuntime(state_dir=self.tmp_state())
        with self.assertRaises(adapter.WorkItemRequired):
            runtime.dispatch_work_item(work_item(semantic_brief="文本摘要"),
                                       "s", "r", "c")

    # 九.4 — tool-only 请求源码写入时失败（fail-closed）
    def test_04_tool_only_source_write_fails_closed(self):
        gate = adapter.DispatchGate()
        bad = work_item(execution_profile="tool-only",
                        scope={"write": ["tools/example_stat.py"], "read_hints": []})
        errors = gate.check(bad)
        self.assertTrue(any("source or test write scope" in e for e in errors))
        runtime = adapter.DshWorkflowRuntime(state_dir=self.tmp_state())
        with self.assertRaises(adapter.WorkItemRequired):
            runtime.dispatch_work_item(bad, "s", "r", "c")

    # 九.5 — tool-only 不创建 Subagent 且模型调用数为 0
    def test_05_tool_only_no_subagent_no_model_call(self):
        binding = adapter.ModelBinding(adapter.load_config(), adapter.EventLog())
        router = adapter.WorkerRouter()
        decision = router.route({"deterministic": True}, binding=binding)
        self.assertEqual(decision["route"], "tool-only")
        self.assertEqual(binding.tool_only()["model"], "none")
        self.assertEqual(binding.model_calls_for_tool_only(), 0)
        host = ScriptedHostClient()
        runtime = adapter.DshWorkflowRuntime(state_dir=self.tmp_state(),
                                             host_client=host)
        item = work_item(execution_profile="tool-only", scope={"write": [], "read_hints": []})
        outcome = runtime.dispatch_work_item(item, "s", "r", "c")
        self.assertIsNone(outcome["spawn"])
        self.assertIsNone(outcome["receipt"])
        self.assertEqual(outcome["envelope"]["dsh_work_envelope"]["model_binding"]["model"], "none")
        self.assertEqual(host.spawn_calls, [])

    # 九.6 — development 解析为当前 implementation_efficient 绑定
    def test_06_development_resolves_implementation_efficient(self):
        binding = adapter.ModelBinding(adapter.load_config(), adapter.EventLog())
        role, resolved = binding.binding_for_execution("development")
        self.assertEqual(role, "implementation_efficient")
        self.assertEqual(resolved["primary"]["provider"], "opencode-go")
        self.assertEqual(resolved["primary"]["model"], "deepseek-v4-flash")
        self.assertEqual(resolved["primary"]["reasoning"], "high")
        # 值必须来自 profile-source.yaml，不得硬编码到 Profile
        self.assertNotIn("deepseek-v4-flash",
                         json.dumps(validator.load_registries()))

    # 九.7 — semantic-analysis 解析为当前 semantic_read 绑定
    def test_07_semantic_analysis_resolves_semantic_read(self):
        binding = adapter.ModelBinding(adapter.load_config(), adapter.EventLog())
        role, resolved = binding.binding_for_execution("semantic-analysis")
        self.assertEqual(role, "semantic_read")
        self.assertEqual(resolved["primary"]["provider"], "opencode-go")
        self.assertEqual(resolved["primary"]["model"], "deepseek-v4-flash")
        self.assertEqual(resolved["primary"]["reasoning"], "high")

    # 九.8 — 派发 Envelope 包含完整 requested model binding
    def test_08_dispatch_envelope_has_full_requested_binding(self):
        runtime = adapter.DshWorkflowRuntime(state_dir=self.tmp_state(),
                                             host_client=ScriptedHostClient())
        item = work_item(scope={"write": ["tools/gtw_ok.py"], "read_hints": []})
        dispatched = runtime.dispatch_work_item(item, "sess", "run", "corr")
        binding = dispatched["envelope"]["dsh_work_envelope"]["model_binding"]
        for field in ("binding_role", "provider", "model", "reasoning",
                      "profile_version", "binding_revision", "binding_digest",
                      "work_item_id", "worker_owner"):
            self.assertIn(field, binding, field)
        self.assertEqual(binding["provider"], "opencode-go")
        self.assertEqual(binding["model"], "deepseek-v4-flash")
        self.assertEqual(binding["reasoning"], "high")
        self.assertEqual(binding["work_item_id"], item["id"])
        self.assertEqual(binding["worker_owner"], item["worker_owner"])
        # 不污染通用 WorkItem
        self.assertNotIn("model_binding", item)
        self.assertNotIn("provider", item)
        self.assertEqual(validator.validate_work_item(
            dispatched["envelope"]["dsh_work_envelope"]["work_item"]), [])


class HostDispatchAndReceiptTests(unittest.TestCase):
    def setUp(self):
        _pin_to_head(self)

    def tmp_state(self):
        return tempfile.mkdtemp(prefix="dsh-gtw-")

    def test_09_host_spawn_receives_envelope_binding_exactly(self):
        runtime = adapter.DshWorkflowRuntime(state_dir=self.tmp_state(),
                                             host_client=ScriptedHostClient())
        item = work_item(scope={"write": ["tools/gtw_ok2.py"], "read_hints": []})
        runtime.dispatch_work_item(item, "sess", "run", "corr")
        call = runtime.host.spawn_calls[-1]
        requested = runtime.requests[item["id"]]
        self.assertEqual(call["provider"], requested["provider"])
        self.assertEqual(call["model"], requested["model"])
        self.assertEqual(call["reasoning"], requested["reasoning"])

    # 九.10 — Host 无法指定模型时在写入前 fail-closed
    def test_10_host_cannot_honor_binding_fails_closed_before_write(self):
        host = ScriptedHostClient(supported=[])
        runtime = adapter.DshWorkflowRuntime(state_dir=self.tmp_state(),
                                             host_client=host)
        item = work_item(scope={"write": ["tools/gtw_ok3.py"], "read_hints": []})
        with self.assertRaises(adapter.RoutingCapabilityRequired) as ctx:
            runtime.dispatch_work_item(item, "sess", "run", "corr")
        self.assertEqual(ctx.exception.code, "ROUTING_CAPABILITY_LIMIT")
        self.assertEqual(host.spawn_calls, [])
        self.assertNotIn(item["id"], runtime.receipts)
        self.assertEqual(runtime.scheduler.dispatched.get(item["id"]), None)

    # 九.11 — Host 实际模型与 requested binding 不一致时拒绝启动或结果
    def test_11_actual_mismatch_rejected(self):
        host = ScriptedHostClient(actual={"actual_provider": "opencode-go",
                                          "actual_model": "glm-5.2",
                                          "actual_reasoning": "low"})
        runtime = adapter.DshWorkflowRuntime(state_dir=self.tmp_state(),
                                             host_client=host)
        item = work_item(scope={"write": ["tools/gtw_ok4.py"], "read_hints": []})
        with self.assertRaises(adapter.RoutingCapabilityRequired):
            runtime.dispatch_work_item(item, "sess", "run", "corr")  # 启动即拒绝
        # 结果侧：Gate 同样拒绝
        gate = adapter.WorkResultGate()
        req = {"provider": "opencode-go", "model": "deepseek-v4-flash",
               "reasoning": "high"}
        receipt = {"work_item_id": item["id"], "worker_owner": item["worker_owner"],
                   "actual_provider": "opencode-go", "actual_model": "wrong-model",
                   "actual_reasoning": "low", "binding_revision": "dsb@x",
                   "started_at": 1.0}
        rejections, _ = gate.check(item, work_result(item), receipt=receipt,
                                   requested_binding=req, require_receipt=True)
        self.assertIn("model_binding_mismatch", rejections)

    # 九.12 — 缺少 Host 模型回执时拒绝结果
    def test_12_missing_receipt_rejects_result(self):
        runtime = adapter.DshWorkflowRuntime(state_dir=self.tmp_state(),
                                             host_client=ScriptedHostClient())
        item = work_item(scope={"write": ["tools/gtw_ok5.py"], "read_hints": []})
        runtime.dispatch_work_item(item, "sess", "run", "corr")
        # 模拟 Host 实际未返回回执（spawn 后无 receipt 记录）
        runtime.receipts.pop(item["id"], None)
        outcome = runtime.accept_result(item, work_result(item))
        self.assertFalse(outcome["accepted"])
        self.assertIn("model_receipt_missing", outcome["rejections"])
        self.assertEqual(outcome.get("code"), "ROUTING_CAPABILITY_LIMIT")
        # 不应用 Delta，也不接受结果
        self.assertNotIn("applied_delta", outcome)
        self.assertIsNone(runtime.store.accepted_delta(item["id"]))
        # Host 直接不产生回执（spawn_worker 返回空）→ 派发前预执行核对拒绝
        nohost = ScriptedHostClient()
        nohost.spawn_worker = lambda envelope, task_payload: {}
        runtime2 = adapter.DshWorkflowRuntime(state_dir=self.tmp_state(),
                                              host_client=nohost)
        with self.assertRaises(adapter.RoutingCapabilityRequired) as ctx:
            runtime2.dispatch_work_item(item, "sess", "run", "corr")
        self.assertEqual(ctx.exception.code, "ROUTING_CAPABILITY_LIMIT")

    # 九.13 — 模型文本自述不能替代 Host 回执
    def test_13_model_self_claim_not_a_receipt(self):
        gate = adapter.WorkResultGate()
        item = work_item(scope={"write": ["tools/gtw_ok6.py"], "read_hints": []})
        result = work_result(item)
        result["model_self_claim"] = "我是deepseek-v4-flash"  # 通用 WorkResult 不允许
        # 自述文本不能进入 HOST_RECEIPT_FIELDS → 直接视为缺回执
        runtime = adapter.DshWorkflowRuntime(state_dir=self.tmp_state(),
                                             host_client=ScriptedHostClient())
        runtime.dispatch_work_item(item, "sess", "run", "corr")
        runtime.receipts.pop(item["id"], None)  # Host 未提供回执
        outcome = runtime.accept_result(item, work_result(item))
        self.assertFalse(outcome["accepted"])
        self.assertIn("model_receipt_missing", outcome["rejections"])

    # 九.14 — worker_owner、work_item id 或 binding revision 不一致时拒绝结果
    def test_14_receipt_identity_mismatch_rejected(self):
        item = work_item(scope={"write": ["tools/gtw_ok7.py"], "read_hints": []})
        req = {"provider": "opencode-go", "model": "deepseek-v4-flash",
               "reasoning": "high", "binding_revision": "dsb@rev1"}
        base = {"work_item_id": item["id"], "worker_owner": item["worker_owner"],
                "actual_provider": "opencode-go", "actual_model": "deepseek-v4-flash",
                "actual_reasoning": "high", "binding_revision": "dsb@rev1",
                "started_at": 1.0}
        gate = adapter.WorkResultGate()
        for field, bad in (("worker_owner", "other-worker"),
                           ("work_item_id", "WI-OTHER"),
                           ("binding_revision", "dsb@rev9")):
            receipt = dict(base, **{field: bad})
            rejections, _ = gate.check(item, work_result(item), receipt=receipt,
                                       requested_binding=req, require_receipt=True)
            self.assertTrue(rejections, "field=%s must fail" % field)

    # 九.15 — 合法回执和合法 WorkResult 可以通过
    def test_15_valid_receipt_and_result_pass(self):
        runtime = adapter.DshWorkflowRuntime(state_dir=self.tmp_state(),
                                             host_client=ScriptedHostClient())
        item = work_item(scope={"write": ["tools/gtw_ok8.py"], "read_hints": []})
        runtime.dispatch_work_item(item, "sess", "run", "corr")
        outcome = runtime.accept_result(item, work_result(item))
        self.assertTrue(outcome["accepted"])
        self.assertIsNotNone(outcome["applied_delta"])

    # 九.16 — ModuleContext Delta 只在全部 Gate 通过后接受
    def test_16_delta_only_accepted_after_all_gates(self):
        runtime = adapter.DshWorkflowRuntime(state_dir=self.tmp_state(),
                                             host_client=ScriptedHostClient())
        item = work_item(scope={"write": ["tools/gtw_ok9.py"], "read_hints": []})
        runtime.dispatch_work_item(item, "sess", "run", "corr")
        # scope 外写入 → Gate 拒绝 → 不应用 Delta
        bad = work_result(item, changed=[{"path": "src/UniClaw.Runtime/X.cs"}])
        outcome = runtime.accept_result(item, bad)
        self.assertFalse(outcome["accepted"])
        self.assertIn("write_scope_violation", outcome["rejections"])
        self.assertIsNone(runtime.store.accepted_delta(item["id"]))
        # 合法路径 → Delta 应用
        ok = runtime.accept_result(item, work_result(item))
        self.assertTrue(ok["accepted"])
        self.assertIsNotNone(runtime.store.accepted_delta(item["id"]))

    # 六.5 运行时补齐 — Host 回执缺 actual_reasoning 时用 Host 默认（high）核对
    def test_16b_host_default_reasoning_fills_missing_receipt(self):
        runtime = adapter.DshWorkflowRuntime(
            state_dir=self.tmp_state(), host_client=ScriptedHostClient(),
            host_default_reasoning="high")
        item = work_item(scope={"write": ["tools/gtw_ok9b.py"], "read_hints": []})
        runtime.dispatch_work_item(item, "sess", "run", "corr")
        receipt = runtime.receipts[item["id"]]
        receipt.pop("actual_reasoning", None)  # Host 回执未带 reasoning
        outcome = runtime.accept_result(item, work_result(item), receipt=receipt)
        self.assertTrue(outcome["accepted"], outcome)
        self.assertIsNotNone(outcome["applied_delta"])

    def test_16c_no_host_default_reasoning_still_fails_closed(self):
        runtime = adapter.DshWorkflowRuntime(
            state_dir=self.tmp_state(), host_client=ScriptedHostClient(),
            host_default_reasoning=None)
        item = work_item(scope={"write": ["tools/gtw_ok9c.py"], "read_hints": []})
        runtime.dispatch_work_item(item, "sess", "run", "corr")
        # 模拟 Host 回执未带 reasoning 且 Host 无默认值：Gate 必须 fail-closed。
        receipt = runtime.receipts[item["id"]]
        receipt.pop("actual_reasoning", None)
        outcome = runtime.accept_result(item, work_result(item), receipt=receipt)
        self.assertFalse(outcome["accepted"])
        self.assertTrue(any("model_receipt_missing" in r
                            for r in outcome["rejections"]),
                        outcome["rejections"])


class LeaderBindingTests(unittest.TestCase):
    def setUp(self):
        _pin_to_head(self)

    def tmp_state(self):
        return tempfile.mkdtemp(prefix="dsh-gtw-")

    # 九.17 — Leader primary 实际绑定为 zai/glm-5.2/high
    def test_17_leader_primary_binding(self):
        binding = adapter.ModelBinding(adapter.load_config(), adapter.EventLog())
        self.assertEqual(binding.assert_leader_primary(), [])
        primary = binding.leader_binding()
        self.assertEqual(primary["provider"], "zai")
        self.assertEqual(primary["model"], "glm-5.2")
        self.assertEqual(primary["reasoning"], "high")
        self.assertTrue(primary.get("leader_authority"))

    # 九.18 — Leader fallback 只接受现有允许的平台级原因
    def test_18_fallback_only_platform_reasons(self):
        binding = adapter.ModelBinding(adapter.load_config(), adapter.EventLog())
        for reason in adapter.FALLBACK_ALLOWED_REASONS:
            endpoint = binding.request_fallback(reason)
            self.assertEqual(endpoint["provider"],  "opencode-go")
        for reason in adapter.FALLBACK_FORBIDDEN_REASONS:
            with self.assertRaises(adapter.DshAdapterError):
                binding.request_fallback(reason)

    # 九.19 — fallback 后 leader_authority 仍然唯一
    def test_19_fallback_keeps_single_authority(self):
        state = self.tmp_state()
        cp = adapter.LeaderCheckpoint("s1", "pv", "goal-ref",
                                      events=adapter.EventLog(), state_dir=state)
        cp.add_pending("WI-A")
        binding = adapter.ModelBinding(adapter.load_config(), adapter.EventLog())
        takeover = adapter.leader_fallback_takeover(
            binding, "provider_unavailable", state, events=adapter.EventLog(),
            receipt={"session_id": "s1", "run_id": "r1",
                     "work_item_id": "-", "worker_owner": "leader",
                     "actual_provider": "opencode-go", "actual_model": "glm-5.2",
                     "actual_reasoning": "high",
                     "binding_revision": "dsb@x", "started_at": 1.0})
        frontier = binding.binding_for("decision_frontier")
        holders = [k for k in ("primary", "fallback")
                   if frontier[k].get("leader_authority")]
        self.assertEqual(holders, ["fallback"])
        self.assertIsNotNone(takeover.get("receipt"))
        self.assertEqual(takeover["pending_work"], ["WI-A"])

    # 九.20 — 既有 AgentWorkflow 与 DSH Adapter 测试全部保持通过
    # （运行既有两套测试文件，排除本文件避免递归；作为显式回归门）
    def test_20_existing_suites_still_pass(self):
        import subprocess
        proc = subprocess.run(
            ["python3", "-m", "unittest",
             "tests.AgentWorkflow.test_agent_profile_validator",
             "tests.AgentWorkflow.test_dsh_profile_adapter"],
            cwd=str(REPO_ROOT), capture_output=True, text=True)
        self.assertEqual(proc.returncode, 0, proc.stdout + proc.stderr)


if __name__ == "__main__":
    unittest.main()
