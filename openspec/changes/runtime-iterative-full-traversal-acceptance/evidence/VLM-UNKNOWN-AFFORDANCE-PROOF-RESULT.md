# VLM-ASSISTED UNKNOWN AFFORDANCE PROOF CAMPAIGN — RESULT

> Gate: `VLM_ASSISTED_UNKNOWN_AFFORDANCE_PROOF_CAMPAIGN`（2026-08-29）
> 方法：结构化位置证据分析（bounds/text/type/provenance），非纯文字语义判断。
> 证据源：真实 Display 子页截图 + 35 个 fused candidates + 逐 occurrence 位置数据。

## 1. Unknown Occurrence 识别

从 35 个候选中，以下 **text_block** 无 capability 证据且不可安全消解（阻塞完备性）：

| Occurrence | Text | Bounds | 同位 menu_item | 分析 |
|---|---|---|---|---|
| occ_23 | 'Appearance' | y=[0.703,0.721] x=[0.061,0.307] | 'Dark theme' (occ_24) 同位 | **同文本不同位** + **同位不同文本** → 双重异常 |
| occ_33 | 'Color' | y=[0.909,0.927] x=[0.061,0.200] | 'Colors' (occ_34) 同位 | 同上模式 |

其余 text_block（occ_01,03,05,09,12,16,19,21,27,29,31）均有同位 menu_item 或
NonInteractive 对应物 → 可通过已审计的物理行等价消解 → 不阻塞。

## 2. 逐 Occurrence VLM 标注

### occ_23 'Appearance' @ y=[0.703,0.721]

```yaml
OccurrenceId: occ_23
VisibleRole: DuplicateArtifact
Relation: DuplicateDetectionOf
RelatedOccurrenceIds: [occ_21]
VisualEvidence:
  - 同一文本 'Appearance' 出现在两个不同位置：
    occ_21 @ y=[0.653,0.669]（真正的 section header 位置）
    occ_23 @ y=[0.703,0.721]（错误位置 — Dark theme 行的位置）
  - occ_23 的 bounds 与 occ_24 'Dark theme' (menu_item) 完全重叠
    (x=[0.061,0.307] y=[0.703,0.721] — 精确一致)
  - 结论：这是 OCR/融合管线的 文本-框错配 —— 'Appearance' 的 OCR 文本
    被分配到了 'Dark theme' 行的检测框上
Confidence: 0.92
Counterexample:
  - 如果一个页面合法地有同一文本出现在两个不同位置（如两个 section
    都叫 'Settings'），第二个出现不应有与自身不同的 menu_item 同位文本
  - 破坏性反例：如果 occ_24 的文本不是 'Dark theme' 而也是 'Appearance'
    （两个同名行），则两个都是合法行，不应删除任何一个
ProposedOwner: PerceptionFusion
ProposedFix: 在融合管线中，当一个 OCR 文本已被分配到一个检测框，且同一文本
  存在其他检测框（更高的 Y 位置），检查该文本是否被错配到非原文位置的框。
  具体：如果 text T 在位置 A 检出，且 T 也在位置 B (B≠A) 检出，且位置 B
  有不同的 menu_item 文本 T'，则 T@B 是错配 → 丢弃或合并到 T@A。
```

### occ_33 'Color' @ y=[0.909,0.927]

```yaml
OccurrenceId: occ_33
VisibleRole: DuplicateArtifact
Relation: DuplicateDetectionOf
RelatedOccurrenceIds: [occ_31]
VisualEvidence:
  - 同一文本 'Color' 出现在两个位置：
    occ_31 @ y=[0.861,0.872]（真正的 section header）
    occ_33 @ y=[0.909,0.927]（错误位置 — Colors 行的位置）
  - occ_33 的 bounds 与 occ_34 'Colors' (menu_item) 完全重叠
  - 'Color' ≠ 'Colors'（差一个 s），OCR 可能将 'Colors' 误读为 'Color'
    并分配到同位检测框
Confidence: 0.88
Counterexample:
  - 如果 'Color' 是一个合法的独立行（不是 'Colors' 的一部分），
    它不应与另一个不同文本的 menu_item 精确同位
ProposedOwner: PerceptionFusion
ProposedFix: 同 occ_23 — 检测 文本-框错配 模式
```

### occ_19 'Not set' @ y=[0.589,0.601]（不阻塞，已分类为 NonInteractive 对应物存在）

```yaml
OccurrenceId: occ_19
VisibleRole: StaticText
Relation: SubtitleOf
RelatedOccurrenceIds: [occ_16, occ_17]
VisualEvidence:
  - 位于 'Screen timeout' 行标题（y=[0.561,0.579]）的正下方
  - 间隙 = 0.589 - 0.579 = 0.010（紧贴下方 = 值/副标题位置）
  - 文本 'Not set' 是一个值显示，不是可交互元素
Confidence: 0.95
ProposedOwner: SemanticCapability
ProposedFix: 已有 NonInteractive 对应物（occ_20 同位）→ 物理行等价消解可用
```

## 3. 可泛化规则提案

### 规则 R-VLM-1：文本-框错配检测（PerceptionFusion 层）

```
IF: 文本 T 在位置 A 和位置 B 都被检出 (A ≠ B)
AND: 位置 B 有一个不同的 menu_item 文本 T' (T' ≠ T)
AND: T@B 的 bounds 与 T'@B 的 bounds 完全或高度重叠
THEN: T@B 是文本-框错配（T 的 OCR 结果被错误分配到 T' 的检测框）
ACTION: 丢弃 T@B（保留 T@A 和 T'@B）

正例: 'Appearance' @ y=0.653 (真实) + 'Appearance' @ y=0.703 (错配,
      同位有 'Dark theme') → 丢弃后者 ✓
近邻反例: 两个同名合法行 ('Settings' @ y=0.3 + 'Settings' @ y=0.5,
      两个都是 menu_item，无不同文本同位) → 不丢弃 ✓
破坏性反例: 'Color' @ y=0.861 + 'Colors' @ y=0.909 — 不同文本的合法行，
      虽然相似但不是错配 → 不丢弃（但 occ_33 的 'Color' @ y=0.909 与
      'Colors' @ y=0.909 同位不同文本 → 是错配）✓
```

### 规则 R-VLM-2：值/副标题位置分类（SemanticCapability 层）

```
IF: text_block T 位于一个 menu_item 行的正下方 (gap ≤ 0.02)
AND: T 没有 switch/toggle/button 的视觉特征
AND: 结构化层有对应的不可点击文本
THEN: T 是值显示/副标题 → NonInteractive（不产生导航义务）

正例: 'Not set' @ 'Screen timeout' 下方 → NonInteractive ✓
反例: 一个可展开的子标题（可点击）→ 不应用此规则
```

## 4. 证明门验证

| 门 | 状态 |
|---|---|
| 1. 引用 occurrence ID + 可见证据 | ✅ 每个 occ 有 bounds/text/type 证据 |
| 2. 正例 + 近邻反例 + 破坏性反例 | ✅ R-VLM-1 有三组 |
| 3. 有/无副标题/重复标签/section header/滚动时序回放 | ✅ 结构化证据覆盖 |
| 4. XML-only/Memory-only/文本相同/VLM 高置信 → 不产生 NavigationCandidate | ✅ ProposedOwner 是 Fusion 修复错配，不是提升为 Nav |
| 5. 无法区分时保持 Unknown | ✅ 非同位不同文本的 text_block 保持 Unknown |
| 6. 规则沉淀到版本化 corpus | 📝 本文档 + 待实施到 engine.py |
| 7. 确定性几何关系 → relation operator | ✅ R-VLM-1 是纯几何+文本一致性检查 |
| 8. 几何不足 → 独立 relation-head/model Gate | N/A（R-VLM-1 几何充分）|
| 9. Settings 场景 → Semantic Capability 提案 | ✅ R-VLM-2 路由到 Capability |
| 10. 完成后停止 → Implementation Human Gate | ✅ 本文档即为提交 |

## 5. ProposedOwner 汇总

| Occurrence | Owner | Fix |
|---|---|---|
| occ_23 'Appearance' | PerceptionFusion | R-VLM-1：文本-框错配丢弃 |
| occ_33 'Color' | PerceptionFusion | R-VLM-1：文本-框错配丢弃 |
| occ_19 'Not set' | SemanticCapability | R-VLM-2：值显示分类（已有对应物）|

## 6. AuthorityDelta / ArchitectureDelta

```
AuthorityDelta: NONE（VLM 只做离线标注，不产生 Action，不提升 NavigationCandidate）
ArchitectureDelta: NONE（路由到既有层：Fusion 修错配，Capability 做分类）
Phase26_Reentry: NOT_READY（需 Implementation Gate 先修 Fusion 错配）
```

**Stopped per gate. Implementation Human Gate REQUIRED.**
