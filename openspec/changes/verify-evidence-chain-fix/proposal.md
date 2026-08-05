## Why

delete-uia + roi-scroll-detection 后引擎遍历正常（target_found，12 步，4/4 动作成功），但 TraceTool verify 判定 `target_page_identity_not_verified`——引擎事实与 verify 判定矛盾。trace-analyzer 深度诊断定位三个独立 bug：空串短路导致身份匹配失效、post-target 分析不入证据链、analysis.jsonl 覆盖写破坏 append 语义。三个 bug 相互放大，必须一起修复才能使 verify 通过。

## What Changes

- **Bug 1 — IdentityMatches 空串守卫**：OCR 空识别 item 的 Name="" 导致 `Contains("")` 恒为 true，第一个空 name 短路命中→身份匹配失败。加 `IsNullOrWhiteSpace` 守卫。
- **Bug 2 — VisualPageAnalyzer 套 AnalysisWritingDecorator**：post-target 分析走裸 `IPageAnalyzer`，不经 decorator，快照永不落 `analysis.jsonl`。在 `CreateRunServices` 中 local provider 路径套 decorator。
- **Bug 3 — analysis.jsonl append 模式**：`AssetStagingWriter` tmp+move 整文件替换，N 次分析只留 1 行。`AssetSubmission` 加 `Append` 标志，`FileAssetStore` 按标志分支。

## Capabilities

### New Capabilities

（无。此变更系 bug 修复，不引入新功能能力。）

### Modified Capabilities

（无。修复实现层使其符合已有 spec 意图——D-197 append-only JSONL 语义、post-target 证据链完整性——spec 级行为不变。）

## Impact

- `src/UniClaw.TraceTool/LocateOneItemRule.cs` — Bug 1 判定层，+2 行守卫
- `src/UniClaw.Core/Observability/ITracePipeline.cs` — Bug 3a `AssetSubmission` record +1 字段
- `src/UniClaw.Host/Artifacts/FileAssetStore.cs` — Bug 3b `WriteAsync` 按 Append 分支
- `src/UniClaw.Host/HostServices/AnalysisWritingDecorator.cs` — Bug 3c 传 `Append: true`
- `src/UniClaw.Host/Commands/HostCommands.cs` — Bug 2 `CreateRunServices` VisualPageAnalyzer 装配
- `tests/UniClaw.TraceTool.Tests/` — Bug 1 单元测试
- `tests/UniClaw.Core.Tests/` — Bug 3 AssetSubmission 序列化测试
- `tests/UniClaw.Host.Tests/` — 集成测试 10.3 verify 通过
- `docs/prd/2026-08-05-verify-evidence-chain-fix-prd.md` — 上游 PRD（已提交）
