# Slice 3 mtime Increment Review

范围只限本次“归档/恢复不得改变 TRX 新鲜度依据”的增量；未扩及产品 Source、C#、删除 API、GC 或既有资产处置。

## 结论：有界通过

1. v2 包在 `tools/test_asset_archives.py:196-201` 以清单中的每个 Run 文件路径保存 `mtimeNs`，该字段随已摘要保护的 manifest 写入 ZIP；`_unpack` 在逐文件 SHA256 校验后于 `:103-119` 验证版本/键集合/整数范围并设置相同 mtime。因此解包消费者实际读取的是恢复的时间语义，而不是 ZIP 解压时间。归档结束前 `:214-216` 还拒绝归档期间源 mtime 漂移。

2. 最终发布链没有重新赋予“现在”的 mtime：临时解包文件已经带 manifest 时间，`_publish_file` 在 `:250-264` 将该 mtime 设置给临时文件，再用 hard link 发布，故只补缺和中断重试仍不会改写既有有效文件。新增测试 `tools/tests/test_asset_archives.py:151-163` 将 TRX 设旧、使 Source 更新，验证恢复后纳秒 mtime 完全相等且原 `freshness_violation` 仍返回 stale。

3. 历史 v1 可读且保守：`_unpack` 对 v1 合成全成员 epoch 0（`:103-111`），`restore_run` 明确返回 `mtimeRestoration: conservative-epoch-v1`（`:285-287`），不把缺失精确时间伪装为新鲜。用例 `:165-185` 覆盖该路径。

4. `archive-final-v2.txt` 记录 12/12，通过的真实 `mtime-restore-proof.json` 显示同一可丢弃 6 文件副本恢复后 SHA 与 mtime 均一致、原 freshness 结论 before/after 均 stale；94 个历史文件保留，且旧包仍可读。

未发现本次 mtime 保证的可行动偏差。
