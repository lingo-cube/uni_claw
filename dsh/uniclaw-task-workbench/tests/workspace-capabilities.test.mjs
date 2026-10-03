import test from 'node:test'
import assert from 'node:assert/strict'
import { createDshWorkspaceCapabilities } from '../src/workspace-capabilities.js'

const clock = () => '2026-10-02T00:00:00.000Z'
const make = (overrides = {}) => {
  const calls = []
  const panel = {
    workspace: () => ({ success: true, projects: [{ projectId: 'p', instances: [{ instanceId: 'i', sessionId: 'dsh-1', task: { title: 'T' } }] }] }),
    session: ({ sessionId }) => { calls.push(['session', sessionId]); return { success: true, task: {}, instance: { projectRef: 'project/android-settings', testSetRef: 'testset/android-settings', taskRef: 'task/android-settings/wifi-state', runId: 'run-1', launchStages: [{ stage: 'run-bound', status: 'succeeded' }], storageNamespaceRef: 'local:task-instance/i' }, metadata: { owner: 'team-a' }, metadataClaims: [{ key: 'agentPreset', value: 'uniagent-prod', valueOrigin: 'configured', availability: 'present', source: 'config', authority: 'host' }], conversation: [{ kind: 'assistant' }], conversationGroups: [], dshTrace: [{ seq: 1 }], uniclawTrace: [{ token: 'VERIFIED' }], uniflowTrace: [{ token: 'VERIFIED' }], uniclawTraceContext: { schemaVersion: 'trc/0.1', traceId: 'trace-1', rootSpanId: 'span-1' }, uniclawTraceTruncated: true, evidence: ['evidence/a.md'] } },
    artifact: ({ sessionId, ref }) => { calls.push(['artifact', sessionId, ref]); return { success: true, ref, name: ref, text: 'detail' } },
    ...overrides,
  }
  return { panel, calls, capabilities: createDshWorkspaceCapabilities({ panel, resolveProductSession: id => id === 'product-1' ? { dshSessionId: 'dsh-1' } : null, now: clock, revision: 4 }) }
}

test('projects and task instances preserve unassociated DSH identity', async () => {
  const { capabilities } = make()
  const result = await capabilities.TaskQuery.listProjects({ requestId: 'r1' })
  assert.equal(result.ok, true)
  assert.equal(result.revision, 4)
  assert.equal(result.data.projects[0].instances[0].sessionId, 'dsh-1')
  assert.deepEqual(result.data.projects[0].instances[0].hostSessionRef, { host: 'dsh', sessionId: 'dsh-1' })
  assert.equal(result.data.projects[0].instances[0].productSessionId, undefined)
  assert.equal(result.data.errors[0].code, 'uncorrelated')
})

test('explicit product session mapping projects session, traces, and evidence', async () => {
  const { capabilities, calls } = make()
  const session = await capabilities.SessionQuery.getSession({ productSessionId: 'product-1' })
  assert.deepEqual(session.data.hostSessionRef, { host: 'dsh', sessionId: 'dsh-1' })
  assert.equal(session.data.runId, 'run-1')
  assert.equal(session.data.projectRef, 'project/android-settings')
  assert.equal(session.data.launchStages[0].stage, 'run-bound')
  assert.equal(session.data.storageNamespaceRef, 'local:task-instance/i')
  assert.equal(session.data.metadataClaims[0].valueOrigin, 'configured')
  const traces = await capabilities.TraceQuery.getTraces({ productSessionId: 'product-1' })
  assert.deepEqual(traces.data.traces.map(item => item.source), ['dsh', 'uniflow', 'uniclaw'])
  assert.equal(traces.data.traceContext.runId, 'run-1')
  assert.deepEqual(traces.data.traceContext.hostSessionRef, { host: 'dsh', sessionId: 'dsh-1' })
  assert.equal(traces.data.traceContext.sources[0].authority, 'dsh-host')
  assert.equal(traces.data.traceContext.sources[0].schemaVersion, null)
  assert.equal(traces.data.traceContext.sources[2].schemaVersion, 'trc/0.1')
  assert.equal(traces.data.traceContext.sources[2].truncated, true)
  assert.equal(traces.data.traceContext.sources.every(item => item.cursor === null), true)
  const evidence = await capabilities.EvidenceQuery.getEvidence({ productSessionId: 'product-1' })
  assert.deepEqual(evidence.data.evidence, ['evidence/a.md'])
  assert.deepEqual(calls, [['session', 'dsh-1'], ['session', 'dsh-1'], ['session', 'dsh-1']])
})

test('missing mapping is uncorrelated and never upgrades a DSH session', async () => {
  const { capabilities, calls } = make()
  const result = await capabilities.SessionQuery.getSession({ productSessionId: 'dsh-1' })
  assert.equal(result.ok, false)
  assert.equal(result.error.code, 'uncorrelated')
  assert.deepEqual(calls, [])
})

test('task query rejects unknown projects and malformed instances', async () => {
  const unknown = make().capabilities
  const missing = await unknown.TaskQuery.listTaskInstances({ projectId: 'missing' })
  assert.equal(missing.error.code, 'not-found')
  const malformed = make({ workspace: () => ({ success: true, projects: [{ projectId: 'p', instances: {} }] }) }).capabilities
  const broken = await malformed.TaskQuery.listProjects()
  assert.equal(broken.error.code, 'unavailable')
})

test('trace and evidence failures retain their requested capability', async () => {
  const { capabilities } = make({ session: () => ({ success: false, error: { code: 'artifact-not-found', message: 'missing' } }) })
  const trace = await capabilities.TraceQuery.getTraces({ productSessionId: 'product-1' })
  const evidence = await capabilities.EvidenceQuery.getEvidence({ productSessionId: 'product-1' })
  assert.equal(trace.capability, 'TraceQuery')
  assert.equal(evidence.capability, 'EvidenceQuery')
})

test('panel errors remain structured and local', async () => {
  const { capabilities } = make({ session: () => ({ success: false, error: { code: 'artifact-not-found', message: 'missing' } }) })
  const result = await capabilities.SessionQuery.getSession({ productSessionId: 'product-1' })
  assert.equal(result.ok, false)
  assert.equal(result.error.code, 'not-found')
})

test('malformed successful panel payloads fail closed', async () => {
  const projects = make({ workspace: () => ({ success: true }) })
  const projectResult = await projects.capabilities.TaskQuery.listProjects()
  assert.equal(projectResult.error.code, 'unavailable')

  const session = make({ session: () => ({ success: true, conversation: [] }) })
  const sessionResult = await session.capabilities.SessionQuery.getSession({ productSessionId: 'product-1' })
  assert.equal(sessionResult.error.code, 'unavailable')
})

test('resolver failures are structured and do not reject capabilities', async () => {
  const { panel } = make()
  const capabilities = createDshWorkspaceCapabilities({ panel, resolveProductSession: () => { throw new Error('resolver down') }, now: clock, revision: 4 })
  const result = await capabilities.SessionQuery.getSession({ productSessionId: 'product-1' })
  assert.equal(result.ok, false)
  assert.equal(result.error.code, 'unavailable')
})

test('detail only accepts controlled relative refs', async () => {
  const { capabilities, calls } = make()
  for (const ref of ['/etc/passwd', '../secret', 'https://example.com/x', 'C:\\secret']) {
    const result = await capabilities.DetailQuery.resolveDetail({ productSessionId: 'product-1', detailRef: { refId: ref } })
    assert.equal(result.ok, false)
    assert.equal(result.error.code, 'not-found')
  }
  const result = await capabilities.DetailQuery.resolveDetail({ productSessionId: 'product-1', detailRef: { refId: 'evidence/a.md' } })
  assert.equal(result.ok, true)
  assert.deepEqual(calls.at(-1), ['artifact', 'dsh-1', 'evidence/a.md'])
})
