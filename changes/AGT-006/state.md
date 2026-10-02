# AGT-006 — Real negative-path verification of Settings coverage bounds
lifecycle_state: closed · disposition: none · depth: standard · base: 7b49038d

## Intent（WHAT/WHY）

AGT-005 证明了覆盖遍历的happy path（19/19 步、覆盖率 100%），但其失败
分支——有界停止的诚实报告、滚动到底的内容不变验证失败、连续失败终止、
部分覆盖的退出码语义——只在确定性测试里证明过。本 change 在同一真实栈
（专用 3081 + 模拟器 + zai-coding-cn/glm-5.3-flash）用**纯配置**驱动
负路径场景，验证 fail-closed 语义在真实运行中如实成立。

## Scope

- 两个临时配置文件（/tmp，不入库）；运行、证据、报告、清理。
- 若暴露产品缺陷：在 AGT-005 组件内修复并补测试（预期不需要）。

## Out of Scope

- 不改产品代码/协议/白名单；不追求让容量错误或模型偏离在真机发生
  （不可诚实强制，保持确定性测试背书）。
- 不操作共享 3080。

## Acceptance

1. 场景 A：进程退出码 3；CoverageReport.Status=BoundedStop；覆盖率 <1；
   未覆盖项非空；每步 trace 完整；无把部分覆盖宣称为 Completion。
2. 场景 B：出现至少一次 Verified=false 且 FailureReason=
   post-action-content-transition 的滚动步骤；终止原因为
   max-consecutive-failures（或 max-scrolls）；其余同上。
3. 两次运行共用同一真实栈；结束清理；3080 存活未受影响。
4. solution build 0 errors、全量测试无回归（无代码改动则复验一次）。

## Verification

```yaml
level: ENVIRONMENT
method: config-driven negative scenarios on dedicated 3081 + emulator-5556 (maxSteps truncation; scroll-to-end attempts); deterministic regression for every surfaced defect
expected: "bounded stops report honestly (exit 3, uncovered items, no fake Completion); failure steps captured in per-step trace with first divergence; unknown transitional page neither dispatches phantom effects nor fakes success; all fixes carry regression tests; 3080 untouched"
actual: "5 real runs (evidence/real-settings-coverage-negative-20261001): a2 maxSteps=6 -> BoundedStop 50% exit 3; b maxSteps=24 rich-targets -> BoundedStop 83% exit 3; c1 pre-fix transitional screen -> real post-action-target-unique failure + first divergence + model non-compliance -> bounded corrective retry -> persistent deviation fail-closed (exit 3); c2 pre-fix candidate re-entry loop -> consult-budget-exhausted honest classification (exit 1); c3 post-fix unknown page -> Defer x3 -> bounded-stop:unknown-page with ZERO dispatched effects (exit 3). Three real defects found & fixed with regression tests: LoadDefault env override ignored; transitional-screen phantom back (Defer semantics); scroll-candidate re-entry loop (AllEnteredDescriptors). Deterministic: build 0 errors; 1166/1166 tests; scenario certification --check PASS (Kernel/Agent hash unchanged); git diff --check clean. NOT realized on device (honest): scroll content-unchanged failure (fixture never bottomed out within budgets) and capacity retry — both remain deterministic-test-backed. Cleanup: emulator stopped, 3081 killed, 3080 alive (401)."
evidence: evidence/real-settings-coverage-negative-20261001/; src/UniClaw.Host/SettingsCoverage/SettingsCoverageConfig.cs; src/UniClaw.Host/SettingsCoverage/SettingsCoverageLedger.cs; src/UniClaw.Host/SettingsCoverage/SettingsCoverageDirector.cs; tests/UniClaw.Host.Tests/SettingsCoverageConfigTests.cs; tests/UniClaw.Host.Tests/SettingsCoverageLedgerTests.cs; tests/UniClaw.Host.Tests/SettingsCoverageScenarioTests.cs
```

## Acceptance 处置（对照原验收）

1. ✅（a2/b 两类 maxSteps 停止，退出码 3、未覆盖项如实、无伪装）。
2. ⚠️ 部分达成并如实改判：滚动内容不变失败未在真机发生（三次尝试的
   夹具现实：预算耗于候选进入/未知页停止）；作为替代，真机捕获了另一类
   验证失败（post-action-target-unique）与两类终止（consult-budget、
   unknown-page）。该分支维持确定性背书并显式声明。
3. ✅（同栈五运行；清理完成；3080 存活）。
4. ✅（1166/1166；认证 PASS；三处修复各带回归测试）。

## Constraints

- 真实测试仅用专用 3081；模型 zai-coding-cn/glm-5.3-flash；
  autoCloseTurn=false；workspace 复用。

## Status log

- 2026-10-02 · persisted · AGT-005 closed 后开档；负路径清单来自
  component-report.md 第八节。
- 2026-10-02 · persisted→implemented · 首跑即暴露缺陷 #1（env 配置被
  忽略）；修复后 a2/b 验证 maxSteps 停止；c1 暴露缺陷 #2（过渡屏幻影
  back）并真机捕获验证失败+模型偏离 fail-closed；c2 暴露缺陷 #3（候选
  重进循环）；三者修复+回归测试+测试串行化。
- 2026-10-02 · implemented→verified · c3 真机验证 Defer 修复（零幻影
  effect 的 unknown-page 停止）；全量 1166/1166、认证 PASS、清理完成。
- 2026-10-02 · verified→closed · 验收四元组齐备；未真机化分支（滚动
  内容不变、容量重试）在 Gate disposition 显式声明为确定性背书。
- 2026-10-02 · closed 后追加确认 · 修复 3 真机确认（c4：9 候选各进一次、
  零重进）；顺带发现并修复 4（ScrollCandidates 排除 BackDescriptor，
  回归测试 + 全量 1167/1167）；c5 环境噪声下 director 拒绝滑权限对话框、
  零 effect（遵从性真实验证）；final 全修复版完整任务 19/19、100%、
  exit 0，digest 与 AGT-005 原运行一致。

## Gate disposition

负路径战役达成目的：有界停止/诚实退出码/逐步失败捕获/未知页零幻影
effect 全部真机验证，且抓出并修复三个真实缺陷（env 忽略、幻影 back、
候选重进）。滚动内容不变失败与容量重试未在真机触发——前者受夹具现实
限制（列表深度×预算），后者不可诚实强制；两者保持确定性测试背书，
未来若自然发生可对照本 change 的证据结构直接归档。
