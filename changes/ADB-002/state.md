# ADB-002 — 真机 ENVIRONMENT 验收（模拟器端到端：locator→tap→世界变化观察证实）
lifecycle_state: closed · disposition: none · depth: standard · base: b7f430d1

## Intent（WHAT/WHY)
ADB-001 的 L3 只验证了失败路径（假 serial）。本 change 补上真实设备上的
完整验收：SpatialLocator（真实归一化 bounds）→ AdbLiveEffectDriver →
真实 `input tap` → **世界状态变化经再观察证实**（uiautomator dump 的
checked 翻转）。设备/配置来自 legacy 考古（uni-agent）：
- AVD `p26_pixel`（本机 ~/.android/avd，1080×2400@420dpi，legacy
  HANDOFF 启动命令）+ emulator 二进制
  /opt/homebrew/share/android-commandlinetools
- 启动入口 `am start -a android.settings.WIFI_SETTINGS`（legacy
  ci-emulator-precondition 同款）
- golden-run device-profile：1080×1920@420dpi（corpus 归一化假设证实）

## Scope
- `tests/.../AdbEnvironmentTests.cs`：ENVIRONMENT 级 [Fact]
  （`DSH_TEST_ADB_ENV=1` 显式启用；默认跳过并说明——启用时按 legacy
  Tier-2 约定 fail-closed 不静默）。流程：am start → dump 解析 Wi-Fi
  Switch 真实 bounds/checked → wm size 动态读 viewport（不硬编码）→
  SpatialLocator → Deliver("set-switch") → 断言 DeliveryCompleted +
  命令串像素精确 → dump 复验 checked 翻转（attempt≠effect：完成由
  观察证实）→ 对称二次（翻回）。
- I-3 活教材注释：checked=true 时盲 tap → false（破坏已满足状态）——
  CDS-001 satisfaction 闸的物理必然性在真机上的直接展示。
- 【收口会话增补，Human 指令「注册相关配置，成为测试常用模拟器」】
  `docs/agents/test-emulator.md`：注册测试常用模拟器——AVD p26_pixel /
  serial emulator-5554 / emulator 二进制与 boot 命令 / Wi-Fi Settings
  环境入口 / golden-run device-profile / 使用约定（显式启用 +
  fail-closed + 生命周期外部管理 + 确定性纪律）/ 运维注意（快照锁残留
  处置——收口会话实测）。本仓 ENVIRONMENT 级验收的唯一注册设备目标，
  变更须同步本文件与 AdbEnvironmentTests 并在对应 change 留痕。

## Out of Scope（禁止）
- 观察侧真机化（dump 解析是测试内 minimal 替身；实时 perception 链
  独立 buyer）；Control policy 真机编排；模拟器生命周期管理（外部
  启动，测试不负责 boot）。

## Acceptance
V1 启用时端到端 GREEN：真实 tap 命令像素精确（969,897 @1080×2400）、
   checked true→false→true 两段翻转均经 dump 证实
V2 未启用时全量套件零影响（显式跳过 + 说明输出）

## Verification
```yaml
verification:
  level: ENVIRONMENT（V1；DSH_TEST_ADB_ENV=1）+ DETERMINISTIC（V2）
  method: 模拟器 emulator-5554 在线时 dotnet test --filter AdbEnvironmentTests；
          全量回归 ×1
  expected: V1 两段翻转断言通过；V2 全量绿
  actual: >
    V1 实测通过（DSH_TEST_ADB_ENV=1，模拟器 emulator-5554 在线，11s）：
    p26_pixel（1080×2400 动态读取）Wi-Fi Settings Switch checked=true
    bounds=[901,834][1038,960]（center 969,897 → 归一化 0.897/0.374 →
    SpatialLocator frame=device-viewport）→ AdbLiveEffectDriver.Deliver
    → DeliveryCompleted + Report "adb -s emulator-5554 shell input tap
    969 897"（像素精确）→ dump 复验 checked=false（真实世界翻转，由
    观察证实非由 receipt）→ 对称二次 → checked=true。V2 默认模式全量
    249/249 GREEN（232 Kernel + 17 Agent；ENVIRONMENT 用例显式跳过零影响）。
    模拟器验收后 emu kill 干净关闭。
  evidence: 测试输出（2026-09-09，两次：ENV=1 filter 跑 1/1 + 默认全量）；
    /tmp/wifi.xml dump；adb devices 空确认关机
```

## Status log
2026-09-09 · understanding→resolved→persisted→planned→implemented · legacy
  模拟器考古（p26_pixel AVD 本机在场 + emulator 二进制 + Wi-Fi Settings
  入口 + golden-run device-profile 证实归一化假设）→ boot 实测（10s
  完成）→ 页面事实采集（Switch checked=true bounds/center）→ 测试落地
  （环境开关 + fail-closed 约定折中）

2026-09-09 · implemented→reviewed→verified→closed · V1 真机 GREEN（两段
  翻转 + 像素精确命令 + attempt≠effect 由 dump 证实）+ V2 默认全量 249/249
  → REAL_DEVICE_END_TO_END_VERIFIED。legacy 模拟器资产（p26_pixel/
  emulator 二进制/Wi-Fi 入口/golden-run device-profile）考古成功并全数
  复用；模拟器已关闭
2026-09-10 · 收口复验修订（A1 不信记录本身；Human 指令注册测试常用模拟器）·
  REVIEW 发现 V2 实现缺陷：RequireEnvironment() 空实现不构成跳过——未
  启用时测试体照样执行 adb（其 V2 全量绿实为模拟器恰好在线的侥幸）。
  修复：测试体前置真跳过守卫（未启用 → 零 adb 调用 + 输出跳过说明）+
  注册文档 docs/agents/test-emulator.md（含快照锁残留处置：硬杀后
  rm ~/.android/avd/p26_pixel.avd/*.lock 再 boot）。复验三路：未启用
  （模拟器在线）2ms 真跳过；启用 V1 真机 GREEN 10s（tap 969 897 →
  dump 证实 checked 翻转两段）；默认全量 279/279 GREEN @ 88e959a3
  （262 Kernel 含并行 WMP-001 新增 + 17 Agent）。修订后 V2 声明与代码
  一致
