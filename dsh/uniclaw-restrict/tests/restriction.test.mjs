// AGT-002 B1 mechanism tests: the restricted session's tool surface must be
// registration-order independent.
//
// These run against a REAL DSH ToolRuntime (the checkout's built libs) with a
// preset scope and an agent scope joined to it, reproducing the composition the
// uniagent-prod preset has. They exist because the previous mechanism (a
// boot-time `deny` snapshot of the then-known global tools) could not cover MCP
// tools, which register asynchronously after boot.
//
// Production shape under test:
//   preset scope   -> @uniclaw/dsh-uniagent-restrict applies restrict({allow: []})
//   agent scope    -> @uniclaw/dsh-decision-channel registers submit_decision
//   global layer   -> deployment tools + late-registering MCP servers

import test from 'node:test'
import assert from 'node:assert/strict'

const DSH_CHECKOUT = process.env.DSH_CHECKOUT
  ?? '/Users/fran/Documents/Code/dk-harness'

// Resolve the DSH packages by their real directories; the checkout's
// node_modules/@deepseek-ai entries are relative symlinks that do not resolve
// from outside the checkout.
const { Context } = await import(`${DSH_CHECKOUT}/vendor/cordis/lib/index.js`)
const { bindScopeParent, createScope } = await import(`${DSH_CHECKOUT}/packages/core/scope/lib/index.js`)
const SystemPrompt = (await import(`${DSH_CHECKOUT}/packages/core/system-prompt/lib/index.js`)).default
const ToolRuntime = (await import(`${DSH_CHECKOUT}/packages/core/tools/lib/index.js`)).default

async function mount() {
  const ctx = new Context()
  await ctx.plugin(SystemPrompt, {})
  await ctx.plugin(ToolRuntime)
  return ctx
}

function tool(name) {
  return {
    name,
    description: `tool ${name}`,
    parameters: { type: 'object', properties: {} },
    output: {
      schema: { type: 'string' },
      render: (_args, value) => [{ type: 'text', text: String(value) }],
    },
    execute: () => Promise.resolve(`ran:${name}`),
  }
}

/** Mint a scope, optionally parented to an ancestor key. */
async function mintScope(ctx, key, parentKey) {
  if (parentKey !== undefined) bindScopeParent(key, parentKey)
  let scope
  await ctx.plugin(Object.assign((inner) => { scope = createScope(inner, key) },
    { inject: ['tools', 'systemPrompt'] }))
  return { scope, key }
}

const visible = (ctx, key) => ctx.tools.schemas(key).map(t => t.name).sort()

/**
 * The uniagent-prod composition: the preset scope carrying the composition's
 * plugins, and the Agent scope that joined it.
 */
async function composition(ctx) {
  const preset = await mintScope(ctx, { id: 'uniagent-prod' })
  const agent = await mintScope(ctx, { id: 'agent' }, preset.key)
  return { preset, agent }
}

/** Mount both preset plugins exactly as the preset row declares them. */
function mountPresetPlugins(ctx, preset, agent) {
  preset.scope.ctx.tools.restrict({ allow: [] })          // @uniclaw/dsh-uniagent-restrict
  agent.scope.ctx.tools.register(tool('submit_decision')) // @uniclaw/dsh-decision-channel
}

test('B1: the preset allow-list closes the inherited surface, including LATE registrations', async () => {
  const ctx = await mount()
  const { preset, agent } = await composition(ctx)

  // Deployment-global tools present at boot.
  ctx.tools.register(tool('usage_overview'))
  ctx.tools.register(tool('usage_query'))
  mountPresetPlugins(ctx, preset, agent)
  assert.deepEqual(visible(ctx, agent.key), ['submit_decision'])

  // MCP servers connect AFTER boot and register their tools late.
  ctx.tools.register(tool('mcp__csharper-mcp__find_symbol_usages'))
  ctx.tools.register(tool('mcp__cwm-roslyn-navigator__detect_antipatterns'))
  assert.deepEqual(visible(ctx, agent.key), ['submit_decision'],
    'late-registered MCP tools must not reach the restricted session')

  // Nothing arriving later can either.
  ctx.tools.register(tool('shell'))
  ctx.tools.register(tool('browser'))
  ctx.tools.register(tool('workflow'))
  assert.deepEqual(visible(ctx, agent.key), ['submit_decision'])
})

test('B1 (regression): a deny-snapshot at boot CANNOT cover late registrations', async () => {
  const ctx = await mount()
  const { preset, agent } = await composition(ctx)
  agent.scope.ctx.tools.register(tool('submit_decision'))

  // The old mechanism: deny every global tool known at boot.
  ctx.tools.register(tool('usage_overview'))
  ctx.tools.register(tool('usage_query'))
  preset.scope.ctx.tools.restrict({ deny: ['usage_overview', 'usage_query'] })
  assert.deepEqual(visible(ctx, agent.key), ['submit_decision'])

  // A tool registering later is admitted by a deny-only filter, by design.
  ctx.tools.register(tool('mcp__csharper-mcp__find_symbol_usages'))
  assert.deepEqual(visible(ctx, agent.key),
    ['mcp__csharper-mcp__find_symbol_usages', 'submit_decision'],
    'this is exactly the leak the allow-list binding removes')

  // It cannot even be named before it exists (restrict validates eagerly).
  const fresh = await mount()
  const scoped = await mintScope(fresh, { id: 'uniagent-prod' })
  assert.throws(
    () => scoped.scope.ctx.tools.restrict({ deny: ['mcp__not__yet__registered'] }),
    /unknown global tool/)
})

test('B1 (regression): a same-layer registration is stripped by the preset allow-list', async () => {
  const ctx = await mount()
  const { preset, agent } = await composition(ctx)

  // The wrong shape: register submit_decision in the SAME scope that applies
  // the restriction. view() resolves a scope's own tools through the ancestor
  // chain, so the same-layer registration is filtered — the catalog is empty.
  preset.scope.ctx.tools.register(tool('submit_decision'))
  preset.scope.ctx.tools.restrict({ allow: [] })
  assert.deepEqual(visible(ctx, agent.key), [],
    'documents why the tool must live in the agent scope instead')
})

test('B1: the scope-aware guard refuses any non-manifest call', async () => {
  const ctx = await mount()
  const { preset, agent } = await composition(ctx)
  mountPresetPlugins(ctx, preset, agent)
  agent.scope.ctx.tools.guard(exec => (exec.name === 'submit_decision'
    ? undefined
    : `uniagent-prod: tool "${exec.name}" is not in the frozen capability manifest [submit_decision]`))

  ctx.tools.register(tool('mcp__csharper-mcp__find_symbol_usages'))

  const denied = await ctx.tools.execute({
    signal: new AbortController().signal,
    callId: 'call-1',
    name: 'mcp__csharper-mcp__find_symbol_usages',
    arguments: {},
    agent: agent.key,
  })
  assert.equal(denied.isError, true)
  assert.match(JSON.stringify(denied.content), /not in the frozen capability manifest/)

  const allowed = await ctx.tools.execute({
    signal: new AbortController().signal,
    callId: 'call-2',
    name: 'submit_decision',
    arguments: {},
    agent: agent.key,
  })
  assert.notEqual(allowed.isError, true)
})

test('B1: the binding is per-session — an ordinary session keeps the full surface', async () => {
  const ctx = await mount()
  const { preset, agent } = await composition(ctx)
  mountPresetPlugins(ctx, preset, agent)

  // A session that did NOT join the uniagent-prod preset is unaffected.
  const ordinary = await mintScope(ctx, { id: 'ordinary' })
  ctx.tools.register(tool('usage_overview'))
  ctx.tools.register(tool('shell'))
  assert.deepEqual(visible(ctx, ordinary.key), ['shell', 'usage_overview'])
  assert.deepEqual(visible(ctx, agent.key), ['submit_decision'])
})
