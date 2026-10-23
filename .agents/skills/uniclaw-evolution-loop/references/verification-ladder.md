# Verification Ladder —— PROVE 阶段的按需验证阶梯（v0）

> 目的：候选修复的证明**按影响面选层、按需升级**，不盲目全量、不重复
> 启动模拟器 —— 循环转速优先。本文件只定义选择与升级规则；各层的运行
> 事实（命令、开关、成本）以既有基准为准，不在此复制。

## 1. 六层阶梯

| Layer | 内容 | 设备/模型 | 典型成本 | 证明力 |
|---|---|---|---|---|
| L0 契约 | 编译 + schema/工具校验 | 无 | 秒 | 形状对 |
| L1 定向单测 | case RED→GREEN + 本文件/组件测试 | 无 | 秒-分钟 | 该 case 修复成立 |
| L2 组件回放/仿真 | ComponentReplay / ScenarioRunner（确定性 double） | 无 | 秒-分钟 | 组件契约不回归 |
| L3 Host 整装回放 | 真实 Runner + 录制输入（如 RecordedPopupRuntimeReplay） | 无 | 分钟 | 真实 Host 链路行为 |
| L4 场景库全量回归 | 41+ 场景 + 八工程 full（默认跳 ENVIRONMENT） | 无 | 分钟-小时 | 无意外回归 |
| L5 ENVIRONMENT live | 模拟器 + 视觉服务 + 真实模型（`DSH_TEST_ADB_ENV=1`） | 是 | boot ~30s + 视觉预热 ~63.5s + 每轮 live | 真实物理映射 |

## 2. 起始层选择（按候选 diff 影响面）

| 候选改动面 | 起始层 |
|---|---|
| 纯函数 / 分类器 / 投影（无状态、无 IO） | L1（+L3 若有整装回放载体） |
| World / Evidence / Control / Runtime 状态机 | L2 → L4 |
| Host 装配 / Director / 策略 | L3 → L4 |
| 感知缝 / 驱动 / 设备交互 / live 专属路径 | L5（唯一能证明的层） |
| 仅文档 / skill / 流程 | L0 |

规则：**从影响面所需的最低层开始**；packet 的 `green` 段必须如实记录
`ladder: { reached, limitedTo, reason }` —— 停在低层不是缺陷，是声明。

## 3. 升级与停止

- 低层绿 ≠ 自动升级：只有当循环**需要**更高层证据（gate 要求 live 证据、
  或低层无法覆盖候选影响面）才升一层。
- L4 不作为每轮默认：它属于 REVIEW/VERIFY 阶段或 gate 要求，不属于单轮
  PROVE 的默认成本。
- L5 失败回退：先按 `uniclaw-debug-evidence` 定位（环境类 F 不立案为候选
  缺陷），不盲目重跑。
- 任何层的结果与既有红冲突时，先做**在途变更对照**（EVO-001 教训），
  再归因。

## 4. L5 会话复用（模拟器不重复启动）

- 生命周期基准不变：只用 `tools/android/start-test-device.sh` /
  `stop-test-device.sh`（supervisor 托管、READY 全过、`.state` 记录 lease）；
  禁止 ad-hoc 手起。见 `docs/agents/test-emulator.md`。
- **循环 lease 约定**：同一演进循环的连续多轮 PROVE 若都需要 L5，共享
  同一次 boot + 视觉预热（启动一次、idle 超时或循环结束时显式回收）；
  预热与正式流程分段计时照旧（不混记）。
- 跨循环不默认保留：循环 CLOSED 或 gate 停止后回收，除非下一个 case 的
  manifest 已声明 L5 需求（避免为"可能要用"占着设备）。
- 快照锁 / 专线 schema-hash 等故障处置照 `test-emulator.md` 既有规则，
  本文件不重复。

## 5. packet 对接

```json
"green": {
  "ladder": { "reached": "L3", "limitedTo": "L1-L3", "reason": "纯分类器候选；无设备需求" }
}
```

`limitedTo` 与 `residualRisk` 呼应：停在低层时，"更高层未证明"必须出现在
residualRisk 里，由 gate 决定是否要求升级。
