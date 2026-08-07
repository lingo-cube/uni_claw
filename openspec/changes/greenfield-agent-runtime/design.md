# Design: Greenfield Agent Runtime — 工程地基

> 本 change 只建**工程边界 + 约束**，不实现任何 Runtime 业务逻辑。
> 业务类型（Agent.cs / Container.cs / TraversalFSM.cs …）在下一个 Vertical Slice 中
> 从真实运行需求中自然产生，本阶段**不创建**任何 stub / NotImplementedException。

## 决策

### D1 — 独立 worktree 隔离

`feature/agent-runtime` 分支创建于 `feature/refactor` HEAD（af6c1ee），独立 worktree
（`uni-claw-agent-runtime/`）。现有工作区的脏改动（Container 网关、depth-popup 等）
**不进入**新 Runtime 工作区。

### D2 — 第一阶段不引用 UniClaw.Core

`UniClaw.Runtime.csproj` **不含任何 ProjectReference**。

原因：当前 Domain / Graph / Traversal / StateMachine 仍位于同一个 UniClaw.Core assembly，
引用即暴露全部旧控制结构，Greenfield 边界立刻失效。

以后某项成熟能力确实需要复用时，单独决策：
- **Extract Foundation** — 能力下沉为独立 assembly
- **Create Adapter** — 适配旧实现
- **Reuse Contract** — 只复用接口/模型

不在本阶段预设答案。

### D3 — Architecture Contract 复用现有 docs/system 体系

Contract 放入 `docs/system/constitution/runtime-architecture-contract.md`
（Tier 1 Constitution 语义：不可违反的 hard constraint）。
**不建立第二套文档体系**，不创建 `.ai/`。

### D4 — 机械 Guard 放在新测试工程

`tests/UniClaw.Runtime.Tests/Architecture/ArchitectureGuardTests.cs`：

| Guard | 机械保证 | 失败信息包含 |
|-------|---------|-------------|
| 1 | `UniClaw.Runtime.csproj` 无任何 `<ProjectReference` | 违反什么 / 为什么 / 读哪个文档 |
| 2 | `src/UniClaw.Runtime/` 源码无 `UniClaw.Core.Traversal` / `UniClaw.Core.StateMachine` | 同上 |
| 3 | Contract 文档存在 + 12 条 invariant 标题齐全 + AGENTS.md 导航目标有效 | 同上 |

### D5 — AGENTS.md 只加一个导航入口

在「系统设计文档（AI Coding 宪章）」之后新增 `## Agent Runtime（新）— Greenfield` 小节，
指向 Contract + OpenSpec change + Guard 文件。不做大规模重构。

## 结构

```
src/UniClaw.Runtime/                    ← 空工程边界（无业务类型）
  UniClaw.Runtime.csproj                ← net10.0，零 ProjectReference
tests/UniClaw.Runtime.Tests/            ← 仅 Architecture Guard 测试
  UniClaw.Runtime.Tests.csproj          ← xUnit 2.6.2（对齐 Core.Tests 约定）
  Architecture/ArchitectureGuardTests.cs
docs/system/constitution/runtime-architecture-contract.md   ← 12 invariants
openspec/changes/greenfield-agent-runtime/                  ← 本 change
```

## 验证

```bash
dotnet build src/UniClaw.Core.sln
dotnet test src/UniClaw.Core.sln
```

- 基线（HEAD）与完成后各跑一次，明确区分 Pre-existing failure 与 New regression
- 新 Guard 测试必须通过；旧测试不得因本 change 退化
