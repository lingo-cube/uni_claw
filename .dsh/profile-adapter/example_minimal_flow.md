# 最小示例：Goal → 模块 Worker → 结果接受

> 本示例演示一次完整的 UniFlow DSH 路径：Leader 理解目标 → 冻结决策 → 生成并
> 校验 WorkItem → 单播唯一 Worker → DSH 自动装载 ModuleContext → Worker 返回
> WorkResult → Leader 接收门 → ModuleContext Delta 接受 → Checkpoint 更新。
> 可直接执行：`python3 .dsh/profile-adapter/example_minimal_flow.py`

```python
#!/usr/bin/env python3
"""Minimal goal → module-worker → acceptance flow through the DSH adapter.

演示强制闭环：DispatchGate → Envelope（含 requested binding）→ Host seam
（示例用回显 binded 的 ScriptedHostClient 展示 Gate 逻辑）→ 回执核对 →
accept_result。真实模型保证必须由真实 Host (tools/dsh_host_integration_check.py)
证明，本示例不模拟宣称模型。"""
import importlib.util, json, tempfile
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location(
    "dsh_profile_adapter", REPO_ROOT / "tools/dsh_profile_adapter.py")
adapter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(adapter)


class ScriptedHostClient:
    """Test-only seam: echoes the envelope's requested binding as the receipt."""

    def __init__(self):
        self.spawn_calls = []

    def supports(self, provider, model, reasoning):
        return True

    def spawn_worker(self, envelope, task_payload):
        binding = envelope["dsh_work_envelope"]["model_binding"]
        self.spawn_calls.append({"provider": binding["provider"],
                                 "model": binding["model"],
                                 "reasoning": binding["reasoning"]})
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


# 1) Leader (zai glm-5.2) 分析目标，确定性事实用 tool-only（不调用模型）
runtime = adapter.DshWorkflowRuntime(
    state_dir=tempfile.mkdtemp(prefix="dsh-ex-"),
    host_client=ScriptedHostClient())
print("leader primary:", runtime.binding.leader_binding())
print("tool-only route:", runtime.router.route({"deterministic": True},
                                               binding=runtime.binding))

# 2) Leader 冻结决策并生成一个 WorkItem（真实校验由上游 validator 裁决）
work_item = {
    "id": "WI-EX-001", "change_set_id": "CS-EX",
    "base_revision": runtime.source.source_revision,
    "role_profile": "module-worker", "execution_profile": "development",
    "module_profile": "engineering-governance", "worker_owner": "module-worker-1",
    "objective": "为治理工具补充一个只读统计子命令",
    "semantic_brief": {
        "summary": "治理工具缺少一个只读统计入口。本任务在授权路径内新增该子命令并用现有测试门验证。",
        "core_points": ["修改集中在tools授权路径", "职责边界不变", "由AgentWorkflow测试门确认"],
    },
    "scope": {"write": ["tools/example_stat.py"], "read_hints": []},
    "anchors": [{"path": "tools/example_stat.py", "symbol": "main"}],
    "change_principles": ["复用现有validator抽象，不引入新边界"],
    "contract_refs": [],
    "acceptance": ["tests/AgentWorkflow 通过"],
    "forbidden": ["禁止修改.ai权威文件"],
    "escalation": ["架构问题返回Leader"],
    "leader_decisions_frozen": True, "unresolved_architecture": [],
}

# 3) 唯一合法派发入口：DispatchGate → binding 解析 → Envelope(model_binding) → Host spawn → 预执行回执核对
dispatched = runtime.dispatch_work_item(work_item, "sess-1", "run-1", "corr-1")
manifest = dispatched["manifest"]
binding = dispatched["envelope"]["dsh_work_envelope"]["model_binding"]
print("requested binding:", binding["provider"], binding["model"], binding["reasoning"])
print("host spawn saw:", runtime.host.spawn_calls)
print("receipt present:", dispatched["receipt"] is not None)
print("module context key:", manifest["profile_context_key"][:16], "…")

# 4) DeepSeek-V4 worker 在边界内执行后返回 WorkResult（含 diff 与测试证据）
result = {
    "id": "WI-EX-001", "status": "DONE",
    "base_revision": work_item["base_revision"],
    "changed": [{"path": "tools/example_stat.py"}],
    "verification": [{"kind": "unittest", "ref": "tests/AgentWorkflow"}],
    "module_context_delta": {"affected_symbols": ["main"],
                             "new_test_refs": [], "contract_changes": [],
                             "obsolete_refs": []},
    "deviations": [], "unresolved": [],
}

# 5) Leader 接收门：schema → revision → owner → 实际修改 vs scope → 回执核对 → Accept
outcome = runtime.accept_result(work_item, result)
print("accepted:", outcome["accepted"], "| delta:", outcome["applied_delta"])

# 6) Checkpoint：pending → completed，引用级状态，不含完整推理
cp = adapter.LeaderCheckpoint("sess-1", runtime.profile_version, "goal-ref",
                              events=runtime.events)
cp.add_pending(work_item["id"]); cp.complete(work_item["id"])
print("checkpoint revision:", cp.data["revision"])
print("events:", runtime.events.names()[:6], "…")
```

运行输出（节选）：

```text
leader primary: {'provider': 'zai', 'model': 'glm-5.2', 'reasoning': 'high', ...}
tool-only route: {'route': 'tool-only', ...}
requested binding: opencode-go deepseek-v4-flash high
host spawn saw: [{'provider': 'opencode-go', 'model': 'deepseek-v4-flash', 'reasoning': 'high'}]
receipt present: True
module context key: 9f3c…  
accepted: True | delta: {'affected_symbols': ['main'], ...}
checkpoint revision: 2
events: ['profile.source.validated', 'profile.loaded', 'workflow.route.selected', ...]
```
