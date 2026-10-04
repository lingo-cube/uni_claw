# PNL-005 Runtime Run Store — verification evidence

日期：2026-10-04（Asia/Shanghai）

## CONTRACT

- method: `python3 tools/validate-workspace-schemas.py`
- expected: Runtime Run projection、event、response schema 及正负样例通过；绝对路径明细引用被拒绝。
- actual: `PASSED 11 schema(s) and examples`；`runtime-run-projection-absolute-detail.json` 被拒绝。

## DETERMINISTIC

- method: `dotnet test tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj --no-restore --filter FullyQualifiedName~RuntimeRunStoreTests`
- expected: 文件快照可重载、事件追加可查询、source/cursor 生效、终态后禁止再次转移、非法 cursor 和路径穿越失败。
- actual: 3/3 passed。

## BUILD / REGRESSION

- method: `dotnet build src/UniClaw.Host.Dsh/UniClaw.Host.Dsh.csproj --no-restore`；`dotnet test tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj --no-restore`
- expected: DSH Host 编译无错误；Host 回归不退化。
- actual: build 0 errors；Host tests 145/145 passed。仓库既有 XML/nullability warnings 保持原状。

## HTTP SMOKE

- method: 启动 `UniClaw.Host.Dsh --runtime-http --runtime-port 3091 --device smoke-device`，调用 health、未知 Run、空请求。
- expected: health 200；未知 Run 使用 404 + 业务 error；格式错误使用 400 + 业务 error；服务可启动和关闭。
- actual: health 200；未知 Run 404 `runtime-run-not-found`；空请求 400 `invalid-request`。服务已关闭。

## ENVIRONMENT

- method: `adb devices`、访问本地 3083/3090。
- expected: 有可用 Android device、DSH Host 和 Runtime Host 后执行真实 `POST → GET run → GET events`。
- actual: 当前没有 ADB device，3083/3090 均未运行；真实 DSH/Kernel/设备闭环未宣称通过。
