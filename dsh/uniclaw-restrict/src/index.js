/**
 * @uniclaw/dsh-uniagent-restrict — the uniagent-prod session capability binding
 * (AGT-002 review B1).
 *
 * Mounted ONLY inside the `uniagent-prod` agent preset composition, in the
 * preset's standing scope; the Agent that joins that preset is a CHILD of it.
 * This package applies ONE declaration:
 *
 *     ctx.tools.restrict({ allow: [] })
 *
 * Why an empty ALLOW list rather than `deny: [<names>]`:
 *
 * DSH's restriction filter is evaluated LIVE, per view, over everything a
 * scope INHERITS — the deployment-global layer plus every ancestor scope on
 * the chain (packages/core/tools/src/index.ts:1187-1201; docs/subsystems/
 * tools.md:165). Two consequences decide the shape here:
 *
 *   - A `deny` list only removes the names it spells; anything registered
 *     afterwards is admitted, by documented design ("a deny-only filter admits
 *     later unlisted inherited tools"). MCP servers connect ASYNCHRONOUSLY
 *     after boot and register their tools late, so ANY boot-time deny snapshot
 *     is structurally unable to cover them. `restrict()` also validates its
 *     names against the live catalog and throws on an unknown one, so a
 *     snapshot cannot even name a tool that does not exist yet.
 *   - An `allow` list excludes every inherited name it does not spell,
 *     whenever that name appears. `allow: []` therefore means "inherit
 *     nothing" — the mechanical equivalent of a frozen manifest, and it holds
 *     no matter what registers later.
 *
 * WHY THE TOOL IS NOT REGISTERED HERE. A restriction never filters the layer
 * the viewing scope OWNS, but it DOES filter that scope's own registrations
 * when the restriction and the registration share that layer: `view()` applies
 * `admits()` to the inherited surface and then merges the scope's own tools
 * (index.ts:1195-1209), and the same-layer case resolves through the ancestor
 * chain, not the exemption. Verified directly: registering `submit_decision`
 * in this same scope and then applying `allow: []` yields an EMPTY catalog.
 *
 * `submit_decision` is therefore registered by @uniclaw/dsh-decision-channel
 * into the AGENT's own scope (the preset's child), where it is genuinely
 * exempt from this preset-level filter — the shape the restricted session
 * needs: exactly one tool, independent of registration order.
 *
 * Named `allow` entries cannot reference scope-local tools ("unknown global
 * tool"), which is why the manifest is expressed as the empty allow-list plus
 * the child-scope exemption rather than `allow: ['submit_decision']`.
 *
 * This package performs no Product decision work and owns no state.
 */

export const name = 'uniclaw-uniagent-restrict'
export const inject = ['tools']

export function apply(ctx) {
  if (ctx.tools === undefined || typeof ctx.tools.restrict !== 'function'
    || typeof ctx.tools.schemas !== 'function') {
    throw new Error('uniclaw-uniagent-restrict: host tools registry unavailable')
  }
  ctx.tools.restrict({ allow: [] })
  const inheritedAtMount = ctx.tools.schemas().map(schema => schema.name).filter(n => n !== undefined)
  console.info('[uniclaw-uniagent-restrict] inherited tool surface closed for uniagent-prod'
    + ` (allow: []); global layer at mount: ${inheritedAtMount.length} entries`)
}
