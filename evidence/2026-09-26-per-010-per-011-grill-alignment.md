# PER-010 / PER-011 — adversarial grill alignment

状态：Q1–Q11 已由用户选择推荐方案并完成裁决。本文件记录设计裁决与仓库事实，不代表 Product 实现已符合设计。

## 已确定的设计树

| 决策 | 用户选择 | 直接约束 |
|---|---|---|
| Q1 checked capability 粒度 | 按本次字段的 claim domain 判断；adapter 默认 collapsed，仅可追溯二态契约允许 exact | Android API level、XML 字段存在和历史样本均不能证明 exact |
| Q2 `partial-unrepresentable` 的位置 | checked claim 保持 `Unknown`；原因放 capability limitation / provenance diagnostic | reason 不成为新 claim state 或 WorldBelief truth |
| Q3 lineage owner | Fusion 计算递归去重的 leaf/source basis；Evidence Ledger 保存不可变 lineage；WorldModel 消费时不把 derived 记作独立佐证 | 没有新的 authority owner |
| Q4 有限覆盖 | `AlignedWithCoverageLimit` 可形成带 limitation 的 fused observation；具体消费的充分性留给 Assurance | coverage limitation 不等于 action admissible |
| Q5 Aligned 后的分歧 | Aligned 只给联合融合资格；同一 claim domain 内，有有效字段 authority 才可 `Supported + overruled-source`，否则 `Conflicted` | 不按时间对齐或 confidence 自动选源 |
| Q6 derived proposal ingress | 可经 P2/P3 进入 WorldModel，但 corroboration 使用去重后的 leaf/source basis | F 不成为新独立 source |
| Q7 collapsed false 与视觉 | semantic checked 保持 `Unknown`；视觉 ON/OFF 只产生 rendered appearance claim | 不从视觉外观补出 `Checked` / `Unchecked`；异 claim domain 不制造同一 claim conflict |
| Q8 direct conflict | F 不得删除 A/B、单独清除 direct conflict，或把解释结果变为独立事实 | WorldModel 原有的有痕裁决权保留；F 不是 resolver |
| Q9 exact 证明 | source/控件语义契约证明本次 claim domain 只有二态，且 metadata 与 fixture/contract evidence 可追溯 | 证明缺席即 collapsed；API 版本不能代替证明 |
| Q10 lineage 异常 | 自引用、循环、缺父证据、声明 basis 与父链不一致均 fail-closed | 不猜测修复，不将 derived 降级为 independent source；原 source evidence 保留 |

## 对抗场景

1. API 36 可表达 Partial，但 legacy XML `checked=false`，且没有控件二态契约：semantic checked 为 `Unknown`，reason 在 limitation；不得输出 `Unchecked`。
2. `A → F`：独立依据仍为 `{A}`；`A+B → F1`、`F1+C → F2`：独立依据为 `{A,B,C}`，F1/F2 不增加票数。
3. screenshot t1 与 hierarchy t2 被判 `Aligned`，中间发生未观察到的 async mutation：若同一 claim domain 不一致，保留冲突或按独立有效的字段 authority 裁决；Aligned 本身不裁决。
4. A、B 直接冲突，F 解释其中一方：A/B 和冲突处置保留可追溯；WorldModel 既有 resolver 可以另行有痕裁决，F 自己不能销案。
5. lineage 有 cycle、missing parent 或伪造 closure：derived admission fail-closed；A/B 原有证据仍可查询。

## 仓库事实与 Q11 裁决

- `src/UniClaw.Host/UiAutomatorDump.cs` 的 `CheckedValue` 将 `checked` 缺失及未识别值回退到 `false`；`MapTargetStateClaim` 把 false 映射到共享 `*.state=off`。这与 Q1/Q2/Q9 的新设计不同。
- `changes/PER-009/state.md` D13 与验收 5 明确把视觉 ON 和 XML checked=false 当作同一 `*.state` 冲突，并允许 XML 定案 OFF；`src/UniClaw.Kernel/World/ConflictResolver.cs` 沿用该路径。这与 Q7 的 semantic/rendered 分轴决定不一致。
- `src/UniClaw.Kernel/Evidence/Provenance.cs` 目前只有 `TransformationLineage` 字符串列表；`EvidenceLedger.Admit` 不验证 EvidenceId 父链，`WorldModel.Reconcile` 把每个 admitted record 的 EvidenceId 加入 basis。目前没有 Q3/Q6/Q10 所需的 derived lineage 执法。此为后续实现缺口，不等同于更换 owner。
- `WorldModel.ResolveConflict` 可有痕清除当前 conflict，保持 EvidenceBasis 并记录 ConflictResolutionLog。Q8 禁止的是 Fusion 自行销案，不否定 WorldModel 既有 authority。

**Q11 裁决**：PER-010/011 保持前向设计并继续 FROZEN；PER-009 不重开。上述不一致正式记录为 semantic migration mismatch。任何 PER-011 implementation 开始前，必须先有 dedicated migration decision，定义 transition mapping、compatibility boundary 和 acceptance；本轮不执行迁移实现，也不声明 Product runtime ready。

## Q11 focused verdict

```sql
Q11 PASS
PER-010 v0.1.1 FROZEN
PER-011 v0.1.1 FROZEN
PER-009 REOPENED: NO
PER-011 implementation gate: dedicated migration decision REQUIRED
```

Baseline impact: Product baseline reopened `NO`; PER-009 reopened `NO`;
WorldModel authority changed `NO`; Grounding authority changed `NO`.

## 文档后续

Q11 已确定；PER-010/011 spec、plan、state、相关 fusion plan 与 `CONTEXT.md` 已同步。只有形成难逆且有真实取舍的系统级决定时才评估 ADR；本轮只记录 migration gate，不创建 ADR。
