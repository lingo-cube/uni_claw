## Why

讨论拍板（B）：trace 携带帧/证据引用，用 **tag 属性通道**（不动 TraceRun schema v1）。目标：从 FAILED span 一键定位"是哪一帧、截图/AssetRef 在哪"。emission 只在有帧身份的边界打标（ObserveAsync/ExecuteAsync）；其余 span 的帧归属由诊断层处理（时间窗推断为 deferred）。

## What Changes

- Emission（Adapters 生产路径，fail-open）：
  - `ObserveAsync` span 打 `observation.seq`（=SequenceNumber）与 `observation.frame`（=`capture:{seq}`，与 ObservationSourceMetadata.FrameReference 一致）；
  - `ExecuteAsync` span 打 `action.kind`（action 类型名）。
  - recorder 已透传任意 tag → `TraceSpan.Attributes`，**零 schema/模型变更**。
- 工具链消费：
  - `execution-tree` 节点带 anchors（observation.seq/frame、action.kind）并做 **span→AssetRef join**（by observation.seq == AssetRef.observationSeq，ArtifactId 排序）；无体 ton=Empty；
  - `tree_view` 透传 observationSeq/frameAssetRefs/actionKind。
- 引用为候选关联，非 world truth（身份纪律延续）。
- 测试：C# emission 断言（Observe/Execute span attributes）；Python execution-tree anchors 透传 + AssetRef join（含与无锚 span）。
- deferred：无锚 span 的时间窗 INFERRED 推断。

## Capabilities

### New Capabilities

- `observability-evidence-anchors`: 观察/执行边界在 span 属性上携带帧/动作引用（tag 通道），工具链据此 span↔AssetRef 直连。

### Modified Capabilities

无。

## Impact

- `src/UniClaw.Runtime.Adapters/PhysicalEnvironment.cs`（2 处 SetTag，fail-open）。
- `tools/runtime_debug/query.py`（execution-tree 锚点提取+join）、`tui/view_models.py`（透传）。
- 测试：C# +1、AgentWorkflow +2。
- 无 TraceRun schema/wire/Runtime authority 变更。
