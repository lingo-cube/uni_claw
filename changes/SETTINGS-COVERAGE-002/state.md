# SETTINGS-COVERAGE-002 — 决策者驱动的 Android Settings 全遍历验证
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 302a5955

## Intent
以当前 Settings Coverage 抽象计划和 Host 执行链为对象，进行一次环境级、有界、多轮的真实 Android Settings 遍历。由 Leader 代 UniAgent 生成/选择决策，记录 Host 对计划的执行、拒绝、修复和终局证据，反推抽象计划的生成经验与能力边界。

## Scope
- 使用注册 Android 模拟器和专用 DSH 线路。
- 覆盖配置声明的一级目标、滚动发现项、二级页面、返回和重复进入。
- 每步记录 observation → abstract directive → agent decision → Host guard → effect receipt → post-action verification → ledger update。
- 失败允许 3–5 轮有界修复；每轮保留原始证据和修复理由。

## Out of Scope
- 不宣称遍历 Android Settings 全部未知菜单。
- 不绕过 Host Director、Kernel assurance、Effect boundary 或 fail-closed 规则。
- 不修改产品代码或配置以迎合本次运行；若发现缺陷，另开修复 Change。

## Acceptance
- A1：environment preflight、设备、DSH、配置和模型身份完整留痕。
- A2：每个决策环节都有输入、抽象计划、决策、Host 处置、Effect、验证和证据引用。
- A3：配置声明的 coverage target 与实际覆盖逐项可对账。
- A4：失败按 A–F 分类，记录 first divergence point；每轮修复不超过 5 轮。
- A5：终局诚实区分 Completed、BoundedStop、FailClosed、EnvironmentBlocked。
- A6：报告明确抽象计划生成经验、Host 能力边界和下一步 Change 建议。

## Verification
```yaml
verification:
  level: ENVIRONMENT
  method: "registered Android emulator + dedicated DSH + Host.Dsh --settings-coverage + per-step trace/report/facts"
  expected: "计划驱动的 Settings coverage 完整执行或诚实终止；每个环节有证据"
  actual: "PASS as an environment characterization report, not full-tree completion: baseline Host run 35/35 verified with 16/16 first-level; deep plan run operator-stopped after 94 journal prepare/submission/receipt frames; direct Leader-file runs exposed no-response and Host policy guard boundaries; final direct first-level run 12/12 verified and Kernel emitted Completion while CoverageReport remained BoundedStop; post-change solution tests 1533/1533"
  evidence: "evidence/settings-coverage-002/; docs/analysis/settings-full-traversal-decision-maker-2026-10-15.md"
```

## Status log
- 2026-10-15 · UNDERSTAND → RESOLVE → PERSIST → PLAN · 明确以现有 settings-coverage.yaml 为抽象计划源，由 Leader 代 UniAgent 做决策并保留 Host 执法证据。
- 2026-10-15 · PLAN → IMPLEMENT · 开始环境预检和真实遍历。
- 2026-10-15 · IMPLEMENT → REVIEW → VERIFY · 完成五轮真实/认知测试：默认 Director、扩展 nested plan、Leader-file advisory、Host policy guard 负向、第一层安全回退；未把任何 bounded/partial 结果宣称为全树完成。
- 2026-10-15 · CLOSED · 报告、系统资产、每轮证据和抽象计划改进建议已落盘；后续修复建议另开 Change。
