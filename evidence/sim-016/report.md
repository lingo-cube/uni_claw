# 弹窗录制提取与故障注入交付（SIM-016）

已接通“原录制→校验提取→显式注入→真实Host/Runtime处理→结果定位”。本轮接入完成；正常跨包页面误判为弹窗的语义问题按用户裁决保留，未宣称修复。

原来的弹窗分类埋在LiveFeed内，离线买方不能直接复用；录制也缺少独立的帧/动作关联、注入记录和原证据与受控结果的区分。现在`src/UniClaw.Host/SettingsPopupClassifier.cs`提取原逻辑，`SettingsTraversalLiveFeed.cs`直接调用同一公开实现；包名不等或资源白名单命中规则保持。没有新增Capability、L1协议或产品仿真分支。

看调用链先打开`tests/UniClaw.Host.Tests/PopupReplay/RecordedPopupEpisodes.cs`、`RecordedPopupReplayTests.cs`和`RecordedPopupRuntimeReplayTests.cs`：提取器核对trace/steps/XML/PNG的SHA、capture文件绑定、原序、触发动作postCapture及分辨率，保留原cycle/context；故障只替换供给或交付响应。Runtime测试经既有`SettingsCoverageRunner.Run`的FeedNext/CurrentCaptureId/IEffectDriver，进入真实Host解析、Director、Ledger/World、Kernel与EffectBoundary。

来源为2026-10-14 SIM弹窗4帧和2026-10-20 Default apps样本2帧，共16份原始资产。Default apps的正常页预期来自独立curated oracle；原XML/route不是原系统窗口栈无覆盖的证明。公开Host红例已保留，当前输出仍为present，导出`KNOWN_FAILURE_UNRESOLVED_SIM017`。此处测试通过仅表示错误可重现。

| 验证内容 | 实际行为与结果 | 证据 |
|---|---|---|
| 原序提取与缺XML一次 | 两份录制前缀按原序复现；缺XML不产分类声明，恢复同输入后可分类；完整原CANCEL闭环请求被拒绝 | host-full.trx、actual/popup-extracted-*.json、actual/popup-missing-xml.json |
| 受控关闭后继续 | 1次交付，后观察absent，非空Root到达要求满足，Completed；2次咨询 | actual/popup-runtime-clears.json |
| 持续弹窗注入 | 关闭后实际输入被替换为原弹窗，2次交付后TerminalNotProven/BoundedStop；3次咨询 | actual/popup-runtime-held.json |
| 交付失败注入 | 1次DeliveryFailed后UnconfirmedDelivery；1次咨询，没有盲重发 | actual/popup-runtime-failed.json |
| 故障与独立预期敏感性 | 禁用hold会错误Completed并被检出；错误oracle、错trace关联、交换XML资产各触发预期失败 | mutation-results.json、四份变异TRX |
| 最终全量 | 八工程2109/2109；Simulation314、Host424；41/41场景，20 certified、21 descriptive | full.txt、module-results.json、simulation-full.trx、host-full.trx |
| 场景真实承载 | 新4场景的6个完整测试名，从Host二进制Trait精确关联本次Host TRX；两个工程各执行一次 | popup-discovery.json、full.txt |
| 工具回归 | 18/18；覆盖双根、完整参数名、缺结果、重复名、Host新鲜度、生成文件排除及CLI成功结尾 | tool-tests.txt |
| 来源/构成/隔离 | 1910保护项中1903未变，7项为必要接线/执法修改；旧37场景与SIM-014的70 pins不变；新156 pins匹配；执行源码零漂移 | protection-check.json、source-provenance.json、final-snapshot.json |
| Runtime构成 | 隔离Debug构建的7份Kernel DLL一致；popup导出不内嵌DLL hash，不作发布级qualification声明 | runtime-artifact-check.json |

首次接线直接让Simulation工程引用Host，违反既有组合根约束，full同时发现Host新文件未在README登记。已把Host专项和fixture移入Host.Tests，恢复Simulation恰好Kernel+Agent两引用；仅Compile链接同一测试侧内容门。跨根承载完整性由coverage对二进制Trait/TRX统一执法，C#保留各程序集的反向Trait有效性检查，缺承载/缺结果仍不能通过。原架构执法测试没有修改。第二次full行为与场景关联均通过，仅报告结尾旧变量导致NameError；已修并补CLI回归。两次失败保留attempt1-*、attempt2-*，最终full全部通过，原预期/认证摘要未改。

Spec来源措辞问题已修；Standards、capture绑定及跨根真值链增量评审无遗留阻断项，见review-disposition.json与review-*.md。SIM-017窗口证据与unknown生命周期前置设计已完成并复核，父任务已等待交付；它仍是候选设计，未实施、未冻结新协议。

原录制缺独立pre-action关联、逐帧原墙钟、CANCEL动作/回执及关闭后帧。成功分支明确使用受控Agent/交付/身份/路由/时钟及post-CANCEL布局拼接；实际receipt由本次EffectBoundary生成，原命令只作文本来源，不执行。不能据此证明原设备已完成关闭、原模型行为、完整原Run或整周录制穷尽。缺当前证据时旧present可能延续的问题也未在本轮修复。

下一步按`changes/SIM-017/design.md`选择可用窗口证据来源、足以支持absent的证据和unknown预算Owner，再开实施Change；原分类误判须以保留红例和独立oracle验证修复，不能回写历史trace掩盖失败。本轮未启设备、真实模型、提交或推送；大模型生成场景也未在本轮执行。

复现：`python3 evidence/sim-016/make-snapshot.py`后运行`python3 evidence/sim-016/run-full.py`；工具专项为`python3 -m unittest discover -s tools/tests`。独立覆盖入口须同时提供Simulation与Host本次TRX，例如`python3 tools/scenario-coverage.py --trx tests/UniClaw.Simulation.Tests/TestResults/sim006-full.trx --trx tests/UniClaw.Host.Tests/TestResults/sim006-full-host.trx`。输入与构成重签必须经致因Change，测试无自动接受新hash或改oracle路径。
