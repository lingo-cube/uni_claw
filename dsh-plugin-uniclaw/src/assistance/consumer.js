/**
 * Deterministic Harness Assistance Consumer (dsh-assistance-provider-adapter A3b).
 *
 * The FIRST consumer implementation is deliberately FAKE/DETERMINISTIC — the
 * APPLY proves the adapter before conflating it with intelligence quality. It is
 * the minimal replaceable consumer behind the bridge's consumer port.
 *
 * Contract:
 *  - input:  a normalized Assistance request (bridge translation of the Runtime
 *            contract)
 *  - output: a structured result { recommendation, additionalEvidence?, reason }
 *            where recommendation is either null (abandon) or one of the Runtime
 *            Agent's accepted whitelist (re-observe / rebind / dismiss-obstruction)
 *  - MUST NOT inspect Runtime private state, generate concrete device actions,
 *    mutate Runtime, or declare goal completion.
 *
 * A future real consumer (e.g. LlmAssistanceConsumer behind the DSH ctx.llm seam,
 * or a SubagentRuntime general host) replaces this instance at the composition
 * root — the bridge contract stays provider-agnostic.
 */
export class DeterministicAssistanceConsumer {
  /**
   * @param {object} [options]
   * @param {(request: object) => object|null} [options.responder] - injectable
   *   deterministic responder (tests); defaults to a fixture mapping.
   */
  constructor({ responder } = {}) {
    this.responder = responder ?? ((request) => {
      // Known test fixture mapping: any request on the Settings page gets the
      // whitelisted "re-observe" recommendation; everything else abandons.
      if (request.semanticPage === 'Settings') {
        return { recommendation: 're-observe', reason: 'deterministic test consumer' };
      }
      return { recommendation: null, reason: 'no deterministic advice for this request' };
    });
  }

  /** Resolve one normalized request → structured result (never throws). */
  resolve(request) {
    if (!request || typeof request !== 'object') {
      return { recommendation: null, reason: 'malformed request' };
    }
    const result = this.responder(request) ?? { recommendation: null, reason: 'no advice' };
    if (result.recommendation !== null
        && !['re-observe', 'rebind', 'dismiss-obstruction'].includes(result.recommendation)) {
      // Never emit an un-whitelisted recommendation: abandon instead.
      return { recommendation: null, reason: `un-whitelisted recommendation suppressed: ${result.recommendation}` };
    }
    return {
      recommendation: result.recommendation ?? null,
      additionalEvidence: typeof result.additionalEvidence === 'string' ? result.additionalEvidence : null,
      reason: typeof result.reason === 'string' ? result.reason : 'deterministic consumer',
    };
  }
}
