// PNL-001 S2 mechanism tests for the uniagent-task scope binding.
//
// These run against a MOCK host ctx: what this package owns is (a) the
// fail-closed validation of the preset row's config.allow and (b) that the
// validated list reaches ctx.tools.restrict verbatim, exactly once. The
// live-catalog semantics of restrict() itself (late MCP tools stay excluded)
// are DSH's contract and are proven by dsh/uniclaw-restrict's tests against
// a real ToolRuntime; they are not re-proved here.

import test from 'node:test'
import assert from 'node:assert/strict'

import { apply } from '../src/index.js'

/** Mock ctx that records restrict calls, like the real registry would. */
function mockCtx() {
  const restrictCalls = []
  return {
    restrictCalls,
    tools: {
      restrict: (filter) => restrictCalls.push(filter),
      schemas: () => [],
    },
  }
}

const VALID = ['read', 'glob', 'grep', 'bash']

test('task-scope: a valid allow list reaches restrict verbatim, exactly once', () => {
  const ctx = mockCtx()
  apply(ctx, { allow: VALID })
  assert.equal(ctx.restrictCalls.length, 1)
  assert.deepEqual(ctx.restrictCalls[0], { allow: VALID })
})

test('task-scope: config is passed as the whole config object; allow is what restrict sees', () => {
  const ctx = mockCtx()
  apply(ctx, { allow: ['shell'] })
  assert.deepEqual(ctx.restrictCalls[0], { allow: ['shell'] })
})

test('task-scope: missing config / missing allow fails closed at mount', () => {
  assert.throws(() => apply(mockCtx(), undefined), /uniclaw-task-scope/)
  assert.throws(() => apply(mockCtx(), {}), /uniclaw-task-scope/)
})

test('task-scope: empty allow array fails closed (no denial-by-accident)', () => {
  assert.throws(() => apply(mockCtx(), { allow: [] }), /uniclaw-task-scope/)
})

test('task-scope: non-string elements fail closed', () => {
  assert.throws(() => apply(mockCtx(), { allow: ['read', 42] }), /uniclaw-task-scope/)
  assert.throws(() => apply(mockCtx(), { allow: [null] }), /uniclaw-task-scope/)
})

test('task-scope: non-array allow fails closed (never a silent deny fallback)', () => {
  assert.throws(() => apply(mockCtx(), { allow: 'read' }), /uniclaw-task-scope/)
  assert.throws(() => apply(mockCtx(), { allow: { 0: 'read' } }), /uniclaw-task-scope/)
})

test('task-scope: an unusable host tools registry fails closed', () => {
  assert.throws(() => apply({}, { allow: VALID }), /host tools registry unavailable/)
})
