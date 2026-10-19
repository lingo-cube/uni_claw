# 咨询输入、慢感知会话与续额修复验收

日期：2026-10-18（Asia/Shanghai）。Change：RUNTIME-CONSULTATION-001。

## 做了什么
修复 Product→DSH 咨询输入和既有预算续额接线，回收内部 Slow 会话，并核验 Slow 返回路径。沿用 UniFlow、diagnosing-bugs/TDD、UniClaw debug evidence 与 DSH plugin development 的职责边界。没有新增 Capability、Effect 权限或生命周期协议。

## 原来的问题与根因
1. 主会话已经复用；增加的是 Slow 请求创建的独立 session。`/slow` 返回后无归档；timeout/abort/detach 也没有停止该 Slow session 的完整路径。
2. 实际 profile 的两行 `promptMount: per-turn` 导致每轮追加完整静态正文。旧探针 `assemble({})` 没有指定作用域，只看到全局 providers，不能证明 preset 子会话是否收到静态指令；该误判留下了长期回退配置。
3. Runner 创建空 AgentPlanPolicy，没有把有限续额上限和阈值接进来。Act/NoAction 的严格 JSON 校验拒绝 renewal；Defer 的 converter/schema 丢失 renewal。此外，Ledger 原来按初始 MaxSteps 停止，会提前截断已经获批的额度。
4. Slow 返回链路并未断开。旧 live-2 的 6 次请求中，1 次投影、5 次因 route 已变化丢弃。一次 `unknown` 路由提案投影后，下一轮决策输入的 routeClaim 出现冲突；不能把这些事实推导成全部遍历循环的唯一根因。见 slow-baseline.json。

## 这次修改
- `dsh/uniclaw-decision-channel/src/index.js`：握手解析实际 Agent，调用 `assemble({scope: agent})`，核对完整 Prompt/Skill 正文；探针返回会话 ID 和名称清单。section 内容缺失时拒绝握手。专用 Product profile 两行切回 section。
- 同一插件 `/slow`：创建前占住请求槽；每次请求继续使用独立 session 上下文、复用任务 workspace；finally 经正式 `workspaceRegistry.archiveSession(id,{stopActivity:true})` 归档并停止活动。成功、无输出、prompt 失败、超时、abort/detach 均收口；归档失败显式返回错误，保留原始 transcript。
- `DecisionJsonConverters.cs` / `ProtocolSchema.cs`：Act/NoAction/Defer 的既有 renewal 字段严格往返，嵌套未知字段仍拒绝；同步单源 schema、TypeScript、插件工件。
- `SettingsCoverageConfig.cs` / `SettingsCoverageRunner.cs`：任务显式声明初始与最终总上限、阈值；最终总上限减初始额度后映射到既有 policy 的“累计追加额度上限”；facts 新增 budgetRenewals 审计投影。
- `SettingsCoverageLedger.cs`：规划停止使用最终总上限；当前可用额度和每笔审批依旧归 Kernel。
- Product Prompt 增补 renewal 形状与申请条件；Prompt/Profile revision 同步为 10。`DshSlowConsult.BuildPrompt` 明确材料不足时 Unknown + 空提案，不猜测路由。未将 semanticDisposition 升为规则授权条件。
- 正式配置保持初始 96 步/112 咨询，最终最多 256 步/320 咨询，触发阈值 20 步/24 咨询。没有启动完整 256 步覆盖运行。

## 验证结果

| method | expected | actual | evidence |
|---|---|---|---|
| ProductProtocolJson/schema 定向 RED→GREEN | 三种决定保留 renewal | 修复前 3/3 失败，修复后 3/3 通过；未知字段仍严格校验 | tests/UniClaw.Agent.Dsh.Tests/ProtocolFoundationTests.cs；.logs/dotnet-final.log |
| 配置→真实 Runner→Kernel 确定性场景 | 获批后跨过初始 4 步，最终不超过 12 步；审计可读取 | 先复现仍停 4 步，再修 Ledger；最终场景与 4 个无效配置用例共 5/5 通过；facts 含 grant records | .logs/audit-red.log、audit-green.log；SettingsCoverageScenarioTests.cs |
| DSH 插件 HTTP/tool 回归 | Slow 成功/失败/超时/中止/分离后回收；缺静态正文拒绝握手 | 38/38 通过 | .logs/plugin-full.log |
| 全量 solution | 既有行为不回归 | 1645/1645 通过；环境开关缺省关闭，不能用此数字代替真实设备证明 | .logs/dotnet-final.log |
| 场景、协议、工件检查 | 同源且无漂移 | 场景认证 28 files/0 violations；覆盖 28/28（20 certified+8 descriptive）；47 invariants；协议生成新鲜；prompt hash、语法与 diff 检查通过 | .logs/certify.log、scenarios.log；source-digests.json |
| 真 DSH 会话作用域回归 | 探针会话 ID 与握手一致、仅含完整 Product/Skill sections | 首次因服务退出连接拒绝；恢复独立专线后 1/1 通过，保留两次记录 | .logs/probe-live.log |
| 真 DSH 握手与模型输入 | 静态正文可见，用户消息不重复追加 | 主会话 1 个、14 次咨询；system/message 内身份 1 次且完整 Skill 存在；14 条 user/message 身份附加次数全为 0 | live-selected.json；原生 DSH session-78dc1794-509b-454a-ab57-ed9b198c39d9 |
| 注册模拟器+真模型有界续额 | 自动申请、审批、额度生效、最终有限 | 初始 4 步/6 咨询，最终上限 12 步/20 咨询；两笔各 +4 步/+4 咨询，后续上下文确认累计 +8/+8；最终 12 次 Effect、14 次咨询，consult-budget-exhausted | preflight.json；bounded-verification.yaml；live-selected.json；live/run-20261017-173444-221/ |
| Slow 真模型调用与归档 | 无材料不编造事实；独立会话回收 | 2 次合成无材料输入的真实模型通道验证，均 Unknown+空提案、2 个 session 均已归档；该项不是设备感知证明 | slow-live.json |
| 同轮真实设备 Slow | 每次结果有去向 | 4 次：2 次 aligned 落成但 proposals=0、1 次错屏丢弃、1 次 Run 结束 detach 后取消；4 个 session 全部已归档 | live-selected.json；同轮 settings-trace.json；宿主 archivedSessionIds 的精确 membership 检查 |

本轮真实运行在新增 facts.budgetRenewals 序列化前构建；审批生效由原生下一轮 renewalWindow 的累计 granted 数值证明。新增审计落盘字段另经 Runner 确定性场景 RED→GREEN 和最终全量套件验证，没有为一个 JSON 投影重复跑设备。

## 新发现与限制
- 有界真实运行覆盖要求完成 25%，status=AgentDecisionFailed、terminal=false，是预算耗尽的未完成结果，不能宣称目标完成或完整设置树覆盖。
- Unknown+空提案是模型输出纪律，本次真实调用遵守了它；没有把 Prompt 当成机械安全执法，也没有改变证据/世界模型 Authority。
- 原有 SETTINGS-COVERAGE-005、Quickstep 唯一性异常、未知页回退及 run-report 摘要适配问题不在本 Change 内，保持已有记录。
- DSH 专用 3081 已更新配置并重载；最终复验曾遇服务退出（连接拒绝），保留 .logs/probe-live-unavailable.log；恢复为独立后台进程后真实探针回归 1/1 通过，PID 见 dsh-service-pid.txt。3080 未操作。借用注册模拟器已有 supervisor，不取得关机所有权。源文件与部署副本有硬链接，复制时已按文件 identity 判断，未清理共享目录。
- 工作区修改未提交、未推送；入口快照及部署配置备份位置见 Change 和 deployment-backup.txt。日志中的既有 NuGet 缓存/编译告警不等同于测试失败。

## 下一步
本提案范围完成。完整设置覆盖仍需独立诊断与验收；不自动启动更长运行。
