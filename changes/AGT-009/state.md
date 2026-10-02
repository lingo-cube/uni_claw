# AGT-009 — Settings 遍历抽象计划、Slow 接入与弹窗清障
lifecycle_state: persisted · disposition: none · depth: decision-heavy · base: 7b49038d

## Intent（WHAT/WHY）

当前 Settings 遍历已经有 Fast/XML 观察、Coverage Director 和 Slow 基础设施，
但三者尚未形成一个完整闭环：无 XML 时缺少语义补证，弹窗会被误当作未知页面，
UniAgent 只能被要求返回当前单步指令，无法向 Kernel 提供受约束的抽象遍历计划。

本 change 建立一个 advisory `AgentDecision.Plan`，接入现有有界 Slow 能力，
并允许弹窗清障、重新观察和重新规划回到既有 Grounding → Assurance → Effect
Gate → Verification 链路。

## Scope

- Kernel：Plan 的最小契约、相关性/预算/字段校验、逐步采纳和失效规则。
- Host/Settings：Slow 异步接入、弹窗语义证据、清障分支和 Director 适配。
- Tests/Evidence：Plan、Slow、弹窗、无 XML、冲突、晚到结果和预算边界。

## Out of Scope

- 不把 `AgentDecision.Policy` 改造成遍历脚本。
- 不新增第二套执行器、状态机、Evidence 或 WorldModel 权威。
- 不扩展通用 `scenarios/schema.json`。
- 不修改 AGT-010（RouteKey/ViewportDigest）和 AGT-011（证据接线/候选质量）。
- 不复制 legacy `uni-agent` 接口，不改变 `UniAgent` Goal Evaluation 职责。

## Decisions

1. `AgentDecision.Plan` 是 advisory、有界、有序的计划；不包含坐标、occurrence、
   过期 selector 或直接 Effect 授权。
2. PlanItem 只有 `Act`、`Observe`、`Control`；Control 只有 `Reobserve`、
   `Replan`、`Stop`。
3. Kernel 每次最多执行一个 Act；每个 Act 都重新 grounding、Assurance、Effect
   Gate、dispatch 和 post-action verification。
4. 新观察、路线/合约变化、冲突或验证失败会废弃剩余计划；Replan 消耗当前 Run
   的全局咨询预算，不能重置预算。
5. Slow 只提供语义证据和策略建议，沿用 PER-017/018、P2、Evidence Ledger 和
   provenance；晚到结果不能追溯性授权已过期的 Effect。
6. 人工策略对不可逆动作、禁止项和审批要求拥有最终约束；记忆和外部资料只能
   帮助形成计划。

## Acceptance

1. Plan 具有稳定的 DecisionId 相关性、有限步数、合法字段和 fail-closed 校验；
   malformed/unknown/超预算计划零 Effect。
2. 多步 Plan 逐步执行：每次最多一个 Act，动作后必须重新观察和验证。
3. Observe 不产生 Effect，也不能单独宣布覆盖完成；Settings record-only 由
   既有 coverage ledger 记录。
4. 无 XML 但 grounding 证据充分时可以继续；证据不足时进入有界 Reobserve、
   Replan 或 fail closed。
5. Slow 在无 XML、结构/视觉冲突、弹窗连续失败或语义不明确时可异步发起；
   超时、未配置、冲突或晚到结果不授权动作。
6. 弹窗可以走 obstacle 分支；清障动作前后有同一分类器证据，弹窗未消失时
   不进入普通遍历。
7. SettingsCoverageDirector 接受受约束的 Reobserve、Replan 和 obstacle 分支，
   仍由 Coverage 配额和终止条件收口。
8. 现有 Kernel、Host、Slow 和相关回归测试通过；新增行为有 evidence 引用。

## Constraints

- Slow 视觉能力必须由 profile 显式配置；文本 Slow 只能处理实际获得的文本/布局。
- XML 对结构事实更强，Slow 对语义解释更强；单一 confidence 不拥有 authority。
- 计划不能绕过 P2、WorldBelief、Grounding、Assurance、Effect Gate 或 Verification。
- 当前共享工作区存在其他脏改动；实现必须在独立 worktree 或等价隔离目录完成。

## Verification

```yaml
level: CONTRACT
method: Plan/Slow/Director deterministic tests; focused Kernel and Host regression; full suite where available
expected: "Plan contract, bounded transitions, no stale authorization, obstacle recovery and no-XML behavior satisfy Acceptance"
actual: pending
evidence: pending
```

## Status log

- 2026-10-02 · persisted · 由 Settings traversal grill、行业一手资料审核和数学/职责完备性审阅收敛；等待 GLM Leader 实施。
