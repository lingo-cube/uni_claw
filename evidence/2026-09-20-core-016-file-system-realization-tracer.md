# CORE-016 — 文件系统域 Core 契约 realization tracer 验证记录

日期：2026-09-20 · Level：SCENARIO（含 CONTRACT/DETERMINISTIC 子项）

## 交付物

- `tests/UniClaw.FileSystemRealization.Tests/`：FileSystemWorld（自持
  identity/观察历史，零 Kernel/Core 引用）、FileSystemCoreProjection
  （唯一投影缝；有效性判定在此完成）、FileSystemRealizationTests
  （S1–S8 + closure，9 测试）、csproj（ProjectReference 仅 UniClaw.Core）
- `UniClaw.Kernel.slnx`：新程序集入 solution（REVIEW F1 修复——worker
  提交时遗漏，导致全量跑不含该程序集）

## Leader REVIEW（第一手代码审阅 + 复核 worker 自报）

| 发现 | 级别 | 处置 |
|---|---|---|
| F1 新测试程序集未入 solution，全量回归不执行 | blocker | 已修：slnx 增行；复跑全量确认 9/9 进入套件 |
| F2 S8 指纹窄于 spec「至少比较」清单（缺 Binding 固定依据/可发送性/Unknown） | minor | 已修：S8 扩为含 binding+attempt 的双序指纹 |
| F3 Kernel DocsMetadataTests 失败 | 非本 Change | 债主 GATE-001（proposal 文档缺 `> Status:/-> Authority:` 头）；本会话补头修复，407/407 |
| worker 自报「S6 freshness 归 realization」「S3 append-only」「Attempt 字段 null」「identity world 自持」 | 核实 | 与 spec v0.2 逐条相符（EvaluateCurrentBinding 用 `with` 派生、历史不动） |

## Verification

```yaml
scenario_suite:
  level: SCENARIO
  method: dotnet test 全量第一手复跑（Leader 本机，solution 含新程序集）
  expected: S1–S8 + closure 全过；全套件零失败
  actual: FileSystemRealization 9/9；Core 14/14；Agent 17/17；Simulation
    132/132；Kernel 407/407——共 579/579
  evidence: 本文件 + 本地测试运行（2026-09-20）
closure_contract:
  level: CONTRACT
  method: GetReferencedAssemblies 白名单（closure 测试）
  expected: UniClaw.* 引用集合 == {UniClaw.Core}
  actual: 通过（closure 测试 GREEN）
  evidence: FileSystemRealizationTests.Closure_TracerAssemblyReferencesOnlyCoreAmongUniClawAssemblies
zero_product_code:
  level: CONTRACT
  method: git status 范围核对
  expected: 改动仅 tests/ 新程序集 + slnx + docs 头（GATE-001 债）+ changes/evidence
  actual: 属实；src/ 零改动
  evidence: 提交 fe627056..HEAD 的文件清单
spec_conformance:
  level: CONTRACT
  method: spec v0.2 §3/§8 逐场景对照测试体
  expected: S1–S8 语义与验收映射 1–6 承载齐全
  actual: 齐全（S6 有效性判定→投影 Stale→CanDispatch=false 链条完整；
    S8 三同限定+必要语义指纹；S3 更正只追加）
  evidence: FileSystemRealizationTests.cs
```

## 检验结论（对本 Change 检验主张的回应）

「Core 候选形状在第二域可表达且演化保持」**未被证伪**：四记录
（Segment/Slice/Evidence/Claim）+ 非 Slice ResourceVersion binding +
Attempt 最小面全部按契约投影成立，无反例。注意：这是**通过**，不是
**证明跨领域充分**——Clause 无买家未用、Attempt 执行字段未接、单第二域。
不触发 Core 冻结或程序集升格（ADR-0024 条件不变）。
