# Slice 2 Standards Review v2

本次仅补审 `tools/test_asset_operations.py`、其测试、`tools/test-assets` dispatcher，及 `test_assets.py` 的 receipt `operation` 消费；不重审产品/C#，v1 的有界通过结论保持。

## P2：replay 的 begin→SIGTERM handler 窗口会留下未收尾 Run

**位置：** `tools/test_asset_operations.py:260-271`。

`replay()` 先在 262 行调用 `begin()`（其间已登记 Run、写 provenance），直到 268-271 行才安装 SIGTERM handler/进入含 `finish()` 的路径。若进程在 `begin()` 返回后、269 行前收到 SIGTERM，默认处理会直接退出；Run 已存在却不会走 306 行 `finish()`，违反收尾规范对取消出口“先记录未完成消费者，再收尾”的要求。现有真实 writer 测试只在 `Popen` 后 123 行发 SIGTERM，未覆盖此窗口。

建议将取消保护及带 `started` 标识的外层 `try/finally` 覆盖 begin、replay 和 finish：SIGTERM 在 Run 已创建后必须使该 Run 以 `cancelled`/`consumersCompleted=false` 完成；仅在尚未创建 Run 时才可无 receipt 返回。恢复旧 handler 应置于收尾之后，避免 finalizer 期间再次终止。

其余定点项符合范围：dispatcher 保留原公开命令并把新命令显式转发；collector 三文件的当前 SHA256 进入 `sourceDigest`，会阻断旧批次 retry；封存 case 的实际 SHA256 与 `declaredSha256` 分字段保存，来源只进 `protectedRefs`、不晋升 `baselineRefs`；replay 在读取 JSON/TRX 后才 finish。已提供的 20 个 Python、两次真实组件 mismatch、39 个相关 C#及8个 helper 结果未在本轮重跑。
