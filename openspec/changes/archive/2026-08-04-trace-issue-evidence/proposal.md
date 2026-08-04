## Why

verification 类失败（`target_page_identity_not_verified` 等）的 evidence 链断裂：**issues.jsonl 里有完整 fingerprint + detail，但 result.json 的 `issueFingerprints` 恒为空**（`ScenarioRunOutcome.IssueFingerprints` 无回填点，D-192 的 issueSink 只写 issues.jsonl 不更新 outcome）→ DiagnoseEngine 的 `issue_fingerprints` evidence 永远缺失、verification 类失败 confidence 恒 low，真实原因只藏在 issues.jsonl 的 detail 里，Agent/脚本无法结构化消费。

## What Changes

- **`TraceRunLoader`**：加载 run 时若存在 `issues.jsonl` → 反序列化每行 RunIssue（fingerprint/summary/detail/stepNumber）→ 聚合到 `TraceRun` 新增的 Issues 集合
- **`DiagnoseEngine`**：`result.IssueFingerprints` 为空但 issues 存在时 → 从 issues.jsonl 补全 `issue_fingerprints` evidence（含 detail 溯源，不再恒 low confidence）
- **无 Host 生产代码改动**（不触碰 issueSink / outcome 回填——那是源头修正，留作后续独立 change）

## Capabilities

### New Capabilities

（无）

### Modified Capabilities

- `trace-run-aggregate`: `TraceRun` 聚合新增 issues.jsonl 记录（fingerprint/summary/detail/stepNumber 集合）
- `trace-analyzer-cli`: diagnose 的 evidence 契约——`issue_fingerprints` evidence 在 result 缺失时由 issues.jsonl 补全，且 detail 进入 evidence 文本

## Impact

- `src/UniClaw.TraceTool/TraceRunLoader.cs` — issues.jsonl 检测与反序列化（~15 行）
- `src/UniClaw.TraceTool/TraceRun.cs` — 新增 `Issues` 集合属性
- `src/UniClaw.TraceTool/DiagnoseEngine.cs` — evidence fallback（~10 行）
- `tests/UniClaw.TraceTool.Tests/` — +3 测试（loader 读 issues / diagnose fallback / 无 issues 文件行为不变）
- 纯 TraceTool 侧；JSON 契约向后兼容（diagnose 输出 evidence 数组新增条目，schemaVersion "1" 不变）
