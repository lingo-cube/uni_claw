# 0018 — canonical 集合枚举顺序 = 增量 frozen 链契约（WMP 系实现边界）

WMP-001 把 WorldState/EvidenceBasis 的出版从「每 revision 全量复制 +
`ToFrozenDictionary`」改为 persistent COW + lazy canonical projection。该优化
的正确性边界在 WMP-002 被实证刻画，且边界比直觉窄得多——不写成 ADR，任何
后续触碰出版的实现都会重新踩坑。

三项实证事实（WMP-002 evidence §3，经 oracle 差分验证）：

1. 公开枚举顺序**不是插入序**：`FrozenDictionary`/`FrozenSet` 按 bucket 内部
   布局输出（rev-6 的 WorldState 首位是 claim-3，不是 claim-0）。
2. frozen 布局**不是 key set 的纯函数**：n=8/64 呈输入序无关，n=20/512 对
   构造输入序列敏感（reversed/sorted/shuffled 输入产生不同布局）。
3. n=512 时**增量链布局 ≠ 任何 fresh 重建**——链上每一级的输入是 parent 的
   materialized 布局加新键，逐级 re-freeze。

## Decision

```text
canonical 公开枚举顺序的契约定义：
  order(root)  = root 新键序列的 frozen 布局
  order(child) = frozenLayout(order(parent) ++ child 新键)
即顺序只能由「parent materialized 布局 + 本级新增」的构造序列增量定义。

推论（实现边界）：
- 任何「从 ImmutableDictionary storage / 共享 order spine / 任意序的 key set
  直接重建 frozen 视图」的实现都改变公开顺序（fresh ≠ 链）→ 禁止。
- 子视图构建必须消费 parent 的 materialized 布局（当前实现：
  PersistentRevisionCollections 的 transient 祖先计算，WMP-002）。
- 祖先视图只在构建过程中临时计算、永不缓存：保留量 ∝ 实际被枚举的
  revision（WMP-002：512 revisions 触碰 Current 的永久保留 12.1MB → ≈0；
  首枚举 O(N·n) 链式工作为该契约的内在代价，eager 替代 = WMP-001 已拒绝
  的 reconcile 回归）。
- 测试侧独立复算顺序的唯一合规方法 = 用 BCL frozen 集合按驱动已知 delta
  维护同一条增量链（同 overload、同输入序列；WMP-002 oracle 的做法）。
```

配套不变式（WMP-003 固化）：容器索引 count-stable 快路径（count 相等 ⇒
整表复用 parent `ContainerPositions`）依赖「容器集合 append-only / Matched
只改 basis 不动位置」这一公开流不变式；内部 canary 固化其现状，未来引入
容器替换/终止流时必须有意识地处理该分支。

## Considered Options

- **结构共享 order spine（append-only 键序列）直接枚举或据此重建**：被拒——
  spine 序 ≠ frozen 布局（事实 2/3），会改变公开顺序、破 golden hash。
- **每 revision eager 物化布局**：被拒——把 O(Σ) 成本移回 reconcile，
  等价回退 WMP-001 的核心收益（43.5MB → 2.7MB@512 的消除）。
- **改契约为插入序（换一种确定顺序）**：被拒——公开可观察顺序是 WMP-001
  冻结的兼容面（baseline 同构，evidence §3.1），golden hash 与全部既有证据
  锚定其上；WMP-001 D4 冻结的「不以性能/实现便利换顺序语义」原则同样
  适用于反向换序。
- **纯共享 spine 直接枚举（无 frozen）**：被拒（WMP-002 Alternatives）——
  每次枚举每键 AVL 查找 O(n log n)，热路径回归风险大于收益。

## Consequences

- 触碰 WorldState/EvidenceBasis 出版、`PersistentRevisionCollections`、或
  任何「重建视图」路径的 change，必须先读本 ADR；oracle（WMP-002）与
  golden hash 是违例的自动检测器。
- lazy 链的首枚举链式尾延迟是**已接受的设计事实**（非缺陷，
  WMP-002 evidence §5）；放弃本契约不是其出路。
- BCL frozen 内部布局变化（运行时升级）会同时移动生产与 oracle 两侧
  （同 overload 同输入序列），golden hash 会大声断裂提醒复核。
- 本 ADR 不改变任何公开接口、authority 或 replay 语义；仅冻结 realization
  边界。
