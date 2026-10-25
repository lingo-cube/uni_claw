# Standards 复核（slice1 最终）

## 做了什么

仅复核上一轮 P1 的 binding 发布修复，以及本轮相关的分类冲突、同名 `summary.json` 登记和 `path` 读取校验；未扩展到归档、清理或其他入口。

## 原来的问题

旧 `open('x')` 直接写目标文件时，另一 Change 能在目标未写完时读取 JSON。`binding-race-red.txt` 已记录该确定性失败。

## 这次修改

`Layout.bind()` 现在先向唯一 `.pending` 文件写入并 fsync，再以 `os.link` 原子创建最终名字；竞争者只会读到完整已发布文件。每个 Change 的 `assets.lock` 继续负责其自身索引更新，二者职责未混淆。新增跨 Change 延迟首写测试，且 metadata 排除改为精确路径，不再漏掉 raw 分类里的 `summary.json`；`test-assets path` 也在返回前核对登记的大小和 SHA256。

## 验证结果

`layout-contracts-green.txt` 显示 9/9 通过；其中并发测试让首个写者停在 `json.dump` 前，第二个 Change 仍成功完成登记并可按 binding 定位 Run，覆盖上一轮复现路径。代码审阅确认 `FileExistsError` 分支仅发生在 `os.link` 已观察到目标存在之后，目标由成功 link 的完整临时文件提供。

## 最终可行动结论

本轮修复后未发现此固定范围内仍可行动的 Standards 缺陷；上一轮 P1 已解除。未把这一结论外推为归档/清理或其他入口已完成。
