# AGT-019 — Agent 异常升级与观察机制（Exception Escalation & Observation）

> **状态：GRILLED / DECISION-COMPLETE（2026-10-17）**。owner 主审（Q2-Q5、
> Q7 修订版）+ Leader 受权决策（Q1/A1/Q6/Q8/R1，含自我对抗修正）+ 盲审
> 14 发现全处置 + 业界对照落地。本文为实现依据（定稿）。
> Grill 记录：`grill-1-blind-review.md` · `grill-2-industry-compare.md`
> 设计稿：`docs/design/agent-exception-escalation-and-observation-v0.2.md`
> （v0.1 已 superseded，仅存档）

## Intent（WHAT/WHY）

全遍历实测（SETTINGS-COVERAGE-002/003/004，2026-10-14/15）暴露的系统性
病根：**出事的时候，该做主的角色不在场**（P1-P7，见设计稿 §1）。

本 change 落地修正面（M1-M6 定稿形态），并把 **UniAgent 地位上升**写为
v1 原则（Q7 修订版裁决）：低维执行面（Director/Kernel/Host）机械处理
不了的一切（异常、容错、恢复路线选择）**默认升级 UniAgent 判断**，
Director 不再有处理不了情形的终局裁量；UniAgent 的编排只在硬约束内
（typed 词汇、预算、policy 执法、无直接效应权）。Kernel/Host 保留全部
执法与低维执行。

## Scope（机制定稿）

- **M1 升级默认反转 + 异常选项集**：卡死判定 = 机械谓词清单（路由不明 /
  材料不足 / 验证失败 / 白名单外弹窗 / 停滞双零——与设计稿 §M1 及
  Acceptance 1 同一清单），谓词命中 → Host **强制**升级 UniAgent（Director
  无裁量空间）；选项集 {再观察, back, blocked+原因, 滚动}：
  - back = Act + task 级 backDescriptor 作目标描述符（协议不动；任务配置
    缺 backDescriptor 则该选项隐藏，宁可少给不瞎给）；
  - blocked+原因 = 回答 union 新增最小成员（RUN-004 D3/D7 修订通道）；
  - 滚动 = 谓词门控 + 任务参数上限（默认 2，标定后冻结）+ 计失败咨询轮；
  - Act/Policy 越界照拒（幻影目标防线）；UnknownOutcome 强制重观察不动；
  - 004-A2 等价执法继承：非白名单响应零 Effect；
  - bounded-stop 保留为预算耗尽后的机械兜底，不是判断。
- **M2 三层观察阶梯 + 参数化 Defer**：①摘要（现状）②局部视图（新增，
  六族字段全上：局部 claims 全量 / 元素 / 容器结构 / 视口 / 诊断 / 趋势
  ——owner 裁决质量优先，纪律 = 无重复冗余 + 结构清晰，后续按实际使用
  精简；显式记录：趋势族无既有读取证据，属 owner 授权偏离 ADR-0011
  准入纪律）；派生归属 = World Model 侧（owner-derived，沿 RUN-005
  PolicyEvaluationView 先例形态：read-only / ephemeral / not persisted /
  not authority），Kernel 只执法不加工③截图 = context 附着（不做新
  工具，不碰 AGT-001 allowlist）：三道机械闸——载荷强制
  epistemic=judgment 标记 / Owner 入账门拒收该来源（与错屏丢弃门同构，
  可单测）/ trace 只记"看过"不记采信；截图判断永不驱动超既有 Grant 的
  弹窗处置，越界即暂停+重授权（不变量 47）；能力三层（profile 声明 /
  宿主绑定 / 协议门控）缺一即拒，组合矩阵逐格 fail-closed；协议新字段
  入 AGT-001 F5 handshake 钉扎；上下文携带相关历史事件/轨迹引用
  （owner 补充）。参数化 Defer {目标, 深度(普通/聚焦/深), 轮数, 目的}；
  ObserveSpec 扩展属 RUN-004 D7 白名单类型字段扩展，V5 上限执法不变。
  三本账（请求轮数上限 / 失败咨询计轮 GQ3 / D3 预算合同）同时生效、
  取最严。
- **M3 Slow 参与闭环**：调度序 = 高置信度优先，低置信度先跳过（Director
  规则；Agent 咨询可建议跳过）；当前容器可做项耗尽且剩余目标必须等待
  或滚动 → 同步等待。"材料不足"谓词 = hierarchyAvailable=false ∨
  proposalCount=0（均为盲审核实"逐周期已产出"的既有 trace 字段；pass1-8
  对照验证后冻结；如需第三判据须先证明是独立字段而非 proposalCount
  同义）；等待预算 ≥ boundedWaitMs（同源引用，不复制数值）且
  ≤ 本周期剩余；Unaligned 错屏丢弃不豁免；slow 令牌带耗时；003 run
  产物补入库。
- **M4 弹窗 policy（任务型白名单）**：`product/policy/` 工件 + 任务分型：
  遍历型 = 无 confirm 权限，{cancel, dismiss, back} 直点；定向修改型 =
  目标流程自身 confirm 放行；通用兜底 = 需点 confirm 时问 owner 一次
  确认。白名单外 confirm 唯一出路 = 暂停+重新授权（不变量 47/46）。
  旧词表降级为禁手清单（只判不许做什么，不判做什么）。Agent 不可用
  兜底 = 仅 {dismiss, back}（不猜 cancel 语义）。白名单 author =
  owner only（Agent 永不参与改 policy）。
- **M5 no-progress 进上下文**：停滞 =（新发现 + 新完成验证）**双零**
  持续窗口 W（任务参数，标定后冻结——pass5 病态 90+/96 vs 合法重入
  repeatedEntries=1 标定）；同 route 重复计数为辅助信号；Host 只供事实
  计数、禁判语（protocol §3:112 判定职责在 Agent）；先注入咨询上下文，
  更高阈值机械硬停兜底（bounded-stop 同族）。
- **M6 地位上升（分期）**：v1（本 change）= 异常容错判断权归 UniAgent
  （M1-M5 落地即成）；v2 = 正常时刻 frontier 自主编排 + Director 退为
  执法+供数——独立 change + v1 运行数据背书，此处仅方向性声明。
- **观测性**：slow 令牌耗时字段（现状无——002 trace 令牌实锤）；run
  产物入库纪律。

## 修正面总览（含 revision 动作）

| 层 | 面 | 机制 | revision 动作 |
|---|---|---|---|
| Host 执行 | Director/Config/Ledger/LiveFeed/UniPerceptionPipeline | M1/M3/M4/M5 | 代码变更（Slice A）；无声明面 revision |
| 协议（RUN-004 D3/D7 修订） | blocked 新成员 / 局部视图字段 / 参数化 ObserveSpec / 进度字段 / 白名单随动 +N | M1/M2/M5 | 独立修订 change（本 change，G4 DEFERRED 先例）；Slice B |
| Kernel（浅） | 视图派生落 World Model 侧（owner-derived；Kernel 只执法——grill F13 订正） | M2 | 不动 Kernel 冻结面 |
| 产品声明 | profiles 视觉轴（M2③ 三层链的 profile 腿）**已由 modelRoles 承载关闭**（2026-10-17 owner 裁决 A）：`modelRoles.slow.semantic.visual: required=false` = 诚实未绑眼睛；capabilities 为工具面不加条目（Q3 裁决：附着形态不造工具，且与冻结清单 ProductHeadless 全等核对会 fail-closed）；视觉 buyer 出现时翻 required=true 即本行执行 | M2③ | 零 yaml 改动；本行不再有独立 revision 动作 |
| 产品声明 | prompt/payload-shapes | M1/M2/M4 | payload-shapes hash 重钉 |
| 产品声明 | skill 遍历纪律（M4/M5 判断纪律） | M4/M5 | SKILL revision+hash 更新，skills-lock.json 随动 |
| 产品声明 | tasks/settings-coverage.yaml = M3/M4/M5 任务参数真源（滚动上限 / 停滞窗口 / 弹窗任务分型） | M3/M4/M5 | task yaml 变更经本 change |
| 产品声明 | policy 工件（M4 新面） | M4 | 新 face 登记（PRF-004），owner 指派 |
| 宿主绑定 | .dsh/product/（能力→多模态模型） | M2③ | adapter 绑定文件随动 |

分层纪律：profile 声明"有没有眼睛"（host-neutral）→ 宿主面定"用哪双"
→ 协议面定"何时允许看"；任一层缺失 = fail-closed 拒绝（组合矩阵逐格
写死，见设计稿 §6）。

## 涉及的已冻结面（修订通道定稿）

| 冻结面 | 修订性质 | 证据 |
|---|---|---|
| SETTINGS-COVERAGE-004「只准同号 Defer」+ A2 保护 | Host change 翻案（显式继承 A2 零 Effect 执法） | pass5 96x 空转 + pass6-8 bounded-stop:unknown-page |
| PER-019「slow 不阻塞周期」 | 定向 amendment（仅"材料不足"谓词口） | 派发零落地：evidence/settings-coverage-002/run-20261014-170444-227/settings-trace.json L300/L326/L924 + run-20261014-162142-145/settings-trace.json L586/L612/L924（合计 4×Dispatched：2×SemanticUnclear + 2×NoXml；2×Dropped\|unaligned-route；0×Landed；令牌无耗时字段）+ 预算错配 product/tasks/settings-coverage.yaml L53-58 |
| RUN-004 D3/D7 冻结字段集（原误指 consultation-protocol §2.1/D3——grill F8 订正：协议文档 Authority:NONE，真冻结面是 RUN-004 CLOSED 的字段集） | 独立修订 change（G4 DEFERRED 先例）：blocked 成员 / 局部视图与进度字段 / 参数化 ObserveSpec / 白名单条目随动 | R5 三判据分裂时 Agent 无视野 + P1 死局无表达通道 |
| AGT-009 弹窗词表（button1 确认侧） | policy 新工件 + Host 默认值变更（grill F8 补列） | SettingsCoverageConfig.cs:86 DefaultObstacleTargets 含 button1 |
| RUN-005 冻结形状 | **不触碰**：局部视图沿其 PolicyEvaluationView 先例形态新增兄弟视图；ObserveSpec 参数化属 RUN-004 D7 白名单类型扩展，V5 上限执法与 L2 Policy 形态不变 | RUN-005/state.md Owner 决策 2 + Baseline Impact |
| AGT-001 §6.2 allowlist | **不触碰**（M2③ 走 context 附着——grill F9 消解） | — |

## 不动红线（含新增执法）

错屏丢弃门（M3 同步等待不豁免 Unaligned 丢弃）· 判断级/事实级分界（三道
机械闸执法，非口号）· 幻影目标防线（Act/Policy 需可 grounding；back
目标 = backDescriptor）· UnknownOutcome 强制重观察 · 失败咨询计轮
（GQ3；参数化 Defer 计轮）· 三判据分离 · Kernel never invents/repairs
Policy · baseline §24 全节（不变量 47：白名单外 confirm 与截图超
Grant 判断 = 暂停+重新授权）· 004-A2 等价（非白名单响应零 Effect）。

## Out of Scope

Memory · Recovery/Agent Continuation（GQ2 DEFER 族）· 跨进程 resume ·
slow.visual 接线（无 buyer）· multi-agent · 产品 baseline 修改 ·
M6 v2（正常时刻自主编排——方向性声明 only，独立 change 承接）。

## Decisions（grill 十裁决，2026-10-17）

| # | 裁决 | 归属 |
|---|---|---|
| Q1 | back=Act+backDescriptor（配置缺失则隐藏）；blocked=RUN-004 通道新成员；M1 通道如实拆分（Host 部分 + 协议部分） | Leader 受权（owner 未异议） |
| A1 | 滚动谓词门控+任务参数上限（默认 2，标定）；计失败轮；返回不算进展；清单外零 Effect | Leader 受权 |
| Q2 | 局部视图六族全上（含趋势）；纪律=无重复冗余+结构清晰；后续按使用精简；显式记录对 ADR-0011 准入的 owner 授权偏离 | **owner** |
| Q3 | 截图 context 附着（不碰 allowlist）；三道机械闸；历史/轨迹引用进上下文；不驱动超 Grant 处置；三层缺一即拒；新字段入 F5 钉扎 | **owner** |
| Q4 | 高置信度优先、低置信度跳过；耗尽且必须等待/滚动时才等/滚；材料不足谓词；预算同源；不豁免丢弃 | **owner** |
| Q5 | 任务型白名单：遍历型无 confirm（cancel/back 直点）；定向修改型放行目标流程 confirm；按需问 owner 兜底；白名单 author=owner | **owner** |
| Q6 | 双零窗口停滞 + 重复计数辅助；阈值标定后冻结；只供事实禁判语；先注入后硬停兜底 | Leader 受权 |
| Q7 | **UniAgent 地位上升**：处理不了的一切默认升级 UniAgent，异常容错判断权归 UniAgent，编排限硬约束内；v2 后置独立 gate | **owner（修订版，推翻 Leader 初判）** |
| Q8 | 深度三档；按"发起者+预算归属"划界；Kernel 反射≤3 聚焦不计轮；Agent 请求带参计轮；三本账同时生效取最严 | Leader 受权 |
| R1 | 四项修正执行：通道改指 RUN-004 / 证据补锚（已锚，见冻结面表）/ 修正面加 revision 列（已加）/ 三处一句话补丁（已落各机制文本） | Leader 受权 |

自我对抗修正三处：滚动上限参数化（A1）· 停滞定义加"新完成验证也为零"
（Q6）· 观察预算三账取最严（Q8）。

## Acceptance（定稿）

1. 异常路径（identity-only / 材料不足 / 白名单外弹窗 / 停滞双零）每条
   有机械可证的**强制升级**记录 + 选项集执法记录 + 非白名单响应零
   Effect 记录（P3 闭合；004-A2 等价执法）；
2. 材料不足谓词命中时 Slow 至少一次 Landed 且令牌带耗时；谓词对
   pass1-8 trace 逐条对照表（P2/P6 闭合）；
3. 遍历型任务全程零 confirm 侧点击；白名单外 confirm 演练路径 = 暂停
   +重授权记录（P4 闭合）；
4. 停滞双零注入咨询上下文且有改道案例；硬停兜底阈值可达、与
   bounded-stop 同族（P5/P7 闭合）；
5. 判断级执法链单测：judgment 标记载荷被入账门拒收；trace 只含"看过"
   不含采信（判断级/事实级红线闭合）；
6. 标定切片（滚动上限 / 停滞窗口 / 重复阈值）完成后数字才冻结；全量
   solution 回归 + 场景库 C8 随动认证零未授权改动。
