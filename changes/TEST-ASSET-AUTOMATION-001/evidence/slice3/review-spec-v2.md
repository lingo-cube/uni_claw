# Slice 3 Spec Re-review

复核范围限于 v1 的两项 P1 修复及其新增回归，固定点仍为 `slice3/before` 的 `test_assets.py`；未检查或要求删源、GC、任务删除、存量迁移，也未触及既有 94 个文件。

## 结论：有界通过

1. `asset_path` / `path-ref` 已不再把“请求文件字节可取”当成消费者成功。`tools/test_assets.py:210-217` 对有归档的 Run 先补齐任一缺失成员（调用 `restore_run`，因此是完整 Run 恢复），再无条件执行 `validate_available`；即使工作目录本来完整，也会检查外部消费者引用。pending 时抛 `ValueError`，两个 CLI 调用面均由其既有异常分支以 2 退出，符合“不伪造来源已修复”。

2. `validate_run` 对归档 Run 无论工作目录完整与否均进入只读的 `validate_available`（`tools/test_assets.py:223-235`）。`tools/test_asset_archives.py:277-297` 对完整工作树读取其原消费者；不完整时仅临时解包后读取消费者，没有恢复或写入。既有工作树损坏仍在 `_work_tree` 处失败，不能被归档遮蔽；来源丢失则结果为 `bytesValid: true`、`consumerStatus: pending`、`valid: false`，`test-assets validate` 因 `valid` 为假返回 1。

3. `restore_run` 保持“可恢复字节”与“消费者来源”分离：仍仅补缺、不覆盖外部 source，来源异常返回 `consumerStatus: pending`；`tools/test-assets:37-41` 返回 1。新增 `tools/tests/test_asset_archives.py:163-179` 覆盖 report 改动后的 archive verify、restore、validate、asset_path 拒绝，以及 Owner 恢复原 report 后重新有效。`archive-contracts-v5.txt` 记录 10/10 通过。

本次复核未发现新的、可行动的 Spec 偏差。
