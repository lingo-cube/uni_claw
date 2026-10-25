# Slice 3 Standards Re-review — mtime bounded pass

做了什么：仅复核归档 v2 的 mtime 保真、v1 兼容和 freshness 边界；未运行构建或测试，未新增硬化、GC、删源或历史处置审查。

原来的问题：`scenario-coverage.freshness_violation` 以文件 mtime 判断陈旧 TRX；原 restore 的新写入时间会把陈旧输入伪装成新鲜。`mtime-red.txt` 记录了真实 RED。

这次修改：归档在写包前采集每个登记成员的 `st_mtime_ns` 并写入 v2 header（`tools/test_asset_archives.py:196-201`）；解包严格要求键集合完整且值为有界整数，再在临时解包时复设时间（`103-119`）。发布恢复文件前从临时源继承 mtime（`250-264`）。归档后的工作树 mtime 与采集值不一致即拒绝发布（`214-216`）。v1 仍可读但以 Epoch 0 恢复，并显式返回 `conservative-epoch-v1`（`104-111`,`285-287`）。

验证结果：`archive-final-v2.txt` 记录12/12通过，覆盖陈旧 TRX 恢复后仍拒绝及 v1 降级。真实同一自有6文件副本的 `mtime-restore-proof.json` 记录 SHA/mtime 全同、覆盖率 before/after 都为 stale；94个历史文件未动，净删源0。实现仍只删除本次失败且 inode 属于本调用的新包（`176-181`,`226-233`），没有 source deletion 接口。

新发现：未发现本必要兼容增量的可行动 Standards 违反；v1 的 Epoch 0 是明确保守语义，不把未知时间伪装为新鲜。
