# RUNTIME-STORAGE-002 Slice 2 — Spec review v2

对象仅为 `evidence/slice2/candidate-v2/manifest.json` 所列 entry-to-owned 冻结补丁；未将 dirty HEAD、父 Change 后续 A1–A10 或 v1 以外历史归入本片。结论：本轮**未发现新的可行动 Spec 缺陷（0）**。以下是静态审阅结论；冻结测试和 `orphan-tests/run-*` 证据可供验证，但未把日志格式本身当作证明。

## v1 缺陷复核

1. **来源错绑已修复。** 契约要求“Runtime要求固定来源前核验facts中的RunId，固定后生成前再次核验私有输入身份；手工artifact-set要求绑定原目录名”（冻结契约 `owned.patch:3189`），以满足 A6 “产物与来源版本/摘要匹配才已交付”（`state.md:47`）。`RetryRequiredReportAsync` 在 Prepare 前调用 `ValidateReportOwnerAsync`（`owned.patch:600-606`）；其对 `runtime-run:` 读取并比对 `facts.runId`（`owned.patch:675-693`），对 `artifact-set:` 比对原目录名（`owned.patch:669-674`）。实际执行生成器前又对私有 `context.InputDirectory` 复核，并保留原目录名作 artifact-set 比对（`owned.patch:611-619`）。因此此前 `runtime-run:A` 以 `runDir:B` 固定 B 的触发现在被拒绝；固定后替换原目录也不影响私有输入身份。

2. **Host-only kill 的并发生成已修复。** 契约要求遗留生成器仍运行时重试显式 busy、不得执行第二个本体（`owned.patch:3198-3199`）。Python wrapper 自身以非阻塞 OS 文件锁保护每 delivery（`owned.patch:717-740`），实际生成使用该锁（`owned.patch:611-619`）；竞争者返回 75/busy（`owned.patch:731-733`），不会进入 `runpy.run_path`。这正覆盖 Host 单杀后 .NET 文件锁释放的边界；冻结的 Host-only-kill 测试也针对“旧 child 存活、retry 仅一调用、释放后同 ID 可 Delivered”（`owned.patch:3341-3373`）。

## 新增关闭路径

关闭先 `DrainAsync(10s)` 追踪 Runtime 后台 work，记录未完成/失败 Run，再令 ToolHost 停止接受新报告进程并取消/等待既有进程（`owned.patch:823-836`、`696-713`）。进程启动位于 `_reportsClosing` 检查之后并受同一锁保护（`owned.patch:754-763`）；所以 Drain 超时后晚到的报告不会穿过关闭边界启动。失败报告仅保留独立状态，不回写 Runtime Outcome；未完成引用仍可查询，符合本片边界。

未报告假想的跨节点/掉电保证或父 A10 清理能力；它们不在该冻结片承诺范围。
