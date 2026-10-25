# 第四片：原文件写入器的重复历史缓存

已复用 remaining-core-inventory：Run 原生记录是转换事实，RuntimePlan 是不可变采纳事实，Control 保存原生操作链。没有独立最新查询 buyer 的记录族不为满足 API 形状另造接口；原完整恢复校验继续执行。本片推进内存成本，不声称完成父 A4。

实际 buyer 是 FileCommittedHistory 的 Control/RuntimePlan 两个公开 adapter，以及 FileRunStateJournal。领域 Owner 持有的业务状态保持；文件 writer 成功确认后仅需已提交序号、RecordingId、hash，不长期保留另一份完整序列化 payload。只读 reader 保留原严格验证和载入后的快照语义。

写入格式、hash/head、CreateNew、取消接受界线、批量 1–2 条、字节上限和 Dispose 等待提交均保持。序号只在 frame/flush/head 成功后推进；失败仍锁死 writer。显式 writer.ReadRecords 从原文件完整读取并核对自身已确认的 RecordingId/序号/hash，读取与追加互斥；不把物理文件提前提交或替换为本 writer 的成功确认。空 writer 仍返回空记录。返回记录是独立快照，可在后续追加后继续使用。

验收：公开 adapter 的 retained-memory RED→GREEN；Control/Plan/Run 读回后可继续原序号且旧返回值不变；原异步、失败锁死、批量、v1/v2和严格恢复及进程 Kill 定向回归。不引入新格式、通用存储接口、自动续跑/删除，不用内存测量声称磁盘空间或吞吐收益。测试缝沿父 state 已接受的公开文件 adapter，无新缝需要用户裁决。

Leader 拥有上述两个生产文件；Luna 仅拥有 CoreHistoryWriterMemoryTests.cs。对比基点是 evidence/slice4/entry 中本轮入口字节，而不是包含其他并行工作的整个 HEAD diff。验证使用当前共享源码，记录指纹与范围，不冒称隔离的全部产品验收。证据仅保留必要输入两文件、日志/结果摘要与评审，避免再次复制完整仓库。

回归扩展（实际188/190后）：StorageBarrierFileTests暴露资源v4前片遗漏。旧取消/正常关闭夹具缺必需初始Snapshot，仅补齐合法输入，原断言保持；资源payload超限guard在fault latch的try之外，首次失败后第二次仍试写，违反原失败锁定契约。Leader额外拥有FileRunResourceJournal.cs与该测试文件；将freeze/限额/反序列化纳入原同一失败屏障，预接受取消/序号冲突不变。入口原字节已追加保存，不修改slice3封存。本片不声称重新证明资源v4全部验收。
