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
 *   POST /api/uniclaw-agent/abort
 *   POST /api/uniclaw-agent/detach
 */

import { createHash } from 'node:crypto'
import { mkdirSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'
import { homedir } from 'node:os'

export const name = 'uniclaw-decision-channel'
export const inject = ['tools', 'connection', 'sessionController', 'agents']

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

/** Derive the submit_decision tool parameter map from the artifact itself. */
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
  return {
    kind: {
      type: 'string',
      enum: kinds,
      required: true,
      description: `AgentDecision kind (derived from the generated Product protocol artifact, schemaHash ${artifact.schemaHash.slice(0, 12)}…)`,
    },
    decisionId: { type: 'string', required: true, description: 'Correlation id; MUST equal context.decisionId.' },
    ...(requiresProposal
      ? { proposal: { type: 'json', required: false, description: 'act/noAction/policy proposal payload (exact Product schema shape).' } }
      : {}),
    ...(requiresSpec
      ? { spec: { type: 'json', required: false, description: 'defer ObserveSpec {subject, maxRounds} (exact Product schema shape).' } }
      : {}),
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
const MAX_TURN_TIMEOUT_MS = 120_000

const state = {
  attached: null, // { productSessionId, productRunId, dshSessionId }
  pending: null,  // { requestId, decisionId, resolve, timer, captured }
  sessionTool: null, // tool definition published by the session-scoped mount (B1)
  sessionToolDisposer: null, // live agent-scope registration for the attached session
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
function consultationPrompt(request, artifact) {
  return [
    'You are the UniAgent decision component of the UniClaw Product runtime.',
    'Answer this consultation by calling the submit_decision tool exactly once,',
    'with a payload that validates against the Product AgentDecision schema',
    `(protocol ${PROTOCOL_VERSION}, schema ${SCHEMA_VERSION}, schemaHash ${artifact.schemaHash}).`,
    '',
    'Payload shape per kind (exact field names; every proposal ALSO carries',
    'decisionId — the same value as the top-level decisionId):',
    '- act:      proposal {decisionId, steps[] = {targetRole, targetDescriptor?, effectClass, desiredState?}, justification?}',
    '- noAction: proposal {decisionId, justification, completion? {basis, checklist[]}}',
    '- defer:    spec {subject?, maxRounds}  (maxRounds: integer >= 1)',
    '- policy:   proposal {policyId, match[], template {targetRole, targetDescriptor?, effectClass, desiredState?}, termination[], guards[], maxApplications, justification?}',
    '  predicates: {kind:"ClaimEquals",subject,value} | {kind:"ClaimInSet",subject,values[]}',
    '',
    'CRITICAL: proposal/spec are JSON OBJECTS (never strings); lists are JSON',
    'arrays. decisionId MUST equal context.decisionId. Choose the semantically',
    'correct kind for the context. Respond with the tool call only — no prose.',
    '',
    '=== AgentDecisionContext (JSON) ===',
    JSON.stringify(request.context, null, 2),
  ].join('\n')
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
  // Root for the Product sessions' dedicated workspaces. Overridable so an
  // operator (or a test) can place them outside the default DSH home.
  const workspaceRoot = config !== null && typeof config === 'object'
    && typeof config.workspaceRoot === 'string' && config.workspaceRoot.length > 0
    ? config.workspaceRoot
    : join(homedir(), '.dsh', 'uniagent-workspaces')

  // ---- submit_decision tool: parameters derived from the artifact (B2).
  // Built once; WHERE it is registered depends on the mount mode (below),
  // because the preset's own restriction filter must not strip it.
  const submitDecisionTool = {
    name: 'submit_decision',
    description:
      'Submit one UniAgent decision (Product AgentDecision JSON for the current consultation). ' +
      'This is the only approved Product output; decisionId must equal the context decisionId.',
    parameters: toolParametersFromSchema(artifact),
    output: {
      schema: {
        type: 'object',
        properties: { accepted: { type: 'boolean' }, reason: { type: 'string' } },
        additionalProperties: false,
      },
      render: (value) => (value && value.accepted
        ? `submit_decision accepted: ${value.reason ?? ''}`
        : `submit_decision rejected: ${value && value.reason ? value.reason : 'unknown'}`),
    },
    execute: (args) => {
      const pending = state.pending
      if (pending === null) return { accepted: false, reason: 'no-pending-consultation' }
      if (pending.captured !== null) return { accepted: false, reason: 'decision-already-captured' }
      if (typeof args.decisionId !== 'string' || args.decisionId !== pending.decisionId) {
        pending.captured = { ok: false, error: { code: 'decision-id-mismatch', message: `expected ${pending.decisionId}` } }
      } else {
        // B3: representation normalization only, then schema-driven validation.
        const normalized = normalizeToSchema(args, artifact.schema.$defs.AgentDecision, artifact.schema)
        const errors = []
        validateToSchema(normalized, artifact.schema.$defs.AgentDecision, artifact.schema, 'decision', errors)
        pending.captured = errors.length === 0
          ? { ok: true, decision: normalized }
          : { ok: false, error: { code: 'decision-schema-invalid', message: errors.slice(0, 4).join('; ') } }
      }
      if (pending.timer !== null) clearTimeout(pending.timer)
      const resolve = pending.resolve
      state.pending = null
      resolve(pending.captured)
      return { accepted: pending.captured.ok, reason: pending.captured.ok ? 'decision captured' : pending.captured.error.code }
    },
  }

  // ---- session journal watch: B3 — turn end WITHOUT capture is a failure.
  ctx.on('session/event', (session, event) => {
    const attached = state.attached
    if (attached === null || session.id !== attached.dshSessionId) return
    const pending = state.pending
    const type = event && typeof event.type === 'string' ? event.type : ''
    if (type === 'turn/end' && pending !== null && pending.captured === null) {
      // The model finished its turn without calling submit_decision. Prose is
      // trajectory evidence only — NEVER promoted to a Product decision.
      pending.captured = {
        ok: false,
        error: { code: 'no-submit-decision', message: 'model finished the turn without calling submit_decision' },
      }
      if (pending.timer !== null) clearTimeout(pending.timer)
      state.pending = null
      pending.resolve(pending.captured)
    }
  })

  const retirePending = () => {
    const pending = state.pending
    if (pending === null) return
    if (pending.timer !== null) clearTimeout(pending.timer)
    state.pending = null
    if (pending.captured === null) {
      pending.captured = { ok: false, error: { code: 'turn-aborted', message: 'aborted before completion' } }
    }
    pending.resolve(pending.captured)
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
    log(`session-scoped submit_decision published for agent-scope registration (schemaHash ${artifact.schemaHash.slice(0, 12)}…)`)
    return
  }
  // Host mode: register the global fallback tool (non-preset sessions). The
  // restricted session replaces this by registering into the agent scope.
  ctx.tools.register(submitDecisionTool)
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

      const reported = Array.isArray(body.reportedCapabilities?.capabilities)
        ? [...new Set(body.reportedCapabilities.capabilities.map(String))].sort()
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
        try {
          // B1: the session MUST run under the restricted uniagent-prod preset.
          // It gets a DEDICATED empty workspace so the Product session never
          // reads or writes the operator's repository. NOTE: the workspace does
          // NOT scope tool visibility — MCP servers are attached at DSH_HOME
          // level, and their `cwd` config only sets the server process's
          // working directory. Tool visibility is owned entirely by the preset
          // composition (see the B1 note at the top of this file).
          const workspaceDir = join(workspaceRoot,
            productSessionId.replace(/[^a-zA-Z0-9._-]/g, '_'))
          mkdirSync(workspaceDir, { recursive: true })
          const created = await controller.create({ agentPreset: AGENT_PRESET_ID, cwd: workspaceDir })
          dshSessionId = typeof created === 'string'
            ? created
            : (created && (created.sessionId ?? created.id))
          if (typeof dshSessionId !== 'string' || dshSessionId.length === 0) {
            return fail(200, 'dsh-session-create-failed', `unexpected session identity: ${JSON.stringify(created).slice(0, 120)}`)
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

      state.attached = { productSessionId, productRunId, dshSessionId }
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
      if (state.pending !== null) {
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
      state.pending = {
        requestId: body.requestId,
        decisionId: context.decisionId,
        resolve: settled,
        captured: null,
        timer: null,
      }
      const pending = state.pending
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
        await controller.prompt({
          requestId: `${body.requestId}-${startedAt}`,
          sessionId: attached.dshSessionId,
          mode: 'queue',
          content: [{ type: 'text', text: consultationPrompt(body, artifact) }],
        }, AbortSignal.timeout(timeoutMs))
      } catch (error) {
        if (state.pending === pending) {
          if (pending.timer !== null) clearTimeout(pending.timer)
          state.pending = null
        }
        return fail(200, 'prompt-failed', String(error && error.message ? error.message : error))
      }

      const captured = await completion
      const durationMs = Date.now() - startedAt
      if (captured.ok !== true) {
        log('consult failed', { requestId: body.requestId, code: captured.error.code, durationMs })
        return json(200, {
          requestId: body.requestId,
          generation: typeof body.generation === 'number' ? body.generation : 0,
          decision: null,
          error: captured.error.code,
          diagnostics: { ...captured.error, durationMs },
        })
      }
      log('consult captured', { requestId: body.requestId, kind: captured.decision.kind, durationMs })
      return json(200, {
        requestId: body.requestId,
        generation: typeof body.generation === 'number' ? body.generation : 0,
        decision: captured.decision,
        diagnostics: { durationMs, source: 'submit_decision', schemaHash: artifact.schemaHash },
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
      retirePending()
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
      // Retire the pending turn first (late decisions are dropped with the
      // mapping), then release the ProductSession ↔ DshSession mapping and the
      // agent-scope tool registration that went with it.
      retirePending()
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

  log(`registered: submit_decision tool + /api/uniclaw-agent/{handshake,consult,abort,detach} (schemaHash ${artifact.schemaHash.slice(0, 12)}…)`)
}
