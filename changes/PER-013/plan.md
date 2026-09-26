# PER-013 Plan — Gate 0 → Slice A → Gate A（第一轮）

> Owner 指令：先做 `Gate 0 → Slice A → Gate A`，Gate A PASS 后汇报，不跨 Slice B。
> 后续 slice 计划在 state.md status log 逐 gate 追加。

## Gate 0 — PER-009 Closure Ratification（docs-only）

1. 复验 `evidence/2026-09-27-per009-closure-audit.md` 与 PER-013 要求的拆分一致：
   - Acceptance #4：已交付 = 环路（Conflict → Focused directive → subject-scoped
     reobserve → bounded retry → resolve/exhausted）；转交 = 真实 region crop +
     coordinate remap（owner：Tier 1 感知管线区域裁剪 change / PER-011 escalation
     slice）。
   - Acceptance #7：已交付 = producer trust table + A/B/C 语义 + authorization
     predicate；转交 = irreversible-effect production wiring（owner：Grant/Phase 6
     不可逆性分类 change）。
2. 要求核对：transfer 有明确 owner ✓（audit Deferred items ownership 表）；D1–D14/
   mechanism 零改动 ✓；未实现部分如实记录不称 PASS ✓（audit C2/C3 裁决 B）；closure
   criteria 与 actual 一致 ✓（audit Verification 881/881）。
3. 处置：PER-009 state.md status log 追加一行 Gate 0 ratification 记录（docs-only）；
   结果 `PER-009 = CLOSED` 保持。若需改 frozen semantics 才能ratify →
   `CLOSURE_GOVERNANCE_CONFLICT`（本次预期不触发）。

## Slice A — Typed Domain

落点 `src/UniClaw.Kernel/Perception/UiHierarchy/`（namespace
`UniClaw.Kernel.Perception.UiHierarchy`）：

```text
ObservedValue.cs        FieldState(Observed|Unknown|Unsupported) + ObservedValue<T>
                        + FieldProvenance(capture/field/source/normalization)
CheckedState.cs         Checked/Unchecked/Partial + CheckedCapability 三能力
CheckedExactProof.cs    三项 proof record（two-state contract / capability 声明 /
                        可追溯 evidence ref），任一缺失 = Collapsed
CheckedSemantics.cs     纯映射：TriState 无损；Exact true/false→Checked/Unchecked
                        （仅当 proof 完整）；Collapsed false→Unknown(
                        partial-unrepresentable)。无 API-level/class/history 参数。
HierarchyCapabilities.cs flags enum（PER-010 十一项能力）+ Declared 集合
CaptureMetadata.cs      PER-010 最小集 + DeviceId/SessionCorrelation +
                        ObservationCycleId? + CaptureDuration?（F-C1：完成时刻语义）
HierarchyCoverage.cs    CompleteWithinDeclaredSurface/Partial/Unknown（+SourceUnavailable
                        供 PER-011 对齐复用）
UiHierarchyObservation.cs Windows[]/Nodes[] + metadata
UiNodeObservation.cs    OccurrenceRef(capture-scoped) + ObservedValue 字段族 +
                        Password(F-E2) + UiBounds
UiHierarchyCapture.cs   五 outcome 构造期执法（Empty⇒零节点；Malformed⇒无部分节点；
                        SourceUnavailable⇒无 observation+diagnostic；Partial⇒带
                        coverage limitation）
```

测试 `tests/UniClaw.Kernel.Tests/Perception/UiHierarchy/`：

- M-01 Exact false→Unchecked（proof 三件齐全）
- M-02 Collapsed false→Unknown(partial-unrepresentable)；exact-capability 缺 proof
  同样 Unknown；任何路径不得 Unchecked
- M-03 TriState 三值无损映射
- Exact-proof enforcement：proof 组件缺失→不满足→Collapsed 行为；无 API 形态可从
  API level/class/样本历史构造 Exact（公开面只有 MapBoolean/MapTriState + proof record）
- missing != false（Unknown 与 Observed(false) 判别；text 空串=Observed("") 与
  Unknown 判别——F-B1 类型层语义）
- Unsupported != Unknown（能力缺失 vs 本次不足判别）
- OccurrenceRef != canonical identity（同 local index 跨 capture 不相等；
  capture-scoped equality）
- Capture outcome 构造执法（违规构造抛错：Malformed 带节点 / Empty 带节点 /
  SourceUnavailable 带 observation / Partial 无 limitation）

## Gate A — exit 判据

```text
M-01–M-03 PASS
exact-proof enforcement PASS
missing/Unknown/Unsupported/occurrence 测试 PASS
dotnet build + 全量七套件 dotnet test PASS
architecture deviation: NONE
```

PASS 后汇报七项（ratification 结果 / files changed / M 结果 / exact-proof 证据 /
新测试 / full solution / deviation），停，不进 Slice B。

---

## Slice B — Retype 现有 UiAutomator Realization（已完成 2026-09-27）

- typed parse：`UiAutomatorDump.ParseHierarchyObservation(xml, UiHierarchyParseContext)`
  → `UiHierarchyCaptureResult`；field policy 显式声明（结构非法 → Malformed 无
  部分节点；字段级非法值 → per-field Unknown；缺席 → attribute-missing；空串文本
  → Observed("")；未知属性忽略不猜值；bounds 不造默认值；password=true 脱敏 +
  provenance 记 redact:password）。legacy `Parse` string path 原样保留（Slice E
  cutover 前仍是生产路径）。
- A-4 测试债偿还：决策核心抽出 `CoObserveXml`（internal test seam，D8 语义不变，
  LivePerception 委托）+ `TagDegradedNoXml` + `ProbeStateMachine.ResetForNewRun`
  直测 + `MapTargetStateClaim` IoU/唯一余量/checkable guard 四例。Host 增
  `InternalsVisibleTo UniClaw.Host.Tests`（照 Kernel D23 先例）。
- Gate B：七套件 949/949 + certification check PASS（Kernel/Agent 源未动，封印
  稳定）。

## Slice C — Metadata + P2 Projection Seam（5.3 接口裁决，2026-09-27）

**裁决**（Slice C entry 条件「5.3 先裁接口」的落档）：

1. **结构 metadata 载体**：`Provenance` 增加可选第 5 位参数
   `HierarchyCaptureDescriptor? Hierarchy = null`（源兼容：既有调用点零改动；
   Legacy path 恒 null）。descriptor 为 Perception.UiHierarchy 命名空间的 typed
   record（CaptureId/AndroidApiLevel/AcquirerKind/AcquirerVersion/HierarchyFormat/
   DeviceId/SessionCorrelation/ObservationCycleId/CaptureTimestamp/CaptureDuration/
   Capabilities/CoverageCompleteness/CoverageLimitation + 节点级 NodeLocalIndex/
   ParentLocalIndex/Field）。被拒备选：generic `IStructuredCaptureMetadata` 接口
   （单一买家不预造抽象）；side-channel store（破坏单 admission path）。
2. **P2 authority 不变**：`EvidenceLedger.Admit` 检查清单零改动——descriptor
   是 provenance payload，Ledger 通用透传；descriptor 完整性由 projector
   fail-closed 保证（无效 observation 抛出，不产部分 claims）。
   `ComputeEvidenceId` 在 Hierarchy 非 null 时附加 canonical render（Legacy
   path 的 EvidenceId 字节不变——null 不追加任何内容）。
3. **Projector**：`TypedHierarchyProposalProjector`（internal，Kernel.Tests 经
   既有 InternalsVisibleTo 可见；照 ControlBeliefView「求值机制面 internal」
   先例不入公开面白名单）。职责 = deterministic representation only：只投影
   `Observed` 字段（Unknown/Unsupported/空串不产 claim——A8 缺检测不产观察，
   不伪造）；subject occurrence-qualified（`ui.node.{captureId}#{index}.{field}`，
   grill F-D1：capture 限定，防跨 revision 误连续）；checked 值域
   checked/unchecked/partial（typed 名，非 on/off——legacy 投影是 Slice D 的
   egress 职责）；bounds 渲染 x1,y1,x2,y2；lineage 仅进程标记
   `typed-hierarchy:v1`（**零结构语义字符串**——CaptureId 等只走 descriptor）。
4. **禁止回潮**：lineage 字符串协议（xml-map:/api:NN）不得再现；Slice E 的
   closure test 将执法。

Gate C：`UiHierarchyObservation → projector → 既有 Admit() → Accepted
EvidenceRecord`，且 `record.Provenance.Hierarchy` 全字段可恢复、EvidenceId 确定性
（同输入同 id；metadata 参与 id；legacy id 不变）。

## Slice E — Consumer Cutover（已完成 2026-09-27）

**Reader inventory（file:line 级，M-07 依据）**：`ui.node.*` per-node claims 在
src/ 零决策消费方（仅生产者写入 belief）→ **observer 级**。`*.state` 消费方
全部在 effect-critical 链：ConflictResolver（裁决）、PostActionXmlRouter
（验证四门）、UniKernel:489（`{role}.state` belief 读取）、LivePerception
（switch.state writer host.live + 组合）、UiAutomatorDump（{role}.state 映射
writer）+ 值域触点 SharedSubjects/AgentPlanPolicy/HostRunner/LegacyStateProjection。

**Cutover 执行（M-06/M-07）**：
- Group 1（observer/read-only）——**已切**：LivePerception XML per-node 证据
  从 legacy `dump.Claims` 通道切换为 typed projector 通道
  （`ComposeXmlEvidence`：occurrence-qualified subjects 完全替代 bare
  `ui.node.{localId}.*`；无 dual-read）。
- Group 2（non-effect-critical）——**空集**（inventory 无此类消费方，记录）。
- Group 3（Control/verification/effect-critical）——**保持 legacy 路由**：
  ConflictResolver / PostActionXmlRouter / UniKernel 的 `*.state` 链按 PER-009
  冻结语义继续（PER-012 egress surface）；cutover 前置 = typed verification
  consumer 存在（PER-011+ / 后续 change），本轮不伪造。顺序合规：晚组未先于
  早组切换。
- M-08 rollback：单一 typed consumer → 不抽动态 routing seam（owner 规则：
  ≥2 consumer 才抽）。rollback = routing-only 显式变更（还原 extras 为
  legacy `dump.Claims`）+ `legacy/degraded` 标记语义（M-05 已执法 degraded
  投影）；`ComposeXmlEvidence` 无自动回退（typed 不可用 → per-node 证据诚实
  缺席，测试 M06_TypedContextUnavailable 锁定）。
- M-09：`LegacyStateSurfaceFreezeTests` 冻结 8 文件触点集——新增 legacy
  consumer = RED。
- F-G1：MapTargetStateClaim 输出仅作为 group-3 legacy 路由证据（initial 相并置
  /post 相主通道），不存在 legacy→typed 转换（M-06 测试断言 per-node surface
  无 bare legacy subject；Slice D closure 断言 Compatibility 面不可达 P2）。

## 合并 Failure Matrix（F-F1 处置；五列）

| failure | detector | evidence state | owner | retry/escalation | Effect 允许？ |
|---|---|---|---|---|---|
| dump timeout | `WaitForExit(timeoutMs)`→(null,transient) | `SourceUnavailable`+`degraded:no-xml`，无 observation | acquisition adapter（Host） | D8 瞬时不占次数，下周期重试；60s×≤3 | 不依赖 XML 证据；XML 权威缺席→视觉域（Assurance 判） |
| dump unavailable（服务/权限） | dump 无 XML→(null,structural) | `SourceUnavailable`+diagnostic | acquisition adapter | D8 结构性占次数+降级窗 | 同上 |
| malformed XML | `XDocument.Parse` 异常/root≠hierarchy | `Malformed`（无部分节点）；legacy 路径→MarkTransient | adapter field policy | 瞬时重试 | 同上 |
| partial XML（裁剪/虚拟化） | acquisition 声明 coverage | `Partial`+limitation（不补值） | adapter | budget 内重采或按 limitation 消费 | coverage 不足→不授权（fail-closed） |
| stale XML | CaptureTimestamp/CycleId vs 消费时点 | supersession 不复取 authority（baseline L888） | Assurance（freshness） | 新 capture re-observe | 不可用旧 XML 证明 effect 成功 |
| empty hierarchy | Parse 零节点 | `Empty`（≠世界 absence） | adapter | 下周期重采 | 不产 absence claim、不据此授权 |
| duplicate nodes | `ResolveUniqueByResourceId` 多命中→null | occurrence 各自保留；唯一性剥夺 | association 层（WorldModel） | focused re-observe（有界） | 不误绑 target（IdentityMatched 失败→剥夺权威） |
| missing attribute | parser 字段缺席 | `Unknown(attribute-missing)`≠false | adapter field policy | 不猜值；需值时 re-observe | 该字段不作授权依据 |
| post-effect old dump | `_lastDispatchAt` vs dump captureTime（D9 门④） | 四门不过→回截图视觉验证 | PostActionXmlRouter/Assurance | 截图验证 | 不得以旧 dump 判成功 |
| device/app switch | typed metadata DeviceId/SessionCorrelation/surface 不符 | `Unknown`/`Unaligned`（不是 Empty） | PER-011 runtime 之前=Assurance 判 | 新 cycle re-observe | 跨 surface 证据不授权 |

（stale / device-switch 行的 runtime 消费者按 X5/X6 处置归 PER-011
implementation；本矩阵为其预留机械形状。）
