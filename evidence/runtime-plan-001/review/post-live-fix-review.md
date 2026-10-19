# RUNTIME-PLAN-001 后续增量复审

## 做了什么

按 WI-RUNTIME-PLAN-001-POST-LIVE-FIX-REVIEW 复审冻结首轮之后的增量。基准取 review/reviewed-inventory.json 与 review/reviewed-implementation.patch；逐项比对 inventory 的 after hash，确认后续变更集中在本轮 Kernel/DSH 修复、测试、真实对照记录以及对应场景 source hash 重认证，没有把 HEAD 的整份 Change 当成本轮改动。读取 ADR-0053、implementation-contract、首轮 review 和两份根因记录。

## 原来的问题

真实 Step 第二次咨询时，Kernel 的无参元素摘要投影在 World 已含多页容器后返回空列表；DSH 无提交重试重建 pending 时丢失当前 decisionId，且成功捕获语义决策后未等物理 turn/end。首个真实对照失败，不能充当性能基线。

## 这次修改

Kernel 的 DeriveElementSummaries 默认从当前 belief 的 CurrentInteractionContainerId 取锚，并继续只筛当前容器 occurrence；导航图测试新增子页 Screen 与 Elements 断言（KernelRunDriver.cs:2108,2295-2305；KernelRunDriverNavigationGraphTests.cs:169-173）。DSH retry pending 现在携带原 decisionId，retry 建立真实 turn/end promise；成功捕获后等待物理屏障（index.js:1362-1398）。

比较 harness 记录目标、模式、边界、覆盖与策略摘要、schema 及 Kernel/Host/harness DLL 和 decision-channel source hash；trial 记录真实 drive/coverage/mode/验证步数。collector 分开统计模型消息/中断尝试、usage 缺失、工具提交与拒绝原因，并要求 cleanupError 为空才将 trial 记为有效。最终 harness 在已知 DSH session 时先撤销 attachment 再 dispose，分别记录两类清理错误（RuntimePlanComparisonLiveTests.cs:120-163；collect_metrics.py:18-62,80-82）。

## 验证结果

Kernel/Host/DSH 的定向 GREEN、source hash 和场景重认证结果来自仓库现有证据；本 Worker 未运行测试、构建、设备或真实模型。新增 retry-failure-fence-red.txt 中 prompt-error 与 30 秒 deadline 两个 route 反例均 RED（0/2）；修复后的 retry-failure-fence-green.txt 五项均通过：保留 decisionId、prompt-error fence、deadline fence、普通 turn 串行、detach 晚到调用。最终 TAP 汇总为 5/5 pass、0 fail，Node 进程正常退出 exit 0（约 90 秒）。

首个 Step 样本在 baseline-metrics.json 中记录为 AgentDecisionFailed、1/2 verified、26 次模型消息/尝试、22 次提交拒绝、1 次无 usage，validTrial=false；334050 是已报告 token 下界。它仍不是有效基线。Plan 对照组及有效 Step 基线/数值门槛仍待现场验收，仿真和单测不能替代。

## 新发现

### 规格轴

**已修复的阻塞：retry 失败/超时曾提前释放物理 turn 锁。** 冻结的 RED 日志证明旧行为下，retry prompt-error 与 30 秒 deadline 后发起的第二个 consultation 都未被 one-in-flight 拦截（0/2）。当前 dsh/uniclaw-decision-channel/src/index.js:1351-1353 仅在 attachment 仍是原对象、pending 与 activeTurn 均已清空时启动 retry；失败/异常出口 1409-1413 仅清除此 retry 自己的 pending，不再抹去活动 turn。turn/end handler 835-837 按 session 匹配后释放 fence；显式 abort/detach 经 retirePending 释放。成功 retry 等待到 deadline 后也只清理本次 pending（1393-1400），仍保留 activeTurn 直到 turn/end 或显式 retire。新增 GREEN route 反例验证第二咨询在 turn/end 前仍返回 one-in-flight；保留旧决策 ID、普通 turn 串行和 detach 晚到调用的五项相关证据全部通过（TAP 5/5，exit 0）。符合 retry-context-fix.md 的屏障要求；未发现该增量仍有阻塞。

**非阻塞计量边界：** collect_metrics.py 将功能性 validTrial 与 tokenAccountingComplete 分开是合理的：前者表示功能/模式/清理达成，后者标记 usage 是否完整。按计划 §6 的 token/费用数值门槛需另要求 tokenAccountingComplete=true；否则缺失 usage 的样本只能提供 token 下界，不能证明成本收益。现有首个失败 Step 样本本身已被标为 invalid。

### 规范轴

**硬性违规：未发现。** 当前元素修复继续使用既有 Kernel 当前页投影，没有改变 World identity、Owner 或定位过滤。harness 的 session 撤销调用复用 DshAgentAdapter 既有 API，先保留 sessionId 再清理；revoke/dispose 错误均进入 trial，collector 已把清理失败排除出 validTrial。增量没有显示职责越界或 ADR-0053 已决语义变化。

**代码气味建议：未发现。** 复审限于后续差异，没有把首轮 71 项复审、D1-D10 或 ADR-0052 决策重新纳入。

## 下一步

本 WorkItem 复审已完成。RUNTIME-PLAN-001 的整体现场验收仍待取得有效 Step 基线，并依据完整成本/时延记录冻结数值门槛，再开始 ShortPlan/RulePlan 组。

## 增补复审：现场准备与 Prompt revision 15

### 做了什么

只读检查 known-path Step/2-4 的 trial/decision 证据、正式与插件 Prompt、两份 manifest/hash、UniAgent profile、profile revision 断言及比较 harness 的共享 guidance；没有运行测试或模型。

### 复核结果

**完整参数示例：通过。** product 与 DSH plugin 的 02-output-discipline.txt 行 21 字面相同。示例包含工具参数最外层 task、必需的 initialization 与 payload、decisionId/kind、Act proposal 的 decisionId/steps，以及所需的 targetRole/effectClass；大括号完整闭合。CURRENT_* 与 ALLOWED_* 明示为需替换的占位值，不会把占位内容宣称为可直接提交的合法决策。DSH 工具参数结构确实要求 task.initialization 和 task.payload（index.js:417-435），Act schema 要求 decisionId、kind、proposal，proposal 再要求 decisionId 与 steps。

**权威边界：通过。** 正式 Prompt 仍明确 availablePlanSources 是只读事实投影而非动作许可（02-output-discipline.txt:19）；新增 harness 指导仅在目标入口均验证、已回根页且 mandatory 义务已满足时建议提交不含 completion 的 NoAction；它明确 intent ID 不是 EffectReceipt ID，不能伪造 dispatch 锚，只有仍需申请 completion 折抵时才引用当前可核验的真实锚（RuntimePlanComparisonLiveTests.cs:111）。没有修改 schema、Kernel 校验、拒绝语义或 DSH retry/模型配置。

**版本与 hash：一致。** Product 与 plugin 的三个 Prompt segment 和 manifest 均逐字相同；两边 prompt-hash.txt 相同，按 manifest bytes 加有序 segments bytes 复算均为 8829fab7be32596578ce36801f6012e09f7734e95df0e1cefb41433000b528bd。两份 manifest 的 promptRevision 均为 15；UniAgent profileRevision 为 15，装配 hash 对应该值；profile 测试断言 revision 15。

### 现场证据边界

Step/2 确有 2/2 验证动作且 Owner CoverageComplete，但模型给 NoAction 附加 completion，伪造 checklist 锚 dispatch:intent-1/2。Kernel 将运行停在 AwaitingCompletionAdjudication / completion-anchors-unverified；这证明拒绝边界仍生效，不是通过样本。Step/3 与 Step/4 的 trial 仍为 AgentDecisionFailed / no-response；决策记录显示第二咨询无 decision，Leader 报告原始与 retry 输出均为不完整 JSON、provider 返回 MALFORMED_RESPONSE 且没有 tool/call。它们属于 revision 15 guidance 前的无效运行，不能证明新增示例已解决真实模型格式问题。**截至这批 Step/2-4 证据，当时尚无有效 Step 样本；后续 Step/5 更新见下节。**

### 结论

本次 Prompt/Profile 与 harness guidance 增量未发现规格或规范阻塞；示例完整、authority 边界清楚、revision/hash 同步。该指导可作为下一轮现场试验输入，但现场效果须由 revision 15 下有效 trial 另行证明。

## 增补复审：Node 夹具清理与 revision 15 Step/5

### 复核结果

**Node 全量失败属于跨用例状态污染，不是 retry fence 产品回归。** 旧汇总 `plugin-final-prompt15.txt` 为 39/44，五个失败均在后续用例观察到 `promptCalls=0` 或缺少首条 content。夹具此前允许 prompt-only 用例发出原 turn/end、触发重试，却不为 retry turn 发 turn/end；模块级共享插件状态于是把仍在途的 `activeTurn` 带进后续用例。新的 `test.afterEach` 只对本用例 `applyHost` 登记的 harness 调用真实 `/api/uniclaw-agent/detach` route，再清空登记（`dsh/uniclaw-decision-channel/tests/plugin.test.mjs:46-55`）。该 route 经产品既有 retire/abort 生命周期释放 fence，没有直接写内部状态，也没有改产品 `index.js`。

**夹具清理没有掩盖屏障断言。** prompt-error 与 deadline fence 用例自身先启动原咨询并发出原 turn/end，等待 retry prompt 出现，然后在 retry turn/end 尚未发生时发起第二咨询，断言返回 `one-in-flight` 且 prompt 数不增加（`plugin.test.mjs:615-647`）。每个用例的 `finally` 先发 retry turn/end、走真实 detach 并等待咨询收敛；顶层 `afterEach` 是额外隔离兜底。因此清理发生在测试断言之后，不会让预期中的拒绝变成通过。专门 GREEN 记录仍为 5/5 pass、0 fail、Node exit 0（`protocol/retry-failure-fence-green.txt`）。未发现规格阻塞或测试通过条件被清理路径短路。

**规范轴：无越界。** afterEach 复用公开 detach route，符合产品/Harness 分层；`activeHarness` 仅由测试安装路径登记，清理针对本测试 harness，不会无条件 retire 共享模块里的任意 session。测试挂钩自身若 detach 抛错会使该用例失败，而不是静默吞掉清理错误。

**现场进展：已出现一个有效的 revision 15 Step 样本，尚不能冻结成本门槛。** known-path Step/5 的 facts 与 `baseline-metrics.json` 一致：Completed、modeConformant、2/2 verified、CoverageComplete、无 error/cleanupError；实际 profileRevision=15、promptHash=8829fab7…528bd，decision-channel source 与 Kernel/Host/harness DLL hash 均已记录，6 次模型调用均有 usage，`tokenAccountingComplete=true`、`validTrial=true`。故此前“仍无有效 Step 样本”的表述已由这次运行更新；现有 evidence 仍只有单个已知路径有效 Step trial，且 provider 未报告货币费用，不能单独建立数值成本门槛或替代计划要求的重复/比较组证据。Plan 组尚未开始。

### 结论

夹具隔离和现场 Step/5 证据增量未发现新的规格或规范阻塞。已核实的 retry 物理 turn fence 在测试内仍被直接反例覆盖；known-path Step/5 可作为 revision 15 下首个有效 Step 样本进入后续比较，完整成本门槛与 Plan 组仍待现场验收。

## 增补复审：Plan 咨询投影精简与 Profile revision 16

### 做了什么

只读复核 `core/compact-context.md`、Plan 输入膨胀诊断、ADR-0053 实施契约、Driver 投影与完成核验代码、Settings Source 描述、Prompt/Profile 16 及现有现场记录。未运行测试、构建或现场动作。

### 规格轴

**审计与咨询投影分层成立，未见尝试/证据丢失。** `CurrentPlanExecutionView()` 仍直接投影 `_planAttempts` 全集及 `_blockedCandidates`；新增 `AgentPlanExecutionView()` 只替换每个 attempt 的 `EvidenceRefs` 为其稳定实际 ID `plan-attempt:<AttemptId>`，其它字段由 record `with` 原样保留（`KernelRunDriver.cs:1806-1828`）。两个 AgentDecisionContext 装配点均使用该 Agent 投影（`KernelRunDriver.cs:1990,2133`），公开 Driver/持久审计仍保留完整 evidence refs。阻塞候选证据没有被这处投影改写。

**plan-attempt 引用是可核验身份，不是自证文本。** `VerifyCompletionAnchors` 仍按精确 AttemptId 查询 Driver 实际记录，并只对 `Status == Verified` 返回通过（`KernelRunDriver.cs:2472-2489`）；新增公开 Driver 测试证实 24 个候选 refs 与 24 个 guard refs 均留在审计 attempt，Agent 上下文只见对应 `plan-attempt:` 标识，且现有完成路径能消费该标识。相邻反例仍证明把旧 `step:1.0` 用作 forEach 完成锚会被拒绝，实际 Verified attempt 锚才通过（`KernelRunDriverPlanTests.cs:1580-1690`）。这没有把 receipt 冒充验证，也没有改 completion verifier。

**必要观察/验证没有为省调用让路。** Profile 16 的新增规则保持 Act 在 Runtime 内逐动作取得新观察并独立验证；只有事实不足以回答下一步问题时才额外 Observe/Defer，未知保留，不得以省调用跳过必要观察（`product/prompt/uniagent-prod/03-payload-shapes.txt:95`）。该指引没有把“减少重复观察”扩展成跳过 Act 后验证。

**RulePlan 的 scope 语义仍闭合。** `SettingsPlanFacts.SourceDescriptions` 明确要求每个迭代 body 内进入候选、观察、返回并确认同一根页，返回不得移到 forEach 之后；Source 暂空不证明范围穷尽（`SettingsPlanFacts.cs:10-14`）。因此首个规则试验因 body 未返回而中断，是既有 scope 校验按契约工作；改动把所需组合约束写进 Source 事实描述，没有放宽 Driver 的范围检查。

### 规范轴

**硬性违规：未发现。** Driver 完整审计面与 Agent 精简面分别由现有两个投影承担；引用到现有 attempt identity，完成语义仍由 Runtime 核验。产品 Prompt 与 DSH plugin 的三个 segment、manifest、prompt-hash.txt 经逐文件比较一致；Profile/manifest revision 均为 16，prompt hash 为 `3cdf975686600bb1f12da8f6fdd9b4f6b2a287e0aab96131aec5918821cb3134`。没有发现 Host 专用协议语义被放入共享 Prompt。

### 验证与证据边界

定向公开 Driver 回归由 RED 转为 GREEN 1/1，Plan driver suite 38/38，来自现有 `core/compact-context.md` 记录；记录显示 Driver 审计 refs 24+24 保留而 Agent attempt refs 缩至单个精确 attempt 引用。诊断确认旧 ShortPlan/1 与 RulePlan/1 功能虽完成但未达性能门槛，原始样本和 thresholds 保留；这次结构性精简有明确输入缩减路径，但当前证据尚未给出修复后 Plan token/成本收益。

当时复核已出现 Profile 16 的 known-path Step/6：trial 为 Completed、modeConformant、2/2 verified、CoverageComplete、无 error/cleanupError；experiment 钉住 Profile 16 与上述 prompt hash 及当前 Kernel/Host/harness/source hashes。该段记录对应的读取截点早于 compact epoch 汇总；随后冻结的 `thresholds-compact` 与 ShortPlan/2、RulePlan/2 结果在下一增补节中复核。该 Profile 16 试验不得与后续 Profile 17 epoch 混用。货币费用在既有 provider 记录中仍不可用。

### 结论

此次性能修复增量在规格与规范两轴均未发现阻塞：审计数据保留、attempt 引用绑定真实身份、完成 verifier 仍只认 Verified，Prompt 未省略必要观察验证。输入投影按设计收敛，但实际 Plan 性能收益尚未证实；Profile 16 新 epoch 目前仅有 Step/6 单次功能样本，需完成另外两份 Step、采集完整指标并冻结新门槛后才能开始 Plan 组。

## 增补复审：终局停滞误报与 Prompt/Profile revision 17

### 做了什么

只读检查真实 compact Plan 诊断与 trial、最终 SettingsCoverageRunner/InterleaveBookkeeper、CoverageStepJournal、终局完成路径、Host RED/GREEN 与 Prompt/Profile 17。未运行测试、构建或模型。

### 规格轴

**新证据确认 compact 性能仍失败，旧试验边界保留。** Profile 16 的 known-path ShortPlan/2、RulePlan/2 均 Completed、2/2 verified，但都仍是 3 次咨询：ShortPlan 为 7 次模型调用、132,808 tokens，RulePlan 为 6 次模型调用、107,794 tokens；对照 Step/6 为 3 次咨询、6 次调用、80,311 tokens，因此均未达到 `comparison-result-compact.json` 的 Plan gates。ShortPlan/2 末尾在检查点选择 Continue 空尾，之后再发 NoAction，并重复观察，恰好是 revision 17 新提示针对的冗余路径。候选 ShortPlan/1 原始与 retry 返回格式错误 JSON、0 动作；作为无效失败消耗保留，不计有效收益。该批属于 Prompt 16 的 compact 尝试，不能与 revision 17 新批混作同一门槛证据。

**Interleave 的同步次序符合 Owner 与验证语义。** `WrappedNext` 只在 `ObservationContext.External` 前调用 `journal.Drain(materializedSteps)`，随后才 `InspectPendingAfterCommit`、记入观察并计算停滞；对 `PostActionEffectFlow` 不提前 drain（`SettingsCoverageRunner.cs:627-636`）。`CoverageStepJournal.Drain` 仍以 Kernel 真实 `EffectReceipts` 与 `PostActionEffectVerification` 生成 `CoverageStepRecord.Verified`，未把 Guard Allow、receipt 到达或模型自述折作验证（`:929-991`）。Driver 的动作链仍先取 PostActionEffectFlow，再调用 `AssuranceVerifier.Verify`，只有通过才更新 PlanAttempt 为 Verified（`KernelRunDriver.cs:1105-1148`）。这样 External 前 flush 可让刚完成的验证事实先进入 Owner ledger；未完成验证的动作仍不能越过原屏障。

**覆盖指纹修正同时保住真实进度与停滞信号。** 最终 `entered` 取 Owner 的 `EnteredRootDescriptors`，排除 AgentSkipped，再并入明确的 RouteEntered/ReachedOverlay marks 并去重（`SettingsCoverageRunner.cs:686-692`）。这解决了真实失败根因：已验证进入在 Owner 账本中可见，但普通验证不会自动生成显式 TargetMark；仅计 TargetMarks 会让进度长期为零。根页 CoverageComplete 时只在当前 route 确为配置 RootRoute 才抑制停滞咨询（`:668-673`）；未完成 traversal 仍按 `(entered, frontier)` 计数，未把 StepsVerified、弹窗交替或任意成功清障算作覆盖进度。

公开 Host Theory 的 `observeAfterCoverage=true` 两条路径在 RED2 中因末尾四个 Observe 收到 STAGNATION intervention 而失败（2/4）；最终 GREEN3 为 10/10，Host 全量为 341/341，Agent 29/29、Agent.Dsh 184/184、Host.Dsh 34/34。原 `SimPopupLoop_NewFingerprintTripsStagnation_OldFingerprintNeverDoes` 仍断言：弹窗 present/absent 交替、每次清障动作都验证成功但覆盖指纹不变时，新指纹到阈值会触发，旧 StepsVerified 指纹永不触发。早期 green/green2 未通过的记录仍保留；最终诊断说明它们仅有终局 early return/External flush 两步，不含后续修正 entered 来源的最终逻辑（`host/terminal-stagnation-diagnosis.md`）。因此最终结果没有把原 popup-loop 进度反例压掉。

**Prompt 17 的 NoAction 指引不改变完成核验。** 新文案允许在已满足目标的 Paused Checkpoint 直接 NoAction，不必为清空空尾而 Continue；同 subject 的 Act 后观察已给出且验证时，不机械追加重复 Observe；有具体欠证时仍需补证，逐动作的新观察、验证和未知保留均明确保留（`product/prompt/uniagent-prod/03-payload-shapes.txt:95-97`）。Kernel 收到 NoAction 仍执行 ValidateDecision，暂停 Plan 被标记 Abandoned；若带 completion 则逐个精确核验 anchor，只认可实际 Verified attempt，未核验锚进入人工裁决；随后仍经过 TerminalEvaluation 的 task evidence barrier 与 `EvaluateTerminalOnce`（`KernelRunDriver.cs:615-648,1568-1577,2472-2489`）。故直接 NoAction 只是去掉空计划往返，不是跳过 Run 完成判定。

### 规范轴

**硬性违规：未发现。** Runner 通过既有私有 Interleave 回调协调 Owner 同步，没有新增 public surface、Effect 权限或 Host 越权改写 Kernel 判定。Settings Source 描述与共享 Prompt 同义明确每个 Act 已自动独立观察验证、同一路由复核不必另加 Observe，真实欠证仍须补充。Product 与 DSH plugin 的 segments、manifest、prompt-hash 经逐文件比较一致，Profile/manifest revision 均为 17，hash 为 `a1574ef2fdf37548f1efb8e191415bf2c09e9b969936b8ebccfcfac29222a179`。

### 验证与计量边界

revision 17 的 known-path Step/7 现已落盘并汇总为有效完整样本：3 咨询、6 次模型调用、81,458 tokens、20,491.8572 ms，profile/hash 与当前 revision 17 相符。候选 Step/5 为 5 咨询、11 次调用、189,352 已报告 tokens，含一次失败且无 usage；Step/6 为 5 咨询、10 次调用、188,953 tokens 且 usage 完整。`freeze_thresholds.py` 现有“baseline 所有样本 usage 完整”断言因此拒绝生成 `thresholds-final`，没有产出新门槛或开始 revision 17 Plan 组。

拟议的保守 token 上界可成立：候选基线已报告下界均值是 `(189352 + 188953) / 2 = 189152.5`；其中一份存在 usage 缺失，因此真实基线均值不低于该值。若 Plan 组要求全部真实调用（含失败/拒绝/修正 attempt）usage 完整，并将每完成任务计入所有尝试的 tokens 控制在 189,152.5 以内，则 Plan 实际量不高于已知基线下界，必然不高于基线真实量；这证明的是 tokens 不回退，不是精确节省比例。缺失 usage 不可按零计、失败调用仍进总量；consultations、真实调用数和时延仍独立比较，货币费用仍无 provider 数据。这个边界比假设缺失用量或改写旧样本更可证。thresholds-final 尚未写入，Plan revision 17 尚未开始；候选 Short/1 的失败仍应保留在总实验消耗记录。

### 结论

最终 Runner 修正保留了 receipt→独立验证→Owner ledger→进度判断顺序；终局提示只在目标已满足且位于 Root 时抑制，popup-loop 反例仍可触发停滞；Prompt 17 的空尾 NoAction 仍走 Kernel 终局核验。未发现规格或规范阻塞。性能效果必须按新 epoch 重新验收；下界门槛若落地，应只称为保守“不回退”证明，并继续要求 Plan 组 usage 完整。

## 增补复审：DSH failed-response usage 传播

### 做了什么

按请求先读了 dk-harness 的 `AGENTS.md`、`packages/AGENTS.md`、`docs/architecture.md`、`docs/testing.md` 与 `docs/AGENTS.md`，再只读检查 provider adapter、stream 回归、英中文 README 与 pairing manifest、生成 bundle 记录及 keyless snapshot 记录，并追踪 usage chunk 经 assembler/Agent 的持久化路径。未运行测试、构建或模型；本节不把既有日志当成本 Worker 执行结果。

### 规格轴

**Provider usage 提前发出，同时仍保持失败语义：通过。** `packages/llm/llm-deepseek/src/translate.ts:146-166` 先要求已有 stop reason 且全部 blocks 已闭合，再计算并发出聚合 usage，之后才执行空内容与 tool JSON 校验。非法 JSON 仍以 `MALFORMED_RESPONSE` 抛出，没有修补参数；该路径不产出 `finish`。现有 stream 回归将 `{` 和 `[]` 两个反例都断言为 `MALFORMED_RESPONSE`，并检查最后收到的 chunk 是 provider event 聚合出的 12 input + 5 output = 17 tokens usage，且没有 finish（`tests/stream.spec.ts:80-96`）。既有 RED 日志为 2/47，GREEN 日志为 3 个文件 209/209。

**Usage 来源与不完整流边界：语义成立。** `updateUsage` 只接受 provider SSE 的 `message_start.message.usage` 与 `message_delta.usage` 非负整数；totalTokens 仅从这些已接收字段求和。usage 只在 `message_stop`、有效 stop reason、所有 blocks settled 后产出；提前 EOF、缺 stop reason/未闭合 block、provider usage 字段非法或流异常仍在该屏障之前抛错，不会合成 usage 或 finish。共享 `BlockAssembler` 记录 usage chunk，但只有 finish chunk 设置终止原因；Agent loop 在流异常 catch 中将已有 `live.usage` 附在 `assistant/attempt`，并重新抛原异常；正常 tool dispatch 位于消费完成流并结算 assistant message 之后。因此这次失败校验可保留费用用量，但不会伪装成成功消息或触发 tool 执行。

一个计量边界需保留：本次改动证明的是“provider 已发 usage 且消息完整结束后，后续输出校验失败不会丢掉 usage”；若 provider 完整 `message_stop` 却未提供 usage 字段，现有 adapter 的零值初始化仍可能产出零 usage。此次证据没有表明 DeepSeek 是否允许这种响应，故不把完整结束事件本身等同于 usage 完整；真实 trial 仍需独立依据 provider usage 覆盖性标记 `tokenAccountingComplete`，不能由此 stream 变更推定。

### 规范轴

**硬性违规：未发现。** 改动局限在既有 DeepSeek provider adapter 的 stream 翻译边界，保留共享 `StreamChunk`、Agent retry 与 tool 调用边界；没有改共享协议、放宽错误校验、补全 JSON、增加重试或切换模型。README 英中说明一致，`README.i18n.yaml` 的 token-effect 配对摘要有对应更新。公开调用链中 usage 保存在失败 `assistant/attempt`，而非作为完成消息或工具授权。

### 证据状态与遗留项

RED/GREEN、叶包构建和 published-bundle smoke 支持 adapter 局部行为。`dsh-usage-entry.json` 是修改前的恢复快照，包含当时 source/test/docs/built files 的哈希，用于回滚恢复边界；它不是当前 checkout 的版本身份，所以与当前文件哈希不同是预期。当前 harness 试验 metadata 已加入 `providerArtifactPath` 与 `providerArtifactSha256`，并要求实际 bundle 文件存在；新 epoch 应使用实际加载的 bundle hash 和新 PID，旧 epoch 不复用。

Snapshot1 的全入口尝试确因新增 scenario 的 `pin: true` 复制了已存在的 retry header owner，在 catalog 阶段报 `header class retry/retry has multiple pins`；当时为 0 tests，并非 replay 失败。该 scenario manifest 已移除重复 pin，改为引用唯一既有 retry header owner。修正后的 `dsh-usage-session-snapshot2.txt` 运行 `failed-response-usage` 的定向 headless profile replay：1 passed、134 skipped 是 `-t failed-response-usage` 精确选择所致；若 catalog 仍失败则会是 0 tests。该路径因此通过所需的 targeted profile replay，无需也不应默认跑无关 134 个 scenario。`snapshots/session/failed-response-usage/session.v3.jsonl` 与 manifest 位于新 generation/新 scenario，不改旧 generation。

另有 plain-Node published artifact smoke（`dsh-provider-built-smoke.mjs` / `.json`）经真实公开 `DeepSeekAdapter` 与 loopback SSE transport，观察到 usage 17、`MALFORMED_RESPONSE`、无 finish、单次 request；这是 bundle 层行为证据。`host-dsh-post-live-fix-final.txt` 显示 Host.Dsh 定向套件 34/34。现有实验尚未因此成为新 epoch 的性能通过；新 PID 加载并固定 provider hash 后还需按计划完成新基线与 Plan 组。

### 结论

此次 usage 传播修复在规格与规范两轴未发现阻塞：完整 provider 响应先记录已报告用量，错误 tool JSON 仍失败且无 finish/tool dispatch，流异常仍不能充当完整统计。定向 keyless profile replay、published-bundle smoke 与 Host.Dsh 34/34 通过；snapshot1 是已修复的重复 pin catalog 失败，不能误读为 replay 失败。恢复快照哈希与当前版本身份用途不同；harness 已具备记录实际 provider artifact path/hash 的字段。新 epoch 仍需用新 PID 加载并固定实际 bundle hash 后完成新的基线及 Plan 性能验收。

## 增补复审：授权换用 Pro 与 accounted Flash 结果

### 做了什么

只读核对 `model-change-pro/authorization-and-binding.json`、换绑前副本与当前 DSH product binding、Flash accounted thresholds/result、`freeze_thresholds.py`、`evaluate_results.py`、metrics collector、当前 Pro Step trial/experiment 以及固定 provider bundle hash。未运行测试、模型或设备动作。

### 规格轴

**模型切换范围与批次隔离：通过。** authorization-and-binding 明确记录 Owner 授权“更换模型并重跑对照”、Flash→官方 catalog 中 `deepseek-v4-pro`、原 3081 service、thinking disabled、proxy/credentials 不变，以及新 Step 基线→冻结阈值→六项 Plan 的先后顺序。当前 bindings 对应授权后的 SHA-256 `aff3cc62…`，备份文件对应授权前 `fe7fb7f1…`；diff 只改变 `selected` 并加入 Pro choice，原 Flash choice、服务 endpoint 与其它 choices 保留。新实验 metadata 记录 Pro model、Prompt/Profile 17 hash、同一 bounds/coverage/action policy、同一 Kernel/Host/harness/decision-channel 和 provider artifact SHA-256 `3c05833a…`。因此模型是有意改变的变量，不能复用 Flash 门槛证明 Pro 的收益；现有 Pro 样本按新 epoch 隔离。此检查截点已看到 Pro known-path Step/10 和 candidates Step/9 的 Completed/CoverageComplete/预期步数均验证、实际 model 字段为 deepseek-v4-pro，且两边 provider artifact hash 固定；candidates Step/10 尚未落盘，Pro baseline/thresholds 与 Plan 组尚未形成，这是正常的未完成验收状态，不是代码阻塞。

**Flash accounted 结果解释准确，旧 epoch 门槛未混算。** `thresholds-accounted.json` 按完整条件（包括 Flash model、Prompt17 与实际 providerArtifactSha256）筛得 known Step repeats 8/9、candidate Step 7/8。known baseline 失败 repeat 8 的消费仍计入 token/time 分子；Plan 按有效完成数摊销各组所有尝试。`comparison-result-accounted.json` 中六项 Plan 全部功能有效且 usage 完整：known ShortPlan 为 3 consultations/7 modelCalls，超过阈值 2/5，tokens/time 93,293/24,066ms 通过；known RulePlan 2/4 全部通过；candidate ShortPlan 两次合计按完成数摊为 2/4.5，candidate RulePlan 为 2/5，三项均过咨询、调用、token 与时间门槛。故“六份均功能完成，四组仅 known ShortPlan 失败”正确；更精确地说，该组的 consultation 与 actual-model-call 两个子门槛失败，其它子门槛通过。整项 exploratory scope 仍非全树/统计显著/一般提速结论，费用仍不可得。

**门槛冻结与比较算法总体符合预注册口径。** `freeze_thresholds.py` 选 baseline 的完整条件字段含 model 与 provider artifact hash；拒绝覆盖已冻结门槛，要求 known/candidates 的有效基线数量分别为 1/2，并在任何同条件 Plan experiment 存在时拒绝冻结。咨询与模型调用阈值设置为最小有效 Step 样本减一；token/time 阈值对所有同条件 Step 尝试（包括无效/失败）按有效完成数摊销。候选 baseline 有 usage 缺失时，Plan token gate 仅接受全部 Plan usage 完整且总量不高于已报告 baseline 下界，不冒称节省比例。`evaluate_results.py` 同样按冻结条件筛组，把每个组所有 attempt 的 consultations、assistant message/attempt 调用、报告 token 与正式/总时延加入分子，再除以有效完成数；token gate 要求组内所有尝试 usage 完整，且没有货币价目推算。当前 `all-trial-metrics.json` 没有 `accountingError`，Pro known/10 与 candidates/9 各有一个唯一 v4 session generation，因此上述已产结果不存在该缺失日志触发条件。

**统计计量边缘缺口：会话账本缺失时 actual-model-call 子门槛可误报。** `collect_metrics.py:17-25,61-62,80-82` 在 sessionId 缺失或匹配到零/多份 generation 时会保留默认 `modelCalls=0` 并记 `accountingError`/`tokenAccountingComplete=false`，但 `validTrial` 只看功能完成字段，不看 accountingError。`evaluate_results.py:11-19` 的 fewerActualModelCalls gate 只检查按有效完成数摊销后的 `modelCalls`，不检查相关样本是否有唯一可读 session ledger。于是功能上 Completed 但会话来源缺失的样本可进入有效完成分母，并以零 modelCalls 进入调用 gate；tokens gate 会被 tokenAccountingComplete 挡住，但 actual-call 子 gate 仍可能显示通过。当前 Flash accounted 样本与已查看 Pro 样本各有 session generation，故没有污染本次结果；这是 Pro 新结果出具前需留意的统计诚信边缘，而不是当前产品运行阻塞。

### 规范轴

**硬性违规：未发现。** 模型变更处在 DSH realization binding 层，授权、前后文件哈希和保留选择均有记录，没有修改共享 Prompt、Kernel/Host 语义、权限或 provider 配置。各 comparison epoch 通过模型、provider bundle 和其余条件摘要隔离；失败旧样本不删除，Pro 不借用 Flash threshold。脚本保存并校验 baseline metrics hash，冻结产物不可覆盖，符合可追溯比较与错误证据不伪装为收益的约束。

### 结论

获授权的换绑和 Pro epoch 规划在规格及规范轴通过；Flash accounted 的最终摘要准确，已冻结门槛与已算结果之间未见混批或算术口径错误。新增 findings 是评估器在会话日志缺失时可能低估 modelCalls 的边缘风险；它未触发于现有 Flash/当前 Pro Step evidence，但 actual-model-call gate 应只在 session accounting 可核验时视为已证明。Pro 新基线、threshold freeze 和六项 Plan 仍待现场完成，当前不宣称 Pro 性能结果。

## 增补复审：Pro 门槛冻结顺序偏差与审慎恢复

### 做了什么

只读检查 Pro Step baseline rows、thresholds-pro 文件状态、冻结脚本当前选择逻辑与 Plan-existence guard、已启动 `known-path/ShortPlan/6` 的文件存在性（不读其 trial/decision/session/usage 内容）、预注册 procedure 和 metrics/evaluator 语义；未运行任何测试、模型或设备动作，也未读取该 Pro Plan 的结果。

### 规格轴

**真实流程偏差成立。** Pro baseline metrics 有 20 行历史记录，但按 model、providerArtifactSha256 与其余条件筛选后，恰有三份 deepseek-v4-pro Step 有效样本（known/10、candidates/9、candidates/10）；`thresholds-pro.json` 当前不存在。已知流程中 freeze 返回失败后，runner 仍启动了已预定 `known-path/ShortPlan/6`；后续文件时间审计发现同批还启动了 `known-path/RulePlan/5`。两项的 `experiment.json` 与 `trial.json` 均已存在，但本复核未读取 trial 内容。故这两个 invocation 在数值门槛正式冻结前开始，不能作为符合顺序的有效预注册 Plan 样本；已 SIGSTOP batch 父进程、暂停后续组的安排适当。不得基于其结果调门槛，也不要把这次偏差改写为“门槛在 Plan 前冻结”。

**建议的最小且可辩护处置：在完全未读取 Plan 结果的前提下，按原固定算法做结果盲法的 late lock，并保留这次试验的通常计量资格。** 原门槛公式已由此前 comparison procedure 固定，Pro baseline 三份有效 Step 与 baseline SHA 均在本次 Plan 启动前落盘；本复核和 `registration-deviation.json` 均记载没有查看该 Plan 的 trial、decision 或 usage。若在任何结果暴露前按 Pro 明确条件从该 baseline 计算 immutable `thresholds-pro`，其具体数字仅是冻结 baseline 的确定函数，不使用这次 Plan 的表现，因而没有 outcome-conditioned gate tuning。该 invocation 的结果可按正常功能有效性进入 known ShortPlan 样本数分母和全尝试成本分子；其余原定四份继续按原repeat执行，避免为了流程修饰把正常功能样本剔除或把调用数门槛机械推高。必须把时间偏差清楚标为“首个 Plan 已启动后才锁定分析门槛”，不得称此轮严格满足“任何 Plan 存在前冻结”的预注册顺序；这使其仍是有结果盲法锁定分析规则的探索性工程对照，而非无偏差的前瞻性预注册实验。若任何 Plan 结果先于阈值锁定被读取，则不再适用此处置，后续只能作为新一轮结果盲前瞻试验。偏差 invocation 的成本始终留在相应组分子和全局账本，功能有效时进入分母。

**不要静默绕过 freeze guard。** 本复核截点的 `freeze_thresholds.py:8` 已改为按 `RunDir` 固定宽度时间戳 basename 选最新运行，能修复 repeat 9/10 的路径字典序问题；但混合 Flash/Pro baseline 仍应显式 pin model/providerArtifactSha256，避免未来 epoch 时间顺序变化时误选。`freeze_thresholds.py:11-12` 的 same-condition Plan existence guard 拒绝任何已存在的 Pro Plan experiment，不能删除或静默绕过。对于有审计记录的 late lock，可增加显式参数/路径引用 deviation 与 blind calculation；仍需校验 baseline hash、Pro 条件、样本数及原公式，并在产物注明冻结晚于首个 Plan 启动。

**正常计量管线可用于结果盲法 late lock，不应人为剔除该样本。** `collect_metrics.py:80-82` 按 drive/coverage/mode/error/verified 定义功能 `validTrial`；`evaluate_results.py:13-19` 以所有条件匹配 attempts 汇总成本分子、以功能有效完成数作分母。既然偏差仅在分析门槛锁定时间，而不是模型、任务、模式或执行条件，且结果仍未读，不需新造 protocol-invalid 样本分类来扭曲本组分母；独立 deviation 记录提供顺序透明度，普通 metrics 保留实际功能结果与全部成本。

### 规范轴

**模型授权仍有效；流程执行偏差应独立记录。** Owner 已明确授权换模型重跑。绑定变更仅限所选实现与新增 catalog choice，Flash 旧 choice、service、Prompt/Profile 17、程序哈希、provider artifact/thinking 设置及代理/凭据均按授权记录保持；三份 Pro Step 的实验 metadata 已记录相同 provider artifact SHA，旧 Flash result/thresholds 仍可追溯。错误在于 freeze 命令失败后 orchestration 没有停止，并非授权不足，也没有证据显示改变了产品语义或弱化门槛。

### 结论

存在一项真实流程顺序偏差，但它不必使本次 Plan 数据报废：若阈值锁定前没有读取任何 Plan 结果，可按 immutable Pro Step baseline 和此前预写的原公式作结果盲法 late lock，保留该 invocation 的普通功能有效性与完整成本，再完成余下四项原计划试验。这样可支持透明、探索性的 Pro comparison，但报告必须声明门槛冻结晚于首个 Plan 启动；不得声称严格满足原始 pre-Plan-freeze 顺序。若结果已被读取，则不能以此方式追溯冻结门槛。当前冻结逻辑还应显式 pin `deepseek-official/deepseek-v4-pro` 和 provider artifact SHA；RunDir basename timestamp 改善了 9/10 字符排序错误并有回归证据，但单靠“最新”仍会在未来混合 epoch/并行时间记录时误选条件。threshold existence guard 保留是正确的；任何 late-lock 只可通过独立 deviation record、baseline hash、所选条件和锁定时间显式审计，不可静默移除 guard。

### 方法裁定补记：结果盲法 late lock 可保留样本

随后新增的 `model-change-pro/blind-gate-calculation.json` 提供了关键独立证据：它声明在未检查首个 Pro Plan 输出前，对原有 `baseline-pro-metrics.json` 使用原 comparison procedure 的固定公式计算边界；已记录 deviation 的 baseline SHA-256 为 `9445bab6…`，且文件字节哈希复核未变。计算结果可由三份 Pro Step 样本核对：known-path ≤2 consultations、≤5 model calls、≤82,081 reported tokens、≤27,745.0817 ms；candidates ≤4、≤9、≤195,483、≤46,773.53085 ms。上述数值不依赖首份 Plan 结果，因此没有 evidence 显示为已见结果调门槛。该 Pro Plan 结果仍未被本复核读取。

据此，上一节中“考虑将异常尝试计作 protocol-invalid 且排除有效完成分母”的候选处置不再是推荐项。若六项任务/模式/环境本来就是同条件的预定六个 Plan 样本，且 first result 在 blind gate calculation 锁定前未被查看，最低偏差且最少浪费的方案是保留该 invocation 的普通功能有效性判断：Completed 且覆盖/步数/mode/清理全部符合时进入相应有效样本分母；无效时照常不进分母。无论有效与否，其所有 consultations/model calls/tokens/time 都进入该组成本分子和全局账本。这样不会人为把流程管理成本摊在 Plan 完成任务上从而令门槛先验地不可达，也不会因样本的产品结果决定是否接受它。

必须如实称此为“首个 Plan invocation 已启动后、任何 Plan 结果读出前作的结果盲法 gate lock”，不能称六个 Plan 在执行前均已完成严格预注册。它可支持本任务尺度的探索性工程对照：公式已预定、 baseline hash 固定、数字由 Pro Step 唯一决定、结果检查尚未发生；但方法强度低于严格 prospective preregistration，不支持统计显著性或普遍性能主张。若日后发现结果在 calculation timestamp 前已被查看，则这一论证失效，应只用此后样本作前瞻比较。

恢复执行前仍需让 immutable `thresholds-pro` 同时带上 baseline SHA、model 与 provider artifact 条件、六个预定样本信息及 late-lock deviation 记录。当前代码对阈值覆盖和同条件 Plan 存在的默认 guard 应保留；只能增加一个显式、可审计的 late-lock 路径，指向 `registration-deviation.json` 与 `blind-gate-calculation.json`，不得静默删除 guard 或直接把手工计算文件冒充已登记 threshold。当前 `freeze_thresholds.py` 的 `Path(RunDir).name` 按真实固定宽度 run 时间戳修复了编号 9/10 的字典序错误；leader 报告的回归断言能复现旧选择 Flash/9 并确认修后选择 Pro/candidates/10。为避免未来混合/并行模型时间序再误选，仍建议显式要求本 epoch model/provider artifact hash，并验证三份 baseline 条件与样本数。此前的主要结论因此收敛为：真实的是门槛文件因选择错误/退出码处理导致晚锁这一流程偏差；无需将首个样本机械判废或额外补跑，只须完整披露、按正常指标定义计入并锁定后续分析边界。

### Late-lock 复核更新：已启动项纠正为两项

后续文件时间审计与更新后的 `registration-deviation.json` 确认，thresholds-pro 锁定前已启动的同条件 Plan 共两项：known-path/ShortPlan/6（experiment mtime 12:46:14 UTC）和 known-path/RulePlan/5（12:46:48 UTC）。两份 trial 结果文件虽已存在，但此 Worker 没有打开任何 Pro Plan trial、model-decisions、session 或 usage 内容；deviation 明确保存两路径、两份 resultFileTimes、`planResultReadBeforeRecord:false` 与 `resultUnblindedAtUtc` 缺省。故旧文中“单个/首项偏差”应按“两个已启动、结果均未解盲”理解；其余原计划四项为候选 ShortPlan/7、8 与 RulePlan/5、6。

Late-lock 条件通过只读核对：baseline-pro-metrics 当前 SHA-256 与 deviation/thresholds 中 `9445bab6…` 一致，20 行历史记录由冻结条件 pin 到 Pro；thresholds-pro 中 model 是 `deepseek-official/deepseek-v4-pro`，provider artifact SHA 为 `3c05833a…`，并记录三个选中的 Pro Step experiment hashes。门槛数值与 blind-gate-calculation 完全一致；thresholds SHA `86db2e73…` 与 deviation 记录一致。`freeze_thresholds.py` 现显式要求 provider、model、provider-sha；按 Step experiment 文件真实 mtime 选 latest，结果冻结 baseline hashes；late-lock 分支要求 baseline SHA 相同、未读取结果声明、deviation 尚未锁定、blind calculation 存在，并只放行 deviation 列出的已启动 Plan 目录，锁定前后都会验证路径集合一致。默认覆盖保护与同条件 Plan existence guard 保留；已记录 attempt 是一个狭窄、显式且哈希绑定的 late-lock 分支，不是一般跳过。

本次修正没有改变先前的方法结论：两项已启动 Plan 都按正常功能有效性判断，所有费用和调用计入各组分子，Completed 且其它条件满足者进入有效完成分母；不得为 late-lock 伪称原顺序严格预注册。baseline 与 gate 数值未改，余下四份继续按原样本定义执行。late-lock 可支持透明的探索性工程比较，不增加额外重复，也不宣称统计显著或一般性能结论。

## 增补复审：Pro 调用计量、Node 夹具归因与 known Plan 重复样本

### 做了什么

只读检查 `comparison/evaluate_results.py`、`thresholds-pro.json`、`model-change-pro/registration-deviation.json`、Node 44 失败诊断，以及 Leader 报告的已解盲 Pro known-path 结果和拟议追加样本方案；未运行测试或模型，也未读取正在运行的 candidate Plan 结果。

### 规格轴

**调用数 gate 的证据条件已收紧，统计方向正确。** `evaluate_results.py` 现要求每个匹配 attempt 都有 sessionId、`modelCalls > 0` 且没有 `accountingError`，才将 `actualCallAccountingComplete` 置为真；只有该条件成立时 `fewerActualModelCalls` 才可能通过。缺失 session 或账本错误不会再因空值被 `sum(... or 0)` 当成低调用数。对调用数 gate 而言这是保守的完整性要求；咨询、调用、token、时间的分子仍汇总所有条件匹配 attempt，分母仍是有效完成数。结果同时输出 `registrationTiming` 与 `registrationDeviationFile`，让晚锁偏差随 evaluator 结果传播。货币成本仍明确为 unavailable。

**late-lock 两项记录与固定数值一致。** deviation 列出 ShortPlan/6 与 RulePlan/5 两个在锁门槛前启动、锁前未解盲的 Plan；阈值文件声明 `late-lock-before-unblinding` 并引用同一 deviation。known-path 仍固定为 ≤2 咨询、≤5 模型调用、≤82,081 报告 token、≤27,745.0817 ms；baseline SHA 与此前盲算一致。没有证据显示这次追加方案改动 baseline 或 gate。

**每种 known Plan 模式各补两次，在限定条件下统计诚实，但应标记为结果触发的扩展样本。** 当前已知 ShortPlan/6 为 2 咨询、4 调用、48,715 token、28.942 秒，仅正式时间 gate 超过冻结的 27.745 秒；RulePlan 目前报告为通过。由于追加决定是在看到 ShortPlan 时间结果后作出，不能把追加两次描述成原始前瞻样本计划，也不能只报告追加结果。对 ShortPlan 与 RulePlan 对称地各增加两次、在开跑前固定 repeat ID、跑完全部四次后统一汇总、无中途看数后停跑或改门槛，可作为透明的工程复核扩展；`evaluate_results.py` 对每个 mode 汇总全部匹配 attempts 的成本并除以有效完成数，适合这一口径。无效尝试照常留在成本分子且不入有效完成分母。

这不会补足单次 known Step baseline 的抽样不确定性，也不能据此称为统计显著、稳定的时间收益或普遍性能提升。报告应保留单个 1.2 秒左右时间超限的事实，并把扩展目的表述为检查该边界表现是否重复；不能把 Rule 对称补样说成由其结果失败触发。当前 thresholds-pro 的 `requiredValidSamplesPerPlanMode` 仍为 1，因此四个追加 trial 是固定的诊断性复核次数，不等同于 gate 要求三份有效 Plan 样本；若任何一次无效，需如实保留并依既定样本计划继续补足，不应只挑有利的三份计算。

**Node 43/44 归因是测试清理夹具问题，产品语义尚待 GREEN 证据。** `protocol/final44-fixture-diagnosis.md` 将 PRF-003 的空 promptCalls 追溯到 PRF-009 测试直接调用 `plugin.apply`、未注册 `activeHarness`，致使测试内触发的真实 retry 屏障未由 afterEach 经公开 detach 清理。拟议改动只为该测试登记 harness，使既有清理钩子执行；不绕过本测试中的物理屏障。现有记录只有 43/44 RED 及根因，修改后完整 44 项尚未验证，所以这是待验证的夹具修复，不构成本轮产品阻塞，也不能提前称 GREEN。

### 规范轴

本次评审边界遵守只读要求；没有运行任何测试、设备或模型。追加已解盲结果后，仍须在剩余 Pro Plan 结果解盲前固定四个新 repeat 的身份和完成规则，并将“观察到 Short 时间超限后追加对称复核”写入 deviation/结果说明。阈值、baseline、模式或口径均不得随结果改动。candidate 正在运行，未查看其结果。

### 结论

发现的计量修正是保守且符合“实际调用必须可审计”的目标；late-lock 证据仍能与两个在锁前启动的 Plan 对齐。known ShortPlan 首样仅时间超限，不能单独证明时间回退稳定；每模式等量追加两次可作为明确披露的探索性复核，不是原预注册样本，也不支持显著性结论。只要预先固定全部四次、完整报告并按原 evaluator 汇总所有尝试成本，未发现需要改变门槛或判废样本的统计诚信阻塞。Node 夹具修复须等全量 44/44 实际通过后才能关闭验证项。

## 终局复审：Pro 全部 Plan 结果与 Node 44 项 TAP

### 做了什么

只读复核 `comparison-result-pro.json`、`model-change-pro/results.md`、`thresholds-pro.json`、model-selection audit、known-path overhead audit、late-lock deviation、evaluator 与 `plugin-tests-final44-green.txt`。检查了 plugin fixture 的精确改动位置；未运行测试、设备或模型。

### 规格轴

**Pro 结果摘要算术与门槛判断一致。** 四个 Plan 组均有效完成：known ShortPlan 3/3，平均 2.333 咨询、5.667 调用、75,613 token、32,692.58 ms；其咨询、调用、正式时延门槛失败，token 不回退。known RulePlan 3/3，2 咨询、4.333 调用、51,829 token、30,369.33 ms；咨询、调用、token 通过，正式时延失败。candidates ShortPlan 2/2，2.5 咨询、5 调用、74,586.5 token、37,262.21 ms；candidates RulePlan 2/2，2 咨询、5 调用、65,437 token、39,142.10 ms；两组均满足各自全部 Plan gates。均值可由 `comparison-result-pro.json` 的 totals 除以有效完成数复算。摘要没有把 candidates 的两组 PASS 外推到 known-path 或整个计划。

**成本与实际模型身份的账本边界清楚。** Pro 13 个 session 在 `formal-model-selection-audit.json` 中均选用 deepseek-official/deepseek-v4-pro、reasoning off，逐调用 usage 均为正；其 77 个调用数与六组 Pro trials 的 modelCalls 合计相符。全历史账本仍为 49 attempts、40 valid、331 calls、7,900,624 reported-token lower bound，并保留 13 次修复前调用的缺失 usage；没有把未知补成零。货币费用未提供，不能声称现金节省。实际调用完整性门槛仍要求每个 attempt 存在 session、正 modelCalls 且无 accountingError。

**known ShortPlan 超额有具体交互路径证据，不能从组均值剔除。** `known-path-overhead-audit.json` 显示 ShortPlan/7 主动插入 Control Reobserve，触发对原计划尾部的中断；随后模型再次提交 Act 并 NoAction，另有 no-pending 与缺 decisionId 拒绝，最终带来额外咨询、调用与耗时。Control Reobserve 与 Plan 的 Observe 语义不同；记录没有把额外交互误报为 Runtime 自动观察。ShortPlan/6、7、8 全部保持在组分子与有效完成分母中，组均值因此仍高于 frozen 时间门槛。结果说明将这一现象作为慢样本归因，同时保留“模型/输出回合耗时有波动”的其他背景，未据此单次过拟合 Prompt 或改阈值。

**样本扩展与 late-lock 披露相互一致。** 两个锁前启动的 known Plan 是 ShortPlan/6、RulePlan/5，阈值在二者结果解盲前以固定 baseline 和公式锁定；这仍是有明确流程偏差的 late lock，不是严格 pre-Plan 注册。随后 known 两模式各追加两次，是 Short 首样时间超限后作出的对称、固定探索性扩展；四次全部完成，旧样本和失败成本均留在汇总。结果说明明确该动机和顺序，且没有显著性主张。`thresholds-pro.json` 中 baseline SHA、model/provider artifact、Prompt 17、程序与权限/观察条件保持冻结；门槛未被修改。

**fixture 与 evaluator 语义未发现回避失败的迹象。** `plugin.test.mjs` 的 per-turn Skill 直接挂载测试新增一行 `activeHarness = harness`，使原有 afterEach 通过公开 detach 清理测试触发的 retry；真实 retry/物理 turn fence 仍在该测试内生效。未见修改产品端屏障以让测试通过。`evaluate_results.py` 仍把所有匹配尝试成本计入分子、按有效完成数摊销，并在会话账本不完整时阻止 actual-call gate 通过。

### 规范轴

`plugin-tests-final44-green.txt` 的 TAP 汇总列出 `# tests 44`、`# pass 44`、`# fail 0`、`# skipped 0`；独立的 `plugin-final-result.json` 又记录 Leader 从 Node 进程取得 `exitCode: 0`，并将该 TAP 日志与此前失败日志分别关联。因此 44/44 通过与进程正常退出两项均有证据，不是仅凭 case 44 或 TAP 推断退出状态。JSON 同时记录末次修复未改生产源码。

### 结论

现场功能完成不等于性能验收完成：known ShortPlan 与 RulePlan 都未过全部性能 gates，candidates 两组通过只能支持已测 2 候选/4 动作集合。按现有数字、审计与固定门槛，Pro 比较的统计归因和成本计量口径一致；known Short 的 Reobserve/重复提交/拒绝成本是可追溯的失败机制。late-lock 与结果触发的对称追加已披露为方法偏差/探索性样本，没有被伪装成前瞻注册或统计显著结论。fixture 单行清理修复现有 TAP 44/44 pass，且独立进程结果为 exit 0。故 RUNTIME-PLAN-001 保持 VERIFY，不关闭，也不因 candidates PASS 外推全通过。
