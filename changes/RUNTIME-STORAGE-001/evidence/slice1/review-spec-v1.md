## Spec review — RUNTIME-STORAGE-001 Slice 1

审查对象为 `candidate-inputs-v1.json` 固定的 22 个 dirty-entry SHA；逐项复算一致。只审当前 Evidence S1–S4 与 ADR-0055，未以 HEAD 或全域 A1–A4 为准；未运行构建、测试或设备验证。

### Findings

1. **[M] 缺少批次等待时间上限。** ADR-0055 要求批量同时具有“数量、字节和等待时间上限”（`docs/adr/0055-async-batch-storage-requires-confirmed-commit.md:9`）。候选只在 `FileEvidenceJournal` 构造器定义 `maxBatchRecords`、`maxBatchBytes`（`src/UniClaw.Host/Runtime/FileEvidenceJournal.cs:26-30`），并在编码循环按二者切分（`:108-115`）；`AppendBatchAsync` 对写门的等待也没有 timeout/deadline（`:89-128`）。S2虽列明数量/字节，仍需按 ADR 明确本片的等待时间语义及上限（例如即时提交的零收集窗口与写门等待期限），并加入相应可观测/契约验证，避免受阻批次无限等待。

### 结论

可行动 Spec finding：1（M）；缺失/部分：1，范围膨胀：0，其他明显错误：0。已保存证据所述 Ledger/Driver 8、Host 288（含最终 6 个文件例）、DSH 24 通过，以及 Kill 后双 reader/源 SHA 和 managed FileStream 64→1 计数，均不足以覆盖该时间上限。
