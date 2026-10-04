# PNL-004 live launch verification — 2026-10-04

## Result

两条真实浏览器链已执行：认证后的任务查询接口可用；接口和页面发起都能到达
Task Launch seam，并在缺少真实 Runtime transport 时 fail-closed。没有把失败伪装成
任务创建成功，也没有生成本地 runId 或 Product Session。

## Verification

```yaml
level: AUTHENTICATED_BROWSER_INTEGRATION
service: http://127.0.0.1:3083/
method:
  - authenticated GET /api/uniclaw-task/tasks via ego-browser page.fetch
  - authenticated POST /api/uniclaw-task/tasks/launch via ego-browser page.fetch
  - UniClaw Workspace → project + → requirement form → 发起 via ego-browser
expected:
  - task list returns existing projects and task instances
  - valid launch request resolves the repository-owned testset catalog before Runtime
  - page launch uses the same canonical request and surfaces structured failure
actual:
  task_list: HTTP 200; ok=true; four local task definitions returned
  interface_launch: HTTP 503; code=runtime-unavailable; message=UNICLAW_RUNTIME_BASE_URL is not configured
  page_launch: dialog accepted requirement and local defaults; status area showed
    "UNICLAW_RUNTIME_BASE_URL is not configured" with source "TaskCommand"; dialog remained open
first_divergence: Host/Runtime transport is not configured after catalog resolution
evidence:
  - browser snapshot after interface launch: HTTP 503 runtime-unavailable
  - browser snapshot after page launch: Workspace 状态 / TaskCommand structured error
host_surface_probe:
  service: authenticated DSH web host at http://127.0.0.1:3081/
  request: GET and POST /api/uniclaw-runtime/runs
  actual: HTTP 404; body=not found
  interpretation: 3081 is a live DSH Host, but it does not expose the Runtime launch
    transport required by this contract; its existing Agent/decision APIs are a different seam
```

## Fix made during verification

The installed DSH plugin snapshot cannot see repository-level `testsets/` by package-relative
path. `UNICLAW_TESTSET_ROOT` is now an explicit Host configuration seam, and
`dsh/test-service.sh` defaults it to the repository `testsets/` directory. A contract test
proves an external configured catalog root resolves before Runtime creation. DSH package
tests pass 64/64 and the deployed profile drift check is clean.

## Gate observed before Runtime Host integration

At this point in the investigation the Runtime launch transport was not yet exposed; the
authenticated 503 and 404 observations above are retained as the safe pre-integration
baseline. The integration and repeat verification are recorded below.

## Runtime Host completion evidence — 2026-10-04

The remaining gate was closed by adding the Runtime HTTP surface to the existing
`UniClaw.Host.Dsh` composition root and configuring the dedicated DSH service with
`UNICLAW_RUNTIME_BASE_URL=http://127.0.0.1:3090`.

```yaml
level: REAL_ANDROID_DSH_HOST_INTEGRATION
services:
  dsh: http://127.0.0.1:3083/
  runtimeHost: http://127.0.0.1:3090/
  device: emulator-5556
method:
  - GET /api/uniclaw-runtime/health
  - POST /api/uniclaw-runtime/runs with android-settings refs
  - authenticated POST /api/uniclaw-task/tasks/launch through DSH Workbench
  - repeat the identical launch request
expected:
  - Runtime Host performs the real DSH handshake and returns Runtime-owned runId, Product Session and dshSessionId
  - HostRunner consumes the same run identity and writes trace, settings trace, facts and execution journal from the real Android emulator
  - Workbench binds the returned DSH session one-to-one and retries idempotently
actual:
  health: HTTP 200; ok=true
  direct_runtime_launch: HTTP 202; runId=run-2b2a80d1be4d477b9837bb76f9507709; productSessionId=product-session-7fb2f02a5bef41888afb35330cc409f5; dshSessionId=session-c0bc46c1-fa8a-4603-84dd-e04e7178053b
  direct_runtime_outcome: Completed / terminal-emitted; facts.json, trace.json, settings-trace.json and exec.journal written under /tmp/uniclaw-pnl004-runs-1752
  workbench_launch: HTTP 200; ack.status=completed; runId=run-1f57df5b816742de9246f67233caa8ca; productSessionId=product-session-f59c8ba131fd47deac741b704257baa7; hostSessionRef=session-d8f1434c-c5f7-4892-8bc1-35174db341c1
  idempotent_retry: HTTP 200; idempotent=true; same taskInstanceId, runId and hostSessionRef
 evidence:
  - authenticated response captured from /api/uniclaw-task/tasks/launch
  - DSH log: handshake accepted and one consult captured for the same productRunId
  - real artifact directory: /tmp/uniclaw-pnl004-runs-1752/run-20261004-095538-272/
  - build: dotnet build src/UniClaw.Host.Dsh/UniClaw.Host.Dsh.csproj --no-restore
  - tests: npm test --prefix dsh/uniclaw-task-workbench (65/65); Host tests (142/142)
```
