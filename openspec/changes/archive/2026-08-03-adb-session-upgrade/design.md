## Context

当前 `AdbCommandRunner` 通过 `Process.Start("adb.exe")` 执行每次 ADB 交互，每次调用的进程启动开销（~50-200ms Windows）在车机自动化场景不可忽略。`IAdbCommandRunner` 是 stateless 接口，无法表达长连接生命周期。`AdbCommandRequest`/`AdbCommandResult` 将 ADB 内部细节（参数数组、exit code、stdout/stderr 字符串）泄露给所有消费者，每个消费者自己做正则解析。

`UniClaw.Device` 项目当前零 NuGet 包依赖。`AdvancedSharpAdbClient` 是 .NET 生态中成熟的 ADB 客户端库，通过 TCP 连接到 ADB server（127.0.0.1:5037）并维护长连接。

**约束**：
- 改动限定 `src/UniClaw.Device/` + 消费者迁移，Core 层不感知
- `IScreenStateProvider` 已有 4 方法锁定的 ArchitectureGuard 先例
- CI 环境可能无法安装 NuGet 包，需保留降级路径
- 现有便捷构造器语义保留（向后兼容）

## Goals / Non-Goals

**Goals:**
- 定义 `IAdbSession : IAsyncDisposable` 接口，3 方法覆盖全部现有 ADB 交互
- 基于 `AdvancedSharpAdbClient` 实现 TCP 长连接 ADB 会话
- 引入 `ShellResult` 替代 stringly-typed `AdbCommandResult`
- 保留 `ProcessAdbSession` 降级方案（CI 兼容）
- 迁移全部消费者到 `IAdbSession`，删除冗余类型

**Non-Goals:**
- 不在 `IAdbSession` 上添加泛化 `RunAsync` 方法（保持类型安全，避免 stringly-typed）
- 不引入并发命令支持（`SemaphoreSlim` 串行化，接入场景命令量低）
- 不改变 ADB 命令语义（同样的 shell 命令、同样的输出）
- 不修改 Core 层任何类型
- 本期不强求 `IAdbSession` 的 ArchitectureGuard 方法数锁定测试

## Decisions

### D-1: 3 方法锁定，不加 RunAsync 泛化方法

**选择**: `IAdbSession` 仅定义 `CaptureScreenshotAsync`、`ExecuteShellAsync`、`DumpUiHierarchyAsync`。

**理由**: 避免 stringly-typed 抽象——当前 `AdbCommandRequest` 让消费者自行拼装命令字符串并解析 stdout，引入 `IAdbSession` 就是为了消除这种模式。新需求应扩展新方法而非加参数。

**替代方案**: 同时提供 `RunAsync(string command)` 兜底方法 → 拒绝，违背去 stringly-typed 的目标。

### D-2: SemaphoreSlim(1,1) 串行化，不引入 Channel/队列

**选择**: 每次命令执行前 `await _semaphore.WaitAsync(ct)`，执行后 `Release()`。

**理由**: ADB 场景命令量低（每 step 2-5 条），单条 ADB 命令 ~2ms。AdvancedSharpAdbClient 底层单 Socket，并发命令会导致帧交错。串行化开销可忽略。

**替代方案**: `Channel<T>` 或 `ActionBlock<T>` 队列 → 拒绝，过度设计，单 Socket 场景不需要。

### D-3: 三级自愈，不无限重试

**选择**: 每次命令执行内嵌三级重试（即时重连 / 500ms 退避 + 重启 adb server / 1000ms 最后尝试），3 次全失败抛 `AdbCommandException`。

**理由**: 死循环重连比快速失败更危险——卡死整个 run。快速失败让 FSM 走 Error 路径。

**替代方案**: 指数退避无限重试 → 拒绝，可能导致 run 永久卡死。

### D-4: ProcessAdbSession 保留为降级方案

**选择**: 同时提供 `ProcessAdbSession`（包装 `AdbCommandRunner`）和 `AdvancedSharpAdbSession`，通过 `UNICLAW_ADB_BACKEND` 环境变量切换。

**理由**: CI 环境可能无法安装 NuGet 包；零风险切换——`process` 模式行为与现有完全一致。

### D-5: DumpUiHierarchyAsync 内部合并两步

**选择**: 方法内部合并 `uiautomator dump` + `cat` 为一次调用，调用方不关心文件路径。

**理由**: 封装的基本要求——当前消费者需要知道 `RemotePath` 常量并分两次调用 `RunAsync`。

### D-6: 保留 AdbCommandException，构造器改携 ShellResult

**选择**: `AdbCommandException` 类保留，构造器从 `(string, AdbCommandResult)` 改为 `(string, ShellResult)`，`Result` 属性类型同步变更。

**理由**: 最小化消费者改动——现有 `catch (AdbCommandException)` 语句不变。

### D-7: Phase 2 探针测试先行确认包 API 签名

**选择**: 实现 `AdvancedSharpAdbSession` 前，先写探针集成测试确认 `AdbClient` 构造/连接、`AdbServer.StartServer`、`ExecuteRemoteCommand`（含 shell exit code 行为）三个签名。

**理由**: 包文档与社区示例存在偏差，探针消除按未验证签名编码的风险。

### D-8: ShellResult.Success 显式定义

**选择**: `Success =` 执行未抛异常 且（包暴露 shell_v2 exit code 时 == 0；否则 stderr 为空）。

**理由**: adb shell 经典传输不返回进程 exit code（shell_v2 协议才支持），判定必须显式。

## Risks / Trade-offs

- **[Risk] AdvancedSharpAdbClient 包 API 与文档不符** → Phase 2 探针测试（I5）先行确认，不符时先更新本文档再编码
- **[Risk] TCP 长连接断开未感知** → 三级自愈在每次命令执行时检测并重连；极端情况下最多损失一次命令
- **[Risk] NuGet 包引入后 CI 编译失败** → `ProcessAdbSession` 不依赖 NuGet 包，`UNICLAW_ADB_BACKEND=process` 完全绕过
- **[Risk] 串行化成为瓶颈** → 当前场景命令量低（每 step 2-5 条），串行化延迟可忽略；未来如需并发可替换 SemaphoreSlim 为 Channel
- **[Trade-off] 首个 NuGet 包引入** → `UniClaw.Device` 从零包到有包，增加了依赖管理负担，但 `AdvancedSharpAdbClient` 是成熟包（NuGet 下载量高），收益远大于风险

## Migration Plan

### Phase 1: IAdbSession + ProcessAdbSession
1. 添加 `IAdbSession` + `ShellResult` + `ProcessAdbSession`
2. 迁移 4 消费者 + 3 测试 fake（接口切换，行为不变）
3. 删除 `AdbCommandRequest`/`AdbCommandResult`/`AdbCommandFailure`/`AdbCommandRunnerOptions`
4. `AdbCommandRunner` 保留（`ProcessAdbSession` 内部使用）
5. 全量测试通过

### Phase 2: AdvancedSharpAdbSession
1. 添加 NuGet 包引用
2. 探针测试确认 API 签名（`AdbClient` 构造/连接、`AdbServer.StartServer`、`ExecuteRemoteCommand`）
3. 实现 `AdvancedSharpAdbSession`
4. `HostCommands` 装配点默认切到 `AdvancedSharpAdbSession`
5. 集成测试（emulator-gated）

### Phase 3: Cleanup
1. `AdbCommandRunner` 标记删除（如 Phase 1 确认无外部引用）
2. `ProcessAdbSession` 降级为 opt-in fallback（`UNICLAW_ADB_BACKEND=process`）

### Rollback
- 设置 `UNICLAW_ADB_BACKEND=process` 即可回退到 Process-per-command 模式
- 如需完全回退 NuGet 包：移除 `PackageReference`，恢复删除的类型（从 git history）

## Open Questions

- `AdvancedSharpAdbClient` 包的实际 API 签名（构造器参数、`Connect` 方法签名、`ExecuteRemoteCommand` 返回类型）——由 Phase 2 探针测试（I5）确认
- `ExecuteRemoteCommand` 是否暴露 shell_v2 exit code ——影响 `ShellResult.Success` 判定逻辑
- `AdbCommandRunner` 是否有 `UniClaw.Device` 项目外的引用 ——决定 Phase 3 能否直接删除
