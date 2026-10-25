# Coverage Spec Review v2

范围：只审阅更新后的 `coverage-delivery/candidate.patch`、`candidate.json` 锁定的 22 文件，以及 Coverage 契约/接续/来源说明；22/22 当前 SHA 与清单一致。未运行构建、测试或产品流程；`coverage-review-fixture-v1` 的首次 232 通过/1 fixture 断言失败只作为既有记录，不替代本审查的运行结论。

## 对 v1 findings 的复核

1. **已修复：Kernel 原 RunId 的固定来源身份校验。** `CoverageReportSourceIdentity.RunId` 已不可空；保存时拒绝 null/空白/首尾空格/超长值（`CoverageReportDelivery.Sources.cs:25-33`），已有 `coverage-source.json` 的 material 不能替换（:35-40），重新固定前也再次验证（:92-101）。pinned Python 在生成前作同一 RunId 检查（`CoverageReportDelivery.Generator.cs:21-27`），并有无效 ID、未固定 material 替换、篡改原 source 的测试覆盖（`RequiredReportCoverageIdentityTests.cs:13-69`）。这符合“Require 早于执行而 Kernel RunId 尚未知”的边界：不把目录名冒充 RunId，而在唯一 material 出现时固定并验证。

2. **已修复：附件大小写歧义。** 接受阶段按 `OrdinalIgnoreCase` 拒绝显式路径冲突（`RequiredReportDelivery.cs:466-473`）；目录枚举中仍出现大小写变体会抛出明确 case-conflict，而非合并或遗漏（`CoverageReportDelivery.Sources.cs:68-82`）。回归用例在任务工作前验证显式冲突拒绝（`RequiredReportCoverageReviewTests.cs:14-27`）。

## 新增范围复核

Coverage 的 GeneratorPath 已移除，仅安装并固定 Host 内 canonical generator（`CoverageReportDelivery.cs:55-79`；`RequiredReportCoverageReviewTests.cs:8-12`）。共享有界进程执行器仍承载取消、关闭和子进程 lease；Coverage 重试复用该执行器，Kill probe 以其 `pythonExecutable` 代理制造真实生成窗口。未见其改变 Run Outcome、Effect/Agent 次数、或以重跑补齐缺失输入的路径。

结论：在这份 22 文件固定候选中，v1 两项均关闭；新增改动未发现额外可复现的 Spec 偏差。本结论限静态审查，实际运行验收仍以独立验证证据为准。
