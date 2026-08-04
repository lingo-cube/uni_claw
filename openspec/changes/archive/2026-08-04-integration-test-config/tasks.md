# Tasks: Integration Test Config

> 状态标记：`[x]` = 已在 `feature/refactor` 分支实施并通过核查（2026-08-04，openspec-researcher 逐文件验证）；`[ ]` = 待执行/待决策。
> 规范文档与台账：`docs/testing/integration-config.md` · `docs/testing/integration-pipeline-issues.md`

## 1. 配置单点真源（已实施 ✅）

- [x] 1.1 新增 `tests/UniClaw.Host.Tests/Integration/integration.config.json` —— schema `uniclaw.integrationConfig.v1`；emulator（serial/outputRoot/runNaming/keepRuns/recordBaseline）、providers（local 带 visionServer / sensenova 带 model+intentModel / claude / qwen / mock）、scenarios（locate-one-item→local、enumerate-settings-safely→mock）
- [x] 1.2 新增 `IntegrationConfig.cs` —— `IntegrationConfigLoader.Load/ResolveScenario/ResolveScenarioByFile`；结构校验（schema 版本/emulator 必填/KnownProviders/云端 model 必填/visionServer 归属与 ocrBackend 枚举/intentModel 归属/mode 枚举/timeout 正数/keepRuns≥0）+ 实际生效校验（env 覆盖后云端 model 空 → fail-fast）+ `UNICLAW_INTEGRATION_PROVIDER/MODEL` env 覆盖
- [x] 1.3 新增 `IntegrationConfigTests.cs` —— 14 个 `[Fact]`：默认加载/归属/引用存在性/文件回退 scope/env 覆盖优先/未知 provider/非 local visionServer/schema 不匹配/非法 ocrBackend/云端无 model/local-mock 无 model 可载/intentModel 归属/覆盖后云端 model 空 fail-fast/覆盖后非云端可载/缺文件 fail-fast

## 2. Provider 运行时预检（已实施 ✅）

- [x] 2.1 新增 `ProviderPreflight.cs` —— `Check(scenario, repoRoot)`：mock 免检；local 查 `DEEPSEEK_API_KEY` + yoloModel/labelMapping 文件存在性（repo-root 解析）+ visionServer 段存在；claude 查 `ANTHROPIC_API_KEY`；sensenova/qwen 查各自 key 或 `~/.litellm/secrets.json`；缺失抛 `InvalidOperationException` 带"缺什么+怎么设"
- [x] 2.2 新增 `ProviderPreflightTests.cs` —— 6 个 `[Fact]`：mock 通过/local 缺 key 失败/local 缺文件失败/local 就绪通过/local 无 visionServer 失败/claude 缺 key 失败

## 3. 测试装配改造（已实施 ✅）

- [x] 3.1 修改 `EmulatorScenarioIntegrationTests.cs` —— `RunScenarioAsync`（:83）Load → ResolveScenarioByFile → `ApplyProviderEnv`（:99，SetEnvIfAbsent 手设优先）→ `ProviderPreflight.Check`；outputRoot/provider/model/mode 全部从 config 解析（:112-126），测试代码零硬编码运行参数
- [x] 3.2 修改 `UniClaw.Host.Tests.csproj`（:36-38）—— integration.config.json Content 拷贝 `CopyToOutputDirectory=PreserveNewest`
- [x] 3.3 新增 `RunScenarioAsync` 启动横幅（Console 输出）——config 解析 + env 注入 + preflight 完成后、跑 Host 前，打印生效配置：场景 id/scope、provider/model/mode/timeout、outputRoot、serial、注入的 env 清单（手设优先标注）、preflight 结论；让"跑了什么"跑前可见（测试侧，不改 `src/`）—— ✅ 2026-08-04 实测（`PrintStartupBanner`，build 0 错误，20/20 测试通过）

## 4. 规范与台账文档（已实施 ✅）

- [x] 4.1 新增 `docs/testing/integration-config.md` —— 7 层配置全景（L0-L6）+ 职责边界 + schema + L1→L4 env 注入映射 + 三层校验链 + 新增 scenario 流程 + 运行示例 + 决策 D-202–D-207
- [x] 4.2 新增 `docs/testing/integration-pipeline-issues.md` —— P1–P6 域问题台账（含各 P2.x 修复状态与细则）

## 5. 决策对齐与台账同步（D-208/D-209 已由并行工作定案）

- [x] 5.1 **P2.6 deepseek（D-208 已定案，验证型任务）**：验证 integration-config.md §3 providers 表声明（deepseek = 内部 text 路由键，无 `--provider deepseek` 入口）与台账状态一致；无代码改动（决策 b）—— ✅ 2026-08-04 核查：§3 声明（[:135](docs/testing/integration-config.md#L135)）与代码事实（CreateProviders 无 deepseek 分支，`--provider deepseek` 落 claude 检查抛异常）及台账 ✅ 全部吻合
- [x] 5.2 **P2.8 拆分（D-209 已定案，实施在台账）**：确认设计对齐——读点 [HostCommands.cs:1216](src/UniClaw.Host/Commands/HostCommands.cs#L1216) = 视觉模式 / [:1605](src/UniClaw.Host/Commands/HostCommands.cs#L1605) = run mode（`UNICLAW_RUN_MODE` 尚不存在）；实施（Host 1 行 + Parse 单测 + §9.1 表更新）属 Host 生产代码变更，随独立 change 落地 —— ✅ 2026-08-04 核查：1216 视觉（single/two_stage）、1605 run mode（mode-a）、`UNICLAW_RUN_MODE` 全仓库零命中，与台账 ⏳ 一致
- [x] 5.3 **P2.9 边界确认（✅ 已划清）**：验证 §9.3 边界表（L2 `UNICLAW_INTEGRATION_*` vs L3 内侧 `UNICLAW_*`）与台账状态；`UNICLAW_CLI_*` 前缀为可选优化，不排期 —— ✅ 2026-08-04 核查：§9.3 边界表（[:292-301](docs/testing/integration-config.md#L292-L301)）与台账 P2.9 ✅ 一致

## 6. 验收

- [x] 6.1 `IntegrationConfigTests`（14）+ `ProviderPreflightTests`（6）全绿：`dotnet test --filter "FullyQualifiedName~IntegrationConfigTests|FullyQualifiedName~ProviderPreflightTests"` —— ✅ 2026-08-04 实测 20/20 通过（124ms）
- [ ] 6.2 集成链路验收：`UNICLAW_INTEGRATION_SCOPES=scenario-locate dotnet test --filter LocateOneItem` 从 config 解析 provider=local 复跑至 success —— **依赖验证域问题解决（P1.1 最终校验身份恒空，台账阶段 1 跟踪）**；本 change 只保证 config 侧就绪
- [x] 6.3 台账更新：本 change 合入后 integration-pipeline-issues.md 阶段 2 标记为本 change 追踪，遗留项（P2.5 阶段5/P2.8）状态同步 —— ✅ 2026-08-04：阶段 2 行已标注 integration-test-config 追踪；P2.5（🚧 消费待阶段 5）/P2.8（⏳ D-209 待实施）状态与台账一致
