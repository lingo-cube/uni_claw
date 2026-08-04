# Trace-Based Validation — 验证移出 Host，基于 trace + 异步落盘资产

> 生成: 2026-08-04 · 状态: 设计稿（待审阅）
> 主题: 集成验证不放在 Host 内嵌同步校验，改为基于 trace 与过程中异步落盘的资产，通过 trace analyzer（TraceTool 规则引擎 + agent 解读）分析得到结论
> 关联: [integration-pipeline-issues.md](../../testing/integration-pipeline-issues.md)（P1.x 验证域 / P3.x 中间信息域）· [trace-analyzer.md](../../../.claude/agents/trace-analyzer.md)（L4 分析层）

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
| V-6 | 落盘 = 统一异步管道（StepAssetSink）P1-P4（§4） | 零主流程时延 + 优雅退出保证落盘 |

## 3. 架构与数据流

```
┌─ Host（run 时）────────────────────────────────────┐
│  引擎执行 → 产物产生点入队 (StepAssetSink)          │
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

## 4. 异步落盘管道（P1-P4）

**载体**：现有 `StepAssetSink`（bounded Channel 256 + SingleReader 后台 writer + DrainAsync 幂等）——**不新建通道**。

| 原则 | 内容 |
|---|---|
| **P1 单一管道** | 所有过程产出物（截图/xml、analysis.jsonl、vision-evidence、safety）经 StepAssetSink；I/O 全在后台 writer |
| **P2 零主流程时延** | `Submit` 非阻塞入队（TryWrite）；通道满 → 计数 dropped 不阻塞（MVP），失败可查 |
| **P3 优雅退出保证落盘** | finalize 协议：`DrainAsync`（幂等，双守卫安全）→ 同步写 result.json 终态 → 退出。**result.json 终态存在 ⇒ 全部异步产物已落盘**。非优雅退出由发布模型兜底（staging 不可见） |
| **P4 失败可观测** | 写失败 → issueSink 留痕 `asset_write_failed` + manifest `assetWriteFailures` 计数；dropped 同计数。verify 的 evidence_missing 可归因 |

**发布模型**（已有，复用）：run 目录 staging 建全骨架 → `Directory.Move` 原子发布。读取侧只见已发布 run。

**三条写入路径**：异步队列（StepAssetSink：截图/analysis.jsonl/vision-evidence）· 同步串行（writeGate：issues/safety-decisions/finalize 元数据）· trace 直接 append（span/execution）。

## 5. 产物关联模型（trace 为索引）

**两级产物规则**：

| 产物级 | 命名 | 关联载体 |
|---|---|---|
| 步级稳定产物（每步一套：before/after 截图+xml） | 固定名 | engine.step span 属性 `artifact_dir: "steps/0004"` |
| 分析级产物（同步可多次：vision-evidence） | 文件名带 spanId：`vision-evidence-{spanId}.json` | ai.analyze span 属性写完整相对路径 |

**机制**：
1. **id = 关联主键**：分析级产物文件名含产生它的 spanId，文件系统零覆盖；读取侧从 span 属性拿完整路径，零猜测。
2. **产物自描述**：vision-evidence.json 内部带 `spanId/stepNumber/runId`——存储模式扩展（对象存储/事件流）后仍可双向关联。
3. **路径 = id 在当前 file 存储模式的解析**：span 属性存解析后相对路径；换存储只改解析层。
4. **统一读取规则**：TraceTool artifactPaths 一律从 span 属性解析（步级取 artifact_dir，分析级取完整路径）——verify/diagnose/取证共用。

## 6. 产出物明细

| 产出物 | 内容 | 路径 | 提交者 | 状态 |
|---|---|---|---|---|
| per-step 资产 | before/after 截图 + UI XML + analysis.json | `steps/{n:D4}/` | RunAssetHook（步开始/结束 Submit） | 🔧 P3.1 修复后可用（截图同步写改异步入队） |
| analysis.jsonl | 分析精简快照（Items 名/类型/坐标） | `{runDir}/analysis.jsonl` | AnalysisWritingDecorator | ✅ 已有（D-197） |
| **vision-evidence.json** | 分析原始证据：candidates、metadata(schema/模型/configHash)、scrollHints、stage 耗时 | 步内 `steps/{n:D4}/vision-evidence-{spanId}.json`；步外 `verification/...` | LocalVisionProvider（注入 sink+runDir，分析返回 Submit） | 🔧 新增 |
| safety-decisions.jsonl | 安全决策 | `{runDir}/safety-decisions.jsonl` | 决策点 | ✅ 已有 |
| trace.jsonl | span/execution | `{runDir}/trace/{runId}/` | trace 服务 | ✅ 已有 |
| issues.jsonl / manifest / result | 留痕 + finalize 元数据 | `{runDir}/` | session 同步 | ✅ 已有 |

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
  "artifactPaths": { "screenshotPaths": ["steps/0004/after.png"], "tracePath": "trace/..." }
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
4. 截图同步写改异步入队（RunAssetHook → StepAssetSink Submit；P3.1 修复的一部分：hook 内无同步文件 I/O，提交失败 issueSink 留痕）。
5. LocalVisionProvider 注入 sink + runDirectory（对齐 AnalysisWritingDecorator 模式），分析返回 Submit 原始响应 JSON。
6. span 属性扩展：engine.step 加 `artifact_dir`；ai.analyze 加 `vision_evidence`（P3.2 正式纳入）。

**TraceTool**
7. `RunEvidenceLoader`（run 目录 → VerificationInput 重建；存储模式扩展点）。
8. `VerifyEngine` + `LocateOneItemRule`（规则平移）。
9. 命令：`verify --run` / `verify --dir [--status pending] [--task-id]` / `watch --dir [--interval]`。
10. artifactPaths 解析统一走 span 属性。
11. 单测：规则层（success/身份回退/not_verified/evidence_missing）+ 幂等 + 临时 run 目录构造（复用 lessons 记录的裸 trace 构造法）。

**测试**
12. RunScenarioAsync 尾部：调 verify CLI → 解析 verdict → 断言 → 失败时 verdict summary 并入测试失败信息。

**agent**
13. trace-analyzer.md L4 补充 verify/watch/批量命令 + 职责声明（成功判定=C# 规则，agent=归因/解读）；L4 文档对齐检查（补 interactive 子命令——当前已漏）。

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
