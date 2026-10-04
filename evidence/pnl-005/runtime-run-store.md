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

- method: 启动 `UniClaw.Host.Dsh --runtime-http --runtime-port 3092 --device smoke-device`，调用 health、Run list、非法 cursor、未知 Run、空请求。
- expected: health/list 200；非法 cursor 400；未知 Run 使用 404 + 业务 error；格式错误使用 400 + 业务 error；服务可启动和关闭。
- actual: health 200；Run list 200（空列表不输出伪造 cursor）；非法 cursor 400 `invalid-cursor`；未知 Run 404 `runtime-run-not-found`；空请求 400 `invalid-request`。服务已关闭。

## ENVIRONMENT

- method: `adb devices`、访问本地 3083/3090。
- expected: 有可用 Android device、DSH Host 和 Runtime Host 后执行真实 `POST → GET run → GET events`。
- actual: 当前没有 ADB device，3083/3090 均未运行；真实 DSH/Kernel/设备闭环未宣称通过。
