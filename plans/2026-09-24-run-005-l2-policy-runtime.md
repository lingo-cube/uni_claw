# RUN-005 — L2 Policy Protocol & Runtime（设计稿 v0.1）

> Status: DESIGN DRAFT（待 Review / Grill）
> Authority: NONE（不修改 AGT-001/baseline；上游顺序见 §0）
> 上游：RUN-004（CLOSED，多轮咨询协议冻结）· AGT-001（CLOSED，设计 FROZEN：
> Policy = closed typed bounded contingent package，Kernel never invents/repairs/
> extends）· product-architecture-baseline §24.2（Bounded Contingent Decision
> Package 语义 + 不变量 43/45）· consultation-protocol-v0.1（T5 预留、V6 预留）·
> decision-granularity-scenarios-v0.1（18 场景）
> 代码基线：AgentDecision.cs / KernelRunDriver.cs（Drive 状态机 L240-534）/
> Run/ExecutionContract.cs（MaxConsultations/MaxTotalSteps）/ ConsultationTypes.cs
> 日期：2026-09-24

---

## 0. 一句话与不变量

**L1 → L2 的差异**：L1 的未来 steps 已知（[A,B,C]，Agent 预先展开）；L2 的
未来 application 数量/目标依赖**后续 fresh observation** 才能决定——Agent
给规则，Kernel 按最新世界逐轮展开。

**不变量（全程成立）**：

```text
UniAgent creates Policy · Kernel executes Policy
Kernel NEVER invents / repairs / extends Policy（含 scope）
Policy = bounded contingent advisory decision package
   ≠ workflow ≠ program ≠ script ≠ effect batch ≠ driver macro
```

---

## 1. Current RUN-004 Capability（继承面，不重建）

已有且直接复用：四元决策缝的 Act/NoAction/Defer 三员 · 六触发点 v1 五相 ·
D1-D7 纪律（幂等、DecisionId 回带、预算合同、步间不咨询、失败整支作废、
同步缝）· V1-V5 校验（V6 预留给 Policy）· Act ≤16 步串行 + 不变量 43 屏障
（StepAct→StepVerify 每步：TargetSpec adopt → Control SelectIntent →
ActViaCurrentGrounding → gate → dispatch → post-action 观察 →
VerifyPostActionEffect）· 失败转移（StepRejected/VerificationFailed +
`_pendingFailure*` → NeedDecision）· 预算合同（MaxConsultations 16 /
MaxTotalSteps 256，`_stepsDispatched` 逐次消费）· Defer 有界等待 ·
完成证明三层。

RUN-005 **只增加**：`AgentDecision.Policy` 第四员 + 展开循环 + Policy 词汇表
+ PolicyInvalidated 边界。

---

## 2. Minimal Policy Model（Q1：.NET authoritative records 草案）

```csharp
public abstract record AgentDecision
{
    ...                                  // 既有三员不动
    public sealed record Policy(PolicyProposal Proposal) : AgentDecision;  // NEW
}

public sealed record PolicyProposal(
    string PolicyId,                     // policy-{sessionId}-{n}（Agent 侧唯一）
    PolicyScope Scope,                   // §2.1
    IReadOnlyList<PolicyPredicate> Match,        // 合取；空 = 非法（V6）
    PolicyActionTemplate ActionTemplate,         // 唯一模板（v1：单模板）
    IReadOnlyList<PolicyPredicate> Termination,  // 合取；非空必填（V6）
    IReadOnlyList<PolicyGuard> Guards,           // 可空（无守卫 = 仅预算上界）
    PolicyBounds Bounds,                 // §6
    PolicyFallback Fallback,             // Reconsult（默认）| FailClosed
    string? Justification);

public sealed record PolicyScope(
    string? ContainerSignature);         // null = 当前唯一 root container
                                          //（与 driver 既有单根假设同构，L397-399）

// —— closed typed AST：discriminator + typed literal operands，仅此四种 ——
public abstract record PolicyPredicate
{
    public sealed record ClaimEquals(string Subject, string Value) : PolicyPredicate;
    public sealed record ClaimNotEquals(string Subject, string Value) : PolicyPredicate;
    public sealed record ElementExists(string Role, string? TextContains = null) : PolicyPredicate;
    public sealed record ElementMissing(string Role) : PolicyPredicate;
}

public sealed record PolicyActionTemplate(
    string EffectClass,                  // 必须 ∈ contract.AllowedEffects（校验）
    string? DesiredState);               // 语义终态字面量；无则 null（点击类）
    // 应用对象 = 当前 match 绑定的元素（语义 role），永不携带坐标/命令/脚本

public abstract record PolicyGuard       // closed guard kind + typed inputs + tri-state
{
    public sealed record ObservationUnchanged(string Subject, int AfterRounds) : PolicyGuard;
}

public sealed record PolicyBounds(
    int MaxApplications,                 // 模板最大适用次数（>0）
    int MaxRounds);                      // 展开轮次上限（≥ MaxApplications）
    // MaxTraversal：DEFER——唯一买家 A1 全树遍历需要 visited 记忆，随 A1 案另裁

public enum PolicyFallback { Reconsult, FailClosed }
```

不提前造的类型（登记）：多模板（B4 每字段不同输入——buyer 到来再加）·
元素属性过滤谓词（A1 checkable 禁点，随 A1）· OR/嵌套组合（v1 只有合取）·
数值比较谓词（A2「temp>20」的 v1 表达 = ClaimNotEquals(temp,"20")，如需
真序比较随真实温控 buyer 立项）。

### 2.1 L1/L2 判据（Q12，冻结）

> **是否需要未来 fresh observation 才能决定下一 application**——是 = L2
>（Policy），否 = L1（Act）。不用复杂度区分。

---

## 3. Closed Vocabulary（Q2/Q3：场景反推矩阵）

| Primitive | 场景来源（granularity 文档 §2/§4） | 消费角色 |
|---|---|---|
| `ClaimEquals(s,v)` | A2 终止 temp=20 · A4 终止音量值 · C1 终止 light=green | Termination |
| `ClaimNotEquals(s,v)` | C1 match light≠green · A2 match temp≠20 | Match |
| `ElementExists(role)` | B6 终止「目标商品出现」；WiFi 示例终止「wifi entry found」 | Termination |
| `ElementMissing(role)` | B2 终止「通知中心空」· B6 match「本屏无目标→滚动」 | Match+Termination |
| Guard `ObservationUnchanged(s,n)` | A2 守卫「连续 2 次温度不变」· C1「灯不变」 | Guard |
| Template `{EffectClass, DesiredState?}` → matched element | A2/A4/C1（按键类）· B2（清除）· B6（滚动一屏） | Action |

**明确不进 v1 的（无当前消费者，逐一登记）**：`ObservationChanged`（B7 等待
语义 = 既有 Defer 域）；`MatchExists/MatchMissing` 作 guard（与 match 谓词
重复）；`BudgetRemaining` guard（预算是机械执法不是守卫）；元素属性谓词
（A1/B4 buyer）；`ElementRoleEquals`（被 `ElementExists(role)` 覆盖）。

---

## 4. PolicyState（Q4/Q5）

**Owner：A —— KernelRunDriver private execution state**（进程内 ephemeral）。

依据（非实现方便）：baseline §24.2 对 Bounded Contingent Decision Package 的
既有裁决「Kernel 只入口校验、Control 只记 ref、**Run Model 不存内容**」→
不进 RunModel（B 拒）；不新建 owner（C 拒——AGT-001 GQ2 已 DEFER 一切
durable policy/consultation owner，RUN-005 不推翻）。

```csharp
// driver 私有（与 _adoptedDecision/_deferRoundsUsed 同层同寿命）
PolicyExecutionState? _policy;   // 字段：PolicyId · ApplicationsUsed · RoundsUsed
                                 // · GuardState（逐守卫 tri-state + 连续未变计数）
                                 // · TerminationStatus（Pending|Satisfied|Unprovable）
```

**不保存且永不权威化**：world truth · effect 成功真值 · target truth ·
match/binding（**每轮从 fresh belief 重派生**，绑定从不缓存——下游 Grounding
本就 fresh）。**Restart 语义**：不恢复、不持久——进程重启 = 活跃 Policy
丢弃 → 回 NeedDecision 重新咨询（与 AGT-001 §2 restart 链完全一致；
cross-process policy resume 在 AGT-001 DEFER 清单内，RUN-005 不解决）。
**对 Agent 可见性**：重咨询时经既有 `ConsultationProgress` 携带 policy 进度
摘要（协议 §2.1 的 `PolicyState?` 槽位即为此预留）——摘要派生自 ephemeral
状态，非 canonical record。

---

## 5. Expansion Algorithm（Q6/Q7/Q11：单轮算法，全链复用）

```text
[NeedDecision] 采纳 Policy（V6 校验通过，_policy 初始化，进入 PolicyExpand）
      ↓（每轮循环体——每次只展开一步，禁止生成 future effect list）
┌─ PolicyExpand（新 phase）────────────────────────────────────┐
│ 1. fresh observation（External）→ reconcile → fresh belief    │
│ 2. scope check：root container 仍匹配 Scope？否→ PolicyInvalidated │
│    (scope-invalidated) → 回 NeedDecision                      │
│ 3. evaluate Termination（对 owner-derived 投影机械求值）        │
│    全 Satisfied → PolicySucceeded → 等价 E1 提案耗尽：          │
│        _policy=null → 回 NeedDecision（StepVerified 相位）     │
│    任一 Unknown  → PolicyInvalidated(termination-unprovable)  │
│ 4. evaluate Guards（tri-state）                                │
│    Violated → PolicyInvalidated(guard-violated)               │
│    Unknown  → 按 Fallback：Reconsult → PolicyInvalidated       │
│               (guard-unknown)；FailClosed → 终局 fail-closed   │
│ 5. bounds check：Applications/MaxApplications、Rounds/MaxRounds │
│    超 → PolicyInvalidated(bounds-exhausted)                    │
│ 6. evaluate Match（合取）                                      │
│    无匹配 → PolicyInvalidated(no-match)                        │
│ 7. derive ONE step：TargetSpec(matchedRole, template.EffectClass,│
│    template.DesiredState) —— 与 Act 步同形                     │
└──────────────────────────────────────────────────────────────┘
      ↓ 复用既有 StepAct→StepVerify 全链（零新执行器）
Control SelectIntent → ActViaCurrentGrounding（fresh）→ Assurance/Gate
      → dispatch（_stepsDispatched++ 消费合同步数）→ 不变量 43 屏障
      → post-action 观察 → VerifyPostActionEffect
      ↓ verified
update _policy counters（ApplicationsUsed++ / RoundsUsed++ / GuardState）
      → 回 PolicyExpand（fresh observation 重启循环）
      ↓ 未 verified
既有 VerificationFailed 失败转移：剩余 Policy 整支作废（D5 同律）
      → _policy=null → NeedDecision(VerificationFailed)
```

**Q11 裁决**：Policy 展开步 = 逐轮物化的**单步 Act 消费**——直接进既有
StepAct 路径，**不建 PolicyEffectExecutor**（新逻辑只有「下一步是什么」与
「还要不要继续」两问，均为封闭 AST 机械求值）。

**Q7 Freshness（逐项证明）**：每轮①fresh observation/reconcile；②match 每轮
重派生（绑定不缓存）；③Grounding fresh（ActViaCurrentGrounding 既有语义）；
④Assurance fresh（三元组逐次）；⑤验证走 post-action 屏障。**Policy 只授权
策略形态，不授权任何未来具体 effect**——任何一轮的 dispatch 都独立走完整
授权链（不变量 43 逐次适用）。

---

## 6. Budget Model（Q10：两层，机械分层）

```text
Contract budget（全局上界，不变）：MaxConsultations(16) · MaxTotalSteps(256)
   > Policy local bounds（更小局部约束）：MaxApplications · MaxRounds
```

- V6 校验（采纳时）：`0 < MaxApplications ≤ StepsRemaining` 且
  `MaxApplications ≤ MaxRounds ≤ RoundsRemaining`——**Policy 不得扩大
  Contract budget**（结构性不可能：每次 dispatch 照常 `_stepsDispatched++`）。
- Policy 轮 = 展开循环体一次（含其 fresh observation）；application = 一次
  成功 dispatch 的模板适用。Policy 咨询轮照常消费 MaxConsultations。

---

## 7. Failure & Reconsult Mapping（Q8/Q9/Q14）

| Policy 内结局 | 映射 | 相位（Phase） |
|---|---|---|
| Termination 全 Satisfied（PolicySucceeded） | E1 提案耗尽同构 → 重咨询（Agent 决定 NoAction/Act/…） | 既有 `StepVerified` |
| Bounds exhausted / NoMatch / GuardViolated / GuardUnknown(Reconsult) / ScopeInvalidated / TerminationUnprovable | 重咨询，携带 reason + policy 进度摘要 | **新 `PolicyInvalidated`**（唯一新增 phase，typed reason 枚举） |
| 展开步 grounding/gate 失败 | 既有失败转移（剩余 Policy 作废） | 既有 `StepRejected` |
| 展开步 post-action 验证失败 | 既有失败转移（同上，D5 同律） | 既有 `VerificationFailed` |
| GuardUnknown + Fallback=FailClosed | fail-closed 终局（零新 effect） | 终局状态复用（TerminalNotProved 语义，reason=policy-guard-unknown） |

**Q14 裁决**：新增**一个** `AgentDecisionPhase.PolicyInvalidated`（typed
reason：BoundsExhausted/NoMatch/GuardViolated/GuardUnknown/ScopeInvalidated/
TerminationUnprovable），吸收 consultation-protocol 预留的 T5 词位。理由（从
咨询语义出发）：Agent 必须能区分「你的 Policy 结构性失效」与「某一步普通
失败」——修复策略不同（换 Policy vs 降 L0）；StepRejected/VerificationFailed
的载荷语义（FailedStepIndex + 步级原因）无法承载 policy 级现场。步级失败
**不**新增相位（复用既有两者）。不造平行状态机：PolicyInvalidated 是
NeedDecision 的输入相位（与 StepRejected 同构），不是 Drive 新状态。

## 8. Termination ≠ Goal Completion（Q15）

```text
Policy termination satisfied → 回 decision boundary（StepVerified 相位）
  → Agent 决策（NoAction+CompletionEvidence / Act / 新 Policy / …）
  → 既有 TerminalEvaluation → RuntimeOutcome → UniAgent Goal Evaluation
```

Policy 成功只表示**策略任务完成**（「找到 wifi entry」≠「Primary Goal
完成」）。Goal 完成判定权恒在 UniAgent（GEV-004）；Kernel 无完成宣告权
（P12 场景执法：termination 达成而 obligation 未满足 → 不得假 Completion）。

---

## 9. Validation（Q13：V6 规则集，全 fail-closed，不修复）

```text
V6a schema/closed vocabulary：未知 AST 节点/未知 guard kind → reject
V6b Match 非空合取 · Termination 非空合取
V6c Bounds：0 < MaxApplications ≤ StepsRemaining；MaxApplications ≤
    MaxRounds ≤ RoundsRemaining（不得扩大合同预算）
V6d ActionTemplate.EffectClass ∈ contract.AllowedEffects（P9：禁行效应
    类在 dispatch 前拒收）
V6e Scope 合法（容器签名可解析）
V6f Fallback/Reconsult 合法枚举
V6g DecisionId 回带（V1 既有，三态同律）
V6h PolicyId 唯一（同 run 内不重复采纳同 id）
```

## 10. Simulation Strategy（Q16）与验收场景（Q17）

第一阶段仅 ScriptedUniAgent + deterministic Policy（禁止 DSH/DeepSeek）。
ScriptedUniAgent 增 Policy 脚本形态（确定性发射 Policy/按相位回应）。

| # | 场景 | 映射载体（优先真实资产） | 断言核心 |
|---|---|---|---|
| P1 | 即时匹配→单次适用→成功 | golden wifi 帧（obs-1 off：ElementExists(toggle)+ClaimNotEquals(wifi,true) → tap → obs-2 on → ClaimEquals 终止） | 1 application、fresh 观察、StepVerified 重咨询 |
| P2 | 重复适用→终止成功 | 确定性驱动递减 claim（A2 温控同构：temp 24→20） | N applications、每轮 fresh、计数正确 |
| P3 | 无匹配→重咨询 | off 帧缺目标 role | PolicyInvalidated(no-match) 相位+进度 |
| P4 | bounds 耗尽→重咨询 | P2 驱动 + MaxApplications=2 | bounds-exhausted + 诚实失败 |
| P5 | guard violated→重咨询 | temp 恒定驱动 + ObservationUnchanged(2) | guard-violated |
| P6 | guard Unknown→fail-closed | Ambiguous 元素帧 | 按 Fallback 两分支 |
| P7 | 中途验证失败→剩余作废 | contradictory-post 资产（BARRIER-003 同构） | VerificationFailed 相位、后续步不 dispatch |
| P8 | application 间世界变化 | 多帧序列（off→on→…） | 每轮 fresh match/termination |
| P9 | 禁行 effect class | template effectClass="swipe" ∉ {tap} | V6d 采纳前拒收 |
| P10 | 局部预算>合同余量 | bounds 撑爆 StepsRemaining | V6c 拒收 |
| P11 | 未知 AST 节点 | 脚本发射非法节点 | V6a 拒收 |
| P12 | 终止成功但 Goal 未满 | termination=ElementExists(menuItem)，obligation 仍欠 | 无假 Completion（TerminalEvaluation 如实判） |

## 11. Authority Matrix Delta（RUN-005 新增行）

| 能力 | Owner |
|---|---|
| 创建 Policy（含 bounds/termination/guard 语义） | **UniAgent**（唯一） |
| 入口校验（V6/V1） | Kernel（机械，既有 ValidateDecision 扩展） |
| Policy 执行状态（ephemeral counters） | KernelRunDriver（私有，不持久） |
| 谓词/守卫求值 | Kernel driver（对 owner-derived 投影的**封闭 AST 机械求值**——非规划；投影仍由 World/Evidence owner 派生） |
| 单步 Control Intent 签发 | Control（既有，不变） |
| Grounding / Assurance / Effect / Verification | 既有 owner（逐次 fresh，不变） |
| Goal 完成判定 | **UniAgent**（GEV，不变） |

**Kernel 是否因 Policy 获得规划权？否**——Kernel 只做：求值封闭 AST（输入
全部来自 Agent 的声明 + World owner 投影）、选择「下一个匹配」机械规则、
逐次复用既有执行链。发明/修复/扩展 Policy 的代码路径不存在（无编辑面：
Policy record immutable，driver 无 mutation API）。

## 12. Open Questions

1. **OQ-相位命名**：`PolicyInvalidated` vs 保留预留名 `PolicyGuardTripped`
   （后者语义窄于实际六种 reason）——倾向前者，grill 裁决。
2. **OQ-谓词求值投影形状**：Policy 求值消费的 owner-derived 投影是复用
   咨询 context 摘要（Elements/Claims）还是独立 PolicyEvaluationView
   （ADR-0011 Consumer View 规则）——倾向独立最小 view（字段有真实读取
   证据），实现 change 定形。
3. **OQ-ClaimNotEquals 表达 A2「temp>20」的充分性**：等值否定 ≠ 序比较；
   若真温控 buyer 要求区间语义，届时加 `ClaimInRange`（closed）——v1 不加。

## 13. Files To Change Later（只列不改）

```text
src/UniClaw.Kernel/Runtime/AgentDecision.cs        +Policy 成员
src/UniClaw.Kernel/Runtime/PolicyTypes.cs（新）     PolicyProposal/Predicate/Guard/
                                                   Bounds/Scope/Fallback + Phase
src/UniClaw.Kernel/Runtime/KernelRunDriver.cs      PolicyExpand phase + 失败映射 + V6
src/UniClaw.Kernel/Runtime/ConsultationTypes.cs    ConsultationProgress.PolicyState 摘要
tests/…/ScriptedUniAgent.cs                        Policy 脚本形态
tests/…/PolicyRuntimeTests.cs（新）                P1-P12
tests/…/SimulationHost.cs / SimContract.cs         bundle Policy 脚本缝（如需）
scenarios/（新条目或既有条目 execution/options 不变；期望字段如适用）
```

## 14. Architecture Delta

**RUN-004 保持不变**：Act/NoAction/Defer 语义 · 六触发点既有五相 · D1-D7 ·
V1-V5 · Act 执行链与 43 屏障 · 预算合同 · 完成证明三层 · Determinism 语义。
**RUN-005 只增加**：Policy 第四员（closed typed records）· 4 谓词 + 1 守卫 +
1 模板的最小词汇表 · driver ephemeral PolicyState · PolicyExpand 单轮循环
（复用 Act 链）· V6 校验 · PolicyInvalidated 相位 · 两层预算执法 · P1-P12
deterministic 验收。

## 15. Verdict

**READY_FOR_GRILL**
