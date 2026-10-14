# SETTINGS-COVERAGE-004 — 未知页面咨询与弹窗识别修复
lifecycle_state: verified · disposition: none · depth: standard · base: de976dd3

## Intent
最后一轮 Settings 运行在 `android.settings` identity-only 路由上由 Director 本地返回 Defer，绕过了 Agent；同时真实 `com.android.settings:id/*` 弹窗资源没有命中 popup whitelist。修复两条 Host seam，保持 Kernel/vision authority 不变。

## Scope
- unknown-route re-observe 必须经过 Agent consultation；只接受同一 DecisionId 的 Defer。
- unknown-route 上收到 Act/Plan/Policy/null 时继续 fail-closed，不产生 Effect。
- popup 默认白名单覆盖真实 Settings 命名空间的 alertTitle/parentPanel/buttonPanel。
- 保留 existing bounded-stop 与 popup obstacle 流程。

## Acceptance
- A1：unknown route 的每次重观察都产生 Agent request/consult record。
- A2：Agent 非 Defer 响应不进入 Kernel Effect，仍返回安全 Defer。
- A3：真实 namespaced popup XML 分类为 `present`。
- A4：全量测试与 Host coverage tests 通过。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: "Host Director unknown-route tests + popup classifier tests + full solution test"
  expected: "Agent is consulted for unknown route; unsafe response is rejected; namespaced popup is present"
  actual: "unknown-route Director tests, namespaced popup classifier tests, and full solution 1540-test run pass"
  evidence: "tests/UniClaw.Host.Tests/SettingsCoverageDirectorPlanTests.cs; tests/UniClaw.Host.Tests/SettingsPopupClassifierTests.cs"
```

## Status log
- 2026-10-15 · UNDERSTAND → RESOLVE → PERSIST · 根据 leader-pass8 request/trace 与最后截图定位两个 Host seam。
- 2026-10-15 · PERSIST → IMPLEMENT → VERIFY · unknown-route consultation now calls Agent and accepts only correlated Defer; Settings namespaced popup IDs classify present; focused tests pass.
