# Slice 4 Standards Review v3

仅复核既有 `test-assets.v1` 仓库根 Run 的兼容路径。8 个候选文件 SHA256 与
更新后的 `candidate.json` 一致；未执行真实 release/restore。

## Result

有界通过。`tools/test_asset_cleanup.py:98-133` 仅在入站 Run 已有 `layout`
时使用 `bound_layout`；无 layout 的存量记录只接受两种显式仓库根：
`root: "repository"`，或 `{kind: "repository", path: ...}`。后者仍经
`checked` 约束在仓库内。该分支只读登记的 JSON 文件和其中的物理/别名
引用，不写旧清单、不补 layout、不重哈希，也不从当下配置猜测外部根。
不明根或登记 JSON 缺失都会抛错并由 release 收尾写为 pending；外部别名
仍经 `resolve()` 成为 blocker。

新增 `test_repository_local_manifests_do_not_require_layout_migration` 覆盖两种
真实兼容格式可继续释放，以及仓库根 JSON 经外部别名引用目标 Run 时拒绝。
这满足保留规范 §11 的归属/引用保护，同时没有扩大成存量迁移或通用清理。
未发现新的 Standards 违反或需单列的 Fowler smell。

验证：`python3 -m unittest tools/tests/test_test_asset_cleanup.py tools/tests/test_asset_archives.py tools/tests/test_asset_operations.py tools/tests/test_test_assets.py -v`，50/50 通过。
