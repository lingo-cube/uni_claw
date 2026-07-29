# Device Layer

> **Tier 3 · Layers**: Android/ADB 具体实现边界。Device 引用 Core 抽象，
> Core 不反向引用 Device。

## Responsibilities

`UniClaw.Device` 只负责 Android 平台动作与观测，不负责编译场景、决定安全性
或解释业务成功：

- `AdbCommandRunner`：统一 serial、timeout、cancellation、stdout/stderr、
  binary output 与结构化失败。
- `AdbScreenCapture`：`exec-out screencap -p`，拒绝空输出。
- `AdbScreenStateProvider`：UIAutomator hierarchy、滚动能力、真实
  no-scroll 与 verified end-of-list；ADB/XML 失败不得伪装为完成。
- `AdbActionExecutor`：标准化坐标到物理像素，执行 click/back/scroll/text，
  并保留不含输入秘密的 action history。
- `AdbEntryActionDriver`：cold launch/deep link 与 package/text wait condition。

## Host Boundary

Device executor/entry driver 在 Host composition 中被 safety decorator 包裹。
Device 本身不允许或拒绝动作；它只执行已通过 Host 确定性策略的参数化命令。
任何 AI/provider 都不得输出或直通原始 ADB 命令。

API 35 fixture 的前台 package 读取使用
`dumpsys activity activities`。Motion click 明确使用 display 0 的 mouse source；
坐标仍来自已验证 UI hierarchy 元素。

## Dependencies

```text
Device → Core abstractions/models
Host → Device
Core -X→ Device
Device -X→ Host/provider
```

## Verification

聚焦测试位于
`tests/UniClaw.Host.Tests/Device/AdbDeviceBoundaryTests.cs`，覆盖 serial 路由、
命令参数、超时/取消、截图、UI XML、screen state、entry 和 secret redaction。
真实 emulator 检查保持显式，不进入默认无设备测试集。
