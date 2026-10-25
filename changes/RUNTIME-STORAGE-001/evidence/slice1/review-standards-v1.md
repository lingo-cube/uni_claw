## Standards review — RUNTIME-STORAGE-001 Slice 1

审查对象仅为 `candidate-inputs-v1.json` 固定的 22 个 dirty-entry 快照及 `candidate-v1.patch`，逐项 SHA256 一致；基点为 `a05176f3c66e0a3206f49dde463f51df1335975c`。未读取 HEAD 脏差异，未运行构建、测试或设备验证。范围仅 S1–S4 Evidence 观察批次。

### Findings

1. **[M] 每个观察批次都复制全部 canonical Evidence，历史增长会把本片的关键等待路径变成 O(历史记录数) 的分配和复制。** `AdmitBatchAsync` 在单 Owner 串行门内创建 `new Dictionary<string, EvidenceRecord>(_canonicalRecords, ...)`（`candidate-source-v1/src/UniClaw.Kernel/Evidence/EvidenceLedger.Batch.cs:22-32`），而 `_canonicalRecords` 是 Ledger 的持续 canonical store（`.../EvidenceLedger.cs:27,46-47`），本片没有上限或清理。批次数/字节数上限不能约束此复制；即使单条观察也会随运行历史增长而放大 GC 和 admission 等待，抵消 ADR-0055 要减少阻塞的目标。保留「同批先前记录可供 lineage 查询」即可：以只读原表加小型 batch overlay 提供查找/递归 lineage，成功确认后再发布，避免复制整个历史表。

### 通过项与结论

Owner/seam 符合架构：Ledger 保留 admission、canonical record 与发布权；Kernel 只保持 Evidence→World 顺序；Host 的 `FileEvidenceJournal` 是介质 adapter。`IAsyncEvidenceJournal → AdmitBatchAsync → ProcessBatchAsync → Driver/Host` 的 await 链与 ADR-0055 的“确认后发布”一致。未发现硬性架构/README/ADR-0055 Owner 违规；启发式 code smell 为判断，除上述增长性复制外为 0。

计数：M 1；硬违规 0；smell judgment 0；范围膨胀 0。
