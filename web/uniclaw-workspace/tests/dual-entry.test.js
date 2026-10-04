'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { createStandaloneWorkspaceHost } = require('../src/hosts/create-standalone-host');

const revision = 7;
const fixture = () => ({
  projects: [{ id: 'p', name: 'Demo' }],
  tasks: [{ id: 't', projectId: 'p', productSessionId: 'product-1', title: 'Research' }],
  sessions: [{ productSessionId: 'product-1', metadata: { owner: 'uni-agent' } }],
  timeline: [
    { id: 'r', kind: 'request', summary: '用户请求' },
    { id: 'd', kind: 'decision', summary: 'Agent 决策' },
    { id: 'o', kind: 'result', summary: '执行结果' },
  ],
  traces: [
    { traceId: 'u', source: 'uniclaw', detailRef: { source: 'uniclaw', refId: 'trace/u' } },
    { traceId: 'd', source: 'dsh' },
  ],
  evidence: [{ evidenceId: 'e', source: 'standalone', detailRef: { source: 'standalone', refId: 'evidence/e' } }],
  details: { 'standalone:evidence/e': { text: 'evidence detail' } },
});

async function run(host) {
  await host.start();
  const controller = host.getController();
  await controller.selectProject('p');
  await controller.selectTaskInstance('product-1');
  await Promise.all([controller.loadSession(), controller.loadTimeline(), controller.loadTraces(), controller.loadEvidence()]);
  await controller.resolveDetail({ source: 'standalone', refId: 'evidence/e' });
  return host;
}

test('standalone entry completes the canonical flow and exposes renderer-neutral regions', async () => {
  const mounts = [];
  const host = await run(createStandaloneWorkspaceHost({ fixture: fixture(), revision, mount: value => mounts.push(value) }));
  const vm = mounts.at(-1).viewModel;
  assert.deepEqual(Object.keys(vm).sort(), ['activePane', 'conversationTimeline', 'detailActions', 'evidencePane', 'executionPane', 'launchComposer', 'metadataPane', 'navigation', 'notices', 'status', 'taskHeader', 'tracePane']);
  assert.deepEqual(Object.keys(vm.conversationTimeline.groups).sort(), ['decision', 'request', 'result']);
  assert.equal(vm.taskHeader.productSessionId, 'product-1');
  assert.equal(vm.detailActions.status, 'ready');
  assert.equal(host.getState().traces.revision, revision);
});

test('standalone keeps local failures and uncorrelated records explicit', async () => {
  const bad = fixture();
  bad.deniedDetailRefs = ['evidence/denied'];
  const host = createStandaloneWorkspaceHost({ fixture: bad, revision, mount: () => {} });
  await host.start();
  await host.getController().selectProject('p');
  await host.getController().selectTaskInstance('missing-product');
  await host.getController().loadSession();
  assert.equal(host.getState().session.errors[0].code, 'not-found');
  assert.equal(host.getState().session.errors[0].source, 'standalone-fixture');
  await host.getController().selectTaskInstance('product-1');
  await host.getController().resolveDetail({ source: 'standalone', refId: 'evidence/denied' });
  const state = host.getState();
  assert.equal(state.detail.errors[0].code, 'permission-denied');
  assert.equal(state.detail.errors[0].source, 'standalone-fixture');
  assert.equal(state.detail.status, 'error');
});

test('standalone restart preserves ProductSessionId and does not leak host identity', async () => {
  const host = await run(createStandaloneWorkspaceHost({ fixture: fixture(), mount: () => {} }));
  assert.equal(host.getState().selection.productSessionId, 'product-1');
  host.stop();
  await host.start();
  assert.equal(host.getState().selection.productSessionId, 'product-1');
  assert.equal(JSON.stringify(host.getState()).includes('dshSessionId'), false);
});
