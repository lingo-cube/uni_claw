# SIM-013 当前源码索引

|事实|源码锚点|局限|
|---|---|---|
|组件隔离/整装均保留真实SUT；整装Owner真件|docs/architecture/simulation-baseline-v0.1.md:40，C1/C2；C7有效delta两文件|组件隔离替身保真是证明前提，不是任意Owner已实现通用mock。|
|外部缝可替换|tests/UniClaw.Simulation.Tests/SimContract.cs:505；SimulationHost.cs:206|策略/driver/Agent/handler等，非六Owner任意替换。|
|六Owner构造与具体依赖|tests/UniClaw.Simulation.Tests/SimulationHost.cs:211；src/UniClaw.Kernel/UniKernel.cs:92|UniKernel消费多个具体组件，统一工厂无任意Owner替换旋钮。|
|同程序集组合面并非编译隔离|tests/UniClaw.Simulation.Tests/SimulationHost.cs:14|源码纪律与只读Facts有价值，不等于独立组件测试程序集。|
|Builder及固定post计数插入|tests/UniClaw.Simulation.Tests/ScenarioBuilder.cs:12、65、95、117、139|构建期插序列，非运行时任意断点。Expect是authoring能力，不是独立真值来源。|
|trace→已有bundle引用|tests/UniClaw.Simulation.Tests/ScenarioImporter.cs:54|不能泛化为任意Host trace/XML import。|
|显式carrier工厂与kind=none限制|tests/UniClaw.Simulation.Tests/ScenarioLibrary.cs:29、60|新carrier需显式注册；专有载体不进入golden-bundle projection。|
|默认EffectDriver始终成功|tests/UniClaw.Simulation.Tests/DeterministicEffectDriver.cs:26|失败可注入别的IEffectDriver，不能宣称默认driver已覆盖失败。|
|输入队列/关联/重复拒绝|tests/UniClaw.Simulation.Tests/ScenarioStimulus.cs:67、113、126|不是网络/多设备模拟器或完整故障目录。|
|Product决策类型直接复用|tests/UniClaw.Simulation.Tests/ProtocolAlignmentTests.cs:24；ScriptedUniAgent.cs|避免仿真decision镜像，但不能证明真实模型决策质量。|
|能力协议/接口注册执法|src/UniClaw.Kernel/Capability/CapabilityHub.cs:191、266|类别有显式规则，Generic并非所有协议的通用一致性证明。|
|感知替换契约当前验证健康|tests/UniClaw.Kernel.Tests/Capability/PerceptionProtocolReplaceabilityTests.cs:34|未调用ObserveSemanticsAsync；不是执行载荷等价证明。|
|模拟检查器并非L1替换|tests/UniClaw.Simulation.Tests/SimulatedCapabilityIntegrationTests.cs:97|ICapability+Generic+内部Inspect；不能证明ILanguageInspector替换。|
|Host实例化含设备/服务动作|src/UniClaw.Host/SettingsTraversalLiveFeed.cs:106、121、133|不可直接当无设备离线测试工厂。|
|进程内typed外部输入缝|src/UniClaw.Kernel/Runtime/RunDriverInputs.cs:88|具体组合缝，不冻结万能协议。|
|状态机单源/生成投影|tools/gen-protocol-machine.py:1、44；schemas/protocol/state-machine.yaml|payload引用record/schema，未统一生成所有协议类型。|
|协议表C#执法|tests/UniClaw.Kernel.Tests/Protocol/ProtocolMachineTests.cs:8、49、86、132|表内/类型存在/枚举检查不证明所有真实runtime事件都走合法转移。|
|DSH schema与严格握手|src/UniClaw.Agent.Dsh/ProtocolSchema.cs:11；Handshake.cs:47|版本、schema版本/hash、profile/manifest不符拒绝，非协商兼容。|
|C8与认证三摘要|tools/scenario_certify.py:1、40、90；tests/UniClaw.Simulation.Tests/ScenarioCertification.cs:6|Runtime source范围为Kernel+Agent，未覆盖全部Host/fixture/adapter。|
|当前完整回归与构成快照|tools/verify-change:34、257、472；evidence/sim-012/verification.json|已有八工程full，未来组件影响映射和更细fingerprint覆盖仍需实现。|

## 检索与证据等级

本轮只读源码/基线检查，不以设计意图或历史Change CLOSED代替当前实现。大模型场景生成的检索范围为src、tests、tools、dsh内的ScenarioBuilder、MinimalScenarioBundle、场景生成及scenario generation相关引用；未找到统一生成器。该结论限于本仓库与该范围。

旧记录仅用于工作方法约束；所有现状判断都核对当前源码。源码/生成物只读核验见checks.json；不新增产品测试、不运行设备/模型。
