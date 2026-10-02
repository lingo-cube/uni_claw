'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { WorkspaceQueryCore } = require('../src/core');
const { createStandaloneFixtureCapabilities } = require('../src/adapters/standalone-fixture');

function makeFixture() {
  return {
    projects: [{ id: 'proj-1', name: 'UniClaw Demo' }],
    tasks: [{ id: 'task-1', projectId: 'proj-1', productSessionId: 'ps-1', source: 'uniclaw', authority: 'uniclaw-runtime' }],
    sessions: [{ productSessionId: 'ps-1', source: 'uniclaw', authority: 'uniclaw-runtime', dshSessionId: 'dsh-1' }],
    timeline: [{ id: 'request-1', productSessionId: 'ps-1', kind: 'request', content: '研究请求', source: 'uniclaw' }],
    traces: [
      { id: 'dsh-1', productSessionId: 'ps-1', source: 'dsh', authority: 'dsh-host' },
      { id: 'uf-1', productSessionId: 'ps-1', source: 'uniflow', authority: 'uniclaw-harness' },
      { id: 'uc-1', productSessionId: 'ps-1', source: 'uniclaw', authority: 'uniclaw-runtime' }
    ],
    evidence: [{ id: 'ev-1', productSessionId: 'ps-1', source: 'uniclaw', authority: 'uniclaw-runtime', detailRef: { source: 'standalone', refId: 'evidence/ev-1.md' } }],
    details: { 'standalone:evidence/ev-1.md': { refId: 'evidence/ev-1.md', content: '受控证据' } },
    deniedDetailRefs: ['evidence/denied.md']
  };
}

test('runs the complete read-only flow through the shared Query Core', async () => {
  const core = new WorkspaceQueryCore(createStandaloneFixtureCapabilities({ fixture: makeFixture(), now: '2026-10-02T00:00:00Z', revision: 7 }));
  const projects = await core.listProjects();
  const tasks = await core.listTaskInstances('proj-1');
  const session = await core.getSession('ps-1');
  const timeline = await core.getTimeline('ps-1');
  const traces = await core.getTraces('ps-1');
  const evidence = await core.getEvidence('ps-1');
  const detail = await core.resolveDetail(evidence.items[0].detailRef);
  assert.equal(projects.items[0].id, 'proj-1');
  assert.equal(tasks.items[0].productSessionId, 'ps-1');
  assert.equal(session.session.productSessionId, 'ps-1');
  assert.equal(timeline.items[0].productSessionId, 'ps-1');
  assert.deepEqual(traces.items.map((item) => item.source), ['dsh', 'uniflow', 'uniclaw']);
  assert.equal(detail.detail.content, '受控证据');
});

test('is source-replaceable, repeatable, and does not mutate the fixture', async () => {
  const fixture = makeFixture();
  const before = JSON.stringify(fixture);
  const core = new WorkspaceQueryCore(createStandaloneFixtureCapabilities({ fixture, revision: 1 }));
  const first = await core.listProjects();
  const repeat = await core.listProjects();
  assert.deepEqual(repeat.items, first.items);
  assert.equal(JSON.stringify(fixture), before);
  const replacement = new WorkspaceQueryCore(createStandaloneFixtureCapabilities({ fixture: { ...fixture, projects: [{ id: 'other', name: 'Other' }] }, revision: 2 }));
  assert.equal((await replacement.listProjects()).items[0].id, 'other');
});

test('returns structured missing, permission, and stale errors', async () => {
  const core = new WorkspaceQueryCore(createStandaloneFixtureCapabilities({ fixture: makeFixture(), revision: 2 }));
  assert.equal((await core.getSession('missing')).errors[0].code, 'not-found');
  assert.equal((await core.resolveDetail({ source: 'standalone', refId: 'evidence/denied.md' })).errors[0].code, 'permission-denied');
  assert.equal((await core.listProjects({ revision: 3 })).errors[0].code, 'stale');
  assert.equal((await core.resolveDetail({ source: 'standalone', refId: 'evidence/unknown.md' })).errors[0].code, 'not-found');
});
