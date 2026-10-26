# Task-delete Spec Review v2

范围仅 candidate-v2 的 11 文件；当前 SHA256 与清单全部一致。未运行测试/构建。

未发现新增 Spec finding。

- 当前 Change 的 `plan.md`/指定 evidence 的 plan、handoff、progress、WorkItem 不再整体跳过（`tools/test_asset_task_references.py:219-223`），其逻辑/物理引用进入 external（231-238）；state、assets 审计与历史 evidence 仍排除，符合“自身历史文件非消费者”（契约 7）。
- repo-owned legacy 文档的仓库相对、`./` 与相对当前文档路径均生成匹配别名（33-45），覆盖跨 Change 相对引用。
- unlink 后再次 `_work_tree`（`tools/test_asset_task_cleanup.py:276-279`）；新未知文件抛错进入 pending/kept（304-314），且在标 retired 前发生。
- receipt 保存 `internal`、`external`、`baselines`，并以 `runBindings` 的 SHA-256 绑定保留 index 原 record（241-243）；未复制 summary 不构成契约缺口。
