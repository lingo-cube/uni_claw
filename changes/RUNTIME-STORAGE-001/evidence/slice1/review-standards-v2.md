## Standards review — RUNTIME-STORAGE-001 Slice 1 v2

只审 `candidate-inputs-v2.json` 固定的 22 个 dirty-entry SHA、`candidate-source-v2` 与 `candidate-v2.patch`；22 项逐一复算一致，基点为 `a05176f3c66e0a3206f49dde463f51df1335975c`。未读取 HEAD 脏差异，也未运行任何验证。

### 关闭项

v1 的历史全表复制已关闭：批内只创建空 overlay（`.../EvidenceLedger.Batch.cs:24-35`），`TryFindRecord` 先查 overlay 再查受同一 serial guard 保护的 canonical 表（`.../EvidenceLedger.cs:186-187`）。同批 lineage 与已确认历史父记录仍分别由测试覆盖（`.../EvidenceBatchAdmissionTests.cs:53-70,97-108`），故每批不再随历史总量复制。等待时间项也闭合：无收集/入场队列，`Wait(0)` 在接受前拒绝重叠，已接受 I/O 仍 await 到提交边界（`.../FileEvidenceJournal.cs:89-132`）；这符合 ADR-0055 的上限要求，且未虚构共享队列语义。

### Findings

1. **[M] v2 固定测试证据与声明计数不一致。** `kernel-all-v2.log` 末行是“通过 1194、失败 3、总计 1197”，而本次 v2 候选新增 `LaterBatchCanResolveAParentFromAlreadyAcknowledgedHistory`（`.../EvidenceBatchAdmissionTests.cs:96-108`），任务说明称应为 1195/1198。现有日志没有候选源/程序集指纹，不能证明新测试在该 22-SHA 快照中执行。请在固定 v2 输入下重采集或补充可比指纹，并保留三项既有失败的 entry 对照；无需把全域迁移纳入本片。

Owner/seam、README 声明及 ADR-0055 的确认后发布边界均未见新违规。计数：M 1（证据对应）；硬违规 0；smell judgment 0。
