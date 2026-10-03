'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { ensureWorkspaceStyles, envelope } = require('../src/browser/entry');

test('injects shared workspace styles once through the browser document seam', () => {
  const styles = [];
  const document = {
    head: { appendChild: (style) => styles.push(style) },
    createElement: (tagName) => ({ tagName, setAttribute() {}, textContent: '', id: '' }),
    getElementById: (id) => styles.find((style) => style.id === id) || null,
  };
  const container = { ownerDocument: document };
  ensureWorkspaceStyles(container, '.workspace { display: grid; }');
  ensureWorkspaceStyles(container, '.workspace { display: block; }');
  assert.equal(styles.length, 1);
  assert.equal(styles[0].textContent, '.workspace { display: grid; }');
  assert.equal(styles[0].id, 'uniclaw-workspace-styles');
});

test('normalizes DSH panel success responses at the host seam', () => {
  const result = envelope({ success: true, projects: [] }, 'TaskQuery');
  assert.equal(result.ok, true);
  assert.deepEqual(result.data.projects, []);
  assert.equal(result.capability, 'TaskQuery');
  assert.equal(typeof result.observedAt, 'string');
});

test('unwraps Typert success envelopes around the panel payload', () => {
  const result = envelope({ ok: true, data: { success: true, data: { projects: [] } } }, 'TaskQuery');
  assert.equal(result.ok, true);
  assert.deepEqual(result.data.projects, []);
  assert.equal(typeof result.observedAt, 'string');
});

test('unwraps the DSH remote value envelope used by static panel clients', () => {
  const result = envelope({ ok: true, value: { success: true, projects: [] } }, 'TaskQuery');
  assert.equal(result.ok, true);
  assert.deepEqual(result.data.projects, []);
});

test('normalizes DSH panel failures without leaking host shape into shared UI', () => {
  const result = envelope({ success: false, error: { code: 'not-found', message: 'missing' } }, 'SessionQuery');
  assert.equal(result.ok, false);
  assert.equal(result.error.code, 'not-found');
  assert.equal(result.error.message, 'missing');
});
