# WORLD-VISUALIZATION-001 实施计划（垂直切片）

基线：`changes/WORLD-VISUALIZATION-001/spec.md` + `frontend-component-decisions.md`
（D1 两层可组装 / D2 G6 5.x vendor / D3 自由布局非树 / D4 手写时间轴与画布 /
Grill QV1–QV8）。每片独立可验证，RED→GREEN，四元组留证于
`evidence/world-viz-001/`；缺省能力一律按 QV7 缺省标注呈现。

## 切片 0：生产只读缝（先审后写；QV2/QV3）

- 接口稿两份：① 图投影历史化 `ProjectWorldGraph(scope, sourceRevisionId?)`
  并由 World 携带稳定语义标签（join WorldSemanticKnowledge；无语义降级
  `Container@短id` + 语义未知徽章）；② revision 时间线投影（id/parent/number/
  触发原因类型/时间，来源 revision 链）。
- 流程：接口稿独立审阅（fresh 只读 Worker，只审形状与不变量：只读、无执行
  授权、历史不可变、Unknown 不折叠）→ 批准后实现 + Kernel 单测（历史投影、
  标签降级、未知 revision 拒绝、current 语义不变）。
- 验收：CONTRACT/DETERMINISTIC 四元组；审阅记录留 evidence。

## 切片 1：fixture 导出工具（QV1）

- 一次性导出器（产品链离线重放 PopupReplay/ComponentReplay composition）：
  经切片 0 缝导出每 revision 的图投影 + 时间线 + 页面视图快照 JSON 到
  web fixture 目录；只提交导出投影，不提交原始 XML；文件头记来源与再生命令。
- 验收：同输入同输出（hash 稳定）；来源链真实（正式 Kernel 链路，非手造）。

## 切片 2：WorldViz 组件骨架 + DSH 入口（D1/QV8/QV7）

- `createWorldViz` 挂载契约 + `WorldVizDataSource` 适配器接口 +
  standalone-fixture 适配器 + DSH 侧边栏"World 可视化"入口面板（选数据源）。
- 内层四子组件（图/时间轴/画布/详情）先落占位与**四态渲染约定**（QV7 先于
  内容）；样式与抽屉/焦点/Escape 惯例沿 workbench 标准。
- 验收：`node --test`（契约/渲染单测）+ 浏览器真实交互截图（fixture 可加载、
  缺省入口可见）。

## 切片 3：世界图（G6 vendor + 图组件；D2/D3）

- vendor G6 5.x（esbuild 产自包含 UMD→CJS，版本锁定 + 许可 + 再生成命令入
  文件头）→ 图组件：三边类型图例筛选、当前页高亮、缩放/平移/聚焦/返回全图、
  徽章（当前/未知/缺口）、环布局（force/antv-dagre 混合）、G6 薄缝可替换。
- 验收：交互验证 + 截图；薄缝替换性单测。

## 切片 4：时间轴 + 版本选择/对比（QV4）

- revision 链 scrubber（手写）+ 访问/观察叠加 lens（两轴正交，不做包含分组）；
  对比模式：页面级 diff + 图级结构变化列表，点变化定位对象/Slice/来源。
- 验收：时间旅行全联动（图/页面/缺口）；对比定位可复现。

## 切片 5：页面/Slice 画布 + 详情/来源面板（QV5）

- 手写画布：region 边界、累计内容（WorldAccumulatedContent）、空洞=未知
  （非空白）；occurrence 收起/展开；证据按需加载（artifact 截断模式）；
  四层导航（单击选中/双击进入/面包屑返回）。

## 切片 6：验收映射 + 全量 Verify + 收口

- 10 个验收例子逐个映射录制/仿真链（Leader 核实输入可用性），界面标识
  真实/录制/仿真；spec 验收表逐项四元组 + 浏览器视觉验证 + 窄窗口可读性。
- Review（spec/standards 双轴）→ Verify → CLOSED → 单独提交推送并核实远端。

## 顺序与边界

- 0 与 1 可并行准备（1 依赖 0 的缝形状冻结后出真实导出）；2 依赖 0/1；3/4/5
  在 2 骨架上按单写者约束串行推进；6 收口。
- 产品 `src/` 写入仅切片 0（先审后写）；其余落在 `web/`、`dsh/`、fixture 资产
  与 evidence。二期项（live 适配器、session 内 World tab、缺口/影响链 diff）
  全部按 QV7 缺省标注，不静默缺席。
