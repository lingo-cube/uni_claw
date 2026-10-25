# Slice 4 Spec 审查（v3：既有仓库根 Run 兼容）

## 范围

本次只复核最终 `candidate.patch` 对无 `layout` 的既有手工 `assets.v1` Run 的引用读取兼容；未重审其他 slice4 逻辑，未执行真实释放、恢复、迁移或重哈希。

## 结论：有界通过

原风险是跨 Run 引用扫描假定每个 Run 都有 immutable layout binding；既有手工记录虽明确了仓库根，仍会在读取前失败，从而无法完成允许的仓库内 JSON 引用核对。

候选仅在 `source_record` 缺少 `layout` 时接受两种明确归属：`root: "repository"`（文件路径相对仓库根）及 `{ "kind": "repository", "path": ... }`（经 `checked` 限制的仓库子根）。读取仅针对记录的 JSON 文件，未写回、补 layout、迁移或复算旧文件摘要。文件缺失、未知根或非绑定 Run 需要归档读取时均抛出错误，由 `release_work` 转为 `pending` 并保留目标工作副本，保持 §11 的失败闭合。

`test_repository_local_manifests_do_not_require_layout_migration` 覆盖两种旧格式可完成无依赖释放，并验证旧 JSON 经仓库外符号链接指向目标 Run 后会阻止释放。该差异未扩大到任务删除、GC 或存量迁移；未发现可复现的规格缺失。
