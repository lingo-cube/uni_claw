# ADR-0038：Capability Definition、Implementation 与 Binding 的三层边界

状态：accepted（2026-10-04）。Capability Hub 需要支持外部 Provider、Host 集成和任务级注入；如果把能力语义、具体实现和运行时装配放进同一个注册项，Hub 会退化为第二个全局状态中心。

本决策由 [ARCH-DOC-018](../../changes/ARCH-DOC-018/state.md) 固化；仓库落地基线见 [repository-role-boundaries-v0.1.md](../architecture/repository-role-boundaries-v0.1.md)。

## Decision

Capability 固定分为三层：

```text
CapabilityDefinition       产品承诺的接口和语义
CapabilityImplementation   模型、Provider、算法或组合 realization
CapabilityBinding           Host/Task 如何注入、激活和暴露
```

Registry 管理 Definition、Implementation 和可复用的 BindingDescriptor；Task 或 Host 在自己的作用域内解析出 CapabilityInstance。解析实例不得修改全局 Registry，也不得取得 Product canonical state。

Provider Manifest 只声明外部实现的 identity、supported capability、协议版本、transport、启动入口、健康检查和测试入口。Capability Descriptor 负责 Product capability 的协议、作用域、生命周期和稳定性；运行时 Health/Lifecycle Fact 由实际 owner 或 Host 在其提交点产生，不能由静态 manifest 冒充。

DSH Tool 是 Capability 的一种 Binding/Exposure，可以拥有独立的 visibility、permission 和 approval 元数据，但不构成新的 Capability 类型。

只有真正跨进程、跨语言的 wire contract 进入 `schemas/capability/`；单一语言内部 interface、Kernel model 和 Host adapter 保留在相应实现目录。

## Consequences

- `Text Semantic Perception` 可以保持一个 Product Definition，而 Fast YOLO/OCR、Slow Text 和外部 Provider 成为不同 Implementation 或组合 realization。
- 任务级注入不会被误实现为全局 singleton；同一能力可以在不同 Host 或 Task 中使用不同 Binding。
- `confidence` 属于感知结果的 `PerceptionAssessment`；`stability` 属于 Capability Descriptor，二者不混用。
- Provider、协议、注册、测试和证据可以通过稳定 ID 建立可追踪关系，但具体 manifest 校验工具另立 Change。

## Rejected alternatives

- 把 Provider、模型、Capability、Tool 和 Task Instance 都作为同一级 Registry entry。
- 让 Provider Manifest 成为 Product Capability 或运行时健康事实的权威。
- 将所有 C# interface 机械迁移到 `schemas/`。
