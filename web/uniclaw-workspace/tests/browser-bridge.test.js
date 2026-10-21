'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { ensureWorkspaceStyles, envelope, createCapabilities } = require('../src/browser/entry');
const { WorkspaceQueryCore } = require('../src/core');
const { createWorkspaceController } = require('../src/features/workspace-controller');

test('injects shared workspace styles once through the browser document seam', () => {
  const styles = [];
  const document = {
    head: { appendChild: (style) => styles.push(style) },
    createElement: (tagName) => ({ tagName, setAttribute() {}, textContent: '', id: '' }),
    getElementById: (id) => styles.find((style) => style.id === id) || null,
  };
  const container = { ownerDocument: document };
  ensureWorkspaceStyles(container, '.workspace { display: grid; }');
  ensureWorkspaceStyles(container, '.workspace { display: block; }');
  assert.equal(styles.length, 1);
  assert.equal(styles[0].textContent, '.workspace { display: grid; }');
  assert.equal(styles[0].id, 'uniclaw-workspace-styles');
});

test('normalizes DSH panel success responses at the host seam', () => {
  const result = envelope({ success: true, projects: [] }, 'TaskQuery');
  assert.equal(result.ok, true);
  assert.deepEqual(result.data.projects, []);
  assert.equal(result.capability, 'TaskQuery');
  assert.equal(typeof result.observedAt, 'string');
});

test('unwraps Typert success envelopes around the panel payload', () => {
  const result = envelope({ ok: true, data: { success: true, data: { projects: [] } } }, 'TaskQuery');
  assert.equal(result.ok, true);
  assert.deepEqual(result.data.projects, []);
  assert.equal(typeof result.observedAt, 'string');
});

test('unwraps the DSH remote value envelope used by static panel clients', () => {
  const result = envelope({ ok: true, value: { success: true, projects: [] } }, 'TaskQuery');
  assert.equal(result.ok, true);
  assert.deepEqual(result.data.projects, []);
});

test('normalizes DSH panel failures without leaking host shape into shared UI', () => {
  const result = envelope({ success: false, error: { code: 'not-found', message: 'missing' } }, 'SessionQuery');
  assert.equal(result.ok, false);
  assert.equal(result.error.code, 'not-found');
  assert.equal(result.error.message, 'missing');
});

test('projects task instances through the browser bridge after project selection', async () => {
  const caps = createCapabilities({
    workspace: () => ({ ok: true, data: { projects: [{ projectId: 'android-settings', instances: [{ productSessionId: 'ps-live', sessionId: 'dsh-live' }] }] } }),
    session: () => ({ ok: true, data: { conversation: [], dshTrace: [], uniclawTrace: [], uniflowTrace: [], evidence: [] } }),
    artifact: () => ({ ok: true, data: { text: 'detail' } }),
  });
  const result = await caps.TaskQuery.listTaskInstances({ projectId: 'android-settings' });
  assert.equal(result.ok, true);
  assert.equal(result.data.taskInstances[0].productSessionId, 'ps-live');
  const missing = await caps.TaskQuery.listTaskInstances({ projectId: 'missing' });
  assert.equal(missing.ok, false);
  assert.equal(missing.error.code, 'not-found');
});

test('projects UniClaw trace and artifact references with inspectable detail actions', async () => {
  const caps = createCapabilities({
    workspace: () => ({ ok: true, data: { projects: [{ projectId: 'p', instances: [{ productSessionId: 'ps-live', sessionId: 'dsh-live' }] }] } }),
    session: () => ({ ok: true, data: {
      dshTrace: [],
      uniclawTrace: [{ definition: 'world.reconcile' }],
      uniclawTraceContext: { traceId: 'trc-1', rootSpanId: 'sp-1', spanCount: 1 },
      uniflowTrace: [],
      evidence: ['/evidence/run/facts.json'],
    } }),
    artifact: () => ({ ok: true, data: { text: 'detail' } }),
  });
  await caps.TaskQuery.listProjects();
  const traces = await caps.TraceQuery.getTraces({ productSessionId: 'ps-live' });
  const evidence = await caps.EvidenceQuery.getEvidence({ productSessionId: 'ps-live' });
  assert.equal(traces.data.traces[0].source, 'uniclaw');
  assert.deepEqual(traces.data.context, { traceId: 'trc-1', rootSpanId: 'sp-1', spanCount: 1, source: 'uniclaw' });
  assert.equal(traces.data.traces[0].detailRef.refId, 'trace.json');
  assert.equal(evidence.data.evidence[0].detailRef.refId, 'facts.json');
});

test('exposes TaskCommand launch through the host-neutral browser bridge', async () => {
  let received;
  const caps = createCapabilities({
    workspace: () => ({ ok: true, data: { projects: [] } }),
    session: () => ({ ok: true, data: { conversation: [], dshTrace: [], uniclawTrace: [], uniflowTrace: [], evidence: [] } }),
    artifact: () => ({ ok: true, data: { text: 'detail' } }),
    launch: (...args) => { received = args; return { success: true, ack: { status: 'completed' } }; }
  });
  const request = {
    schemaVersion: 'uniclaw.workspace.task-launch-request.v1', contractVersion: 'uniclaw.workspace.contract.v1',
    launchRequestId: 'launch-request-1', projectRef: { id: 'project/p' }, testSetRef: { id: 'testset/p', version: 'default' },
    taskRef: { id: 'task/p/t' }, idempotencyKey: 'idem-1', correlationId: 'corr-1', requestedAt: new Date().toISOString(), environmentIntent: { device: { id: 'emulator-5558', override: true } }, metadata: []
  };
  const result = await caps.TaskCommand.launchTask(request);
  assert.equal(result.ok, true);
  assert.equal(result.data.ack.status, 'completed');
  assert.equal(received[0], request.schemaVersion);
  assert.deepEqual(received[3], request.projectRef);
  assert.deepEqual(received[5], request.taskRef);
  assert.deepEqual(received[9], request.environmentIntent);
});

test('preserves partial Host launch state at the shared browser seam', async () => {
  const caps = createCapabilities({
    workspace: () => ({ ok: true, data: { projects: [] } }),
    session: () => ({ ok: true, data: { conversation: [], dshTrace: [], uniclawTrace: [], uniflowTrace: [], evidence: [] } }),
    artifact: () => ({ ok: true, data: { text: 'detail' } }),
    launch: () => ({ success: false, error: { code: 'launch-partial', message: 'Host session pending', partial: { status: 'partial', launchId: 'launch-1' } } })
  });
  const result = await caps.TaskCommand.launchTask({});
  assert.equal(result.ok, false);
  assert.deepEqual(result.error.partial, { status: 'partial', launchId: 'launch-1' });
});

test('refresh reads one session detail for all panes and reads fresh detail on the next refresh', async () => {
  let reads = 0;
  const caps = createCapabilities({
    workspace: () => ({ success: true, projects: [{ projectId: 'p', instances: [{ productSessionId: 'ps', sessionId: 'host' }] }] }),
    session: (sessionId) => {
      assert.equal(sessionId, 'host');
      const revision = ++reads;
      return { ok: true, revision, data: {
        metadata: { readMarker: revision }, conversation: [{ kind: 'request', summary: `read-${revision}` }],
        uniclawTrace: [{ label: `read-${revision}` }], evidence: [{ title: `read-${revision}` }]
      } };
    },
    artifact: () => ({ success: true, text: 'detail' })
  });
  const controller = createWorkspaceController({ queryCore: new WorkspaceQueryCore(caps) });
  await controller.loadProjects();
  await controller.selectTaskInstance('ps');
  await controller.refresh();
  const first = controller.getState();
  assert.equal(first.session.session.metadata.readMarker, 1);
  assert.equal(first.timeline.items[0].summary, 'read-1');
  assert.equal(first.traces.items[0].label, 'read-1');
  assert.equal(first.evidence.items[0].title, 'read-1');
  assert.deepEqual(['session', 'timeline', 'traces', 'evidence'].map((key) => first[key].revision), [1, 1, 1, 1]);
  assert.equal(reads, 1);
  await controller.refresh();
  const next = controller.getState();
  assert.equal(next.session.session.metadata.readMarker, 2);
  assert.equal(next.timeline.items[0].summary, 'read-2');
  assert.equal(reads, 2);
});

test('shared failures keep each pane source and a later read can recover', async () => {
  let reads = 0;
  const caps = createCapabilities({
    workspace: () => ({ success: true, projects: [] }),
    session: () => ++reads === 1
      ? { success: false, error: { code: 'permission-denied', message: 'access revoked' } }
      : { success: true, conversation: [{ summary: 'access restored' }] },
    artifact: () => ({ success: true })
  });
  const results = await Promise.all([
    caps.SessionQuery.getSession({ productSessionId: 'ps' }),
    caps.SessionQuery.getTimeline({ productSessionId: 'ps' }),
    caps.TraceQuery.getTraces({ productSessionId: 'ps' }),
    caps.EvidenceQuery.getEvidence({ productSessionId: 'ps' })
  ]);
  assert.ok(results.every((result) => !result.ok && result.error.code === 'permission-denied'));
  assert.deepEqual(results.map((result) => result.error.source), ['SessionQuery', 'SessionQuery', 'TraceQuery', 'EvidenceQuery']);
  assert.equal(reads, 1);
  const retry = await caps.SessionQuery.getTimeline({ productSessionId: 'ps' });
  assert.equal(retry.ok, true);
  assert.equal(retry.data[0].summary, 'access restored');
  assert.equal(reads, 2);
});

test('a rejected remote read is released so the next read can recover', async () => {
  let reads = 0;
  const caps = createCapabilities({
    workspace: () => ({ success: true, projects: [] }),
    session: () => ++reads === 1 ? Promise.reject(new Error('connection closed')) : { success: true, conversation: [] },
    artifact: () => ({ success: true })
  });
  const failed = await Promise.allSettled([
    caps.SessionQuery.getSession({ productSessionId: 'ps' }),
    caps.TraceQuery.getTraces({ productSessionId: 'ps' })
  ]);
  assert.ok(failed.every((result) => result.status === 'rejected' && result.reason.message === 'connection closed'));
  assert.equal(reads, 1);
  assert.equal((await caps.SessionQuery.getSession({ productSessionId: 'ps' })).ok, true);
  assert.equal(reads, 2);
});

test('switching tasks keeps the new panes when the previous session read arrives late', async () => {
  const pending = new Map();
  const caps = createCapabilities({
    workspace: () => ({ success: true, projects: [{ projectId: 'p', instances: [
      { productSessionId: 'ps1', sessionId: 'host1' }, { productSessionId: 'ps2', sessionId: 'host2' }
    ] }] }),
    session: (id) => new Promise((resolve) => pending.set(id, resolve)),
    artifact: () => ({ success: true })
  });
  const controller = createWorkspaceController({ queryCore: new WorkspaceQueryCore(caps) });
  const detail = (id) => ({ success: true, metadata: { owner: id }, conversation: [{ summary: id }], uniclawTrace: [{ label: id }], evidence: [{ title: id }] });
  await controller.loadProjects();
  await controller.selectTaskInstance('ps1');
  const previous = controller.refresh();
  await Promise.resolve();
  await controller.selectTaskInstance('ps2');
  const next = controller.refresh();
  await Promise.resolve();
  pending.get('host2')(detail('new task'));
  await next;
  pending.get('host1')(detail('old task'));
  await previous;
  const state = controller.getState();
  assert.equal(state.selection.productSessionId, 'ps2');
  assert.equal(state.session.session.metadata.owner, 'new task');
  assert.equal(state.timeline.items[0].summary, 'new task');
  assert.equal(state.traces.items[0].label, 'new task');
  assert.equal(state.evidence.items[0].title, 'new task');
});
