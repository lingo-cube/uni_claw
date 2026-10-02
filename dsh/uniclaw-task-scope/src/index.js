/**
 * @uniclaw/dsh-task-scope — the uniagent-task session capability binding
 * (PNL-001 决策 6, slice S2).
 *
 * Mounted ONLY inside the `uniagent-task` agent preset composition, in the
 * preset's standing scope. Unlike @uniclaw/dsh-uniagent-restrict (AGT-002,
 * frozen `allow: []`), the allow-list here comes from the preset row's
 * config, so each deployment names exactly the tools a task session needs:
 *
 *     - id: uniclaw-task-scope
 *       name: '@uniclaw/dsh-task-scope'
 *       config:
 *         allow: ['read', 'glob', 'grep', 'bash', ...]
 *
 * Why an ALLOW list rather than `deny: [<names>]` — same reasoning as the
 * AGT-002 binding: DSH's restriction filter is evaluated LIVE per view over
 * everything the scope INHERITS, so a boot-time deny snapshot is structurally
 * unable to cover MCP tools that register asynchronously after boot, while
 * an allow list excludes every inherited name it does not spell, whenever it
 * appears. Named allow entries are validated against the live catalog by
 * restrict() itself, so a stale name fails at mount instead of leaking later.
 *
 * FAIL-CLOSED CONFIG. A missing / empty / wrong-typed allow list would
 * otherwise silently produce an unrestricted session (no restrict call at
 * all) or a denial-by-empty-accident. Both are worse than a failed boot, so
 * apply() throws instead of falling back to deny or to an empty list — the
 * session must not come up with an unintended tool surface.
 *
 * This package performs no Product task work and owns no state.
 */

export const name = 'uniclaw-task-scope'
export const inject = ['tools']

/**
 * @param {object} ctx host context with the tools registry
 * @param {{ allow?: unknown }} config the preset row's config block
 */
export function apply(ctx, config) {
  if (ctx.tools === undefined || typeof ctx.tools.restrict !== 'function'
    || typeof ctx.tools.schemas !== 'function') {
    throw new Error('uniclaw-task-scope: host tools registry unavailable')
  }
  const allow = config?.allow
  if (!Array.isArray(allow)) {
    throw new Error('uniclaw-task-scope: config.allow must be a non-empty string array'
      + ` (got ${allow === undefined ? 'missing' : typeof allow})`)
  }
  if (allow.length === 0 || allow.some(n => typeof n !== 'string' || n === '')) {
    throw new Error('uniclaw-task-scope: config.allow must be a non-empty string array'
      + ` (got ${JSON.stringify(allow)})`)
  }
  ctx.tools.restrict({ allow })
  const inheritedAtMount = ctx.tools.schemas().map(schema => schema.name).filter(n => n !== undefined)
  console.info(`[uniclaw-task-scope] task session tool surface restricted to`
    + ` ${allow.length} named tools; inherited layer at mount: ${inheritedAtMount.length} entries`)
}
