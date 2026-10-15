# Agent 异常升级与观察机制（设计讨论稿 v0.1）

> **SUPERSEDED（2026-10-17）**：本稿为未 grill 候选形态，已被 v0.2
> （GRILLED / DECISION-COMPLETE）取代——见
> `agent-exception-escalation-and-observation-v0.2.md`。本稿仅存档，
> 不再是任何实现的依据。

> Status: DRAFT / CANDIDATE
> Authority: NONE（本文档只整理问题、候选机制与借鉴对照，供 owner 讨论
> 敲定；不修改任何冻结面）
> 日期：2026-10-16
> 谱系：SETTINGS-COVERAGE-002/003/004 全遍历实测发现（2026-10-14/15）→
> 2026-10-16 owner 系列讨论收敛 → 本稿
> 上游：consultation-protocol-v0.1 · AGT-001 FROZEN · RUN-004（CLOSED）·
> RUN-005 FROZEN · PER-019 · baseline §24.2/§24.4/§24.7/不变量 43-47 ·
> product/AGENTS.md（ADR-0040/0041）

---

## 1. 问题清单（遍历实测坐实，证据见三个 change 的 evidence）

| # | 症状 | 根因定性 |
|---|---|---|
| P1 | identity-only 页面（感知只产出路由身份，无可 grounding 材料）是 pass4-8 全部死因 | 感知能力死角 + 无升级路径 |
| P2 | Slow 四重不利：异步结果迟到被错屏丢弃 · Defer 等待预算(2轮) < fetch 界(15s) · 与决策共享单飞行会话 · slow.visual 未接线 | 结构性调度错配 |
| P3 | 异常三宗罪：004 前本地自答绕过 Agent（已修）；现行仍是"只准 Defer"受限咨询；popup 卡死/no-progress/coverage 分歧不在任何触发词汇里 | 决策权错位 |
| P4 | 弹窗清障词表含 button1（确认侧），会点"确定"；目标由 Director 死规则选，Agent 橡皮图章 | 决策权错位 + 词表缺陷 |
| P5 | 同路由 96 步空转零报警（pass5） | no-progress 监督盲区 |
| P6 | slow 令牌零耗时字段；Dropped 时耗时在手边未记；003 run 产物未入库 | 观测性缺口 |
| P7 | Kernel Completion / Host terminal / Coverage 三判据分离正确，但 Agent 看不到 coverage 状态（R5：12/12 verified 却 5/15 覆盖即终局） | 上下文缺口 |

## 2. 候选机制总览（讨论收敛的形状）

### M1 异常边界真咨询

```text
卡住类型分诊：
  看不清 / grounding 失败 / 验证失败 → Agent 选项集 {再观察, 回去(back),
    标记 blocked+原因, (有滚动容器时)滚动}；Act/Policy 仍拒（防幻影目标）
  派发结果未知(UnknownOutcome) → 无选择：强制重观察对账（§24.4 冻结，
    Agent 也不许选"等待"）
分层：Kernel 便宜反射在前（聚焦复查≤3/慢看触发谓词），Agent 定向深思在后
```

### M2 三层观察阶梯 + 参数化 Defer

```text
① 摘要（现状）：元素角色/边界/可点性 + claims
② 局部视图（新增，默认形态）：Owner 派生只读视图——局部 claims 全量
   (值+判定+冲突) + 元素(边界/状态/认识论) + 容器结构/滚动容器/视口摘要
   + 观察侧诊断(XML在否/fast在否/入账提案数/各段耗时/近N周期趋势)
③ 原始截图（能力门控）：仅视觉模型、仅触发式、判断级不入账
Defer 升级为带参观察请求 {目标, 深度(普通/聚焦/深), 轮数, 目的}
越贵的眼睛越晚出手：② 便宜文本可用且可审计，③ 最后手段
```

### M3 Slow 参与闭环（反射层修补）

触发词 = "Fast 产不出任何东西"时**同步等待**（该周期本无事可做）；等待
预算 ≥ fetch 界；令牌带耗时；run 产物入库补证。

### M4 弹窗 dismiss-only policy

`product/policy/` 工件（ADR-0041 面）：遍历场景弹窗只许 {cancel, dismiss,
back}，confirm/positive 禁止除非白名单；处置权交 Agent（判断级读弹窗
内容选择），Host 执法不变。超界前一次咨询（换清障手段）。

### M5 no-progress 进上下文

frontier 增长/停滞摘要 + 同 route 重复计数进咨询上下文；Agent 见停滞即
改策略。

### M6 Director 角色反转（方向，分期；§3.2.1 维度化叙事）

摸底期"Director 预嚼指令、Agent 照念"→ 产品期 Director 退为执法+供数，
frontier 状态交 Agent 自主决策——**编排者归位**（Agent 在策略维度编排，
不是被放权）。v1 先做异常时刻真咨询，全面反转后置。

## 3. Harness 通行设计对照（参考面）

### 3.1 DSH 自身机制对照

| DSH 机制 | DSH 里的做法 | 映射与启示 |
|---|---|---|
| Turn 生命周期 | 无工具调用即收尾；`concludesTurn` 终结工具；max-tokens 粘滞 | 已借鉴（AGT-001：submit_decision=唯一终结）。**关键启示：Agent 主动观察应发生在 turn 内部多步推理里，不需要新循环** |
| Steering 注入 | Host 可 mid-turn 注入转向，agent 收件箱重读 | 对应物 = AbortCurrentTurn + cancel 语义（AGT-001 已冻结），不复用其词表 |
| ask-user 工具 | Agent 需要人裁定时主动发起提问 | 对应物 = 完成证明层3（AwaitingCompletionAdjudication）；**Agent 主动请求信息 = 工具形态、受 allowlist 管**——观察请求若走此形态需入 AGT-001 allowlist |
| Goal 轮次上限 | goal-round-driver 自主续跑有 max_goal_rounds | 同律已有（MaxConsultations/预算合同/M3 元原则）：一切自主循环显式预算 |
| 工具 schema 约束 | workflow 工具 restricted JSON Schema 校验子结果 | 已借鉴（GQ4 单源生成） |
| read 类工具（fs 等） | Agent 把读环境当推理材料，常态 | **最重要对照差异**：文件系统没有 owner 权威问题，我们的 WorldBelief 是 canonical 的——DSH 可以给 read 工具，我们必须给 **view 工具**（Owner 派生只读投影，ADR-0011 纪律） |
| Session resume/fork | log replay 恢复 | 对应物已 DEFER（GQ2：跨进程走 owner records） |
| Approval 流 | 危险操作审批 UI | 对应物 = Grant 链（§24.6 冻结）；不借鉴其交互形态 |
| 诊断 fail-loud | 启动 stderr 捕获进 tail、首现场大声失败 | 同哲学已有（fail-closed）；P6 观测性修补是其延伸 |

### 3.2 行业通行模式对照

| 模式 | 通行做法 | 映射 |
|---|---|---|
| 升级阶梯（escalation ladder） | 便宜自愈 → 工具辅助自查 → 人裁/放弃 | M1/M2 的反射→视图→截图→申报分层即此结构的 owner-aware 版 |
| Agent-as-orchestrator（多数 harness） | Agent 一个维度编排一切：指令式驱动工具，约束少且软（提示词级） | **我们不是反着选，而是维度化**：UniAgent 也是 orchestrator——**高维、声明式、多硬约束**（目标→契约→策略→恢复路线；typed 词汇/预算/安全 policy/无直接效应权）；Kernel 是低维机械执行编排+执法面。两层各管一层。含义：①L2 Policy 就是 Agent 的编排语言（约束多不是降级，是高维编排安全存在的前提）；②不能照搬「随便 read」的低维工具面——高维编排者的传感器必须经 Owner 中介（M2-② view 工具化） |
| 渐进披露（progressive disclosure） | 有界上下文 + 按需拉取（plan mode、resources） | M2-② 局部视图 = progressive disclosure 的 Owner 派生版；反倾倒原则（协议 §5.2）同源 |
| Human-in-the-loop | 不确定即问人 | 仅两处保留：完成证明层3 与 Grant；其余不确定走 Agent 选项集而非问人 |

### 3.2.1 维度化重述（对 M6 的修正性叙事）

「Director 反转」不是给 Agent 放权，而是**编排者归位**：摸底期 Director 预嚼
指令 = 低维执行面越权代行了高维编排；产品期形态 = Agent 在策略维度编排
（frontier 状态 → 自主决策），Director 退为执法+供数。同理，M1 异常真咨询
= 异常时刻编排权的恢复；M2 观察阶梯 = 编排者的 Owner 中介传感器。

## 4. 机制 ↔ 冻结面 ↔ 修订路径对照

| 机制 | 借鉴来源 | 冲突的已定约束 | 修订路径 |
|---|---|---|---|
| M1 选项集含 back/blocked | escalation ladder | 004「只准同号 Defer」 | Host change（约束住 Director 代码，不动 Kernel 协议）；新证据=pass5/pass6-8 |
| M2-② 局部视图 | DSH read 工具的 view 化 + progressive disclosure | 协议 §2.1 有界摘要（反倾倒原则不破，按容器/作用域切） | 协议窄扩展 amendment |
| M2-③ 截图 | DSH 多模态输入 | 协议 §2.1 + AGT-001 工具面（若走 context 附着则非工具；若走请求形态则入 allowlist） | 协议 amendment + AGT-001 允许的 reasoning-only 通道登记 |
| M2 参数化 Defer | ask-user 的"请求"形态 | ObserveSpec 现形 {Subject, MaxRounds}（V5 上限不变） | 协议 amendment（随 M2） |
| M3 同步 Slow | — | PER-019「slow 不阻塞周期」 | PER amendment（定向：仅"无可用材料"触发词） |
| M4 弹窗 policy | approval/危险面分级的思路 | AGT-009 词表（含 button1） | 新 policy 工件 + Host change |
| M5 no-progress 上下文 | — | Progress 现字段集（RUN-004 D3） | 协议字段扩展（v3 或预留槽） |
| M6 Director 反转 | agent-as-orchestrator 的反面教训 | coverage-director 模式现状 | 分期，v2 方向性声明即可 |

**声明不动**（任何修订不得触及）：错屏丢弃门 · 判断级/事实级分界（Agent
目击不入世界模型）· 幻影目标防线（Act/Policy 需可 grounding）·
UnknownOutcome 强制重观察 · 失败咨询计轮（GQ3）· 三判据分离 · Kernel
never invents/repairs Policy。

## 5. 待裁决问题（后续讨论敲定清单）

| # | 问题 | 关键权衡 |
|---|---|---|
| Q1 | back 的载荷形态：纯无目标安全步的 Act？还是独立成员？（safe 动作类已含 back，具体协议形状待核） | 协议膨胀 vs 载荷清晰 |
| Q2 | 局部视图字段冻结：最小集如何取（§2-M2-② 所列六族）；每字段要有真实读取证据（ADR-0011） | 信息充分 vs 视图膨胀 |
| Q3 | 截图附着形状：触发词集、能力声明面（profile）、判断级标记进 trace | 与 M2-② 的先后与互斥 |
| Q4 | Slow 同步化的等待预算与触发词终形 | 与 fetch 界的关系 |
| Q5 | 弹窗 policy 工件形态与 Agent 判断权边界（词表兜底 vs 纯语义判读） | 机械可判 vs 语义灵活 |
| Q6 | no-progress 的机械定义（frontier N 周期不增？同 route 计数阈值？） | 误报率 vs 灵敏度 |
| Q7 | Director 反转分期表 | v1 范围冻结 |
| Q8 | ObserveSpec 扩展的深度枚举（普通/聚焦/深）与 E4b 反射的关系 | 反射与深思的分界线 |

## 6. 建议的落地编队（敲定后）

1. **Change A（Host 层，打包）**：M1 选项集 + M4 弹窗 policy + M3 Slow
   同步化 + P6 观测性（令牌耗时/产物入库）——证据齐备，冲突面小。
2. **Change B（协议 amendment）**：M2（②局部视图 + ③截图 + 参数化
   Defer）+ M5 no-progress 字段——动咨询协议，单独评审。
3. **Change C（方向性）**：M6 分期表——先记录方向，不实现。
