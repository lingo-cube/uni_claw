# 指纹稳定性去重增强 — PRD-pre

> 状态: 需求提炼阶段 (Battle #5 输出)
> 来源: 用户提案 × fsm-analyzer 源码审计 × shadow-fsm-analyzer 对抗评审
> 日期: 2026-08-05
> 前置: D-G12 + V1-V4 已落地 (e2e-dedup-vision-quality, tasks 1-6 [x])

---

## 0. 现状基线

### 0.1 已落地的防御

| 机制 | 层级 | 状态 |
|------|------|------|
| D-G12 目的地去重 | 引擎侧 (`_childDestinations`, per-parent 指纹集) | ✅ 已落地 (L6-1/L6-2 测试通过) |
| V1 同排 item 合并 | Vision 侧 (Y 坐标去重) | ✅ 已落地 |
| V2 副标题类型降级 | Vision 侧 (Y 差 < 0.035 → text) | ✅ 已落地 |
| V3 OCR 按 bbox 独立 | Vision 侧 (不跨 bbox 拼接) | ✅ 已落地 |
| V4 文本归一化 | Vision 侧 (折叠空格/标点, 仅用于 identity key) | ✅ 已落地 |
| `_generatedPairs` | 引擎侧 (fingerprint, childName) dedup, 跨 Invalidate 持久 | ✅ 已有 (D-3) |
| `VisitedNodes` | 引擎侧 (NodeId 维度) | ✅ 已有 |

### 0.2 已知漏洞 (源码锚定)

Battle #5 发现的 4 类穿透路径：

| # | 漏洞 | 根因 | 源码锚定 |
|---|------|------|---------|
| A | **NodeId 内嵌原始 OCR 文本** | 两处构造 `dyn_{template}_{itemText}_{parent}`，文本变体 = 新 NodeId = 全新"未访问"节点 | TemplateInstantiator.cs:58, TraversalEngine.cs:995 |
| B | **V4 归一化未接入引擎指纹** | `NormalizeTextForIdentity` 在 Vision 项目内，引擎无法引用 | LocalVisionProvider.cs:554-585 |
| C | **全量 item 参与 PageFingerprint** | 非导航 item (副标题/Switch/dynamic text) 抖动污染页面身份 | TraversalEngine.cs:1956-1959 |
| D | **D-G12 目的地指纹 = 全量 item hash** | 同页 OCR 抖动 → 不同指纹 → D-G12 漏判 | TraversalEngine.cs:383-384 |

**结论**: D-G12 解决了"两个不同 NodeId 导航到同一物理页"的检测问题，但**指纹本身的不稳定使得检测条件 (preFp != postFp 守卫 + 目的地指纹比对) 在抖动场景下失效。**

---

## 1. 问题精炼 (Battle #5 对用户方案的修正)

### 1.1 方案一 "Container 文本去重" 的修正

**原方案**: 新增 `ContainerIdentity { PageTitle, ImmutableSortedSet<MenuItemText>, TextHash }`，文本去重为主键，视觉指纹兜底。

**Battle 发现的 4 个问题**:

1. **增量被高估**。`PageAnalysis.PageFingerprint` 已内建 `(Type.ToLower, Name)` 排序多重集哈希——排序归一化已存在。方案一的真正增量不是"换个哈希公式"，而是**内容选择**（只哈希可导航类型的 item，排除动态文本）。

2. **PageTitle 无来源**。`PageAnalysis` 无 PageTitle 字段，Android 子页面常无标题栏。无来源字段参与身份哈希 = 引入新的不稳定源。

3. **动态文本是存在性前提**。Settings Switch "On"/"Off" 变化 → Name 变 → 指纹变 → D-G12 判重失效。方案必须回答"哪些文本是动态的、如何排除"，不能只写"动态元素排除规则"六个字。

4. **兜底裁决规则缺失**。"视觉指纹兜底"何时启用？三键冲突 (文本/视觉/位置) 以谁为准？

**修正后的方案一 = PageFingerprint v2**:
- 保留 `(Type, Name)` 结构 + 排序归一化 (复用现有公式)
- 新增: **类型白名单** — 只有可导航类型参与哈希 (排除 Switch/text/dynamic)
- 新增: **文本归一化** — 将 V4 `NormalizeTextForIdentity` 移入引擎侧, Name 输入归一化后再 hash
- PageTitle **不参与哈希** (仅作为调试字段)
- 不引入新 `ContainerIdentity` 类型 — 直接改进 `PageFingerprint` 公式

### 1.2 方案二 "Node ID 归一化" 的修正

**原方案**: `{element.Class}_{text}` 替代 `{pageFingerprint}_{elementIndex}`。

**Battle 发现的 2 个问题**:

1. **不解决页面级重复**。Node ID 稳定只保证"同一元素跨帧同 ID"，但"不同元素 (副标题 vs 主标题) → 同一物理页"的检测仍是 D-G12 的职责。方案二单独部署时的去重贡献≈0。

2. **消歧问题**。同名同类型元素 (Settings 中多个 Switch) 仍需稳定下标。文本+类型 ID 会碰撞。

**修正后的方案二**:
- `dyn_{template}_{NormalizedText}_{parent}` (两处构造点)
- 同名消歧: 使用稳定排序后下标 (按坐标 Y 排序, 已被 ScrollHandler 证明稳定)
- 兜底: 无文本时回退视觉指纹 (与方案一统一)，**不是**相对位置
- 三键收敛为两级裁决: 文本 → 视觉 → 保守不去重

### 1.3 两者关系的修正

**原方案**: 方案一为主 (P0), 方案二为辅 (P1)。

**修正**: 方案一 P0 内部再拆分:
- **P0a (不依赖跨帧)**: 文本归一化移入引擎 + 类型白名单 (独立可交付)
- **P0b (依赖跨帧 identity)**: 动态值排除 (需要方案二的跨帧关联能力)

方案二是方案一 P0b 的前置依赖，不是独立的"辅"。

---

## 2. 设计

### 2.1 PageFingerprint v2

```
// 当前 (TraversalEngine.cs:1956)
Items.Select(i => (i.Type.ToLowerInvariant(), i.Name ?? ""))
     .OrderBy(t => t.Item1).ThenBy(t => t.Item2)
     .Aggregate(17, (hash, t) => { foreach ch in t.Item1+t.Item2; hash = hash*31 + ch })

// v2 改动: 类型白名单 + 文本归一化
var navigableTypes = new[] { MenuItemType.MenuItem, MenuItemType.TextButton, ... };
Items.Where(i => navigableTypes.Contains(i.Type))          // ← 新增: 类型过滤
     .Select(i => (i.Type.ToLowerInvariant(), NormalizeTextForIdentity(i.Name)))  // ← 新增: 文本归一化
     .OrderBy(t => t.Item1).ThenBy(t => t.Item2)
     .Aggregate(17, ...)  // 不变
```

**影响面**: 指纹语义变化 → 需要迁移:
- L6-1/L6-2 fixture (D-G12 测试)
- `Fingerprint_Deterministic` / `Fingerprint_DifferentInput_DifferentHash` 等 5 个测试
- `CacheInvalidation_PreservesGeneratedPairs` (`_generatedPairs` 键包含 fingerprint)

**裁决原则** (嵌入公式设计): **dedup 只在高置信度判等时跳过——宁可多访问，不可漏访问。** 类型白名单宁可保守 (少排除)，不可激进 (误排除)。

### 2.2 Node ID 归一化

```
// 当前 (两处)
NodeId = $"dyn_{childTemplate}_{matchedItem.Text}_{parentNodeId}"

// 归一化
var stableText = NormalizeTextForIdentity(matchedItem.Text);
NodeId = string.IsNullOrEmpty(stableText)
    ? $"dyn_{childTemplate}_pos_{item.Coordinate.Y:F4}_{parentNodeId}"  // 兜底: 视觉位置
    : $"dyn_{childTemplate}_{stableText}_{parentNodeId}";
```

**同名消歧**: 同 parent 下同 stableText → 按 Y 坐标排序后追加 `_N` 后缀。

**影响面**: `dyn_` 格式断言仅 2 处 (AIIntentSimulationTests.cs:181, FixVerificationTests.cs:248)。

### 2.3 职责边界

```
Vision 侧 (已有 V1-V4, 不变):
  → 产出准确的 item 列表 (去重、类型正确、文本归一化 identity key)

引擎侧 (本次改动):
  → 类型白名单过滤 (PageFingerprint v2, 只用可导航类型)
  → 文本归一化接入引擎指纹
  → Node ID 归一化 (文本→视觉兜底)
  → D-G12 目的地去重 (不变, 但输入指纹更稳定 → 召回率提升)
```

---

## 3. 实施路径 (修正后)

| 优先级 | 内容 | 依赖 | 改动量 | 修复的漏洞 |
|--------|------|------|--------|-----------|
| **P0a** | V4 `NormalizeTextForIdentity` 移入 Core + 应用于 childName (:953) 与 NodeId 两处 (:58/:995) | 无 | ~50 行 | A: NodeId 文本变体穿透 (根因单点) |
| **P0b** | PageFingerprint 加**类型白名单** (只哈希可导航 MenuItemType) | 无 (MenuItemType 字段可得) | ~10 行 | C: 非导航 item 污染页面身份 |
| **P1** | PageFingerprint 输入归一化 (折叠空格/标点) + ItemFingerprint 同步 | P0a (NormalizeTextForIdentity 已在 Core) | ~10 行 | B: Vision 归一化未接入引擎 |
| **P2** | 身份指纹 vs 变化检测指纹分离 (identity = 归一化可导航 item 子集; change = 全量敏感) | P0b + P1 | ~50 行 | D: 副标题出现/消失导致的指纹抖动 (D-74 假导航, D-G12 漏判) |
| **P3** | 清理: 统一 PageFingerprint 重复实现 (删一留一); 移除 VisitedChildren 死结构; 修正过期注释 | 无 | ~20 行 | 死代码漂移 |

**P0a+P0b 可并行。总计 ~130 行生产代码 (14 文件) + ~90 行测试迁移。**

---

## 4. 风险与取舍

| 风险 | 影响 | 缓解 |
|------|------|------|
| 指纹语义变化 → 现有 fixture 失效 | L6/D-G12/Fingerprint 测试需迁移 | P0a 不碰指纹公式, P0b 才改; 分步迁移 |
| 类型白名单误排除可导航 item | 合法子树被跳过 (漏访问) | 保守原则: 白名单从宽, 只排除明确非导航类型 |
| 同名消歧 Y 排序不稳定 | 同 text 元素下标漂移 | 滚动后 Y 坐标变化已被 ScrollHandler 证明稳定; 同名元素 Y 差通常显著 |
| 纯图标页面 (无文本) 退化为视觉指纹 | 方案一的去重增益 = 0 | 可接受 — 图标页数量少, 视觉指纹兜底 |
| OCR 噪声级不稳定 (规则归一化覆盖不了) | 方案一/二均无效 | 这是 `ITextUnderstanding` (AI OCR 纠错) 的领域, 不在本次范围 |

---

## 5. Battle 验证

| 用户提案 | Battle 修正 | 判定 |
|---------|-----------|------|
| 方案一 ContainerIdentity 文本去重 | 简化为 PageFingerprint v2 (不改类型, 只加过滤+归一化) | ✅ 修正: 现有公式已有排序归一化, 增量在内容选择 |
| PageTitle 参与哈希 | 移除 — 无来源字段 | ✅ 修正: 引入新不稳定源 |
| 方案二 Node ID 归一化 | 保留, 但兜底统一为视觉指纹 | ✅ 修正: 两套兜底收敛 |
| "方案一为主 P0, 方案二为辅 P1" | P0a(归一化)+P0b(白名单) 并行, 方案二 = P0a 的 part | ✅ 修正: 方案二是 P0b 的前置 |
| 动态元素排除规则 | 拆为类型白名单 (P0b) + 动态值检测 (P2, 依赖跨帧) | ✅ 修正: 分步 |
| 整体改动量 ~55 行 | 修正为 ~130 行 (含测试迁移) | ✅ 修正: 原估算未含测试迁移 |

**6/6 用户提案要点经 Battle 修正后保留方向、精炼实现。**

---

## 6. 后续

1. **评审本 PRD-pre** → 确认 P0a/P0b 范围
2. **openspec:propose** → 生成 formal change (e2e-dedup-fingerprint-stability)
3. **Phase 1**: P0a+P0b 并行实施 (~60 行)
4. **Phase 2**: P1+P2 (指纹分离 + 归一化接入)
5. **E2E 验证**: tasks 7.1-7.3 (enumerate-settings-safely, 无副标题重复进入, 步数 ≤ 120)
