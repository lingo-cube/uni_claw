# RUNTIME-STORAGE-002 顺序接续指令

状态：**Control/Plan前置已通过；准备派发World，Coverage顺序依赖World。** 原Leader的本片证据为evidence/core-versions/README.md、versions-storage-final.json（Host159/159）、review-spec.md与review-standards.md（各0）、verification.json（65项）。用户已授权交给既有“存储验收后的测试资产治理”（local/01a1730a-957c-7ab2-8d46-2574de5a93e9）；父Change未关闭。

## 接续顺序

1. **Control / Plan（前置已完成，原Leader）**：新写v2可逆State/Versions及独立周期基线，原不可变事实和旧v1严格读取保留，数据latest/双向版本不是完整认证或执行许可。原两行batch一次确认、故障锁死和Kill恢复已验。具体候选及成本见上述引用，后续任务不得修改本片封存证据。
2. **World 独立基线与重建链策略（第一项，已有资产治理聊天）**：针对真实 World 历史读取者与完整恢复契约，明确基线的消费、生成/提交位置、覆盖序号及增量链重建上界；实现必要的独立基线和链策略，并以原严格完整链验证为兼容基准。目标会话作为这两片的执行Leader，完成本片独立双轴评审、验收四元组及资产收尾后，按用户已授权顺序自行派发并进入Coverage，无需再等人类确认。
3. **Coverage 独立必交付与附件保护（第二项，同一聊天）**：把现有 Coverage 正式报告接入既有必交付机制，固定要求、必要输入及来源身份，支持跨重启查询和仅报告重试；逐用途保护截图、层级等附件及其他唯一来源，覆盖未交付来源保护与缺失时显式失败。不能用 run-report 已接入来替代 Coverage 自身验收。

## 源码定位与真实职责

- World 原始记录由 `src/UniClaw.Host/Runtime/FileWorldJournal.cs` 持有文件介质与严格读取/提交：记录带 RecordingId、顺序、前序摘要和 hash，head 与记录末位必须一致；读构造会验证整条文件。World 语义记录及修订由 Kernel 的 `src/UniClaw.Kernel/World/WorldHistory.cs`、`WorldModel.HistoryRecording.cs` 等 Owner 产生。Host journal 是原记录的持久化 adapter，不得替 World 改写事实或把只读历史恢复解释为执行许可。
- `src/UniClaw.Host/Runtime/WorldRevisionDeltaCodec.cs` 与 `FileWorldJournal` 当前支持 revision 增量编码/解码；当前完整历史仍从初始化记录顺序解码并由原 reader 校验。需补的是独立基线的真实读取消费和有界重建策略，不得把当前缓存/分配优化称作基线，也不得未经兼容证明截断旧链或放宽旧 reader。
- Control 的语义 Owner 在 `src/UniClaw.Kernel/Control/TraversalControl*.cs`，文件 journal 在 `src/UniClaw.Host/Runtime/FileControlTraversalJournal.cs`；Plan 的版本 Owner/记录在 `src/UniClaw.Kernel/Runtime/RuntimePlanHistory.cs`。这两者是本顺序第一步的产品前置，属于原 Leader，不随本交接转移。
- Coverage 的实时事实 Owner 是 `SettingsCoverageLedger`（`src/UniClaw.Host/SettingsCoverage/SettingsCoverageLedger.cs`）及其调用者；`src/UniClaw.Host/SettingsCoverage/SettingsCoverageRunner.cs` 运行并在收尾产出 coverage 结果、report 与附件。账目参与遍历、屏障和 overview 等实时消费，不可整体降为辅助缓存；报告/附件是交付材料，不反向成为账目 Owner。
- 已有 Host 交付实现位于 `src/UniClaw.Host/Runtime/RequiredReportDelivery.cs`，其接入路径包括 Runtime/工具与报告端点。它持久化接受的要求、固定输入和生成器、发布/交付状态，支持读取和报告重试。Coverage 任务应复用该既有产品契约的合适入口，不另造第二套交付状态，也不改变 Runtime Outcome、Agent 咨询或 Effect 结果。

## 配置与路径约束

- 产品存储配置与 Harness 开发测试资产配置分开。产品配置通过现有产品构造器的 `Options` / limits 等显式参数注入；产品代码不得读取 `test-assets.v1`、测试资产 JSON 或其他 Harness 资产登记文件。开发资产配置归资产治理 Change 与工具所有。
- 产品文件路径由 Host 的产品根目录与分类相对路径组合。若需要可配置分类位置，产品根与分类相对路径必须经过同一个解析逻辑，且解析结果贯通创建/写入、读取/查询、恢复和报告来源保护；不能由各消费者各自拼路径，也不能把 Harness 的 `parentRoot/workPath/archivePath/categoryPaths` 直接当成产品配置。
- 资产治理工具自身仍按其既定契约，以一份解析逻辑将 `parentRoot` 与相对 `workPath/archivePath/categoryPaths` 组合，贯通资产写、读、引用、恢复和收尾；产品存储不能消费这份开发配置。不要为产品和Harness合并配置，也不要新造没有真实 buyer 的通用配置平台。

## 验收要求与文件责任

**第一项：World。** 目标会话按workitems/WI-STORAGE002-WORLD-CONTINUATION.json负责World存储实现、对应公开测试及证据；至少证明基线由真实 World reader 消费、提交位置与 World 原记录身份/序号一致、基线缺失/损坏时行为明确、重建链有界且最新/历史读取符合契约。保留现有完整链 reader 的校验和旧格式读取，证明基线/增量组合与逻辑记录顺序、值及恢复结果一致；不重放领域操作、不重新入证、不生成执行许可。记录字节、读取/解码/重建成本及新增基线成本，避免只凭结构声称收益。产品 Kernel Owner、World 语义事实及其他产品存储族仍属原 Leader；资产治理聊天若触及不归属文件，先回 Leader 划定具体文件和调用者，不扩大范围。

**第二项：Coverage。** 目标会话按workitems/WI-STORAGE002-COVERAGE-CONTINUATION.json负责Coverage输出到既有必交付产品路径接线、对应测试与证据；Leader 保持 `SettingsCoverageLedger` 的领域事实和运行判定语义。验证接受要求后、输入固定后、生成/发布中、落盘但未确认及重启后的查询/重试窗口；重试只生成/校验报告，Agent/Effect/任务重执行为零，Runtime Outcome 不变。清点每类 XML/PNG/trace/facts/report 输入的真实消费者与用途：实时事实继续由原 Owner 管理；必交付输入及被证明/显式保留引用的附件，在交付未完成时受保护；不可恢复的必要来源明确报错，不伪造完整报告或重跑任务补料。不得把 Coverage 事件字段不足误当成可无损重建报告。

交接聊天按上述两个WorkItem的具体文件责任独立推进。产品领域事实/判定语义与本片Control/Plan、资源实现保持；必要的交付机制扩展需兼容原run-report并运行相关回归。共同的INDEX/state只小范围追加，不整文件覆盖他人修改，不改父Change为CLOSED。
## 执行与交接条件
- 现在先完成你正在进行的资产治理片收尾，然后直接进入World；不要求等待本聊天的额外确认。World通过独立双轴评审、公开验收及资产收尾后，在本文件追加证据与结论，将Coverage WorkItem改为in_progress并接续。
- 若出现与已接受架构/原Owner语义冲突、需要削弱严格恢复或必要来源保护、或确需改动分配之外产品核心文件，先暂停该具体项并如实解释冲突；不要为普通参数/实现选择重复向用户确认。
- 两项都完成后，在目标聊天给用户交付代码/行为、四元组、实际证据和残余问题，并在本文件追加结果。父RUNTIME-STORAGE-002的A1–A10最终汇总与关闭仍由原Leader处理，不把局部完成冒认为父关闭；无需给其他聊天回发工具消息或创建自动监控。

## 当前派发结论
Control/Plan前置已通过，World可以开始；Coverage待World完成后按上述同授权直接接续。传递工具返回成功后，由原Leader追加实际派发回执，不能将“已发指令”等同“两个后续实现已完成”。

## 实际派发回执
原Leader已通过send_message_to_thread把上述顺序与两个WorkItem发至既有会话，工具返回目标threadId成功；见evidence/core-versions/dispatch.json。本回执仅证明指令已传递，不代表World或Coverage已经实现/验收。目标先完成当前资产片收尾，再按本文件接续。

## Leader并行保留范围（2026-10-26）
TaskInstanceStore/ProductSessionStore的确认顺序与关闭后静默写入已由原Leader补正（evidence/host-identity/README.md，12/12、两轴0）。RuntimeRunStore混合字段核定继续由原Leader承担，不纳入World/Coverage委派；原产品文件责任与顺序不变。

## World接续实际完成（2026-10-26，本聊天）
已完成FileWorldJournal v3同operation/head提交独立基线（默认32）及WorldHistoryVersionReader有界数据查询；原完整WorldHistoryRead/旧v1/v2校验保持，数据投影不授续跑。四元组：method=154项公开相关测试＋同ID/同逻辑128修订成本probe＋既有封存v1/v2与原Evidence公开读取＋两轴独立审查；expected=基线实际消费/重建≤32/原值顺序及旧SHA保持/无执行许可；actual=154/154、两轴各0、真实旧源六文件SHA保持、v3 latest32对旧v2 128且仍扫258帧，v3文件1,236,533对v2 1,059,133 bytes（额外约16.7%，未声称稳定速度/物理IO/峰值）；evidence=world-baselines-contract.md、evidence/world-baselines/README.md、candidate.json/patch、world-final-v2-result.json、cost-result.json、legacy-result.json、review-standards/spec-v1.md、verification.json。新独占工具构建副本已按精确清单删除，原源/证据/RED与所有Run保留，见evidence/world-baselines/finalization.json与cache-cleanup-receipt.json。用户另明确批准WorldAsyncStorageTests.cs仅一行格式期望更新。
World WorkItem done，按原授权Coverage WorkItem现in_progress并接续；父Change不关闭。最新用户整体提交推送授权由原Leader统一集成，本聊天不自行提交/宽暂存；最终提供精确文件/差异/必要未跟踪依赖清单，资产GC/任务删除/迁移不随产品接续扩入。

## Coverage审查处置中（2026-10-26）
初版20文件候选的公开回归为Host225/225、Dsh19/19、原报告Python9/9；独立两轴发现RunId缺失校验、附件大小写歧义、公开GeneratorPath可替换唯一生成器。正最小修复，candidate-v1.json/patch与review-*-v1.md保留；新增coverage-review-red-v1两项真实失败已封存，最新candidate将在修复与复验后更新，暂勿将初版作为最终。原Leader已发现联合HEAD中SettingsCoverageScenarioTests.cs需只加partial关键字；此一行是本片新增partial报告测试的必要既有counterpart，集成时精确加入，不带其余并行fixture/断言。共享源码不额外改该文件。

## Coverage接续实际完成（2026-10-26，本聊天最终）
原Runner直接写报告的窗口已接入既有RequiredReportDelivery：启动前接受/绑定；原Ledger的report/steps唯一JSON与按用途声明的PNG/XML固定；同一六态跨重启查询与仅报告重试，固定后原目录/生成器丢失仍可恢复交付，私有输入损坏明确失败。Host/Dsh/HTTP与路径/来源保护贯通；交付故障独立于Runtime结果。四元组：method=真实确定性Runner、原报告兼容、附件/摘要/路径/进程关闭与四真实Kill窗口；expected=原字节/原结果保持、重试不重跑、必要来源不补造；actual=Host233/233、Dsh19/19、Python9/9、独立双轴v2各0；evidence=evidence/coverage-delivery/README.md、candidate.json/patch（最终22项）、coverage-host-final-v2-result.json、coverage-dsh-final-v1-result.json、review-*-v2.md、verification.json、finalization.json。旧RED与所有中间失败保留，不改原summary，受控fixture自清理，共享构建缓存不删，task-retained实际资产全部保留。
初版审查缺口已处置：删除任意GeneratorPath公开替换；Kernel RunId三处校验并拒绝未固定材料身份替换；大小写歧义接受/发现明确失败。candidate-v1与初审留存。此前“审查处置中”是历史状态，已由本节替代。
WI World/Coverage均done，父仍open。总集成入口为evidence/coverage-delivery/integration-manifest.json：World13项均按当前或Coverage进入前Program副本核对SHA；WorldStorageProbe需带before/WorldRevisionDeltaCodec.cs编译依赖；Coverage22项、Runner精确patch、既有ScenarioTests仅partial关键字补丁、资产31项与LIFECYCLE/LAYOUT/AUTOMATION状态证据/LOCAL skill/nav/schema清单均列明。RuntimeToolHost/Program/Runner是共享混合文件，按具体slice差异合入，勿宽暂存。资产外部rawRun与本机config/bindings保留原处，不作Git发布文件；锁和缓存排除。本聊天未动真实index、未提交推送；原Leader按最新整体授权统一总验收/精确提交，无需本聊天额外回发或自动监控。
