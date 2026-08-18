/**
 * AssistanceBridge (dsh-assistance-provider-adapter A3) — the versioned
 * Runtime ↔ DSH protocol translator. PROVIDER-AGNOSTIC BY CONTRACT:
 *
 * Owns:
 *  - transport mapping (assistance.pending / assistance.resolve via the adapter)
 *  - protocol mapping (Runtime contract → DSH-side consumer representation)
 *  - correlation (requestId echo through the consumer and back)
 *  - bounded polling (fixed interval, reentrancy guard, reconnect-safe)
 *  - duplicate requestId suppression
 *
 * Does NOT own:
 *  - semantic reasoning / model selection / recovery planning / route planning
 *  - truth / authorization
 *
 * The intelligence decision belongs to the injected Harness Assistance Consumer
 * (the consumer port is replaceable — e.g. DeterministicAssistanceConsumer now,
 * a future LlmAssistanceConsumer behind ctx.llm later). This file MUST carry
 * zero required imports/references to any llm/model package or model identifier
 * (static guard).
 */
export class AssistanceBridge {
  /**
   * @param {object} deps
   * @param {object} deps.adapter - UniClawAdapter (assistancePending/assistanceResolve)
   * @param {object} deps.consumer - Harness Assistance Consumer ({ resolve(request) → structured result })
   * @param {number} [deps.pollIntervalMs] - bounded poll interval (COMPOSITION_POLICY)
   */
  constructor({ adapter, consumer, pollIntervalMs = 200 }) {
    if (!adapter || typeof adapter.assistancePending !== 'function'
        || typeof adapter.assistanceResolve !== 'function') {
      throw new TypeError('AssistanceBridge requires an adapter exposing assistancePending/assistanceResolve');
    }
    if (!consumer || typeof consumer.resolve !== 'function') {
      throw new TypeError('AssistanceBridge requires an injectable Harness Assistance Consumer');
    }
    this.adapter = adapter;
    this.consumer = consumer;
    this.pollIntervalMs = pollIntervalMs;
    this._timer = null;
    this._polling = false;
    this._seen = new Set();
    this._seenLimit = 1000;
    this.stats = { polls: 0, resolved: 0, abandoned: 0, rejected: 0, errors: 0 };
  }

  /** Start the bounded poll loop (idempotent). */
  start() {
    if (this._timer) return;
    this._timer = setInterval(() => {
      this.pollOnce().catch(() => { /* reconnect-safe: next tick retries */ });
    }, this.pollIntervalMs);
    if (this._timer.unref) this._timer.unref();
  }

  /** Stop the poll loop (plugin dispose). */
  dispose() {
    if (this._timer) {
      clearInterval(this._timer);
      this._timer = null;
    }
  }

  /** Translate the Runtime contract digest into the DSH-side consumer representation. */
  normalize(request) {
    return {
      requestId: request.requestId,
      runId: request.runId,
      semanticPage: request.semanticPage,
      beliefState: request.beliefState,
      worldVersion: request.worldVersion,
      observation: {
        sequence: request.observation?.sequence ?? null,
        foregroundApplication: request.observation?.foregroundApplication ?? null,
        elementCount: request.observation?.elementCount ?? 0,
        elementTexts: Array.isArray(request.observation?.elementTexts) ? request.observation.elementTexts : [],
      },
    };
  }

  /** Translate the structured consumer result into the wire AssistanceAdvice shape. */
  translate(result, request) {
    const recommendation = (result && typeof result.recommendation === 'string')
      ? result.recommendation
      : null;
    return {
      requestId: request.requestId,
      worldVersion: request.worldVersion,
      recommendation,
      additionalEvidence: (result && typeof result.additionalEvidence === 'string')
        ? result.additionalEvidence
        : null,
      reason: (result && typeof result.reason === 'string') ? result.reason : 'assistance bridge',
    };
  }

  /** One bounded poll: fetch pending → per-request consumer → resolve. */
  async pollOnce() {
    if (this._polling) return; // reentrancy guard
    this._polling = true;
    try {
      if (this.adapter.getState && this.adapter.getState() !== 'connected') {
        return; // reconnect-safe: skip while disconnected
      }
      const page = await this.adapter.assistancePending();
      const requests = Array.isArray(page?.requests) ? page.requests : [];
      this.stats.polls += 1;
      for (const request of requests) {
        if (this._seen.has(request.requestId)) continue; // duplicate suppression
        this._seen.add(request.requestId);
        this._trimSeen();
        try {
          // Mechanical compatibility: the consumer port MAY be synchronous
          // (DeterministicAssistanceConsumer) or asynchronous
          // (LlmAssistanceConsumer) — await accepts both. The bridge's role is
          // unchanged: transport/protocol translation only.
          const result = await this.consumer.resolve(this.normalize(request));
          const advice = this.translate(result, request);
          const outcome = await this.adapter.assistanceResolve(advice);
          if (outcome?.resolved === true) {
            this.stats.resolved += advice.recommendation ? 1 : 0;
            if (!advice.recommendation) this.stats.abandoned += 1;
          } else {
            this.stats.rejected += 1;
          }
        } catch (err) {
          this.stats.errors += 1;
          // A failing request must not block the poll: drop it for this tick;
          // a later poll may retry it (resolve stays pending until timeout).
        }
      }
    } catch (err) {
      this.stats.errors += 1;
      // Transport error (disconnect): bounded, reconnect-safe — next poll retries.
    } finally {
      this._polling = false;
    }
  }

  _trimSeen() {
    if (this._seen.size > this._seenLimit) {
      this._seen.clear();
    }
  }
}
