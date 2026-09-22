> Status: DRAFT / CANDIDATE
> Authority: NONE
> 日期：2026-09-22 · 方法：150 场景语料（scenario-reasoning-v0.1）反向推导
> · 输入谱系：C2（感知异常）/ C3（绑定歧义）/ C9（决策形状）/ C13（合取语义）
> 为主压力源；决策粒度分层（decision-granularity-scenarios-v0.1）为粒度输入
> 用途：RUN-004（多轮协议）与 RUN-005（策略提案）的契约规格基座

# 咨询协议与数据格式推导 v0.1 —— 从 150 场景反推 P25 契约 v2

## 0. 推导规则

每个字段/每条协议纪律必须指回场景压力（SR-xxx）或等价类论证（Cn）；
无出处者不进格式。**语料给出的三条元原则**先行：

- **M1（C2/C3 哲学审计）**：跨界信息用**认识论词汇**（四值/三态/冲突标记），
  **数值置信度永不入协议**——SR-023「producer 自报 confidence 虚高」已证伪
  数值通道的可信性；
- **M2（C13 合取语义）**：失败上下文**原样转述不净化**——任一轴 fail-closed
  则整体 fail-closed，净化会把合取退化成合取的子集；
- **M3（C9 边界 / T6）**：预算内 liveness 成立、预算外诚实失败——协议的一切
  循环体（Act 执行、Defer 等待、Policy 展开）都必须有显式预算与耗尽语义。

## 1. 接口协议（咨询触发与纪律）

### 1.1 触发点（六种，状态机边界卡的四条 + 语料补两条）

| # | 触发 | 携带 | 出处 |
|---|---|---|---|
| T1 | InitialPlanning | 初始世界摘要 | C1 |
| T2 | ProposalExhausted（提案全部步验证完） | 进度+新世界摘要 | SR-099/103 |
| T3 | StepRejected(stepIndex, reason) | 失败步+原因原文 | SR-025/026/106 |
| T4 | VerificationFailed(stepIndex, reason) | 同上 | SR-034/064/106 |
| T5 | PolicyGuardTripped（L2 守卫触发，RUN-005） | 守卫名+现场 | 粒度文档 A2 推演 |
| T6 | DeferRoundsExhausted（等待预算尽） | 等了什么/多久 | SR-067/068 |

### 1.2 纪律（每条有场景出处）

```text
D1 每触发点恰一次；断点续跑不重问（幂等）          ← SR-107/108
D2 DecisionId = decision-{runId}-{n}，n 严格递增；
   回答必须回带同号（防串话）                       ← P25 既有 + SR-047
D3 轮次预算合同声明（默认 16，遍历类 64）；
   耗尽 = 诚实失败，不续借                          ← SR-102/049，T6 边界
D4 提案内步间不咨询（串行屏障属执行，不属决策）      ← SR-104
D5 失败即作废整支提案（不可中途续接残段）           ← SR-035（复合态解体）
D6 同步请求-回答（现缝形态 Func<Ctx,Decision?>）；
   null 回答 = no-response fail-closed             ← P25 既有
D7 协议记录可序列化（未来 LLM realization 序列化
   同一组 record，不另立协议）                      ← 词表无委托/不透明物
```

## 2. 数据格式（P25 契约 v2）

### 2.1 AgentDecisionContext（问什么——给 Agent 的有界输入）

```yaml
# ── 关联与目标（沿用）──
DecisionId / RunId / ContractVersion / Objective / AllowedEffects
# ── 触发面（新）──
Phase:            T1..T6 之一
Reason:           string?        # 失败原因原文，不净化（M2）
FailedStepIndex:  int?           # 提案内第几步（SR-103/106）
# ── 世界面（认识论摘要，M1：词汇非分数）──
Screen:   { ContainerId, Signature }
Elements: [ { Role, Text?, Bounds,
              Clickable, Checkable, Enabled,     # D12 能力/状态字段（SR-032 开关禁点）
              Epistemic: Observed | Partial | Ambiguous } ]   # SR-011/013/025
Claims:   subject -> { Value, Disposition, InConflict: bool } # SR-022 双源矛盾可见
PendingObligations                                # 沿用
# ── 进程面（新）──
Progress:        { RoundsUsed, StepsDispatched, StepsVerified, PolicyState? }
BudgetRemaining: { Rounds, Steps }                # SR-100（no-progress 判定输入）/ SR-102
```

**格式裁决（M1 落地）**：Elements/Claims 只携带判别词汇与冲突标记；
`confidence` 字段**不存在**于协议任何位置。

### 2.2 AgentDecision（答什么——封闭 union v2）

```yaml
AgentDecision =
  | Act(Proposal)                # L0/L1：Steps≤16（沿用）+ Justification
  | Policy(PolicyProposal)       # L2（RUN-005）：谓词/模板/终止/守卫/预算
  | NoAction(CompletionEvidence) # 收工——【裁决⑧落形】必须携带完成自证
  | Defer(ObserveSpec)           # 再观察/等待——必须带界

CompletionEvidence: { Basis, Checklist }     # Agent 自证完成载荷（uni-agent
  GoalEvidence 模式）；防空洞收工——SR-074（Outcome18 判例）+ SR-150
ObserveSpec: { Subject?, MaxRounds ≤4 }      # SR-067/068：Defer 有界，
  不与 DeferRoundsExhausted 成环
```

### 2.3 机械入口校验（P25 先例扩展，全部 fail-closed）

```text
V1 回带 DecisionId ≠ 上下文 → correlation-mismatch           （沿用）
V2 Steps 空 / >16 / EffectClass ∉ AllowedEffects             （沿用）
V3 轮次/步数超剩余预算 → budget-exceeded                      （新，SR-049）
V4 NoAction 无 CompletionEvidence → hollow-completion         （新，SR-074）
V5 Defer 的 MaxRounds > 界限 / 嵌套 Defer                     （新，SR-068）
V6 Policy 形态（RUN-005）：谓词/终止/守卫/预算四缺一          （新）
```

## 3. 语料对照表（格式条目 ← 场景出处，抽样）

| 格式/协议条目 | 出处 | 压力说明 |
|---|---|---|
| Elements.Checkable 入上下文 | SR-032 + 用户遍历场景 | 开关禁点规则的数据前提 |
| Claims.InConflict | SR-022/140 | 双源矛盾对 Agent 可见，终局不静默 |
| Reason 不净化 | C13 合取论证 | 净化=丢失「哪一轴 fail-closed」 |
| FailedStepIndex | SR-103/106 | 重议从断点语义开始，不需全量重看 |
| Progress 字段组 | SR-100 | no-progress 判定是 Agent 职责，数据须给足 |
| BudgetRemaining | SR-102/049 | Agent 打包前知道自己还剩多少（L2 预算的前提） |
| CompletionEvidence | SR-073/074/150 | 「宣称≠完成」三判例 + 开放目标的证明路径 |
| Defer 有界 | SR-067/068 | 等待不许无限，诚实预算同 M3 |
| 幂等触发 | SR-107/108 | 跨进程续跑咨询序列不变（回放 digest 前提） |
| 数值禁令 | SR-023 + C2/C3 哲学 | confidence 虚高已证伪；四值词汇替代 |

## 4. 本推导明确不进 v1 的（登记）

- **世界突变主动检测**（SR-046 human preemption）：v1 无专门触发点——突变
  自然表现为 T3/T4（接地失效/验证失败）；主动检测 = preemption Phase 6/7 买家；
- **跨 run 迟到反馈入上下文**（SR-058）：恢复编排域，协议不沾；
- **Policy 形态的字段冻结**：RUN-005 spec 评审时对照粒度文档 18 场景逐条定；
- **Transport/序列化具体格式**（JSON schema 等）：LLM realization 立项时
  从本格式机械投影，不提前锁。

## 5. 证伪条款

本格式/协议在以下观测下认输并触发窄修：
1. 六触发点之外出现「必须问 Agent 但无处安放」的真实场景 → 触发面不完备；
2. Agent 需要的字段频繁超出有界摘要（要求全量倾倒才能决策）→ 摘要投影
   不够，需扩 owner 派生面（走 view 变更，不开倾倒）；
3. CompletionEvidence 被证可伪造空转（Basis 无从核验）→ 裁决⑧方案①失败，
   退方案②（可观察状态命题）。
