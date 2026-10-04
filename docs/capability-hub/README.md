# Capability Hub 管理面

这里是 Capability Hub 的独立管理入口。它描述如何定制、注册、集成和开发 Capability；不承载具体 Product 业务规则，也不取代 ADR、Change State 或 Evidence 的唯一真相源。

## 管理边界

| 内容 | 位置 | 责任 |
|---|---|---|
| Hub 管理骨架与 Registry | [`src/UniClaw.Kernel/Capability/`](../../src/UniClaw.Kernel/Capability/) | `ICapability`、`ICapabilityHub`、Descriptor、协议、依赖、作用域、生命周期、健康状态和信任域隔离 |
| Capability Hub 代码索引 | [`src/UniClaw.Kernel/Capability/README.md`](../../src/UniClaw.Kernel/Capability/README.md) | 管理面、Host-neutral 协议面、实现面、Adapter、测试面的代码归属和变更规则 |
| Hub 单元测试 | [`tests/UniClaw.Kernel.Tests/Capability/`](../../tests/UniClaw.Kernel.Tests/Capability/) | 注册、装配和 fail-closed 契约 |
| 定制、集成、协议和开发指南 | [`customization-integration-development-protocol-v0.1.md`](customization-integration-development-protocol-v0.1.md) | 新 Capability 的买方判断、协议选择、Adapter 开发和验收流程 |
| 运行时接缝设计 | [`../design/runtime-capability-integration-seams-v0.1.md`](../design/runtime-capability-integration-seams-v0.1.md) | lifecycle fact、Envelope、correlation 和能力类别语义 |
| 架构决策 | [`../adr/0035-capability-management-hub-trust-scoped-registries.md`](../adr/0035-capability-management-hub-trust-scoped-registries.md) | Hub 的架构地位、注册域和权威边界 |
| 当前 Change State | [`../../changes/CAP-001/state.md`](../../changes/CAP-001/state.md) | CAP-001 的路线、WorkItem 状态和验证记录 |
| 验证证据 | [`../../evidence/cap-001/`](../../evidence/cap-001/) | Registry、Text Semantic 和 Slow Visual 的运行证据 |

## 三个注册域

Capability Hub 不是一个跨域共享注册表。三个信任域分别由自己的 Composition Root 创建独立 Registry：

- Product Capability Registry：Product perception、acquisition、grounding 和 effect provider；
- Runtime Integration Registry：Observer、Inspector、Measurement、Artifact/Report Sink；
- Harness Capability Registry：Fixture、Environment Manager、Stimulus、Fault Injector。

三个注册域可以共享 Host-neutral lifecycle fact 和 correlation 词汇，但不能共享可变注册状态、权限或装配依赖。跨域组合必须经过显式 Adapter 或 Promotion 协议。

注册能力描述和注入能力实例是两件事。常驻能力可以由 Composition Root 注册实例；
任务专属能力只在任务下发时创建 task-scoped binding，不能因为已经有 Descriptor
就被当成全局 singleton。当前 task-scoped binding 接口尚未冻结，见 Capability
源码索引中的[注册与任务注入](../../src/UniClaw.Kernel/Capability/README.md#注册与任务注入)。

## 新 Capability 的管理入口

新增能力时依次检查：

1. 在指南中确定买方、注册域、主分类和输出类型；
2. 选择已经冻结的协议，写清输入、输出、关联、失败、背压和生命周期；
3. 在 `src/UniClaw.Kernel/Capability/` 增加或复用 Descriptor/Registry 管理语义；
4. 在对应的 Product、Runtime Integration 或 Harness 目录实现 Adapter；
5. 通过确定性测试证明跨域隔离、生命周期、错配、超时、迟到和 Teardown 语义；
6. 在 `changes/` 和 `evidence/` 留下可追溯的 WorkItem 与验证记录。

## 可执行实例与协议接口

`ICapability` 是 Kernel 内部可执行能力的根接口，只暴露不可变的
`CapabilityDescription`。Capability Hub 通过 `ICapabilityHub` 接收实例注册、
协议一致性校验和实例解析；健康、生命周期和 lifecycle fact 仍由 Hub/Registry
拥有。

Product perception 的协议接口继承 `ICapability`：

- `ISemanticPerception` → `Semantic Perception@1.0`
- `IUiElementPerception` → `UI Element Perception@1.0`

Product perception Descriptor 声明的协议必须与实例实现的派生接口一致；不一致时
注册失败且不发布 lifecycle fact。具体 payload 方法、请求/结果 DTO 和模型链路
在后续的感知纵向 Change 中冻结。

DSH Tool/Skill 不属于 Kernel Capability 接口。本 Change 只提供共享 Capability
实例；如果 DSH 需要调用某项能力，由 Host/DSH Adapter 使用 DSH 原生
`ctx.tools.register(...)` 做显式投影。

具体协议和开发规则见[Capability 定制化、集成与开发协议指南](customization-integration-development-protocol-v0.1.md)。
