## Why

UniFlow 当前只能把 Profile、约束和验收条件交给 Worker，Leader 选中的调试 Skill
不会进入可验证的 WorkItem / ModuleContext 链路。结果是 Worker 即使遇到 UI 或
Runtime 问题，也可能绕过项目调试方法，直接陷入漫长代码调用链或凭症状猜 Owner。

## What Changes

- 为 WorkItem 增加向后兼容的有序 `required_skills` 名称数组，由 Leader 显式选择，
  并由 Validator 从仓库受信任 Skill 根唯一解析。
- Codex 与 DSH Worker 在执行前接收同一份已解析 Skill 上下文；Skill 缺失、重名、
  名称非法或不可达时 fail-closed，不静默继续。
- DSH 延迟派发记录必须携带可供会话侧直接消费的 Worker payload，包括 canonical
  Skill 路径、完整正文、顺序和 fail-closed 指令；不得只保存名称或摘要哈希。
- UniFlow 对 Bug / 失败调查强制选择通用证据驱动调试 Skill；Runtime 行为问题再追加
  Runtime 专用 Skill，同时保留 Skill `Authority: NONE`。
- Leader 在语义归因、架构判断或代码深挖前先完成一次简短 Reality Preflight：从
  用户可见目标、当前可观察状态、人类最短可行路径和预期可见变化建立可证伪假设，
  再用最近 falsifier / First Divergence 决定最小证据入口和 Owner。
- 为相关调试与 UniAgent 演进 Skill 增加 UI-first 方法：先建立用户可见目标、当前
  界面状态和人类最短可行操作路径假设，再沿 First Divergence 进入必要代码证据；
  不得把坐标、固定点击序列或偶然 UI 路径写成 Runtime 权威。

## Capabilities

### New Capabilities

- `uniflow-required-skill-propagation`: 定义 Leader 选择、WorkItem 传递、Worker
  fail-closed 加载和 UI-first 调试方法的可验证行为。

### Modified Capabilities

无。

## Impact

- `.ai/schemas/work-item.schema.json`
- `.ai/workflows/uniflow-coding-workflow.md`
- `.ai/profiles/roles.json`
- `.ai/task-contract.md`
- `.ai/skills/{evidence-driven-debugging,runtime-behavior-debugging,uniagent-evolution-loop}/`
- `tools/agent_profile_validator.py`
- `tools/dsh_profile_adapter.py`
- `.codex/agents/{module-worker,test-author,verifier,semantic-analyzer}.toml`
- `tests/AgentWorkflow/` 与 DSH Profile Adapter 文档/示例

不增加依赖，不修改 Runtime、Perception、Strategy Contract、GoalEvidence、
SourceIdentity 或任何产品运行时协议。
