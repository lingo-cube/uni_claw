# CAP-001 Perception S5 Verification

## Slow Visual vertical slice

| method | expected | actual | evidence |
|---|---|---|---|
| `SlowConsultation` with `RequiresRawArtifact=true`, raw bytes present, `FastBasis=null` | Visual request reaches the existing orchestrator and P2 projector | Visual profile is selected from raw artifact; no `SlowTextGate` call; successful and empty results remain projector outcomes | `tests/UniClaw.Kernel.Tests/Perception/SlowConsultationTests.cs`, `SlowReplayRealizationTests.cs` |
| Host visual request construction with no Fast basis | Same-cycle raw artifact is the only visual prerequisite | `BuildSlowRequest(... visual:true)` carries raw artifact and leaves `FastBasis` null; host no longer gates Visual on Fast availability | `tests/UniClaw.Host.Tests/SettingsPopupClassifierTests.cs`, `src/UniClaw.Host/SettingsTraversalLiveFeed.cs` |
| OpenCode Visual execution without resolver / with unresolved artifact | Fail closed with diagnostic and zero proposals | Resolver missing, resolution failure, and empty bytes return invalid/infrastructure failure before provider call | `src/UniClaw.Kernel/Perception/OpenCodeSlowRealization.cs` |
| Slow failure and late projection | Timeout/provider unavailable/late cannot authorize effects | Timeout and unavailable statuses produce zero projected proposals; late results preserve `IsLate` while still using `SlowResultProjector` | `src/UniClaw.Kernel/Perception/SlowConsultation.cs`, `SlowOrchestration.cs` |
| Kernel perception tests | Deterministic Visual and regression suite passes | 224 tests passed | `dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --no-restore --filter FullyQualifiedName~Perception` |
| Full Kernel/Host regression | No regression outside the perception slice | Kernel 747 passed; Host 142 passed | `dotnet test ... --no-restore` |

`git diff --check` passed. `WI-CAP001-005` is now marked `done` after the Leader review.

The Visual path has no dependency on `FastTextBasis` or `SlowTextGate`; Fast YOLO/OCR remains a Text Semantic gate only. No WorkItem status change is made here.
