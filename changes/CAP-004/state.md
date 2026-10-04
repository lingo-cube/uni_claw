# CAP-004 — Capability Hub code ownership and inventory
lifecycle_state: closed · disposition: none · depth: standard · base: working-tree

## Intent（WHAT/WHY）
把 Capability Hub 的管理代码单独列成可维护的代码面，明确 Hub 管理、Host-neutral
能力协议、具体能力实现和测试夹具的归属。当前源码已经位于独立目录，但缺少一份
维护者可以直接使用的 ownership/index，导致新增能力时容易把管理语义、结果协议和
具体实现混在一起。

## Scope
- 增加 `src/UniClaw.Kernel/Capability/README.md` 作为 Capability Hub 代码索引和变更规则。
- 在 Capability Hub 文档入口中链接源码索引。
- 列出当前 Hub 管理代码、Integration contracts、Product realization、Adapter 和测试面的关系。

## Out of Scope
- 移动或重命名现有 C# 源码。
- 新增 Capability、Exposure policy、DSH Tool/Skill 或 Host projection。
- 修改 Kernel public surface、运行时调用链或任何 Product authority。

## Decisions
1. `CapabilityHub.cs` 是 Hub 管理面的唯一当前入口，负责注册域、描述、生命周期、健康状态、实例解析和装配校验。
2. `IntegrationContracts.cs` 是 Host-neutral 结果/关联协议，不属于 Hub 管理实现；它只通过明确的 contract 类型与 Hub 并列存在。
3. 具体 Product perception 和 Runtime Integration realization 放在各自的实现目录；仿真夹具只能留在测试项目。
4. 本 Change 只建立 ownership/index，不做目录迁移；未来若要拆 `Hub/`、`Contracts/` 子目录，必须另立 Change 并先验证引用和 public surface。

## Acceptance
1. 维护者能从 Capability 目录索引找到管理面、协议面、实现面、Adapter 和测试面。
2. 索引明确哪些修改属于 Hub 管理，哪些必须走协议/买方评审，哪些不得进入 Kernel。
3. `docs/capability-hub/README.md` 链接到该索引。
4. 文档变更不改变源码行为；差异检查通过。

## Constraints
- 保留当前工作树中的已有改动，不清理、不重置、不移动无关文件。
- 不把 DSH transport、ToolDefinition、Skill 或测试 fixture 类型写入 Kernel 管理代码。

## Verification
level: CONTRACT
method: inspect the ownership index and documentation links; `git diff --check`
expected: Capability Hub code ownership is explicit and all references resolve without source changes
actual: ownership index added, Capability Hub README linked, `git diff --check` passed
evidence: current verification turn command output

## Status log
- 2026-10-04 · UNDERSTAND → RESOLVE → PERSIST → PLAN · 用户要求先单独列出 Capability Hub 代码管理面。
- 2026-10-04 · PLAN → IMPLEMENT · 增加源码 ownership/index 和文档入口链接，保持源码位置与运行时行为不变。
- 2026-10-04 · IMPLEMENT → VERIFY → CLOSED · 文档链接检查和差异检查通过。
