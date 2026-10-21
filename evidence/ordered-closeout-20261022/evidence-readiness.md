# Ordered closeout evidence readiness (2026-10-22)

范围：只核对仓库中已落盘工件。下述“可复用”表示可供后续验收参考，不等于当前环境状态；没有执行设备、服务或模型操作。

## SETTINGS-COVERAGE-004

Change 的四项验收见 `changes/SETTINGS-COVERAGE-004/state.md:13-17`。

| Acceptance | 可读工件与结论 | 缺项 / 最小后续入口 |
|---|---|---|
| A1：unknown route 每次重观察都产生 Agent request/consult record | 确定性用例 `UnknownPage_DeferPath_StillWorks`（`tests/UniClaw.Host.Tests/SettingsCoverageDirectorPlanTests.cs:214-233`）断言 UnknownRouteIdentity 上调用一次 Consult 会调用 Agent delegate 一次、返回 Defer、写出 `defer-unknown-page` consult record；冻结 TRX 中该用例 Passed。另有实际定向运行记录：`evidence/settings-coverage-004/live-fix/run-20261014-232314-431/consult-bridge.json`、`settings-trace.json`，汇总 `evidence/settings-coverage-004/live-fix-summary.json` 记载 unknown-route decisions -7/-8 的 Agent requests。 | 这些证据覆盖对应 unknown-route 分支，不要求遍历完整 Settings 树。若要独立证明“每次”多次重观察的一一关联，现有 consult bridge 与 trace 可继续核对；不构成全树缺口。 |
| A2：非 Defer 响应不进入 Effect，仍安全 Defer | `UnknownPage_RejectsAgentActWithoutEffect`（同文件 `:236-251`）配置 Agent 返回 Act，断言结果为安全 Defer、记录 `DecisionKind=act` 和拒绝原因；冻结 TRX 中 Passed。 | 当前确定性反例覆盖 Act 这一非-Defer分支。没有单独的设备/模型事实要求：A2 是 Host 确定性行为验收。不要把完整 traversal 或真实 Agent 调用另加为门槛。 |
| A3：真实 namespaced popup XML 分类为 present | `SettingsPopupClassifierTests.RealPopupXml_ClassifiesPresent` 使用真实语料 `evidence/real-settings-coverage-negative-20261001/run-e1-with-evidence/evidence/capture-3e3809ea136e408aaa72f75513953086.xml`；`SettingsNamespacedPopupIds_ClassifyPresent` 的 XML 明确含 `com.android.settings:id/alertTitle` 与 `buttonPanel`。二者均在冻结 Host TRX 中 Passed。Settings 模拟器运行另有 `evidence/settings-coverage-004/live-fix/run-20261014-232314-431/settings-trace.json` 的 cycles 7–9 `popup=present`，以及 `live-fix2/run-20261014-232522-213/evidence/capture-72f58e6959294d76a9d44de7d28731e8.xml`（包名 `com.android.settings`，含 CANCEL/SAVE）。 | 确定性 classifier 的真实 XML 输入与期望分类已有可复核测试及当前结果；模拟器 trace 是补充事实，不要求把完整 traversal 作为 A3 条件。 |
| A4：全量测试及 Host coverage tests 通过 | 冻结结果 `evidence/eng-opt-001/final-tests/results.json` 汇总 8 个测试程序集共 2024/2024 Passed；Host 原始结果 `eng-opt-final_net10.0_20261021235516.trx` 中可见上列 Unknown/Popup 用例 Passed。 | 无此项证据缺口；当前冻结 TRX 可替代旧的 1541-test 汇总，无需重跑全量。 |

综合判断：SETTINGS-COVERAGE-004 的 DETERMINISTIC acceptance 可由 Host 定向用例与 2026-10-21 冻结的 2024-test TRX 支撑；A1/A3 另有真实定向运行材料。live-fix 的 TerminalNotProven 和 live-fix2 的手动停止只说明没有完成整次 traversal，而整树遍历不属于本 Change 的 Acceptance，因此不构成这些 acceptance 的缺口。当前没有理由要求新增真实 Agent/模型事实来替代 Host 确定性测试。

## TRAJECTORY-001：H7 真实 DSH 传输证据

H7 对应 Acceptance 3、7（`changes/TRAJECTORY-001/state.md:69-79`）：Heartbeat、eventDelta、Case Packet 使用同一 eventSeq cutoff，真实 DSH 请求实际收到语义可读的 World Model、Kernel、Traversal 三组状态；另需桥审计与一轮真实设备留证。

- 当前直接 H7 工件只有 `evidence/trajectory-001/h7-preflight-20261017.json`。它记录 `ENVIRONMENT_UNAVAILABLE`：3081 拒绝连接、`UNICLAW_DSH_E2E_BASE` 未设置；`DshOpenedHttpPeerTests.Consult_SerializesHeartbeatReferenceIndexAndCasePacket` 只证明确定性序列化桥，不是真实传输。
- 可参考的新现场证据在 `changes/WORLD-OBSERVATION-001/evidence/live-high-final/` 与 `live-high-verified/`，包含 DSH handshake/request/response、设备 capture、preflight；其 `EVIDENCE-BOUNDARY.json` 说明这些运行用于 World Observation 的 XML/no-XML/high 场景。请求 prompt 是页面识别输入，现有 JSON 中没有 H7 要求的 Heartbeat + eventDelta + Case Packet 同 cutoff 字段，因此不能复用为 TRAJECTORY-001 H7 通过证据，也不能据此声称 Heartbeat 已真实送达。
- `changes/WORLD-OBSERVATION-001/evidence/live-final/semantic-*-request.json` 同样是页面语义识别 state/questions 请求，不包含上述三组轨迹字段；其成功设备/模型结果仅可证明另一个场景的运行线路，不证明 H7。
- 最小后续入口：在注册专用 DSH 3081 与设备环境可用时，按 `docs/agents/test-emulator.md` 的环境入口完成 preflight，再运行 TRAJECTORY-001 的 live DSH E2E/真实设备流程；保存 outbound request（证明三组字段及相同 cutoff）、DSH 接收/响应记录和设备 run 证据。无需扩展到全树结论；World 单点成功也不能替代全树 traversal 或真实 Heartbeat 验收。

## 本次盘点边界

只读了 Change 状态、指定 evidence 目录与环境规范，并写入本文件。没有读取凭据，也没有操作设备、模型或服务；没有修改产品源码或 Change 状态。状态中的旧结论不视为 2026-10-22 当前现场状态。
