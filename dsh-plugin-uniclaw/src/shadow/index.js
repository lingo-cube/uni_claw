/**
 * Shadow cognition orchestrator — `dsh-plugin-uniclaw` shadow module.
 *
 * Mission (frozen): human → `uniclaw-shadow-analyze <runId> [--focus ...]
 * [--reason ...]` → truthful DSH session identity → deterministic UniClaw
 * read-only retrieval → bounded context assembly → OPTIONAL one `ctx.llm`
 * call → one `ShadowAnalysis` artifact classified `COGNITIVE_INFERENCE` →
 * concise command response → optional bounded process-local cache.
 *
 * NOTHING is written back to the Kernel; ZERO custom session events are
 * appended; the artifact is ephemeral (`EPHEMERAL_PROCESS_LOCAL`).
 */
'use strict';

import {
  CLASSIFICATION,
  MAX_FACTS,
  MAX_UNCERTAINTIES,
  MAX_RECOMMENDATIONS,
  createShadowAnalysis,
  parseModelOutput,
  recommendation,
  uncertainty,
} from './analysis.js';
import { assembleContext, DEFAULT_LIMITS } from './context.js';
import { invokeOneShotModel, SYSTEM_PROMPT, DEFAULT_TIMEOUT_MS } from './model.js';
import { createShadowCache, DEFAULT_CACHE_MAX_ENTRIES } from './cache.js';

/** Frozen config defaults (tasks.md Slice 1). */
export const DEFAULT_SHADOW_CONFIG = Object.freeze({
  enabled: true,
  model: { provider: null, model: null },
  autoTriggers: [],
  maxEvents: DEFAULT_LIMITS.maxEvents,
  maxContextChars: DEFAULT_LIMITS.maxContextChars,
  maxEvidenceRefs: DEFAULT_LIMITS.maxEvidenceRefs,
  evidenceBytesPerRef: DEFAULT_LIMITS.evidenceBytesPerRef,
  timeoutMs: DEFAULT_TIMEOUT_MS,
  visual: { enabled: false },
});

/**
 * Validate the frozen shadow config surface. `shadow.autoTriggers` is
 * reserved for V1 and MUST be `[]` (auto triggers deferred — no auto-trigger
 * machinery is built). Returns only the explicitly provided keys; defaults
 * are merged by {@link resolveShadowConfig}.
 */
export function validateShadowConfig(input) {
  if (input === undefined || input === null) return {};
  if (typeof input !== 'object' || Array.isArray(input)) {
    throw new TypeError('shadow config must be an object');
  }
  const validated = {};
  if (input.enabled !== undefined) {
    if (typeof input.enabled !== 'boolean') throw new TypeError('shadow.enabled must be a boolean');
    validated.enabled = input.enabled;
  }
  if (input.model !== undefined) {
    if (typeof input.model !== 'object' || input.model === null || Array.isArray(input.model)) {
      throw new TypeError('shadow.model must be an object');
    }
    const model = {};
    if (input.model.provider !== undefined) {
      if (typeof input.model.provider !== 'string' || input.model.provider.length === 0) {
        throw new TypeError('shadow.model.provider must be a non-empty string');
      }
      model.provider = input.model.provider;
    }
    if (input.model.model !== undefined) {
      if (typeof input.model.model !== 'string' || input.model.model.length === 0) {
        throw new TypeError('shadow.model.model must be a non-empty string');
      }
      model.model = input.model.model;
    }
    validated.model = model;
  }
  if (input.autoTriggers !== undefined) {
    if (!Array.isArray(input.autoTriggers) || input.autoTriggers.length !== 0) {
      throw new TypeError('shadow.autoTriggers is reserved and MUST be [] in V1 (auto triggers deferred)');
    }
    validated.autoTriggers = [];
  }
  for (const key of ['maxEvents', 'maxContextChars', 'maxEvidenceRefs', 'evidenceBytesPerRef', 'timeoutMs']) {
    if (input[key] !== undefined) {
      if (!Number.isInteger(input[key]) || input[key] <= 0) {
        throw new TypeError(`shadow.${key} must be a positive integer`);
      }
      validated[key] = input[key];
    }
  }
  if (input.visual !== undefined) {
    if (typeof input.visual !== 'object' || input.visual === null || Array.isArray(input.visual)) {
      throw new TypeError('shadow.visual must be an object');
    }
    if (input.visual.enabled !== undefined) {
      if (typeof input.visual.enabled !== 'boolean') throw new TypeError('shadow.visual.enabled must be a boolean');
      validated.visual = { enabled: input.visual.enabled };
    }
  }
  return validated;
}

/** Merge validated shadow config over the frozen defaults. */
export function resolveShadowConfig(validated = {}) {
  const cfg = { ...DEFAULT_SHADOW_CONFIG, ...validated };
  cfg.model = { ...DEFAULT_SHADOW_CONFIG.model, ...(validated.model ?? {}) };
  cfg.visual = { ...DEFAULT_SHADOW_CONFIG.visual, ...(validated.visual ?? {}) };
  cfg.autoTriggers = [];
  return cfg;
}

/** Per-session analysis sequence (in-process only; resets on restart). */
let analysisSeq = 0;

function nextAnalysisId(runId) {
  analysisSeq += 1;
  return `shadow-${runId}-${analysisSeq}`;
}

/** Deterministic zero-model summary (model absent/failed/malformed paths). */
function buildDeterministicSummary({ runId, focus, snapshot, eventCount, trap, status }) {
  const runState = snapshot?.runState?.value !== null && snapshot?.runState?.value !== undefined
    ? stableLabel(snapshot.runState.value)
    : 'unknown';
  const trapPart = focus === 'trap' && trap?.found === true
    ? `; trap: ${trap.trap?.value?.kind ?? 'present'}`
    : '';
  return `Run ${runId} (focus ${focus}): read-model digest — runState=${runState}, ${eventCount} bounded runtime events${trapPart}. Model cognition ${status}.`;
}

function stableLabel(value) {
  if (typeof value === 'string') return value;
  try {
    const text = JSON.stringify(value);
    return text === undefined ? String(value) : text;
  } catch {
    return String(value);
  }
}

/** Deterministic human-facing recommendations derived from current uncertainties. */
function buildDeterministicRecommendations(uncertainties) {
  const out = [];
  for (const entry of uncertainties) {
    if (out.length >= MAX_RECOMMENDATIONS) break;
    if (entry.reason === 'missing-data') {
      out.push(recommendation(`investigate missing snapshot field "${entry.topic.replace(/^snapshot field /, '')}" in the DriverHost read surface`));
    } else if (entry.reason === 'unresolved-evidence-ref') {
      out.push(recommendation(`resolve evidence ref "${entry.topic.replace(/^evidence /, '')}" before drawing conclusions from it`));
    } else if (entry.reason === 'context-assembly-failed') {
      out.push(recommendation('re-run the analysis when the DriverHost read surface is available'));
    } else if (entry.reason === 'model-unavailable') {
      out.push(recommendation('configure shadow.model.provider/model to enable cognitive interpretation'));
    } else if (entry.reason === 'model-timeout' || entry.reason === 'model-error') {
      out.push(recommendation('re-run the analysis for a fresh model interpretation'));
    }
  }
  return out;
}

/**
 * Run one bounded shadow analysis.
 *
 * @param {object} params
 * @param {object} params.facade   narrowed read-only facade
 *                                 ({ getRunSnapshot, getRuntimeEvents, getTrap, getEvidence })
 * @param {object|null} params.llm optional `ctx.get('llm')` service
 * @param {object} params.config   resolved shadow config
 * @param {object} params.cache    bounded process-local cache (write-only convenience)
 * @param {object} params.request  { runId, sessionId, focus, reason, signal? }
 * @returns {Promise<object>} frozen ShadowAnalysis artifact
 */
export async function runShadowAnalysis({ facade, llm, config, cache, request }) {
  if (!request || typeof request.runId !== 'string' || request.runId.length === 0) {
    throw new TypeError('shadow analysis requires a non-empty runId');
  }
  if (typeof request.sessionId !== 'string' || request.sessionId.length === 0) {
    throw new TypeError('shadow analysis requires a truthful DSH sessionId (never invented)');
  }
  const cfg = resolveShadowConfig(config);
  const focus = typeof request.focus === 'string' ? request.focus : 'general';
  const reason = typeof request.reason === 'string' ? request.reason.trim() : undefined;
  const requestedAt = Date.now();
  const analysisId = nextAnalysisId(request.runId);

  // Deterministic retrieval + bounded context assembly (zero-model).
  const context = await assembleContext({
    facade,
    limits: cfg,
    request: { runId: request.runId, focus, reason },
  });

  // OPTIONAL one model call (0-or-1). Deterministic collection is zero-model.
  const modelCall = await invokeOneShotModel({
    llm,
    provider: cfg.model.provider,
    model: cfg.model.model,
    userText: context.text,
    signal: request.signal ?? null,
    timeoutMs: cfg.timeoutMs,
  });

  const uncertainties = context.uncertainties.slice(0, MAX_UNCERTAINTIES);
  let hypotheses = [];
  let recommendations = [];
  let humanSummary = '';

  if (modelCall.status === 'success' && modelCall.text) {
    const parsed = parseModelOutputSafe(modelCall.text);
    if (parsed.ok) {
      hypotheses = parsed.hypotheses;
      recommendations = parsed.recommendations;
      humanSummary = parsed.humanSummary;
      for (const entry of parsed.uncertainties) {
        if (uncertainties.length >= MAX_UNCERTAINTIES) break;
        uncertainties.push(entry);
      }
    } else {
      modelCall.status = 'error';
      modelCall.error = parsed.error;
      uncertainties.push(uncertainty('model output', 'model-error'));
    }
  } else if (modelCall.uncertainty) {
    uncertainties.push(uncertainty('model call', modelCall.uncertainty));
  }

  if (!humanSummary) {
    humanSummary = buildDeterministicSummary({
      runId: request.runId,
      focus,
      snapshot: context.snapshot,
      eventCount: context.eventCount,
      trap: context.trap,
      status: modelCall.status,
    });
  }
  if (recommendations.length === 0) {
    recommendations = buildDeterministicRecommendations(uncertainties);
  }

  const facts = context.facts.slice(0, MAX_FACTS);
  const artifact = createShadowAnalysis({
    analysisId,
    runId: request.runId,
    sessionId: request.sessionId,
    focus,
    requestedAt,
    completedAt: Date.now(),
    classification: CLASSIFICATION,
    evidenceRefs: context.selectedRefs.map((ref) => ({
      locator: ref.locator,
      kind: ref.kind,
      maturity: ref.maturity,
      sizeBytes: ref.sizeBytes,
      runId: ref.runId,
    })),
    observedFacts: facts,
    hypotheses,
    uncertainties,
    recommendations,
    humanSummary,
    model: { provider: cfg.model.provider, model: cfg.model.model },
    modelCall: {
      trigger: 'human.request',
      evidenceRefs: context.selectedRefs.map((ref) => ref.locator),
      inputEventCount: context.eventCount,
      contextChars: context.contextChars,
      provider: cfg.model.provider,
      model: cfg.model.model,
      status: modelCall.status,
      startedAt: modelCall.startedAt,
      finishedAt: modelCall.finishedAt,
      error: modelCall.error,
    },
  });

  // Bounded process-local cache write — contained; never fails the analysis.
  if (cache && typeof cache.set === 'function') {
    try {
      cache.set(artifact.runId, artifact);
    } catch {
      // contained and logged at the caller level (§13)
    }
  }
  return artifact;
}

function parseModelOutputSafe(text) {
  try {
    return parseModelOutput(text);
  } catch (err) {
    return { ok: false, humanSummary: '', hypotheses: [], uncertainties: [], recommendations: [], error: err?.message ?? 'model output parse failure' };
  }
}

export { createShadowCache, DEFAULT_CACHE_MAX_ENTRIES };
