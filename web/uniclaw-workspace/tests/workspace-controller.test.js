'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { WorkspaceQueryCore } = require('../src/core');
const { createStandaloneFixtureCapabilities } = require('../src/adapters/standalone-fixture');
const { createWorkspaceController } = require('../src/features/workspace-controller');

function fixture() { return { projects: [{ id: 'p1', name: 'Demo' }], tasks: [{ id: 't1', projectId: 'p1', productSessionId: 'ps1' }], sessions: [{ productSessionId: 'ps1' }], timeline: [{ productSessionId: 'ps1', kind: 'request' }], traces: [{ productSessionId: 'ps1', source: 'uniflow' }], evidence: [{ productSessionId: 'ps1', detailRef: { source: 'fixture', refId: 'ev.md' } }], details: { 'fixture:ev.md': { content: 'evidence' } } }; }
function controller(overrides = {}) { return createWorkspaceController({ queryCore: new WorkspaceQueryCore(createStandaloneFixtureCapabilities({ fixture: { ...fixture(), ...overrides }, revision: 1 })) }); }

test('runs project to task to read-model detail flow', async () => {
  const c = controller();
  await c.loadProjects(); await c.selectProject('p1'); await c.selectTaskInstance('ps1');
  await Promise.all([c.loadSession(), c.loadTimeline(), c.loadTraces(), c.loadEvidence()]);
  const evidence = c.getState().evidence.items[0];
  await c.resolveDetail(evidence.detailRef);
  const state = c.getState();
  assert.equal(state.projects.items[0].id, 'p1'); assert.equal(state.selection.productSessionId, 'ps1');
  assert.equal(state.session.session.productSessionId, 'ps1'); assert.equal(state.timeline.items[0].kind, 'request');
  assert.equal(state.traces.items[0].source, 'uniflow'); assert.equal(state.detail.detail.content, 'evidence');
});

test('preserves local errors and lazy detail failures', async () => {
  const c = controller({ deniedDetailRefs: ['denied.md'] });
  await c.selectTaskInstance('ps1'); await c.loadEvidence();
  const missing = await c.resolveDetail(); const denied = await c.resolveDetail({ source: 'fixture', refId: 'denied.md' });
  assert.equal(missing.detail.status, 'error'); assert.equal(missing.detail.errors[0].code, 'not-found');
  assert.equal(denied.detail.errors[0].code, 'permission-denied');
});

test('selection is product-session based and old async results cannot overwrite it', async () => {
  let release;
  const core = { listProjects: async () => ({ status: 'ready', items: [], errors: [], revision: 1 }), listTaskInstances: async () => ({ status: 'ready', items: [], errors: [], revision: 1 }), getSession: async (id) => new Promise((resolve) => { release = () => resolve({ status: 'ready', session: { productSessionId: id }, errors: [], revision: id === 'old' ? 1 : 2 }); }), getTimeline: async () => ({ status: 'ready', items: [], errors: [], revision: 1 }), getTraces: async () => ({ status: 'ready', items: [], errors: [], revision: 1 }), getEvidence: async () => ({ status: 'ready', items: [], errors: [], revision: 1 }), resolveDetail: async () => ({ status: 'error', errors: [{ code: 'not-found' }] }) };
  const c = createWorkspaceController({ queryCore: core });
  await c.selectTaskInstance('old'); const pending = c.loadSession('old'); await c.selectTaskInstance('new'); release(); await pending;
  assert.equal(c.getState().selection.productSessionId, 'new'); assert.equal(c.getState().session.status, 'idle');
});

test('project task list preserves loading, partial, and error read-model states', async () => {
  let resolveOld;
  const oldPromise = new Promise((resolve) => { resolveOld = resolve; });
  const error = { code: 'unavailable', message: 'offline', retryable: true, source: 'fixture' };
  const core = {
    listProjects: async () => ({ status: 'ready', items: [], errors: [], revision: 1 }),
    listTaskInstances: async (projectId) => projectId === 'old'
      ? oldPromise
      : projectId === 'error'
        ? ({ status: 'error', items: [], errors: [error], revision: 3 })
      : ({ status: 'partial', items: [{ productSessionId: 'ps-new' }], errors: [error], revision: 2 }),
    getSession: async () => ({ status: 'ready', session: null, errors: [], revision: 1 }),
    getTimeline: async () => ({ status: 'ready', items: [], errors: [], revision: 1 }),
    getTraces: async () => ({ status: 'ready', items: [], errors: [], revision: 1 }),
    getEvidence: async () => ({ status: 'ready', items: [], errors: [], revision: 1 }),
    resolveDetail: async () => ({ status: 'ready', detail: {}, errors: [], revision: 1 })
  };
  const c = createWorkspaceController({ queryCore: core });
  const old = c.selectProject('old');
  assert.equal(c.getState().selection.taskInstances.status, 'loading');
  const next = c.selectProject('new');
  await next;
  assert.equal(c.getState().selection.taskInstances.status, 'partial');
  assert.equal(c.getState().selection.taskInstances.items[0].productSessionId, 'ps-new');
  assert.equal(c.getState().selection.taskInstances.errors[0].code, 'unavailable');
  resolveOld({ status: 'error', items: [], errors: [error], revision: 1 });
  await old;
  assert.equal(c.getState().selection.projectId, 'new');
  assert.equal(c.getState().selection.taskInstances.status, 'partial');
  await c.selectProject('error');
  assert.equal(c.getState().selection.taskInstances.status, 'error');
  assert.equal(c.getState().selection.taskInstances.errors[0].code, 'unavailable');
});

test('old lazy detail cannot overwrite after task selection changes', async () => {
  let resolveDetail;
  const detailPromise = new Promise((resolve) => { resolveDetail = resolve; });
  const base = fixture();
  const capabilities = createStandaloneFixtureCapabilities({ fixture: base, revision: 1 });
  const core = new WorkspaceQueryCore({
    ...capabilities,
    DetailQuery: { resolveDetail: async () => detailPromise }
  });
  const c = createWorkspaceController({ queryCore: core });
  await c.selectTaskInstance('ps1');
  const pending = c.resolveDetail({ source: 'fixture', refId: 'ev.md' });
  assert.equal(c.getState().detail.status, 'loading');
  await c.selectTaskInstance('ps2');
  resolveDetail({ ok: true, data: { content: 'old' }, revision: 1 });
  await pending;
  assert.equal(c.getState().selection.productSessionId, 'ps2');
  assert.equal(c.getState().detail.status, 'idle');
  assert.equal(c.getState().detail.detail, null);
});

test('detail query receives current product identity without mutating DetailRef', async () => {
  const calls = [];
  const c = createWorkspaceController({ queryCore: {
    resolveDetail: async (...args) => { calls.push(args); return { status: 'ready', detail: { content: 'ok' }, errors: [], revision: 1 }; }
  } });
  const ref = { source: 'fixture', refId: 'ev.md' };
  const noSelection = await c.resolveDetail(ref);
  assert.equal(noSelection.detail.status, 'error');
  assert.equal(calls.length, 0);
  await c.selectTaskInstance('ps1');
  await c.resolveDetail(ref);
  assert.deepEqual(calls[0], [ref, { productSessionId: 'ps1' }]);
  assert.deepEqual(ref, { source: 'fixture', refId: 'ev.md' });
});

test('refresh is repeatable and controller remains renderer independent', async () => {
  const c = controller(); await c.selectTaskInstance('ps1'); const first = await c.refresh(); const second = await c.refresh();
  assert.deepEqual(second.projects.items, first.projects.items); assert.equal(typeof c.subscribe, 'function');
});

test('launch action reports loading then refreshes the selected task', async () => {
  const calls = [];
  const c = createWorkspaceController({ queryCore: {
    listProjects: async () => ({ status: 'ready', items: [{ projectId: 'p1', instances: [{ productSessionId: 'ps1', taskRef: { id: 'task/p/t' } }] }], errors: [], revision: 1 }),
    listTaskInstances: async () => ({ status: 'ready', items: [{ productSessionId: 'ps1', taskRef: { id: 'task/p/t' } }], errors: [], revision: 1 }),
    getSession: async () => ({ status: 'ready', session: {}, errors: [], revision: 1 }), getTimeline: async () => ({ status: 'ready', items: [], errors: [], revision: 1 }), getTraces: async () => ({ status: 'ready', items: [], errors: [], revision: 1 }), getEvidence: async () => ({ status: 'ready', items: [], errors: [], revision: 1 }),
    launchTask: async (request) => { calls.push(request); return { status: 'ready', data: { ack: { status: 'completed' } }, errors: [], revision: 2 }; }
  } });
  await c.selectProject('p1'); await c.selectTaskInstance('ps1');
  await c.launchTask({ taskRef: { id: 'task/p/t' } });
  assert.equal(calls.length, 1);
  assert.equal(c.getState().launch.status, 'ready');
});

test('launch action keeps partial Host recovery visible without refreshing as success', async () => {
  let refreshed = 0;
  const c = createWorkspaceController({ queryCore: {
    listProjects: async () => ({ status: 'ready', items: [], errors: [], revision: 1 }),
    listTaskInstances: async () => ({ status: 'ready', items: [], errors: [], revision: 1 }),
    launchTask: async () => ({ status: 'partial', data: { status: 'partial' }, errors: [{ code: 'launch-partial', message: 'Host session pending' }], revision: 2 }),
    refresh: async () => { refreshed += 1; return { status: 'ready', items: [], errors: [], revision: 3 }; }
  } });
  await c.launchTask({});
  assert.equal(c.getState().launch.status, 'partial');
  assert.equal(refreshed, 0);
});

test('pane selection is durable in renderer-neutral controller state', async () => {
  const c = controller();
  c.selectPane('evidence');
  assert.equal(c.getState().ui.activePane, 'evidence');
  c.selectPane('unknown');
  assert.equal(c.getState().ui.activePane, 'trace');
});

test('trace mode and detail return pane are durable in controller state', async () => {
  const c = controller();
  c.selectTraceMode('split');
  assert.equal(c.getState().ui.traceMode, 'split');
  c.selectTraceSource('uniclaw');
  assert.equal(c.getState().ui.traceSource, 'uniclaw');
  await c.selectTaskInstance('ps1');
  c.selectPane('evidence');
  await c.resolveDetail({ source: 'fixture', refId: 'ev.md' });
  assert.equal(c.getState().ui.activePane, 'evidence');
  assert.equal(c.getState().ui.detailReturnPane, 'evidence');
  assert.equal(c.getState().ui.detailModalOpen, true);
  c.closeDetail();
  assert.equal(c.getState().ui.detailModalOpen, false);
  c.selectPane(c.getState().ui.detailReturnPane);
  assert.equal(c.getState().ui.activePane, 'evidence');
});

test('trace node inspection opens a local detail modal without fetching the whole artifact', async () => {
  const c = controller();
  await c.selectTaskInstance('ps1');
  await c.loadTraces('ps1');
  c.inspectTrace(0);
  const state = c.getState();
  assert.equal(state.ui.detailModalOpen, true);
  assert.equal(state.detail.status, 'ready');
  assert.equal(state.detail.detail.format, 'trace-record');
  assert.equal(state.detail.detail.record.source, 'uniflow');
});
