# PROJECT_LEADER_RUNTIME_DEBUGGING_TOOLCHAIN_FOUNDATION_RESULT

> Gate: `PROJECT_LEADER_RUNTIME_DEBUGGING_TOOLCHAIN_FOUNDATION_GATE`
> Date: 2026-08-30
> Decision: Runtime Debugging Toolchain 建设 **APPROVED**（Foundation + 契约；实现分片各自 gate）
> AuthorityDelta: `NONE` · ArchitectureDelta: `NONE` · RuntimeBehaviorDelta: `NONE`
> 前置冻结：P0 Contract — `docs/analysis/runtime-debugging-capability-p0-contract.md`（含 canonical Debug IR v0 / Evidence Packet v0 契约与 machine schema，路径见下）
> 本文是 Foundation 记录 + 目标架构；不是实现授权。**完成 Foundation 后停止**：不铺 TUI/Replay。

## 0. 结论速览

| 项 | 结论 |
|---|---|
| 工具链方向 | APPROVED；复用而非重造（`.ai/skills/evidence-driven-debugging` + Runtime Trace/Evidence + casebook + validation harness） |
| P0 契约 | 已冻结（Debug IR v0 / Evidence Packet v0 / occurrence correlation / differential / tooling contract / 五案例 / skill 扩展路由） |
| AssetRef | **一等公民**（本 Gate 新增冻结）——正式进入 Evidence / Trace / Debug IR 查询链，不再作为"旁边附件" |
| 统一 Query Core | 本 Gate 定义只读确定性契约（run/trace/time/evidence/log/asset 六族查询） |
| CLI / TUI | CLI=P1a 垂直切片（并行 change `runtime-debug-p1a-summarize-occurrence`，已 gate）；TUI=P3，仅消费 Query/Analysis Core |
| 治理 | 工具链 = Large / long-lived capability → 独立 OpenSpec change `runtime-debugging-toolchain`，**停在 Human Gate** |
| Generic Trace 重构 / Runtime authority / Phase 2.6 traversal | **NOT AUTHORIZED**（保持现状与 STOPPED） |

## 1. Existing Capability Map（复用清单）

| 资产 | Producer | Storage | 标识符 | 时间戳 | 可查询性 | 当前手工痛点 | Authority 边界 |
|---|---|---|---|---|---|---|---|
| Runtime Trace（spans） | `RuntimeObservability`（16 组件） | `RuntimeTraceRecorder` → `TraceRun`（capture bundle / DriverHost 读模型） | TraceId/SpanId/ParentSpanId（+RunId） | 单调 StartOffsetNs/DurationNs | `GetTraceSummary/GetTraceSpans`、`GetRunTimeline` | 需手工拼 span 树与事件流；perception 内部此前不可拆（本轮已拆） | 观测 fail-open；结构 outcome ≠ 语义 |
| 语义 journal | Agent `DecisionRecord`（~100 点） | AgentStateSnapshot.Trace → RuntimeEventEnvelope store | RunId/ContainerId/StepId/ActionId/RecoveryId/Sequence+CorrelationId | 无时间戳（仅有 Sequence） | `GetRuntimeEvents` | 决策序列无耗时；与 span 只靠 RunId 弱关联 | I-2：journal 权威；不可被 spans 覆盖 |
| 观测/发生 | SemanticCapabilityEnvironment / projector / fusion | Observation / CanonicalOccurrence / AdmittedSemanticEvidenceSnapshot | ObservationSeq / OccurrenceId / StableKey / RowId | 帧序号（SequenceNumber） | 内存对象 / packet fixtures | 逐 seq 手工核 StableKey/RowId；StableKey≠SameOccurrence 证明 | perception 输出 = 候选证据，非世界真值 |
| 证据链 | raw→normalized→fused→canonical→semantic→affordance | EvidenceRef / AssetRef（capture bundle artifacts） | EvidenceRef id / assetId | 帧序号 | `GetEvidence`（catalog） | 链不完整可见；大 artifact 需手工翻 | ASSET_REF ≠ WORLD_TRUTH |
| 日志/诊断 | `Console.Error.WriteLine`（[semantic-diagnostic] 等）+ Harness Diagnostics | stdout / TraceRun.Diagnostics | 无结构化 id | 部分带 seq | 无查询面 | 日志与 trace/evidence 不同源；只能 grep | 诊断文本 ≠ 契约 |
| 截图/帧/裁剪/stage | AdbScreenshotSource / Vision host / capture session | capture bundle artifacts（sha256 校验） | ArtifactId/ContentHash + AssetRef（待建） | capture seq | `ITraceCaptureReader`（按 bundle） | 与 occurrence 不关联；Diagnosis 只写"seq16 row_012 有问题" | ASSET ≠ Runtime truth |
| replay/falsifier 资产 | Harness Replay（AssetContracts/TraceAsset）+ ValidationHarness 各 tier | replay fixture/evidence 文件 + P0 packet fixtures | TraceId/EventId/DeviceId | 有 | ScenarioCatalog/packet 层 | 手工提取 failure→fixture | replay = 离线证据 |
| casebook | `docs/decisions/runtime-debugging-casebook/`（6 案例） | markdown | 案例编号 | 无 | 全文检索 | 经验不结构化为可查询 ref | 案例无 authority |
| Skill | `.ai/skills/evidence-driven-debugging`（SKILL.md + references/runtime/*） | P0 契约文件 | — | — | — | Leader 仍手工拼"读哪个 JSON/哪个 seq/哪个 StableKey" | Skill 不产生架构权威 |

## 2. Target Architecture

```text
Run 产物（spans+journal+evidence+logs+assets）
        │  （capture bundle / DriverHost / tools/runtime_debug 输入）
        ▼
Debug Query Core（READ_ONLY · DETERMINISTIC · NO_TRACE_MUTATION）
  ├─ Run Query   ├─ Trace Query   ├─ Time Query   ├─ Evidence Query   ├─ Log Query   ├─ Asset Query
        ▼
Analysis Core（结构性事实优先：summary / causal chain / diff / first-blocker / occurrence timeline）
        ▼
Debug IR v0 + Evidence Packet v0（canonical schema 已冻结；Assets 只存 Ref 不存体）
        ▼
Agent/LLM Diagnosis（FDP / Owner / GapKind / Disposition；FACT≠INFERENCE≠MISSING_EVIDENCE）
        ▼
CLI（runtime-debug，P1a 起） · TUI（P3，仅消费同一 Core） · Skill 路由（自动触发）
```

CLI / TUI / Agent **共用同一个 Query+Analysis Core**；禁止三套诊断逻辑。

## 3. Debug Data Model & Ref Model（统一 correlation keys）

Correlation keys：`RunId / TraceId / SpanId / EventId / ObservationSeq / OccurrenceId / StableKey / RowId / EvidenceRef / AssetRef / Timestamp / RelativeTimestamp`。

**身份纪律（查询/候选关联用，永不升级为 authority）**：`StableKey != SameOccurrence proof`、`RowId != SameSource proof`、`Bounds != Identity`、`Text != Identity`。

| Ref | 指向 | 来源 | 可关联到 |
|---|---|---|---|
| RunRef | 一次 run | coordinator/capture | TraceRef、EventRef、AssetRef、LogRef |
| TraceRef | TraceRun | recorder | SpanRef、EventId、CorrelationId |
| SpanRef | 一个 span | TraceRun.Spans | EventRef、Time、AssetRef |
| EventRef | RuntimeEventEnvelope | projector/store | DecisionRef、ObservationSeq、SpanRef |
| ObservationRef | 一帧观测 | SemanticObservationReference | OccurrenceRef、AssetRef（screenshot/frame）、EvidenceRef |
| OccurrenceRef | canonical occurrence | fact projector/occurrence 域 | EvidenceRef、StableKey/RowId、crop AssetRef |
| EvidenceRef | 证据链节点 | catalog/capture | 上游 raw→…→affordance 链、AssetRef |
| LogRef | 日志条目 | stdout capture + Diagnostics | RunId、Time、ObservationSeq（可用时）、SpanRef |
| AssetRef | 大体积 artifact | capture bundle（一等公民） | ObservationRef、OccurrenceRef、EventRef、Time |
| ArtifactRef | 持久化 capture 资产 | bundle manifest | AssetRef、DeviceRef、SessionRef |
| DecisionRef | DecisionRecord 投影 | journal | EventRef、Reason、RunState |
| StateRef | AgentStateSnapshot | observability 投影 | ContainerId、Belief、RunState |

## 4. AssetRef —— 一等公民（本 Gate 冻结）

AssetRef 至少覆盖：screenshot · viewport screenshot · cropped screenshot · raw frame · annotated frame · stage image · detector visualization · semantic overlay · video/screen recording · trace artifact · JSON artifact · log artifact · replay fixture。

```text
AssetRef:
  assetId          必填
  assetType        必填（closed set，见上）
  runId            必填
  timestamp        wall-clock（可用时）
  relativeTimestamp（run-relative，可用时）
  observationSeq?  traceId?  spanId?  occurrenceId?
  producer         必填（AdbScreenshotSource / Vision / Fusion / Recorder / ...）
  path|uri         必填
  mimeType         必填
  sha256           有 content identity 时必填
  parentAssetRef?  cropBounds?  annotations?  metadata?
```

**Required projections**：`EvidenceRef → AssetRef`；`Trace/Event → AssetRef`；`Observation → screenshot/frame`；`Occurrence → crop/overlay`（`Occurrence(row_012) → EvidenceRef E123 → AssetRef screenshot://run-r4/seq16 → crop [x1,y1,x2,y2]`）。

**规则**：诊断报告必须同时携带 TraceRef + EvidenceRef + Screenshot/Frame AssetRef（有 crop 则给 crop AssetRef）；**大文件不复制进 Debug IR，只保存 Ref**；AssetRef 塞进 debug 链 ≠ Asset 成为 world truth。

## 5. Debug IR v0 / Evidence Packet v0

**Canonical（继承 P0 冻结，不重造）**：
- Debug IR 语义：`.ai/skills/evidence-driven-debugging/references/runtime/debug-ir-schema.md`；machine schema：`runtime-debug-ir.v0.schema.json`（字段含 `ExpectedReality / ObservedReality / TerminalState / TargetObservation / TargetOccurrence / EvidenceChain / LastGood / FirstBad / GapKind / Owner / EvidenceRefs / AssetRefs / LogRefs / TraceRefs / MissingEvidence / Confidence / Disposition`— 与本 Gate 一致；AssetRefs/LogRefs 入列）。
- Evidence Packet：`evidence-packet.md` + `runtime-debug-evidence-packet.v0.schema.json`。
- `Disposition` closed set：`EVIDENCE_COLLECTION / MINIMAL_REPAIR / ARCHITECTURE_GATE / ENVIRONMENT_GATE / INSUFFICIENT_EVIDENCE`。
- Packet 基础版本机器可生成；语义诊断由 Agent 完成。

## 6. Query Core Contract（新增冻结）

只读 + 确定性 + prune-only（**隐藏 ≠ 删除**；禁止为 TUI 简洁修改/删除原始 Trace）：

| 族 | 查询 |
|---|---|
| Run | `runs` · `latest`（显式输入，禁止猜）· `summary` · `terminal` · `blockers` · `observations` · `unknowns` |
| Trace | `tree`（EXECUTION：Run→Span→Event→ChildSpan）· `causal`（CAUSAL/EVIDENCE：Observation→Occurrence→Evidence→OperatorDecision→SemanticAdmission→Affordance→RuntimeState→Terminal）· `path/ancestors/descendants` · `query/filter`（type/owner/stage/observation/occurrence pruning） |
| Time | wall-clock 区间 · run-relative 区间 · around event/FDP · before/after span · observation 时间窗 |
| Evidence | occurrence evidence chain · observation evidence · raw→normalized→fused→canonical→semantic→affordance · related refs |
| Log | time range · trace/span/event · observation · occurrence · owner/component · severity · around FDP |
| Asset | screenshot for observation · assets for occurrence · assets around event/time · stage assets · open/reveal |

两种 Tree 显式区分（EXECUTION vs CAUSAL）；Causal 是 FDP 主视图；可 prune（只看 Observation→Fusion→Semantic→Affordance→Completeness，隐藏 HTTP/serialization/metrics/bookkeeping）。

## 7. CLI Contract

统一入口 `runtime-debug`（命令面与 closed status `OK/INVALID_INPUT/EVIDENCE_UNAVAILABLE/IDENTITY_MISMATCH/AMBIGUOUS_OCCURRENCE/INSUFFICIENT_TRACE_COVERAGE/SCHEMA_VIOLATION` 继承 P0 tooling-contract）：

```text
runtime-debug runs | run latest | run summary <run> | run blockers <run> | run compare <a> <b>
runtime-debug trace tree|causal|query|path|ancestors|descendants <run|ref> [filters]
runtime-debug evidence occurrence|observation|chain|packet <ref|seq>
runtime-debug diff observation|occurrence|trace <good> <bad>
runtime-debug logs <run> [--from --to --around --span --event --observation --occurrence --owner --type]
runtime-debug assets <run> | asset show <assetRef> | asset related <ref>
runtime-debug diagnose <run>
```

全部命令 `READ_ONLY · DETERMINISTIC · NO_RUNTIME_AUTHORITY · NO_TRACE_MUTATION`；JSON canonical，Markdown 仅 derived view；fail closed。

## 8. TUI Architecture（P3，本 Gate 只冻结架构）

TUI **不实现分析逻辑**，只调用 Query/Analysis Core。区域：Runs · Trace/Causal Tree · Timeline · Filters · Evidence Inspector · Asset Viewer · Logs · Good/Bad Diff · Diagnosis。

- Asset Viewer：screenshot / occurrence crop / bounds 定位 / Good-Bad 切换 / stage image / AssetRef metadata。
- Tree：expand/collapse · type/owner/stage/time pruning · only errors/decisions/evidence-bearing · jump to occurrence/screenshot/logs/causal parent-child。
- 快捷键草案：`t` execution tree · `c` causal · `f` filter · `e` evidence · `a` asset · `l` logs · `g/b` mark good/bad · `d` diff · `p` packet · `x` first divergence。
- UI framework：按仓库技术栈调查后定（不预选）；本 Gate 不实现。

## 9. Skill Debugging Bug 自动触发路由（继承 P0 + 扩展）

```text
IF Runtime/FSM/Traversal/Perception/Fusion/Semantic/Completeness E2/E3/E4 validation failure
THEN load runtime debugging workflow:
Freeze Reality → Query Run → Find First Blocker → Build Evidence Packet
→ Find Good/Bad Pair → Trace Diff → LAST_GOOD/FIRST_BAD → FDP → Owner → GapKind → Disposition
```

Implementation WorkItem 前强制检查：`FDP exists && Owner exists && EvidenceRefs exist`；否则只允许 `EVIDENCE_COLLECTION`。诊断输出显式区分 `FACT / INFERENCE / MISSING_EVIDENCE`。

## 10. Correlation Strategy（trace/evidence/log/asset）

1. 一链主键：`RunId → TraceId → SpanId/EventId → ObservationSeq → OccurrenceId → EvidenceRef → AssetRef`，`CorrelationId`（=TraceId）贯通 envelope/span；
2. **Asset 经 AssetRef 挂入链**（Observation→frame/screenshot，Occurrence→crop/overlay，Event→stage image）；
3. Log 独立但按 `RunId/Time/ObservationSeq` 关联（可用时），LogRef 入 Debug IR；
4. 决策（DecisionRef）经 Sequence/CorrelationId 与 spans 并在 timeline 读模型（`GetRunTimeline`）两侧对齐；
5. 身份纪律：任何候选关联不得升级为 authority；证据不足处 fail closed + `MISSING_EVIDENCE`。

## 11. P0–P5 Roadmap

| Phase | 内容 | 状态 |
|---|---|---|
| P0 Contract/Data Foundation | Debug IR v0 · Evidence Packet v0 · refs · Good/Bad · five-case mapping · skill 路由 | **已冻结**（同日） |
| P1 Read-only Query CLI | run/trace/evidence/log/asset query；scope 垂直切片 `summarize`+`occurrence`（并行 change `runtime-debug-p1a-summarize-occurrence`） | P1a 已 gate，实施中 |
| P2 Analysis | causal chain · run compare · first blocker · FDP 辅助 · packet generator | planned |
| P3 TUI | tree/timeline/filter/viewer/logs/diff/diagnosis（只消费 Core） | planned（先框架调查） |
| P4 Replay/Minimization | `runtime-debug replay extract|replay|minimize`；failure→fixture→minimal falsifier→RED→repair→GREEN→fresh real | contract 本轮仅预留 |
| P5 Agent/Harness Integration | failure→automatic tooling→Debug IR→diagnosis→Gate | planned |

## 12. First Vertical Slice（P1a，已并入并行 change）

```text
一个真实 Run → query → trace causal tree → evidence chain
→ screenshot AssetRef → time-window logs → Evidence Packet → CLI 输出
```

P1a 实现 `summarize` + `occurrence`（Python stdlib `tools/runtime_debug/`），P0 五 fixtures 做 CLI contract tests；不实现 diff/terminal-chain/packet-generator/TUI/Replay，不改 Trace/wire/authority。本 Foundation 的垂直切片即 **P1a + AssetRef 查询面**（后者作为独立任务进 P1/P2 分片）。

## 13. OpenSpec Decision（治理）

- 工具链 = Large / long-lived capability → **独立 OpenSpec change**：`runtime-debugging-toolchain`（proposal/design/specs/tasks 已创建，见 `openspec/changes/runtime-debugging-toolchain/`），**停在 Human Gate**，不 Apply；
- 实现分片（P1a 等）走各自 change/gate；本 Foundation 不一次实现 P0–P5；
- 已检查现有 taxonomy：`docs/analysis/runtime-debugging-capability-landscape.md`（§2 资产图/§6 边界/§7 gap/§8 tooling/§9 automation）+ P0 Contract 为已冻结输入；本 change 引用而非复制。

## 14. Implementation Ownership Split

| 组件 | Owner | 边界 |
|---|---|---|
| Query Core（六族查询） | 独立 Developer Tooling（`tools/runtime_debug/`，stdlib-only） | 只读离线输入（packet/capture/manifest）；不启动 Runtime/设备 |
| Analysis Core（结构性事实 + packet 生成） | 同上 + `.ai/skills/evidence-driven-debugging` | FACT 先行；LLM 不绕过 deterministic core |
| CLI / TUI | 同上（TUI 仅消费 Core） | 禁止各自实现 correlation |
| AssetRef 物化（bundle→asset 目录 + ref 索引） | Harness capture 侧只读投影（如需写回则另 gate） | 大文件不复制进 IR |
| Diagnosis / FDP / Owner / Disposition | Agent/Skill | FACT vs INFERENCE vs MISSING；NO_FDP→NO_IMPLEMENTATION |
| Runtime/Contract | 不动 | Generic Trace 重构 NOT AUTHORIZED |

## 15. Tests / Buyer Cases（七个历史案例）

Checkbox adapter regression · Search icon/ChildOf · fusion uniform-list NOOP fallback · bounds rounding projection exception · source-normalizer representation-order drift · phantom satellite publication · current semantic fragment verdict inconsistency。

每个案例必须能回答：first blocker · TargetOccurrence · Trace chain 位置 · screenshot/frame AssetRef · Good/Bad pair · FirstBad · Owner · GapKind。

**核心 benchmark**："在尽量不读 production code 的前提下，仅靠 Debug Toolchain 定位 FDP 与 Owner"。P0 已冻结五案例映射（`acceptance-examples.md`）；本 Foundation 将 corpus 扩到七个。

## 16. Risks

| Risk | Mitigation |
|---|---|
| 工具链顺手扩 Trace / authority | 本 Gate 明令禁止；TRACE_GAP 先记录、有 buyer 再单独 Gate |
| CLI/TUI/Agent 三套逻辑 | 单一 Query/Analysis Core 契约；P0 tooling-contract enforce |
| Asset 当 truth / 大文件入 IR | AssetRef 只引用；sha256 + mimeType；ASSET_REF≠WORLD_TRUTH |
| grep 当最终 query 架构 | Query Core 只读确定性查询面替代 |
| 并行会话重叠（p1a 同向推进） | Foundation 引用既有 P0/p1a；本 change 作为 umbrella，实现分片各自 gate |

特别确认达成：**截图、frame、crop、stage image 经 AssetRef 正式进入 Evidence/Trace/Debug IR 查询链**（不再作为"旁边的附件"）。

## 17. Next Human Gate

1. 审 `openspec/changes/runtime-debugging-toolchain/`（umbrella proposal/design/spec/tasks）→ 通过后 P1 分片按各自 change apply；
2. P1a（`runtime-debug-p1a-summarize-occurrence`）继续其既有 gate；
3. 本 Gate 后不铺 TUI/Replay；等 P1/P2 用七个 buyer case 收敛后，再单独 Gate P3/P4。