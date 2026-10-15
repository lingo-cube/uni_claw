# AGT-019 — Agent 异常升级与观察机制

lifecycle_state: implementing · review_state: **grilled-decision-complete** · disposition: none · depth: decision-heavy · base: 7b49038d

## Status

**Grill 完成，spec 定稿，实现就绪**。三轨 grill（2026-10-17）：

- owner 主审：Q2/Q3/Q4/Q5/Q7（Q7 为修订版——UniAgent 地位上升，处理
  不了的一切默认升级 UniAgent，异常容错判断权归 UniAgent，编排限硬
  约束内；推翻 Leader 初判）；
- Leader 受权决策（owner 委托 + 自我对抗一轮）：Q1/A1/Q6/Q8/R1，修正
  三处（滚动上限参数化 / 停滞双零定义 / 观察预算三账取最严）；
- fresh-subagent 盲审（glm-5.3-flash）：14 发现全处置，其中 F8 通道
  张冠李戴（真冻结面 = RUN-004 D3/D7，非 consultation-protocol 草稿）、
  F4 触发词失效、F5/F7 口号式设防均已按裁决改设计；F10 证据已补锚
  （002 trace 4×Dispatched/2×Dropped/0×Landed，行号入 spec 冻结面表）；
- 业界对照（glm-5.3-flash，带源 URL）：M1/M4/M6 与通行一致（MCP
  elicitation/ACP options/Playwright 默认 dismiss/Anthropic 二分），
  M2/M3/M5 为有意取舍，M5 补"注入 vs 硬停"叙事节。

下一步 = plan 步骤 3：实现编队 Slice A（Host 层 + 声明面同步）展开。

## 来源证据

SETTINGS-COVERAGE-002/003/004 全遍历实测（2026-10-14/15）+ 2026-10-16
owner 系列讨论 + 2026-10-17 三轨 grill（记录：本目录 grill-1 盲审 /
grill-2 业界对照）。

## Verification

```yaml
verification:
  level: CONTRACT
  method: 三轨 grill 交叉对表（owner 主审 rounds + fresh-subagent 盲审 +
    业界对照取证）+ 冻结面/红线/工件纪律逐项核对（盲审产出四张表）+
    证据补锚（git 工作区实文件行号）
  expected: 十裁决齐备且归属可溯；14 发现全处置（改设计/补叙事/裁决/补证四类）；
    修订通道全部指向真实冻结面；红线执法从口号变机械闸
  actual: spec 定稿（Decisions/Acceptance/冻结面表含 revision 动作）；
    设计稿 v0.2（v0.1 superseded）；F10 补锚完成
  evidence: grill-1-blind-review.md + grill-2-industry-compare.md +
    changes/AGT-019/spec.md + docs/design/agent-exception-escalation-and-observation-v0.2.md
```

## Status log

- 2026-10-17 · implementing · **owner 订正 → skip-first 落地**：M3 期"无
  confidence 信号"结论被 owner 订正——置信面即 ElementEpistemic 枚举
  （Observed=双源/Partial=视觉单源/Ambiguous 保留；ConsultationTypes.cs
  "词汇无分数"系有意设计），M3 子代理只搜字面 confidence 故漏判。实现：
  Ledger 五处选目标点改双源优先（NativeRid 在场 = Observed 等价），
  单源跳过不丢弃，OrderBy 稳定保确定性；1 RED→GREEN + 1 守护。
  **遗留（精确已定位）**：Kernel 侧 DeriveElementSummaries 仍硬编码
  Observed（KernelRunDriver.cs L1350；顺带 Enabled:true 同款退化）——
  诚实填值（Native!=null→Observed 否则 Partial）为独立小片，改 Agent
  可见面需过 golden 检查，未在本轮内做。
- 2026-10-17 · implementing · **Slice A 收口**：声明面随动完成（e165e642）
  ——skill guidance 增补弹窗/停滞/未知页处置纪律，manifest revision 2 +
  profile sha256 重钉（推导 = sha256(manifest++guidance)，与 loader
  VerifySkill 一致）；C8 场景库 check PASS（28 文件 0 违规，无需重封）；
  全量 8 工程绿。**标定切片准备就绪**：M5 初值 3/5 + 滚动上限默认 2 已
  作为任务参数在 yaml 可调；标定需真机跑 pass 型 run（stagnation 计数
  + slow ms= 令牌即证据面），冻结呈报 owner。待 owner 三项：skip-first
  confidence 面决策、标定数字冻结、Slice B（blocked 协议成员）。
- 2026-10-17 · implementing · **Slice A / M3 完成（委派 glm-5.3-flash，
  Leader 审查后提交 c9e61d02）**：UniPerceptionPipeline.WaitAndPoll 有界
  同步等待（材料不足谓词 hierarchyAvailable=false ∨ proposalCount=0，
  既有 trace 字段；预算 = 配置值 ∩ fetch 界剩余；unaligned 门全额执法）；
  全部结算令牌带 |ms= 耗时（P6 闭合）。独立复验全量 1561/1561。
  **边界发现（待 owner 知悉，不阻塞）**：Q4 的"高置信度优先跳过低置信"
  在 Runner/LiveFeed 无实现缝——perception payload 不产 confidence 信号
  （全源码仅 Kernel ConflictResolver 注释提及），排序缝在
  Ledger.NextDirective；实现需先给 perception 加 confidence 面（产品
  决策，候选 Slice B+）。M3 的"耗尽后等待/滚动"部分已落地。
- 2026-10-17 · implementing · **Slice A / M1 + M5 完成（TDD，自主推进）**：
  M1 = 路由不明强制升级给选项集 {再观察, back（配置了才给）, 滚动（容器
  可见才给）}，选项外响应保持 004-A2 零 Effect 安全 Defer（a9074a57）；
  M5 = 双零窗口（新完成验证 + frontier 计数同零才计停滞）注入事实计数
  （禁 Host 判语）+ bounded-stop:no-progress 硬停兜底，保守初值 3/5 入
  bounds yaml，冻结待标定切片（114f3fe1）。M3（Runner 侧：同步等待
  谓词 + slow 令牌耗时）已委派 glm-5.3-flash 子代理进行中（workflow-2）。
  Host.Tests 242/242。
- 2026-10-17 · implementing · **Slice A / M4 完成（TDD）**：弹窗处置任务型
  白名单落地——`product/policy/android-settings-popup-disposition.json`
  新工件（遍历型：denyObstacleTargets 禁手清单 + confirmWhitelist 空 +
  pause-reauthorize）；Config 装载（工件 deny ∪ yaml deny，fail-closed；
  BackDescriptor 可空化 = Q1"缺 backDescriptor 则隐藏"的前置）；
  Director 障碍分支三段：安全词（过禁手清单）→ 返回键 dismiss →
  bounded-stop:popup-confirm-requires-reauthorization（不变量 47）；
  DefaultObstacleTargets 移除 button1。验证：全 solution 8 工程全绿
  （Kernel 887 / Host 225+ / Simulation 188 / …，含 5 个新测试）。
  下一步 = M1 异常强制升级 + 选项集（Director unknown-route 分支改造）。
- 2026-10-17 · designing→implement-ready · 三轨 grill 完成（owner 主审
  + 盲审 + 业界对照，子代理按 owner 指令走 zai-coding-cn/glm-5.3-flash
  显式覆盖）；十裁决（owner 五条：Q2-Q5 + Q7 修订版；Leader 受权五条，
  Q7 修订版推翻 Leader 初判）+ 盲审 14 发现全处置 + F10 补锚 → spec 定稿
  + 设计稿 v0.2。下一步 = Slice A 实现编队（plan 步骤 3）。
- 2026-10-16 · understand→designing · 遍历问题归因 + 机制讨论收敛
  （P1-P7 / M1-M6 / harness 对照 / Q1-Q8 待裁决 / 修正面含 profile 订正）
  → spec/plan/state 落盘，**review_state: draft-not-grilled**。
  下一步 = plan 步骤 1（grill，建议 owner 主审 + 盲审双形态）。
