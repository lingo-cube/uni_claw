# Group A：Control＋Runtime Plan异步与合法双记录批次

## 做了什么
原两类来源均同步保存，且有相同的frame/hash/head文件代码。现Control所有外层操作与初始化、Plan初始化/接替begin/采纳沿原Owner→Kernel/Driver→两个正式Host真实await；共同文件机制收拢到Host内部`FileCommittedHistory<T>`，保留两个领域adapter、原schema/严格读取选项/身份/序列/hash/head/EOF和刷盘请求。同步兼容入口及旧journal保留。

最终候选为`candidate-inputs-v2.json`的20路径；v1及其评审均保留。接受前取消不写，接受后等原确认；保存失败/ack丢失锁存、不盲重试。批次只覆盖Control配置规则→启动之间无外部调用的一对记录：支持时共同确认后发布，不支持时保留旧逐条确认与失败语义。最大2条、编码frame及整批16MiB默认可配字节上限、收集/准入窗口0，无专用线程、队列或跨Owner事务。Plan replacement-begin→Control abandon→Plan adopt仍三次独立确认。

## 四元组
| Acceptance | method | expected | actual | evidence |
|---|---|---|---|---|
| D1 | 公开Kernel/Driver/ControlLoop及延迟journal；初始化、外层操作、双记录批次、取消/失败 | 未ack不发布/交工作/事件，同Owner拒绝重入，失败不重试，接受后取消结算 | 新Kernel26例全通过；批次等待时Owner位1/原源3行、工作0；确认后才发布配置与启动，取消后不再select | 最终`kernel-final-v2.trx`，中间`batch-contracts.trx` |
| D2 | 公开Driver分别延迟接替begin/Control abandon/adopt并取消，弃置失败前/后对照 | 各阶段按原顺序确认，旧Plan在新采纳前保持，失败不回滚原提交或重试 | 三阶段各自等待/取消通过；Control弃置失败时原Plan保持、begin未闭合、新adopt不发生，旧逐条journal回归保持 | `kernel-final-v2.trx`、`candidate-source-v2/src/UniClaw.Kernel/Runtime/` |
| D3 | 两真实文件adapter、公开Driver同场景mask批次能力作逐条基线，计数/关闭/取消/字节及条数上限 | 保留原行序/结果且真实managed写/flush减少，原格式/严格度及刷盘请求不变 | 新Host10例全通过；同场景10条Control原行、完成operation1，frame/head写入及flush各10→9，局部配置＋启动2→1；Plan始终2→2；条数超限不毒化，整批字节超限写前失败锁存 | `host-final-v2.trx`、`host-final-v2/kill/measurement/{single,batch}.json` |
| D4 | 真实非terminal Driver在Control select和Plan adopt原文件提交后Owner ack前各Kill，再各启动两个新reader | 未确认不调用handler/Effect；原完整行和源SHA读回，不获执行许可 | Control Owner4/源5条；Plan Owner1/源2条且尚未发布Plan；两处均operation0/handler0。四reader均读回原Control＋Plan行、SHA不变；Control窗口计数峰值2证明确实走原生批次 | 最终`host-final-v2/kill/{control,plan}/`的ready/expected/kill-observed/reader-0/reader-1/source-hashes |
| D5 | 固定20候选/656入口、前片122封存、明确并发输入、最终TRX/26程序集/660实际构建输入绑定；一组双轴review和verify | 新行为通过、旧失败按相同构建面归属、不归入其他任务代码 | 最终Kernel1249/1252、Host292/293、DSH24/24，同3+1旧失败；新26＋10共36例全通过。642非本片输入不变，1个外部DSH测试单列；前片122封存不变，Standards/Spec v2各0，verify通过 | `entry.json`、`concurrent-inputs-v1.json`、`candidate-verification-binding-v2.json`、`test-summary-v2.json`、`review-summary.json`、`verification.json` |

## M1与最终选择
Standards v1指出只有逐条async、缺组内真实批次收益。找到现有Driver的配置→启动buyer后，按D3补可选原生双行批次、原Owner共同确认及managed调用统计；没有延后外部调用前的屏障。`batch-red.log`记录缺CanAppendBatch的RED，`batch-green.trx`和最终用例证明真实I/O计数；Standards/Spec v2各0。`candidate-v1-v2.patch`单列增量，原v1候选、报告及测试不覆盖。

最终只选择`kernel-final-v2.trx`、`host-final-v2.trx`、`dsh-final-v2.trx`及`host-final-v2/`。旧`*-final.trx`、`v1-*.trx`、`host-final/`和其他中间导出不是最终结果。Kernel原失败为演进顺序、origin断言、旧README登记；登记从入口7处减少为6处，仅补本片所改KernelRunDriver.PlanHistory。Host原失败为随机OwnerId参与整份JSON比较。两组失败名与入口同源Slice 3最终TRX一致，未将未运行/跳过算通过。

## 来源与并发
656入口构建输入与Slice 3最终源码逐一SHA相同；原887文件/资源归档在`baseline-entry-source.tar.gz`，656输入从归档再次验SHA。Kernel/Host复用前片同源已封存最终TRX作旧失败基线，不再跑同一旧源码。

固定候选首次检查发现其他任务修改了`tests/UniClaw.Host.Dsh.Tests/SettingsCoveragePopupTaskSimulationTests.cs`，原断言拒绝把它当作本片未变输入。保留原/新SHA、补丁和新源码快照（`concurrent-inputs-v1.json`、`concurrent-input-v1.patch`、`concurrent-source-v1/`），不覆盖、不列入20路径candidate。它不是Kernel/Host测试项目依赖，也不匹配所跑DSH两类选择；DSH以该明确快照构建验证。实际660输入与最终程序集绑定，不声称所有非本片文件未变化。

## 失败尝试
`control-red.log`/`plan-red.log`缺async接口、`files-red.log`缺两文件异步关闭为预期RED。`group-contracts.log`首轮fixture漏required输入；v2的3个替换用例漏handler绑定前提，未进入目标等待而超时，补原公开绑定后17例通过，加入取消/失败覆盖后22例通过；未放宽产品校验。`files-kill.log`的probe局部变量重名修正；`files-contracts.trx`首轮8通过/1失败是fixture在Run保存未完成、Control尚未接受前就关闭Control，改为先经公开Run admission，再对实际已接受的Control初始化关闭，9例通过。M1补批次后新增4个Owner用例及1个真实I/O场景（含边界），最终36例通过。

`verify-final-v2.log`首次工具版本替换误将Slice 3基线也改成不存在的v2文件；修正为原基线v1后`verify-final-v2b.log`exit0，未改产品或重复功能回归。

## 限定与后续
计数是实际managed frame/head调用，不是物理磁盘syscall或延迟/掉电保证。原World增量场景仍128观测/257操作/128修订、7,951,934→1,107,547字节。批次不合并语义记录，不合并Plan三个接替阶段，不引入续跑或新记录Owner。

父change继续implementing，主要运行期存储完成5/9。剩余按两组推进：B资源＋咨询＋可靠执行；C身份归档、全域初始化及关闭/产物/剩余回流和最终验收。原路径优化及恢复游标/接收协议不在本组。

`verify.py`检查当前v2候选、保护范围、并发输入、原封存/基线、最终测试/计数/Kill和绑定；`reproduce.py <全新空目录>`从原归档＋v2候选＋明确并发快照还原载体，顺序运行同选择三组测试并另导出新证据。两工具已语法检查，原始回归均实际运行；不为工具再跑一轮。v1工具另存，后续改动使用新候选，原证据保持。
