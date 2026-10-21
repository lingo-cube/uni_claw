# UniClaw 成为完备 UI 自动化测试专业 Agent 的 Gap 调研

> Status: DRAFT（研究输入，未授权实现）
> Authority: NONE

日期：2026-10-08  
范围：当前 `uni-harness` 工作树；只读审查产品代码、Workspace 前端、测试资产、环境工具和架构文档。本文是研究输入，不改变产品代码、Change State 或现有测试资产。

## 结论

项目已经具备一条较完整的 Android Settings 自动化内核：截图和 UiAutomator 采集、结构化/视觉观察、世界模型 grounding、Effect Boundary、动作策略、场景映射、TRX 覆盖率和运行证据都已有代码或测试承载。它还不是一个完备的 UI 自动化测试专业 Agent，主要原因是浏览器链路只有接口与 dry-run，没有真实观察和执行闭环；Agent 计划仍是受 Settings coverage director 驱动的单步咨询，而不是跨平台、可恢复、可解释的测试任务系统；Workspace 前端测试停留在 Node seam/HTML 字符串层；测试环境、视觉回归、并发隔离和失败诊断还没有形成统一的专业测试产品面。

## 已有基础（作为 gap 的边界）

| 垂直 | 已有证据 | 当前能证明什么 |
|---|---|---|
| Android 观察 | `src/UniClaw.Host/UiAutomatorDump.cs`、`SettingsTraversalLiveFeed.cs`、`tests/UniClaw.Host.Tests/UiAutomatorDumpTests.cs` | 可采集并解析真实设备截图/UI hierarchy；有 live selector 和 fail-closed 环境预检 |
| 感知与回放 | `src/UniClaw.Kernel/Perception/`、`tests/UniClaw.Simulation.Tests/WorldReconstruction*`、golden corpus | screenshot、XML、provider response 可做带 provenance 的 replay 输入；场景状态由真实 TRX 派生 |
| 执行安全 | `src/UniClaw.Kernel/Effects/EffectBoundary.cs`、`TargetBinding.cs`、`AdbLiveEffectDriver.cs` | target binding、freshness、effect gate、receipt 和驱动支持集有明确边界 |
| 覆盖编排 | `src/UniClaw.Host/SettingsCoverage/SettingsCoverageDirector.cs`、`SettingsCoverageLedger.cs` | Settings 遍历有界、单步、弹窗清障、未知页面 defer 和覆盖账本 |
| 测试治理 | `tools/scenario-coverage.py`、`tools/validate-testset-manifests.py`、`scenarios/README.md` | 场景必须映射测试；陈旧 TRX、无结果、状态不匹配会失败 |
| Workspace 契约 | `web/uniclaw-workspace/tests/*.test.js`、`testsets/workspace-contract/README.md` | Query Core、DSH envelope、静态渲染和 browser bridge 的纯 Node 契约存在 |

## Gap 1：浏览器真实执行与浏览器观察没有闭环（P0）

### 证据与细节

1. `EgoBrowserEffectDriver` 明确是 dry-run，只构造 `ego click node:{id}`，真实 CDP 接入属于后续 change（[EgoBrowserEffectDriver.cs:5](/Users/fran/Documents/Code/spacex/uni_claw/src/UniClaw.Kernel/Effects/EgoBrowserEffectDriver.cs:5)、[EgoBrowserEffectDriver.cs:44](/Users/fran/Documents/Code/spacex/uni_claw/src/UniClaw.Kernel/Effects/EgoBrowserEffectDriver.cs:44)）。
2. `NativeLocator.BrowserNode` 只定义 `browser.backend-node-id` 字符串锚；仓库没有对应 CDP/DOM observation provider、page/frame/session identity 或 stale node 重新获取实现（[NativeLocator.cs:4](/Users/fran/Documents/Code/spacex/uni_claw/src/UniClaw.Kernel/World/NativeLocator.cs:4)）。
3. `EgoBrowserDeliveryTests` 的“端到端”只在 Kernel double 上验证 `ego click node:827`，测试本身标为 dry-run（[EgoBrowserDeliveryTests.cs:14](/Users/fran/Documents/Code/spacex/uni_claw/tests/UniClaw.Kernel.Tests/EgoBrowserDeliveryTests.cs:14)、[EgoBrowserDeliveryTests.cs:80](/Users/fran/Documents/Code/spacex/uni_claw/tests/UniClaw.Kernel.Tests/EgoBrowserDeliveryTests.cs:80)）。

### 影响

无法证明当前页面目标是否存在、唯一、可见、可交互，click 是否真的改变 DOM 状态，以及导航、弹窗、iframe 或 DOM 重建后旧目标是否失效。浏览器“通过”目前可能只是 Kernel 收到了 dry-run receipt。

### 补齐建议

冻结 browser observation/delivery contract：page、frame、DOM/ARIA snapshot、stable backend node、locator provenance、actionability、post-action observation 和 browser artifacts。实现可替换的真实 Chromium/CDP adapter，覆盖启动、context 隔离、导航、DOM/ARIA、click/fill/press/scroll、截图、console/network。用本地测试站点验收：DOM 重建使旧 node 失败，系统 re-observe/re-ground，真实状态变化由新观察证明。

## Gap 2：跨平台 UI 感知模型仍以 Android 为中心（P0/P1）

真实环境基准只注册 Android AVD、ADB screenshot、UiAutomator 和 Settings probe（[test-emulator.md:8](/Users/fran/Documents/Code/spacex/uni_claw/docs/agents/test-emulator.md:8)）。`slow.visual` 仍是 description-only、没有运行时实例（[PerceptionCapabilityComposition.cs:5](/Users/fran/Documents/Code/spacex/uni_claw/src/UniClaw.Host/SettingsCoverage/PerceptionCapabilityComposition.cs:5)），Settings profile 也明确 `visualEnabled: false`（[settings-coverage.yaml:40](/Users/fran/Documents/Code/spacex/uni_claw/.dsh/profiles/settings-coverage.yaml:40)）。

这能支撑 Android XML/截图场景，却没有 Web DOM/ARIA、canvas、shadow DOM、iframe、跨域页面或浏览器权限弹窗的观察语义。建议为 browser 建独立 observation capability，DOM/ARIA 为结构事实，截图/OCR/视觉为补充证据，统一投影到 World occurrence，同时保留 producer、page/frame、capture revision 和冲突状态；视觉证据只能形成候选/诊断，不能独立授权 effect。

## Gap 3：Agent 还不是通用测试任务规划器（P1）

`SettingsCoverageDirector` 的职责是从 coverage ledger 取下一条 directive，并要求模型每次只提交 exactly one step；偏离只允许有界纠正（[SettingsCoverageDirector.cs:5](/Users/fran/Documents/Code/spacex/uni_claw/src/UniClaw.Host/SettingsCoverage/SettingsCoverageDirector.cs:5)、[SettingsCoverageDirector.cs:164](/Users/fran/Documents/Code/spacex/uni_claw/src/UniClaw.Host/SettingsCoverage/SettingsCoverageDirector.cs:164)）。Profile 只配置固定 Settings 页面、步数、滚动和弹窗规则（[settings-coverage.yaml:8](/Users/fran/Documents/Code/spacex/uni_claw/.dsh/profiles/settings-coverage.yaml:8)）。产品 Agent 输出仍是单一 `submit_decision`，`browser`、`device`、`click`、`workflow` 等直接能力被禁止（[ProductCapabilities.cs:7](/Users/fran/Documents/Code/spacex/uni_claw/src/UniClaw.Agent/Profile/ProductCapabilities.cs:7)）。

安全边界是正确的，但缺少 TestIntent/TestPlan/TestStep/TestAssertion/TestOracle/TestData/RetryPolicy/StopPolicy 这一套受控测试任务契约。Agent 目前能做受约束 traversal consultant，不能表达登录、准备数据、跨页面业务流、参数化、断言组合、测试依赖、失败后收敛或跨浏览器差异。建议先实现有限 DSL（导航、输入、点击、等待、断言、截图、网络/console 断言），由 Host/Kernel 负责 capability resolution、grounding、安全门和证据。

## Gap 4：Workspace 前端缺少真实浏览器 QA 层（P1）

`web/uniclaw-workspace/package.json` 只有 Node test 和 browser bundle build，没有 Playwright/WebDriver 依赖或服务脚本（[package.json:1](/Users/fran/Documents/Code/spacex/uni_claw/web/uniclaw-workspace/package.json:1)）。`browser-bridge.test.js` 使用手写 document/container double 测样式注入和 envelope，其余测试主要断言 HTML 字符串、view model 和 controller（[browser-bridge.test.js:7](/Users/fran/Documents/Code/spacex/uni_claw/web/uniclaw-workspace/tests/browser-bridge.test.js:7)）。Workspace contract README 只承诺共享前端与 DSH static bundle 的契约入口，没有真实浏览器、键盘/鼠标、响应式、可访问性或视觉回归验收（[README.md:1](/Users/fran/Documents/Code/spacex/uni_claw/testsets/workspace-contract/README.md:1)）。

因此无法发现真实布局溢出、focus 顺序、pointer event、滚动容器、浏览器差异、字体/截图漂移、弹窗遮挡或 host bridge 生命周期问题。建议增加固定 server root 的真实浏览器 harness、Chromium/Firefox/WebKit 矩阵、可访问性快照、视觉 baseline、console/network 收集和 trace viewer，并将 DOM contract、interaction、accessibility、visual、responsive 分层。

## Gap 5：测试环境矩阵与并发隔离不足（P1）

环境规范只登记一个 Android AVD；ENVIRONMENT 测试显式开启，调用方负责设备生命周期（[test-emulator.md:21](/Users/fran/Documents/Code/spacex/uni_claw/docs/agents/test-emulator.md:21)）。真实咨询还依赖专用 DSH 3081 和本机代理（[test-emulator.md:68](/Users/fran/Documents/Code/spacex/uni_claw/docs/agents/test-emulator.md:68)）。Test Lab 仍是 Host 未提供持久化 Project/Test Set Catalog 时的声明式目录（[testsets/README.md:3](/Users/fran/Documents/Code/spacex/uni_claw/testsets/README.md:3)），manifest 校验只枚举三个固定文件（[validate-testset-manifests.py:10](/Users/fran/Documents/Code/spacex/uni_claw/tools/validate-testset-manifests.py:10)）。

缺少 browser context/device farm、iOS/Windows、并行 worker lease、网络/权限/locale/时区/账号 fixture reset。建议定义 EnvironmentProfile/Lease/Fixture/Teardown schema；每个 run 隔离 browser context、device serial、storage namespace、artifact root，并把环境失败标为 blocked/pending-env，不能污染产品结论。

## Gap 6：断言、视觉回归与测试 oracle 不够完整（P1）

场景覆盖工具解决了“自报 passing”问题，状态来自真实 TRX（[scenario-coverage.py:2](/Users/fran/Documents/Code/spacex/uni_claw/tools/scenario-coverage.py:2)）；golden 认证解决 expectations digest 和源码 hash（[scenarios/README.md:25](/Users/fran/Documents/Code/spacex/uni_claw/scenarios/README.md:25)）。但没有像素 diff 阈值、结构化 accessibility diff、DOM semantic diff、动画冻结、字体/渲染容差、性能/网络断言或人工视觉 gate，也没有统一 browser DOM/ARIA/network/console/video/HAR artifact contract。

建议扩展 Assertion/Artifact schema：semantic DOM/ARIA、visual baseline、layout geometry、console/network、download/upload、performance budget 分开表达，每条断言带 expected/actual/evidence/provenance。视觉比较只报告候选 diff，重要区域和阈值由测试 author/人工 gate 固化。

## Gap 7：可靠执行、恢复和诊断尚未覆盖浏览器语义（P1/P2）

Effect Boundary 有 execution journal 和 replay/recovery 方向，但 browser driver 目前没有真实 browser attempt id、CDP request/response、page revision 或 idempotency 关联。Settings director 能处理 unknown page、popup bounded stop 和 directive retry（[SettingsCoverageDirector.cs:85](/Users/fran/Documents/Code/spacex/uni_claw/src/UniClaw.Host/SettingsCoverage/SettingsCoverageDirector.cs:85)），没有 navigation timeout、target closed、frame detached、stale element、dialog blocked、network idle 不稳定、download pending 等 browser failure taxonomy。

建议为 browser execution journal 增加 page/session/frame revision、action attempt、locator snapshot、pre/post capture、retry class 和 teardown status；统一映射 re-observe、re-ground、retry、replan、stop，并接入 run-report/Workspace。

## Gap 8：安全策略覆盖面仍以 Android Settings 为主（P1）

Settings profile 有 `actionPolicy.required: true` 并绑定 Android forbidden actions policy（[settings-coverage.yaml:48](/Users/fran/Documents/Code/spacex/uni_claw/.dsh/profiles/settings-coverage.yaml:48)）；但浏览器危险动作（真实提交、购买/支付、删除、发送、敏感上传、下载执行文件、OAuth、持久化 cookie）没有同等级 taxonomy、审批、scope、expiry 和 evidence。建议增加 browser action policy；测试默认使用本地假站和假账号，真实外部副作用必须显式 blocked 或人工批准。

## 建议的补齐顺序

1. P0：冻结 browser observation/delivery/evidence contract，实现真实 Chromium adapter 和最小本地站点 E2E，证明真实 DOM → 重新定位 → click → 新观察。
2. P1：落地 TestIntent/TestPlan/TestAssertion/TestData/EnvironmentProfile/Lease/Artifact schema；补真实浏览器 harness、可访问性、视觉、console/network 证据。
3. P1：把 browser action policy、retry taxonomy、execution journal 和 run-report 接入闭环；补跨 worker/browser context/device lease。
4. P2：扩展多浏览器、多 viewport、locale/network 矩阵、性能预算和 flaky quarantine；由真实 buyer 驱动，不预造空协议。

## 研究边界

本文只使用仓库当前一手代码、测试、配置和架构文档。没有把 dry-run、fixture、Node seam 或 Android live evidence 误计为真实浏览器 UI 通过；没有修改产品代码或覆盖工作树已有变更。
