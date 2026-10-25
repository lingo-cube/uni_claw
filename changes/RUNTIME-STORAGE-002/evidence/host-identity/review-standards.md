# Standards review — Host identity candidate

范围仅为 `candidate.json:5-8` 指定的四文件，相对 `entry.json` 的捕获基点 `a05176f3`；当前 SHA-256 与候选清单逐项一致。未运行构建或测试。

## 文档规则违例：0

`TaskInstanceStore.cs:44-52,67-77` 与 `ProductSessionStore.cs:42-50,65-75` 均先 `Append`，成功后才更新索引/返回；`Dispose` 与写入同持 `_sync`，关闭或故障后拒绝写入。这符合 `host-identity-contract.md:3-5` 的核心身份确认顺序、原 JSONL/API/同步边界，也符合 `storage-inventory.md:25-26` 的“写入异常阻止成功响应”。变更仍在 Host 的显式生产缝和测试面内，未造成 Harness 渗透（`AGENTS.md:24-32,80-82`）。

## 判断性 smell：0

两 Store 的对称 guard/append 形状不是本候选的可报告重复：契约明确不新增跨 Store 事务或通用抽象（`host-identity-contract.md:5`），强行抽取将超出 buyer。测试的四例也分别覆盖两个独立身份 Owner 的关闭后 Create/Bind 行为（`host-identity-contract.md:7`）。
