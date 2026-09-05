# UniFlow V2 — Development Workflow Control Plane

> **Explore = 把事情搞清楚；UniFlow = 把已经搞清楚的事情做完。**
> Explore 属于 Pre-UniFlow；UniFlow 从 EXPLORE_RESOLVED 启动。
> Codex 与 DSH 只是执行 Harness（adapter），不得重定义本文件语义。

## 1. 标准流程

```text
Intent
  ├─ Feature / Refactor → Pre-UniFlow Explore
  │     人工交互: grill-with-docs（upstream 语义，产出 Shared Understanding）
  │     Leader 自动: grilling + domain-modeling + repo inspection 组合
  │                  （不伪造 /grill-with-docs 调用，不建第二套 Explore 流程）
  └─ Bug / Regression → diagnosing-bugs
        Reliable RED → Reproduce → Minimise → Falsifiable Hypotheses
        → Instrument / Collect Evidence → FDP → Owner → Root Cause
              ↓
        EXPLORE_RESOLVED
              ↓
      ======== UniFlow START ========
              ↓
        Plan（Leader 执行意图）
              ↓
      Leader Orchestration
        /            \
  Direct Execute    Delegate
      ↓               ↓
 Relevant Skills   transient WorkItem
      ↓               ↓
 TDD / Implement   Fresh SubAgent
       \             /
        \           /
          Review
            ↓
          Verify
            ↓
        Complete
```

**Explore 硬边界**：

- `grill-with-docs completion ≠ permission to implement`——完成后
  STOP → EXPLORE_RESOLVED → 交还 Leader / UniFlow，不得自行进入实现。
- Bug：**Diagnosis 属于 Pre-UniFlow；Fix 属于 UniFlow。**
- 保留项目独有能力：Trace / Evidence / Expected vs Actual / FDP / Owner。

## 2. UniFlow 启动条件（EXPLORE_RESOLVED）

全部满足才 `ENTER_UNIFLOW`，任一不满足 `STAY_IN_EXPLORE`：

1. 要解决的问题明确；
2. 当前真实状态已确认；
3. 期望状态明确；
4. 主要约束 / 禁止项明确；
5. 未知项不会阻止实施；
6. 不存在尚未解决的真实 Human Decision。

## 3. UniFlow 职责

只负责：**Plan / Route / Execute / Delegate / Review Coordination /
Verify / Complete**。

```text
UniFlow = WHEN / WHAT NEXT
Skill   = HOW（不拥有第二套 lifecycle）
```

## 4. Plan（Leader 执行意图，非强制工件）

```text
Small / single-context    → Plan 可只存在当前 Leader context
Multi-session / long-run  → Plan 应持久化（plans/）
Need SubAgent             → 从当前 Plan 编译临时 WorkItem
```

`Plan ≠ mandatory artifact`：只在 Context Boundary 或需要后续恢复时要求
持久化。Plan 不是 Architecture truth（那在 `docs/adr/` 与 Product Baseline）。

## 5. Route：Direct vs Delegate

```text
Need delegation?
├─ NO  → Direct execution（相关 Skills 就地使用；TDD 默认纪律）
└─ YES → 从当前 Plan 编译 transient WorkItem → Fresh SubAgent
```

- 委派判据：Fresh Context 隔离 / 并行 / 上下文卸载，且任务可自包含陈述、
  验收可独立验证。否则直接执行，不制造 WorkItem。
- 确定性操作（查找 / 读取 / 明确命令）→ Tool Only，不派 Agent。
- 当前上下文已持有热知识且任务不大 → 直接执行。

## 6. WorkItem（transient delegation contract）

WorkItem 不是 Issue Tracker、Backlog、Plan store 或长期记忆，只是
**Leader → SubAgent 的临时 Delegation Contract**（schema：
`schemas/work-item.schema.json`；载荷按需落 `workitems/`）。

- 编译自当前 Plan；SubAgent 返回 Result + Evidence 后使命即完成。
- 委派拆分：垂直 tracer-bullet 优先（一条行为切片一个 WorkItem）；问题大到
  一个 Context 无法规划时，先拆决策再拆实现。
- 禁止：Codex session id / DSH worker id / 具体模型名 / harness 命令。
- 修改 acceptance / forbidden / frozen_decisions = 重新决策，回 Leader。

## 7. Context 策略

- Leader single-context execution：**可以继续当前健康 context**（不强制 fresh）。
- Leader → SubAgent delegation：默认 **Fresh / Disposable** context。
- 跨 Agent 禁止依赖旧 Conversation。
- 持久状态来自：`AGENTS.md` / ADR / `CONTEXT.md` / 持久化 Plan / Git /
  Tests / Trace-Evidence——**不是 Session**。

## 8. Review

- 默认 `code-review`：分开回答 Built Right? 与 Built The Right Thing?；
  检查 Standards / Intent / Acceptance / Frozen Decisions / Scope /
  Boundary / Evidence。
- 高风险任务可用 Fresh Review Context；**普通小任务不为 ceremony 强制
  独立 Reviewer**。

## 9. Verify / Complete

- 完成由 Leader / UniFlow 判定；**SubAgent self-report 与 code-review 都
  不能直接决定 Complete**。
- 判定依据：Acceptance / Tests / Diff / Trace-Evidence / 架构约束 / 未决项。
- `SubAgent says done ≠ Evidence proves done`；判定不依赖「哪个 Harness」。

## 10. Execute 硬规则（跨分级恒定）

```text
No reliable RED                → No fix
No falsifiable hypothesis      → Continue diagnosis
No sufficient evidence         → Continue diagnosis
No FDP / Owner                 → No implementation delegation
No RED → GREEN regression      → Not proven fixed
```

## 11. Model Routing

```text
Required capability → Leader / UniFlow routing → Harness adapter
→ Concrete model
```

- 共享映射在 `model-routing.yaml`（capability → tier）；provider/model 绑定
  只在 adapter。
- Skill 只声明 required capability，不绑定具体模型。
- 禁止 silent downgrade。

## 12. Adapter 边界（Codex / DSH 对称）

Adapter 负责：AGENTS.md / Skill 发现、session 创建、WorkItem 注入、工具
调用、模型配置、Result / Evidence 返回。Adapter 不得修改 WorkItem、
Acceptance、Decision、Skill procedure、Verification、Completion 语义。
同一 WorkItem 双 Harness 产生可规范化同构结果
（status / changes / tests / evidence / unresolved / escalation）。

## 13. Human Gate

只有真实材料性边界才请求人裁决：不变量变化、ownership / authority 转移、
安全语义、不可逆动作、两个均成立且无证据倾斜的产品级选择、显著预算扩张。
普通实现选择、测试修复、文档整理不构成 Human Gate。
