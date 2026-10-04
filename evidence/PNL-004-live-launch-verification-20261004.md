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
```

## Fix made during verification

The installed DSH plugin snapshot cannot see repository-level `testsets/` by package-relative
path. `UNICLAW_TESTSET_ROOT` is now an explicit Host configuration seam, and
`dsh/test-service.sh` defaults it to the repository `testsets/` directory. A contract test
proves an external configured catalog root resolves before Runtime creation. DSH package
tests pass 64/64 and the deployed profile drift check is clean.

## Remaining gate

Deploy a real Host-facing Runtime HTTP service and set `UNICLAW_RUNTIME_BASE_URL`; then repeat
the same interface and page flows and prove Runtime-owned `runId`, Product Session, Host Session,
and recovery binding. Until that exists, the observed 503 is the expected safe boundary.
