# Design — trace-issue-evidence

## Context

D-192 之后，verification 失败（如 `target_page_identity_not_verified`）通过 `issueSink` 写入 `issues.jsonl`（fingerprint = SHA256(category|phase|summary)[..20] + detail 带引擎原因），但 **outcome 的 `IssueFingerprints` 全程无回填**——`ScenarioCompletionVerifier` 只 `outcome with { Status/CompletionReason/... }`，不更新指纹；`VerificationAnalyzer` 构造时的 `issues` builder 收集的是 page 名（且仅 enumerate/verify 路径）。结果：`result.json.issueFingerprints` 恒空 → DiagnoseEngine [L157-163] 的 `issue_fingerprints` evidence 永不命中 → `confidence = evidence.Count > 0 ? "medium" : "low"` [L174] 恒 low，真实原因（issue detail）只存在于 issues.jsonl。

Stakeholders: 顶层统筹（Agent 排查失败 run）、TraceTool 消费侧（诊断工具）、后续源头修正 change。

## Goals / Non-Goals

**Goals**
- `diagnose` 对 verification 类失败恢复 evidence 链：issue_fingerprints evidence 携带指纹 + detail
- confidence 从恒 low 恢复为 evidence 驱动的正常计算（medium）
- 对**存量 run 立即生效**（历史 run 的 issues.jsonl 已存在）
- 保持"TraceRun 是 run 目录唯一入口"（TraceRunLoader 聚合一切产物）

**Non-Goals**
- 不改 Host 生产代码（issueSink 回填 outcome = 源头修正，留作独立 change，本 change 只做只读侧补齐）
- 不改 JSON 契约（schemaVersion "1"、退出码、evidence 上限均不变）
- 不做 issues.jsonl 的 CLI 直读（仍走 TraceRun 聚合，禁止子命令绕过聚合层）

## Decisions

### D-1: issues.jsonl 由 TraceRunLoader 聚合，DiagnoseEngine 不直接读文件

TraceRun 新增 `IReadOnlyList<RunIssueRecord>`（或等价集合），TraceRunLoader 在加载 run 目录时检测 `issues.jsonl` 存在则逐行反序列化。DiagnoseEngine 只消费 TraceRun 暴露的集合。

- **备选 A**：DiagnoseEngine 直接读 `{runDir}/issues.jsonl` —— 破坏"Subcommands SHALL NOT read files directly"（trace-run-aggregate spec 既有要求）。
- **选 TraceRunLoader 聚合**：与 result.json/manifest.json/trace/steps 同构，聚合层单点。

### D-2: 直接复用 Host `RunIssue` record，坏行跳过

issues.jsonl 每行是 Host `RunIssue` 的 System.Text.Json 序列化（schemaVersion "1"）。**TraceTool 已引用 UniClaw.Host 且 `RunManifest`/`RunResult` 同为直接复用**（TraceRun.cs 同源消费）——issues 沿用同模式：`JsonSerializer.Deserialize<RunIssue>`，未知字段忽略，坏行跳过（与 result.json 缺失/坏行降级语义一致，不 fail 加载）。

- **备选 A**：TraceTool 侧镜像 record（Fingerprint/Summary/Detail/...）——引入字段漂移风险，且与既有"直接复用 Host.Artifacts 类型"模式不一致。
- **选直接复用 RunIssue**：零新增依赖、字段契约由单一类型定义（RunAssets.cs:111），无漂移。

### D-3: fallback 条件 = result 指纹为空 + issues 存在，非空不重复

`result.IssueFingerprints` 非空 → 保持现状（透传 result，issues 不重复追加）；result 为空 → 若 issues 集合有可用 fingerprint 则追加一条 `issue_fingerprints` evidence，文本 = `fingerprint: detail`（fingerprint 与 detail 都来自同一 issue 记录）。issues 存在但全部无可用 fingerprint → 不产出空指纹条目（spec 场景 3）。

### D-4: 证据来源标注（detail 溯源）

evidence 文本含 issue detail 而非仅 fingerprint——这是本 change 的核心价值（真实原因可消费）。字段内保留 `detail` 原文（如 `Post-action page identity '<empty>' did not match the scenario success identities.`）。必要时 evidence 文本加 `(issues.jsonl)` 前缀区分来源，不引入新 evidence type。

## Risks / Trade-offs

- [issues.jsonl 与 result.json 指纹未来双写（源头修正 change 落地后）] → D-3 的"非空不重复"规则天然幂等：result 有指纹后 issues fallback 自动停用，无需本 change 后续改动。
- [镜像 record 与 Host RunIssue 字段漂移] → 双端由 openspec/specs/run-metadata-enrichment 与 D-2 的 JSON 契约约束；字段变化需同步镜像（TraceTool 测试含字段级断言）。
- [坏行静默跳过掩盖数据问题] → 与 result.json 语义一致（D-93/现有降级契约）；issues 计数可在 load 时保留（数量与行数差异可察觉），不引入新警告机制。

## Migration Plan

纯增量：TraceRun 加只读属性（默认空集合，向后兼容）；DiagnoseEngine fallback 不影响现有路径（result 非空时行为不变）。无需数据迁移，无部署顺序要求。

## Open Questions

（无——源头修正（Host 回填）明确留作后续 change，不在本 change 范围）
