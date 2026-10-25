# Slice 3 Standards Re-review — bounded pass

做了什么：仅复核 v1 后外部依赖 pending 的两项修复、读取入口和恢复证明；未运行构建，未扩大到产品、GC、任务删除或历史清理。

原来的问题：归档字节完整而 packet 外部来源已失效时，`path/path-ref` 与只读 `validate` 会把可读字节误报为成功。

这次修改：`asset_path` 对已归档 Run 先补齐任一缺失成员，再无条件调用 `validate_available`；消费者 pending 即抛错，故 `path`/`path-ref` 不再成功退出（`tools/test_assets.py:204-220`，`tools/test_asset_operations.py:175-182,331-349`）。`validate_run` 统一委托只读校验（`223-236`）；`validate_available` 在本地完整时复读消费者，在缺失时临时解包复读，异常返回 `valid:false, bytesValid:true, consumerStatus:pending`，不写工作树也不掩盖本地损坏（`tools/test_asset_archives.py:277-297`）。`restore_run` 仍只补缺，外部来源不覆盖，pending 保持 CLI 1（`255-274`）。

验证结果：`archive-contracts-v5.txt` 为10/10；`contracts-final.txt` 为归档加原操作/布局32/32；`verify-entry-final.txt` 为15/15。真实 `real-restore-proof.json` 证明新建6文件副本按清单核摘要删除后，validate 仍只读、path-ref 完整恢复，原 `evolution-signal.py` CLI 的 failureKey/source SHA 一致；既有94文件保留、净删源0。

新发现：未发现此增量的可行动 Standards 违反。没有新增GC或通用删源；受保护清理/任务级联删除继续留在后续范围。
