# UniAgent Emulator Tier B Validation — Result

DocumentType: `VALIDATION_RESULT`
Decision: `PROJECT_LEADER_UNIAGENT_EMULATOR_TIER_B_VALIDATION_RESULT`
Change: `openspec/changes/uniagent-emulator-validation-harness/`（Tier B 执行，Human Decision AUTHORIZED）
Date: 2026-08-26
Authority: Runtime Architecture Contract I-1..I-14 与 Architecture v1 不变；本结果不新增架构权威。

---

## 1. Executive Decision

**TIER_B_PARTIAL_WITH_EVIDENCE** —— S1/S2/S3 全部在真实 Android Emulator（API 15 级系统，
com.uniclaw.fixture CAPSTONE 场景）上获得证据级验证，核心命题成立；但目标函数按
Harness 侧 Truthfulness 弱化（GoalEvaluator 满足条件宽于 fixture 内部 8/8 计数），
故不取 TIER_B_PASS，保留证据侧行政复核。无 STOP 条件触发，零 Runtime 修改。

**核心命题（真实设备验证）**："上层给任务，Runtime 自己干；能完成就完成，处理不了就有
证据地安全停止，上层不需要 Run 内微操" —— **成立**（S1 完成路径 + S2 fail-closed 路径双证）。

## 2. S1 Result — PASS（真实设备自主探索）

- Directive 准入：Accept（run-1）；Emulator 零 UI 内容（闭词汇，无坐标/路径/Action/序列）。
- 真实自主探索：6× ActionDispatched（真实 tap 经 AdbDispatchTarget）。
- 终态：`Completed`，`GoalEvidenceProduced` 先于 `RunCompleted`（事件序真实）。
- Truthful coverage：wire tier ledger 逐字 `Unavailable`，无杜撰。
- 证据：`docs/work/active/tierb-s1-result.json` + `tierb-s1-terminal-screen.png`。
- 诚实记录（Harness finding，见 §10）：fixture 内部 `Visited 3/8` —— Runtime 忠实执行了
  binding GoalEvaluator 声明的语义（可解析页即满足），GoalEvaluator 未对齐 fixture 8/8
  完成语义。这是 harness binding 的目标函数选择，不是 Runtime 行为缺陷。

## 3. S2 Result — PASS_BOUNDED_FAIL_CLOSED（真实异常自主处置）

- `run.strategy.start` = 1；Run 内 Emulator 控制调用 = 0。
- 真实异常：+6s fixture force-stop（外部世界事件，非 Emulator Run 内动作），注入日志
  `[tierb] anomaly injected: fixture force-stop` 落盘。
- Runtime 自主处置：终态 `Failed`，明确理由
  `"post-action transition did not settle within 3 fresh observations；fail closed（composition policy；zero redispatch）"`。
- 事件支撑：`[ActionDispatched×5, RunFailed]`；无无限 retry（zero redispatch 明示）；
  无隐藏 fallback；recovery 证据如实缺席（未伪造）。
- bounded fail-closed 未被标记为 recovery success（S2 修订语义遵守）。
- 证据：`tierb-s2-result.json` + `tierb-s2-runlog.txt`（含注入时间线）+ 终态截图。

## 4. S3 Result — PASS（真实跨 Run 适应）

- Run 1：run-1，Completed（8 events）；harness 本地分析提取事实 `evh3-8-events`。
- Run 2：run-2（distinct runId；total 恰好 2 次 accepted start），Completed。
- R1 影响仅进入 Run 2 strategyId（payload diff = strategyId ONLY）；零 mid-run mutation；
  无 Memory、无 Planner、Runtime state 未被触碰。
- 证据：`tierb-s3-result.json` + `tierb-s3-runlog.txt`。

## 5. Human-readable Reality Analysis

Tier A（确定性世界）与 Tier B（真实设备）之间隔着一整层感知现实：真实 OCR 帧率、
真实 screenshot→UDS 感知周期、真实 foreground 生命周期。Tier B 暴露的不是 Runtime 缺陷，
而是**验证装置与现实的三次错位**（全部 Validation Harness 类）与**一次 Runtime 安全
机制的正确触发**（ancestry-cycle guard 拦截了我 binding 的错误分支授权——这恰是该
安全机制在真实设备上有效的实证）。Runtime 在两个方向（完成/安全停止）都表现出
契约内行为；所有偏差先取证后定位，未发生"先改代码再看"。

## 6. Evidence References

每场景保留：Goal、Generated Directive、StrategyId、RunId、Admission、Emulator Call Log
（含 payload digest）、lifecycle events、Snapshot、Diagnostics、Terminal reason、
Android foreground/environment state（dumpsys + uiautomator + screencap 终态截图）。
存放：`docs/work/active/tierb-{s1,s2,s3}-*`。Trap：如实不存在（strategy path 无 trap
机制，S2 修订语义下不要求）。

## 7. First Divergence Points（全部定位后再动作）

| # | 现象 | FDP | Owner | 处置 |
|---|---|---|---|---|
| 1 | `entry does not match the verified Startup boundary` | directive scope 硬编码 Tier A 假 app/root（`Agent.OpenWorld.cs:82` 比对拒绝） | Validation Harness | directive 参数化 |
| 2 | `Unresolved inventory cannot be accepted` | 无 CALLER_SOURCE_PROVENANCE grounding（`BranchInventoryEvidence` 契约） | Validation Harness | 按 Tier A 同机制产 occurrence grounding |
| 3 | `ancestry cycle detected for 'Fixture Root'` | 我方 binding 把父返回键授权为子分支（`Agent.OpenWorld.cs:435` 安全拦截） | Validation Harness | 子页改 record-only，父返回走 verified-return |
| 4 | terminal=`Idle` events=`[]` | collector 轮询预算 5s < 真实探索 ~40s（设备已到 Child 03 证明探索在跑） | Validation Harness | 预算扩至 60s |
| 5 | S2 首跑异常未触发 | 注入延迟 15s > run 时长（timing 日志证实） | Validation Harness | 延迟调 6s + 注入留痕 |

无 Runtime owner 项 —— 五次偏差零 Runtime 修改。

## 8. Failure Classification

全部五次偏离均分类为 **Validation Harness**（装置组成/参数/时序），无
Strategy Compilation / Discovery / Grounding / Authorization / Execution / Recovery /
Environment 类失败。未出现任何裸 "Runtime failed" 结论。

## 9. Runtime Capability Findings

1. **Startup 边界验证在真实设备上有效**（app+entry 双重比对，错配即 fail-closed）。
2. **CALLER_SOURCE_PROVENANCE 契约在真实感知下被正确执行**（无 grounding 零派发）。
3. **ancestry-cycle 身份安全在真实 OCR 世界正确拦截循环授权**——Tier A 未覆盖的
   真实有效性证据。
4. **异常处置契约真实成立**：外部世界崩溃级事件 → bounded、明确理由、zero-redispatch
   的自主终态（S2 命题的核心证据）。
5. **goal 语义权在 binding**：Runtime 忠实执行 GoalEvaluator 声明的目标函数——
   "完成"的定义质量是 capability binding 的责任，不是 Runtime 的。

## 10. Harness Findings

1. Tier A fixture 常量与 Tier B 现实解耦不足（已修：binding/directive 参数化）。
2. collector 轮询预算需按 tier 参数化（已修：60s；仍 finite，fail-closed 保持）。
3. **GoalEvaluator 目标函数弱于 fixture 完成语义**（Visited 3/8 即 Completed）——
   记录为待议项：是否要求 8/8 属验证协议的目标强度选择，Human 可裁定后一行调整。
4. 源形态守护随 Tier B 授权演进（Adapters/Vision.Host 前向引用入允许清单，注释记录
   Human 授权与角色限定；PhysicalHost 禁令不变）。

## 11. Regression Result

真实环境验证未污染确定性基线：harness 51/51；确定性全量 2103/2103 + Semantic 32/32；
架构守护 61/61；consistency ALL PASS；`openspec validate --strict` PASS；
`git diff --check` PASS；Runtime 生产源码零修改（工作树 Runtime diff 为会话前既有
Phase-2 在途状态，未触碰）。

## 12. AuthorityDelta

`NONE`。零新增 Runtime API/wire/ownership；Tier B 组合仅消费既有生产管线
（AdbScreenshotSource/LocalVisionPerceptionSource/AdbDispatchTarget/VisionServiceHost）。

## 13. ArchitectureDelta

`NONE_RUNTIME`。新增仅 harness 侧：`TierBProgram.cs`（tierb 入口）、
`RealityFixtureStrategyBinding.cs`（真实 fixture 语义绑定）、collector 轮询预算调整、
guard 允许清单演进（Human 授权记录在注释）。

## 14. Tier C Recommendation

**RECOMMEND_READY_PENDING_HUMAN_GATE**：真实设备（physical device）Tier C 的前置能力
面已被 Tier B 证据覆盖（同一生产管线，仅 serial 换物理设备）。建议 Human 裁定：
是否在物理设备上重复 S1–S3，以及 GoalEvaluator 目标强度（§10.3）先行收紧与否。

## 15. Remaining Human Gates

1. Tier C 执行授权（NOT_AUTHORIZED 状态维持，本结果不改变）。
2. GoalEvaluator 目标强度裁定（§10.3，影响 TIER_B_PASS 与否的行政复核）。
3. Phase 2.5 lifecycle 结论 / Phase 3 Memory / Archive —— 全部 Human-owned，未动。
