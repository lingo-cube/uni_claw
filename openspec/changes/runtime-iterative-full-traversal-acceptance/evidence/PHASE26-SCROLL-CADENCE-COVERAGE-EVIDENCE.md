# PHASE26-SCROLL-CADENCE-COVERAGE-EVIDENCE

> 目的：为"滚动节奏-覆盖率"有界门沉淀证据（Phase 2.6 push 的剩余重复触发全部属于**检测覆盖/滚动节奏波动**，且全部正确 fail-closed）。
> 数据：runF..runN（fresh real，settingscampaign 1，emulator-5554，时间戳采证开启）。

## 1. 失败分布（8 个近期 run）

| run | 推进深度 | terminal | 类 |
|---|---|---|---|
| F | root+child epoch ✓ | post-completeness INVALIDATED（tier 错配 → 已修）| 已修复 |
| G | root ✓→child 入口 | 过渡 settle 预算（3 观测未稳定）| 节奏 |
| H | root+child epoch ✓+post-completeness PASS | 返回控件缺位（→ 已修，重放证实）| 已修复 |
| I | root | root Unknown | 检测波动 |
| J | root+child epoch ✓+post-completeness PASS | 返回控件缺位（已修；J 为修复前基线）| 已修复 |
| K | root ✓→child 入口 | 过渡 settle 预算 | 节奏 |
| L | root | normalizer Unresolved（win11/14 稀疏回跳窗）| 节奏/覆盖 |
| M | root | root Unknown | 检测波动 |
| N | **多容器（child+2 子容器）** | quiescence identity-unresolvable ×3 → 预算耗尽 | 节奏/覆盖 |

## 2. 覆盖率证据（稀疏/回跳/滚出）

- **稀疏窗**：runL win11 仅 4 个 nav 行（win8 8 行 → win11 4 行，-50% 突降仍被 accept）；runN 深度容器仅 3 anchors（同页应有更多实建）。
- **回跳/滚动反向**：runL win14 出现 `row_009` 位置 0.79→0.17（视口回跳），union 序因此不连贯。
- **title 滚出**：runN seq48 连续 3 帧 `page identity unresolvable — title band scrolled off` → quiescence 预算耗尽（正确 fail-closed，但系检测瞬时覆盖不足）。
- **root Unknown 波动**：runI/M root 个别行单帧 Unknown（检测/分类瞬时差异）。

## 3. 根因类别（全部 fail-closed 正确）

`ScrollForward` 快速 fling（StepFraction=1.0，~40% 屏高/次，时长按速度封顶）→ 过渡帧 OCR 采样稀疏、
window 行数骤降、偶发回跳 —— 系统各层（settle/quiescence/normalizer/Unknown 计数）都按设计刹车。
无单一代码缺陷；是"滚动步进 vs 感知覆盖率"的匹配问题。

## 4. 候选有界杠杆（供 Leader/授权）

1. **首选 · 滚动节奏-覆盖率门**：ScrollForward 的步长/时长按**感知覆盖率证据**调节（例如：前窗行数
   骤降或 identity 不可解析时，下一步以更小步长/更长时长滚动、并在 settle 预算内重采集）；
   不增加预算、不加 sleep、不放宽任何 fail-closed；仅改变后续动作的物理参数（语义=覆盖优先）。
   - 证据引用：本文件 §1/§2。
   - 反例保证：漂亮整帧（runH/J 的 8-17 行窗）下行为不变；genuine 页面边界（真实左/右出）仍 fail-closed。
2. 次选 · 稀疏窗拒绝：acceptance 层对与上一 accepted 窗行数下降 >50% 的候选窗 fail-closed 重观测（
   不视为稳定）——改动在 viewport acceptance 边界，行为变化更大，需单独评估。
3. 维持现状抽签：每轮 ~5 分钟真机，概率性等一次干净节奏（当前深路径率约 2-3/9）。

## 5. 冻结约束保持

不增加 budget/retry/sleep；不放宽 fail-closed；不改 swipe 参数以外的语义；不按单项证据判 duplicate；
不改 completeness 检查本身。滚动节奏参数的调整**只影响后续动作的物理步进**，不改变任何判定。

## 6. 首窗检测方差（runI/M 的 root Unknown）——无合法有界修复

- 复现形态：root 首个视图窗（seq7/8 类）中 `'Wallpaper'(tb, row_014)`/`'Accessibility'(tb)` 判 Unknown。
- 决定性证据（runM seq7/8）：该帧 **menu_item 无 'Wallpaper' 同行、XML 截断于 'Display'(≤0.952)，
  'Wallpaper' 带 0.79-0.81 处无任何通道同类**；'Accessibility' 有同行 mi 的同帧即 NonInteractive ✓。
- 判定：这是**双通道同步漏检**（OCR 短读 + XML 截断）——把无验证依据的 tb 提为可交互行违反
  "不按单项证据判 interactivity"；Unknown 是正确 fail-closed。同批次 run（F/H/J/K/L/N）root 均完成，
  说明该行在其他 run 中通道正常——纯检测方差，无 invariant-合规修复。
- 结论：I/M 类残余不可修（保持 Unknown 诚实）；与 settle/title-off/sparse-window 一并归入
  **感知/节奏方差族**，唯一出口 = 干净节奏 run（经验抽签）或 Hetero 层检测改进（非本 gate 范围）。

## 7. 结论

本 gate 的边界结论：**确定性挡路已全部核销；剩余触发全部为"双通道感知/节奏方差"且正确 fail-closed；
无 invariant-合规的有界修复可消除它们**。继续冲刺 Completed 的选项：(a) 经验抽签（每轮 ~5 分钟，
深路径率约 2-3/9，需一次全链干净的 run）；(b) 由 Human 裁决接受"确定性阶段"为验收点并暂缓
Completed 目标；或 (c) 感知通道改进（OCR/XML 截断/合成）作为后续独立 Large gate。

## 8. 发现：structured（uiautomator）通道可靠性 + 未校准坐标偏移（采集缺陷，非运行时候选）

- **实测（本页真机）**：动态静态根页多次 `uiautomator dump`——(a) 同一个静态页不同时刻 dump 内容
  可整段不同（曾仅 2 行 → 重 dump 16 行）；(b) 即使枚举完整，XML 行与视觉行**pitch 相同但整体
  错位 ~150-350px**（'Network & internet' XML center 867px vs 视觉 514px；'Settings' 标题 XML 472px
  vs 视觉 ~210px）；(c) 元素级的同文本行 band 差随(Y)变化（视 2.6/8 节拟合，实为页相关偏移+缩放的
  复合，非固定 1939px——root bounds 实测 `[0,0][1080,2400]`）。
- **机理**：emulator 上 uiautomator 的 accessibility 快照在滚动/渲染态下不可靠（漏枚举 + 坐标与
  screencap 应用窗口帧存在页相关偏移）。运行时主要靠 text-identity 佐证（稳健）；bounds 级跨通道
  佐证因此结构性受限（仅顶层返回图标侥幸可用）。
- **物化影响**：'Wallpaper'/'Bluetooth, pairing' 类 Unknown 根因 = XML 该行缺失（漏枚举）+ 偏移
  使 bounds 佐证不可用——均正确 fail-closed；OCR 乱码（'Lou'）与无 peer 图标为同族通道方差。
- **可选修复（采集层，Medium/Large gate）**：① 用 accessibility bridge（dumpsys accessibility）
  替代 uiautomator 或对漏枚举/截断重采（证据：静态页重 dump 可补齐）；② 每观测用同文本行对拟合
  XML→vision 映射后再归一化（bounds 佐证激活）。两者均不动运行时判定；赔偿有限（'Wallpaper' 类
  根因在 XML 缺行，重采可根治）。
- **本轮实测补充**：runP = 'Lou'(OCR)+'Bluetooth, pairing'(通道方差)；runQ = 环境空跑；静态根页
  探针重 dump 证明 uiautomator 内容枚举非确定。