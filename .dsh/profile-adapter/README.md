# DSH Profile Adapter — 使用文档

> DSH 是 UniClaw 通用 Profile Core 的**消费者和运行适配器**，不是 Profile 的第二权威来源。
> 所有 Profile 语义（合并优先级、WorkItem/WorkResult 校验、模块归属、上下文键）由
> `tools/agent_profile_validator.py` 裁决；本适配器只添加 DSH 运行关注点。
> OpenSpec change: `openspec/changes/dsh-uniflow-profile-adapter/`

## 架构

```text
UniClaw Profile Core (.ai/profiles, .ai/schemas, agent_profile_validator.py)
        ↓  read-only, version-gated
DSH ProfileSource (.dsh/profile-adapter/profile-source.yaml)
        ↓
DSH ProfileAdapter (compose → AgentProfile; 冲突 → LeaderDecisionRequired)
        ↓
DSH WorkflowRuntime (Router / Scheduler / ResultGate / Checkpoint / Events)
        ↓
ModelBinding (zai glm-5.2 primary · opencode-go glm-5.2 fallback · opencode-go deepseek-v4-flash worker · tool-only none)
```

> 绑定中的 provider 名以本机 DSH Host 实际注册的路由为准：`~/.dsh/settings.yaml`
> 下该 Host 注册的路由为 `opencode-go`（`opencode` 未被注册，派发会以
> `NO_ADAPTER` fail-closed）。provider/model 值一律由
> `profile-source.yaml` 的 `model_bindings` 解析，不得硬编码进
> RoleProfile / ExecutionProfile / WorkItem / Worker 提示词。

## 组件与上游机制映射

| DSH 组件 | 职责 | 委托的上游机制 |
|---|---|---|
| `ProfileSource` | 钉扎 root/schema_version/revision，激活前跑 `validation_command` | `load_registries` + validator PASS 门 |
| `ProfileAdapter` | `AgentProfile = Role + Execution + Module` | `compose_profile` / `merge_mapping_strict` |
| `ModelBinding` | provider 绑定、leader authority 唯一 token、fallback 允许清单 | `.ai/model-routing.yaml` 逻辑角色（DSH 侧绑定独立维护） |
| `WorkerRouter` | 路由策略（§7 路由表） | `route_task` |
| `Scheduler` | 单 owner 单播、拒 fanout、拒并发同文件写、write-heavy 串行 | `validate_change_set` / `validate_work_item` |
| `ModuleContextStore` | ModuleContext 权威状态 + delta 接受门 | `build_context_manifest` / `accept_module_context_delta` |
| `WorkerSessionCache` | 按 `ProfileContextKey` 复用，仅追加 WorkItem/Contract/已接受 delta | `profile_context_key` |
| `WorkResultGate` | 顺序接收门 → Accept/Reject | `validate_work_result` / `_path_allowed` |
| `LeaderCheckpoint` | 引用/摘要级检查点，fallback 从最新 checkpoint 恢复 | —（DSH 持有） |
| `dsh_work_envelope` | 运行字段外层包裹，不污染通用 WorkItem | `validate_work_item`（解包后仍通过） |

## 命令

```bash
python3 tools/dsh_profile_adapter.py validate          # 版本门 + 上游验证 + 绑定装配
python3 -m unittest discover -s tests/AgentWorkflow -p 'test_*.py'
```

## Python API 快速参考

```python
import importlib.util
from pathlib import Path
REPO_ROOT = Path(".").resolve()
spec = importlib.util.spec_from_file_location(
    "dsh_profile_adapter", REPO_ROOT / "tools/dsh_profile_adapter.py")
adapter = importlib.util.module_from_spec(spec); spec.loader.exec_module(adapter)

runtime = adapter.DshWorkflowRuntime()                 # 激活时校验 source
runtime.binding.leader_binding()                       # zai glm-5.2 (authority)
runtime.binding.request_fallback("provider_unavailable")  # 仅限 5 种平台级原因
runtime.router.route({"deterministic": True})          # → tool-only, 不调用模型
runtime.dispatch_work_item(work_item, "sess", "run", "corr")  # 唯一合法派发入口
runtime.accept_result(work_item, work_result)
```

## 强制闭环（Apply increment A1–A10）

- `dispatch_work_item()` 只接受合法 JSON WorkItem；Markdown / 自然语言 /
  缺必填字段 / 非对象 `semantic_brief` 一律 `WorkItemRequired`。tool-only
  不创建 Subagent、model=none、禁止声明写入范围或语义判断请求。
- Envelope 的 `dsh_work_envelope.model_binding` 携带解析后的运行元数据
  （binding role / provider / model / reasoning / profile_version /
  binding_revision / binding_digest / work_item_id / worker_owner），
  通用 WorkItem 不被污染。
- Host seam（`DshHostClient`）从已校验 Envelope 显式读取
  provider/model/reasoning 传给 Subagent 创建；不支持时在任何文件修改前
  抛 `ROUTING_CAPABILITY_LIMIT`（默认 `CapabilityLimitedHostClient` 诚实
  fail-closed，不模拟成功回执）。
- 实际模型回执由 Host 生成（`HOST_RECEIPT_FIELDS`），模型正文自述不算；
  `read_host_receipt_from_session_log` 从 Host 会话日志 `request/header`
  事件读取真实回执。WorkResultGate 核对 requested vs actual + id + owner +
  revision；缺回执（`model_receipt_missing`）或任一不一致
  （`model_binding_mismatch`）→ 拒绝结果、不应用 ModuleContext Delta、
  返回 `ROUTING_CAPABILITY_LIMIT`，无静默 fallback。
- reasoning 补齐：若 binding 声明 reasoning 而 Host 回执未带该字段（当前
  spawn seam 不传递 effort），运行时用 Host 侧默认
  （`agent-default-model.reasoningEffort`，本部署为 high）补齐参与核对；
  Host 无默认时仍 fail-closed（`model_receipt_missing`）。该默认值来自 Host
  配置，非模型自述；可经 `DshWorkflowRuntime(host_default_reasoning=...)`
  显式覆盖。
- Leader 绑定：`record_leader_receipt` + `assert_leader_primary`
  （zai/glm-5.2/high）；当前会话实际回执不满足时必须如实 fail-closed，
  不得以“计划使用某模型”冒充已绑定。
- Spawn seam 审计：`audit_subagent_spawn_seams()` 列出 Host 原生 seam 与
  UniFlow 唯一合法入口；Worker 永远不能自行再 spawn
  （`Scheduler.request_spawn` 硬拒绝）。

## 纪律（映射到实施约束）

- 上游 Profile 只读（测试 04 断言字节 + mtime 不变）。
- fallback 只处理平台级失败；worker 测试失败/规则冲突等业务原因被显式拒绝。
- ModuleContext 权威状态在 DSH（`state/module-context.json`），模型会话只是缓存。
- 事件仅 12 个固定名字，落在 `state/events.jsonl`。
