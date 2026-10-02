'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { createStandaloneWorkspaceHost } = require('../src/hosts/create-standalone-host');

function makeFixture() {
  return {
    projects: [{ id: 'project-1', name: 'Demo' }],
    tasks: [{ id: 'task-1', projectId: 'project-1', productSessionId: 'product-session-1', title: 'Research' }],
    sessions: [{ productSessionId: 'product-session-1' }],
    timeline: [], traces: [], evidence: [], details: {}
  };
}

test('starts, loads projects, selects a project and ProductSession task', async () => {
  const mounts = [];
  const host = createStandaloneWorkspaceHost({ fixture: makeFixture(), mount: (value) => mounts.push(value) });
  await host.start();
  await host.getController().selectProject('project-1');
  await host.getController().selectTaskInstance('product-session-1');
  assert.equal(host.getState().projects.items[0].id, 'project-1');
  assert.equal(host.getState().selection.productSessionId, 'product-session-1');
  assert.equal(mounts.at(-1).state.selection.productSessionId, 'product-session-1');
  assert.equal(typeof mounts.at(-1).html, 'string');
  assert.equal(mounts.at(-1).viewModel.navigation.selectedProductSessionId, 'product-session-1');
});

test('restart preserves fixture identity and does not mutate fixture', async () => {
  const fixture = makeFixture();
  const before = JSON.stringify(fixture);
  const host = createStandaloneWorkspaceHost({ fixture, mount: () => {} });
  const firstApp = host.getApp();
  await host.start();
  host.stop();
  await host.start();
  assert.strictEqual(host.getApp(), firstApp);
  assert.equal(JSON.stringify(fixture), before);
  assert.equal(host.getState().projects.items[0].id, 'project-1');
});

test('mount receives html, viewModel and state', async () => {
  let received;
  const host = createStandaloneWorkspaceHost({
    fixture: makeFixture(),
    render: (viewModel) => `<workspace>${viewModel.navigation.projects.length}</workspace>`,
    mount: (value) => { received = value; }
  });
  await host.start();
  assert.equal(received.html, '<workspace>1</workspace>');
  assert.ok(received.viewModel);
  assert.ok(received.state);
});

test('render and mount errors are observable through the host', async () => {
  const renderError = new Error('render failed');
  const renderHost = createStandaloneWorkspaceHost({ fixture: makeFixture(), render: () => { throw renderError; }, mount: () => {} });
  await assert.rejects(renderHost.start(), renderError);
  assert.strictEqual(renderHost.getError(), renderError);

  const mountError = new Error('mount failed');
  const mountHost = createStandaloneWorkspaceHost({ fixture: makeFixture(), mount: () => { throw mountError; } });
  await assert.rejects(mountHost.start(), mountError);
  assert.strictEqual(mountHost.getError(), mountError);
});

test('fixture validation errors remain observable and public API has no DSH session field', () => {
  assert.throws(() => createStandaloneWorkspaceHost({ fixture: null, mount: () => {} }), /fixture is required/);
  const host = createStandaloneWorkspaceHost({ fixture: makeFixture(), mount: () => {} });
  assert.deepEqual(Object.keys(host).sort(), ['getApp', 'getController', 'getError', 'getState', 'start', 'stop']);
});
