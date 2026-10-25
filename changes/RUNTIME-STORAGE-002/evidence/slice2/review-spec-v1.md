# RUNTIME-STORAGE-002 Slice 2 — Spec review v1

审阅对象仅为 `evidence/slice2/candidate-v1/manifest.json` 所列 entry-to-owned 冻结补丁；未以 dirty HEAD、父 Change 未完成项或前片历史作为本片实现。结论：2 个可行动缺陷；其余重点（私有生成器/输入固定、Published 后 Confirm 恢复、终态先于报告重试）在冻结代码中有对应实现，但尚不把未运行的测试当作验证通过。

## (c) 看似实现但错误

1. **`runtime-run` 的来源可被任意 run 目录替换（可验证）。** 需求写明“必交付报告需求、必要输入及状态可跨重启恢复……产物与来源版本/摘要匹配才已交付”（`state.md:47`），且契约规定输入固定来自“已停止写入的Run产物集”（`report-delivery-contract.md:14-15`）。但 HTTP retry 将调用者给出的 `runDir` 直接传给 `RetryRequiredReportAsync`（`owned.patch:947`）；该方法在尚未固定时仅 `ResolveRunDir` 并 `PrepareAsync`（`owned.patch:595-600`），`PrepareAsync` 也只把该目录作为首次 `SourceDirectory`（`owned.patch:129-140`），没有核验它对应 `ownerRef = runtime-run:<runId>` 或其中事实的 run id。触发：先为 `runtime-run:A` 接受请求，再对其 delivery 调用 retry body `{ "runDir": "B" }`。结果是 A 的 requirement 可用 B 的私有字节进入 Ready/Delivered；哈希只证明“B 的输入未变”，不证明 A 的来源版本，违背 A6 来源绑定。应由 Host 从 OwnerRef/Run 投影解析唯一目录，或在固定前拒绝不匹配 run 身份。

2. **非合作生成器在 Host 进程被终止后不保留跨进程单 writer（可验证缺陷）。** 契约原文：“非合作生成器超时不能释放租约并允许无限并发重试；未结束操作保留所有权”（`report-delivery-contract.md:24`）；A6 明确要求生成中 kill 后新 Host 仅生成/校验报告（`state.md:47`）。实现的唯一租约是进程持有的 `FileStream`（`owned.patch:277-286`），而 `_settling` 也是内存字典（`owned.patch:44,247-254`）。若 Host 在已启动 Python 后仅自身被终止，OS 会释放该文件锁；新 Host 可立即取得锁并再次执行生成器（`owned.patch:195,220-223`），原子进程若仍在则并行。现有 kill 测试显式 `Kill(entireProcessTree: true)`（`owned.patch:2106`），因此未覆盖这个触发。后果是同一 requirement 可重复生成，且不满足“未结束操作保留所有权”。需持久化/可探测的 in-flight 归属，或让恢复先确认旧子进程已结束后再重试。

## (a)/(b)

除上述两项外，未发现本冻结 diff 将报告重试扩大为任务重跑、Effect 重投或 Runtime Outcome 回写的可验证越界。完整父 A1–A10、真实启动端到端与设备/模型均不属于本片已证明范围。
