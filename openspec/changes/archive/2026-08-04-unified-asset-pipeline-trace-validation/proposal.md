## Why

三条写入路径并存（StepAssetSink 异步单条 / writeGate 同步 / trace 同步 append），产物散落 run 根与 trace/ 下，无分类、无版本声明；管道实现（StepAssetSink）在 Host 而 Core 只有 trace 模型；验证逻辑内嵌 Host（`ScenarioCompletionVerifier` ~310 行，依赖内存对象，无法事后复盘）。run 布局演进无显式版本，旧分析器会被静默破坏。

## What Changes

- **统一提交语义**：截图/分析/证据都是 **trace 的信息**——产生点统一提交（引用事件 + 字节）；引用事件进 trace 事件流（同步 append），字节经 **Core 公共管道**（批量异步落盘 `assets/{runId}/`）物理分离存储
- **管道公共实现迁 Core**：`ITracePipeline`（Submit/DrainAsync）+ `AssetSubmission` + 分类模型 + bounded Channel 批量 flush + 幂等 DrainAsync——现 StepAssetSink 逻辑迁入 Core，Host 的 StepAssetSink **删除**
- **职责重定位**：Host 只做组合装配（用什么后端存/往哪里存 + runId 注入）+ 元数据 + 异常/日志；写侧各入口自持配置来源（测试链路 integration.config `storage` 段 / 直跑 CLI env），读侧 CLI 参数即配置 + run 元数据作装配参考
- **失败可观测（P4）**：写失败每条经 `IPipelineFailureSink` → issueSink；DrainAsync 后读 `PipelineStats`（Accepted/Dropped/WriteFailures）→ 扩展 `assets.sink_failure` 汇总 trace 事件；**计数属事件域，不回写 manifest**
- **文件存储 V2（BREAKING）**：`RunAssetVocabulary.SchemaVersion` "1"→"2"；`assets/{runId}/` 资产空间（steps/、analysis.jsonl 移入，runId 分桶与 trace/ 对称）；`trace/{runId}/trace.jsonl`；新增 `criteria.json`、`vision-evidence-*`（配置门控）；**移除 safety-decisions.jsonl + steps/{n}/safety-decision.json 落盘**（safety 决策只写 trace `safety.*` 事件）；旧工具读 V2 **明确拒绝**（不静默错读），新工具双版本读取
- **验证移出 Host**：TraceTool 规则引擎（`VerifyEngine` + `LocateOneItemRule`，D-201 语义平移）；`verify --run` / `verify --dir`（幂等批量）/ `watch --run-id`（盯单 run）三命令；run 结束写 `status="pending_verification"` + `criteria.json` 快照，verify 回写最终判定（仅 pending 写回）
- **读侧查询器**：`TraceQueries` 聚合（`ITraceEventQuery` + `IAssetQuery` 读窄化——分析器不持有写能力）；`RunEvidenceLoader` 按 schemaVersion 分发 V1/V2

## Capabilities

### New Capabilities

- `trace-pipeline`: 统一资产提交管线——Core 公共实现（ITracePipeline/AssetSubmission/分类模型/批量 flush/DrainAsync）、IAssetStore 介质接口、IPipelineFailureSink + PipelineStats 失败可观测、引用事件 + 字节物理分离的提交语义
- `run-layout-v2`: 文件存储 V2——schemaVersion "2" 声明、assets/{runId}/ + trace/{runId}/ 布局、criteria.json、旧工具拒绝 + 新工具双版本读取
- `trace-based-validation`: 验证规则引擎——VerifyEngine + LocateOneItemRule、verify/watch 命令契约、evidence-missing 门、写回幂等

### Modified Capabilities

- `trace-analyzer-cli`: 子命令扩展（verify/watch）+ 读侧查询器装配（CLI 参数即配置，run 元数据参考）
- `file-trace-storage`: ai.evidence 引用事件（TraceFields 45→48）（Core FileTraceStorage 的 {baseDir}/{traceId} 布局天然支持 V2 的 trace/{runId}——traceId==runId，V2 只是装配层换 baseDir，Core 不变）
- `run-metadata-enrichment`: manifest V2——顶层 schemaVersion "1"→"2"、资产清单/引用按 V2 更新、移除 safetyDecimals 项
- `integration-test-config`: 新增 `storage` 段（后端键，位置复用 emulator.outputRoot）+ `providers.local.evidenceStorage` 门控

## Acceptance Criteria

> 验收细节与执行方法：`docs/prd/2026-08-04-unified-asset-pipeline-trace-validation-prd.md` §7（16 项，本清单为提案级契约）

**管道（trace-pipeline）**

- [ ] 批量聚合：100 Submit → 底层落盘写次数 ≪ 100；DrainAsync 后 100 条全部落盘
- [ ] 优雅启停：mid-run DrainAsync → 剩余 buffer 冲刷；result.json 终态 ⇒ 资产完整（P3 不变式）
- [ ] 失败计数：TryWrite 满 → Dropped 计数；写异常 → WriteFailures + `asset_write_failed` issue 条目；post-drain 汇总事件（`assets.sink_failure`）携带 failed/accepted/dropped 全部计数；manifest 不回写
- [ ] 介质抽象：mock IAssetStore 注入 → 管道/查询器协议不变（分析器代码零改动）

**布局 V2（run-layout-v2）**

- [ ] V2 run 目录断言：`assets/{runId}/steps/`、`assets/{runId}/analysis.jsonl`、`trace/{runId}/trace.jsonl`、`criteria.json`、manifest schemaVersion "2"
- [ ] safety 不落盘：V2 run 无 safety-decisions.jsonl、无步级 safety-decision.json，manifest 无 safetyDecimals 项；决策全量在 trace `safety.*` 事件
- [ ] 双版本读取：V1 构造 run → V1 解析通过；V2 run → V2 解析通过
- [ ] 旧工具拒绝：unsupported schemaVersion → loud error + 升级提示（绝不静默错读）

**验证（trace-based-validation）**

- [ ] 规则正确性：VerifyEngine 单测（success / identity-fallback / not_verified / evidence_missing）
- [ ] 写回幂等：`verify --run` 非 pending 只报告不写回；批量/调度交错永不重复写回；原子 tmp+move
- [ ] E2E 链路：LocateOneItem run 结束 pending_verification → verify → 最终 **success**
- [ ] watch 契约：`watch --run-id` 轮询 pending_verification 自动 verify，退出码 = verify 的；>1 匹配目录报错
- [ ] 退出码 + JSON schemaVersion 与现有 CLI 契约对齐（0=verified · 1=not_verified · 2=usage · 3=evidence missing）

**回归**

- [ ] 全量单测绿（V1 解析器保留；Host 改动不破坏 enumerate/mock 路径；StepAssetSink 删除后无引用残留）

## Impact

- **Core**：`ITracePipeline` 公共实现迁入（Channels 内建无包依赖）、`IAssetStore`/`IAssetQuery`/`ITraceEventQuery`/`TraceQueries`、`IPipelineFailureSink`/`PipelineStats`、V2 布局模型（常量 + 纯路径函数）、ai.evidence 引用事件契约
- **Host**：删除 StepAssetSink；FileAssetStore（staging 原子写 + writeGate，E4 提取 AssetStagingWriter）；V2 布局迁移（产生点提交相对路径，runId 装配注入）；LocalVisionProvider 注入 `ITracePipeline?` + evidenceStorage 门控；移除 RunAssetSafetyDecisionSink 落盘；run 结束写 pending_verification + criteria.json；删除 ScenarioCompletionVerifier locate 分支
- **TraceTool**：RunEvidenceLoader、VerifyEngine + LocateOneItemRule、verify/watch 命令、schemaVersion 分发、文件查询器装配
- **测试**：RunScenarioAsync 尾部调 verify CLI 断言；管道/规则/布局/幂等单测
- **agent**：trace-analyzer.md L4 补 verify/watch/批量命令 + 职责声明
- **配置**：integration.config `storage` 段 + `providers.local.evidenceStorage`（写侧测试链路）；`UNICLAW_ASSET_BACKEND`/`UNICLAW_EVIDENCE_STORAGE`（直跑）
- **文档**：PRD `docs/prd/2026-08-04-unified-asset-pipeline-trace-validation-prd.md`（权威）；设计稿 `docs/superpowers/specs/2026-08-04-unified-asset-pipeline-design.md` + `2026-08-04-trace-based-validation-design.md`（过程稿）
