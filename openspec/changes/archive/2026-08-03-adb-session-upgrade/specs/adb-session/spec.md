## ADDED Requirements

### Requirement: IAdbSession defines 3 methods and extends IAsyncDisposable

`IAdbSession` SHALL define exactly 3 methods: `CaptureScreenshotAsync`, `ExecuteShellAsync`, and `DumpUiHierarchyAsync`. The interface SHALL extend `IAsyncDisposable` to express long-lived connection lifecycle. The interface SHALL be in `UniClaw.Device` namespace.

#### Scenario: Interface method count is 3
- **WHEN** the `IAdbSession` interface is inspected
- **THEN** exactly 3 methods are declared: `CaptureScreenshotAsync`, `ExecuteShellAsync`, and `DumpUiHierarchyAsync`
- **THEN** no generic `RunAsync` method exists

#### Scenario: Interface is disposable
- **WHEN** an `IAdbSession` instance is used
- **THEN** it can be `await using`'d or passed to `await using var`

### Requirement: CaptureScreenshotAsync returns PNG bytes

`IAdbSession.CaptureScreenshotAsync(CancellationToken)` SHALL capture the current device screen as a PNG image and return the raw bytes.

#### Scenario: Successful screenshot capture
- **WHEN** `CaptureScreenshotAsync` is called on an active ADB session
- **THEN** a non-empty `byte[]` containing valid PNG data is returned

#### Scenario: Screenshot capture fails
- **WHEN** `CaptureScreenshotAsync` fails to capture (device disconnected or empty output)
- **THEN** an `AdbCommandException` is thrown with a message indicating the failure reason

### Requirement: ExecuteShellAsync returns structured result

`IAdbSession.ExecuteShellAsync(string command, CancellationToken)` SHALL execute an ADB shell command and return a `ShellResult` containing `Success`, `StandardOutput`, and `StandardError`.

#### Scenario: Successful shell command
- **WHEN** a valid shell command (e.g., `input tap 100 200`) is executed
- **THEN** `ShellResult.Success` is `true` and `StandardOutput` may contain command output

#### Scenario: Failed shell command
- **WHEN** a shell command fails (e.g., invalid path)
- **THEN** `ShellResult.Success` is `false` and `StandardError` contains the error message

#### Scenario: ShellResult does not expose ADB internals
- **WHEN** a `ShellResult` is inspected
- **THEN** it has exactly 3 properties: `Success`, `StandardOutput`, `StandardError`
- **THEN** no `ExitCode`, `Arguments`, `Duration`, `BinaryOutput`, or `Failure` fields exist

### Requirement: DumpUiHierarchyAsync returns XML string

`IAdbSession.DumpUiHierarchyAsync(CancellationToken)` SHALL capture the current UI hierarchy as an XML string. The method SHALL internally combine `uiautomator dump` and `cat` operations; callers SHALL NOT provide or know about remote file paths.

#### Scenario: UI hierarchy dump succeeds
- **WHEN** `DumpUiHierarchyAsync` is called on an active ADB session
- **THEN** a non-empty string containing valid UI hierarchy XML is returned

#### Scenario: Caller is isolated from internal file paths
- **WHEN** `DumpUiHierarchyAsync` is called
- **THEN** the caller provides no remote path argument
- **THEN** the method internally handles the dump file location

### Requirement: AdvancedSharpAdbSession maintains a TCP long connection

`AdvancedSharpAdbSession` SHALL connect to ADB server via TCP (127.0.0.1:5037) and maintain a long-lived connection. Commands SHALL be serialized through a `SemaphoreSlim(1,1)` to prevent frame interleaving on the single underlying socket.

#### Scenario: Connection is established on construction
- **WHEN** `AdvancedSharpAdbSession` is constructed with a valid device serial
- **THEN** ADB server is started if not running, and a TCP connection is established to the device

#### Scenario: Commands are serialized
- **WHEN** two concurrent calls are made to any session method
- **THEN** the second call waits for the first to complete before executing
- **THEN** no frame interleaving or socket corruption occurs

### Requirement: AdvancedSharpAdbSession implements 3-tier self-healing

`AdvancedSharpAdbSession` SHALL implement 3-tier self-healing on each command execution: (1) immediate reconnect with 0ms delay, (2) backoff with `AdbServer.StartServer` restart after 500ms, (3) final attempt after 1000ms. After 3 total failures, an `AdbCommandException` SHALL be thrown.

#### Scenario: Connection recovers on first retry
- **WHEN** a command fails due to a transient connection loss
- **THEN** the session reconnects immediately and retries the command successfully

#### Scenario: Connection recovers after server restart
- **WHEN** the ADB server has been killed (adb kill-server)
- **THEN** the session restarts the ADB server on the second retry and reconnects
- **THEN** subsequent commands succeed

#### Scenario: All retries exhausted
- **WHEN** all 3 retry attempts fail
- **THEN** an `AdbCommandException` is thrown with message indicating "connection lost after N retries"
- **THEN** no further automatic retries occur

### Requirement: ProcessAdbSession wraps AdbCommandRunner for fallback

`ProcessAdbSession` SHALL implement `IAdbSession` by delegating to an internal `AdbCommandRunner` instance. `DisposeAsync` SHALL be a no-op (no persistent state). The behavior of its 3 methods SHALL be equivalent to the existing `AdbCommandRunner.RunAsync` usage patterns.

#### Scenario: ProcessAdbSession delegates to AdbCommandRunner
- **WHEN** `CaptureScreenshotAsync` is called on `ProcessAdbSession`
- **THEN** it internally calls `_runner.RunAsync` with `exec-out screencap -p` and captures binary output

#### Scenario: ProcessAdbSession produces same results as direct AdbCommandRunner
- **WHEN** the same ADB operation is performed via `ProcessAdbSession` and via direct `AdbCommandRunner.RunAsync`
- **THEN** the output content is identical

### Requirement: UNICLAW_ADB_BACKEND switches between implementations

The system SHALL select the `IAdbSession` implementation based on the `UNICLAW_ADB_BACKEND` environment variable: `sharp` selects `AdvancedSharpAdbSession`, `process` selects `ProcessAdbSession`. The default SHALL be `sharp`.

#### Scenario: Default backend is AdvancedSharpAdbSession
- **WHEN** `UNICLAW_ADB_BACKEND` is not set
- **THEN** `AdvancedSharpAdbSession` is used

#### Scenario: Process backend is used for CI
- **WHEN** `UNICLAW_ADB_BACKEND` is set to `process`
- **THEN** `ProcessAdbSession` is used, requiring no NuGet package

### Requirement: Consumer convenience constructors preserve backward compatibility

Existing convenience constructors (e.g., `AdbActionExecutor(string serial, string adbPath = "adb", TimeSpan? timeout = null)`) SHALL remain with unchanged signatures. Internally they SHALL delegate to `ProcessAdbSession` instead of directly constructing `AdbCommandRunner`.

#### Scenario: String-based constructor still works
- **WHEN** `new AdbActionExecutor("emulator-5554")` is called
- **THEN** a valid executor backed by `ProcessAdbSession` is created
- **THEN** behavior is identical to before the migration

### Requirement: AdbCommandException carries ShellResult

`AdbCommandException` SHALL be preserved with its constructor changed from `(string operation, AdbCommandResult result)` to `(string operation, ShellResult result)`. The `Result` property type SHALL change accordingly. Existing `catch (AdbCommandException)` statements SHALL compile without modification.

#### Scenario: Exception carries ShellResult on command failure
- **WHEN** an ADB command fails and `AdbCommandException` is thrown
- **THEN** the exception's `Result` property is a `ShellResult` with `Success == false` and `StandardError` containing the failure details

### Requirement: Serial validation on construction

Both `AdvancedSharpAdbSession` and `ProcessAdbSession` SHALL validate the device serial on construction. An empty or null serial SHALL cause an `ArgumentException` at construction time (fail-fast).

#### Scenario: Empty serial throws on construction
- **WHEN** `AdvancedSharpAdbSession` or `ProcessAdbSession` is constructed with an empty or null serial
- **THEN** an `ArgumentException` is thrown immediately

### Requirement: ObjectDisposedException after disposal

After `DisposeAsync()` completes, calling any method on `AdvancedSharpAdbSession` SHALL throw `ObjectDisposedException`.

#### Scenario: Post-disposal call throws
- **WHEN** `CaptureScreenshotAsync`, `ExecuteShellAsync`, or `DumpUiHierarchyAsync` is called after `DisposeAsync()` has completed
- **THEN** an `ObjectDisposedException` is thrown
