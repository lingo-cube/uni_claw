/**
 * Deterministic DSH commands over the read-only UniClaw surface (PLUG-F7).
 * Every handler is a pure read: it formats the classified snapshot / trap /
 * evidence / run list from the wire DTOs and returns a CommandResult. No
 * command ever dispatches an action or changes Kernel state — control
 * operations are audited and deferred (control.support).
 *
 * The fifth command, `uniclaw-shadow-analyze`, is the Shadow Cognition
 * inspection surface (design §11): zero-model dispatch (the handler runs
 * directly against the receiving agent — the command is never sent to the
 * model), truthful session identity, deterministic read-only retrieval,
 * optional one `ctx.llm` call, and a structured-text `ShadowAnalysis`
 * response. It writes ZERO session events.
 */
'use strict';

import { FOCUS_VALUES } from './shadow/analysis.js';
import { runShadowAnalysis } from './shadow/index.js';

const NAME_PATTERN = /^[a-z][a-z0-9_-]*$/;

/** Format one classified field as `value (classification — truth source)`. */
function formatField(label, field) {
  const value = field && field.value !== null && field.value !== undefined
    ? JSON.stringify(field.value)
    : 'N/A';
  const classification = field?.classification ?? 'unknown';
  const truthSource = field?.truthSource ? ` — ${field.truthSource}` : '';
  return `${label}: ${value} (${classification}${truthSource})`;
}

/** Format a snapshot wire DTO into a stable, human-readable block. */
export function formatSnapshot(snapshot) {
  if (!snapshot || typeof snapshot !== 'object') {
    return 'no snapshot data';
  }
  const lines = [
    `runId: ${snapshot.runId}`,
    formatField('runState', snapshot.runState),
    formatField('currentSemanticPage', snapshot.currentSemanticPage),
    formatField('activeTrap', snapshot.activeTrap),
    formatField('currentGoal', snapshot.currentGoal),
    formatField('lastDecision', snapshot.lastDecision),
    formatField('lastAction', snapshot.lastAction),
    formatField('recoveryState', snapshot.recoveryState),
    formatField('latestGoalEvidence', snapshot.latestGoalEvidence),
    formatField('currentObservationSequence', snapshot.currentObservationSequence),
    formatField('currentContainerSummary', snapshot.currentContainerSummary),
    formatField('bindingsSummary', snapshot.bindingsSummary),
    formatField('stateBeliefsSummary', snapshot.stateBeliefsSummary),
  ];
  if (Array.isArray(snapshot.diagnostics) && snapshot.diagnostics.length > 0) {
    lines.push(`diagnostics: ${snapshot.diagnostics.join('; ')}`);
  }
  return lines.join('\n');
}

function extractRunId(rawInput) {
  return rawInput.trim();
}

/** Build the deterministic command definitions bound to one adapter.
 *
 * Without `shadowContext` the four control-plane commands are returned
 * (back-compat shape). With `shadowContext` the fifth command,
 * `uniclaw-shadow-analyze`, is appended. `shadowContext` =
 * `{ config, facade, getLlm, cache }` (built by the plugin; the facade is the
 * narrowed read-only retrieval surface — no adapter internals reach the
 * command).
 */
export function buildCommands(adapter, shadowContext) {
  const commands = [
    {
      name: 'uniclaw-inspect-run',
      description: 'Inspect the classified read-only snapshot of one UniClaw run.',
      input: { hint: '<runId>' },
      recordInput: true,
      handler: async (invocation) => {
        const runId = extractRunId(invocation.rawInput);
        if (!runId) {
          return { kind: 'error', text: 'usage: uniclaw-inspect-run <runId>' };
        }
        try {
          const snapshot = await adapter.getRunSnapshot(runId);
          return { kind: 'success', text: formatSnapshot(snapshot) };
        } catch (err) {
          return { kind: 'error', text: errorText(err) };
        }
      },
    },
    {
      name: 'uniclaw-inspect-trap',
      description: 'Inspect the classified active trap of one UniClaw run.',
      input: { hint: '<runId>' },
      recordInput: true,
      handler: async (invocation) => {
        const runId = extractRunId(invocation.rawInput);
        if (!runId) {
          return { kind: 'error', text: 'usage: uniclaw-inspect-trap <runId>' };
        }
        try {
          const result = await adapter.getTrap(runId);
          const lines = [`runId: ${result.runId}`, `found: ${result.found}`];
          if (result.trap?.value) {
            lines.push(`kind: ${result.trap.value.kind}`);
            lines.push(`scope: ${result.trap.value.scope}`);
            lines.push(`expected: ${result.trap.value.expected ?? 'N/A'}`);
            lines.push(`observed: ${result.trap.value.observed ?? 'N/A'}`);
            lines.push(`source: ${result.trap.value.source}`);
            lines.push(`evidence: ${result.trap.value.evidence}`);
            if (result.trap.value.lastActionDescription) {
              lines.push(`lastAction: ${result.trap.value.lastActionDescription}`);
            }
            lines.push(`classification: ${result.trap.classification}`);
          }
          if (result.diagnostic) lines.push(`diagnostic: ${result.diagnostic}`);
          return { kind: 'success', text: lines.join('\n') };
        } catch (err) {
          return { kind: 'error', text: errorText(err) };
        }
      },
    },
    {
      name: 'uniclaw-evidence-open',
      description: 'Open one logical evidence ref (metadata only; resolution by logical locator only).',
      input: { hint: '<locator> <runId>' },
      recordInput: true,
      handler: async (invocation) => {
        const parts = invocation.rawInput.trim().split(/\s+/);
        const [locator, runId] = parts;
        if (!locator || !runId) {
          return { kind: 'error', text: 'usage: uniclaw-evidence-open <locator> <runId>' };
        }
        try {
          const resolution = await adapter.getEvidence({ locator, runId });
          const lines = [`found: ${resolution.found}`];
          if (resolution.found) {
            if (resolution.captureSessionId) lines.push(`captureSessionId: ${resolution.captureSessionId}`);
            if (resolution.record) {
              lines.push(`record.order: ${resolution.record.order}`);
              lines.push(`record.kind: ${resolution.record.kind}`);
              lines.push(`record.sequenceNumber: ${resolution.record.sequenceNumber}`);
              if (resolution.record.actionId) lines.push(`record.actionId: ${resolution.record.actionId}`);
            }
            if (resolution.artifact) {
              lines.push(`artifact.artifactId: ${resolution.artifact.artifactId}`);
              lines.push(`artifact.byteCount: ${resolution.artifact.byteCount}`);
            }
            if (resolution.ref) {
              lines.push(`ref.kind: ${resolution.ref.kind}`);
              lines.push(`ref.maturity: ${resolution.ref.maturity}`);
            }
          }
          if (resolution.diagnostic) lines.push(`diagnostic: ${resolution.diagnostic}`);
          return { kind: 'success', text: lines.join('\n') };
        } catch (err) {
          return { kind: 'error', text: errorText(err) };
        }
      },
    },
    {
      name: 'uniclaw-runs-list',
      description: 'List run ids registered with the DriverHost read surface.',
      handler: async () => {
        try {
          const result = await adapter.listRuns();
          const runs = Array.isArray(result?.runIds) ? result.runIds : [];
          return { kind: 'success', text: runs.length === 0 ? '(no runs registered)' : runs.join('\n') };
        } catch (err) {
          return { kind: 'error', text: errorText(err) };
        }
      },
    },
  ];

  if (shadowContext) {
    commands.push(buildShadowCommand(shadowContext));
  }

  return commands;
}

/**
 * Parse `uniclaw-shadow-analyze` input deterministically:
 * `<runId> [--focus <value>] [--reason <text>]`. Unknown or duplicate flags
 * are rejected; `--reason` consumes the remainder of the line.
 */
export function parseShadowInvocation(rawInput) {
  const trimmed = typeof rawInput === 'string' ? rawInput.trim() : '';
  if (!trimmed) {
    return { error: 'usage: uniclaw-shadow-analyze <runId> [--focus <value>] [--reason <text>]' };
  }
  const tokens = trimmed.split(/\s+/);
  const runId = tokens[0];
  const rest = tokens.slice(1);
  let focus = 'general';
  let reason;
  const seen = new Set();
  let i = 0;
  while (i < rest.length) {
    const token = rest[i];
    if (token === '--focus') {
      if (seen.has('focus')) return { error: 'duplicate --focus flag' };
      const value = rest[i + 1];
      if (value === undefined || value.startsWith('--')) {
        return { error: 'usage: --focus requires a value (general|trap|failure|completion|progress|blocked)' };
      }
      if (!FOCUS_VALUES.includes(value)) {
        return { error: `unknown focus "${value}" (allowed: ${FOCUS_VALUES.join(', ')})` };
      }
      focus = value;
      seen.add('focus');
      i += 2;
    } else if (token === '--reason') {
      if (seen.has('reason')) return { error: 'duplicate --reason flag' };
      const value = rest.slice(i + 1).join(' ').trim();
      if (!value) return { error: 'usage: --reason requires text' };
      reason = value;
      seen.add('reason');
      break;
    } else {
      return { error: `unknown argument "${token}"` };
    }
  }
  return { runId, focus, reason };
}

/** Structured-text rendering of one ShadowAnalysis (authoritative surface). */
export function formatShadowAnalysis(analysis) {
  const call = analysis?.modelCall;
  const lines = [
    `shadow analysis: ${analysis.analysisId}`,
    `classification: ${analysis.classification}`,
    `runId: ${analysis.runId}`,
    `sessionId: ${analysis.sessionId}`,
    `trigger: ${analysis.trigger}`,
    `focus: ${analysis.focus}`,
    `model call: ${call?.status ?? 'n/a'}${call?.provider ? ` (${call.provider}/${call.model ?? '?'}, ${call.inputEventCount} events, ${call.contextChars} chars)` : ''}`,
    `humanSummary: ${analysis.humanSummary}`,
  ];
  if (Array.isArray(analysis.observedFacts) && analysis.observedFacts.length > 0) {
    lines.push('observedFacts:');
    for (const entry of analysis.observedFacts) {
      const ref = entry.ref ? ` (${entry.ref.kind}: ${entry.ref.eventId ?? entry.ref.field ?? entry.ref.locator ?? ''})` : '';
      lines.push(`  [${entry.classification}] ${entry.claim}${ref}`);
    }
  }
  if (Array.isArray(analysis.hypotheses) && analysis.hypotheses.length > 0) {
    lines.push('hypotheses:');
    for (const entry of analysis.hypotheses) {
      const flag = entry.flaggedUncertain ? ' (uncertain)' : '';
      const refs = entry.supportingRefs?.length ? ` refs=${entry.supportingRefs.join(',')}` : '';
      lines.push(`  [${entry.classification}] ${entry.claim}${refs}${flag}`);
    }
  }
  if (Array.isArray(analysis.uncertainties) && analysis.uncertainties.length > 0) {
    lines.push('uncertainties:');
    for (const entry of analysis.uncertainties) {
      lines.push(`  ${entry.topic}: ${entry.reason}`);
    }
  }
  if (Array.isArray(analysis.recommendations) && analysis.recommendations.length > 0) {
    lines.push('recommendations:');
    for (const entry of analysis.recommendations) {
      lines.push(`  [${entry.target}] ${entry.text}`);
    }
  }
  return lines.join('\n');
}

function buildShadowCommand(shadowContext) {
  return {
    name: 'uniclaw-shadow-analyze',
    description: 'Produce a bounded ShadowAnalysis (COGNITIVE_INFERENCE) for one UniClaw run; read-only, zero session writes.',
    input: { hint: '<runId> [--focus <value>] [--reason <text>]' },
    recordInput: true,
    handler: async (invocation) => {
      const parsed = parseShadowInvocation(invocation.rawInput);
      if (parsed.error) return { kind: 'error', text: parsed.error };
      if (shadowContext.config && shadowContext.config.enabled === false) {
        return { kind: 'error', text: 'shadow cognition is disabled by configuration (shadow.enabled=false)' };
      }
      // Truthful session identity only — never invented (design §12).
      const sessionId = invocation.agent?.session?.id;
      if (typeof sessionId !== 'string' || sessionId.length === 0) {
        return {
          kind: 'error',
          text: 'shadow analysis requires a truthful DSH session identity (invocation.agent.session.id); none is available — refusing to invent one',
        };
      }
      try {
        const analysis = await runShadowAnalysis({
          facade: shadowContext.facade,
          llm: typeof shadowContext.getLlm === 'function' ? shadowContext.getLlm() : null,
          config: shadowContext.config ?? {},
          cache: shadowContext.cache,
          request: {
            runId: parsed.runId,
            sessionId,
            focus: parsed.focus,
            reason: parsed.reason,
            signal: invocation.signal ?? null,
          },
        });
        return { kind: 'success', text: formatShadowAnalysis(analysis) };
      } catch (err) {
        // Fail-open relative to the Kernel (§13): the Kernel is never
        // touched; the shadow-level failure is reported as command text.
        return { kind: 'error', text: `shadow analysis failed: ${err?.message ?? String(err)}` };
      }
    },
  };
}

function errorText(err) {
  if (err && err.name === 'UniClawRpcError') {
    return `DriverHost error [${err.code}]: ${err.message}`;
  }
  return `error: ${err?.message ?? String(err)}`;
}

export { NAME_PATTERN };
