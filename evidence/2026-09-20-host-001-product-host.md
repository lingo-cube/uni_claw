# HOST-001 — Product Host 最小 composition root 验证记录

日期：2026-09-20 · Level：SCENARIO（含 CONTRACT 子项）

## 交付物

- `src/UniClaw.Host/`：HostRunner（组合根 + 单次 run + 产物落盘 + 退出码）、
  V0Runtime（仿真外部缝：帧源/occurrence 派生/咨询/确定性投递/虚拟时钟）、
  Program（console 入口）
- `tests/UniClaw.Host.Tests/`：端到端 ×2 复现 + 依赖闭包执法
- solution 收录两项目

## 首跑实录（第一手 console 运行）

```text
status   : Completed (terminal-emitted)
outcome  : Completion
delivered: 1 [DeliveryCompleted]
EXIT=0
artifacts: ./runs/<runid>/{exec.journal, trace.json, facts.json}
journal  : prepare 记录含 "admissible:True:checks:10:freshness:Sufficient"
           （CORE-013 执行源 + FRS-008 产品新鲜度在产品路径首次联动）
```

装配期发现与修正（全部留痕）：
1. criteria 派生 obligation 是**刻意占位**（Subject 空、不可判定）→ 显式
   Obligations（switch.state=on）+ post-action 帧 state claim → Completion；
2. occurrence 是 revision-local（逐条证据重派生）→ 帧批序固定：内容帧
   必须批尾（否则 post-action 唯一目标验证无对象）；
3. UIW-005 D4：内容 claim 不参与容器判别（否则铸新容器撞死单根约束）。

## Verification

```yaml
end_to_end:
  level: SCENARIO
  method: HostRunner.RunOnce 进程内两跑 + console 第一手运行
  expected: Completed/Completion、单投递 DeliveryCompleted、journal
            pre-dispatch 记录、trace/facts 落盘、两跑 digest 一致（Acceptance #7）
  actual: 4/4 通过（端到端、复现 digest、闭包 ×2）；console EXIT=0
  evidence: tests/UniClaw.Host.Tests/HostTests.cs + 本文件首跑实录
closure:
  level: CONTRACT
  method: GetReferencedAssemblies 白名单 + 禁词类型扫描
  expected: Host 的 UniClaw.* 引用 == {UniClaw.Kernel}；无
            ScenarioStimulus/Oracle/Importer/Replay 类型
  actual: GREEN ×2（RFS-001 closure 债已还）
  evidence: HostClosureTests
regression:
  level: DETERMINISTIC
  method: dotnet test 全量
  expected: 零回归
  actual: 598/598（Core 14 + Agent 17 + FSRealization 9 + Host 4 + Simulation 132 + Kernel 422）
  evidence: 本地全量运行 2026-09-20
```

## 组合面（全部经抽象缝——「利用抽象能力构建完整流程」）

真核心：ProductAssociationStrategy（UIW-005）· ProductFreshnessEvaluator
（FRS-008）· EffectBoundary + FileExecutionJournal（CORE-013）·
KernelRunDriver 自驱（RUN-003 公开缝）。仿真外部：FrameFeed ·
FrameOccurrenceStrategy · V0 Consult · DeterministicDeliveryDriver ·
VirtualClock（换入路径均已登记）。
