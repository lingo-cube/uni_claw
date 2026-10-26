# WORLD-VISUALIZATION-001 — World模型运行过程可视化
lifecycle_state: persisted · disposition: none · depth: standard · predecessor: WORLD-UNCERTAINTY-001 (closed 2026-10-23)

## Intent
用户2026-10-23追加：上面任务完成后，实现World模型可视化，清楚展示图模型、Slice、版本、变更与整个流程运作；复用Replay基础但作为独立功能，以此前例子做可视化验收。具体交付见spec.md。不是静态演示稿，须有实际可运行、可检查交互的功能。

## 顺序与授权
先完成WORLD-UNCERTAINTY-001的独立场景/公理/架构审查及核心实现、Review/Verify、CLOSED、负责提交推送，再进入本功能PLAN/IMPLEMENT。当前仅固化需求，不使可视化成为核心模型验收前置。核心实现会话Leader继续负责此独立Change，Leader为用户最新指定gpt-6.1-sol/ultra；普通worker显式gpt-6-luna、fork_turns=none，每会话最多1名，同路径单写者，Git提交推送串行；模型/会话配置留Host执行证据，不进入共享路由/契约。

## Scope / Out of Scope
只读消费World正式图、Slice、来源、版本和变更/影响信息，提供关联查看、时间推进、版本对比、来源追溯与页面表达联动。复用既有Replay可用渲染/读取基础，实际可复用部分以核心交付后代码核实。正式产品不可依赖测试程序集；必要能力经清晰只读缝提取，不让可视化/Replay/Oracle成为世界真值或执行权威。

不增加通用回滚/执行控制台、Goal评价平台、全历史数据库或另一套World模型；不改既定身份/访问/观察/解释/World修订语义。真实图标/字体像素保真不属本期；对整个流程的准确、完整、易理解展示与交互属于本期。

## Acceptance
按spec.md中行为、交互和示例验收表逐项提供method/expected/actual/evidence；实际运行界面并验证交互，保留截图/可复现输入与结果。以WORLD-UNCERTAINTY-001正式交付后的例子及生产链输入为依据，真实录制/受控仿真/离线推演清晰标识，未知/未覆盖/冲突/过期不可渲染成确定当前事实。

## Constraints
共享脏现场先保存负责路径/差异/哈希；不覆盖其他会话、不全仓stage/reset/clean/stash、不force push。核心与可视化两项分别验收、分别提交；本功能完成后同样提交推送并核实远端包含提交。独立工作可并行，依赖与共享写入串行。

## Verification
level: CONTRACT
method: 用户追加要求与前置Change已接受规则交叉核对
expected: 功能范围、前置顺序、示例验收、只读权威与提交推送明确
actual: 需求已固化；功能未实施，未运行产品/浏览器验收，无视觉通过结论
evidence: spec.md；../WORLD-UNCERTAINTY-001/spec.md；../WORLD-UNCERTAINTY-001/implementation-handoff.md

## Status log
- 2026-10-23 PERSIST：用户追加功能已记录，等待前置核心实现完成；在实施会话按顺序继续，不提前启动产品写入。
- 2026-10-23 前置解除+组件方向裁决：WORLD-UNCERTAINTY-001 已 CLOSED（HEAD 56a75fd7，工作区除 .dsh/acl-reports 外干净），deferred disposition 解除。核对 workbench 标准（双入口 vanilla、contract v1、runtime-http/standalone-fixture 适配器先例、打包器仅相对 require）与 G6 分发事实（npm 无 v6，latest 5.1.1）后，用户裁决：组件面向整体系统可组装、图引擎选 G6（按 5.x）、自由布局非树、时间轴/画布手写解耦，详见 frontend-component-decisions.md D1-D5。遗留设计点（节点标签 join、历史图只读缝、数据适配器、diff 粒度、例子映射）留 PLAN 解决；未改产品源码。
- 2026-10-23 RESOLVE 收口（grill-with-docs）：两轮 grill 前沿清空，QV1–QV8 全部 Accepted A（用户两轮"按推荐"，另主动追加缺省功能显式标注要求即 QV7），共同理解达成。裁决全文见 frontend-component-decisions.md"Grill 裁决"节；术语"缺省标注（Deferred Marker）"按 domain-modeling 入 CONTEXT.md Task Workbench 节。→ PLAN：plans/WORLD-VISUALIZATION-001.md（切片 0 只读缝先独立审阅后写 src）。仍未写产品源码。
- 2026-10-23 IMPLEMENT 切片 0 完成（PLAN→IMPLEMENT→VERIFY）：只读缝接口稿 rev.2 独立审阅两轮（rev.1 双 REJECT：历史标签可被后续纠错回填 / 时间线比较键未定致恒真；修订后复审双 APPROVE）→ 实现：ProjectWorldGraph(scope, sourceRevisionId?) + StructuralRelations/NodeLabels（WorldGraphProjection additive init）、ProjectWorldTimeline + WorldRevisionTimeline/Summary/ChangeSummary/WorldNodeLabel、WorldModel.Visualization 派生（纯读取）。新测试 10/10 绿（WorldVisualizationReadSeamTests）；三套件与基线 worktree 对照**零回归**（Kernel 5 环境失败同基线 + 1 负载时序 flaky 单测通过；Host 15 失败集合与基线完全相同；Simulation 313/314 唯一失败同基线）。场景认证 20/41 经 scenario_certify.py --change WORLD-VISUALIZATION-001 重封（check PASS）；ComponentReplay composition pins 显式重认证（独立 oracle 与录制输入未改）；KernelRuntimeSurfaceWhitelist 增集 4 类型。过程事故（Windows CRLF 检出 vs 字节 pins：autocrlf 修正 + 工作树 LF 归一化，内容零变化）与全部四元组见 evidence/slice-0-verification.md。后续：切片 1 fixture 导出、切片 2/3 前端骨架与 G6 vendor。
- 2026-10-23 原型迭代（切片 1 预演 + 交互细化）：prototype/world-viz-export（dotnet 控制台，正式 UniKernel/WorldModel 链 + 切片 0 缝 + ProjectWorldPage 页面表达导出；导航经反射调 internal 登记口，已注释说明）产出 98 revision 演示 session（导航环/Contains/语义建立/沿用/归属纠错全标记真实出现）；prototype/web 节点缩略 = Container 页面表达重绘 + 语义挂钩（用户澄清的需求）。用户指出两处修正后修复：record PascalCase vs JS camelCase 导致默认 revision undefined 静默崩（像素自检抓出）；缩略项坐标过滤。截图三张留 prototype/web。
- 2026-10-23 IMPLEMENT 切片 2 组件层完成（部分）：web/uniclaw-workspace 落正式组件——features/world-viz/{world-viz-view-model, render-world-viz-html, create-world-viz}（D1 两层契约外层：mount/update/dispose/onEvent 展示性事件；QV7 四态与缺省清单进 view-model）、adapters/world-fixture（shape 校验 fail-closed）、styles/world-viz.css（wv- 前缀宿主安全）。测试 10 个（四态/XSS 转义/事件契约/失败态/fail-closed），workspace 全量 113/113 绿。harness 构建脚本（build-world-viz-harness.mjs）内联组件+真实 fixture，node 复放 + 无头 Edge 截图像素验证（216 色、缩略区 2.6%），证据 evidence/slice-2/harness-{final,reuse}.png；期间修 harness loader 相对路径与入口 id 两处缺陷。**切片 2 尾项（未完成，诚实记录）**：DSH 入口接线（host 半部 world 投影方法 + client 模板第二 slot + bundle 重建）留下一轮；G6 vendor（切片 3）未开始。
- 2026-10-23 复杂视图验证（用户指示：找含纠错的历史数据，无则构造）：勘查结论——仓库真实录制（PopupReplay/Recent/settings-coverage-004）均无纠错，纠错仅存于合成测试；按指示扩展导出器为复杂多轮 session（198 revisions、4 容器、导航边 5 含环、Contains×2、双稳定语义、纠错接受×2+拒绝×1，时间线标记：语义 8/沿用 2/导航 5/结构 2）。过程中实证两个纠错涟漪语义（均为产品正确行为，非 bug）：①误身份曾覆盖 home 的 signature claim，纠错撤回后 HomeSig 不再匹配旧容器（"仅由被撤销解释支撑的签名不参与未来匹配"），同页重访改用 display 家族；②Contains 要求两端 signature 唯一对应既有容器，home 签名被撤后以其为父端的第二条 Contains 不成立，改为 display>detail。复杂视图截图 evidence/slice-2/complex-{final,correction2,semantic}.png（harness 组件渲染，274 色/缩略区 5%）；rev-175 为纠错+沿用同 revision 复合态。
- 2026-10-23 缩略图改版 + 真实截图挂载（用户指示：预览换缩略图、可直接用截图缩略）：①节点卡缩略改小（高 64px）并支持双形态——有实拍截图（captureId 精确映射）显示 <img> + "实拍"角标，无截图回退页面表达重绘，详情面板标注来源（"实拍截图"/"页面表达重绘（无实拍）"）；②导出器新增真实录制数据集 data-recorded.js（Recent corpus 6 capture 独立 world、12 revisions、4 容器；2 帧实拍 PNG 精确挂载：default-apps before/after）；③重要教训（诚实记录）：录制段初版混入合成 world，corpus 受控签名被 observed-title-layout 关联策略跨来源误匹配到合成容器——正确结构是会话隔离（独立 world/kernel 双数据集导出）；真实策略对相似标题页的跨页匹配（sims/after≈all-apps/before 同容器）如实保留为 identity 歧义展示素材。测试 113/113 绿；截图 evidence/slice-2/{recorded-shots,thumb-final}.png（359/389 色，实拍照片色彩多样性显著高于纯重绘 216-274 色）。
- 2026-10-23 缺口审计 + B4-1 修复（签名撤回状态落产品缝）：World 模型 gap 审计（对照 world-gap-audit.md + CLOSED 残留 + 当日 grep）结论——四项优先缺口已闭合、无违约项；剩余分层：B1 buyer 延期（Overlays 零生产者、Continuity 无生产实现、生命周期/merge/Goal/Runtime 恢复）、B2 证据边界（Agent provider 封套 diagnostic-only、新鲜窗 3s 未标定、真机未验）、B3 静态风险未复现、B4 表达层新发现。用户确认后修 B4-1：WorldGraphProjection 增 SignatureWithdrawnContainers（additive；DeriveSignatureWithdrawn 于 WorldModel.Visualization——Applied 纠错 RevisionNumber 界定 + PreviousContainer≠Corrected + 选定 revision 无新 signature，历史不回填）；Kernel 新测试 SignatureWithdrawn_AppearsOnLosingContainer_AndHistoricalRevisionStaysClean 一次通过（11/11）；Kernel 全量 1108/1113（5 失败=基线同款环境）；场景认证重封 PASS、ComponentReplay pins 更新 3 处（WorldModel/WorldBeliefRevision/Visualization）、Simulation 313/314（唯一=基线环境）；前端徽章"签名被撤回"+详情说明（后续同签名观察会创建新容器），测试 114/114；导出验证 home/detail 两容器 withdrawn=True、首个 rev-98（纠错时刻）✓。关联修正（撤关系/废语义/滚动置待定）产品层本已实现（Corrections.cs 撤回链），时间轴结构差分可见；导航边撤回在时间轴差分外的表达仍留切片 3。截图 evidence/slice-2/withdrawn-badge.png。
