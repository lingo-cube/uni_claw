# PER-009 实现计划（Direct 路线，2026-09-22 开工）

> 状态：执行中；完成一项勾一项并跑对应测试。
> ENVIRONMENT 暂标（用户裁决 2026-09-22：本机无模拟器）：live/真机项
> 标 PENDING-ENV，不阻塞纯逻辑切片；有模拟器环境复跑即验收。
> 注：scope/lineage 字符串为 provenance 展示元数据，不参与比较，
> 仅 claim subject 迁移到常量类（防手滑的目标面）。

## 切片（依赖序）

- [x] S1 SharedSubjects 常量类 + 存量迁移（switch.state ×5 / screen.frame ×5 / HostRunner scope 集与 obligation）
- [x] S2 UiAutomatorDump 解析器（纯函数）+ fixture 测试（**纠错记录**：
      S2 时报"8/8 绿"是误数——bounds 解析 bug 自始存在（"][ 中缝"），
      S6a 复跑抓出并修复，现为真 8/8）；adb 拉取 live 测试 PENDING-ENV
- [x] S3 ConflictResolver（冻结规则：字段表 / 三道门 / 两类冲突 / confidence 盲 / 不升档；9 例测试绿）
- [x] S4 producer-trust 表 + CSS 级联查找 + A/B/C 门槛（6 例测试绿）
- [x] S5 ControlBeliefView（internal）+ 聚焦复查策略（Observe+TargetSubject 复用既有意图面，零 ControlIntentKind 变更）+ WorldModel.DeriveControlBeliefView（6 例测试绿）；KernelRunDriver 推送接线随 S6
- [x] S6a 观察指令经 Kernel 驱动面传导（台账 #20 A 方案裁决）：
      ObservationDirective（Context+Depth+Subjects）+ NextInput 缝迁移
      （6 feeds + HostRunner + sim + 测试）；driver 聚焦复查分支
      （Observe∧TargetSubject∧有界 ≤3 次 → Focused 拉取；普通 Observe
      保持原 fail-closed 语义）；白名单授权 +2；全量 439 通过（仅存量 4 env）
- [x] S6b-1 driver 聚焦环路集成测试（全栈版：world 真实 Conflict →
      UniKernel.CurrentConflictedSubjects → policy → 驱动面 Focused，
      单一真相源接线；手工注入式旧测随接线删除，policy 规则由
      AgentPlanPolicyConflictTests 覆盖）
- [x] S6b-2 XML producer 接入 feeds：LiveFrameFeed 双源组合（TryCoObserveXml
      + D8 ProbeStateMachine 60s×≤3 + degraded:no-xml lineage 标记）+
      adb dump 执行器（TryDumpToDevice，结构性 vs 瞬时分类）；
      5 例探测状态机测试绿（live 行为 PENDING-ENV）
- [x] S7 路由器（PostActionXmlRouter：四门纯函数，8/8 绿；含
      confidence 盲结构锁、tri-state、同名错配防线、时序约束）
- [ ] S7-wiring StepVerify 接线（需 XML→Kernel 快照桥：
      身份解析映射的 Kernel 侧落点 = TargetSpec.Role → resource-id
      尾段约定，待真机校准后接入——PENDING-ENV）
- [ ] S8 值域断言（{on,off,partial}）+ 全量回归 + Verification 四元组回填

## PENDING-ENV 清单（Acceptance 映射）

- #2 前半（真机同帧双源 XML+视觉）→ PENDING-ENV
- adb dump 拉取实测 / 60s×3 探测实测 → PENDING-ENV（解析与策略 fixture 全测）
- #8 验证路由的 live 实测 → PENDING-ENV（路由逻辑 doubles 可测）

## 本机环境事实（2026-09-22 记录，非 PER-009 回归）

- UniClaw.Host.Tests 存量 4 例（EndToEnd×2 / Replay×2）在本机稳定失败：
  `exec.journal` 文件被占用（IOException）——**stash 实验**：不含本 change
  改动的干净基线同样失败 → 判定为本机文件锁环境问题（journal 句柄/
  同毫秒 run 目录碰撞），非本 change 引入。有模拟器/干净环境复跑再判。
- 本 change 全部新测试（8 例）绿；迁移点行为等值（常量类仅改拼写来源）。
- Kernel.Tests 存量 4 例 VisionServiceHostTests（进程托管类）在本机失败：
  stash 基线实验同样失败 → 存量环境问题。本 change 后全量 439 通过 /
  仅此 4 例环境失败。
- 本 change 附带修复：docs/analysis/invariant-enforcement-matrix.md 补
  Status/Authority 头（DocsMetadataTests 执法——ARCH-DOC-016 产物自身
  曾违反 ARCH-DOC-015 文档治理，已补）。
