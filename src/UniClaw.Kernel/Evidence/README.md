# Evidence Ledger 代码索引（L2 · 基线 §11）

Sole **Evidence Admission Authority**：把 Provider 产生的 raw artifact /
typed output 规范化为可引用的 canonical Evidence Record。不判断
current-world truth、relevance、weight 或任何 Proof（权威表见基线 §10）。

## 代码归属

| 文件 | 职责 |
|---|---|
| `EvidenceLedger.cs` | admission 权威本体（P2 唯一 ingress；record supersession/invalidation） |
| `EvidenceRecord.cs` | canonical record 语义与不变量 |
| `AdmissionRecord.cs` | admission 留痕（integrity/provenance/scope/lineage 校验记录） |
| `ObservationProposal.cs` | 观察提案（producer → P2 的输入形状） |
| `Provenance.cs` | 来源、capture time、transformation lineage |
| `SharedSubjects.cs` | 共享 subject 层（PER-009 D7 / ADR-0027：跨 producer 共用 claim key） |

## 变更规则

进入本目录的改动必须仍是 admission 语义：record/id/provenance/引用与
supersession。判断语义真值、权重或充分性的逻辑不属此维度（分别归
World Model / Assurance）。感知 proposal 的**负载词汇**由感知维度拥有，
本目录只持有通用提案形状。

## 指向

- 基线 §11（canonical responsibility / L3）；四语义门：ADR-0004；
- 消费者视图 owner 派生投影：ADR-0011；并行多源观察：ADR-0027；
- 上游 producer：`../Perception/README.md`；下游：`../World/README.md`。
