# Tasks: greenfield-agent-runtime

> 本 change 只建工程地基，不实现 Runtime 业务逻辑。

- [x] 1. 创建独立 worktree + 分支 `feature/agent-runtime`（基于 `feature/refactor` HEAD af6c1ee）
- [x] 2. 创建 OpenSpec change 文件（proposal / design / tasks）
- [x] 3. 创建 `src/UniClaw.Runtime/UniClaw.Runtime.csproj`（零 ProjectReference，不引用 UniClaw.Core）
- [x] 4. 创建 `tests/UniClaw.Runtime.Tests/UniClaw.Runtime.Tests.csproj`（对齐 xUnit 约定）
- [x] 5. 将两个新工程加入 `src/UniClaw.Core.sln`
- [x] 6. 编写 Architecture Contract：`docs/system/constitution/runtime-architecture-contract.md`（12 invariants）
- [x] 7. AGENTS.md 增加「Agent Runtime（新）」导航入口（唯一入口，不大规模重构）
- [x] 8. 编写 `ArchitectureGuardTests.cs`（Guard 1/2/3，失败信息含违反内容 + 文档指针）
- [x] 9. 验证：baseline build/test 记录 → 完成后 build/test 对比，无 New regression

## Deferred（后续 Vertical Slice 决策）

- Agent / Container / Traversal / Recovery 业务类型与类名
- 复用决策：IActionExecutor / PageAnalysis / UniBrain / Graph / SourceGen / Foundation
- ContainerFSM 是否存在、TraversalFSM 状态设计
- Memory / LLM-VLM / Android / Vision / DynamicMatch
