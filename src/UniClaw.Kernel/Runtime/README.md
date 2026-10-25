# Runtime 代码索引 — Uni Kernel 驱动面（Product T1 本体 · 基线 §7/§24）

RUNTIME-STORAGE-001 C组：`RunResourceRecovery.cs`增加IAsyncRunRecoveryArchive，`KernelRunDriver.Resources.cs`在原身份确认后才发布ResourceBinding；`KernelRunDriver.cs`对completion verifier/adjudicator和cancel使用原ProcessBatchAsync，裁决恢复与Drive共用串行门。`RunDriverInputs.cs`的可选异步诊断观察缝让正式Host等待原咨询/遍历回流产物，保留旧同步回调；没有新增生命周期、恢复授权或专用线程。


RUNTIME-STORAGE-001 B组（Slice 5）：`KernelRunDriver.Resources.cs`的initialize/begin/confirm与checkpoint经`IAsyncRunResourceJournal`等待原确认；`KernelRunDriver.cs`经`IAsyncConsultationJournal`等待reserve/complete/reject，reserve未确认不咨询、结果未确认不采纳。Drive门覆盖等待；接受后取消先结算，失败锁存。旧同步journal保留原异常与幂等语义；身份archive.Initialize仍同步，留C组。资源、P25与执行各自格式/Owner保持，不跨外部屏障合批。


RUNTIME-STORAGE-001 Group A（Slice 4）：`KernelRunDriver.PlanHistory.cs`沿`IAsyncRuntimePlanJournal`等待initialize/replacement-begin/adopt确认，确认后才发布原采纳历史和新Plan。`KernelRunDriver.cs`等待Control全部外层操作，select/drain确认前不交出工作或事件；原Drive门覆盖所有await。接替顺序仍是Plan replacement-begin确认→旧Control abandon确认→匹配Plan adopt确认；故障锁存、不回滚另一Owner已提交记录，取消先结算已接受阶段，后续不执行新工作。两个正式Host await using来源；真实双窗口Kill探针为`tools/RunRecoveryProbe/ControlPlanAsyncProbe.cs`。剩余资源/P25/Effect/归档及全域初始化/收尾按组B、C继续，未宣称全域完成或续跑。

Group A v2：Control配置规则＋启动由`ConfigureAndStartAsync`在原生支持批次时共同确认，仍保留两条原记录；旧journal不支持时保留逐条行为。接受后取消先结算整批，在下一安全边界停止，不额外选择工作；Plan三个接替阶段仍不合批。公开Driver同场景10条记录实际managed frame/head写入及flush从10到9，局部两条2→1，非物理磁盘调用或延迟保证。

RUNTIME-STORAGE-001 Slice 3：Run admission/cycle/action/terminal及遍历事实沿`UniKernel→KernelRunDriver.cs`真实await，cycle未确认不投递，action记录未确认不继续观察/咨询，terminal未确认不发Outcome。遍历operation/Running记录确认后才调用handler；在Running保存等待时取消，先完成已接受记录再保存Cancelled，不能留下活跃调用或将持久化错误重判为handler结果。原Drive是显式同步兼容入口；Driver门覆盖全部新await。attempt reflux改走已有Evidence异步批次；completion/cancel证据、构造及Control/Plan/资源/P25/Effect仍有同步路径，全域未完成。新的真实Kill探针为`tools/RunRecoveryProbe/RunAsyncProbe.cs`。

RUNTIME-STORAGE-001 Slice 2：观察处理等待Evidence批次后继续await World relevance/reconcile；权限冲突与动作后的导航/视口记录同样await。`UniKernel`串行门覆盖整个观察批次，`KernelRunDriver.cs`既有Drive门覆盖全部await；未确认时不咨询/投递/重拉。World确认后在安全边界重新检查取消，StepVerify继续先结算已发生动作的验证屏障。Run/Control/Plan/资源/P25/Effect及若干非观察回流仍有同步写入；本片不代表全域异步完成。

`UniKernel` 的**驱动面**：六个 Product T2 责任域的组装与推进循环。它不是第七
个 Product T2——不拥有任何 Product T2 authority，只保证它们的组合纪律（§7：互不重叠、
intent/judgment/delivery 分离、跨 Product T2 只传 typed immutable、capability
不旁路、terminal 后无旁路 effect path、outcome 只从 canonical Outcome
State 发出）。

**公开面 = 冻结面**：本目录类型在 Kernel 公开面白名单中
（RUN-003/RUN-005 提升，`KernelRuntimeSurfaceWhitelistTests` 执法）——
任何公开类型增删 = 白名单修订 = 显式 change（HOST-001 D8）。

## 代码归属

| 文件 | 职责 |
|---|---|
| `KernelRunDriver.cs` | primary run 的推进循环（legal activation 结果、幂等语义 §24.1 不变量 44；ADR-0019 自驱）；普通Act以发起容器及覆盖层状态限制尾部续行，Plan可在新事实下跨Slice展开，Guard失败保留正确步骤索引 |
| `RunDriverInputs.cs` | 驱动输入（observation/cancel/unexpected 的 typed 形状） |
| `AgentDecision.cs` | Agent 决策缝词汇（proposal/disposition） |
| `AgentPlanPolicy.cs` | 决策计划策略（FROZEN v0.3.1 咨询缝词汇） |
| `TraversalExecution.cs` | 持续遍历World引用投影、操作级typed语言handler、调用/状态/报告及Recovery事实交接词汇 |
| `PlanLanguageHandlerCall.cs` | 单个typed语言handler的机械异步生命周期、协作取消与迟到异常观察；无Owner写权限 |
| `PlanExecution.cs` | 计划候选/守卫投影、明确续行位置、逐动作尝试及阻碍证据视图 |
| `RuntimePlanGraph.cs` | 原Plan节点与子块引用的冻结、枚举及结构校验；不创建第二执行器 |
| `PolicyProtocol.cs` / `PolicyRuntime.cs` | 策略求值协议与运行时（Slice A/B；求值机制面保持 internal） |
| `ConsultationTypes.cs` | 咨询缝 typed 词汇 |
| `ConsultationJournal.cs` | P25 四元组 journal seam、semantic count 与 fail-closed replay |
| `DiagnosticObservations.cs` | 可选观察回调的有界单消费者、独立失败状态和有界关闭；不参与正式咨询/执行确认 |

RUNTIME-STORAGE-002首片：`ObserveConsultationAsync`及同步/异步遍历观察者只尝试进入同一有界辅助队列，Driver不等待导出，不因其异常Reject正式P25。调用方在Run作用域结束时await `CompleteDiagnosticObservationsAsync`或异步释放Driver；Closed只代表停止接收，Pending/Unsettled仍须明确呈现，不能冒充flush成功。回调payload是只读诊断输入，不得变更其中集合或驱动领域操作。上限与等待默认值只在`DiagnosticObservationOptions`定义；超时后停接收并持有/观察未完成回调，不继续无限堆积。它不是关键journal，不能承载恢复所需唯一记录。

## 变更规则

驱动循环、Product T2 组装顺序、决策/咨询缝词汇进本目录；新增公开类型必须
同 change 修订白名单测试；决策语义的所有权仍在 UniAgent（L1），本目录
只持有缝的 Kernel 侧形状。

## 指向

- 基线 §7/§24；ADR-0019（自驱）、ADR-0025（driving surface 公开+白名单）、
  ADR-0026（buyer-driven 泛化）；
- 被组装的六域：`../Evidence/` `../World/` `../Run/` `../Control/`
  `../Assurance/` `../Effects/` 各 README。

## RUNTIME-PLAN-002 持续遍历

`PlanItem.TraverseItem`复用原Plan/ContinuePlan。ControlLoop持有Control/TraversalControl；KernelRunDriver协调当前World投影、typed语言handler和原StepAct→StepVerify链。RunModel记录TraversalExecutionView和历史，状态查询读Owner记录，暂停不产生最终报告。ReadOnly仅记录/检查，Navigate始终经原许可/验证；发现不足/深度/排除/未知在报告中保留，不推出Goal完成。字段、输入关联及协议生成闭包见[正式执行契约](../../../changes/RUNTIME-PLAN-002/execution-contract.md)。

RUNTIME-PLAN-003增加typed异步语言适配。`Drive`在等待调用时可恢复地返回`WaitingForHandler`；`DriveAsync`等待完成/截止/取消后接回原位置，真实外部输入等待仍原样返回。Run按callId记录Running及单一终态，状态查询不推进。业务Finding及原逐动作验证语义不变，见[异步执行契约](../../../changes/RUNTIME-PLAN-003/execution-contract.md)。

## RUNTIME-RECOVERY-002 资源事实记录

RunDriverInputs.ResourceJournal是生产存储缝，driver在P25、dispatch及post-action核验前记Begin，在安全边界记Confirm；verify的WaitingForInput跨Drive保留同一Begin，原Verify调用结算后才允许Confirm；确认失败后不再咨询或派发，实际Receipt保留。记录原两维消耗、Runtime授拒、复核及已资助事项/进展依据；RunResourceAccounting只解释这些历史资源事实；默认Inspect只给分项，不算完整余额。原driver签发的RunResourceBinding与记录身份匹配且adapter核实原P25/exec完整文件前缀后，才可能报告AccountingComplete；该adapter核对是受信存储缝的来源证明，不是任意调用者的新执行许可。ResourceSnapshot的pending/terminal信息仅是记录边界的只读状态，不替代原Owner当前真相。余额完整也不授予继续许可，RunResourceRead.ContinuationSupported固定false；新driver接入已有账目只报告read-only。当前记录只支持新Run的单accepted View；多View/原执行现场恢复保持未实现，不以新View清空旧账。测试/legacy composition可省略资源源，但不能据此声称具备持久性。契约及限定范围见changes/RUNTIME-RECOVERY-002/。

RunResourceBinding的构造器不公开，仅在原driver成功初始化后签发；已有历史、未激活或未初始化driver不能签发。它不提供序列化恢复构造，也不授予继续许可。公开journal.Identity是历史显示字段，不是qualification输入。

RUNTIME-RECOVERY-003增加IRunRecoveryArchive：原driver资源初始化后、首次咨询/Effect前归档RunModel原Owner身份和Runtime原策略；失败锁存并阻断外部操作。RunResourceAccounting.RestoreBinding经原Owner纯读取校验及Runtime逐项关联恢复sealed只读binding，不从resource.Identity或新driver接纳合同自证。新File资源源的唯一RecordingId与原归档关联，区分同合同/RunId的不同录制。该binding依旧不授予执行许可；多View、RunState、World/Control/Plan等现场未恢复。

RUNTIME-RECOVERY-004把原Owner状态记录故障与只读模式接入Kernel/driver的外部调用前检查。状态持久失败后保留实际Receipt，资源Begin不被伪造Confirm，后续Drive不会再咨询/派发；Kernel直接Act/EvaluateTerminal及activation也有对应门。Run状态只读恢复不依赖资源snapshot.Terminal，不把Agent.Blocked/外层unknown标签当canonical Outcome。完整执行依赖与跨Owner一致恢复点仍待后续验证。

`RunResourceRecord.RunHistoryPosition`关联当时原Owner已提交版本；新流从Initialize起不能缺关联或变更source，位置不能回退。driver正常返回前，若Owner提交晚于最后资源记录且没有未决操作，写入checkpoint Begin/Confirm；该记录只允许Terminal投影改变，不允许消耗、授额或资助依据变动，不是新的canonical Run状态。

`RunResourceAccounting.InspectRecovery`仅比较原只读Run与已认证资源末位：完整账、完整关联、相同原Run/source/版本及终态投影才给出`RunRecoveryPoint`。缺口保留分项和原Owner历史，`StateAndResourcesConsistent=false`、Point=null；不退回旧Owner版本，不从Progress.Acts推算资源消费。`ContinuationSupported`恒false。这个点只覆盖Run/Runtime，不表示World/Control/Plan/当前Evidence已恢复。

原archive同时携带EvidenceRecordingId；Kernel只投影原Ledger source id，driver在首次外部调用前绑定。Evidence持久失败按原Owner故障锁存：后续Drive不咨询/投递，若失败发生在实际Receipt回流的AttemptReport admission，保留原Receipt/exec与resource Begin，不假装Confirm或重发动作。恢复Ledger只读会阻断Kernel激活、Intent、Act与terminal评价，未恢复World/Control/Plan或三Owner共同cutoff。

ORIGIN-CORRELATION-001：StepVerify的每个同步NextInput以原in-flight Receipt建立独立短期link，ObservationDirective显式传递。采集源只在本次请求完成后用CompleteObservation回填同一link，wrapper保留原对象；driver检查批次及逐proposal关联一致才送P2，finally关闭本次link（含等待、异常、补充循环）。未echo仍可External入证；错/跨请求echo在入证前UnexpectedInput，不自动填充。模型completion anchors只核验原事实引用，不产生动作观察关联，不能据此清偿MaterialEffect。

短生命周期PostActionEffectVerification仍是既有局部状态谓词判定，不能替代run-level MaterialEffect/Outcome Proof；本片不把该结果升级为因果证明。完整效果义务继续由Assurance消费canonical Observation + post Context +实际PostActionSelf判定。异步迟到采集、恢复后的重新关联不在此同步切片内，保守External。

RUNTIME-RECOVERY-007：原RunResourceRecord的每次Initialize/Begin/Confirm同时携带原EvidenceHistoryPosition（可空兼容旧记录，seq0为已提交空历史），Runtime验证同原recording、单调seq及首帧起完整关联。CheckpointOwnerHistories在正常返回且无pending操作时比较Run/Evidence两条原位置；任一更新就以既有checkpoint提交，只能沿载同一资源账目及原terminal投影，不能增耗/授额。

InspectRecovery仍只表示原Run/Runtime历史。InspectEvidenceRecovery在原两方核验、两原Owner readonly及精确Evidence末位一致后，返回独立RunEvidenceRecoveryRead/Point；无法对齐时Point=null、HistoriesConsistent=false，保留RunAndResources及可读原Evidence。三个单独读取成功不等于共同位置成功。提交后的Evidence后缀（含duplicate/rejected、仅入证不增Run）必须由后续原checkpoint关联后才能成为新共同历史点；不回退旧Ledger/Run、补记或续跑。可信停止writer是前提，非活跃并发协调器。

RUNTIME-RECOVERY-010：原资源Initialize/Begin/Confirm同时携带WorldHistoryPosition；原archive v3绑定World录制GUID，仍可读取v1/v2旧账目。Driver在原安全返回处比较Run/Evidence/World三个Owner末位，仅World的relevance/demand/continuity变化也由既有checkpoint确认，资源事实不增不减。World位置必须从初始化绑定、同源单调、不能半途补绑或脱绑；旧无World记录不能自动升级为四方点。

`RunResourceAccounting.InspectWorldRecovery`返回独立`RunWorldRecoveryRead/Point`，只在原Run/资源/Evidence三方完整、World原Run/Evidence来源一致及World完整原末位精确相同时认证。World仅数据读取，不是恢复的WorldModel；缺口保留可读分项但Point=null，不回退旧前缀、不补写。原World确认后但资源尚未checkpoint的后缀会拒绝四方，已确认账目仍可独立读。ContinuationSupported恒false；没有Current/Control/Plan/coverage或执行续跑许可，读取要求原writer停止。

## 原历史读取的存储依赖（RUNTIME-RECOVERY-015）

`RunRecoveryReader(IRunRecoveryArchive, IRunRecoverySources)`编排既有Run/Evidence/World/Control只读历史和资源共同点查询。Reader只依赖原存储接口，来源的创建与释放由装配方负责；它不使用文件路径、流、Host适配器，不初始化或写入来源、构造执行Kernel或推进Run。来源必须是原writer停止后的受信稳定历史，不是面向外部JSON的admission接口。

IRunRecoverySources按既有领域提供原历史及资源读取。原资源记录仍由RunResourceAccounting解释；适配器必须核实原咨询/执行来源关联，严格读取还须核实完整原提交末位。文件适配器使用原文件前缀和head/EOF，其他介质需要提供其真实原提交关联证明，不能直接将标志置true代替核验。来源异常按约定转为不可用，保留其他可读原历史，不认证共同点。缺源不是空状态；ContinuationSupported恒false。

当前FileRunRecoveryArchive保留兼容查询入口，统一委托该Reader；测试内存来源在原文件目录移除后经同一Reader读取并保留原关联。该证据证明查询编排不依赖文件，未实现或验证数据库耐久性、远程并发、Task/期限或跨进程续跑。

## 原Plan采纳历史（RUNTIME-RECOVERY-016）

Driver在V7前一次冻结完整Plan集合，V7验证的节点与当前执行Frame/Loop使用的节点相同，调用者修改原List不改变采纳内容。原日志通过`IRuntimePlanJournal`写入，八类节点以原值、稳定节点路径和子块引用保存；不借用Agent wire反序列化构造第二执行器。

初始化绑定原Run身份/Run录制GUID；新采纳先提交原输入与严格递增的原Plan版本表，ack成功后才发布新Plan。替换暂停Plan/活跃Traversal时，先保存`replacement-begin`，再由原Control弃置，最后保存匹配的新`adopt`；中断保留未闭合边界并阻断后续Drive。跨Owner并非原子事务，已发生的Control提交不能回滚。采纳提交可在ack丢失后存活，不能据此声称原Runtime已开始执行。

`IRunRecoverySources.RuntimePlanHistory`提供同一原源，`RunRecoveryReader.ReadRuntimePlanHistory`只读核对归档v5的Plan录制GUID、原Run关联、完整节点/块结构及版本/接替关系。缺源/旧归档不可用；合法初始化且无采纳才是已知空。Reader不拥有来源生命周期。完整运行游标、Control工作接收及六方共同点仍未实现，`ContinuationSupported=false`。

运行中Plan接替使用原Append确认后缓存的最新采纳序号和pending被接替序号，避免为取两个位置反复展开完整`RuntimePlanHistory`；失败或丢ack不会推进缓存。公开完整历史读取与跨源校验保持，缓存不是可恢复执行游标。

## Observation storage (RUNTIME-STORAGE-001, slice 1)

KernelRunDriver now awaits ProcessBatchAsync through the observation pull and DriveCore chain. DriveAsync keeps its serial gate during storage waits; cancellation is observed after accepted storage settles and before the next consultation/effect. Drive remains an explicit synchronous compatibility entry. Other domain journals, completion/attempt reflux and cancellation evidence still have synchronous paths and remain migration work. No new execution Owner or storage-wait replay state is introduced.
