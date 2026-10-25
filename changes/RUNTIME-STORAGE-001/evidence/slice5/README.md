# B组：资源账目、P25咨询与可靠执行原生异步

## 做了什么
`KernelRunDriver.Resources.cs`现在经`IAsyncRunResourceJournal`等待initialize/begin/confirm及checkpoint；`KernelRunDriver.cs`经`IAsyncConsultationJournal`等待reserve/complete/reject。原外部调用前记录、结果确认、次数/幂等/fingerprint规则不变。`EffectBoundary.DispatchAsync`经`IAsyncReliableExecutionSource`等待prepare，确认前零投递；`UniKernel.ActAsync`与Driver实际await。原同步接口保留显式桥或兼容分支，旧同步P25异常行为不变。

三个文件格式分别保留：资源v2/v3/hash/head及P25/exec源前缀，咨询JSONL和原多态codec，执行大端长度帧与原重放/故障范围。资源源前缀读取/hash也原生async。咨询旧Store先更新内存再写盘，现写/flush确认后才发布原条目。正式HostRunner与SettingsCoverageRunner异步关闭三来源，等待已接受写入。

`FileExecutionJournal`只把同一次实际delivery返回后的submission＋receipt原两帧合批；prepare仍单条独立确认。最大2帧、默认16MiB整批、收集窗口0；无额外线程/队列。所有写入口串行门拒绝重入，接受后取消先结算。执行结果保存故障保留真实Receipt并阻断后续delivery，不把已发送变成未发送。

## 四元组
| Acceptance | method | expected | actual | evidence |
|---|---|---|---|---|
| E1 | 公开Driver延迟资源三阶段、前/后提交故障、取消、重入 | 确认前零下一外部操作，不阻塞调用方，失败不盲重试 | 新三阶段等待、六故障及两取消通过；initialize未确认不发布ResourceBinding，Begin未确认不咨询 | `kernel-final.trx`，`candidate-source-v1/tests/UniClaw.Kernel.Tests/Runtime/StorageBarrierAsyncTests.cs` |
| E2 | 公开Driver延迟P25三写入口、前/后故障与取消；原幂等/回放/异常测试 | reserve确认后才咨询；结果确认后才使用，不重建新correlation | 三等待、六故障、三取消通过；故障后不再写/咨询。原同步InterruptedP25异常及未决账目保持 | `kernel-final.trx`、`host-final.trx` |
| E3 | EffectBoundary三等待、三值、取消/结果故障；真实Driver准备等待取消RED→GREEN | 未成功prepare零delivery，结果保存失败不吞Receipt，不重入/盲补发 | 新35个Kernel用例全通过。Driver取消测试修复前delivery1，传递原token贯通Driver→grounding→Act→Dispatch后delivery0；已实际投递仍保留同一个Receipt | `driver-cancel-red-v2.trx`、`barriers-final-contracts.trx`、`kernel-final.trx` |
| E4 | 三真实文件、正常关闭/取消/配额、执行反馈/关联旧读；真实Driver计数与4处Kill/各2新reader | 原格式/记录/身份/SHA保持；真实批次少写/flush；接受写入正常关闭结算 | 新Host13例全通过；原3帧、1delivery/1receipt、1492字节保持，写/flush/批次3→2，峰值1→2；四Kill八reader原记录及SHA不变 | `host-final.trx`、`host-final/kill/` |
| E5 | 固定候选/源码/程序集/回归、前片封存、双轴review、独立核验 | 新行为全通过、无新增回归，不混入并发修改 | Kernel1284/1287、Host343/344、DSH24/24，同3+1旧失败；646保护输入保持、663实际输入/26程序集绑定，前片342封存不变；双轴/verify见最终报告 | `entry.json`、`candidate-verification-binding-v1.json`、`test-summary.json`、`review-*-v1.md`、`verification.json` |

## 真实I/O与Kill
最终只选`kernel-final.trx`、`host-final.trx`、`dsh-final.trx`及`host-final/`，不选中间`native-v3/`或中间TRX。计数为真实managed调用，不是物理磁盘syscall、延迟或掉电承诺。

同一实际Driver场景，single将结果两帧逐条确认，batch共写；完整Attempt及两个Entry逐字段相等。原prepare/frame1、submission/frame2、receipt/frame3都保留，大端长度头和body在原帧内编码，没有另造恢复格式。两模式都是1delivery、1receipt、1492编码字节；frame write/flush/batch各3→2，批峰值1→2，最大961字节。

四个非terminal Driver提交后、Owner ack前真实Kill窗口：resource为consult Begin已保存且咨询0；consultation为Pending已保存且咨询0；prepare为原Attempt已保存且delivery0；result为两结果帧已保存且真实delivery/Owner receipt各1。等待中NextInput均仅调用1次，无后续推进。每处两个全新reader从原文件还原Run只读、资源原行、咨询原条目和执行原Attempt；原源SHA不变，无续跑许可。最终ready/expected/kill-observed/reader-0/reader-1/source-hashes均在对应子目录。

## 基线与失败尝试
入口660构建输入逐项等于组A最终660输入（无外部漂移）；原891文件/资源归档`baseline-entry-source.tar.gz`，核对660输入SHA。最终20候选路径中14为已有构建输入、3为新C#文件、3为README；646非本组输入保持，实际663输入固定。Kernel/Host旧失败复用前片同源码入口基线；Host新增选择的资源/咨询相关旧用例也全部通过。

Kernel旧失败仍为ClaimEvolution链顺序、RunTrace的origin断言及README登记；本组登记Resources文件后从6处旧缺项减为5处，剩余文件未修改。Host旧失败为WorldOperationReplay随机OwnerId参与整份JSON比较。没有把跳过/未运行计作通过。

保留失败：`resource-red.log`缺async接口；`resource-green.log`首次漏将带资源等待的Consult方法改async；`consult-red.log`同编译前置失败，不冒称独立行为RED。`resource-consult-green.trx`六例是fixture缺ContractRef未admit，补原要求后v2六例通过。`execution-red.log`缺async接口。`native-group-green.trx`两失败是新异常包装改变旧同步InterruptedP25行为，兼容分支恢复旧行为；v2仅执行字节限额异常未转原Failure，补同类处理后v3全部通过。`driver-cancel-red.log`fixture漏命名空间；`driver-cancel-red-v2.trx`才是实际delivery1的行为RED。最终统一回归在修复后实际运行，没有重复不变全量测试。首次`verify-final.log`比较.NET大写SHA与Python小写SHA时误报不匹配，统一大小写后`verify-final-v2.log`exit0；没有改产品、原文件或重跑功能测试。

## 限定与后续
本组完成运行期资源/P25/执行三域，连同前片为八领域；将身份归档计入总路径为8/9。archive.Initialize、全域初始化/Host普通产物/剩余completion/cancel回流及最终性能验收仍在C组，父change保持implementing。资源与咨询的因果单条确认保持，本组不承诺它们的flush次数减少；本组批次收益仅来自实际执行结果对。路径优化/恢复游标/自动续跑仍不在本组。

`verify.py`核对候选/输入/归档/前片封存、最终TRX、实际Kill/原读/计数及review摘要；`reproduce.py <全新空目录>`从入口原归档＋候选快照还原并顺序跑最终同选择测试。工具已语法检查，原回归/真实Kill已经执行，不为工具再跑一遍功能测试。
