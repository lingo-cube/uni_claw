# PNL-004 — Production Runtime/Host adapter audit

## Result

The PNL-004 launch contract, Host-neutral launch context seam, and DSH
`uniclawRuntime` transport registration are implemented. The registered adapter
now calls a configured Host-facing Runtime endpoint and fails closed when that
endpoint is absent or malformed. A real Host/Runtime transport deployment is
still required before a page launch can be accepted as a real device run;
WI-PNL004-005 remains pending.

## Evidence

```yaml
level: READ-ONLY-ARCHITECTURE-AUDIT
method: inspect src/UniClaw.Host/HostRunner.cs, src/UniClaw.Host.Dsh/Program.cs, src/UniClaw.Agent.Dsh/DshOpenedHttpPeer.cs, dsh/uniclaw-decision-channel/src/index.js; run DSH/Web/schema/manifest tests
expected: a production seam accepts TaskLaunch logical refs, delegates Runtime-owned identity creation, binds Product Session and Host Session, and registers uniclawRuntime for the DSH task-workbench plugin
actual: HostRunner.RunOnce(runRoot, options) at src/UniClaw.Host/HostRunner.cs:55 now accepts LaunchContext and uses its RunId for trace correlation, then verifies it against Kernel canonical RunId; src/UniClaw.Host.Dsh/Program.cs:73 still mints ProductSessionId inside the DSH host and line 93 creates DshAgentAdapter; DshOpenedHttpPeer exposes only handshake/consult/slow/abort/detach at lines 31-35; dsh/uniclaw-task-workbench/src/index.js now registers a thin `uniclawRuntime` HTTP adapter through ctx.reflect.provide, while the Host-facing Runtime endpoint remains an explicit deployment prerequisite
evidence: src/UniClaw.Host/HostRunner.cs; tests/UniClaw.Host.Tests/HostTests.cs; dsh/uniclaw-task-workbench/src/index.js launchTask and tests; dsh/uniclaw-task-workbench/tests/workbench.test.mjs; 63 DSH tests; 72 Web tests; Host 142 tests; 7 workspace schemas; 2 test-set manifests; evidence/pnl003-real-task-android-settings-20261003/run-20261003-075727-844/
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

The launch interface is now registered through the existing DSH reflection
service. The adapter sends only logical references, correlation, idempotency and
launch identity to the configured Runtime endpoint, and validates that the
response contains Runtime-owned `runId` and `productSessionId`. With no endpoint
configured, the provider returns `runtime-unavailable`; malformed responses return
`runtime-response-invalid`. No fallback identity is created.

This still does not prove a real device launch: `UniClaw.Host.Dsh` remains a
console composition root that mints a Product Session locally and runs
`HostRunner.RunOnce`; it is not yet the Host-facing HTTP service described by the
adapter. WI-PNL004-005 therefore remains pending until that transport is deployed
and exercised against a real Host/Runtime composition. The adapter preserves the
existing partial recovery behavior and does not change AGT-001/RUN-005 authority.
