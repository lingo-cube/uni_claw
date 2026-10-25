# Slice 4 Spec 审查（v2：external alias 复核）

## 范围

本次仅复核最终 `candidate.patch` 中跨 Run 外部来源的符号链接别名修复；固定基点仍为 `slice4/entry.json` 与 `before/`。未执行真实 `release-work`、恢复、清理、构建或设备/模型服务。

## 结论：有界通过

此前的风险是：其他 Run 的 `provenance.json` 可将目标工作文件写为仓库外符号链接下的 `localPath`；仅做词法绝对化时，路径字符串不在目标 Run 下，可能错误放行释放。

候选在 `tools/test_asset_cleanup.py::_physical_reference` 将绝对或仓库相对来源以 `Path.resolve()` 规范到真实目标后，再判定其与目标 Run 根的包含关系。故 `external-alias/raw/case.trx` 指向目标 Run 时会成为 `source-dependency` blocker；`release_work` 在该判定以及符号链接解析环导致的 `RuntimeError` 下均写入 `pending` 并保持工作副本，符合 `test-asset-retention.md` §11 的“未知/漂移依赖保留、返回 1/pending”约束。

新增 `test_other_run_source_via_external_symlink_is_protected` 覆盖该触发：建立仓库外目录链接、由另一 Run 的 provenance 指向别名，断言完成状态为 false、无 deleted 且原文件仍在。未发现该处新的范围扩张或可复现规格错误；本结论不覆盖任务删除、GC 或存量迁移。
