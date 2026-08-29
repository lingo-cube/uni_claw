# DSH Profile Adapter — 使用文档

> DSH 是 UniClaw 通用 Profile Core 的**消费者和运行适配器**，不是 Profile 的第二权威来源。
> 所有 Profile 语义（合并优先级、WorkItem/WorkResult 校验、模块归属、上下文键）由
> `tools/agent_profile_validator.py` 裁决；本适配器只添加 DSH 运行关注点。
> 基础 OpenSpec change: `openspec/changes/dsh-uniflow-profile-adapter/`；当前 successor:
> `openspec/changes/dsh-uniflow-run-scoped-operational-state/`

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
| `ModuleContextStore` | ModuleContext 权威状态 + required Skill 解析 + delta 接受门 | `build_context_manifest` / `accept_module_context_delta` |
| `WorkerSessionCache` | 按 `ProfileContextKey` 复用，仅追加 WorkItem/Contract/已接受 delta | `profile_context_key` |
| `WorkResultGate` | 顺序接收门 → Accept/Reject | `validate_work_result` / `_path_allowed` |
| `LeaderCheckpoint` | 引用/摘要级检查点，fallback 从最新 checkpoint 恢复 | —（DSH 持有） |
| `dsh_work_envelope` | 运行字段外层包裹，不污染通用 WorkItem | `validate_work_item`（解包后仍通过） |

## 命令

```bash
python3 tools/dsh_profile_adapter.py validate          # 版本门 + 上游验证 + 绑定装配
python3 tools/dsh_profile_adapter.py dispatch <wi.json> [--session-id S] [--run-id R] [--record-dir D]
                                                       # 单命令派发收口：gate→binding→
                                                       # envelope→v2 原子 dispatch record
python3 tools/dsh_profile_adapter.py receipt <session-dir> --work-item-id ID --worker-owner OWNER [--session-id S --run-id R]
                                                       # 从持久 session 日志重建回执并核对
python3 -m unittest discover -s tests/AgentWorkflow -p 'test_*.py'
```

### 派发/回执语义（生命周期 L2/L3）

- `dispatch` 是 UniFlow 的唯一合法 CLI 派发入口：WorkItem 先过 DispatchGate，
  再解析 ModelBinding，原子产出 dispatch record（同目录临时文件 + `os.replace`，
  崩溃不留半写状态）。**dispatch record 是命令副作用而非记忆义务——无记录即未派发**。
  记录含 requested binding、profile_version、binding_revision 与
  `PENDING_SESSION_SPAWN` 回执状态；实际 Subagent 创建由 DSH 会话侧按
  envelope.model_binding 执行（`DeferredSessionSpawnHostClient`，非能力绕过：
  PENDING 回执不可能通过 WorkResultGate，验收前必须先 `receipt`）。
- `receipt` 用于 DSH 重启后回执恢复与验收前核对：从 Host 持久 session 日志
  （`request/header` 事件，模型自述不算）重建实际回执，与 dispatch record 中
  requested binding 核对。缺记录/缺日志/无 header → `RECEIPT_LOST`（exit 1，
  fail-closed 不猜）；字段不一致 → `RECEIPT_MISMATCH`（exit 1）。
- profile-source `source_revision` 钉扎漂移时 `validate`/`dispatch` 均拒绝
  （`STALE_PROFILE_SOURCE` 语义）——更新钉扎属版本管理动作，须与协议变更同 commit。

### v2 Run-scoped operational state

- 默认布局为 `system/events.jsonl`、
  `sessions/<session_id>/runs/<run_id>/events.jsonl` 和同一 Run 下的
  `dispatches/<work_item_id>.json`；`module-context.json` 与
  `leader-checkpoint.json` 保持 state 根目录不变。
- `session_id`、`run_id`、`correlation_id` 来自显式 dispatch 输入和 Envelope。Host
  session 目录名只定位日志并记录为 `host_session_id`，不会推导或覆盖 UniFlow Run
  身份；路径组件拒绝空值、`.`、`..`、绝对路径和路径分隔符。
- 新 dispatch 单写 v2，不迁移、不双写、不改写 v1。receipt 在提供 Session/Run
  时先精确读取 v2；v2 不存在时只读回退到 v1 flat record。显式 `--record-dir`
  仍保持 flat 兼容。
- `validate` 使用临时事件 sink，不创建或追加默认 operational state。历史 v1
  文件归档、保留和删除不属于本 change；回滚只需停止 v2 writer，保留旧 reader。

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
- WorkItem 的有序 `required_skills` 由上游 validator 只从 `.ai/skills`
  唯一解析；manifest 通过 `context_sources.required_skills` 给出 canonical 路径，
  并通过 `required_skill_context.documents` 按顺序携带完整正文与摘要，通过
  `required_skill_context.directive` 给出动作前完整读取和 fail-closed 要求。缺失、
  歧义、非法名称、正文为空或 payload 顺序不一致均在 Host spawn 前返回
  `REQUIRED_SKILL_UNAVAILABLE`。DSH 不维护第二套 Skill 正文，也不把 `.dsh` /
  `.agents` adapter 当作真相源。
- in-process Host 从 `spawn_worker(envelope, worker_payload)` 收到完整 payload；CLI
  延迟派发把同一 payload 原样写入 dispatch record，DSH 会话侧必须用它创建 Worker，
  不得只转发 envelope 或 Skill 名称。此门证明 adapter 交付；模型实际执行仍以 Host
  回执和集成证据为准。
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
- 事件仍仅 12 个固定名字；System 与 Run 按不可变身份上下文分流。历史 v1
  `state/events.jsonl` 于 2026-08-29 按 Human Gate 一次性复制拆分为
  `system/events.jsonl`、可回溯身份的 `sessions/<sid>/runs/<rid>/events.jsonl`
  与无身份 `legacy/events.jsonl`；原文件、flat dispatch record 与其余历史
  状态只读保留（授权与校验见 `docs/work/active/dsh-uniflow-v1-events-legacy-migration-gate.md`）。
