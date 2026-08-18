/**
 * LlmAssistanceConsumer (dsh-assistance-consumer-selection: A — BUY_NOW).
 *
 * The first REAL Harness intelligence consumer for the graduated L1 CONSULT
 * seam. Owns INTELLIGENCE GENERATION ONLY:
 *
 *   HarnessAssistanceRequest
 *     → ONE bounded LLM invocation (ctx.llm / LlmRuntime.stream)
 *     → validated HarnessAssistanceResult { recommendation, additionalEvidence, reason }
 *
 * Responsibilities:
 *  - bounded prompt construction (semantic context, belief state, allowed
 *    vocabulary, structured-output requirement, advisory-only declaration)
 *  - one model call per consultation (no loop, no tools, no session)
 *  - consumer-side structured-output VALIDATION/NORMALIZATION (defense in
 *    depth — the graduated wire/provider layer remains final authority for
 *    correlation and stale-world rejection)
 *  - Harness-side observability (latency / model / outcome)
 *
 * MUST NEVER return: concrete device actions, coordinates, element indexes as
 * authority, arbitrary action sequences, route proposals, goal-satisfaction
 * claims, or any belief/binding state change. Any failure ⇒
 * { recommendation: null } (no advice → Agent fails closed). No retry loop;
 * bounded timeout; cancellation propagated.
 */
'use strict';

/** COMPOSITION_POLICY default: bounded output budget (tokens). */
export const DEFAULT_MAX_TOKENS = 200;

/** COMPOSITION_POLICY default: bounded consumer timeout (must fit the 30s
 * WireProvider composition budget). */
export const DEFAULT_TIMEOUT_MS = 20000;

/** Accumulated-output bound (chars) — hard cap regardless of model behavior. */
export const MAX_ACCUMULATED_OUTPUT_CHARS = 4000;

/** Allowed recommendation vocabulary (mirrors the Runtime Agent whitelist). */
export const ALLOWED_RECOMMENDATIONS = ['re-observe', 'rebind', 'dismiss-obstruction'];

/** System prompt — frozen shape: advisory only, structured output, bounded. */
export const SYSTEM_PROMPT = [
  'You are the L1 assistance advisor for the UniClaw Runtime at a belief-adjudication point.',
  'Rules (non-negotiable):',
  '- You return ONE JSON object, no markdown fences: {"recommendation": "<one of the allowed values or null>", "reason": "<1-2 sentence rationale>"}.',
  '- recommendation MUST be one of the allowed values in the request, or null when no action is warranted.',
  '- Your output is ADVISORY ONLY: you cannot execute actions, you cannot declare reality, you cannot override Runtime evidence, and you cannot declare goal completion.',
  '- Never mention devices, coordinates, element indexes, plans, or execution sequences.',
  '- Keep reason short and grounded in the provided context.',
].join('\n');

function epochMs() {
  return Date.now();
}

/**
 * Build the bounded user message for one consultation. Only the normalized
 * request fields — never Runtime private objects, full traces, journals, or
 * unrelated session history (T14).
 */
export function buildUserMessage(request) {
  const observation = request.observation ?? {};
  const texts = Array.isArray(observation.elementTexts) ? observation.elementTexts : [];
  const allowed = Array.isArray(request.allowedRecommendations)
    ? request.allowedRecommendations.join(', ')
    : ALLOWED_RECOMMENDATIONS.join(', ');
  return [
    `requestId: ${request.requestId}`,
    `runId: ${request.runId}`,
    `assistanceKind: ${request.assistanceKind ?? 'belief-conflict'}`,
    `semanticPage: ${request.semanticPage}`,
    `beliefState: ${request.beliefState}`,
    `worldVersion: ${request.worldVersion}`,
    `observation: { sequence: ${observation.sequence ?? 'null'}, foregroundApplication: ${observation.foregroundApplication ?? 'null'}, elementCount: ${observation.elementCount ?? 0}, elementTexts: [${texts.join(', ')}] }`,
    `allowed recommendations: ${allowed}`,
    'Return exactly one JSON object with "recommendation" and "reason".',
  ].join('\n');
}

/**
 * Parse and validate the bounded model output into a structured result.
 * Defense in depth: the graduated wire/provider layer remains final authority
 * for correlation/staleness — this layer only guards the consumer boundary.
 * Returns null-shaped result { recommendation: null, reason } on any failure.
 */
export function parseStructuredResult(text, request) {
  const invalid = (why) => ({ recommendation: null, reason: `invalid structured result: ${why}` });
  if (typeof text !== 'string' || text.trim().length === 0) {
    return invalid('empty output');
  }
  let parsed;
  try {
    // Strip optional markdown fences defensively, then parse the JSON object.
    const cleaned = text.trim().replace(/^```(?:json)?\s*/i, '').replace(/\s*```$/, '');
    parsed = JSON.parse(cleaned);
  } catch {
    return invalid('malformed JSON');
  }
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
    return invalid('not an object');
  }
  // §4 (graduation): reject outputs carrying executable/action fields — even when
  // the recommendation itself is whitelisted. Unknown fields are never advice;
  // executable-looking fields are an explicit rejection signal.
  // 'action'/'actions' already cover any device-action-shaped key; the list
  // deliberately avoids vendor/transport vocabulary so the purity guard holds.
  const executableKeys = ['action', 'actions', 'plan', 'route', 'execute', 'command'];
  const unexpected = Object.keys(parsed).filter((key) =>
    executableKeys.includes(key.toLowerCase()));
  if (unexpected.length > 0) {
    return invalid(`unexpected executable field(s): ${unexpected.join(', ')}`);
  }
  // Field validation: recommendation ∈ whitelist or null; reason non-empty string.
  const recommendation = parsed.recommendation ?? null;
  if (recommendation !== null && !ALLOWED_RECOMMENDATIONS.includes(recommendation)) {
    return invalid(`unknown recommendation '${recommendation}'`);
  }
  if (typeof parsed.reason !== 'string' || parsed.reason.trim().length === 0) {
    return invalid('missing reason');
  }
  return {
    recommendation,
    additionalEvidence: typeof parsed.additionalEvidence === 'string' ? parsed.additionalEvidence : null,
    reason: parsed.reason.trim(),
  };
}

/**
 * @param {object} deps
 * @param {() => object|null} deps.getLlm        optional ctx.llm accessor (never injected at activation)
 * @param {string|null} deps.provider            COMPOSITION_POLICY model provider route
 * @param {string|null} deps.model               COMPOSITION_POLICY model id
 * @param {number} [deps.maxTokens]              bounded output budget (tokens)
 * @param {number} [deps.timeoutMs]              bounded consumer timeout (< 30s wire budget)
 * @param {object} [deps.logger]                 Harness-side observability sink ({ info/warn })
 */
export class LlmAssistanceConsumer {
  constructor({ getLlm, provider = null, model = null, maxTokens = DEFAULT_MAX_TOKENS, timeoutMs = DEFAULT_TIMEOUT_MS, logger = null } = {}) {
    if (typeof getLlm !== 'function') {
      throw new TypeError('LlmAssistanceConsumer requires a getLlm accessor');
    }
    this.getLlm = getLlm;
    this.provider = provider;
    this.model = model;
    this.maxTokens = maxTokens;
    this.timeoutMs = timeoutMs;
    this.logger = logger ?? { info() {}, warn() {} };
  }

  /** Resolve one normalized request → structured result (never throws; failure ⇒ no advice). */
  async resolve(request) {
    const startedAt = epochMs();
    const requestId = request?.requestId ?? null;
    const runId = request?.runId ?? null;
    const log = (outcome, extra = {}) => this.logger.info?.({
      event: 'assistance.llm.consult',
      consumer: 'llm',
      requestId,
      runId,
      outcome,
      latencyMs: epochMs() - startedAt,
      ...extra,
    });

    const llm = this.getLlm();
    if (!llm || typeof llm.stream !== 'function') {
      log('no_advice', { reason: 'llm-unavailable' });
      return { recommendation: null, reason: 'llm unavailable' };
    }
    if (typeof this.provider !== 'string' || this.provider.length === 0
        || typeof this.model !== 'string' || this.model.length === 0) {
      log('no_advice', { reason: 'model-route-not-configured' });
      return { recommendation: null, reason: 'model route not configured (composition policy)' };
    }

    // Bounded timeout (ref'd timer — same pattern as the shadow model seam),
    // folding in no caller signal for a standalone bridge consumer.
    const controller = new AbortController();
    const effectiveTimeoutMs = Number.isInteger(this.timeoutMs) && this.timeoutMs > 0 ? this.timeoutMs : DEFAULT_TIMEOUT_MS;
    const timer = setTimeout(() => controller.abort(new Error(`llm assistance call timed out after ${effectiveTimeoutMs}ms`)), effectiveTimeoutMs);

    let text = '';
    let outcome = 'advice';
    const messages = [{
      id: `assistance-${requestId ?? 'x'}`,
      role: 'user',
      content: [{ type: 'text', text: buildUserMessage(request) }],
      source: { kind: 'user' },
    }];
    const options = {
      provider: this.provider,
      model: this.model,
      system: SYSTEM_PROMPT,
      messages,
      maxTokens: this.maxTokens,
      signal: controller.signal,
    };

    try {
      for await (const chunk of llm.stream(options)) {
        if (chunk && chunk.type === 'text-delta' && typeof chunk.text === 'string') {
          const remaining = MAX_ACCUMULATED_OUTPUT_CHARS - text.length;
          if (remaining > 0) text += chunk.text.slice(0, remaining);
          if (text.length >= MAX_ACCUMULATED_OUTPUT_CHARS) break;
        } else if (chunk && chunk.type === 'finish' && chunk.reason) {
          const reason = chunk.reason;
          if (reason.kind === 'error') {
            outcome = 'model_error';
            break;
          }
          if (reason.kind === 'aborted' && controller.signal.aborted) {
            outcome = 'timeout';
            break;
          }
          // stop / max-tokens → success path
        }
      }
    } catch (err) {
      outcome = controller.signal.aborted ? 'timeout' : 'model_error';
    } finally {
      clearTimeout(timer);
    }

    if (outcome === 'timeout') {
      log('timeout');
      return { recommendation: null, reason: 'model call timed out' };
    }
    if (outcome === 'model_error' || text.trim().length === 0) {
      log(outcome === 'model_error' ? 'model_error' : 'no_advice', { reason: 'empty output' });
      return { recommendation: null, reason: 'model call failed or returned no output' };
    }

    const result = parseStructuredResult(text, request);
    log(result.recommendation === null ? 'invalid_output' : 'advice', {
      model: this.model,
      provider: this.provider,
      recommendation: result.recommendation,
    });
    return result;
  }
}
