# UniClaw 字段有效性：数学公理与哲学推导（2026-10-14）

> Status: COMPLETE / HISTORICAL-PREMISES
> Authority: NONE

本文保留初始字段公理及当时推导，关于P24/P25未闭合的实例前提由[同日最小buyer复核第六节](uniclaw-completeness-check-2026-10-14.md#六按最小购买者闭包重新验证2026-10-14)修正。不得将这些历史“当前推导”当作2026-10-22产品缺陷清单；不改变数学定义本身，也不外推为无条件活性或全生命周期完备证明。

## 论证对象

本文把协议边看成字段转换，而不是只看状态转换：

```text
F_producer → validate → F_canonical → project → F_consumer
```

一条边的完备性不是“record 能编译”，而是字段集合能否在整个生命周期内保留身份、事实、判断、失败和恢复所需要的信息。

定义字段有效性：

```text
Valid(F) = presence ∧ domain ∧ authority ∧ cross-field invariant
```

定义字段完备性：

```text
Complete(F) = Valid(F)
               ∧ absence semantics
               ∧ retry/late semantics
               ∧ recovery semantics
               ∧ downstream consumption
```

因此：

```text
存在 ≠ 有效
有效 ≠ 完备
完备 ≠ 已有真实运行证据
```

## 一、字段闭包的八条公理

### A1：字段存在公理

如果下游判断需要某个语义，上游必须提供该字段或明确的派生规则：

```text
Need(c) ⇒ Produce(c) ∨ Derive(c, declared rule)
```

例如 P1/P24/P25 需要 Task、Session、Contract、Run、Boundary 的关联；不能只传一个通用 `correlationId`。

**当前推导**：P1 的身份三件套已进入 `ExecutionContract`；P24 的 `acceptedContractViewRef + contractGeneration` 和 P25 的完整 boundary correlation 仍不完整。

**结论**：主字段集合部分成立，激活/咨询字段存在性未闭合。

### A2：值域封闭公理

字段值必须能被有限规则判定：

```text
value ∈ Domain(field)
```

`Origin`、Observation kind、Dispatch outcome、Terminal classification、BudgetValueOrigin 等需要封闭值域；自由字符串只能作为诊断信息，不能承担权威判断。

**当前推导**：P2/P3 的 Origin 由 Evidence Ledger 从 Context 机械推导，producer 自报只作诊断；P15 三态 outcome 和终局四分类已经具备明确值域。

**结论**：字段值域在核心证据和终局链上成立；Grant、用户中途命令和恢复状态仍有 Deferred 值域。

### A3：单一写入公理

每个 canonical 字段必须有唯一写入权威：

```text
WriteAuthority(canonicalField) = 1
```

Runtime 铸造 Task/Session，Run Model 接受 Contract View，Ledger 铸造 EvidenceId，World Model 铸造 Belief revision，Assurance 铸造 Judgment/Proof，Effect Boundary 铸造 Binding/Receipt。

**当前反例**：RUNTIME-IDENTITY-001 虽然新增 Store，但 `/runs` 仍用 launch idempotency 再次 mint，而非消费前序 binding；这使“字段有 Owner”与“字段链真正连续”分离。

**结论**：Owner 模型成立；实现上的跨边单写路径未闭合。

### A4：跨字段不变量公理

字段必须满足跨字段关系，而不是各自合法就算合法：

```text
TaskInstanceId → ProductSessionId → RunId
IntentId + BindingId + RevisionId → AssuranceJudgment
acceptedContractViewRef + contractGeneration → P24 activation key
AttemptId + PostActionSelf Origin → post-action Evidence
ExpectedEffect + Evidence → EffectVerification
```

**当前反例**：ProductSession 绑定 `run-pending-{launchId}`，而 RuntimeRunStore 生成另一真实 RunId；两个字段各自格式合法，但组合不满足 identity continuity。

**结论**：字段组合不变量是当前最关键的身份缺口。

### A5：缺失与不确定公理

字段缺失必须区分：

```text
absent ≠ unknown ≠ rejected ≠ not-applicable
```

例如：

- 缺 post-action Evidence → 不能伪造 Verified Effect；
- UnknownOutcome → 必须 re-observe；
- `GoalEvaluation = Undetermined` → `NeedsFollowUp`；
- 缺 `ProductSessionId` → P1 admission 拒绝，而不是生成隐式默认身份。

**当前推导**：Evidence、Assurance 和终局链基本遵守；`ExecutionContract` 预算仍允许 null 并在 View 端 default，虽然已有 `BudgetValueOrigin` 留痕，但字段缺失和默认值的产品语义仍依赖 policy。

**结论**：拒绝/未知语义基本成立；默认值和“未创建 binding”仍需更严格的字段状态。

### A6：当前性与时间公理

字段必须能区分历史和当前：

```text
current(x) ⇔ revision(x) = currentRevision
freshEnough(x, consumer) ⇔ consumer policy accepts temporal basis
```

`CaptureTime` 只能是 provenance；freshness 必须是消费侧判断。Binding、Judgment 和 Evidence 不能在 revision 变化后继续假装有效。

**当前推导**：P4/P10/P13 的 revision/freshness 语义分离成立；跨 Run late feedback、watchdog 和 canonical clock 仍缺字段和恢复边。

**结论**：进程内 currentness 语义成立，跨边时间闭包不完备。

### A7：重试与迟到公理

同一语义请求必须可判断复用、冲突或新尝试：

```text
retry(k, same payload) = reuse
retry(k, changed payload) = conflict
late(event, terminal) = observe-only or reject
```

这要求稳定 `idempotencyKey`、`generation`、`sequence`、`AttemptId` 或 `boundary correlation`。

**当前推导**：P1/P17/P18 的目标语义有幂等和 single-winner；TaskInstanceStore 没有对“同 key 不同 taskRef”做冲突判断，P24/P25 的生成号和 boundary correlation 仍未完整落地。

**结论**：幂等原则成立，字段实现不完备。

### A8：恢复充分性公理

重启后的持久字段必须足以确定唯一下一步：

```text
Persisted(F) + owner rules ⇒ same identity + same legal next transition
```

仅保存 `runId` 或日志文本不足以恢复；至少需要 lifecycle status、revision/generation、attempt/sequence、contract reference、binding reference、terminal/emission latch 和 recovery disposition。

**当前推导**：journal 的 torn/commit 语义支持 Attempt 层恢复；P24/P25、跨 Run late feedback、Host attach 和 Session→真实 Run 绑定还不足以满足该公理。

**结论**：局部恢复成立，生命周期恢复不完备。

## 二、从字段到数学性质

### Safety

若所有授权字段都有封闭值域、currentness 和唯一 Owner，则错误字段不能穿过 Gate。当前核心安全成立；缺失 Grant/A47/anti-spoof 字段时只能得出条件安全。

### Consistency

若 identity/correlation/generation/sequence 字段足够，则同一语义不会产生两个 canonical 结果。当前 P1/P17/P18 设计有此性质，但 RUNTIME identity binding 和 P25 boundary 字段未满足前提。

### Liveness

若 budget/hard cap/watchdog/recovery fields 完整，则非终态要么减少终止测度，要么等待并可唤醒，要么进入 BLOCKED。P1 的预算来源、WaitingForInput watchdog 和恢复引用不完整，因此 Liveness 只能条件成立。

### Reachability

若每个目标状态有完整输入字段和消费者，则目标状态可达。Completion/Failure/SafeStop/Escalation 字段足够；mid-run User command、Observation Request、admitted Contract View expiry 没有完整字段链，因此不可达或悬挂。

### Epistemic soundness

若 Evidence、Origin、ExpectedEffect、Revision 和 Proof references 都在同一条字段链上，则系统不把自己的期待伪装成现实。Origin 和 ExpectedEffect 已有结构，但 P22 prior 的独立佐证政策仍需落成字段级规则。

## 三、哲学推导改写为字段问题

### 认识论：字段是系统的“可知道边界”

系统不是因为拥有更多文本而知道更多，而是因为字段中存在可授权、可追溯、可验证的来源。`EvidenceId`、`Origin`、`Provenance`、`RevisionId` 和 `ExpectedEffect` 把“我看到了什么”“这来自哪里”“它是否仍然当前”“我预期改变什么”分开。

字段缺失时最诚实的结果不是补默认真值，而是 `Unknown`、`Insufficient` 或拒绝。

### 证明论：没有 basis 字段就没有 proof

Outcome Proof 必须引用 obligation status、basis/effect/situation EvidenceId 和 unresolved uncertainty。没有这些字段，Completion 只是一个结论字符串，不能构成证明。

因此：

```text
ProofWithoutBasis = Claim
ProofWithBasis = AuditableProof
```

### 身份哲学：连续性由关联字段证明

Task、Session、Run、Host Session、Attempt、Binding 不能因当前一对一而合并。连续性需要显式 binding 字段；从文件路径、transcript、DSH session 或 placeholder RunId 推导身份，是把名称误当作同一性。

### 责任哲学：字段写入权就是制度权力

谁能写 `EvidenceId`、`RevisionId`、`BindingId`、`OutcomeState`，谁就拥有相应制度权力。字段 Owner 不清，就会出现 Capability、Host、Workspace 或 Kernel 通过“方便的字段复制”取得隐含 authority。

### 时间哲学：没有 generation/sequence 就无法区分现在和过去

迟到事件不是普通数据，它必须带有 sequence、revision、attempt 或 generation。否则系统无法知道它应当更新当前状态、保留为历史，还是被 terminal closure 拒绝。

## 四、字段级总判定

| 字段性质 | 判定 |
|---|---|
| 核心安全字段 | 基本成立 |
| Evidence/Origin/Revision 字段 | 条件成立 |
| Task/Session/Run 关联字段 | **当前实现不完备** |
| P24 activation 字段 | 未闭合 |
| P25 consultation 字段 | 未闭合 |
| ExpectedEffect/Attempt 证明字段 | 结构存在，跨边消费需执法 |
| terminal proof 字段 | 基本成立，P19 交付字段缺口 |
| recovery/generation/sequence 字段 | 部分成立 |
| 整体字段完备性 | **部分完备，不是完全完备** |

最终结论：**UniClaw 当前最根本的问题不是缺少更多状态，而是关键语义在字段链上没有贯通。字段有效性要以值域、Owner、跨字段不变量和恢复消费共同证明；只要 Task/Session/Run、P24/P25、ExpectedEffect/Attempt、Origin/Revision 的链路仍有断点，生命周期就不能被判定为完备。**

参考：`docs/design/protocol-completeness-formal-audit-v0.1.md` 附录 A/B、`docs/design/protocol-workflow-steps.generated.js`、`docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md`、`docs/adr/0045-runtime-task-instance-session-and-agent-contract-sequence.md`、`docs/adr/0046-p1-p24-p25-contract-activation-consultation-boundary.md`。

## 五、最小购买者闭包的重新推导（2026-10-14）

设 `B` 是当前存在 consumer、场景或已接受 Change 的 buyer 集合，`F(B)` 是这些
buyer 实际需要的字段集合。完备性不应对整个想象中的协议全集求值，而应计算：

```text
Complete_min(B) = Complete(F(B))
```

对没有当前 buyer 的字段 `f`，只要满足：

```text
¬Buyer(f) ∧ NoCurrentConsumer(f) ∧ NoSafetyDependency(f)
∧ ExplicitDeferred(f) ∧ FutureReopenRule(f)
```

则 `f` 可以保持 deferred，不构成当前闭包反例。反之，若字段缺失会破坏当前
identity、authority、Effect safety 或 terminal closure，则不能用“未来 buyer”
掩盖：

```text
Buyer(f) ∨ SafetyDependency(f) ⇒ f ∈ F(B)
```

按此推导，P5、P20/P21、没有 consumer 的 GoalEvaluation delivery、Host lifecycle
command transport、P1 canonical authoring seam 和通用跨进程扩展可以暂时排除在
`F(B)` 外；Task/Session/Run、P24 activation、P25 consultation、P1 identity/admission
的字段校验仍属于 `F(B)`，因为它们直接参与当前运行链的身份和安全判定。

因此，新的数学结论是：

```text
全协议全集完备性：不要求当前成立
最小 buyer 闭包：仍不完备
最小闭包的首要反例：identity continuity、P24 key、P25 correlation/HardCap
```

这避免了两种对称错误：把没有购买者的预留协议当作当前缺陷，或把当前运行链的
字段断点伪装成 deferred。
