# WORLD-UNCERTAINTY-001 Spec Review

固定点 `f541a8971a8880e5c70a0841a03c45c6eeb1b06a`；仅审本线相关实现。以下是可达规格问题，不是整体通过声明。

1. **P1，访问来源会被旧导航误授予。** K02要求“普通新capture”不算访问，且 producer 无重入证明时访问未知（`review/scenario-derivation.md:110`）。`WorldModel.History` 按 `TargetContainerId == owner` 取导航，且只要求其目标revision早于当前capture；没有核对导航目标revision的 `CurrentPageObservation.EvidenceId` 与当前 `group.Key`，或该revision的 `IdentityEvidenceId` 与导航目标证据（`src/UniClaw.Kernel/World/WorldModel.History.cs:58-64`）。因此 A→B→A 后，A 的后续普通capture可借用旧 B→A 边得到 `VisitSourceRef`。应要求导航目标revision对应此capture及其identity证据，并加旧导航后普通capture回归。

2. **P1，缺 DeviceId 时同签名跨设备仍可并页。** `PageCaptureObservation.DeviceId` 可缺省（`PageObservation.cs:17,68-73`）；page identity 的匹配门只比较 `page.Capture.DeviceId == source.DeviceId` 和 session（`WorldModel.History.cs:11-23`），故两个未知设备（null）且同 session、同签名会通过。K02明确要求“相同route不同父/设备再做负例”（`scenario-derivation.md:110`）。对新page identity要求双方有非空且相等的DeviceId，否则保留Unknown；当前已有测试仅覆盖明确不同的device值（`WorldSemanticReuseTests.cs:103-115`）。

3. **P2，生产Host没有语义profile接线。** K05要求对应充分后World沿用稳定语义并免重复慢调用（`spec.md:33`）。Host两个World装配点均未传 `nativeContextProfiles`（`HostRunner.cs:163-166`、`SettingsCoverageRunner.cs:205-208`），而无profile时语义刷新直接返回（`WorldModel.SemanticKnowledge.cs:109-112`）。因此目前该路径只能保持Unknown并调用原供应方；这是缺少获批真实profile时的正确fail-closed行为，但K05生产能力尚未接通，受控测试不能代替它。

本次未运行构建/测试。K08计数与新结果复核、K09/K10影响和终局行为、K11当前材料资格结果均未据本次静态审查宣称通过；本文件不替代相应场景验证。
