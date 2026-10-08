/**
 * @uniclaw/dsh-decision-channel — UniClaw Product decision channel (AGT-002).
 *
 * Frozen seam (AGT-001 §1/§6.2/§14; AGT-002 Q21–Q23 + review B1/B2/B3/S1):
 * DSH opens the physical channel; the Kernel owns all Product semantics.
 *
 * B2: the plugin consumes the GENERATED Product protocol artifact
 * (schema/product-protocol.schema.json + schema-hash.txt, byte-identical to
 * the .NET ProductProtocolSchema output — enforced by
 * PluginSchemaArtifactTests on the Product side). The submit_decision tool
 * definition, the decision validation, and the reported schemaHash ALL derive
 * from that single artifact. There is no handwritten protocol truth here.
 *
 * B1: every consultation drives a REAL DSH session created under the
 * `uniagent-prod` agent preset, whose composition restricts the session tool
 * surface to exactly [submit_decision]. The binding is declarative and
 * registration-order independent: the preset scope closes everything it
 * INHERITS with `restrict({allow: []})` (see
 * @uniclaw/dsh-uniagent-restrict), while submit_decision survives as an
 * own-layer registration. The handshake then mechanically verifies the ACTUAL
 * visible tool catalog (ctx.tools.schemas(agent)) equals the expected manifest
 * and fails closed otherwise; a scope-aware execution guard refuses any
 * non-manifest call as a second line of defence.
 *
 * B3: submit_decision is the ONLY Product output path. A turn that ends
 * without a capture is a consultation failure (no-submit-decision); assistant
 * prose is never promoted to a Product decision. Normalization performs only
 * representation-level conversions (JSON text → object, singleton → array
 * where the schema expects an array, numeric text → number where the schema
 * expects a number). No semantic defaults, no field invention — missing
 * Product-required fields fail closed.
 *
 * S1: /api/uniclaw-agent/detach is the realization-private detach — it
 * retires the DSH-side attachment (pending turn aborted, mapping released)
 * and nothing else.
 *
 * Routes (all on the authenticated /api lane):
 *   POST /api/uniclaw-agent/handshake
 *   POST /api/uniclaw-agent/consult
 *   POST /api/uniclaw-agent/slow
 *   POST /api/uniclaw-agent/abort
 *   POST /api/uniclaw-agent/detach
 */

import { createHash } from 'node:crypto'
import { mkdirSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'
import { homedir } from 'node:os'
import { createRequire } from 'node:module'

/** Sync availability probe for the profile-only typert-protocol package. */
const panelProtocolAvailable = () => {
  try {
    createRequire(import.meta.url).resolve('@deepseek-ai/dsh-typert-protocol')
    return true
  } catch {
    return false
  }
}

export const name = 'uniclaw-decision-channel'
export const inject = ['tools', 'connection', 'sessionController', 'agents', 'workspaceRegistry']

// ---------------------------------------------------------------------------
// Generated protocol artifact (single source; B2).
// ---------------------------------------------------------------------------

const PACKAGE_ROOT = dirname(dirname(fileURLToPath(import.meta.url)))
const SCHEMA_PATH = join(PACKAGE_ROOT, 'schema', 'product-protocol.schema.json')
const SCHEMA_HASH_PATH = join(PACKAGE_ROOT, 'schema', 'schema-hash.txt')

function loadSchemaArtifact() {
  const schema = JSON.parse(readFileSync(SCHEMA_PATH, 'utf8'))
  const schemaHash = readFileSync(SCHEMA_HASH_PATH, 'utf8').trim()
  // Local hash of the artifact as loaded (ordinal-sorted canonical JSON) —
  // the plugin reports THIS value; the Product side compares it against its
  // own generator output. Self-check refuses startup on drift.
  const localHash = createHash('sha256')
    .update(JSON.stringify(sortKeysDeep(schema)))
    .digest('hex')
  return { schema, schemaHash, localHash }
}

function sortKeysDeep(value) {
  if (Array.isArray(value)) return value.map(sortKeysDeep)
  if (value !== null && typeof value === 'object') {
    const sorted = {}
    for (const key of Object.keys(value).sort()) sorted[key] = sortKeysDeep(value[key])
    return sorted
  }
  return value
}

/** Resolve a local $ref (#/$defs/Name) inside the artifact. */
function deref(schema, node, depth = 0) {
  if (node === null || typeof node !== 'object') return node
  if (depth > 12) return node
  if (typeof node.$ref === 'string' && node.$ref.startsWith('#/$defs/')) {
    const def = schema.$defs[node.$ref.slice('#/$defs/'.length)]
    return deref(schema, def, depth + 1)
  }
  return node
}

// ---------------------------------------------------------------------------
// Schema-driven representation normalization (B3: no semantic completion).
// Only three conversion classes, each gated on what the SCHEMA at this
// position expects: JSON text → object/array; singleton object (or {item})
// → array; numeric text → number. Nothing else.
// ---------------------------------------------------------------------------

function normalizeToSchema(value, schemaNode, rootSchema) {
  const node = deref(rootSchema, schemaNode)
  if (value === null || value === undefined) return value
  if (typeof value === 'string') {
    if ((node.type === 'object' || node.type === 'array' || node.oneOf !== undefined)
        && (value.trimStart().startsWith('{') || value.trimStart().startsWith('['))) {
      try { return normalizeToSchema(JSON.parse(value), node, rootSchema) } catch { /* not JSON text */ }
    }
    if ((node.type === 'number' || node.type === 'integer') && /^-?\d+(\.\d+)?$/.test(value.trim())) {
      const parsed = Number(value)
      if (node.type !== 'integer' || Number.isInteger(parsed)) return parsed
    }
    return value
  }
  if (Array.isArray(value)) {
    const items = deref(rootSchema, node.items ?? {})
    return value.map(item => normalizeToSchema(item, items, rootSchema))
  }
  if (typeof value === 'object') {
    if (node.type === 'array') {
      if (Array.isArray(value.item)) return value.item.map(entry => normalizeToSchema(entry, deref(rootSchema, node.items ?? {}), rootSchema))
      if (value.item !== undefined) return [normalizeToSchema(value.item, deref(rootSchema, node.items ?? {}), rootSchema)]
      return [normalizeToSchema(value, node.items ?? {}, rootSchema)]
    }
    // oneOf: discriminate by the branch's kind const when the value carries
    // one, so each field normalizes against the branch that will validate it.
    if (node.oneOf !== undefined && typeof value.kind === 'string') {
      for (const branch of node.oneOf) {
        const resolved = deref(rootSchema, branch)
        const kind = resolved.properties?.kind?.const ?? resolved.const
        if (kind === value.kind) return normalizeToSchema(value, resolved, rootSchema)
      }
    }
    const properties = node.properties ?? {}
    const result = {}
    for (const [key, child] of Object.entries(value)) {
      result[key] = normalizeToSchema(child, properties[key] ?? {}, rootSchema)
    }
    return result
  }
  return value
}

// ---------------------------------------------------------------------------
// Schema-driven validation (subset: type, required, properties,
// additionalProperties:false, items, enum/const, oneOf). Missing required
// fields fail closed; there is no per-kind handwritten rule anywhere.
// ---------------------------------------------------------------------------

function typeMatches(value, expected) {
  switch (expected) {
    case 'object': return typeof value === 'object' && value !== null && !Array.isArray(value)
    case 'array': return Array.isArray(value)
    case 'string': return typeof value === 'string'
    case 'boolean': return typeof value === 'boolean'
    case 'number': return typeof value === 'number'
    case 'integer': return typeof value === 'number' && Number.isInteger(value)
    default: return true
  }
}

function validateToSchema(value, schemaNode, rootSchema, path, errors) {
  const node = deref(rootSchema, schemaNode)
  if (node === null || typeof node !== 'object' || Object.keys(node).length === 0) return true
  if (node.oneOf !== undefined) {
    const firstBranchErrors = []
    for (const branch of node.oneOf) {
      const branchErrors = []
      validateToSchema(value, branch, rootSchema, path, branchErrors)
      if (branchErrors.length === 0) return true
      if (firstBranchErrors.length === 0) firstBranchErrors.push(...branchErrors.slice(0, 2))
    }
    errors.push(`${path || 'decision'}: ${firstBranchErrors[0] ?? 'no oneOf branch matched'}`)
    return false
  }
  if (node.const !== undefined && value !== node.const) {
    errors.push(`${path || 'value'}: expected const ${JSON.stringify(node.const)}, got ${JSON.stringify(value)}`)
    return false
  }
  if (node.enum !== undefined && !node.enum.includes(value)) {
    errors.push(`${path || 'value'}: ${JSON.stringify(value)} not in enum [${node.enum.map(String).join(',')}]`)
    return false
  }
  if (node.type !== undefined && value !== null && value !== undefined && !typeMatches(value, node.type)) {
    errors.push(`${path || 'value'}: expected ${node.type}, got ${Array.isArray(value) ? 'array' : typeof value}`)
    return false
  }
  if (typeof value === 'number' && node.minimum !== undefined && value < node.minimum) {
    errors.push(`${path || 'value'}: must be >= ${node.minimum}`)
    return false
  }
  if (typeof value === 'number' && node.maximum !== undefined && value > node.maximum) {
    errors.push(`${path || 'value'}: must be <= ${node.maximum}`)
    return false
  }
  if (node.type === 'array' && Array.isArray(value) && node.items !== undefined) {
    value.forEach((item, index) => validateToSchema(item, node.items, rootSchema, `${path}[${index}]`, errors))
  }
  if (node.type === 'object' && typeof value === 'object' && value !== null && !Array.isArray(value)) {
    for (const required of node.required ?? []) {
      if (value[required] === undefined) {
        errors.push(`${path ? path + '.' : ''}${required}: required by Product schema but missing`)
      }
    }
    const properties = node.properties ?? {}
    for (const [key, child] of Object.entries(value)) {
      if (properties[key] === undefined) {
        if (node.additionalProperties === false) {
          errors.push(`${path ? path + '.' : ''}${key}: not in Product schema (additionalProperties false)`)
        }
        continue
      }
      validateToSchema(child, properties[key], rootSchema, `${path ? path + '.' : ''}${key}`, errors)
    }
  }
  return errors.length === 0
}

// Completion evidence is consumed by the Product verifier, so a successful
// noAction completion must carry machine-checkable anchors.  Keeping this
// small shape check at the channel boundary lets the same physical DSH turn
// correct prose checklists instead of escalating an avoidable formatting
// error to the human adjudication gate.
function validateCompletionAnchors(decision, errors) {
  if (decision?.kind !== 'noAction' || decision.proposal?.completion === undefined) return
  const checklist = decision.proposal.completion?.checklist
  if (!Array.isArray(checklist) || checklist.length === 0) {
    errors.push('decision.proposal.completion.checklist: at least one trace anchor is required')
    return
  }
  for (const [index, anchor] of checklist.entries()) {
    if (typeof anchor !== 'string'
      || !/^(?:step:\d+\.\d+|dispatch:\S+|obs:\S+)$/.test(anchor)) {
      errors.push(`decision.proposal.completion.checklist[${index}]: expected step:N.M, dispatch:ID, or obs:ID anchor`)
    }
  }
}

/** Derive the common Agent task envelope parameter map from the artifact. */
function toolParametersFromSchema(artifact) {
  const decision = deref(artifact.schema, artifact.schema.$defs.AgentDecision)
  const branches = decision.oneOf ?? []
  const kinds = []
  let requiresSpec = false
  let requiresProposal = false
  for (const branch of branches) {
    const resolved = deref(artifact.schema, branch)
    const kind = resolved.properties?.kind?.const ?? resolved.const
    if (kind !== undefined && !kinds.includes(kind)) kinds.push(kind)
    if (resolved.properties?.spec !== undefined) requiresSpec = true
    if (resolved.properties?.proposal !== undefined) requiresProposal = true
  }
  const payloadProperties = {
    kind: {
      type: 'string',
      enum: kinds,
      description: `AgentDecision kind (derived from the generated Product protocol artifact, schemaHash ${artifact.schemaHash.slice(0, 12)}…)`,
    },
    decisionId: { type: 'string', description: 'Correlation id; MUST equal context.decisionId.' },
    ...(requiresProposal
      ? { proposal: { type: 'object', additionalProperties: true, description: 'act/noAction/policy proposal payload (exact Product schema shape).' } }
      : {}),
    ...(requiresSpec
      ? { spec: { type: 'object', additionalProperties: true, description: 'defer ObserveSpec {subject, maxRounds} (exact Product schema shape).' } }
      : {}),
  }
  const capabilitySelection = {
    type: 'object',
    description: 'Optional task capability selection; allowed only in the first task initialization.',
    properties: {
      capabilityId: { type: 'string', description: 'Registered capability id.' },
      expectedLanguage: { type: 'string' },
      ignoreRoutes: { type: 'array', items: { type: 'string' } },
    },
    required: ['capabilityId'],
    additionalProperties: false,
  }
  return {
    type: 'object',
    properties: {
      task: {
        type: 'object',
        properties: {
          initialization: {
            type: 'object', properties: { capabilitySelection }, additionalProperties: false,
          },
          payload: {
            type: 'object', properties: payloadProperties,
            required: ['kind', 'decisionId'], additionalProperties: false,
          },
        },
        required: ['initialization', 'payload'], additionalProperties: false,
      },
    },
    required: ['task'], additionalProperties: false,
  }
}

// ---------------------------------------------------------------------------
// Channel state + helpers.
// ---------------------------------------------------------------------------

const PROTOCOL_VERSION = 'uniclaw.agent.protocol.v1'
const SCHEMA_VERSION = 'uniclaw.agent.schema.v1'
const PROFILE_ID = 'uniagent-prod'
const PROFILE_VERSION = '1'
const CAPABILITIES = ['submit_decision']
const AGENT_PRESET_ID = 'uniagent-prod'
const EXPECTED_SESSION_TOOLS = ['submit_decision']
const DEFAULT_TURN_TIMEOUT_MS = 60_000
const MAX_TURN_TIMEOUT_MS = 240_000  // PER-019: reasoning models (deepseek-flash thinking) legitimately exceed 120s

// ---------------------------------------------------------------------------
// Process-local event ledger (PNL-001). Observation only — never a Product
// truth source; the panel service below is a projection of this ledger.
// ---------------------------------------------------------------------------

const LEDGER_LIMIT = 500
const ledger = []
const recordEvent = (kind, fields = {}) => {
  ledger.push({ ts: new Date().toISOString(), kind, ...fields })
  if (ledger.length > LEDGER_LIMIT) ledger.splice(0, ledger.length - LEDGER_LIMIT)
}

/** Projection of the channel runtime for the control panel / tests. */
export function panelSnapshot() {
  return {
    attached: state.attached,
    inFlight: {
      consult: state.pending !== null,
      slow: state.slowPending !== null,
    },
    promptProbe: state.promptProbe,
    events: ledger.slice(),
  }
}

const state = {
  attached: null, // { productSessionId, productRunId, dshSessionId }
  pending: null,  // { requestId, decisionId, resolve, timer, captured, lastError }
  activeTurn: null, // { sessionId, turnEnd, resolveTurnEnd, pending }
  slowPending: null, // { requestId, sessionId, resolve, timer, text }
  sessionTool: null, // tool definition published by the session-scoped mount (B1)
  sessionToolDisposer: null, // live agent-scope registration for the attached session
  initialCapabilitySelectionSettled: false,
  // PRF-004 — isolation probe: names (never text) of the prompt inputs this
  // product preset scope assembles, recorded once at preset-mount time by the
  // session-scoped row and exposed read-only by the host row for mechanical
  // audit. null = not recorded yet; { error } = assembly failed.
  promptProbe: null,
}

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

/** Resolve the live Agent behind one restricted session. */
async function resolveSessionAgent(ctx, sessionId) {
  const controller = ctx.get('sessionController')
  const found = await controller.resolveAgent(sessionId)
  if (found !== null && typeof found === 'object' && 'error' in found) {
    throw new Error(`agent-resolution-failed: ${JSON.stringify(found.error).slice(0, 160)}`)
  }
  return found.agent ?? found
}

/**
 * The ACTUAL model-visible tool catalog for the attached session's agent.
 * @param ctx - host-mode context (holds the tools registry).
 * @param sessionId - the DSH session whose agent is inspected.
 */
async function sessionToolCatalog(ctx, sessionId) {
  const agent = await resolveSessionAgent(ctx, sessionId)
  return ctx.tools.schemas(agent).map(schema => schema.name).sort()
}

/** Consultation prompt built from the context + artifact identity. */
// PRF-003 — product prompt manifest artifact: versioned static segments
// (identity / output discipline / payload shapes) loaded once at boot from the
// package copy (kept byte-identical with the canonical product/prompt/ source
// by the sync test). Fail-closed gates: artifact hash and protocol-schema
// coupling — a schema bump without a manifest bump refuses to load (Q10).
const PROMPT_TOKENS = ['protocolVersion', 'schemaVersion', 'schemaHash']

// PRF-003 — exported for the deterministic failure tests (hash/coupling).
export function loadPromptArtifactFor(root, schemaArtifact) {
  return loadPromptArtifactAt(root, schemaArtifact)
}

function loadPromptArtifact(schemaArtifact) {
  return loadPromptArtifactAt(join(PACKAGE_ROOT, 'prompt'), schemaArtifact)
}

function loadPromptArtifactAt(root, schemaArtifact) {
  const manifest = JSON.parse(readFileSync(join(root, 'manifest.json'), 'utf8'))
  if (manifest.schemaVersion !== 'uniagent.prompt/v1')
    throw new Error(`uniclaw-decision-channel: unsupported prompt manifest schemaVersion ${manifest.schemaVersion}`)
  if (manifest.protocol?.protocolVersion !== PROTOCOL_VERSION
    || manifest.protocol?.schemaVersion !== SCHEMA_VERSION
    || manifest.protocol?.schemaHash !== schemaArtifact.schemaHash)
    throw new Error(
      'uniclaw-decision-channel: prompt manifest protocol coupling mismatch '
      + `(manifest ${manifest.protocol?.protocolVersion}/${manifest.protocol?.schemaVersion}/`
      + `${manifest.protocol?.schemaHash?.slice(0, 12)}… != loaded ${PROTOCOL_VERSION}/${SCHEMA_VERSION}/`
      + `${schemaArtifact.schemaHash.slice(0, 12)}…); bump the manifest and promptRevision with the schema change`)
  const digest = createHash('sha256')
  digest.update(readFileSync(join(root, 'manifest.json')))
  const segments = []
  for (const name of manifest.segments ?? []) {
    const raw = readFileSync(join(root, name))
    digest.update(raw)
    let text = raw.toString('utf8')
    for (const token of PROMPT_TOKENS)
      text = text.replaceAll(`{{${token}}}`, { protocolVersion: PROTOCOL_VERSION, schemaVersion: SCHEMA_VERSION, schemaHash: schemaArtifact.schemaHash }[token])
    if (/\{\{[a-z][a-z0-9_]*\}\}/.test(text))
      throw new Error(`uniclaw-decision-channel: unknown {{token}} left in prompt segment ${name}`)
    segments.push(text.trimEnd())
  }
  if (segments.length === 0)
    throw new Error('uniclaw-decision-channel: prompt manifest has no segments')
  const computed = digest.digest('hex')
  const recorded = readFileSync(join(root, 'prompt-hash.txt'), 'utf8').trim()
  if (computed !== recorded)
    throw new Error(
      `uniclaw-decision-channel: prompt artifact hash mismatch (file ${computed.slice(0, 12)}… != prompt-hash.txt ${recorded.slice(0, 12)}…); `
      + 'regenerate via tools/prompt-manifest-hash.py and bump promptRevision')
  return {
    revision: manifest.promptRevision,
    segments,
    staticText: segments.join('\n\n'),
  }
}

function consultationPrompt(request, artifact, promptArtifact, promptMount) {
  const allowedEffects = Array.isArray(request.context?.allowedEffects)
    ? request.context.allowedEffects
    : []
  const allowedEffectsInstruction = allowedEffects.length > 0
    ? `Allowed effectClass tokens for this decision (copy exactly, case-sensitive): ${JSON.stringify(allowedEffects)}.`
    : 'The exact effectClass tokens are the values in context.allowedEffects; copy one of those values.'
  // PRF-003: static identity / output discipline / payload shapes live in the
  // versioned prompt artifact. In section mount they ride the session system
  // prompt (registered by the preset row); in per-turn fallback they lead the
  // turn content. Only genuinely per-turn lines remain here.
  const dynamic = [
    `This is the CURRENT consultation turn. The current Product DecisionId is ${request.context.decisionId}.`,
    'Ignore decisionIds from all previous turns in this task session; they are historical and must not be reused.',
    allowedEffectsInstruction,
    '',
    '=== AgentDecisionContext (JSON) ===',
    JSON.stringify(request.context, null, 2),
  ]
  if (promptMount !== 'section' && promptArtifact) return [promptArtifact.staticText, '', ...dynamic].join('\n')
  return dynamic.join('\n')
}

export function apply(ctx, config) {
  const log = (...parts) => console.info('[uniclaw-decision-channel]', ...parts)

  const artifact = loadSchemaArtifact()
  if (artifact.localHash !== artifact.schemaHash) {
    throw new Error(
      `uniclaw-decision-channel: schema artifact self-check failed (file hash ${artifact.localHash} != schema-hash.txt ${artifact.schemaHash})`)
  }
  log(`generated protocol artifact loaded: schemaHash=${artifact.schemaHash.slice(0, 12)}…`)

  // Dual-mode mount (B1), discriminated by the mounting row's config: the
  // HOST row (profile patch, no sessionScoped flag) registers the
  // authenticated routes + the global tool; a PRESET row with
  // config.sessionScoped registers ONLY the session-scoped submit_decision
  // tool. Environment probing cannot discriminate (scoped contexts resolve
  // host services up the chain); the row config is explicit author intent.
  const sessionScoped = config !== null && typeof config === 'object'
    && config.sessionScoped === true
  const controller = sessionScoped ? undefined : ctx.get('sessionController')
  const workspaceRegistry = sessionScoped ? undefined : ctx.get('workspaceRegistry')
  const configuredString = (key, fallback) => {
    const value = config !== null && typeof config === 'object' ? config[key] : undefined
    return typeof value === 'string' && value.trim().length > 0 ? value.trim() : fallback
  }
  const configuredBoolean = (key, fallback) =>
    config !== null && typeof config === 'object' && typeof config[key] === 'boolean'
      ? config[key] : fallback
  const workspaceTitle = configuredString('workspaceTitle', 'UniClaw Product Sessions')
  const sessionTitle = configuredString('sessionTitle', 'UniClaw Product Consultation')
  const slowSessionTitle = configuredString('slowSessionTitle', `${sessionTitle} · Slow`)
  const workspaceKey = configuredString('workspaceKey', workspaceTitle)
  const workspaceReuse = configuredBoolean('workspaceReuse', false)
  const autoCloseTurn = configuredBoolean('autoCloseTurn', false)
  // PRF-003: versioned static prompt artifact + mount mode. 'section' (default):
  // the preset row registers the static text as a scoped system-prompt section
  // and each turn carries only the dynamic lines. 'per-turn': fallback that
  // prepends the static text to every turn (set it on BOTH rows to avoid a
  // duplicated section + per-turn copy).
  const promptMount = configuredString('promptMount', 'section')
  if (promptMount !== 'section' && promptMount !== 'per-turn')
    throw new Error(`uniclaw-decision-channel: config.promptMount must be 'section' or 'per-turn' (got '${promptMount}')`)
  const promptArtifact = loadPromptArtifact(artifact)
  log(`product prompt manifest loaded (revision ${promptArtifact.revision}, mount ${promptMount})`)
  // Root for the Product sessions' dedicated workspaces. Overridable so an
  // operator (or a test) can place them outside the default DSH home.
  const workspaceRoot = config !== null && typeof config === 'object'
    && typeof config.workspaceRoot === 'string' && config.workspaceRoot.length > 0
    ? config.workspaceRoot
    : join(homedir(), '.dsh', 'uniagent-workspaces')
  const workspacePath = (productSessionId) => join(workspaceRoot,
    (workspaceReuse ? workspaceKey : productSessionId).replace(/[^a-zA-Z0-9._-]/g, '_'))

  // ---- submit_decision tool: parameters derived from the artifact (B2).
  // Built once; WHERE it is registered depends on the mount mode (below),
  // because the preset's own restriction filter must not strip it.
  const submitDecisionTool = {
    name: 'submit_decision',
    description:
      'Submit one Agent task envelope (initialization plus Product AgentDecision payload). ' +
      'This is the only approved Product output; task.payload.decisionId must equal the context decisionId.',
    parameters: toolParametersFromSchema(artifact),
    output: {
      schema: {
        type: 'object',
        properties: { accepted: { type: 'boolean' }, reason: { type: 'string' } },
        additionalProperties: false,
      },
      // DSH tool output renderers return content blocks. Returning a bare
      // string makes the real runtime call `content.some(...)` on a string
      // and turns an otherwise captured decision into a tool execution error.
      render: (_args, value) => [{
        type: 'text',
        text: value && value.accepted
          ? `submit_decision accepted: ${value.reason ?? ''}`
          : `submit_decision rejected: ${value && value.reason ? value.reason : 'unknown'}`,
      }],
    },
    execute: (args) => {
      const pending = state.pending
      if (pending === null) return { accepted: false, reason: 'no-pending-consultation' }
      if (pending.captured !== null) return { accepted: false, reason: 'decision-already-captured' }
      const task = args?.task
      const initialization = task?.initialization
      const payload = task?.payload
      const receivedDecisionId = payload?.decisionId
      if (typeof receivedDecisionId !== 'string' || receivedDecisionId !== pending.decisionId) {
        const received = typeof receivedDecisionId === 'string' ? receivedDecisionId : '<missing>'
        pending.lastError = { code: 'decision-id-mismatch', message: `expected ${pending.decisionId}; received ${received}` }
      } else {
        // A corrected tool call may recover the same consultation after a
        // previous correlation or schema rejection.
        pending.lastError = null
        if (task === null || typeof task !== 'object' || Array.isArray(task)
          || initialization === null || typeof initialization !== 'object' || Array.isArray(initialization)
          || payload === null || typeof payload !== 'object' || Array.isArray(payload)) {
          pending.lastError = { code: 'task-envelope-invalid', message: 'task requires initialization and payload objects' }
        }
        const selection = initialization?.capabilitySelection
        if (selection !== undefined) {
          if (selection === null || typeof selection !== 'object' || Array.isArray(selection)
            || typeof selection.capabilityId !== 'string' || selection.capabilityId.trim().length === 0
            || (selection.expectedLanguage !== undefined && typeof selection.expectedLanguage !== 'string')
            || (selection.ignoreRoutes !== undefined
              && (!Array.isArray(selection.ignoreRoutes)
                || selection.ignoreRoutes.some(route => typeof route !== 'string' || route.trim().length === 0)))) {
            pending.lastError = { code: 'capability-selection-invalid', message: 'capabilitySelection shape is invalid' }
          } else if (state.initialCapabilitySelectionSettled) {
            pending.lastError = { code: 'capability-selection-not-initial', message: 'capabilitySelection is accepted only in the first task initialization' }
          }
        }
        const decisionArgs = payload
        // B3: representation normalization only, then schema-driven validation.
        const normalized = normalizeToSchema(decisionArgs, artifact.schema.$defs.AgentDecision, artifact.schema)
        const errors = []
        if (pending.lastError === null) {
          validateToSchema(normalized, artifact.schema.$defs.AgentDecision, artifact.schema, 'decision', errors)
          if (errors.length === 0) validateCompletionAnchors(normalized, errors)
        }
        if (errors.length === 0 && pending.lastError === null) {
          pending.captured = {
            ok: true,
            task: { initialization: selection === undefined ? {} : { capabilitySelection: selection }, payload: normalized },
          }
          if (selection !== undefined) state.initialCapabilitySelectionSettled = true
          recordEvent('decision-captured', {
            requestId: pending.requestId, decisionId: pending.decisionId, decisionKind: normalized.kind,
            capabilitySelection: selection?.capabilityId ?? null,
          })
        } else if (pending.lastError === null) {
          pending.lastError = { code: 'decision-schema-invalid', message: errors.slice(0, 4).join('; ') }
          recordEvent('decision-rejected', {
            requestId: pending.requestId, decisionId: pending.decisionId, code: 'decision-schema-invalid',
          })
        }
      }
      if (pending.captured === null) {
        // Keep the physical turn alive after a malformed tool call. The model
        // receives the rejection content and can correct the same consultation
        // without creating a second Product DecisionId or DSH session.
        if (pending.lastError !== null && pending.lastError.code !== 'decision-schema-invalid') {
          recordEvent('decision-rejected', {
            requestId: pending.requestId, decisionId: pending.decisionId, code: pending.lastError.code,
          })
        }
        return { accepted: false, reason: pending.lastError.code }
      }
      if (pending.timer !== null) clearTimeout(pending.timer)
      const resolve = pending.resolve
      state.pending = null
      // The semantic decision is already captured. Close the physical DSH
      // turn before the next task consultation so trailing tool/event work
      // cannot overlap the next pending decision on the same session.
      const attached = state.attached
      const controller = ctx.get('sessionController')
      if (autoCloseTurn && pending.captured.ok === true && attached !== null
        && controller !== undefined && typeof controller.cancel === 'function') {
        try { controller.cancel({ sessionId: attached.dshSessionId }) } catch (error) { /* fail closed at the turn barrier */ }
      }
      resolve(pending.captured)
      return { accepted: pending.captured.ok, reason: pending.captured.ok ? 'decision captured' : pending.captured.error.code }
    },
  }

  // ---- session journal watch: B3 — turn end WITHOUT capture is a failure.
  ctx.on('session/event', (session, event) => {
    const attached = state.attached
    const slowPending = state.slowPending
    if (slowPending !== null && session.id === slowPending.sessionId) {
      const type = event && typeof event.type === 'string' ? event.type : ''
      if (type === 'assistant/message') {
        const blocks = event.data?.message?.content
        if (Array.isArray(blocks)) {
          const text = blocks.filter(block => block && block.type === 'text'
            && typeof block.text === 'string').map(block => block.text).join('')
          if (text.trim().length > 0) slowPending.text = text
        }
      }
      if (type === 'assistant/attempt') {
        const stream = event.data?.stream
        if (Array.isArray(stream)) {
          const text = stream.flatMap(chunk => chunk?.type === 'text-chunks' && Array.isArray(chunk.texts)
            ? chunk.texts : []).filter(value => typeof value === 'string').join('')
          if (text.trim().length > 0) slowPending.text = text
        }
      }
      if (type === 'turn/end') {
        if (slowPending.timer !== null) clearTimeout(slowPending.timer)
        state.slowPending = null
        slowPending.resolve(slowPending.text === null
          ? { ok: false, error: { code: 'no-slow-result', message: `model finished without structured SlowResult text${event.data?.reason ? ` (${JSON.stringify(event.data.reason).slice(0, 240)})` : ''}` } }
          : { ok: true, text: slowPending.text })
      }
      return
    }
    if (attached === null || session.id !== attached.dshSessionId) return
    const pending = state.pending
    const activeTurn = state.activeTurn
    const type = event && typeof event.type === 'string' ? event.type : ''
      if (type === 'turn/end' && pending !== null && pending.captured === null) {
      // The model finished its turn without calling submit_decision. Prose is
      // trajectory evidence only — NEVER promoted to a Product decision.
      pending.captured = {
        ok: false,
        error: pending.lastError ?? { code: 'no-submit-decision', message: 'model finished the turn without calling submit_decision' },
      }
      if (pending.timer !== null) clearTimeout(pending.timer)
      state.pending = null
      pending.resolve(pending.captured)
    }
    if (type === 'turn/end' && activeTurn !== null && activeTurn.sessionId === session.id) {
      if (state.activeTurn === activeTurn) state.activeTurn = null
      activeTurn.resolveTurnEnd()
    }
  })

  const retirePending = () => {
    const pending = state.pending
    if (pending !== null) {
      if (pending.timer !== null) clearTimeout(pending.timer)
      state.pending = null
      if (pending.captured === null) {
        pending.captured = { ok: false, error: { code: 'turn-aborted', message: 'aborted before completion' } }
      }
      pending.resolve(pending.captured)
    }
    const activeTurn = state.activeTurn
    if (activeTurn !== null) {
      state.activeTurn = null
      activeTurn.resolveTurnEnd()
    }
  }

  // ---- Product channel routes on the authenticated /api lane (host mode).
  if (sessionScoped) {
    // B1: publish the tool definition for the host-side handshake to register
    // into the AGENT's own scope, and close the inherited surface here.
    //
    // The tool is deliberately NOT registered in this preset scope: the preset
    // composition applies `restrict({allow: []})`
    // (@uniclaw/dsh-uniagent-restrict) in this very layer, and a same-layer
    // registration is filtered by it (view() resolves a scope's own tools
    // through the ancestor chain; only the VIEWING scope's own layer is
    // exempt). Registering here would therefore yield an empty catalog.
    state.sessionTool = submitDecisionTool

    // Second line of defence: a monotonic execution guard, evaluated against
    // the live registry at execution time, so it also covers tools that
    // register after this line.
    ctx.tools.guard((exec) => (exec.name === 'submit_decision'
      ? undefined
      : `uniagent-prod: tool "${exec.name}" is not in the frozen capability manifest [submit_decision]`))

    // PRF-003 / Q11(a): mount the static product prompt as a scoped
    // system-prompt section of this preset (sessions joining uniagent-prod
    // inherit it; dev/harness sections stay in other scopes). Live probe
    // finding (PRF-004): registration must go through ctx.inject(...) — the
    // scoped-service pattern the MCP client uses for server instructions;
    // a bare ctx.get('systemPrompt').section() does not reach the
    // preset-scope assembly.
    if (promptMount !== 'per-turn') {
      ctx.inject(['systemPrompt'], (inner) => {
      const systemPrompt = inner.systemPrompt
      if (typeof systemPrompt.section !== 'function')
        throw new Error(
          "uniagent-decision-channel: promptMount 'section' requires the systemPrompt service "
          + 'in the preset scope; fix the mount or set config.promptMount to per-turn')
      systemPrompt.section({
        name: 'uniagent-prod:product-prompt',
        order: 100,
        interpolate: false,
        text: promptArtifact.staticText,
      })

      })
    }
    // PRF-004 — isolation probe (independent of prompt mount mode): assemble
    // this preset scope's prompt inputs once at mount and record the
    // inventory (names only) for the host-row read-only route. This is the
    // mechanical evidence for "the product session sees only product prompt
    // inputs" — never trust it, probe it.
    ctx.inject(['systemPrompt'], (inner) => {
      const systemPrompt = inner.systemPrompt
      if (typeof systemPrompt.assemble !== 'function') return
      systemPrompt.assemble({}).then(
        (assembly) => {
          state.promptProbe = {
            recordedAt: new Date().toISOString(),
            // Render drops empty sections; list only sections that carry
            // text, so the inventory reflects what the model can receive.
            sections: (assembly.sections ?? [])
              .filter(s => typeof s.text === 'string' && s.text.trim().length > 0)
              .map(s => s.name),
            contexts: (assembly.contexts ?? []).map(c => c.name),
            tools: (assembly.tools ?? []).map(t => t.name),
            variables: Object.keys(assembly.variables ?? {}).sort(),
          }
          log(`prompt probe recorded: sections=[${state.promptProbe.sections.join(', ')}] contexts=[${state.promptProbe.contexts.join(', ')}] tools=${state.promptProbe.tools.length}`)
        },
        (error) => {
          state.promptProbe = { error: String(error?.message ?? error) }
          log(`prompt probe FAILED: ${state.promptProbe.error}`)
        })
    })

    log(`session-scoped submit_decision published for agent-scope registration (schemaHash ${artifact.schemaHash.slice(0, 12)}…, promptRevision ${promptArtifact.revision})`)
    return
  }
  // Host mode: register the global fallback tool (non-preset sessions). The
  // restricted session replaces this by registering into the agent scope.
  ctx.tools.register(submitDecisionTool)
  ctx.connection.fetch.register({
    path: '/api/uniclaw-agent/prompt-probe',
    methods: ['GET'],
    requestBody: 'buffered',
    fetch: async () => {
      // PRF-004: read-only isolation-probe inventory (names only; no prompt
      // text leaves this route). absent = the session-scoped preset row has
      // not recorded yet (assembly pending or preset not mounted).
      if (state.promptProbe === null)
        return json(200, { recorded: false })
      if (state.promptProbe.error !== undefined)
        return json(200, { recorded: true, error: state.promptProbe.error })
      const { recordedAt, sections, contexts, tools, variables } = state.promptProbe
      return json(200, { recorded: true, recordedAt, sections, contexts, tools, variables })
    },
  })

  ctx.connection.fetch.register({
    path: '/api/uniclaw-agent/handshake',
    methods: ['POST'],
    requestBody: 'buffered',
    fetch: async (request) => {
      const body = await readJson(request)
      if (body === null) return fail(200, 'gateway/bad-request', 'handshake body must be a JSON object')
      const expected = body.protocol
      if (expected === null || typeof expected !== 'object') {
        return fail(200, 'handshake-rejected', 'handshake requires protocol stamp')
      }
      for (const field of ['protocolVersion', 'schemaVersion', 'schemaHash', 'profileId', 'profileVersion', 'capabilityManifestHash']) {
        if (typeof expected[field] !== 'string' || expected[field].length === 0) {
          return fail(200, 'handshake-rejected', `handshake-field-missing:${field}`)
        }
      }
      const frozen = {
        protocolVersion: PROTOCOL_VERSION,
        schemaVersion: SCHEMA_VERSION,
        profileId: PROFILE_ID,
        profileVersion: PROFILE_VERSION,
      }
      const codes = {
        protocolVersion: 'protocol-version-mismatch',
        schemaVersion: 'schema-version-mismatch',
        profileId: 'profile-id-mismatch',
        profileVersion: 'profile-version-mismatch',
      }
      for (const [field, code] of Object.entries(codes)) {
        if (expected[field] !== frozen[field]) {
          return fail(200, 'handshake-rejected', `${code}:${expected[field]}`)
        }
      }
      // B2: the Product caller's expected schemaHash vs the DSH LOCALLY
      // loaded artifact hash. Mismatch refuses the attachment.
      if (expected.schemaHash !== artifact.schemaHash) {
        return fail(200, 'handshake-rejected',
          `schema-hash-mismatch:product=${expected.schemaHash.slice(0, 12)}…:dsh-local=${artifact.schemaHash.slice(0, 12)}…`)
      }

      // The Product client sends its requested capability manifest as
      // `expectedCapabilities`; accept the historical `reportedCapabilities`
      // spelling only for compatibility with older peers.
      const capabilityInput = body.expectedCapabilities ?? body.reportedCapabilities
      const reported = Array.isArray(capabilityInput?.capabilities)
        ? [...new Set(capabilityInput.capabilities.map(String))].sort()
        : null
      if (reported === null) return fail(200, 'handshake-rejected', 'capability-manifest-missing')
      if (reported.join('\n') !== CAPABILITIES.join('\n')) {
        return fail(200, 'handshake-rejected', 'capability-manifest-mismatch')
      }
      const productSessionId = typeof body.productSessionId === 'string' ? body.productSessionId : null
      const productRunId = typeof body.productRunId === 'string' ? body.productRunId : null
      if (productSessionId === null || productRunId === null) {
        return fail(200, 'handshake-rejected', 'product-identity-missing')
      }
      if (state.attached !== null && state.attached.productSessionId !== productSessionId) {
        return fail(200, 'handshake-rejected', 'another-product-session-attached')
      }

      let dshSessionId = state.attached === null ? null : state.attached.dshSessionId
      if (dshSessionId === null) {
        if (controller === undefined || typeof controller.create !== 'function') {
          return fail(500, 'session-controller-unavailable', 'the host session controller is not available to this plugin')
        }
        if (workspaceRegistry === undefined || typeof workspaceRegistry.create !== 'function') {
          return fail(500, 'workspace-registry-unavailable', 'the host workspace registry is not available to this plugin')
        }
        try {
          // B1: the session MUST run under the restricted uniagent-prod preset.
          // It gets a DEDICATED empty workspace so the Product session never
          // reads or writes the operator's repository. NOTE: the workspace does
          // NOT scope tool visibility — MCP servers are attached at DSH_HOME
          // level, and their `cwd` config only sets the server process's
          // working directory. Tool visibility is owned entirely by the preset
          // composition (see the B1 note at the top of this file).
          const workspaceDir = workspacePath(productSessionId)
          mkdirSync(workspaceDir, { recursive: true })
          const workspace = await workspaceRegistry.create(workspaceDir, workspaceTitle)
          const created = await controller.create({ agentPreset: AGENT_PRESET_ID, workspaceId: workspace.id })
          dshSessionId = typeof created === 'string'
            ? created
            : (created && (created.sessionId ?? created.id))
          if (typeof dshSessionId !== 'string' || dshSessionId.length === 0) {
            return fail(200, 'dsh-session-create-failed', `unexpected session identity: ${JSON.stringify(created).slice(0, 120)}`)
          }
          if (typeof controller.rename === 'function') {
            await controller.rename({ sessionId: dshSessionId, title: sessionTitle })
          }
        } catch (error) {
          return fail(200, 'dsh-session-create-failed', String(error && error.message ? error.message : error))
        }
        // B1: register submit_decision into the AGENT's OWN scope. The preset
        // scope has already closed everything it INHERITS with
        // restrict({allow: []}) (@uniclaw/dsh-uniagent-restrict); a tool
        // registered in the agent's own layer is exempt from that filter, so
        // this is the one name the restricted session sees — regardless of
        // what MCP servers register globally, and regardless of when.
        if (state.sessionTool !== null) {
          try {
            const agent = await resolveSessionAgent(ctx, dshSessionId)
            const agentCtx = agent !== null && typeof agent === 'object' ? agent.ctx : undefined
            if (agentCtx === undefined || agentCtx.tools === undefined) {
              state.attached = null
              return fail(200, 'session-capability-binding-failed',
                'the restricted session agent exposes no scoped tools context')
            }
            state.sessionToolDisposer = agentCtx.tools.register(state.sessionTool)
            log('submit_decision registered into the restricted agent scope')
          } catch (error) {
            state.attached = null
            return fail(200, 'session-capability-binding-failed',
              String(error && error.message ? error.message : error))
          }
        }
        // B1: mechanically verify the ACTUAL model-visible tool catalog equals
        // the frozen manifest. This is a pure check: the preset composition
        // (restrict({allow: []}) closing the inherited surface) plus the
        // agent-scope registration of submit_decision is what makes it hold,
        // including for tools MCP servers registered after boot. Any drift
        // fails closed.
        try {
          const catalog = await sessionToolCatalog(ctx, dshSessionId)
          if (catalog.join('\n') !== EXPECTED_SESSION_TOOLS.join('\n')) {
            state.attached = null
            return fail(200, 'session-capability-mismatch',
              `actual=[${catalog.join(',')}] expected=[${EXPECTED_SESSION_TOOLS.join(',')}]`)
          }
          log('restricted session verified: visible tools =', catalog.join(','))
        } catch (error) {
          state.attached = null
          return fail(200, 'session-capability-verification-failed', String(error && error.message ? error.message : error))
        }
      }

      if (state.attached === null || state.attached.productRunId !== productRunId)
        state.initialCapabilitySelectionSettled = false
      state.attached = { productSessionId, productRunId, dshSessionId }
      recordEvent('attach', { productSessionId, productRunId, dshSessionId })
      log('handshake accepted', { productSessionId, productRunId, dshSessionId })
      return json(200, {
        accepted: true,
        protocol: {
          protocolVersion: PROTOCOL_VERSION,
          schemaVersion: SCHEMA_VERSION,
          schemaHash: artifact.schemaHash,
          profileId: PROFILE_ID,
          profileVersion: PROFILE_VERSION,
          capabilityManifestHash: expected.capabilityManifestHash,
        },
        reportedCapabilities: { capabilities: CAPABILITIES },
        runtimeCapabilities: EXPECTED_SESSION_TOOLS,
        runtimePreset: AGENT_PRESET_ID,
        dshSessionId,
      })
    },
  })

  ctx.connection.fetch.register({
    path: '/api/uniclaw-agent/slow',
    methods: ['POST'],
    requestBody: 'buffered',
    fetch: async (request) => {
      const body = await readJson(request)
      if (body === null) return fail(400, 'gateway/bad-request', 'slow body must be a JSON object')
      const attached = state.attached
      if (attached === null) return fail(200, 'channel-not-attached', 'handshake required before slow')
      if (typeof body.requestId !== 'string' || body.requestId.length === 0
        || typeof body.prompt !== 'string' || body.prompt.length === 0) {
        return fail(400, 'gateway/bad-request', 'slow requires requestId and prompt')
      }
      if (state.pending !== null || state.slowPending !== null)
        return fail(200, 'one-in-flight', 'a consultation is already in progress')
      if (controller === undefined || typeof controller.prompt !== 'function')
        return fail(500, 'session-controller-unavailable', 'the host session controller is not available to this plugin')

      const timeoutMs = Number.isInteger(body.turnTimeoutMs) && body.turnTimeoutMs > 0
        ? Math.min(body.turnTimeoutMs, MAX_TURN_TIMEOUT_MS) : DEFAULT_TURN_TIMEOUT_MS
      let settled
      const completion = new Promise(resolve => { settled = resolve })
      let slowSessionId
      try {
        const workspaceDir = workspacePath(`slow-${body.requestId}`)
        mkdirSync(workspaceDir, { recursive: true })
        if (workspaceRegistry === undefined || typeof workspaceRegistry.create !== 'function') {
          return fail(500, 'workspace-registry-unavailable', 'the host workspace registry is not available to this plugin')
        }
        const workspace = await workspaceRegistry.create(workspaceDir, workspaceTitle)
        const created = await controller.create({ agentPreset: 'uniclaw-slow', workspaceId: workspace.id })
        slowSessionId = typeof created === 'string' ? created : (created && (created.sessionId ?? created.id))
        if (typeof slowSessionId !== 'string' || slowSessionId.length === 0)
          return fail(200, 'dsh-session-create-failed', 'slow session identity missing')
        if (typeof controller.rename === 'function') {
          await controller.rename({ sessionId: slowSessionId, title: slowSessionTitle })
        }
      } catch (error) {
        return fail(200, 'dsh-session-create-failed', String(error && error.message ? error.message : error))
      }
      const slowPending = { requestId: body.requestId, sessionId: slowSessionId, resolve: settled, text: null, timer: null }
      state.slowPending = slowPending
      recordEvent('slow-start', { requestId: body.requestId })
      slowPending.timer = setTimeout(() => {
        if (state.slowPending === slowPending) {
          state.slowPending = null
          recordEvent('slow-failed', { requestId: body.requestId, code: 'turn-timeout' })
          settled({ ok: false, error: { code: 'turn-timeout', message: `slow turn deadline elapsed (${timeoutMs}ms)` } })
        }
      }, timeoutMs)
      try {
        if (body.model !== null && typeof body.model === 'object'
          && typeof body.model.provider === 'string' && typeof body.model.model === 'string'
          && typeof controller.selectModel === 'function') {
          await controller.selectModel({ sessionId: slowSessionId,
            provider: body.model.provider, model: body.model.model })
        }
        const content = [{ type: 'text', text: body.prompt }]
        if (body.image !== null && typeof body.image === 'object'
          && body.image.mediaType === 'image/png' && typeof body.image.data === 'string'
          && body.image.data.length > 0) {
          content.push({ type: 'image', mediaType: 'image/png', data: body.image.data,
            ...(typeof body.image.name === 'string' && body.image.name.length > 0
              ? { name: body.image.name } : {}) })
        }
        await controller.prompt({ requestId: `${body.requestId}-${Date.now()}`,
          sessionId: slowSessionId, mode: 'queue', content }, AbortSignal.timeout(timeoutMs))
      } catch (error) {
        if (state.slowPending === slowPending) {
          if (slowPending.timer !== null) clearTimeout(slowPending.timer)
          state.slowPending = null
        }
        return fail(200, 'prompt-failed', String(error && error.message ? error.message : error))
      }
      const captured = await completion
      if (captured.ok !== true) {
        recordEvent('slow-failed', { requestId: body.requestId, code: captured.error.code })
        return json(200, { requestId: body.requestId, text: null,
          error: captured.error.code, diagnostics: captured.error })
      }
      recordEvent('slow-complete', { requestId: body.requestId })
      return json(200, { requestId: body.requestId, text: captured.text,
        error: null, diagnostics: { source: 'assistant/message' } })
    },
  })

  ctx.connection.fetch.register({
    path: '/api/uniclaw-agent/consult',
    methods: ['POST'],
    requestBody: 'buffered',
    fetch: async (request) => {
      const body = await readJson(request)
      if (body === null) return fail(400, 'gateway/bad-request', 'consult body must be a JSON object')
      const attached = state.attached
      if (attached === null) return fail(200, 'channel-not-attached', 'handshake required before consult')
      if (typeof body.requestId !== 'string' || body.requestId.length === 0) {
        return fail(400, 'gateway/bad-request', 'consult requires requestId')
      }
      const context = body.context
      if (context === null || typeof context !== 'object' || typeof context.decisionId !== 'string') {
        return fail(400, 'gateway/bad-request', 'consult requires AgentDecisionContext with decisionId')
      }
      if (typeof body.productSessionId === 'string' && body.productSessionId !== attached.productSessionId) {
        return fail(200, 'product-mapping-mismatch', 'consult product session differs from attachment')
      }
      if (typeof body.productRunId === 'string' && body.productRunId !== attached.productRunId) {
        return fail(200, 'product-mapping-mismatch', 'consult product run differs from attachment')
      }
      if (typeof body.dshSessionId === 'string' && body.dshSessionId !== attached.dshSessionId) {
        return fail(200, 'dsh-session-mapping-mismatch', 'consult DSH session differs from attachment')
      }
      if (state.pending !== null || state.activeTurn !== null) {
        return fail(200, 'one-in-flight', 'a consultation is already in progress')
      }
      if (controller === undefined || typeof controller.prompt !== 'function') {
        return fail(500, 'session-controller-unavailable', 'the host session controller is not available to this plugin')
      }

      const timeoutMs = Number.isInteger(body.turnTimeoutMs) && body.turnTimeoutMs > 0
        ? Math.min(body.turnTimeoutMs, MAX_TURN_TIMEOUT_MS)
        : DEFAULT_TURN_TIMEOUT_MS
      const startedAt = Date.now()
      let settled
      const completion = new Promise((resolve) => { settled = resolve })
      let resolveTurnEnd
      const turnEnd = new Promise((resolve) => { resolveTurnEnd = resolve })
      state.pending = {
        requestId: body.requestId,
        decisionId: context.decisionId,
        resolve: settled,
        captured: null,
        lastError: null,
        timer: null,
      }
      const pending = state.pending
      const activeTurn = {
        sessionId: attached.dshSessionId,
        turnEnd,
        resolveTurnEnd,
        pending,
      }
      state.activeTurn = activeTurn
      recordEvent('consult-start', {
        requestId: body.requestId, decisionId: context.decisionId,
        dshSessionId: attached.dshSessionId,
        promptRevision: promptArtifact.revision,
      })
      pending.timer = setTimeout(() => {
        if (state.pending === pending && pending.captured === null) {
          pending.captured = { ok: false, error: { code: 'turn-timeout', message: `consult turn deadline elapsed (${timeoutMs}ms)` } }
          state.pending = null
          settled(pending.captured)
        }
      }, timeoutMs)

      try {
        if (body.model !== null && typeof body.model === 'object'
          && typeof body.model.provider === 'string' && typeof body.model.model === 'string'
          && typeof controller.selectModel === 'function') {
          await controller.selectModel({
            sessionId: attached.dshSessionId,
            provider: body.model.provider,
            model: body.model.model,
          })
        }
        const content = [{ type: 'text', text: consultationPrompt(body, artifact, promptArtifact, promptMount) }]
        if (body.image !== null && typeof body.image === 'object'
          && body.image.mediaType === 'image/png' && typeof body.image.data === 'string'
          && body.image.data.length > 0) {
          content.push({
            type: 'image',
            mediaType: 'image/png',
            data: body.image.data,
            ...(typeof body.image.name === 'string' && body.image.name.length > 0
              ? { name: body.image.name } : {}),
          })
        }
        await controller.prompt({
          requestId: `${body.requestId}-${startedAt}`,
          sessionId: attached.dshSessionId,
          mode: 'queue',
          content,
        }, AbortSignal.timeout(timeoutMs))
      } catch (error) {
        if (state.pending === pending) {
          if (pending.timer !== null) clearTimeout(pending.timer)
          state.pending = null
        }
        if (state.activeTurn === activeTurn) {
          state.activeTurn = null
          activeTurn.resolveTurnEnd()
        }
        recordEvent('consult-failed', { requestId: body.requestId, code: 'prompt-failed' })
        return fail(200, 'prompt-failed', String(error && error.message ? error.message : error))
      }

      const captured = await completion
      if (captured.ok === true) {
        // submit_decision resolves the semantic result before the DSH turn has
        // necessarily emitted turn/end. Keep the task session serialized until
        // that physical turn boundary is observed, otherwise a late tool call
        // from the previous turn can be captured by the next pending decision.
        // Keep the shared DSH session reusable without cancelling its physical
        // turn.  Provider/model latency can exceed the old short grace window;
        // wait up to the consultation deadline for the natural turn boundary.
        await Promise.race([
          activeTurn.turnEnd,
          new Promise(resolve => setTimeout(resolve, timeoutMs)),
        ])
      }
      const durationMs = Date.now() - startedAt
      if (captured.ok !== true) {
        recordEvent('consult-failed', {
          requestId: body.requestId, code: captured.error.code, durationMs,
        })
        log('consult failed', { requestId: body.requestId, code: captured.error.code, durationMs })
        return json(200, {
          requestId: body.requestId,
          generation: typeof body.generation === 'number' ? body.generation : 0,
          decision: null,
          error: captured.error.code,
          diagnostics: { ...captured.error, durationMs },
        })
      }
      log('consult captured', { requestId: body.requestId, kind: captured.task.payload.kind, durationMs })
      recordEvent('consult-complete', {
        requestId: body.requestId, decisionId: captured.task.payload.decisionId,
        decisionKind: captured.task.payload.kind, durationMs,
      })
      const decisionDiagnostics = {
        durationMs,
        source: 'submit_decision',
        schemaHash: artifact.schemaHash,
        productSessionId: attached.productSessionId,
        productRunId: attached.productRunId,
        decisionId: captured.task.payload.decisionId,
        ...(captured.task.payload.kind === 'policy'
          ? { policyId: captured.task.payload.proposal.policyId }
          : {}),
      }
      return json(200, {
        requestId: body.requestId,
        generation: typeof body.generation === 'number' ? body.generation : 0,
        task: captured.task,
        diagnostics: decisionDiagnostics,
      })
    },
  })

  ctx.connection.fetch.register({
    path: '/api/uniclaw-agent/abort',
    methods: ['POST'],
    requestBody: 'buffered',
    fetch: async (request) => {
      const body = await readJson(request)
      if (body === null) return fail(400, 'gateway/bad-request', 'abort body must be a JSON object')
      recordEvent('abort')
      retirePending()
      if (state.slowPending !== null) {
        const slow = state.slowPending
        if (slow.timer !== null) clearTimeout(slow.timer)
        state.slowPending = null
        slow.resolve({ ok: false, error: { code: 'turn-aborted', message: 'aborted before completion' } })
      }
      const attached = state.attached
      if (attached !== null && controller !== undefined && typeof controller.cancel === 'function') {
        try { controller.cancel({ sessionId: attached.dshSessionId }) } catch (error) { log('abort cancel failed', String(error)) }
      }
      return json(200, { aborted: true })
    },
  })

  // ---- S1: realization-private detach.
  ctx.connection.fetch.register({
    path: '/api/uniclaw-agent/detach',
    methods: ['POST'],
    requestBody: 'buffered',
    fetch: async (request) => {
      const body = await readJson(request)
      if (body === null) return fail(400, 'gateway/bad-request', 'detach body must be a JSON object')
      const hadAttachment = state.attached !== null
      recordEvent('detach', { hadAttachment })
      // Retire the pending turn first (late decisions are dropped with the
      // mapping), then release the ProductSession ↔ DshSession mapping and the
      // agent-scope tool registration that went with it.
      retirePending()
      if (state.slowPending !== null) {
        const slow = state.slowPending
        if (slow.timer !== null) clearTimeout(slow.timer)
        state.slowPending = null
        slow.resolve({ ok: false, error: { code: 'turn-aborted', message: 'detached before completion' } })
      }
      state.attached = null
      if (state.sessionToolDisposer !== null) {
        try { state.sessionToolDisposer() } catch (error) { log('tool release failed', String(error)) }
        state.sessionToolDisposer = null
      }
      log('detached', { hadAttachment })
      // Physical/realization cleanup only: no Run termination, no decision,
      // no Outcome write, no Product lifecycle change. The DSH session itself
      // stays alive (strategy continuity is realization state; the 1:1
      // mapping is re-established by the next handshake).
      return json(200, { detached: true, hadAttachment })
    },
  })

  // ---- PNL-001: control panel data service (host mount only). Static Client
  // half calls uniclawDecisionPanel.overview() through the Typert gateway's
  // SRC runtime resolution (same proven pattern as provider-usage). The
  // typert-protocol package exists in the DSH profile but not in the offline
  // test environment — degrade to ledger-only there. Probe with a SYNC
  // require.resolve first: a failed dynamic import inside the node --test
  // child leaves module-resolution sockets that stall the runner (~60s).
  if (!sessionScoped && panelProtocolAvailable()) {
    import('@deepseek-ai/dsh-typert-protocol').then(({ TypertRemoteService, Remote }) => {
      class DecisionPanelHost extends TypertRemoteService {
        static inject = []
        constructor(ctxRef) { super(ctxRef, 'uniclawDecisionPanel') }
        async overview() {
          const snap = panelSnapshot()
          return { success: true, queriedAt: new Date().toISOString(), ...snap }
        }
      }
      const panelDecorator = Remote('overview')
      let panelInitializer = null
      panelDecorator(DecisionPanelHost.prototype.overview, {
        kind: 'method', name: 'overview', private: false, static: false, metadata: undefined,
        access: { get: () => DecisionPanelHost.prototype.overview },
        addInitializer(fn) { panelInitializer = fn },
      })
      if (panelInitializer) panelInitializer.call(Object.create(DecisionPanelHost.prototype))
      new DecisionPanelHost(ctx)

      const PANEL_DESCRIPTOR = {
        id: 'uniclawDecisionPanel.overview',
        service: 'uniclawDecisionPanel',
        namespace: 'uniclawDecisionPanel',
        method: 'overview',
        invocation: { kind: 'direct' },
        parameters: [],
        result: {
          mode: 'strict',
          typeSymbol: 'uniclaw-decision-channel/JsonValue',
          // This typert build validates `create` (a schema FACTORY returning
          // { parse }) on the codec object itself; the older `schema: { parse }`
          // shape is rejected with "strict codec has no create() factory".
          create: () => ({ parse: (value) => value }),
        },
      }
      const registerTypertEndpoint = () => {
        const typert = ctx.get('typert')
        if (typert === undefined) return false
        try {
          typert.register({
            package: '@uniclaw/dsh-decision-channel',
            face: 'host',
            schemas: [],
            model: { services: [], events: [], objects: [] },
            invocations: [PANEL_DESCRIPTOR],
          })
          return true
        } catch (error) {
          const message = error && error.message ? error.message : String(error)
          if (/already registered|endpoint .* already|invocation id .* already/.test(message)) return true
          log('typert.register failed:', message)
          return false
        }
      }
      if (!registerTypertEndpoint()) {
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
      log('panel service registered: uniclawDecisionPanel.overview')
    }).catch((error) => {
      log('panel service unavailable (typert-protocol not importable):',
        String(error && error.message ? error.message : error).slice(0, 160))
    })
  }

  log(`registered: submit_decision tool + /api/uniclaw-agent/{handshake,consult,slow,abort,detach} (schemaHash ${artifact.schemaHash.slice(0, 12)}…)`)
}
