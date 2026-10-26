'use strict';

// WORLD-VISUALIZATION-001 切片 2：WorldVizDataSource 的离线 fixture 适配器。
// 输入 = 导出器（产品链只读缝）产出的数据对象；shape 校验 fail-closed，
// 不修补、不猜补。live（runtime-http）适配器属切片 4（QV7 缺省标注）。

function validateFixture(fixture) {
  if (!fixture || typeof fixture !== 'object') throw new TypeError('fixture is required');
  if (!fixture.timeline || !Array.isArray(fixture.timeline.Revisions)) {
    throw new TypeError('fixture.timeline.Revisions is required');
  }
  if (!fixture.graphs || typeof fixture.graphs !== 'object') {
    throw new TypeError('fixture.graphs is required');
  }
  for (const revision of fixture.timeline.Revisions) {
    if (!revision || typeof revision.RevisionId !== 'string' || !fixture.graphs[revision.RevisionId]) {
      throw new TypeError('every revision must have a matching graph projection');
    }
  }
  return fixture;
}

function createWorldFixtureDataSource(fixture) {
  const data = validateFixture(fixture);
  return Object.freeze({
    // 只读快照：同一数据源重复加载返回同一深冻结对象，不产生第二次"读取"语义。
    load: async () => data,
    describe: () => data.provenance ?? '离线 fixture（来源未标注）',
  });
}

module.exports = { createWorldFixtureDataSource, validateFixture };
