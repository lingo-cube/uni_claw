# RUN-006 — 全遍历真机轮（AGT-019 机制 live 验证）

lifecycle_state: implementing · review_state: none · disposition: none · depth: standard · base: 4df64a7c

## Intent（WHAT/WHY）

AGT-019（M1/M3/M4/M5 + Slice B 全量）已离线全绿但**从未真机运行**——
"慢感知被真实使用吗"（owner 2026-10-17）的答案是 0 次 Landed（002 实录）
+ 修复后零真机证据。本 change = 注册模拟器上一轮**全遍历**（settings
coverage，真实产品配置），验证 AGT-019 机制在真实链路生效并收集：
① 验收 1-4 实机证据；② 标定切片数据（M5 阈值 3/5、滚动上限 2 的
真实分布）；③ 优化建议。

## Scope

- 注册模拟器 `p26_pixel`（docs/agents/test-emulator.md；不得另起设备）。
- 全遍历：真实产品配置（product/tasks/settings-coverage.yaml，slow
  enabled / popup policy / stagnation bounds 全开）+ 真实 DSH 咨询链
  （consult-bridge 留痕，先例 = SETTINGS-COVERAGE-003/004）。
- Worker 执行（glm-5.3-flash），≤3 轮尝试；阻塞回 Leader。
- 证据落 evidence/run-006/；报告 + 优化建议落 changes/RUN-006/report.md。

## Out of Scope

机制代码修改（发现问题 → 记录 + 优化建议，回 AGT-019 通道修）；标定
数字冻结（数据呈 owner）；多设备。

## Acceptance

1. 一轮全遍历跑完（完成或有界停止，终局诚实）；
2. trace 含 slow 令牌（Dispatched/Landed/Dropped 带 ms=）——无论触发
   与否均如实记录并归因；
3. consult-bridge 含 LocalView/Progress 字段实机在场证据；
4. M4/M1 路径零 confirm 侧点击、异常升级记录（如有异常时刻）；
5. report.md：四元组 + 标定数据 + ≥3 条优化建议。

## Status log

- 2026-10-17 · closed · **终局轮（第 4-6 轮）：慢感知真实使用闭环**。
  修复链：slow 错误解析归一（对象/字符串双形状，DshOpenedHttpPeer）→
  真凶 uniclaw-slow preset 缺失（镜像 web patch preset 行进 product
  profile）→ 弹窗遮蔽轴（MaterialInsufficiency +popupOccluded，谓词
  6/6 测试）。终局证据：**6× Landed|projected=1|late|ms=850-1215**
  （同周期等待落地投影，真机真模型真实 DSH 链）——P2/P6 闭合，
  验收 2 达成。环境已清理。
- 2026-10-17 · closed-ready · **追加轮：真实 DSH 链跑通（owner 授权
  full-access，3 次尝试）**：插件部署 → B6 漏钉修复（prompt manifest
  钉扎链整环，c97069af）→ deepseek-flash 真实咨询链全遍历（97 周期
  96 步，38%，导航真实发生）。slow 六段全通到传输层，最后一里 =
  插件 slow 文本捕获空（事件形状适配，有界后续项）。发现②③真实链
  复现。环境已清理（模拟器 + 3081 停）。
- 2026-10-17 · closed-ready · **全遍历完成（Worker 2 轮 + Leader 复核）**：
  BoundedStop 诚实终局（37.5%，96 步全 verified）；slow 触发链真机实证
  （6×Dispatched，桥缺席 0 Landed——环境阻塞非机制失效）；progress 双
  停滞字段 97 载荷实机在场；零 confirm（45 弹窗全 CANCEL）。关键负结果：
  M5 双零判据在弹窗循环不可达（标定未达）；新发现：桥 DTO 缺 LocalView
  映射、popupConsecutiveCycles 疑未消费。报告 = report.md（四元组 +
  5 条优化建议）。咨询链降级（directive-follower）如实申报。
- 2026-10-17 · created · owner 指令（"RUN 开头 + 跑全遍历 + Worker
  执行 ≤3 轮 + 报告建议 + 提交推送"）；baseline = AGT-019 全量离线绿。
