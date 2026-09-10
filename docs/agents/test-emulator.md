# 测试常用模拟器（注册环境）

> 本仓 Product Runtime 一切 ENVIRONMENT 级验收（真实设备目标）的**注册
> 标准模拟器**。ENVIRONMENT 类测试不得另起设备目标；变更本文件登记的
> 任何事实须同步 `tests/UniClaw.Kernel.Tests/AdbEnvironmentTests.cs` 并
> 在对应 change 留痕。

## 注册事实（provenance：legacy uni-agent 考古 + ADB-002 实测）

| 项 | 值 |
|---|---|
| AVD | `p26_pixel`（本机 `~/.android/avd/`，1080×2400 @ 420dpi） |
| serial | `emulator-5554` |
| emulator 二进制 | `/opt/homebrew/share/android-commandlinetools/emulator/emulator` |
| adb | Homebrew `adb`（37.0.1 实测） |
| boot 命令 | `/opt/homebrew/share/android-commandlinetools/emulator/emulator -avd p26_pixel`（legacy HANDOFF 同款；boot 实测 ~10s） |
| 环境入口（ADB-002） | `adb -s emulator-5554 shell am start -a android.settings.WIFI_SETTINGS`（Wi-Fi Settings 页，含 checkable Switch） |
| golden-run device-profile | 1080×1920 @ 420dpi（corpus 归一化假设的设备事实来源） |
| 备用 AVD | `scroll-test`（在场，未注册——无 buyer） |

## 使用约定

- **显式启用**：ENVIRONMENT 级测试以 `DSH_TEST_ADB_ENV=1` 开关启用；
  默认显式跳过（零影响全量套件——V2 断言面）。
- **fail-closed**：启用后前置缺失（模拟器离线 / adb 缺席 / 页面未打开）
  直接 FAIL，不静默跳过（legacy Tier-2 约定）。
- **生命周期外部管理**：测试不负责 boot/关机；模拟器由调用方预先启动
  （`adb devices` 应见 `emulator-5554  device`）。
- **确定性纪律**：ENVIRONMENT 测试只验证「真实物理映射 + 世界变化经
  再观察证实」，不做性能断言、不依赖时序精度（轮询间隔为宽松常量）。

## 运行速查

```bash
# 0) boot（若未在线）
/opt/homebrew/share/android-commandlinetools/emulator/emulator -avd p26_pixel &
# 1) 确认在线
adb devices | grep emulator-5554
# 2) 运行 ENVIRONMENT 验收
DSH_TEST_ADB_ENV=1 dotnet test --filter AdbEnvironmentTests
# 3) 全量回归（默认跳过环境测试）
dotnet test
```

### 运维注意（实测）

- **快照锁残留**：上一次模拟器进程被硬杀后再 boot 报
  `FATAL | A snapshot operation ... pending and timeout has expired`。
  处置：确认无 emulator 进程（`pgrep -fl qemu-system`）后
  `rm ~/.android/avd/p26_pixel.avd/*.lock`，再正常 boot。
- boot 到 `sys.boot_completed=1` 实测 ~30s；`adb devices` 出现
  `device` 状态即可用。

## 相关 change

- ADB-001（L3-lite：真实 adb 二进制 × 假 serial，失败通道）
- ADB-002（V1：locator → tap → dump 证实世界翻转；本环境注册的建立者）
