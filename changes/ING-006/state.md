# ING-006 — Observation Ingress Semantic Kind & Proposal Rename

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 221e9444

## Intent

**WHAT**: Observation ingress 落地显式 semantic kind（Observation ｜
AttemptReport）与 ObservationContext（External ｜ PostActionEffectFlow）；
pre-admission 输入候选 rename `ObservationRecord` → `ObservationProposal`
（消 baseline §11.2 一词两义）；MaterialEffect fulfillment policy 从
producer-prefix 判断迁移为 accepted Observation + explicit
PostActionEffectFlow context。

**WHY**: 协议基线第二梯队第 1 项（用户裁决优先序）：同时消除两个真实
问题——"Observation Record 一词两义"（模型挑战 major-1，协议 P2 冲突
台账）与 "kind 只在协议没落代码"（P2/P3 known gap）。四分职责（Kind /
Context / ProducerIdentity / Authenticity）使语义判别不再依赖字符串
前缀巧合。

## Scope

- `ObservationRecord` → `ObservationProposal`（全仓 rename，含 E2B 冻结面
  显式解冻）
- proposal 与 EvidenceRecord 携带 kind + ObservationContext
- admission 增 `kind-recognized` 结构性检查（未知 kind fail-closed）
- World Model relevance kind-aware（AttemptReport 定义性非 world-relevant）
- MaterialEffect 判定迁移：kind=Observation ∧ Context=PostActionEffectFlow
  ∧ accepted evidence（删除 `Producer.StartsWith("effect.boundary")` 路径）
- `ExportAttemptEvidence` 产出 Kind=AttemptReport + Context=PostActionEffectFlow
- Evidence identity 语义收紧（canonical semantic content 变化不得复用同 id）
- 测试迁移（E2B 机械 rename / C2E / OUT / GEV 构造面适配）+ 新增用例

## Out of Scope（红线）

- freshness（P4）、P5/P11 曝射面、DispatchResult/EffectReceipt outcome 词汇
- Memory、F2、legacy
- anti-spoofing / producer 身份真实性核验（明确保持 Deferred ⑦——本
  change 不得声称或暗示拥有该能力）
- 协议语义修订（只做台账状态同步）

## Decisions

| # | 决策 | 来源 |
|---|---|---|
| D1 | E2B 冻结承诺升维：byte-level 零漂移 → **验收 8 条语义零漂移**（断言不变，类型名机械替换）；rename 属本 change 显式授权；后续 change 冻结基线 = 语义冻结 | 立项 Q1 |
| D2 | kind 必填落 proposal + EvidenceRecord；admission `kind-recognized` fail-closed（实现层只认已锁二成员，新 kind 需协议+代码双改） | 立项 Q2 |
| D3 | 四分职责：**Kind**（这是什么证据）/ **ObservationContext**（External ｜ PostActionEffectFlow，在什么观察流程中产生）/ **ProducerIdentity**（provenance，谁实际生产）/ **Authenticity**（Deferred ⑦）。不引入 "Origin" 一词——不让一个词把 context/producer/authenticity 重新揉在一起。命名取 `ObservationContext`（`AcquisitionContext` 备选被拒：与协议 "post-action effect flow 观察 acquisition" 措辞重叠度低者胜出属 realization 选择） | 立项 Q3 修正 |
| D4 | relevance kind-aware：AttemptReport 定义性非 world-relevant（kind 级短路，不再依赖 attempt.* subject 碰巧在 scope 外） | 立项 Q4 |
| D5 | Evidence identity 语义：**canonical semantic content 变化（Kind / ObservationContext / relevant provenance）不得复用同一 EvidenceId**；hash 字段拼法属 realization。`attempt.*` subject 保留但降级为描述性 subject/provenance，不再承担 kind 或 relevance 判定职责 | 立项 Q5 修正 |
| D6 | 验收 ⑤ 拆分：⑤a Kind=AttemptReport 即使 Context=PostActionEffectFlow 也不得满足 MaterialEffect（kind 门，可验证）；⑤b 伪造 Kind/Context 的真实性核验**不属本 change**，保持 Deferred ⑦ | 立项 Q6 修正 |
| D7 | MaterialEffect fulfillment policy 迁移措辞：accepted Observation + explicit PostActionEffectFlow context——不使用"自产 origin"（producer 与 context 已拆开） | 立项 Q6 修正 |
| D8 | Route=Direct（同一 ingress 语义切片：kind/context/判定/rename 高内聚，拆散易把 kind 门与判定迁移割裂）；depth=decision-heavy；TDD RED 先行 | 与 CBA-005 D8 同构 |

## Acceptance（8 条）

1. **Rename 全面落地**：全仓无 `ObservationRecord` 残留（编译 + grep
   双证）；`ObservationProposal` 为唯一 proposal 名
2. **kind 落地**：proposal 与 EvidenceRecord 均携带 kind +
   ObservationContext；未知 kind admission 拒绝（`kind-recognized`
   fail-closed，零 canonical 副作用）
3. **AttemptReport kind 门**：AttemptReport 判别走显式 kind
   （producer 前缀判别路径删除）；回流 Attempt Evidence 语义不变
   （admitted、非 world-relevant、零 effect-claim）
4. **MaterialEffect fulfillment policy 迁移**：accepted Observation +
   Context=PostActionEffectFlow（kind 门 ∧ context 门 ∧ accepted
   evidence）；capability 生产、PostActionEffectFlow context 的观察可满足
   （原 producer-prefix 误拒情形消除）
5. **⑤a**：Kind=AttemptReport，即使 Context=PostActionEffectFlow →
   不得满足 MaterialEffect（独立用例）；**⑤b**：伪造 Kind/Context 的
   真实性核验不属本 change，验收与文档不得声称 anti-spoofing 能力
   （Deferred ⑦ 保持，用例不测试伪造不可识别性）
6. **E2B 验收 8 条语义零漂移**：断言不变，仅构造类型名机械替换
   （D1 冻结升维的首次执行）
7. **C2E/OUT/GEV 回归 GREEN**：构造面适配（helper 增 kind/context
   参数），行为断言不变
8. **台账同步**：P2/P3 known gap 消除标注、Observation Record 冲突
   台账闭合、CONTEXT 词条同步（新增 **Observation Context**；
   Obligation Fulfillment policy 措辞更新为 context 表达；Observation
   词条载体注记）

## Constraints

- 协议基线 P2/P3/D3-D7 为直接权威；Target v0.1 不变量 8-20、33/34
  不可违反
- 已定型语义零改动：admission 检查族、relevance 两产出、Reconciliation、
  Attempt≠Effect、terminal 语义
- 测试验证行为，不验证实现细节

## Verification

```yaml
level: DETERMINISTIC
method: >
  RED（目标签名桩 + 迁移/新增用例先行）→ GREEN（最小实现：rename /
  kind+context 字段 / kind-recognized / kind-aware relevance / 判定迁移 /
  Export 适配）→ REVIEW（fresh SubAgent）→ VERIFY（验收 8 条逐条 +
  全量 dotnet test + grep 残留检查 + 台账同步核对）
expected: >
  验收 8 条全 GREEN；全量 ≥56 GREEN（迁移不删除用例，新增若干）；
  grep -r ObservationRecord 零命中（排除 docs 历史台账引用）
actual: >
  2026-09-09 dotnet test（UniClaw.Kernel.slnx；SDK 10.0.400；net10.0）：
  RED 失败 5 / 通过 57（机械迁移批零回归）→ GREEN 失败 0 / 通过 62 →
  Review 修复（F2 context 对称校验 + N7）后 失败 0 / 通过 63（Kernel 46
  = 既有 39 + N1-N7；Agent 17 零改动）。grep ObservationRecord 残留 = 1
  处有意历史注记；E2B/C2E 断言改动数 0；红线零触碰（Review A2 轴核证
  无 anti-spoofing 能力声称）。验收 1-8 逐条证明映射见 evidence
evidence: evidence/2026-09-09-ing-006-deterministic.md
```

## Assumptions

- 单线程内存模型；EvidenceId hash 拼法变更导致既有测试内 id 值无跨
  change 稳定性需求（id 均为测试内比较，无硬编码 id 断言——ENTRY 需
  复核此点）
- GEV/C2E/OUT 构造面适配均为机械参数补充，无行为断言变化

## Alternatives Considered

| 备选 | 被拒原因 |
|---|---|
| 保留 producer 前缀判别、只加 kind 字段 | 半途方案：context 判定仍藏字符串，正统 capability post-action 观察仍被误拒 |
| `Origin` 字段 | 立项 Q3 修正否决——一个词揉合 context/producer/authenticity 三职责 |
| `AcquisitionContext` 命名 | 备选；`ObservationContext` 与词条语义直连，取主提案 |
| ⑤ 原文"冒充仍被拒" | 立项 Q6 修正否决——anti-spoofing Deferred 下不可证明，验收不得声称不存在的能力 |
| 别名并存（ObservationProposal + ObservationRecord 双名） | 一词两义不消反增 |

## Owner / Authority Impact

- 零 Owner 迁移：Ledger admission 面（增一项结构性检查）、World Model
  relevance 判定（增 kind 分支）、Assurance 判定输入迁移——各 Owner
  职责不变，判别依据从字符串约定升维为显式语义字段

## ADR Refs

- docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md
  （P2/P3 + Terminology note 冲突台账 + Deferred ⑦）
- 模型挑战处置 commit 221e9444（Observation 极性锚定、MaterialEffect
  收窄降级为 policy）

## Residual Risks

- kind/context 均 producer 自报，伪造不可检测（⑦ 保持）——本 change
  的判定门是**语义归类**门，不是真实性门；文档措辞已按 ⑤b 约束
- EvidenceId 派生输入变化会使历史 id 失配——GREENFIELD 无持久化存储，
  影响限于测试内

## Status Log

| 日期 | from→to | 依据 |
|---|---|---|
| 2026-09-09 | →resolved | ENTRY 核验（base 221e9444、rename 面 8 文件实测、MaterialEffect 前缀判定定位）+ 立项 grill 一轮（6 问 + 3 修正：Origin 拆为 Context/ProducerIdentity、EvidenceId 语义升维、⑤ 拆 5a/5b）定稿 acceptance 8 / D1-D8 |
| 2026-09-09 | resolved→planned | PLAN 落盘 plans/2026-09-09-ing-006-observation-ingress-kind-and-rename.md；Route: Direct（D8） |
| 2026-09-09 | planned→implemented | TDD RED（5 失败：N1/N2/N3/N4/N6 = 四项新行为 + policy 判别器；既有 56 + N5 机械项全保持）→ GREEN（62/62：Kernel 45 + Agent 17）；grep `ObservationRecord` 仅余 1 处有意历史注记；改动面 = plan After 表 + 1 新测试文件零意外；E2B 8 断言零改动（D1 语义冻结首次执行） |
| 2026-09-09 | implemented→reviewed | fresh SubAgent 六轴 APPROVE（A1-A6 全 PASS）；F1/F3 陈旧注释、F2 context 无对称校验（均 minor/nit） |
| 2026-09-09 | reviewed→implemented | 修复批：F1 EvaluateObligations 注释改 kind∧context；F3 措辞去误读；F2 补 `context-recognized` + N7 用例；复跑 63/63 GREEN |
| 2026-09-09 | implemented→verified | 验收 1-8 逐条对照 evidence 全 GREEN；grep 残留 = 1 有意注记；四元组 actual/evidence 完整；台账核对（N1-N7 落盘、断言零变化、56→63） |
| 2026-09-09 | verified→closed | 范围完成 + acceptance 被证明 + 无未授权改动；A9 文档同步：CONTEXT（Observation Context 词条 / Evidence Record 字段 / Obligation Fulfillment policy 措辞）+ 协议 P2/P3 known gap 消除标注 + Observation Record 冲突台账闭合；无阻塞 Human Decision |
