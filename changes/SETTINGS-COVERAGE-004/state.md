# SETTINGS-COVERAGE-004 — 未知页面咨询与弹窗识别修复
lifecycle_state: closed · disposition: none · depth: standard · base: de976dd3

## Intent
最后一轮 Settings 运行在 `android.settings` identity-only 路由上由 Director 本地返回 Defer，绕过了 Agent；同时真实 `com.android.settings:id/*` 弹窗资源没有命中 popup whitelist。修复两条 Host seam，保持 Kernel/vision authority 不变。

## Scope
- unknown-route re-observe 必须经过 Agent consultation；只接受同一DecisionId的已授权有界选项（Defer、配置back、可见容器scroll及诚实blocked），仍经原guard。原only-Defer规则已由AGT-019 Owner Q7修订。
- unknown-route 上收到选项外Act、Plan、Policy、null或错DecisionId时继续fail-closed，不产生Effect；不得把有界恢复扩为普通导航许可。
- popup 默认白名单覆盖真实 Settings 命名空间的 alertTitle/parentPanel/buttonPanel。
- 保留 existing bounded-stop 与 popup obstacle 流程。

## Acceptance
- A1：unknown route 的每次重观察都产生 Agent request/consult record。
- A2：同DecisionId的有界选项按AGT-019 Q7处理；选项外或不相关响应不进入Kernel Effect，安全Defer或明确拒绝，保持零Effect。原“所有非Defer拒绝”保留为早期历史规则，不覆盖后继已授权恢复。
- A3：真实 namespaced popup XML 分类为 `present`。
- A4：全量测试与 Host coverage tests 通过。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: "Host Director unknown-route tests + popup classifier tests + full solution test"
  expected: "Agent is consulted for unknown route; unsafe response is rejected; namespaced popup is present"
  actual: "unknown-route Director tests, namespaced popup classifier tests, full solution 1541-test run, and two registered-emulator runs pass the targeted behavior: Agent requests appear for unknown-route Defer and popup obstacle Clear/CANCEL"
  evidence: "tests/UniClaw.Host.Tests/SettingsCoverageDirectorPlanTests.cs; tests/UniClaw.Host.Tests/SettingsPopupClassifierTests.cs; evidence/settings-coverage-004/live-fix-summary.json"
```

## Status log
- 2026-10-15 · UNDERSTAND → RESOLVE → PERSIST · 根据 leader-pass8 request/trace 与最后截图定位两个 Host seam。
- 2026-10-15 · PERSIST → IMPLEMENT → VERIFY · unknown-route consultation now calls Agent and accepts only correlated Defer; Settings namespaced popup IDs classify present; focused tests pass.
- 2026-10-15 · VERIFY · registered emulator confirmed unknown-route Agent requests and popup obstacle Agent directives; traversal run was intentionally stopped after the targeted branch evidence was captured.


## 2026-10-22范围复核
原A2“仅接受Defer”是修复时的unknown分支规则，后续AGT-019所有者Q7明确扩为有界选项集：再观察、已配置back、可见容器scroll及诚实blocked，仍经原guard，选项外Act/Plan/Policy拒绝。相关正负测试在本轮53项中全部通过。此次只记录已经授权的后继语义，不新放宽执行权限、不回滚后继恢复。

本任务验收等级原为DETERMINISTIC，不要求完整Settings树；旧两次定向live人为停止不阻止这项有限修复收口。全树仍归SETTINGS-COVERAGE-003/005、RUN-006。保留旧1541的历史声明，当前全套证据改指ENG-OPT-001冻结2024项TRX，不假称本切片重跑全套。

## 2026-10-22 Verification
level: DETERMINISTIC
method: 两个既有Host公开测试类，当前53项；逐项核对ENG-OPT-001最终8项目TRX及AGT-019 Q7/正负恢复用例
expected: A1-A4在原有限修复与已接受后继语义下有证据，未知/不安全提案不获得新权限
actual: 当前53/53，原完整回归2024/2024可追溯；独立Standards/Spec及补审无剩余阻塞，按有限修复范围关闭
evidence: evidence/ordered-closeout-20261022/settings004-verification.json；settings004-current.trx；evidence-readiness.md；evidence/eng-opt-001/final-tests/

- 2026-10-22 · VERIFY→REVIEW · 补准确TRX指针，核对原有限修复与后继AGT-019 Q7语义；无源码修改，不新增全树验收条件。

- 2026-10-22 · REVIEW→VERIFY→CLOSED · Standards指出旧Scope/A2与Q7后继语义冲突，已明确替代关系并补审关闭；Spec0阻塞。当前53/53及冻结全套TRX、真实XML样本均可回指，不要求全树、不改产品权限。
