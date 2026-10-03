'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { ensureWorkspaceStyles, envelope, createCapabilities } = require('../src/browser/entry');

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
