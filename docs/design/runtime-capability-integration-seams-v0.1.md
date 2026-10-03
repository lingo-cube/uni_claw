> Status: DRAFT
> Authority: NONE
> 日期: 2026-10-03 · 方法: 现有调用链盘点 × 能力买家场景 × 接缝推导

# Runtime 能力集成接缝 v0.1

## 0. 目的与边界

本文只讨论 UniClaw Kernel / Host 如何为未来业务能力预留集成接缝，不讨论语言识别、性能分析、报告格式或具体实现。

目标是让语言合规、性能测量、可访问性检查、审计、外部诊断等能力以模块方式接入，而不是每增加一种能力就修改 `UniKernel` 主循环、`WorldModel` 或 `EffectBoundary` 的业务语义。

本文不冻结公共接口、不新增 Product Authority、不改变现有 Runtime pipeline，也不授权能力模块直接修改 World、Run、Assurance 或 Effect Owner。

## 1. 当前缺口

现有组合链已经有明确的 Owner 和调用顺序：

```text
Observation
  → EvidenceLedger.Admit
  → WorldModel.Reconcile
  → Slice / Grounding
  → Control Intent
  → Canonical Binding
  → Assurance Judgment
  → EffectBoundary.Dispatch
  → Receipt
  → Post-action Observation / Verification
  → Terminal Outcome
```

当前缺少的是贯穿这些阶段的能力集成面：

- `UniKernel` 只组合固定 L2 Owner，没有能力模块注册或生命周期通知面；
- `IRunTrace` 是结构因果观察面，不是业务能力插件接口；
- `RuntimeStageMetrics` 是 Runtime 内部量化观察面，不承载业务检查；
- `SettingsTraversalLiveFeed.TraceEntry` 已开始混合 Popup、Slow、Grounding、Effect、Verification，继续扩展会形成 Host 专用的万能记录对象；
- 业务模块没有统一的 correlation 入口，难以把“观察结果”“动作”“回执”“后置验证”关联起来。

## 2. 接缝设计原则

### 2.1 能力模块不等于 Owner

能力模块只能观察、测量、提出建议或提供显式的 Effect realization；不能因为接入 Kernel 就获得以下权力：

- 铸造 Evidence、WorldBelief、ContainerIdentity 或 LogicalItem；
- 修改 Run State 或绕过 Control；
- 形成 Canonical Binding、Assurance Judgment 或 Effect authorization；
- 把 Trace、Measurement 或 Finding 升格为 Product truth。

### 2.2 Hook 是生命周期事实，不是可变上下文

接缝传递不可变的、带 correlation 的生命周期事件。能力模块不能通过修改事件对象影响主流程。

每个事件必须有：

1. producer 和 Owner；
2. 发生位置和时序；
3. correlation identity；
4. 可见数据范围；
5. 能力模块允许产生的输出；
6. 模块失败时的行为。

### 2.3 不做万能回调

语言检查和性能测量都属于观察型能力，但“给策略建议”和“提供新的可执行 Effect”具有不同的 authority 和失败语义，不能统一成一个可以返回任意动作的回调。

## 3. 候选生命周期节点

以下是应预留的候选节点，名称只是语义占位，尚未冻结为接口：

| 节点 | 产生位置 | 适合的能力 | 能否改变主流程 |
|---|---|---|---|
| `RunAdmitted` | Run Model 接受 Contract 后 | 预算、环境、场景级能力初始化 | 否 |
| `ObservationAccepted` | Evidence Ledger admission 成功后 | 语言合规、可访问性、审计、证据质量 | 否 |
| `BeliefRevisionPublished` | WorldModel reconciliation 后 | 世界投影检查、覆盖检查、审计 | 否 |
| `SliceDerived` | Consumer Slice 派生后 | 页面/容器级业务检查 | 否 |
| `IntentIssued` | Control Loop 签发 Intent 后 | 策略诊断、计划审计 | 否 |
| `BindingResolved` | Effect Boundary 形成或拒绝 Binding 后 | 目标绑定诊断、操作审计 | 否 |
| `AssuranceJudged` | Runtime Assurance 完成后 | 授权检查审计、拒绝分类 | 否 |
| `EffectDispatchStarted` | Driver 调用前 | 投递测量、预算计时 | 否 |
| `EffectReceiptProduced` | Driver 返回并形成 Receipt 后 | 投递结果测量、执行审计 | 否 |
| `PostActionVerified` | 后置观察与 Assurance 验证后 | 交互响应测量、回归检查 | 否 |
| `RunTerminal` | Outcome exactly-once emission 后 | 报告汇总、外部导出 | 否 |

所有事件都应使用现有运行关联，而不是由能力模块重新铸造身份。候选 correlation 包括：`RunId`、`IntentId`、`BindingId`、`ReceiptId`、`CaptureId`、`ObservationCycleId`、`RevisionId`。

## 4. 能力类别

### 4.1 Observation Capability

消费已接受的观察、Slice 或 Revision，产生 `Finding`、`Measurement` 或 `Evidence Reference`。

示例：

- 预期中文但观察到英文；
- 菜单项缺少副标题；
- 当前页面缺少要求的结构；
- 某项出现频率或覆盖率异常。

这类能力不能阻止动作，除非另有明确的 Runtime Contract / Assurance 语义接管它。

### 4.2 Advisory Capability

消费当前 Runtime View 和既有证据，产生有限的 `Advisory` 或计划建议，交回现有 Control / Agent 决策缝。

这类能力不能直接调用 Driver，不能输出坐标或绕过重新 Grounding。

### 4.3 Measurement Capability

消费生命周期时间点和关联事实，产生非权威的 `MeasurementSample`。

它只能测量已发生的事情，不能因为耗时超过阈值而自行重试、改变策略或授权动作。

### 4.4 Effect Capability

为新的 effect class 或外部执行通道提供显式 realization。它必须注册到 Effect Boundary 的既有 Gate / Binding / Receipt 链，不能通过观察 Hook 直接执行。

## 5. 推荐的集成层形状

推荐保留一个被动的生命周期分发面，加上两个语义独立的主动接缝：

```text
UniKernel / Owner pipeline
        │
        ├── immutable lifecycle event
        ▼
Runtime Capability Integration
        ├── Observation / Measurement modules
        ├── Advisory consultation seam
        └── Effect capability registration seam
```

被动生命周期面可以由一个内部 Capability Hub 承接，再按模块声明的订阅阶段分发。Hub 负责顺序、隔离、预算和 correlation；业务模块不直接依赖 `UniKernel` 内部对象。

主动接缝必须保持窄：

- Advisory 只在明确的咨询阶段被调用，并返回受限结果；
- Effect capability 只在 Effect Boundary 注册和执行；
- 两者都不能把任意回调插入 Kernel 主循环。

## 6. 接缝必须冻结的语义

实现前应先冻结以下内容，而不是先决定 C# 接口名：

1. 生命周期事件是否在 Owner commit 前、commit 后或两者都可见；
2. 模块接收的是 Raw Artifact、Evidence、Revision、Slice 还是专门的只读投影；
3. 能力模块是同步参与当前调用，还是只允许 post-commit 异步消费；
4. 模块异常、超时、容量不足时是否隔离、降级或 fail-closed；
5. 模块结果属于诊断、建议、证据还是 Product Contract 的验收输入；
6. 一个模块是否允许跨 Run 持有状态；
7. 模块结果如何携带 provenance、版本和 correlation；
8. 能力模块的输出是否可以被后续模块消费，以及是否允许形成链式依赖。

## 7. 当前不应做的事情

- 不在 `UniKernel` 中加入 `List<Action<object>>` 或任意字典 Hook；
- 不把业务检查字段继续堆进 `SettingsTraversalLiveFeed.TraceEntry`；
- 不让能力模块直接取得 `WorldModel`、`RunModel` 或 `EffectBoundary` 的可变引用；
- 不把所有能力都建成 Agent Tool；
- 不把 Trace 或 Metrics 变成第二套 Product truth；
- 不因“未来可能有很多能力”提前建立开放式插件协议、跨进程事件总线或持久化平台。

## 8. 需要 Grill 的决策前沿

以下问题按依赖顺序排列。回答前不应冻结具体接口：

1. 第一阶段是否只允许 Observation / Measurement 两类被动能力进入 Kernel，Advisory 和 Effect Capability 另立后续决策？
2. 生命周期事件的首要消费面是 `ObservationAccepted` / `BeliefRevisionPublished`，还是必须把 `Intent` / `Effect` / `PostAction` 一并纳入第一版接缝？
3. 能力模块的结果是否统一属于非权威 Integration Evidence，还是允许某些模块结果直接进入 Contract Acceptance？
4. 能力模块是否允许持有 Run 内状态，还是每次事件都必须无状态计算？
5. 多个能力模块之间是否允许结果互相消费，还是第一版严格禁止链式依赖？

## 9. 现阶段结论

当前最值得预留的不是“语言检查 Hook”或“性能 Hook”，而是一个受生命周期、Owner、correlation 和输出类型约束的能力集成面。

`ObservationAccepted`、`BeliefRevisionPublished`、`EffectReceiptProduced`、`PostActionVerified` 是最有买家证据的四个候选节点；其余节点先作为候选登记，等真实能力场景出现后再决定是否升格。

本文保持 `Authority: NONE`。完成 Grill 并达成共同理解之前，不创建 ADR、不修改 Kernel、不冻结公共接口。
