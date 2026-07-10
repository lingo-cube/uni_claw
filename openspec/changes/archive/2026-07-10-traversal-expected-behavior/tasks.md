## 1. Record 定义与基础结构

- [x] 1.1 创建 `CompletionExpectation` sealed record (Success, Reason, FinalState?) — `src/UniClaw.Core/Simulation/ExpectedBehavior/`
- [x] 1.2 创建 `PageCoverageExpectation` sealed record (Required, Forbidden) — 支持 "auto_derive" sentinel
- [x] 1.3 创建 `ElementCoverageExpectation` sealed record (Required, RequiredRatio=0.95)
- [x] 1.4 创建 `CollisionProof` sealed record (Text, ExpectedDistinct, ParentPages?)
- [x] 1.5 创建 `DfsPropertiesExpectation` sealed record (RootFirst, ParentBeforeChild, BackAfterForward)
- [x] 1.6 创建 `NumericAnchor` sealed record (TotalSteps, VisitedPagesCount, ActionHistoryCount, ElapsedSecondsMax)
- [x] 1.7 创建 `RuleResult` sealed record (RuleId, Passed, Message, Actual?)
- [x] 1.8 创建 `VerificationReport` sealed record (AllPassed, Summary, Details) — AllPassed 排除 numeric_anchor
- [x] 1.9 创建 `ExpectedBehavior` 顶层 sealed record (Scenario, Description, Completion, PageCoverage, ElementCoverage, CollisionProof, DfsProperties, NumericAnchor)

## 2. 序列化与 auto_derive 推导

- [x] 2.1 实现 `ExpectedBehavior.FromJson(string path)` — DomainJsonOptions 反序列化，处理 collision_proof 的 "auto_derive" 字符串/数组双态
- [x] 2.2 实现 `ExpectedBehavior.WithFixtureDerivation(StateFixture fixture)` — 替换 "auto_derive" sentinel: page_coverage → fixture 页面名, element_coverage → 非-readonly 元素 ID, collision_proof → 同 Text 不同页面组合

## 3. Verify 验证逻辑

- [x] 3.1 实现 `VerifyCompletion(TraversalResult)` — 比较 Success, CompletionReason, FinalState
- [x] 3.2 实现 `VerifyPageCoverage(TraversalResult)` — Required Contains 检查 + Forbidden 不存在检查
- [x] 3.3 实现 `VerifyElementCoverage(TraversalResult)` — 计算 RequiredRatio, 比较 ActionHistory 中覆盖率
- [x] 3.4 实现 `VerifyCollisionProof(TraversalResult)` — 按 Text 分组统计 VisitedPages distinct count
- [x] 3.5 实现 `VerifyDfsProperties(TraversalResult)` — RootFirst + ParentBeforeChild + BackAfterForward 顺序检查
- [x] 3.6 实现 `VerifyNumericAnchor(TraversalResult)` — ±5% tolerance 数值比较, 标记 informational
- [x] 3.7 实现 `ExpectedBehavior.Verify(TraversalResult)` — 调度 6 个维度验证, 组装 VerificationReport

## 4. JSON 预期定义文件

- [x] 4.1 创建 `tests/.../Baseline/Fixtures/expected/` 目录
- [x] 4.2 创建 `settings-full-traversal.json` — 场景1 全量遍历预期定义 (numeric_anchor: TotalSteps=145, VisitedPagesCount=19, ActionHistoryCount=38)
- [x] 4.3 创建 `settings-target-search.json` — 场景2 目标搜索预期定义 (page_coverage.required 手写, Forbidden=["Storage","Internal Storage","SD Card"])

## 5. 基线测试升级

- [x] 5.1 重构 `SimulationBaselineTests.cs` 场景1 — 从内联 Assert 替换为 ExpectedBehavior.FromJson + WithFixtureDerivation + Verify + Assert.True(report.AllPassed, report.Summary)
- [x] 5.2 重构 `SimulationBaselineTests.cs` 场景2 — 同上，目标搜索场景使用手写 page_coverage.required
- [x] 5.3 确保所有基线测试通过 (dotnet test --filter "FullyQualifiedName~Baseline")

## 6. 文档同步

- [x] 6.1 更新 `docs/system/layers/simulation-baseline.md` §2 — 七类规则映射到 ExpectedBehavior 子 record, 引用 Fixtures/expected/*.json 清单
- [x] 6.2 更新 `docs/system/constitution/constraints.md` — 新增 C-11 补充: ExpectedBehavior schema 锁定声明
- [x] 6.3 更新 `docs/system/decisions/log.md` — 记录 D-E1~D-E7 决策
