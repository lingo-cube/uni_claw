# Slice 6 Spec Review v3

固定基线 `a05176f3c66e0a3206f49dde463f51df1335975c`；仅复核 M1 与 v2→v3。23 个 `candidate-inputs-v3.json` SHA 全匹配 `candidate-source-v3`，成员集合与 v2 相同；增量仅为 `StorageClosureFileTests.cs`（`candidate-v2-v3.patch:1-40`）。未运行构建、测试、live 或网络操作。

## 缺失/部分（0）

M1 已关闭：F4 要求“两正式Runner离线产物/关闭回归”（`archive-init-close-group.md:17`）。新增暂停消息循环下的公开 `HostRunner.RunOnce` 回归（`candidate-source-v3/tests/UniClaw.Host.Tests/StorageClosureFileTests.cs:170-200`）：真实 Evidence/World factory 与 World Owner 初始化完成后，缺 `DeviceId` 在 delivery/感知拉取之前按既有失败路径停下（`candidate-source-v3/src/UniClaw.Host/HostRunner.cs:171-188`）；断言 failure 产物、两个来源只读打开、World initialize 记录及咨询为零（测试:191-199）。`host-once-bridge.trx` 为 1/1 通过（:8,23-30）。这证明同步桥/产物/关闭的离线边界，不宣称真机正常 run。

## 越界（0）

没有产品源码改变；不触及 Owner、记录顺序、取消、P24、历史读取、队列、DB、续跑或路径优化。

## 看似实现、实际错误（0）

新增测试复用公开失败边界，未伪造 delivery、模型或设备调用；最终 Host 回归尚在运行，Kernel/DSH 同源结果复用不作源码缺陷。

**计数：缺失/部分 0；越界 0；看似实现实际错误 0。**
