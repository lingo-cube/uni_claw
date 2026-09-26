// AGT-002 review-round plugin contract tests (node:test, no DSH runtime).
// Drive apply() with a mock cordis ctx and exercise the registered routes
// directly: schema-hash gate (B2), capability gate (B1), no-prose-promotion
// and no-semantic-defaulting (B3), detach semantics (S1).

import test from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'

const PACKAGE_ROOT = dirname(dirname(fileURLToPath(import.meta.url)))
const plugin = await import(join(PACKAGE_ROOT, 'src', 'index.js'))

const SCHEMA_HASH = readFileSync(join(PACKAGE_ROOT, 'schema', 'schema-hash.txt'), 'utf8').trim()

/**
 * Writable root for the dedicated Product-session workspaces these tests make
 * the plugin create. The plugin defaults to `<DSH home>/uniagent-workspaces`;
 * a test must never write into the operator's real home.
 */
const TEST_WORKSPACE_ROOT = mkdtempSync(join(tmpdir(), 'uniclaw-ws-'))

/** Apply the plugin in HOST mode (routes + tool) against a fresh mock. */
function applyHost(harness) {
  // The session-scoped mount publishes the tool definition the host-mode
  // handshake registers into the agent scope (B1). In the real deployment both
  // rows are mounted; do the same here so the flow is exercised end to end.
  const sessionMount = mockCtx()
  plugin.apply(sessionMount.ctx, { sessionScoped: true })
  plugin.apply(harness.ctx, { workspaceRoot: TEST_WORKSPACE_ROOT })
}

function mockCtx({ visibleTools = ['submit_decision'], createdSession = 'session-test-1' } = {}) {
  const routes = new Map()
  const events = new Map()
  let sessionSeq = 0
  const ctx = {
    tools: {
      registered: [],
      guards: [],
      register(definition) { ctx.tools.registered.push(definition) },
      guard(guard) { ctx.tools.guards.push(guard) },
      schemas(agent) { return visibleTools.map(name => ({ name })) },
      restrict() { throw new Error('restrict is session-scoped only (preset)') },
    },
    connection: {
      fetch: {
        register(route) { routes.set(route.path, route) },
      },
    },
    on(event, handler) {
      if (!events.has(event)) events.set(event, [])
      events.get(event).push(handler)
    },
    get(key) {
      if (key === 'sessionController') return controller
      return undefined
    },
  }
  const controller = {
    createCalls: [],
    promptCalls: [],
    cancels: [],
    agentScopedRegistrations: [],
    async create(request) {
      controller.createCalls.push(request)
      sessionSeq += 1
      return { sessionId: `${createdSession}-${sessionSeq}` }
    },
    async prompt(request) { controller.promptCalls.push(request) },
    selectModel() {},
    cancel(request) { controller.cancels.push(request) },
    async resolveAgent(sessionId) {
      // The restricted session's Agent exposes its OWN scoped tools context.
      // That is where submit_decision is registered (B1): the preset scope
      // applies restrict({allow: []}) in its own layer, which would strip a
      // same-layer registration.
      return {
        agent: {
          id: sessionId,
          ctx: {
            tools: {
              register(definition) {
                controller.agentScopedRegistrations.push(definition)
                return () => {
                  const at = controller.agentScopedRegistrations.indexOf(definition)
                  if (at >= 0) controller.agentScopedRegistrations.splice(at, 1)
                }
              },
            },
          },
        },
      }
    },
  }
  return { ctx, routes, events, controller }
}

async function post(ctx, path, body) {
  const route = ctx ? undefined : undefined
  throw new Error('use harness.routes')
}

async function call(routes, path, body) {
  const route = routes.get(path)
  assert.ok(route, `route not registered: ${path}`)
  const request = new Request(`http://dsh.invalid${path}`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(body),
  })
  const response = await route.fetch(request)
  return { status: response.status, body: await response.json() }
}

function handshakeBody(overrides = {}) {
  const { protocol, ...rest } = overrides
  return {
    protocol: {
      protocolVersion: 'uniclaw.agent.protocol.v1',
      schemaVersion: 'uniclaw.agent.schema.v1',
      schemaHash: SCHEMA_HASH,
      profileId: 'uniagent-prod',
      profileVersion: '1',
      capabilityManifestHash: 'ba8855e41db09771081a7d217850bf99e263ddd84b30d5d076e4df245e1fd637',
      ...(protocol ?? {}),
    },
    reportedCapabilities: { capabilities: ['submit_decision'] },
    productSessionId: 'product-A',
    productRunId: 'run-A',
    ...rest,
  }
}

function emitTurnEnd(harness, sessionId) {
  for (const handler of harness.events.get('session/event') ?? []) {
    handler({ id: sessionId }, { type: 'turn/end', data: {} })
  }
}

test('apply registers four routes and the schema-derived tool', () => {
  const harness = mockCtx()
  applyHost(harness)
  for (const path of [
    '/api/uniclaw-agent/handshake',
    '/api/uniclaw-agent/consult',
    '/api/uniclaw-agent/abort',
    '/api/uniclaw-agent/detach',
  ]) {
    assert.ok(harness.routes.has(path), `missing ${path}`)
  }
  const tool = harness.ctx.tools.registered.find(t => t.name === 'submit_decision')
  assert.ok(tool)
  assert.deepEqual([...tool.parameters.kind.enum].sort(), ['act', 'defer', 'noAction', 'policy'])
})

test('B2: product schemaHash != DSH-local artifact → attach refused', async () => {
  const harness = mockCtx()
  applyHost(harness)
  const result = await call(harness.routes, '/api/uniclaw-agent/handshake',
    handshakeBody({ protocol: { schemaHash: 'deadbeef'.repeat(8) } }))
  assert.equal(result.body.ok, false)
  assert.match(result.body.error.message, /schema-hash-mismatch/)
  assert.equal(harness.controller.createCalls.length, 0)
})

test('B1: session tool catalog != manifest → attach refused', async () => {
  const harness = mockCtx({ visibleTools: ['submit_decision', 'shell'] })
  applyHost(harness)
  const result = await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  assert.equal(result.body.ok, false)
  assert.equal(result.body.error.code, 'session-capability-mismatch')
})

test('B1: session is created under the uniagent-prod preset', async () => {
  const harness = mockCtx()
  applyHost(harness)
  const result = await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  assert.equal(result.body.accepted, true)
  assert.equal(harness.controller.createCalls[0].agentPreset, 'uniagent-prod')
  assert.deepEqual(result.body.runtimeCapabilities, ['submit_decision'])
  assert.equal(result.body.runtimePreset, 'uniagent-prod')
})

test('B1: submit_decision is registered into the AGENT scope, not the host scope', async () => {
  const harness = mockCtx()
  applyHost(harness)
  // Tests share the plugin's module-level channel state; detach is the
  // realization-private reset (S1) and is idempotent.
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
  const result = await call(harness.routes, '/api/uniclaw-agent/handshake',
    handshakeBody({ productSessionId: 'product-agentscope', productRunId: 'run-agentscope' }))
  assert.equal(result.body.accepted, true, result.body.error?.message)
  // The preset scope applies restrict({allow: []}) in its own layer, which
  // would strip a same-layer registration; the agent's own layer is exempt.
  assert.deepEqual(
    harness.controller.agentScopedRegistrations.map(t => t.name),
    ['submit_decision'])
})

test('B1: detach releases the agent-scope registration', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
  await call(harness.routes, '/api/uniclaw-agent/handshake',
    handshakeBody({ productSessionId: 'product-release', productRunId: 'run-release' }))
  assert.equal(harness.controller.agentScopedRegistrations.length, 1)
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
  assert.equal(harness.controller.agentScopedRegistrations.length, 0)
})

test('B3: prose containing valid-looking JSON but no submit_decision → fail closed', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  const consultPromise = call(harness.routes, '/api/uniclaw-agent/consult', {
    requestId: 'req-prose-1',
    generation: 1,
    productSessionId: 'product-A',
    productRunId: 'run-A',
    turnTimeoutMs: 5000,
    context: { decisionId: 'decision-prose-1', runId: 'run-A', phase: 'InitialPlanning' },
  })
  // Wait for the prompt to be admitted, then the model answers in PROSE that
  // embeds a perfectly valid AgentDecision JSON — but never calls the tool.
  for (let i = 0; i < 50 && harness.controller.promptCalls.length === 0; i++) {
    await new Promise(resolve => setTimeout(resolve, 5))
  }
  assert.equal(harness.controller.promptCalls.length, 1)
  const sessionId = harness.controller.promptCalls[0].sessionId
  emitTurnEnd(harness, sessionId)
  const result = await consultPromise
  assert.equal(result.body.decision, null)
  assert.equal(result.body.error, 'no-submit-decision')
})

test('B3: missing Product-required fields → schema-invalid, never completed', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  let settleConsult
  const consultPromise = new Promise(resolve => { settleConsult = resolve })
  const consultRoute = harness.routes.get('/api/uniclaw-agent/consult')
  const request = new Request('http://dsh.invalid/api/uniclaw-agent/consult', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({
      requestId: 'req-missing-1',
      generation: 1,
      productSessionId: 'product-A',
      productRunId: 'run-A',
      turnTimeoutMs: 5000,
      context: { decisionId: 'decision-missing-1', runId: 'run-A', phase: 'InitialPlanning' },
    }),
  })
  consultRoute.fetch(request).then(async response => settleConsult({ status: response.status, body: await response.json() }))
  for (let i = 0; i < 50 && harness.controller.promptCalls.length === 0; i++) {
    await new Promise(resolve => setTimeout(resolve, 5))
  }
  assert.equal(harness.controller.promptCalls.length, 1)
  const tool = harness.ctx.tools.registered.find(t => t.name === 'submit_decision')
  // act without steps (targetRole/effectClass absent) — and NO semantic
  // completion may invent them.
  tool.execute({
    kind: 'act',
    decisionId: 'decision-missing-1',
    proposal: { justification: 'go' },
  })
  const result = await consultPromise
  assert.equal(result.body.decision, null)
  assert.equal(result.body.error, 'decision-schema-invalid')
  assert.match(result.body.diagnostics.message, /steps|required/)
})

test('B3: effect/target variant is NOT synthesized into steps (no semantic defaulting)', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  let settleConsult
  const consultPromise = new Promise(resolve => { settleConsult = resolve })
  const consultRoute = harness.routes.get('/api/uniclaw-agent/consult')
  const request = new Request('http://dsh.invalid/api/uniclaw-agent/consult', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({
      requestId: 'req-semvar-1',
      generation: 1,
      productSessionId: 'product-A',
      productRunId: 'run-A',
      turnTimeoutMs: 5000,
      context: { decisionId: 'decision-semvar-1', runId: 'run-A', phase: 'InitialPlanning' },
    }),
  })
  consultRoute.fetch(request).then(async response => settleConsult({ status: response.status, body: await response.json() }))
  for (let i = 0; i < 50 && harness.controller.promptCalls.length === 0; i++) {
    await new Promise(resolve => setTimeout(resolve, 5))
  }
  assert.equal(harness.controller.promptCalls.length, 1)
  const tool = harness.ctx.tools.registered.find(t => t.name === 'submit_decision')
  tool.execute({
    kind: 'act',
    decisionId: 'decision-semvar-1',
    proposal: { effect: 'tap', target: { role: 'toggle' } },
  })
  const result = await consultPromise
  assert.equal(result.body.decision, null)
  assert.equal(result.body.error, 'decision-schema-invalid')
})

test('B3: representation normalization still accepts {item} wrapping and JSON strings', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  let settleConsult
  const consultPromise = new Promise(resolve => { settleConsult = resolve })
  const consultRoute = harness.routes.get('/api/uniclaw-agent/consult')
  const request = new Request('http://dsh.invalid/api/uniclaw-agent/consult', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({
      requestId: 'req-repr-1',
      generation: 1,
      productSessionId: 'product-A',
      productRunId: 'run-A',
      turnTimeoutMs: 5000,
      context: { decisionId: 'decision-repr-1', runId: 'run-A', phase: 'InitialPlanning' },
    }),
  })
  consultRoute.fetch(request).then(async response => settleConsult({ status: response.status, body: await response.json() }))
  for (let i = 0; i < 50 && harness.controller.promptCalls.length === 0; i++) {
    await new Promise(resolve => setTimeout(resolve, 5))
  }
  assert.equal(harness.controller.promptCalls.length, 1)
  const tool = harness.ctx.tools.registered.find(t => t.name === 'submit_decision')
  tool.execute({
    kind: 'act',
    decisionId: 'decision-repr-1',
    proposal: JSON.stringify({ decisionId: 'decision-repr-1', steps: { item: { targetRole: 'switch', effectClass: 'tap', desiredState: 'on' } } }),
  })
  const result = await consultPromise
  assert.equal(result.body.error ?? 'ok', 'ok', result.body.diagnostics?.message ?? '')
  const steps = result.body.decision.proposal.steps
  assert.ok(Array.isArray(steps) && steps.length === 1)
  assert.equal(steps[0].targetRole, 'switch')
  assert.equal(steps[0].effectClass, 'tap')
})

test('S1: detach releases the mapping; attach B then succeeds without restart', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  const detach = await call(harness.routes, '/api/uniclaw-agent/detach', {})
  assert.equal(detach.body.detached, true)

  const second = await call(harness.routes, '/api/uniclaw-agent/handshake',
    handshakeBody({ productSessionId: 'product-B', productRunId: 'run-B' }))
  assert.equal(second.body.accepted, true, second.body.error?.message)
  assert.notEqual(second.body.dshSessionId, '')
})

test('S1: detach during in-flight turn drops the late decision', async () => {
  const harness = mockCtx()
  applyHost(harness)
  // Tests share the plugin's module-level channel state; detach is the
  // realization-private reset (S1) and is idempotent.
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
  const handshake = await call(harness.routes, '/api/uniclaw-agent/handshake',
    handshakeBody({ productSessionId: 'product-detach', productRunId: 'run-detach' }))
  assert.equal(handshake.body.accepted, true, handshake.body.error?.message)
  let settleConsult
  const consultPromise = new Promise(resolve => { settleConsult = resolve })
  const consultRoute = harness.routes.get('/api/uniclaw-agent/consult')
  const request = new Request('http://dsh.invalid/api/uniclaw-agent/consult', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({
      requestId: 'req-detach-1',
      generation: 1,
      productSessionId: 'product-detach',
      productRunId: 'run-detach',
      turnTimeoutMs: 10000,
      context: { decisionId: 'decision-detach-1', runId: 'run-detach', phase: 'InitialPlanning' },
    }),
  })
  consultRoute.fetch(request).then(
    async response => settleConsult({ status: response.status, body: await response.json() }),
    error => settleConsult({ status: 0, body: { transportError: String(error) } }))
  for (let i = 0; i < 50 && harness.controller.promptCalls.length === 0; i++) {
    await new Promise(resolve => setTimeout(resolve, 5))
  }
  assert.equal(harness.controller.promptCalls.length, 1)

  await call(harness.routes, '/api/uniclaw-agent/detach', {})
  const result = await consultPromise
  assert.equal(result.body.decision, null)
  assert.equal(result.body.error, 'turn-aborted')

  // The late model response arrives after the mapping was released.
  const tool = harness.ctx.tools.registered.find(t => t.name === 'submit_decision')
  const late = tool.execute({ kind: 'noAction', decisionId: 'decision-detach-1', proposal: { justification: 'late' } })
  assert.equal(late.accepted, false)
  assert.equal(late.reason, 'no-pending-consultation')
})
