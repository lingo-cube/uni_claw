# PROTOCOL-FIX-001 修复包提案 —— 形式审计裁决的实施切片

> Status: `DRAFT / PROPOSAL`
> Authority: `NONE`（提案稿；裁决依据：`protocol-completeness-formal-audit-v0.1.md` 附录 C grill 记录 + ADR-0048/0049/0050）
> Date: 2026-10-12 · 开工时在 `changes/PROTOCOL-FIX-001/state.md` 立 change

## 0. 原则

每个切片独立可验收（UniFlow 垂直切片）；切片内协议基线同步修订 + 锚定测试 +
负向自证（改一字段即 RED 的 tripwire 先例）。切片顺序按"一个字段闭几个缺陷"
的杠杆排序。

## 1. 切片清单

### S1 · Origin 字段族（杠杆最高：一石三鸟）

- **类型**：`ObservationProposal` / `EvidenceRecord` 增 `Origin`（封闭二值）；
  `EffectReceipt` 回流侧同源携带；`EvidenceId` canonical 输入纳入 origin。
- **判定**：EB/Kernel 侧机械推导——仅当存在显式 attempt correlation 键判
  `PostActionSelf`，否则 `External`；**时间启发式 tripwire 测试**（出现即 RED）。
- **同步**：协议基线 P2/P3 Status 的 origin known gap 闭合注记；ADR-0047 known
  gaps 更新。
- **闭合缺陷**：ADR-0047 gap、D6 判定输入、E5 前提显式化。
- **验收**：①误归外部方向锚定（外部观察即使紧邻动作也不满足 MaterialEffect）；
  ②correlation 键存在时正确判 post-action；③origin 入 EvidenceId。

### S2 · Expected Effect 载荷（D8）

- **类型**：`PostActionEffectVerificationInput` 增期望载荷（`StateBecomes` +
  变化型封闭词汇 `ContentChanged`/`RouteStable`）；滚动三布尔迁移为
  `RouteStable ∧ ContentChanged` 实例并删除特设字段。
- **同步**：CONTEXT「Expected Effect」已落；ADR-0049 已立。
- **验收**：①滚动验证行为等价迁移（前后场景 digest 对照）；②新谓词扩展走协议
  变更（vocab 测试）；③无期望载荷的验证输入 = RED。

### S3 · 预算两层顶 + 续授（D1/D7，体量最大）

- **契约侧**：`ExecutionContract` 预算保持可空；admission 产出
  `valueOrigin ∈ {configured, default}` 留痕；View 沿载来源。
- **policy 侧**：`HardCap`（步数/轮次各一，有限）+ `RenewalThreshold` 参数面。
- **P25 侧**：续授窗口标记（driver 检查阈值）；UniAgent 提案续授量；policy 校验
  `已授+提案 ≤ HardCap`；**终问携带续授提案**（Q10 边界确认）。
- **记账**：driver 消耗态 + decision 留痕 + trace；不进 Run State；恢复语义注明
  "随 ⑰"。
- **验收**：①硬顶有限 ⇒ 必停机（liveness 锚定：无限续授请求全部被拒）；②续授
  经 P25 且留痕完整；③初始额度 valueOrigin 可审计；④契约不可变未被触碰
  （续授前后 View 字节不变）。

### S4 · P10 locator 曝射收窄（A.2 新发现）

- **类型**：`CanonicalBinding` 移除 `TargetLocator/TargetNative/TargetSpace`；
  `CoreSemanticProjection` 改从 `DeliveryTarget` 取（delivery record 从 delivery
  material 投影）；EB lowering 直取 view 原值（行为零变化）。
- **消费面已查清**：全仓两处（`EffectBoundary:261`、`CoreSemanticProjection:160`）。
- **验收**：①Core 投影输出字节等价（迁移前后对照）；②公开面白名单同步；③
  `CanonicalBinding` 无 locator 的形状快照（衔接 lockdown 计划 P2/P3）。

### S5 · 期望渗透立法（D6，最小）

- **规则**：Assurance 明文"prior 影响的 association 连续性不得作为 MaterialEffect
  唯一依据"（现有 TargetSpec state 断言要求的显性化）+ 锚定测试。
- **验收**：仅身份连续、无 state/谓词对照的验证 = 拒绝。

### S6 · 机械项搭车（不占独立切片）

- D9 拒绝指纹（可选）；P25/P1/P18 身份字段补齐**不并入本包**——走 ADR-0046
  既有轨道（避免范围扩散）。

## 2. 顺序与依赖

```text
S1（Origin）──▶ S5（D6 立法依赖 Origin 判定输入）
S2（Expected Effect）─┐
S3（预算/续授）───────┼─▶ 三者互不阻塞，可并行立 change
S4（locator 收窄）───┘（独立）
```

建议节奏：S1+S5 一个 change；S2 一个；S3 一个（最大）；S4 一个。全部完成后
协议基线做一次窄修订（P1/P2/P3/P10/P25 状态与载荷同步 + 附录 A 缺口表销项）。

## 3. 与锁定计划（lockdown-plan）的衔接

S1–S4 交付的字段形状即 lockdown P2/P3 快照的**初始内容**——先修后锁，避免把
缺口锁死（锁定计划 §5 高优先审阅组已被本 grill 预消化）。

## 4. 总验收

| # | 判据 |
|---|---|
| A1 | 附录 A.1 缺字段表：Origin/ExpectedEffect/valueOrigin 三行销项（其余走既有轨道） |
| A2 | 附录 A.2 多字段表：locator 行销项 |
| A3 | 审计 §4 表 D1/D6/D7/D8/E5 标记"闭合路径确立→已闭合" |
| A4 | 协议基线窄修订一次到位；DocsMetadata/白名单/快照测试全绿 |
