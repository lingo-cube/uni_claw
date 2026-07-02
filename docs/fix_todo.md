# Fix TODO：待修正问题清单

> 记录跨 Phase 的已知问题，标注来源、影响和计划修正时机。

---

## F-1: AI 层 PageAnalysis/PopupInfo 与 Domain 层重复定义

**发现日期**: 2026-07-02
**来源**: Phase 1.1 提案审查（05 §3.3、Phase 1 design R-4）
**计划修正**: Phase 2（Graph/AI 层重构时一并处理）

### 问题

C# 存在两个同名但不同结构的 PageAnalysis 和 PopupInfo 类型：

| 版本 | 位置 | 来源 | 对齐 Python |
|------|------|------|------------|
| Domain 完整版 | `Domain/Models/Content/PageAnalysisRecords.cs` | Phase 1 从 `content_models.py` 逐行搬运 | ✅ 1:1 |
| AI 简化版 | `AI/IAIStrategyAdvisor.cs:143-157` | C# 重写时自造 | ❌ Python 不存在此版本 |

**Domain 版 PageAnalysis**（12 字段）：Level1Dir, Level1Menus, Level2Dir, Level2Menus, CurrentPath, Items, IsPopup, PopupInfo, CloseButton, BackButton, HasScroll, IsEndOfList

**AI 简化版 PageAnalysis**（3 字段）：FlattenedScreen, Path, PopupInfo(AI版)

**Domain 版 PopupInfo**：Title?, Content?, CloseButton?(Coordinate)

**AI 简化版 PopupInfo**：Detected(bool), CloseButton?((double,double) tuple), Message?(string)

### 根因

C# 重写（commit `a9a831a`）为 `IAIStrategyAdvisor` 接口定义参数类型时，没有引用已有的 Domain 版 PageAnalysis，而是自造了一个简化版。Python main 分支**没有独立的 AI 接口文件**（`ai_strategy_advisor.py` 不存在），AI 简化版是 C# 独创设计，无 Python 基准。

### 影响

1. **违反单一源原则**（Phase 1 design R-4）：两个同名类型并存，调用方需区分命名空间 `UniClaw.Core.Domain.Models.Content.PageAnalysis` vs `UniClaw.Core.AI.PageAnalysis`，IDE 补全容易混淆。
2. **AI 层接口与 Domain 层断裂**：`IAIStrategyAdvisor` 5 个方法签名都使用 AI 简化版 PageAnalysis，无法直接接收 Domain 版数据，需要手动转换。
3. **PopupInfo 字段不兼容**：AI 版的 `Detected`/`Message` 与 Domain 版的 `Title`/`Content` 语义不同，无法互换。

### 修正方向

Phase 2 统一到 Domain 版，两条可选路径：

| 路径 | 做法 | 优劣 |
|------|------|------|
| **A: 删除 AI 简化版 + 适配层** | 删除 `IAIStrategyAdvisor.cs` 内的 PageAnalysis/PopupInfo 定义；`IAIStrategyAdvisor` 方法签名改为使用 Domain 版 PageAnalysis；AI 实现层内部做 FlattenedScreen→Domain PageAnalysis 的构造适配 | ✅ 单一源，无平行类型。❌ AI 层需要构造完整 PageAnalysis（12 字段），当前只产出 FlattenedScreen |
| **B: 保留 AI 简化版但改名消除歧义** | 将 AI 版 PageAnalysis 重命名为 `AIScreenSnapshot`，PopupInfo 重命名为 `AIPopupSnapshot`，与 Domain 版不再同名；`IAIStrategyAdvisor` 签名使用新名 | ✅ 改动小，不破坏 AI 层现有实现。❌ 仍有两套平行类型，只是不再混淆命名 |

推荐路径 A——但需要 Phase 2 先设计 AI→Domain 的数据构造管道（AI 输出 FlattenedScreen + 分析结果 → 组装为 Domain PageAnalysis），这是 Phase 2 AI 层重构的核心工作之一。

### 相关文档

- [05-model-relationship-map.md §3.3](refactor/05-model-relationship-map.md) — 上层→Domain 桥接点对比
- [Phase 1 design R-4](../openspec/changes/archive/2026-07-01-phase1-domain-core-models/design.md) — PageAnalysis/PopupInfo 双存在风险
- Python `src/models/content_models.py` — PageAnalysis/PopupInfo 原始定义
