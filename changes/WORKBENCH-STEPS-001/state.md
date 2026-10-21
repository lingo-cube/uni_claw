# WORKBENCH-STEPS-001 — 可读步骤与 Workbench 提交
lifecycle_state: verified
disposition: none
depth: standard

## Intent
把既有会话步骤中的结构化字段改成可读动作与目标；完成本任务此前已授权的 Workbench 研究、P0、布局、卡片及集成改动的 scoped commit/push。

## Scope / constraints
仅 renderer/CSS/现有 renderer tests、README 和生成 client；不改输入投影、产品协议、Runtime、实际命令。public seam 沿用已批准的 renderWorkspaceHtml。共享目录有大量并行工作，提交只包含本任务归属路径；不包含其他 Agnes 研究或其他产品变更。
产品 profile 只原子更新 client 并保留备份；仅重载本任务拥有的 3082，保留 3080/3081。

## Decisions
优先已有 label/description；已知 tap 显示“点击”；其他动作保留原值；目标来自 targetDescriptor/targetRole，期望状态显式标识。每个对象步骤的完整字段原样转义显示在原生 details 中；不推断成功状态。

## Acceptance
- A1：真实 tap/target 步骤显示动作与目标，完整参数可展开，文字步骤保持原文。
- A2：未知动作、不完整对象保持可读且不臆造；所有文本/属性转义；决策仍与回执分离。
- A3：测试通过、bundle 一致，真实 DSH 既有会话中桌面/390px 展示与展开正常；不触发任务执行。
- A4：审查 staged paths 与 diff，只提交本任务；推送 origin/uni-harness 后核实远端 commit。

## Plan
RED/GREEN → build/regression → scope review → owned preview browser → curated evidence → scoped commit/push。

## Verification
level: ENVIRONMENT
method: 现有 renderer 接口测试、全量 Workspace/DSH 回归、bundle 重建、真实 DSH 既有会话只读浏览和截图审查。
expected: A1-A3 成立；A4 提交路径归属明确，随后执行提交/推送。
actual: 169/169；点击步骤可读且参数完整，刷新后保留展开；390px 无横向溢出；浏览器批次包含最终 client 完整字节。提交与推送待执行。
evidence: evidence/workbench-steps-20261022/verification.json、review.md、tests.txt、browser-checks.json、桌面/手机截图。

## Status
- 2026-10-22：用户授权下一步完成后提交推送；已同步远端确认 origin/uni-harness 与当前 HEAD 一致，index 为空；保存脏工作区和目标文件快照。

- 2026-10-22：planned → implemented：步骤展示改为来源字段摘要与可展开参数；修复对象之外空步骤的显示，不修改输入/执行。
- 2026-10-22：implemented → reviewed：累计 Workbench diff 与提交归属清单复核；未知动作保留、字段转义、原始参数保留与 authority 边界确认。
- 2026-10-22：reviewed → verified：169/169 与真实 DSH 浏览器参数/刷新/390px 检查通过；仅本任务拥有的 3082 重载。
