## Spec review — RUNTIME-STORAGE-001 Slice 1 v2

审查对象为 `candidate-inputs-v2.json` 固定的 22 个 dirty-entry SHA；逐项复算一致。仅审 Evidence S1–S4 与 ADR-0055，未运行构建、测试或设备验证。

v1 的 M 已关闭：此片明确单 Owner、无收集/准入队列，原先 ADR-0055 的等待时间上限在该形态下被具体化为两个零窗口。`FileEvidenceJournal.AppendBatchAsync` 在接受前先 `Wait(0)`，重叠写入立即拒绝（`src/UniClaw.Host/Runtime/FileEvidenceJournal.cs:89-97`）；一旦接受，仍等待 Write/Flush/head 的真实提交边界（`:104-127`），不会把取消或超时误报为未写。`DisposeAsync` 继续等待写门再释放流（`:179-183`），符合 ADR 对已接受记录关闭时等待/明确未确认项的要求（`docs/adr/0055-async-batch-storage-requires-confirmed-commit.md:9`）。

这与当前片 S3 的“第二个异步 Drive 被串行门拒绝”和“全域背压/关闭后续迁移”一致（`changes/RUNTIME-STORAGE-001/state.md:26`）；不应在此片要求通用共享队列或 timeout。v2 的 batch-only overlay 仍经 canonical fallback 查找历史 parent（`src/UniClaw.Kernel/Evidence/EvidenceLedger.cs:186-187,230`），不改变跨批 lineage 语义。

### 结论

可行动 Spec finding：0；缺失/部分：0，范围膨胀：0，明显错误：0。Kernel-final/Host-final 正在或已由现有证据轨道处理，本次不重跑并不将其当作新验证声明。
