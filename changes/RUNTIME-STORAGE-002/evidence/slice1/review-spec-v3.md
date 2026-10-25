# Slice 1 SPEC review — candidate-v3

固定比较：`evidence/slice1/entry-source` → immutable `candidate-v3` / `candidate-v3.patch`；未使用可变工作区或 HEAD 大 diff。v2 的「先构造全量 JSON 再限额」已消除：`AuxiliaryArtifacts` 以受限 `IBufferWriter` 编码、超额前拒绝扩容，并以临时文件完成后改名发布（`AuxiliaryArtifacts.cs:57-76,94-135`）。

## Findings

### P1 — 候选夹带了会改变正式 Act/Effect 行为的业务逻辑

本片只授权咨询/遍历观察者与 Host 可选输出的异常、等待、关闭隔离；`state.md:42,55-57` 与 `storage-contract.md:46,74` 要求辅助失败不改写正式决策、业务预算或 Effect 次数，且 Host/诊断面不取得领域事实权。可是 `KernelRunDriver.cs:361-362,970,1102-1118` 新增并使用 `_actOriginContainerId`：多步 `AgentDecision.Act` 的第一步若跨容器，后续未执行步骤现在被改为 `StepRejected`、清空已采纳 Act 并返回 `NeedDecision`。

实际触发：一个合法的两步 Act，其第一步导航到另一 container、第二步本应在新 container 执行时，候选不再投递第二个 Effect，而是生成 `act-scope-changed` 并再次咨询。这样会改变 Effect 调用/回执、资源消耗、正式结算与最终结果，和任何辅助导出故障无关；也没有对应本片需求或验证。该改动必须移出此候选/独立立项，首片仅保留诊断隔离改动。
