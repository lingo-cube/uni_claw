> Status: DRAFT
> Authority: NONE
> Grill: ACCEPTED · 2026-10-04
> 修订 2026-10-06（所有者命名裁决）：组合能力更名 **Text Semantic Perception
> → UniPerception**（capability id `text.semantic` → `uni.perception`）。
> 理由：语义识别只是该组件能力之一，它同时提供 UI 元素识别（双协议）；
> 命名不得偏向单一能力。旧名在历史记录中保留为别名。同裁决：该组件为
> 本仓自定义组件，行为默认异步（Fast+XML 先行入世界模型，Slow 晚到补语义）。
> 日期: 2026-10-03 · 方法: 现有调用链盘点 × 能力买家场景 × 接缝推导 × 对抗性审阅

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
| `RunActivated` / `RunResumed` | 合法 activation 或恢复后 | 外部观察器建立会话、设备对时 | 否 |
| `ObservationCaptureStarted` | Host 开始采集 screenshot/XML/其他输入时 | 采集器、摄像头、网络抓包、设备遥测 | 否 |
| `ObservationCaptured` | Raw Artifact 采集完成后 | 原始证据归档、采集耗时、外部画面对齐 | 否 |
| `ObservationAccepted` | Evidence Ledger admission 成功后 | 语言合规、可访问性、审计、证据质量 | 否 |
| `BeliefRevisionPublished` | WorldModel reconciliation 后 | 世界投影检查、覆盖检查、审计 | 否 |
| `SliceDerived` | Consumer Slice 派生后 | 页面/容器级业务检查 | 否 |
| `ConsultationStarted` / `ConsultationCompleted` | Slow/Agent consultation 前后 | 模型耗时、外部决策服务、超时诊断 | 否 |
| `IntentIssued` | Control Loop 签发 Intent 后 | 策略诊断、计划审计 | 否 |
| `BindingResolved` | Effect Boundary 形成或拒绝 Binding 后 | 目标绑定诊断、操作审计 | 否 |
| `AssuranceJudged` | Runtime Assurance 完成后 | 授权检查审计、拒绝分类 | 否 |
| `EffectDispatchStarted` | Driver 调用前 | 投递测量、预算计时 | 否 |
| `EffectReceiptProduced` | Driver 返回并形成 Receipt 后 | 投递结果测量、执行审计 | 否 |
| `PostActionObservationStarted` / `PostActionVerified` | 动作后重新观察及验证完成后 | 交互响应测量、视觉回归、状态验证 | 否 |
| `WaitEntered` / `ExternalStimulusReceived` | Drive 等待输入或收到外部输入时 | 摄像头/设备旁路、人工介入、异步系统 | 否 |
| `RunStateChanged` | Run Model 完成合法状态迁移后 | 状态观察、资源策略、恢复审计 | 否 |
| `FaultOrSafeStop` | fail-closed、取消、障碍或安全停止时 | 故障分析、环境诊断、恢复审计 | 否 |
| `RunTerminal` | Outcome exactly-once emission 后 | 报告汇总、外部导出、收尾观察 | 否；Host/Harness 资源清理由独立 Teardown 负责 |

所有事件都应使用现有运行关联，而不是由能力模块重新铸造身份。候选 correlation 包括：`RunId`、`IntentId`、`BindingId`、`ReceiptId`、`CaptureId`、`ObservationCycleId`、`RevisionId`。外部观测器可以附加自己的 `ObservationId`、设备时间、帧号或传感器时间，但不能用自己的时钟改写 Runtime 的 canonical 时间语义。

### 3.1 Kernel 时间与外部测量的分工

Kernel 事件的时间点只证明 Runtime 某个调用边界发生了；它不证明用户在屏幕上已经看到了变化，也不证明设备已经稳定。内部 Kernel/Host 测量和摄像头、屏幕录制、设备端帧时间、系统 trace 等外挂测量都应被支持。集成接缝应允许这些测量通过 correlation 回挂到 Effect、Receipt 和 PostAction Observation；两类测量可以并存、互相校准，但不能强迫所有测量都包在 Kernel 调用栈内，也不能把任一来源自动升格为用户可见响应的唯一权威。

### 3.2 生命周期事实的发布语义

生命周期事件不是 Hub 推断出来的状态，也不是 Trace 的替代物。每个 canonical Owner 在自己的成功提交点之后发布对应的 immutable lifecycle fact：Evidence Ledger 发布 `ObservationAccepted`，World Model 发布 `BeliefRevisionPublished`，Effect Boundary 发布 `EffectReceiptProduced`，Run Model 发布 Run 状态和 terminal 事实；Host/Harness 只发布自己的环境事实。发布失败、模块未消费或事件丢失不回写 Owner，也不改变 canonical state。

开始/结束成对的过程必须表达显式终点：`Completed`、`Failed`、`Cancelled`、`TimedOut` 或 `Unknown`。只有开始事件没有失败或未知终点的过程不能作为完整测量样本。Run 状态变化使用显式 typed fact，不用 `FaultOrSafeStop` 这一万能事件替代。

### 3.3 Envelope、顺序与外部关联

统一 Envelope 至少包含 `EventId`、`RunId`、可选的 `ParentEventId`/领域 correlation、单调的 Run 内 `EventSeq`、`SourceOwner`、`SchemaVersion`、事件种类和结果状态。`EventSeq` 由 Runtime/Host 的事实发布者分配；Hub 不重排为新的 canonical 顺序。Runtime elapsed、Host monotonic time、外部设备时间和传感器时间分开表达，不能把任何 wall-clock 字段称为 canonical clock。

外部测量必须先取得显式 correlation token 或 association record，再回挂到 Effect、Receipt、Capture 或 PostAction Observation。禁止通过时间窗口猜测归属；迟到、重复、无法关联和 Run terminal 后到达的样本都必须有明确 disposition。

### 3.4 收尾与 Hub 故障

`RunTerminal` 是被动通知，不是资源清理命令。摄像头、录屏、设备连接和其他 Fixture 的 Teardown 由 Host/Harness 自己持有，必须幂等，并独立于 Hub 是否收到或完成 terminal 事件。Hub 只负责把收尾事实交给观察、报告或诊断模块；模块失败不能阻止 Host 资源收尾。

## 4. 能力类别

业界一手资料研究见[自动化测试能力接缝研究 v0.1](../research/automation-capability-hooks-industry-v0.1.md)。该研究只提供模式输入，不改变 UniClaw 的 Owner / Authority 语义。

### 4.0 Capability Hub 的职责

Capability Hub 是能力管理面，不是新的 Product Owner。它负责：

- 能力 descriptor、版本、分类和协议选择；
- 显式注册、启用、停用、drain、cleanup 和健康状态；
- 订阅范围、作用域、事件分发、顺序、背压和能力级失败隔离；
- 将统一 lifecycle event 转发为对应分类的协议输入；
- 收集模块输出的 correlation、provenance 和 Artifact Reference。

Hub 不负责业务规则、语言判断、性能算法、WorldModel 写入、Assurance 判定或 Effect 执行授权。

### 4.0.1 现有能力的候选归类

前面六类不足以覆盖当前实现。当前代码中已经存在主动观察源、诊断接收器和 Host 夹具；因此暂时扩展为以下九类候选。分类是协议选择输入，不是实现层级，也不表示这些分类已经冻结。

| 候选分类 | 当前/计划能力 | 适合的协议 | 说明 |
|---|---|---|---|
| **Observation Source / Observation Proposal Producer** | `UiAutomatorDump`、`UiHierarchyOccurrenceStrategy`、`FrameOccurrenceStrategy`、Fast Perception、Slow provider、截图/XML/视觉采集 | `Capture → normalize/project → ObservationProposal` | 主动获取或产生观察输入；不能直接写 Evidence Ledger / WorldModel |
| **Observer / Telemetry** | `IRunTrace`、`TraceCatalog`、`RuntimeStageMetrics` 的 Adapter、运行审计监听器 | 单向 lifecycle event 订阅 | 只观察，不产生 Product claim，不回流决策；现有 Trace/Metrics Owner 不迁移 |
| **Inspector / Validator** | 语言合规、可访问性、视觉回归、Popup/Route/Viewport 检查、覆盖审计 | 只读 Observation/Slice/Revision → Finding | 结果默认是非权威 Finding；不能直接授权动作 |
| **Measurement / Sensor** | 内部 dispatch/receipt/verification 计时、摄像头、录屏、设备 trace、网络性能采集 | Operation / Point Event → MeasurementSample，允许异步回传 | 内部和外部测量并存；不能把单一来源自动升格为用户可见响应权威 |
| **Advisory / Planner** | `ConsultAgent`、`SettingsCoverageDirector`、`AgentPlanPolicy`、Slow 的策略建议投影 | 有界 request/response → Advisory / Plan | 交回既有 Control / Agent 决策缝；不能直接输出 Effect 授权 |
| **Effect Provider** | `AdbEffectDriver`、Ego Browser delivery、未来新的 effect class | Effect Boundary 注册 → Binding/Gate/Receipt | 执行能力必须经过既有 Effect Boundary |
| **Artifact / Report Sink** | screenshot/XML raw artifact 落盘、`StepArtifact`、trace/report writer、外部视频/JSON 保存 | Artifact Reference → append/finalize/retention | 保存附件和报告；Artifact 不因存在而成为 Product fact，也不直接产生 Evidence Record |
| **Fixture / Environment Manager** | 设备连接、Host setup/teardown、摄像头/录屏启动与清理、环境 identity/health | `Setup → Ready/Failed → Teardown` | 属于 Host/Harness；不拥有业务 Run 成功或 Product truth |
| **Harness Stimulus / Fault Injector** | 测试延迟、超时、断连、错误回执、外部输入注入 | Test-only stimulus → controlled runtime input | 只存在于 Harness；不能作为普通 Product capability 启用 |

`Slow` 需要拆开看：Slow provider 属于 `Observation Source / Observation Proposal Producer`；由 Control 授权的 Slow consultation 属于 `Advisory / Planner`；两者可以由同一个 Host adapter 实现，但在 Hub 中应声明为两个协议角色，不能共享一个无边界的“Slow Hook”。

对于 Fast + Slow Text，Product Capability Registry 注册的是 `UniPerception` 组合能力（2026-10-06 前旧名 Text Semantic Perception），Slow Text 不独立注册；需要文本语义解释时，其依赖链固定为 `Fast（YOLO + OCR）→ Slow Text`。缺少可用的 YOLO/OCR 前置依据或关联不完整时，不调用 Slow Text、不产生该阶段的语义 proposal；Fast 已有的合法输出仍按既有路径处理。YOLO/OCR 正常完成但检测结果为空不等于能力缺失，是否足以回答具体 claim 需要另行定义。`Slow Visual` 和其他感知能力可保持独立。

该决定限定组合能力的装配边界，不要求每次 Fast 调用都执行 Slow Text，也不把 Slow Text 的模型适配与 transport 实现合并进 Fast。

### 4.0.2 感知能力分类与协议角色（CAP-001 S2 冻结）

感知能力按外部买方问题分为两个协议接口；`Fast`、`Slow`、YOLO、OCR、XML 和视觉模型是实现来源或内部 realization，不是这两个接口的替代命名。

| 协议接口 | 买方问题 | 输入投影 | 输出投影 | 权威边界 |
|---|---|---|---|---|
| **Semantic Perception** | 这个界面或元素表达了什么语义？ | 同一 `CaptureId` / `ObservationCycleId` 下的有界 UI 元素依据、OCR/视觉依据、已接受引用和 claim | `SemanticObservationProposal`：语义 claim、来源 lineage、`PerceptionAssessment` | 只产生 proposal，经 P2/P3；不拥有 Evidence、WorldModel 或 Gate |
| **UI Element Perception** | 当前 capture 中有哪些元素、属性、几何和关联？ | XML/Hierarchy、YOLO/视觉检测、OCR token 和 capture metadata | `UiElementObservationProposal`：capture-local occurrence、字段、bounds、关联和 `PerceptionAssessment` | occurrence 只在 capture 内有效；不生成跨 revision identity 或 Effect binding |

协议结果必须带 `CaptureId`、`ObservationCycleId`、`SessionCorrelation`、来源 lineage 和能力状态。结果不完整时保留失败/不确定语义，不用空列表冒充“没有元素”或“没有语义”。

`UniPerception`（旧名 Text Semantic Perception）是注册级复合能力：它至少声明 `Semantic Perception`，并在 Fast 投影能够产出 capture-local 元素依据时同时声明 `UI Element Perception`。后者来自 Fast 的 YOLO/OCR/结构投影，不意味着 Slow Text 自身成为 UI 元素能力；Slow Text 只负责在有界前置依据上完成语义解释。`Slow Visual` 可以独立注册，并按实现能力声明一个或两个协议接口。

`PerceptionAssessment` 是两个协议共用的非权威评估包，最小字段语义如下：

| 字段 | 允许语义 | 约束 |
|---|---|---|
| `Disposition` | `Supported`、`Partial`、`Unknown`、`Conflicted`、`Unsupported`、`Unaligned`、`Unavailable` | 描述这次输出能否被消费；不是 Product truth |
| `Coverage` | `CompleteWithinDeclaredSurface`、`Partial`、`Unknown`、`SourceUnavailable` | 描述来源覆盖面；空检测与来源缺失分开 |
| `Uncertainty` | `None`、`Bounded`、`Material` | 描述剩余不确定性；不能被压缩成单个分数 |
| `AssociationQuality` | `Unique`、`ManyToOne`、`OneToMany`、`Ambiguous`、`Unassociated` | 关联不唯一时，字段级融合降为 Unknown/Conflicted |
| `SourcePath` | 有序的来源角色、provider/ref 和 lineage | 必须可回溯到 capture/cycle；不得只留一个 opaque 来源名 |
| `ProviderConfidence` | 可选的 provider 原始置信度及其 provider/model/version 来源 | 只作校准输入或诊断；不能直接成为 Admission、Assurance 或 Effect Gate |

Provider 没有置信度时，`ProviderConfidence` 为空是合法状态；不得把缺失置信度解释成零。任何需要把评估升级为 Product Contract 或 Gate 输入的场景，都要另立 Promotion 协议。

### 4.0.3 来源与字段级融合矩阵

XML/Hierarchy、YOLO、OCR 和视觉模型不做数组级拼接，而是在相同 capture/cycle 和明确 element association 下按字段融合：

| 来源 | 主要字段 | 语义地位 |
|---|---|---|
| XML / Hierarchy | occurrence 结构、role/class、resource-id、enabled、checked、selected、focus、visibility、`declaredText`、声明 bounds | UI Element 结构来源；字段缺失是 Unknown/Unsupported，不是 false |
| YOLO / Visual | rendered bounds、视觉对象、appearance、icon、遮挡和 rendered state | UI Element 渲染/几何来源；不能单独制造结构 identity |
| OCR | `renderedText`、token、文本区域与视觉关联 | 文本渲染来源；不覆盖 XML 的 `declaredText` |
| Slow Text | 基于 Fast 前置依据的语义解释、分类或 claim | Semantic Perception realization；不单独成为 UI Element 或 Evidence authority |
| Slow Visual | 视觉语义和/或 UI 元素观察 | 独立 capability；按注册声明实现一个或两个协议 |

`declaredText` 与 `renderedText` 必须保留为不同字段；二者一致、冲突或一方缺失都进入 `PerceptionAssessment`，不静默覆盖。缺少唯一 association、capture/cycle 未对齐、坐标空间不兼容或 lineage 无法验证时，融合结果为 `Unknown`、`Conflicted` 或 `Unaligned`，不得通过时间窗口猜测关联。

### 4.0.4 信任域与注册域

Capability Hub 是一个架构管理角色，不等于一个跨域可加载的注册表。Composition Root 必须建立彼此隔离的注册域：

| 注册域 | 管理能力 | 允许的输出面 |
|---|---|---|
| Product Capability Registry | Text Semantic Perception、Slow Visual、Perception / Acquisition / Grounding / Effect Provider | 既有 P2、P9、P14、P15 和 Effect Boundary 接缝 |
| Runtime Integration Registry | Observer / Inspector / Measurement / Artifact Report Sink | lifecycle fact、Finding、MeasurementSample、Artifact Reference |
| Harness Capability Registry | Fixture / Environment Manager / Stimulus / Fault Injector | Host/Test 生命周期和受控测试输入 |

三类注册域可以复用同一组 Host-neutral Envelope 和 correlation 词汇，但不能共享可变注册状态、权限或装配依赖。跨域组合必须经过显式 Adapter 或 Promotion；Harness capability 不能被 Product Host 隐式加载，Runtime Integration finding 也不能直接成为 Product fact。

### 4.1 Observation Capability

消费已接受的观察、Slice 或 Revision，产生 `Finding`、`Measurement` 或指向既有 canonical Evidence Record 的引用；它不创建 Evidence Record。

示例：

- 预期中文但观察到英文；
- 菜单项缺少副标题；
- 当前页面缺少要求的结构；
- 某项出现频率或覆盖率异常。

这类能力不能阻止动作，除非另有明确的 Runtime Contract / Assurance 语义接管它。

### 4.2 UniPerception（旧名 Text Semantic Perception）

这是一个组合能力，而不是一个新的 Evidence 或 World Owner。它对外至少提供 Semantic Perception，并可把 Fast 阶段的 YOLO/OCR/结构投影作为 UI Element Perception 输出；它消费同一 capture/cycle 下由 Fast 阶段产生的 YOLO 与 OCR 依据，再按 bounded budget 调用 Slow Text。两阶段的 provenance、correlation、coverage、`PerceptionAssessment` 和失败状态必须保留在组合结果中。只有形成合法 `ObservationProposal` 后，结果才进入既有 P2。

YOLO/OCR 前置不满足时，不进行 Slow Text 语义解释，结果需显式表达 `Unavailable`、`Unknown` 或 `Unaligned`；正常空检测仍与能力缺失分开。现有 P2 admission 和 provider confidence 不自动成为 truth 的规则保持不变；`PerceptionAssessment` 只提供结构化非权威评估。

**已发现但未实现的接线缺口**：`SlowContracts.cs` 中的 `SemanticReasoningContext` 主要保存 Evidence ID；`OpenCodeSlowRealization.SerializeContext` 序列化这些引用而不解析实际 YOLO/OCR 内容；Settings Host 的 `BuildSlowRequest` 当前传入空 EvidenceIds。因此，已有 Slow request 并不能证明模型实际收到前置感知信息。后续需明确有界 YOLO/OCR 输入投影、同一 capture/cycle 的关联与 lineage、以及原始输出和已接受 Evidence 的对应路径；不得以一个 `FastAvailable` 布尔值或不透明 Evidence ID 代替实际上下文。

### 4.3 Advisory Capability

消费当前 Runtime View 和既有证据，产生有限的 `Advisory` 或计划建议，交回现有 Control / Agent 决策缝。

这类能力不能直接调用 Driver，不能输出坐标或绕过重新 Grounding。

### 4.4 Measurement Capability

消费生命周期时间点和关联事实，产生非权威的 `MeasurementSample`。

它只能测量已发生的事情，不能因为耗时超过阈值而自行重试、改变策略或授权动作。

### 4.5 Effect Capability

为新的 effect class 或外部执行通道提供显式 realization。它必须注册到 Effect Boundary 的既有 Gate / Binding / Receipt 链，不能通过观察 Hook 直接执行。

## 5. 推荐的集成层形状

推荐保留一个 Host-neutral 的被动生命周期事实面，加上按信任域隔离的管理接缝和两个语义独立的主动接缝：

```text
Canonical Owners / Host
        │
        ├── post-commit immutable lifecycle fact
        ▼
Host-neutral lifecycle fact seam
        ├── Product Capability Registry
        ├── Runtime Integration Registry
        └── Harness Capability Registry
                ├── passive observers / measurements
                ├── Advisory consultation seam（显式调用）
                └── Effect capability seam（只经 Effect Boundary）
```

每个注册域内的 Capability Hub 负责能力注册、作用域、分发、背压和模块级失败隔离；Hub 不创建事实、不重排 canonical 顺序，也不持有 Product/Harness 的共享可变状态。业务模块不直接依赖 `UniKernel` 内部对象。

主动接缝必须保持窄：

- Advisory 只在明确的咨询阶段被调用，并返回受限结果；
- Effect capability 只在 Effect Boundary 注册和执行；
- 两者都不能把任意回调插入 Kernel 主循环。

## 5.1 Capability Hub 的架构语义地位

仓库已有 canonical 架构术语 **Capability Plane**：它承载 Perception、Grounding Provider、Effect Provider 等可替换 Product capabilities，并通过 P2、P9、P14、P15 等协议与 Kernel Owner 交互。本设计不再创建平行的“Capability Integration Plane”。

**Capability Hub 是 Capability Plane 能力管理角色的管理/装配 realization，不是新的 L2、Owner 或 Authority。** Product Capability Hub 管理 Product Capability Plane 的装配；Runtime Integration Hub 和 Harness Capability Hub 使用同一管理语义，但属于不同注册域。每个 Hub 只拥有能力注册、分类、协议选择、作用域、健康、背压和生命周期管理；能力本身拥有各自的实现和输出；Evidence Ledger、World Model、Run Model、Control、Assurance、Effect Boundary 继续保留原 Owner。

Hub 的管理范围需要按信任分区理解：

```text
Capability management role（共享语义）
├── Product Capability Plane
│   └── Product Capability Registry / Hub
├── Runtime Integration Registry / Hub
└── Harness Capability Registry / Hub
```

三类注册域可以共享 Host-neutral lifecycle fact 和 correlation 语义，但不能共享权限或注册状态：Harness 能力不能获得 Product Runtime 的 Effect 或 World authority；Runtime Integration 能力也不能把 Finding / Measurement 自动升级成 Product fact。

因此，Capability Hub 需要被提升为架构文档中的**能力管理角色**，但不应被提升为新的产品 Owner。ADR 记录的是“为什么 Hub 属于 Capability Plane 能力管理语义，以及为什么 Product / Runtime Integration / Harness 必须使用独立注册域”，不冻结具体类名、进程数或部署结构。

## 6. 接缝必须冻结的语义

实现前应先冻结以下内容，而不是先决定 C# 接口名：

1. 生命周期事件是否在 Owner commit 前、commit 后或两者都可见；
2. 模块接收的是 Raw Artifact、Evidence、Revision、Slice 还是专门的只读投影；
3. 能力模块是同步参与当前调用，还是只允许 post-commit 异步消费；
4. 模块异常、超时、容量不足时是否隔离、降级或 fail-closed；
5. 模块结果属于诊断、建议、证据还是 Product Contract 的验收输入；
6. 一个模块是否允许跨 Run 持有状态；
7. 模块结果如何携带 provenance、版本和 correlation；
8. 能力模块的输出是否可以被后续模块消费，以及是否允许形成链式依赖；
9. Hub 是否只做分发与短期关联，Trace/Metrics 是否继续由现有专用 Owner/Adapter 管理；
10. Host/Harness 的 Teardown 是否独立于 Hub 通知，并以幂等协议执法。

## 7. 当前不应做的事情

- 不在 `UniKernel` 中加入 `List<Action<object>>` 或任意字典 Hook；
- 不把业务检查字段继续堆进 `SettingsTraversalLiveFeed.TraceEntry`；
- 不让能力模块直接取得 `WorldModel`、`RunModel` 或 `EffectBoundary` 的可变引用；
- 不把所有能力都建成 Agent Tool；
- 不把 Trace 或 Metrics 变成第二套 Product truth；
- 不把 `IRunTrace` 或 `RuntimeStageMetrics` 改造成 Hub 的通用 Owner；只能通过显式 Adapter 消费生命周期事实；
- 不让 `RunTerminal` 通知承担 Host/Harness 资源清理；
- 不因“未来可能有很多能力”提前建立开放式插件协议、跨进程事件总线或持久化平台。

## 8. 已闭合的接缝语义检查表

以下语义已通过 Grill；它们约束后续接口设计，但不等于公共 C# 接口已经冻结：

1. Capability Management 是否只作为共享语义；Product、Runtime Integration、Harness 是否使用独立注册域与装配根？
2. 核心事件目录是否封闭，能力模块是否只能在自己的 Adapter / module stream 内增加细节事件？
3. Observation / Measurement 是否明确禁止回流；Advisory / Effect 是否必须经过独立的主动接缝？
4. 统一 Envelope 是否 Host-neutral，平台 payload 是否完全由 Adapter 拥有？
5. canonical Owner 是否在 post-commit 点发布 lifecycle fact；Hub 是否禁止推断、重排和改写事实？
6. 背压、丢失、迟到和重复事件是否由各注册域按交付等级显式报告？
7. Artifact / Report 的持久化是否永远属于 Host / Artifact Store，Hub 只保留短期关联？
8. Simulation 与 Product Runtime 是否必须发布同一组 Host-neutral lifecycle facts？

## 9. 现阶段结论

当前最值得预留的不是“语言检查 Hook”或“性能 Hook”，而是一个受生命周期、Owner、correlation 和输出类型约束的能力集成面。

`ObservationAccepted`、`BeliefRevisionPublished`、`EffectReceiptProduced`、`PostActionVerified` 是最有买家证据的四个候选节点。第一版只实现 Observation / Inspector / Measurement 的被动接缝；Run activation/resume、Raw Artifact 采集、Slow/Agent consultation、等待/外部输入、故障/安全停止和 terminal 先登记为候选接缝，不自动进入同一个 Product Capability Registry。

## 10. Grill 已收敛决策（仍非实现授权）

- Measurement 同时支持 Kernel/Host 内部测量与外部传感器测量；两者共享 correlation，不互相替代。
- 第一版登记完整生命周期目录，但只实现少数已有买家的能力模块。
- `ObservationCaptured` 与 `ObservationAccepted` 同时保留，分别服务原始附件关联与已接受观察检查。
- `Text Semantic Perception` 是 Product Capability Registry 的组合能力；Slow Text 不独立注册，固定依赖同一 capture/cycle 的 YOLO 与 OCR 前置。
- YOLO/OCR 前置依据不可用或关联不完整时，不调用 Slow Text、不产生 Slow Text 语义 proposal；不抹除已有合法 Fast 输出，也不把正常空检测自动判为能力缺失。
- 外部观察必须显式关联，Kernel 不根据时间窗口自动猜测归属。
- Artifact/Attachment 与 Evidence 分层；Artifact 不因存在而自动成为 Product fact；Artifact Sink 不直接产生 Evidence Record。
- Setup/Teardown 属于 Host/Harness 生命周期；Kernel 消费其状态和环境 identity。
- Host/Harness Teardown 独立于 Hub 通知并且幂等；`RunTerminal` 只是被动通知，不是资源清理命令。
- 能力模块故障默认隔离，不改变主 Run；升级为 Gate 需要单独的 Promotion 决策。
- canonical Owner 在 post-commit 点发布 lifecycle fact；能力模块异步消费；主 authority path 不等待模块完成，Hub 不推断、重排或改写事实。
- 同一 Run 内事件使用单调序号；队列丢失、模块失败和外部迟到必须显式表达，禁止静默丢弃。
- 所有能力事件共享统一 Envelope；能力只扩展自己的 payload，不重复定义 Run/Action/Observation correlation。
- Envelope 至少包含 EventId、RunId、Parent/Correlation、Run 内 EventSeq、SourceOwner、SchemaVersion 和结果状态；Runtime elapsed 与外部设备时间分开表达，不引入 canonical wall-clock。
- 能力模块由 Composition Root 显式注册并声明作用域、版本、订阅范围和失败策略。
- 测试延迟、超时、断连和错误回执通过独立 Harness Stimulus/Fault Injection 接缝提供。
- 大型截图、视频、摄像头帧和网络包只以 Artifact Reference 进入事件，内容由 Host/Artifact Store 保存。
- 有开始和结束的过程使用 Operation 事件对；单点事实使用 Point Event；二者不折叠成一个万能事件。
- Capability Management 作为共享架构语义；Product、Runtime Integration、Harness 各由自己的 Composition Root 创建注册域和 Hub；Kernel 只依赖窄的被动生命周期发布接缝。
- Kernel 核心事件目录保持受控；模块细节事件留在模块或 Adapter 内部。
- Observation / Measurement 结果第一版不回流修改 Control、Assurance 或 Effect。
- 统一 Envelope 属于 Host-neutral 共享面，平台 payload 留在 Adapter。
- 背压不阻塞 authority path；非关键事件丢失必须显式诊断。
- Hub 只做分发和短期关联，Artifact/Report 持久化由 Host/Artifact Store 负责。
- `IRunTrace` 与 `RuntimeStageMetrics` 保持各自既有 Owner/Adapter；Hub 只能通过显式 Adapter 投影事实，不能成为第二套 Trace/Metrics truth。
- Simulation 与 Product Runtime 发布同一组 Host-neutral lifecycle facts，环境差异由 Adapter 补充。
- 一个模块声明一个主分类；需要多种协议时通过多个显式 Adapter 组合。
- 各注册域按分类选择协议：Observer 单向订阅、Inspector 只读输入到 Finding、Measurement/Sensor 异步样本、Advisory 有界咨询、Effect Provider 经 Effect Boundary、Fixture/Harness 走 Host/Test 生命周期。
- 各注册域管理能力状态 `Declared → Registered → Ready → Active → Draining → Closed`，并显式表达 `Failed` / `Quarantined`；状态、版本、作用域、订阅范围和健康信息不跨注册域共享。
- Core 只保留受控分类；新增分类需要独立架构决策，不允许模块自声明任意协议。
- 分类之间禁止隐式链式调用；需要组合时建立显式 Aggregator 或 Promotion 协议。
- `Observation Source / Observation Proposal Producer` 不拥有 Evidence admission；需要把 Finding / Measurement 升级为 Product fact 时，必须另立 Promotion 协议和独立 Gate。

本文保持 `Authority: NONE`。本节冻结的是买方语义、来源边界和评估语义，不冻结具体 C# 类型、provider transport 或部署结构。
