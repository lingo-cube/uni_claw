# PNL-003 Plan — Workspace 契约到第一条可运行切片

状态：IMPLEMENT；Leader：当前会话；Worker：Luna。

## 目标

沿着一条可验证的垂直切片，把设计变成可替换的工程缝：

```text
schema contract → Query Core → DSH Adapter → standalone Adapter → shared frontend
```

每一步都必须保持 Product Runtime、Host Session、来源存储和通用前端的 Owner/Authority 边界。

## 第一项：Workspace Contract Foundation

### Worker 负责

Luna 只负责 `schemas/workspace/` 的第一版语言无关契约、示例和确定性校验工具：

- RecordEnvelope：`kind/source/authority/productSessionId/correlationId/detailRef` 等已冻结字段。
- Query capability：Task/Session/Trace/Evidence/Detail 五类窄能力及版本。
- Query error：`unavailable/uncorrelated/stale/permission-denied/not-found/timeout` 等可观察错误。
- README：字段 Owner、版本策略、禁止把 DSH session 当 ProductSession 的规则。
- 正例/反例和 `tools/validate-workspace-schemas.py`。

### 状态

已完成并通过 Leader Review/Verify；证据：`evidence/PNL-003-WI-PNL003-001.md`。

## 第二项：Host-neutral Query Core

### Worker 负责

Luna 只实现纯 Query Core，不接 DSH、React、DOM、HTTP 或文件系统：

- 建立 `web/uniclaw-workspace` 的独立 package 和 `src/core/`。
- 通过注入的 Task/Session/Trace/Evidence/Detail capability 组合只读查询。
- 输出稳定的 TaskInstance、Session、Conversation Timeline、Trace、Evidence 和 Detail 读模型。
- 保留 `productSessionId`、来源、authority、correlation、snapshot/revision 和局部 QueryError。
- 对未关联、stale、timeout、permission-denied、not-found、unavailable 做显式投影；不折叠成空数据。
- 提供纯内存 fixture 测试，验证来源替换、重复读取幂等、旧快照响应不能覆盖新快照。

### 状态

已完成并通过 Leader Review/Verify；证据：`evidence/PNL-003-WI-PNL003-002.md`。

## 第三项：DSH Adapter（当前）

WorkItem：`workitems/WI-PNL003-003.json`。

只把现有 DSH workbench 的读取能力映射到五类 capability；不改变 DSH HTTP/typert 旧 API，不把 DSH source shape 泄漏进 Query Core，不接 UI 改造。Adapter 必须通过显式 `ProductSessionId → dshSessionId` resolver 读取 session，不能把任意 DSH session 升级为产品任务。

### 验收标准

1. 导出 `createDshWorkspaceCapabilities({ panel, resolveProductSession, now, revision })`，只消费注入能力，不读取 DSH globals、文件系统、网络或 Product Runtime。
2. 输出 `TaskQuery`、`SessionQuery`、`TraceQuery`、`EvidenceQuery`、`DetailQuery` 五个只读 capability；成功响应带 `snapshotId`、`revision`、`observedAt` 至少一项快照元数据。
3. `TaskQuery` 保留未关联实例标记；没有显式 `productSessionId` 的 DSH/observed 实例不能生成 ProductSession。
4. Session/Timeline/Trace/Evidence/Detail 的映射缺失统一返回 `uncorrelated`；不得静默使用传入的 DSH `sessionId`。
5. 现有 conversation、dshTrace、uniclawTrace、uniflowTrace、evidence 能进入稳定 capability data，并保留来源分类。
6. Detail 仅通过注入的 `panel.artifact` 读取受控相对引用；绝对路径、协议 URL、路径 traversal 和无 ProductSession 映射请求均拒绝。
7. 覆盖成功、未关联、局部失败、旧 revision/快照元数据、detail 拒绝和 DSH session 不升级为 ProductSession 的确定性测试。
8. `npm test`、`node --check`、`git diff --check` 通过，变更只在 WorkItem scope。

### 状态

已完成并通过 Leader Review/Verify；证据：`evidence/PNL-003-WI-PNL003-003.md`。

### Worker 禁止

- 不改共享 Query Core、Workspace schema、Product Runtime、`CONTEXT.md`、ADR 和架构方案。
- 不改 DSH 现有 HTTP/typert/client API；不接 React、样式或 UI。
- 不直接读取文件、网络、DSH globals、Typert、sessionController 或 Host transport；只消费注入的 panel 能力。
- 不新增 Product authority、写操作、缓存/归档/删除接口；不把 DSH session 当 ProductSession。
- 如果 Query Core 或 schema 需要新增领域字段或改变 capability 形状，停止并报告 `UPSTREAM_DESIGN_CONFLICT`。

## 后续垂直切片

1. Leader Review Contract Foundation，确认 schema 与设计决策一致。
2. Query Core：消费 schema，提供只读分页、关联、snapshot/revision 和局部失败读模型。
3. DSH Adapter：把现有 workbench 的 workspace/session/trace/evidence/artifact 映射到 capability（当前 WorkItem）。
4. Capability Response Contract：闭合 capability schema 对成功/失败/快照响应的声明后，再进入 standalone adapter。
5. Standalone Adapter：使用固定 fixture 读取同一契约，证明不依赖 DSH（当前 WorkItem）；HTTP/认证另立边界。
6. Shared Frontend 功能状态层：项目列表 → TaskInstance → Uni-Agent 时间线 → Trace/Evidence 详情的选择、刷新和局部错误状态（当前 WorkItem）。
7. Shared Frontend View Model：将功能状态投影为导航、任务卡片、请求→决策→结果时间线、Trace/Evidence/Metadata panes（当前 WorkItem）。
8. Shared Frontend Renderer：View Model → 语义 HTML + 独立 styles，验证任务导航、Uni-Agent 时间线和 Trace/Evidence panes（当前 WorkItem）。
9. Shared Frontend Host-neutral App wrapper：组装 Query Core、Controller、View Model、Renderer，通过注入 mount 复用（当前 WorkItem）。
10. Standalone Host wrapper：装配 fixture、Query Core、App 和 mount，验证独立入口生命周期（当前 WorkItem）。
11. DSH Host wrapper：将现有 DSH panel 映射接入同一 App（当前 WorkItem）。
12. 双入口与失败路径验证：DSH、独立 Web、未关联、超时、权限拒绝、stale、Host 重启复用。

## 第四项：Capability Response Contract（当前）

WorkItem：`workitems/WI-PNL003-004.json`。

第三项 Review 发现 `capability.schema.json` 当前只声明 envelope 控制字段，未声明 Query Core/DSH Adapter 已使用的 `data/error/revision/observedAt`。本项只闭合语言无关契约和验证样例，不改运行时实现；完成后才进入 standalone adapter。

### 验收标准

1. 成功响应 `ok=true` 且含 `data`，失败响应 `ok=false` 且含结构化 `error`；两者保留 capability、版本和 requestId。
2. 响应至少含 `snapshotId`、`revision`、`observedAt` 之一，不能以空值伪造快照。
3. `nextCursor`、局部 `errors`、`productSessionId` 和 capability-specific data 有明确可选形状；未知关键字段继续 fail closed。
4. 新增成功/失败正例与 malformed/unknown-field 反例；validator 能报告 JSON path。
5. 不改变 Query Core、DSH Adapter、DSH 旧 API 或 Product Runtime。

### 状态

已完成并通过 Leader Review/Verify；证据：`evidence/PNL-003-WI-PNL003-004.md`。下一步进入 standalone adapter。

## 第五项：Standalone Fixture Adapter（当前）

WorkItem：`workitems/WI-PNL003-005.json`。

用纯内存 fixture 提供五类 capability，让同一 Query Core 完成第一条只读主流程。当前不引入 HTTP、认证、文件系统或 UI，先验证来源替换与失败语义。

### 验收标准

1. `createStandaloneFixtureCapabilities({ fixture, now, revision })` 提供五类 capability，不依赖 DSH、React、DOM、fetch、文件系统或 Host globals。
2. 输出可被 capability schema 校验的成功/失败 envelope，读取结果带 revision 或 observedAt。
3. 同一 Query Core 完成项目→任务实例→session→timeline→trace/evidence→detail，ProductSessionId 保持一致。
4. 缺失 session/detail、权限拒绝、stale 显式返回 QueryError；重复读取幂等，fixture 不被修改。
5. 测试覆盖来源替换、旧 revision 拒绝、受控 detail ref 和无 DSH 依赖。

### 状态

已完成并通过 Leader Review/Verify；证据：`evidence/PNL-003-WI-PNL003-005.md`。下一步进入共享前端功能层。

## 第六项：Shared Frontend 功能状态层（当前）

WorkItem：`workitems/WI-PNL003-006.json`。

只实现 Query Core 到渲染器之间的 Host-neutral 状态编排，不实现 React、DOM、CSS 或 DSH client。功能层维护项目/任务选择、Uni-Agent timeline、来源分类 trace/evidence、detail lazy load、刷新和局部错误。

### 验收标准

1. `createWorkspaceController({ queryCore })` 提供项目、任务、session、timeline、trace、evidence、detail、refresh 只读 action。
2. 各状态区保留 loading/ready/partial/error、数据、错误和 snapshot metadata；错误不变成空成功。
3. 任务切换只使用 ProductSessionId；旧响应不能覆盖当前选择或更新 revision。
4. 测试覆盖主流程、局部失败、任务切换、旧响应、detail lazy load 和无 Host/React/DOM 依赖。

### 状态

已完成并通过 Leader Review/Verify；证据：`evidence/PNL-003-WI-PNL003-006.md`。下一步进入共享前端渲染层。

## 第七项：Shared Frontend View Model（当前）

WorkItem：`workitems/WI-PNL003-007.json`。

先建立纯函数 View Model 接口，把 Controller state 投影为渲染器可消费的结构。它负责信息层级与来源分组，不负责 React、DOM、CSS 或 Host 挂载。

### 验收标准

1. `createWorkspaceViewModel(state, options)` 不读取、不修改输入状态，也不调用 Query Core。
2. 输出 navigation、taskHeader、conversationTimeline、tracePane、evidencePane、metadataPane、detailActions 和 notices/status。
3. 时间线按 request、decision、result 分组；主线显示 Uni-Agent 摘要，原始数据只作为详情引用。
4. Trace 按 source 分组，Evidence 保留 detail action、权限拒绝、缺失和未关联状态。
5. 局部错误、partial、stale、permission-denied、uncorrelated 显式保留。
6. 测试覆盖完整投影、输入不可变和无 React/DOM/DSH 依赖。

### 状态

已完成并通过 Leader Review/Verify；证据：`evidence/PNL-003-WI-PNL003-007.md`。下一步进入具体渲染器和样式层。

## 第八项：Shared Frontend HTML Renderer（当前）

WorkItem：`workitems/WI-PNL003-008.json`。

实现第一个 Host-neutral renderer：View Model 转语义 HTML，样式单独落在 `src/styles/`。本项不接 React、DSH 挂载或浏览器状态，只冻结可复用的渲染输出和交互 data attributes。

### 验收标准

1. `renderWorkspaceHtml(viewModel, options)` 只消费 View Model，不调用 Query Core、adapter 或 Host API。
2. 输出 navigation、task header、request→decision→result timeline、trace/evidence/metadata panes、detail action 和 notices。
3. 未关联任务、局部错误和禁用 Detail action 保持可见；可用 detail 使用受控 `data-detail-ref`。
4. 用户文本、属性和引用经过 HTML escaping；renderer 不写入 DSH session、绝对路径或任意 URL。
5. 样式独立维护 token、布局、卡片、timeline、pane、状态色和响应式规则。

### 状态

已完成并通过 Leader Review/Verify；证据：`evidence/PNL-003-WI-PNL003-008.md`。下一步进入 Host wrapper 与浏览器场景验收。

## 第九项：Host-neutral App Wrapper（当前）

WorkItem：`workitems/WI-PNL003-009.json`。

组装 Query Core、Controller、View Model 和 Renderer，通过注入 `mount` 输出 `{html, viewModel, state}`。本项不接 DOM、React、DSH transport 或认证，先固定入口生命周期与依赖替换契约。

### 验收标准

1. `createWorkspaceApp` 支持依赖注入并提供 start、stop、getController、getState。
2. start/stop 幂等，mount 更新来自同一 Controller snapshot，停止后不再更新。
3. Query、renderer、mount 错误可观察，不静默成功。
4. Standalone fixture + Query Core 可完成启动、项目加载和任务选择。
5. 不依赖 DOM、React、DSH、Host globals、fetch 或 fs/path。

### 状态

已完成并通过 Leader Review/Verify；证据：`evidence/PNL-003-WI-PNL003-009.md`。下一步进入真实 Web/DSH Host wrapper 与浏览器场景验收。

## 第十项：Standalone Host Wrapper（当前）

WorkItem：`workitems/WI-PNL003-010.json`。

装配 standalone fixture、Query Core、App wrapper 和注入 mount，验证独立入口可以复用完整通用前端链路；本项不接 DOM、网络或 DSH。

### 验收标准

1. `createStandaloneWorkspaceHost` 提供 start、stop、getApp、getController、getState、getError。
2. 启动、项目加载、ProductSession task 选择和 stop→start 都保持 identity 与 fixture 不变。
3. mount 接收 `{html, viewModel, state}`；错误继续通过 App wrapper 暴露。
4. 不依赖 DSH、DOM、React、Host globals、fetch、fs/path 或网络。

### 状态

已完成并通过 Leader Review/Verify；证据：`evidence/PNL-003-WI-PNL003-010.md`。下一步进入 DSH Host wrapper 与真实浏览器场景。

## 第十一项：DSH Host Wrapper

WorkItem：`workitems/WI-PNL003-011.json`。

把已有 DSH panel adapter 接入同一 Workspace App。DSH 只负责 Host 装配、显式 ProductSession 映射和 mount，不改旧 HTTP/typert/client API，也不把 DSH session 语义带入共享前端。

### 验收标准

1. `createDshWorkspaceHost` 组装 DSH capability、Query Core 和通用 App。
2. fake DSH panel 可完成项目、任务、session、timeline、trace、evidence、detail 主流程。
3. 映射缺失返回 `uncorrelated`，错误和 restart 生命周期可观察。
4. mount View Model 与公共 Host API 不暴露 DSH sessionId。
5. 不修改旧 DSH API、Product Runtime 或共享 web 代码。

### 状态

已完成并通过 Leader Review/Verify；证据：`evidence/PNL-003-WI-PNL003-011.md`。下一步进入 Standalone/DSH 双入口运行与浏览器场景验收。

## 第十二项：双入口与失败路径验收

WorkItem：`workitems/WI-PNL003-012.json`。

在不改变共享前端和现有 DSH API 的前提下，用同一组验收场景对 Standalone 与 DSH Host 进行对照：主流程、局部失败、未关联、stale、权限/超时、Host restart，以及 DSH identity 脱敏。契约测试提供可复现证据，浏览器截图只证明实际呈现。

### 验收标准

1. 两个 Host 都完成项目→任务→session→timeline→trace/evidence→detail 主流程，并输出同一组 mount 区域。
2. 局部错误、未关联和 stale 保持可见，成功数据不被伪装为空。
3. stop→start 可恢复且 ProductSessionId 不变；DSH public state/viewModel 不暴露 DSH sessionId。
4. 运行证据可复现，截图不替代契约、错误和依赖边界验证。

### 发现与状态

双入口契约测试曾暴露 Query Core 的失败 envelope 元数据丢失；该问题由第十三项修复后，双入口验收已完成。证据：`evidence/PNL-003-WI-PNL003-012.md`。

## 第十三项：Query Core 失败 envelope 元数据

WorkItem：`workitems/WI-PNL003-013.json`。

只修共享 Query Core 的错误归一化，保留失败 capability、source 和响应快照元数据，避免前端无法定位错误来自哪类 capability 或哪个 revision。成功路径与 Host adapter 不在本项范围。

### 状态

已完成并通过 Leader Review/Verify；证据：`evidence/PNL-003-WI-PNL003-013.md`。下一步处理浏览器呈现中发现的 DSH 项目任务重复展示，再进入最终交付验收。

## 第十四项：导航任务卡去重

WorkItem：`workitems/WI-PNL003-014.json`。

修复 DSH 入口中 project task cards 与当前 task list 的重复渲染，只在 renderer 导航投影处去重，不改变 View Model、Query Core 或来源数据。

### 状态

已完成并通过 Leader Review/Verify；证据：`evidence/PNL-003-WI-PNL003-014.md`。PNL-003 的双入口、失败路径和浏览器呈现验收完成。

## 第十五项：真实 DSH Static Client 挂载缝审计

WorkItem：`workitems/WI-PNL003-015.json`。

当前 Host wrapper 和浏览器静态预览已完成，但真实 DSH `client.js` 仍是 classic bundle，通用 Workspace 是 CommonJS source；先审计最小 bundle/bridge seam，避免通过复制 renderer 形成第二套前端。审计完成后再实现实际静态 Client 挂载。

### 状态

审计完成但判定 `UPSTREAM_DESIGN_CONFLICT`；证据：`evidence/PNL-003-WI-PNL003-015.md`。当前不能把 Node Host wrapper 直接塞进浏览器，也不能把 DSH client 里的第二套 React renderer 当作 shared Workspace。下一步应实现显式 browser bridge/generated bundle，再做真实 DSH 重启与浏览器验收。

## 第十六项：Browser Bridge / Generated Bundle（当前）

WorkItem：`workitems/WI-PNL003-016.json`。

按第十五项审计结论实现 browser-compatible shared bundle，让 DSH static Client 只承担 loader、RPC descriptor 和挂载适配；完成离线 bundle、profile restart 与浏览器主流程验收后，才能宣称真实 DSH 入口接入完成。

## 第一项验收标准

1. `python3 tools/validate-workspace-schemas.py` 通过，校验所有 Workspace schema 和 examples。
2. 正例覆盖 RecordEnvelope、五类 capability 和每种 Query error；反例至少覆盖缺少 `productSessionId`、未声明版本、未知关键字段和未授权 capability。
3. 所有公共 schema 有稳定 `$id`、明确 `schemaVersion`/契约版本和 `additionalProperties: false`；非关键字段可向后兼容。
4. 共享 schema 不包含 DSH import、DSH RPC、文件绝对路径或 Product Runtime 写操作。
5. 校验失败能指出文件、JSON path 和原因；不会把错误样例当作通过。
6. `git diff --check` 通过，Worker 变更只在 WorkItem scope 内。

## Leader 验收动作

Worker 返回后，Leader 按以下顺序处理：

1. 检查 WorkItem scope、forbidden 和 frozen decisions。
2. 运行 schema validator 和 `git diff --check`。
3. 对照架构文档逐字段 review Owner/Authority、版本、错误语义和 DSH 解耦。
4. 若出现语义冲突，退回 RESOLVE，不在 Worker 结果上自行绕过。
5. 通过后再派发 Query Core；未通过不进入 UI 或 Adapter 实现。
