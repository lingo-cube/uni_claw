# World Model 代码索引（Product T2 · 基线 §12）

Sole **Belief / Reconciliation Authority**：Current WorldBelief、World
Graph、Reconciliation。不拥有 Goal、Run progress、intent 或 Assurance
Judgment（权威表见基线 §10）。

## 代码归属

| 文件 | 职责 |
|---|---|
| `PageObservation.cs` | 当前capture/身份证据严格codec、不可变观察状态及World派生ObservationNeed |
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

## WORLD-VISUALIZATION-001 实现（切片 0）

- `WorldRevisionTimeline`：revision 时间线与节点语义标签的只读投影类型；
  时间为采集高水位，提交墙钟不伪造。
- `WorldModel.Visualization`：图投影增补（capture-scoped 结构关系、Owner 侧
  语义标签 join 与界定内纠错冲突判定）与 revision 时间线派生；纯读取，
  不修改 World 状态、不产生 revision。
