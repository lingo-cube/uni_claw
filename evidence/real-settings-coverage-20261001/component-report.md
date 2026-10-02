# AGT-005 组件功效与验证程度总结

## 一、做到什么程度

AGT-005 把 Settings 菜单遍历从「单目标短路径」升级为可配置、有界、可追溯的覆盖遍历，并在专用 DSH 3081 + emulator-5556 上完成了一次真实遍历：**19/19 步全部验证（成功率 100%）、覆盖率 100%（根页 + 7/7 一级入口 + 滚动发现的 Search settings + 8 个二级路由 + 9 次验证返回 + Apps 重复进入）、分歧点 NONE、单一 DSH session、kernel 终态 Completed/Completion、退出码 0**；确定性侧 solution build 0 errors、全量 **1162/1162** 测试通过、**29** 个场景经 AGT-005 重认证 PASS。覆盖完成以 CoverageReport 为准（非 Settings 全树穷举）；结束后清理 3081 与测试设备，3080 存活未受影响。

## 二、组件清单与各自功效

| 组件 | 位置 | 功效 | 验证方式与结果 |
|---|---|---|---|
| settings-coverage.yaml + SettingsCoverageConfig | `.dsh/profiles/settings-coverage.yaml`、`src/UniClaw.Host/SettingsCoverage/SettingsCoverageConfig.cs` | 遍历范围/步数/滚动/目标/终止条件/session 全部配置化；fail-closed 加载校验（缺字段、非法值、重复目标、终止条件全关即拒绝） | 21 个配置单测（缺字段/非法值逐项 fail-closed + 默认 profile 解析）全过；真实运行参数全来自配置（console.log 配置回显） |
| SettingsCoverageLedger | `SettingsCoverageLedger.cs` | 纯逻辑覆盖账本：发现/进入/滚动/返回/重复记账、可见性约束指令引擎（只指挥当前可见目标、离根恒返、滚动候选区分前后）、终止评估、覆盖率报告、首个分歧点（含重复 effect 检测） | 12 个账本单测（含 NextDirective 优先序、未验证滚动不计入）；真实运行 19 步记账正确、报告逐项有证据 |
| SettingsCoverageDirector | `SettingsCoverageDirector.cs` | 包在 ConsultAgent 缝上的计划执法者：directive 注入派生 AgentDecisionContext（协议面零改动）、单步/目标/effect 符合性校验（有界纠正再咨询 MaxDirectiveRetries 次，仍偏离 → null fail closed）、终局 NoAction（coverage-complete / bounded-stop 理由留痕） | 场景测试证明偏离纠正恢复、持续偏离 fail closed、非法 desiredState 拒绝；真实运行 20 轮咨询（19 act + 1 终局 noAction）全部一次通过、零偏离 |
| SettingsCoverageRunner | `SettingsCoverageRunner.cs` | 组合根（单 Kernel run，contract 由配置派生）+ NextInput 交错记账缝（InterleaveBookkeeper：观察/回执按拉取点配对、receipts↔verifications 按派发序、DecisionId 按 (role,descriptor,effect) FIFO 归属）+ coverage-steps.json / coverage-report.json 工件 | 场景测试 5 项（全量使命 19 步 / maxSteps 有界停止 / 纠正 / fail closed / 非法 desiredState）过；真实运行 19 步 trace 逐字段齐备无缺失 |
| Kernel swipe effect | `src/UniClaw.Kernel/Effects/AdbEffectDriver.cs`（AdbEffectDriver/AdbLiveEffectDriver 同源 TryBuildTap） | swipe-up/down 进入 driver 支持集；起止点由 scrollable 容器自身 bounds 派生（上下留 15% 边距、300ms），无坐标魔数 | AdbSwipeEffectTests 6 项（up/down 方向、小容器行程、不支持 effect 拒绝）过；真机 1 次 swipe-up 实际发出 `input swipe 540 1638 540 907 300` |
| scrollable occurrence | `src/UniClaw.Host/UiHierarchyOccurrenceStrategy.cs`（:135-144） | scrollable 容器成为独立可 grounding 目标（role="scrollable"，以 resource-id 为稳定身份，避免嵌套容器文本碰撞），携带 spatial bounds | ScrollableOccurrenceTests 3 项过；真实运行滚动 target=`com.android.settings:id/main_content_scrollable_container` grounding 成功 |
| 滚动验证语义 | `src/UniClaw.Kernel/Assurance/RuntimeAssurance.cs`（:98-109） | swipe 步骤专属 checks：`post-action-route-unchanged` ∧ `post-action-content-transition`——路由变化或内容未变均 fail closed | ScrollVerificationTests 6 项（通过/路由变/内容不变/证据缺失×2/tap 不 incur）过；真机滚动步骤两 checks 通过 |
| PostActionEffectVerification 公开面 | `UniKernel.cs`（PostActionVerifications）、`Assurance/PostActionEffectVerification.cs` | 白名单显式修订为 public，使 runner 能逐步消费验证判定组装 trace（非绕缝） | 由 runner 交错记账缝消费验证：真实 19 步每步 checks 落盘齐备 |
| Host.Dsh --settings-coverage | `src/UniClaw.Host.Dsh/Program.cs` | 真实入口；kernel 终态（end-on-root 义务）与覆盖判定分离，进程退出码取覆盖判定（部分覆盖=3，不伪装成功） | 真实运行 exit 0；bounded-stop 语义由场景测试覆盖 |
| DshOpenedHttpPeer / DshAgentAdapter | `src/UniClaw.Agent.Dsh/` | 容量错误识别→延迟重试→同一 session（既有件，未改动语义）；session 复用（adapter attach 一次，全程单 session） | 既有回归 `Consult_RetriesSelectedModelCapacity_OnSameRequest` + SessionReuseTests 过；真实运行单一 session-c6d36c6b 贯穿 20 轮（容量重试未实际触发，见第六节） |
| SettingsTraversalLiveFeed | `src/UniClaw.Host/SettingsTraversalLiveFeed.cs` | 每周期采集链：真实截图 + Fast 视觉 + hierarchy dump → typed 解析投影 → 世界模型证据源 | 真实运行 20 周期全链可用，见第三节 |
| EvidenceLedger + WorldModel | `src/UniClaw.Host/`（EvidenceLedger、WorldModel 及各 Strategy） | 字段 claim 入账、occurrence 景观、route 信念修正、belief 喂回可见性约束 | 真实运行 23868 spans、11,915 条入账，见第四节 |
| 控制与 Grounding 链 | AgentPlanPolicy → DescriptorTargetPolicy → SelectIntent → ActViaCurrentGrounding → CanonicalBinding → spatial locator → dispatch | TargetSpec 采纳到像素派发的完整 grounding 链 | 真实运行 19/19 步 grounding 零失败，见第四节 |

## 三、感知链：功效与实测

以下均为**真机实测**（evidence/real-settings-coverage-20261001/run-final/settings-trace.json，20 周期 = 1 External 初始 + 19 PostActionEffectFlow；每步 postCaptureId 均落在 coverage-steps.json）：

- **SettingsTraversalLiveFeed**（`src/UniClaw.Host/SettingsTraversalLiveFeed.cs`）：每周期 = 真实截图（AdbScreenshotAcquisition，独立 captureId）+ Fast 视觉（VisionServiceSession YOLO）+ hierarchy dump（UiAutomatorDump.TryDumpToDevice + ProbeStateMachine 退避）。真实运行 20 周期全部完成。
- **Fast 视觉**：20/20 可用，延迟 483–2655ms（中位约 590ms）；按设计只作遥测、永不成为动作目标（AGT-004 冻结约束）。
- **hierarchy**：20/20 可用，每 dump 约 1.95–2.24s，是全部 grounding 的唯一来源。
- **typed 解析与投影**：UiAutomatorDump.ParseHierarchyObservation + TypedHierarchyProposalProjector，每周期 209–820 条 per-node 字段 claim（capture 限定 subject）。
- **路由指纹**：DeriveScreenIdentity 按页标题；真实运行出现 9 个不同路由，其中 Search 页无标题 → 回退身份 "android.settings"（回退路径被真实走到，导航验证仍通过）。
- **坐标空间**：capture 实测 w/h → device-viewport 归一化 bounds → tap 像素投影（真机点击落点正确：Navigate up (73,182)、条目行 (540,y)）。
- **诚实备注**：feed 级 TraceEntry 的 grounding/assurance/verification/slow/effect 遥测字段本轮全为 null（未启用），逐步 checks 实际落在 coverage-steps.json。

## 四、世界模型：功效与实测

以下均为**真机实测**（run-final/trace.json，23868 spans）：

- **EvidenceLedger**：11,915 条 evidence.admit；WorldModel relevance+reconcile 11,896 条（差额 = 被拒/不相关）。
- **UiHierarchyOccurrenceStrategy**：按 capture/node 身份 join 字段 claim → occurrence 景观（条目/滚动容器/Navigate up）；本轮新增 scrollable 角色真机生效。
- **ProductAssociationStrategy**：android.settings 容器身份 + ui.screen.route claims；每次导航 route 值被后续证据修正（revised-claim 处置路径真实走过）。
- **WorldBeliefRevision**：当前 belief 的 occurrences 被 runner 的 InterleaveBookkeeper 每周期读取喂给账本（可见性约束的真实数据来源）。
- **控制与 grounding 链**：AgentPlanPolicy 采纳 TargetSpec → DescriptorTargetPolicy 首个未访问匹配 → SelectIntent → ActViaCurrentGrounding → CanonicalBinding → spatial locator → dispatch；19/19 步 grounding 零失败。
- **world.derive-slice** 38 次（每步 2 次：动作前 grounding + 动作后验证）；**world.resolve-current** 19 次（每步一次权威冲突裁决调用——真实运行零冲突可裁决，空转但语义在场）。
- **Assurance freshness**（5 分钟 evaluator）全程 fresh；post-action 验证每步留 checks。

## 五、是否符合预期（对照任务书验收）

- **可配置** ✅ — 范围/步数/滚动/目标/终止条件全在 `.dsh/profiles/settings-coverage.yaml`，21 个 fail-closed 校验测试证明非法配置被拒。
- **有界** ✅ — maxSteps/maxScrolls/连续失败/咨询轮次均配置执法；场景测试证明 bounded-stop 诚实停止（BoundedStop + uncoveredItems，不伪装完成）。
- **可追溯** ✅ — 19 步真实 trace 逐字段齐备：DecisionId、DSH session、动作前目标唯一性、ADB effect receipt（id/outcome/命令）、动作后 capture、route 前后、Grounding/Assurance/Verification checks。
- **覆盖清单** ✅ — 根页、7/7 可见一级入口、1 个滚动发现入口（Search settings）、8 个二级路由（要求 ≥2）、9 次验证返回、Apps 重复进入（新步骤+新回执），coverage-report.json 全 covered、分歧 NONE。
- **清理** ✅ — emulator 停止、3081 杀掉、3080 全程仅存活探测未动。
- **3080 非干扰** ✅ — 以 401 存活探测在运行前后确认存活；但注意这是探测级确认，非全程监控（见第六节）。

## 六、核心步骤时间线

- 复验 AGT-004 closed 于 7b49038d，确认两步闭环与 route/typed 验证语义已在；设计定形：单 Kernel run + director 包装 ConsultAgent 缝 + 配置化 bounds。
- Kernel 最小扩展：swipe-up/down effect 支持集（bounds 派生命令）、scrollable occurrence 角色、滚动验证（路由未变 ∧ 内容变化 fail-closed）、PostActionEffectVerification 升 public。
- flash 委派实现 config + ledger（fail-closed 校验、记账、指令引擎、终止评估、报告）。
- 实现 director（指令注入/有界纠正/终局 NoAction）与 runner（交错记账缝 + 工件）+ Host.Dsh `--settings-coverage` 入口。
- 白名单显式修订：PostActionEffectVerification 从 internal 升 public 供 runner 消费。
- 场景测试暴露指令引擎缺陷（首版指令未按可见性约束指挥）并修正：只指挥当前可见目标、离根恒返、滚动候选区分滚动前后。
- 全量验证：build 0 errors、1162 测试、29 场景重认证 + coverage 链 PASS；环境检查（专用 3081、emulator-5556、3080 存活）。
- 首次真实运行因 3081 服务被管道 `| head` SIGPIPE 中断，仅完成 1 步（失败尝试证据如实保留于 /tmp/real-settings-coverage-ag005/run-20261001-134817-590）。
- 无管道重启后完整重跑成功：19/19 步、覆盖率 100%、分歧 NONE、exit 0。
- 清理：emulator 停止、3081 杀掉、确认 3080 存活未受影响。

## 七、得到验证的设计与功能

**真实设备上验证过**（evidence/real-settings-coverage-20261001/run-final/）：
配置驱动完整遍历（参数全来自 YAML）；19/19 步全验证 trace（DecisionId/session/唯一性/receipt/路由/checks）；容器 bounds 派生 swipe 命令真实下发并被验证（路由未变 ∧ 内容变化）；滚动后才可见条目的发现与进入（Search settings）；可见性约束指令引擎全程零偏离；9 次 Navigate up 返回全部路由回根；重复进入产生新回执；单一 DSH session 贯穿 20 轮咨询；end-on-root 义务与覆盖判定分离、exit 0。感知链：20 周期截图/Fast 视觉/hierarchy 三源齐备（Fast 20/20、hierarchy 20/20）、typed 投影每周期 209–820 条 claim、Search 页无标题路由回退真实走到、capture→viewport→tap 像素投影落点正确。世界模型：23868 spans 中 11,915 条 evidence.admit、occurrence 景观含 scrollable 角色、route 信念经 revised-claim 修正、derive-slice 38 次 / resolve-current 19 次、freshness 全程 fresh、19/19 步 grounding 零失败。

**仅确定性测试里证明过**：
配置非法值 fail-closed 全分支（21 项）；账本各失败/分歧记账分支（路由未变、目标消失、重复 effect）；maxSteps/maxScrolls/连续失败 bounded-stop；偏离纠正与持续偏离 fail closed、非法 desiredState 拒绝；swipe-down 方向与小容器行程；滚动验证 6 种正反分支；scrollable 投影 3 项；容量重试（ScriptedHandler）；session 复用。

## 八、没测到 / 未验证清单

- **容量重试未在真实运行触发**：真实运行无容量错误；仅 ScriptedHandler 单测（`Consult_RetriesSelectedModelCapacity_OnSameRequest`）。
- **指令偏离纠正、持续偏离 fail-closed、非法 desiredState、maxSteps 有界停止**：仅在模拟世界场景测试验证；真实模型全程零偏离。
- **swipe-down 命令**仅单测构造（`SwipeDown_ReversesDirection`）；真实运行只用了 swipe-up 一次。
- **滚动到底（内容不变）失败路径**仅有单测（`Swipe_ContentUnchanged_FailsClosed`），未在真机复现。
- **重复 effect 检测**仅有账本单测（真实运行无重复 effect 发生，FirstDivergence=null）。
- **Policy/Defer 决策种类**在覆盖模式未使用（真实 20 轮全部 act/noAction）。
- **多步 Act 提案被 director 拒绝**的路径无显式单测（代码在 `ValidateAdherence` 的 `directive-requires-single-step` 分支）。
- **真机失败恢复路径**（grounding 失败 E2 循环、UnconfirmedDelivery）未在真实运行出现——真实运行零失败。
- **其他 locale / 3080 非干扰**仅以存活探测（401）在运行前后确认，未做变体矩阵或全程监控。
- **Focused/Slow 观察深度与 Slow 通道**（PER-017/018）未参与本轮。
- **视觉 vs XML 双源权威冲突**未在真实运行发生（resolve-current 空转；词汇仅 Kernel 确定性测试覆盖）。
- **ProbeStateMachine 结构性失败退避**未在真机触发（仅确定性测试）。
- **旋转/中途视口变化、NAF 节点、API<35 行为**未测。
- **Fast 视觉作为动作目标**（设计上永不）与其检测质量未评估（只统计可用性）。
- **多容器 belief**（单根执法下未出现）、Policy lease/Policy 决策、logical items、live 数据上的 E2B 冲突销案未测。

## 九、一句话总结

AGT-005 用「配置声明覆盖边界 + director 有界指挥 + 账本逐步记账」在真机上一次性达成 19/19 步、100% 覆盖、零分歧的可追溯遍历，感知链（截图/Fast 视觉/hierarchy/typed 投影）与世界模型（11,915 条入账、occurrence 景观、route 修正）全程真实供数，但失败/冲突类路径（容量重试、偏离纠正、双源冲突、grounding 失败循环）在真实运行中未发生，只在测试里证明过。
