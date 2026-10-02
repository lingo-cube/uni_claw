'use strict';

const { WorkspaceQueryCore } = require('../core');
const { createStandaloneFixtureCapabilities } = require('../adapters/standalone-fixture');
const { createWorkspaceApp } = require('../app/create-workspace-app');

function createStandaloneWorkspaceHost({ fixture, mount, render, viewOptions, now, revision } = {}) {
  if (!fixture || typeof fixture !== 'object') throw new TypeError('fixture is required');
  if (typeof mount !== 'function') throw new TypeError('mount is required');

  const capabilities = createStandaloneFixtureCapabilities({ fixture, now, revision });
  const queryCore = new WorkspaceQueryCore(capabilities);
  const app = createWorkspaceApp({ queryCore, mount, ...(render ? { render } : {}), viewOptions });

  return Object.freeze({
    start: () => app.start(),
    stop: () => app.stop(),
    getApp: () => app,
    getController: () => app.getController(),
    getState: () => app.getState(),
    getError: () => app.getError()
  });
}

module.exports = { createStandaloneWorkspaceHost };
