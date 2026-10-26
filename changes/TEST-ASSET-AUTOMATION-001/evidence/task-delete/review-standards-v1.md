# Standards review v1

固定审查 `candidate.json` 的 11 个文件；SHA-256 全部一致，未审其余 dirty HEAD。

**硬违反：0；判断型异味：0。**

`task_access → assets.lock → Run lock` 顺序成立；退役阻断读/archive/restore/release/新建，未标记活动 Run 仍可 finish。删除前复核身份与 SHA；链接、硬链接、漂移、未知及外部/基线/同任务消费者均保留或 pending。回执 portable 化且未改原 runs/archives/hash。引用投影、退役编排与既有调用面职责未越界。

只读既有 91 项回归与 actual-cli-v2 通过证据；未运行测试或探针。
