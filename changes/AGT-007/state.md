# AGT-007 — Real scroll-to-end failure verification
lifecycle_state: closed · disposition: none · depth: standard · base: 7b49038d

## Intent（WHAT/WHY）

AGT-006 关闭后仅剩的可诚实真机触发缺口：swipe 在列表底部"内容不变"
（post-action-content-transition）验证失败 ×3 → max-consecutive-failures
诚实停止。修复 3（候选重进）就位前预算总被浪费、走不到底；现在饥饿配置
（targets=1、scrollDiscovered=99、maxSteps=60）应能逐个进入候选后滚到
列表底部触发该分支。若候选因路由指纹碰撞不可进入（c4 的 Security &
privacy 现象）导致提前停止，则顺手加固指令引擎（跳过近期失败的候选）。

## Scope

- 真机运行 + 证据 + 报告 + 清理；必要时的小加固 + 回归测试。
- 不改协议/Kernel 语义；不操作 3080。

## Acceptance

1. 真机出现 ≥1 步 Verified=false 且 FailureReason=post-action-content-
   transition 的 swipe；终止原因为 max-consecutive-failures（或如实记录
   为何没到达）；退出码非 0、无伪装。
2. 若做加固：针对性回归测试 + 全量测试无回归。
3. 结束清理，3080 存活。

## Status log

- 2026-10-02 · persisted · 开档：饥饿配置 + 修复 3/4 就位的真机滚动到底实验。

## Verification

```yaml
level: ENVIRONMENT
method: 4 real runs (d1-d4) with starved config on clean/dirty device states; deterministic regression for the shipped hardening
expected: ">=1 real swipe step fails with post-action-content-transition, terminating in max-consecutive-failures; or honest documentation of why unreachable"
actual: "branch unreachable on this fixture, cause deterministic (d1/d3/d4 byte-identical digests): bottom entry 'Security & privacy' fails route-change verification (step 20), then low-battery notification card 'Dismiss' pollutes candidates and captures the model (b-23 deviation fail-closed). Shipped hardening: FailedEntryDescriptors skip (regression FailedEntryCandidate_IsNotReEntered... plus FailedEntryCandidate_IsSkipped_NotRetried); all runs fail-safe (zero unsafe effects, exit 3/1, honest reports). d2 independently re-verified unknown-page stop. Branch remains deterministic-test-backed."
evidence: evidence/real-settings-coverage-negative-20261001/ (run-d1..d4 + README addendum); src/UniClaw.Host/SettingsCoverage/SettingsCoverageLedger.cs; tests/UniClaw.Host.Tests/SettingsCoverageLedgerTests.cs
```

## Status log

- 2026-10-02 · persisted · 开档：饥饿配置 + 修复 3/4 就位的真机滚动到底实验。
- 2026-10-02 · implemented · d1 确认卡点（S&H 不可验证 + Dismiss 卡片）；
  加固 5：失败候选跳过 + 回归测试。
- 2026-10-02 · verified→closed · d3/d4 与 d1 逐字节一致（确定性阻塞）；
  判定分支不可达归因于真实 UI 形态；d2 复验未知页停止；全部运行 fail-safe。

## Gate disposition

滚动到底（内容不变）失败在本夹具不可达的原因已实证归档：底部不可验证
入口（路由碰撞）+ 通知卡片候选污染。该分支维持确定性测试背书。战役净
收获：修复 5（失败候选跳过）与四次额外的 fail-safe 复验。Settings 遍历
主线（正常 + 负路径 + 加固）至此全部关闭；后续如需在受控浅列表夹具
（自建 app）上触发该分支，可另立实验。
