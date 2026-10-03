# PNL-004 — DSH Workspace task launch action

```yaml
level: IMPLEMENTATION-VERIFY
method: inspect the DSH Typert contribution, browser bridge, shared Query Core/controller/view model, then run DSH/Web smoke tests and the canonical schema validators
expected: the DSH workspace exposes a Host-backed launch action; the request is canonical, logical refs are validated before dispatch, shared frontend remains Host-neutral, and failures remain structured
actual: the mounted contribution exposes uniclawTaskPanel.launch with the full TaskLaunchRequest parameter list; the browser bridge maps the selected task to schemaVersion/contractVersion/launchRequestId/projectRef/testSetRef/taskRef/idempotencyKey/correlationId/requestedAt/metadata; the shared controller reports loading/ready/error and refreshes after success; physical or incomplete refs leave the action disabled; standalone frontend has no DSH launch dependency
evidence: dsh/uniclaw-task-workbench/src/index.js; dsh/uniclaw-task-workbench/src/client.js; web/uniclaw-workspace/src/browser/entry.js; web/uniclaw-workspace/src/core/index.js; web/uniclaw-workspace/src/features/workspace-controller.js; web/uniclaw-workspace/src/features/workspace-view-model.js; web/uniclaw-workspace/tests/workspace-view-model.test.js; dsh/uniclaw-task-workbench/tests/client.test.mjs; 56 DSH tests; 70 Web tests; 7 workspace schemas; 2 test-set manifests; git diff --check
```

This change is a UI/adapter seam only. It does not claim that a production
`uniclawRuntime` provider exists. When the Host provider is absent, the same
button returns a structured `unavailable` notice instead of presenting a fake
successful run.
