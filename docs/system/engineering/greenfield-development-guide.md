# Greenfield Development Guide

## 1. 建设原则

不要一次构建整个系统。

严格按照 Vertical Slice 逐步推进。

---

## 2. Phase 0 — Architecture Skeleton

建立：

- project structure；
- core contracts；
- core models；
- dependency rules；
- test infrastructure；
- fake environment；
- architecture docs。

不要实现复杂业务。

---

## 3. Phase 1 — Deterministic Runtime

实现：

- Run lifecycle；
- Startup；
- Observation；
- World Belief；
- RecoveryAnchor；
- Container；
- Traversal；
- basic action / verify；
- Normal Scenario。

---

## 4. Phase 2 — Trap & Recovery

实现：

- Trap；
- scope escalation；
- RecoveryRequest；
- RecoveryRuntime；
- Recovery verification；
- Recovery Scenario。

---

## 5. Phase 3 — Robust Execution

实现：

- uncertain action；
- idempotency handling；
- Popup；
- Scroll；
- Dynamic Grounding；
- local history。

---

## 6. Phase 4 — Real Environment

接入：

- real screenshot；
- YOLO；
- OCR；
- device action；
- application lifecycle。

---

## 7. Phase 5 — Semantic Intelligence

增加：

- semantic page resolution；
- Container identity；
- Memory；
- LLM/VLM fallback；
- async semantic enrichment。

---

## 8. Phase 6 — Advanced Agent

根据真实需求再考虑：

- dynamic re-plan；
- richer memory；
- container navigation graph；
- adaptive recovery；
- learning from successful runs。

不要提前实现 Phase 6。

---

## 9. 项目结构原则

不要首先追求漂亮目录。

目录应该表达 Runtime Architecture。

可参考：

```text
src/
  UniClaw.Core/
    Agent/
    Startup/
    Container/
    Traversal/
    Recovery/
    World/
    Planning/
    Memory/
    Capabilities/
      Vision/
      Device/
      AI/
      External/
    Model/
      Observation/
      Graph/
      Actions/
    Observability/

tests/
  Unit/
  Architecture/
  Scenario/
  Integration/

docs/
  system/
  decisions/
  scenarios/
```

具体目录允许调整。

必须保持：

```text
高层概念清晰
+
依赖方向可验证
```

---

## 10. Architecture Tests

不要只用文档约束架构。

能够机械验证的规则必须加入 Architecture Tests，例如：

- Traversal namespace 不得依赖 Agent；
- Environment 不得依赖 Agent；
- Domain / Model 不得引用 Runtime implementation；
- 核心 contracts 不得引用具体 Android Adapter。

如果某条 Architecture Invariant 能通过测试锁定，就不要只写 README。

---

## 11. Scenario Tests 优先于大量 Unit Tests

Unit Test 验证组件。

Scenario Test 验证架构。

Agent Runtime 最重要的是不同组件协同后的控制权是否正确。

第一阶段重点 Scenario：

- NormalExecution
- AgentRecovery
- ContainerRecovery
- ScrollIdentity
- UncertainAction
- StartupFailure
- RecoveryFailure

每一个架构 Bug 最好最终变成新的 Scenario Test。

---

## 12. 每个核心类必须回答的问题

新增核心类之前必须说明：

### Purpose

它为什么存在？

### Owns

它唯一拥有哪份 mutable state？

### Does Not Own

哪些职责明确不属于它？

### Inputs

它消费什么？

### Outputs

它产生什么？

### Authority

它允许作哪些决定？

### Lifecycle

谁创建？什么时候销毁？

### Failure

失败产生 Exception、Result 还是 Trap？

### Dependencies

允许依赖什么？

如果回答不了，不要创建这个类。

---

## 13. 每个接口必须证明价值

不要为了 Clean Architecture 创建：

- IXxxService
- IXxxManager
- IXxxProvider

然后只有一个实现且不存在真正边界。

优先对以下情况创建接口：

- 外部能力；
- 可替换策略；
- AI Provider；
- Device；
- Vision；
- Storage；
- Clock；
- nondeterministic environment。

纯内部实现如果没有替换需求，可以保持简单。

---

## 14. 不允许的架构味道

发现以下情况必须停止并重新评估：

- `AgentRuntime.cs` 持续膨胀；
- 一个 Context 包含所有 Runtime 状态；
- 多个组件都能 PressBack；
- 多个组件维护 CurrentPage；
- FSM handler 里开始调用 LLM；
- Fingerprint 决定 Page Identity；
- Traversal 可以 Launch App；
- Container 可以 Replan Goal；
- Vision Provider 修改 Runtime State；
- Graph 被当成真实 UI；
- Memory 被当成当前事实；
- 大量特殊 case 塞入 Engine 主循环；
- 一个 bug fix 需要同时修改五层共享 flag。

---

## 15. 编码原则

优先：

- small cohesive types
- explicit ownership
- immutable observations
- structured results
- async cancellation
- deterministic core
- dependency injection at boundaries
- scenario-first testing
- observability by design

避免：

- God Object
- Global mutable state
- Service Locator
- hidden side effects
- magic flags
- bool-driven orchestration
- deep inheritance
- reflection-driven core behavior
- unbounded generic abstraction
- premature plugin framework

---

## 16. 文档原则

项目必须对 Coding Agent 友好。

根目录应提供简洁 AI Entry Point，例如：

```text
AGENTS.md
```

AGENTS.md 不应成为几百行知识仓库。

它主要提供：

- 项目目标；
- 不可突破原则；
- 文档路由；
- 开发流程；
- 构建测试入口。

详细知识进入相应文档目录。

每个核心 Runtime 模块建议存在简洁设计文档，至少包含：

- Purpose
- Responsibility
- State Ownership
- Dependency
- Lifecycle
- Failure / Trap
- Examples

---

## 17. Architecture Decisions

影响长期结构的决策必须记录 ADR，例如：

- 为什么 Container 以 Semantic Page 为边界？
- 为什么 Fingerprint 不是 Page Identity？
- 为什么 RecoveryAnchor 属于 Startup？
- 为什么 Traversal 不允许依赖 Agent？

ADR 记录：

```text
Context
Decision
Consequences
```

不要记录每一个普通代码选择。

---

## 18. AI Coding 工作方式

实现功能时不要：

```text
Prompt → immediately code
```

应该：

```text
Requirement
→ Scenario
→ Responsibility
→ Authority
→ State Owner
→ Interfaces
→ Implementation
→ Verification
```

面对复杂问题：

先给出设计判断。

如果需求不足：

明确 `Deferred Decision`。

不要为了显得完整猜未来设计。

---

## 19. 设计自由度

Architecture Invariants 不允许静默突破。

以下内容允许根据实现分析：

- Container 是否需要独立 FSM；
- TraversalFSM 具体状态；
- Global lifecycle 具体 enum；
- Recovery 是否内部采用 FSM；
- WorldBelief 具体模型；
- Plan Graph 数据结构；
- Container Navigation 是否使用 Graph；
- Semantic Identity 算法；
- Memory backend；
- AI Provider 接口；
- DI 框架；
- namespace；
- project assembly boundaries。

如果某项建议导致不必要复杂度，应：

1. 明确指出；
2. 解释当前真实 Requirement；
3. 区分 Architecture Invariant 和 Implementation Suggestion；
4. 给出更小方案。

不要机械执行文档。

---

## 20. 第一阶段完成标准

第一阶段不以"写了多少类"为完成标准。

必须满足：

1. Normal Scenario 可以完全在 Fake Environment 中运行；
2. Agent Recovery Scenario 可以运行；
3. Scroll 不会因为 Fingerprint 改变导致 Container Identity 错误；
4. uncertain Action 不会盲目重复执行；
5. Popup 可以在 Container Scope 恢复；
6. Startup 能建立 RecoveryAnchor；
7. Recovery 成功必须经过 Observation + Verify；
8. Global lifecycle 与 Traversal protocol 职责分离；
9. 一个 mutable state 只有一个 owner；
10. Dependency direction 有自动 Guard；
11. Scenario Trace 可以解释系统为什么做每一步；
12. 不依赖 LLM 也能跑完确定性测试。

满足以后，再扩大真实设备能力。

---

## 21. 第一项工作

不要立即实现完整系统。

首先完成：

### A. Architecture Proposal

输出：

- Runtime component model；
- ownership table；
- dependency diagram；
- runtime state model；
- normal lifecycle；
- trap / recovery lifecycle；
- minimal project structure；
- deferred decisions。

### B. Minimum Contracts

只定义第一条 Vertical Slice 真正需要的 contracts。

### C. Fake Environment

建立可以确定性驱动页面变化的 simulation。

### D. Normal WiFi Scenario

实现完整正常生命周期。

### E. Recovery WiFi Scenario

实现 Launcher drift + Agent Recovery。

### F. Architecture Review

完成后检查：

- 是否出现 God Object；
- 是否出现重复 authority；
- 是否混淆 Runtime State / World State；
- 是否将 Plan 当 Reality；
- 是否产生不必要 FSM；
- 是否能够解释每一个状态 owner；
- 是否可以不用真实手机和 LLM 完成核心测试。

通过以后再继续下一阶段。
