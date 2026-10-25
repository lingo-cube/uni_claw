# Slice 2 Spec review v2（P1 适用性复判）

## 结论

撤回 `review-spec-v1.md` 的 P1，第二片在所审范围内有界通过；v1 保留为审查轨迹，不再作为阻塞结论。未运行构建、测试或服务/模型/设备。

原始权威已在 `changes/PNL-012/state.md:36,40-42` 的 D1 固定：诊断复用 `LogicalProfileId.AgentDecision`，解析不到即显式 `ROUTING_UNAVAILABLE`（503），禁止静默降级；slice2 固定快照 `before/src/UniClaw.Host.Dsh/RuntimeHttpServer.cs:574-576` 也保留同一既有注释。现行 `docs/agents/test-asset-retention.md` §9 只是与这项既定边界一致的澄清：HTTP 的 `developmentAssets` “只提供 Change/config/retry，**不替代既有模型路由或产品 profile**”；无开发 Owner 的兼容调用保留原路径。当前 `RuntimeHttpServer.cs:577-597` 仍是该既有 `run-diagnosis` 的 fail-closed 准入，解析不到返回 `routing-unavailable`，没有静默假模型或降级。故它发生在真实诊断尚未执行之前，不构成资产 Run 已开始但未收尾，也没有产品 Host/profile 读取或依赖 Harness 资产 YAML。

`plan.md:5` 的“显式开发Change/配置经 Python 解析”限定的是已通过该模型准入、进入 `InvokeProcedureWithAssetsAsync` 的资产出口：`RuntimeToolHost.DevelopmentAssets.cs:48-65` 由 `test-assets begin` 解析配置并取得 Run 路径；默认 HTTP 路径不传 `developmentAssets`，不会读取资产配置。规范与计划均未要求 routing 拒绝前分配 Run、记录配置，或在无模型身份时伪造诊断资产。因此 v1 将调用顺序解释成新增产品决定，证据不足。

重放仍静态核对为：`tools/test_asset_operations.py:213-232` 保存当前实际字节 SHA256 与声明 hash；`252-300` 先消费 replay JSON/TRX、分列 CLI 与 carrier 退出码再 finish；`176-206` 仅终止本次自建会话进程组。诊断在 `RuntimeToolHost.DevelopmentAssets.cs:80-101` 对正常、失败、取消均尝试 finish，收尾失败不覆盖原诊断异常。归档/GC、产品存储、旧诊断结论与正式 baseline 均不在本片判定范围。

如需在 routing 拒绝时给调用方更多资产配置可见性，可另立为只读 preflight/错误详情改进；它不能被当作本片缺陷，也不应创建两阶段 Run 或绕开原模型路由。
