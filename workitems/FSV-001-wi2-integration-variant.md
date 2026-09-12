# FSV-001 WI-2 — FastScreen provider + adapter + Integration 变体（transient WorkItem 载荷）

- status: dispatched → subagent c47f2d27-9c30-4b82-8855-8bef89d20487
- objective: uniclaw_perception/screenparse/（provider+adapter，55→canonical
  映射）+ pipeline.py screenparse stage（recognize 后 fuse 前，默认零行为）
  + 变体 fastscreen-integration + server 接线（rescue 合并池/screenParse[]/
  Server-Timing）+ pytest（映射完备/确定性/rescue/默认字节等价回归）。
- acceptance: 默认管道三资产字节等价；pytest 全绿；变体路径冒烟通过；
  映射表回报告供 Leader review。
- result: 完成（2026-09-12）。screenparse 模块（provider/adapter 纯函数）+
  STAGES/ScreenParseParams/变体 fastscreen-integration + server 接线 +
  identity 纳管 + pytest 44+9 绿。默认路径三资产感知面逐字节一致（唯
  pipelineRevision/deploymentId=源码哈希按设计变化；configId 严格一致）。
  变体实测：rescuedCount 2–4/资产；corroboration 空（fs conf≥0.80 稀少，
  OOD 数据事实）；screenparse CPU ≈460–540ms。偏离 6 项经 Leader review
  全部接受（bounds 补齐/纯函数化/screenParse 预处理坐标空间/5 元计时/
  回归锚语义/变体 configId 独立）。
