---
status: accepted
date: 2026-09-20
---

# 0026 — Kernel 是 UI 的领域运行时；运行时泛化 buyer-driven；组件化判据是边界执法而非包数量

问题：Kernel 应聚焦 UI，还是抽取一个「依赖于 Core World 的通用运行
时」再在其上实现 UI 运行时？（2026-09-20 提出；同日裁决，台账事件 #9。）

## 决策

1. **领域运行时（Domain Runtime）**。UniClaw.Kernel 是某一个领域
   「自主动手 + 逐步验证」的运行时机制集合（admission → belief →
   intent → grounding → assurance → gate → dispatch → serial
   verification → terminal proof）。当前唯一领域实现 = UI：
   World/UiRealization 留在 Kernel 包内（模块边界由
   UiRealizationBoundaryTests 执法），**不拆独立包**——单消费者包
   没有买家；NO_REAL_BUYER 同样适用于我们自己的拆包提案。
2. **跨领域层 = Core 记录，不是运行时**。Core 五记录 + 三内部结构是
   唯一泛化层，已双域验证（UI 投影缝 + 文件系统直连，CORE-016）。
   不需要运行时机制的 realization 直连 Core、不经 Kernel。
3. **运行时泛化的开门条件（buyer-driven）**：第二个需要运行时机制
   （验证屏障 / 新鲜度门 / Run 义务）的非 UI 领域出现——例如文件
   agent 要「移动前确认快照够新、移动后验证再动下一步」。抽取方法与
   Core 记录层同配方：让该领域先手搓一个循环，与 UI 运行时 **diff**
   出 SPI 面。**抽象来自两个真实实现的对比，不来自单一实现的外推**
   （n=1 抽象会把偶然细节当本质冻进契约层）。
4. **组件化判据**：边界的清晰与可执法（测试 / 编译级）＞ 包数量。
   单消费者的物理拆包是仪式，不是分离。

## 论据（为什么现在不抽）

- 机制骨架（入证 / Run / 门 / 屏障相位机）确实领域中立，但它们运转
  所依赖的协议不变量全部 UI 词汇化（same-referent、容器、接地）；
  抽通用层 = 重推导冻结协议语义，而验证重推导的领域只有一个。
- 唯一的非 UI 领域（文件系统）已实证**不需要** Kernel 级机制。
- 本仓库有成本先例：记录层泛化（Core）耗费 CORE-001..016 十六个
  change 且需要两个域验证才站稳；运行时层重复该实验，目前没有第二个
  域陪跑。

## Consequences

- 术语 **Domain Runtime（领域运行时）** 入 CONTEXT.md（含 _Avoid_:
  通用运行时 / god runtime）。
- 修正记录：CORE-016 **并未**满足 ADR-0024/CORE-015 的程序集升格条件
  （它消费 Core 记录，不消费 UiRealization 类型）；2026-09-20 前的
  「条件已满足」表述为误记。升格条件（UiRealization 类型的第二个
  消费者）保持记录；若本 ADR 第 3 条的通用运行时抽取发生，包边界将
  按该领域重新切割，componentization-map §2 的依赖记录（①②）届时
  重新生效。
- 「完全分离完成」终点线 = Core ✓ + Agent ✓ + Kernel（UI 领域运行时，
  模块边界执法）✓ + **Host 落地**（HOST-001）。
- 本 ADR 由「决策被推翻」（World 拆包提案被人否决）与「新术语定型」
  两条 LEARN-hook 触发落档。
