# World observation slice evidence

- RED target: `PageObservationRecoveryTests` specifies strict capture codec behavior: correlation and items round-trip; unknown members and duplicate items reject.
- GREEN command: `dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --no-restore --filter FullyQualifiedName~PageObservationRecoveryTests`.
- Scope: World-only files; no Host, Runtime, UniKernel, public whitelist, protocol, or documentation changes.
