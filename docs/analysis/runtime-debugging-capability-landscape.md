# Runtime Debugging Capability Landscape

> DocumentType: `CAPABILITY_LANDSCAPE_ANALYSIS`
> Status: `ANALYSIS_ACCEPTED / ROADMAP_DRAFT_NOT_AUTHORIZED`
> Date: 2026-08-30
> OriginGate: `PROJECT_LEADER_RUNTIME_DEBUGGING_CAPABILITY_ROADMAP_GATE`
> AcceptedBy: `PROJECT_LEADER_RUNTIME_DEBUGGING_CAPABILITY_P0_CONTRACT_GATE`
> Authority: `NONE`
> Scope: Runtime debugging evidence reading, correlation, differential analysis, replay/minimization, and debug-packet generation
> ChangeMode: `ANALYSIS_AND_DOCUMENTATION_ONLY`
> AuthorityDelta: `NONE`
> ArchitectureDelta: `NONE`
> RuntimeBehaviorDelta: `NONE`

本文只盘点当前能力、真实 buyer evidence、重复人工动作与候选能力方向。它不是 Runtime Contract、Decision、Spec、OpenSpec、Tooling 实现授权或正式 Roadmap；不改变 Runtime、Trace、Span、Event、Harness、Replay、GoalEvidence、FSM、Traversal、Semantic、Vision、部署身份或生命周期。

---

## 0. Executive Conclusion

UniClaw 已经拥有相当多的 Runtime 调试基础，不需要从零建设一个 generic debugger，也没有证据支持现在实施一套新的 generic Trace architecture。

当前真实能力已经覆盖：

- 证据驱动调试方法与 E0-E4 风险分级；
- Runtime debugging casebook；
- Agent `TraceEvent`、BCL `Activity` Span、Harness `TraceRun` 与只读 Trace/Span 查询；
- Harness capture、content-addressed artifact、显式 persisted-capture reader；
- Phase 2.6 validation-side frame/stage dump；
- Fusion operator causal trace；
- production Vision deployment identity / validation-scoped shadow receipt；
- deterministic ReplayEnvironment、operator offline replay 与真实失败到 falsifier 的人工闭环；
- Tier B / Settings campaign / fixture freeze 等 validation harness 入口。

真正重复消耗 Leader 时间的是这些资产之间没有统一的调试中间表示和机械关联层：

```text
现有状态
Run JSON + Frames + Stage Views + Fusion Trace + Runtime Trace + Receipt + Tests
  → 人工找 seq
  → 人工找 StableKey / OccurrenceId
  → 人工做 Good/Bad pair
  → 人工重建 LAST_GOOD / FIRST_BAD
  → 人工判断 GapKind / Owner
  → 人工抄成 replay falsifier
  → 人工整理 Gate packet

候选目标
Runtime Evidence
  → Debug IR
  → First Divergence Point
  → Owner
  → Evidence Packet
  → WorkItem / Human Gate
```

因此最合理的 P0 不是扩充 production Trace，而是先冻结一个 `Runtime Debug IR v0` 与 Evidence Packet format，并让现有证据资产可被统一读取。只有当 IR 明确暴露某一必要事实始终缺失，才允许提出新的 Trace coverage gate。

正式 Roadmap 当前不应创建。仓库现有授权只支持研究结论；本文末尾仅提供 `Roadmap Draft`，等待 Human 明确认可“要建设这些能力”后再进入正式 lifecycle。

---

## 1. PROJECT_CONTEXT_RESOLUTION

| Field | Resolution |
|---|---|
| Task Type | Architecture research + documentation analysis；不是 implementation、Runtime repair 或 Trace implementation |
| Current State | Phase 2.6 真实调试材料已形成多轮 diagnosis/repair evidence；当前工作区同时存在未提交 Runtime/Trace 相关修复，不能与 committed HEAD 或 graduated capability 混为一谈 |
| Relevant Architecture | Architecture v1；RuntimeAgent 保持 execution/world truth/verification/recovery/completion authority；Data Plane/Harness 只读证据不能成为 Runtime truth |
| Relevant Contract | Runtime Architecture Contract I-1..I-14，重点是 Observation=Evidence、single decision authority、GoalEvidence completion |
| Active Work | `runtime-iterative-full-traversal-acceptance` 及其 Phase 2.6 evidence；本分析不改变其 gate、tasks 或 lifecycle |
| Required Decision | 是否认可 Runtime Debug IR / Evidence Packet 方向；是否授权创建正式 Roadmap；是否授权后续 offline tooling P0/P1 |
| Required Skill | `evidence-driven-debugging`、`runtime-behavior-debugging`；均为 `Authority: NONE` |
| Excluded Context | 新 generic Trace architecture、production instrumentation、wire/API、Runtime behavior、Memory、control plane、自动修复、自动 owner authority |
| Known Facts | 现有资产已经能定位多个真实 FDP；Fusion trace 已产生直接 buyer value；stage/receipt/replay 均已被真实 campaign 使用 |
| Unknowns | 正式 Debug IR owner、packet lifecycle/retention、是否要把更多 Runtime decision coverage 加入 Trace、正式 Roadmap 是否授权 |
| Assumptions | Debug tooling 首先是 validation/offline read-only consumer；任何 inference 均不得回写 Runtime 或成为 action/completion authority |
| Allowed Actions | 只读 inventory、证据比较、Analysis 文档、候选 contract、Roadmap Draft |
| Forbidden Actions | 修改 production Runtime/Trace/Span/Event、实现 CLI、创建 OpenSpec、创建正式 Roadmap、变更 lifecycle/authority |
| Verification Plan | 对照 source、tests、casebook、Phase 2.6 evidence、current taxonomy；最后证明仅新增 Analysis/registry 文档且 `git diff --check` 通过 |

---

## 2. Existing Capability Map

### 2.1 Capability inventory

| Existing asset | 已经解决什么 | 当前边界 | 真实 buyer evidence | 仍需人工操作 |
|---|---|---|---|---|
| [Evidence-Driven Debugging Skill](../../.ai/skills/evidence-driven-debugging/SKILL.md) + [Runtime Behavior Debugging Skill](../../.ai/skills/runtime-behavior-debugging/SKILL.md) | 固化 Expected Reality → Observed Reality → Gap → Evidence → FDP → Owner → Minimal Change；定义 E0-E4、失败分类、STOP 条件与 capability-test 规则 | 方法论，`Authority: NONE`；不会读取文件、对齐 seq、生成 diff、证明 owner 或授权修复 | Capstone revisit、external transition、stale observation 等真实问题已经按此结构进入 casebook/result | Agent 仍需手工定位证据、选择 Good/Bad pair、重建时间线、填写每个字段 |
| [Runtime Debugging Casebook](../decisions/runtime-debugging-casebook/AGENTS.md) | 保存 6 个真实案例的固定十字段结构，复用 Reality Gap、FDP、Owner、Rejected Alternatives | 历史经验，不是当前问题证据、Runtime contract 或 owner authority；引用前必须重新证明相同 Gap | scroll stability、revisit coverage、stale wrong tap、scroll profile、external settle、foreground parser 均有真实 trace/test/result | 全文人工检索；无 machine-readable index、GapKind vocabulary、相似案例匹配或 packet 输出 |
| Agent [`TraceEvent`](../../src/UniClaw.Runtime/Model/TraceEvent.cs) | 追加式记录 Run/Container/Step/Action/Reason/RunState/Trap/Recovery 因果片段 | Agent-owned flat event list；不是 Span 树、artifact store、generic debugger；字段粒度不足以解释所有 perception/semantic predicate | Phase 2.6 stage artifact 已把 Agent trace 与 accepted viewport sequence 对齐；Harness report 用它投影 last decision/action/recovery | 人工扫描 Reason 文本、解析 sequence、按 Run/Container 重建链；无统一事件语义或 terminal-chain projector |
| Runtime [`Activity` observability](../../src/UniClaw.Runtime/Observability/RuntimeObservability.cs) | 在批准边界产生 hierarchical Span、stable layer/component、outcome 与 point events；监听失败不影响 Runtime | fail-open observability；无 Runtime-owned buffer/persistence；Span outcome 不能驱动 Result/GoalEvidence/Recovery；当前 coverage 粗 | `trace-span-read-model` 已验证 Run/Refresh/Observe/Execute 等结构性边界可读 | 需要人工判断 Span 是否覆盖 FDP；`StartSpan(name, layer,component)` 仍是独立字符串组合，命名/coverage 不完整 |
| Harness [`TraceRun` / `RuntimeTraceRecorder`](../../src/UniClaw.Runtime.Harness/TraceRun.cs) | 把 Runtime Activity 冻结成 immutable Span/Event 树，隔离 listener failure | Harness-local diagnostic projection，不是 Runtime truth；当前工作区 recorder 有未提交 event timing/attribute/trace-id 修复，不能视为 committed baseline | 已有 observability conformance 和 trace/span read-model buyer；可随 capture bundle 持久化 | 需人工拿到具体 TraceRun；工作树与 HEAD 能力需区分；尚无通用 causal-chain/diff/coverage evaluator |
| DriverHost [Trace/Span read model](../../src/UniClaw.Runtime.DriverHost/Model/TraceSpanReadModel.cs) + [projector](../../src/UniClaw.Runtime.DriverHost/Projection/TraceSpanReadModelProjector.cs) | 对一个显式注册、已 finalized run 提供 summary 与稳定 cursor-paged span 查询；filter 绑定 run/trace/filter | in-process only；无 wire/CLI/UI；不扫描、不推断 latest、不关联 stage artifacts、不 replay | 已批准 change 的 19/19 tasks；历史验证记录为 Trace/Reader/Guard 80/80，persisted reader 58/58 | 人工写调用方；无法直接回答 occurrence timeline、Good/Bad diff、terminal causal chain |
| Harness [capture session/store/reader](../../src/UniClaw.Runtime.Harness/Capture/TraceCaptureSession.cs) | 冻结 observation/action/result，附加 content-addressed artifact，原子 publish；reader 按显式 `CaptureSessionId` 整体 fail-closed 校验 | Harness 数据面；不扫描、修复、重放、写回或选择 Scenario；不拥有 Runtime outcome | capture/read-model capability 已有完整测试；可保存 optional ObservabilityTrace 与 checksum artifacts | Stage evidence 尚未统一进入此 bundle；caller 需人工给 CaptureSessionId、run/trace link 与 artifact meaning |
| Phase 2.6 [stage capture](../../src/UniClaw.Runtime.ValidationHarness/SettingsCampaign/SettingsCampaignProgram.cs) | 通过 `P26_FRAMES`、`P26_FUSION_TRACES`、`P26_STAGE_EVIDENCE` 按 sequence 保存 frames、Fusion trace、candidate/stage views、accepted viewport decisions 与 Agent trace | Settings campaign / validation-side；默认写临时 JSON；不是通用 artifact contract；环境变量和文件命名是 campaign-local | checkbox、Fusion instability、projection bounds、source normalizer 均依赖这些 dumps 找到 exact occurrence/FDP | 手工设置 env、保存 `/tmp`、关联多个 JSON、找 seq/StableKey、复制证据到仓库、校验使用了哪个 receipt |
| [Fusion causal trace](../../platforms/perception/uniclaw_perception/fusion/causal_trace.py) + [operator trace](../../platforms/perception/uniclaw_perception/operators/trace.py) | 记录 InputRefs、RouterDecision、OperatorAttempt/Result、ValidatorDecision、RowStabilization、FusionOutput 和 first failed composition decision；heavy stage data 只保留 refs | 只覆盖 perception/Fusion operator pipeline；`TRACE != CONTROL / EVIDENCE AUTHORITY / SEMANTIC ADMISSION`；不是 end-to-end Runtime trace | seq 4/5 直接给出 7 anchors、uniform-list cadence NOOP、relation-head skipped 与 unresolved titles，定位 `FRAME_LOCAL_FUSION_INSTABILITY` | 仍需手工把 Fusion trace 与 frames、semantic admission、normalizer、terminal 连接；无跨 seq/跨 run diff |
| Vision [deployment identity / receipt](../../src/UniClaw.Vision.Host/CanonicalVisionHostFactory.cs) | canonical production path 一次性读取 receipt；按 model/config/pipeline/deployment 四轴与 schema 对 `/version` fail-closed 验证；live Host 不被后续 receipt mutation 偷换 | Host 只比较 expected/observed，不能选择、推广或重写部署；validation shadow receipt 不等于 CURRENT ACTIVE | Phase 2.6 多个 campaign 因 identity drift 被阻止，后续用 validation-scoped shadow receipt 绑定工作树 `/version`；CURRENT ACTIVE 未改 | run JSON 不总是携带 receipt/digest；人工创建 shadow receipt、记录 digest、证明 run 与 receipt/config/pipeline 一致 |
| Real-run → replay falsifier workflow | 真实 frame/stage/trace → 抽取 deterministic input → RED falsifier → repair → GREEN → fresh real campaign | 当前是工程实践，不是统一工具；replay 不拥有 Runtime authority，不能用旧 fixture 替代 fresh real confirmation | frame-local Fusion repair 使用同一捕获几何重放；bounds rounding 与 normalizer order 均从真实 pair 提炼 deterministic falsifier | 人工抽 candidates/bounds/rows、手写 fixture/test、逐步删减输入、维护 evidence provenance |
| Harness replay + catalog | [`ReplayEnvironment`](../../src/UniClaw.Runtime.Harness/Replay/ReplayEnvironment.cs) 对 observation/dispatch 分离推进并在脚本耗尽/动作漂移时 fail-closed；ScenarioCatalog 深验证 manifest/hash/path/provenance | 只执行已明确 manifest；不会从真实 capture 自动生成 fixture，不会最小化，不选择 Scenario | Observation replay、golden run、reality-seeded assets 已证明 deterministic replay 价值 | capture→manifest、artifact→frame、failure→minimal subset 仍由人完成 |
| Existing validation/diagnostic entry points | `tierb`、`settingscampaign`、`fixturefreeze`、Android reality fixture capture/build/install；campaign report 含 gates、ledger、terminal、knowledge/PlanDelta | 多个专用入口，不是统一 runtime-debug CLI；部分路径/设备/临时文件固定于当前环境 | Phase 2.5/2.6 Real Emulator campaign 与 fixture freeze 已实际使用 | 人工记命令/env/path、提取 report 子树、跨工具拼 packet；没有统一 summarize/occurrence/diff/chain 命令 |

### 2.2 Current Trace / Span / Event topology

当前至少有三类不同的“Trace”，必须保持区分：

| Plane | Model | Primary purpose | Not allowed |
|---|---|---|---|
| Runtime decision evidence | `Agent.Trace : TraceEvent[]` | Run/Action/Reason/Trap/Recovery 的 Runtime-owned追加式事实 | 不能把外部 debug inference 写回成为 Runtime truth |
| Structural observability | BCL `Activity` → Harness `TraceRun/TraceSpan/ObservabilityEvent` | approved boundary 的层级结构、耗时、outcome、events | 不能驱动 FSM、GoalEvidence、Recovery 或 action |
| Perception decision provenance | Fusion/operator causal trace | 解释 operator 为什么 activated/noop/rejected、fallback 是否运行 | 不能进入 semantic admission、authorization 或 Runtime control |

三类数据可以在 Debug IR 中被关联，但不能被合并成一个新的 authority-bearing “万能 Trace”。

### 2.3 Working-tree qualification

本次盘点时工作区已经存在其他工作：

- `RuntimeTraceRecorder.cs` 的 event timing/attributes/trace-id 修复未提交；
- semantic projection bounds 与 source normalizer logical-order repair 未提交；
- 相应诊断/repair result 文档与测试仍在工作区。

因此本文把它们视为“当前调试 buyer evidence / candidate repair evidence”，不声明它们已毕业、已归档或属于稳定 committed capability。

---

## 3. Repeated Debugging Workflow

### 3.1 Recent bug extraction

| Real bug | Symptom / Terminal | Target occurrence and comparison | LAST_GOOD → FIRST_BAD | GapKind / Owner | Replay / fresh confirmation |
|---|---|---|---|---|---|
| Checkbox adapter regression | 真机帧已有 canonical `checkbox`，但 checkbox→LocalControl 在受控路径不可达；原 worker 假设“checkbox→Unknown”被证据否定 | seq 4/5 `row_009` checkbox 与同 row menu item；对照 RPER-6 contract / historical adapter behavior | Perception 已产出 checkbox → `PhysicalEnvironment.NormalizeType` 不再做 checkbox→toggle | `CONTRACT_REGRESSION` / Adapter；同时暴露 capability coverage 与 campaign composition 独立 gap | RED `test_rper_06` + C# capability falsifiers；当前 HEAD 已恢复 normalization，但具体 campaign receipt/provenance 仍需人工核对 |
| Duplicate precedence | canonical toggle/checkbox 若先走 LocalControl，会把同一物理导航行的 duplicate rendering 变成第二 action source | 同 text、overlap、same physical row 的 menu item 与 checkbox/toggle；对照真实 checkbox child counterexample | duplicate relation 已成立 → affordance pattern 先给 LocalControl | `DECISION_PRECEDENCE_GAP` / Settings Semantic Capability | `Run6_after_adapter_normalization_false_checkbox...` 与 genuine checkbox child tests；需 fresh frame 验证真实 duplicate 不再独立授权 |
| Search icon / `ChildOf` | textless Search icon 被当作独立 Unknown；仅改成 NonInteractive 会破坏视觉角色信息 | icon occurrence + structured `search_action_bar` parent；对照 unrelated/ambiguous/interactive child | Parent relation 可由当前帧证据证明 → semantic output 未保存 Child relation或误把 parent clickability给 child | `COMPOSITION_RELATION_GAP` / Semantic composition + Settings capability | `GenericSemanticCompositionTests`: decorative child only、interactive child、unrelated、ambiguous fail-closed；fresh campaign 仍需 packet 关联 Unknown 消失与 Search relation |
| Frame-local Fusion instability | 同一物理 `Display` row 在 seq 4/5 为 text_block，seq 7+ 为 menu_item，终端 Unknown/completeness failed | seq 4/5 bad vs seq 7/10 good；同页、不同 viewport；Fusion refs + geometry | anchors/input 正确 → uniform-list cadence NOOP 且 relation-head count-only delegated/skipped | `FRAME_LOCAL_FUSION_INSTABILITY` / Perception Fusion operator routing | operator offline replay + captured geometry RED/GREEN + fresh real trace；这是现有 Fusion causal trace 的最强 buyer evidence |
| Projection bounds rounding | 合法 full-width occurrence 在 semantic projection 抛 bounds exception；一个 occurrence 使 seq24/25 整帧 38 candidates 变 Empty | exact index 0 bounds；对照 upstream raw/normalized/fused bounds 均 valid | input bounds `X2==1.0f` valid → float32 `X2-X1` 后再 widen，重建 > 1.0 | `NUMERICAL_BOUNDARY_GAP` / `SemanticObservationFactProjector` | deterministic boundary falsifier；工作树 repair result 声明 fresh run 零 projection exception，但当前仍是未提交并行工作 |
| Source normalizer order drift | seq22→25 同一容器、真实 top-to-bottom order 不变，但 anchor map `3→0` 被判 non-monotonic | 12 shared rows的空间 rank/ΔCenterY Good pair；raw array/canonical signature order Bad pair；target `row_010` | SameSource、stable spatial order 均成立 → order-sensitive predicate 使用 perception serialization order | `REPRESENTATION_ORDER_DRIFT` / `SourceEquivalenceNormalizer` | deterministic logical-order projection falsifier候选；fresh real confirmation 与独立 gate 尚需按当前工作区状态重新核实 |

### 3.2 Common workflow

六类 bug 的 owner 不同，但调试动作高度一致：

```text
0. Reproduction Context
   显式冻结 run/capture/receipt/model/config/pipeline/runtime revision/environment

1. Symptom
   记录用户可见目标、实际界面、terminal state/reason；不从代码名描述现实

2. Freeze Evidence
   保存 Run JSON、frames、stage views、Runtime trace、Fusion trace、receipt、action history

3. Target Occurrence
   用 run + sequence + frame + source + occurrenceId/StableKey 精确定位；StableKey 本身不是 identity proof

4. Good / Bad Pair
   选择最小可比 pair；显式列出 controlled axes 与 changed axes

5. Trace / Stage Diff
   按 pipeline stage 对比 inputs、decisions、outputs、refs，而不是先读长调用链

6. LAST_GOOD / FIRST_BAD
   LAST_GOOD = 最后一个仍与现实一致且有 evidence ref 的节点
   FIRST_BAD = 第一个可证伪偏离节点；不能用最终 throw/terminal 代替

7. GapKind / Owner
   先分类 gap，再把产生 FIRST_BAD decision/state 的 seam 设为 owner；symptom owner 禁止

8. Replay Falsifier
   从真实 evidence 提取 deterministic input；证明 RED，保留 counterexamples 与 fail-closed guards

9. Minimal Repair
   只在单独授权后进入；修 FIRST_BAD owner seam，不扩大到新 architecture

10. Fresh Real Confirmation
    使用匹配 receipt/deployment identity 重跑；fixture GREEN 不能替代 fresh reality

11. Debug Packet → WorkItem / Human Gate
    固化 IR、refs、missing evidence、confidence、disposition；不把 diagnosis 自动变成 Apply authority
```

最适合机械化的是 0、2、3、4、5、6、8 的数据准备，以及 11 的 packet 生成。7 的 owner/authority judgment 与 9 的修复授权仍由 Leader/Human gate 控制。

---

## 4. Runtime Debug IR v0 — Candidate Contract

### 4.1 Position

`Runtime Debug IR` 是调试工具与 Agent 之间的只读交换格式：

```text
Runtime Debug IR
= evidence-backed diagnosis projection
!= Runtime Fact owner
!= WorldBelief
!= GoalEvidence
!= Trace architecture
!= repair authorization
```

它的目标是让一个 case 从“散落的 JSON/Markdown/trace”变成可校验、可 diff、可交接的结构化 packet。

### 4.2 Candidate shape

```yaml
SchemaVersion: runtime-debug-ir.v0
CaseId: string
SourceRun:
  RunId: string
  CaptureSessionId: string | UNAVAILABLE
  TraceRunId: string | UNAVAILABLE
  DeploymentIdentityRef: EvidenceRef | UNAVAILABLE

ExpectedReality: string
ObservedReality: string

Terminal:
  State: string | UNAVAILABLE
  Reason: string | UNAVAILABLE
  EvidenceRef: EvidenceRef | UNAVAILABLE

FirstBadObservation:
  SequenceNumber: integer | UNAVAILABLE
  FrameId: string | UNAVAILABLE
  EvidenceRef: EvidenceRef | UNAVAILABLE

TargetOccurrence:
  OccurrenceId: string | UNAVAILABLE
  StableKey: string | UNAVAILABLE
  SourceKind: PRIMARY_VISION | AUXILIARY_STRUCTURED | RUNTIME_DERIVED | UNKNOWN
  ObservationRef: EvidenceRef
  IdentityClaim: SAME_OCCURRENCE_CONFIRMED | COMPARISON_ONLY | UNRESOLVED

GoodComparison:
  Kind: PREVIOUS_OBSERVATION | NEXT_OBSERVATION | OTHER_RUN | REPLAY | CONTRACT_BASELINE | NONE_AVAILABLE
  Ref: EvidenceRef | UNAVAILABLE
  ControlledAxes: [string]
  ChangedAxes: [string]

EvidenceChain:
  - Order: integer
    Stage: string
    Fact: string
    Ref: EvidenceRef
    Status: CONFIRMED | CONTRADICTED | UNAVAILABLE

LastGood:
  Stage: string
  Fact: string
  Ref: EvidenceRef

FirstBad:
  Stage: string
  PredicateOrDecision: string
  Fact: string
  Ref: EvidenceRef

FailureClass: DISCOVERY | GROUNDING | AUTHORIZATION | EXECUTION | RECOVERY | ENVIRONMENT | CROSS_STAGE
GapKind: EVIDENCE_MISSING | CONTRACT_REGRESSION | CAPABILITY_COVERAGE_GAP | COMPOSITION_RELATION_GAP | DECISION_PRECEDENCE_GAP | CORRELATION_FAILURE | REPRESENTATION_DRIFT | FRAME_LOCAL_INSTABILITY | NUMERICAL_BOUNDARY_GAP | BOUNDED_POLICY_GAP | ENVIRONMENT_MISMATCH | TRACE_COVERAGE_GAP | ARCHITECTURE_OWNERSHIP_UNRESOLVED | UNKNOWN

Owner:
  Kind: AGENT | CONTAINER | TRAVERSAL | ENVIRONMENT | DEVICE_ADAPTER | RUNTIME_WORLD | SEMANTIC_CAPABILITY | VISION_FUSION | TEST_HARNESS | VALIDATION_HARNESS | DEPLOYMENT_COMPOSITION | MULTI_OWNER_GATE | UNKNOWN
  Module: string | UNAVAILABLE
  Evidence: [EvidenceRef]

EvidenceRefs: [EvidenceRef]
MissingEvidence: [MissingEvidence]
Confidence: CONFIRMED_BY_FALSIFIER | HIGH | MEDIUM | LOW | UNASSESSED
SuggestedDisposition: EVIDENCE_COLLECTION | MINIMAL_REPAIR | ARCHITECTURE_GATE | ENVIRONMENT_GATE | INSUFFICIENT_EVIDENCE
```

### 4.3 Field semantics

| Field | Rule |
|---|---|
| `ExpectedReality` / `ObservedReality` | 人类可读现实描述；Observed 只含事实，不包含 root-cause inference |
| `Terminal` | 记录 public projection/trace observed terminal；若不可用必须写 `UNAVAILABLE`，不能从 exception 猜 |
| `FirstBadObservation` | 定位 evidence timeline；不等于 FIRST_BAD predicate，后者可能发生在该 observation 内部 stage |
| `TargetOccurrence` | `StableKey` 只做相关性线索；必须单独记录 identity claim status |
| `GoodComparison` | 必须列 controlled/changed axes，防止把 receipt、locale、pipeline、viewport 差异误当算法差异 |
| `EvidenceChain` | 有序、引用式、不可把整块 artifact 复制进 IR；每个 inference 必须落到 ref |
| `LastGood` / `FirstBad` | 必须是相邻可解释边界；若中间缺 coverage，Disposition 只能是 Evidence Collection/Insufficient Evidence |
| `GapKind` | v0 closed set；具体 bug 名留在 CaseId/Fact，不把每次 blocker 变成新 vocabulary |
| `Owner` | 产生 FIRST_BAD decision/state 的 seam；`MULTI_OWNER_GATE` 表示边界未裁决，禁止 Agent 猜 owner |
| `Confidence` | 只有 deterministic falsifier 对同一 predicate 复现，才可 `CONFIRMED_BY_FALSIFIER` |
| `SuggestedDisposition` | closed set，仅用于路由；不授权修复、架构、环境变更或 implementation |

### 4.4 EvidenceRef and MissingEvidence

候选 `EvidenceRef`：

```yaml
EvidenceRef:
  Kind: RUN_REPORT | RUNTIME_TRACE | SPAN_TRACE | FUSION_TRACE | OBSERVATION | FRAME | STAGE_ARTIFACT | ACTION_HISTORY | REPLAY | TEST_RESULT | RECEIPT | DECISION | CODE_SYMBOL
  Uri: string
  Digest: string | UNAVAILABLE
  RunId: string | UNAVAILABLE
  SequenceNumber: integer | UNAVAILABLE
  FrameId: string | UNAVAILABLE
  OccurrenceId: string | UNAVAILABLE
  StableKey: string | UNAVAILABLE
```

`MissingEvidence` 必须说明：缺什么、为什么需要、由谁采、采集后能否区分哪两个假设。它不是一个自由文本“TODO”。

### 4.5 Evidence Packet format

候选 packet 是一个 immutable manifest，不是新的 artifact database：

```text
runtime-debug-packet.v0
├── packet-manifest.json        # packet id、source run/capture、generation receipt、ref digests
├── debug-ir.json               # Runtime Debug IR v0
├── evidence-index.json         # refs + availability + integrity result
├── comparisons/
│   ├── good-bad.json           # controlled axes / changed axes
│   ├── trace-diff.json         # optional derived projection
│   └── terminal-chain.json     # optional derived projection
└── artifacts/                  # optional;默认引用现有 content-addressed artifacts，不复制
```

Packet rules：

1. explicit run/capture/trace/receipt，禁止隐式 `latest`；
2. refs 默认指向现有 artifact/capture，避免复制大文件；
3. derived outputs 标记 `DERIVED_PROJECTION / Authority:NONE`；
4. 缺证据时 packet 仍可生成，但必须 fail closed 为 `INSUFFICIENT_EVIDENCE`；
5. packet 生成器不得修改、补写、修复或重放 source capture；
6. packet 不能自动创建 WorkItem 或 Apply authorization，只能作为输入。

---

## 5. Capability Layers

| Layer | Input | Output | Automation potential | Current implementation | Gap |
|---|---|---|---|---|---|
| A. Evidence Reading | explicit run/capture/trace/stage/receipt refs | normalized evidence index + availability/integrity | High | FileTraceCaptureReader、TraceSpan read model、campaign JSON、receipt parser 各自可读 | 无统一 reader；schema/path/env 分散；仍靠人识别 artifact meaning |
| B. Evidence Correlation | evidence index + run/seq/frame/occurrence/stable key | occurrence timeline + cross-artifact links | High/Medium | Phase 2.6 通过 sequenceNumber、row_id、candidate id 手工关联；capture records 有 order/sequence/frame | 缺统一 correlation rules；StableKey/occurrence/source identity 容易混淆；无 ambiguity result |
| C. Trace Analysis | one ordered trace/stage chain | terminal chain、first failed predicate candidate、coverage gaps | Medium | Fusion 已有 `first_failed_composition_decision`；Agent Reason/Span/Fusion 各自可读 | 只有 Fusion 有直接 predicate；Runtime end-to-end chain 需人重建；coverage 不足未机器化 |
| D. Differential Trace Analysis | Good/Bad pair + controlled axes | stage-by-stage delta、LAST_GOOD/FIRST_BAD candidate | High/Medium | operator trace deterministic bytes、人工 seq pair、tests/replay | 无跨 frame/run统一 trace diff；不同 trace vocabularies 不能直接比较 |
| E. Reality / Gap Classification | Expected/Observed + diff + chain | FailureClass、GapKind、Owner candidate、confidence/disposition | Medium/Low | Skills + casebook + Leader analysis | 语义判断不可完全自动；closed vocab 未冻结；owner仍需 authority review |
| F. Replay & Minimization | evidence packet + exact predicate oracle | deterministic replay、minimal falsifier、counterexample set | Medium | ReplayEnvironment、operator replay、手工真实→falsifier | 无自动 capture→fixture；无 delta minimization/shrink；provenance 手工 |
| G. Debug Packet Generation | Debug IR + refs + derived outputs | immutable packet + Markdown summary | High | 现有 diagnosis/result 文档人工编排 | 无 schema、generator、integrity/availability summary、receipt attachment |
| H. Leader Review / Gate Decision | packet + scope/authority/lifecycle context | Evidence Collection / Minimal Repair / Architecture Gate / Environment Gate / Insufficient Evidence | Low / must remain human-governed | Project Leader + Human gate | 不应自动化 authority/lifecycle；需要标准 review checklist 和 WorkItem translation |

自动化终点应在“生成可审查 packet”，而不是“自动改 production code”。

---

## 6. Skill / Tooling / Trace / Artifact Boundaries

### 6.1 Definitions

| Layer | Responsibility | Example | Forbidden expansion |
|---|---|---|---|
| Skill | 告诉 Agent 如何从现实、证据、FDP、Owner 到最小修复 | E0-E4、Expected/Observed/Gap、failure classification | 不能定义 Runtime truth、authority、lifecycle、schema/wire 或 implementation authorization |
| Tooling | 对已有 immutable evidence 做机械读取、关联、diff、投影与 packet 生成 | summarize run、occurrence timeline、trace diff、terminal chain | 不能补造 missing evidence、修改 capture、选择 owner authority、驱动 Runtime/action/repair |
| Trace | 记录运行时事实、结构边界与决策因果 | Agent TraceEvent、Activity Span、Fusion operator decision | 不能成为 debugger/control；不能因 debug 需要反向重定义 Runtime contract |
| Artifact | 保存大体积或不可内联的 evidence | screenshot、raw YOLO/OCR、stage views、capture manifest、replay fixture | 不能仅凭存在就成为 truth；必须有 provenance、identity、hash/ref |
| Debug IR | 对以上资产的只读 evidence-backed diagnosis projection | LastGood/FirstBad/GapKind/Owner refs | 不能成为 Runtime Fact/WorldBelief/GoalEvidence、自动 Apply 或架构权威 |

### 6.2 Non-negotiable equations

```text
TRACE != DEBUGGER
TRACE != CONTROL
TRACE != AUTHORITY
TRACE != ARTIFACT STORAGE

SKILL != RUNTIME CONTRACT
SKILL != AUTHORITY

TOOLING != AUTHORITY
TOOLING != REPAIR AUTHORIZATION
TOOLING != RUNTIME INPUT

ARTIFACT != TRUTH WITHOUT PROVENANCE
DEBUG_IR != RUNTIME FACT
HISTORICAL_REPLAY != FRESH_REAL_CONFIRMATION
```

### 6.3 Decision boundary for new trace coverage

只有满足以下全部条件，才应提出新的 Trace coverage gate：

1. Debug IR 已尝试用现有 evidence 构建；
2. `MissingEvidence` 明确指出某个必需 predicate/decision 不可观察；
3. 缺口在至少一个真实 buyer case 中阻止 FDP 定位；
4. 不能通过现有 artifact/capture/tooling correlation 解决；
5. 新 event/span 只记录事实，不改变行为、时序、authority 或 retention contract；
6. Human/architecture gate 明确授权其 scope、cost、sampling/retention 与 owner。

---

## 7. Gap Matrix

| Gap | Evidence | Current workaround | Candidate capability | Priority | Gate |
|---|---|---|---|---|---|
| 无统一 diagnosis schema | 六个近期 bug 的文档字段/命名不同 | Leader 手工写 Markdown | Runtime Debug IR v0 | P0 | Human 接受 non-authoritative contract |
| 无 packet format | evidence 分散在 `/tmp`、OpenSpec evidence、capture、tests | 人工复制/链接 | Evidence Packet v0 | P0 | Tooling/data lifecycle gate |
| Evidence reading 分散 | capture、span、campaign、receipt 各有 reader | 记命令和 JSON path | explicit multi-source reader | P0/P1 | validation/tooling only |
| occurrence correlation 人工 | seq/row_id/candidate/occurrence/source identity 不同 | `rg`/JSON 阅读 | occurrence timeline + ambiguity result | P1 | no implicit identity proof |
| Good/Bad diff 人工 | Fusion/bounds/order cases反复找 pair | 手工脚本/表格 | differential trace/stage projector | P0/P1 | derived projection only |
| terminal chain 人工 | Agent TraceEvent、Span、Fusion trace 分离 | 搜 Reason、terminal、accepted seq | terminal causal-chain projector | P1 | no completion authority |
| deployment identity 未总随 packet | checkbox report明确 run JSON 缺 receipt env | 手记 shadow receipt/digest | mandatory receipt ref in packet | P0 | deployment owner确认 |
| stage capture 未接 generic capture bundle | SettingsCampaign直接写三个 JSON | 人工复制/checksum | packet adapter referencing existing files；后续才考虑 capture integration | P0/P1 | integration beyond adapter needs gate |
| replay fixture 手工提取 | 38 candidates→几行/bounds 全靠人 | 手写 falsifier | capture→fixture extractor | P2 | validation-only contract + provenance gate |
| falsifier minimization 手工 | 人工删 evidence 直到保留 failure | repeated edit/test | delta minimization / shrink | P2 | exact predicate oracle required |
| Trace completeness 未量化 | Fusion trace可定位，其他链路不一定 | 人读 production code补洞 | FDP benchmark + completeness score | P2/P3 | generic scoring semantics Human gate |
| 正式 Roadmap 未授权 | 当前只有研究 gate | 口头排序 | 文内 Draft | 当前 | Human 明确认可后才建正式文件 |

---

## 8. Candidate Tooling Interfaces — Contract Only

### 8.1 Common rules

所有命令候选默认：

- offline/read-only；
- input 必须显式，不允许猜 `latest`；
- JSON 为 canonical machine output，Markdown 只是 derived view；
- deterministic ordering；
- provenance/receipt/availability 进入输出；
- ambiguity/missing evidence fail closed；
- 不启动 Runtime、不操作设备、不重跑、不改 capture、不创建 WorkItem、不实现 repair。

候选公共状态：

| Status | Meaning |
|---|---|
| `OK` | 输入完整且投影成功 |
| `INVALID_INPUT` | schema/ref/参数非法 |
| `EVIDENCE_UNAVAILABLE` | 显式证据缺失 |
| `IDENTITY_MISMATCH` | run/capture/trace/receipt 不能证明同一上下文 |
| `AMBIGUOUS_OCCURRENCE` | occurrence correlation 多解，拒绝猜测 |
| `INSUFFICIENT_TRACE_COVERAGE` | 不能从现有 trace 证明 LAST_GOOD/FIRST_BAD |

### 8.2 Interfaces

```text
runtime-debug summarize <run-or-packet-ref>
```

Input：显式 run/capture/packet ref。Output：SourceRun、terminal、evidence availability、receipt identity、主要 timelines、MissingEvidence；不输出 root cause 猜测。

```text
runtime-debug occurrence <run-or-packet-ref> --stable-key <key>
runtime-debug occurrence <run-or-packet-ref> --occurrence-id <id>
```

Input：one selector exactly。Output：按 observation sequence 的 occurrence timeline、source/provenance、type/role/bounds changes、linked decisions；多解返回 `AMBIGUOUS_OCCURRENCE`。

```text
runtime-debug trace-diff <left-ref> <right-ref> [--scope <stage|occurrence|terminal>]
```

Input：Good/Bad refs + 可选 scope。Output：controlled axes、changed axes、stage inputs/decisions/outputs delta、LAST_GOOD/FIRST_BAD candidate、coverage gaps。不同 receipt 默认拒绝比较，除非显式标记 comparison purpose。

```text
runtime-debug terminal-chain <run-or-packet-ref>
```

Input：run/capture/packet。Output：terminal reason backwards chain 到 last accepted observation、last action/decision、relevant semantic/normalization/Fusion refs；链缺段必须标 `INSUFFICIENT_TRACE_COVERAGE`。

```text
runtime-debug packet <run-or-packet-ref> [--target-stable-key <key> | --target-occurrence-id <id>]
```

Input：显式 source + optional target。Output：`runtime-debug-packet.v0`；只生成 Evidence Reading/Correlation/derived projections，`GapKind/Owner/SuggestedDisposition` 可保持 `UNKNOWN/UNASSESSED` 等待 Leader。

### 8.3 Explicit non-interface

以下接口不在候选范围：

```text
runtime-debug fix
runtime-debug retry
runtime-debug click
runtime-debug promote-receipt
runtime-debug graduate
runtime-debug choose-owner
```

---

## 9. Automation Candidates

### 9.1 Safe early automation

1. Evidence availability / integrity summary；
2. run/capture/trace/receipt identity cross-check；
3. sequence/frame/occurrence index；
4. occurrence timeline；
5. Good/Bad structural diff；
6. Agent Reason/Span/Fusion trace ordered projection；
7. MissingEvidence generation；
8. Debug Packet JSON + Markdown rendering；
9. deterministic golden tests against existing evidence files。

这些都可以保持 validation/offline、read-only、`Authority:NONE`。

### 9.2 Automation requiring stronger review

1. 自动推荐 GapKind；
2. 自动推荐 Owner；
3. 自动选 Good/Bad pair；
4. 自动提取 ReplayFixture；
5. 自动最小化 falsifier；
6. Trace completeness score；
7. 自动生成 WorkItem。

这些能力会引入 inference、artifact lifecycle 或跨边界 contract。即使实现，也必须把输出标记为 candidate，Leader/Human 继续拥有采用与授权。

### 9.3 Not candidates

- Runtime 自动读取 Debug IR；
- 用 trace/debug score 驱动 action、recovery、completion；
- 自动修改 production code；
- 自动刷新或推广 receipt；
- 把历史 replay 结果当 fresh world truth；
- 为了 debugger convenience 放宽 fail-closed。

---

## 10. Roadmap Draft — Not Authorized

### P0 — Common language and packet

Deliverables（候选）：

- Runtime Debug IR v0；
- Evidence Packet v0；
- Evidence Reading + Trace Analysis Skill 增量，避免与现有 Skill 重复；
- Good/Bad differential workflow；
- 一个只读 corpus：checkbox、duplicate precedence、Search/ChildOf、Fusion instability、bounds rounding、normalizer order drift 六例映射到统一 IR。

Exit evidence：

- 六例都能表达，且不新增 case-specific field；
- missing evidence 可显式表示；
- owner/GapKind 可保持 unresolved；
- packet 不复制大 artifact、不隐式选 latest、不产生 authority。

Gate：Human 接受 IR/packet 是 non-authoritative tooling contract；没有 production Trace/Runtime implementation。

### P1 — Mechanical correlation and diff

Deliverables（候选）：

- automated occurrence correlation；
- trace/stage diff tooling；
- terminal causal-chain tooling；
- summarize / occurrence / trace-diff / terminal-chain / packet offline CLI；
- explicit ambiguity、identity mismatch、insufficient coverage statuses。

Exit evidence：

- 对六例输出与人工 diagnosis 一致；
- 同 receipt/different receipt guard 可证；
- zero writes、zero device/runtime calls、deterministic output；
- 工具不能直接输出 Apply authorization。

Gate：validation/tooling implementation gate；若需要新 production events，立即停止并单独请求 Trace coverage gate。

### P2 — Replay extraction and completeness

Deliverables（候选）：

- replay fixture extraction；
- automatic falsifier minimization；
- trace completeness scoring；
- capture/stage artifact 到 packet 的 provenance-preserving adapter。

Exit evidence：

- 至少三个真实 case 可从 packet 生成 deterministic RED；
- minimizer 保持 exact predicate 与 counterexamples；
- score 只表示 debug coverage，不表示 Runtime correctness；
- fresh real confirmation 仍为独立必需步骤。

Gate：artifact/replay lifecycle + trace semantics architecture gate。

### P3 — End-to-end debugging benchmark

Benchmark question：

> 不读 production code，仅靠 Trace + Evidence 能否定位 First Divergence Point？

Candidate measures：

- `FDP_IDENTIFIED / FDP_AMBIGUOUS / TRACE_COVERAGE_GAP`；
- evidence refs completeness；
- occurrence correlation ambiguity；
- owner candidate correctness（独立 Leader review）；
- 从 packet 到 deterministic falsifier 的人工步骤数；
- fresh real confirmation reproducibility。

禁止把“benchmark 定位成功率”解释为 Runtime correctness、architecture quality 或自动修复授权。

---

## 11. Documentation Governance and Canonical Location

### 11.1 Human taxonomy correction

`PROJECT_LEADER_RUNTIME_DEBUGGING_CAPABILITY_P0_CONTRACT_GATE` 已明确裁决：

- `docs/analysis/`：non-normative Analysis，`Authority: NONE`，不进入 Decision Registry；
- `docs/decisions/`：Decision、Result、Casebook；
- `docs/decisions/runtime-debugging-casebook/`：真实调试经验；
- `docs/work/active/`：current projection；
- `openspec/changes/`：lifecycle / approved work；
- `docs/architecture/`：architecture authority/projection；
- `.ai/skills/`：method-only process guidance。

本 gate 只建立 `docs/analysis/` 的 non-normative 分类并迁移本文；不裁决相邻未跟踪的
`docs/anaylzer/runtime-stability-engineering-landscape.md`，也不建立 `docs/roadmaps/`。

> 更新（2026-08-30）：该 Stability landscape 草稿已于同日合并进
> `docs/analysis/runtime-stability-engineering-landscape.md`（正文 + 附录 A–D），
> `docs/anaylzer/` 草稿已删除；本 gate 结论不变。

### 11.2 Canonical location

本文的 canonical location：

```text
docs/analysis/runtime-debugging-capability-landscape.md
```

本文是 non-normative landscape analysis，不是冻结 Decision；不得登记到
`docs/decisions/index.md`。

当前不创建：

```text
docs/roadmaps/runtime-debugging-capability-roadmap.md
```

正式 Roadmap 仍未授权；不得因本次目录纠正创建 roadmap 文件。

---

## 12. DeepSeek Worker vs Leader / Human Gate

### 12.1 可由 DeepSeek worker 执行的 bounded mechanical work

以下不是当前授权；只有在 Human 接受 P0/P1 且 Leader 冻结 WorkItem 后，才可委派：

| Work | Worker boundary | Required leader acceptance |
|---|---|---|
| 六例→Debug IR fixtures | 只新增 validation/tooling fixture；逐字段引用现有 evidence；不改结论 | IR schema、GapKind/Owner vocabulary 已冻结 |
| EvidenceRef/index reader | 只读 explicit paths/IDs；fail closed；不扫描 latest、不写回 | source formats、status vocabulary、path/provenance policy 已冻结 |
| summarize / occurrence projector | deterministic derived projection；ambiguity 不猜 | correlation identity rules 已冻结 |
| trace-diff / terminal-chain projector | 只比较已有 trace/stage；coverage gap 显式 | comparison axes、LAST_GOOD/FIRST_BAD semantics 已冻结 |
| packet JSON/Markdown renderer | 机械渲染、hash/integrity、golden tests | packet format 与 lifecycle 已冻结 |
| Skill 文档去重/增量 | 只更新批准章节，不复制 Runtime contract | Leader 决定与现有两份 Skill 的合并/引用策略 |
| CLI scaffold/tests | 仅 validation/offline 工程，zero Runtime/device call | P1 implementation gate 明确授权 |

Worker 不得决定 architecture、owner、lifecycle、formal roadmap、new trace coverage 或 repair scope；不得把 `SuggestedDisposition` 变成自动执行。

### 12.2 必须由 Leader / Human architecture gate 裁决

- Runtime Debug IR 是否成为正式 tooling contract，谁拥有版本/lifecycle；
- Evidence Packet 的 canonical storage、retention、privacy、hash/provenance；
- 是否创建正式 Roadmap / OpenSpec；
- 是否扩充 production Runtime/Fusion/Span/Event coverage；
- cross-layer EvidenceRef / OccurrenceRef 的稳定 identity contract；
- Trace sampling、failure-triggered richer capture、cost budget；
- 自动 replay extraction 是否允许读取/复制哪些 artifacts；
- trace completeness score 的语义与误用 guard；
- 任何 Runtime、wire/API、DriverHost、DSH、control plane、GoalEvidence、FSM、Traversal、Semantic authority 变化；
- 将 debug packet 转成 WorkItem 的 authority/provenance gate。

---

## 13. Historical Next Human Gate

本节记录 Analysis 当时提出的下一 gate。该 P0 contract direction 已由
`PROJECT_LEADER_RUNTIME_DEBUGGING_CAPABILITY_P0_CONTRACT_GATE` 接受；实际冻结结果见
[P0 contract](runtime-debugging-capability-p0-contract.md)。正式 Roadmap、P1 tooling、P2/P3
与 production Trace/Runtime change 仍未授权。

建议下一个 gate：

```text
PROJECT_LEADER_RUNTIME_DEBUGGING_CAPABILITY_P0_CONTRACT_GATE
```

Human 需要裁决：

1. 是否认可核心问题定义：优先统一现有 evidence，而不是先建新 generic Trace architecture；
2. 是否接受 `Runtime Debug IR v0` 与 `Evidence Packet v0` 作为 `Authority:NONE` 的 tooling contracts；
3. 是否授权创建正式 Runtime Debugging Capability Roadmap；
4. 正式文档继续放 `docs/decisions/`，还是另开 knowledge-taxonomy decision；
5. 是否只授权 P0 documentation/schema/corpus，还是同时授权 P1 offline read-only tooling；
6. 是否明确 P2/P3 与任何 new trace coverage 继续 `NOT_AUTHORIZED`。

推荐最小裁决：

```text
ACCEPT_P0_CONTRACT_DIRECTION
FORMAL_ROADMAP_CREATION: HUMAN_DECISION_REQUIRED
P0_DOCUMENTATION_AND_FIXTURE_CORPUS: CANDIDATE_FOR_AUTHORIZATION
P1_OFFLINE_TOOLING: SEPARATE_IMPLEMENTATION_GATE
P2_P3: NOT_AUTHORIZED
PRODUCTION_RUNTIME_TRACE_CHANGE: NOT_AUTHORIZED
```

Human Gate 之前，停止在 Analysis；不创建正式 Roadmap、不实现 tooling、不修改 production Trace/Runtime。

---

## 14. Source Inventory

Primary current sources：

- [Architecture index](../architecture/README.md)
- [Current architecture state](../architecture/current-architecture-state.md)
- [Runtime Architecture Contract](../system/constitution/runtime-architecture-contract.md)
- [Current gates](../work/active/current-gates.md)
- [Evidence-Driven Debugging Skill](../../.ai/skills/evidence-driven-debugging/SKILL.md)
- [Runtime Behavior Debugging Skill](../../.ai/skills/runtime-behavior-debugging/SKILL.md)
- [Runtime Debugging Casebook](../decisions/runtime-debugging-casebook/AGENTS.md)
- [Runtime observability](../../src/UniClaw.Runtime/Observability/RuntimeObservability.cs)
- [TraceEvent](../../src/UniClaw.Runtime/Model/TraceEvent.cs)
- [TraceRun](../../src/UniClaw.Runtime.Harness/TraceRun.cs)
- [Trace capture](../../src/UniClaw.Runtime.Harness/Capture/TraceCaptureSession.cs)
- [Trace/Span read model](../../src/UniClaw.Runtime.DriverHost/Model/TraceSpanReadModel.cs)
- [ReplayEnvironment](../../src/UniClaw.Runtime.Harness/Replay/ReplayEnvironment.cs)
- [Vision deployment receipt composition](../../src/UniClaw.Vision.Host/CanonicalVisionHostFactory.cs)
- [Settings campaign stage capture](../../src/UniClaw.Runtime.ValidationHarness/SettingsCampaign/SettingsCampaignProgram.cs)
- [Fusion causal trace](../../platforms/perception/uniclaw_perception/fusion/causal_trace.py)

Recent buyer evidence：

- [Checkbox raw-to-semantic diagnostic](../../openspec/changes/runtime-iterative-full-traversal-acceptance/evidence/CHECKBOX-RAW-VISION-TO-SEMANTIC-TRACE-DIAGNOSTIC-RESULT.md)
- [Fusion trace coverage result](../../openspec/changes/runtime-iterative-full-traversal-acceptance/evidence/FUSION-TRACE-COVERAGE-RESULT.md)
- [Frame-local Fusion role stability repair result](../../openspec/changes/runtime-iterative-full-traversal-acceptance/evidence/FRAME-LOCAL-FUSION-ROLE-STABILITY-REPAIR-RESULT.md)
- [Semantic projection bounds diagnostic](../../openspec/changes/runtime-iterative-full-traversal-acceptance/evidence/SEMANTIC-PROJECTION-BOUNDS-DIAGNOSTIC-RESULT.md)
- [Source normalizer logical-order diagnostic](../../openspec/changes/runtime-iterative-full-traversal-acceptance/evidence/SOURCE-NORMALIZER-LOGICAL-ORDER-DIAGNOSTIC-RESULT.md)
- [Phase 2.6 evidence index](../../openspec/changes/runtime-iterative-full-traversal-acceptance/evidence/README.md)
- [Runtime Stability Engineering Landscape](runtime-stability-engineering-landscape.md) — non-normative analysis; not a taxonomy source（由已归档的 `docs/anaylzer/` 草稿合并而来）
