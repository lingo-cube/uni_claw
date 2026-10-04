import test from 'node:test'
import assert from 'node:assert/strict'
import { createRuntimeProvider, runtimeProviderContract } from '../src/runtime-provider.js'

test('runtime provider posts the canonical launch payload and validates Runtime-owned IDs', async () => {
  const calls = []
  const provider = createRuntimeProvider({
    baseUrl: 'http://runtime.local/',
    fetchImpl: async (url, init) => {
      calls.push({ url, init })
      return { ok: true, async json() { return { runId: 'run-1', productSessionId: 'product-session-1' } } }
    },
  })
  const result = await provider.createRun({
    projectRef: { id: 'project/demo' },
    testSetRef: { id: 'testset/demo', version: 'default' },
    taskRef: { id: 'task/demo' },
    correlationId: 'corr-1',
    idempotencyKey: 'idem-1',
    launchId: 'launch-1',
  })
  assert.deepEqual(result, { runId: 'run-1', productSessionId: 'product-session-1' })
  assert.equal(calls.length, 1)
  assert.equal(calls[0].url, 'http://runtime.local/api/uniclaw-runtime/runs')
  assert.equal(calls[0].init.method, 'POST')
  assert.deepEqual(JSON.parse(calls[0].init.body), {
    schemaVersion: runtimeProviderContract.requestSchema,
    contractVersion: runtimeProviderContract.contractVersion,
    projectRef: { id: 'project/demo' },
    testSetRef: { id: 'testset/demo', version: 'default' },
    taskRef: { id: 'task/demo' },
    correlationId: 'corr-1',
    idempotencyKey: 'idem-1',
    launchId: 'launch-1',
  })
})

test('runtime provider rejects malformed responses instead of minting fallback IDs', async () => {
  const provider = createRuntimeProvider({
    baseUrl: 'http://runtime.local',
    fetchImpl: async () => ({ ok: true, async json() { return { runId: 'run-only' } } }),
  })
  await assert.rejects(provider.createRun({}), (error) => error.code === 'runtime-response-invalid')
})

test('runtime provider stays explicitly unavailable without a configured transport', async () => {
  const provider = createRuntimeProvider({ fetchImpl: async () => { throw new Error('must not call') } })
  await assert.rejects(provider.createRun({}), (error) => error.code === 'runtime-unavailable')
})

test('runtime recovery uses the same validated response seam', async () => {
  const calls = []
  const provider = createRuntimeProvider({
    baseUrl: 'http://runtime.local',
    fetchImpl: async (url, init) => {
      calls.push({ url, init })
      return { ok: true, async json() { return { run: { runId: 'run-recovered', productSessionId: 'product-recovered' } } } }
    },
  })
  const result = await provider.recoverRun({ launchId: 'launch-1', idempotencyKey: 'idem-1' })
  assert.deepEqual(result, { runId: 'run-recovered', productSessionId: 'product-recovered' })
  assert.equal(calls[0].url, 'http://runtime.local/api/uniclaw-runtime/runs/recover')
})
