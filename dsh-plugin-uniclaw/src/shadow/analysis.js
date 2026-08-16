/**
 * ShadowAnalysis output builder — frozen V1 schema (design §7, D4).
 *
 * The artifact is the bounded DSH-side cognitive-inference product of the
 * shadow change. Every field below has a current buyer; nothing else exists:
 * no confidence framework, no severity ontology, no memory system, no planner
 * state, no execution proposal, no approval status, no action authorization.
 *
 * Epistemic vocabulary is frozen (design §6 / spec "Evidence hierarchy never
 * collapses"): observed facts are `kernel-fact` (RuntimeEvent records) or
 * `derived-read-model` (RunSnapshot/trap projections), hypotheses are always
 * `shadow-inference`, recommendations are always human-facing
 * (`human-investigation`).
 */
'use strict';

/** Constant artifact classification (survives caching and presentation). */
export const CLASSIFICATION = 'COGNITIVE_INFERENCE';

/** V1-only trigger (auto triggers are deferred; `shadow.autoTriggers` MUST be []). */
export const TRIGGER = 'human.request';

/** Frozen analysis focus vocabulary (design §7). */
export const FOCUS_VALUES = Object.freeze([
  'general',
  'trap',
  'failure',
  'completion',
  'progress',
  'blocked',
]);

/** Frozen uncertainty reason vocabulary (design §7 / §13). */
export const UNCERTAINTY_REASONS = Object.freeze([
  'missing-data',
  'stale-data',
  'unresolved-evidence-ref',
  'context-assembly-failed',
  'model-unavailable',
  'model-timeout',
  'model-error',
]);

/** Frozen model-call status vocabulary (design §10 accounting). */
export const MODEL_CALL_STATUSES = Object.freeze([
  'success',
  'error',
  'timeout',
  'aborted',
  'not-configured',
]);

/** Observed-fact classification vocabulary (never collapses into hypotheses). */
export const FACT_CLASSIFICATIONS = Object.freeze(['kernel-fact', 'derived-read-model']);

/** Hypothesis classification (non-authoritative shadow inference). */
export const HYPOTHESIS_CLASSIFICATION = 'shadow-inference';

/** Recommendation target (humans only — Kernel input from Shadow is ZERO). */
export const RECOMMENDATION_TARGET = 'human-investigation';

/** Bounded cap on parsed model output consumed per analysis (design §10 "bounded output"). */
export const MAX_MODEL_OUTPUT_CHARS = 16000;

/** Deterministic cap on facts/hypotheses/uncertainties/recommendations per artifact. */
export const MAX_FACTS = 64;
export const MAX_HYPOTHESES = 5;
export const MAX_UNCERTAINTIES = 12;
export const MAX_RECOMMENDATIONS = 5;

function deepFreeze(value) {
  if (value && typeof value === 'object' && !Object.isFrozen(value)) {
    for (const key of Object.keys(value)) deepFreeze(value[key]);
    Object.freeze(value);
  }
  return value;
}

/**
 * Build one frozen `ShadowAnalysis` artifact. Every input is validated or
 * defaulted deterministically; the caller supplies already-bounded arrays.
 */
export function createShadowAnalysis(input) {
  if (!input || typeof input !== 'object') {
    throw new TypeError('createShadowAnalysis requires an input object');
  }
  if (typeof input.analysisId !== 'string' || input.analysisId.length === 0) {
    throw new TypeError('ShadowAnalysis requires a non-empty analysisId');
  }
  if (typeof input.runId !== 'string' || input.runId.length === 0) {
    throw new TypeError('ShadowAnalysis requires a non-empty runId');
  }
  if (typeof input.sessionId !== 'string' || input.sessionId.length === 0) {
    throw new TypeError('ShadowAnalysis requires a non-empty sessionId');
  }
  if (!FOCUS_VALUES.includes(input.focus)) {
    throw new TypeError(`ShadowAnalysis focus must be one of ${FOCUS_VALUES.join(', ')}`);
  }
  const model = input.model ?? {};
  const modelCall = input.modelCall ?? {};
  return deepFreeze({
    analysisId: input.analysisId,
    runId: input.runId,
    sessionId: input.sessionId,
    trigger: TRIGGER,
    focus: input.focus,
    requestedAt: Number.isFinite(input.requestedAt) ? input.requestedAt : Date.now(),
    completedAt: Number.isFinite(input.completedAt) ? input.completedAt : Date.now(),
    classification: CLASSIFICATION,
    evidenceRefs: Array.isArray(input.evidenceRefs) ? input.evidenceRefs : [],
    observedFacts: Array.isArray(input.observedFacts) ? input.observedFacts : [],
    hypotheses: Array.isArray(input.hypotheses) ? input.hypotheses : [],
    uncertainties: Array.isArray(input.uncertainties) ? input.uncertainties : [],
    recommendations: Array.isArray(input.recommendations) ? input.recommendations : [],
    humanSummary: typeof input.humanSummary === 'string' ? input.humanSummary : '',
    model: {
      provider: typeof model.provider === 'string' ? model.provider : null,
      model: typeof model.model === 'string' ? model.model : null,
    },
    modelCall: {
      trigger: TRIGGER,
      evidenceRefs: Array.isArray(modelCall.evidenceRefs) ? modelCall.evidenceRefs : [],
      inputEventCount: Number.isInteger(modelCall.inputEventCount) ? modelCall.inputEventCount : 0,
      contextChars: Number.isInteger(modelCall.contextChars) ? modelCall.contextChars : 0,
      provider: typeof modelCall.provider === 'string' ? modelCall.provider : null,
      model: typeof modelCall.model === 'string' ? modelCall.model : null,
      status: MODEL_CALL_STATUSES.includes(modelCall.status) ? modelCall.status : 'not-configured',
      startedAt: Number.isFinite(modelCall.startedAt) ? modelCall.startedAt : null,
      finishedAt: Number.isFinite(modelCall.finishedAt) ? modelCall.finishedAt : null,
      error: typeof modelCall.error === 'string' ? modelCall.error : null,
    },
  });
}

/** One observed fact: kernel fact or derived read-model value, with a ref. */
export function fact(claim, classification, ref) {
  if (typeof claim !== 'string' || claim.length === 0) {
    throw new TypeError('fact requires a non-empty claim');
  }
  if (!FACT_CLASSIFICATIONS.includes(classification)) {
    throw new TypeError(`fact classification must be one of ${FACT_CLASSIFICATIONS.join(', ')}`);
  }
  return { claim, classification, ref: ref ?? null };
}

/** One non-authoritative hypothesis, always `shadow-inference`. */
export function hypothesis(claim, supportingRefs, flaggedUncertain) {
  if (typeof claim !== 'string' || claim.length === 0) {
    throw new TypeError('hypothesis requires a non-empty claim');
  }
  return {
    claim,
    classification: HYPOTHESIS_CLASSIFICATION,
    supportingRefs: Array.isArray(supportingRefs) ? supportingRefs.slice(0, MAX_HYPOTHESES) : [],
    ...(flaggedUncertain === true ? { flaggedUncertain: true } : {}),
  };
}

/** One explicit uncertainty with a frozen reason. */
export function uncertainty(topic, reason) {
  if (typeof topic !== 'string' || topic.length === 0) {
    throw new TypeError('uncertainty requires a non-empty topic');
  }
  if (!UNCERTAINTY_REASONS.includes(reason)) {
    throw new TypeError(`uncertainty reason must be one of ${UNCERTAINTY_REASONS.join(', ')}`);
  }
  return { topic, reason };
}

/** One human-facing recommendation (Kernel never consumes it). */
export function recommendation(text) {
  if (typeof text !== 'string' || text.length === 0) {
    throw new TypeError('recommendation requires non-empty text');
  }
  return { text, target: RECOMMENDATION_TARGET };
}

/**
 * Deterministically extract the structured JSON object from model output.
 * Accepts raw JSON or a ```json fenced block. Fails closed on malformed
 * output: never invents claims. Valid fields are accepted per-field; invalid
 * entries are dropped (still never fabricated).
 *
 * @returns {{ ok: boolean, humanSummary: string, hypotheses: object[],
 *   uncertainties: object[], recommendations: object[], error?: string }}
 */
export function parseModelOutput(rawText) {
  const bounded = (typeof rawText === 'string' ? rawText : '').slice(0, MAX_MODEL_OUTPUT_CHARS);
  const trimmed = bounded.trim();
  let jsonText = trimmed;
  const fence = /^```(?:json)?\s*([\s\S]*?)\s*```$/;
  const fenceMatch = fence.exec(trimmed);
  if (fenceMatch) jsonText = fenceMatch[1];

  let parsed;
  try {
    parsed = JSON.parse(jsonText);
  } catch {
    return { ok: false, humanSummary: '', hypotheses: [], uncertainties: [], recommendations: [], error: 'model output is not valid JSON' };
  }
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
    return { ok: false, humanSummary: '', hypotheses: [], uncertainties: [], recommendations: [], error: 'model output is not a JSON object' };
  }
  const humanSummary = typeof parsed.humanSummary === 'string' ? parsed.humanSummary.trim() : '';
  if (!humanSummary) {
    return { ok: false, humanSummary: '', hypotheses: [], uncertainties: [], recommendations: [], error: 'model output lacks humanSummary' };
  }

  const hypotheses = [];
  if (Array.isArray(parsed.hypotheses)) {
    for (const entry of parsed.hypotheses) {
      if (hypotheses.length >= MAX_HYPOTHESES) break;
      if (entry && typeof entry === 'object' && typeof entry.claim === 'string' && entry.claim.trim()) {
        hypotheses.push({
          claim: entry.claim.trim(),
          classification: HYPOTHESIS_CLASSIFICATION,
          supportingRefs: Array.isArray(entry.supportingRefs)
            ? entry.supportingRefs.filter((r) => typeof r === 'string').slice(0, 8)
            : [],
          ...(entry.flaggedUncertain === true ? { flaggedUncertain: true } : {}),
        });
      }
    }
  }

  const uncertainties = [];
  if (Array.isArray(parsed.uncertainties)) {
    for (const entry of parsed.uncertainties) {
      if (uncertainties.length >= MAX_UNCERTAINTIES) break;
      if (
        entry && typeof entry === 'object' &&
        typeof entry.topic === 'string' && entry.topic.trim() &&
        UNCERTAINTY_REASONS.includes(entry.reason)
      ) {
        uncertainties.push({ topic: entry.topic.trim(), reason: entry.reason });
      }
    }
  }

  const recommendations = [];
  if (Array.isArray(parsed.recommendations)) {
    for (const entry of parsed.recommendations) {
      if (recommendations.length >= MAX_RECOMMENDATIONS) break;
      if (entry && typeof entry === 'object' && typeof entry.text === 'string' && entry.text.trim()) {
        recommendations.push({ text: entry.text.trim(), target: RECOMMENDATION_TARGET });
      }
    }
  }

  return { ok: true, humanSummary, hypotheses, uncertainties, recommendations };
}
