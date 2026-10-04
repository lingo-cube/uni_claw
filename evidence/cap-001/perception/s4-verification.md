# CAP-001 S4 verification

## Changes

- `FastTextBasis` carries typed YOLO detections, OCR tokens, capture/session/cycle and freshness/provider state.
- `SlowTextGate` rejects missing, stale, misaligned, or unavailable Fast prerequisites with distinct diagnostics; Visual Slow remains independent.
- Host `SettingsTraversalLiveFeed` retains Fast output and passes the same-cycle basis through `SlowConsultationRequest`; existing P2 projection remains the only Slow ingress.

## Verification

- method: `dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --no-restore --filter 'FullyQualifiedName~TextFastBasisTests|FullyQualifiedName~SlowConsultationTests|FullyQualifiedName~OpenCodeSlowRealizationTests|FullyQualifiedName~KernelRuntimeSurfaceWhitelistTests'`; expected: basis gate, provider serialization, P2 ingress and public-surface tests pass; actual: 21 passed, 0 failed; evidence: command output 2026-10-04.
- method: `dotnet test tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj --no-restore --filter 'FullyQualifiedName~SettingsPopupClassifierTests'`; expected: Host request seam carries the typed basis and keeps Visual independent; actual: 14 passed, 0 failed; evidence: command output 2026-10-04.
- method: `dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --no-restore`; expected: no Kernel regression; actual: 747 passed, 0 failed; evidence: command output 2026-10-04.
- method: `dotnet test tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj --no-restore`; expected: no Host regression; actual: 142 passed, 0 failed; evidence: command output 2026-10-04.
- method: `git diff --check`; expected: no whitespace errors; actual: pass; evidence: command output 2026-10-04.

## Limits

The live provider JSON is normalized to bounded string labels/tokens. No provider-specific model payload crosses the Slow request boundary.
