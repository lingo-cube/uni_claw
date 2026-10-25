# Slice 4 Standards Review v2

仅复核 v1 P1 的修复及其引用保护回归。8 个候选文件 SHA256 均与更新后的
`candidate.json` 一致；基点仍为 `slice4/before/`，未执行真实
release/restore。

## Result

有界通过，v1 的 P1 已关闭。`tools/test_asset_cleanup.py:25-35` 现在对
`localPath` 以 `Path.resolve()` 取得实际路径，再与待释放 Run 根比较；外部
符号链接别名因此成为 blocker。解析环抛出的 `RuntimeError` 被
`release_work` 的 :206 捕获并如实写入 pending，符合
`docs/agents/test-asset-retention.md` §11 的“链接、未知或漂移依赖保留”规则，
不会把扫描异常改写为可删除。

新增 `test_other_run_source_via_external_symlink_is_protected` 覆盖原 RED：
另一 Run 的 provenance `localPath` 经仓库外链接指向目标工作副本时，释放
不完成且无 deleted。既有有效逻辑引用、直接跨 Run provenance、归档缺工作
目录时只解包不恢复、仓库内链接、硬链及 FIFO 拒绝仍由同组测试覆盖；未见
Fowler judgement smell 需要单独处置。

验证：`python3 -m unittest tools/tests/test_test_asset_cleanup.py tools/tests/test_asset_archives.py tools/tests/test_asset_operations.py tools/tests/test_test_assets.py -v`，49/49 通过。
