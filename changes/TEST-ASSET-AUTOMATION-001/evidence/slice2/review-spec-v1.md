# Slice 2 Spec review v1

## 结论

有界不通过：重放侧已静态满足本片的独立 Run、实际输入字节与声明 hash 分列、TRX 消费、CLI/内部 carrier 退出码分列，以及 SIGTERM 后对自建进程组收尾的形状；诊断侧仍有一处会使显式开发资产请求依赖产品 profile，未满足本片约束。A5 归档/GC尚未实施，按 Change scope 不计为本片漏项。未运行 .NET/Python 测试或调用服务、模型、设备；结论来自固定 `before/` 与当前指定文件的静态比对。

## P1：显式 developmentAssets 在 Python 解析前被产品模型 profile 阻断

- **位置：** `src/UniClaw.Host.Dsh/RuntimeHttpServer.cs:586-605`。
- **复现：** 对 `run-diagnosis` 提交含 `developmentAssets.changeId` 与有效资产配置的请求，同时令 `_bindings/_agentProfile` 无法解析 `LogicalProfileId.AgentDecision`。代码先在 589-594 调 `DshModelManagement.ResolveDshModel`，直接返回 `routing-unavailable`（597）；因此不会调用 604-605 的 `InvokeProcedureWithAssetsAsync`，也不会执行 Python `test-assets begin` 的布局/Change 校验。
- **违反：** `plan.md` 第2步要求“显式开发Change/配置经Python解析”；`docs/agents/test-asset-retention.md` §8规定布局由资产 CLI/配置解析，并要求非法配置不回退。该开发资产入口不应以产品 profile 是否存在决定能否建立/核验开发 Run；默认产品请求继续保持原 profile 路由即可。
- **建议：** 将 developmentAssets 的布局/Change begin（及其明确错误）置于独立的开发资产边界，或至少在 profile 解析前完成该 Python 管理入口；模型诊断本身仍可在真正需要 transport 时按既有路由失败。补一条 HTTP/Host 定向测试：开发配置有效、产品 profile 不可用时，断言得到可见的资产 Run/明确资产结果，而非提前 `routing-unavailable`；无 `developmentAssets` 的旧请求仍保持原行为。

## 已核对的边界

`tools/test_asset_operations.py:213-232`为 case 输入记录当前 SHA256 与声明 SHA256；`252-300`先读 replay JSON/TRX 再 `finish`，且返回 `cliExitCode` 与 `carrierExitCode`；`176-206`只终止本次 `start_new_session` 的进程组。`RuntimeToolHost.DevelopmentAssets.cs:80-101`对成功、失败、取消均尝试 finish，且不以收尾错误覆盖原诊断异常。上述仅构成第二片重放/诊断的静态审查，不覆盖旧诊断结论、产品存储或正式 baseline。
