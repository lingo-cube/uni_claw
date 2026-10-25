# World Model 代码索引（Product T2 · 基线 §12）

RUNTIME-STORAGE-001 C组：`WorldModel.HistoryRecording.cs`的CreateAsync在原initialize确认后才返回Owner；`WorldHistory.cs`的IAsyncWorldJournal.InitializeAsync承接这个实际buyer，旧adapter默认保留显式同步兼容，正式文件adapter原生await。旧构造与历史只读格式保持；新建Host不再经同步初始化写入。


Sole **Belief / Reconciliation Authority**：Current WorldBelief、World
Graph、Reconciliation。不拥有 Goal、Run progress、intent 或 Assurance
Judgment（权威表见基线 §10）。

## 代码归属

| 文件 | 职责 |
|---|---|
| `PageObservation.cs` | 当前capture/身份证据严格codec、不可变观察状态及World派生ObservationNeed |
| `OverlayObservation.cs` | capture/cycle/session/time关联的覆盖窗观察候选与严格codec |
| `WorldModel.Overlay.cs` | World派生当前popup、未知/冲突与ObservationNeed；迟到仅审计，阻止旧present复活 |
| `WorldModel.cs` | belief/reconciliation 权威本体（不变量 14-15） |
| `WorldModel.Semantics.cs` | 将有当前身份观察/capture依据的accepted页面语义投影到既有容器；不以类别铸造身份 |
| `WorldModel.Viewports.cs` | 已验证滚动的观察关联、accepted 重叠锚配准与未知原因（WRC-003） |
| `ViewportTransition.cs` | 操作回执关联的前后观察、可选空间变换和对应锚 |
| `WorldBeliefRevision.cs` | 显式冲突：同 subject 既存 belief 遭遇不相容 accepted evidence |
| `WorldRevisionIndex.cs` | revision 索引 |
| `PersistentRevisionCollections.cs` | revision 持久化集合 |
| `ConflictResolver.cs` | 冲突裁决器 typed semantic.checked 实现（PER-009） |
| `ProducerTrust.cs` + `producer-trust.json` | (源×类别)→ABC 信任等级表（PER-009 D5 / ADR-0028） |
| `RelevanceJudgment.cs` | 单条 accepted evidence 的 belief relevance 判定留痕 |
| `TransitionContext.cs` | transition epistemic strength（P22 / UWM-009 §12） |
| `ConsumerViews.cs` | scoped claim 粒度语义（owner 派生不可变投影，ADR-0011） |
| `PostActionXmlRouter.cs` | 事后验证 XML 路由（PER-009 S7 四门收紧版，D9） |
| `Slice.cs` | Slice = WorldBelief revision 的 scoped immutable 投影（含 OccurrenceFact） |
| `SpatialLocator.cs` | frame-bound 归一化空间锚（DSE-002） |
| `NativeLocator.cs` | 平台原生定位锚（DSE-003 / P14 第二种 delivery target） |

## UiRealization/ 子目录

UI 世界的 realization 模块（ADR-0024：kernel world module behind
dependency boundary）：`UiEntityModel`、`ContainerAssociation`、
`Continuity`（ADR-0015 demand-gated logical item continuity）、
`GroundingSeam`、`OccurrenceDescriptorMatcher`、
`ProductAssociationStrategy`。presentation 再观察修订不冲突（ADR-0016）。

## 变更规则

WORLD-SEMANTICS-002：当前 `ui.screen` 的历史观察不回退当前claim/身份；
同帧身份声明冲突不推进关联。语义观察沿P2/P3进入，须明确引用当前身份观察并
匹配其capture，才能作为容器scoped claim进入Slice；PageKey只是类别，不能
mint/merge身份或创建导航边。普通状态claim的冲突/权威裁决机制保持独立。

WRC-003：`WorldBeliefRevision.ViewportTransitions` 保留同页滚动观察关联；
`WorldGraphProjection` 按 scope 派生这些关系，既有导航 `Edges` 语义不变。
Kernel 仅在实际 dispatch 事后验证成功后登记，World 以 accepted 前后观察
计算位移。证据不足保留未知；对应锚不是 LogicalItem identity，历史内容
坐标不是当前可执行 locator。

belief/reconciliation/世界投影语义进本目录；UI realization 细节进
`UiRealization/`；定位与 delivery 锚只定义形状，执行归 Effect Boundary。

## 指向

- 基线 §12；协议：`docs/architecture/uworld-protocol-baseline-l4.md`；
- ADR-0012/0015/0016/0024/0028；上游：`../Evidence/README.md`。

## WORLD-UNCERTAINTY-001 实现

- `WorldConsumptionBasis`：Owner派生的实际页面/目标来源及未证明范围。
- `WorldPageView`：固定World修订的页面观察、解释、上下文与未知读模型。
- `SliceContentPresentation`：Replay共用的无权威Slice纯展示投影。
- `WorldAccumulatedContent`：非执行的累计内容、当前/历史状态及配准来源。
- `WorldModel.Corrections`：精确历史归属纠正与受影响解释/关系撤回。
- `WorldModel.Knowledge`：依实际来源和显式buyer派生四类知识缺口。
- `WorldModel.History`：固定修订的历史解释、访问来源与页面读供给。
- `SemanticVerificationObservation`：独立语义验证载荷、严格codec与当前知识来源。
- `WorldModel.Content`：沿World已证滚动链派生累计页面片段。
- `WorldModel.Context`：同capture原生父子上下文关联的World裁决。
- `WorldModel.SemanticKnowledge`：核验native profile与本轮对应后派生稳定语义沿用。
- `PageInterpretationCorrection`：指定原capture与旧解释的纠正载荷和处置记录。
- `WorldKnowledgeView`：身份/归属/关系依据/观察覆盖四类只读摘要。
- `ContainerContextObservation`：原生父子上下文证据载荷及严格codec。

## 原操作历史（RUNTIME-RECOVERY-009）

`WorldHistoryRecording`将原World绑定到独立journal及原Run/Evidence recording。8个外层写入口覆盖9个revision发布位置，以及不产生revision的relevance/demand/continuity日志变化。持久路径在候选Owner状态内执行，收齐Publish之后的日志/来源/时间门限后，先恢复已发布状态；journal确认成功才发布候选。失败锁存World并经Kernel/Driver关闭消费与执行，不补记、不重试；无journal的既有内存路径保持原行为。

`WorldHistoryRecord`保存新增修订/日志后缀、原输入（含原P22）、必要需求/来源映射/页面时间/解释撤回，初始化记录scope、原profile指纹和实现/strategy二进制身份。二进制身份与原输出供历史审计，不是可重新执行的strategy配置；索引与metrics不成为持久权威。候选集合目前复制原内存集合，日志与revision前缀不重复写入磁盘；长规模代价需另行测量。

WORLD-HISTORY-002：`WorldHistory.cs`的`IWorldJournal.AppendOperation`将不含revisions的操作元数据与本次不可变typed修订分别交给adapter；`WorldModel.HistoryRecording.cs`保留原Owner的提交确认后发布。默认实现补回原完整JSON后调用Append，兼容已有非文件/拦截adapter；生产FileWorldJournal直接编码修订变化，不先序列化完整累计revision。文件编码不进入World事实模型；ReadRecords与WorldHistoryRead的逻辑输出仍为完整原修订。原输入/日志与必要侧状态继续保留；异步迁移仍属RUNTIME-STORAGE-001。

RUNTIME-STORAGE-001 Slice 2：`WorldModel.Async.cs`覆盖八类操作的异步入口，共享`WorldModel.HistoryRecording.cs`原候选逻辑。`IAsyncWorldJournal`成功表示真实完整操作确认；等待期间恢复旧Owner，串行门拒绝其他写入，成功才发布，失败锁存。旧同步入口为显式阻塞兼容桥；旧IWorldJournal保持同步fallback。接受前取消零写入；接受后等待真实确认，Driver随后处理取消。无journal内存路径不引入磁盘行为，构造期初始化仍同步。文件增量格式及历史读取语义保持，不合并跨操作屏障。

`WorldHistoryRead.ReadOriginal`只接受原readonly存储与原readonly Evidence Ledger，验证World/Run/Evidence源、版本、完整操作/修订链、必需侧状态及typed位置中的原accepted引用。返回冻结JSON历史数据，保留字段/顺序、原未知与原判断；没有WorldModel/Current/Binding接口，不调用策略或重新入证。它不恢复当前现场、四方共同位置或执行续跑。详细验收与实际进程Kill证据见`changes/RUNTIME-RECOVERY-009/`。

日志的决定来源及生效依赖须为原accepted/已处理记录；未应用纠正/语义提议中的字段与原source Claim逐值核对，允许原未知ID，不能提升为依据。association候选保留原strategy输出，未消费候选不是accepted依赖；最终belief依据仍严格校验。读取只解析原载荷，不重跑Owner判断。同版本附加revision字段保持原JSON和顺序，必需字段缺失与未知schema继续拒绝。
