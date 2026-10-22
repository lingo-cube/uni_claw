# CHECKED-GUARD-001 独立 Review

范围：按 `entry/before-manifest.json` 所列 30 个原文件 before-image 与当前文件比较，并检查新增公开缝测试/fixture；manifest 记录其 SHA256 与写前 snapshot 相等。未使用 `/tmp` 中已被原位用于 GREEN/full 的 checkout 作为 before-image。

## Standards

**结论：无阻塞。** 产品改动限于 `SemanticCheckedResolver` 的消费门；`RuntimeAssurance` 注释及测试 fixture 按 Change 所述。guard 在 occurrence 唯一、typed node/claim 唯一且 capability 缺席已处理后、将 claim 投影为 Observed 前执行（`SemanticCheckedResolver.cs:145-155`），要求该 `NodeRef` 的 `CheckableObservedTrue`，而 `NodeRef` 含 capture identity。raw checked 仍在 typed claim 路径保留。新增 `CheckedGuardPublicTests` 通过公开 `UniKernel.Process` / `VerifyPostActionEffect` 调用链验证；没有新增内部 resolver 专属测试。

`CheckedGuardFixture` 是显式调用的合成观察构造，不自动遍历或改写所有 checked 值；逐项来源见 `fixture-rationale.md`。新增 `CheckedGuardPublicTests` 的 7 例分别覆盖 true、false、missing、malformed、other-node、old-capture 与 mismatched-metadata。Partial、Unsupported、temporal 拒绝边界由未修改的 SIM-011 原 11 例等既有载体承担。

## Spec

**结论：零阻塞，范围与冻结约束符合。** `frozen-inputs.json` 所列原输入哈希 before/after 一致，25 个 legacy XML 纳入清单；SIM-011 原 11 例未见通过过滤角色、改 expected 的变更，SIM-010 原 XML 未升级 Exact。`certification-diff.json` 的 20 个 golden 条目中 expectationsDigest 与 executionDigest 前后相同，仅 runtimeSourceHash、certifiedByChange、certifiedAt 更新为本 Change。

具体行为：同 capture+node 缺少 Observed(true) 的 checkable 时，resolver 返回 Unknown `checkable-not-observed-true`；false/missing 真实负例仍拒绝。Capability 缺席仍在 guard 之前返回 Unsupported。

## 注意事项

检查中未发现可复现的 spec 阻塞。需留意 guard 对所有经该 resolver 消费的 typed claim 生效：旧 Partial/temporal 输入若没有同节点、同 capture 的 checkable=true，会在后续时序/值域处理前转为 Unknown 并提前拒绝。若这些是预期拒绝边界则符合当前 Change；若要求保留其原有拒绝原因或继续执行时序判定，应另行定义 Acceptance/fixture，不应通过放宽本 guard 掩盖。

本文件仅记录 Review；未运行或签发 Verify。收到的 full 八工程/场景 PASS 汇报不作为本审查依据或 Verify 替代。
