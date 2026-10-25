# Private-index hunk Spec review

范围：只读审查 `draft-run.json` 指定的私有 `GIT_INDEX_FILE` 中 `HEAD..cached` 差异，以及两个选择器、两个排除补丁与依赖归属说明；未修改真实工作树、私有索引或源码，未构建/测试，也未重审 Host 32/32。

## Findings

0 项。

`KernelRunDriver.cs` 的私有索引版本不含 `_stepExpectedEffect`、`_actOrigin*`、`overlayRecovery` 或 `OverlayObservationCodec`；后置验证保持原五参数调用，恢复观察仍只取 `page-identity`/`CurrentContainerSubject`。`UniKernel.cs` 不含 `authoredExpected`、Overlay dismissal evidence 或 `OverlayDismissed` 分支；`WorldModel.cs` 不含 Overlay reconcile/current-overlay gate。三者分别与 `excluded-parallel-hunks.patch`、`excluded-world-hunks.patch` 的排除边界一致。

保留部分符合 `dependency-ownership.md`：Storage 调用方仅连同已关闭的 Origin/Plan provider 及当前 World storage 的最小闭包进入私有索引，不以整套 dirty Kernel 或 Overlay provider 补编译。`_pendingFailedStepIndex` 在所选分支恢复为 `_planItemIndex`；该恢复属于明确排除的并行 Act failed-step 语义，不能由任何 clean-view 编译成功推断其业务正确性。

当前私有树为 `79f2ab367e792cf6aea08e0f7062fb131ff8cd70`。本结论只确认选择范围；联合构建与 sibling 语义仍由其各自验收负责。
