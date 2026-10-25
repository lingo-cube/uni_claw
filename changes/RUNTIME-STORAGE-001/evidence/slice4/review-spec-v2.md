# Slice 4 Spec review v2（v1→v2 增量）

固定输入已核：base `a05176f3c66e0a3206f49dde463f51df1335975c`；`candidate-v2.patch` SHA256 `c73ed0…e7a03`、v1→v2 patch `77b71b…0c576`、inputs `709b1d…04d47`。patch、`candidate-source-v2` 与当前工作区逐一匹配同 20 路径 SHA；未纳入并发 DSH 文件或其他脏改动。

**M1/D3 — 0 项 Missing/partial/wrong。** 更新 spec 要求（`control-plan-async-group.md:7,9,14`）的唯一合法 buyer 是 configure-rules→start。`ConfigureAndStartAsync`只在 `CanAppendBatch` 为真时构造两条原行并共同确认，否则逐条（`TraversalControl.History.cs:101-116`）；确认前仅 candidate，失败不发布（L142-176）。Driver 改为该单入口，取消在后续选择前停下（`KernelRunDriver.cs:1587-1595,2272-2275`）。

**文件/Owner 边界 — 0 项 scope creep。** `FileCommittedHistory`限制 1–2 条、连续序号和整批编码 16MiB，单次 WriteAsync/FlushAsync/末行 head，且仍无队列或跨 Owner 合批（`FileCommittedHistory.cs:89-127`）。Plan 没有 `CanAppendBatch`，原三阶段未变。计数明确是 managed 调用；同 Driver 测试保留 10 原行并断言 10→9（`ControlPlanAsyncStorageTests.cs:68-105`），与 measurement 的 Plan 2→2 一致。

结论：M1 增量符合更新的 D3 与 Group A 的独立 Owner 约束；本次为静态 Spec 复核，未运行构建或测试。v1 报告未修改。
