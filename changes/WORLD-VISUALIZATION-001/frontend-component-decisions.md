# 前端组件方向裁决（2026-10-23，用户确认）

地位：WORLD-VISUALIZATION-001 的 RESOLVE 输出记录。用户在本轮讨论中确认以下方向；
本文件只固化已裁决项与既定事实，不构成实施完成或新 Owner 决定。

## 输入与核对事实

- 用户要求：前端组件必须能被后续整体系统组装使用（不是一次性页面）；图组件选
  G6（用户原话"选v6"；npm 无 v6，@antv/g6 dist-tags latest = 5.1.1，本仓库按
  5.x 主线执行，实施时锁定具体版本并记录来源与 MIT 许可）；其余按 Leader 推荐。
- workbench 标准已核对：双入口架构——`web/uniclaw-workspace/` 为 canonical 前端
  源码（vanilla JS、零 npm 运行时依赖），`dsh/uniclaw-task-workbench/` 为 DSH
  插件（host：只读 Typert 面板方法；client：经 `__ModuleLoader__` 注入的浏览器
  bundle）；能力契约 `uniclaw.workspace.contract.v1`；已有 runtime-http 与
  standalone-fixture 两个数据适配器先例。
- 打包约束已核对：`build-browser-bundle.mjs` 只收集相对路径 require，外部包依赖
  DSH 宿主 externalRequire（仅 react 与 dsh-client 包）；因此 G6 必须 vendor。

## 裁决

### D1 组件化总则（用户要求，对所有组件生效；2026-10-23 补两层结构）

- 组装是**两层**的：
  - **内层**——world 图、时间轴、页面/Slice 画布、详情/来源面板各自独立组件，
    输入为规范化 view-model（纯数据入、事件出）；
  - **外层**——**整个 World 可视化本身也是一个可复用组件**（用户明确）：单一
    挂载契约（mount(容器, 适配器, 选项) → 实例/销毁），取数只经一个数据适配器
    缝，对宿主只暴露展示性事件；后续整体系统可把它作为一个单元直接装配。
- 组件不直接耦合宿主或原始 C# 投影形状；宿主适配（DSH 面板 / standalone
  fixture / runtime-http）留在边缘，沿用双入口与既有能力封套。
- G6 封装隔离在薄缝后面（接口可替换），组件不感知图引擎细节。

### D2 图引擎：AntV G6 5.x

- 许可 MIT；npm latest 5.1.1；中文文档一手；深色主题、内置交互（缩放/平移/
  聚焦/框选）、combo 分组、多布局（force/antv-dagre 等）满足需求。
- 集成方式：一次性用 esbuild/rollup 产出自包含 UMD → 包装为 CJS 模块，提交至
  `web/uniclaw-workspace/src/vendor/`；文件头记录版本、来源 URL、许可与再生成
  命令；现有打包器不改或最小改动。
- 体积代价（~1MB 级 dist 提交进仓库）用户已知情并接受。

### D3 图形态：自由布局，非树

- 导航图允许环：force / antv-dagre 自由布局 + 当前页高亮聚焦，不限定树/DAG。
- Contains 父子关系按约束布局子图；三种边类型（Contains/Overlays/NavigatesTo）
  图例筛选；节点徽章表达当前页/未知/缺口状态（不只靠颜色）。

### D4 手写组件（与图引擎解耦）

- 时间轴（World revision 链 scrubber，访问/观察标记为叠加 lens，两轴正交不
  做包含分组）与页面/Slice 画布（空间矩形、region 边界、空洞=未知非空白）用
  现有 vanilla 渲染函数风格手写。

### D5 遗留设计点（PLAN 内解决，不需要新用户裁决）

- 节点显示名 join（ContainerBelief ↔ WorldSemanticKnowledge）与无语义时的
  降级显示；历史图投影只读缝（`ProjectWorldGraph` 仅 current，需新增生产只读
  缝并先独立审阅）；world 数据适配器（仿 standalone-fixture/runtime-http）；
  版本 diff 粒度；验收例子到录制/仿真链路映射（real/recorded/simulated 标识）。

## 边界

- 组件状态只驻留 UI 生命周期，不持久复制 Host/Runtime 数据；全部只读，不新增
  执行命令或权限；不把界面呈现当产品 World 真值（不变量沿 spec.md）。

## Grill 裁决（2026-10-23，grill-with-docs，两轮前沿清空；QV1–QV8 全部 Accepted A）

用户两轮均"按推荐"，另主动追加缺省标注要求（→QV7）。D5 遗留设计点由本节解决。

- **QV1 fixture 来源**：一次性导出工具用产品代码（Kernel WorldModel）离线重放
  既有录制（PopupReplay/ComponentReplay composition），导出每 revision 只读快照
  JSON 作为 fixture 资产提交；不手工造演示数据；live（runtime-http）第二步接。
- **QV2 只读缝**：新增图投影历史参数 + revision 时间线投影两个生产只读缝，作为
  切片 0 先出接口稿过独立审阅，批准后才动 src；审阅只针对缝形状。
- **QV3 节点标签**：新图投影缝由 World 直接携带稳定语义标签（Owner 侧 join
  WorldSemanticKnowledge）；无语义 → `Container@短id` + 语义未知徽章；降级路径
  随接口稿一起审。
- **QV4 对比粒度**：先页面级 diff（两 WorldPageView 元素/语义参考增删改）+ 图级
  结构变化列表（Container/边新增、撤销、归属切换）；缺口/沿用/影响链 diff 二期。
- **QV5 导航形态**：图单击=选中高亮（详情面板摘要）、双击=进入页面视图（面包屑
  返回）；页面内 occurrence 默认收起；证据按需加载（沿用 artifact() 截断读取）。
- **QV6 验收数据链**：10 个验收例子全部用录制/受控仿真覆盖，界面分别标识
  真实/录制/仿真；live 不作本期验收前提（生产 profile 未验证为既定事实）。
- **QV7 缺省标注（用户新增要求）**：四态严格区分——未知（World 认识论）/无数据
  /加载失败/未实现（功能缺省）；缺省功能显示可见的不可用入口占位（含归属切片
  说明与变更记录引用），不静默隐藏、不冒充其他三态；样式复用 workbench 不可用
  卡片惯例。术语"缺省标注（Deferred Marker）"已入 CONTEXT.md Task Workbench 节。
- **QV8 DSH 入口**：独立顶层面板入口（侧边栏"World 可视化"，面板内选数据源：
  离线 fixture 列表 / 运行中 session）；workbench 会话内 World tab 与 live
  适配器同作二期，届时按 QV7 标注。

工程自有项（Leader 负责核实，不构成用户裁决）：导出工具落点、G6 版本锁定与
vendor 再生成命令、fixture 体积控制（只提交导出投影，不提交原始 XML）、验收
例子到具体录制/场景的逐个映射。
