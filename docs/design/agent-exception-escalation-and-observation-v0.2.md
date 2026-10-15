# Agent 异常升级与观察机制（设计稿 v0.2 — GRILLED）

> Status: **GRILLED / DECISION-COMPLETE**（2026-10-17）
> Authority: NONE（docs 分类学：设计记录无执行权威；实现权威 =
> changes/AGT-019/ 的 spec 与 Decisions 表）
> 取代 v0.1（未 grill 候选稿，仅存档）。Authority：本稿经 owner 主审 +
> fresh-subagent 盲审 + 业界对照三轨 grill，是 AGT-019 的实现依据；
> 字段级冻结仍以 changes/AGT-019/spec.md 为 Change State 真相源。
> Grill 记录：changes/AGT-019/grill-1-blind-review.md（盲审 14 发现）·
> changes/AGT-019/grill-2-industry-compare.md（业界对照，带源 URL）
> 谱系：SETTINGS-COVERAGE-002/003/004 全遍历实测 → 2026-10-16 owner
> 系列讨论 → v0.1 候选 → 2026-10-17 三轨 grill → 本稿
> 上游：RUN-004（CLOSED，D3/D7 字段集冻结）· RUN-005（FROZEN）·
> AGT-001 FROZEN · PER-019 · baseline §24.2/§24.4/§24.7/不变量 43-47 ·
> product/AGENTS.md（ADR-0040/0041）

---

## 1. 问题清单（实测坐实，证据锚点）

| # | 症状 | 根因定性 | 证据锚点 |
|---|---|---|---|
| P1 | identity-only 页面（只有路由身份，无 grounding 材料）是 pass4-8 全部死因 | 感知死角 + 无升级路径 | 004 analysis-v2.md L51-52 |
| P2 | Slow 四重不利：迟到被错屏丢弃 · 等待预算(2轮)<fetch界(15s) · 共享单飞行会话 · slow.visual 未接线 | 结构性调度错配 | 002 trace 派发零落地（见 §5 PER-019 行锚点）+ settings-coverage.yaml L53-58 |
| P3 | 异常三宗罪：004 前本地自答（已修）；现行"只准 Defer"；popup 卡死/no-progress/coverage 分歧不在触发词里 | 决策权错位 | 004 state.md A2 + pass5-8 |
| P4 | 弹窗词表含 button1（确认侧）会点"确定"；目标由 Director 死规则选，Agent 橡皮图章 | 决策权错位 + 词表缺陷 | SettingsCoverageConfig.cs:86 |
| P5 | 同路由 96 步空转零报警 | no-progress 监督盲区 | 003 pass5（90/96 同路由） |
| P6 | slow 令牌零耗时字段；003 run 产物未入库 | 观测性缺口 | 002 trace slow 令牌格式（无耗时成分） |
| P7 | 三判据分离正确，但 Agent 看不到 coverage 状态（R5：12/12 verified 却 5/15 覆盖即终局） | 上下文缺口 | 004 R5 记录 |

## 2. 机制定稿

### M1 升级默认反转 + 异常选项集

```text
升级判定 = 机械谓词清单（Director 无裁量）：
  路由不明 / 材料不足(M3 谓词) / 验证失败 / 白名单外弹窗(M4) / 停滞双零(M5)
  → 谓词命中 = Host 强制升级 UniAgent
选项集 {再观察, back, blocked+原因, 滚动}：
  back    = Act + task 级 backDescriptor（配置缺失 → 选项隐藏）
  blocked = 回答 union 新成员（RUN-004 D3/D7 修订通道），计失败轮
  滚动    = 谓词门控 + 任务参数上限（默认 2，标定）+ 计失败轮
  Act/Policy 越界照拒；UnknownOutcome 强制重观察（§24.4，无选择权）
执法：非白名单响应零 Effect（004-A2 等价）；bounded-stop 仅预算兜底
反射在前（Kernel ≤3 聚焦复查）深思在后（Agent）
```

### M2 三层观察阶梯 + 参数化 Defer

```text
① 摘要（现状）
② 局部视图（新增，默认形态）：六族全上——局部 claims 全量(值+判定+冲突)
   / 元素(边界/状态/认识论) / 容器结构 / 视口 / 观察侧诊断(XML在否/fast在否
   /入账提案数/各段耗时) / 近N周期趋势
   纪律 = 无重复冗余 + 结构清晰（owner Q2 裁决：质量优先于准入门槛，
   对 ADR-0011 逐字段证据准入的授权偏离，显式记录）；后续按使用精简
   派生 = World Model 侧 owner-derived（沿 RUN-005 PolicyEvaluationView
   先例：read-only/ephemeral/not persisted/not authority），Kernel 只执法
③ 截图（能力门控，最后手段）：context 附着（不做工具，不碰 AGT-001
   allowlist——F9 消解）；三道机械闸：
   a) 载荷强制 epistemic=judgment 标记
   b) Owner 入账门拒收该来源（与错屏丢弃门同构，可单测证明）
   c) trace 只记"看过"不记采信
   约束：截图判断永不驱动超既有 Grant 的弹窗处置，越界=暂停+重授权(47)
   能力三层 fail-closed 矩阵（§6）；新协议字段入 AGT-001 F5 钉扎
   上下文携带历史事件/轨迹引用（owner Q3 补充）
Defer 参数化 {目标, 深度(普通/聚焦/深), 轮数, 目的}；越贵越晚用
```

### M3 Slow 参与闭环

```text
调度序：高置信度优先 → 低置信度跳过（不阻塞）→ 当前容器可做项耗尽
        且剩余目标必须等待或滚动 → 同步等待 / 滚动
材料不足谓词（机械可判，盲审核实的既有 trace 字段）：
  hierarchyAvailable=false ∨ proposalCount=0
  （"可grounding元素=0"与 proposalCount 同义，收编；pass1-8 对照后冻结）
预算：≥ boundedWaitMs（同源引用）且 ≤ 本周期剩余
不豁免：等待期间 Unaligned 错屏丢弃照常（F14）
观测：slow 令牌带耗时；003 run 产物补入库
```

### M4 弹窗 policy（任务型白名单）

```text
product/policy/ 工件（新 face，PRF-004，owner 指派）+ 任务分型：
  遍历型   ：无 confirm 权限；{cancel, dismiss, back} 直点
  定向修改型：目标流程自身 confirm 放行
  通用兜底 ：需点 confirm → 问 owner 一次
硬边界：白名单外 confirm 唯一出路 = 暂停+重新授权（不变量 47/46，
        不是 Agent 处置，不是咨询一次再选——F7）
旧词表 → 禁手清单（只判不许做什么）；Agent 不可用兜底 = {dismiss, back}
白名单 author = owner only（Kernel never invents/repairs Policy 红线）
```

### M5 no-progress 进上下文

```text
停滞 =（新发现 + 新完成验证）双零 持续窗口 W（任务参数；只看新发现会
       在合法长验证段误报——自我对抗修正）
辅助 = 同 route 重复计数（阈值与 W 同批标定：pass5 病态 90+/96 vs
       合法重入 repeatedEntries=1）
纪律 = Host 只供事实计数、禁判语（protocol §3:112 判定职责在 Agent）
处置 = 先注入咨询上下文（Agent 改道建议）→ 更高阈值机械硬停兜底
       （bounded-stop 同族；LangGraph recursion_limit 同构——见 §3）
```

### M6 UniAgent 地位上升（Q7 修订版）

```text
原则（v1 即生效）：低维执行面处理不了的一切 → 默认升级 UniAgent；
  异常容错判断权归 UniAgent；编排限硬约束内（typed 词汇/预算/policy
  执法/无直接效应权）——"编排者归位"（高维声明式编排 vs 低维机械执行）
v1（本 change）= M1-M5 落地即成
v2 = 正常时刻 frontier 自主编排 + Director 退为执法+供数
     （独立 change + v1 运行数据背书；方向性声明 only）
```

## 3. 业界对照（定稿版；完整取证表见 grill-2，此处结论 + 关键先例）

| 机制 | 通行做法关键先例 | 我们的定位 |
|---|---|---|
| M1 | MCP elicitation 三值动作集（accept/decline/cancel，协议层封死动作空间）；ACP request_permission 必填 options 菜单 | **同构移植+扩展**：有界选项集有直接协议先例；差异（给模型而非人）需叙事已补（§3.2.1 维度化） |
| M2 | Playwright MCP 默认给 accessibility snapshot、截图显式升级；MCP audience/priority 注解 | 分层通行；"judgment 级不入 ledger"无明文先例 = **自有增强**，执法靠三道机械闸而非承诺 |
| M3 | MCP progress notifications + 异步响应；MCP 要求 client 实现工具超时 | **可辩护取舍**：bound 内退化为同步、bound 外回落通行模式；有界等待与超时要求同向 |
| M4 | Playwright 默认 host 自动 dismiss 弹窗（明文）；Selenium accept/dismiss 双原语；Codex/Claude Code host 层拦危险动作 | **证据最强项**：与 Playwright 默认同构；confirm 禁按白名单是可控增强 |
| M5 | LangGraph recursion_limit 硬停；各家停止条件；无"注入上下文"先例 | **最弱项**：方向更温和（先注入后硬停兜底 = 与主流硬停兼容），叙事节见 §4 |
| M6 | Anthropic workflows(确定性) vs agents(模型驱动)二分；Codex/Claude Code 模型驱动+host 机械执法 | **位置一致**：执法线在 host/Kernel；差异 = 双层切分与异常时编排权回交，维度化叙事（§4） |

## 4. 两节叙事（grill 方法要求补齐）

**M5 为何"注入"优于纯硬停**：硬停把信息扔掉（LangGraph 抛异常中止），
注入把判断材料交给有上下文的决策者（UniAgent），硬停退居兜底——
误报成本从"错误终止"降为"多一段上下文"；且与预算执法兼容（注入不
替代 bounded-stop，只是更早、更便宜的一道闸）。

**M6 维度化编排（v0.1 §3.2.1 保留 + Q7 修订）**：不是"给 Agent 放权"
而是"编排者归位"——摸底期 Director 预嚼指令 = 低维执行面越权代行高维
编排；Q7 修订把原则提前到 v1：处理不了的一切默认上交（谓词清单强制，
Director 无裁量），但编排永远在硬约束内（选项集/预算/policy/无直接
效应权）。与业界"模型驱动 + host 机械执法"共识形态一致；独有差异 =
Kernel/Agent 双层与异常时刻回交，锚点 = Anthropic workflows-vs-agents
二分（Kernel=确定性层，Agent=自由度层）。

## 5. 机制 ↔ 冻结面 ↔ 修订通道（定稿表；与 spec 同步）

| 机制 | 冲突的已定约束 | 修订通道 | 证据 |
|---|---|---|---|
| M1 Defer-only 放宽 | 004「只准同号 Defer」+ A2 | Host change（继承 A2 零 Effect） | pass5 96x + pass6-8 |
| M1 blocked 成员 | RUN-004 D3/D7 字段集 | 独立修订 change（G4 DEFERRED 先例） | P1 死局无表达 |
| M2-② 局部视图 | RUN-004 D3 字段集（AgentDecisionContext） | 同上；派生沿 RUN-005 PolicyEvaluationView 先例（不触 RUN-005） | R5 无视野 |
| M2-③ 截图 | 协议上下文形状 + 能力声明 | 同上（context 附着，不碰 AGT-001 allowlist） | P1 + 业界 audience 注解先例 |
| M2 参数化 Defer | RUN-004 D7 ObserveSpec 形状 | 同上；V5 上限执法不变；三本账取最严 | — |
| M3 同步等待 | PER-019「slow 不阻塞周期」 | 定向 amendment（仅材料不足谓词口） | 002 trace 派发零落地（4×Dispatched/2×Dropped/0×Landed，文件行号见 spec 冻结面表）+ L53-58 预算错配 |
| M4 弹窗 policy | AGT-009 词表（button1） | policy 新工件 + Host 默认值变更 | Config.cs:86 |
| M5 进度字段 | RUN-004 D3（ConsultationProgress） | 独立修订 change（同 M2 通道） | P5/P7 |

## 6. 能力门控 fail-closed 矩阵（M2③；六格逐格写死）

| profile 声明 | 宿主绑定 | 协议门控 | 结果 |
|---|---|---|---|
| 无 | 任意 | 任意 | 拒（覆盖全部反向组合：无+已绑、无+放开等——绑定与协议门控**都不得反向创造能力**） |
| 有 | 未绑 | 任意 | 拒（诚实 NotConfigured，required=false 先例） |
| 有 | 已绑 | 未放开 | 拒（协议不许可，绑而不用） |
| 有 | 已绑 | 放开 | 允许（唯一放行格） |

2×2×2 = 8 种组合全部有落点；上表 4 行经通配覆盖全部 8 格。

新协议字段（触发词/标记）入 AGT-001 F5 handshake 钉扎（protocolVersion/
schemaVersion/schemaHash 之外新增钉扎项，随 Slice B 落地）。

## 7. 十裁决记录

见 changes/AGT-019/spec.md Decisions 表（owner 五条半：Q2/Q3/Q4/Q5/Q7；
Leader 受权五条：Q1/A1/Q6/Q8/R1；自我对抗修正三处已并入）。

## 8. 落地编队（grill 后定稿）

1. **Slice A（Host 层 + 声明面同步）**：M1 强制升级 + 选项集执法（含
   004-A2 继承）+ M3 调度序/同步等待/令牌耗时 + M4 policy 工件 + 任务
   分型 + M5 双零窗口/硬停兜底 + 观测性；同步落 tasks/settings-
   coverage.yaml 参数（滚动上限/停滞窗口/弹窗分型）与 policy 工件
   （声明面不落后于实现）；标定切片在数字冻结前完成。
2. **Slice B（协议修订）**：RUN-004 D3/D7 独立修订——blocked 成员 /
   局部视图字段 / 参数化 ObserveSpec / 进度字段 / 白名单随动；F5 钉扎
   新字段；握手与 payload-shapes hash 重钉。
3. **Slice C（方向性）**：M6 v2 分期表记录，不实现。
4. 可选合并：ScriptedUniAgent 相位感知改造与 RUN-005 切片 C 协同。
5. 验证：全量 solution 测试 + 场景库 C8 搭乘认证 + coverage 工具 +
   （环境可用时）真机异常路径留证（Acceptance 1-6 对表）。
