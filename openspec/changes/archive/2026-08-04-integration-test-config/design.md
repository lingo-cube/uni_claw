# Design: Integration Test Config

## Context

集成测试链路的运行参数此前散落三处：测试代码硬编码（locate→sensenova、enumerate→mock）、手动 export（视觉服务 6 个 env）、无配置真源（scope 门控只走 env）。漏设 provider env 即静默撞云端空响应（实测 3m33s）。本 change 在 `feature/refactor` 分支已实现主体（**此前未经 OpenSpec change 追踪**），本设计将其正式化并给出剩余配置边界问题的决策。

**已实施事实**（2026-08-04 核查，`tests/UniClaw.Host.Tests/Integration/`）：

- `integration.config.json` — schema `uniclaw.integrationConfig.v1`：emulator（serial/outputRoot/runNaming/keepRuns/recordBaseline）、providers 按 id 分块（local 带 visionServer 段；sensenova 带 model + intentModel）、scenarios（绑定 provider/mode/timeout）
- `IntegrationConfig.cs` — `IntegrationConfigLoader` 静态类：`Load(path?)` / `ResolveScenario(id, providerOverride, modelOverride)` / `ResolveScenarioByFile(file, scope)` + 私有 `Validate`。`KnownProviders` = {local, sensenova, claude, qwen, mock}（**无 deepseek**）；`RequiresModel` 云端三件套；visionServer 只挂 local；mode ∈ {direct, legacy, interactive}；timeoutSeconds > 0
- `IntegrationConfigTests.cs` — 14 个 `[Fact]`（schema/归属/枚举/必填/实际生效校验/env 覆盖）
- `ProviderPreflight.cs` — `Check(scenario, repoRoot)`：mock 免检；local 查 `DEEPSEEK_API_KEY` + yoloModel/labelMapping 文件存在性；claude 查 `ANTHROPIC_API_KEY`；sensenova/qwen 查各自 key 或 `~/.litellm/secrets.json`。配套 6 个单测
- `EmulatorScenarioIntegrationTests.cs` — `RunScenarioAsync`（:83）Load → ResolveScenarioByFile → **ApplyProviderEnv**（:99，私有静态方法在测试类内，非 loader）→ ProviderPreflight.Check；outputRoot/provider/model/mode 全部来自 config（:112-126）
- `UniClaw.Host.Tests.csproj`（:36-38）— config Content 拷贝 `CopyToOutputDirectory=PreserveNewest`
- 文档：`docs/testing/integration-config.md`（7 层配置全景 + schema + 决策 D-202–D-207）、`docs/testing/integration-pipeline-issues.md`（问题台账）

**已确认缺口**（配置域，台账同步跟踪）：

- P2.5：`recordBaseline`/`keepRuns` 只定义+校验，无消费方（阶段 5 基线/清理落地）
- P2.8：`UNICLAW_VISION_MODE` 双读点——[:1216](src/UniClaw.Host/Commands/HostCommands.cs#L1216) 视觉模式 single/two_stage、[:1605](src/UniClaw.Host/Commands/HostCommands.cs#L1605) run mode mode-a，`UNICLAW_RUN_MODE` 尚不存在（D-209 已定案拆分，实施待办）

验证域问题（P1.1 最终校验身份恒空 等）由台账跟踪，不在本 change。

## Goals / Non-Goals

**Goals:**

- 运行参数单点真源：`integration.config.json`（schema 版本化），加载即校验，非法配置 fail-fast 且报"缺什么+怎么设"
- 三层校验链覆盖三个错误面：文件结构 → 实际生效配置（env 覆盖后）→ 运行时前提（凭据/路径）
- L2 env 覆盖通道保持"文件 < env < 显式参数"，测试代码零硬编码运行参数
- 决策入档：D-202–D-207 + D-210 本 change 记录；D-208（deepseek）/D-209（env 拆分）与台账决策注册对齐

**Non-Goals:**

- **不改 `src/` 生产代码**——config 是测试侧装配层，经 env 通道注入；Host 的 CLI env 回退（L3 内侧）保持为直跑兜底
- P2.8 拆分 `UNICLAW_VISION_MODE`（Host 生产代码变更；D-209 已定案，实施在台账跟踪）
- P2.9 统一命名前缀（推迟，边界已文档化）
- P2.5 `recordBaseline`/`keepRuns` 消费逻辑（阶段 5 基线/清理，独立 change；本 change 只保证配置键就绪）
- 验证域问题（P1.1 最终校验身份恒空 等）——台账跟踪、独立 change（本 change 验收 6.2 依赖其解决）

## Decisions

### D-202 | 配置单点真源 + schema 版本化

- **Decision**: 运行配置收敛到 `integration.config.json`，schema `uniclaw.integrationConfig.v1`，加载即校验（fail-fast）。对齐 label-mapping.json 既有模式。
- **Alternatives**: 维持硬编码+手动 env —— 拒绝：漏设静默走错配置，实测代价 3m33s；config 不加 schema 版本 —— 拒绝：无演进边界。
- **Source**: finding:P2.1-P2.5

### D-203 | providers 按 id 分块，visionServer 只挂 local

- **Decision**: `providers` 段按 provider id 分块（每块自己的 model/实现细节）；`visionServer` 只允许挂在 `local` 下（loader 强制校验）。
- **Alternatives**: 扁平 `"visionServer": {...}` —— 拒绝：无法体现归属，视觉服务是 local 专属能力。
- **Source**: 设计评审 (2026-08-04)

### D-204 | 优先级 file < env < param

- **Decision**: 文件值是默认，`UNICLAW_INTEGRATION_PROVIDER/MODEL` env 是 CI per-run 选择器（覆盖不改文件），显式参数最高。`SetEnvIfAbsent` 是唯一注入点（手设/CI 优先）。
- **Alternatives**: 文件覆盖 env —— 拒绝：CI/本地互相污染；env 全部入文件 —— 拒绝：per-run 变化不该改共享文件。
- **Source**: 设计评审 (2026-08-04)

### D-205 | model 只对消费方必填，config 不带死值

- **Decision**: 云端（sensenova/claude/qwen）model 必填（Host 侧构造参数强制）；local/mock 不消费模型名——可省略；原占位值已删。**覆盖后校验**：env 切到云端而 model 空 → fail-fast。
- **Alternatives**: 所有 provider 强制 model —— 拒绝：local 的 `providers.local.model` 是死值（local 分支忽略 `options.Model`，text 走 `DEEPSEEK_MODEL`），误导且无消费方。
- **Source**: finding:P2.10

### D-206 | 意图推理模型入 config 管辖

- **Decision**: `providers.sensenova.intentModel`（可选，仅 sensenova 可挂）→ 装配期注入 `SENSENOVA_MODEL`（SetEnvIfAbsent）。config 是真源，env 是覆盖通道。复用 visionServer 注入模式，不动 Host。
- **Alternatives**: 维持 env 唯一管辖 —— 拒绝：同一"provider 用哪个模型"双键割裂（P2.7），config 管不到意图推理。
- **Source**: finding:P2.7

### D-207 | 三层校验链

- **Decision**: `Load()`（文件结构）→ `ResolveScenario()`（实际生效配置）→ `ProviderPreflight.Check()`（运行时前提）。均 fail-fast。
- **Alternatives**: 单层 Load 校验 —— 拒绝：env 覆盖切云端而 model 空、缺 `DEEPSEEK_API_KEY`、模型文件未下载都是运行时才暴露的错误；装配期预检让失败发生在跑 Host 之前。
- **Source**: 用户要求"按实际配置了才加载检查" (2026-08-04)

### D-210 | ApplyProviderEnv 留在测试装配层，不进 loader

- **Decision**: env 注入（`ApplyProviderEnv`/`ApplyVisionServerEnv`/`SetEnvIfAbsent`）保持为 `EmulatorScenarioIntegrationTests` 的私有静态助手；loader 是纯解析+校验，不产生副作用。
- **Rationale**: loader 职责 = 读配置、出结论；改进程 env 是测试装配动作。分离使 loader 可单测（14 用例全无 env 污染），且 env 修改集中在一个调用点（RunScenarioAsync :99），便于审计。
- **Alternatives**: 注入逻辑并入 loader —— 拒绝：loader 单测需起进程级 env，污染面扩大。

### 台账决策引用（并行工作已定案，本 change 对齐，不重复决策）

- **D-208 | deepseek = 内部 text 路由键，不进 config providers（P2.6）** — 证据：`CreateProviders` 无 `providerId=="deepseek"` 分支（用户无法 `--provider deepseek`）；deepseek 键由 local（独立 DEEPSEEK_* 凭据）与 qwen two_stage（复用 qwen 凭据）各自装配"文本推理角色"。声明已落 integration-config.md §3 providers 表。本 change 无实施项。
- **D-209 | 拆分 `UNICLAW_VISION_MODE`（视觉模式）/ 新增 `UNICLAW_RUN_MODE`（run mode）（P2.8）** — 已取证：全仓库仅 2 读点（[HostCommands.cs:1216](src/UniClaw.Host/Commands/HostCommands.cs#L1216) 视觉 / [:1605](src/UniClaw.Host/Commands/HostCommands.cs#L1605) run mode），无第三处引用，拆分无残留风险。实施 = Host 代码 1 行 + Parse 单测 + §9.1 表更新，台账跟踪、独立 change。

## Risks / Trade-offs

- **配置与 Host 侧 provider 集合单向漂移**（config `KnownProviders` vs Host 实际构造分支）→ D-208 声明边界（§3 providers 表）+ §9.3 文档锚定；若 Host 新增可选 provider，需同步 config（已在 [integration-config.md](docs/testing/integration-config.md) §6 新增 provider 步骤固化）
- **env 注入纪律依赖测试侧自觉**（优先级 file < env < param 由 SetEnvIfAbsent 保证）→ 单一注入点收敛；测试评审时检查是否绕过
- **文档与代码漂移**（integration-config.md 声称事实）→ 文档生成时基于代码核查（本 design 已复核文件行号）；台账是追踪真源，规范文档随变更更新
- **P2.8 一变量两义是现存风险**（设 `UNICLAW_VISION_MODE=single` 会把 run mode 设成非法值）→ 本 change 不实施修复；design 已记录读点语义（1216 视觉 / 1605 run mode），台账 P2.8 保持 ⏳ 待实施（D-209）
- **已完成状态可能掩盖未验证行为**（配置系统单测绿 ≠ 全链路验收）→ 验收门：配置单测 + preflight 单测全绿 + `LocateOneItem` 从 config 解析 local 复跑至 success（依赖验证域问题解决，P1.1 台账跟踪）
- **config 文件经 csproj Content 拷贝**（schema 演进需同步输出目录）→ `CopyToOutputDirectory=PreserveNewest` 已配置；改 schema 时先跑 `IntegrationConfigTests` 全量

## Migration Plan

- **现状**: 阶段 2 已在 `feature/refactor` 分支实现，无历史版本需要迁移
- **落地方式**: 纯新增文件 + 测试侧行为变更；`src/` 零改动
- **回滚**: 移除 config/loader/preflight/测试装配改动即回到硬编码状态（不推荐，P2.1 代价复现）；文档与 change 保留

## Open Questions

1. **P2.8 实施时机（台账 D-209 已定案，实施待办）**: 拆分 `UNICLAW_VISION_MODE`（视觉模式，保留）/ 新增 `UNICLAW_RUN_MODE`（run mode，[:1605](src/UniClaw.Host/Commands/HostCommands.cs#L1605) 读点）——实施属 Host 生产代码变更（1 行 + Parse 单测 + §9.1 表更新），何时实施由用户定，随独立 change 落地；本 change 只做设计对齐。
