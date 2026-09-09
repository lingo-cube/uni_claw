# Plan — ADB-001 ADB live 执行层

> PlanType: adb-001-live-driver / Status: ADOPTED /
> References: changes/ADB-001/state.md · legacy AdbProcessRunner/AdbDispatchTarget
> （uni-agent 实证，DIRECT IDEA）· DSE-001 HD-1 裁决词汇 · DSE-002 locator 协议

## 切片

```text
DispatchRequest → AdbTapCommand.TryBuild（共享：支持集+投影+args；dry-run 同源）
    ↓
AdbProcessRunner.RunAsync(adb, [-s, serial, shell, input, tap, X, Y], 10s)
    ↓ 三态真实映射（legacy 实证 × HD-01 词汇）：
  TimedOut        → UnknownOutcome(timeout-killed)   设备侧效果未知→re-observe
  !Started/Exit≠0 → DeliveryFailed(transport)+StdErr diagnostic
  Exit==0         → DeliveryCompleted + Report=命令串
```

## Before / After

| 文件 | 变化 |
|---|---|
| `Effects/AdbProcess.cs`（新） | internal `IAdbProcessRunner` + `AdbProcessRunner`（legacy 平移：64MiB bound、linked CTS、Kill(entireProcessTree)、AdbProcessResult） |
| `Effects/AdbEffectDriver.cs` | 投影/支持集/args 提取为 `internal static AdbTapCommand.TryBuild`；dry-run 行为零变化 |
| `Effects/AdbLiveEffectDriver.cs`（新） | ctor(serial, viewportW/H, IAdbProcessRunner?, adbExe="adb", clock?)；Deliver=TryBuild→runner→三态映射；同步等待 ≤10s（债标注：异步签名=HD-1 第二阶段 buyer） |
| `tests/.../AdbLiveDriverTests.cs`（新） | L1 fake 三态 / L2 共享构造回归 / L3 EnvLite（真 adb 假 serial，adb 缺席时显式跳过）/ L4 既有回归 |

## 验收映射
state.md L1–L4 → 测试内同名。
