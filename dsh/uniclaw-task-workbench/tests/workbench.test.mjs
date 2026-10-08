// PNL-001 S1/S2 contract tests (node:test, no DSH runtime).
// Drive apply() with a mock cordis ctx and exercise the registered routes
// directly: schema-hash gate, protocol validation (fail-closed), JSON store
// persistence, and instantiate session creation. Same mock discipline as
// dsh/uniclaw-decision-channel/tests/plugin.test.mjs.

import test from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync, copyFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'

const PACKAGE_ROOT = dirname(dirname(fileURLToPath(import.meta.url)))
const plugin = await import(join(PACKAGE_ROOT, 'src', 'index.js'))

const FROZEN_SCHEMA_DIR = join(PACKAGE_ROOT, 'schema')

/** Writable temp root per test run: tampered schema copies + JSON stores. The
 * real frozen schema/ and the operator's home are never touched. */
const TEST_ROOT = mkdtempSync(join(tmpdir(), 'uniclaw-task-wb-'))

/** Apply the plugin in host mode against a fresh mock ctx. */
function applyHost({ schemaDir = FROZEN_SCHEMA_DIR, controller, registry, runtime } = {}) {
  const routes = new Map()
  const provided = {}
  const ctx = {
    reflect: { provide(key, value) { provided[key] = value } },
    connection: {
      fetch: {
        register(route) { routes.set(route.path, route) },
      },
    },
    on() {},
    get(key) {
      if (key === 'sessionController') return controller
      if (key === 'workspaceRegistry') return registry
      if (key === 'uniclawRuntime') return runtime
      return undefined
    },
  }
  const storePath = join(TEST_ROOT, `store-${Math.random().toString(36).slice(2)}.json`)
  plugin.apply(ctx, { schemaDir, storePath, artifactRoots: [] })
  return { routes, storePath, provided }
}

function mockController({ createError = null } = {}) {
  const controller = {
    createCalls: [],
    async create(request) {
      controller.createCalls.push(request)
      if (createError !== null) throw createError
      return { sessionId: `session-mock-${controller.createCalls.length}` }
    },
  }
  return controller
}

function mockRegistry({ existingPaths = {} } = {}) {
  const registry = {
    createCalls: [],
    async resolveByPath(path) {
      return existingPaths[path] ?? null
    },
    async create(path, title) {
      registry.createCalls.push({ path, title })
      return { id: `workspace-${registry.createCalls.length}`, path, title }
    },
    async resolveProjectToWorkspace(projectRef) {
      return { id: `workspace-project-${projectRef.replace(/[^a-z0-9]+/gi, '-')}` }
    },
  }
  return registry
}

async function post(routes, path, body) {
  const route = routes.get(path)
  assert.ok(route, `route ${path} must be registered`)
  return route.fetch(new Request(`http://dsh.local${path}`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(body),
  }))
}

async function get(routes, path) {
  const route = routes.get(path)
  assert.ok(route, `route ${path} must be registered`)
  return route.fetch(new Request(`http://dsh.local${path}`, { method: 'GET' }))
}

const createTaskBody = {
  title: 'Fix flaky login test',
  requirement: 'Make tests/auth.test.mjs deterministic on CI.',
  projectRef: { path: '/tmp/uniclaw-demo-repo' },
}

test('apply registers a Host Runtime provider without minting local identities', () => {
  const { provided } = applyHost()
  assert.equal(typeof provided.uniclawRuntime?.createRun, 'function')
  assert.equal(typeof provided.uniclawRuntime?.recoverRun, 'function')
})

test('apply preserves an existing Host Runtime provider', () => {
  const runtime = { createRun() {}, recoverRun() {} }
  const { provided } = applyHost({ runtime })
  assert.equal(provided.uniclawRuntime, undefined)
})

test('schema artifact self-check: drifted schema refuses apply() startup', () => {
  const tamperedDir = join(TEST_ROOT, 'tampered-schema')
  mkdirSync(tamperedDir, { recursive: true })
  copyFileSync(join(FROZEN_SCHEMA_DIR, 'task-protocol.schema.json'), join(tamperedDir, 'task-protocol.schema.json'))
  copyFileSync(join(FROZEN_SCHEMA_DIR, 'schema-hash.txt'), join(tamperedDir, 'schema-hash.txt'))
  // Byte-drift the schema without updating the frozen hash.
  const schema = JSON.parse(readFileSync(join(tamperedDir, 'task-protocol.schema.json'), 'utf8'))
  schema.$defs.CreateTaskRequest.properties.title.minLength = 99
  writeFileSync(join(tamperedDir, 'task-protocol.schema.json'), JSON.stringify(schema, null, 2))
  assert.throws(() => plugin.apply({
    connection: { fetch: { register() {} } },
    get: () => undefined,
  }, { schemaDir: tamperedDir }), /self-check failed/)
})

test('POST /api/uniclaw-task/tasks: valid body 200 and JSON store persists readably', async () => {
  const { routes, storePath } = applyHost()
  const response = await post(routes, '/api/uniclaw-task/tasks', createTaskBody)
  assert.equal(response.status, 200)
  const payload = await response.json()
  assert.equal(payload.ok, true)

  // Persistence is the proof: the store on DISK reads back with the task.
  const disk = JSON.parse(readFileSync(storePath, 'utf8'))
  assert.equal(disk.storeVersion, 1)
  const stored = disk.tasks.find(t => t.taskId === payload.task.taskId)
  assert.ok(stored, 'task persisted to disk')
  assert.equal(stored.title, createTaskBody.title)
  assert.equal(stored.requirement, createTaskBody.requirement)
  assert.equal(stored.status, 'active')

  // The list route serves the same store.
  const list = await get(routes, '/api/uniclaw-task/tasks')
  assert.equal(list.status, 200)
  assert.equal((await list.json()).tasks.length, 1)
})

test('POST /api/uniclaw-task/tasks: invalid bodies fail closed with gateway/bad-request', async () => {
  const { routes } = applyHost()

  // Missing required requirement.
  let response = await post(routes, '/api/uniclaw-task/tasks', { title: 'x' })
  assert.equal(response.status, 400)
  assert.equal((await response.json()).error.code, 'gateway/bad-request')

  // Extra field (additionalProperties:false).
  response = await post(routes, '/api/uniclaw-task/tasks', { ...createTaskBody, priority: 'high' })
  assert.equal(response.status, 400)
  assert.equal((await response.json()).error.code, 'gateway/bad-request')

  // Status outside the enum.
  response = await post(routes, '/api/uniclaw-task/tasks', { ...createTaskBody, status: 'archived' })
  assert.equal(response.status, 400)
  assert.equal((await response.json()).error.code, 'gateway/bad-request')

  // Non-JSON body.
  const route = routes.get('/api/uniclaw-task/tasks')
  response = await route.fetch(new Request('http://dsh.local/x', { method: 'POST', body: 'not json' }))
  assert.equal(response.status, 400)
  assert.equal((await response.json()).error.code, 'gateway/bad-request')
})

test('instantiate: active task creates uniagent-task session and persists the instance', async () => {
  const controller = mockController()
  const registry = mockRegistry()
  const { routes, storePath } = applyHost({ controller, registry })

  const created = await post(routes, '/api/uniclaw-task/tasks', createTaskBody)
  const { task } = await created.json()

  const response = await post(routes, '/api/uniclaw-task/tasks/instantiate', { taskId: task.taskId })
  assert.equal(response.status, 200)
  const payload = await response.json()
  assert.equal(payload.ok, true)

  // Session identity comes from the mocked controller, under the frozen preset.
  const instance = payload.instance
  assert.match(instance.instanceId, /^psi-[0-9a-f]{16}$/)
  assert.equal(instance.sessionId, 'session-mock-1')
  assert.equal(instance.status, 'active')
  assert.deepEqual(controller.createCalls, [{ agentPreset: 'uniagent-task', workspaceId: 'workspace-1' }])
  // 决策 4: projectRef.path is resolved (missing → registered) via the registry.
  assert.deepEqual(registry.createCalls, [{ path: '/tmp/uniclaw-demo-repo', title: undefined }])

  // Persisted on disk, not just in the response.
  const disk = JSON.parse(readFileSync(storePath, 'utf8'))
  const stored = disk.tasks.find(t => t.taskId === task.taskId)
  assert.equal(stored.instances.length, 1)
  assert.equal(stored.instances[0].sessionId, 'session-mock-1')
  assert.equal(stored.instances[0].status, 'active')
})

test('instantiate: existing workspace path is reused via resolveByPath', async () => {
  const controller = mockController()
  const registry = mockRegistry({ existingPaths: { '/tmp/uniclaw-demo-repo': { id: 'workspace-existing' } } })
  const { routes } = applyHost({ controller, registry })

  const { task } = await (await post(routes, '/api/uniclaw-task/tasks', createTaskBody)).json()
  const response = await post(routes, '/api/uniclaw-task/tasks/instantiate', { taskId: task.taskId })
  assert.equal(response.status, 200)
  assert.deepEqual(controller.createCalls, [{ agentPreset: 'uniagent-task', workspaceId: 'workspace-existing' }])
  assert.deepEqual(registry.createCalls, [])
})

test('instantiate: unknown taskId → task-not-found; non-active → task-not-active', async () => {
  const controller = mockController()
  const registry = mockRegistry()
  const { routes } = applyHost({ controller, registry })

  let response = await post(routes, '/api/uniclaw-task/tasks/instantiate', { taskId: 'task-0123456789abcdef' })
  assert.equal(response.status, 404)
  assert.equal((await response.json()).error.code, 'task-not-found')

  // Draft definitions do not instantiate.
  const created = await post(routes, '/api/uniclaw-task/tasks', { ...createTaskBody, status: 'draft' })
  const { task } = await created.json()
  response = await post(routes, '/api/uniclaw-task/tasks/instantiate', { taskId: task.taskId })
  assert.equal(response.status, 409)
  assert.equal((await response.json()).error.code, 'task-not-active')
  assert.deepEqual(controller.createCalls, [])
})

test('instantiate: controller.create failure → session-create-failed (fail closed, nothing recorded)', async () => {
  const controller = mockController({ createError: new Error('preset not deployed') })
  const registry = mockRegistry()
  const { routes, storePath } = applyHost({ controller, registry })

  const { task } = await (await post(routes, '/api/uniclaw-task/tasks', createTaskBody)).json()
  const response = await post(routes, '/api/uniclaw-task/tasks/instantiate', { taskId: task.taskId })
  assert.equal(response.status, 500)
  assert.equal((await response.json()).error.code, 'session-create-failed')

  // No instance is recorded for a failed session creation.
  const disk = JSON.parse(readFileSync(storePath, 'utf8'))
  assert.equal(disk.tasks.find(t => t.taskId === task.taskId).instances, undefined)
})

test('store corruption → store-corrupted, not a 5xx crash', async () => {
  const { routes, storePath } = applyHost()
  writeFileSync(storePath, '{ not json')
  const response = await get(routes, '/api/uniclaw-task/tasks')
  assert.equal(response.status, 500)
  assert.equal((await response.json()).error.code, 'store-corrupted')
  rmSync(storePath) // leave the temp root clean for later tests in this file
})

test('instantiate: malformed body → gateway/bad-request before any store access', async () => {
  const controller = mockController()
  const { routes } = applyHost({ controller, registry: mockRegistry() })
  const response = await post(routes, '/api/uniclaw-task/tasks/instantiate', { nope: true })
  assert.equal(response.status, 400)
  assert.equal((await response.json()).error.code, 'gateway/bad-request')
  assert.deepEqual(controller.createCalls, [])
})

test('launch: Runtime-owned run and Host binding are explicit and idempotent', async () => {
  const controller = mockController()
  const runtime = { calls: [], async createRun(request) { runtime.calls.push(request); return { runId: 'run-runtime-1', productSessionId: 'product-session-1' } } }
  const { routes } = applyHost({ controller, registry: mockRegistry(), runtime })
  const request = { schemaVersion: 'uniclaw.workspace.task-launch-request.v1', contractVersion: 'uniclaw.workspace.contract.v1', launchRequestId: 'launch-request-1', projectRef: { id: 'project/android-settings' }, testSetRef: { id: 'testset/android-settings', version: 'default' }, taskRef: { id: 'task/android-settings/toggle-wifi-reuse' }, idempotencyKey: 'idem-1', correlationId: 'corr-1', requestedAt: new Date().toISOString(), environmentIntent: { device: { id: 'emulator-5558', override: true, source: 'workspace-launch-form' } }, metadata: [] }
  const first = await post(routes, '/api/uniclaw-task/tasks/launch', request)
  assert.equal(first.status, 200)
  const firstPayload = await first.json()
  assert.equal(firstPayload.ok, true)
  assert.equal(firstPayload.instance.runId, 'run-runtime-1')
  assert.equal(firstPayload.instance.productSessionId, 'product-session-1')
  assert.equal(firstPayload.instance.status, 'active')
  assert.deepEqual(runtime.calls[0].environmentIntent, request.environmentIntent)
  assert.equal(firstPayload.ack.schemaVersion, 'uniclaw.workspace.task-launch-ack.v1')
  assert.equal(firstPayload.ack.bindings.runId, 'run-runtime-1')
  assert.deepEqual(firstPayload.ack.bindings.hostSessionRef, { host: 'dsh', sessionId: firstPayload.instance.sessionId })
  assert.equal(firstPayload.ack.bindings.storageNamespaceRef.storageKind, 'local-filesystem')
  assert.ok(firstPayload.ack.stages.every(stage => stage.schemaVersion && stage.contractVersion && stage.stageId && stage.recordedAt && stage.availability === 'present'))
  const second = await post(routes, '/api/uniclaw-task/tasks/launch', request)
  const secondPayload = await second.json()
  assert.equal(second.status, 200)
  assert.equal(secondPayload.idempotent, true)
  assert.equal(secondPayload.instance.instanceId, firstPayload.instance.instanceId)
  assert.equal(runtime.calls.length, 1)
  assert.equal(controller.createCalls.length, 1)
})

test('launch: Runtime-owned dshSessionId is reused without creating a second DSH session', async () => {
  const controller = mockController()
  const runtime = { async createRun() { return { runId: 'run-runtime-session', productSessionId: 'product-runtime-session', dshSessionId: 'dsh-runtime-session' } } }
  const { routes } = applyHost({ controller, registry: mockRegistry(), runtime })
  const request = { schemaVersion: 'uniclaw.workspace.task-launch-request.v1', contractVersion: 'uniclaw.workspace.contract.v1', launchRequestId: 'launch-runtime-session', projectRef: { id: 'project/android-settings' }, testSetRef: { id: 'testset/android-settings', version: 'default' }, taskRef: { id: 'task/android-settings/toggle-wifi-reuse' }, idempotencyKey: 'idem-runtime-session', correlationId: 'corr-runtime-session', requestedAt: new Date().toISOString(), metadata: [] }
  const response = await post(routes, '/api/uniclaw-task/tasks/launch', request)
  assert.equal(response.status, 200)
  const payload = await response.json()
  assert.equal(payload.ok, true)
  assert.equal(payload.instance.sessionId, 'dsh-runtime-session')
  assert.equal(payload.instance.hostSessionRef, 'dsh-runtime-session')
  assert.deepEqual(payload.ack.bindings.hostSessionRef, { host: 'dsh', sessionId: 'dsh-runtime-session' })
  assert.equal(controller.createCalls.length, 0)
})

test('launch: Runtime hostSessionRef is reused without requiring project resolution', async () => {
  const controller = mockController()
  const runtime = { async createRun() { return { runId: 'run-runtime-host-ref', productSessionId: 'product-runtime-host-ref', hostSessionRef: { host: 'dsh', sessionId: 'dsh-host-ref' } } } }
  const { routes } = applyHost({ controller, registry: undefined, runtime })
  const request = { schemaVersion: 'uniclaw.workspace.task-launch-request.v1', contractVersion: 'uniclaw.workspace.contract.v1', launchRequestId: 'launch-runtime-host-ref', projectRef: { id: 'project/android-settings' }, testSetRef: { id: 'testset/android-settings', version: 'default' }, taskRef: { id: 'task/android-settings/toggle-wifi-reuse' }, idempotencyKey: 'idem-runtime-host-ref', correlationId: 'corr-runtime-host-ref', requestedAt: new Date().toISOString(), metadata: [] }
  const response = await post(routes, '/api/uniclaw-task/tasks/launch', request)
  assert.equal(response.status, 200)
  const payload = await response.json()
  assert.equal(payload.ok, true)
  assert.equal(payload.instance.sessionId, 'dsh-host-ref')
  assert.equal(payload.instance.hostSessionRef, 'dsh-host-ref')
  assert.equal(controller.createCalls.length, 0)
})

test('launch: Host-configured catalog root resolves repository-owned testsets', async () => {
  const catalogRoot = join(TEST_ROOT, 'configured-testsets')
  const manifestDir = join(catalogRoot, 'external-catalog')
  mkdirSync(manifestDir, { recursive: true })
  writeFileSync(join(manifestDir, 'manifest.json'), JSON.stringify({
    schemaVersion: 'testset-manifest.v1',
    projectRef: 'project/external-catalog',
    testSetRef: 'testset/external-catalog',
    version: 'default',
    tasks: [{ taskRef: 'task/external-catalog/smoke', displayName: 'External catalog smoke' }],
  }))
  const previous = process.env.UNICLAW_TESTSET_ROOT
  process.env.UNICLAW_TESTSET_ROOT = catalogRoot
  try {
    const runtime = { async createRun() { return { runId: 'run-external-catalog', productSessionId: 'session-external-catalog' } } }
    const { routes } = applyHost({ controller: mockController(), registry: mockRegistry(), runtime })
    const response = await post(routes, '/api/uniclaw-task/tasks/launch', {
      schemaVersion: 'uniclaw.workspace.task-launch-request.v1', contractVersion: 'uniclaw.workspace.contract.v1',
      launchRequestId: 'launch-external-catalog', projectRef: { id: 'project/external-catalog' },
      testSetRef: { id: 'testset/external-catalog', version: 'default' }, taskRef: { id: 'task/external-catalog/smoke' },
      idempotencyKey: 'idem-external-catalog', correlationId: 'corr-external-catalog', requestedAt: new Date().toISOString(), metadata: [],
    })
    assert.equal(response.status, 200)
    assert.equal((await response.json()).instance.runId, 'run-external-catalog')
  } finally {
    if (previous === undefined) delete process.env.UNICLAW_TESTSET_ROOT
    else process.env.UNICLAW_TESTSET_ROOT = previous
  }
})

test('launch: Host failure leaves a partial recoverable instance after Runtime created run', async () => {
  const runtime = { async createRun() { return { runId: 'run-partial-1', productSessionId: 'product-partial-1' } } }
  const controller = mockController({ createError: new Error('host unavailable') })
  const { routes } = applyHost({ controller, registry: mockRegistry(), runtime })
  const response = await post(routes, '/api/uniclaw-task/tasks/launch', { schemaVersion: 'uniclaw.workspace.task-launch-request.v1', contractVersion: 'uniclaw.workspace.contract.v1', launchRequestId: 'launch-request-partial', projectRef: { id: 'project/uni-claw-test-lab' }, testSetRef: { id: 'testset/workspace-contract', version: 'default' }, taskRef: { id: 'task/workspace-contract/request-decision-result' }, idempotencyKey: 'idem-partial', correlationId: 'corr-partial', requestedAt: new Date().toISOString(), metadata: [] })
  assert.equal(response.status, 500)
  const payload = await response.json()
  assert.equal(payload.error.code, 'launch-partial')
  assert.equal(payload.error.details.partial.status, 'partial')
  assert.equal(payload.error.details.partial.runId, 'run-partial-1')
})

test('launch: Runtime failure keeps a stable pending instance and requires recovery before retry', async () => {
  let recoverCalls = 0
  let createCalls = 0
  const runtime = {
    async createRun() { createCalls += 1; throw new Error('runtime transport lost') },
    async recoverRun({ launchId }) { recoverCalls += 1; return { runId: `run-recovered-${launchId}`, productSessionId: 'product-recovered-1' } },
  }
  const controller = mockController()
  const { routes } = applyHost({ controller, registry: mockRegistry(), runtime })
  const request = { schemaVersion: 'uniclaw.workspace.task-launch-request.v1', contractVersion: 'uniclaw.workspace.contract.v1', launchRequestId: 'launch-request-runtime-retry', projectRef: { id: 'project/uni-claw-test-lab' }, testSetRef: { id: 'testset/workspace-contract', version: 'default' }, taskRef: { id: 'task/workspace-contract/request-decision-result' }, idempotencyKey: 'idem-runtime-retry', correlationId: 'corr-runtime-retry', requestedAt: new Date().toISOString(), metadata: [] }
  const first = await post(routes, '/api/uniclaw-task/tasks/launch', request)
  assert.equal(first.status, 500)
  const second = await post(routes, '/api/uniclaw-task/tasks/launch', request)
  const payload = await second.json()
  assert.equal(second.status, 200)
  assert.equal(payload.instance.status, 'active')
  assert.equal(createCalls, 1)
  assert.equal(recoverCalls, 1)
})

test('launch: missing Product Session stays partial and does not create a Host session', async () => {
  const controller = mockController()
  const runtime = { async createRun() { return { runId: 'run-without-product-session' } } }
  const { routes } = applyHost({ controller, registry: mockRegistry(), runtime })
  const request = { schemaVersion: 'uniclaw.workspace.task-launch-request.v1', contractVersion: 'uniclaw.workspace.contract.v1', launchRequestId: 'launch-request-no-product-session', projectRef: { id: 'project/uni-claw-test-lab' }, testSetRef: { id: 'testset/workspace-contract', version: 'default' }, taskRef: { id: 'task/workspace-contract/request-decision-result' }, idempotencyKey: 'idem-no-product-session', correlationId: 'corr-no-product-session', requestedAt: new Date().toISOString(), metadata: [] }
  const response = await post(routes, '/api/uniclaw-task/tasks/launch', request)
  const payload = await response.json()
  assert.equal(response.status, 409)
  assert.equal(payload.error.code, 'product-session-unavailable')
  assert.equal(payload.error.details.partial.status, 'partial')
  assert.equal(controller.createCalls.length, 0)
})

test('launch: partial retry reuses the Runtime run and binds the recovered Host session', async () => {
  let failHost = true
  const controller = { createCalls: [], async create(request) {
    controller.createCalls.push(request)
    if (failHost) throw new Error('host unavailable')
    return { sessionId: 'session-recovered' }
  } }
  const runtime = { calls: [], async createRun(request) { runtime.calls.push(request); return { runId: 'run-recover-1', productSessionId: 'product-recover-1' } } }
  const { routes } = applyHost({ controller, registry: mockRegistry(), runtime })
  const request = { schemaVersion: 'uniclaw.workspace.task-launch-request.v1', contractVersion: 'uniclaw.workspace.contract.v1', launchRequestId: 'launch-request-retry', projectRef: { id: 'project/uni-claw-test-lab' }, testSetRef: { id: 'testset/workspace-contract', version: 'default' }, taskRef: { id: 'task/workspace-contract/request-decision-result' }, idempotencyKey: 'idem-retry', correlationId: 'corr-retry', requestedAt: new Date().toISOString(), metadata: [] }
  const first = await post(routes, '/api/uniclaw-task/tasks/launch', request)
  assert.equal(first.status, 500)
  failHost = false
  const second = await post(routes, '/api/uniclaw-task/tasks/launch', request)
  const payload = await second.json()
  assert.equal(second.status, 200)
  assert.equal(payload.idempotent, false)
  assert.equal(payload.instance.runId, 'run-recover-1')
  assert.equal(payload.instance.status, 'active')
  assert.equal(runtime.calls.length, 1)
  assert.equal(controller.createCalls.length, 2)
})

test('launch: metadata and logical refs fail closed before Runtime creation', async () => {
  const runtime = { calls: 0, async createRun() { runtime.calls += 1; return { runId: 'run-never-created' } } }
  const { routes } = applyHost({ controller: mockController(), registry: mockRegistry(), runtime })
  const base = { schemaVersion: 'uniclaw.workspace.task-launch-request.v1', contractVersion: 'uniclaw.workspace.contract.v1', launchRequestId: 'launch-request-invalid', projectRef: { id: 'project/android-settings' }, testSetRef: { id: 'testset/android-settings', version: 'default' }, taskRef: { id: 'task/android-settings/toggle-wifi-reuse' }, idempotencyKey: 'idem-invalid', correlationId: 'corr-invalid', requestedAt: new Date().toISOString(), metadata: [] }
  let response = await post(routes, '/api/uniclaw-task/tasks/launch', { ...base, metadata: [{ key: 'agentPreset', value: 'uniagent-prod', valueOrigin: 'observed', availability: 'present', source: 'test', authority: 'test' }] })
  assert.equal(response.status, 400)
  assert.equal((await response.json()).error.code, 'invalid-launch-request')
  response = await post(routes, '/api/uniclaw-task/tasks/launch', { ...base, projectRef: { id: '../physical/path' } })
  assert.equal(response.status, 400)
  assert.equal(runtime.calls, 0)
})

// ---------------------------------------------------------------------------
// PNL-001 panel service (WI-PNL001-003). The six uniclawTaskPanel methods are
// exercised through the exported factory — the exact implementation the Typert
// decoration delegates to (typert-protocol is absent offline, by design).
// ---------------------------------------------------------------------------

import { createTaskRepository } from '../src/repository.js'
const { createTaskPanelService } = plugin
const { createArtifactSource } = await import('../src/artifacts.js')

const FIXTURE_EVENTS = [
  { seq: 0, type: 'system/message', data: 'host setup should stay in DSH Trace' },
  { seq: 1, type: 'user/message', role: 'user', data: 'You are the UniAgent decision component of the UniClaw Product runtime.\n=== AgentDecisionContext (JSON) ===\n{"decisionId":"decision-fixture-1","objective":"login","phase":"InitialPlanning","allowedEffects":[],"contractVersion":"v1","currentWorldClaims":{},"pendingObligations":[],"runId":"run-fixture-1"}' },
  { seq: 2, type: 'user/message', role: 'user', data: 'run the login task' },
  { seq: 3, type: 'tool/call', data: { name: 'bash', output: 'x'.repeat(500) } },
  { seq: 4, type: 'turn/end', data: 'done' },
]

const CARD_EVENTS = [
  {
    seq: 10,
    type: 'user/message',
    data: {
      source: { kind: 'user' },
      content: [{ type: 'text', text: `You are the UniAgent decision component.\n=== AgentDecisionContext (JSON) ===\n${JSON.stringify({ decisionId: 'decision-real-1', objective: '完成登录', phase: 'InitialPlanning', screen: '登录页', budgetRemaining: 4, runId: 'run-real-1' })}` }],
    },
  },
  {
    seq: 14,
    type: 'assistant/message',
    data: { message: { content: [{ type: 'tool-call', id: 'call-real-1', name: 'submit_decision', arguments: JSON.stringify({ decision: { decisionId: 'decision-real-1', kind: 'act', proposal: { justification: '登录按钮可见', steps: ['点击登录'], }, spec: { target: '登录按钮' } } }) }] } },
  },
  {
    seq: 15,
    type: 'tool/call',
    data: { callId: 'call-real-1', name: 'submit_decision', arguments: JSON.stringify({ decision: { decisionId: 'decision-real-1', kind: 'act', proposal: { justification: '登录按钮可见', steps: ['点击登录'], }, spec: { target: '登录按钮' } } }) },
  },
  {
    seq: 16,
    type: 'tool/result',
    data: { toolCallId: 'call-real-1', message: { content: [{ type: 'text', text: 'submit_decision accepted: decision captured' }] } },
  },
]

/** Events carrying UniFlow semantics in their text (the only source the
 * extractor reads — zero fabricated entries). */
const SEMANTIC_EVENTS = [
  { seq: 1, type: 'agent/message', ts: '2026-01-01T00:00:01Z', data: 'phase gate: STAY_IN_DIAGNOSIS pending; evidence at evidence/debug-trace.md' },
  { seq: 2, type: 'agent/message', ts: '2026-01-01T00:00:02Z', data: 'gate passed EXPLORE_RESOLVED, refs evidence/foo.md and evidence/bar.log' },
  { seq: 3, type: 'tool/call', ts: '2026-01-01T00:00:03Z', data: { output: 'plain tool output, no semantics' } },
  { seq: 4, type: 'agent/message', ts: '2026-01-01T00:00:04Z', data: 'COMPLETE for task; see evidence/summary.md' },
]

/** Panel service over a fresh store + mock ctx (sessionQuery optional). */
function makePanel({
  sessionQuery = {
    async listSessions() {
      return [{ header: { id: 'session-panel-1', createdAt: Date.parse('2026-01-01T00:00:00Z') }, live: true, persisted: true }]
    },
    async readTitleSnapshots(ids) {
      return ids.map(sessionId => ({
        sessionId,
        status: 'fulfilled',
        value: { session: { id: sessionId }, title: { title: 'Panel session' } },
      }))
    },
    async readSession(sessionId) { return sessionId === 'session-panel-1' ? { events: FIXTURE_EVENTS } : { events: [] } },
  },
  controller,
} = {}) {
  const repository = createTaskRepository(join(TEST_ROOT, `panel-${Math.random().toString(36).slice(2)}.json`))
  const ctx = {
    get(key) {
      if (key === 'sessionQuery') return sessionQuery
      if (key === 'sessionController') return controller
      return undefined
    },
  }
  return { service: createTaskPanelService(ctx, repository), repository }
}

function seedInstance(repository, { withSession = true } = {}) {
  const task = repository.create({ title: 'Panel task', requirement: 'Panel requirement.' })
  const instance = repository.addInstance(task.taskId, {
    instanceId: 'psi-panel0000000001',
    ...(withSession ? { sessionId: 'session-panel-1' } : {}),
    status: 'active',
    startedAt: new Date().toISOString(),
  })
  return { task, instance }
}

test('panel sessions: listSessions+readTitles merged; uniclaw marks repository instance sessions', async () => {
  const { service, repository } = makePanel()
  seedInstance(repository) // instance.sessionId = session-panel-1 → uniclaw
  const result = await service.sessions()
  assert.equal(result.success, true)
  assert.deepEqual(result.sessions, [
    { sessionId: 'session-panel-1', title: 'Panel session', lastActiveAt: null, createdAt: Date.parse('2026-01-01T00:00:00Z'), live: true, persisted: true, cwd: null, agentPreset: null, uniclaw: true },
  ])
})

test('panel sessions: session not in the instance table is not marked uniclaw; title falls back', async () => {
  const { service, repository } = makePanel()
  repository.create({ title: 'No instances', requirement: 'R.' })
  const result = await service.sessions()
  assert.equal(result.success, true)
  assert.deepEqual(result.sessions, [
    { sessionId: 'session-panel-1', title: 'Panel session', lastActiveAt: null, createdAt: Date.parse('2026-01-01T00:00:00Z'), live: true, persisted: true, cwd: null, agentPreset: null, uniclaw: false },
  ])
})

test('panel sessions: sessionQuery missing → session-query-unavailable', async () => {
  const { service } = makePanel({ sessionQuery: null })
  const result = await service.sessions()
  assert.equal(result.success, false)
  assert.equal(result.error.code, 'session-query-unavailable')
})

test('panel trace: semantic extraction from event text (gate/outcome/evidence, tokens + refs)', async () => {
  const { service, repository } = makePanel({
    sessionQuery: { async readSession() { return { events: SEMANTIC_EVENTS } } },
  })
  const result = await service.trace({ sessionId: 'session-panel-1' })
  assert.equal(result.success, true)
  assert.equal(result.sessionAvailable, true)

  const kinds = Object.fromEntries(['gate', 'outcome', 'evidence'].map(k => [k, result.entries.filter(e => e.kind === k)]))
  // Gate token STAY_IN_DIAGNOSIS with the evidence path of the same event text.
  assert.equal(kinds.gate.length, 1)
  assert.equal(kinds.gate[0].token, 'STAY_IN_DIAGNOSIS')
  assert.deepEqual(kinds.gate[0].evidenceRefs, ['evidence/debug-trace.md'])
  // Outcome tokens + same-event evidence refs.
  assert.deepEqual(kinds.outcome.map(e => e.token), ['EXPLORE_RESOLVED', 'COMPLETE'])
  assert.deepEqual(kinds.outcome[0].evidenceRefs, ['evidence/foo.md', 'evidence/bar.log'])
  assert.deepEqual(kinds.outcome[1].evidenceRefs, ['evidence/summary.md'])
  // Evidence entries: one per path, token = the path itself.
  assert.equal(kinds.evidence.length, 4)
  assert.ok(kinds.evidence.every(e => e.evidenceRefs.length === 1 && e.token === e.evidenceRefs[0]))
  // All entries carry the event ts and capped text.
  assert.ok(result.entries.every(e => typeof e.ts === 'string' && e.ts.startsWith('2026-01-01')))
  assert.ok(result.entries.every(e => e.text.length <= 200))
})

test('panel trace: no semantic hits in events → entries=[]', async () => {
  const { service, repository } = makePanel()
  seedInstance(repository)
  const result = await service.trace({ sessionId: 'session-panel-1' })
  assert.equal(result.success, true)
  assert.deepEqual(result.entries, [])
})

test('panel trace: sessionQuery missing → session-query-unavailable; missing sessionId → invalid-request', async () => {
  const { service } = makePanel({ sessionQuery: null })
  const missing = await service.trace({ sessionId: 'session-panel-1' })
  assert.equal(missing.error.code, 'session-query-unavailable')

  const { service: withQuery } = makePanel()
  const noSessionId = await withQuery.trace({})
  assert.equal(noSessionId.success, false)
  assert.equal(noSessionId.error.code, 'invalid-request')
})

test('panel workspace: task instances are grouped by project and definitions without instances are omitted', async () => {
  const { service, repository } = makePanel()
  const first = repository.create({ title: 'Settings traversal', requirement: 'R1', projectRef: { path: '/repo/uni-claw' } })
  const second = repository.create({ title: 'No instance yet', requirement: 'R2', projectRef: { path: '/repo/uni-claw' } })
  const third = repository.create({ title: 'Other project', requirement: 'R3', projectRef: { path: '/repo/other' } })
  repository.addInstance(first.taskId, { instanceId: 'psi-1', productSessionId: 'product-session-1', sessionId: 'session-panel-1', status: 'active', startedAt: '2026-01-01T00:00:00Z' })
  repository.addInstance(third.taskId, { instanceId: 'psi-2', sessionId: 'session-panel-2', status: 'completed', startedAt: '2026-01-02T00:00:00Z', endedAt: '2026-01-02T00:10:00Z' })
  const result = service.workspace()
  assert.equal(result.success, true)
  assert.equal(result.localConfig.device, 'emulator-5556')
  assert.deepEqual(result.localConfig.deviceOptions, ['emulator-5556'])
  assert.equal(result.localConfig.authMode, 'browser-token')
  assert.equal(result.localConfig.authStatus, 'host-managed')
  assert.deepEqual(result.projects.map(p => p.path), ['/repo/uni-claw', '/repo/other'])
  assert.deepEqual(result.projects[0].instances.map(i => i.sessionId), ['session-panel-1'])
  assert.equal(result.projects[0].instances[0].productSessionId, 'product-session-1')
  assert.equal(result.projects[0].instances[0].correlationStatus, 'correlated')
  assert.deepEqual(result.projects[1].instances.map(i => i.sessionId), ['session-panel-2'])
  assert.equal(result.projects.some(p => p.instances.some(i => i.task.taskId === second.taskId)), false)
})

test('panel session: one detail projection carries conversation, DSH trace, UniClaw trace and evidence', async () => {
  const { service, repository } = makePanel()
  const { task, instance } = seedInstance(repository)
  const result = await service.session({ sessionId: instance.sessionId })
  assert.equal(result.success, true)
  assert.equal(result.task.taskId, task.taskId)
  assert.equal(result.instance.instanceId, instance.instanceId)
  assert.equal(result.conversation.length, FIXTURE_EVENTS.length - 1)
  assert.deepEqual(result.conversation.map(event => event.label), ['调用方请求', '任务输入', '能力调用', '回合结束'])
  assert.equal(result.dshTrace.length, FIXTURE_EVENTS.length)
  assert.equal(result.conversationGroups.length, 1)
  assert.equal(result.conversationGroups[0].status, 'completed')
  assert.equal(result.conversationGroups[0].request.label, '调用方请求')
  assert.deepEqual(result.uniclawTrace, [])
  assert.deepEqual(result.evidence, [])
})

test('panel session: request, submit_decision and result become correlated product cards', async () => {
  const { service, repository } = makePanel({
    sessionQuery: {
      async listSessions() { return [{ header: { id: 'session-panel-1', createdAt: 1 }, live: true, persisted: true }] },
      async readTitleSnapshots() { return [] },
      async readSession() { return { events: CARD_EVENTS } },
    },
  })
  const { instance } = seedInstance(repository)
  const result = await service.session({ sessionId: instance.sessionId })
  assert.equal(result.success, true)
  assert.deepEqual(result.conversation.map(event => event.label), ['调用方请求', 'Uni-Agent 决策'])
  assert.equal(result.conversation[0].objective, '完成登录')
  assert.equal(result.conversation[1].decisionKind, 'act')
  assert.equal(result.conversation[1].decisionId, 'decision-real-1')
  assert.equal(result.conversation[1].result.text, 'submit_decision accepted: decision captured')
  assert.equal(result.conversation[1].result.isError, false)
  assert.equal(result.conversationGroups.length, 1)
  assert.equal(result.conversationGroups[0].status, 'submitted')
  assert.equal(result.conversationGroups[0].request.objective, '完成登录')
  assert.equal(result.conversationGroups[0].decision.decisionKind, 'act')
  assert.equal(result.conversationGroups[0].result.text, 'submit_decision accepted: decision captured')
  assert.equal(result.conversationGroups[0].submission.seq, 16)
  assert.deepEqual(result.conversationGroups[0].stages.map(stage => stage.label), ['调用方请求', 'Uni-Agent 决策', '提交结果'])
  assert.deepEqual(result.runStages, [])
})

test('panel session: missing task instance fails closed', async () => {
  const { service } = makePanel()
  const result = await service.session({ sessionId: 'not-a-task-instance' })
  assert.equal(result.success, false)
  assert.equal(result.error.code, 'task-instance-not-found')
})

test('panel workspace/session: explicit run metadata links an observed session and Kernel Trace', async () => {
  const runDir = join(TEST_ROOT, 'observed-run')
  mkdirSync(runDir, { recursive: true })
  writeFileSync(join(runDir, 'metadata.json'), JSON.stringify({
    dshSessionId: 'session-observed-1', productSessionId: 'product-1',
    productSessionTitle: '真实 Settings 任务', workspace: 'UniClaw Product Tasks', status: 'Completed', outcome: 'Satisfied', device: 'emulator-5556', androidApi: 35, wmSize: '1080x1920', real: true,
  }))
  writeFileSync(join(runDir, 'facts.json'), JSON.stringify({
    outcome: 'Satisfied', reason: '目标状态已满足', completionAnchors: [{ anchor: 'switch-state-checked', verified: true }],
  }))
  writeFileSync(join(runDir, 'trace.json'), JSON.stringify({
    schemaVersion: 'trc/0.1', traceId: 'trc-1', rootSpanId: 'sp-1', runId: 'run-1', spans: [{ spanId: 'sp-1', parentSpanId: null, spanDefinitionId: 'world.reconcile', structuralOutcome: 0, spanKind: 'internal', status: 'OK', durationMs: 125, captureSequence: 1, references: [{ kind: 1, value: 'ev-1' }], events: [] }],
  }))
  const repository = createTaskRepository(join(TEST_ROOT, 'artifact-store.json'))
  const source = createArtifactSource([TEST_ROOT])
  const sessionQuery = {
    async listSessions() { return [{ header: { id: 'session-observed-1', createdAt: 1, cwd: '/repo' }, live: false, persisted: true }] },
    async readTitleSnapshots() { return [{ sessionId: 'session-observed-1', status: 'fulfilled', value: { title: { title: '真实 Settings 任务' } } }] },
    async readSession() { return { events: SEMANTIC_EVENTS } },
  }
  const ctx = { get(key) { return key === 'sessionQuery' ? sessionQuery : undefined } }
  const service = createTaskPanelService(ctx, repository, source)
  const workspace = service.workspace()
  assert.equal(workspace.projects[0].instances[0].sessionId, 'session-observed-1')
  const detail = await service.session({ sessionId: 'session-observed-1' })
  assert.equal(detail.success, true)
  assert.equal(detail.metadata.device, 'emulator-5556')
  assert.equal(detail.metadata.androidApi, 35)
  assert.equal(detail.metadata.wmSize, '1080x1920')
  assert.equal(detail.metadata.real, true)
  assert.deepEqual(detail.uniclawTraceContext, { schemaVersion: 'trc/0.1', traceId: 'trc-1', rootSpanId: 'sp-1', runId: 'run-1', spanCount: 1, recorderTerminal: null })
  assert.equal(detail.uniclawTrace[0].definition, 'world.reconcile')
  assert.equal(detail.uniclawTrace[0].spanKind, 'internal')
  assert.equal(detail.uniclawTrace[0].status, 'OK')
  assert.equal(detail.uniclawTrace[0].durationMs, 125)
  assert.equal(detail.uniclawTrace[0].references[0].kind, 'Evidence')
  assert.ok(detail.evidence.some(ref => ref.endsWith('/trace.json')))
  assert.deepEqual(detail.runStages.map(stage => [stage.kind, stage.label, stage.status]), [
    ['execution', '执行结果', 'Satisfied'], ['verification', '验证结果', '已验证'],
  ])
  const traceArtifact = await service.artifact({ sessionId: 'session-observed-1', ref: join(runDir, 'trace.json') })
  assert.equal(traceArtifact.success, true)
  assert.equal(traceArtifact.name, 'trace.json')
  assert.match(traceArtifact.text, /trc\/0\.1/)
  const relativeArtifact = await service.artifact({ sessionId: 'session-observed-1', ref: 'evidence/not-linked.md' })
  assert.equal(relativeArtifact.success, false)
  assert.equal(relativeArtifact.error.code, 'artifact-not-found')
})

test('panel diagnostic: frozen template prompt reaches controller.prompt with skill name + sessionId', async () => {
  const controller = mockController()
  controller.promptCalls = []
  controller.prompt = async (request) => { controller.promptCalls.push(request) }
  const { service, repository } = makePanel({ controller })
  const { instance } = seedInstance(repository)
  const result = await service.diagnostic({ sessionId: instance.sessionId, skill: 'diagnosing-bugs' })
  assert.deepEqual(result, { success: true, injected: true })
  assert.equal(controller.promptCalls.length, 1)
  const prompt = controller.promptCalls[0]
  assert.equal(prompt.sessionId, instance.sessionId)
  assert.match(prompt.content[0].text, /diagnosing-bugs/)
  assert.match(prompt.content[0].text, new RegExp(instance.sessionId))
})

test('panel diagnostic: arbitrary session (no instance) prompts directly with the frozen skill text', async () => {
  const controller = mockController()
  controller.promptCalls = []
  controller.prompt = async (request) => { controller.promptCalls.push(request) }
  const { service } = makePanel({ controller })
  const result = await service.diagnostic({ sessionId: 'session-free-form', skill: 'uniclaw-debug-evidence' })
  assert.deepEqual(result, { success: true, injected: true })
  assert.equal(controller.promptCalls[0].sessionId, 'session-free-form')
  assert.match(controller.promptCalls[0].content[0].text, /uniclaw-debug-evidence/)
})

test('panel diagnostic: invalid skill → invalid-skill; missing sessionId → invalid-request', async () => {
  const { service } = makePanel({ controller: mockController() })
  let result = await service.diagnostic({ sessionId: 'session-panel-1', skill: 'not-a-skill' })
  assert.equal(result.error.code, 'invalid-skill')
  result = await service.diagnostic({ skill: 'diagnosing-bugs' })
  assert.equal(result.error.code, 'invalid-request')
})

test('panel overview/createTask/instantiate: frozen response shapes over the shared core', async () => {
  const controller = mockController()
  const registry = mockRegistry({ existingPaths: { '/tmp/uniclaw-demo-repo': { id: 'workspace-existing' } } })
  const repository = createTaskRepository(join(TEST_ROOT, `panel-core-${Math.random().toString(36).slice(2)}.json`))
  const ctx = {
    get(key) {
      if (key === 'sessionController') return controller
      if (key === 'workspaceRegistry') return registry
      return undefined
    },
  }
  const service = createTaskPanelService(ctx, repository)

  const created = service.createTask({ title: 'T', requirement: 'R', projectRef: { path: '/tmp/uniclaw-demo-repo' } })
  assert.equal(created.success, true)
  assert.equal(created.task.status, 'active')

  const overview = service.overview()
  assert.equal(overview.success, true)
  assert.deepEqual(Object.keys(overview.tasks[0]).sort(),
    ['createdAt', 'instanceCount', 'projectRef', 'requirement', 'status', 'taskId', 'title'])

  const instantiated = await service.instantiate({ taskId: created.task.taskId })
  assert.equal(instantiated.success, true)
  assert.match(instantiated.instance.instanceId, /^psi-[0-9a-f]{16}$/)
  assert.equal(instantiated.instance.sessionId, 'session-mock-1')
  // instantiate errors surface as {success:false,error:{code}}, never throw.
  const bad = await service.instantiate({ taskId: 'task-missing00000000' })
  assert.equal(bad.success, false)
  assert.equal(bad.error.code, 'task-not-found')
})

test('typert-protocol unavailable offline: apply() still registers working HTTP routes', () => {
  // Environment sanity for THIS guard: without the package the probe must be
  // false, so the typert block is skipped and the HTTP lane is untouched.
  let resolvable = true
  try { createRequireProbe() } catch { resolvable = false }
  assert.equal(resolvable, false, 'this degrade test requires the offline env (no typert-protocol)')
  const { routes } = applyHost()
  assert.ok(routes.get('/api/uniclaw-task/tasks'))
  assert.ok(routes.get('/api/uniclaw-task/tasks/instantiate'))
})

// Mirrors the plugin's own sync probe so the test asserts the degrade path,
// not just the absence of an exception.
import { createRequire } from 'node:module'
function createRequireProbe() {
  createRequire(import.meta.url).resolve('@deepseek-ai/dsh-typert-protocol')
}
