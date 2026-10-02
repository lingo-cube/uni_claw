# AGT-008 — Persist per-step visual evidence in coverage traversal
lifecycle_state: closed · disposition: none · depth: standard · base: 7b49038d

## Intent（WHAT/WHY）

AGT-007 复盘暴露：真实运行中截图与 hierarchy XML 仅作内存消费（视觉服务/
typed 解析），证据链只有 captureId 引用而无实体文件——事后无法回答
"点击发生时屏幕上到底是什么"（Security & privacy 点击失败与 Dismiss
卡片归属均因此不可确证）。本 change 把每观察周期的截图 PNG 与 XML 作为
证据工件落盘：文件名 = captureId，步骤记录的 postCaptureId 可直接关联。

## Scope

- SettingsCoverageConfig 增加可选 `evidence` 段（persistScreenshots/
  persistHierarchies，缺省 true——追溯性 fail-open 到"留存"侧）。
- SettingsTraversalLiveFeed 支持证据目录：每周期写 `{captureId}.png` 与
  `{captureId}.xml`（dump 失败/探针跳过 = 该周期无 XML，如实缺文件）。
- SettingsCoverageRunner 创建 `run-*/evidence/` 并接线；facts.json 汇总
  证据文件计数。
- 单测（writer + 配置解析）+ 全量回归 + 一次真机完整任务验证文件链。

## Out of Scope

- HostRunner（Wi-Fi 闭环）同款接线（后续按需另立）；视觉内容解析/比对；
- 3080；协议/Kernel 改动。

## Acceptance

1. 真机完整任务后：`run-*/evidence/` 含与观察周期数一致的 PNG（每个
   非空）与对应 XML；每个步骤的 postCaptureId 都能找到同名文件。
2. 配置解析：缺省 true；显式 false 关闭；非法值 fail closed。
3. 全量测试无回归；结束清理；3080 未动。

## Status log

- 2026-10-02 · persisted · 开档：AGT-007 复盘的直接后续。

## Verification

```yaml
level: ENVIRONMENT
method: config/writer unit tests; full regression; one real full mission on dedicated 3081 + emulator-5556 with persistence on
expected: "evidence/{captureId}.png+xml per observation cycle; every step postCaptureId resolves to both files; mission behavior unchanged; explicit-false disables; invalid value fails closed"
actual: "real run: 40 evidence files (20 cycles x png+xml), zero empty, 19/19 steps resolve; mission 19/19 steps CoverageComplete exit 0 with digest identical to prior successful runs (9A8BC...); facts.evidenceFiles=40; deterministic: 4 new tests, full suite green, certification PASS (Kernel/Agent unchanged); cleanup done, 3080 alive"
evidence: evidence/real-settings-coverage-evidence-20261002/; src/UniClaw.Host/SettingsTraversalLiveFeed.cs; src/UniClaw.Host/SettingsCoverage/SettingsCoverageConfig.cs; src/UniClaw.Host/SettingsCoverage/SettingsCoverageRunner.cs; tests/UniClaw.Host.Tests/SettingsCoverageConfigTests.cs; tests/UniClaw.Host.Tests/SettingsCoverageScenarioTests.cs
```

## Status log

- 2026-10-02 · persisted · 开档：AGT-007 复盘的直接后续。
- 2026-10-02 · implemented · 配置 evidence 段（缺省全开/fail closed）、
  feed 按周期落盘 {captureId}.png/.xml、runner 接线 + facts 计数、
  4 个新测试。
- 2026-10-02 · verified→closed · 真机完整任务验证：40 文件、19 步全关联、
  行为零变化（digest 一致）；清理完成，3080 存活。

## Gate disposition

追溯缺口已闭合：逐步视觉证据成为默认产物（可配置关闭）。此后同类悬案
（点击时屏幕是什么、某按钮属于哪张卡）可直接打开该步骤的 png/xml 复盘。
HostRunner（Wi-Fi 闭环）的同款接线未做——按需另立小 change。
