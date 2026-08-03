# ADB Session Upgrade — IAdbSession × AdvancedSharpAdbClient

> 日期: 2026-08-02
> 状态: approved
> 范围: `src/UniClaw.Device/` + 消费者迁移

## 1. Motivation

当前 `AdbCommandRunner` 每次 ADB 交互启动一个新 `Process`（`Process.Start("adb.exe")`），问题：

- **延迟累加**。车机自动化每个 step 做 2-5 次 ADB 调用（截图 + shell + dump），每次都 spawn/kill 进程，进程启动开销（~50-200ms Windows）在 1080P 截图推理几百毫秒的预算里不可忽略。
- **无连接复用**。Process 模式无法利用 ADB server 5037 端口的 TCP 长连接，每次重新握手。
- **stringly-typed 抽象**。`AdbCommandRequest` / `AdbCommandResult` 把 ADB 内部细节（参数数组、exit code、stdout/stderr 字符串）泄露给所有消费者，每个消费者自己做正则解析。
- **无生命周期语义**。`IAdbCommandRunner` 是 stateless 接口，表达不了长连接持有的生命周期。

目标：引入 `AdvancedSharpAdbClient` NuGet 包，通过 `IAdbSession` 接口封装 TCP 长连接，替换 Process-per-command 模式。

## 2. Architecture

```
                                IAdbSession : IAsyncDisposable
                               /              \
                              /                \
               AdvancedSharpAdbSession    ProcessAdbSession
               (AdvancedSharpAdbClient)   (包装 AdbCommandRunner)
               
消费者 (全部注入 IAdbSession):
  AdbScreenCapture     → CaptureScreenshotAsync()
  AdbActionExecutor    → ExecuteShellAsync()
  AdbScreenStateProvider → DumpUiHierarchyAsync()
  AdbEntryActionDriver → ExecuteShellAsync()
  ScenarioObservation  → ExecuteShellAsync()
```

### 2.1 IAdbSession 接口（3 方法锁定）

放 `src/UniClaw.Device/IAdbSession.cs`，namespace `UniClaw.Device`。

```csharp
public interface IAdbSession : IAsyncDisposable
{
    string Serial { get; }

    /// <summary>捕获当前屏幕截图，返回 PNG 字节流。</summary>
    Task<byte[]> CaptureScreenshotAsync(CancellationToken ct = default);

    /// <summary>执行 shell 命令，返回结构化结果。</summary>
    Task<ShellResult> ExecuteShellAsync(
        string command,
        CancellationToken ct = default);

    /// <summary>
    /// 拉取当前 UI 层级 XML。
    /// 内部合并 uiautomator dump + cat 为一次调用，调用方不关心文件路径。
    /// </summary>
    Task<string> DumpUiHierarchyAsync(CancellationToken ct = default);
}

public sealed record class ShellResult(
    bool Success,
    string StandardOutput,
    string StandardError);
```

**锁定理由**：
- `IScreenStateProvider` 已有 4 方法锁定的先例（ArchitectureGuardTests 约束）。
- 3 个方法覆盖全部现有 ADB 交互，不需要泛化 `RunAsync`。
- `ShellResult` 替换 `AdbCommandResult`，去掉了消费者不关心的 `ExitCode`、`Arguments`、`Duration`、`BinaryOutput`、`Failure` 字段。
- `IAsyncDisposable` 表达长连接生命周期——`IAdbCommandRunner` 表达不了的语义。

### 2.2 AdvancedSharpAdbSession 实现

放 `src/UniClaw.Device/AdvancedSharpAdbSession.cs`。

```
┌─────────────────────────────────────────────────────────────┐
│                  AdvancedSharpAdbSession                     │
├─────────────────────────────────────────────────────────────┤
│  _semaphore   SemaphoreSlim(1,1)  ← 命令串行化锁             │
│  _client      AdbClient           ← TCP 长连接               │
│  _endpoint    DnsEndPoint         ← 127.0.0.1:5037           │
├─────────────────────────────────────────────────────────────┤
│  CaptureScreenshotAsync()  → screencap -p  → byte[]          │
│  ExecuteShellAsync(cmd)    → shell <cmd>   → ShellResult     │
│  DumpUiHierarchyAsync()    → dump + cat    → string (XML)    │
│                                                              │
│  DisposeAsync()            → _client 释放 + _semaphore 释放  │
└─────────────────────────────────────────────────────────────┘
```

**命令串行化**：`SemaphoreSlim(1,1)` 保证同一时刻只有一个命令在 Socket 上传输。AdvancedSharpAdbClient 底层单 Socket，并发命令会导致帧交错。串行化开销可忽略（单条 ADB 命令 ~2ms）。

**API 确认（Phase 2 第一步，探针先行）**：包的实际 API 以安装后实测为准，本文档代码块不视为已确认签名。已知事实：包文档与社区示例的常用用法为参数构造器 + `Connect("127.0.0.1:5037")`；`AdbServer.StartServer` 静态方法存在（自动定位或传入 adb 路径）。实现前先写探针集成测试（I5）确认 `AdbClient` 构造/连接、`AdbServer.StartServer`、`ExecuteRemoteCommand`（含 shell exit code 行为）三个签名，再落实现有代码。

**连接生命周期**：
```
构造:
  1. AdbServer.StartServer (如 5037 未运行)
  2. new AdbClient(...)   ← 构造/连接签名以 Phase 2 探针实测为准
                             (文档示例: 参数构造器 + Connect("127.0.0.1:5037"))
  3. Connect to device by serial

每次命令执行:
  1. await _semaphore.WaitAsync(ct)
  2. 执行命令
  3. 如连接断开 → Reconnect() 重试
  4. _semaphore.Release()

DisposeAsync:
  1. _client.Dispose()
  2. _semaphore.Dispose()
```

**三级自愈**（在每次命令执行时内嵌）：

| 尝试 | 策略 | 延迟 |
|------|------|------|
| 1 | 即时重连 | 0ms |
| 2 | 退避 + AdbServer.StartServer 重启 adb server | 500ms |
| 3 | 最后尝试 | 1000ms |

3 次全失败 → 抛出 `AdbCommandException`。不引入无限重试——死循环重连比快速失败更危险。

### 2.3 ProcessAdbSession 降级实现

放 `src/UniClaw.Device/ProcessAdbSession.cs`。

```csharp
public sealed class ProcessAdbSession : IAdbSession
{
    private readonly AdbCommandRunner _runner;

    // CaptureScreenshotAsync → _runner.RunAsync(exec-out screencap -p, CaptureBinaryOutput: true)
    // ExecuteShellAsync     → _runner.RunAsync(shell <cmd>)
    // DumpUiHierarchyAsync  → _runner.RunAsync(dump) + _runner.RunAsync(cat)
    // DisposeAsync          → no-op (ProcessAdbSession 无持久状态)
}
```

**目的**：
- 允许 `AdvancedSharpAdbSession` 和 `ProcessAdbSession` 并行存在
- CI 环境不能装 NuGet 包时 fallback
- 通过 `UNICLAW_ADB_BACKEND=sharp` 或构造器参数切换

## 3. Consumer Migration

### 3.1 生产消费者（6 文件）

| 文件 | 改动 | 说明 |
|------|------|------|
| `AdbScreenCapture.cs` | `IAdbCommandRunner _runner` → `IAdbSession _session` | `RunAsync(exec-out screencap -p, CaptureBinaryOutput: true)` → `CaptureScreenshotAsync()` |
| `AdbActionExecutor.cs` | 同上 | `RunShellAsync` → `ExecuteShellAsync`；`GetScreenDimensionsAsync` 中 `wm size` 改用 `ExecuteShellAsync` |
| `AdbScreenStateProvider.cs` | 同上 | 两次 `RunAsync`（dump + cat）→ 一次 `DumpUiHierarchyAsync()`；去掉 `RemotePath` 常量 |
| `AdbEntryActionDriver.cs` | 同上 | 全部 `RunAsync` → `ExecuteShellAsync` |
| `ScenarioObservation.cs` | 同上 | 字段类型替换 |
| `HostCommands.cs` | 同上 | 字段/参数类型替换（L104 字段 + L815/L1165 参数，共 3 处） |

### 3.2 构造器双签名（向后兼容）

现有便捷构造器保留语义：

```csharp
// AdbActionExecutor 示例
public AdbActionExecutor(IAdbSession session, TimeSpan? timeout = null);  // 主路径

public AdbActionExecutor(string serial, string adbPath = "adb", TimeSpan? timeout = null)
    : this(new ProcessAdbSession(new AdbCommandRunnerOptions(serial, adbPath, timeout)), timeout)
{
}
```

第二个构造器内部从 `new AdbCommandRunner(...)` 变为 `new ProcessAdbSession(new AdbCommandRunnerOptions(...))`。HostCommands 装配逻辑零改动即可编译。

### 3.3 测试 Fakes（3 文件）

| Fake 类 | 文件 | 改动 |
|---------|------|------|
| `FakeAdbRunner` | `AdbDeviceBoundaryTests.cs:345` | `IAdbCommandRunner` → `IAdbSession`；需实现 3 方法而非 1 方法，但返回类型简化（`byte[]` / `ShellResult` / `string` 比构造 `AdbCommandResult` 简单） |
| `FakeAdbRunner` | `RunnerTestHarness.cs:144` | 同上 |
| `FakeRunner` | `HostCommandTests.cs:211` | 同上 |
| `AdbTestContext` | `AdbTestContext.cs:16` | 字段类型 `IAdbCommandRunner` → `IAdbSession` |

### 3.4 装配点（HostCommands）

`HostCommands.cs` 的 `CreateRunner`（L838）改为按 `UNICLAW_ADB_BACKEND` 环境变量选择：`sharp` → `new AdvancedSharpAdbSession(serial)`，`process` → `new ProcessAdbSession(...)`（默认 `sharp`）。`IAdbCommandRunner` 字段/参数类型（L104/L815/L1165）同步替换为 `IAdbSession`。

### 3.5 可删除的类型（迁移完成后）

`AdbCommandRunner`、`AdbCommandRequest`、`AdbCommandResult`、`AdbCommandFailure`、`AdbCommandRunnerOptions` — 5 个类型。

`AdbCommandException` 保留：`ShellResult.Success == false` 或自愈重试耗尽可能时仍用它抛异常。构造器从 `(string operation, AdbCommandResult result)` 改为 `(string operation, ShellResult result)`，`Result` 属性类型同步改为 `ShellResult`（`AdbCommandResult` 已删除，见 §4.1）。

## 4. Error Handling

### 4.1 异常层级

```
AdbCommandException (保留，构造器与 Result 属性改为携带 ShellResult)
  ├─ 连接失败: "ADB session connection lost after N retries"
  ├─ 命令失败: ShellResult.Success == false → "shell command failed: {stderr}"
  └─ 截图失败: 空输出 → "screenshot capture returned no bytes"

OperationCanceledException
  └─ CancellationToken 取消 → 透传

ArgumentException
  └─ serial 为空 / 非法 → 构造期 fail-fast（对齐现有 AdbCommandRunner 行为，A6）

**`ShellResult.Success` 判定**：adb shell 经典传输不返回进程 exit code（shell_v2 协议才支持）。`Success =` 执行未抛异常 且（包暴露 shell_v2 exit code 时 == 0；否则 stderr 为空）。具体行为由 Phase 2 探针测试（I5）确认。
```

### 4.2 超时控制

`AdvancedSharpAdbSession` 构造函数接受 `TimeSpan? defaultTimeout`（默认 30s，对齐现有 `AdbCommandRunnerOptions` 默认值）。每个命令方法接受独立的 `CancellationToken`，不额外包装超时——超时由 `CancellationTokenSource.CancelAfter` 在调用方控制，保持与现有模式一致。

## 5. NuGet 依赖

在 `UniClaw.Device.csproj` 中添加：

```xml
<PackageReference Include="AdvancedSharpAdbClient" Version="*" />
```

这是 `UniClaw.Device` 项目的第一个 NuGet 包引用（当前零包）。

## 6. Testing Strategy

### 6.1 单元测试（不改动现有）

- 3 个测试 fake 更新为 `IAdbSession` 后，现有单元测试**断言需改写**（`ShellResult` 无 `ExitCode`/`Arguments`/`Duration`/`BinaryOutput`/`Failure` 字段，改为断言 `Success`/`StandardOutput`），行为语义不变。
- `AdvancedSharpAdbSession` 本身不新增单元测试——它对 NuGet 包有硬依赖，mock ADB 协议本身价值低。
- A5 的 `ProcessAdbSession` 等价性测试（同输入 → 同输出）为新增单元测试。

### 6.2 集成测试

- 新增 `AdbSessionIntegrationTests`（emulator-gated，加入既有 skip 列表 `docs/validation/unit_test_status.md` 的 9 个 emulator-gated group）。
- 测试内容：截图捕获、shell 命令、UI 层级 dump、自愈重连（手动 kill adb server 后验证自动恢复）。
- `ProcessAdbSession` 与 `AdvancedSharpAdbSession` 对比测试：同一设备、同一操作、结果一致。

### 6.3 Architecture Guard

- 不新增 Guard 测试（本改动在 Device 层，Core 层不感知 `IAdbSession`）。
- `IAdbSession` 3 方法锁定：后续可加 Guard（参考 `IScreenStateProvider` 4 方法锁定的模式），本期不强求。

## 7. Rollout Sequence

```
Phase 1: IAdbSession + ProcessAdbSession
  1. 添加 IAdbSession + ShellResult + ProcessAdbSession
  2. 迁移 4 消费者 + 3 测试 fake（接口切换，行为不变）
  3. 删除 AdbCommandRequest/AdbCommandResult/AdbCommandFailure/AdbCommandRunnerOptions
  4. AdbCommandRunner 保留（ProcessAdbSession 内部使用）
  5. 全量测试通过

Phase 2: AdvancedSharpAdbSession
  1. 添加 NuGet 包引用
  2. 实现 AdvancedSharpAdbSession
  3. HostCommands 装配点切到 AdvancedSharpAdbSession
  4. 集成测试（emulator-gated）

Phase 3: Cleanup
  1. AdbCommandRunner 标记 [Obsolete] 或删除（如 Phase 1 确认无外部引用）
  2. ProcessAdbSession 降级为 opt-in fallback（UNICLAW_ADB_BACKEND=process）
```

## 8. Acceptance Criteria

### 8.1 硬性验收（单元测试全绿）

| # | 标准 | 验证方式 |
|---|---|---|
| A1 | `IAdbSession` 接口 3 方法 + `IAsyncDisposable`，编译通过 | dotnet build |
| A2 | `ShellResult` 替换 `AdbCommandResult`，4 个消费者编译通过 | dotnet build |
| A3 | 3 个测试 fake（`FakeAdbRunner` ×2 + `FakeRunner`）迁移到 `IAdbSession`，现有测试全绿 | `dotnet test` 0 fail |
| A4 | `ArchitectureGuardTests` 全绿（`IScreenStateProvider` 4 方法锁定、依赖方向等） | `dotnet test --filter ArchitectureGuard` |
| A5 | `ProcessAdbSession` 包装 `AdbCommandRunner`，3 方法行为与原有 `AdbCommandRunner.RunAsync` 一致 | 单元测试：同输入 → 同输出 |
| A6 | `AdvancedSharpAdbSession` 构造期 serial 为空 → `ArgumentException` | 单元测试 |
| A7 | `IAdbSession.DisposeAsync()` 释放后再次调用 → `ObjectDisposedException` | 单元测试 |

### 8.2 集成验收（emulator-gated，Phase 2）

| # | 标准 |
|---|---|
| I1 | `AdvancedSharpAdbSession` shell 命令结果与 `AdbCommandRunner` 一致 |
| I2 | `CaptureScreenshotAsync()` 返回非空 PNG |
| I3 | 手动 `adb kill-server` 后下一次命令自动恢复 |
| I4 | `ProcessAdbSession` 与 `AdvancedSharpAdbSession` 对比测试通过 |
| I5 | API 探针测试：确认 `AdbClient` 构造/连接、`AdbServer.StartServer`、`ExecuteRemoteCommand`（含 shell exit code 行为）的实际签名；与文档假设不符时先更新本文档再继续 |

### 8.3 性能指标（待办，不阻塞合入）

> 记入 `docs/validation/unit_test_status.md` backlog，后续 benchmark 脚本覆盖。

| # | 标准 | 目标 |
|---|---|---|
| P1 | `CaptureScreenshotAsync` 延迟 | < 200ms (1080P) |
| P2 | `ExecuteShellAsync("input tap")` 延迟 | < 5ms |
| P3 | 连续 1000 次命令无 socket 泄露 | `netstat` 连接数不增长 |

## 9. Decisions

| ID | 决策 | 理由 |
|----|------|------|
| D-1 | 3 方法锁定，不加 RunAsync 泛化方法 | 避免 stringly-typed 抽象；新需求扩展方法而非加参数 |
| D-2 | SemaphoreSlim 串行化，不引入 Channel/队列 | ADB 场景命令量低（每 step 2-5 条），串行化简单且安全 |
| D-3 | 三级自愈，不无限重试 | 死循环重连卡死整个 run；快速失败让 FSM 走 Error 路径 |
| D-4 | ProcessAdbSession 保留为降级方案 | CI 环境兼容性；零风险切换 |
| D-5 | DumpUiHierarchyAsync 内部合并两步 | 调用方不关心文件路径是封装的基本要求 |
| D-6 | 保留 AdbCommandException，构造器改携 ShellResult | 最小化消费者改动；catch 语句不变 |
| D-7 | Phase 2 探针测试先行确认包 API 签名（构造器/StartServer/exit code） | 包文档与示例存在偏差，探针消除按未验证签名编码的风险 |
| D-8 | `ShellResult.Success` 显式定义（无异常 + shell_v2 exit code==0 或 stderr 为空） | adb shell 经典传输不返回 exit code，判定必须显式 |
