# RUN-006 — 全遍历真机轮报告（Leader 复核版）

> Worker：glm-5.3-flash（≤3 轮纪律，实用 2 轮 + 1 次就地修正）；Leader
> 复核了全部关键声明（以复核数为准）。证据：evidence/run-006/。
> 咨询链降级申报：真实 DSH 3081 桥因沙箱无法自起（~/.dsh profile 写入
> 被拒，EPERM 实录）→ 文档化 directive-follower double（如实标注）。

## 四元组

```yaml
verification:
  level: ENVIRONMENT
  method: 注册模拟器 p26_pixel（克隆去锁，注册启动器 ANDROID_AVD_HOME 覆盖）
    一轮全遍历（真实产品配置全 AGT-019 机制开启；96 步预算；97 咨询周期）
  expected: 终局诚实；slow 触发链真机开火；AGT-019 字段实机在场；
    零 confirm 点击；标定数据
  actual: BoundedStop(budget-exceeded:steps)，覆盖 37.5%（96 步全
    dispatched+verified）；slow 6×Dispatched(真机触发实证)+6×Rejected
    (3081 桥缺席)+40×Skipped(预算尽)，0 Landed；progress 双停滞字段
    97 载荷全在场（值 0，判据未达）；confirm 侧点击 0（45 次弹窗全走
    CANCEL 安全词）；localView 桥载荷 null（见发现③）
  evidence: evidence/run-006/full-traversal-1/run-20261015-165826-729/
    （settings-trace / consult-bridge / coverage-report / coverage-steps
    / 194 组 capture）+ manifest.json + tools/directive-follower.py
```

## 核心结论（回答"慢感知被真实使用吗"）

**触发链真实开火，落地仍被环境挡住**：46 个 popup 周期里触发器 6 次
真实 Dispatched（SemanticUnclear：popup present + identity-only route，
ms= 令牌在场）；全部 Rejected 于 `slow-transport-failed:Connection
refused (127.0.0.1:3081)` —— 真实模型桥缺席，非机制失效。待桥可用
（沙箱外/leader 手起 3081）重跑即闭环。

## 发现（Leader 复核新增）

1. **复核数字差**：slow 令牌实测 6 Dispatched / 6 Rejected / 40 Skipped
   （worker 报 6/5/46；以 trace 复核为准）。
2. **M5 判据在弹窗循环下不可达**（本轮最重要负结果）：每步 CANCEL tap
   都"验证通过"→ stepsVerified 持续 +1 → 双零窗口永不成立，3/5 未被
   标定到。96 步全 verified 与 37.5% 覆盖并存。
3. **localView 桥载荷 null**：Director 装饰层每咨询写入（离线测试证），
   但 DSH consult-bridge 的 context DTO 未携带——桥侧 DTO 映射缺新字段，
   需一小片修（归 AGT-019 通道）。
4. **popupConsecutiveCycles 疑似未消费**：观测 46 个 present 周期未触发
   其语义（待核 SlowSettings.PopupConsecutiveCycles 在 coverage-director
   模式的接线）。

## 优化建议（Worker 5 条，Leader 保留并排序）

1. **M5 判据加轴**（发现②的直接对策）：route+viewportDigest 指纹的
   零进展计数——否则弹窗/循环场景永远进不了注入门。
2. **M4 弹窗指纹上限**：同一指纹 N 次未清除 → 升级暂停+重授权（不变量
   47 通道现成）；顺带核 popupConsecutiveCycles 接线（发现④）。
3. **slow 预算耗尽可观测性**：首个 budget-exhausted 发诊断事件，避免
   78 周期静默空转。
4. **桥 DTO 补 LocalView 映射**（发现③，小片）。
5. **环境可运行性**：dsh/test-service.sh 支持 profile 位置隔离
   （DSH_TEST_PROFILE_HOME），受限沙箱 Worker 可自起真实链；
   Host.Dsh 相对路径按仓库根解析（本轮实证陷阱）。

## 遗留

- 真实 DSH 链重跑（桥可用时）——同时收 slow Landed 实证与验收 2。
- M5 阈值标定：本轮仅得"不可达"负结果，加轴后重标。
