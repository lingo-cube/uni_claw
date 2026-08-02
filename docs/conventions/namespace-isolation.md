# Namespace Isolation Convention

> 项目约定：命名空间依赖方向规则，防止循环引用和不希望的向上依赖。
> 最后更新: 2026-07-30

## 核心规则

### D-130: UniBrain 不依赖 Traversal / StateMachine

`UniClaw.Core.UniBrain` 命名空间**禁止**引用 `UniClaw.Core.Traversal` 或 `UniClaw.Core.StateMachine`。

**Guard**: `ArchitectureGuardTests.UniBrain_DoesNotReferenceTraversal`（CI-blocking）

**后果**: 所有被 UniBrain 消费的接口/抽象，必须放在 UniBrain（或其消费者所在的 namespace），不能放在 Traversal/。

### D-17: Observability 是 cross-cutting utility

`StateMachine` / `Traversal` 可以引用 `Observability`，不视为向上违规。Observability 是横切工具层，不是传统顶层。

### D-131: Observation 是 UniBrain × Traversal 桥接层

`UniClaw.Core.Observation`（`ObservationPipeline` / `ObservationConfig` / `UiAutomatorPageAnalysis`）
是唯一的 UniBrain × Traversal 桥接命名空间：管线实现 `IPageAnalyzer`（UniBrain）同时消费
`IObservableScreenStateProvider` / `ScreenStateResult`（Traversal），按 D-130 不能放在 UniBrain/，
故按本约定「多个消费者不同 namespace → 提取到独立子目录」新建 Observation/。

**Guard**: `ArchitectureGuardTests.UniBrain_DoesNotReferenceTraversal`（CI-blocking）——若把管线放回
UniBrain/ 会直接撞 D-130。

**接口位置**（按「放在消费者所在 namespace」规则）：
- `IScreenStateCache`、`IUiAutomatorAvailability` 留在 `Traversal/` —— 实现方是外层（Device/`AdbScreenStateProvider`、
  Host/`StepCaptureStore`），Observation 只读接口；放 Traversal/ 不撞 D-130（Observation 非 UniBrain）。
- `ObservationPipeline` 是 `IPageAnalyzer` 实现，但**不是** `IUniBrain` 表面成员——Host 直接
  `new ObservationPipeline(...)` 组装，`IUniBrain.PageAnalyzer` 仍是 `PageAnalyzer`（UniBrain）。

### IScreenCapture 位置

`IScreenCapture` 放在 `UniClaw.Core.UniBrain/IScreenCapture.cs`（namespace `UniClaw.Core.UniBrain`），**不放 Traversal/**。

**理由**: `PageAnalyzer`（UniBrain）是 `IScreenCapture` 的唯一 Core 消费者。若接口放 Traversal/，PageAnalyzer 必须 `using UniClaw.Core.Traversal` → 直接撞 D-130。

**对比 IActionExecutor**: 其消费者是 TraversalEngine（同目录）+ StateMachine/OperationDispatcher，都不在 UniBrain，所以放 Traversal/ 不撞 D-130。

## 设计原则

新增接口/抽象时，按以下规则选位置：

1. **放在消费者所在 namespace** — 优先
2. **如果多个消费者在不同 namespace** — 放在最底层消费者的 namespace，或提取到独立的 Contracts/Abstractions 子目录
3. **不盲目照搬现有模式** — `IActionExecutor` 放 Traversal/ 是特例，不适用于有 UniBrain 消费者的接口

## 来源

- Memory: [[iscreencapture-unibrain-namespace]]
- Charter: `docs/system/constitution/constraints.md` — D-130, D-17
- Guard Tests: `ArchitectureGuardTests.cs`
