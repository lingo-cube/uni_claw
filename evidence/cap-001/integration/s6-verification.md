# CAP-001 Integration Contract Verification (S6)

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| DETERMINISTIC | `dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --filter 'FullyQualifiedName~IntegrationContractsTests|FullyQualifiedName~KernelRuntimeSurfaceWhitelistTests' --no-restore` | Envelope、correlation、Finding、Measurement、Artifact、Fixture 契约及 Kernel 公共面通过 | 6 passed, 0 failed | `tests/UniClaw.Kernel.Tests/Capability/IntegrationContractsTests.cs`; `tests/UniClaw.Kernel.Tests/Runtime/KernelRuntimeSurfaceWhitelistTests.cs` |
| DETERMINISTIC | 同上，缺关联与独立时钟字段断言 | 缺少显式 association 的 Measurement 被拒绝；Runtime、Host monotonic、外部设备和传感器时间分开保留 | covered by `Measurement_requires_explicit_correlation`, `Envelope_distinguishes_point_and_terminal_success` | same |
| DETERMINISTIC | 同上，迟到/重复/终点状态断言 | Late、Duplicate、TimedOut、Unavailable 等状态保留为 typed status/disposition | covered by `Late_and_duplicate_are_explicit_dispositions`, `Terminal_failure_states_and_fixture_states_are_typed` | same |

## Contract boundary

`CapabilityEnvelope` 只携带身份、顺序、来源、版本、状态、correlation 和分离的 Runtime elapsed、Host monotonic、外部设备/传感器时间；没有 canonical wall-clock 或 opaque payload。`MeasurementSample` 要求显式 operation/receipt/capture/observation correlation，不能用时间窗口猜测归属。Runtime Integration 与 Harness 仅复用这些不可变词汇，不共享 Registry、权限或 Owner 写入路径。

限制：本切片只冻结数据形状和构造时不变量，不负责语言校验、性能算法、采集、Fixture 资源管理、去重存储或生命周期调度；Late/Duplicate 的最终处理由其所属 Host/Adapter 决定。NuGet 漏洞缓存权限警告不影响本次编译和测试结果。
