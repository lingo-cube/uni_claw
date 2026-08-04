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
