# WMP-001 — World Model Internal Performance Evidence

## 1. Baseline / isolation

- Fixed point: `b7f430d15c0c6714039c8f47b55e14026722ae65` (`uni-harness`).
- Prerequisites: DSE-003=`db51140b`、ADB-001=`b7f430d1`，两者 state 均
  `closed` 且已提交。
- 启动时 WorldModel / Slice / ConsumerViews / UiEntityModel 零 dirty；主树仅有
  ADB-002、ADB 环境测试与分析/展示文档等 5 个无关 untracked。
- clean isolated baseline `/tmp/wmp001-preflight.h468FY/repo`：Kernel 231/231、
  Agent 17/17，全量 248/248 GREEN。

## 2. 实现边界

- `WorldRevisionIndex` 由每个 canonical revision 原子派生，仅存 container
  position、occurrence id/role/container、LogicalItem position、missing owner、
  conflict subject/bucket 与 dotted claim prefix bucket；不进入 revision、不暴露。
- evidence revision 对不变索引结构复用；container append、conflict append、
  LogicalItem append 增量更新；occurrence 替换仍按新 revision 全量建索引。
- `WorldState` / `EvidenceBasis` 使用 persistent storage；公开枚举时沿 parent
  Frozen publication 顺序 lazy materialize。该实现避免 reconciliation 每轮全量
  copy，同时保持旧版可观察枚举顺序。Graph/Conflicts/Containers/Relations/
  LogicalItems 使用 immutable COW；Occurrences 仍每 evidence revision 新派生。
- unresolved dotted prefix 也保留在索引：claim 先 accepted、container 后建立时，
  Slice 仍能看到该 claim；索引从不据此建立 container 或 belief。
- 既有 `RuntimeStage`/aggregate 不变；新增计数面全部为程序集内部 evidence seam。

## 3. Canonical 深度等价

同一 public-interface-only probe 源文件分别运行于 baseline clone 与 WMP 工作树：

| 场景 | baseline | WMP-001 | 结果 |
|---|---|---|---|
| reconcile 8 revisions | `f3e6b003…53a5` | `f3e6b003…53a5` | 全等 |
| reconcile 64 revisions | `c1dc8ce2…f4b1` | `c1dc8ce2…f4b1` | 全等 |
| reconcile 512 revisions | `ac95fb02…bde` | `ac95fb02…bde` | 全等 |
| real-asset cold/warm/partial/grounding（509,908 chars） | `acd28543…17a0` | `acd28543…17a0` | 全等 |
| 64 container-scoped claims Slice | `c2a5df1f…9c81` | `c2a5df1f…9c81` | 全等 |

哈希输入逐 revision 包含 revision/parent/number、WorldState、WorldGraph、
EvidenceBasis、Conflicts、Containers/Relations/Occurrences/LogicalItems；real-asset
另含 Slice occurrence 与 ResolveCurrent candidates。测试同时覆盖 fresh revision
拒绝 stale occurrence/binding、replay、EvidenceBasis、continuity 与 delayed-container。

## 4. 确定性扫描 / 复制规模

### Reconcile：唯一 claim 递增（N revisions）

旧实现每轮完整复制 state/graph/basis；新实现每轮只写 3 个 canonical change
（claim、graph key、evidence basis）。旧 scanned 为线性 `graph.Contains` 的累计
遍历；新 reconcile 与 index-build 分别各做 1 次 changed-entry lookup。

| N | copied baseline → WMP | 降幅 | scanned baseline → WMP（reconcile + index） | 降幅 |
|---:|---:|---:|---:|---:|
| 8 | 108 → 24 | 77.8% | 28 → 16 | 42.9% |
| 64 | 6,240 → 192 | 96.9% | 2,016 → 128 | 93.7% |
| 512 | 393,984 → 1,536 | 99.6% | 130,816 → 1,024 | 99.2% |

### Lookup / projection scenarios

| Phase | baseline scan | WMP scan | 说明 |
|---|---:|---:|---|
| ResolveCurrent（100 occurrences / 10 roles） | 100 | 10 | candidate 顺序逐项相同 |
| DeriveSlice（4 containers / 100 occurrences / scope=2） | 110 | 52 | 2 scope + 50 indexed occurrences；不扫 state |
| scoped claims（64 selected） | 131 | 65 | 1 scope + 64 indexed claims |
| Outcome consumer view（40 claims/40 conflicts/100 occurrences） | 180 | 52 | state 兼容扫描 40 + conflict 2 + role bucket 10 |
| Action assurance（同 corpus） | 最多 40 conflicts | 1 | conflict subject lookup |

- 1,000 demand 注册、hot lookup、尾删均为 point lookup；中段删除测试明确记录
  498 次 position shift，以保留公开 registry 顺序，不隐瞒该 O(n) 路径。
- 32 LogicalItem continuity append：index build 共 scanned=65、copied=97；若每个
  revision 重建 occurrence + item index 则为 528 scans。`ResolveContinuity` 因公开
  strategy seam 仍必须复制完整 candidates/existing-items，scanned=1,520、
  copied=1,552；本 change 未绕过该协议输入。

## 5. Allocation / timing（预热后 5 次中位数）

### Reconcile total（internal metrics 关闭；ticks 为 Stopwatch ticks）

| N | allocated bytes baseline → WMP | 降幅 | wall ticks baseline → WMP | 变化 |
|---:|---:|---:|---:|---:|
| 8 | 39,648 → 36,160 | 8.8% | 23,417 → 21,292 | -9.1% |
| 64 | 918,904 → 302,928 | 67.0% | 766,625 → 182,542 | -76.2% |
| 512 | 43,582,408 → 2,695,880 | 93.8% | 86,409,625 → 2,116,334 | -97.6% |

WMP metrics 开启时，N=8/64/512 的 instrumented allocations 为
36,752 / 303,520 / 2,696,472 bytes，stage ticks 为
18,084 / 121,252 / 1,238,782。internal operation probe 另记录：

| operation | corpus | scanned | copied | allocated bytes | ticks |
|---|---|---:|---:|---:|---:|
| ResolveCurrent | 100/10 roles | 10 | 10 | 2,272 | 15,042 |
| DeriveSlice occurrences | 4 containers/100 occ | 52 | 52 | 8,864 | 242,208 |
| DeriveSlice scoped claims | 64 claims | 65 | 65 | 17,976 | 414,333 |
| Consumer views | 40/40/100 | 53 | 6 | 182,016 | 2,583,292 |
| Continuity index build | 32 items | 65 | 97 | 19,552 | 40,586 |
| ResolveContinuity | 32 items | 1,520 | 1,552 | 220,496 | 1,349,749 |

operation allocation/ticks 是单次测试运行事实，含 lazy canonical projection 与
观测器成本；只作辅助证据，不设门槛、不与嵌套阶段相加。

### Occurrence index 固定成本 / 拐点

一次 512-occurrence revision 后的读取数：

| reads | allocated baseline → WMP | wall ticks baseline → WMP | 裁决 |
|---:|---:|---:|---|
| 0 | 122,784 → 172,928 | 58,250 → 157,750 | index 固定成本明显不利 |
| 8 | 182,320 → 232,464 | 103,625 → 193,458 | 尚未回本 |
| 64 | 599,032 → 649,176 | 434,417 → 376,166 | 耗时约 -13.4%，开始回本 |

8/64 occurrence 输入也呈相同形状：0–8 reads 不利，约 64 reads 时本次样本耗时
开始回本。只读查询输出分配不变，index 自身约增加 5–50 KB/revision，因此
allocation 不会靠更多 reads 回本。确定性扫描的理论拐点更早（build=N，查询由
N 降至 N/8），但实际常数项使耗时拐点显著后移；两者均如实保留。

64 scoped-claim Slice 单次派生：allocation 36,384→12,704 bytes（-65.1%），
wall 21,833→20,875 ticks（-4.4%，仅辅助）。

### LAT-001 固定场景复跑（单次，辅助）

既有 baseline 文档与 WMP 复跑的 WorldReconciliation totalMs：
golden 7.120→0.660、scroll-v1 2.310→0.626、scroll-v2 2.014→0.606、
warm 3.981→1.301、grounding 0.763→0.221。运行/机器虽相同，但两边都是单次且
包含 JIT/GC，故仅说明方向，不作为确定性验收；invocation/input/output 定义未改。

## 6. REVIEW / VERIFY

- Standards review：PASS。首次发现 Change State 滞后与 delayed-container 漏索引
  风险，均修复后复审通过。保留的 non-blocking judgement：dictionary/set 两个
  private lazy-freeze wrapper 有少量形状重复；为消重引入泛型状态机收益不足。
- Spec review：PASS。首次指出 index copied 低报、Slice 仍扫 state、中段 demand
  删除覆盖不足；全部修复并复审。无 scope creep。
- WMP focused：13/13 GREEN；benchmark/canonical probe：17/17 GREEN；LAT-001：
  8/8 GREEN；最终主树排除他人 ADB 环境测试后 Kernel 261/261 + Agent 17/17。
- WMP 精确提交树 clean isolated checkout 全量：Kernel 261/261、Agent 17/17，
  合计 278/278 GREEN。
- 主树直接全量仅失败 1 个无关 untracked `AdbEnvironmentTests`：本机无
  `emulator-5554`。该文件不由 WMP-001 拥有、不提交；最终以 clean isolated
  WMP HEAD 全量结果为准。
