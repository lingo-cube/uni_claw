# WORKBENCH-LAYOUT-001 — 对话主区与按需详情布局
lifecycle_state: closed · disposition: none · depth: standard · base: bce0ec3a981a4598b889bb0bf37d2b3e389aa41c

## Intent（WHAT/WHY）
按用户批准的布局建议，将解决过程作为默认主区，Trace 独立切换，证据/元数据/节点详情按需打开；改善窄屏导航、阅读位置和焦点体验。

执行目录：`/Users/fran/.codex/worktrees/workbench-conversation-layout/uni_claw`（已附加 worktree）；共享 checkout 中的本文件作为同一 Change 的恢复记录。

## Scope / Out of Scope
- Workspace renderer、UI Controller/ViewModel、browser 接线、布局/阅读状态、样式、相关测试、生成 client 与 README；新增本 Change 与独立 evidence。
- 保留上轮 P0 的理由/回执分离和在途读取合并；不修改产品协议、Runtime、决策提交、设备动作、launch 请求与 Host 服务配置，不重启当前运行服务。
- 不引入前端框架、模型流式协议或新的产品控制权限；所有新增选择和详情均为只读 UI 状态。

## Decisions
- Controller 管理主视图/详情选择；renderer 只消费读模型；DOM 阅读与焦点保存在 UI 层，不成为产品事实。
- 默认主视图为 conversation，Trace 独立切换；右侧详情默认收起，桌面并排、窄屏抽屉。
- 点击对话查看已有请求/决策/回执原始字段；点击 Trace 查看既有节点；Evidence 继续走受控 detail resolver。
- 顶部保留任务标题、真实来源状态与原有发起/刷新入口；不把查询 ready 当 Runtime completed。
- 沿用既有 Controller/ViewModel/renderer/browser public seams 做行为验证；不新增 Capability Registry entry。

## Acceptance
- A1：默认对话占主区；Trace 可独立切换；Metadata 不常驻；按需面板可关闭。
- A2：对话/Trace/证据选择展示对应记录，来源/错误/缺失可见；切任务关闭旧详情，迟到读取不污染新任务。
- A3：打开/关闭详情、刷新、视图切换及任务往返保留阅读位置；底部跟随、上翻提示新内容；输入与焦点不会被整体挂载丢失。
- A4：桌面和窄屏无整体横向溢出；导航/详情抽屉可键盘关闭、焦点返回；现有任务发起与查询回归通过。
- A5：静态 client 同步构建，相关测试通过；隔离浏览器实测并检查像素画面；当前服务与真实设备不受影响。

## Constraints
修改前保存当前原文（包含 P0 dirty edits），证据在 evidence/workbench-layout-20261021/entry.json 与 before/；不清理、不回退任何并行改动。

## Verification
verification:
  level: DETERMINISTIC
  method: public seams 的 RED→GREEN；Web+DSH 测试；隔离 fixture 的浏览器交互、响应式尺寸和截图审查
  expected: 新布局与阅读行为符合 A1–A5，任务执行接口不变
  actual: 隔离实现已通过 164/164 测试、构建/语法检查、浏览器交互与桌面/390px 窄屏截图审查；最终包 hash 与浏览器一致，launch 块不变。共享入口多次被并行覆盖，尚未集成回共享 checkout 或更新运行 profile，故不宣称 CLOSED。
  evidence: 隔离执行目录下 evidence/workbench-layout-20261021/verification.json、review.md、layout-only.patch 和浏览器证据

## Status log
- 2026-10-21 · understanding→resolved→persisted→planned：用户批准布局修改；核对现有 P0 与 Workspace Authority，保存并行工作区原文，明确只改 UI。
- 2026-10-21 · planned→implemented：对话主区/独立 Trace/按需详情、阅读锚点/焦点恢复、窄屏抽屉实现；新增 Controller/renderer 行为测试先 RED 后 GREEN。
- 2026-10-21 · implemented→reviewed：核对只读 UI 与 launch 不变；修正 Trace 展开保持、legacy timeline 无详情来源时不显示无效入口；修正窄屏截图需等待导航过渡完成。
- 2026-10-21 · reviewed→verified：164/164 通过，最终 client 与浏览器 hash 一致；桌面选中详情、任务往返/刷新 scrollTop=1100、连续键盘输入、390×844 抽屉/焦点/无横向溢出及像素图均核验。
- 2026-10-21 · disposition none→blocked：共享 browser entry 至少三次被并行恢复为入口 P0 原文；转入隔离 worktree 并恢复共享 checkout 中本轮修改。隔离实现已验证，尚未合回/部署。恢复条件：共享文件写入归属稳定后应用 layout-only.patch、重跑回归并验证真实 profile 挂载；无需重新设计布局。

- 2026-10-22: verified/blocked -> closed/none: user authorized the next integration step. Exact entry baseline was unchanged; merged approved layout/cards into the shared checkout with recovery snapshots. Real installed product profile and existing-session browser path verified on owned 3082, 167/167 tests; see shared evidence/workbench-integration-20261022/verification.json. Active 3081 was preserved, its loaded client is not claimed updated. Earlier blocked entries describe history.
