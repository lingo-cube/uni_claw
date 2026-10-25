# Run运行期原生异步：Slice 3证据

## 修改与原行为
原Run的admission/cycle/action/terminal和遍历记录同步写文件，`DriveAsync`会卡在保存处。现沿`IAsyncRunStateJournal→RunModel→UniKernel→KernelRunDriver→两个正式Host`真实await；首次身份、后续进度和遍历事实确认后发布，等待时只读原状态，重入拒绝，失败锁存。文件frame和临时head采用原生WriteAsync/FlushAsync及WriteThrough，原子替换head后确认，正常关闭等待已接受记录。

Run保留记录权威，Kernel/Driver不取得记录Owner；同步兼容入口与async共用同一候选/确认实现，旧同步journal可用。默认16MiB可配编码frame上限，一条在途、零收集/准入窗口。原v2/v1读取、frame/hash/head/EOF与单writer进程终止故障范围保持。WriteThrough保留刷盘请求，依据[官方FileOptions](https://learn.microsoft.com/en-us/dotnet/api/system.io.fileoptions?view=net-10.0)与[.NET 10 Unix的O_SYNC映射](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/Microsoft/Win32/SafeHandles/SafeFileHandle.Unix.cs)；未把普通FlushAsync当Flush(true)。未新增掉电/目录元数据耐久性保证。

## 独立四元组
| Acceptance | method | expected | actual | evidence |
|---|---|---|---|---|
| C1 | 公开Run Owner与延迟/失败journal，原身份及typed记录 | 未ack不发布、单Owner拒绝重入、写前取消零写/接受后结算、写前失败或提交后ack丢失锁存 | 新Kernel18例包含admission、cycle/action/terminal、取消和两种失败；全部通过 | `kernel-final.trx`、`candidate-source-v1/src/UniClaw.Kernel/Run/` |
| C2 | Kernel非阻塞调用探针、Driver等待cycle/action/Running、terminal编排 | cycle未ack零Effect；action未ack不观察/咨询；terminal未ack无Outcome；Running未ack零handler，取消后写Cancelled | 非阻塞RED→GREEN；Driver普通/取消/失败，terminal等待及handler取消均通过 | `kernel-wait-red.trx`、`handler-cancel-red.trx`、`run-contracts-v3.trx`、最终`kernel-final.trx` |
| C3 | 原生文件公开接口，正常关闭、写前取消、字节上限及旧源回归 | async frame/flush/head后确认，关闭结算，过大写前失败锁存，v1/v2读取不变 | 新Host5例含C4全通过，扩大RunState/Crash/Traversal文件回归；两个正式Host await using Run源 | `host-final.trx`、`candidate-v1.patch` |
| C4 | 非terminal真实Driver，Run提交后Owner获ack前Kill，再启动两个reader | 未确认Running不调用handler；新reader读取原全部记录且不修改源，不获继续执行权 | Owner位5、源6条、已发布handler0/真实调用0、已有operation1；Kill非0，两个新进程各读原6条及Running历史，SHA不变 | 最终选择`host-final/kill/`的ready/kill-observed/expected/reader-0/reader-1/source-hashes |
| C5 | 固定17候选、653入口输入/基线归档、前片120封存、最终程序集/TRX绑定、双轴review与verify | 不改非本片输入及原封存，新行为通过，旧失败按相同源码归属 | 642非本片构建输入/前片120项不变；Kernel1223/1226、Host200/201、DSH24/24；同3+1旧失败与精确入口源码基线一致；两轴结论及最终验证见独立文件 | `entry.json`、`baseline-archive.json`、`test-summary.json`、`candidate-verification-binding.json`、`review-summary.json`、`verification.json` |

## 最终结果与基线选择
最终只选`kernel-final.trx`、`host-final.trx`、`dsh-final.trx`，绑定候选17个SHA及26个相关程序集SHA。新增18 Kernel＋5 Host共23例全部通过。Kernel原三个失败为演进顺序、origin断言、旧源码README登记；本片补齐所改RunModel.History/TraversalHistory的登记，使违规从入口9处降到7处，无新增登记违规。Host原失败是随机OwnerId参与整份JSON比较。Host本次扩大RunStateRecovery/Crash和Traversal文件选择，新增选择的旧测试通过。

入口653项构建输入与Slice 2最终候选及其受保护输入逐一SHA完全一致，因此复用前片封存的`../slice2/kernel-final.trx`与`../slice2/host-final.trx`作同源旧失败基线，不另跑同一源码。前片已实际跑过的3+1旧失败仍由原TRX保留。入口完整884文件在`baseline-entry-source.tar.gz`，653构建输入全部从归档再次验SHA；资源/fixtures一并保留，复现不依赖可变工作区。

## 失败尝试保留
`run-owner-red.log`缺IAsyncRunStateJournal、`kernel-wait-red.trx`同步桥TimeoutException、`file-async-red.log`缺IAsyncDisposable都是预期RED。`run-contracts.trx`首轮将后续阻断原因错误要求含首次异常子类名，原接口本就只返回稳定的失败类别；修正断言为持久失败且不重试，未改产品错误语义。随后v2 16/16、补handler后的v3 18/18。

实际Kill首次`file-contracts.trx`4通过/1超时：fixture的graph scope漏合同版本，与当前Run不关联，Driver从未进入目标提交。`kill-probe-diagnostic.trx`快速显示stale-or-unassociated-projection及后续同版本Plan拒绝；修正fixture为真实`RunView.Version`后`file-contracts-v2.trx`5/5，不放宽产品关联检查。中间`file-green/kill/`保留，最终只引用`host-final/kill/`。

新增异步等待暴露的取消窗口有`handler-cancel-red.trx`：原状态停留Running；共享`CancelPendingPlanLanguageCallAsync`先等待已接受Running确认，再记录Cancelled、关闭调用，处理器仍未调用，最终GREEN。保存异常不再被handler结果catch误分类或重记。

`verify-pre-review.log`首次核验把旧README错误消息要求完全相同，实际本片已减少两个所改Run文件的登记违规；`verify-pre-review-v2.log`又暴露沿用旧摘要的数量写错。直接核对两份原TRX为9→7后，核验改为严格子集且恰只减少本片两个Run文件，输出真实数量，不修改源码或重跑功能测试。

## 性能边界与剩余工作
本片单逻辑记录仍一条frame，一次frame/head确认，不声称额外合批或物理磁盘调用减少；非阻塞调用通过公开延迟确认及原生文件路径共同证明。原World增量场景仍128观测/257操作/128修订、7,951,934→1,107,547字节，减少86.07198%（`host-final/size/size.json`）。

父change保持implementing。构造/历史读取、Control/Runtime Plan/资源/P25/执行/归档、completion/cancel证据及部分Host产物仍有同步路径；无全域完成、实时设备/模型或续跑声明。下一工程动作是Control遍历日志原生async，保持选中工作保存后才交出，不借此重做恢复游标或Control/Runtime接收协议。

## 复核与复现
`verify.py`核对当前候选/保护范围/同源基线/封存/最终测试与真实Kill；`verification.json`保存结果。`reproduce.py <全新空目录>`从归档及17候选快照还原独立载体，顺序跑同选择三组测试，导出新Kill与size，保持本片原证据。两个工具已检查语法；原始功能回归已实际运行，没有为了工具再跑重复回归。后续代码变化需新候选与证据，不覆盖本片封存。
