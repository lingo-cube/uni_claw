import test from 'node:test'
import assert from 'node:assert/strict'
import { createCapabilities } from '../../../web/uniclaw-workspace/src/browser/entry.js'

test('browser bridge adapts ProductSessionId and keeps strict RPC boundary', async () => {
  const calls = []
  const caps = createCapabilities({
    workspace: (...args) => { calls.push(['workspace', ...args]); return { ok: true, data: { projects: [] } } },
    session: (...args) => { calls.push(['session', ...args]); return { ok: true, data: { session: { productSessionId: 'ps-1' }, conversation: [], dshTrace: [], uniclawTrace: [], uniflowTrace: [], evidence: [] } } },
    artifact: (...args) => { calls.push(['artifact', ...args]); return { ok: true, data: { text: 'detail' } } },
  })
  const session = await caps.SessionQuery.getSession({ productSessionId: 'ps-1' })
  await caps.DetailQuery.resolveDetail({ productSessionId: 'ps-1', detailRef: { refId: 'ev-1' } })
  assert.equal(session.data.productSessionId, 'ps-1')
  assert.deepEqual(calls, [['session', 'ps-1'], ['artifact', 'ps-1', 'ev-1']])
})

test('malformed remote response becomes structured failure', async () => {
  const caps = createCapabilities({ workspace: () => null, session: () => null, artifact: () => null })
  const result = await caps.TaskQuery.listProjects()
  assert.equal(result.ok, false)
  assert.equal(result.error.code, 'unavailable')
})
