# PROJECT_LEADER_EXTERNAL_FOREGROUND_DETECTION_FIX_RESULT

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Task: PROJECT_LEADER_EXTERNAL_FOREGROUND_DETECTION_FIX — 修复 ExternalBoundary
> 测试环境中 foreground package 检测错误（`DeriveForegroundFromXml` 解析失败，
> 一直回退到 stale owned foreground）。
>
> **AuthorityDelta: NONE — ArchitectureDelta: NONE (production) / TEST-ONLY**
> （uiautomator 辅助解析器提取 + 修复 + 单测；Runtime 零改动）。

---

## 1. Human Symptom

EBD 真机测试：点击 "App location permissions" 后系统**真实进入了外部权限页**
（com.android.permissioncontroller），XML 证据连续 6 帧稳定显示 external package，
settle candidate + confirmation 条件全部满足——但流程仍然在 external-transition
settle 处失败：foreground 从未被识别为 external，settle budget 耗尽，fail-closed。
所有 27 个 AllStructured 帧的 foreground 都是 settings（owned），与逐帧 XML 矛盾。

## 2. Evidence Confirmation

- **外部页面真实出现**：EBD 最后一次运行的 XML 帧 21-26 =
  `com.android.permissioncontroller`（6 帧稳定 external，满足 candidate +
  confirmation 语义）；XML 0-13 = Settings Root，14-20 = Location subpage。
- **AllStructured 帧 1-27 全部 fg=settings**：与 XML 矛盾——检测层丢失 external。
- **根因（分类 D — foreground detection 错误，非 budget / cadence / contract）**：
  `DeriveForegroundFromXml` 的正则 `"<node[^>]*?package=\"([^\"]*)\">"` 要求
  `package="..."` 的**闭合引号后紧跟 `>`**——即 package 必须是节点**最后一个
  attribute**。真实 uiautomator dump 的 root node 形如
  `index/text/resource-id/class/package/content-desc/bounds/…`，package 之后总有
  其他 attribute → 对 settings 帧和 external 帧**全部匹配失败** → 返回 null →
  回退 `obs.ForegroundApplication`（stale owned settings）→ settle 永远看不到
  external。已对真实 XML 帧验证该正则逐帧失配。

## 3. Implementation Summary

**Tests harness (仅测试侧辅助解析器 — `tests/UniClaw.Runtime.Tests/Scenario/`)：**

| change | detail |
|--------|--------|
| `UiAutomatorXml.ForegroundPackage` (new) | 从 `ExternalBoundaryRealDeviceTests` 内联私有正则提取为独立内部解析器。新正则 `"<node\b[^>]*?\spackage=\"([^\"]*)\""`：`package="…"` 在属性位置（空白分隔、引号分隔值）于节点开标签内**任意位置**匹配——不依赖 attribute 顺序，不再要求 `>` 紧跟其后；`[^>]` 段不能跨越 `>`，匹配被限制在单个开标签内。保持原语义：返回 dump 中第一个带 package 的 node 的包名（root node 恒有 package）。 |
| `ExternalBoundaryTests.cs` | 删除有缺陷的私有 `DeriveForegroundFromXml`；调用点改用 `UiAutomatorXml.ForegroundPackage(xml) ?? obs.ForegroundApplication`（fallback 语义不变）。 |
| `ForegroundDetectionTests` (new, 8 测试) | 覆盖：package 在第一个 attribute；package 在中间 attribute；package 后存在 content-desc/bounds 等 attribute；package 是最后一个 attribute（旧正则唯一能解析的形状，回归保护）；无 package 节点 → null；空/空白/无节点 XML → null；external package 检测（root node = permissioncontroller 的完整 dump）；realistic Settings-root dump（package 前后都有 attribute 的真实形状）。 |

**约束满足**：仅修复测试环境 foreground detection parser；未修改
ExternalBoundary handler、settle budget、transition contract、Agent、Scroll
Execution Profile、OCR/Vision；未使用 ADB 作为 primary authority；external
foreground detection 仍是辅助测试/环境状态检测，**从不注入 Runtime observation**
（Vision-first contract 未绕过，uiautomator 仍仅作辅助分析）。

## 4. Owner

**Test harness (uiautomator auxiliary parsing)** — 测试项目内辅助设备状态检测。
Runtime（`src/UniClaw.Runtime/`）零改动。

## 5. AuthorityDelta

**NONE** — Agent authority、DFS ownership、Traversal、GoalEvidence、Lifecycle、
Semantic capability、ADB-primary 契约全部不变；无场景知识引入；无 fail-closed
弱化。

## 6. ArchitectureDelta

**NONE (production) / TEST-ONLY** — 内联私有正则 → 共享内部辅助解析器
`UiAutomatorXml.ForegroundPackage` + 专用单测文件。非 BREAKING；非 Runtime
架构变更。

## 7. Test Result

- `ForegroundDetectionTests`: **8/8 PASS**（上述 5 项必需覆盖 + 3 项回归保护）。
- EBD deterministic suite (`Scenario.ExternalBoundaryTests`, EBD1-EBD20):
  **16/16 PASS**。
- `ExternalBoundary_RealDevice`: **PASS**（多次）— 完整链路（external foreground
  被检测 → settle candidate + confirmation → SystemBack → verified return）通过。
- `Capstone_OneAgentOneRun_RealEmulator_ReachesCapstoneComplete`: **PASS**
  （独立复跑与最终全量回归均通过；见 §8 关于中间几次 OCR 随机失败）。
- Full regression: **1980 PASS / 0 FAIL / 1980 total**（最终全量回归全绿）。
- `scripts/check-consistency.sh`: **ALL PASS**；`git diff --check`: clean。

## 8. Remaining Risk

- **已知、已上报的感知层缺陷（非本次范围）**：OCR 随机 garbling。验证过程中
  Capstone 中间出现过 2 次失败，证据链均指向 OCR 对 fixture 状态行的乱码
  （`LUNI:MU` @ seq=10-11、`hildn`/`ChildnA` @ seq=79）→ 产生 UNRESOLVED
  interactive UNKNOWN affordance → completeness / post-completeness 校验
  fail-closed（"Unknown interaction affordances remain" /
  "Post-completeness fresh evidence INVALIDATED"）。与 EBD 已知的 ~1/3 归一化
  失败同源（external-boundary-evidence-analysis.md 已报告）。已确认与本修复
  无关：Capstone 测试路径完全不引用 `UiAutomatorXml`（全仓库 grep 验证），
  且含本修复的独立运行与最终全量回归均为全绿。
- `UiAutomatorXml.ForegroundPackage` 返回**第一个**带 package 的 node 的包名；
  真实 dump 中 root node 恒有 package，行为已由测试固定（`ExternalPackage_FirstNode_Detected`）。
- EBD 真机残余 ~1/3 OCR 归一化失败仍未解决（感知层关注点，已上报未修复）。
