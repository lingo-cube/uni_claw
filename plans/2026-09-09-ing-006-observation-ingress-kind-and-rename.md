# Plan — ING-006 Observation Ingress Semantic Kind & Proposal Rename

> PlanType: ing-006-observation-ingress-kind-and-rename / Status: ADOPTED /
> References: changes/ING-006/state.md · 协议基线 P2/P3 · commit 221e9444

## 垂直切片：一次 ingress 语义贯穿三个 Authority

```text
观察生产者（capability / Effect Boundary）
        ↓  ObservationProposal（Kind + ObservationContext + Claim + Provenance）
Evidence Ledger · Admission（+kind-recognized fail-closed）
        ↓  EvidenceRecord（kind + context 随记录携带）
World Model · Relevance（kind-aware：AttemptReport 定义性非 world-relevant）
        ↓  relevant Observation → Reconciliation（路径不变）
Assurance · MaterialEffect 判定
        kind=Observation ∧ Context=PostActionEffectFlow ∧ accepted
        （替代 Producer.StartsWith("effect.boundary")）
```

四分职责（D3）：Kind=它是什么 · Context=为什么/在哪个流程 ·
ProducerIdentity=谁生产 · Authenticity=Deferred ⑦。

## Before / After

### Before

- `ObservationRecord(Claim, Provenance?)`——无 kind/context；与 baseline
  §11.2 canonical "Observation Record" 一词两义
- AttemptReport/自产观察靠 producer 前缀 + `attempt.*` subject 约定判别
- MaterialEffect 判定 = `Producer.StartsWith("effect.boundary")`
- relevance = 纯 subject-scope；EvidenceId 不含 kind 语义

### After

| 文件 | 变化 | Owner |
|---|---|---|
| `Evidence/ObservationRecord.cs` → rename | `ObservationProposal`；新增 `IngressKind { Observation, AttemptReport }` 与 `ObservationContext { External, PostActionEffectFlow }`（必填，非 nullable） | 词汇 |
| `Evidence/EvidenceRecord.cs` | + `Kind` + `ObservationContext`（语义字段随记录携带） | Evidence Ledger |
| `Evidence/EvidenceLedger.cs` | `Admit(ObservationProposal)`；增 `kind-recognized` 检查；`ComputeEvidenceId` 纳入 kind/context（拼法 realization） | Evidence Ledger |
| `Evidence/AdmissionRecord.cs` | 无结构变化（新检查走既有 AdmissionCheck 留痕） | Evidence Ledger |
| `World/WorldModel.cs` | `JudgeRelevance` 增 kind 分支：AttemptReport → `IsRelevant=false`（reason: `attempt-report-not-world-relevant`），subject-scope 判定保持 | World Model |
| `Assurance/RuntimeAssurance.cs` | `ProducerMatches` 删除；判定改 `Kind==Observation && Context==PostActionEffectFlow`（doc 注释同步 D3/D7 措辞） | Assurance |
| `Effects/EffectBoundary.cs` | `ExportAttemptEvidence` 产出 `ObservationProposal(Kind=AttemptReport, Context=PostActionEffectFlow)`；`attempt.*` subject 保留为描述性 | Effect Boundary |
| `UniKernel.cs` | `Process(ObservationProposal)` 签名 rename | Uni Kernel |
| `tests/…`（4 文件） | E2B 机械 rename（D1 语义冻结首次执行）；C2E/OUT/GEV Observation helper 增 kind/context 参数（post-action 观察处 `Context=PostActionEffectFlow`）；新增用例见映射表 | —— |

## 关键语义（固化，防漂移）

1. **kind 门先于 context 门**：AttemptReport 无论 context 如何永不满足
   MaterialEffect（⑤a）；context 门只对 Observation 生效。
2. **判定门是语义归类门，不是真实性门**：kind/context 均 producer
   自报，伪造不可检测（⑤b，Deferred ⑦）——验收与注释不得声称
   anti-spoofing。
3. **Evidence identity**：canonical semantic content（Kind /
   ObservationContext / relevant provenance）变化 → 不同 EvidenceId；
   同语义内容重放 → 幂等复用。
4. **relevance 双留痕不变**：kind 分支只是 relevance 判定的显式化，
   Admission 与 Relevance 仍是两个独立产出（E2B 验收 1 保持）。
5. **冻结升维（D1）**：E2B 验收 8 条断言不变；类型名替换不构成语义
   漂移。

## TDD 次序（RED → GREEN → REVIEW → VERIFY）

1. RED：目标类型/签名就位（stub），迁移用例 + 新增用例先行：
   - 新增 N1 `kind-recognized`：未知 kind → AdmissionRecord Rejected，
     零 canonical 副作用（验收 2）
   - 新增 N2 ⑤a：Kind=AttemptReport + Context=PostActionEffectFlow →
     MaterialEffect 不满足（验收 5a）
   - 新增 N3 context 门正例：capability producer + Context=
     PostActionEffectFlow 的 Observation → MaterialEffect 满足
     （验收 4，原误拒情形消除的正面证明）
   - 新增 N4 relevance kind 分支：AttemptReport（subject 在 scope 内！）
     → 仍 irrelevant（验收 3 的强形式）
2. GREEN：按 After 表最小实现；grep `ObservationRecord` 零残留
3. REVIEW：fresh SubAgent（轴：kind 门/context 门次序 / ⑤b 能力声称
   越界 / E2B 语义冻结核对 / relevance 双产出保持 / rename 完整性 /
   意外改动）
4. VERIFY：全量 `dotnet test`；`grep -rn "ObservationRecord" src tests`
   零命中；台账同步（P2/P3 + 冲突台账闭合 + CONTEXT 三词条）；
   evidence 落盘

## 验收 ↔ 用例映射

| 验收 | 用例 |
|---|---|
| 1 rename 零残留 | grep + 编译双证（VERIFY 步） |
| 2 kind 落地 + fail-closed | N1；既有 accepted 路径迁移后 GREEN |
| 3 AttemptReport kind 门 | C2E Accepted5 迁移（回流 admitted + irrelevant）；N4 |
| 4 MaterialEffect policy 迁移 | OUT 既有 MaterialEffect 用例迁移 GREEN；N3 |
| 5a kind≠context 越权 | N2 |
| 5b 能力声称边界 | REVIEW 轴检查（无对应运行时用例——不可测试性即本条内容） |
| 6 E2B 语义冻结 | E2B 8 用例断言不变 GREEN |
| 7 回归 | C2E 10 / OUT 18 / GEV 17 构造面适配后 GREEN |
| 8 台账同步 | VERIFY 步文档核对 |

## 迁移台账（草稿，VERIFY 时核对）

```text
rename：src 4 文件 + tests 4 文件（E2B×3 / C2E×1 / OUT×1 / GEV×1 处构造）
迁移：E2B Observation helper（类型名）；C2E/OUT/GEV helper（+kind/context 参数）
删除：ProducerMatches 前缀判定路径
新增：N1-N4 四用例
零改动：E2B 8 条断言本体、terminal 链、GEV 判定格
```
