# C组与父任务完成：身份归档、初始化、关闭与最终验收

## 实际修改
`IRunRecoveryArchive`新增原生异步派生缝，Driver在原身份确认后才发布ResourceBinding或咨询。文件保留原schema/hash/一次创建与只读恢复，接受后取消结算、故障锁存。归档是一次写入，不为它预造队列或跨Owner事务。

`FileEvidenceJournal.CreateAsync`和`FileWorldJournal.CreateAsync`确认原seq0 marker/head；`WorldModel.CreateAsync`经既有World存储缝确认原initialize后才返回Owner。正式两Host显式等待，旧同步构造与历史读取保留。新资源文件不再同步读取空流。

Driver的completion verifier/adjudicator、cancel回流改为原`ProcessBatchAsync`；同次completion多义务的原记录共同确认后再供World使用。裁决恢复与Drive共用串行门，未ack不发布终局，原PostActionSelf不能凭自报建立MaterialEffect证明。两Host直接输出文件及原诊断观察者都等待异步写出；Trace的Finalize仍是内存操作。初始化异常进入原失败记录/关闭路径。

统一回归发现同步桥可能依赖调用方的消息循环。暂停SynchronizationContext的公开Runner测试实际RED后，为Driver和两Host的await显式添加ConfigureAwait(false)；没有Task.Run包装存储。SettingsCoverageRunner的完整离线流程和HostRunner.RunOnce的原启动失败路径都实测不依赖调用方消息循环。后者在真实文件/World Owner初始化后、创建delivery或拉取感知前因缺DeviceId停止，验证failure.json和关闭，不调用设备或模型。

## 最终证据选择
最终候选是`candidate-inputs-v3.json`的23路径。最终测试为`kernel-final-v2.trx`、`host-final-v3.trx`、`dsh-final-v2.trx`，实际导出选择`host-final-v3/`。v3仅新增Host.Tests的一个独立case；Kernel/DSH源码、运行时和测试程序集逐SHA不变，复用v2结果；Host全相关选择重新运行。该复用关系在`candidate-verification-binding-v3.json`明示，不冒称三者均v3重跑。

Kernel **1297/1300**、Host **356/357**、DSH **24/24**；本组新增Kernel **13/13**、Host **8/8**，共21例通过。未运行或跳过未计作通过。旧失败名称与同源入口Slice 5相同：ClaimEvolution顺序、RunTrace的origin断言、README登记，以及WorldOperationReplay随机OwnerId整份JSON比较。本组补齐所改RunResourceRecovery登记，原README缺项5→4；剩余文件未修改。

## 本组四元组
| Acceptance | method | expected | actual | evidence |
|---|---|---|---|---|
| F1 | 原Archive/Driver延迟、前后故障、取消，真实提交后ack前Kill＋两个新reader | 无提前绑定/咨询/投递，失败不重试，原身份/历史/SHA保持 | 等待时绑定空、咨询/观察/投递均0；归档已确认文件1批。两个新进程原身份逐字段相等、Run只读、共同历史一致、源SHA保持，无续跑许可 | `kernel-final-v2.trx`、`host-final-v3/closure/archive/` |
| F2 | 两原生文件factory旧读/预取消，World Owner factory延迟/取消/故障 | 原marker与initialize确认后才返回；旧格式保持 | 文件marker/head可即时重开读取；预取消不创建来源；Owner未ack不返回，接受后取消先结算，故障无可用Owner | `kernel-final-v2.trx`、`host-final-v3.trx` |
| F3 | completion/cancel回流RED→GREEN、故障与裁决重入 | 未确认不终局，不绕Drive门，自报不产生MaterialEffect证明 | 原同步回流两例RED，改async后等待未完成；completion两条一批，cancel一条；裁决与Drive拒绝重入，故障阻断、不重试，MaterialEffect仍TerminalNotProven | `reflux-red.trx`、`kernel-final-v2.trx` |
| F4 | 两Runner同步桥/产物关闭、九原生writer计数、已有合法批次/增量对照 | 原来源及输出确认，实际计数可核对，等待/峰值有量纲 | 两同步桥离线回归通过；九来源23语义记录＋2 marker、24 frame写/flush、19 head写/flush，真实1delivery/1receipt；三合法批次仍减I/O，World增量字节保持 | `host-final-v3.trx`、下表及`host-final-v3/` |
| F5 | 23候选/667实际输入/26程序集、各片封存、回归/两轴/核验 | 新行为全通过，无新增失败，不覆盖并行代码，父验收全部有据 | 21新例全通过，同旧3+1失败；644保护输入及3并行输入明确绑定；此前各片/World增量封存逐SHA不变；最终两轴0、verify exit0 | `verification.json`、`review-summary.json`、`candidate-verification-binding-v3.json` |

## 九条生产接线
| 领域 | 核心确认缝 | 正式Host实现/初始化 | 运行期屏障 |
|---|---|---|---|
| Run | IAsyncRunStateJournal | FileRunStateJournal，AdmitContractAsync首次保存 | View/RunId/状态/终局确认后发布 |
| Evidence | IAsyncEvidenceJournal | FileEvidenceJournal.CreateAsync＋EvidenceLedger | 批量确认后才供World消费 |
| World | IAsyncWorldJournal | FileWorldJournal.CreateAsync＋WorldModel.CreateAsync | 原initialize及八操作确认后发布 |
| Control | IAsyncControlTraversalJournal | FileControlTraversalJournal，Kernel admission初始化 | select/drain确认前不交工作/事件 |
| Runtime Plan | IAsyncRuntimePlanJournal | FileRuntimePlanJournal，Driver首次等待 | begin→Control abandon→adopt分别确认 |
| 资源 | IAsyncRunResourceJournal | FileRunResourceJournal，Driver首次等待 | begin先保存，confirm保留原事实 |
| P25 | IAsyncConsultationJournal | FileConsultationJournal | reserve先保存，结果先确认再消费 |
| 执行 | IAsyncReliableExecutionSource | FileExecutionJournal | prepare成功后才delivery；结果失败保留真实Receipt |
| 身份 | IAsyncRunRecoveryArchive | FileRunRecoveryArchive | 原身份确认后才绑定或进行外部操作 |

两个正式Runner等待上述来源及关闭；异常同样结算已接受写入。没有跨操作收集/准入队列：窗口0、同Owner最多一条在途，原调用者等待未完成Task实现背压；非法重入在接受前拒绝，不是接受后丢弃。新介质实现领域缝，核心不依赖文件或通用存储平台。旧同步API/历史读取保留兼容；文件打开/rename等平台元数据操作及序列化仍同步，不声称整个运行时零阻塞。

## 性能与数量
同一真实Driver场景，1delivery/1receipt，停在原WaitingForInput屏障。下表“记录”不含seq0 marker，“批次”只含语义记录提交；frame/head两列各自的write和flush次数相等。

| 领域 | 原记录 | 已确认批次 | frame write/flush | head write/flush | 峰值批记录 | 峰值frame字节 |
|---|---:|---:|---:|---:|---:|---:|
| Run | 3 | 3 | 3 | 3 | 1 | 1769 |
| Evidence | 2 | 2 | 3 | 3 | 1 | 2473 |
| World | 4 | 4 | 5 | 5 | 1 | 5722 |
| Control | 1 | 1 | 1 | 1 | 1 | 2011 |
| Plan | 1 | 1 | 1 | 1 | 1 | 1547 |
| 资源 | 6 | 6 | 6 | 6 | 1 | 1885 |
| P25 | 2 | 2 | 2 | 0 | 1 | 639 |
| 归档 | 1 | 1 | 1 | 0 | 1 | 2534 |
| 执行 | 3 | 2 | 2 | 0 | 2 | 952 |

等待实测、三段初始化时间、Driver总经过时间见`closure/performance/performance.json`；总时间包含CPU与等待I/O，不是纯磁盘耗时或加速比。来源计数是真实managed调用，不是物理syscall或掉电证明。

最终同选择同时复核三条真实批次收益：Evidence 64原记录保存，含初始化frame/head各65→2，扣除marker为64→1，编码字节保持132978；Control原10行/完成operation1，frame/head各10→9，配置/启动局部2→1，Plan仍2→2；执行原3帧/1delivery/1receipt保持，write/flush各3→2。不能跨Agent或Effect屏障凑批，其他因果单条不承诺少flush。World同128观测/257操作/128修订，7,951,934→1,107,547字节，减少86.07%，属于既有增量编码收益。

## 父Acceptance映射
| 父项 | method | expected | actual | evidence |
|---|---|---|---|---|
| A1 | 九条正式接线、各Owner延迟/重入/取消与单写门，初始化/普通输出/关闭回归 | 核心依赖领域抽象，真实async确认，不入队即成功或丢记录 | 九领域实现原生async；各自await后发布，接受后结算；无跨操作队列/专用线程 | 各片原封存＋最终TRX、上方接线表 |
| A2 | 原Evidence mixed admission/lineage/duplicate/rejected、真实文件64条对照 | 原顺序/结果保持，真实write/flush更少，确认后供World消费 | 原64条/字节保持，扣marker写/flush64→1；部分提交/失head不发布整个候选 | `host-final-v3/evidence/`、最终EvidenceBatch用例 |
| A3 | 各片原Owner/Driver前后故障、三值、取消/关闭、实际Kill/新reader；最终回归 | 保留全部确认屏障，prepare未确认零delivery，无盲重试 | Run/Evidence/World/Control/Plan/资源/P25/执行/身份确认与故障通过；本片归档Kill完成，既有实际Kill在最终Host选择重跑 | 各片封存、最终TRX及`host-final-v3/` |
| A4 | 九来源原格式读回、真实计数/经过时间/峰值、双轴review和verify | 全范围完成，不以接口存在当完成 | 两正式Host异步接线/关闭/产物通过；九源计数、三合法批次、旧读取及Source绑定成立；最终两轴0、核验通过 | 本文、`review-*-v3.md`、`verification.json` |

## 失败与并发记录
保留`archive-red.log`/`factories-red.log`/`world-owner-red.log`缺接口或factory的RED、`reflux-red.trx`两行为RED。`host-closure-green.log`首次给无token的HostRunner传入不存在的变量，仅修调用；`closure-files.log`首次probe局部run变量重名，仅改reader变量。后续行为均实际通过。

首次统一v1 Host回归被同步上下文阻塞，由本任务终止自己的probe/testhost；只完成26例的中止TRX不得当全量绿。暂停消息循环测试`sync-context-red.trx`复现，v2修复后完整Host356例通过。Spec v2 M1要求补另一Host同步桥，v3增加公开HostRunOnce失败/输出/关闭用例1/1，Host全相关选择357例重新运行；Kernel/DSH同源结果按SHA复用。各轮候选/报告/中止和失败日志不覆盖。

并行任务修改FiniteMenuGraphSimulationTests及Host.Tests.csproj，并新增RealAgent partial测试。三项在`concurrent-inputs-v1.json`与`concurrent-source-v1/`单列，不属于23候选或所选测试；原/新SHA、补丁保留，未回退。最终667输入绑定包含这些明确快照。入口原663输入逐项与前片最终源码一致，归档保留原基线；644其余输入保持。

## 复现与边界
`verify.py`核对候选/原归档/输入/程序集、所有相关原封存、最终TRX、实际计数/Kill/旧读和review摘要；`reproduce.py <全新空目录>`重建入口归档＋v3＋明确并行快照并顺序运行同选择。工具做语法核验，功能回归在原载体已经实际执行，不为工具再跑一次。

父RUNTIME-STORAGE-001的A1–A4已证明，完成并关闭。未改P24会话/激活控制面存储、恢复游标/自动续跑或路径优化；路径剪枝设计仍为此前单独记录的后续事项。本组没有真机或模型调用，不把离线I/O与进程Kill证据扩大为OS崩溃/断电或设备效果证明。
