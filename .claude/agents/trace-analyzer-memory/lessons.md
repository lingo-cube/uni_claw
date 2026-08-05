# trace-analyzer 案例经验

> 每次诊断后精简追加：日期 + 来源 + 事实/方法/局限。同主题合并，重复不追加，错误认知立即纠正删除。每条 ≤3 句。

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
