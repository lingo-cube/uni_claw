# Standards 审查（slice1）

## 做了什么

以 `evidence/slice1/before` 的四个入口文件为固定比较点，审阅本片新增的 `test_assets.py`、`test-assets`、两组测试及 `verify-change` 首次登记改动；未以 HEAD 宽 diff 归属，也未审归档、清理或其他入口。

## 发现

### P1：本地 immutable binding 的跨 Change 并发首次登记会读到半写 JSON

[`tools/test_assets.py:115`](/Users/fran/Documents/Code/spacex/uni_claw/tools/test_assets.py:115) 用 `open('x')` 创建 binding 后直接 `json.dump`；另一进程若为**不同 Change**（因此持有不同的 `changes/<id>/assets.lock`）但使用相同 `repositoryKey/configDigest`，会在写者关闭文件前于 [`121-123`](/Users/fran/Documents/Code/spacex/uni_claw/tools/test_assets.py:121) 捕获 `FileExistsError` 并立刻 `json.loads(path.read_text())`。此时可读取空/截断 JSON，抛出 `JSONDecodeError`，使第二个真实 Run 在创建或登记前失败。

复现：两个进程分别对不同 Change 调用 `AssetRun`，共享全新 profile 与同一 layout；在第一个 `open('x')` 后、关闭前暂停它，再让第二个进入 `bind()`。现有 `assets.lock` 仅保护单个 Change 的索引，不能覆盖该 profile 级文件。

影响：首个并发 verify-change 不具备声明的原子登记能力，失败不是可见的配置冲突，也没有重试/等待语义。用同目录锁或临时文件写完并原子发布 binding（读者仅接受已发布文件）修复，并增加跨 Change 并发回归测试。

## 其余

未发现仓库已文档化的其他硬性规范违反；给定的 smell baseline 未见足以单独构成可行动缺陷的新增项。
