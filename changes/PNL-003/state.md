# PNL-003 — UniClaw Workspace 工程化与前端解耦架构
lifecycle_state: implemented · disposition: none · depth: decision-heavy · base: 7b49038d

## Intent（WHAT/WHY）

把现有 DSH 任务工作台提升为 UniClaw 的产品级 Workspace 方案：任务实例与 session 一一对应，界面聚焦 Uni-Agent 的“请求 → 决策 → 结果”，并按来源展示 UniClaw Trace、DSH Trace、元数据和证据。核心前端必须可独立运行，DSH 只能通过 Host Adapter 接入。

原因是当前展示形态仍以 DSH 为中心，数据来源、Host 生命周期和产品语义容易耦合；继续在 DSH 包内堆功能会使独立 Web、其他 Host 和异构数据接入反复重写。

## Scope / Out of Scope

### Scope

- 需求、边界、分层、统一记录形状、查询契约和前端目录组织。
- Task/Session、Conversation、Trace、Evidence、Metadata 的只读投影关系。
- 独立 Web、DSH 和未来 Host 的 adapter 边界。
- 延迟详情、来源/权威标识、关联失败和异构分类规则。

### Out of Scope

- 本 Change 不包含 Product Runtime 写操作、Runtime/Trace 采集或证据存储；只读工程实现已覆盖 schema、Host-neutral Query Core、共享 Workspace 前端与 DSH Host Adapter/static bundle。
- 不改变 Product Runtime 的执行权威、调度、写操作和生命周期。
- 不在本轮决定完整 OTel 平台、实时推送或开放式插件市场。

## Decisions（Rounds 1–5）

- 设计主文档：`docs/design/uniclaw-workspace-architecture-v0.1.md`。
- 数据流采用 source → correlation → normalization → projection → query → render。
- 统一记录保留 `source`、`authority`、`correlationId` 和 `detailRef`，前端只消费读模型。
- Workspace v0.1 是只读观察产品，不包含停止、重试、批准或任务编辑。
- `web/uniclaw-workspace` 是通用前端 canonical source；DSH 只提供 Host Adapter 和静态发布物。
- `TaskInstance` 的身份是 `ProductSessionId`；首版映射一个 primary DSH Host Session。
- 辅助能力不默认命名为 DSH Session：可以来自 DSH 子 session 或外部能力，统一以 `AuxiliaryExecutionRef` 挂在 Product Session 下，不创建新的 TaskInstance/Product Session。
- UniClaw、DSH、Evidence 的事实按来源和权威分开保留；冲突与缺失显式呈现。
- 首版使用带快照时间的查询、显式刷新或有界轮询；实时事件另立协议。
- 新来源统一通过 Adapter → Normalization → Projection → Query 接入。
- metadata 只复用已有 canonical Product Session 值；DSH `sessionId` 保持 Host reference 语义。
- 辅助执行不成为新的一级用户语义域，按实际语义进入既有面板。
- Product Runtime 生成产品语义 ID；无显式 correlation 时显示未关联，禁止静默推断。
- 详情统一经受控 `DetailResolver` 读取；不允许客户端直接访问文件系统或任意外部 URL。
- 独立 Web 与 DSH 共享同一套 TS/React 源码；功能组件、基础 UI 和 styles 分层维护。
- v0.1 只读取已有 UniClaw/DSH Trace；OTel 采集、存储和采样另立 Change。
- 建立 Host-neutral Workspace Query Core；DSH 与独立 Web 只装配来源、权限、传输和挂载适配器。
- 快照记录各来源版本或读取时间；不支持历史读取的来源不得宣称严格快照一致，旧响应不得覆盖新快照。
- Workspace 不建立来源长期副本，不接管缓存、归档、删除和保留；Host/Source 自己管理，DSH 若开放管理必须提供显式 capability/interface。
- 第一工程化切片为项目列表 → 任务实例 → Uni-Agent 时间线 → Trace/Evidence 详情，双入口运行并覆盖失败路径。
- Product Runtime/Task Repository 是 TaskInstance 权威；无 ProductSessionId mapping 的 Host 观察只显示未关联，不自动升级为任务实例。
- Query Core 的 canonical 实现放在通用 Web workspace 的独立 core package；语言无关契约进入 `schemas/workspace/`。
- capability 采用 TaskQuery、SessionQuery、TraceQuery、EvidenceQuery、DetailQuery 窄接口；现有 DSH API 仅作为 adapter 内部兼容层。
- v0.1 reliability contract 冻结为有界超时、可取消读取、幂等刷新、旧响应不覆盖新快照、错误状态分型、Host 重启不创建第二个 Product Task。
- 任务详情使用可识别快照与来源时间/revision；懒加载必须保留一致性声明，不把快照 ID 当作跨来源原子一致性证明。
- 面板和来源局部失败；不可用、未关联、过期、权限拒绝明确区分，不能折叠为空数据。
- Host/Source Adapter 执行身份和来源权限，Query Core 消费受限能力及拒绝结果，前端不拥有权限真相。
- 记录、读模型、详情和能力显式版本化；非关键新增兼容，不兼容关键字段/能力拒绝。
- 验证包括前端契约、核心/适配器确定性测试、两个入口的场景与故障路径证据。

## Technical Validation Frontiers

- 共享 Query Core 的落点与两个 Host 的复用方式。
- 来源不支持历史读取时的跨来源快照、懒加载和刷新失效规则。
- 保留/删除策略归属、快照过期和权限撤销处理。
- 第一个完整工程化切片与双入口、异构来源的验收边界。
- 任务发现与 TaskInstance/ProductSession authority 的事实来源和未关联 session 处理。
- Host capability 接口与 DSH 当前工作台 API 的拆分、版本和迁移方式。
- `schemas/workspace/` 契约落地、DSH adapter 映射、source revision/读取时间、stale 和 failure path 的确定性验证。

## Plan / WorkItems

- Plan：`plans/PNL-003-plan.md`
- Completed WorkItem：`workitems/WI-PNL003-001.json`（Luna Worker；Workspace schema foundation）
- Completed WorkItem：`workitems/WI-PNL003-002.json`（Luna Worker；Host-neutral Query Core）
- Completed WorkItem：`workitems/WI-PNL003-003.json`（Luna Worker；DSH Host Adapter）
- Completed WorkItem：`workitems/WI-PNL003-004.json`（Luna Worker；Capability Response Contract）
- Completed WorkItem：`workitems/WI-PNL003-005.json`（Luna Worker；Standalone Fixture Adapter）
- Completed WorkItem：`workitems/WI-PNL003-006.json`（Luna Worker；Shared Frontend 功能状态层）
- Completed WorkItem：`workitems/WI-PNL003-007.json`（Luna Worker；Shared Frontend View Model）
- Completed WorkItem：`workitems/WI-PNL003-008.json`（Luna Worker；Shared Frontend HTML Renderer）
- Completed WorkItem：`workitems/WI-PNL003-009.json`（Luna Worker；Host-neutral App Wrapper）
- Completed WorkItem：`workitems/WI-PNL003-010.json`（Luna Worker；Standalone Host Wrapper）
- Completed WorkItem：`workitems/WI-PNL003-011.json`（Luna Worker；DSH Host Wrapper）
- Completed WorkItem：`workitems/WI-PNL003-012.json`（Luna Worker；双入口与失败路径验收）
- Completed WorkItem：`workitems/WI-PNL003-013.json`（Luna Worker；Query Core 失败 envelope 元数据）
- Completed WorkItem：`workitems/WI-PNL003-014.json`（Luna Worker；导航任务卡去重）
- Blocked WorkItem：`workitems/WI-PNL003-015.json`（Luna Worker；真实 DSH Static Client 挂载缝审计，已拆出 WI-PNL003-016）
- Completed WorkItem：`workitems/WI-PNL003-016.json`（Luna Worker；Browser Bridge / Generated Bundle）
- Completed evidence：`evidence/PNL-003-WI-PNL003-001.md`
- Completed evidence：`evidence/PNL-003-WI-PNL003-002.md`
- Completed evidence：`evidence/PNL-003-WI-PNL003-011.md`
- Completed evidence：`evidence/PNL-003-WI-PNL003-012.md`
- Completed evidence：`evidence/PNL-003-WI-PNL003-013.md`
- Completed evidence：`evidence/PNL-003-WI-PNL003-014.md`
- Completed evidence：`evidence/PNL-003-WI-PNL003-016.md`

## Assumptions

- 一个 TaskInstance 对应一个 Product Session 和一个 primary DSH Host Session；辅助能力可来自 DSH 或外部系统，但不创建新的 Product identity。
- Uni-Agent 主线是默认信息层级，底层 Trace 是可展开的诊断信息。
- DSH 当前静态客户端能力可以被包在 Host Adapter 内，不向通用前端泄漏。

## Alternatives

- DSH-first 前端：拒绝，因会把 DSH 约束写进产品前端。
- 每个来源一套页签/模型：拒绝，因无法形成一次任务的统一执行事实。
- `getEverything()` 巨型接口：拒绝，因会隐藏权威和失败边界。
- 首版开放式 Registry：延后，等第二种真实渲染变体出现。

## Owner / Authority impact

- Workspace 负责只读组合、投影和可视化，不产生 Product Runtime 事实。
- UniClaw Runtime 负责任务、Agent 语义和 UniClaw Trace 的业务权威。
- DSH Host 负责 DSH session、Host trace、挂载、认证和传输生命周期。
- Evidence/External Trace 的权威需在 Grill 中逐项确认。

## Acceptance

1. 方案文档完整描述需求、边界、架构、前端组织、统一记录和验收标准。
2. 文档明确通用前端不依赖 DSH，并给出独立 Web 与 DSH 的适配路径。
3. 文档列出所有需要 Human Decision 的架构前沿，能直接用于一轮 Grill。
4. 本 Change 不包含 Product Runtime 写操作、Runtime/Trace 采集或证据存储；当前工程实现覆盖 schema、Query Core、共享 Workspace UI 和 DSH Host Adapter 的只读接缝，并受 WorkItem 验收约束。

## Verification

```yaml
verification:
  level: CONTRACT
  method: "阅读 docs/design/uniclaw-workspace-architecture-v0.1.md，并检查 git diff --check 与工作区变更清单"
  expected: "需求、边界、分层、数据契约、目录规则、验收标准和 Human Gates 齐全；无实现代码变更"
  actual: "五轮 Grill 决策已写入架构方案、Change State、glossary 和 ADR；schema foundation、Query Core、DSH Adapter 三个只读切片已分别通过 Leader Review/Verify，最终 UI 与运行时挂载仍未宣称完成"
  evidence: "docs/design/uniclaw-workspace-architecture-v0.1.md；plans/PNL-003-plan.md；evidence/PNL-003-WI-PNL003-001.md；evidence/PNL-003-WI-PNL003-002.md；evidence/PNL-003-WI-PNL003-003.md"
```

## Residual risks

- 如果 Canonical Home 或 Authority Model 在 Grill 中改变，前端目录和查询契约需要回到 RESOLVE 修订。
- 如果首版必须实时展示，当前快照查询边界不足，需要单独定义事件/断线协议。
- 如果证据允许任意文件或外部 URL，Detail Boundary 会成为安全和权限决策前沿，不能由 UI 自行放宽。

## ADR refs

- 待 Grill 后补充；本 Change 当前不宣称已有新 ADR 决策。

## Status log

- 2026-10-02 · UNDERSTAND → RESOLVE · 完成 Workspace 需求、边界、分层、前端解耦和 Human Gates 的 Pre-Grill 草案。
- 2026-10-02 · RESOLVE（Round 1）· 确认只读 Workspace、通用前端 canonical source、ProductSession identity、来源冲突可见、快照优先和 Adapter 接入规则；Host Session cardinality 仍待确认。
- 2026-10-02 · RESOLVE（Round 1）· 确认一个 primary Host Session；DSH 或外部辅助能力统一通过 AuxiliaryExecutionRef 关联，不生成第二个 TaskInstance/Product Session。
- 2026-10-02 · RESOLVE（Round 2）· 确认辅助执行按实际语义进入既有面板、显式 correlation、受控详情读取、同源 TS/React 前端与 Trace 读取范围；进入一致性、失败语义、权限、版本和验证契约裁决。
- 2026-10-02 · RESOLVE（Round 3）· 确认可识别快照、局部失败、来源权限执法、契约版本化与分层验证；继续裁决共享核心落点、跨来源快照可行性、数据生命周期和首个工程化切片。
- 2026-10-02 · RESOLVE（Round 4）· 确认共享 Query Core、来源版本/读取时间快照、Host/Source 管理缓存归档、DSH 通过 capability/interface 暴露能力，以及双入口完整主流程切片。
- 2026-10-02 · RESOLVE（Round 5）· 确认 TaskInstance authority、schemas/workspace 契约真相、窄 capability 迁移和 v0.1 reliability contract；Grill 决策前沿清空，转入 PERSIST/PLAN。
- 2026-10-02 · PERSIST → PLAN · 建立 `plans/PNL-003-plan.md` 与 `WI-PNL003-001`；第一项只实现共享 schema foundation，Leader 负责 Review 后再进入 Query Core。
- 2026-10-02 · PLAN → IMPLEMENT → REVIEW → VERIFY · Luna 完成 Workspace schema foundation；Leader 修正 WorkItem 状态和 DetailRef 相对引用边界，validator、反例和 diff 检查通过；Change 保持 open，下一步进入 Query Core。
- 2026-10-02 · IMPLEMENT · 派发 `WI-PNL003-002`；Query Core 只消费五类 capability，不触碰 DSH、Product Runtime、HTTP 或 UI。
- 2026-10-02 · IMPLEMENT → REVIEW → VERIFY · Query Core 通过 10 个测试、静态依赖扫描、语法检查和 schema 校验；旧 revision 防覆盖、局部失败和稳定 session/timeline 读模型均有证据；下一步进入 DSH Adapter。
- 2026-10-02 · PLAN → IMPLEMENT · 建立 `WI-PNL003-003`；验收冻结为五类 capability 映射、显式 ProductSession→DSH session 关联、受控 Detail 引用、局部失败和快照元数据；派发 Luna 实现，Leader 负责独立 Review/Verify。
- 2026-10-02 · IMPLEMENT → REVIEW · Luna 完成 DSH Adapter；Leader 发现并要求修正 hostSessionRef schema 形状、malformed payload 静默降级和 resolver 异常未结构化问题。
- 2026-10-02 · REVIEW → VERIFY · 修正后 DSH package 38 项测试通过；node --check、schema validator、三个 WorkItem schema 校验、依赖扫描和 git diff --check 通过；DSH Adapter 切片完成，运行时挂载仍作为后续接入边界。
- 2026-10-02 · VERIFY → RESOLVE → PLAN · Leader 发现 capability schema 只声明控制字段，未覆盖实际 data/error/snapshot 响应；建立 `WI-PNL003-004` 闭合契约，完成前不派发 standalone adapter。
- 2026-10-02 · IMPLEMENT → REVIEW → VERIFY · Luna 闭合 capability 成功/失败响应 schema，修正 snapshot 元数据约束为 `anyOf`；validator、WorkItem schema 校验、adapter 实际响应 schema 校验和 diff 检查通过；下一步进入 standalone adapter。
- 2026-10-02 · PLAN → IMPLEMENT · 建立 `WI-PNL003-005`；固定 standalone fixture adapter 的只读主流程、来源替换、失败和无 DSH 依赖验收；派发 Luna，Leader 负责 Review/Verify。
- 2026-10-02 · IMPLEMENT → REVIEW → VERIFY · Standalone fixture adapter 通过 13 项 Web package 测试、node --check、精确依赖扫描、实际响应 schema 校验和 diff 检查；同一 Query Core 已验证 DSH 外来源替换和完整只读主流程。
- 2026-10-02 · PLAN → IMPLEMENT · 建立 `WI-PNL003-006`；固定 Host-neutral 前端功能状态层的选择、局部错误、刷新、detail lazy load 和过期响应验收；派发 Luna，Leader 负责 Review/Verify。
- 2026-10-02 · IMPLEMENT → REVIEW → VERIFY · Workspace controller 通过 20 项 Web package 测试、node --check、精确依赖扫描和 diff 检查；修正任务/详情切换后的旧响应覆盖，并确保 detail 请求携带当前 ProductSessionId；功能状态层完成，下一步进入渲染层。
- 2026-10-02 · PLAN → IMPLEMENT · 建立 `WI-PNL003-007`；冻结 renderer-neutral View Model 的信息层级、来源分组、请求→决策→结果主线和 Detail/Evidence 状态验收；派发 Luna，Leader 负责 Review/Verify。
- 2026-10-02 · IMPLEMENT → REVIEW → VERIFY · View Model 通过 25 项 Web package 测试、node --check、精确依赖扫描和 diff 检查；修正未关联证据带 detailRef 时仍显示按钮的问题；renderer-neutral 投影完成，下一步进入具体渲染器与样式层。
- 2026-10-02 · PLAN → IMPLEMENT · 建立 `WI-PNL003-008`；冻结语义 HTML renderer、独立样式 token、摘要优先、Detail data attribute 和 HTML escaping 验收；派发 Luna，Leader 负责 Review/Verify。
- 2026-10-02 · IMPLEMENT → REVIEW → VERIFY · HTML renderer 通过 29 项 Web package 测试、node --check、精确依赖扫描、CSS 选择器检查和 diff 检查；文本/属性 escaping、摘要优先、未关联与禁用 Detail 状态均有证据；下一步进入 Host wrapper 与浏览器场景验收。
- 2026-10-02 · PLAN → IMPLEMENT · 建立 `WI-PNL003-009`；冻结 App wrapper 的依赖注入、start/stop 生命周期、mount 输出和错误可见性验收；派发 Luna，Leader 负责 Review/Verify。
- 2026-10-02 · IMPLEMENT → REVIEW → VERIFY · App wrapper 通过 36 项 Web package 测试、node --check、精确 Host 依赖扫描和 diff 检查；修正 stop→start 生命周期复用旧 Promise 的问题，并验证 in-flight stop 不再 mount；下一步进入真实 Host wrapper 与浏览器场景。
- 2026-10-02 · PLAN → IMPLEMENT · 建立 `WI-PNL003-010`；冻结 Standalone Host 的装配、入口生命周期、mount 输出、fixture identity 和错误透传验收；派发 Luna，Leader 负责 Review/Verify。
- 2026-10-02 · IMPLEMENT → REVIEW → VERIFY · Standalone Host wrapper 通过 41 项 Web package 测试、node --check、精确 Host 依赖扫描和 diff 检查；启动、任务选择、restart、fixture 不变和错误透传均有证据；下一步进入 DSH Host wrapper 与真实浏览器场景。
- 2026-10-02 · PLAN → IMPLEMENT · 建立 `WI-PNL003-011`；冻结 DSH Host 装配、显式 ProductSession 映射、共享 App 复用、错误与 restart 生命周期验收；派发 Luna，Leader 负责 Review/Verify。
- 2026-10-02 · IMPLEMENT → REVIEW → VERIFY · Luna 完成 DSH Host wrapper；Leader 独立复核 DSH panel → capability → Query Core → App → mount 链路，44 项 DSH 测试、41 项共享 Web 测试、schema validator、语法、依赖边界和 diff 检查通过；mount/public facade 对 DSH session identity 做脱敏，下一步进入双入口运行与浏览器场景验收。
- 2026-10-02 · PLAN → IMPLEMENT · 建立 `WI-PNL003-012`；冻结 Standalone/DSH 同一 App contract、主流程、局部失败、未关联、stale、restart 和 DSH identity 脱敏验收；派发 Luna，Leader 负责独立 Review/Verify。
- 2026-10-02 · IMPLEMENT → REVIEW · 双入口测试由 Luna 完成并通过当前测试集；Leader 发现 Query Core 丢失失败 envelope 的 capability 与 revision/snapshot 元数据，WI-PNL003-012 暂不关闭，拆出 `WI-PNL003-013` 修复共享 Core。
- 2026-10-02 · PLAN → IMPLEMENT · 建立 `WI-PNL003-013`；冻结失败 envelope 元数据保留、错误码不变和成功路径不回归验收；派发 Luna，Leader 负责独立 Review/Verify。
- 2026-10-02 · IMPLEMENT → REVIEW → VERIFY · Query Core 失败 envelope 现在保留 capability、source、snapshotId/revision/observedAt；stale 也保留 incoming metadata；Web 46 项、DSH 47 项测试、schema、语法、边界扫描和 diff 检查通过。双入口验收闭合，浏览器场景发现 DSH 导航存在重复任务卡，下一步单独修正导航投影。
- 2026-10-02 · PLAN → IMPLEMENT · 建立 `WI-PNL003-014`；冻结导航 renderer 去重、无嵌套任务兼容和未关联任务可见验收；派发 Luna，Leader 负责独立 Review/Verify。
- 2026-10-02 · IMPLEMENT → REVIEW → VERIFY · Renderer 按 ProductSessionId/任务 id 去重导航卡；无嵌套任务和未关联任务仍可见；Web 48 项、DSH 47 项测试、schema、语法、依赖扫描和 diff 检查通过，浏览器复验 DSH 左侧重复任务消失。PNL-003 双入口工程化切片完成。
- 2026-10-02 · PLAN → IMPLEMENT · 建立 `WI-PNL003-015`；冻结真实 DSH static Client 的 bundle/bridge seam 审计，要求不复制 shared renderer，派发 Luna 做实现就绪分析，Leader 负责裁决后再进入实际挂载。
- 2026-10-02 · IMPLEMENT → REVIEW · Luna 审计确认 `UPSTREAM_DESIGN_CONFLICT`：现有 DSH classic client 不调用 Node Host wrapper，shared Workspace 是 CommonJS 且没有 browser export；当前 client 还内嵌第二套 React renderer/state/RPC 投影，不能宣称已复用通用前端。下一步需建立显式 browser bridge/generated bundle，保留 shared source 单一真相。
- 2026-10-02 · PLAN → IMPLEMENT · 用户确认上一轮验收无问题并继续；建立 `WI-PNL003-016`，冻结 browser bridge/generated bundle、strict codec、profile restart 和真实浏览器验收，派发 Luna 实现，Leader 负责独立 Review/Verify。
- 2026-10-03 · IMPLEMENT → REVIEW → VERIFY · 完成 `WI-PNL003-016`：修正 DSH Typert `{ok,value:{success,...}}` 解包、观察产物 ProductSession 映射和 browser bridge 事件委托；加入 DSH capability 超时，避免连接断开时永久 loading。Web 55/55、DSH 49/49、bundle build、profile deploy/drift check、node --check 与 diff check 通过。专用 DSH 浏览器已显示真实项目/任务卡并验证 `correlated` 选择；旧实录没有活动 Host session 时按约显示结构化 `DSH session capability timed out`，未伪造对话或 Trace。详细证据见 `evidence/PNL-003-WI-PNL003-016.md`。
- 2026-10-03 · IMPLEMENT → REVIEW → VERIFY · 根据真实浏览器验收优化错误通知交互：相同 code/message 聚合为单一状态栏，显示影响数量和来源，详情使用原生折叠展开，并提供“重试当前任务”；Web 57/57、browser bundle build、profile deploy/drift check 通过。真实 DSH 在 session capability 超时时展示 `4 处` 聚合行，展开后保留四条来源明细，点击重试进入 loading 并回到结构化错误状态。详细证据补充在 `evidence/PNL-003-WI-PNL003-016.md`。
- 2026-10-03 · VERIFY · 将本轮真实数据验收任务、DSH cold-read 修复和工作区边界收敛到当前 `uni-harness` 分支：详情读取改为目标 session header 直读，browser capability timeout 调整为 30 秒；专用 DSH `3083` 重启后真实页面可读 session、trace、evidence、metadata，并按 `AgentDecisionContext` 边界决定是否显示 Uni-Agent 对话。跨 workspace/普通 DSH session 的“暂无对话”与 not-found 结果均保持如实可见，不伪造产品数据。证据见 `evidence/PNL-003-WI-PNL003-016.md`。
- 2026-10-03 · VERIFY → CLOSED candidate · 在专用 DSH `3083` 上完成真实 `uniagent-prod` handshake + consult，实际返回 `act` AgentDecision 并写入 session；Workspace 页面已显示真实“调用方请求 → Uni-Agent 决策 → 提交结果”Round 1，同时保留 DSH Trace/Metadata。真实 session 映射、产品投影和截图证据已补入 `evidence/PNL-003-WI-PNL003-016.md`；运行时 task store 仍由 DSH Host 管理，不写入产品仓库。
- 2026-10-03 · VERIFY → CLOSED candidate · 真实页面复验后收敛信息层级：页签仅保留 Trace/Evidence/详情，Metadata 与单轮执行结果留在右侧摘要栏；DSH adapter 补齐可读 Trace 投影，修复重复裸 `trace` 文本。UniClaw runtime 产物缺失时显示结构化空状态，不把 DSH submission 冒充产品执行结果。Web 58/58、DSH 49/49、bundle build、profile drift check、node --check 与 diff check 通过；证据见 `evidence/PNL-003-WI-PNL003-016.md`。
- 2026-10-03 · VERIFY → CLOSED candidate · 真实 Android API 35 emulator 任务完成 Settings → Network & internet → Internet，Uni-Agent 3 轮真实对话（2 act + 1 noAction），2 次 DeliveryCompleted，产物纳入 `evidence/pnl003-real-task-android-settings-20261003`；建立 Android Settings 与 Workspace 契约两个项目化测试集。
- 2026-10-03 · IMPLEMENT → REVIEW → VERIFY · 修复 shared browser bridge 缺失 `TaskQuery.listTaskInstances`，补齐 UniClaw Trace/Evidence 来源与明细引用；真实页面已显示项目任务、请求→决策→提交、250 条 Trace、5 个 Evidence 文件明细入口、Metadata 和 Completion 执行结果。Web 59/59、DSH 50/50、bundle、deploy drift、语法与 diff 检查通过。
- 2026-10-03 · IMPLEMENT → REVIEW → VERIFY · 根据真实页面复验继续收敛信息层级：观察页签移到任务头部下方；对话采用连续背景容器与分轮卡片；Trace 按 DSH/UniClaw Runtime 分组并预留父子 span 缩进；长 JSON/任务标识在容器内换行或截断；`facts.json`、Evidence 文件和 Trace 明细均可点击；诊断 Skill 只保留禁用的首屏入口，注册与管理延后。浏览器实测 3 轮对话、2 个 Trace 来源组、5 个 Evidence 明细按钮、`facts.json` 执行结果入口，`body/document.scrollWidth=780`（viewport 785，无横向溢出）；renderer 8/8、view-model 15/15、bundle/deploy drift/node check/diff check 通过。
- 2026-10-03 · IMPLEMENT → REVIEW → VERIFY · 根据宽屏与真实 Android Settings 页面复验，移除主内容固定最大宽度；新增可隐藏/展开、可拖拽且持久化的左侧任务栏；将内容区改成主观察列与摘要栏双列，Trace 收敛为单一统一树根，保留来源 chip 与 parentSpanId 子树；窄视口自动堆叠主列/摘要栏。参考 DSH 原生交互与 OpenTelemetry span nesting，未引入 React/MUI 第三方组件，继续保持共享前端 Host-neutral。renderer 8/8、view-model 15/15、browser bundle、DSH drift check、node check 与 diff check 通过。
- 2026-10-03 · IMPLEMENT → REVIEW → VERIFY · 修复对话容器无限增长、详情页无返回、Trace 明细入口藏在折叠分支和 Trace 信息稀疏问题；新增 Trace 合并/按来源切换，DSH span 投影补齐 label/summary/text 与 outcome/references/events，真实 Android Settings 任务验证对话限高、250 节点双模式、详情 ready→返回 Trace；Web 62/62、DSH 50/50、bundle 7 modules、profile drift clean、node check 与 diff check 通过。证据见 `evidence/PNL-003-WI-PNL003-016.md`。
- 2026-10-03 · IMPLEMENT → REVIEW → VERIFY · 根据真实页面反馈重做 Trace 交互：新增全部/DSH/UniClaw 来源切换器，按来源只渲染当前来源，全部来源仅显示可展开摘要；固定轨迹查看器高度并把长内容移入内部滚动；明细改为可滚动遮罩弹窗，支持关闭按钮、背景和返回来源。真实页面验证 DSH 50、UniClaw 200 切换，明细弹窗 ready→关闭返回；Web 62/62、DSH 50/50、bundle 7 modules、drift clean。证据见 `evidence/PNL-003-WI-PNL003-016.md`。
- 2026-10-03 · IMPLEMENT → REVIEW → VERIFY · 根据真实任务复验修复明细展示与设备元数据：弹窗 dialog 使用不透明白底；合法 JSON 的 `text` 解析为嵌套对象，截断/非 JSON 内容拆成元数据与原始文本两个可读区，避免反斜杠字符串包裹；Metadata 提升 `device/androidApi/wmSize/real` 为设备与运行环境卡片。真实 Android Settings 页面验证 `emulator-5556 / Android API 35 / 1080x1920 / real device`、Trace `trace.json` 长详情可读且可关闭返回。Web 64/64、DSH 50/50、bundle 7 modules、profile drift clean、node check 与 diff check 通过；证据见 `evidence/PNL-003-WI-PNL003-016.md`。
- 2026-10-03 · IMPLEMENT → REVIEW → VERIFY · 根据状态截图修复头部徽章错位，`ready/correlated` 均为 32px 高且 top 同值；参考 OTel Span 模型，Trace 增加 traceId/rootSpanId/runId 上下文、已展示/总 spans 计数与节点 status/kind/duration/events/links/attributes 事实。无真实 timing 不伪造 waterfall 数据；共享 renderer 未引入 OTel SDK 或宿主 UI 依赖。真实页面验证 `250/3821` context、UniClaw Completed 节点及双来源切换；Web 65/65、DSH 50/50、bundle 7 modules、profile drift clean、node check 与 diff check 通过，证据见 `evidence/PNL-003-WI-PNL003-016.md`。
- 2026-10-03 · IMPLEMENT → REVIEW → VERIFY · 增加 span 节点级“查看节点”入口与结构化详情弹窗，区分节点检查和 `trace.json` 原始文件读取；UniClaw context 计数修正为来源内 `200/3821`，不混入 DSH 事件；节点详情展示 Trace/Span/Parent、结构结果、events、references、links、attributes/resource（存在才显示）。真实页面点击 `evidence.admit` 已验证弹窗内容和返回 Trace；Web 66/66、DSH 50/50、bundle 7 modules、profile drift clean、node check 与 diff check 通过，证据见 `evidence/PNL-003-WI-PNL003-016.md`。
