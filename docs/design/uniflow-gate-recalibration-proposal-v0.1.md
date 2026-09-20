# UniFlow 门控再校准提案 v0.1（草案 · 待对抗审核）

> Status: SUPERSEDED
> Authority: NONE
> 状态: SUPERSEDED by v0.2（2026-09-20 对抗审核 + 四处裁决；历史保留不改写）
> 日期: 2026-09-20
> 上游: ADR-0007（结构冻结条款）、ADR-0004（已 superseded，语义被吸收）、
> `.agents/skills/uniflow/SKILL.md`（现行流程文本）
> 证据: 2026-09-19 单日人为介入审计（附录 A）
> 用途: 交给独立 AI 做对抗审核；审核通过前不立项、不改任何文件。

---

## 1. 问题陈述

UniFlow 当前把「需要人决定」实现为**阻塞状态**而非**决策分类**，且所有
Change 无论大小走同一条八状态全量路径。2026-09-19 的单日审计显示：

- 18 个 commit、15 个 Change（CORE-001..015）、2 个 ADR；
- **10 个阻塞场合、≥16 个显式裁决槽**，其中：
  - 两份 docket（CORE-012/CORE-014）共 8 个 `DECIDE` 槽：
    **6 个原样采纳 agent 建议、2 个小幅收紧/补充、0 个推翻**；
  - CORE-015 分步裁决 4 次（step1/framing/step2/step4）；
  - ABG-001/UAP-001 于 09-13/14 完成实现，09-19 才获批准关闭
    ——「待人工关闭」僵尸态 5~6 天；
- 对照：方向型交互中人的修正率显著（EXP-008 grill 三轮 15 问 6 处用户
  修正；E2B-001 D8 用户推翻 plan 初版方案；CORE-015 用户补充裁决直接
  重构工作方向）；
- 门腐烂：CORE-008 挂 `resolve_human_gate` 至今，实际已被 CORE-012
  裁决 + CORE-013 实现 + ADR-0023 落档关闭，账面未销。

**诊断**：人在方向点（intent/scope/reframe）信息量高，在汇聚点
（选项分析完备后的 docket、closure 盖章）信息量趋零。现流程对两者
给予同等阻塞重量，这是「繁琐 + 人为介入过频」的直接来源。

## 2. 提议（四项窄修 + 一项卫生）

### P-A 决策三分类（核心）

把「Human Decision」从单一阻塞状态改为三分类：

| 类 | 名称 | 判据 | 行为 |
|---|---|---|---|
| C1 | 阻塞 | ①方向真两可（intent/scope/重大取舍无既定推导）②推翻或 supersede 已接受 ADR/冻结不变量 ③不可逆外部影响或产品范围变更 ④新开顶层 Owner/谱系 | 立即阻塞，人显式裁决 |
| C2 | 干了再报告 | 有真实备选，但爆炸半径有界：不触碰冻结 seam、可回退、无外部不可逆影响 | 直接执行；完整选项分析仍必须落档（state.md/evidence）；进日结摘要，人可在 veto 窗口（下次触点前）推翻 |
| C3 | 机械判定 | 测试绿否、范围超否、基线回归否——有确定性判据 | 机器自判，不出现在人的视野 |

分类判据是客观可核对的（四条 C1 触发器均为事实问题），不依赖 agent
自评「我有信心」。

**回放 09-19**：CORE-012 Q2–Q4、CORE-014 Q2–Q4、CORE-010 确认、
ABG/UAP closure → C2/C3；CORE-003 grill、CORE-012 Q1（点名修改
EffectBoundary 冻结面）、CORE-015 framing → C1。
阻塞场合 10 → **3 + 1 份日结**。

### P-B 双通道（大小分道）

- **MINIMAL fast path**：满足全部资格条件的小活走
  「理解→干→验→关」四个落脚点，Change State 折叠进结构化
  commit message。资格条件（全部满足，任一失守即升级 STANDARD）：
  单会话内完成；不触碰冻结面（AGENTS.md 禁改清单、不变量执法测试
  覆盖的 seam）；acceptance 可机械判定；可回退。
  既有「跨会话自动升级 STANDARD」规则保留，新增「资格失守自动升级」。
- **STANDARD/DECISION-HEAVY**：现行八状态路径不变，不减任何一步。

八状态词汇本身不重标记（`lifecycle_state` 是既有 state.md 与
entry/resume 协议的负载词汇，重命名 churn 大于收益）。

### P-C REVIEW/VERIFY 单遍合答

现行文本「REVIEW 与 VERIFY（分离，永不合并）」改为判断分离表述：

> REVIEW 与 VERIFY 是两个判断——「实现得好吗」与「完成被证明了吗」
> ——不得互相替代、不得由同一自报互相背书。执行遍数按深度分档：
> Direct + MINIMAL 可单遍合答（两个问题分别显式作答）；
> Delegate 与 DECISION-HEAVY 强制分遍（worker 自报永不决定完成，
> 该语义不变）。

### P-D closure 去盖章 + 抽样审计

- 自动 CLOSED 条件：acceptance 全部被证明（验证四元组齐）+ 无 C1
  残留 + 无未授权改动。
- 人改为事后抽样审计（建议起始率：DECISION-HEAVY 全抽、
  STANDARD 1/4、MINIMAL 1/10；抽到问题按失败边回溯，并计一次
  摩擦记录）。抽样率本身为可调参数，待裁决。
- 依据：ADR-0007 A9 明文「普通任务不需要毕业报告」；当前实践
  （awaiting-human-closure 排队）比规则更硬，属漂移而非新发明。

### P-E 卫生项：开门登记与销账

- 单一 open-gates 登记处（建议 `changes/README.md` 增一节），
  每个 C1 门记录：所属 change、触发器、开启日期、关闭依据。
- Change 关闭时核销其名下开门；立即销账 CORE-008（依据：
  CORE-012 docket 裁决 + CORE-013 实现 + ADR-0023）。

## 3. 不变项（承重墙，本提案不动）

1. Resolve Gate 六问语义——不带矛盾进入实现；
2. 验证声明四元组（method/expected/actual/evidence ref）与四级
   level——完成必须有可复现证明；
3. 五条失败边与 BLOCKED 正交 disposition；
4. 八状态词汇与 entry/resume 复验协议（仅复验范围收窄为跨会话
   必做、同会话内按需）；
5. PERSIST durability ALWAYS, depth VARIABLE（含自动升级规则）。

## 4. 预期效果与度量

- 阻塞场合/日：10 → ≈4（3 C1 + 1 日结触点）（回放口径）；
- closure 延迟：5~6 天 → 当日；
- 采纳后持续度量（周报）：每 Change 阻塞次数、日结 veto 率、
  抽样审计发现率、C2 误分类率（审计出 C2 项实为 C1 即计）。

## 5. 风险与反方论证（供对抗审核重点攻击）

- **R1 橡皮章是幸存者偏差**：0 推翻可能因为阻塞门迫使 agent 做出了
  完备分析；去掉等待可能连带降低分析质量。缓解：C2 仍强制完整选项
  分析落档（去掉的是等待，不是功课）+ veto 窗口。
- **R2 自利分类**：agent 倾向把 C1 判成 C2 以减少打断。缓解：四条
  触发器为事实判据；误分类入抽样审计与摩擦记录。
- **R3 自动关闭的迟到发现爆炸半径**：GREENFIELD 阶段回退成本低，
  但随产品面扩大而上升。缓解：抽样率随风险分级；失败计数回流。
- **R4 fast path 滥用**：agent 为省 state.md 而压档。缓解：资格条件
  客观 + 自动升级 + 抽样。
- **R5 多模型受众**：本仓库 canonical surface 被 DSH/Codex 多模型
  消费；较弱模型可能需要更多而非更少门控。缓解：分类规则落在
  共享层（AGENTS.md 禁 host 专有语义），不按模型分叉；深度分层
  （capability tier → 指令厚度）另案处理，不在本提案内。
- **R6 日结批处理伤 C1 时效**：C1 立即阻塞不受批处理影响；仅 C2
  进日结。若 C1 被误入批处理队列即违反 P-A。

## 6. 实施路径（审核通过后）

1. ADR-0007 窄修（LEARN-hook 证据：①2026-09-20 用户体感反馈
   ②2026-09-19 审计——两条已齐）；
2. `uniflow/SKILL.md` 对应条款改写（A6.5 表述、决策三分类、
   双通道资格条件、closure 规则）；
3. P-E 清账（CORE-008 + 登记处建立）；
4. 两周观察期后按 §4 度量复盘，必要时校准抽样率与资格条件。

## 7. 请对抗审核者优先攻击的位置

1. C1 四条触发器是否有漏（某类真需人裁决的决策落进 C2/C3）；
2. 单遍合答是否会在 Direct 大型 Change 上失效（当前阈值是
   「MINIMAL 才可合答」还是「非 Delegate 即可」——本提案取前者，
   论证是否充分）；
3. 抽样审计对 closure 语义的削弱是否可接受（ADR-0004 遗产
   「Review 与 worker 自报永不决定 COMPLETE」的边界在哪）；
4. MINIMAL 资格四条件是否可被形式化核对（谁执法：测试/脚本/人）；
5. 度量口径（§4）是否能证伪本提案（若采纳后 veto 率/误分类率
   高企，回退路径是什么）。

---

## 附录 A · 2026-09-19 人为介入审计明细

| # | 场合 | 类型 | 裁决槽 | 结果 vs 建议 |
|---|---|---|---|---|
| 1 | CORE-003 grill-with-doc | 方向 | 多轮多问 | 产出 D 系列（方向价值高） |
| 2 | CORE-010 human-confirmed | 确认 | 4 问 | 按建议确认 |
| 3 | CORE-011 granted-by-human | 授权 | 1 | 批准首切片 |
| 4 | CORE-012 docket | 汇聚 | 4 (`DECIDE-Q1..Q4`) | 6 槽原样采纳（含 Q1=A 点名授权） |
| 5 | CORE-014 docket | 汇聚 | 4 | 2 槽小修（Q3 收紧/Q1 Session 补充），余原样 |
| 6-9 | CORE-015 step1/framing/step2/step4 | 方向+分步 | 4 | framing 为真实方向补充；其余通过 |
| 10 | ABG-001/UAP-001 closure | 收尾 | 批准 | 经 09-16 docket 批准关闭（实现完成于 09-13/14） |
| — | CORE-008 `resolve_human_gate` | 汇聚 | （悬置） | 实际由 #4 吸收；账面未销（P-E） |

证据文件：`evidence/2026-09-19-core-012-human-review-docket.md`、
`evidence/2026-09-19-core-014-human-review-docket.md`、
`changes/CORE-008/state.md`、`changes/CORE-015/state.md`、
`changes/ABG-001/state.md`、`changes/UAP-001/state.md`。
