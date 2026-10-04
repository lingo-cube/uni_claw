# CAP-001 External Artifact/Measurement Verification (S9)

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| DETERMINISTIC | `dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --filter 'FullyQualifiedName~ExternalArtifactMeasurementContractTests' --no-restore` | Artifact/Measurement 显式绑定、错配拒绝、状态与 Fixture 生命周期测试通过 | 16 passed, 0 failed | `tests/UniClaw.Kernel.Tests/Capability/ExternalArtifactMeasurementContractTests.cs` |

`ArtifactMeasurementBinding` 只按完整 `CapabilityCorrelation` 绑定 Run/Capture/Receipt/Operation；缺 Artifact、缺 Measurement 或任一关联错配都会拒绝。ExternalDeviceTime/SensorTime 保留在 MeasurementSample，与 RuntimeElapsed/HostMonotonicTime 分开。FixtureLifecycleFact 仍是独立 Host/Harness 事实，不表达业务成功。没有时间窗口推断，也没有采集、存储、驱动、视觉算法或 Owner 写入路径。
