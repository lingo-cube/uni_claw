# PER-009 评审整改记录（CHANGES_REQUIRED → 全项处置）

> 日期：2026-09-22 · 对象：`evidence/2026-09-22-per009-review-report.md` 全部发现
> 方法：Leader 直路修复（用户指令「按需求修和完成」；模拟器自举）

## 修复矩阵（评审编号 → 处置）

| 评审项 | 处置 | 落点 |
|---|---|---|
| **C-1** 裁决器死代码 | 接线：driver **StepAct 与 TerminalEvaluation 双点**先裁决后聚焦；`WorldModel.ResolveConflict`（owner 销案：清冲突条目 + 值生效 Revise 痕迹链 + `ConflictResolutionLog` 留档不删）；`UniKernel.ResolveAuthorityConflicts`（快照自映射 claim lineage 构造，`SnapshotFromLineage`） | WorldModel / UniKernel / KernelRunDriver |
| **C-2** 路由器零快照 | 接线：`VerifyPostActionEffect(target, processed, dispatchTime)`——post 相 XML 映射 claim 为 establishing producer 时经四门路由裁决（UseXml → 零截图验证）；driver 记 `_lastDispatchAt`（Receipt.DispatchedAt）执法门④ | UniKernel / KernelRunDriver |
| **C-3** 探测永久耗尽 | `ResetForNewRun()`（feed 每 Run 新建天然成立；长生命周期宿主显式调用） | UiAutomatorDump |
| **S-1** 超时失效 + 吞异常 | 双修：ReadToEndAsync→WaitForExit(timeout)→Kill 超时路径；`catch when (not InvalidOperationException)` fail-closed 上抛；**传输修正**（PENDING-ENV 实测发现：API 35 `dump /dev/tty` 不回显 XML → 定点文件+cat+rm 单次原子命令——此前 XML 通道在真机全程降级） | UiAutomatorDump |
| **P-3** 跨源不碰头 | `MapTargetStateClaim`：XML 节点↔目标 bounds 空间映射（归一化 IoU≥0.5 ∧ 领先次名≥0.25 ∧ checkable guard）→ `{role}.state` 共享 claim（lineage 携带 xml-map/checkable/unique 快照） | UiAutomatorDump / LiveFrameFeed |
| **P-1** 信任表未落盘 | `src/UniClaw.Kernel/World/producer-trust.json`（EmbeddedResource）+ `LoadFrozenTable()` fail-closed | ProducerTrust |
| **P-2** 门槛零调用 | **显式递延**：授权门槛全接线需 effect 不可逆性分类（Grant/Phase 6 域，无模型）；不伪造调用点 | 记录于 state.md |
| **P-6** Focused 不裁剪 | 部分：directive.Subjects 过滤（映射层）；真裁剪重扫待感知管线区域裁剪 API（Tier 1 change） | LiveFrameFeed |
| **Scope** DeepProducer | 移除（含冻结表条目） | ProducerTrust |
| **P-8** 文档偏移 | plan S7/S8 勾选 + state 整改行 | plans / state |

## Verification

```yaml
level: DETERMINISTIC + SCENARIO
method: 整改回归 8 测试 + 全量四套件 + 真机双源全真闭环
expected: C-1 销案且不升档（D13）/ 三剥夺路径（D3/D14/D12）/ 复位 /
        冻结表载入 / 四门路由正反例 / occurrences 携带；全量零回归；
        真机 Completed + adb 独立源翻转
actual: 整改 8/8；全量 655/655（Kernel 460 含并行会话 51 例零回归）；
        真机 HostLiveFull GREEN（11s；新传输通道 XML 实际在场——
        手动 dump 实证 switchWidget checked/checkable/bounds 与视觉
        检测 bounds IoU 高位对齐）
evidence: tests/UniClaw.Kernel.Tests/World/Per009RemediationTests.cs + 本文件
```

## 遗留（登记在案）

- P-2 全接线（等不可逆性分类模型）；P-6 真裁剪重扫（等管线区域裁剪）；
  冲突触发率统计（Tier 2 立项买家数据）待多场景运行积累。
