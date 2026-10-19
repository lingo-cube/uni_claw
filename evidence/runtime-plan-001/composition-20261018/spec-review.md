# RUNTIME-PLAN-001 组合规格只读审查

范围：仅核对入口快照后 `inventory.json` 所列29个增量及当前源码；未运行构建、测试、模型或设备。

未发现可证实的本轮阻塞缺陷。`If/Repeat` 的三态出口、块帧/候选恢复、ControlPath 精确续行、版本递增及在途 Effect 的验证屏障均有对应实现和定向用例（`KernelRunDriver.cs:1374-1403,1996-2036,2587-2597`；`KernelRunDriverPlanTests.cs:353-421`）。Host 只投影已公布的 source/condition，未见把场景循环藏入 Host 或赋予 condition Effect 权限（`SettingsPlanFacts.cs:69-117`）。

仍有一项部分满足/范围一致性缺口：`composition-contract.md:21,28` 要求当前页候选与允许范围同源。`SettingsPlanFacts.ProjectCurrent` 在达到 `MaxDepth` 后正确将 Plan source 截断为空（`SettingsPlanFacts.cs:94-102`；`maxDepth=1` 允许一级页内的一层 `nested-enter`，既有测试已固定此语义），但 `SettingsCoverageLedger.DynamicNestedCandidates/Frontier` 不消费深度，仍会把更深页观察到的入口写入 frontier/report（`SettingsCoverageLedger.cs:844-855,902-920`）。反例：root→Network→Private DNS 后在 Private DNS 发现 C；Plan 的 `settings.current-navigation` 为空，报告却可能把 `Private DNS>C` 列 pending。它不造成越界 dispatch，却会使 Agent/人看到的候选范围与可执行 source 不一致；应统一投影或明确 report 为原始未裁剪发现。

未见超出规格的任意代码、递归、第二 Driver 或 Host 调度；也不能把现有仿真六动作二咨询、switch 仅记录，表述为本增量设备、模型或性能验证。

## 修复复核

上述范围缺口已结清：`KnownNavigationDepths` 只从已验证、可计覆盖的导航边推导深度（`SettingsCoverageLedger.cs:902-929`）；候选、frontier、visited 与 skip 投影共同经该深度筛选（`SettingsCoverageLedger.cs:844-951`），`SettingsPlanFacts` 也读取同一投影（`SettingsPlanFacts.cs:94-118`）。因此 `maxDepth=1` 仍允许 A→B，而 B 页新见 C 同时不会出现在 report frontier 或 Plan source；新增反例见 `SettingsPlanFactsTests.cs:38-67`。无关联的旧观察没有被编造深度或完成结论；进入当前页时仍先由 verified navigation path 失败关闭（`SettingsPlanFacts.cs:94-103`）。

`RecordBuyer_…(conditional: true)` 静态可见：其 `IfItem` 由候选绑定的只读 condition 决定，随后仍走同一 `ForEach`/guard/验证 Driver（`KernelRunDriverPlanTests.cs:889-1020`），未新增记录场景专用执行器。本次仅核读；`depth-green 2/2` 与 Host `343/343` 是 Leader 提供的验证结果，未由本审查重跑。

## 现场注册核对

发现两项会失真，当前不得据此宣布任一 Plan 组通过。第一，`evaluate()` 把 Plan 缺失的 `formalElapsedMilliseconds` 和 `providerReportedTokens.totalTokens` 分别当作 0（`live_batch.py:142-149`），而 `complete` 仅要求 `tokenAccountingComplete`，不要求这两个数存在（`:137-145`）。因此一个功能有效、usage 块存在但没有 `totalTokens` 或正式计时的 Plan trial，可借零 token/零时延通过相应收益门；这与注册的「完整 session usage 可得、正式耗时上限」相冲突（`live-registration.md:10-11`）。基线端反而直接索引这两个字段（`:116-117`），形成不对称。应把缺失量度判失败/不可评估，不能归零。

第二，完整条件只在 `run()` 已执行 Plan 后比较（`live_batch.py:123-128`）；运行前 `check_transport()` 只校验 PID、launch-env 与 provider artifact（`:42-48`），不校验冻结的 prompt/profile/Kernel/Host/schema 等 `CONDITION_KEYS`（`:10-13`）。故这些输入漂移会先产生一份 Plan trial，之后才停止，未满足注册「fingerprint 漂移即停止」（`live-registration.md:13`）的运行前含义。现有脚本会终止而不是自动把它计入结果，仍应在发起 Plan 前对冻结条件做预检或将该试次明确排除。

## 后续修订复核

现场计量缺失门已修正：`accounted()` 同时要求正模型调用、每次调用有 usage、正 `totalTokens` 与正正式耗时；freeze 和 Plan 评估均以它为前提（`live_batch.py:42-48,123-146,157-179`），缺失值不再归零。Plan 调用前还将当前 DLL、profile/prompt、任务配置、decision channel 与 schema 的逐文件 hash 对比 freeze 记录（`:49-70,145-146`），在创建 trial 目录和调用测试前失败关闭。静态上已结清前两项注册缺陷；当前仅有五份 Step、nested-scope 无有效样本且无 `thresholds.json`、无 Plan trial，符合基线不足即停，未见旧门槛被覆盖。

RouteKey 修订属于 Host 对观察 XML 的页面身份投影，未触及 Kernel、wire、Effect 或权限：根页 `homepage_title` 优先，随后读取 `collapsing_toolbar.content-desc`，最后保持 `android:id/title` 的旧页回退，无法解析仍为 `android.settings`（`SettingsTraversalLiveFeed.cs:304-374`）。这避免列表首行冒充页标题；Security & privacy 既有语料及两个新 XML 都断言为 `src=collapsing_toolbar`，根页稳定性与无标题回退仍有用例（`SettingsRouteKeyTests.cs:51-95`）。未见越权或职责倒置。每页 nested 数量在重复 Observation 下的反例仍在补，尚无可审增量，未把它计入本结论。

## nested 每页上限复核

修复与注册范围一致：普通 `DynamicNestedCandidates` 以 route 为键在整个 Run 累计 `pageCounts`，因此 `maxEntriesPerPage=1` 时同页两次观察 Internet/SIMs 只保留先发现的 Internet；新增反例直接覆盖这一行为（`SettingsCoverageLedger.cs:931-955`；`SettingsCoverageLedgerTests.cs:266-276`）。深度过滤、已访问/已到达/Owner skip 过滤仍在其外层 projection 执行（`SettingsCoverageLedger.cs:844-899`），候选仅是 Host 的只读发现集合，未写入 World、未授予动作许可，也未改变 Kernel 的逐动作 guard/验证链。

`includeSkipped=true` 仅供既有 skip-reason/失败入口审计保留全观察投影；普通可执行 frontier 不走该分支，故不会借审计条目突破每页候选上限。静态未见事实或许可转移。Leader报告 Host `345/346` 尚有已知旧 RouteKey 断言失败、重跑未完成；本审查不将该部分视为通过。

## 对照任务公平性复核

修订已消除模式间的目标文本差异：`RuntimePlanComparisonLiveTests` 在配置后生成唯一 `scopedObjective`，所有 Step、ShortPlan、ComposedPlan 均以 `context.Objective + scopedObjective + 各自形态说明` 调用 Agent，且将同一 scope 写入 `experiment.goal`（`RuntimePlanComparisonLiveTests.cs:72-96,120-130`）。nested 目标明确要求每个一级页一项允许的动态子入口、一级页之后一层、逐层回根，并明确 frontier 不能忽略；known 目标明确不进入子页。各 mode 的分支仅约束决策产物形态（逐步、有限叶步骤、If/Repeat），没有改变 targets、深度、开关政策、Owner coverage 或有效性判据（`:122-125,198-207`）。因此 ShortPlan 无法满足动态发现时只能如实失败，不构成对它的任务放宽。

新批次的 `live_batch.py` 只枚举 `composition-r18-commongoal-` 前缀；基线必须对两个 task 各取2个有效样本、最多3次，freeze 明确拒绝已有 Plan，随后才固定8个 Plan（`common-goal-epoch/live_batch.py:7-10,112-153`）。当前原始目录只有3个该前缀的 Step `trial.json`，未见该前缀 Plan；不能把它表述为比较结论。旧失败未被覆盖：旧 epoch 的5个 trial/60次调用/1,808,383 token 保留在根目录账本，route-fix 的5个 trial/43次调用/822,216 token 留在其独立 stopped 账本；两者均未被新前缀纳入分母（`common-goal-epoch/live-registration.md:15-17`）。未见阈值绕过、Plan 后改目标或隐去失败成本。

## 固定批次结果与措辞复核

结果文件的判定无伪通过：4份 Step 均有效后才冻结 `thresholds.json`；8份固定 Plan 中5份满足 `Completed + CoverageComplete + modeConformant`，6份完成行为范围（`live-metrics.json`；`formal-audit.json`）。known/Composed 的两份均1次咨询，均值为2.5次实际模型调用、24,018.5 token、18,483.04075ms，且逐项低于已冻结的2/5/84,198/21,795.7851门槛，故这是唯一2/2正式通过组。known/Short虽2/2功能有效，四项收益门均未通过，不能称为通过。

nested/Composed-1 是 `TerminalNotProven/BoundedStop`、仅1个已验证动作，必须作为功能失败保留；nested/Composed-2 则真实完成 `Completed/CoverageComplete` 的8个验证动作、2咨询/4调用/71,445 token/51,020.7904ms，并消费两个当前页候选投影、无 switch 点击（`formal-audit.json`）。但其首次 Plan 为 `ForEach+If` 而没有预注册的 `Repeat`，`modeConformant=false`；它只能写作一次现场未知子入口展开的行为证据，不能回填为正式 PASS 或泛化完成证据。nested/Short 为1/2正式有效，也不是通过组。

`live-result.json` 对有效数为0的组按原始两次总量除以 `max(valid,1)` 计分；有效数为1的组也包含失败试次成本。因此 results 不得把这些 `values` 写成“每个有效完成的均值”：应明确是注册评分聚合，且0有效组无有效均值。审查时 `results.md` 尚未存在，故未对其成稿措辞背书；以上区分必须写入。12会话共109次调用均有正 usage、`slowModelEnabled=false`，实际模型选择为 `deepseek-flash`，12/12 有本次直连 socket 见证；两份旧失败 epoch 仍为独立保留账本，未混入本轮成功分母。

## 成稿一致性复核

`results.md`、Change State/Verification 顶部与末尾、ADR-0053 末尾均与冻结 JSON 一致：固定8个 Plan 为正式有效5/8、实际范围完成6/8，且只有 known/Composed 2/2 同时通过功能与全部收益门（`common-goal-epoch/live-result.json`；`results.md` 验证表及数值表；`state.md:8`；`verification.md:7`；`ADR-0053:47-51`）。nested/Composed-2 的8个验证动作、2咨询与51.0207904秒被正确限定为行为实例；其初始提案缺 Repeat，仍为预注册形态失败，未被写成 PASS 或稳定泛化。

成稿还正确区分计分聚合与均值：nested/Composed 0/2 行不展示有效完成均值，并解释原 `live-result` 的4咨询/9调用/134,987 token/71.06秒是两次总量；nested/Short 的188.19秒等数值为两次固定尝试全部成本除以1个正式有效完成，并非成功样本本身的均值（`results.md` 数值表及其后说明）。未见遗漏失败、功能/形态混写或事后门槛调整。
