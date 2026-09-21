> Status: DRAFT
> Authority: NONE
> 日期: 2026-09-20 · 方法: 场景 × 接口推导（静态 + 测试引用；未运行 uni-agent）

# 能力接口覆盖推导 v0.1 —— VNext 场景库 × uni-agent 实跑 × 当前接口

## 0. 输入与口径

- 场景库蒸馏：`evidence/2026-09-18-core-minimal-candidate-validation.md` §3、
  `evidence/2026-09-19-core-bidirectional-validation.md` §场景库交叉约束、
  VNext 评审原文（~/.codex/attachments/52556104…/pasted-text.txt）。
- uni-agent 实跑考古：分支内测试/场景目录（十一类场景 + 能力面，
  证据路径见考古报告；本推导不引用未运行代码）。
- 接口基线：§「接口清单」（2026-09-20 敲定盘点）+ 白名单 197 项。

## 1. A–E 类（VNext 场景库）判定摘要

| 类 | ✅ | ◐ |
|---|---|---|
| A 发生语义 | 门开关历史 / 先发生后证据 / 更正不抹历史 | 跨域发生归属（Event 触发器，登记 open） |
| B 局部观察 | 滚动重叠 / 历史 basis 固定 / 重渲染 | — |
| C 执行 | Unknown 不升级 / 二次 dispatch 拒绝 / 迟到回执（结构） | 重试补偿策略（CORE-014 Q3 deferred） |
| D 有效性 | 版本漂移 / 同时编辑 / 标定漂移 | — |
| E 终局责任 | 无关证据 / 无候选 / 权限执法 | 恢复消费编排（= Host 后继）；显式授权谱系 |

## 2. F 类（uni-agent 实跑）判定

| # | 场景 | 判定 | 接口链与缺口 |
|---|---|---|---|
| F1 | 期望态零操作完成 | ✅ | P25 NoAction → 零 Effect 终局（RFS S 系列） |
| F2 | 真机感知校准 + 独立验证 | ✅ | live 闭环 RUN-002 实证；独立验证=第二源观察入 P2 |
| F3 | 感知管线回归 | ✅ | platforms/perception 同仓（PER 系列） |
| F4 | 证据纠正/撤销 | ✅ | CLE-001 + Evidence supersede + CORE-016 S3 |
| F5 | 外部 assistance 咨询 | ✅（缝层） | agent realization 内部自由；Contradicted=claim conflict 面 |
| F6 | Golden Path 多步导航 | ◐轻 | 全链可表达；LaunchApp/SetSwitch/PressBack 等 **effect 词表** = driver realization 扩展，缝不动 |
| F7 | 弹窗恢复 / 漂移再入 / 断点续跑 | ◐ | 世界面 ✅（遮挡=新容器、回归=signature 匹配）；策略面撞 F9；跨进程续跑=恢复编排（Host 后继） |
| F8 | 滚动后同屏身份连续 | ◐ | **UIW-005 精确匹配权衡的首个具体反例**：元素级连续 ✓（LogicalItem），容器级连续需归一化/重叠匹配——同缝迭代 buyer 已到 |
| F9 | 开放世界遍历（36 观测/35 派发） | ◐ | **决策缝形状缺口**：当前「每 Run 单次咨询 + ≤16 步」（P25 TRACER_HYPOTHESIS 原文）装不下自适应多轮遍历——**演进证据已到** |
| F10 | 有界候选安全 / 再验证 / 穷尽 | ◐ | 单步验证屏障 ✅（StepVerify）；遍历策略= F9 同缺口 |

## 3. 合龙结论

1. **记录与不变量层无未登记缺口**（A–E 12/15 ✅，3 处 ◐ 均有登记）。
2. **两个登记项首次拿到实证买家**（本推导的核心产出）：
   - 多轮决策形状（决策契约组 TRACER_HYPOTHESIS）← F7/F9/F10；
   - association 策略迭代（归一化/重叠）← F8。
3. 恢复编排维持「HOST-001 直接后继」原判。
4. effect 词表扩展（LaunchApp/SetSwitch/scroll/PressBack）为 driver
   realization 层工作，不构成接口变更。

## 4. 对队列的影响（建议，待人裁决优先级）

- HOST-001（装配）后继候选队列更新：①恢复编排（原判）②多轮决策形状
  change（F9 证据喂入决策契约组冻结）③association 滚动连续性迭代（F8）。
  ②③为新增；顺序建议 ①→②（多轮形状是遍历/弹窗/恢复策略的共同前置）→③。

## 5. 本推导的限制

- 静态推导 + 既有测试引用；未运行 uni-agent 分支、未构造新场景测试；
- F 类判定基于考古报告的文件证据路径，未逐文件复核（报告方已区分
  「有运行证据」与「仅代码存在」）。
