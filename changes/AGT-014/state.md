# AGT-014 — Android Settings full-chain reliability run

lifecycle_state: persisted · disposition: none · depth: standard · base: e68ba121

## Intent（WHAT/WHY）

在 AGT-012 的三项 Android Settings 真实测试集和 AGT-013 的运行时安全策略
基础上，执行一次真实的全链路压力验证：profile → perception → Agent
consultation → Runtime guard → Kernel/effect → post-action verification →
trace/facts/evidence。目标是暴露可靠性、智能性、容错性和可诊断性问题，不把
仿真结果冒充真实模型/设备能力。

## Scope / Out of Scope

### 范围

- 任务一：找到指定 Settings 菜单项。
- 任务二：找到指定开关，切换后重复执行并复用已满足状态。
- 任务三：有界遍历 Settings 菜单，禁止动作在 dispatch 前拒绝。
- 真实 Android emulator、真实 DSH Agent、ADB effect、post-action verification。
- 对 trace、日志、facts、失败定位信息做压力检查；局部缺陷可直接修复。

### 不在范围

- 不修改产品架构、共享协议、模型路由或 Agent 模型。
- 不把 live 失败改成 PASS；环境/凭据/模型不可用时保留明确阻塞证据。
- 不扩大“全量遍历”为无界探索；以测试集和 profile 的有界预算为准。

## Acceptance

1. 三项任务各有真实运行结果，记录 method/expected/actual/evidence。
2. 每个动作可追溯到 decision、policy digest、guard verdict、effect receipt、
   post-action observation；失败保留 first divergence。
3. 禁止/未知/重复动作不产生 effect；设备或 Agent 异常安全停止并可定位。
4. 发现不涉及架构、模型、协议的局部缺陷时完成修复并补回归验证。
5. 需要 Owner 裁决的事项单独列出，未完成项不伪装为 CLOSED。

## Plan

1. 预检 emulator、ADB、DSH endpoint、profile 和运行目录。
2. 并行执行三项真实场景与故障注入/可观测性审计。
3. 对局部实现缺陷走复现 → 修复 → 回归；架构/协议/模型问题停在 Gate。
4. 汇总四级验证和可复查 evidence，决定 CLOSED 或保留 BLOCKED/OPEN。

## Verification

```yaml
level: ENVIRONMENT
method: real API 35 emulator + DSH Agent + ADB + SettingsCoverage/HostRunner live runs; deterministic regression after any local fix
expected: three tasks and failure paths produce traceable, safe, honest outcomes
actual: deterministic + real-device chain partial PASS; full solution 1307/1307 and Host.Dsh build pass; the prewarmed real DSH Settings objective now completes through 3 consultations and 2 guarded ADB taps after the 7890 proxy and runtime-token prompt fix; the remaining AGT-014 tasks and fault-injection summary are still open
evidence: evidence/agt-014/verification-2026-10-06.md; evidence/agt-014/dsh-runtime-token-fix-2026-10-06.md
```

## Status log

- 2026-10-06 · UNDERSTAND → RESOLVE → PERSIST · 明确真实三任务、故障路径、
  证据要求和不可越过的架构/模型/协议边界。

- 2026-10-06 · PLAN → IMPLEMENT → VERIFY · 真实 API35 emulator 链路通过 typed、
  Host Wi-Fi、ADB、坐标和有界 coverage；首轮环境故障已做局部修复。
- 2026-10-06 · VERIFY · 真实 DSH handshake 成功，但 glm-5.3-flash consultation
  两次 75s 无响应，未产生 effect。首帧原始快路径 63.7s 已拆证为视觉服务
  冷启动 63.314s、PNG 解码 0.056s、当前截图推理 0.619s；撤回“586 proposals
  导致首帧感知变慢”的未证实归因，保留首轮预热与后续 consultation 作为待处理项。
- 2026-10-06 · IMPLEMENT · SettingsTraversalLiveFeed 在首轮观察前显式预热视觉
  服务；settings-trace 增加预热、解码和当前截图推理分段计时；预热失败仍沿用
  原有 hierarchy 可用、fast fail-closed 语义。
- 2026-10-06 · VERIFY · 最终构建预热复验首帧 fast path 0.721s（startup=0、
  decode=0.055s、inference=0.664s），environment-preflight=true；但 DSH
  consultation 仍 75s 无响应、零 effect（后续 journal 复核修正：最终回合一次请求，
  adapter 重复诊断行不能计作两次 consultation）；
  环境启动器 supervisor 复验通过，设备在启动脚本退出后仍在线。
- 2026-10-06 · IMPLEMENT · 正式流程前写入 environment-preflight.json，facts 的
  beginner guidance 增加该入口；启动脚本改用独立 emulator supervisor，补齐
  PID/serial/clone 生命周期记录。
- 2026-10-06 · VERIFY → IMPLEMENT → VERIFY · 全量并行回归暴露 process-wide
  config-path 环境变量竞态；将相关 Host tests 收入不可并行集合后 Host.Tests
  恢复 153/153 PASS。
- 2026-10-06 · RESOLVE · DSH 原始 session journal 证实 provider 首次请求 + 5 次
  重试均为 TIMEOUT；实际上下文只有 8 elements/4 claims/1 obligation。
  当前无凭据网络探针复现直连 UND_ERR_CONNECT_TIMEOUT（10.551s），7890
  线路 HTTP 401（0.159s）；故障定位至 provider 网络准备，撤回 586 proposals
  上下文压力假设与最终回合“两次 consultation”的重复日志计数。
  证据：evidence/agt-014/dsh-provider-timeout-diagnosis-2026-10-06.md。
- 2026-10-06 · VERIFY · DSH 家目录配置 HTTP/HTTPS proxy=127.0.0.1:7890 后，
  专用 3081 + 同一 `zai-coding-cn/glm-5.3-flash` 的最小真实咨询通过；DSH
  日志为 `consult captured`、`kind=act`、11.177s，测试 1/1 PASS（12.172s）。
  provider/submit_decision seam 已恢复，正式 Settings 全链路尚待复验。
- 2026-10-06 · VERIFY → IMPLEMENT → VERIFY · 7890 代理后的首次正式回合确认
  DSH 已返回模型决策，但 `effectClass=navigate` 被 Host 以 `unknown-action`
  在 dispatch 前拒绝；这不是网络超时，也没有产生设备 effect。随后把
  `effectClass` 与 `context.allowedEffects` 的逐字约束和
  `navigate:tap / back:tap / scroll:swipe-up` 映射加入 DSH prompt 与 Host
  policy projection，并补回归测试。
- 2026-10-06 · VERIFY · 修复后的真实 Settings 回合通过：3 次 consultation
  分别为 `tap Network & internet`、`tap Internet`、`noAction Wi-Fi=checked`；
  `facts.status=Completed`、`delivered=2`、两个 Guard 均 Allow，ADB journal
  两个 `DeliveryCompleted`，无 Wi-Fi 重复切换。证据：
  `evidence/agt-014/dsh-task2-proxy-runtime-token-fix-20261006/run-20261006-020702-678/`
  和 `evidence/agt-014/dsh-runtime-token-fix-2026-10-06.md`。
