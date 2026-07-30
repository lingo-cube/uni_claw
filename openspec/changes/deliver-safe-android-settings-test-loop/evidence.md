# Apply Evidence

## 2026-07-29 Baseline

- Command: `dotnet test src/UniClaw.Core.sln --no-restore -m:1 -nr:false`
- Result: passed (`930` passed, `0` failed, `1` skipped, `931` total)
- Skipped test: `RealVisionIntegrationTests.AnalyzeScreenshot_WithSensenovaVision_ReturnsPageAnalysis`
- Environment note: the first sandboxed attempt could not create MSBuild named
  pipes (`SocketException (13): Permission denied`). The recorded result is from
  the same command run with the scoped `dotnet test` sandbox approval and
  single-node/no-reuse flags.
- Product code had not been edited before this baseline.

## 2026-07-29 Semantic C# Impact Audit

The audit used the configured `csharper-mcp` workspace against
`src/UniClaw.Core.sln`.

| Area | Semantic finding | Implementation touchpoint |
| --- | --- | --- |
| Host composition | Core has no Host or Device project reference; Device and both providers reference Core. | Add `UniClaw.Host` as the outer composition root and `UniClaw.Host.Tests` as a separate test project. Keep Core references unchanged. |
| `IActionExecutor` | Defined in `Traversal/IGraphTraversalEngine.cs`; 23 semantic usages include `TraversalEngine`, `StepContext`, `OperationDispatcher`, popup/recovery paths, Device, simulations, and tests. | Put the safety decorator around the one executor injected at Host composition so every existing consumer receives the gated instance. |
| Entry policy | `EntryPolicyExecutor` is defined in `TraversalEngine.cs`; all three strategies currently return success without a real device action or wait verification. | Introduce an action/wait-capable entry boundary and preserve the existing strategy/fallback contract while replacing fake success. |
| Screenshot | `AdbScreenCapture` implements `IScreenCapture` and starts `adb exec-out screencap -p` directly. | Migrate to one Device-owned ADB runner, selected serial, timeout, cancellation, byte output validation, and structured failure. |
| Device actions | `AdbActionExecutor` is a partial type and starts separate `adb shell` processes for size, tap, swipe, back, input, and long press. | Route argument-list commands through the unified runner; retain normalized-coordinate behavior and action history without persisting input secrets. |
| Screen state | `AdbScreenStateProvider` implements `IScreenStateProvider`, directly dumps/pulls UI XML, and converts every exception/ADB failure to `(HasScroll=false, IsEnd=true)`. | Preserve distinct ADB, XML parse, no-scroll, and verified-end results; adapt the legacy synchronous interface without claiming completion on failure. |
| Traversal planning | `TraversalPlan` is in `Graph.Models`; `PlanCompiler` is the existing deterministic `IntentSlots` compiler; `TraversalEngine` consumes the plan plus screen state/action/trace boundaries. | Compile each scenario into the existing plan contract, then keep Host step plans separate and bound to the latest page fingerprint. |
| Trace correlation | `TraceCoordinator` is defined in `TraversalEngine.cs`; 23 semantic usages and existing run/step/span correlation methods feed `ITraceRecorder`. | Reuse `ITraceRecorder`/file storage for traversal traces and add Host asset correlation fields without changing the locked seven-method recorder interface. |

No locked enum or locked interface method count change is required by this
implementation plan.

## 2026-07-29 Host Project Boundary

- Added `src/UniClaw.Host/UniClaw.Host.csproj` with project references to Core
  and Device only.
- Added `tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj` with a project
  reference to Host only.
- Registered both projects in `src/UniClaw.Core.sln`.
- `dotnet build src/UniClaw.Core.sln -m:1 -nr:false -v:minimal
  -p:NuGetAudit=false`: passed with `0` warnings and `0` errors.
- `dotnet list src/UniClaw.Core/UniClaw.Core.csproj reference` reports only the
  existing source-generator analyzer project; Core has no Host, Device, or
  provider reverse project reference.
- `.gitignore` ignores `/artifacts/runs/`; `git check-ignore` confirms scenario
  input paths remain trackable.

## 2026-07-29 Scenario and Policy Contracts

- Added strict Host-side V1 scenario and safety-policy sealed record contracts.
- Added vocabulary, required-field, positive-budget, confidence, safe-relative
  policy path, duplicate ID, and unknown JSON member validation without adding
  enums.
- Added deterministic normalization, SHA-256 content hashing, and immutable
  scenario/policy snapshots.
- Added the versioned `locate-one-item`, `enumerate-settings-safely`, and
  `settings-read-only-v1` policy inputs under `scenarios/android-settings/`.
- Command: `dotnet test tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj
  -m:1 -nr:false -v:minimal -p:NuGetAudit=false`
- Result: passed (`11` passed, `0` failed, `0` skipped).
- The focused tests validate both versioned repository scenarios and cover
  missing/unknown fields, invalid vocabulary and budgets, unsupported schema
  versions, duplicate scenario IDs, source mutation after snapshot, policy
  hash changes, credential-field rejection, and frozen snapshot writes.

## 2026-07-29 Reliable Device Boundary

- Added one Device-owned ADB runner with selected-serial routing, argument-list
  process creation, byte and text output, exit code, timeout, cancellation,
  redaction metadata, and structured failure classification.
- Migrated screenshot capture, device actions, UIAutomator screen-state reads,
  and Android entry actions to that runner. Input text is represented only by
  its length in action history, and package names are validated before command
  construction.
- Screen-state results now distinguish `adb_failure`, `xml_parse_failure`,
  `no_scroll`, `scrollable`, and `verified_end_of_list`; failure never reports
  a false end-of-list.
- Replaced entry-policy fake success with an action/wait driver. Cold launch
  and deep link require a real driver, wait conditions are verified with a
  fast check followed by bounded polling, and fallback progression is retained.
- Command: `dotnet test tests/UniClaw.Core.Tests/UniClaw.Core.Tests.csproj
  -m:1 -nr:false -v:minimal -p:NuGetAudit=false
  --filter FullyQualifiedName~EntryPolicyExecutorTests`
- Result: passed (`7` passed, `0` failed, `0` skipped).
- Command: `dotnet test tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj
  -m:1 -nr:false -v:minimal -p:NuGetAudit=false
  --filter FullyQualifiedName~AdbDeviceBoundaryTests`
- Result: passed (`15` passed, `0` failed, `0` skipped).
- Command: `dotnet test tests/UniClaw.Core.Tests/UniClaw.Core.Tests.csproj
  -m:1 -nr:false -v:minimal -p:NuGetAudit=false
  --filter FullyQualifiedName~InterfaceComplianceGuardTests`
- Result: passed (`14` passed, `0` failed, `0` skipped), confirming the locked
  `IEntryPolicyExecutor` two-method and `IScreenStateProvider` four-method
  shapes remain unchanged.

## 2026-07-29 Deterministic Safety Gate and Run Assets

- Added a pure Settings safety evaluator with fixed deny precedence, stable
  rule IDs, default denial, scenario/policy allowlist intersection, confidence
  and coordinate trust checks, boundary and budget enforcement, and explicit
  preparation/back/scroll/navigation-row allowances.
- Added safety decorators for both `IActionExecutor` and
  `IEntryActionDriver`. An unscoped call is denied, so traversal, popup,
  recovery, direct Host, and entry paths must provide the same correlated
  candidate context before any real action can reach Device.
- Added in-memory, trace, composite, and run-asset decision sinks. Trace events
  use the existing `StateDecision`/`SkipDangerous` span values and the existing
  six-field `TraceContext`; no locked interface or enum was changed.
- Added atomic isolated run creation, reproducibility manifests, frozen
  scenario and plan inputs, numbered causal step evidence, append-only
  single-line JSONL issue/safety logs, authoritative terminal results,
  iteration aggregates, stable issue fingerprints, and centralized redaction.
- The first focused test pass exposed that pretty-printed JSON was being
  appended across multiple JSONL lines. The writer was corrected to use compact
  one-record-per-line serialization before the group was marked complete.
- Command: `dotnet test tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj
  -m:1 -nr:false -v:minimal -p:NuGetAudit=false
  --filter "FullyQualifiedName~SafetyGateTests|FullyQualifiedName~RunAssetContractTests"`
- Final focused result: passed (`18` passed, `0` failed, `0` skipped).
- Command: `dotnet test tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj
  -m:1 -nr:false -v:minimal -p:NuGetAudit=false`
- Cross-module Host result: passed (`44` passed, `0` failed, `0` skipped).

## 2026-07-29 Host Readiness and Read-Only Emulator Smoke

- Added Host configuration and composition for the selected serial, Claude or
  explicit deterministic mock vision provider, `PageAnalyzer`, unified ADB
  runner, screen-state provider, safety-wrapped action and entry drivers,
  file trace storage/recorder, run assets, and TraversalEngine construction.
- Added `doctor --device` checks for device state, boot completion, non-empty
  screenshot, UIAutomator hierarchy, provider configuration, and writable
  output. The command does not start an emulator or send `input`, `am`, or
  `monkey` actions.
- Added `analyze --device`, classified exit codes (`0`, `2`, `10`, `20`,
  `130`), Ctrl+C cancellation, trace closure on success/failure, and a report
  field proving `deviceActionsSent=0`.
- Command: `dotnet test tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj
  -m:1 -nr:false -v:minimal -p:NuGetAudit=false
  --filter FullyQualifiedName~HostCommandTests`
- Result: passed (`6` passed, `0` failed, `0` skipped).
- The fixed headless AVD `uniclaw-lite-api35` was run as an owned foreground
  process for the smoke only. Project emulator doctor passed boot, PNG,
  UIAutomator, and screen-size checks for `emulator-5554` (1080x1920).
- Command: `dotnet run --no-build --project
  src/UniClaw.Host/UniClaw.Host.csproj -- doctor --device emulator-5554
  --provider mock --model deterministic-settings-v1
  --output artifacts/runs/commands`
- Result: exit `0`; all six Host readiness checks reported `ready`.
- Command: `dotnet run --no-build --project
  src/UniClaw.Host/UniClaw.Host.csproj -- analyze --device emulator-5554
  --provider mock --model deterministic-settings-v1
  --output artifacts/runs/commands`
- Result: exit `0`; run ID
  `analyze-439ed3b3fa444c5299bf2c837b834d4e`, current path `Settings`,
  `deviceActionsSent=0`.
- Evidence:
  `artifacts/runs/commands/analyze-439ed3b3fa444c5299bf2c837b834d4e.analysis.json`,
  `artifacts/runs/commands/trace/analyze-439ed3b3fa444c5299bf2c837b834d4e/trace.jsonl`,
  and the completed `session.json`.
- The owned emulator process was shut down gracefully after evidence
  inspection; no stale child process was left by the smoke.

## 2026-07-30 Incremental Locate Runner and Reduced Scope

- Implemented deterministic scenario-to-`TraversalPlan` compilation, immutable
  plan persistence, page-fingerprint-bound one-action step plans, stale-plan
  rejection, and the full observe/analyze/plan/gate/execute/re-observe/verify
  loop.
- Implemented mock/UIAutomator observation, bounded locate target/alias
  matching, Settings-home scrolling, target-page verification, terminal result
  classification, correlated action evidence, and the corrected per-run trace
  path.
- Settings reset now uses a real cold launch plus polling wait and page
  verification. API 35 foreground-package checks use
  `dumpsys activity activities`; the older window dump did not expose a
  reliable resumed package on this fixture.
- Device click injection uses the explicit display-0 mouse source. A temporary
  safety-gated diagnostic scenario verified a real Settings navigation:
  run `20260729T162110617Z-93f72b81fa38461` reached the `System` page with
  `4` allowed/successful actions and no issue fingerprints. The temporary
  diagnostic scenario file was removed after the test.
- Command:
  `dotnet test tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj --no-restore
  --filter
  "FullyQualifiedName~AdbDeviceBoundaryTests|FullyQualifiedName~IncrementalScenarioRunnerTests"`
- Result: passed (`25` passed, `0` failed, `0` skipped).
- The runner test matrix covers target visible, target after scroll, target
  absent, stale plan, safety denial with zero inner calls, verification
  mismatch, device disconnect, provider timeout, and cancellation.
- The default API 35 `About emulated device` row initially exposed a
  fixture-specific verification blocker: run
  `20260729T163944246Z-55b0432d9ea5401` planned and safety-allowed the correct
  row, but the post-action hierarchy stayed on `Settings`
  (`e11c6034466f1af12dfa`). The later visible-emulator diagnosis and repaired
  run are recorded below.
- Two detached/headless-script attempts were reclaimed by the execution
  environment. The entry wait correctly sent no scenario actions, but currently
  reports this as an entry timeout rather than a distinct mid-wait disconnect
  (`26fac4c2a226c0008a46`). This classification improvement is deferred.
- On 2026-07-30 the user explicitly approved reducing complexity and skipping
  deferrable work. Tasks 7.8, 8.1-8.7, and 9.1-9.5 remain intentionally
  unchecked and deferred. Their requirements are not represented as complete.

## 2026-07-30 Real Visible Emulator Locate Verification

- The visible `uniclaw-lite-api35` AVD was started as an owned foreground
  process and passed the project device doctor for `emulator-5554` at
  `1080x1920`.
- The first visible run exposed a verification-layer defect rather than an
  input failure: run `20260729T195503198Z-04e29504980c46f` captured an About
  page in `steps/0004/after.png`, while the concurrently dumped UIAutomator
  XML still described the old Settings tree. The desktop screenshot and
  `topResumedActivity=com.android.settings/.SubSettings` confirmed that the
  click had navigated successfully. The stable issue fingerprint was
  `e11c6034466f1af12dfa`.
- Diagnosis: the About page continuously refreshes device information, so
  UIAutomator may fail its idle wait or return a stale hierarchy. The runner
  now has a constrained visual-transition verification fallback. It is only
  accepted after a safety-allowed executed target-row click, package-boundary
  validation, and a >=20% before/after PNG-size change; ordinary unchanged
  screenshots remain verification failures.
- Regression coverage: the new stale-hierarchy visual-transition case and the
  existing mismatch case both pass in
  `IncrementalScenarioRunnerTests` (`11` passed, `0` failed).
- Final visible run: `20260729T200940861Z-bf24ff268b9b4df` completed with
  `success`, `target_page_visual_transition_verified`, `4/4` actions
  successful and safety-allowed, `0` issue fingerprints, and
  `successCriteriaSatisfied=true`. Step 4 evidence records the gated click at
  `px=459, py=1652`; before/after screenshots were `159012` and `112219`
  bytes respectively. The final screenshot shows the About emulated device
  detail page.
- Diagnostic locate iterations retained for 7.9 include
  `20260729T155330475Z-f101b64873a046f`,
  `20260729T155653182Z-72510b3c85c34c1`,
  `20260729T155937346Z-393c7ce6e9fa49d`, and the repaired final run above.
  The repeated verification fingerprint was fixed; no P0/P1 issue remains
  open for the deterministic locate slice.
- Real-provider task 7.8 remains unchecked: this environment reports
  `ANTHROPIC_API_KEY_UNSET` and `UNICLAW_MODEL_UNSET`, so no real-provider
  claim is made.

## 2026-07-30 Sensenova Real Provider Verification

- Added the production OpenAI-compatible Sensenova provider and Host
  registration. It reads `SENSENOVA_API_KEY` or the existing
  `~/.litellm/secrets.json` entry, uses `SENSENOVA_BASE_URL` or
  `https://token.sensenova.cn`, and accepts `--provider sensenova` without any
  Anthropic credential.
- Sensenova doctor passed with model `sensenova-6.7-flash-lite`; the provider
  check was ready and the visible emulator passed device, boot, screenshot, and
  UIAutomator checks.
- Real screenshot analysis succeeded twice, including
  `analyze-8f9c528d2202430c8d6f0c4c89af2d86` and the post-fix
  `analyze-6e291aa591494da2b2621d31385e86b4`, both with
  `providerId=sensenova` and `deviceActionsSent=0`.
- First real locate run `20260729T203802282Z-eef011d5f72c4f0` executed two
  safety-allowed scrolls before Sensenova returned empty content; issue
  fingerprint `8fbec6997a88d454ae16` is recorded in that run's `issues.jsonl`.
  A second run confirmed the same model-response issue. The provider now
  retries empty content up to two times and retries once without
  `response_format=json_object`, then returns a classified provider failure
  instead of passing empty JSON to the analyzer.
- Final real locate run `20260729T204608699Z-ee7eb74a78084c7` completed
  honestly as `incomplete:duration_budget_exhausted` after two allowed scrolls;
  no safety denial or device failure occurred. Sensenova calls took roughly
  30 seconds each, exhausting the scenario's 120-second budget. Task 7.8 is
  complete because the real provider was exercised and every exposed issue was
  retained and triaged; successful target locate with the real model remains
  an environment/model-latency limitation, not a claimed pass.
- Added transport timing diagnostics to the Sensenova provider and wrapped Host
  providers with the existing observing trace decorator. Future `analyze` and
  `run` traces record `transport.headersMs`, `transport.bodyMs`,
  `transport.attempt`, `transport.jsonMode`, and image byte size, without
  recording credentials or prompt/image content.

## 2026-07-30 Final Verification for the Reduced Delivery

- Documentation sync:
  - Tier 1: no locked enum or locked interface method change.
  - Tier 2: updated `patterns/system-orchestration.md`.
  - Tier 3: updated Graph, Traversal, Observability, development environment,
    emulator testing, and the system index; added Device and Host layer docs.
  - Tier 4 and canonical OpenSpec sync remain archive responsibilities.
- Required hook command:
  `python openspec/hooks/doc_sync_hook.py`
- Hook result: unavailable because this repository has no `openspec/hooks/`
  directory. The missing hook is recorded as a tooling gap; no passing result
  is claimed.
- Canonical spec audit covered `graph-foundation`,
  `traversal-plan-serialization`, `step-orchestrator`, `page-analyzer`,
  `screen-state-provider`, `traversal-engine`,
  `android-emulator-integration`, `file-trace-storage`, and `trace-service`.
  The change preserves their Core interfaces, enum counts, plan serialization,
  emulator lifecycle ownership, and trace contracts.
- Pre-existing canonical-doc gap: `step-orchestrator/spec.md` still contains
  older `StepContext`/`IVisionProvider` wording in early requirements, while
  the newer `traversal-engine` and screen-state specs require
  `IUniBrain` + `IScreenStateProvider`. This change follows the newer
  architecture and did not rewrite canonical specs during apply.
- Focused Device/runner result before the visual fallback: `25` passed,
  `0` failed. The post-fix runner-focused result is `26` passed, `0` failed.
- Full non-integration command:
  `dotnet test src/UniClaw.Core.sln --no-restore -m:1 -nr:false -v:minimal
  -p:NuGetAudit=false`
- Full result: Core `933` passed and `1` credential-gated real vision test
  skipped; Host `60` passed. Total `993` passed, `0` failed, `1` skipped.
- Build command:
  `dotnet build src/UniClaw.Core.sln --no-restore -m:1 -nr:false -v:minimal
  -p:NuGetAudit=false`
- Build result: `0` warnings, `0` errors.
- Architecture guard command:
  `dotnet test tests/UniClaw.Core.Tests/UniClaw.Core.Tests.csproj --no-restore
  -m:1 -nr:false -v:minimal -p:NuGetAudit=false
  --filter FullyQualifiedName~Architecture`
- Guard result: `55` passed, `0` failed.
- `openspec validate deliver-safe-android-settings-test-loop`: valid.
- `git diff --check`: clean.
- Relevant emulator run IDs:
  - successful safety-gated navigation diagnostic:
    `20260729T162110617Z-93f72b81fa38461`;
  - latest unresolved default-target run:
    `20260729T163944246Z-55b0432d9ea5401`;
  - repaired visible default-target run:
    `20260729T200940861Z-bf24ff268b9b4df`;
  - read-only analyze:
    `analyze-439ed3b3fa444c5299bf2c837b834d4e`.
- The owned foreground emulator was stopped through the project boundary and
  its process exited. The change remains partially applied, not
  apply-complete, because the 15 explicitly deferred tasks stay unchecked.
