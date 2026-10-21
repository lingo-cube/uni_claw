# RUNTIME-IDENTITY-001 原范围收口复核（2026-10-21）
范围仅原State A1–A4：两个Store的创建幂等、Dispose后恢复、TaskInstance与ProductSession的1:1、BindRun可选/幂等/拒绝第二绑定。后续HTTP链和全设备生命周期不由本记录证明。

## 独立Review
- Standards：独立审查确认Runtime owner铸造中性身份、JSONL追加、末条恢复、相同绑定幂等/不同绑定拒绝，符合ADR-0044/0045/0046，0项阻塞。
- Spec：独立审查确认A1–A4均有源码与公开Store测试支撑，0项阻塞。
- 两审查者均只读，不运行测试；Leader执行以下Verify。

## Verify四元组
- method：隔离快照内 `dotnet test tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~RuntimeIdentityStoreTests' --logger 'trx;LogFileName=identity-current.trx'`（results-directory指向本目录baseline）。
- expected：8项原Store行为断言通过，覆盖A1–A4。
- actual：8/8 Passed，0失败；涉及的3文件SHA256与当前工作区一致。无设备/模型调用。
- evidence：baseline/identity-current.trx、identity-current.txt、identity-source-hashes.json。

旧State仅编译/源码探针，不足以证明其verified；本轮补上独立Review和确定性Verify后，按原Store范围关闭。历史记录保留，不倒填成旧轮已执行。
