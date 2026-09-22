# PER-009 立项 grill docket —— 多源观察与信任（grill-with-docs，2026-09-22）

> 三轮（R1 四问 / R2 四问 / R3 四问），全程同会话，wait ≈0。
> 技能：grilling + domain-modeling（grill-with-docs 路由）。
> 结论：前沿清空，用户确认；PER-009 立项（本 docket + state.md 落档）。

## 已核事实（不占裁决，引文在案）

- 基线 §24.3：post-action 观察可为 cheap/scoped/**native signal**——
  XML dump 做事后验证法理已开。
- `WorldModel.cs` L233 + 单测 A3/A4：跨源冲突显式化已实现。
- **视觉不写也永不写 `*.state`**（LiveVisionStrategy DIRECT v1 合同仅
  ui.detect.* / ui.text.* / spatial.*）；今天写 switch.state 的是 Host
  feed（host.live 等），值来自 adb 读系统设置。
- **跨源碰头至今发生次数 = 0**（共享 key 全部单 producer 在写）。
- 发现：route-1 的"读系统设置"源可转正为 `platform.settings`
  （系统设置类状态天然 A 级）——记为候选，另议。
- Android 无障碍规范速览已给（importantForAccessibility /
  contentDescription / SurfaceView·WebView 壳 / Compose·Flutter 语义树）；
  正式调研 = change 内任务。
- iOS：当前域 Android-only，不需要；共享 key 平台中立不焊死。

## 裁决记录（D 编号与 state.md 一致）

```text
R1-Q1  全页面通吃，视觉+XML 永远并行（缺席即数据）        → D1
R1-Q2  维度逐步加；配置文件管理好；修订人/自动双通道         → D5
       （附注：自动通道留痕+可回滚）
R1-Q3  共享 key 放领域协议层；最终形态=常量类+半页语义纸     → D7
R1-Q4  dump 失败区分"服务未启用 vs 瞬时失败"；预算挂视觉延迟 → D8
R2-Q1  底牌表加 CSS 级联覆盖维度（app/系统特例）            → D5
R2-Q2  Tier 2 触发 = f(置信等级, 冲突程度)，非"吵了就问"      → D6
R2-Q3  词汇不上守护脚本（用户质疑成立，退一步）             → D7
R2-Q4  60s 降级窗口 + 重试上限                               → D8
R3-Q1  一个 DECISION-HEAVY 竖切 change；Tier 2 独立后置      → D10
R3-Q2  Tier 2 触发公式三点确认                              → D6
R3-Q3  常量类（含存量迁移）+ 半页纸 + 值格式测试随 XML 验收    → D7
R3-Q4  60s × 每 Run 最多 3 次探测                            → D8
```

用户两项关键修正（推翻我的初版）：标准控件终审权 = XML 而非深模型；
底牌表加级联覆盖维度。

## 明确不做（防六个月后再提议）

元素级统一命名（假合并）· 数值置信度 · 无买家先建 coordinator ·
ObservationNeed 协议边（等 F9）· iOS · platform.settings 转正（候选另议）。
