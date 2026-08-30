# PROJECT_LEADER_SEMANTIC_PROJECTION_BOUNDS_DIAGNOSTIC_RESULT

> Gate: `PROJECT_LEADER_SEMANTIC_PROJECTION_BOUNDS_DIAGNOSTIC_GATE`（诊断模式）
> Date: 2026-08-30
> HEAD: `3986d3d`（工作树）
> Disposition: **DIAGNOSIS ONLY / PRODUCTION REPAIR NOT AUTHORIZED / Phase 2.6 STOPPED**
> Scope: 真机 run-1（post anchor-repair）中 Display child admission 前抛出的
> `ArgumentOutOfRangeException: Bounds must fit the normalized frame. (Parameter 'width')`

## 1. 结论速览

| 项 | 结论 |
|---|---|
| 精确 occurrence | PrimaryVision occurrence `vision:0` — 文本 `Display`、`menu_item`（row_010，full-width toolbar 标题带） |
| 触发 predicate | `SemanticNormalizedBounds` ctor 第 147–148 行：`left + width > 1`（`left=X1`，`width=X2−X1`） |
| 根因类别 | **B. ROUNDING_BOUNDARY_BUG**（float32 减法重建边界溢出） |
| FDP | `SemanticObservationFactProjector.Normalize`（第 111–112 行）：float32 `X2 − X1` 先做减法再提升为 double，重建值 `X1 + (X2−X1)` 越过 1.0 |
| Owner | `SemanticObservationFactProjector`（`src/UniClaw.Runtime/Capabilities/Perception/Semantic/V2/`）——不是 normalizer、不是 fusion、不是叙述来源 |
| Frame 级影响 | **整帧证据丢弃**：单个 occurrence（index 0）抛异常 → `SemanticCapabilityEnvironment` catch → 整帧 `AdmittedSemanticEvidenceSnapshot.Empty`；seq24/25 各 38 个候选全部被丢弃 |
| 上游数据 | 全部在 [0,1] 内、无任何非法值（逐层扫描证实）——**不是数据问题，是重建算术问题** |
| 复现窗口 | 与检测像素偏移相关（设备状态相关）：`x1_px ∈ {0, 3, 8}` 中仅 3px 触发；非确定性 flaky 不是回归 |
| 生产修复 | **未授权**；最小修复候选见 §7，等 repair gate |

## 2. Exact Failing Occurrence

| 属性 | 值 |
|---|---|
| Observation source | PrimaryVision（Sources[0]，`ObservationSourceTier.PrimaryVision`） |
| SourceId / ElementIndex | `vision:0`（`observation.Elements[0]`，index 0） |
| Text | `Display` |
| PerceptionType | `menu_item`（raw detection label `toolbar`，fused type `menu_item`） |
| StableKey | `row_010` |
| Fused bounds（full-screen normalized） | `{ x1: 0.002778, y1: 0.0625, x2: 1.0, y2: 0.120625 }` |
| Fused boundsPx | `[3, 150, 1080, 290]` |
| Raw detection（PREPROCESSED） | `{ x1: 0.002268, y1: 1e-5, x2: 1.0, y2: 0.066426 }` px `[2, 0, 720, 93]` |
| ElementBounds（float32，C# 侧） | `X1=0.0027779999654740095f, Y1=0.0625f, X2=1.0f, Y2=0.12062499672174454f` |
| `ElementBounds.IsValid` | **true**（0 ≤ X1 ≤ X2 ≤ 1 全部成立，X2 恰好 = 1.0f） |

这是 Display 子页顶部的 full-width 工具栏标题带（首行、贴右边缘、右缘恰好等于帧边界 1.0）。

## 3. Bounds Transformation Chain（bounds 变换链）

```text
raw/normalized detection（PREPROCESSED 720 宽）
  candidate_1 'Display' menu_item   { x1:0.002268, x2:1.0 }  px [2,0,720,93]        [0,1]✓
→ 9-stage fusion（composition-input ... row-stabilization，bound 全程不变）
→ fusedEvidence（full-screen normalized 1080 宽，_remap_coords 重映射）
  candidate_1 'Display' menu_item   { x1:0.002778, x2:1.0 }  px [3,150,1080,290]    [0,1]✓
→ canonical bound（C# ElementBounds，float32）
  X1=0.0027779999654740095f  X2=1.0f  IsValid=true                                   [0,1]✓
→ SemanticObservationFactProjector.AddVisionFacts　（guard: IsValid && X2>X1 && Y2>Y1 → 通过）
→ Normalize(bounds) = new(X1, Y1, X2−X1, Y2−Y1)
  float32 减法：X2−X1 = 1.0f − 0.0027779999654740095f = 0.9972220063209534f   ← 向上舍入
→ SemanticNormalizedBounds(left=X1, top=Y1, width=0.9972220063209534, height=Y2−Y1)
  left + width = 0.0027779999654740095 + 0.9972220063209534 = 1.0000000062864274  [>1 ✗]
→ throw ArgumentOutOfRangeException(nameof(width), "Bounds must fit the normalized frame.")
```

### 首次超界层级

- **没有**任何一层的数据本身出界：raw / normalized / fusion 全部候选 / fusedEvidence /
  canonical 的 `x2/y2` 最大值 = 1.0（恰好等于帧缘，不是 >1）。
- “超界”只出现在 **投影器重建算术** 中：`X1_double + (X2−X1)_float32`。
- 因此首次超界的**层级** = `SemanticObservationFactProjector.Normalize` 的 float32 减法 +
  `SemanticNormalizedBounds` 的 double 精度检查之间的精度边界（float32 → double widening 发生在减法**之后**）。

## 4. 根因力学（ROUNDING_BOUNDARY_BUG）

`ElementBounds` 以 **float32** 存储边界（`X1/X2`）。`Normalize` 先做 float32 减法：

- 精确数学值：`1.0 − 0.0027779999654740095 = 0.9972220000345260`
- float32 舍入：`0.9972220063209534f`（**向上**舍入，误差 ≈ +6.3e-9）
- double 检查：`left + width = X1f↑double + 0.9972220063209534 = 1.0000000062864274 > 1.0`

`SemanticNormalizedBounds` 的不变量 `left+width ≤ 1` 在数学上等价于 `X2 ≤ 1`，对合法输入
（`X2 = 1.0f`）本应恒真；但 float32 减法先舍入、后提升 double，使重建值越过 1.0。

**触发条件**：full-width（`X2 == 1.0f`）+ 非零 `X1`（使 `1−X1` 的 float32 表示向上舍入）。
`x1_px=3`（`X1=0.002778`）触发；`x1_px=8`（`0.006944`→向下舍入）与 `x1_px=0`（差精确）不触发。

### 为什么这是 flaky 而非 diag 回归

| run / seq | 工具条 x1（px） | normalized X1 | `left+width` | 结果 |
|---|---|---|---|---|
| diag run seq25 | 8 | 0.006944 | 0.9999999990… | 通过（diagnose 时无异常） |
| diag run seq28 | 0 | 0.0 | 1.0 精确 | 通过 |
| repair run-1 seq24/25 | 3 | 0.002778 | **1.0000000063** | **抛异常** |

同一元素在不同 run 的检测像素偏移不同（YOLO 边框抖动 → `_remap_coords` 后 0/3/8px），
因此该缺陷表现为设备状态相关的偶发 `ArgumentOutOfRangeException`。

## 5. 证据矛盾的解释

诊断早期观察到“frames/stage evidence 各层 bounds 全部在 [0,1] 内，但投影在 seq24/25 抛
`X2>1` 类异常”的矛盾。该矛盾的解法：

- stage evidence 中记录的 bounds **全部合法**（这也解释了为什么此前无法在记录数据里找到超界值）；
- projector 抛出的不是“源数据出界”，而是**重建算术 `X1+(X2−X1)` 在 double 下 > 1**；
- 因此无需、也不应通过修正上游数据（fusion / canonical / normalizer）或放宽元素合法性来修复。

## 6. Frame-wide Impact（帧级影响）

帧 `seq24` / `seq25`（Display child）：

- `candidates=38`，`semanticAdmission=0`（唯一两个 DROP 帧；其余 15 帧 admitted ≥ 8）；
- 整帧被 `SemanticCapabilityEnvironment.ObserveAsync` catch 后置为 `AdmittedSemanticEvidenceSnapshot.Empty`；
- 后果链：`admission=[]` → `viewport exploration exhausted: source-seq=…; no new admitted navigation occurrence`
  → `Source normalization is unresolved; completeness cannot be proven.` → run-1 terminal `Failed`。

关键 observation：**单个 occurrence（index 0，第一个元素）使整帧 38 个候选全部被丢弃**。
这与先例 PER_OCCURRENCE_SEMANTIC_FAULT_CONTAINMENT（Vision 零宽防护）的教训同构：
一个边界元素的 Geometry fact 不应让整个 observation 的 admitted 证据归零。

## 7. Minimal Repair Candidate（诊断建议，未实施）

最小修复点：`SemanticObservationFactProjector.Normalize`（第 111–112 行）——在**提升到 double 之后**再做减法，
消除 float32 减法舍入：

```csharp
// 现状：float32 减法先做，后提升 double → 1.0000000063 > 1 抛异常
private static SemanticNormalizedBounds Normalize(ElementBounds bounds) =>
    new(bounds.X1, bounds.Y1, bounds.X2 - bounds.X1, bounds.Y2 - bounds.Y1);

// 候选：先提升，后 double 减法 → left + width == 1.0 精确通过
private static SemanticNormalizedBounds Normalize(ElementBounds bounds) =>
    new((double)bounds.X1, (double)bounds.Y1, (double)bounds.X2 - bounds.X1, (double)bounds.Y2 - bounds.Y1);
```

数值验证（本文档 §4 同一输入）：`(double)1.0f − (double)0.0027779999654740095 = 0.9972220000345260`，
`left + width = 0.0027779999654740095 + 0.9972220000345260 = 1.0` 精确 ≤ 1 → 通过。

该候选：

- 不引入 clamp / epsilon 魔术值（不动 `SemanticNormalizedBounds` 不变量本身）；
- 不改 fusion / canonical / normalizer / Pattern-5 / completeness；
- 保留 fail-closed：真正非法的 bounds（`X2<X1`、`X2>1`、负宽高）仍被同一 ctor 拒绝；
- 覆盖同源 second site（`AddStructuredFacts` 第 98 行共用同一 `Normalize`）。

评审建议：repair gate 中随修复附带两条回归测试——（1）`X1=0.002778f, X2=1.0f` 的
full-width 元素不再抛异常；（2）`X2=1.05f` 真非法值仍 fail-closed。

## 8. Phase 2.6 Next Gate

1. **`SEMANTIC_PROJECTION_BOUNDS_REPAIR_GATE`**（待 Human 授权；本 gate 不实施）：
   应用 §7 最小候选 + 两条回归测试 → `dotnet build/test` + 机械检查 → 真机
   re-run（settingscampaign）验证 seq24/25 不再整帧 DROP。
2. 修复后重新进入 Display child admission 链路，继续 `runtime-iterative-full-traversal-acceptance`
   的下一帧验证（anchor-adjacent 修复已原地待命，互不干扰）。
3. Phase 2.6 在修复 gate 完成并复验前维持 **STOPPED**。

## 9. Artifacts

| Artifact | SHA-256 |
|---|---|
| `/tmp/p26-normalization-gate-repair-stage.json` | `cbe92875…`（repair run-1 stage evidence，seq24/25 全层 bounds 与 admission=0 的权威来源） |
| `/tmp/p26-normalization-gate-repair-frames.json` | `133fa9bb…`（repair run-1 frames，elements[0]=Display 与 38 候选） |
| 真机 run-1 运行日志 | `[semantic-diagnostic] seq=24/25 stage=project FAILED type=ArgumentOutOfRangeException … Bounds must fit the normalized frame. (Parameter 'width')` |

无 production 代码改动；本 gate 仅证据记录。