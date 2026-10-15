# RUN-006 报告副本（canonical 汇报在 Worker 终局消息；本文件为证据留档）

date: 2026-10-16 · run: evidence/run-006/full-traversal-1/run-20261015-165826-729 · tier: 降级（directive-follower double，理由见 manifest.json consultChain）

## 四元组

- method: 注册模拟器 p26_pixel（emulator-5556）+ 真实产品配置 product/tasks/settings-coverage.yaml 全遍历；入口 UniClaw.Host.Dsh `--settings-coverage`；consult 走 leader-file 决策桥（真实 DSH 3081 桥因会话沙箱无法启动专用实例，见 manifest）。
- expected: 一轮全遍历（完成或有界停止）；slow 令牌如实留痕；AGT-019 字段在场证据；零 confirm 侧点击；标定数据。
- actual: 有界停止（budget-exceeded:steps，96 步），coverage BoundedStop 38%；slow 6 Dispatched + 5 Rejected(transport) + 46 Skipped(budget)；97 次咨询全留痕；0 confirm 侧点击。
- evidence: 本目录 run-20261015-165826-729/（settings-trace.json、consult-bridge.json、coverage-report.json、coverage-steps.json、capability-facts.json、194 组 capture png/xml）+ manifest.json。

## 标定数据切片

- 每咨询 stepsVerified 增量分布：96 次 +1、1 次 0（首轮）——SIM 弹窗循环期间每步都"验证通过"（route 有变化），故 M5 stagnation 3/5 未触发（诚实归因：非机制失效，是触发条件不满足）。
- 滚动使用：0 次滚动指令（未走到 scroll 分支即被 SIM 弹窗耗尽预算）；same-route repeat 计数全程 0；stagnantConsultations 全程 0。
- slow：budget maxRequestsPerRun=6 在第 6 次 Dispatched 后耗尽（19 周期起 Skipped|budget-exhausted 至 97 周期）。

## 优化建议（≥3）

1. 弹窗处置 policy 需覆盖 "SIM name & color" 设置向导类对话框（com.android.settings SIM setup）：其 CANCEL 点击后立即重现（或 directive 在同一 route 反复再入），建议 M4 增加"同一 popup 指纹 N 次未清除 → 升级暂停+重授权"而非继续消耗步骤预算（不变量 47 通道已存在）。
2. M5 stagnation 的 progress 语义应区分"验证通过但 frontier 零增长"：本轮 96 步全 verified 却 38% 覆盖率，建议 stagnation 判据加第二轴（如 sameRouteConsultations 用 route+viewportDigest 指纹计数），否则弹窗循环永远到不了 3/5 注入门。
3. slow 预算耗尽后 40 个周期静默 Skipped：建议在 Skipped|budget-exhausted 首次出现时发出一次诊断事件（sink/trace 字段），并允许 popup 连续未清场景提升预算或显式有界停止，避免 78 个空转周期。
4. real-chain 可运行性：test-service.sh 需支持 DSH_TEST_PROFILE 隔离的 profile 写入位置（或预热脚本在宿主会话完成 profile 初始化），使受限沙箱 Worker 也能起 3081 专线——否则每轮 live 验证都依赖 leader 手起实例。
5. 遍历入口应把 popup 循环上限（popupConsecutiveCycles=2 配置存在但本轮观测 46 次 present 周期）与实际执行对齐：核查该配置在 coverage-director 模式下的执法路径（疑似未被消费）。
