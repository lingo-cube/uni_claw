# DEEP-SCROLL-STABILITY-REPAIR_RESULT（runN/R/T 类有界修复）

> 前置：`PHASE26-SCROLL-CADENCE-COVERAGE-EVIDENCE.md`（节奏/覆盖方差编目）。
> Round 3-4 真机深路径（runN/R/T）暴露三个稳定性 gate 的重复触发，均正确 fail-closed 但属
> "标记失效/回弹视口"类的判定边界问题。本文件记录三个有界修复（含 RED→GREEN 与回归处理）。

## 1. 触发与 FDP

| run | 触发 | FDP |
|---|---|---|
| N/R | `page identity unresolvable — title band scrolled off` ×3 → 预算耗尽 | 深滚子页标题滚出 → `ResolveSemanticPage`=null → stability 无条件 pending，静止帧永不能成为决策基础 |
| T | `scroll stability frame left the container (page 'Settings')` | 深滚子页全部标记滚出 → binding 根回退把子页解析为父页 'Settings' → 被误判"离开容器" |
| U/L | `Source normalization is unresolved`（root 层）| 接受的 epoch 含**分屏/回弹帧**（行序非单调/纯重复窗）→ normalizer 正确 fail-closed（**非修复对象**——接受不连贯证据=放宽 fail-closed）|

## 2. 修复（前向探索作用域；revisit 保持严格语义）

| 文件 | 内容 |
|---|---|
| `src/UniClaw.Runtime/Agent/Agent.OpenWorld.cs` | `ConfirmScrollStabilityAsync` 新增 `titleOffStableConfirmation: bool`（默认 false）——仅 `ExploreCurrentContainerViewportsAsync` 传 true： ① **title-off 确认**：page=null + 前景未变 + 行集与上一帧稳定（IsViewportStable）→ 确认该帧为决策基础（容器身份由连续性承担）； ② **identity-misfire 确认**：page 解析为别的页（如 'Settings' 回退）但行集与上一帧一致 → 同容器（深滚标记全失）非离开容器，确认； ③ 真离开（前景变/行集变/页变且行变）仍 fail-closed。 |

## 3. RED→GREEN / 回归

- 新测试 `TitleOff_StableRows_ConfirmedInsteadOfBudgetExhaustion`（SettleWorld 派生 TitleOffWorld + 映射/Page/Goal 全域）：修复前 FAIL（预算耗尽路径）→ 修复后 PASS（title-off CONFIRMED + 无预算耗尽 trace）。
- **回归（首版未加作用域）**：`RVT212/212b`（revisit 的 null-identity=歧义→停语义）被破坏 → 以作用域参数修复（revisit 调用点保持默认 false）→ Revisit 套件 12/12 恢复。
- Stability/Revisit/Quiescence：15/15、19/20 全绿；全量 C# 套件 **2364/5**（零新增，5=既有环境性）。

## 4. 生产端验证（真机）

- **runV（修复后）**：**4 层容器遍历**（root epoch 16 ✓ → Display child epoch 17 ✓ → 'Interaction controls' epoch 4 ✓ → Accessibility 容器）——title-off/misfire 修复在真实深滚路径端到端生效（无 quiescence/左出失败）；最终停在 Accessibility 容器的节标题/描述 Unknown（感知方差家族，条目见
  `PHASE26-SCROLL-CADENCE-COVERAGE-EVIDENCE.md` §6）。
- runS（修复后）root Unknown 'Lou'+'Bluetooth, pairing'：既有家族。

## 5. 边界

- 未触碰：normalizer / completeness / P5 / P7 / 副文本规则 / settle budget / retry / sleep / swipe 参数。
- 作用域参数默认 false：revisit、返回、分支派发的 stability 语义完全不变。
- U/L 类的"回弹视口入 epoch"保持 fail-closed（正确拒绝不连贯证据），不修。