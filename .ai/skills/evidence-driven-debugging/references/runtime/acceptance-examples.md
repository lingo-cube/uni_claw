# Runtime Debug IR v0 — Acceptance Case Mappings

> Status: `P0_ACCEPTANCE_CORPUS`
> Authority: `NONE`
> Purpose: schema expressivity only; no bug repair or lifecycle claim

以下映射只证明一个通用 IR 能表达五个真实 diagnosis。EvidenceRef IDs 指向原报告、
test 或 captured artifact；本文件不复制大 artifact，不把工作树 repair 视为毕业能力。

## 1. Checkbox Adapter Regression

| IR field | Mapping |
|---|---|
| ExpectedReality | canonical checkbox/switch provider 在 adapter contract 内归一成 Runtime 可消费的 toggle representation |
| ObservedReality | failing contract evidence 显示 `checkbox` 已到 canonical candidate，但 adapter `NormalizeType` 路径曾缺失；真实 seq4/5 `row_009` checkbox rendering 实际被 duplicate precedence 判为 NonInteractive，并非 terminal Unknown 来源 |
| TerminalState | `OBSERVED`: campaign terminal 为 Unknown completeness failure；与本 target occurrence 无直接因果，必须保留此差异 |
| TargetObservation / Occurrence | Run/seq4-5；`row_009` checkbox rendering；status `CANDIDATE` across frames，因为 StableKey/RowId 不是同 occurrence proof |
| GoodComparison | RPER-6 adapter contract + historical normalized behavior；不是伪装成 good real run |
| BadComparison | RED `test_rper_06` + failing adapter source state |
| EvidenceChain | raw=`MISSING`; normalized/fused/canonical=`PRESENT checkbox`; semanticAdmission=`PRESENT no primitive state`; affordance=`PRESENT NonInteractive duplicate`; runtimeState=`PRESENT terminal caused by another textless icon` |
| LastGood → FirstBad | canonical checkbox available → adapter contract normalization absent（component-scope FDP） |
| GapKind / Owner | `CONTRACT_REGRESSION` / `DEVICE_ADAPTER`, confirmed for general adapter path；real `row_009` terminal causality explicitly rejected |
| MissingEvidence | raw label/stage view and original run receipt are missing for end-to-end real-run claim |
| Confidence / Disposition | `CONFIRMED`; historical diagnosis maps to `MINIMAL_REPAIR`, but this corpus grants no repair or current-state claim |

Refs: [checkbox diagnostic](../../../../../openspec/changes/runtime-iterative-full-traversal-acceptance/evidence/CHECKBOX-RAW-VISION-TO-SEMANTIC-TRACE-DIAGNOSTIC-RESULT.md),
[RPER-6 test](../../../../../platforms/perception/tests/test_reality_repair.py),
[semantic capability tests](../../../../../tests/UniClaw.Runtime.Tests/Perception/ExternalSettingsSemanticCapabilityTests.cs).

## 2. Search Icon / ChildOf

| IR field | Mapping |
|---|---|
| ExpectedReality | textless Search icon 保留视觉 `Icon` role，同时用 `ChildOf(SearchActionBar)` 表达组合关系；decorative child 不获得独立 action |
| ObservedReality | Search icon occurrence 进入 semantic layer 后缺少可消费的 parent relation，落入 Unknown/completeness symptom；直接改成 NonInteractive 会丢失视觉 role |
| TerminalState | `OBSERVED`: Unknown interaction affordances remain |
| TargetObservation / Occurrence | explicit observation + icon OccurrenceId；与 structured `search_action_bar` parent 的 evidence relation |
| GoodComparison | capability tests: decorative child、interactive child、unrelated occurrence、ambiguous parent fail closed |
| BadComparison | real Search icon terminal evidence / pre-repair semantic output |
| EvidenceChain | raw/normalized/fused/canonical icon=`PRESENT`; semanticAdmission parent facts=`PRESENT`; affordance composition relation=`MISSING/FIRST_BAD`; runtimeState Unknown=`PRESENT` |
| LastGood → FirstBad | parent/child evidence is available → semantic composition does not emit usable ChildOf relation |
| GapKind / Owner | `COMPOSITION_GAP` / `SEMANTIC_CAPABILITY` |
| MissingEvidence | if run↔test comparison receipt/revision is absent, fresh-real closure remains blocked |
| Confidence / Disposition | `HIGH`; historical mapping `MINIMAL_REPAIR`; corpus itself authorizes nothing |

Refs: [checkbox/search diagnostic](../../../../../openspec/changes/runtime-iterative-full-traversal-acceptance/evidence/CHECKBOX-RAW-VISION-TO-SEMANTIC-TRACE-DIAGNOSTIC-RESULT.md),
[Settings semantic capability](../../../../../src/UniClaw.Semantic.Settings/SettingsSemanticCapability.cs),
[tests](../../../../../tests/UniClaw.Runtime.Tests/Perception/ExternalSettingsSemanticCapabilityTests.cs).

## 3. Fusion NOOP Fallback

| IR field | Mapping |
|---|---|
| ExpectedReality | uniform-list 返回 NOOP 时，未组合行继续进入 relation-head fallback；只有实际 composition success 才能委派 |
| ObservedReality | seq4/5 有 7 anchors；cadence gap 128 超容差使 uniform-list NOOP，但 count-only router 仍 delegated/skipped fallback，`Display`/`Dark theme` 保持 text_block |
| TerminalState | pre-repair run completeness failure；post-repair terminal 有独立 quiescence blocker，不能冒充同一 FDP |
| TargetObservation / Occurrence | seq4/5，`Display row_010` 与相邻 row candidates；Fusion trace refs 明确同 sequence |
| GoodComparison | seq7/10：cadence model activated、rows composed；以及 captured-geometry replay |
| BadComparison | seq4/5 NOOP + fallback skipped |
| EvidenceChain | raw/normalized anchors=`PRESENT`; fused operator attempt/NOOP/router decision=`PRESENT`; canonical text_block=`PRESENT`; downstream Unknown/completeness=`PRESENT` |
| LastGood → FirstBad | operator inputs/anchors correct → router treats anchor count as ownership after uniform-list NOOP |
| GapKind / Owner | `DECISION_LOGIC_GAP` / `VISION_FUSION` |
| MissingEvidence | none for operator FDP; fresh-real campaign identity still required for closure claims |
| Confidence / Disposition | `CONFIRMED` by causal trace + deterministic replay; historical `MINIMAL_REPAIR` only |

Refs: [Fusion trace result](../../../../../openspec/changes/runtime-iterative-full-traversal-acceptance/evidence/FUSION-TRACE-COVERAGE-RESULT.md),
[role stability result](../../../../../openspec/changes/runtime-iterative-full-traversal-acceptance/evidence/FRAME-LOCAL-FUSION-ROLE-STABILITY-REPAIR-RESULT.md).

## 4. Projection Bounds Rounding

| IR field | Mapping |
|---|---|
| ExpectedReality | normalized full-width bounds ending at `X2=1.0` project without losing the frame；truly invalid bounds stay fail closed |
| ObservedReality | seq24/25 `vision:0`, `row_010`, fused bounds valid；float32 subtraction then double widening reconstructs `left+width > 1`, one exception drops all 38 candidates |
| TerminalState | viewport exploration exhausted / no new admitted navigation occurrence after frame-level empty admission |
| TargetObservation / Occurrence | seq24/25, PrimaryVision `vision:0`, `row_010`, explicit fused/stage selectors |
| GoodComparison | seq25 diagnostic or seq28 boundary instances where reconstructed sum stays ≤1 |
| BadComparison | seq24/25 repair-run instance with x1≈0.002778, x2=1.0, reconstructed sum≈1.0000000063 |
| EvidenceChain | raw/normalized/fused bounds=`PRESENT valid`; canonical projection=`FIRST_BAD exception`; semanticAdmission=`PRESENT empty`; affordance/runtimeState downstream absent/exhausted |
| LastGood → FirstBad | valid fused bounds → numerical projection reconstruction violates normalized invariant |
| GapKind / Owner | `NUMERICAL_BOUNDARY_GAP` / `RUNTIME_PERCEPTION` (`SemanticObservationFactProjector` seam) |
| MissingEvidence | none for deterministic FDP; matched receipt/revision required for fresh-real repair confirmation |
| Confidence / Disposition | `CONFIRMED`; diagnostic-time `MINIMAL_REPAIR` candidate, not authorization |

Ref: [bounds diagnostic](../../../../../openspec/changes/runtime-iterative-full-traversal-acceptance/evidence/SEMANTIC-PROJECTION-BOUNDS-DIAGNOSTIC-RESULT.md).

## 5. Source Normalizer Representation-Order Drift

| IR field | Mapping |
|---|---|
| ExpectedReality | same-source frames with stable top-to-bottom row order reconcile despite perception serialization-order changes |
| ObservedReality | accepted seq22→25 share 12 rows with stable spatial order and near-uniform viewport translation；`row_010|menu_item` canonical array position flips 3→0 and anchor monotonicity rejects merge |
| TerminalState | normalization/viewport progression fails to accept the representation as same logical source; terminal belongs to the wider run |
| TargetObservation / Occurrence | Good seq22 vs Bad seq25; `row_010` is correlation candidate supported by same source, StableKey/type and spatial evidence, not StableKey alone |
| GoodComparison | spatial/top-to-bottom logical order and shared-row geometry |
| BadComparison | fusion/canonical serialization order consumed by normalizer predicate |
| EvidenceChain | raw/fused/canonical order=`PRESENT`; runtime source normalization predicate=`FIRST_BAD`; later runtime state=`PRESENT` |
| LastGood → FirstBad | same-source identity + spatial order stable → normalizer uses element-array order and sees false 3→0 reversal |
| GapKind / Owner | `REPRESENTATION_DRIFT` / `RUNTIME_WORLD` (`SourceEquivalenceNormalizer` seam) |
| MissingEvidence | fresh-real confirmation and independent gate evidence remain separate from diagnosis mapping |
| Confidence / Disposition | `CONFIRMED` for diagnosis; historical `MINIMAL_REPAIR` candidate only |

Ref: [logical-order diagnostic](../../../../../openspec/changes/runtime-iterative-full-traversal-acceptance/evidence/SOURCE-NORMALIZER-LOGICAL-ORDER-DIAGNOSTIC-RESULT.md).

## Corpus Acceptance

All five cases fit the same seven-stage chain, correlation status, comparison records,
LastGood/FirstBad, closed GapKind/Owner and closed Disposition. No case-specific field, new
Runtime wire/API, Trace mutation or authority change is required. Missing raw/receipt/fresh-run
evidence is expressible without pretending the diagnosis is complete.

## Machine-readable Conformance Fixtures

The historical mappings have corresponding v0 Evidence Packet fixtures:

- [checkbox adapter regression](fixtures/checkbox-adapter-regression.packet.json)
- [Search icon / ChildOf](fixtures/search-icon-child-of.packet.json)
- [Fusion NOOP fallback](fixtures/fusion-noop-fallback.packet.json)
- [projection bounds rounding](fixtures/projection-bounds-rounding.packet.json)
- [source normalizer order drift](fixtures/source-normalizer-order-drift.packet.json)

Leader validation: Draft 2020-12 schema, internal EvidenceRef resolution, evidence digests, deterministic input digests, repair-gate consistency, comparison-axis ordering, and LastGood/FirstBad stage ordering PASS for all five fixtures. Fixtures remain historical diagnosis samples and do not authorize repair.
