# 0010 — Freshness 是消费相对的 sufficiency judgment，不是 WorldBelief 属性

协议基线 P4/P10/P13 与产品基线 §12/§20.4 多处以「revision / Slice /
binding 携带、降级或 loss freshness」的措辞表述 freshness（P4 known gap
要求 freshness 参与判定）。P4 落地裁决（立项 grill，2026-09-09）：
**freshness 不是 WorldBelief / Slice / CanonicalBinding 的自身属性，
不存在全局 `IsFresh` 真相**。职责拆分为两层：

```text
World Model
→ 表达 Freshness Basis（判定输入聚合，temporal provenance 层面）

Consumption Requirement + Freshness Basis
        ↓
Assurance（消费时，action judgment 内）
→ Freshness Judgment
  Sufficient / Insufficient / Unknown
        ↓
Authorization
```

Freshness Judgment 是 **Freshness Basis × Consumption Requirement 的
关系判断**，不是 belief 单侧属性，也不是 revision 自带的全局状态。
CanonicalBinding validity 与该 judgment 正交（见下）。

理由：revision 不可变且系统无 canonical clock（Deferred ⑪）——若在
Reconciliation 时冻结 Fresh 状态，无新 revision 时该状态永不变化，
「revision current 但 freshness insufficient」（Pressure Scenario 14）
将不可表达；freshness 本质随消费时刻 / 动作上下文变化，只能是
consumption-time 判断。

规则：

- Insufficient 与 Unknown 都 fail-closed，且不得折叠（输入不足 ≠ 判定
  不满足，来源不同、词汇可区分）；
- freshness 判定输入不足 → Unknown（runtime uncertainty）；必需的
  freshness evaluator 未配置 → composition/configuration error，
  fail-fast——两者不混；
- Consumption Requirement 表达「这次消费对 freshness 的要求」（当前
  realization 用 target + effect 语义；字段集不锁，随 buyer 扩展）；
  evaluator 输入显式携带 requirement，防止退化成「按 basis 判一个全局
  Fresh/Stale」；
- 同输入 deterministic evaluator 必须给同结果；被拒 binding 是否允许
  re-judgment 保持 Deferred ③，本裁决不偷解——consumption-relative 的
  证明用**同 revision 上两种消费（低 / 高 requirement）得到不同
  judgment**表达，不用「同 binding 先拒后放」；
- 派生 validity 面（Slice / CanonicalBinding validity）保持 revision
  currency / consumption 派生，不混入 freshness 维度；
- Freshness Judgment 只对该次消费有效，不回写 belief、不形成第二
  WorldBelief Authority；
- freshness policy（阈值 / 算法 / canonical clock）保持 Deferred ④/⑪，
  实现经可注入 deterministic evaluator seam 测试，不提前锁算法。

## Supersedes / narrows 产品基线 §20.4 该句的解读

产品基线 §20.4 Canonical Binding 失效条件含「freshness loss 后失效」
（§12 lifecycle「freshness 下降时必须降级」同源）。**该句按旧字面
（freshness 不足 ⇒ binding invalid）不得继续实现**，由本 ADR 语义修正：

```text
CanonicalBinding validity
= revision currency + consumption state（派生，无 event）

Freshness
= consumption-relative Assurance sufficiency

Freshness Insufficient / Unknown
→ authorization denied
≠ binding invalidation
```

即 freshness 不足拒绝的是**授权**（Assurance judgment not
admissible → Gate 拒绝），binding 的派生 validity 不因此消失
（validity ≠ authorization 的 freshness 面）。基线文档本身暂不改动；
协议基线 P4/P10/P13 Validity 措辞随 P4 实现 change 精化并标注本 ADR。

## Considered Options

- **Expression-time（reconcile 时冻结 Fresh/Stale 到 revision）**：被拒
  ——不可变 revision 上的 Fresh 永不降级，Scenario 14 不可构造；且制造
  belief 对象上的第二真相。
- **二值折叠（Sufficient / Insufficient）**：被拒——Unknown（判定输入
  不足）与 Insufficient（判定过且不满足）语义不同源，折叠违反
  「unknown ≠ fresh 且不得伪装成判定过」。
- **同 binding 先拒后放证明 consumption-relative**：被拒——违反
  deterministic evaluator 同输入同结果，且偷解 Deferred ③
  （re-judgment 许可未决）。

## Consequences

- 协议基线 P4/P10/P13 Validity/Status 措辞随 P4 实现 change 精化
  （「双维度评估」拆为 currency 在 World Model、freshness sufficiency
  在 Assurance 消费时）+ known gap 消除标注；场景表第 2 行
  「intent freshness 判定拒绝」的 currentness 误用词汇一并净化。
- CBA-005 验收 9「不使用 freshness 措辞」约束（防 P4 未落地前的词汇
  混淆）由本裁决取代：currentness 系列检查名保留，freshness 成为真实
  语义后归 Assurance judgment。
