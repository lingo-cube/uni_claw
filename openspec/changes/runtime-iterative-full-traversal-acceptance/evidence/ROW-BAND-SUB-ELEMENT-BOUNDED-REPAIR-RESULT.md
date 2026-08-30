# ROW-BAND-SUB-ELEMENT_BOUNDED_REPAIR_RESULT（Option A 有界收尾）

> 前置：`REAL_CONTAINER_EXIT_CAUSE_EVIDENCE_COLLECTION_GATE` 后，Leader 选 **A. 有界收尾**：
> 只修最后一个确定性阻塞（'Not set'/'Will never' 类无同行 menu_item 的副文本行 → child 完整性 Unknown），
> 跑 fresh real 后由 Leader 裁决。Phase 2.6 维持 STOPPED。

## 0. 一句话

修复达成：child（Display）**discovery epoch 首次完整通过——`inventory complete: sources=17, unresolved=0, seq=[22,25,28,31]`**；
'Not set'/'Will never' 类 Unknown 完整性阻塞消失。fresh real（runF）terminal 前移到**下一层**
post-completeness 一致性（fresh 帧出现 frozen 之外的 nav 签名，fail-closed 正确）——本次有界目标完成，
裁决点到达。

## 1. Minimal Diff

| 文件 | 内容 |
|---|---|
| `src/UniClaw.Semantic.Settings/SettingsSemanticCapability.cs`（+~90） | 新增 **ROW_BAND_SUB_ELEMENT 谓词**（`IsRowBandSubElement`）：occurrence 级（复用 `ViewOf`），text_block 满足 ①完全含于唯一 menu_item 行带（且子高度 ≤ 0.8×行高——等大重叠框排除）或 ②同列、正下方 0≤隙≤0.8×行高（'Will never' 实测隙 0.010625 vs P7 0.0105 的量化为 0.7% 抖动）、异文本；守卫：非 menu_item、非 toggle 形、无自身文本的 XML 同行、**异文本于所有 menu_item 行**（同文本属 P5 域，歧义保持 fail-closed）→ 恰一候选行 → NonInteractive supporting。P5/P7 语义零放宽 |
| `tests/…/ExternalSettingsSemanticCapabilityTests.cs`（+6 测试）| Buyer A（'Not set' 包含）+ Buyer B（'Will never' 下方）+ 反例（无邻近行 / 双行歧义 / toggle 形 / XML 同文本 / 缺几何或文本）+ 既有 P5 反例保持 |

## 2. RED→GREEN

- **RED**（未改生产）：2 个 buyer FAIL（'Not set'/'Will never' 无 NonInteractive）——与实帧 Unknown 一致。
- **GREEN**：**41/41**（含收紧过程中的两处反例回归：等大异文框不误吞（Pattern-5 C）、同文本歧义对不受新规则干涉（P5 E））。

## 3. Deterministic / Fresh Real

- 全量 C# 确定性套件：**2351 passed / 5 failed（=既有环境性 CORR_HOST×3 + Capstone_RealEmulator + ExternalBoundary_RealDevice，零新增）**。
- `git diff --check` CLEAN。
- **fresh real（runF）**：
  - root：`inventory complete: sources=16/17, unresolved=0` ✓；
  - **child：`inventory complete: sources=17, unresolved=0, seq=[22,25,28,31]` + `branch inventory: 16 anchors`（首次 child discovery epoch 全绿）**；
  - terminal = `Post-completeness fresh evidence INVALIDATED: 'nav:14' does not resolve to any proven frozen logical source` —— **下一层**（post-completeness 一致性；fresh 帧含 frozen 之外的 nav 签名 → fail-closed 正确）。

## 4. 裁决点（Leader）

本次有界目标（'Not set'/'Will never' 阻塞）**已达成并验证**；尚未到 terminal=Completed——现被
post-completeness 一致性层阻断（新能力层，属既有机制的正确 fail-closed；需按新 gate 诊断 'nav:14'
是真实新源还是已知源的变体签名【如已登记 StableKey 漂移、全宽/局部宽工具栏角色变体】）。

由 Leader 裁决：
- **A1 继续**：开下一 gate（post-completeness 一致性诊断/修复——'nav:14' 归类）；
- **A2 接受当前证据并搁置**：Phase 2.6 记为"child epoch 已通过、terminal 停在 post-completeness
  一致性（fail-closed 正确），残余逐层编目"。证据已足够厚。

## 5. Phase 2.6 readiness / 边界

- **Phase 2.6 维持 STOPPED**（裁决前）。
- 零触碰：completeness 机制、P5/P7、`SourceGroundingNormalizer`/analyzer、Fusion、OCR/ICON/Safety、
  quiescence budget/retry/sleep、swipe 参数、左出 predicate。