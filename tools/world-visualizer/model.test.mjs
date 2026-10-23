import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import { validateDataset, changesBetween, changesBetweenFrames, normalize } from './model.mjs';

test('actual Owner exports keep frame/page/slice provenance aligned', async () => {
  for (const name of (await readdir(new URL('./examples/', import.meta.url))).filter(n => n.endsWith('.json') && n !== 'index.json')) {
    const raw = JSON.parse(await readFile(new URL('./examples/' + name, import.meta.url), 'utf8'));
    const data = validateDataset(raw);
    assert.ok(data.frames.length, name);
    assert.equal(data.frames[0].revision.revisionId.length > 0, true, name);
  }
});

test('frame comparison includes actual scope, page, knowledge and result updates at the same W', async () => {
  const recovery=validateDataset(JSON.parse(await readFile(new URL('./examples/recovery.json',import.meta.url),'utf8')));
  assert.equal(recovery.frames[0].revision.revisionId,recovery.frames[1].revision.revisionId);
  assert.ok(changesBetweenFrames(...recovery.frames).some(c=>c.field==='recovery'));
  const a={revision:{revisionId:'w'},pages:[{containerId:'c',unknown:['x']}],slices:[{sliceId:'s',slice:{rootContainerId:'c'}}],knowledge:{gaps:[{gapId:'g',reason:'old'}]}};
  const b=structuredClone(a);b.pages[0].unknown=[];b.slices=[];b.knowledge.gaps[0].reason='new';
  const fields=changesBetweenFrames(a,b).map(c=>c.field);
  for(const field of ['pages','slices','knowledge.gaps'])assert.ok(fields.includes(field),field);
});

test('reject cross-W page and current slice instead of silently repairing provenance', () => {
  const base = {schemaVersion:'world-visualization.v1', id:'x', title:'x', provenance:{}, frames:[{key:'a', revision:{revisionId:'rev-1', containers:[]}, pages:[], slices:[]}]};
  const page = structuredClone(base); page.frames[0].pages = [{sourceRevisionId:'rev-2'}];
  assert.throws(() => validateDataset(page), /版本/);
  const slice = structuredClone(base); slice.frames[0].slices = [{sliceId:'s',slice:{sourceRevisionId:'rev-2'}}];
  assert.throws(() => validateDataset(slice), /版本/);
  assert.throws(() => validateDataset({...base, frames:[]}), /时点/);
});

test('dictionary subject keys are not camel-cased; disappearance is not physical deletion', () => {
  const d = normalize({WorldState:{'UI.Subject':{Value:'x'}}});
  assert.equal(d.worldState['UI.Subject'].value, 'x');
  const r = {revisionId:'a', containers:[{identity:{containerId:'c'}}], occurrences:[{occurrenceId:'o', owningContainerId:'c', state:'on'}]};
  const diffs = changesBetween(r, {...r, revisionId:'b', occurrences:[]});
  assert.equal(diffs[0].type, '本版未再观察');
  assert.equal(diffs[0].id, 'o');
});
