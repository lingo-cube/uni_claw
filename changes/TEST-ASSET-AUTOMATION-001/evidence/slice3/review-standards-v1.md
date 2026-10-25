# Slice 3 Standards Review — bounded pass

做了什么：仅审阅 Run 归档/恢复与其测试，比较固定基线 `evidence/slice3/before/tools/test_assets.py`、`test-assets` 和新增归档模块；未审产品存储、C#、已关闭切片或历史清理。

原来的问题：归档发布失败曾会误删已注册的同名包；`collision-red.txt`记录了该真实碰撞。现在 `archive_run` 以 `xb` 创建、记录 inode，并仅在未发布且 inode 仍属本调用时清理新包（`tools/test_asset_archives.py:196-224`）；已注册包、来源和旧备份不删除。

这次修改：原绑定决定归档/恢复根（`77-80`,`255-259`）；ZIP成员、路径、大小、摘要、Run绑定及链接均在解包前后校验（`84-112`）。恢复只补缺失文件，遇不同内容/链接或并发创建即拒绝（`240-252`,`260-269`）。归档在索引锁内追加记录（`208-213`），不改历史Run摘要。原TRX/信号/packet消费者分别复读；消费者失效返回`bytesValid: true, consumerStatus: pending`，不与字节恢复混称成功（`226-237`,`270-274`）。`validate`的归档回退只读（`277-287`）。

验证结果：`archive-contracts-v4.txt`记录10项归档测试通过；`archive-and-operations-v3.txt`记录归档加布局操作32项通过。包含错包、ZIP slip/重复/链接、已有不同文件、外部引用失效、部分恢复重试与碰撞回归。未发现本片可复现、可行动的 Standards 违反。

新发现：无通用删源、任务删除或GC；已有94文件仍保留。A5后续受保护清理/任务级联删除仍为开放事项，不是本片缺失。
