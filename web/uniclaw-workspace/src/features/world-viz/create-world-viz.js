'use strict';

// WORLD-VISUALIZATION-001 切片 2：WorldViz 组件（D1 外层挂载契约）。
// createWorldViz({ dataSource, mount, render, options }) → 实例。
// - 取数只经 dataSource 缝（两层可组装的外层）；宿主只看到展示性事件，
//   无执行语义；阅读状态只驻留本组件生命周期（沿 workbench 纪律）。

const { createWorldVizViewModel } = require('./world-viz-view-model');
const { renderWorldVizHtml } = require('./render-world-viz-html');

function createWorldViz({ dataSource, mount, render = renderWorldVizHtml, options = {} } = {}) {
  if (!dataSource || typeof dataSource.load !== 'function') throw new TypeError('dataSource.load is required');
  if (typeof mount !== 'function') throw new TypeError('mount is required');
  if (typeof render !== 'function') throw new TypeError('render must be a function');

  const listeners = new Map();
  let phase = 'idle'; // idle | loading | ready | failed
  let raw = null;
  let error = null;
  let revision = null;
  let selectedNode = null;
  let loadToken = 0;

  const emit = (type, payload) => {
    for (const handler of listeners.get(type) || []) handler(payload);
  };

  const publish = () => {
    if (phase === 'loading') return;
    if (phase === 'failed') {
      const failedModel = { failed: true, error, provenance: dataSource.describe ? dataSource.describe() : '未知来源', deferred: [] };
      mount({ html: render(failedModel), viewModel: failedModel });
      return;
    }
    const viewModel = createWorldVizViewModel(raw, { revision, selectedNode });
    mount({ html: render(viewModel), viewModel });
  };

  function start() {
    if (phase === 'loading' || phase === 'ready') return Promise.resolve();
    const token = ++loadToken;
    phase = 'loading';
    error = null;
    return Promise.resolve().then(() => dataSource.load()).then((data) => {
      if (token !== loadToken) return;
      raw = data;
      phase = 'ready';
      revision = options.initialRevision && data.graphs && data.graphs[options.initialRevision]
        ? options.initialRevision : null;
      selectedNode = null;
      emit('loaded', { revisions: raw.timeline ? raw.timeline.Revisions.length : 0 });
      publish();
    }).catch((loadError) => {
      if (token !== loadToken) return;
      phase = 'failed';
      error = loadError && loadError.message ? loadError.message : String(loadError);
      emit('error', { error });
      publish();
    });
  }

  function update() {
    if (phase !== 'ready' && phase !== 'failed') return;
    publish();
  }

  function selectRevision(revisionId) {
    if (phase !== 'ready' || !raw.graphs || !raw.graphs[revisionId]) return false;
    revision = revisionId;
    selectedNode = null;
    publish();
    emit('revision-select', { revisionId });
    return true;
  }

  function selectNode(nodeId) {
    if (phase !== 'ready') return false;
    selectedNode = nodeId;
    publish();
    emit('node-select', { nodeId, revisionId: revision });
    return true;
  }

  function onEvent(type, handler) {
    if (typeof handler !== 'function') throw new TypeError('handler must be a function');
    if (!listeners.has(type)) listeners.set(type, new Set());
    listeners.get(type).add(handler);
    return () => listeners.get(type).delete(handler);
  }

  function dispose() {
    loadToken += 1;
    phase = 'idle';
    raw = null;
    error = null;
    revision = null;
    selectedNode = null;
    listeners.clear();
  }

  return Object.freeze({ start, update, selectRevision, selectNode, onEvent, dispose,
    getState: () => ({ phase, revision, selectedNode, error }) });
}

module.exports = { createWorldViz };
