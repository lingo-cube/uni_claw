import { createRequire } from 'node:module'

import { createDshWorkspaceCapabilities } from './workspace-capabilities.js'

const require = createRequire(import.meta.url)
const { WorkspaceQueryCore } = require('../../../web/uniclaw-workspace/src/core/index.js')
const { createWorkspaceApp } = require('../../../web/uniclaw-workspace/src/app/create-workspace-app.js')

function redact(value) {
  if (Array.isArray(value)) return value.map(redact)
  if (!value || typeof value !== 'object') return value
  const output = {}
  for (const [key, item] of Object.entries(value)) {
    if (key === 'dshSessionId' || key === 'sessionId') continue
    if (key === 'hostSessionRef' && item && typeof item === 'object') {
      output[key] = { host: item.host }
      continue
    }
    output[key] = redact(item)
  }
  return output
}

export function createDshWorkspaceHost({ panel, resolveProductSession, mount, render, viewOptions, now, revision } = {}) {
  if (!panel || typeof panel !== 'object') throw new TypeError('panel is required')
  if (typeof resolveProductSession !== 'function') throw new TypeError('resolveProductSession is required')
  if (typeof mount !== 'function') throw new TypeError('mount is required')

  const capabilities = createDshWorkspaceCapabilities({ panel, resolveProductSession, now, revision })
  const queryCore = new WorkspaceQueryCore(capabilities)
  const app = createWorkspaceApp({
    queryCore,
    render,
    viewOptions,
    mount: ({ html, viewModel, state }) => mount({ html, viewModel: redact(viewModel), state: redact(state) }),
  })
  const controller = app.getController()
  const publicController = Object.freeze({
    ...Object.fromEntries(['loadProjects', 'selectProject', 'selectTaskInstance', 'loadSession', 'loadTimeline', 'loadTraces', 'loadEvidence', 'resolveDetail', 'refresh'].map((name) => [name, controller[name].bind(controller)])),
    subscribe: (listener) => controller.subscribe((state) => listener(redact(state))),
    getState: () => redact(controller.getState()),
  })
  const publicApp = Object.freeze({
    start: app.start,
    stop: app.stop,
    getController: () => publicController,
    getState: () => redact(app.getState()),
    getError: () => app.getError(),
  })

  return Object.freeze({
    start: app.start,
    stop: app.stop,
    getApp: () => publicApp,
    getController: () => publicController,
    getState: () => redact(app.getState()),
    getError: () => app.getError(),
  })
}
