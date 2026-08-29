# PROJECT_LEADER_SETTINGS_STRUCTURAL_CHILD_TITLE_BAND_REPAIR_RESULT

## Gate

`SETTINGS_STRUCTURAL_CHILD_TITLE_BAND_REPAIR`（2026-08-29，UniFlow 执行记录）

## 1. First Divergence Point — CONFIRMED AND FIXED

`SettingsStrategyBinding.ResolveStructuralTitleElement` 把"首个 clickable row 上方的
所有独立左列文本带"都当作竞争页面标题 → Display 标题被 Brightness section header
否决 → 子页身份不解析 → settle 失败。

**修复（topmost-wins + nesting subordination）**：
- 候选按垂直重叠聚成多个带（gap > `TitleFallbackColumnTolerance` 分带）
- **顶部带**是唯一的标题决策者；更低带是 section header，不否决
- 顶部带内先合并同文本重复（保留最上出现）
- Y1 最小的元素 = 标题；其范围 [Y1,Y2] 内的其他 Y1 = 嵌套从属（caption）
- Y1 > 标题 Y2 的同级竞争 → null（fail-closed）

## 2. Leader Ruling During Implementation

Worker 发现 frozen spec 内部矛盾（acceptance b wallpaper vs acceptance d conflict）。
裁决：嵌套从属规则（caption Y1 ∈ 标题范围 → 从属不冲突；peer Y1 > 标题 Y2 → 冲突）。
三个场景全部满足。

## 3. Tests

| Suite | Result |
|---|---|
| SettingsStrategyBindingTests（含 8 个新 R3 验收） | **23/23** |
| OpenWorldPostActionSettleTests | **9/9** |
| OpenWorld（全部） | **100/100** |
| check-consistency.sh | ALL PASS |
| Full suite | 2272 pass / 9 fail（全部预存/环境类） |

新测试覆盖：Display+Brightness → SettingsSubpage(Display) · wallpaper 嵌套 caption 保持 ·
同文本 OCR 重复合并 · peer 竞争 → null · 无 Navigate up → 不走 fallback · 无标题 → null ·
多更低带全忽略 · 合并先于嵌套检查。

## 4. Settle Budget

`MaxPostActionSettleObservations` 撤回到冻结值 **3**（8 仅为绕过本缺陷的 workaround）。
SET2/SET4/SET6 脚本恢复 3 帧版本。9/9 settle 测试绿。

## 5. Real-Emulator Verification

**子页转场确认**（在此前 run 中验证）：
- seq 18-19：连续两帧 Navigate up 存在 + 页面解析为 SettingsSubpage(Display) ✓
- Agent 进入 Display 子页（11 个菜单项）✓
- 子页探索开始（滚动触发）✓

**Scrolled-title tolerance（R3 后续）**：子页滚动后标题带滚出画面 → 页面解析暂时
不可确定 → quiescence gate 继续观察（pending）而非立即 "left container"。前景变化
或解析到不同页面仍然是真正的离开（fail-closed）。

## 6. New First Divergence Point（子页探索推进后）

**非确定性 Unknown interaction affordances**：感知层在不同 run 中偶发产生不认识
的 text_block（不同 run 的 Unknown 集合不同）。这些元素阻塞完备性（completeness
check）。已部署的缓解：text_block 非阻塞默认 + StableKey 重复消解。剩余是感知
层的 run-to-run 非确定性 —— 需要跨 run 的一致性改进（非本 change 范围）。

## 7. Deltas

```
AuthorityDelta:     NONE（纯 harness-local binding 修复）
ArchitectureDelta:  NONE（同一 binding 文件，同一消费路径）
LifecycleDelta:     NONE（不自动毕业、不重入 Phase 2.6）
```

## 8. Status

```
TitleBandRepair:     COMPLETE（23/23 + 100/100 + ALL PASS）
DisplayChildResolve: PROVEN（真机连续帧解析 + settle PASS + 子页进入）
SettleBudgetRevert:  DONE（3，非 workaround）
NewFDP:              非确定性 Unknown affordances（感知层 run-to-run 一致性）
Graduation:          NOT_AUTOMATIC（等待 Human Gate）
Phase26:             NOT_REENTERED
```

**Stopped per gate.**
