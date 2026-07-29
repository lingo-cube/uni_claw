# UniClaw.Core — AI Coding Charter 知识库

> **架构**: 四层纵切 — Constitution → Patterns → Layers → Decisions
> **版本**: v1.0 (从横切文档 01-07 迁移)
> **详细规格**: [charter-specification.md](charter-specification.md)

---

## 系统概览

UniClaw.Core 是一个**UI 自动遍历引擎**——给定一个 TraversalPlan (意图+目标)，引擎自动遍历 App 页面、处理 popup、恢复错误，直到完成或终止。

```
IntentSlots → PlanCompiler → TraversalPlan → TraversalEngine 初始化
                                                      ↓
GlobalFSM: Idle → Initializing → Traversing ──────────┤
                                                      ↓
StepOrchestrator 14-step 循环 ←←←←←←←←←←←←←←←←←←←←←
│ TraversalFSM: NodeSelect → Execute → Verify → Branch│
│ Handler: popup → container → error (dispatch+fallback)│
│ DynamicMatcher: PageAnalysis → 匹配子节点            │
│ Action: 执行 → 新截图 → 新 PageAnalysis → 循环      │
└─────────────────────────────────────────────────→→→→→
                                                      ↓
GlobalFSM: Traversing → Completed (terminal)
```

各层职责: Domain 提供纯数据模型 → Graph 编译遍历蓝图 → StateMachine 定义运行时状态和 Handler → Traversal 编排 14-step 主循环。
完整流程见 [system-orchestration.md](patterns/system-orchestration.md)。

---

## Tier 1 · Constitution (跨 Phase 不变, CI 强制)

| 文档 | 核心内容 | Guard tests |
|------|---------|------------|
| [constraints.md](constitution/constraints.md) | 8 条 hard constraint (C-1~C-10) — 火山级 enum 锁定、架构级依赖规则、安全级 cast-back 阻断 | ArchitectureGuardTests.cs (阻断性 CI) |
| [locked-enums.md](constitution/locked-enums.md) | 10+2 enum 值锁定 + cascade 影响图 + 扩展铁律 | EnumValueGuardTests (12 tests) |
| [prohibited-patterns.md](constitution/prohibited-patterns.md) | 7 条禁止模式 — ToDictionary、视觉行为混淆、非 sealed record 等 | grep test / ArchUnitNET (Phase 2.2) |

**AI 摄入**: constitution ≈ 4 页, 包含所有不可违反的规则。修改代码前必读。

---

## Tier 2 · Patterns (缓慢追加)

| 文档 | 核心内容 |
|------|---------|
| [system-orchestration.md](patterns/system-orchestration.md) | 系统整体架构 + FSM 生命周期 + 跨层数据流转图 |
| [fsm-design.md](patterns/fsm-design.md) | 双 FSM 架构: TraversalFSM 8×8 + GlobalFSM 8×8 迁移矩阵 + 独立性原则 |
| [handler-pipeline.md](patterns/handler-pipeline.md) | 通用管道 detect→classify→decide→execute + 3 Handler 差异对比表 |
| [readonly-isolation.md](patterns/readonly-isolation.md) | 三级集合安全 + ReadOnlySetWrapper + TraversalContextSnapshot |
| [dispatch-table.md](patterns/dispatch-table.md) | Hook dispatch + fallback chain + Log-and-Continue + 4 实例对比 |
| [development-environment.md](development-environment.md) | Codex/MCP/sandbox/emulator stable local-development baseline |

**AI 摄入**: 每篇 ≈ 2 页。修改 Handler/FSM/Context 时按需读。

---

## Tier 3 · Layers (改代码才改文档)

| 文档 | 核心内容 | 状态 |
|------|---------|------|
| [domain.md](layers/domain.md) | 24+2 类型 · 三岛拓扑 · 桥 · 稳定性 · 校验 · 序列化 | Phase 1 完成 |
| [graph.md](layers/graph.md) | TraversalPlan · PlanCompiler 4 模板 · DynamicMatcher 5 维匹配 | Phase 2 实现中 |
| [state-machine.md](layers/state-machine.md) | 双 FSM · 3 Handler · NodeStack · Context 26 字段 | Phase 2 实现中 |
| [traversal.md](layers/traversal.md) | StepOrchestrator 14-step · 6 子组件 | Phase 2 实现中 |
| [observability.md](layers/observability.md) | Trace CQRS · File/InMemory storage · Host correlation | Phase 2 可用 |
| [device.md](layers/device.md) | 统一 ADB runner · capture/action/screen-state/entry | Settings 最小闭环可用 |
| [host.md](layers/host.md) | CLI 组合根 · scenario/safety/assets/incremental locate | 部分交付，枚举/repeat deferred |

**AI 摄入**: 每篇 ≈ 3 页。只读当前工作层的 layer doc，不读其他层。

---

## Tier 4 · Decisions (append-only)

| 文档 | 核心内容 |
|------|---------|
| [log.md](decisions/log.md) | D-1~D-17 条目 — Decision + Source + Ref + Guard + Commit + Status |

**Source 三路分类**: `openspec:{change-id}` / `finding:{H/M/D-id}` / `direct-commit`

**AI 摄入**: ≈ 3 页。遇到争议或查已定决策时读。

---

## Phase 2.3 路线图 (TraversalFSM handler 实现)

当前核心缺口: TraversalFSM 6/8 handler 是 stub (返回 hardcoded default state)。
核心模型已 100% 完成，核心运行逻辑未完成 — 引擎能思考但不能行动。

| Phase | Handler | 依赖 | 构成 | 状态 |
|-------|---------|------|------|------|
| **P1 (2.3a)** | HandleExecute + HandleBranch | IActionExecutor, ChildrenStrategy | **最小可运行遍历循环**: 选节点→执行→验证→选下一 | OpenSpec proposed → `phase2-3a-core-traversal-loop` |
| **P2 (2.3b)** | HandleResultVerify + HandlePreconditionCheck | IVisionProvider | 验证+纠正 (3-round retry) | 未开始 |
| **P3 (2.3c)** | HandleErrorHandling + HandlePopupHandling | ErrorHandler sub-components, IVisionProvider | 容错+弹窗处理 | 未开始 |
| **Phase 3** | ADB/Vision 真实实现 | Android Debug Bridge, AI Vision API | 真实设备交互 | 未开始 |

---

## AI Context Routing (→ CLAUDE.md)

| 任务类型 | 必读 | 按需读 |
|---------|------|-------|
| Domain 类型修改 | constitution/* + layers/domain.md | patterns/readonly-isolation |
| Graph 层修改 | constitution/* + layers/graph.md | patterns/fsm-design |
| StateMachine 层修改 | constitution/* + patterns/fsm-design + layers/state-machine.md | patterns/handler-pipeline |
| Traversal 层修改 | constitution/* + patterns/dispatch-table + layers/traversal.md | patterns/fsm-design |
| 新增 enum | constitution/locked-enums.md + layers/<affected-layer>.md | decisions/log.md |
| 修 bug | decisions/log.md + layers/<affected-layer>.md | constitution/constraints.md |
| 新增 Handler | constitution/* + patterns/handler-pipeline + patterns/dispatch-table | layers/state-machine.md |
| Phase 规划 | constitution/* + all patterns + decisions/log.md | all layers |

**规则**: 先读 constitution → 再读 patterns → 再读当前 layer。不读不相关的 layer。

---

## 旧横切文档 (已归档至 _archive/)

01-07 横切文档已从 `docs/system/` 移至 `docs/system/_archive/`，内容全部已被四层纵切结构覆盖。

---

## Guard Tests

`tests/UniClaw.Core.Tests/Architecture/ArchitectureGuardTests.cs` — CI-blocking:

- **EnumValueGuardTests** (12 tests): 10 Phase2 enum + 2 Domain enum 值数锁定
- **DependencyDirectionGuardTests** (3 tests): C-5 Graph→StateMachine 单向依赖
