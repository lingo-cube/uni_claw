# CAP-001 Measurement Contract Verification (S8)

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| DETERMINISTIC | `dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --filter 'FullyQualifiedName~OperationMeasurementContractTests' --no-restore` | start/terminal 配对、阶段、终点状态、关联、序号和独立时钟测试通过 | 13 passed, 0 failed | `tests/UniClaw.Kernel.Tests/Capability/OperationMeasurementContractTests.cs` |

契约区分 Dispatch、Receipt、PostActionVerification 三阶段；开始和终点必须是 typed OperationStarted/OperationTerminal，并共享同一 correlation。Late/Duplicate 是关联处置，不能伪装成终点状态。契约不生成 wall-clock、不实现计时器或阈值策略。
