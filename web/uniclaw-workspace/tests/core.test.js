'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { WorkspaceQueryCore, queryError, success } = require('../src/core');

function fixture(overrides = {}) {
  const base = {
    TaskQuery: {
      listProjects: async () => success({ projects: [{ id: 'p1', name: 'Project' }] }, { snapshotId: 's1', revision: 1, observedAt: '2026-10-02T00:00:00Z' }),
      listTaskInstances: async ({ projectId }) => success({ taskInstances: [{ id: 't1', projectId, productSessionId: 'ps1', source: 'uniclaw', authority: 'uniclaw-runtime', correlationId: 'req-1' }] }, { snapshotId: 's1', revision: 1 })
    },
    SessionQuery: {
      getSession: async () => success({ productSessionId: 'ps1', source: 'uniclaw', authority: 'uniclaw-runtime' }, { snapshotId: 's1', revision: 1 }),
      getTimeline: async () => success({ items: [{ kind: 'request', correlationId: 'req-1', source: 'uniclaw', authority: 'uniclaw-runtime' }] }, { snapshotId: 's1', revision: 1 })
    },
    TraceQuery: { getTraces: async () => success({ traces: [{ id: 'tr-1', productSessionId: 'ps1', source: 'dsh', authority: 'dsh-host', correlationId: 'req-1' }] }, { snapshotId: 's1', revision: 1 }) },
    EvidenceQuery: { getEvidence: async () => success({ evidence: [{ id: 'ev-1', productSessionId: 'ps1', source: 'uniclaw', authority: 'uniclaw-runtime', correlationId: 'req-1' }] }, { snapshotId: 's1', revision: 1 }) },
    DetailQuery: { resolveDetail: async () => success({ refId: 'evidence/ev-1', content: 'fixture' }, { snapshotId: 's1', revision: 1 }) }
  };
  return { ...base, ...overrides };
}

test('composes the five injected capabilities without source-specific coupling', async () => {
  const core = new WorkspaceQueryCore(fixture());
  const tasks = await core.listTaskInstances('p1');
  const timeline = await core.getTimeline('ps1');
  const traces = await core.getTraces('ps1');
  const evidence = await core.getEvidence('ps1');
  assert.equal(tasks.status, 'ready');
  assert.equal(tasks.items[0].productSessionId, 'ps1');
  assert.equal(timeline.items[0].correlationId, 'req-1');
  assert.equal(traces.items[0].source, 'dsh');
  assert.equal(evidence.items[0].authority, 'uniclaw-runtime');
});

test('replacing source implementations preserves read-model semantics', async () => {
  const replacement = fixture({
    TraceQuery: { getTraces: async () => success({ traces: [{ id: 'other-trace', productSessionId: 'ps1', source: 'external', authority: 'external-system', correlationId: 'req-1' }] }, { snapshotId: 'external-1', revision: 3 }) }
  });
  const traces = await new WorkspaceQueryCore(replacement).getTraces('ps1');
  assert.equal(traces.status, 'ready');
  assert.deepEqual(Object.keys(traces.items[0]).sort(), ['authority', 'correlationId', 'id', 'productSessionId', 'source']);
  assert.equal(traces.items[0].source, 'external');
});

test('local failures remain structured and are not converted to successful empty data', async () => {
  const error = queryError('permission-denied', 'Evidence access denied', 'filesystem', { correlationId: 'req-1', retryable: false });
  const core = new WorkspaceQueryCore(fixture({ EvidenceQuery: { getEvidence: async () => ({ ok: false, error }) } }));
  const result = await core.getEvidence('ps1');
  assert.equal(result.status, 'error');
  assert.equal(result.errors[0].code, 'permission-denied');
  assert.equal(result.errors[0].retryable, false);
});

test('partial page errors remain visible beside successful records', async () => {
  const error = queryError('timeout', 'Trace source timed out', 'otel');
  const core = new WorkspaceQueryCore(fixture({ TraceQuery: { getTraces: async () => success({ traces: [{ id: 'tr-1' }], errors: [error] }, { revision: 2 }) } }));
  const result = await core.getTraces('ps1');
  assert.equal(result.status, 'partial');
  assert.equal(result.items.length, 1);
  assert.equal(result.errors[0].code, 'timeout');
});

test('repeated reads are idempotent and an older response cannot replace a newer revision', async () => {
  let revision = 2;
  const core = new WorkspaceQueryCore(fixture({
    TaskQuery: {
      listProjects: async () => success({ projects: [{ id: `p${revision}` }] }, { revision }),
      listTaskInstances: async () => success({ taskInstances: [] }, { revision })
    }
  }));
  const first = await core.listProjects();
  const repeat = await core.listProjects();
  assert.deepEqual(repeat.items, first.items);
  revision = 1;
  const old = await core.listProjects();
  assert.equal(old.status, 'error');
  assert.equal(old.errors[0].code, 'stale');
  revision = 3;
  const fresh = await core.listProjects();
  assert.equal(fresh.items[0].id, 'p3');
});

test('stale failures preserve the incoming capability envelope metadata', async () => {
  let revision = 2;
  const core = new WorkspaceQueryCore(fixture({
    TaskQuery: {
      listProjects: async () => ({
        schemaVersion: 'uniclaw.workspace.capability.v1',
        contractVersion: 'uniclaw.workspace.contract.v1',
        capability: 'TaskQuery',
        ok: true,
        data: { projects: [{ id: `p${revision}` }] },
        revision,
        snapshotId: `snap-${revision}`,
        observedAt: '2026-10-02T01:02:03Z'
      })
    }
  }));
  await core.listProjects();
  revision = 1;
  const stale = await core.listProjects();
  assert.equal(stale.status, 'error');
  assert.equal(stale.errors[0].code, 'stale');
  assert.equal(stale.capability, 'TaskQuery');
  assert.equal(stale.revision, 1);
  assert.equal(stale.snapshotId, 'snap-1');
  assert.equal(stale.observedAt, '2026-10-02T01:02:03Z');
});

test('uncorrelated source failures are explicit', async () => {
  const error = queryError('uncorrelated', 'No ProductSession mapping', 'dsh', { retryable: false });
  const core = new WorkspaceQueryCore(fixture({ SessionQuery: { getSession: async () => ({ ok: false, error }), getTimeline: async () => ({ ok: false, error }) } }));
  const result = await core.getTimeline('ps-missing');
  assert.equal(result.status, 'error');
  assert.equal(result.errors[0].code, 'uncorrelated');
});

test('capability throws and malformed envelopes become structured unavailable errors', async () => {
  const core = new WorkspaceQueryCore(fixture({
    TaskQuery: {
      listProjects: async () => { throw new Error('source offline'); },
      listTaskInstances: async () => ({ data: { taskInstances: [] }, revision: 1 })
    }
  }));
  const thrown = await core.listProjects();
  const missingOk = await core.listTaskInstances('p1');
  assert.equal(thrown.errors[0].code, 'unavailable');
  assert.match(thrown.errors[0].message, /source offline/);
  assert.equal(missingOk.errors[0].code, 'unavailable');
});

test('successful responses require snapshot metadata and expected page keys', async () => {
  const core = new WorkspaceQueryCore(fixture({
    TaskQuery: {
      listProjects: async () => success({ projects: [] }),
      listTaskInstances: async () => success({ other: [] }, { revision: 1 })
    }
  }));
  const missingMetadata = await core.listProjects();
  const malformedPage = await core.listTaskInstances('p1');
  assert.equal(missingMetadata.errors[0].code, 'unavailable');
  assert.equal(malformedPage.errors[0].code, 'unavailable');
  assert.deepEqual((await new WorkspaceQueryCore(fixture()).listProjects()).items, [{ id: 'p1', name: 'Project' }]);
});

test('timeline requires an explicit array payload', async () => {
  const malformed = new WorkspaceQueryCore(fixture({
    SessionQuery: { getSession: fixture().SessionQuery.getSession, getTimeline: async () => success({}, { revision: 1 }) }
  }));
  const empty = new WorkspaceQueryCore(fixture({
    SessionQuery: { getSession: fixture().SessionQuery.getSession, getTimeline: async () => success({ items: [] }, { revision: 1 }) }
  }));
  assert.equal((await malformed.getTimeline('ps1')).status, 'error');
  assert.equal((await malformed.getTimeline('ps1')).errors[0].code, 'unavailable');
  assert.equal((await empty.getTimeline('ps1')).status, 'ready');
  assert.deepEqual((await empty.getTimeline('ps1')).items, []);
});

test('session projects capability envelopes into a stable read model', async () => {
  const core = new WorkspaceQueryCore(fixture());
  const ready = await core.getSession('ps1');
  assert.equal(ready.status, 'ready');
  assert.equal(ready.session.productSessionId, 'ps1');
  assert.deepEqual(ready.errors, []);
  const error = queryError('not-found', 'Session not found', 'uniclaw', { retryable: false });
  const failed = await new WorkspaceQueryCore(fixture({ SessionQuery: { getSession: async () => ({ ok: false, error }), getTimeline: fixture().SessionQuery.getTimeline } })).getSession('missing');
  assert.equal(failed.status, 'error');
  assert.equal(failed.session, null);
  assert.equal(failed.errors[0].code, 'not-found');
});

test('failed capability envelopes preserve source, capability, and available snapshot metadata', async () => {
  const error = queryError('timeout', 'source timed out', 'dsh-host');
  const failed = (capability) => async () => ({
    ok: false,
    error,
    capability,
    snapshotId: 'snap-failed',
    revision: 9,
    observedAt: '2026-10-02T01:02:03Z'
  });
  const core = new WorkspaceQueryCore(fixture({
    TaskQuery: { listProjects: failed('TaskQuery'), listTaskInstances: failed('TaskQuery') },
    SessionQuery: { getSession: failed('SessionQuery'), getTimeline: failed('SessionQuery') },
    TraceQuery: { getTraces: failed('TraceQuery') },
    EvidenceQuery: { getEvidence: failed('EvidenceQuery') },
    DetailQuery: { resolveDetail: failed('DetailQuery') }
  }));
  const results = await Promise.all([
    core.listProjects(),
    core.listTaskInstances('p1'),
    core.getSession('ps1'),
    core.getTimeline('ps1'),
    core.getTraces('ps1'),
    core.getEvidence('ps1'),
    core.resolveDetail({ source: 'dsh-host', refId: 'trace-1' })
  ]);
  for (const result of results) {
    assert.equal(result.status, 'error');
    assert.equal(result.errors[0], error);
    assert.equal(result.capability, result === results[0] || result === results[1] ? 'TaskQuery' : result === results[2] || result === results[3] ? 'SessionQuery' : result === results[4] ? 'TraceQuery' : result === results[5] ? 'EvidenceQuery' : 'DetailQuery');
    assert.equal(result.source, 'dsh-host');
    assert.equal(result.snapshotId, 'snap-failed');
    assert.equal(result.revision, 9);
    assert.equal(result.observedAt, '2026-10-02T01:02:03Z');
  }
});
