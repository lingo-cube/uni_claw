# 异步批量存储实施顺序

目标是让调用者异步等待真正保存完成，并在合法批次中减少实际I/O。已有读写抽象解决“依赖谁”，尚未解决“如何异步与何时合批”。以ADR-0055为目标约束，旧同步实现继续如实保留当前语义，完成验证再切换。

实施进度（2026-10-25）：下文调用链和“当前接口”为原方案入口快照。Slice 1已完成Evidence观察批量及正式Driver/Host等待；Slice 2已完成World八操作原生async及文件/Kernel/Driver/Host等待，v2增量格式保持；Slice 3已完成Run核心状态及遍历事实原生async和正式等待/关闭链。独立证据在`evidence/slice1/`、`evidence/slice2/`、`evidence/slice3/`。Group A（Slice 4）已完成Control＋Plan异步及合法配置/启动批次，实际managed调用10→9（局部2→1）。Group B（Slice 5）资源/P25/执行已贯通原生async，实际执行结果两原帧合批，write/flush各3→2；八运行期领域已接线，将身份归档计入总路径为8/9。C组（Slice 6）也已完成：九路径原生接线、初始化/关闭、普通产物和完成/取消/裁决回流通过，父A1–A4四元组、两轴最终0及独立核验成立，本change已关闭。当前最终选择/计数/故障范围见evidence/slice6/README.md；下文保留原方案入口快照。

## 1. 当前调用链与第一条真实buyer

`KernelRunDriver.DriveAsync → DriveAsyncCore → DriveCore → UniKernel/各Owner → 同步journal → 文件Write/Flush → 发布Owner状态`。DriveAsync目前只在语言handler等待处让出，不能证明存储不阻塞。

观察入口一次包含多条proposal，Evidence admission是首个真实批量buyer：先得到有序候选admission/原结果，异步保存整批，再发布并让World消费。duplicate/rejected仍是原历史，不能丢。必须通过公开Owner/Driver证明行为等价，不能靠私有字段setter构造批量。World本身已在一次外层操作中把多项delta记录为一条历史，不再额外制造无意义记录。

## 2. 全部接入清单

| 存储 | 当前接口 / 实现 | 原确认约束 | 异步与批量迁移 |
|---|---|---|---|
| Run | IRunStateJournal / FileRunStateJournal | 完整原状态ack后发布 | Owner异步确认；不把跨Effect的Action/Cycle盲合并 |
| Evidence | IEvidenceJournal / FileEvidenceJournal | admission保存后才发布 | 观察内多admission先批量；保留原顺序/duplicate/rejected |
| World | IWorldJournal / FileWorldJournal | 外层完整delta提交后切换candidate | 原生async；已有外层delta批次不拆小 |
| Control | IControlTraversalJournal / FileControlTraversalJournal | select确认后才交出工作 | 异步候选提交；Select/Drain及Runtime接收仍分别记录 |
| Runtime Plan | IRuntimePlanJournal / FileRuntimePlanJournal | init/begin/adopt各自确认 | 异步采纳；begin→Control弃置→adopt仍是三个因果阶段 |
| 资源 | IRunResourceJournal / FileRunResourceJournal | begin先于外部操作，confirm在结果之后 | 原生async；不把前后屏障并成一个延后批次 |
| P25咨询 | IConsultationJournal / FileConsultationJournal | reserve先于Agent，complete/reject在结果之后 | async返回原结果；内存索引在相应确认后发布 |
| 执行 | IReliableExecutionSource / FileExecutionJournal | prepare成功后才投递；三值及未决保持 | async prepare/追加；取消/未知不能变成盲补发 |
| 原身份归档 | IRunRecoveryArchive / FileRunRecoveryArchive | 第一次外部操作前完整绑定来源 | 初始化异步；只写一次，不为凑batch新增重复归档 |
| 诊断Trace | IRunTrace / AsyncFileTraceWriter | 可关闭可丢，不是恢复来源 | 保持现有异步批量和故障隔离，不共用关键队列 |
| Host快照/报告 | 各Runner终局产物写入 | 不作原Owner恢复源 | 正常关闭等待异步输出；不将其完成等同日志提交 |

## 3. 每层只承担自己的职责

领域层提供有序不可变记录与提交确认缝，保留所有原身份、序列及Owner规则。异步await前保存候选，不提前发布；完成后由原Owner发布。恢复Reader仍读取停止writer后的原来源，不新建执行器。

Host adapter负责真正异步I/O、批次上限、背压、flush/head及原介质失败。批量格式须能识别完整提交末位；不许读到未确认尾部后静默截短认证共同点。首个buyer通过前不冻结公共通用WAL/事务接口，不替换旧文件格式后只保留内存往返证据。

Driver/Host负责await与串行推进。一个Owner待提交时不能进入第二个Drive或通过取消绕开保存屏障。旧同步入口若保留，必须明确是同步兼容入口；生产异步入口不能退回同步阻塞桥。初始化从构造期同步I/O迁移时也须显式完成后才进入首次外部操作。

## 4. 合批与不能合批

同一次观察内尚未对外发布、且可按原顺序共同确认的admission可合批。不同生产者的独立请求可以在明确顺序与源标识下合批，但现有Owner的单线程规则不因此改变。

`资源begin → Agent/Effect → confirm`、`P25 reserve → Agent → complete`、`Effect prepare → Deliver → receipt`及`Plan replacement-begin → Control abandon → adopt`不能通过延后前半段提交来获得批次。纯因果串行处可能仍只有单记录批次，应如实报告；异步等待的收益与磁盘次数减少分别衡量。

队列满时生产者异步等待，不丢记录；队列按数量和字节有界，批次按数量/字节/最长等待触发。具体阈值通过实际场景测试后选取。请求入队前取消可以不接受；接受后取消不能声称未写，需保留最终原确认或未知状态。写入失败后停止受影响的后续确认，不能跳过坏记录继续报告成功。关闭先停止接收，再等待已接受项，最后释放存储；超时保留未确认事实。

## 5. 可判定的实施顺序

1. 固定当前文件单条写入基线与公开I/O计数观察缝；区分managed flush和flush-to-disk，不能混算耐久性。没有批量收益的串行场景也进入基线。
2. 公开Evidence batch与故障契约RED：多admission顺序/结果等价、未确认前不可见、满队列等待、写前/批中/提交后ack丢失均不能错误发布。
3. 实现原生异步adapter与Evidence候选确认，证明多条记录少于逐条基线的真实write/flush次数；同一读取缝复读原历史。
4. 把UniKernel观察/Driver/Host等待链接通，原Effect前屏障与串行入口保持；通过公共入口实测等待期间调用者可继续，不以Task.Run或CompletedTask代替。
5. 按清单迁移其他领域及初始化，逐个保持原确认语义；尚未迁移者明确标记，不能宣称“全部已异步”。
6. 全链原场景、背压/取消/关闭、批中/确认丢失/Kill、两个新reader及源SHA。报告记录数、批次数、write/flush次数、等待、峰值积压和旧读取兼容。双轴review与独立四元组后关闭。

本方案的产品实现与性能结果以各实施片独立证据为准，不能将首两片结果推及全部存储。完整游标/Control接收、数据库、自动续跑和路径优化仍独立保留，不借性能改造重做Owner或恢复协议。
