# Slice 6 Spec Review v2

固定基线 `a05176f3c66e0a3206f49dde463f51df1335975c`；只增量审 v1→v2。`candidate-inputs-v2.json` 的 23 SHA 全匹配 `candidate-source-v2`；v2 差异只含 Driver、两个 Host 与闭包测试。`concurrent-inputs-v1.json` 的三个菜单图/项目路径与候选零重叠，未覆盖。未运行任何构建、测试、live 或网络操作。

## 缺失/部分（1）

- **M1（验证范围）**：F4 要求“两正式Runner离线产物/关闭回归”（`archive-init-close-group.md:17`）。新增暂停消息循环 RED→GREEN 只调用 `SettingsCoverageRunner.Run`（`candidate-source-v2/tests/UniClaw.Host.Tests/StorageClosureFileTests.cs:139-166`）；另一正式同步入口 `HostRunner.RunOnce`（`candidate-source-v2/src/UniClaw.Host/HostRunner.cs:90`）没有同类实测。补一个不运行消息循环的 `HostRunner.RunOnce` 回归，才能把“两 Host”等待在 v2 的同步桥保证证明完整。现有 GREEN 9/9 仅证明前者（`sync-context-green.trx:8-16,71-72`）。

## 越界（0）

变更仅为现有 await 加 `ConfigureAwait(false)` 与该测试；没有变更 Owner、记录顺序、取消、P24、历史读取、队列、DB、续跑或路径优化。Driver 与两 Host 的普通 await 均显式脱离上下文（`candidate-v1-v2.patch:3-1361`）。

## 看似实现、实际错误（0）

RED 确认旧同步桥在暂停 context 超时（`sync-context-red.trx:8-16,34-45`），GREEN 显示该测试通过；不将最终回归/封存进行中的元数据归为源码缺陷。

**计数：缺失/部分 1；越界 0；看似实现实际错误 0。**
