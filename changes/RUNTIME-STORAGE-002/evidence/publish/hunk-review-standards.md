# Private-index hunk isolation Standards review

范围：只读检查 `draft-run.json` 的 `GIT_INDEX_FILE` 相对 `HEAD` 的 cached diff，以及两个 selector 与两份 excluded patch；未改真实工作树/index，未构建或测试，未复查 Host32。

## 可操作问题（仓库规则）

0 项。

`select-kernel-hunks.py:17-53` 先保留 Storage、已关闭 Origin/Plan 所需内容，再从 `KernelRunDriver.cs` 移除 Act scope、failed-step、ExpectedEffect 与 Overlay recovery，并在 `UniKernel.cs:34-42` 移除 authored ExpectedEffect/Overlay evidence。私有 index 中这些 sibling 标记均不存在；Origin 的 `AttemptCorrelation`、Plan 的 `RuntimePlanGraph` 与 Storage 调用仍存在。`select-world-hunks.py:10-21` 明确移除 `OverlayObservationCodec`、`ReconcileOverlay`、`RequireCurrentOverlay` 和 Overlay gating；缓存的 `WorldModel.cs` 保留 Owner facade/`*Core` 所需的 container 路径。

## 判断性异味

0 项。文本定位式 selector 有 assertions 覆盖目标锚点及最终禁入标记，适合此一次性私有提交视图；未把混合文件整件归属为 Storage，也未新建抽象或第二 Owner。

## 只读核对

私有 index 的三文件 cached diff 仍包含 `KernelRunDriver.cs`、`UniKernel.cs`、`WorldModel.cs` 的最小闭合改动；`excluded-parallel-hunks.patch` 与 `excluded-world-hunks.patch` 逐项对应被排除的 sibling。`git diff --cached --check` 唯一提示是 `src/UniClaw.Host/Runtime/HistoryVersions.cs:36` 的既有 EOF 空行，非本三文件隔离 hunk，未计入本审查。

结论：hunk isolation Standards actionable findings = 0；judgement smells = 0。
