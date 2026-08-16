# §33 GATE DECISION — Physical Device Binding is Emulator-Only (no real phone)

- **Authority**: `PROJECT_LEADER_PHYSICAL_WIFI_OFF_TO_ON_IMPLEMENTATION_GATE`（授权链终点 `APPROVED_SLICE_1_AND_SLICE_2`，见 `implementation-authorization-physical-wifi-off-to-on.md`）
- **Change**: `openspec/changes/physical-wifi-off-to-on-minimum-semantic-loop/`
- **Date**: 2026-08-12
- **Status**: 记录于真实接线（tasks 2.2 `PhysicalEnvironment` 接线）之前 — 满足 tasks 1.1「任何真实接线实现前必须完成」

## 决策

**本 change 的一切真实设备绑定只允许 emulator（AVD `uniclaw-lite-api35`，serial `emulator-5554`）；禁止连接真实手机。**

## 依据

- 宪章 §33「第一阶段不要连接真实手机」：第一阶段先建立可模拟环境以保证确定性架构测试；Runtime Kernel 稳定后再接入真实设备。
- 本 change 的真实 IO 目标（新鲜 Observation / 动作后 fresh screenshot / perception evidence）在 emulator 上可完整达成：
  - `AdbScreenshotSource`（screencap，§33 允许的 Android Adapter 路径）
  - `LocalVisionPerceptionSource`（本机 vision server，非外部真实设备）
  - `AdbDispatchTarget`（`adb shell input tap` — 仅 UI 语义环，无隐藏 API / 无 `svc wifi` / 无 `cmd wifi`，spec 实施约束）
- 确定性保障：emulator 由 `config.ini` 强制 cold boot（`fastboot.forceColdBoot=yes`）；Tier 2 集成测试以 emulator 为前置，无 emulator 时显式失败（tasks 7.1）。

## 范围

| 允许 | 禁止 |
|---|---|
| AVD emulator（uniclaw-lite-api35, x86_64, android-35） | 真实手机 / 任何 USB 物理设备 |
| adb screencap / input tap / getprop（标准 adb 接口） | `svc wifi` / `cmd wifi` / emulator console wifi 命令 / UiAutomator 隐藏接口 |
| 本机 vision server（UDS `/tmp/uniclaw-vision.sock`） | 外部 AI provider / 网络摄像头 |

## 复核

- spec.md「实施约束」、design.md「Implementation Constraints」、tasks.md 6.7 已同步此边界。
- 此记录为 Slice 1 前置（tasks 1.1）✅；Slice 2 校准（tasks 5.1）亦在本边界内。
