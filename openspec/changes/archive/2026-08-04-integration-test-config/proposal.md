# Proposal: Integration Test Config

## Why

集成测试的运行参数（provider/model/outputRoot/scope 门控/视觉服务 env）散落在测试代码硬编码与手动 export 中：漏设 `UNICLAW_INTEGRATION_PROVIDER` 即静默撞云端 Sensenova 空响应（实测 3m33s），outputRoot 命名不可预测，CI/本地差异靠记忆。运行配置需要**单点真源 + fail-fast 校验 + env 覆盖通道**。

## What Changes

- **新增 `tests/UniClaw.Host.Tests/Integration/integration.config.json`**（schema `uniclaw.integrationConfig.v1`）——测试运行配置单点真源：`emulator`（serial/outputRoot/runNaming/keepRuns/recordBaseline）、`providers`（按 id 分块；`visionServer` 只挂 local）、`scenarios`（绑定 provider/mode/timeout）
- **新增 `IntegrationConfigLoader`**（tests 项目）——三层校验链：`Load()` 文件结构（schema/归属/枚举/云端 model 必填）→ `ResolveScenario()` 实际生效配置（env 覆盖后仍校验必填）→ `ProviderPreflight.Check()` 运行时前提（凭据 env/文件存在性）；全部 fail-fast
- **新增 `ProviderPreflight`**——按 provider 预检（mock 无检；local 查 `DEEPSEEK_API_KEY` + 模型/映射文件；claude/sensenova/qwen 查凭据 env 或 `~/.litellm/secrets.json`）
- **新增 provider env 注入**（`ApplyProviderEnv`，SetEnvIfAbsent 手设优先）——`providers.local.visionServer` → `UNICLAW_VISION_*`/`OMP`/`OCR`/`YOLO`/`LABEL_MAPPING`（相对路径解析为 repo-root 绝对）；`providers.sensenova.intentModel` → `SENSENOVA_MODEL`（P2.7 双键语义消除）
- **修改 `EmulatorScenarioIntegrationTests`**——`RunScenarioAsync` 从 config 解析 provider/model/outputRoot/超时，装配期接入 preflight；测试代码不再硬编码任何运行参数
- **新增文档**——`docs/testing/integration-config.md`（配置规范：7 层职责边界模型 + schema + 校验链 + 决策 D-202–D-207）、`docs/testing/integration-pipeline-issues.md`（问题台账，P1–P6 域状态追踪）

**非目标**：不改 Host/Device/Core 生产代码 —— config 是测试侧装配层，经 env 通道注入；Host 的 CLI env 回退保持为直跑兜底。

**台账边界项**（integration-pipeline-issues.md 追踪，决策或实施不在本 change，仅配置域相关项）：

- P2.5：`recordBaseline`/`keepRuns` 配置键已落地（定义 + 校验），**无消费方** —— 消费逻辑属阶段 5（基线录制/清理，独立 change）
- P2.6：deepseek 是 Host 内部 text 路由键，不在 config `KnownProviders` —— ✅ **D-208 已定案**（内部路由键不进 config，声明已落 integration-config.md §3），本 change 仅验证对齐
- P2.8：`UNICLAW_VISION_MODE` 一变量两义 —— ✅ **D-209 已定案**（拆分 `UNICLAW_RUN_MODE`，读点取证完成）；实施属 **Host 生产代码变更**，台账跟踪、独立 change
- P2.9：`UNICLAW_PROVIDER`（L3 内侧 CLI 回退）vs `UNICLAW_INTEGRATION_PROVIDER`（L2 测试覆盖）命名空间混淆——边界已文档化（integration-config.md §9.3）✅，代码改名推迟

## Capabilities

### New Capabilities

- `integration-test-config`: `integration.config.json` 的 schema、`IntegrationConfigLoader` 三层校验链（结构 → 实际生效 → 运行时前提）、L2 env 覆盖优先级（file < env < param）。覆盖 D-202–D-205、D-207。
- `provider-preflight`: 各 provider 运行时前提预检（凭据 env / secrets 文件 / 本地路径存在性），装配期 fail-fast。
- `provider-env-injection`: L1 → L4 env 注入映射（visionServer 段、`intentModel` → `SENSENOVA_MODEL`），SetEnvIfAbsent 手设/CI 优先。覆盖 D-206。

### Modified Capabilities

（无 — 现有 spec 的 REQUIREMENTS 不变；本 change 全部为测试侧新增装配层，`android-emulator-integration` 的仿真器生命周期需求不受影响）

## Impact

- **新增**：`tests/UniClaw.Host.Tests/Integration/integration.config.json`、`IntegrationConfig.cs`（loader + DTO + 校验）、`IntegrationConfigTests.cs`、`ProviderPreflight.cs`、`ProviderPreflightTests.cs`、`docs/testing/integration-config.md`、`docs/testing/integration-pipeline-issues.md`
- **修改**：`tests/UniClaw.Host.Tests/Integration/EmulatorScenarioIntegrationTests.cs`（RunScenarioAsync 从 config 解析 + env 注入 + preflight）、`tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj`（config Content 拷贝）
- **未改**：`src/` 生产代码（Host/Device/Core）—— 配置经 env 通道注入，无运行架构改动
- **兼容性**：配置文件纯新增，测试代码行为由参数驱动；env 通道保持"手设 > 注入"，CI 覆盖不受影响
- **复用**：label-mapping.json 的 schema 版本 + 构造期校验模式（D-202 对齐）、`SetEnvIfAbsent` 注入模式（D-206 复用 visionServer 注入）
