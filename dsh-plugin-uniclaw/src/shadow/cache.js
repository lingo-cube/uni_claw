/**
 * Bounded process-local cache (design §9.2 — EPHEMERAL_PROCESS_LOCAL).
 *
 * `Map<runId, { analysis, at }>` bounded to `maxEntries` (default 20),
 * insertion-order eviction. It is convenience only, NEVER authoritative, and
 * NEVER a Memory/Knowledge Store/History Database: disposable, restart-lossy,
 * and a fresh analysis is recomputable on demand from the graduated read
 * surfaces. The command response is the authoritative human inspection
 * surface, not this cache.
 */
'use strict';

/** Smallest bounded cache size consistent with the design (no frozen cap). */
export const DEFAULT_CACHE_MAX_ENTRIES = 20;

/**
 * @param {object} [params]
 * @param {number} [params.maxEntries] positive integer bound
 * @returns {Readonly<{get, set, delete, clear, size}>} bounded cache
 */
export function createShadowCache({ maxEntries = DEFAULT_CACHE_MAX_ENTRIES } = {}) {
  const bound = Number.isInteger(maxEntries) && maxEntries > 0 ? maxEntries : DEFAULT_CACHE_MAX_ENTRIES;
  const entries = new Map();

  return Object.freeze({
    /** @returns {object|undefined} the bounded recent ShadowAnalysis for runId */
    get(runId) {
      const entry = entries.get(runId);
      return entry ? entry.analysis : undefined;
    },
    /** Insert or refresh one run's bounded recent artifact; evicts oldest on overflow. Never throws. */
    set(runId, analysis) {
      try {
        entries.delete(runId);
        entries.set(runId, { analysis, at: Date.now() });
        while (entries.size > bound) {
          const oldest = entries.keys().next().value;
          if (oldest === undefined) break;
          entries.delete(oldest);
        }
      } catch {
        // contained: cache write failure never fails the analysis (§13)
      }
    },
    delete(runId) {
      return entries.delete(runId);
    },
    clear() {
      entries.clear();
    },
    get size() {
      return entries.size;
    },
  });
}
