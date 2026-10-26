# Task-delete Spec Review v3

范围仅 candidate-v3 11 文件；SHA256 均与清单一致。未运行测试/构建。

无新增 Spec finding。v3 仅在 `_portable_receipt` 递归投影诊断及引用字符串：先保留既有结构化 logicalRoot/binding 身份（`tools/test_asset_task_cleanup.py:183-202`），再把仓库根、binding registry 和旧 work/archive 根替换为逻辑标识（203-217）。缺失 legacy JSON 的 pending 也不再写入主机绝对路径；删除资格、引用保护、pending/retired 语义及原 Run/index/hash 均未改变，符合原契约的审计壳与不重写要求（`task-delete-contract.md:3,9-11`）。
