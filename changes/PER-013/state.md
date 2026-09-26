# PER-013 — Android Hierarchy Typed Observation Migration

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 6b3c8e92

## Intent（WHAT/WHY）

见 spec.md。迁移 PER-009 XML realization → PER-010 typed observation + PER-012
compatibility contract；不重写 UiAutomator、不实现 PER-011 fusion。

## Decisions / Acceptance / Verification

见 spec.md（D1–D7；Acceptance 1–8；DETERMINISTIC/ENVIRONMENT 分级）。

## Gate 状态

- **Gate 0（PER-009 closure ratification）：PASS**。Acceptance #4/#7 拆分与
  evidence/2026-09-27-per009-closure-audit.md C2/C3 裁决及 Deferred items
  ownership 表逐项一致（#4 转交 owner = Tier 1 感知管线区域裁剪 change /
  PER-011 escalation slice；#7 转交 owner = Grant/Phase 6 不可逆性分类
  change）；D1–D14 与 mechanism.md 零改动；未实现部分按 audit 如实记录不称
  PASS；closure criteria 与 actual（881/881）一致。PER-009 = CLOSED 保持。
  无 CLOSURE_GOVERNANCE_CONFLICT。ratification 记录：PER-009 state.md
  status log「gate-0-ratified」行。
- **Slice A / Gate A：PASS（含 5.3 review）**。typed domain 7 文件落地
  `src/UniClaw.Kernel/Perception/UiHierarchy/`；M-01–M-03 + exact-proof
  enforcement + missing≠false / Unsupported≠Unknown / OccurrenceRef≠identity
  + capture outcome 构造执法共 37 例新测试全绿；Kernel 公开面白名单显式修订
  （HOST-001 D8 路径，+22 条目）；场景库 29 文件按 C8 协议以 `--change
  PER-013` 再认证（Kernel 源码集合变更的法定代价）；full solution 918/918。
  architecture deviation：NONE。
  **5.3 review（2026-09-27）**：对照 Gate A exit 判据逐项复核——
  M-01–M-03 ✓（M01/M02/M03 测试名可追溯 PER-012 矩阵）；exact-proof
  执法 ✓（公开面反射冻结，无 API level/class/历史样本参数；tri-state 走
  boolean 通道 fail-closed）；missing≠false / Unsupported≠Unknown /
  occurrence≠identity ✓；无 Android-version-specific Product type ✓（band
  概念未进类型）；无 raw XML 字段进 core contract ✓（Kernel 新类型零
  XML/string payload）；无 canonical identity 铸造 ✓（OccurrenceRef
  capture-scoped equality + 表面冻结测试）；deviation NONE ✓。进入 Slice B。
- **Slice B / Gate B：PASS**。typed parse（`ParseHierarchyObservation`）落地
  Host：field policy 显式（结构非法→Malformed 无部分节点；字段级非法→per-field
  Unknown；缺席→attribute-missing；空串→Observed("")；未知属性忽略；bounds
  不造默认值；password=true 脱敏+provenance redact:password）；parent/child
  保留（不再 Descendants 展平）；checked collapsed 默认（M-02 typed 修正 A-5）；
  legacy `Parse` string path 原样保留（Slice E cutover 前仍是生产路径）。
  A-4 债务偿还：`CoObserveXml` 决策核心（internal seam，D8 行为不变）+
  `TagDegradedNoXml` + `ResetForNewRun` 直测 + `MapTargetStateClaim`
  IoU/余量/guard 四例；Host 增 InternalsVisibleTo（Kernel D23 先例）。
  Host 49/49；全量 949/949；certification PASS（Kernel/Agent 源未动）。
- **Slice C / Gate C：PASS（含 5.3 接口裁决，落档 plan.md）**。
  `Provenance` 增可选 `HierarchyCaptureDescriptor? Hierarchy`（legacy 恒 null，
  源兼容）；`EvidenceLedger.Admit` 检查清单零改动（P2 authority 不变）；
  `ComputeEvidenceId` 仅在 descriptor 非 null 时追加 canonical render——
  **legacy EvidenceId 字节不变**（WorldModelCanonicalOracle GoldenHashes +
  ClaimEvolution A6 回归通过即为证明）；`TypedHierarchyProposalProjector`
  internal（ControlBeliefView 先例）：只投影 Observed、subject
  occurrence-qualified（ui.node.{captureId}#{index}.{field}，F-D1）、
  checked 值域 checked/unchecked/partial、lineage 仅 typed-hierarchy:v1
  （零结构语义字符串）。Gate C 机械证明：
  observation → projector → 既有 Admit() → Accepted EvidenceRecord，
  `record.Provenance.Hierarchy` 全字段恢复、EvidenceId 确定性 +
  metadata 参与。Kernel 566/566（+9 projector）；白名单 +1（descriptor）；
  场景库再认证（change=PER-013）；全量 958/958。
- **Slice D / Gate D：PASS（含 5.3 architecture review）**。
  Forward path 机械证明：observation → projector → 既有 Admit() →
  JudgeRelevance → WorldModel.Reconcile → belief 携带 occurrence-qualified
  claims（Unknown/Unsupported 不进 belief）。Compatibility path：
  `UniClaw.Kernel.Compatibility.LegacyStateProjection`（不进 World/）；
  M-04 无损（Checked→on / Unchecked→off）；M-05 拒损（Unknown/Unsupported/
  typed Partial/Conflicted → `unavailable`+Degraded，绝不 ON/OFF；typed
  Partial 不投 legacy "partial" 的保守裁决记录于 plan.md——该通道从未经真实
  legacy consumer 验证）。Architecture closure（反射执法测试）：
  Compatibility namespace 全类型的 method/property/field 签名零引用
  EvidenceLedger/ObservationProposal/ObservationClaim/EvidenceRecord/
  AdmissionRecord/WorldModel —— `legacy → typed/P2/Ledger/WorldModel`
  方向不存在。本 slice 零生产接线（cutover 属 Slice E）。白名单 +2；
  Kernel 575/575（+9）；场景库再认证；全量 967/967。
  **5.3 review（2026-09-27）**：对照 authority closure D7 逐项——无第二
  Evidence writer ✓（单一 Admit）；无第二 WorldModel path ✓；legacy 反向
  不可达 ✓（closure 反射）；projector 无 truth 决策 ✓（纯表示映射，Slice C
  冻结）；string lineage 未回潮 ✓（lineage 仅 typed-hierarchy:v1）。deviation
  NONE。
- **Slice E / Gate E：PASS**。Reader inventory 落档（plan.md）：`ui.node.*`
  零决策消费方（observer 级）；`*.state` 全部 effect-critical（ConflictResolver
  / PostActionXmlRouter / UniKernel:489 / LivePerception / MapTargetStateClaim）。
  M-06：LivePerception XML per-node 证据切 typed 路由
  （`ComposeXmlEvidence`——occurrence-qualified 完全替代 legacy dump.Claims，
  无 dual-read；legacy `{role}.state` 映射按相位保留）；projector 升公开
  （真实 buyer = Host live feed，ADR-0026；白名单 +1）。M-07：group1 已切、
  group2 空集、group3 保持 legacy（cutover 前置 = typed verification
  consumer，PER-011+；顺序合规）。M-08：单一 typed consumer → 不抽动态
  routing seam（owner ≥2 规则）；rollback = routing-only + legacy/degraded
  标记语义；`ComposeXmlEvidence` 无自动回退（测试锁定）。M-09：
  `LegacyStateSurfaceFreezeTests` 冻结 8 文件触点集（新增 legacy consumer =
  RED）。F-G1：无 legacy→typed 转换（M-06 + Slice D closure 双锁）。
  F-F1 合并五列 failure matrix 落档 plan.md。API level 经 adb getprop 缓存
  （`TryGetApiLevel`，未知→typed 证据诚实缺席）。Host 54/54（+5 route）；
  Kernel 576/576（+1 freeze）；全量 973/973；再认证 PASS。
- **Slice F — M-10 legacy removal gate：`deletion-blocked`（如实记录，非失败）**。
  四 gate 判定：
  1. zero production legacy readers — **NOT MET**：`*.state` 仍有 5 个生产
     触点（ConflictResolver / PostActionXmlRouter / UniKernel:489 /
     LivePerception / MapTargetStateClaim；freeze 守卫的冻结名单即 reader
     inventory）。
  2. migration fixtures PASS — **MET**：M-01–M-09 全部执行且绿。
  3. shadow/cutover acceptance — group-1（observer 级 per-node）确定性
     acceptance PASS；真机 shadow 见下（live 记录）。
  4. rollback observation window — **NOT MET**：无 rollback 事件可观察
     （无动态 routing seam；rollback = routing-only 显式变更）。
  裁决：legacy surface 保留（这正是迁移期设计状态）；**不新增**
  `LegacyStateDeletionGate` 组件（无 runtime buyer，owner 规则）。删除
  条件的推进 owner = 后续 typed verification consumer 落地的 change。
- **Slice F — 真机验证：PASS（required chain）+ 全闭环 gap 如实记录**。
  环境：API 35 arm64 emulator（scroll-test AVD 的 /tmp 克隆，headless；
  `com.uniclaw.fixture` 在场；boot ~30s；由 5.3-Flash subagent 建立环境，
  leader 会话接管执行）。
  - **required chain PASS**（`TypedLiveChainTests.RealDump_ToTypedObservation_
    ToP2_ToWorldModel`，DSH_TEST_PERCEPTION_LIVE=1）：real dump 9285B
    （launcher3 层级）→ typed observation（Complete，nodes>0）→ 既有
    Admit（全部 Accepted）→ JudgeRelevance/Reconcile → belief 携带
    occurrence-qualified claims；metadata 正确（descriptor vs `adb getprop`
    对拍：api=35、deviceId、acquirer/format）；**collapsed false 实机执法**：
    本次 dump 含 26 个 `checked="false"`、0 个 `checked="true"` 节点 →
    emitted checked claims = 0（Unknown 不降级、不产 off）。
  - **degraded/source-unavailable 可观察**：语义由确定性测试覆盖
    （CoObserveXml 结构性/瞬时/非法 XML 三分类 + ProbeStateMachine 耗尽门）；
    健康设备上未强行制造失败，如实记录。
  - **post-action path 未旁路**：代码级保留（post 相 stateClaim 主通道不变，
    M06_PostPhase 测试）+ 四门 router 零改动。
  - **全闭环（HostLiveFull，视觉+tap+复查）：environment-blocked，非
    PER-013 回归**——headless 下三次失败（swiftshader×2/angle×1），形态相同
    （delivered=1，wifi 未翻转）；**基线判别**：base commit 6b3c8e92
    （零 PER-013 改动，worktree + 主仓库 venv）同样失败、同形态。2026-09-27
    的 GREEN 记录为窗口化 emulator 环境。gap 记录：headless GPU 渲染下视觉
    检测→tap 命中链不可用；不以 fixture 冒充（owner 规则），后续有窗口环境
    复跑即可。

## Final Focused Grill（G1–G8，5.3 执行）

| # | 攻击面 | 裁决 | 机械证据 |
|---|---|---|---|
| G1 | Unknown 压成 false | PASS | `MissingAttribute_IsUnknown_NotObservedFalse`（parser/类型双层）；`M02_CollapsedCheckedFalse_...NeverUnchecked`；`Project_UnknownAndEmpty_EmitNoClaims`（Unknown 不产 claim ≠ off）；live：checked claim 数 == 原始 XML `checked="true"` 数 |
| G2 | Exact 偷推导 | PASS | `CheckedApiSurface_Frozen_...`（公开面无 API level/class/历史样本参数）；`ExactProofSatisfied` 要求 capability=Exact ∧ proof 三件全；`MapBoolean_TriStateCapability_...FailClosed`；M-01/M-02 |
| G3 | occurrence 偷变 identity | PASS | `OccurrenceRef_IsCaptureScoped_...NeverEqual` + `SurfaceFrozen`；projector subject = `ui.node.{captureId}#{index}.field`（F-D1）；无任何 canonical id 字段 |
| G4 | raw XML 泄露 Agent | PASS | AgentDecisionContext/ConsultationTypes 零改动（git status）；Kernel typed contract 无 XML payload 字段；projector 输出进 P2 不进 Agent；Host xml string 仅 adapter 内部 |
| G5 | bounds 绕过 Grounding | PASS | AgentActionProposal 仍无坐标（零改动）；bounds 仅作 `ui.node.*.bounds` 证据 claim，无消费方直连 dispatch；MapTargetStateClaim 维持 legacy P2 证据路径（四门 router 执法） |
| G6 | legacy 反向进 typed path | PASS | Slice D closure 反射（Compatibility 面 0 引用 P2/Ledger/WorldModel/Proposal）；`M06_TypedRoute_...NoDualRead`（per-node surface 无 legacy subject）；`ComposeXmlEvidence` 无 legacy→typed 转换 |
| G7 | source failure 伪装 empty | PASS | `UiHierarchyCaptureResult.Validate` 构造执法（SourceUnavailable≠Empty、Malformed 无部分节点）；`CoObserveXml_TransientFailure/StructuralFailure/ MalformedXml` 分类测试；`Parse_MalformedXml_ThrowsFailClosed` |
| G8 | capability 优先于 API level | PASS | `ResolveCheckedCapability_Precedence`（declared flags 唯一输入）；`TryGetApiLevel` 仅填 metadata 事实字段，不参与 capability 推断；legacy adapter 恒 collapsed（API 35 实机亦然——live 测试断言） |

Verdict：**PASS**（G1–G8 全过，无 PASS_WITH_FINDINGS 级发现；live 证据
强化 G1/G8——26×checked=false → 0 claims、API 35 实机仍 collapsed）。

## CLOSED Gate

```text
Gate 0 PASS · Slice A/B/C/D/E PASS · Slice F（M-10 deletion-blocked 如实
记录 + required chain 真机 PASS + 全闭环 gap 含基线判别）
Final Grill PASS · Full solution 974/974（live 门控套件另 +1 PASS）
Architecture deviation NONE
→ PER-013 = CLOSED（design/implementation 契约面；legacy *.state surface
   按 PER-012 迁移契约保留，删除条件推进 owner = 后续 typed verification
   consumer change）
```

### Grill findings 处置追加（Slice D 时点）

- F-H1（ElementSummary 无 cycle/freshness 引用）与 X5/X6（stale/timestamp
  场景测试）：**移出 PER-013**——属 agent-context 投影与融合/时序对齐域，
  owner = PER-011 implementation change / 后续 agent context change。PER-013
  的 Slice F 真机验证仍覆盖「post-action path 未旁路」（X5 的纪律子集）。

## Status log

- 2026-09-27 · verified→**closed** · Slice F + Final Grill + CLOSED：
  M-10 deletion-blocked（四 gate 如实）；真机 required chain PASS（API 35
  emulator，TypedLiveChainTests，26×false→0 claims 实机执法 M-02）；
  HostLiveFull 全闭环 headless 环境 gap（base 6b3c8e92 判别同败 = 非回归）；
  G1–G8 全 PASS；974/974；deviation NONE。emulator 环境由 5.3-Flash
  subagent 建立（AVD /tmp 克隆绕开 snapshot-pending FATAL），leader 接管
  执行与判别。
- 2026-09-27 · implementing（Slice E 完成）· reader inventory + M-06~M-09 执行
  （详见 Gate 状态与 plan.md Slice E 节）：typed 路由 cutover（observer 级）、
  effect-critical 保持 legacy（PER-012 egress surface）、freeze 守卫、合并
  failure matrix。Kernel/Host 改动 + 测试 +5+1；全量 973/973；再认证 PASS。
- 2026-09-27 · implementing（Slice D 完成）· LegacyStateProjection（Compatibility
  namespace，egress-only）+ M-04/M-05 测试 + forward path 全链路集成测试
  （projector→Admit→JudgeRelevance→Reconcile→belief）+ architecture closure
  反射执法；5.3 authority closure review PASS（见 Gate 状态）；F-H1/X5/X6 处置
  移交记录。Kernel 575/575；全量 967/967；再认证 PASS。
- 2026-09-27 · implementing（Slice C 完成）· Slice C：5.3 接口裁决落档 plan.md
  （descriptor 载体/P2 零改动/EvidenceId legacy 字节不变/projector internal +
  只投影 Observed + occurrence-qualified subject）；实现 HierarchyCaptureDescriptor
  （Kernel，公开面白名单 +1）+ Provenance 可选第 5 参 + ComputeEvidenceId 条件追加
  + TypedHierarchyProposalProjector（internal）；9 例 Gate C 测试（Admit 全链路/
  descriptor 恢复/EvidenceId 确定性/lineage 零协议字符串/fail-closed）。途中修复：
  ComputeEvidenceId 曾对 legacy 追加 "-"（GoldenHashes/ClaimEvolution 红）→ 改为
  条件追加后全绿；白名单位置排序修正。场景库再认证 change=PER-013；全量 958/958。
- 2026-09-27 · implementing（Slice B 完成）· typed parse + A-4 债务（详见 Gate 状态）；
  Host 49/49（+31：typed 21 + debt 10）。
- 2026-09-27 · planned→implementing · Gate 0 PASS（docs-only ratification，
  见上）；Slice A 落地：ObservedValue / CheckedSemantics（含 CheckedExactProof
  三项证明执法）/ HierarchyCapabilities / HierarchyCoverage / CaptureMetadata
  （F-C1：CaptureTimestamp=完成时刻 + 可选 CaptureDuration）/ UiHierarchyObservation
  （occurrence capture-local 构造执法 + Password 字段 F-E2）/ UiHierarchyCapture
  （五 outcome 构造期 fail-closed）。测试：CheckedSemanticsTests（M-01–M-03 +
  API 面冻结——无 API level/class/历史样本 Exact 通道）+ UiHierarchyTypedModelTests
  （missing≠false、empty-string≠Unknown、Unsupported≠Unknown、OccurrenceRef
  capture-scoped + 表面冻结、capture outcome 执法、capability 优先级）。
  途中修复：T? 默认参数 CS1750、Xunit using、enum 笔误、UiBounds FullName
  白名单位置。全量七套件 + 再认证后 918/918（0 环境失败）。
- 2026-09-27 · understanding→persisted · PER-010 adversarial grill PASS_WITH_FINDINGS
  落档（evidence/2026-09-26-per-010-adversarial-grill.md，17 findings）；owner 指令
  findings 全量随本 change 携带；spec/plan 按 owner 最终版 slice 指令落档；base
  6b3c8e92 与 git 实况一致，工作树干净。

## Verification

```yaml
level: DETERMINISTIC
method: >
  dotnet test 七套件（Agent / Agent.Dsh / Core / FileSystemRealization /
  Host / Kernel / Simulation）+ Kernel 白名单测试 + 场景库再认证
  （python3 tools/scenario_certify.py --change PER-013 --all → --check）
expected: >
  M-01–M-03 PASS；exact-proof 三项执法 PASS；missing/Unknown/Unsupported/
  occurrence 测试 PASS；typed parse A-5/bounds/parent/child/password 执法 PASS；
  A-4 债务测试 PASS；Gate C 全链路（projector→Admit→EvidenceRecord，descriptor
  可恢复）PASS；legacy EvidenceId 字节不变；零回归
actual: >
  Agent 17/17 · Agent.Dsh 121/121 · Core 14/14 · FileSystemRealization 9/9 ·
  Host 55/55（+1 门控 live：TypedLiveChain 真机 PASS）· Kernel 576/576 ·
  Simulation 182/182 ——合计 974/974，0 失败，0 环境失败；
  certification check PASS（29 files, 0 violations）；
  live：TypedLiveChainTests PASS（API 35 emulator，9285B real dump）；
  HostLiveFull headless gap（base 判别非回归）
evidence: >
  本文件 Gate 状态 + tests/UniClaw.Kernel.Tests/Perception/UiHierarchy/*.cs +
  tests/UniClaw.Host.Tests/{UiHierarchyTypedParseTests,UiAutomatorDumpDebtTests}.cs +
  scenarios/*.json certification 块（change=PER-013）+
  changes/PER-009/state.md gate-0-ratified 行
```
