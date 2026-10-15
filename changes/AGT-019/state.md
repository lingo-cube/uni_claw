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

- 2026-10-17 · designing→implement-ready · 三轨 grill 完成（owner 主审
  + 盲审 + 业界对照，子代理按 owner 指令走 zai-coding-cn/glm-5.3-flash
  显式覆盖）；十裁决（owner 五条：Q2-Q5 + Q7 修订版；Leader 受权五条，
  Q7 修订版推翻 Leader 初判）+ 盲审 14 发现全处置 + F10 补锚 → spec 定稿
  + 设计稿 v0.2。下一步 = Slice A 实现编队（plan 步骤 3）。
- 2026-10-16 · understand→designing · 遍历问题归因 + 机制讨论收敛
  （P1-P7 / M1-M6 / harness 对照 / Q1-Q8 待裁决 / 修正面含 profile 订正）
  → spec/plan/state 落盘，**review_state: draft-not-grilled**。
  下一步 = plan 步骤 1（grill，建议 owner 主审 + 盲审双形态）。
