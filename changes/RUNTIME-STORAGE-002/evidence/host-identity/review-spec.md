# Host identity Spec review

范围仅为 `entry.json` 锚定的三处旧入口与 `candidate.patch`/`candidate.json` 所列四文件；未使用脏 HEAD 差异。候选哈希已与工作区一致：`13207181`、`9ef76326`、`50756230`、`508d10e2`。

## Findings

0。

两 Store 均先 `EnsureWritable`，再处理幂等重入；新 Create/Bind 均先 append、后更新双索引（`TaskInstanceStore.cs:44-52,67-77`；`ProductSessionStore.cs:42-50,65-75`）。这符合“确认前不发布成功”、关闭后连幂等写入也拒绝，以及失败后仅可重开核验来源的片级契约（`host-identity-contract.md:3`；`storage-contract.md:11`；`state.md:44,56-58`）。`Dispose` 与写入共用 `_sync`，且 `Get` 未被关闭屏蔽（两 Store `:57-59/:109-117`），符合既有查询边界。

新增四例分别覆盖两 Store 的关闭后 Create/Bind，断言异常、内存、原字节和重开 reader；原八例只改独占目录收尾，未改断言。未引入新格式、事务或越过 Store 的身份铸造；Runtime 仍是 TaskInstance/ProductSession 铸造者，重试复用既有 binding（`ADR-0045:14-17,24-27`）。本候选未声称解决 ADR-0046 的跨进程/P24 缺口（`:48-54`）。
