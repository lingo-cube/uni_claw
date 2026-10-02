import test from 'node:test'
import assert from 'node:assert/strict'
import { createDshWorkspaceHost } from '../src/create-workspace-host.js'

const revision = 7
const clock = () => '2026-10-02T00:00:00.000Z'
function panel(overrides = {}) {
  return {
    workspace: async () => ({ success: true, projects: [{ projectId: 'p', instances: [{ instanceId: 'i', sessionId: 'dsh-1', productSessionId: 'product-1', title: 'Research' }, { instanceId: 'orphan', sessionId: 'dsh-orphan' }] }] }),
    session: async () => ({ success: true, task: { title: 'Research' }, instance: {}, metadata: { owner: 'uni-agent' }, conversation: [{ kind: 'request', summary: '用户请求' }, { kind: 'decision', summary: 'Agent 决策' }, { kind: 'result', summary: '执行结果' }], conversationGroups: [], dshTrace: [{ traceId: 'd', detailRef: { refId: 'trace/d' } }], uniclawTrace: [{ traceId: 'u' }], uniflowTrace: [{ traceId: 'f' }], evidence: [{ source: 'dsh', detailRef: { refId: 'evidence/e' } }] }),
    artifact: async ({ ref }) => ({ success: true, ref, text: 'detail' }),
    ...overrides,
  }
}
function make(overrides = {}) {
  const mounts = []
  const host = createDshWorkspaceHost({ panel: panel(overrides), resolveProductSession: id => id === 'product-1' ? { dshSessionId: 'dsh-1' } : null, mount: value => mounts.push(value), now: clock, revision })
  return { host, mounts }
}
async function run(host) {
  await host.start()
  const controller = host.getController()
  await controller.selectProject('p')
  await controller.selectTaskInstance('product-1')
  await Promise.all([controller.loadSession(), controller.loadTimeline(), controller.loadTraces(), controller.loadEvidence()])
  await controller.resolveDetail({ refId: 'evidence/e' })
  return host
}

test('DSH entry exposes the same App regions and complete product flow', async () => {
  const { host, mounts } = make()
  await run(host)
  const vm = mounts.at(-1).viewModel
  assert.deepEqual(Object.keys(vm).sort(), ['conversationTimeline', 'detailActions', 'evidencePane', 'metadataPane', 'navigation', 'notices', 'status', 'taskHeader', 'tracePane'])
  assert.deepEqual(Object.keys(vm.conversationTimeline.groups).sort(), ['decision', 'request', 'result'])
  assert.equal(vm.taskHeader.productSessionId, 'product-1')
  assert.equal(host.getState().detail.status, 'ready')
  assert.equal(host.getState().traces.revision, revision)
})

test('DSH preserves uncorrelated and permission or timeout facts', async () => {
  const { host } = make({ artifact: async () => ({ success: false, error: { code: 'permission-denied', message: 'denied' } }) })
  await host.start()
  await host.getController().selectProject('p')
  const tasks = host.getState().selection.taskInstances
  assert.equal(tasks.items.find(item => item.instanceId === 'orphan').correlationStatus, 'uncorrelated')
  await host.getController().selectTaskInstance('product-1')
  await host.getController().resolveDetail({ refId: 'evidence/e' })
  const detail = host.getState().detail
  assert.equal(detail.errors[0].code, 'permission-denied')
  assert.equal(detail.errors[0].source, 'dsh')
  assert.equal(detail.status, 'error')
})

test('DSH restart keeps ProductSessionId and redacts session identity from public output', async () => {
  const { host, mounts } = make()
  await run(host)
  host.stop()
  await host.start()
  assert.equal(host.getState().selection.productSessionId, 'product-1')
  const serialized = JSON.stringify({ state: host.getState(), viewModel: mounts.at(-1).viewModel })
  assert.equal(serialized.includes('dsh-1'), false)
  assert.equal(serialized.includes('sessionId'), false)
  assert.equal(serialized.includes('dshSessionId'), false)
})
