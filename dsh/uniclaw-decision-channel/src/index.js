/**
 * @uniclaw/dsh-decision-channel — UniClaw Product decision channel (AGT-002).
 *
 * Frozen seam (AGT-001 §1/§6.2/§14; AGT-002 Q21–Q23): DSH opens the physical
 * channel; the Kernel owns all Product semantics. This plugin mounts three
 * authenticated fetch routes on the /api lane —
 *
 *   POST /api/uniclaw-agent/handshake   (profile/protocol stamp validation)
 *   POST /api/uniclaw-agent/consult     (one decision turn; returns AgentDecision)
 *   POST /api/uniclaw-agent/abort       (mechanical current-turn abort)
 *
 * — and registers the single-tool `submit_decision` capability. A consult
 * drives a REAL DSH session (1 Product Session : 1 DSH Session) with the real
 * model: the prompt carries the serialized AgentDecisionContext; the model is
 * required to answer by calling submit_decision with the Product AgentDecision
 * JSON; the tool's execute() is the capture point. The plugin performs no
 * Product authority: no recovery, no Run/World reconstruction, no outcome.
 *
 * Fail-closed rules:
 *  - handshake rejects any protocol stamp mismatch (versions/schema/profile/
 *    capability hash) and any capability drift beyond the frozen manifest;
 *  - consult requires a prior accepted handshake for the same Product session
 *    and refuses concurrent turns;
 *  - turn deadline / no-submit / malformed decision JSON → error response,
 *    never an invented decision.
 *
 * This plugin is deliberately dependency-free (plain cordis plugin object),
 * so it installs into any profile without private-registry resolution.
 */

export const name = 'uniclaw-decision-channel'
export const inject = ['tools', 'connection', 'sessions', 'sessionController']

const PROTOCOL_VERSION = 'uniclaw.agent.protocol.v1'
const SCHEMA_VERSION = 'uniclaw.agent.schema.v1'
const PROFILE_ID = 'uniagent-prod'
const PROFILE_VERSION = '1'
const CAPABILITIES = ['submit_decision']
const CAPABILITY_MANIFEST_HASH = 'ba8855e41db09771081a7d217850bf99e263ddd84b30d5d076e4df245e1fd637'
const DEFAULT_TURN_TIMEOUT_MS = 60_000
const MAX_TURN_TIMEOUT_MS = 120_000

/** Active Product attachment: handshake state + the bound DSH session. */
const state = {
  attached: null, // { productSessionId, productRunId, dshSessionId }
  pending: null,  // { requestId, decisionId, resolve, timer, captured, turnStartSeq }
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

/** Extract the JSON object embedded in a model text answer (last {...} block). */
function extractJsonObject(text) {
  if (typeof text !== 'string' || text.length === 0) return null
  const start = text.lastIndexOf('{')
  if (start < 0) return null
  for (let end = text.indexOf('}', start); end !== -1; end = text.indexOf('}', end + 1)) {
    const candidate = text.slice(start, end + 1)
    try {
      const parsed = JSON.parse(candidate)
      if (parsed !== null && typeof parsed === 'object' && !Array.isArray(parsed)) return parsed
    } catch {
      /* keep scanning */
    }
  }
  return null
}

/** Validate the captured payload as an AgentDecision envelope. */
function validateDecision(payload) {
  if (payload === null || typeof payload !== 'object') return 'decision-not-an-object'
  const { kind, decisionId } = payload
  if (typeof decisionId !== 'string' || decisionId.length === 0) return 'decision-id-missing'
  if (kind === 'act') {
    const proposal = payload.proposal
    if (proposal === null || typeof proposal !== 'object') return 'act-proposal-missing'
    if (!Array.isArray(proposal.steps) || proposal.steps.length === 0) return 'act-steps-missing'
    for (const step of proposal.steps) {
      if (step === null || typeof step !== 'object') return 'act-step-malformed'
      if (typeof step.targetRole !== 'string' || step.targetRole.length === 0) return 'act-step-target-role-missing'
      if (typeof step.effectClass !== 'string' || step.effectClass.length === 0) return 'act-step-effect-class-missing'
    }
    return null
  }
  if (kind === 'noAction') {
    const proposal = payload.proposal
    if (proposal === null || typeof proposal !== 'object') return 'no-action-proposal-missing'
    if (typeof proposal.justification !== 'string') return 'no-action-justification-missing'
    return null
  }
  if (kind === 'defer') {
    const spec = payload.spec
    if (spec === null || typeof spec !== 'object') return 'defer-spec-missing'
    if (!Number.isInteger(spec.maxRounds) || spec.maxRounds < 1) return 'defer-max-rounds-missing'
    return null
  }
  if (kind === 'policy') {
    const proposal = payload.proposal
    if (proposal === null || typeof proposal !== 'object') return 'policy-proposal-missing'
    if (typeof proposal.policyId !== 'string' || proposal.policyId.length === 0) return 'policy-id-missing'
    if (!Array.isArray(proposal.match) || proposal.match.length === 0) return 'policy-match-missing'
    if (!Array.isArray(proposal.termination) || proposal.termination.length === 0) return 'policy-termination-missing'
    if (!Number.isInteger(proposal.maxApplications) || proposal.maxApplications < 1) return 'policy-max-applications-missing'
    return null
  }
  return 'unknown-decision-kind'
}

/**
 * Normalize model-shaped variants onto the Product AgentDecision JSON shape
 * ({kind, decisionId, proposal|spec}) — semantic fields stay mandatory and
 * are validated by validateDecision after normalization.
 */
function normalizeDecision(payload) {
  if (payload === null || typeof payload !== 'object') return payload
  const parseMaybe = (value) => {
    if (typeof value !== 'string') return value
    try { return JSON.parse(value) } catch { return value }
  }
  payload = { ...payload, proposal: parseMaybe(payload.proposal), spec: parseMaybe(payload.spec) }
  const kind = payload.kind
  if (kind === 'act') {
    const proposal = payload.proposal && typeof payload.proposal === 'object' ? payload.proposal : {}
    const asStepList = (value) => {
      if (Array.isArray(value)) return value
      if (value && typeof value === 'object') {
        if (Array.isArray(value.item)) return value.item
        if (value.item && typeof value.item === 'object') return [value.item]
        return [value]
      }
      return []
    }
    let steps = asStepList(proposal.steps)
    if (steps.length === 0) steps = asStepList(payload.steps)
    if (steps.length === 0 && (proposal.effect || proposal.target)) {
      const target = proposal.target && typeof proposal.target === 'object' ? proposal.target : {}
      steps = [{
        targetRole: target.role ?? target.targetRole ?? 'toggle',
        targetDescriptor: target.text ?? target.targetDescriptor ?? null,
        effectClass: proposal.effect ?? proposal.effectClass ?? 'tap',
        desiredState: proposal.desiredState ?? target.state ?? null,
      }]
    }
    return {
      kind,
      decisionId: payload.decisionId,
      proposal: { decisionId: proposal.decisionId ?? payload.decisionId, steps, justification: proposal.justification ?? payload.justification ?? null },
    }
  }
  if (kind === 'noAction') {
    const proposal = payload.proposal && typeof payload.proposal === 'object' ? payload.proposal : {}
    const justification = proposal.justification ?? payload.justification ?? ''
    const completion = proposal.completion ?? payload.completion ?? null
    return {
      kind,
      decisionId: payload.decisionId,
      proposal: { decisionId: proposal.decisionId ?? payload.decisionId, justification, ...(completion === null ? {} : { completion }) },
    }
  }
  if (kind === 'defer') {
    const spec = payload.spec && typeof payload.spec === 'object' ? payload.spec : {}
    const maxRounds = Number.isInteger(spec.maxRounds) ? spec.maxRounds
      : (Number.isInteger(payload.maxRounds) ? payload.maxRounds : null)
    const subject = typeof spec.subject === 'string' ? spec.subject
      : (typeof payload.subject === 'string' ? payload.subject : null)
    return {
      kind,
      decisionId: payload.decisionId,
      spec: { subject, maxRounds },
    }
  }
  if (kind === 'policy') {
    const source = payload.proposal && typeof payload.proposal === 'object' ? payload.proposal : {}
    const asPredicateList = (value) => {
      if (Array.isArray(value)) return value
      if (value && typeof value === 'object') {
        if (Array.isArray(value.item)) return value.item
        if (value.item && typeof value.item === 'object') return [value.item]
        return [value]
      }
      return []
    }
    const guards = Array.isArray(source.guards) ? source.guards : []
    const maxApplications = Number.isInteger(source.maxApplications)
      ? source.maxApplications
      : (typeof source.maxApplications === 'string' && /^\d+$/.test(source.maxApplications)
          ? Number.parseInt(source.maxApplications, 10) : null)
    const template = source.template && typeof source.template === 'object' ? source.template : {}
    return {
      kind,
      decisionId: payload.decisionId,
      proposal: {
        policyId: source.policyId ?? 'policy-1',
        match: asPredicateList(source.match),
        template: {
          targetRole: template.targetRole ?? 'toggle',
          targetDescriptor: template.targetDescriptor ?? null,
          effectClass: template.effectClass ?? 'tap',
          desiredState: template.desiredState ?? null,
        },
        termination: asPredicateList(source.termination),
        guards,
        maxApplications,
        justification: source.justification ?? null,
      },
    }
  }
  return payload
}

function stampOf(protocol) {
  if (protocol === null || typeof protocol !== 'object') return null
  return protocol
}

function capabilitiesOf(body) {
  const raw = body && (body.reportedCapabilities || body.expectedCapabilities)
  const list = raw && Array.isArray(raw.capabilities) ? raw.capabilities : null
  if (list === null) return null
  return [...new Set(list.map(c => String(c).trim()).filter(c => c.length > 0))].sort()
}

/** Prompt the model: context in, submit_decision out. */
function consultationPrompt(request) {
  return [
    'You are the UniAgent decision component of the UniClaw Product runtime.',
    'Answer this consultation by calling the submit_decision tool exactly once,',
    'with EXACTLY this payload shape (Product AgentDecision contract):',
    '',
    '- act:      {"kind":"act","decisionId":"<context.decisionId>","proposal":{"steps":[{"targetRole":"<element role>","targetDescriptor":"<element text or null>","effectClass":"<one of context.allowedEffects>","desiredState":"<target value or null>"}],"justification":"<short reason>"}}',
    '- noAction: {"kind":"noAction","decisionId":"<id>","proposal":{"justification":"<why no action>","completion":null}}',
    '- defer:    {"kind":"defer","decisionId":"<id>","spec":{"subject":"<claim subject or null>","maxRounds":<1..4>}}',
    '- policy:   {"kind":"policy","decisionId":"<id>","proposal":{"policyId":"policy-<n>","match":[{"kind":"ClaimEquals","subject":"<s>","value":"<v>"}],"template":{"targetRole":"<role>","targetDescriptor":null,"effectClass":"<allowed>","desiredState":"<v>"},"termination":[{"kind":"ClaimEquals","subject":"<s>","value":"<v>"}],"guards":[],"maxApplications":<1..4>,"justification":"<reason>"}}',
    '',
    'CRITICAL: the proposal is a JSON OBJECT (never a string); act uses proposal.steps[]',
    'with fields targetRole/targetDescriptor/effectClass/desiredState — NOT effect/target.',
    'decisionId MUST equal context.decisionId. Choose the semantically correct kind:',
    'act when concrete steps now; policy for bounded repeat-apply rules; defer to wait',
    'for observations; noAction when the goal is already satisfied.',
    'Respond with the tool call only — no prose, no other tool.',
    '',
    '=== AgentDecisionContext (JSON) ===',
    JSON.stringify(request.context, null, 2),
  ].join('\n')
}

export function apply(ctx) {
  const log = (...parts) => console.info('[uniclaw-decision-channel]', ...parts)

  // ---- submit_decision tool: the frozen single-tool product output surface.
  ctx.tools.register({
    name: 'submit_decision',
    description:
      'Submit one UniAgent decision (AgentDecision JSON: kind act/noAction/defer/policy). ' +
      'This is the only approved Product output; decisionId must equal the context decisionId.',
    parameters: {
      kind: { type: 'string', enum: ['act', 'noAction', 'defer', 'policy'], required: true, description: 'Decision kind' },
      decisionId: { type: 'string', required: true, description: 'Correlation id from the context' },
      proposal: { type: 'json', required: false, description: 'act/noAction/policy proposal payload' },
      spec: { type: 'json', required: false, description: 'defer ObserveSpec {subject, maxRounds}' },
    },
    output: {
      schema: {
        type: 'object',
        properties: {
          accepted: { type: 'boolean' },
          reason: { type: 'string' },
        },
        additionalProperties: false,
      },
      render: (value) => (value && value.accepted
        ? `submit_decision accepted: ${value.reason ?? ''}`
        : `submit_decision rejected: ${value && value.reason ? value.reason : 'unknown'}`),
    },
    execute: (args) => {
      const pending = state.pending
      console.info('[uniclaw-decision-channel] submit_decision raw args:', JSON.stringify(args).slice(0, 800))
      if (pending === null) {
        return { accepted: false, reason: 'no-pending-consultation' }
      }
      if (pending.captured !== null) {
        return { accepted: false, reason: 'decision-already-captured' }
      }
      if (typeof args.decisionId !== 'string' || args.decisionId !== pending.decisionId) {
        pending.captured = { ok: false, error: { code: 'decision-id-mismatch', message: `expected ${pending.decisionId}` } }
      } else {
        const normalized = normalizeDecision(args)
        const invalid = validateDecision(normalized)
        pending.captured = invalid === null
          ? { ok: true, decision: normalized }
          : { ok: false, error: { code: invalid, message: `submit_decision payload rejected: ${invalid}` } }
      }
      if (pending.timer !== null) clearTimeout(pending.timer)
      const resolve = pending.resolve
      state.pending = null
      resolve(pending.captured)
      return { accepted: pending.captured.ok, reason: pending.captured.ok ? 'decision captured' : pending.captured.error.code }
    },
  })

  // ---- session journal watch: turn boundaries + assistant fallback text.
  ctx.on('session/event', (session, event) => {
    const attached = state.attached
    if (attached === null || session.id !== attached.dshSessionId) return
    const pending = state.pending
    const type = event && typeof event.type === 'string' ? event.type : ''
    if (type === 'turn/end' && pending !== null && pending.captured === null) {
      // Turn finished without a submit_decision capture: fail closed, but let
      // the consult's own deadline race decide (assistant fallback is parsed
      // first if present in the same event).
      const data = event.data && typeof event.data === 'object' ? event.data : {}
      const assistantText = typeof data.text === 'string' ? data.text : null
      const embedded = assistantText === null ? null : extractJsonObject(assistantText)
      if (embedded !== null) {
        const invalid = validateDecision(normalizeDecision(embedded))
        pending.captured = invalid === null
          ? { ok: true, decision: normalizeDecision(embedded) }
          : { ok: false, error: { code: invalid, message: 'assistant-embedded decision invalid' } }
      } else {
        pending.captured = { ok: false, error: { code: 'no-submit-decision', message: 'model finished the turn without calling submit_decision' } }
      }
      if (pending.timer !== null) clearTimeout(pending.timer)
      state.pending = null
      pending.resolve(pending.captured)
    }
  })

  // ---- Product channel routes on the authenticated /api lane.
  const controller = ctx.get('sessionController')

  ctx.connection.fetch.register({
    path: '/api/uniclaw-agent/handshake',
    methods: ['POST'],
    requestBody: 'buffered',
    fetch: async (request) => {
      const body = await readJson(request)
      if (body === null) return fail(400, 'gateway/bad-request', 'handshake body must be a JSON object')
      const expected = stampOf(body.protocol)
      if (expected === null) return fail(400, 'gateway/bad-request', 'handshake requires protocol stamp')
      for (const field of ['protocolVersion', 'schemaVersion', 'schemaHash', 'profileId', 'profileVersion', 'capabilityManifestHash']) {
        if (typeof expected[field] !== 'string' || expected[field].length === 0) {
          return fail(200, 'handshake-rejected', `handshake-field-missing:${field}`)
        }
      }
      // The channel's identity is frozen here; the caller must expect exactly it.
      const frozen = {
        protocolVersion: PROTOCOL_VERSION,
        schemaVersion: SCHEMA_VERSION,
        profileId: PROFILE_ID,
        profileVersion: PROFILE_VERSION,
        capabilityManifestHash: CAPABILITY_MANIFEST_HASH,
      }
      const codes = {
        protocolVersion: 'protocol-version-mismatch',
        schemaVersion: 'schema-version-mismatch',
        profileId: 'profile-id-mismatch',
        profileVersion: 'profile-version-mismatch',
        capabilityManifestHash: 'capability-manifest-hash-mismatch',
      }
      for (const [field, code] of Object.entries(codes)) {
        if (expected[field] !== frozen[field]) {
          return fail(200, 'handshake-rejected', `${code}:${expected[field]}`)
        }
      }

      const reported = capabilitiesOf(body)
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

      // 1 Product Session : 1 DSH Session (AGT-002 §7). Create lazily on
      // first handshake; subsequent handshakes for the same session re-report.
      let dshSessionId = state.attached === null ? null : state.attached.dshSessionId
      if (dshSessionId === null) {
        if (controller === undefined || typeof controller.create !== 'function') {
          return fail(500, 'session-controller-unavailable', 'the host session controller is not available to this plugin')
        }
        try {
          // Same public path the web UI drives: controller.create wires
          // workspace, catalog, and agent resolution end-to-end.
          const created = await controller.create({})
          dshSessionId = typeof created === 'string'
            ? created
            : (created && (created.sessionId ?? created.id))
          if (typeof dshSessionId !== 'string' || dshSessionId.length === 0) {
            return fail(200, 'dsh-session-create-failed', `unexpected session identity: ${JSON.stringify(created).slice(0, 120)}`)
          }
        } catch (error) {
          return fail(200, 'dsh-session-create-failed', String(error && error.message ? error.message : error))
        }
      }
      state.attached = { productSessionId, productRunId, dshSessionId }
      log('handshake accepted', { productSessionId, productRunId, dshSessionId })
      return json(200, {
        accepted: true,
        protocol: {
          protocolVersion: PROTOCOL_VERSION,
          schemaVersion: SCHEMA_VERSION,
          schemaHash: expected.schemaHash,
          profileId: PROFILE_ID,
          profileVersion: PROFILE_VERSION,
          capabilityManifestHash: CAPABILITY_MANIFEST_HASH,
        },
        reportedCapabilities: { capabilities: CAPABILITIES },
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
          content: [{ type: 'text', text: consultationPrompt(body) }],
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
        diagnostics: { durationMs, source: 'submit_decision' },
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
      const attached = state.attached
      const pending = state.pending
      if (pending !== null) {
        if (pending.timer !== null) clearTimeout(pending.timer)
        state.pending = null
        if (pending.captured === null) {
          pending.captured = { ok: false, error: { code: 'turn-aborted', message: 'aborted before completion' } }
        }
        pending.resolve(pending.captured)
      }
      if (attached !== null && controller !== undefined && typeof controller.cancel === 'function') {
        try {
          controller.cancel({ sessionId: attached.dshSessionId })
        } catch (error) {
          log('abort cancel failed', String(error))
        }
      }
      return json(200, { aborted: true })
    },
  })

  log('registered: submit_decision tool + /api/uniclaw-agent/{handshake,consult,abort}')
}
