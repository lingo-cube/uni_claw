# Phase 2.6 完整数据管线流程图（2026-08-29）

> 目的：全局视图，防止"唤醒后遗漏"或"修一层忘一层"。
> 每一步标注：数据形态 / 已部署的修复 / 可能的故障点 / 修复间的交互。

## 全景

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Python 感知层                                │
│                                                                     │
│  截图 (1080×2400 PNG)                                              │
│    ↓                                                                │
│  YOLO 检测 [置信度 ≥ 0.20] ← S2fix7: 从 0.35 降到 0.20             │
│    → 输出: 检测框列表 [{label, confidence, bounds}]                  │
│    故障点: 漏检（中间行消失）→ 已通过降阈值缓解                      │
│                                                                     │
│  OCR (RapidOCR)                                                     │
│    → 输出: 文本令牌列表 [{text, confidence, bounds}]                 │
│    故障点: 错字（"internet"→"intemet"）→ 由稳定化器处理              │
│                                                                     │
│  融合管线 (fuse_evidence)                                           │
│    ├─ 启发式合并（行组合、图标对齐等）                                │
│    ├─ S1/S2 算子管线:                                                │
│    │   uniform-list-row-grouping (≥4锚点 → 组合为 menu_item)        │
│    │   → row-relation-head (<4锚点 → 从原始几何组合)                │
│    │     ├─ S2fix4: topmost head 规则（标题在上，非最宽）            │
│    │     └─ S2fix2: verifier 标题列豁免                              │
│    │   spacing-verifier (几何验证)                                   │
│    │   text-relation-check (仅 veto/降置信) [S4]                     │
│    │   structured-corroboration (仅佐证) [S4]                        │
│    ├─ S2fix3: 同行去重（同文本非导航，垂直重叠 ≥ 半高）               │
│    ├─ S2fix5: 相邻行去重（gap ≤ 较短者高度）                         │
│    ├─ S2fix6: 列提升（text_block 在菜单列 → menu_item）              │
│    └─ 行稳定化 ← 【已启用 stabilize=True】                            │
│         ├─ 三元组 Jaccard 相似度匹配                                 │
│         ├─ 邻居上下文锚定（上面是谁、下面是谁）                        │
│         └─ 跨帧缓存（canonical_text 保留首次清晰观测）               │
│           故障点: 两次不同的 OCR 错误可能产生两个 canonical           │
│           → 依赖上下文锚定兜底                                       │
│                                                                     │
│  输出: candidates [{text, type, bounds, confidence}]                 │
│    text 应该跨帧一致（同一行 = 同一 canonical text）                  │
└─────────────────────────┬───────────────────────────────────────────┘
                          ↓ JSON over UDS socket
┌─────────────────────────────────────────────────────────────────────┐
│                          C# Runtime 层                               │
│                                                                     │
│  Observation 构建                                                    │
│    → Elements + StructuredElements + Sources                        │
│                                                                     │
│  SemanticCapabilityEnvironment                                      │
│    → SettingsSemanticCapability 分类:                                │
│      "Settings" 标题 → settings.container                           │
│      可点击行 → settings.preference-row (NavigationCandidate)       │
│      搜索栏 → settings.search-role (LocalControl)                   │
│      返回按钮 → settings.navigate-up (ParentReturnControl)          │
│      toggle 形状 → preference-row (LocalControl)                     │
│    → AdmittedSemanticEvidence                                       │
│                                                                     │
│  InteractionAffordanceAnalyzer                                      │
│    → 每个 element 得到 Classification:                              │
│      NavigationCandidate / LocalControl / NonInteractive /          │
│      ParentReturnControl / Unknown                                  │
│    签名 = Text|PerceptionType                                       │
│    ⚠️ 如果 Python 稳定化失败 → 签名不稳定 → 下游断链                  │
│                                                                     │
│  SettingsStrategyBinding (harness-local)                            │
│    ├─ R1: 结构标题回退（无 collapsing_toolbar 时用最左列文本）       │
│    ├─ R2: 标题排除（不作为导航目标）                                  │
│    └─ 根页回退（无搜索栏 + 无返回按钮 = 还在根页）                    │
│                                                                     │
│  Quiescence Admission Gate                                          │
│    → 滚动后连续观测确认：                                            │
│      count 一致 + 签名有序一致 + 位置漂移有界 + 无同帧重复            │
│      [freshness 检查: SequenceNumber 严格递增]                       │
│    → 只接纳最后确认帧；预算耗尽 → RunFailed（带分类详情）            │
│                                                                     │
│  SourceEquivalenceNormalizer                                        │
│    三级回退:                                                        │
│    1. 严格 suffix(union) ↔ prefix(window) 唯一重叠                  │
│    2. 边界容忍: 跳过窗口首行（顶部截断行）                            │
│       [边界容忍: 只跳首行有数学意义，跳尾行不可能更优]               │
│    3. 锚定合并: [缩窄版]                                             │
│       条件: ≥1 锚点 + ≥1 新插入行 + 前向排序                         │
│       (拒绝: 纯重复/回滚视图 → 保持 Unresolved)                     │
│       ⚠️ 已知问题: 插入顺序可能错误（多行插入时）                     │
│    全部失败 → Unresolved → "Source normalization is unresolved"     │
│                                                                     │
│  Container Completeness                                             │
│    → inventory 完备性证明 → 分支遍历 → GoalEvidence → 终态           │
└─────────────────────────────────────────────────────────────────────┘
```

## 已部署修复清单（按管线顺序）

| # | 修复 | 位置 | 解决的问题 | 状态 |
|---|---|---|---|---|
| 1 | S1/S2 算子框架 | Python fusion | 同行重复框、副标题独立成菜单 | ✅ |
| 2 | S2fix3 同行去重 | Python fusion | 同文本非导航同行重复 | ✅ |
| 3 | S2fix4 topmost head | Python relation-head | 副标题比标题宽时误选 | ✅ |
| 4 | S2fix5 相邻行去重 | Python fusion | 标题+影子紧挨着 | ✅ |
| 5 | S2fix6 列提升 | Python fusion | text_block 类型不稳定 | ✅ |
| 6 | 行稳定化 | Python fusion (stabilize=True) | OCR 文本跨帧不一致 | ✅ 已启用 |
| 7 | S2fix7 置信度降低 | YOLO config (0.35→0.20) | 中间行漏检 | ✅ |
| 8 | Quiescence gate | C# Agent.OpenWorld | 滚动后瞬态帧被当稳定世界 | ✅ 毕业 |
| 9 | R1 标题回退 | C# SettingsStrategyBinding | 无 collapsing_toolbar 子页 | ✅ |
| 10 | R2 标题排除 | C# SettingsStrategyBinding | 标题不应作为导航目标 | ✅ |
| 11 | 根页回退 | C# SettingsStrategyBinding | 滚动后搜索栏不可见 | ✅ |
| 12 | 边界容忍 | C# SourceEquivalenceNormalizer | 顶部截断行断链 | ✅ |
| 13 | 锚定合并 | C# SourceEquivalenceNormalizer | 中间漏检行恢复 | ✅ (插入顺序待修) |
| 14 | InitialStep 0.4→0.6 | C# Agent.OpenWorld | 第一次滚动太小 | ✅ |

## ⚠️ 已知未解决问题

| # | 问题 | 影响 | 优先级 |
|---|---|---|---|
| A | 锚定合并插入顺序错误 | 多行插入时顺序可能反 | 高（当前阻塞点） |
| B | 稳定化器两次不同 OCR 错误 | 可能产生两个 canonical | 中（上下文锚定兜底） |
| C | C# 签名仍含 PerceptionType | 类型不稳定时签名不同 | 低（列提升已缓解） |

## 唤醒检查清单

每次从 goal round / worker 完成唤醒时，按此顺序检查：

```
□ 所有修复是否仍启用？（特别是 stabilize=True、置信度 0.20、步长 0.6）
□ 上次跑到了哪里？（terminal reason 是什么）
□ 上次的 union 里有什么？（哪些行已识别、哪些还缺）
□ 有没有新 worker 完成？如果有，验收了吗？
□ 全量测试还绿吗？（dotnet test + pytest）
□ candidate receipt 是否需要刷新？（感知代码变了吗）
□ 模拟器还活着吗？
```
