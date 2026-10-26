# Review-spec v1 裁定

仅只读复核候选与既有测试；未改源码或 v1。

## F1：撤回原表述；保留不同的执行窗口缺口

v1 所列“完成 Run 后新增未登记文件”在计划阶段不会退役：`_inventory` 于生成候选前调用 `_work_tree`（`tools/test_asset_task_cleanup.py:55-68`），后者发现 `present - entries.keys()` 即抛错（`tools/test_asset_archives.py:51-59`）；catch 将 Run 置 problem/pending（`test_asset_task_cleanup.py:79-81`），再从 eligible 移除（142）。既有 `test_unknown_changed_and_linked_run_material_is_preserved`（`tools/tests/test_task_asset_deletion.py:228-246`）覆盖此路径。因此撤回 F1。

不同且未由该测试覆盖的触发：计划复验/候选逐项 preflight 后，未遵守并行协议的一方在 Run 根新增普通文件。删除循环只复核每个候选的 identity/digest（`test_asset_task_cleanup.py:260-272`），标 retired 在目录清空前（273-298）；目录非空仅 continue，未重新 `_work_tree` 或写 pending。契约要求未遵守协议的并行漂移“明确未决”（`task-delete-contract.md:11`）。外层排他锁会挡住遵守 `task_access` 的操作，故这是非协作写入窗口，非原 F1。

## F3：缩窄

不主张另复制 record/summary：原 record 在不可变 index 中保留，`runBindings` 是其 digest（`test_asset_task_cleanup.py:241-243`）。但回执仅保存 `internal`，未保存当时的 `external`/`baselines` 投影（241-242、206-225）；`kept.references` 只覆盖阻塞项，不能构成完整图。故 F3 仅为审计完整性缺口，补 projection 字段即可。
