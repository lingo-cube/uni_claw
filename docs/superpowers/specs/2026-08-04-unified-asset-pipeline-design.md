# 统一资产管线与存储 — 前置设计（trace-based-validation 的基础设施）

> 生成: 2026-08-04 · 状态: 设计稿（待审阅）
> 主题: 所有过程产物统一进入分类管线（StepAssetSink 泛化），异步批量落盘、优雅启停；存储介质抽象（IAssetStore）；文件存储显式版本化 **V2**（旧分析工具识别拒绝，新分析工具双版本支持）
> 关联: [2026-08-04-trace-based-validation-design.md](./2026-08-04-trace-based-validation-design.md)（第一个消费者）· [integration-config.md](../../testing/integration-config.md)

---

## 1. 背景与目标

**现状**：三条写入路径并存——StepAssetSink（截图/analysis.jsonl 异步单条写）、writeGate 同步串行（issues/safety-decisions/finalize 元数据）、trace 同步 append。产物分布在 run 根（steps/、analysis.jsonl）与 trace/ 下，无分类、无版本声明演进机制。

**目标**：
1. **统一分类管线**：高频过程资产统一进入同一管道（不新建通道），按类型分类路由，异步**批量**落盘，优雅启停保证落盘。
2. **统一 TraceStore**：trace 存储按分类自动路由到不同类型存储后端（事件流/资产字节），后端可组合；**同一份组合声明驱动写侧路由与读侧查询器装配**——运行侧只需声明组合，查询侧由组合自动产出分析器可用的查询器集。文件存储为本次实现，对象存储/事件流为后续。
3. **文件存储 V2**：run 布局重构为分类目录（任务根 + `assets/` + `trace/` + 元数据），**显式版本化**——`RunAssetVocabulary.SchemaVersion` "1" → "2"；旧分析工具识别 V2 明确拒绝（不静默错读）；新分析工具双版本读取（V1/V2 分发）。

**非目标**：trace 事件流（span/execution JSONL）异步化（保持同步 append，理由见 A-5）；对象存储/事件流实现（仅接口）；V1 run 的迁移/重写（只读兼容）。

## 2. 决策记录

| # | 决策 | 理由 |
|---|---|---|
| A-1 | 统一管线 = StepAssetSink 泛化（bounded Channel + 后台 writer 演进），不新建通道 | 现有形态已验证（P3 双守卫 DrainAsync），泛化成本最低 |
| A-2 | 触发点 = 产生点提交（归责原则）；无集中收集器 | hook 提截图、分析装饰器提 analysis、provider 提 vision-evidence、决策点提 safety/issues |
| A-3 | 统一 TraceStore：分类（record_type）→ 存储后端（事件流/资产字节）路由，后端可组合；`TraceStoreComposer` 同一组合声明驱动写侧路由 + 读侧查询器装配 | 不同分类适配不同存储途径；运行侧只声明组合，查询侧组合自动产出分析器查询器集（分析器不知后端细节） |
| A-4 | 文件存储显式版本化 V2：`SchemaVersion` "1"→"2"，布局重构（assets/ 分类），旧工具识别拒绝 + 新工具双版本读取 | 布局变化破坏旧工具读取，必须显式版本声明，不能静默错读 |
| A-5 | trace 事件流（span/execution JSONL）保持同步 append，不入管线 | ITraceRecorder/FileTraceStorage 契约冻结 + 事件小 + watch 模式依赖实时落盘；最终一致由 P3 统一兜底 |
| A-6 | 批量落盘：后台 writer 缓冲聚合（50ms / 64 条 flush） | 减少 IO 次数；DrainAsync flush 剩余保证完整性 |

## 3. 统一分类管线（P1-P6）

**载体**：现有 `StepAssetSink`（bounded Channel 256 + SingleReader 后台 writer + DrainAsync 幂等）泛化——**不新建通道**。

| 原则 | 内容 |
|---|---|
| **P1 统一分类管道** | 高频过程资产（截图/xml、analysis.jsonl、vision-evidence）统一进入同一管道，管道内按产物类型分类路由。**低频/可靠性优先产物不入 sink**：issues/safety-decisions/result/manifest 走同步 writeGate（现状）；**trace 事件流保持同步 append 现状**（A-5） |
| **P2 零主流程时延** | `Submit` 非阻塞入队（TryWrite）；通道满 → 计数 dropped 不阻塞（MVP），失败可查 |
| **P3 优雅启停保证落盘** | 启动：管道随 run 启动创建；退出：`DrainAsync`（幂等，双守卫安全，flush 缓冲）→ 同步写 result.json 终态 → 退出。**result.json 终态存在 ⇒ 全部异步产物已落盘**。非优雅退出由发布模型兜底（staging 不可见） |
| **P4 失败可观测** | 写失败 → issueSink 留痕 `asset_write_failed` + manifest `assetWriteFailures` 计数；dropped 同计数 |
| **P5 分类路由 + 后端组合** | 统一 TraceStore：record_type 分类 → 后端（事件流/资产字节）路由；`TraceStoreComposer` 组合声明驱动写侧路由与读侧查询器装配（§4）；文件实现复用 RunAssets（staging + `Directory.Move` 原子发布 + writeGate） |
| **P6 批量落盘** | 后台 writer **批量 flush**（缓冲聚合：每 50ms 或 64 条一次写），非逐条 IO；DrainAsync flush 剩余 |

## 4. 统一 TraceStore：分类路由 + 后端组合 + 查询器装配

**目标形态**：trace 存储按**分类**自动路由到不同类型的存储后端，后端可组合；**同一份组合声明驱动两侧**——运行（写）侧按组合路由，查询（读）侧按组合装配查询器给分析器。

### 4.1 分类维度（已有机制）

`record_type` discriminator（FileTraceStorage 现状）：`span / execution / state_transition / error / page_transition / ai_call`（D-91 契约）。扩展资产分类：`asset.screenshot / asset.xml / asset.analysis / asset.vision_evidence`。

### 4.2 存储后端（按存储形态分类）

| 后端 | 形态 | 接口 | 本次实现 |
|---|---|---|---|
| 事件后端 | append 型记录（JSONL 行） | `ITraceEventStore`（写侧对齐现有 ITraceStorage） | FileTraceEventStore（现有 FileTraceStorage 演进） |
| 资产后端 | 字节流对象（相对路径 + 类型） | `IAssetStore`（Write/Read/Exists/List） | FileAssetStore（复用 RunAssets：staging 原子写 + writeGate） |
| （未来）对象存储 / KV 后端 | 同形态接口 | 同接口 | 后续实现 |

### 4.3 组合器 TraceStoreComposer

- **组合声明**：分类 → 后端映射（运行侧配置，唯一需要知道的存储知识）。默认：全部 → file。
- **写侧**：`TraceRecorder`（span/execution 事件）与 sink（资产）统一经 `Composer.Route(category)` 路由到对应后端——产生点不知道后端细节。
- **读侧**：`Composer.BuildQueries()` 按同一组合装配**查询器集**（事件查询器 + 资产查询器），注入分析器。

```csharp
// 写侧路由（组合声明 → 后端）
public sealed class TraceStoreComposer
{
    ITraceEventStore RouteEvent(string recordType);   // span/execution/... → 事件后端
    IAssetStore RouteAsset(string assetCategory);     // asset.screenshot/... → 资产后端

    // 读侧装配（同一组合 → 查询器集）
    TraceQueries BuildQueries();   // { ITraceEventQuery, IAssetQuery, ... }
}
```

### 4.4 查询器（读侧，分析器面向的接口）

- `ITraceEventQuery`（读 span/execution 树，对齐现有 ITraceQuery 读侧）
- `IAssetQuery`（读资产字节流，按引用路径）
- 分析器（RunEvidenceLoader / diagnose / verify）构造注入 `TraceQueries`（查询器集）——**不知后端细节，只面向查询接口**；换后端/换组合不改变分析器代码。

### 4.5 文件后端实现（本次）

- **FileTraceEventStore**：现有 FileTraceStorage 演进（V2 布局：`trace/{runId}/trace.jsonl`，行格式不变）。
- **FileAssetStore**：路径解析到 run staging 目录（`{stagingPath}/{relativePath}`），复用 RunAssets 原子写（tmp + move）与 writeGate；run finalize 时 staging 原子发布——资产随 run 一起可见/不可见。
- 默认组合：span/execution/asset.* 全 → file（单 run 目录内，V2 布局 §5）。

## 5. 文件存储 V2 布局与版本机制

### 5.1 V2 布局

```
{outputRoot}/{scope}/{scenarioId}/{runId}/      ← 任务根（runId == traceId，HostCommands.cs:692）
├── manifest.json                               ← 顶层 schemaVersion: "2"（V2 声明，旧工具识别点）
├── result.json / issues.jsonl / safety-decisions.jsonl / plan.json / scenario.snapshot.json / criteria.json
│                                               ← Host 元数据（run 根，V1 位置不变）
├── trace/{runId}/trace.jsonl                   ← trace 事件流（位置不变，行格式不受 V2 影响）
└── assets/                                     ← 结果资产（异步批量落盘，V2 新增分类目录）
    ├── steps/{n:D4}/before|after.png/xml       ← 截图按 span 树：engine.step 级目录（V1 run 根 → 移入）
    ├── steps/{n:D4}/analysis.json              ← 步级分析（V1 run 根 → 移入）
    ├── steps/{n:D4}/safety-decision.json       ← 步级安全决策（V1 run 根 → 移入）
    ├── analysis.jsonl                          ← 分析快照（V1 run 根 → 移入）
    └── vision-evidence-{stepSpanId}[-{seq}].json ← 新增：分析原始证据（配置门控）
```

**V2 变更点**（version bump 的理由）：
1. `steps/`、`analysis.jsonl` 从 run 根 → `assets/` 下（路径变化 → 旧工具读取破坏）
2. 新增 `assets/vision-evidence-*`（配置门控）
3. 新增 `criteria.json`（verificationCriteria 快照，验证消费）
4. 其余文件位置不变

**索引层级**：第一级 = runId（=traceId，目录根，`TraceContext.TraceId` 已存在无需新增）；第二级 = span 树（engine.step 目录 / spanId 文件名）；第三段 seq 区分同步多次分析。

### 5.2 版本机制与兼容策略

- **声明**：`RunAssetVocabulary.SchemaVersion` "1" → "2"，manifest.json 顶层 `"schemaVersion": "2"`（旧字段位置，旧工具必然读到）。
- **旧工具行为**（未升级的分析工具/TraceTool/脚本）：读 manifest 见 `schemaVersion: "2"` 且未支持 → **明确报错拒绝**（"unsupported run layout version 2 — upgrade the analyzer"），绝不静默错读；对 `"1"` 走原逻辑。
- **新工具行为**（升级后的 TraceTool/RunEvidenceLoader/trace-analyzer）：按 `schemaVersion` 分发——`"1"` → V1 布局解析器（现状逻辑保留）；`"2"` → V2 布局解析器（assets/ 感知）；未知版本 → 明确报错。
- **双解析器并存**：V1 解析器保留现有代码路径（读取存量 run）；V2 解析器新写。写入侧只产 V2。
- **边界**：trace.jsonl 行格式与布局版本**解耦**（trace 事件契约独立冻结，不随布局版本变化）；TraceTool 输出契约 schemaVersion "1"（WriteJson）独立，不动。

## 6. 触发点清单（谁写到管线）

**规律：触发点 = 产生点（归责原则），无集中收集器。**

| 产物 | 提交者（代码点） | 触发时机 | 入管道方式 |
|---|---|---|---|
| 截图 before/after.png+xml | RunAssetHook.OnBefore/AfterStepAsync（`_sink.Submit`） | 引擎每步开始/结束 | sink 异步批量 |
| 步级 analysis.json / safety-decision.json | RunAssetHook / SafetyGate（步级写入） | 步上下文内 | sink 异步批量 |
| analysis.jsonl | AnalysisWritingDecorator（分析返回后 Submit） | 每次页面分析完成 | sink 异步批量 |
| vision-evidence | LocalVisionProvider.CompleteVisionAsync（响应解析前 Submit + 同步写 ai.evidence 引用事件） | 每次视觉分析响应返回 | sink 异步批量（配置门控） |
| safety-decisions.jsonl | SafetyGate 决策 → RunAssets.AppendSafetyDecisionAsync | 每次安全决策 | writeGate 同步（现状） |
| issues.jsonl | HostCommands.cs:866（issue 产生处） | 失败/异常发生 | writeGate 同步（现状） |
| result.json / manifest.json | RunAssets.FinalizeAsync | run 结束（P3 终态） | writeGate 同步（现状） |
| trace.jsonl | TraceRecorder（StartSpan/EndSpan/RecordEvent） | 各 span 生命周期 | 同步 append（现状） |

## 7. 改动清单

**Core**
1. 后端接口：`ITraceEventStore`（写侧对齐现有 ITraceStorage）、`IAssetStore`（Write/Read/Exists/List）。
2. `TraceStoreComposer`：组合声明（分类 → 后端）+ `RouteEvent/RouteAsset`（写侧路由）+ `BuildQueries()`（读侧查询器装配）。
3. 查询器接口：`ITraceEventQuery`（对齐现有 ITraceQuery 读侧）、`IAssetQuery`；`TraceQueries` 聚合（分析器注入面）。
4. `RunAssetVocabulary.SchemaVersion` "1" → "2"；manifest 顶层 schemaVersion 升 "2"。

**Host**
5. StepAssetSink 泛化：分类路由（产物类型 → assets/ 子路径）；后台 writer 批量 flush（50ms/64 条缓冲聚合，DrainAsync flush 剩余）；提交经 `Composer.RouteAsset`。
6. `FileTraceEventStore`（现有 FileTraceStorage 演进，V2 布局）+ `FileAssetStore`（复用 RunAssets：staging 原子写 + writeGate）；默认组合全 → file。
7. V2 布局迁移：RunAssetHook / AnalysisWritingDecorator / SafetyGate 步级写路径改 `assets/...`（steps/、analysis.jsonl 移入 assets/）。
8. RunAssetSession 元数据/引用路径按 V2 更新（manifest 资产清单）。

**TraceTool / 分析工具**
9. 读取入口版本分发：读 manifest.schemaVersion → "1" V1 解析器 / "2" V2 解析器 / 未知 → 明确报错（退出码 + stderr 说明）。
10. 分析器构造改 DI 注入 `TraceQueries`（查询器集，由组合装配——换后端/换组合不改变分析器代码）。
11. 单测：组合/查询器装配（写侧路由 + 读侧查询器对应）、V1/V2 双读、旧工具拒绝行为（未支持版本 → 明确报错）、批量 flush（缓冲聚合 + DrainAsync flush 剩余）、目录布局断言。

**验证（首个消费者）**
10. [trace-based-validation-design.md](./2026-08-04-trace-based-validation-design.md) 依赖本设计产物（provider 提交 vision-evidence、RunEvidenceLoader 读 V2 布局）。

## 8. 联动与验收

**联动**：trace-based-validation（第一个消费者，vision-evidence 采集 + RunEvidenceLoader DI）· P3.x（落盘完整性）· integration-config（evidenceStorage 配置段）。

**验收**：

| 项 | 方式 |
|---|---|
| 管线批量 | 单测：100 条 Submit → 写次数 << 100（缓冲聚合）；DrainAsync 后 100 条全落盘 |
| 优雅启停 | 单测：中途 DrainAsync → 剩余缓冲 flush；终态 result.json 存在 ⇒ 资产完整 |
| 组合与装配 | 单测：组合声明（分类→后端）→ 写侧 Route 路由正确 + 读侧 BuildQueries 装配对应查询器；换组合（mock 后端）→ 分析器代码不变 |
| 介质抽象 | 单测：mock 后端注入分析器 → 查询接口协议不变 |
| V2 布局 | 集成验证：run 后目录结构断言（assets/steps/、assets/analysis.jsonl、schemaVersion "2"） |
| 双版本读取 | 单测：V1 构造 run（旧布局）→ 新分析器 V1 解析通过；V2 run → V2 解析通过 |
| 旧工具拒绝 | 单测：解析器遇未支持版本 → 明确报错（不静默） |
| 回归 | 全量单测绿（V1 解析器保留，存量断言不破坏） |

**边界（非目标）**：trace 事件流异步化；对象存储/事件流实现；V1 run 迁移重写（只读兼容）；watch 用轮询（不引 FileSystemWatcher）。
