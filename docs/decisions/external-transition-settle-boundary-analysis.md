# External Transition Settle Boundary Analysis

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Task: PROJECT_LEADER_EXTERNAL_TRANSITION_SETTLE_BOUNDARY_ANALYSIS — analyze
> whether the ExternalBoundary remaining real-device failure is a transition
> settle contract mismatch (vs simply needing a larger budget). **Analysis
> only — no code changed.** Constraints honored: no settle-budget / handler /
> scroll / OCR-Vision / ADB-XML / Semantic changes.

---

## Phase 1 — Human Symptom

点击 "App location permissions" 后系统**确实进入了外部页面**（permissioncontroller
逐帧出现并持续稳定），但 Runtime 的 external transition settle 未完成确认，
判为失败（"did not settle into an external foreground"）。**页面到了，检测没
看见。**

## Phase 2 — Evidence Timeline (real device, one failing run)

| 帧 (XML index) | package | 观察 seq (AllStructured) 的 fg |
|----------------|---------|-------------------------------|
| 0-13 | settings | settings |
| 14-20 | settings (Location 子页) | settings |
| **21-26 (6 帧)** | **com.android.permissioncontroller** | **settings（未检测到 external）** |

回答：
1. **permissioncontroller 第几帧出现？** — XML 21 起，**连续 6 帧**（21-26）
   稳定出现——证据层面 external 已真实到达。
2. **settle budget 消耗？** — 6 帧预算全部耗尽，candidate 从未出现（检测的
   fg 全为 settings）。
3. **失败时最后一帧状态？** — settle 最后一帧的 XML 已是 permissioncontroller
   （external 稳定），但检测的 fg = settings（回退值）。
4. **是否已出现 external 但稳定条件不足？** — **否**——external 已出现且连续
   6 帧稳定，稳定条件在证据层面完全满足；是**检测层未报告 external**。

## Phase 3 — Contract Analysis

当前 external success contract = **"external appeared + stable"**（candidate
帧前台离开 owned + 连续帧同一外部前台确认）——与普通 Action settle 的
candidate+confirmation 结构**同构共享**（同一 bounded settle 模式）。语义
正确、非不适配——失败发生在**检测输入**（前台包未正确报告），而非契约
判定。

## Phase 4 — Failure Classification

**D — Foreground detection 错误**（决定性）：

- `DeriveForegroundFromXml` 的 regex `<node[^>]*?package="([^"]*)">` 要求
  package 属性后**紧跟 `>`**；实际 uiautomator node 中 package 后总有其他
  属性（content-desc/checkable/.../bounds）——**对 settings 帧与 external
  帧都匹配失败**（已验证）→ 前台全部回退 `obs.ForegroundApplication`
  （PhysicalEnvironment，全为 settings）。
- 因此 external 帧（XML 21-26）的 fg 从未被报告为 permissioncontroller，
  settle 的 candidate 条件（离开 owned）从未满足。
- **排除**：A（budget 不足——external 在窗口内出现且稳定）、B（cadence
  慢——6 帧足够）、C（contract 不合理——appeared+stable 正确）。

## Phase 5 — Decision Boundary

**Root cause 在测试侧前台检测函数**（`DeriveForegroundFromXml` regex 缺陷 +
回退路径），**不在 settle contract / budget / cadence**。推荐下一步：修复
`DeriveForegroundFromXml` 的 regex（去掉对 package 后 `>` 的依赖：
`<node[^>]*?package="([^"]*)"`）——测试 harness 的检测修复（不在禁止列表：
非 handler、非 settle、非 budget）——随后 settle 将看到 external
（candidate + confirmation），EBD 真机复验。
