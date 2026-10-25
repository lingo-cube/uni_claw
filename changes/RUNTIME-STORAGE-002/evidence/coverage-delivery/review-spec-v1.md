# Coverage Spec Review v1

范围：仅审阅 `coverage-delivery/candidate.patch` 与 `candidate.json` 固定的 20 个文件；按 `coverage-delivery-contract.md`、接续 Coverage 段及 `source-uses.md` 对照。未运行构建、测试或产品流程。

## Findings

1. **P1：原 RunId 没有被验证，完整来源身份绑定可被绕过。** 契约要求 `coverage-source.json` 同时绑定 coverage-run 目录、原 RunId 和三种 recording 身份（`coverage-delivery-contract.md:8`；`source-uses.md:5`）。但 `CoverageReportSourceIdentity.RunId` 可空，`SaveMaterialAsync` 只校验三个 recording ID（`src/UniClaw.Host/Runtime/CoverageReportDelivery.Sources.cs:29-32`）；固定生成器也只循环这三个字段（`CoverageReportDelivery.Generator.cs:21-24`）。因此，重试前若固定来源的 `runId` 缺失或被替换，仍可通过生成并发布，未满足已声明的源/输出 identity 绑定。应将 RunId 设为不可空且在保存和生成器的 pinned-input 校验中验证其格式/与来源的绑定。

2. **P1：大小写仅不同的显式附件会在保存唯一来源时静默合并，不能逐路径完整保全。** 接受阶段只用 `StringComparer.Ordinal` 去重（`RequiredReportDelivery.cs:466-473`），但 Capture 使用 `OrdinalIgnoreCase` 字典，并把冲突项合并为一个 `SourceAttachment`（`CoverageReportDelivery.Sources.cs:41,67-78`）。在大小写敏感的产品根，配置 `evidence/A.xml` 和 `evidence/a.xml`（均可为必需、不同文件）会被接受却只进入一个归档成员；在大小写不敏感根，语义也依赖于未显式拒绝的歧义。这违背附件按路径/用途/必要性登记且归档清单与 SHA 一并保全的契约（`coverage-delivery-contract.md:9`）。应在接受时按与捕获/ZIP一致的大小写折叠规则拒绝该类重复，或保留两个完全独立、可验证的路径项。

结论：本次限定 Spec 审查 2 项可复现缺口；其余要求（提前接受、私有 pinned 输入后的仅报告重试、交付与 Outcome 隔离、确认后投影及投影故障单报）在固定候选中有对应实现，未见额外范围问题。
