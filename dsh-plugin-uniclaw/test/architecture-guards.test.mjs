/**
 * Architecture guards for the Shadow Cognition vertical slice (§53, F16/F17).
 *
 * The gate's production footprint is `dsh-plugin-uniclaw` ONLY:
 *   RuntimeFilesChanged = MUST_BE_NONE, DriverHostFilesChanged = MUST_BE_NONE,
 *   zero new wire methods, no new runtime emitters, no session persistence.
 *
 * Guards:
 *  1. Hard-forbidden Kernel/Runtime/session machinery tokens have ZERO matches
 *     anywhere in the plugin source (src/**): ADB, PhysicalEnvironment,
 *     DeviceAction, sessionPersistence, PersistenceCoordinator, and any
 *     session write (session.append / Session.create / new Session / .flush()).
 *     The plugin never writes to a session; command lifecycle events are
 *     appended by the real dsh-commands registry, not by this code.
 *  2. Runtime-semantics nouns (Container, Binding, StateBelief) have zero
 *     bare-word matches in src/** — the slice only ever touches them as
 *     camelCase read-model field labels (currentContainerSummary,
 *     bindingsSummary, stateBeliefsSummary), which word-boundary regexes do
 *     not match.
 *  3. `GoalEvidence` is allowed ONLY in the frozen epistemic language
 *     (OBS-F9): the exact phrase "Kernel GoalEvidence indicates ..." in the
 *     model prompt, never in a creation/mutation verb position.
 *  4. The adapter's wire method table (_request literals) is EXACTLY the
 *     frozen 8 read-only methods — zero new wire methods (F16).
 *  5. Zero shadow footprint under src/UniClaw.Runtime: the modified-file set
 *     there is exactly the pre-existing Phase 0-3 baseline recorded at gate
 *     start (2026-08-15), with zero untracked files and zero shadow-related
 *     additions in the runtime diffs.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, '..', '..');
const pluginRoot = join(repoRoot, 'dsh-plugin-uniclaw');
const srcRoot = join(pluginRoot, 'src');

/** Every plugin source file (recursive, deterministic order). */
function listSourceFiles() {
  const out = [];
  const walk = (dir) => {
    for (const entry of readdirSync(dir).sort()) {
      const full = join(dir, entry);
      const st = statSync(full);
      if (st.isDirectory()) walk(full);
      else if (entry.endsWith('.js')) out.push(full);
    }
  };
  walk(srcRoot);
  return out;
}

const FROZEN_WIRE_METHODS = [
  'ping',
  'run.list',
  'run.snapshot.get',
  'run.trap.get',
  'run.events.after',
  'run.events.drain',
  'evidence.get',
  'control.support',
];

/** Pre-existing Phase 0-3 baseline modifications under src/UniClaw.Runtime,
 * captured at gate start 2026-08-15 (before any shadow work). Any file OUTSIDE
 * this recorded set appearing as modified/added/untracked fails the guard. */
const RUNTIME_BASELINE_MODIFIED = [
  'src/UniClaw.Runtime/Agent/Agent.SemanticRun.cs',
  'src/UniClaw.Runtime/Agent/Agent.cs',
  'src/UniClaw.Runtime/Model/Actions/DeviceAction.cs',
  'src/UniClaw.Runtime/Startup/Startup.cs',
  'src/UniClaw.Runtime/Traversal/Traversal.cs',
];

function git(args) {
  return execFileSync('git', ['-C', repoRoot, ...args], { encoding: 'utf8' });
}

test('F16/F17 guards: plugin source never touches Kernel/Runtime/session machinery', () => {
  const files = listSourceFiles();
  assert.ok(files.length >= 8, `plugin source tree is present (${files.length} files)`);

  // 1. Hard-forbidden tokens — ZERO matches anywhere in src/**.
  const hardForbidden = /ADB|PhysicalEnvironment|DeviceAction|sessionPersistence|PersistenceCoordinator|session\.append|Session\.create|new Session|\.flush\(|\.append\(/;
  const hardHits = [];
  for (const file of files) {
    const text = readFileSync(file, 'utf8');
    const lines = text.split('\n');
    for (let i = 0; i < lines.length; i += 1) {
      if (hardForbidden.test(lines[i])) hardHits.push(`${relative(pluginRoot, file)}:${i + 1}: ${lines[i].trim()}`);
    }
  }
  assert.deepEqual(hardHits, [], 'no Kernel/Runtime/session-write machinery referenced in plugin source');

  // 2. Runtime-semantics nouns as bare words — ZERO matches.
  const nounForbidden = /\b(Container|Binding|StateBelief)\b/;
  const nounHits = [];
  for (const file of files) {
    const text = readFileSync(file, 'utf8');
    const lines = text.split('\n');
    for (let i = 0; i < lines.length; i += 1) {
      if (nounForbidden.test(lines[i])) nounHits.push(`${relative(pluginRoot, file)}:${i + 1}: ${lines[i].trim()}`);
    }
  }
  assert.deepEqual(nounHits, [], 'runtime-semantics nouns appear only as camelCase read-model labels, never bare');

  // 3. GoalEvidence: every bare occurrence must be the frozen epistemic
  //    language (OBS-F9: "Kernel GoalEvidence indicates ..." / unavailable),
  //    never in a creation/mutation verb position.
  const goalEvidence = /\bGoalEvidence\b/;
  const badGoalHits = [];
  let goalOccurrences = 0;
  for (const file of files) {
    const text = readFileSync(file, 'utf8');
    const lines = text.split('\n');
    for (let i = 0; i < lines.length; i += 1) {
      if (!goalEvidence.test(lines[i])) continue;
      goalOccurrences += 1;
      const line = lines[i];
      const okLanguage = line.includes('Kernel GoalEvidence') && !/(create|emit|produce|append|write|set|persist|mutate|insert|raise)\b/i.test(line);
      if (!okLanguage) badGoalHits.push(`${relative(pluginRoot, file)}:${i + 1}: ${line.trim()}`);
    }
  }
  assert.ok(goalOccurrences >= 1, 'the frozen epistemic GoalEvidence language is present');
  assert.deepEqual(badGoalHits, [], 'GoalEvidence appears only in the frozen epistemic language');
});

test('F16: adapter wire method table is exactly the frozen 8 read-only methods', () => {
  const adapterSource = readFileSync(join(srcRoot, 'adapter.js'), 'utf8');
  const literals = [...adapterSource.matchAll(/_request\('([a-z.]+)'/g)].map((m) => m[1]);
  assert.deepEqual([...new Set(literals)].sort(), [...FROZEN_WIRE_METHODS].sort(), 'wire method set matches the frozen read-only table');
  assert.equal(new Set(literals).size, FROZEN_WIRE_METHODS.length, 'no duplicate or additional wire methods');
});

test('F16: zero shadow footprint under src/UniClaw.Runtime', () => {
  const porcelain = git(['status', '--porcelain', '--', 'src/UniClaw.Runtime'])
    .split('\n')
    .filter((line) => line.trim().length > 0);

  const modifiedNow = porcelain.filter((line) => line.startsWith(' M ')).map((line) => line.slice(3));
  const addedOrUntracked = porcelain.filter((line) => !line.startsWith(' M '));

  assert.deepEqual(
    [...modifiedNow].sort(),
    [...RUNTIME_BASELINE_MODIFIED].sort(),
    'modified runtime files are exactly the pre-existing Phase 0-3 baseline (no new modifications)',
  );
  assert.deepEqual(addedOrUntracked, [], 'zero added/untracked files under src/UniClaw.Runtime');

  const runtimeDiff = git(['diff', '--', 'src/UniClaw.Runtime']);
  assert.ok(!/shadow|cognitive/i.test(runtimeDiff), 'runtime diffs contain zero shadow-related additions');
});
