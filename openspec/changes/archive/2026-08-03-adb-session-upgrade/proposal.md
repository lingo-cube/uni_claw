## Why

当前 `AdbCommandRunner` 每次 ADB 交互启动新 `Process`（`Process.Start("adb.exe")`），进程启动开销（~50-200ms Windows）在车机自动化每 step 2-5 次 ADB 调用的预算里不可忽略，且 stringly-typed 的 `AdbCommandRequest`/`AdbCommandResult` 抽象把 ADB 内部细节泄露给所有消费者。引入 `AdvancedSharpAdbClient` NuGet 包通过 TCP 长连接（ADB server 5037 端口）替代 Process-per-command 模式，消除进程 spawn 延迟，同时用类型安全的 `IAdbSession` 接口封装 ADB 交互语义。

## What Changes

- **新增** `IAdbSession` 接口（3 方法 + `IAsyncDisposable`）：`CaptureScreenshotAsync()`、`ExecuteShellAsync()` → `ShellResult`、`DumpUiHierarchyAsync()`
- **新增** `ShellResult` record（`Success`、`StandardOutput`、`StandardError`），替换 stringly-typed `AdbCommandResult`
- **新增** `AdvancedSharpAdbSession`：基于 `AdvancedSharpAdbClient` 的 TCP 长连接实现，含 `SemaphoreSlim` 命令串行化 + 三级自愈重连
- **新增** `ProcessAdbSession`：包装现有 `AdbCommandRunner` 的降级实现，CI 环境 fallback
- **新增** NuGet 依赖 `AdvancedSharpAdbClient`（`UniClaw.Device` 项目首个外部包引用）
- **修改** 4 个生产消费者（`AdbScreenCapture`、`AdbActionExecutor`、`AdbScreenStateProvider`、`AdbEntryActionDriver`）+ `ScenarioObservation` + `HostCommands`：`IAdbCommandRunner` → `IAdbSession`
- **修改** 3 个测试 fake（`FakeAdbRunner` ×2 + `FakeRunner`）+ `AdbTestContext`：实现 `IAdbSession` 替代 `IAdbCommandRunner`
- **修改** `AdbCommandException`：构造器参数从 `AdbCommandResult` 改为 `ShellResult`；`Result` 属性类型同步变更
- **BREAKING**: 删除 `AdbCommandRequest`、`AdbCommandResult`、`AdbCommandFailure`、`AdbCommandRunnerOptions` 四个类型；`IAdbCommandRunner` 接口删除
- **保留** `AdbCommandRunner`（Phase 1 为 `ProcessAdbSession` 内部使用，Phase 3 标记删除）
- **新增** `UNICLAW_ADB_BACKEND` 环境变量切换（`sharp` / `process`，默认 `sharp`）

## Capabilities

### New Capabilities
- `adb-session`: TCP 长连接 ADB 会话接口，替代 Process-per-command 模式。定义 `IAdbSession`（3 方法 + `IAsyncDisposable`）、`ShellResult`、`AdvancedSharpAdbSession`（TCP 长连接 + 自愈）、`ProcessAdbSession`（降级实现），以及消费者迁移。

### Modified Capabilities
<!-- None: 本改动在 Device 层内部，Core 层不感知 IAdbSession，不改变任何已有 spec 级需求 -->

## Impact

- **Affected code**: `src/UniClaw.Device/`（新增 4 文件、修改 5 文件、删除 4 类型）、`src/UniClaw.Host/Commands/HostCommands.cs`（装配点 + 字段/参数类型）、`tests/UniClaw.Host.Tests/`（HostCommands 测试 fake）、`tests/UniClaw.Device.Tests/`（Device 层测试 fake）
- **NuGet dependency**: `AdvancedSharpAdbClient`（`UniClaw.Device.csproj` 首个外部包引用）
- **API compatibility**: `IAdbCommandRunner` 接口删除，所有消费者必须迁移到 `IAdbSession`；便捷构造器（`AdbActionExecutor(string serial, ...)`）保留向后兼容，内部从 `new AdbCommandRunner(...)` 变为 `new ProcessAdbSession(...)`
- **No Core layer impact**: Core 层不依赖 `IAdbCommandRunner`，不感知此次变更
