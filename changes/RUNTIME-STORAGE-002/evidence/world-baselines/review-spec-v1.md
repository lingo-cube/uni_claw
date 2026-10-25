# World 基线 Spec 审查（v1）

**结论：有界通过。** 审阅固定基点 `entry.json` + `before/` 与 13 文件 `candidate.patch/json`；既有 7 文件的 before/current SHA 和候选 SHA 对齐，新增 6 文件均在候选清单内。未比较工作树 HEAD，未编译或运行。

`FileWorldJournal` 在同一 `CommitAsync` 编码原 operation、写 frame、flush、确认 head 的链内生成 v3 基线；计数在 `WorldRevisionDeltaWriter` 每个 revision 后递增，初始 revision 及每个 interval 边界为完整 values 基线（`FileWorldJournal.cs`、`WorldRevisionDeltaCodec.cs`）。reader 对 v3 每包固定 interval、base、基线位置和独立 values 完整性拒绝；`WorldHistoryVersionReader` 仍复用完整 frame/hash/head 校验，按 revision（非 operation）索引，只从目标基线加 tail 解码，并固定 `FullWorldHistoryValidated=false`、`ContinuationSupported=false`，不构造 live World 或授权执行。

原 reader 继续全量解码 v3，再由 `WorldHistoryRead.ReadOriginal` 保持既有领域认证；v1/v2 只读、无迁移/重哈希路径。缺失/依赖基线、interval 改变、revision 缺口/重复、早期物理损坏、未确认 head 皆有拒绝覆盖；早期逻辑坏 delta 仍由原完整 reader 认证发现，数据查询未冒充该认证。`WorldAsyncStorageTests` 仅 schema v2→v3 一行，符合直接授权。

成本记录披露了同 ID 128 revision 样本：v3 1,236,533 bytes、v2 1,059,133 bytes（+16.7%），latest 解码 32 vs 128，但仍验证 258 物理帧；并明确未测物理 I/O/峰值内存、无稳定速度保证。既有证据报告相关公开用例 154 通过、旧 v1/v2 消费者和 SHA 未变；本审查未重跑。
