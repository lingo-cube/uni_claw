# UniClaw 本地实现差距收敛 PRD

> 状态：Draft / 待评审  
> 日期：2026-07-29  
> 范围：`feature/refactor` 当前本地工作区  
> 目标：把已完成的 Core、UniBrain、Provider、Device 纵向切片收敛为可运行、可验证、可交付的 Android 真机闭环  
> 流程说明：本次仅做现状审计与 PRD 整理，未走 OpenSpec apply，也未修改业务代码

---

## 0. 结论

当前项目不是“核心能力未实现”，而是“组件已齐、产品闭环未闭”：

- Domain、Graph、StateMachine、Traversal、Simulation、Observability 已形成较完整的类型安全内核。
- UniBrain 的页面视觉分析、文本理解、下一步决策各已有至少一条垂直切片。
- Claude、DeepSeek、ADB 截图、ADB 操作、ADB 屏幕状态均已有生产项目承载。
- 非集成测试基线为 **930/930 通过**。

但当前仍不能作为一个可靠的真机产品运行，主要阻塞点是：

1. 没有可执行 Host / CLI 和 composition root，无法从配置组装并启动完整链路。
2. `EntryPolicyExecutor` 只返回成功结果，不执行真实 deeplink、冷启动或页面等待。
3. Core facade 中 5 个能力方法仍显式抛 `NotImplementedException`，Advisor/Text 也未接入生产遍历主链。
4. `AdbScreenStateProvider` 对真实滚动状态的判断依据不可靠，并会吞掉所有错误。
5. 没有“启动 App → 截图分析 → 执行动作 → 页面复验 → 滚动 → 完成 → trace 落盘”的自动化真机验收。
6. 安全筛选未实现、未接线，不满足真实设备上的安全执行条件。

因此下一阶段不应继续横向扩充模型类型，而应优先完成一个**受控、可观测、可重复的 plan-driven Android MVP**，再接自然语言与 AI Advisor。

---

## 1. 审计基线

### 1.1 审计对象

本 PRD 对照以下真相源：

- 当前 C# solution：`src/UniClaw.Core.sln`
- 当前 Core / Provider / Device 实现
- `docs/prd/2026-07-22-unibrain-prd.md`
- `docs/prd/2026-07-22-prompt-template-engine.md`
- `docs/refactor/2026-07-15-python-csharp-gap-triage.md`
- `docs/system/decisions/log.md`
- 已归档 OpenSpec changes

当前 `openspec/changes/` 下无活跃 change，全部位于 `archive/`。

### 1.2 验证结果

| 检查 | 结果 | 说明 |
|---|---:|---|
| C# semantic diagnostics — error | 0 | 工作区无编译错误 |
| C# semantic diagnostics — warning | 484 | 384 条 CS1591；另含 nullable、生成代码、XML 注释和 analyzer 告警 |
| 非集成测试 | 930/930 通过 | `Category!=Integration`，现有 Debug 构建产物 |
| 真模型集成测试 | 默认 Skip | 需要手工去掉 `Skip`，当前不进入 CI |
| Device 项目自动化测试 | 0 | 测试项目未引用 `UniClaw.Device` |
| 可执行 Host 项目 | 0 | solution 中项目均为 class library / test / source generator |

完整 `dotnet build` 在本次审计中因本地 C# 工作区进程占用而长时间无输出，已取消；因此本 PRD 不声明“当前完整构建命令已通过”。语义诊断提供了 0 error 的源码级证据，测试结果提供了现有构建产物的运行级证据。

### 1.3 本地工作树边界

审计时工作树已有用户未提交改动，包括：

- MCP / 开发环境文档调整
- Android emulator 文档与 doctor 脚本
- `RealVisionIntegrationTests.cs` 的 OpenAI-compatible vision 试验实现

其中测试内 inline provider 视为**本地验证 spike**，不能视为已交付的生产 Provider。

---

## 2. 当前能力地图

| 能力域 | 当前水位 | 判断 |
|---|---|---|
| Domain | 不可变模型、fail-fast、JSON 约定已成型 | 可用 |
| Graph | PlanCompiler、模板实例化、动态匹配、Plan JSON 已具备 | 可用 |
| StateMachine | 双 FSM、Popup/Error/Container handler 已接线 | 可用 |
| Traversal | 主循环、滚动、导航子页、pause/resume、hook 已具备 | 可用 |
| Simulation | Stateful / scroll mock、ExpectedBehavior、基线报告完整 | 成熟 |
| Observability | 内存 + JSONL 文件存储、trace/span/source generator 已具备 | 可用，缺 Host 组装 |
| UniBrain facade | 3 子接口和组合容器已落地 | 结构完成 |
| PageAnalyzer | 截图 → vision → JSON → `PageAnalysis` 已实现 | 主路径可用，2 方法未实现 |
| TraversalAdvisor | `DecideNextActionAsync` 已实现 | 其余 3 方法未实现，且无生产消费者 |
| TextUnderstanding | `UnderstandTextAsync` 已实现 | 无生产消费者 |
| Model routing | router + observing decorator 已实现 | 无 composition root |
| Claude Provider | text + vision 传输已实现 | 缺生产实测和韧性 |
| DeepSeek Provider | text 已实现 | vision / multimodal 未实现 |
| Device | screenshot / action / scroll-state 三实现已存在 | 未测试，可靠性不足 |
| Host / CLI | 不存在 | 产品阻塞 |

---

## 3. Gap 清单

### 3.1 P0 — 真机闭环阻塞

#### GAP-P0-01：缺少可执行 Host 与 composition root

**现状**

- solution 包含 Core、SourceGen、ClaudeProvider、DeepSeekProvider、Device、Tests。
- 没有 `OutputType=Exe` 项目，没有 `Program.cs`、CLI 命令、配置加载或生命周期托管。
- `IModelRouter` 的生产引用只有其实现自身；各 provider、prompt、trace、device、engine 需要调用方手工拼装。

**影响**

- 用户无法用一个稳定入口运行 plan。
- Provider 路由、trace decorator、Device serial、超时、输出目录等约定无法被统一执行。
- 集成方式只能散落在测试或临时代码中。

**目标**

新增一个薄 Host，负责配置、依赖组装、运行、退出码和产物输出；Core 保持纯 class library。

---

#### GAP-P0-02：入口策略是假成功，不执行设备操作

**现状**

`EntryPolicyExecutor.ExecuteStrategy` 当前行为：

- `DirectDeeplink`：直接返回 `Sent deeplink...`
- `ColdLaunch`：直接返回 `Cold launched...`
- `BindCurrentScreen`：直接返回成功

它不持有 `IActionExecutor`、`IPageAnalyzer` 或 Device 接口，不执行命令，也不等待/验证页面。

**影响**

- engine 可以在错误 App、错误页面上开始遍历。
- trace 会记录入口成功，但设备状态并未改变，属于误报。

**目标**

- `BindCurrentScreen` 必须至少做一次页面分析并验证可用性。
- `ColdLaunch` 必须真实启动指定 package/activity 并等待首屏稳定。
- `DirectDeeplink` 必须真实发送 URI，并验证目标页面。
- primary 失败后才进入 fallback；所有失败必须带可诊断原因。

---

#### GAP-P0-03：滚动状态实现无法可靠支撑真机遍历

**现状**

`AdbScreenStateProvider`：

- 每次 `HasScroll`、`GetScrollProgress`、`IsEndOfList` 都同步阻塞执行一次 `uiautomator dump`。
- 依赖 XML 中 `scrollY` / `scrollYMax` 属性；属性缺失时 `maxScrollY=0`，直接判定 `IsEnd=true`。
- 任意异常都被吞掉并回落为 `(HasScroll=false, Progress=0, IsEnd=true)`。
- 无 CancellationToken、命令超时、设备 serial 或错误诊断。
- `GetScrollSwipeConfig()` 固定返回 null。

**影响**

- 可滚动页面可能被误判为已到底，真机遍历提前结束。
- ADB 断连与“页面确实不可滚动”不可区分。
- 单个 branch 判断可能重复执行多次昂贵 dump。

**目标**

滚动判定改为一次采样、多字段复用，并具备明确的 unavailable/error 状态。MVP 推荐：

1. `PageAnalysis.HasScroll` 作为当前屏幕候选信息；
2. 实际 swipe 后以 seen-set 差分判断是否揭示新元素；
3. accessibility tree 只用于补充“存在 scrollable node”，不依赖未保证存在的绝对进度属性；
4. ADB 失败不得伪装成“列表到底”。

---

#### GAP-P0-04：没有完整真机 E2E 验收

**现状**

- `RealVisionIntegrationTests` 只覆盖“预存截图 → 真模型 → PageAnalysis”，默认 Skip。
- 测试不经过 `UniClaw.Device`、`TraversalEngine`、入口策略、ADB action 或 JSONL trace。
- 本地 OpenAI-compatible provider 仍在测试文件内。
- `UniClaw.Core.Tests.csproj` 未引用 `UniClaw.Device`。

**影响**

组件分别可用不等于系统可用；以下跨边界风险均未被自动发现：

- 设备选择与 ADB 断连
- 坐标归一化与实际点击
- 首屏等待与页面稳定
- Provider 返回格式漂移
- 滚动终止
- trace 会话是否完整关闭

**目标**

建立 emulator-first 的可重复 smoke E2E，并保留 real-device 手工验证入口。

---

#### GAP-P0-05：真实设备安全执行未闭环

**现状**

- `TraversalAdvisor.ScreenSafetyAsync` 显式 `NotImplementedException`。
- 该接口方法没有生产调用方。
- engine 的动态节点执行不经过统一安全 gate。

**影响**

当遍历真实设备时，删除、支付、授权、提交等高风险控件没有系统级阻断。

**目标**

MVP 默认使用 allowlist：

- 限定 App/package；
- 限定允许的 operation；
- 对危险文本/类型执行 deny-by-default；
- 未知动作不执行；
- 每次拒绝写入 trace。

AI safety 可作为补充判断，不能成为唯一安全边界。

---

### 3.2 P1 — 能力完整性与可靠性

#### GAP-P1-01：UniBrain 仍是部分实现

以下生产方法仍显式抛 `NotImplementedException`：

| 类型 | 方法 |
|---|---|
| `PageAnalyzer` | `FindAppEntryAsync` |
| `PageAnalyzer` | `VerifyPageTypeAsync` |
| `TraversalAdvisor` | `InferContainerTypeAsync` |
| `TraversalAdvisor` | `HandleExceptionAsync` |
| `TraversalAdvisor` | `ScreenSafetyAsync` |

这些边界由 D-143 / D-144 明确记录为垂直切片 defer，不是偶发遗漏；本 PRD 将其重新纳入产品 backlog。

此外：

- `DecideNextActionAsync` 只有测试调用方，没有 engine 生产调用方。
- `UnderstandTextAsync` 只有测试调用方，没有“自然语言 → IntentSlots / PlanCompiler”主链。
- facade 目前对运行时而言主要只提供 `PageAnalyzer`。

**要求**

- P0 plan-driven MVP 不强制接入 Advisor/Text。
- P1 goal-driven 模式必须定义清晰接缝：`goal → text understanding → plan/advice → engine`。
- 未实现 capability 在 Host 启动时必须提前拒绝配置，不能运行中才抛 NIE。

---

#### GAP-P1-02：Provider 韧性与能力声明不足

**Claude Provider**

- 已支持 text + vision。
- vision 响应仍写入 `ModelResponse.Mode="text"`，观测语义失真。
- 当前真实 vision spike 绕过 Claude Provider，尚未证明生产实现可与目标 endpoint 稳定互通。
- 无 retry/backoff、429/5xx 策略、并发闸门或熔断。

**DeepSeek Provider**

- text 路径已实现。
- vision / multimodal 显式抛 `NotImplementedException`。

**通用缺口**

- Provider 没有结构化的 capability 声明，错误路由只能在调用时失败。
- 没有统一的秘密来源、redaction 规则和配置诊断。
- 没有生产级 OpenAI-compatible multimodal provider；当前仅存在测试内 spike。

**要求**

- Host 组装期校验 capability 与 provider mode。
- token、耗时、provider、model、mode、success/error 全量入 trace。
- 429/5xx/timeout 使用有界重试；用户取消必须立即传播。
- API key 不出现在异常、trace、控制台和测试产物中。

---

#### GAP-P1-03：ADB 基础设施重复且不可诊断

三个 Device 类分别直接创建 `Process`，缺少统一的：

- device serial 选择；
- command timeout；
- stdout/stderr/exit code 结果模型；
- cancellation 后进程回收；
- structured argument escaping；
- 可替换 fake runner；
- 命令级 trace。

`AdbActionExecutor.InputTextAsync` 仍依赖字符串拼接和有限字符替换，复杂文本可靠性不足。

**要求**

在不让 Core 依赖 Device 的前提下，Device 层应统一 ADB command runner。是否新增 `IAdbCommandRunner` 属实现设计决策，进入 OpenSpec propose 时确认。

---

#### GAP-P1-04：Observability 已有能力未形成默认交付物

**现状**

- JSONL `FileTraceStorage` 已实现。
- `ObservingModelProvider` 已实现。
- Host 不存在，因此没有默认 trace 目录、run manifest、结束状态或失败归档。

**要求**

每次 Host 运行必须产生：

- `session.json`
- `trace.jsonl`
- 最终 `TraversalResult`
- 脱敏后的运行配置摘要
- 失败时的最后页面截图或截图引用（可配置）

---

#### GAP-P1-05：质量门禁与文档漂移

**现状**

- 930 个非集成测试通过。
- 工作区有 484 个 warning：
  - CS1591：384
  - CS8669：28（source generator 生成代码 nullable context）
  - CS1572：34
  - 其余含 nullable、unused、xUnit analyzer、Roslyn analyzer
- `docs/system/README.md` 仍描述 Phase 2 handler stub / Phase 3 ADB 未开始，与当前实现不一致。
- AGENTS 项目概览中的“840 测试、0 功能性 warning”已过期。

**要求**

- 先建立 warning baseline，再按类别归零或显式 suppress；不得用全局 `NoWarn` 掩盖 nullable/analyzer 风险。
- 业务相关 warning（nullable、生成代码、analyzer）优先于 XML 文档完整度。
- 本 PRD实施完成后同步 system layer docs、README、AGENTS 测试基线。

---

### 3.3 P2 — 后续产品化

以下不阻塞 plan-driven MVP：

- Prompt Markdown/YAML 文件加载、版本选择、hot reload；
- AI response cache、token budget、debounce；
- 多 provider 动态切换和自动降级；
- iOS / 非 ADB 平台；
- Web dashboard / Prometheus；
- 自然语言自由规划的全自治模式；
- DeepSeek vision；
- 高级截图压缩、局部裁剪和视觉缓存；
- 长时运行恢复与 context replay。

---

## 4. 产品目标

### 4.1 P0 目标：Plan-driven Android MVP

用户可以提供一个合法 `TraversalPlan` 和运行配置，通过单一命令在指定 Android emulator/device 上运行：

```text
load config
  → validate device/provider/plan
  → enter or bind target app
  → capture and analyze screen
  → traverse and execute allowed actions
  → handle popup/navigation/scroll
  → terminate deterministically
  → persist result and trace
```

### 4.2 P1 目标：Goal-driven assisted mode

用户提供自然语言目标，由 TextUnderstanding / Advisor 辅助生成或修正 plan，但：

- Core traversal 仍是最终执行权威；
- 所有动作仍经过 deterministic safety gate；
- AI 失败可诊断、可回退、不可静默执行未知动作。

### 4.3 非目标

- 不新增 `TypeHint`、`SelectionState`、`TraversalState` 等锁定 enum 值。
- 不改变 Domain 三岛依赖规则。
- 不把 Provider/ADB 细节放入 Core。
- 不用真实设备测试替代 Simulation baseline。
- 不在 P0 追求完全自治。

---

## 5. 用户故事

### US-1：从当前页面运行

作为开发者，我可以把 emulator 停在 Settings 首页，使用 `BindCurrentScreen` 启动一个 plan，并得到明确的成功/失败结果和 trace。

### US-2：从冷启动运行

作为开发者，我可以指定 package/activity，让 Host 冷启动 App、等待首屏稳定后开始遍历。

### US-3：可重复滚动遍历

作为开发者，我可以运行一个包含长列表的场景，系统不会因为 XML 缺少滚动进度属性而提前结束，也不会无限滚动。

### US-4：安全失败

作为设备所有者，当页面包含删除、购买、授权等高风险操作时，默认策略拒绝执行并写入 trace。

### US-5：故障可诊断

作为维护者，当 ADB 断开、模型超时、JSON 不合法或页面不稳定时，我能从退出码、最终结果和 trace 中区分故障原因。

---

## 6. 功能需求

### FR-1：Host 命令

Host SHALL 至少支持：

- `doctor`：检查 ADB、设备、截图、uiautomator、provider 配置；
- `analyze`：只截图并输出 `PageAnalysis`，不执行动作；
- `run --plan <file>`：执行 plan-driven traversal；
- `--device <serial>`、`--provider <id>`、`--output <dir>`；
- Ctrl+C 取消并完成 trace/session 收尾。

### FR-2：启动前校验

Host SHALL 在执行任何设备动作前校验：

- plan 可反序列化且通过构造期校验；
- device 唯一且 online；
- provider 支持 plan 所需 capability/mode；
- prompt template 完整；
- target package 在 allowlist；
- trace 输出目录可写。

### FR-3：真实入口执行

EntryPolicy SHALL：

- 真实执行策略；
- 使用有界等待确认页面稳定；
- 返回实际使用的 strategy、耗时、失败原因；
- fallback 不得把 ADB/provider 错误伪装为成功。

### FR-4：屏幕分析

PageAnalyzer SHALL：

- 对空截图、传输失败、无效 JSON、越界坐标 fail-fast；
- 保留 `ElementTypeMapper` 作为 type → action 唯一真相源；
- 输出 provider/model/mode/token/latency trace；
- 将真实 vision mode 记录为 `vision`。

### FR-5：滚动

滚动流程 SHALL：

- 单次 step 最多采样一次 screen-state；
- ADB unavailable 与 no-scroll 分开表达；
- 以 swipe 后元素差分作为主要终止证据；
- 具有最大滚动次数 / 最大无新元素次数；
- 不依赖新增 FSM state。

### FR-6：安全执行

每个有副作用的 operation SHALL 在调用 `IActionExecutor` 前通过安全 gate：

- allowlisted app/package；
- allowlisted operation；
- denylisted 文本/语义；
- 坐标范围；
- 可配置 dry-run；
- 拒绝结果写入 trace。

### FR-7：结果与退出码

Host SHALL 提供稳定退出码：

- `0`：目标完成；
- 非 0：配置、设备、provider、plan、运行时、取消分别可区分。

最终结果 SHALL 包含：

- completion reason；
- step count / elapsed；
- visited pages/nodes；
- action summary；
- error summary；
- trace location。

### FR-8：测试

必须新增三层验证：

1. Device unit tests：fake command runner，不依赖真实 ADB；
2. Emulator smoke：真实 ADB + mock model 或固定 PageAnalysis；
3. Real AI E2E：手工/受保护 CI，真实 screenshot/provider，全链路但限定安全 App。

Simulation baseline 继续作为 Core 行为回归主门禁。

---

## 7. 非功能需求

### NFR-1：可靠性

- 所有外部调用有 timeout 和 CancellationToken。
- 所有 retry 有上限和退避。
- 取消后不得遗留 ADB 子进程。
- 单次运行失败不得破坏下次运行。

### NFR-2：安全

- secret 永不进入日志、trace、异常和测试快照。
- 真实设备模式默认 deny-by-default。
- destructive action 必须显式启用。

### NFR-3：可观测性

- 每个模型调用、ADB 命令、入口策略、动作、安全拒绝均可关联到 trace/session。
- 失败路径与成功路径都必须结束 session。

### NFR-4：架构

- Core 不引用 Device 或具体 Provider。
- UniBrain 不引用 StateMachine / Traversal。
- Device 和 Provider 由 Host 组合。
- 不新增锁定 enum 值。

### NFR-5：质量

- 现有 930 个非集成测试保持全绿。
- 新增 Device / Host 测试进入默认测试集。
- 编译 error 必须为 0。
- nullable、source-generator、analyzer 类 warning 在 P1 结束前归零。

---

## 8. 验收场景

### AC-1：当前页分析

Given emulator 在线且停在 Settings 首页  
When 执行 `analyze`  
Then 输出非空 `PageAnalysis`、至少一个 item、provider/model/mode trace，且不产生设备动作。

### AC-2：BindCurrentScreen 遍历

Given emulator 已在 Settings 首页，加载只读安全 plan  
When 执行 `run`  
Then engine 完成遍历，产生至少一次截图分析和一次安全 tap，退出码为 0，session 正常关闭。

### AC-3：冷启动

Given emulator 不在目标 App  
When 使用 `ColdLaunch`  
Then Host 启动指定 package，等待首屏稳定，验证成功后开始 traversal；启动失败时不得 fallback 成假成功。

### AC-4：长列表

Given 一个至少需要 2 次 swipe 的列表  
When 执行 plan  
Then 至少揭示一批新元素，最终因“连续无新元素/边界”终止，不因缺少 `scrollYMax` 在首次判断即结束。

### AC-5：安全拒绝

Given 页面出现危险操作  
When engine 准备执行  
Then action 不发送到 ADB，结果标记为 blocked，trace 包含规则和目标。

### AC-6：故障

Given 运行中断开 ADB 或模拟 provider timeout  
When 外部调用失败  
Then run 在配置的超时内结束，返回非 0 退出码，session 关闭，错误类型可诊断。

### AC-7：稳定性

同一 emulator fixture 连续运行 10 次，要求：

- 无 hang；
- 无遗留 adb 子进程；
- 结果全部可解析；
- 成功率不低于 90%；
- 失败均有明确分类和 trace。

---

## 9. 交付切片

### Slice A — Host 与只读分析闭环

- 新建 executable Host；
- 配置加载、device/provider 选择、secret 注入；
- `doctor` / `analyze`；
- composition root；
- 默认 JSONL trace。

**出口**：一条命令可对在线 emulator 截图并输出 PageAnalysis。

### Slice B — Device 可靠性与真实 EntryPolicy

- 统一 ADB command runner；
- serial、timeout、cancellation、diagnostics；
- Device unit tests；
- BindCurrentScreen / ColdLaunch 真实执行；
- 重构滚动状态采样。

**出口**：无 AI Advisor 参与时，固定 plan 可在 emulator 上启动并安全执行。

### Slice C — Plan-driven E2E

- Host `run --plan`；
- safety gate；
- emulator smoke fixture；
- action/page/scroll/trace 全链路验收；
- 失败退出码与产物。

**出口**：AC-1 至 AC-7 通过。

### Slice D — Goal-driven AI

- 接入 `UnderstandTextAsync`；
- 完成/裁剪 UniBrain 5 个 NIE 方法；
- 明确 `DecideNextActionAsync` 在 engine 外还是 engine 内的权威边界；
- capability 启动期校验；
- exception recovery 与 safety AI 辅助。

**出口**：自然语言目标可稳定转为可审计 plan/advice，不绕过 deterministic safety。

### Slice E — 质量与文档收敛

- warning baseline 清理；
- source generator 加 `#nullable`；
- nullable/analyzer warning 归零；
- system layer docs、README、AGENTS 同步；
- CI 分层：unit / simulation / emulator / real-provider protected。

---

## 10. 优先级总表

| ID | 项目 | 优先级 | 建议切片 |
|---|---|---:|---|
| P0-01 | Host / composition root | P0 | A |
| P0-02 | 真实 EntryPolicy | P0 | B |
| P0-03 | 可靠滚动状态 | P0 | B |
| P0-04 | 真机 E2E | P0 | C |
| P0-05 | deterministic safety gate | P0 | C |
| P1-01 | UniBrain 部分实现与主链接线 | P1 | D |
| P1-02 | Provider 韧性/capability | P1 | A/D |
| P1-03 | ADB command 基础设施 | P1 | B |
| P1-04 | trace 默认交付物 | P1 | A/C |
| P1-05 | warnings 与文档漂移 | P1 | E |

---

## 11. 待确认决策

| 决策 | 推荐 |
|---|---|
| 第一可交付物是 plan-driven 还是 goal-driven？ | 先 plan-driven，缩短真机闭环 |
| 滚动权威来源？ | PageAnalysis 候选 + swipe 后 seen-set 差分；accessibility 仅补充 |
| 首个生产 vision provider？ | 选一个已实际验证的 endpoint；OpenAI-compatible 若保留应移出测试成为独立 Provider |
| 未实现 UniBrain 方法是删除还是补齐？ | 保留接口，按 Slice D 补齐；Host 启动时拒绝未支持 capability |
| Device 是否抽 `IAdbCommandRunner`？ | 推荐抽取，便于 serial/timeout/fake 测试；在 OpenSpec propose 中确认 |
| emulator smoke 是否进入默认 CI？ | 默认 PR 使用 mock/unit；emulator 进入独立受控 job |

---

## 12. 实现证据索引

| 结论 | 证据 |
|---|---|
| Traversal 主链消费页面分析 | `src/UniClaw.Core/Traversal/TraversalEngine.cs:268` |
| Advisor/Text 无生产方法调用方 | C# semantic usage 查询；现有调用均位于 tests |
| PageAnalyzer 2 个 NIE | `src/UniClaw.Core/UniBrain/PageAnalyzer.cs:95` |
| TraversalAdvisor 3 个 NIE | `src/UniClaw.Core/UniBrain/TraversalAdvisor.cs:110` |
| DeepSeek vision/multimodal NIE | `src/UniClaw.DeepSeekProvider/DeepSeekModelProvider.cs:104` |
| EntryPolicy 假执行 | `src/UniClaw.Core/Traversal/TraversalEngine.cs:1439` |
| ADB scroll failure 回落为“到底” | `src/UniClaw.Device/AdbScreenStateProvider.cs:38` |
| ADB action/capture 分别直接创建 Process | `src/UniClaw.Device/AdbActionExecutor.cs`、`AdbScreenCapture.cs` |
| 真 vision 测试默认 Skip | `tests/UniClaw.Core.Tests/UniBrain/RealVisionIntegrationTests.cs:49` |
| Tests 未引用 Device | `tests/UniClaw.Core.Tests/UniClaw.Core.Tests.csproj` |
| 无 executable Host | `src/UniClaw.Core.sln` 与各 `.csproj` |

---

## 13. Definition of Done

本 PRD 的 P0 可判定完成，当且仅当：

- 有一个可执行 Host；
- `doctor`、`analyze`、`run --plan` 可用；
- 真实 EntryPolicy 不再假成功；
- Device 命令支持 serial、timeout、cancellation 和可测试 runner；
- 滚动不依赖 `scrollYMax` 才能正确终止；
- 所有真实动作经过 deterministic safety gate；
- emulator 端到端场景覆盖启动、分析、点击、滚动、完成和 trace；
- 现有 930 个非集成测试保持全绿；
- session/result/trace 在成功、失败、取消三条路径都完整；
- system docs 与实际实现同步。

完成上述 P0 后，再以独立 OpenSpec changes 推进 Slice D/E。
