## 1. 核心修复 — TraversalEngine

- [ ] 1.1 `src/UniClaw.Core/Traversal/TraversalEngine.cs`：`Initialize()` 方法中 `effectiveMaxDepth` 计算提前到 `TraversalRuntimeContext` 构造之前（约 line 114），传给 `maxDepth` 参数。去掉 line 147-149 的重复计算，复用同一变量。

## 2. 构建验证

- [ ] 2.1 `dotnet build src/UniClaw.Core -c Debug` 通过

## 3. 仿真验证 — 修复后 L2 测试 PASS

- [ ] 3.1 `dotnet test --filter FixVerificationTests.DepthConstraint_StopsAtLevel2` — 从 FAIL 变为 PASS
- [ ] 3.2 `dotnet test --filter SettingsEnumerateRegression` — 保持 PASS
- [ ] 3.3 `dotnet test --filter FsmInvariant_SubframeDepthNeverExceedsMaxDepth` — 保持 PASS

## 4. 回归验证 — 已有测试不破坏

- [ ] 4.1 `dotnet test tests/UniClaw.Core.Tests --filter TraceReplay` — 全部 PASS
- [ ] 4.2 `dotnet test tests/UniClaw.Core.Tests` — 全量 Core 测试 PASS

## 5. 集成验证 — E2E enumerate

- [ ] 5.1 启动模拟器 → 打开 Settings → `--provider local` enumerate → 预期 `enumerated_all_first_level`（不复现 `settings_home_not_restored`）
