import test from 'node:test'
import assert from 'node:assert/strict'
import { createDshWorkspaceHost } from '../src/create-workspace-host.js'

const clock = () => '2026-10-02T00:00:00.000Z'
function makePanel(overrides = {}) {
  return {
    workspace: async () => ({ success: true, projects: [{ projectId: 'p', instances: [{ instanceId: 'i', sessionId: 'dsh-1', productSessionId: 'product-1' }] }] }),
    session: async () => ({ success: true, task: { title: 'Research' }, instance: {}, metadata: {}, conversation: [{ kind: 'assistant', text: 'done' }], conversationGroups: [], dshTrace: [{ seq: 1 }], uniclawTrace: [{ token: 'u' }], uniflowTrace: [{ token: 'f' }], evidence: [{ source: 'dsh', detailRef: { refId: 'evidence/a.md' } }] }),
    artifact: async ({ ref }) => ({ success: true, ref, text: 'detail' }),
    ...overrides,
  }
}

function make(overrides = {}) {
  const mounts = []
  const host = createDshWorkspaceHost({
    panel: makePanel(overrides),
    resolveProductSession: (id) => id === 'product-1' ? { dshSessionId: 'dsh-1' } : null,
    mount: (value) => mounts.push(value),
    render: (vm) => JSON.stringify(vm),
    now: clock,
    revision: 2,
  })
  return { host, mounts }
}

test('assembles the full workspace flow and redacts host session identity', async () => {
  const { host, mounts } = make()
  await host.start()
  const controller = host.getController()
  await controller.selectProject('p')
  await controller.selectTaskInstance('product-1')
  await Promise.all([controller.loadSession(), controller.loadTimeline(), controller.loadTraces(), controller.loadEvidence()])
  await controller.resolveDetail({ source: 'dsh', refId: 'evidence/a.md' })
  const serialized = JSON.stringify(mounts.at(-1))
  assert.equal(serialized.includes('dsh-1'), false)
  assert.equal(host.getState().session.session.productSessionId, 'product-1')
  assert.equal(host.getState().session.session.hostSessionRef.sessionId, undefined)
})

test('missing mapping stays uncorrelated', async () => {
  const { host } = make()
  await host.start()
  await host.getController().selectTaskInstance('missing-product')
  const state = await host.getController().loadSession()
  assert.equal(state.session.errors[0].code, 'uncorrelated')
})

test('panel, renderer, and mount errors remain observable', async () => {
  const { host } = make({ workspace: async () => ({ success: false, error: { code: 'permission-denied', message: 'no access' } }) })
  await host.start()
  assert.equal(host.getState().projects.errors[0].code, 'permission-denied')
  const renderError = new Error('render failed')
  const rendered = createDshWorkspaceHost({ panel: makePanel(), resolveProductSession: () => null, render: () => { throw renderError }, mount: () => {} })
  await assert.rejects(rendered.start(), renderError)
  assert.strictEqual(rendered.getError(), renderError)
})

test('stop then start reuses the same app contract', async () => {
  const { host, mounts } = make()
  await host.start()
  const app = host.getApp()
  host.stop()
  await host.start()
  assert.strictEqual(host.getApp(), app)
  assert.ok(mounts.length > 1)
})
