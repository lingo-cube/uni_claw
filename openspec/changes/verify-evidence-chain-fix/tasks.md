## 1. Bug 3a — AssetSubmission 加 Append 字段

- [x] 1.1 `src/UniClaw.Core/Observability/ITracePipeline.cs`：`AssetSubmission` record 加 `bool Append = false` 字段

## 2. Bug 3b — FileAssetStore 按 Append 分支

- [x] 2.1 `src/UniClaw.Host/Artifacts/FileAssetStore.cs`：`WriteAsync` 方法中检查 `append` 标志；true → `FileStream` with `FileMode.Append, FileShare.Read`；false → 保持 `AssetStagingWriter.WriteBytesAsync`

## 3. Bug 3c — AnalysisWritingDecorator 传 Append:true

- [x] 3.1 `src/UniClaw.Host/HostServices/AnalysisWritingDecorator.cs`：`SubmitSnapshot` 方法的 `AssetSubmission` 构造传 `Append: true`

## 4. Bug 1 — IdentityMatches 空值守卫

- [x] 4.1 `src/UniClaw.TraceTool/LocateOneItemRule.cs`：`IdentityMatches` 方法入口加 `IsNullOrWhiteSpace(actual) || IsNullOrWhiteSpace(expected)` → `return false`

## 5. Bug 2 — VisualPageAnalyzer 套 AnalysisWritingDecorator

- [x] 5.1 `src/UniClaw.Host/Commands/HostCommands.cs`：`CreateRunServices` 方法中 `VisualPageAnalyzer` 赋值改为 `accessor is not null ? new AnalysisWritingDecorator(providerBrain.PageAnalyzer, accessor, pipeline, assets.RunDirectory) : providerBrain.PageAnalyzer`

## 6. 构建验证

- [x] 6.1 `dotnet build` Release 全项目通过（UniClaw.Core + UniClaw.Host + UniClaw.TraceTool）

## 7. 单元测试

- [x] 7.1 `tests/UniClaw.TraceTool.Tests/`：`LocateOneItemRule` 测试——空串/空白串/null actual、空 expected、正向 containment 保持（参照 spec `verify-evidence-chain` Requirement 1）
- [x] 7.2 `tests/UniClaw.Core.Tests/`：`AssetSubmission` 序列化测试——Append 默认 false、Append=true 正确序列化
- [x] 7.3 `tests/UniClaw.Host.Tests/`：`FileAssetStore` 测试——Append 模式追加、非 Append 模式保持 tmp+move

## 8. 集成测试

- [ ] 8.1 运行 `scenario-locate`（local provider，无 UIA）→ verify 判定 `target_page_identity_verified`
- [ ] 8.2 检查 `analysis.jsonl` 行数 ≥ engine steps + post-target + reset polls
- [ ] 8.3 检查 `analysis.jsonl` 最后一行包含 expected identity 的 item 名

## 9. 全量回归

- [ ] 9.1 `dotnet test` 全量通过（Core + Host + TraceTool），已有测试不被破坏
