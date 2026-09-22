# RUN-004 spec — 多轮决策协议实施规格 v0.1

> 状态: DRAFT — 待评审 · 事实基线: HEAD 414bd903 · 白名单现额 200 项（已核）
> 推导层（本 spec 引用不复制）：`consultation-protocol-v0.1.md`（格式+协议，
> 逐字段 SR 出处）、`decision-granularity-scenarios-v0.1.md`（L2 经济学）、
> 裁决⑧（台账 #21）。以下全部条目已对照源码核verified。

## 1. 类型定义（新增 6 个公开类型 + 既有类型改动）

### 1.1 新增（Runtime 命名空间，白名单 200→206）

```csharp
public enum ElementEpistemic { Observed, Partial, Ambiguous }

public sealed record ElementSummary(
    string Role, string? Text, string? Bounds,
    bool Clickable, bool Checkable, bool Enabled,
    ElementEpistemic Epistemic);

public sealed record ClaimSummary(string Value, string Disposition, bool InConflict);

public sealed record ConsultationProgress(int RoundsUsed, int StepsDispatched, int StepsVerified);

public sealed record ConsultationBudget(int RoundsRemaining, int StepsRemaining);

public sealed record CompletionEvidence(string Basis, IReadOnlyList<string> Checklist);

public sealed record ObserveSpec(string? Subject, int MaxRounds);
```

### 1.2 既有改动（成员级，不加类型）

| 类型 | 改动 |
|---|---|
| `AgentDecisionPhase` | +4 成员：StepVerified / StepRejected / VerificationFailed / DeferRoundsExhausted（PolicyGuardTripped 留 RUN-005，纪律不预置） |
| `AgentDecisionContext` | +5 字段（FailureReason / FailedStepIndex / Elements / Progress / BudgetRemaining）；`CurrentWorldClaims` 值类型 string→ClaimSummary |
| `AgentDecision` | +1 case：`Defer(ObserveSpec Spec)` |
| `AgentNoActionProposal` | +1 字段：`CompletionEvidence? Completion` |
| `RunDriveStatus` | +1 成员：`AwaitingCompletionAdjudication` |
| `ExecutionContract` | +1 可选字段：`int? MaxConsultations = null`（默认 16） |

**库内消费方涟漪**（已核，全部忽略改动字段，改动机械）：Host V0Runtime.Consult、
Simulation ScriptedUniAgent.Consult、KernelRunDriverTests 脚本咨询——
CurrentWorldClaims 类型变更处共 3 文件。

## 2. 驱动器改动（零新状态，核验过的 4 边 + 1 微循环 + 3 字段）

```text
现状相位：NeedInitialObservation → NeedDecision → StepAct ⇄ StepVerify → TerminalEvaluation

改动（全部在 KernelRunDriver.cs）：
E1  StepAct: stepIndex ≥ steps.Count        现:→TerminalEvaluation   改:→NeedDecision (phase=StepVerified)
E2  StepAct: grounding/gate 拒绝             现:return GroundingFailed 改:预算余→NeedDecision (StepRejected+reason+stepIndex)
                                                                     预算尽→return 不变
E3  StepVerify: 验证失败                     现:return VerificationFailed 改:同 E2 (VerificationFailed)
E4  StepAct: plain Observe（无 subject）     现:return GroundingFailed 改:→TerminalEvaluation（终局如实判）
M1  NeedDecision 收 Defer：就地 PullObservations(External) 一轮 → 留在
    NeedDecision 再咨询（扣轮次；MaxRounds 计数；耗尽→phase=DeferRoundsExhausted 再问一次，
    仍 Defer → return TerminalNotProven "defer-exhausted"）
F1  字段：_consultCounter（DecisionId 序号）、_awaitingRuling（裁决⑧等待旗）、
    _lastCompletionEvidence（供人工裁决读取）
F2  _consulted 单旗 → 按边界的已咨询记录（幂等续跑）
```

## 3. 完成证明三层（裁决⑧ = c + 人为终极裁定）

```text
层1 世界状态命题折抵：现有 obligation 路径，零改动
层2 锚定自证：NoAction+Completion → Checklist 每项 = "step:{n}" | "dispatch:{receiptId}"
    | "obs:{evidenceId}" 格式锚点 → UniKernel.VerifyCompletionAnchors 机械核验
    （锚点引用存在于 RunState/journal/ledger）→ 全锚住 = obligation 附加折抵；
    任一锚不住 → 层3
层3 人为终极裁定：_awaitingRuling=true → Drive() 返回
    AwaitingCompletionAdjudication（合法等待）→ Host 呈递 docket（证据+锚点核验
    结果）→ 人工输入 ruling（approved/rejected + 备注）→ 经
    ResumeWithAdjudication(ruling) 恢复 → 按 ruling 走 TerminalEvaluation
    （approved = 层2 等效折抵；rejected = TerminalNotProven "completion-rejected"）
    裁决记录 append 进 facts（不 emission 二次）
```

## 4. 机械校验（ValidateProposal 扩展，全部 fail-closed）

```text
既有：empty-steps / too-many-steps / missing-target-role / missing-effect-class
      / effect-class-not-allowed                                    （已核 L424-433）
新增 V3 budget-exceeded：提案步数 > StepsRemaining 或本轮后轮次将尽仍发起
新增 V4 hollow-completion：NoAction 且 Objective 含 mandatory 义务而
      Completion 为 null（SR-074）
新增 V5 defer-unbounded：MaxRounds > 4 或嵌套 Defer（上轮已是 Defer 应答）
```

## 5. 上下文组装（驱动器职责，owner 派生面）

| 字段 | 派生自 | 备注 |
|---|---|---|
| Elements | WorldModel 当前 revision occurrences（owner 派生投影）| Epistemic 从 occurrence 状态映射 |
| CurrentWorldClaims | WorldModel WorldState + Conflicts | InConflict = 该 subject 有 Conflict 条目 |
| Progress / Budget | 驱动器自身计数 | SR-100 no-progress 判定输入 |
| FailureReason / FailedStepIndex | E2/E3 捕获点原文 | M2 不净化 |

## 6. 验收映射（state.md Acceptance 1–8 → 承载）

| # | 场景 | 测试落点 |
|---|---|---|
| 1-3 | 弹窗重议/遍历/重规划 | KernelRunDriverTests 新 RED 三例（脚本 feed + 脚本多轮咨询） |
| 4 | Defer 轮 | 同上（低置信帧脚本） |
| 5 | 零动作终局 | 现有 DesiredState 语义 + E4 断言 |
| 6 | 预算耗尽 | E2/E3 预算尽分支断言 |
| 7 | 三层完成证明 | 层2 锚点核验正反例 + 层3 等待/恢复/裁决记录 |
| 8 | 确定性/续跑 | 两跑决策序列一致 + WaitingForInput 中断后不重问 |

## 7. 事实核对记录（防事件 #3/#6 重演，提交前完成）

- [x] MaxProposalSteps=16 存在（KernelRunDriver L97）
- [x] ValidateProposal 五检查存在（L427-433）
- [x] AgentObligationView 字段不变（AgentDecision.cs L28/35）
- [x] 白名单现额 200（含 PER-009 +3）
- [x] RunDriveStatus 12 成员（含 PER-009 未加新——awaiting 为本 change 首 add）
- [x] ExecutionContract record 无 MaxConsultations（本 change 加）
