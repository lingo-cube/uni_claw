# World Model Consistency — diagnostic route (WMP-002 / DBG-001)

> 该 reference 属 `uniclaw-debug-evidence`（证据语义扩展）。通用诊断循环
> （tight loop → reproduce → falsifiable hypotheses → minimal fix）由上游
> `diagnosing-bugs` 唯一拥有；本文件只回答「World Model 一致性怀疑取什么
> 证据、跑哪一条命令、结果怎么解释」。不定义任何 lifecycle 或完成判定
> （UniFlow 唯一拥有）。

## 1. 何时走这条路由

症状命中任一即适用（关键词 → 归属）：

| 症状 | 归属面 |
|---|---|
| `ResolveCurrent` / `DeriveSlice` / `DeriveBindingView` / consumer view 结果与 `Current` 公开集合的朴素扫描不一致 | `WorldRevisionIndex`（owner-internal 查询投影） |
| `WorldState` / `EvidenceBasis` 枚举顺序、内容跨 revision 漂移；历史 revision 「变了」 | `PersistentRevisionCollections` canonical publication |
| stale occurrence / stale binding / anchor fail-closed 误判 | revision-local occurrence 语义 + 索引 |
| replay / counterfactual 重建与原运行 hash 不一致 | reconciliation 确定性（evidence id 铸造、strategy 确定性） |
| WMP 性能回退（reconcile/首枚举/retained memory） | WMP-002 materialization/performance probe |

## 2. 唯一命令（agent-runnable）

```bash
bash .agents/skills/uniclaw-debug-evidence/scripts/world-model-consistency.sh
```

- 任意 cwd 可运行（脚本自定位仓库根）；调用确定性 World Model 测试集
  （WMP-002：canonical oracle + materialization probe + performance +
  benchmark probe；WMP-003：容器索引不变式 + stale-position canary）。
- exit 0 = GREEN；任一 mismatch → 非零，输出中搜
  `WMP-DIVERGENCE schema=wmp-canonical-oracle/1 op=… owner=… rev=… key=…
  expectedCount=… actualCount=… firstDivergentPosition=… expected=… actual=…`。
- 依赖：本地 `dotnet` SDK；命令以 detailed verbosity 运行，因此 probe 证据行
  （`WMP-MAT` / `WMP-STAGE` / `WMP-ORACLE` / `WMP-PROBE`）与测试输出原样可见。
  `dotnet restore` 只消费已声明的 NuGet 包——可能尝试 NuGet 漏洞索引查询
  （NU1900 警告，离线时仍成功），除此之外不访问任何远程诊断数据源；不点击、
  无 Host session 状态、不依赖未提交文件、不修改产品状态（仅 bin/obj 构建产物）。

## 3. 诊断顺序（compose into diagnosing-bugs）

1. 先按 `diagnosing-bugs` Phase 1 建立 tight loop——本命令就是那条
   red-capable 回路：单命令、确定性、秒级、无人工。**先跑命令，再读码。**
2. RED → FDP 行直接给出首次分歧（operation / revision / lookup key /
   expected vs actual / owner）。按 §1 表定位归属面，再进 hypotheses。
3. GREEN → 只能排除**已覆盖 fixture** 上的索引/COW/顺序分歧（scale
   8/64/512、real-asset cold/warm/partial/grounding、golden hash、replay、
   invariance、sabotage 六类已验证）。不能证明不存在未知错误；此时怀疑
   应转向 fixture 之外（未覆盖的调用形态 / 消费方逻辑 / 环境差异）。

## 4. 证据等级（E0-E4 裁决）

- World Model 是 stateful module：**最低 E2**（状态快照 = revision 公开集合
  渲染 + 操作历史 = evidence 序列）。
- 问题进入 Runtime / Grounding 链（perception → admission → reconcile →
  consumer view 的任一跨组件环节）→ **升 E3**（trace + 状态转移 + 观测 +
  决策记录）。
- 性能问题：不要用普通日志推断；先跑固定 probe（本命令含 materialization
  /performance 集；WMP-002 evidence 有基线数字可对照），再看 `WMP-MAT` /
  `WMP-STAGE` 行。

## 5. Evidence packet 映射

RED 时 FDP 行 → packet 字段：
- **Expected Reality** = expected=…（naive canonical scan 之值）
- **Observed Reality** = actual=…（优化路径实值）
- **Reality Gap** = op + firstDivergentPosition + expectedCount/actualCount
- **Evidence Reference** = 命令 + 失败测试名 + FDP 行原文
- **First Divergence** = firstDivergentPosition 处的 expected/actual identity
- **Owner** = owner= 字段（最小 owning seam）
- lifecycle stage = reconcile / consumer-view / replay（op 推断）；last
  correct state = FDP 位置之前的最后一个一致条目；failure class 默认 B
  （grounding/occurrence 解析失败域），纯顺序/内容漂移按实际归类；
  remaining uncertainty = GREEN 语义边界（§3.3）；escalation = 若证据指向
  需改 runtime 架构 / authority / 跨 seam 所有权 → 按 SKILL §4 停止扩展取证
  并携带 escalation 返回。

## 6. 纪律

- 临时日志必须 `[DEBUG-<id>]` 前缀，完成前删除（diagnosing-bugs Phase 6）。
- 不得为诊断扩大 `WorldModel` 公开接口、不得在 src/ 加诊断分支或第二套
  实现（WMP-002 边界）；不得让诊断逻辑参与 belief/revision/replay/Assurance/
  Effect 决策。
- 不得据此 reference 声明任何完成状态；完成判定归 UniFlow。
