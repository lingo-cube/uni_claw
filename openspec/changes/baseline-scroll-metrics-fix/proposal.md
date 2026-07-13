## Why

基线测试报告中的滚动指标全部为 0，导致滚动场景测试无法验证实际滚动行为。`ScrollableBaselineTests.cs` 全部 6 个滚动场景生成的报告不准确，`BaselineReportCollector` 硬编码滚动指标，`ExpectedBehavior.Verify` 未验证滚动字段。

这个问题影响滚动场景的基线测试有效性，无法检测滚动逻辑是否正确执行。

## What Changes

- **修复 `BaselineReportCollector.BuildActualNumeric` 方法**
  - 从 `ScrollableMockActionExecutor.ScrollHistory` 计算滚动指标
  - 从 `ScrollableMockVisionService` 获取当前进度
  - 移除硬编码的 `JumpDetected`, `JumpRecovered`, `AdaptiveStepIncreases` (暂时保持 0，标记为 Phase 3)

- **更新 JSON 预期文件**
  - 更新 6 个滚动场景的 `numericAnchor` 滚动字段占位值
  - 文件: `tests/UniClaw.Core.Tests/Baseline/Fixtures/expected/scroll/*.json`

- **(可选) 扩展 `ExpectedBehavior.Verify`**
  - 在 `VerifyNumericAnchor` 中添加滚动指标验证逻辑
  - 滚动指标当前只在报告中展示对比，不作为 CI-blocking 规则

## Capabilities

### New Capabilities
- `baseline-scroll-metrics`: 基线测试滚动指标收集与验证

### Modified Capabilities
- 无现有需求变更，仅修复数据收集实现

## Impact

**受影响的代码:**
- `tests/UniClaw.Core.Tests/Baseline/BaselineReportCollector.cs` — 修改 `BuildActualNumeric` 方法
- `tests/UniClaw.Core.Tests/Baseline/Fixtures/expected/scroll/*.json` — 更新 6 个文件的数值
- `src/UniClaw.Core/Simulation/ExpectedBehavior/ExpectedBehavior.Verify.cs` — (可选) 扩展验证

**受影响的测试:**
- `ScrollableBaselineTests.cs` 全部 6 个滚动场景

**不包含的变更 (Phase 3 Future Work):**
- 不引入新的 `ScrollStatistics` 数据结构
- 不修改 `TraversalResult`
- 不集成 `ScrollHandler` 到 `TraversalEngine`
- `JumpDetected`, `JumpRecovered`, `AdaptiveStepIncreases` 暂时保持 0
