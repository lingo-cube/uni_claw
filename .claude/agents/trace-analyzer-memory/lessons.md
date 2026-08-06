# trace-analyzer 案例经验

> 每次诊断后精简追加：日期 + 来源 + 事实/方法/局限。同主题合并，重复不追加，错误认知立即纠正删除。每条 ≤3 句。

## 2026-08-06 — 480af793 全帧 0 items：设备停在 Settings 搜索页，非视觉回归（e2e-dedup-vision-quality 首个 E2E run）

- "0 items 全帧 + all_visited 4 步终止"先别查视觉管线：独立 OCR 该 run 自己的 steps 截图（PIL+RapidOCR，与管线无关）判屏幕内容——本 run 截图只有状态栏 + "Search..."（y=0.077），且 dumpsys（只读）证实 com.android.settings.intelligence/.search.SearchActivity 至今仍 resumed、lastLaunchTime 比 run 早 36 分钟（APP_SEARCH_SETTINGS intent，adb am start 遗留）→ 是设备态残留，不是 crop=0/逆变换/Normalize 回归；视觉管线判断完全正确
- 空页 → entry.generate match_count=0 + ROI 路径失效（0 bbox 无 ROI）→ 走 legacy seen-set：swipe → scroll_empty_retry_1_of_2 → scroll_no_new_elements_end_reached → all_visited——引擎行为对空页完全正确；entry 的 AnalyzeUntilSettledAsync 门（≥3 items 或 hasScroll）被空页 hasScroll=true 直接放行，prep 无 force-stop/包断言是放大器
- 方法：诊断 0 items run 先 md5 截图（本 run 8 张全同，屏幕冻结）+ 独立 OCR + dumpsys lastLaunchTime（活性定位到 run 前 36 分钟的外部会话）；"哪个改动导致"必须区分 屏幕态（device）与 分析态（vision），vision 改动不可能改变屏幕内容

## 2026-08-04 — 裸 trace exit 3 是版本差异，不是损坏（bare-trace-test 验证）

- 2026-07-29 的 trace 有 6 行记录但 0 span（exit 3）：span 埋点引入于 ddfac50（2026-08-03）——早 5 天的 trace 无 span 是版本差异预期；判定用 `git log -S RecordTypeSpan` + 文件名时间戳比对
- 局限：CLI 无 raw 记录输出，无法区分"旧格式记录"与"全坏行"（坏行静默跳过无警告）；此缺口回报顶层（可加记录统计子命令），不手工解析 JSONL
- 下次：exit 3 先查版本时间线再下结论；外部 trace 一律先声明"无 run 上下文，verdict 不可推导"

## 2026-08-04 — Host verification 失败类 cause 的 evidence 陷阱（locate-one-item run）

- cause=target_page_identity_not_verified 等是 Host 覆写 reason（ScenarioCompletionVerifier），不是 Core CompletionReason 4 值（Timeout/MaxDepth/AllVisited/Incomplete，ContainerHandler.cs）——diagnose 的 cause 直接透传 result.completionReason，解读前先分清来源层
- result.json issueFingerprints 恒空（ScenarioRunOutcome.IssueFingerprints 无任何赋值点，issueSink 只写 issues.jsonl）→ DiagnoseEngine 的 issue_fingerprints evidence 永远缺失、confidence 恒 low；verification 类失败必须读 issues.jsonl 的 detail 才有真实原因（如 "Post-action page identity '<empty>'"）
- steps/ 资产可能只有 safety-decision.json（无 before/after/analysis）——screenshotPaths 空不异常；页面身份用 analysis.jsonl（D-197 快照）与 safety-decision 的 normalizedTarget 交叉验证引擎行为

## 2026-08-04 — issue_fingerprints evidence 已由 issues.jsonl 补全（trace-issue-evidence change）

- **该缺口已修复**：TraceRun.Issues 聚合 issues.jsonl（TraceRunLoader 逐行反序列化 RunIssue，坏行跳过），DiagnoseEngine 在 result 指纹空时 fallback → evidence `issues.jsonl: {fingerprint} — {summary}`，confidence low→medium；result 指纹非空时不重复
- RunIssue 契约（RunAssets.cs）**无 Detail 字段**——D-192 失败详情内嵌于 Summary（`target_page_identity_not_verified: <detail>`）；issue 的 fingerprint 是 SHA256(category|phase|summary)[..20]
- result.json 缺失 issueFingerprints 字段时 `ImmutableArray.Length` 会 NRE——判断用 `IsDefaultOrEmpty`，不要用 `{ Length: > 0 }`

## 2026-08-05 — verify 判定非幂等 + identity 链路三处断点（manual-roi-verify locate-one-item）

- verify 写回把 verdict.cause 写入 result.completionReason（TraceCommands.cs:803，引擎事实字段）→ 二次 verify 的 TargetActionExecuted（completionReason=="target_found"）翻转：首次 target_page_identity_not_verified → 复验 target_action_not_executed；验证判定前先查 result.completionReason 是否已被污染，用 /tmp 副本恢复 target_found 复验可还原首次判定
- analysis.jsonl 覆盖写（FileAssetStore.WriteAsync → AssetStagingWriter tmp+move 整文件替换）破坏 D-197 append-only 语义：5 次引擎分析只留 1 行；post-target 分析（HostCommands.cs:923）走 HostRunServices.VisualPageAnalyzer=raw provider（不经 AnalysisWritingDecorator）→ 目标页面快照永不落盘 → verify 只能判 click 前的页面
- LocateOneItemRule 空串 bug：IdentityMatches 的 normalizedExpected.Contains(normalizedActual) 在 actual="" 时恒 true（"".Contains 反向）→ 空 name item 短路 fallback → finalIdentity="" → 必 not_verified；修复=两侧 IsNullOrWhiteSpace 守卫
- delete-uia 后果：steps before/after.xml 随 UIA 移除（RunAssetHook.cs:18 注释），post-target 身份改视觉 AI（HostCommands.cs:921-922 注释明示）但未接证据链；session 结束后 post-target 记录写裸路径 trace/trace.jsonl（_currentTraceId 清空）→ 孤立占位资产，TraceRunLoader 不读

## 2026-08-05 — locate-one-item 5-run 对比：预滚动 + verify 幂等 + ROI 滚动缺口

- **run.log 的 `TraversalEngine: Engine terminated reason=` 是未污染引擎事实**（result.completionReason 已被 verify 写回覆盖，DiagnoseEngine verdict 的 completionReason 不可信）；确认 `Engine terminated reason=max_steps/all_visited/target_found steps=N` 与用户描述一致
- **判断滚动是否像素级有效的方法**：analysis.jsonl 相邻行的 items (name,x,y) 坐标签名全等 → 页面没动（6411 两次 scroll 后 row2==row10 全等 → swipe 无效）；签名变化 → 滚动有效。ROI 检测把"首屏 swipe 无效"当 EndReached（页面没变=三对相同）→ 引擎停止滚动 → all_visited，是真实缺口（6411）
- **swipe 可误触导航**：488d 无 click 记录但页面从 Settings 列表变 Wallpaper & style 子页（items=5 Choose wallpaper）→ swipe 落点落在菜单项触发导航；maxScrolls=6 来自 scenario.snapshot.json boundaries
- **LocateOneItemRule fallback 宽松匹配**：finalIdentity=Level1MenuNames.LastOrDefault→fallback Items 首个含 expected 子串的 name；成功 run 靠 "device"⊂"About device"（包含关系）通过 identityMatched——首次 verify（completionReason=target_found）→ verified 写回 target_page_identity_verified；复验（污染后）翻转 target_action_not_executed（TargetActionExecuted=completionReason=="target_found"&&actionsSucceeded>0，RunEvidenceLoader.cs:85）；VerifyEngine evidence 的 final_identity 恒显示 Level1MenuNames.LastOrDefault（'<none>'），与判定用 fallback 值不同，读 evidence 别误会
- 预滚动前后 run 起始页面相同（16 项 Settings 主页顶部）——预滚动解决的不是起始位置而是滚动可用性（3/3 成功含 cold-boot；未预滚动 0/2）

## 2026-08-05 — settings_home_not_restored run: max_steps 耗尽 + 身份链全程为空（bd5af64f）

- completionReason=settings_home_not_restored 是 Host 侧 verified_end_of_list 判定：引擎 max_steps=120 终止时仍在枚举（3 次 scroll 均 scroll_revealed_new_elements，25 发现/19 访问，endOfList 全程 false）；引擎终止时其实在 Settings 主页列表上（fingerprint 稳定 + 截图 md5 0117-0119 全同、0120 仅状态栏 0.018% 差异），但 level1MenuNames 在全部 150 行 analysis.jsonl 恒空 → finalIdentity '<empty>' ≠ Settings → 失败归因于身份链而非导航
- 步骤 85 点击 stray 项 "home,lock screen"（视觉模型误检/重复）打开搜索 IME（31 items=键盘键 ZXC/dfgｈjｋ），back 后 6 次 items=0 空白 ~8s → step 90 Click-on-Text 错误（cached 节点只有 text 无坐标，nodeId dyn_menu_container_Accessibility_root）→ ErrorHandler 重试 decide_next_action 撞 DeepSeek 400（deepseek-v4-flash-0731 已不被 API 接受，仅 45 次 ai_call 中 1 次失败）→ Unknown/Backtrack → back 恢复
- 最后点击（step117/118 target "Display, interaction, audio" 重复检测）为 no-op：verification_retry_single+verification_page_unchanged，但引擎前向模型仍注册 page_transition 到 Display 子页 + generate 8 子节点（模型/观测分歧）；该点击无 safety.click 记录（19 次 click 记录 vs 20 次执行，trace 缺口）
- Host bin 18:05 早于 TraversalEngine.cs@20:15 与 InterceptionHandler.cs@20:34 工作区改动——本 run 未覆盖当前引擎修复；FSM 文件 16:46-16:50 改动已含

## 2026-08-05 — enumerate 双入重访：幻影 OCR 子项 + D-G12 未触发 + D-G13 替换（2832a0487a）

- 根页 OCR 幻影副标题项（"Bluetooth, pairing" 位于 Connected devices 行内 y=0.579）被 DynamicMatch 当作可点子项生成 → 点击落到 Connected devices 页（fp 1213574877）；该 fp 在 step13/18/42 三次 verification 相同——"点击两次"实为 Connected devices 页被 3 次进入（1 直点 + 2 幻影项）
- 三个 OCR 变体对（逗号空格/空格差异）全部双入：Bluetooth, pairing↔Bluetooth,pairing（同 fp 1213574877）、Notification history, conversations↔Notification history,conversations（**异 fp**：881591846 Battery 页 vs -486945685 Notifications 页——第一步点偏）、Appsecurity↔App security（同页 Security&privacy 但 fp 不同 112375740 vs 426922695，仅 item type text/menuItem 差异）
- D-G12 child_destination_duplicate 在 trace.jsonl/run.log/analysis.jsonl 全部 0 命中——20/20 verification_passed 全部跟 safety.back（正常回退路径），dedup 从未触发；分析进行中（23:23）D-G12 已从工作区删除、被 D-G13（NormalizeItemText 生成期 nodeId 归一化）替换——同文本 OCR 变体将坍缩为同一 nodeId，是直接对症修复

## 2026-08-06 — enumerate run bc37815245f6462: child_control_execution_detected = 名称集合精确匹配失败（非身份）

- **guard 语义（ScenarioCompletionVerifier.cs，Host 侧 enumerate 完成判定）**：discovered = generate 记录 parentNodeId=="root" 的 childNodeId 经 ExtractRootEntry+Normalize；clickDecisions = safety 日志 click 决策的 NormalizedTarget；outOfScope = 不在 discovered 集合的点击。与页面身份无关——所有 19 次 verification_passed 全部通过（destination 身份恒为 Settings）。判定是 whitespace-only Normalize 后的 Ordinal 精确匹配
- **触发点击**：step 65 "darktheme,fontsize,brightness"（Dark theme 行）。根因：nodeId 文本（discovered 来源）被逗号空格归一化成 "Darktheme, fontsize, brightness"，而 click 决策 target（SafetyGate.Normalize 只坍缩空白）保持 "darktheme,fontsize,brightness" → "darktheme, fontsize, brightness" ≠ "darktheme,fontsize,brightness" → 1 个 out-of-scope → child_control_execution_detected；failedEntries=1 = outOfScopeClicks.Length；issue.stepNumber=120 是 run 长度（outcome.Steps）不是肇事步
- **D-G13（NormalizeItemText 生成期 nodeId 归一化）不闭合此缺口**：D-G13 只归一化 nodeId，click 决策 target 仍走 raw itemText + SafetyGate 仅 whitespace Normalize → 同一 OCR 变体（逗号空格）仍 mismatch，guard 会再次触发；修复需 SafetyGate/Verifier 的 Normalize 增加逗号空格归一化，或 click target 用归一化 nodeId 文本
- 方法：guard 归因不要猜身份，直接复现 verifier 的两集合 diff（discovered 23 vs click 19，交集 18）——差异即肇事点击；analysis.jsonl 无 identity 字段，身份证据在 verification_passed + safety pageIdentity metadata

## 2026-08-06 — D-G13 E2E 双问题定位（bc37815245f6462，max_steps 截断 + child_control 归一化误报）

- 根页 5 次 scroll 全部 scroll_revealed_new_elements（fp 逐次变化，滚动有效），但 152 行 analysis.jsonl isEndOfList 全 false——末次位置已见 "About emulated device"(y=0.912) 真列表底仍未置 true；枚举被 max_steps=120 截断而非 end 检测，GlobalFSM Traversing→Error=max_steps
- child_control_execution_detected 的 1 个 out-of-scope click = step65 "darktheme,fontsize,brightness"（无空格变体）vs 生成期 nodeId "Darktheme, fontsize, brightness"——Host Normalize 只 lowercase+去空白不归一逗号空格 → 归一化字符串不匹配误报，点击本身正确到达 Display 页；D-G13 修了 nodeId dedup（Connected devices 双入 3→2）但 click NormalizedTarget 管道仍分歧
- 坐标错位 = 副标题幻影项模式：19 clicks 中 5 个是 subtitle phantom（bluetooth pairing/darktheme/on 1 app/notification history/display interaction audio），2/5 真错位（step32 notification history→Battery fp881591846、step117 display interaction audio y=0.100→搜索 IME fp-1984822436，step114 首点 page_unchanged 重试）；3/5 落父行页变重复访问（fp 双现 rows 15+21 / 75+81 / 117+123）

## 2026-08-06 — 33-run 全量对比（scenario-enumerate）：引擎进步、验证层是当前唯一拦路层

- 时间线三阶段：Aug1（19 run 3 成功 13%）→ Aug2（17 run 0 成功）→ Aug5（8 run 0 成功）；Aug1-2 全部 pre-span run（exit 3 无法 diagnose）discovered=0、steps 0-38——失败是 infra/早期 Host 门（runtime_failure 10/click_did_not_leave_home 6/package_boundary 5），引擎根本跑不起来
- Aug5 六个 span run 引擎枚举已工作（discovered 23-25、visited 18-20、actions 43-44），失败全在 Host 侧验证层：5× settings_home_not_restored（同一 fingerprint e32ad8b9，level1MenuNames 在每 run 每行 analysis.jsonl 恒空 → finalIdentity '<empty>'）+ 1× child_control_execution_detected（dceb39fd 归一化误报）；criteria.json 在 enumerate run 恒不存在（该场景判定走 Host verifier+issues.jsonl，非 TraceTool verify，属预期）
- 系统性缺口：6 run 所有 analysis 行 isEndOfList 全 false（引擎从不自我检测列表底，4/6 run 靠 max_steps=120 截断）；all_visited 早停 run 有独立模式（59c81a71 steps=59 all_visited 但 21 scroll 只 visited 2/11、cb67eef steps=45 只 1/7）——滚动不转化为访问覆盖
- timeline 慢步全为 engine.step 且 aiCallCount=0（慢在视觉分析/操作 settle，非 LLM）；run.log "→ deny" 全是 action=wait deny.default（噪音，非 click 拒绝）；skill-test 3 run 全 interrupted（result 恒 run_initialized、无 Engine terminated 记录）无法判定
