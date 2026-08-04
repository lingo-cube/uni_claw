# 集成测试链路问题清单

> 生成: 2026-08-04 · 适用范围: `EmulatorScenarioIntegrationTests` + Host scenario run 链路
> 关联: [integration-tests.md](integration-tests.md) · [test-tiers.md](test-tiers.md)
> 状态图例: ✅ 已修 / 🚧 修复中 / ⏳ 待修 / 🔍 待调研

---

## 一句话总结

Local vision provider 链路已跑通核心能力（OCR 全图路径 19.3s→2.8s、点击导航有效、YOLO+OCR+融合质量达标），
但集成测试仍卡在 **验证规则不匹配**（`target_page_identity_not_verified`），且存在 **配置散乱、中间信息不可追溯、
耗时冗余、基线缺失** 四类系统性问题。下文按问题域列出细则。

---

## 问题总览

| ID | 问题 | 影响 | 优先级 | 状态 |
|---|---|---|---|---|
| P1.1 | 最终校验身份恒空（`CurrentPath=[]`） | 测试恒失败，链路无法验收 | P0 | ✅ |
| P1.2 | 遍历期/校验期身份来源不一致 | 同屏两阶段身份结论不同 | P1 | ✅ |
| P1.3 | 身份回退无单测覆盖 —— 已作废（D-211：D-201 回退随 locate 路径死代码化） | — | — | ✅ |
| P2.1 | provider/model 默认硬编码在测试代码 | 换 provider 要改代码/手设 env | P1 | ✅ |
| P2.2 | outputRoot 命名/位置硬编码 | 无时区、无场景级目录 | P2 | ✅ |
| P2.3 | scope 门控只走 env，无配置文件 | CI/本地差异靠记忆 | P2 | ✅ |
| P2.4 | 视觉服务 env 靠手动 export | 跑一次要 export 5 个变量 | P1 | ✅ |
| P2.5 | recordBaseline/keepRuns 等计划 knobs 未落地 | 基线流程不可用 | P2 | 🚧 |
| P2.6 | deepseek provider 未入 config providers | config 无法绑定 deepseek | P3 | ✅ |
| P2.7 | sensenova 模型双键语义割裂 | 意图推理模型 config 管不到 | P3 | ✅ |
| P2.8 | UNICLAW_VISION_MODE 一变量两义 | 设视觉模式会污染 run mode | P2 | ⏳ |
| P2.9 | UNICLAW_PROVIDER 命名空间混淆 | L2 覆盖与 CLI 回退难区分 | P3 | ✅ |
| P2.10 | providers.local.model 是死值 | 误导：填 sensenova 模型名但从不生效 | P2 | ✅ |
| P3.1 | 截图不落盘（hook 异常被吞） | 失败现场无图可查 | P0 | 🚧 |
| P3.2 | trace 无 artifact 引用 | 无法从 trace 定位截图/分析 | P1 | ✅ |
| P3.3 | steps/ 目录内容残缺 | 只有 safety-decision，无 before/after 资产 | P1 | 🚧 |
| P3.4 | 中间信息无固定布局约定 | 读取侧无稳定路径可依赖 | P2 | ✅ |
| P4.1 | 引擎观察步 ADB 地板 ~1.15s/步 | 12 步约 14s 纯 ADB 开销 | P1 | ⏳ |
| P4.2 | 滚动后强制全量视觉 3.25s/次 | 4 次滚动 ~13s | P1 | ⏳ |
| P4.3 | 最终校验 4.3s（750ms + 全量分析） | 每次 run 固定开销 | P2 | 🚧 |
| P4.4 | server 每 run 冷启动 ~6s | 2 测试 ≈ 12s | P2 | ⏳ |
| P4.5 | YOLO 640px CPU 3.4s/次 | 单次分析占大头 | P3 | ⏳ |
| P4.6 | stale uvicorn 进程堆积 | 占内存/抢 CPU | P3 | ⏳ |
| P5.1 | 无 emulator-info 采集 | 基线无法绑定 AOSP 版本 | P2 | ⏳ |
| P5.2 | 无聚合报告 integration-summary.json | CI 无可消费摘要 | P2 | ⏳ |
| P5.3 | 无基线记录/对比 | 指标漂移无感知 | P2 | ⏳ |
| P5.4 | artifact 无限累积 | 磁盘膨胀 | P3 | ⏳ |
| P6.1 | D-199/D-200/D-201/OCR 路径未录决策 | 决策无据可查 | P2 | 🚧 |

---

## 细则

### 域 1 · 验证规则与本地视觉 provider 不匹配

#### P1.1 最终校验身份恒空 — 测试恒失败 🔍(根因已定位)

- **现象**: `completionReason=target_page_identity_not_verified`，`FailureDetail: Post-action page identity '<empty>' did not match...`。点击已执行且导航有效（手动 `input mouse -d 0 tap 460 1738` 验证 About 页正常打开），失败发生在校验而非执行。
- **根因**: 最终校验 [HostCommands.cs:840](src/UniClaw.Host/Commands/HostCommands.cs#L840) 用 `services.VisualPageAnalyzer` 纯视觉分析取 `finalAnalysis.CurrentPath.LastOrDefault()`；而 [LocalVisionProvider.cs:331](src/UniClaw.LocalVisionProvider/LocalVisionProvider.cs#L331) **硬编码 `CurrentPath = []`** —— 结构化视觉管道没有 LLM 式的页面路径推断能力。`expectedPageIdentities`（About device/About emulated device/About phone...）永远匹配不上。
- **影响**: 本地视觉 provider 下 locate 模式永远失败，链路无法验收。
- **修复**: ✅ **已落地（2026-08-04，决策落号 D-218「验证移出 Host」）**：从 Host 管线移除 locate 的 post-action 页面身份校验，校验职责移交 TraceTool `VerifyEngine` + `LocateOneItemRule`（D-201 语义平移）。`ScenarioCompletionVerifier` 仅保留 enumerate 分支；run 结束写 `pending_verification` + 引擎事实 + `criteria.json`；`verify --run/--dir/watch --run-id` 命令产出终判，写回仅 pending（终态永不覆写）。实施走 OpenSpec change **unified-asset-pipeline-trace-validation**（已归档，tasks 22/22，specs 已同步 `openspec/specs/trace-based-validation/`）。**编号映射**：本台账原引"D-211"（方案编号）→ log.md 实际落号 **D-218**（D-211 在 log.md 被资产管线「引用事件 + 字节物理分离」占用，同 P6.1 注的模式）。

#### P1.2 遍历期/校验期身份来源不一致

- **现象**: 同一次 run，遍历期 step 12 决策日志 pageIdentity="Settings"（正确），校验期 finalIdentity="<empty>"（错误）。
- **根因**: 遍历期身份来自 [ScenarioObservation.cs:89-91](src/UniClaw.Host/Runner/ScenarioObservation.cs#L89-L91) 的 `CurrentPath.LastOrDefault() ?? FindPageIdentity(hierarchy)`（UIAutomator 标题节点回退）；校验期只用纯视觉 `VisualPageAnalyzer`（无 hierarchy 合并，见 [HostCommands.cs:838-841](src/UniClaw.Host/Commands/HostCommands.cs#L838-L841) 注释 "Post-target verification demands AI-quality page identity"）。
- **影响**: 同一物理屏幕两阶段身份结论不同，属于系统不一致。
- **修复**: ✅ 随 P1.1（D-218）一并解决——校验期不再做页面身份匹配，两阶段身份来源不一致问题随校验移除而消失；不再需要 hierarchy 兜底。

#### P1.3 身份回退无单测保护 — 已作废 ✅

- **修复**: ~~⏳ `ScenarioCompletionVerifierTests` 增加"CurrentPath 空 + Items 含预期身份文本 → success"用例。~~ **作废（2026-08-04）**：D-211 移除 locate post-action 身份校验后，D-201 回退成为死代码，无单测需求。

### 域 2 · 配置散乱

#### P2.1 provider/model 默认硬编码

- **现象**: locate→`sensenova`、enumerate→`mock` 写死在 [EmulatorScenarioIntegrationTests.cs:48-63](tests/UniClaw.Host.Tests/Integration/EmulatorScenarioIntegrationTests.cs#L48-L63)；本次跑 local 全靠 `UNICLAW_INTEGRATION_PROVIDER=local` 手设（漏设即撞云端 Sensenova 空响应失败，实测 3m33s）。
- **修复**: ✅ `integration.config.json` —— providers 按 id 分块（local 带 visionServer），scenarios 引用 provider id（[integration-config.md](integration-config.md) §3）。

#### P2.2 outputRoot 命名/位置硬编码

- **现象**: `artifacts/runs/integration/{scope}/{yyyyMMdd-HHmmss}` 无时区、无 scenario 级目录。
- **修复**: ✅ 并入 config `emulator.outputRoot/runNaming`（UTC `yyyyMMddTHHmmssZ` + `{scenarioId}` 目录层），`RunScenarioAsync` 按 config 拼接。

#### P2.3 scope 门控只走 env

- **现象**: [IntegrationFactAttribute.cs:17-30](tests/UniClaw.Host.Tests/Integration/IntegrationFactAttribute.cs#L17-L30) 只认 `UNICLAW_INTEGRATION_SCOPES`。
- **修复**: ✅ config 提供 scenario→scope 映射（`scenarios[].scope`，loader 校验唯一）；env 仍是 CI 选择器（保留）。

#### P2.4 视觉服务 env 靠手动 export

- **现象**: `UNICLAW_OMP_THREADS/OCR_BACKEND/OCR_TEXT_SCORE/YOLO_MODEL/LABEL_MAPPING/VISION_SOCK` 每次跑前手设。
- **修复**: ✅ 测试按 config `providers.local.visionServer` 段注入 env（`ApplyVisionServerEnv`，见 [integration-config.md](integration-config.md) §4）；`OCR_LANG/OCR_PARALLEL` 明确 out-of-scope 用 server 默认。

#### P2.5 计划 knobs 未落地

- **现象**: `recordBaseline`/`keepRuns`/`emulator-info`（glittery-launching-clover.md）只有设想无实现。
- **修复**: 🚧 已并入 config `emulator` 段（`recordBaseline`/`keepRuns` 字段 + 校验），消费逻辑随阶段 5 落地。

#### P2.6 deepseek provider 未入 config providers

- **现象**: Host 代码里 deepseek 是真实 provider——local 模式的 text 路由默认（[HostCommands.cs:1125](src/UniClaw.Host/Commands/HostCommands.cs#L1125) 起，`providers["deepseek"]` + `DEEPSEEK_BASE_URL/MODEL/API_KEY`）；但 config `KnownProviders` = {local, sensenova, claude, qwen, mock}，无 deepseek。
- **影响**: config 想绑定 deepseek 会 fail-fast 报未知 provider；两个 provider 集合单向漂移。
- **修复**: ✅ **决策 (b) D-208——deepseek 是内部 text 路由键，不进测试 provider 集**。证据：`CreateProviders` 无 `providerId=="deepseek"` 分支（会落到 [HostCommands.cs:1286-1293](src/UniClaw.Host/Commands/HostCommands.cs#L1286-L1293) claude 检查抛"does not expose vision capability"）——用户无法 `--provider deepseek`；deepseek 键由 local（[1271](src/UniClaw.Host/Commands/HostCommands.cs#L1271)，独立 DEEPSEEK_* 凭据）和 qwen two_stage（[1231](src/UniClaw.Host/Commands/HostCommands.cs#L1231)，复用 qwen 凭据）各自装配，语义是"文本推理角色"而非独立 provider。config 的 KnownProviders 是"用户可选 provider"集合，deepseek 不属于。已文档声明（[integration-config.md](integration-config.md) §3 providers 表）。

#### P2.7 sensenova 模型双键语义割裂

- **现象**: sensenova 的模型名有两个键——主链路走 `options.Model`（config `providers.sensenova.model` 管辖，值 `sensenova-6.7-flash-lite`）；意图推理走 `SENSENOVA_MODEL ?? "deepseek-v4-flash"`（[CreateIntentExtractor](src/UniClaw.Host/Commands/HostCommands.cs#L1345-L1350)，env 管辖）。
- **分析结论** (2026-08-04 按职责×能力复核): `deepseek-v4-flash` 经 sensenova 端点**是有意设计**（注释写明用便宜模型做意图推理），非复制粘贴残留、非 bug。剩余问题：
  1. **语义割裂**（与 P2.9 同类）：同一"provider 用哪个模型"概念两个键，config 管不到意图推理模型；
  2. **隐式假设无校验**："端点托管 deepseek 模型名"无配置层兜底，模型不可用时意图推理静默失败；
  3. 文档误导（最初被误判为复制粘贴，说明该默认值缺文档锚定）。
- **修复**: ✅ 决策已定并落地——意图推理模型收进 config 管辖：`providers.sensenova.intentModel` 字段（可选，仅 sensenova 可挂，loader 校验归属），测试装配期注入 `SENSENOVA_MODEL`（env 已设优先，模式同 visionServer 注入）。主链路模型与意图推理模型现在都在 config 内，双键语义消除。剩余待验证：sensenova 端点是否真正托管 `deepseek-v4-flash` 模型名（运行期确认，不阻塞）。

#### P2.8 UNICLAW_VISION_MODE 一变量两义

- **现象**: [HostCommands.cs:1216](src/UniClaw.Host/Commands/HostCommands.cs#L1216)（qwen 分支）读 `UNICLAW_VISION_MODE` 作视觉模式（`single`/`two_stage`，默认 `"single"`）；[HostCommands.cs:1605](src/UniClaw.Host/Commands/HostCommands.cs#L1605) Parse 读同一变量作 run mode（默认 `"mode-a"`）。
- **影响**: 设 `UNICLAW_VISION_MODE=two_stage`（合法视觉模式）会把 run 命令的 mode 设成 `"two_stage"`，或反之设 mode 值污染视觉模式。
- **决策** (2026-08-04 取证): 🔍 **拆分——Parse 读点改用新变量 `UNICLAW_RUN_MODE`，`UNICLAW_VISION_MODE` 归视觉（D-209）**。证据：全仓库仅 2 个读点（1216/1796），无第三处引用，拆分无残留风险。方向：run mode 是 CLI 回退链概念（与 `UNICLAW_OUTPUT/PROVIDER/MODEL/RUN_PURPOSE/TASK_ID` 同族，见 [integration-config.md](integration-config.md) §9.1），视觉模式变量名天然贴合视觉语义。**待实施**（Host 代码 1 行 + Parse 单测 + §9.1 表更新）——2026-08-04 复核 [HostCommands.cs:1796](src/UniClaw.Host/Commands/HostCommands.cs#L1796) 仍读 `UNICLAW_VISION_MODE ?? "mode-a"` 作 run mode，拆分未实施。

#### P2.9 UNICLAW_PROVIDER 命名空间混淆

- **现象**: `UNICLAW_PROVIDER`（Host CLI 回退，L3 内侧）vs `UNICLAW_INTEGRATION_PROVIDER`（测试 config 覆盖，L2），前缀相似。
- **影响**: 手设错变量名时静默走默认 provider（claude/文件值），难排查。
- **修复**: ✅ 边界已划清（[integration-config.md](integration-config.md) §9.3 命名空间表，D-207 三层校验链的 §9 侧）。剩余"统一前缀 `UNICLAW_CLI_*`"为可选优化（需同步测试/Host 调用点，收益低，不排期）。

#### P2.10 providers.local.model 是死值

- **现象**: config `providers.local.model = "sensenova-6.7-flash-lite"`，但 local 分支下 Host 完全忽略 `options.Model`（[HostCommands.cs:1119-1130](src/UniClaw.Host/Commands/HostCommands.cs#L1119-L1130)）：视觉走本地 YOLO+OCR（visionServer 段），text 固定走 `DEEPSEEK_MODEL ?? "deepseek-v4-flash-0731"`（[HostCommands.cs:1266-1275](src/UniClaw.Host/Commands/HostCommands.cs#L1266-L1275)）。该值只是满足 loader "model 必填" 校验的占位。
- **根因**: loader 的 model 必填校验未区分 provider 语义——local 的模型由 visionServer 段 + DEEPSEEK_MODEL 决定，model 字段无消费方。
- **影响**: 误导（文档曾推荐它）；换 model 会以为改 config 生效。
- **修复**: ✅ 方案 (a) 落地——loader 差异化校验：云端（sensenova/claude/qwen）model 必填，local/mock 可省略；config 删掉死值；新增 `ProviderPreflight`（测试装配期按 provider 预检凭据 env + 本地路径存在性，缺什么当场 fail-fast）。方案 (b) 的意图推理部分已随 P2.7 落地（`sensenova.intentModel` 入 config）；local 的 text 模型（`DEEPSEEK_MODEL`）仍由 env 管辖、未入 config——local 无单一 text 模型键的需求，暂不追。

### 域 3 · 中间信息不可追溯

#### P3.1 截图不落盘 🔍

- **现象**: run 目录 `steps/` 下只有 4 个 `safety-decision.json`（对应决策步），**全 run 无一张截图/XML**；`SuccessEvidence` 引用的 `steps/0012/after.png` 不存在。
- **根因**: [RunAssetHook.cs:54](src/UniClaw.Host/Hooks/RunAssetHook.cs#L54) 每步 `WriteBeforeAsync` 经 sink 落盘；但 [TraversalEngine.cs:687-700](src/UniClaw.Core/Traversal/TraversalEngine.cs#L687-L700) `FireAsync` 是 **Log-and-Continue** —— hook 异常（疑 `BeginStepAsync` causal-next 门）被静默吞掉，无 trace/issue 留痕。`safety-decision.json` 由 [RunAssets.cs:529-548](src/UniClaw.Host/Artifacts/RunAssets.cs#L529-L548) 独立路径写出，故只有它存在。
- **影响**: 失败现场无图可查；"从 trace 快速定位问题"（本会话目标）无从谈起。
- **修复**: 🚧 **"吞异常无留痕"已修**——StepAssetSink 删除，资产提交改走 Core `ITracePipeline`（bounded Channel + 批量 flush），hook/提交失败由 `asset_write_failed` issue 留痕（[HostCommands.cs:1040-1064](src/UniClaw.Host/Commands/HostCommands.cs#L1040-L1064)，path + exception），不再静默吞掉；`FileAssetStore` staging 原子写 + writeGate。**"落盘回归"待验证**——需 E2E run 确认 `steps/{n:D4}/before|after.png/xml + analysis.json` 真实产出（机制在 `RunAssets.cs` V2 布局，P3.3 随此回归）。

#### P3.2 trace 无 artifact 引用

- **现象**: 最新 run trace.jsonl 仅 4 事件（2×ai.call + ai.analyze），无任何截图/文件引用。
- **修复**: ✅ **ai.evidence 引用事件已实现**（unified change，TraceFields 45→48）：产生点提交时发同步引用事件，`evidence_path` 为**相对路径**（`vision-evidence-{stepSpanId}[-{seq}].json`，E1 fix）——producer 不知 runId（装配期注入），reader 从 run 上下文解析 `assets/{runId}/{relativePath}`。已随 [2026-08-04-unified-asset-pipeline-trace-validation-prd.md](docs/prd/2026-08-04-unified-asset-pipeline-trace-validation-prd.md) 文档化。

#### P3.3 steps/ 目录内容残缺

- **修复**: 🚧 V2 布局已定义 `steps/{n:D4}/before|after.png/xml + analysis.json`（PRD §3.1，span tree 映射 engine.step 目录），但**产出真实存在性**待 P3.1 落盘回归后确认（当前管道重写后未经 E2E run 验证）。

#### P3.4 中间信息无固定布局约定

- **修复**: ✅ V2 布局约定已文档化（PRD §3.1）：`trace/{runId}/`（事件流）+ `assets/{runId}/`（字节，`steps/{n:D4}/` 唯一中间信息位置 + `criteria.json`/`pending_verification` 独立文件），`safety` 决策不落盘（trace 覆盖）。读取侧按 `assets/{runId}/{relativePath}` 解析。

### 域 4 · 耗时（实测 run 59s：引擎 28.9s + 引擎外 ~30s）

#### P4.1 引擎观察步 ADB 地板 ~1.15s/步

- **实测**: 11 个无动作观察步 gap 1.10-1.26s（analysis.jsonl 时间戳），构成 = screencap + uiautomator dump + dumpsys activity 三次 ADB 往返。
- **修复**: ⏳ 截图/dump 跨步缓存（同屏复用，动作后失效），预计 12.6s→4s。

#### P4.2 滚动后强制全量视觉 3.25s/次

- **实测**: 4 次滚动后 gap 3.07-3.50s（YOLO+OCR 全量）。
- **修复**: ⏳ 滚动后先解析 dump，目标行出现才跑视觉（UIA-first），预计 13s→5s。

#### P4.3 最终校验 4.3s

- **实测**: [HostCommands.cs:837](src/UniClaw.Host/Commands/HostCommands.cs#L837) 750ms 稳定窗口 + 全量 YOLO+OCR ~3.5s。
- **修复**: 🚧 **判定开销已随 P1.1（D-218）移除**（不再做身份匹配），但最终视觉 pass **仍保留**——[HostCommands.cs:948](src/UniClaw.Host/Commands/HostCommands.cs#L948) `AnalyzeCurrentPageAsync` 仍在跑（750ms 稳定窗口 + 全量 YOLO+OCR ~3.5s），产出写引擎事实（finalPath/finalItems）供 trace 分析。~4.3s/run 固定开销未省去；优化（跳过或降级该 pass）归阶段 4 计时线，不再阻塞验证链路。

#### P4.4 server 每 run 冷启动 ~6s

- **实测**: YOLO load+warm 4.0s + OCR warm 0.7s + uvicorn ~1s（PythonVisionService 每 run 删 socket 重建）。
- **修复**: ⏳ harness 级单例复用，2 测试省 2×6s。

#### P4.5 YOLO 640px CPU 3.4s/次（实验）

- **修复**: ⏳ 480px 或 int8 量化，需质量验证。

#### P4.6 stale uvicorn 进程堆积

- **现状**: 4 个残留进程（9981/9766/7558/14807）占内存。
- **修复**: ⏳ PythonVisionService 退出清理 + 手动清当前残留。

### 域 5 · 基线/资产缺失

#### P5.1-P5.4 emulator-info / 聚合报告 / 基线对比 / 清理策略

- 全部 ⏳，设计见 glittery-launching-clover.md（Phase 2-4），配置键并入 integration.config.json。

### 域 6 · 决策未记录

#### P6.1 决策未录 log.md

- **待录**: ~~D-199/D-200/D-201~~（已由 trace 线录入 log.md）、~~config 线 D-202–D-207/D-210~~（✅ 2026-08-04 已录 log.md **D-203–D-209**，编号冲突已映射）、~~unified 线 D-210–D-221~~（✅ 2026-08-04 归档提取已录，含 D-218「验证移出 Host」——本台账原引"D-211 方案"的正式落号；**编号映射**：台账方案号 D-211 → log.md **D-218**，log.md 的 D-211 被资产管线「引用事件 + 字节物理分离」占用，同 config 线映射模式）、~~D-198 OCR 后端切换~~（✅ 已录）。**仍待录**: OCR 全图 det+批量 rec 路径重构、并行度 2→4、预热内核化（local-vision 实现细节，归 local-vision 线 backlog）。
- **修复**: 🚧 主体已完成（2026-08-04 归档提取 D-210–D-221）；剩余 3 项 local-vision 实现决策待补录，不阻塞。

---

## 修复顺序（阶段依赖）

1. **阶段 1**（P1.1/P1.2）: ✅ **完成** —— 由 OpenSpec change **unified-asset-pipeline-trace-validation** 落地（2026-08-04 已归档，tasks 22/22，specs 同步 `openspec/specs/trace-based-validation/` 等）；locate post-action 校验移除（D-218）+ 测试断言切换引擎级 + 进程内 VerifyEngine 终判（[EmulatorScenarioIntegrationTests.cs:312-316](tests/UniClaw.Host.Tests/Integration/EmulatorScenarioIntegrationTests.cs#L312-L316)）
2. **阶段 2**（P2.x）: ✅ 完成 —— 由 OpenSpec change **integration-test-config** 追踪（4/4 artifacts，apply 通过）；integration.config.json + 加载器 + RunScenarioAsync 改造（含启动横幅 `[integration-config]` 打印生效配置）+ ProviderPreflight + 规范文档（[integration-config.md](integration-config.md)），20 个配置/预检单测通过（loader 14 + preflight 6）；P2.5 消费逻辑留给阶段 5；**P2.8 拆分仍未实施**（D-209，2026-08-04 复核 :1796 仍读 UNICLAW_VISION_MODE）
3. **阶段 3**（P3.x）: 🚧 部分完成 —— P3.2/P3.4 ✅（ai.evidence 引用事件 + V2 布局文档化，unified change）；P3.1/P3.3 🚧 待 E2E 落盘回归（管道已重写，产出存在性未验证）
4. **阶段 4**（P4.x）: ⏳ 待做 —— adb 耗时实测 → dump 缓存 → UIA-first → server 复用；P4.3 判定已移除但视觉 pass 仍在（4.3s 开销未省，随本阶段计时线）
5. **阶段 5**（P5.x）: ⏳ 待做 —— emulator-info / summary / 基线 / 清理（P2.5 消费逻辑随此落地）
6. **阶段 6**（P6.1）: 🚧 主体完成 —— D-210–D-221 已录（含 D-218）；剩余 3 项 local-vision 实现决策（OCR 路径重构/并行度/预热）待补录，不阻塞
