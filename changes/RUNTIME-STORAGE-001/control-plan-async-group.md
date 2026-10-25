# Control＋Runtime Plan异步组（Slice 4 / Group A）
状态：COMPLETE（D1–D5，v2）；父change保持implementing。使用已授权的公开journal/ControlLoop/Kernel/Driver、正式文件adapter及原reader缝；不向用户重新索要常规许可。

## Scope / Decisions
IAsyncControlTraversalJournal、IAsyncRuntimePlanJournal扩展各自原接口。Control所有外层操作及初始化共用候选→await确认→发布；单Owner一条在途，等待时旧历史/状态可读，重入立即拒绝，失败包括ack丢失锁存。候选内部嵌套Gap/Pause仍合于原完整外层操作。Plan初始化/接替begin/采纳分别await，原Driver串行门覆盖全部等待；新Plan只在采纳确认后交给执行器。

两种原文件frame/hash/head形状相同，提取Host内部FileCommittedHistory<T>，仅两个实际adapter使用。保留各自schema、错误前缀、payload严格选项（Plan额外required constructor）、GUID/序列/hash/head/EOF规则和刷盘请求，Asynchronous＋WriteThrough/FlushAsync→原子head替换→ack。单次最多2条，编码frame及整批默认16MiB可配上限，零收集/准入窗口，不新增线程/队列/WAL/跨域事务。文件构造/历史读取及其他存储仍在后续范围。

REVIEW M1处置（v2）：配置规则与启动之间没有外部调用或工作交接，是合法双记录buyer。IAsyncControlTraversalJournal以可选CanAppendBatch声明整批确认；原不支持批次的journal仍逐条确认/发布，旧失败语义不变。Driver→Control.ConfigureAndStartAsync准备两条原行、等待整批确认后一次发布；文件一次frame WriteAsync/FlushAsync及head提交。保留原两行/序号/格式，不新造合并后的语义操作；原Plan initialize/接替begin/adopt仍各自一条、各自确认。

## Acceptance
D1 Control：初始化与所有外层操作异步；未ack不发布或交Work/Progress；同Owner同步/异步重入拒绝，失败前/提交后ack丢失锁存不重试，接受后取消先结算。公开Driver等待select时零handler/Effect/新观察或咨询，取消后不交出工作。
D2 Runtime Plan：初始化/采纳/接替begin真实await；未ack不发布新Plan或执行；replacement-begin确认→旧Control abandon确认→匹配adopt确认，分别保留原来源，失败/取消不盲重试或回滚已提交Owner。
D3 文件共享真实async写/flush/head及关闭等待，原两格式/严格度/读取和原刷盘请求不变；最多2条且总字节/单帧有界，写前取消/超限无写入。配置＋启动的公开Driver场景保留原行序与结果、实际managed frame/head写入及flush少于逐条基线；不将managed调用当物理磁盘syscall。两个正式Host await using两个来源，不以Task.Run包装存储；Plan跨Owner阶段不得凑批。
D4 实际非terminal Driver在Control select和Plan adopt各自文件提交后Owner ack前Kill，各两个新reader沿原源读取原行/状态及SHA，不授予继续执行。等待取消、写前失败、ack丢失保持零提前调用/投递。
D5 dirty入口656构建输入/20候选边界与前片122封存固定；非本片输入保持，其他任务的并发变化单列原/新SHA、快照和归属，不覆盖或归为本片。新行为用例及相关回归、与实际构建面匹配的旧失败归属、单组独立双轴review/四元组通过。组A通过不等于其余四类或全域完成。

并发输入核对：固定候选时发现`tests/UniClaw.Host.Dsh.Tests/SettingsCoveragePopupTaskSimulationTests.cs`在外部修改，未写入本片20路径diff。它不属于Kernel/Host测试的项目依赖，也不匹配本片DSH两类选择；原/新SHA、补丁及新源码保存在`evidence/slice4/concurrent-inputs-v1.json`及配套快照。Kernel/Host旧失败仍可对照完整同源入口，DSH以当前明确来源构建验证，不冒充所有非本片文件完全未变化。

## Plan
Control公开延迟确认RED→async Owner及Driver链→Plan同缝RED与等待/接替实现→两文件原生async/共享机制→失败取消与2个实际Kill/4个新reader→固定一组候选/最终回归/双轴review/验证封存。

## Verification / Result
level: DETERMINISTIC + SCENARIO
method: 公开Owner/Kernel/Driver延迟确认与真实文件；配置＋启动的实际Driver逐条/批次计数对照，双窗口Kill/四新reader；20候选SHA/660实际构建输入/26程序集、同源旧失败对照和独立双轴review/verify。
expected: D1–D5，父A1–A4保持未全域完成。
actual: 新Kernel26/26、Host10/10；最终Kernel1249/1252、Host292/293、DSH24/24，同3+1旧失败；原README7→6无新增。10条Control原记录及1个完成operation保持，frame/head write/flush各10→9，配置＋启动局部2→1，Plan仍2→2。Control Owner4/源5、Plan Owner1/源2，未调用handler/未交operation；四个新reader原记录/源SHA不变。642非本片输入保持、1外部DSH测试单列，前片122封存不变；v2两轴0，verify通过。主要运行期5/9，下一组B资源/P25/执行，随后C归档与全域初始化/收尾。
evidence: `evidence/slice4/README.md`、`candidate-inputs-v2.json`、`test-summary-v2.json`、`review-summary.json`、`verification.json`、`final-artifacts.json`（均在slice4）。
