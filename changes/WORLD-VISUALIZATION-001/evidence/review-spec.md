# World Visualizer — Spec Review

范围：对照 `changes/WORLD-VISUALIZATION-001/spec.md`、MV01–18、RU01–08、CI01–12 与当前可视化代码及七个打包示例。此审查只判展示覆盖，不把核心测试、导出成功或模型测试通过当作视觉/交互验收。

## 阻塞/待处置发现

1. **P1：版本比较没有比较正式页面/Slice/知识投影。** 规格要求比较前后 World 或局部视图，并能定位变化对应对象、Slice 和来源（`spec.md`“变更比较”）。当前 `app.mjs:69,174-180` 只把两个 `revision` 交给 `changesBetween`；`model.mjs:42-59` 只比较 `containers`、`occurrences`、`relations`、`semanticKnowledge`。当 PageView、Slice 表达、coverage/semantic references 或 effect review 变化而这四类revision字段不变时，比较面板会显示无差异；点击已有差异也只选 revision 对象，未定位对应 Slice/来源。应把同 W 的正式 PageView/Slice/knowledge/source 引用纳入比较结果，或将缺失差异清楚标为未比较。
2. **P1：历史纠错视角缺旧时点页面与 Slice 证据。** correction 导出保留全部 8 个实际revision，但 rev-1…rev-7 没有 PageView/Slice（`examples/correction.json`；打包器 `package_examples.py:106-122`）。因此可看到“历史 W 数据未导出/未知”和 rev-8 当前解释，不能完成规格要求的“前后图、相关边、Slice和页面表达联动；旧视图保留”或 MV08–11 的跨轮受影响/独立分支比较。打包器正确没有把当前页回填旧 W；需补真实 exact-W 历史 Owner 投影，或将此例明确限制为消费影响局部，不称完整纠错可视化。
3. **P2：未展示的主题数量仍大于当前示例集合。** 下方覆盖账逐条列出；尤其导航/失败重试/多轮动态状态、迟到结果、同 route 跨设备反例、纠错逆转、in-flight delivery、恢复/不可逆操作均无对应可交互帧。不能把 `correction` 的单个已纠正当前帧或 `scroll` 的五个局部快照泛化成这些场景已满足。

## 已确认的边界

`model.mjs:1-32` 只验证、归一化和呈现输入，不推导身份或许可；`serve.py:14-20,26-28` 只提供 loopback 静态 GET。打包器新修正把所有 correction revisions 留作独立帧，只有精确 source-W 有页面/知识/影响/咨询；其他帧为空且明确未知。scroll 优先复用独立 `world-visualization.v1` Owner 导出。录制示例当前页与 graph 均为 rev-2，并把实际 HTTP consultation 按其 `worldKnowledge.sourceRevisionId` 精确配对；它仍是录制 XML、受控时间/设备标签与 DSH HTTP double，不是真机或最终模型证据。

## 验证边界

`python3 -m py_compile tools/world-visualizer/package_examples.py`、打包命令及 `node --test tools/world-visualizer/model.test.mjs`（3项）通过。此处没有启动浏览器、执行真实交互、核对桌面/窄窗截图或 live provider。Spec 的界面验收和整体验收仍待 Leader 独立完成。
