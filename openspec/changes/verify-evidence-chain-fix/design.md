## Context

delete-uia 移除 UIAutomator 后，locate-one-item verify 判定 `target_page_identity_not_verified`。引擎侧 `target_found`（12 步，4/4 动作成功）与 verify 侧矛盾。trace-analyzer 对 run `20260804T170915736Z-9d605e0c50ee465` 深度诊断定位三个独立 bug，均位于 verify 证据链路的不同层级：

- **判定层**（TraceTool）：`IdentityMatches` 空串短路
- **装配层**（Host）：post-target 分析不经 `AnalysisWritingDecorator`
- **I/O 层**（资产管道）：`FileAssetStore` 覆盖写破坏 append 语义

上游 PRD：`docs/prd/2026-08-05-verify-evidence-chain-fix-prd.md`。

## Goals / Non-Goals

**Goals:**
- 修复 `IdentityMatches` 空串短路，使 OCR 空识别 item 不污染身份匹配
- 使 post-target 页面分析写入 `analysis.jsonl`，verify 能读取到 target 点击后页面身份
- 使 `analysis.jsonl` 为 append-only，每次分析一行，不被覆盖

**Non-Goals:**
- 不修改 `ScenarioRunOutcome.SuccessEvidence = default` 序列化异常（pre-existing bug，独立 PRD）
- 不引入新 capabilitiy——这是实现修正，不是功能变更
- 不改动非 local provider 路径的 verify 行为

## Decisions

### D1: IdentityMatches 守卫在方法入口，不在上游

**选择**：在 `IdentityMatches` 方法入口加 `IsNullOrWhiteSpace` 守卫，返回 `false`。

**替代**：在 fallback 逻辑（`LocateOneItemRule.cs:42-45`）中过滤空 name item，不传给 `IdentityMatches`。

**理由**：守卫在方法入口是防御式编程——`IdentityMatches` 是公共方法，调用方不应假设输入非空。过滤空 item 虽然也能解决本次 bug，但如果新增调用点忘了过滤，同类 bug 会重现。方法入口守卫是"契约层"修正，上游过滤是"调用层"规避。

### D2: VisualPageAnalyzer 套 AnalysisWritingDecorator，不显式提交

**选择**：在 `CreateRunServices` 中 local provider 路径（`accessor is not null`）对 `VisualPageAnalyzer` 套 `AnalysisWritingDecorator`。

**替代**：在 `HostCommands.cs:925` 行后显式序列化 `finalAnalysis` 并 `pipeline.Submit()`。

**理由**：
1. 复用已有 `AnalysisWritingDecorator.SubmitSnapshot` 序列化逻辑，不重复造轮子
2. reset 验证的 `AnalyzeUntilSettledAsync` 也走 `VisualPageAnalyzer`（[HostCommands.cs:1129](src/UniClaw.Host/Commands/HostCommands.cs#L1129)），decorator 自动为 reset 阶段的分析 poll 也写快照——更多证据，无副作用
3. `AnalysisWritingDecorator` 不做缓存，只做写入 + accessor 更新——D-19x 的"不走 `InvalidatingPageAnalysisCache`"约束完整满足
4. 非 local provider 路径（`accessor is null`）保持裸 analyzer，不影响 AI provider 行为

**约束验证 — D-19x**：
- D-19x 要求 reset 验证"不走 `InvalidatingPageAnalysisCache`"，原因是缓存会把首帧/半渲染的退化结果毒化引擎
- `AnalysisWritingDecorator` 只调用 `_accessor.Current = result` 和 `_pipeline.Submit()`——不缓存、不拦截 `AnalyzeCurrentPageAsync` 的返回值
- reset 阶段 decorator 写入的 poll 快照是附加证据，不影响 correctness（`RunEvidenceLoader` 取最后一行）

### D3: AssetSubmission.Append 显式标志，不按文件名推断

**选择**：`AssetSubmission` record 加 `bool Append = false` 字段，`FileAssetStore.WriteAsync` 按此分支（`FileMode.Append` vs `tmp+move`），`AnalysisWritingDecorator` 传 `Append: true`。

**替代**：`FileAssetStore` 按 `relativePath.EndsWith(".jsonl")` 自动判断 append。

**理由**：
1. 显式优于隐式——`Append` 是提交者的意图表达，属于 `AssetSubmission` 数据对象的职责
2. `EndsWith(".jsonl")` 不能覆盖"某个 jsonl 其实需要覆盖写"的场景（虽然当前不存在，但语义上这是个假设，不是契约）
3. 默认 `false` 保持所有现有提交行为不变（截图等二进制资产）

**I/O 语义**：
- Append: `new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.Read)` — 允许并发读（diagnose/verify 可在 run 执行期间读取）
- Overwrite: 保持现有 `AssetStagingWriter.WriteBytesAsync` tmp+move 原子写入

## Risks / Trade-offs

**R1: reset 阶段 poll 快照增加 analysis.jsonl 噪音**
- Risk: D2 使 `AnalyzeUntilSettledAsync` 的每次 poll（最多 timeout/1s 次）都写一行 analysis.jsonl
- Mitigation: `RunEvidenceLoader` 只取最后一行——多几行不影响 verify 判断。文件大小可控（每行 ~1-5KB，30 行 = 150KB max）
- Trade-off: 接受噪音换取 code reuse 和 reset 阶段可观测性

**R2: Accessor 更新竞态**
- Risk: post-target 分析走 decorator 后会更新 `_accessor.Current`——但此时引擎已完成，`VisionScreenStateProvider` 不再被查询
- Mitigation: 实际上 zero-risk，但代码层面不显式保证
- Trade-off: 接受隐式时序安全（post-target 在 engine.RunAsync 返回之后才执行）而非额外引入"只写不更新 accessor"的 decorator 变体

**R3: Append 模式无原子性保证**
- Risk: `FileMode.Append` 不是原子操作——写入中途崩溃可能留下半行
- Mitigation: 每行是独立 JSON 对象 + `\n` 终止符，`RunEvidenceLoader` 已有 `try-catch JsonException` 跳过格式错误行（[RunEvidenceLoader.cs:74-77](src/UniClaw.TraceTool/RunEvidenceLoader.cs#L74-L77)）。半行 → JSON parse fail → 静默跳过 → 取前一行（合法 JSON）作为 last row
- Trade-off: 接受"可能丢最后一行" vs 实现复杂度（atomic append 需要 journal 或 2-phase write，过度工程）
