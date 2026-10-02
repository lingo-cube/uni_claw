'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { createWorkspaceViewModel } = require('../src/features/workspace-view-model');

function state(overrides = {}) {
  return {
    projects: { status: 'ready', items: [{ id: 'p1', name: '调研项目', tasks: [{ productSessionId: 'ps1', title: 'Uni-Agent 调研', status: 'running' }, { instanceId: 'observed:dsh2', observed: true, correlationStatus: 'uncorrelated', title: '孤立 DSH 观察' }] }], errors: [] },
    selection: { projectId: 'p1', productSessionId: 'ps1', taskInstances: { status: 'ready', items: [{ productSessionId: 'ps1', title: 'Uni-Agent 调研', projectId: 'p1', metadata: { owner: 'team-a' } }, { instanceId: 'observed:dsh2', observed: true, correlationStatus: 'uncorrelated', title: '孤立 DSH 观察' }], errors: [] } },
    session: { status: 'ready', session: { productSessionId: 'ps1', source: 'uniclaw', authority: 'runtime', metadata: { priority: 'high' } }, errors: [] },
    timeline: { status: 'ready', items: [{ id: 'r', kind: 'request', summary: '调研请求', detailRef: { source: 'u', refId: 'r' } }, { id: 'd', kind: 'decision', summary: '选择搜索策略' }, { id: 'o', kind: 'result', summary: '得到证据' }], errors: [] },
    traces: { status: 'ready', items: [{ id: 'a', source: 'uniclaw', authority: 'runtime', correlationId: 'c1' }, { id: 'b', source: 'dsh', authority: 'host', detailRef: { source: 'dsh', refId: 'b' } }, { id: 'c', source: 'uniflow', authority: 'harness' }], errors: [] },
    evidence: { status: 'partial', items: [{ id: 'e1', title: '报告', source: 'uniclaw', productSessionId: 'ps1', detailRef: { source: 'u', refId: 'e1' } }, { id: 'e2', title: '受限文件', detailRef: { source: 'u', refId: 'denied' }, error: { code: 'permission-denied', message: '拒绝' } }, { id: 'e3', title: '孤立证据', correlationStatus: 'uncorrelated' }], errors: [{ code: 'stale', message: '旧快照' }] },
    detail: { status: 'idle', detail: null, errors: [] },
    ...overrides
  };
}

test('projects complete controller state into renderer-neutral sections', () => {
  const input = state();
  const before = JSON.stringify(input);
  const view = createWorkspaceViewModel(input);
  assert.equal(view.navigation.selectedProductSessionId, 'ps1');
  assert.equal(view.taskHeader.productSessionId, 'ps1');
  assert.deepEqual(Object.keys(view.conversationTimeline.groups), ['request', 'decision', 'result']);
  assert.equal(view.conversationTimeline.groups.request[0].summary, '调研请求');
  assert.deepEqual(Object.keys(view.tracePane.groups).sort(), ['dsh', 'uniclaw', 'uniflow']);
  assert.equal(view.evidencePane.items[0].detailAction.enabled, true);
  assert.equal(view.evidencePane.items[1].detailAction.enabled, false);
  assert.equal(view.evidencePane.items[1].detailAction.reason, 'permission-denied');
  assert.equal(view.evidencePane.items[2].detailAction.enabled, false);
  assert.equal(view.evidencePane.items[2].detailAction.reason, 'uncorrelated');
  assert.equal(view.metadataPane.items.owner, 'team-a');
  assert.equal(view.metadataPane.items.priority, 'high');
  assert.equal(view.status, 'partial');
  assert.equal(JSON.stringify(input), before);
});

test('keeps uncorrelated instances visible without inventing product identity', () => {
  const view = createWorkspaceViewModel(state());
  const orphan = view.navigation.taskInstances.find((item) => item.observed);
  assert.equal(orphan.productSessionId, null);
  assert.equal(orphan.correlationStatus, 'uncorrelated');
  assert.equal(view.navigation.projects[0].tasks[1].productSessionId, null);
});

test('blocks detail action for uncorrelated evidence even when a reference exists', () => {
  const view = createWorkspaceViewModel(state({ evidence: { status: 'ready', items: [{ id: 'orphan-evidence', correlationStatus: 'uncorrelated', detailRef: { source: 'u', refId: 'orphan.md' } }], errors: [] } }));
  assert.equal(view.evidencePane.items[0].detailAction.enabled, false);
  assert.equal(view.evidencePane.items[0].detailAction.reason, 'uncorrelated');
  assert.ok(view.notices.some((notice) => notice.code === 'uncorrelated'));
  assert.equal(view.status, 'partial');
});

test('preserves loading, errors, stale and missing detail states', () => {
  const view = createWorkspaceViewModel(state({ timeline: { status: 'error', items: [], errors: [{ code: 'unavailable', message: 'offline' }] }, traces: { status: 'loading', items: [], errors: [] }, evidence: { status: 'ready', items: [{ id: 'e', title: 'missing' }], errors: [] } }));
  assert.equal(view.conversationTimeline.status, 'error');
  assert.equal(view.tracePane.status, 'loading');
  assert.equal(view.evidencePane.items[0].detailAction.enabled, false);
  assert.equal(view.evidencePane.items[0].detailAction.reason, 'not-found');
  assert.ok(view.notices.some((notice) => notice.code === 'unavailable'));
  assert.equal(view.status, 'loading');
});

test('does not expose renderer or host dependencies', () => {
  const source = require('node:fs').readFileSync(require('node:path').join(__dirname, '../src/features/workspace-view-model.js'), 'utf8');
  assert.doesNotMatch(source, /react|document\.|window\.|fetch\(|node:fs|node:path|dsh|host/i);
});
