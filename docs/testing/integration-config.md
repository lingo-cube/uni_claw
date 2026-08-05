# 集成测试配置规范 (integration.config.json)

> 生成: 2026-08-04 · 状态: ✅ 已实施
> 角色: **提案参考文档** —— 集成测试配置标准化的现状/设计/决策/验收事实基准（提案作者一份文档看全）
> 单点真源: `tests/UniClaw.Host.Tests/Integration/integration.config.json`
> 加载器: `IntegrationConfigLoader` (tests 项目) · 覆盖: P2.1-P2.5, P2.7, P2.9-P2.10
> 关联: [integration-pipeline-issues.md](integration-pipeline-issues.md)（问题与动机）· [decisions/log.md](../system/decisions/log.md)（D-202 起待并入）

---

## 1. 配置全景与职责边界

集成测试链路上的配置分散在 **7 个层面**，各自有独立的载体、归属和消费方。
本规范只管辖 **L1 (integration.config.json)** 及其与上下层的关系；其余层是既有事实，本文明确它们的边界，防止职责串位。

**判层总则**：真源只有两个（L0 做什么、L1 怎么跑）；其余全部是"派生或事实"——
L2 是覆盖通道、L3 是消费边界、L4 是注入通道、L5 是资产、L6 是环境事实。
**"配置层"一词只保留给 L0/L1/L2**；L3-L6 不是配置，各有不可变更的性质（见"性质"列）。
任何新增配置项的归属判定用 §1.1 判责三问。

| 层 | 性质 | 载体 | 控制什么 | 谁消费 | 谁写入 | 覆盖权 |
|---|---|---|---|---|---|---|
| **L0 场景定义** | 真源 · 做什么 | `scenarios/android-settings/{id}.v1.json` | **做什么**：目标、动作白名单、边界、成功标准、重置 | Host 引擎 | scenario 作者（git 资产） | 无 |
| **L1 测试运行配置** | 真源 · 怎么跑 | `integration.config.json` | **怎么跑**：设备、产物布局、provider 与模型、绑定与超时 | `IntegrationConfigLoader` | 开发者 | L2 env |
| **L2 运行期覆盖** | 覆盖通道 | `UNICLAW_INTEGRATION_*` env | **这次怎么选**：provider/model 覆盖、scope 门控、配置路径 | loader + `IntegrationFactAttribute` | CI 调用者 | 显式参数 |
| **L3 Host 调用边界** | 消费边界（非配置） | `HostCommandOptions` | 测试 → Host 的一次调用参数（**唯一出口**） | `HostCommands` | 测试代码（由 L1/L2 解析） | 无 |
| **L4 视觉服务运行参数** | 注入通道（非配置） | `UNICLAW_VISION_*` / `OMP` / `YOLO` / `OCR` env | 本地视觉服务自身参数 | `PythonVisionService`（C# 启动）+ `server.py`（Python，**双消费方**） | 测试注入（源自 L1 visionServer 段） | 手设 env |
| **L5 识别资产** | 资产（非配置） | `label-mapping.json`、YOLO 权重 | 标签映射、模型权重（内容不进配置，只进路径引用） | `server.py` | 资产库 | 无 |
| **L6 进程环境事实** | 环境事实（非配置） | `UNICLAW_REPO_ROOT` / `ADB_SERIAL` / `ADB_PATH` | 仓库根、设备定位 | Host / `AdbTestContext` | 运行环境 | 无 |

### 1.1 职责总则

1. **L0 管"做什么"，L1 管"怎么跑"** — scenario 内容（目标/边界/成功标准）永不进入 integration.config.json；config 只负责绑定"哪个 provider 用哪个模式跑它、超时多少"。理由：scenario 是可复用资产（同一份可被 mock/local/sensenova 共用），provider 是环境选择。
2. **L1 是静态真源，L2 是 per-run 开关** — 会变的（provider 覆盖、scope 门控）走 env；文件值作为默认。env 已设时文件不覆盖 env。
3. **测试代码不硬编码任何运行参数** — provider/model/outputRoot/视觉参数全部从 L1 解析、L2 覆盖后注入；`HostCommandOptions` 是唯一出口（L3）。
4. **L4 由 L1 注入，直连可手设** — 测试按 `providers.local.visionServer` 注入视觉 env；手工启动服务器的人可自行设置同名 env（优先级：手设 > 注入）。
5. **L5/L6 是环境事实，不进本配置** — 标签映射与模型内容属于资产；repo root / ADB 细节属于运行环境。
6. **判责三问（任何新增配置项的归属判定）** — 必须先回答：① 谁写（唯一 author）？② 谁消费（恰好一个 consumer，或显式声明未来消费方，如 keepRuns → 阶段5 清理器）？③ 会变吗、变由谁管（per-run 变 → L2；环境变 → L6；资产 → L5；很少变 → L1；场景内容 → L0）？答不出三问的配置项是死值候选（P2.10 教训）。

### 1.2 每个配置项的范围与职责

| 配置项 | 层 | 职责（它控制什么） | 消费方 | 缺失行为 |
|---|---|---|---|---|
| scenario 的 `target` / `boundaries` / `successCriteria` | L0 | 目标页、步数预算、成功标准 | 引擎 + verifier | 引擎启动即报错（场景资产必填） |
| `schema` | L1 | 配置格式版本 | loader | **fail-fast**：版本不匹配抛错 |
| `emulator.serial` | L1 | 设备定位；`"auto"` 委托 `AdbTestContext` 单设备解析 | 测试代码 | 默认 `"auto"` |
| `emulator.outputRoot` | L1 | 产物根（相对 repo root）；实际目录由测试按 `{root}/{scope}/{scenarioId}/{runNaming时间戳}` 拼接 | 测试代码 | **fail-fast**：必填 |
| `emulator.runNaming` | L1 | run 目录命名格式（UTC） | 测试代码 | 默认 `yyyyMMddTHHmmssZ` |
| `emulator.keepRuns` | L1 | 清理策略：每 scenario 保留最近 N 个成功 + 全部失败 | 阶段5 清理器 | 默认 5 |
| `emulator.recordBaseline` | L1 | 基线录制模式（true = 不对比、写基线） | 阶段5 基线器 | 默认 false |
| `providers.{id}.model` | L1 | 该 provider 的默认模型名（云端必填） | loader → L3 | 云端 **fail-fast**；local/mock 不消费 |
| `providers.sensenova.intentModel` | L1 | 意图推理模型（P2.7） | 测试 → `SENSENOVA_MODEL` | 可选，缺省走 Host 默认 |
| `providers.local.visionServer.*` | L1 | 本地视觉服务全部运行参数（socket/port/线程/OCR/模型/映射） | 测试 → L4 | 仅 `local` 可挂；参数本身有默认 |
| `scenarios[].provider` | L1 | 绑定：哪个 provider 跑该 scenario | loader | **fail-fast**：引用必须存在 |
| `scenarios[].mode` | L1 | Host 执行模式（direct/legacy/interactive） | loader → L3 | **fail-fast**：枚举校验 |
| `scenarios[].timeoutSeconds` | L1 | 单场景超时 | 测试代码 | **fail-fast**：必须 > 0 |
| `UNICLAW_INTEGRATION_CONFIG` | L2 | 配置文件路径覆盖（多环境并存） | loader | 默认走输出目录 |
| `UNICLAW_INTEGRATION_PROVIDER` | L2 | 本次 run 的 provider 覆盖（CI 选择器） | loader | 默认文件值 |
| `UNICLAW_INTEGRATION_MODEL` | L2 | 本次 run 的 model 覆盖 | loader | 默认 provider 段值 |
| `UNICLAW_INTEGRATION_SCOPES` | L2 | 测试门控选择器（哪些 scope 跑） | `IntegrationFactAttribute` | 默认全跳（集成测试不默认跑） |
| `UNICLAW_VISION_SOCK` / `PORT` / `OMP_THREADS` / `OCR_BACKEND` / `OCR_TEXT_SCORE` / `YOLO_MODEL` / `LABEL_MAPPING` | L4 | 视觉服务运行参数 | C# `PythonVisionService` + Python `server.py` | 注入自 L1；手设优先 |
| `UNICLAW_OCR_LANG` / `UNICLAW_OCR_PARALLEL` | L4 | OCR 语言 / 并行度 | `server.py` | **明确 out-of-scope**：用 server 默认值，不进 config |
| `UNICLAW_SETTLE_DELAY_MS` | L4 | 操作后 UI settle 等待（ms），默认 300，0 关闭 | `PageInvalidatingActionExecutor` | 模拟器 300 / 真机可降到 100-150；非 per-run |

### 1.3 职责禁区（反模式）

- ❌ scenario 内容（目标/边界/成功标准）写进 integration.config.json → L0 职责，config 只做绑定
- ❌ 视觉参数挂到非 `local` provider → visionServer 只允许挂在 `local` 下（loader 强制校验）
- ❌ 测试代码硬编码 provider/model/outputRoot → 一律从 L1 解析
- ❌ per-run 选择（scope 门控、provider 覆盖）写进文件 → 走 L2 env
- ❌ 视觉参数硬编码在测试里 → 从 `providers.local.visionServer` 注入
- ❌ config 中相对路径不解析 → repo-root 相对路径统一在注入点（`ApplyVisionServerEnv`）解析为绝对
- ❌ 配置项答不出判责三问（谁写/谁消费/变由谁管）仍入库 → 死值（P2.10 教训：`providers.local.model` 无消费方，误导配置者）

## 2. 文件位置与优先级

| 项 | 值 |
|---|---|
| 默认路径 | `tests/UniClaw.Host.Tests/Integration/integration.config.json`（csproj Content 拷贝到测试输出） |
| 路径覆盖 | `UNICLAW_INTEGRATION_CONFIG=<path>` |

**优先级：配置文件 < L2 env 覆盖 < 显式参数**

CI 用 env 覆盖、本地默认读文件；env 已设时配置文件不覆盖 env。

## 3. Schema

```json
{
  "schema": "uniclaw.integrationConfig.v1",
  "emulator": {
    "serial": "auto",
    "outputRoot": "artifacts/runs/integration",
    "runNaming": "yyyyMMddTHHmmssZ",
    "keepRuns": 5,
    "recordBaseline": false
  },
  "providers": {
    "local": {
      "visionServer": {
        "socket": "/tmp/uniclaw-vision.sock",
        "ompThreads": 4,
        "ocrBackend": "rapidocr",
        "ocrTextScore": 0.5,
        "yoloModel": "artifacts/local-vision/models/android_ui_detection_yolov8/best.pt",
        "labelMapping": "tools/local_vision/label-mapping.json"
      }
    },
    "sensenova": { "model": "sensenova-6.7-flash-lite", "intentModel": "deepseek-v4-flash" },
    "claude": { "model": "claude-sonnet-5" },
    "qwen": { "model": "qwen-plus" },
    "mock": {}
  },
  "scenarios": [
    {
      "id": "locate-one-item",
      "file": "locate-one-item.v1.json",
      "scope": "scenario-locate",
      "provider": "local",
      "mode": "direct",
      "timeoutSeconds": 180
    }
  ]
}
```

| 段 | 字段 | 约束 |
|---|---|---|
| `schema` | — | 必须等于 `uniclaw.integrationConfig.v1` |
| `emulator` | `serial` | `"auto"` = 单在线设备自动解析；或固定 serial |
| | `outputRoot` | 相对 repo root |
| | `runNaming` | UTC 命名格式 |
| | `keepRuns` | ≥ 0 |
| | `recordBaseline` | 布尔 |
| `providers` | 键 = provider id | 必须 ∈ {local, sensenova, claude, qwen, mock}（与 Host 侧对齐）。**deepseek 不在其列**——它是 Host 内部 text 路由键（D-208）：由 local 分支（独立 DEEPSEEK_* 凭据）与 qwen two_stage 分支（复用 qwen 凭据）各自装配"文本推理角色"，无 `--provider deepseek` 入口，不是用户可选 provider |
| | `model` | **仅云端必填**（sensenova/claude/qwen 的模型是构造参数，Host 侧强制）；local/mock 不消费（P2.10），可省略 |
| | `intentModel` | **仅 sensenova**（P2.7）：意图推理模型（CreateIntentExtractor），可选；注入 `SENSENOVA_MODEL` |
| | `visionServer` | **只允许挂在 `local` 下**；`ocrBackend` ∈ {rapidocr, paddleocr} |
| `scenarios` | `id` / `file` / `scope` | 必填 |
| | `provider` | 必须存在于 `providers` 段 |
| | `mode` | ∈ {direct, legacy, interactive} |
| | `timeoutSeconds` | > 0 |

## 4. provider env 注入映射 (L1 → L4)

测试运行时按 provider 从配置注入环境变量（`ApplyProviderEnv`，仅当 env 未设时，保持手设/CI 覆盖优先）：

### 4.1 local → visionServer

`providers.local.visionServer` 注入以下变量：

| 配置字段 | 环境变量 | 说明 |
|---|---|---|
| `socket` | `UNICLAW_VISION_SOCK` | UDS 路径 |
| `port` | `UNICLAW_VISION_PORT` | TCP 端口（覆盖 socket） |
| `ompThreads` | `UNICLAW_OMP_THREADS` | OpenMP 线程数 |
| `ocrBackend` | `UNICLAW_OCR_BACKEND` | rapidocr / paddleocr |
| `ocrTextScore` | `UNICLAW_OCR_TEXT_SCORE` | 识别置信度阈值 |
| `yoloModel` | `UNICLAW_YOLO_MODEL` | 相对路径解析为 repo-root 绝对路径 |
| `labelMapping` | `UNICLAW_LABEL_MAPPING` | 同上 |

### 4.2 sensenova → intentModel

| 配置字段 | 环境变量 | 说明 |
|---|---|---|
| `intentModel` | `SENSENOVA_MODEL` | 意图推理模型（Host `CreateIntentExtractor` 读）；缺省时 Host 回落 `SENSENOVA_MODEL ?? "deepseek-v4-flash"` |

## 5. 加载与校验

- `IntegrationConfigLoader.Load(path?)`：解析 + 校验（结构层），失败抛 `InvalidOperationException`（fail-fast）
- `ResolveScenario`/`ResolveScenarioByFile`：按文件名定位 scenario（回退 scope 匹配），返回生效配置（含 provider 解析 + env 覆盖）——**同时按实际生效配置校验**：覆盖后的 provider/model 也要满足必填规则（如切到云端 provider 而 model 为空 → fail-fast），不只看文件原样
- `ProviderPreflight.Check(scenario, repoRoot)`：运行时前提预检（可用性层）——每个 provider 检查自己的配置（凭据 env / secrets 文件 / 本地路径），测试装配期调用

**检查链**：Load（文件结构）→ ResolveScenario（实际生效配置）→ ProviderPreflight（运行时前提）——三层各自覆盖不同的错误面。
- 结构校验规则：schema 版本 / emulator 必填段 / provider 已知集 / **云端 model 必填（local/mock 不强制，P2.10）** / visionServer 归属与枚举 / scenario 引用存在性 / mode 枚举 / timeout 正数
- 可用性预检规则（[ProviderPreflight.cs](../../tests/UniClaw.Host.Tests/Integration/ProviderPreflight.cs)）：

| provider | 预检内容 | 缺失时 |
|---|---|---|
| mock | 无（确定性） | — |
| local | `DEEPSEEK_API_KEY`（text 路由）+ visionServer 段存在 + `yoloModel`/`labelMapping` 文件存在（repo-root 解析） | fail-fast，提示设 key / 下载资产 |
| claude | `ANTHROPIC_API_KEY` | fail-fast |
| sensenova | `SENSENOVA_API_KEY` 或 `~/.litellm/secrets.json` | fail-fast |
| qwen | `QWEN_API_KEY` 或 `~/.litellm/secrets.json` | fail-fast |

## 6. 新增一个 scenario

1. 在 `scenarios/` 写 scenario JSON（`{id}.v1.json`）— 只写"做什么"（L0）
2. 在配置 `scenarios` 段加一项：`id`/`file`/`scope`/`provider`/`mode` — 只写"怎么跑"（L1）
3. 若用新 provider：先在 `providers` 段登记（含 model；local 需 visionServer）
4. 在 `EmulatorScenarioIntegrationTests` 加测试方法（`[IntegrationFact(scope)]`）
5. 跑 `dotnet test --filter FullyQualifiedName~IntegrationConfigTests` 验证配置合法

## 7. 运行示例

```bash
# 本地默认（配置文件指定 provider=local）
UNICLAW_INTEGRATION_SCOPES=scenario-locate dotnet test --filter LocateOneItem

# CI 覆盖 provider
UNICLAW_INTEGRATION_SCOPES=scenario-locate \
UNICLAW_INTEGRATION_PROVIDER=sensenova dotnet test --filter LocateOneItem
```

## 8. 推荐配置：当前测试

> 实测生效值（2026-08-04）。来源：`integration.config.json` 文件值 + loader 解析 + `ApplyVisionServerEnv` 注入；env 未设时生效，CI/手设优先。

### 8.1 LocateOneItem（scope=scenario-locate · local 全链路）

| 配置项 | 生效值 | 来源 |
|---|---|---|
| provider | `local` | `scenarios[].provider` |
| model | 无（local 不消费模型名） | P2.10 已删死值 |

> local 真实生效的模型键 = `visionServer.*`（视觉，本地 YOLO+OCR）+ `DEEPSEEK_MODEL`（text 决策）。装配期 `ProviderPreflight` 预检 `DEEPSEEK_API_KEY` + 模型/映射文件存在性。
| mode | `direct` | `scenarios[].mode` |
| timeoutSeconds | 180 | `scenarios[].timeoutSeconds` |
| serial | `auto`（单在线设备解析） | `emulator.serial` |
| 产物目录 | `artifacts/runs/integration/scenario-locate/locate-one-item/{yyyyMMddTHHmmssZ}/` | emulator 段 |
| UNICLAW_VISION_SOCK | `/tmp/uniclaw-vision.sock` | visionServer.socket |
| UNICLAW_OMP_THREADS | 4 | visionServer.ompThreads |
| UNICLAW_OCR_BACKEND | `rapidocr` | visionServer.ocrBackend |
| UNICLAW_OCR_TEXT_SCORE | 0.5 | visionServer.ocrTextScore |
| UNICLAW_YOLO_MODEL | `{repo}/artifacts/local-vision/models/android_ui_detection_yolov8/best.pt` | visionServer.yoloModel（解析绝对路径） |
| UNICLAW_LABEL_MAPPING | `{repo}/tools/local_vision/label-mapping.json` | visionServer.labelMapping（同上） |
| 外部依赖 | Python 3.11 + `.venv-local-vision`（视觉服务自动拉起）；text 路由走 deepseek → 需 `DEEPSEEK_API_KEY` | 运行环境 |

```bash
UNICLAW_INTEGRATION_SCOPES=scenario-locate dotnet test --filter LocateOneItem
```

### 8.2 EnumerateSettings（scope=scenario-enumerate · mock）

| 配置项 | 生效值 | 来源 |
|---|---|---|
| provider | `mock` | `scenarios[].provider` |
| model | `deterministic-ui` | `providers.mock.model` |
| mode | `direct` | `scenarios[].mode` |
| timeoutSeconds | 180（默认） | 缺省值 |
| serial | `auto` | `emulator.serial` |
| 产物目录 | `artifacts/runs/integration/scenario-enumerate/enumerate-settings-safely/{yyyyMMddTHHmmssZ}/` | emulator 段 |
| vision env | **无注入**（mock 无 visionServer 段） | — |
| 外部依赖 | 无（纯确定性，不碰云端/视觉） | — |

```bash
UNICLAW_INTEGRATION_SCOPES=scenario-enumerate dotnet test --filter EnumerateSettings
```

### 8.3 运行环境（两个测试通用）

| env | 值 | 说明 |
|---|---|---|
| `UNICLAW_INTEGRATION_SCOPES` | `scenario-locate,scenario-enumerate` | 门控选择器 |
| `UNICLAW_REPO_ROOT` | 测试锚定为 repo root | 路径解析基座 |
| `UNICLAW_ADB_SERIAL` | 可选 | 覆盖 `auto`（多设备时必设） |
| `UNICLAW_INTEGRATION_PROVIDER`/`MODEL` | 可选 | 覆盖 scenario 绑定（如切 sensenova 跑 locate） |
| `DEEPSEEK_API_KEY` | local 模式的 text 路由 | 云端密钥，走 secrets 管理 |

## 9. Host 侧 env 全景（L3 内侧，Host 契约锚点）

> **本节是 Host 内部契约锚点，不是配置规范** —— Host 自身有一层 env 回退（CLI 参数默认值，直跑 `uniclaw` 命令时生效）。
> 只读参考，**不进 integration.config.json**；测试链路配置一律以 L1/L2 为准，本节不参与任何装配。
> 用途：决策与台账的锚点（问题见 [integration-pipeline-issues.md](integration-pipeline-issues.md) P2.6-P2.9），不是配置项登记表。

### 9.1 CLI 参数 env 回退（[HostCommands.cs:1596-1609](src/UniClaw.Host/Commands/HostCommands.cs#L1596-L1609)）

| env | 用途 | 默认 |
|---|---|---|
| `UNICLAW_OUTPUT` | run 产物根 | `artifacts/runs/commands` |
| `UNICLAW_PROVIDER` | 默认 provider | `claude` |
| `UNICLAW_MODEL` | 默认 model | 无 |
| `UNICLAW_VISION_MODE` | ⚠️ run mode（与 8.2 同变量两义，见 P2.8） | `mode-a` |
| `UNICLAW_RUN_PURPOSE` | run 目的标签 | 无 |
| `UNICLAW_TASK_ID` | 任务 id | 无 |

### 9.2 provider 专属（同一文件内）

| env | 用途 | 默认 | 备注 |
|---|---|---|---|
| `ANTHROPIC_API_KEY` | claude | — | secrets |
| `SENSENOVA_BASE_URL` | sensenova 端点 | `https://token.sensenova.cn` | — |
| `SENSENOVA_MODEL` | sensenova 模型 | ⚠️ `deepseek-v4-flash` | 疑复制粘贴残留（P2.7） |
| `QWEN_BASE_URL` / `QWEN_MODEL` | qwen 端点/模型 | `qwen3.7-plus` | — |
| `QWEN_API_KEY` | qwen | — | 或 `~/.litellm/secrets.json` |
| `DEEPSEEK_BASE_URL` / `DEEPSEEK_MODEL` | deepseek 端点/模型 | `https://api.deepseek.com` / `deepseek-v4-flash-0731` | 未入 config providers（P2.6） |
| `DEEPSEEK_API_KEY` | deepseek | — | secrets |
| `UNICLAW_ADB_BACKEND` | ADB 后端选择 | 无 | — |
| `UNICLAW_UVICORN_PATH` | 视觉服务 uvicorn 路径（[PythonVisionService.cs](src/UniClaw.Device/PythonVisionService.cs)） | 无 | — |
| `ANDROID_HOME` / `ANDROID_SDK_ROOT` | ADB 定位 | 无 | L6 环境事实 |

### 9.3 与 L2 的边界

| env | 层 | 职责 |
|---|---|---|
| `UNICLAW_PROVIDER` / `UNICLAW_MODEL` | L3 内侧（Host CLI 回退） | 直跑 Host CLI 时生效 |
| `UNICLAW_INTEGRATION_PROVIDER` / `UNICLAW_INTEGRATION_MODEL` | L2（测试 config 覆盖） | 测试跑场景时生效；经 loader 解析后落进 `HostCommandOptions`，**不经过** CLI env 回退 |

两条命名空间易混（P2.9）：测试链路优先走 L2 → L3 显式参数；CLI env 回退只在直跑 `uniclaw run` 时兜底。

**长期方向（可选，不排期）**：统一前缀 `UNICLAW_CLI_*` 区分 L3 内侧 env 与 L2 `UNICLAW_INTEGRATION_*`（需同步测试/Host 调用点，收益低）。命名空间即边界：**一个前缀 = 一层**。

## 10. 背景与动机（提案用）

**问题**（详见 [integration-pipeline-issues.md](integration-pipeline-issues.md) 域 2，P2.1-P2.5）：

| 现象 | 后果 |
|---|---|
| provider/model 硬编码在测试代码（locate→sensenova、enumerate→mock） | 换 provider 要改代码或手设 `UNICLAW_INTEGRATION_PROVIDER`；漏设撞云端 Sensenova 空响应失败（实测 3m33s） |
| outputRoot 命名/位置硬编码（`{scope}/{yyyyMMdd-HHmmss}` 无时区无 scenario 级目录） | 产物目录不可预测，跨 scenario 无法整理 |
| scope 门控只走 env | CI/本地差异靠记忆，无配置真源 |
| 视觉服务 env 靠手动 export（6 个变量） | 跑一次要 export 5-6 个变量，漏设静默走错后端/模型 |
| recordBaseline/keepRuns 等计划 knobs 无落点 | 基线/清理流程不可用 |

**目标**：一份 schema 版本化配置文件 + fail-fast 校验 + env 覆盖通道，收敛散落配置。

**非目标**：不改 Host/Device/Core 生产代码 —— config 是测试侧装配层，通过 env 通道注入（Host 的 CLI env 回退保持为直跑兜底）。

## 11. 决策记录（提案用，D-202 起待并入 decisions/log.md）

### D-202 | 配置单点真源 + schema 版本化

- **Decision**: 集成测试运行配置收敛到 `integration.config.json`，schema `uniclaw.integrationConfig.v1`，加载即校验（fail-fast）。对齐 label-mapping.json 既有模式（schema 版本 + 构造期校验）。
- **Rationale**: provider/model/outputRoot/视觉参数散落代码与手动 env，漏设即静默走错配置（P2.1/P2.4 实测代价 3m33s）。
- **Source**: finding:P2.1-P2.5 · **Status**: Fixed

### D-203 | providers 按 id 分块，visionServer 只挂 local

- **Decision**: `providers` 段按 provider id 分块（每块自己的 model + 实现细节）；`visionServer` 只允许挂在 `local` 下（loader 强制校验）。
- **Rationale**: 扁平 `"visionServer": {...}` 无法体现它属于哪个 provider（设计反馈）；vision 服务是 local 专属能力，挂在其他 provider 上语义错误。
- **Source**: 设计评审 (2026-08-04) · **Status**: Fixed

### D-204 | 优先级 file < env < param

- **Decision**: 配置文件值为默认，`UNICLAW_INTEGRATION_PROVIDER/MODEL` env 是 CI 每 run 选择器（覆盖不改文件），显式参数最高。env 已设时配置文件不覆盖 env。
- **Rationale**: 文件是静态真源（多人共享），每 run 变化走 env，避免 CI/本地互相污染。
- **Source**: 设计评审 (2026-08-04) · **Status**: Fixed

### D-205 | model 只对消费方必填，config 不带死值

- **Decision**: 云端（sensenova/claude/qwen）model 必填（Host 侧构造参数强制）；local/mock 不消费模型名 —— 可省略，原占位值已删。config 不携带无消费方的字段。
- **Rationale**: `providers.local.model="sensenova-6.7-flash-lite"` 是死值（local 分支忽略 options.Model，text 走 DEEPSEEK_MODEL），误导且无意义（P2.10）。
- **Source**: finding:P2.10 · **Status**: Fixed

### D-206 | 意图推理模型入 config 管辖

- **Decision**: `providers.sensenova.intentModel`（可选，仅 sensenova 可挂）→ 测试装配期注入 `SENSENOVA_MODEL`（SetEnvIfAbsent，手设优先）。config 是真源，env 仍是覆盖通道。
- **Rationale**: 意图推理模型（CreateIntentExtractor，`deepseek-v4-flash` 经 sensenova 端点）此前只由 env 管辖，config 管不到 —— 同一"provider 用哪个模型"双键割裂（P2.7）。复用 visionServer 的 config→env 注入模式，不动 Host。
- **Source**: finding:P2.7 · **Status**: Fixed

### D-207 | 三层校验链

- **Decision**: 配置检查分三层，各覆盖不同错误面：① `Load()` 文件结构（schema/归属/枚举）→ ② `ResolveScenario()` **实际生效配置**（env/参数覆盖后也要满足必填）→ ③ `ProviderPreflight.Check()` 运行时前提（凭据 env / secrets 文件 / 本地路径存在性）。均 fail-fast。
- **Rationale**: 文件校验通过 ≠ 运行可用 —— env 覆盖切到云端 provider 而 model 为空、缺 DEEPSEEK_API_KEY、模型文件未下载，都是运行时才暴露的错误；装配期预检让失败发生在跑 Host 之前。
- **Source**: 用户要求"按实际配置了才加载检查" (2026-08-04) · **Status**: Fixed

## 12. 改动清单（提案用）

| 类型 | 文件 | 内容 |
|---|---|---|
| 新增 | `tests/UniClaw.Host.Tests/Integration/integration.config.json` | 配置单点真源（emulator/providers/scenarios） |
| 新增 | `tests/UniClaw.Host.Tests/Integration/IntegrationConfig.cs` | loader + DTO + 结构校验 + 实际生效校验 + env 覆盖 |
| 新增 | `tests/UniClaw.Host.Tests/Integration/IntegrationConfigTests.cs` | loader 单测（14 个） |
| 新增 | `tests/UniClaw.Host.Tests/Integration/ProviderPreflight.cs` | 各 provider 运行时前提预检 |
| 新增 | `tests/UniClaw.Host.Tests/Integration/ProviderPreflightTests.cs` | 预检单测（6 个） |
| 新增 | `docs/testing/integration-config.md` | 本文档 |
| 新增 | `docs/testing/integration-pipeline-issues.md` | 问题清单（P2.x 状态与细则） |
| 修改 | `tests/UniClaw.Host.Tests/Integration/EmulatorScenarioIntegrationTests.cs` | RunScenarioAsync 从 config 解析 provider/model/outputRoot + ApplyProviderEnv 注入 + preflight 接入 |
| 修改 | `tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj` | Content 拷贝 integration.config.json 到测试输出 |

**未改**：`src/` 生产代码（Host/Device/Core）—— 配置经 env 通道注入，无运行架构改动。

## 13. 验收与验证（提案用）

| 验收点 | 验证方式 | 状态 |
|---|---|---|
| 配置加载/校验正确 | `IntegrationConfigTests` 14 个用例（schema/归属/枚举/必填/实际生效校验/env 覆盖） | ✅ 通过 |
| 各 provider 运行时前提预检 | `ProviderPreflightTests` 6 个用例（凭据/路径/就绪） | ✅ 通过 |
| 测试代码不再硬编码运行参数 | RunScenarioAsync 全部从 config 解析 | ✅ 已改 |
| 视觉 env 免手动 export | ApplyProviderEnv 按 config 注入（SetEnvIfAbsent） | ✅ 已改 |
| 集成链路可用 | LocateOneItem 从 config 解析 provider=local 复跑至 success | 🚧 阶段 1 进行中 |
| 非法配置 fail-fast | 任一校验层抛 `InvalidOperationException` 带"缺什么+怎么设" | ✅ 单测覆盖 |
