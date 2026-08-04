# 统一资产管线与 Trace 验证 — 设计

> 权威细节：`docs/prd/2026-08-04-unified-asset-pipeline-trace-validation-prd.md`（§2-4 架构、§6 改动清单、§7 验收）
> 本文记录 HOW 与关键决策（含备选与理由）。

## Context

现状三条写入路径并存（StepAssetSink 异步单条 / writeGate 同步 / trace 同步 append），产物散落 run 根与 trace/；管道实现在 Host（Core 只有 trace 模型）；验证内嵌 Host（ScenarioCompletionVerifier 依赖内存对象，无法事后复盘）；run 布局无版本声明。约束：Core.csproj 无 Channels 包（net10 内建）；TraceTool 已引用 Host（历史依赖，保留）；LocalVisionProvider 只引 Core；traceId == runId（HostCommands.cs:692）。

## Goals / Non-Goals

**Goals**: 统一提交语义（引用事件 + 字节）；管道公共实现在 Core；职责四层（Core 模型+公共实现 / Host 组合+元数据 / TraceTool 规则引擎 / agent 解读）；信息与物理分离；V2 显式版本化（旧工具拒绝、新工具双读）；验证移出 Host。

**Non-Goals**: trace 事件流异步化；对象存储/事件流实现（仅接口）；V1 run 迁移（只读兼容）；enumerate 规则迁移（MVP 只迁 locate）；watch 用轮询（不引 FileSystemWatcher）；agent 判成功。

## Decisions

| # | 决策 | 备选与理由 |
|---|---|---|
| D-1 | 统一管线 = **Core 公共实现**（ITracePipeline：bounded Channel 256 + 批量 flush 50ms/64 条 + DrainAsync 幂等），Host 删除 StepAssetSink，只装配 | 备选：仅模型+接口进 Core / 留在 Host。管道是通用机制，公共实现只应有一份；Host 不写管道代码 |
| D-2 | 资产（截图/证据）**是 trace 的信息**：引用事件同步 append（trace 是索引），字节经管道批量落盘（物理分离） | 备选：字节同步写 trace。字节体积/写入形态不适合与事件流共存，但语义同属 trace 信息 |
| D-3 | 触发点 = 产生点提交（归责原则）；无集中收集器 | 备选：统一收集器。产生点即提交点，责任可追溯 |
| D-4 | V2 布局：run 根元数据 + `trace/{runId}/` + `assets/{runId}/`（第一级 runId 分桶，与 trace 对称，后端空间键）；SchemaVersion "1"→"2"；旧工具明确拒绝，新工具双解析器分发 | 备选：不版本化/原地改。布局变化必然破坏旧读取，必须显式声明；双解析器并存保存量 |
| D-5 | 失败计数属**事件/日志域**：PipelineStats（Accepted/Dropped/WriteFailures）DrainAsync 后读 → 扩展 `assets.sink_failure` 汇总 trace 事件（+dropped_count）；写失败每条经 IPipelineFailureSink → issueSink；**不回写 manifest** | 备选：manifest 字段回写。manifest 是一次性元数据快照（StartAsync BuildManifest 写），回写破坏快照语义；归因方（verify）本来就读 issues/trace |
| D-6 | `IAssetQuery` = **读窄化视图**（Read/Exists，无 Write）；TraceQueries = ITraceEventQuery + IAssetQuery；IAssetStore 全接口只给写侧管道与实现者；FileAssetStore 双接口分面 | 备选：TraceQueries 直接聚合 IAssetStore（少一接口）。分析器不应持有写能力（ISP）；同一对象不同分面 |
| D-7 | 写侧配置**各入口自持、边界不混淆**：测试链路 = integration.config `storage` 段（位置复用 emulator.outputRoot，无重复字段）→ L1→L3 显式注入；直跑 = CLI env（UNICLAW_ASSET_BACKEND / UNICLAW_OUTPUT / UNICLAW_EVIDENCE_STORAGE） | 备选：统一配置文件。对齐 integration-config.md §9.3 边界模式（一个前缀 = 一层）；测试链路不经 CLI env 回退 |
| D-8 | 读侧 **CLI 参数即配置**（位置显式必填；后端默认不定死）；装配函数形状保留（将来 --backend/--config 只换装配源）；**run 元数据（manifest）作装配参考**（taskId/mode 等作默认，显式参数覆盖） | 备选：读侧独立配置文件。MVP 无此需求；元数据复用 Host 已产出事实（与写侧 D-204 优先级同构） |
| D-9 | 验证移出 Host：run 结束写 `pending_verification` + 引擎事实 + `criteria.json`（独立文件）；VerifyEngine + LocateOneItemRule（D-201 语义平移）；写回仅 pending（`verify --run` 非 pending 只报告不写回） | 备选：verificationCriteria 内嵌 result.json。criteria 是验证契约快照，独立文件消费侧读取更清晰；写回幂等保护终态 |
| D-10 | safety 决策**删除落盘**（safety-decisions.jsonl + 步级 json 零读取方）：TraceSafetyDecisionSink 已全字段写 trace `safety.*` 事件；信息不够补 trace 字段，不恢复落盘 | 备选：保留落盘。trace 覆盖全字段 + 零读取方 → 落盘是死产物 |
| D-11 | watch 盯**指定 run-id**：叶子目录名 == runId 定位，轮询 pending_verification（P3 终态 ⇒ 资产完整）→ 自动 verify → 退出码 = verify 的 | 备选：watch --dir 扫全部新 run。盯单 run 是实际需求（长跑任务），扫描语义归 verify --dir |
| D-12 | manifest 由 RunAssets.StartAsync 写（run 开始），finalize 只更新 result.json | 现状事实确认（RunAssets.cs:254 BuildManifest → staging） |

## Risks / Trade-offs

- [dropped/写失败只丢字节，引用事件已在 trace——读取方见引用无字节] → manifest 无计数，但 issues（asset_write_failed）与 `assets.sink_failure` 汇总事件可归因；P3 不变式（result.json 终态 ⇒ 资产完整）作为归因前提
- [V2 布局破坏存量分析器] → 版本声明 + 旧工具明确拒绝（绝不静默错读）；V1 解析器保留读存量
- [双解析器并存维护成本] → 只读侧双解析，写侧只产 V2；trace 行格式与布局版本解耦
- [TraceTool 引用 Host 的历史依赖] → 读侧装配自持，不新增对 Host 写侧依赖；布局模型引用 Core
- [批量 verify 多进程并发竞态] → MVP 单实例串行 + 写回前重读 status（read-modify-write）；多进程需要时加锁文件/原子状态

## Migration Plan

1. Core：管道公共实现 + 接口 + 布局模型（无行为变化，纯新增/迁移）
2. Host：装配切换（删除 StepAssetSink → Core 管道）+ FileAssetStore + V2 布局迁移 + safety 落盘移除 + pending_verification 写回
3. TraceTool：规则引擎 + 命令 + 版本分发
4. 测试：单测 + RunScenarioAsync 尾部 verify 断言（E2E）
5. 验收：按 PRD §7 逐项（管道批量/优雅启停/V2 布局/双读/规则/E2E/幂等/回归）

Rollback：V1 解析器全程保留；Host 改动不破坏 enumerate/mock 路径（回归验收）。

## Open Questions

无遗留——5 个未定点（safety 落盘 / 配置形状 / 计数明细 / watch 语义 / IAssetQuery 关系）已全部闭环，见 PRD 与决策表。
