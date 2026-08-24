# PROJECT_LEADER_EXTERNAL_BOUNDARY_TRANSITION_SETTLE_ANALYSIS_RESULT

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Task: PROJECT_LEADER_EXTERNAL_BOUNDARY_TRANSITION_SETTLE_ANALYSIS — analyze
> the ExternalBoundary external-foreground detection failure after a
> successful action. **Analysis only; no code changed.**
>
> **AuthorityDelta: NONE — ArchitectureDelta: NONE** (analysis; the recommended
> fix stays inside the Agent's existing external-transition handler).

---

## 1. Human Symptom

点击 "App location permissions" 后系统**实际进入了外部权限页面**
（com.android.permissioncontroller），但 Runtime 未识别，把成功判成失败
（"did not produce an external foreground"），流程中止。

## 2. Evidence Timeline

- ebd_obs_0-13: Settings Root（settings）。
- ebd_obs_14-21: Location 子页（settings）——含 stability 确认帧 + 边界 tap
  的 post-action 帧。
- **ebd_obs_22: com.android.permissioncontroller**（外部权限页，真实出现）。
- 失败点: `TryHandleExternalBoundaryAsync` 用 `firstPostAction`
  （tap 后第一帧，前台仍 = settings）判外部 → fail-closed；后续帧证实外部
  页面已打开。
- 普通 branch dispatch 用 `SettlePostActionObservationAsync`（bounded 3 帧）
  确认转场；external boundary 的进入判断**无 settle**（返回判断有）。

## 3. Root Cause Classification

**A — External boundary detection 缺少 transition settle**（决定性：第一帧
误判 + 后续帧证实外部出现）。B/C/D 均有证据排除（前台检测正确、动作成功、
permission 页面正常）。

## 4. Owner

**Agent — external transition handling**（`TryHandleExternalBoundaryAsync`
进入判断）。Environment 前台检测正确；Semantic / fixture 无关。未改变
DFS / Traversal / GoalEvidence / Vision-first / ADB auxiliary-only。

## 5. ArchitectureDelta

**NONE**（分析）；修复方向为 Agent 机制内 ADDITIVE（进入判断加 bounded
settle），非 BREAKING，非跨层契约。

## 6. Recommended Next Step

**Decision: 1 — 最小修复任务**：给 `TryHandleExternalBoundaryAsync` 的进入
判断加与普通 dispatch / boundary 返回相同的 bounded
`SettlePostActionObservationAsync`——候选帧前台 != owned 应用即确认外部
转场（确认帧为 `ExternalForeground`）；预算耗尽保持既有 fail-closed。
范围：仅该 handler；不动 scroll stability / OCR / Vision / Semantic /
普通 dispatch / Agent authority / Lifecycle / 场景知识。随后 EBD 真机复验
（预期：外部前台被识别 → SystemBack → verified parent return 全链路）。

## 7. Remaining Risk

- 转场延迟可能超过 settle 预算（慢设备/冷启动 permission activity）→
  fail-closed（保守正确，不误判成功）；预算值可随真机证据微调。
- 确认帧的前台包必须同时满足"非 owned 且稳定"——settle 的候选确认语义
  已覆盖（连续帧一致），避免瞬时弹窗误判为外部页面。
- EBD 测试断言（外部前台被观测、恰好 1 次 SystemBack、外部不作为容器）
  保持——修复目标是让 Runtime 识别真实转场，而非放宽断言。
