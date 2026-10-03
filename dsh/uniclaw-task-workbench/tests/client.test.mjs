// PNL-001 S3 client bundle smoke tests (node:test, no DSH runtime).
// Same discipline as decision-channel's client smoke: a fake
// window.__ModuleLoader__ captures the bundle definition, a react stub drives
// factory() execution, and apply() is exercised without any real host.
// PNL-004：UniClaw Product Workspace 消费三个只读投影和一个 Host launch command。

import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'

const PACKAGE_ROOT = dirname(dirname(fileURLToPath(import.meta.url)))

const reactStub = {
  createElement: (...args) => ({ $$el: args }),
  useState: () => [0, () => {}],
  useEffect: () => {},
}

/** Evaluate src/client.js against a fake ModuleLoader; returns the loaded def.
 * Cache-busted per call: ESM would otherwise serve the first import and the
 * bundle would never re-run against the fake loader. */
let importSeq = 0
async function loadClient() {
  let loaded = null
  globalThis.window = { __ModuleLoader__: { load(def) { loaded = def } } }
  try {
    await import(join(PACKAGE_ROOT, 'src', 'client.js') + `?smoke=${++importSeq}`)
  } finally {
    delete globalThis.window
  }
  assert.notEqual(loaded, null, 'client.js must call window.__ModuleLoader__.load')
  return loaded
}

const fakeRequire = (name) => {
  if (name !== 'react') throw new Error(`unexpected require: ${name}`)
  return reactStub
}

test('client smoke: loader id and exports shape', async () => {
  const loaded = await loadClient()
  assert.equal(loaded.id, '@uniclaw/dsh-task-workbench')
  const mod = loaded.factory(fakeRequire)
  assert.equal(typeof mod.apply, 'function')
  assert.deepEqual(mod.inject, ['slots', 'remote'])
})

test('client smoke: workspace/session/artifact contribution, parameter-object shape', async () => {
  const loaded = await loadClient()
  const mod = loaded.factory(fakeRequire)
  // Capture the contribution via remote.$mount. $mount is lazy in the client
  // (first data load), so the test renders the sidebar entry through the
  // registered slot and clicks it to trigger ensureMounted().
  const mounted = []
  const renders = new Map()
  const ctx = {
    get(key) {
      if (key === 'slots') {
        return {
          inject: (_name, register) => register(),
          register: (meta, render) => { renders.set(meta.id, render) },
        }
      }
      if (key === 'remote') {
        return { $mount: (contribution) => { mounted.push(contribution); return Promise.resolve() } }
      }
      return undefined
    },
  }
  await mod.apply(ctx)
  assert.deepEqual([...renders.keys()].sort(), ['uniclaw-task-panel-entry', 'uniclaw-task-panel-overlay'])
  const entryRender = renders.get('uniclaw-task-panel-entry')
  let el = entryRender({}) // react stub element: { $$el: [type, props, …] }
  // Unwrap nested component elements (Entry → button) until a host element.
  while (typeof el.$$el[0] === 'function') el = el.$$el[0](el.$$el[1])
  el.$$el[1].onClick() // Entry onClick → setOpen + loadWorkspace → $mount
  await new Promise((resolve) => setTimeout(resolve, 0))
  assert.equal(mounted.length, 1)
  const ids = mounted[0].descriptors.map((d) => d.id)
  // PNL-004 API: three read-only workspace methods plus the explicit launch command.
  assert.deepEqual(ids, ['uniclawTaskPanel.workspace', 'uniclawTaskPanel.session', 'uniclawTaskPanel.artifact', 'uniclawTaskPanel.launch'])
  for (const d of mounted[0].descriptors) {
    assert.equal(d.namespace, 'uniclawTaskPanel')
    assert.equal(d.result.mode, 'strict')
    // strict 透传 codec（本构建形态）：create() 物化 { parse }，identity parse。
    assert.equal(d.result.create().parse({ any: 'shape' }).any, 'shape')
    for (const p of d.parameters) {
      // 参数 descriptor 形状（本构建 typert）：{name,wire,source,codec} 对象。
      assert.equal(typeof p.name, 'string')
      assert.equal(p.wire, p.name)
      assert.equal(p.source, 'json')
      assert.equal(p.codec.mode, 'strict')
    }
  }
  const session = mounted[0].descriptors.find((d) => d.method === 'session')
  assert.deepEqual(session.parameters.map((p) => p.name), ['sessionId'])
  const workspace = mounted[0].descriptors.find((d) => d.method === 'workspace')
  assert.equal(workspace.parameters.length, 0)
  const artifact = mounted[0].descriptors.find((d) => d.method === 'artifact')
  assert.deepEqual(artifact.parameters.map((p) => p.name), ['sessionId', 'ref'])
  const launch = mounted[0].descriptors.find((d) => d.method === 'launch')
  assert.deepEqual(launch.parameters.map((p) => p.name), ['schemaVersion', 'contractVersion', 'launchRequestId', 'projectRef', 'testSetRef', 'taskRef', 'idempotencyKey', 'correlationId', 'requestedAt', 'metadata', 'taskId'])
})

test('client smoke: loading workspace and first session mounts the remote contribution once', async () => {
  const loaded = await loadClient()
  const mod = loaded.factory(fakeRequire)
  const mounted = []
  const renders = new Map()
  const remoteNamespace = {
    workspace: () => Promise.resolve({
      success: true,
      projects: [{
        projectId: '/tmp/project',
        name: 'project',
        instances: [{ instanceId: 'instance-1', sessionId: 'session-1', status: 'active', task: { title: 'task' } }],
      }],
    }),
    session: () => Promise.resolve({ success: true, conversation: [], dshTrace: [], uniclawTrace: [], uniflowTrace: [], evidence: [] }),
  }
  const ctx = {
    get(key) {
      if (key === 'slots') {
        return {
          inject: (_name, register) => register(),
          register: (meta, render) => { renders.set(meta.id, render) },
        }
      }
      if (key === 'remote') return { $mount: (contribution) => { mounted.push(contribution); return Promise.resolve() } }
      if (key === 'remote.uniclawTaskPanel') return remoteNamespace
      return undefined
    },
  }
  await mod.apply(ctx)
  let el = renders.get('uniclaw-task-panel-entry')({})
  while (typeof el.$$el[0] === 'function') el = el.$$el[0](el.$$el[1])
  el.$$el[1].onClick()
  await new Promise((resolve) => setTimeout(resolve, 0))
  await new Promise((resolve) => setTimeout(resolve, 0))
  assert.equal(mounted.length, 1)
})

test('client smoke: apply with no slots degrades silently', async () => {
  const loaded = await loadClient()
  const mod = loaded.factory(fakeRequire)
  assert.doesNotThrow(() => mod.apply({ get: () => undefined }))
})

test('client smoke: RPC failure surfaces as structured error, panel does not throw', async () => {
  const loaded = await loadClient()
  const mod = loaded.factory(fakeRequire)
  // remote present, namespace missing → callRpc resolves a structured error
  // instead of rejecting; apply still registers the slots.
  const registered = []
  const ctx = {
    get(key) {
      if (key === 'slots') {
        return {
          inject(name, register) { register() },
          register(meta, render) { registered.push(meta.id) },
        }
      }
      if (key === 'remote') return { $mount: () => Promise.resolve() }
      return undefined // remote.uniclawTaskPanel 未挂载
    },
  }
  await mod.apply(ctx)
  assert.deepEqual(registered.sort(), ['uniclaw-task-panel-entry', 'uniclaw-task-panel-overlay'])
})

test('client smoke: source contains the descriptor bridge and workspace UI contract', async () => {
  const source = readFileSync(join(PACKAGE_ROOT, 'src', 'client.js'), 'utf8')
  assert.ok(source.includes('const contribution={package:"@uniclaw/dsh-task-workbench"'))
  assert.ok(source.includes('remote.uniclawTaskPanel'))
  assert.ok(source.includes('workspaceStyleText'))
  assert.ok(source.includes('uniclaw-workspace-styles'))
  for (const required of ['Uni-Agent 解决过程', '执行与验证', 'Trace', 'Evidence', 'Metadata', '查看明细', 'conversationGroups', 'runStages']) {
    assert.ok(source.includes(required), `workspace UI missing: ${required}`)
  }
})
