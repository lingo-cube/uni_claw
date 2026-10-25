# Slice 6 Standards review v3（增量）

## 做了什么

仅审 `candidate-v2-v3.patch`：唯一候选改动是 `StorageClosureFileTests.cs` 的公开 `HostRunner.RunOnce` bridge 回归。`candidate-inputs-v3.json` SHA-256 为 `dc2ea0135dda5c08d34bc7f529d3e4601f27d6cc442d2381243d1e20e5b07e23`；23/23 候选源 SHA 匹配，产品源码哈希未变。

## 原来的问题

v2 已证明 Settings 同步 bridge 不依赖调用方消息循环；尚缺同一条件下，普通 Host 在初始化失败后确实等待 failure artifact 与资源关闭的公开回归。

## 这次修改

新增测试在暂停 `SynchronizationContext` 的线程调用现有 `HostRunner.RunOnce`，走缺失 `DeviceId` 的既有 `ENVIRONMENT_UNAVAILABLE` 路径。测试在返回后读取真实 `failure.json`、只读 Evidence/World journal，断言 World initialize、Evidence 空、咨询为零。

## 验证结果

**硬问题：0。** 没有生产变更、新 Owner、存储实现、Task.Run 或额外生命周期。该测试未改变 ADR-0055 第7、9、11行的提交/结算/关闭规则，且符合 AGENTS.md §1.5 的可替换 seam 边界。`candidate-v2-v3.patch:169-202` 明确在 delivery/感知前停止，并在 bridge 已返回后读取失败产物和旧格式；`host-once-bridge.trx` 目标 1/1 通过。v1/v2 的硬违规 0 结论不变；Kernel/DSH 的 v2 同源复用不构成此源码增量。

**Fowler smells（判断性建议）：0。** `PausedContext` 已有夹具复用；新用例只覆盖第二个真实公开 buyer，不引入生产泛化、Middle Man、Data Clump 或重复业务形状。只读构造器用于验证既有格式，非同步写入回归。

## 新发现

无 Standards 阻塞；本评审仅读取既有 1/1 证据，未执行测试。

## 下一步

由 Host 最终回归绑定新增用例后继续既定封存；不扩展到 P24、历史读取改造、队列/DB/续跑或路径优化。

**计数：硬问题 0；smells 0；总计 0。**
