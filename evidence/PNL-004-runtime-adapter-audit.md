# PNL-004 — Production Runtime/Host adapter audit

## Result

The PNL-004 launch contract and a Host-neutral launch context seam are
implemented, but the repository does not yet contain the production DSH
`uniclawRuntime` provider that would make a page launch a real Host run. This is
the reason WI-PNL004-005 and WI-PNL004-006 stay pending and WI-PNL004-007
remains open.

## Evidence

```yaml
level: READ-ONLY-ARCHITECTURE-AUDIT
method: inspect src/UniClaw.Host/HostRunner.cs, src/UniClaw.Host.Dsh/Program.cs, src/UniClaw.Agent.Dsh/DshOpenedHttpPeer.cs, dsh/uniclaw-decision-channel/src/index.js; run DSH/Web/schema/manifest tests
expected: a production seam accepts TaskLaunch logical refs, mints a Runtime run, binds Product Session and Host Session, and registers uniclawRuntime for the DSH task-workbench plugin
actual: HostRunner.RunOnce(runRoot, options) at src/UniClaw.Host/HostRunner.cs:55 now accepts LaunchContext and uses its RunId for trace correlation, then verifies it against Kernel canonical RunId; src/UniClaw.Host.Dsh/Program.cs:73 still mints ProductSessionId inside the DSH host and line 93 creates DshAgentAdapter; DshOpenedHttpPeer exposes only handshake/consult/slow/abort/detach at lines 31-35; the decision-channel plugin injects DSH services at dsh/uniclaw-decision-channel/src/index.js:62-64 and has no production uniclawRuntime registration
evidence: src/UniClaw.Host/HostRunner.cs; tests/UniClaw.Host.Tests/HostTests.cs; dsh/uniclaw-task-workbench/src/index.js launchTask and tests; dsh/uniclaw-task-workbench/tests/workbench.test.mjs; 56 DSH tests; 67 Web tests; Host 142 tests; 7 workspace schemas; 2 test-set manifests; evidence/pnl003-real-task-android-settings-20261003/run-20261003-075727-844/
```

## Interface validation (2026-10-04)

The launch interface itself is usable and fail-closed. The task-workbench
accepts the versioned logical `TaskLaunchRequest`, calls
`ctx.get('uniclawRuntime').createRun(...)`, requires the Runtime response to
contain both `runId` and `productSessionId`, and only then calls the DSH
`sessionController.create(...)`. Existing tests prove idempotency, Runtime
failure recovery, missing Product Session handling, and Host-session partial
recovery. `HostRunner.LaunchContext` also accepts the same five correlation
values and verifies the supplied Runtime `RunId` against Kernel's canonical
`RunId`.

This validation does not prove production registration. In the current
repository `uniclawRuntime` is only a test context double: there is no DSH
plugin or Host transport that provides `createRun`/`recoverRun`, and
`UniClaw.Host.Dsh` is a console composition root that mints a Product Session
locally and runs `HostRunner.RunOnce`; it is not an RPC service. Registering a
Node object that merely returns IDs would violate the Runtime-owned run and
Product Session authority rules, so no fake provider was added.

## Required next slice

Implement WI-PNL004-007 as an explicit adapter. It must receive logical
references and launch metadata, make Runtime the owner of `runId`, establish or
obtain Product Session before Host binding, pass the correlation into
HostRunner/DSH consultation, and register the capability through the existing
DSH plugin seam. The missing prerequisite is a real Host-facing launch
transport (or an in-process Host service owned by the same composition root)
whose response contract returns the Runtime-created `runId` and
`productSessionId`. Once that seam exists, the provider registration is a thin
adapter; until then the correct behavior is the existing structured
`runtime-unavailable` response. It must preserve the current fail-closed
recovery behavior and must not change AGT-001/RUN-005 authority boundaries.
