## 1. Phase 1: IAdbSession + ShellResult + ProcessAdbSession

- [x] 1.1 Add `IAdbSession` interface (3 methods + `IAsyncDisposable`) to `src/UniClaw.Device/IAdbSession.cs`
- [x] 1.2 Add `ShellResult` record (`Success`, `StandardOutput`, `StandardError`) to `src/UniClaw.Device/ShellResult.cs`
- [x] 1.3 Update `AdbCommandException`: change constructor from `(string, AdbCommandResult)` to `(string, ShellResult)`, update `Result` property type
- [x] 1.4 Add `ProcessAdbSession` wrapping `AdbCommandRunner` to `src/UniClaw.Device/ProcessAdbSession.cs`
- [x] 1.5 Migrate `AdbScreenCapture`: `IAdbCommandRunner` → `IAdbSession`, `RunAsync(exec-out screencap -p)` → `CaptureScreenshotAsync()`
- [x] 1.6 Migrate `AdbActionExecutor`: `IAdbCommandRunner` → `IAdbSession`, `RunShellAsync` → `ExecuteShellAsync`, `wm size` → `ExecuteShellAsync`
- [x] 1.7 Migrate `AdbScreenStateProvider`: `IAdbCommandRunner` → `IAdbSession`, dump + cat → single `DumpUiHierarchyAsync()`, remove `RemotePath` constant
- [x] 1.8 Migrate `AdbEntryActionDriver`: all `RunAsync` calls → `ExecuteShellAsync`
- [x] 1.9 Migrate `ScenarioObservation`: field type `IAdbCommandRunner` → `IAdbSession`
- [x] 1.10 Migrate `HostCommands`: field (L104) + parameters (L815, L1165) from `IAdbCommandRunner` to `IAdbSession`; update `CreateRunner` (L838) to create `ProcessAdbSession`
- [x] 1.11 Update convenience constructors in `AdbScreenCapture`, `AdbScreenStateProvider`, `AdbActionExecutor`: each `(string serial, ...)` ctor internally delegates to `ProcessAdbSession` instead of `new AdbCommandRunner(...)`
- [x] 1.12 Migrate test fake `FakeAdbRunner` in `AdbDeviceBoundaryTests.cs` (L345): `IAdbCommandRunner` → `IAdbSession`, implement 3 methods
- [x] 1.13 Migrate test fake `FakeAdbRunner` in `RunnerTestHarness.cs` (L144): `IAdbCommandRunner` → `IAdbSession`, implement 3 methods
- [x] 1.14 Migrate test fake `FakeRunner` in `HostCommandTests.cs` (L211): `IAdbCommandRunner` → `IAdbSession`, implement 3 methods
- [x] 1.15 Migrate `AdbTestContext`: field type `IAdbCommandRunner` → `IAdbSession`
- [x] 1.16 Update test assertions: replace `ExitCode`/`Arguments`/`Duration`/`BinaryOutput`/`Failure` field assertions with `Success`/`StandardOutput`
- [x] 1.17 Delete `IAdbCommandRunner` interface; `AdbCommandRequest`/`AdbCommandResult`/`AdbCommandFailure`/`AdbCommandRunnerOptions` retained for `AdbCommandRunner` internal use (deferred to Phase 3)
- [x] 1.19 Verify all existing unit tests pass (`dotnet test` 0 fail): Core 1083 pass + Host 142 pass = 1225/0
- [x] 1.18 Add `ProcessAdbSession` equivalence unit test: construction guards, ShellResult mapping, Serial forwarding (6 tests)

## 2. Phase 2: AdvancedSharpAdbSession

- [x] 2.1 Add `AdvancedSharpAdbClient` NuGet package reference (v3.6.16) to `UniClaw.Device.csproj`
- [x] 2.2 API probe: confirmed via XML docs — `AdbClient()`, `AdbServer.StartServerAsync()`, `ExecuteRemoteCommandAsync` with `IShellOutputReceiver`, `ConsoleOutputReceiver`
- [x] 2.3 Implement `AdvancedSharpAdbSession` with `SemaphoreSlim(1,1)` serialization, 3-tier self-healing, `RawByteReceiver` for binary capture
- [x] 2.4 Serial validation: empty/null serial → `ArgumentException` on construction (A6)
- [x] 2.5 `ObjectDisposedException` guard: `ThrowIfDisposed()` before each method (A7)
- [x] 2.6 `UNICLAW_ADB_BACKEND` env var switch in `CreateRunner` (default: `sharp` → `AdvancedSharpAdbSession`)
- [x] 2.9 Verify ArchitectureGuard tests still pass: 56/56 ✅
- [x] 2.7 Write emulator-gated integration tests: screenshot capture, shell command, UI dump, self-healing reconnect, ProcessAdbSession vs AdvancedSharpAdbSession comparison (I1-I4)
- [x] 2.8 Add integration tests to emulator-gated skip list in `docs/validation/unit_test_status.md`

## 3. Phase 3: Cleanup

- [x] 3.1 Mark `AdbCommandRunner` with `[Obsolete]` — only 1 external reference (our own ProcessAdbSessionTests null cast)
- [x] 3.2 Verify `ProcessAdbSession` works as opt-in fallback: `UNICLAW_ADB_BACKEND=process` → ProcessAdbSession in CreateRunner; all tests pass
