> Status: FROZEN / PROTOCOL GUIDE v0.1（修订必须经 change；CAP-010 升格——先例：perception-provider-baseline 的 COMPONENT-BASELINE 体例）
> Authority: COMPONENT（协议指南：能力定制/集成/开发的 canonical 路径，被 capability-component skill 引用；架构权威在 ADR-0035/0038 与产品基线，冲突时以其为准）
> Applies to: Product Capability Registry、Runtime Integration Registry、Harness Capability Registry
> Date: 2026-10-04
> Prerequisites: [Capability Hub 与信任域注册 ADR](../adr/0035-capability-management-hub-trust-scoped-registries.md)、[Runtime 能力集成接缝](../design/runtime-capability-integration-seams-v0.1.md)

# Capability 定制化、集成与开发协议指南 v0.1

## 0. 这份文档解决什么问题

这份文档规定新增一项 Capability 时，如何判断它属于哪类能力、如何选择协议、如何进入正确的 Registry、如何实现 Adapter、如何接入生命周期事实，以及如何证明它没有取得不属于自己的权威。

它不是一个开放式插件系统，也不是新的任务系统。它把已经冻结的 Capability Hub、感知协议、P2/P3 和三类注册域整理成一条开发路径：

```text
业务需求
  → 能力买方与输出分类
  → 协议选择
  → 注册域与 Capability Descriptor
  → Adapter / Implementation
  → lifecycle fact 订阅或主动调用
  → Finding / Measurement / Proposal / Receipt
  → 对应 Owner 验收
```

这份文档冻结的是定制和集成的语义规则，不冻结具体 C# 类型、进程部署、provider transport 或序列化格式。若新能力需要突破本文的权威边界，必须先建立独立架构决策和 Promotion 协议。

## 1. Capability 的基本定义

Capability 是一个可替换的能力模块。它必须有一个明确的买方问题、一个主分类、一个协议接口、一组输入投影、一组输出类型和一个失败策略。

Capability Hub 只管理以下内容：

- descriptor、版本、分类、协议声明和依赖；
- 注册域、作用域、启用状态、健康状态和生命周期；
- lifecycle fact 的订阅范围、分发、背压和能力级故障隔离；
- Composition Root 中的显式装配关系。

Capability Hub 不拥有以下内容：

- Evidence Ledger、WorldModel、Run Model、Control、Assurance 或 Effect Authority；
- 语言判断、性能算法、视觉模型的 Product 结论；
- 任意模块的自动编排权；
- 通过回调修改主流程的权力。

一个模块可以有多个 Adapter，但只能声明一个主分类。需要多个协议时，使用多个显式协议角色或 Adapter 组合，禁止用一个“万能 Hook”隐藏不同的失败和权威语义。

## 2. 先判断买方，再定义能力

新增能力必须先回答五个问题：

| 问题 | 必须明确的答案 |
|---|---|
| 谁消费结果？ | Product Runtime、Runtime Integration 还是 Harness |
| 消费什么输入？ | lifecycle fact、Raw Artifact、Observation、Evidence、Revision、Slice、Effect request 或测试刺激 |
| 结果是什么？ | Proposal、Finding、MeasurementSample、Advisory、Receipt、Artifact Reference、环境状态或受控测试输入 |
| 结果是否改变主权威？ | 默认不改变；若要改变，必须指定现有 Owner 和 Promotion 协议 |
| 失败如何处理？ | 隔离、降级、Unknown、Unavailable、Fail-closed、取消或重试；不能留给调用方猜测 |

如果无法回答“谁消费结果”，不要先定义接口。如果结果只是记录、分析或报告，通常属于 Runtime Integration；如果结果要进入 Product 的观察链，才考虑 Product Capability；如果结果只服务测试注入，属于 Harness。

## 3. 协议选择表

| 主分类 | 买方问题 | 输入协议 | 输出协议 | 默认调用方式 | 默认权威边界 |
|---|---|---|---|---|---|
| **Acquisition / Observation Source** | 如何取得原始观察输入？ | `CaptureRequest` 或环境采集请求 | `ArtifactReference`、采集元数据、覆盖和状态 | 显式调用 | 不能直接写 Evidence 或 WorldModel |
| **Semantic Perception** | 这个界面或元素表达了什么语义？ | 同一 capture/cycle 的有界依据和 claim | `SemanticObservationProposal` + `PerceptionAssessment` | 有界主动调用或指定生命周期消费 | 结果必须经 P2/P3 |
| **UI Element Perception** | 当前 capture 中有哪些元素、属性和几何？ | XML/Hierarchy、YOLO、OCR、视觉和 capture metadata | `UiElementObservationProposal` + `PerceptionAssessment` | 有界主动调用或指定生命周期消费 | occurrence 只在 capture 内有效 |
| **Observer / Telemetry** | 某个 Owner 何时提交了什么事实？ | immutable lifecycle fact | 诊断或遥测记录 | post-commit 单向订阅 | 不产生 Product claim，不回流决策 |
| **Inspector / Validator** | 观察结果是否满足检查规则？ | Observation、Revision、Slice 或 Evidence 引用 | `Finding` | post-commit 异步检查 | Finding 默认非权威 |
| **Measurement / Sensor** | 已发生的操作用了多久、是否可观测？ | Operation/Point Event、关联 token、外部样本 | `MeasurementSample` | 同步点测量或异步回传 | 不改变策略、不授权动作 |
| **Advisory / Planner** | 在当前约束下有什么建议？ | 有界 Runtime View、目标和预算 | `Advisory` 或 `PlanProposal` | 显式有界咨询 | 必须返回既有 Control/Agent 决策缝 |
| **Effect Provider** | 如何执行已授权的 Effect？ | Effect Boundary request | Binding、Gate result、Receipt | 经既有 Effect Boundary | 不能自行授权 |
| **Artifact / Report Sink** | 如何保存原始材料或报告？ | Artifact/Report reference | append/finalize/retention receipt | 显式写入 | Artifact 不自动成为 Evidence |
| **Fixture / Environment Manager** | 如何建立和清理测试/设备环境？ | setup 配置和环境约束 | Ready/Failed、句柄、Teardown result | Host/Harness 生命周期 | 不拥有业务成功 |
| **Harness Stimulus / Fault Injector** | 如何注入延迟、断连或错误？ | 测试刺激描述 | 受控输入和注入回执 | 仅 Harness | 不能作为 Product 能力启用 |

`Fast`、`Slow`、YOLO、OCR、XML、视觉模型是实现来源或 realization 名称，不是新的外部买方协议。

## 4. 感知能力的特殊规则

感知对外只有两个协议接口：

### 4.1 Semantic Perception

输入必须能够回溯到同一个 `CaptureId`、`ObservationCycleId` 和 `SessionCorrelation`，并携带有界的 UI 元素、OCR、视觉依据或已接受引用。输出至少包含：

- 语义 claim；
- 来源 lineage；
- `PerceptionAssessment`；
- 能力状态和诊断。

它只产生 proposal，不拥有 Evidence admission、WorldModel reconciliation、Assurance 或 Effect authority。

### 4.2 UI Element Perception

输入可以来自 XML/Hierarchy、YOLO、OCR、视觉模型和 capture metadata。输出包含 capture-local occurrence、字段、bounds、element association 和 `PerceptionAssessment`。

occurrence 不直接成为跨 revision identity，也不直接成为 Effect binding。没有明确 association、capture/cycle 不一致、坐标空间不兼容或 lineage 无法验证时，结果必须表达 `Unknown`、`Conflicted` 或 `Unaligned`。

### 4.3 已冻结的组合关系

- `Text Semantic Perception` 是 Product Registry 中的复合能力。
- `Text Semantic Perception` 的链路是 `Fast YOLO + OCR → Slow Text`。
- Slow Text 不单独注册为 Product 感知能力。
- 缺少、过期或不对齐的 Fast 前置依据时，不调用 Slow Text。
- YOLO/OCR 正常完成但检测为空，与 provider 缺失分开表达；不能把空数组直接解释为能力缺失。
- `Slow Visual` 是独立能力，可以声明 Semantic Perception、UI Element Perception 或两者。
- Slow Visual 不读取 `FastTextBasis`，不经过 `SlowTextGate`；它只要求同一 capture/cycle 的 raw artifact 满足其输入协议。

### 4.4 PerceptionAssessment

两个感知协议共用非权威评估包：

| 字段 | 语义 |
|---|---|
| `Disposition` | `Supported`、`Partial`、`Unknown`、`Conflicted`、`Unsupported`、`Unaligned`、`Unavailable` |
| `Coverage` | `CompleteWithinDeclaredSurface`、`Partial`、`Unknown`、`SourceUnavailable` |
| `Uncertainty` | `None`、`Bounded`、`Material` |
| `AssociationQuality` | `Unique`、`ManyToOne`、`OneToMany`、`Ambiguous`、`Unassociated` |
| `SourcePath` | 来源角色、provider/ref、版本和 lineage |
| `ProviderConfidence` | 可选的原始 provider 置信度及其来源；不能直接成为 Gate 或 Product truth |

## 5. 共享 Envelope 与协议输出

所有 lifecycle fact 和能力输出共享 Host-neutral 的关联语义。能力可以扩展自己的 payload，但不能重复定义 Run、Action、Observation 的身份。

最小 Envelope 语义如下：

```text
EventId
RunId
ParentEventId / DomainCorrelation
EventSeq                 // Run 内单调序号
SourceOwner
SchemaVersion
EventKind
Status                   // Completed / Failed / Cancelled / TimedOut / Unknown
RuntimeElapsed           // 可选，Runtime 单调耗时
HostMonotonicTime        // 可选，Host 单调时间
ExternalDeviceTime       // 可选，设备或传感器时间
Payload                  // 仅由具体协议拥有
```

能力输出还必须说明：

- 输入关联：`CaptureId`、`ObservationCycleId`、`RevisionId`、`ReceiptId` 等适用字段；
- 结果状态和诊断；
- provenance、版本、来源和 Artifact Reference；
- 是否可以异步回传；
- 迟到、重复、无法关联和 terminal 后到达的处理方式。

禁止通过时间窗口猜测外部样本属于哪个操作。外部观察必须先取得显式 correlation token 或 association record。

## 6. Capability Descriptor 如何定制

当前 Hub Descriptor 至少包含以下内容：

```text
CapabilityId
Version
Scope                         // ProductRuntime / RuntimeIntegration / Harness
Protocols[]                   // Name + Version
Dependencies[]               // CapabilityId + VersionRange
Health                        // Unknown / Healthy / Degraded / Unhealthy
Category                      // Composite / Independent / Realization / Source / Adapter
Roles[]                       // ProductProtocol / Realization / Source / Adapter
Relationships[]               // Requires + target capability
```

定制规则：

1. `CapabilityId` 表达买方能力或明确的实现角色，不能把 provider 名称当成 Product 能力名。
2. `Version` 是能力契约版本；provider/model/version 放在 Adapter binding 或 `SourcePath`，不要污染 Product capability identity。
3. `Scope` 必须匹配 Registry：Product、Runtime Integration、Harness 不能越域注册。
4. `Protocols` 只声明实际实现并且版本已冻结的协议；不允许“先声明未来能力”。
5. `Dependencies` 与 `Relationships` 必须一致；复合能力必须显式列出强绑定依赖。
6. 一个模块声明一个主 `Category`；Source 和 Adapter 不能冒充 Product perception。
7. Product 感知能力必须声明 Product protocol role；复合感知必须声明 Semantic Perception。
8. Descriptor 不包含 opaque payload、可变业务状态、Evidence record 或 Effect authorization。

### 6.1 可执行实例与协议接口

Kernel 内部的可执行模块实现 `ICapability`，通过不可变 `Description` 自描述：

```csharp
public interface ICapability
{
    CapabilityDescription Description { get; }
}
```

产品感知协议是派生接口：

```csharp
public interface ISemanticPerception : ICapability { }
public interface IUiElementPerception : ICapability { }
```

Product perception 的实例注册必须同时满足：Descriptor 声明的协议与实例实际
实现的协议接口一致。`ICapabilityHub`/`CapabilityRegistry` 在注册时执行这一校验；
失败时不产生 `Registered` lifecycle fact。Hub 只管理实例解析和生命周期，具体
执行 payload 仍由后续协议 Change 冻结。

DSH Tool 不在 Kernel 中定义。需要向 DSH 暴露 Capability 时，由 Host/DSH
Adapter 将已解析的 `ICapability` 投影为 DSH 原生 ToolDefinition，并交给
DSH 的 `ctx.tools` 管理 scope、schema、guard 和释放。

例如，Text Semantic 的装配语义是：

```text
CapabilityId: product.text-semantic-perception
Category: CompositeProductPerception
Scope: ProductRuntime
Protocols: Semantic Perception@1.0, UI Element Perception@1.0
Dependencies: fast.yolo@..., fast.ocr@..., slow.text@...
Relationships: Requires(fast.yolo), Requires(fast.ocr), Requires(slow.text)
Roles: ProductProtocol(Semantic Perception), ProductProtocol(UI Element Perception), Realization(text-composite)
```

Slow Text 作为内部 realization 或 Adapter 被依赖，不单独作为 Product perception 注册。

## 7. 如何集成一个新 Capability

### 第一步：写买方说明

用一句话写清楚“谁在什么节点需要什么结果”。例如：

> Runtime Integration 的语言 Inspector 在 `ObservationAccepted` 后检查声明文本是否符合当前语言规则，并输出可追溯 Finding。

不要从“我想要一个 Hook”开始；Hook 不是买方。

### 第二步：选择注册域和主分类

- 影响 Product 观察语义的能力进入 Product Registry；
- 只监听、检查、测量或保存报告的能力进入 Runtime Integration Registry；
- 设备/摄像头/录屏资源和故障注入进入 Harness Registry，除非有明确的 Product buyer；
- 跨域组合通过显式 Adapter 或 Promotion，禁止隐式加载。

### 第三步：选择协议，不先定义具体类名

根据第 3 节选择主分类，并写出：输入投影、输出投影、同步/异步、预算、取消、失败状态、关联字段和权威边界。若一个能力同时需要检查和测量，拆成两个显式协议角色，而不是让一个输出对象同时承载 Finding 和 Measurement。

### 第四步：冻结 Descriptor 和依赖关系

先注册能力描述，再实现 transport。Hub 只检查 descriptor、scope、协议、依赖、分类关系和生命周期；不替能力推断模型是否正确。

### 第五步：实现 Adapter

Adapter 负责把平台或 provider 语义投影到冻结协议：

- 取得输入并建立显式 correlation；
- 限制 payload 大小和上下文范围；
- 保留来源、版本、lineage 和 Artifact Reference；
- 将 provider failure 转成协议状态；
- 不把 provider 的业务结论直接写进 Kernel Owner；
- 在 provider 不可用、超时、重复和迟到时保持明确 disposition。

Adapter 不得把 Host transport、session、provider-specific model 字段带进共享 Kernel 协议。

### 第六步：接入 lifecycle fact 或主动调用

Observer、Inspector、Measurement 和 Artifact Sink 默认从 canonical Owner 的 post-commit fact 开始消费。Advisory、Perception、Effect Provider 使用各自的主动接缝，不能通过被动事件订阅伪装成同步调用。

主 authority path 默认不等待被动能力完成。只有协议明确要求同步结果时，才设置独立 bounded wait，并定义超时结果；能力故障不能静默改变主流程。

### 第七步：验证和发布生命周期

至少验证：

- Descriptor 能在正确 Registry 注册，跨域注册失败；
- 生命周期按 `Declared → Registered → Ready → Active → Draining → Closed` 变化；
- `Failed`、`Quarantined`、`Unavailable` 不会被解释成成功；
- 关联正确、错配、迟到、重复、空结果和 provider 缺失互相区分；
- 能力输出没有绕过既有 Owner；
- Teardown 独立且幂等；
- 所有结果可以回溯到输入、版本和来源。

## 8. 如何开发具体功能

### 8.1 语言校验 Inspector

```text
ObservationAccepted
  → read-only text projection
  → language rule adapter
  → Finding
      - expectedLanguage
      - observedText / declaredText
      - affected occurrence / Evidence reference
      - disposition: Pass / Violation / Unknown
      - coverage / diagnostic
```

它不能直接修改菜单文字，不能阻止 Effect，也不能把 `Violation` 自动写成 Product truth。若未来要让语言违规进入 Assurance，必须另立 Promotion 或 Runtime Contract。

### 8.2 性能 Measurement

不要只定义一个“点击耗时”。建议以 Operation 和 Point Event 分开：

```text
EffectDispatchStarted
  → EffectReceiptProduced
  → PostActionObservationStarted
  → first visible change / stable frame
  → PostActionVerified
```

Measurement 输出应说明测量定义、起止点、时钟来源、样本状态、误差和 correlation。摄像头或录屏可以异步提交外部样本，但必须关联到 `ReceiptId`、`CaptureId` 或显式 association record。

### 8.3 摄像头与外挂传感器

摄像头不是一个万能 Hook：

```text
Fixture: camera setup / teardown
Acquisition: frame capture → ArtifactReference
Measurement: frame timestamps → MeasurementSample
Inspector: frame/content check → Finding
Artifact Sink: video retention → ArtifactReference
```

每个模块使用自己的协议角色；共享的是 correlation 和生命周期事实，不共享可变状态。

### 8.4 Text Semantic Perception

```text
Fast YOLO + OCR
  → bounded FastTextBasis
  → SlowTextGate
  → Slow Text realization
  → ObservationProposal
  → existing P2/P3
```

Fast 前置缺失、过期或不对齐时不调用 Slow Text。正常空检测保留为合法空观察语义，不能冒充 provider 不可用。Slow Text 不独立注册为 Product perception。

### 8.5 Slow Visual

```text
RawArtifact(capture/cycle)
  → Slow Visual realization
  → Semantic/UI Element proposal
  → existing P2/P3
```

Slow Visual 不依赖 FastTextBasis，不作为 Text Semantic fallback。它可以根据注册 Descriptor 声明一个或两个感知协议。

## 9. 故障、背压和生命周期协议

| 情况 | 能力模块输出 | Hub / 主流程行为 |
|---|---|---|
| 输入缺失 | `Invalid` / `Unknown` + diagnostic | 不调用后续实现，主流程按既有语义继续或 fail-closed |
| 来源不可用 | `Unavailable` / `SourceUnavailable` | 隔离能力，不伪造空成功 |
| 正常空结果 | `Completed` + 空集合或显式 negative observation（按协议定义） | 不自动解释成能力缺失 |
| 超时 | `TimedOut` | 结束本次操作，不把未完成当成功 |
| 迟到 | `Late` / association disposition | 不追溯改变已完成主流程 |
| 重复 | `Duplicate` / idempotent disposition | 不重复产生事实或测量 |
| 背压 | dropped/queued/rejected diagnostic | 不阻塞 authority path；丢失必须可见 |
| 模块异常 | `Failed` / `Quarantined` | 能力级隔离，不修改 canonical Owner |
| Teardown | `Completed` / `Failed`，要求幂等 | Host/Harness 自己负责，不依赖 Hub 通知 |

有开始和结束的过程必须有终点状态；只有开始事件不能成为完整 Measurement 样本。

## 10. 开发验收清单

提交一个新 Capability 前，必须能回答：

- 买方是谁？
- 主分类和注册域是什么？
- 使用哪个协议，为什么不是其他协议？
- 输入是哪个只读投影或 Artifact Reference？
- 输出是 Proposal、Finding、Measurement、Advisory、Receipt 还是环境状态？
- correlation、provenance、版本和 lineage 如何保留？
- 同步还是异步？预算、取消和背压如何处理？
- 缺失、空、冲突、超时、迟到、重复和不可用如何区分？
- 输出会不会绕过 Evidence、WorldModel、Control、Assurance 或 Effect Owner？
- Descriptor、依赖和生命周期是否能在正确 Registry 中验证？
- Adapter 是否可以被替换而不修改 Kernel 主流程？
- 是否有成功、失败、错配和 teardown 的确定性测试与证据？

如果最后两项无法证明，能力还没有达到可集成状态。

## 11. 不允许的定制方式

- 在 `UniKernel` 中添加 `List<Action<object>>`、任意字典 Hook 或可变回调集合；
- 把所有能力都建成 Agent Tool；
- 让 Inspector、Measurement 或 Artifact Sink 直接写 Evidence Ledger、WorldModel 或 Effect Boundary；
- 用时间窗口猜测摄像头样本属于哪个操作；
- 用单个 confidence 分数替代 `PerceptionAssessment`；
- 用 `FastAvailable`、opaque ID 或空列表代替实际的有界输入语义；
- 让 Slow Text 独立注册或让 Slow Visual 隐式成为 Text fallback；
- 把 Product、Runtime Integration 和 Harness 的注册状态、权限或依赖混在一个 Registry；
- 在没有买方和 Promotion 协议时新增开放式分类。

## 12. 当前实现顺序

建议按下面顺序继续开发：

1. 保持 Capability Hub 和感知协议冻结；
2. 为 Runtime Integration 定义统一 lifecycle Envelope 和只读投影；
3. 先接入语言校验 Inspector，验证 `ObservationAccepted → Finding`；
4. 再接入内部 Operation Measurement，验证 dispatch/receipt/verification 的关联；
5. 再接入摄像头 Fixture、Artifact、外部 Measurement；
6. 最后根据真实买方决定是否需要 Finding/Measurement Promotion 到 Product Contract 或 Assurance。

本文不授权以上能力直接进入 Product Authority。它只规定新增 Capability 的定制、集成、开发和验证方式。
