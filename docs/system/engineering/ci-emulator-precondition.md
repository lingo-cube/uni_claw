# CI Emulator Precondition — Tier 2 (Real Integration)

> 来源：`openspec/changes/physical-wifi-off-to-on-minimum-semantic-loop/design.md`
> 「Implementation Slices」Tier 2 定义与 Open Question 3。
> 任务 7.1 约束记录。最后更新: 2026-08-14

## 规则

1. **Tier 2（真实集成）需要 emulator 前置**。运行前必须存在可达的 emulator
   （`adb devices` 解析到非空 serial，`AdbDevicePreflight` 4 轴 readiness 通过）。
2. **显式失败，不静默 Skip**。与 CORR_HOST03/04（`VisionHostFactoryCompositionTests`）
   同一约定：长超时 + 显式失败报告；前置不满足时测试/证明以 FAIL/非零退出结束，
   绝不静默跳过并假装通过。
3. **Tier 0/1/3 无环境依赖**，可在无 emulator 的 CI 上正常运行。

## 降级策略（Open Question 3 的回答）

- 若 CI **无** emulator runner：Tier 2 不在 CI 执行；改为本地验证 + 显式失败说明
  （本记录即降级说明的权威位置），CI 只跑 Tier 0/1/3 + 约束/一致性检查。
- 若 CI **有** emulator runner：Tier 2 按上表完整执行，前置失败 = 构建失败。

## 本地验证基线（Slice 2 证明使用的环境事实）

| 项 | 值 |
|---|---|
| AVD | `uniclaw-lite-api35`（Android 15 / API 35，1080×1920 @ 420dpi） |
| serial | `emulator-5554` |
| adb | `/usr/local/bin/adb` |
| 视觉服务 | `platforms/perception` Uvicorn，UDS `/tmp/uniclaw-vision.sock` |
| 启动机制 | `am start -a android.settings.WIFI_SETTINGS`（`--launch-intent`，锁定机制） |
| 证明入口 | `dotnet run --project src/UniClaw.Runtime.PhysicalHost -- --slice2 --adb <adb> --serial <serial> --vision-socket <sock>` |
| 期望 | exit 0 + `PROOF-SLICE2` 全 true（satisfied / exactlyOneSetSwitch / freshObservationAdvanced / sourcePointsAtFresh / perceptionSwitchOn） |

F2（设备不可用）与 F6-live（perception 不可用）现场反证均为显式非零退出
（exit 2）+ 非 SATISFIED 终止，见 Slice 2 运行证明记录。
