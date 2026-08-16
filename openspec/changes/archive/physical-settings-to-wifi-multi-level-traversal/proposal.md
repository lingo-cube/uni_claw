# Proposal: Physical Settings → WiFi Multi-Level Traversal（Agent 自主多页语义闭环）

| 属性 | 内容 |
|------|------|
| Change ID | `physical-settings-to-wifi-multi-level-traversal` |
| 状态 | Proposed（**本 change 只产出 proposal / design / specs / tasks，不实施**） |
| 类型 | Vertical Slice（毕业语义环之上的第一条 **Agent 自主多页遍历** 真实切片） |
| 日期 | 2026-08-14 |
| 分支 | `uni-agent` |
| 上游 | `docs/decisions/physical-wifi-slice2-graduation-decision.md`（GRADUATED_PHYSICAL_WIFI_MINIMUM_SEMANTIC_LOOP，RealityLevel=EMULATOR_REALITY_END_TO_END_SEMANTIC_LOOP） |
| 前置事实 | Slice 2 已知限制：MultiLevelTraversalClaim = **NOT_PROVEN_AND_NOT_CLAIMED**（单页闭环） |
| Authority | `PROJECT_LEADER_SELECT_NEXT_REAL_AGENT_SCENARIO_MULTI_LEVEL_TRAVERSAL` |

> **验收约束**：本 change 完成后运行 `openspec validate physical-settings-to-wifi-multi-level-traversal` 通过即停（STOP after OpenSpec validation）。任何实现都需另行授权。

## Why

Slice 2 毕业证明已把「第一条真实 Agent 语义闭环」钉在**单页**：`RunSemanticGoalAsync` 从启动锚点落地的页面直接绑定目标对象，页面变更即 `SemanticContradiction`。而 Agent 的真实价值恰恰在**自主多页**：目标对象不在当前容器时，Agent 必须决定下一个语义跳点、执行导航动作、用 fresh 观测验证页面/容器变更，最终在目标真正绑定的页面上执行毕业的 SetEnabled→SetSwitch→fresh GoalEvidence 链。

本 change 选择**真实 Settings 应用**作为场景：初始世界 = Settings 根页，目标 = `WifiConnectivity.Enabled = true`。Agent 必须自主走到 Wi‑Fi 开关（预期语义路线可能为 Settings → Network & internet → Internet → WiFi 控制），但**路线不得编码为脚本**——每一步跳点由当前 fresh 观测 + 调用侧声明的页面识别知识（既有 `PageAnalysisCriteria`）决定，路由是涌现的，不是预编排的。

## What Changes

- **语义环导航相位（最小 Runtime 接线，非新架构）**：`Agent.SemanticRun.cs` 的闭环增加一个导航分支——当目标对象在当前容器未绑定时：
  1. 用既有 `PageAnalysis.Analyze(observation, _pageAnalysisCriteria)`（多源证据：TEXT_ANCHOR / TEXT_ANCHOR_NEGATIVE / SWITCH_DISTRIBUTION / FOREGROUND）解析当前观测里**恰好一个**已知非自身页面的识别锚点 → 下一跳候选；
  2. 唯一候选 → Agent 授权 → 构造 `DeviceAction.Tap(groundedIndex, bounds)` → 经既有 `Traversal.ExecuteLoweredActionAsync` 分发（fresh 观测 + 序列推进 + Rejected→Failed 协议不变）；
  3. fresh 观测需证明**页面/容器变更**（`Reconcile.FromObservation` 新页面名 ≠ 当前页 ∧ `!container.IsStillMine(fresh)` ∧ 序列严格推进）→ `CreateContainer(newPage)` + `Bind(freshObs)`（既有 OpenWorld 路径同款语义，见 design.md §Decisions）；
  4. 无候选 / 多候选 / 页面未变 / 页面 Unknown → **fail closed，零导航分发**（不猜坐标、不编造进度）。
  - 目标对象一经绑定 → 回到毕业的 SetEnabled 语义链（capability 选择 → SemanticAction → LowerAction → SetSwitch → 物理 tap → fresh 观测 SwitchState=true → `GoalEvidence` → `Satisfied`），**该链零改动**。
- **多页页面识别知识（调用侧声明，裁决 11）**：宿主按 `PageAnalysisCriteria`（既有模型：`PageAnchors` / `PageNegativeAnchors` / `PageSwitchStateAnchors`）声明本场景页面词汇（如 `SettingsRoot` / `NetworkAndInternet` / `WifiInternet`）及其区分锚点；`resolveSemanticPage` 从常量「Settings」升级为多页解析（宿主侧函数，不触及 Runtime 语义模型）。锚点以真实 emulator 观测校准（实现阶段），共享文本（如页面标题行）用 negative 锚点消歧。
- **证明宿主（PhysicalHost）**：入口 = Settings 根页（`android.settings.SETTINGS`），**不得**导航到 Internet 页/点按任何行；Agent 拥有启动后的一切导航权。成功条件 = `Satisfied` ∧ 每跳验证 ∧ 恰一次 SetSwitch（初始 OFF 时）∧ `GoalEvidence.SourceObservationSequence == fresh 观测序列` ∧ 感知 SwitchState=true；`settings get global wifi_on` 读回仅佐证。
- **Falsifier F1–F6**（见 tasks/specs）与 **现实证明要求**（emulator-5554 现场多页回放，§33 emulator-only）。

## Capabilities

### New Capabilities

- `physical-settings-to-wifi-multi-level-traversal`: Agent 自主多页遍历语义闭环（Settings 根页 → Wi‑Fi 开关 → SetEnabled），含每跳 fresh 观测验证、跨页 binding 生命周期、容器推进、导航失败 fail-closed 与六条 falsifier。

### Modified Capabilities

无（本仓库无 `openspec/specs/` 主规格；`physical-wifi-off-to-on-minimum-semantic-loop` 已归档，其规格**不改动**——多页遍历是新能力，不是对单页闭环的修改；毕业链在最终页原样复用）。

## Impact

- `src/UniClaw.Runtime/Agent/Agent.SemanticRun.cs`：语义环增加导航分支（接线既有 PageAnalysis / Reconcile / Container / Traversal；Agent 仍为唯一语义 authority——裁决不迁移）。
- `src/UniClaw.Runtime.Adapters/`：**零改动**（PhysicalEnvironment / Translator / AdbDispatchTarget / ImageSwitchStateProvider 机制不变；导航即 Tap 已有机制）。
- `src/UniClaw.Runtime.PhysicalHost/Program.cs`：多页页面识别知识 + 根页启动 + 证明输出（宿主侧接线）。
- `tests/UniClaw.Runtime.Tests/`：多页 Fake falsifier 套件（F1–F6）+ 约束测试 + 校准资产扩展 + 真实环境集成 tier。
- 文档：毕业决策记录（RealityLevel=EMULATOR_REALITY_MULTI_LEVEL_SEMANTIC_LOOP）；`docs/decisions/`。
- 依赖：无新增外部依赖；emulator/adb 为环境前置（显式失败，非静默跳过）。

## 非目标（Forbidden / Deferred，本 change 明确不做）

- **不做**：新 Provider / Provider registry / workflow engine / 导航 DSL / 硬编码屏幕序列 / 坐标脚本 / WiFi 专用 navigator / WorldState 注入 / 隐藏 emulator API / 新语义 authority。
- **不扩展**：任意深度、任意应用、弹窗处理、滚动恢复（真实 Settings 路线若自然需要滚动，只记录压力、按需裁剪）、跨应用遍历、通用浏览器导航、planner 重构。
- 多页遍历的**证明层级目标**：EMULATOR_REALITY_MULTI_LEVEL_SEMANTIC_LOOP（emulator-only，§33；不称 REAL_DEVICE_PROVEN）。
