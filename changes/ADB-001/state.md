# ADB-001 — ADB 真机执行层（live driver：进程协议 + 三态真实映射）
lifecycle_state: closed · disposition: none · depth: standard · base: 1cdc4a74

## Intent（WHAT/WHY)
DSE-002 的 AdbEffectDriver 是 dry-run（命令构造，零进程）。当务之急
（Human 方向校准 2026-09-09：ADB 优先，ego-browser 仅验证协议扩展性——
已由 DSE-003 + spike 完成）是把执行层接真：进程孵化、超时、退出码到
三态 outcome 的**真实物理映射**——legacy 三态（Dispatched/TimedOut/
Rejected，`uni-agent:AdbDispatchTarget.cs` 实证）经 HD-1 裁决词汇
（DeliveryCompleted/DeliveryFailed/UnknownOutcome）的最终落码。
这是 Operate 审计 → Human Gate → 实现的全链闭合点。

## Scope
- `Effects/AdbProcess.cs`（新）：internal `IAdbProcessRunner` seam +
  `AdbProcessRunner`（legacy 平移：argument-safe 启动、64MiB stdout
  bound、linked CTS（timeout→Kill entireProcessTree）、三通道结果
  Started/TimedOut/ExitCode/StdOut/StdErr/FailureReason）。
- `Effects/AdbLiveEffectDriver.cs`（新）：live driver——支持集与
  dry-run 一致（{click,tap,set-switch} × device-viewport × spatial-only，
  命令构造/投影与 AdbEffectDriver 共享提取）；Deliver → runner 真实
  执行 → 三态映射：
  ```text
  TimedOut        → UnknownOutcome + reason=timeout-killed
                    （F1 物理事实：kill 的只是 adb client，设备侧效果未知——
                     唯一合法后继 re-observe，never blind redispatch）
  !Started/Exit≠0 → DeliveryFailed + reason=transport + diagnostic(StdErr)
  Exit==0         → DeliveryCompleted + Report=完整命令串
  ```
- 同步阻塞债如实标注：Deliver 同步等待进程（≤10s）——单线程语义环内
  行为正确；异步签名切换 = HD-1 第二阶段 buyer（机械手/pending），不预建。
- 测试：fake runner 三态×reason×diagnostic 全测 + **ENVIRONMENT-lite**
  （假 serial × 真实 adb 二进制：真实进程/错误通道/exit≠0 →
  DeliveryFailed 全链验证，无需设备）。

## Out of Scope（禁止）
- 观察侧真机链（截图→perception→occurrence 真机化）——独立 buyer
  （corpus 观察链已由 PER-002/003 验证，spatial 数据源不变）。
- IEffectDriver 异步签名 / RequestAccepted / pending（HD-1 第二阶段
  defer 不变；同步阻塞是显式记录的债，非隐藏等待）。
- 设备管理/发现/预检（HD-5 四轴 readiness 仍是 rationale；serial 由
  组合根显式传入）。
- 真机点击验收——设备不在场，ENVIRONMENT 项留待设备（见 Verification）。

## Decisions
- dry-run 与 live 分离为两个 driver（禁止布尔模式开关——设计质量
  约束 5）；共享命令构造/投影经 internal 静态提取（AdbTapCommand）。
- 三态映射 = legacy 实证语义 × DSE-001 裁决词汇的合体，映射表进
  AdbLiveEffectDriver doc（audit trail）。
- ENVIRONMENT-lite：无设备下用真实 adb 二进制验证失败路径——进程层
  真实、设备层缺席，诚实分级。

## Acceptance
L1 fake runner：三态各一——timeout-killed→UnknownOutcome（断言 reason
   与「设备侧未知」语义）；exit=1+StdErr→DeliveryFailed（diagnostic
   透传）；exit=0→DeliveryCompleted + Report 命令串精确
L2 fake runner：支持集失败族回归（locator/frame/effect 三 reason 与
   dry-run driver 行为一致——共享构造的证据）
L3 ENVIRONMENT-lite：假 serial 真实进程 → DeliveryFailed，reason 含
   transport 语义、diagnostic 含真实 adb 错误输出
L4 既有全量零回归（dry-run driver 行为不变）

## Constraints
- 新代码仅限 Effects/（AdbProcess/AdbLiveEffectDriver + AdbEffectDriver
  内部提取）+ 新测试；确定性（fake runner / clock 注入）；L3 单独标注
  ENVIRONMENT-lite。

## Verification
```yaml
verification:
  level: DETERMINISTIC（L1/L2/L4）+ ENVIRONMENT-lite（L3）
  method: dotnet test 全解决方案 ×2 + L3 真实 adb 二进制单跑
  expected: L1–L4 全绿；真机点击（设备在场）另立验收
  actual: >
    240/240 GREEN（Kernel 223 + Agent 17；两次独立运行，LatencyBaseline
    并行 in-flight 文件移开-复原期间验证，内容零修改）。L1 fake runner
    三态映射全绿（timeout-killed→UnknownOutcome 含"设备侧效果未知"语义、
    exit=1+StdErr→DeliveryFailed+diagnostic 透传、ExitCode==0→
    DeliveryCompleted+精确命令串 "-s serial shell input tap 540 164"+
    runner 参数断言）；L2 共享 TryBuildTap 支持集失败族与 dry-run 一致
    （UnreachableException 证明零进程执行）；L3 EnvLite 实测通过（真实
    adb 37.0.1 二进制 × 假 serial → 真实进程/错误通道/exit≠0 →
    DeliveryFailed(transport)，诊断含 serial）；L4 既有含
    DeliveryTargetAdbTests 全绿（dry-run 零变化）。变更面 = Effects/
    （AdbProcess.cs 新增——IAdbProcessRunner internal seam +
    InternalsVisibleTo(Tests)；AdbLiveEffectDriver.cs 新增；AdbEffectDriver
    TryBuildTap 提取）+ AdbLiveDriverTests.cs 新增（7 用例）。
  evidence: dotnet test 输出（2026-09-09，两次独立运行）；L3 含真实进程执行
```

## Status log
2026-09-09 · understanding→resolved→planned · 优先级校准（ADB 当务之急）；
  环境侦察：adb 37.0.1 在场、无设备/模拟器 → DETERMINISTIC(fake runner)
  + ENVIRONMENT-lite(假 serial 真进程) 双层验收；to-spec + PLAN 同会话

2026-09-09 · planned→implemented · Direct 实施。AdbProcessRunner（legacy
  平移 + timeout→TimedOut 三通道）/ TryBuildTap 共享提取 / AdbLiveEffectDriver
  （三态映射 doc 化 audit trail）/ ctor 拆分（internal runner 注入缝）
2026-09-09 · implemented→reviewed · REVIEW 偏离 2 条：①internal seam 需
  InternalsVisibleTo(Tests)（仓库首例，加于 AdbProcess.cs assembly 属性）；
  ②一度误移并行会话 RuntimeStageMetrics.cs（其已被 LAT-001 三个文件引用）
  ——即时复原并修正处置边界（只移真正编译阻塞的 LatencyBaselineTests）。
  三态映射与 PLAN 完全一致
2026-09-09 · reviewed→verified→closed · 两次独立 240/240 GREEN（含 L3
  EnvLite 真实进程）+ diff 审阅合规 → ADB_LIVE_EXECUTION_ESTABLISHED。
  legacy 三态实证 → HD-01 裁决词汇 → 真实物理映射的审计全链闭合。
  真机点击验收（设备在场）留待 ENVIRONMENT change；观察侧真机链独立 buyer
