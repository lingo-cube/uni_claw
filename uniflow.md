# UniFlow V2 — Development Workflow Control Plane

> **Explore = 把事情搞清楚；UniFlow = 把已经搞清楚的事情做完。**
> UniFlow 由四个 Semantic Gate 驱动：Explore Resolution → Execution
> Readiness → Verification → Completion。流程可以轻，**Gate 不能软化**。
> Project declares domain truth and authority; UniFlow checks readiness and
> evidence; Harness never invents project semantics.

## 1. 标准流程

```text
Intent
  ├─ Feature / Refactor → Pre-UniFlow Explore
  │     默认 grill-with-docs；按需 domain-modeling / research /
  │     prototype / show-me
  └─ Bug / Regression → diagnosing-bugs
        （Trace / Evidence / FDP / Owner 能力作为 extension 按需接入）
              ↓
     [Gate 1] Explore Resolution Gate → EXPLORE_RESOLVED
              ↓
      ======== UniFlow START ========
              ↓
     Leader Execution Decision
              ↓
     [Gate 2] Execution Readiness Gate → EXECUTION_READY
              ↓
     Execution Routing
      /        |         \
  Direct    Delegate   Human Gate
   ↓           ↓
Relevant   transient WorkItem
Skills         ↓
   ↓       Fresh SubAgent
Implement      ↓
          Relevant Skills
               ↓
            Implement
       \       /
        \     /
         Review
           ↓
   [Gate 3] Verification Gate → VERIFIED
           ↓
   [Gate 4] Completion Gate → COMPLETE
```

**Explore 硬边界**：`grill-with-docs completion ≠ permission to implement`——
Skill 结束后必须回到 Gate 1，Skill 自己不得进入 Implementation lifecycle。
Bug：**Diagnosis 属于 Pre-UniFlow；Fix 属于 UniFlow**；Root Cause 未明确 →
`STAY_IN_DIAGNOSIS`，不得为了推进流程直接进入 Fix。

## 2. Gate 1 — Explore Resolution Gate

> 问题是否已经足够清楚，可以停止探索并进入受控执行？

```text
Intent clear?
Task Type known?
In Scope clear?
Relevant Out of Scope clear?
Known facts sufficient?
Blocking Unknowns resolved?
Relevant Assumptions explicit?
Owner / Authority identifiable?
Acceptance expressible?
Blocking Human Decision absent?
```

满足 → `EXPLORE_RESOLVED`；否则 → `STAY_IN_EXPLORE`。
这是 semantic contract：逐项心中有数即可，**不要求生成固定表格**。

## 3. Gate 2 — Execution Readiness Gate

```text
Scope executable?
Owner / Authority non-conflicting?
Acceptance verifiable?
Execution direction selected?
Verification strategy defined?
Direct / Delegate decision valid?
Required Human Gate resolved?
Stop conditions known?
```

只有 `EXECUTION_READY` 才能执行。

## 4. Gate 3 — Verification Gate

> 是否存在足够 Evidence 支持「这个实现满足 Acceptance」？

```text
Acceptance satisfied?
Tests sufficient?
Expected behavior observed?
Relevant Evidence available?
Diff within authorized scope?
Required architecture constraints respected?
Blocking review findings resolved?
Unresolved items declared?
```

不满足 → `VERIFICATION_FAILED` → 回到执行 / 诊断。
满足 → `VERIFIED`。

## 5. Gate 4 — Completion Gate

> 用户请求是否真正完成？

```text
Requested scope complete?
Verification passed?
No hidden unauthorized changes?
Residual risks declared when relevant?
Required durable docs synchronized?
Required Human Decisions resolved?
No blocking unresolved issue remains?
```

满足 → `COMPLETE`；否则不得宣布完成。

## 6. Owner / Authority 规则

UniFlow 不定义项目内部 Owner / Authority：

```text
Project Context / Baseline / CONTEXT.md / ADR
        ↓ declares
UniFlow
        ↓ checks explicitness + conflict
```

UniFlow 只能 `identify / reference / validate / detect conflict / fail
closed`；不得 `invent / override / reinterpret` 项目 authority。

## 7. Plan（Leader 执行意图，非强制工件）

```text
Small / single-context    → Plan 可只存在当前 Leader context
Multi-session / long-run  → Plan 应持久化（plans/）
Need SubAgent             → 从当前 Plan 编译临时 WorkItem
```

`Plan ≠ mandatory artifact`：只在 Context Boundary 或需要后续恢复时持久化。

## 8. Route：Direct / Delegate / Human Gate

**Direct**（bounded local objective / current context sufficient / risk
controlled / delegation adds little value）→ Relevant Skills → Implement。

**Delegate**（bounded independent objective / clear semantic boundary /
explicit acceptance / context can be packaged / SubAgent 可独立执行）→
transient WorkItem → Fresh SubAgent → Result + Evidence → Leader。
委派拆分垂直 tracer-bullet 优先；问题大到无法规划先拆决策。

**Human Gate**——只用于：未决产品含义 / 重要架构选择 / authority 冲突 /
生命周期语义选择 / 不可逆或破坏性决定 / 项目证据无法裁决的 tradeoff。
普通代码事实必须先由 Agent 调查；确定性操作 → Tool Only。

## 9. WorkItem（transient delegation contract）

WorkItem 只是 **Leader → SubAgent 的临时 Delegation Contract**（schema：
`schemas/work-item.schema.json`；载荷按需落 `workitems/`）。不是 issue
tracker / backlog / persistent Plan / architecture truth / project memory。
禁止 harness 专有字段（session id / worker id / 模型名 / harness 命令）。
修改 acceptance / forbidden / frozen_decisions = 重新决策，回 Leader。

## 10. Context 策略

- Leader single-context execution：可以继续当前健康 context。
- Leader → SubAgent delegation：默认 Fresh / Disposable。
- 跨 Agent 禁止依赖旧 Conversation。
- 持久状态来自 `AGENTS.md` / ADR / `CONTEXT.md` / 持久化 Plan / Git /
  Tests / Trace-Evidence——不是 Session。

## 11. Review / Verify 分离

```text
Review = Is the implementation good?   （默认 code-review：intent / design /
                                         scope / boundary / standards / quality）
Verify = Is the completion claim proven?（UniFlow 依据 Gate 3/4 判定）
```

Review 不能宣布 COMPLETE；Worker 也不能。高风险任务可用 Fresh Review
Context；普通小任务不为 ceremony 强制独立 Reviewer。

## 12. Fail-Closed 总表（含 Execute 硬规则）

```text
Explore unresolved            → no UniFlow
Execution not ready           → no execution
Authority conflict unresolved → no execution
Delegation context insuff.    → no dispatch
Evidence insufficient         → no VERIFIED
Human Decision unresolved     → no COMPLETE

No reliable RED               → No fix
No falsifiable hypothesis     → Continue diagnosis
No FDP / Owner                → No implementation delegation
No RED → GREEN regression     → Not proven fixed
```

## 13. Model Routing

```text
Required capability → Leader / UniFlow routing → Harness adapter
→ Concrete model
```

共享映射在 `model-routing.yaml`（capability → tier）；provider/model 绑定只在
adapter；Skill 只声明 required capability；禁止 silent downgrade。

## 14. Adapter 边界（Codex / DSH 对称）

Adapter 负责 discovery / session / WorkItem 注入 / 工具调用 / 模型配置 /
Result-Evidence 返回；不得修改 WorkItem、Acceptance、Decision、Skill
procedure、Verification、Completion 语义。同一 WorkItem 双 Harness 产生可
规范化同构结果（status / changes / tests / evidence / unresolved /
escalation）。
