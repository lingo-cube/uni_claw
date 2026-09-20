# UniFlow 门控再校准提案 v0.2（shadow-mode 实验方案）

> Status: DRAFT
> Authority: NONE
> 状态: DRAFT — 对抗审核通过方向、四处裁决落定（附录 B）；本版是
> **两周 shadow-mode 实验方案**，不是 ADR-0007 窄修。
> 日期: 2026-09-20 · 取代 v0.1（审核处置见附录 C）
> 上游: ADR-0007 · `.agents/skills/uniflow/SKILL.md` ·
> 审阅方结论（2026-09-20 对抗审核，七条关键问题）
> 效力: 实验期间现行 SKILL.md 与 ADR-0007 **零改动**；本文件是实验
> 规则的唯一载体。升级为规范必须凭校准报告另立 ADR 窄修 change。

---

## 0. 裁决摘要（2026-09-20，人裁决四处）

| # | 问题 | 裁决 |
|---|---|---|
| Q1 | 采用方式 | **Shadow mode 先行**：人工门全保留，agent 并行输出分类，两周后凭数据定窄修 |
| Q2 | 自动关闭范围 | **一期零自动关闭**（严于审阅建议）：shadow 期 closure 全人工、只做批量化降延迟；MINIMAL 机检自动关闭属二期，且以 reopen 协议落地为前提 |
| Q3 | C1 边界 | **扩展 + 推导基准**：无法由已接受工件唯一推出即 C1；「可推出」= 由 ADR/冻结不变量/已裁决 Decisions 经确定性推导或单一显然步骤得出 |
| Q4 | 执法方式 | **manifest + 确定性脚本**：脚本验形状/冻结面/一致性；不可机检字段 agent 声明 + 抽样兜底 |

## 1. 问题陈述（v0.2 修正表述）

现行 UniFlow 中「需要人决定」**不是 lifecycle_state**：它是 Resolve Gate
与 CLOSED 判定中的阻塞条件，`BLOCKED` 是正交 disposition。持久化已有
三档（MINIMAL/STANDARD/DECISION-HEAVY），缺的是**遍历折叠规则**与
**决策分类路由**——这是语义扩展，不是状态重标记。

2026-09-19 审计（附录 A）：15 个 Change、10 个阻塞场合、≥16 个显式
裁决槽；两份 docket 的 8 个 `DECIDE` 槽 6 个原样采纳、2 个小修、
0 个推翻；closure 排队 5~6 天；CORE-008 门账实不符。对照：方向型
交互人工修正率高（EXP-008 6/15；CORE-015 reframe；E2B D8 推翻）。

**假设（待 shadow 数据检验）**：人工注意力在方向点信息量高、在汇聚点
趋零。若两周内人对 C2/C3 分类极少 override，假设成立；若频繁
override，假设被证伪，提案撤销。

## 2. 决策分类（P-A，shadow 期仅记录不生效）

### C1 阻塞（立即等人）

五条触发器（任一命中）：

1. 方向真两可：intent / scope / 重大产品取舍无既定推导；
2. **不可唯一推出**：无法由已接受工件（ADR、冻结不变量、本 Change
   已裁决 Decisions）经确定性推导或单一显然步骤得出的语义判断——
   含 Owner/Authority 边界变化、acceptance 的产品语义成分、风险与
   体验取舍、回退成本或并行影响本身需人批准的情况；
3. 推翻或 supersede 已接受 ADR / 冻结不变量；
4. 不可逆外部影响或产品范围变更；
5. 新开顶层 Owner / 谱系。

「单一显然步骤」的边界歧义是 shadow mode 的核心校准对象。

### C2 干了再报告（shadow 期 = 记录为「若无此流程本可自决」）

资格硬边界（全部满足）：

- 零外部副作用；不触碰冻结 seam、公共契约、ADR、Owner/Authority、
  产品 acceptance；
- 可推出性成立（触发器 2 反面），推导链必须落档；
- 回退路径具体（非「理论上可 revert」）：点名回退面、并行 dirty
  处置、文档状态恢复。

**veto 语义**：截止点 = 人对日结的显式 ACK（无 ACK 窗口不关）；
被推翻 → 记一次误分类、同类后续决策升级 C1；下游 Change 消费
未 ACK 的 C2 结论时按 C1 对待。

### C3 机械判定

有确定性判据（测试/脚本/边界测试）的问题，机器自判，不进人视野。

## 3. Shadow mode 协议（本版主体）

1. **期限**：两周或 ≥30 个决策事件（先到者）；
2. **人工门零变化**：所有 docket、grill、closure、step gate 照现行
   流程执行；`awaiting-human-*` 语义原样保留；
3. **并行输出**：每个本会发生的人工决策，agent 在台账追加一行——
   change、问题、建议分类（C1/C2/C3）、推导链引用、人工实际裁决、
   是否 override、等待耗时；
4. **台账**：单一文件 `evidence/2026-09-20-gate-recalibration-shadow-ledger.md`
   （实验产物，非 standing structure；不新建第二 task system）；
5. **closure 批量化**（一期唯一的实际变更）：待人工关闭的 Change
   攒入日结/既有触点批量裁决，不逐个打断——只改节奏，不改语义、
   不自动关闭任何 Change；
6. **校准报告**（实验出口）：C2 override 率、C1 漏检率（人工推翻
   agent 判为 C2/C3 的比例，注意这是**下界估计**——只能观察到被
   抽中/被 override 的）、分类分布、「单一显然步骤」边界案例集、
   等待耗时对比。报告决定：立 ADR-0007 窄修 change / 重校 / 撤案。

**回退触发（初值，待裁决）**：C2 override 率 >20%、C1 漏检 >10%、
或抽样发现重大缺陷 ≥2 → 暂停实验重校。

## 4. 二期设计（shadow 通过后才实施，先落字供审）

### P-B′ 双通道 + manifest 执法

MINIMAL fast path（理解→干→验→关四落脚点，State 折叠进 commit
message）资格四条件不变（单会话、不碰冻结面、acceptance 可机械
判定、可回退），执法改为机器可读 trailer + 确定性脚本：

```text
Fastpath-V1:
  Depth: MINIMAL
  Decision-Class: C2
  Scope: <paths>
  Acceptance: <mechanical check ref>
  Verification: <command + evidence ref>
  Frozen-Surface-Check: <script ref + result>
  Session-Boundary: asserted-single
  Rollback-Ref: <revert plan ref>
```

`tools/validate-fastpath.py`（确定性）：trailer 形状；Scope 对冻结
allowlist（复用 closure 测试口径）；Acceptance/Verification 引用
存在且指向绿证据；与 commit message 结构段一致。**不可机检项**
（Session-Boundary、Rollback 质量）：agent 声明，抽样审计兜底，
虚假声明 = 摩擦记录 + 升级。任一资格失守自动升级 STANDARD
（升级规则本身由脚本触发，不经人工判断）。

### P-C′ REVIEW/VERIFY 单遍合答

两个判断（实现得好吗 / 完成被证明了吗）**各自产生独立证据行**——
review 记录与 verify 四元组不得互相引用为证明；单遍合答仅限
Direct + MINIMAL，Delegate 与 DECISION-HEAVY 强制分遍
（worker 自报永不决定完成，语义不变）。

### P-D′ 分期 closure

- 一期（shadow 期）：零自动关闭；仅批量化节奏（§3.5）；
- 二期前提：reopen 协议 + 执法脚本就绪；
- 二期范围：**仅严格机检通过的 MINIMAL** 可自动 CLOSED；
  DECISION-HEAVY、含 C1、被推翻过 C2 的 Change 永久保留人工
  closure；
- reopen 协议（新增失败边）：CLOSED → VERIFY（reopen）；触发 =
  抽样审计发现验收/文档同步/授权任一不满足；处置 = 不可变追加
  reopen 记录（不改写原 closed 事实），原证据全保留；
- 自动关闭执行者：脚本验机器条件（acceptance 引用绿、开门登记
  核销、授权路径匹配）+ Leader 签发，输出落不可变日志；
- 抽样率初值：DECISION-HEAVY 全抽、STANDARD 1/4、MINIMAL 1/10
  （可调参数）。

### P-E′ 门登记（一期即可做，不依赖 shadow 结果）

- `state.md` 保持唯一事实源；open-gates 登记处是**可生成索引**
  （建议 `changes/README.md` 一节），脚本检查索引与 Change State
  一致性；
- CORE-008 追加 superseded/absorbed 记录（原门触发器、被 CORE-012
  docket + CORE-013 实现 + ADR-0023 吸收、残余项：无、原 phase
  不再表示当前事实的原因），**不改写历史行**。

## 5. 不变项（承重墙）

Resolve Gate 六问语义；验证四元组与四级 level；五条既有失败边与
BLOCKED disposition；八状态词汇与 entry/resume 复验（跨会话必做、
同会话按需）；PERSIST durability ALWAYS / depth VARIABLE 及自动
升级规则；`awaiting-human-*` 现行语义（一期不动）。

## 6. 度量口径（可证伪）

- 阻塞场合：一个 Change 的一次连续人工交互计 1（docket 会话计 1，
  槽位数单列）；预期目标 10 → ≈4（目标非验收线）；
- C2 override 率分母 = ACK 日结内的 C2 条目数；
- 抽样发现率分母 = 被抽审的已关 Change 数；
- 误分类率 = 下界估计（censored），报告必须注明；
- closure 延迟 = 验证四元组齐 → CLOSED 落档。

## 7. 遗留开放问题（下轮审核候选）

1. 「单一显然步骤」的判例集如何初值化（shadow 前三天人工预标？）；
2. 台账事件边界：连续追问算一事件还是多事件；
3. 二期 MINIMAL 自动关闭的抽样率是否应为 1/10 起步（回退成本
   随产品面上升）；
4. 日结 ACK 的载体（聊天确认 / 台账批注）哪种构成有效 veto 截止。

---

## 附录 A · 2026-09-19 人为介入审计明细

（同 v0.1，未变）10 场合 / ≥16 槽；证据：
`evidence/2026-09-19-core-012-human-review-docket.md`、
`evidence/2026-09-19-core-014-human-review-docket.md`、
`changes/CORE-008/state.md`、`changes/CORE-015/state.md`、
`changes/ABG-001/state.md`、`changes/UAP-001/state.md`。

## 附录 B · 裁决记录

2026-09-20 对抗审核（独立 AI）：七条关键问题，判定「退回补齐，
保留为候选」。同日人裁决 Q1–Q4（§0 表）；裁决 Q2/Q3 采纳提案方
收紧版（严于审阅建议）。

## 附录 C · v0.1 → v0.2 变更溯源

| 审阅问题 | 处置 | 落点 |
|---|---|---|
| 1 表述失准（非状态、是阻塞条件） | 全盘接受 | §1 重写 |
| 2 C1 覆盖不足 | 接受+收紧（推导基准） | §2 触发器 2 |
| 3 veto 无安全边界 | 全盘接受 | §2 C2 veto 语义 |
| 4 自动 CLOSED 缺 reopen/独立权威 | 全盘接受+更严（一期零自动关闭） | §3.5、§4 P-D′ |
| 5 执法未形式化 | 全盘接受（含不可机检限度声明） | §4 P-B′ |
| 6 度量不可证伪 | 全盘接受（shadow mode 即检验） | §3、§6 |
| 7 销账≠删除 / 双真相 | 全盘接受 | §4 P-E′ |
