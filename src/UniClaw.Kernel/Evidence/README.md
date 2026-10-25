# Evidence Ledger 代码索引（Product T2 · 基线 §11）

Sole **Evidence Admission Authority**：把 Provider 产生的 raw artifact /
typed output 规范化为可引用的 canonical Evidence Record。不判断
current-world truth、relevance、weight 或任何 Proof（权威表见基线 §10）。

## 代码归属

| 文件 | 职责 |
|---|---|
| `EvidenceLedger.cs` | admission 权威本体（P2 唯一 ingress；record supersession/invalidation） |
| `EvidenceLedger.History.cs` | 原入证日志、只读恢复及写入抽象 |
| `EvidenceLedger.Batch.cs` | 一次观察的有序候选、异步批量确认及串行发布 |
| `EvidenceRecord.cs` | canonical record 语义与不变量 |
| `AdmissionRecord.cs` | admission 留痕（integrity/provenance/scope/lineage 校验记录） |
| `ObservationProposal.cs` | 观察提案（producer → P2 的输入形状；Origin 自报仅诊断） |
| `ObservationOrigin.cs` | 来源性质封闭二值（External \| PostActionSelf，ADR-0048；canonical 值由账本核验原实际投递关联） |
| `Provenance.cs` | 来源、capture time、transformation lineage |
| `SharedSubjects.cs` | 共享 subject 层（PER-009 D7 / ADR-0027：跨 producer 共用 claim key） |

RUNTIME-STORAGE-001首片：`IAsyncEvidenceJournal`确认同一观察内的有序原记录。
`AdmitBatchAsync`在原Owner内准备候选，允许后续derived解析同批已准备的parent；
写入确认前不发布admission或canonical record，失败后锁存而不重试。入队或Task
创建不是提交。原`Admit`与legacy journal保留同步兼容，未宣称所有调用已异步。
公开入证串行门阻止同一Owner在await期间被重入；独立只读恢复仍只解释原记录。

## 变更规则

进入本目录的改动必须仍是 admission 语义：record/id/provenance/引用与
supersession。判断语义真值、权重或充分性的逻辑不属此维度（分别归
World Model / Assurance）。感知 proposal 的**负载词汇**由感知维度拥有，
本目录只持有通用提案形状。

## 指向

- 基线 §11（canonical responsibility / L3）；四语义门：ADR-0004；
- 消费者视图 owner 派生投影：ADR-0011；并行多源观察：ADR-0027；
- 上游 producer：`../Perception/README.md`；下游：`../World/README.md`。

`IEvidenceJournal`仅存原Owner的typed admission历史；`EvidenceLedger(journal)`每次accepted/duplicate/rejected入证先同步提交，成功后才发布canonical record与admission log。失败锁存，不再接受新入证；Kernel/driver关闭后续咨询、Intent/Effect与terminal评价，已发生Receipt仍保留。无journal的既有构造器继续保持原内存语义。

`RestoreReadOnly`从完整原源校验原EvidenceId、admission形状、Origin闭合值域以及已存在parents/leaf closure，冻结原字段与集合，不调用Admit、不更换capture time、不推入World/Reconcile。Origin原样保留，不由Context重新推导或认证；恢复Ledger只读，不能作为Kernel执行现场。HistoryPosition仅标明原录制与入证末位，不证明当前World。

ORIGIN-CORRELATION-001替换早期Context推导：新Admit只认原Effect Boundary issuer、同Kind、仍有效的不可自行构造link。无link为External；错issuer/kind/过期link拒绝入证。Origin自报、producer、Context、时间相邻和lineage字符串都不能补造关联。canonical保存稳定AttemptReference（owner/Receipt/Intent/Binding及可选可靠AttemptId），不保存可重新启用的live token。

新admission/identity版本2，11项checks，v2身份域含原v1内容摘要、实际Origin及结构化关联字段；不同实际attempt不复用id。内部ComputeEvidenceId预测当前身份，ComputeLegacyEvidenceId仅供旧v1历史校验。RestoreReadOnly分别验证v1/v2原身份，v1原Origin冻结而不认证关联；v2校验关联形状及身份。两者都不重新注册issuer、不创建live link、不执行入证或迁移旧文件。历史Origin不取得current或MaterialEffect authority。

当前实际模型仍没有supersession/invalidation引用与写入接口；本片保存的是已有admission、canonical facts与derived lineage，不声称已恢复失效/替代关系或补全Evidence lifecycle。该基线缺口保留后续处理。005两方共同点不自动代表Evidence；007另以原资源提交的精确Evidence位置核对三份原历史。

`AttemptObservationLink.cs`承载原投递关联词汇；Ledger不判断真实性、因果效果或Proof。同步adapter是否确实把本次采集结果回填，是明确的信任缝；opaque link将它收窄到原实际Receipt及本次请求，不能替代观察内容真实性。

RUNTIME-RECOVERY-007只消费Ledger已提交的HistoryPosition，不改入证/Origin/身份/血缘策略。其Sequence是全部admission次数，duplicate/rejected也推进；canonical record数量或Run.Progress不能替代。原readonly Ledger与原资源位置相等只证明该历史边界，不授予current、truth、Proof或执行权威。
