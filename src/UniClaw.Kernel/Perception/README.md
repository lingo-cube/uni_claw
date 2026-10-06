# Perception 代码索引（语义感知 + UI 元素感知）

这个目录是 Kernel 感知维度的代码入口。**能力的身份是两个协议接口，不是
「快慢」**；快慢是 union 组件内部的异步实现策略（§3，已降级但保留）。
维护代码前先读 §1-§3 分清概念，再按 §4-§5 找文件。

## 1. 能力身份：两个协议接口

| 接口 | 协议（名称@版本） | 回答的问题 |
|---|---|---|
| [`ISemanticPerception`](../Capability/CapabilityHub.cs) | Semantic Perception@1.0 | 这个界面/元素表达了什么语义？ |
| [`IUiElementPerception`](../Capability/CapabilityHub.cs) | UI Element Perception@1.0 | 当前 capture 里有哪些元素、属性和几何？ |

- 两接口定义在 Kernel Capability 面（L0 `ICapability` → L1 协议接口，
  协议常量 `PerceptionProtocol` 同点），本目录不重复定义；
- 协议-接口一致性由 `CapabilityRegistry.ValidateImplementation` 注册执法
  （声明双协议的实例必须实现双接口，反之亦然）；
- 可替换性（R5）钉在这两个 L1 面上：消费闭包只写
  `ISemanticPerception + IUiElementPerception`
  （`tests/UniClaw.Kernel.Tests/Capability/PerceptionProtocolReplaceabilityTests.cs`）；
- 协议**负载词汇**（SemanticObservationProposal / PerceptionAssessment）
  尚未冻结——留给下一个真实感知买方驱动（CAP-009 out-of-scope），本目录
  与 Capability 面均不预造。

## 2. UniPerception：双协议的 union 组件

[`UniPerceptionCapability`](../Capability/UniPerceptionCapability.cs)
同时实现两个协议接口，是感知能力的 L2 组合实现（capability id
`uni.perception`，CompositeProductPerception，依赖 fast.yolo / fast.ocr /
slow.text）。身份 = 双协议；现实语义责任 = **健康聚合 owner**
（`ICapabilityHealthCheckable`，买方为真机运行时诊断）；实例注册经组合根
（Host `PerceptionCapabilityComposition`），生命周期纯内存常驻
（RL1：Registered 即稳态）。

## 3. 快慢（Fast/Slow）：隐性概念，降级但保留

快慢**不是能力协议**，是 UniPerception union 内部的**异步实现策略**。
保留这个概念的原因：感知行为默认异步（所有者裁决 2026-10-06）——
**Fast+XML 先行入世界模型，Slow 晚到补语义**。词汇面见 CONTEXT.md
「Perception / Fast-Slow」。

| 面 | 语义 | 文件 |
|---|---|---|
| Fast（同步先行） | 同帧 fast 观察 + typed 传递 + 帧复用 + 几何执法 | `FastPerception`（strategy 组合根）、`TextFastBasis`（同一 capture 内 YOLO/OCR typed 传递）、`StrategyObservationCache`（帧计算复用）、`CoordinateSpace`（坐标归一执法） |
| Slow（异步补语义） | 有界咨询 + 升级路由 + 可替换 realization | `SlowContracts`（封闭词汇：request/result/status）、`SlowConsultation`（有界等待 + 诚实状态）、`SlowOrchestration`（Control-owned keyed attempt 预留）、`SlowEscalationRoute`（Fast→Slow 升级路由）、`OpenCodeSlowRealization`（OpenCode provider adapter）、`SlowReplayRealization`（deterministic 缺省替换件） |

替换 Slow realization（OpenCode ⇄ replay ⇄ 未来的真件）只动 adapter +
组合根，消费方零改动（R5，同 §1 测试执法）。

## 4. 采集/传输面（provider 契约侧）

| 文件 | 负责 |
|---|---|
| `AdbScreenshotAcquisition` | 设备截屏 → PNG RawArtifact（capture） |
| `PngImage` | PNG→RGBA 零依赖解码（analyze_raw 输入） |
| `VisionServiceTransport` | uds \| loopback-tcp 端点（封闭层次） |
| `VisionServiceClient` | /v1/analyze_raw 调用 + 全失败分类 |
| `VisionServiceHost` | provider 进程生命周期（拉起/探活/fail-loud） |
| `LiveVisionStrategy` | 响应 JSON → ArtifactObservation（parity 锚） |

契约（端点/schema/失败分类/变体/身份）冻结在
[perception-provider-baseline](../../docs/architecture/perception-provider-baseline-v0.1.md) §3。

## 5. 子目录归属

| 子目录 | 负责 | 不负责 |
|---|---|---|
| `Fusion/` | YOLO+OCR 证据融合：lineage 校验（`LineageValidator`）、occurrence 关联（`OccurrenceAssociator`）、融合契约（`FusionContracts`）、融合引擎（`FusionEngine`）、有界升级策略（`BoundedEscalationPolicy`） | 不做产品语义决策，不写 Evidence/WorldModel |
| `UiHierarchy/` | UI 层级（XML/hierarchy）采集→typed proposal 投影：capture 元数据与描述符（`CaptureMetadata`/`HierarchyCaptureDescriptor`）、观察记录与取值（`UiHierarchyCapture`/`UiHierarchyObservation`/`ObservedValue`）、语义核验（`CheckedSemantics`/`SemanticCheckedResolver`）、覆盖与能力声明（`HierarchyCoverage`/`HierarchyCapabilities`）、typed 投影（`TypedHierarchyProposalProjector`） | occurrence 只在 capture 内有效；不直接成为跨 revision identity 或 Effect binding |

## 6. 方向（我们要做的）

1. **协议负载词汇冻结**（SemanticObservationProposal / PerceptionAssessment）
   ——由下一个真实感知买方驱动，独立 change；
2. **slow.visual 实例化**——visual 接线后从 description-only 转实例注册；
3. **fast.yolo / fast.ocr 独立能力协议词汇**——fast 能力独立 change 冻结；
4. provider 侧扩展点（detect/recognize 并行、替代推理后端、OCR 模型对照）
   见冻结基线 §6（OPT-001/OPT-002）。

## 7. 指向

- 冻结基线（契约权威）：`docs/architecture/perception-provider-baseline-v0.1.md`
- 能力管理面与协议纪律：`src/UniClaw.Kernel/Capability/README.md`、
  `docs/capability-hub/customization-integration-development-protocol-v0.1.md`
- ADR：0020（双 artifact 确定性锚）、0021（provider 黑盒变体选择）
- Provider 侧（Python）：`platforms/perception/README.md`
