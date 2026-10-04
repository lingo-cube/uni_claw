/**
 * @uniclaw/dsh-task-workbench — UniClaw Product Workspace host half (PNL-002).
 *
 * One plugin, three authenticated HTTP routes on the /api lane (决策 5):
 *   GET  /api/uniclaw-task/tasks              — list task definitions
 *   POST /api/uniclaw-task/tasks              — create a task definition
 *   POST /api/uniclaw-task/tasks/instantiate  — create a task instance (S2)
 *
 * The wire protocol is the GENERATED artifact (schema/task-protocol.schema.json
 * + schema-hash.txt), same discipline as decision-channel's product-protocol:
 * apply() self-checks the canonical-JSON sha256 against schema-hash.txt and
 * refuses startup on drift. Request bodies are validated by a minimal validator
 * covering exactly the subset the artifact uses (type/enum/required/properties/
 * additionalProperties/minLength/$ref) — no handwritten protocol truth here.
 *
 * instantiate turns an active Task definition into a real DSH session under
 * the `uniagent-task` preset (决策 6) and records the Task Instance (决策 1:
 * Task = definition, Session = instance). The session controller and
 * workspace registry are host services resolved through ctx, so tests drive
 * the whole surface with mocks.
 */

import { createRequire } from 'node:module'
import { randomBytes } from 'node:crypto'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'
import { createTaskRepository, sha256File } from './repository.js'
import { createArtifactSource } from './artifacts.js'
import { createRuntimeProvider } from './runtime-provider.js'

/** Sync availability probe for the profile-only typert-protocol package.
 * A failed dynamic import inside the node --test child leaves
 * module-resolution sockets that stall the runner (~60s), so availability is
 * probed synchronously BEFORE any dynamic import (same pattern as
 * decision-channel's panel block). */
const panelProtocolAvailable = () => {
  try {
    createRequire(import.meta.url).resolve('@deepseek-ai/dsh-typert-protocol')
    return true
  } catch {
    return false
  }
}

export const name = 'uniclaw-task-workbench'
export const inject = ['connection', 'sessionController', 'workspaceRegistry']

const PACKAGE_ROOT = dirname(dirname(fileURLToPath(import.meta.url)))
const REPOSITORY_ROOT = dirname(dirname(PACKAGE_ROOT))
const AGENT_PRESET_ID = 'uniagent-task'
const envText = (name, fallback) => {
  const value = process.env[name]
  return typeof value === 'string' && value.trim().length > 0 ? value.trim() : fallback
}
const configuredDevice = envText('UNICLAW_ANDROID_DEVICE', 'emulator-5556')
const configuredDeviceOptions = [...new Set([
  configuredDevice,
  ...envText('UNICLAW_ANDROID_DEVICE_OPTIONS', configuredDevice)
    .split(',').map(item => item.trim()).filter(Boolean),
])]
const DEFAULT_LOCAL_LAUNCH_DEFAULTS = Object.freeze({
  projectRef: { id: 'project/uni-claw-test-lab', label: 'UniClaw Test Lab' },
  testSetRef: { id: 'testset/workspace-contract', version: 'default' },
  taskRef: { id: 'task/workspace-contract/request-decision-result', label: 'Request, decision, and result timeline' },
})
const DEFAULT_LOCAL_CONFIG = Object.freeze({
  schemaVersion: 'uniclaw.workspace.local-config.v1',
  machine: `${process.platform} · ${process.arch}`,
  hostVersion: 'dsh-local',
  hostEndpoint: envText('UNICLAW_DSH_WEB_URL', 'http://127.0.0.1:3081'),
  authMode: envText('UNICLAW_DSH_AUTH_MODE', 'browser-token'),
  authStatus: envText('UNICLAW_DSH_AUTH_STATUS', 'host-managed'),
  nodeVersion: process.version,
  workspaceVersion: '0.1.0',
  agentPreset: AGENT_PRESET_ID,
  device: configuredDevice,
  deviceOptions: configuredDeviceOptions,
})

/** Load the protocol artifact and fail closed if its bytes drifted from the frozen hash. */
function loadSchemaArtifact(schemaDir) {
  const schema = JSON.parse(readFileSync(join(schemaDir, 'task-protocol.schema.json'), 'utf8'))
  const schemaHash = readFileSync(join(schemaDir, 'schema-hash.txt'), 'utf8').trim()
  // Hash of the artifact AS FILED (sha256 over the raw bytes, via the
  // repository seam's sha256File) — matches the Leader-frozen schema-hash.txt.
  const localHash = sha256File(join(schemaDir, 'task-protocol.schema.json'))
  if (localHash !== schemaHash) {
    throw new Error(
      `uniclaw-task-workbench: schema artifact self-check failed (file hash ${localHash} != schema-hash.txt ${schemaHash})`)
  }
  return { schema, schemaHash }
}

/** Resolve a local $ref (#/$defs/Name) inside the artifact. */
function deref(schema, node, depth = 0) {
  if (node === null || typeof node !== 'object') return node
  if (depth > 12) return node
  if (typeof node.$ref === 'string' && node.$ref.startsWith('#/$defs/')) {
    return deref(schema, schema.$defs[node.$ref.slice('#/$defs/'.length)], depth + 1)
  }
  return node
}

// ---------------------------------------------------------------------------
// Minimal schema validation — only the keywords the artifact uses. Unknown
// keywords are ignored by design (subset validator); REQUIRED fields missing,
// enum violations, extra properties under additionalProperties:false and
// too-short strings all fail closed at the route boundary.
// ---------------------------------------------------------------------------

function typeMatches(value, expected) {
  switch (expected) {
    case 'object': return typeof value === 'object' && value !== null && !Array.isArray(value)
    case 'array': return Array.isArray(value)
    case 'string': return typeof value === 'string'
    case 'number': case 'integer': return typeof value === 'number'
    case 'boolean': return typeof value === 'boolean'
    default: return true
  }
}

function validateToSchema(value, schemaNode, rootSchema, path, errors) {
  const node = deref(rootSchema, schemaNode)
  if (node === null || typeof node !== 'object' || Object.keys(node).length === 0) return true
  if (node.enum !== undefined && !node.enum.includes(value)) {
    errors.push(`${path || 'value'}: ${JSON.stringify(value)} not in enum [${node.enum.map(String).join(',')}]`)
    return false
  }
  if (node.type !== undefined && value !== null && value !== undefined && !typeMatches(value, node.type)) {
    errors.push(`${path || 'value'}: expected ${node.type}, got ${Array.isArray(value) ? 'array' : typeof value}`)
    return false
  }
  if (typeof value === 'string' && typeof node.minLength === 'number' && value.length < node.minLength) {
    errors.push(`${path || 'value'}: shorter than minLength ${node.minLength}`)
    return false
  }
  if (node.type === 'object' && typeof value === 'object' && value !== null && !Array.isArray(value)) {
    for (const required of node.required ?? []) {
      if (value[required] === undefined) {
        errors.push(`${path ? path + '.' : ''}${required}: required by task protocol but missing`)
      }
    }
    const properties = node.properties ?? {}
    for (const [key, child] of Object.entries(value)) {
      if (properties[key] === undefined) {
        if (node.additionalProperties === false) {
          errors.push(`${path ? path + '.' : ''}${key}: not in task protocol schema (additionalProperties false)`)
        }
        continue
      }
      validateToSchema(child, properties[key], rootSchema, `${path ? path + '.' : ''}${key}`, errors)
    }
  }
  return errors.length === 0
}

const validateRequest = (body, rootSchema, defName) => {
  const errors = []
  const ok = validateToSchema(body, rootSchema.$defs[defName], rootSchema, 'body', errors)
  return { ok, message: errors.slice(0, 4).join('; ') }
}

const newInstanceId = () => `psi-${randomBytes(8).toString('hex')}`
const newLaunchId = () => `launch-${randomBytes(8).toString('hex')}`
const nowIso = () => new Date().toISOString()

const launchStage = ({ stage, status = 'succeeded', source, authority, availability = 'present', valueOrigin, taskInstanceId, runId, diagnostic }) => ({
  schemaVersion: 'uniclaw.workspace.launch-stage-record.v1',
  contractVersion: 'uniclaw.workspace.contract.v1',
  stageId: `stage-${randomBytes(6).toString('hex')}`,
  stage, status, source, authority, availability, valueOrigin,
  ...(taskInstanceId ? { taskInstanceId } : {}), ...(runId ? { runId } : {}),
  ...(diagnostic ? { diagnostic } : {}), recordedAt: nowIso(),
})

const loadCatalog = (testSetRef) => {
  const manifestPath = join(REPOSITORY_ROOT, 'testsets', testSetRef.id.replace(/^testset\//, ''), 'manifest.json')
  try {
    const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'))
    const task = manifest.tasks?.find(item => item.taskRef === testSetRef.taskRef)
    if (manifest.testSetRef !== testSetRef.id || manifest.projectRef !== testSetRef.projectRef
      || manifest.version !== testSetRef.version
      || (testSetRef.sourceRevision && manifest.sourceRevision !== testSetRef.sourceRevision) || !task) return null
    return { manifest, task }
  } catch { return null }
}

const logicalRefValid = (ref) => typeof ref?.id === 'string'
  && ref.id.length > 0
  && !ref.id.startsWith(('/', './', '../'))
  && !ref.id.includes('\\')
  && !ref.id.split('/').some(segment => segment === '..')

const resolveLaunchDefaults = (value) => {
  const candidate = value && typeof value === 'object' ? value : {}
  const projectRef = logicalRefValid(candidate.projectRef) ? { ...candidate.projectRef } : DEFAULT_LOCAL_LAUNCH_DEFAULTS.projectRef
  const testSetRef = logicalRefValid(candidate.testSetRef) && candidate.testSetRef.version === 'default'
    ? { id: candidate.testSetRef.id, version: 'default', ...(candidate.testSetRef.sourceRevision ? { sourceRevision: candidate.testSetRef.sourceRevision } : {}) }
    : DEFAULT_LOCAL_LAUNCH_DEFAULTS.testSetRef
  const taskRef = logicalRefValid(candidate.taskRef) ? { ...candidate.taskRef } : DEFAULT_LOCAL_LAUNCH_DEFAULTS.taskRef
  return Object.freeze({ projectRef, testSetRef, taskRef })
}

const resolveLocalConfig = (value) => {
  const candidate = value && typeof value === 'object' ? value : {}
  const strings = Object.fromEntries(Object.keys(DEFAULT_LOCAL_CONFIG)
    .filter(key => key !== 'deviceOptions' && typeof candidate[key] === 'string' && candidate[key].length > 0)
    .map(key => [key, candidate[key]]))
  const device = strings.device || DEFAULT_LOCAL_CONFIG.device
  const configuredDevices = Array.isArray(candidate.deviceOptions)
    ? candidate.deviceOptions.filter(item => typeof item === 'string' && item.length > 0)
    : []
  const deviceOptions = [...new Set([device, ...configuredDevices])]
  return Object.freeze({ ...DEFAULT_LOCAL_CONFIG, ...strings, device, deviceOptions })
}

const configuredClaimsValid = claims => Array.isArray(claims)
  && claims.every(claim => claim !== null && typeof claim === 'object'
    && typeof claim.key === 'string' && claim.key.length > 0
    && Object.hasOwn(claim, 'value')
    && claim.valueOrigin === 'configured'
    && ['present', 'partial', 'unavailable', 'not-collected', 'not-applicable', 'not-associated', 'permission-denied'].includes(claim.availability)
    && typeof claim.source === 'string' && claim.source.length > 0
    && typeof claim.authority === 'string' && claim.authority.length > 0)

const launchRequestValid = (body) => body !== null && typeof body === 'object'
  && body.schemaVersion === 'uniclaw.workspace.task-launch-request.v1'
  && body.contractVersion === 'uniclaw.workspace.contract.v1'
  && typeof body.launchRequestId === 'string' && body.launchRequestId.length > 0
  && typeof body.correlationId === 'string' && body.correlationId.length > 0
  && typeof body.requestedAt === 'string' && !Number.isNaN(Date.parse(body.requestedAt))
  && typeof body.idempotencyKey === 'string' && body.idempotencyKey.length > 0
  && logicalRefValid(body.projectRef)
  && logicalRefValid(body.taskRef)
  && logicalRefValid(body.testSetRef)
  && body.testSetRef.version === 'default'
  && (!Object.hasOwn(body.testSetRef, 'sourceRevision')
    || (typeof body.testSetRef.sourceRevision === 'string'
      && body.testSetRef.sourceRevision.length > 0
      && !body.testSetRef.sourceRevision.includes('/')
      && !body.testSetRef.sourceRevision.includes('\\')))
  && configuredClaimsValid(body.metadata ?? [])

const launchRefId = ref => typeof ref === 'string' ? ref : ref?.id

/** Task Launch write seam. Runtime is the only authority allowed to mint runId.
 * The existing instantiateTask path remains frozen for compatibility. */
const launchTask = async (ctx, repository, request) => {
  if (!launchRequestValid(request)) {
    return { ok: false, status: 400, code: 'invalid-launch-request', message: 'canonical TaskLaunchRequest fields and logical refs are required (testSetRef.version must be default)' }
  }
  const catalog = loadCatalog({ ...request.testSetRef, projectRef: launchRefId(request.projectRef), taskRef: launchRefId(request.taskRef) })
  if (catalog === null) return { ok: false, status: 400, code: 'unknown-catalog-reference', message: 'test set or task reference is unavailable' }
  const existing = repository.findInstanceByIdempotencyKey(request.idempotencyKey)
  if (existing !== null && existing.instance.status !== 'partial') return { ok: true, idempotent: true, instance: existing.instance, ack: toLaunchAck(existing.instance) }

  const launchId = existing?.instance?.launchId ?? newLaunchId()
  const instanceId = existing?.instance?.instanceId ?? newInstanceId()
  // Resolve the exact catalog task before any external side effect. A logical
  // project reference is metadata; it is never sent to Host path APIs.
  const taskRef = launchRefId(request.taskRef)
  const taskRecord = request.taskId
    ? repository.get(request.taskId)
    : repository.get(taskRef)
  if (taskRecord !== null) {
    const storedProject = taskRecord.projectRef?.id ?? taskRecord.projectRef?.path ?? null
    if (storedProject !== null && storedProject !== launchRefId(request.projectRef)) {
      return { ok: false, status: 409, code: 'task-project-mismatch', message: 'task reference belongs to a different logical project' }
    }
  }
  let taskId = taskRecord?.taskId
  if (!taskId) {
    try {
      taskId = repository.create({ taskId: taskRef, title: request.taskRef.label ?? taskRef, requirement: request.taskRef.label ?? taskRef, projectRef: { id: launchRefId(request.projectRef) }, status: 'active' }).taskId
    } catch (error) {
      return { ok: false, status: 500, code: 'task-create-failed', message: String(error?.message ?? error) }
    }
  }
  let stored = existing?.instance ?? null
  if (!stored) {
    try {
      stored = repository.addInstance(taskId, {
        instanceId, launchId, idempotencyKey: request.idempotencyKey,
        projectRef: launchRefId(request.projectRef), testSetRef: launchRefId(request.testSetRef), testSetVersion: request.testSetRef.version, taskRef,
        correlationId: request.correlationId, launchRequestId: request.launchRequestId,
        metadata: request.metadata ?? {}, metadataClaims: Array.isArray(request.metadata) ? request.metadata : [],
        ...(request.environmentIntent && typeof request.environmentIntent === 'object' ? { environmentIntent: request.environmentIntent } : {}),
        launchStages: [launchStage({ stage: 'requested', source: 'task-launch', authority: 'task-launch', valueOrigin: 'configured' })],
        status: 'partial', startedAt: nowIso(), runtimeStatus: 'pending',
      })
    } catch (error) {
      return { ok: false, status: 500, code: 'instance-store-failed', message: 'launch instance could not be persisted before Runtime call' }
    }
  }

  const runtime = ctx.get('uniclawRuntime')
  const canRecoverUnknown = existing?.instance?.runtimeStatus === 'unknown'
    && (typeof runtime?.recoverRun === 'function' || typeof runtime?.getRunByLaunchId === 'function')
  if (runtime === undefined || runtime === null
    || (typeof runtime.createRun !== 'function' && !canRecoverUnknown)) {
    return { ok: false, status: 500, code: 'runtime-unavailable', message: 'UniClaw Runtime run seam is unavailable' }
  }
  let run
  if (existing?.instance?.runId) run = { runId: existing.instance.runId, productSessionId: existing.instance.productSessionId }
  if (!run && existing?.instance?.runtimeStatus === 'unknown') {
    const recover = typeof runtime.recoverRun === 'function' ? runtime.recoverRun
      : (typeof runtime.getRunByLaunchId === 'function' ? runtime.getRunByLaunchId : null)
    if (recover === null) return { ok: false, status: 409, code: 'run-outcome-unknown', message: 'Runtime run outcome is unknown; recovery seam is unavailable', partial: existing.instance }
    try {
      run = await recover({ launchId, idempotencyKey: request.idempotencyKey })
    } catch (error) {
      return { ok: false, status: 409, code: 'run-outcome-unknown', message: String(error?.message ?? error), partial: existing.instance }
    }
  }
  try {
    if (!run) run = await runtime.createRun({ projectRef: request.projectRef, testSetRef: request.testSetRef, taskRef: request.taskRef, correlationId: request.correlationId, idempotencyKey: request.idempotencyKey, launchId, ...(request.environmentIntent ? { environmentIntent: request.environmentIntent } : {}) })
  } catch (error) {
    const code = typeof error?.code === 'string' && error.code.length > 0 ? error.code : 'run-create-failed'
    try { repository.updateInstance(taskId, instanceId, { runtimeStatus: 'unknown', recovery: { code: 'run-create-unknown', message: String(error?.message ?? error) } }) } catch { /* preserve original error */ }
    return { ok: false, status: code === 'runtime-unavailable' ? 503 : 500, code, message: String(error?.message ?? error) }
  }
  const runId = typeof run?.runId === 'string' ? run.runId : ''
  if (runId.length === 0) {
    try { repository.updateInstance(taskId, instanceId, { runtimeStatus: 'unknown', recovery: { code: 'run-create-unknown', message: 'Runtime did not return runId' } }) } catch { /* preserve original failure */ }
    return { ok: false, status: 409, code: 'run-outcome-unknown', message: 'Runtime did not return runId; launch is retained for recovery', partial: repository.get(taskId)?.instances?.find(item => item.instanceId === instanceId) }
  }
  const productSessionId = typeof run.productSessionId === 'string' ? run.productSessionId : null

  const base = {
    instanceId, launchId, idempotencyKey: request.idempotencyKey,
    projectRef: launchRefId(request.projectRef), testSetRef: launchRefId(request.testSetRef), testSetVersion: request.testSetRef.version, taskRef: launchRefId(request.taskRef),
    correlationId: request.correlationId, launchRequestId: request.launchRequestId, runId,
    ...(productSessionId === null ? {} : { productSessionId }),
    runIdMetadata: { value: runId, valueOrigin: 'generated', availability: 'present', source: 'uniclaw-runtime', authority: 'uniclaw-runtime' },
    metadata: request.metadata ?? {},
    metadataClaims: Array.isArray(request.metadata) ? request.metadata : [],
    ...(request.environmentIntent && typeof request.environmentIntent === 'object' ? { environmentIntent: request.environmentIntent } : {}),
    launchStages: [launchStage({ stage: 'requested', source: 'task-launch', authority: 'task-launch', valueOrigin: 'configured' })],
    status: 'partial', startedAt: new Date().toISOString(),
  }
  try {
    stored = repository.updateInstance(taskId, instanceId, { ...base, runtimeStatus: 'succeeded' })
  } catch {
    return { ok: false, status: 500, code: 'instance-store-failed', message: `run ${runId} created but task instance update failed`, partial: { launchId, runId, status: 'partial' } }
  }

  if (productSessionId === null) {
    stored = repository.updateInstance(taskId, instanceId, {
      ...base, runtimeStatus: 'succeeded', status: 'partial',
      launchStages: [...(stored?.launchStages ?? base.launchStages), launchStage({ stage: 'failed', status: 'partial', source: 'uniclaw-runtime', authority: 'uniclaw-runtime', valueOrigin: 'observed', availability: 'not-associated', taskInstanceId: instanceId, runId, diagnostic: { code: 'product-session-unavailable', message: 'Runtime did not return productSessionId' } })],
    })
    return { ok: false, status: 409, code: 'product-session-unavailable', message: 'Runtime did not return productSessionId', partial: stored }
  }
  const controller = ctx.get('sessionController')
  const registry = ctx.get('workspaceRegistry')
  try {
    if (!registry) throw new Error('workspace registry unavailable')
    if (typeof registry.resolveProjectToWorkspace !== 'function') throw new Error('logical project resolver unavailable')
    const workspace = await registry.resolveProjectToWorkspace(launchRefId(request.projectRef))
    const workspaceId = workspace?.id
    if (typeof workspaceId !== 'string' || workspaceId.length === 0) throw new Error('logical project resolver returned no workspace')
    const created = await controller.create({ agentPreset: AGENT_PRESET_ID, workspaceId, launchId, idempotencyKey: request.idempotencyKey })
    const sessionId = typeof created === 'string' ? created : (created?.sessionId ?? created?.id)
    if (typeof sessionId !== 'string' || sessionId.length === 0) throw new Error('Host did not return session identity')
    const namespace = stored.storageNamespace ?? { schemaVersion: 'uniclaw.workspace.local-storage-namespace.v1', contractVersion: 'uniclaw.workspace.contract.v1', namespaceRef: { id: `task-instance-${instanceId}`, storageKind: 'local-filesystem', adapter: 'task-repository' }, taskInstanceId: instanceId, runId, status: 'ready', createdAt: nowIso(), revision: '1' }
    stored = repository.updateInstance(taskId, instanceId, {
      sessionId, productSessionId, hostSessionRef: sessionId,
      storageNamespaceRef: namespace.namespaceRef.id,
      storageNamespace: namespace,
      status: 'active',
      launchStages: [...(stored?.launchStages ?? base.launchStages), launchStage({ stage: 'instance-created', source: 'task-repository', authority: 'uniclaw-runtime', valueOrigin: 'generated', taskInstanceId: instanceId }), launchStage({ stage: 'run-bound', source: 'uniclaw-runtime', authority: 'uniclaw-runtime', valueOrigin: 'generated', taskInstanceId: instanceId, runId }), launchStage({ stage: 'host-session-bound', source: 'dsh-session-controller', authority: 'dsh-host', valueOrigin: 'generated', taskInstanceId: instanceId }), launchStage({ stage: 'completed', source: 'task-launch', authority: 'uniclaw-runtime', valueOrigin: 'derived', taskInstanceId: instanceId })],
    })
    return { ok: true, instance: stored, ack: toLaunchAck(stored) }
  } catch (error) {
    const message = String(error?.message ?? error)
    try { stored = repository.updateInstance(taskId, instanceId, { status: 'partial', recovery: { code: 'host-session-or-storage-failed', message }, launchStages: [...(stored?.launchStages ?? base.launchStages), launchStage({ stage: 'failed', status: 'partial', source: 'task-launch', authority: 'uniclaw-runtime', valueOrigin: 'derived', availability: 'unavailable', taskInstanceId: instanceId, diagnostic: { code: 'launch-partial', message } })] }) } catch { /* preserve original failure */ }
    return { ok: false, status: 500, code: 'launch-partial', message, partial: stored ?? base }
  }
}

/** Project persisted launch state to the versioned write-ack contract. The
 * instance keeps legacy string refs for old callers; the ack exposes typed
 * binding refs required by the workspace schemas. */
const toLaunchAck = (instance) => ({
  schemaVersion: 'uniclaw.workspace.task-launch-ack.v1',
  contractVersion: 'uniclaw.workspace.contract.v1',
  launchRequestId: instance.launchRequestId,
  launchId: instance.launchId,
  taskInstanceId: instance.instanceId,
  status: instance.status === 'partial' ? 'partial' : (instance.status === 'failed' ? 'failed' : 'completed'),
  stages: Array.isArray(instance.launchStages) ? instance.launchStages : [],
  correlationId: instance.correlationId,
  acknowledgedAt: nowIso(),
  bindings: {
    ...(typeof instance.productSessionId === 'string' ? { productSessionId: instance.productSessionId } : {}),
    ...(typeof instance.runId === 'string' ? { runId: instance.runId } : {}),
    ...(typeof instance.hostSessionRef === 'string' ? { hostSessionRef: { host: 'dsh', sessionId: instance.hostSessionRef } } : {}),
    ...(instance.storageNamespace?.namespaceRef ? { storageNamespaceRef: instance.storageNamespace.namespaceRef } : {}),
  },
})

// ---------------------------------------------------------------------------
// Product Workspace panel service. Plain functions first,
// thin Typert decoration second: the six methods are testable without the
// typert-protocol package, which is absent from the offline test environment.
// ---------------------------------------------------------------------------

/** Frozen diagnostic skill enum (决策 9) — nothing outside this list injects. */
const DIAGNOSTIC_SKILLS = ['diagnosing-bugs', 'uniclaw-debug-evidence']

/** 决策 9: the diagnostic prompt template is frozen in the host; the client
 * may choose the skill, never the prompt text. */
const diagnosticPrompt = (skill, task, instance) => [
  `You are running the frozen UniClaw diagnostic skill "${skill}" for task-workbench instance ${instance.instanceId}.`,
  `Task ${task.taskId} "${task.title}"; DSH session ${instance.sessionId}.`,
  'Collect runtime evidence per the skill (Expected/Observed/Gap with concrete evidence levels) and report findings; do not modify code in this turn.',
].join('\n')

/** Hard truncation for extracted semantic entry text (same cap as the old
 * physical projection — large payloads never pass through whole). */
const SUMMARY_LIMIT = 200
const ARTIFACT_DETAIL_LIMIT = 64_000

// ---------------------------------------------------------------------------
// UniClaw semantic trace extraction. The extractor ONLY reads event text
// (forbidden: any data not sourced from an event). A hit per match:
//   gate     — a UniFlow stay/escalate gate token (STAY_IN_*)
//   outcome  — a UniFlow outcome token (the rest of the frozen list)
//   evidence — an evidence/ path reference
// ---------------------------------------------------------------------------

const GATE_TOKENS = ['STAY_IN_EXPLORE', 'STAY_IN_DIAGNOSIS']
const OUTCOME_TOKENS = ['EXPLORE_RESOLVED', 'EXECUTION_READY', 'VERIFIED', 'VERIFICATION_FAILED', 'COMPLETE']
const TOKEN_RE = new RegExp(`(${[...GATE_TOKENS, ...OUTCOME_TOKENS].join('|')})`, 'g')
const EVIDENCE_RE = /(evidence\/[A-Za-z0-9._\-/]+)/g

const contentText = (content) => Array.isArray(content)
  ? content.flatMap(block => {
    if (block?.type === 'text' && typeof block.text === 'string') return [block.text]
    if (block?.type === 'tool-call') return [block.name, block.arguments]
    return [] // reasoning blocks are deliberately excluded from the workspace
  }).filter(Boolean).join('\n') : ''

const eventText = (event) => {
  const data = event === null || typeof event !== 'object' ? event : (event.data ?? event.text)
  if (typeof data === 'string') return data
  if (event?.type === 'user/message') return contentText(data?.content)
  if (event?.type === 'assistant/message') return contentText(data?.message?.content)
  if (event?.type === 'tool/call') return [data?.name, data?.arguments, data?.output].filter(Boolean).map(value => typeof value === 'string' ? value : JSON.stringify(value)).join('\n')
  if (event?.type === 'tool/result') return [contentText(data?.message?.content), data?.error?.name, data?.error?.code].filter(Boolean).join('\n')
  if (event?.type === 'todo/write') return Array.isArray(data?.todos) ? data.todos.map(todo => `${todo.status}: ${todo.content}`).join('\n') : ''
  if (event?.type === 'turn/end') return data?.reason?.kind === 'error' ? data.reason.error?.message ?? 'error' : data?.reason?.kind ?? ''
  return JSON.stringify(data ?? '')
}

const eventTs = (event) => {
  if (event !== null && typeof event === 'object') {
    if (typeof event.ts === 'string') return event.ts
    if (typeof event.timestamp === 'string') return event.timestamp
    // dsh-session events carry `time` as Unix epoch milliseconds.
    if (Number.isSafeInteger(event.time)) return new Date(event.time).toISOString()
  }
  return ''
}

/** Extract semantic entries from one event's text. evidenceRefs on gate/outcome
 * entries carry the evidence paths found in the SAME event text (zero-fabrication). */
const extractEntries = (event) => {
  const text = eventText(event)
  if (text.length === 0) return []
  const ts = eventTs(event)
  const refs = [...text.matchAll(EVIDENCE_RE)].map(m => m[1])
  const entries = []
  for (const match of text.matchAll(TOKEN_RE)) {
    entries.push({
      ts,
      kind: GATE_TOKENS.includes(match[1]) ? 'gate' : 'outcome',
      token: match[1],
      text: text.slice(0, SUMMARY_LIMIT),
      evidenceRefs: refs,
    })
  }
  for (const match of text.matchAll(EVIDENCE_RE)) {
    entries.push({ ts, kind: 'evidence', token: match[1], text: text.slice(0, SUMMARY_LIMIT), evidenceRefs: [match[1]] })
  }
  return entries
}

// Internal helper failures carry {ok:false, code, message}; the public panel
// boundary maps them to the frozen {success:false, error:{code,...}} shape.
const panelError = (code, message) => ({ ok: false, code, message })
const panelFail = (internal) => ({ success: false, error: { code: internal.code, message: internal.message } })

/**
 * Shared instantiate core (决策 1/4/6): used by BOTH the HTTP route and the
 * panel service, so the two protocols cannot drift. Returns
 * {ok:true, instance} or {ok:false, status, code, message} — protocol shapes
 * ({ok:...} vs {success:...}) stay at each boundary.
 */
const instantiateTask = async (ctx, repository, taskId) => {
  const task = repository.get(taskId)
  if (task === null) return { ok: false, status: 404, code: 'task-not-found', message: `unknown taskId: ${taskId}` }
  // Only active definitions instantiate; draft/archived fail closed.
  if (task.status !== 'active') {
    return { ok: false, status: 409, code: 'task-not-active', message: `taskId ${task.taskId} is ${task.status}, not active` }
  }

  // 决策 4: the Task Project IS a DSH workspace. projectRef.path resolves
  // through the registry (same source as workspaceRegistry.list()); an
  // unseen path is registered on the spot.
  const controller = ctx.get('sessionController')
  const workspaceRegistry = ctx.get('workspaceRegistry')
  let workspaceId
  if (task.projectRef?.path !== undefined) {
    try {
      if (typeof workspaceRegistry?.resolveByPath !== 'function'
        || typeof workspaceRegistry?.create !== 'function') {
        return { ok: false, status: 500, code: 'workspace-resolve-failed', message: 'the host workspace registry is not available to this plugin' }
      }
      const resolved = await workspaceRegistry.resolveByPath(task.projectRef.path)
      workspaceId = resolved?.id ?? (await workspaceRegistry.create(task.projectRef.path)).id
    } catch (error) {
      return { ok: false, status: 500, code: 'workspace-resolve-failed', message: String(error && error.message ? error.message : error) }
    }
  }
  if (typeof workspaceId !== 'string' || workspaceId.length === 0) {
    return { ok: false, status: 500, code: 'workspace-resolve-failed', message: 'no workspace resolved for this task' }
  }

  // The session MUST run under the uniagent-task preset (决策 6).
  let sessionId
  try {
    if (typeof controller?.create !== 'function') {
      return { ok: false, status: 500, code: 'session-create-failed', message: 'the host session controller is not available to this plugin' }
    }
    const created = await controller.create({ agentPreset: AGENT_PRESET_ID, workspaceId })
    sessionId = typeof created === 'string' ? created : (created && (created.sessionId ?? created.id))
    if (typeof sessionId !== 'string' || sessionId.length === 0) {
      return { ok: false, status: 500, code: 'session-create-failed', message: `unexpected session identity: ${JSON.stringify(created).slice(0, 120)}` }
    }
  } catch (error) {
    return { ok: false, status: 500, code: 'session-create-failed', message: String(error && error.message ? error.message : error) }
  }

  const instance = {
    instanceId: newInstanceId(),
    sessionId,
    status: 'active',
    startedAt: new Date().toISOString(),
  }
  try {
    return { ok: true, instance: repository.addInstance(task.taskId, instance) }
  } catch (error) {
    return { ok: false, status: 500, code: 'store-corrupted', message: String(error && error.message ? error.message : error) }
  }
}

/** sessionQuery seam (决策 7): trace events come from the host's
 * sessionQuery.readSession — listEvents returns payload-less records
 * ({sessionId,seq,type,time,surface}) with no text to extract from, so the
 * full log snapshot is the minimal real source. Missing seam fails closed. */
const listSessionEvents = async (ctx, sessionId) => {
  const sessionQuery = ctx.get('sessionQuery')
  if (sessionQuery === undefined || sessionQuery === null
    || typeof sessionQuery.readSession !== 'function') {
    return panelError('session-query-unavailable', 'the host sessionQuery service is not available to this plugin')
  }
  try {
    const snapshot = await sessionQuery.readSession(sessionId)
    const events = snapshot !== null && typeof snapshot === 'object' && Array.isArray(snapshot.events)
      ? snapshot.events
      : []
    const session = snapshot !== null && typeof snapshot === 'object' && snapshot.session && typeof snapshot.session === 'object'
      ? snapshot.session
      : null
    return { ok: true, events, session }
  } catch (error) {
    return panelError('session-query-failed', String(error && error.message ? error.message : error))
  }
}

/** sessions() compatibility seam: list host sessions with title + UniClaw marker. */
const listSessionsCore = async (ctx, repository) => {
  const sessionQuery = ctx.get('sessionQuery')
  if (sessionQuery === undefined || sessionQuery === null
    || typeof sessionQuery.listSessions !== 'function') {
    return panelError('session-query-unavailable', 'the host sessionQuery service is not available to this plugin')
  }
  let sessions
  try {
    sessions = await sessionQuery.listSessions()
  } catch (error) {
    return panelError('session-query-failed', String(error && error.message ? error.message : error))
  }
  // Real shapes (dsh-session-query/dsh-session): listSessions() returns
  // SessionRecord[] = { header: { id, createdAt(epoch ms), cwd?, ... }, live,
  // persisted }; titles come from readTitleSnapshots(ids) → per-id results
  // {sessionId, status:'fulfilled', value:{session, title?{title}}}.
  const records = (Array.isArray(sessions) ? sessions : [])
    .filter(r => r !== null && typeof r === 'object' && r.header && typeof r.header.id === 'string')
  let titleById = new Map()
  if (records.length > 0 && typeof sessionQuery.readTitleSnapshots === 'function') {
    try {
      const results = await sessionQuery.readTitleSnapshots(records.map(r => r.header.id))
      for (const item of Array.isArray(results) ? results : []) {
        if (item === null || typeof item !== 'object' || item.status !== 'fulfilled') continue
        const snapshot = item.value
        const title = snapshot && snapshot.title
        const text = title && typeof title.title === 'string' ? title.title
          : (typeof title === 'string' ? title : '')
        if (text.length > 0) titleById.set(item.sessionId, text)
      }
    } catch { /* titles are cosmetic; the list still ships without them */ }
  }
  // uniclaw = the session backs a recorded task instance in the repository.
  const uniclawSessions = new Set()
  for (const task of repository.list()) {
    for (const instance of Array.isArray(task.instances) ? task.instances : []) {
      if (typeof instance.sessionId === 'string') uniclawSessions.add(instance.sessionId)
    }
  }
  const mapped = records.map(record => ({
    sessionId: record.header.id,
    title: titleById.get(record.header.id) ?? '',
    lastActiveAt: null,
    createdAt: Number.isSafeInteger(record.header.createdAt) ? record.header.createdAt : null,
    live: record.live === true,
    persisted: record.persisted === true,
    cwd: typeof record.header.cwd === 'string' ? record.header.cwd : null,
    agentPreset: typeof record.header.agentPreset === 'string' ? record.header.agentPreset : null,
    uniclaw: uniclawSessions.has(record.header.id),
  }))
  return { ok: true, sessions: mapped }
}

/** Find the instance backing a sessionId — the active one wins, else any.
 * Returns {ok:true, task?, instance?}; instance is optional (arbitrary session). */
const findInstanceBySessionId = (repository, sessionId) => {
  const tasks = repository.list()
  let fallback = null
  for (const task of tasks) {
    for (const instance of Array.isArray(task.instances) ? task.instances : []) {
      if (instance.sessionId !== sessionId) continue
      if (instance.status === 'active') return { ok: true, task, instance }
      if (fallback === null) fallback = { ok: true, task, instance }
    }
  }
  return fallback ?? { ok: true }
}

/** Project the task-instance repository into the product workspace navigation.
 * A task definition without an instance is intentionally omitted: the workspace
 * answers "what has run", while task creation/instantiation remain API actions. */
const traceReferenceKinds = ['Run', 'Evidence', 'WorldRevision', 'AssociationDecision', 'Intent', 'Binding', 'AssuranceJudgment', 'Receipt', 'OutcomeProof', 'RuntimeOutcome', 'Artifact']
const structuralOutcomes = ['Completed', 'Faulted', 'Cancelled', 'Incomplete']
const TRACE_SPAN_LIMIT = 200

const traceProjection = (trace) => (Array.isArray(trace?.spans) ? trace.spans.slice(0, TRACE_SPAN_LIMIT) : []).map(span => {
  const structuralOutcome = structuralOutcomes[span.structuralOutcome] ?? String(span.structuralOutcome ?? 'Unknown')
  const references = (span.references ?? []).map(ref => ({
    kind: traceReferenceKinds[ref.kind] ?? String(ref.kind ?? 'Unknown'),
    value: ref.value,
  }))
  const events = (span.events ?? []).map(event => ({
    eventId: event.eventId,
    timestamp: event.timestamp ?? event.ts ?? null,
    reasonCode: event.reasonCode ?? null,
    attributes: event.attributes ?? null,
    references: (event.references ?? []).map(ref => ({ kind: traceReferenceKinds[ref.kind] ?? String(ref.kind ?? 'Unknown'), value: ref.value })),
  }))
  const definition = span.spanDefinitionId || span.spanId || 'span'
  const facts = [
    `outcome=${structuralOutcome}`,
    references.length > 0 ? `references=${references.map(ref => `${ref.kind}:${ref.value}`).join(', ')}` : '',
    events.length > 0 ? `events=${events.map(event => event.eventId).join(', ')}` : '',
  ].filter(Boolean)
  return {
    kind: 'span',
    spanId: span.spanId,
    parentSpanId: span.parentSpanId ?? null,
    definition,
    label: definition,
    summary: `${definition} · ${structuralOutcome}`,
    text: facts.join(' · '),
    structuralOutcome,
    traceId: trace?.traceId ?? null,
    spanKind: span.spanKind ?? span.kind ?? null,
    status: span.status?.code ?? span.status ?? null,
    startTime: span.startTime ?? span.startTs ?? null,
    endTime: span.endTime ?? span.endTs ?? null,
    durationMs: Number.isFinite(span.durationMs) ? span.durationMs : null,
    attributes: span.attributes ?? null,
    resource: span.resource ?? null,
    links: Array.isArray(span.links) ? span.links : [],
    captureSequence: span.captureSequence,
    references,
    events,
  }
})

const workspaceCore = (repository, artifactSource, launchDefaults = DEFAULT_LOCAL_LAUNCH_DEFAULTS, localConfig = DEFAULT_LOCAL_CONFIG) => {
  const projectMap = new Map()
  const sessionIds = new Set()
  for (const task of repository.list()) {
    const path = typeof task.projectRef?.path === 'string' && task.projectRef.path.length > 0
      ? task.projectRef.path : '未指定项目'
    const projectId = path
    if (!projectMap.has(projectId)) {
      projectMap.set(projectId, {
        projectId,
        name: path === '未指定项目' ? path : path.split('/').filter(Boolean).pop() || path,
        path: path === '未指定项目' ? null : path,
        instances: [],
      })
    }
    const project = projectMap.get(projectId)
    for (const instance of Array.isArray(task.instances) ? task.instances : []) {
      if (typeof instance?.sessionId !== 'string' || instance.sessionId.length === 0) continue
      sessionIds.add(instance.sessionId)
      project.instances.push({
        instanceId: instance.instanceId,
        sessionId: instance.sessionId,
        ...(typeof instance.productSessionId === 'string' && instance.productSessionId.length > 0
          ? { productSessionId: instance.productSessionId, correlationStatus: 'correlated' }
          : { correlationStatus: 'uncorrelated' }),
        status: instance.status,
        startedAt: instance.startedAt,
        endedAt: instance.endedAt ?? null,
        projectRef: instance.projectRef ?? task.projectRef ?? null,
        testSetRef: instance.testSetRef ?? task.testSetRef ?? null,
        taskRef: instance.taskRef ?? task.taskRef ?? null,
        runId: instance.runId ?? null,
        launchStages: Array.isArray(instance.launchStages) ? instance.launchStages : [],
        storageNamespaceRef: instance.storageNamespaceRef ?? null,
        metadataClaims: Array.isArray(instance.metadataClaims) ? instance.metadataClaims : [],
        task: {
          taskId: task.taskId,
          title: task.title,
          requirement: task.requirement,
          status: task.status,
          createdAt: task.createdAt,
          projectRef: task.projectRef ?? null,
        },
      })
    }
  }
  const artifactIndex = artifactSource?.scan?.() ?? { runs: [], warnings: [] }
  for (const run of artifactIndex.runs) {
    const metadata = run.metadata
    const sessionId = metadata.dshSessionId
    if (sessionIds.has(sessionId)) continue
    const projectName = typeof metadata.workspace === 'string' && metadata.workspace.length > 0 ? metadata.workspace : '已发现运行产物'
    const projectId = `artifact:${projectName}`
    if (!projectMap.has(projectId)) projectMap.set(projectId, { projectId, name: projectName, path: null, instances: [] })
    projectMap.get(projectId).instances.push({
      instanceId: `observed:${sessionId}`,
      sessionId,
      ...(typeof metadata.productSessionId === 'string' && metadata.productSessionId.length > 0
        ? { productSessionId: metadata.productSessionId, correlationStatus: 'correlated' }
        : { correlationStatus: 'uncorrelated' }),
      status: typeof metadata.status === 'string' ? metadata.status : 'observed',
      startedAt: null,
      endedAt: null,
      observed: true,
      task: {
        taskId: typeof metadata.productSessionId === 'string' ? metadata.productSessionId : `observed:${sessionId}`,
        title: typeof metadata.productSessionTitle === 'string' ? metadata.productSessionTitle : sessionId,
        requirement: null,
        status: 'observed',
        createdAt: null,
        projectRef: { name: projectName },
      },
    })
  }
  return { success: true, projects: [...projectMap.values()].filter(p => p.instances.length > 0), launchDefaults, localConfig, artifactWarnings: artifactIndex.warnings }
}

const DSH_TRACE_LABELS = {
  'user/message': '用户消息', 'assistant/message': '模型消息', 'tool/call': '能力调用',
  'tool/result': '能力结果', 'turn/start': '回合开始', 'turn/end': '回合结束',
  'step/start': '步骤开始', 'step/end': '步骤结束', 'system/message': '系统消息',
}
const dshEventProjection = (event) => {
  const type = typeof event?.type === 'string' ? event.type : 'event'
  const raw = eventText(event).trim()
  const detail = raw.replace(/\s+/g, ' ').slice(0, 220)
  return {
  seq: Number.isSafeInteger(event?.seq) ? event.seq : null,
  ts: eventTs(event),
  type,
  source: 'dsh',
  label: DSH_TRACE_LABELS[type] || type,
  summary: detail ? `${DSH_TRACE_LABELS[type] || type} · ${detail}` : (DSH_TRACE_LABELS[type] || type),
  text: raw.slice(0, 1600),
  role: typeof event?.role === 'string' ? event.role : null,
  }
}

const UNI_AGENT_EVENT_KINDS = {
  'user/message': { kind: 'user', role: 'user', label: '任务输入' },
  'assistant/message': { kind: 'assistant', role: 'assistant', label: 'Uni-Agent' },
  'agent/message': { kind: 'assistant', role: 'assistant', label: 'Uni-Agent' },
  'tool/call': { kind: 'tool-call', role: 'tool', label: '能力调用' },
  'tool/result': { kind: 'tool-result', role: 'tool', label: '能力结果' },
  'todo/write': { kind: 'plan', role: 'assistant', label: '执行计划' },
  'turn/end': { kind: 'turn-end', role: 'system', label: '回合结束' },
}

const jsonSuffix = (text, marker) => {
  const start = text.indexOf(marker)
  if (start < 0) return null
  const jsonStart = text.indexOf('{', start + marker.length)
  if (jsonStart < 0) return null
  try { return JSON.parse(text.slice(jsonStart).trim()) } catch { return null }
}

const decisionContextFromEvent = (event) => {
  if (event?.type !== 'user/message' || event?.data?.source?.kind === 'runtime-context') return null
  return jsonSuffix(eventText(event), '=== AgentDecisionContext (JSON) ===')
}

const toolCallFromEvent = (event) => {
  if (event?.type === 'tool/call') {
    const data = event.data ?? {}
    return { callId: data.callId ?? data.id ?? null, name: data.name ?? '', arguments: data.arguments ?? data.input ?? '' }
  }
  if (event?.type !== 'assistant/message') return null
  const blocks = event.data?.message?.content
  const block = Array.isArray(blocks) ? blocks.find(item => item?.type === 'tool-call') : null
  return block === undefined || block === null ? null : { callId: block.id ?? null, name: block.name ?? '', arguments: block.arguments ?? '' }
}

const toolResultFromEvent = (event) => {
  if (event?.type !== 'tool/result') return null
  const data = event.data ?? {}
  return {
    callId: data.toolCallId ?? data.callId ?? data.message?.toolCallId ?? null,
    text: eventText(event),
    isError: data.isError === true || data.error !== undefined,
    seq: event.seq ?? null,
    ts: eventTs(event),
  }
}

const decisionFromCall = (call) => {
  if (call?.name !== 'submit_decision' || typeof call.arguments !== 'string') return null
  try {
    const parsed = JSON.parse(call.arguments)
    const decision = parsed.decision ?? parsed
    const proposal = decision.proposal ?? null
    return {
      decisionId: decision.decisionId ?? proposal?.decisionId ?? null,
      kind: decision.kind ?? null,
      justification: proposal?.justification ?? null,
      steps: Array.isArray(proposal?.steps) ? proposal.steps : [],
      spec: decision.spec ?? null,
      raw: call.arguments,
    }
  } catch { return { raw: call.arguments } }
}

/** Product-facing Uni-Agent timeline. Requests are extracted from the actual
 * AgentDecisionContext; decision calls and results are correlated by callId.
 * Host lifecycle, prompt injection, permission and model setup stay in dshTrace. */
const uniAgentProjection = (events) => {
  const resultsByCall = new Map()
  const callsByCall = new Map()
  const canonicalCallIds = new Set()
  for (const event of events) {
    const call = toolCallFromEvent(event)
    if (call?.callId) {
      callsByCall.set(call.callId, call)
      if (event?.type === 'tool/call') canonicalCallIds.add(call.callId)
    }
    const result = toolResultFromEvent(event)
    if (result?.callId) resultsByCall.set(result.callId, result)
  }
  const cards = []
  for (const event of events) {
    const context = decisionContextFromEvent(event)
    if (context !== null) {
      cards.push({
        seq: event.seq ?? null, ts: eventTs(event), type: event.type, kind: 'request', role: 'request', label: '调用方请求',
        decisionId: context.decisionId ?? null, objective: context.objective ?? null, phase: context.phase ?? null,
        screen: context.screen ?? null, budgetRemaining: context.budgetRemaining ?? null, runId: context.runId ?? null,
      })
      continue
    }
    if (event?.type === 'user/message' && event?.data?.source?.kind === 'runtime-context') continue
    const call = toolCallFromEvent(event)
    // DSH records a submit_decision twice: once inside the assistant message
    // and once as the canonical tool/call record. Render one product card.
    if (event?.type === 'assistant/message' && call?.callId && canonicalCallIds.has(call.callId)) continue
    const decision = decisionFromCall(call)
    if (decision !== null) {
      const result = call?.callId ? resultsByCall.get(call.callId) : null
      const { kind: decisionKind, ...decisionFields } = decision
      cards.push({
        seq: event.seq ?? null, ts: eventTs(event), type: event.type, kind: 'decision', role: 'assistant', label: 'Uni-Agent 决策',
        callId: call?.callId ?? null, decisionKind: decisionKind ?? null, ...decisionFields,
        result: result ? { text: result.text, isError: result.isError, seq: result.seq ?? null, ts: result.ts ?? '' } : null,
      })
      continue
    }
    // assistant tool-call is duplicated by the subsequent tool/call record.
    if (event?.type === 'assistant/message' && call !== null) continue
    const result = toolResultFromEvent(event)
    if (result !== null && result.callId && callsByCall.has(result.callId)) continue
    const shape = UNI_AGENT_EVENT_KINDS[event?.type]
    if (shape === undefined) continue
    const text = eventText(event)
    if (event.type === 'assistant/message' && /^Decision submitted:/.test(text.trim())) continue
    if (event.type === 'user/message') {
      cards.push({ seq: event.seq ?? null, ts: eventTs(event), type: event.type, kind: 'user', role: 'user', label: '任务输入', text: text.slice(0, 1600) })
    } else if (event.type === 'tool/result') {
      cards.push({ seq: event.seq ?? null, ts: eventTs(event), type: event.type, kind: 'result', role: 'tool', label: '能力结果', text: text.slice(0, 1600), isError: result?.isError === true })
    } else {
      cards.push({ seq: event.seq ?? null, ts: eventTs(event), type: event.type, kind: shape.kind, role: shape.role, label: shape.label, text: text.slice(0, 1600) })
    }
  }
  return cards
}

/** Group the product-facing cards into one causal round. The flat `conversation`
 * projection remains for compatibility; this read model is the client-facing
 * shape for the product timeline: request → decision → submission. Actual
 * execution and verification are supplied by a separately correlated run
 * artifact, never inferred from DSH's accepted submit_decision response. */
const uniAgentGroupProjection = (cards) => {
  const groups = []
  let current = null
  const begin = (request = null) => {
    current = {
      groupId: `round-${groups.length + 1}`,
      round: groups.length + 1,
      request,
      decision: null,
      submission: null,
      result: null,
      end: null,
      extras: [],
    }
    groups.push(current)
  }
  for (const card of Array.isArray(cards) ? cards : []) {
    if (card.kind === 'request') {
      if (current !== null && (current.request !== null || current.decision !== null || current.extras.length > 0)) current = null
      begin(card)
      continue
    }
    if (current === null) begin(null)
    if (card.kind === 'decision') {
      current.decision = card
      if (card.result !== null && card.result !== undefined) {
        current.submission = {
          kind: 'result', role: 'tool', label: card.result.isError ? '提交失败' : '提交结果',
          seq: card.result.seq ?? card.seq, ts: card.result.ts ?? card.ts,
          text: card.result.text, isError: card.result.isError,
        }
        current.result = current.submission // compatibility alias for existing consumers
      }
    } else if (card.kind === 'result') {
      current.submission = card
      current.result = card // compatibility alias for existing consumers
    } else if (card.kind === 'turn-end') {
      current.end = card
    } else {
      current.extras.push(card)
    }
  }
  return groups.map(group => ({
    ...group,
    stages: [group.request, group.decision, group.submission].filter(Boolean),
    status: group.submission?.isError === true ? 'error' : group.submission !== null ? 'submitted' : group.end !== null ? 'completed' : 'pending',
  }))
}

/**
 * Project the real UniClaw run conclusion into explicit stages. DSH's tool
 * acknowledgement is deliberately excluded: it only proves that the
 * decision was accepted by the host. Execution needs a runtime outcome and
 * verification needs a completion anchor, coverage step, or explicit
 * UniFlow verification token from a correlated artifact/session.
 */
const runStagesProjection = (observed, uniflowTrace) => {
  if (observed === null || observed === undefined) return []
  const metadata = observed.metadata ?? {}
  const facts = observed.facts ?? {}
  const files = Array.isArray(observed.files) ? observed.files : []
  const ref = (name) => files.find(file => file.name === name)?.ref ?? null
  const stages = []
  const outcome = typeof metadata.outcome === 'string' && metadata.outcome.length > 0
    ? metadata.outcome
    : typeof facts.outcome === 'string' && facts.outcome.length > 0 ? facts.outcome : null
  if (outcome !== null) {
    stages.push({
      kind: 'execution', label: '执行结果', status: outcome,
      source: 'UniClaw runtime artifact', text: typeof facts.reason === 'string' && facts.reason.length > 0 ? facts.reason : `RuntimeOutcome: ${outcome}`,
      evidenceRefs: [ref('facts.json') ?? ref('metadata.json')].filter(Boolean),
    })
  }

  const anchors = Array.isArray(facts.completionAnchors) ? facts.completionAnchors : []
  const coverage = Array.isArray(observed.coverageSteps) ? observed.coverageSteps : []
  const verifiedToken = (Array.isArray(uniflowTrace) ? uniflowTrace : []).find(entry => entry.token === 'VERIFIED' || entry.token === 'VERIFICATION_FAILED')
  let verification = null
  if (anchors.length > 0) {
    const passed = anchors.every(anchor => anchor?.verified === true)
    verification = { status: passed ? '已验证' : '验证失败', text: `完成锚点 ${anchors.filter(anchor => anchor?.verified === true).length}/${anchors.length}`, source: 'facts.json', evidenceRefs: [ref('facts.json')].filter(Boolean) }
  } else if (coverage.length > 0) {
    const passed = coverage.every(step => step?.verified === true)
    verification = { status: passed ? '已验证' : '验证失败', text: `覆盖步骤 ${coverage.filter(step => step?.verified === true).length}/${coverage.length}`, source: 'coverage-steps.json', evidenceRefs: [ref('coverage-steps.json')].filter(Boolean) }
  } else if (verifiedToken !== undefined) {
    verification = { status: verifiedToken.token === 'VERIFIED' ? '已验证' : '验证失败', text: verifiedToken.text, source: 'UniFlow trace', evidenceRefs: verifiedToken.evidenceRefs ?? [] }
  }
  if (verification !== null) stages.push({ kind: 'verification', label: '验证结果', ...verification })
  return stages
}

/** Read-only detail projection for one Task Instance. All four panes use the
 * same readSession snapshot, so the client cannot accidentally mix sessions. */
const sessionDetailCore = async (ctx, repository, artifactSource, sessionId) => {
  if (typeof sessionId !== 'string' || sessionId.length === 0) {
    return panelError('invalid-request', 'session requires a sessionId string')
  }
  const resolved = findInstanceBySessionId(repository, sessionId)
  const artifactIndex = artifactSource?.scan?.() ?? { runs: [], warnings: [] }
  const observed = artifactIndex.runs.find(run => run.metadata?.dshSessionId === sessionId)
  const observedResolved = observed === undefined ? null : {
    task: {
      taskId: typeof observed.metadata.productSessionId === 'string' ? observed.metadata.productSessionId : `observed:${sessionId}`,
      title: observed.metadata.productSessionTitle ?? sessionId,
      requirement: null,
      status: 'observed',
      createdAt: null,
      projectRef: { name: observed.metadata.workspace ?? '已发现运行产物' },
    },
    instance: {
      instanceId: `observed:${sessionId}`,
      sessionId,
      status: observed.metadata.status ?? 'observed',
      startedAt: null,
      endedAt: null,
      observed: true,
    },
  }
  const selected = resolved.instance === undefined ? observedResolved : resolved
  if (selected === null || selected.instance === undefined) {
    return panelError('task-instance-not-found', `no UniClaw task instance is associated with session ${sessionId}`)
  }
  const events = await listSessionEvents(ctx, sessionId)
  if (!events.ok) return events
  // The event read already returned the exact session header. Avoid calling
  // listSessions()/readTitleSnapshots() here: those methods scan and title-fold
  // the entire persisted corpus, which makes one task card depend on unrelated
  // sessions and turns a cold real read into a timeout.
  const sessionHeader = events.session
  const metadata = {
    sessionId,
    title: '',
    lastActiveAt: null,
    createdAt: Number.isSafeInteger(sessionHeader?.createdAt) ? sessionHeader.createdAt : null,
    live: false,
    persisted: true,
    cwd: typeof sessionHeader?.cwd === 'string' ? sessionHeader.cwd : null,
    agentPreset: typeof sessionHeader?.agentPreset === 'string' ? sessionHeader.agentPreset : null,
  }
  if (observed?.metadata) {
    const artifactMetadata = observed.metadata
    metadata.device = artifactMetadata.device ?? null
    metadata.androidApi = artifactMetadata.androidApi ?? null
    metadata.wmSize = artifactMetadata.wmSize ?? null
    metadata.dshEndpoint = artifactMetadata.dshEndpoint ?? null
    metadata.taskSet = artifactMetadata.taskSet ?? null
    metadata.real = artifactMetadata.real === true
    metadata.deliveredEffects = artifactMetadata.deliveredEffects ?? null
    metadata.consultations = artifactMetadata.consultations ?? null
    metadata.artifact = {
      productSessionId: artifactMetadata.productSessionId ?? null,
      productSessionTitle: artifactMetadata.productSessionTitle ?? null,
      workspace: artifactMetadata.workspace ?? null,
      productModel: artifactMetadata.productModel ?? null,
      outcome: artifactMetadata.outcome ?? null,
      status: artifactMetadata.status ?? null,
      runDir: artifactMetadata.runDir ?? null,
    }
  }
  const dshTrace = events.events.map(dshEventProjection)
  const conversation = uniAgentProjection(events.events)
  const conversationGroups = uniAgentGroupProjection(conversation)
  const uniflowTrace = events.events.flatMap(extractEntries)
  const uniclawTrace = traceProjection(observed?.trace)
  const uniclawTraceContext = observed?.trace ? {
    schemaVersion: observed.trace.schemaVersion ?? null,
    traceId: observed.trace.traceId ?? null,
    rootSpanId: observed.trace.rootSpanId ?? null,
    runId: observed.trace.runId ?? null,
    spanCount: Array.isArray(observed.trace.spans) ? observed.trace.spans.length : 0,
    recorderTerminal: observed.trace.recorderTerminal ?? null,
  } : null
  const runStages = runStagesProjection(observed, uniflowTrace)
  const artifactFiles = (observed?.files ?? []).map(file => file.ref)
  const evidence = [...new Set([
    ...uniflowTrace.flatMap(e => Array.isArray(e.evidenceRefs) ? e.evidenceRefs : []),
    ...artifactFiles,
  ])]
  return {
    success: true,
    task: selected.task,
    instance: selected.instance,
    metadata,
    conversation,
    conversationGroups,
    runStages,
    dshTrace,
    uniclawTrace,
    uniflowTrace,
    uniclawTraceContext,
    uniclawTraceTruncated: (observed?.trace?.spans?.length ?? 0) > TRACE_SPAN_LIMIT,
    evidence,
    artifactWarnings: artifactIndex.warnings,
  }
}

/**
 * The panel service methods over the shared repository + host ctx.
 * Exported as a factory so the offline tests drive the exact implementation
 * the Typert decoration delegates to.
 * @param ctx - host-mode context (sessionQuery / sessionController seams).
 */
export function createTaskPanelService(ctx, repository, artifactSource = null, launchDefaults = DEFAULT_LOCAL_LAUNCH_DEFAULTS, localConfig = DEFAULT_LOCAL_CONFIG) {
  const withStore = (work) => {
    try {
      return work()
    } catch (error) {
      return panelFail(panelError('store-corrupted', String(error && error.message ? error.message : error)))
    }
  }

  return {
    overview() {
      return withStore(() => ({
        success: true,
        tasks: repository.list().map(task => ({
          taskId: task.taskId,
          title: task.title,
          requirement: task.requirement,
          projectRef: task.projectRef,
          status: task.status,
          createdAt: task.createdAt,
          instanceCount: Array.isArray(task.instances) ? task.instances.length : 0,
        })),
      }))
    },

    createTask(body) {
      if (body === null || typeof body !== 'object'
        || typeof body.title !== 'string' || body.title.length === 0
        || typeof body.requirement !== 'string' || body.requirement.length === 0) {
        return panelFail(panelError('invalid-request', 'createTask requires title and requirement strings'))
      }
      return withStore(() => ({
        success: true,
        task: repository.create({ title: body.title, requirement: body.requirement, projectRef: body.projectRef }),
      }))
    },

    async instantiate(body) {
      const result = await instantiateTask(ctx, repository, typeof body?.taskId === 'string' ? body.taskId : '')
      if (result.ok) return { success: true, instance: result.instance }
      return panelFail(panelError(result.code, result.message))
    },

    async launch(body) {
      const result = await launchTask(ctx, repository, body)
      if (result.ok) return { success: true, idempotent: result.idempotent === true, ack: result.ack, instance: result.instance }
      return { success: false, error: { code: result.code, message: result.message, ...(result.partial ? { partial: result.partial } : {}) } }
    },

    async sessions() {
      const result = await listSessionsCore(ctx, repository)
      if (!result.ok) return panelFail(result)
      return { success: true, sessions: result.sessions }
    },

    async trace(body) {
      const sessionId = body?.sessionId
      if (typeof sessionId !== 'string' || sessionId.length === 0) {
        return panelFail(panelError('invalid-request', 'trace requires a sessionId string'))
      }
      const events = await listSessionEvents(ctx, sessionId)
      if (!events.ok) return panelFail(events)
      // Semantic entries are extracted from event text ONLY (zero-fabrication).
      const entries = events.events.flatMap(extractEntries)
      return { success: true, entries, sessionAvailable: true }
    },

    workspace() {
      return withStore(() => workspaceCore(repository, artifactSource, launchDefaults, localConfig))
    },

    async session(body) {
      const result = await sessionDetailCore(ctx, repository, artifactSource, body?.sessionId)
      return result.ok === false ? panelFail(result) : result
    },

    artifact(body) {
      const sessionId = body?.sessionId
      const ref = body?.ref
      if (typeof sessionId !== 'string' || sessionId.length === 0
        || typeof ref !== 'string' || ref.length === 0) {
        return panelFail(panelError('invalid-request', 'artifact requires sessionId and ref strings'))
      }
      if (artifactSource === null || typeof artifactSource.read !== 'function') {
        return panelFail(panelError('artifact-source-unavailable', '关联运行产物读取服务不可用'))
      }
      try {
        const file = artifactSource.read(sessionId, ref)
        return {
          success: true,
          ref: file.ref,
          name: file.name,
          text: file.text.slice(0, ARTIFACT_DETAIL_LIMIT),
          truncated: file.text.length > ARTIFACT_DETAIL_LIMIT,
        }
      } catch (error) {
        return panelFail(panelError('artifact-not-found', String(error && error.message ? error.message : error)))
      }
    },

    async diagnostic(body) {
      const skill = body?.skill
      if (typeof skill !== 'string' || !DIAGNOSTIC_SKILLS.includes(skill)) {
        return panelFail(panelError('invalid-skill', `skill must be one of [${DIAGNOSTIC_SKILLS.join(', ')}]`))
      }
      const sessionId = body?.sessionId
      if (typeof sessionId !== 'string' || sessionId.length === 0) {
        return panelFail(panelError('invalid-request', 'diagnostic requires a sessionId string'))
      }
      const resolved = findInstanceBySessionId(repository, sessionId)
      const controller = ctx.get('sessionController')
      if (controller === undefined || typeof controller.prompt !== 'function') {
        return panelFail(panelError('session-controller-unavailable', 'the host session controller is not available to this plugin'))
      }
      const text = resolved.instance !== undefined
        ? diagnosticPrompt(skill, resolved.task, resolved.instance)
        : `You are running the frozen UniClaw diagnostic skill "${skill}" for DSH session ${sessionId}.\nCollect runtime evidence per the skill (Expected/Observed/Gap with concrete evidence levels) and report findings; do not modify code in this turn.`
      try {
        await controller.prompt({
          requestId: `diag-${sessionId}-${Date.now()}`,
          sessionId,
          mode: 'queue',
          content: [{ type: 'text', text }],
        }, AbortSignal.timeout(60_000))
      } catch (error) {
        return panelFail(panelError('prompt-failed', String(error && error.message ? error.message : error)))
      }
      return { success: true, injected: true }
    },
  }
}

// ---------------------------------------------------------------------------
// HTTP plumbing — same shapes as decision-channel (json/fail/readJson).
// ---------------------------------------------------------------------------

function json(status, value) {
  return new Response(JSON.stringify(value), {
    status,
    headers: { 'content-type': 'application/json', 'cache-control': 'no-store' },
  })
}

function fail(status, code, message, extra = {}) {
  return json(status, { ok: false, error: { code, message, details: extra } })
}

async function readJson(request) {
  try {
    const body = await request.json()
    if (body === null || typeof body !== 'object' || Array.isArray(body)) return null
    return body
  } catch {
    return null
  }
}

export function apply(ctx, config) {
  const log = (...parts) => console.info('[uniclaw-task-workbench]', ...parts)

  // Test/ops seams: the JSON store location and the schema directory are
  // overridable (config) so tests never touch the operator's real home and
  // the hash self-check is exercisable against a tampered copy.
  const configuredDir = (value, fallback) =>
    typeof value === 'string' && value.trim().length > 0 ? value.trim() : fallback
  const schemaDir = configuredDir(config?.schemaDir, join(PACKAGE_ROOT, 'schema'))
  const storePath = configuredDir(config?.storePath, undefined)
  const launchDefaults = resolveLaunchDefaults(config?.launchDefaults ?? config?.defaultLaunch)
  const localConfig = resolveLocalConfig(config?.localConfig)
  const artifactRoots = config !== null && typeof config === 'object' && Object.hasOwn(config, 'artifactRoots')
    ? (Array.isArray(config.artifactRoots) ? config.artifactRoots : [])
    : [join(process.cwd(), 'evidence')]

  // Fail-closed gate: a drifted artifact refuses plugin startup entirely.
  const artifact = loadSchemaArtifact(schemaDir)
  log(`task protocol artifact loaded: schemaHash=${artifact.schemaHash.slice(0, 12)}…`)

  // Register the Host-facing Runtime transport at the composition boundary.
  // An existing Host provider wins; otherwise the adapter delegates to the
  // configured Runtime endpoint and stays explicitly unavailable when no
  // endpoint is configured. It never creates IDs locally.
  const existingRuntime = typeof ctx.get === 'function' ? ctx.get('uniclawRuntime') : undefined
  if (existingRuntime === undefined && ctx.reflect && typeof ctx.reflect.provide === 'function') {
    const runtimeProvider = createRuntimeProvider({
      baseUrl: configuredDir(config?.runtimeBaseUrl, process.env.UNICLAW_RUNTIME_BASE_URL),
      fetchImpl: config?.runtimeFetch,
      timeoutMs: config?.runtimeTimeoutMs,
      createPath: config?.runtimeCreatePath,
      recoverPath: config?.runtimeRecoverPath,
    })
    ctx.reflect.provide('uniclawRuntime', runtimeProvider)
    log('registered: uniclawRuntime Host transport adapter')
  }


  const repository = createTaskRepository(storePath)
  const artifactSource = createArtifactSource(artifactRoots)

  /** Store read/write failures are a corrupted/unsupported store, not a client error. */
  const withStore = (work) => {
    try {
      return work()
    } catch (error) {
      return fail(500, 'store-corrupted', String(error && error.message ? error.message : error))
    }
  }

  // /api/uniclaw-task/tasks — one registration, two methods (a second
  // register() on the same path would silently replace the first).
  // GET lists task definitions; POST creates one from a validated body.
  ctx.connection.fetch.register({
    path: '/api/uniclaw-task/tasks',
    methods: ['GET', 'POST'],
    requestBody: 'buffered',
    fetch: async (request) => {
      if (request.method === 'GET') {
        return withStore(() => json(200, { ok: true, tasks: repository.list() }))
      }
      const body = await readJson(request)
      if (body === null) return fail(400, 'gateway/bad-request', 'task body must be a JSON object')
      const check = validateRequest(body, artifact.schema, 'CreateTaskRequest')
      if (!check.ok) return fail(400, 'gateway/bad-request', check.message)
      return withStore(() => {
        const task = repository.create({
          taskId: body.taskId,
          title: body.title,
          requirement: body.requirement,
          projectRef: body.projectRef,
          status: body.status,
        })
        log('task created', { taskId: task.taskId })
        return json(200, { ok: true, task })
      })
    },
  })

  // POST /api/uniclaw-task/tasks/instantiate — thin HTTP adapter over the
  // shared instantiateTask core (the panel service uses the same core, so the
  // two protocols cannot drift). Wire shape unchanged: routes/status codes/
  // response fields are frozen.
  ctx.connection.fetch.register({
    path: '/api/uniclaw-task/tasks/launch',
    methods: ['POST'],
    requestBody: 'buffered',
    fetch: async (request) => {
      const body = await readJson(request)
      if (body === null) return fail(400, 'gateway/bad-request', 'launch body must be a JSON object')
      const result = await launchTask(ctx, repository, body)
      if (!result.ok) return fail(result.status, result.code, result.message, result.partial ? { partial: result.partial } : undefined)
      return json(200, { ok: true, idempotent: result.idempotent === true, ack: result.ack, instance: result.instance })
    },
  })

  ctx.connection.fetch.register({
    path: '/api/uniclaw-task/tasks/instantiate',
    methods: ['POST'],
    requestBody: 'buffered',
    fetch: async (request) => {
      const body = await readJson(request)
      if (body === null) return fail(400, 'gateway/bad-request', 'instantiate body must be a JSON object')
      const check = validateRequest(body, artifact.schema, 'InstantiateRequest')
      if (!check.ok) return fail(400, 'gateway/bad-request', check.message)

      const result = await instantiateTask(ctx, repository, body.taskId)
      if (!result.ok) return fail(result.status, result.code, result.message)
      log('task instantiated', { taskId: body.taskId, instanceId: result.instance.instanceId, sessionId: result.instance.sessionId })
      return json(200, { ok: true, instance: result.instance })
    },
  })

  // ---- Product Workspace: task panel data service (host mount only). Static Client
  // half calls uniclawTaskPanel.<method> through the Typert gateway's SRC
  // runtime resolution. The typert-protocol package exists in the DSH profile
  // but not in the offline test environment — degrade to HTTP-only there, via
  // the same SYNC require.resolve probe as decision-channel (a failed dynamic
  // import inside the node --test child stalls the runner ~60s).
  if (panelProtocolAvailable()) {
    const panel = createTaskPanelService(ctx, repository, artifactSource, launchDefaults, localConfig)
    const PANEL_METHODS = ['overview', 'createTask', 'instantiate', 'launch', 'sessions', 'trace', 'workspace', 'session', 'artifact', 'diagnostic']
    import('@deepseek-ai/dsh-typert-protocol').then(({ TypertRemoteService, Remote }) => {
      class TaskPanelHost extends TypertRemoteService {
        static inject = []
        constructor(ctxRef) { super(ctxRef, 'uniclawTaskPanel') }
      }
      // Manual decorator application (no decorator syntax in plain JS): one
      // Remote() per method, each collecting its own initializer. Gateway
      // dispatch passes POSITIONAL values (one per declared wire parameter,
      // cf. dsh-goal remoteExportCreate) while panel functions take one body
      // object — pack positionally here, BEFORE decorating, so the Remote
      // marker always references the final implementation.
      const PARAM_NAMES = {
        overview: [],
        createTask: ['title', 'requirement', 'projectRef'],
        instantiate: ['taskId'],
        launch: ['schemaVersion', 'contractVersion', 'launchRequestId', 'projectRef', 'testSetRef', 'taskRef', 'idempotencyKey', 'correlationId', 'requestedAt', 'environmentIntent', 'metadata', 'taskId'],
        sessions: [],
        trace: ['sessionId'],
        workspace: [],
        session: ['sessionId'],
        artifact: ['sessionId', 'ref'],
        diagnostic: ['sessionId', 'skill'],
      }
      const initializers = []
      for (const method of PANEL_METHODS) {
        const names = PARAM_NAMES[method]
        TaskPanelHost.prototype[method] = async function (...values) {
          const body = {}
          names.forEach((n, i) => { if (values[i] !== undefined) body[n] = values[i] })
          return panel[method](body)
        }
        let collected = null
        Remote(method)(TaskPanelHost.prototype[method], {
          kind: 'method', name: method, private: false, static: false, metadata: undefined,
          access: { get: () => TaskPanelHost.prototype[method] },
          addInitializer(fn) { collected = fn },
        })
        if (collected) initializers.push(collected)
      }
      const hostInstance = new TaskPanelHost(ctx)
      for (const initializer of initializers) initializer.call(hostInstance)

      // Wire parameter descriptors per method (frozen API). The gateway
      // validates incoming args fields against THIS list, so every business
      // parameter must be declared; optional ones accept undefined (the wire
      // omits undefined fields).
      const strictCodec = (symbol) => ({
        mode: 'strict',
        typeSymbol: symbol,
        create: () => ({ parse: (value) => value }),
      })
      const param = (name, optional = false) => ({
        name, wire: name, source: 'json',
        codec: strictCodec(`uniclaw-task-workbench/${name}`),
        ...(optional ? { acceptsUndefined: true } : {}),
      })
      const PANEL_PARAMETERS = {
        overview: [],
        createTask: [param('title'), param('requirement'), param('projectRef', true)],
        instantiate: [param('taskId')],
        launch: [param('schemaVersion'), param('contractVersion'), param('launchRequestId'), param('projectRef'), param('testSetRef'), param('taskRef'), param('idempotencyKey'), param('correlationId'), param('requestedAt'), param('environmentIntent', true), param('metadata', true), param('taskId', true)],
        sessions: [],
        trace: [param('sessionId')],
        workspace: [],
        session: [param('sessionId')],
        artifact: [param('sessionId'), param('ref')],
        diagnostic: [param('sessionId'), param('skill')],
      }
      const PANEL_DESCRIPTORS = PANEL_METHODS.map(method => ({
        id: `uniclawTaskPanel.${method}`,
        service: 'uniclawTaskPanel',
        namespace: 'uniclawTaskPanel',
        method,
        invocation: { kind: 'direct' },
        parameters: PANEL_PARAMETERS[method],
        result: strictCodec('uniclaw-task-workbench/JsonValue'),
      }))
      const registerTypertEndpoint = () => {
        const typert = ctx.get('typert')
        if (typert === undefined) return false
        try {
          typert.register({
            package: '@uniclaw/dsh-task-workbench',
            face: 'host',
            schemas: [],
            model: { services: [], events: [], objects: [] },
            invocations: PANEL_DESCRIPTORS,
          })
          return true
        } catch (error) {
          const message = error && error.message ? error.message : String(error)
          // Idempotent re-registration (host reloads) is success, not failure.
          if (/already registered|endpoint .* already|invocation id .* already/.test(message)) return true
          log('typert.register failed:', message)
          return false
        }
      }
      if (!registerTypertEndpoint()) {
        // The typert gateway may come up after this plugin mounts; retry with
        // backoff instead of leaving the panel unreachable for the whole run.
        let attempts = 0
        const delay = () => {
          if (attempts++ >= 20) return
          if (registerTypertEndpoint()) return
          const timer = ctx.get('timer')
          if (timer !== undefined) timer.timeout(delay, 500)
          else if (typeof setTimeout === 'function') setTimeout(delay, 500)
        }
        delay()
      }
      log('panel service registered: uniclawTaskPanel.{overview,createTask,instantiate,sessions,trace,workspace,session,artifact,diagnostic}')
    }).catch((error) => {
      log('panel service unavailable (typert-protocol not importable):',
        String(error && error.message ? error.message : error).slice(0, 160))
    })
  }

  log(`registered: /api/uniclaw-task/{tasks,tasks/instantiate} + uniclawTaskPanel panel service (schemaHash ${artifact.schemaHash.slice(0, 12)}…)`)
}
