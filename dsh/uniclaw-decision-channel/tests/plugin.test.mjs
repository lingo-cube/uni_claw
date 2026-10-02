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
  plugin.apply(harness.ctx, {
    workspaceRoot: TEST_WORKSPACE_ROOT,
    workspaceTitle: 'UniClaw test project',
    workspaceKey: 'UniClaw shared test project',
    workspaceReuse: true,
    autoCloseTurn: false,
    sessionTitle: 'UniClaw test consultation',
    slowSessionTitle: 'UniClaw test slow consultation',
  })
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
      if (key === 'workspaceRegistry') return {
        async create(path, title) {
          controller.workspaceCreateCalls.push({ path, title })
          return { id: `workspace-${title}`, path, title }
        },
      }
      return undefined
    },
  }
  const controller = {
    createCalls: [],
    workspaceCreateCalls: [],
    renameCalls: [],
    promptCalls: [],
    cancels: [],
    agentScopedRegistrations: [],
    async create(request) {
      controller.createCalls.push(request)
      sessionSeq += 1
      return { sessionId: `${createdSession}-${sessionSeq}` }
    },
    async rename(request) { controller.renameCalls.push(request); return { title: request.title, seq: 1 } },
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

function emitAssistantText(harness, sessionId, text) {
  for (const handler of harness.events.get('session/event') ?? []) {
    handler({ id: sessionId }, {
      type: 'assistant/message',
      data: { message: { content: [{ type: 'text', text }] } },
    })
  }
}

test('apply registers five routes and the schema-derived tool', () => {
  const harness = mockCtx()
  applyHost(harness)
  for (const path of [
    '/api/uniclaw-agent/handshake',
    '/api/uniclaw-agent/consult',
    '/api/uniclaw-agent/slow',
    '/api/uniclaw-agent/abort',
    '/api/uniclaw-agent/detach',
  ]) {
    assert.ok(harness.routes.has(path), `missing ${path}`)
  }
  const tool = harness.ctx.tools.registered.find(t => t.name === 'submit_decision')
  assert.ok(tool)
  assert.deepEqual([...tool.parameters.properties.kind.enum].sort(), ['act', 'defer', 'noAction', 'policy'])
  assert.deepEqual(tool.parameters.required, ['kind', 'decisionId'])
  assert.deepEqual(tool.output.render({}, { accepted: true, reason: 'captured' }), [
    { type: 'text', text: 'submit_decision accepted: captured' },
  ])
})

test('Slow route returns assistant JSON text and accepts image content', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  const slowPromise = call(harness.routes, '/api/uniclaw-agent/slow', {
    requestId: 'slow-1',
    prompt: '{"status":"Succeeded","proposals":[]}',
    model: { provider: 'opencode-go', model: 'deepseek-v4-flash-vision-exp' },
    image: { mediaType: 'image/png', data: 'aGVsbG8=', name: 'capture.png' },
  })
  for (let i = 0; i < 50 && harness.controller.promptCalls.length === 0; i++)
    await new Promise(resolve => setTimeout(resolve, 5))
  assert.equal(harness.controller.promptCalls.length, 1)
  assert.equal(harness.controller.promptCalls[0].content[1].type, 'image')
  emitAssistantText(harness, harness.controller.promptCalls[0].sessionId,
    '{"status":"Succeeded","proposals":[]}')
  emitTurnEnd(harness, harness.controller.promptCalls[0].sessionId)
  const result = await slowPromise
  assert.equal(result.body.error, null)
  assert.match(result.body.text, /Succeeded/)
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
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
  assert.equal(harness.controller.createCalls[0].workspaceId, 'workspace-UniClaw test project')
  assert.deepEqual(harness.controller.renameCalls, [{
    sessionId: 'session-test-1-1', title: 'UniClaw test consultation',
  }])
  assert.deepEqual(result.body.runtimeCapabilities, ['submit_decision'])
  assert.equal(result.body.runtimePreset, 'uniagent-prod')
})

test('shared project and turn lifecycle follow explicit configuration', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
  await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody({
    productSessionId: 'product-shared', productRunId: 'run-shared',
  }))
  const tool = harness.ctx.tools.registered.find(t => t.name === 'submit_decision')
  const consultPromise = call(harness.routes, '/api/uniclaw-agent/consult', {
    requestId: 'req-shared-1', generation: 1,
    productSessionId: 'product-shared', productRunId: 'run-shared',
    dshSessionId: 'session-test-1-1',
    context: { decisionId: 'decision-shared-1', runId: 'run-shared', phase: 'InitialPlanning' },
  })
  for (let i = 0; i < 50 && harness.controller.promptCalls.length === 0; i++)
    await new Promise(resolve => setTimeout(resolve, 5))
  tool.execute({ kind: 'noAction', decisionId: 'decision-shared-1',
    proposal: { decisionId: 'decision-shared-1', justification: 'done' } })
  emitTurnEnd(harness, harness.controller.promptCalls[0].sessionId)
  await consultPromise
  assert.equal(harness.controller.cancels.length, 0)
  assert.match(harness.controller.workspaceCreateCalls[0].path, /UniClaw_shared_test_project$/)
})

test('configured project and title are applied to slow sessions', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  const slowPromise = call(harness.routes, '/api/uniclaw-agent/slow', {
    requestId: 'slow-title-1',
    prompt: '{"status":"Succeeded","proposals":[]}',
  })
  for (let i = 0; i < 50 && harness.controller.promptCalls.length === 0; i++)
    await new Promise(resolve => setTimeout(resolve, 5))
  assert.equal(harness.controller.promptCalls.length, 1)
  const slowSessionId = harness.controller.promptCalls[0].sessionId
  emitAssistantText(harness, slowSessionId, '{"status":"Succeeded","proposals":[]}')
  emitTurnEnd(harness, slowSessionId)
  await slowPromise
  assert.equal(harness.controller.createCalls.at(-1).workspaceId, 'workspace-UniClaw test project')
  assert.deepEqual(harness.controller.renameCalls.at(-1), {
    sessionId: slowSessionId, title: 'UniClaw test slow consultation',
  })
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

test('session mapping rejects a wrong DSH session or Product Run', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
  await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())

  const wrongSession = await call(harness.routes, '/api/uniclaw-agent/consult', {
    requestId: 'req-wrong-session',
    generation: 1,
    productSessionId: 'product-A',
    productRunId: 'run-A',
    dshSessionId: 'session-other',
    context: { decisionId: 'decision-wrong-session', runId: 'run-A', phase: 'InitialPlanning' },
  })
  assert.equal(wrongSession.body.error.code, 'dsh-session-mapping-mismatch')

  const wrongRun = await call(harness.routes, '/api/uniclaw-agent/consult', {
    requestId: 'req-wrong-run',
    generation: 1,
    productSessionId: 'product-A',
    productRunId: 'run-other',
    dshSessionId: 'session-test-1-1',
    context: { decisionId: 'decision-wrong-run', runId: 'run-other', phase: 'InitialPlanning' },
  })
  assert.equal(wrongRun.body.error.code, 'product-mapping-mismatch')
  assert.equal(harness.controller.promptCalls.length, 0)
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
    dshSessionId: 'session-test-1-1',
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
      dshSessionId: 'session-test-1-1',
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
  emitTurnEnd(harness, harness.controller.promptCalls[0].sessionId)
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
      dshSessionId: 'session-test-1-1',
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
  emitTurnEnd(harness, harness.controller.promptCalls[0].sessionId)
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
      dshSessionId: 'session-test-1-1',
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
  emitTurnEnd(harness, harness.controller.promptCalls[0].sessionId)
  const result = await consultPromise
  assert.equal(result.body.error ?? 'ok', 'ok', result.body.diagnostics?.message ?? '')
  const steps = result.body.decision.proposal.steps
  assert.ok(Array.isArray(steps) && steps.length === 1)
  assert.equal(steps[0].targetRole, 'switch')
  assert.equal(steps[0].effectClass, 'tap')
})

test('B3: act, policy, noAction and defer are accepted with traceable identities', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
  const handshake = await call(harness.routes, '/api/uniclaw-agent/handshake',
    handshakeBody({ productSessionId: 'product-four-kinds', productRunId: 'run-four-kinds' }))
  assert.equal(handshake.body.accepted, true)
  const cases = [
    {
      kind: 'act',
      proposal: {
        decisionId: 'decision-act-1',
        steps: [{ targetRole: 'switch', targetDescriptor: 'wifi', effectClass: 'tap', desiredState: 'on' }],
        justification: 'turn wifi on',
      },
    },
    {
      kind: 'policy',
      proposal: {
        policyId: 'policy-wifi-1',
        match: [{ kind: 'claimEquals', subject: 'wifi.state', value: 'off' }],
        actionTemplate: { targetRole: 'switch', targetDescriptor: 'wifi', effectClass: 'tap', desiredState: 'on' },
        termination: [{ kind: 'claimEquals', subject: 'wifi.state', value: 'on' }],
        guards: [{ kind: 'observationUnchanged', subject: 'wifi.state', afterRounds: 2 }],
        maxApplications: 2,
        justification: 'enable wifi until observed on',
      },
    },
    {
      kind: 'noAction',
      proposal: { decisionId: 'decision-no-action-1', justification: 'already on' },
    },
    {
      kind: 'defer',
      spec: { subject: 'wifi.state', maxRounds: 2 },
    },
  ]

  const tool = harness.ctx.tools.registered.find(t => t.name === 'submit_decision')
  for (let index = 0; index < cases.length; index++) {
    const entry = cases[index]
    const decisionId = entry.kind === 'policy' ? `decision-policy-${index + 1}` : entry.proposal?.decisionId ?? `decision-${entry.kind}-${index + 1}`
    const requestId = `req-four-kinds-${index + 1}`
    const consultPromise = call(harness.routes, '/api/uniclaw-agent/consult', {
      requestId,
      generation: index + 1,
      productSessionId: 'product-four-kinds',
      productRunId: 'run-four-kinds',
      dshSessionId: handshake.body.dshSessionId,
      context: { decisionId, runId: 'run-four-kinds', phase: 'InitialPlanning' },
    })
    for (let i = 0; i < 50 && harness.controller.promptCalls.length <= index; i++)
      await new Promise(resolve => setTimeout(resolve, 5))
    assert.equal(harness.controller.promptCalls.length, index + 1)
    const payload = { ...entry, decisionId }
    if (entry.kind === 'act' || entry.kind === 'noAction') payload.proposal = { ...entry.proposal, decisionId }
    const submitted = tool.execute(payload)
    assert.equal(submitted.accepted, true, submitted.reason)
    emitTurnEnd(harness, harness.controller.promptCalls[index].sessionId)
    const result = await consultPromise
    assert.equal(result.body.error ?? null, null, result.body.diagnostics?.message ?? '')
    assert.equal(result.body.requestId, requestId)
    assert.equal(result.body.generation, index + 1)
    assert.equal(result.body.decision.kind, entry.kind)
    assert.equal(result.body.decision.decisionId, decisionId)
    assert.equal(result.body.diagnostics.source, 'submit_decision')
    assert.equal(result.body.diagnostics.productRunId, 'run-four-kinds')
    assert.equal(result.body.diagnostics.decisionId, decisionId)
    assert.equal(result.body.diagnostics.schemaHash, SCHEMA_HASH)
    if (entry.kind === 'policy') assert.equal(result.body.diagnostics.policyId, 'policy-wifi-1')
  }
})

test('B3: bounded policy and defer fields reject invalid generated output', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
  const handshake = await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody({
    productSessionId: 'product-bounds', productRunId: 'run-bounds',
  }))
  const tool = harness.ctx.tools.registered.find(t => t.name === 'submit_decision')
  const invalid = [
    {
      kind: 'policy', decisionId: 'decision-policy-bad',
      proposal: {
        policyId: 'policy-bad', match: [{ kind: 'claimEquals', subject: 'wifi.state', value: 'off' }],
        actionTemplate: { targetRole: 'switch', effectClass: 'tap' },
        termination: [{ kind: 'claimEquals', subject: 'wifi.state', value: 'on' }],
        guards: [], maxApplications: 0,
      },
    },
    { kind: 'defer', decisionId: 'decision-defer-bad', spec: { subject: 'wifi.state', maxRounds: 0 } },
  ]
  for (let index = 0; index < invalid.length; index++) {
    const requestId = `req-bounds-${index + 1}`
    const consultPromise = call(harness.routes, '/api/uniclaw-agent/consult', {
      requestId, generation: index + 1, productSessionId: 'product-bounds', productRunId: 'run-bounds',
      dshSessionId: handshake.body.dshSessionId,
      context: { decisionId: invalid[index].decisionId, runId: 'run-bounds', phase: 'InitialPlanning' },
    })
    for (let i = 0; i < 50 && harness.controller.promptCalls.length <= index; i++)
      await new Promise(resolve => setTimeout(resolve, 5))
    const submitted = tool.execute(invalid[index])
    assert.equal(submitted.accepted, false)
    assert.equal(submitted.reason, 'decision-schema-invalid')
    emitTurnEnd(harness, harness.controller.promptCalls[index].sessionId)
    const result = await consultPromise
    assert.equal(result.body.error, 'decision-schema-invalid')
    assert.ok(result.body.diagnostics.message.length > 0)
  }
})

test('task session serializes consultations until the physical turn ends', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
  await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())

  const first = call(harness.routes, '/api/uniclaw-agent/consult', {
    requestId: 'req-serial-1',
    generation: 1,
    productSessionId: 'product-A',
    productRunId: 'run-A',
    dshSessionId: 'session-test-1-1',
    context: { decisionId: 'decision-serial-1', runId: 'run-A', phase: 'InitialPlanning' },
  })
  for (let i = 0; i < 50 && harness.controller.promptCalls.length === 0; i++)
    await new Promise(resolve => setTimeout(resolve, 5))
  const tool = harness.ctx.tools.registered.find(t => t.name === 'submit_decision')
  tool.execute({
    kind: 'noAction',
    decisionId: 'decision-serial-1',
    proposal: { decisionId: 'decision-serial-1', justification: 'wait for turn boundary' },
  })

  const second = await call(harness.routes, '/api/uniclaw-agent/consult', {
    requestId: 'req-serial-2',
    generation: 2,
    productSessionId: 'product-A',
    productRunId: 'run-A',
    dshSessionId: 'session-test-1-1',
    context: { decisionId: 'decision-serial-2', runId: 'run-A', phase: 'StepVerified' },
  })
  assert.equal(second.body.error.code, 'one-in-flight')
  assert.equal(harness.controller.promptCalls.length, 1)

  emitTurnEnd(harness, harness.controller.promptCalls[0].sessionId)
  const firstResult = await first
  assert.equal(firstResult.body.decision.kind, 'noAction')
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
      dshSessionId: 'session-test-1-1',
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

// ---------------------------------------------------------------------------
// PNL-001: event ledger + control panel projection.
// ---------------------------------------------------------------------------

test('PNL-001: ledger records attach, consult lifecycle and decision events', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
  const handshake = await call(harness.routes, '/api/uniclaw-agent/handshake',
    handshakeBody({ productSessionId: 'product-pnl', productRunId: 'run-pnl' }))
  assert.equal(handshake.body.accepted, true)

  const tool = harness.ctx.tools.registered.find(t => t.name === 'submit_decision')
  const consultPromise = call(harness.routes, '/api/uniclaw-agent/consult', {
    requestId: 'req-pnl-1',
    turnTimeoutMs: 100,
    generation: 1,
    productSessionId: 'product-pnl',
    productRunId: 'run-pnl',
    dshSessionId: handshake.body.dshSessionId,
    context: { decisionId: 'decision-pnl-1', runId: 'run-pnl', phase: 'InitialPlanning' },
  })
  for (let i = 0; i < 50 && harness.controller.promptCalls.length < 1; i++)
    await new Promise(resolve => setTimeout(resolve, 5))
  const submitted = tool.execute({
    kind: 'noAction', decisionId: 'decision-pnl-1',
    proposal: { decisionId: 'decision-pnl-1', justification: 'nothing to do' },
  })
  assert.equal(submitted.accepted, true, submitted.reason)
  emitTurnEnd(harness, harness.controller.promptCalls[0].sessionId)
  const result = await consultPromise
  assert.equal(result.body.error ?? null, null)

  const snap = plugin.panelSnapshot()
  assert.equal(snap.attached.productSessionId, 'product-pnl')
  const kinds = snap.events.map(e => e.kind)
  assert.ok(kinds.includes('attach'))
  assert.ok(kinds.includes('consult-start'))
  assert.ok(kinds.includes('decision-captured'))
  assert.ok(kinds.includes('consult-complete'))
  const complete = snap.events.findLast(e => e.kind === 'consult-complete')
  assert.equal(complete.decisionId, 'decision-pnl-1')
  assert.equal(complete.decisionKind, 'noAction')
  assert.equal(typeof complete.durationMs, 'number')
})

test('PNL-001: ledger records decision rejection and detach', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
  const handshake = await call(harness.routes, '/api/uniclaw-agent/handshake',
    handshakeBody({ productSessionId: 'product-pnl-bad', productRunId: 'run-pnl-bad' }))
  assert.equal(handshake.body.accepted, true)
  const tool = harness.ctx.tools.registered.find(t => t.name === 'submit_decision')
  const consultPromise = call(harness.routes, '/api/uniclaw-agent/consult', {
    requestId: 'req-pnl-bad',
    turnTimeoutMs: 100,
    generation: 1,
    productSessionId: 'product-pnl-bad',
    productRunId: 'run-pnl-bad',
    dshSessionId: handshake.body.dshSessionId,
    context: { decisionId: 'decision-pnl-expected', runId: 'run-pnl-bad', phase: 'InitialPlanning' },
  })
  for (let i = 0; i < 50 && harness.controller.promptCalls.length < 1; i++)
    await new Promise(resolve => setTimeout(resolve, 5))
  const wrong = tool.execute({ kind: 'noAction', decisionId: 'decision-WRONG', proposal: { decisionId: 'decision-WRONG', justification: 'x' } })
  assert.equal(wrong.accepted, false)
  assert.equal(plugin.panelSnapshot().events.some(e => e.kind === 'decision-rejected' && e.code === 'decision-id-mismatch'), true)
  const recovered = tool.execute({ kind: 'noAction', decisionId: 'decision-pnl-expected', proposal: { decisionId: 'decision-pnl-expected', justification: 'x' } })
  assert.equal(recovered.accepted, true, recovered.reason)
  emitTurnEnd(harness, harness.controller.promptCalls[0].sessionId)
  const badResult = await consultPromise
  assert.equal(badResult.body.error ?? null, null)
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
  const snap = plugin.panelSnapshot()
  assert.equal(snap.attached, null)
  assert.equal(snap.events.some(e => e.kind === 'detach' && e.hadAttachment === true), true)
  assert.equal(snap.inFlight.consult, false)
})
