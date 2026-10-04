# ARCH-DOC-018 — 异构仓库职责与 Capability 三层边界
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: working-tree

## Intent（WHAT/WHY）

把异构仓库的根目录职责、外部 Provider 边界和 Capability 的 Definition /
Implementation / Binding 三层语义固定下来。当前目录、Provider、DSH 配置、
测试资产和生成状态的职责已经在实现与文档中分散存在；如果不形成 Change State，
后续目录迁移和任务级注入实现容易再次混合 authority、装配和运行实例。

## Scope

- 冻结根目录职责分类：产品代码、外部 Provider、工具、测试、测试资产、契约/控制面、治理资料和生成状态。
- 将 `platforms/perception` 定义为迁移前的历史路径，目标语义为 `providers/perception`。
- 固定 Provider Manifest、Capability Descriptor 和 Health/Lifecycle Fact 的边界。
- 固定 `CapabilityDefinition`、`CapabilityImplementation`、`CapabilityBindingDescriptor` 和 `CapabilityInstance` 的关系。
- 固定 DSH Tool 是 Capability 的 Binding/Exposure，不是新的 Capability 类型。
- 固定 Workspace Query Core 的 canonical source 为 `web/uniclaw-workspace`。

## Out of Scope

- 不在本 Change 内移动 `platforms/perception` 或修改 Provider 启动路径。
- 不在本 Change 内实现 Provider Manifest validator、`schemas/capability/` 迁移或任务级 Capability API。
- 不改变 Capability Hub、Evidence、WorldBelief、Run、Control、Assurance 或 Effect 的 authority。
- 不实现 DSH Tool/Skill 注册。
- 不拆分 Kernel、Host 或 Perception 程序集。

## Decisions

1. 根目录职责和 Provider 边界按 [ADR-0037](../../docs/adr/0037-repository-role-boundaries-and-provider-boundary.md) 执行；`.dsh/` 是 DSH 开发配置与开发装配，不是产品源码、Provider 或生成状态。
2. Capability 三层和静态/动态元数据边界按 [ADR-0038](../../docs/adr/0038-capability-definition-implementation-binding-boundary.md) 执行。
3. `CapabilityBindingDescriptor` 属于可复用装配规则；`CapabilityInstance` 只存在于 Host/Task 作用域，不修改全局 Registry。
4. 只有真正跨进程、跨语言的 wire contract 进入 `schemas/capability/`；单语言内部 interface 留在实现目录。
5. 目录迁移必须另立 Change，先完成引用盘点、manifest、验证入口和回滚证据。

## Acceptance

1. `CONTEXT.md` 包含 Provider、Provider Manifest、Capability Definition/Implementation/Binding/Instance 和 DSH Development Configuration 的规范术语。
2. 仓库结构基线列出每个根目录的语义、允许放置内容和禁止放置内容。
3. ADR-0037 和 ADR-0038 可从基线和 Change State 互相追溯。
4. 文档明确 `platforms/perception` 的迁移目标、当前运行入口和迁移前置条件。
5. 文档明确 DSH Tool、Provider Manifest、Capability Descriptor、Health/Lifecycle Fact 和 PerceptionAssessment 不得互相冒充 authority。
6. 文档变更不移动源码、不改变运行时行为，且差异检查通过。

## Constraints

- 保留工作区现有改动，不清理、不重置、不移动无关文件。
- 不因业界存在 `staging`、`contrib` 或 `vendor` 就在 UniClaw 预造同名目录。
- 不把 Provider、模型、Capability、Tool 和 Task Instance 合并成同一级 Registry entry。

## Verification

```yaml
verification:
  level: CONTRACT
  method: >-
    检查 CONTEXT.md、ADR-0037、ADR-0038 和 repository-role-boundaries-v0.1.md
    的术语、路径和互相引用；运行 git diff --check。
  expected: >-
    结构职责、Provider 边界、Capability 三层和 canonical source 规则一致；
    无空白引用、无格式错误，且没有源码迁移。
  actual: >-
    CONTEXT.md、ADR-0037、ADR-0038 和 repository-role-boundaries-v0.1.md
    已存在并完成双向追溯；git diff --check 通过；未修改产品源码或 Provider
    运行路径。
  evidence: >-
    当前工作树文档检查输出；git diff --check。
```

## Status log

- 2026-10-04 · UNDERSTAND → RESOLVE · 完成异构项目结构研究和对抗审阅；锁定根目录、Provider、Capability 三层与 canonical source 决策。
- 2026-10-04 · RESOLVE → PERSIST · 用户接受 Q1 补充 `.dsh/` 为 DSH 开发配置，其余审阅项按推荐接受；形成 ADR-0037、ADR-0038 和仓库职责基线。
- 2026-10-04 · PERSIST → REVIEW → VERIFY · Worker 发现 ADR、结构基线和 Change State 缺少双向追溯；补齐反向链接，确认 Provider 当前路径未迁移、Workspace 规则仅作为后续 Change 约束。
- 2026-10-04 · VERIFY → CLOSED · 文档 Acceptance 全部满足；`git diff --check` 通过；Provider 迁移、manifest validator、schema 抽取和 Workspace 重复 Core 保留为后续独立 Change。
