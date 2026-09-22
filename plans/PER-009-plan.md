# PER-009 实现计划（Direct 路线，2026-09-22 开工）

> 状态：执行中；完成一项勾一项并跑对应测试。
> ENVIRONMENT 暂标（用户裁决 2026-09-22：本机无模拟器）：live/真机项
> 标 PENDING-ENV，不阻塞纯逻辑切片；有模拟器环境复跑即验收。
> 注：scope/lineage 字符串为 provenance 展示元数据，不参与比较，
> 仅 claim subject 迁移到常量类（防手滑的目标面）。

## 切片（依赖序）

- [x] S1 SharedSubjects 常量类 + 存量迁移（switch.state ×5 / screen.frame ×5 / HostRunner scope 集与 obligation）
- [x] S2 UiAutomatorDump 解析器（纯函数）+ fixture 测试（8/8 绿）；adb 拉取 live 测试 PENDING-ENV
- [x] S3 ConflictResolver（冻结规则：字段表 / 三道门 / 两类冲突 / confidence 盲 / 不升档；9 例测试绿）
- [x] S4 producer-trust 表 + CSS 级联查找 + A/B/C 门槛（6 例测试绿）
- [x] S5 ControlBeliefView（internal）+ 聚焦复查策略（Observe+TargetSubject 复用既有意图面，零 ControlIntentKind 变更）+ WorldModel.DeriveControlBeliefView（6 例测试绿）；KernelRunDriver 推送接线随 S6
- [ ] S6 观察请求 depth/subjects 透传 + Focused 裁剪重扫 + driver 接线
- [ ] S7 事后验证路由四门（dispatch 后重解析防同名错配 + 时序约束）
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
