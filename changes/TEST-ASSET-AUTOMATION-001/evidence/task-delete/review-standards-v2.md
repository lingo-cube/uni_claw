# Standards review v2

固定 11 项 SHA-256 一致，仅复审 v2 增量。

**硬违反 1：portable 回执遗漏。** `test_asset_task_cleanup.py:200-203` 只投影 kept/deleted 等，不处理 `pending`。缺失旧 repository Run JSON 时，`test_asset_task_references.py:192-202` 把绝对路径写入错误；它经 `_delete_locked` 落入 `taskDeletions.pending`。这违反《test-asset-retention.md》§12「主机绝对路径不作为跨机身份」。应在持久化前递归投影/剥离 pending 和引用图中的路径，并断言回执无绝对路径。

其余增量未见可操作异味；未运行测试。
