# WORKBENCH-AGNES-RESEARCH-001 — UniAgent Workbench 借鉴 Agnes 的对话投影与界面设计
lifecycle_state: closed · disposition: none · depth: standard · base: bce0ec3a981a4598b889bb0bf37d2b3e389aa41c

## Intent（WHAT/WHY）
调研当前 UniAgent Workbench 如何借鉴 Agnes Harness 的对话投影、同步、消息呈现与工作台布局，产出有源码依据的取舍和后续切片，供产品设计评审使用。

## Scope / Out of Scope
- 只新增本文件、`docs/research/uniagent-workbench-agnes-design-research-20261021.md` 与 `evidence/workbench-agnes-research-20261021/` 下研究证据。
- 当前 UniClaw dirty working tree 为本地事实；Agnes 本地 clean 源码固定到 `98bc11572ba162bcdf6ce2b29612a44508faf951`。
- 只读浏览现有本地工作台；不发起任务、不调用模型/设备、不启动或修改已有服务、不改产品代码/协议/配置、不决定迁移宿主或前端框架。

## Decisions
- 使用 research 技能；背景研究仅产出 `/tmp/agnes-workbench-upstream-20261021.md`，最终报告由主 agent 综合。
- 保留产品事实 Owner：UI 只读投影；Host 会话、产品 Run、现实效果与验证结果分别标识。
- 研究报告为 Research / Authority NONE，不冻结新的接口与架构决定。

## Acceptance
- A1：两边实际调用链、主要源码锚点和版本可追溯。
- A2：覆盖对话投影/流式同步/消息与工具呈现/布局与详情/插件扩展，并区分已有能力和缺口。
- A3：给出可借鉴、需适配、不宜照搬的判断、优先级、最小切片和可判定验收。
- A4：明确静态阅读、测试断言、浏览器观察的证据边界；校验新增文件链接和变更范围。

## Constraints
保留全部既有并行改动；不重置、不清理、不覆盖。遵守现有架构和 Product / Development Harness 分离；不以宿主 tool success 代替 Runtime Outcome 或产品验证。

## Verification
verification:
  level: CONTRACT
  method: 固定上游版本与源码导航核查、研究链接检查、当前工作台只读浏览、前后变更范围核查
  expected: 研究可追溯且建议可验收，只产生声明中的新增研究文件
  actual: 研究报告已完成；29 个本地链接和 19 个固定上游链接核对通过（最终计数见 verification.json）；72/72 现有测试通过；两项探针复现问题；浏览器 DOM 读取成功、截图超时，未作像素验收。研究期间其他并行任务改变既有 diff，未宣称全工作区不变。
  evidence: docs/research/uniagent-workbench-agnes-design-research-20261021.md；evidence/workbench-agnes-research-20261021/verification.json

## Status log
- 2026-10-21 · understanding→resolved：确认用户需要借鉴设计与实现的调研；已有 Agnes 调研不覆盖本轮 Workbench 深入对照。
- 2026-10-21 · resolved→persisted→planned：固定版本、读取现状、记录工作区入口清单；按投影与呈现两条线研究。
- 2026-10-21 · planned→implemented：完成两边调用链、字段丢失、重复读取、上游投影/同步/布局/附件/滚动机制的研究稿及只读探针。
- 2026-10-21 · implemented→reviewed：核对源码与推断边界；纠正“独立 attachments 槽为空就代表没有附件 UI”的局部误读，确认真实 ContentBlock 附件路径；仅记录建议，不实施。
- 2026-10-21 · reviewed→verified：固定上游仍 clean；引用/文件/行号核查通过，现有测试 72/72，两项探针与现有工作台 DOM 观察有证据；截图超时明确保留限制。发现并行任务写入既有文件，不撤销、不宣称全局不变。
- 2026-10-21 · verified→closed：研究范围完成，报告提供 P0/P1/P2 切片和可判定验收；产品实现与设计采纳仍属后续工作。
