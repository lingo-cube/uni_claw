# RUN-005 — L2 Policy Protocol & Runtime（设计稿 v0.3 · FROZEN）

> Status: **FROZEN（RUN-005 design freeze，2026-09-24，owner 裁决
> PASS_WITH_ONE_NARROW_AMENDMENT；Further Grill NOT REQUIRED）**
> Lineage: v0.1 Draft → Owner 预审（PASS_WITH_FINDINGS）+ 独立盲审（REOPEN）
> → v0.2 dual-grill revision → Owner 终裁（PASS_WITH_ONE_NARROW_AMENDMENT）
> → v0.3 narrow amendment（semantic lease 冻结 + Owner 决策 1/2 + GuardCursor
> warm-up 澄清）→ **FROZEN**。
> 冻结后修改须经独立 change；实现以本稿为权威（Slice A→B→C，见
> changes/RUN-005/plan.md）。
> Authority: NONE（不修改 AGT-001/baseline；上游顺序同前）
> 日期：2026-09-24 · 修订日志见 §16

---

## 0. 一句话与不变量（不变）

**L1 未来 steps 已知（Agent 预先展开）；L2 下一 application 依赖后续 fresh
observation（Agent 给规则，Kernel 机械展开）。**

```text
UniAgent creates Policy · Kernel executes Policy
Kernel NEVER invents / repairs / extends Policy（含 scope）
Policy = bounded contingent advisory decision package ≠ workflow/program/
         script/effect batch/driver macro
一切 Policy 内结局 fail closed 回 Agent decision boundary（基线原文语义）
```

## 1. Current RUN-004 Capability（继承面，不变）

见 v0.1 §1（全部继承：三员决策、五相、D1-D7、V1-V5、Act 链 + 43 屏障、
失败整支作废、预算合同 16/256、完成证明三层）。代码锚点补充：
`CurrentBudget.RoundsRemaining = MaxConsultations - _consultCounter`（L566-568，
**咨询轴**）；`PhaseForCurrent`：`_completedSteps.Count>0 → StepVerified 否则
InitialPlanning`（L638-648）；`DeriveElementSummaries` 无稳定元素 identity、
`Text` 恒 null（L673-683）；`ElementEpistemic.Ambiguous` v1 不产生。

## 2. Minimal Policy Model（v0.2 修订）

```csharp
public abstract record AgentDecision
{
    ...                                   // 既有三员不动
    public sealed record Policy(PolicyProposal Proposal) : AgentDecision;  // NEW
}

public sealed record PolicyProposal(
    string PolicyId,
    IReadOnlyList<PolicyPredicate> Match,        // 合取门控（非空）
    PolicyActionTemplate ActionTemplate,         // 自带语义目标（≠ match 派生）
    IReadOnlyList<PolicyPredicate> Termination,  // 合取（非空）
    IReadOnlyList<PolicyGuard> Guards,           // 可空
    int MaxApplications,                         // 唯一局部预算（>0）
    string? Justification);
// v0.2 删除项（grill 裁决）：PolicyScope 字段（§4）、PolicyFallback 枚举
//（§7）、MaxRounds（§6）——理由各节。

// —— closed typed AST；求值一律 PolicyTruth 三态（§3）——
public abstract record PolicyPredicate
{
    public sealed record ClaimEquals(string Subject, string Value) : PolicyPredicate;
    public sealed record ClaimInSet(string Subject, IReadOnlyList<string> Values)
        : PolicyPredicate;                       // 有界方向语义（F8(o)/F3(o)）
    public sealed record ElementExists(string Role) : PolicyPredicate;
}
// v0.2 删除项：ClaimNotEquals（过冲反例 19→18；A2/C1 由 ClaimInSet 覆盖）·
// ElementMissing（absence 不可证——缺 coverage 输入，随 B6 DEFER）·
// TextContains（Text 恒 null，无数据源）。

public sealed record PolicyActionTemplate(       // = AgentActionStep 同形（F2(o)）
    string TargetRole,                           // 语义目标——Kernel 零猜测
    string? TargetDescriptor,
    string EffectClass,                          // ∈ AllowedEffects（V6d）
    string? DesiredState);                       // 语义终态字面量

public abstract record PolicyGuard               // closed kind + typed inputs + tri-state
{
    public sealed record ObservationUnchanged(string Subject, int AfterRounds) : PolicyGuard;
}

public enum PolicyTruth { Satisfied, Violated, Unknown }   // 一切谓词/守卫的求值格
```

**v1 谓词收敛为三员 + 单守卫 + 单模板**。不提前造（登记 buyer）：元素属性
过滤（A1/B4）· 多模板（B4）· coverage 依赖谓词（B6）· 序比较（真区间 buyer）·
逐元素记忆（B2/A1，granularity §5-2 明文归驱动器执行态——随其 buyer）。

## 3. Epistemic Evaluation（v0.2 新增：PolicyTruth 逐 primitive 推导表）

一切 Match/Termination/Guard 求值为 `PolicyTruth`；**Unknown 一律 fail closed
回 decision boundary**（基线 §24.2 原文语义；M1 认识论词汇落地）：

| Primitive | Satisfied | Violated | Unknown |
|---|---|---|---|
| `ClaimEquals(s,v)` | claim 存在且 Value==v 且 !InConflict | claim 存在且 Value≠v 且 !InConflict | subject 缺席 · InConflict=true（SR-022 族：conflicted claim 永不满足终止） |
| `ClaimInSet(s,vs)` | 存在、!InConflict、Value∈vs | 存在、!InConflict、Value∉vs | 同上 |
| `ElementExists(r)` | 本 revision occurrences 含 role=r 且 Epistemic=Observed | **v1 永不**（absence 不可证——bounds 耗尽兜底） | 未见 · Epistemic≠Observed（Ambiguous v1 不产生，防御性保留） |
| `ObservationUnchanged(s,n)` | 连续未变计数 < n（含 warm-up：首样本前/窗口未满） | 连续 n 轮值不变 | 本轮 subject 值 Unknown（缺席/冲突） |

合取语义：任一 Unknown → 整体 Unknown；任一 Violated → 整体 Violated；
否则 Satisfied。

## 4. Scope 与 Semantic Lease（v0.3：lease 规则冻结）

**v1 无 PolicyScope 字段**（v0.2 裁决维持）。Scope 语义 = 「当前唯一 root
container」；invalidation 于 container identity 变化 · 零/多 root（既有检查
覆盖）。**内容变化（滚动/可见元素变化/content revision）≠ lease 失效**。

**Semantic Lease 冻结规则（owner 终裁必补 1——不整体 DEFER）**：

```text
Policy adoption  → bind current active execution lease
每次 PolicyExpand → verify lease still valid
lease invalid    → PolicyInvalidated(LeaseInvalidated) → NeedDecision
                 → zero new Effect
```

**仍 DEFER**（登记）：lease detection vocabulary（检测机制词表）·
human-preemption detection（§24.7 检测机制 = Phase 6/7 buyer）·
cross-process lease recovery（AGT-001 GQ2 同族）。v1 的 lease 判据 =
上述 scope 语义（容器身份 + 单根持存）——检测词表深化随 buyer。

### 4.1 PolicyEvaluationView（Owner 决策 2：独立最小视图）

谓词/守卫求值的输入载体 = **独立最小 `PolicyEvaluationView`**，不绑定
`AgentDecisionContext`（避免未来 Agent context 裁剪影响 Policy runtime
correctness）。契约（owner 原文语义）：**owner-derived · read-only ·
ephemeral · fresh-derived（每轮即席派生）· not persisted · not recovery
state · not authority**。字段 = PolicyTruth 求值所需的最小 claim
（Value/Disposition/InConflict）与 occurrence（Role/Epistemic）投影；
按 Consumer View 规则（ADR-0011）由 World owner 派生；Text 不进 v1。

## 5. Expansion Algorithm（v0.2 修订：单轮，全链复用 + E4 映射）

```text
[NeedDecision] 采纳 Policy（V6 通过；_policy 初始化；进入 PolicyExpand）
┌─ PolicyExpand（每轮恰一步；良基不变量：每次重入 remainingApplications
│  严格减 1 或退出——语义上等价 ≤N 节点有限展开 DAG，F5(o)）──────────┐
│ 1. fresh observation（External）→ reconcile → fresh belief           │
│ 2. lease check：active execution lease 仍有效（scope 语义：容器身份  │
│    + 单根持存）？否→ exit(lease-invalidated)；零/多根 → 既有路径      │
│ 3. Termination := 合取求值（PolicyTruth，经 PolicyEvaluationView）    │
│    Satisfied → exit(policy-succeeded)；Unknown → exit(termination-   │
│    unprovable)；Violated → 继续                                      │
│ 4. Guards := 逐个 tri-state：Violated → exit(guard-violated)；       │
│    Unknown → exit(guard-unknown)                                     │
│    【v0.2：无 Fallback 分支——Unknown 同样回 decision boundary】      │
│ 5. bounds：ApplicationsUsed ≥ MaxApplications → exit(bounds-         │
│    exhausted)                                                        │
│ 6. Match := 合取求值：Satisfied → 继续；Violated/Unknown →           │
│    exit(no-match / match-unknown)                                    │
│ 7. 物化单步：AgentActionStep(template.TargetRole, …, template.       │
│    EffectClass, template.DesiredState) —— 目标来自模板，Kernel 零猜测 │
└──────────────────────────────────────────────────────────────────────┘
      ↓ 复用既有 StepAct→StepVerify 全链（零新执行器；E4b 聚焦复查在步链
        内部，FocusRetryCap 有界，与 Policy 轮无关）
Control SelectIntent → fresh Grounding → Assurance/Gate → dispatch
（_stepsDispatched++，消费合同步数）→ 43 屏障 → post-action 验证
      ↓ verified：ApplicationsUsed++ / GuardCursor 更新 → 回 PolicyExpand
      ↓ SelectIntent 返回 plain-Observe（E4，目标已满足）：
          重评 Termination on fresh belief——Satisfied → exit(policy-
          succeeded)；否则 exit(control-non-act)（F7(b)：消灭静默楔死）
      ↓ grounding/gate 失败：既有 StepRejected 转移（剩余 Policy 作废）
      ↓ 验证失败：既有 VerificationFailed 转移（同上，D5 同律）
```

## 6. Budget Model（v0.2 修订：F1(o)/F2(b)）

```text
Contract（全局，不变）：MaxConsultations(16) · MaxTotalSteps(256)
Policy local（唯一）：MaxApplications
V6c：0 < MaxApplications ≤ StepsRemaining（= MaxTotalSteps - _stepsDispatched）
```

- **展开轮不消耗 MaxConsultations**（显式声明：PolicyExpand 循环体零咨询；
  仅 exit 后的重咨询照常计轮）。
- **v1 无 MaxRounds**：算法中每次重入必 dispatch（消耗 application）或退出，
  无「既不 dispatch 又不退出」的轮——单约束即完备有界（F1(o) 推论采纳）。

## 7. Failure & Reconsult Mapping（v0.2 修订：F1(b)/F4(o)/F6(o)/F10(b)）

| Policy 内结局 | Phase | 载荷 |
|---|---|---|
| Termination Satisfied（含 0-application 即时满足、E4 路径） | 既有 `StepVerified`（**经 `_pendingPolicyOutcome` 显式捕获**，不依赖 `_completedSteps` 推导——F7(o)/F6(b)） | policy 摘要 |
| bounds-exhausted / no-match / match-unknown / guard-violated / **guard-unknown** / **lease-invalidated** / termination-unprovable / control-non-act | 新 `PolicyInvalidated`（typed reason；**Owner 决策 1：采用此名，不用 PolicyGuardTripped——它统一承载一切 policy 级 invalidation，只是 NeedDecision 的 typed cause，不建新状态机**） | reason 原文（不净化，M2）+ policy 摘要 |
| 展开步 grounding/gate 失败 | 既有 `StepRejected` | 既有载荷 + policy 摘要 |
| 展开步验证失败 | 既有 `VerificationFailed` | 同上 |
| **invalidation 时 `RoundsRemaining ≤ 0`** | 诚实失败终局（`consult-budget-exhausted` 既有语义，F10(b)） | — |

**`PolicyFallback` 字段删除（F1(b)/F4(o)）**：一切 invalidation（含 Guard
Unknown）唯一出口 = 零新 effect + 回 Agent decision boundary；safe-stop/
terminal 权威恒在 Contract/Run/Assurance，Policy 无 Run 终局权。终局状态复用
零（v0.1 的 TerminalNotProven 分支删除——该状态本就非终态，会楔死 run）。

## 8. PolicyState（v0.2 修订）

Owner 不变：**KernelRunDriver private ephemeral**（baseline「Run Model 不存
内容」+ AGT-001 GQ2 DEFER）。字段 v0.2：

```csharp
PolicyExecutionState? _policy;   // PolicyId · ApplicationsUsed · TerminationStatus
PolicyGuardCursor?[]  _guardCursors;  // 每 guard：Subject · LastObservedValue ·
                                      // ConsecutiveUnchangedCount（历史比较
                                      // operand，非 current World truth）。
                                      // 【v0.3 澄清】Warm-up ≠ PolicyTruth.
                                      // Unknown：第一份样本只初始化 cursor；
                                      // 样本不足属 warm-up（Satisfied）；Unknown
                                      // 只表示当前证据冲突/不足、无法合法求值
PendingPolicyOutcome? _pendingPolicyOutcome;  // Phase+Reason+Summary{PolicyId,
     // ApplicationsUsed, TerminationStatus}——活到下次咨询、消费即清
     //（F7(o)/F6(b)：与 _pendingFailure* 同构；Progress.PolicyState 的数据源）
```

不存/不权威化：world·effect·target truth、match/binding（每轮 fresh）。
**逐元素记忆（已访问/已尝试）不进 v1**——granularity §5-2 要求归驱动器执行
态，但其唯一买家（B2/A1）整体 DEFER（F5(b)）；随 buyer 到来加
`AttemptedElements`（需投影 identity 字段 = view 变更，协议 §5.2 通道）。

## 9. Validation（V6 v0.2）

```text
V6a closed vocabulary：未知 AST 节点 → reject（P11）
V6b Match/Termination 非空合取
V6c 0 < MaxApplications ≤ StepsRemaining（P10；不得扩大合同步数预算）
V6d Template.EffectClass ∈ AllowedEffects；TargetRole 非空（P9）
V6e DecisionId 回带（V1 三态同律）
V6f PolicyId 同 run 内唯一
```

## 10. Vocabulary Matrix（v0.2：消费者重定）

| Primitive | v1 消费者 | 场景 |
|---|---|---|
| `ClaimInSet` | **A2 match**（temp∈{24,23,22,21}——方向语义：19/25 出集即停）· C1 match（light∈{red,yellow}） | §4.1/§2 |
| `ClaimEquals` | A2/A4/C1 termination（temp=20 / 音量值 / light=green） | §2 |
| `ElementExists` | termination「目标出现」（P12 系：wifi entry found） | §2/B6 终止面 |
| `ObservationUnchanged(s,n)` | A2/C1 守卫（temp 不动/灯不变） | §4.1 |
| Template（AgentActionStep 同形） | A2/A4/C1（TargetRole=减号/音量键/电源键） | §4.1 |

**DEFER（买家未到，逐一登记）**：B6 翻页（需 ElementMissing+coverage+内容
稳定 scoping）· B2 逐项清除（需逐元素记忆+投影 identity）· A1 遍历（同 B2
+traversal bound）· B4 表单（多模板+属性过滤）。

## 11. Simulation（P1-P12 v0.2 修订）

ScriptedUniAgent **重做相位感知脚本序列**（`PolicyInvalidated` 为合法再咨询
相位；现 duplicate-call 纪律会误杀全部 P3-P7/P10/P11——F9(b)），纪律核算
随动。P 矩阵修正：P3 no-match = ClaimInSet 出集；P6 Guard/谓词 Unknown 触发
= **conflicted claim**（可产；Ambiguous 元素 v1 不产生——F8(b)）；其余不变
（载体仍优先真实资产）。全部 deterministic，禁 DSH。

## 12. Authority Matrix Delta（不变 + 强化）

同 v0.1 §11（创建=UniAgent；校验/求值=Kernel 机械封闭 AST；执行链 owner
不变；Goal 完成恒 UniAgent）。v0.2 强化：模板自带目标 → **「从 match 猜
target」的代码路径不存在**；`PolicyTruth` 三态 → conflicted/absent 数据
永不被动满足终止。

## 13. Open Questions（v0.3：前两项已裁决关闭）

1. ~~OQ-相位命名~~ → **已裁决（Owner 决策 1）**：`PolicyInvalidated(reason)`，
   不用 `PolicyGuardTripped`；NeedDecision 的 typed cause，非状态机。
2. ~~OQ-谓词求值投影~~ → **已裁决（Owner 决策 2）**：独立最小
   `PolicyEvaluationView`（§4.1 契约），不绑定 AgentDecisionContext。
3. OQ-semantic lease 深化：**绑定/校验/失效规则已冻结（§4）**；仍 DEFER =
   lease detection vocabulary · human-preemption detection · cross-process
   lease recovery（随 Phase 6/7 与 Recovery buyer）。

## 14. Files To Change Later（v0.2 补 ScriptedUniAgent 重做）

同 v0.1 §13 + `ScriptedUniAgent.cs`（相位感知重做，非扩展）+
`KernelRunDriver.cs` 增 `_pendingPolicyOutcome`/`_guardCursors` 捕获。

## 15. Architecture Delta（不变）

RUN-004 全保；RUN-005 只增：Policy 员（三谓词+单守卫+自带目标模板+单界）、
PolicyTruth 三态求值、ephemeral PolicyState（含 GuardCursor/pending outcome）、
PolicyExpand 良基循环（复用 Act 链）、V6、PolicyInvalidated 相位、单层局部
预算、E4 映射、P1-P12。

## 16. Revision Log

- **v0.3（2026-09-24，FROZEN）**：owner 终裁窄修——semantic lease 规则冻结
  （adoption 绑定 / 每轮校验 / 失效→PolicyInvalidated(LeaseInvalidated)→
  NeedDecision→零新 effect；content revision/scroll/可见元素变化 ≠ 失效；
  detection vocabulary/preemption/cross-process recovery 维持 DEFER）·
  Owner 决策 1 落形（PolicyInvalidated 统一承载，非 PolicyGuardTripped）·
  Owner 决策 2 落形（独立最小 PolicyEvaluationView，§4.1 契约）·
  GuardCursor 澄清（Warm-up ≠ PolicyTruth.Unknown）· OQ-1/2 关闭。其余
  v0.2 裁决全部维持（MaxRounds/PolicyFallback 删除、PolicyTruth 三态、
  ClaimInSet、TargetRole、良基递减、_pendingPolicyOutcome、E4 映射、
  预算门、B2/B6/A1/B4 DEFER）。
- v0.2（2026-09-24）：dual-grill 修订（详见上版日志；§16 v0.2 条目由本版
  收编：删 MaxRounds/PolicyFallback/PolicyScope/ClaimNotEquals/
  ElementMissing/TextContains；PolicyTruth 表；自带目标模板；良基不变量；
  GuardCursor；pending outcome；E4 映射；预算门；消费者 DEFER 重定；
  ScriptedUniAgent 重做登记；P3/P6 触发修正）。
- v0.1（2026-09-24）：初稿。

## 17. Verdict

**FROZEN**（owner 终裁 PASS_WITH_ONE_NARROW_AMENDMENT；Further Grill NOT
REQUIRED）。实现按 changes/RUN-005/plan.md Slice A→B→C 执行，以本稿为
权威；禁止再开完整 grill / 修改 AGT-001 / 接 DSH / 新增 executor / canonical
owner / 扩大 v1 vocabulary。
