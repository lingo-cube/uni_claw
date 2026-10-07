# PNL-009 — Workbench 工具面板（消费 ToolInvoke：清单 + run-report 调用）

lifecycle_state: implement · disposition: none · depth: standard · base: working-tree

## Intent（WHAT/WHY）

PNL-008 已在 Runtime Host 侧落地工具投影与调用端点，但 Workbench 无消费面：
用户在 Workspace 看到 run 后无法就地生成全链路报告。本 Change 补齐 Web 侧：
「工具」tab 展示 tool-registry 投影，并对 run-report 提供安全调用入口。

## Scope

- adapter（runtime-http.js）：`ToolInvoke` capability——`listTools`（GET
  /api/uniclaw-runtime/tools）与 `invokeTool(name, {runDir})`（POST，capability
  envelope 形状与既有端点一致）。
- core（index.js）：`listTools`/`invokeTool` 可选 capability 优雅降级
  （TaskCommand 同款：缺失 → unavailable 错误，不抛异常）。
- controller：`tools`/`report` 状态 + `loadTools`（幂等防重入）/`setToolsRunDir`
  /`generateReport`（action token 竞态防护）；selectPane 允许 `tools`。
- view-model：`toolsPane` 投影（清单 items + runDir + report 结果）；
  dual-entry 键集合契约同步。
- renderer：「工具」tab + renderToolsPane（工具表：名称/说明/调用形态/姿态/
  状态；runDir 输入 + 生成按钮；结果区显示 exitCode、report.json/md 相对
  路径、失败 stderr 尾部）。
- entry.js：`generate-report` 分发、runDir input 监听、tools tab 首次选中
  懒加载。
- 测试：adapter 2 例（envelope + 降级）、controller 2 例（全流程状态机 +
  无 runDir/无 capability 边界）；浏览器 bundle 重建。

## Out of Scope

- model-procedure 工具执行与模型路由（run-diagnosis 仍 planned）。
- runId→runDir 自动解析（RuntimeRunStore 无该字段；P2 finalize 自动产出
  报告后自然消解，届时 runDir 输入退化为兜底）。
- 诊断入口占位按钮启用、工具权限审批、外部副作用工具。

## Decisions

1. ToolInvoke 是可选 capability：standalone fixture host 未提供时面板降级
   显示 unavailable，不阻塞其余 Workspace 功能（与 TaskCommand 先例一致）。
2. runDir 由用户输入 runs 根下单段目录名（PNL-008 的安全边界在 Host 侧二次
   执法）；UI 侧不做路径猜测。
3. 工具失败（exitCode≠0）按业务数据渲染（结果区 + stderr 尾部），不弹
   全局错误。
4. 懒加载：tools tab 首次选中才 loadTools，不进启动路径。

## Alternatives rejected

- 启动即拉取工具清单：工具面非核心路径，懒加载降低 standalone/fixture
  模式的负担。
- UI 侧从 runs 列表推导 runDir：store 无映射字段，推导即伪造（PNL-003
  纪律），留给 P2。

## Owner / Authority impact

- 无新增 authority：Workbench 仍为只读投影 + 显式用户触发的工具调用；
  工具真相仍在 tool-registry（PNL-007），执行边界仍在 Runtime Host（PNL-008）。

## Acceptance

1. adapter/core：ToolInvoke envelope 与降级行为正确。
   - method: node --test tests/runtime-http-adapter.test.js
   - expected: 2 个新用例 pass
   - actual: pass（全文件 0 fail）
   - evidence: verification 段命令 1
2. controller：工具面板状态机全流程（加载→输入→切换 tab→生成→结果）
   与边界（无 runDir no-op、无 capability 报 unavailable）。
   - method: node --test tests/workspace-controller.test.js
   - expected: 2 个新用例 pass
   - actual: pass（全文件 0 fail）
   - evidence: verification 段命令 2
3. 全量 web 测试与 dual-entry 契约同步。
   - method: npm test
   - expected: 全部 pass
   - actual: 83/83 pass
   - evidence: verification 段命令 3
4. 浏览器 bundle 重建（entry/renderer 变更后）。
   - method: npm run build:browser
   - actual: generated 7 shared modules
   - evidence: verification 段命令 4

## verification（2026-10-06，CONTRACT/DETERMINISTIC 级）

```text
1. node --test tests/runtime-http-adapter.test.js → fail 0（含 2 个 ToolInvoke 用例）
2. node --test tests/workspace-controller.test.js → fail 0（含 2 个工具面板用例）
3. npm test → 83/83 pass
4. npm run build:browser → generated 7 shared modules
```

## Status

- 2026-10-06 IMPLEMENT：Web 五层接线 + 4 用例 + bundle 重建。待 REVIEW。
