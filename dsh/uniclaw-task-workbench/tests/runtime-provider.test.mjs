import { createServer } from 'node:http'
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

test('runtime provider works against a real local HTTP transport', async () => {
  const server = createServer((request, response) => {
    let body = ''
    request.on('data', chunk => { body += chunk })
    request.on('end', () => {
      assert.equal(request.method, 'POST')
      assert.equal(request.url, '/api/uniclaw-runtime/runs')
      const payload = JSON.parse(body)
      assert.equal(payload.idempotencyKey, 'idem-http-1')
      response.setHeader('content-type', 'application/json')
      response.end(JSON.stringify({ runId: 'run-http-1', productSessionId: 'product-http-1' }))
    })
  })
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve))
  try {
    const address = server.address()
    const provider = createRuntimeProvider({ baseUrl: `http://127.0.0.1:${address.port}` })
    const result = await provider.createRun({
      projectRef: { id: 'project/http' },
      testSetRef: { id: 'testset/http', version: 'default' },
      taskRef: { id: 'task/http' },
      correlationId: 'corr-http-1',
      idempotencyKey: 'idem-http-1',
      launchId: 'launch-http-1',
    })
    assert.deepEqual(result, { runId: 'run-http-1', productSessionId: 'product-http-1' })
  } finally {
    await new Promise((resolve, reject) => server.close(error => error ? reject(error) : resolve()))
  }
})
