# PER-013 — Android Hierarchy Typed Observation Migration

版本：v0.1（implementation change；carrier of PER-010/011/012 frozen contracts）

```text
Leader: 5.3
Worker: 5.3-Flash
Base: 6b3c8e92
```

## Intent（WHAT/WHY）

迁移现有 PER-009 XML realization 到 PER-010 typed observation + PER-012
compatibility contract：legacy XML 不再直接产出 string claims（missing=false、
值域 string 映射、lineage 字符串协议），而是经 typed domain
（`UiHierarchyObservation v1`）与 deterministic projector 进入既有 P2 /
EvidenceLedger / WorldModel。同时补 PER-009 closure audit 移交的 A-1/A-3/A-4/A-5。

不是重写 UiAutomator，不实现 PER-011 fusion（M-11/M-12/M-13 明确不做）。

## Scope / Out of Scope

Scope（slice 序列，每 slice 有独立 gate，未过不进下一 slice）：

- Gate 0：PER-009 closure ratification（docs-only，Acceptance #4/#7 交付/转交拆分）
- Slice A：Typed domain（`UniClaw.Kernel/Perception/UiHierarchy/`）+ M-01–M-03
- Slice B：Retype 现有 UiAutomator realization（复用 TryDumpToDevice /
  ProbeStateMachine / ResolveUniqueByResourceId；重构 Parse/ToNodeInfo）+ A-4 测试债
- Slice C：CaptureMetadata 结构化 + `TypedHierarchyProposalProjector`（P2 前确定性
  投影，无 truth authority；消灭 `xml-map:`/`api:36` 字符串协议）
- Slice D：typed forward path + LegacyStateProjection（Compatibility namespace，
  egress-only，Unknown/Partial/Conflicted → unavailable/degraded）+ authority closure
- Slice E：consumer cutover（observer → non-effect-critical → effect-critical，
  legacy XOR typed，rollback 仅 routing + `legacy/degraded` 标记）+ M-06–M-09
- Slice F：legacy removal gate（M-10）+ 真机验证（fixture 不得冒充实机）

Out of Scope（本 change 明确不实现）：

- M-11 TransitiveEvidenceBasis runtime · M-12 MalformedLineage runtime ·
  M-13 Aligned fusion runtime
- Visual/Hierarchy Fusion · OCR fusion · VLM/deep escalation ·
  region crop 实现（Tier 1 感知管线 / PER-011 escalation slice 所有）·
  Grant/irreversible gate（Phase 6 所有）
- 不改 PER-009 D1–D14 / mechanism.md；不重开 PER-009/010/011/012

## Decisions

- D1 落点：typed contract 在 `UniClaw.Kernel/Perception/UiHierarchy/`（Product
  typed observation contract，不是 Host XML schema）。
- D2 Slice A 严格实现 PER-010 `UiHierarchyObservation v1` 最小字段集；checked 语义
  按 `checkedTriState / checkedBooleanExact / checkedBooleanCollapsed` 三能力，
  Exact 需三项 proof（two-state contract + capability metadata 声明 + 可追溯
  contract/fixture evidence），缺任一即 Collapsed（false → Unknown(
  `partial-unrepresentable`)）。无任何以 AndroidApiLevel / class / 历史样本为参的
  Exact 推导 API（PER-010 禁止清单的编译期执法）。
- D3 grill findings 随本 change 携带（evidence/2026-09-26-per-010-adversarial-grill.md）：
  - F-E2：`UiNodeObservation` 携带 `Password`（ObservedValue<bool>，能力属性非状态
    权威），供 Slice B/C 脱敏执法；redaction policy 在 adapter/normalization 层。
  - F-C1：`CaptureMetadata.CaptureTimestamp` 语义 = 采集完成时刻；携带可选
    `CaptureDuration`（撕裂检测输入，provenance-only）。
  - F-D1：Slice C projector 的 claim subject 必须 capture/occurrence-qualified。
  - F-B1/F-E1/F-B2/F-E3/F-E4：Slice B/C adapter contract 与 fixture 落位。
  - F-A1/F-A2/F-F1/F-H1/F-C3/X5-X6：Slice C–F 各 gate 的 acceptance 组成部分。
- D4 Slice C 裁接口、Flash 后实现：`UiHierarchyObservation → projector →
  ObservationProposal[] → 既有 EvidenceLedger.Admit`；P2 authority / Ledger owner /
  WorldModel owner 不变；projector 无 belief/conflict/identity authority。
- D5 LegacyStateProjection 放 Compatibility/Migration namespace，不进 `World/`；
  Unknown/Partial/Conflicted → `unavailable/degraded`，不得压成 ON/OFF。
- D6 不预造 `StateSurfaceRouting` / `LegacyStateDeletionGate` 大组件；仅当 ≥2 个
  真实 consumer 需要共享动态 cutover/rollback 时才抽 seam。
- D7 Authority closure（全程）：adapter = acquisition+normalization；projector =
  deterministic representation only；P2/Ledger = sole admission；WorldModel = sole
  belief/reconciliation；Legacy projection = egress-only；Grounding = target
  binding；Assurance = effect authorization。出现 second Evidence writer / second
  WorldModel path / legacy→P2 / legacy→WorldModel / projector truth decision /
  string lineage 复辟 → 立即 STOP `IMPLEMENTATION_DESIGN_CONFLICT`。

## Acceptance

1. Gate 0：PER-009 Acceptance #4/#7 拆分 ratification 落档（交付 vs 转交，
   transfer 有 owner，D1–D14/mechanism 零改动，未实现部分不称 PASS）；PER-009 保持
   CLOSED。
2. Slice A/Gate A：typed domain 类型落地；M-01–M-03 fixture PASS；`missing != false`、
   `Unsupported != Unknown`、`OccurrenceRef != canonical identity` 测试 PASS；无
   Android-version-specific Product type、无 raw XML 字段进 core contract、无
   canonical identity 铸造；full solution PASS。
3. Slice B/Gate B：existing parser/probe regression PASS + new typed fixtures PASS
   （含 A-4 测试债：MapTargetStateClaim IoU/唯一余量/checkable guard、TryCoObserveXml、
   degraded:no-xml、ResetForNewRun）+ full solution PASS。
4. Slice C/Gate C：结构 metadata 随 accepted Evidence 可恢复，不依赖解析 lineage
   字符串；`UiHierarchyObservation → projector → Admit() → EvidenceRecord` 机械证明。
5. Slice D：M-04/M-05 PASS + architecture closure（legacy projection 不能调 P2/
   Ledger/WorldModel）。
6. Slice E：M-06–M-09 PASS；consumer 单路由，无 dual-read。
7. Slice F：M-10 四 gate 记录或显式不满足记录；真机路径复跑（fixture 不冒充实机）。
8. Authority closure D7 全程零违反；architecture deviation = NONE 或显式 conflict。

## Constraints

- 既有 dirty 文件不触碰（当前工作树干净，base = 6b3c8e92）。
- Product/Harness 分离：产品代码只进 `src/`、`tests/`。
- 每 slice gate 由 5.3 review；Flash 不跨 gate。

## Verification

```yaml
level: DETERMINISTIC（真机项 = ENVIRONMENT，Slice F）
method: dotnet test 全量七套件 + 每 slice gate 的机械断言（见 Acceptance）
expected: 零 PER-009 回归；typed fixtures 全绿；authority closure 零违反
actual: （逐 gate 回填）
evidence: changes/PER-013/state.md 各 gate 行 + evidence/ 文件
```

## References

- changes/PER-010/spec.md（FROZEN typed observation contract）
- changes/PER-011/spec.md（FROZEN fusion contract，本 change 不实现其 runtime）
- changes/PER-012/spec.md + plans/2026-09-26-per-012-semantic-migration-fixture-matrix.md
  （FROZEN migration contract + M-01–M-14）
- evidence/2026-09-27-per009-closure-audit.md（Gate 0 标的 + A-1~A-5 移交）
- evidence/2026-09-26-per-010-adversarial-grill.md（findings 载体）
