# Capability Hub 代码索引

这个目录是 Kernel 中 Capability Plane 的代码入口。它不是所有能力实现的
收容目录，也不是 DSH Tool 或 Skill 的注册目录。维护代码时先判断改动属于哪一面，
再进入对应文件或目录。

## 当前代码归属

| 代码面 | 当前位置 | 负责什么 | 不负责什么 |
|---|---|---|---|
| Hub 管理面 | [`CapabilityHub.cs`](CapabilityHub.cs) | `ICapability`、`ICapabilityHub`、`CapabilityDescription`、信任域、注册/解析、生命周期事实、健康状态和装配校验（CAP-008 起含 ModelRouting 类别的协议/实例执法） | 不执行语言/视觉/性能算法，不拥有 Finding、Measurement 或 Product authority；不把任务专属实例当成全局单例 |
| Host-neutral 协议面 | [`IntegrationContracts.cs`](IntegrationContracts.cs) | correlation、Envelope、Finding、Measurement、Artifact、Fixture lifecycle、语言 Inspector request 等跨 Host 结果协议 | 不注册实例、不调度能力、不决定 DSH Tool 形状 |
| Product 感知协议 | `CapabilityHub.cs` 中的 `ISemanticPerception`、`IUiElementPerception` 及其协议常量 | 表达 Product 买方接口和协议一致性 | 不表达 Fast/Slow provider，不携带 DSH 或模型 provider 语义 |
| 能力画像与影响披露 | `CapabilityProfile.cs`（CAP-012 D7）+ `UniPerceptionCapability.cs`（CAP-009 感知实例） | 影响披露词汇（「没配白名单会怎样」）与感知可执行实现 | 不拥有 admission/judgment 语义 |
| Model Management 能力组件 | [`ModelManagement.cs`](ModelManagement.cs)（CAP-006/007/008） | `IModelManagement : ICapability`（logical profile → 冻结 binding 解析、候选偏好、健康证据）、`ModelManagementProtocol`、可选健康能力面 `ICapabilityHealthCheckable` + `CapabilityHealthReport` | 不下载/安装/热切换模型；不把外部 binding registry 带入 Product Runtime；provider 词汇不进此层 |
| UniPerception L2 | [`UniPerceptionCapability.cs`](UniPerceptionCapability.cs)（CAP-009） | `uni.perception` 组合能力的可执行实现：双协议 marker（Semantic + UI Element）+ 健康聚合 owner（`PerceptionHealthSource` 命名源注入，worst-of 聚合） | 不拥有协议负载词汇（SemanticObservationProposal / PerceptionAssessment 待后续感知 change 冻结）；不执行感知算法 |
| Language Inspection | [`LanguageInspection.cs`](LanguageInspection.cs)（CAP-012） | `ILanguageInspector : ICapability`（有界文本投影 → 语言格式 Finding）+ `LanguageFormatInspector`（确定性 Unicode 脚本规则，en/zh；不支持诚实 Unknown）+ `LanguageInspectionProtocol` 常量 | 不接 Assurance/Effect（Finding 非权威）；不支持语言不猜（Unknown）；不做异步调度 |
| Product realization | `src/UniClaw.Kernel/Perception/` | Fast/Slow 感知的具体实现和既有 P2/P3 接缝 | 不把实现细节倒灌到 Hub 管理面 |
| Host / 外部 Adapter | `src/UniClaw.Host/`、`src/UniClaw.Host.Dsh/` | 设备、Host transport、外部观测和必要的 DSH 原生投影 | 不修改 Kernel 的 Capability 管理规则 |
| Kernel contract tests | [`tests/UniClaw.Kernel.Tests/Capability/`](../../../tests/UniClaw.Kernel.Tests/Capability/) | Hub 注册/解析、跨域隔离、协议一致性（含 ModelRouting 执法、UniPerception 双协议一致性、健康聚合契约）和 Integration contract 边界 | 不放真实模型、设备或 DSH fixture |
| Simulation fixtures | [`tests/UniClaw.Simulation.Tests/`](../../../tests/UniClaw.Simulation.Tests/) | 用确定性夹具验证能力装配、关联和结果状态 | 不把仿真类加入 Kernel public surface，不代表真实算法质量 |

## 变更规则

### 修改 Hub 管理面

只有以下类型的改动进入 `CapabilityHub.cs`：

- 新的注册域、生命周期或健康状态管理语义；
- Descriptor、依赖、关系和协议一致性校验；
- `ICapability` / `ICapabilityHub` 管理接口；
- 实例注册、解析和 fail-closed 装配规则。

如果改动需要知道文本内容、图像内容、操作耗时或业务结果，它就不属于 Hub
管理面，应进入协议面或具体能力实现。

## 注册与任务注入

Capability 有两个必须分开的维度：

1. **注册域**：能力属于 Product、Runtime Integration 还是 Harness。这由
   `CapabilityScope` / `TrustDomain` 表达，决定谁可以发现和装配能力。
2. **注入生命周期**：能力实例是 Composition Root 常驻，还是在下发某个任务时
   才创建并绑定。这不是 `CapabilityScope` 的含义，不能把两个维度压成一个字段。

因此要区分两条路径：

```text
CapabilityDescription
  → Hub catalog / descriptor registration

Task dispatch
  → task capability request
  → task-scoped instance binding
  → task capability context
  → capability execution
```

常驻能力可以由 Composition Root 注册实例并由 Hub 解析。任务注入能力可以只把
Descriptor 放进 Hub catalog；任务下发时由对应的 Composition Root 或 Host 创建
实例，绑定 `TaskId` / `RunId` / correlation 和取消范围，再交给任务上下文使用。
任务结束后必须释放或关闭该绑定，不能把它写回全局 Registry 作为下一个任务的
隐式共享实例。

当前 `CapabilityRegistry.Resolve()` 仍是全局实例解析，尚未提供 task-scoped
binding 接口。因此任务注入属于后续独立 Change；在该接口冻结前，不要把任务专属
能力注册成全局 singleton，也不要在 `CapabilityDescription.Scope` 中偷塞生命周期
语义。

### 修改协议面

新增或修改 `IntegrationContracts.cs` 前必须写清：买方、输入投影、输出类型、
correlation、版本、失败/迟到/重复语义和生命周期。结果协议不能直接写入
Evidence、WorldModel、Control、Assurance 或 Effect owner。

### 新增具体 Capability

具体能力先在对应信任域注册，再通过已有协议接入。实现可以是 Product realization、
Runtime Integration inspector/measurement，或 Harness fixture；不能因为实现名字
叫 Fast、Slow、YOLO、OCR、XML 或视觉模型就新增一个外部买方协议。

### 外部暴露

Kernel Capability 只保留 Host-neutral 管理和协议语义。是否投影为 DSH Tool、使用
什么 schema、scope、guard 和 transport，由 Host/DSH Adapter 单独决定；不得把
`ToolDefinition` 或 `ctx.tools` 写入本目录。

## 当前文件与后续拆分

当前四个 C# 文件保持原位置，以降低工作树迁移风险：

```text
Capability/
├── CapabilityHub.cs            # Hub 管理面 + Product protocol marker + ModelRouting/LanguageInspection 执法
├── IntegrationContracts.cs     # Host-neutral integration contracts
├── ModelManagement.cs          # Model Management 能力组件 + 健康能力面 mixin（CAP-006/007/008）
├── UniPerceptionCapability.cs  # uni.perception L2：双协议 marker + 健康聚合 owner（CAP-009）
├── LanguageInspection.cs       # Language Inspection：L1 + L2 + 协议常量（CAP-012）
└── README.md                   # ownership/index（本文件）
```

如果后续需要拆成 `Hub/`、`Contracts/`、`Protocols/` 子目录，必须单独建立 Change，
先完成引用扫描、项目编译、public surface whitelist 和全量测试，再执行可回滚迁移。
