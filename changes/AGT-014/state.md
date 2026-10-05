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
actual: deterministic + real-device chain partial PASS; full solution 1307/1307 and Host.Dsh build pass; real DSH handshake accepted but two 75s consultation timeouts, zero effects; owner decision required
evidence: evidence/agt-014/verification-2026-10-06.md
```

## Status log

- 2026-10-06 · UNDERSTAND → RESOLVE → PERSIST · 明确真实三任务、故障路径、
  证据要求和不可越过的架构/模型/协议边界。

- 2026-10-06 · PLAN → IMPLEMENT → VERIFY · 真实 API35 emulator 链路通过 typed、
  Host Wi-Fi、ADB、坐标和有界 coverage；首轮环境故障已做局部修复。
- 2026-10-06 · VERIFY · 真实 DSH handshake 成功，但 glm-5.3-flash consultation
  两次 75s 无响应，586 proposals/首帧感知 63.7s；未产生 effect，保留为需 Owner
  裁决的运行边界，未关闭 Change。
- 2026-10-06 · VERIFY → IMPLEMENT → VERIFY · 全量并行回归暴露 process-wide
  config-path 环境变量竞态；将相关 Host tests 收入不可并行集合后 Host.Tests
  恢复 153/153 PASS。
