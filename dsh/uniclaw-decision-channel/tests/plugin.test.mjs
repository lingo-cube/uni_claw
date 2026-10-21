// AGT-002 review-round plugin contract tests (node:test, no DSH runtime).
// Drive apply() with a mock cordis ctx and exercise the registered routes
// directly: schema-hash gate (B2), capability gate (B1), no-prose-promotion
// and no-semantic-defaulting (B3), detach semantics (S1).

import test from 'node:test'


import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { cpSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'

const PACKAGE_ROOT = dirname(dirname(fileURLToPath(import.meta.url)))
const plugin = await import(join(PACKAGE_ROOT, 'src', 'index.js'))

const SCHEMA_HASH = readFileSync(join(PACKAGE_ROOT, 'schema', 'schema-hash.txt'), 'utf8').trim()
const SKILL_DIR = join(PACKAGE_ROOT, 'skill', 'android-automotive-ui-testing')
const CANONICAL_SKILL_DIR = join(PACKAGE_ROOT, '..', '..', 'product', 'skills', 'android-automotive-ui-testing')
const EXPECTED_ARTIFACTS = {
  promptHash: readFileSync(join(PACKAGE_ROOT, 'prompt', 'prompt-hash.txt'), 'utf8').trim(),
  skill: {
    name: 'android-automotive-ui-testing', revision: 4,
    sha256: readFileSync(join(SKILL_DIR, 'skill-hash.txt'), 'utf8').trim(),
  },
}

/** Minimal schema artifact shape consumed by the prompt loader. */
function schemaArtifactFixture() {
  return { schemaHash: SCHEMA_HASH }
}

/**
 * Writable root for the dedicated Product-session workspaces these tests make
 * the plugin create. The plugin defaults to `<DSH home>/uniagent-workspaces`;
 * a test must never write into the operator's real home.
 */
const TEST_WORKSPACE_ROOT = mkdtempSync(join(tmpdir(), 'uniclaw-ws-'))

function submitTask(tool, payload, capabilitySelection) {
  return tool.execute({
    task: {
      initialization: capabilitySelection === undefined ? {} : { capabilitySelection },
      payload,
    },
  })
}

/** Apply the plugin in HOST mode (routes + tool) against a fresh mock.
 * extraConfig merges into the host-row config (e.g. promptMount for tests
 * that assert the full per-turn prompt text). */
let activeHarness = null
test.afterEach(async () => {
  if (activeHarness !== null) {
    await call(activeHarness.routes, '/api/uniclaw-agent/detach', {})
    activeHarness = null
  }
})

function applyHost(harness, extraConfig = {}, pluginImplementation = plugin) {
  activeHarness = harness
  // The session-scoped mount publishes the tool definition the host-mode
  // handshake registers into the agent scope (B1). In the real deployment both
  // rows are mounted; do the same here so the flow is exercised end to end.
  const sessionMount = mockCtx()
  pluginImplementation.apply(sessionMount.ctx, { sessionScoped: true })
  harness.controller.inheritedPromptSections = sessionMount.controller.promptSections
  pluginImplementation.apply(harness.ctx, {
    workspaceRoot: TEST_WORKSPACE_ROOT,
    workspaceTitle: 'UniClaw test project',
    workspaceKey: 'UniClaw shared test project',
    workspaceReuse: true,
    autoCloseTurn: false,
    sessionTitle: 'UniClaw test consultation',
    slowSessionTitle: 'UniClaw test slow consultation',
    ...extraConfig,
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
    inject(names, callback) {
      // cordis inject: services resolve as ctx properties; in tests the
      // service is already available via ctx.get — surface it and invoke now.
      for (const name of names) ctx[name] = ctx.get(name)
      callback(ctx)
      return () => {}
    },
    on(event, handler) {
      if (!events.has(event)) events.set(event, [])
      events.get(event).push(handler)
    },
    get(key) {
      if (key === 'sessionController') return controller
      // PRF-003: systemPrompt service mock — captures scoped-section
      // registrations (the mount used by the preset row).
      if (key === 'systemPrompt') return {
        section(record) { controller.promptSections.push(record); return record },
        getSectionOrder() { return 0 },
        async assemble({ scope } = {}) {
          return {
            // One empty section exercises the render-drop filter.
            sections: [
              ...(scope ? [...controller.inheritedPromptSections, ...controller.promptSections] : [])
                .map(record => ({ name: record.name, text: record.text })),
              { name: 'deployment:persona-prefix', text: '   ' },
            ],
            contexts: [{ name: 'probe-context' }],
            tools: [{ name: 'submit_decision' }],
            variables: { probeVar: 'x' },
          }
        },
      }
      if (key === 'workspaceRegistry') return {
        async archiveSession(sessionId, options) {
          controller.archivedSessions.push({ sessionId, options });
        },
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
    archivedSessions: [],
    agentScopedRegistrations: [],
    promptSections: [],
    inheritedPromptSections: [],
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
    expectedArtifacts: structuredClone(EXPECTED_ARTIFACTS),
    productSessionId: 'product-A',
    productRunId: 'run-A',
    ...rest,
  }
}

function emitTurnEnd(harness, sessionId, reason) {
  for (const handler of harness.events.get('session/event') ?? []) {
    handler({ id: sessionId }, { type: 'turn/end', data: reason === undefined ? {} : { reason } })
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
  assert.deepEqual([...tool.parameters.properties.task.properties.payload.properties.kind.enum].sort(), ['act', 'blocked', 'continuePlan', 'defer', 'noAction', 'plan', 'policy', 'skip'])
  assert.deepEqual(tool.parameters.required, ['task'])
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
  assert.deepEqual(harness.controller.archivedSessions, [{
    sessionId: harness.controller.promptCalls[0].sessionId, options: { stopActivity: true },
  }])
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
})

for (const outcome of ['no-output', 'timeout', 'prompt-failure', 'abort', 'detach']) {
  test(`Slow ${outcome} archives its isolated session and ignores late output`, async () => {
    const harness = mockCtx()
    applyHost(harness)
    await call(harness.routes, '/api/uniclaw-agent/detach', {})
    await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
    if (outcome === 'prompt-failure') harness.controller.prompt = async request => {
      harness.controller.promptCalls.push(request)
      throw new Error('transport rejected')
    }
    const pending = call(harness.routes, '/api/uniclaw-agent/slow', {
      requestId: `slow-${outcome}`, prompt: 'observe current screen', turnTimeoutMs: 100,
    })
    for (let i = 0; i < 50 && harness.controller.promptCalls.length === 0; i++)
      await new Promise(resolve => setTimeout(resolve, 1))
    const sessionId = harness.controller.promptCalls[0].sessionId
    if (outcome === 'no-output') emitTurnEnd(harness, sessionId)
    if (outcome === 'abort' || outcome === 'detach')
      await call(harness.routes, `/api/uniclaw-agent/${outcome}`, {})
    const result = await pending
    assert.equal(result.body.error?.code ?? result.body.error, {
      'no-output': 'no-slow-result', 'timeout': 'turn-timeout',
      'prompt-failure': 'prompt-failed', 'abort': 'turn-aborted', 'detach': 'turn-aborted',
    }[outcome])
    assert.deepEqual(harness.controller.archivedSessions, [{ sessionId, options: { stopActivity: true } }])
    emitAssistantText(harness, sessionId, '{"status":"Succeeded","proposals":[{"subject":"late","value":"ignored"}]}')
    emitTurnEnd(harness, sessionId)
    await call(harness.routes, '/api/uniclaw-agent/detach', {})
  })
}

test('Section mode rejects a session whose actual scope is missing the static instructions', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
  harness.controller.inheritedPromptSections = []
  const result = await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  assert.equal(result.body.error.code, 'session-prompt-verification-failed')
})

test('Rejected misplaced renewal exposes the offending field and a corrected proposal is captured intact', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
  const handshake = await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  const consultation = call(harness.routes, '/api/uniclaw-agent/consult', {
    requestId: 'renewal-correction', generation: 1,
    productSessionId: 'product-A', productRunId: 'run-A', dshSessionId: handshake.body.dshSessionId,
    context: { decisionId: 'renewal-correction-1', runId: 'run-A', phase: 'StepVerified' },
  })
  try {
    for (let i = 0; i < 50 && harness.controller.promptCalls.length === 0; i++)
      await new Promise(resolve => setTimeout(resolve, 1))
    const tool = harness.ctx.tools.registered[0]
    const renewal = { additionalSteps: 20, additionalRounds: 24, justification: 'verified progress' }
    const payload = { kind: 'act', decisionId: 'renewal-correction-1',
      proposal: { decisionId: 'renewal-correction-1', steps: [
        { targetRole: 'ui.element', targetDescriptor: 'Navigate up', effectClass: 'tap' },
      ] }, renewal }
    const rejected = submitTask(tool, payload)
    assert.equal(rejected.accepted, false)
    assert.equal(rejected.reason, 'decision-schema-invalid')
    const feedback = tool.output.render({}, rejected)[0].text
    assert.match(feedback, /renewal.*(unknown|additional|allowed)|(?:unknown|additional).*renewal/i)
    const corrected = { ...payload, proposal: { ...payload.proposal, renewal } }
    delete corrected.renewal
    assert.equal(submitTask(tool, corrected).accepted, true)
    emitTurnEnd(harness, harness.controller.promptCalls[0].sessionId)
    const captured = await consultation
    assert.deepEqual(captured.body.task.payload.proposal.renewal, renewal)
    assert.equal(captured.body.task.payload.renewal, undefined)
  } finally {
    await call(harness.routes, '/api/uniclaw-agent/detach', {})
    await consultation
  }
})

test('Explicit budget review is validated and captured at the decision union top level', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
  const handshake = await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  const consultation = call(harness.routes, '/api/uniclaw-agent/consult', {
    requestId: 'review-response', generation: 1,
    productSessionId: 'product-A', productRunId: 'run-A', dshSessionId: handshake.body.dshSessionId,
    context: { decisionId: 'review-response-1', runId: 'run-A', phase: 'StepVerified',
      budgetReview: { reviewId: 'review-1', required: true },
      executionOverview: { overviewId: 'overview-1', objective: 'inspect settings' } },
  })
  try {
    for (let i = 0; i < 50 && harness.controller.promptCalls.length === 0; i++)
      await new Promise(resolve => setTimeout(resolve, 1))
    const tool = harness.ctx.tools.registered[0]
    const response = { reviewId: 'review-1', outcome: 'RequestRenewal',
      assessment: { overviewId: 'overview-1', judgment: 'advancing',
        nextDirection: 'inspect the known menu', nextSegmentExpectation: 'capture visible state',
        expectedObservableEvidence: ['visible state'], evidenceRefs: ['obs:1'], nextItemKey: 'root:observed-menu' },
      renewal: { additionalSteps: 20, additionalRounds: 0, justification: 'known branch remains' } }
    assert.match(harness.controller.promptCalls[0].content[0].text, /overview-1/)
    assert.match(harness.controller.promptCalls[0].content[0].text, /review-1/)
    assert.match(harness.controller.promptCalls[0].content[0].text, /本轮必须明确复核额度/)
    const invalid = submitTask(tool, { kind: 'act', decisionId: 'review-response-1',
      proposal: { decisionId: 'review-response-1', steps: [{ targetRole: 'button', effectClass: 'tap' }] },
      reviewResponse: { ...response, outcome: 'Maybe' } })
    assert.equal(invalid.accepted, false)
    assert.equal(invalid.reason, 'decision-schema-invalid')
    const { assessment, ...missingAssessment } = response
    assert.equal(submitTask(tool, { kind: 'act', decisionId: 'review-response-1',
      proposal: { decisionId: 'review-response-1', steps: [{ targetRole: 'button', effectClass: 'tap' }] },
      reviewResponse: missingAssessment }).accepted, false)
    const { renewal, ...missingRenewal } = response
    assert.equal(submitTask(tool, { kind: 'act', decisionId: 'review-response-1',
      proposal: { decisionId: 'review-response-1', steps: [{ targetRole: 'button', effectClass: 'tap' }] },
      reviewResponse: missingRenewal }).accepted, false)
    assert.equal(submitTask(tool, { kind: 'act', decisionId: 'review-response-1',
      proposal: { decisionId: 'review-response-1', steps: [{ targetRole: 'button', effectClass: 'tap' }],
        renewal: response.renewal }, reviewResponse: response }).accepted, false)
    const valid = { kind: 'act', decisionId: 'review-response-1',
      proposal: { decisionId: 'review-response-1', steps: [{ targetRole: 'button', effectClass: 'tap' }] },
      reviewResponse: response }
    assert.equal(submitTask(tool, valid).accepted, true)
    emitTurnEnd(harness, harness.controller.promptCalls[0].sessionId)
    const captured = await consultation
    assert.deepEqual(captured.body.task.payload.reviewResponse, response)
    assert.equal(captured.body.task.payload.proposal.reviewResponse, undefined)
  } finally {
    await call(harness.routes, '/api/uniclaw-agent/detach', {})
    await consultation
  }
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

test('RUNTIME-PLAN-004: missing or malformed artifact pins fail before session creation', async () => {
  for (const expectedArtifacts of [undefined, {}, { ...EXPECTED_ARTIFACTS, promptHash: 'bad' }]) {
    const harness = mockCtx()
    applyHost(harness)
    const body = handshakeBody()
    if (expectedArtifacts === undefined) delete body.expectedArtifacts
    else body.expectedArtifacts = expectedArtifacts
    const result = await call(harness.routes, '/api/uniclaw-agent/handshake', body)
    assert.equal(result.body.ok, false)
    assert.match(result.body.error.message, /artifact-pins-missing-or-malformed/)
    assert.equal(harness.controller.createCalls.length, 0)
    await call(harness.routes, '/api/uniclaw-agent/detach', {})
  }
})

test('RUNTIME-PLAN-004: prompt and skill pins reject drift before session creation', async () => {
  for (const expectedArtifacts of [
    { ...EXPECTED_ARTIFACTS, promptHash: '0'.repeat(64) },
    { ...EXPECTED_ARTIFACTS, skill: { ...EXPECTED_ARTIFACTS.skill, revision: 3 } },
    { ...EXPECTED_ARTIFACTS, skill: { ...EXPECTED_ARTIFACTS.skill, sha256: '0'.repeat(64) } },
  ]) {
    const harness = mockCtx()
    applyHost(harness)
    const result = await call(harness.routes, '/api/uniclaw-agent/handshake',
      handshakeBody({ expectedArtifacts }))
    assert.equal(result.body.ok, false)
    assert.match(result.body.error.message, /artifact-pin-mismatch/)
    assert.equal(harness.controller.createCalls.length, 0)
    assert.equal(harness.controller.promptCalls.length, 0)
    await call(harness.routes, '/api/uniclaw-agent/detach', {})
  }
})

test('RUNTIME-PLAN-004: same-revision Skill content and its package hash can drift together but Profile pin rejects it', async () => {
  const temporaryRoot = mkdtempSync(join(tmpdir(), 'uniclaw-skill-drift-'))
  const packageCopy = join(temporaryRoot, 'package')
  mkdirSync(packageCopy)
  for (const directory of ['src', 'schema', 'prompt', 'skill'])
    cpSync(join(PACKAGE_ROOT, directory), join(packageCopy, directory), { recursive: true })
  const skillDirectory = join(packageCopy, 'skill', 'android-automotive-ui-testing')
  const manifestBytes = readFileSync(join(skillDirectory, 'manifest.yaml'))
  const guidancePath = join(skillDirectory, 'guidance.md')
  const changedGuidance = Buffer.concat([readFileSync(guidancePath), Buffer.from('\nTampered boot artifact.\n')])
  writeFileSync(guidancePath, changedGuidance)
  const changedHash = createHash('sha256').update(Buffer.concat([manifestBytes, changedGuidance])).digest('hex')
  writeFileSync(join(skillDirectory, 'skill-hash.txt'), `${changedHash}\n`)
  const driftedPlugin = await import(join(packageCopy, 'src', 'index.js'))
  const harness = mockCtx()
  applyHost(harness, {
    skillArtifact: {
      path: 'skill/android-automotive-ui-testing', name: EXPECTED_ARTIFACTS.skill.name,
      revision: EXPECTED_ARTIFACTS.skill.revision, sha256: changedHash,
    },
  }, driftedPlugin)

  const result = await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())

  assert.equal(result.body.ok, false)
  assert.match(result.body.error.message, /skill-artifact-pin-mismatch/)
  assert.equal(harness.controller.createCalls.length, 0)
  assert.equal(harness.controller.promptCalls.length, 0)
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
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
  assert.deepEqual(result.body.artifacts, EXPECTED_ARTIFACTS)
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
  submitTask(tool, { kind: 'noAction', decisionId: 'decision-shared-1',
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

test('retry after no-submit keeps the original consultation decisionId', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
  const handshake = await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  const consultPromise = call(harness.routes, '/api/uniclaw-agent/consult', {
    requestId: 'req-retry-context-1',
    generation: 1,
    productSessionId: 'product-A',
    productRunId: 'run-A',
    dshSessionId: handshake.body.dshSessionId,
    turnTimeoutMs: 5000,
    context: { decisionId: 'decision-retry-context-1', runId: 'run-A', phase: 'InitialPlanning' },
  })
  let consultationSettled = false
  consultPromise.then(() => { consultationSettled = true })
  try {
    for (let i = 0; i < 50 && harness.controller.promptCalls.length < 1; i++)
      await new Promise(resolve => setTimeout(resolve, 5))
    assert.equal(harness.controller.promptCalls.length, 1)
    emitTurnEnd(harness, handshake.body.dshSessionId)

    for (let i = 0; i < 50 && harness.controller.promptCalls.length < 2; i++)
      await new Promise(resolve => setTimeout(resolve, 5))
    assert.equal(harness.controller.promptCalls.length, 2, 'the no-submit turn should start one retry')
    assert.equal(harness.controller.promptCalls[1].content[0].text,
      'You did not call submit_decision. You MUST call the submit_decision tool NOW to submit your decision for this consultation. Do not respond with text only.')

    const tool = harness.ctx.tools.registered.find(t => t.name === 'submit_decision')
    const decisionId = 'decision-retry-context-1'
    const submitted = submitTask(tool, {
      kind: 'noAction', decisionId,
      proposal: { decisionId, justification: 'retry uses the same consultation context' },
    })
    assert.equal(submitted.accepted, true, submitted.message ?? submitted.reason)
    await new Promise(resolve => setTimeout(resolve, 10))
    assert.equal(consultationSettled, false, 'HTTP consultation must wait for the retry physical turn/end')
    emitTurnEnd(harness, handshake.body.dshSessionId)
    const result = await consultPromise
    assert.equal(result.body.error ?? null, null)
    assert.equal(result.body.task.payload.decisionId, decisionId)
    assert.equal(result.body.diagnostics.source, 'submit_decision_retry')
  } finally {
    emitTurnEnd(harness, handshake.body.dshSessionId)
    await call(harness.routes, '/api/uniclaw-agent/detach', {})
    await consultPromise
  }
})

test('turn/end provider parse failure is included in the single nudge and the original decision can be captured', async () => {
  const harness = mockCtx()
  applyHost(harness)
  const handshake = await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  const decisionId = 'decision-turn-error-retry'
  const consultPromise = call(harness.routes, '/api/uniclaw-agent/consult', {
    requestId: 'req-turn-error-retry', generation: 1,
    productSessionId: 'product-A', productRunId: 'run-A', dshSessionId: handshake.body.dshSessionId,
    turnTimeoutMs: 5000,
    context: { decisionId, runId: 'run-A', phase: 'InitialPlanning', allowedEffects: ['tap'] },
  })
  try {
    for (let i = 0; i < 50 && harness.controller.promptCalls.length < 1; i++)
      await new Promise(resolve => setTimeout(resolve, 5))
    assert.equal(harness.controller.promptCalls.length, 1)
    emitTurnEnd(harness, 'unrelated-session', {
      kind: 'error', error: { code: 'WRONG_SESSION', message: 'must not settle this consultation' },
    })
    assert.equal(harness.controller.promptCalls.length, 1)
    emitTurnEnd(harness, handshake.body.dshSessionId, {
      kind: 'error',
      error: { code: 'MALFORMED_RESPONSE', message: 'DeepSeek Messages stream: tool input is invalid JSON' },
    })

    for (let i = 0; i < 50 && harness.controller.promptCalls.length < 2; i++)
      await new Promise(resolve => setTimeout(resolve, 5))
    assert.equal(harness.controller.promptCalls.length, 2, 'the provider failure should use only the existing retry nudge')
    const nudge = harness.controller.promptCalls[1]
    assert.equal(nudge.sessionId, handshake.body.dshSessionId)
    assert.match(nudge.content[0].text, /MALFORMED_RESPONSE/)
    assert.match(nudge.content[0].text, /tool input is invalid JSON/)
    assert.match(nudge.content[0].text, new RegExp(decisionId))
    assert.match(nudge.content[0].text, /authorization unchanged/)
    const privateFailure = plugin.panelSnapshot().events.findLast(event =>
      event.kind === 'consult-turn-error' && event.requestId === 'req-turn-error-retry')
    assert.equal(privateFailure.code, 'MALFORMED_RESPONSE')
    assert.match(privateFailure.message, /tool input is invalid JSON/)

    const tool = harness.ctx.tools.registered.find(t => t.name === 'submit_decision')
    const submitted = submitTask(tool, {
      kind: 'noAction', decisionId,
      proposal: { decisionId, justification: 'submit the original consultation decision after provider error feedback' },
    })
    assert.equal(submitted.accepted, true, submitted.message ?? submitted.reason)
    emitTurnEnd(harness, handshake.body.dshSessionId)

    const result = await consultPromise
    assert.equal(result.body.task.payload.decisionId, decisionId)
    assert.equal(result.body.diagnostics.source, 'submit_decision_retry')
  } finally {
    emitTurnEnd(harness, handshake.body.dshSessionId)
    await call(harness.routes, '/api/uniclaw-agent/detach', {})
    await consultPromise
  }
})

for (const retryFailure of ['prompt-error', 'deadline']) {
  test(`retry ${retryFailure} preserves the physical turn fence until turn/end`, async () => {
    const harness = mockCtx()
    applyHost(harness)
    await call(harness.routes, '/api/uniclaw-agent/detach', {})
    const handshake = await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
    const prompt = harness.controller.prompt.bind(harness.controller)
    harness.controller.prompt = async (...args) => {
      const result = await prompt(...args)
      if (retryFailure === 'prompt-error' && args[0].requestId.includes('-nudge-'))
        throw new Error('simulated retry transport error after queueing')
      return result
    }
    const body = {
      requestId: `req-retry-${retryFailure}`, generation: 1,
      productSessionId: 'product-A', productRunId: 'run-A',
      dshSessionId: handshake.body.dshSessionId, turnTimeoutMs: 100,
      context: { decisionId: `decision-${retryFailure}`, runId: 'run-A', phase: 'InitialPlanning' },
    }
    const consultation = call(harness.routes, '/api/uniclaw-agent/consult', body)
    try {
      for (let i = 0; i < 50 && harness.controller.promptCalls.length < 1; i++)
        await new Promise(resolve => setTimeout(resolve, 5))
      assert.equal(harness.controller.promptCalls.length, 1)
      emitTurnEnd(harness, handshake.body.dshSessionId)
      await consultation
      assert.equal(harness.controller.promptCalls.length, 2)
      const next = await call(harness.routes, '/api/uniclaw-agent/consult', {
        ...body, requestId: body.requestId + '-next', generation: 2,
        context: { ...body.context, decisionId: body.context.decisionId + '-next' },
      })
      assert.equal(next.body.error?.code, 'one-in-flight')
      assert.equal(harness.controller.promptCalls.length, 2, 'no new physical prompt before retry turn/end')
    } finally {
      emitTurnEnd(harness, handshake.body.dshSessionId)
      await call(harness.routes, '/api/uniclaw-agent/detach', {})
      await consultation
    }
  })
}

test('B3: consultation prompt binds effectClass to runtime allowedEffects', async () => {
  const harness = mockCtx()
  applyHost(harness, { promptMount: 'per-turn' })
  await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  const consultPromise = call(harness.routes, '/api/uniclaw-agent/consult', {
    requestId: 'req-effect-token-1',
    generation: 1,
    productSessionId: 'product-A',
    productRunId: 'run-A',
    dshSessionId: 'session-test-1-1',
    turnTimeoutMs: 5000,
    context: {
      decisionId: 'decision-effect-token-1',
      runId: 'run-A',
      phase: 'InitialPlanning',
      allowedEffects: ['tap'],
    },
  })
  for (let i = 0; i < 50 && harness.controller.promptCalls.length === 0; i++) {
    await new Promise(resolve => setTimeout(resolve, 5))
  }
  assert.equal(harness.controller.promptCalls.length, 1)
  const prompt = harness.controller.promptCalls[0].content[0].text
  assert.match(prompt, /effectClass.*runtime token/i)
  assert.match(prompt, /MUST be[\s\S]*context\.allowedEffects/i)
  assert.match(prompt, /Allowed effectClass tokens.*\["tap"\]/i)
  assert.match(prompt, /use `tap`/i)
  assert.match(prompt, /use `swipe-up`/i)
  assert.match(prompt, /policy meanings navigate, back, and scroll/i)
  assert.match(prompt, /switch\/checkable control[\s\S]*desiredState is mandatory/i)
  emitTurnEnd(harness, harness.controller.promptCalls[0].sessionId)
  const result = await consultPromise
  assert.equal(result.body.error, 'no-submit-decision')
})

test('B3: missing Product-required fields → schema-invalid, never completed', async () => {
  const harness = mockCtx()
  applyHost(harness, { promptMount: 'per-turn' })
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
  submitTask(tool, {
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
  applyHost(harness, { promptMount: 'per-turn' })
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
  submitTask(tool, {
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
  applyHost(harness, { promptMount: 'per-turn' })
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
  submitTask(tool, {
    kind: 'act',
    decisionId: 'decision-repr-1',
    proposal: JSON.stringify({ decisionId: 'decision-repr-1', steps: { item: { targetRole: 'switch', effectClass: 'tap', desiredState: 'on' } } }),
  })
  emitTurnEnd(harness, harness.controller.promptCalls[0].sessionId)
  const result = await consultPromise
  assert.equal(result.body.error ?? 'ok', 'ok', result.body.diagnostics?.message ?? '')
  const steps = result.body.task.payload.proposal.steps
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
      spec: { subject: 'wifi.state', maxRounds: 2, depth: 'Normal' },
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
    const submitted = submitTask(tool, payload)
    assert.equal(submitted.accepted, true, submitted.reason)
    emitTurnEnd(harness, harness.controller.promptCalls[index].sessionId)
    const result = await consultPromise
    assert.equal(result.body.error ?? null, null, result.body.diagnostics?.message ?? '')
    assert.equal(result.body.requestId, requestId)
    assert.equal(result.body.generation, index + 1)
    assert.equal(result.body.task.payload.kind, entry.kind)
    assert.equal(result.body.task.payload.decisionId, decisionId)
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
    const submitted = submitTask(tool, invalid[index])
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
  submitTask(tool, {
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
  assert.equal(firstResult.body.task.payload.kind, 'noAction')
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
  const late = submitTask(tool, { kind: 'noAction', decisionId: 'decision-detach-1', proposal: { justification: 'late' } })
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
  const submitted = submitTask(tool, {
    kind: 'noAction', decisionId: 'decision-pnl-1',
    proposal: { decisionId: 'decision-pnl-1', justification: 'nothing to do' },
  }, { capabilityId: 'runtime.language-inspector', expectedLanguage: 'en' })
  assert.equal(submitted.accepted, true, submitted.reason)
  emitTurnEnd(harness, harness.controller.promptCalls[0].sessionId)
  const result = await consultPromise
  assert.equal(result.body.error ?? null, null)
  assert.equal(result.body.task.initialization.capabilitySelection.capabilityId, 'runtime.language-inspector')
  assert.equal(result.body.task.initialization.capabilitySelection.expectedLanguage, 'en')
  assert.equal(result.body.decision, undefined)

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
  const wrong = submitTask(tool, { kind: 'noAction', decisionId: 'decision-WRONG', proposal: { decisionId: 'decision-WRONG', justification: 'x' } })
  assert.equal(wrong.accepted, false)
  assert.equal(plugin.panelSnapshot().events.some(e => e.kind === 'decision-rejected' && e.code === 'decision-id-mismatch'), true)
  const recovered = submitTask(tool, { kind: 'noAction', decisionId: 'decision-pnl-expected', proposal: { decisionId: 'decision-pnl-expected', justification: 'x' } })
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

// ---- PRF-003: versioned product prompt manifest + scoped seam ----

const PROMPT_DIR = join(PACKAGE_ROOT, 'prompt')
const CANONICAL_PROMPT_DIR = join(PACKAGE_ROOT, '..', '..', 'product', 'prompt', 'uniagent-prod')

test('PRF-003: package prompt artifact stays byte-identical with product/prompt canonical', () => {
  const canonicalFiles = readdirSync(CANONICAL_PROMPT_DIR).sort()
  const packageFiles = readdirSync(PROMPT_DIR).sort()
  assert.deepEqual(packageFiles, canonicalFiles)
  for (const name of canonicalFiles)
    assert.ok(readFileSync(join(CANONICAL_PROMPT_DIR, name)).equals(readFileSync(join(PROMPT_DIR, name))),
      `prompt artifact drift: ${name}`)
})

test('PRF-003: prompt artifact hash mismatch fails closed at load', () => {
  const dir = mkdtempSync(join(tmpdir(), 'uniclaw-prompt-drift-'))
  for (const name of readdirSync(PROMPT_DIR)) cpSync(join(PROMPT_DIR, name), join(dir, name))
  writeFileSync(join(dir, '01-identity.txt'), 'tampered identity\n')
  assert.throws(() => plugin.loadPromptArtifactFor(dir, schemaArtifactFixture()), /prompt artifact hash mismatch/)
})

test('PRF-009: package skill artifact stays byte-identical with product canonical', () => {
  for (const name of ['manifest.yaml', 'guidance.md'])
    assert.ok(readFileSync(join(CANONICAL_SKILL_DIR, name)).equals(readFileSync(join(SKILL_DIR, name))),
      `skill artifact drift: ${name}`)
  const artifact = plugin.loadSkillArtifactFor(PACKAGE_ROOT)
  assert.equal(artifact.name, 'android-automotive-ui-testing')
  assert.equal(artifact.revision, 4)
  assert.match(artifact.guidance, /独立期望与授权预算/)
  assert.match(artifact.guidance, /有序 handlers/)
  assert.match(artifact.guidance, /Running.*不能当作成功/)
  assert.match(artifact.guidance, /交付结果与证据/)
  assert.equal(artifact.sha256, readFileSync(join(CANONICAL_SKILL_DIR, '..', '..', 'profiles', 'uniagent-prod.yaml'), 'utf8').match(/sha256: ([0-9a-f]{64})/)?.[1])
})

test('PRF-009: skill path/name/revision/hash drift fails closed at boot', () => {
  const dir = mkdtempSync(join(tmpdir(), 'uniclaw-skill-drift-'))
  const skillDir = join(dir, 'skill', 'android-automotive-ui-testing')
  mkdirSync(skillDir, { recursive: true })
  for (const name of ['manifest.yaml', 'guidance.md', 'skill-hash.txt'])
    cpSync(join(SKILL_DIR, name), join(skillDir, name))
  assert.throws(() => plugin.loadSkillArtifactFor(PACKAGE_ROOT, { path: '../escape' }), /package-relative/)
  assert.throws(() => plugin.loadSkillArtifactFor(dir, { name: 'wrong-name' }), /name mismatch/)
  writeFileSync(join(skillDir, 'guidance.md'), 'tampered\n')
  assert.throws(() => plugin.loadSkillArtifactFor(dir), /hash mismatch/)
})

test('PRF-009: per-turn fallback carries Skill guidance with the static prompt', async () => {
  const harness = mockCtx()
  activeHarness = harness // This test mounts directly; still retire its retry in afterEach.
  const sessionMount = mockCtx()
  plugin.apply(sessionMount.ctx, { sessionScoped: true, promptMount: 'per-turn' })
  plugin.apply(harness.ctx, { workspaceRoot: TEST_WORKSPACE_ROOT, promptMount: 'per-turn' })
  await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  const consultPromise = call(harness.routes, '/api/uniclaw-agent/consult', {
    requestId: 'req-skill-perturn-1', generation: 1,
    productSessionId: 'product-A', productRunId: 'run-A', dshSessionId: 'session-test-1-1',
    turnTimeoutMs: 5000,
    context: { decisionId: 'decision-skill-perturn-1', runId: 'run-A', phase: 'InitialPlanning' },
  })
  for (let i = 0; i < 50 && harness.controller.promptCalls.length === 0; i++)
    await new Promise(resolve => setTimeout(resolve, 5))
  const prompt = harness.controller.promptCalls[0].content[0].text
  assert.match(prompt, /独立期望与授权预算/)
  assert.match(prompt, /交付结果与证据/)
  emitTurnEnd(harness, harness.controller.promptCalls[0].sessionId)
  await consultPromise
})

test('PRF-003: prompt manifest protocol coupling mismatch fails closed at load', () => {
  const dir = mkdtempSync(join(tmpdir(), 'uniclaw-prompt-coupling-'))
  for (const name of readdirSync(PROMPT_DIR)) cpSync(join(PROMPT_DIR, name), join(dir, name))
  const manifest = JSON.parse(readFileSync(join(dir, 'manifest.json'), 'utf8'))
  manifest.protocol.schemaHash = 'deadbeef'.repeat(8)
  writeFileSync(join(dir, 'manifest.json'), JSON.stringify(manifest))
  assert.throws(() => plugin.loadPromptArtifactFor(dir, schemaArtifactFixture()), /protocol coupling mismatch/)
})

test('PRF-003: preset mount registers the scoped product-prompt section; host mount does not', () => {
  const preset = mockCtx()
  plugin.apply(preset.ctx, { sessionScoped: true })
  assert.equal(preset.controller.promptSections.length, 2)
  const [section, skillSection] = preset.controller.promptSections
  assert.equal(section.name, 'uniagent-prod:product-prompt')
  assert.equal(section.order, 100)
  assert.equal(section.interpolate, false)
  assert.match(section.text, /Role: You are the UniAgent decision component/)
  assert.match(section.text, /The task envelope must validate against the Product AgentDecision/)
  assert.match(section.text, /effectClass.*runtime token/i)
  assert.match(section.text, /顶层提交 `reviewResponse/)
  assert.match(section.text, /不是事实来源/)
  assert.equal(skillSection.name, 'uniagent-prod:skill:android-automotive-ui-testing')
  assert.equal(skillSection.order, 110)
  assert.match(skillSection.text, /独立期望与授权预算/)
  assert.match(skillSection.text, /Kernel 运行时协调执行/)
  assert.match(skillSection.text, /handler 执行 Owner/)

  const host = mockCtx()
  plugin.apply(host.ctx, {})
  assert.equal(host.controller.promptSections.length, 0)
})

test('PRF-003: section mount keeps per-turn content dynamic-only; consult-start carries promptRevision', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  const consultPromise = call(harness.routes, '/api/uniclaw-agent/consult', {
    requestId: 'req-prompt-section-1',
    generation: 1,
    productSessionId: 'product-A',
    productRunId: 'run-A',
    dshSessionId: 'session-test-1-1',
    turnTimeoutMs: 5000,
    context: {
      decisionId: 'decision-prompt-section-1',
      runId: 'run-A',
      phase: 'InitialPlanning',
      allowedEffects: ['tap'],
    },
  })
  for (let i = 0; i < 50 && harness.controller.promptCalls.length === 0; i++) {
    await new Promise(resolve => setTimeout(resolve, 5))
  }
  const prompt = harness.controller.promptCalls[0].content[0].text
  // Static discipline lives in the section, not the turn.
  assert.doesNotMatch(prompt, /You are the UniAgent decision component/)
  assert.doesNotMatch(prompt, /payload shape per kind/i)
  // Per-turn lines remain.
  assert.match(prompt, /current Product DecisionId is decision-prompt-section-1/)
  assert.match(prompt, /Allowed effectClass tokens.*\["tap"\]/)
  assert.match(prompt, /=== AgentDecisionContext \(JSON\) ===/)
  emitTurnEnd(harness, harness.controller.promptCalls[0].sessionId)
  await consultPromise
  const snap = plugin.panelSnapshot()
  const start = snap.events.find(e => e.kind === 'consult-start')
  // Revision tracks the manifest (dynamic read: future bumps don't break tests).
  const manifestRevision = JSON.parse(readFileSync(join(PROMPT_DIR, 'manifest.json'), 'utf8')).promptRevision
  assert.equal(start.promptRevision, manifestRevision)
})

test('PRF-003: per-turn fallback mounts the full static text in every turn', async () => {
  const harness = mockCtx()
  applyHost(harness, { promptMount: 'per-turn' })
  await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  const consultPromise = call(harness.routes, '/api/uniclaw-agent/consult', {
    requestId: 'req-prompt-perturn-1',
    generation: 1,
    productSessionId: 'product-A',
    productRunId: 'run-A',
    dshSessionId: 'session-test-1-1',
    turnTimeoutMs: 5000,
    context: { decisionId: 'decision-perturn-1', runId: 'run-A', phase: 'InitialPlanning' },
  })
  for (let i = 0; i < 50 && harness.controller.promptCalls.length === 0; i++) {
    await new Promise(resolve => setTimeout(resolve, 5))
  }
  const prompt = harness.controller.promptCalls[0].content[0].text
  assert.match(prompt, /You are the UniAgent decision component/)
  assert.match(prompt, /current Product DecisionId is decision-perturn-1/)
  emitTurnEnd(harness, harness.controller.promptCalls[0].sessionId)
  await consultPromise
})

test('PRF-004: handshake records the actual session isolation-probe inventory; host route exposes it read-only', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
  const handshake = await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  assert.equal(handshake.body.accepted, true)
  const probe = plugin.panelSnapshot().promptProbe
  assert.ok(probe, 'probe should be recorded for the attached session')
  assert.equal(probe.error, undefined)
  assert.deepEqual(probe.sections, ['uniagent-prod:product-prompt', 'uniagent-prod:skill:android-automotive-ui-testing'])
  assert.deepEqual(probe.contexts, ['probe-context'])
  assert.deepEqual(probe.tools, ['submit_decision'])
  assert.deepEqual(probe.variables, ['probeVar'])

  const route = harness.routes.get('/api/uniclaw-agent/prompt-probe')
  assert.ok(route, 'probe route registered')
  const response = await route.fetch(new Request('http://dsh.invalid/api/uniclaw-agent/prompt-probe'))
  const body = await response.json()
  assert.equal(body.recorded, true)
  assert.deepEqual(body.sections, ['uniagent-prod:product-prompt', 'uniagent-prod:skill:android-automotive-ui-testing'])
  assert.equal(body.recordedAt, probe.recordedAt)
})


test('ADR-0053: dynamic Plan and exact-position continuation use the same formal tool', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
  const handshake = await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  const tool = harness.ctx.tools.registered[0]
  assert.ok(tool.parameters.properties.task.properties.payload.properties.position)
  const position = { planId: 'p-menu', version: 1, itemIndex: 0, iteration: 1, bodyIndex: 2, candidateKey: 'entry:C', workUsed: 7, controlPath: '0.0.0:repeat:1/0.0.0:then:0' }
  const payloads = [
    { kind: 'plan', decisionId: 'plan-wire-1', proposal: { planId: 'p-menu', version: 1, maxLocalOperations: 30,
      stageExpectation: 'inspect observed candidates', items: [{ kind: 'forEach', source: 'settings.root-navigation',
        body: [{ kind: 'act', targetRole: '$candidate', targetDescriptor: '$candidate', effectClass: 'tap' },
          { kind: 'observe' }, { kind: 'act', targetRole: 'ui.element', targetDescriptor: 'Navigate up', effectClass: 'tap' }],
        maxIterations: 3, continueOnBlocked: true }, { kind: 'checkpoint', reason: 'review actual results' }] } },
    { kind: 'continuePlan', decisionId: 'plan-wire-2', position },
    { kind: 'plan', decisionId: 'plan-wire-3', proposal: { planId: 'p-conditional', version: 1, maxLocalOperations: 30,
      items: [{ kind: 'repeat', maxIterations: 3, body: [{ kind: 'if', condition: 'records.need-evidence',
        then: [{ kind: 'observe', subject: 'records.receipt' }], else: [] }] }] } },
  ]
  for (const [index, payload] of payloads.entries()) {
    const consultation = call(harness.routes, '/api/uniclaw-agent/consult', {
      requestId: `plan-wire-${index}`, generation: index + 1,
      productSessionId: 'product-A', productRunId: 'run-A', dshSessionId: handshake.body.dshSessionId,
      context: { decisionId: payload.decisionId, runId: 'run-A', phase: 'StepVerified',
        ...(index === 1 ? { planExecution: { position, status: 'Paused' } } : {}) },
    })
    for (let i = 0; i < 50 && harness.controller.promptCalls.length <= index; i++)
      await new Promise(resolve => setTimeout(resolve, 1))
    if (index === 0) {
      const bad = structuredClone(payload)
      bad.proposal.items[0].body = [{ kind: 'forEach', source: 'settings.root-navigation', body: [], maxIterations: 2, continueOnBlocked: false }]
      assert.equal(submitTask(tool, bad).accepted, false)
    }
    assert.equal(submitTask(tool, payload).accepted, true)
    emitTurnEnd(harness, handshake.body.dshSessionId)
    const captured = await consultation
    assert.deepEqual(captured.body.task.payload, payload)
  }
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
})

test('RUNTIME-PLAN-002: DSH accepts typed TraverseItem and rejects malformed operation bindings', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
  const handshake = await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  const tool = harness.ctx.tools.registered[0]
  const payload = {
    kind: 'plan', decisionId: 'traverse-plugin-1',
    proposal: { planId: 'p-traverse', version: 1, maxLocalOperations: 24,
      stageExpectation: 'inspect this observed menu',
      items: [{ kind: 'traverse', source: 'settings.current-navigation', maxDepth: 0,
        returnStep: { targetRole: 'Navigate up', effectClass: 'tap' },
        rules: [
          { ruleId: 'record', condition: 'always', operation: { operationId: 'read-current', kind: 'ReadOnly', step: null,
            before: [{ handlerId: 'language-before', capabilityId: 'runtime.language-inspector', protocolVersion: '1.0', expectedLanguage: 'en', required: true, requirePass: false }],
            afterVerified: [] } },
          { ruleId: 'open', condition: 'candidate.openable', operation: { operationId: 'navigate-candidate', kind: 'Navigate',
            step: { targetRole: 'menu item', targetDescriptor: '$candidate', effectClass: 'tap' }, before: [],
            afterVerified: [{ handlerId: 'language-after', capabilityId: 'runtime.language-inspector', protocolVersion: '1.0', expectedLanguage: 'en', required: true, requirePass: true }] } },
        ] }],
    },
  }
  const consultation = call(harness.routes, '/api/uniclaw-agent/consult', {
    requestId: 'traverse-plugin-request', generation: 1,
    productSessionId: 'product-A', productRunId: 'run-A', dshSessionId: handshake.body.dshSessionId,
    context: { decisionId: payload.decisionId, runId: 'run-A', phase: 'StepVerified' },
  })
  for (let i = 0; i < 50 && harness.controller.promptCalls.length === 0; i++)
    await new Promise(resolve => setTimeout(resolve, 1))

  const badDepth = structuredClone(payload)
  badDepth.proposal.items[0].maxDepth = 'zero'
  assert.equal(submitTask(tool, badDepth).accepted, false, 'wrongly typed traversal bound is rejected')
  const badVersion = structuredClone(payload)
  badVersion.proposal.items[0].rules[0].operation.before[0].protocolVersion = '2.0'
  assert.equal(submitTask(tool, badVersion).accepted, false, 'unsupported Language Inspection version is rejected')
  const badBinding = structuredClone(payload)
  badBinding.proposal.items[0].rules[0].operation.before[0].capabilityId = 'runtime.anything'
  assert.equal(submitTask(tool, badBinding).accepted, false, 'unregistered capability binding is rejected')
  const badReadOnly = structuredClone(payload)
  badReadOnly.proposal.items[0].rules[0].operation.step = { targetRole: 'menu', effectClass: 'tap' }
  assert.equal(submitTask(tool, badReadOnly).accepted, false, 'ReadOnly operation cannot carry an action step')
  const emptyHandler = structuredClone(payload)
  emptyHandler.proposal.items[0].rules[0].operation.before[0].handlerId = ''
  assert.equal(submitTask(tool, emptyHandler).accepted, false, 'empty typed handler identity is rejected')
  const duplicateHandler = structuredClone(payload)
  duplicateHandler.proposal.items[0].rules[1].operation.afterVerified[0].handlerId = 'language-before'
  assert.equal(submitTask(tool, duplicateHandler).accepted, false, 'handler identity is unique across rules and phases')
  const reservedOperation = structuredClone(payload)
  reservedOperation.proposal.items[0].rules[1].operation.operationId = '$return'
  assert.equal(submitTask(tool, reservedOperation).accepted, false, 'runtime return operation identity is reserved')

  assert.equal(submitTask(tool, payload).accepted, true)
  emitTurnEnd(harness, handshake.body.dshSessionId)
  const captured = await consultation
  assert.deepEqual(captured.body.task.payload, payload)
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
})

test('Slow high effort is applied only to its isolated request and attested in diagnostics', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  const pending = call(harness.routes, '/api/uniclaw-agent/slow', {
    requestId: 'slow-high', prompt: 'observe title', reasoningEffort: 'high',
    model: { provider: 'commandcode', model: 'configured-model' },
  })
  for (let i = 0; i < 50 && harness.controller.promptCalls.length === 0; i++)
    await new Promise(resolve => setTimeout(resolve, 5))
  const sessionId = harness.controller.promptCalls[0].sessionId
  const hook = harness.events.get('agent/request')?.[0]
  assert.equal(typeof hook, 'function')
  const seed = { provider: 'commandcode', model: 'configured-model', reasoningEffort: 'low' }
  assert.deepEqual(await hook({ agent: { session: { id: 'unrelated-session' } } }, async () => seed), seed)
  const actual = await hook({ agent: { session: { id: sessionId } } }, async () => seed)
  assert.equal(actual.reasoningEffort, 'high')
  emitAssistantText(harness, sessionId, '{"pageKey":"apps","observedTitle":"Apps"}')
  emitTurnEnd(harness, sessionId)
  const result = await pending
  assert.equal(result.body.diagnostics.reasoningEffort, 'high')
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
})

test('Correlated Slow abort ignores an expired request and leaves the main session alone', async () => {
  const harness = mockCtx()
  applyHost(harness)
  await call(harness.routes, '/api/uniclaw-agent/handshake', handshakeBody())
  const pending = call(harness.routes, '/api/uniclaw-agent/slow', { requestId: 'slow-owned', prompt: 'observe' })
  for (let i = 0; i < 50 && harness.controller.promptCalls.length === 0; i++)
    await new Promise(resolve => setTimeout(resolve, 5))
  const isolated = harness.controller.promptCalls[0].sessionId
  const stale = await call(harness.routes, '/api/uniclaw-agent/abort', { slowRequestId: 'expired-request' })
  assert.equal(stale.body.aborted, false)
  assert.equal(harness.controller.cancels.length, 0)
  const stopped = await call(harness.routes, '/api/uniclaw-agent/abort', { slowRequestId: 'slow-owned' })
  assert.equal(stopped.body.aborted, true)
  await pending
  assert.deepEqual(harness.controller.cancels, [{ sessionId: isolated }])
  await call(harness.routes, '/api/uniclaw-agent/detach', {})
})
