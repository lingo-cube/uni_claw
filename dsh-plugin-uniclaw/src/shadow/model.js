/**
 * One-shot model invocation seam (design §10, D6).
 *
 * Shadow builds its OWN `GenerateOptions` and calls `ctx.llm.stream` exactly
 * once per analysis (0-or-1 model calls; deterministic retrieval precedes the
 * call and is zero-model). NO loop semantics: no derived history, no agent
 * loop, no tool loop, no loop marker, `purpose` left unset, ZERO tools.
 *
 * Failure mapping (§13): not-configured / model-unavailable when there is no
 * usable model route; `timeout` + `model-timeout` on AbortSignal.timeout;
 * `error` + `model-error` on any other model failure; `aborted` when the
 * caller's signal fires (status-only — the spec allows status OR uncertainty).
 */
'use strict';

/** Default one-shot model timeout (frozen config default). */
export const DEFAULT_TIMEOUT_MS = 60000;

/** Bound on accumulated model output (bounded-output baseline, design §10). */
export const MAX_ACCUMULATED_OUTPUT_CHARS = 16000;

/**
 * Analyst system prompt (frozen epistemic contract). Kernel facts are
 * authoritative; derived read-model values stay labeled; hypotheses are
 * non-authoritative; missing evidence stays uncertainty; recommendations are
 * human-only; the model has no tools and mutates nothing.
 */
export const SYSTEM_PROMPT = [
  'You are the DSH Shadow Cognition analyst for one UniClaw run. You produce a post-hoc interpretation for a human investigator.',
  'Epistemic rules (non-negotiable):',
  '- Kernel facts are authoritative: RuntimeEvent records in the provided context are kernel facts.',
  '- Derived read-model values (RunSnapshot fields, trap projections, evidence metadata) are labeled derived-read-model: they are DriverHost projections, not Kernel truth.',
  '- Shadow hypotheses are NON-authoritative inferences, always classified shadow-inference.',
  '- Missing evidence stays uncertainty; never fabricate data, sequences, or freshness.',
  '- Recommendations target HUMANS ONLY (human-investigation); nothing you write authorizes or proposes execution.',
  '- You have no tools; you cannot mutate anything.',
  '- Never assert goal satisfaction as a Kernel fact from inference. You may report what the provided context says (e.g. "Kernel GoalEvidence indicates ...") only when the context contains it.',
  'Answer with ONE JSON object, no markdown fences:',
  '{"humanSummary": "<2-4 sentence plain-language summary>", "hypotheses": [{"claim": "<string>", "supportingRefs": ["<eventId or locator>"], "flaggedUncertain": true|false}], "uncertainties": [{"topic": "<string>", "reason": "missing-data|stale-data|unresolved-evidence-ref|context-assembly-failed|model-unavailable|model-timeout|model-error"}], "recommendations": [{"text": "<human investigation step>"}]}',
  'Keep hypotheses <= 5, uncertainties <= 5, recommendations <= 5. Every claim must be traceable to the provided context or explicitly flagged as inference.',
].join('\n');

function epochMs() {
  return Date.now();
}

/**
 * Invoke the DSH model route exactly once.
 *
 * @param {object} params
 * @param {object|null} params.llm        `ctx.get('llm')` — optional service
 * @param {string|null} params.provider   configured shadow.model.provider
 * @param {string|null} params.model      configured shadow.model.model
 * @param {string} params.userText        assembled bounded context
 * @param {AbortSignal|null} params.signal caller signal (command cancellation)
 * @param {number} params.timeoutMs       bounded timeout
 * @param {string} params.systemPrompt    analyst prompt
 * @returns {Promise<{status, uncertainty, text, startedAt, finishedAt, error}>}
 */
export async function invokeOneShotModel({ llm, provider, model, userText, signal, timeoutMs, systemPrompt = SYSTEM_PROMPT }) {
  const startedAt = epochMs();
  const unavailable = { status: 'not-configured', uncertainty: 'model-unavailable', text: null, error: null };
  if (!llm || typeof llm.stream !== 'function') {
    return { ...unavailable, startedAt, finishedAt: epochMs() };
  }
  if (typeof provider !== 'string' || provider.length === 0 || typeof model !== 'string' || model.length === 0) {
    return { ...unavailable, startedAt, finishedAt: epochMs() };
  }

  // Bounded timeout: a ref'd setTimeout (NOT AbortSignal.timeout, whose
  // internal timer is unref'd and cannot fire in a minimal event loop) aborts
  // a controller; the caller's signal is folded in via AbortSignal.any.
  const controller = new AbortController();
  const effectiveTimeoutMs = Number.isInteger(timeoutMs) && timeoutMs > 0 ? timeoutMs : DEFAULT_TIMEOUT_MS;
  const timer = setTimeout(() => controller.abort(new Error(`shadow model call timed out after ${effectiveTimeoutMs}ms`)), effectiveTimeoutMs);
  const combined = signal instanceof AbortSignal ? AbortSignal.any([signal, controller.signal]) : controller.signal;

  let text = '';
  let status = 'success';
  let uncertainty = null;
  let error = null;
  const messages = [{
    id: 'shadow-analysis-input',
    role: 'user',
    content: [{ type: 'text', text: userText }],
    source: { kind: 'user' },
  }];
  const options = { provider, model, system: systemPrompt, messages, signal: combined };

  try {
    for await (const chunk of llm.stream(options)) {
      if (chunk && chunk.type === 'text-delta' && typeof chunk.text === 'string') {
        const remaining = MAX_ACCUMULATED_OUTPUT_CHARS - text.length;
        if (remaining > 0) text += chunk.text.slice(0, remaining);
        if (text.length >= MAX_ACCUMULATED_OUTPUT_CHARS) break; // bounded consumption
      } else if (chunk && chunk.type === 'finish' && chunk.reason) {
        const reason = chunk.reason;
        if (reason.kind === 'error') {
          status = 'error';
          uncertainty = 'model-error';
          error = reason.failure?.message ?? 'model call failed';
          if (reason.failure?.code === 'NO_ADAPTER') {
            status = 'not-configured';
            uncertainty = 'model-unavailable';
          }
        } else if (reason.kind === 'aborted') {
          const callerAborted = signal?.aborted === true;
          const timedOut = combined.aborted && !callerAborted;
          status = timedOut ? 'timeout' : 'aborted';
          if (timedOut) uncertainty = 'model-timeout';
          else error = reason.failure?.message ?? 'model call aborted';
        }
        // reason.kind 'stop' | 'max-tokens' | 'tool-calls' → success path
      }
    }
  } catch (err) {
    const callerAborted = signal?.aborted === true;
    if (callerAborted) {
      status = 'aborted';
      error = err?.message ?? 'model call aborted';
    } else if (combined.aborted) {
      status = 'timeout';
      uncertainty = 'model-timeout';
    } else {
      status = 'error';
      uncertainty = 'model-error';
      error = err?.message ?? String(err);
    }
  } finally {
    clearTimeout(timer);
  }

  // An empty successful response fails closed (bounded-output baseline).
  if (status === 'success' && text.trim().length === 0) {
    status = 'error';
    uncertainty = 'model-error';
    error = 'model returned no output';
  }

  return {
    status,
    uncertainty,
    text: text.length > 0 ? text : null,
    startedAt,
    finishedAt: epochMs(),
    error,
  };
}
