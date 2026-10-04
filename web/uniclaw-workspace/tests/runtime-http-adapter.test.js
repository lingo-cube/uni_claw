'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { WorkspaceQueryCore } = require('../src/core');
const { createRuntimeHttpCapabilities } = require('../src/adapters/runtime-http');

function fixtureRun(overrides = {}) {
  return {
    schemaVersion: 'uniclaw.workspace.runtime-run-projection.v1',
    contractVersion: 'uniclaw.workspace.contract.v1',
    runId: 'run-1',
    productSessionId: 'session-1',
    taskInstanceId: 'task-1',
    hostSessionRef: { host: 'dsh', sessionId: 'dsh-1' },
    projectRef: { id: 'project-a', label: 'Project A' },
    testSetRef: { id: 'smoke', version: 'default' },
    taskRef: { id: 'task-a', label: 'Wi-Fi check' },
    launchId: 'launch-1',
    idempotencyKey: 'idem-1',
    correlationId: 'corr-1',
    environment: { device: { id: 'emulator-5556' } },
    status: 'completed',
    phase: 'finalize',
    outcome: 'completion',
    reason: 'done',
    revision: 4,
    observedAt: '2026-10-04T06:00:04Z',
    lastEventSequence: 2,
    consistency: 'current',
    artifacts: [{ artifactId: 'facts-1', kind: 'facts', availability: 'present', detailEndpoint: 'runs/run-1/artifacts/facts' }],
    ...overrides,
  };
}

function createFetch() {
  const run = fixtureRun();
  const events = [{ eventId: 'event-1', runId: run.runId, eventType: 'run.completed', source: 'runtime', authority: 'uniclaw-runtime', occurredAt: run.observedAt, sequence: 2, summary: 'done', payloadRef: 'runs/run-1/events/2' }];
  const calls = [];
  const fetchImpl = async (url, init = {}) => {
    calls.push({ url, init });
    const parsed = new URL(url);
    if (parsed.pathname.endsWith('/events')) return response(200, { ok: true, runId: run.runId, events, revision: 4, observedAt: run.observedAt });
    if (parsed.pathname.endsWith('/runs/run-1')) return response(200, { ok: true, run, revision: 4, observedAt: run.observedAt });
    if (parsed.pathname.endsWith('/runs') && init.method === 'POST') return response(202, { ok: true, run, revision: 1, observedAt: run.observedAt });
    if (parsed.pathname.endsWith('/runs')) return response(200, { ok: true, runs: [run], revision: 4, observedAt: run.observedAt });
    return response(404, { ok: false, error: { code: 'runtime-run-not-found', message: 'missing', retryable: false, details: {} } });
  };
  return { fetchImpl, calls };
}

function response(status, body) {
  return { ok: status >= 200 && status < 300, status, json: async () => body };
}

test('maps Runtime Run projections into host-neutral workspace capabilities', async () => {
  const { fetchImpl } = createFetch();
  const capabilities = createRuntimeHttpCapabilities({ baseUrl: 'http://runtime.test', fetchImpl, detailResolver: async () => ({ text: 'facts' }) });
  const core = new WorkspaceQueryCore(capabilities);

  const projects = await core.listProjects();
  assert.equal(projects.status, 'ready');
  assert.equal(projects.items[0].projectId, 'project-a');
  assert.equal(projects.items[0].instances[0].runId, 'run-1');
  assert.equal((await core.listTaskInstances('project-a')).items[0].status, 'completed');
  assert.equal((await core.getSession('session-1')).session.hostSessionRef.sessionId, 'dsh-1');
  assert.equal((await core.getTimeline('session-1')).items[0].kind, 'run.completed');
  assert.equal((await core.getTimeline('session-1')).items[0].runId, 'run-1');
  assert.equal((await core.getTraces('session-1')).items[0].source, 'runtime');
  assert.equal((await core.getEvidence('session-1')).items[0].detailRef.refId, 'runs/run-1/artifacts/facts');
  assert.deepEqual((await core.resolveDetail({ refId: 'facts', source: 'uniclaw-runtime' })).detail, { text: 'facts' });
});

test('launches through Runtime HTTP without coupling the UI to DSH', async () => {
  const { fetchImpl, calls } = createFetch();
  const capabilities = createRuntimeHttpCapabilities({ baseUrl: 'http://runtime.test', fetchImpl });
  const result = await capabilities.TaskCommand.launchTask({
    launchRequestId: 'launch-request-1',
    idempotencyKey: 'idem-request-1',
    correlationId: 'corr-request-1',
    projectRef: { id: 'project-a' },
    testSetRef: { id: 'smoke', version: 'default' },
    taskRef: { id: 'task-a' },
    environmentIntent: { device: { id: 'emulator-5556' } },
  });
  assert.equal(result.ok, true);
  assert.equal(result.data.run.runId, 'run-1');
  const body = JSON.parse(calls.at(-1).init.body);
  assert.equal(body.launchId, 'launch-request-1');
  assert.equal(body.projectRef.id, 'project-a');
  assert.equal(body.environmentIntent.device.id, 'emulator-5556');
  const core = new WorkspaceQueryCore(capabilities);
  assert.equal((await core.launchTask({ taskRef: { id: 'task-a' } })).status, 'ready');
});

test('converts Runtime HTTP business errors to shared query errors', async () => {
  const capabilities = createRuntimeHttpCapabilities({
    baseUrl: 'http://runtime.test',
    fetchImpl: async () => response(404, { ok: false, error: { code: 'runtime-run-not-found', message: 'missing', retryable: false, details: {} } }),
  });
  const result = await capabilities.TaskQuery.listProjects();
  assert.equal(result.ok, false);
  assert.equal(result.error.code, 'not-found');
  assert.equal(result.error.source, 'uniclaw-runtime');
});
