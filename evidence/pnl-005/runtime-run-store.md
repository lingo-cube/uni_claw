# PNL-005 Runtime Run Store — verification evidence

日期：2026-10-04（Asia/Shanghai）

## CONTRACT

- method: `python3 tools/validate-workspace-schemas.py`
- expected: Runtime Run projection、event、response schema 及正负样例通过；绝对路径明细引用被拒绝。
- actual: `PASSED 12 schema(s) and examples`；`runtime-run-projection-absolute-detail.json` 被拒绝。

## DETERMINISTIC

- method: `dotnet test tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj --no-restore --filter FullyQualifiedName~RuntimeRunStoreTests`
- expected: 文件快照可重载、事件追加可查询、source/cursor 生效、终态后禁止再次转移、非法 cursor 和路径穿越失败。
- actual: 4/4 passed；新增 Run 列表按 product session 过滤和 opaque cursor 稳定分页验证。

## BUILD / REGRESSION

- method: `dotnet build src/UniClaw.Host.Dsh/UniClaw.Host.Dsh.csproj --no-restore`；`dotnet test tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj --no-restore`
- expected: DSH Host 编译无错误；Host 回归不退化。
- actual: build 0 errors；Host tests 146/146 passed。仓库既有 XML/nullability warnings 保持原状。

## WORKBENCH ADAPTER

- method: `npm test`（`web/uniclaw-workspace`）
- expected: Runtime HTTP projection 可映射为 host-neutral Workspace capabilities；launch、timeline、trace、evidence 和 detail seam 不依赖 DSH。
- actual: 79/79 passed；adapter 使用逻辑 Runtime endpoint，发起结果由 WorkspaceQueryCore 归一为 `status: ready`，不把 Host 专有对象泄漏到 UI core。

## HTTP SMOKE

- method: 启动 `UniClaw.Host.Dsh --runtime-http --runtime-port 3094 --device smoke-device`，调用 health、Run list、非法 cursor、未知 Run、空请求。
- expected: health/list 200；非法 cursor 400；未知 Run 使用 404 + 业务 error；格式错误使用 400 + 业务 error；服务可启动和关闭。
- actual: health/list 200（空列表不输出伪造 cursor，列表成功/失败均使用 list-response schema）；非法 cursor 400 `invalid-cursor`；未知 Run 404 `runtime-run-not-found`；空请求 400 `invalid-request`。服务已关闭。

## ENVIRONMENT

- method: 启动 `tools/android/start-test-device.sh`（持久会话保持 `emulator-5556`）、启动 DSH test service `3083`、启动 `UniClaw.Host.Dsh --runtime-http --runtime-port 3090 --device emulator-5556`；先直接 POST，再通过 Ego Lite Workspace 页面发起一次任务；随后读取 Runtime run/events 和 DSH task/session projection。
- expected: 真实设备、DSH Host 和 Runtime Host 形成一条可查询链路；页面发起只创建一个 Runtime Run 和一个已绑定的 Host session；Runtime 终态、事件、设备元信息和 DSH 对话/trace 均可读。
- actual: 通过。直接请求生成 `run-260164a0a0f84653b7b28e05b2cf85d8`，`202 → completed`，绑定 `session-916e963c-36c9-4620-8fc8-2d452bbb8ca6`；页面发起生成 `run-2b8b5a532db844aeb94a7b27b8dcb9c6`，`202 → completed`，绑定 `product-session-ad25301c9339488eb4bc8445ae2ffd34` / `session-0906bd07-c8d7-4633-81c2-8eb697a5dd71`，环境设备为 `emulator-5556`。Runtime events 为 `run.accepted(source=runtime)`、`run.started(source=dsh)`、`run.completed(source=runtime)`，revision `3`；artifacts 暴露 facts/trace/evidence/journal 的逻辑 detail endpoint。DSH 侧页面可见 1 个 Uni-Agent round、请求→决策→结果和 22 条 DSH trace。重复创建请求保持同一 run identity；无第二个 DSH session 被创建。

## REAL ACCEPTANCE

- method: `ego-browser` TaskSpace `PNL-005 real workspace acceptance`，打开 DSH Workspace，点击 `android-settings` 项目旁的 `+`，只填写需求并保持本地设备默认值，然后选择新建任务。
- expected: 发起窗口允许只填需求；本地配置显示机器、Host/Workspace 版本、认证方式和 `emulator-5556`；任务创建后可进入会话，看到 Uni-Agent 过程、DSH trace 和 Runtime 关联信息。
- actual: 页面显示默认设备 `emulator-5556`、`darwin · arm64`、`dsh-local`、`browser-token`、`uniagent-task`；发起后实例 `psi-be9a39d680b3c2a1` 写入 `run-2b8b5a532db844aeb94a7b27b8dcb9c6`，`host-session-bound` 阶段成功，页面进入 `Request, decision, and result timeline` 会话。对话显示调用方请求、Uni-Agent `noAction` 决策和提交结果；Trace 面板显示 22 条 DSH 原生事件并支持查看节点。
- note: 当前 Workbench 的 DSH session projection 仍把 Host session 实例标为 `active`，Runtime Run 已是 `completed`；两者是不同 authority，后续应补一个 session lifecycle reconciliation change，不在 PNL-005 本次修复中偷偷合并。
