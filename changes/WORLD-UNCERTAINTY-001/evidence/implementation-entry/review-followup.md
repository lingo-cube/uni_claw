# Review follow-up

1. **导航访问借用后续普通 capture：resolved。** `DerivePageView` 现在把 visit 关联限制到 relation 的 `TargetRevisionId`、目标 capture 证据与该 capture identity basis（`src/UniClaw.Kernel/World/WorldModel.History.cs:59-67`）；Host replay 新增“返回后普通重采不借上次访问”负例（`tests/UniClaw.Host.Tests/WorldOperationReplayTests.cs:59-68,215-228`）。仍只证明受控 replay，不是 live traversal 认证。

2. **未知设备身份跨观察归属：resolved。** 新门要求来源 capture 的 DeviceId 非空且目标历史页 device/session 相符（`src/UniClaw.Kernel/World/WorldModel.History.cs:11-24`）；测试覆盖设备不同及双方缺失（`tests/UniClaw.Kernel.Tests/World/WorldSemanticReuseTests.cs:103-119`）。

3. **Host profile 正式装配：接线 resolved，live 来源仍未验收。** `HostRunner` 与 `SettingsCoverageRunner` 的可选 profile 同时传入 World；Settings feed 对实际 typed hierarchy 调用 projector，并复用 capture 的 Space（`src/UniClaw.Host/HostRunner.cs:60-72,138-171`；`src/UniClaw.Host/SettingsCoverage/SettingsCoverageRunner.cs:69-74,170-176,207-213`；`src/UniClaw.Host/SettingsTraversalLiveFeed.cs:321-331`）。新增组合测试用 `SettingsCoverageRunner`、Ledger/World/Driver 和受控输入验证配置时咨询可读当前语义、无配置时仍 Unknown（`tests/UniClaw.Host.Tests/WorldSemanticHostCompositionTests.cs:16-75`）；其 `FeedNext` 会绕过真实 `SettingsTraversalLiveFeed`，故没有证明真实 XML/device、已配置的生产 profile 或模型消费者。生产 profile 必须匹配 parser 的 `LegacyUiAutomatorXml` / `adb-uiautomator/legacy`（`src/UniClaw.Host/UiAutomatorDump.cs:285-295,352-364`）；目前没有 live profile 证据，不能标 live PASS。测试未运行（本轮禁构建）。

4. **Recovery 状态归属：resolved。** 可变 `WorldRecoveryControl` 现在由 `ControlLoop` 私有持有，Kernel 暴露窄操作转接，Driver 不再持有/读取该对象（`src/UniClaw.Kernel/Control/ControlLoop.cs:15-25`；`src/UniClaw.Kernel/UniKernel.WorldConsumption.cs:10-18`；`src/UniClaw.Kernel/Runtime/KernelRunDriver.cs:468-484,2871-2875,3113`）。

**影响审查边界仍诚实且受限。** Assurance 以 receipt 精确关联原消费 basis、dispatch 后 verification basis；缺旧页面血缘生成 `DependenciesUnknown`，不把无记录当零影响（`src/UniClaw.Kernel/Assurance/RuntimeAssurance.WorldImpact.cs:41-69`；`src/UniClaw.Kernel/World/WorldConsumptionBasis.cs:12-38`）。Kernel 在实际 dispatch 后记录 use、消费更正时追加 reassessment（`src/UniClaw.Kernel/UniKernel.cs:372-375,716-729`）；测试覆盖 pending verification 不重发、terminal 不重开、legacy 缺 lineage Unknown（`tests/UniClaw.Kernel.Tests/Runtime/WorldCorrectionImpactTests.cs:224-312,314-447`）。这些记录只证明显式提供的 page/target/decision 来源，不声称完整模型思考依赖、所有外部副作用归因或 live provider；测试未运行，最终验收仍待 Leader 串行执行。

## 后续有界修复

Contains端点追加同device/session实际历史依据检查，foreignParent真实反例先RED后GREEN(3项)；无新Container/Owner。旧capture可选字段null编码导致冻结语义basis漂移，新增字段WhenWritingNull保留旧编码；冻结输入/响应/独立Oracle不改。OperationReplay补显式受控device来源，Profile旧revision断言随27同步。产品语义严格核对未放松，原失败日志保留，最后Verify仍另行执行。
