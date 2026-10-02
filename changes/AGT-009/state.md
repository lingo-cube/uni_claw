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

### 实施期冻结（Leader，2026-10-02）

7. Plan 契约形状：`AgentDecision.Plan(DecisionId, AgentPlanProposal(Items,
   Justification))`（DecisionId 在 union 成员上，Policy 先例）；`PlanItem` =
   `ActItem(TargetRole, TargetDescriptor, EffectClass, DesiredState?)` |
   `ObserveItem(Subject?)` | `ControlItem(PlanControlKind, Reason?)`。
   「不能包含坐标/occurrence/selector/Effect 授权」由类型形状执法（结构上
   不存在这些字段），非运行时过滤。
8. V7 校验（全部 fail closed 零 Effect）：plan:empty / plan:too-many-items
   （上限 16，与 MaxProposalSteps 同界）/ plan:missing-target-role /
   plan:missing-effect-class / plan:effect-class-not-allowed /
   plan:unsupported-desired-state / plan:unknown-control（枚举闭合外）/
   plan:unknown-item（closed union 外派生类型）/ budget-exceeded:steps
   （ActItem 数 > StepsRemaining）/ correlation-mismatch（D2 同律）。
9. 执行机制：新 `PlanExpand` phase 逐项消费（良基：每轮恰一项/物化单步/有界
   转移退出）；ActItem 物化为单步复用 StepAct→StepVerify 全链（零新执行器、
   零新状态机）；ObserveItem 拉取一轮 External 观察（零 Effect，可恢复）；
   Control 只做有界转移。步锚归档 (consultN, planItemIndex)——多 ActItem
   的完成锚唯一可核验。
10. 失效条件的精确化：步链失败（grounding/gate/verification/no-root）→
    VoidActivePlanForStepFailure（镜像 policy 作废先例）；「冲突」= 与剩余
    ActItem 语义目标相交的权威域悬案（"role[:desc]" 相等或前缀——与
    AgentPlanPolicy 聚焦复查同律；实测一刀切「任何悬案」会误杀合法多步流：
    ui.container.observed 值变化悬案不触及 menuItem 目标）。路线/合约变化：
    合约在 Run 内结构冻结；路线变化经 grounding no-match / 验证失败既有
    路径自然废弃（计划最小字段不含期望屏幕声明）。
11. E4 语义：ActItem 目标已满足（Control desired-state satisfaction）→
    零 dispatch 消费该项继续计划。ObserveItem 与计划耗尽都不构成 completion
    proof——终局仍由 NoAction+Completion / TerminalEvaluation 既有语义判定。
12. Stop：废弃剩余计划、零新 Effect、不再消耗咨询轮次，直接终局评估（层1
    如实判定完成或未证）。
13. DSH wire 协议：DecisionJsonConverters 对未知 decision kind 已 fail
    closed（"plan" 的线上序列化是 AGT-009 范围外的 wire 扩展，Kernel/Host
    进程内 seam 不受影响）。
14. 场景认证：Kernel 源变更使 29 个场景 seal 失效（运行时源码哈希钉扎，
    SIM-002 设计行为）；场景行为测试全部不变绿后经唯一 sanction 路径
    `tools/scenario_certify.py --change AGT-009 --all` 重认证。

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
- 2026-10-02 · implemented · Leader 在独立 worktree（../uni_claw-agt009，快照 a4d3b391 = PER-017/018 + Settings Traversal + 确认改动）冻结 Plan 契约并落地 Kernel 侧（AgentDecision.cs / KernelRunDriver.cs PlanExpand / 16 项确定性测试 / 白名单 + tripwire / 场景重认证 AGT-009）；Worker（glm-5.3-flash，独立分支 agt009-worker）承接 Host/Slow/Director 接入（WI-AGT009-002）。实施期冻结决策见 §Decisions 7-14。
