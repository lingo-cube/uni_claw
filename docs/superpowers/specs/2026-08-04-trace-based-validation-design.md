# Trace-Based Validation — 验证移出 Host，基于 trace + 异步落盘资产

> 生成: 2026-08-04 · 状态: 设计稿（待审阅）
> 主题: 集成验证不放在 Host 内嵌同步校验，改为基于 trace 与过程中异步落盘的资产，通过 trace analyzer（TraceTool 规则引擎 + agent 解读）分析得到结论
> 关联: [integration-pipeline-issues.md](../../testing/integration-pipeline-issues.md)（P1.x 验证域 / P3.x 中间信息域）· [trace-analyzer.md](../../../.claude/agents/trace-analyzer.md)（L4 分析层）
> 前置依赖: [2026-08-04-unified-asset-pipeline-design.md](./2026-08-04-unified-asset-pipeline-design.md)（统一提交 = trace 信息 + Core 公共管道 P1-P6 + 资产引用事件 + 文件存储 V2 布局/版本机制——本设计消费其产物，不重复设计）

---

## 1. 背景与目标

**现状**：验证逻辑内嵌在 Host —— `ScenarioCompletionVerifier`（~310 行）在 run 结束**同步**判定 successCriteria（locate 身份回退 + targetAction；enumerate 账本式统计），产出 ScenarioRunOutcome 写入 result.json。问题：验证逻辑随场景演进持续膨胀，Host 职责边界模糊；验证依赖内存对象（TraversalResult/finalAnalysis/SafetyDecisionJournal），事后无法复盘。

**目标**：验证移出 Host —— Host 只跑 + 落盘；验证由 **TraceTool 规则引擎**（确定性 C# 规则）基于 trace 与异步落盘资产在 run 后判定；**trace-analyzer agent** 解读结论与失败归因。

**非目标**：enumerate 验证规则迁移（MVP 只迁 locate）；存储抽象层（仅标注扩展点）；agent 参与成功判定（成功由 C# 规则产出）。

## 2. 决策记录

| # | 决策 | 理由 |
|---|---|---|
| V-1 | 验证载体 = TraceTool 规则引擎（C#，确定性 verdict/evidence JSON）+ agent 解读 | 验证必须确定性（CI 消费）；agent 擅长归因与自然语言结论 |
| V-2 | 验证时机 = 测试内串行（run 完 → 测试调 `trace verify` → 断言 verdict） | CI 一步到位；失败归因由 agent 事后补充 |
| V-3 | Host 边界 = run 结束写引擎事实 + `status="pending_verification"` + verificationCriteria 快照；verify 回写最终判定 | Host 保留 run 事实，判定权移交分析侧 |
| V-4 | MVP 只迁 locate_one_item 验证规则；enumerate 规则暂留 Host | 当前集成测试只有 locate 实跑，迁移风险最小 |
| V-5 | 产物关联 = trace 持有 id（span 属性引用产物），产物文件名含 spanId | 同一步可多次分析，固定文件名会覆盖；id 是稳定的关联主键（存储模式扩展点） |
| V-6 | 落盘/存储 = 前置设计（统一提交 = trace 信息 + Core 公共管道 P1-P6 + 引用事件 + 文件存储 V2），本设计只消费其产物 | 见 [unified-asset-pipeline-design.md](./2026-08-04-unified-asset-pipeline-design.md) |

## 3. 架构与数据流

```
┌─ Host（run 时）────────────────────────────────────┐
│  引擎执行 → 产生点提交 (Core 公共管道 + 引用事件)   │
│    · 每步 hook: before/after 截图+xml               │
│    · 分析返回: vision-evidence + analysis.jsonl     │
│  run finalize: DrainAsync → result.json            │
│    status="pending_verification" + 引擎事实         │
│    + verificationCriteria 快照                      │
└──────────────┬─────────────────────────────────────┘
               ▼ (已发布 run, 资产完整)
┌─ TraceTool（run 后, 测试内串行）─────────────────────┐
│  trace verify --run <dir>                          │
│  RunEvidenceLoader 读盘重建:                       │
│    analysis.jsonl 末条 → finalAnalysis.Items       │
│    result.json 引擎字段 → TraversalResult          │
│    criteria 快照 → expectedPageIdentities          │
│  LocateOneItemRule: 身份回退 + targetAction 判定    │
│  → verdict/evidence/artifactPaths (span 属性解析)   │
│  回写 result.json (原子) + issues.jsonl (失败留痕)   │
└──────────────┬─────────────────────────────────────┘
               ▼
┌─ 测试（断言）+ agent（解读）─────────────────────────┐
│  测试: 断言 verdict == verified                    │
│  失败: verdict summary 入测试失败信息               │
│  agent: 读 verdict + 资产 → 归因 + 完整结论          │
└───────────────────────────────────────────────────┘
```

### 运行模式（同一规则引擎，三种触发）

| 模式 | 命令 | 场景 |
|---|---|---|
| 一次性 | `trace verify --run <dir>` | 测试内串行断言 |
| 批量补验 | `trace verify --dir <root> [--status pending] [--task-id <id>]` | CI cron / 定时 / 漏验补跑（幂等：只处理 pending） |
| 实时监控 | `trace watch --dir <root> [--interval 5s] [--task-id <id>]` | 长跑任务实时盯（轮询新完成 run 自动 verify） |

## 4. 异步落盘管道与存储（前置设计，消费侧）

> **统一提交（trace 信息 + Core 公共管道 P1-P6）、资产引用事件、文件存储 V2 布局与版本兼容 = 前置设计**，见 [unified-asset-pipeline-design.md](./2026-08-04-unified-asset-pipeline-design.md)。本设计不重复设计，只消费其产物。

本设计保留的验证域相关保证（前置 P3/P4 的消费侧语义）：
- **P3 终态不变式**：result.json 终态存在 ⇒ 全部异步产物已落盘——verify 的 evidence_missing 归因前提。
- **P4 失败可观测**：`asset_write_failed` 留痕 + `assetWriteFailures` 计数——verify 读 manifest 区分管道故障 vs run 未产出。
- **V2 版本兼容**：verify 读 run 前先读 manifest.schemaVersion（"1"/"2" 分发解析；未知版本明确报错，不静默）。

## 5. 产物关联模型（trace 为索引）

**两级产物规则**：

| 产物级 | 命名 | 关联载体 |
|---|---|---|
| 步级稳定产物（每步一套：before/after 截图+xml） | 固定名 | engine.step span 属性 `artifact_dir: "assets/{runId}/steps/0004"`（V2 布局） |
| 分析级产物（同步可多次：vision-evidence） | 文件名带 spanId：`vision-evidence-{stepSpanId}[-{seq}].json` | ai.evidence 点事件属性写完整相对路径（**提交时同步已知**，主通道） |

**机制**：
1. **id = 关联主键**：分析级产物文件名含产生它的 engine.step spanId（经 `EngineStepSpanContext.CurrentSpanId` AsyncLocal 读取），文件系统零覆盖；同一步多次分析（ai.call 重试）用 provider 内 per-step 自增 `seq` 后缀区分。
2. **路径提交时已知，属性同步可写**：路径 = 纯函数(spanId, seq) 由 provider 在 Submit 前拼出——`Submit(type, bytes, path)` 入 Core 公共管道的同时，同步 `RecordEventAsync("ai.evidence", parent=stepSpanId, attrs={evidence_path, evidence_type, byte_count})`。不需要写盘后回读路径（ai.analyze span 在 provider 返回后才创建，改由 provider 自己创建 ai.evidence 点事件承载引用）。
3. **trace 与资产物理分离**：trace.jsonl 只含引用（ai.evidence 事件，轻量）；字节流资产独立落盘 `{runDir}/assets/{runId}/`（与 `trace/{runId}/` 空间并列，第一级均按 runId），P3 finalize DrainAsync 保证 run 发布时资产完整。
4. **配置门控**：结果资产**默认不存储**；integration.config `providers.local.evidenceStorage.enabled` 启用后该 spanType 才 Submit 资产 + 写引用（"针对部分方法存储"落地为配置，扩展点：spanTypes 列表）。
5. **产物自描述**：vision-evidence.json 内部带 `spanId/stepNumber/runId/seq`——存储模式扩展（对象存储/事件流）后仍可双向关联。
6. **统一读取规则**：TraceTool artifactPaths 一律从 span 解析（步级取 artifact_dir 属性，分析级取 ai.evidence 属性）——verify/diagnose/取证共用。

## 6. 产出物明细

| 产出物 | 内容 | 路径 | 提交者（代码点） | 触发时机 | 入管道方式 |
|---|---|---|---|---|---|
| per-step 资产 | before/after 截图 + UI XML + analysis.json | `{runDir}/assets/{runId}/steps/{n:D4}/`（V2） | RunAssetHook（OnBefore/AfterStepAsync 提交） | 引擎每步开始/结束 | Core 公共管道批量异步（已是现状机制） |
| analysis.jsonl | 分析精简快照（Items 名/类型/坐标） | `{runDir}/assets/{runId}/analysis.jsonl`（V2） | AnalysisWritingDecorator（分析返回后 Submit） | 每次页面分析完成 | Core 公共管道批量异步（D-197） |
| **vision-evidence.json** | 分析原始证据：candidates、metadata(schema/模型/configHash)、scrollHints、stage 耗时 | `{runDir}/assets/{runId}/vision-evidence-{stepSpanId}[-{seq}].json` | LocalVisionProvider.CompleteVisionAsync（响应解析前 Submit + 同步写 ai.evidence 引用事件） | 每次视觉分析响应返回 | Core 公共管道批量异步（配置 evidenceStorage 门控，默认关闭） |
| safety-decisions.jsonl | 安全决策 | `{runDir}/safety-decisions.jsonl` | SafetyGate 决策 → RunAssets.AppendSafetyDecisionAsync | 每次安全决策 | writeGate 同步（现状） |
| issues.jsonl | 失败/异常留痕 | `{runDir}/issues.jsonl` | HostCommands.cs:866（issue 产生处） | 失败/异常发生 | writeGate 同步（现状） |
| result.json / manifest.json | finalize 元数据 + 验证字段 | `{runDir}/` | RunAssets.FinalizeAsync | run 结束（P3 终态） | writeGate 同步（现状） |
| trace.jsonl | span/execution | `{runDir}/trace/{runId}/` | TraceRecorder（StartSpan/EndSpan/RecordEvent） | 各 span 生命周期 | 同步 append（现状） |

**触发点规律**：产生点即提交点（归责原则）——hook 提截图、分析装饰器提 analysis、provider 提 vision-evidence、决策点提 safety/issues；无集中收集器。sink 只管高频过程资产；低频可靠性产物（safety/issues/元数据）保持同步 writeGate。

## 7. TraceTool verify 契约

```bash
$ trace verify --run <dir> [--format json]
# 退出码: 0 = verified · 1 = not_verified · 2 = usage/目录错误 · 3 = 证据缺失
# stdout 单文档 (schemaVersion "1"):
{
  "runId": "...", "status": "failure",
  "verdict": { "cause": "target_page_identity_not_verified",
               "confidence": "high",
               "failingStep": 12,
               "summary": "Post-action identity 'Settings' != expected 'About device'..." },
  "evidence": [
    { "type": "final_identity", "step": 12, "description": "analysis.jsonl 末条 identity='Settings'" },
    { "type": "expected_identities", "description": "About device / About emulated device / ..." },
    { "type": "target_action_executed", "description": "click 成功 1 次" },
    { "type": "click_target_matches_identity", "description": "safety-decision 点击目标 == 预期身份行" }
  ],
  "artifactPaths": { "screenshotPaths": ["assets/{runId}/steps/0004/after.png"], "tracePath": "trace/{runId}/trace.jsonl" }
}
```

**规则引擎**：`VerifyEngine.VerifyAsync(TraceRun)` → `IVerificationRule` 列表；MVP = `LocateOneItemRule`（D-201 身份回退语义原样平移：analysis.jsonl 末条 Items 匹配 expectedPageIdentities；targetActionExecuted = completionReason==target_found && 有成功 action）。

**回写**：原子更新 result.json（tmp + move，只写验证字段；`status` 回写为 `success`/`failure`，对齐 RunAssetVocabulary.ResultStatuses）；失败同时 issues.jsonl 追加（详情内嵌 Summary——RunIssue 契约无 Detail 字段）。幂等：只处理 `pending_verification`。

**证据缺失门**：analysis.jsonl 无末条 → `evidence_missing`（退出码 3）。**独立于 diagnose 的 span 判空**——locate 主证据是 analysis.jsonl + result 引擎字段，trace 无 span 不阻塞验证。归因：读 manifest.assetWriteFailures 区分管道故障 vs run 未产出。

## 8. 改动清单

**Host**
1. run 结束写 result.json：`status="pending_verification"` + 引擎事实 + 新字段 `verificationCriteria`（expectedPageIdentities/mode 快照）。
2. 删除 ScenarioCompletionVerifier 的 locate 分支（~60 行规则移入 TraceTool）；调用点改为写引擎事实。
3. enumerate 分支保留不动。
4. P3.1 修复：hook 异常（BeginStepAsync/capture 失败）不再被 FireAsync Log-and-Continue 静默吞——issueSink 留痕 + FailedCount 可观测。**截图异步化已是现状**（RunAssetHook 已提交 Core 公共管道 before/after），无需异步化改造。
5. LocalVisionProvider 注入 `ITracePipeline? pipeline`（Core 接口）+ `ITraceContextProvider` + `evidenceStorage` 开关（可选，null/关闭 → 完全 no-op）：响应解析前（L89 后）拼相对路径 `vision-evidence-{stepSpanId}-{seq}.json`（**runId 由管道装配时注入**，产生点不需知道 runId）→ `pipeline.Submit((type, bytes, path))`（Core 公共管道批量异步落盘，落盘全路径 `assets/{runId}/…`）+ 同步 `RecordEventAsync("ai.evidence", parent=stepSpanId, attrs={evidence_path, evidence_type, byte_count})`（trace 引用，evidence_path 为含 runId 全路径）。spanId 读 `EngineStepSpanContext.CurrentSpanId`；per-step seq 防 ai.call 重试覆盖。
6. 新字段入目录：TraceFields 新增 `ai.evidence_path/ai.evidence_type/ai.evidence_bytes` + `TraceSpanFields.AiEvidence` profile（Basic: path/type；Extended: byte_count），同步更新 SpanFieldLevelsTests 目录覆盖断言（45 键 → 48 键）。
7. 配置：integration.config `providers.local.evidenceStorage`（MVP: `enabled` bool，默认 false；扩展点：spanTypes 列表）；ProviderPreflight 校验该段。
   *（管线泛化、IAssetStore、V2 布局迁移 = 前置设计改动清单，见 [unified-asset-pipeline-design.md](./2026-08-04-unified-asset-pipeline-design.md) §7）*

**TraceTool**
8. `RunEvidenceLoader`（run 目录 → VerificationInput 重建；构造 DI 注入 `IAssetStore`——分析器适配存储介质，存储模式扩展点；读取前分发 manifest.schemaVersion V1/V2）。
9. `VerifyEngine` + `LocateOneItemRule`（规则平移）。
10. 命令：`verify --run` / `verify --dir [--status pending] [--task-id]` / `watch --dir [--interval]`。
11. artifactPaths 解析统一走 span 属性（步级 artifact_dir；分析级 ai.evidence 属性）。
12. 单测：规则层（success/身份回退/not_verified/evidence_missing）+ 幂等 + 临时 run 目录构造（复用 lessons 记录的裸 trace 构造法）。

**测试**
13. RunScenarioAsync 尾部：调 verify CLI → 解析 verdict → 断言 → 失败时 verdict summary 并入测试失败信息。

**agent**
14. trace-analyzer.md L4 补充 verify/watch/批量命令 + 职责声明（成功判定=C# 规则，agent=归因/解读）；L4 文档对齐检查（补 interactive 子命令——当前已漏）。

**实现注意**：result.json 的 issueFingerprints 用 `IsDefaultOrEmpty` 判断（NRE 陷阱，lessons 已记录）。

## 9. 联动与验收

**联动**：P3.x（verify 是落盘完整性的第一个消费者；locate 验证不依赖截图，不阻塞 MVP）· P5.x（批量 verify 与 integration-summary 衔接）· P1.x（D-201 逻辑平移而非重写——现在手写的单测用例在 TraceTool 侧同样通过）。

**验收**：

| 项 | 方式 |
|---|---|
| 规则正确性 | VerifyEngine 单测（success/回退/失败/证据缺失 4 场景） |
| 链路走通 | LocateOneItem：run → verify → **success** |
| 幂等 | 批量/定时/串行交错不重复验证（单测） |
| 契约 | 退出码 + JSON schemaVersion 对齐现有 CLI 契约 |
| 落盘保证 | P3：finalize 后资产完整（集成验证）；P4：管道失败留痕（单测） |
| 回归 | 全量单测绿（Host 改动不破坏 enumerate/mock 路径） |

**边界（非目标）**：enumerate 不迁；不做存储抽象层（仅标注扩展点：id 关联 + RunEvidenceLoader 替换）；watch 用轮询（不引 FileSystemWatcher）；agent 不判成功。
