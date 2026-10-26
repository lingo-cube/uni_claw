'use strict';

// WORLD-VISUALIZATION-001 切片 2：WorldViz view-model（纯规范化，无 IO）。
// 输入 = 导出器经切片 0 只读缝产出的数据（PascalCase record 序列化）；
// 输出 = 渲染层 view model，四态约定（QV7）：unknown（未采集/未知）、
// empty（无数据）、failed（加载失败）由组件层表达，本层只做数据归一与
// not-collected 标注；缺省（deferred）功能以显式清单传递，不静默隐藏。

function markerChips(changes) {
  const c = changes || {};
  return {
    navigation: !!(c.NavigationVerified),
    correctionApplied: !!(c.CorrectionApplied),
    correctionRejected: !!(c.CorrectionRejected),
    semantic: !!(c.SemanticKnowledgeUpdated),
    reused: !!(c.SemanticKnowledgeReused),
    structural: Array.isArray(c.StructuralRelationChanges) && c.StructuralRelationChanges.length > 0,
  };
}

function normalizeRevision(summary) {
  if (!summary || typeof summary.RevisionId !== 'string') throw new TypeError('revision summary requires RevisionId');
  return {
    id: summary.RevisionId,
    parent: summary.ParentRevisionId ?? null,
    number: summary.RevisionNumber,
    // 采集时间高水位；提交墙钟未记录——渲染层必须原样呈现，不伪造。
    captureHighWater: summary.LatestCaptureTime ?? null,
    markers: markerChips(summary.Changes),
    changes: summary.Changes || null,
  };
}

function normalizeThumb(thumb) {
  if (!thumb) return { state: 'not-collected', reason: '无观察片段（未采集）' };
  const items = Array.isArray(thumb.items) ? thumb.items : [];
  const placed = items.filter((item) => typeof item.x1 === 'number' && typeof item.y1 === 'number');
  return {
    state: placed.length > 0 ? 'ok' : 'unknown',
    reason: placed.length > 0 ? null : '观察片段为空（未知）',
    width: Math.max(1, thumb.width || 1),
    height: Math.max(1, thumb.height || 1),
    captureId: thumb.captureId ?? null,
    isCurrent: !!thumb.isCurrent,
    // 真实设备截图（captureId 精确映射才有；否则 null → 渲染回退页面表达重绘）。
    screenshot: typeof thumb.screenshot === 'string' && thumb.screenshot.length > 0 ? thumb.screenshot : null,
    items: placed,
    viewUnknown: Array.isArray(thumb.viewUnknown) ? thumb.viewUnknown : [],
  };
}

function normalizeNode(node) {
  if (!node || typeof node.id !== 'string') throw new TypeError('graph node requires id');
  const labelUnknown = node.labelUnknown !== false && (node.label == null || node.label === '');
  return {
    id: node.id,
    label: labelUnknown ? null : node.label,
    // 语义挂钩（用户要求）：节点必须携带语义状态，未知不得伪装成"已确认无名称"。
    semantic: {
      state: labelUnknown ? 'unknown' : 'ok',
      reused: !!node.reused,
      conflicted: !!node.conflicted,
      knowledgeSource: node.knowledgeSource ?? null,
      evidenceRefs: Array.isArray(node.evidenceRefs) ? node.evidenceRefs : [],
    },
    // 纠错涟漪状态：唯一签名被 Applied 纠错撤走且无新签名——后续同签名观察会
    // 铸造新容器（产品语义），视图必须可见可解释，不静默。
    signatureWithdrawn: !!node.signatureWithdrawn,
    pageThumb: normalizeThumb(node.pageThumb),
  };
}

function normalizeGraph(graph, revisionId) {
  if (!graph) {
    return {
      state: 'unknown', revisionId, currentContainerId: null,
      unknown: ['source-revision-unknown'], nodes: [], navigationEdges: [], structuralRelations: [],
    };
  }
  return {
    state: Array.isArray(graph.nodes) && graph.nodes.length > 0 ? 'ok' : 'empty',
    revisionId: graph.revisionId ?? revisionId,
    currentContainerId: graph.currentContainerId ?? null,
    unknown: Array.isArray(graph.unknown) ? graph.unknown : [],
    nodes: (graph.nodes || []).map(normalizeNode),
    navigationEdges: (graph.navigationEdges || []).map((e) => ({ source: e.source, target: e.target, scope: e.scope ?? null })),
    structuralRelations: (graph.structuralRelations || []).map((e) => ({ source: e.source, target: e.target, kind: e.kind })),
  };
}

function createWorldVizViewModel(raw, selection = {}) {
  if (!raw || typeof raw !== 'object') throw new TypeError('raw world data is required');
  if (!raw.timeline || !Array.isArray(raw.timeline.Revisions)) throw new TypeError('raw.timeline.Revisions is required');
  if (!raw.graphs || typeof raw.graphs !== 'object') throw new TypeError('raw.graphs is required');
  const revisions = raw.timeline.Revisions.map(normalizeRevision);
  if (revisions.length === 0) {
    return {
      provenance: raw.provenance ?? '未知来源',
      empty: true,
      timeline: { revisions: [], currentRevisionId: null },
      graph: { state: 'empty', nodes: [], navigationEdges: [], structuralRelations: [], unknown: ['无 revision'] },
      detail: null,
      deferred: defaultDeferred(),
    };
  }
  const currentRevisionId = raw.timeline.CurrentRevisionId ?? revisions[revisions.length - 1].id;
  const revisionId = selection.revision && raw.graphs[selection.revision] ? selection.revision : currentRevisionId;
  const graph = normalizeGraph(raw.graphs[revisionId], revisionId);
  const selectedNode = selection.selectedNode
    ? graph.nodes.find((node) => node.id === selection.selectedNode) ?? null
    : null;
  return {
    provenance: raw.provenance ?? '未知来源',
    empty: false,
    timeline: { revisions, currentRevisionId },
    graph,
    selection: { revisionId, nodeId: selectedNode ? selectedNode.id : null },
    detail: selectedNode,
    deferred: defaultDeferred(),
  };
}

// QV7 缺省清单：可见入口占位，不静默隐藏；二期切片逐项解除。
function defaultDeferred() {
  return [
    { id: 'live-runtime-http', label: 'live 运行链（runtime-http 适配器）', note: '属切片 4，未实现' },
    { id: 'page-diff', label: '版本对比 diff 视图', note: '属切片 4，未实现' },
  ];
}

module.exports = { createWorldVizViewModel };
