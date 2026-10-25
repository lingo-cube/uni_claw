# Slice 4 Spec 审查（v1）

## 范围

固定基点为 `slice4/entry.json` 与 `before/`；仅审阅 `candidate.patch`/`candidate.json` 所列 8 个文件，未使用宽泛 HEAD 差异，也未调用真实 `release-work`、恢复、清理、构建或设备/模型服务。

## 结论：有界通过

本片只实现显式授权的新 verification Run 的归档工作副本释放，未把任务级联删除、GC、存量迁移或旧 100 文件计为本片缺陷或成果。

原问题是归档后的工作副本没有一个受保护、可重试且不改写历史事实的释放出口。候选实现以 `workReleasePolicy` 仅在 `AssetRun(... purpose='verification')` 创建时登记；`release_work` 要求完成收尾、`sourceStable`、v2 解包及原消费者校验，并在每次 unlink 前复核引用、身份、字节和 mtime。归档/恢复/释放共享 Run 锁；回执逐项追加 `deleted`/`alreadyAbsent`/`pending`，不改原 summary、finalization 或文件摘要。

规格复核还覆盖了两项易错边界：逻辑引用仅在完整 token 边界时放行，`/unknown`、`#bad`、`?bad`、`@bad` 均会阻止释放；其他已登记 Run 的 JSON（含仓库外来源的 `provenance`）会从工作副本读取，缺失时仅解包归档读取、不恢复，并将指向目标工作根的依赖保留。对应定向契约测试位于 `tools/tests/test_test_asset_cleanup.py`。

未发现可复现的本片缺失、范围扩张或“看似实现但错误”的问题。后续如实现任务删除或全局 GC，须另行审查，不能复用本结论。
