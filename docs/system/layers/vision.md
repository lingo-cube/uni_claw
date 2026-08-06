# Layers — Vision

> **Tier 3 · Layers**: Vision 层规格书。改 LocalVisionProvider / Python vision server / label-mapping 配置时更新。
> 状态: e2e-dedup-vision-quality tasks 1-6 完成 (V1-V4 后处理已落地)
> 源码: `src/UniClaw.LocalVisionProvider/` (C# 提取管线) + `tools/local_vision/` (Python server: YOLO + OCR)
> 配置: `label-mapping.json` (YOLO label → MenuItemType) + `integration.config.json` (模型路径, OCR 后端)
> 约束: → constitution C-1 (枚举锁定), D-198 (RapidOCR 默认后端), `docs/system/local-vision-environment-consolidation.md`
> 定位: **感知输入层** — 为 Traversal 提供 `PageAnalysis` 帧缓冲 (FSM 外部输入, 不参与状态决策)

---

## 1. 管线 (Pipeline)

```
screenshot (RGB) → YOLO detect → bbox[] → per-bbox OCR → ItemDto[] → MenuItem[] → PageAnalysis
                                    ↑ label-mapping.json          ↑ V1-V4 后处理
```

| 阶段 | 组件 | 产出 |
|------|------|------|
| 目标检测 | Deki-Yolo (`android_ui_detection_yolov8/best.pt`, 21 标签) | bbox[] + label |
| label 映射 | `label-mapping.json` → MenuItemType | 类型标注 |
| OCR | RapidOCR (D-198), **按 bbox 独立** (V3, 不跨 bbox 拼接) | 文本 |
| 聚合 | LocalVisionProvider.cs item 提取 | ItemDto[] |
| 后处理 | V1-V4 (见 §3) | MenuItem[] |
| 页面语义 | popup 检测 + ANR 检测 + scroll 判定 | PageAnalysis (含 PageFingerprint 输入) |

## 2. Type Inventory

### 映射 (YOLO 21 标签 → MenuItemType)

Deki-Yolo 21 标签经 label-mapping.json 映射到 `MenuItemType` (Domain 锁定, → C-1)。交互式过滤策略见 deki-yolo-label-mapping 决策 (2026-08-04): text_block 保留决策 — 保留为低优先级导航候选, 不删除。

### 核心类型

| 类型 | 所在 | 用途 |
|------|------|------|
| `PageAnalysis` | Domain.Models.Content | 帧缓冲 — Items + IsPopup + HasScroll/IsEndOfList + PageFingerprint 输入 |
| `MenuItem` | Domain.Models.Content | 提取后的 UI 元素 (Type/Name/Coordinate) |
| `ItemDto` | LocalVisionProvider | 中间产物 — YOLO bbox + OCR 文本聚合 |
| `PageFingerprint` | TraversalEngine 计算属性 | (Type,Name) 排序多重集哈希 — 见 fingerprint-stability PRD-pre |

## 3. 后处理 V1-V4 (e2e-dedup-vision-quality)

| # | 修复 | 内容 | 状态 |
|---|------|------|------|
| V1 | 同排 item 合并 | Y 坐标去重 | ✅ 已落地 |
| V2 | 副标题类型降级 | Y 差 < 0.035 → text (避免误标 menuItem) | ✅ 已落地 |
| V3 | OCR 按 bbox 独立 | 不跨 bbox 拼接文本 | ✅ 已落地 |
| V4 | 文本归一化 | 折叠空格/标点 — 仅用于 identity key, 不改变显示文本 | ✅ 已落地 |

**V4 归属注意**: `NormalizeTextForIdentity` 当前在 Vision 项目内, 引擎无法引用 — 指纹稳定 PRD-pre P0a 计划移入 Core (待拍板)。

## 4. 模型与运行环境

| 项 | 值 |
|----|----|
| YOLO 模型 | Deki-Yolo (`android_ui_detection_yolov8/best.pt`), 21 标签 |
| OCR 后端 | RapidOCR (D-198 默认) |
| Python 运行时 | `.venv-local-vision` (Python 3.11) — 见 local-vision-runtime |

## 5. 已知缺口 (设计提案状态)

- **指纹稳定性** (fingerprint-stability-dedup PRD-pre): 类型白名单 + 归一化接入引擎指纹 + Node ID 归一化 — **待拍板** (P0a/P0b)
- **搜索框 type 波动 / Accessibility ResolveTextTarget / excludePatterns 死配置**: e2e-dedup-vision-quality Non-Goals, 未排期
