# 基线复测 — WMP-001（P2）落地后（LAT-001 fixture 对照组职责）

> 对照基线：`evidence/2026-09-09-lat-001-baseline.md`（P0，fb8e45b 时点）
> 本次时点：HEAD `ec1bb657`（含 FCR-001 / DSE-003 / ADB-001 / WMP-001 /
> ADB-002）；fixture 零修改重跑（F8 全 corpus + FCR R8），计数断言全绿
> （LatencyBaseline 8/8 + FrameComputationReuse 9/9）。
> 性质：单次运行原始观测（同 P0 限制：JIT/GC/首调噪声；计数确定性可复现，
> ms 不可）。**不设门槛，只报告事实。**

## 1. 核心结论

**WMP-001（revision-bound 索引 + 精确 COW）对热点第一 reconcile 的收益
在 corpus 真实资产场景实测可辨**：

| 阶段（稳态场景） | P0 基线 | P2 后 | 变化 |
|---|---|---|---|
| WorldReconciliation · scroll01-v1 cold（55 inv） | 2.310 ms | **0.853 ms** | **-63%** |
| WorldReconciliation · scroll01-v2 cold（55 inv） | 2.014 ms | 0.742 ms | -63% |
| WorldReconciliation · nav03-parent cold（24 inv） | 0.304 ms | 0.214 ms | -30% |
| WorldReconciliation · popup01 cold（30 inv） | 0.463 ms | 0.348 ms | -25% |
| WorldReconciliation · popup04/09 cold（19 inv） | 0.210 ms | 0.164 ms | -22% |
| WorldReconciliation · warm 相（220 inv，165 幂等） | 3.981 ms | 2.826 ms | -29% |
| OccurrenceDerivation（strategy 缝，非 WMP 面） | ~持平 | ~持平 | 0（符合预期） |
| EvidenceAdmission / FastPerceptionStrategy / Emission | 持平 | 持平 | 0（符合预期） |

降幅随场景规模递增（19 obs -22% → 55 obs -63%）——与"索引固定成本 +
规模拐点"的 COW 语义一致。

## 2. 热点排序（P2 后）

reconcile 仍是第一（全程合计 ~6–7 ms vs occurrence ~5.6 ms 持平），但
差距显著收窄；occurrence derivation（WorldModel 外的 strategy 缝）与
perception 链未变。**下一个可辩护的结构性目标（若继续性能线）将是
occurrence derivation / 真实 provider 下的 strategy 面**——仅排序陈述，
非立项建议。

## 3. 噪声与不可声明项（诚实边界）

- **golden 首场景 reconcile 7.12 → 10.6 ms**：首跑 JIT 方差（单次 max
  9.4 ms 主导），非回归证据；单次观测限制与 P0 相同。
- **SliceDerivation 0.96→1.12 ms / CurrentGrounding 0.37→0.63 ms**
  （4/6 次调用，首调主导）：corpus 规模（≤16 occurrence）下 WMP 索引
  无可辨差异——其收益设计上在更大规模拐点后显现（见其 state 的规模
  拐点验收），本数据不构成正反结论。
- **FCR R8 端到端**：warm attempts 4→1 与 canonical 全等**稳定复现**
  （admissions=220 / newRevisions=55 逐相全等）；allStages 单次噪声
  剧烈（cold off=14.3/on=2.2 ms 系 off 侧先付 JIT；warm off=1.0/on=6.5 ms
  系反向噪声）——重申 P0/P1 结论：**corpus double 下端到端不可声明**。

## 4. 方法

`dotnet test --filter F8_Benchmark / R8_FourPhases --logger detailed`
（原始表见测试输出）；两次独立运行均绿后取单次誊录。fixture 与断言
零修改——计数断言（invocations / 规模 / canonical）全部通过，证明
WMP-001 未改变 canonical 行为（其自身深度等价验收的独立旁证）。
