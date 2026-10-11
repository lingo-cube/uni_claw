# ADR-0042：Product T0–T3 拓扑与独立语义生命周期

状态：accepted（2026-10-11，ARCH-ALIGN-001）

产品架构基线原先把组件、Goal/Contract/Run、Evidence/WorldBelief、Outcome
放进同一套 L0–L3 编号，并在不同文档中复用裸层号。这让层号无法回答一个
概念是组件、运行时对象、证据流节点还是实现细节，也让 `Authority: NONE`
的候选元数据与仓库实际消费方式发生冲突。

## Decision

### 1. Product 拓扑使用命名空间 T0–T3

```text
Product T0  系统边界与外部参与者：UniClaw、User、Host、Device/Environment
Product T1  顶层产品组件：UniAgent、Uni Kernel、Capability Plane、Memory System
Product T2  Uni Kernel 的六个责任域：Evidence Ledger、World Model、Run Model、
             Control Loop、Assurance、Effect Boundary
Product T3  责任域内部模型、协议实现和 realization 模块
```

`Product T0–T3` 是当前产品拓扑的唯一公共层级名称。层号只表达拓扑位置，
不推断概念的 Owner、Authority、生命周期或协议状态。

### 2. 产品语义另建生命周期轴

以下对象不再作为 Product T0/T1 组件列出，而在独立的 Product Semantic
Lifecycle 中描述：

```text
Product Session → Primary Goal → Execution Contract → Primary Run
                 → Outcome State / Runtime Outcome → Goal Evaluation
```

Evidence Record、WorldBelief Revision、Control Intent、Canonical Binding、
Effect、Outcome Proof 等沿 Evidence/Belief/Control/Effect flow 关联到该生命周期。
它们的 canonical Owner 由责任域和 UniAgent 语义分别持有，不由拓扑层号决定。

### 3. 其他层级必须带命名空间

Capability maturity/protocol 使用 `Capability C*`；decision granularity 使用
`Decision G*`；profile assembly 使用 `Profile A*`；Harness evidence 使用
`Harness E*`；协议边使用 `Protocol P*`。裸 `L0`、`L1`、`L2`、`L3`、`L4` 不再
表示 Product 架构位置。

### 4. 旧 Product L0–L3 的迁移规则

现有文档中的 Product `L0–L3` 只作为历史引用或迁移映射保留。当前文档、
README、代码索引和新协议不得以旧层号作为公共架构名称；迁移期间必须显式写出
`Product T*` 对应关系。历史 ADR、Change State 和证据不改写其原始记录。

### 5. 基线权威状态

`docs/architecture/product-architecture-baseline-l0-l3.md` 保持稳定路径，正文
改为 Product T0–T3 的 canonical baseline，并声明：

```text
Status: ADOPTED / PRODUCT-T0-T3-CLOSED
Authority: PRODUCT_ARCHITECTURE_BASELINE
```

该路径是现阶段唯一产品架构权威。后续修改必须通过新的 Change State 与 ADR
记录；不得用设计稿、实现状态、局部协议或未提交词汇表覆盖它。

## Consequences

- UniAgent、Uni Kernel、Capability Plane、Memory System 在同一 Product T1；
  UniAgent 不再因某个 README 的旧层号被误读为 Product T1 之外的另一种层级。
- 协议审阅可以分别沿语义生命周期、Evidence/Belief flow 和 Protocol graph
  展开，不再从 L1/L2/L3 推断消息含义。
- 需要在后续协议 Change 中更新协议文件名、交叉引用和当前文档的旧层号；这次
  ADR 不改变 P1–P26 的载荷、编号或执行行为。
- Core、Workspace、Host/DSH、Provider 和 Development Harness 的 authority
  不因 Product T1 命名变化而提升；它们仍按各自边界单独审阅。

## Rejected alternatives

- **保留单一 Product L0–L3，并把语义对象塞进 L1**：继续混淆组件与生命周期对象。
- **严格上下文优先且不设语义轴**：能表达外部边界，但无法清楚表达 Goal、Run、
  Evidence 与 Outcome 的生命周期关系。
- **Product L0–L3 与 T0–T3 长期并存**：会重新制造同名异义，层号仍无法作为稳定
  文档入口。
