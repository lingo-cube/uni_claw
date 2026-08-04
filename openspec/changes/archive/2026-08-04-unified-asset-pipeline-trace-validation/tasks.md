# Tasks

> 权威细节：`docs/prd/2026-08-04-unified-asset-pipeline-trace-validation-prd.md`（§6 改动清单、§7 验收）
> Specs: trace-pipeline / run-layout-v2 / trace-based-validation (new) + trace-analyzer-cli / file-trace-storage / run-metadata-enrichment / integration-test-config (modified)

## 1. Core — 管道公共实现

- [x] 1.1 `ITracePipeline`（Submit(AssetSubmission)/DrainAsync）+ `AssetSubmission`（category/bytes/relativePath）+ 分类模型（record_type + `asset.*`）；公共实现迁入 StepAssetSink 逻辑（bounded Channel 256 + SingleReader 批量 flush 50ms/64 条 + 幂等 DrainAsync）——Channel 用 net10 内建（无包引用）
- [x] 1.2 `IPipelineFailureSink`（写失败通知接口）+ `PipelineStats`（Accepted/Dropped/WriteFailures，DrainAsync 后读取）
- [x] 1.3 `IAssetStore`（Write/Read/Exists/List，键 = `{runId}/{relativePath}`，接口非实现）；事件侧复用现有 ITraceStorage/FileTraceStorage
- [x] 1.4 查询接口：`ITraceEventQuery` + `IAssetQuery`（只读分面 Read/Exists 无 Write，per-run runId 注入）+ `TraceQueries` 聚合（分析器面只暴露读）
- [x] 1.5 信息模型：ai.evidence 引用事件契约（相对 evidence_path/evidence_type/byte_count）+ TraceFields 45→48 键 + `TraceSpanFields.AiEvidence` profile（Basic: path/type；Extended: byte_count）+ SpanFieldLevelsTests 覆盖更新
- [x] 1.6 V2 布局模型（常量 + 纯路径函数）；`RunAssetVocabulary.SchemaVersion` "1"→"2" + manifest 顶层 bump

## 2. Host — 装配与布局迁移

- [x] 2.1 **删除 StepAssetSink**；装配 Core 管道（backend file + location + runId 注入）；DrainAsync 后读 PipelineStats → 写/扩展 `assets.sink_failure` 汇总 trace 事件（metadata: failed/accepted/**dropped**，复用 HostCommands.cs:882 检查点）；订阅 IPipelineFailureSink → issueSink（`asset_write_failed`，path + exception）
- [x] 2.2 `FileAssetStore`（staging 原子写 + writeGate）；提取 `AssetStagingWriter`（tmp+move）与 RunAssets 共享（E4）
- [x] 2.3 V2 布局迁移：producers 提交 relativePath（runId 装配注入 → `assets/{runId}/…`）；steps/、analysis.jsonl 移入资产空间
- [x] 2.4 元数据 V2（manifest 资产清单/引用更新，移除 safetyDecimals 项）+ 配置：integration.config `storage` 段（backend 键，location 复用 `emulator.outputRoot`）+ `providers.local.evidenceStorage` 门控（默认 false）；入口边界：测试链路 L1→L3 显式注入（不经 CLI env 回退）；直跑用 `UNICLAW_ASSET_BACKEND`（默认 file）+ 现有 `UNICLAW_OUTPUT` + `UNICLAW_EVIDENCE_STORAGE`（默认 off）
- [x] 2.5 **移除 RunAssetSafetyDecisionSink 落盘**（safety-decisions.jsonl + steps/{n}/safety-decision.json）——safety 决策只写 trace `safety.*` 事件（TraceSafetyDecisionSink 全字段已覆盖）；manifest 资产清单删除 safetyDecimals 项
- [x] 2.6 run 结束写 result.json：`status="pending_verification"` + 引擎事实；verificationCriteria 快照 → 独立 `criteria.json`；删除 ScenarioCompletionVerifier locate 分支（~60 行 → TraceTool）；enumerate 分支不动
- [x] 2.7 P3.1 修复：hook 异常（BeginStepAsync/capture 失败）不再被 FireAsync Log-and-Continue 静默吞掉——issueSink trace 条目
- [x] 2.8 LocalVisionProvider：注入 `ITracePipeline?` + `ITraceContextProvider` + evidenceStorage 门控（null/off → 完全 no-op）；响应解析前（L89）构建相对路径 `vision-evidence-{stepSpanId}-{seq}.json` → `pipeline.Submit(...)` + 同步 `RecordEventAsync("ai.evidence", parent=stepSpanId, attrs={evidence_path(相对)/evidence_type/byte_count})`；spanId 经 `EngineStepSpanContext.CurrentSpanId`；per-step seq 防 ai.call 重试覆盖

## 3. TraceTool — 规则引擎与命令

- [x] 3.1 读入口版本分发：manifest.schemaVersion → "1" V1 解析器 / "2" V2 解析器 / unknown → loud error（exit code + stderr）
- [x] 3.2 文件查询实现 + 配置驱动装配（config → TraceQueries）；分析器注入 TraceQueries（后端/组合替换不改分析器代码）；MVP：CLI 参数即配置（位置参数显式必填；后端默认不定死）；装配函数形状保留（将来 --backend/--config 只换装配源）
- [x] 3.3 `RunEvidenceLoader`（run 目录 → VerificationInput 重建；DI `IAssetQuery` per-run runId 注入；读前 schemaVersion 分发；向装配暴露 manifest 元数据——taskId/mode/scenarioId/providerId 作默认，显式 CLI 覆盖）
- [x] 3.4 `VerifyEngine` + `LocateOneItemRule`（D-201 规则平移：last analysis row Items 匹配 expectedPageIdentities；targetActionExecuted = completionReason==target_found && 成功 action；identity 回退；click_target_matches_identity 改读 trace `safety.*` 事件）
- [x] 3.5 命令：`verify --run <dir>`（status 不限）/ `verify --dir [--status pending] [--task-id]`（仅 pending，写回前重读）/ `watch --run-id <id> --dir <root> [--interval]`（叶子目录名 == runId 定位，>1 匹配报错；轮询 pending_verification；自动 verify；退出码 = verify 的）；退出码 0=verified · 1=not_verified · 2=usage/dir · 3=evidence missing；写回仅 pending（非 pending 只报告不写回；原子 tmp+move；失败 append issues.jsonl）；`--format json` 单 JSON 文档
- [x] 3.6 单元测试：规则层（success / identity-fallback / not_verified / evidence_missing）+ 写回幂等 + 临时 run 目录构造

## 4. 测试 — E2E 链路

- [x] 4.1 RunScenarioAsync 尾部：调用 verify CLI → 解析 verdict → 断言；失败时把 verdict summary 合并进测试失败信息

## 5. agent — 文档

- [x] 5.1 trace-analyzer.md L4：补 verify/watch/批量命令 + 职责声明（成功 = C# 规则；agent = 归因/解读）+ 补 interactive 子命令缺失项

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| Core（管道/接口/布局模型） | `docs/prd/2026-08-04-unified-asset-pipeline-trace-validation-prd.md` §2-4 + `docs/superpowers/specs/2026-08-04-unified-asset-pipeline-design.md` |
| Host（装配/元数据/V2 迁移） | `docs/prd/2026-08-04-unified-asset-pipeline-trace-validation-prd.md` §3-4 + `docs/superpowers/specs/2026-08-04-unified-asset-pipeline-design.md` |
| TraceTool（规则引擎/命令） | `docs/prd/2026-08-04-unified-asset-pipeline-trace-validation-prd.md` §5 + `docs/superpowers/specs/2026-08-04-trace-based-validation-design.md` |
| 测试（E2E 链路） | `docs/prd/2026-08-04-unified-asset-pipeline-trace-validation-prd.md` §7 验收 + `docs/testing/test-tiers.md` |
| 配置（storage 段/门控） | `docs/testing/integration-config.md` + `docs/prd/2026-08-04-unified-asset-pipeline-trace-validation-prd.md` §2.5 |
| agent（trace-analyzer.md） | `.claude/agents/trace-analyzer.md`（L4 命令/职责更新） |
