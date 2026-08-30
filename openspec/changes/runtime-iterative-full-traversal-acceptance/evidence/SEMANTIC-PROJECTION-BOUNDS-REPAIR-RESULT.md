# PROJECT_LEADER_SEMANTIC_PROJECTION_BOUNDS_REPAIR_RESULT

> Gate: `PROJECT_LEADER_SEMANTIC_PROJECTION_BOUNDS_REPAIR_GATE`
> Date: 2026-08-30
> HEAD: `3986d3d`（工作树，未提交）
> Decision: **诊断 ACCEPTED（B. ROUNDING_BOUNDARY_BUG）；最小生产修复 APPROVED；更宽 fault-containment 改动 NOT AUTHORIZED**
> Repair: `SemanticObservationFactProjector.Normalize` 最小修复已实施并 full-verified
> Phase 2.6 **维持 STOPPED**

## 1. 结论速览

| 验证维度 | 结果 |
|---|---|
| 修复 diff | `SemanticObservationFactProjector.Normalize`：减法**先提升到 double 再做**（+9 生产行，含确定性文档注释）；5 个回归测试（+85 测试行） |
| RED→GREEN | 2 个新测试在 HEAD 代码 RED（与生产异常同栈 `SemanticEvidenceV2.cs:148 ← SemanticObservationFactProjector.cs:112 ← 70/98`），修复后 GREEN 11/11 |
| 合法输入保持 | `X1=0.002778f, X2=1.0f`（full-width 帧边元素）不再抛异常；`Left+Width == 1.0` 精确（12dp） |
| 非法输入保持 fail-closed | `X2=1.05f` 无 Geometry fact；`X2<X1` / 负维度 / `left<0` 各抛 `ArgumentOutOfRangeException`（bounds 不变量未放宽） |
| 两条注入路径覆盖 | structured（`AddStructuredFacts`）+ primary vision（`AddVisionFacts`）各自有 full-width 回归测试 |
| 全量确定性套件 | **2305/2310 passed**；5 个失败与 HEAD 基线环境性 pre-existing 完全同签名（CORR_HOST03/04/09、Capstone_RealEmulator、ExternalBoundary_RealDevice），与本 gate 无关 |
| 机械校验 | build 0 errors；`check-consistency` ALL PASS（C1..C15）；`openspec validate --changes --strict` 23/23；`git diff --check` CLEAN |
| fresh real proof（run-1，21 frames） | **零 projection exception、零整帧 DROP**；root 页 resolved（sources=16, unresolved=0）；Display child admission 链推进为 4 个 accepted observations；terminal 仍 Failed，**新的 first blocker 已精确定位并记录（不修）** |
| FaultContainmentPressure | `SINGLE_OCCURRENCE_EXCEPTION → WHOLE_FRAME_DROP` **FAULT_CONTAINMENT_PRESSURE_PRESENT**（机制仍存在）；修复后 fresh run 无 buyer evidence → **不开新 fault-containment gate** |
| Phase 2.6 readiness | **NOT READY / STOPPED**（residual blocker 见 §8） |

## 2. Minimal Code Change

### `src/UniClaw.Runtime/Capabilities/Perception/Semantic/V2/SemanticObservationFactProjector.cs`（唯一生产文件，+9 行）

`Normalize` 现在先把 float32 bounds 提升为 double，**再在有 double 精度下做减法和重建检查**：

```csharp
private static SemanticNormalizedBounds Normalize(ElementBounds bounds) =>
    new((double)bounds.X1, (double)bounds.Y1, (double)bounds.X2 - bounds.X1, (double)bounds.Y2 - bounds.Y1);
```

修复前为 `bounds.X2 - bounds.X1`（float32 减法 → 向上舍入为 `0.9972220063209534f`
→ 提升 double 后 `left + width = 1.0000000062864274 > 1` → 合法 full-width 元素被误判越界）。
修复后 `(double)1.0f − (double)0.0027779999654740095 = 0.9972220000345260`，`left + width == 1.0` 精确 ≤ 1。

未触碰：`SemanticNormalizedBounds` 不变量（`X2==1.0f` 合法、`X2>1`/负维度非法）、
`AddVisionFacts`（`IsValid && X2>X1 && Y2>Y1` guard）、`AddStructuredFacts`（`IsValid` guard）、
fusion / normalizer / Pattern-5 / completeness / frame-wide fault containment。

### `tests/UniClaw.Runtime.Tests/Perception/SemanticObservationFactProjectorTests.cs`（+85 行 = 5 测试，共 11 个）

| 测试 | 钉死内容 |
|---|---|
| `FullWidthVisionElementAtFrameEdgeProjectsWithoutFloatReconstructionException` | vision 路径：`X1=0.002778f, X2=1.0f` 合法投影；`Left=0.0027779999654740095, Width=0.9972220000345260, Left+Width=1.0`（12dp） |
| `FullWidthStructuredElementAtFrameEdgeProjectsWithoutFloatReconstructionException` | structured 路径：同 bounds 经 `AddStructuredFacts` 投影，Auxiliary Geometry 和精确 |
| `FullWidthElementWithZeroLeftEdgeStillProjects` | `X1=0f, X2=1f` 宽度 1.0 精确 |
| `OutOfFrameRightEdgeStillFailsClosed` | `X2=1.05f` → 无 Geometry fact；`new SemanticNormalizedBounds(0.05,0.05,1.0,0.15)` 抛 `ArgumentOutOfRangeException` |
| `InvertedAndNegativeDimensionsStillFailClosed` | `width<0` / `height<0` / `left<0` 各抛 `ArgumentOutOfRangeException` |

> 数值注意：修复后的 width 是**float32 源值在 double 域做减法的精确值** `0.9972220000345260`，
> 不是旧 float32 舍入值 `0.9972220063209534`——断言已按修复后语义写入。

## 3. RED→GREEN（确定性，先证 RED 再证 GREEN）

- **RED**：HEAD 代码（工作树修复回退为 `git show HEAD` 版本）下运行新测试组 →
  `FullWidthVisionElementAtFrameEdge…`、`FullWidthStructuredElementAtFrameEdge…` **FAIL**，
  异常栈与生产完全一致（`SemanticEvidenceV2.cs:148 ← SemanticObservationFactProjector.cs:112 ← 70/98`）→ RED 有效。
- **GREEN**：恢复工作树修复文件 → 同组测试 PASS；`SemanticObservationFactProjectorTests` **11/11**；
  Perception/Semantic 全组 402/402。

## 4. 反例保持（illegal-bounds preservation）

| 输入 | 行为 | 断言 |
|---|---|---|
| `X2=1.05f`（越右界） | `IsValid=false` → 无 Geometry fact；直接构造也抛 | `OutOfFrameRightEdgeStillFailsClosed` ✅ |
| `X2 < X1` / `width ≤ 0` | ctor 抛 `nameof(left)` "Bounds must be normalized and positive." | `InvertedAndNegative…` ✅ |
| `height ≤ 0` / `left < 0` | 同上 fail-closed | 同上 ✅ |
| 合法 full-width `X2==1.0f` | 精确通过（`left+width==1.0`） | 3 个 full-width 测试 ✅ |

不变量（`SemanticNormalizedBounds`：`0≤left`、`left+width≤1`、`0<width≤1`）逐字未改——只修了
“float32 减法的重建算术”，未放宽任何边界接受。

## 5. 套件 / 构建 / 一致性

- `dotnet build src/UniClaw.Runtime.sln`：0 errors / 0 warnings。
- 全量确定性套件：**2305 passed / 5 failed（2310 total）**。5 个失败：
  `CORR_HOST03/04/09`（Vision host config identity mismatch）、`Capstone_OneAgentOneRun_RealEmulator`、
  `ExternalBoundary_RealDevice`——与 HEAD 基线复跑**同 5 个、同签名**，环境性 pre-existing，与本 gate 无关。
- `scripts/check-consistency.sh`：**ALL PASS**（C1..C15）。
- `openspec validate --changes --strict`：23/23 passed。
- `git diff --check`：CLEAN。

## 6. 真机验证（fresh real campaign, post-fix）

环境：`emulator-5554`；validation-scoped shadow receipt（`/private/tmp/p26-shadow-receipt.json`，
与工作树 pipeline revision 一致，CURRENT-ACTIVE 未动）；`settingscampaign 1`（1 autonomous run）。

### run-1（post-fix，21 observed frames）
| Artifact | SHA-256 |
|---|---|
| `/tmp/p26-projection-repair-r1-stage.json` | `d6cc6621402a736ba311620b90b09142c4f40d8bebe024a13f7428e5705da02d` |
| `/tmp/p26-projection-repair-r1-frames.json` | `f962236bc36e79bd44a1adf83979d01858c992d7d0c8e69290a2b900db0781dd` |
| `/tmp/p26-projection-repair-r1-fusion.json` | `d742d124b0761fa1114c8c722fb9820e1cd3452fa0c58d622ac759468ca66523` |

- **零 projection exception**（21/21 frames 全部正常投影；修复前 seq24/25 在 Display child 抛
  `ArgumentOutOfRangeException: Bounds must fit the normalized frame`）。
- **零整帧 DROP**：`whole-frame DROP frames = []`（修复前 seq24/25 各 38 个候选 → admission=0 整帧丢弃）。
- **admission 链推进**：21/21 frames admitted（seq 1,2,4,5,7,8,10,11,13,14,16,17,19,21,22,24,25,27,28,30,31）；
  修复前 child 只剩 seq25 单窗可见。
- **root 页 resolved**：`open-world container inventory complete: sources=16, unresolved=0,
  seq=[2,5,8,11,14,17,19]`——真实 root normalization 成功（strict overlap 链在本 gate 下逐帧成立，确定性重放 16/16 精确复现）。
- **Display child**：accepted observations 推进为 `[22, 25, 28, 31]`（trace：
  `viewport exploration continue: source-seq=22` → 25/28/31 CONFIRMED → exhausted at 31）；
  **terminal 仍 Failed**：`Source normalization is unresolved; completeness cannot be proven.`

### 修复验收判定
- Display full-width occurrence **不再抛 projection exception**：✅ 21 帧零异常。
- 对应 frame **不再出现 `38 candidates → admission=0` 整帧 DROP**：✅ 零 DROP。
- admission 链继续向前推进：✅（child 从 1 窗 → 4 窗；root 完整 resolved）。
- **记录新的 first blocker，不顺手修**：✅ 见 §8（normalizer 顺序敏感，非本 gate Owner，未授权）。

## 7. FaultContainmentPressure 登记

- `SINGLE_OCCURRENCE_EXCEPTION → WHOLE_FRAME_DROP` = **FAULT_CONTAINMENT_PRESSURE_PRESENT**：
  `SemanticCapabilityEnvironment.ObserveAsync` catch 任何 projector 异常 → `AdmittedSemanticEvidenceSnapshot.Empty`
  （整帧丢弃）的机制**仍然存在**（本次修复消除了该触发源，但未做 frame-wide fault containment）。
- 修复后 fresh run **无真实 buyer evidence**（零 projection exception、零整帧 DROP）→
  **不单独开 fault-containment gate**；仅当后续新增 blocker 再现“单 occurrence 拖垮整帧”的真实 buyer 时再评估。

## 8. Residual Blocker（记录，不修）

### 精确 first divergence（确定性重放 `SourceEquivalenceNormalizer.Normalize`）

Display child accepted observations = `[22(15 sigs), 25(17), 28(17), 31(17)]`。失败发生在 **pair 1
（seq22→seq25）**，不是此前 anchor gate 所假设的 pair 3（seq28→seq31）：

| operator/predicate | result | evidence |
|---|---|---|
| in-frame non-empty / duplicate | pass | 15 / 17 / 17 / 17，无 in-frame exact duplicate |
| strict suffix(union)-prefix(window) | noop | union tail `…row_027\|menu_item, row_028, row_032` 与 window head `row_010\|menu_item, row_020\|text_block…` 无连续重叠 |
| boundary skip-first / last / both | noop | 三者均无 unique overlap |
| anchor count | pass | 13 anchors |
| anchor order | **failed** | union idx 序列 `[3, 0, 1, 2, 4, 5, 6, 9, 10, 11, 12, 13, 14]` —— window row 0（`row_010` Display 标题带）→ union idx 3，window row 1（`row_020\|text_block`）→ union idx 0，**非单调** → `TryAnchorBasedMerge` fail-closed |
| final | **Unresolved** | pair 1 即失败；28→31 identical-window 形状根本未到达 |

**顺序重排的根因**：seq22 中 `row_010`（Display 工具栏标题带）是 partial-width（`X1=0.0667…`）、
element idx 13；seq25 中它是 full-width（`X1=0, X2=1`，正是本次 bounds 修复合法化的 full-width 元素）、
element idx 0 —— 进入 child 后的第一帧与首次稳定确认帧之间，标题带从元素序列**中段跳到最前**，
打乱 signature 顺序，使 anchor 单调性检查 fail-closed（`orderedByWindow[i].UnionIdx ≤ [i-1].UnionIdx → null`）。

### 对 SOURCE-NORMALIZATION-ANCHOR-CONFIRMATION 文档的记录修正

- 此前 anchor diag/repair 文档的 premise（“accepted Display child seq = 25/28/31”）**不完整**：
  `acceptedViewportDecisions` 只列出 scroll-stability CONFIRMED 帧，漏掉 child 容器**首个 accepted observation**
  （seq22；根容器同理漏掉 seq2，但 root 链不受影响）。真实 normalizer 输入是 `[22, 25, 28, 31]`。
- 因此真实 first divergence 一直是 pair 1（22→25 non-monotonic anchors），anchor-adjacent 修复
  （28→31 identical-window，单元验证 44/44 保持正确）在真机链上**从未被到达**——这正是本 run 仍在
  `Source normalization is unresolved` 失败的原因。
- 分类：**NORMALIZER_ORDER_SENSITIVITY / element-order reorder between accepted windows**
  （同 StableKey 行在不同 accepted 帧间的元素索引/顺序漂移，落在 strict + boundary + anchor 单调性之外）。
- Owner：`SourceEquivalenceNormalizer`（Runtime / World normalization）——**不在本 gate 授权范围**
  （本 gate 只授权 bounds 修复；normalizer 改动明确 NOT AUTHORIZED）。

### 独立记录（不混入本 blocker）

- `SEMANTIC_PATTERN_PREDICATE_FACT_FRAGMENTATION`（text_block 副本被 admission 为 NavigationCandidate）
  与 `Color contrast` StableKey 漂移（`row_035→row_036`）仍为已登记上游证据质量问题，本 gate 未触碰。

## 9. Deltas / Phase 2.6

- **AuthorityDelta: NONE**（无授权/权限变化；Human 仅批准 bounds 最小修复）。
- **ArchitectureDelta: NONE**（无新 abstraction/boundary；纯算术域提升，无新状态）。
- **RuntimeBehaviorDelta: BOUNDS-FIX-ONLY**——只改变“float32 边界减法重建”的精度路径；
  不改变 bounds 不变量接受集合（合法/非法输入判定逐项保持，§4）、不改变 fusion/normalizer/Pattern-5/completeness。
- **Phase 2.6 reentry readiness：NOT READY / 维持 STOPPED**。下一 blocker 移至
  `SourceEquivalenceNormalizer` pair 1（22→25，element-order reorder → anchor 非单调 fail-closed），
  需要其自身 Human Gate；本 gate 产出不自动触发任何重入，终态保持诚实 Failed。