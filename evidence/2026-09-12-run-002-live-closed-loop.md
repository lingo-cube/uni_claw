# RUN-002 Evidence — Live 闭环 Tracer Bullet

> Date: 2026-09-12 · level: SCENARIO（ENVIRONMENT 主链 + trace 断言）
> 环境: p26_pixel / emulator-5554 · platforms/perception 真实服务（UDS）·
> 门控 DSH_TEST_PERCEPTION_LIVE=1（默认跳过零影响全量）。

## 闭环实录（1/1 GREEN，7s）

```text
seed            admission=Accepted → rev-1（root container 铸造）
frame1（live）  截屏→服务推理→54 proposals→join→Process → rev-2，22 occurrences
control         SelectIntent → Act（DescriptorTargetPolicy 命中 switch）
grounding       ActViaCurrentGrounding(TargetDescriptor("switch")) → UniqueCandidate
dispatch        AdbLiveEffectDriver 真实 tap → DeliveryCompleted @ switch   ← A1
frame2（live）  再观察 → rev-3，景观 digest 509→446（变化）                  ← A2
terminal        EvaluateTerminal → 无 proof / evidence-insufficient（如实）  ← A4
trace           FinalizeArtifact：span 全 Complete，感知 span 引用 derived
                artifact id（reference-oriented 因果可核验）                 ← A3
metrics         ArtifactsPresented ≥ 2（两帧经 perception）
全量回归        Kernel 352/352 + Agent 17/17（默认门控下子弹零影响）        ← A5
```

## Trace 缺口台账（Human 指令：记录不顺手大改）

| # | 发现 | 性质/建议 |
|---|---|---|
| 1 | **FastPerception 直连（不经组合缝）时零 span**——观察侧没有统一组合入口，trace/metrics 必须各自显式注入，漏注即静默无观察 trace（本 bullet 实证踩中后修复接线） | 缺口。建议后续 change：观察编排组合缝（如 kernel.Observe(capture)）或注入断言面 |
| 2 | **trace correlation 与 RunModel.RunId 两阶段不同步**：kernel ctor 需先于 AdmitContract 存在，而 RunId 在 AdmitContract 后铸造 → BeginRun 只能用占位 correlation | 缺口。建议：trace correlation 支持延迟绑定 RunId |
| 3 | **ownerless occurrence 从 Slice 静默消失**：DeriveSlice 只取 container bucket，owner=null 的 occurrence 不入景观且无任何诊断——live 组合首帧即踩中（policy Observe 而非 Act，无解释） | 体验缺口。建议：DeriveSlice 对 ownerless occurrence 计数诊断（不改语义） |
| 4 | **span 无时长**（Deferred ⑪ 有意：canonical clock 未决）——延迟画像只能靠 RuntimeStageMetrics，trace 侧无法回答"这帧花了多久" | 已知 defer。OPT-001 计时补全时一并考虑 span timing |
| 5 | **RecorderTerminal=Quarantined 当无 outcome emission**——terminal 语义（evidence-insufficient 等 non-proof 终局评估）不产生 emission，trace 封存状态与 run 终局状态可能长期不一致 | 语义观察。建议后续裁决：non-proof 终局是否也标记 recorder |
| 6 | Process 侧诊断面良好（KernelResult 四通道 + Admission.RejectionReason 实证可用：空 transformation-lineage 被正确拒绝） | 正面记录 |

## 交付

- `tests/UniClaw.Kernel.Tests/Perception/LiveClosedLoopBulletTests.cs`（1 用例）
- test-side join 策略（帧级 master proposal，PER-003 D2 先例）+ seed 模式
  （UIW NewKernel(seed:true) 先例——发现#3 的组合解）

## 实现期修正（留痕）

- join 对异常 subject 长度设防；RevisionId 短串截断设防；seed 需非空
  transformation lineage（admission 正确拒绝空 lineage——非缺陷）；
  FastPerception trace 注入顺序（发现#1）。
