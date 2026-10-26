# Task-delete Spec Review v1

范围：仅 candidate 的 11 文件；SHA256 全部一致。未运行测试/探针。

## (a) 规格缺失/部分

- **F1 未登记工作材料漏报却可退役。** 违背“未登记列保留/未知”（`task-delete-contract.md:3`；`state.md:62`）。触发：完成 Run 根新增未登记普通文件。`observed`（`test_asset_task_cleanup.py:36-38`）只在 archive 未知项处理（82-90）；仍删除候选并标 retired（265-274），目录非空也继续（291-298）。

- **F2 本任务 plan/WorkItem 引用被跳过。** 契约保护外部逻辑/物理/别名/目录引用，仅自身历史文件例外（`task-delete-contract.md:7`）。触发：`changes/C-001/plan.md` 以 `test-assets:C-001/R/...` 引用 Run。扫描称覆盖 Plans/WorkItems（`test_asset_task_references.py:198`），却跳过整个当前 Change（213）。

## (b) 范围扩张

无。

## (c) 实现有误

- **F3 回执缺原引用图和 record 摘要。** 契约要求二者支持部分失败重试（`task-delete-contract.md:9`）。receipt 只写 `runBindings`/`internal`（`test_asset_task_cleanup.py:241-242`），未存 `external`、`baselines`、record/summary（206-225）。触发：work 已删、archive 失败后重试，无法复核原保护依据。
