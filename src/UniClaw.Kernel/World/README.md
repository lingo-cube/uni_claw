# World Model 代码索引（L2 · 基线 §12）

Sole **Belief / Reconciliation Authority**：Current WorldBelief、World
Graph、Reconciliation。不拥有 Goal、Run progress、intent 或 Assurance
Judgment（权威表见基线 §10）。

## 代码归属

| 文件 | 职责 |
|---|---|
| `WorldModel.cs` | belief/reconciliation 权威本体（不变量 14-15） |
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

belief/reconciliation/世界投影语义进本目录；UI realization 细节进
`UiRealization/`；定位与 delivery 锚只定义形状，执行归 Effect Boundary。

## 指向

- 基线 §12；协议：`docs/architecture/uworld-protocol-baseline-l4.md`；
- ADR-0012/0015/0016/0024/0028；上游：`../Evidence/README.md`。
