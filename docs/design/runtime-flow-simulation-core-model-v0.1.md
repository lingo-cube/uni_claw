# Runtime Flow & Simulation Core Model v0.1

> DocumentType: `RUNTIME_FLOW_SIMULATION_CORE_MODEL_V0_1`
>
> Status: `DRAFT / REVIEW_REQUIRED`
>
> Authority: `NONE`
>
> Version: `v0.1`
>
> Date: `2026-09-12`
>
> Governing Change: `RFS-001`
>
> Review Gate: `Human review required before architecture adoption or implementation`

---

## 1. 这份文档只解决什么

本文件是供 Human 单独审阅的**核心模型与核心接口归档**。它只回答五个问题：

1. Fast 先返回、Slow 后敲定时，Runtime 如何持续工作而不污染世界模型；
2. Runtime 做错、无法决定或超出策略容忍范围时，如何恢复或请求 UniAgent 裁决；
3. 如何从 WorldBelief 的 Slice 与资产还原某一版完整页面，并验证表达是否正确；
4. 如何用捕获资产重放任意流程，并把代表性场景提升为测试基线；
5. 如何用 Trace 串起因果关系，同时不让 Trace 成为第二份 truth。

它不决定模型、算法、阈值、prompt、存储、transport、部署、UI 或训练方式；也不替代
现有冻结架构。本文所有新增名称均为候选。

## 2. 一句话模型

> Perception 可以渐进地产生观察；只有 Evidence Ledger 与 World Model 能把观察变成
> 当前世界判断；Runtime 基于当前判断行动并重新观察验证；无法安全继续时请求
> UniAgent 裁决；同一套 Runtime 可由真实外部能力或确定性回放资产驱动。

## 3. 完整闭环

```text
Primary Goal
  → Execution Contract
  → legal Run activation
  → Kernel self-driven loop
       → Observe
       → PerceptionUpdate (Preliminary / Final / Failed)
       → Evidence admission
       → WorldBelief reconciliation
       → Slice for the current consumer
       → Intent → Grounding → Assurance → Effect
       → Post-action Observe
       → Verify / Reconcile
       → Continue
          | Local Recovery
          | Awaiting Supervision → UniAgent Resolution → re-enter loop
  → Outcome Proof
  → terminal Runtime Outcome
  → UniAgent Goal Evaluation
```

关键约束：

- `Preliminary` 可以快速支撑低风险动作，但它不是较低等级的 WorldBelief；它仍须经过
  Evidence admission 与 reconciliation。
- `Final` 只表示这次观察会话的 producer 已完成，不表示“这一定是真的”。
- Slow 发现 Fast 有误时，不直接修改 Container Graph；它提交带 lineage 的新 proposal，
  由 World Model 生成新 revision。
- Effect 后必须重新观察。不能因为命令成功送达就假定页面已按预期改变。
- 所有重试、补偿和 UniAgent 指令最终都要重新进入
  `Evidence → WorldBelief → Assurance → Effect → Verification`，不能旁路闭环。

## 4. 核心模型

### 4.1 ObservationSession

一次有边界的观察过程。它把同一来源快照上先后到达的 Fast/Slow 结果关联起来。

最小语义：

- `ObservationSessionId`
- `RunId`、触发原因与 Observation Scope
- 原始来源/资产引用及其捕获时刻
- deadline、budget 与所需观察质量
- 生命周期：`Open → Completed | Partial | Failed | Cancelled`

它不拥有 Evidence、WorldBelief 或 Run State。

### 4.2 PerceptionUpdate

`IPerception` 对一次 ObservationSession 的异步输出 envelope。

最小语义：

- `ObservationSessionId`
- `Stage = Preliminary | Final | Failed`
- `ObservationProposal[]`
- `SourceArtifactRef[]` 与 producer/strategy/model version
- proposal 间的 `supersedes` / `refines` lineage（若存在）
- coverage、uncertainty、completion reason

Fast→Slow 可以发 `Preliminary` 后再发 `Final`；Slow-only 或单次完整模型可以只发一次
`Final`。因此 Runtime 不需要知道使用了哪种模型组合。

### 4.3 WorldPresentation

对**一个确定的 WorldBelief revision**进行可视化还原的非权威 artifact。它由该 revision
派生的一个或多个 Slice、原始/派生资产与明确的空间变换组成。

最小语义：

- `SourceWorldBeliefRevisionId`，且只能有一个
- `SliceRef[]`
- `AssetRef[]`
- `SpatialFrame` 与显式 transform
- `CoverageManifest`
- `UnknownRegion[]`、`Conflict[]`、`UnrenderableClaim[]`
- 每个可视元素回指 occurrence/evidence/asset 的 lineage

“完整页面”不是“图片铺满了”，而是 `CoverageManifest` 声明的目标范围全部被解释；
无法解释的区域必须显式显示为 unknown。

### 4.4 ContainerInteractionEpisode

围绕一个 `ContainerIdentity` 的非权威执行视图，用于解释 Fast 与 Slow 之间发生了什么。
它关联：

- 进入容器时的 revision 与 Slice；
- 已选择的 intents、bindings、effect receipts；
- 每次动作的 expected change；
- 后续 PerceptionUpdate 与实际 revision；
- 重复命中、遗漏候选、歧义和 first divergence。

它解决“同一容器内做过哪些事、预期和实际差在哪”的查询，但**不得写进 Slice 本体，
不得成为 Container Graph 的执行历史，也不得作为 Effect authority**。

### 4.5 SupervisionCase / SupervisionResolution

Runtime 在尚未终止、又无法安全自行决定时提交的结构化裁决请求。

`SupervisionCase` 至少包含：

- 原因：无法形成安全 intent、策略预算耗尽、验证不一致、未知副作用等；
- Goal/Contract/Run、current revision、Slice 与 ContainerInteractionEpisode 引用；
- 已尝试动作、receipts、observed differences 与 retry budget；
- Runtime 允许接受的 resolution capabilities。

`SupervisionResolution` 只表达受约束的决定，例如：

- `ResumeWithConstraint`
- `ReobserveWithScope`
- `ReorientToKnownCheckpoint`
- `RequestContractRevision`
- `TerminateSafely`

UniAgent 不直接下发设备动作。Runtime 必须校验 resolution 是否被 Contract 与策略允许，
再从观察或控制入口恢复。

`SupervisionCase` 是**非终态暂停**；现有 `Escalation Outcome Proof` 是**终态结果**。
两者不能共用一个状态或名字。

### 4.6 ReplayBundle

一次可重放运行所需的不可变外部输入集合。它记录 external seams，不记录一套替代 Runtime。

应包含：

- Goal/Contract 与激活输入；
- 原始图像、结构化页面资产及 capture metadata；
- 录制的 PerceptionUpdate；
- EffectDriver 外部结果与环境反馈；
- SupervisionResolution；
- schema、策略、producer/model 与 asset 版本；
- owner record references、内容 hash 和完整 lineage；
- 预期 semantic digest（用于比较，不作为 Runtime 输入）。

### 4.7 RuntimeScenario

一个人为编排的测试故事：初始条件、外部事件序列、虚拟时间、允许的 fault injection 与
预期不变量。它描述“环境会发生什么”，不描述 Runtime 每一 cycle 应调用哪个内部方法。

### 4.8 ScenarioBaseline

从代表性 RuntimeScenario + ReplayBundle 中显式晋升出的版本化测试界线。它由两层组成：

1. 必须满足的语义断言：Outcome、WorldBelief claims、unknown/conflict、effect 次数、
   policy budget、supervision 边界、first-divergence 规则；
2. 可选的视觉 golden：WorldPresentation 的布局/覆盖/像素容差，用来辅助发现展示偏差。

截图不能单独成为 baseline，因为“截图看起来一样”不能证明 identity、lineage、uncertainty
与 action authority 正确。

### 4.9 SimulationReport

一次模拟的确定性结果：实际 semantic digest、断言结果、WorldPresentation diff、
first divergence、未消费资产、Trace 引用和最终 Outcome。它是验证产物，不是产品 truth。

## 5. 核心接口

以下是语义形状，不提前冻结语言、namespace、序列化或 transport。

### 5.1 IPerception

```text
Observe(ObservationRequest) → async stream<PerceptionUpdate>
```

- **Buyer**：Runtime observation port。
- **Owner**：Perception。
- **职责**：隐藏 Fast/Slow/单次完整模型的选择与组合，渐进返回 proposal。
- **失败**：以明确 `Failed`/`Partial` 结束；静默无输出不等于 OK_EMPTY。
- **禁止**：不得 admission Evidence、修改 WorldBelief、选择 intent 或执行 action。

Perception 内部可以有 `IPerceptionCoordinator` 决定 Fast-only、Fast→targeted Slow、
Slow-only 或单次完整调用，但该策略不暴露给 Runtime。

### 5.2 IWorldPresentationComposer

```text
Compose(WorldPresentationRequest) → WorldPresentation
```

- **Buyer**：Simulation、诊断工具、Human review。
- **Owner**：非权威 presentation component。
- **输入**：一个 revision 的 Slice 集合、资产、目标 viewport/coverage。
- **失败**：revision 不一致、SpatialFrame 不可转换、lineage 缺失或 coverage 不完整时，
  返回结构化 gap；不得伪造完整页面。
- **禁止**：不得回写 WorldModel、合并跨 revision truth 或作为 grounding 输入。

### 5.3 IRuntimeSupervisor

```text
Resolve(SupervisionCase) → async SupervisionResolution
```

- **Buyer**：处于 `AwaitingSupervision` 的 Runtime。
- **Owner**：UniAgent realization adapter；裁决语义仍服从 UniAgent/Contract authority。
- **职责**：针对 Runtime 已结构化的问题选择允许的恢复方向或安全终止。
- **失败**：超时、拒绝、无效 resolution 都保持 Runtime 不可行动，直到策略决定终止。
- **禁止**：不得直接 dispatch effect、改写 belief、伪造 receipt 或绕过 Contract。

### 5.4 IRuntimeSimulation

```text
Run(RuntimeScenario, RuntimeFixtureSet) → async SimulationReport
```

- **Buyer**：Development Harness / test suite。
- **Owner**：Simulation runner，仅拥有模拟生命周期。
- **职责**：装配 replay adapters、virtual scheduler 与真实 Runtime，注入 external events，
  收集断言和 first divergence。
- **失败**：缺资产、事件顺序不合法、未消费事件、非确定性输出均显式失败。
- **禁止**：不得自己实现 Control/WorldModel/Assurance，也不得逐 cycle 调用 Kernel 内部步骤。

### 5.5 IReplaySource

```text
Open(ReplayBundleRef) → ReplayBundle
```

- **Buyer**：Replay adapters 与 Simulation runner。
- **Owner**：Development Harness asset boundary。
- **职责**：按 hash/version 提供不可变资产与录制的 external-seam events。
- **失败**：版本、hash、schema 或 lineage 不匹配时 fail closed。
- **禁止**：不得把预期 canonical output 注入 Runtime 充当实际输出。

### 5.6 IRunTrace

沿用现有 non-authoritative trace boundary，并扩展可引用的 operation/reference：

- ObservationSession / PerceptionUpdate
- WorldBelief revision / Slice / WorldPresentation
- intent / binding / assurance / effect / verification
- local recovery / SupervisionCase / Resolution
- terminal proof / outcome

Trace 只能引用 owner record；Runtime 的任何判断不能依赖 Trace 是否开启。

## 6. 页面组装与正确性验证

页面还原分四步：

```text
Select one WorldBelief revision
  → derive/select compatible Slices
  → resolve AssetRefs and SpatialFrames
  → compose WorldPresentation with explicit coverage and unknowns
```

随后才进行验证：

```text
WorldPresentation + ScenarioBaseline
  → semantic assertions
  → optional visual comparison
  → discrepancy report
```

必须保持：

1. 一次 WorldPresentation 只对应一个 WorldBelief revision。
2. Slice 未包含某元素表示“不在该投影视野”，不能自动解释成“不存在”。
3. 跨 revision 只允许生成标明时间轴的 diagnostic montage，不能叫 expected page。
4. 空间值必须携带 SpatialFrame；只有存在显式 transform 才能组装。
5. 每个呈现元素都能回指 occurrence/evidence/asset。
6. Presentation diff 只报告差异，不直接修改 WorldBelief 或判定 Effect 可执行。

## 7. 三种回放必须分开

| 模式 | 输入 | 验证对象 | 是否确定性 |
|---|---|---|---|
| World Presentation Replay | 同 revision Slice + assets | 世界模型表达能否还原目标页面 | 应确定性 |
| Runtime Event Replay | 录制的 perception/effect/supervision external events | 完整 Runtime 状态与恢复流程 | 应确定性 |
| Model Evaluation Replay | 原始图片/语料 + 真实模型 | Fast/Slow 模型质量与鲁棒性 | 不保证，应独立度量 |

前两种用于先把流程本身跑通；第三种才把模型的不稳定性重新引入。模型评测失败不能被
误报成 Runtime 状态机失败，反之亦然。

## 8. Live 与 Replay 的共同装配

```text
Live:
  Real Perception + Real EffectDriver + Real UniAgent
                         ↓
                    same Runtime

Replay:
  ReplayPerception + ReplayEffectDriver + ScriptedSupervisor + VirtualScheduler
                         ↓
                    same Runtime
```

Replay adapter 只能替换外部不稳定能力。Evidence Ledger、World Model、Control、Grounding、
Assurance、Run Model 与 closed-loop driver 必须使用产品实现，否则模拟只是在测试替身自己。

## 9. 异常与恢复分类

| 情况 | 默认去向 |
|---|---|
| Fast 后 Slow 纠正元素/关系 | 新 proposal → admission → 新 revision → 当前 intent 重新校验 |
| 重复点中同一元素 | post-action observation → episode 对比 → local recovery 或 supervision |
| 漏掉元素 | coverage/expected-change gap → targeted reobserve；仍不足则 supervision |
| Effect delivery 未知 | 不盲目重发；先重观察并核对 receipt/effect evidence |
| 达到 Contract/策略重试上限 | `AwaitingSupervision`，附完整尝试和差异证据 |
| UniAgent 无法给出合法恢复 | Assurance 形成 Safe-Stop/Escalation proof，进入终态 |

Runtime 能否“回到某页继续”不能由 LLM 自由发挥；必须存在可引用 checkpoint、允许的
恢复能力、当前 evidence，以及重新 grounding/assurance 的路径。

## 10. Trace 与 first divergence

Trace 的目标是回答：“同一个预期流程，第一次从哪里开始不一样？”

比较时应规范化忽略 TraceId、SpanId、wall-clock timing、provider request id 等噪声，
优先比较：

1. external input / asset hash；
2. PerceptionUpdate 的 canonical proposal semantics；
3. Evidence admission/rejection；
4. WorldBelief semantic digest；
5. selected intent 与 binding basis；
6. assurance judgment；
7. effect/receipt 与 post-action verification；
8. supervision 与 terminal outcome。

Trace on/off 必须保持 canonical output 等价。ReplayBundle 是重放输入，Trace 是因果索引；
两者可以互相引用，但不能互相冒充。

## 11. Scenario 晋升为测试基线的门槛

一个真实事故或人工场景只有满足以下条件才能成为 `ScenarioBaseline`：

1. 所有 AssetRef 可解析且 hash 固定；
2. schema、策略和 producer/model 版本明确；
3. 确定性 Runtime Event Replay 连续两次 semantic digest 相同；
4. expected assertions 经 Human review，不从当前实现结果自动反推；
5. coverage、unknown、conflict 与允许容差明确；
6. first-divergence 与 Trace reference 完整；
7. 不依赖设备、网络、真实模型或 wall clock 才能运行。

建议首批基线覆盖：

- Fast 正确，Slow 只补充细节；
- Fast 认错，Slow 在 effect 前纠正；
- Fast 认错，effect 已发生，post-action verification 触发补偿；
- 重复元素导致重复点击风险；
- 漏元素后 targeted Slow 找回；
- Effect unknown，重观察后确认成功/失败两支；
- retry budget 耗尽，UniAgent 决定恢复；
- UniAgent 无合法恢复，安全终止；
- 跨 revision Slice 被拒绝组装为完整页面；
- Trace 开关不改变 canonical 结果。

## 12. 必须由 Human 裁决的五件事

### H1 — 页面还原的时间边界

**建议**：WorldPresentation 严格绑定一个 WorldBelief revision；跨 revision 只作为明确标注的
诊断时间拼图。

### H2 — 测试基线的权威内容

**建议**：语义断言是主基线，视觉 golden 是辅助基线，截图永不单独定义正确性。

### H3 — 重放捕获边界

**建议**：ReplayBundle 捕获 external seam 输入；Runtime canonical outputs 只作为 expected
digest/checkpoint，不作为回放输入。

### H4 — 非终态监督

**建议**：引入 `AwaitingSupervision + SupervisionCase/Resolution`；与终态 Escalation 完全分离。

### H5 — 容器执行记录

**建议**：采用非权威 `ContainerInteractionEpisode`，不要把动作历史塞入 Slice 或
Container Graph。

在 H1–H5 审阅完成前，本文件保持 `Authority: NONE`，不得作为实现或验收的规范依据。
