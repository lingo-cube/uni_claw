# WORKBENCH-CARDS-001 — 对话卡片与工具展示
lifecycle_state: closed · disposition: none · depth: standard · base: bce0ec3a981a4598b889bb0bf37d2b3e389aa41c

## Intent
借鉴 Agnes 实际源码中的轻量工具行、摘要/详情层级、弱化元信息，改善现有独立 Workbench 的消息与工具展示。

## Scope / Out of Scope
仅 renderer、样式、相关测试、生成 client 和说明；执行目录为当前已附加的隔离 worktree。保留既有对话/详情/报告/诊断入口，不改来源投影、Controller、Runtime、执行与提交语义；不改共享 checkout 或当前运行服务。

## Decisions
- 参考 Agnes 98bc11572ba162bcdf6ce2b29612a44508faf951 的 packages/web-ui/src/conversation/messages.tsx:ConversationToolCard 与 packages/web/public/style.css。采用呈现层次，不引入 React 或其生命周期判断。
- 工具正文短摘要优先；长内容/JSON 按需展开，错误正文始终可见；仅显示来源状态，不推断执行成功。
- 工具目录改窄屏列表卡片，调用形态/姿态按需展开。保留原报告与诊断门控。
- 原生 details 使用现有阅读状态缝保存展开，图标内置 SVG 且正文统一转义。

## Acceptance
- A1：消息角色、决策步骤和回执视觉层级清晰；工具元信息与详情可展开，刷新保持展开与焦点。
- A2：错误正文直接可见，JSON/长回执可完整读取；无伪造工具成功、无 XSS、理由/回执仍分离。
- A3：目录卡片保留名称/说明/状态/调用形态/姿态，报告/诊断操作及门控不变。
- A4：桌面与 390px 浏览器 fixture 无横向溢出，完整测试及生成产物一致。

## Plan
行为测试 RED → renderer/CSS → 测试 GREEN 与生成 → diff review → 浏览器视觉/交互验证 → 记录交付边界。

## Verification
level: SCENARIO（隔离 fixture，非实际 Runtime/部署验收）
method: full Workspace/Workbench tests, bundle generation, syntax/whitespace checks, desktop and 390px isolated browser fixtures.
expected: A1-A4 pass, product flow and authority unchanged.
actual: 167/167; retained expansion on refresh; errors visible; no horizontal overflow; catalog keyboard-expandable; browser bundle hash matches final generated client.
evidence: evidence/workbench-cards-20261022/verification.json, review.md, final-tests.txt, screenshots, cards-only.patch.
limit: Catalog uses standalone renderer fixture because DSH lacks ToolInvoke. No actual Runtime/Profile/deployment acceptance claimed.

## Status
- 2026-10-22：已记录当前脏工作区与目标文件快照，开始局部样式修改。共享集成仍属于 WORKBENCH-LAYOUT-001 的待解决事项。

- 2026-10-22: planned -> implemented: message/tool/catalog presentation and 3 behavior tests; product call chain unchanged.
- 2026-10-22: implemented -> reviewed: scoped diff, authority, escaping and command-gate review recorded.
- 2026-10-22: reviewed -> verified: 167/167 tests and browser fixture checks passed; generated bundle hash matched browser response.
- 2026-10-22: verified -> closed: isolated presentation scope complete. Shared integration remains separately blocked under WORKBENCH-LAYOUT-001; no deployment claimed.

- 2026-10-22: follow-up integration completed under WORKBENCH-INTEGRATION-001. Shared source and installed product client updated; actual profile/browser path verified on 3082 without new model/device execution. Earlier isolated-only limits describe the prior delivery.
