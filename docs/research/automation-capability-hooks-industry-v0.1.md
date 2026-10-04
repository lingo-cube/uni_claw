# 自动化测试能力接缝：业界一手资料研究 v0.1

状态：Research / Authority: NONE  
范围：只研究业界自动化测试框架和可观测性规范如何暴露扩展接缝；不定义 UniClaw 接口，也不实现性能分析、语言检查或视觉分析。

## 1. 研究结论

业界成熟框架通常把“测试动作”和“附加能力”分开：核心执行器提供稳定的生命周期、事件、上下文和结果附件；检查器、性能采集器、视觉/可访问性扫描器和外部观测器作为 fixture、listener、plugin、reporter 或独立 telemetry consumer 接入。

对 UniClaw 最有价值的启发不是照搬某个框架的 API，而是预留四类事实接缝：

1. **生命周期事实**：session/run、setup/teardown、action/command、observation、assertion/verification、retry/timeout、terminal。
2. **事件订阅**：允许外挂模块异步接收带相关性的不可变事件；事件消费者可以只观察，不取得 Kernel 决策权。
3. **证据与附件**：截图、DOM/ARIA 快照、网络记录、日志、视觉帧、扫描结果和自定义文件必须能挂到一次 test/run/action 上。
4. **时序与关联**：区分有明确开始/结束的 operation、单点 event 和外部异步信号；所有信号都需要 Run/Action/Observation 等 correlation。

用户提出的“点击后响应时间可能被等待污染、未来可能外挂摄像头观测”应直接影响接缝设计：Kernel 只能发布动作发出、receipt、观察到达、验证完成等可观测事实及时间戳；“真实用户可见响应”可以由外部摄像头或其他传感器产生独立观测，再通过 correlation 关联，不能把 Kernel 内部等待时间自动宣称为用户感知延迟。

## 2. 一手资料中的能力模式

### 2.1 Setup、teardown、fixture 与隔离

Playwright Test 把 fixture 定义为建立每个测试所需环境的机制，并强调 fixture 在测试之间隔离；fixture 可以是 test-scoped 或 worker-scoped，且自定义 fixture 会出现在 UI、Trace Viewer 和测试报告中。[Playwright Fixtures](https://playwright.dev/docs/test-fixtures)

`TestInfo` 可在测试函数、`beforeEach`、`afterEach`、`beforeAll`、`afterAll` 和 test-scoped fixture 中取得，提供附件、输出路径、超时、重试状态、测试状态等上下文。[Playwright TestInfo](https://playwright.dev/docs/api/class-testinfo)

**对 UniClaw 的启发**：能力模块需要明确作用域（Run-scoped、Action-scoped 或 process-scoped）、初始化/收尾阶段、隔离边界和失败隔离行为。一个模块不应依赖另一个模块的隐式初始化顺序。Run terminal 时应有收尾通知和未完成采集的处置语义。

**不能直接照搬**：Playwright fixture 是测试框架控制的依赖注入机制；UniClaw 的能力模块不能因此取得 WorldModel、Assurance 或 Effect authority，也不能把“fixture 初始化成功”当成产品能力已被授权。

### 2.2 事件订阅和双向事件流

WebDriver BiDi 用双向 WebSocket 让控制端订阅并接收浏览器事件。W3C 规范的 `session.subscribe` 可订阅全局或指定 navigable 的事件；规范还定义了未发出的日志事件缓冲。[WebDriver BiDi Specification](https://w3c.github.io/webdriver-bidi/)

Selenium 的官方说明把 BiDi 描述为从浏览器流式获得网络请求、控制台消息、JavaScript 错误等事件，并支持在 Selenium session 过程中监听、记录或操纵事件。[Selenium WebDriver BiDi](https://www.selenium.dev/documentation/webdriver/bidi/)

Playwright 也提供 Page 级事件订阅；网络请求通常经历 `request`、`response`、`requestfinished`，失败时可能发出 `requestfailed`。[Playwright Page events](https://playwright.dev/docs/api/class-page) 这种模型把“发起”“收到响应头”“完成下载”“失败”分成不同事实，消费者可选择自己需要的边界。

**对 UniClaw 的启发**：应有明确的事件目录、订阅范围、事件顺序/缓冲策略和背压/丢失语义。对一次点击至少应能区分 `dispatch_started`、`receipt_produced`、`observation_accepted`、`verification_completed`，而不是只发一个“button_done”。外挂摄像头等慢消费者可以独立订阅并异步回传 observation。

**不能直接照搬**：BiDi 事件是浏览器自动化协议的 wire-level 事件；UniClaw 需要的是 Kernel lifecycle facts，不能把浏览器事件名称直接升级为产品语义，也不能允许订阅者通过事件回调绕过 Effect Boundary 修改动作。

### 2.3 Trace、snapshot、artifact 和报告

Playwright tracing 可以记录操作、网络活动、截图、DOM snapshot、ARIA snapshot 和源文件；支持把同一个 BrowserContext 切成多个 trace chunk，也支持实时写入未归档 trace 文件。[Playwright Tracing](https://playwright.dev/docs/api/class-tracing)

Playwright `testInfo.attach()` 将截图、JSON 或外部文件挂到测试结果，reporter 可以展示这些附件；输出路径按测试隔离，避免并行测试互相覆盖。[Playwright TestInfo attachments](https://playwright.dev/docs/api/class-testinfo)

Appium 的 session event history 默认记录 driver command 的开始/结束时间，driver 或 plugin 可以追加自定义事件类型；事件可通过 API 按类型过滤读取。[Appium Protocol: event history](https://appium.io/docs/en/latest/reference/api/appium/)

**对 UniClaw 的启发**：能力接缝应允许将附件和诊断结果关联到 Run、Action、Observation 或 Verification；附件应有 content type、来源、捕获时间和生命周期，结果报告与 Kernel 权威模型分离。Trace 适合因果操作链；snapshot/视频/摄像头帧属于外部证据附件，不应塞进一个万能 event payload。

**不能直接照搬**：Playwright trace 是浏览器专用格式，Appium event history 也只保证 session command 级别；UniClaw 仍需自己的 correlation、authority 和 retention 规则，不能把 trace 文件存在就当成 Evidence 已被接受。

### 2.4 性能、时序与异步完成

OpenTelemetry 将 Span 定义为有开始和结束的 operation，包含 start/end timestamp、attributes、events、status 和 parent context；单点事实应使用 Span Event，而不是伪造一个 duration span。[OpenTelemetry Traces](https://opentelemetry.io/docs/concepts/signals/traces/)

OpenTelemetry 的规范建议为有时长且具有监控价值的 operation 定义 span，为点时事件使用 event，并通常配套 duration metric 与 exception event。[How to write semantic conventions](https://opentelemetry.io/docs/specs/semconv/how-to-write-conventions/)

OpenTelemetry 的 SpanKind 还区分 request/response 的 CLIENT/SERVER 与异步 PRODUCER/CONSUMER，这说明“发出请求”和“得到最终处理结果”可能不是同一个时间区间。[OpenTelemetry Trace API](https://opentelemetry.io/docs/specs/otel/trace/api/)

**对 UniClaw 的启发**：性能接缝应该发布边界事实，而不是强行给出一个单一 latency。至少保留 action dispatch、driver receipt、post-action observation、verified 等时间点；外部传感器可用 producer/consumer 式关联记录用户可见完成时间。对于等待、超时、取消和未观察到的情况，数据模型需能表达 unknown/censored，而不能记作零或成功。

**不能直接照搬**：OpenTelemetry 是 telemetry 交换和语义规范，不会告诉 UniClaw 哪个 UI 状态是“完成”；它也不解决摄像头帧与动作的业务匹配。Span/metric 的命名和导出应在能力层适配，不能反向成为 Kernel 的决策接口。

### 2.5 视觉、可访问性、本地化与快照检查

Playwright 的 accessibility testing 文档明确指出，自动化扫描可以捕获部分常见问题（例如缺少标签、对比度、重复 ID），但不能发现全部可访问性问题，需要结合人工评估和真实用户测试。[Playwright Accessibility testing](https://playwright.dev/docs/accessibility-testing)

该文档还建议在交互后等待目标状态，再运行扫描；扫描结果可以作为 JSON attachment 附到测试报告，并可通过 fixture 复用规则配置。

Playwright tracing 支持每次 action 的 DOM、ARIA 和 screen snapshot；发布说明说明 ARIA 与 screen snapshot 可在 Trace Viewer 中并排查看。[Playwright Tracing](https://playwright.dev/docs/api/class-tracing) · [Playwright release notes](https://playwright.dev/docs/release-notes)

**对 UniClaw 的启发**：语言合规、可访问性、视觉回归、布局/遮挡等都应是“观察完成后运行的独立检查能力”，其输出是 finding/attachment/report。接缝应允许检查器指定观察版本、目标范围、规则版本和证据引用，并承认自动化检查覆盖不完整。

**不能直接照搬**：DOM/ARIA 快照只适用于有相应语义树的平台；移动原生 UI 或摄像头观测需要不同 adapter。自动扫描的 Pass 不能直接提升成产品正确性或动作授权，中文字符比例也不能等同于本地化翻译正确。

### 2.6 Plugin / driver / 外挂观测

Appium 将 driver 和 plugin 作为可选扩展；plugin 可以拦截或处理特定 Appium command，添加新路由，并在异常 session shutdown 时得到清理机会。[Appium Plugins](https://appium.io/docs/en/2.15/ecosystem/plugins/) · [Building Plugins](https://appium.io/docs/en/latest/developing/build-plugins/)

Appium 文档特别提醒 plugin 很强大，因此必须由启动 Appium server 的管理员显式启用和信任；这体现了“能力可插拔”与“能力有权限边界”同时存在。

**对 UniClaw 的启发**：能力集成层需要显式注册/启用、能力身份、版本、作用域和失败隔离；外挂观察器可以只消费事件并写入独立证据通道，必要时提供 cleanup/health 状态。执行型扩展必须继续通过既有 Effect Boundary。

**不能直接照搬**：Appium plugin 可以覆盖 command，这是 Host/driver 层的特权。UniClaw 的观察和诊断模块不应默认拥有 command interception 权限；扩展可执行能力需要另一条经过治理的接口。

## 3. 建议纳入 UniClaw 接缝清单的生命周期节点

下表是设计输入，不是已冻结接口：

| 类别 | 建议事实节点 | 典型消费者 | 接缝设计关注点 |
|---|---|---|---|
| 生命周期 | RunAdmitted / RunStarted / RunTerminal | fixture、资源管理、报告器 | 作用域、幂等收尾、失败隔离 |
| 环境 | SetupStarted / SetupCompleted / TeardownStarted / TeardownCompleted | 设备健康、环境采样、外挂 recorder | 不把 setup 成功当业务成功 |
| 动作 | IntentIssued / DispatchStarted / ReceiptProduced | 时序测量、命令审计 | action correlation、取消和重试 |
| 观察 | CaptureStarted / ObservationAccepted / RevisionPublished | 语言、视觉、可访问性检查 | 来源、版本、处理时间和接受状态 |
| 验证 | VerificationStarted / Verified / VerificationUnknown | 业务检查、报告 | “未验证”不能伪装成失败或成功 |
| 异步外部 | SensorObservationReceived / CameraFrameLinked / NetworkEventReceived | 摄像头、设备传感器、BiDi/Appium adapter | 外部时钟、关联置信度、延迟和丢失 |
| 可靠性 | RetryScheduled / Timeout / Cancelled / Failure | flaky 分析、性能统计 | attempt 与 logical action 分开 |
| 证据 | ArtifactAttached / ArtifactFinalized | trace、截图、视频、JSON 报告 | content type、大小、保留策略、引用完整性 |

这组节点覆盖了用户提出的语言检查、按钮响应测量和外挂摄像头，同时也覆盖业界常见的网络/控制台观测、失败重试、快照和报告需求。下一轮设计应先确认每个节点的 Owner、Authority、correlation、同步/异步语义和失败行为，再决定接口拆分。

## 4. Grill 时应重点追问的问题

1. 哪些事件是 Kernel 已确认的事实，哪些只是 adapter 或外部传感器的观察？
2. 外挂摄像头结果如何与一次 `Intent` 或 `Receipt` 关联：时间窗口、设备帧号、显式标记还是概率关联？
3. `PostActionVerified` 是否要求 Kernel 内部验证，还是允许外部 verifier 以独立 evidence 形式提交？
4. 能力模块的失败、超时、背压和卸载是否影响主 Run？不同能力类型是否需要不同隔离级别？
5. 哪些附件应实时流式、哪些只在 RunTerminal 归档？是否需要按 action/observation 分片以控制大小？
6. 如何避免同一次操作同时被 Kernel metrics、OpenTelemetry span、外部 recorder 重复计时后产生互相矛盾的“响应时间”？
7. 哪些检查结果仅用于诊断，哪些结果在未来经过显式 promotion 后才可能进入 Acceptance 或阻断策略？

## 5. 对本轮问题的边界结论

当前不应冻结“性能 Hook = 点击前后计时”这一单一模型。应先冻结一个能表达多来源事实的 Integration Surface：Kernel 发布生命周期事件，能力模块订阅并生成独立 evidence/measurement，外部视觉或传感器模块可以异步回传带 correlation 的 observation。业界资料支持这种分层和关联方向，但不提供 UniClaw 的 Owner/Authority 决策；后者必须由项目自己的架构基线和 Grill 决定。

