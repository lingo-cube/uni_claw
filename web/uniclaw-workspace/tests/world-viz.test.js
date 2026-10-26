'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');

const { createWorldVizViewModel } = require('../src/features/world-viz/world-viz-view-model');
const { renderWorldVizHtml } = require('../src/features/world-viz/render-world-viz-html');
const { createWorldViz } = require('../src/features/world-viz/create-world-viz');
const { createWorldFixtureDataSource } = require('../src/adapters/world-fixture');

function revision(id, number, overrides = {}) {
  return { RevisionId: id, ParentRevisionId: number > 1 ? `rev-${number - 1}` : null, RevisionNumber: number,
    LatestCaptureTime: `2026-10-23T09:0${number}:00.000Z`,
    Changes: { PageObservation: true, ObservedContainerIds: [], NavigationVerified: false, CorrectionApplied: false,
      CorrectionRejected: false, CorrectedCaptureEvidenceIds: [], SemanticKnowledgeUpdated: false,
      SemanticKnowledgeReused: false, StructuralRelationChanges: [], NewEvidenceIds: [], ...overrides } };
}

function graph(nodes, { unknown = ['navigation-graph-is-observation-bounded'], current = null, nav = [], struct = [] } = {}) {
  return { revisionId: 'rev-1', currentContainerId: current, unknown,
    nodes: nodes.map(([id, label, thumb]) => ({ id, label, labelUnknown: label == null, reused: false, conflicted: false,
      knowledgeSource: label ? 'rev-1' : null, evidenceRefs: label ? ['ev-1', 'ev-2'] : [], pageThumb: thumb })),
    navigationEdges: nav.map(([source, target]) => ({ kind: 'navigatesTo', source, target, scope: 'run:t' })),
    structuralRelations: struct.map(([source, target]) => ({ kind: 'contains', source, target })) };
}

function thumb(items) {
  return { width: 1080, height: 2160, captureId: 'ev-cap-0001', isCurrent: true,
    items: items.map(([role, text, x1, y1, x2, y2]) => ({ role, text, state: null, x1, y1, x2, y2 })), viewUnknown: ['whole-page-coverage-unproven'] };
}

function fixture({ revisions, graphs, provenance = 'test-fixture' } = {}) {
  const revs = revisions ?? [
    revision('rev-1', 1, { NavigationVerified: true }),
    revision('rev-2', 2, { SemanticKnowledgeUpdated: true, SemanticKnowledgeReused: true }),
    revision('rev-3', 3, { CorrectionApplied: true, CorrectedCaptureEvidenceIds: ['ev-cap-0001'] }),
  ];
  const gs = graphs ?? {
    'rev-1': graph([['ctr-a', '设置', thumb([['title', '设置', 0, 0, 1000, 180], ['row', 'Wi-Fi', 0, 300, 1000, 420]])], ['ctr-b', null, null]], { current: 'ctr-a', nav: [['ctr-a', 'ctr-b']] }),
    'rev-2': graph([['ctr-a', '设置', thumb([['title', '设置', 0, 0, 1000, 180]])]], { current: 'ctr-a' }),
    'rev-3': graph([['ctr-a', '设置', thumb([['title', '设置', 0, 0, 1000, 180]])]], { current: 'ctr-a' }),
  };
  return { provenance, timeline: { Revisions: revs, CurrentRevisionId: 'rev-3' }, graphs: gs };
}

test('view-model：PascalCase 归一 + 语义未知不折叠', () => {
  const viewModel = createWorldVizViewModel(fixture(), { revision: 'rev-1' });
  assert.equal(viewModel.timeline.revisions.length, 3);
  assert.equal(viewModel.timeline.currentRevisionId, 'rev-3');
  const labeled = viewModel.graph.nodes.find((n) => n.id === 'ctr-a');
  const unknown = viewModel.graph.nodes.find((n) => n.id === 'ctr-b');
  assert.equal(labeled.semantic.state, 'ok');
  assert.equal(labeled.label, '设置');
  assert.equal(unknown.semantic.state, 'unknown');
  assert.equal(unknown.pageThumb.state, 'not-collected');
  assert.equal(viewModel.deferred.length, 2); // QV7：缺省清单显式携带
});

test('view-model：marker chips 从 Changes 派生', () => {
  const viewModel = createWorldVizViewModel(fixture());
  const first = viewModel.timeline.revisions.find((r) => r.id === 'rev-1');
  assert.equal(first.markers.navigation, true);
  const second = viewModel.timeline.revisions.find((r) => r.id === 'rev-2');
  assert.equal(second.markers.semantic, true);
  assert.equal(second.markers.reused, true);
  const third = viewModel.timeline.revisions.find((r) => r.id === 'rev-3');
  assert.equal(third.markers.correctionApplied, true);
});

test('view-model：空 World → empty 态，不伪造', () => {
  const viewModel = createWorldVizViewModel({ provenance: 'p', timeline: { Revisions: [], CurrentRevisionId: null }, graphs: {} });
  assert.equal(viewModel.empty, true);
  const html = renderWorldVizHtml(viewModel);
  assert.match(html, /无数据（空 World/);
});

test('view-model：非法 revision 选择回退 current，不抛', () => {
  const viewModel = createWorldVizViewModel(fixture(), { revision: 'rev-404' });
  assert.equal(viewModel.selection.revisionId, 'rev-3');
});

test('renderer：节点缩略、语义徽章、缺省标注、XSS 转义', () => {
  const raw = fixture();
  raw.graphs['rev-3'].nodes[0].label = '<script>alert(1)</script>';
  const viewModel = createWorldVizViewModel(raw);
  const html = renderWorldVizHtml(viewModel);
  assert.match(html, /data-node="ctr-a"/);
  assert.match(html, /wv-ti wv-title/); // title 项渲染
  assert.match(html, /语义未知/); // ctr-b 未知徽章
  assert.match(html, /缺省 · live 运行链/); // QV7 缺省占位
  assert.match(html, /提交时间未记录/); // 时间诚实
  assert.doesNotMatch(html, /<script>alert/); // 转义
  assert.match(html, /&lt;script&gt;/);
});

test('renderer：无观察片段占位（斜纹文案），不画空屏', () => {
  const viewModel = createWorldVizViewModel(fixture(), { revision: 'rev-1' });
  const html = renderWorldVizHtml(viewModel);
  assert.match(html, /无观察片段（未采集）/);
});

test('adapter：shape 校验 fail-closed（同步拒绝）', () => {
  const source = createWorldFixtureDataSource(fixture());
  assert.equal(source.describe(), 'test-fixture');
  assert.throws(() => createWorldFixtureDataSource({ timeline: { Revisions: [] } }), TypeError);
  const broken = fixture();
  delete broken.graphs['rev-2'];
  assert.throws(() => createWorldFixtureDataSource(broken), /every revision must have a matching graph/);
});

test('renderer：签名撤回徽章与详情说明（纠错涟漪可见）', () => {
  const raw = fixture();
  raw.graphs['rev-3'].nodes[0].signatureWithdrawn = true;
  const viewModel = createWorldVizViewModel(raw, { selectedNode: 'ctr-a' });
  const html = renderWorldVizHtml(viewModel);
  assert.match(html, /签名被撤回/);
  assert.match(html, /后续同签名观察会创建新容器/);
});

test('component：挂载契约 + 展示性事件 + 失败态', async () => {
  const mounts = [];
  const events = [];
  const component = createWorldViz({
    dataSource: createWorldFixtureDataSource(fixture()),
    mount: (payload) => mounts.push(payload),
  });
  component.onEvent('loaded', (e) => events.push(['loaded', e]));
  component.onEvent('revision-select', (e) => events.push(['rev', e]));
  component.onEvent('node-select', (e) => events.push(['node', e]));
  await component.start();
  assert.equal(component.getState().phase, 'ready');
  assert.equal(mounts.length, 1);
  assert.match(mounts[0].html, /data-role="world-viz"/);
  assert.match(mounts[0].html, /data-node="ctr-a"/);
  assert.equal(events[0][0], 'loaded');
  assert.equal(component.selectRevision('rev-1'), true);
  assert.equal(events.filter((e) => e[0] === 'rev').length, 1);
  assert.equal(component.selectRevision('rev-404'), false); // 非法选择拒绝
  assert.equal(component.selectNode('ctr-b'), true);
  assert.match(mounts[mounts.length - 1].html, /ctr-b/);
  component.dispose();
  assert.equal(component.getState().phase, 'idle');
});

test('component：加载失败 → failed 态渲染 + error 事件', async () => {
  const mounts = [];
  const errors = [];
  const component = createWorldViz({
    dataSource: { load: async () => { throw new Error('boom'); } },
    mount: (payload) => mounts.push(payload),
  });
  component.onEvent('error', (e) => errors.push(e));
  await component.start();
  assert.equal(component.getState().phase, 'failed');
  assert.match(mounts[0].html, /加载失败：boom/);
  assert.equal(errors.length, 1);
});

test('component：缺 dataSource/mount fail-closed', () => {
  assert.throws(() => createWorldViz({}), TypeError);
  assert.throws(() => createWorldViz({ dataSource: { load: async () => ({}) } }), TypeError);
});
