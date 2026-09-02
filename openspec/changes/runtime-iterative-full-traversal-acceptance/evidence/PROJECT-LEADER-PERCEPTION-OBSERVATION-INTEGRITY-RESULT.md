# PROJECT_LEADER_PERCEPTION_OBSERVATION_INTEGRITY_AND_ASSET_EVIDENCE_RESULT

> Gate：PERCEPTION_OBSERVATION_INTEGRITY_AND_ASSET_EVIDENCE（诊断/可观测性优先；零修复授权）。
> 结论：**逐级证据链（Capture→…→RuntimeObservation）已实现并武装**；Z7（带账本首轮）全链完整 38/38
> 观测、**健康→空→健康异常未复现 → NOT_REPRODUCED**，capture-on-recurrence 保持武装；**Owner 不猜测**。
> Z6 seq5 保持 PERCEPTION_RESULT_EMPTY（未证 CAPTURE_ACQUISITION_FAILURE，gate 更正接受）。
> Phase 2.6 STOPPED。

## 1. 精确 stage pipeline（可观测的逐级序列）

```
Capture(screencap) → [perception /analyze]
  → rawModelDetections → normalizedDetections
  → fusionStages[composition-input → composition-output → toggle-inference →
     prune-empty-text → short-text-noise-floor → same-line-nonnav-dedup →
     column-aligned-type-promotion → text-box-misattribution → row-stabilization]
  → fusedEvidence → structuredEvidence(AdbUiHierarchySource 并行) → RuntimeObservation
```
输入指纹 = `inputFingerprint`（fusion trace 的 raw 输入哈希，AssetRef 代理）。

## 2. Observation evidence schema（新记录，validation-side only）

每观测：`{seq, timestampUtc, capture:{inputFingerprint, rawDetectionCount,
normalizedDetectionCount}, fusionStages:{stage→candidates}, fusedEvidenceCount,
structuredEvidenceCount, primaryElementCount, affordanceCount, emptyRuntimeObservation}`——
从**既有** stageViews/fusion trace 派生；**MISSING_ASSET 语义**：raw 输入指纹缺失=采集本身未产出
（区别于"campaign 未存 PNG"——本账本记指纹/计数，不依赖 PNG 留档）。零生产行为变更、被动。

## 3. AssetRef 覆盖（本 gate 交付物）

- `ObservationRef(seq) → TraceRef(inputFingerprint) → EvidenceRef(stageViews/fusion) → AssetRef(指纹)`。
- RawFrameAssetRef = inputFingerprint（哈希引用，不复制大图）；AnnotatedDetectionAssetRef =
  rawModelDetections（引用）。
- 原始 PNG 留档未落地（搁置：anomaly-triggered retention 环形缓冲属后续 acquisition gate——本门只保证
  **指纹级引用面**可归因）。

## 4. Z6 精确 replay / LAST_GOOD / FIRST_BAD（Z7 复跑对照）

| 帧 | 上轮 Z6 | Z7（账本启用 base run）|
|---|---|---|
| LAST_GOOD (N) | seq4：prim=7（Settings/搜索/4 行）| seq54：prim=16（健康）|
| FIRST_BAD (N+1) | **seq5：fused=0 structured=0（指纹未知——无账本）** | **无空帧**（min prim=8，全链健康）|
| NEXT (N+2) | seq6：prim=8（恢复）| — |
| 9 问（对 Z6 seq5）| ①②③④⑤⑥⑦⑧⑨ **均不可答**（当时无逐级计数）| 由账本武装：复发时逐级可答 |
**归因**：Z6 seq5 的 FIRST_DIVERGENT stage **未确立（NOT_REPRODUCED）**；不猜测 Owner（gate §11）。

## 5. Failure taxonomy 就位

分类枚举（CAPTURE_EMPTY / WRONG_FRAME / TIMEOUT / DETECTOR_EMPTY_ON_VALID_FRAME /
OCR_EMPTY_ON_VALID_FRAME / STRUCTURED_EMPTY / MULTI_CHANNEL_EMPTY_ON_VALID_FRAME /
FUSION_DROPPED / PUBLICATION_DROPPED / TEMPORAL_VARIANCE / UNKNOWN）已随账本可按级判定：
- raw=0 且指纹在 → DETECTOR/捕获内容空；
- raw>0 且 composition-input=0 → fusion 入口断；
- 中途某 stage 归零 → 该 stage 为 FIRST_DIVERGENT；
- 帧健康但连续性 fail-closed → TEMPORAL_VARIANCE。
Terminal reason 不作为分类（账本独立于 runtime 决策）。

## 6. Control cases（Z7 实测）

| 控制 | Z7 结果 |
|---|---|
| A 健康完整 root 帧 | ✓ 多帧（prim 8-9，structured 5）|
| B 有效帧+detector miss | 未复现（raw 计数全程健康）|
| C 有效帧+OCR miss | 未单独观测 |
| D structured-only absent while vision healthy | **Z7 未出现**（structured 恒 ≥1；历史 XML 间隙族仍登记，仅不在本 run）|
| E 真空/错帧捕获 | 未复现 |
| F 上游皆零→fusion 零 | 未复现 |
| G fusion 收证据却输出零 | 未复现（row-stabilization 计数=prim 级）|
| H 正常滚动序列 | ✓ 全程（38 观测）|
仪器被动：仅派生记录，未动管线。

## 7. 频率与状况

- Z6/Z6b：发作 2 runs；Z7：0/38（账本下）。频率无法可靠估计直至复发（捕获武装）。
- Z7 终态：深容器 `Unknown interaction affordances remain`（历史 root-Unknown→深度 Unknown 族，
  非完整性方差——下游问题依旧独立成立）。

## 8. Recommended minimal repair gate（证据后）

在 FIRST_DIVERGENT 确立前**不授权任何修复**。复发路径：
- raw 有效 + detector/OCR 零 → `DETECTION_OCR_RELIABILITY` buyer；
- raw 缺失/错帧 → `PERCEPTION_ACQUISITION_STABILITY` buyer；
- 上游证据在、fusion/publication 丢 → `FUSION_PUBLICATION_BUG`；
- 帧各自有效但节奏方差异常 → `TEMPORAL_STABILITY` buyer。
（gate §8 规则原样；未见证据不选。）

## 9. VLM shadow（更正指标）

- role-evaluable=11；正确=7；假语义提升=4 → **accuracy≈63.6% / false-promotion≈36.4%** →
  **UI-TARS-2B_ROLE_CONTRACT = NOT_READY**。
- 独立子能力（不捆绑角色权威）可后续单独评估：VLM_TEXT_REREAD、VLM_REGION_GROUNDING。
- 本 gate 对 VLM 零消耗（无新推理）。

## 10. Owner / StableKey / Phase 2.6

- 本 gate Owner：ValidationHarness 观测插桩（完整性账本，被动、只读）。
- 历史 ROOT_UNKNOWN（OCR 串/短读 + 双通道漏检 + 深页角色）：仍登记但**非最早期 fresh blocker**
  （冻结：EARLIEST_FRESH_BLOCKER = PERCEPTION_OBSERVATION_INTEGRITY_VARIANCE；Z7 未发）。
- StableKey：**PARKED_NOT_GRADUATED**（重开条件已登记：Runtime 暴露 EXPECTED_CONTAINER_TRANSITION /
  AUTHORIZED_CHILD_ENTRY_OBLIGATION 只读事实）。
- 资产：`settingscampaign` 新增 `P26_INTEGRITY` 输出；代码 `ObservationIntegrityLedger.cs`（harness，被动）；
  Z7 证据 `/tmp/p26-intgr-runZ7-{integrity,stage,frames,fusion}.json`。

## 11. 最终状态

- **NOT_REPRODUCED**（Z7 全链健康）；capture-on-recurrence 武装（账本已接，复发即逐级归因）。
- 未改任何：capture/detector/OCR/retry/budget/cadence/normalizer/VLM/语义准入。Phase 2.6 STOPPED。