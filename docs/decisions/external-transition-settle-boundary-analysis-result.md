# PROJECT_LEADER_EXTERNAL_TRANSITION_SETTLE_BOUNDARY_ANALYSIS_RESULT

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Task: PROJECT_LEADER_EXTERNAL_TRANSITION_SETTLE_BOUNDARY_ANALYSIS. **Analysis
> only; no code changed.**
>
> **AuthorityDelta: NONE — ArchitectureDelta: NONE** (analysis; the recommended
> fix is a test-harness detection-function correction, not a settle/handler/
> budget/contract change).

---

## 1. Human Symptom

外部页面（permissioncontroller）已真实打开并稳定出现，但 Runtime 的 external
transition settle 未完成确认，判为失败。**页面到了，检测没看见。**

## 2. Evidence Timeline

XML 21-26（6 帧）为 permissioncontroller（连续稳定）；但同帧的检测 fg 全部为
settings。settle 6 帧预算耗尽，candidate 从未出现（检测未报告 external）。
四问：external 第 22 次观察起出现且稳定（≥6 帧）；budget 耗尽时 external 已在
场；失败最后一帧 XML 已是 external；**稳定条件满足——是检测层未报告**。

## 3. Current Contract

external success contract = "external appeared + **stable**"（candidate +
confirmation），与普通 Action settle 同构共享。**契约语义正确、非不适配**；
失败在检测输入（前台包未正确报告），不在契约判定。

## 4. Failure Classification

**D — Foreground detection 错误**（决定性）：`DeriveForegroundFromXml` 的
regex 要求 `package="..."` 紧跟 `>`，而实际 uiautomator node 中 package 后总
有其他属性 → **对所有帧匹配失败** → 前台全部回退 stale 值（settings）→
settle 的离开-owned 条件从未满足。A（budget）/B（cadence）/C（contract）均
有证据排除。

## 5. Owner

**Environment 前台检测（测试 harness 的 `DeriveForegroundFromXml`）** + 回退
路径（`obs.ForegroundApplication`）。非 settle / handler / budget / contract /
scroll / OCR-Vision / ADB-XML / Semantic。

## 6. ArchitectureDelta

**NONE**（分析）；修复 = 测试侧检测函数（regex）修正，ADDITIVE 无架构影响。

## 7. Recommended Next Step

修复 `DeriveForegroundFromXml` 的 regex（去掉对 package 后 `>` 的依赖：
`<node[^>]*?package="([^"]*)"`）——测试 harness 的检测修复（不在禁止列表）；
随后 external 帧将被检测为 permissioncontroller → settle 的 candidate+
confirmation 正常走通 → EBD 真机复验（预期 normalization 通过 + 外部前台识别
+ SystemBack + verified return 全链路）。同时可移除本次分析加的
FRAME_TIMELINE 诊断（保留为 evidence dump 亦可）。
