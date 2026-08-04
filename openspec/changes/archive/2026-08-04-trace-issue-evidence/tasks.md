# Tasks — trace-issue-evidence

## 1. TraceRun 聚合 issues.jsonl（D-1 / D-2）

- [x] 1.1 TraceRun 新增只读 `Issues` 集合属性（`IReadOnlyList<RunIssue>`，复用 Host.Artifacts.RunIssue，默认空）
- [x] 1.2 TraceRunLoader 加载 run 时检测 `issues.jsonl`：存在则逐行 `JsonSerializer.Deserialize<RunIssue>`，坏行跳过；缺失/全坏行 → 空集合，不 fail 加载

## 2. DiagnoseEngine fallback（D-3 / D-4）

- [x] 2.1 `result.IssueFingerprints` 为空且 Issues 非空且存在可用 fingerprint → 追加 `issue_fingerprints` evidence（文本含 fingerprint + detail，标注 issues.jsonl 来源）
- [x] 2.2 result 指纹非空时保持现状（issues 不重复）；issues 全无可用 fingerprint → 不产出空指纹条目

## 3. 测试（spec 场景全覆盖）

- [x] 3.1 TraceRunTests：issues.jsonl 存在 → N 条记录字段完整；缺失 → 空集合加载成功；坏行 → 跳过
- [x] 3.2 DiagnoseTests：verification 失败 run（result 指纹空 + issues 有 detail）→ issue_fingerprints evidence 含指纹与 detail、confidence=medium
- [x] 3.3 DiagnoseTests：result 指纹非空 + issues 存在 → 不重复；issues 无可用指纹 → 无空条目

## 4. 验证

- [x] 4.1 全 solution build 0 errors + 全量测试通过（TraceTool 32+ 新测试；Host/Core 零改动回归）
- [x] 4.2 端到端冒烟：真实 run（locate-one-item verification 失败）→ diagnose 输出含 issue_fingerprints evidence（指纹 271c8e6c949909e032f0 + summary 内嵌 detail），confidence=medium
