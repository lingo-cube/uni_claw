# RUN-004 spec — 多轮决策协议实施规格 v0.2

> 状态: REVIEWED-1 闭合（2026-09-22 对抗评审 1B+9maj+6min 全项处置，见 §9 处置表）
> 事实基线: HEAD 17fc7c16 · 白名单现额 200（已核）
> 推导层（引用不复制）：`consultation-protocol-v0.1.md` · `decision-granularity-scenarios-v0.1.md` · 裁决⑧（台账 #21）

## 0. 边界卡（固化——评审 S3）

**状态机边界**：驱动器拥有「问的时机（六触发点）/节奏（每点恰一次、轮次预算
执法）/咨询号铸造/上下文组装（只收集转述不判断）/提案逐字执行（中途失败
作废整支）/诚实终局」。不拥有：决策内容、世界认知、授权与验证判断、意图
签发、Agent 策略状态。
**UniAgent 边界**：策略留内部（可有状态，须确定性）；过界只有每次恰一个
AgentDecision（Act≤16 步 / NoAction+自证 / Defer 有界）；无回调、无流式、
无撤回。单轮/多轮是打包风格，协议无感。

## 1. 类型定义（S1 修正：公开面增量 +8 → 白名单 200→208）

### 1.1 新增公开类型 7 个 + 嵌套 1 个（全部经 D7 授权，无偷渡）

```csharp
public enum ElementEpistemic { Observed, Partial, Ambiguous }        // ← v0.1 漏计，补授权

public sealed record ElementSummary(
    string Role, string? Text, string? Bounds,
    bool? Clickable, bool? Checkable, bool? Enabled,                // 三态：null=无来源（F3）
    ElementEpistemic Epistemic);

public sealed record ClaimSummary(string Value, string Disposition, bool InConflict);

public sealed record ConsultationProgress(int RoundsUsed, int StepsDispatched, int StepsVerified);

public sealed record ConsultationBudget(int RoundsRemaining, int StepsRemaining);

public sealed record CompletionEvidence(string Basis, IReadOnlyList<string> Checklist);

public sealed record ObserveSpec(string? Subject, int MaxRounds);

// 嵌套公开类型（白名单按 FullName 计——先例 L139-156）：
public abstract record AgentDecision { ...
    public sealed record Defer(ObserveSpec Spec) : AgentDecision; }  // +1 条目
```

### 1.2 既有改动（成员级；枚举成员/record 字段**不进白名单计数**——S2 注记）

| 类型 | 改动 |
|---|---|
| `AgentDecisionPhase` | +4：StepVerified（=协议 T2 ProposalExhausted，词表映射登记——F13）/ StepRejected / VerificationFailed / DeferRoundsExhausted |
| `AgentDecisionContext` | +6：FailureReason / FailedStepIndex / **Screen（F2 补）** / Elements / Progress / BudgetRemaining；CurrentWorldClaims 值型 string→ClaimSummary |
| `AgentNoActionProposal` | +1：`CompletionEvidence? Completion` |
| `RunDriveStatus` | +1：AwaitingCompletionAdjudication（成员不进白名单） |
| `ExecutionContract` | +2：`int? MaxConsultations = null`、`int? MaxTotalSteps = null`（F5/F6） |
| `ExecutionContractView` | +2：`int MaxConsultations`、`int MaxTotalSteps`（**已解析非空**——通道修通，F5） |

**Screen（F2）**：`sealed record ScreenSummary(string ContainerId, string? Signature)`
——白名单 +1（累计 +9 → **209**）。派生：Current.Containers 唯一根 →
ContainerId；Signature = WorldState[ui.container.signature.{id}]。

## 2. 驱动器改动（零新状态；边/微循环/字段逐点映射——F10 补全）

```text
E1  StepAct: stepIndex ≥ steps.Count        现:→TerminalEvaluation   改:→NeedDecision (StepVerified)
E2  StepAct 失败点逐条映射（F10 表）：
    no-single-root-container (L255)         → StepRejected 回边（预算余）
    grounding:* (L299-300)                  → StepRejected 回边
    gate-rejected (L301-305)                → StepRejected 回边
    focus 耗尽 (L288-292)                   → 保持 return（PER-009 聚焦域语义）
    UnconfirmedDelivery (L307-310)          → 保持 return（恢复屏障：结果未知禁自动重试，不入决策环）
    ControlIntentKind.Recovery              → 保持现 fail-closed（恢复意图归恢复编排域，登记）
    预算尽                                    → return（reason 规范化，见 §2.1）
E3  StepVerify 失败                          → VerificationFailed 回边（同 E2 预算律）
E4  plain Observe（无 subject）              现:GroundingFailed 改:→TerminalEvaluation
M1  NeedDecision 收 Defer：就地 PullObservations 一轮→留在 NeedDecision 再咨询
    （MaxRounds 计数；耗尽→phase=DeferRoundsExhausted 终问一次，V5 豁免（F7），
     仍 Defer → return TerminalNotProven "defer-exhausted"）
F1' 字段：_consultCounter / _perBoundaryConsulted / _completedSteps（§3 归档）/
    _awaitingRuling / _lastAnswer（V5 状态）/ _stepsDispatched 计数
```

### 2.1 预算状态机（F6 补全）

```text
来源：ExecutionContract.MaxConsultations / MaxTotalSteps（null→16 / 256，
     在 RunModel.AdmitContract 解析进 View——F5 通道）
canonical：两字段参与 MintRunId 编码（预算=合同语义成分：同 version 异预算
     = 不同合同 → version-conflict fail-closed——F5 陷阱封闭）
扣减：每消费询 1 轮（Act/NoAction/Defer 应答的咨询同律）；每 dispatch 1 步；
     E2/E3 作废提案不退还剩余步（D5 作废语义）
耗尽谓词：consult 前 RoundsRemaining ≤ 0 → return 原状态 + reason
     "consult-budget-exhausted"；dispatch 前 StepsRemaining ≤ 0 →
     "dispatch-budget-exhausted"（reason 前缀规范=可观测性，状态复用不歧义）
上报：Progress/BudgetRemaining 即时快照进上下文
```

## 3. 完成证明三层（裁决⑧；F8/F9 闭环）

```text
层1 世界状态命题折抵：现有 obligation 路径，零改动
层2 锚定自证：
    锚点格式（修订）："step:{decisionN}.{stepIndex}" | "dispatch:{receiptId}"
                    | "obs:{evidenceId}"
    归档源（F8）：driver 新字段 _completedSteps:
      List<(int DecisionN, int StepIndex, string ReceiptId)> —— 每步
      StepVerify 成功时追加（非状态、字段级；与 D2「零新状态」不冲突——
      显式登记该张力与裁决：字段不是相位）
    核验：UniKernel.VerifyCompletionAnchors(evidence) → 逐锚查
      step: → _completedSteps 命中；dispatch: → EffectReceipts 命中；
      obs: → ledger.CanonicalRecords 命中。全过 = 附加折抵；任一 miss → 层3
层3 人为终极裁定（F9 闭环）：
    等待：_awaitingRuling=true → Drive() 返回 AwaitingCompletionAdjudication
    呈递（公开只读面，零新类型）：driver.PendingCompletionDossier →
      (CompletionEvidence Evidence, IReadOnlyList<(string Anchor, bool Verified)> Results)
    裁决：public void ResumeWithAdjudication(bool approved, string? note)
      approved → TerminalEvaluation（层2 等效折抵）
      rejected → return TerminalNotProven "completion-rejected"【终局，
        不再咨询——防 NoAction→驳回→NoAction 环】
    落档：HostRunner 把裁决 (approved, note, 锚点核验结果) append 进
      facts.json（UniKernel 无 facts store——落产品组合根，职责正确）
    持久化（诚实边界）：v1 裁决等待为进程内状态；跨进程裁决续跑 = 显式
      递延（登记），与恢复编排同域
```

## 4. 决策校验入口（F7 重构）

```csharp
// KernelRunDriver 实例方法（替换 static ValidateProposal 的调用点；
// Act case 内部沿用既有五检查 L426-437）
private string? ValidateDecision(
    AgentDecision decision, ExecutionContractView view,
    ConsultationBudget remaining, AgentDecision? lastAnswer)
// V3 budget-exceeded：提案步数 > StepsRemaining
// V4 hollow-completion：NoAction ∧ 存在 mandatory 义务 ∧ Completion=null
// V5 defer-unbounded：Defer ∧ (MaxRounds>4 ∨ lastAnswer 是 Defer)
//    ——豁免：phase==DeferRoundsExhausted 的终问（M1 T6 路径）
```

## 5. 上下文组装（F3/F4 补源）

| 字段 | 派生算法 |
|---|---|
| Screen | §1.2（唯一根容器 + signature claim） |
| Elements | **occurrence 投影 ×XML 增强**：基准 = Current.Occurrences（Role/Locator→Bounds/State→Epistemic=Observed）；对每 occurrence 在**同观察批**的 XML 证据（ledger，producer=platform.uiautomator）做 bounds IoU≥0.5 连接（P-3 同款）：命中→补 Text/Clickable/Checkable/Enabled，Epistemic=Observed；未命中→三态字段=null，Epistemic=Partial（视觉单源）。Ambiguous 保留给多候选连接（v1 不产生，登记） |
| Claims | WorldState → Value；Disposition = InConflict?"conflicted":(SupersededEvidenceIds≠null?"revised":"established")（F4 值域三分）；InConflict = subject∈Conflicts |
| Progress/Budget | 驱动器计数（§2.1） |

## 6. 验收（F12 补：11 项）

1–8 原 eight（state.md）不变；
9. T6 全链：低置信→Defer 至 MaxRounds 耗尽→DeferRoundsExhausted 终问→仍 Defer→`defer-exhausted` 终局
10. V3/V4/V5 各一 RED（超步预算提案 / 空洞 NoAction / 无界·嵌套 Defer + T6 豁免正例）
11. 层3 双分支：approved→Completion 与 rejected→`completion-rejected`（含 dossier 呈递断言）

## 7. 消费方涟漪（F1 重列，区分类型面/行为面）

类型面（编译红，机械改）：
- ScriptedUniAgentTests L12-17（位置参数构造 AgentDecisionContext——改命名参数）
- AsyncPerceptionScenarioTests L310/L1141-1143/L1332-1335（CurrentWorldClaims 按
  string 断言——改 ClaimSummary 断言）
行为面（语义改，须逐处核对）：
- KernelRunDriverTests L334 `decision-{runId}-1` 断言 → 计数化后按轮次取值
- ScriptedUniAgent.cs L43-49 Phase 纪律/单次调用假设 → 多轮化后 sim 剧本
  须按 DecisionId 序列出多应答（sim 侧升级，随本 change）
- KernelRunDriver L385-387 上下文构造点 → v2 组装

## 8. 事实核对 v0.2（F11 更正后全过）

- ValidateProposal 五检查 @ L426-437（含 effect-class-not-allowed L436-437）✓
- AgentObligationView 定义 @ L35-40（L28 为 CurrentWorldClaims——v0.1 误引）✓
- MaxProposalSteps=16 @ L97 ✓ · RunDriveStatus 12 成员 ✓ · 白名单 200 ✓
- 枚举成员/record 字段不进白名单（S2 注记）✓

## 9. 评审处置表（v0.1→v0.2）

| 发现 | 处置 | 落点 |
|---|---|---|
| S1 blocker | +8→+9（含 ScreenSummary）授权补全，200→209；嵌套计入 | §1 |
| F1 | 涟漪重列：类型面 2 文件 + 行为面 3 处；删「改动机械」 | §7 |
| F2/F3/F4 | Screen 补字段+派生；Elements=occurrence×XML 连接算法；Disposition 三分值域+算法 | §1.2/§5 |
| F5/F6/F7 | View 通道+null 解析+canonical 参与；预算状态机（扣减/耗尽谓词/reason 规范）；ValidateDecision 形状+T6 豁免 | §1.2/§2.1/§4 |
| F8/F9 | _completedSteps 归档（字段级，与零新状态的张力显式裁决）；dossier 元组公开面（零新类型）；rejected 终局防环；落档 facts；跨进程递延登记 | §3 |
| F10 | E2 五失败点+Recovery+UnconfirmedDelivery 逐条映射表 | §2 |
| F11/F13 | 行号更正；T2⇄StepVerified 词表映射 | §8/§1.2 |
| F12 | 验收 9–11（T6 全链/V3-V5/层3 双分支） | §6 |
| S2/S3 | 白名单计数注记；边界卡固化 §0 | §0/§8 |
