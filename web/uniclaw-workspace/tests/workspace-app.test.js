'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { WorkspaceQueryCore } = require('../src/core');
const { createStandaloneFixtureCapabilities } = require('../src/adapters/standalone-fixture');
const { createWorkspaceApp } = require('../src/app/create-workspace-app');

function fixture() {
  return {
    projects: [{ id: 'p1', name: 'Demo' }],
    tasks: [{ id: 't1', projectId: 'p1', productSessionId: 'ps1', title: 'Research' }],
    sessions: [{ productSessionId: 'ps1' }], timeline: [], traces: [], evidence: [], details: {}
  };
}
function core() { return new WorkspaceQueryCore(createStandaloneFixtureCapabilities({ fixture: fixture(), revision: 1 })); }

test('starts once, mounts standalone project data, and exposes controller state', async () => {
  const mounts = [];
  const app = createWorkspaceApp({ queryCore: core(), mount: (value) => mounts.push(value), render: (vm) => `<main>${vm.navigation.projects.length}</main>` });
  const first = app.start(); const second = app.start();
  assert.strictEqual(first, second);
  await first;
  assert.equal(mounts.at(-1).html, '<main>1</main>');
  assert.equal(app.getState().projects.status, 'ready');
  assert.equal(typeof app.getController().selectTaskInstance, 'function');
});

test('selection produces a new mount from one controller snapshot', async () => {
  const mounts = [];
  const app = createWorkspaceApp({ queryCore: core(), mount: (value) => mounts.push(value) });
  await app.start(); const before = mounts.length;
  await app.getController().selectProject('p1');
  await app.getController().selectTaskInstance('ps1');
  assert.ok(mounts.length > before);
  const latest = mounts.at(-1);
  assert.equal(latest.state.selection.productSessionId, 'ps1');
  assert.equal(latest.viewModel.navigation.selectedProductSessionId, 'ps1');
});

test('stop unsubscribes and prevents later mounts', async () => {
  const mounts = [];
  const app = createWorkspaceApp({ queryCore: core(), mount: (value) => mounts.push(value) });
  await app.start(); app.stop(); const count = mounts.length;
  await app.getController().selectTaskInstance('ps1');
  assert.equal(mounts.length, count);
});

test('stop then start creates a fresh lifecycle and mounts again', async () => {
  const mounts = [];
  const app = createWorkspaceApp({ queryCore: core(), mount: (value) => mounts.push(value) });
  await app.start(); app.stop(); const stoppedCount = mounts.length;
  await app.start();
  assert.ok(mounts.length > stoppedCount);
});

test('stopping an in-flight start prevents its later mount and allows restart', async () => {
  let release;
  const pending = new Promise((resolve) => { release = resolve; });
  const queryCore = { listProjects: () => pending };
  const mounts = [];
  const app = createWorkspaceApp({ queryCore, mount: (value) => mounts.push(value) });
  const first = app.start();
  app.stop();
  release({ status: 'ready', items: [], errors: [], revision: 1 });
  await first;
  const stoppedCount = mounts.length;
  const second = app.start();
  release({ status: 'ready', items: [], errors: [], revision: 2 });
  await second;
  assert.ok(mounts.length > stoppedCount);
});

test('query failures remain in controller state and renderer errors are observable', async () => {
  const failing = { listProjects: async () => ({ status: 'error', items: [], errors: [{ code: 'unavailable', message: 'offline' }] }) };
  const queryApp = createWorkspaceApp({ queryCore: failing, mount: () => {} });
  await queryApp.start();
  assert.equal(queryApp.getState().projects.errors[0].code, 'unavailable');

  const rendererError = new Error('render failed');
  const renderApp = createWorkspaceApp({ queryCore: core(), render: () => { throw rendererError; }, mount: () => {} });
  await assert.rejects(renderApp.start(), rendererError);
  assert.strictEqual(renderApp.getError(), rendererError);
});

test('mount failure is observable and dependencies are replaceable', async () => {
  const mountError = new Error('mount failed');
  const app = createWorkspaceApp({ queryCore: core(), render: (vm) => vm.status, mount: () => { throw mountError; } });
  await assert.rejects(app.start(), mountError);
  assert.strictEqual(app.getError(), mountError);
});
