import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import { validateDataset, changesBetween, changesBetweenFrames, normalize, pageGeometry } from './model.mjs';

test('page display preserves device pixel proportions for portrait, landscape and square captures', () => {
  const elements=[{locator:{x1:0,y1:0,x2:1,y2:1}}];
  for(const [pixelWidth,pixelHeight] of [[1080,2400],[2400,1080],[1000,1000]]) {
    const g=pageGeometry(elements,{pixelWidth,pixelHeight});
    assert.equal(g.aspectKnown,true);
    assert.ok(Math.abs(g.scaleY/g.scaleX-pixelHeight/pixelWidth)<1e-10,`${pixelWidth}×${pixelHeight}`);
    assert.ok(Math.abs((g.width-20)/(g.height-20)-pixelWidth/pixelHeight)<1e-10);
  }
});

test('accumulated bounds keep negative and offscreen coordinates without changing pixel proportions', () => {
  const g=pageGeometry([{locator:{x1:-.2,y1:-.5,x2:1.2,y2:2}}],{pixelWidth:1080,pixelHeight:2400});
  assert.equal(g.minX,-.2);assert.equal(g.minY,-.5);
  assert.ok(Math.abs(g.scaleY/g.scaleX-2400/1080)<1e-10);
  assert.ok(Math.abs(g.height-(2.5*g.scaleY+20))<1e-10);
});

test('missing or invalid device dimensions remain explicitly unknown', () => {
  for(const space of [undefined,{}, {pixelWidth:0,pixelHeight:2400},{pixelWidth:1080,pixelHeight:NaN}]) {
    const g=pageGeometry([{locator:{x1:0,y1:0,x2:1,y2:1}}],space);
    assert.equal(g.aspectKnown,false);assert.equal(g.scaleX,g.scaleY);
  }
});

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
