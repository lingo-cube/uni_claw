# WORKBENCH-AGNES-P0-001 — 修正决策正文并合并并发详情读取
lifecycle_state: closed · disposition: none · depth: standard · base: bce0ec3a981a4598b889bb0bf37d2b3e389aa41c

## Intent（WHAT/WHY）
用户授权在不影响主流程的前提下推进 Agnes 调研建议。先完成两个已有明确复现的 P0：决策理由被提交回执覆盖；一次刷新对同一 Host Session 重复读取完整详情。

## Scope / Out of Scope
- 修改现有 Workspace ViewModel、DSH browser adapter、相关已有接口测试、同步生成的静态 client 与 Workbench README。
- 只消费已有只读查询；不改产品协议、身份、任务 launch、submit_decision、Kernel/Runtime、Effect/Assurance、服务配置或当前部署。
- 本轮不引入新框架、流式协议、分组算法、插件槽或全新布局；完整 Plan 交互与 Runtime 验证来源治理仍为后续切片。

## Decisions
- 现有 public seams：createWorkspaceViewModel / renderWorkspaceHtml / createCapabilities / WorkspaceQueryCore + Controller；沿用既有边界验证，不引入新产品能力。
- decision 正文由自身内容/理由决定，不回退到 submission result；其他结果卡保留原有回执正文。
- 对相同 Product Session 与实际 Host Session 的在途详情读取去重，完成后立即释放；不长期缓存，不改变独立刷新和权限读取生命周期。
- 各 consumer 保留自身 capability/错误来源；共享的只是同一个 Host 返回结果，不能宣称跨 Owner 原子快照。

## Acceptance
- A1：有 justification 和 accepted/error receipt 时，决策正文仍为理由；没有理由不拿回执冒充决策；提交结果仍单独可见；输入和 HTML 转义保持。
- A2：并发读取同一 session 的 session/timeline/trace/evidence 共享同一次详情响应与读取标记；成功/失败结算后再次读取取得新结果；不同 session 不混用。
- A3：错误保留各 consumer 来源，失败后可重读；任务切换的迟到响应隔离、launch 字段与行为的已有测试通过。
- A4：Web、DSH Workbench 的相关测试与 bundle 构建/语法检查通过；浏览器验证使用隔离 fixtures，不触发真实产品任务或重启用户服务。

## Constraints
既有工作区有并行改动；修改前记录选定文件原文及来源指纹，只写 Scope 内文件。入口快照：evidence/workbench-agnes-p0-20261021/entry.json 与 before/。保持旧协议与现有 Host 查询 API，不把客户端观察时间伪装成 Owner revision。

## Verification
verification:
  level: DETERMINISTIC
  method: 既有 public seams 的 RED→GREEN、Web/DSH 测试、构建/语法检查与隔离浏览器交互
  expected: 决策与回执分离；同一并发读取批次共用结果、结算后重读；主流程命令和 Runtime 边界不变
  actual: 决策理由/回执与非决策结果优先级分别正确；并发详情读取 4→1，下一次刷新重新读取；错误恢复/跨任务迟到隔离通过；Web+DSH 161/161；生成包与语法检查通过；隔离浏览器确认展示与模拟 launch 请求不变。当前服务未重启，未做真实 Runtime 任务或像素验收。
  evidence: evidence/workbench-agnes-p0-20261021/verification.json；review.md；implementation.patch；full-tests.txt；browser-result.json；browser-baseline-result.json

## Status log
- 2026-10-21 · understanding→resolved：复核上一轮研究及当前源码；两个原因已由真实路径探针确定，不重复开展开放式根因调查。
- 2026-10-21 · resolved→persisted→planned：采用现有 ViewModel/renderer/query 接口做小切片；选定文件当前 clean，保存原文和 dirty 工作区清单。
- 2026-10-21 · planned→implemented：两项接口行为各自先 RED 后 GREEN；决策正文按 kind 收紧，adapter 仅合并在途详情请求；README 与静态 client 同步。
- 2026-10-21 · implemented→reviewed：独立区分非 decision 的结果优先级，审查发现初稿扩大影响，新增 RED 断言后纠正；launch/controller 接线与入口原文逐字节相同，见 review.md。
- 2026-10-21 · reviewed→verified：Web+DSH 全部 161 项通过；浏览器在同一 fixture 对照旧/新生成包，详情读取 4→1、决策理由正确，模拟提交字段及一次性提交保持。发现全页重绘替换输入、提交后弹窗未自动收起，旧包亦复现，未混入修复。
- 2026-10-21 · verified→closed：本轮两个 P0 范围完成，无产品 Runtime 修改；当前部署不变，完整 Plan/增量同步/新布局及表单既有问题留后续切片。其他并行工作改变既有 tracked diff，未回退或声明全局不变。
