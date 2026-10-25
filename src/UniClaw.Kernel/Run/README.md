# Run Model 代码索引（Product T2 · 基线 §13）

## 原生异步记录（RUNTIME-STORAGE-001 Slice 3）
`RunModel.History.cs`定义`IAsyncRunStateJournal`，成功表示原提交确认。`RunModel.cs`的admission/cycle/action/terminal与`RunModel.TraversalHistory.cs`的遍历记录共用单Owner门及候选→确认→发布；同步入口是同一实现的显式阻塞兼容桥，旧同步journal仍可使用。首次admission的View/RunId/State也只在确认后发布。等待时可读旧状态，写入重入立即拒绝；接受前取消不写，接受后结算真实确认，保存失败含ack丢失锁存且不重试。

Kernel/Driver只编排await，不能取得Run记录权威；Run不依赖Host或文件。文件构造/历史读取、Control/Plan/资源/P25/执行/归档的存储仍有同步路径，详见父change进度，不能据此宣称全域异步或可继续执行。

Sole **Canonical Run State Recording Authority**：Execution Contract
View、Objective State、Proof Obligation State、Progress State、Outcome
State。不拥有 WorldBelief、Control Intent 或 Outcome Proof judgment
（权威表见基线 §10）。

## 代码归属

| 文件 | 职责 |
|---|---|
| `RunModel.cs` | run state 记录权威本体（Target §13） |
| `ExecutionContract.cs` / `ExecutionContractView.cs` | 已接受 contract 的 typed 检查与 immutable canonical view（§13.2） |
| `RunState.cs` | Objective State：primary run 对 contract objective 的执行侧状态 |
| `RunObligation.cs` | Run-level Proof Obligation 六类（不变量 36） |
| `OutcomeState.cs` | Terminal Outcome State（terminal classification 记录） |
| `../Outcome/RuntimeOutcome.cs` | **本维度出口投影**（物理目录独立，语义归 Run Model）：UniKernel 只从 canonical Outcome State 发出的 immutable RuntimeOutcome（基线 §7） |

## 变更规则

run/contract 执行状态与义务语义进本目录；`Outcome/` 保持物理独立但
归属本维度——新增出口投影字段时两处同步审。run state 的事件化与投影
边界遵循 ADR-0036。

## 指向

- 基线 §13；primary run 自驱：ADR-0019；事件与投影边界：ADR-0036；
- 消费方：`../Runtime/README.md`（驱动面）、Host 投影。

## 原身份历史归档（RUNTIME-RECOVERY-003）

RunModel.Identity只投影实际accepted View、RunId及其原始来源。读取归档复用Owner的合同检查和content-derived RunId规则，返回只读历史View，不创建RunModel/RunState、不调用Admit/Activate、不判断旧Run当前active/terminal。Runtime关联其原始政策与资源录制源后才恢复只读binding。原合同修订链、Outcome历史和执行位置均未恢复；当前只支持单View。可信归档协议负责来源，校验不是防恶意文件替换的签名认证。

## 原状态历史持久化与只读恢复（RUNTIME-RECOVERY-004）

RunModel可注入IRunStateJournal；原无参构造保留。实际Admit/Cycle/Action/Terminal四类typed提交先写原历史，再发布内存State；故障锁存并阻断后续写入/外部动作。Outcome只冻结Assurance原判断和refs，不重新判断。身份v2及Outcome记录保留ordinal/ordinal-ignore-case集合语义；无法表示的自定义comparer明确失败，不猜测原合同或证据引用的查询规则。

RestoreReadOnly校验原身份、源RecordingId、连续合法transition及唯一terminal末尾，恢复Contract/Objective/Obligations/Progress/Outcome和原历史。恢复实例IsReadOnly=true，terminal不回active，非terminal恢复实例同样不可激活/执行，不能重新emission。它没有World/Control/Plan或Outcome emission台账，不承诺完整续跑。Progress记录不能代替资源或外部Effect事实；提交后ack未知时只读持久commit，不推断此前已emission。

原Owner的`HistoryPosition`只投影已提交历史RecordingId/Sequence；无持久源时为null，不提供恢复认证。Runtime资源记录引用此位置，原source及终态合法性仍由RunModel恢复验证；不能由资源Terminal标记创建或修改canonical Outcome。

## 原遍历执行事实（RUNTIME-RECOVERY-012）

五个既有记录入口现在将Initialized/Reset/Handler/Operation/Gap/Completed/View事实写入同一Run源，确认后再发布内存变化。核心`History`仍只含Admitted/Cycle/Action/Terminal；`HistoryPosition`覆盖执行事实提交，不伪造核心进度。Reset清当前事实列表，保留旧历史及当时原最新view，不能当作新Plan已采纳。

`RestoreReadOnly`按原顺序恢复原版本view、原callId的Running/唯一终态、operation/gap/工作记录和Finding；Finding只保存原结果，包括Runtime拒绝的关联，不重判领域结论。`TraversalHandlers/Operations/Gaps/CompletedWork`可查询尚未形成下一view的已提交事实。缺原依赖、非法后缀或无初始化来源拒绝。旧核心历史仍可读，`TraversalHistoryAvailable=false`表示缺执行来源，不代表确知为空；新原录制从admission标记已知来源。

Host文件格式v2及head v2拒绝混合版本，v2记录载荷拒绝未知字段，继续读取原v1核心文件但不给执行完整性；可信IRunStateJournal的来源保证仍由adapter承担。Run/资源共同末位比较自动包含新事实，历史位置不等于Control/Runtime游标或执行许可。Running只说明原调用未记录终态，不包含可重建Task/期限或重试许可；完整Control/Plan恢复、当前事实与continuation仍未提供。
