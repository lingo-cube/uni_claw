# UniClaw Profile-based Codex Coding Workflow

> DocumentType: EXECUTION_WORKFLOW
> Authority: NONE
> Version: 1.0.0
> Portable profiles: `.ai/profiles/*.json`
> Model binding source: `.ai/model-routing.yaml`
> Codex adapter: `.codex/config.toml` + `.codex/agents/*.toml`

本工作流复用现有 `.ai/development-protocol.md`、Task/Result Contract 与模型路由，不建立新的 Runtime、架构、协议或生命周期权威。Profile 只描述执行策略；冲突时服从根 `AGENTS.md` 的 Authority Order。

## 0. 触发与按需加载

统一触发格式：`执行 UniFlow：<任务内容>`；“按 UniFlow 执行”等语义明确的表达等价。
Codex 与 DSH 从根 `AGENTS.md` 识别该约定，识别后才加载本文，并按任务最小化读取
`.ai/profiles/`、`.ai/schemas/work-item.schema.json`、相关模块规则和权威契约。未触发
`UniFlow` 时，不得把本文、全部 Profile、全部 OpenSpec 或历史 Decisions 加入默认上下文。

`UniFlow` 的固定流程是：识别 Profile → 生成并校验 WorkItem → 单播匹配执行者 → 按
`acceptance` 验证。平台只负责适配：Codex 使用 `.codex/agents/*.toml`；DSH 使用自身
可用的执行/委派能力消费同一 Profile 与 WorkItem，不复制另一套工作流或约束。

## 1. 最小 Profile 模型

```text
AgentProfile = RoleProfile + ExecutionProfile + Optional ModuleProfile
AgentInvocation = AgentProfile + ModelBinding + ModuleContext + WorkItem
```

- RoleProfile：只保留 `coding-leader` 与 `module-worker` 两个稳定职责。
- ExecutionProfile：`development`、`test-authoring`、`verification`、`semantic-analysis`、`tool-only`。
- ModuleProfile：只覆盖会反复接收任务且边界稳定的真实模块。
- ModelBinding：继续由 `.ai/model-routing.yaml` 独立维护；Profile 不包含 provider/model。
- Codex TOML：只做适配，不是通用 Profile 真相源。

## 2. UniClaw 模块映射

| ModuleProfile | 主要 owned paths | 用途 |
|---|---|---|
| `runtime-core` | `src/UniClaw.Runtime/` | Agent、Container、Traversal、Recovery、World、Planning 与 Runtime 内部能力边界 |
| `runtime-integration` | Runtime Adapters、DriverHost、Harness、PhysicalHost、Vision.Host | 真实 IO、生产组合、协议/只读投影与 replay/test harness |
| `semantic-capability` | `src/UniClaw.Semantic.*`、`src/UniClaw.Settings.ValidationHost/` | 外部语义 provider、Settings 解释、Fast Semantic、评估与 Android 视觉读取 |
| `engineering-governance` | `.ai/`、`.codex/`、`.claude/`、`.dsh/`、`openspec/`、`docs/`、`scripts/`、`tools/` | Coding Harness、OpenSpec/知识治理与一致性工具 |

不按每个目录、项目或测试类别继续拆 Profile。测试目录通过每个 ModuleProfile 的 `test_paths` 绑定到对应生产模块；测试编写与验证的差异由 ExecutionProfile 表达。

当前生产代码中不存在 `SemanticBuyer`。真实边界是 `ISemanticProvider` / `IExternalSemanticCapability` → `SemanticEvidence` → `ISemanticEvidenceFusion` / Runtime admission → Fact/Runtime consumer。涉及这条链路的只读调查使用 `semantic-analysis`，不创建 `SemanticBuyer` Profile。

## 3. Codex Agent 映射

| Codex adapter | RoleProfile | ExecutionProfile | 默认模型 | sandbox |
|---|---|---|---|---|
| 主任务线程 | `coding-leader` | 由任务决定 | `gpt-5.6-sol` | 当前主线程策略 |
| `module-worker` | `module-worker` | `development` | `gpt-5.6-luna` | `workspace-write` |
| `test-author` | `module-worker` | `test-authoring` | `gpt-5.6-luna` | `workspace-write` |
| `verifier` | `module-worker` | `verification` | `gpt-5.6-luna` | `workspace-write`，仅允许生成测试产物 |
| `semantic-analyzer` | `module-worker` | `semantic-analysis` | `gpt-5.6-luna` | `read-only` |

`tool-only` 不启动 Agent。当前 Codex 支持显式 spawn 覆盖 reasoning，因此 custom agent 文件只固定 Luna 模型，默认 effort 来自 `.codex/config.toml` 的 `medium`；简单读取可显式用 `low`，困难但局部的任务可显式用 `high`。架构、生命周期、authority 或强耦合跨模块问题保留给 Sol Leader。

## 4. Leader 工作流

```text
1. Tool Only 获取确定性事实
2. 识别一个主要 ModuleProfile
3. 选择 ExecutionProfile
4. 冻结本次 change principles、contract 与 acceptance
5. 生成一个 WorkItem，填写精炼中文 semantic_brief，并指定唯一 worker_owner
6. 校验 WorkItem
7. 单播给一个 Worker；禁止同一 WorkItem fanout
8. 接收精简 WorkResult，独立核对仓库与验证证据
9. Leader 接受后才应用 ModuleContext Delta
10. Leader 完成最终验证与用户输出
```

以下情况不派发 Worker：

- 文件查找、符号查找、调用方列表、配置读取、明确命令、测试名称提取等确定性操作；
- 未冻结的公共接口、生命周期、Invariant、恢复路径或 authority 决策；
- 强耦合跨模块且契约尚不能先冻结；
- Sol 已持有精确热上下文且交接成本高于任务本身。

跨模块但契约可冻结时，由 Sol 先冻结公共契约，再按模块生成多个独立 WorkItem。每个 WorkItem 只有一个主要模块和一个 Owner；有依赖的 WorkItem 串行，同一文件不得由不同 Worker 同时写入，write-heavy 默认串行。

## 5. 自动上下文加载

Worker 不接收完整主线程。Leader 传递 WorkItem 后，Worker运行：

```bash
python3 tools/agent_profile_validator.py context --module <module-id> --execution <execution-id> --revision <base-revision>
```

生成的 manifest 包含：

```text
module-worker RoleProfile
+ selected ExecutionProfile
+ selected ModuleProfile
+ 目标路径生效的 AGENTS.md
+ ModuleProfile 中的 Specs / Decisions / Invariants selectors
+ entrypoints / public contracts / test gates
+ ProfileContextKey
```

动态占位符 `<selected-change>` 与 `<task-relevant-decision>` 必须由 WorkItem 的 `contract_refs` 和 `read_hints` 解析；禁止扫描全部 OpenSpec 或历史 Decisions。关键 change principles 必须直接写入 WorkItem，不能只引用 Profile。

## 6. WorkItem 与 WorkResult

- 输入 schema：`.ai/schemas/work-item.schema.json`
- 输出 schema：`.ai/schemas/work-result.schema.json`
- 既有详细 Task/Result Contract：`.ai/task-contract.md`、`.ai/result-contract.md`
- 现有构建与JSON序列化辅助：`tools/agent_profile_validator.py` 中的 `build_work_item`、`serialize_work_item`

WorkItem 强制包含 `semantic_brief`、`worker_owner` 与 `leader_decisions_frozen: true`。`module_profile` 是单个字符串而不是数组；存在未决架构问题时禁止派发。

`semantic_brief` 只帮助 Worker快速完成语义定位，不建立新约束。固定优先级为：

```text
Contract / Invariant
→ change_principles
→ forbidden
→ acceptance
→ scope
→ semantic_brief
```

如果摘要与正式约束冲突，Worker停止执行并返回 `BLOCKED_FOR_SPEC`，Reason 标记为 `RULE_CONFLICT`。`core_points` 中包含“必须、不得、只能、禁止、需要保持、不能改变”时，对应内容必须同时出现在 `change_principles`、`forbidden`、`acceptance` 或 `contract_refs` 指向的权威契约中。校验器只执行稳定的结构、长度和文本锚点检查，不做中文识别或语义相似度判断。

所有新生成的 `objective`、`semantic_brief`、`change_principles`、`acceptance`、`forbidden`、`escalation` 使用精炼中文；类型名、接口名、状态名和代码符号保留原文。不生成中英双语副本，不复制完整 Spec、代码或背景长文，且 `objective` 与 `semantic_brief.summary` 不得简单重复。

```json
{
  "id": "WI-EXAMPLE-001",
  "change_set_id": "CS-EXAMPLE",
  "base_revision": "<git-revision>",
  "role_profile": "module-worker",
  "execution_profile": "development",
  "module_profile": "runtime-core",
  "worker_owner": "module-worker-1",
  "objective": "修复已授权的Runtime局部行为",
  "semantic_brief": {
    "summary": "当前目标路径的行为与既有契约不一致。本任务需要在授权范围内恢复契约要求的结果，同时维持现有职责边界。",
    "core_points": [
      "修改集中在WorkItem指定的runtime-core路径",
      "现有ownership与lifecycle语义保持不变",
      "结果由指定测试门确认"
    ]
  },
  "scope": {"write": ["src/UniClaw.Runtime/<approved-path>"], "read_hints": []},
  "anchors": [{"path": "src/UniClaw.Runtime/<approved-path>", "symbol": "<symbol>"}],
  "change_principles": ["复用现有Runtime抽象，不引入新职责边界"],
  "contract_refs": ["<approved-spec-or-contract>"],
  "acceptance": ["指定测试门通过并保留验证证据"],
  "forbidden": ["禁止修改未授权路径或扩大架构范围"],
  "escalation": ["出现语义、架构、ownership、authority或invariant问题时返回Leader"],
  "leader_decisions_frozen": true,
  "unresolved_architecture": []
}
```

Worker 不返回完整推理、完整 diff、完整日志或自行创建的后续任务。WorkResult 中的 `module_context_delta` 只是候选；只有 Leader 明确接受后才能进入后续上下文。

## 7. 路由表

| 请求形态 | 路由 |
|---|---|
| 确定性读取/命令 | Tool Only |
| 单模块、原则完整、原子实现 | Luna `module-worker` |
| 单模块测试编写 | Luna `test-author` |
| 只运行测试与收集证据 | Luna `verifier` |
| SemanticEvidence / Fact / 消费边界分析 | Luna `semantic-analyzer` |
| 公共接口、生命周期、Invariant、恢复、authority 决策 | Sol Leader |
| 跨模块、契约可先冻结 | Sol 冻结契约后拆成多个单播 WorkItem |
| 跨模块、强耦合、契约未定 | Sol 直接执行 |

## 8. 上下文缓存

```text
ProfileContextKey = RoleProfileVersion + ExecutionProfileVersion + ModuleProfileVersion + ModelBindingVersion + RuleDigest + SourceRevision
```

键一致时，可以复用 Worker thread 或稳定 prompt 前缀，只追加新 WorkItem、新 ChangeSet Contract 与已接受的 ModuleContext Delta。不得持续累积旧对话、失败日志、完整 diff、过期计划或未接受推断。规则/版本/revision 变化、revision 不连续或 Worker blocked 后必须重新加载。

## 9. 扩展与替换

增加 ModuleProfile 前必须同时满足：稳定职责、清晰 owned paths、局部规则、公共入口/边界、独立测试门、会重复接收任务。满足后只修改 `.ai/profiles/modules.json` 并补 validator 测试；不要复制新的 Codex agent TOML。

替换 Sol/Luna 时只修改 `.ai/model-routing.yaml` 与必要的 Codex adapter model binding，不修改 Role/Execution/Module Profile。禁止静默降低 tier 或用 provider 身份改变 authority。

## 10. DSH 边界

DSH 在收到 `UniFlow` 后按需消费 `.ai/profiles/*.json`、两个 schema、WorkItem/WorkResult 和路由规则。若当前 DSH adapter 不具备独立委派能力，则由主执行者按同一 WorkItem 边界内联执行并显式记录能力限制，不得改变 Profile 语义。以下职责仍留给未来 DSH：持久化线程池、跨会话缓存、队列/调度、并发冲突锁、自动验收状态机与 provider runtime。当前实现不创建独立 Agent Runtime、不实现通用 fanout，也不自动修改权威 ModuleContext。

## 11. 验证

```bash
python3 tools/agent_profile_validator.py validate
python3 -m unittest discover -s tests/AgentWorkflow -p 'test_*.py'
scripts/check-consistency.sh
```
