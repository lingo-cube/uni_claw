# 统一资产管线与存储 — 前置设计（trace-based-validation 的基础设施）

> 生成: 2026-08-04 · 状态: 设计稿（待审阅）
> 主题: 截图/分析/证据**都是 trace 的信息**——产生点统一提交（引用事件 + 字节）；引用事件进 trace 事件流（同步 append），字节经 **Core 公共管道**（批量异步落盘）物理分离存储。Host 无管道实现（移除 StepAssetSink），只做组合装配（用什么存/往哪里存）与元数据；分析器由配置装配查询器。文件存储显式版本化 **V2**（旧工具识别拒绝，新工具双版本支持）
> 关联: [2026-08-04-trace-based-validation-design.md](./2026-08-04-trace-based-validation-design.md)（第一个消费者）· [integration-config.md](../../testing/integration-config.md)

---

## 1. 背景与目标

**现状**：三条写入路径并存——StepAssetSink（**Host 层**，截图/analysis.jsonl 异步单条写）、writeGate 同步串行（issues/safety-decisions/finalize 元数据）、trace 同步 append。产物分布在 run 根（steps/、analysis.jsonl）与 trace/ 下，无分类、无版本声明演进机制。管道实现（StepAssetSink）在 Host，Core 只有 trace 模型。

**目标**：
1. **统一提交语义**：截图、分析、证据**都是 trace 的信息**——产生点统一"提交 trace 信息"（引用事件 + 字节），不新建通道。
2. **职责分层**：**管道公共实现在 Core**（bounded Channel + 后台 writer + 批量 flush）——Host **不需要 StepAssetSink**，只装配（组合：用什么后端存、往哪里存）+ 产生元数据。
3. **信息与物理分离**：trace 层只读写 trace（事件流 + 资产**引用**，同步 append）；资产字节是 trace 信息的物理载体，经管道**批量异步**落盘（`assets/{runId}/`）——字节不适合与事件流共存，物理分离存储。
4. **分析器配置装配**：查询器由配置装配（查什么 → 配置 → 查询器），分析器只关心查什么与怎么分析，不知后端细节。
5. **文件存储 V2**：run 布局重构（run 根元数据 + `trace/{runId}/` 事件流 + `assets/{runId}/` 资产空间），**显式版本化**——`RunAssetVocabulary.SchemaVersion` "1" → "2"；旧分析工具识别 V2 明确拒绝（不静默错读）；新分析工具双版本读取（V1/V2 分发）。

**非目标**：trace 事件流（span/execution/ai.* JSONL）异步化（保持同步 append，理由见 A-5）；对象存储/事件流实现（仅接口）；V1 run 的迁移/重写（只读兼容）。

## 2. 决策记录

| # | 决策 | 理由 |
|---|---|---|
| A-1 | 统一管线 = **Core 公共实现**（`ITracePipeline`：bounded Channel + 后台 writer + 批量 flush + DrainAsync）；Host 的 StepAssetSink **移除**（逻辑并入 Core） | 管道是通用机制，公共实现只应在 Core 一份；Host 不写管道代码，只装配 |
| A-2 | 触发点 = 产生点提交（归责原则），提交语义 = 写 trace 信息（引用事件 + 字节） | hook 提截图、分析装饰器提 analysis、provider 提 vision-evidence；无集中收集器 |
| A-3 | 资产（截图/证据）**是 trace 的信息**：信息模型含资产引用，字节**物理分离**存储——trace 层只读写 trace（事件流 + 引用），字节经管道落盘 | 字节不适合与事件流共存（体积/写入形态），但语义同属 trace 信息——trace 是索引，引用 = 主通道 |
| A-4 | 文件存储显式版本化 V2：`SchemaVersion` "1"→"2"，布局重构（assets/ 分类 + runId 分桶），旧工具识别拒绝 + 新工具双版本读取 | 布局变化破坏旧工具读取，必须显式版本声明，不能静默错读 |
| A-5 | trace 事件流（span/execution/ai.*，**含资产引用事件**）保持**同步 append**，不入管道 | ITraceRecorder/FileTraceStorage 契约冻结 + 事件小 + watch 模式依赖实时落盘；字节才走管道（批量） |
| A-6 | 批量落盘：管道后台 writer 缓冲聚合（50ms / 64 条 flush） | 减少 IO 次数；DrainAsync flush 剩余保证完整性 |

## 3. 统一分类管线（P1-P6）

**载体（分层）**：**接口 + 公共实现在 Core**——`ITracePipeline`（Submit/DrainAsync）+ `AssetSubmission`（类型/字节/relativePath）+ 分类模型（record_type 扩展 `asset.*`）。公共实现 = 现 StepAssetSink 逻辑迁入 Core（bounded Channel 256 + SingleReader + 批量 flush + DrainAsync 幂等）。Host 只装配（后端 + 位置 + runId 注入）。

| 原则 | 内容 |
|---|---|
| **P1 统一提交** | 高频过程资产（截图/xml、analysis.jsonl、vision-evidence）统一经 Core 公共管道 Submit。**低频/可靠性优先产物不走管道**：issues/result/manifest 走同步 writeGate（Host 元数据）；safety 决策走同步 trace append（不落盘）；**trace 事件流（含引用事件）保持同步 append**（A-5） |
| **P2 零主流程时延** | `Submit` 非阻塞入队（TryWrite）；通道满 → 计数 dropped 不阻塞（MVP），失败可查 |
| **P3 优雅启停保证落盘** | 启动：管道随 run 启动创建；退出：`DrainAsync`（幂等，flush 缓冲）→ 同步写 result.json 终态 → 退出。**result.json 终态存在 ⇒ 全部字节已落盘**。非优雅退出由发布模型兜底（staging 不可见） |
| **P4 失败可观测** | 写失败 → issueSink 留痕 `asset_write_failed` + manifest `assetWriteFailures` 计数；dropped 同计数 |
| **P5 分类路由 + 组合** | 管道公共实现按分类（record_type / `asset.*`）路由；**组合 = 配置**——写侧由 Host 配置（后端键 + 位置）装配管道；读侧由分析器配置装配查询器（§4.3/4.4） |
| **P6 批量落盘** | 后台 writer **批量 flush**（缓冲聚合：每 50ms 或 64 条一次写），非逐条 IO；DrainAsync flush 剩余 |

## 4. 信息模型与分层

### 4.1 信息模型：资产是 trace 的信息

```
trace 信息（Core 模型）
├─ 事件流：span / execution / state_transition / ai.* / ai.evidence（引用事件）— 轻量，JSONL 同步 append
└─ 资产：截图/证据字节（文件名含 spanId，引用 = 主通道，trace 事件持有路径）
     ↑ 字节与事件流物理分离（不适合共存）：事件流同步 append，字节经管道批量落盘 assets/{runId}/
```

- **引用 = 主通道**：提交字节时同步写引用事件（ai.evidence 等：evidence_path/type/bytes）——trace 是索引，后处理按引用读资产。
- **物理分离**：事件流 `trace/{runId}/trace.jsonl`（同步 append）；字节 `assets/{runId}/…`（管道批量异步）。

### 4.2 管道公共实现（Core）

- **接口**：`ITracePipeline`（Submit / DrainAsync）+ `AssetSubmission`（类型/字节/relativePath）+ 分类模型。
- **公共实现**：bounded Channel 256 + SingleReader 后台 writer + 批量 flush（50ms/64 条）+ DrainAsync 幂等——现 StepAssetSink 逻辑**迁入 Core**，Host 的 StepAssetSink 删除。
- **产生点视角**：hook / decorator / provider 只调 Submit（提交相对路径，runId 由装配注入）——不知后端、不知位置、不知落盘细节。

### 4.3 组合与装配（写侧 Host / 读侧分析器）

- **组合声明 = 配置**：后端键（file）+ 位置（outputRoot）。Host 只告诉管道"**用什么存、往哪里存**"。
- **各入口自持配置来源，边界明确不混淆**（对齐 integration-config.md §9.3 边界模式：一个前缀 = 一层，测试链路不经 CLI env 回退）：
  - 测试链路：integration.config `storage` 段（后端键；位置**复用 `emulator.outputRoot`**，不新增重复字段）；经 L1→L3 显式参数注入
  - Host 直跑：CLI env 回退（`UNICLAW_ASSET_BACKEND` + 既有 `UNICLAW_OUTPUT` + `UNICLAW_EVIDENCE_STORAGE`）
- **Host 装配**：构造管道公共实现（后端 + 位置 + runId 注入）——Host 唯一的管道相关职责；其余职责 = 产生元数据。
- **分析器装配**：**CLI 参数即配置**（位置参数显式必填；后端默认不定死，通常按实际指定）→ 查询器集（trace 查询 + 资产查询）——分析器只关心"查什么、怎么分析"；装配函数形状保留，将来 `--backend`/`--config` 只换装配源，查询器与分析器代码不变。

### 4.4 查询器（读侧）

- `ITraceEventQuery`（读事件流/span 树，对齐现有 ITraceQuery 读侧）
- `IAssetQuery`（读资产字节流，按引用路径——引用含 runId 键）
- **接口在 Core；文件查询器实现与装配在 TraceTool**（配置驱动；V1/V2 分发解析自持，布局模型引用 Core）——分析器注入 `TraceQueries`（聚合），换后端/换组合不改变分析器代码。

### 4.5 后端实现（本次）

- **事件后端**：现有 FileTraceStorage 演进（V2 布局 `trace/{runId}/trace.jsonl`，行格式不变）——**只管事件流**。
- **资产后端**：`IAssetStore`（Write/Read/Exists/List，键 = `{runId}/{relativePath}`，runId 由装配注入）；FileAssetStore 路径解析到 run staging 目录（`{stagingPath}/assets/{runId}/{relativePath}`），复用 RunAssets 原子写（tmp + move）与 writeGate；finalize 时 staging 原子发布——资产随 run 一起可见/不可见。
- 默认组合：全 → file（单 run 目录内，V2 布局 §5）。

## 5. 文件存储 V2 布局与版本机制

### 5.1 V2 布局

```
{outputRoot}/{scope}/{scenarioId}/{runId}/      ← run 根（runId == traceId，HostCommands.cs:692）
├── manifest.json                               ← 顶层 schemaVersion: "2"（V2 声明，旧工具识别点）
├── result.json / issues.jsonl / plan.json / scenario.snapshot.json / criteria.json
│                                               ← Host 元数据（run 根，V1 位置不变）
├── trace/{runId}/trace.jsonl                   ← 事件流空间（同步 append，含资产引用事件；按 runId 分桶，行格式不受 V2 影响）
└── assets/{runId}/                             ← 资产空间（字节经管道批量落盘，V2 新增；第一级按 runId，与 trace/ 对称）
    ├── steps/{n:D4}/before|after.png/xml       ← 截图按 span 树：engine.step 级目录（V1 run 根 → 移入）
    ├── steps/{n:D4}/analysis.json              ← 步级分析（V1 run 根 → 移入）
    ├── analysis.jsonl                          ← 分析快照（V1 run 根 → 移入）
    └── vision-evidence-{stepSpanId}[-{seq}].json ← 新增：分析原始证据（配置门控）
```

> **runId 两级说明**：run 根 = run 目录（元数据载体）；`trace/` 与 `assets/` 是**后端存储空间**——各自第一级键 = runId（共享存储键，与 run 根命名巧合同源）。后端切对象存储/事件流时键空间不变（`trace/{runId}/…`、`assets/{runId}/…`）。

**V2 变更点**（version bump 的理由）：
1. `steps/`、`analysis.jsonl` 从 run 根 → `assets/` 下（路径变化 → 旧工具读取破坏）
2. 资产空间第一级按 runId 分桶（`assets/{runId}/…`，与 `trace/{runId}` 对称——后端空间键，切对象存储不变）
3. 新增 `assets/{runId}/vision-evidence-*`（配置门控）
4. 新增 `criteria.json`（verificationCriteria 快照，验证消费）
5. **移除 safety 落盘**：`safety-decisions.jsonl` + `steps/{n}/safety-decision.json` 不再产出（零读取方；safety 决策全字段已由 TraceSafetyDecisionSink 写 trace `safety.*` 事件——trace 是唯一信息源，信息不够补 trace 字段，不恢复落盘）
6. 其余文件位置不变

**索引层级**：第一级 = runId（=traceId，`TraceContext.TraceId` 已存在无需新增——trace 与资产空间共用同一键，`trace/{runId}` / `assets/{runId}`）；第二级 = span 树（engine.step 目录 / spanId 文件名）；第三段 seq 区分同步多次分析。

### 5.2 版本机制与兼容策略

- **声明**：`RunAssetVocabulary.SchemaVersion` "1" → "2"，manifest.json 顶层 `"schemaVersion": "2"`（旧字段位置，旧工具必然读到）。
- **旧工具行为**（未升级的分析工具/TraceTool/脚本）：读 manifest 见 `schemaVersion: "2"` 且未支持 → **明确报错拒绝**（"unsupported run layout version 2 — upgrade the analyzer"），绝不静默错读；对 `"1"` 走原逻辑。
- **新工具行为**（升级后的 TraceTool/RunEvidenceLoader/trace-analyzer）：按 `schemaVersion` 分发——`"1"` → V1 布局解析器（现状逻辑保留）；`"2"` → V2 布局解析器（assets/ 感知）；未知版本 → 明确报错。
- **双解析器并存**：V1 解析器保留现有代码路径（读取存量 run）；V2 解析器新写。写入侧只产 V2。
- **边界**：trace.jsonl 行格式与布局版本**解耦**（trace 事件契约独立冻结，不随布局版本变化）；TraceTool 输出契约 schemaVersion "1"（WriteJson）独立，不动。

## 6. 触发点清单（谁写到管线）

**规律：触发点 = 产生点（归责原则），无集中收集器；提交 = 写 trace 信息（引用事件 + 字节）。**

| 产物 | 提交者（代码点） | 触发时机 | 入管道方式 |
|---|---|---|---|
| 截图 before/after.png+xml | RunAssetHook.OnBefore/AfterStepAsync（Submit） | 引擎每步开始/结束 | 字节经管道批量异步 |
| 步级 analysis.json | RunAssetHook（步级写入） | 步上下文内 | 字节经管道批量异步 |
| analysis.jsonl | AnalysisWritingDecorator（分析返回后 Submit） | 每次页面分析完成 | 字节经管道批量异步 |
| vision-evidence | LocalVisionProvider.CompleteVisionAsync（响应解析前 Submit + 同步写 ai.evidence 引用事件） | 每次视觉分析响应返回 | 字节经管道批量异步（配置门控）；引用同步 append |
| safety 决策（trace 事件） | SafetyGate → TraceSafetyDecisionSink（现状已存在，全字段） | 每次安全决策 | 同步 append 进 trace.jsonl（`safety.*` 事件；**落盘移除**） |
| issues.jsonl | HostCommands.cs:866（issue 产生处） | 失败/异常发生 | writeGate 同步（Host 元数据，现状） |
| result.json / manifest.json | RunAssets.FinalizeAsync | run 结束（P3 终态） | writeGate 同步（Host 元数据，现状） |
| trace.jsonl（含引用事件） | TraceRecorder（StartSpan/EndSpan/RecordEvent） | 各 span 生命周期 | 同步 append（现状） |

## 7. 改动清单

**Core（模型 + 公共实现）**
1. 管道：`ITracePipeline`（Submit/DrainAsync）+ `AssetSubmission`（类型/字节/relativePath）+ 分类模型（record_type 扩展 `asset.*`）+ **公共实现**（现 StepAssetSink 逻辑迁入：bounded Channel + 批量 flush + DrainAsync 幂等）。
2. 信息模型：资产引用事件契约（ai.evidence：evidence_path/type/bytes）——引用在 trace 事件里，字节物理分离。
3. 资产后端接口：`IAssetStore`（Write/Read/Exists/List，键 = `{runId}/{relativePath}`）。（事件侧沿用 ITraceStorage/FileTraceStorage，不新增）
4. 查询器接口：`ITraceEventQuery` / `IAssetQuery` + `TraceQueries` 聚合（分析器注入面）。
5. 文件布局模型：V2 布局常量 + 路径生成纯函数（写侧生成/读侧解析共用）；`RunAssetVocabulary.SchemaVersion` "1" → "2"，manifest 顶层升 "2"。

**Host（组合装配 + 元数据）**
6. **移除 StepAssetSink**：管道改用 Core 公共实现；Host 装配（后端 file + 位置 + runId 注入）。
7. V2 布局迁移：产生点提交相对路径（runId 装配注入 → `assets/{runId}/…`）；steps/、analysis.jsonl 移入资产空间。
8. 元数据（manifest/result/issues）位置不变（V1 兼容）；**移除 RunAssetSafetyDecisionSink 落盘**（safety-decisions.jsonl + 步级 json；safety 决策只写 trace，manifest 资产清单移除 safetyDecimals 项）；manifest 资产清单/引用按 V2 更新。
9. 配置：integration.config `storage` 段（后端键；位置复用 `emulator.outputRoot`）+ `providers.local.evidenceStorage` 门控（enabled 默认 false，扩展点 spanTypes）；入口边界——测试链路经 L1→L3 显式参数注入，直跑走 `UNICLAW_ASSET_BACKEND`/`UNICLAW_OUTPUT`/`UNICLAW_EVIDENCE_STORAGE` env，互不混淆。

**TraceTool（配置装配 + 分析）**
10. 读取入口版本分发：读 manifest.schemaVersion → "1" V1 解析器 / "2" V2 解析器 / 未知 → 明确报错（退出码 + stderr 说明）。
11. 文件查询器实现 + **配置装配**（配置 → TraceQueries）；分析器构造注入 `TraceQueries`（换后端/换组合不改变分析器代码）。MVP：CLI 参数即配置（位置显式必填；后端默认不定死）；装配函数形状保留（将来 `--backend`/`--config` 只换装配源）。
12. 单测：管道公共实现（批量 flush + DrainAsync）、组合装配（写侧路由/读侧查询器对应）、V1/V2 双读、旧工具拒绝行为（未支持版本 → 明确报错）、目录布局断言。

**验证（首个消费者）**
13. [trace-based-validation-design.md](./2026-08-04-trace-based-validation-design.md) 依赖本设计产物（provider 提交 vision-evidence、RunEvidenceLoader 读 V2 布局）。

## 8. 联动与验收

**联动**：trace-based-validation（第一个消费者，vision-evidence 采集 + RunEvidenceLoader DI）· P3.x（落盘完整性）· integration-config（evidenceStorage 配置段）。

**验收**：

| 项 | 方式 |
|---|---|
| 管道批量 | 单测：100 条 Submit → 写次数 << 100（缓冲聚合）；DrainAsync 后 100 条全落盘 |
| 优雅启停 | 单测：中途 DrainAsync → 剩余缓冲 flush；终态 result.json 存在 ⇒ 资产完整 |
| 组合与装配 | 单测：配置（后端键 + 位置）→ Host 装配管道路由正确 + TraceTool 配置装配对应查询器；换组合（mock 后端）→ 分析器代码不变 |
| 介质抽象 | 单测：mock 后端注入分析器 → 查询接口协议不变 |
| V2 布局 | 集成验证：run 后目录结构断言（`assets/{runId}/steps/`、`assets/{runId}/analysis.jsonl`、schemaVersion "2"） |
| 双版本读取 | 单测：V1 构造 run（旧布局）→ 新分析器 V1 解析通过；V2 run → V2 解析通过 |
| 旧工具拒绝 | 单测：解析器遇未支持版本 → 明确报错（不静默） |
| 回归 | 全量单测绿（V1 解析器保留，存量断言不破坏） |

**边界（非目标）**：trace 事件流异步化；对象存储/事件流实现；V1 run 迁移重写（只读兼容）；watch 用轮询（不引 FileSystemWatcher）。
