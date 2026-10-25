# Spec review — core versions

## 做了什么

仅审阅 `candidate.patch` 的精确 14 文件（两个 adapter、版本存储/读取、通用 delta、资源 v4 机械抽取、probe 及五个测试），对照 `core-versions-contract.md:2-4`、`control-plan-contract.md:7-11` 和 `state.md:A3/A4/A7/A9/A10`。没有审阅共享脏树的 HEAD diff，也没有运行构建或测试。

候选清单稳定：14 个当前文件 SHA-256 均等于 `candidate.json`；`candidate.patch` SHA-256 为 `f4d391b94e612b260e1b4202dd5eff4bd68cdbcab4eb5cd477d66ce088886380`。将本片 hunks 仅在临时副本反向应用后，三个旧测试恢复并逐字节匹配入口 RED：Control `3b7083…c83ed`、Plan `3fc527…99ac`、WriterMemory `d3393e…292ca`。

## 发现

**0 个 Spec finding。**

`VersionedCommittedHistory` 以单 gate、最多 1/2 行 batch、先写帧/基线再原子 head，head 确认后才发布内存状态，失败置 fault（`HistoryVersionStorage.cs:58-136`），符合“确认后发布、失败锁死”（契约:4；A3）。latest 校验 head 长度、末帧和 baseline 绑定，而 `ReadRecords` 重放整链（`HistoryVersionStorage.Read.cs:48-66,120-189`），符合“latest 有界但不取代严格恢复”（契约:3；A4）。v1 只读分流不改写，v2 独立 schema（两个 adapter:32-40），资源仅复用同形 JSON delta（`ResourceSnapshotDelta.cs:8-31`），符合 A9/资源 v4 限界。配置仅为构造 options，未引入无 buyer 的配置平台；成本 probe 将 head/baseline 纳入字节统计。

## 验证结果与下一步

82/82 是现有定向证据，不作为父验收结论；本次结论仅为静态 Spec 评审通过。保留父 A1–A10 及完整父验收的未完成状态。
