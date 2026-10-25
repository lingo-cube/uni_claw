# Slice 1 SPEC review — candidate-v5 test-only delta

生产文件与 v4 相同，沿用 [v4 的生产结论](review-spec-v4.md)：0 项生产 SPEC 发现。本次仅审 `AuxiliaryObservationIsolationTests.cs` 的拥塞回归；并发 Act 语义不归本片。

## Findings

### P2 — 回归没有证明三条 P25 journal 条目都存在并已完成

测试要覆盖“三次正式咨询（两个 Defer、一个 Blocked）在 Capacity=1 拥塞下每条 P25 Completed/ordinal 不变”，但 `AuxiliaryObservationIsolationTests.cs:207-218` 仅断言 `JournalEntries()` 非空，并在 `242-246` 对该列表做 `Assert.All`/序列对照；`JournalEntries()` 在 `73-74` 用 `OfType<ConsultationJournalEntry>()` 静默滤掉查找失败的 key。随后 `ConsumedCount == 3`（`217`、`240-241`）不能证明该过滤后的列表也是三条，所以一个缺失或未取回的 P25 条目不会使“每条 Completed/ordinal”断言失败。

实际触发：若三次 ConsultAgent 调用均被计入 consumed count，但其中一个 `journal.Find(key)` 未返回条目，测试仍可得到 count=3，且只对剩余条目验证 Completed/ordinal。请在两臂显式断言 `Entries.Count == 3`（并最好比较每条 key/decision 顺序）后再作 `Assert.All` 与 ordinal 对照，才能证明该回归声称的三条正式结算均未受拥塞影响。
