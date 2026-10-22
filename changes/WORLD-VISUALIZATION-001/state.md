# WORLD-VISUALIZATION-001 — World模型运行过程可视化
lifecycle_state: persisted · disposition: deferred_until_predecessor_closed · depth: standard · predecessor: WORLD-UNCERTAINTY-001

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
